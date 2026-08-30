namespace BehaviorTree
{
    /// <summary>
    /// 根据布尔条件返回 Success 或 Failure 的条件节点基类。
    /// </summary>
    public abstract class ConditionalNode : BehaviorNode
    {
        /// <summary>
        /// 执行条件判断并映射为节点状态。
        /// </summary>
        protected sealed override NodeStatus Execute(BehaviorTreeContext context)
        {
            return Evaluate(context) ? NodeStatus.Success : NodeStatus.Failure;
        }

        /// <summary>
        /// 计算当前条件是否成立。
        /// </summary>
        protected abstract bool Evaluate(BehaviorTreeContext context);
    }
}
