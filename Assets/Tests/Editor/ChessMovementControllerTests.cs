#if UNITY_EDITOR
using System.Collections.Generic;
using AutoChess;
using Chess;
using NUnit.Framework;
using Pathfinding;
using Pathfinding.Util;
using UnityEngine;

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
        _movementController = new ChessMovementController(_owner, _board, _astarAI);
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

    private sealed class FakeAstarAI : IAstarAI
    {
        public Vector3 FakePosition;
        public int SearchPathCount;

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
