namespace BehaviorTree
{
    /// <summary>
    /// 行为节点的运行时状态。
    /// </summary>
    public enum NodeStatus
    {
        /// <summary>节点尚未开始执行。</summary>
        Idle,

        /// <summary>节点仍在执行。</summary>
        Running,

        /// <summary>节点执行成功。</summary>
        Success,

        /// <summary>节点执行失败。</summary>
        Failure
    }

    /// <summary>
    /// Action 节点一次更新允许返回的结果。
    /// </summary>
    public enum ActionResult
    {
        /// <summary>Action 仍需继续执行。</summary>
        Running,

        /// <summary>Action 已成功完成。</summary>
        Success,

        /// <summary>Action 已失败。</summary>
        Failure
    }

    /// <summary>
    /// Parallel 节点判定成功所使用的聚合策略。
    /// </summary>
    public enum ParallelSuccessPolicy
    {
        /// <summary>所有子节点成功时 Parallel 才成功。</summary>
        All,

        /// <summary>任一子节点成功时 Parallel 即成功。</summary>
        Any
    }

    /// <summary>
    /// Parallel 节点判定失败所使用的聚合策略。
    /// </summary>
    public enum ParallelFailurePolicy
    {
        /// <summary>任一子节点失败时 Parallel 即失败。</summary>
        Any,

        /// <summary>所有子节点失败时 Parallel 才失败。</summary>
        All
    }
}
