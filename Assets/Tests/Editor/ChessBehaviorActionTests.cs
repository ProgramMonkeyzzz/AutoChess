#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using AutoChess;
using AutoChess.Navigation;
using BehaviorTree;
using Chess;
using Chess.Behavior;
using Chess.FSM.State;
using Camp;
using Config;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 覆盖棋子行为树 Action 的发令结果、移动/攻击副作用、SelectTarget 索敌和 LogicId 注册。
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
    /// 验证无 Owner 时棋子 Action 全部失败。
    /// </summary>
    [TestCase(ChessBehaviorTreeLogic.AttackTarget)]
    [TestCase(ChessBehaviorTreeLogic.MoveToTarget)]
    [TestCase(ChessBehaviorTreeLogic.MoveAwayFromTarget)]
    [TestCase(ChessBehaviorTreeLogic.SelectTarget)]
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
        _owner.AttackController.AttackTarget();
        Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<AttackState>());

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
    /// 验证逻辑目录包含棋子 Action，且重复 Ensure 不会抛异常。
    /// </summary>
    [Test]
    public void CatalogContainsChessActionsAndEnsureIsIdempotent()
    {
        Assert.DoesNotThrow(ChessBehaviorTreeLogic.EnsureCatalogRegistered);

        AssertCatalogEntry(ChessBehaviorTreeLogic.AttackTarget, "攻击目标");
        AssertCatalogEntry(ChessBehaviorTreeLogic.MoveToTarget, "向目标移动");
        AssertCatalogEntry(ChessBehaviorTreeLogic.MoveAwayFromTarget, "向目标反方向移动");
        AssertSelectTargetCatalogEntry();
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
    /// 验证无敌人时 SelectTarget 失败。
    /// </summary>
    [Test]
    public void SelectTargetFailsWhenNoEnemies()
    {
        CampManager camps = CreateCampManager();
        ChessController owner = camps.RegisterChess(1, 1, Vector2.zero);

        Assert.That(
            TickAction(ChessBehaviorTreeLogic.SelectTarget, owner),
            Is.EqualTo(NodeStatus.Failure));
        Assert.That(owner.TargetSelector.SelectedChessController, Is.Null);
    }

    /// <summary>
    /// 验证非法 Mode 失败且不改写当前索敌目标。
    /// </summary>
    [Test]
    public void SelectTargetFailsForInvalidModeAndKeepsPreviousTarget()
    {
        Assert.That(
            TickAction(
                ChessBehaviorTreeLogic.SelectTarget,
                _owner,
                CreateModeParameters(99)),
            Is.EqualTo(NodeStatus.Failure));
        Assert.That(_owner.TargetSelector.SelectedChessController, Is.SameAs(_target));
    }

    /// <summary>
    /// 验证存在敌人时 SelectTarget 成功并选中最近敌人。
    /// </summary>
    [Test]
    public void SelectTargetSucceedsAndSelectsNearestEnemy()
    {
        CampManager camps = CreateCampManager();
        ChessController owner = camps.RegisterChess(1, 1, Vector2.zero);
        ChessController close = camps.RegisterChess(2, 1, new Vector2(1f, 0f));
        camps.RegisterChess(2, 1, new Vector2(10f, 0f));

        Assert.That(
            TickAction(
                ChessBehaviorTreeLogic.SelectTarget,
                owner,
                CreateModeParameters((int)ChessTargetSelector.TargetSelectMode.Nearest)),
            Is.EqualTo(NodeStatus.Success));
        Assert.That(owner.TargetSelector.SelectedChessController, Is.SameAs(close));
    }

    /// <summary>
    /// 验证缺少 Mode 参数时按最近敌人索敌。
    /// </summary>
    [Test]
    public void SelectTargetWithoutModeParameterSelectsNearest()
    {
        CampManager camps = CreateCampManager();
        ChessController owner = camps.RegisterChess(1, 1, Vector2.zero);
        ChessController close = camps.RegisterChess(2, 1, new Vector2(1f, 0f));
        camps.RegisterChess(2, 1, new Vector2(10f, 0f));

        Assert.That(
            TickAction(ChessBehaviorTreeLogic.SelectTarget, owner),
            Is.EqualTo(NodeStatus.Success));
        Assert.That(owner.TargetSelector.SelectedChessController, Is.SameAs(close));
    }

    /// <summary>
    /// 验证 Register 后可以通过 LogicId 和 Mode 参数构建并 Tick SelectTarget。
    /// </summary>
    [Test]
    public void RegisterBuildsAndTicksSelectTargetByLogicId()
    {
        CampManager camps = CreateCampManager();
        ChessController owner = camps.RegisterChess(1, 1, Vector2.zero);
        ChessController far = camps.RegisterChess(2, 1, new Vector2(10f, 0f));
        camps.RegisterChess(2, 1, new Vector2(1f, 0f));

        Assert.That(
            TickAction(
                ChessBehaviorTreeLogic.SelectTarget,
                owner,
                CreateModeParameters((int)ChessTargetSelector.TargetSelectMode.Farthest)),
            Is.EqualTo(NodeStatus.Success));
        Assert.That(owner.TargetSelector.SelectedChessController, Is.SameAs(far));
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
    private static NodeStatus TickAction(
        string logicId,
        ChessController owner,
        Dictionary<string, object> parameters = null)
    {
        var registry = new BehaviorTreeRegistry();
        ChessBehaviorTreeLogic.Register(registry);
        var action = new ActionNodeData(logicId);
        if (parameters != null)
        {
            foreach (KeyValuePair<string, object> pair in parameters)
            {
                action.Parameters[pair.Key] = pair.Value;
            }
        }

        var root = new RootNodeData();
        root.AddChild(action);
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
    /// 断言逻辑目录中已注册 SelectTarget 及其 Mode 参数。
    /// </summary>
    private static void AssertSelectTargetCatalogEntry()
    {
        Assert.That(
            BehaviorTreeLogicCatalog.TryGet(
                BehaviorTreeLogicKind.Action,
                ChessBehaviorTreeLogic.SelectTarget,
                out BehaviorTreeLogicDefinition definition),
            Is.True);
        Assert.That(definition.DisplayName, Is.EqualTo("选择目标"));
        Assert.That(definition.Category, Is.EqualTo(ChessBehaviorTreeLogic.CatalogCategory));
        Assert.That(definition.Parameters.Count, Is.EqualTo(1));
        Assert.That(definition.Parameters[0].Key, Is.EqualTo(ChessBehaviorTreeLogic.SelectTargetModeParameter));
        Assert.That(definition.Parameters[0].Type, Is.EqualTo(BehaviorTreeParameterType.Int));
        Assert.That(definition.Parameters[0].DisplayName, Is.EqualTo("索敌方式"));
        Assert.That(
            definition.Parameters[0].DefaultInt,
            Is.EqualTo((int)ChessTargetSelector.TargetSelectMode.Nearest));
    }

    /// <summary>
    /// 创建 SelectTarget 的 Mode 参数字典。
    /// </summary>
    private static Dictionary<string, object> CreateModeParameters(int mode)
    {
        return new Dictionary<string, object>
        {
            { ChessBehaviorTreeLogic.SelectTargetModeParameter, mode }
        };
    }

    /// <summary>
    /// 创建已注册默认配置的阵营管理器。
    /// </summary>
    private static CampManager CreateCampManager()
    {
        var configs = new ChessConfigRepository();
        configs.Register(new ChessBaseData
        {
            ConfigId = 1,
            Name = "SelectTargetTest",
            MaxHp = 100,
            Atk = 1,
            Range = 1f,
            MoveSpeed = 1f,
            AtkSpeed = 1f,
            WindUpTime = 0f,
            BulletSpeed = 0f
        });
        return new CampManager(configs);
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
