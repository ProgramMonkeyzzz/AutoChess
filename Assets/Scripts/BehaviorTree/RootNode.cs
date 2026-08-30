using System;

namespace BehaviorTree
{
    /// <summary>
    /// 行为树根节点，最多包含一个子节点。
    /// </summary>
    public sealed class RootNode : CompositeNode
    {
        /// <summary>
        /// 创建空根节点。
        /// </summary>
        public RootNode()
        {
        }

        /// <summary>
        /// 创建包含指定子节点的根节点。
        /// </summary>
        public RootNode(BehaviorNode child)
        {
            if (child != null)
            {
                AddChild(child);
            }
        }

        /// <summary>
        /// 添加唯一的根子节点。
        /// </summary>
        public override void AddChild(BehaviorNode child)
        {
            if (Children.Count > 0)
            {
                throw new InvalidOperationException("RootNode 只能包含一个子节点。 ");
            }

            base.AddChild(child);
        }

        /// <summary>
        /// 执行根节点的唯一子节点。
        /// </summary>
        protected override NodeStatus Execute(BehaviorTreeContext context)
        {
            return Children.Count == 0 ? NodeStatus.Success : Children[0].Tick(context);
        }
    }
}
