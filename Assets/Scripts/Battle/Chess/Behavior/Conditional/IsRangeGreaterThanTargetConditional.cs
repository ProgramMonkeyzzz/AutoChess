using BehaviorTree;
using Chess;

namespace Chess.Behavior
{
    /// <summary>
    /// 判断当前棋子的运行时射程是否严格大于索敌目标的运行时射程。
    /// </summary>
    public sealed class IsRangeGreaterThanTargetConditional : ConditionalNode
    {
        /// <summary>
        /// 存在索敌目标且自己的当前射程大于目标当前射程时成立；无目标或射程数据缺失时失败。
        /// </summary>
        protected override bool Evaluate(BehaviorTreeContext context)
        {
            if (!ChessBehaviorQueries.TryGetSelectedTarget(
                    context,
                    out ChessController owner,
                    out ChessController target))
            {
                return false;
            }

            return ChessBehaviorQueries.IsCurrentRangeGreaterThan(owner, target);
        }
    }
}
