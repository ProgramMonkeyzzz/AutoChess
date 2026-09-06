using BehaviorTree;
using Chess;

namespace Chess.Behavior
{
    /// <summary>
    /// 判断当前棋子的攻击冷却是否已经结束。
    /// </summary>
    public sealed class IsAttackCDReadyConditional : ConditionalNode
    {
        /// <summary>
        /// Owner 与攻击控制器存在且剩余冷却小于等于零时成立；前摇期间冷却为 0 也视为就绪。
        /// </summary>
        protected override bool Evaluate(BehaviorTreeContext context)
        {
            if (!ChessBehaviorQueries.TryGetOwner(context, out ChessController owner)
                || owner.AttackController == null)
            {
                return false;
            }

            return owner.AttackController.CooldownRemaining <= 0f;
        }
    }
}
