#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using AutoChess;
using AutoChess.Editor;
using AutoChess.Navigation;
using BehaviorTree;
using Camp;
using Chess;
using Chess.Behavior;
using Chess.FSM.State;
using Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 覆盖棋子风筝行为树图结构、资产编译和决策分支。
/// </summary>
public class ChessCombatBehaviorTreeGraphTests
{
    /// <summary>由 builder 编译得到的风筝行为树定义。</summary>
    private BehaviorTreeDefinition _definition;

    /// <summary>移动相关测试使用的导航管理器。</summary>
    private NavigationManager _navigation;

    /// <summary>
    /// 编译风筝行为树定义。
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        ChessBehaviorTreeLogic.EnsureCatalogRegistered();
        Assert.That(
            ChessCombatBehaviorTreeGraphBuilder.TryBuildDefinition(
                out _definition,
                out List<string> errors),
            Is.True,
            string.Join("\n", errors));
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
    /// 验证 builder 编译出的节点顺序、LogicId 和 SelectTarget Mode。
    /// </summary>
    [Test]
    public void BuilderCompilesExpectedKitingTree()
    {
        AssertCombatTreeStructure(_definition);
    }

    /// <summary>
    /// 验证提交的 ChessCombat 资产与 builder 结构一致。
    /// </summary>
    [Test]
    public void CommittedAssetMatchesBuilderDefinition()
    {
        var graph = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(
            ChessCombatBehaviorTreeGraphBuilder.AssetPath);
        Assert.That(graph, Is.Not.Null, "缺少 " + ChessCombatBehaviorTreeGraphBuilder.AssetPath);

        Assert.That(
            BehaviorTreeGraphCompiler.TryBuildDefinition(
                graph,
                out BehaviorTreeDefinition definition,
                out List<string> errors),
            Is.True,
            string.Join("\n", errors));
        AssertCombatTreeStructure(definition);
    }

    /// <summary>
    /// 验证无目标时根选择器回退到索敌。
    /// </summary>
    [Test]
    public void NoTargetSelectsNearestLivingEnemy()
    {
        CampManager camps = CreateCamps();
        ChessController owner = camps.RegisterChess(1, 1, Vector2.zero);
        ChessController close = camps.RegisterChess(2, 1, new Vector2(3f, 0f));
        camps.RegisterChess(2, 1, new Vector2(20f, 0f));

        Assert.That(TickCombat(owner), Is.EqualTo(NodeStatus.Success));
        Assert.That(owner.TargetSelector.SelectedChessController, Is.SameAs(close));
    }

    /// <summary>
    /// 验证当前目标死亡时重新索敌并跳过尸体。
    /// </summary>
    [Test]
    public void DeadTargetReselectsLivingEnemy()
    {
        CampManager camps = CreateCamps();
        ChessController owner = camps.RegisterChess(1, 1, Vector2.zero);
        ChessController dead = camps.RegisterChess(2, 1, new Vector2(3f, 0f));
        ChessController living = camps.RegisterChess(2, 1, new Vector2(20f, 0f));
        dead.RuntimeData.Hp = 0;
        dead.Fsm.CheckCurrentStateTransition();
        SetSelected(owner, dead);

        Assert.That(TickCombat(owner), Is.EqualTo(NodeStatus.Success));
        Assert.That(owner.TargetSelector.SelectedChessController, Is.SameAs(living));
    }

    /// <summary>
    /// 验证冷却就绪且目标在自己射程内时发起攻击。
    /// </summary>
    [Test]
    public void ReadyAttackStartsWhenTargetInRange()
    {
        CombatPair pair = CreateCombatPair(new Vector2(3f, 0f), 8f);
        BindMovement(pair.Owner);

        Assert.That(TickCombat(pair.Owner), Is.EqualTo(NodeStatus.Success));
        Assert.That(pair.Owner.AttackController.IsWindingUp, Is.False);
        Assert.That(pair.Owner.AttackController.CooldownRemaining, Is.GreaterThan(0f));
        Assert.That(pair.Target.RuntimeData.Hp, Is.LessThan(pair.Target.BaseData.MaxHp));
    }

    /// <summary>
    /// 验证冷却未就绪且自己在目标射程内时逃离。
    /// </summary>
    [Test]
    public void CooldownNotReadyAndThreatenedFlees()
    {
        CombatPair pair = CreateCombatPair(new Vector2(3f, 0f), 8f);
        BindMovement(pair.Owner);
        pair.Owner.AttackController.AttackTarget();
        Assert.That(pair.Owner.AttackController.CooldownRemaining, Is.GreaterThan(0f));

        Assert.That(TickCombat(pair.Owner), Is.EqualTo(NodeStatus.Success));
        Assert.That(pair.Owner.MovementController.HasDestination, Is.True);
        Assert.That(pair.Owner.MovementController.TargetChess, Is.SameAs(pair.Target));
        Assert.That(pair.Owner.Fsm.CurrentState, Is.TypeOf<MovementState>());
    }

