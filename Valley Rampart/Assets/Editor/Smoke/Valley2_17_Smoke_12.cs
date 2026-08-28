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
//    P2 缺口① ClaimInitial：注入最小 Building（新王国 id=88）→ ClaimInitial 写入 3×3 初始圈 + 广播事件（坐标序确定性）。
//    P3 DZ-008 两探针（裁决补裁1）：8 国满员拦截立国（CheckConditions 因 Count>=max 拒）+ 吞并路径不受上限影响
//       （Count=8 时有主营地 TryAnnex 仍真，不查 Count）。
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

        // ---- P2 缺口① ClaimInitial 写入+广播+确定性 ----
        bool p2 = ProbeClaimInitial(ts, br, grid);
        results.Add($"P2 缺口① ClaimInitial 3×3写入+广播+确定性 ={p2}");

        // ---- P3 DZ-008 满员拦截立国 + 吞并不受上限影响 ----
        bool p3block, p3annex;
        ProbeDZ008(reg, ts, vcs, out p3block, out p3annex);
        results.Add($"P3 DZ008 满员拦截立国={p3block} 吞并不受上限={p3annex}");

        bool allPass = p1 && p2 && p3block && p3annex;
        Debug.Log("[2_17_12冒烟] " + string.Join(" | ", results));
        Debug.Log($"[2_17_12冒烟] ===== {(allPass ? "ALL PASS" : "HAS FAIL")}（P1真判定 / P2缺圈入 / P3 DZ-008 探针）=====");
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

    /// <summary>P2：注入最小建筑（新王国 id=88）→ ClaimInitial 写入 3×3 圈 + 广播（坐标序确定性）。返 true=全通过。</summary>
    private static bool ProbeClaimInitial(TerritorySystem ts, BuildingRegistry br, GridSystem grid)
    {
        var go = new GameObject("s12_claim_building");
        var b = go.AddComponent<Building>();
        var cell = new GridCoord(700, 700);                    // 远离现有领土，防重叠
        b.coord = cell;
        b.kingdomId = CLAIM_KINGDOM;
        br.Register(b);

        int fired = 0;
        List<Vector2Int> added = null;
        System.Action<TerritoryChangedEvent> handler = e =>
        {
            if (e.KingdomId == CLAIM_KINGDOM) { fired++; added = new List<Vector2Int>(e.Added ?? System.Array.Empty<Vector2Int>()); }
        };
        EventBus.Subscribe(handler);
        ts.ClaimInitial(CLAIM_KINGDOM);
        EventBus.Unsubscribe(handler);

        int size = ts.KingdomCellCount(CLAIM_KINGDOM);
        var mid = grid.CellToMidChunk(cell);
        bool sorted = true;
        bool inRing = added != null;
        if (added != null)
            for (int i = 0; i < added.Count; i++)
            {
                int dx = System.Math.Abs(added[i].x - mid.x), dy = System.Math.Abs(added[i].y - mid.y);
                if (dx > 2 || dy > 2) inRing = false;                    // 快照不越界（中区块含 footprint≥1 者 dx,dy≤1；留 1 容差）
                if (i > 0 && (added[i - 1].x > added[i].x || (added[i - 1].x == added[i].x && added[i - 1].y > added[i].y))) sorted = false;
            }
        bool ok = fired >= 1 && size >= 9 && sorted && inRing;

        // 清理：注销建筑 + 清账本该王国残留
        br.Unregister(b);
        Object.Destroy(go);
        var d = Ledger(ts);
        if (d != null)
        {
            var kv = new List<Vector2Int>();
            foreach (var pair in d) if (pair.Value == CLAIM_KINGDOM) kv.Add(pair.Key);
            for (int i = 0; i < kv.Count; i++) d.Remove(kv[i]);
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

    private class RunHost : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}