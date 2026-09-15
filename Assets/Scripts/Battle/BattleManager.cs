using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using AutoChess.Bullet;
using AutoChess.CircularDamage;
using AutoChess.Navigation;
using AutoChess.Render;
using AutoChess.Utils;
using BehaviorTree;
using Camp;
using Chess;
using Config;
using UnityEngine;

namespace AutoChess
{
    /// <summary>
    /// 管理战斗状态专属线程，并通过纯数据快照与主线程导航调度器和渲染管理器通信。
    /// </summary>
    public sealed class BattleManager : IDisposable
    {
        /// <summary>获取阵营管理器。</summary>
        public CampManager CampManager => _campManager;

        /// <summary>获取当前战斗棋盘。</summary>
        public ChessBoard ChessBoard => _chessBoard;

        /// <summary>获取子弹管理器。</summary>
        public BulletManager BulletManager => _bulletManager;

        /// <summary>获取圆形范围伤害管理器。</summary>
        public CircularDamageManager CircularDamageManager => _circularDamageManager;

        /// <summary>获取棋子配置仓库。</summary>
        public ChessConfigRepository Configs => _configs;

        /// <summary>获取行为树定义仓库。</summary>
        public BehaviorTreeRepository BehaviorTrees => _behaviorTrees;

        /// <summary>战斗固定步长。</summary>
        public float FixedStep { get; }

        /// <summary>战斗线程是否已启动且仍在运行。</summary>
        public bool IsRunning => _worker != null && _worker.IsAlive;

        private readonly CampManager _campManager;
        private readonly ChessConfigRepository _configs;
        /// <summary>按名称索引的行为树定义仓库。</summary>
        private readonly BehaviorTreeRepository _behaviorTrees;
        private readonly ChessBoard _chessBoard;
        private readonly BulletManager _bulletManager;
        /// <summary>延迟圆形范围伤害管理器。</summary>
        private readonly CircularDamageManager _circularDamageManager;
        private readonly ConcurrentQueue<Action> _commands = new ConcurrentQueue<Action>();
        private readonly ConcurrentQueue<NavigationInputSnapshot> _navigationInputs =
            new ConcurrentQueue<NavigationInputSnapshot>();
        private readonly ConcurrentQueue<NavigationResultSnapshot> _navigationResults =
            new ConcurrentQueue<NavigationResultSnapshot>();
        private readonly List<NavigationResultSnapshot> _pendingResults =
            new List<NavigationResultSnapshot>();
        /// <summary>战斗线程发布、主线程消费的最新渲染帧。</summary>
        private BattleRenderFrame _latestRenderFrame;
        private readonly AutoResetEvent _advanceSignal = new AutoResetEvent(false);
        private readonly object _gate = new object();
        private Thread _worker;
        private bool _started;
        private bool _stopRequested;
        private bool _resourcesDisposed;
        private int _pendingSteps;
        private long _stepId;
        private bool _disposed;

        /// <summary>创建战斗管理器及其战斗子系统。</summary>
        public BattleManager(float fixedStep = 1f / 60f)
            : this(fixedStep, null)
        {
        }

        /// <summary>创建战斗管理器并注入棋子配置与行为树仓库；空仓库不从 Resources 加载。</summary>
        public BattleManager(
            float fixedStep,
            ChessConfigRepository configs,
            BehaviorTreeRepository behaviorTrees = null)
        {
            if (fixedStep <= 0f || float.IsNaN(fixedStep) || float.IsInfinity(fixedStep))
            {
                throw new ArgumentOutOfRangeException(nameof(fixedStep));
            }

            FixedStep = fixedStep;
            _configs = configs ?? new ChessConfigRepository();
            _behaviorTrees = behaviorTrees ?? new BehaviorTreeRepository();
            _campManager = new CampManager(_configs, _behaviorTrees);
            _chessBoard = new ChessBoard();
            _bulletManager = new BulletManager();
            _circularDamageManager = new CircularDamageManager(_campManager);
        }

