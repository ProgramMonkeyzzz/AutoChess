#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using BehaviorTree;
using Chess;
using Chess.FSM;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

public class FSMTests
{
    [Test]
    public void FsmAndObjectsUseTheSameChessControllerContext()
    {
        var chessController = new ChessController();
        var fsm = new FSM(chessController);
        var events = new List<string>();
        var idle = new IdleState(fsm, events);
        var attack = new AttackState(fsm, events);
        var transition = new ConditionalTransition<IdleState, AttackState>(fsm, () => true);

        Assert.That(fsm.ChessController, Is.SameAs(chessController));
        Assert.That(idle.Fsm, Is.SameAs(fsm));
        Assert.That(transition.Fsm, Is.SameAs(fsm));

        fsm.AddState(idle);
        fsm.AddState(attack);
        fsm.AddTransition(transition);
        Assert.That(transition.CanReadController(chessController), Is.True);
    }

    [Test]
    public void TickTransitionsBeforeCallingStayOnNewState()
    {
        var fsm = CreateFsm(out List<string> events, out IdleState idle, out AttackState attack);
        bool canAttack = false;
        fsm.AddTransition(new ConditionalTransition<IdleState, AttackState>(fsm, () => canAttack));

        fsm.Start<IdleState>();
        fsm.Tick();

        Assert.That(events, Is.EqualTo(new[] { "idle.enter", "idle.stay" }));
        Assert.That(fsm.CurrentState, Is.SameAs(idle));

        canAttack = true;
        fsm.Tick();

        Assert.That(events, Is.EqualTo(new[]
        {
            "idle.enter", "idle.stay", "idle.exit", "attack.enter", "attack.stay"
        }));
        Assert.That(fsm.CurrentState, Is.SameAs(attack));
    }

    [Test]
    public void FalseTransitionKeepsCurrentState()
    {
        var fsm = CreateFsm(out List<string> events, out IdleState idle, out AttackState attack);
        fsm.AddTransition(new ConditionalTransition<IdleState, AttackState>(fsm, () => false));

        fsm.Start<IdleState>();
        fsm.Tick();

        Assert.That(fsm.CurrentState, Is.SameAs(idle));
        Assert.That(attack.EnterCount, Is.EqualTo(0));
        Assert.That(events, Is.EqualTo(new[] { "idle.enter", "idle.stay" }));
    }

    [Test]
    public void FirstSatisfiedTransitionWinsRegistrationOrder()
    {
        var fsm = new FSM(new ChessController());
        var events = new List<string>();
        var idle = new IdleState(fsm, events);
        var attack = new AttackState(fsm, events);
        var dead = new DeadState(fsm, events);
        fsm.AddState(idle);
        fsm.AddState(attack);
        fsm.AddState(dead);
        fsm.AddTransition(new ConditionalTransition<IdleState, AttackState>(fsm, () => true));
        fsm.AddTransition(new ConditionalTransition<IdleState, DeadState>(fsm, () => true));

        fsm.Start<IdleState>();
        fsm.Tick();

        Assert.That(fsm.CurrentState, Is.SameAs(attack));
        Assert.That(dead.EnterCount, Is.EqualTo(0));
    }

    [Test]
    public void AddStateRejectsDuplicateAndCrossFsmStates()
    {
        var fsm = new FSM(new ChessController());
        var otherFsm = new FSM(new ChessController());
        var events = new List<string>();
        var state = new IdleState(fsm, events);

        fsm.AddState(state);
        Assert.Throws<ArgumentException>(() => fsm.AddState(new IdleState(fsm, events)));
        Assert.Throws<InvalidOperationException>(() => otherFsm.AddState(state));
    }

    [Test]
    public void AddTransitionRejectsMissingStatesAndDuplicateInstances()
    {
        var fsm = new FSM(new ChessController());
        var events = new List<string>();
        var idle = new IdleState(fsm, events);
        var attack = new AttackState(fsm, events);
        var transition = new ConditionalTransition<IdleState, AttackState>(fsm, () => true);
        fsm.AddState(idle);

        Assert.Throws<InvalidOperationException>(() => fsm.AddTransition(transition));

        fsm.AddState(attack);
        fsm.AddTransition(transition);
        Assert.Throws<ArgumentException>(() => fsm.AddTransition(transition));
    }

