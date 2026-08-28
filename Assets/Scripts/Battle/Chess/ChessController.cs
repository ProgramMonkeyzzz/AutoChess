using AutoChess;
using Camp;
using Chess.FSM;
using Chess.FSM.State;
using Config;
using Pathfinding;
using UnityEngine;

namespace Chess
{
    public class ChessController
    {
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
        private int _chessId;
        private int _camp;
        private ChessBaseData _chessBaseData;
        private ChessRuntimeData _chessRuntimeData;
        private CampGroup _campGroup;
        private FSM.FSM _fsm;
        private ChessMovementController _movementController;
        /// <summary>该棋子的伤害接收器。</summary>
        private ChessDamageReceiver _chessDamageReceiver;
        /// <summary>从移动状态到死亡状态的单向转换。</summary>
        private MovementToDeadTransition _movementToDeadTransition;
        /// <summary>从移动状态到攻击状态的转换。</summary>
        private MovementToAttackTransition _movementToAttackTransition;
        /// <summary>从攻击状态到移动状态的转换。</summary>
        private AttackToMovementTransition _attackToMovementTransition;
        /// <summary>从攻击状态到死亡状态的转换。</summary>
        private AttackToDeadTransition _attackToDeadTransition;
        /// <summary>该棋子的攻击控制器。</summary>
        private AttackController _attackController;

        /// <summary>
        /// 创建棋子控制器，并准备运行时可变数据。
        /// </summary>
        public ChessController() : this(null)
        {
        }

        /// <summary>
        /// 创建绑定静态配置的棋子控制器，并初始化运行时战斗数据。
        /// </summary>
        public ChessController(ChessBaseData baseData)
        {
            _chessBaseData = baseData;
            // 移动控制器需要持续写入运行时位置，因此默认保证数据对象存在。
            _chessRuntimeData = new ChessRuntimeData();
            if (_chessBaseData != null)
            {
                _chessRuntimeData.Hp = _chessBaseData.MaxHp;
                _chessRuntimeData.CurrentAtkMultipler = 1;
                _chessRuntimeData.CurrentAtkIncrement = 0;
                _chessRuntimeData.CurrentRangeMultipler = 1;
                _chessRuntimeData.CurrentRangeIncrement = 0;
            }

            _targetSelector = new ChessTargetSelector(this);
            _chessDamageReceiver = new ChessDamageReceiver(this);
            _attackController = new AttackController(this);

            // 先注册状态和转换，再启动默认的 MovementState。
            _fsm = new FSM.FSM(this);
            _fsm.AddState(new MovementState(_fsm));
            _fsm.AddState(new AttackState(_fsm));
            _fsm.AddState(new DeadState(_fsm));
            _movementToAttackTransition = new MovementToAttackTransition(_fsm);
            _movementToDeadTransition = new MovementToDeadTransition(_fsm);
            _attackToMovementTransition = new AttackToMovementTransition(_fsm);
            _attackToDeadTransition = new AttackToDeadTransition(_fsm);
            _fsm.AddTransition(_movementToAttackTransition);
            _fsm.AddTransition(_movementToDeadTransition);
            _fsm.AddTransition(_attackToMovementTransition);
            _fsm.AddTransition(_attackToDeadTransition);
            _fsm.Start<MovementState>();
        }

        /// <summary>
        /// 绑定该棋子的 A* 移动组件。
        /// </summary>
        public ChessMovementController InitializeMovement(ChessBoard chessBoard, IAstarAI astarAI)
        {
            _movementController = new ChessMovementController(this, chessBoard, astarAI);
            return _movementController;
        }

        /// <summary>
        /// 每帧按 FSM、移动、攻击顺序驱动棋子战斗逻辑。
        /// </summary>
        public void Tick()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// 使用指定时间步长驱动棋子状态、移动和攻击逻辑。
        /// </summary>
        public void Tick(float deltaTime)
        {
            _fsm.Tick();
            _movementController?.Tick();
            _attackController?.Tick(deltaTime);
        }

        /// <summary>
        /// 在移动命令成功建立后切回默认移动状态。
        /// </summary>
        internal void EnterMovementState()
        {
            if (_fsm.CurrentState is DeadState)
            {
                return;
            }

            _fsm.ChangeState<MovementState>();
        }

        /// <summary>
        /// 在攻击流程开始后从移动状态切换到攻击状态。
        /// </summary>
        internal void EnterAttackState()
        {
            if (_fsm.CurrentState is MovementState)
            {
                _fsm.ChangeState<AttackState>();
            }
        }

        /// <summary>
        /// 在受到伤害后检查并立即执行 MovementState 到 DeadState 的转换。
        /// </summary>
        internal void CheckDeathStateTransition()
        {
            StateTransition deathTransition;
            if (_fsm.CurrentState is MovementState)
            {
                deathTransition = _movementToDeadTransition;
            }
            else if (_fsm.CurrentState is AttackState)
            {
                deathTransition = _attackToDeadTransition;
            }
            else
            {
                return;
            }

            if (!deathTransition.CanTransition())
            {
                return;
            }

            _fsm.ChangeState<DeadState>();
        }
    }
}
