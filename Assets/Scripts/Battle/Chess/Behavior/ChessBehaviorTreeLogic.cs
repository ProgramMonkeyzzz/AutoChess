using System;
using BehaviorTree;
using UnityEngine;

namespace Chess.Behavior
{
    /// <summary>
    /// 注册棋子行为树 Action/Conditional 的 LogicId、编辑器目录和运行时工厂。
    /// </summary>
    public static class ChessBehaviorTreeLogic
    {
        /// <summary>当前是否存在索敌目标。</summary>
        public const string HasTarget = "HasTarget";

        /// <summary>当前索敌目标是否在自己的运行时射程内。</summary>
        public const string IsTargetInRange = "IsTargetInRange";

        /// <summary>自己是否在当前索敌目标的运行时射程内。</summary>
        public const string IsSelfInTargetRange = "IsSelfInTargetRange";

        /// <summary>当前索敌目标是否已经死亡。</summary>
        public const string IsTargetDead = "IsTargetDead";

        /// <summary>对当前索敌目标发起一次攻击命令。</summary>
        public const string AttackTarget = "AttackTarget";

        /// <summary>向当前索敌目标下达一次追踪移动命令。</summary>
        public const string MoveToTarget = "MoveToTarget";

        /// <summary>向当前索敌目标反方向下达一次持续远离命令。</summary>
        public const string MoveAwayFromTarget = "MoveAwayFromTarget";

        /// <summary>编辑器逻辑目录中的分类名称。</summary>
        public const string CatalogCategory = "Chess";

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器域加载时把棋子 Action/Conditional 写入逻辑目录。
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod]
        private static void BootstrapCatalogInEditor()
        {
            EnsureCatalogRegistered();
        }
#endif

        /// <summary>
        /// 运行时域加载时把棋子 Action/Conditional 写入逻辑目录。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BootstrapCatalogAtRuntime()
        {
            EnsureCatalogRegistered();
        }

        /// <summary>
        /// 幂等注册棋子 Action/Conditional 的编辑器逻辑目录。
        /// </summary>
        public static void EnsureCatalogRegistered()
        {
            RegisterCatalogConditional(HasTarget, "当前存在目标");
            RegisterCatalogConditional(IsTargetInRange, "目标在自己射程内");
            RegisterCatalogConditional(IsSelfInTargetRange, "自己在目标射程内");
            RegisterCatalogConditional(IsTargetDead, "当前目标已死亡");
            RegisterCatalogAction(AttackTarget, "攻击目标");
            RegisterCatalogAction(MoveToTarget, "向目标移动");
            RegisterCatalogAction(MoveAwayFromTarget, "向目标反方向移动");
        }

        /// <summary>
        /// 向指定注册表绑定棋子 Action/Conditional 工厂，并确保编辑器目录已注册。
        /// </summary>
        public static void Register(BehaviorTreeRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            EnsureCatalogRegistered();
            registry.RegisterConditional(HasTarget, data => new HasTargetConditional());
            registry.RegisterConditional(IsTargetInRange, data => new IsTargetInRangeConditional());
            registry.RegisterConditional(IsSelfInTargetRange, data => new IsSelfInTargetRangeConditional());
            registry.RegisterConditional(IsTargetDead, data => new IsTargetDeadConditional());
            registry.RegisterAction(AttackTarget, data => new AttackTargetAction());
            registry.RegisterAction(MoveToTarget, data => new MoveToTargetAction());
            registry.RegisterAction(MoveAwayFromTarget, data => new MoveAwayFromTargetAction());
        }

        /// <summary>
        /// 若目录中尚不存在指定 LogicId，则注册无参 Action 描述。
        /// </summary>
        private static void RegisterCatalogAction(string logicId, string displayName)
        {
            if (BehaviorTreeLogicCatalog.TryGet(
                    BehaviorTreeLogicKind.Action,
                    logicId,
                    out _))
            {
                return;
            }

            BehaviorTreeLogicCatalog.RegisterAction(
                logicId,
                displayName,
                CatalogCategory);
        }

        /// <summary>
        /// 若目录中尚不存在指定 LogicId，则注册无参 Conditional 描述。
        /// </summary>
        private static void RegisterCatalogConditional(string logicId, string displayName)
        {
            if (BehaviorTreeLogicCatalog.TryGet(
                    BehaviorTreeLogicKind.Conditional,
                    logicId,
                    out _))
            {
                return;
            }

            BehaviorTreeLogicCatalog.RegisterConditional(
                logicId,
                displayName,
                CatalogCategory);
        }
    }
}
