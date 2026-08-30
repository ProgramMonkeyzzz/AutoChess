using System;

namespace BehaviorTree
{
    /// <summary>
    /// 所有行为树运行时节点的公共基类。
    /// </summary>
    public abstract class BehaviorNode
    {
        /// <summary>节点当前运行时状态。</summary>
        private NodeStatus _status = NodeStatus.Idle;

        /// <summary>
        /// 获取节点当前状态。
        /// </summary>
        public NodeStatus Status => _status;

        /// <summary>
        /// 推进一次节点生命周期。
        /// </summary>
        public NodeStatus Tick(BehaviorTreeContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_status == NodeStatus.Success || _status == NodeStatus.Failure)
            {
                return _status;
            }

            if (_status == NodeStatus.Idle)
            {
                _status = NodeStatus.Running;
                OnStart(context);
            }

            NodeStatus nextStatus = Execute(context);
            if (nextStatus == NodeStatus.Idle)
            {
                throw new InvalidOperationException(
                    $"节点 {GetType().FullName} 的 Execute 不能返回 Idle。 ");
            }

            _status = nextStatus;
            if (nextStatus == NodeStatus.Success || nextStatus == NodeStatus.Failure)
            {
                OnStop(context);
            }

            return _status;
        }

        /// <summary>
        /// 中止当前运行中的节点，并递归中止运行中的子节点。
        /// </summary>
        public void Abort(BehaviorTreeContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_status != NodeStatus.Running)
            {
                return;
            }

            try
            {
                OnAbort(context);
            }
            finally
            {
                _status = NodeStatus.Idle;
            }
        }

        /// <summary>
        /// 清理节点状态，使节点可以在下一次 Start 后重新执行。
        /// </summary>
        public void Reset()
        {
            _status = NodeStatus.Idle;
            OnReset();
        }

        /// <summary>
        /// 节点首次开始执行时调用。
        /// </summary>
        protected virtual void OnStart(BehaviorTreeContext context)
        {
        }

        /// <summary>
        /// 节点每次 Tick 的实际执行逻辑。
        /// </summary>
        protected abstract NodeStatus Execute(BehaviorTreeContext context);

        /// <summary>
        /// 节点完成或被中止时调用。
        /// </summary>
        protected virtual void OnStop(BehaviorTreeContext context)
        {
        }

        /// <summary>
        /// 节点被外部中止时调用；默认复用停止生命周期。
        /// </summary>
        protected virtual void OnAbort(BehaviorTreeContext context)
        {
            OnStop(context);
        }

        /// <summary>
        /// 节点被 Reset 时调用。
        /// </summary>
        protected virtual void OnReset()
        {
        }
    }
}
