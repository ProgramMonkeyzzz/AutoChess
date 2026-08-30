using System;
using System.Threading;

namespace BehaviorTree
{
    /// <summary>
    /// 节点执行期间可访问的共享上下文。
    /// </summary>
    public sealed class BehaviorTreeContext
    {
        /// <summary>
        /// 创建行为树上下文。
        /// </summary>
        public BehaviorTreeContext(
            Blackboard blackboard,
            object owner = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
            Owner = owner;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// 当前行为树使用的黑板。
        /// </summary>
        public Blackboard Blackboard { get; }

        /// <summary>
        /// 行为树所属的外部对象，例如棋子控制器。
        /// </summary>
        public object Owner { get; }

        /// <summary>
        /// 当前运行器用于通知异步节点取消的令牌。
        /// </summary>
        public CancellationToken CancellationToken { get; internal set; }

        /// <summary>
        /// 当前 Tick 的时间步长。
        /// </summary>
        public float DeltaTime { get; set; }
    }
}
