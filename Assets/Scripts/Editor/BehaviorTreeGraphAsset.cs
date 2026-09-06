#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using BehaviorTree;
using NodeCanvas.Framework;
using ParadoxNotion;
using ParadoxNotion.Design;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace AutoChess.Editor
{
    /// <summary>
    /// 仅用于 Unity 编辑器的行为树图资产，实际运行时数据由导出的 bytes 提供。
    /// </summary>
    [CreateAssetMenu(
        fileName = "BehaviorTree",
        menuName = "AutoChess/Behavior Tree Asset",
        order = 0)]
    [GraphInfo(packageName = "AutoChess BehaviorTree")]
    public sealed class BehaviorTreeGraphAsset : Graph
    {
        /// <summary>
        /// 保存编辑器导出目标和版本的附加数据。
        /// </summary>
        [Serializable]
        private sealed class DerivedSerializationData
        {
            /// <summary>上一次导出的 bytes 资产路径。</summary>
            public string exportAssetPath;
        }

        /// <summary>默认的 bytes 资源目录。</summary>
        public const string DefaultBytesFolder = "Assets/Resources/Configs/BehaviorTrees";

        /// <summary>默认的编辑源资产目录。</summary>
        public const string DefaultSourceFolder = "Assets/Configs/BehaviorTrees";

        /// <summary>上一次成功导出的 bytes 资产路径。</summary>
        [NonSerialized]
        private string _exportAssetPath;

        /// <summary>
        /// 获取上一次成功导出的 bytes 资产路径。
        /// </summary>
        public string ExportAssetPath => _exportAssetPath;

        /// <summary>行为树图只允许使用本适配层节点。</summary>
        public override Type baseNodeType => typeof(BehaviorTreeGraphNode);

        /// <summary>编辑器图不需要运行时 Agent。</summary>
        public override bool requiresAgent => false;

        /// <summary>行为树必须存在唯一的 Root 节点。</summary>
        public override bool requiresPrimeNode => true;

        /// <summary>将图作为树处理，使 NodeCanvas 自动维护子节点顺序。</summary>
        public override bool isTree => true;

        /// <summary>树节点从上到下排列。</summary>
        public override PlanarDirection flowDirection => PlanarDirection.Vertical;

        /// <summary>编辑器图不启用 NodeCanvas 黑板覆盖。</summary>
        public override bool allowBlackboardOverrides => false;

        /// <summary>编辑器图不接受黑板变量拖放。</summary>
        public override bool canAcceptVariableDrops => false;

        /// <summary>
        /// 序列化图自身的导出路径。
        /// </summary>
        public override object OnDerivedDataSerialization()
        {
            return new DerivedSerializationData { exportAssetPath = _exportAssetPath };
        }

        /// <summary>
        /// 反序列化图自身的导出路径。
        /// </summary>
        public override void OnDerivedDataDeserialization(object data)
        {
            var derivedData = data as DerivedSerializationData;
            _exportAssetPath = derivedData == null ? null : derivedData.exportAssetPath;
        }

        /// <summary>
        /// 图启用时确保新建资产拥有唯一 Root 节点。
        /// </summary>
        protected override void OnGraphObjectEnable()
        {
            base.OnGraphObjectEnable();
            EnsureRootNode();
        }

        /// <summary>
        /// 在 NodeCanvas GraphEditor 工具栏中显示行为树专用操作。
        /// </summary>
        protected override void OnGraphEditorToolbar()
        {
            base.OnGraphEditorToolbar();

            if (GUILayout.Button(new GUIContent("Validate"), EditorStyles.toolbarButton))
            {
                BehaviorTreeGraphEditorCommands.Validate(this);
            }

            if (GUILayout.Button(new GUIContent("Export Bytes"), EditorStyles.toolbarButton))
            {
                BehaviorTreeGraphEditorCommands.ExportBytes(this);
            }

            if (GUILayout.Button(new GUIContent("Export Bytes As..."), EditorStyles.toolbarButton))
            {
                BehaviorTreeGraphEditorCommands.ExportBytesAs(this);
            }
        }

        /// <summary>
        /// 设置 bytes 导出路径并标记图资产为脏。
        /// </summary>
        public void SetExportAssetPath(string assetPath)
        {
            _exportAssetPath = string.IsNullOrWhiteSpace(assetPath) ? null : assetPath;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// 获取图中唯一的 Root 节点；不存在或重复时返回 null。
        /// </summary>
        public BehaviorTreeRootGraphNode GetUniqueRootNode()
        {
            if (allNodes == null)
            {
                return null;
            }

            var roots = allNodes.OfType<BehaviorTreeRootGraphNode>().ToList();
            return roots.Count == 1 ? roots[0] : null;
        }

        /// <summary>
        /// 新建图时创建 Root；已有图不自动删除用户节点。
        /// </summary>
        private void EnsureRootNode()
        {
            if (allNodes == null)
            {
                allNodes = new List<Node>();
            }

            if (allNodes.OfType<BehaviorTreeRootGraphNode>().Any())
            {
                return;
            }

            var root = AddNode<BehaviorTreeRootGraphNode>(new Vector2(0f, -80f));
            if (root != null)
            {
                root.name = "Root";
            }
        }
    }

    /// <summary>
    /// 行为树图连接类型，复用 NodeCanvas 的连线绘制和交互。
    /// </summary>
    public sealed class BehaviorTreeGraphConnection : Connection
    {
    }

    /// <summary>
    /// 行为树图节点基类，统一端口和 Root 入边约束。
    /// </summary>
    public abstract class BehaviorTreeGraphNode : Node
    {
        /// <summary>行为树节点只能接收一个父节点。</summary>
        public override int maxInConnections => 1;

        /// <summary>行为树节点不能连接自身。</summary>
        public override bool canSelfConnect => false;

        /// <summary>行为树连线使用本项目的连接类型。</summary>
        public override Type outConnectionType => typeof(BehaviorTreeGraphConnection);

        /// <summary>普通节点不作为行为树起点。</summary>
        public override bool allowAsPrime => false;

        /// <summary>节点注释显示在节点右侧。</summary>
        public override Alignment2x2 commentsAlignment => Alignment2x2.Right;

        /// <summary>节点图标显示在标题侧边，便于同时保留自定义名称。</summary>
        public override Alignment2x2 iconAlignment => Alignment2x2.Left;

        /// <summary>
        /// 禁止任何节点连接到 Root，避免形成反向或多根结构。
        /// </summary>
        protected override bool CanConnectToTarget(Node targetNode)
        {
            return !(targetNode is BehaviorTreeRootGraphNode);
        }

        /// <summary>在节点窗口内显示类型标签与关键参数。</summary>
        protected override void OnNodeGUI()
        {
            var body = BehaviorTreeGraphNodePresentation.BuildBodyText(this);
            if (!string.IsNullOrEmpty(body))
            {
                GUILayout.Label(body, GUILayout.MinWidth(120f));
            }
        }

        /// <summary>复合节点的出边显示从 1 开始的子节点序号。</summary>
        public override string GetConnectionInfo(int index)
        {
            return BehaviorTreeGraphNodePresentation.GetConnectionInfo(this, index);
        }

        /// <summary>在图上显示 LogicId 或缺子节点等编辑器警告。</summary>
        protected override string GetWarningOrError()
        {
            return BehaviorTreeGraphNodePresentation.GetWarningOrError(this);
        }
    }

    /// <summary>
    /// 行为树 Root 图节点。
    /// </summary>
    [DoNotList]
    [ProtectedSingleton]
    [Description("行为树唯一入口，最多连接一个子节点。")]
    [ParadoxNotion.Design.Icon(BehaviorTreeGraphNodePresentation.RootIconName)]
    [Color(BehaviorTreeGraphNodePresentation.RootColorHex)]
    public sealed class BehaviorTreeRootGraphNode : BehaviorTreeGraphNode
    {
        /// <summary>Root 不允许父节点。</summary>
        public override int maxInConnections => 0;

        /// <summary>Root 最多包含一个子节点。</summary>
        public override int maxOutConnections => 1;

        /// <summary>Root 是唯一允许作为行为树起点的节点。</summary>
        public override bool allowAsPrime => true;
    }

    /// <summary>
    /// 行为树复合节点基类。
    /// </summary>
    public abstract class BehaviorTreeCompositeGraphNode : BehaviorTreeGraphNode
    {
        /// <summary>复合节点可以连接任意数量的子节点。</summary>
        public override int maxOutConnections => -1;
    }

    /// <summary>
    /// Sequence 图节点。
    /// </summary>
    [Name("Sequence")]
    [Category("Composites")]
    [Description("按从左到右顺序执行子节点，全部成功才成功，任一失败即失败。")]
    [ParadoxNotion.Design.Icon(BehaviorTreeGraphNodePresentation.SequenceIconName)]
    [Color(BehaviorTreeGraphNodePresentation.SequenceColorHex)]
    public sealed class BehaviorTreeSequenceGraphNode : BehaviorTreeCompositeGraphNode
    {
    }

    /// <summary>
    /// Selector 图节点。
    /// </summary>
    [Name("Selector")]
    [Category("Composites")]
    [Description("按从左到右尝试子节点，任一成功即成功，全部失败才失败。")]
    [ParadoxNotion.Design.Icon(BehaviorTreeGraphNodePresentation.SelectorIconName)]
    [Color(BehaviorTreeGraphNodePresentation.SelectorColorHex)]
    public sealed class BehaviorTreeSelectorGraphNode : BehaviorTreeCompositeGraphNode
    {
    }

    /// <summary>
    /// Parallel 图节点，保存运行时使用的成功/失败聚合策略。
    /// </summary>
    [Name("Parallel")]
    [Category("Composites")]
    [Description("同时执行子节点，按成功/失败策略聚合结果。")]
    [ParadoxNotion.Design.Icon(BehaviorTreeGraphNodePresentation.ParallelIconName)]
    [Color(BehaviorTreeGraphNodePresentation.ParallelColorHex)]
    public sealed class BehaviorTreeParallelGraphNode : BehaviorTreeCompositeGraphNode
    {
        /// <summary>Parallel 成功聚合策略。</summary>
        [LabelText("Success Policy")]
        public ParallelSuccessPolicy successPolicy = ParallelSuccessPolicy.All;

        /// <summary>Parallel 失败聚合策略。</summary>
        [LabelText("Failure Policy")]
        public ParallelFailurePolicy failurePolicy = ParallelFailurePolicy.Any;
    }

    /// <summary>
    /// 行为树叶节点基类。
    /// </summary>
    public abstract class BehaviorTreeLeafGraphNode : BehaviorTreeGraphNode
    {
        /// <summary>叶节点不能连接子节点。</summary>
        public override int maxOutConnections => 0;
    }

    /// <summary>
    /// Odin/NodeCanvas 图序列化使用的强类型参数值。
    /// </summary>
    [Serializable]
    public sealed class BehaviorTreeEditorParameter
    {
        /// <summary>传给运行时逻辑的参数 key。</summary>
        [LabelText("Key")]
        public string key;

        /// <summary>参数值类型。</summary>
        [LabelText("Type")]
        public BehaviorTreeParameterType type;

        /// <summary>字符串参数值。</summary>
        [Sirenix.OdinInspector.ShowIf("IsString")]
        [LabelText("Value")]
        public string stringValue;

        /// <summary>整数参数值。</summary>
        [Sirenix.OdinInspector.ShowIf("IsInt")]
        [LabelText("Value")]
        public int intValue;

        /// <summary>浮点参数值。</summary>
        [Sirenix.OdinInspector.ShowIf("IsFloat")]
        [LabelText("Value")]
        public float floatValue;

        /// <summary>布尔参数值。</summary>
        [Sirenix.OdinInspector.ShowIf("IsBool")]
        [LabelText("Value")]
        public bool boolValue;

        /// <summary>
        /// 获取参数是否为字符串。
        /// </summary>
        public bool IsString()
        {
            return type == BehaviorTreeParameterType.String;
        }

        /// <summary>
        /// 获取参数是否为整数。
        /// </summary>
        public bool IsInt()
        {
            return type == BehaviorTreeParameterType.Int;
        }

        /// <summary>
        /// 获取参数是否为浮点数。
        /// </summary>
        public bool IsFloat()
        {
            return type == BehaviorTreeParameterType.Float;
        }

        /// <summary>
        /// 获取参数是否为布尔值。
        /// </summary>
        public bool IsBool()
        {
            return type == BehaviorTreeParameterType.Bool;
        }

        /// <summary>
        /// 将编辑器参数转换为运行时字典值。
        /// </summary>
        public object ToRuntimeValue()
        {
            switch (type)
            {
                case BehaviorTreeParameterType.Int:
                    return intValue;
                case BehaviorTreeParameterType.Float:
                    return floatValue;
                case BehaviorTreeParameterType.Bool:
                    return boolValue;
                default:
                    return stringValue ?? string.Empty;
            }
        }

        /// <summary>
        /// 根据目录参数描述创建默认编辑值。
        /// </summary>
        public static BehaviorTreeEditorParameter FromDefinition(
            BehaviorTreeParameterDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var parameter = new BehaviorTreeEditorParameter
            {
                key = definition.Key,
                type = definition.Type
            };

            switch (definition.Type)
            {
                case BehaviorTreeParameterType.Int:
                    parameter.intValue = definition.DefaultInt;
                    break;
                case BehaviorTreeParameterType.Float:
                    parameter.floatValue = definition.DefaultFloat;
                    break;
                case BehaviorTreeParameterType.Bool:
                    parameter.boolValue = definition.DefaultBool;
                    break;
                default:
                    parameter.stringValue = definition.DefaultString ?? string.Empty;
                    break;
            }

            return parameter;
        }
    }

    /// <summary>
    /// Action 图节点，保存 LogicId 和可编辑参数。
    /// </summary>
    [Name("Action")]
    [Category("Leaves")]
    [Description("执行一次 Action 逻辑并立即返回结果。")]
    [ParadoxNotion.Design.Icon(BehaviorTreeGraphNodePresentation.ActionIconName)]
    [Color(BehaviorTreeGraphNodePresentation.ActionColorHex)]
    public sealed class BehaviorTreeActionGraphNode : BehaviorTreeLeafGraphNode
    {
        /// <summary>绑定运行时 Action 工厂的 LogicId。</summary>
        [HideInInspector]
        public string logicId;

        /// <summary>Action 的强类型编辑参数列表。</summary>
        [HideInInspector]
        public List<BehaviorTreeEditorParameter> parameters =
            new List<BehaviorTreeEditorParameter>();

        /// <summary>获取或设置 Action LogicId。</summary>
        public string LogicId
        {
            get { return logicId; }
            set { logicId = value; }
        }

        /// <summary>获取 Action 编辑参数。</summary>
        public List<BehaviorTreeEditorParameter> Parameters
        {
            get
            {
                if (parameters == null)
                {
                    parameters = new List<BehaviorTreeEditorParameter>();
                }

                return parameters;
            }
        }

#if UNITY_EDITOR
        /// <summary>绘制 Action LogicId 和 Odin 参数 Inspector。</summary>
        protected override void OnNodeInspectorGUI()
        {
            BehaviorTreeGraphInspector.DrawLeafInspector(
                this,
                BehaviorTreeLogicKind.Action,
                logicId,
                Parameters,
                value => logicId = value);
        }
#endif
    }

    /// <summary>
    /// Conditional 图节点，保存 LogicId 和可编辑参数。
    /// </summary>
    [Name("Conditional")]
    [Category("Leaves")]
    [Description("判定条件并返回 Success 或 Failure。")]
    [ParadoxNotion.Design.Icon(BehaviorTreeGraphNodePresentation.ConditionalIconName)]
    [Color(BehaviorTreeGraphNodePresentation.ConditionalColorHex)]
    public sealed class BehaviorTreeConditionalGraphNode : BehaviorTreeLeafGraphNode
    {
        /// <summary>绑定运行时 Conditional 工厂的 LogicId。</summary>
        [HideInInspector]
        public string logicId;

        /// <summary>Conditional 的强类型编辑参数列表。</summary>
        [HideInInspector]
        public List<BehaviorTreeEditorParameter> parameters =
            new List<BehaviorTreeEditorParameter>();

        /// <summary>获取或设置 Conditional LogicId。</summary>
        public string LogicId
        {
            get { return logicId; }
            set { logicId = value; }
        }

        /// <summary>获取 Conditional 编辑参数。</summary>
        public List<BehaviorTreeEditorParameter> Parameters
        {
            get
            {
                if (parameters == null)
                {
                    parameters = new List<BehaviorTreeEditorParameter>();
                }

                return parameters;
            }
        }

#if UNITY_EDITOR
        /// <summary>绘制 Conditional LogicId 和 Odin 参数 Inspector。</summary>
        protected override void OnNodeInspectorGUI()
        {
            BehaviorTreeGraphInspector.DrawLeafInspector(
                this,
                BehaviorTreeLogicKind.Conditional,
                logicId,
                Parameters,
                value => logicId = value);
        }
#endif
    }
}

#endif
