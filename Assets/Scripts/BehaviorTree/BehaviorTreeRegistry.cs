using System;
using System.Collections.Generic;

namespace BehaviorTree
{
    /// <summary>
    /// 将可序列化节点数据中的 LogicId 注册为运行时逻辑工厂。
    /// </summary>
    public sealed class BehaviorTreeRegistry
    {
        /// <summary>按 LogicId 保存的 Action 节点工厂。</summary>
        private readonly Dictionary<string, Func<ActionNodeData, ActionNode>> _actionFactories =
            new Dictionary<string, Func<ActionNodeData, ActionNode>>();

        /// <summary>按 LogicId 保存的 Conditional 节点工厂。</summary>
        private readonly Dictionary<string, Func<ConditionalNodeData, ConditionalNode>> _conditionalFactories =
            new Dictionary<string, Func<ConditionalNodeData, ConditionalNode>>();

        /// <summary>
        /// 注册一个自定义 Action 工厂；重复 LogicId 会抛出异常。
        /// </summary>
        public void RegisterAction(string logicId, Func<ActionNodeData, ActionNode> factory)
        {
            ValidateLogicId(logicId);
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (_actionFactories.ContainsKey(logicId))
            {
                throw new InvalidOperationException($"Action LogicId '{logicId}' 已经注册。 ");
            }

            _actionFactories.Add(logicId, factory);
        }

        /// <summary>
        /// 注册一个自定义 Conditional 工厂；重复 LogicId 会抛出异常。
        /// </summary>
        public void RegisterConditional(
            string logicId,
            Func<ConditionalNodeData, ConditionalNode> factory)
        {
            ValidateLogicId(logicId);
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (_conditionalFactories.ContainsKey(logicId))
            {
                throw new InvalidOperationException($"Conditional LogicId '{logicId}' 已经注册。 ");
            }

            _conditionalFactories.Add(logicId, factory);
        }

        /// <summary>
        /// 根据 Action 数据创建运行时 Action 节点。
        /// </summary>
        internal ActionNode CreateAction(ActionNodeData data)
        {
            ValidateLogicId(data?.LogicId);
            if (!_actionFactories.TryGetValue(data.LogicId, out Func<ActionNodeData, ActionNode> factory))
            {
                throw new InvalidOperationException($"没有注册 Action LogicId '{data.LogicId}'。 ");
            }

            ActionNode node = factory(data);
            if (node == null)
            {
                throw new InvalidOperationException($"Action LogicId '{data.LogicId}' 的工厂返回了 null。 ");
            }

            return node;
        }

        /// <summary>
        /// 根据 Conditional 数据创建运行时 Conditional 节点。
        /// </summary>
        internal ConditionalNode CreateConditional(ConditionalNodeData data)
        {
            ValidateLogicId(data?.LogicId);
            if (!_conditionalFactories.TryGetValue(
                    data.LogicId,
                    out Func<ConditionalNodeData, ConditionalNode> factory))
            {
                throw new InvalidOperationException($"没有注册 Conditional LogicId '{data.LogicId}'。 ");
            }

            ConditionalNode node = factory(data);
            if (node == null)
            {
                throw new InvalidOperationException($"Conditional LogicId '{data.LogicId}' 的工厂返回了 null。 ");
            }

            return node;
        }

        /// <summary>
        /// 校验自定义逻辑标识。
        /// </summary>
        private static void ValidateLogicId(string logicId)
        {
            if (string.IsNullOrWhiteSpace(logicId))
            {
                throw new ArgumentException("LogicId 不能为空。 ", nameof(logicId));
            }
        }
    }
}
