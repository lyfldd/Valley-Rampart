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

    // ===== 运行时状态 =====
    /// <summary>主城等级（0=废墟未修复，1-6）。</summary>
    public int CastleLevel { get; private set; }

    /// <summary>王国名（2_13 取代君主名；存档显示用，默认"河谷王国"）。</summary>
    public string KingdomName { get; set; } = "河谷王国";

    /// <summary>六大模块等级 [Civil,Production,Livelihood,Military,Commerce,Science]。</summary>
    public int[] ModuleLevels { get; private set; } = new int[6];

    /// <summary>贸易剩余额度（索引=资源等级-1，13 档：1粮/2木/3石/4矿/5金/6水晶/7火油/8特食/9肉/10Metal/11石弹/12火弹/13魔弹）。</summary>
    public int[] TradeQuotaRemaining { get; private set; } = new int[13];

    // ===== 2_17 步骤11 批1·per-kingdom 贸易额度结构（只结构，AI 主动贸易归 P2 econ-train）=====
    // TradeQuotaRemaining / TryConsumeTradeQuota 是玩家(id=0) 单例数组，TradePanel 依赖其公开 API。
    // 本批不改 API 签名（玩家路径完全不动 = 零回归，HH.30）。以下 per-kingdom 额度映射（Dictionary<int,int[]>）
    // 备 AI/动态王国主动贸易接入：key=kingdomId，value=13 档额度（索引=资源等级-1，同玩家）。
    // TODO[批2 econ-train] AI 主动贸易：填充 _perKingdomTradeQuota 并接入每日刷新/消耗语义（玩家名额度不受影响）
    private readonly Dictionary<int, int[]> _perKingdomTradeQuota = new Dictionary<int, int[]>();

    /// <summary>贸易额度刷新倒计时（天，索引=资源等级-1，13 档；D220 每日全量重置后保留数组兼容存档，语义退化为占位）。</summary>
    public int[] TradeCooldownDays { get; private set; } = new int[13];

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
        for (int i = 0; i < TradeQuotaRemaining.Length; i++)
        {
            var quota = _tradeConfig.GetQuota(i + 1);
            TradeQuotaRemaining[i] = quota.amountPerCycle;
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
                           "Module_Military", "Module_Commerce" };
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
        // 2_17 步骤11 批2：主城等级变更后镜像到用户王国 KingdomState[0]（per-kingdom 解锁态数据通道）
        MirrorPlayerUnlockToRegistry();
        Debug.Log($"[KingdomManager] 主城等级 → {clamped}，模块等级=[{string.Join(",", ModuleLevels)}]");
    }

    // ===== 2_17 步骤11 批2·玩家解锁态 → KingdomState[0] 镜像（HH.30：CastleLevel/ModuleLevels 玩家 getter 不变，另写 KingdomState[0] 供 per-kingdom 消费）=====

    /// <summary>
    /// 把玩家(id=0) 主城/模块解锁态同步到 KingdomState[0]。
    /// per-kingdom 数据通道：KingdomManager 仍为玩家的真源，KingdomState[0].castleLevel/moduleLevels 为镜像（读档/升级/复位时镜像写桶0）。
    /// AI 王国解锁态由各 KingdomState 独立持有（本批只建数据通道，不实现 AI 升级逻辑）。
    /// </summary>
    public void SyncPlayerUnlockToState(KingdomState playerK)
    {
        if (playerK == null) return;
        playerK.castleLevel = CastleLevel;
        playerK.moduleLevels = ModuleLevels != null ? (int[])ModuleLevels.Clone() : new int[6];
    }

    private void MirrorPlayerUnlockToRegistry()
    {
        if (KingdomRegistry.Instance == null) return;
        SyncPlayerUnlockToState(KingdomRegistry.Instance.Get(0));
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
        for (int i = 0; i < TradeQuotaRemaining.Length; i++)
        {
            var quota = _tradeConfig != null ? _tradeConfig.GetQuota(i + 1) : TradeQuotaDef.Zero;
            TradeQuotaRemaining[i] = quota.amountPerCycle;   // 每日全量恢复
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
            kingdomName = KingdomName,
            castleLevel = CastleLevel,
            moduleLevels = (int[])ModuleLevels.Clone(),
            currentDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1,
            tradeQuotaRemaining = (int[])TradeQuotaRemaining.Clone(),
            tradeCooldownDays = (int[])TradeCooldownDays.Clone(),
            researchLevels = null,   // D461 研究系统退役：字段保留（schema 零变更）但恒不写
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
        KingdomName = string.IsNullOrEmpty(data.kingdomName) ? "河谷王国" : data.kingdomName;

        // 模块等级：优先存值，空/越界则按解锁表重算（迁移兜底）
        if (data.moduleLevels != null && data.moduleLevels.Length == 6)
            ModuleLevels = (int[])data.moduleLevels.Clone();
        else
            RecomputeModuleLevels();

        if (data.tradeQuotaRemaining != null && data.tradeQuotaRemaining.Length >= 7)
            TradeQuotaRemaining = ResizeQuotaArray(data.tradeQuotaRemaining);
        if (data.tradeCooldownDays != null && data.tradeCooldownDays.Length >= 7)
            TradeCooldownDays = ResizeQuotaArray(data.tradeCooldownDays);

        // 2_12 步骤8.4：读到国库字段缓存（国库=主城学堂后建，vault 就绪后由 TreasureVault 读出恢复）
        TreasuryStone = data.treasuryStone;
        TreasuryWood = data.treasuryWood;
        TreasuryFood = data.treasuryFood;
        TreasurySpecialFood = data.treasurySpecialFood;
        TreasuryMeat = data.treasuryMeat;
        TreasuryMetal = data.treasuryMetal;

        // 2_17 步骤11 批2：读档后把玩家解锁态镜像到 KingdomState[0]（若 Registry 已就绪；未就绪则 Registry 创建玩家态时回拉）
        MirrorPlayerUnlockToRegistry();

        Debug.Log($"[KingdomManager] 读档恢复：主城 Lv.{CastleLevel}，模块=[{string.Join(",", ModuleLevels)}]");
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
        TradeQuotaRemaining = new int[13];
        TradeCooldownDays = new int[13];
        _castleBuilding = null;
        InitTradeQuotas();   // 新建游戏重新初始化贸易额度
        // 2_12 步骤8.4：清国库读档缓存（防新建局读到上局残留）
        TreasuryStone = TreasuryWood = TreasuryFood = TreasurySpecialFood = TreasuryMeat = TreasuryMetal = 0;
        // 2_17 步骤11 批2：复位同步玩家解锁态镜像到 KingdomState[0]（随 Registry 重置后回拉一致）
        MirrorPlayerUnlockToRegistry();
    }
}