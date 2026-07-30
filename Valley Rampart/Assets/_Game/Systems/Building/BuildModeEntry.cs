using UnityEngine;

/// <summary>
/// 建造模式虚拟 UI 栈条目（3.3.4 批次2）。
/// 无面板，代表"建造模式"占栈位。入栈激活建造模式，出栈退出建造模式。
/// ESC 关栈顶时，退出建造模式并重新打开返回面板（建造菜单），实现"ESC 返回菜单"。
/// </summary>
public class BuildModeEntry : IUIStackEntry
{
    private readonly BuildingDef _def;
    private readonly IUIPanel _returnTo; // ESC 后返回的面板（建造菜单），null=回普通游戏

    public BuildModeEntry(BuildingDef def, IUIPanel returnTo = null)
    {
        _def = def;
        _returnTo = returnTo;
    }

    public void Open(Interactor ctx)
    {
        BuildController.Instance?.EnterBuildMode(_def);
    }

    public void Close()
    {
        BuildController.Instance?.ExitBuildMode();
        // 退出建造模式后重新打开返回面板（建造菜单）
        if (_returnTo != null && UIManager.Instance != null)
            UIManager.Instance.Push(_returnTo, new Interactor(Faction.Human_Player, Vector3.zero));
    }
}
