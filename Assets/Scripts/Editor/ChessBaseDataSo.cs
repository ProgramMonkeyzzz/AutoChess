using UnityEngine;
using UnityEngine.Serialization;

namespace Editor
{
    [CreateAssetMenu(menuName = "Chess/ChessBaseData")]
    public class ChessBaseDataSo : ScriptableObject
    {
        /// <summary>Resources/Configs/BehaviorTrees 下的配置名，不含扩展名；留空表示不挂树。</summary>
        private const string BehaviorTreeNameTooltip =
            "Resources/Configs/BehaviorTrees 下的配置名，不含扩展名；留空表示不挂树。";

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

        /// <summary>Movement 状态 Enter 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        [Tooltip(BehaviorTreeNameTooltip)]
        public string MovementEnterBehaviorTree;

        /// <summary>Movement 状态 Stay 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        [Tooltip(BehaviorTreeNameTooltip)]
        public string MovementStayBehaviorTree;

        /// <summary>Movement 状态 Exit 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        [Tooltip(BehaviorTreeNameTooltip)]
        public string MovementExitBehaviorTree;

        /// <summary>Attack 状态 Enter 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        [Tooltip(BehaviorTreeNameTooltip)]
        public string AttackEnterBehaviorTree;

        /// <summary>Attack 状态 Stay 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        [Tooltip(BehaviorTreeNameTooltip)]
        public string AttackStayBehaviorTree;

        /// <summary>Attack 状态 Exit 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        [Tooltip(BehaviorTreeNameTooltip)]
        public string AttackExitBehaviorTree;

        /// <summary>Dead 状态 Enter 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        [Tooltip(BehaviorTreeNameTooltip)]
        public string DeadEnterBehaviorTree;

        /// <summary>Dead 状态 Stay 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        [Tooltip(BehaviorTreeNameTooltip)]
        public string DeadStayBehaviorTree;

        /// <summary>Dead 状态 Exit 行为树名，对应 Resources/Configs/BehaviorTrees 下的配置名（不含扩展名）；空白表示不挂树。</summary>
        [Tooltip(BehaviorTreeNameTooltip)]
        public string DeadExitBehaviorTree;
    }
}
