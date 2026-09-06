using BehaviorTree;
using Chess;

namespace Chess.Behavior
{
    /// <summary>
    /// 停止并清除当前移动命令，不要求存在索敌目标。
    /// </summary>
    public sealed class StopMovementAction : ActionNode
    {
        /// <summary>
        /// 尝试停止移动；无 Owner 或无移动控制器时失败，已停止时仍成功。
        /// </summary>
        protected override ActionResult OnUpdate(BehaviorTreeContext context)
        {
            if (!ChessBehaviorQueries.TryGetOwner(context, out ChessController owner)
                || owner.MovementController == null)
            {
                return ActionResult.Failure;
            }

            owner.MovementController.Stop();
            return ActionResult.Success;
        }
    }
}
