using BehaviorTree;
using Chess;

namespace Chess.Behavior
{
    /// <summary>
    /// 对当前索敌目标发起一次攻击命令，成功起手或已在攻击流程中时立即成功。
    /// </summary>
    public sealed class AttackTargetAction : ActionNode
    {
        /// <summary>
        /// 尝试攻击当前索敌目标；无目标、无法起手且当前不在攻击流程中时失败。
        /// </summary>
        protected override ActionResult OnUpdate(BehaviorTreeContext context)
        {
            if (!ChessBehaviorQueries.TryGetSelectedTarget(
                    context,
                    out ChessController owner,
                    out _)
                || owner.AttackController == null
                || ChessBehaviorQueries.IsDead(owner))
            {
                return ActionResult.Failure;
            }

            owner.AttackController.AttackTarget();
            return owner.AttackController.IsAttackInProgress
                ? ActionResult.Success
                : ActionResult.Failure;
        }
    }
}
