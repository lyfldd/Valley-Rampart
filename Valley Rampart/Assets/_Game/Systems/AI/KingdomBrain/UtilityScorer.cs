using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  效用评分器（2_17 步骤9，D321~D323/D345/D346，纯 C# 无 Unity 引用）
//  对王国可见候选行动按四因子评分（D323）选出胜者 → 国策焦点。
//  纯函数层（无副作用）：输入 王国快照 + 效用配置 SO + 剧本阶段 → 输出 顶行动 id。
//  可单测；同 seed 确定；评分全程不触碰实体（可行性=D346 二值门控，全由王国台账读）。
//
//  评分 = 需求强度 × 性格权重 × 可行性 × 阶段权重
//   - 需求强度：NeedScore 缺口函数单调分 0..1
//   - 性格权重：personality[axis]×action.axisWeight（五轴独立线性乘入，D311 契约）
//   - 可行性：Feasible 二值（D346），不可行→0 直接出局
//   - 阶段权重：action.stageWeight[stage]（D321 阶段只做可见性门控+该权重，不参与连续性）
// ============================================================================

/// <summary>效用候选行动 id（D321 扁平 15 项；P0 实装 ①~⑥+⑬⑭，D345；0=None）。</summary>
public enum UtilityAction : byte
{
    None = 0,
    BuildHouse = 1,        // ①建住宅
    BuildWarehouse = 2,    // ②建仓库
    BuildCapacity = 3,     // ③建产能
    BoostHarvest = 4,      // ④强化采集
    Grain = 5,             // ⑤屯粮（常设底线焦点）
    RecruitWorker = 6,     // ⑥招工人（p 人口增长唯一途径，防卡死存活期）
    RecruitWarrior = 7,    // ⑦招战士（P1）
    Tech = 8,              // ⑧科技升级（P1）
    BuildWall = 9,         // ⑨修工事城墙（P1）
    Expand = 10,           // ⑩推边界（P1 步骤12）
    Expedition = 11,       // ⑪组建出征军（P1，依赖 2_18）
    Reinforce = 12,        // ⑫边境增援（P1）
    Rebuild = 13,          // ⑬重建（D318 全阶段）
    Defense = 14,          // ⑭防御姿态（D318 全阶段 / 常设底线焦点）
    Diplomacy = 15,        // ⑮外交姿态（P1，2_18 接管）
    // ===== HH.86 件3b/3c 供给与铁链补全（尾插保持 int 稳定；参数占位待策划调，报告列报）=====
    BuildWell = 16,        // ⑯建水井：井损重建通道（DZ-043；WellGap=本国 Active 井数 < 目标）
    BuildBlacksmith = 17,  // ⑰建铁匠铺：AI 铁链打通（石→Metal；MetalGap=国库 Metal 低于底线）
    BuildWarAcademy = 18,  // ⑱建战争学院（人类专属；raceId=0）
    BuildWarCamp = 19,     // ⑲建兽人战营（兽人专属；raceId=3）
    BuildLeyForge = 20,    // ⑳建地脉熔炉（矮人专属；raceId=2）
    BuildArcheryRange = 21, // ㉑建精灵射箭场（精灵专属；raceId=1）
    // ===== 2_22 P0 批B 建军链（尾插保持 int 稳定；圈号系⑯⑰已被 HH.86 占用，下列圈号=2_22 文档系编号）=====
    TrainGeneral = 22,      // 2_22⑯ 训练将军（B2）：军事期起（minStage=3）；执行=兵营 Barracks 队列训练 General（与玩家同链 generalLimit）
    BuildBarracks = 23,     // 2_22⑰a 建兵营（B3）：共通军事建筑（General/Warrior/Cavalry 训练入口）
    BuildTrainingCamp = 24, // 2_22⑰b 建训练营（B3）：共通军事建筑（Archer/Mage/Healer+本族专属兵训练入口）
    BuildSiegeWorkshop = 25, // 2_22㉔ 建投掷机厂（B8）：共通中性建筑（SiegeWorkshopBuilding 弹药厂）
    ProduceMachine = 26     // 2_22㉕ 造战争机器（B8）：战争态势驱动（MachineDemand）；不进配兵双环（语义正交）
}

