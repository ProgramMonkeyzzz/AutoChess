namespace Chess.FSM.State
{
    /// <summary>
    /// 棋子的死亡状态，不提供返回移动状态的转换。
    /// </summary>
    public class DeadState : StateBase
    {
        /// <summary>
        /// 创建绑定到指定状态机的死亡状态。
        /// </summary>
        public DeadState(FSM fsm) : base(fsm)
        {
        }

        /// <summary>
        /// 进入死亡状态时停止并清理当前移动命令。
        /// </summary>
        public override void OnStateEnter()
        {
            _fsm.ChessController.MovementController?.Stop();
        }
    }
}
