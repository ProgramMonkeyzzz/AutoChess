using System;
using System.Collections.Generic;
using System.Threading;
using AutoChess.Navigation;
using AutoChess.Render;
using AutoChess.Utils;
using Chess;
using Config;
using UnityEngine;

namespace AutoChess
{
    /// <summary>
    /// 全局 Unity 主线程管理器，负责 RVO Job 调度、战斗线程时钟、渲染快照消费和日志输出。
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        private const float DefaultFixedStep = 1f / 60f;
        private const int MaxCatchUpSteps = 4;

        private static GameManager _instance;
        private static int _knownMainThreadId;
        private readonly Dictionary<long, List<NavigationInputSnapshot>> _inputBatches =
            new Dictionary<long, List<NavigationInputSnapshot>>();
        /// <summary>主线程按 UniqueId 记录的导航代理句柄。</summary>
        private readonly Dictionary<int, NavigationAgentHandle> _agentByUniqueId =
            new Dictionary<int, NavigationAgentHandle>();
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
        
        /// <summary>获取渲染管理器。</summary>
        public RenderManager RenderManager { get; private set; }

        /// <summary>Unity 生命周期加载棋子配置并初始化战斗、导航和固定步时钟。</summary>
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
            BattleManager.Configs.LoadFromResources();
            NavigationManager = new NavigationManager(BattleManager.ChessBoard);
            RenderManager = new RenderManager(transform);
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
            ApplyRenderFrame();

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

        /// <summary>在主线程按配置创建棋子导航代理、排队加入战斗并立刻创建视图。</summary>
        public int RegisterChess(int campId, int configId, Vector2 position)
        {
            EnsureMainThread();
            if (NavigationManager == null || BattleManager == null)
            {
                return 0;
            }

            if (!BattleManager.Configs.TryGet(configId, out ChessBaseData config) || config == null)
            {
                Debug.LogError("GameManager 找不到 ConfigId=" + configId + " 的棋子配置。");
                return 0;
            }

            NavigationAgentHandle handle = NavigationManager.RegisterAgent(
                position,
                new NavigationAgentConfig(
                    ChessMovementController.ChessRadius,
                    config.MoveSpeed,
                    2f,
                    0.5f,
                    10,
                    0.5f));
            if (!handle.IsValid)
            {
                return 0;
            }

            int uniqueId = ChessController.AllocateUniqueId();
            _agentByUniqueId[uniqueId] = handle;
            BattleManager.RegisterChess(campId, configId, uniqueId, position, handle);
            CreateChessView(campId, uniqueId, position);
            return uniqueId;
        }

        /// <summary>在主线程注销棋子的导航代理与视图，并排队移出战斗阵营。</summary>
        public void UnregisterChess(int uniqueId)
        {
            EnsureMainThread();
            if (uniqueId > 0 && _agentByUniqueId.TryGetValue(uniqueId, out NavigationAgentHandle handle))
            {
                NavigationManager?.UnregisterAgent(handle);
                _agentByUniqueId.Remove(uniqueId);
            }

            if (uniqueId > 0)
            {
                RenderManager?.RemoveChessView(uniqueId);
            }

            BattleManager?.UnregisterChess(uniqueId);
        }

        /// <summary>在主线程立刻创建棋子视图，使用注册时传入的出生点。</summary>
        private void CreateChessView(int campId, int uniqueId, Vector2 position)
        {
            if (RenderManager == null)
            {
                return;
            }

            ChessView view = RenderManager.CreateChessView(uniqueId, campId);
            if (view == null)
            {
                return;
            }

            view.SetPosition(position);
            view.SetForward(Vector2.up);
        }

        /// <summary>消费战斗线程发布的最新渲染帧并同步视图。</summary>
        private void ApplyRenderFrame()
        {
            if (BattleManager == null || RenderManager == null)
            {
                return;
            }

            if (BattleManager.TryConsumeRenderFrame(out BattleRenderFrame frame))
            {
                RenderManager.ApplyFrame(frame);
            }
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

        /// <summary>按顺序停止战斗线程并释放主线程导航与渲染资源。</summary>
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

            RenderManager?.Dispose();
            RenderManager = null;
            NavigationManager?.Dispose();
            _agentByUniqueId.Clear();
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
