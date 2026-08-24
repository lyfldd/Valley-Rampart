using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 王国经营管理器（3.5 实施计划 P0 步骤1，Singleton + ISaveable, Global）。
/// 职责：主城等级（0-6）+ 六大模块等级 + 主城解锁表（跨级解锁）+ 建筑解锁判定 + 贸易额度。
///
/// 解锁机制（3.5 §2.1）：
///   - 主城升级 → 按 CastleUnlockTable 逐行比对 requiredCastleLevel → 跨级解锁模块等级（科技 2/4/6）。
///   - 建筑解锁两条规则：可升级建筑（基础）模块级达标即可建/升级（建筑等级 ≤ 模块级）；
///     特殊建筑（unlockBuildings）模块级解锁即建（无前置，独立放置）。
///
/// 存档：KingdomSaveData（§2.3，castleLevel/moduleLevels/贸易额度/coolDown）。
/// </summary>
public class KingdomManager : Singleton<KingdomManager>, ISaveable
{
    public string SaveId => "KingdomManager";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    // ===== 配置引用 =====
    private KingdomConfig _config;
    private TradeConfig _tradeConfig;
    private CastleUnlockTable _unlockTable;
    private List<ModuleDef> _moduleDefs;   // 6 模块资产（建筑模块归属/特殊建筑判定用）
    private ResearchProjectList _projectList;   // 2_12 步骤13：科技项目列表（增益聚合查）

    // ===== 运行时状态 =====
    /// <summary>主城等级（0=废墟未修复，1-6）。</summary>
    public int CastleLevel { get; private set; }

    /// <summary>六大模块等级 [Civil,Production,Livelihood,Military,Commerce,Science]。</summary>
    public int[] ModuleLevels { get; private set; } = new int[6];

    /// <summary>贸易剩余额度（索引=资源等级-1，13 档：1粮/2木/3石/4矿/5金/6水晶/7火油/8特食/9肉/10Metal/11石弹/12火弹/13魔弹）。</summary>
    public int[] TradeQuotaRemaining { get; private set; } = new int[13];

    /// <summary>贸易额度刷新倒计时（天，索引=资源等级-1，13 档；D220 每日全量重置后保留数组兼容存档，语义退化为占位）。</summary>
    public int[] TradeCooldownDays { get; private set; } = new int[13];

    /// <summary>
    /// 各模块研究等级（索引=模块，与 ModuleLevels 同构；研究完成提升，独立于主城解锁）。
    /// QQQ.2 Q4：学院/工坊研究系统落地——研究项目完成后提升 Science 模块研究等级。
    /// </summary>
    public int[] ResearchLevels { get; private set; } = new int[6];

    /// <summary>已研究科技 id 集（2_12 步骤13 D224~D227：研究完成即解锁对应增益/建筑。持久化，读档恢复）。</summary>
    private readonly HashSet<string> _researchedTechs = new HashSet<string>();

    /// <summary>主城 Building 引用（修复/升级时同步 level）。</summary>
    private Building _castleBuilding;

    /// <summary>全局配置（供其他王国系统读取）。</summary>
    public KingdomConfig Config => _config;

    /// <summary>市场贸易配置（D216~D220，步骤10 从 KingdomConfig 迁出）。</summary>
    public TradeConfig TradeConfig => _tradeConfig;

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;

        _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        _tradeConfig = Resources.Load<TradeConfig>("Config/TradeConfig");
        _unlockTable = Resources.Load<CastleUnlockTable>("Config/CastleUnlockTable");
        _projectList = Resources.Load<ResearchProjectList>("Config/ResearchProjectList");
        LoadModuleDefs();

        // 初始化贸易额度（P1 修复：P0 遗留——TradeQuotaRemaining 从未填充，导致贸易永远额度不足）
        InitTradeQuotas();

        // 主城修复完成 → castleLevel 置 1（步骤2）
        EventBus.Subscribe<BuildingActivatedEvent>(OnBuildingActivated);

