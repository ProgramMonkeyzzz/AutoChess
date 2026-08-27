#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Chess;
using Chess.FSM;
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
    public void StartAndChangeStateValidateLifecycle()
    {
        var fsm = CreateFsm(out _, out _, out _);

        Assert.Throws<InvalidOperationException>(() => fsm.Start<UnregisteredState>());
        fsm.Start<IdleState>();
        Assert.Throws<InvalidOperationException>(() => fsm.Start<IdleState>());
        Assert.That(fsm.ChangeState<IdleState>(), Is.False);
        Assert.That(fsm.ChangeState<AttackState>(), Is.True);
    }

    [Test]
    public void TickBeforeStartDoesNothing()
    {
        var fsm = CreateFsm(out List<string> events, out _, out _);

        fsm.Tick();

        Assert.That(events, Is.Empty);
        Assert.That(fsm.IsStarted, Is.False);
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
        private readonly List<string> _events;
        private readonly string _name;

        protected RecordingState(FSM fsm, List<string> events, string name) : base(fsm)
        {
            _events = events;
            _name = name;
        }

        public int EnterCount { get; private set; }

        public override void OnStateEnter()
        {
            EnterCount++;
            _events.Add(_name + ".enter");
        }

        public override void OnStateExit()
        {
            _events.Add(_name + ".exit");
        }

        public override void OnStateStay()
        {
            _events.Add(_name + ".stay");
        }
    }

    private sealed class IdleState : RecordingState
    {
        public IdleState(FSM fsm, List<string> events) : base(fsm, events, "idle")
        {
        }
    }

    private sealed class AttackState : RecordingState
    {
        public AttackState(FSM fsm, List<string> events) : base(fsm, events, "attack")
        {
        }
    }

    private sealed class DeadState : RecordingState
    {
        public DeadState(FSM fsm, List<string> events) : base(fsm, events, "dead")
        {
        }
    }

    private sealed class UnregisteredState : RecordingState
    {
        public UnregisteredState(FSM fsm, List<string> events) : base(fsm, events, "unregistered")
        {
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
