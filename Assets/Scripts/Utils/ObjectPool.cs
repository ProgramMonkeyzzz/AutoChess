using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AutoChess.Utils
{
    public interface IPool : IDisposable
    {
        bool IsFromPool { get; set; }
    }

    public class ObjectPool : Singleton<ObjectPool>
    {
        private ConcurrentDictionary<Type, Pool> typePool = new ConcurrentDictionary<Type, Pool>();

        private readonly Func<Type, Pool> AddPoolFunc = _ => new Pool();
        

        public static T Fetch<T>(bool isFromPool = true) where T : class, IPool
        {
            return Fetch(typeof(T), isFromPool) as T;
        }
        
        public static object Fetch(Type type, bool isFromPool = true)
        {
            if (Instance == null)
            {
                return Activator.CreateInstance(type);
            }

            if (!isFromPool)
            {
                return Activator.CreateInstance(type);
            }

            Pool pool = Instance.GetPool(type);
            object obj = pool.Get(type);
            if (obj is IPool p)
            {
                p.IsFromPool = true;
            }

            return obj;
        }

        public static void Recycle<T>(ref T obj) where T : class, IPool
        {
            Recycle(obj);
            obj = null;
        }

        public static void Recycle(object obj)
        {
            if (Instance == null)
            {
                return;
            }

            if (obj is IPool p)
            {
                if (!p.IsFromPool)
                {
                    return;
                }
            }

            Type type = obj.GetType();
            Pool pool = Instance.GetPool(type);
            pool.Return(obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Pool GetPool(Type type)
        {
            return this.typePool.GetOrAdd(type, AddPoolFunc);
        }


#if UNITY_EDITOR
        private class Pool
        {
            private const int MaxNum = 1000;

            private readonly System.Collections.Generic.HashSet<object> items = new();

            public object Get(Type type)
            {
                lock (this)
                {
                    if (this.items.Count <= 0)
                    {
                        return Activator.CreateInstance(type);
                    }

                    object obj = this.items.First();
                    this.items.Remove(obj);
                    return obj;
                }
            }

            public void Return(object obj)
            {
                lock (this)
                {
                    if (this.items.Count >= MaxNum)
                    {
                        return;
                    }

                    if (!this.items.Add(obj))
                    {
                        throw new Exception("object already in pool: " + obj.GetType().FullName);
                    }
                }
            }
        }

#else
        private class Pool
        {
            private const int MaxNum = 1000;
            
            private int numItems;
            private readonly ConcurrentQueue<object> items = new();
            private object fastItem;


            public object Get(Type type)
            {
                object item = this.fastItem;
                if (item == null || Interlocked.CompareExchange(ref this.fastItem, null, item) != item)
                {
                    if (this.items.TryDequeue(out item))
                    {
                        Interlocked.Decrement(ref this.numItems);
                        return item;
                    }

                    return Activator.CreateInstance(type);
                }

                return item;
            }

            public void Return(object obj)
            {
                if (this.fastItem != null || Interlocked.CompareExchange(ref this.fastItem, obj, null) != null)
                {
                    if (Interlocked.Increment(ref this.numItems) <= MaxNum)
                    {
                        this.items.Enqueue(obj);
                        return;
                    }

                    Interlocked.Decrement(ref this.numItems);
                }
            }
        }
#endif
    }
}