    [Test]
    public void StartAndCheckCurrentStateTransitionValidateLifecycle()
    {
        var fsm = CreateFsm(out _, out IdleState idle, out AttackState attack);

        Assert.That(fsm.CheckCurrentStateTransition(), Is.Null);
        Assert.Throws<InvalidOperationException>(() => fsm.Start<UnregisteredState>());
        fsm.Start<IdleState>();
        Assert.Throws<InvalidOperationException>(() => fsm.Start<IdleState>());
        Assert.That(fsm.CheckCurrentStateTransition(), Is.SameAs(idle));
        Assert.That(fsm.CurrentState, Is.SameAs(idle));
        Assert.That(attack.EnterCount, Is.EqualTo(0));

        fsm.AddTransition(new ConditionalTransition<IdleState, AttackState>(fsm, () => true));
        Assert.That(fsm.CheckCurrentStateTransition(), Is.SameAs(attack));
        Assert.That(fsm.CurrentState, Is.SameAs(attack));
        Assert.That(fsm.CheckCurrentStateTransition(), Is.SameAs(attack));
        Assert.That(attack.EnterCount, Is.EqualTo(1));
    }

    /// <summary>
    /// 验证目标已是当前状态时检查转换不会重入生命周期。
    /// </summary>
    [Test]
    public void CheckCurrentStateTransitionSkipsReentryWhenTargetIsCurrent()
    {
        var fsm = CreateFsm(out List<string> events, out IdleState idle, out _);
        fsm.AddTransition(new ConditionalTransition<IdleState, IdleState>(fsm, () => true));
        fsm.Start<IdleState>();
        events.Clear();

        Assert.That(fsm.CheckCurrentStateTransition(), Is.SameAs(idle));
        Assert.That(events, Is.Empty);
        Assert.That(idle.EnterCount, Is.EqualTo(1));
    }

    /// <summary>
    /// 验证 CheckCurrentStateTransition 按注册顺序选择第一个可切换目标，且不执行 Stay。
    /// </summary>
    [Test]
    public void CheckCurrentStateTransitionSelectsFirstSatisfiedTargetWithoutStay()
    {
        var fsm = new FSM(new ChessController());
        var events = new List<string>();
        var idle = new IdleState(fsm, events);
        var attack = new AttackState(fsm, events);
        var dead = new DeadState(fsm, events);
        fsm.AddState(idle);
        fsm.AddState(attack);
        fsm.AddState(dead);
        fsm.AddTransition(new ConditionalTransition<IdleState, AttackState>(fsm, () => true));
        fsm.AddTransition(new ConditionalTransition<IdleState, DeadState>(fsm, () => true));
        fsm.Start<IdleState>();

        Assert.That(fsm.CheckCurrentStateTransition(), Is.SameAs(attack));
        Assert.That(dead.EnterCount, Is.EqualTo(0));
        Assert.That(events, Is.EqualTo(new[] { "idle.enter", "idle.exit", "attack.enter" }));
    }

    [Test]
    public void TickBeforeStartDoesNothing()
    {
        var fsm = CreateFsm(out List<string> events, out _, out _);

        fsm.Tick();

        Assert.That(events, Is.Empty);
        Assert.That(fsm.IsStarted, Is.False);
    }

    /// <summary>
    /// 验证三个行为树按状态自身逻辑之后或退出清理之前执行，并接收正确时间步。
    /// </summary>
    [Test]
    public void BehaviorTreesRunInLifecycleOrderAndStayReceivesDeltaTime()
    {
        var fsm = new FSM(new ChessController());
        var events = new List<string>();
        var enterAction = new RecordingAction(events, "tree.enter", ActionResult.Success);
        var stayAction = new RecordingAction(events, "tree.stay", ActionResult.Running);
        var exitAction = new RecordingAction(events, "tree.exit", ActionResult.Success);
        BehaviorTreeRunner enterTree = CreateRunner(enterAction);
        BehaviorTreeRunner stayTree = CreateRunner(stayAction);
        BehaviorTreeRunner exitTree = CreateRunner(exitAction);
        var idle = new IdleState(fsm, events, enterTree, stayTree, exitTree);
        var attack = new AttackState(fsm, events);
        fsm.AddState(idle);
        fsm.AddState(attack);
        bool canAttack = false;
        fsm.AddTransition(new ConditionalTransition<IdleState, AttackState>(fsm, () => canAttack));

        fsm.Start<IdleState>();
        fsm.Tick(0.25f);
        fsm.Tick(0.5f);
        canAttack = true;
        fsm.CheckCurrentStateTransition();

        Assert.That(events, Is.EqualTo(new[]
        {
            "idle.enter", "tree.enter",
            "idle.stay", "tree.stay",
            "idle.stay", "tree.stay",
            "tree.exit", "idle.exit", "attack.enter"
        }));
        Assert.That(idle.EnterBehaviorTree, Is.SameAs(enterTree));
        Assert.That(idle.StayBehaviorTree, Is.SameAs(stayTree));
        Assert.That(idle.ExitBehaviorTree, Is.SameAs(exitTree));
        Assert.That(enterAction.DeltaTimes, Is.EqualTo(new[] { 0f }));
        Assert.That(stayAction.DeltaTimes, Is.EqualTo(new[] { 0.25f, 0.5f }));
        Assert.That(exitAction.DeltaTimes, Is.EqualTo(new[] { 0f }));
    }

