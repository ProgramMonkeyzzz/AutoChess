using BehaviorTree;

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
        /// <param name="fsm">该状态所属的状态机。</param>
        /// <param name="enterBehaviorTree">进入状态时执行一次的行为树运行器。</param>
        /// <param name="stayBehaviorTree">停留在状态期间逐帧推进的行为树运行器。</param>
        /// <param name="exitBehaviorTree">退出状态时执行一次的行为树运行器。</param>
        public MovementState(
            FSM fsm,
            BehaviorTreeRunner enterBehaviorTree = null,
            BehaviorTreeRunner stayBehaviorTree = null,
            BehaviorTreeRunner exitBehaviorTree = null)
            : base(fsm, enterBehaviorTree, stayBehaviorTree, exitBehaviorTree)
        {
        }

        /// <summary>
        /// 离开移动状态时停止并清理当前移动命令。
        /// </summary>
        protected override void OnStateExitCore()
        {
            _fsm.ChessController.MovementController?.Stop();
        }
    }
}
