namespace BehaviorTree
{
    /// <summary>
    /// 按顺序执行子节点的 Sequence 节点。
    /// </summary>
    public sealed class SequenceNode : CompositeNode
    {
        /// <summary>当前等待完成的子节点索引。</summary>
        private int _currentIndex;

        /// <summary>
        /// 创建 Sequence 节点。
        /// </summary>
        public SequenceNode(System.Collections.Generic.IEnumerable<BehaviorNode> children = null)
            : base(children)
        {
        }

        /// <summary>
        /// 依次执行子节点，直到出现 Running 或 Failure。
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

                if (childStatus == NodeStatus.Failure)
                {
                    return NodeStatus.Failure;
                }

                _currentIndex++;
            }

            return NodeStatus.Success;
        }

        /// <summary>
        /// 重置当前执行索引。
        /// </summary>
        protected override void OnReset()
        {
            _currentIndex = 0;
            base.OnReset();
        }
    }
}
