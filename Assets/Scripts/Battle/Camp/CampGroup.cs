using System.Collections.Generic;
using Chess;

namespace Camp
{
    public class CampGroup
    {
        private int _campId;

        /// <summary>
        /// key是chessId
        /// </summary>
        private Dictionary<int,HashSet<ChessController>> _chessDict;

        private CampManager _campManager;

        public CampGroup(int campId)
        {
            _campId = campId;
            _chessDict = new Dictionary<int, HashSet<ChessController>>();
        }

        public int CampId => _campId;

        public CampManager CampManager => _campManager;

        /// <summary>
        /// 获取该阵营所有棋子
        /// </summary>
        public IEnumerable<ChessController> GetAllChesses()
        {
            foreach (var chessSet in _chessDict.Values)
            {
                foreach (var chess in chessSet)
                {
                    yield return chess;
                }
            }
        }
    }
}
