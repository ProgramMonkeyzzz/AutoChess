using BehaviorTree;
using Chess;

namespace Chess.Behavior
{
    /// <summary>
    /// 向当前索敌目标下达一次贴身追踪移动命令，发令成功后立即成功。
    /// </summary>
    public sealed class MoveToTargetAction : ActionNode
    {
        /// <summary>
        /// 尝试向当前索敌目标移动；无目标、无移动控制器、攻击前摇中或命令被拒绝时失败。
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

            // 攻击状态/前摇中不得发令，以免取消前摇或一边移动一边出手。
            if (ChessBehaviorQueries.IsInAttackOrWindUp(owner))
            {
                return ActionResult.Failure;
            }

            return owner.MovementController.MoveToTarget(target)
                ? ActionResult.Success
                : ActionResult.Failure;
        }
    }
}
