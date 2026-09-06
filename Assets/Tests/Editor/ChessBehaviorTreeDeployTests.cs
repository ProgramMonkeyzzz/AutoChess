#if UNITY_EDITOR
using AutoChess.Utils;
using BehaviorTree;
using Camp;
using Chess;
using Chess.Behavior;
using Chess.FSM.State;
using Config;
using NUnit.Framework;
using UnityEngine;

/// <summary>验证创建棋子时按配置名部署 9 槽状态行为树。</summary>
public class ChessBehaviorTreeDeployTests
{
    [TearDown]
    public void TearDown()
    {
        DrainLogs();
    }

    /// <summary>验证空名或空白名不会挂树，也不写错误日志。</summary>
    [Test]
    public void EmptyNamesLeaveAllBehaviorTreesNull()
    {
        DrainLogs();
        var trees = new BehaviorTreeRepository();
        trees.Register("StayTree", CreateActionDefinition(ChessBehaviorTreeLogic.StopMovement));
        ChessBaseData config = CreateConfig(1);
        config.MovementStayBehaviorTree = "   ";
        CampManager camps = CreateCamps(config, trees);

        ChessController chess = camps.RegisterChess(1, 1, Vector2.zero);

        Assert.That(chess, Is.Not.Null);
        AssertNoBehaviorTrees(chess);
        Assert.That(ThreadLogQueue.TryDequeue(out _), Is.False);
    }

    /// <summary>验证命中配置名后会创建独立 Runner，且 Owner 为该棋子。</summary>
    [Test]
    public void NamedTreesCreateRunnersOwnedByChess()
    {
        DrainLogs();
        var trees = new BehaviorTreeRepository();
        BehaviorTreeDefinition stay = CreateActionDefinition(ChessBehaviorTreeLogic.StopMovement);
        BehaviorTreeDefinition attackEnter = CreateActionDefinition(ChessBehaviorTreeLogic.StopMovement);
        trees.Register("StayTree", stay);
        trees.Register("AtkEnter", attackEnter);
        ChessBaseData config = CreateConfig(1);
        config.MovementStayBehaviorTree = " StayTree ";
        config.AttackEnterBehaviorTree = "AtkEnter";
        CampManager camps = CreateCamps(config, trees);

        ChessController chess = camps.RegisterChess(1, 1, Vector2.zero);

        Assert.That(chess, Is.Not.Null);
        MovementState movement = chess.Fsm.GetState<MovementState>();
        AttackState attack = chess.Fsm.GetState<AttackState>();
        DeadState dead = chess.Fsm.GetState<DeadState>();
        Assert.That(movement.EnterBehaviorTree, Is.Null);
        Assert.That(movement.StayBehaviorTree, Is.Not.Null);
        Assert.That(movement.StayBehaviorTree.Context.Owner, Is.SameAs(chess));
        Assert.That(movement.ExitBehaviorTree, Is.Null);
        Assert.That(attack.EnterBehaviorTree, Is.Not.Null);
        Assert.That(attack.EnterBehaviorTree.Context.Owner, Is.SameAs(chess));
        Assert.That(attack.StayBehaviorTree, Is.Null);
        Assert.That(dead.EnterBehaviorTree, Is.Null);
        Assert.That(ThreadLogQueue.TryDequeue(out _), Is.False);
    }

    /// <summary>验证找不到树名时跳过该槽、记录错误，棋子仍创建成功。</summary>
    [Test]
    public void MissingTreeNameSkipsSlotAndLogsError()
    {
        DrainLogs();
        ChessBaseData config = CreateConfig(1);
        config.MovementStayBehaviorTree = "MissingTree";
        CampManager camps = CreateCamps(config, new BehaviorTreeRepository());

        ChessController chess = camps.RegisterChess(1, 1, Vector2.zero);

        Assert.That(chess, Is.Not.Null);
        Assert.That(chess.Fsm.GetState<MovementState>().StayBehaviorTree, Is.Null);
        Assert.That(ThreadLogQueue.TryDequeue(out ThreadLogRecord record), Is.True);
        Assert.That(record.Level, Is.EqualTo(ThreadLogLevel.Error));
        Assert.That(record.Message, Does.Contain("MissingTree"));
    }

    /// <summary>验证构建失败时跳过该槽、记录错误，棋子仍创建成功。</summary>
    [Test]
    public void InvalidTreeDefinitionSkipsSlotAndLogsError()
    {
        DrainLogs();
        var trees = new BehaviorTreeRepository();
        trees.Register("BadTree", CreateActionDefinition("NotRegisteredLogic"));
        ChessBaseData config = CreateConfig(1);
        config.AttackStayBehaviorTree = "BadTree";
        CampManager camps = CreateCamps(config, trees);

        ChessController chess = camps.RegisterChess(1, 1, Vector2.zero);

        Assert.That(chess, Is.Not.Null);
        Assert.That(chess.Fsm.GetState<AttackState>().StayBehaviorTree, Is.Null);
        Assert.That(ThreadLogQueue.TryDequeue(out ThreadLogRecord record), Is.True);
        Assert.That(record.Level, Is.EqualTo(ThreadLogLevel.Error));
        Assert.That(record.Message, Does.Contain("BadTree"));
    }

