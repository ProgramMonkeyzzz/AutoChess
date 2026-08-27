namespace Chess.FSM.State
{
    /// <summary>
    /// 棋子的默认状态，统一承载静止待机和移动控制。
    /// </summary>
    public class MovementState : StateBase
    {
        /// <summary>
        /// 创建绑定到指定状态机的移动状态。
        /// </summary>
        public MovementState(FSM fsm) : base(fsm)
        {
        }

        /// <summary>
        /// 在移动状态内逐帧刷新追踪目标和运行时位置。
        /// </summary>
        public override void OnStateStay()
        {
            // 棋子可先创建状态机、再绑定场景中的 A* 移动组件。
            _fsm.ChessController.MovementController?.Tick();
        }

        /// <summary>
        /// 离开移动状态时停止 A*，避免攻击等后续状态仍受旧路径驱动。
        /// </summary>
        public override void OnStateExit()
        {
            _fsm.ChessController.MovementController?.Stop();
        }
    }
}
