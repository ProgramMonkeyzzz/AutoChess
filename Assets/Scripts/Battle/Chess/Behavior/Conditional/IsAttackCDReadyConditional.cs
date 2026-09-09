using BehaviorTree;
using Chess;

namespace Chess.Behavior
{
    /// <summary>
    /// 判断当前棋子的攻击冷却是否已经结束且不在前摇。
    /// </summary>
    public sealed class IsAttackCDReadyConditional : ConditionalNode
    {
        /// <summary>
        /// Owner 与攻击控制器存在、剩余冷却小于等于零且当前不在前摇时成立。
        /// </summary>
        protected override bool Evaluate(BehaviorTreeContext context)
        {
            if (!ChessBehaviorQueries.TryGetOwner(context, out ChessController owner)
                || owner.AttackController == null)
            {
                return false;
            }

            return owner.AttackController.CooldownRemaining <= 0f
                   && !owner.AttackController.IsWindingUp;
        }
    }
}
