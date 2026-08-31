#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using BehaviorTree;
using NodeCanvas.Framework;

namespace AutoChess.Editor
{
    /// <summary>
    /// 将 NodeCanvas 行为树图编译为运行时行为树定义。
    /// </summary>
    public static class BehaviorTreeGraphCompiler
    {
        /// <summary>
        /// 尝试编译行为树图，并返回所有结构错误。
        /// </summary>
        public static bool TryBuildDefinition(
            BehaviorTreeGraphAsset graph,
            out BehaviorTreeDefinition definition,
            out List<string> errors)
        {
            definition = null;
            errors = new List<string>();

            if (graph == null)
            {
                errors.Add("行为树图不能为空。 ");
                return false;
            }

            if (graph.allNodes == null)
            {
                errors.Add("行为树图的节点列表不能为空。 ");
                return false;
            }

            var rootNodes = graph.allNodes
                .Where(node => node is BehaviorTreeRootGraphNode)
                .Cast<BehaviorTreeRootGraphNode>()
                .ToList();

            if (rootNodes.Count != 1)
            {
                errors.Add($"行为树必须包含且只能包含一个 Root，当前为 {rootNodes.Count} 个。 ");
            }

            var root = rootNodes.Count == 1 ? rootNodes[0] : null;
            if (root != null && graph.primeNode != root)
            {
                errors.Add("Root 必须是 NodeCanvas 图的 Prime 节点。 ");
            }

            ValidateNodeCollection(graph, root, errors);

            if (root == null)
            {
                return false;
            }

            var visited = new HashSet<Node>();
            var visiting = new HashSet<Node>();
            var rootData = ConvertNode(root, true, visited, visiting, errors) as RootNodeData;
            if (rootData == null)
            {
                errors.Add("无法将 Root 编译为 RootNodeData。 ");
            }

            foreach (Node node in graph.allNodes)
            {
                if (node != null && !visited.Contains(node))
                {
                    errors.Add($"节点 '{node.name}' 未连接到 Root。 ");
                }
            }

            if (errors.Count > 0 || rootData == null)
            {
                return false;
            }

            definition = new BehaviorTreeDefinition(rootData);
            return true;
        }

        /// <summary>
        /// 校验图节点集合的 Root、端口和节点类型约束。
        /// </summary>
        private static void ValidateNodeCollection(
            BehaviorTreeGraphAsset graph,
            BehaviorTreeRootGraphNode root,
            List<string> errors)
        {
            var nodeSet = new HashSet<Node>();
            foreach (Node node in graph.allNodes)
            {
                if (node == null)
                {
                    continue;
                }

                if (!nodeSet.Add(node))
                {
                    errors.Add($"节点 '{node.name}' 在图节点列表中重复引用。 ");
                }
            }

            foreach (Node node in graph.allNodes)
            {
                if (node == null)
                {
                    errors.Add("行为树不能包含 null 节点。 ");
                    continue;
                }

                if (!(node is BehaviorTreeGraphNode))
                {
                    errors.Add($"节点 '{node.name}' 不是本项目行为树节点类型。 ");
                    continue;
                }

                if (node != root && node.inConnections.Count != 1)
                {
                    errors.Add($"节点 '{node.name}' 必须且只能拥有一个父节点。 ");
                }

                if (node is BehaviorTreeRootGraphNode)
                {
                    if (node.inConnections.Count != 0)
                    {
                        errors.Add("Root 不能拥有父节点。 ");
                    }

                    if (node.outConnections.Count > 1)
                    {
                        errors.Add("Root 最多只能拥有一个子节点。 ");
                    }
                }

                if (node is BehaviorTreeLeafGraphNode && node.outConnections.Count > 0)
                {
                    errors.Add($"叶节点 '{node.name}' 不能拥有子节点。 ");
                }

                if (node is BehaviorTreeActionGraphNode action
                    && string.IsNullOrWhiteSpace(action.LogicId))
                {
                    errors.Add($"Action 节点 '{node.name}' 的 LogicId 不能为空。 ");
                }

                if (node is BehaviorTreeConditionalGraphNode conditional
                    && string.IsNullOrWhiteSpace(conditional.LogicId))
                {
                    errors.Add($"Conditional 节点 '{node.name}' 的 LogicId 不能为空。 ");
                }

                foreach (Connection connection in node.outConnections)
                {
                    if (connection == null || connection.targetNode == null)
                    {
                        errors.Add($"节点 '{node.name}' 包含无效连线。 ");
                        continue;
                    }

                    if (!nodeSet.Contains(connection.targetNode))
                    {
                        errors.Add($"节点 '{node.name}' 的连线目标不属于当前行为树图。 ");
                        continue;
                    }

                    if (connection.targetNode is BehaviorTreeRootGraphNode)
                    {
                        errors.Add("行为树连线不能指向 Root。 ");
                    }
                }
            }
        }

