using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 装备厂装备管理面板（装备厂 id=Armory）。挂在 SampleScene 的 EquipmentPanel GameObject 上（UIDocument）。
/// 实现 IUIPanel，由 BuildingPanel 的「装备管理」按钮 SetTarget 后 Push 入栈，关闭 Pop。
///
/// 显示：装备厂名 + 等级；该厂可产出的全部装备（动态从 Resources/Equipment 加载，不硬编码列表）。
/// 每件装备一张卡片：名称 + 适配职业 + 属性修正（攻/防/血）+ 描述（由修正值拼装，SO 无 description 字段）。
/// 卡片下列出当前可穿戴单位（我方 + 存活 + 职业匹配 + 未装备）各带「装备」按钮；
/// 以及已穿戴该装备的单位各带「卸下」按钮。无人可装备时显示空提示。
///
/// 刷新策略：事件驱动（UnitDiedEvent 单位死亡重建列表）+ 打开时 + 装备/卸下操作后本地 Refresh。
/// 所属系统：EquipmentSystem（穿戴/卸下）/ UnitRegistry（我方单位）/ Building（装备厂等级）。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class EquipmentPanel : MonoBehaviour, IUIPanel
{
    private Building _armory;            // 所属装备厂建筑（提供 def.displayName / 等级）
    private bool _bound;                 // 防止重复绑定
    private bool _visible;               // 面板是否显示（事件回调里判断）

    // ===== UI 元素引用 =====
    private VisualElement _root;
    private Label _titleLabel;
    private VisualElement _equipmentList;
    private Label _emptyHint;
    private Button _closeButton;

    // ===== 对外 API（由 BuildingPanel.OnEquipmentClicked 调 SetTarget → Push）=====

    /// <summary>设置所属装备厂建筑（提供 def.displayName / 等级）。打开前调用。</summary>
    public void SetTarget(Building armory)
    {
        _armory = armory;
    }

    // ===== IUIPanel =====

    public void Open(Interactor ctx)
    {
        if (!_bound) Bind();
        _visible = true;
        Refresh();
        SetVisible(true);
    }

    public void Close()
    {
        _visible = false;
        SetVisible(false);
        _armory = null;
    }

    public void Refresh()
    {
        if (_armory == null || _armory.def == null) return;
        if (_titleLabel != null)
            _titleLabel.text = $"{_armory.def.displayName}（Lv.{_armory.level}）";
        RebuildEquipmentList();
    }

    // ===== Unity 生命周期 =====

    private void OnEnable()
    {
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
        if (!_bound) Bind();
        SetVisible(false);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
        Unbind();
    }

    private void Start()
    {
        if (!_bound) Bind();
    }

    // ===== 事件回调（单位死亡 → 重建列表，剔除已死单位）=====

    private void OnUnitDied(UnitDiedEvent evt)
    {
        if (_visible) Refresh();
    }

    // ===== 列表构建 =====

    /// <summary>重建装备列表；无装备定义时显示空提示。</summary>
    private void RebuildEquipmentList()
    {
        if (_equipmentList == null) return;
        _equipmentList.Clear();

        // 动态加载全部装备定义（按 id 去重，避免重复资产）
        var defs = LoadEquipmentDefs();
        bool any = false;

        foreach (var def in defs)
        {
            var card = CreateEquipmentCard(def);
            if (card != null)
            {
                _equipmentList.Add(card);
                any = true;
            }
        }

        if (_emptyHint != null)
            _emptyHint.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;
    }

    /// <summary>从 Resources/Equipment 加载全部装备定义并按 id 去重（EquipmentSystem 未暴露遍历接口）。</summary>
    private static List<EquipmentDef> LoadEquipmentDefs()
    {
        var result = new List<EquipmentDef>();
        var all = Resources.LoadAll<EquipmentDef>("Equipment");
        var seen = new HashSet<string>();
        foreach (var def in all)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) continue;
            if (seen.Add(def.id)) result.Add(def);
        }
        return result;
    }

    /// <summary>创建单个装备卡片（名称 + 适配职业 + 属性 + 可装备/已穿戴单位）。</summary>
    private VisualElement CreateEquipmentCard(EquipmentDef def)
    {
        var card = new VisualElement();
        card.AddToClassList("equipment-card");

        // 头部：装备名 + 适配职业
        var header = new VisualElement();
        header.AddToClassList("equipment-card-header");
        var name = new Label { text = EquipName(def) };
        name.AddToClassList("equipment-card-name");
        var profession = new Label { text = $"适配：{OccName(def.compatibleWith)}" };
        profession.AddToClassList("equipment-card-profession");
        header.Add(name);
        header.Add(profession);
        card.Add(header);

        // 属性修正（攻/防/血/程/CD，非零才显示）
        var mods = new Label { text = ModsText(def) };
        mods.AddToClassList("equipment-card-mods");
        card.Add(mods);

        // 单位区
        var unitsHost = new VisualElement();
        unitsHost.AddToClassList("equipment-units");

        var equippable = GetEquippableUnits(def);   // 可穿戴（未装备）
        var worn = GetWornUnits(def);               // 已穿戴该装备

        // 可穿戴单位：每个「装备」按钮
        foreach (var unit in equippable)
            unitsHost.Add(CreateEquipRow(unit, def));

        // 已穿戴单位：显示已装备名 + 「卸下」按钮
        foreach (var unit in worn)
            unitsHost.Add(CreateUnequipRow(unit));

        if (equippable.Count == 0 && worn.Count == 0)
        {
            var none = new Label { text = "暂无符合条件单位" };
            none.AddToClassList("equipment-card-empty");
            unitsHost.Add(none);
        }

        card.Add(unitsHost);
        return card;
    }

    /// <summary>当前可穿戴某装备的单位：我方 + 存活 + 职业匹配 + 未装备。</summary>
    private static List<UnitController> GetEquippableUnits(EquipmentDef def)
    {
        var result = new List<UnitController>();
        if (UnitRegistry.Instance == null || EquipmentSystem.Instance == null) return result;
        foreach (var unit in UnitRegistry.Instance.GetAllUnits())
        {
            if (unit == null || unit.Data == null) continue;
            if (unit.Data.faction != Faction.Human_Player) continue;
            if (!unit.IsAlive) continue;
            if (unit.EffectiveOccupation != def.compatibleWith) continue;
            if (EquipmentSystem.Instance.IsEquipped(unit)) continue;
            result.Add(unit);
        }
        return result;
    }

    /// <summary>当前已穿戴某装备的单位（我方 + 存活）。</summary>
    private static List<UnitController> GetWornUnits(EquipmentDef def)
    {
        var result = new List<UnitController>();
        if (UnitRegistry.Instance == null) return result;
        foreach (var unit in UnitRegistry.Instance.GetAllUnits())
        {
            if (unit == null || unit.Data == null) continue;
            if (unit.Data.faction != Faction.Human_Player) continue;
            if (!unit.IsAlive) continue;
            if (string.IsNullOrEmpty(unit.EquipId) || unit.EquipId != def.id) continue;
            result.Add(unit);
        }
        return result;
    }

    /// <summary>创建单个可装备单位行：职业名 + 「装备」按钮。</summary>
    private VisualElement CreateEquipRow(UnitController unit, EquipmentDef def)
    {
        var row = new VisualElement();
        row.AddToClassList("equipment-unit-row");

        var name = new Label { text = OccName(unit.EffectiveOccupation) };
        name.AddToClassList("equipment-unit-name");
        row.Add(name);

        var btn = new Button(() => OnEquipClicked(unit, def.id)) { text = "装备" };
        btn.AddToClassList("equipment-unit-equip");
        row.Add(btn);
        return row;
    }

    /// <summary>创建单个已穿戴单位行：已装备名 + 「卸下」按钮。</summary>
    private VisualElement CreateUnequipRow(UnitController unit)
    {
        var row = new VisualElement();
        row.AddToClassList("equipment-unit-row");

        var equippedDef = EquipmentSystem.Instance != null ? EquipmentSystem.Instance.GetEquippedDef(unit) : null;
        var name = new Label { text = $"{OccName(unit.EffectiveOccupation)}（已装备 {EquipName(equippedDef)}）" };
        name.AddToClassList("equipment-unit-name");
        row.Add(name);

        var btn = new Button(() => OnUnequipClicked(unit)) { text = "卸下" };
        btn.AddToClassList("equipment-unit-unequip");
        row.Add(btn);
        return row;
    }

    // ===== 装备 / 卸下操作 =====

    private void OnEquipClicked(UnitController unit, string equipId)
    {
        if (EquipmentSystem.Instance == null) return;
        if (EquipmentSystem.Instance.TryEquip(unit, equipId))
            Refresh();
        else
            Debug.Log("[EquipmentPanel] 装备失败（职业不符 / 已装备 / 装备不存在）");
    }

    private void OnUnequipClicked(UnitController unit)
    {
        if (EquipmentSystem.Instance == null) return;
        if (EquipmentSystem.Instance.TryUnequip(unit))
            Refresh();
        else
            Debug.Log("[EquipmentPanel] 卸下失败（未装备）");
    }

    // ===== 按钮绑定 / 解绑 =====

    private void Bind()
    {
        if (_bound) return;
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        _root = doc.rootVisualElement;

        _titleLabel = _root.Q<Label>("equipment-title");
        _equipmentList = _root.Q<VisualElement>("equipment-list");
        _emptyHint = _root.Q<Label>("equipment-empty-hint");
        _closeButton = _root.Q<Button>("equipment-close-button");

        if (_closeButton != null) _closeButton.clicked += OnCloseClicked;

        // 标题栏拖动（可拖动窗口，不破坏关闭按钮点击）
        var panel = _root.Q<VisualElement>("equipment-panel");
        var handle = _root.Q<VisualElement>("drag-handle");
        if (panel != null && handle != null) UIDragHelper.Attach(panel, handle);

        _bound = true;
    }

    private void Unbind()
    {
        if (!_bound) return;
        if (_closeButton != null) _closeButton.clicked -= OnCloseClicked;
        _bound = false;
    }

    private void OnCloseClicked()
    {
        UIManager.Instance?.CloseCurrent();
    }

    // ===== 辅助 =====

    /// <summary>装备中文显示名（SO 无 displayName 字段，按 id 映射；未知 id 回退 id）。</summary>
    private static string EquipName(EquipmentDef def)
    {
        if (def == null) return "未知";
        switch (def.id)
        {
            case "Crossbow": return "弩";
            case "HeavyArmor": return "重甲";
            case "Shield": return "盾牌";
            case "Staff": return "法杖";
            case "Relic": return "圣物";
            case "Lance": return "骑枪";
            default: return def.id;
        }
    }

    /// <summary>属性修正文本（攻/防/血/射程/攻速，非零才显示；SO 无 description 字段，以此充当描述）。</summary>
    private static string ModsText(EquipmentDef def)
    {
        var m = def.modifiers;
        var parts = new List<string>();
        if (m.attack != 0) parts.Add($"攻{m.attack:+#;-#;0}");
        if (m.defense != 0) parts.Add($"防{m.defense:+#;-#;0}");
        if (m.maxHp != 0) parts.Add($"血{m.maxHp:+#;-#;0}");
        if (m.attackRange != 0f) parts.Add($"射程{m.attackRange:+#.#;-#.#;0}");
        if (m.attackCD != 0f) parts.Add($"攻速{m.attackCD:+#.#;-#.#;0}");
        return parts.Count > 0 ? $"属性：{string.Join("  ", parts)}" : "属性：无修正";
    }

    /// <summary>职业中文显示名（默认回退 ToString）。</summary>
    private static string OccName(Occupation occ)
    {
        switch (occ)
        {
            case Occupation.Resident: return "居民";
            case Occupation.Worker: return "工人";
            case Occupation.Porter: return "搬运工";
            case Occupation.Vagrant: return "流浪汉";
            case Occupation.Child: return "小孩";
            case Occupation.General: return "将军";
            case Occupation.Archer: return "弓箭手";
            case Occupation.Warrior: return "战士";
            case Occupation.Civilian: return "平民";
            case Occupation.Mage: return "法师";
            case Occupation.Healer: return "治疗师";
            case Occupation.Crossbowman: return "弩手";
            case Occupation.HeavyWarrior: return "重装战士";
            case Occupation.Bishop: return "主教";
            case Occupation.ShieldGuard: return "盾卫";
            case Occupation.Archmage: return "大法师";
            case Occupation.Cavalry: return "骑兵";
            default: return occ.ToString();
        }
    }

    private void SetVisible(bool visible)
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}