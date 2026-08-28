using System.Collections.Generic;
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
//
//  2_17 完整局批次（HH.26 裁决② 拆双证）：ExecuteFocus 接真实指令通道——
//    ⑥招工人 → 流浪汉招募（花 aiRecruitFoodCost 粮 → KingdomFoundry.ConvertVagrantsToWorkers 直转工人）；
//    建造类 ①②③④⑤ → BuildController.TryBuild 门面（镜像玩家同入口，AI 台账扣费；主城螺旋选址）。
//  派遣落地计数 DispatchStats per-kingdom（trainOk/buildOk/try 面），供 P0 完整局 harness 断言。
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

    // ===== 派遣落地计数（完整局批次执行面观测；运行时态不入档——读档后归零属已知边界）=====
    private struct DispatchStat { public int trainOk, buildOk, trainTry, buildTry; }
    private static readonly Dictionary<int, DispatchStat> s_dispatch = new Dictionary<int, DispatchStat>();

    /// <summary>某王国的派遣落地数（trainOk=⑥实体化数；buildOk=建造类落成受理数）。</summary>
    public static (int trainOk, int buildOk, int trainTry, int buildTry) GetDispatch(int kingdomId)
    {
        return s_dispatch.TryGetValue(kingdomId, out var s) ? (s.trainOk, s.buildOk, s.trainTry, s.buildTry) : (0, 0, 0, 0);
    }

    /// <summary>清空全部派遣计数（harness 两轮间/新开局归零用）。</summary>
    public static void ResetDispatchStats() => s_dispatch.Clear();

    private static void Bump(int kingdomId, bool train, bool ok)
    {
        if (!s_dispatch.TryGetValue(kingdomId, out var s)) s = default;
        if (train) { s.trainTry++; if (ok) s.trainOk++; }
        else { s.buildTry++; if (ok) s.buildOk++; }
        s_dispatch[kingdomId] = s;
    }

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
    /// 每日王国脑 tick（D347 五步②）。SimMode 挂细模拟→采快照→剧本推进→同步阶段→刷新焦点→焦点下发执行。
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
        ExecuteFocus(kingdom, cfg);                 // 焦点下发真实执行（完整局批次接通）

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

    // ===== 焦点下发真实执行（2_17 完整局批次，HH.26 裁决② B-步骤9 双证之「评分→真实派遣」）=====

    /// <summary>
    /// 焦点下发执行：接 D345 指令通道真面。
    /// ⑥招工人 → 找未招募流浪汉 → 花 aiRecruitFoodCost 粮 → ConvertVagrantsToWorkers 直转本国工人；
    /// 建造类（①House②Warehouse③quarry④farm⑤Granary）→ BuildController.TryBuild 门面（同入口同规则，
    /// AI 台账扣费）；⑬⑭/None 维持姿态无实体指令。
    /// </summary>
    private void ExecuteFocus(KingdomState kingdom, KingdomBrainConfig cfg)
    {
        switch ((UtilityAction)kingdom.focus)
        {
            case UtilityAction.RecruitWorker:
                ExecuteRecruitWorker(kingdom, cfg);
                break;
            case UtilityAction.BuildHouse:
            case UtilityAction.BuildWarehouse:
            case UtilityAction.BuildCapacity:
            case UtilityAction.BoostHarvest:
            case UtilityAction.Grain:
            case UtilityAction.BuildWall:
                ExecuteBuildFocus(kingdom, cfg);
                break;
            case UtilityAction.RecruitWarrior:
                ExecuteRecruitWarrior(kingdom, cfg);
                break;
            case UtilityAction.Tech:
                ExecuteTech(kingdom, cfg);
                break;
            case UtilityAction.Expedition:
            case UtilityAction.Reinforce:
            case UtilityAction.Diplomacy:
                // ⑪⑫⑮ 占位可执行子集：可被选作焦点并"执行"，但宣战/增援动作接口指向 2_18 未落地桩（S0 无实体指令）
                Debug.Log($"[KingdomBrain] k{kingdomId} 占位焦点 {(UtilityAction)kingdom.focus} 执行（L3/2_18 接口待接线，仅置位无实体动作）");
                break;
            case UtilityAction.Rebuild:
            case UtilityAction.Defense:
            case UtilityAction.None:
            default:
                break;   // 姿态/占位无实体指令
        }
    }

    /// <summary>⑥招工人真实通道：流浪汉 → 本国工人（D345 人口增长唯一途径，防卡死关键路径）。</summary>
    private void ExecuteRecruitWorker(KingdomState kingdom, KingdomBrainConfig cfg)
    {
        int cost = Mathf.Max(1, cfg.aiRecruitFoodCost);
        UnitController vagrant = FindRecruitableVagrant();
        if (vagrant == null)
        {
            Bump(kingdomId, train: true, ok: false);
            return;   // 无候选（营地无流浪汉）：派遣尝试失败，明日再试（不空转硬造人口）
        }
        if (kingdom.GetResourceValue(ResourceType.Food) < cost)
        {
            Bump(kingdomId, train: true, ok: false);
            Debug.Log($"[KingdomBrain] k{kingdomId} ⑥招粮不足（需{cost}）");
            return;
        }

        kingdom.Spend(new ResourcePack { food = cost });   // AI 台账扣费（镜像玩家 recruitFoodCost 语义）
        int converted = KingdomFoundry.ConvertVagrantsToWorkers(
            new List<int> { vagrant.npcId }, kingdomId);
        bool ok = converted > 0;
        Bump(kingdomId, train: true, ok: ok);
        if (ok)
            Debug.Log($"[KingdomBrain] k{kingdomId} ⑥招工人落地：流浪汉#{vagrant.npcId} → Worker（粮-{cost}）");
    }

    /// <summary>⑦招战士真实通道（D348 兵力目标）：直转本国一个活工人为战士（直转模式，成本 SO）。</summary>
    private void ExecuteRecruitWarrior(KingdomState kingdom, KingdomBrainConfig cfg)
    {
        int gold = Mathf.Max(1, cfg.recruitWarriorCostGold);
        int food = Mathf.Max(1, cfg.recruitWarriorCostFood);
        if (kingdom.GetResourceValue(ResourceType.Gold) < gold || kingdom.GetResourceValue(ResourceType.Food) < food)
        {
            Bump(kingdomId, train: true, ok: false);
            return;
        }
        var w = FindOwnWorker();
        if (w == null)
        {
            Bump(kingdomId, train: true, ok: false);
            return;   // 无本国工人可转战士（人口不足），明日再试
        }
        if (kingdom.warriorCount >= UtilityScorer.MilitaryTarget(kingdom, cfg))
        {
            Bump(kingdomId, train: true, ok: false);
            return;   // 已达兵力目标：无需再招（评分门控兜底）
        }

        kingdom.Spend(new ResourcePack { gold = gold, food = food });
        w.SetOccupation(Occupation.Warrior);
        Bump(kingdomId, train: true, ok: true);
        Debug.Log($"[KingdomBrain] k{kingdomId} ⑦招战士落地：工人#{w.npcId} → Warrior（金-{gold} 粮-{food}，兵力 {kingdom.warriorCount}）");
    }

    /// <summary>找一个本王国活工人（Worker/Porter/Civilian，对齐 workerCount 口径；确定性：npcId 最小序）。</summary>
    private UnitController FindOwnWorker()
    {
        if (UnitRegistry.Instance == null || UnitRegistry.Instance.GetAllUnits() == null) return null;
        UnitController best = null;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive) continue;
            if (u.kingdomId != kingdomId) continue;
            if (u.EffectiveOccupation != Occupation.Worker
                && u.EffectiveOccupation != Occupation.Porter
                && u.EffectiveOccupation != Occupation.Civilian) continue;   // 仅工人口径（对齐 workerCount）
            if (best == null || u.npcId < best.npcId) best = u;
        }
        return best;
    }

    /// <summary>⑧科技升级真实通道（占位可执行）：花金提升王国科技（per-kingdom 解锁态步骤11 落地的门面；执行以金可负担为门）。</summary>
    private void ExecuteTech(KingdomState kingdom, KingdomBrainConfig cfg)
    {
        int cost = Mathf.Max(1, cfg.techUpgradeCostGold);
        if (kingdom.GetResourceValue(ResourceType.Gold) < cost)
        {
            Bump(kingdomId, train: false, ok: false);
            return;
        }
        kingdom.Spend(new ResourcePack { gold = cost });
        Bump(kingdomId, train: false, ok: true);
        Debug.Log($"[KingdomBrain] k{kingdomId} ⑧科技升级落地（金-{cost}；per-kingdom 解锁态步骤11 接入）");
    }

    /// <summary>找一个可招募流浪汉（活体、Vagrant、未被招募、未入籍 kingdomId&lt;0）。固定遍历序=确定性。</summary>
    private static UnitController FindRecruitableVagrant()
    {
        if (UnitRegistry.Instance == null || UnitRegistry.Instance.GetAllUnits() == null) return null;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive) continue;
            if (u.kingdomId >= 0) continue;                       // 已入籍者不重复招
            if (u.EffectiveOccupation != Occupation.Vagrant) continue;
            if (u.IsVagrantRecruited) continue;
            return u;
        }
        return null;
    }

    /// <summary>建造类焦点真实通道：SO buildingId → 主城螺旋选址 → BuildController.TryBuild（门面校验/扣费一体）。</summary>
    private void ExecuteBuildFocus(KingdomState kingdom, KingdomBrainConfig cfg)
    {
        var def0 = UtilityActionConfig.LoadConfig().Find((UtilityAction)kingdom.focus);
        if (def0 == null || string.IsNullOrEmpty(def0.Value.buildingId))
        {
            Bump(kingdomId, train: false, ok: false);
            return;
        }
        var bdef = BuildingFactory.FindDefById(def0.Value.buildingId);
        if (bdef == null)
        {
            Bump(kingdomId, train: false, ok: false);
            Debug.LogWarning($"[KingdomBrain] k{kingdomId} 行动 {(UtilityAction)kingdom.focus} 的 buildingId={def0.Value.buildingId} 未找到 def");
            return;
        }

        var spot = FindAIBuildSpot(kingdom.id, bdef, cfg.aiBuildRadius);
        if (!spot.HasValue)
        {
            Bump(kingdomId, train: false, ok: false);
            return;   // 半径内无合法落位：明日再试
        }

        var bc = BuildController.Instance;
        if (bc == null) { Bump(kingdomId, train: false, ok: false); return; }
        bool ok = bc.TryBuild(bdef, spot.Value, GateOrientation.Horizontal, kingdomId);
        Bump(kingdomId, train: false, ok: ok);
        if (ok)
            Debug.Log($"[KingdomBrain] k{kingdomId} 建造焦点落地：{def0.Value.buildingId} @ ({spot.Value.x},{spot.Value.y})");
    }

    /// <summary>
    /// AI 选址器：以本国主城为锚的切比雪夫环带扫描（r=0..maxR 固定序=确定性），取首个放置合法微格。
    /// 合法性由 PlacementValidator 全量校验（占用/地形/水域/资源节点需求/AI 国库资源门——与玩家同套规则）。
    /// </summary>
    private static GridCoord? FindAIBuildSpot(int kingdomId, BuildingDef def, int maxRadius)
    {
        var grid = GridSystem.Instance;
        var anchorCell = FindCastleCell(kingdomId);
        if (grid == null || anchorCell == null) return null;
        var anchor = anchorCell.Value;

        for (int r = 0; r <= Mathf.Max(1, maxRadius); r++)
        {
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;   // 只扫当前环
                var cell = new GridCoord(anchor.x + dx, anchor.y + dy);
                if (!grid.IsInBounds(cell)) continue;
                var sub = grid.CellToSub(cell, 0, 0);
                if (PlacementValidator.ValidatePlacement(def, sub, GateOrientation.Horizontal, kingdomId).ok)
                    return sub;
            }
        }
        return null;
    }

    /// <summary>找某国主城（castle 建筑坐标；固定遍历序）。无主城 → null。</summary>
    private static GridCoord? FindCastleCell(int kingdomId)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return null;
        for (int i = 0; i < reg.All.Count; i++)
        {
            var b = reg.All[i];
            if (b != null && b.kingdomId == kingdomId && b.def != null && b.def.id == "castle" && b.IsActive)
                return b.coord;
        }
        return null;
    }
}