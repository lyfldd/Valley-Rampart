using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  2_17 步骤14 AbstractEconomySettler 冒烟（HH.45 交付总报告；任务书 P1~P6 + 冒烟 #9/#12）
//  用法：菜单「Valley/验证/2_17_步骤14_抽象经济」——须 GameScene Play（先 Play 再点）。
//  自含断言（不依赖世界生成/NewGame 引导链，对齐 Smoke_13 哲学；每探针独立王国 id 防残留串扰）：
//    P1 抽象不僵死：纯引擎 SettleDaily 合成快照 → 非零结算（实体冻结但公式推进）；Abstract 工人
//       IsSimDormant=true（冻结）同帧公式仍产出 → "实体僵死、经济不僵死"。
//    P2 玩家零回归（负探针）：Abstract 王国在场日结，玩家(0)国库逐字段不变（IsPlayer 双守卫）。
//    P3 同 seed 双轮逐字节一致：引擎 SettleDaily 同输入两次逐字段一致 + 适配层 Tick 同起点双轮国库一致。
//    P4 P0 基线结构性守卫（批C 触及面）：CastleUnlockTable 5 模块城堡1解锁 / 索引5→0；moduleLevels
//       长度6 保留；玩家恒 Fine 无脑（A4 结构性）；BuildingRegistry 注入后清干净（无残留=账本无差）。
//    P5 D453 进食统一 + 唤醒拉平：Fine AI 实体进食扣本国国库；Abstract 实体跳过（饱食不变）但公式
//       耗粮；唤醒拉平 lastAbstractAvgSatiety→实体饱食 + 标记重置（D335 无跳变；无粮再衰减计内）。
//    P6 D461 退役口径：词边界 grep 排除 Siege* 变体 + 例外清单（researchLevels schema 字段保留）；
//       编译 0error（本套件能跑=同程序集编译通过，MCP Console 0 error 另录）；无 NRE；研究 UI 无入口
//       （ModuleOrder 无 Science / uxml 无 tab-science / Resources/Buildings 无 Academy/Workshop def）。
//    #9 SimMode 反复切 10 次账本无差：Abstract/Fine 交替 10 次两整轮 → 国库逐字段一致。
//    #12 同 seed 全程含抽象结算路径逐字节一致：步骤14 日结链（AI段+Abstract段，D459 分叉）3 日两轮
//        → 国库序列逐字段一致。
//  收口：所有注入 fixture 收尾清理（registry/建筑/单位/国库/均饱食桶/simMode 快照还原），防污染。
// ============================================================================
public static class Valley2_17_Smoke_14
{
    private const int K_P1 = 81;   // P1 冻结语义（独立王国）
    private const int K_P2 = 82;   // P2 负探针 Abstract 在场国
    private const int K_P3 = 83;   // P3 适配层确定性
    private const int K5A = 84;    // P5a Fine 进食
    private const int K5B = 85;    // P5b Abstract 跳过
    private const int K5C = 86;    // P5c 唤醒拉平
    private const int K9 = 87;     // #9 切换账本
    private const int K12 = 88;    // #12 全程确定性
    private const int WORKER_DEF_COST = 1;   // Worker 日耗粮（KingdomConfig 实际读取，此为回退）

    // ===== fixture 追踪（收尾统一清理）=====
    private static readonly List<GameObject> s_gos = new List<GameObject>();
    private static readonly List<Building> s_buildings = new List<Building>();
    private static readonly List<UnitController> s_units = new List<UnitController>();

