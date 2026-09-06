#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BehaviorTree;
using NodeCanvas.Editor;
using ParadoxNotion.Design;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace AutoChess.Editor
{
    /// <summary>
    /// 使用 Odin 绘制行为树叶节点的 LogicId 和参数。
    /// </summary>
    internal static class BehaviorTreeGraphInspector
    {
        /// <summary>缓存的参数 PropertyTree，避免 GraphEditor 在 Layout/Repaint 之间重建导致 GUILayout 失配。</summary>
        private static PropertyTree _parameterTree;

        /// <summary>当前缓存 PropertyTree 绑定的叶节点。</summary>
        private static BehaviorTreeGraphNode _parameterTreeNode;

        /// <summary>当前缓存 PropertyTree 绑定的参数列表实例。</summary>
        private static List<BehaviorTreeEditorParameter> _parameterTreeList;

        /// <summary>
        /// 在程序集卸载前释放缓存的 PropertyTree。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void RegisterParameterTreeCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= DisposeParameterTree;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeParameterTree;
        }

        /// <summary>
        /// 绘制叶节点 Inspector，并在 LogicId 变更后同步目录默认参数。
        /// </summary>
        public static void DrawLeafInspector(
            BehaviorTreeGraphNode node,
            BehaviorTreeLogicKind kind,
            string logicId,
            List<BehaviorTreeEditorParameter> parameters,
            Action<string> setLogicId)
        {
            var definitions = GetDefinitions(kind)
                .OrderBy(definition => definition.Category)
                .ThenBy(definition => definition.DisplayName)
                .ToList();

            var labels = new string[definitions.Count + 1];
            labels[0] = "Custom LogicId";
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                labels[i + 1] = string.IsNullOrWhiteSpace(definition.Category)
                    ? definition.DisplayName
                    : definition.Category + "/" + definition.DisplayName;
            }

            var selectedIndex = 0;
            for (var i = 0; i < definitions.Count; i++)
            {
                if (string.Equals(definitions[i].LogicId, logicId, StringComparison.Ordinal))
                {
                    selectedIndex = i + 1;
                    break;
                }
            }

            EditorGUI.BeginChangeCheck();
            var newSelectedIndex = EditorGUILayout.Popup("Logic", selectedIndex, labels);
            if (EditorGUI.EndChangeCheck() && newSelectedIndex > 0)
            {
                var selectedDefinition = definitions[newSelectedIndex - 1];
                setLogicId(selectedDefinition.LogicId);
                SynchronizeParameters(selectedDefinition, parameters);
                logicId = selectedDefinition.LogicId;
            }

            EditorGUI.BeginChangeCheck();
            var newLogicId = EditorGUILayout.TextField("LogicId", logicId ?? string.Empty);
            var logicChanged = EditorGUI.EndChangeCheck();
            if (logicChanged)
            {
                setLogicId(newLogicId);
                logicId = newLogicId;
            }

            if (BehaviorTreeLogicCatalog.TryGet(kind, logicId, out var knownDefinition))
            {
                SynchronizeParameters(knownDefinition, parameters);
            }
            else if (!string.IsNullOrWhiteSpace(logicId))
            {
                EditorGUILayout.HelpBox(
                    "LogicId 未在 BehaviorTreeLogicCatalog 注册，将按手动参数导出。",
                    MessageType.Warning);
            }

            EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            DrawParameterList(node, parameters);
            if (GUILayout.Button("Add Parameter", EditorStyles.miniButton))
            {
                parameters.Add(new BehaviorTreeEditorParameter
                {
                    key = "Parameter",
                    type = BehaviorTreeParameterType.String,
                    stringValue = string.Empty
                });
            }

            var parametersChanged = EditorGUI.EndChangeCheck();
            if (logicChanged || parametersChanged || newSelectedIndex != selectedIndex)
            {
                EditorUtility.SetDirty(node.graph);
                node.graph.SelfSerialize();
            }
        }

        /// <summary>
        /// 为参数列表创建 Odin PropertyTree；始终以列表宿主作为唯一 target，允许空列表。
        /// </summary>
        internal static PropertyTree CreateParameterPropertyTree(
            List<BehaviorTreeEditorParameter> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            return PropertyTree.Create(new ParameterListHost { Parameters = parameters });
        }

        /// <summary>
        /// 获取指定类别的可编辑逻辑目录。
        /// </summary>
        private static IEnumerable<BehaviorTreeLogicDefinition> GetDefinitions(
            BehaviorTreeLogicKind kind)
        {
            return kind == BehaviorTreeLogicKind.Action
                ? BehaviorTreeLogicCatalog.Actions
                : BehaviorTreeLogicCatalog.Conditionals;
        }

        /// <summary>
        /// 绘制并缓存参数列表的 PropertyTree。
        /// </summary>
        private static void DrawParameterList(
            BehaviorTreeGraphNode node,
            List<BehaviorTreeEditorParameter> parameters)
        {
            if (parameters == null)
            {
                return;
            }

            if (_parameterTree == null
                || !ReferenceEquals(_parameterTreeNode, node)
                || !ReferenceEquals(_parameterTreeList, parameters))
            {
                DisposeParameterTree();
                _parameterTree = CreateParameterPropertyTree(parameters);
                _parameterTreeNode = node;
                _parameterTreeList = parameters;
            }

            _parameterTree.UpdateTree();
            _parameterTree.Draw(false);
        }

        /// <summary>
        /// 释放缓存的参数 PropertyTree。
        /// </summary>
        private static void DisposeParameterTree()
        {
            if (_parameterTree != null)
            {
                _parameterTree.Dispose();
                _parameterTree = null;
            }

            _parameterTreeNode = null;
            _parameterTreeList = null;
        }

        /// <summary>
        /// 将目录中缺少的参数添加到叶节点并更新已知参数类型。
        /// </summary>
        private static void SynchronizeParameters(
            BehaviorTreeLogicDefinition definition,
            List<BehaviorTreeEditorParameter> parameters)
        {
            if (definition == null || parameters == null || definition.Parameters == null)
            {
                return;
            }

            foreach (BehaviorTreeParameterDefinition parameterDefinition in definition.Parameters)
            {
                var parameter = parameters.FirstOrDefault(item =>
                    item != null && string.Equals(item.key, parameterDefinition.Key, StringComparison.Ordinal));
                if (parameter == null)
                {
                    parameters.Add(BehaviorTreeEditorParameter.FromDefinition(parameterDefinition));
                    continue;
                }

                parameter.type = parameterDefinition.Type;
            }
        }

        /// <summary>
        /// Odin 绘制参数列表时使用的宿主，避免把空 List 当作 PropertyTree 的多目标。
        /// </summary>
        [Serializable]
        private sealed class ParameterListHost
        {
            /// <summary>当前叶节点的可编辑参数。</summary>
            [HideLabel]
            [ListDrawerSettings(
                Expanded = true,
                DraggableItems = true,
                ShowPaging = false,
                ShowFoldout = false,
                HideAddButton = true,
                ListElementLabelName = "key")]
            public List<BehaviorTreeEditorParameter> Parameters;
        }
    }

    /// <summary>
    /// 将行为树图定义导出为可被 Resources 加载的 bytes 文件。
    /// </summary>
    public static class BehaviorTreeGraphExporter
    {
        /// <summary>
        /// 使用图中记录的路径导出；首次导出时退化为另存为流程。
        /// </summary>
        public static bool Export(BehaviorTreeGraphAsset graph, out string error)
        {
            error = null;
            if (graph == null)
            {
                error = "行为树图不能为空。 ";
                return false;
            }

            var path = graph.ExportAssetPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return ExportAs(graph, out error);
            }

            return ExportToPath(graph, path, out error);
        }

        /// <summary>
        /// 弹出 Unity 项目内文件选择器并导出 bytes。
        /// </summary>
        public static bool ExportAs(BehaviorTreeGraphAsset graph, out string error)
        {
            error = null;
            if (graph == null)
            {
                error = "行为树图不能为空。 ";
                return false;
            }

            EnsureFolder(BehaviorTreeGraphAsset.DefaultBytesFolder);
            var defaultName = string.IsNullOrWhiteSpace(graph.name) ? "BehaviorTree" : graph.name;
            var path = EditorUtility.SaveFilePanelInProject(
                "Export BehaviorTree Bytes",
                defaultName,
                "bytes",
                "选择行为树 bytes 导出路径",
                BehaviorTreeGraphAsset.DefaultBytesFolder);
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            return ExportToPath(graph, path, out error);
        }

        /// <summary>
        /// 将图编译并写入指定的 Unity 项目资产路径。
        /// </summary>
        public static bool ExportToPath(
            BehaviorTreeGraphAsset graph,
            string assetPath,
            out string error)
        {
            error = null;
            if (!TryValidateAssetPath(assetPath, out error))
            {
                return false;
            }

            if (!BehaviorTreeGraphCompiler.TryBuildDefinition(
                    graph,
                    out var definition,
                    out var compileErrors))
            {
                error = string.Join("\n", compileErrors);
                return false;
            }

            try
            {
                var bytes = BehaviorTreeBinarySerializer.Serialize(definition);
                var absolutePath = GetProjectAbsolutePath(assetPath);
                var directory = Path.GetDirectoryName(absolutePath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    error = "无法确定 bytes 输出目录。 ";
                    return false;
                }

                Directory.CreateDirectory(directory);
                File.WriteAllBytes(absolutePath, bytes);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                graph.SetExportAssetPath(assetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"行为树 bytes 导出成功：{assetPath}", graph);
                return true;
            }
            catch (Exception exception)
            {
                error = $"导出行为树 bytes 失败：{exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// 校验 Unity 项目内 bytes 路径。
        /// </summary>
        private static bool TryValidateAssetPath(string assetPath, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                error = "bytes 路径不能为空。 ";
                return false;
            }

            var normalizedPath = assetPath.Replace('\\', '/');
            if (!normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.Contains("../", StringComparison.Ordinal)
                || !string.Equals(Path.GetExtension(normalizedPath), ".bytes", StringComparison.OrdinalIgnoreCase))
            {
                error = "bytes 路径必须是 Assets 下且扩展名为 .bytes。 ";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 确保 Unity 项目目录存在。
        /// </summary>
        private static void EnsureFolder(string assetFolder)
        {
            var absoluteFolder = GetProjectAbsolutePath(assetFolder);
            Directory.CreateDirectory(absoluteFolder);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 将 Unity 资产路径转换为本机绝对路径。
        /// </summary>
        private static string GetProjectAbsolutePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
            {
                throw new IOException("无法确定 Unity 项目根目录。 ");
            }

            return Path.Combine(
                projectRoot.FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }

    /// <summary>
    /// 提供行为树资产创建、校验和导出的 Unity 菜单命令。
    /// </summary>
    public static class BehaviorTreeGraphEditorCommands
    {
        /// <summary>
        /// 创建行为树源资产并立即打开 NodeCanvas GraphEditor。
        /// </summary>
        [MenuItem("Tools/AutoChess/Behavior Tree/Create Asset", priority = 0)]
        public static void CreateAsset()
        {
            EnsureSourceFolder();
            var path = EditorUtility.SaveFilePanelInProject(
                "Create BehaviorTree Asset",
                "BehaviorTree",
                "asset",
                "选择行为树源资产路径",
                BehaviorTreeGraphAsset.DefaultSourceFolder);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var graph = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
            graph.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = graph;
            EditorGUIUtility.PingObject(graph);
            GraphEditor.OpenWindow(graph);
        }

        /// <summary>
        /// 校验当前行为树图并显示结构错误。
        /// </summary>
        public static void Validate(BehaviorTreeGraphAsset graph)
        {
            if (!BehaviorTreeGraphCompiler.TryBuildDefinition(
                    graph,
                    out var definition,
                    out var compileErrors))
            {
                ShowErrors("行为树校验失败", compileErrors);
                return;
            }

            try
            {
                BehaviorTreeBinarySerializer.Serialize(definition);
                EditorUtility.DisplayDialog("行为树校验", "行为树结构和参数校验通过。", "确定");
            }
            catch (Exception exception)
            {
                ShowErrors("行为树序列化校验失败", new[] { exception.Message });
            }
        }

        /// <summary>
        /// 导出当前图记录的 bytes 路径或启动另存为流程。
        /// </summary>
        public static void ExportBytes(BehaviorTreeGraphAsset graph)
        {
            if (!BehaviorTreeGraphExporter.Export(graph, out var error) && !string.IsNullOrWhiteSpace(error))
            {
                ShowErrors("行为树导出失败", new[] { error });
            }
        }

        /// <summary>
        /// 通过项目文件选择器另存当前图的 bytes 文件。
        /// </summary>
        public static void ExportBytesAs(BehaviorTreeGraphAsset graph)
        {
            if (!BehaviorTreeGraphExporter.ExportAs(graph, out var error) && !string.IsNullOrWhiteSpace(error))
            {
                ShowErrors("行为树导出失败", new[] { error });
            }
        }

        /// <summary>
        /// 确保行为树源资产目录存在并刷新 AssetDatabase。
        /// </summary>
        private static void EnsureSourceFolder()
        {
            var projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
            {
                return;
            }

            var absoluteFolder = Path.Combine(
                projectRoot.FullName,
                BehaviorTreeGraphAsset.DefaultSourceFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(absoluteFolder);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 将错误列表写入 Console 并显示给编辑器用户。
        /// </summary>
        private static void ShowErrors(string title, IEnumerable<string> errors)
        {
            var message = string.Join("\n", errors.Where(error => !string.IsNullOrWhiteSpace(error)));
            Debug.LogError($"{title}：\n{message}");
            EditorUtility.DisplayDialog(title, message, "确定");
        }
    }
}

#endif
