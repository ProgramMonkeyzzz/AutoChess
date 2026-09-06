#if UNITY_EDITOR

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
    /// 构建并重建棋子风筝行为树编辑器图。
    /// </summary>
    public static class ChessCombatBehaviorTreeGraphBuilder
    {
        /// <summary>提交到仓库的风筝行为树源资产路径。</summary>
        public const string AssetPath = "Assets/Configs/BehaviorTrees/ChessCombat.asset";

        /// <summary>
        /// 通过菜单重建风筝行为树源资产，不导出 bytes。
        /// </summary>
        [MenuItem("Tools/AutoChess/Behavior Tree/Rebuild Chess Combat Asset", priority = 1)]
        public static void RebuildCommittedAsset()
        {
            ChessBehaviorTreeLogic.EnsureCatalogRegistered();
            EnsureSourceFolder();

            var graph = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(AssetPath);
            var created = false;
            if (graph == null)
            {
                graph = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
                graph.name = "ChessCombat";
                created = true;
            }

            Populate(graph);
            if (created)
            {
                AssetDatabase.CreateAsset(graph, AssetPath);
            }
            else
            {
                EditorUtility.SetDirty(graph);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"已重建棋子风筝行为树图：{AssetPath}", graph);
        }

        /// <summary>
        /// 清空图节点并写入风筝行为树结构。
        /// </summary>
        public static void Populate(BehaviorTreeGraphAsset graph)
        {
            if (graph == null)
            {
                throw new System.ArgumentNullException(nameof(graph));
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

            var attackSequence = AddSequence(graph, new Vector2(-40f, 340f), "AttackIfReady");
            var kitingSelector = AddSelector(graph, new Vector2(200f, 340f), "KitingSelector");
            graph.ConnectNodes(combatSelector, attackSequence);
            graph.ConnectNodes(combatSelector, kitingSelector);

            graph.ConnectNodes(
                attackSequence,
                AddConditional(
                    graph,
                    new Vector2(-120f, 440f),
                    ChessBehaviorTreeLogic.IsAttackCDReady,
                    "IsAttackCDReady"));
            graph.ConnectNodes(
                attackSequence,
                AddAction(
                    graph,
                    new Vector2(40f, 440f),
                    ChessBehaviorTreeLogic.AttackTarget,
                    "AttackTarget"));

            var fleeSequence = AddSequence(graph, new Vector2(80f, 440f), "FleeIfThreatened");
            var holdOrChase = AddSelector(graph, new Vector2(320f, 440f), "HoldOrChase");
            graph.ConnectNodes(kitingSelector, fleeSequence);
            graph.ConnectNodes(kitingSelector, holdOrChase);

            graph.ConnectNodes(
                fleeSequence,
                AddConditional(
                    graph,
                    new Vector2(0f, 540f),
                    ChessBehaviorTreeLogic.IsSelfInTargetRange,
                    "IsSelfInTargetRange"));
            graph.ConnectNodes(
                fleeSequence,
                AddAction(
                    graph,
                    new Vector2(160f, 540f),
                    ChessBehaviorTreeLogic.MoveAwayFromTarget,
                    "MoveAwayFromTarget"));

            var holdSequence = AddSequence(graph, new Vector2(240f, 540f), "HoldIfInRange");
            graph.ConnectNodes(holdOrChase, holdSequence);
            graph.ConnectNodes(
                holdOrChase,
                AddAction(
                    graph,
                    new Vector2(440f, 540f),
                    ChessBehaviorTreeLogic.MoveToTarget,
                    "MoveToTarget"));

            graph.ConnectNodes(
                holdSequence,
                AddConditional(
                    graph,
                    new Vector2(160f, 640f),
                    ChessBehaviorTreeLogic.IsTargetInRange,
                    "IsTargetInRange"));
            graph.ConnectNodes(
                holdSequence,
                AddAction(
                    graph,
                    new Vector2(320f, 640f),
                    ChessBehaviorTreeLogic.StopMovement,
                    "StopMovement"));
        }

        /// <summary>
        /// 构建内存中的风筝行为树图并编译为运行时定义。
        /// </summary>
        public static bool TryBuildDefinition(
            out BehaviorTreeDefinition definition,
            out List<string> errors)
        {
            var graph = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
            try
            {
                Populate(graph);
                return BehaviorTreeGraphCompiler.TryBuildDefinition(graph, out definition, out errors);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
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
        /// 创建指定 LogicId 的 Conditional 节点。
        /// </summary>
        private static BehaviorTreeConditionalGraphNode AddConditional(
            BehaviorTreeGraphAsset graph,
            Vector2 position,
            string logicId,
            string name)
        {
            var node = graph.AddNode<BehaviorTreeConditionalGraphNode>(position);
            node.name = name;
            node.LogicId = logicId;
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
