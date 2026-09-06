using System;
using System.Threading;
using AutoChess;
using AutoChess.Navigation;
using AutoChess.Render;
using AutoChess.Utils;
using BehaviorTree;
using Camp;
using Chess.Behavior;
using Chess.FSM;
using Chess.FSM.State;
using Config;
using UnityEngine;

namespace Chess
{
    public class ChessController
    {
        /// <summary>进程内单调递增的棋子实例唯一 ID。</summary>
        public int UniqueId { get; }

        /// <summary>棋子静态配置 ID。</summary>
        public int ConfigId { get; private set; }

        public int Camp => _camp;
        public ChessRuntimeData RuntimeData => _chessRuntimeData;
        public ChessBaseData BaseData => _chessBaseData;
        public CampGroup CampGroup => _campGroup;
        /// <summary>获取该棋子的目标选择器。</summary>
        public ChessTargetSelector TargetSelector => _targetSelector;
        public ChessMovementController MovementController => _movementController;
        /// <summary>获取该棋子的攻击控制器。</summary>
        public AttackController AttackController => _attackController;

        /// <summary>获取该棋子的伤害接收器。</summary>
        public ChessDamageReceiver DamageReceiver => _chessDamageReceiver;

        /// <summary>获取该棋子的战斗状态机。</summary>
        public FSM.FSM Fsm => _fsm;

        private ChessTargetSelector _targetSelector;
        private int _camp;
        private ChessBaseData _chessBaseData;
        private ChessRuntimeData _chessRuntimeData;
        private CampGroup _campGroup;
        private FSM.FSM _fsm;
        private ChessMovementController _movementController;
        /// <summary>该棋子的伤害接收器。</summary>
        private ChessDamageReceiver _chessDamageReceiver;
        /// <summary>该棋子的攻击控制器。</summary>
        private AttackController _attackController;
        /// <summary>上一份渲染快照中的棋盘位置，用于计算位移朝向。</summary>
        private Vector2 _lastRenderPosition;
        /// <summary>是否已经生成过渲染快照。</summary>
        private bool _hasLastRenderPosition;
        /// <summary>棋子实例 ID 生成器。</summary>
        private static int _nextUniqueId;

        /// <summary>
        /// 创建棋子控制器，并准备运行时可变数据。
        /// </summary>
        public ChessController() : this((ChessBaseData)null)
        {
        }

        /// <summary>
        /// 创建绑定静态配置的棋子控制器，并初始化运行时战斗数据。
        /// </summary>
        public ChessController(ChessBaseData baseData)
            : this(baseData, null)
        {
        }

        /// <summary>
        /// 创建绑定静态配置的棋子控制器，并按仓库部署状态行为树。
        /// </summary>
        public ChessController(ChessBaseData baseData, BehaviorTreeRepository behaviorTrees)
            : this(AllocateUniqueId(), baseData, behaviorTrees)
        {
            _fsm.Start<MovementState>();
        }

        /// <summary>使用预先分配的 UniqueId 创建棋子，供阵营工厂使用。</summary>
        internal ChessController(int uniqueId, ChessBaseData baseData)
            : this(uniqueId, baseData, null)
        {
        }

        /// <summary>使用预先分配的 UniqueId 和可选行为树仓库创建棋子，不启动 FSM。</summary>
        internal ChessController(
            int uniqueId,
            ChessBaseData baseData,
            BehaviorTreeRepository behaviorTrees)
        {
            if (uniqueId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(uniqueId));
            }

            UniqueId = uniqueId;
            _chessBaseData = baseData;
            ConfigId = baseData == null ? 0 : baseData.ConfigId;
            // 移动控制器需要持续写入运行时位置，因此默认保证数据对象存在。
            _chessRuntimeData = new ChessRuntimeData();
            if (_chessBaseData != null)
            {
                _chessRuntimeData.Hp = _chessBaseData.MaxHp;
                _chessRuntimeData.CurrentAtkMultipler = 1;
                _chessRuntimeData.CurrentAtkIncrement = 0;
                _chessRuntimeData.CurrentRangeMultipler = 1;
                _chessRuntimeData.CurrentRangeIncrement = 0f;
            }

            _targetSelector = new ChessTargetSelector(this);
            _chessDamageReceiver = new ChessDamageReceiver(this);
            _attackController = new AttackController(this);

            // 先按配置部署状态行为树并注册转换；公开构造函数随后启动 MovementState。
            _fsm = new FSM.FSM(this);
            _fsm.AddState(new MovementState(
                _fsm,
                CreateStateBehaviorTree(behaviorTrees, _chessBaseData == null ? null : _chessBaseData.MovementEnterBehaviorTree),
                CreateStateBehaviorTree(behaviorTrees, _chessBaseData == null ? null : _chessBaseData.MovementStayBehaviorTree),
                CreateStateBehaviorTree(behaviorTrees, _chessBaseData == null ? null : _chessBaseData.MovementExitBehaviorTree)));
            _fsm.AddState(new AttackState(
                _fsm,
                CreateStateBehaviorTree(behaviorTrees, _chessBaseData == null ? null : _chessBaseData.AttackEnterBehaviorTree),
                CreateStateBehaviorTree(behaviorTrees, _chessBaseData == null ? null : _chessBaseData.AttackStayBehaviorTree),
                CreateStateBehaviorTree(behaviorTrees, _chessBaseData == null ? null : _chessBaseData.AttackExitBehaviorTree)));
            _fsm.AddState(new DeadState(
                _fsm,
                CreateStateBehaviorTree(behaviorTrees, _chessBaseData == null ? null : _chessBaseData.DeadEnterBehaviorTree),
                CreateStateBehaviorTree(behaviorTrees, _chessBaseData == null ? null : _chessBaseData.DeadStayBehaviorTree),
                CreateStateBehaviorTree(behaviorTrees, _chessBaseData == null ? null : _chessBaseData.DeadExitBehaviorTree)));
            _fsm.AddTransition(new MovementToAttackTransition(_fsm));
            _fsm.AddTransition(new MovementToDeadTransition(_fsm));
            _fsm.AddTransition(new AttackToMovementTransition(_fsm));
            _fsm.AddTransition(new AttackToDeadTransition(_fsm));
        }

