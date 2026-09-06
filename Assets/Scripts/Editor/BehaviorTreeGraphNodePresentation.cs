#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BehaviorTree;

namespace AutoChess.Editor
{
    /// <summary>
    /// 为行为树图节点生成类型标签、节点正文和警告文本，供 GraphEditor 绘制与测试断言使用。
    /// </summary>
    public static class BehaviorTreeGraphNodePresentation
    {
        /// <summary>Root 节点标题栏颜色。</summary>
        public const string RootColorHex = "ffd54f";

        /// <summary>Sequence 节点标题栏颜色。</summary>
        public const string SequenceColorHex = "bf7fff";

        /// <summary>Selector 节点标题栏颜色。</summary>
        public const string SelectorColorHex = "b3ff7f";

        /// <summary>Parallel 节点标题栏颜色。</summary>
        public const string ParallelColorHex = "ff64cb";

        /// <summary>Action 节点标题栏颜色。</summary>
        public const string ActionColorHex = "ff9f43";

        /// <summary>Conditional 节点标题栏颜色。</summary>
        public const string ConditionalColorHex = "4dd0e1";

        /// <summary>Root 节点图标资源名。</summary>
        public const string RootIconName = "BT";

        /// <summary>Sequence 节点图标资源名。</summary>
        public const string SequenceIconName = "Sequencer";

        /// <summary>Selector 节点图标资源名。</summary>
        public const string SelectorIconName = "Selector";

        /// <summary>Parallel 节点图标资源名。</summary>
        public const string ParallelIconName = "Parallel";

        /// <summary>Action 节点图标资源名。</summary>
        public const string ActionIconName = "Action";

        /// <summary>Conditional 节点图标资源名。</summary>
        public const string ConditionalIconName = "Condition";

        /// <summary>Root 类型标签。</summary>
        public const string RootTypeLabel = "ROOT";

        /// <summary>Sequence 类型标签。</summary>
        public const string SequenceTypeLabel = "SEQUENCE";

        /// <summary>Selector 类型标签。</summary>
        public const string SelectorTypeLabel = "SELECTOR";

        /// <summary>Parallel 类型标签。</summary>
        public const string ParallelTypeLabel = "PARALLEL";

        /// <summary>Action 类型标签。</summary>
        public const string ActionTypeLabel = "ACTION";

        /// <summary>Conditional 类型标签。</summary>
        public const string ConditionalTypeLabel = "CONDITION";

        /// <summary>叶节点未填写 LogicId 时的硬错误。</summary>
        public const string MissingLogicIdError = "* LogicId 不能为空。";

        /// <summary>叶节点 LogicId 未登记到目录时的警告。</summary>
        public const string UnknownLogicIdWarning = "LogicId 未在 BehaviorTreeLogicCatalog 注册。";

        /// <summary>复合节点或 Root 没有子节点时的警告。</summary>
        public const string MissingChildrenWarning = "没有子节点。";

        /// <summary>
        /// 返回节点在图中显示的类型标签，自定义名称不会覆盖该标签。
        /// </summary>
        public static string GetTypeLabel(BehaviorTreeGraphNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (node is BehaviorTreeRootGraphNode)
            {
                return RootTypeLabel;
            }

            if (node is BehaviorTreeSequenceGraphNode)
            {
                return SequenceTypeLabel;
            }

            if (node is BehaviorTreeSelectorGraphNode)
            {
                return SelectorTypeLabel;
            }

            if (node is BehaviorTreeParallelGraphNode)
            {
                return ParallelTypeLabel;
            }

            if (node is BehaviorTreeActionGraphNode)
            {
                return ActionTypeLabel;
            }

            if (node is BehaviorTreeConditionalGraphNode)
            {
                return ConditionalTypeLabel;
            }

            return node.GetType().Name;
        }

