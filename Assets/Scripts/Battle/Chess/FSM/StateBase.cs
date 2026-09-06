using System;
using BehaviorTree;

namespace Chess.FSM
{
    /// <summary>
    /// 棋子状态基类，统一调度状态自身逻辑及进入、停留、退出行为树。
    /// </summary>
    public abstract class StateBase
    {
        /// <summary>该状态所属的状态机。</summary>
        protected readonly FSM _fsm;

        /// <summary>进入状态时执行一次的行为树运行器。</summary>
        private readonly BehaviorTreeRunner _enterBehaviorTree;

        /// <summary>停留在状态期间逐帧推进的行为树运行器。</summary>
        private readonly BehaviorTreeRunner _stayBehaviorTree;

        /// <summary>退出状态时执行一次的行为树运行器。</summary>
        private readonly BehaviorTreeRunner _exitBehaviorTree;

        /// <summary>
        /// 获取该状态所属的状态机。
        /// </summary>
        public FSM Fsm => _fsm;

        /// <summary>获取进入状态时执行的行为树运行器。</summary>
        public BehaviorTreeRunner EnterBehaviorTree => _enterBehaviorTree;

        /// <summary>获取停留在状态期间执行的行为树运行器。</summary>
        public BehaviorTreeRunner StayBehaviorTree => _stayBehaviorTree;

        /// <summary>获取退出状态时执行的行为树运行器。</summary>
        public BehaviorTreeRunner ExitBehaviorTree => _exitBehaviorTree;

        /// <summary>
        /// 创建绑定到指定状态机的状态，并保存各生命周期阶段的可选行为树。
        /// </summary>
        /// <param name="fsm">该状态所属的状态机。</param>
        /// <param name="enterBehaviorTree">进入状态时执行一次的行为树运行器。</param>
        /// <param name="stayBehaviorTree">停留在状态期间逐帧推进的行为树运行器；到达终态后每帧重新执行。</param>
        /// <param name="exitBehaviorTree">退出状态时执行一次的行为树运行器。</param>
        protected StateBase(
            FSM fsm,
            BehaviorTreeRunner enterBehaviorTree = null,
            BehaviorTreeRunner stayBehaviorTree = null,
            BehaviorTreeRunner exitBehaviorTree = null)
        {
            _fsm = fsm ?? throw new ArgumentNullException(nameof(fsm));
            _enterBehaviorTree = enterBehaviorTree;
            _stayBehaviorTree = stayBehaviorTree;
            _exitBehaviorTree = exitBehaviorTree;
        }

        /// <summary>
        /// 重置本状态的行为树，执行状态进入逻辑，并以零时间步推进一次进入行为树。
        /// </summary>
        public void OnStateEnter()
        {
            ResetBehaviorTrees();
            OnStateEnterCore();
            TickBehaviorTree(_enterBehaviorTree, 0f);
        }

        /// <summary>
        /// 以零时间步推进一次退出行为树，执行状态退出逻辑，并停止本状态的全部行为树。
        /// </summary>
        public void OnStateExit()
        {
            try
            {
                TickBehaviorTree(_exitBehaviorTree, 0f);
            }
            finally
            {
                try
                {
                    OnStateExitCore();
                }
                finally
                {
                    StopBehaviorTrees();
                }
            }
        }

        /// <summary>
        /// 使用零时间步执行一次状态停留逻辑。
        /// </summary>
        public void OnStateStay()
        {
            OnStateStay(0f);
        }

        /// <summary>
        /// 使用指定时间步执行状态停留逻辑，并推进停留行为树。
        /// </summary>
        /// <param name="deltaTime">本次状态更新使用的时间步长。</param>
        public void OnStateStay(float deltaTime)
        {
            OnStateStayCore(deltaTime);
            TickStayBehaviorTree(_stayBehaviorTree, deltaTime);
        }

        /// <summary>
        /// 执行派生状态自身的进入逻辑。
        /// </summary>
        protected virtual void OnStateEnterCore()
        {
        }

        /// <summary>
        /// 执行派生状态自身的停留逻辑。
        /// </summary>
        /// <param name="deltaTime">本次状态更新使用的时间步长。</param>
        protected virtual void OnStateStayCore(float deltaTime)
        {
        }

        /// <summary>
        /// 执行派生状态自身的退出逻辑。
        /// </summary>
        protected virtual void OnStateExitCore()
        {
        }

        /// <summary>
        /// 确保行为树已经启动，并使用指定时间步推进一次。
        /// </summary>
        private static void TickBehaviorTree(BehaviorTreeRunner behaviorTree, float deltaTime)
        {
            if (behaviorTree == null)
            {
                return;
            }

            if (!behaviorTree.IsStarted)
            {
                behaviorTree.Start();
            }

            behaviorTree.Tick(deltaTime);
        }

        /// <summary>
        /// 推进 Stay 行为树；已到达 Success/Failure 时先重置再重新执行，Running 则继续。
        /// </summary>
        private static void TickStayBehaviorTree(BehaviorTreeRunner behaviorTree, float deltaTime)
        {
            if (behaviorTree == null)
            {
                return;
            }

            if (behaviorTree.Status == NodeStatus.Success
                || behaviorTree.Status == NodeStatus.Failure)
            {
                behaviorTree.Reset();
            }

            TickBehaviorTree(behaviorTree, deltaTime);
        }

        /// <summary>
        /// 重置本状态保存的全部行为树，使状态重入时可以重新执行。
        /// </summary>
        private void ResetBehaviorTrees()
        {
            _enterBehaviorTree?.Reset();
            _stayBehaviorTree?.Reset();
            _exitBehaviorTree?.Reset();
        }

        /// <summary>
        /// 停止本状态保存的全部行为树，并取消仍在运行的异步节点。
        /// </summary>
        private void StopBehaviorTrees()
        {
            _enterBehaviorTree?.Stop();
            _stayBehaviorTree?.Stop();
            _exitBehaviorTree?.Stop();
        }
    }
}
