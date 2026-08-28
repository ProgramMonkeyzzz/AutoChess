namespace Chess.FSM.State
{
    /// <summary>
    /// 棋子的默认状态，仅作为 FSM 的初始状态，不直接驱动移动控制器。
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
        /// 离开移动状态时停止并清理当前移动命令。
        /// </summary>
        public override void OnStateExit()
        {
            _fsm.ChessController.MovementController?.Stop();
        }
    }
}