        /// <summary>
        /// 构建节点窗口正文：类型标签、Parallel 策略、叶节点 LogicId 与参数。
        /// </summary>
        public static string BuildBodyText(BehaviorTreeGraphNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            var builder = new StringBuilder();
            builder.Append("<size=10><b>");
            builder.Append(GetTypeLabel(node));
            builder.Append("</b></size>");

            var parallel = node as BehaviorTreeParallelGraphNode;
            if (parallel != null)
            {
                builder.Append("\n<size=9>Success ");
                builder.Append(parallel.successPolicy);
                builder.Append("</size>");
                builder.Append("\n<size=9>Failure ");
                builder.Append(parallel.failurePolicy);
                builder.Append("</size>");
            }

            var action = node as BehaviorTreeActionGraphNode;
            if (action != null)
            {
                AppendLeafDetails(builder, node.name, action.LogicId, action.Parameters);
            }

            var conditional = node as BehaviorTreeConditionalGraphNode;
            if (conditional != null)
            {
                AppendLeafDetails(builder, node.name, conditional.LogicId, conditional.Parameters);
            }

            return builder.ToString();
        }

        /// <summary>
        /// 复合节点在连线上显示从 1 开始的子节点序号；子节点不足 2 个时不显示。
        /// </summary>
        public static string GetConnectionInfo(BehaviorTreeGraphNode node, int index)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (!(node is BehaviorTreeCompositeGraphNode))
            {
                return null;
            }

            if (node.outConnections == null || node.outConnections.Count <= 1)
            {
                return null;
            }

            if (index < 0 || index >= node.outConnections.Count)
            {
                return null;
            }

            return (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 返回节点警告或硬错误；没有问题时返回 null。
        /// </summary>
        public static string GetWarningOrError(BehaviorTreeGraphNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            var action = node as BehaviorTreeActionGraphNode;
            if (action != null)
            {
                return GetLeafWarningOrError(BehaviorTreeLogicKind.Action, action.LogicId);
            }

            var conditional = node as BehaviorTreeConditionalGraphNode;
            if (conditional != null)
            {
                return GetLeafWarningOrError(BehaviorTreeLogicKind.Conditional, conditional.LogicId);
            }

            if (node.outConnections == null || node.outConnections.Count == 0)
            {
                return MissingChildrenWarning;
            }

            return null;
        }

        /// <summary>
        /// 将叶节点 LogicId 与参数追加到节点正文，LogicId 与自定义名称相同时不重复显示。
        /// </summary>
        private static void AppendLeafDetails(
            StringBuilder builder,
            string nodeName,
            string logicId,
            IList<BehaviorTreeEditorParameter> parameters)
        {
            if (string.IsNullOrWhiteSpace(logicId))
            {
                builder.Append("\n<size=9><i>未指定 LogicId</i></size>");
            }
            else if (!string.Equals(logicId, nodeName, StringComparison.Ordinal))
            {
                builder.Append("\n<size=9>");
                builder.Append(logicId);
                builder.Append("</size>");
            }

            if (parameters == null)
            {
                return;
            }

            for (var i = 0; i < parameters.Count; i++)
            {
                var formatted = FormatParameter(parameters[i]);
                if (string.IsNullOrEmpty(formatted))
                {
                    continue;
                }

                builder.Append("\n<size=9>");
                builder.Append(formatted);
                builder.Append("</size>");
            }
        }

        /// <summary>
        /// 将编辑器参数格式化为 key=value，供节点窗口阅读。
        /// </summary>
        public static string FormatParameter(BehaviorTreeEditorParameter parameter)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.key))
            {
                return null;
            }

            string value;
            switch (parameter.type)
            {
                case BehaviorTreeParameterType.Int:
                    value = parameter.intValue.ToString(CultureInfo.InvariantCulture);
                    break;
                case BehaviorTreeParameterType.Float:
                    value = parameter.floatValue.ToString("0.##", CultureInfo.InvariantCulture);
                    break;
                case BehaviorTreeParameterType.Bool:
                    value = parameter.boolValue ? "true" : "false";
                    break;
                default:
                    value = parameter.stringValue ?? string.Empty;
                    break;
            }

            return parameter.key + "=" + value;
        }

        /// <summary>
        /// 检查叶节点 LogicId 是否为空或未登记。
        /// </summary>
        private static string GetLeafWarningOrError(BehaviorTreeLogicKind kind, string logicId)
        {
            if (string.IsNullOrWhiteSpace(logicId))
            {
                return MissingLogicIdError;
            }

            if (!BehaviorTreeLogicCatalog.TryGet(kind, logicId, out _))
            {
                return UnknownLogicIdWarning;
            }

            return null;
        }
    }
}

#endif
