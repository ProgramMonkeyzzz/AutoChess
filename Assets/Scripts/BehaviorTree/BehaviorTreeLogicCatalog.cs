using System;
using System.Collections.Generic;

namespace BehaviorTree
{
    /// <summary>
    /// 可编辑行为树逻辑的节点类别。
    /// </summary>
    public enum BehaviorTreeLogicKind
    {
        /// <summary>Action 逻辑。</summary>
        Action,

        /// <summary>Conditional 逻辑。</summary>
        Conditional
    }

    /// <summary>
    /// 编辑器叶节点参数支持的值类型。
    /// </summary>
    public enum BehaviorTreeParameterType
    {
        /// <summary>字符串参数。</summary>
        String,

        /// <summary>整数参数。</summary>
        Int,

        /// <summary>单精度浮点参数。</summary>
        Float,

        /// <summary>布尔参数。</summary>
        Bool
    }

    /// <summary>
    /// 描述一个可在叶节点中编辑的参数。
    /// </summary>
    [Serializable]
    public sealed class BehaviorTreeParameterDefinition
    {
        /// <summary>
        /// 创建字符串参数描述。
        /// </summary>
        public BehaviorTreeParameterDefinition(
            string key,
            BehaviorTreeParameterType type,
            string displayName = null)
        {
            Key = key;
            Type = type;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName;
        }

        /// <summary>传给运行时逻辑的稳定参数 key。</summary>
        public string Key;

        /// <summary>编辑器显示名称。</summary>
        public string DisplayName;

        /// <summary>参数值类型。</summary>
        public BehaviorTreeParameterType Type;

        /// <summary>字符串类型的默认值。</summary>
        public string DefaultString;

        /// <summary>整数类型的默认值。</summary>
        public int DefaultInt;

        /// <summary>浮点类型的默认值。</summary>
        public float DefaultFloat;

        /// <summary>布尔类型的默认值。</summary>
        public bool DefaultBool;

