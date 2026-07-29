using System;
using UnityEngine;

/// <summary>
/// 通用可交互接口（3.3.1 P3）。任何需要被点击/选择产生交互的实体
/// （建筑/资源点/裂隙/主城/NPC）实现本接口即可接入统一交互派发，无需各自写点击逻辑。
/// </summary>
public interface IInteractable
{
    /// <summary>被点击时调用。返回交互结果（开 UI / 执行动作 / 子菜单）。</summary>
    InteractionResult Interact(Interactor ctx);
}

/// <summary>交互结果类型。</summary>
public enum InteractionKind { ShowUI, DoAction, OpenSubmenu, None }

/// <summary>交互结果。</summary>
public readonly struct InteractionResult
{
    public readonly InteractionKind kind;
    public readonly IUIPanel panel;       // ShowUI：要打开的面板
    public readonly Action action;        // DoAction：要执行的一次性动作

    public InteractionResult(InteractionKind kind, IUIPanel panel = null, Action action = null)
    {
        this.kind = kind;
        this.panel = panel;
        this.action = action;
    }

    public static InteractionResult None => new InteractionResult(InteractionKind.None);
    public static InteractionResult DoAction(Action action) => new InteractionResult(InteractionKind.DoAction, null, action);
    public static InteractionResult ShowUI(IUIPanel panel) => new InteractionResult(InteractionKind.ShowUI, panel);
}

/// <summary>交互发起者上下文。</summary>
public readonly struct Interactor
{
    public readonly Faction faction;      // 发起者阵营（决定敌方建筑选项）
    public readonly Vector3 position;     // 用于距离判定

    public Interactor(Faction faction, Vector3 position)
    {
        this.faction = faction;
        this.position = position;
    }
}
