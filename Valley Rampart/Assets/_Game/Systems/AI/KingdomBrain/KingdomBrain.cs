using UnityEngine;

// ============================================================================
//  王国脑主脑（2_17 步骤8，D337 补八格：Foundry 创建 → D279 灭亡销毁）
//  职责：王国日 tick 权威驱动（由 DayCycleSettlement 五步②调用，非自挂 Update）。
//  每 AI 王国（id>0）一个实例；玩家(id=0)无 Brain（D338，工厂/注册表双短路）。
//  纯数据容器 + 逻辑，不挂 MonoBehaviour（单例经由 KingdomBrainRegistry 持有）。
//
//  tick 顺序：SimMode 挂 Fine（P0 恒 Fine）→ 采集探针快照 → ScriptStageMachine 推进
//  （单向+最小停留+每日最多升荤级）→ 同步 KingdomState.scriptPhase → FocusController 刷新国策焦点。
//  国库口径 = 【昨日结存】（HH.24 裁决① A 准：植入五步②、日结入账之前，花昨日余额，
//  契合 15_账本「一·补二」1 日滞后登记；不含在途储仓产出）。
// ============================================================================

public class KingdomBrain
{
    /// <summary>所属王国 id（>0；玩家 id=0 永不会建 Brain）。</summary>
    public readonly int kingdomId;

    /// <summary>剧本四阶段状态机（D317~D320/D349，线程安全概念上单 tick）。</summary>
    public ScriptStageMachine StageMachine { get; } = new ScriptStageMachine();

    /// <summary>国策焦点 + 常设底线 + 被攻击打断（D322/D340）。</summary>
    public FocusController Focus { get; }

    /// <summary>当前剧本阶段（快捷映射 StageMachine.Stage）。</summary>
    public ScriptStage Stage => StageMachine.Stage;

    public KingdomBrain(int kingdomId)
    {
        this.kingdomId = kingdomId;
        Focus = new FocusController(kingdomId);
    }

    /// <summary>订阅王国脑事件（王国诞生时由 Factory 调用；Unsubscribe 成对，D337/D340）。</summary>
    public void Subscribe() => Focus.Subscribe();

    /// <summary>退订全部事件（灭亡销毁钩子，D337；2_19 灭亡管线接入）。</summary>
    public void Unsubscribe() => Focus.Unsubscribe();

    /// <summary>
    /// 每日王国脑 tick（D347 五步②）。SimMode 挂细模拟→采快照→剧本推进→同步阶段→刷新焦点。
    /// 玩家/空王国短路；灭国后王国已从 Registry 移除故 Get 为空直接返回。
    /// </summary>
    public void Tick(int day)
    {
        var kingdom = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(kingdomId) : null;
        if (kingdom == null || kingdom.IsPlayer) return;   // 玩家无脑（D338）
        if (SimModeManager.Instance != null && SimModeManager.Instance.GetMode(kingdomId) != SimMode.Fine)
            return;   // 抽象粒度不在细模拟 tick（P1 步骤14 交给 AbstractEconomySettler）

        var cfg = KingdomBrain.LoadConfig();
        var ucfg = UtilityActionConfig.LoadConfig();
        kingdom.simMode = SimMode.Fine;

        var ctx = BuildContext(kingdom, cfg);
        bool upgraded = StageMachine.Tick(ctx, cfg);
        kingdom.scriptPhase = StageMachine.Stage;   // 双向：升级同步 + 保持同步
        Focus.Update(kingdom, cfg, ucfg, day);      // D322 焦点模型（底线→评分→防抖切换）→ kingdom.focus=行动id
        ExecuteFocus(kingdom);                      // 焦点下发执行（P0 起步骨架）

        if (upgraded)
            Debug.Log($"[KingdomBrain] k{kingdomId} 剧本阶段 → {ScriptStageMachine.Name(StageMachine.Stage)} (Day {day})");
    }

    /// <summary>采集剧本推进判定快照（凑齐后为纯函数判定；口径均为【昨日结存】）。</summary>
    public static ScriptStageContext BuildContext(KingdomState k, KingdomBrainConfig cfg)
    {
        int population = k.workerCount + k.warriorCount;   // P0 人口口径=工人+战士
        int dailyGrain = Mathf.Max(1, population * cfg.grainConsumptionPerPop);

        return new ScriptStageContext
        {
            housedAll = cfg.surviveToDevelop_housedAll,     // P0 房指标未接入，按 SO 占位真源探针
            grainDaysOk = population > 0
                && k.GetResourceValue(ResourceType.Food) >= cfg.surviveToDevelop_grainDays * dailyGrain,
            unemployedOk = true,                            // P0 任务统计未接入，占位无失业
            workerCount = k.workerCount,
            capacityCount = CountActiveBuildings(k.id),     // P0 产能近似：活跃建筑数
            netInflowPositive = true,                       // P0 收支差统计未接入，占位净流入为正
            warriorCount = k.warriorCount,
            populationCount = population,
            expansionChunks = k.Territory.Count             // 领土真源在 TerritorySystem（D342）
        };
    }

    /// <summary>某王国活跃建筑数（P0 产能下限近似；BuildingRegistry 真源，固定遍历）。</summary>
    private static int CountActiveBuildings(int kingdomId)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return 0;
        int n = 0;
        for (int i = 0; i < reg.All.Count; i++)
        {
            var b = reg.All[i];
            if (b != null && b.kingdomId == kingdomId && b.IsActive) n++;
        }
        return n;
    }

    /// <summary>载入王国脑配置（缺 asset 时回退默认占位实例；so-data-driven 禁魔法数）。</summary>
    public static KingdomBrainConfig LoadConfig()
    {
        var cfg = Resources.Load<KingdomBrainConfig>("Config/Kingdoms/KingdomBrainConfig");
        return cfg != null ? cfg : ScriptableObject.CreateInstance<KingdomBrainConfig>();
    }

    /// <summary>
    /// 焦点下发执行（2_17 步骤9，D345 指令通道执行骨架构图；P0 完整局批次补齐真实派遣——建造选址/招募实体化）。
    /// 本步先落实焦点契约（kingdom.focus=行动 id 已有），实体派遣留执行口，保证冒烟#19"⑥存活期可执行"在焦点层可测。
    /// </summary>
    private void ExecuteFocus(KingdomState kingdom)
    {
        switch ((UtilityAction)kingdom.focus)
        {
            case UtilityAction.RecruitWorker:
                // ⑥招工人：存活期防卡死关键路径。P0 批次经 D345 招募通道（王国无业池→居民转工人）。
                if (kingdom.resources.gold > 0f)
                    Debug.Log($"[KingdomBrain] k{kingdomId} 焦点=⑥招工人 下发（执行骨架，P0 批次接招募通道）");
                break;
            case UtilityAction.BuildHouse:
            case UtilityAction.BuildWarehouse:
            case UtilityAction.BuildCapacity:
            case UtilityAction.BoostHarvest:
            case UtilityAction.Grain:
                // 建造/采集类：P0 批次补选址 + BuildController.TryBuild(def, sub, orient, kingdomId)。
                Debug.Log($"[KingdomBrain] k{kingdomId} 焦点={(UtilityAction)kingdom.focus} 下发（建造骨架，P0 批次接选址）");
                break;
            case UtilityAction.Rebuild:
            case UtilityAction.Defense:
            case UtilityAction.None:
            default:
                break;   // 姿态/占位无实体指令
        }
    }
}