/// <summary>需求强度缺口函数类型（D323 单调缺口；参数 needA/needB 语义见各 case）。</summary>
public enum NeedKind : byte
{
    HouseGap,        // 住房缺口：needA=住房目标人口（P0 用人口代理无房数）
    WarehouseGap,    // 仓储缺口：needA=仓储容量基线（储量越高越需加仓）
    CapacityGap,     // 产能缺口：needA=产能目标（活跃建筑数不足则补）
    HarvestGap,      // 采集缺口：needB=粮裕日阈值（粮产不足则强化采集）
    GrainGap,        // 屯粮缺口：needA=粮储底线日（低于则屯）
    RecruitWorkerGap,// 工人缺口：needA=工人目标（少于则招募）
    RebuildGap,      // 重建缺口：needA=损毁比阈值（P0 无损毁统计，占位低分）
    DefenseNeed,     // 防御缺口：P0 占位 0（被攻击由常设底线 cover；邻国威胁归 P1）
    RecruitWarriorGap, // 战士缺口（⑦）：目标=D348 兵力目标，己方战士未达 → 缺口（needA=缺口归一基线）
    TechGap,         // 科技缺口（⑧）：needA=科技目标基线（金存量越高越想升），解锁态 per-kingdom 步骤11
    WallGap,         // 工事城墙缺口（⑨）：needA=城墙目标座数，现存城墙不足则补
    ExpeditionNeed,  // 出征军（⑪ 占位可执行）：军事期兵力充裕即想出征（needA=兵力基线）
    ReinforceNeed,   // 边境增援（⑫ 占位）：军事期边境需增强（needA=增援目标；L3 意图接口占位）
    DiplomacyNeed,   // 外交姿态（⑮ 占位可执行）：外交轴意愿（needA=外交基线）
    TerritoryGap,    // 领土缺口（⑩ 推边界）：needA=目标非初始占区数；非初始占区 < 目标越缺越想扩（HH.32 裁2 A′，欲望与容量分离）
    // ===== HH.86 件3b/3c（尾插）=====
    WellGap,         // 水井缺口（⑯ 建水井）：needA=目标井数（默认 1）；本国 Active 井 < 目标 → 缺口（井损重建通道 DZ-043）
    MetalGap,        // 金属缺口（⑰ 建铁匠铺）：needA=国库 Metal 底线（占位 30）；低于则想建铁匠铺打通石→Metal
    ExclusiveGap,    // 专属建筑缺口（⑱~㉑）：本国无族专属建筑 → 占位底分 0.5（军事期军备面；族门禁在 Feasible）
    // ===== 2_22 P0 批A / A4 军事维度缺口（D589 内源节拍：纯缺口驱动评分，不乘威胁门控——
    //      威胁=0 时缺口依然评分，批B 行动 ⑯训练将军/⑰建军事建筑/⑦扩多兵种 落地后消费；
    //      行动条目批A 不落=评分循环无对应 def，零行为漂移）=====
    GeneralGap,      // 缺将军：本国将军数 < generalLimit → 缺口（读快照 GeneralCount；将军补任链 §3.2）
    FormationGap,    // 缺编队：本国编队数 < needA 目标 → 缺口（读快照 FormationCount；成军链批B）
    UnitTypeGap,     // 缺兵种：军事训练域可训兵种多样性缺口（读快照 OwnedCombatOccupations；可训域=D570 口径 共通+本族）
    // ===== 2_22 P0 批B / B8 战争机器（D558→D570）=====
    MachineDemand    // 造机器需求（㉕）：战争态势驱动=军事期(stage==3)+邻接威胁非空（快照 Threats）→ 需求分；
                     // 守城需求=警戒/动员档（批C 接入位，P0 占位=军事期+威胁即驱动）；族门禁/上限在执行链 D558
}

/// <summary>效用评分器（纯函数层，2_17 步骤9）。单入口 ScoreTop。</summary>
public static class UtilityScorer
{
    /// <summary>P0 每人口每日粮耗近似（与 KingdomBrainConfig.grainConsumptionPerPop 默认对齐）。</summary>
    private const int PerPopGrain = 1;

    /// <summary>
    /// 评分淘汰构成普查（HH.115 件E#6 存在性判定）：把「无可执行候选」分型为
    /// **空候选集**（defTotal=0）vs **候选全不可行**（infeasible>0 且 top=None）vs **无需求/轴权零**——
    /// 六考长局评分死锁归因面（14 锁死型诊断的候选侧分型；行为零变化，只多一层可观测性）。
    /// </summary>
    public struct ScoreCensus
    {
        public int defTotal;        // 候选集总数（cfg.actions 长度，扣 None 占位）
        public int stageFiltered;   // 阶段门控淘汰数
        public int noNeed;          // 无需求淘汰数
        public int infeasible;      // Feasible 二值门控淘汰数（D346）
        public int axisFiltered;    // 轴权/阶段权重为零淘汰数
        public UtilityAction top;   // 最优行动（None=无可执行）
    }

    /// <summary>对王国可见候选打分，返回最优行动 id（无可执行 → None）。诊断需求用带普查重载。</summary>
    public static UtilityAction ScoreTop(KingdomState k, UtilityActionConfig cfg, ScriptStage stage)
        => ScoreTop(k, cfg, stage, out _);

