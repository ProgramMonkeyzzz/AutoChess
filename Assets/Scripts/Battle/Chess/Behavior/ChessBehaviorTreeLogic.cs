using System;
using System.Collections.Generic;
using BehaviorTree;
using Chess;
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

        /// <summary>当前攻击冷却是否已经结束。</summary>
        public const string IsAttackCDReady = "IsAttackCDReady";

        /// <summary>自己的当前射程是否严格大于索敌目标的当前射程。</summary>
        public const string IsRangeGreaterThanTarget = "IsRangeGreaterThanTarget";

        /// <summary>对当前索敌目标发起一次攻击命令。</summary>
        public const string AttackTarget = "AttackTarget";

        /// <summary>向当前索敌目标下达一次追踪移动命令。</summary>
        public const string MoveToTarget = "MoveToTarget";

        /// <summary>向当前索敌目标反方向下达一次持续远离命令。</summary>
        public const string MoveAwayFromTarget = "MoveAwayFromTarget";

        /// <summary>停止并清除当前移动命令。</summary>
        public const string StopMovement = "StopMovement";

        /// <summary>按指定方式从敌方中选择当前索敌目标。</summary>
        public const string SelectTarget = "SelectTarget";

        /// <summary>SelectTarget 的索敌方式参数 key。</summary>
        public const string SelectTargetModeParameter = "Mode";

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
            RegisterCatalogConditional(IsAttackCDReady, "攻击冷却已就绪");
            RegisterCatalogConditional(IsRangeGreaterThanTarget, "自己射程大于目标射程");
            RegisterCatalogAction(AttackTarget, "攻击目标");
            RegisterCatalogAction(MoveToTarget, "向目标移动");
            RegisterCatalogAction(MoveAwayFromTarget, "向目标反方向移动");
            RegisterCatalogAction(StopMovement, "停止移动");
            RegisterCatalogAction(
                SelectTarget,
                "选择目标",
                new[]
                {
                    new BehaviorTreeParameterDefinition(
                        SelectTargetModeParameter,
                        BehaviorTreeParameterType.Int,
                        "索敌方式")
                    {
                        DefaultInt = (int)ChessTargetSelector.TargetSelectMode.Nearest
                    }
                });
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
            registry.RegisterConditional(IsAttackCDReady, data => new IsAttackCDReadyConditional());
            registry.RegisterConditional(IsRangeGreaterThanTarget, data => new IsRangeGreaterThanTargetConditional());
            registry.RegisterAction(AttackTarget, data => new AttackTargetAction());
            registry.RegisterAction(MoveToTarget, data => new MoveToTargetAction());
            registry.RegisterAction(MoveAwayFromTarget, data => new MoveAwayFromTargetAction());
            registry.RegisterAction(StopMovement, data => new StopMovementAction());
            registry.RegisterAction(SelectTarget, data => new SelectTargetAction(ReadSelectTargetMode(data)));
        }

        /// <summary>棋子运行时共享的 LogicId 注册表。</summary>
        private static readonly object RuntimeRegistryLock = new object();

        /// <summary>已绑定棋子 Action/Conditional 工厂的共享注册表。</summary>
        private static BehaviorTreeRegistry _runtimeRegistry;

        /// <summary>使用棋子 LogicId 注册表和独立黑板创建行为树运行器。</summary>
        public static BehaviorTreeRunner CreateRunner(BehaviorTreeDefinition definition, ChessController owner)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return new BehaviorTreeRunner(definition, GetOrCreateRuntimeRegistry(), new Blackboard(), owner);
        }

        /// <summary>获取已注册棋子 Action/Conditional 的共享运行时表。</summary>
        private static BehaviorTreeRegistry GetOrCreateRuntimeRegistry()
        {
            lock (RuntimeRegistryLock)
            {
                if (_runtimeRegistry == null)
                {
                    _runtimeRegistry = new BehaviorTreeRegistry();
                    Register(_runtimeRegistry);
                }

                return _runtimeRegistry;
            }
        }

        /// <summary>
        /// 从 Action 参数读取索敌方式；缺参或无法解析时使用最近敌人。
        /// </summary>
        private static ChessTargetSelector.TargetSelectMode ReadSelectTargetMode(ActionNodeData data)
        {
            if (data == null
                || data.Parameters == null
                || !data.Parameters.TryGetValue(SelectTargetModeParameter, out object raw)
                || raw == null)
            {
                return ChessTargetSelector.TargetSelectMode.Nearest;
            }

            if (raw is ChessTargetSelector.TargetSelectMode mode)
            {
                return mode;
            }

            try
            {
                return (ChessTargetSelector.TargetSelectMode)Convert.ToInt32(raw);
            }
            catch (Exception)
            {
                return ChessTargetSelector.TargetSelectMode.Nearest;
            }
        }

        /// <summary>
        /// 若目录中尚不存在指定 LogicId，则注册 Action 描述。
        /// </summary>
        private static void RegisterCatalogAction(
            string logicId,
            string displayName,
            IEnumerable<BehaviorTreeParameterDefinition> parameters = null)
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
                CatalogCategory,
                parameters);
        }

        /// <summary>
        /// 若目录中尚不存在指定 LogicId，则注册 Conditional 描述；Invert 由目录自动追加。
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
