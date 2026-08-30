using System;

namespace BehaviorTree
{
    /// <summary>
    /// 将行为树数据定义转换为运行时节点实例。
    /// </summary>
    public static class BehaviorTreeNodeFactory
    {
        /// <summary>
        /// 创建一个包含完整运行时节点树的根节点。
        /// </summary>
        public static RootNode Create(
            BehaviorTreeDefinition definition,
            BehaviorTreeRegistry registry)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            BehaviorTreeDefinitionValidator.Validate(definition);

            return (RootNode)CreateNode(definition.Root, registry, true);
        }

        /// <summary>
        /// 递归创建一个运行时节点。
        /// </summary>
        private static BehaviorNode CreateNode(
            NodeData data,
            BehaviorTreeRegistry registry,
            bool isRoot)
        {
            if (data == null)
            {
                throw new InvalidOperationException("行为树节点数据不能为 null。 ");
            }

            if (isRoot && !(data is RootNodeData))
            {
                throw new InvalidOperationException("行为树根节点必须是 RootNodeData。 ");
            }

            BehaviorNode node;
            if (data is RootNodeData rootData)
            {
                var root = new RootNode();
                AddChildren(root, rootData, registry);
                node = root;
            }
            else if (data is SequenceNodeData sequenceData)
            {
                var sequence = new SequenceNode();
                AddChildren(sequence, sequenceData, registry);
                node = sequence;
            }
            else if (data is SelectorNodeData selectorData)
            {
                var selector = new SelectorNode();
                AddChildren(selector, selectorData, registry);
                node = selector;
            }
            else if (data is ParallelNodeData parallelData)
            {
                var parallel = new ParallelNode(
                    parallelData.SuccessPolicy,
                    parallelData.FailurePolicy);
                AddChildren(parallel, parallelData, registry);
                node = parallel;
            }
            else if (data is ActionNodeData actionData)
            {
                EnsureLeaf(data);
                node = registry.CreateAction(actionData);
            }
            else if (data is ConditionalNodeData conditionalData)
            {
                EnsureLeaf(data);
                node = registry.CreateConditional(conditionalData);
            }
            else
            {
                throw new InvalidOperationException(
                    $"不支持的行为树节点数据类型 {data.GetType().FullName}。 ");
            }

            return node;
        }

        /// <summary>
        /// 将数据子节点递归添加到复合节点。
        /// </summary>
        private static void AddChildren(
            CompositeNode parent,
            NodeData data,
            BehaviorTreeRegistry registry)
        {
            if (data.Children == null)
            {
                throw new InvalidOperationException(
                    $"节点 {data.GetType().FullName} 的 Children 不能为 null。 ");
            }

            foreach (NodeData childData in data.Children)
            {
                parent.AddChild(CreateNode(childData, registry, false));
            }
        }

        /// <summary>
        /// 验证叶节点没有子节点。
        /// </summary>
        private static void EnsureLeaf(NodeData data)
        {
            if (data.Children != null && data.Children.Count > 0)
            {
                throw new InvalidOperationException(
                    $"叶节点 {data.GetType().FullName} 不能包含子节点。 ");
            }
        }
    }
}
