#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using BehaviorTree;
using Chess.Behavior;
using NodeCanvas.Framework;
using UnityEditor;
using UnityEngine;

namespace AutoChess.Editor
{
    /// <summary>
    /// 构建并重建棋子战斗行为树编辑器图；近战/远程按相对射程合并到同一棵树。
    /// </summary>
    public static class ChessCombatBehaviorTreeGraphBuilder
    {
        /// <summary>提交到仓库的战斗行为树源资产路径。</summary>
        public const string AssetPath = "Assets/Configs/BehaviorTrees/ChessCombat.asset";

        /// <summary>战斗行为树运行时 bytes 路径。</summary>
        public const string BytesPath = "Assets/Resources/Configs/BehaviorTrees/ChessCombat.bytes";

        /// <summary>
        /// 通过菜单重建战斗行为树源资产，不导出 bytes。
        /// </summary>
        [MenuItem("Tools/AutoChess/Behavior Tree/Rebuild Chess Combat Asset", priority = 1)]
        public static void RebuildCommittedAsset()
        {
            RebuildAsset(AssetPath, "ChessCombat", Populate, "已重建棋子战斗行为树图：");
        }

        /// <summary>
        /// 通过菜单重建战斗行为树源资产并导出运行时 bytes。
        /// </summary>
        [MenuItem("Tools/AutoChess/Behavior Tree/Rebuild And Export Chess Combat", priority = 2)]
        public static void RebuildAndExportCommittedAsset()
        {
            RebuildCommittedAsset();
            ExportAsset(AssetPath, BytesPath, "未找到战斗行为树图：", "导出 ChessCombat.bytes 失败。", "已导出棋子战斗行为树：");
        }

        /// <summary>
        /// 供 Unity 批处理调用：重建并导出战斗树，同时导出棋子配置。
        /// </summary>
        public static void RebuildCombatTreeAndExportConfigs()
        {
            RebuildAndExportCommittedAsset();
            ConfigBinaryExporter.GenerateBinaryConfigs();
        }

        /// <summary>
        /// 清空图节点并写入相对射程战斗树：超距追击，射程更长则逃离，相对近战且自身在目标射程外则追击，否则停止。
        /// </summary>
        public static void Populate(BehaviorTreeGraphAsset graph)
        {
            BehaviorTreeSelectorGraphNode combatSelector = PopulateSharedRoot(graph);

            // CD 就绪且目标在自己射程内时优先攻击。
            var attackSequence = AddSequence(graph, new Vector2(-360f, 340f), "AttackIfReady");
            // 目标超出自己射程时一律追击，近战远程都先贴近。
            var chaseSequence = AddSequence(graph, new Vector2(-80f, 340f), "ChaseIfOutOfRange");
            // 自身当前射程更长：安全区或贴身都撤离风筝。
            var fleeSequence = AddSequence(graph, new Vector2(160f, 340f), "FleeIfLongerRange");
            // 相对近战且自身还不在目标射程内时继续逼近。
            var shorterSafeSequence = AddSequence(graph, new Vector2(420f, 340f), "ChaseIfShorterAndSafe");
            graph.ConnectNodes(combatSelector, attackSequence);
            graph.ConnectNodes(combatSelector, chaseSequence);
            graph.ConnectNodes(combatSelector, fleeSequence);
            graph.ConnectNodes(combatSelector, shorterSafeSequence);
            // 双方都能打到且自己并不更远时近战站桩。
            graph.ConnectNodes(
                combatSelector,
                AddAction(
                    graph,
                    new Vector2(680f, 340f),
                    ChessBehaviorTreeLogic.StopMovement,
                    "StopMovement"));

            AddAttackSequenceChildren(graph, attackSequence);

            graph.ConnectNodes(
                chaseSequence,
                AddConditional(
                    graph,
                    new Vector2(-160f, 440f),
                    ChessBehaviorTreeLogic.IsTargetInRange,
                    "IsTargetOutOfRange",
                    true));
            graph.ConnectNodes(
                chaseSequence,
                AddAction(
                    graph,
                    new Vector2(-20f, 440f),
                    ChessBehaviorTreeLogic.MoveToTarget,
                    "MoveToTarget"));

            graph.ConnectNodes(
                fleeSequence,
                AddConditional(
                    graph,
                    new Vector2(80f, 440f),
                    ChessBehaviorTreeLogic.IsRangeGreaterThanTarget,
                    "IsRangeGreaterThanTarget"));
            graph.ConnectNodes(
                fleeSequence,
                AddAction(
                    graph,
                    new Vector2(240f, 440f),
                    ChessBehaviorTreeLogic.MoveAwayFromTarget,
                    "MoveAwayFromTarget"));

            graph.ConnectNodes(
                shorterSafeSequence,
                AddConditional(
                    graph,
                    new Vector2(360f, 440f),
                    ChessBehaviorTreeLogic.IsRangeGreaterThanTarget,
                    "IsRangeNotGreaterThanTarget",
                    true));
            graph.ConnectNodes(
                shorterSafeSequence,
                AddConditional(
                    graph,
                    new Vector2(520f, 440f),
                    ChessBehaviorTreeLogic.IsSelfInTargetRange,
                    "IsSelfOutOfTargetRange",
                    true));
            graph.ConnectNodes(
                shorterSafeSequence,
                AddAction(
                    graph,
                    new Vector2(680f, 440f),
                    ChessBehaviorTreeLogic.MoveToTarget,
                    "MoveToTarget"));
        }

