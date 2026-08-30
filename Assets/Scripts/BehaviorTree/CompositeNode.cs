using System;
using System.Collections.Generic;

namespace BehaviorTree
{
    /// <summary>
    /// 维护子节点集合的行为树复合节点基类。
    /// </summary>
    public abstract class CompositeNode : BehaviorNode
    {
        private readonly List<BehaviorNode> _children = new List<BehaviorNode>();

        /// <summary>
        /// 创建一个可选初始子节点集合的复合节点。
        /// </summary>
        protected CompositeNode(IEnumerable<BehaviorNode> children = null)
        {
            if (children == null)
            {
                return;
            }

            foreach (BehaviorNode child in children)
            {
                AddChild(child);
            }
        }

        /// <summary>
        /// 获取当前复合节点的只读子节点集合。
        /// </summary>
        public IReadOnlyList<BehaviorNode> Children => _children;

        /// <summary>
        /// 添加一个非空子节点。
        /// </summary>
        public virtual void AddChild(BehaviorNode child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            _children.Add(child);
        }

        /// <summary>
        /// 中止所有仍在运行的子节点。
        /// </summary>
        protected override void OnAbort(BehaviorTreeContext context)
        {
            foreach (BehaviorNode child in _children)
            {
                child.Abort(context);
            }

            base.OnAbort(context);
        }

        /// <summary>
        /// 递归重置所有子节点。
        /// </summary>
        protected override void OnReset()
        {
            foreach (BehaviorNode child in _children)
            {
                child.Reset();
            }

            base.OnReset();
        }
    }
}
