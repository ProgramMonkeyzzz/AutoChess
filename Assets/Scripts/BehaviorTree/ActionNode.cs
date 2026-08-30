using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace BehaviorTree
{
    /// <summary>
    /// 执行主动逻辑的行为树 Action 节点基类。
    /// </summary>
    public abstract class ActionNode : BehaviorNode
    {
        /// <summary>
        /// 将 Action 的三态结果转换为行为节点状态。
        /// </summary>
        protected sealed override NodeStatus Execute(BehaviorTreeContext context)
        {
            ActionResult result = OnUpdate(context);
            switch (result)
            {
                case ActionResult.Running:
                    return NodeStatus.Running;
                case ActionResult.Success:
                    return NodeStatus.Success;
                case ActionResult.Failure:
                    return NodeStatus.Failure;
                default:
                    throw new InvalidOperationException(
                        $"Action 节点 {GetType().FullName} 返回了无效结果 {result}。 ");
            }
        }

        /// <summary>
        /// 执行一次同步 Action 更新；子类应返回 Running、Success 或 Failure。
        /// </summary>
        protected virtual ActionResult OnUpdate(BehaviorTreeContext context)
        {
            throw new NotImplementedException(
                $"Action 节点 {GetType().FullName} 必须重写 OnUpdate。 ");
        }
    }

    /// <summary>
    /// 使用 UniTask 实现异步逻辑的 Action 节点基类。
    /// </summary>
    public abstract class AsyncActionNode : ActionNode
    {
        /// <summary>当前等待完成的 UniTask。</summary>
        private UniTask<ActionResult> _pendingTask;

        /// <summary>当前是否已经创建待完成的 UniTask。</summary>
        private bool _hasPendingTask;

        /// <summary>用于单独取消当前异步 Action 的取消源。</summary>
        private CancellationTokenSource _actionCancellation;

        /// <summary>
        /// 轮询异步 Action；任务未完成时返回 Running。
        /// </summary>
        protected sealed override ActionResult OnUpdate(BehaviorTreeContext context)
        {
            if (!_hasPendingTask)
            {
                _actionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    context.CancellationToken);
                _pendingTask = OnUpdateAsync(context, _actionCancellation.Token);
                _hasPendingTask = true;
            }

            if (_pendingTask.Status == UniTaskStatus.Pending)
            {
                return ActionResult.Running;
            }

            try
            {
                return _pendingTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                return ActionResult.Failure;
            }
            finally
            {
                ClearPendingTask();
            }
        }

        /// <summary>
        /// 创建一次异步 Action；实现必须使用 UniTask 而不是 Task。
        /// </summary>
        protected abstract UniTask<ActionResult> OnUpdateAsync(
            BehaviorTreeContext context,
            CancellationToken cancellationToken);

        /// <summary>
        /// 中止异步 Action 并通知异步逻辑取消。
        /// </summary>
        protected override void OnAbort(BehaviorTreeContext context)
        {
            CancelPendingTask();
            base.OnAbort(context);
        }

        /// <summary>
        /// 清理异步 Action 的任务和取消源。
        /// </summary>
        protected override void OnReset()
        {
            CancelPendingTask();
            base.OnReset();
        }

        /// <summary>
        /// 请求当前异步任务取消。
        /// </summary>
        private void CancelPendingTask()
        {
            if (_actionCancellation != null)
            {
                _actionCancellation.Cancel();
            }

            if (_hasPendingTask)
            {
                _pendingTask.SuppressCancellationThrow().Forget();
            }

            ClearPendingTask();
        }

        /// <summary>
        /// 清空已完成或已取消的异步任务引用。
        /// </summary>
        private void ClearPendingTask()
        {
            _hasPendingTask = false;
            _pendingTask = default(UniTask<ActionResult>);

            if (_actionCancellation != null)
            {
                _actionCancellation.Dispose();
                _actionCancellation = null;
            }
        }
    }
}