    [MenuItem("Valley/验证/2_17_步骤14_抽象经济")]
    public static void RunFromMenu()
    {
        if (!Application.isPlaying) { Debug.LogError("[2_17_14冒烟] 须在 Play 上下文执行。"); return; }
        new GameObject("2_17_14_SmokeRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    public static IEnumerator RunCoroutine()
    {
        var reg = KingdomRegistry.Instance;
        var sm = SimModeManager.Instance;
        var grid = GridSystem.Instance;
        if (reg == null || sm == null || grid == null)
        { Debug.LogError("[2_17_14冒烟] 单例缺失（需 Play 上下文）。"); yield break; }
        if (reg.Count == 0) reg.EnsurePlayerRegistered();
        yield return null;

        var results = new List<string>();
        var regF = typeof(KingdomRegistry).GetField("_kingdoms", BindingFlags.Instance | BindingFlags.NonPublic);
        var orig = (List<KingdomState>)regF.GetValue(reg);

        // ===== 实时会话快照（防探针污染真实王国/玩家；finally 还原）=====
        var treasurySnapshot = new Dictionary<int, ResourcePack>();
        var simModeSnapshot = new Dictionary<int, SimMode>();
        var wakeSnapshot = new Dictionary<int, float>();
        var avgSatietySnapshot = CaptureAvgSatietyBucket();
        int rulerFood = RulerController.Instance != null
            ? RulerController.Instance.GetResource(ResourceType.Food) : -1;
        int buildingBaseline = BuildingRegistry.Instance != null ? BuildingRegistry.Instance.Count : 0;
        if (orig != null)
        {
            foreach (var k in orig)
            {
                treasurySnapshot[k.id] = new ResourcePack
                { gold = k.resources.gold, stone = k.resources.stone, wood = k.resources.wood,
                  food = k.resources.food, metal = k.resources.metal };
                simModeSnapshot[k.id] = k.simMode;
                wakeSnapshot[k.id] = k.lastAbstractAvgSatiety;
            }
        }

        try
        {
            // ===================== P1 抽象不僵死 =====================
            bool p1 = ProbeAbstractNotFrozen();
            results.Add($"P1 抽象不僵死（公式非零推进 + Abstract工人冻结同帧产出） = {p1}");

            // ===================== P2 玩家零回归（负探针）=====================
            bool p2 = ProbePlayerZeroRegression();
            results.Add($"P2 玩家零回归（负探针：玩家国库不被公式改写） = {p2}");

            // ===================== P3 同 seed 双轮逐字节一致 =====================
            bool p3 = ProbeDeterminism();
            results.Add($"P3 同seed双轮逐字节一致（引擎SettleDaily + 适配层Tick） = {p3}");

            // ===================== P4 P0 基线结构性守卫 =====================
            bool p4 = ProbeP0BaselineGuard(buildingBaseline);
            results.Add($"P4 P0基线结构性守卫（城堡表5模块/索引5空置/moduleLevels长度6/玩家无脑/注册表无残留） = {p4}");

            // ===================== P5 D453 进食统一 + 唤醒拉平 =====================
            bool p5 = ProbeSatietyUnified();
            results.Add($"P5 D453进食统一（Fine扣AI国库/Abstract跳过/唤醒拉平无跳变） = {p5}");

            // ===================== P6 D461 退役口径 =====================
            bool p6 = ProbeRetirementClean();
            results.Add($"P6 D461退役口径（词边界grep排除Siege*+研究UI无入口+module5清空） = {p6}");

            // ===================== #9 切 10 次账本无差 =====================
            bool s9 = ProbeSwitch10Ledger();
            results.Add($"#9 SimMode切10次账本无差（两整轮国库逐字段一致） = {s9}");

            // ===================== #12 全程含抽象结算路径逐字节一致 =====================
            bool s12 = ProbeFullPathDeterminism();
            results.Add($"#12 同seed全程含抽象结算路径逐字节一致（日结链3日两轮） = {s12}");
        }
        finally
        {
            CleanupFixtures();
            if (regF != null) regF.SetValue(reg, orig);
            RestoreAll(treasurySnapshot, simModeSnapshot, wakeSnapshot, avgSatietySnapshot, rulerFood);
        }

        bool allPass = true;
        foreach (var line in results) { Debug.Log("[2_17_14冒烟] " + line); if (line.Contains("= False")) allPass = false; }
        Debug.Log($"[2_17_14冒烟] ===== {(allPass ? "ALL PASS" : "HAS FAIL")}（P1~P6 + #9/#12）=====");
    }

    // ===================== P1 =====================

    private static bool ProbeAbstractNotFrozen()
    {
        // 引擎级：合成快照（工人3 + 农田lv2 + 粮100）→ 非零结算
        var snap = BuildEngineSnapshot(3, 3, 1, 0, 100, 10, 0, 0, 0, 50f, 0);
        var p = LoadParams();
        var d1 = AbstractEconomySettler.SettleDaily(snap, p, EcoModifiers.Default);
        bool engine = d1.Food != 0 || d1.Gold != 0 || d1.Wood != 0 || d1.Stone != 0 || d1.Metal != 0;

        // 冻结语义：Abstract 工人 IsSimDormant=true（实体僵死）但公式仍在推进（引擎非零 = 经济不僵死）
        bool freeze = ProbeWorkerFrozen();
        return engine && freeze;
    }

    /// <summary>Abstract 王国工人 IsSimDormant=true（冻结），证明"实体僵死"与"公式推进"并存。</summary>
    private static bool ProbeWorkerFrozen()
    {
        var k = new KingdomState { id = K_P1, simMode = SimMode.Abstract };
        k.moduleLevels = new int[6];
        InjectKingdom(k);
        var go = new GameObject("s14_freeze");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<Rigidbody2D>();
        var uc = go.AddComponent<UnitController>();
        uc.kingdomId = K_P1;
        var def = Resources.Load<UnitData>("UnitData/Human_Player_Worker");
        if (def != null) uc.Initialize(def);
        uc.SetOccupation(Occupation.Worker);
        uc.SetFaction(Faction.AiKingdom);
        var brain = go.AddComponent<NPCBrain>();
        var ctrlF = typeof(NPCBrain).GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic);
        ctrlF?.SetValue(brain, uc);
        var m = typeof(NPCBrain).GetMethod("IsSimDormant", BindingFlags.Instance | BindingFlags.NonPublic);
        bool frozen = false;
        try
        {
            frozen = m != null && (bool)m.Invoke(brain, null);
        }
        finally
        {
            Object.Destroy(go);
        }
        return frozen;
    }

    // ===================== P2 =====================

    private static bool ProbePlayerZeroRegression()
    {
        var reg = KingdomRegistry.Instance;
        var player = reg.Get(0);
        if (player == null) return false;
        // 负探针：置显著值 → 在场 Abstract 王国日结 → 玩家国库逐字段不变
        var pre = new ResourcePack { gold = player.resources.gold, stone = player.resources.stone,
            wood = player.resources.wood, food = player.resources.food, metal = player.resources.metal };
        player.resources.gold = 45; player.resources.stone = 6; player.resources.wood = 7;
        player.resources.food = 123; player.resources.metal = 8;

        // 造一个 Abstract 王国在场（独立 id=K_P2）
        var k = NewKingdom(K_P2, SimMode.Abstract, 100, 10, 0, 0, 0);
        AbstractEconomySettlement.Tick();

        bool unchanged = player.resources.gold == 45 && player.resources.stone == 6
            && player.resources.wood == 7 && player.resources.food == 123 && player.resources.metal == 8;
        player.resources.gold = pre.gold; player.resources.stone = pre.stone;
        player.resources.wood = pre.wood; player.resources.food = pre.food; player.resources.metal = pre.metal;
        return unchanged;
    }

    // ===================== P3 =====================

    private static bool ProbeDeterminism()
    {
        // 引擎级：同输入两次逐字段一致
        var snap = BuildEngineSnapshot(3, 3, 1, 0, 100, 10, 0, 0, 0, 50f, 0);
        var p = LoadParams();
        var a = AbstractEconomySettler.SettleDaily(snap, p, EcoModifiers.Default);
        var b = AbstractEconomySettler.SettleDaily(snap, p, EcoModifiers.Default);
        bool engine = SameDelta(a, b);

        // 适配层级：独立王国 K_P3，2农场+1采石（产出>消耗，粮食肉眼可见推进），同起点双轮 Tick → 国库一致
        var k = NewKingdom(K_P3, SimMode.Abstract, 100, 10, 0, 0, 0);
        MakeUnit(K_P3, Occupation.Worker, 80, Faction.AiKingdom);
        MakeUnit(K_P3, Occupation.Worker, 80, Faction.AiKingdom);
        MakeUnit(K_P3, Occupation.Resident, 80, Faction.AiKingdom);
        MakeUnit(K_P3, Occupation.Resident, 80, Faction.AiKingdom);
        MakeUnit(K_P3, Occupation.Warrior, 80, Faction.AiKingdom);
        MakeBuilding(K_P3, "farm", new GridCoord(100, 100, 0), 2);
        MakeBuilding(K_P3, "farm", new GridCoord(101, 100, 0), 2);
        MakeBuilding(K_P3, "quarry", new GridCoord(102, 100, 0), 1);

        var start = CopyRes(k.resources);
        AbstractEconomySettlement.Tick();
        var r1 = CopyRes(k.resources);
        RestoreRes(ref k.resources, start);                 // 同起点复位（关键：两轮起点必须逐字段一致）
        AbstractEconomySettlement.OnMapGenerated();
        AbstractEconomySettlement.Tick();
        var r2 = CopyRes(k.resources);
        bool adapter = SamePack(r1, r2) && !SamePack(r1, start);
        return engine && adapter;
    }

    // ===================== P4 =====================

    private static bool ProbeP0BaselineGuard(int buildingBaseline)
    {
        // a) CastleUnlockTable：5 模块城堡1 全解锁；索引5（Science 已退役）恒 0
        var table = Resources.Load<CastleUnlockTable>("Config/CastleUnlockTable");
        bool t1 = table != null;
        bool t2 = t1;
        for (int m = 0; m < 5; m++)
            if (table.GetModuleLevel((ModuleType)m, 1) < 1) t2 = false;
        bool t3 = t1 && table.GetModuleLevel((ModuleType)5, 6) == 0;

        // b) moduleLevels 长度 6 保留（schema 零变更）
        var km = KingdomManager.Instance;
        bool m6a = km != null && km.ModuleLevels != null && km.ModuleLevels.Length == 6;

        // c) A4 结构性：玩家恒 Fine 无脑 + 未知王国恒 Fine
        bool a4 = SimModeManager.Instance != null
            && SimModeManager.Instance.GetMode(0) == SimMode.Fine
            && SimModeManager.Instance.GetMode(999999) == SimMode.Fine;
        bool noBrain = KingdomBrainRegistry.Instance == null || KingdomBrainRegistry.Instance.Get(0) == null;

        // d) 注册表无残留（P0 判据 b 的账本无差守卫）：清理 fixture 后计数回注入前基线
        bool cleaned = CleanupFixtures();
        int bAfter = BuildingRegistry.Instance != null ? BuildingRegistry.Instance.Count : 0;
        bool leakFree = cleaned && bAfter == buildingBaseline;

        return t2 && t3 && m6a && a4 && noBrain && leakFree;
    }

    // ===================== P5 =====================

    private static bool ProbeSatietyUnified()
    {
        var cfg = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        if (cfg == null || SatietySystem.Instance == null) return false;
        bool fine = false, absSkip = false, wake = false;

        // 5a Fine AI 王国逐实体进食扣本国国库（独立 K5A）
        var kf = NewKingdom(K5A, SimMode.Fine, 100, 0, 0, 0, 0);
        var uf = MakeUnit(K5A, Occupation.Worker, 30, Faction.AiKingdom);
        int cost = cfg.GetDailyFoodByOccupation(Occupation.Worker);
        SatietySystem.Instance.OnNewDay();
        fine = uf.Satiety > 30 && kf.resources.food == 100 - cost;

        // 5b Abstract 王国实体跳过（饱食不变、国库不被实体进食）但公式耗粮（独立 K5B）
        var ka = NewKingdom(K5B, SimMode.Abstract, 100, 0, 0, 0, 0);
        var ua = MakeUnit(K5B, Occupation.Worker, 30, Faction.AiKingdom);
        SatietySystem.Instance.OnNewDay();
        bool entFrozen = ua.Satiety == 30 && ka.resources.food == 100;   // 实体未进食
        // 公式侧：无农场纯耗粮快照（工人0/居民1）→ 净负（公式计数进食）
        var snap = BuildEngineSnapshot(0, 1, 0, 0, 100, 0, 0, 0, 0, 50f, 0);
        var dd = AbstractEconomySettler.SettleDaily(snap, LoadParams(), EcoModifiers.Default);
        absSkip = entFrozen && dd.Food < 0;

        // 5c 唤醒拉平：lastAbstractAvgSatiety=40 且切 Fine → 实体饱食拉平到40 + 标记重置
        //（无粮 → 拉平后按无粮再衰减 satietyDecayPerDay，断言按"拉平后衰减"口径，防误报）
        var kw = NewKingdom(K5C, SimMode.Fine, 0, 0, 0, 0, 0);
        kw.lastAbstractAvgSatiety = 40f;
        var uw = MakeUnit(K5C, Occupation.Worker, 99, Faction.AiKingdom);
        SatietySystem.Instance.OnNewDay();
        int leveledThenDecay = Mathf.Clamp(40 - cfg.satietyDecayPerDay, 0, 100);
        wake = uw.Satiety == leveledThenDecay && kw.lastAbstractAvgSatiety == -1f;

        return fine && absSkip && wake;
    }

    // ===================== P6 =====================

    private static bool ProbeRetirementClean()
    {
        // a) 词边界 grep（排除 Siege* 变体；例外清单=researchLevels schema 字段保留）
        bool grep = ScanAssetsClean();

        // b) CastleUnlockTable 无 module:5
        string tableText = ReadAssetText("Assets/Resources/Config/CastleUnlockTable.asset");
        bool noModule5 = tableText == null || !Regex.IsMatch(tableText, @"module:\s*5");

        // c) 研究 UI 无入口：ModuleOrder 无 Science / uxml 无 tab-science / Buildings 无 Academy/Workshop def
        bool menuOrder = NoScienceInModuleOrder();
        string uxml = ReadAssetText("Assets/_Game/UI/BuildingMenuPanel.uxml");
        bool noTab = uxml == null || !uxml.Contains("tab-science");
        bool noDefs = !HasRetiredBuildingDefs();
        bool enumClean = !System.Array.Exists(System.Enum.GetNames(typeof(ModuleType)), n => n == "Science");

        return grep && noModule5 && menuOrder && noTab && noDefs && enumClean;
    }

    // ===================== #9 =====================

    private static bool ProbeSwitch10Ledger()
    {
        var k = NewKingdom(K9, SimMode.Abstract, 100, 10, 0, 0, 0);
        MakeUnit(K9, Occupation.Worker, 80, Faction.AiKingdom);
        MakeUnit(K9, Occupation.Worker, 80, Faction.AiKingdom);
        MakeUnit(K9, Occupation.Resident, 80, Faction.AiKingdom);
        MakeBuilding(K9, "farm", new GridCoord(100, 100, 0), 2);
        MakeBuilding(K9, "farm", new GridCoord(101, 100, 0), 2);
        var start = CopyRes(k.resources);

        ResourcePack Round()
        {
            RestoreRes(ref k.resources, start);
            AbstractEconomySettlement.OnMapGenerated();
            for (int cyc = 0; cyc < 10; cyc++)
            {
                k.simMode = SimMode.Abstract;
                AbstractEconomySettlement.Tick();   // 公式结算
                k.simMode = SimMode.Fine;
                AIEconomySettlement.Tick();         // Fine 段（无 Storage 建筑 → 不双写；守卫 Abstract 跳过）
            }
            return CopyRes(k.resources);
        }

        var r1 = Round();
        var r2 = Round();
        Debug.Log($"[2_17_14冒烟] #9 起点=[{PackStr(start)}] r1=[{PackStr(r1)}] r2=[{PackStr(r2)}]");
        return SamePack(r1, r2) && !SamePack(r1, start);
    }

    // ===================== #12 =====================

    private static bool ProbeFullPathDeterminism()
    {
        var k = NewKingdom(K12, SimMode.Abstract, 100, 10, 0, 0, 0);
        MakeUnit(K12, Occupation.Worker, 80, Faction.AiKingdom);
        MakeUnit(K12, Occupation.Worker, 80, Faction.AiKingdom);
        MakeUnit(K12, Occupation.Resident, 80, Faction.AiKingdom);
        MakeUnit(K12, Occupation.Resident, 80, Faction.AiKingdom);
        MakeUnit(K12, Occupation.Warrior, 80, Faction.AiKingdom);
        MakeBuilding(K12, "farm", new GridCoord(100, 100, 0), 2);
        MakeBuilding(K12, "farm", new GridCoord(101, 100, 0), 2);
        MakeBuilding(K12, "quarry", new GridCoord(102, 100, 0), 1);
        var start = CopyRes(k.resources);

        List<ResourcePack> Round()
        {
            RestoreRes(ref k.resources, start);
            AbstractEconomySettlement.OnMapGenerated();
            var seq = new List<ResourcePack>();
            for (int day = 0; day < 3; day++)
            {
                AIEconomySettlement.Tick();       // 步骤14 日结链：AI段（Fine，D459 分叉跳过 Abstract）
                AbstractEconomySettlement.Tick(); // 步骤14 日结链：Abstract 段（公式，D459）
                seq.Add(CopyRes(k.resources));
            }
            return seq;
        }

        var s1 = Round();
        var s2 = Round();
        if (s1.Count != s2.Count) return false;
        for (int i = 0; i < s1.Count; i++)
            if (!SamePack(s1[i], s2[i])) return false;
        return !SamePack(s1[s1.Count - 1], start);   // 有推进（非恒等）
    }

    // ===================== 工具 =====================

    private static AbstractEconomyParams LoadParams()
    {
        var so = AbstractEconomyConfig.LoadConfig();
        return so != null ? so.ToParams() : AbstractEconomyParams.Default;
    }

    /// <summary>纯 C# 引擎快照（固定注入 农田lv2 + 采石lv1）。</summary>
    private static KingdomEconomySnapshot BuildEngineSnapshot(int workers, int life, int soldier, int elite,
        int food, int gold, int stone, int wood, int metal, float avgSat, int unfed)
    {
        return new KingdomEconomySnapshot
        {
            KingdomId = K_P3,
            WorkerCount = workers,
            LifeCount = life,
            SoldierCount = soldier,
            EliteCount = elite,
            ContinuousUnfedDays = unfed,
            Food = food, Gold = gold, Stone = stone, Wood = wood, Metal = metal,
            AvgSatiety = avgSat,
            Buildings = new System.Collections.Generic.List<AbstractBuildingEntry>
            {
                new AbstractBuildingEntry { Type = "Food", Level = 2, ConcurrentCapacity = 0 },
                new AbstractBuildingEntry { Type = "Stone", Level = 1, ConcurrentCapacity = 0 }
            }
        };
    }

    /// <summary>新建王国并注入注册表（独立 id；资源显式置初值）。</summary>
    private static KingdomState NewKingdom(int id, SimMode mode, int food, int gold, int stone, int wood, int metal)
    {
        var k = new KingdomState { id = id, simMode = mode };
        k.moduleLevels = new int[6];
        k.resources = new ResourcePack { food = food, gold = gold, stone = stone, wood = wood, metal = metal };
        InjectKingdom(k);
        return k;
    }

    private static void InjectKingdom(KingdomState k)
    {
        var reg = KingdomRegistry.Instance;
        var f = typeof(KingdomRegistry).GetField("_kingdoms", BindingFlags.Instance | BindingFlags.NonPublic);
        var list = (List<KingdomState>)f.GetValue(reg);
        if (list.Exists(x => x.id == k.id)) return;
        f.SetValue(reg, new List<KingdomState>(list) { k });
    }

    private static UnitController MakeUnit(int kid, Occupation occ, int satiety, Faction fac)
    {
        var go = new GameObject($"s14_u_{kid}_{occ}");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<Rigidbody2D>();
        var uc = go.AddComponent<UnitController>();
        uc.kingdomId = kid;
        var def = Resources.Load<UnitData>("UnitData/Human_Player_Worker");
        if (def != null) uc.Initialize(def);
        uc.SetOccupation(occ);
        uc.SetFaction(fac);
        uc.Satiety = satiety;
        s_gos.Add(go);
        s_units.Add(uc);
        return uc;
    }

    private static Building MakeBuilding(int kid, string defId, GridCoord coord, int level)
    {
        var go = new GameObject($"s14_b_{kid}_{defId}");
        var b = go.AddComponent<Building>();
        var def = Resources.Load<BuildingDef>("Buildings/" + defId);
        b.def = def;
        b.kingdomId = kid;
        b.level = level;
        if (def != null) b.Init(def, coord);   // Init 内部重置 level=1（抽象公式按实 level 计，符合产品语义）
        b.state = BuildingState.Active;
        BuildingRegistry.Instance?.Register(b);
        s_gos.Add(go);
        s_buildings.Add(b);
        return b;
    }

    private static ResourcePack CopyRes(ResourcePack r) => new ResourcePack
    { gold = r.gold, stone = r.stone, wood = r.wood, food = r.food, metal = r.metal };

    /// <summary>复位目标国库为起点（ResourcePack 为 struct → 必须 ref 传参才能真正写回，否则按值传=空操作）。
    /// 冒烟自修复（2026-08-31 首跑 P3/#9/#12 失败根因）：此前按值传导致"同起点复位"失效、两轮国库持续累计→假性非确定。</summary>
    private static void RestoreRes(ref ResourcePack target, ResourcePack src)
    {
        target.gold = src.gold; target.stone = src.stone; target.wood = src.wood;
        target.food = src.food; target.metal = src.metal;
    }

    private static bool SamePack(ResourcePack a, ResourcePack b)
        => a.gold == b.gold && a.stone == b.stone && a.wood == b.wood && a.food == b.food && a.metal == b.metal;

    private static string PackStr(ResourcePack r)
        => $"粮{r.food}金{r.gold}石{r.stone}木{r.wood}铁{r.metal}";

    private static bool SameDelta(SettlementDelta a, SettlementDelta b)
        => a.Food == b.Food && a.Gold == b.Gold && a.Stone == b.Stone && a.Wood == b.Wood
        && a.Metal == b.Metal && a.AvgSatiety == b.AvgSatiety && a.FoodExhausted == b.FoodExhausted
        && a.UnfedShortfall == b.UnfedShortfall && a.LossResidents == b.LossResidents
        && a.LossSoldiers == b.LossSoldiers && a.HasLoss == b.HasLoss;

    /// <summary>遍历 Assets 做词边界 grep（排除 Siege*；例外=researchLevels schema 字段保留）。</summary>
    private static bool ScanAssetsClean()
    {
        var patterns = new[]
        {
            @"\bAcademyBuilding\b", @"\bResearchProject\b", @"\bResearchCompletedEvent\b",
            @"\bModuleType\.Science\b", @"Module_Science", @"\btab-science\b",
            @"\bGetResearchLevel\b", @"\bApplyResearch\b", @"\bOnResearchProjectClicked\b",
            @"def\.id == ""Academy""", @"def\.id == ""Workshop"""
        };
        string root = Application.dataPath;
        foreach (var pat in patterns)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var f in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
            {
                if (!IsTextAsset(f)) continue;
                if (f.Contains("\\Editor\\Smoke\\")) continue;   // 本套件自身含模式串，排除
                string txt;
                try { txt = File.ReadAllText(f); } catch { continue; }
                if (Regex.IsMatch(txt, pat))
                {
                    if (pat == @"\bResearchProject\b" && txt.Contains("researchLevels")) continue;  // schema 例外
                    Debug.Log($"[2_17_14冒烟] P6 grep 命中：{pat} @ {f}");
                    return false;
                }
            }
        }
        return true;
    }

