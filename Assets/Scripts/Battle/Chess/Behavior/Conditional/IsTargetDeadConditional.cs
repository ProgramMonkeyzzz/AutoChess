using BehaviorTree;
using Chess;

namespace Chess.Behavior
{
    /// <summary>
    /// 判断当前索敌目标是否已经死亡。
    /// </summary>
    public sealed class IsTargetDeadConditional : ConditionalNode
    {
        /// <summary>
        /// 存在索敌目标且目标已死亡时成立；无目标时失败。
        /// </summary>
        protected override bool Evaluate(BehaviorTreeContext context)
        {
            if (!ChessBehaviorQueries.TryGetSelectedTarget(context, out _, out ChessController target))
            {
                return false;
            }

            return ChessBehaviorQueries.IsDead(target);
        }
    }
}
