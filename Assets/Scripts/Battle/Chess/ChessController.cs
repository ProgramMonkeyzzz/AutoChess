using AutoChess;
using Camp;
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
        }

        /// <summary>
        /// 绑定该棋子的 A* 移动组件。
        /// </summary>
        public ChessMovementController InitializeMovement(ChessBoard chessBoard, IAstarAI astarAI)
        {
            _movementController = new ChessMovementController(this, chessBoard, astarAI);
            return _movementController;
        }
    }
}