        /// <summary>
        /// 构建内存中的战斗行为树图并编译为运行时定义。
        /// </summary>
        public static bool TryBuildDefinition(
            out BehaviorTreeDefinition definition,
            out List<string> errors)
        {
            return TryBuildDefinition(Populate, out definition, out errors);
        }

        /// <summary>
        /// 写入共用的 Root、换目标和索敌结构，并返回战斗选择器。
        /// </summary>
        private static BehaviorTreeSelectorGraphNode PopulateSharedRoot(BehaviorTreeGraphAsset graph)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            ChessBehaviorTreeLogic.EnsureCatalogRegistered();
            ClearNodes(graph);

            var root = AddRoot(graph, new Vector2(0f, -80f), "Root");
            var rootSelector = AddSelector(graph, new Vector2(0f, 40f), "RootSelector");
            graph.ConnectNodes(root, rootSelector);

            var retargetSequence = AddSequence(graph, new Vector2(-420f, 140f), "RetargetIfDead");
            var combatSequence = AddSequence(graph, new Vector2(0f, 140f), "CombatIfHasTarget");
            var acquireTarget = AddSelectTarget(graph, new Vector2(420f, 140f), "SelectTarget");
            graph.ConnectNodes(rootSelector, retargetSequence);
            graph.ConnectNodes(rootSelector, combatSequence);
            graph.ConnectNodes(rootSelector, acquireTarget);

            graph.ConnectNodes(
                retargetSequence,
                AddConditional(
                    graph,
                    new Vector2(-500f, 240f),
                    ChessBehaviorTreeLogic.IsTargetDead,
                    "IsTargetDead"));
            graph.ConnectNodes(
                retargetSequence,
                AddSelectTarget(graph, new Vector2(-340f, 240f), "ReselectTarget"));

            graph.ConnectNodes(
                combatSequence,
                AddConditional(
                    graph,
                    new Vector2(-80f, 240f),
                    ChessBehaviorTreeLogic.HasTarget,
                    "HasTarget"));
            var combatSelector = AddSelector(graph, new Vector2(80f, 240f), "CombatSelector");
            graph.ConnectNodes(combatSequence, combatSelector);
            return combatSelector;
        }

        /// <summary>
        /// 为攻击序列挂上冷却、射程和攻击叶节点。
        /// </summary>
        private static void AddAttackSequenceChildren(
            BehaviorTreeGraphAsset graph,
            BehaviorTreeSequenceGraphNode attackSequence)
        {
            graph.ConnectNodes(
                attackSequence,
                AddConditional(
                    graph,
                    new Vector2(-360f, 440f),
                    ChessBehaviorTreeLogic.IsAttackCDReady,
                    "IsAttackCDReady"));
            graph.ConnectNodes(
                attackSequence,
                AddConditional(
                    graph,
                    new Vector2(-200f, 440f),
                    ChessBehaviorTreeLogic.IsTargetInRange,
                    "IsTargetInRange"));
            graph.ConnectNodes(
                attackSequence,
                AddAction(
                    graph,
                    new Vector2(-40f, 440f),
                    ChessBehaviorTreeLogic.AttackTarget,
                    "AttackTarget"));
        }

        /// <summary>
        /// 用指定填充函数构建内存图并编译为运行时定义。
        /// </summary>
        private static bool TryBuildDefinition(
            Action<BehaviorTreeGraphAsset> populate,
            out BehaviorTreeDefinition definition,
            out List<string> errors)
        {
            var graph = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
            try
            {
                populate(graph);
                return BehaviorTreeGraphCompiler.TryBuildDefinition(graph, out definition, out errors);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(graph);
            }
        }

