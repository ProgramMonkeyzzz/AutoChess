namespace BehaviorTree
{
    /// <summary>
    /// 按顺序尝试子节点的 Selector 节点。
    /// </summary>
    public sealed class SelectorNode : CompositeNode
    {
        /// <summary>当前正在尝试的子节点索引。</summary>
        private int _currentIndex;

        /// <summary>
        /// 创建 Selector 节点。
        /// </summary>
        public SelectorNode(System.Collections.Generic.IEnumerable<BehaviorNode> children = null)
            : base(children)
        {
        }

        /// <summary>
        /// 依次尝试子节点，直到出现 Running 或 Success。
        /// </summary>
        protected override NodeStatus Execute(BehaviorTreeContext context)
        {
            while (_currentIndex < Children.Count)
            {
                NodeStatus childStatus = Children[_currentIndex].Tick(context);
                if (childStatus == NodeStatus.Running)
                {
                    return NodeStatus.Running;
                }

                if (childStatus == NodeStatus.Success)
                {
                    return NodeStatus.Success;
                }

                _currentIndex++;
            }

            return NodeStatus.Failure;
        }

        /// <summary>
        /// 重置当前尝试索引。
        /// </summary>
        protected override void OnReset()
        {
            _currentIndex = 0;
            base.OnReset();
        }
    }
}
