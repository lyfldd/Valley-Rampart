using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  2_17 步骤13 SimMode 冒烟（HH.42 §三批C + 实施计划步骤13 验收映射 #9/#10/#17）
//  用法：菜单「Valley/验证/2_17_步骤13_SimMode」——须 GameScene Play（先 Play 再点）。
//  自含断言（不依赖世界生成/NewGame 引导链，对齐 Smoke_11/12 哲学）：
//    P1 #17 活跃带视野：SimMode 判定只读 LODSystem 活跃带（D344，与相机缩放无关）——
//       聚焦 K 领土 → Evaluate → Fine；活跃带不变再 Evaluate → Fine 集不变（缩放全图不改变活跃带）。
//    P2 #9 切换迟滞：出视野日1 仍 Fine（counter=1）/ 日2 切 Abstract（counter=2）/ 入视野立即 Fine + counter 复位；
//       反复切 10 次无异常（账本无差全量部分归步骤14 对账验收，本步只验切换机制）。
//    P3 #10 战斗锁强制 Fine：#10a 事件驱动立即（UnitDamagedEvent 落入 Abstract 领土 → 当场 Fine）；
//        #10b 日判兜底（RegisterHeatEvent 注入热点 + 活跃带不覆盖 → EvaluateAllKingdoms 走战斗热点分支 → Fine）。
//    P4 军事单位不冻结（D281）：Abstract 王国 工人/搬运工 → 冻结 true；战士 → false；Fine 态工人 → false；玩家(0) → false。
//    P5 D454 ProduceMachine AI 分支：国库充足 → 成功+生成带 kingdomId / per-kingdom 上限（2 台后第3台拒）/ 国库不足 → 拒。
//    P6 P0 基线零回归（玩家恒 Fine / 未知王国恒 Fine，A4 结构性）。
//  收口：所有注入 fixture 收尾清理（registry/领土/热点/焦点/SimMode 计数/机器单位 + 他国 simMode 快照还原），防污染。
// ============================================================================
public static class Valley2_17_Smoke_13
{
    private const int K = 88;               // 合成 AI 王国 id（SimMode 判定探针）
    private const int K2 = 99;              // 合成 AI 王国 id（ProduceMachine 探针）
    private const int MACHINE_LIMIT = 2;    // 无投掷机厂时 GetMachineLimit()=2（占位探针期望）
    private static readonly Vector2Int MID_A = new Vector2Int(10, 10);    // K 领土中区块（入视野靶；cell 40~43 界内）
    private static readonly Vector2Int MID_FAR = new Vector2Int(30, 30);  // 出视野聚焦中区块（cell 120~123 界内 128×128）

