using BehaviorTree;

namespace Chess.FSM.State
{
    /// <summary>
    /// 棋子的攻击状态，进入时停止全部移动，退出时取消当前攻击前摇。
    /// </summary>
    public class AttackState : StateBase
    {
        /// <summary>
        /// 创建绑定到指定状态机的攻击状态。
        /// </summary>
        /// <param name="fsm">该状态所属的状态机。</param>
        /// <param name="enterBehaviorTree">进入状态时执行一次的行为树运行器。</param>
        /// <param name="stayBehaviorTree">停留在状态期间逐帧推进的行为树运行器。</param>
        /// <param name="exitBehaviorTree">退出状态时执行一次的行为树运行器。</param>
        public AttackState(
            FSM fsm,
            BehaviorTreeRunner enterBehaviorTree = null,
            BehaviorTreeRunner stayBehaviorTree = null,
            BehaviorTreeRunner exitBehaviorTree = null)
            : base(fsm, enterBehaviorTree, stayBehaviorTree, exitBehaviorTree)
        {
        }

        /// <summary>
        /// 进入攻击状态时停止并清理当前移动命令，前摇期间保持站桩。
        /// </summary>
        protected override void OnStateEnterCore()
        {
            _fsm.ChessController.MovementController?.Stop();
        }

        /// <summary>
        /// 离开攻击状态时取消前摇并保留已有攻击冷却。
        /// </summary>
        protected override void OnStateExitCore()
        {
            _fsm.ChessController.AttackController?.StopAttack();
        }
    }
}
