using UnityEngine;
using UnityEngine.Serialization;

namespace Editor
{
    [CreateAssetMenu(menuName = "Chess/ChessBaseData")]
    public class ChessBaseDataSo : ScriptableObject
    {
        /// <summary>棋子静态配置 ID。</summary>
        [FormerlySerializedAs("ChessId")]
        public int ConfigId;
        public string Name;
        public int MaxHp;
        public int Atk;
        /// <summary>棋子的基础攻击射程，使用棋盘 XZ 平面距离。</summary>
        public float Range;
        /// <summary>棋子的基础移动速度。</summary>
        public float MoveSpeed;

        /// <summary>棋子每秒可执行的基础攻击次数。</summary>
        public float AtkSpeed;

        public float WindUpTime;

        /// <summary>子弹在棋盘 XZ 平面上的移动速度；非正数表示即时伤害。</summary>
        public float BulletSpeed;
    }
}
