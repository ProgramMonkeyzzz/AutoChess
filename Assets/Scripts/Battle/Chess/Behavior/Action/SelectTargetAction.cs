using BehaviorTree;
using Chess;

namespace Chess.Behavior
{
    /// <summary>
    /// 按指定索敌方式选择当前目标，选中敌人后立即成功。
    /// </summary>
    public sealed class SelectTargetAction : ActionNode
    {
        /// <summary>本次索敌使用的方式。</summary>
        public ChessTargetSelector.TargetSelectMode Mode { get; }

        /// <summary>
        /// 创建指定索敌方式的选择目标 Action。
        /// </summary>
        public SelectTargetAction(ChessTargetSelector.TargetSelectMode mode)
        {
            Mode = mode;
        }

        /// <summary>
        /// 尝试按当前方式索敌；无 Owner、无法分发或未选中敌人时失败。
        /// </summary>
        protected override ActionResult OnUpdate(BehaviorTreeContext context)
        {
            if (!ChessBehaviorQueries.TryGetOwner(context, out ChessController owner)
                || owner.TargetSelector == null
                || !owner.TargetSelector.Select(Mode)
                || owner.TargetSelector.SelectedChessController == null)
            {
                return ActionResult.Failure;
            }

            return ActionResult.Success;
        }
    }
}
