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
/// 覆盖棋子目标 Conditional 的索敌、射程比较、死亡判定、攻击冷却、Invert 取反和 LogicId 注册。
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
    /// 验证无 Owner 时 Conditional 全部失败。
    /// </summary>
    [TestCase(ChessBehaviorTreeLogic.HasTarget)]
    [TestCase(ChessBehaviorTreeLogic.IsTargetInRange)]
    [TestCase(ChessBehaviorTreeLogic.IsSelfInTargetRange)]
    [TestCase(ChessBehaviorTreeLogic.IsTargetDead)]
    [TestCase(ChessBehaviorTreeLogic.IsAttackCDReady)]
    [TestCase(ChessBehaviorTreeLogic.IsRangeGreaterThanTarget)]
    public void MissingOwnerFailsAllConditionals(string logicId)
    {
        Assert.That(TickConditional(logicId, null), Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证无索敌目标时目标相关 Conditional 全部失败。
    /// </summary>
    [TestCase(ChessBehaviorTreeLogic.HasTarget)]
    [TestCase(ChessBehaviorTreeLogic.IsTargetInRange)]
    [TestCase(ChessBehaviorTreeLogic.IsSelfInTargetRange)]
    [TestCase(ChessBehaviorTreeLogic.IsTargetDead)]
    [TestCase(ChessBehaviorTreeLogic.IsRangeGreaterThanTarget)]
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
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsAttackCDReady, _owner),
            Is.EqualTo(NodeStatus.Success));
    }

    /// <summary>
    /// 验证攻击冷却 Conditional 不要求存在索敌目标。
    /// </summary>
    [Test]
    public void IsAttackCDReadySucceedsWithoutTargetWhenCooldownReady()
    {
        SelectTarget(_owner, null);

        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsAttackCDReady, _owner),
            Is.EqualTo(NodeStatus.Success));
    }

    /// <summary>
    /// 验证缺少攻击控制器时攻击冷却 Conditional 失败。
    /// </summary>
    [Test]
    public void IsAttackCDReadyFailsWhenAttackControllerMissing()
    {
        typeof(ChessController)
            .GetField("_attackController", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_owner, null);

        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsAttackCDReady, _owner),
            Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证冷却中攻击冷却 Conditional 失败，前摇期间冷却为 0 时成功。
    /// </summary>
    [Test]
    public void IsAttackCDReadyMatchesCooldownAndWindUp()
    {
        ChessBaseData windUpConfig = CreateConfig(100, 5f);
        windUpConfig.WindUpTime = 0.5f;
        _owner = new ChessController(windUpConfig);
        _owner.RuntimeData.CurrentPosition = Vector2.zero;
        SelectTarget(_owner, _target);
        _owner.AttackController.AttackTarget();
        Assert.That(_owner.AttackController.IsWindingUp, Is.True);
        Assert.That(_owner.AttackController.CooldownRemaining, Is.EqualTo(0f));
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsAttackCDReady, _owner),
            Is.EqualTo(NodeStatus.Success));

        _owner = new ChessController(CreateConfig(100, 5f));
        _owner.RuntimeData.CurrentPosition = Vector2.zero;
        SelectTarget(_owner, _target);
        _owner.AttackController.AttackTarget();
        Assert.That(_owner.AttackController.IsWindingUp, Is.False);
        Assert.That(_owner.AttackController.CooldownRemaining, Is.GreaterThan(0f));
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsAttackCDReady, _owner),
            Is.EqualTo(NodeStatus.Failure));
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
    /// 验证自己当前射程大于目标时 IsRangeGreaterThanTarget 成功。
    /// </summary>
    [Test]
    public void IsRangeGreaterThanTargetSucceedsWhenOwnerRangeIsLarger()
    {
        Assert.That(
            _owner.RuntimeData.GetCurrentRange(_owner.BaseData.Range),
            Is.GreaterThan(_target.RuntimeData.GetCurrentRange(_target.BaseData.Range)));
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsRangeGreaterThanTarget, _owner),
            Is.EqualTo(NodeStatus.Success));
    }

    /// <summary>
    /// 验证双方当前射程相等时 IsRangeGreaterThanTarget 失败。
    /// </summary>
    [Test]
    public void IsRangeGreaterThanTargetFailsWhenRangesAreEqual()
    {
        _target.RuntimeData.CurrentRangeIncrement = 1f;

        Assert.That(
            _owner.RuntimeData.GetCurrentRange(_owner.BaseData.Range),
            Is.EqualTo(_target.RuntimeData.GetCurrentRange(_target.BaseData.Range)));
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsRangeGreaterThanTarget, _owner),
            Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证自己当前射程小于目标时 IsRangeGreaterThanTarget 失败。
    /// </summary>
    [Test]
    public void IsRangeGreaterThanTargetFailsWhenOwnerRangeIsSmaller()
    {
        _target.RuntimeData.CurrentRangeIncrement = 2f;

        Assert.That(
            _owner.RuntimeData.GetCurrentRange(_owner.BaseData.Range),
            Is.LessThan(_target.RuntimeData.GetCurrentRange(_target.BaseData.Range)));
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsRangeGreaterThanTarget, _owner),
            Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证 IsRangeGreaterThanTarget 使用双方运行时射程乘区和增量。
    /// </summary>
    [Test]
    public void IsRangeGreaterThanTargetUsesCurrentRangeModifiers()
    {
        _owner.RuntimeData.CurrentRangeMultipler = 0.5f;
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsRangeGreaterThanTarget, _owner),
            Is.EqualTo(NodeStatus.Failure));

        _owner.RuntimeData.CurrentRangeIncrement = 1.6f;
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsRangeGreaterThanTarget, _owner),
            Is.EqualTo(NodeStatus.Success));

        _target.RuntimeData.CurrentRangeMultipler = 1.5f;
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsRangeGreaterThanTarget, _owner),
            Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证死亡目标仍按当前射程比较，且不要求目标存活。
    /// </summary>
    [Test]
    public void IsRangeGreaterThanTargetStillComparesDeadTargetRange()
    {
        _target.RuntimeData.Hp = 0;

        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsRangeGreaterThanTarget, _owner),
            Is.EqualTo(NodeStatus.Success));

        _target.RuntimeData.CurrentRangeIncrement = 2f;
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsRangeGreaterThanTarget, _owner),
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
    /// 验证逻辑目录包含棋子 Conditional，且重复 Ensure 不会抛异常。
    /// </summary>
    [Test]
    public void CatalogContainsChessConditionalsAndEnsureIsIdempotent()
    {
        Assert.DoesNotThrow(ChessBehaviorTreeLogic.EnsureCatalogRegistered);

        AssertCatalogEntry(ChessBehaviorTreeLogic.HasTarget, "当前存在目标");
        AssertCatalogEntry(ChessBehaviorTreeLogic.IsTargetInRange, "目标在自己射程内");
        AssertCatalogEntry(ChessBehaviorTreeLogic.IsSelfInTargetRange, "自己在目标射程内");
        AssertCatalogEntry(ChessBehaviorTreeLogic.IsTargetDead, "当前目标已死亡");
        AssertCatalogEntry(ChessBehaviorTreeLogic.IsAttackCDReady, "攻击冷却已就绪");
        AssertCatalogEntry(ChessBehaviorTreeLogic.IsRangeGreaterThanTarget, "自己射程大于目标射程");
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
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.IsRangeGreaterThanTarget, _owner),
            Is.EqualTo(NodeStatus.Success));
    }

    /// <summary>
    /// 验证 Invert=true 时 HasTarget 对无目标和有目标结果取反。
    /// </summary>
    [Test]
    public void InvertHasTargetFlipsMissingAndSelectedTarget()
    {
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.HasTarget, _owner, true),
            Is.EqualTo(NodeStatus.Failure));

        SelectTarget(_owner, null);
        Assert.That(
            TickConditional(ChessBehaviorTreeLogic.HasTarget, _owner, true),
            Is.EqualTo(NodeStatus.Success));
    }

    /// <summary>
    /// 使用指定 LogicId 构建单节点行为树并推进一次。
    /// </summary>
    private static NodeStatus TickConditional(string logicId, ChessController owner, bool invert = false)
    {
        var registry = new BehaviorTreeRegistry();
        ChessBehaviorTreeLogic.Register(registry);
        var root = new RootNodeData();
        var data = new ConditionalNodeData(logicId);
        if (invert)
        {
            data.Parameters[ConditionalNode.InvertParameterKey] = true;
        }

        root.AddChild(data);
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
        Assert.That(definition.Parameters.Count, Is.EqualTo(1));
        Assert.That(definition.Parameters[0].Key, Is.EqualTo(ConditionalNode.InvertParameterKey));
        Assert.That(definition.Parameters[0].Type, Is.EqualTo(BehaviorTreeParameterType.Bool));
        Assert.That(definition.Parameters[0].DisplayName, Is.EqualTo("取反"));
        Assert.That(definition.Parameters[0].DefaultBool, Is.False);
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
