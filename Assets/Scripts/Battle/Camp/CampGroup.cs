using System;
using System.Collections.Generic;
using AutoChess.Utils;
using Chess;
using Config;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// 保存一个阵营的棋子，并由所属 CampManager 的工作线程独占访问。
    /// </summary>
    public class CampGroup
    {
        private readonly int _campId;
        /// <summary>按 ConfigId 分组的棋子实例集合。</summary>
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

        /// <summary>按 ConfigId 查配置并使用预分配 UniqueId 创建棋子。</summary>
        public ChessController RegisterChess(int configId, int uniqueId, Vector2 position)
        {
            if (uniqueId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(uniqueId));
            }

            ChessConfigRepository configs = _campManager == null ? null : _campManager.Configs;
            if (configs == null || !configs.TryGet(configId, out ChessBaseData source) || source == null)
            {
                ThreadLogQueue.Enqueue(ThreadLogLevel.Error,
                    "CampGroup 找不到 ConfigId=" + configId + " 的棋子配置。");
                return null;
            }

            ChessController chess = new ChessController(uniqueId, source.Clone());
            if (chess.RuntimeData != null)
            {
                chess.RuntimeData.CurrentPosition = position;
            }

            if (!_chessDict.TryGetValue(configId, out HashSet<ChessController> chessSet))
            {
                chessSet = new HashSet<ChessController>();
                _chessDict.Add(configId, chessSet);
            }

            chessSet.Add(chess);
            chess.SetCampContext(_campId, configId, this);
            return chess;
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

        /// <summary>按导航代理 ID，相等时再按 UniqueId 对阵营内棋子进行稳定排序。</summary>
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
