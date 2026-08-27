using UnityEngine;

namespace AutoChess
{
    public class ChessBoard
    {
        public int Width = 100;
        public int Height = 100;

        /// <summary>
        /// 判断棋盘 XZ 平面上的坐标是否有效。
        /// </summary>
        public bool ContainsPosition(Vector2 position)
        {
            // 棋盘原点固定为世界坐标 (0, 0)，宽高表示可用范围上限。
            return !float.IsNaN(position.x) && !float.IsInfinity(position.x)
                   && !float.IsNaN(position.y) && !float.IsInfinity(position.y)
                   && position.x >= 0f && position.x <= Width
                   && position.y >= 0f && position.y <= Height;
        }
    }
}
