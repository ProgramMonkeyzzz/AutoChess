using AutoChess;
using Camp;
using Chess.FSM.State;
using Config;
using Pathfinding;

namespace Chess
{
    public class ChessController
    {
        public int Camp => _camp;
        public ChessRuntimeData RuntimeData => _chessRuntimeData;
        public ChessBaseData BaseData => _chessBaseData;
        public CampGroup CampGroup => _campGroup;
        public ChessMovementController MovementController => _movementController;

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

        /// <summary>
        /// 创建棋子控制器，并准备运行时可变数据。
        /// </summary>
        public ChessController()
        {
            // 移动控制器需要持续写入运行时位置，因此默认保证数据对象存在。
            _chessRuntimeData = new ChessRuntimeData();
            _targetSelector = new ChessTargetSelector(this);

            // 所有棋子创建后都从兼容静止与移动的 MovementState 开始运行。
            _fsm = new FSM.FSM(this);
            _fsm.AddState(new MovementState(_fsm));
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
        /// 每帧驱动棋子状态机，由当前状态决定需要更新的子系统。
        /// </summary>
        public void Tick()
        {
            _fsm.Tick();
        }

        /// <summary>
        /// 在移动命令成功建立后切回默认移动状态。
        /// </summary>
        internal void EnterMovementState()
        {
            _fsm.ChangeState<MovementState>();
        }
    }
}