    /// <summary>对王国可见候选打分，返回最优行动 id + 淘汰构成普查（HH.115 件E#6）。</summary>
    public static UtilityAction ScoreTop(KingdomState k, UtilityActionConfig cfg, ScriptStage stage, out ScoreCensus census)
    {
        census = default;
        if (k == null || cfg == null || cfg.actions == null) return UtilityAction.None;

        float best = -1f;
        UtilityAction top = UtilityAction.None;
        var defs = cfg.actions;
        for (int i = 0; i < defs.Length; i++)
        {
            var def = defs[i];
            if (def.id == UtilityAction.None) continue;
            census.defTotal++;
            if (stage < def.minStage) { census.stageFiltered++; continue; }   // D321 阶段可见性门控

            float need = NeedScore(k, def);
            if (need <= 0.0001f) { census.noNeed++; continue; }               // 无需求 → 不入选（免刷 0 分干扰）
            if (!Feasible(k, def)) { census.infeasible++; continue; }         // D346 二值门控：不可行 → 出局

            float axis = def.axisWeight;
            if (k.personality != null && def.axis >= 0 && def.axis < k.personality.Length)
                axis *= Mathf.Clamp01(k.personality[def.axis]); // 五轴独立线性乘入（D311）
            if (axis <= 0.0001f) { census.axisFiltered++; continue; }

            float stageW = (def.stageWeight != null && (int)stage < def.stageWeight.Length)
                ? Mathf.Max(0f, def.stageWeight[(int)stage]) : 1f;
            if (stageW <= 0f) { census.axisFiltered++; continue; }

            float score = need * axis * stageW;
            if (score > best) { best = score; top = def.id; }
        }
        census.top = top;
        return top;
    }

