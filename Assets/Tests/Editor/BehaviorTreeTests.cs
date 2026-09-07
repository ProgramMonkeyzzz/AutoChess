#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using AutoChess.Utils;
using BehaviorTree;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

/// <summary>
/// 覆盖纯 C# 行为树节点、黑板、运行器和二进制定义的 EditMode 测试。
/// </summary>
public class BehaviorTreeTests
{
    /// <summary>每个测试使用的独立行为树上下文。</summary>
    private BehaviorTreeContext _context;

    /// <summary>
    /// 为每个测试创建新的黑板和上下文。
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _context = new BehaviorTreeContext(new Blackboard());
    }

    /// <summary>
    /// 验证黑板支持同一声明类型的写入、更新和读取。
    /// </summary>
    [Test]
    public void BlackboardReadsAndUpdatesStronglyTypedValues()
    {
        var blackboard = new Blackboard();

        blackboard.Set("Count", 1);
        object initialStoredValue = GetBlackboardStoredValue(blackboard, "Count");
        blackboard.Set("Count", 2);
        object updatedStoredValue = GetBlackboardStoredValue(blackboard, "Count");

        Assert.That(blackboard.Contains("Count"), Is.True);
        Assert.That(initialStoredValue, Is.TypeOf<ValueTypeWrap<int>>());
        Assert.That(updatedStoredValue, Is.SameAs(initialStoredValue));
        Assert.That(blackboard.TryGet("Count", out int value), Is.True);
        Assert.That(value, Is.EqualTo(2));
        Assert.That(blackboard.Get<int>("Count"), Is.EqualTo(2));
    }

    /// <summary>
    /// 验证黑板拒绝空 key 和同一 key 的跨类型覆盖。
    /// </summary>
    [Test]
    public void BlackboardRejectsInvalidKeysAndTypeChanges()
    {
        var blackboard = new Blackboard();
        blackboard.Set("Value", 1);

        Assert.Throws<ArgumentException>(() => blackboard.Set(" ", 1));
        Assert.Throws<InvalidOperationException>(() => blackboard.Set("Value", "1"));
        Assert.That(blackboard.TryGet("Value", out string _), Is.False);
        Assert.Throws<InvalidCastException>(() => blackboard.Get<string>("Value"));
        Assert.Throws<KeyNotFoundException>(() => blackboard.Get<int>("Missing"));
    }

    /// <summary>
    /// 验证黑板可以删除单个变量并清空全部变量。
    /// </summary>
    [Test]
    public void BlackboardRemovesAndClearsValues()
    {
        var blackboard = new Blackboard();
        blackboard.Set("First", 1);
        blackboard.Set("Second", 2);

        Assert.That(blackboard.Remove("First"), Is.True);
        Assert.That(blackboard.Contains("First"), Is.False);

        blackboard.Clear();

        Assert.That(blackboard.Contains("Second"), Is.False);
        Assert.That(blackboard.Remove("Second"), Is.False);
    }

    /// <summary>
    /// 验证黑板包装普通值类型，同时保持引用类型和可空值类型的原有存储语义。
    /// </summary>
    [Test]
    public void BlackboardWrapsValueTypesAndPreservesReferenceAndNullableValues()
    {
        var blackboard = new Blackboard();
        var referenceValue = new object();

        blackboard.Set("State", BlackboardTestState.Ready);
        blackboard.Set("Reference", referenceValue);
        blackboard.Set<int?>("Nullable", 3);

        Assert.That(
            GetBlackboardStoredValue(blackboard, "State"),
            Is.TypeOf<ValueTypeWrap<BlackboardTestState>>());
        Assert.That(GetBlackboardStoredValue(blackboard, "Reference"), Is.SameAs(referenceValue));
        Assert.That(GetBlackboardStoredValue(blackboard, "Nullable"), Is.TypeOf<int>());
        Assert.That(blackboard.Get<BlackboardTestState>("State"), Is.EqualTo(BlackboardTestState.Ready));
        Assert.That(blackboard.Get<int?>("Nullable"), Is.EqualTo(3));

        var updatedReferenceValue = new object();
        blackboard.Set("Reference", updatedReferenceValue);
        blackboard.Set<int?>("Nullable", null);

        Assert.That(GetBlackboardStoredValue(blackboard, "Reference"), Is.SameAs(updatedReferenceValue));
        Assert.That(blackboard.Get<object>("Reference"), Is.SameAs(updatedReferenceValue));
        Assert.That(GetBlackboardStoredValue(blackboard, "Nullable"), Is.Null);
        Assert.That(blackboard.TryGet("Nullable", out int? nullableValue), Is.True);
        Assert.That(nullableValue, Is.Null);
    }

    /// <summary>
    /// 验证删除和清空黑板时会重置并回收值类型包装对象。
    /// </summary>
    [Test]
    public void BlackboardRemoveAndClearRecycleValueTypeWraps()
    {
        var blackboard = new Blackboard();
        blackboard.Set("Removed", new RemovedBlackboardValue(7));
        var removedWrap =
            (ValueTypeWrap<RemovedBlackboardValue>)GetBlackboardStoredValue(blackboard, "Removed");

        Assert.That(blackboard.Remove("Removed"), Is.True);
        Assert.That(removedWrap.Value.Number, Is.Zero);

        ValueTypeWrap<RemovedBlackboardValue> recycledRemovedWrap =
            ObjectPool.Fetch<ValueTypeWrap<RemovedBlackboardValue>>();
        Assert.That(recycledRemovedWrap, Is.SameAs(removedWrap));
        recycledRemovedWrap.Dispose();

        blackboard.Set("Cleared", new ClearedBlackboardValue(9));
        var clearedWrap =
            (ValueTypeWrap<ClearedBlackboardValue>)GetBlackboardStoredValue(blackboard, "Cleared");

        blackboard.Clear();

        Assert.That(clearedWrap.Value.Number, Is.Zero);
        Assert.That(blackboard.Contains("Cleared"), Is.False);

        ValueTypeWrap<ClearedBlackboardValue> recycledClearedWrap =
            ObjectPool.Fetch<ValueTypeWrap<ClearedBlackboardValue>>();
        Assert.That(recycledClearedWrap, Is.SameAs(clearedWrap));
        recycledClearedWrap.Dispose();
    }

    /// <summary>
    /// 验证 Action 的 Running、Success 和生命周期只按约定触发。
    /// </summary>
    [Test]
    public void ActionSupportsThreeResultsAndLifecycle()
    {
        var action = new ScriptedAction(ActionResult.Running, ActionResult.Success);

        Assert.That(action.Tick(_context), Is.EqualTo(NodeStatus.Running));
        Assert.That(action.StartCount, Is.EqualTo(1));
        Assert.That(action.StopCount, Is.Zero);

        Assert.That(action.Tick(_context), Is.EqualTo(NodeStatus.Success));
        Assert.That(action.UpdateCount, Is.EqualTo(2));
        Assert.That(action.StopCount, Is.EqualTo(1));

        Assert.That(action.Tick(_context), Is.EqualTo(NodeStatus.Success));
        Assert.That(action.UpdateCount, Is.EqualTo(2));

        action.Reset();
        Assert.That(action.Status, Is.EqualTo(NodeStatus.Idle));
        Assert.That(action.ResetCount, Is.EqualTo(1));
    }

    /// <summary>
    /// 验证 Action 可以明确返回失败。
    /// </summary>
    [Test]
    public void ActionCanReturnFailure()
    {
        var action = new ScriptedAction(ActionResult.Failure);

        Assert.That(action.Tick(_context), Is.EqualTo(NodeStatus.Failure));
        Assert.That(action.StopCount, Is.EqualTo(1));
    }

    /// <summary>
    /// 验证 Conditional 将布尔值映射为 Success 和 Failure。
    /// </summary>
    [Test]
    public void ConditionalMapsBooleanToNodeStatus()
    {
        Assert.That(new FixedConditional(true).Tick(_context), Is.EqualTo(NodeStatus.Success));
        Assert.That(new FixedConditional(false).Tick(_context), Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证 Sequence 在 Running 子节点处暂停，并在其成功后于同一 Tick 继续。
    /// </summary>
    [Test]
    public void SequencePausesAtRunningChildAndContinuesInOrder()
    {
        var first = new ScriptedAction(ActionResult.Success);
        var second = new ScriptedAction(ActionResult.Running, ActionResult.Success);
        var third = new ScriptedAction(ActionResult.Success);
        var sequence = new SequenceNode(new BehaviorNode[] { first, second, third });

        Assert.That(sequence.Tick(_context), Is.EqualTo(NodeStatus.Running));
        Assert.That(first.UpdateCount, Is.EqualTo(1));
        Assert.That(second.UpdateCount, Is.EqualTo(1));
        Assert.That(third.UpdateCount, Is.Zero);

        Assert.That(sequence.Tick(_context), Is.EqualTo(NodeStatus.Success));
        Assert.That(second.UpdateCount, Is.EqualTo(2));
        Assert.That(third.UpdateCount, Is.EqualTo(1));
    }

    /// <summary>
    /// 验证 Sequence 在首个失败节点处短路，空 Sequence 直接成功。
    /// </summary>
    [Test]
    public void SequenceShortCircuitsOnFailureAndEmptySequenceSucceeds()
    {
        var skipped = new ScriptedAction(ActionResult.Success);
        var sequence = new SequenceNode(new BehaviorNode[]
        {
            new ScriptedAction(ActionResult.Failure),
            skipped
        });

        Assert.That(sequence.Tick(_context), Is.EqualTo(NodeStatus.Failure));
        Assert.That(skipped.UpdateCount, Is.Zero);
        Assert.That(new SequenceNode().Tick(_context), Is.EqualTo(NodeStatus.Success));
    }

    /// <summary>
    /// 验证 Selector 在 Running 子节点处暂停并在首个成功节点处短路。
    /// </summary>
    [Test]
    public void SelectorPausesAndShortCircuitsOnSuccess()
    {
        var running = new ScriptedAction(ActionResult.Running, ActionResult.Failure);
        var success = new ScriptedAction(ActionResult.Success);
        var skipped = new ScriptedAction(ActionResult.Success);
        var selector = new SelectorNode(new BehaviorNode[] { running, success, skipped });

        Assert.That(selector.Tick(_context), Is.EqualTo(NodeStatus.Running));
        Assert.That(success.UpdateCount, Is.Zero);

        Assert.That(selector.Tick(_context), Is.EqualTo(NodeStatus.Success));
        Assert.That(success.UpdateCount, Is.EqualTo(1));
        Assert.That(skipped.UpdateCount, Is.Zero);
    }

    /// <summary>
    /// 验证所有子节点失败时 Selector 失败，空 Selector 也失败。
    /// </summary>
    [Test]
    public void SelectorFailsWhenAllChildrenFail()
    {
        var selector = new SelectorNode(new BehaviorNode[]
        {
            new ScriptedAction(ActionResult.Failure),
            new ScriptedAction(ActionResult.Failure)
        });

        Assert.That(selector.Tick(_context), Is.EqualTo(NodeStatus.Failure));
        Assert.That(new SelectorNode().Tick(_context), Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证默认 Parallel 需要全部成功，并在任一失败时失败。
    /// </summary>
    [Test]
    public void ParallelDefaultPolicyRequiresAllSuccessAndAnyFailureFails()
    {
        var delayed = new ScriptedAction(ActionResult.Running, ActionResult.Success);
        var parallel = new ParallelNode(children: new BehaviorNode[]
        {
            new ScriptedAction(ActionResult.Success),
            delayed
        });

        Assert.That(parallel.Tick(_context), Is.EqualTo(NodeStatus.Running));
        Assert.That(parallel.Tick(_context), Is.EqualTo(NodeStatus.Success));

        var failureParallel = new ParallelNode(children: new BehaviorNode[]
        {
            new ScriptedAction(ActionResult.Success),
            new ScriptedAction(ActionResult.Failure)
        });
        Assert.That(failureParallel.Tick(_context), Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证任一成功策略达到条件后会中止仍在运行的子节点。
    /// </summary>
    [Test]
    public void ParallelAnySuccessAbortsRemainingRunningChildren()
    {
        var running = new ScriptedAction(ActionResult.Running);
        var parallel = new ParallelNode(
            ParallelSuccessPolicy.Any,
            ParallelFailurePolicy.All,
            new BehaviorNode[] { running, new ScriptedAction(ActionResult.Success) });

        Assert.That(parallel.Tick(_context), Is.EqualTo(NodeStatus.Success));
        Assert.That(running.Status, Is.EqualTo(NodeStatus.Idle));
        Assert.That(running.StopCount, Is.EqualTo(1));
    }

    /// <summary>
    /// 验证任一失败策略达到条件后会中止仍在运行的子节点。
    /// </summary>
    [Test]
    public void ParallelAnyFailureAbortsRemainingRunningChildren()
    {
        var running = new ScriptedAction(ActionResult.Running);
        var parallel = new ParallelNode(
            ParallelSuccessPolicy.All,
            ParallelFailurePolicy.Any,
            new BehaviorNode[] { running, new ScriptedAction(ActionResult.Failure) });

        Assert.That(parallel.Tick(_context), Is.EqualTo(NodeStatus.Failure));
        Assert.That(running.Status, Is.EqualTo(NodeStatus.Idle));
        Assert.That(running.StopCount, Is.EqualTo(1));
    }

    /// <summary>
    /// 验证全部失败策略会等待尚未完成的子节点。
    /// </summary>
    [Test]
    public void ParallelAllFailureWaitsForEveryChildToFail()
    {
        var delayedFailure = new ScriptedAction(ActionResult.Running, ActionResult.Failure);
        var parallel = new ParallelNode(
            ParallelSuccessPolicy.All,
            ParallelFailurePolicy.All,
            new BehaviorNode[] { new ScriptedAction(ActionResult.Failure), delayedFailure });

        Assert.That(parallel.Tick(_context), Is.EqualTo(NodeStatus.Running));
        Assert.That(parallel.Tick(_context), Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证全部成功和全部失败策略遇到混合终态时不会永久停留在 Running。
    /// </summary>
    [Test]
    public void ParallelAllPoliciesFailWhenCompletedResultsAreMixed()
    {
        var parallel = new ParallelNode(
            ParallelSuccessPolicy.All,
            ParallelFailurePolicy.All,
            new BehaviorNode[]
            {
                new ScriptedAction(ActionResult.Success),
                new ScriptedAction(ActionResult.Failure)
            });

        Assert.That(parallel.Tick(_context), Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证同一 Tick 同时满足成功和失败规则时失败优先。
    /// </summary>
    [Test]
    public void ParallelFailureTakesPriorityOverSuccess()
    {
        var parallel = new ParallelNode(
            ParallelSuccessPolicy.Any,
            ParallelFailurePolicy.Any,
            new BehaviorNode[]
            {
                new ScriptedAction(ActionResult.Success),
                new ScriptedAction(ActionResult.Failure)
            });

        Assert.That(parallel.Tick(_context), Is.EqualTo(NodeStatus.Failure));
        Assert.That(new ParallelNode().Tick(_context), Is.EqualTo(NodeStatus.Success));
    }

    /// <summary>
    /// 验证中止复合节点会递归中止运行中的 Action，且停止生命周期只调用一次。
    /// </summary>
    [Test]
    public void AbortRecursivelyStopsRunningChildOnce()
    {
        var action = new ScriptedAction(ActionResult.Running);
        var root = new RootNode(action);

        Assert.That(root.Tick(_context), Is.EqualTo(NodeStatus.Running));
        root.Abort(_context);
        root.Abort(_context);

        Assert.That(root.Status, Is.EqualTo(NodeStatus.Idle));
        Assert.That(action.Status, Is.EqualTo(NodeStatus.Idle));
        Assert.That(action.StopCount, Is.EqualTo(1));
    }

    /// <summary>
    /// 验证根节点最多包含一个子节点，空根节点成功。
    /// </summary>
    [Test]
    public void RootAllowsOnlyOneChildAndEmptyRootSucceeds()
    {
        Assert.That(new RootNode().Tick(_context), Is.EqualTo(NodeStatus.Success));

        var root = new RootNode(new ScriptedAction(ActionResult.Success));
        Assert.Throws<InvalidOperationException>(
            () => root.AddChild(new ScriptedAction(ActionResult.Success)));
    }

    /// <summary>
    /// 验证注册表和节点工厂可以从数据定义创建自定义运行时逻辑。
    /// </summary>
    [Test]
    public void RegistryBuildsCustomLogicFromDefinition()
    {
        var rootData = new RootNodeData("Root");
        var sequenceData = new SequenceNodeData("Sequence");
        sequenceData.AddChild(new ConditionalNodeData("CanRun"));
        var actionData = new ActionNodeData("WriteValue");
        actionData.Parameters.Add("Value", 7);
        sequenceData.AddChild(actionData);
        rootData.AddChild(sequenceData);

        var registry = new BehaviorTreeRegistry();
        registry.RegisterConditional("CanRun", data => new FixedConditional(true));
        registry.RegisterAction("WriteValue", data => new BlackboardWriteAction(
            "Result",
            (int)data.Parameters["Value"]));
        var blackboard = new Blackboard();
        var runner = new BehaviorTreeRunner(
            new BehaviorTreeDefinition(rootData),
            registry,
            blackboard);

        runner.Start();

        Assert.That(runner.Tick(0.25f), Is.EqualTo(NodeStatus.Success));
        Assert.That(blackboard.Get<int>("Result"), Is.EqualTo(7));
        Assert.That(runner.Context.DeltaTime, Is.EqualTo(0.25f));
    }

    /// <summary>
    /// 验证注册表构建 Conditional 时按 Invert 参数取反，缺参或无法解析不取反。
    /// </summary>
    [Test]
    public void RegistryAppliesConditionalInvertParameter()
    {
        Assert.That(TickRegisteredConditional(true, null), Is.EqualTo(NodeStatus.Success));
        Assert.That(TickRegisteredConditional(false, null), Is.EqualTo(NodeStatus.Failure));
        Assert.That(TickRegisteredConditional(true, false), Is.EqualTo(NodeStatus.Success));
        Assert.That(TickRegisteredConditional(false, false), Is.EqualTo(NodeStatus.Failure));
        Assert.That(TickRegisteredConditional(true, true), Is.EqualTo(NodeStatus.Failure));
        Assert.That(TickRegisteredConditional(false, true), Is.EqualTo(NodeStatus.Success));
        Assert.That(TickRegisteredConditional(true, "not-a-bool"), Is.EqualTo(NodeStatus.Success));
        Assert.That(TickRegisteredConditional(false, "not-a-bool"), Is.EqualTo(NodeStatus.Failure));
    }

    /// <summary>
    /// 验证未注册 LogicId 会在构建行为树时给出明确错误。
    /// </summary>
    [Test]
    public void UnknownLogicIdFailsDuringTreeConstruction()
    {
        var rootData = new RootNodeData();
        rootData.AddChild(new ActionNodeData("Missing"));

        Assert.Throws<InvalidOperationException>(() => new BehaviorTreeRunner(
            new BehaviorTreeDefinition(rootData),
            new BehaviorTreeRegistry(),
            new Blackboard()));
    }

    /// <summary>
    /// 验证运行器启动约束、终态保持、停止和重置行为。
    /// </summary>
    [Test]
    public void RunnerControlsStartTerminalStopAndReset()
    {
        ScriptedAction createdAction = null;
        var registry = new BehaviorTreeRegistry();
        registry.RegisterAction("Run", data =>
        {
            createdAction = new ScriptedAction(ActionResult.Success);
            return createdAction;
        });
        var rootData = new RootNodeData();
        rootData.AddChild(new ActionNodeData("Run"));
        var runner = new BehaviorTreeRunner(
            new BehaviorTreeDefinition(rootData),
            registry,
            new Blackboard());

        Assert.Throws<InvalidOperationException>(() => runner.Tick());

        runner.Start();
        Assert.Throws<InvalidOperationException>(runner.Start);
        Assert.That(runner.Tick(), Is.EqualTo(NodeStatus.Success));
        Assert.That(runner.Tick(), Is.EqualTo(NodeStatus.Success));
        Assert.That(createdAction.UpdateCount, Is.EqualTo(1));

        runner.Stop();
        Assert.That(runner.IsStarted, Is.False);

        runner.Reset();
        Assert.That(runner.Status, Is.EqualTo(NodeStatus.Idle));
        Assert.Throws<InvalidOperationException>(() => runner.Tick());
    }

    /// <summary>
    /// 验证行为树定义二进制往返保留结构、名称、策略和节点参数。
    /// </summary>
    [Test]
    public void BinarySerializationRoundTripPreservesDefinitionData()
    {
        var root = new RootNodeData("Root");
        var parallel = new ParallelNodeData(
            ParallelSuccessPolicy.Any,
            ParallelFailurePolicy.All,
            "Parallel");
        var action = new ActionNodeData("Attack", "AttackAction");
        action.Parameters.Add("Damage", 12);
        parallel.AddChild(action);
        parallel.AddChild(new ConditionalNodeData("HasTarget", "TargetCondition"));
        root.AddChild(parallel);

        byte[] bytes = BehaviorTreeBinarySerializer.Serialize(new BehaviorTreeDefinition(root));
        BehaviorTreeDefinition copy = BehaviorTreeBinarySerializer.Deserialize(bytes);

        Assert.That(copy.Version, Is.EqualTo(BehaviorTreeDefinition.CurrentVersion));
        Assert.That(copy.Root.Name, Is.EqualTo("Root"));
        var copiedParallel = copy.Root.Children[0] as ParallelNodeData;
        Assert.That(copiedParallel, Is.Not.Null);
        Assert.That(copiedParallel.SuccessPolicy, Is.EqualTo(ParallelSuccessPolicy.Any));
        Assert.That(copiedParallel.FailurePolicy, Is.EqualTo(ParallelFailurePolicy.All));
        var copiedAction = copiedParallel.Children[0] as ActionNodeData;
        Assert.That(copiedAction.LogicId, Is.EqualTo("Attack"));
        Assert.That(copiedAction.Parameters["Damage"], Is.EqualTo(12));
    }

    /// <summary>
    /// 验证定义序列化不包含运行时黑板数据或节点执行状态。
    /// </summary>
    [Test]
    public void BinarySerializationDoesNotPersistBlackboardOrRuntimeState()
    {
        var root = new RootNodeData();
        root.AddChild(new ActionNodeData("Success"));
        var definition = new BehaviorTreeDefinition(root);
        var registry = new BehaviorTreeRegistry();
        registry.RegisterAction("Success", data => new ScriptedAction(ActionResult.Success));
        var blackboard = new Blackboard();
        blackboard.Set("RuntimeOnly", 99);
        var runner = new BehaviorTreeRunner(definition, registry, blackboard);
        runner.Start();
        runner.Tick();

        BehaviorTreeDefinition copy = BehaviorTreeBinarySerializer.Deserialize(
            BehaviorTreeBinarySerializer.Serialize(definition));
        var newBlackboard = new Blackboard();
        var newRunner = new BehaviorTreeRunner(copy, registry, newBlackboard);

        Assert.That(newRunner.Status, Is.EqualTo(NodeStatus.Idle));
        Assert.That(newBlackboard.Contains("RuntimeOnly"), Is.False);
    }

    /// <summary>
    /// 验证序列化会拒绝未知版本、空子节点和不可序列化参数。
    /// </summary>
    [Test]
    public void BinarySerializationRejectsInvalidDefinitions()
    {
        var invalidVersion = new BehaviorTreeDefinition(new RootNodeData()) { Version = 2 };
        Assert.Throws<SerializationException>(
            () => BehaviorTreeBinarySerializer.Serialize(invalidVersion));

        var nullChildRoot = new RootNodeData();
        nullChildRoot.Children.Add(null);
        Assert.Throws<SerializationException>(() => BehaviorTreeBinarySerializer.Serialize(
            new BehaviorTreeDefinition(nullChildRoot)));

        var action = new ActionNodeData("InvalidParameter");
        action.Parameters.Add("Value", new NonSerializableValue());
        var parameterRoot = new RootNodeData();
        parameterRoot.AddChild(action);
        Assert.Throws<SerializationException>(() => BehaviorTreeBinarySerializer.Serialize(
            new BehaviorTreeDefinition(parameterRoot)));
    }

    /// <summary>
    /// 验证 AsyncActionNode 使用 UniTask 完成时从 Running 转为对应结果。
    /// </summary>
    [Test]
    public void AsyncActionPollsUniTaskUntilCompletion()
    {
        var source = new UniTaskCompletionSource<ActionResult>();
        var action = new TestAsyncAction(source);

        Assert.That(action.Tick(_context), Is.EqualTo(NodeStatus.Running));

        source.TrySetResult(ActionResult.Success);

        Assert.That(action.Tick(_context), Is.EqualTo(NodeStatus.Success));
        Assert.That(action.InvocationCount, Is.EqualTo(1));
    }

    /// <summary>
    /// 验证运行器停止时会取消 AsyncActionNode 收到的 UniTask 取消令牌。
    /// </summary>
    [Test]
    public void RunnerStopCancelsAsyncActionUniTaskToken()
    {
        var source = new UniTaskCompletionSource<ActionResult>();
        TestAsyncAction createdAction = null;
        var registry = new BehaviorTreeRegistry();
        registry.RegisterAction("Async", data =>
        {
            createdAction = new TestAsyncAction(source);
            return createdAction;
        });
        var root = new RootNodeData();
        root.AddChild(new ActionNodeData("Async"));
        var runner = new BehaviorTreeRunner(
            new BehaviorTreeDefinition(root),
            registry,
            new Blackboard());

        runner.Start();
        Assert.That(runner.Tick(), Is.EqualTo(NodeStatus.Running));
        CancellationToken actionToken = createdAction.ReceivedToken;

        runner.Stop();

        Assert.That(actionToken.IsCancellationRequested, Is.True);
        Assert.That(createdAction.Status, Is.EqualTo(NodeStatus.Idle));
        Assert.That(createdAction.StopCount, Is.EqualTo(1));
    }

    /// <summary>
    /// 读取黑板私有条目当前保存的实际对象，用于验证值类型包装行为。
    /// </summary>
    private static object GetBlackboardStoredValue(Blackboard blackboard, string key)
    {
        FieldInfo entriesField = typeof(Blackboard).GetField(
            "_entries",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(entriesField, Is.Not.Null);

        var entries = (IDictionary)entriesField.GetValue(blackboard);
        object entry = entries[key];
        Assert.That(entry, Is.Not.Null);

        PropertyInfo valueProperty = entry.GetType().GetProperty("Value");
        Assert.That(valueProperty, Is.Not.Null);
        return valueProperty.GetValue(entry);
    }

    /// <summary>
    /// 用于验证枚举值会被值类型包装对象承载。
    /// </summary>
    private enum BlackboardTestState
    {
        /// <summary>默认状态。</summary>
        None,

        /// <summary>就绪状态。</summary>
        Ready
    }

    /// <summary>
    /// 用于验证 Remove 回收行为的独立值类型。
    /// </summary>
    private readonly struct RemovedBlackboardValue
    {
        /// <summary>
        /// 创建具有指定数值的测试数据。
        /// </summary>
        public RemovedBlackboardValue(int number)
        {
            Number = number;
        }

        /// <summary>测试数据保存的数值。</summary>
        public int Number { get; }
    }

    /// <summary>
    /// 用于验证 Clear 回收行为的独立值类型。
    /// </summary>
    private readonly struct ClearedBlackboardValue
    {
        /// <summary>
        /// 创建具有指定数值的测试数据。
        /// </summary>
        public ClearedBlackboardValue(int number)
        {
            Number = number;
        }

        /// <summary>测试数据保存的数值。</summary>
        public int Number { get; }
    }

    /// <summary>
    /// 可按脚本顺序返回 ActionResult 并记录生命周期次数的测试 Action。
    /// </summary>
    private sealed class ScriptedAction : ActionNode
    {
        /// <summary>该 Action 每次更新依次返回的结果。</summary>
        private readonly ActionResult[] _results;

        /// <summary>下一次更新读取的结果索引。</summary>
        private int _resultIndex;

        /// <summary>
        /// 创建具有指定结果序列的测试 Action。
        /// </summary>
        public ScriptedAction(params ActionResult[] results)
        {
            _results = results == null || results.Length == 0
                ? new[] { ActionResult.Running }
                : results;
        }

        /// <summary>Action 开始次数。</summary>
        public int StartCount { get; private set; }

        /// <summary>Action 更新次数。</summary>
        public int UpdateCount { get; private set; }

        /// <summary>Action 停止次数。</summary>
        public int StopCount { get; private set; }

        /// <summary>Action 重置次数。</summary>
        public int ResetCount { get; private set; }

        /// <summary>
        /// 记录 Action 开始。
        /// </summary>
        protected override void OnStart(BehaviorTreeContext context)
        {
            StartCount++;
        }

        /// <summary>
        /// 返回脚本中当前结果，超出脚本后保持最后一个结果。
        /// </summary>
        protected override ActionResult OnUpdate(BehaviorTreeContext context)
        {
            UpdateCount++;
            int index = Math.Min(_resultIndex, _results.Length - 1);
            _resultIndex++;
            return _results[index];
        }

        /// <summary>
        /// 记录 Action 停止。
        /// </summary>
        protected override void OnStop(BehaviorTreeContext context)
        {
            StopCount++;
        }

        /// <summary>
        /// 记录 Action 重置并恢复结果索引。
        /// </summary>
        protected override void OnReset()
        {
            ResetCount++;
            _resultIndex = 0;
        }
    }

    /// <summary>
    /// 使用注册表构建固定结果 Conditional 并 Tick 一次；invert 为 null 表示不写入参数。
    /// </summary>
    private static NodeStatus TickRegisteredConditional(bool evaluateResult, object invert)
    {
        var root = new RootNodeData();
        var data = new ConditionalNodeData("Fixed");
        if (invert != null)
        {
            data.Parameters[ConditionalNode.InvertParameterKey] = invert;
        }

        root.AddChild(data);
        var registry = new BehaviorTreeRegistry();
        registry.RegisterConditional("Fixed", _ => new FixedConditional(evaluateResult));
        var runner = new BehaviorTreeRunner(
            new BehaviorTreeDefinition(root),
            registry,
            new Blackboard());
        runner.Start();
        return runner.Tick();
    }

    /// <summary>
    /// 返回固定布尔结果的测试 Conditional。
    /// </summary>
    private sealed class FixedConditional : ConditionalNode
    {
        /// <summary>条件节点返回的固定结果。</summary>
        private readonly bool _result;

        /// <summary>
        /// 创建固定结果条件节点。
        /// </summary>
        public FixedConditional(bool result)
        {
            _result = result;
        }

        /// <summary>
        /// 返回构造时指定的条件结果。
        /// </summary>
        protected override bool Evaluate(BehaviorTreeContext context)
        {
            return _result;
        }
    }

    /// <summary>
    /// 向黑板写入固定整数的测试 Action。
    /// </summary>
    private sealed class BlackboardWriteAction : ActionNode
    {
        /// <summary>目标黑板 key。</summary>
        private readonly string _key;

        /// <summary>写入黑板的整数。</summary>
        private readonly int _value;

        /// <summary>
        /// 创建黑板写入 Action。
        /// </summary>
        public BlackboardWriteAction(string key, int value)
        {
            _key = key;
            _value = value;
        }

        /// <summary>
        /// 写入黑板并立即成功。
        /// </summary>
        protected override ActionResult OnUpdate(BehaviorTreeContext context)
        {
            context.Blackboard.Set(_key, _value);
            return ActionResult.Success;
        }
    }

    /// <summary>
    /// 返回外部 UniTaskCompletionSource 任务的测试异步 Action。
    /// </summary>
    private sealed class TestAsyncAction : AsyncActionNode
    {
        /// <summary>控制异步 Action 完成的 UniTask 源。</summary>
        private readonly UniTaskCompletionSource<ActionResult> _source;

        /// <summary>
        /// 创建测试异步 Action。
        /// </summary>
        public TestAsyncAction(UniTaskCompletionSource<ActionResult> source)
        {
            _source = source;
        }

        /// <summary>异步逻辑启动次数。</summary>
        public int InvocationCount { get; private set; }

        /// <summary>异步逻辑收到的取消令牌。</summary>
        public CancellationToken ReceivedToken { get; private set; }

        /// <summary>异步 Action 停止次数。</summary>
        public int StopCount { get; private set; }

        /// <summary>
        /// 返回由测试控制的 UniTask。
        /// </summary>
        protected override UniTask<ActionResult> OnUpdateAsync(
            BehaviorTreeContext context,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            ReceivedToken = cancellationToken;
            return _source.Task;
        }

        /// <summary>
        /// 记录异步 Action 停止。
        /// </summary>
        protected override void OnStop(BehaviorTreeContext context)
        {
            StopCount++;
        }
    }

    /// <summary>
    /// 用于验证参数校验的不可序列化测试类型。
    /// </summary>
    private sealed class NonSerializableValue
    {
    }
}
#endif
