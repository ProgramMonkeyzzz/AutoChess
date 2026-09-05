using BehaviorTree;
using Chess;

namespace Chess.Behavior
{
    /// <summary>
    /// 判断当前索敌目标是否位于自己的运行时射程内。
    /// </summary>
    public sealed class IsTargetInRangeConditional : ConditionalNode
    {
        /// <summary>
        /// 存在索敌目标且棋盘距离不超过自己的当前射程时成立；无目标或数据缺失时失败。
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

            return ChessBehaviorQueries.IsWithinCurrentRange(owner, target, owner);
        }
    }
}