    /// <summary>验证无仓库时非空树名只打日志、不挂树，公开构造函数仍立即进入 MovementState。</summary>
    [Test]
    public void PublicConstructorWithoutRepositoryLogsAndStartsImmediately()
    {
        DrainLogs();
        ChessBaseData config = CreateConfig(1);
        config.MovementStayBehaviorTree = "StayTree";

        var chess = new ChessController(config);

        Assert.That(chess.Fsm.CurrentState, Is.InstanceOf<MovementState>());
        Assert.That(chess.Fsm.GetState<MovementState>().StayBehaviorTree, Is.Null);
        Assert.That(ThreadLogQueue.TryDequeue(out ThreadLogRecord record), Is.True);
        Assert.That(record.Level, Is.EqualTo(ThreadLogLevel.Error));
        Assert.That(record.Message, Does.Contain("StayTree"));
    }

    /// <summary>验证工厂在写入阵营上下文之后才执行 Movement Enter 行为树。</summary>
    [Test]
    public void FactoryStartsFsmAfterCampContextSoEnterCanSelectTarget()
    {
        DrainLogs();
        var trees = new BehaviorTreeRepository();
        trees.Register("SelectNearest", CreateActionDefinition(ChessBehaviorTreeLogic.SelectTarget));
        ChessBaseData config = CreateConfig(1);
        config.MovementEnterBehaviorTree = "SelectNearest";
        CampManager camps = CreateCamps(config, trees);
        ChessController enemy = camps.RegisterChess(2, 1, new Vector2(3f, 0f));

        ChessController owner = camps.RegisterChess(1, 1, Vector2.zero);

        Assert.That(owner.TargetSelector.SelectedChessController, Is.SameAs(enemy));
        Assert.That(owner.Fsm.GetState<MovementState>().EnterBehaviorTree, Is.Not.Null);
        Assert.That(owner.Fsm.GetState<MovementState>().EnterBehaviorTree.Context.Owner, Is.SameAs(owner));
    }

    /// <summary>验证 CreateRunner 使用独立黑板且 Owner 为棋子。</summary>
    [Test]
    public void CreateRunnerBindsOwnerAndFreshBlackboard()
    {
        var chess = new ChessController(CreateConfig(1));
        BehaviorTreeRunner runner = ChessBehaviorTreeLogic.CreateRunner(
            CreateActionDefinition(ChessBehaviorTreeLogic.StopMovement),
            chess);

        Assert.That(runner.Context.Owner, Is.SameAs(chess));
        Assert.That(runner.Context.Blackboard, Is.Not.Null);
        runner.Start();
        Assert.That(runner.Tick(), Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>断言三状态九个行为树槽位都为空。</summary>
    private static void AssertNoBehaviorTrees(ChessController chess)
    {
        MovementState movement = chess.Fsm.GetState<MovementState>();
        AttackState attack = chess.Fsm.GetState<AttackState>();
        DeadState dead = chess.Fsm.GetState<DeadState>();
        Assert.That(movement.EnterBehaviorTree, Is.Null);
        Assert.That(movement.StayBehaviorTree, Is.Null);
        Assert.That(movement.ExitBehaviorTree, Is.Null);
        Assert.That(attack.EnterBehaviorTree, Is.Null);
        Assert.That(attack.StayBehaviorTree, Is.Null);
        Assert.That(attack.ExitBehaviorTree, Is.Null);
        Assert.That(dead.EnterBehaviorTree, Is.Null);
        Assert.That(dead.StayBehaviorTree, Is.Null);
        Assert.That(dead.ExitBehaviorTree, Is.Null);
    }

    /// <summary>创建只含一个 Action 的行为树定义。</summary>
    private static BehaviorTreeDefinition CreateActionDefinition(string logicId)
    {
        var root = new RootNodeData();
        root.AddChild(new ActionNodeData(logicId));
        return new BehaviorTreeDefinition(root);
    }

    /// <summary>创建已注册配置和行为树仓库的阵营管理器。</summary>
    private static CampManager CreateCamps(ChessBaseData config, BehaviorTreeRepository trees)
    {
        var configs = new ChessConfigRepository();
        configs.Register(config);
        return new CampManager(configs, trees);
    }

    /// <summary>创建测试用棋子配置。</summary>
    private static ChessBaseData CreateConfig(int configId)
    {
        return new ChessBaseData
        {
            ConfigId = configId,
            Name = "Config" + configId,
            MaxHp = 100,
            Atk = 1,
            Range = 8f,
            MoveSpeed = 1f,
            AtkSpeed = 1f,
            WindUpTime = 0.1f,
            BulletSpeed = 0f
        };
    }

    /// <summary>清空线程日志队列，避免测试互相污染。</summary>
    private static void DrainLogs()
    {
        while (ThreadLogQueue.TryDequeue(out _))
        {
        }
    }
}
#endif
