#if UNITY_EDITOR
using System;
using System.Text.RegularExpressions;
using System.IO;
using BehaviorTree;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>验证行为树仓库的内存注册和 Resources 加载。</summary>
public class BehaviorTreeRepositoryTests
{
    private const string ResourcesFolder = "Assets/Resources/Configs/__BehaviorTreeRepositoryTests";
    private const string ResourcesPath = "Configs/__BehaviorTreeRepositoryTests";

    [SetUp]
    public void SetUp()
    {
        DeleteAssetIfPresent(ResourcesFolder);
        CreateFolder("Assets/Resources/Configs", "__BehaviorTreeRepositoryTests");
    }

    [TearDown]
    public void TearDown()
    {
        DeleteAssetIfPresent(ResourcesFolder);
        AssetDatabase.Refresh();
    }

    /// <summary>验证按名称注册后可以查询，空白名视为未命中。</summary>
    [Test]
    public void RegisterAndTryGet()
    {
        var repo = new BehaviorTreeRepository();
        BehaviorTreeDefinition definition = CreateDefinition("StopMovement");
        repo.Register("StayTree", definition);

        Assert.That(repo.TryGet("StayTree", out BehaviorTreeDefinition stored), Is.True);
        Assert.That(stored, Is.SameAs(definition));
        Assert.That(repo.TryGet(" StayTree ", out stored), Is.True);
        Assert.That(stored, Is.SameAs(definition));
        Assert.That(repo.TryGet("Missing", out _), Is.False);
        Assert.That(repo.TryGet("  ", out _), Is.False);
    }

    /// <summary>验证重复名称注册会抛出异常。</summary>
    [Test]
    public void DuplicateNameThrows()
    {
        var repo = new BehaviorTreeRepository();
        repo.Register("StayTree", CreateDefinition("StopMovement"));
        Assert.Throws<InvalidOperationException>(
            () => repo.Register("StayTree", CreateDefinition("SelectTarget")));
    }

    /// <summary>验证空资源路径会被拒绝。</summary>
    [Test]
    public void LoadFromResourcesRejectsEmptyPath()
    {
        var repo = new BehaviorTreeRepository();
        Assert.Throws<ArgumentException>(() => repo.LoadFromResources(null));
        Assert.Throws<ArgumentException>(() => repo.LoadFromResources(""));
    }

    /// <summary>验证从临时 Resources 目录加载合法 .bytes，并跳过坏数据。</summary>
    [Test]
    public void LoadFromResourcesReadsValidBytesAndSkipsInvalid()
    {
        WriteBytes(ResourcesFolder + "/StayTree.bytes", CreateDefinition("StopMovement"));
        File.WriteAllBytes(GetProjectAbsolutePath(ResourcesFolder + "/BadTree.bytes"), new byte[] { 1, 2, 3 });
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        LogAssert.Expect(LogType.Error, new Regex("加载行为树失败 BadTree"));
        var repo = new BehaviorTreeRepository();
        int loadedCount = repo.LoadFromResources(ResourcesPath);

        Assert.That(loadedCount, Is.EqualTo(1));
        Assert.That(repo.TryGet("StayTree", out BehaviorTreeDefinition stay), Is.True);
        Assert.That(stay, Is.Not.Null);
        Assert.That(repo.TryGet("BadTree", out _), Is.False);
    }

    /// <summary>创建只含一个 Action 的测试行为树定义。</summary>
    private static BehaviorTreeDefinition CreateDefinition(string logicId)
    {
        var root = new RootNodeData();
        root.AddChild(new ActionNodeData(logicId));
        return new BehaviorTreeDefinition(root);
    }

    /// <summary>将行为树定义序列化为临时 .bytes 并同步导入。</summary>
    private static void WriteBytes(string assetPath, BehaviorTreeDefinition definition)
    {
        string absolutePath = GetProjectAbsolutePath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllBytes(absolutePath, BehaviorTreeBinarySerializer.Serialize(definition));
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    /// <summary>确保 Unity 文件夹存在。</summary>
    private static void CreateFolder(string parent, string folderName)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + folderName))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    /// <summary>删除测试专用临时资源。</summary>
    private static void DeleteAssetIfPresent(string assetPath)
    {
        if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null || AssetDatabase.IsValidFolder(assetPath))
        {
            AssetDatabase.DeleteAsset(assetPath);
        }
    }

    /// <summary>将 Assets 相对路径转换为绝对路径。</summary>
    private static string GetProjectAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
#endif
