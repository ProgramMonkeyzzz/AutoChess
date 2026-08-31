using BehaviorTree;

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
        /// <param name="fsm">该状态所属的状态机。</param>
        /// <param name="enterBehaviorTree">进入状态时执行一次的行为树运行器。</param>
        /// <param name="stayBehaviorTree">停留在状态期间逐帧推进的行为树运行器。</param>
        /// <param name="exitBehaviorTree">退出状态时执行一次的行为树运行器。</param>
        public DeadState(
            FSM fsm,
            BehaviorTreeRunner enterBehaviorTree = null,
            BehaviorTreeRunner stayBehaviorTree = null,
            BehaviorTreeRunner exitBehaviorTree = null)
            : base(fsm, enterBehaviorTree, stayBehaviorTree, exitBehaviorTree)
        {
        }

        /// <summary>
        /// 进入死亡状态时停止并清理当前移动命令。
        /// </summary>
        protected override void OnStateEnterCore()
        {
            _fsm.ChessController.MovementController?.Stop();
        }
    }
}
