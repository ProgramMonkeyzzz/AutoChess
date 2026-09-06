using System.Collections.Generic;
using AutoChess;
using Config;
using UnityEngine;

namespace AutoChess.Sandbox
{
    /// <summary>
    /// SampleScene 战斗测试布阵组件：Play 后按 Inspector 配置注册棋子。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class BattleTestRunner : MonoBehaviour
    {
        /// <summary>按阵营分组的棋子出生配置，默认空列表。</summary>
        [Header("战斗测试布阵")]
        [Tooltip("按阵营配置棋子。Play 后自动注册。ConfigId：0=近战 Test0，1=远程 Test1。X/Z 为棋盘坐标，范围 0-100。阵营 1 蓝、2 红。")]
        [SerializeField]
        private List<BattleTestCampConfig> _camps = new List<BattleTestCampConfig>();

        /// <summary>在 GameManager 初始化之后按配置注册棋子。</summary>
        private void Start()
        {
            if (_camps == null || _camps.Count == 0)
            {
                Debug.Log("BattleTestRunner 未配置棋子，跳过战斗测试生成。");
                return;
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.BattleManager == null)
            {
                Debug.LogError("BattleTestRunner 无法获取 GameManager 或 BattleManager。");
                return;
            }

            var messages = new List<string>();
            int spawned = BattleTestSpawner.Spawn(
                _camps,
                gameManager.BattleManager.ChessBoard,
                gameManager.RegisterChess,
                messages);
            for (int i = 0; i < messages.Count; i++)
            {
                Debug.LogWarning(messages[i]);
            }

            if (spawned > 0)
            {
                Debug.Log("BattleTestRunner 已注册 " + spawned + " 个棋子。");
            }
        }

        /// <summary>在 Scene 视图绘制出生点，便于填写棋盘 XZ 坐标。</summary>
        private void OnDrawGizmos()
        {
            if (_camps == null)
            {
                return;
            }

            for (int campIndex = 0; campIndex < _camps.Count; campIndex++)
            {
                BattleTestCampConfig camp = _camps[campIndex];
                if (camp == null || camp.Chesses == null)
                {
                    continue;
                }

                Color campColor = GetCampColor(camp.CampId);
                for (int chessIndex = 0; chessIndex < camp.Chesses.Count; chessIndex++)
                {
                    BattleTestChessConfig chess = camp.Chesses[chessIndex];
                    if (chess == null || !IsFinite(chess.X) || !IsFinite(chess.Z))
                    {
                        continue;
                    }

                    Vector3 worldPosition = new Vector3(chess.X, ChessBaseData.Radius, chess.Z);
                    Gizmos.color = campColor;
                    Gizmos.DrawSphere(worldPosition, ChessBaseData.Radius);
#if UNITY_EDITOR
                    UnityEditor.Handles.color = campColor;
                    UnityEditor.Handles.Label(worldPosition + Vector3.up * ChessBaseData.Radius,
                        chess.ConfigId.ToString());
#endif
                }
            }
        }

        /// <summary>按阵营返回着色：1 蓝、2 红、其他灰。</summary>
        private static Color GetCampColor(int campId)
        {
            switch (campId)
            {
                case 1:
                    return Color.blue;
                case 2:
                    return Color.red;
                default:
                    return Color.gray;
            }
        }

        /// <summary>判断浮点坐标是否为有限值。</summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