    /// <summary>
    /// 验证停留行为树到达终态后保持结果，并在状态重入时重置后重新执行。
    /// </summary>
    [Test]
    public void StayBehaviorTreeHoldsTerminalStatusAndRerunsAfterStateReentry()
    {
        var fsm = new FSM(new ChessController());
        var events = new List<string>();
        var stayAction = new RecordingAction(events, "tree.stay", ActionResult.Success);
        BehaviorTreeRunner stayTree = CreateRunner(stayAction);
        var idle = new IdleState(fsm, events, stayBehaviorTree: stayTree);
        var attack = new AttackState(fsm, events);
        fsm.AddState(idle);
        fsm.AddState(attack);
        bool toAttack = false;
        bool toIdle = false;
        fsm.AddTransition(new ConditionalTransition<IdleState, AttackState>(fsm, () => toAttack));
        fsm.AddTransition(new ConditionalTransition<AttackState, IdleState>(fsm, () => toIdle));

        fsm.Start<IdleState>();
        fsm.Tick(0.1f);
        fsm.Tick(0.2f);

        Assert.That(stayAction.UpdateCount, Is.EqualTo(1));
        Assert.That(stayTree.Status, Is.EqualTo(NodeStatus.Success));

        toAttack = true;
        fsm.CheckCurrentStateTransition();
        toAttack = false;
        toIdle = true;
        fsm.CheckCurrentStateTransition();
        fsm.Tick(0.3f);

        Assert.That(stayAction.UpdateCount, Is.EqualTo(2));
        Assert.That(stayAction.DeltaTimes, Is.EqualTo(new[] { 0.1f, 0.3f }));
    }

    /// <summary>
    /// 验证离开状态时停止三棵 Running 行为树并取消其运行令牌。
    /// </summary>
    [Test]
    public void StateExitCancelsRunningEnterStayAndExitBehaviorTrees()
    {
        var fsm = new FSM(new ChessController());
        var events = new List<string>();
        var enterAction = new RunningAsyncAction();
        var stayAction = new RunningAsyncAction();
        var exitAction = new RunningAsyncAction();
        BehaviorTreeRunner enterTree = CreateRunner(enterAction);
        BehaviorTreeRunner stayTree = CreateRunner(stayAction);
        BehaviorTreeRunner exitTree = CreateRunner(exitAction);
        var idle = new IdleState(fsm, events, enterTree, stayTree, exitTree);
        var attack = new AttackState(fsm, events);
        fsm.AddState(idle);
        fsm.AddState(attack);
        bool canAttack = false;
        fsm.AddTransition(new ConditionalTransition<IdleState, AttackState>(fsm, () => canAttack));

        fsm.Start<IdleState>();
        fsm.Tick(0.1f);
        fsm.Tick(0.2f);
        canAttack = true;
        fsm.CheckCurrentStateTransition();

        Assert.That(enterAction.InvocationCount, Is.EqualTo(1));
        Assert.That(stayAction.InvocationCount, Is.EqualTo(1));
        Assert.That(exitAction.InvocationCount, Is.EqualTo(1));
        Assert.That(enterAction.ReceivedToken.IsCancellationRequested, Is.True);
        Assert.That(stayAction.ReceivedToken.IsCancellationRequested, Is.True);
        Assert.That(exitAction.ReceivedToken.IsCancellationRequested, Is.True);
        Assert.That(enterAction.StopCount, Is.EqualTo(1));
        Assert.That(stayAction.StopCount, Is.EqualTo(1));
        Assert.That(exitAction.StopCount, Is.EqualTo(1));
        Assert.That(enterTree.IsStarted, Is.False);
        Assert.That(stayTree.IsStarted, Is.False);
        Assert.That(exitTree.IsStarted, Is.False);
    }

