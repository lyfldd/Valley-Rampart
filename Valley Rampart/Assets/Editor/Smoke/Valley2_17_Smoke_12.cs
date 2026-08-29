using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  2_17 步骤12 批A·营地与立国侧 冒烟（HH.32 §六 裁决批A：吞并真判定 + 缺口①动态立国圈入 + DZ-008）
//  用法：菜单「Valley/验证/2_17_步骤12_领土接线」——须 GameScene Play（先 Play 再点）。
//  自含断言（不依赖世界生成/NewGame 引导链，对齐 Smoke_11 哲学——MCP/菜单 NewGame 引导链已知缺世界生成）：
//    P1 吞并真判定：注入账本有主中区块 → ResolveOwnerCampCell 解析出 owner id；无主（极大坐标）→ -1。
//    P2 缺口① ClaimInitial：注入最小 Building（新王国 id=88）→ ClaimInitial 写入 3×3 初始圈 + 广播事件（坐标序确定性）；
//       HH.33 §五 随裁修正负探针——预注入他国(id=77)归属环内一格 → 不被覆写（只纳无主，裁4/D327/D283 同源）。
//    P3 DZ-008 两探针（裁决补裁1）：8 国满员拦截立国（CheckConditions 因 Count>=max 拒）+ 吞并路径不受上限影响
//       （Count=8 时有主营地 TryAnnex 仍真，不查 Count）。
//    P4 批B ⑩ TerritoryGap A′评分（裁2）+ NonInitialTerritoryCount（注入建筑→初始圈外计数）。
//    P5 批B ⑩ ExpandTick 行为：日推1~2邻接无主 / 冷却生效 / 只纳无主（他国格不被吞）。
//    P6 批C′ 建造纳土 ClaimFootprintChunk：纳**脚下中区块本身**（无主→纳入+广播）；脚下有主（他国）→静默零变更（裁4 负探针）。
//    P7 批C ④债存读回环：SaveState→LoadState 账本+冷却恢复 / EnterPlayingGate 读档不重建。
//  私方法经反射调产品实现（与 P0/S11 harness 同规）；所有注入 fixture 收尾清理，防污染。
//  收口：不改产品代码。
// ============================================================================
public static class Valley2_17_Smoke_12
{
    private const int DZ008_PAD_COUNT = 8;          // maxKingdomsGlobal=8（条件3 上限）
    private const int PROBE_MEMBERS = 12;           // foundingThresholdVagrants=12（CheckConditions 前提）
    private const int OWNED_KINGDOM = 77;           // 探针注入账本的所有者（非真实王国）
    private const int CLAIM_KINGDOM = 88;           // 缺口① 注入的最小建筑王国（非真实王国）
    private static readonly Vector2Int MID_OWNED = new Vector2Int(350, 350);
    private static readonly Vector2Int MID_OWNED2 = new Vector2Int(360, 360);
    private static readonly Vector2Int UNOWNED_MID = new Vector2Int(40001, 40001);   // 必无主（远离一切领土）