    /// <summary>需求强度缺口函数（0..1 单调；口径=王国昨日结存，与王国脑 tick 于入账前一致）。</summary>
    public static float NeedScore(KingdomState k, UtilityActionDef d)
    {
        int pop = k.workerCount + k.warriorCount;
        float food = k.GetResourceValue(ResourceType.Food);
        float grainDays = pop > 0 ? food / (float)Mathf.Max(1, pop * PerPopGrain) : 0f;

        switch (d.need)
        {
            case NeedKind.HouseGap:      // HH.86 件3a②：本国房容 vs 本国人口（旧=人口/needA 纯代理与房容无关→有房仍建=连轴帮凶之一）；房容≥人口 → 0 不缺
            {
                int houseCap = HappinessSystem.Instance != null
                    ? HappinessSystem.Instance.GetHouseCapacityByKingdom(k.id) : 0;
                if (houseCap >= pop) return 0f;
                return Mathf.Clamp01((pop - houseCap) / Mathf.Max(1f, pop));
            }
            case NeedKind.WarehouseGap:  // 储量越接近容量基线越需加仓
                float maxRes = Mathf.Max(Mathf.Max(food, k.resources.gold), Mathf.Max(k.resources.stone, Mathf.Max(k.resources.wood, 0f)));
                return Mathf.Clamp01(maxRes / Mathf.Max(1f, d.needA));
            case NeedKind.CapacityGap:   // 产能建筑不足
                int cap = CountActiveBuildings(k.id);
                return Mathf.Clamp01((d.needA - cap) / Mathf.Max(1f, d.needA));
            case NeedKind.HarvestGap:    // 粮裕日不足则强化采集（needB=粮裕日阈值）
                return Mathf.Clamp01(1f - grainDays / Mathf.Max(1f, d.needB));
            case NeedKind.GrainGap:      // 粮储日低于底线（needA=底线日）
                return d.needA > 0 ? Mathf.Clamp01((d.needA - grainDays) / d.needA) : 0f;
            case NeedKind.RecruitWorkerGap: // 工人 < 目标（needA=工人目标）
                return Mathf.Clamp01((d.needA - k.workerCount) / Mathf.Max(1f, d.needA));
            case NeedKind.RebuildGap:    // P0 无损毁统计 → 占位低分（损毁配额系统 P1 接入）
                return 0f;
            case NeedKind.DefenseNeed:   // P0 占位 0：被攻击由常设底线⑭强制；邻国威胁归 P1 兵力目标
                return 0f;
            case NeedKind.RecruitWarriorGap:   // ⑦ 战士缺口：k.warriorCount 未达 D348 兵力目标 → 缺口
            {
                int target = MilitaryTarget(k, KingdomBrain.LoadConfig());
                return WarriorGapScore(k.warriorCount, target);
            }
            case NeedKind.TechGap:       // ⑧ 科技：金存量越高越想升级；但目标模块已满城堡1上限 → 0（HH.30 Q3 防刷分，读解锁态）
            {
                var tcfg = KingdomBrain.LoadConfig();
                ModuleType target = tcfg != null ? tcfg.techTargetModule : ModuleType.Civil;
                // moduleLevels 未初始化（AI 未科技升级过）= 0；castleLevel 立国=1。满分前提=未达城堡1 上限。
                if (k.moduleLevels != null && k.castleLevel > 0
                    && k.moduleLevels[(int)target] >= GetTargetCap(target, k.castleLevel))
                    return 0f;   // 已升满 → 无需求（TechGap=0，行动停）
                return Mathf.Clamp01(k.GetResourceValue(ResourceType.Gold) / Mathf.Max(1f, d.needA));
            }
            case NeedKind.WallGap:       // ⑨ 城墙：现存城墙不足目标需补建（needA=目标座数）
            {
                int have = CountForts(k.id);
                if (have >= (int)Mathf.Max(0, d.needA)) return 0f;
                return Mathf.Clamp01((d.needA - have) / d.needA);
            }
            case NeedKind.ReinforceNeed: // ⑫ 边境增援（L3 意图接口占位）：军事期战士相对兵力目标不足即想增援
            {
                int target = MilitaryTarget(k, KingdomBrain.LoadConfig());
                if (k.warriorCount >= target) return 0f;
                return Mathf.Clamp01((target - k.warriorCount) / Mathf.Max(1f, target));
            }
            case NeedKind.DiplomacyNeed: // ⑮ 外交（占位可执行）：外交轴权重越高、姿态越均衡越想维持；P1 视角若要持续任意动作给恒 0.5（可执行占位底分）
                return 0.5f;
            case NeedKind.ExpeditionNeed: // ⑪ 出征军（占位可执行）：军事期且战士达标 → 想出征（占位底分 0.5）
                return k.warriorCount >= MilitaryTarget(k, KingdomBrain.LoadConfig()) ? 0.5f : 0f;
            case NeedKind.TerritoryGap:    // ⑩ 推边界（HH.32 裁2 A′）非初始占区 < 目标越缺越想扩（欲望与容量分离）
            {
                // 非初始占区 = 初始圈(D343)之外的新增中区块；needA=目标数(SO，占位 6)
                int nonInitial = TerritorySystem.Instance != null
                    ? TerritorySystem.Instance.NonInitialTerritoryCount(k.id) : 0;
                return d.needA > 0 ? Mathf.Clamp01((d.needA - nonInitial) / d.needA) : 0f;
            }
            // ===== HH.86 件3b/3c 三缺口 =====
            case NeedKind.WellGap:       // ⑯ 本国 Active 井数 < 目标（needA，占位 1）→ 缺口（井损重建 DZ-043）
            {
                int have = CountActiveDef(k.id, "Well");
                return have >= (int)Mathf.Max(1, d.needA) ? 0f : Mathf.Clamp01((d.needA - have) / Mathf.Max(1f, d.needA));
            }
            case NeedKind.MetalGap:      // ⑰ 国库 Metal 低于底线（needA，占位 30）→ 想建铁匠铺打通石→Metal（DZ-052）
                return Mathf.Clamp01((d.needA - k.GetResourceValue(ResourceType.Metal)) / Mathf.Max(1f, d.needA));
            case NeedKind.ExclusiveGap:  // ⑱~㉑ 本国已有族专属建筑 → 0；无 → 占位底分 0.5（族门禁在 Feasible，M6 复用）
                return KingdomRace.HasExclusiveBuilding(k.id, d.buildingId) ? 0f : 0.5f;
            // ===== 2_22 P0 批B / B2：训练将军（读 SituationHub 快照；快照缺席回退 0 分防 NRE）=====
            case NeedKind.GeneralGap:    // 缺将军：GeneralCount < generalLimit → 缺口；叠加内源势能项（D590 增补节②：
                                         // 缺口=内源基线、势能=连续叠加——无缺口时势能仍给行动压力=无袭扰环境不恒死滞）
            {
                if (!SituationHub.TryGet(k.id, out var sitG) || sitG == null) return 0f;
                var mcfg = KingdomManager.Instance != null ? KingdomManager.Instance.Config : null;
                int limit = mcfg != null && mcfg.generalLimit > 0 ? mcfg.generalLimit : 2;   // 对齐 CanTrainGeneral 兜底
                int haveG = sitG.GeneralCount;
                float gapG = haveG >= limit ? 0f : Mathf.Clamp01((limit - haveG) / (float)limit);
                return gapG + InternalDrive(k);
            }
            case NeedKind.FormationGap:  // 缺编队：FormationCount < needA 目标 → 缺口+内源势能叠加
            {
                if (!SituationHub.TryGet(k.id, out var sitF) || sitF == null) return 0f;
                int wantF = (int)Mathf.Max(1, d.needA);
                float gapF = sitF.FormationCount >= wantF ? 0f : Mathf.Clamp01((wantF - sitF.FormationCount) / (float)wantF);
                return gapF + InternalDrive(k);
            }
            case NeedKind.UnitTypeGap:   // 缺兵种：可训域战斗兵种（共通+本族，D570 口径）多样性缺口+内源势能叠加
            {
                if (!SituationHub.TryGet(k.id, out var sitU) || sitU == null) return 0f;
                // 可训域=TrainingDef（raceId==-1 共通 || ==本国族）且 IsCombat(toOccupation)，去重计数
                var trainable = new HashSet<int>();
                var tcfg = Resources.Load<TrainingConfig>("Config/TrainingConfig");
                int myRace = KingdomRace.GetKingdomRace(k.id);
                if (tcfg != null && tcfg.trainings != null)
                {
                    for (int i = 0; i < tcfg.trainings.Length; i++)
                    {
                        var t = tcfg.trainings[i];
                        if (t.raceId != -1 && t.raceId != myRace) continue;   // D419 族门禁预过滤
                        if (!MilitaryProfessions.IsCombat(t.toOccupation)) continue;
                        trainable.Add((int)t.toOccupation);
                    }
                }
                int totalT = trainable.Count;
                if (totalT <= 0) return 0f;
                // 拥有侧=快照 OwnedCombatOccupations 与可训域交集
                int ownedT = 0;
                if (sitU.OwnedCombatOccupations != null)
                    for (int i = 0; i < sitU.OwnedCombatOccupations.Count; i++)
                        if (trainable.Contains(sitU.OwnedCombatOccupations[i])) ownedT++;
                float gapU = ownedT >= totalT ? 0f : Mathf.Clamp01((totalT - ownedT) / (float)totalT);
                return gapU + InternalDrive(k);
            }
            case NeedKind.MachineDemand: // ㉕ 造机器（B8）：战争态势驱动=军事期+守城/攻城需求域。
                                          // 批C 姿态层细化（批B 占位口径兑现）：需求域=邻接威胁非空（攻城向）
                                          // 或 姿态≥警戒档（PostureHub 守城向=动员档无威胁也有守城需求）；内源项不驱动机器（语义正交）
            {
                if (!SituationHub.TryGet(k.id, out var sitM) || sitM == null) return 0f;
                if (k.scriptPhase != ScriptStage.Military) return 0f;             // 军事期才响应战争态势
                bool threatM = sitM.Threats != null && sitM.Threats.Count > 0;    // 攻城需求：邻接威胁非空
                bool postureM = PostureHub.Get(k.id) >= MilitaryPosture.Alert;    // 守城需求：警戒/动员档（批C）
                if (!threatM && !postureM) return 0f;
                float wantM = Mathf.Max(1, d.needA);
                return Mathf.Clamp01(wantM / (wantM + sitM.MachineCount)) * 0.8f; // 机器数越少需求越高（上限内）
            }
            default: return 0f;
        }
    }

