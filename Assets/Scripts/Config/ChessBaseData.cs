using System;
using UnityEngine;

namespace Config
{
    [Serializable]
    public class ChessBaseData
    {
        public const float Radius = 0.5f;

        /// <summary>
        /// 将棋盘射程转换为双方中心点距离上限，补偿双方各 Radius 的碰撞半径。
        /// </summary>
        public static float ToCenterDistanceLimit(float range)
        {
            return range + Radius * 2f;
        }
        
        /// <summary>棋子静态配置 ID，同一配置可对应多个 UniqueId 实例。</summary>
        public int ConfigId;
        public string Name;
        public int MaxHp;
        public int Atk;
        /// <summary>棋子的基础攻击射程，使用棋盘 XZ 平面距离；判定中心距时会加上双方 Radius。</summary>
        public float Range;
        /// <summary>棋子的基础移动速度。</summary>
        public float MoveSpeed;

        /// <summary>棋子每秒可执行的基础攻击次数。</summary>
        public float AtkSpeed;

        public float WindUpTime;

        /// <summary>子弹在棋盘 XZ 平面上的移动速度；非正数表示即时伤害。</summary>
        public float BulletSpeed;

        /// <summary>Movement 状态 Enter 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        public string MovementEnterBehaviorTree;

        /// <summary>Movement 状态 Stay 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        public string MovementStayBehaviorTree;

        /// <summary>Movement 状态 Exit 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        public string MovementExitBehaviorTree;

        /// <summary>Attack 状态 Enter 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        public string AttackEnterBehaviorTree;

        /// <summary>Attack 状态 Stay 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        public string AttackStayBehaviorTree;

        /// <summary>Attack 状态 Exit 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        public string AttackExitBehaviorTree;

        /// <summary>Dead 状态 Enter 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        public string DeadEnterBehaviorTree;

        /// <summary>Dead 状态 Stay 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        public string DeadStayBehaviorTree;

        /// <summary>Dead 状态 Exit 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        public string DeadExitBehaviorTree;

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
                BulletSpeed = BulletSpeed,
                MovementEnterBehaviorTree = MovementEnterBehaviorTree,
                MovementStayBehaviorTree = MovementStayBehaviorTree,
                MovementExitBehaviorTree = MovementExitBehaviorTree,
                AttackEnterBehaviorTree = AttackEnterBehaviorTree,
                AttackStayBehaviorTree = AttackStayBehaviorTree,
                AttackExitBehaviorTree = AttackExitBehaviorTree,
                DeadEnterBehaviorTree = DeadEnterBehaviorTree,
                DeadStayBehaviorTree = DeadStayBehaviorTree,
                DeadExitBehaviorTree = DeadExitBehaviorTree
            };
        }
    }
}
