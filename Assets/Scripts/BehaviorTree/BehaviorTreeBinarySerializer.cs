using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace BehaviorTree
{
    /// <summary>
    /// 使用 BinaryFormatter 读写可信本地行为树定义的二进制序列化器。
    /// </summary>
    public static class BehaviorTreeBinarySerializer
    {
        /// <summary>
        /// 将行为树定义序列化为字节数组。
        /// </summary>
        public static byte[] Serialize(BehaviorTreeDefinition definition)
        {
            using (var stream = new MemoryStream())
            {
                Serialize(stream, definition);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 将行为树定义写入可写流；调用完成后保留流的打开状态。
        /// </summary>
        public static void Serialize(Stream stream, BehaviorTreeDefinition definition)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanWrite)
            {
                throw new ArgumentException("目标流不可写。 ", nameof(stream));
            }

            BehaviorTreeDefinitionValidator.Validate(definition);

#pragma warning disable SYSLIB0011
            var formatter = new BinaryFormatter();
            formatter.Serialize(stream, definition);
#pragma warning restore SYSLIB0011
        }

        /// <summary>
        /// 从字节数组反序列化行为树定义。
        /// </summary>
        public static BehaviorTreeDefinition Deserialize(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            using (var stream = new MemoryStream(data, false))
            {
                return Deserialize(stream);
            }
        }

        /// <summary>
        /// 从可读流反序列化行为树定义；仅允许读取可信来源的数据。
        /// </summary>
        public static BehaviorTreeDefinition Deserialize(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanRead)
            {
                throw new ArgumentException("源流不可读。 ", nameof(stream));
            }

#pragma warning disable SYSLIB0011
            var formatter = new BinaryFormatter();
            object value = formatter.Deserialize(stream);
#pragma warning restore SYSLIB0011

            if (!(value is BehaviorTreeDefinition definition))
            {
                throw new SerializationException("二进制内容不是 BehaviorTreeDefinition。 ");
            }

            BehaviorTreeDefinitionValidator.Validate(definition);
            return definition;
        }
    }

    /// <summary>
    /// 在构建或序列化前校验行为树数据结构。
    /// </summary>
    internal static class BehaviorTreeDefinitionValidator
    {
        /// <summary>
        /// 校验行为树版本、结构、策略和叶节点参数。
        /// </summary>
        internal static void Validate(BehaviorTreeDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.Version != BehaviorTreeDefinition.CurrentVersion)
            {
                throw new SerializationException(
                    $"不支持行为树数据版本 {definition.Version}，当前版本为 " +
                    $"{BehaviorTreeDefinition.CurrentVersion}。 ");
            }

            if (definition.Root == null)
            {
                throw new SerializationException("行为树定义缺少 RootNodeData。 ");
            }

            var visited = new HashSet<NodeData>();
            ValidateNode(definition.Root, true, visited);
        }

        /// <summary>
        /// 递归校验单个节点及其子节点。
        /// </summary>
        private static void ValidateNode(NodeData data, bool isRoot, HashSet<NodeData> visited)
        {
            if (data == null)
            {
                throw new SerializationException("行为树不能包含 null 子节点。 ");
            }

            if (!visited.Add(data))
            {
                throw new SerializationException("行为树不能包含循环或共享的节点数据引用。 ");
            }

            if (isRoot != (data is RootNodeData))
            {
                throw new SerializationException(
                    isRoot ? "行为树顶层节点必须是 RootNodeData。 " : "RootNodeData 只能位于行为树顶层。 ");
            }

            if (data.Children == null)
            {
                throw new SerializationException(
                    $"节点 {data.GetType().FullName} 的 Children 不能为 null。 ");
            }

            ValidateNodeType(data);

            foreach (NodeData child in data.Children)
            {
                ValidateNode(child, false, visited);
            }
        }

        /// <summary>
        /// 校验当前具体节点类型具有合法的字段和值。
        /// </summary>
        private static void ValidateNodeType(NodeData data)
        {
            if (data is RootNodeData && data.Children.Count > 1)
            {
                throw new SerializationException("RootNodeData 最多只能包含一个子节点。 ");
            }

            if (data is ParallelNodeData parallel)
            {
                if (!Enum.IsDefined(typeof(ParallelSuccessPolicy), parallel.SuccessPolicy)
                    || !Enum.IsDefined(typeof(ParallelFailurePolicy), parallel.FailurePolicy))
                {
                    throw new SerializationException("ParallelNodeData 包含无效的并行策略。 ");
                }

                return;
            }

            if (data is ActionNodeData action)
            {
                ValidateLeaf(action, action.LogicId, action.Parameters);
                return;
            }

            if (data is ConditionalNodeData conditional)
            {
                ValidateLeaf(conditional, conditional.LogicId, conditional.Parameters);
                return;
            }

            if (!(data is RootNodeData)
                && !(data is SequenceNodeData)
                && !(data is SelectorNodeData))
            {
                throw new SerializationException(
                    $"不支持的行为树节点数据类型 {data.GetType().FullName}。 ");
            }
        }

        /// <summary>
        /// 校验 Action 或 Conditional 叶节点。
        /// </summary>
        private static void ValidateLeaf(
            NodeData data,
            string logicId,
            Dictionary<string, object> parameters)
        {
            if (data.Children.Count > 0)
            {
                throw new SerializationException(
                    $"叶节点 {data.GetType().FullName} 不能包含子节点。 ");
            }

            if (string.IsNullOrWhiteSpace(logicId))
            {
                throw new SerializationException(
                    $"叶节点 {data.GetType().FullName} 的 LogicId 不能为空。 ");
            }

            if (parameters == null)
            {
                throw new SerializationException(
                    $"叶节点 {data.GetType().FullName} 的 Parameters 不能为 null。 ");
            }

            foreach (KeyValuePair<string, object> parameter in parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter.Key))
                {
                    throw new SerializationException("叶节点参数 key 不能为空。 ");
                }

                object value = parameter.Value;
                if (value != null && !value.GetType().IsSerializable)
                {
                    throw new SerializationException(
                        $"参数 '{parameter.Key}' 的类型 {value.GetType().FullName} 不可序列化。 ");
                }
            }
        }
    }
}
