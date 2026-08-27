using System.Collections.Generic;
using Chess;

namespace Camp
{
    public class CampManager
    {
        private Dictionary<int,CampGroup> _campDict;

        public CampManager()
        {
            _campDict = new Dictionary<int, CampGroup>();
        }

        /// <summary>
        /// 获取指定阵营的所有敌方棋子
        /// </summary>
        public IEnumerable<ChessController> GetEnemyChesses(int ownerCamp)
        {
            foreach (var kvp in _campDict)
            {
                if (kvp.Key != ownerCamp)
                {
                    foreach (var chess in kvp.Value.GetAllChesses())
                    {
                        yield return chess;
                    }
                }
            }
        }
    }
}