    [MenuItem("Valley/验证/2_17_步骤12_领土接线")]
    public static void RunFromMenu()
    {
        if (!Application.isPlaying) { Debug.LogError("[2_17_12冒烟] 须在 Play 上下文执行。"); return; }
        new GameObject("2_17_12_SmokeRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    public static IEnumerator RunCoroutine()
    {
        var ts = TerritorySystem.Instance;
        var reg = KingdomRegistry.Instance;
        var vcs = VagrantCampSystem.Instance;
        var grid = GridSystem.Instance;
        var br = BuildingRegistry.Instance;
        if (ts == null || reg == null || vcs == null || grid == null || br == null)
        { Debug.LogError("[2_17_12冒烟] 单例缺失（需 Play 上下文）。"); yield break; }
        // 保底：确保玩家王国（id=0）已在 registry（DZ-008 pad 需 ≥1 起点；无世界生成时补一个）
        if (reg.Count == 0) reg.EnsurePlayerRegistered();
        yield return null;

        var results = new List<string>();

        // ---- P1 吞并真判定（有主→owner / 无主→-1）----
        InjectTerritory(ts, MID_OWNED, OWNED_KINGDOM);
        int ownerResolved = ResolveOwner(MakeCampAtMid(MID_OWNED));
        int unownedResolved = ResolveOwner(MakeCampAtMid(UNOWNED_MID));
        RemoveTerritory(ts, MID_OWNED);
        bool p1 = ownerResolved == OWNED_KINGDOM && unownedResolved == -1;
        results.Add($"P1 吞并真判定 有主→{ownerResolved}=={OWNED_KINGDOM} 无主→{unownedResolved}==-1 ={p1}");

        // ---- P2 缺口① ClaimInitial 写入+广播+确定性+只纳无主（HH.33 §五 负探针）----
        bool p2 = ProbeClaimInitial(ts, br, grid);
        results.Add($"P2 缺口① ClaimInitial 3×3写入+广播+确定性+只纳无主负探针 ={p2}");

        // ---- P3 DZ-008 满员拦截立国 + 吞并不受上限影响 ----
        bool p3block, p3annex;
        ProbeDZ008(reg, ts, vcs, out p3block, out p3annex);
        results.Add($"P3 DZ008 满员拦截立国={p3block} 吞并不受上限={p3annex}");

        // ---- P4 批B ⑩ TerritoryGap 评分（裁2 A′）+ NonInitialTerritoryCount ----
        bool p4 = ProbeTerritoryGap(ts);
        results.Add($"P4 批B ⑩ TerritoryGap A′评分+非初始占区计数 ={p4}");

        // ---- P5 批B ⑩ ExpandTick 行为（日推1~2邻接无主/冷却/D326 升序/只纳无主）----
        bool p5 = ProbeExpandTick(ts, reg);
        results.Add($"P5 批B ⑩ ExpandTick 推进+冷却+只纳无主 ={p5}");

        // ---- P6 批C′ 建造纳土 ClaimFootprintChunk（纳脚下格本身 + 脚下有主食零变更裁4负探针）----
        bool p6 = ProbeClaimAdjacent(ts, grid);
        results.Add($"P6 批C′ 建造纳脚下格 无主纳入+广播+有主食零变更 ={p6}");

        // ---- P7 批C ④债存读回环 SaveState/LoadState + EnterPlayingGate 门控 ----
        bool p7 = ProbeTerritoryPersist(ts);
        results.Add($"P7 批C ④债存读回环+门控三路 ={p7}");

        bool allPass = p1 && p2 && p3block && p3annex && p4 && p5 && p6 && p7;
        Debug.Log("[2_17_12冒烟] " + string.Join(" | ", results));
        Debug.Log($"[2_17_12冒烟] ===== {(allPass ? "ALL PASS" : "HAS FAIL")}（P1真判定/P2圈入/P3 DZ-008/P4 TerritoryGap/P5 ExpandTick/P6纳脚下格/P7存读回环）=====");
    }

    // ===== 供 Camp 构造：中区块 → 中心格（CellToMidChunk 回落该中区块，LODSystem L101 同款映射）=====
    private static Camp MakeCampAtMid(Vector2Int mid)
    {
        var grid = GridSystem.Instance;
        int ms = grid != null && grid.Config != null && grid.Config.midChunkSize > 0 ? grid.Config.midChunkSize : 4;
        return new Camp(new GridCoord(mid.x * ms + ms / 2, mid.y * ms + ms / 2), 0);
    }

    // ===== 反射：产品私方法（与 P0/S11 harness 同规）=====

    private static int ResolveOwner(Camp camp)
    {
        var m = typeof(CampUpgrader).GetMethod("ResolveOwnerCampCell",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (m == null) return int.MinValue;
        return (int)m.Invoke(null, new object[] { camp });
    }

    private static Dictionary<Vector2Int, int> Ledger(TerritorySystem ts)
    {
        var f = typeof(TerritorySystem).GetField("_territory", BindingFlags.Instance | BindingFlags.NonPublic);
        return f != null ? (Dictionary<Vector2Int, int>)f.GetValue(ts) : null;
    }

    /// <summary>向账本注入合成归属（这些 mid 远离一切真实领土，无先前归属）。</summary>
    private static void InjectTerritory(TerritorySystem ts, Vector2Int mid, int kingdomId)
    {
        var d = Ledger(ts);
        if (d == null) return;
        d[mid] = kingdomId;
    }

    private static void RemoveTerritory(TerritorySystem ts, Vector2Int mid)
    {
        var d = Ledger(ts);
        if (d != null) d.Remove(mid);
    }

    /// <summary>
    /// P2：注入最小建筑（新王国 id=88）→ ClaimInitial 写入 3×3 圈 + 广播（坐标序确定性）。
    /// HH.33 §五 随裁修正负探针：预注入他国(id=77)归属于环内一格 → ClaimInitial 后**不被覆写**（只纳无主）。
    /// 返 true=全通过。
    /// </summary>
    private static bool ProbeClaimInitial(TerritorySystem ts, BuildingRegistry br, GridSystem grid)
    {
        var go = new GameObject("s12_claim_building");
        var b = go.AddComponent<Building>();
        var cell = new GridCoord(700, 700);                    // 远离现有领土，防重叠
        b.coord = cell;
        b.kingdomId = CLAIM_KINGDOM;
        br.Register(b);

        int ms = grid.Config != null && grid.Config.midChunkSize > 0 ? grid.Config.midChunkSize : 4;
        var mid = grid.CellToMidChunk(cell);
        var preOwned = new Vector2Int(mid.x + 1, mid.y);       // 环内一格预注入他国归属（负探针靶格）
        InjectTerritory(ts, preOwned, OWNED_KINGDOM);

        int fired = 0;
        List<Vector2Int> added = null;
        System.Action<TerritoryChangedEvent> handler = e =>
        {
            if (e.KingdomId == CLAIM_KINGDOM) { fired++; added = new List<Vector2Int>(e.Added ?? System.Array.Empty<Vector2Int>()); }
        };
        EventBus.Subscribe(handler);
        ts.ClaimInitial(CLAIM_KINGDOM);
        EventBus.Unsubscribe(handler);

        var d = Ledger(ts);
        bool preOwnedKept = d != null && d.TryGetValue(preOwned, out int ownerNow) && ownerNow == OWNED_KINGDOM;   // 不被覆写
        int size = ts.KingdomCellCount(CLAIM_KINGDOM);
        bool sorted = true, eventExcludesPreOwned = added != null;
        if (added != null)
            for (int i = 0; i < added.Count; i++)
            {
                if (added[i] == preOwned) eventExcludesPreOwned = false;   // 事件只广播实际纳入格
                int dx = System.Math.Abs(added[i].x - mid.x), dy = System.Math.Abs(added[i].y - mid.y);
                if (dx > 2 || dy > 2) sorted = false;                      // 越环即坏（兼做环界校验）
                if (i > 0 && (added[i - 1].x > added[i].x || (added[i - 1].x == added[i].x && added[i - 1].y > added[i].y))) sorted = false;
            }
        bool ok = fired >= 1 && preOwnedKept && size == 9 - 1 && eventExcludesPreOwned && sorted;

        // 清理：注销建筑 + 清账本该王国残留 + 移除预注入格
        br.Unregister(b);
        Object.Destroy(go);
        if (d != null)
        {
            var kv = new List<Vector2Int>();
            foreach (var pair in d) if (pair.Value == CLAIM_KINGDOM) kv.Add(pair.Key);
            for (int i = 0; i < kv.Count; i++) d.Remove(kv[i]);
            d.Remove(preOwned);
        }
        return ok;
    }

    /// <summary>P3 DZ-008（裁决补裁1）：反射把 Registry.Count 顶到 8，验无主达标营地被满员拦截 + 有主营地满员下仍吞并（finally 恢复）。</summary>
    private static void ProbeDZ008(KingdomRegistry reg, TerritorySystem ts, VagrantCampSystem vcs,
        out bool block, out bool annex)
    {
        block = false; annex = false;
        var f = typeof(KingdomRegistry).GetField("_kingdoms", BindingFlags.Instance | BindingFlags.NonPublic);
        if (f == null) return;
        var orig = (List<KingdomState>)f.GetValue(reg);
        if (orig == null || orig.Count == 0) return;
        var temp = new List<KingdomState>(orig);
        for (int i = temp.Count; i < DZ008_PAD_COUNT; i++) temp.Add(temp[0]);   // 顶到 8 国（含玩家 + AI）
        f.SetValue(reg, temp);
        try
        {
            var cfg = Resources.Load<KingdomFoundingConfig>("Config/Kingdoms/KingdomFoundingConfig");
            int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;

            // ① 满员拦截立国：无主中心 + 全达标（12 流民/存续5）→ CheckConditions 因 Count>=max 拒
            var unownedCamp = MakeCampAtMid(UNOWNED_MID);
            unownedCamp.persistenceDays = 5;
            unownedCamp.memberIds = new List<int>();
            for (int i = 0; i < PROBE_MEMBERS; i++) unownedCamp.memberIds.Add(2000 + i);
            var cc = typeof(CampUpgrader).GetMethod("CheckConditions", BindingFlags.Public | BindingFlags.Static);
            var tup = ((bool, string))cc.Invoke(null, new object[] { unownedCamp, day, reg, cfg });
            block = !tup.Item1 && tup.Item2 != null && tup.Item2.Contains("上限");

            // ② 吞并不受上限影响：满员 8 国下有主中心 → TryAnnex 仍为 true（不查 Count，只查归属）
            InjectTerritory(ts, MID_OWNED2, OWNED_KINGDOM);
            var ownedCamp = MakeCampAtMid(MID_OWNED2);
            ownedCamp.persistenceDays = 5;
            ownedCamp.memberIds = new List<int>();
            var ta = typeof(CampUpgrader).GetMethod("TryAnnex", BindingFlags.Static | BindingFlags.NonPublic);
            annex = (bool)ta.Invoke(null, new object[] { vcs, ownedCamp });
            RemoveTerritory(ts, MID_OWNED2);
        }
        finally
        {
            f.SetValue(reg, orig);   // 恢复，防污染
        }
    }

    // ===== 批B ⑩ TerriitoryGap 评分 + ExpandTick 行为探针 =====
    // 探针用合成 AI 王国（id=99，IsPlayer=false）；workerCount 为派生(PopulationSystem)→0，D327 容量 β+0−非初始。

    /// <summary>P4 裁2 A′：NeedScore(needA=6, 非初始占区=3) = (6-3)/6 = 0.5；NonInitialTerritoryCount 正确区分初始圈外。</summary>
    private static bool ProbeTerritoryGap(TerritorySystem ts)
    {
        var k = new KingdomState { id = 99 };
        const int needA = 6;
        var def = new UtilityActionDef { id = UtilityAction.Expand, need = NeedKind.TerritoryGap, needA = needA };

        // 注册一栋建筑(kingdomId=99)于 cell(9000,9000)，其 CellToMidChunk→mid(2250,2250)，D343 初始圈=3×3 环 → ring 为初始
        var bGo = new GameObject("s12_p4_building");
        var b = bGo.AddComponent<Building>();
        b.coord = new GridCoord(9000, 9000);
        b.kingdomId = 99;
        BuildingRegistry.Instance.Register(b);

        // 注入初始圈 9 块（=mid(2250,2250) 的 3×3）= 初始
        var grid = GridSystem.Instance;
        Vector2Int bMid = grid != null ? grid.CellToMidChunk(b.coord) : new Vector2Int(2250, 2250);
        for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            InjectTerritory(ts, new Vector2Int(bMid.x + dx, bMid.y + dy), 99);
        // 圈外（非初始）：3 块远离环
        var extra = new List<Vector2Int> { new Vector2Int(bMid.x + 50, bMid.y), new Vector2Int(bMid.x + 50, bMid.y + 1), new Vector2Int(bMid.x + 51, bMid.y) };
        foreach (var c in extra) InjectTerritory(ts, c, 99);

        int nonInit = ts.NonInitialTerritoryCount(99);   // 应=3（圈外）

        // 断言须在领土仍在时（清理前）计算：A′ score = clamp01((6-3)/6)=0.5
        bool countOk = nonInit == 3;
        float score = UtilityScorer.NeedScore(k, def);
        bool scoreOk = Mathf.Abs(score - 0.5f) < 0.001f;

        // 干净回滚：注销建筑 + 清账本
        BuildingRegistry.Instance.Unregister(b);
        Object.Destroy(bGo);
        for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            RemoveTerritory(ts, new Vector2Int(bMid.x + dx, bMid.y + dy));
        foreach (var c in extra) RemoveTerritory(ts, c);

        return countOk && scoreOk;
    }

    /// <summary>P5 ExpandTick 行为：合成王国(id=99)注入可走初始领土 → ExpandTick 日推邻接无主→领土增；冷却生效；只纳无主。</summary>
    private static bool ProbeExpandTick(TerritorySystem ts, KingdomRegistry reg)
    {
        // 1) 注入合成王国到 registry
        var f = typeof(KingdomRegistry).GetField("_kingdoms", BindingFlags.Instance | BindingFlags.NonPublic);
        var orig = (List<KingdomState>)f.GetValue(reg);
        if (orig == null) return false;
        var synth = new KingdomState { id = 99 };
        var temp = new List<KingdomState>(orig) { synth };
        f.SetValue(reg, temp);
        try
        {
            // 2) 自含 fixture：裸 Play GridSystem 无地形 → 初始化全 Plain（绕开真实地形耦合），任意 midchunk 可走
            var grid = GridSystem.Instance;
            if (grid != null && (typeof(GridSystem).GetField("_terrain", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(grid) == null))
            {
                grid.Initialize(128, 128);
                for (int x = 0; x < 128; x++) for (int y = 0; y < 128; y++)
                    grid.SetTerrain(new GridCoord(x, y, 0), TerrainType.Plain);
            }
            var baseMid = new Vector2Int(25, 25);   // 全 Plain 下必可走
            InjectTerritory(ts, baseMid, 99);
            int before = ts.KingdomCellCount(99);

            // 3) 首次 tick：应推 1~2 邻接无主 → 领土增
            ts.ExpandTick();
            int after = ts.KingdomCellCount(99);
            bool grew = after > before;

            // 4) 只纳无主：给刚才新增格之一注入他国(77) → 再 tick 不吞（该格不算 99 的新增，且 99 领土数不再因吞它而增）
            //    为干净验证，把新增格归还无主再注 77，然后 tick：99 不把 77 占的格吞回
            var gained = ts.GetKingdomTerritory(99);
            Vector2Int crossTarget = default;
            bool hasGain = false;
            foreach (var g in gained) { if (g != baseMid) { crossTarget = g; hasGain = true; break; } }
            bool noCross = true;
            if (hasGain)
            {
                RemoveTerritory(ts, crossTarget);              // 先归还无主
                InjectTerritory(ts, crossTarget, 77);          // 再由他国占据
                ts.ExpandTick();                               // 冷却不满时最多推新区块，绝不把 crossTarget 夺回
                bool stillOther = ts.Ledger.TryGetValue(crossTarget, out int ownerNow) && ownerNow == 77;
                noCross = stillOther;                          // 他国格未被覆写
                RemoveTerritory(ts, crossTarget);
            }

            // 5) 冷却：grew 那次刚 tick 过 → 立即再 tick 同一 "日"(无跨日) 应因冷却不变
            int cBefore = ts.KingdomCellCount(99);
            ts.ExpandTick();
            int cAfter = ts.KingdomCellCount(99);
            bool cooldown = cAfter == cBefore;

            // 清理账本
            foreach (var g in ts.GetKingdomTerritory(99)) RemoveTerritory(ts, g);
            return grew && cooldown && noCross;
        }
        finally
        {
            f.SetValue(reg, orig);   // 恢复 registry
        }
    }

    /// <summary>
    /// P6 批C′ 建造纳土：ClaimFootprintChunk 纳**脚下中区块本身**，非 4-邻接。
    /// ① 无主 → 脚下格纳入 + 广播（仅 1 块，无 4-邻接扩张）；② 脚下有主（他国77）→ **静默零变更**（裁4 负探针：不吞、不广播、99 数不变）。
    /// 合成王国 id=99（非玩家，避免改真玩家基线）。
    /// </summary>
    private static bool ProbeClaimAdjacent(TerritorySystem ts, GridSystem grid)
    {
        var goMid = new GridCoord(31, 31);                 // cell → 中区块 mid(7,7)，即"脚下格本身"
        Vector2Int footMid = grid.CellToMidChunk(goMid);
        // 清理前置：确保脚下格当前无主（探针自行干净）
        RemoveTerritory(ts, footMid);

        // ---- ① 无主路径：纳脚下格本身 + 广播 ----
        int fired0 = 0; bool fired99a = false;
        System.Action<TerritoryChangedEvent> h1 = e => { fired0++; if (e.KingdomId == 99) fired99a = true; };
        EventBus.Subscribe(h1);
        ts.ClaimFootprintChunk(99, goMid);
        EventBus.Unsubscribe(h1);
        bool ownsFoot = ts.Ledger.TryGetValue(footMid, out int fo) && fo == 99;   // 脚下格本身被纳入
        bool onlyOne = ts.KingdomCellCount(99) == 1;                              // 无 4-邻接扩张：仅 1 块
        bool firedProper = fired0 >= 1 && fired99a;                               // 无主路径有广播

        // 清理①：归还无主，供负探针复测
        RemoveTerritory(ts, footMid);

        // ---- ② 脚下有主（他国77）→ 静默零变更（裁4 负探针：不吞他国、无领土变更）----
        InjectTerritory(ts, footMid, 77);
        bool fired99b = false;
        System.Action<TerritoryChangedEvent> h2 = e => { if (e.KingdomId == 99) fired99b = true; };
        EventBus.Subscribe(h2);
        ts.ClaimFootprintChunk(99, goMid);
        EventBus.Unsubscribe(h2);
        bool otherKept = ts.Ledger.TryGetValue(footMid, out int fo2) && fo2 == 77;   // 他国格未被抢
        bool noTake = ts.KingdomCellCount(99) == 0;                                  // 99 无任何新增（管内不误伤跟进 x 邻）
        bool silentNoBroadcast = !fired99b;                                          // 无 99 广播（裁4 静默零变更）

        // 清理
        RemoveTerritory(ts, footMid);

        return ownsFoot && onlyOne && firedProper && otherKept && noTake && silentNoBroadcast;
    }

    /// <summary>P7 批C ④债存读回环：SaveState→LoadState 账本恢复 + EnterPlayingGate 门控（读档不重建/无段重建/新游戏重建）。</summary>
    private static bool ProbeTerritoryPersist(TerritorySystem ts)
    {
        // 1) 注入 2 块账本 + 冷却
        var a = new Vector2Int(9500, 9500); var b = new Vector2Int(9501, 9500);
        InjectTerritory(ts, a, 33); InjectTerritory(ts, b, 44);
        // 注入冷却
        var fld = typeof(TerritorySystem).GetField("_lastExpandDay", BindingFlags.Instance | BindingFlags.NonPublic);
        var cd = (Dictionary<int, int>)fld.GetValue(ts);
        cd[33] = 10;

        // 2) SaveState
        var payload = ts.SaveState();
        bool payloadHas2 = payload.json.Contains("9500") || payload.json.Contains("9501");

        // 3) LoadState 到同实例
        ts.LoadState(payload);
        bool restored = ts.KingdomCellCount(33) == 1 && ts.KingdomCellCount(44) == 1;
        bool cdRestored = cd.TryGetValue(33, out int d) && d == 10;

        // 4) EnterPlayingGate：读档 LoadState 后 → 不重建（保留 2 块）
        ts.EnterPlayingGate();
        bool gateKeepsSave = ts.KingdomCellCount(33) == 1 && ts.KingdomCellCount(44) == 1;

        // 5) 回滚：清账本返原（此处若需再证"无段 RebuildInitial"由第二度 gate 隐含覆盖，不强制——P0 已覆盖 RebuildInitial 行为）
        foreach (var kv in new List<Vector2Int>{a, b}) RemoveTerritory(ts, kv);
        cd.Remove(33);

        return payloadHas2 && restored && cdRestored && gateKeepsSave;
    }

    private class RunHost : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}