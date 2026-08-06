using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战争机器生产链（3.5 §13.7 / 实施计划 P1 步骤7；Singleton + ISaveable, Global）。
///
/// 投掷机厂（SiegeWorkshop）生产：
///   - 战争机器：投掷机（SiegeMachine）/ 弩炮（Ballista），上限 2 + 每级+2（§13.7），经 UnitFactory 生成放置。
///   - 弹药：普通弹（石×1）/ 燃烧弹（火油×1）/ 魔法弹（水晶×1），产出入全局弹药库存。
///
/// 3.7 已实现战争机器战斗逻辑与 Ammo_* 资产；本系统聚焦「经营产出链」：
///   生产机器 → 生成单位；生产弹药 → 入库存 → 供给战争机器（ResupplySiegeUnit 补齐 AmmoFireball/AmmoMagic）。
/// 弹药库存随 SiegeProductionSaveData 持久化。
/// </summary>
public class SiegeProductionSystem : Singleton<SiegeProductionSystem>, ISaveable
{
    public string SaveId => "SiegeProductionSystem";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    private SiegeProductionConfig _config;

    // 弹药库存：ProjectileType → 数量（石/火/魔）
    private readonly Dictionary<ProjectileType, int> _ammoStock = new Dictionary<ProjectileType, int>
    {
        { ProjectileType.Stone, 0 },
        { ProjectileType.Fireball, 0 },
        { ProjectileType.Magic, 0 }
    };

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        _config = Resources.Load<SiegeProductionConfig>("Config/SiegeProductionConfig");
        SaveManager.Instance.RegisterSaveable(this);
    }

    private SiegeProductionConfig Cfg()
    {
        if (_config == null) _config = Resources.Load<SiegeProductionConfig>("Config/SiegeProductionConfig");
        return _config;
    }

    /// <summary>当前投掷机厂等级（无则 0）。</summary>
    public int WorkshopLevel()
    {
        if (BuildingRegistry.Instance == null) return 0;
        var all = BuildingRegistry.Instance.All;
        for (int i = 0; i < all.Count; i++)
        {
            var b = all[i];
            if (b != null && b.def != null && b.def.id == "SiegeWorkshop" && b.IsActive)
                return b.level;
        }
        return 0;
    }

    /// <summary>战争机器上限（§13.7：2 + 每级+2）。</summary>
    public int GetMachineLimit()
    {
        var cfg = Cfg();
        int baseLimit = cfg != null ? cfg.siegeMachineLimitBase : 2;
        int perLevel = cfg != null ? cfg.siegeMachineLimitPerLevel : 2;
        int lv = WorkshopLevel();
        return baseLimit + perLevel * Mathf.Max(0, lv - 1);
    }

    /// <summary>当前已放置战争机器数（场上投掷机+弩炮单位）。</summary>
    public int GetPlacedMachineCount()
    {
        int count = 0;
        if (UnitRegistry.Instance == null) return count;
        foreach (var unit in UnitRegistry.Instance.GetAllUnits())
        {
            if (unit == null || unit.Data == null) continue;
            if (unit.Data.faction != Faction.Human_Player) continue;
            if (unit.EffectiveOccupation == Occupation.SiegeMachine || unit.EffectiveOccupation == Occupation.Ballista)
                count++;
        }
        return count;
    }

    /// <summary>
    /// 生产战争机器（投掷机厂产出，§13.7）。校验上限 + 造价 → 扣费 → 生成单位。
    /// 生成位置由调用方传入（玩家放置点/厂旁）。
    /// </summary>
    public bool ProduceMachine(Occupation type, Vector2 spawnPos)
    {
        if (type != Occupation.SiegeMachine && type != Occupation.Ballista) return false;
        if (GetPlacedMachineCount() >= GetMachineLimit())
        {
            Debug.Log($"[SiegeProduction] 机器已达上限 {GetMachineLimit()}，需升级投掷机厂");
            return false;
        }
        var cfg = Cfg();
        if (cfg == null || RulerController.Instance == null) return false;
        var cost = type == Occupation.SiegeMachine ? cfg.catapultCost : cfg.ballistaCost;
        if (!RulerController.Instance.CanAfford(cost)) { Debug.Log("[SiegeProduction] 资源不足，无法生产机器"); return false; }

        RulerController.Instance.Spend(cost);
        if (UnitFactory.Instance != null)
        {
            UnitFactory.Instance.SpawnUnit(Faction.Human_Player, type, spawnPos);
            Debug.Log($"[SiegeProduction] 生产 {type}（造价 金{cost.gold} 石{cost.stone} 木{cost.wood}）");
            return true;
        }
        return false;
    }

    // ===== 弹药生产 =====

    /// <summary>弹药造价对应的资源消耗（§13.4：普通石/燃烧火油/魔法水晶）。</summary>
    private bool TrySpendAmmoCost(ProjectileType type)
    {
        var cfg = Cfg();
        if (cfg == null || RulerController.Instance == null) return false;
        switch (type)
        {
            case ProjectileType.Fireball:
                if (RulerController.Instance.GetResource(ResourceType.FireOil) < cfg.fireballAmmoCost) return false;
                RulerController.Instance.ModifyResource(ResourceType.FireOil, false, cfg.fireballAmmoCost); return true;
            case ProjectileType.Magic:
                if (RulerController.Instance.GetResource(ResourceType.Crystal) < cfg.magicAmmoCost) return false;
                RulerController.Instance.ModifyResource(ResourceType.Crystal, false, cfg.magicAmmoCost); return true;
            default:
                if (RulerController.Instance.GetResource(ResourceType.Stone) < cfg.stoneAmmoCost) return false;
                RulerController.Instance.ModifyResource(ResourceType.Stone, false, cfg.stoneAmmoCost); return true;
        }
    }

    /// <summary>生产一单位弹药（投掷机厂，§13.7）。成功后入弹药库存。</summary>
    public bool ProduceAmmo(ProjectileType type)
    {
        if (!_ammoStock.ContainsKey(type)) return false;
        if (!TrySpendAmmoCost(type)) { Debug.Log($"[SiegeProduction] 弹药资源不足（{type}）"); return false; }
        _ammoStock[type]++;
        Debug.Log($"[SiegeProduction] 生产 {type} 弹药 ×1，库存 {_ammoStock[type]}");
        return true;
    }

    /// <summary>弹药库存查询。</summary>
    public int GetAmmoStock(ProjectileType type) => _ammoStock.TryGetValue(type, out var v) ? v : 0;

    /// <summary>
    /// 用库存弹药补给战争机器（补齐 AmmoFireball/AmmoMagic）。石弹由 UnitController 自动补给。
    /// 供给失败（库存空）返回 false。
    /// </summary>
    public bool ResupplySiegeUnit(UnitController siege, ProjectileType type)
    {
        if (siege == null || !_ammoStock.TryGetValue(type, out int stock) || stock <= 0) return false;
        if (type == ProjectileType.Fireball) { siege.AmmoFireball++; _ammoStock[type]--; return true; }
        if (type == ProjectileType.Magic) { siege.AmmoMagic++; _ammoStock[type]--; return true; }
        return false;
    }

    // ===== ISaveable, Global =====

    public SavePayload SaveState()
    {
        var data = new SiegeProductionSaveData { saveDataVersion = 1 };
        data.ammoStock = new int[3] { GetAmmoStock(ProjectileType.Stone), GetAmmoStock(ProjectileType.Fireball), GetAmmoStock(ProjectileType.Magic) };
        return new SavePayload
        {
            typeName = typeof(SiegeProductionSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(SiegeProductionSaveData).AssemblyQualifiedName) return;
        var data = JsonUtility.FromJson<SiegeProductionSaveData>(payload.json);
        _ammoStock[ProjectileType.Stone] = data.ammoStock != null && data.ammoStock.Length > 0 ? data.ammoStock[0] : 0;
        _ammoStock[ProjectileType.Fireball] = data.ammoStock != null && data.ammoStock.Length > 1 ? data.ammoStock[1] : 0;
        _ammoStock[ProjectileType.Magic] = data.ammoStock != null && data.ammoStock.Length > 2 ? data.ammoStock[2] : 0;
        Debug.Log($"[SiegeProduction] 读档恢复弹药库存 石{_ammoStock[ProjectileType.Stone]}/火{_ammoStock[ProjectileType.Fireball]}/魔{_ammoStock[ProjectileType.Magic]}");
    }

    public void ResetState()
    {
        _ammoStock[ProjectileType.Stone] = 0;
        _ammoStock[ProjectileType.Fireball] = 0;
        _ammoStock[ProjectileType.Magic] = 0;
    }
}

/// <summary>战争机器生产存档数据（弹药库存）。</summary>
[System.Serializable]
public class SiegeProductionSaveData
{
    public int saveDataVersion = 1;
    public int[] ammoStock;   // [0]=Stone [1]=Fireball [2]=Magic
}