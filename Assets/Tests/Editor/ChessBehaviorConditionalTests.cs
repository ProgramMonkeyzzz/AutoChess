#if UNITY_EDITOR
using System.Reflection;
using BehaviorTree;
using Chess;
using Chess.Behavior;
using Chess.FSM.State;
using Config;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 覆盖棋子目标 Conditional 的索敌、射程、死亡判定和 LogicId 注册。
/// </summary>
public class ChessBehaviorConditionalTests
{
    /// <summary>每个测试使用的己方棋子。</summary>
    private ChessController _owner;

    /// <summary>每个测试使用的索敌目标。</summary>
    private ChessController _target;

    /// <summary>
    /// 创建带默认射程的棋子，并把目标放在己方射程内。
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        ChessBehaviorTreeLogic.EnsureCatalogRegistered();
        _owner = new ChessController(CreateConfig(100, 5f));
        _target = new ChessController(CreateConfig(80, 4f));
        _owner.RuntimeData.CurrentPosition = Vector2.zero;
        _target.RuntimeData.CurrentPosition = new Vector2(3f, 0f);
        SelectTarget(_owner, _target);
    }

    /// <summary>
    /// 验证无 Owner 时四个 Conditional 全部失败。
    /// </summary>
    [TestCase(ChessBehaviorTreeLogic.HasTarget)]
    [TestCase(ChessBehaviorTreeLogic.IsTargetInRange)]
    [TestCase(ChessBehaviorTreeLogic.IsSelfInTargetRange)]
    [TestCase(ChessBehaviorTreeLogic.IsTargetDead)]
    public void MissingOwnerFailsAllConditionals(string logicId)
    {
        Assert.That(TickConditional(logicId, null), Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证无索敌目标时四个 Conditional 全部失败。
    /// </summary>
    [TestCase(ChessBehaviorTreeLogic.HasTarget)]
    [TestCase(ChessBehaviorTreeLogic.IsTargetInRange)]
    [TestCase(ChessBehaviorTreeLogic.IsSelfInTargetRange)]
    [TestCase(ChessBehaviorTreeLogic.IsTargetDead)]
    public void MissingTargetFailsAllConditionals(string logicId)
    {
        SelectTarget(_owner, null);

        Assert.That(TickConditional(logicId, _owner), Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证存在索敌目标时 HasTarget 成功，且不要求目标存活。
    /// </summary>
    [Test]
    public void HasTargetSucceedsWhenSelectedEvenIfDead()
    {
        Assert.That(TickConditional(ChessBehaviorTreeLogic.HasTarget, _owner), Is.EqualTo(NodeStatus.Success));

        _target.RuntimeData.Hp = 0;
        Assert.That(TickConditional(ChessBehaviorTreeLogic.HasTarget, _owner), Is.EqualTo(NodeStatus.Success));
    }

    /// <summary>
    /// 验证选中自己也算存在目标。
    /// </summary>
    [Test]
    public void HasTargetSucceedsWhenSelectingSelf()
    {
        SelectTarget(_owner, _owner);

        Assert.That(TickConditional(ChessBehaviorTreeLogic.HasTarget, _owner), Is.EqualTo(NodeStatus.Success));
    }

    /// <summary>
    /// 验证存活目标不会让 IsTargetDead 成功。
    /// </summary>
    [Test]
    public void IsTargetDeadFailsForLivingTarget()
    {
        Assert.That(_target.RuntimeData.Hp, Is.GreaterThan(0));
        Assert.That(_target.Fsm.CurrentState, Is.Not.TypeOf<DeadState>());
        Assert.That(TickConditional(ChessBehaviorTreeLogic.IsTargetDead, _owner), Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证目标 HP 归零时 IsTargetDead 成功。
    /// </summary>
    [Test]
    public void IsTargetDeadSucceedsWhenHpIsZero()
    {
        _target.RuntimeData.Hp = 0;

        Assert.That(TickConditional(ChessBehaviorTreeLogic.IsTargetDead, _owner), Is.EqualTo(NodeStatus.Success));
    }

    /// <summary>
    /// 验证目标进入死亡状态时 IsTargetDead 成功。
    /// </summary>
    [Test]
    public void IsTargetDeadSucceedsWhenInDeadState()
    {
        _target.RuntimeData.Hp = 0;
        _target.Fsm.CheckCurrentStateTransition();
        Assert.That(_target.Fsm.CurrentState, Is.TypeOf<DeadState>());

        Assert.That(TickConditional(ChessBehaviorTreeLogic.IsTargetDead, _owner), Is.EqualTo(NodeStatus.Success));
    }

    /// <summary>
    /// 验证目标位于自己射程边界内（含相等）时 IsTargetInRange 成功。
    /// </summary>
    [Test]
    public void IsTargetInRangeSucceedsOnExactBoundary()
    {
        _target.RuntimeData.CurrentPosition = new Vector2(5f, 0f);

        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsTargetInRange, _owner),
            Is.EqualTo(NodeStatus.Success));
    }

    /// <summary>
    /// 验证小数距离按精确射程判定，越界失败、界内成功。
    /// </summary>
    [Test]
    public void IsTargetInRangeUsesExactFractionalDistance()
    {
        _owner = new ChessController(CreateConfig(100, 3.5f));
        _owner.RuntimeData.CurrentPosition = Vector2.zero;
        _target.RuntimeData.CurrentPosition = new Vector2(3.4f, 0f);
        SelectTarget(_owner, _target);

        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsTargetInRange, _owner),
            Is.EqualTo(NodeStatus.Success));

        _target.RuntimeData.CurrentPosition = new Vector2(3.6f, 0f);
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsTargetInRange, _owner),
            Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证 IsTargetInRange 使用自己的运行时射程乘区和增量。
    /// </summary>
    [Test]
    public void IsTargetInRangeUsesOwnerCurrentRangeModifiers()
    {
        _target.RuntimeData.CurrentPosition = new Vector2(7.7f, 0f);
        _owner.RuntimeData.CurrentRangeMultipler = 1.5f;
        _owner.RuntimeData.CurrentRangeIncrement = 0.25f;

        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsTargetInRange, _owner),
            Is.EqualTo(NodeStatus.Success));

        _target.RuntimeData.CurrentPosition = new Vector2(7.8f, 0f);
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsTargetInRange, _owner),
            Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证自己位于目标射程边界内时 IsSelfInTargetRange 成功。
    /// </summary>
    [Test]
    public void IsSelfInTargetRangeSucceedsOnExactBoundary()
    {
        _target.RuntimeData.CurrentPosition = new Vector2(4f, 0f);

        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsSelfInTargetRange, _owner),
            Is.EqualTo(NodeStatus.Success));
    }

    /// <summary>
    /// 验证自己越出目标射程时 IsSelfInTargetRange 失败。
    /// </summary>
    [Test]
    public void IsSelfInTargetRangeFailsOutsideTargetRange()
    {
        _target.RuntimeData.CurrentPosition = new Vector2(4.1f, 0f);

        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsSelfInTargetRange, _owner),
            Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证 IsSelfInTargetRange 使用目标的运行时射程乘区和增量。
    /// </summary>
    [Test]
    public void IsSelfInTargetRangeUsesTargetCurrentRangeModifiers()
    {
        _target.RuntimeData.CurrentPosition = new Vector2(6.2f, 0f);
        _target.RuntimeData.CurrentRangeMultipler = 1.5f;
        _target.RuntimeData.CurrentRangeIncrement = 0.25f;

        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsSelfInTargetRange, _owner),
            Is.EqualTo(NodeStatus.Success));

        _target.RuntimeData.CurrentPosition = new Vector2(6.3f, 0f);
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsSelfInTargetRange, _owner),
            Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证死亡目标仍按距离判定射程，且 IsTargetDead 同时成功。
    /// </summary>
    [Test]
    public void DeadTargetStillEvaluatesRangeByDistance()
    {
        _target.RuntimeData.Hp = 0;

        Assert.That(TickConditional(ChessBehaviorTreeLogic.HasTarget, _owner), Is.EqualTo(NodeStatus.Success));
        Assert.That(TickConditional(ChessBehaviorTreeLogic.IsTargetDead, _owner), Is.EqualTo(NodeStatus.Success));
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsTargetInRange, _owner),
            Is.EqualTo(NodeStatus.Success));

        _target.RuntimeData.CurrentPosition = new Vector2(6f, 0f);
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsTargetInRange, _owner),
            Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证逻辑目录包含四个棋子 Conditional，且重复 Ensure 不会抛异常。
    /// </summary>
    [Test]
    public void CatalogContainsChessConditionalsAndEnsureIsIdempotent()
    {
        Assert.DoesNotThrow(ChessBehaviorTreeLogic.EnsureCatalogRegistered);

        AssertCatalogEntry(ChessBehaviorTreeLogic.HasTarget, "当前存在目标");
        AssertCatalogEntry(ChessBehaviorTreeLogic.IsTargetInRange, "目标在自己射程内");
        AssertCatalogEntry(ChessBehaviorTreeLogic.IsSelfInTargetRange, "自己在目标射程内");
        AssertCatalogEntry(ChessBehaviorTreeLogic.IsTargetDead, "当前目标已死亡");
    }

    /// <summary>
    /// 验证 Register 后可以通过 LogicId 构建并 Tick 棋子 Conditional。
    /// </summary>
    [Test]
    public void RegisterBuildsAndTicksConditionalsByLogicId()
    {
        Assert.That(TickConditional(ChessBehaviorTreeLogic.HasTarget, _owner), Is.EqualTo(NodeStatus.Success));
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsTargetInRange, _owner),
            Is.EqualTo(NodeStatus.Success));
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsSelfInTargetRange, _owner),
            Is.EqualTo(NodeStatus.Success));
        Assert.That(TickConditional(ChessBehaviorTreeLogic.IsTargetDead, _owner), Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 使用指定 LogicId 构建单节点行为树并推进一次。
    /// </summary>
    private static NodeStatus TickConditional(string logicId, ChessController owner)
    {
        var registry = new BehaviorTreeRegistry();
        ChessBehaviorTreeLogic.Register(registry);
        var root = new RootNodeData();
        root.AddChild(new ConditionalNodeData(logicId));
        var runner = new BehaviorTreeRunner(
            new BehaviorTreeDefinition(root),
            registry,
            new Blackboard(),
            owner);
        runner.Start();
        return runner.Tick();
    }

    /// <summary>
    /// 断言逻辑目录中已注册指定棋子 Conditional。
    /// </summary>
    private static void AssertCatalogEntry(string logicId, string displayName)
    {
        Assert.That(
            BehaviorTreeLogicCatalog.TryGet(
                BehaviorTreeLogicKind.Conditional,
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
    private static ChessBaseData CreateConfig(int maxHp, float range)
    {
        return new ChessBaseData
        {
            MaxHp = maxHp,
            Atk = 1,
            Range = range,
            MoveSpeed = 1f,
            AtkSpeed = 1f,
            WindUpTime = 0f,
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
