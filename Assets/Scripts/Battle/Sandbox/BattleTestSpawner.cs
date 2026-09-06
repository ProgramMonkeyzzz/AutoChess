using System;
using System.Collections.Generic;
using AutoChess;
using UnityEngine;

namespace AutoChess.Sandbox
{
    /// <summary>
    /// 战斗测试中单个棋子的出生配置。
    /// </summary>
    [Serializable]
    public class BattleTestChessConfig
    {
        /// <summary>棋子静态配置 ID。0=近战 Test0，1=远程 Test1。</summary>
        [Tooltip("棋子 ConfigId。0=近战 Test0，1=远程 Test1。")]
        public int ConfigId;

        /// <summary>棋盘 X 坐标，范围 0-100。</summary>
        [Tooltip("棋盘 X 坐标，范围 0-100。")]
        public float X = 50f;

        /// <summary>棋盘 Z 坐标，范围 0-100。</summary>
        [Tooltip("棋盘 Z 坐标，范围 0-100。")]
        public float Z = 50f;
    }

    /// <summary>
    /// 战斗测试中一个阵营及其棋子出生列表。
    /// </summary>
    [Serializable]
    public class BattleTestCampConfig
    {
        /// <summary>阵营 ID。1 蓝、2 红，其他灰色。</summary>
        [Tooltip("阵营 ID。1 蓝、2 红，其他灰色。")]
        public int CampId = 1;

        /// <summary>该阵营要生成的棋子列表。</summary>
        [Tooltip("该阵营棋子的 ConfigId 与棋盘 XZ 出生点。")]
        public List<BattleTestChessConfig> Chesses = new List<BattleTestChessConfig>();
    }

    /// <summary>
    /// 一条已通过校验、待交给 GameManager 注册的出生请求。
    /// </summary>
    public readonly struct BattleTestSpawnRequest
    {
        /// <summary>阵营 ID。</summary>
        public readonly int CampId;

        /// <summary>棋子静态配置 ID。</summary>
        public readonly int ConfigId;

        /// <summary>棋盘 XZ 出生点。</summary>
        public readonly Vector2 Position;

        /// <summary>创建一条出生请求。</summary>
        public BattleTestSpawnRequest(int campId, int configId, Vector2 position)
        {
            CampId = campId;
            ConfigId = configId;
            Position = position;
        }
    }

    /// <summary>
    /// 按阵营顺序收集合法出生点并调用注册回调。
    /// </summary>
    public static class BattleTestSpawner
    {
        /// <summary>按阵营配置顺序收集合法出生点；非法或越界项跳过并写入警告。</summary>
        public static List<BattleTestSpawnRequest> Collect(
            IReadOnlyList<BattleTestCampConfig> camps,
            ChessBoard board,
            ICollection<string> messages = null)
        {
            var requests = new List<BattleTestSpawnRequest>();
            if (camps == null)
            {
                return requests;
            }

            for (int campIndex = 0; campIndex < camps.Count; campIndex++)
            {
                BattleTestCampConfig camp = camps[campIndex];
                if (camp == null || camp.Chesses == null)
                {
                    continue;
                }

                for (int chessIndex = 0; chessIndex < camp.Chesses.Count; chessIndex++)
                {
                    BattleTestChessConfig chess = camp.Chesses[chessIndex];
                    if (chess == null)
                    {
                        continue;
                    }

                    if (!IsFinite(chess.X) || !IsFinite(chess.Z))
                    {
                        AddMessage(messages,
                            "BattleTestSpawner 跳过非法坐标 campId=" + camp.CampId
                            + " configId=" + chess.ConfigId
                            + " (" + chess.X + ", " + chess.Z + ")。");
                        continue;
                    }

                    Vector2 position = new Vector2(chess.X, chess.Z);
                    if (board != null && !board.ContainsPosition(position))
                    {
                        AddMessage(messages,
                            "BattleTestSpawner 跳过棋盘外坐标 campId=" + camp.CampId
                            + " configId=" + chess.ConfigId
                            + " (" + chess.X + ", " + chess.Z + ")。");
                        continue;
                    }

                    requests.Add(new BattleTestSpawnRequest(camp.CampId, chess.ConfigId, position));
                }
            }

            return requests;
        }

        /// <summary>收集合法出生点并逐个注册；注册返回 0 时记警告且继续后续棋子。</summary>
        public static int Spawn(
            IReadOnlyList<BattleTestCampConfig> camps,
            ChessBoard board,
            Func<int, int, Vector2, int> registerChess,
            ICollection<string> messages = null)
        {
            if (registerChess == null)
            {
                throw new ArgumentNullException(nameof(registerChess));
            }

            List<BattleTestSpawnRequest> requests = Collect(camps, board, messages);
            int spawned = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                BattleTestSpawnRequest request = requests[i];
                int uniqueId = registerChess(request.CampId, request.ConfigId, request.Position);
                if (uniqueId <= 0)
                {
                    AddMessage(messages,
                        "BattleTestSpawner 注册失败 campId=" + request.CampId
                        + " configId=" + request.ConfigId
                        + " (" + request.Position.x + ", " + request.Position.y + ")。");
                    continue;
                }

                spawned++;
            }

            return spawned;
        }

        /// <summary>判断浮点坐标是否为有限值。</summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>在调用方提供的列表中追加一条警告。</summary>
        private static void AddMessage(ICollection<string> messages, string message)
        {
            if (messages == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            messages.Add(message);
        }
    }
}
