using BehaviorTree;
using Chess;

namespace Chess.Behavior
{
    /// <summary>
    /// 向当前索敌目标的反方向下达一次持续远离命令，发令成功后立即成功。
    /// </summary>
    public sealed class MoveAwayFromTargetAction : ActionNode
    {
        /// <summary>
        /// 尝试远离当前索敌目标；无目标、无移动控制器或命令被拒绝时失败。
        /// </summary>
        protected override ActionResult OnUpdate(BehaviorTreeContext context)
        {
            if (!ChessBehaviorQueries.TryGetSelectedTarget(
                    context,
                    out ChessController owner,
                    out ChessController target)
                || owner.MovementController == null)
            {
                return ActionResult.Failure;
            }

            return owner.MovementController.MoveAwayFromTarget(target)
                ? ActionResult.Success
                : ActionResult.Failure;
        }
    }
}