    /// <summary>
    /// 验证冷却未就绪、自己打得到且不被威胁时停止移动。
    /// </summary>
    [Test]
    public void CooldownNotReadyAndSafeInRangeStops()
    {
        CombatPair pair = CreateCombatPair(new Vector2(3f, 0f), 1f);
        BindMovement(pair.Owner);
        pair.Owner.AttackController.AttackTarget();
        Assert.That(pair.Owner.AttackController.CooldownRemaining, Is.GreaterThan(0f));
        Assert.That(pair.Owner.MovementController.MoveToTarget(pair.Target), Is.True);
        Assert.That(pair.Owner.MovementController.HasDestination, Is.True);

        Assert.That(TickCombat(pair.Owner), Is.EqualTo(NodeStatus.Success));
        Assert.That(pair.Owner.MovementController.HasDestination, Is.False);
    }

    /// <summary>
    /// 验证冷却未就绪且目标不在自己射程内时追击。
    /// </summary>
    [Test]
    public void CooldownNotReadyAndOutOfRangeChases()
    {
        CombatPair pair = CreateCombatPair(new Vector2(3f, 0f), 1f);
        BindMovement(pair.Owner);
        pair.Owner.AttackController.AttackTarget();
        Assert.That(pair.Owner.AttackController.CooldownRemaining, Is.GreaterThan(0f));
        pair.Target.RuntimeData.CurrentPosition = new Vector2(20f, 0f);

        Assert.That(TickCombat(pair.Owner), Is.EqualTo(NodeStatus.Success));
        Assert.That(pair.Owner.MovementController.HasDestination, Is.True);
        Assert.That(pair.Owner.MovementController.TargetChess, Is.SameAs(pair.Target));
    }

    /// <summary>
    /// 验证冷却就绪但攻击失败时落入走位并追击。
    /// </summary>
    [Test]
    public void ReadyAttackFallsThroughToChaseWhenOutOfRange()
    {
        CombatPair pair = CreateCombatPair(new Vector2(20f, 0f), 1f);
        BindMovement(pair.Owner);

        Assert.That(pair.Owner.AttackController.CooldownRemaining, Is.EqualTo(0f));
        Assert.That(TickCombat(pair.Owner), Is.EqualTo(NodeStatus.Success));
        Assert.That(pair.Owner.AttackController.IsAttackInProgress, Is.False);
        Assert.That(pair.Owner.MovementController.HasDestination, Is.True);
        Assert.That(pair.Owner.MovementController.TargetChess, Is.SameAs(pair.Target));
    }

    /// <summary>
    /// 断言风筝树的节点类型、LogicId 和 SelectTarget Mode。
    /// </summary>
    private static void AssertCombatTreeStructure(BehaviorTreeDefinition definition)
    {
        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.Root, Is.Not.Null);
        SelectorNodeData rootSelector = Child<SelectorNodeData>(definition.Root, 0);
        Assert.That(rootSelector.Children.Count, Is.EqualTo(3));

        SequenceNodeData retarget = Child<SequenceNodeData>(rootSelector, 0);
        AssertLogic(Child<ConditionalNodeData>(retarget, 0), ChessBehaviorTreeLogic.IsTargetDead);
        AssertSelectTarget(Child<ActionNodeData>(retarget, 1));

        SequenceNodeData combat = Child<SequenceNodeData>(rootSelector, 1);
        AssertLogic(Child<ConditionalNodeData>(combat, 0), ChessBehaviorTreeLogic.HasTarget);
        SelectorNodeData combatSelector = Child<SelectorNodeData>(combat, 1);
        Assert.That(combatSelector.Children.Count, Is.EqualTo(2));

        SequenceNodeData attack = Child<SequenceNodeData>(combatSelector, 0);
        AssertLogic(Child<ConditionalNodeData>(attack, 0), ChessBehaviorTreeLogic.IsAttackCDReady);
        AssertLogic(Child<ActionNodeData>(attack, 1), ChessBehaviorTreeLogic.AttackTarget);

        SelectorNodeData kiting = Child<SelectorNodeData>(combatSelector, 1);
        SequenceNodeData flee = Child<SequenceNodeData>(kiting, 0);
        AssertLogic(Child<ConditionalNodeData>(flee, 0), ChessBehaviorTreeLogic.IsSelfInTargetRange);
        AssertLogic(Child<ActionNodeData>(flee, 1), ChessBehaviorTreeLogic.MoveAwayFromTarget);

        SelectorNodeData holdOrChase = Child<SelectorNodeData>(kiting, 1);
        SequenceNodeData hold = Child<SequenceNodeData>(holdOrChase, 0);
        AssertLogic(Child<ConditionalNodeData>(hold, 0), ChessBehaviorTreeLogic.IsTargetInRange);
        AssertLogic(Child<ActionNodeData>(hold, 1), ChessBehaviorTreeLogic.StopMovement);
        AssertLogic(Child<ActionNodeData>(holdOrChase, 1), ChessBehaviorTreeLogic.MoveToTarget);

