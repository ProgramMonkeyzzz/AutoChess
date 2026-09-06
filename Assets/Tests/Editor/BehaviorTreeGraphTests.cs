#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoChess.Editor;
using BehaviorTree;
using NUnit.Framework;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 覆盖 NodeCanvas 行为树编辑适配层、编译器和 bytes 导出流程。
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