        /// <summary>
        /// 获取适合写入运行时参数字典的默认值。
        /// </summary>
        public object GetDefaultValue()
        {
            switch (Type)
            {
                case BehaviorTreeParameterType.Int:
                    return DefaultInt;
                case BehaviorTreeParameterType.Float:
                    return DefaultFloat;
                case BehaviorTreeParameterType.Bool:
                    return DefaultBool;
                default:
                    return DefaultString ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// 描述一个可供编辑器选择的 Action 或 Conditional 逻辑。
    /// </summary>
    [Serializable]
    public sealed class BehaviorTreeLogicDefinition
    {
        /// <summary>
        /// 创建逻辑描述。
        /// </summary>
        public BehaviorTreeLogicDefinition(
            BehaviorTreeLogicKind kind,
            string logicId,
            string displayName,
            string category = null,
            IEnumerable<BehaviorTreeParameterDefinition> parameters = null)
        {
            if (string.IsNullOrWhiteSpace(logicId))
            {
                throw new ArgumentException("LogicId 不能为空。 ", nameof(logicId));
            }

            Kind = kind;
            LogicId = logicId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? logicId : displayName;
            Category = category ?? string.Empty;
            Parameters = parameters == null
                ? new List<BehaviorTreeParameterDefinition>()
                : new List<BehaviorTreeParameterDefinition>(parameters);
        }

        /// <summary>逻辑节点类别。</summary>
        public BehaviorTreeLogicKind Kind;

        /// <summary>绑定运行时工厂的稳定标识。</summary>
        public string LogicId;

        /// <summary>编辑器中的显示名称。</summary>
        public string DisplayName;

        /// <summary>编辑器中的分类路径。</summary>
        public string Category;

        /// <summary>逻辑节点的参数描述列表。</summary>
        public List<BehaviorTreeParameterDefinition> Parameters;
    }

    /// <summary>
    /// 保存可编辑 Action/Conditional 逻辑目录的进程级注册表。
    /// </summary>
    public static class BehaviorTreeLogicCatalog
    {
        private static readonly Dictionary<string, BehaviorTreeLogicDefinition> ActionDefinitions =
            new Dictionary<string, BehaviorTreeLogicDefinition>(StringComparer.Ordinal);

        private static readonly Dictionary<string, BehaviorTreeLogicDefinition> ConditionalDefinitions =
            new Dictionary<string, BehaviorTreeLogicDefinition>(StringComparer.Ordinal);

        /// <summary>
        /// 获取已经注册的 Action 逻辑目录。
        /// </summary>
        public static IReadOnlyCollection<BehaviorTreeLogicDefinition> Actions => ActionDefinitions.Values;

        /// <summary>
        /// 获取已经注册的 Conditional 逻辑目录。
        /// </summary>
        public static IReadOnlyCollection<BehaviorTreeLogicDefinition> Conditionals =>
            ConditionalDefinitions.Values;

        /// <summary>
        /// 注册一个 Action 逻辑描述；同类别下重复 LogicId 会抛出异常。
        /// </summary>
        public static void RegisterAction(BehaviorTreeLogicDefinition definition)
        {
            Register(definition, BehaviorTreeLogicKind.Action, ActionDefinitions);
        }

        /// <summary>
        /// 注册一个 Conditional 逻辑描述；同类别下重复 LogicId 会抛出异常。
        /// </summary>
        public static void RegisterConditional(BehaviorTreeLogicDefinition definition)
        {
            Register(definition, BehaviorTreeLogicKind.Conditional, ConditionalDefinitions);
        }

        /// <summary>
        /// 便捷注册 Action 逻辑及其参数描述。
        /// </summary>
        public static void RegisterAction(
            string logicId,
            string displayName,
            string category = null,
            IEnumerable<BehaviorTreeParameterDefinition> parameters = null)
        {
            RegisterAction(new BehaviorTreeLogicDefinition(
                BehaviorTreeLogicKind.Action,
                logicId,
                displayName,
                category,
                parameters));
        }

        /// <summary>
        /// 便捷注册 Conditional 逻辑及其参数描述。
        /// </summary>
        public static void RegisterConditional(
            string logicId,
            string displayName,
            string category = null,
            IEnumerable<BehaviorTreeParameterDefinition> parameters = null)
        {
            RegisterConditional(new BehaviorTreeLogicDefinition(
                BehaviorTreeLogicKind.Conditional,
                logicId,
                displayName,
                category,
                parameters));
        }

        /// <summary>
        /// 尝试读取指定类别和 LogicId 的逻辑描述。
        /// </summary>
        public static bool TryGet(
            BehaviorTreeLogicKind kind,
            string logicId,
            out BehaviorTreeLogicDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(logicId))
            {
                return false;
            }

            return GetDefinitions(kind).TryGetValue(logicId, out definition);
        }

        /// <summary>
        /// 清空当前进程中的逻辑目录，供编辑器域重载和测试使用。
        /// </summary>
        public static void Clear()
        {
            ActionDefinitions.Clear();
            ConditionalDefinitions.Clear();
        }

        /// <summary>
        /// 将描述写入对应类别的目录并校验参数定义。
        /// </summary>
        private static void Register(
            BehaviorTreeLogicDefinition definition,
            BehaviorTreeLogicKind expectedKind,
            Dictionary<string, BehaviorTreeLogicDefinition> definitions)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.Kind != expectedKind)
            {
                throw new ArgumentException("逻辑描述的类别与注册方法不一致。 ", nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(definition.LogicId))
            {
                throw new ArgumentException("LogicId 不能为空。 ", nameof(definition));
            }

            if (definitions.ContainsKey(definition.LogicId))
            {
                throw new InvalidOperationException(
                    $"{expectedKind} LogicId '{definition.LogicId}' 已经注册。 ");
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (definition.Parameters != null)
            {
                foreach (BehaviorTreeParameterDefinition parameter in definition.Parameters)
                {
                    if (parameter == null || string.IsNullOrWhiteSpace(parameter.Key))
                    {
                        throw new ArgumentException("参数 key 不能为空。 ", nameof(definition));
                    }

                    if (!Enum.IsDefined(typeof(BehaviorTreeParameterType), parameter.Type))
                    {
                        throw new ArgumentException(
                            $"参数 '{parameter.Key}' 包含无效的参数类型。 ",
                            nameof(definition));
                    }

                    if (!keys.Add(parameter.Key))
                    {
                        throw new ArgumentException(
                            $"参数 key '{parameter.Key}' 不能重复。 ",
                            nameof(definition));
                    }
                }
            }

            definitions.Add(definition.LogicId, definition);
        }

        /// <summary>
        /// 获取指定类别对应的逻辑字典。
        /// </summary>
        private static Dictionary<string, BehaviorTreeLogicDefinition> GetDefinitions(
            BehaviorTreeLogicKind kind)
        {
            return kind == BehaviorTreeLogicKind.Action
                ? ActionDefinitions
                : ConditionalDefinitions;
        }
    }
}
