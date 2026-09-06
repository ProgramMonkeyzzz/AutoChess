using System;
using System.Collections.Generic;
using BehaviorTree;
using Chess;
using Config;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// 管理全部阵营，并由 BattleManager 工作线程独占推进。
    /// </summary>
    public class CampManager
    {
        private readonly Dictionary<int, CampGroup> _campDict = new Dictionary<int, CampGroup>();
        /// <summary>按 UniqueId 索引的棋子实例。</summary>
        private readonly Dictionary<int, ChessController> _byUniqueId =
            new Dictionary<int, ChessController>();
        private readonly ChessConfigRepository _configs;
        /// <summary>按名称索引的行为树定义仓库。</summary>
        private readonly BehaviorTreeRepository _behaviorTrees;

        /// <summary>创建阵营管理器并绑定棋子配置与行为树仓库。</summary>
        public CampManager(ChessConfigRepository configs = null, BehaviorTreeRepository behaviorTrees = null)
        {
            _configs = configs ?? new ChessConfigRepository();
            _behaviorTrees = behaviorTrees ?? new BehaviorTreeRepository();
        }

        /// <summary>获取棋子配置仓库。</summary>
        public ChessConfigRepository Configs => _configs;

        /// <summary>获取行为树定义仓库。</summary>
        public BehaviorTreeRepository BehaviorTrees => _behaviorTrees;

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

        /// <summary>按 ConfigId 创建棋子并分配 UniqueId，供测试直接调用。</summary>
        public ChessController RegisterChess(int campId, int configId, Vector2 position)
        {
            return RegisterChess(campId, configId, ChessController.AllocateUniqueId(), position);
        }

        /// <summary>使用预分配 UniqueId 按 ConfigId 创建棋子并写入出生点。</summary>
        public ChessController RegisterChess(int campId, int configId, int uniqueId, Vector2 position)
        {
            if (uniqueId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(uniqueId));
            }

            if (_byUniqueId.ContainsKey(uniqueId))
            {
                throw new InvalidOperationException("UniqueId " + uniqueId + " 已存在。");
            }

            ChessController chess = GetOrCreateCamp(campId).RegisterChess(configId, uniqueId, position);
            if (chess == null)
            {
                return null;
            }

            _byUniqueId[uniqueId] = chess;
            return chess;
        }

        /// <summary>按 UniqueId 查找棋子实例。</summary>
        public bool TryGetChess(int uniqueId, out ChessController chess)
        {
            return _byUniqueId.TryGetValue(uniqueId, out chess) && chess != null;
        }

        /// <summary>按 UniqueId 注销棋子。</summary>
        public bool UnregisterChess(int uniqueId)
        {
            if (!_byUniqueId.TryGetValue(uniqueId, out ChessController chess))
            {
                return false;
            }

            return UnregisterChess(chess);
        }

        /// <summary>从所有阵营中注销指定棋子。</summary>
        public bool UnregisterChess(ChessController chess)
        {
            if (chess == null)
            {
                return false;
            }

            bool removed = false;
            foreach (CampGroup group in _campDict.Values)
            {
                if (group.UnregisterChess(chess))
                {
                    removed = true;
                    break;
                }
            }

            if (removed)
            {
                _byUniqueId.Remove(chess.UniqueId);
            }

            return removed;
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

        /// <summary>按导航代理 ID，相等时再按 UniqueId 对棋子进行稳定排序。</summary>
        private static int CompareChess(ChessController left, ChessController right)
        {
            int leftId = left?.MovementController?.Agent.AgentId ?? int.MaxValue;
            int rightId = right?.MovementController?.Agent.AgentId ?? int.MaxValue;
            int compare = leftId.CompareTo(rightId);
            if (compare != 0)
            {
                return compare;
            }

            int leftUniqueId = left == null ? int.MaxValue : left.UniqueId;
            int rightUniqueId = right == null ? int.MaxValue : right.UniqueId;
            return leftUniqueId.CompareTo(rightUniqueId);
        }
    }
}
