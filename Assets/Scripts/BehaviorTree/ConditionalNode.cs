namespace BehaviorTree
{
    /// <summary>
    /// 根据布尔条件返回 Success 或 Failure 的条件节点基类。
    /// </summary>
    public abstract class ConditionalNode : BehaviorNode
    {
        /// <summary>框架级取反参数的稳定 key。</summary>
        public const string InvertParameterKey = "Invert";

        /// <summary>为 true 时对 Evaluate 结果取反；缺省不取反。</summary>
        public bool Invert { get; set; }

        /// <summary>
        /// 执行条件判断并映射为节点状态。
        /// </summary>
        protected sealed override NodeStatus Execute(BehaviorTreeContext context)
        {
            bool result = Evaluate(context);
            if (Invert)
            {
                result = !result;
            }

            return result ? NodeStatus.Success : NodeStatus.Failure;
        }

        /// <summary>
        /// 计算当前条件是否成立。
        /// </summary>
        protected abstract bool Evaluate(BehaviorTreeContext context);
    }
}
