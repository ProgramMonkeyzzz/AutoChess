using System;
using System.Collections.Generic;
using System.Linq;
using AutoChess.Utils;
using UnityEngine;

namespace AutoChess
{
    public class EventDispatcher : Singleton<EventDispatcher>
    {
        // 存储所有事件类型的监听器
        private readonly Dictionary<Type, List<Delegate>> _listeners = new Dictionary<Type, List<Delegate>>();

        /// <summary>
        /// 注册事件监听器
        /// </summary>
        /// <typeparam name="T">事件类型（必须为struct）</typeparam>
        /// <param name="eventCallback">事件回调函数</param>
        public void RegisterListener<T>(Action<T> eventCallback) where T : struct
        {
            if (eventCallback == null)
                throw new ArgumentNullException(nameof(eventCallback));


            Type eventType = typeof(T);

            if (!_listeners.ContainsKey(eventType))
            {
                _listeners[eventType] = new List<Delegate>();
            }

            // 避免重复添加相同的回调
            if (!_listeners[eventType].Contains(eventCallback))
            {
                _listeners[eventType].Add(eventCallback);
            }
        }

        /// <summary>
        /// 移除事件监听器
        /// </summary>
        /// <typeparam name="T">事件类型（必须为struct）</typeparam>
        /// <param name="eventCallback">要移除的事件回调函数</param>
        public void RemoveListener<T>(Action<T> eventCallback) where T : struct
        {
            if (eventCallback == null)
                throw new ArgumentNullException(nameof(eventCallback));


            Type eventType = typeof(T);

            if (_listeners.TryGetValue(eventType, out var listeners))
            {
                listeners.Remove(eventCallback);

                // 如果没有监听器了，可以选择移除该类型条目（可选）
                if (listeners.Count == 0)
                {
                    _listeners.Remove(eventType);
                }
            }
        }

        /// <summary>
        /// 广播事件
        /// </summary>
        /// <typeparam name="T">事件类型（必须为struct）</typeparam>
        /// <param name="dynamicEvent">事件实例</param>
        public void Send<T>(T dynamicEvent) where T : struct
        {
            Type eventType = typeof(T);
            List<Delegate> listenersCopy = null;


            if (_listeners.TryGetValue(eventType, out var listeners))
            {
                // 创建副本以避免在迭代过程中修改集合
                listenersCopy = listeners.ToList();
            }


            if (listenersCopy != null)
            {
                foreach (var handler in listenersCopy)
                {
                    try
                    {
                        var action = handler as Action<T>;
                        action?.Invoke(dynamicEvent);
                    }
                    catch (Exception ex)
                    {
                        // 处理回调中的异常，避免影响其他监听器
                        Debug.LogError($"[EventDispatcher] Error broadcasting event {eventType}: {ex}");
                        // 可以根据需要选择是否重新抛出或记录日志
                    }
                }
            }
        }

        /// <summary>
        /// 清空所有事件的监听器
        /// </summary>
        public void ClearAll()
        {
            _listeners.Clear();
        }

        /// <summary>
        /// 清空指定事件类型的所有监听器
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        public void ClearListeners<T>() where T : struct
        {
            _listeners.Remove(typeof(T));
        }

        /// <summary>
        /// 获取指定事件的监听器数量
        /// </summary>
        public int GetListenerCount<T>() where T : struct
        {
            if (_listeners.TryGetValue(typeof(T), out var listeners))
            {
                return listeners.Count;
            }

            return 0;
        }
    }
}