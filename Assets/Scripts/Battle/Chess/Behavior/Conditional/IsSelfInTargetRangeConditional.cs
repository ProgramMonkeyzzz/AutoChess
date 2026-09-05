using BehaviorTree;
using Chess;

namespace Chess.Behavior
{
    /// <summary>
    /// 判断自己是否位于当前索敌目标的运行时射程内。
    /// </summary>
    public sealed class IsSelfInTargetRangeConditional : ConditionalNode
    {
        /// <summary>
        /// 存在索敌目标且棋盘距离不超过目标当前射程时成立；无目标或数据缺失时失败。
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

            return ChessBehaviorQueries.IsWithinCurrentRange(owner, target, target);
        }
    }
}
