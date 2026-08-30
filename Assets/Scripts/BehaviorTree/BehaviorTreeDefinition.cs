using System;
using System.Collections.Generic;

namespace BehaviorTree
{
    /// <summary>
    /// 可序列化的行为树数据定义，不包含运行时节点状态和黑板值。
    /// </summary>
    [Serializable]
    public sealed class BehaviorTreeDefinition
    {
        /// <summary>当前支持的行为树数据版本。</summary>
        public const int CurrentVersion = 1;

        /// <summary>该定义的格式版本。</summary>
        public int Version = CurrentVersion;

        /// <summary>行为树根节点数据。</summary>
        public RootNodeData Root;

        /// <summary>
        /// 创建空行为树定义。
        /// </summary>
        public BehaviorTreeDefinition()
        {
        }

        /// <summary>
        /// 创建包含指定根节点数据的行为树定义。
        /// </summary>
        public BehaviorTreeDefinition(RootNodeData root)
        {
            Root = root;
        }
    }

    /// <summary>
    /// 所有节点数据的公共基类。
    /// </summary>
    [Serializable]
    public abstract class NodeData
    {
        /// <summary>
        /// 创建节点数据。
        /// </summary>
        protected NodeData(string name = null)
        {
            Name = name;
        }

        /// <summary>节点显示名称。</summary>
        public string Name;

        /// <summary>节点的子节点数据。</summary>
        public List<NodeData> Children = new List<NodeData>();

        /// <summary>
        /// 添加一个非空子节点数据。
        /// </summary>
        public virtual void AddChild(NodeData child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            Children.Add(child);
        }
    }

    /// <summary>
    /// 根节点数据，最多包含一个子节点。
    /// </summary>
    [Serializable]
    public sealed class RootNodeData : NodeData
    {
        /// <summary>
        /// 创建根节点数据。
        /// </summary>
        public RootNodeData(string name = null)
            : base(name)
        {
        }

        /// <summary>
        /// 添加唯一的根子节点数据。
        /// </summary>
        public override void AddChild(NodeData child)
        {
            if (Children.Count > 0)
            {
                throw new InvalidOperationException("RootNodeData 只能包含一个子节点。 ");
            }

            base.AddChild(child);
        }
    }

    /// <summary>
    /// Sequence 节点数据。
    /// </summary>
    [Serializable]
    public sealed class SequenceNodeData : NodeData
    {
        /// <summary>
        /// 创建 Sequence 节点数据。
        /// </summary>
        public SequenceNodeData(string name = null)
            : base(name)
        {
        }
    }

    /// <summary>
    /// Selector 节点数据。
    /// </summary>
    [Serializable]
    public sealed class SelectorNodeData : NodeData
    {
        /// <summary>
        /// 创建 Selector 节点数据。
        /// </summary>
        public SelectorNodeData(string name = null)
            : base(name)
        {
        }
    }

    /// <summary>
    /// Parallel 节点数据。
    /// </summary>
    [Serializable]
    public sealed class ParallelNodeData : NodeData
    {
        /// <summary>
        /// 创建 Parallel 节点数据。
        /// </summary>
        public ParallelNodeData(
            ParallelSuccessPolicy successPolicy = ParallelSuccessPolicy.All,
            ParallelFailurePolicy failurePolicy = ParallelFailurePolicy.Any,
            string name = null)
            : base(name)
        {
            SuccessPolicy = successPolicy;
            FailurePolicy = failurePolicy;
        }

        /// <summary>Parallel 的成功聚合策略。</summary>
        public ParallelSuccessPolicy SuccessPolicy = ParallelSuccessPolicy.All;

        /// <summary>Parallel 的失败聚合策略。</summary>
        public ParallelFailurePolicy FailurePolicy = ParallelFailurePolicy.Any;
    }

    /// <summary>
    /// 自定义 Action 节点数据。
    /// </summary>
    [Serializable]
    public sealed class ActionNodeData : NodeData
    {
        /// <summary>
        /// 创建 Action 节点数据。
        /// </summary>
        public ActionNodeData(string logicId, string name = null)
            : base(name)
        {
            LogicId = logicId;
        }

        /// <summary>用于绑定 Action 运行时逻辑的稳定标识。</summary>
        public string LogicId;

        /// <summary>传递给 Action 工厂的可序列化参数。</summary>
        public Dictionary<string, object> Parameters = new Dictionary<string, object>();
    }

    /// <summary>
    /// 自定义 Conditional 节点数据。
    /// </summary>
    [Serializable]
    public sealed class ConditionalNodeData : NodeData
    {
        /// <summary>
        /// 创建 Conditional 节点数据。
        /// </summary>
        public ConditionalNodeData(string logicId, string name = null)
            : base(name)
        {
            LogicId = logicId;
        }

        /// <summary>用于绑定 Conditional 运行时逻辑的稳定标识。</summary>
        public string LogicId;

        /// <summary>传递给 Conditional 工厂的可序列化参数。</summary>
        public Dictionary<string, object> Parameters = new Dictionary<string, object>();
    }
}
