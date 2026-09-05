using UnityEngine;

namespace Chess
{
    /// <summary>
    /// 棋子的运行时可变战斗数据。
    /// </summary>
    public class ChessRuntimeData
    {
        /// <summary>棋子当前生命值。</summary>
        public int Hp;

        /// <summary>当前攻击力乘区。</summary>
        public float CurrentAtkMultipler;

        /// <summary>当前攻击力固定增量。</summary>
        public int CurrentAtkIncrement;

        /// <summary>当前射程乘区。</summary>
        public float CurrentRangeMultipler;

        /// <summary>当前射程固定增量。</summary>
        public float CurrentRangeIncrement;

        /// <summary>棋子当前棋盘 XZ 坐标。</summary>
        public Vector2 CurrentPosition;

        /// <summary>
        /// 根据基础攻击力计算当前最终攻击力。
        /// </summary>
        public int GetCurrentAtk(int baseAtk)
        {
            return (int)(baseAtk * CurrentAtkMultipler + CurrentAtkIncrement);
        }

        /// <summary>
        /// 根据基础射程计算当前最终射程，结果保留浮点精度。
        /// </summary>
        public float GetCurrentRange(float baseRange)
        {
            return baseRange * CurrentRangeMultipler + CurrentRangeIncrement;
        }
    }
}