        SaveManager.Instance.RegisterSaveable(this);
    }

    /// <summary>按配置初始化各资源档贸易额度 + 刷新倒计时（新建游戏/读档无损时调用）。</summary>
    public void InitTradeQuotas()
    {
        if (_tradeConfig == null) return;
        TradeQuotaRemaining = new int[13];
        TradeCooldownDays = new int[13];
        float mult = GetTradeQuotaMultiplier();   // 2_12 步骤13：贸易科技提升每日额度
        for (int i = 0; i < TradeQuotaRemaining.Length; i++)
        {
            var quota = _tradeConfig.GetQuota(i + 1);
            TradeQuotaRemaining[i] = (int)(quota.amountPerCycle * mult);
            TradeCooldownDays[i] = 1;   // D220：每日全量重置
        }
    }

    protected override void OnDestroy()
    {
        if (_instance != this) return;
        base.OnDestroy();
        EventBus.Unsubscribe<BuildingActivatedEvent>(OnBuildingActivated);
    }

    private void LoadModuleDefs()
    {
        _moduleDefs = new List<ModuleDef>();
        string[] names = { "Module_Civil", "Module_Production", "Module_Livelihood",
                           "Module_Military", "Module_Commerce", "Module_Science" };
        for (int i = 0; i < names.Length; i++)
        {
            var def = Resources.Load<ModuleDef>("Modules/" + names[i]);
            if (def != null) _moduleDefs.Add(def);
        }
    }

    // ===== 主城修复（步骤2：Ruins castleLevel=0 → 修复完成 castleLevel=1）=====

    private void OnBuildingActivated(BuildingActivatedEvent evt)
    {
        if (evt.Building == null || evt.Building.sourceType != BuildingType.CastleCore) return;
        _castleBuilding = evt.Building;
        // 修复完成 = 该建筑转为 Active；若此前 castleLevel==0 则置 1
        if (CastleLevel < 1)
        {
            SetCastleLevel(1);
        }
    }

    /// <summary>显式设置主城等级（读档恢复 / 修复完成回调）。</summary>
    public void SetCastleLevel(int level)
    {
        int clamped = Mathf.Clamp(level, 0, 6);
        if (clamped == CastleLevel) return;
        CastleLevel = clamped;
        RecomputeModuleLevels();
        var castle = FindCastleBuilding();
        if (castle != null) castle.level = Mathf.Max(1, clamped);
        Debug.Log($"[KingdomManager] 主城等级 → {clamped}，模块等级=[{string.Join(",", ModuleLevels)}]");
    }

    /// <summary>查找场景主城建筑（BuildingRegistry 按 CastleCore 来源）。</summary>
    private Building FindCastleBuilding()
    {
        if (_castleBuilding != null && _castleBuilding.gameObject != null) return _castleBuilding;
        if (BuildingRegistry.Instance != null)
        {
            var all = BuildingRegistry.Instance.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].sourceType == BuildingType.CastleCore)
                {
                    _castleBuilding = all[i];
                    return _castleBuilding;
                }
            }
        }
        return null;
    }

    // ===== 主城升级（步骤1：TryUpgradeCastle）=====

    /// <summary>尝试升级主城（Lv1 修复走 BuildingPanel，Lv2+ 走此接口）。校验消耗 → 扣费 → 升级 → 解锁模块。</summary>
    public bool TryUpgradeCastle()
    {
        if (CastleLevel < 1 || CastleLevel >= 6) return false;
        var cost = _config != null ? _config.GetCastleUpgradeCost(CastleLevel + 1) : ResourcePack.Zero;
        if (RulerController.Instance == null || !RulerController.Instance.CanAfford(cost)) return false;

        RulerController.Instance.Spend(cost);
        SetCastleLevel(CastleLevel + 1);
        return true;
    }

    /// <summary>当前主城下一级升级消耗（UI 显示用）。</summary>
    public ResourcePack NextCastleUpgradeCost()
    {
        return _config != null ? _config.GetCastleUpgradeCost(CastleLevel + 1) : ResourcePack.Zero;
    }

    // ===== 模块等级 =====

    /// <summary>按跨级解锁表重算六大模块等级（由某主城等级得出最高可达模块级）。</summary>
    private void RecomputeModuleLevels()
    {
        if (_unlockTable == null) return;
        for (int m = 0; m < 6; m++)
        {
            ModuleLevels[m] = _unlockTable.GetModuleLevel((ModuleType)m, CastleLevel);
        }
    }

    /// <summary>获取某模块当前等级。</summary>
    public int GetModuleLevel(ModuleType module)
    {
        int idx = (int)module;
        return idx >= 0 && idx < ModuleLevels.Length ? ModuleLevels[idx] : 0;
    }

    /// <summary>某模块是否已解锁到指定等级（特殊建筑解锁判定）。</summary>
    public bool IsSpecialUnlocked(ModuleType module, int lv)
    {
        return GetModuleLevel(module) >= lv;
    }

    // ===== 建筑解锁判定（步骤1：IsBuildingUnlocked）=====

    /// <summary>
    /// 建筑是否可建造。
    /// 规则：模块已解锁（moduleLevel≥1）；
    ///   特殊建筑（出现在某 tier 的 unlockBuildings）需 moduleLevel ≥ 首次解锁 tier；
    ///   基础建筑（可升级）只需 moduleLevel ≥ 1。
    /// </summary>
    public bool IsBuildingUnlocked(BuildingDef def)
    {
        if (def == null) return false;
        ModuleType module = ResolveModule(def);
        int moduleLevel = GetModuleLevel(module);
        if (moduleLevel < 1) return false;

        // 2_12 步骤13（D224~D227）：科技解锁新内容——铁匠铺/弩塔/魔法塔等需先研究对应科技才可建
        if (!string.IsNullOrEmpty(def.requiredTechId) && !IsTechResearched(def.requiredTechId))
            return false;

        int specialTier = FindSpecialUnlockTier(def.id);
        if (specialTier > 0)
            return moduleLevel >= specialTier;   // 特殊建筑
        return true;                              // 基础建筑：模块已解锁即可建
    }

    /// <summary>建筑升级门槛：下一级 ≤ 模块级（3.5 §2.1 可升级建筑 ≤ 模块级）。</summary>
    public bool CanUpgradeBuilding(Building b)
    {
        if (b == null || b.def == null) return false;
        int nextLevel = b.level + 1;
        return nextLevel <= GetModuleLevel(ResolveModule(b.def));
    }

    /// <summary>某模块研究等级（研究系统：研究完成提升，独立于主城模块等级）。</summary>
    public int GetResearchLevel(ModuleType module)
    {
        int idx = (int)module;
        return idx >= 0 && idx < ResearchLevels.Length ? ResearchLevels[idx] : 0;
    }

    /// <summary>研究完成：提升对应模块研究等级（学院/工坊均属 Science 模块，QQQ.2 Q4）+ 登记已研究科技（2_12 步骤13 D224~D227）。</summary>
    public void ApplyResearch(ResearchProject project)
    {
        int idx = (int)ModuleType.Science;
        if (idx < 0 || idx >= ResearchLevels.Length) return;
        ResearchLevels[idx] = Mathf.Max(ResearchLevels[idx], project.researchLevel);
        if (!string.IsNullOrEmpty(project.id)) _researchedTechs.Add(project.id);
        Debug.Log($"[KingdomManager] 研究完成：{project.displayName} → 科技研究等级 {ResearchLevels[idx]}（已解锁科技数 {_researchedTechs.Count}）");
    }

    // ===== 2_12 步骤13（D224~D227）：科技状态查询 + 增益聚合 =====

    /// <summary>某科技是否已研究（_researchedTechs 含 id 即 true；用 canonical 研究项目存在性兜底校验）。</summary>
    public bool IsTechResearched(string techId)
    {
        return !string.IsNullOrEmpty(techId) && _researchedTechs.Contains(techId);
    }

    /// <summary>已研究科技 id（存档序列化用）。</summary>
    public string[] GetResearchedTechIds()
    {
        var arr = new string[_researchedTechs.Count];
        _researchedTechs.CopyTo(arr);
        return arr;
    }

    /// <summary>读档恢复已研究科技集合（幂等，可在 Init 后随时调）。</summary>
    public void SetResearchedTechIds(string[] ids)
    {
        _researchedTechs.Clear();
        if (ids == null) return;
        for (int i = 0; i < ids.Length; i++)
            if (!string.IsNullOrEmpty(ids[i])) _researchedTechs.Add(ids[i]);
    }

    /// <summary>某研究项目（按 id 查 _projectList；找不到返回 default）。</summary>
    private ResearchProject GetResearch(string id)
    {
        return _projectList != null ? _projectList.GetById(id) : default;
    }

    /// <summary>贸易额度倍率：聚合并已研究科技中 tradeQuotaMult>0 项的乘积（默认 1）。</summary>
    public float GetTradeQuotaMultiplier()
    {
        float m = 1f;
        foreach (var id in _researchedTechs)
        {
            var p = GetResearch(id);
            if (p.tradeQuotaMult > 0f) m *= p.tradeQuotaMult;
        }
        return m;
    }

    /// <summary>建筑效率倍率：聚合并已研究科技中 buildEfficiencyMult>0 项的乘积（默认 1；&lt;1 缩短施工时长）。</summary>
    public float GetBuildEfficiencyMultiplier()
    {
        float m = 1f;
        foreach (var id in _researchedTechs)
        {
            var p = GetResearch(id);
            if (p.buildEfficiencyMult > 0f) m *= p.buildEfficiencyMult;
        }
        return m;
    }

    /// <summary>牧场容量倍率：聚合并已研究科技中 ranchCapacityMult>0 项的乘积（默认 1）。</summary>
    public float GetRanchCapacityMultiplier()
    {
        float m = 1f;
        foreach (var id in _researchedTechs)
        {
            var p = GetResearch(id);
            if (p.ranchCapacityMult > 0f) m *= p.ranchCapacityMult;
        }
        return m;
    }

    /// <summary>解析建筑归属模块（优先 BuildingDef.moduleType，回退 ModuleDef 查表）。</summary>
    public ModuleType ResolveModule(BuildingDef def)
    {
        if (def == null) return ModuleType.Civil;
        if (def.moduleType != ModuleType.Civil) return def.moduleType;  // Civil 默认兜底，见 == 语义
        // 回退：扫描 ModuleDef 资产找建筑归属
        if (_moduleDefs != null)
        {
            for (int i = 0; i < _moduleDefs.Count; i++)
            {
                var md = _moduleDefs[i];
                if (md == null) continue;
                for (int t = 0; t < md.tiers.Length; t++)
                {
                    if (Contains(md.tiers[t].upgradeBuildings, def.id) ||
                        Contains(md.tiers[t].unlockBuildings, def.id))
                        return (ModuleType)i;
                }
            }
        }
        return ModuleType.Civil;
    }

    /// <summary>建筑是否为特殊建筑（出现在任一 tier 的 unlockBuildings），返回其首次解锁 tier（0=基础建筑）。</summary>
    private int FindSpecialUnlockTier(string buildingId)
    {
        if (_moduleDefs == null || string.IsNullOrEmpty(buildingId)) return 0;
        for (int i = 0; i < _moduleDefs.Count; i++)
        {
            var md = _moduleDefs[i];
            if (md == null) continue;
            for (int t = 0; t < md.tiers.Length; t++)
            {
                if (Contains(md.tiers[t].unlockBuildings, buildingId))
                    return md.tiers[t].tier;
            }
        }
        return 0;
    }

    private static bool Contains(string[] arr, string val)
    {
        if (arr == null || string.IsNullOrEmpty(val)) return false;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] == val) return true;
        return false;
    }

    // ===== 贸易额度（步骤6：额度/周期存 KingdomManager）=====

    /// <summary>每日结算刷新贸易额度（由 DayCycleSettlement 调用）。D220：每日全量重置——各档额度恢复满额（去皮 refreshDays 多天递减）。</summary>
    public void TickTradeCooldowns()
    {
        float mult = GetTradeQuotaMultiplier();   // 2_12 步骤13：贸易科技提升每日额度
        for (int i = 0; i < TradeQuotaRemaining.Length; i++)
        {
            var quota = _tradeConfig != null ? _tradeConfig.GetQuota(i + 1) : TradeQuotaDef.Zero;
            TradeQuotaRemaining[i] = (int)(quota.amountPerCycle * mult);   // 每日全量恢复
            TradeCooldownDays[i] = 1;
        }
    }

    /// <summary>尝试从某资源等级扣减贸易额度（P1 贸易实际执行用；额度不足返回 false）。</summary>
    public bool TryConsumeTradeQuota(int resourceLevel, int amount)
    {
        int idx = resourceLevel - 1;
        if (idx < 0 || idx >= TradeQuotaRemaining.Length) return false;
        if (TradeQuotaRemaining[idx] < amount) return false;
        TradeQuotaRemaining[idx] -= amount;
        return true;
    }

    // ===== 存档（ISaveable, Global）=====

    public SavePayload SaveState()
    {
        // 2_12 步骤8.4：国库非金真源持久化（金走 Ruler 直通不在此列）
        var tv = TreasureVault.Instance;
        var data = new KingdomSaveData
        {
            saveDataVersion = 1,
            castleLevel = CastleLevel,
            moduleLevels = (int[])ModuleLevels.Clone(),
            currentDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1,
            tradeQuotaRemaining = (int[])TradeQuotaRemaining.Clone(),
            tradeCooldownDays = (int[])TradeCooldownDays.Clone(),
            researchLevels = (int[])ResearchLevels.Clone(),
            researchedTechIds = GetResearchedTechIds(),
            waveProgress = 0,
            treasuryStone = tv != null ? tv.GetAmount(ResourceType.Stone) : 0,
            treasuryWood = tv != null ? tv.GetAmount(ResourceType.Wood) : 0,
            treasuryFood = tv != null ? tv.GetAmount(ResourceType.Food) : 0,
            treasurySpecialFood = tv != null ? tv.GetAmount(ResourceType.SpecialFood) : 0,
            treasuryMeat = tv != null ? tv.GetAmount(ResourceType.Meat) : 0,
            treasuryMetal = tv != null ? tv.GetAmount(ResourceType.Metal) : 0
        };
        return new SavePayload
        {
            typeName = typeof(KingdomSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = data.saveDataVersion
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(KingdomSaveData).AssemblyQualifiedName) return;
        var data = JsonUtility.FromJson<KingdomSaveData>(payload.json);

        int level = Mathf.Clamp(data.castleLevel, 0, 6);
        CastleLevel = level;

        // 模块等级：优先存值，空/越界则按解锁表重算（迁移兜底）
        if (data.moduleLevels != null && data.moduleLevels.Length == 6)
            ModuleLevels = (int[])data.moduleLevels.Clone();
        else
            RecomputeModuleLevels();

        if (data.tradeQuotaRemaining != null && data.tradeQuotaRemaining.Length >= 7)
            TradeQuotaRemaining = ResizeQuotaArray(data.tradeQuotaRemaining);
        if (data.tradeCooldownDays != null && data.tradeCooldownDays.Length >= 7)
            TradeCooldownDays = ResizeQuotaArray(data.tradeCooldownDays);

        // 研究等级（旧档缺失=0 保留）
        if (data.researchLevels != null && data.researchLevels.Length == 6)
            ResearchLevels = (int[])data.researchLevels.Clone();
        // 2_12 步骤13：读档恢复已研究科技（旧档缺失=空集，无科技效果）
        SetResearchedTechIds(data.researchedTechIds);

        // 2_12 步骤8.4：读到国库字段缓存（国库=主城学堂后建，vault 就绪后由 TreasureVault 读出恢复）
        TreasuryStone = data.treasuryStone;
        TreasuryWood = data.treasuryWood;
        TreasuryFood = data.treasuryFood;
        TreasurySpecialFood = data.treasurySpecialFood;
        TreasuryMeat = data.treasuryMeat;
        TreasuryMetal = data.treasuryMetal;

        Debug.Log($"[KingdomManager] 读档恢复：主城 Lv.{CastleLevel}，模块=[{string.Join(",", ModuleLevels)}]，研究=[{string.Join(",", ResearchLevels)}]");
    }

    // ===== 2_12 步骤8.4 国库读档缓存（读档 Global 先落字段，主城 TreasureVault 就绪后据此恢复）=====
    public int TreasuryStone { get; private set; }
    public int TreasuryWood { get; private set; }
    public int TreasuryFood { get; private set; }
    public int TreasurySpecialFood { get; private set; }
    public int TreasuryMeat { get; private set; }
    public int TreasuryMetal { get; private set; }

    /// <summary>把旧存档（7/9 档）额度数组扩展到当前 13 档（v1 兼容：缺档补初始额度）。</summary>
    private int[] ResizeQuotaArray(int[] old)
    {
        int[] result = new int[13];
        for (int i = 0; i < result.Length; i++)
        {
            if (i < old.Length) { result[i] = old[i]; continue; }
            // 缺档：按配置初始额度填入（新增 Metal/弹药档）
            var quota = _tradeConfig != null ? _tradeConfig.GetQuota(i + 1) : TradeQuotaDef.Zero;
            result[i] = quota.amountPerCycle;
        }
        return result;
    }

    /// <summary>返回主菜单时重置（由 TeardownManager 调）。</summary>
    public void ResetState()
    {
        CastleLevel = 0;
        ModuleLevels = new int[6];
        ResearchLevels = new int[6];
        TradeQuotaRemaining = new int[13];
        TradeCooldownDays = new int[13];
        _castleBuilding = null;
        InitTradeQuotas();   // 新建游戏重新初始化贸易额度
        // 2_12 步骤8.4：清国库读档缓存（防新建局读到上局残留）
        TreasuryStone = TreasuryWood = TreasuryFood = TreasurySpecialFood = TreasuryMeat = TreasuryMetal = 0;
    }
}