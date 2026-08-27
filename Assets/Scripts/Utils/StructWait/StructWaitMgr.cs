using System;
using System.Collections.Generic;
using System.Threading;
using AutoChess.Utils;
using Cysharp.Threading.Tasks;

namespace AutoChess
{
    public class StructWaitMgr : Singleton<StructWaitMgr>
    {
        private readonly object _gate = new object();
        private readonly Dictionary<Type, HashSet<object>> _tcss = new Dictionary<Type, HashSet<object>>();

        public UniTask<T> Wait<T>(CancellationToken cancellationToken = default)
            where T : struct, IStructWaitResult
        {
            var result = new StructWaitResult<T>();
            lock (_gate)
            {
                if (!_tcss.TryGetValue(typeof(T), out var set))
                {
                    set = new HashSet<object>();
                    _tcss.Add(typeof(T), set);
                }

                set.Add(result);
            }

            return WaitCore(result.Tcs, cancellationToken);
        }

        private async UniTask<T> WaitCore<T>(UniTaskCompletionSource<T> tcs,
            CancellationToken cancellationToken) where T : struct, IStructWaitResult
        {
            using (cancellationToken.Register(() => Cancel(tcs, cancellationToken)))
            {
                return await tcs.Task;
            }
        }

        private void Cancel<T>(UniTaskCompletionSource<T> tcs, CancellationToken cancellationToken)
            where T : struct, IStructWaitResult
        {
            bool removed;

            lock (_gate)
            {
                removed = _tcss.TryGetValue(typeof(T), out var set) && set.Remove(tcs);
                if (removed && set.Count == 0)
                {
                    _tcss.Remove(typeof(T));
                }
            }

            if (removed)
            {
                tcs.TrySetCanceled(cancellationToken);
            }
        }

        public void NotifyAll<T>(T result) where T : struct, IStructWaitResult
        {
            HashSet<object> set;

            lock (_gate)
            {
                if (!_tcss.TryGetValue(typeof(T), out set))
                {
                    return;
                }

                _tcss.Remove(typeof(T));
            }

            foreach (var item in set)
            {
                if (item is StructWaitResult<T> waitResult)
                {
                    waitResult.Tcs.TrySetResult(result);
                }
            }
        }

        public void Dispose()
        {
            foreach (KeyValuePair<Type, HashSet<object>> keyValuePair in _tcss)
            {
                foreach (object tcs in keyValuePair.Value)
                {
                    ((IDisposable)tcs).Dispose();
                }
            }
        }
    }
}
