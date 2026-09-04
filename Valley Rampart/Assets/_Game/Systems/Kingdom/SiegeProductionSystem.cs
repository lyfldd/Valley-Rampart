using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战争机器生产链（3.5 §13.7 / 实施计划 P1 步骤7；Singleton + ISaveable, Global）。
///
/// 投掷机厂（SiegeWorkshop）生产：
///   - 战争机器：投掷机（SiegeMachine）/ 弩炮（Ballista），上限 2 + 每级+2（§13.7），经 UnitFactory 生成放置。
///   - 弹药：由 SiegeWorkshopBuilding 专属组件产出入厂级弹药仓（ResourceType.StoneAmmo 等，HH.19 A×4）。
///
/// 2_12 步骤9 / HH.19 裁决后职责收敛：
///   - 弹药真源已移 SiegeWorkshopBuilding 厂级弹药仓（3 子 StorageComponent）；本类退役原 _ammoStock 全局弹药账
///     （ProjectileType 键）、退役 ResupplySiegeUnit 直填接口、退役 ProduceAmmo 全局生产。
///   - 本体仅保留：战争机器上限/数量查询 + 生产战争机器（ProduceMachine）。
///   - ISaveable 保留（战争机器相关状态占位 / 旧档兼容），旧档 ammoStock 弹药账不再由本类读——由
///     SiegeWorkshopBuilding.RestoreLegacyAmmo 承接迁移（裁决口径2：不丢档）。
/// </summary>
public class SiegeProductionSystem : Singleton<SiegeProductionSystem>, ISaveable
{
    public string SaveId => "SiegeProductionSystem";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    private SiegeProductionConfig _config;

    // ===== 旧档弹药账迁移桥（HH.19 裁决口径2：不丢档）=====
    // SiegeProductionSystem(Global) 先于建筑(Scene)读档——LoadState 读到旧档 ammoStock 先缓存在此，
    // 待 SiegeWorkshopBuilding 厂仓就绪后 RestoreLegacyAmmo 消费并入厂仓，随后归零。
    public static int LegacyStoneAmmo;
    public static int LegacyFireballAmmo;
    public static int LegacyMagicAmmo;

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

    /// <summary>当前已放置战争机器数（场上投掷机+弩炮单位）。玩家路径：按 Faction.PlayerCamp 统计（现网语义不变）。</summary>
    public int GetPlacedMachineCount()
    {
        int count = 0;
        if (UnitRegistry.Instance == null) return count;
        foreach (var unit in UnitRegistry.Instance.GetAllUnits())
        {
            if (unit == null || unit.Data == null) continue;
            if (unit.Data.faction != Faction.PlayerCamp) continue;
            if (IsMachineOccupation(unit.EffectiveOccupation))
                count++;
        }
        return count;
    }

    /// <summary>是否战争机器职业（3.7 投掷机/弩炮 + 2_20 M7 四族专属机器 D497）。</summary>
    private static bool IsMachineOccupation(Occupation occ)
    {
        return occ == Occupation.SiegeMachine || occ == Occupation.Ballista
            || occ == Occupation.Mortar || occ == Occupation.VineCatapult || occ == Occupation.Ram;
    }

    /// <summary>四族专属机器白名单（2_20 M7 D496/D497）：投掷机退役共通槽；弩炮收编人类重弩炮；臼炮/藤蔓/攻城槌各族专属。</summary>
    private static bool IsRaceAllowedMachine(int race, Occupation type)
    {
        switch (type)
        {
            case Occupation.Ballista:     return race == RaceIds.Human;  // 重弩炮=人类专属（D496 收编）
            case Occupation.Mortar:       return race == RaceIds.Dwarf;
            case Occupation.VineCatapult: return race == RaceIds.Elf;
            case Occupation.Ram:          return race == RaceIds.Orc;
            case Occupation.SiegeMachine: return false;                  // 投掷机退役共通槽（D496，枚举保留）
            default: return false;
        }
    }

    /// <summary>机器造价查表（2_20 M7：臼炮最贵梯度，其余沿用弩炮/投掷机量级占位，2_20.1 §8.1）。</summary>
    private ResourcePack GetMachineCost(Occupation type)
    {
        var cfg = Cfg();
        if (cfg == null) return ResourcePack.Zero;
        switch (type)
        {
            case Occupation.Ballista:     return cfg.ballistaCost;
            case Occupation.Mortar:       return cfg.mortarCost;
            case Occupation.VineCatapult: return cfg.vineCatapultCost;
            case Occupation.Ram:          return cfg.ramCost;
            default: return ResourcePack.Zero;
        }
    }

