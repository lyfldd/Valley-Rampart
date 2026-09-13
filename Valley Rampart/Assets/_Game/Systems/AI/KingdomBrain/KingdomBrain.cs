using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  王国脑主脑（2_17 步骤8，D337 补八格：Foundry 创建 → D279 灭亡销毁）
//  职责：王国日 tick 权威驱动（由 DayCycleSettlement 五步②调用，非自挂 Update）。
//  每 AI 王国（id>0）一个实例；玩家(id=0)无 Brain（D338，工厂/注册表双短路）。
//  纯数据容器 + 逻辑，不挂 MonoBehaviour（单例经由 KingdomBrainRegistry 持有）。
//
//  tick 顺序：同步 simMode（D476 Abstract 期脑照跑，仅经济执行分叉）→ 采集探针快照 → ScriptStageMachine 推进
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

    /// <summary>常设军事姿态层（2_22 P0 批C / C1，D518：三档升降滞回，不占焦点槽）。</summary>
    public MilitaryPostureController Posture { get; }

    /// <summary>当前剧本阶段（快捷映射 StageMachine.Stage）。</summary>
    public ScriptStage Stage => StageMachine.Stage;

    // ===== 态势层（2_22 P0 批A / A1+A2，D517/D528/D589）=====
    /// <summary>最新统一国情快照（日 tick 子步① 全量重建后经 SituationHub 供评分域消费）。</summary>
    private SituationSnapshot _situation;
    /// <summary>损毁日志（LossEntry 流水；日 tick 聚合进快照窗口+TTL 清除；运行时态不入档——
    /// 读档后窗口重建属已知边界=快照无持久态八格口径）。</summary>
    private readonly List<LossEntry> _lossLog = new List<LossEntry>();
    /// <summary>事件缓冲：日 tick 后到达的损毁（次 tick 并入 _lossLog）。</summary>
    private readonly List<LossEntry> _pendingLosses = new List<LossEntry>();
    /// <summary>最近一次外部接触日（KingdomAttackedEvent.HitDay/UnitDiedEvent 死亡日；peaceDays 递推基准）。</summary>
    private int _lastContactDay = -1;
    /// <summary>最近事件日（脏标记；日 tick 消费后置 -1；dirtyTtlDays 异常兜底）。</summary>
    private int _dirtyDay = -1;

    /// <summary>本国将军数（A4 GeneralGap 消费；口径对齐 TrainingSystem.CanTrainGeneral L676 按 kingdomId 扫 UnitRegistry）。</summary>
    public static int CountGenerals(int kingdomId)
    {
        int count = 0;
        if (UnitRegistry.Instance != null)
        {
            foreach (var unit in UnitRegistry.Instance.GetAllUnits())
            {
                if (unit == null || unit.Data == null) continue;
                if (unit.kingdomId != kingdomId) continue;
                if (unit.EffectiveOccupation == Occupation.General) count++;
            }
        }
        return count;
    }

    // ===== 派遣落地计数（完整局批次执行面观测；运行时态不入档——读档后归零属已知边界）=====
    private struct DispatchStat { public int trainOk, buildOk, trainTry, buildTry; }
    private static readonly Dictionary<int, DispatchStat> s_dispatch = new Dictionary<int, DispatchStat>();

    /// <summary>某王国的派遣落地数（trainOk=⑥实体化数；buildOk=建造类落成受理数）。</summary>
    public static (int trainOk, int buildOk, int trainTry, int buildTry) GetDispatch(int kingdomId)
    {
        return s_dispatch.TryGetValue(kingdomId, out var s) ? (s.trainOk, s.buildOk, s.trainTry, s.buildTry) : (0, 0, 0, 0);
    }

    /// <summary>清空全部派遣计数（harness 两轮间/新开局归零用）；**同点清空退避表**（HH.224/D670 硬约束②）。</summary>
    public static void ResetDispatchStats()
    {
        s_dispatch.Clear();
        ActionBackoff.Reset();   // HH.224/D670：退避表须与派遣计数同点清零（否则 P0 B4 轮间一致滑落）
    }

    private static void Bump(int kingdomId, bool train, bool ok)
    {
        if (!s_dispatch.TryGetValue(kingdomId, out var s)) s = default;
        if (train) { s.trainTry++; if (ok) s.trainOk++; }
        else { s.buildTry++; if (ok) s.buildOk++; }
        s_dispatch[kingdomId] = s;
    }

    // ===== HH.224/D670 治本批：通用「执行失败退避」上报口 =====
    /// <summary>行动**真失败**上报（退避表；键＝kingdomId×行动id，与 `s_dispatch` 同点清零）。
    /// 🔴**仅"真失败"可调**——"已达目标/正常态"的 `Bump(ok:false)`（如 ⑦ warrior≥target／⑧ 已上限）**不得**上报，
    /// 否则会把达标误判为失败（HH.225 §一 落点清单分流）。
    /// `kind`＝失败分型（HH.226 追加项③）：`Env`＝环境让渡型（如 ⑥ 无候选，HH.28 裁决①）→ 仅标注不改行为。</summary>
    private void ReportActionFail(KingdomState kingdom, ActionBackoff.FailKind kind = ActionBackoff.FailKind.Self)
    {
        if (kingdom == null) return;
        var a = (UtilityAction)kingdom.focus;
        if (a == UtilityAction.None) return;
        int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
        float need = 0f;
        var d = UtilityActionConfig.LoadConfig().Find(a);
        if (d.HasValue) need = UtilityScorer.NeedScore(kingdom, d.Value);
        ActionBackoff.ReportFail(kingdomId, a, day, need, kind);
    }

    /// <summary>行动**成功落地**上报 ⇒ 退避计数清零（自愈之一）。</summary>
    private void ReportActionOk(KingdomState kingdom)
    {
        if (kingdom == null) return;
        var a = (UtilityAction)kingdom.focus;
        if (a == UtilityAction.None) return;
        ActionBackoff.ReportSuccess(kingdomId, a);
    }

    public KingdomBrain(int kingdomId)
    {
        this.kingdomId = kingdomId;
        Focus = new FocusController(kingdomId);
        Posture = new MilitaryPostureController(kingdomId);
    }

    /// <summary>订阅王国脑事件（王国诞生时由 Factory 调用；Unsubscribe 成对，D337/D340）。</summary>
    public void Subscribe()
    {
        Focus.Subscribe();
        // A2（2_22 P0 批A）：态势层事件源——事件只置脏标记/缓冲，不即时改快照（D517 机制）
        EventBus.Subscribe<KingdomAttackedEvent>(OnSituationAttacked);
        EventBus.Subscribe<UnitDiedEvent>(OnSituationUnitDied);
        // A6（2_22 P0 批A）：将军阵亡/编队解散上浮 → 缺将军/缺编队补任缺口感知（§3.2）
        EventBus.Subscribe<GeneralDiedEvent>(OnGeneralDied);
        EventBus.Subscribe<FormationDisbandedEvent>(OnFormationDisbanded);
    }

    /// <summary>退订全部事件（灭亡销毁钩子，D337；2_19 灭亡管线接入）。</summary>
    public void Unsubscribe()
    {
        Focus.Unsubscribe();
        EventBus.Unsubscribe<KingdomAttackedEvent>(OnSituationAttacked);
        EventBus.Unsubscribe<UnitDiedEvent>(OnSituationUnitDied);
        EventBus.Unsubscribe<GeneralDiedEvent>(OnGeneralDied);
        EventBus.Unsubscribe<FormationDisbandedEvent>(OnFormationDisbanded);
        SituationHub.Remove(kingdomId);   // 快照槽随脑退订移除（无持久态八格口径）
        PostureHub.Remove(kingdomId);     // 姿态档槽随脑退订移除（批C，同上口径）
    }

    // ===== 态势层事件处理（A2/A6：只置脏标记+缓冲，不改快照聚合值=D517 机制）=====

    private void OnSituationAttacked(KingdomAttackedEvent evt)
    {
        if (evt.KingdomId != kingdomId) return;
        int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 0;
        // D556 处方 A 终态口径：窗口按真实受击日刷新（HitDay；-1=旧行为按收到日）
        int contactDay = evt.HitDay >= 0 ? evt.HitDay : day;
        if (contactDay > _lastContactDay) _lastContactDay = contactDay;
        _dirtyDay = day;
        // C6 危机打断①被攻（2_22 §3.1 尾注/D340）：事件即时不等日 tick，焦点当日重规划
        // （FocusController 自身 OnAttacked 已置标记，InterruptReplan 内 Update 消费→防御窗口当日生效）
        InterruptFocus(day);
    }

    private void OnSituationUnitDied(UnitDiedEvent evt)
    {
        if (!(evt.Unit is UnitController uc) || uc == null) return;
        if (uc.kingdomId != kingdomId) return;   // 只记本国损毁
        if (evt.Cause == DeathCause.Demolished) return;   // 拆除不计（对齐 L128 击杀统计口径）
        int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 0;
        _pendingLosses.Add(new LossEntry { Day = day, IsBuilding = false, OccupationId = (int)uc.EffectiveOccupation });
        if (day > _lastContactDay) _lastContactDay = day;
        _dirtyDay = day;
    }

    private void OnGeneralDied(GeneralDiedEvent evt)
    {
        if (evt.KingdomId != kingdomId) return;
        int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 0;
        _dirtyDay = day;
        // C6 危机打断②军覆（2_22 §3.1 尾注/A6 事件域）：将军阵亡+无存活编队 → 当日即时重规划
        TryInterruptOnArmyWipe(day);
    }

    private void OnFormationDisbanded(FormationDisbandedEvent evt)
    {
        if (evt.KingdomId != kingdomId) return;
        int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 0;
        _dirtyDay = day;
        // C6 危机打断②军覆：编队解散（GeneralLost）后无存活编队+无将军 → 当日即时重规划
        TryInterruptOnArmyWipe(day);
    }

    /// <summary>
    /// 军覆判定+危机打断（C6）：将军数==0 且无本国存活编队 → 焦点当日重规划（不等次日 tick）。
    /// 打断事件与快照解耦（看事件本身实时查询，不读日 tick 快照聚合值=D517 机制不破坏）。
    /// </summary>
    private void TryInterruptOnArmyWipe(int day)
    {
        if (CountGenerals(kingdomId) > 0) return;
        if (CountOwnFormations() > 0) return;
        Debug.Log($"[KingdomBrain] k{kingdomId} 军覆危机（无将军+无编队）→ 当日焦点重规划（C6/D340）");
        InterruptFocus(day);
    }

    // ===== E1 存档面（2_22 P0 批E）：兵种表现统计=损毁流水入档/恢复 =====

    /// <summary>收集本国损毁流水（_lossLog 追加序，含未并入的 pending；确定性=存读同序）。
    /// 供 KingdomBrainSave.SaveState 入档（TTL 窗口连续性=读档后统计恢复）。</summary>
    public void CollectLosses(System.Collections.Generic.List<KingdomBrainSaveLossEntry> into)
    {
        if (into == null) return;
        for (int i = 0; i < _lossLog.Count; i++)
        {
            var l = _lossLog[i];
            into.Add(new KingdomBrainSaveLossEntry { day = l.Day, isBuilding = l.IsBuilding, occupationId = l.OccupationId });
        }
        for (int i = 0; i < _pendingLosses.Count; i++)
        {
            var l = _pendingLosses[i];
            into.Add(new KingdomBrainSaveLossEntry { day = l.Day, isBuilding = l.IsBuilding, occupationId = l.OccupationId });
        }
    }

    /// <summary>恢复本国损毁流水（KingdomBrainSave.ApplyPendingRestore 调）：清空现流水后按存序回填
    /// ——读档后日 tick 窗口聚合产出与存档时刻一致（兵种表现统计恢复）。</summary>
    public void RestoreSavedState(MilitaryPosture posture, int postureLastChangeDay,
        System.Collections.Generic.List<KingdomBrainSaveLossEntry> losses)
    {
        Posture.Restore(posture, postureLastChangeDay);   // 姿态档位+滞回基准（E1 件②）
        _lossLog.Clear();
        _pendingLosses.Clear();
        if (losses != null)
        {
            for (int i = 0; i < losses.Count; i++)
            {
                var e = losses[i];
                _lossLog.Add(new LossEntry { Day = e.day, IsBuilding = e.isBuilding, OccupationId = e.occupationId });
            }
        }
        _dirtyDay = -1;   // 恢复后不残留事件脏标记（存读无事件在途）
    }

    /// <summary>本国存活编队计数（实时查询；军覆判定面）。</summary>
    private int CountOwnFormations()
    {
        if (FormationManager.Instance == null) return 0;
        var fs = FormationManager.Instance.AllFormations;
        int cnt = 0;
        for (int i = 0; i < fs.Count; i++)
            if (fs[i] != null && fs[i].KingdomId == kingdomId) cnt++;
        return cnt;
    }

    /// <summary>危机打断入口：焦点即时重规划（当日生效；执行面随下次日 tick，决策当日完成）。</summary>
    private void InterruptFocus(int day)
    {
        var kingdom = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(kingdomId) : null;
        if (kingdom == null || kingdom.IsPlayer) return;
        Focus.InterruptReplan(kingdom, KingdomBrain.LoadConfig(), UtilityActionConfig.LoadConfig(), day);
    }

    /// <summary>
    /// 每日王国脑 tick（D347 五步②）。SimMode 挂细模拟→采快照→剧本推进→同步阶段→刷新焦点→焦点下发执行。
    /// 玩家/空王国短路；灭国后王国已从 Registry 移除故 Get 为空直接返回。
    /// </summary>
    public void Tick(int day)
    {
        var kingdom = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(kingdomId) : null;
        if (kingdom == null || kingdom.IsPlayer) return;   // 玩家无脑（D338）

        // D476（HH.50 裁决，HH.51 种族1 批A 对拍后小改）：Abstract 期王国脑照跑（决策/剧本/宣战判定照常，
        // 防抽象国冻结+D361 有效性+外交先例同构）——仅经济执行分叉（D459：Abstract 由 AbstractEconomySettler
        // 公式镜像结算，AIEconomySettlement 防双写已跳过；本处焦点下发实体经济指令跳过）。
        // 原实现=整 tick 短路（互斥），对拍不符 D476 → 改为脑照跑+经济执行分叉。
        var mode = SimModeManager.Instance != null ? SimModeManager.Instance.GetMode(kingdomId) : SimMode.Fine;

        var cfg = KingdomBrain.LoadConfig();
        var ucfg = UtilityActionConfig.LoadConfig();
        kingdom.simMode = mode;   // 同步真实模式（原恒写 Fine 会覆写 Abstract 态；GetMode 读同字段=幂等）
        ActionBackoff.OnDayTick(kingdomId, day);   // HH.224/D670：退避冷却自愈（连续 N 日无新失败 ⇒ 复位）

        // ① 态势层重建（2_22 P0 批A / A2，D517：日 tick 全量重建=纯函数聚合；事件只置脏标记
        // 已在 handler 侧完成。子步序断言锚=①重建→②剧本（下方 StageMachine.Tick）→③评分（Focus.Update））
        var scfg = SituationConfig.Load();
        _situation = BuildSituation(kingdom, day, scfg);
        SituationHub.Put(kingdomId, _situation);
        UpdateLearnedWeights(_situation);   // B5 局内环自整定（P0=日 tick 结算节拍，战斗结算事件 2_18 后切换）

        var ctx = BuildContext(kingdom, cfg);
        bool upgraded = StageMachine.Tick(ctx, cfg);
        kingdom.scriptPhase = StageMachine.Stage;   // 双向：升级同步 + 保持同步
        Focus.Update(kingdom, cfg, ucfg, day);      // D322 焦点模型（底线→评分→防抖切换）→ kingdom.focus=行动id
        if (mode == SimMode.Fine)
            ExecuteFocus(kingdom, cfg);             // 焦点下发真实执行（完整局批次接通）；Abstract 期=经济执行分叉跳过

        // ⑥ 姿态层（2_22 P0 批C / C1，D518：焦点外常态行为——不占国策焦点槽）。
        // 消费当日快照+国库军力做三档升降（滞回防抖），随档驱动 C3 巡逻/C4 驻防/C5 动员行为。
        Posture.Evaluate(_situation, kingdom, scfg, MilitaryPostureConfig.Load(), day);
        if (mode == SimMode.Fine)
            ExecutePosture(kingdom, MilitaryPostureConfig.Load());   // Abstract 期姿态行为同样分叉跳过（经济执行分叉同款）

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

    // ===== 态势层构建（2_22 P0 批A / A2，D517 五件聚合 + D589 内源节拍时间场）=====

    /// <summary>
    /// 统一国情快照构建（日 tick 子步①）。纯函数聚合：
    /// 威胁分布/边境接触面=TerritorySystem 邻接查询（A5 同口径）× KingdomRegistry 兵力；
    /// 军力现状=本国战士（机器战力入口径：B8 落地前 MachineCount 恒 0=零行为差异，D570）；
    /// 损毁清单/兵种表现统计=窗口聚合（事件缓冲次 tick 并入+TTL 滚动）；
    /// peaceDays=内源节拍时间场（D589：无接触日自然递推——评分域军事缺口内源化数据地基）。
    /// 邻接表按 id 升序保确定性（同 seed 红线）。
    /// </summary>
    private SituationSnapshot BuildSituation(KingdomState k, int day, SituationConfig cfg)
    {
        // 事件缓冲并入流水+TTL 清除（消费脏标记：构建后清 _dirtyDay）
        if (_pendingLosses.Count > 0)
        {
            _lossLog.AddRange(_pendingLosses);
            _pendingLosses.Clear();
        }
        int ttl = cfg != null ? cfg.lossTtlDays : 7;
        if (ttl > 0)
            _lossLog.RemoveAll(l => day - l.Day >= ttl);

        var snap = new SituationSnapshot
        {
            KingdomId = kingdomId,
            Day = day,
            OwnWarriorCount = k.warriorCount,
            // A2/D570 军力现状口径含机器（E4 冒烟 P9f 实锤接线缺口：B8 落地后恒 0 未接线——
            // SiegeProductionSystem.GetPlacedMachineCountByKingdom 单源计数）
            MachineCount = SiegeProductionSystem.Instance != null
                ? SiegeProductionSystem.Instance.GetPlacedMachineCountByKingdom(kingdomId) : 0,
            Threats = new List<ThreatEntry>(),
            Losses = new List<LossEntry>(_lossLog),
            PeaceDays = _lastContactDay < 0 ? -1 : day - _lastContactDay,
            Dirty = _dirtyDay >= 0,   // 周期内发生过事件（脏标记 TTL 兜底=异常未消费态）
            PopulationBlockPlaceholder = true // ③人口盘点块占位：真值随 2_23 资源 P1
        };
        _dirtyDay = -1;   // 日 tick 消费完毕（下一事件重新置位）

        // 威胁分布+边境接触面（A5 邻接口径；id 升序保确定性）
        var ts = TerritorySystem.Instance;
        var reg = KingdomRegistry.Instance;
        if (ts != null && reg != null)
        {
            var adj = new List<int>(ts.GetAdjacentKingdoms(kingdomId));
            adj.Sort();
            for (int i = 0; i < adj.Count; i++)
            {
                var other = reg.Get(adj[i]);
                if (other == null || other.IsPlayer) continue;   // 玩家不入威胁分布（与 NeighborMilitary 同口径）
                snap.Threats.Add(new ThreatEntry { KingdomId = other.id, WarriorCount = other.warriorCount });
            }
        }
        snap.BorderContactCount = snap.Threats.Count;

        // 军事维度缺口真值（A4 数据源）：将军数/编队数/在场战斗职业去重（确定性升序）
        snap.GeneralCount = CountGenerals(kingdomId);
        var ownedCombat = new HashSet<int>();
        if (UnitRegistry.Instance != null)
        {
            foreach (var unit in UnitRegistry.Instance.GetAllUnits())
            {
                if (unit == null || unit.Data == null || unit.kingdomId != kingdomId) continue;
                var occ = unit.EffectiveOccupation;
                if (MilitaryProfessions.IsCombat(occ)) ownedCombat.Add((int)occ);
            }
        }
        snap.OwnedCombatOccupations = new List<int>(ownedCombat);
        snap.OwnedCombatOccupations.Sort();
        if (FormationManager.Instance != null)
        {
            var fs = FormationManager.Instance.AllFormations;
            int cnt = 0;
            for (int i = 0; i < fs.Count; i++)
                if (fs[i] != null && fs[i].KingdomId == kingdomId) cnt++;
            snap.FormationCount = cnt;
        }

        // 内源势能三输入（D590 增补节②；现算轻量口径=R-A1 前占位，R-A1 真值块落地后接替——
        // 口径如实注记：经济=gold/100、人口=(worker+warrior)/20、仓储=food/50，全 clamp01）
        snap.DriveEconomic = Mathf.Clamp01(k.resources.gold / 100f);
        snap.DrivePopPressure = Mathf.Clamp01((k.workerCount + k.warriorCount) / 20f);
        snap.DriveStorage = Mathf.Clamp01(k.GetResourceValue(ResourceType.Food) / 50f);

        // ② 经济诊断块真值（2_23 资源 P0 批A / R-A1；D630 收支口径 A+，日 tick 全量重建）
        // 注：内源势能三输入（上方）本批维持既有现算口径不动——"真值块接替"=消费面扩展，
        //     归批B/C（本批=就位+可产出，L-01：真值就位≠本批消费），避免引入行为漂移。
        snap.Economy = BuildEconomyBlock(k, day);

        // 兵种表现统计骨架：窗口内 per 职业死亡计数（确定性聚合；表现分=B5 战斗结算域）
        var deaths = new Dictionary<int, int>();
        for (int i = 0; i < _lossLog.Count; i++)
        {
            var l = _lossLog[i];
            if (l.IsBuilding) continue;
            deaths.TryGetValue(l.OccupationId, out int n);
            deaths[l.OccupationId] = n + 1;
        }
        snap.UnitPerformance = new List<UnitPerfStat>(deaths.Count);
        foreach (var kv in deaths)
            snap.UnitPerformance.Add(new UnitPerfStat { OccupationId = kv.Key, Deaths = kv.Value });
        snap.UnitPerformance.Sort((a, b) => a.OccupationId.CompareTo(b.OccupationId));   // 确定性序
        return snap;
    }

    // ===== 经济诊断块构建（2_23 资源 P0 批A / R-A1；D630 收支口径 A+）=====

    /// <summary>
    /// 经济诊断块取数（Unity 侧适配层，对齐 AbstractEconomySettlement.BuildSnapshot 分工）：
    /// KingdomState 台账号 + BuildingRegistry 盘点 + 人口派生计数 → EconomyInput（纯 DTO）
    /// → EconomyDiagnosis.Build（纯函数四件真值）。收支窗口＝自上次重建以来累计，取后清零。
    /// 确定性：建筑遍历固定排序（同 seed 两轮逐字节一致；⑤-3 硬性 a 同款纪律）。
    /// </summary>
    private static EconomyBlock BuildEconomyBlock(KingdomState k, int day)
    {
        var dcfg = KingdomDiagnosisConfig.Load();
        var mcfg = MustHaveConfig.Load();
        int pop = k.workerCount + k.warriorCount;

        var buildings = QueryKingdomBuildingsSorted(k.id);
        var prod = new Dictionary<int, ProductionEntry>();
        int storageUsed = 0, storageCap = 0, fortCount = 0, gatherNodes = 0, warehouses = 0;

        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            var def = b.def;
            if (def == null) continue;

            if (b.IsFortification) fortCount++;
            if (def.isResourceNode) gatherNodes++;                        // 实体型资源点（采集点口径）
            if (def.id == "Warehouse" || def.id == "Granary") warehouses++;

            var storage = b.GetComponent<StorageComponent>();
            if (storage != null)
            {
                storageUsed += storage.storedAmount;
                storageCap += storage.capacity;
            }

            int eco = MapProduceToEco(def);
            if (eco < 0) continue;
            prod.TryGetValue(eco, out var entry);
            entry.Resource = (EcoResource)eco;
            entry.Count += 1;
            entry.LevelSum += b.level;
            entry.RateSum += def.producer.rate;
            entry.WorkersSum += def.concurrentWorkers;
            prod[eco] = entry;
        }

        var production = new List<ProductionEntry>(prod.Count);
        foreach (var kv in prod) production.Add(kv.Value);

        var input = new EconomyInput
        {
            KingdomId = k.id,
            Day = day,
            StockGold = k.resources.gold,
            StockStone = k.resources.stone,
            StockWood = k.resources.wood,
            StockFood = k.resources.food,
            StockMetal = k.resources.metal,
            Flow = EconomyDiagnosis.TakeFlow(k.id),   // 读后清零（纯函数化关键，D630 A+）
            WorkerCount = k.workerCount,
            WarriorCount = k.warriorCount,
            Production = production,
            StorageUsed = storageUsed,
            StorageCapacity = storageCap,
            FortCount = fortCount,
            GatherNodeCount = gatherNodes,
            WarehouseCount = warehouses,
            IsAbstract = k.simMode == SimMode.Abstract,
            Baseline = mcfg.ResolveBaseline(pop),
            GrainConsumptionPerPop = dcfg != null ? dcfg.grainConsumptionPerPop : 1,
            FarmPerPeopleBaseline = dcfg != null ? dcfg.capacityPerPeopleBaseline : 0f,
            GatherNodePerPopBaseline = dcfg != null ? dcfg.gatherNodePerPopBaseline : 0f
        };
        return EconomyDiagnosis.Build(input);
    }

    /// <summary>产能建筑 → 五元资源映射（对齐 AIEconomySettlement.MapToPack/AbstractEconomySettlement 口径）；-1=非五元跳过。
    /// 2_23 资源 P0 批C·C′（D644 裁 A）：加**真产能守卫**——口径对齐 BuildingFactory.cs:295
    /// （rate>0 && kind==Resource && !isResourceNode），只计真产能建筑。
    /// 修前缺陷：缺守卫致仓储类（Granary→Food／Warehouse→Wood）与非产能建筑（~25 个 outputResource 默认 0=Gold、
    /// wood_pile/stone_pile）被计入产能盘点 ⇒ 掩蔽 ⑤通道A 与 ③R-C2 产能缺口判定（Farm 被摧毁后不重建粮产能）。
    /// 零行为漂移：工厂侧本就未给 rate=0 / isResourceNode 建筑挂 ProducerComponent（实际产出未变），本改仅收口诊断口径。</summary>
    private static int MapProduceToEco(BuildingDef def)
    {
        if (def == null || def.producer.kind != ProduceKind.Resource
            || def.producer.rate <= 0f || def.isResourceNode) return -1;   // C′：真产能守卫（D644）
        if (def.isBlacksmith) return (int)EcoResource.Metal;   // 铁匠铺 矿石→Metal（D200/D609）
        switch (def.outputResource)
        {
            case ResourceType.Gold: return (int)EcoResource.Gold;
            case ResourceType.Stone: return (int)EcoResource.Stone;
            case ResourceType.Wood: return (int)EcoResource.Wood;
            case ResourceType.Food: return (int)EcoResource.Food;
            case ResourceType.Metal: return (int)EcoResource.Metal;
            default: return -1;   // Ore/Crystal/FireOil/Meat/SpecialFood/Water 等：非五元，不入诊断产能盘点
        }
    }

    /// <summary>某王国 Active 建筑（固定排序：坐标→def.id；⑤-3 硬性 a 同款，禁依赖注册序/收集序）。</summary>
    private static List<Building> QueryKingdomBuildingsSorted(int kingdomId)
    {
        var list = new List<Building>();
        var reg = BuildingRegistry.Instance;
        if (reg != null && reg.All != null)
        {
            var src = reg.All;
            for (int i = 0; i < src.Count; i++)
                if (src[i] != null && src[i].kingdomId == kingdomId && src[i].IsActive)
                    list.Add(src[i]);
        }
        list.Sort((a, b) =>
        {
            if (a.coord.y != b.coord.y) return a.coord.y.CompareTo(b.coord.y);
            if (a.coord.x != b.coord.x) return a.coord.x.CompareTo(b.coord.x);
            var ad = a.def != null ? a.def.id : "";
            var bd = b.def != null ? b.def.id : "";
            return string.CompareOrdinal(ad, bd);
        });
        return list;
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
            case UtilityAction.BoostHarvest:
            case UtilityAction.BuildWall:
            // HH.86 件3b/3c 六新建造行动：路由到同一 ExecuteBuildFocus（SO buildingId 通用通道）
            case UtilityAction.BuildWell:
            case UtilityAction.BuildBlacksmith:
            case UtilityAction.BuildWarAcademy:
            case UtilityAction.BuildWarCamp:
            case UtilityAction.BuildLeyForge:
            case UtilityAction.BuildArcheryRange:
                ExecuteBuildFocus(kingdom, cfg);
                break;
            // 2_23 资源 P0 批C（R-C2，D639 列报8）：③建产能种类诊断化——不再恒 quarry，
            // 执行时按快照产能缺口动态解最缺产能 def（反查表 SO 驱动；无缺口/表缺省 → null=沿用 SO buildingId）
            case UtilityAction.BuildCapacity:
                ExecuteBuildFocus(kingdom, cfg, ResolveTriageDefId(kingdom.id));
                break;
            // 2_23 资源 P0 批C（R-C1，D639 全列报）：⑤屯粮改根因三通道分诊
            //   （触发→A→B→C 固定序→断供严重度 粮→金→石→木；单通道响应不叠加；铁不参与触发）
            case UtilityAction.Grain:
                ExecuteGrainTriage(kingdom, cfg);
                break;
            case UtilityAction.RecruitWarrior:
                ExecuteRecruitArmy(kingdom, cfg);
                break;
            // 2_22 P0 批B：⑯训练将军 + ㉔/㉕战争机器（路由追加；建造类 23/24/25 走既有 ExecuteBuildFocus 通用通道）
            case UtilityAction.TrainGeneral:
                ExecuteTrainGeneral(kingdom, cfg);
                break;
            case UtilityAction.BuildBarracks:
            case UtilityAction.BuildTrainingCamp:
            case UtilityAction.BuildSiegeWorkshop:
                ExecuteBuildFocus(kingdom, cfg);
                break;
            case UtilityAction.ProduceMachine:
                ExecuteProduceMachine(kingdom, cfg);
                break;
            // HH.221/D685 A②：㉗ 采集世界资源点——决策核**只出粗意图**（"缺 X → 下发采集"），
            // 选点/可达/派工/距离一律归 WorldGatherRegistry + TaskScheduler（禁 O-3 决策核直选目标；
            // 与建造类经门面同构：本处只调**门面**，不碰具体资源点）。
            case UtilityAction.GatherWorldResource:
                ExecuteWorldGatherFocus(kingdom, cfg);
                break;
            case UtilityAction.Tech:
                ExecuteTech(kingdom, cfg);
                break;
            case UtilityAction.Expand:
                ExecuteExpand(kingdom, cfg);
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

    // ===== 姿态层行为驱动（2_22 P0 批C / C3 巡逻 + C4 驻防 + C5 动员，D518 §3.3）=====

    /// <summary>
    /// 姿态档下发行为（日 tick 子步⑥，SimMode.Fine 才执行）。档位语义：
    /// 无=回收巡逻（无常态军事行为）；警戒=补巡逻到目标数（C3，主威胁方向）；
    /// 动员=停止远程派遣+召回（StopPatrol）+守军编队自动派驻（C4 集结守军）。
    /// </summary>
    private void ExecutePosture(KingdomState kingdom, MilitaryPostureConfig pcfg)
    {
        var posture = Posture.Current;
        if (posture == MilitaryPosture.Alert)
        {
            ExecuteAlertPatrol(pcfg);   // C3 警戒巡逻
            return;
        }
        if (posture == MilitaryPosture.Mobilized)
        {
            ExecuteMobilize(kingdom, pcfg);   // C4 驻防 + C5 召回
            return;
        }
        // None：无常态军事行为——回收既有巡逻（警戒解除后士兵回归决策核常态；
        // 档位滞回窗保护下不会反复启停 churn）
        StopAllOwnPatrols();
    }

    /// <summary>C3 警戒档巡逻：本国空闲战斗单位补巡逻到目标数（方向=主威胁方向；npcId 升序确定性）。</summary>
    private void ExecuteAlertPatrol(MilitaryPostureConfig pcfg)
    {
        int want = pcfg != null ? Mathf.Max(0, pcfg.alertPatrolCount) : 2;
        if (want <= 0) return;
        var dirOpt = ResolveMainThreatDirection();
        if (!dirOpt.HasValue) return;   // 无威胁锚（降档竞态/主城缺失）→ 本轮不补

        int patrolling = 0;
        var candidates = new List<UnitController>();
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units != null)
        {
            foreach (var u in units)
            {
                if (u == null || !u.IsAlive || u.kingdomId != kingdomId) continue;
                if (!MilitaryProfessions.IsCombat(u.EffectiveOccupation)) continue;
                var brain = u.GetComponent<NPCBrain>();
                if (brain != null && brain.HasFormationSlot) continue;   // 编队士兵不拉去巡逻
                if (brain != null && PatrolTaskSystem.IsPatrolling(brain)) { patrolling++; continue; }
                candidates.Add(u);
            }
        }
        candidates.Sort((a, b) => a.npcId.CompareTo(b.npcId));   // 确定性：npcId 升序
        for (int i = 0; i < candidates.Count && patrolling < want; i++)
        {
            var brain = candidates[i].GetComponent<NPCBrain>();
            if (brain == null) continue;
            PatrolTaskSystem.StartPatrol(brain, dirOpt.Value);
            patrolling++;
        }
    }

    /// <summary>主威胁方向（C3）：快照威胁表首个非零兵国主城 → 本国主城指向（表序=id 升序确定性）。</summary>
    private Vector2? ResolveMainThreatDirection()
    {
        if (_situation?.Threats == null || _situation.Threats.Count == 0) return null;
        int threatKid = -1;
        for (int i = 0; i < _situation.Threats.Count; i++)
            if (_situation.Threats[i].WarriorCount > 0) { threatKid = _situation.Threats[i].KingdomId; break; }
        if (threatKid < 0) return null;
        var own = FindCastleCell(kingdomId);
        var foe = FindCastleCell(threatKid);
        if (!own.HasValue || !foe.HasValue) return null;
        Vector2 d = new Vector2(foe.Value.x - own.Value.x, foe.Value.y - own.Value.y);
        return d.sqrMagnitude > 1e-6f ? d.normalized : (Vector2?)null;
    }

    /// <summary>C4+C5 动员档：停止远程派遣+召回（StopPatrol 本国巡逻单位）+守军编队自动派驻（集结守军）。</summary>
    private void ExecuteMobilize(KingdomState kingdom, MilitaryPostureConfig pcfg)
    {
        // C5 停止远程派遣+召回：本国全部巡逻单位停巡（士兵回归 AI 决策核常态=自然回城；
        // ⑪出征本为占位桩无派遣面，被宣战硬触发器=2_18 P1 挂点不实现）
        StopAllOwnPatrols();
        // C4 守军集结：目标数内自动派驻 isGarrison 守城编队（锚点=本国工事，无则主城）
        int want = pcfg != null ? Mathf.Max(1, pcfg.mobilizeGuardSquads) : 1;
        int have = CountOwnGarrisonSquads();
        for (int n = have; n < want; n++)
        {
            if (!TrySpawnGarrisonSquad()) break;
        }
    }

    /// <summary>本国巡逻单位全停（C5 召回；确定性遍历）。</summary>
    private void StopAllOwnPatrols()
    {
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units == null) return;
        foreach (var u in units)
        {
            if (u == null || !u.IsAlive || u.kingdomId != kingdomId) continue;
            var brain = u.GetComponent<NPCBrain>();
            if (brain != null && PatrolTaskSystem.IsPatrolling(brain)) PatrolTaskSystem.StopPatrol(brain);
        }
    }

    /// <summary>本国守军（isGarrison）编队计数（C4 目标数判定面）。</summary>
    private int CountOwnGarrisonSquads()
    {
        if (FormationManager.Instance == null) return 0;
        var fs = FormationManager.Instance.AllFormations;
        int cnt = 0;
        for (int i = 0; i < fs.Count; i++)
            if (fs[i] != null && fs[i].KingdomId == kingdomId && fs[i].isGarrison) cnt++;
        return cnt;
    }

    /// <summary>
    /// C4 守军编队自动派驻：AddComponent 既有模式（镜像 B7 将军成军/AIDebugSpawnController 样例链
    /// =L-06 生产链合规，士兵成员来自场景 FindIdleSoldiers 真实单位非裸构）；
    /// 锚点=本国工事建筑（IsFortification）transform，无工事回退主城 transform。
    /// 口径注记：跨国招兵已修（HH.144 件1/D598）——FindIdleSoldiers 增 kingdomId 过滤
    ///（过滤锚=ResolveKingdomId override），守军编队成员国籍纯度由 override+过滤双保险。
    /// </summary>
    private bool TrySpawnGarrisonSquad()
    {
        Transform anchor = FindFortificationAnchor(kingdomId);
        if (anchor == null) anchor = FindCastleTransform(kingdomId);
        if (anchor == null) return false;
        var go = new GameObject($"AI_Garrison_k{kingdomId}");
        var fc = go.AddComponent<FormationController>();
        // r3 修（P5 实锤）：faction 镜像 B7 先例（TrainingSystem：fc.faction=将军单位 Data.faction）——
        // 守军编队无将军→取本国任一活体单位。AI 王国单位 faction 实测=PlayerCamp（归属面=kingdomId 既有模型），
        // 硬设 AiKingdom 会让 FindIdleSoldiers 的 faction 过滤落空=0 候选→空壳。
        fc.faction = ResolveKingdomFaction(kingdomId);
        if (fc.formationTable == null)
            fc.formationTable = Resources.Load<FormationTable>("Formations/FormationTable");
        fc.InitGarrison(anchor);
        // r3 修（HH.140 P5 实锤）：显式归属——招募池为共享阵营面时成员国籍混杂，靠首成员反推会漂移
        //（实测 kid=0/3：玩家/他国兵入编→守军计数按 id 过滤落空）。
        fc.SetKingdomIdOverride(kingdomId);
        fc.RecruitStandard();
        if (fc.MemberCount <= 0)
        {
            // r3 修（P5 实锤）：中区块编队上限等空间约束拒绝招募→0 成员无将军→KingdomId 解析=-1
            // →计数面（KingdomId==kingdomId && isGarrison）永不满足→have 恒 0→每次动员重复建壳堆积
            //（与批B D594"计数面与创建面失配→无限循环"同型）。即建即毁止堆积，明日再试。
            Object.Destroy(go);
            Debug.Log($"[KingdomBrain] k{kingdomId} 守军编队招募落空（空间约束/无兵源）→销毁空壳明日再试（C4）");
            return false;
        }
        Debug.Log($"[KingdomBrain] k{kingdomId} 动员档守军编队派驻 @ {anchor.name}（C4，锚=工事/主城，成员={fc.MemberCount}）");
        return true;
    }

    /// <summary>本国单位真实 faction（守军编队 faction 取值源；无本国活体单位→AiKingdom 兜底）。</summary>
    private static Faction ResolveKingdomFaction(int kid)
    {
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units != null)
        {
            foreach (var u in units)
            {
                if (u == null || !u.IsAlive || u.kingdomId != kid) continue;
                if (u.Data != null) return u.Data.faction;
            }
        }
        return Faction.AiKingdom;
    }

    /// <summary>找本国工事建筑 transform（IsFortification 既有判定=⑨城墙类；固定遍历序确定性）。</summary>
    private static Transform FindFortificationAnchor(int kid)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return null;
        for (int i = 0; i < reg.All.Count; i++)
        {
            var b = reg.All[i];
            if (b != null && b.kingdomId == kid && b.IsActive && b.IsFortification)
                return b.transform;
        }
        return null;
    }

    /// <summary>找某国主城建筑 transform（守军编队兜底锚点；固定遍历序确定性）。</summary>
    private static Transform FindCastleTransform(int kid)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return null;
        for (int i = 0; i < reg.All.Count; i++)
        {
            var b = reg.All[i];
            if (b != null && b.kingdomId == kid && b.def != null && b.def.id == "castle" && b.IsActive)
                return b.transform;
        }
        return null;
    }

    /// <summary>⑥招工人真实通道：流浪汉 → 本国工人（D345 人口增长唯一途径，防卡死关键路径）。</summary>
    private void ExecuteRecruitWorker(KingdomState kingdom, KingdomBrainConfig cfg)
    {
        int cost = Mathf.Max(1, cfg.aiRecruitFoodCost);
        UnitController vagrant = FindRecruitableVagrant();
        if (vagrant == null)
        {
            Bump(kingdomId, train: true, ok: false);
            ReportActionFail(kingdom, ActionBackoff.FailKind.Env);   // HH.226 追加项③：⑥无候选＝**环境让渡型**（HH.28 裁决①·仅标注）
            // HH.81/D542 件1 静默双坑修（P0 调优观测口）：无候选时打诊断——流浪池活体+守卫拒绝计数。
            // 口径注记：pool=Vagrant 职业活体总数；拒绝计数按 ⑥守卫序分桶（已入籍/已招募/异族），
            // 与 FindRecruitableVagrant 的 alive→kingdomId→Vagrant→Recruited→race 过滤序等价自洽（观测非判定）。
            int pool = 0, dIngrid = 0, dRecruited = 0, dRace = 0;
            var regUnits = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
            if (regUnits != null)
            {
                foreach (var u in regUnits)
                {
                    if (u == null || !u.IsAlive) continue;
                    if (u.EffectiveOccupation != Occupation.Vagrant) continue;
                    pool++;
                    if (u.kingdomId >= 0) dIngrid++;
                    else if (u.IsVagrantRecruited) dRecruited++;
                    else if (u.raceId != KingdomRace.GetKingdomRace(kingdomId)) dRace++;
                }
            }
            Debug.Log($"[KingdomBrain] k{kingdomId} ⑥招工无候选：流浪池活体={pool}（已入籍拒{dIngrid}/已招募拒{dRecruited}/异族拒{dRace}）");
            return;   // 无候选（营地无流浪汉）：派遣尝试失败，明日再试（不空转硬造人口）
        }
        if (kingdom.GetResourceValue(ResourceType.Food) < cost)
        {
            Bump(kingdomId, train: true, ok: false);
            ReportActionFail(kingdom);   // HH.224/D670：⑥真失败（粮不足）
            Debug.Log($"[KingdomBrain] k{kingdomId} ⑥招粮不足（需{cost}）");
            return;
        }

        kingdom.Spend(new ResourcePack { food = cost });   // AI 台账扣费（镜像玩家 recruitFoodCost 语义）
        int converted = KingdomFoundry.ConvertVagrantsToWorkers(
            new List<int> { vagrant.npcId }, kingdomId);
        bool ok = converted > 0;
        Bump(kingdomId, train: true, ok: ok);
        if (ok) ReportActionOk(kingdom); else ReportActionFail(kingdom);   // HH.224/D670：⑥成败上报
        if (ok)
            Debug.Log($"[KingdomBrain] k{kingdomId} ⑥招工人落地：流浪汉#{vagrant.npcId} → Worker（粮-{cost}）");
    }

    /// <summary>
    /// ⑦招兵候选集（HH.194/D656 硬条款1 同源 helper）：可训域 ∩ 建筑在场 ∩ 军事职业 ∩ 非 General（确定性升序）。
    /// 评分侧（UtilityScorer.Feasible RecruitWarrior）与执行侧（<see cref="ExecuteRecruitArmy"/>）**共用本 helper**
    /// ⇒ "评分可执行 ⇔ 执行候选非空"单源——禁两侧各自手搓近似漂移（HH.193 根因同型，L-31）。
    /// 候选域（D570 细化）：TrainingDef raceId∈{-1,本族} 且 IsCombat 且 非 General（⑯专属域）；
    /// 建筑前置联动：兵种训练建筑不在场/等级不足 → 该兵种不入候选（由 ⑰ MilitaryBuildingGap 缺口评分导向先建——双向咬合）。
    /// </summary>
    internal static List<(TrainingDef def, Building b, float score)> CollectRecruitCandidates(int kingdomId)
    {
        var result = new List<(TrainingDef def, Building b, float score)>();
        var tcfg = Resources.Load<TrainingConfig>("Config/TrainingConfig");
        int myRace = KingdomRace.GetKingdomRace(kingdomId);
        if (tcfg == null || tcfg.trainings == null) return result;

        var seen = new HashSet<Occupation>();
        for (int i = 0; i < tcfg.trainings.Length; i++)
        {
            var t = tcfg.trainings[i];
            if (t.raceId != -1 && t.raceId != myRace) continue;                    // D419/D570 族门禁预过滤
            if (t.toOccupation == Occupation.General) continue;                    // ⑯专属域
            if (!MilitaryProfessions.IsCombat(t.toOccupation)) continue;           // 军事域
            if (!seen.Add(t.toOccupation)) continue;                               // 同兵种多条目去重（首个=优先）
            var b = FindKingdomBuilding(kingdomId, t.buildingId);                  // 建筑前置联动
            if (b == null) continue;                                               // 缺建筑→本轮不可选招（防空转）
            if (t.minBuildingLevel > 0 && b.level < t.minBuildingLevel) continue;  // 建筑等级未到（⑰升级导向）
            result.Add((t, b, 0f));
        }
        return result;
    }

    /// <summary>⑦ 评分侧建筑前置（D656 硬条款1）：与执行侧候选集同源（=CollectRecruitCandidates 非空）。</summary>
    internal static bool HasRecruitCandidate(int kingdomId)
        => CollectRecruitCandidates(kingdomId).Count > 0;

    /// <summary>
    /// ⑦招兵扩多兵种（2_22 P0 批B / B6，§3.4 双环内环消费端）：按双环权重选招，不再是 Warrior 直转。
    /// 招募分 = 出厂倾向(B4 RaceDef.unitPriors) × 性格调制(好战轴) × 局内环学习权重(B5) × 经济可负担 ÷ 多样性惩罚。
    /// 候选域见 <see cref="CollectRecruitCandidates"/>（D656 起评分/执行同源共用）。
    /// 执行=TryTrainFromKingdomPool（B1 系统级入口，AI 与玩家同链：国库扣费/族门禁/建筑等级）；
    /// 兵源池=Resident（训练链 fromOccupation 源）；池空时 Worker 先转 Resident（AI 编制内调配，如实列报）。
    /// 确定性：候选按 Occupation int 升序遍历，同分取小 id。
    /// </summary>
    private void ExecuteRecruitArmy(KingdomState kingdom, KingdomBrainConfig cfg)
    {
        if (kingdom.warriorCount >= UtilityScorer.MilitaryTarget(kingdom, cfg))
        {
            Bump(kingdomId, train: true, ok: false);
            return;   // 已达兵力目标（D348 门控兜底）
        }

        var raceDef = KingdomRace.GetKingdomRaceDef(kingdomId);
        if (raceDef == null) { Bump(kingdomId, train: true, ok: false); ReportActionFail(kingdom, ActionBackoff.FailKind.Env); return; }   // HH.228／D680 追记：⑦RaceDef **数据/资产缺失**（`KingdomRace.cs:50-66`＝kingdomId<0 或 `Config/Races` 无该 raceId 资产）⇒ 配置/资产给定物（Env）

        // 候选集（D656 同源 helper：评分侧 Feasible 同用，禁本地手搓）
        var candidates = CollectRecruitCandidates(kingdomId);
        if (candidates.Count == 0)
        {
            Bump(kingdomId, train: true, ok: false);
            ReportActionFail(kingdom);   // HH.224/D670：⑦真失败（无可选招兵种）
            return;   // 无可选招兵种（缺建筑/缺 def）→ ⑰建造缺口评分导向
        }

        // 双环招募分（确定性：遍历序=candidates 追加序=TrainingConfig 顺序×去重）
        float militant = kingdom.personality != null && kingdom.personality.Length > 0 ? kingdom.personality[0] : 0.5f;
        float bestScore = -1f;
        int bestIdx = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            var t = candidates[i].def;
            float prior = raceDef.GetUnitPrior(t.toOccupation);                    // B4 出厂先验
            float personalityMod = 1f + (militant - 0.5f) * 0.2f;                  // 性格调制（好战轴保守 ±10%）
            float learned = BattleLearnedWeights.Get(kingdomId, (int)t.toOccupation); // B5 局内环
            // 经济可负担（effective 成本近似=base×trainCostMul ceil，与 TryTrain 同口径）
            float costMul = raceDef.trainCostMul;
            bool affordable = kingdom.resources.gold >= Mathf.CeilToInt(t.costGold * costMul)
                           && (t.costCrystal <= 0 || kingdom.crystal >= Mathf.CeilToInt(t.costCrystal * costMul))
                           && (t.costMetal <= 0 || kingdom.resources.metal >= Mathf.CeilToInt(t.costMetal * costMul));
            if (!affordable) continue;                                             // 经济不可负担→本轮不选
            float score = prior * personalityMod * learned;
            candidates[i] = (t, candidates[i].b, score);
            if (score > bestScore) { bestScore = score; bestIdx = i; }
        }
        if (bestIdx < 0)
        {
            Bump(kingdomId, train: true, ok: false);
            ReportActionFail(kingdom);   // HH.224/D670：⑦真失败（无可负担候选）
            Debug.Log($"[KingdomBrain] k{kingdomId} ⑦选招无可负担候选（资源不足），明日再试");
            return;
        }

        var chosen = candidates[bestIdx].def;
        var chosenB = candidates[bestIdx].b;
        bool ok = TrainingSystem.Instance != null
                  && TrainingSystem.Instance.TryTrainFromKingdomPool(kingdomId, chosenB, chosen.toOccupation);
        if (!ok && FindOwnWorker() != null)
        {
            // 兵源池兜底：Resident 池空但 Worker 在 → Worker 先转 Resident（AI 编制内调配）再训练（如实列报 HH.138）
            var w = FindOwnResident() ?? FindOwnWorker();
            if (w != null && w.EffectiveOccupation != Occupation.Resident) w.SetOccupation(Occupation.Resident);
            ok = TrainingSystem.Instance != null
                 && TrainingSystem.Instance.TryTrainFromKingdomPool(kingdomId, chosenB, chosen.toOccupation);
        }
        Bump(kingdomId, train: true, ok: ok);
        if (ok) ReportActionOk(kingdom); else ReportActionFail(kingdom);   // HH.224/D670：⑦成败上报
        if (ok)
            Debug.Log($"[KingdomBrain] k{kingdomId} ⑦多兵种选招落地：→ {chosen.toOccupation}（先验{raceDef.GetUnitPrior(chosen.toOccupation):F2}×性格{militant:F2}×学习{BattleLearnedWeights.Get(kingdomId, (int)chosen.toOccupation):F2}，@{chosenB.def.id}）");
    }

    /// <summary>
    /// 2_22⑯ 训练将军（B2）：执行=本国兵营 Barracks 队列训练 General（与玩家同链 generalLimit=2）。
    /// 成军链（B7）由将军毕业事件驱动（TrainingSystem 出队→BindGeneral→RecruitStandard，见 B7 接线）。
    /// </summary>
    private void ExecuteTrainGeneral(KingdomState kingdom, KingdomBrainConfig cfg)
    {
        var barracks = FindKingdomBuilding(kingdomId, BuildingIds.Barracks);
        if (barracks == null)
        {
            Bump(kingdomId, train: true, ok: false);
            ReportActionFail(kingdom);   // HH.224/D670：⑯真失败（无兵营）
            return;   // 无兵营 → ⑰建兵营缺口评分导向先建
        }
        bool ok = TrainingSystem.Instance != null
                  && TrainingSystem.Instance.TryTrainFromKingdomPool(kingdomId, barracks, Occupation.General);
        if (!ok && FindOwnWorker() != null)
        {
            var w = FindOwnResident() ?? FindOwnWorker();
            if (w != null && w.EffectiveOccupation != Occupation.Resident) w.SetOccupation(Occupation.Resident);
            ok = TrainingSystem.Instance != null
                 && TrainingSystem.Instance.TryTrainFromKingdomPool(kingdomId, barracks, Occupation.General);
        }
        Bump(kingdomId, train: true, ok: ok);
        if (ok) ReportActionOk(kingdom); else ReportActionFail(kingdom);   // HH.224/D670：⑯成败上报
        if (ok) Debug.Log($"[KingdomBrain] k{kingdomId} ⑯训练将军入队（兵营，队列中）");
    }

    /// <summary>
    /// 2_22㉕ 造战争机器（B8，D558→D570）：执行=SiegeProductionSystem.ProduceMachine(type,spawnPos,kingdomId)
    /// AI overload 同链（族门禁 IsRaceAllowedMachine+per-kingdom 上限+国库扣费）；选型=本族机器 def 首个。
    /// 位置=主城旁固定偏移近点（厂/城产出惯例位；非选址打分器链——机器非建筑无 PlacementValidator 面）。
    /// 不进配兵双环（机器不走训练链，B6 候选域已排除机器=语义正交）。
    /// </summary>
    private void ExecuteProduceMachine(KingdomState kingdom, KingdomBrainConfig cfg)
    {
        var sps = SiegeProductionSystem.Instance;
        if (sps == null) { Bump(kingdomId, train: false, ok: false); ReportActionFail(kingdom, ActionBackoff.FailKind.Env); return; }   // HH.228／D680 追记：㉕门面单例未就绪（`SiegeProductionSystem.Instance==null`）⇒ Env
        if (FindKingdomBuilding(kingdomId, BuildingIds.SiegeWorkshop) == null)
        {
            Bump(kingdomId, train: false, ok: false);
            ReportActionFail(kingdom);   // HH.224/D670：㉕真失败（无厂前置）
            return;   // 厂前置守卫：无投掷机厂不可造（评分侧 ㉔ 建厂缺口导向先建——两行动咬合）
        }
        // 本族机器选型（确定性：Occupation int 升序首个本族可造机器——IsRaceAllowedMachine 同源校验）
        Occupation[] machines = { Occupation.Ballista, Occupation.SiegeMachine, Occupation.Mortar, Occupation.VineCatapult, Occupation.Ram };
        int myRace = KingdomRace.GetKingdomRace(kingdomId);
        Occupation pick = machines[0];
        bool found = false;
        for (int i = 0; i < machines.Length; i++)
        {
            if (SiegeProductionSystem.IsMachineAllowed(myRace, machines[i])) { pick = machines[i]; found = true; break; }
        }
        if (!found) { Bump(kingdomId, train: false, ok: false); ReportActionFail(kingdom, ActionBackoff.FailKind.Env); return; }   // HH.228／D680 追记：㉕本族无可造机器（`IsMachineAllowed` 全否＝族属/配置给定，AI 不可自消）⇒ Env
        // D594 整改令·执行侧防御预检（可选条款一并落）：prefab 缺失扣费前拦截——
        // 双保险第二层（第一层=评分侧 Feasible 不评）；口径同源 MachinePanel.IsPrefabMissing（HH.111 P5）
        if (MachinePanel.IsPrefabMissing(pick, out string whyMissing))
        {
            Bump(kingdomId, train: false, ok: false);
            ReportActionFail(kingdom, ActionBackoff.FailKind.Env);   // HH.228／D680 追记：㉕prefab **资产缺失**（`MachinePanel.IsPrefabMissing`）⇒ Env（HH.224/D670 ㉕真失败·prefab 缺失）
            Debug.Log($"[KingdomBrain] k{kingdomId} ㉕造机器拦截：{pick} prefab 缺失（{whyMissing}）——扣费前防御预检（D594）");
            return;
        }

        var anchor = FindCastleCell(kingdomId);
        if (!anchor.HasValue) { Bump(kingdomId, train: false, ok: false); ReportActionFail(kingdom, ActionBackoff.FailKind.Env); return; }   // HH.228／D680 追记：㉕主城锚点缺失（`FindCastleCell` 无值＝世界/门面未提供）⇒ Env
        var spawnPos = new Vector2(anchor.Value.x + 1.5f, anchor.Value.y + 1.5f);   // 主城旁近点（厂/城产出惯例位）
        bool ok = sps.ProduceMachine(pick, spawnPos, kingdomId);
        Bump(kingdomId, train: false, ok: ok);
        if (ok) ReportActionOk(kingdom); else ReportActionFail(kingdom);   // HH.224/D670：㉕成败上报
        if (ok) Debug.Log($"[KingdomBrain] k{kingdomId} ㉕造机器落地：{pick} @ {spawnPos}");
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

    /// <summary>⑧科技升级真实通道（2_17 步骤11 批3b，HH.30 策划 Q1→A / Q2→A′ 裁）：花金提升王国目标模块解锁态。</summary>
    /// 闭环（HH.30 Q2-A′ 语义链）：moduleLevels 全0 起步 → TechGap 评分(金) → ExecuteTech 花金升降目标模块+1(≤城堡1上限)
    ///  → 升满 TechGap=0 → 停。castleLevel 固定 1（2_16 立国 AI 城堡即 Active，等价玩家主城修复完成 Lv1）。
    /// AI 城堡升级动作记债挂账（超批3 范围，随军事期内容一并议，见 HH.30）。
    private void ExecuteTech(KingdomState kingdom, KingdomBrainConfig cfg)
    {
        int cost = Mathf.Max(1, cfg.techUpgradeCostGold);
        if (kingdom.GetResourceValue(ResourceType.Gold) < cost)
        {
            Bump(kingdomId, train: false, ok: false);
            ReportActionFail(kingdom);   // HH.224/D670：⑧真失败（金不足）
            return;
        }

        // 初始化 AI 王国产科技解锁态（castleLevel 固定 1 = 2_16 立国 AI 城堡 Active 等价玩家修复完成；moduleLevels 全0 起步）
        if (kingdom.moduleLevels == null) kingdom.moduleLevels = new int[6];
        if (kingdom.castleLevel <= 0) kingdom.castleLevel = 1;

        // 目标模块（SO 驱动，默认 Civil；城堡1 上限 = CastleUnlockTable 该城堡级最大可达模块级）
        ModuleType target = cfg.techTargetModule;
        int idx = (int)target;
        int cap = GetCastleModuleCap(target, kingdom.castleLevel);
        if (kingdom.moduleLevels[idx] >= cap)
        {
            // 已升满城堡1上限 → 不可再升（TechGap 应为 0 已停；防御性 Bump ok:false 防刷分）
            Bump(kingdomId, train: false, ok: false);
            Debug.Log($"[KingdomBrain] k{kingdomId} ⑧科技已达城堡{kingdom.castleLevel}上限 {target}={cap}，停（防刷分）");
            return;
        }

        kingdom.Spend(new ResourcePack { gold = cost });
        kingdom.moduleLevels[idx]++;
        Bump(kingdomId, train: false, ok: true);
        ReportActionOk(kingdom);   // HH.224/D670：⑧成功落地 ⇒ 退避清零
        Debug.Log($"[KingdomBrain] k{kingdomId} ⑧科技升级落地：{target} → Lv{kingdom.moduleLevels[idx]}（金-{cost}；城堡{kingdom.castleLevel}上限{cap}）");
    }

    /// <summary>城堡该级可达到的目标模块上限（CastleUnlockTable.GetModuleLevel，静态表共享）。</summary>
    private static int GetCastleModuleCap(ModuleType module, int castleLevel)
    {
        var table = Resources.Load<CastleUnlockTable>("Config/CastleUnlockTable");
        return table != null ? table.GetModuleLevel(module, castleLevel) : 0;
    }

    /// <summary>
    /// ⑩推边界焦点下发（2_17 步骤12 批B，HH.32 裁3 吞并/扩张=A 日 tick 一致性）。
    /// 实际扩张引擎=DayCycleSettlement 步骤3 TerritorySystem.ExpandTick（D326 升序/冷却5日/日推1~2邻接无主/D327 硬容量门）。
    /// 本方法=焦点一致性占位：确认 AI 已选⑩ 即可，无需实体指令——扩张随日 tick 自动落地，避免双写。
    /// </summary>
    private void ExecuteExpand(KingdomState kingdom, KingdomBrainConfig cfg)
    {
        int nonInitial = TerritorySystem.Instance != null
            ? TerritorySystem.Instance.NonInitialTerritoryCount(kingdomId) : -1;
        Debug.Log($"[KingdomBrain] k{kingdomId} 焦点⑩推边界 下发（实际扩张=DayCycle 步骤3 ExpandTick 日 tick；非初始占区={nonInitial}）");
    }

    /// <summary>找一个可招募流浪汉（活体、Vagrant、未被招募、未入籍 kingdomId&lt;0、同族 D469）。固定遍历序=确定性。</summary>
    private UnitController FindRecruitableVagrant()
    {
        if (UnitRegistry.Instance == null || UnitRegistry.Instance.GetAllUnits() == null) return null;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive) continue;
            if (u.kingdomId >= 0) continue;                       // 已入籍者不重复招
            if (u.EffectiveOccupation != Occupation.Vagrant) continue;
            if (u.IsVagrantRecruited) continue;
            // D469 招募限同族（HH.51 批B）：异族流民=永久野人不可回收。
            // 濒死 AI 自救窗口不破（D322）：流民池主体=本国自身流失人口（同族），走查确认当前世界全 Human。
            if (u.raceId != KingdomRace.GetKingdomRace(kingdomId)) continue;
            return u;
        }
        return null;
    }

    /// <summary>找本国活居民（B6 兵源池：Resident=训练链 fromOccupation 源；确定性 npcId 最小序）。</summary>
    private UnitController FindOwnResident()
    {
        if (UnitRegistry.Instance == null || UnitRegistry.Instance.GetAllUnits() == null) return null;
        UnitController best = null;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive) continue;
            if (u.kingdomId != kingdomId) continue;
            if (u.EffectiveOccupation != Occupation.Resident) continue;
            if (best == null || u.npcId < best.npcId) best = u;
        }
        return best;
    }

    /// <summary>
    /// B5 局内环自整定（日 tick 结算节拍）：窗口死亡统计 → BattleLearnedWeights 更新+归因日志。
    /// η=0.05 占位（七日滚动窗口下同一事件衰减式影响，总量可控——口径如实列报 HH.138）。
    /// </summary>
    private void UpdateLearnedWeights(SituationSnapshot snap)
    {
        if (snap?.UnitPerformance == null || snap.UnitPerformance.Count == 0) return;
        var stats = new UnitPerfInput[snap.UnitPerformance.Count];
        for (int i = 0; i < stats.Length; i++)
            stats[i] = new UnitPerfInput { OccupationId = snap.UnitPerformance[i].OccupationId, Deaths = snap.UnitPerformance[i].Deaths };
        var deltas = BattleLearnedWeights.UpdateFromLosses(kingdomId, stats, 0.05f);
        if (deltas.Count == 0) return;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < deltas.Count; i++)
            sb.Append($" {(Occupation)deltas[i].OccupationId}:{deltas[i].OldW:F2}→{deltas[i].NewW:F2}(perf{deltas[i].PerfScore:F2})");
        Debug.Log($"[KingdomBrain] k{kingdomId} 局内环权重更新（窗口死亡归因）：{sb}");
    }

    /// <summary>找本国任一 Active 建筑（B2/B6 建筑前置联动：训练建筑在场判定；确定性 id 序）。</summary>
    private static Building FindKingdomBuilding(int kingdomId, string buildingId)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null || string.IsNullOrEmpty(buildingId)) return null;
        Building best = null;
        for (int i = 0; i < reg.All.Count; i++)
        {
            var b = reg.All[i];
            if (b == null || b.def == null || !b.IsActive) continue;
            if (b.kingdomId != kingdomId || b.def.id != buildingId) continue;
            if (best == null || string.CompareOrdinal(b.def.id, best.def.id) < 0) best = b;
        }
        return best;
    }

    // ===== 建造类焦点真实通道 =====

    /// <summary>
    /// 建造焦点执行（SO buildingId 通用通道）。2_23 资源 P0 批C（R-C1/R-C2）：
    /// overrideBuildingId 非空时**覆盖 def0.buildingId**（⑤三通道分诊/BuildCapacity 种类化
    /// 传实参；null=沿用 SO）——选址/成本/可行性全走被覆盖目标 def（ExecuteBuildFocus 后续同链）。
    /// </summary>
    private void ExecuteBuildFocus(KingdomState kingdom, KingdomBrainConfig cfg, string overrideBuildingId = null)
    {
        var def0 = UtilityActionConfig.LoadConfig().Find((UtilityAction)kingdom.focus);
        if (def0 == null || string.IsNullOrEmpty(def0.Value.buildingId))
        {
            Bump(kingdomId, train: false, ok: false);
            ReportActionFail(kingdom, ActionBackoff.FailKind.Env);   // HH.228 补遗A／D682：建造链①**配置给定物缺失**（`UtilityActionConfig` 无该 action 条目／`buildingId` 未填）⇒ Env（HH.224/D670 建造链失败点①）
            return;
        }
        string bid = overrideBuildingId ?? def0.Value.buildingId;   // R-C1/R-C2：动态目标 def
        var bdef = BuildingFactory.FindDefById(bid);
        if (bdef == null)
        {
            Bump(kingdomId, train: false, ok: false);
            ReportActionFail(kingdom, ActionBackoff.FailKind.Env);   // HH.228 补遗A／D682：建造链②**资产缺失**（全仓无该 `BuildingDef`）⇒ Env（HH.224/D670 建造链失败点②）
            Debug.LogWarning($"[KingdomBrain] k{kingdomId} 行动 {(UtilityAction)kingdom.focus} 的 buildingId={bid} 未找到 def");
            return;
        }

        // D3（2_22 P0 批D，D525 §3.7）：选址半边升级——首格即用 → 打分器最优格（全量候选+F1/F2/F3 特征）
        var spot = PlacementScorer.TryPick(kingdom.id, bdef, cfg.aiBuildRadius,
            BuildingPlacementConfig.Load(), SituationHub.TryGet(kingdomId, out var sitB) ? sitB : null, out var pickB);
        if (!spot)
        {
            // HH.88 件3：选址无落位静默点观测口（HH.87 列报 10）——失败原因+kingdomId
            Debug.LogWarning($"[KingdomBrain] k{kingdomId} 建造焦点选址失败：{bdef.id} 半径 {cfg.aiBuildRadius} 内无合法落位（明日再试）");
            Bump(kingdomId, train: false, ok: false);
            ReportActionFail(kingdom);   // HH.224/D670：建造链失败点③（选址无落位＝DZ-107 主病灶）｜HH.228 实读后判：**维持 `Self`**（含 AI 自身可调 `aiBuildRadius` 影响，非"无论怎么决策都无法自消"）
            return;   // 半径内无合法落位：明日再试
        }
        var spotCell = pickB.Sub;

        var bc = BuildController.Instance;
        if (bc == null)
        {
            // HH.224/D670 补观测（D675 抓漏）：原为**零日志静默失败** ⇒ 按 HH.88 件3 静默点观测口精神补齐
            Bump(kingdomId, train: false, ok: false);
            ReportActionFail(kingdom, ActionBackoff.FailKind.Env);   // HH.228／D680 追记：建造链④**门面未就绪**（`BuildController.Instance==null`）⇒ Env（HH.224/D670 建造链失败点④）
            Debug.LogWarning($"[KingdomBrain] k{kingdomId} 建造焦点投放中止：BuildController 实例缺失（门面未就绪，明日再试）");
            return;
        }
        bool ok = bc.TryBuild(bdef, spotCell, GateOrientation.Horizontal, kingdomId);
        Bump(kingdomId, train: false, ok: ok);
        if (ok)
        {
            ReportActionOk(kingdom);   // HH.224/D670：建造落地 ⇒ 退避清零（自愈）
            Debug.Log($"[KingdomBrain] k{kingdomId} 建造焦点落地：{def0.Value.buildingId} @ ({spotCell.x},{spotCell.y})" +
                      $"（打分 {pickB.Score:F3}=F1 {pickB.F1:F2}+F2 {pickB.F2:F2}+F3 {pickB.F3:F2}，候选 {pickB.Candidates}）");
        }
        else
        {
            ReportActionFail(kingdom);   // HH.224/D670：建造链失败点⑤（TryBuild=false 门面校验/扣费未过）
            // HH.88 件3：TryBuild=false 静默点观测口（门面校验/扣费未过；细分原因看 [BuildController] 拒绝日志）
            Debug.LogWarning($"[KingdomBrain] k{kingdomId} 建造焦点 TryBuild=false：{def0.Value.buildingId} @ ({spotCell.x},{spotCell.y})（门面校验/扣费未过，细分原因见 [BuildController] 拒绝日志）");
        }
    }

    // ===== ⑤三通道分诊 + ③种类解析（2_23 资源 P0 批C / R-C1·R-C2，D639 全列报最终口径）=====

    /// <summary>
    /// ⑤屯粮根因三通道分诊（R-C1）：触发（粮=底线日告警既有；金/石/木=净流量为负+水位<基线 单日判定）
    /// → 断供严重度 粮→金→石→木 取首发（铁不参与 D639 列报3）→ A→B→C 固定序、命中即止、单通道不叠加。
    /// 通道A：无对应产能建筑（Production[r].Count==0 且反查表有 def）→ 建对应产能（D639 列报4）；
    /// 通道B：实际日产出/人口低（Flow.In(r)/pop &lt; 阈值，D639 列报5 改裁=非 RateSum 潜在产出率）
    ///   → 派工偏向（批B）即时生效，⑤不建（Bump ok 不空转）；
    /// 通道C：仓储占用溢出 → 粮建 Granary／非粮建 Warehouse（D639 列报6）；
    /// 全不命中 → 正常态不空转（Bump ok）。所有信号来自批A EconomyBlock（无跨日状态=无持久态红线）。
    /// </summary>
    private void ExecuteGrainTriage(KingdomState kingdom, KingdomBrainConfig cfg)
    {
        if (!SituationHub.TryGet(kingdomId, out var sit) || sit == null || sit.Economy == null)
        {
            // 诊断块未就绪（读档后首日）：当日跳过，明日重建后重试（非失败，避免异常刷屏）
            Bump(kingdomId, train: false, ok: true);
            return;
        }
        var eco = sit.Economy;
        var dcfg = KingdomDiagnosisConfig.Load();

        int r = ResolveTriageResource(eco, dcfg);
        if (r < 0) { Bump(kingdomId, train: false, ok: true); return; }   // 无触发=正常态
        var er = (EcoResource)r;

        // A→B→C 固定序决策（纯函数 DecideTriage；R-C4 探针同输入同输出断言=确定性）
        switch (DecideTriage(eco, dcfg, er))
        {
            case TriageDecision.BuildCapacity:   // 通道A 建对应产能（D639 列报4）
                ExecuteBuildFocus(kingdom, cfg, dcfg.FindTriageDef(er));
                break;
            case TriageDecision.BuildGranary:    // 通道C 粮→Granary（D639 列报6）
                ExecuteBuildFocus(kingdom, cfg, "Granary");
                break;
            case TriageDecision.BuildWarehouse:  // 通道C 非粮→Warehouse（D639 列报6）
                ExecuteBuildFocus(kingdom, cfg, "Warehouse");
                break;
            case TriageDecision.NoOp:            // 通道B（偏向批B 生效）/全不命中=正常态，⑤ 不建不空转
            default:
                Bump(kingdomId, train: false, ok: true);
                break;
        }
    }

    // ===== ㉗ 采集世界资源点（HH.221/D685 A②；A① 通道的决策出口）=====

    /// <summary>
    /// 采集焦点执行（决策核**只出粗意图**；选点/可达/派工/距离归 A① 层）。
    /// 触发资源＝2_23 三通道分诊首发断供资源（**单源**，避 DZ-118/DZ-128 双源）；
    /// 落**通道B**（有产能但日产出低）才属本行动域（通道A/C 已由 ③/⑤ 承接；与评分 `NeedScore`/`Feasible` 同判据，L-31 同源）。
    /// 执行＝调 `WorldGatherRegistry.Advertise`（**门面**）下发粗意图→本层选点并注册 `WorldGatherSource`。
    /// 失败分型（HH.228/D680 口径）：
    ///   · 快照缺席／无断供触发／非石木／非通道B ⇒ **正常态**（Bump ok，非失败——防灌退避）；
    ///   · 领土内**无可达目标**（`candidates==0`）⇒ **Env**（世界未提供该资源点，AI 无法以自身行动消除）；
    ///   · 在册满额／已全部在册 ⇒ **成功**（意图已落地，非失败）。
    /// </summary>
    private void ExecuteWorldGatherFocus(KingdomState kingdom, KingdomBrainConfig cfg)
    {
        if (!SituationHub.TryGet(kingdomId, out var sit) || sit == null || sit.Economy == null)
        {
            Bump(kingdomId, train: false, ok: true);   // 诊断块未就绪（读档后首日）：当日跳过，明日重建后重试
            return;
        }
        var dcfg = KingdomDiagnosisConfig.Load();
        int r = ResolveTriageResource(sit.Economy, dcfg);
        if (r < 0 || !WorldGatherRegistry.TryMapWorldResource((EcoResource)r, out var rt))
        {
            Bump(kingdomId, train: false, ok: true);   // 无断供触发／该资源无世界采集通道 ⇒ 正常态不空转
            return;
        }
        var decW = DecideTriage(sit.Economy, dcfg, (EcoResource)r);
        if (decW != TriageDecision.NoOp)
        {
            // HH.251/D699（Gate=G1-1）A+ 施工批·㉗↔③ 双轨解耦：通道C 归 ⑤、通道A 且 ③建产能可行 ⇒ 照旧让位
            //（③ 更优·避抢焦点）；**新增**＝通道A 且 ③ `Feasible==false`（无产能且建产能不可行）⇒ ㉗ 接管
            //（降级通道·即时采集），不再空转让位。判据与评分侧 `NeedScore.GatherShortageGap` 入口放宽**同源**
            //（同一 `UtilityScorer.Feasible(③)`，L-31 禁另造第二套可行性判据）。
            bool takeover = false;
            if (decW == TriageDecision.BuildCapacity)
            {
                var ucfgW = UtilityActionConfig.LoadConfig();
                var capDefW = ucfgW != null ? ucfgW.Find(UtilityAction.BuildCapacity) : null;
                takeover = capDefW.HasValue && !UtilityScorer.Feasible(kingdom, capDefW.Value);
            }
            if (!takeover)
            {
                Bump(kingdomId, train: false, ok: true);   // 通道C／通道A 且 ③可行 ⇒ 本行动正常让位（非失败）
                return;
            }
            // 只读观测打点（HH.251 §三 授权）：区分「接管（通道A·③不可行）」与「通道B 常态派发」两条入径
            Debug.Log($"[KingdomBrain] k{kingdomId} ㉗接管：通道A且③不可行（{rt}）——降级通道派发");
        }

        var reg = WorldGatherRegistry.Instance;
        if (reg == null)
        {
            Bump(kingdomId, train: false, ok: false);
            ReportActionFail(kingdom, ActionBackoff.FailKind.Env);   // HH.228/D680：单例未就绪 ⇒ Env
            return;
        }
        var (registered, candidates) = reg.Advertise(kingdomId, rt);
        if (candidates <= 0)
        {
            Bump(kingdomId, train: false, ok: false);
            ReportActionFail(kingdom, ActionBackoff.FailKind.Env);   // 领土内无可达目标 ⇒ Env（世界给定物）
            Debug.LogWarning($"[KingdomBrain] k{kingdomId} ㉗采集：本国领土内无可采 {rt} 资源点（明日再试／待推进拓土）");
            return;
        }
        Bump(kingdomId, train: false, ok: true);
        ReportActionOk(kingdom);   // 意图已落地（新立案或已在册），退避清零
        Debug.Log($"[KingdomBrain] k{kingdomId} ㉗采集下发：{rt} 新立案 {registered} 个（候选 {candidates}，在册 {reg.CountOf(kingdomId)}）"
                  + $"——选点/派工归 WorldGatherRegistry+TaskScheduler");
    }

    /// <summary>⑤三通道 A→B→C 固定序决策（D639 全列报；纯函数=R-C4 确定性断言锚）。
    /// A 通道：无对应产能建筑且反查表有 def → 建对应产能（D639 列报4）；
    /// B 通道：实际日产出/人口低（Flow.In(r)/pop&lt;阈值，D639 列报5）→ 偏向批B 生效，no-op；
    /// C 通道：仓储占用溢出 → 粮 Granary／非粮 Warehouse（D639 列报6）；
    /// 全不命中 → no-op（正常态不空转）。命中即止、单通道响应不叠加（D639 列报7）。</summary>
    internal static TriageDecision DecideTriage(EconomyBlock eco, KingdomDiagnosisConfig dcfg, EcoResource er)
    {
        // A：无对应产能建筑且反查表有 def → 建对应产能
        if (CountProductionOf(eco, er) == 0)
        {
            string capDef = dcfg != null ? dcfg.FindTriageDef(er) : null;
            if (!string.IsNullOrEmpty(capDef)) return TriageDecision.BuildCapacity;
        }
        // B：实际日产出/人口低 → 派工偏向（批B）即时生效，⑤ 不建（Bump ok）
        int pop = eco.Population;
        float dailyInPerPop = pop > 0 ? eco.Flow.In(er) / (float)pop : 0f;
        if (dailyInPerPop < (dcfg != null ? dcfg.channelBOutputPerPop : 1f)) return TriageDecision.NoOp;
        // C：仓储占用溢出 → 粮 Granary／非粮 Warehouse
        if (eco.StorageOccupancy >= (dcfg != null ? dcfg.storageOccupancyThreshold : 0.9f))
            return er == EcoResource.Food ? TriageDecision.BuildGranary : TriageDecision.BuildWarehouse;
        return TriageDecision.NoOp;
    }

    /// <summary>触发判定+断供严重度取首发（粮→金→石→木；铁跳过=D639 列报3）。
    /// 粮：GrainReserveDays&lt;grainReserveDaysFloor（既有底线日，底线机制不动，D639 列报2）；
    /// 金/石/木：Flow.Net(r)&lt;0 且 Stock(r)&lt;reserveTargetDaysOther×max(1,Out(r))（单日判定 D639 列报1）。
    /// 返回 EcoResource int；无触发 → -1。纯函数（读快照无随机）。
    /// HH.221/D685 A②：可见性 private → **internal**（`UtilityScorer.NeedKind.GatherShortageGap` 单源复用之需；
    /// 行为/语义逐字不变，仅放宽可见性=禁另造第二套触发判据 DZ-118/DZ-128）。</summary>
    internal static int ResolveTriageResource(EconomyBlock eco, KingdomDiagnosisConfig dcfg)
    {
        int floor = dcfg != null ? dcfg.grainReserveDaysFloor : 2;
        int reserveDays = dcfg != null ? dcfg.reserveTargetDaysOther : 3;
        var order = dcfg != null ? dcfg.shortageSeverityOrder : null;
        if (order == null || order.Length == 0) return -1;
        for (int i = 0; i < order.Length; i++)
        {
            int r = order[i];
            if (r == (int)EcoResource.Metal) continue;   // 铁不参与触发（D639 列报3：心跳四成员）
            if (r == (int)EcoResource.Food)
            {
                if (eco.GrainReserveDays < floor) return r;
            }
            else
            {
                float net = eco.Flow.Net((EcoResource)r);
                if (net < 0f)
                {
                    int stock = eco.StockOf((EcoResource)r);
                    int outAmt = eco.Flow.Out((EcoResource)r);
                    if (stock < reserveDays * Mathf.Max(1, outAmt)) return r;
                }
            }
        }
        return -1;
    }

    /// <summary>按资源取产能建筑数（EconomyBlock.Production 聚合；无 → 0）。
    /// 口径沿用批A MapProduceToEco（含带 producer 的仓储类=批A 既有验收口径，本批不改——如实注记）。</summary>
    private static int CountProductionOf(EconomyBlock eco, EcoResource r)
    {
        if (eco.Production == null) return 0;
        var list = eco.Production;
        for (int i = 0; i < list.Count; i++)
            if (list[i].Resource == r) return list[i].Count;
        return 0;
    }

    /// <summary>R-C2 ③种类解析（D639 列报8）：按产能缺口（Count/pop 升序）解最缺资源 → 反查表 def（SO）。
    /// 无快照/表内缺省（Wood/Gold/Metal 无实体产能）→ null=沿用 SO buildingId（③ 恒 quarry 退化态）。
    /// 确定性：Production 已是 EcoResource 升序聚合；比例计算纯函数。</summary>
    private string ResolveTriageDefId(int forKingdom)
    {
        if (!SituationHub.TryGet(forKingdom, out var sit) || sit == null || sit.Economy == null) return null;
        var eco = sit.Economy;
        var dcfg = KingdomDiagnosisConfig.Load();
        if (dcfg == null || dcfg.triageCapacityDefs == null) return null;

        int pop = Mathf.Max(1, eco.Population);
        int bestR = -1; float bestRatio = float.MaxValue;
        var list = eco.Production;
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                float ratio = list[i].Count / (float)pop;
                if (ratio < bestRatio) { bestRatio = ratio; bestR = (int)list[i].Resource; }
            }
        }
        if (bestR >= 0)
        {
            string def = dcfg.FindTriageDef((EcoResource)bestR);
            if (!string.IsNullOrEmpty(def)) return def;
        }
        return null;
    }

    /// <summary>
    /// AI 选址器兼容壳（2_22 P0 批D / D3，D525）：产品路径已升级 PlacementScorer.TryPick
    /// （全量候选+特征打分+argmax 固定平局序，ExecuteBuildFocus 直调）——本方法保留为单一实现
    /// 委托壳（批B HH.137 探针反射依赖+潜在脚本化调用），首格即用语义已废止。
    /// 合法性由 PlacementValidator 全量校验（与玩家同套规则，scorer 内全继承）。
    /// </summary>
    private static GridCoord? FindAIBuildSpot(int kingdomId, BuildingDef def, int maxRadius)
    {
        SituationHub.TryGet(kingdomId, out var sit);
        if (PlacementScorer.TryPick(kingdomId, def, maxRadius, BuildingPlacementConfig.Load(), sit, out var r))
            return r.Sub;
        return null;
    }

    /// <summary>找某国主城（castle 建筑坐标；固定遍历序）。无主城 → null。
    /// public static（批C C3 主威胁方向复用=敌城/己城锚点查询；原 private 收编）。</summary>
    public static GridCoord? FindCastleCell(int kingdomId)
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

/// <summary>⑤三通道分诊响应决策（R-C1，D639；R-C4 探针确定性断言对象）。</summary>
public enum TriageDecision : byte
{
    NoOp,            // 通道B（偏向批B 生效）/全不命中=正常态不空转（⑤ Bump ok）
    BuildCapacity,   // 通道A：建对应产能（反查表 SO def）
    BuildGranary,    // 通道C·粮：建 Granary
    BuildWarehouse   // 通道C·非粮：建 Warehouse
}