        /// <summary>
        /// 重建指定路径的行为树源资产，不导出 bytes。
        /// </summary>
        private static void RebuildAsset(
            string assetPath,
            string graphName,
            Action<BehaviorTreeGraphAsset> populate,
            string logPrefix)
        {
            ChessBehaviorTreeLogic.EnsureCatalogRegistered();
            EnsureSourceFolder();

            var graph = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(assetPath);
            var created = false;
            if (graph == null)
            {
                graph = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
                graph.name = graphName;
                created = true;
            }

            populate(graph);
            if (created)
            {
                AssetDatabase.CreateAsset(graph, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(graph);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(logPrefix + assetPath, graph);
        }

        /// <summary>
        /// 将指定源资产导出为运行时 bytes。
        /// </summary>
        private static void ExportAsset(
            string assetPath,
            string bytesPath,
            string missingMessage,
            string exportFailMessage,
            string successPrefix)
        {
            var graph = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(assetPath);
            if (graph == null)
            {
                throw new InvalidOperationException(missingMessage + assetPath);
            }

            if (!BehaviorTreeGraphExporter.ExportToPath(graph, bytesPath, out string error))
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error) ? exportFailMessage : error);
            }

            Debug.Log(successPrefix + bytesPath, graph);
        }

        /// <summary>
        /// 删除图中全部现有节点；force 用于移除带 ProtectedSingleton 的 Root。
        /// </summary>
        private static void ClearNodes(BehaviorTreeGraphAsset graph)
        {
            if (graph.allNodes == null)
            {
                return;
            }

            var nodes = new List<Node>(graph.allNodes);
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null)
                {
                    graph.RemoveNode(nodes[i], false, true);
                }
            }
        }

        /// <summary>
        /// 创建 Root 节点。
        /// </summary>
        private static BehaviorTreeRootGraphNode AddRoot(
            BehaviorTreeGraphAsset graph,
            Vector2 position,
            string name)
        {
            var node = graph.AddNode<BehaviorTreeRootGraphNode>(position);
            node.name = name;
            return node;
        }

        /// <summary>
        /// 创建 Selector 节点。
        /// </summary>
        private static BehaviorTreeSelectorGraphNode AddSelector(
            BehaviorTreeGraphAsset graph,
            Vector2 position,
            string name)
        {
            var node = graph.AddNode<BehaviorTreeSelectorGraphNode>(position);
            node.name = name;
            return node;
        }

        /// <summary>
        /// 创建 Sequence 节点。
        /// </summary>
        private static BehaviorTreeSequenceGraphNode AddSequence(
            BehaviorTreeGraphAsset graph,
            Vector2 position,
            string name)
        {
            var node = graph.AddNode<BehaviorTreeSequenceGraphNode>(position);
            node.name = name;
            return node;
        }

        /// <summary>
        /// 创建指定 LogicId 的 Conditional 节点，可设置 Invert。
        /// </summary>
        private static BehaviorTreeConditionalGraphNode AddConditional(
            BehaviorTreeGraphAsset graph,
            Vector2 position,
            string logicId,
            string name,
            bool invert = false)
        {
            var node = graph.AddNode<BehaviorTreeConditionalGraphNode>(position);
            node.name = name;
            node.LogicId = logicId;
            if (invert)
            {
                node.Parameters.Clear();
                node.Parameters.Add(
                    new BehaviorTreeEditorParameter
                    {
                        key = ConditionalNode.InvertParameterKey,
                        type = BehaviorTreeParameterType.Bool,
                        boolValue = true
                    });
            }

            return node;
        }

        /// <summary>
        /// 创建指定 LogicId 的 Action 节点。
        /// </summary>
        private static BehaviorTreeActionGraphNode AddAction(
            BehaviorTreeGraphAsset graph,
            Vector2 position,
            string logicId,
            string name)
        {
            var node = graph.AddNode<BehaviorTreeActionGraphNode>(position);
            node.name = name;
            node.LogicId = logicId;
            return node;
        }

        /// <summary>
        /// 创建 Mode=最近 的 SelectTarget Action。
        /// </summary>
        private static BehaviorTreeActionGraphNode AddSelectTarget(
            BehaviorTreeGraphAsset graph,
            Vector2 position,
            string name)
        {
            var node = AddAction(graph, position, ChessBehaviorTreeLogic.SelectTarget, name);
            node.Parameters.Clear();
            node.Parameters.Add(
                new BehaviorTreeEditorParameter
                {
                    key = ChessBehaviorTreeLogic.SelectTargetModeParameter,
                    type = BehaviorTreeParameterType.Int,
                    intValue = (int)Chess.ChessTargetSelector.TargetSelectMode.Nearest
                });
            return node;
        }

        /// <summary>
        /// 确保行为树源资产目录存在。
        /// </summary>
        private static void EnsureSourceFolder()
        {
            var projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
            {
                return;
            }

            var absoluteFolder = Path.Combine(
                projectRoot.FullName,
                BehaviorTreeGraphAsset.DefaultSourceFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(absoluteFolder);
            AssetDatabase.Refresh();
        }
    }
}

#endif