    /// <summary>
    /// 2_17 步骤11 批1·按王国归属统计已放置战争机器数（AI/动态王国口径）。
    /// 只统计不改产：AI 战争机器生产链归步骤13。玩家调用点仍走 GetPlacedMachineCount()（零回归）。
    /// </summary>
    public int GetPlacedMachineCountByKingdom(int kingdomId)
    {
        int count = 0;
        if (UnitRegistry.Instance == null) return count;
        foreach (var unit in UnitRegistry.Instance.GetAllUnits())
        {
            if (unit == null || unit.Data == null) continue;
            if (unit.kingdomId != kingdomId) continue;
            if (IsMachineOccupation(unit.EffectiveOccupation))
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
        // 2_20 M7 D496/D497：per-race 白名单（投掷机退役共通槽；弩炮收编人类重弩炮；各族专属机器）
        int playerRace = KingdomRace.GetKingdomRace(0);
        if (!IsRaceAllowedMachine(playerRace, type))
        {
            Debug.Log($"[SiegeProduction] 生产失败：{type} 非种族 {playerRace} 专属机器（M7 白名单）");
            return false;
        }
        if (GetPlacedMachineCount() >= GetMachineLimit())
        {
            Debug.Log($"[SiegeProduction] 机器已达上限 {GetMachineLimit()}，需升级投掷机厂");
            return false;
        }
        var cfg = Cfg();
        if (cfg == null || RulerController.Instance == null) return false;
        var cost = GetMachineCost(type);
        if (!RulerController.Instance.CanAfford(cost)) { Debug.Log("[SiegeProduction] 资源不足，无法生产机器"); return false; }

        RulerController.Instance.Spend(cost);
        if (UnitFactory.Instance != null)
        {
            UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, type, spawnPos);
            Debug.Log($"[SiegeProduction] 生产 {type}（造价 金{cost.gold} 石{cost.stone} 木{cost.wood}）");
            return true;
        }
        return false;
    }

    /// <summary>
    /// 生产战争机器（AI/王国归属 overload，2_17 步骤13 批C 清偿 D454：能力打通+触发延后军事期）。
    /// 玩家(id=0) 缺省走原 ProduceMachine→Faction.PlayerCamp，零回归。
    /// AI(id&gt;0)：国库扣费 + per-kingdom 上限 + 生成带 kingdomId 单位（SpawnUnit 门面自动覆写 AiKingdom 阵营）。
    /// 触发方（UtilityAction/SiegeWorkshop 前置）属 2_18 军事期内容一并议（HH.42 §四决策点2 裁 A）。
    /// </summary>
    public bool ProduceMachine(Occupation type, Vector2 spawnPos, int kingdomId)
    {
        if (kingdomId == 0) return ProduceMachine(type, spawnPos);   // 玩家缺省走原路径（Faction.PlayerCamp）
        // 2_20 M7 D496/D497：AI per-race 白名单（与玩家同规则，镜像 D399）
        int race = KingdomRace.GetKingdomRace(kingdomId);
        if (!IsRaceAllowedMachine(race, type))
        {
            Debug.Log($"[SiegeProduction] k{kingdomId} 生产失败：{type} 非种族 {race} 专属机器（M7 白名单）");
            return false;
        }

        // per-kingdom 上限（D454：AI 本国战争机器数不超厂上限）
        if (GetPlacedMachineCountByKingdom(kingdomId) >= GetMachineLimit())
        {
            Debug.Log($"[SiegeProduction] k{kingdomId} 战争机器已达上限 {GetMachineLimit()}，需升级投掷机厂");
            return false;
        }
        var cfg = Cfg();
        var kingdom = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(kingdomId) : null;
        if (cfg == null || kingdom == null) return false;
        var cost = GetMachineCost(type);
        if (!kingdom.CanAfford(cost))
        {
            Debug.Log($"[SiegeProduction] k{kingdomId} 资源不足，无法生产 {type}");
            return false;
        }

        kingdom.Spend(cost);   // AI 国库台账扣费（镜像玩家 Spend 语义）
        if (UnitFactory.Instance != null)
        {
            UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, type, spawnPos, kingdomId);
            Debug.Log($"[SiegeProduction] k{kingdomId} 生产 {type}（造价 金{cost.gold} 石{cost.stone} 木{cost.wood}）");
            return true;
        }
        return false;
    }

    // ===== ISaveable, Global =====
    // 2_12 步骤9：本类不再持久化弹药账（真源=SiegeWorkshopBuilding 厂仓，随建筑链保存）。保留 SaveId 但写默认空档。

    public SavePayload SaveState()
    {
        var data = new SiegeProductionSaveData { saveDataVersion = 2 };
        return new SavePayload
        {
            typeName = typeof(SiegeProductionSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 2
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(SiegeProductionSaveData).AssemblyQualifiedName) return;
        var data = JsonUtility.FromJson<SiegeProductionSaveData>(payload.json);
        // v1：旧档弹药账缓存到迁移桥，待厂仓就绪并入（HH.19 裁决口径2：不丢档）。
        if (data.ammoStock != null && data.ammoStock.Length >= 3)
        {
            LegacyStoneAmmo = data.ammoStock[0];
            LegacyFireballAmmo = data.ammoStock[1];
            LegacyMagicAmmo = data.ammoStock[2];
            if (LegacyStoneAmmo + LegacyFireballAmmo + LegacyMagicAmmo > 0)
                Debug.Log($"[SiegeProduction] 旧档弹药账待迁桥：石{LegacyStoneAmmo}/火{LegacyFireballAmmo}/魔{LegacyMagicAmmo}");
        }
    }

    public void ResetState()
    {
        // 无全局弹药账需重置（真源=厂仓，随建筑拆除/销毁）；清空迁移桥缓存。
        LegacyStoneAmmo = LegacyFireballAmmo = LegacyMagicAmmo = 0;
    }
}

/// <summary>战争机器生产存档数据（弹药库存已退役，v2 空架；字段保留供旧档识别）。</summary>
[System.Serializable]
public class SiegeProductionSaveData
{
    public int saveDataVersion = 2;
    public int[] ammoStock;   // v1 [0]=Stone [1]=Fireball [2]=Magic，v2 不再写入/恢复（由厂仓迁移）
}