    /// <summary>
    /// 验证转换帧依次执行旧状态退出、新状态进入和新状态停留行为树。
    /// </summary>
    [Test]
    public void TransitionFrameRunsExitEnterAndStayTreesInOrder()
    {
        var fsm = new FSM(new ChessController());
        var events = new List<string>();
        var idle = new IdleState(
            fsm,
            events,
            exitBehaviorTree: CreateRunner(
                new RecordingAction(events, "idle.tree.exit", ActionResult.Success)));
        var attackStayAction = new RecordingAction(
            events,
            "attack.tree.stay",
            ActionResult.Running);
        var attack = new AttackState(
            fsm,
            events,
            CreateRunner(new RecordingAction(events, "attack.tree.enter", ActionResult.Success)),
            CreateRunner(attackStayAction));
        fsm.AddState(idle);
        fsm.AddState(attack);
        fsm.AddTransition(new ConditionalTransition<IdleState, AttackState>(fsm, () => true));
        fsm.Start<IdleState>();
        events.Clear();

        fsm.Tick(0.4f);

        Assert.That(events, Is.EqualTo(new[]
        {
            "idle.tree.exit", "idle.exit",
            "attack.enter", "attack.tree.enter",
            "attack.stay", "attack.tree.stay"
        }));
        Assert.That(attackStayAction.DeltaTimes, Is.EqualTo(new[] { 0.4f }));
    }

    /// <summary>
    /// 使用指定 Action 创建测试专用行为树运行器。
    /// </summary>
    private static BehaviorTreeRunner CreateRunner(ActionNode action)
    {
        var registry = new BehaviorTreeRegistry();
        registry.RegisterAction("TestAction", data => action);
        var root = new RootNodeData();
        root.AddChild(new ActionNodeData("TestAction"));
        return new BehaviorTreeRunner(
            new BehaviorTreeDefinition(root),
            registry,
            new Blackboard());
    }

    private static FSM CreateFsm(
        out List<string> events,
        out IdleState idle,
        out AttackState attack)
    {
        var fsm = new FSM(new ChessController());
        events = new List<string>();
        idle = new IdleState(fsm, events);
        attack = new AttackState(fsm, events);
        fsm.AddState(idle);
        fsm.AddState(attack);
        fsm.AddState(new DeadState(fsm, events));
        return fsm;
    }

    private abstract class RecordingState : StateBase
    {
        /// <summary>记录状态生命周期的事件集合。</summary>
        private readonly List<string> _events;

        /// <summary>写入事件集合的状态名称。</summary>
        private readonly string _name;

        /// <summary>
        /// 创建带有可选生命周期行为树的测试状态。
        /// </summary>
        protected RecordingState(
            FSM fsm,
            List<string> events,
            string name,
            BehaviorTreeRunner enterBehaviorTree = null,
            BehaviorTreeRunner stayBehaviorTree = null,
            BehaviorTreeRunner exitBehaviorTree = null)
            : base(fsm, enterBehaviorTree, stayBehaviorTree, exitBehaviorTree)
        {
            _events = events;
            _name = name;
        }

        /// <summary>获取状态进入次数。</summary>
        public int EnterCount { get; private set; }

        /// <summary>记录状态进入事件。</summary>
        protected override void OnStateEnterCore()
        {
            EnterCount++;
            _events.Add(_name + ".enter");
        }

        /// <summary>记录状态退出事件。</summary>
        protected override void OnStateExitCore()
        {
            _events.Add(_name + ".exit");
        }

        /// <summary>记录状态停留事件。</summary>
        protected override void OnStateStayCore(float deltaTime)
        {
            _events.Add(_name + ".stay");
        }
    }

