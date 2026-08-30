using System;
using System.Threading;

namespace BehaviorTree
{
    /// <summary>
    /// 管理行为树实例的启动、逐帧更新、停止和重置。
    /// </summary>
    public sealed class BehaviorTreeRunner
    {
        /// <summary>该运行器实际执行的根节点。</summary>
        private readonly RootNode _root;

        /// <summary>在每次 Tick 中传递给节点的共享上下文。</summary>
        private readonly BehaviorTreeContext _context;

        /// <summary>用于通知当前一轮异步 Action 停止的取消源。</summary>
        private CancellationTokenSource _runCancellation;

        /// <summary>运行器是否已经启动且尚未停止或重置。</summary>
        private bool _isStarted;

        /// <summary>
        /// 使用数据定义创建一个独立的行为树运行时实例。
        /// </summary>
        public BehaviorTreeRunner(
            BehaviorTreeDefinition definition,
            BehaviorTreeRegistry registry,
            Blackboard blackboard,
            object owner = null)
        {
            _root = BehaviorTreeNodeFactory.Create(definition, registry);
            _context = new BehaviorTreeContext(
                blackboard ?? throw new ArgumentNullException(nameof(blackboard)),
                owner);
        }

        /// <summary>
        /// 获取行为树根节点的当前状态。
        /// </summary>
        public NodeStatus Status => _root.Status;

        /// <summary>
        /// 获取运行器是否已经启动且尚未停止或重置。
        /// </summary>
        public bool IsStarted => _isStarted;

        /// <summary>
        /// 获取运行器正在使用的共享上下文。
        /// </summary>
        public BehaviorTreeContext Context => _context;

        /// <summary>
        /// 启动行为树；已启动时重复调用会抛出异常。
        /// </summary>
        public void Start()
        {
            if (_isStarted)
            {
                throw new InvalidOperationException("行为树运行器已经启动。 ");
            }

            DisposeRunCancellation();
            _runCancellation = new CancellationTokenSource();
            _context.CancellationToken = _runCancellation.Token;
            _isStarted = true;
        }

        /// <summary>
        /// 使用零时间步长推进一次行为树。
        /// </summary>
        public NodeStatus Tick()
        {
            return Tick(0f);
        }

        /// <summary>
        /// 使用指定时间步长推进一次行为树。
        /// </summary>
        public NodeStatus Tick(float deltaTime)
        {
            if (!_isStarted)
            {
                throw new InvalidOperationException("行为树运行器尚未启动。 ");
            }

            if (_root.Status == NodeStatus.Success || _root.Status == NodeStatus.Failure)
            {
                return _root.Status;
            }

            _context.DeltaTime = deltaTime;
            return _root.Tick(_context);
        }

        /// <summary>
        /// 中止正在运行的节点并停止运行器；已停止时调用不会产生效果。
        /// </summary>
        public void Stop()
        {
            if (!_isStarted)
            {
                return;
            }

            CancelRun();
            _root.Abort(_context);
            _isStarted = false;
            DisposeRunCancellation();
        }

        /// <summary>
        /// 中止当前执行并递归清理全部节点状态，之后需要重新 Start。
        /// </summary>
        public void Reset()
        {
            if (_isStarted)
            {
                CancelRun();
                _root.Abort(_context);
            }

            _root.Reset();
            _isStarted = false;
            _context.DeltaTime = 0f;
            DisposeRunCancellation();
        }

        /// <summary>
        /// 通知本轮运行中的异步节点取消。
        /// </summary>
        private void CancelRun()
        {
            if (_runCancellation != null && !_runCancellation.IsCancellationRequested)
            {
                _runCancellation.Cancel();
            }
        }

        /// <summary>
        /// 释放本轮运行使用的取消源。
        /// </summary>
        private void DisposeRunCancellation()
        {
            if (_runCancellation == null)
            {
                return;
            }

            _runCancellation.Dispose();
            _runCancellation = null;
            _context.CancellationToken = CancellationToken.None;
        }
    }
}