    [MenuItem("Valley/验证/2_17_步骤13_SimMode")]
    public static void RunFromMenu()
    {
        if (!Application.isPlaying) { Debug.LogError("[2_17_13冒烟] 须在 Play 上下文执行。"); return; }
        new GameObject("2_17_13_SmokeRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    public static IEnumerator RunCoroutine()
    {
        var sm = SimModeManager.Instance;
        var lod = LODSystem.Instance;
        var ts = TerritorySystem.Instance;
        var reg = KingdomRegistry.Instance;
        var grid = GridSystem.Instance;
        if (sm == null || lod == null || ts == null || reg == null || grid == null)
        { Debug.LogError("[2_17_13冒烟] 单例缺失（需 Play 上下文）。"); yield break; }
        if (reg.Count == 0) reg.EnsurePlayerRegistered();
        yield return null;

        // 自含 fixture：裸 Play GridSystem 无地形 → 初始化全 Plain（绕开真实地形耦合）
        var terrainF = typeof(GridSystem).GetField("_terrain", BindingFlags.Instance | BindingFlags.NonPublic);
        if (terrainF != null && terrainF.GetValue(grid) == null)
        {
            grid.Initialize(128, 128);
            for (int x = 0; x < 128; x++) for (int y = 0; y < 128; y++)
                grid.SetTerrain(new GridCoord(x, y, 0), TerrainType.Plain);
        }

        // 他国 simMode 快照（防探针 focal 拨动污染真实王国；还原在 finally）
        var modeSnapshot = new Dictionary<int, SimMode>();
        foreach (var k in reg.GetAll()) if (!k.IsPlayer && k.id != K) modeSnapshot[k.id] = k.simMode;

        var results = new List<string>();
        var regF = typeof(KingdomRegistry).GetField("_kingdoms", BindingFlags.Instance | BindingFlags.NonPublic);
        var orig = (List<KingdomState>)regF.GetValue(reg);
        var synth = new KingdomState { id = K };
        var temp = new List<KingdomState>(orig) { synth };
        regF.SetValue(reg, temp);
        try
        {
            InjectTerritory(ts, MID_A, K);

            // ---- P1 #17 活跃带视野/缩放不变 Fine 集 ----
            lod.SetFocalCenter(MidToWorld(grid, MID_A));   // 聚焦 K 领土
            yield return null;                              // 等一帧渲染活跃带
            sm.EvaluateAllKingdoms();
            bool p1a = reg.Get(K).simMode == SimMode.Fine;
            sm.EvaluateAllKingdoms();                       // 活跃带不变再评 → Fine 集不变（D344 缩放无关）
            bool p1b = reg.Get(K).simMode == SimMode.Fine;
            results.Add($"P1 #17 活跃带视野 Fine集稳定（聚焦覆盖→Fine；带不变 Fine集不变） = {p1a && p1b}");

            // ---- P2 #9 切换迟滞（出视野2日切A·入视野立即F·反复切10次）----
            lod.SetFocalCenter(MidToWorld(grid, MID_FAR));  // 出视野
            yield return null;
            bool p2 = true;
            int failCycle = -1;
            for (int cyc = 0; cyc < 10; cyc++)
            {
                sm.EvaluateAllKingdoms();                   // 出视野日1：counter=1 → 仍 Fine（迟滞）
                bool day1 = reg.Get(K).simMode == SimMode.Fine;
                sm.EvaluateAllKingdoms();                   // 出视野日2：counter=2 → Abstract
                bool day2 = reg.Get(K).simMode == SimMode.Abstract;
                lod.SetFocalCenter(MidToWorld(grid, MID_A));// 入视野
                yield return null;
                sm.EvaluateAllKingdoms();                   // 入视野 → 立即 Fine + counter 复位
                bool back = reg.Get(K).simMode == SimMode.Fine;
                lod.SetFocalCenter(MidToWorld(grid, MID_FAR));  // 再出视野（下一循环）
                yield return null;
                if (!(day1 && day2 && back)) { p2 = false; failCycle = cyc; break; }
            }
            results.Add($"P2 #9 切换迟滞（出1日F/出2日A/入立即F，反复10次" + (p2 ? "" : $" 失败@cyc{failCycle}") + $") = {p2}");

            // ---- P3 #10 战斗锁强制 Fine ----
            // #10a 事件驱动立即：K 置 Abstract → UnitDamagedEvent 落入领土 → 当场 Fine（无需等日 tick）
            reg.Get(K).simMode = SimMode.Abstract;
            EventBus.Publish(new UnitDamagedEvent(null, null, 0, MidToWorld(grid, MID_A)));
            bool p3a = reg.Get(K).simMode == SimMode.Fine;
            // #10b 日判兜底（纯战斗热点路径）：清热度/活跃带 → 活跃带不覆盖（置非空中心但 band 空）+ 注入热点 → Evaluate → Fine
            ClearLodState(lod);
            SetActiveCentersNonEmpty(lod, MID_FAR);         // 关闭无中心兜底，band 仍空 → 领土不被活跃带覆盖
            reg.Get(K).simMode = SimMode.Abstract;
            lod.RegisterHeatEvent(CellAtMid(MID_A), HeatSource.Hit, 0f);   // 热点热度注入（Hit 默认 +0.4 > 阈值）
            sm.EvaluateAllKingdoms();
            bool p3b = reg.Get(K).simMode == SimMode.Fine;
            results.Add($"P3 #10 战斗锁（事件驱动立即={p3a} 日判热点兜底={p3b}） = {p3a && p3b}");

            // ---- P4 军事单位不冻结（D281）/ 非军事冻结（D334）----
            bool p4 = ProbeFreeze(reg);
            results.Add($"P4 军事不冻结/工人冻结（D281/D334） = {p4}");

            // ---- P5 D454 ProduceMachine AI 分支 ----
            bool p5 = ProbeProduceMachine();
            results.Add($"P5 D454 ProduceMachine AI（扣费/上限/带kingdomId） = {p5}");

            // ---- P6 P0 基线零回归（玩家恒 Fine / 未知王国 Fine，A4）----
            bool p6 = sm.GetMode(0) == SimMode.Fine && sm.GetMode(999999) == SimMode.Fine;
            results.Add($"P6 P0基线 玩家/未知王国恒Fine（A4） = {p6}");
        }
        finally
        {
            // 收尾清理：领土 / 热度 / 焦点 / SimMode 计数 / registry / 他国 simMode 还原
            RemoveTerritory(ts, MID_A);
            ClearLodState(lod);
            ClearUncoveredDays(sm);
            regF.SetValue(reg, orig);
            foreach (var kv in modeSnapshot)
            {
                var k = reg.Get(kv.Key);
                if (k != null) k.simMode = kv.Value;
            }
        }

        bool allPass = true;
        foreach (var line in results) { Debug.Log("[2_17_13冒烟] " + line); if (line.Contains("= False")) allPass = false; }
        Debug.Log($"[2_17_13冒烟] ===== {(allPass ? "ALL PASS" : "HAS FAIL")}（P1#17/P2#9迟滞/P3#10战斗锁/P4军事不冻结/P5 D454/P6基线）=====");
    }

    // ===== fixture 工具 =====

    /// <summary>中区块 → 中心格坐标（CellToMidChunk 回落该中区块，LODSystem L101 同款映射）。</summary>
    private static GridCoord CellAtMid(Vector2Int mid)
    {
        var grid = GridSystem.Instance;
        int ms = grid != null && grid.Config != null && grid.Config.midChunkSize > 0 ? grid.Config.midChunkSize : 4;
        return new GridCoord(mid.x * ms + ms / 2, mid.y * ms + ms / 2);
    }

    private static Vector2 MidToWorld(GridSystem grid, Vector2Int mid) => grid.CoordToWorld(CellAtMid(mid));

    private static Dictionary<Vector2Int, int> Ledger(TerritorySystem ts)
    {
        var f = typeof(TerritorySystem).GetField("_territory", BindingFlags.Instance | BindingFlags.NonPublic);
        return f != null ? (Dictionary<Vector2Int, int>)f.GetValue(ts) : null;
    }

    private static void InjectTerritory(TerritorySystem ts, Vector2Int mid, int kingdomId)
    {
        var d = Ledger(ts);
        if (d != null) d[mid] = kingdomId;
    }

    private static void RemoveTerritory(TerritorySystem ts, Vector2Int mid)
    {
        var d = Ledger(ts);
        if (d != null) d.Remove(mid);
    }

    /// <summary>反射调 Clear()（Dictionary/HashSet/List 通用），清 LODSystem 热度/活跃带/中心。</summary>
    private static void ClearLodState(LODSystem lod)
    {
        ClearField(lod, "_midStates");
        ClearField(lod, "_activeBandSet");
        ClearField(lod, "_activeCenters");
        typeof(LODSystem).GetField("_focalMidChunk", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(lod, null);
    }

    private static void ClearField(object target, string name)
    {
        if (target == null) return;
        var f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        var v = f?.GetValue(target);
        v?.GetType().GetMethod("Clear")?.Invoke(v, null);
    }

    /// <summary>P3b：活跃带非空中心（关闭无中心兜底）但 band 未渲染（空）→ 领土不被活跃带覆盖，走战斗热点分支。</summary>
    private static void SetActiveCentersNonEmpty(LODSystem lod, Vector2Int mid)
    {
        var f = typeof(LODSystem).GetField("_activeCenters", BindingFlags.Instance | BindingFlags.NonPublic);
        var list = f?.GetValue(lod) as List<Vector2Int>;
        if (list != null) list.Add(mid);
    }

    /// <summary>清 SimModeManager 连续未覆盖日数（防跨冒烟残留）。</summary>
    private static void ClearUncoveredDays(SimModeManager sm)
    {
        ClearField(sm, "_uncoveredDays");
    }

    /// <summary>P4：构造带 NPCBrain 的单位，反射调 IsSimDormant 验证军事不冻结/工人冻结/玩家不冻结。</summary>
    private static bool ProbeFreeze(KingdomRegistry reg)
    {
        var k = reg.Get(K);   // 合成王国 88（registry 已注入）
        var go = new GameObject("s13_freeze");
        go.AddComponent<SpriteRenderer>();        // UnitController.Awake 依赖（无则 NRE）
        go.AddComponent<Rigidbody2D>();           // 同上
        var uc = go.AddComponent<UnitController>();
        uc.kingdomId = K;
        var brain = go.AddComponent<NPCBrain>();
        var ctrlF = typeof(NPCBrain).GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic);
        ctrlF.SetValue(brain, uc);
        var m = typeof(NPCBrain).GetMethod("IsSimDormant", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            uc.SetOccupation(Occupation.Worker);
            k.simMode = SimMode.Abstract;
            bool workerFrozen = (bool)m.Invoke(brain, null);          // Abstract 工人 → 冻结 true

            uc.SetOccupation(Occupation.Warrior);
            bool warriorNotFrozen = !(bool)m.Invoke(brain, null);     // Abstract 战士 → 不冻结 false（D281）

            uc.SetOccupation(Occupation.Porter);
            bool porterFrozen = (bool)m.Invoke(brain, null);          // Abstract 搬运工 → 冻结 true

            uc.SetOccupation(Occupation.Worker);
            k.simMode = SimMode.Fine;
            bool workerFineNotFrozen = !(bool)m.Invoke(brain, null);  // Fine 工人 → 不冻结 false

            uc.kingdomId = 0;
            k.simMode = SimMode.Abstract;
            bool playerNotFrozen = !(bool)m.Invoke(brain, null);      // 玩家(0) → 不冻结 false

            return workerFrozen && warriorNotFrozen && porterFrozen && workerFineNotFrozen && playerNotFrozen;
        }
        finally
        {
            Object.Destroy(go);
        }
    }

    /// <summary>P5：ProduceMachine AI 分支（D454）——充足国库→成功+带 kingdomId / 上限 / 国库不足拒。</summary>
    private static bool ProbeProduceMachine()
    {
        if (!UnitDataManager.Instance.IsInitialized) UnitDataManager.Instance.LoadAll();
        var reg = KingdomRegistry.Instance;
        var regF = typeof(KingdomRegistry).GetField("_kingdoms", BindingFlags.Instance | BindingFlags.NonPublic);
        var orig = (List<KingdomState>)regF.GetValue(reg);
        var k2 = new KingdomState { id = K2, resources = new ResourcePack { gold = 200, stone = 200, wood = 200, food = 200, metal = 0 } };
        var temp = new List<KingdomState>(orig) { k2 };
        regF.SetValue(reg, temp);
        var siege = SiegeProductionSystem.Instance;
        try
        {
            var pos = new Vector2(500, 500);
            bool ok1 = siege.ProduceMachine(Occupation.SiegeMachine, pos, K2);                    // 国库充足 → 成功
            bool spawned1 = HasMachineOfKingdom(K2, Occupation.SiegeMachine);
            bool ok2 = siege.ProduceMachine(Occupation.Ballista, pos + new Vector2(3, 0), K2);    // 第2台 → 成功
            int count2 = CountMachineOfKingdom(K2);
            bool ok3 = siege.ProduceMachine(Occupation.SiegeMachine, pos + new Vector2(6, 0), K2); // 第3台 → 超上限拒
            k2.resources = default;                                                                 // 花光国库
            bool ok4 = !siege.ProduceMachine(Occupation.Ballista, pos + new Vector2(9, 0), K2);     // 国库不足 → 拒
            return ok1 && spawned1 && ok2 && count2 >= MACHINE_LIMIT && !ok3 && ok4;
        }
        finally
        {
            // 清理：生成的机器单位回池（防池残留 + 场景污染）
            var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
            if (units != null)
            {
                var list = new List<UnitController>(units);
                for (int i = 0; i < list.Count; i++)
                {
                    var u = list[i];
                    if (u == null || u.kingdomId != K2) continue;
                    if (u.EffectiveOccupation != Occupation.SiegeMachine && u.EffectiveOccupation != Occupation.Ballista) continue;
                    UnitFactory.Instance.ReturnUnitToPool(u);
                }
            }
            regF.SetValue(reg, orig);
        }
    }

    private static bool HasMachineOfKingdom(int kingdomId, Occupation occ)
    {
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units == null) return false;
        foreach (var u in units)
            if (u != null && u.kingdomId == kingdomId && u.EffectiveOccupation == occ) return true;
        return false;
    }

    private static int CountMachineOfKingdom(int kingdomId)
    {
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units == null) return 0;
        int c = 0;
        foreach (var u in units)
        {
            if (u == null || u.kingdomId != kingdomId) continue;
            if (u.EffectiveOccupation == Occupation.SiegeMachine || u.EffectiveOccupation == Occupation.Ballista) c++;
        }
        return c;
    }

    private class RunHost : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}
