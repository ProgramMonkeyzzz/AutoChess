using System;
using System.Collections.Generic;
using AutoChess.Utils;

namespace BehaviorTree
{
    /// <summary>
    /// 在行为树节点之间共享运行时数据的强类型黑板。
    /// </summary>
    public sealed class Blackboard
    {
        /// <summary>
        /// 直接保存在字典槽位中的黑板变量声明类型和值。
        /// </summary>
        private readonly struct Entry
        {
            /// <summary>
            /// 创建具有固定声明类型的黑板变量条目。
            /// </summary>
            public Entry(Type valueType, object value)
            {
                ValueType = valueType;
                Value = value;
            }

            /// <summary>黑板变量首次写入时确定的声明类型。</summary>
            public Type ValueType { get; }

            /// <summary>黑板变量当前保存的值。</summary>
            public object Value { get; }
        }

        /// <summary>按 key 保存的全部黑板变量。</summary>
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        /// <summary>
        /// 写入或更新一个黑板变量。
        /// </summary>
        public void Set<T>(string key, T value)
        {
            ValidateKey(key);

            Type valueType = typeof(T);
            if (_entries.TryGetValue(key, out Entry entry))
            {
                if (entry.ValueType != valueType)
                {
                    throw new InvalidOperationException(
                        $"黑板变量 '{key}' 已使用类型 {entry.ValueType.FullName}，不能写入类型 {valueType.FullName}。 ");
                }

                if (ShouldWrapValueType(valueType))
                {
                    ((IValue<T>)entry.Value).Value = value;
                }
                else
                {
                    _entries[key] = new Entry(valueType, value);
                }

                return;
            }

            if (ShouldWrapValueType(valueType))
            {
                IValue<T> wrappedValue = FetchValueTypeWrap<T>();
                wrappedValue.Value = value;
                _entries.Add(key, new Entry(valueType, wrappedValue));
                return;
            }

            _entries.Add(key, new Entry(valueType, value));
        }

        /// <summary>
        /// 尝试读取指定类型的黑板变量。
        /// </summary>
        public bool TryGet<T>(string key, out T value)
        {
            ValidateKey(key);

            if (!_entries.TryGetValue(key, out Entry entry))
            {
                value = default(T);
                return false;
            }

            if (entry.ValueType != typeof(T))
            {
                value = default(T);
                return false;
            }

            if (ShouldWrapValueType(entry.ValueType))
            {
                value = ((IValue<T>)entry.Value).Value;
                return true;
            }

            if (entry.Value == null)
            {
                value = default(T);
                return true;
            }

            value = (T)entry.Value;
            return true;
        }

        /// <summary>
        /// 读取指定类型的黑板变量；变量不存在或类型不匹配时抛出异常。
        /// </summary>
        public T Get<T>(string key)
        {
            ValidateKey(key);

            if (!_entries.TryGetValue(key, out Entry entry))
            {
                throw new KeyNotFoundException($"黑板变量 '{key}' 不存在。 ");
            }

            if (entry.ValueType != typeof(T))
            {
                throw new InvalidCastException(
                    $"黑板变量 '{key}' 的类型为 {entry.ValueType.FullName}，不能读取为 {typeof(T).FullName}。 ");
            }

            if (ShouldWrapValueType(entry.ValueType))
            {
                return ((IValue<T>)entry.Value).Value;
            }

            if (entry.Value == null)
            {
                return default(T);
            }

            return (T)entry.Value;
        }

        /// <summary>
        /// 判断黑板中是否存在指定变量。
        /// </summary>
        public bool Contains(string key)
        {
            ValidateKey(key);
            return _entries.ContainsKey(key);
        }

        /// <summary>
        /// 删除指定黑板变量。
        /// </summary>
        public bool Remove(string key)
        {
            ValidateKey(key);

            if (!_entries.TryGetValue(key, out Entry entry))
            {
                return false;
            }

            _entries.Remove(key);
            RecycleValueTypeEntry(entry);
            return true;
        }

        /// <summary>
        /// 清空黑板中的全部变量。
        /// </summary>
        public void Clear()
        {
            foreach (Entry entry in _entries.Values)
            {
                RecycleValueTypeEntry(entry);
            }

            _entries.Clear();
        }

        /// <summary>
        /// 判断指定类型是否使用非可空值类型包装。
        /// </summary>
        private static bool ShouldWrapValueType(Type valueType)
        {
            return valueType.IsValueType && Nullable.GetUnderlyingType(valueType) == null;
        }

        /// <summary>
        /// 从对象池获取当前泛型值类型对应的包装对象。
        /// </summary>
        private static IValue<T> FetchValueTypeWrap<T>()
        {
            return (IValue<T>)ObjectPool.Fetch(ValueTypeWrapTypeCache<T>.WrappedType);
        }

        /// <summary>
        /// 将值类型条目中的池化包装对象重置并归还对象池。
        /// </summary>
        private static void RecycleValueTypeEntry(Entry entry)
        {
            if (!ShouldWrapValueType(entry.ValueType) || entry.Value == null)
            {
                return;
            }

            ((IPool)entry.Value).Dispose();
        }

        /// <summary>
        /// 缓存无约束泛型方法所需的闭合 ValueTypeWrap 类型。
        /// </summary>
        private static class ValueTypeWrapTypeCache<T>
        {
            /// <summary>当前泛型参数对应的闭合包装类型。</summary>
            public static readonly Type WrappedType =
                typeof(ValueTypeWrap<>).MakeGenericType(typeof(T));
        }

        /// <summary>
        /// 验证黑板 key 不为空。
        /// </summary>
        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("黑板变量 key 不能为空。 ", nameof(key));
            }
        }
    }
}