        /// <summary>
        /// 递归转换一个 NodeCanvas 节点及其子节点。
        /// </summary>
        private static NodeData ConvertNode(
            Node node,
            bool isRoot,
            HashSet<Node> visited,
            HashSet<Node> visiting,
            List<string> errors)
        {
            if (node == null)
            {
                errors.Add("行为树不能包含 null 子节点。 ");
                return null;
            }

            if (visiting.Contains(node))
            {
                errors.Add($"节点 '{node.name}' 参与了循环连线。 ");
                return null;
            }

            if (!visited.Add(node))
            {
                errors.Add($"节点 '{node.name}' 被多个父节点共享。 ");
                return null;
            }

            if (!visiting.Add(node))
            {
                errors.Add($"节点 '{node.name}' 无法进入编译状态。 ");
                return null;
            }

            NodeData data = CreateNodeData(node, isRoot, errors);
            if (data == null)
            {
                visiting.Remove(node);
                return null;
            }

            foreach (Connection connection in node.outConnections)
            {
                if (connection == null || connection.targetNode == null)
                {
                    continue;
                }

                NodeData childData = ConvertNode(
                    connection.targetNode,
                    false,
                    visited,
                    visiting,
                    errors);
                if (childData == null)
                {
                    continue;
                }

                try
                {
                    data.AddChild(childData);
                }
                catch (Exception exception)
                {
                    errors.Add($"节点 '{node.name}' 添加子节点失败：{exception.Message}");
                }
            }

            visiting.Remove(node);
            return data;
        }

        /// <summary>
        /// 根据图节点类型创建对应的运行时数据节点。
        /// </summary>
        private static NodeData CreateNodeData(Node node, bool isRoot, List<string> errors)
        {
            string nodeName = string.IsNullOrWhiteSpace(node.name) ? null : node.name;

            if (node is BehaviorTreeRootGraphNode)
            {
                if (!isRoot)
                {
                    errors.Add("Root 只能位于行为树顶层。 ");
                    return null;
                }

                return new RootNodeData(nodeName);
            }

            if (isRoot)
            {
                errors.Add("行为树顶层节点必须是 Root。 ");
                return null;
            }

            if (node is BehaviorTreeSequenceGraphNode)
            {
                return new SequenceNodeData(nodeName);
            }

            if (node is BehaviorTreeSelectorGraphNode)
            {
                return new SelectorNodeData(nodeName);
            }

            var parallel = node as BehaviorTreeParallelGraphNode;
            if (parallel != null)
            {
                return new ParallelNodeData(parallel.successPolicy, parallel.failurePolicy, nodeName);
            }

            var action = node as BehaviorTreeActionGraphNode;
            if (action != null)
            {
                var actionData = new ActionNodeData(action.LogicId, nodeName);
                CopyParameters(action.Parameters, actionData.Parameters, nodeName, errors);
                return actionData;
            }

            var conditional = node as BehaviorTreeConditionalGraphNode;
            if (conditional != null)
            {
                var conditionalData = new ConditionalNodeData(conditional.LogicId, nodeName);
                CopyParameters(conditional.Parameters, conditionalData.Parameters, nodeName, errors);
                return conditionalData;
            }

            errors.Add($"不支持的行为树图节点类型 {node.GetType().FullName}。 ");
            return null;
        }

        /// <summary>
        /// 校验并复制叶节点参数到运行时字典。
        /// </summary>
        private static void CopyParameters(
            IEnumerable<BehaviorTreeEditorParameter> source,
            IDictionary<string, object> target,
            string nodeName,
            List<string> errors)
        {
            if (source == null)
            {
                errors.Add($"叶节点 '{nodeName}' 的参数列表不能为空。 ");
                return;
            }

            foreach (BehaviorTreeEditorParameter parameter in source)
            {
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.key))
                {
                    errors.Add($"叶节点 '{nodeName}' 的参数 key 不能为空。 ");
                    continue;
                }

                if (!Enum.IsDefined(typeof(BehaviorTreeParameterType), parameter.type))
                {
                    errors.Add($"叶节点 '{nodeName}' 的参数 '{parameter.key}' 类型无效。 ");
                    continue;
                }

                if (!target.ContainsKey(parameter.key))
                {
                    target.Add(parameter.key, parameter.ToRuntimeValue());
                }
                else
                {
                    errors.Add($"叶节点 '{nodeName}' 的参数 key '{parameter.key}' 重复。 ");
                }
            }
        }
    }
}

#endif
