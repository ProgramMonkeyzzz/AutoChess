using System.Collections.Generic;

namespace BehaviorTree
{
    /// <summary>
    /// 并行推进全部子节点并按策略聚合结果的 Parallel 节点。
    /// </summary>
    public sealed class ParallelNode : CompositeNode
    {
        /// <summary>
        /// 创建 Parallel 节点。
        /// </summary>
        public ParallelNode(
            ParallelSuccessPolicy successPolicy = ParallelSuccessPolicy.All,
            ParallelFailurePolicy failurePolicy = ParallelFailurePolicy.Any,
            IEnumerable<BehaviorNode> children = null)
            : base(children)
        {
            SuccessPolicy = successPolicy;
            FailurePolicy = failurePolicy;
        }

        /// <summary>
        /// 获取成功聚合策略。
        /// </summary>
        public ParallelSuccessPolicy SuccessPolicy { get; }

        /// <summary>
        /// 获取失败聚合策略。
        /// </summary>
        public ParallelFailurePolicy FailurePolicy { get; }

        /// <summary>
        /// Tick 所有未完成子节点并计算 Parallel 结果。
        /// </summary>
        protected override NodeStatus Execute(BehaviorTreeContext context)
        {
            if (Children.Count == 0)
            {
                return NodeStatus.Success;
            }

            int successCount = 0;
            int failureCount = 0;
            foreach (BehaviorNode child in Children)
            {
                NodeStatus childStatus = child.Status;
                if (childStatus == NodeStatus.Idle || childStatus == NodeStatus.Running)
                {
                    childStatus = child.Tick(context);
                }

                if (childStatus == NodeStatus.Success)
                {
                    successCount++;
                }
                else if (childStatus == NodeStatus.Failure)
                {
                    failureCount++;
                }
            }

            bool failureReached = FailurePolicy == ParallelFailurePolicy.Any
                ? failureCount > 0
                : failureCount == Children.Count;
            if (failureReached)
            {
                AbortRunningChildren(context);
                return NodeStatus.Failure;
            }

            bool successReached = SuccessPolicy == ParallelSuccessPolicy.Any
                ? successCount > 0
                : successCount == Children.Count;
            if (successReached)
            {
                AbortRunningChildren(context);
                return NodeStatus.Success;
            }

            if (successCount + failureCount == Children.Count)
            {
                return NodeStatus.Failure;
            }

            return NodeStatus.Running;
        }

        /// <summary>
        /// 中止所有尚未完成的子节点。
        /// </summary>
        private void AbortRunningChildren(BehaviorTreeContext context)
        {
            foreach (BehaviorNode child in Children)
            {
                if (child.Status == NodeStatus.Running)
                {
                    child.Abort(context);
                }
            }
        }
    }
}
