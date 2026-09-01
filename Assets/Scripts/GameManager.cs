using System;
using System.Collections.Generic;
using System.Threading;
using AutoChess.Navigation;
using AutoChess.Utils;
using Chess;
using UnityEngine;

namespace AutoChess
{
    /// <summary>
    /// 全局 Unity 主线程管理器，负责 RVO Job 调度、战斗线程时钟和快照交换。
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        private const float DefaultFixedStep = 1f / 60f;
        private const int MaxCatchUpSteps = 4;

        private static GameManager _instance;
        private static int _knownMainThreadId;
        private readonly Dictionary<long, List<NavigationInputSnapshot>> _inputBatches =
            new Dictionary<long, List<NavigationInputSnapshot>>();
        private readonly Dictionary<ChessController, NavigationAgentHandle> _agentByChess =
            new Dictionary<ChessController, NavigationAgentHandle>();
        private float _timeAccumulator;
        private int _mainThreadId;
        private bool _initialized;
        private bool _destroying;

        /// <summary>获取全局 GameManager，只允许 Unity 主线程懒创建。</summary>
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (_knownMainThreadId != 0
                        && Thread.CurrentThread.ManagedThreadId != _knownMainThreadId)
                    {
                        throw new InvalidOperationException("GameManager.Instance 只能从 Unity 主线程访问。");
                    }

                    GameObject go = new GameObject("GameManager");
                    _instance = go.AddComponent<GameManager>();
                    DontDestroyOnLoad(go);
                }

                return _instance;
            }
        }

        /// <summary>获取战斗管理器。</summary>
        public BattleManager BattleManager { get; private set; }

        /// <summary>获取主线程导航管理器。</summary>
        public NavigationManager NavigationManager { get; private set; }

        /// <summary>Unity 生命周期初始化战斗、导航和固定步时钟。</summary>
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _knownMainThreadId = _mainThreadId;
            DontDestroyOnLoad(gameObject);
            BattleManager = new BattleManager(DefaultFixedStep);
            NavigationManager = new NavigationManager(BattleManager.ChessBoard);
            BattleManager.Start();
            _initialized = true;
        }

        /// <summary>按 Unity 帧时间推进固定步时钟并调度主线程 RVO。</summary>
        private void Update()
        {
            if (!_initialized || _destroying)
            {
                return;
            }

            EnsureMainThread();
            DrainNavigationInputs();

            float deltaTime = Math.Max(0f, Time.deltaTime);
            _timeAccumulator += deltaTime;
            int stepCount = Math.Min(MaxCatchUpSteps,
                (int)Math.Floor(_timeAccumulator / BattleManager.FixedStep));
            if (stepCount > 0)
            {
                _timeAccumulator -= stepCount * BattleManager.FixedStep;
                if (stepCount == MaxCatchUpSteps && _timeAccumulator >= BattleManager.FixedStep)
                {
                    _timeAccumulator = 0f;
                    ThreadLogQueue.Enqueue(ThreadLogLevel.Warning,
                        "GameManager 丢弃了超出最大追赶步数的战斗时间。");
                }

                BattleManager.RequestAdvance(stepCount);
            }

            FlushThreadLogs();
        }

        /// <summary>在主线程注册棋子导航代理并排队加入战斗阵营。</summary>
        public NavigationAgentHandle RegisterChess(int campId, int chessId, ChessController chess)
        {
            EnsureMainThread();
            if (NavigationManager == null || BattleManager == null || chess == null)
            {
                return NavigationAgentHandle.Invalid;
            }

            if (chess.RuntimeData == null)
            {
                return NavigationAgentHandle.Invalid;
            }

            if (_agentByChess.TryGetValue(chess, out NavigationAgentHandle existing))
            {
                NavigationManager.UnregisterAgent(existing);
                _agentByChess.Remove(chess);
            }

            float maxSpeed = chess.BaseData == null ? 0f : chess.BaseData.MoveSpeed;
            NavigationAgentHandle handle = NavigationManager.RegisterAgent(
                chess.RuntimeData.CurrentPosition,
                new NavigationAgentConfig(
                    ChessMovementController.ChessRadius,
                    maxSpeed,
                    2f,
                    0.5f,
                    10,
                    0.5f));
            if (handle.IsValid)
            {
                _agentByChess[chess] = handle;
                BattleManager.RegisterChess(campId, chessId, chess, handle);
            }

            return handle;
        }

        /// <summary>在主线程注销棋子的导航代理并排队移出战斗阵营。</summary>
        public void UnregisterChess(ChessController chess)
        {
            EnsureMainThread();
            if (chess != null && _agentByChess.TryGetValue(chess, out NavigationAgentHandle handle))
            {
                NavigationManager?.UnregisterAgent(handle);
                _agentByChess.Remove(chess);
            }

            BattleManager?.UnregisterChess(chess);
        }

        /// <summary>消费 BattleManager 发布的完整导航输入批次。</summary>
        private void DrainNavigationInputs()
        {
            while (BattleManager.TryDequeueNavigationInput(out NavigationInputSnapshot input))
            {
                if (!_inputBatches.TryGetValue(input.StepId, out List<NavigationInputSnapshot> batch))
                {
                    batch = new List<NavigationInputSnapshot>();
                    _inputBatches.Add(input.StepId, batch);
                }

                if (input.Agent.IsValid)
                {
                    batch.Add(input);
                }

                if (!input.IsStepComplete)
                {
                    continue;
                }

                IReadOnlyList<NavigationResultSnapshot> results =
                    NavigationManager.Tick(input.StepId, batch, BattleManager.FixedStep);
                BattleManager.SubmitNavigationResults(results);
                _inputBatches.Remove(input.StepId);
            }
        }

        /// <summary>在主线程输出战斗线程暂存的日志。</summary>
        private static void FlushThreadLogs()
        {
            while (ThreadLogQueue.TryDequeue(out ThreadLogRecord record))
            {
                switch (record.Level)
                {
                    case ThreadLogLevel.Warning:
                        Debug.LogWarning(record.Message);
                        break;
                    case ThreadLogLevel.Error:
                        Debug.LogError(record.Message);
                        break;
                    default:
                        Debug.Log(record.Message);
                        break;
                }
            }
        }

        /// <summary>按顺序停止战斗线程并释放主线程导航资源。</summary>
        private void OnDestroy()
        {
            if (_destroying)
            {
                return;
            }

            _destroying = true;
            if (BattleManager != null)
            {
                BattleManager.Stop(TimeSpan.FromSeconds(2));
                BattleManager.Dispose();
            }

            NavigationManager?.Dispose();
            _agentByChess.Clear();
            if (_instance == this)
            {
                _instance = null;
                _knownMainThreadId = 0;
            }
        }

        /// <summary>确认当前调用来自 GameManager 初始化时记录的主线程。</summary>
        private void EnsureMainThread()
        {
            if (_mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                throw new InvalidOperationException("GameManager 只能在 Unity 主线程调用。");
            }
        }
    }
}
