#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>验证棋子配置仓库的内存注册和 Resources 加载。</summary>
public class ChessConfigRepositoryTests
{
    private const string ResourcesFolder = "Assets/Resources/Configs/__ChessConfigRepositoryTests";
    private const string ResourcesPath = "Configs/__ChessConfigRepositoryTests";

    [SetUp]
    public void SetUp()
    {
        DeleteAssetIfPresent(ResourcesFolder);
        CreateFolder("Assets/Resources/Configs", "__ChessConfigRepositoryTests");
    }

    [TearDown]
    public void TearDown()
    {
        DeleteAssetIfPresent(ResourcesFolder);
        AssetDatabase.Refresh();
    }

    /// <summary>验证内存注册后可按 ConfigId 查询，且 ConfigId=0 合法。</summary>
    [Test]
    public void RegisterAndTryGet()
    {
        var repo = new ChessConfigRepository();
        var zero = CreateConfig(0, "Zero", 10);
        var one = CreateConfig(1, "One", 20);
        repo.Register(zero);
        repo.Register(one);

        Assert.That(repo.TryGet(0, out ChessBaseData storedZero), Is.True);
        Assert.That(storedZero.Name, Is.EqualTo("Zero"));
        Assert.That(storedZero.MaxHp, Is.EqualTo(10));
        Assert.That(repo.TryGet(1, out ChessBaseData storedOne), Is.True);
        Assert.That(storedOne.Name, Is.EqualTo("One"));
        Assert.That(repo.TryGet(99, out _), Is.False);
    }

    /// <summary>验证重复 ConfigId 注册会抛出异常。</summary>
    [Test]
    public void DuplicateConfigIdThrows()
    {
        var repo = new ChessConfigRepository();
        repo.Register(CreateConfig(3, "A", 1));
        Assert.Throws<InvalidOperationException>(() => repo.Register(CreateConfig(3, "B", 2)));
    }

    /// <summary>验证仓库保存的是配置副本，外部修改不影响已注册数据。</summary>
    [Test]
    public void RegisterStoresClone()
    {
        var repo = new ChessConfigRepository();
        ChessBaseData source = CreateConfig(7, "Clone", 30);
        source.MovementStayBehaviorTree = "ChessCombat";
        repo.Register(source);
        source.MaxHp = 99;
        source.Name = "Mutated";
        source.MovementStayBehaviorTree = "Changed";

        Assert.That(repo.TryGet(7, out ChessBaseData stored), Is.True);
        Assert.That(stored, Is.Not.SameAs(source));
        Assert.That(stored.MaxHp, Is.EqualTo(30));
        Assert.That(stored.Name, Is.EqualTo("Clone"));
        Assert.That(stored.MovementStayBehaviorTree, Is.EqualTo("ChessCombat"));
    }

    /// <summary>验证空资源路径会被拒绝。</summary>
    [Test]
    public void LoadFromResourcesRejectsEmptyPath()
    {
        var repo = new ChessConfigRepository();
        Assert.Throws<ArgumentException>(() => repo.LoadFromResources(null));
        Assert.Throws<ArgumentException>(() => repo.LoadFromResources(""));
    }

    /// <summary>验证从临时 Resources 目录加载 .bytes 配置，包含 ConfigId=0。</summary>
    [Test]
    public void LoadFromResourcesReadsTemporaryBytes()
    {
        WriteBytes(ResourcesFolder + "/Zero.bytes", CreateConfig(0, "LoadedZero", 11));
        WriteBytes(ResourcesFolder + "/One.bytes", CreateConfig(4, "LoadedFour", 22));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var repo = new ChessConfigRepository();
        int loadedCount = repo.LoadFromResources(ResourcesPath);

        Assert.That(loadedCount, Is.EqualTo(2));
        Assert.That(repo.TryGet(0, out ChessBaseData zero), Is.True);
        Assert.That(zero.Name, Is.EqualTo("LoadedZero"));
        Assert.That(zero.MaxHp, Is.EqualTo(11));
        Assert.That(zero.MovementStayBehaviorTree, Is.EqualTo("StayTree"));
        Assert.That(repo.TryGet(4, out ChessBaseData four), Is.True);
        Assert.That(four.Name, Is.EqualTo("LoadedFour"));
        Assert.That(four.MaxHp, Is.EqualTo(22));
    }

    /// <summary>创建测试用棋子配置。</summary>
    private static ChessBaseData CreateConfig(int configId, string name, int maxHp)
    {
        return new ChessBaseData
        {
            ConfigId = configId,
            Name = name,
            MaxHp = maxHp,
            Atk = 1,
            Range = 1f,
            MoveSpeed = 1f,
            AtkSpeed = 1f,
            WindUpTime = 0.1f,
            BulletSpeed = 0f,
            MovementStayBehaviorTree = name == "LoadedZero" ? "StayTree" : null
        };
    }

    /// <summary>将配置序列化为临时 .bytes 并同步导入。</summary>
    private static void WriteBytes(string assetPath, ChessBaseData data)
    {
        string absolutePath = GetProjectAbsolutePath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
#pragma warning disable SYSLIB0011
        var formatter = new BinaryFormatter();
        using (var stream = File.Open(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            formatter.Serialize(stream, data);
        }
#pragma warning restore SYSLIB0011
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
