using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class EventBus
{
    private static readonly Dictionary<Type, Delegate> _subscribers = new Dictionary<Type, Delegate>();
	public static void Subscribe<T>(Action<T> handler) where T : struct
	{
		Type eventType=typeof(T);
		if(_subscribers.TryGetValue(eventType, out Delegate exists))
		{
			_subscribers[eventType] = Delegate.Combine(exists, handler);
		}
		else
		{
			_subscribers[eventType]= handler;
		}
	}
	public static void Unsubscribe<T>(Action<T> handler) where T : struct
	{
		Type type=typeof(T);
		if(!_subscribers.TryGetValue(type, out Delegate exists))
			return;
		Delegate after = Delegate.Remove(exists, handler);
		if(after == null)
		{
			_subscribers.Remove(type);
		}
		else
		{
			_subscribers[type] = after;
		}
    }
    public static void Publish<T>(T eventData) where T : struct
	{
		if (!_subscribers.TryGetValue(typeof(T), out var del))
				return;
		var list =del.GetInvocationList();
		for(int i = 0;i < list.Length;i++)
		{
            try
            {
                ((Action<T>)list[i]).Invoke(eventData);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventBus] Handler for {typeof(T).Name} threw: {e}");
            }
        }
	}
    public static void Clear()
    {
        _subscribers.Clear();
    }
}