        AssertSelectTarget(Child<ActionNodeData>(rootSelector, 2));
    }

    /// <summary>
    /// 读取指定类型的子节点。
    /// </summary>
    private static T Child<T>(NodeData parent, int index) where T : NodeData
    {
        Assert.That(parent, Is.Not.Null);
        Assert.That(parent.Children, Is.Not.Null);
        Assert.That(parent.Children.Count, Is.GreaterThan(index));
        Assert.That(parent.Children[index], Is.InstanceOf<T>());
        return (T)parent.Children[index];
    }

    /// <summary>
    /// 断言叶节点 LogicId。
    /// </summary>
    private static void AssertLogic(NodeData node, string logicId)
    {
        var action = node as ActionNodeData;
        if (action != null)
        {
            Assert.That(action.LogicId, Is.EqualTo(logicId));
            return;
        }

        var conditional = node as ConditionalNodeData;
        Assert.That(conditional, Is.Not.Null);
        Assert.That(conditional.LogicId, Is.EqualTo(logicId));
    }

    /// <summary>
    /// 断言 SelectTarget 的 Mode 为最近。
    /// </summary>
    private static void AssertSelectTarget(ActionNodeData action)
    {
        Assert.That(action.LogicId, Is.EqualTo(ChessBehaviorTreeLogic.SelectTarget));
        Assert.That(action.Parameters.ContainsKey(ChessBehaviorTreeLogic.SelectTargetModeParameter), Is.True);
        Assert.That(
            Convert.ToInt32(action.Parameters[ChessBehaviorTreeLogic.SelectTargetModeParameter]),
            Is.EqualTo((int)ChessTargetSelector.TargetSelectMode.Nearest));
    }

    /// <summary>
    /// 推进一次风筝行为树。
    /// </summary>
    private NodeStatus TickCombat(ChessController owner)
    {
        var registry = new BehaviorTreeRegistry();
        ChessBehaviorTreeLogic.Register(registry);
        var runner = new BehaviorTreeRunner(_definition, registry, new Blackboard(), owner);
        runner.Start();
        return runner.Tick();
    }

    /// <summary>
    /// 创建带默认配置的双方棋子，并把目标设为当前索敌目标。
    /// </summary>
    private static CombatPair CreateCombatPair(Vector2 targetPosition, float targetRange)
    {
        CampManager camps = CreateCamps();
        ChessController owner = camps.RegisterChess(1, 1, Vector2.zero);
        ChessController target = camps.RegisterChess(2, 1, targetPosition);
        target.BaseData.Range = targetRange;
        SetSelected(owner, target);
        return new CombatPair(owner, target);
    }

    /// <summary>
    /// 创建已注册棋子配置的阵营管理器。
    /// </summary>
    private static CampManager CreateCamps()
    {
        var configs = new ChessConfigRepository();
        configs.Register(CreateConfig(1, 5f, 0f));
        return new CampManager(configs);
    }

    /// <summary>
    /// 绑定己方导航代理。
    /// </summary>
    private void BindMovement(ChessController owner)
    {
        var board = new ChessBoard { Width = 40, Height = 40 };
        _navigation = new NavigationManager(board);
        NavigationAgentHandle agent = _navigation.RegisterAgent(
            owner.RuntimeData.CurrentPosition,
            ChessMovementController.ChessRadius,
            2f);
        owner.InitializeMovement(board, agent);
    }

    /// <summary>
    /// 创建测试用棋子配置。
    /// </summary>
    private static ChessBaseData CreateConfig(int configId, float range, float windUpTime)
    {
        return new ChessBaseData
        {
            ConfigId = configId,
            Name = "CombatTree",
            MaxHp = 100,
            Atk = 5,
            Range = range,
            MoveSpeed = 2f,
            AtkSpeed = 2f,
            WindUpTime = windUpTime,
            BulletSpeed = 0f
        };
    }

    /// <summary>
    /// 测试侧设置当前索敌目标。
    /// </summary>
    private static void SetSelected(ChessController owner, ChessController target)
    {
        PropertyInfo property = typeof(ChessTargetSelector).GetProperty(
            nameof(ChessTargetSelector.SelectedChessController));
        property.GetSetMethod(true).Invoke(owner.TargetSelector, new object[] { target });
    }

    /// <summary>
    /// 战斗测试使用的己方与目标。
    /// </summary>
    private readonly struct CombatPair
    {
        /// <summary>己方棋子。</summary>
        public ChessController Owner { get; }

        /// <summary>当前目标。</summary>
        public ChessController Target { get; }

        /// <summary>创建一对测试棋子。</summary>
        public CombatPair(ChessController owner, ChessController target)
        {
            Owner = owner;
            Target = target;
        }
    }
}
#endif
