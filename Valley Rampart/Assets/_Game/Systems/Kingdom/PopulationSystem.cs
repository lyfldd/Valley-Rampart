using UnityEngine;

/// <summary>
/// 人口系统（3.5 实施计划 P0 步骤5，数据层先行；Singleton + ISaveable, Global）。
/// 生育事件：每日结算，条件（整体幸福>60 且 平均饱食>50）→ 每 2 人每 5 天 +1（birthCooldownDays 计数）。
/// 出生 = 无职业废人（吃粮不干活，须训练转职）。
///
/// P0 占位：幸福/饱食输入暂用占位常量（等 AI 定后接真实值）；出生仅改人口计数，
/// 具体新生 Unit 生成（含房屋容量占用）后置 AI/民政系统。PopulationSaveData 保留 count/cooldown。
/// </summary>
public class PopulationSystem : Singleton<PopulationSystem>, ISaveable
{
    public string SaveId => "PopulationSystem";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    private KingdomConfig _config;

    /// <summary>人口数（无性别，每 2 人 5 天 1 人）。</summary>
    public int PopulationCount { get; private set; }

    /// <summary>生育冷却倒计时（天，到 0 且满足条件即 +1）。</summary>
    public int BirthCooldownDays { get; private set; }

    /// <summary>平均饱食（P1 接真实值：SatietySystem 平均）。</summary>
    public float AvgSatiety { get; private set; } = 50f;

    /// <summary>平均幸福（P1 接真实值：HappinessSystem 整体幸福）。</summary>
    public float AvgHappiness { get; private set; } = 50f;

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        BirthCooldownDays = LifeConfig().birthCooldownDefault;
        SaveManager.Instance.RegisterSaveable(this);
    }

    private KingdomConfig LifeConfig()
    {
        if (_config == null) _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        return _config;
    }

    /// <summary>初始化人口（新建游戏开局 10 人，§13.5）。</summary>
    public void SetInitialPopulation(int count)
    {
        PopulationCount = Mathf.Max(0, count);
        BirthCooldownDays = LifeConfig() != null ? LifeConfig().birthCooldownDefault : 5;
        Debug.Log($"[PopulationSystem] 开局人口 = {PopulationCount}");
    }

    /// <summary>每日结算（DayCycleSettlement 统一入口调用）。
    /// 3.5 P0-1 补全三层生育前置：
    ///   ① 全局：幸福>threshold 且 平均饱食>threshold（已有）
    ///   ② 房屋：王国房屋剩余容量 > 0（房屋满=禁止生育，硬前置）
    ///   ③ 个体：单对 10 天冷却（birthPairCooldownDays，计数制下作全局冷却对齐）
    /// 随机配对 = 现有 PopulationCount/birthCouplesDivisor 对数模型（计数制抽象）。
    /// </summary>
    public void OnNewDay()
    {
        var cfg = LifeConfig();
        if (cfg == null) return;

        // P1：接真实值（占位常量已替换）——整体幸福读 HappinessSystem，平均饱食读 SatietySystem
        if (HappinessSystem.Instance != null)
            AvgHappiness = HappinessSystem.Instance.OverallHappiness;
        if (SatietySystem.Instance != null)
            AvgSatiety = SatietySystem.Instance.GetAverageSatiety();

        int pairCooldown = cfg.birthPairCooldownDays > 0 ? cfg.birthPairCooldownDays : cfg.birthIntervalDays;

        BirthCooldownDays--;
        if (BirthCooldownDays > 0) return;

        // ② 房屋硬前置：王国房屋剩余容量 > 0（房屋满 = 禁止生育）
        bool hasHouse = false;
        if (HappinessSystem.Instance != null)
        {
            int houseCapacity = HappinessSystem.Instance.GetTotalHouseCapacity();
            hasHouse = houseCapacity > PopulationCount;   // 剩余容量 > 0
        }

        // ① 全局条件：幸福>60 且 平均饱食>50（真实值）
        bool happy = AvgHappiness > cfg.birthHappinessThreshold;
        bool fed = AvgSatiety > cfg.birthSatietyThreshold;
        if (!happy || !fed || !hasHouse)
        {
            // 条件不满足则重置冷却，待下轮再评估
            BirthCooldownDays = pairCooldown;
            return;
        }

        // ③ 随机配对：每 2 人生育（人口/2 = 对数）；三层惩罚2：幸福低 → 人口增长因子降低
        int couples = PopulationCount / cfg.birthCouplesDivisor;
        int growthFactor = HappinessSystem.Instance != null
            ? Mathf.RoundToInt(HappinessSystem.Instance.GetPopulationGrowthFactor() * 100f)
            : 100;
        if (couples >= 1 && growthFactor >= 1)
        {
            // 幸福因子 < 100 时按概率/比例折算增量（幸福 50 → 增量为 50%）
            int gain = growthFactor >= 100 ? 1 : (Random.value * 100f < growthFactor ? 1 : 0);
            PopulationCount += gain;
            if (gain > 0)
                Debug.Log($"[PopulationSystem] 生育 +1，人口 → {PopulationCount}（出生=无职业废人；幸福因子{growthFactor}%；房屋容量{GetCurrentHouseCapacity()}）");
        }
        BirthCooldownDays = pairCooldown;
    }

    /// <summary>当前王国房屋总容量（供生育日志/调试；无 HappinessSystem 返回 0）。</summary>
    private int GetCurrentHouseCapacity()
    {
        return HappinessSystem.Instance != null ? HappinessSystem.Instance.GetTotalHouseCapacity() : 0;
    }

    // ===== ISaveable, Global =====

    public SavePayload SaveState()
    {
        var data = new PopulationSaveData
        {
            saveDataVersion = 1,
            populationCount = PopulationCount,
            birthCooldownDays = BirthCooldownDays,
            avgSatiety = AvgSatiety,
            avgHappiness = AvgHappiness
        };
        return new SavePayload
        {
            typeName = typeof(PopulationSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = data.saveDataVersion
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(PopulationSaveData).AssemblyQualifiedName) return;
        var data = JsonUtility.FromJson<PopulationSaveData>(payload.json);
        PopulationCount = Mathf.Max(0, data.populationCount);
        BirthCooldownDays = Mathf.Max(0, data.birthCooldownDays);
        AvgSatiety = data.avgSatiety;
        AvgHappiness = data.avgHappiness;
        Debug.Log($"[PopulationSystem] 读档恢复：人口 {PopulationCount}，生育冷却 {BirthCooldownDays} 天");
    }

    /// <summary>返回主菜单时重置。</summary>
    public void ResetState()
    {
        PopulationCount = 0;
        BirthCooldownDays = LifeConfig() != null ? LifeConfig().birthCooldownDefault : 5;
        AvgSatiety = 50f;
        AvgHappiness = 50f;
    }
}