    /// <summary>
    /// D348 兵力目标占位公式：clamp(floor + ⌈威胁×scale⌉ + 阶段系数, floor, 2+工人数)。
    /// 威胁 = 邻国兵力 / max(己方兵力, 分母下限)（D339 分母零保护）；阶段系数=存活/发育 0、扩张 +expandFactor、军事 +militaryFactor。
    /// 邻国兵力真源=KingdomRegistry 其它非玩家王国战士（D348：从领土表+Registry 实时拉取为演进目标）。
    /// </summary>
    public static int MilitaryTarget(KingdomState k, KingdomBrainConfig cfg)
        => MilitaryTargetFromThreat(k, NeighborMilitary(k.id), cfg);

    /// <summary>城堡该级可达到的目标模块上限（CastleUnlockTable 静态表共享；2_17 批3b TechGap 读解锁态）。</summary>
    private static int GetTargetCap(ModuleType module, int castleLevel)
    {
        var table = UnityEngine.Resources.Load<CastleUnlockTable>("Config/CastleUnlockTable");
        return table != null ? table.GetModuleLevel(module, castleLevel) : 0;
    }

    /// <summary>纯威胁注入版兵力目标（冒烟#5 探针）：直接给威胁兵力，可脱离世界确定性测试 D348 公式与⑧⑪⑫缺口。</summary>
    public static int MilitaryTargetFromThreat(KingdomState k, int neighborMilitary, KingdomBrainConfig cfg)
    {
        int stageFactor = k.scriptPhase == ScriptStage.Military ? cfg.militaryStageFactor
            : k.scriptPhase == ScriptStage.Expand ? cfg.militaryExpandStageFactor : 0;
        return D348Target(k.warriorCount, k.workerCount, neighborMilitary, stageFactor, cfg);
    }