    private sealed class IdleState : RecordingState
    {
        /// <summary>创建测试用空闲状态。</summary>
        public IdleState(
            FSM fsm,
            List<string> events,
            BehaviorTreeRunner enterBehaviorTree = null,
            BehaviorTreeRunner stayBehaviorTree = null,
            BehaviorTreeRunner exitBehaviorTree = null)
            : base(
                fsm,
                events,
                "idle",
                enterBehaviorTree,
                stayBehaviorTree,
                exitBehaviorTree)
        {
        }
    }

    private sealed class AttackState : RecordingState
    {
        /// <summary>创建测试用攻击状态。</summary>
        public AttackState(
            FSM fsm,
            List<string> events,
            BehaviorTreeRunner enterBehaviorTree = null,
            BehaviorTreeRunner stayBehaviorTree = null,
            BehaviorTreeRunner exitBehaviorTree = null)
            : base(
                fsm,
                events,
                "attack",
                enterBehaviorTree,
                stayBehaviorTree,
                exitBehaviorTree)
        {
        }
    }

    private sealed class DeadState : RecordingState
    {
        /// <summary>创建测试用死亡状态。</summary>
        public DeadState(FSM fsm, List<string> events) : base(fsm, events, "dead")
        {
        }
    }

    private sealed class UnregisteredState : RecordingState
    {
        /// <summary>创建测试用未注册状态。</summary>
        public UnregisteredState(FSM fsm, List<string> events) : base(fsm, events, "unregistered")
        {
        }
    }

    /// <summary>
    /// 记录行为树执行顺序和时间步的测试 Action。
    /// </summary>
    private sealed class RecordingAction : ActionNode
    {
        /// <summary>记录执行顺序的事件集合。</summary>
        private readonly List<string> _events;

        /// <summary>每次执行时写入的事件名称。</summary>
        private readonly string _eventName;

        /// <summary>每次执行后返回的固定结果。</summary>
        private readonly ActionResult _result;

        /// <summary>
        /// 创建返回固定结果的记录 Action。
        /// </summary>
        public RecordingAction(
            List<string> events,
            string eventName,
            ActionResult result)
        {
            _events = events;
            _eventName = eventName;
            _result = result;
        }

        /// <summary>获取 Action 实际执行次数。</summary>
        public int UpdateCount { get; private set; }

        /// <summary>获取每次执行收到的时间步。</summary>
        public List<float> DeltaTimes { get; } = new List<float>();

        /// <summary>记录本次行为树更新并返回预设结果。</summary>
        protected override ActionResult OnUpdate(BehaviorTreeContext context)
        {
            UpdateCount++;
            DeltaTimes.Add(context.DeltaTime);
            _events.Add(_eventName);
            return _result;
        }
    }

    /// <summary>
    /// 始终保持 Running 并记录异步取消情况的测试 Action。
    /// </summary>
    private sealed class RunningAsyncAction : AsyncActionNode
    {
        /// <summary>控制异步 Action 完成状态的 UniTask 源。</summary>
        private readonly UniTaskCompletionSource<ActionResult> _source =
            new UniTaskCompletionSource<ActionResult>();

        /// <summary>获取异步逻辑启动次数。</summary>
        public int InvocationCount { get; private set; }

        /// <summary>获取异步 Action 停止次数。</summary>
        public int StopCount { get; private set; }

        /// <summary>获取 Action 最近一次执行收到的取消令牌。</summary>
        public CancellationToken ReceivedToken { get; private set; }

        /// <summary>返回仅在取消时结束的 UniTask，使 Action 保持 Running。</summary>
        protected override UniTask<ActionResult> OnUpdateAsync(
            BehaviorTreeContext context,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            ReceivedToken = cancellationToken;
            cancellationToken.Register(() => _source.TrySetCanceled(cancellationToken));
            return _source.Task;
        }

        /// <summary>记录异步 Action 被行为树运行器停止。</summary>
        protected override void OnStop(BehaviorTreeContext context)
        {
            StopCount++;
        }
    }

    private sealed class ConditionalTransition<TState1, TState2> : StateTransition<TState1, TState2>
        where TState1 : StateBase
        where TState2 : StateBase
    {
        private readonly Func<bool> _condition;

        public ConditionalTransition(FSM fsm, Func<bool> condition) : base(fsm)
        {
            _condition = condition;
        }

        public override bool CanTransition()
        {
            return _condition();
        }

        public bool CanReadController(ChessController chessController)
        {
            return ReferenceEquals(_fsm.ChessController, chessController);
        }
    }
}
#endif