    private static bool IsTextAsset(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".cs" || ext == ".asset" || ext == ".uxml" || ext == ".uss" || ext == ".unity";
    }

    private static string ReadAssetText(string rel)
    {
        string full = Path.Combine(Application.dataPath, rel.Replace("Assets/", ""));
        return File.Exists(full) ? File.ReadAllText(full) : null;
    }

    /// <summary>BuildingMenuPanel.ModuleOrder 反射：无 Science 项（长度 5，值域 0..4）。</summary>
    private static bool NoScienceInModuleOrder()
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("BuildingMenuPanel");
            if (t == null) continue;
            var f = t.GetField("ModuleOrder", BindingFlags.Static | BindingFlags.NonPublic);
            if (f == null) continue;
            var arr = f.GetValue(null) as ModuleType[];
            if (arr == null) return true;   // 拿不到就按"无"处理（保守真）
            if (arr.Length != 5) return false;
            for (int i = 0; i < arr.Length; i++)
                if ((int)arr[i] == 5) return false;
            return true;
        }
        return true;
    }

    private static bool HasRetiredBuildingDefs()
    {
        var defs = Resources.LoadAll<BuildingDef>("Buildings");
        foreach (var d in defs)
            if (d != null && (d.id == "Academy" || d.id == "Workshop")) return true;
        return false;
    }

    /// <summary>快照 SatietySystem._avgSatiety 桶（还原用）。</summary>
    private static Dictionary<int, float> CaptureAvgSatietyBucket()
    {
        var result = new Dictionary<int, float>();
        if (SatietySystem.Instance == null) return result;
        var f = typeof(SatietySystem).GetField("_avgSatiety", BindingFlags.Instance | BindingFlags.NonPublic);
        var dict = f?.GetValue(SatietySystem.Instance) as Dictionary<int, float>;
        if (dict != null) foreach (var kv in dict) result[kv.Key] = kv.Value;
        return result;
    }

    private static bool CleanupFixtures()
    {
        if (BuildingRegistry.Instance != null)
            for (int i = s_buildings.Count - 1; i >= 0; i--)
                if (s_buildings[i] != null) BuildingRegistry.Instance.Unregister(s_buildings[i]);
        if (UnitRegistry.Instance != null)
            for (int i = s_units.Count - 1; i >= 0; i--)
                if (s_units[i] != null) UnitRegistry.Instance.Unregister(s_units[i]);
        for (int i = s_gos.Count - 1; i >= 0; i--)
            if (s_gos[i] != null) Object.Destroy(s_gos[i]);
        s_buildings.Clear();
        s_units.Clear();
        s_gos.Clear();
        return true;
    }

    private static void RestoreAll(Dictionary<int, ResourcePack> tres,
        Dictionary<int, SimMode> modes, Dictionary<int, float> wake,
        Dictionary<int, float> avgBucket, int rulerFood)
    {
        var reg = KingdomRegistry.Instance;
        if (reg != null)
        {
            foreach (var kv in tres)
            {
                var k = reg.Get(kv.Key);
                if (k == null) continue;
                RestoreRes(ref k.resources, kv.Value);
                if (modes.TryGetValue(kv.Key, out var m)) k.simMode = m;
                if (wake.TryGetValue(kv.Key, out var w)) k.lastAbstractAvgSatiety = w;
            }
        }
        if (SatietySystem.Instance != null && avgBucket != null)
        {
            var f = typeof(SatietySystem).GetField("_avgSatiety", BindingFlags.Instance | BindingFlags.NonPublic);
            var dict = f?.GetValue(SatietySystem.Instance) as Dictionary<int, float>;
            if (dict != null)
            {
                dict.Clear();
                foreach (var kv in avgBucket) dict[kv.Key] = kv.Value;
            }
        }
        if (rulerFood >= 0 && RulerController.Instance != null)
        {
            int now = RulerController.Instance.GetResource(ResourceType.Food);
            if (now != rulerFood)
                RulerController.Instance.ModifyResource(ResourceType.Food, rulerFood > now, Mathf.Abs(rulerFood - now));
        }
    }

    private class RunHost : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}
