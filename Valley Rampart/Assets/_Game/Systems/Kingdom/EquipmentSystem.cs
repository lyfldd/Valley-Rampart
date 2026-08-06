using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 装备系统（3.5 §八 / 实施计划 P1 步骤6；Singleton）。
///
/// 规则（§八）：
///   - 装备厂商产装备；NPC 穿戴 = 属性修正（StatModifier 叠加到职业基础值）+ 职业转变。
///   - 职业转变映射（§八）：战士+重甲=重装战士；战士+盾牌=盾卫；弓手+弩=弩手；
///     法师+法杖=大法师；治疗师+圣物=主教。
///   - 编队不受影响：编队按职能（Occupation）划分，换装备不改变职能、不退出编队
///     （FormationController 持单位引用，改 occupation 不自动退出编队）。
///
/// 数据层实现：穿戴/卸下改 UnitController 运行时属性（Attack/Defense/MaxHp）+ RuntimeOccupation。
/// 装备资产 Resources/Equipment/（EquipmentDef）。法杖/圣物资产若缺失由资产生成步骤补建。
/// </summary>
public class EquipmentSystem : Singleton<EquipmentSystem>
{
    private readonly Dictionary<string, EquipmentDef> _defs = new Dictionary<string, EquipmentDef>();

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        LoadDefs();
    }

    private void LoadDefs()
    {
        _defs.Clear();
        var all = Resources.LoadAll<EquipmentDef>("Equipment");
        foreach (var def in all)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) continue;
            _defs[def.id] = def;
        }
        Debug.Log($"[EquipmentSystem] 加载装备定义 {_defs.Count} 项");
    }

    /// <summary>按 id 取装备定义（Play Mode 同路径 Resources 加载）。</summary>
    public EquipmentDef GetDef(string equipId)
    {
        if (string.IsNullOrEmpty(equipId)) return null;
        if (_defs.Count == 0) LoadDefs();
        return _defs.TryGetValue(equipId, out var def) ? def : null;
    }

    /// <summary>单位当前是否已装备。</summary>
    public bool IsEquipped(UnitController unit) => unit != null && !string.IsNullOrEmpty(unit.EquipId);

    /// <summary>单位已装备的定义（无则 null）。</summary>
    public EquipmentDef GetEquippedDef(UnitController unit) => unit != null ? GetDef(unit.EquipId) : null;

    /// <summary>
    /// 穿戴装备（§八装备驱动职业）。
    /// 校验：未装备 + 职业匹配装备适用范围 → 叠加属性修正 + 职业转变。
    /// </summary>
    public bool TryEquip(UnitController unit, string equipId)
    {
        if (unit == null || unit.Data == null) return false;
        if (IsEquipped(unit)) { Debug.Log("[EquipmentSystem] 已装备，需先卸下"); return false; }
        var def = GetDef(equipId);
        if (def == null) { Debug.Log($"[EquipmentSystem] 装备 {equipId} 不存在"); return false; }

        // 职业匹配校验（§八：战士/弓手/法师/治疗师 + 对应装备）
        Occupation cur = unit.EffectiveOccupation;
        if (cur != def.compatibleWith)
        {
            Debug.Log($"[EquipmentSystem] {cur} 无法装备 {equipId}（需 {def.compatibleWith}）");
            return false;
        }

        // 1. 属性修正（StatModifier 叠加）
        ApplyModifiers(unit, def.modifiers, 1);

        // 2. 职业转变（映射表）
        Occupation target = ResolveProfessionChange(def);
        if (target != cur)
        {
            unit.SetOccupation(target);
            Debug.Log($"[EquipmentSystem] {cur} 装备 {equipId} → 职业转变 {target}");
        }

        unit.EquipId = def.id;
        Debug.Log($"[EquipmentSystem] {unit.Data.occupation} 穿戴 {def.id}，属性修正 攻{def.modifiers.attack}/防{def.modifiers.defense}/血{def.modifiers.maxHp}");
        return true;
    }

    /// <summary>卸下装备（还原属性修正 + 还原职业为装备适用范围基础职业）。</summary>
    public bool TryUnequip(UnitController unit)
    {
        if (unit == null || !IsEquipped(unit)) return false;
        var def = GetDef(unit.EquipId);
        if (def == null) return false;

        // 1. 还原属性修正（反向叠加）
        ApplyModifiers(unit, def.modifiers, -1);

        // 2. 还原职业（装备适用范围即基础职业）
        unit.SetOccupation(def.compatibleWith);

        Debug.Log($"[EquipmentSystem] {unit.Data.occupation} 卸下 {def.id}，还原基础职业 {def.compatibleWith}");
        unit.EquipId = null;
        return true;
    }

    /// <summary>职业转变映射（§八/§13.8 装备驱动职业）。</summary>
    public static Occupation ResolveProfessionChange(EquipmentDef def)
    {
        if (def == null) return Occupation.Civilian;
        switch (def.id)
        {
            case "HeavyArmor": return Occupation.HeavyWarrior;   // 战士+重甲=重装战士
            case "Shield": return Occupation.ShieldGuard;        // 战士+盾=盾卫
            case "Crossbow": return Occupation.Crossbowman;      // 弓手+弩=弩手
            case "Staff": return Occupation.Archmage;            // 法师+法杖=大法师
            case "Relic": return Occupation.Bishop;              // 治疗师+圣物=主教
            default: return def.compatibleWith;                  // 无映射则保持
        }
    }

    /// <summary>叠加/还原属性修正（sign=1 叠加，-1 还原）。只作用于 UnitController 运行时属性。</summary>
    private void ApplyModifiers(UnitController unit, StatModifier m, int sign)
    {
        if (unit == null) return;
        if (m.attack != 0) unit.SetAttack(Mathf.Max(0, unit.Attack + sign * m.attack));
        if (m.defense != 0) unit.SetDefense(Mathf.Max(0, unit.Defense + sign * m.defense));
        if (m.maxHp != 0)
        {
            int newMax = Mathf.Max(1, unit.MaxHp + sign * m.maxHp);
            unit.SetMaxHp(newMax);
        }
        // attackRange/attackCD 属职业快照（AI 战斗侧），本数据层不直接改，留 AI 接入后置。
    }
}