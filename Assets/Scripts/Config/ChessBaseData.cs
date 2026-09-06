using System;
using UnityEngine;

namespace Config
{
    [Serializable]
    public class ChessBaseData
    {
        public const float Radius = 0.5f;
        
        /// <summary>棋子静态配置 ID，同一配置可对应多个 UniqueId 实例。</summary>
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

        /// <summary>复制一份静态配置，避免多个棋子实例共享可变数据。</summary>
        public ChessBaseData Clone()
        {
            return new ChessBaseData
            {
                ConfigId = ConfigId,
                Name = Name,
                MaxHp = MaxHp,
                Atk = Atk,
                Range = Range,
                MoveSpeed = MoveSpeed,
                AtkSpeed = AtkSpeed,
                WindUpTime = WindUpTime,
                BulletSpeed = BulletSpeed
            };
        }
    }
}
