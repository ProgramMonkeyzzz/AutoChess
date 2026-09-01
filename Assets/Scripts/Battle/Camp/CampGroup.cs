using System;
using System.Collections.Generic;
using Chess;

namespace Camp
{
    /// <summary>
    /// 保存一个阵营的棋子，并由所属 CampManager 的工作线程独占访问。
    /// </summary>
    public class CampGroup
    {
        private readonly int _campId;
        private readonly Dictionary<int, HashSet<ChessController>> _chessDict =
            new Dictionary<int, HashSet<ChessController>>();
        private readonly CampManager _campManager;

        /// <summary>创建绑定阵营管理器的阵营分组。</summary>
        public CampGroup(int campId, CampManager campManager = null)
        {
            _campId = campId;
            _campManager = campManager;
        }

        /// <summary>获取阵营 ID。</summary>
        public int CampId => _campId;

        /// <summary>获取所属阵营管理器。</summary>
        public CampManager CampManager => _campManager;

        /// <summary>注册一个棋子实例到指定配置 ID 分组。</summary>
        public void RegisterChess(int chessId, ChessController chess)
        {
            if (chess == null)
            {
                throw new ArgumentNullException(nameof(chess));
            }

            if (!_chessDict.TryGetValue(chessId, out HashSet<ChessController> chessSet))
            {
                chessSet = new HashSet<ChessController>();
                _chessDict.Add(chessId, chessSet);
            }

            chessSet.Add(chess);
            chess.SetCampContext(_campId, chessId, this);
        }

        /// <summary>从当前阵营分组中注销棋子实例。</summary>
        public bool UnregisterChess(ChessController chess)
        {
            bool removed = false;
            foreach (HashSet<ChessController> chessSet in _chessDict.Values)
            {
                removed |= chessSet.Remove(chess);
            }

            return removed;
        }

        /// <summary>按导航代理 ID 稳定推进阵营内所有棋子。</summary>
        public void Tick(float fixedStep)
        {
            List<ChessController> chesses = new List<ChessController>(GetAllChesses());
            chesses.Sort(CompareChess);
            foreach (ChessController chess in chesses)
            {
                chess.Tick(fixedStep);
            }
        }

        /// <summary>获取该阵营所有棋子。</summary>
        public IEnumerable<ChessController> GetAllChesses()
        {
            List<ChessController> result = new List<ChessController>();
            foreach (HashSet<ChessController> chessSet in _chessDict.Values)
            {
                result.AddRange(chessSet);
            }

            return result;
        }

        /// <summary>按导航代理 ID 对阵营内棋子进行稳定排序。</summary>
        private static int CompareChess(ChessController left, ChessController right)
        {
            int leftId = left?.MovementController?.Agent.AgentId ?? int.MaxValue;
            int rightId = right?.MovementController?.Agent.AgentId ?? int.MaxValue;
            int compare = leftId.CompareTo(rightId);
            return compare != 0 ? compare : (left?.ChessId ?? int.MaxValue).CompareTo(right?.ChessId ?? int.MaxValue);
        }
    }
}