        /// <summary>按配置名创建状态生命周期行为树；空白不挂树，缺树或构建失败时记录错误并跳过。</summary>
        private BehaviorTreeRunner CreateStateBehaviorTree(BehaviorTreeRepository behaviorTrees, string treeName)
        {
            if (string.IsNullOrWhiteSpace(treeName))
            {
                return null;
            }

            string name = treeName.Trim();
            if (behaviorTrees == null || !behaviorTrees.TryGet(name, out BehaviorTreeDefinition definition) || definition == null)
            {
                ThreadLogQueue.Enqueue(ThreadLogLevel.Error, "ChessController 找不到行为树 '" + name + "'。");
                return null;
            }

            try
            {
                return ChessBehaviorTreeLogic.CreateRunner(definition, this);
            }
            catch (Exception exception)
            {
                ThreadLogQueue.Enqueue(
                    ThreadLogLevel.Error,
                    "ChessController 构建行为树 '" + name + "' 失败: " + exception.Message);
                return null;
            }
        }

        /// <summary>分配下一个进程内唯一棋子 ID。</summary>
        internal static int AllocateUniqueId()
        {
            return Interlocked.Increment(ref _nextUniqueId);
        }

        /// <summary>
        /// 绑定该棋子的 RVO 导航代理。
        /// </summary>
        public ChessMovementController InitializeMovement(ChessBoard chessBoard, NavigationAgentHandle agent)
        {
            _movementController = new ChessMovementController(this, chessBoard, agent);
            return _movementController;
        }

        /// <summary>
        /// 设置棋子的阵营上下文，供 CampManager 在注册时调用。
        /// </summary>
        internal void SetCampContext(int camp, int configId, CampGroup campGroup)
        {
            _camp = camp;
            ConfigId = configId;
            _campGroup = campGroup;
        }

        /// <summary>
        /// 生成当前棋子的导航输入，供 BattleManager 发布给主线程。
        /// </summary>
        internal NavigationInputSnapshot BuildNavigationInput(long stepId)
        {
            return _movementController == null
                ? default
                : _movementController.BuildNavigationInput(stepId);
        }

        /// <summary>
        /// 应用主线程 RVO 计算结果，供 BattleManager 更新运行时位置。
        /// </summary>
        internal bool ApplyNavigationResult(NavigationResultSnapshot result)
        {
            return _movementController != null && _movementController.ApplyNavigationResult(result);
        }

        /// <summary>
        /// 每帧按 FSM、移动、攻击顺序驱动棋子战斗逻辑。
        /// </summary>
        [Obsolete("战斗逻辑必须由 BattleManager 传入固定时间步。")]
        public void Tick()
        {
            Tick(1f / 60f);
        }

        /// <summary>
        /// 使用指定时间步长驱动棋子状态、移动和攻击逻辑。
        /// </summary>
        public void Tick(float deltaTime)
        {
            _fsm.Tick(deltaTime);
            _movementController?.Tick();
            _attackController?.Tick(deltaTime);
        }

        /// <summary>
        /// 判断棋子是否仍应出现在渲染快照中。
        /// </summary>
        internal bool IsAlive
        {
            get
            {
                return _chessRuntimeData != null
                       && _chessRuntimeData.Hp > 0
                       && !(_fsm.CurrentState is DeadState);
            }
        }

        /// <summary>
        /// 生成当前棋子的渲染快照，并更新位移朝向所需的上一帧位置。
        /// </summary>
        public ChessRenderSnapshot CreateRenderSnapshot()
        {
            Vector2 position = _chessRuntimeData == null ? Vector2.zero : _chessRuntimeData.CurrentPosition;
            Vector2 forward = ResolveRenderForward(position);
            _lastRenderPosition = position;
            _hasLastRenderPosition = true;
            return new ChessRenderSnapshot(UniqueId, _camp, position, forward);
        }

        /// <summary>
        /// 按索敌目标、移动终点、本步位移的优先级计算 XZ 朝向。
        /// </summary>
        private Vector2 ResolveRenderForward(Vector2 position)
        {
            ChessController selected = _targetSelector == null ? null : _targetSelector.SelectedChessController;
            if (selected != null && !ReferenceEquals(selected, this) && selected.IsAlive
                && selected.RuntimeData != null)
            {
                Vector2 toTarget = selected.RuntimeData.CurrentPosition - position;
                if (toTarget.sqrMagnitude > 0.000001f)
                {
                    return toTarget;
                }
            }

            if (_movementController != null && _movementController.HasDestination)
            {
                Vector2 toDestination = _movementController.Destination - position;
                if (toDestination.sqrMagnitude > 0.000001f)
                {
                    return toDestination;
                }
            }

            if (_hasLastRenderPosition)
            {
                Vector2 displacement = position - _lastRenderPosition;
                if (displacement.sqrMagnitude > 0.000001f)
                {
                    return displacement;
                }
            }

            return Vector2.up;
        }
    }
}
