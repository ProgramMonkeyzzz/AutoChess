#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoChess.Editor;
using BehaviorTree;
using NUnit.Framework;
using ParadoxNotion;
using ParadoxNotion.Design;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 覆盖 NodeCanvas 行为树编辑适配层、编译器和 bytes 导出流程，以及图节点类型颜色、正文标签和警告。
/// </summary>
public class BehaviorTreeGraphTests
{
    private const string TemporaryRoot = "Assets/__BehaviorTreeGraphTests";

    /// <summary>
    /// 在每个测试前清理逻辑目录并准备临时导出目录。
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        BehaviorTreeLogicCatalog.Clear();
        Directory.CreateDirectory(GetAbsolutePath(TemporaryRoot));
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 在每个测试后清理逻辑目录和临时资产。
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        BehaviorTreeLogicCatalog.Clear();
        if (AssetDatabase.IsValidFolder(TemporaryRoot))
        {
            AssetDatabase.DeleteAsset(TemporaryRoot);
        }

        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 验证新建图自动创建唯一 Root 且各类节点端口数量正确。
    /// </summary>
    [Test]
    public void NewGraphCreatesRootAndEnforcesPorts()
    {
        var graph = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
        try
        {
            var root = graph.GetUniqueRootNode();
            Assert.That(root, Is.Not.Null);
            Assert.That(graph.primeNode, Is.SameAs(root));
            Assert.That(graph.allNodes.Count(node => node is BehaviorTreeRootGraphNode), Is.EqualTo(1));
            Assert.That(root.maxInConnections, Is.EqualTo(0));
            Assert.That(root.maxOutConnections, Is.EqualTo(1));

            var sequence = graph.AddNode<BehaviorTreeSequenceGraphNode>(new Vector2(0f, 80f));
            var action = graph.AddNode<BehaviorTreeActionGraphNode>(new Vector2(0f, 160f));
            Assert.That(graph.ConnectNodes(root, sequence), Is.Not.Null);
            Assert.That(graph.ConnectNodes(sequence, action), Is.Not.Null);
            Assert.That(graph.ConnectNodes(root, action), Is.Null);
            Assert.That(action.maxOutConnections, Is.EqualTo(0));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    /// <summary>
    /// 验证图编译后保留节点名称、Parallel 策略、LogicId、参数和子节点顺序。
    /// </summary>
    [Test]
    public void CompilerPreservesDefinitionData()
    {
        var graph = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
        try
        {
            var root = graph.GetUniqueRootNode();
            root.name = "RootNode";
            var parallel = graph.AddNode<BehaviorTreeParallelGraphNode>(new Vector2(0f, 80f));
            parallel.name = "ParallelNode";
            parallel.successPolicy = ParallelSuccessPolicy.Any;
            parallel.failurePolicy = ParallelFailurePolicy.All;
            var action = graph.AddNode<BehaviorTreeActionGraphNode>(new Vector2(-100f, 160f));
            action.name = "AttackAction";
            action.LogicId = "Attack";
            action.Parameters.Add(new BehaviorTreeEditorParameter
            {
                key = "Damage",
                type = BehaviorTreeParameterType.Int,
                intValue = 12
            });
            var conditional = graph.AddNode<BehaviorTreeConditionalGraphNode>(new Vector2(100f, 160f));
            conditional.name = "HasTarget";
            conditional.LogicId = "HasTarget";

            graph.ConnectNodes(root, parallel);
            graph.ConnectNodes(parallel, action);
            graph.ConnectNodes(parallel, conditional);

            Assert.That(
                BehaviorTreeGraphCompiler.TryBuildDefinition(
                    graph,
                    out var definition,
                    out var errors),
                Is.True,
                string.Join("\n", errors));

            Assert.That(definition.Root.Name, Is.EqualTo("RootNode"));
            var copiedParallel = definition.Root.Children[0] as ParallelNodeData;
            Assert.That(copiedParallel, Is.Not.Null);
            Assert.That(copiedParallel.Name, Is.EqualTo("ParallelNode"));
            Assert.That(copiedParallel.SuccessPolicy, Is.EqualTo(ParallelSuccessPolicy.Any));
            Assert.That(copiedParallel.FailurePolicy, Is.EqualTo(ParallelFailurePolicy.All));
            Assert.That(((ActionNodeData)copiedParallel.Children[0]).Parameters["Damage"], Is.EqualTo(12));
            Assert.That(((ActionNodeData)copiedParallel.Children[0]).LogicId, Is.EqualTo("Attack"));
            Assert.That(((ConditionalNodeData)copiedParallel.Children[1]).LogicId, Is.EqualTo("HasTarget"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    /// <summary>
    /// 验证未连接到 Root 的节点不能被导出。
    /// </summary>
    [Test]
    public void CompilerRejectsOrphanNodes()
    {
        var graph = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
        try
        {
            graph.AddNode<BehaviorTreeActionGraphNode>(new Vector2(0f, 80f));

            Assert.That(
                BehaviorTreeGraphCompiler.TryBuildDefinition(
                    graph,
                    out _,
                    out var errors),
                Is.False);
            Assert.That(errors.Any(error => error.Contains("未连接到 Root")), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    /// <summary>
    /// 验证重复 Root 和空 LogicId 在图编译阶段被拒绝。
    /// </summary>
    [Test]
    public void CompilerRejectsDuplicateRootAndEmptyLogicId()
    {
        var graph = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
        try
        {
            var root = graph.GetUniqueRootNode();
            graph.AddNode<BehaviorTreeRootGraphNode>(new Vector2(100f, 0f));
            var action = graph.AddNode<BehaviorTreeActionGraphNode>(new Vector2(0f, 80f));
            graph.ConnectNodes(root, action);

            Assert.That(
                BehaviorTreeGraphCompiler.TryBuildDefinition(
                    graph,
                    out _,
                    out var errors),
                Is.False);
            Assert.That(errors.Any(error => error.Contains("只能包含一个 Root")), Is.True);
            Assert.That(errors.Any(error => error.Contains("LogicId 不能为空")), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    /// <summary>
    /// 验证逻辑目录的参数默认值可创建为编辑参数并拒绝重复 LogicId。
    /// </summary>
    [Test]
    public void LogicCatalogRegistersDefaultsAndRejectsDuplicates()
    {
        var parameter = new BehaviorTreeParameterDefinition(
            "Damage",
            BehaviorTreeParameterType.Int,
            "Damage");
        parameter.DefaultInt = 9;
        BehaviorTreeLogicCatalog.RegisterAction(
            "AttackCatalogTest",
            "Attack",
            "Combat",
            new[] { parameter });

        Assert.That(
            BehaviorTreeLogicCatalog.TryGet(
                BehaviorTreeLogicKind.Action,
                "AttackCatalogTest",
                out var definition),
            Is.True);
        var editorParameter = BehaviorTreeEditorParameter.FromDefinition(definition.Parameters[0]);
        Assert.That(editorParameter.key, Is.EqualTo("Damage"));
        Assert.That(editorParameter.intValue, Is.EqualTo(9));
        Assert.Throws<InvalidOperationException>(() => BehaviorTreeLogicCatalog.RegisterAction(
            "AttackCatalogTest",
            "Duplicate"));
    }

    /// <summary>
    /// 验证导出的 bytes 可以被现有序列化器读取并保留定义内容。
    /// </summary>
    [Test]
    public void ExporterWritesReadableBytes()
    {
        var graph = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
        var assetPath = TemporaryRoot + "/Attack.bytes";
        try
        {
            var root = graph.GetUniqueRootNode();
            var action = graph.AddNode<BehaviorTreeActionGraphNode>(new Vector2(0f, 80f));
            action.LogicId = "Attack";
            graph.ConnectNodes(root, action);

            Assert.That(
                BehaviorTreeGraphExporter.ExportToPath(graph, assetPath, out var error),
                Is.True,
                error);
            Assert.That(File.Exists(GetAbsolutePath(assetPath)), Is.True);

            var copy = BehaviorTreeBinarySerializer.Deserialize(
                File.ReadAllBytes(GetAbsolutePath(assetPath)));
            Assert.That(copy.Root.Children.Count, Is.EqualTo(1));
            Assert.That(((ActionNodeData)copy.Root.Children[0]).LogicId, Is.EqualTo("Attack"));
            Assert.That(graph.ExportAssetPath, Is.EqualTo(assetPath));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    /// <summary>
    /// 验证各图节点类型具有互不相同的颜色、图标、说明，并且标题图标对齐到左侧。
    /// </summary>
    [Test]
    public void GraphNodesUseDistinctTypeColorsAndIcons()
    {
        AssertTypePresentation(
            typeof(BehaviorTreeRootGraphNode),
            BehaviorTreeGraphNodePresentation.RootColorHex,
            BehaviorTreeGraphNodePresentation.RootIconName);
        AssertTypePresentation(
            typeof(BehaviorTreeSequenceGraphNode),
            BehaviorTreeGraphNodePresentation.SequenceColorHex,
            BehaviorTreeGraphNodePresentation.SequenceIconName);
        AssertTypePresentation(
            typeof(BehaviorTreeSelectorGraphNode),
            BehaviorTreeGraphNodePresentation.SelectorColorHex,
            BehaviorTreeGraphNodePresentation.SelectorIconName);
        AssertTypePresentation(
            typeof(BehaviorTreeParallelGraphNode),
            BehaviorTreeGraphNodePresentation.ParallelColorHex,
            BehaviorTreeGraphNodePresentation.ParallelIconName);
        AssertTypePresentation(
            typeof(BehaviorTreeActionGraphNode),
            BehaviorTreeGraphNodePresentation.ActionColorHex,
            BehaviorTreeGraphNodePresentation.ActionIconName);
        AssertTypePresentation(
            typeof(BehaviorTreeConditionalGraphNode),
            BehaviorTreeGraphNodePresentation.ConditionalColorHex,
            BehaviorTreeGraphNodePresentation.ConditionalIconName);

        var colors = new HashSet<string>
        {
            BehaviorTreeGraphNodePresentation.RootColorHex,
            BehaviorTreeGraphNodePresentation.SequenceColorHex,
            BehaviorTreeGraphNodePresentation.SelectorColorHex,
            BehaviorTreeGraphNodePresentation.ParallelColorHex,
            BehaviorTreeGraphNodePresentation.ActionColorHex,
            BehaviorTreeGraphNodePresentation.ConditionalColorHex
        };
        Assert.That(colors.Count, Is.EqualTo(6));

        var graph = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
        try
        {
            Assert.That(graph.GetUniqueRootNode().iconAlignment, Is.EqualTo(Alignment2x2.Left));
            Assert.That(
                graph.AddNode<BehaviorTreeSelectorGraphNode>(Vector2.zero).iconAlignment,
                Is.EqualTo(Alignment2x2.Left));
            Assert.That(
                graph.AddNode<BehaviorTreeActionGraphNode>(Vector2.zero).iconAlignment,
                Is.EqualTo(Alignment2x2.Left));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }

        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        Assert.That(typeof(BehaviorTreeGraphNode).GetMethod("OnNodeGUI", flags), Is.Not.Null);
        Assert.That(typeof(BehaviorTreeGraphNode).GetMethod("GetWarningOrError", flags), Is.Not.Null);
        Assert.That(
            typeof(BehaviorTreeGraphNode).GetMethod(
                "GetConnectionInfo",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly),
            Is.Not.Null);
    }

    /// <summary>
    /// 验证节点正文显示类型标签、Parallel 策略、叶节点 LogicId/参数，以及连线序号和警告。
    /// </summary>
    [Test]
    public void NodeBodyShowsTypeLogicAndWarnings()
    {
        var graph = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
        try
        {
            var root = graph.GetUniqueRootNode();
            Assert.That(
                BehaviorTreeGraphNodePresentation.GetTypeLabel(root),
                Is.EqualTo(BehaviorTreeGraphNodePresentation.RootTypeLabel));
            Assert.That(
                BehaviorTreeGraphNodePresentation.BuildBodyText(root),
                Does.Contain(BehaviorTreeGraphNodePresentation.RootTypeLabel));
            Assert.That(
                BehaviorTreeGraphNodePresentation.GetWarningOrError(root),
                Is.EqualTo(BehaviorTreeGraphNodePresentation.MissingChildrenWarning));

            var selector = graph.AddNode<BehaviorTreeSelectorGraphNode>(new Vector2(0f, 80f));
            selector.name = "RootSelector";
            Assert.That(
                BehaviorTreeGraphNodePresentation.GetTypeLabel(selector),
                Is.EqualTo(BehaviorTreeGraphNodePresentation.SelectorTypeLabel));
            var selectorBody = BehaviorTreeGraphNodePresentation.BuildBodyText(selector);
            Assert.That(selectorBody, Does.Contain(BehaviorTreeGraphNodePresentation.SelectorTypeLabel));
            Assert.That(selectorBody, Does.Not.Contain("RootSelector"));
            Assert.That(selector.GetConnectionInfo(0), Is.Null);

            var sequence = graph.AddNode<BehaviorTreeSequenceGraphNode>(new Vector2(-120f, 160f));
            sequence.name = "RetargetIfDead";
            Assert.That(
                BehaviorTreeGraphNodePresentation.BuildBodyText(sequence),
                Does.Contain(BehaviorTreeGraphNodePresentation.SequenceTypeLabel));

            var parallel = graph.AddNode<BehaviorTreeParallelGraphNode>(new Vector2(120f, 160f));
            parallel.successPolicy = ParallelSuccessPolicy.Any;
            parallel.failurePolicy = ParallelFailurePolicy.All;
            var parallelBody = BehaviorTreeGraphNodePresentation.BuildBodyText(parallel);
            Assert.That(parallelBody, Does.Contain(BehaviorTreeGraphNodePresentation.ParallelTypeLabel));
            Assert.That(parallelBody, Does.Contain("Success Any"));
            Assert.That(parallelBody, Does.Contain("Failure All"));

            var namedAction = graph.AddNode<BehaviorTreeActionGraphNode>(new Vector2(-200f, 240f));
            namedAction.name = "AttackTarget";
            namedAction.LogicId = "AttackTarget";
            var namedActionBody = BehaviorTreeGraphNodePresentation.BuildBodyText(namedAction);
            Assert.That(namedActionBody, Does.Contain(BehaviorTreeGraphNodePresentation.ActionTypeLabel));
            Assert.That(namedActionBody, Does.Not.Contain("AttackTarget"));

            var aliasedAction = graph.AddNode<BehaviorTreeActionGraphNode>(new Vector2(0f, 240f));
            aliasedAction.name = "ReselectTarget";
            aliasedAction.LogicId = "SelectTarget";
            aliasedAction.Parameters.Add(new BehaviorTreeEditorParameter
            {
                key = "Mode",
                type = BehaviorTreeParameterType.Int,
                intValue = 0
            });
            var aliasedBody = BehaviorTreeGraphNodePresentation.BuildBodyText(aliasedAction);
            Assert.That(aliasedBody, Does.Contain(BehaviorTreeGraphNodePresentation.ActionTypeLabel));
            Assert.That(aliasedBody, Does.Contain("SelectTarget"));
            Assert.That(aliasedBody, Does.Contain("Mode=0"));
            Assert.That(
                BehaviorTreeGraphNodePresentation.FormatParameter(aliasedAction.Parameters[0]),
                Is.EqualTo("Mode=0"));

            var emptyAction = graph.AddNode<BehaviorTreeActionGraphNode>(new Vector2(200f, 240f));
            Assert.That(
                BehaviorTreeGraphNodePresentation.BuildBodyText(emptyAction),
                Does.Contain("\u672a\u6307\u5b9a LogicId"));
            Assert.That(
                BehaviorTreeGraphNodePresentation.GetWarningOrError(emptyAction),
                Is.EqualTo(BehaviorTreeGraphNodePresentation.MissingLogicIdError));

            var unknownConditional = graph.AddNode<BehaviorTreeConditionalGraphNode>(new Vector2(200f, 320f));
            unknownConditional.LogicId = "MissingLogic";
            Assert.That(
                BehaviorTreeGraphNodePresentation.GetTypeLabel(unknownConditional),
                Is.EqualTo(BehaviorTreeGraphNodePresentation.ConditionalTypeLabel));
            Assert.That(
                BehaviorTreeGraphNodePresentation.GetWarningOrError(unknownConditional),
                Is.EqualTo(BehaviorTreeGraphNodePresentation.UnknownLogicIdWarning));

            BehaviorTreeLogicCatalog.RegisterAction("AttackTarget", "Attack");
            BehaviorTreeLogicCatalog.RegisterAction("SelectTarget", "Select");
            Assert.That(BehaviorTreeGraphNodePresentation.GetWarningOrError(namedAction), Is.Null);
            Assert.That(BehaviorTreeGraphNodePresentation.GetWarningOrError(aliasedAction), Is.Null);

            graph.ConnectNodes(selector, sequence);
            Assert.That(selector.GetConnectionInfo(0), Is.Null);
            graph.ConnectNodes(selector, parallel);
            Assert.That(selector.GetConnectionInfo(0), Is.EqualTo("1"));
            Assert.That(selector.GetConnectionInfo(1), Is.EqualTo("2"));
            Assert.That(root.GetConnectionInfo(0), Is.Null);
            Assert.That(namedAction.GetConnectionInfo(0), Is.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    /// <summary>
    /// 断言图节点类型带有匹配的颜色、图标和说明属性。
    /// </summary>
    private static void AssertTypePresentation(Type type, string hexColor, string iconName)
    {
        var color = type.GetCustomAttribute<ColorAttribute>();
        Assert.That(color, Is.Not.Null, type.Name + " missing Color");
        Assert.That(color.hexColor, Is.EqualTo(hexColor));

        var icon = type.GetCustomAttribute<ParadoxNotion.Design.IconAttribute>();
        Assert.That(icon, Is.Not.Null, type.Name + " missing Icon");
        Assert.That(icon.iconName, Is.EqualTo(iconName));

        Assert.That(type.GetCustomAttribute<ParadoxNotion.Design.DescriptionAttribute>(), Is.Not.Null, type.Name + " missing Description");
    }

    /// <summary>
    /// 验证叶节点参数 Inspector 用列表宿主创建 PropertyTree，空列表和多参数都不会按多目标创建。
    /// </summary>
    [Test]
    public void ParameterPropertyTreeAcceptsEmptyListAndInspectsHost()
    {
        Assert.Throws<ArgumentNullException>(
            () => BehaviorTreeGraphInspector.CreateParameterPropertyTree(null));

        var emptyParameters = new List<BehaviorTreeEditorParameter>();
        PropertyTree emptyTree = null;
        try
        {
            Assert.DoesNotThrow(
                () => emptyTree = BehaviorTreeGraphInspector.CreateParameterPropertyTree(emptyParameters));
            Assert.That(emptyTree, Is.Not.Null);
            Assert.That(emptyTree.WeakTargets.Count, Is.EqualTo(1));
            Assert.That(GetHostParameters(emptyTree), Is.SameAs(emptyParameters));
        }
        finally
        {
            emptyTree?.Dispose();
        }

        var parameters = new List<BehaviorTreeEditorParameter>
        {
            new BehaviorTreeEditorParameter
            {
                key = "Damage",
                type = BehaviorTreeParameterType.Int,
                intValue = 12
            },
            new BehaviorTreeEditorParameter
            {
                key = "Enabled",
                type = BehaviorTreeParameterType.Bool,
                boolValue = true
            }
        };
        PropertyTree tree = null;
        try
        {
            tree = BehaviorTreeGraphInspector.CreateParameterPropertyTree(parameters);
            Assert.That(tree.WeakTargets.Count, Is.EqualTo(1));
            Assert.That(GetHostParameters(tree), Is.SameAs(parameters));
        }
        finally
        {
            tree?.Dispose();
        }
    }

    /// <summary>
    /// 从参数 PropertyTree 读取宿主中绑定的列表实例。
    /// </summary>
    private static List<BehaviorTreeEditorParameter> GetHostParameters(PropertyTree tree)
    {
        Assert.That(tree, Is.Not.Null);
        Assert.That(tree.WeakTargets, Is.Not.Null);
        Assert.That(tree.WeakTargets.Count, Is.GreaterThan(0));
        var host = tree.WeakTargets[0];
        Assert.That(host, Is.Not.Null);
        var field = host.GetType().GetField("Parameters");
        Assert.That(field, Is.Not.Null);
        return field.GetValue(host) as List<BehaviorTreeEditorParameter>;
    }

    /// <summary>
    /// 将 Unity 资产路径转换为本机绝对路径。
    /// </summary>
    private static string GetAbsolutePath(string assetPath)
    {
        var projectRoot = Directory.GetParent(Application.dataPath);
        if (projectRoot == null)
        {
            throw new InvalidOperationException("无法确定 Unity 项目根目录。 ");
        }

        return Path.Combine(
            projectRoot.FullName,
            assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}

#endif