        /// <summary>启动唯一的战斗工作线程。</summary>
        public void Start()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(BattleManager));
                }

                if (_started)
                {
                    throw new InvalidOperationException("BattleManager 不能重复启动。");
                }

                _started = true;
                _worker = new Thread(RunWorker)
                {
                    IsBackground = true,
                    Name = "AutoChess.BattleManager"
                };
                _worker.Start();
            }
        }

        /// <summary>请求战斗线程推进指定数量的固定步。</summary>
        public void RequestAdvance(int stepCount)
        {
            if (stepCount <= 0)
            {
                return;
            }

            lock (_gate)
            {
                EnsureStarted();
                if (_stopRequested)
                {
                    return;
                }

                _pendingSteps = Math.Min(4, _pendingSteps + stepCount);
            }

            _advanceSignal.Set();
        }

        /// <summary>兼容旧调用方的同步 Tick；正式运行时请由 GameManager 请求固定步。</summary>
        [Obsolete("运行时应由 GameManager.RequestAdvance 驱动 BattleManager。")]
        public void Tick(float deltaTime)
        {
            if (!_started)
            {
                float step = Math.Max(0f, deltaTime);
                _bulletManager.Tick(step);
                _circularDamageManager.Tick(step);
                return;
            }

            int steps = (int)Math.Floor(Math.Max(0f, deltaTime) / FixedStep);
            if (steps > 0)
            {
                RequestAdvance(Math.Min(4, steps));
            }
        }

        /// <summary>按 ConfigId 在线程安全队列中创建棋子，立即返回 UniqueId。</summary>
        public int RegisterChess(int campId, int configId, Vector2 position)
        {
            return RegisterChess(campId, configId, position, default);
        }

        /// <summary>按 ConfigId 创建棋子并绑定导航句柄。</summary>
        public int RegisterChess(int campId, int configId, Vector2 position, NavigationAgentHandle agent)
        {
            if (!TryValidateConfig(configId))
            {
                return 0;
            }

            return RegisterChess(campId, configId, ChessController.AllocateUniqueId(), position, agent);
        }

        /// <summary>使用预分配 UniqueId 创建棋子，供 GameManager 立即建视图。</summary>
        public int RegisterChess(
            int campId,
            int configId,
            int uniqueId,
            Vector2 position,
            NavigationAgentHandle agent)
        {
            if (uniqueId <= 0 || !TryValidateConfig(configId))
            {
                return 0;
            }

            EnqueueOrRun(() =>
            {
                ChessController chess = _campManager.RegisterChess(campId, configId, uniqueId, position);
                if (chess != null && agent.IsValid)
                {
                    chess.InitializeMovement(_chessBoard, agent);
                }
            });
            return uniqueId;
        }

        /// <summary>按 UniqueId 查找棋子；仅在未启动或战斗线程上安全。</summary>
        public bool TryGetChess(int uniqueId, out ChessController chess)
        {
            return _campManager.TryGetChess(uniqueId, out chess);
        }

        /// <summary>按 UniqueId 在线程安全队列中注销棋子。</summary>
        public void UnregisterChess(int uniqueId)
        {
            EnqueueOrRun(() => _campManager.UnregisterChess(uniqueId));
        }

        /// <summary>在线程安全队列中注销棋子。</summary>
        public void UnregisterChess(ChessController chess)
        {
            EnqueueOrRun(() => _campManager.UnregisterChess(chess));
        }

        /// <summary>
        /// 立刻拷贝受影响阵营后注册圆形范围伤害；非法参数返回 false。
        /// </summary>
        public bool RegisterCircularDamage(
            Vector2 center,
            float radius,
            float delay,
            IEnumerable<int> affectedCamps,
            int damage)
        {
            if (!AutoChess.CircularDamage.CircularDamageManager.AreParametersValid(
                    center, radius, delay, damage)
                || !AutoChess.CircularDamage.CircularDamageManager.TryCopyAffectedCamps(
                    affectedCamps, out HashSet<int> camps))
            {
                return false;
            }

            EnqueueOrRun(() => _circularDamageManager.Register(center, radius, delay, camps, damage));
            return true;
        }

        /// <summary>校验 ConfigId 是否已注册，缺失时记录错误。</summary>
        private bool TryValidateConfig(int configId)
        {
            if (_configs.TryGet(configId, out ChessBaseData data) && data != null)
            {
                return true;
            }

            ThreadLogQueue.Enqueue(ThreadLogLevel.Error,
                "BattleManager 找不到 ConfigId=" + configId + " 的棋子配置。");
            return false;
        }

        /// <summary>提交主线程 RVO 结果，供下一个战斗固定步消费。</summary>
        public void SubmitNavigationResults(NavigationResultSnapshot result)
        {
            if (!_disposed)
            {
                _navigationResults.Enqueue(result);
                _advanceSignal.Set();
            }
        }

        /// <summary>批量提交主线程 RVO 结果。</summary>
        public void SubmitNavigationResults(IReadOnlyList<NavigationResultSnapshot> results)
        {
            if (results == null)
            {
                return;
            }

            for (int i = 0; i < results.Count; i++)
            {
                SubmitNavigationResults(results[i]);
            }
        }

        /// <summary>取出最新一份渲染帧；同一时刻只保留最新步，允许丢弃未消费的旧帧。</summary>
        public bool TryConsumeRenderFrame(out BattleRenderFrame frame)
        {
            frame = Interlocked.Exchange(ref _latestRenderFrame, null);
            return frame != null;
        }

        /// <summary>取出一条完整导航输入记录，供 GameManager 按步组批处理。</summary>
        public bool TryDequeueNavigationInput(out NavigationInputSnapshot snapshot)
        {
            return _navigationInputs.TryDequeue(out snapshot);
        }

        /// <summary>停止战斗线程并在指定时间内等待其退出。</summary>
        public bool Stop(TimeSpan timeout)
        {
            Thread worker;
            lock (_gate)
            {
                if (!_started)
                {
                    return true;
                }

                _stopRequested = true;
                worker = _worker;
            }

            _advanceSignal.Set();
            bool stopped = worker == null || worker.Join(timeout);
            return stopped;
        }

        /// <summary>停止战斗线程并释放战斗资源。</summary>
        public void Dispose()
        {
            if (_resourcesDisposed)
            {
                return;
            }

            bool stopped = Stop(TimeSpan.FromSeconds(2));
            if (stopped)
            {
                _bulletManager.Dispose();
                _circularDamageManager.Dispose();
                _advanceSignal.Dispose();
                _resourcesDisposed = true;
                _disposed = true;
            }
        }

        /// <summary>战斗线程等待步进信号并执行固定步。</summary>
        private void RunWorker()
        {
            try
            {
                while (true)
                {
                    _advanceSignal.WaitOne();
                    while (true)
                    {
                        ExecutePendingCommands();
                        lock (_gate)
                        {
                            if (_stopRequested)
                            {
                                return;
                            }

                            if (_pendingSteps <= 0)
                            {
                                break;
                            }

                            _pendingSteps--;
                        }

                        RunStep();
                    }
                }
            }
            catch (Exception exception)
            {
                ThreadLogQueue.Enqueue(ThreadLogLevel.Error,
                    $"BattleManager 工作线程异常：{exception}");
            }
            finally
            {
                try
                {
                    ExecutePendingCommands();
                }
                catch (Exception exception)
                {
                    ThreadLogQueue.Enqueue(ThreadLogLevel.Error,
                        $"BattleManager 释放战斗资源失败：{exception}");
                }
            }
        }

        /// <summary>在线程内执行排队的注册、注销等战斗命令。</summary>
        private void ExecutePendingCommands()
        {
            while (_commands.TryDequeue(out Action command))
            {
                try
                {
                    command();
                }
                catch (Exception exception)
                {
                    ThreadLogQueue.Enqueue(ThreadLogLevel.Error,
                        $"BattleManager 命令执行失败：{exception}");
                }
            }
        }

        /// <summary>执行一个固定战斗步并发布导航输入与渲染快照。</summary>
        private void RunStep()
        {
            long currentStep = ++_stepId;
            ApplyPendingNavigationResults(currentStep);
            _campManager.Tick(FixedStep);
            _bulletManager.Tick(FixedStep);
            _circularDamageManager.Tick(FixedStep);
            PublishNavigationInputs(currentStep);
            PublishRenderFrame(currentStep);
        }

        /// <summary>按稳定代理顺序发布当前步的导航输入；无代理时仍发送结束标记。</summary>
        private void PublishNavigationInputs(long currentStep)
        {
            List<NavigationInputSnapshot> inputs = new List<NavigationInputSnapshot>();
            foreach (ChessController chess in _campManager.GetAllChesses())
            {
                NavigationInputSnapshot input = chess.BuildNavigationInput(currentStep);
                if (input.Agent.IsValid)
                {
                    inputs.Add(input);
                }
            }

            inputs.Sort((left, right) => left.Agent.AgentId.CompareTo(right.Agent.AgentId));
            if (inputs.Count == 0)
            {
                _navigationInputs.Enqueue(new NavigationInputSnapshot(
                    currentStep,
                    NavigationAgentHandle.Invalid,
                    default,
                    default,
                    0f,
                    0f,
                    false,
                    0,
                    true));
                return;
            }

            for (int i = 0; i < inputs.Count; i++)
            {
                NavigationInputSnapshot input = inputs[i];
                _navigationInputs.Enqueue(new NavigationInputSnapshot(
                    input.StepId,
                    input.Agent,
                    input.Position,
                    input.TargetPoint,
                    input.DesiredSpeed,
                    input.MaxSpeed,
                    input.Enabled,
                    input.CommandVersion,
                    i == inputs.Count - 1));
            }
        }

        /// <summary>拷贝存活棋子和活动子弹为不可变渲染帧，并只保留最新一份。</summary>
        private void PublishRenderFrame(long currentStep)
        {
            List<ChessRenderSnapshot> chessSnapshots = new List<ChessRenderSnapshot>();
            foreach (ChessController chess in _campManager.GetAllChesses())
            {
                if (chess == null || !chess.IsAlive)
                {
                    continue;
                }

                chessSnapshots.Add(chess.CreateRenderSnapshot());
            }

            List<BulletRenderSnapshot> bulletSnapshots = new List<BulletRenderSnapshot>();
            foreach (BulletRuntimeData bullet in _bulletManager.Bullets)
            {
                if (bullet == null || bullet.BulletId <= 0)
                {
                    continue;
                }

                bulletSnapshots.Add(bullet.CreateRenderSnapshot());
            }

            BattleRenderFrame frame = new BattleRenderFrame(
                currentStep,
                chessSnapshots.ToArray(),
                bulletSnapshots.ToArray());
            Interlocked.Exchange(ref _latestRenderFrame, frame);
        }

        /// <summary>只应用早于当前战斗步的最新 RVO 结果，避免旧结果覆盖新命令。</summary>
        private void ApplyPendingNavigationResults(long currentStep)
        {
            while (_navigationResults.TryDequeue(out NavigationResultSnapshot result))
            {
                _pendingResults.Add(result);
            }

            Dictionary<int, NavigationResultSnapshot> latestByAgent =
                new Dictionary<int, NavigationResultSnapshot>();
            List<NavigationResultSnapshot> futureResults = new List<NavigationResultSnapshot>();
            for (int i = 0; i < _pendingResults.Count; i++)
            {
                NavigationResultSnapshot result = _pendingResults[i];
                if (result.StepId >= currentStep)
                {
                    futureResults.Add(result);
                    continue;
                }

                if (!result.Agent.IsValid)
                {
                    continue;
                }

                if (!latestByAgent.TryGetValue(result.Agent.AgentId, out NavigationResultSnapshot existing)
                    || result.StepId >= existing.StepId)
                {
                    latestByAgent[result.Agent.AgentId] = result;
                }
            }

            foreach (ChessController chess in _campManager.GetAllChesses())
            {
                int agentId = chess.MovementController == null
                    ? 0
                    : chess.MovementController.Agent.AgentId;
                if (agentId > 0 && latestByAgent.TryGetValue(agentId, out NavigationResultSnapshot result))
                {
                    chess.ApplyNavigationResult(result);
                }
            }

            _pendingResults.Clear();
            _pendingResults.AddRange(futureResults);
        }

        /// <summary>将命令直接执行在未启动阶段，启动后转入战斗线程。</summary>
        private void EnqueueOrRun(Action command)
        {
            lock (_gate)
            {
                if (_disposed || _stopRequested)
                {
                    throw new ObjectDisposedException(nameof(BattleManager));
                }

                if (!_started)
                {
                    command();
                    return;
                }
            }

            _commands.Enqueue(command);
            _advanceSignal.Set();
        }

        /// <summary>确保 BattleManager 已经启动。</summary>
        private void EnsureStarted()
        {
            if (!_started)
            {
                throw new InvalidOperationException("BattleManager 尚未启动。");
            }
        }
    }
}
