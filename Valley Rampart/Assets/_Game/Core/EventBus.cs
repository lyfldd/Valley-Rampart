using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局事件总线。所有系统间通信通过此总线发布/订阅事件。
/// 事件必须是 struct（值类型），避免 GC 开销。
/// </summary>
public static class EventBus
{
    private static readonly Dictionary<Type, Delegate> _subscribers = new Dictionary<Type, Delegate>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _subscribers.Clear();
    }

    public static void Subscribe<T>(Action<T> handler) where T : struct
    {
        if (handler == null) return;

        Type eventType = typeof(T);

        if (_subscribers.TryGetValue(eventType, out Delegate exists))
        {
            _subscribers[eventType] = Delegate.Combine(exists, handler);
        }
        else
        {
            _subscribers[eventType] = handler;
        }
    }

    public static void Unsubscribe<T>(Action<T> handler) where T : struct
    {
        if (handler == null) return;

        Type type = typeof(T);

        if (!_subscribers.TryGetValue(type, out Delegate exists))
            return;

        Delegate after = Delegate.Remove(exists, handler);

        if (after == null)
        {
            _subscribers.Remove(type);
        }
        else
        {
            _subscribers[type] = after;
        }
    }

    /// <summary>
    /// 发布事件。如果无订阅者，打印警告（帮助排查启动时序问题）。
    /// </summary>
    public static void Publish<T>(T eventData) where T : struct
    {
        if (!_subscribers.TryGetValue(typeof(T), out var del))
        {
            Debug.LogWarning($"[EventBus] 事件 {typeof(T).Name} 无订阅者，事件被丢弃。");
            return;
        }

        Delegate[] list = del.GetInvocationList();

        for (int i = 0; i < list.Length; i++)
        {
            try
            {
                ((Action<T>)list[i]).Invoke(eventData);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventBus] {typeof(T).Name} 的处理器抛出异常: {e}");
            }
        }
    }

    /// <summary>
    /// 检查某事件类型是否有订阅者。
    /// </summary>
    public static bool HasSubscribers<T>() where T : struct
    {
        return _subscribers.ContainsKey(typeof(T));
    }

    public static void Clear()
    {
        _subscribers.Clear();
    }
}
