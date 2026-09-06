#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Config;
using UnityEditor;
using UnityEngine;

namespace AutoChess.Editor
{
    public static class ConfigBinaryExporter
    {
        private const string SourceRoot = "Assets/Configs";
        private const string OutputRoot = "Assets/Resources/Configs";
        private const string MenuPath = "Tools/Configs/Generate Binary Configs";

        [MenuItem(MenuPath)]
        public static void GenerateBinaryConfigs()
        {
            ExportAll();
        }

        public static ExportSummary ExportAll()
        {
            return ExportAll(SourceRoot, OutputRoot);
        }

        internal static ExportSummary ExportAll(string sourceRoot, string outputRoot)
        {
            if (!AssetDatabase.IsValidFolder(sourceRoot))
            {
                string message = $"Config export skipped because {sourceRoot} does not exist.";
                Debug.LogWarning(message);
                return new ExportSummary(0, 0, new[] { message });
            }

            string[] assetGuids = AssetDatabase.FindAssets("t:ChessBaseDataSo", new[] { sourceRoot });
            if (assetGuids.Length == 0)
            {
                string message = $"Config export found no ChessBaseDataSo assets under {sourceRoot}.";
                Debug.LogWarning(message);
                return new ExportSummary(0, 0, new[] { message });
            }

            int exportedCount = 0;
            int skippedCount = 0;
            var errors = new List<string>();

            foreach (string assetGuid in assetGuids)
            {
                string assetPath = NormalizePath(AssetDatabase.GUIDToAssetPath(assetGuid));
                var source = AssetDatabase.LoadAssetAtPath<global::Editor.ChessBaseDataSo>(assetPath);
                if (source == null)
                {
                    skippedCount++;
                    errors.Add($"Unable to load ChessBaseDataSo at '{assetPath}'.");
                    continue;
                }

                try
                {
                    string outputAssetPath = GetOutputAssetPath(assetPath, sourceRoot, outputRoot);
                    string outputAbsolutePath = GetProjectAbsolutePath(outputAssetPath);
                    string outputDirectory = Path.GetDirectoryName(outputAbsolutePath);
                    if (string.IsNullOrEmpty(outputDirectory))
                    {
                        throw new IOException($"Could not determine output directory for '{outputAssetPath}'.");
                    }

                    Directory.CreateDirectory(outputDirectory);
                    WriteBinary(outputAbsolutePath, ToRuntimeData(source));
                    exportedCount++;
                }
                catch (Exception exception)
                {
                    skippedCount++;
                    errors.Add($"Failed to export '{assetPath}': {exception.Message}");
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var summary = new ExportSummary(exportedCount, skippedCount, errors);
            if (summary.HasErrors)
            {
                Debug.LogWarning($"Config export completed with {summary.ExportedCount} exported and " +
                                 $"{summary.SkippedCount} skipped.\n{string.Join("\n", summary.Errors)}");
            }
            else
            {
                Debug.Log($"Config export completed: {summary.ExportedCount} ChessBaseData files written.");
            }

            return summary;
        }

        /// <summary>
        /// 将编辑器 ScriptableObject 配置转换为运行时序列化数据。
        /// </summary>
        internal static ChessBaseData ToRuntimeData(global::Editor.ChessBaseDataSo source)
        {
            return new ChessBaseData
            {
                ConfigId = source.ConfigId,
                Name = source.Name,
                MaxHp = source.MaxHp,
                Atk = source.Atk,
                Range = source.Range,
                MoveSpeed = source.MoveSpeed,
                AtkSpeed = source.AtkSpeed,
                WindUpTime = source.WindUpTime,
                BulletSpeed = source.BulletSpeed
            };
        }

        private static string GetOutputAssetPath(string sourceAssetPath, string sourceRoot, string outputRoot)
        {
            string relativePath = sourceAssetPath.Substring(sourceRoot.Length).TrimStart('/');
            string outputRelativePath = Path.ChangeExtension(relativePath, ".bytes");
            return NormalizePath(Path.Combine(outputRoot, outputRelativePath));
        }

        private static string GetProjectAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void WriteBinary(string outputPath, ChessBaseData data)
        {
#pragma warning disable SYSLIB0011
            var formatter = new BinaryFormatter();
            using (var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                formatter.Serialize(stream, data);
            }
#pragma warning restore SYSLIB0011
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        public readonly struct ExportSummary
        {
            public ExportSummary(int exportedCount, int skippedCount, IReadOnlyList<string> errors)
            {
                ExportedCount = exportedCount;
                SkippedCount = skippedCount;
                Errors = errors;
            }

            public int ExportedCount { get; }
            public int SkippedCount { get; }
            public IReadOnlyList<string> Errors { get; }
            public bool HasErrors => Errors != null && Errors.Count > 0;
        }
    }
}
#endif