    /// <summary>
    /// D348 兵力目标纯整数核心（冒烟#5 直接测，零世界耦合）：clamp(floor + ⌈威胁×scale⌉ + 阶段系数, floor, 2+工人数)。
    /// 威胁 = neighborMilitary / max(warrior, 分母下限)（D339 分母零保护）；软帽 2+工人数=军力受经济人口约束。
    /// </summary>
    public static int D348Target(int warrior, int worker, float neighborMilitary,
        int stageFactor, KingdomBrainConfig cfg)
    {
        int denom = Mathf.Max(warrior, cfg.militaryThreatDenominatorMin);
        float threat = denom > 0 ? neighborMilitary / (float)denom : 0f;
        int target = cfg.militaryTargetFloor + Mathf.CeilToInt(threat * cfg.militaryThreatScale) + stageFactor;
        return Mathf.Clamp(target, cfg.militaryTargetFloor, cfg.militaryTargetFloor + worker);
    }

    /// <summary>⑦战士缺口纯分数（冒烟#5 探针）：兵力未达 D348 目标时缺口占比 target，兵力越缺越该补（恒 0..1）。</summary>
    public static float WarriorGapScore(int warrior, int target)
        => warrior >= target ? 0f : Mathf.Clamp01((target - warrior) / Mathf.Max(1f, target));

    /// <summary>
    /// 邻接王国兵力之和（D515/D339：威胁分子=「邻接」非全体；A5 邻接修正——旧实现=全部非玩家
    /// 王国战士求和，与 2_17 §3.1.3/D339/D348 文档口径偏差，本批对齐）。真源=KingdomRegistry
    /// 非玩家王国战士数 × TerritorySystem.AreKingdomsAdjacent（中区块 4 邻接触）。
    /// 玩家国(id=0)不计入（原口径保留：AI 间互算，玩家压力走常设底线被攻窗口）。
    /// </summary>
    private static int NeighborMilitary(int selfId)
    {
        var reg = KingdomRegistry.Instance;
        if (reg == null) return 0;
        var ts = TerritorySystem.Instance;
        int sum = 0;
        var all = reg.GetAll();
        for (int i = 0; i < all.Count; i++)
        {
            var o = all[i];
            if (o == null || o.id == selfId || o.IsPlayer) continue;
            // A5 邻接过滤：非邻接国兵力不入威胁分子（负探针锚：远距 AI 国加兵，威胁分不变）
            if (ts == null || ts.AreKingdomsAdjacent(selfId, o.id)) sum += o.warriorCount;
        }
        return sum;
    }

    /// <summary>
    /// 内源势能项（2_22 P0 批B，D590 增补节②③/D589 列报2）：三连续输入线性加权 × SO 总权重。
    /// 叠加语义=缺口分 + drive（不整体 clamp）——缺口满格 1.0 时势能仍有边际（0.1 保守量级），
    /// 无缺口时势能独立给行动压力（无袭扰环境不恒死滞=内源节拍核心）。
    /// 权重 SO 化（SituationConfig.internalDriveWeight 初始 0.1=数值禁区保守下沿，只接结构不调值；
    /// 可训练标量预留=factor_registry S 行随 B4 登记）。置 0=退化六考死滞表型（负探针锚）。
    /// </summary>
    private static float InternalDrive(KingdomState k)
    {
        if (!SituationHub.TryGet(k.id, out var sit) || sit == null) return 0f;
        var scfg = SituationConfig.Load();
        if (scfg == null || scfg.internalDriveWeight <= 0f) return 0f;
        float drive = scfg.driveEconShare * sit.DriveEconomic
                    + scfg.drivePopShare * sit.DrivePopPressure
                    + scfg.driveStorageShare * sit.DriveStorage;
        return scfg.internalDriveWeight * Mathf.Clamp01(drive);
    }

