using BehaviorTree;

namespace Chess.Behavior
{
    /// <summary>
    /// 判断当前棋子是否存在索敌目标。
    /// </summary>
    public sealed class HasTargetConditional : ConditionalNode
    {
        /// <summary>
        /// 当 Owner 存在且索敌目标非空时成立，不要求目标存活。
        /// </summary>
        protected override bool Evaluate(BehaviorTreeContext context)
        {
            return ChessBehaviorQueries.TryGetSelectedTarget(context, out _, out _);
        }
    }
}
