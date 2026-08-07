using System;
using UnityEngine;

/// <summary>
/// 统一点击交互接口（3.5.1 §六，E-S8）。通用 NPC 点击交互，预留可扩展：
/// 点击任意实现者 → 收集全部可触发交互 → 按 priority 从高到低取第一个执行。
/// 与 IInteractable（建筑/资源点面板交互）正交：InteractionManager 先走 IInteractable，
/// 未命中再回落 IClickInteractable（轻量表现 + 高频一次性操作：招募/训练/对话）。
/// </summary>
public interface IClickInteractable
{
    /// <summary>注册的交互行为列表（对话/招募/训练...）。实现方应缓存，保证 oneShot 状态跨点击有效。</summary>
    InteractAction[] GetInteractActions();
}

/// <summary>单个交互行为（3.5.1 §6.2）。priority 越大越先响应；oneShot 执行后失效。</summary>
public class InteractAction
{
    public readonly string actionId;      // 交互标识（如 "recruit" / "talk"）
    public readonly int priority;         // 优先级（数字越大越先响应）
    public readonly bool oneShot;         // 是否一次性（执行后失效/降级）

    private readonly Func<bool> _canTrigger;
    private readonly Action _execute;
    private bool _triggered;

    public InteractAction(string actionId, int priority, bool oneShot,
                          Func<bool> canTrigger, Action execute)
    {
        this.actionId = actionId;
        this.priority = priority;
        this.oneShot = oneShot;
        _canTrigger = canTrigger;
        _execute = execute;
    }

    /// <summary>当前是否可触发（一次性已执行过 → false；条件委托 → 委托结果）。</summary>
    public bool CanTrigger()
    {
        if (oneShot && _triggered) return false;
        return _canTrigger == null || _canTrigger();
    }

    /// <summary>触发表现（头顶语句 + 实际效果）。oneShot 执行后标记失效。</summary>
    public void Execute()
    {
        _execute?.Invoke();
        if (oneShot) _triggered = true;
    }
}

/// <summary>点击交互优先级子系统（3.5.1 §6.3）：收集可触发项，priority 最高者执行。</summary>
public static class ClickInteractDispatcher
{
    /// <summary>派发一次点击交互。有动作被执行返回 true。</summary>
    public static bool TryDispatch(IClickInteractable target)
    {
        if (target == null) return false;
        var actions = target.GetInteractActions();
        if (actions == null) return false;

        InteractAction best = null;
        for (int i = 0; i < actions.Length; i++)
        {
            var a = actions[i];
            if (a == null || !a.CanTrigger()) continue;
            if (best == null || a.priority > best.priority) best = a;
        }
        if (best == null) return false;

        best.Execute();
        return true;
    }
}

/// <summary>点击交互优先级常量（3.5.1 §6.3/§6.4 默认档位）。</summary>
public static class InteractPriority
{
    public const int RecruitVagrant = 100;  // 流浪汉招募（一次性，最高）
    public const int TrainResident = 50;    // 居民训练（转职入口）
    public const int Talk = 10;             // 对话（兜底轻量表现）
}

/// <summary>头顶交互语句（交互语言，3.5.1 §6.1）：轻量 TextMesh 气泡，自动消失。</summary>
public static class OverheadSpeech
{
    /// <summary>气泡显示时长（秒，2.5s 自动消失；OverheadSpeechManager 空槽计时共用）。</summary>
    public const float BubbleDuration = 2.5f;
    private const float LOCAL_Y_OFFSET = 1.4f;
    private const string BUBBLE_NAME = "OverheadSpeech";

    /// <summary>
    /// 在单位头顶显示一句交互语（QQQ.2 §需求1 / DR-6：每单位复用覆盖）。
    /// 每单位只保留 1 个气泡，新说话先销毁旧气泡再显示新的，不叠加。
    /// 连点丢话可接受（旧的被覆盖）。host 为 null 静默跳过。
    /// </summary>
    public static void Show(Transform host, string text, float duration = BubbleDuration)
    {
        if (host == null || string.IsNullOrEmpty(text)) return;

        // 复用覆盖：销毁该单位旧的头顶气泡（避免叠加）
        var old = host.Find(BUBBLE_NAME);
        if (old != null) UnityEngine.Object.Destroy(old.gameObject);

        var go = new GameObject(BUBBLE_NAME);
        go.transform.SetParent(host, false);
        go.transform.localPosition = new Vector3(0f, LOCAL_Y_OFFSET, 0f);

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.characterSize = 0.12f;
        tm.fontSize = 48;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 100;

        UnityEngine.Object.Destroy(go, duration);
    }
}