    /// <summary>二值可行性门控（D346）。不看需求连续量，硬条件不过 → 0 出局。
    /// 完整局批次口径修正：按 UtilityActionDef 成本镜像判资源（与门面执行同口径双保险）；
    /// ⑥招工人=粮付得起 aiRecruitFoodCost 且未达工人目标（AI 人口增长唯一通道）。</summary>
    private static bool Feasible(KingdomState k, UtilityActionDef d)
    {
        switch (d.id)
        {
            case UtilityAction.RecruitWorker:
            {
                // 招工人：粮 ≥ 招募成本（SO：aiRecruitFoodCost）且未达工人目标
                var bcfg = KingdomBrain.LoadConfig();
                return k.GetResourceValue(ResourceType.Food) >= Mathf.Max(1, bcfg.aiRecruitFoodCost)
                       && k.workerCount < d.needA;
            }
            case UtilityAction.RecruitWarrior:
            {
                // ⑦ 招战士：金粮 ≥ 直转成本 && 有工人可转款 && 未达兵力目标
                var bcfg = KingdomBrain.LoadConfig();
                return k.GetResourceValue(ResourceType.Gold) >= Mathf.Max(1, bcfg.recruitWarriorCostGold)
                       && k.GetResourceValue(ResourceType.Food) >= Mathf.Max(1, bcfg.recruitWarriorCostFood)
                       && k.workerCount > 0
                       && k.warriorCount < MilitaryTarget(k, bcfg);
            }
            case UtilityAction.Tech:
            {
                // ⑧ 科技升级：金 ≥ 升级成本（per-kingdom 解锁态步骤11 占位可执行前提）
                var bcfg = KingdomBrain.LoadConfig();
                return k.GetResourceValue(ResourceType.Gold) >= Mathf.Max(1, bcfg.techUpgradeCostGold);
            }
            case UtilityAction.BuildWall:
            {
                // ⑨ 修工事城墙：按 def 成本镜像判资源 + 现存城墙未达目标（选址/前置归执行门面）
                return k.GetResourceValue(ResourceType.Gold) >= d.costGold
                    && k.GetResourceValue(ResourceType.Stone) >= d.costStone
                    && k.GetResourceValue(ResourceType.Wood) >= d.costWood
                    && k.GetResourceValue(ResourceType.Food) >= d.costFood
                    && CountForts(k.id) < (int)Mathf.Max(0, d.needA);
            }
            case UtilityAction.Expedition:
            case UtilityAction.Reinforce:
            case UtilityAction.Diplomacy:
                // ⑪⑫⑮ 占位可执行：无硬门槛（L3 意图 / 2_18 外交接口占位，评分够即可选）
                return true;
            case UtilityAction.Expand:
            {
                // ⑩ 推边界可行性（D346 二值）：有初始领土可向外扩 + 非初始占区未达目标（needA=SO 目标）
                int nonInitial = TerritorySystem.Instance != null
                    ? TerritorySystem.Instance.NonInitialTerritoryCount(k.id) : 0;
                return k.Territory.Count > 0
                       && nonInitial < (int)Mathf.Max(0, d.needA);
            }
            case UtilityAction.BuildHouse:
            case UtilityAction.BuildWarehouse:
            case UtilityAction.BuildCapacity:
            case UtilityAction.BoostHarvest:
            case UtilityAction.Grain:
            case UtilityAction.BuildWell:
            case UtilityAction.BuildBlacksmith:
            case UtilityAction.BuildWarAcademy:
            case UtilityAction.BuildWarCamp:
            case UtilityAction.BuildLeyForge:
            case UtilityAction.BuildArcheryRange:
            // 2_22 P0 批B 三军事建造行动（批B 遗留缺口补全：原缺 case → default:false 挡死评分侧，
            // 批C 搭车随 D594 整改令一并补——三守卫①上限②族门禁③限建镜像全继承）
            case UtilityAction.BuildBarracks:
            case UtilityAction.BuildTrainingCamp:
            case UtilityAction.BuildSiegeWorkshop:
            {
                // 建造类：按 def 成本镜像逐项判国库（选址/前置等硬规则归执行门面二次校验）
                // HH.86 件3a②/3c 三守卫扩：
                // ①上限守卫（DZ-041 连轴根治）：d.buildTargetCap>0 时同 def 本国已建须 < 上限（对齐 WallGap 目标座数模式）——
                //   WarehouseGap/GrainGap 储量驱动纯单调 need 会连轴建满图（HH.85 实锤 79 座），上限封顶；
                // ②族门禁（M6 复用）：BuildingDef.raceId>=0 时须与本国族匹配（四专属行动）；
                // ③每族限建 1 镜像（def.uniquePerKingdom）：本国已建 ≥1 → 不可行（防焦点锁定在恒拒行动）。
                var bdef = BuildingFactory.FindDefById(d.buildingId);
                if (bdef != null)
                {
                    if (bdef.raceId >= 0 && bdef.raceId != KingdomRace.GetKingdomRace(k.id)) return false;
                    if (bdef.uniquePerKingdom && CountActiveDef(k.id, d.buildingId) >= 1) return false;
                }
                if (d.buildTargetCap > 0 && CountActiveDef(k.id, d.buildingId) >= d.buildTargetCap) return false;
                return k.GetResourceValue(ResourceType.Gold) >= d.costGold
                    && k.GetResourceValue(ResourceType.Stone) >= d.costStone
                    && k.GetResourceValue(ResourceType.Wood) >= d.costWood
                    && k.GetResourceValue(ResourceType.Food) >= d.costFood;
            }
            // ===== 2_22 P0 批B 遗留缺口补全+D594 整改令（批C 搭车）=====
            case UtilityAction.TrainGeneral:
            {
                // ⑯ 训练将军（批B 遗留补 case）：金成本镜像（TrainingDef General 条目×trainCostMul ceil，
                // 与 TryTrainFromKingdomPool 扣费同口径）+将军上限镜像（generalLimit，对齐 CanTrainGeneral）；
                // 兵营前置由 GeneralGap 评分导向建（执行面 FindKingdomBuilding 早退守卫已有，评分不镜像防过严）。
                var mcfg = KingdomManager.Instance != null ? KingdomManager.Instance.Config : null;
                int limit = mcfg != null && mcfg.generalLimit > 0 ? mcfg.generalLimit : 2;
                if (KingdomBrain.CountGenerals(k.id) >= limit) return false;
                var tcfgG = Resources.Load<TrainingConfig>("Config/TrainingConfig");
                var raceDefG = KingdomRace.GetKingdomRaceDef(k.id);
                int myRaceG = KingdomRace.GetKingdomRace(k.id);
                if (tcfgG?.trainings == null || raceDefG == null) return false;
                for (int i = 0; i < tcfgG.trainings.Length; i++)
                {
                    var t = tcfgG.trainings[i];
                    if (t.toOccupation != Occupation.General) continue;
                    if (t.raceId != -1 && t.raceId != myRaceG) continue;   // D419 族门禁
                    return k.resources.gold >= Mathf.CeilToInt(t.costGold * raceDefG.trainCostMul);
                }
                return false;   // 无可训 General 条目（域配置缺失）
            }
            case UtilityAction.ProduceMachine:
            {
                // ㉕ 造战争机器（D594 整改令本体=🔴B8 prefab 预检+批B 遗留补 case）：
                // ①厂前置镜像（无投掷机厂不可评——防评分选中执行早退空转，⑳建厂缺口导向先建）
                // ②per-kingdom 上限镜像（GetPlacedMachineCountByKingdom/GetMachineLimit 单源）
                // ③prefab 预检（D594 硬条款）：本族可造机器 prefab 全缺失 → 不评（false）——
                //   缺口驱动持续选中→反复扣费→不生成→上限守卫永不触发=资源流失黑洞（D594 风险定性）；
                //   判定同源=MachinePanel.IsPrefabMissing（UnitDataManager 图纸面，HH.111 P5 口径）；
                // ④金成本镜像（PeekMachineCost 单源，选型=本族首台 IsMachineAllowed 机器升序确定性）。
                var sps = SiegeProductionSystem.Instance;
                if (sps == null) return false;
                if (CountActiveDef(k.id, BuildingIds.SiegeWorkshop) < 1) return false;   // ①厂前置
                if (sps.GetPlacedMachineCountByKingdom(k.id) >= sps.GetMachineLimit()) return false;   // ②上限
                Occupation[] machinePool = { Occupation.Ballista, Occupation.SiegeMachine, Occupation.Mortar, Occupation.VineCatapult, Occupation.Ram };
                int raceK = KingdomRace.GetKingdomRace(k.id);
                Occupation pickK = machinePool[0];
                bool foundK = false;
                bool anyPrefabReady = false;
                for (int i = 0; i < machinePool.Length; i++)
                {
                    if (!SiegeProductionSystem.IsMachineAllowed(raceK, machinePool[i])) continue;
                    if (!foundK) { pickK = machinePool[i]; foundK = true; }   // 升序首台=执行选型同源
                    if (!MachinePanel.IsPrefabMissing(machinePool[i], out _)) anyPrefabReady = true;   // ③任一台在场即可
                }
                if (!foundK) return false;                                    // 本族无可造机器
                if (!anyPrefabReady) return false;                            // ③prefab 全缺失→不评（D594 硬条款）
                var costK = sps.PeekMachineCost(pickK);
                return k.resources.gold >= costK.gold;   // ④金成本镜像（石木按 2_20.1 §8.1 机器造价域=金主导，执行面全量校验兜底）
            }
            case UtilityAction.Rebuild:
            case UtilityAction.Defense:
                // 姿态项全阶段可见、无硬门槛（D318）
                return true;
            default:
                return false;
        }
    }

    /// <summary>某王国活跃建筑数（与 KingdomBrain.BuildContext 同口径；P0 产能下限近似）。</summary>
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

    /// <summary>某王国已建成工事城墙座数（⑨ 缺口统计；按建筑 IsFortification 判定，D318 全阶段可修）。</summary>
    private static int CountForts(int kingdomId)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return 0;
        int n = 0;
        for (int i = 0; i < reg.All.Count; i++)
        {
            var b = reg.All[i];
            if (b != null && b.kingdomId == kingdomId && b.IsActive && b.IsFortification)
                n++;
        }
        return n;
    }

    /// <summary>某王国指定 def id 的 Active 建筑数（HH.86 件3b/3c：WellGap 统计+Feasible 限建/上限守卫共用）。</summary>
    private static int CountActiveDef(int kingdomId, string defId)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return 0;
        int n = 0;
        for (int i = 0; i < reg.All.Count; i++)
        {
            var b = reg.All[i];
            if (b != null && b.def != null && b.kingdomId == kingdomId && b.IsActive
                && b.def.id == defId)
                n++;
        }
        return n;
    }
}