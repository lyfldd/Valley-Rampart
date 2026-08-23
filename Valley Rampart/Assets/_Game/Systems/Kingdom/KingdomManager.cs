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
    private CastleUnlockTable _unlockTable;
    private List<ModuleDef> _moduleDefs;   // 6 模块资产（建筑模块归属/特殊建筑判定用）

    // ===== 运行时状态 =====
    /// <summary>主城等级（0=废墟未修复，1-6）。</summary>
    public int CastleLevel { get; private set; }

    /// <summary>六大模块等级 [Civil,Production,Livelihood,Military,Commerce,Science]。</summary>
    public int[] ModuleLevels { get; private set; } = new int[6];

    /// <summary>贸易剩余额度（索引=资源等级-1，9 档：1粮/2木/3石/4矿/5金/6水晶/7火油/8特殊食物/9肉）。</summary>
    public int[] TradeQuotaRemaining { get; private set; } = new int[9];

    /// <summary>贸易额度刷新倒计时（天，索引=资源等级-1，9 档）。</summary>
    public int[] TradeCooldownDays { get; private set; } = new int[9];

    /// <summary>
    /// 各模块研究等级（索引=模块，与 ModuleLevels 同构；研究完成提升，独立于主城解锁）。
    /// QQQ.2 Q4：学院/工坊研究系统落地——研究项目完成后提升 Science 模块研究等级。
    /// </summary>
    public int[] ResearchLevels { get; private set; } = new int[6];

    /// <summary>主城 Building 引用（修复/升级时同步 level）。</summary>
    private Building _castleBuilding;

    /// <summary>全局配置（供其他王国系统读取）。</summary>
    public KingdomConfig Config => _config;

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;

        _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        _unlockTable = Resources.Load<CastleUnlockTable>("Config/CastleUnlockTable");
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
        if (_config == null || _config.merchantQuotas == null) return;
        TradeQuotaRemaining = new int[9];
        TradeCooldownDays = new int[9];
        for (int i = 0; i < TradeQuotaRemaining.Length; i++)
        {
            var quota = _config.GetQuota(i + 1);
            TradeQuotaRemaining[i] = quota.amountPerCycle;
            TradeCooldownDays[i] = quota.refreshDays;
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

    /// <summary>研究完成：提升对应模块研究等级（学院/工坊均属 Science 模块，QQQ.2 Q4）。</summary>
    public void ApplyResearch(ResearchProject project)
    {
        int idx = (int)ModuleType.Science;
        if (idx < 0 || idx >= ResearchLevels.Length) return;
        ResearchLevels[idx] = Mathf.Max(ResearchLevels[idx], project.researchLevel);
        Debug.Log($"[KingdomManager] 研究完成：{project.displayName} → 科技研究等级 {ResearchLevels[idx]}");
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

    /// <summary>每日结算时刷新贸易额度冷却（由 DayCycleSettlement 调用）。</summary>
    public void TickTradeCooldowns()
    {
        for (int i = 0; i < TradeCooldownDays.Length; i++)
        {
            if (TradeCooldownDays[i] <= 0) continue;
            TradeCooldownDays[i]--;
            if (TradeCooldownDays[i] <= 0)
                ResetQuota(i);
        }
    }

    private void ResetQuota(int resourceLevelIndex)
    {
        var quota = _config != null ? _config.GetQuota(resourceLevelIndex + 1) : TradeQuotaDef.Zero;
        TradeQuotaRemaining[resourceLevelIndex] = quota.amountPerCycle;
        TradeCooldownDays[resourceLevelIndex] = quota.refreshDays;
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

    /// <summary>把旧存档（7 档）额度数组扩展到当前 9 档（v1 兼容：缺档补初始额度）。</summary>
    private int[] ResizeQuotaArray(int[] old)
    {
        int[] result = new int[9];
        for (int i = 0; i < result.Length; i++)
        {
            if (i < old.Length) { result[i] = old[i]; continue; }
            // 缺档：按配置初始额度填入（特殊食物=8，肉=9）
            var quota = _config != null ? _config.GetQuota(i + 1) : TradeQuotaDef.Zero;
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
        TradeQuotaRemaining = new int[9];
        TradeCooldownDays = new int[9];
        _castleBuilding = null;
        InitTradeQuotas();   // 新建游戏重新初始化贸易额度
        // 2_12 步骤8.4：清国库读档缓存（防新建局读到上局残留）
        TreasuryStone = TreasuryWood = TreasuryFood = TreasurySpecialFood = TreasuryMeat = TreasuryMetal = 0;
    }
}