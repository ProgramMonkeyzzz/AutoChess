using BehaviorTree;
using Chess;
using UnityEngine;

namespace Chess.Behavior
{
    /// <summary>
    /// 棋子行为树 Action/Conditional 共享的 Owner、索敌目标和射程查询。
    /// </summary>
    internal static class ChessBehaviorQueries
    {
        /// <summary>
        /// 从行为树上下文读取棋子 Owner；缺失时返回 false。
        /// </summary>
        public static bool TryGetOwner(BehaviorTreeContext context, out ChessController owner)
        {
            owner = context == null ? null : context.Owner as ChessController;
            return owner != null;
        }

        /// <summary>
        /// 读取当前索敌目标；Owner 或索敌目标缺失时返回 false。
        /// </summary>
        public static bool TryGetSelectedTarget(
            BehaviorTreeContext context,
            out ChessController owner,
            out ChessController target)
        {
            target = null;
            if (!TryGetOwner(context, out owner) || owner.TargetSelector == null)
            {
                return false;
            }

            target = owner.TargetSelector.SelectedChessController;
            return target != null;
        }

        /// <summary>
        /// 判断指定棋子是否已经死亡；缺失数据视为死亡。
        /// </summary>
        public static bool IsDead(ChessController chess)
        {
            return chess == null || !chess.IsAlive;
        }

        /// <summary>
        /// 读取棋子当前运行时射程；配置或运行时数据缺失时返回 false。
        /// </summary>
        public static bool TryGetCurrentRange(ChessController chess, out float range)
        {
            range = 0f;
            if (chess == null || chess.BaseData == null || chess.RuntimeData == null)
            {
                return false;
            }

            range = chess.RuntimeData.GetCurrentRange(chess.BaseData.Range);
            return true;
        }

        /// <summary>
        /// 判断 from 到 to 的棋盘距离是否不超过 rangeOwner 的当前射程。
        /// </summary>
        public static bool IsWithinCurrentRange(
            ChessController from,
            ChessController to,
            ChessController rangeOwner)
        {
            if (from == null
                || to == null
                || from.RuntimeData == null
                || to.RuntimeData == null
                || !TryGetCurrentRange(rangeOwner, out float range))
            {
                return false;
            }

            return Vector2.Distance(
                       from.RuntimeData.CurrentPosition,
                       to.RuntimeData.CurrentPosition) <= range;
        }
    }
}
