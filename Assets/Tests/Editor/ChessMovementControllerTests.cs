#if UNITY_EDITOR
using System.Collections.Generic;
using AutoChess;
using Chess;
using Chess.FSM;
using Chess.FSM.State;
using NUnit.Framework;
using Pathfinding;
using Pathfinding.Util;
using UnityEngine;
using UnityEngine.TestTools;

public class ChessMovementControllerTests
{
    private ChessBoard _board;
    private FakeAstarAI _astarAI;
    private ChessController _owner;
    private ChessMovementController _movementController;

    [SetUp]
    public void SetUp()
    {
        _board = new ChessBoard { Width = 20, Height = 20 };
        _astarAI = new FakeAstarAI { FakePosition = new Vector3(2f, 3f, 4f) };
        _owner = new ChessController();
        // 通过棋子入口绑定移动控制器，确保状态能够驱动同一个实例。
        _movementController = _owner.InitializeMovement(_board, _astarAI);
    }

    /// <summary>
    /// 验证所有棋子创建后都立即启动默认移动状态。
    /// </summary>
    [Test]
    public void NewChessStartsInMovementState()
    {
        Assert.That(_owner.Fsm.IsStarted, Is.True);
        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<MovementState>());
    }

    /// <summary>
    /// 验证尚未绑定场景移动组件的棋子也可以安全更新默认状态。
    /// </summary>
    [Test]
    public void TickWithoutMovementControllerDoesNotThrow()
    {
        var chessController = new ChessController();

        Assert.DoesNotThrow(chessController.Tick);
        Assert.That(chessController.Fsm.CurrentState, Is.TypeOf<MovementState>());
    }

    /// <summary>
    /// 验证棋子统一 Tick 会经由 MovementState 同步运行时坐标。
    /// </summary>
    [Test]
    public void ChessTickUpdatesMovementThroughMovementState()
    {
        Assert.That(_movementController.MoveToPosition(new Vector2(7f, 8f)), Is.True);
        _astarAI.FakePosition = new Vector3(6f, 3f, 9f);

        _owner.Tick();

        Assert.That(_owner.RuntimeData.CurrentPosition, Is.EqualTo(new Vector2(6f, 9f)));
    }

    /// <summary>
    /// 验证离开移动状态会停止 A* 并清除尚未完成的命令。
    /// </summary>
    [Test]
    public void LeavingMovementStateStopsAndClearsMovement()
    {
        Assert.That(_movementController.MoveToPosition(new Vector2(7f, 8f)), Is.True);

        OtherState otherState = EnterOtherState();

        Assert.That(_owner.Fsm.CurrentState, Is.SameAs(otherState));
        Assert.That(_astarAI.isStopped, Is.True);
        Assert.That(_movementController.HasDestination, Is.False);
    }

    /// <summary>
    /// 验证成功的固定坐标命令会让其他状态切回 MovementState。
    /// </summary>
    [Test]
    public void SuccessfulPositionCommandReturnsToMovementState()
    {
        EnterOtherState();

        Assert.That(_movementController.MoveToPosition(new Vector2(7f, 8f)), Is.True);

        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<MovementState>());
    }

    /// <summary>
    /// 验证成功的目标追踪命令会让其他状态切回 MovementState。
    /// </summary>
    [Test]
    public void SuccessfulTargetCommandReturnsToMovementState()
    {
        var target = new ChessController();
        target.RuntimeData.CurrentPosition = new Vector2(12f, 4f);
        EnterOtherState();

        Assert.That(_movementController.MoveToTarget(target), Is.True);

        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<MovementState>());
    }

    /// <summary>
    /// 验证失败的移动命令不会打断棋子的当前状态。
    /// </summary>
    [Test]
    public void FailedMovementCommandsKeepCurrentState()
    {
        OtherState otherState = EnterOtherState();
        var invalidTarget = new ChessController();
        invalidTarget.RuntimeData.CurrentPosition = new Vector2(21f, 4f);

        Assert.That(_movementController.MoveToPosition(new Vector2(-1f, 5f)), Is.False);
        Assert.That(_movementController.MoveToTarget(invalidTarget), Is.False);

        Assert.That(_owner.Fsm.CurrentState, Is.SameAs(otherState));
    }

    /// <summary>
    /// 验证 A* 寻路请求抛出异常时不会切换当前状态或保留半完成命令。
    /// </summary>
    [Test]
    public void PathSearchFailureKeepsCurrentState()
    {
        OtherState otherState = EnterOtherState();
        _astarAI.ThrowOnSearchPath = true;
        LogAssert.Expect(LogType.Error, "棋子 A* 寻路请求失败：测试寻路失败");

        Assert.That(_movementController.MoveToPosition(new Vector2(7f, 8f)), Is.False);

        Assert.That(_owner.Fsm.CurrentState, Is.SameAs(otherState));
        Assert.That(_movementController.HasDestination, Is.False);
    }

    /// <summary>
    /// 验证 Stop 只负责移动清理，不隐式改变 FSM 状态。
    /// </summary>
    [Test]
    public void StopDoesNotChangeCurrentState()
    {
        OtherState otherState = EnterOtherState();

        _movementController.Stop();

        Assert.That(_owner.Fsm.CurrentState, Is.SameAs(otherState));
    }

    [Test]
    public void FixedPositionUsesXZCoordinatesAndSynchronizesRuntimePosition()
    {
        Assert.That(_owner.RuntimeData.CurrentPosition, Is.EqualTo(new Vector2(2f, 4f)));

        Assert.That(_movementController.MoveToPosition(new Vector2(7.25f, 8.5f)), Is.True);

        Assert.That(_astarAI.destination, Is.EqualTo(new Vector3(7.25f, 3f, 8.5f)));
        Assert.That(_astarAI.SearchPathCount, Is.EqualTo(1));
        Assert.That(_movementController.Destination, Is.EqualTo(new Vector2(7.25f, 8.5f)));

        _astarAI.FakePosition = new Vector3(5f, 6f, 7f);
        _movementController.Tick();

        Assert.That(_owner.RuntimeData.CurrentPosition, Is.EqualTo(new Vector2(5f, 7f)));
    }

    [Test]
    public void InvalidFixedPositionIsRejected()
    {
        Assert.That(_movementController.MoveToPosition(new Vector2(-0.01f, 5f)), Is.False);
        Assert.That(_movementController.MoveToPosition(new Vector2(float.NaN, 5f)), Is.False);
        Assert.That(_astarAI.SearchPathCount, Is.EqualTo(0));
    }

    [Test]
    public void InvalidTargetStopsAndClearsCommand()
    {
        var target = new ChessController();
        target.RuntimeData.CurrentPosition = new Vector2(21f, 4f);

        Assert.That(_movementController.MoveToTarget(target), Is.False);

        Assert.That(_astarAI.isStopped, Is.True);
        Assert.That(_movementController.HasDestination, Is.False);
        Assert.That(_movementController.TargetChess, Is.Null);
    }

    [Test]
    public void TargetMovementKeepsSafeDistanceAndRefreshesWhenTargetMoves()
    {
        var target = new ChessController();
        target.RuntimeData.CurrentPosition = new Vector2(12f, 4f);

        Assert.That(_movementController.MoveToTarget(target), Is.True);

        float initialDistance = Vector2.Distance(
            new Vector2(_astarAI.destination.x, _astarAI.destination.z),
            target.RuntimeData.CurrentPosition);
        Assert.That(initialDistance, Is.EqualTo(ChessMovementController.NoOverlapDistance).Within(0.0001f));
        Assert.That(_astarAI.SearchPathCount, Is.EqualTo(1));

        target.RuntimeData.CurrentPosition = new Vector2(12f, 10f);
        _movementController.Tick();

        Assert.That(_astarAI.SearchPathCount, Is.EqualTo(2));
        float refreshedDistance = Vector2.Distance(
            new Vector2(_astarAI.destination.x, _astarAI.destination.z),
            target.RuntimeData.CurrentPosition);
        Assert.That(refreshedDistance, Is.GreaterThanOrEqualTo(ChessMovementController.NoOverlapDistance));
        Assert.That(_movementController.TargetChess, Is.SameAs(target));
    }

    [Test]
    public void TargetAlreadyWithinSafeDistanceStopsWithoutPathRequest()
    {
        var target = new ChessController();
        target.RuntimeData.CurrentPosition = new Vector2(2.5f, 4f);

        Assert.That(_movementController.MoveToTarget(target), Is.True);

        Assert.That(_astarAI.isStopped, Is.True);
        Assert.That(_astarAI.SearchPathCount, Is.EqualTo(0));
        Assert.That(_movementController.HasReachedDestination, Is.True);
        Assert.That(_movementController.IsMoving, Is.False);
    }

    [Test]
    public void StopClearsCommandAndStopsAstar()
    {
        Assert.That(_movementController.MoveToPosition(new Vector2(7f, 8f)), Is.True);

        _movementController.Stop();

        Assert.That(_astarAI.isStopped, Is.True);
        Assert.That(_movementController.HasDestination, Is.False);
        Assert.That(_movementController.TargetChess, Is.Null);
        Assert.That(_movementController.IsMoving, Is.False);
    }

    [Test]
    public void NewCommandReplacesPreviousTargetCommand()
    {
        var target = new ChessController();
        target.RuntimeData.CurrentPosition = new Vector2(12f, 4f);
        Assert.That(_movementController.MoveToTarget(target), Is.True);

        Assert.That(_movementController.MoveToPosition(new Vector2(9f, 9f)), Is.True);

        Assert.That(_movementController.TargetChess, Is.Null);
        Assert.That(_movementController.Destination, Is.EqualTo(new Vector2(9f, 9f)));
        Assert.That(_astarAI.destination, Is.EqualTo(new Vector3(9f, 3f, 9f)));
    }

    /// <summary>
    /// 注册并进入一个测试专用的非移动状态。
    /// </summary>
    private OtherState EnterOtherState()
    {
        var otherState = new OtherState(_owner.Fsm);
        _owner.Fsm.AddState(otherState);
        _owner.Fsm.ChangeState<OtherState>();
        return otherState;
    }

    /// <summary>
    /// 表示攻击等未来状态，用于验证 MovementState 的进入和退出边界。
    /// </summary>
    private sealed class OtherState : StateBase
    {
        public OtherState(FSM fsm) : base(fsm)
        {
        }
    }

    private sealed class FakeAstarAI : IAstarAI
    {
        public Vector3 FakePosition;
        public int SearchPathCount;
        public bool ThrowOnSearchPath;

        public float radius { get; set; }
        public float height { get; set; }
        public Vector3 position => FakePosition;
        public Quaternion rotation { get; set; }
        public float maxSpeed { get; set; }
        public Vector3 velocity => Vector3.zero;
        public Vector3 desiredVelocity => Vector3.zero;
        public Vector3 desiredVelocityWithoutLocalAvoidance { get; set; }
        public float remainingDistance => 0f;
        public bool reachedDestination { get; set; }
        public bool reachedEndOfPath { get; set; }
        public Vector3 endOfPath => destination;
        public Vector3 destination { get; set; }
        public bool canSearch { get; set; }
        public bool simulateMovement { get; set; } = true;
        public bool hasPath { get; set; }
        public bool pathPending { get; set; }
        public bool updatePosition { get; set; } = true;
        public bool updateRotation { get; set; } = true;
        public bool isStopped { get; set; }
        public Vector3 steeringTarget => destination;
        public System.Action onSearchPath { get; set; }
        public NativeMovementPlane movementPlane => default;

        public void GetRemainingPath(List<Vector3> buffer, out bool stale)
        {
            buffer.Clear();
            buffer.Add(FakePosition);
            stale = false;
        }

        public void GetRemainingPath(
            List<Vector3> buffer,
            List<PathPartWithLinkInfo> partsBuffer,
            out bool stale)
        {
            GetRemainingPath(buffer, out stale);
            partsBuffer?.Clear();
        }

        public void SearchPath()
        {
            if (ThrowOnSearchPath)
            {
                // 模拟底层 A* 在创建路径时失败。
                throw new System.InvalidOperationException("测试寻路失败");
            }

            SearchPathCount++;
            onSearchPath?.Invoke();
        }

        public void SetPath(Path path, bool updateDestinationFromPath = true)
        {
        }

        public void Teleport(Vector3 newPosition, bool clearPath = true)
        {
            FakePosition = newPosition;
        }

        public void Move(Vector3 deltaPosition)
        {
            FakePosition += deltaPosition;
        }

        public void MovementUpdate(float deltaTime, out Vector3 nextPosition, out Quaternion nextRotation)
        {
            nextPosition = FakePosition;
            nextRotation = rotation;
        }

        public void FinalizeMovement(Vector3 nextPosition, Quaternion nextRotation)
        {
            FakePosition = nextPosition;
            rotation = nextRotation;
        }
    }
}
#endif
