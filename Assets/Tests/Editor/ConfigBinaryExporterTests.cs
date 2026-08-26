#if UNITY_EDITOR
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using AutoChess.Editor;
using Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ConfigBinaryExporterTests
{
    private const string SourceRoot = "Assets/Configs/__ConfigBinaryExporterTests";
    private const string OutputRoot = "Assets/Resources/Configs/__ConfigBinaryExporterTests";
    private const string SourceFolder = "Assets/Configs/__ConfigBinaryExporterTests/Nested";
    private const string OutputFolder = "Assets/Resources/Configs/__ConfigBinaryExporterTests/Nested";
    private const string SourceAssetPath = SourceFolder + "/Test.asset";
    private const string OutputAssetPath = OutputFolder + "/Test.bytes";

    [SetUp]
    public void SetUp()
    {
        DeleteAssetIfPresent("Assets/Configs/__ConfigBinaryExporterTests");
        DeleteAssetIfPresent("Assets/Resources/Configs/__ConfigBinaryExporterTests");
        CreateFolder("Assets/Configs", "__ConfigBinaryExporterTests");
        CreateFolder("Assets/Configs/__ConfigBinaryExporterTests", "Nested");

        var source = ScriptableObject.CreateInstance<global::Editor.ChessBaseDataSo>();
        source.ChessId = 42;
        source.Name = "Test Chess";
        source.MaxHp = 500;
        source.Atk = 12;
        source.Range = 3;
        source.MoveSpeed = 4;
        source.AtkSpeed = 5;
        source.WindUpTime = 0.75f;
        AssetDatabase.CreateAsset(source, SourceAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [TearDown]
    public void TearDown()
    {
        DeleteAssetIfPresent("Assets/Configs/__ConfigBinaryExporterTests");
        DeleteAssetIfPresent("Assets/Resources/Configs/__ConfigBinaryExporterTests");
        AssetDatabase.Refresh();
    }

    [Test]
    public void ExportsNestedChessBaseDataAndPreservesFields()
    {
        ConfigBinaryExporter.ExportSummary summary = ConfigBinaryExporter.ExportAll(SourceRoot, OutputRoot);

        Assert.That(summary.ExportedCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(File.Exists(GetProjectAbsolutePath(OutputAssetPath)), Is.True);

        ChessBaseData data = ReadBinary(OutputAssetPath);
        Assert.That(data.ChessId, Is.EqualTo(42));
        Assert.That(data.Name, Is.EqualTo("Test Chess"));
        Assert.That(data.MaxHp, Is.EqualTo(500));
        Assert.That(data.Atk, Is.EqualTo(12));
        Assert.That(data.Range, Is.EqualTo(3));
        Assert.That(data.MoveSpeed, Is.EqualTo(4));
        Assert.That(data.AtkSpeed, Is.EqualTo(5));
        Assert.That(data.WindUpTime, Is.EqualTo(0.75f));
    }

    [Test]
    public void ReexportOverwritesExistingBytes()
    {
        ConfigBinaryExporter.ExportAll(SourceRoot, OutputRoot);

        var source = AssetDatabase.LoadAssetAtPath<global::Editor.ChessBaseDataSo>(SourceAssetPath);
        source.MaxHp = 999;
        EditorUtility.SetDirty(source);
        AssetDatabase.SaveAssets();

        ConfigBinaryExporter.ExportAll(SourceRoot, OutputRoot);

        ChessBaseData data = ReadBinary(OutputAssetPath);
        Assert.That(data.MaxHp, Is.EqualTo(999));
    }

    private static ChessBaseData ReadBinary(string assetPath)
    {
#pragma warning disable SYSLIB0011
        var formatter = new BinaryFormatter();
        using (var stream = File.OpenRead(GetProjectAbsolutePath(assetPath)))
        {
            return (ChessBaseData)formatter.Deserialize(stream);
        }
#pragma warning restore SYSLIB0011
    }

    private static void CreateFolder(string parent, string folderName)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + folderName))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private static void DeleteAssetIfPresent(string assetPath)
    {
        if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null || AssetDatabase.IsValidFolder(assetPath))
        {
            AssetDatabase.DeleteAsset(assetPath);
        }
    }

    private static string GetProjectAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
#endif
