using System;
using Cysharp.Threading.Tasks;

namespace AutoChess
{
    public enum EStructWaitResult
    {
        Success,//正常完成
        Dispose,//释放资源
        Cancel,//取消
        Timeout,//超时
    }

    public interface IStructWaitResult
    {
        EStructWaitResult Result { get; set; }
    }

    public class StructWaitResult<T> : IDisposable where T : struct, IStructWaitResult
    {
        public UniTaskCompletionSource<T> Tcs;

        public StructWaitResult()
        {
            Tcs = new UniTaskCompletionSource<T>();
        }

        public void Dispose()
        {
            Tcs.TrySetResult(new T() { Result = EStructWaitResult.Dispose });
        }
    }
}
