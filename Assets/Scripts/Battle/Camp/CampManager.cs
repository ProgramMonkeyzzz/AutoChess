using System;
using System.Collections.Generic;
using Chess;

namespace Camp
{
    /// <summary>
    /// 管理全部阵营，并由 BattleManager 工作线程独占推进。
    /// </summary>
    public class CampManager
    {
        private readonly Dictionary<int, CampGroup> _campDict = new Dictionary<int, CampGroup>();

        /// <summary>获取或创建指定阵营。</summary>
        public CampGroup GetOrCreateCamp(int campId)
        {
            if (!_campDict.TryGetValue(campId, out CampGroup group))
            {
                group = new CampGroup(campId, this);
                _campDict.Add(campId, group);
            }

            return group;
        }

        /// <summary>将棋子注册到指定阵营和配置 ID 的分组。</summary>
        public void RegisterChess(int campId, int chessId, ChessController chess)
        {
            if (chess == null)
            {
                throw new ArgumentNullException(nameof(chess));
            }

            GetOrCreateCamp(campId).RegisterChess(chessId, chess);
        }

        /// <summary>从所有阵营中注销指定棋子。</summary>
        public bool UnregisterChess(ChessController chess)
        {
            if (chess == null)
            {
                return false;
            }

            foreach (CampGroup group in _campDict.Values)
            {
                if (group.UnregisterChess(chess))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>按稳定阵营顺序推进全部棋子战斗逻辑。</summary>
        public void Tick(float fixedStep)
        {
            List<CampGroup> groups = new List<CampGroup>(_campDict.Values);
            groups.Sort((left, right) => left.CampId.CompareTo(right.CampId));
            foreach (CampGroup group in groups)
            {
                group.Tick(fixedStep);
            }
        }

        /// <summary>获取指定阵营的所有敌方棋子，并按稳定代理 ID 排序。</summary>
        public IEnumerable<ChessController> GetEnemyChesses(int ownerCamp)
        {
            List<ChessController> result = new List<ChessController>();
            foreach (KeyValuePair<int, CampGroup> pair in _campDict)
            {
                if (pair.Key != ownerCamp)
                {
                    result.AddRange(pair.Value.GetAllChesses());
                }
            }

            result.Sort(CompareChess);
            return result;
        }

        /// <summary>获取全部阵营棋子，并按稳定导航代理 ID 排序。</summary>
        public IEnumerable<ChessController> GetAllChesses()
        {
            List<ChessController> result = new List<ChessController>();
            foreach (CampGroup group in _campDict.Values)
            {
                result.AddRange(group.GetAllChesses());
            }

            result.Sort(CompareChess);
            return result;
        }

        /// <summary>按导航代理 ID 对棋子进行稳定排序。</summary>
        private static int CompareChess(ChessController left, ChessController right)
        {
            int leftId = left?.MovementController?.Agent.AgentId ?? int.MaxValue;
            int rightId = right?.MovementController?.Agent.AgentId ?? int.MaxValue;
            int compare = leftId.CompareTo(rightId);
            return compare != 0 ? compare : (left?.ChessId ?? int.MaxValue).CompareTo(right?.ChessId ?? int.MaxValue);
        }
    }
}
