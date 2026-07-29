using UnityEngine;

/// <summary>
/// 建筑面板（3.3 第七节）。IUIPanel 实现，显示建筑信息 + 升级/拆除按钮。
/// 首版用 OnGUI 临时实现（IMGUI），后续替换为正式 UGUI/UXML。
/// 由 Building.Interact → SetTarget(this) → ShowUI(this) 触发打开。
/// </summary>
public class BuildingPanel : Singleton<BuildingPanel>, IUIPanel
{
    private Building _target;
    private bool _isOpen;
    private Rect _windowRect = new Rect(20, 20, 280, 240);

    /// <summary>设置当前面板目标建筑。</summary>
    public void SetTarget(Building b) { _target = b; }

    // ===== IUIPanel =====

    public void Open(Interactor ctx) { _isOpen = true; }
    public void Close() { _isOpen = false; _target = null; }
    public void Refresh() { /* OnGUI 每帧重绘，无需特殊刷新 */ }

    // ===== OnGUI 临时实现 =====

    void OnGUI()
    {
        if (!_isOpen || _target == null || _target.def == null) return;

        var def = _target.def;
        _windowRect = GUI.Window(0, _windowRect, (id) =>
        {
            GUILayout.Label($"<b>{def.displayName}</b>  Lv.{_target.level}", new GUIStyle(GUI.skin.label) { richText = true });
            if (!string.IsNullOrEmpty(def.description))
                GUILayout.Label(def.description);
            GUILayout.Space(5);

            GUILayout.Label($"HP: {_target.hp}/{_target.maxHp}");
            if (def.combat.maxHp > 0)
                GUILayout.Label($"攻 {def.combat.attack}  防 {def.combat.defense}  射程 {def.combat.range}");
            if (def.producer.rate > 0)
                GUILayout.Label($"产能 {def.producer.rate}/s  上限 {def.producer.capacity}");
            GUILayout.Label($"占地 {_target.cellWidth}格  障碍 {_target.isObstacle}  阵营 {def.faction}");

            GUILayout.Space(10);

            // 升级按钮
            if (_target.isPlayerBuilt && def.levels != null && def.levels.Length > 0 &&
                _target.level - 1 < def.levels.Length)
            {
                var lvCost = def.levels[_target.level - 1].upgradeCost;
                string costStr = $"升级 (金{lvCost.gold} 石{lvCost.stone} 木{lvCost.wood} 粮{lvCost.food})";
                if (GUILayout.Button(costStr))
                {
                    if (RulerController.Instance != null && RulerController.Instance.CanAfford(lvCost))
                    {
                        RulerController.Instance.Spend(lvCost);
                        _target.TryUpgrade();
                    }
                    else
                    {
                        Debug.Log("[BuildingPanel] 资源不足，无法升级");
                    }
                }
            }

            // 拆除按钮（仅玩家建筑 + 可拆）
            if (_target.isPlayerBuilt && def.isDestructible)
            {
                if (GUILayout.Button("拆除 (退50%资源)"))
                {
                    RulerController.Instance?.Refund(def.cost, 0.5f);
                    int maxHp = _target.maxHp;
                    _target.TakeDamage(maxHp); // 触发 Die → Free + Unregister + Destroy
                    Close();
                }
            }

            // 关闭按钮
            if (GUILayout.Button("关闭"))
            {
                UIManager.Instance?.CloseCurrent();
            }

            GUI.DragWindow();
        }, "建筑面板");
    }
}
