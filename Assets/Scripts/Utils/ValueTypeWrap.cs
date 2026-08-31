namespace AutoChess.Utils
{
    /// <summary>
    /// 为黑板等运行时容器提供值类型的强类型读写接口。
    /// </summary>
    public interface IValue<T>
    {
        /// <summary>包装的值类型数据。</summary>
        T Value { get; set; }
    }

    /// <summary>
    /// 使用对象池承载值类型，避免值类型在容器中重复装箱。
    /// </summary>
    public class ValueTypeWrap<T> : IValue<T>, IPool where T : struct
    {
        /// <summary>该实例是否由对象池创建。</summary>
        public bool IsFromPool { get; set; }

        /// <summary>当前包装的值类型数据。</summary>
        public T Value { get; set; }

        /// <summary>
        /// 清理包装值并将实例归还到对象池。
        /// </summary>
        public void Dispose()
        {
            Value = default(T);
            ObjectPool.Recycle(this);
        }
    }
}
