#if UNITY_EDITOR
using System.Reflection;
using AutoChess;
using AutoChess.Navigation;
using BehaviorTree;
using Chess;
using Chess.Behavior;
using Chess.FSM.State;
using Config;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 覆盖棋子行为树 Action 的发令结果、移动/攻击副作用和 LogicId 注册。
/// </summary>
public class ChessBehaviorActionTests
{
    /// <summary>每个测试使用的己方棋子。</summary>
    private ChessController _owner;

    /// <summary>每个测试使用的索敌目标。</summary>
    private ChessController _target;

    /// <summary>移动相关测试使用的棋盘。</summary>
    private ChessBoard _board;

    /// <summary>移动相关测试使用的导航管理器。</summary>
    private NavigationManager _navigation;

    /// <summary>
    /// 创建带攻击和移动参数的棋子，并把目标放在己方射程内。
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        ChessBehaviorTreeLogic.EnsureCatalogRegistered();
        _owner = new ChessController(CreateConfig(100, 12, 5f, 2f, 0.5f, 2f));
        _target = new ChessController(CreateConfig(80, 1, 4f, 1f, 0f, 1f));
        _owner.RuntimeData.CurrentPosition = new Vector2(5f, 10f);
        _target.RuntimeData.CurrentPosition = new Vector2(8f, 10f);
        SelectTarget(_owner, _target);
    }

    /// <summary>
    /// 释放测试创建的导航管理器。
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        if (_navigation != null)
        {
            _navigation.Dispose();
            _navigation = null;
        }
    }

    /// <summary>
    /// 验证无 Owner 时三个 Action 全部失败。
    /// </summary>
    [TestCase(ChessBehaviorTreeLogic.AttackTarget)]
    [TestCase(ChessBehaviorTreeLogic.MoveToTarget)]
    [TestCase(ChessBehaviorTreeLogic.MoveAwayFromTarget)]
    public void MissingOwnerFailsAllActions(string logicId)
    {
        Assert.That(TickAction(logicId, null), Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证无索敌目标时三个 Action 全部失败。
    /// </summary>
    [TestCase(ChessBehaviorTreeLogic.AttackTarget)]
    [TestCase(ChessBehaviorTreeLogic.MoveToTarget)]
    [TestCase(ChessBehaviorTreeLogic.MoveAwayFromTarget)]
    public void MissingTargetFailsAllActions(string logicId)
    {
        BindOwnerMovement();
        SelectTarget(_owner, null);

        Assert.That(TickAction(logicId, _owner), Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证未绑定移动控制器时两个移动 Action 失败，攻击仍可成功。
    /// </summary>
    [Test]
    public void MissingMovementControllerFailsMoveActionsButAllowsAttack()
    {
        Assert.That(
            TickAction(ChessBehaviorTreeLogic.MoveToTarget, _owner),
            Is.EqualTo(NodeStatus.Failure));
        Assert.That(
            TickAction(ChessBehaviorTreeLogic.MoveAwayFromTarget, _owner),
            Is.EqualTo(NodeStatus.Failure));
        Assert.That(
            TickAction(ChessBehaviorTreeLogic.AttackTarget, _owner),
            Is.EqualTo(NodeStatus.Success));
        Assert.That(_owner.AttackController.IsWindingUp, Is.True);
    }

    /// <summary>
    /// 验证射程内攻击 Action 成功并进入前摇。
    /// </summary>
    [Test]
    public void AttackTargetSucceedsInRangeAndStartsWindUp()
    {
        Assert.That(
            TickAction(ChessBehaviorTreeLogic.AttackTarget, _owner),
            Is.EqualTo(NodeStatus.Success));
        Assert.That(_owner.AttackController.IsWindingUp, Is.True);
        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<AttackState>());
        Assert.That(_target.RuntimeData.Hp, Is.EqualTo(80));
    }

    /// <summary>
    /// 验证零前摇攻击 Action 成功并立即命中。
    /// </summary>
    [Test]
    public void AttackTargetWithZeroWindUpHitsImmediately()
    {
        _owner = new ChessController(CreateConfig(100, 12, 5f, 2f, 0f, 2f));
        _owner.RuntimeData.CurrentPosition = new Vector2(5f, 10f);
        SelectTarget(_owner, _target);

        Assert.That(
            TickAction(ChessBehaviorTreeLogic.AttackTarget, _owner),
            Is.EqualTo(NodeStatus.Success));
        Assert.That(_owner.AttackController.IsAttackInProgress, Is.True);
        Assert.That(_target.RuntimeData.Hp, Is.EqualTo(68));
    }

    /// <summary>
    /// 验证越界、死亡目标和选中自己时攻击 Action 失败。
    /// </summary>
    [Test]
    public void AttackTargetFailsForInvalidTargets()
    {
        _target.RuntimeData.CurrentPosition = new Vector2(12f, 10f);
        Assert.That(
            TickAction(ChessBehaviorTreeLogic.AttackTarget, _owner),
            Is.EqualTo(NodeStatus.Failure));

        _target.RuntimeData.CurrentPosition = new Vector2(8f, 10f);
        _target.RuntimeData.Hp = 0;
        Assert.That(
            TickAction(ChessBehaviorTreeLogic.AttackTarget, _owner),
            Is.EqualTo(NodeStatus.Failure));

        _target.RuntimeData.Hp = 80;
        SelectTarget(_owner, _owner);
        Assert.That(
            TickAction(ChessBehaviorTreeLogic.AttackTarget, _owner),
            Is.EqualTo(NodeStatus.Failure));
        Assert.That(_owner.AttackController.IsAttackInProgress, Is.False);
    }

    /// <summary>
    /// 验证已在前摇或冷却中且仍有目标时，再次执行攻击 Action 仍成功。
    /// </summary>
    [Test]
    public void AttackTargetSucceedsWhenAttackAlreadyInProgress()
    {
        _owner.AttackController.AttackTarget();
        Assert.That(_owner.AttackController.IsWindingUp, Is.True);
        Assert.That(
            TickAction(ChessBehaviorTreeLogic.AttackTarget, _owner),
            Is.EqualTo(NodeStatus.Success));

        _owner = new ChessController(CreateConfig(100, 12, 5f, 2f, 0f, 2f));
        _owner.RuntimeData.CurrentPosition = new Vector2(5f, 10f);
        SelectTarget(_owner, _target);
        _owner.AttackController.AttackTarget();
        Assert.That(_owner.AttackController.IsWindingUp, Is.False);
        Assert.That(_owner.AttackController.CooldownRemaining, Is.GreaterThan(0f));
        Assert.That(
            TickAction(ChessBehaviorTreeLogic.AttackTarget, _owner),
            Is.EqualTo(NodeStatus.Success));
    }

    /// <summary>
    /// 验证向目标移动 Action 成功后建立命令并切回移动状态。
    /// </summary>
    [Test]
    public void MoveToTargetSucceedsAndEntersMovementState()
    {
        BindOwnerMovement();
        _owner.Fsm.ChangeState<AttackState>();

        Assert.That(
            TickAction(ChessBehaviorTreeLogic.MoveToTarget, _owner),
            Is.EqualTo(NodeStatus.Success));
        Assert.That(_owner.MovementController.HasDestination, Is.True);
        Assert.That(_owner.MovementController.TargetChess, Is.SameAs(_target));
        Assert.That(_owner.MovementController.Destination.x, Is.EqualTo(7f).Within(0.0001f));
        Assert.That(_owner.MovementController.Destination.y, Is.EqualTo(10f).Within(0.0001f));
        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<MovementState>());
    }

    /// <summary>
    /// 验证选中自己或自己死亡时向目标移动失败。
    /// </summary>
    [Test]
    public void MoveToTargetFailsForSelfOrDeadOwner()
    {
        BindOwnerMovement();
        SelectTarget(_owner, _owner);
        Assert.That(
            TickAction(ChessBehaviorTreeLogic.MoveToTarget, _owner),
            Is.EqualTo(NodeStatus.Failure));
        Assert.That(_owner.MovementController.HasDestination, Is.False);

        SelectTarget(_owner, _target);
        _owner.DamageReceiver.ApplyDamage(1000);
        Assert.That(
            TickAction(ChessBehaviorTreeLogic.MoveToTarget, _owner),
            Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证死亡目标只要坐标合法，向目标移动仍然成功。
    /// </summary>
    [Test]
    public void MoveToTargetSucceedsForDeadTargetWithValidPosition()
    {
        BindOwnerMovement();
        _target.RuntimeData.Hp = 0;

        Assert.That(
            TickAction(ChessBehaviorTreeLogic.MoveToTarget, _owner),
            Is.EqualTo(NodeStatus.Success));
        Assert.That(_owner.MovementController.HasDestination, Is.True);
    }

    /// <summary>
    /// 验证反向移动 Action 把终点放在反方向并夹到棋盘内。
    /// </summary>
    [Test]
    public void MoveAwayFromTargetPlacesDestinationOppositeAndClamped()
    {
        BindOwnerMovement();

        Assert.That(
            TickAction(ChessBehaviorTreeLogic.MoveAwayFromTarget, _owner),
            Is.EqualTo(NodeStatus.Success));
        Assert.That(_owner.MovementController.Destination.x, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(_owner.MovementController.Destination.y, Is.EqualTo(10f).Within(0.0001f));
        Assert.That(_owner.MovementController.IsMoving, Is.True);
        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<MovementState>());
    }

    /// <summary>
    /// 验证目标移动后 Tick 会刷新远离终点。
    /// </summary>
    [Test]
    public void MoveAwayFromTargetRefreshesDestinationWhenTargetMoves()
    {
        BindOwnerMovement();
        Assert.That(
            TickAction(ChessBehaviorTreeLogic.MoveAwayFromTarget, _owner),
            Is.EqualTo(NodeStatus.Success));

        _target.RuntimeData.CurrentPosition = new Vector2(5f, 2f);
        _owner.MovementController.Tick();

        Assert.That(_owner.MovementController.Destination.x, Is.EqualTo(5f).Within(0.0001f));
        Assert.That(_owner.MovementController.Destination.y, Is.EqualTo(19.5f).Within(0.0001f));
    }

    /// <summary>
    /// 验证与目标重叠时远离使用默认 +Z 方向。
    /// </summary>
    [Test]
    public void MoveAwayFromTargetUsesDefaultDirectionWhenOverlapping()
    {
        BindOwnerMovement();
        _target.RuntimeData.CurrentPosition = _owner.RuntimeData.CurrentPosition;

        Assert.That(
            TickAction(ChessBehaviorTreeLogic.MoveAwayFromTarget, _owner),
            Is.EqualTo(NodeStatus.Success));
        Assert.That(_owner.MovementController.Destination.x, Is.EqualTo(5f).Within(0.0001f));
        Assert.That(_owner.MovementController.Destination.y, Is.EqualTo(19.5f).Within(0.0001f));
    }

    /// <summary>
    /// 验证已经贴边时远离命令视为到达。
    /// </summary>
    [Test]
    public void MoveAwayFromTargetReachesWhenAlreadyOnFarEdge()
    {
        BindOwnerMovement();
        _owner.RuntimeData.CurrentPosition = new Vector2(0.5f, 10f);

        Assert.That(
            TickAction(ChessBehaviorTreeLogic.MoveAwayFromTarget, _owner),
            Is.EqualTo(NodeStatus.Success));
        Assert.That(_owner.MovementController.HasReachedDestination, Is.True);
        Assert.That(_owner.MovementController.IsMoving, Is.False);
    }

    /// <summary>
    /// 验证选中自己时反向移动失败。
    /// </summary>
    [Test]
    public void MoveAwayFromTargetFailsWhenSelectingSelf()
    {
        BindOwnerMovement();
        SelectTarget(_owner, _owner);

        Assert.That(
            TickAction(ChessBehaviorTreeLogic.MoveAwayFromTarget, _owner),
            Is.EqualTo(NodeStatus.Failure));
        Assert.That(_owner.MovementController.HasDestination, Is.False);
    }

    /// <summary>
    /// 验证逻辑目录包含三个棋子 Action，且重复 Ensure 不会抛异常。
    /// </summary>
    [Test]
    public void CatalogContainsChessActionsAndEnsureIsIdempotent()
    {
        Assert.DoesNotThrow(ChessBehaviorTreeLogic.EnsureCatalogRegistered);

        AssertCatalogEntry(ChessBehaviorTreeLogic.AttackTarget, "攻击目标");
        AssertCatalogEntry(ChessBehaviorTreeLogic.MoveToTarget, "向目标移动");
        AssertCatalogEntry(ChessBehaviorTreeLogic.MoveAwayFromTarget, "向目标反方向移动");
    }

    /// <summary>
    /// 验证 Register 后可以通过 LogicId 构建并 Tick 棋子 Action。
    /// </summary>
    [Test]
    public void RegisterBuildsAndTicksActionsByLogicId()
    {
        BindOwnerMovement();

        Assert.That(
            TickAction(ChessBehaviorTreeLogic.MoveToTarget, _owner),
            Is.EqualTo(NodeStatus.Success));
        Assert.That(
            TickAction(ChessBehaviorTreeLogic.MoveAwayFromTarget, _owner),
            Is.EqualTo(NodeStatus.Success));
        Assert.That(
            TickAction(ChessBehaviorTreeLogic.AttackTarget, _owner),
            Is.EqualTo(NodeStatus.Success));
    }

    /// <summary>
    /// 绑定己方棋子的导航代理，供移动 Action 使用。
    /// </summary>
    private void BindOwnerMovement()
    {
        _board = new ChessBoard { Width = 20, Height = 20 };
        _navigation = new NavigationManager(_board);
        NavigationAgentHandle agent = _navigation.RegisterAgent(
            _owner.RuntimeData.CurrentPosition,
            ChessMovementController.ChessRadius,
            2f);
        _owner.InitializeMovement(_board, agent);
    }

    /// <summary>
    /// 使用指定 LogicId 构建单节点行为树并推进一次。
    /// </summary>
    private static NodeStatus TickAction(string logicId, ChessController owner)
    {
        var registry = new BehaviorTreeRegistry();
        ChessBehaviorTreeLogic.Register(registry);
        var root = new RootNodeData();
        root.AddChild(new ActionNodeData(logicId));
        var runner = new BehaviorTreeRunner(
            new BehaviorTreeDefinition(root),
            registry,
            new Blackboard(),
            owner);
        runner.Start();
        return runner.Tick();
    }

    /// <summary>
    /// 断言逻辑目录中已注册指定棋子 Action。
    /// </summary>
    private static void AssertCatalogEntry(string logicId, string displayName)
    {
        Assert.That(
            BehaviorTreeLogicCatalog.TryGet(
                BehaviorTreeLogicKind.Action,
                logicId,
                out BehaviorTreeLogicDefinition definition),
            Is.True);
        Assert.That(definition.DisplayName, Is.EqualTo(displayName));
        Assert.That(definition.Category, Is.EqualTo(ChessBehaviorTreeLogic.CatalogCategory));
        Assert.That(definition.Parameters, Is.Empty);
    }

    /// <summary>
    /// 创建测试用棋子配置。
    /// </summary>
    private static ChessBaseData CreateConfig(
        int maxHp,
        int atk,
        float range,
        float atkSpeed,
        float windUpTime,
        float moveSpeed)
    {
        return new ChessBaseData
        {
            MaxHp = maxHp,
            Atk = atk,
            Range = range,
            MoveSpeed = moveSpeed,
            AtkSpeed = atkSpeed,
            WindUpTime = windUpTime,
            BulletSpeed = 0f
        };
    }

    /// <summary>
    /// 测试侧设置当前索敌目标，保持生产目标选择器不开放手动写入接口。
    /// </summary>
    private static void SelectTarget(ChessController owner, ChessController target)
    {
        PropertyInfo property = typeof(ChessTargetSelector).GetProperty(
            nameof(ChessTargetSelector.SelectedChessController));
        property.GetSetMethod(true).Invoke(owner.TargetSelector, new object[] { target });
    }
}
#endif
