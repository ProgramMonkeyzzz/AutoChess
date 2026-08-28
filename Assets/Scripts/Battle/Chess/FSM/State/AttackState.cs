namespace Chess.FSM.State
{
    /// <summary>
    /// 棋子的攻击状态，进入时停止移动，退出时取消当前攻击前摇。
    /// </summary>
    public class AttackState : StateBase
    {
        /// <summary>
        /// 创建绑定到指定状态机的攻击状态。
        /// </summary>
        public AttackState(FSM fsm) : base(fsm)
        {
        }

        /// <summary>
        /// 进入攻击状态时停止并清理当前移动命令。
        /// </summary>
        public override void OnStateEnter()
        {
            _fsm.ChessController.MovementController?.Stop();
        }

        /// <summary>
        /// 离开攻击状态时取消前摇并保留已有攻击冷却。
        /// </summary>
        public override void OnStateExit()
        {
            _fsm.ChessController.AttackController?.StopAttack();
        }
    }
}
