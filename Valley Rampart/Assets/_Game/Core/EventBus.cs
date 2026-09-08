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

    /// <summary>白名单种子（唯一维护点；域重载后 ResetStatics 重建）。</summary>
    private static readonly string[] WhitelistSeed = new string[]
    {
        nameof(ExecutorMoveCompleteEvent),   // 执行器移动完成（每单位每次移动，高频）
        nameof(ExecutorArrivedEvent),        // 执行器到达（同上）
        nameof(GameSavedEvent),              // 存档完成（存读档 UI 未常驻订阅时正常丢弃）
    };

    /// <summary>
    /// 无订阅者警告白名单（D563② 静音裁决：名单集中管理——高频/正常空转事件显式登记；
    /// **新增事件默认仍告警**，防白名单变永久消音器。登记即承诺：该事件允许零订阅者丢弃）。
    /// 初始化=静态字段初始化器直接填充（不依赖 Play 态 RuntimeInitializeOnLoadMethod——编辑器域探活/工具态同样生效）。
    /// </summary>
    private static readonly HashSet<string> _noSubscriberWhitelist = new HashSet<string>(WhitelistSeed);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _subscribers.Clear();
        _noSubscriberWhitelist.Clear();
        foreach (var name in WhitelistSeed) _noSubscriberWhitelist.Add(name);
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
            // D563②：白名单内事件静默丢弃；名单外仍告警（新增事件默认可见，防时序问题埋没）
            if (!_noSubscriberWhitelist.Contains(typeof(T).Name))
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
