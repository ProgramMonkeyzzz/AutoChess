#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using AutoChess;
using AutoChess.Navigation;
using Chess;
using Chess.FSM.State;
using Config;
using NUnit.Framework;
using UnityEngine;

/// <summary>覆盖无障碍棋盘上的 RVO 移动命令和快照交换语义。</summary>
public class ChessMovementControllerTests
{
    private ChessBoard _board;
    private NavigationManager _navigation;
    private ChessController _owner;
    private NavigationAgentHandle _agent;
    private ChessMovementController _movement;

    [SetUp]
    public void SetUp()
    {
        _board = new ChessBoard { Width = 20, Height = 20 };
        _navigation = new NavigationManager(_board);
        _owner = new ChessController(CreateConfig(100, 2f));
        _owner.RuntimeData.CurrentPosition = new Vector2(2f, 4f);
        _agent = _navigation.RegisterAgent(_owner.RuntimeData.CurrentPosition, ChessMovementController.ChessRadius, 2f);
        _movement = _owner.InitializeMovement(_board, _agent);
    }

    [TearDown]
    public void TearDown()
    {
        _navigation.Dispose();
    }

    /// <summary>验证棋子默认进入移动状态并成功绑定 RVO 代理。</summary>
    [Test]
    public void NewChessStartsInMovementStateAndHasAgent()
    {
        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<MovementState>());
        Assert.That(_agent.IsValid, Is.True);
        Assert.That(_movement.Agent, Is.EqualTo(_agent));
    }

    /// <summary>验证棋子创建后已注册死亡状态并绑定伤害接收器。</summary>
    [Test]
    public void NewChessRegistersDeadStateAndDamageReceiver()
    {
        Assert.That(_owner.DamageReceiver, Is.Not.Null);
        _owner.RuntimeData.Hp = 0;
        Assert.That(_owner.Fsm.CheckCurrentStateTransition(), Is.TypeOf<DeadState>());
        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<DeadState>());
    }

    /// <summary>验证已注册的死亡转换会在移动状态 Tick 时响应归零的 HP。</summary>
    [Test]
    public void ZeroHpTransitionsToDeadStateOnFsmTick()
    {
        _owner.RuntimeData.Hp = 0;
        _owner.Fsm.Tick();
        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<DeadState>());
    }

    /// <summary>验证尚未绑定导航代理的棋子也可以安全更新默认状态。</summary>
    [Test]
    public void TickWithoutMovementControllerDoesNotThrow()
    {
        var chessController = new ChessController();
        chessController.RuntimeData.Hp = 100;
        Assert.DoesNotThrow(() => chessController.Tick(0f));
        Assert.That(chessController.Fsm.CurrentState, Is.TypeOf<MovementState>());
    }

    /// <summary>验证非致命伤害只扣除 HP，不切换棋子状态。</summary>
    [Test]
    public void NonLethalDamageKeepsMovementState()
    {
        _owner.RuntimeData.Hp = 100;
        _owner.DamageReceiver.ApplyDamage(30);
        Assert.That(_owner.RuntimeData.Hp, Is.EqualTo(70));
        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<MovementState>());
    }

    /// <summary>验证致命伤害会立即切换到死亡状态并停止已有移动。</summary>
    [Test]
    public void LethalDamageImmediatelyEntersDeadState()
    {
        _owner.RuntimeData.Hp = 50;
        Assert.That(_movement.MoveToPosition(new Vector2(7f, 8f)), Is.True);
        _owner.DamageReceiver.ApplyDamage(50);
        Assert.That(_owner.RuntimeData.Hp, Is.EqualTo(0));
        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<DeadState>());
        Assert.That(_movement.HasDestination, Is.False);
    }

    /// <summary>验证固定点命令只更新本地命令并生成纯数据输入。</summary>
    [Test]
    public void FixedPositionBuildsNavigationInput()
    {
        Assert.That(_movement.MoveToPosition(new Vector2(7f, 8f)), Is.True);

        NavigationInputSnapshot input = _movement.BuildNavigationInput(1);
        Assert.That(input.Agent, Is.EqualTo(_agent));
        Assert.That(input.TargetPoint, Is.EqualTo(new Vector2(7f, 8f)));
        Assert.That(input.DesiredSpeed, Is.EqualTo(2f));
        Assert.That(input.Enabled, Is.True);
    }

    /// <summary>验证 RVO 结果会更新运行时位置并支持到达判断。</summary>
    [Test]
    public void NavigationResultUpdatesRuntimePosition()
    {
        Assert.That(_movement.MoveToPosition(new Vector2(7f, 8f)), Is.True);
        NavigationInputSnapshot input = _movement.BuildNavigationInput(1);
        IReadOnlyList<NavigationResultSnapshot> results = _navigation.Tick(
            1,
            new[] { input },
            0.5f);

        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(_movement.ApplyNavigationResult(results[0]), Is.True);
        Assert.That(_owner.RuntimeData.CurrentPosition, Is.Not.EqualTo(new Vector2(2f, 4f)));
        Assert.That(_owner.RuntimeData.CurrentPosition.x, Is.InRange(0.5f, 19.5f));
        Assert.That(_owner.RuntimeData.CurrentPosition.y, Is.InRange(0.5f, 19.5f));
    }

    /// <summary>验证追踪目标会保持双方半径之和的安全距离。</summary>
    [Test]
    public void TargetMovementKeepsSafeDistance()
    {
        ChessController target = new ChessController(CreateConfig(100, 1f));
        target.RuntimeData.CurrentPosition = new Vector2(12f, 4f);

        Assert.That(_movement.MoveToTarget(target), Is.True);
        Assert.That(_movement.Destination.x, Is.EqualTo(11f).Within(0.0001f));
        Assert.That(_movement.Destination.y, Is.EqualTo(4f).Within(0.0001f));
        Assert.That(_movement.HasReachedDestination, Is.False);
    }

    /// <summary>验证进入攻击或死亡状态时 Stop 会清除移动命令。</summary>
    [Test]
    public void StateExitStopsMovementCommand()
    {
        Assert.That(_movement.MoveToPosition(new Vector2(7f, 8f)), Is.True);
        ChessController target = new ChessController(CreateConfig(100, 1f));
        target.RuntimeData.CurrentPosition = new Vector2(3f, 4f);
        SelectTarget(_owner, target);
        _owner.AttackController.AttackTarget();
        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<AttackState>());
        Assert.That(_movement.HasDestination, Is.False);

        Assert.That(_movement.MoveToPosition(new Vector2(7f, 8f)), Is.True);
        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<MovementState>());
        _owner.DamageReceiver.ApplyDamage(1000);
        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<DeadState>());
        Assert.That(_movement.HasDestination, Is.False);
    }

    /// <summary>验证死亡棋子拒绝新的移动命令。</summary>
    [Test]
    public void DeadChessRejectsMovement()
    {
        _owner.DamageReceiver.ApplyDamage(1000);

        Assert.That(_movement.MoveToPosition(new Vector2(7f, 8f)), Is.False);
        Assert.That(_movement.HasDestination, Is.False);
    }

    /// <summary>验证旧命令结果不能覆盖新命令结果。</summary>
    [Test]
    public void StaleNavigationResultIsIgnored()
    {
        Assert.That(_movement.MoveToPosition(new Vector2(7f, 8f)), Is.True);
        NavigationInputSnapshot oldInput = _movement.BuildNavigationInput(1);
        Assert.That(_movement.MoveToPosition(new Vector2(9f, 9f)), Is.True);
        NavigationResultSnapshot stale = new NavigationResultSnapshot(
            1,
            _agent,
            new Vector2(7f, 8f),
            2f,
            true,
            false,
            oldInput.CommandVersion);

        Assert.That(_movement.ApplyNavigationResult(stale), Is.False);
        Assert.That(_owner.RuntimeData.CurrentPosition, Is.EqualTo(new Vector2(2f, 4f)));
    }

    /// <summary>验证远离目标会把终点放到反方向并夹到棋盘内。</summary>
    [Test]
    public void MoveAwayFromTargetPlacesOppositeDestination()
    {
        ChessController target = new ChessController(CreateConfig(100, 1f));
        target.RuntimeData.CurrentPosition = new Vector2(12f, 4f);

        Assert.That(_movement.MoveAwayFromTarget(target), Is.True);
        Assert.That(_movement.TargetChess, Is.SameAs(target));
        Assert.That(_movement.Destination.x, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(_movement.Destination.y, Is.EqualTo(4f).Within(0.0001f));
        Assert.That(_movement.HasReachedDestination, Is.False);
        Assert.That(_movement.IsMoving, Is.True);
        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<MovementState>());
    }

    /// <summary>验证目标移动后 Tick 会刷新远离终点。</summary>
    [Test]
    public void MoveAwayFromTargetRefreshesDestinationWhenTargetMoves()
    {
        ChessController target = new ChessController(CreateConfig(100, 1f));
        target.RuntimeData.CurrentPosition = new Vector2(12f, 4f);
        Assert.That(_movement.MoveAwayFromTarget(target), Is.True);

        target.RuntimeData.CurrentPosition = new Vector2(2f, 1f);
        _movement.Tick();

        Assert.That(_movement.Destination.x, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(_movement.Destination.y, Is.EqualTo(19.5f).Within(0.0001f));
    }

    /// <summary>验证与目标重叠时远离使用默认 +Z 方向。</summary>
    [Test]
    public void MoveAwayFromTargetUsesDefaultDirectionWhenOverlapping()
    {
        ChessController target = new ChessController(CreateConfig(100, 1f));
        target.RuntimeData.CurrentPosition = _owner.RuntimeData.CurrentPosition;

        Assert.That(_movement.MoveAwayFromTarget(target), Is.True);
        Assert.That(_movement.Destination.x, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(_movement.Destination.y, Is.EqualTo(19.5f).Within(0.0001f));
    }

    /// <summary>验证已经贴在远离方向边缘时视为到达。</summary>
    [Test]
    public void MoveAwayFromTargetReachesWhenAlreadyOnFarEdge()
    {
        ChessController target = new ChessController(CreateConfig(100, 1f));
        _owner.RuntimeData.CurrentPosition = new Vector2(0.5f, 4f);
        target.RuntimeData.CurrentPosition = new Vector2(12f, 4f);

        Assert.That(_movement.MoveAwayFromTarget(target), Is.True);
        Assert.That(_movement.Destination.x, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(_movement.Destination.y, Is.EqualTo(4f).Within(0.0001f));
        Assert.That(_movement.HasReachedDestination, Is.True);
        Assert.That(_movement.IsMoving, Is.False);
    }

    /// <summary>验证死亡目标只要坐标合法仍可远离。</summary>
    [Test]
    public void MoveAwayFromTargetAllowsDeadTarget()
    {
        ChessController target = new ChessController(CreateConfig(100, 1f));
        target.RuntimeData.CurrentPosition = new Vector2(12f, 4f);
        target.RuntimeData.Hp = 0;

        Assert.That(_movement.MoveAwayFromTarget(target), Is.True);
        Assert.That(_movement.HasDestination, Is.True);
        Assert.That(_movement.Destination.x, Is.EqualTo(0.5f).Within(0.0001f));
    }

    /// <summary>验证自己或空目标会被拒绝，并清除已有远离命令。</summary>
    [Test]
    public void MoveAwayFromTargetRejectsSelfAndNull()
    {
        ChessController target = new ChessController(CreateConfig(100, 1f));
        target.RuntimeData.CurrentPosition = new Vector2(12f, 4f);
        Assert.That(_movement.MoveAwayFromTarget(target), Is.True);

        Assert.That(_movement.MoveAwayFromTarget(_owner), Is.False);
        Assert.That(_movement.HasDestination, Is.False);

        Assert.That(_movement.MoveAwayFromTarget(target), Is.True);
        Assert.That(_movement.MoveAwayFromTarget(null), Is.False);
        Assert.That(_movement.HasDestination, Is.False);
    }

    /// <summary>创建测试用棋子配置。</summary>
    /// <summary>
    /// 测试侧设置当前索敌目标，保持生产目标选择器不开放手动写入接口。
    /// </summary>
    private static void SelectTarget(ChessController owner, ChessController target)
    {
        PropertyInfo property = typeof(ChessTargetSelector).GetProperty(
            nameof(ChessTargetSelector.SelectedChessController));
        property.GetSetMethod(true).Invoke(owner.TargetSelector, new object[] { target });
    }

    private static ChessBaseData CreateConfig(int hp, float moveSpeed)
    {
        return new ChessBaseData
        {
            MaxHp = hp,
            Atk = 1,
            Range = 5,
            MoveSpeed = moveSpeed,
            AtkSpeed = 1f,
            WindUpTime = 0f,
            BulletSpeed = 0f
        };
    }
}
#endif
