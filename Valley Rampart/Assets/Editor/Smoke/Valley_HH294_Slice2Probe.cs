#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HH.294 片2 验证探针（Editor-only · 正门 `TestHarnessApi.EnterTestRun` · 零生产代码改动）。
///
/// 判据（任务书 §四 片2）：
///   ① 生成一张图**正常且能走通**（要素计数 ＋ GridSystem 层 BFS 连通）
///   ② `allowedTerrain`（现读地表物）语义**仍成立**：mine.asset（allowedTerrain=[Mine]）——
///      **非矿山格 ⇒ 建不成（reason=Terrain）**；**矿山 2×2 簇 ⇒ 建成（ok=true）**
///   ③ 派生一致抽样：`GetWalkFlags` 与地表物独立推导逐格一致
/// 用法（MCP）：Play 内 `Valley_HH294_Slice2Probe.Run();`；证据 `Logs/hh294_slice2_probe.log`。自收尾。
/// </summary>
public static class Valley_HH294_Slice2Probe
{
    const int Seed = 21107;

    static readonly StringBuilder _log = new StringBuilder();
    static string _logPath;
    static int _pass, _fail;

    public static void Run()
    {
        if (!EditorApplication.isPlaying) { UnityEngine.Debug.LogError("[HH294S2] 须先 GameScene 进 Play（正门 EnterTestRun）。"); return; }
        _log.Clear(); _pass = 0; _fail = 0;
        var host = new GameObject("HH294_Slice2ProbeHost").AddComponent<ProbeHost>();
        host.Host(Coroutine());
    }

    class ProbeHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    static IEnumerator Coroutine()
    {
        var cfg = new NewGameConfig
        {
            mapSeed = Seed, worldSeed = Seed, difficulty = 2,
            worldSize = WorldSize.Large, selectedSlotId = "smoke_hh294s2", kingdomName = "河谷王国"
        };
        Line("片2 验证启动（正门 EnterTestRun · seed=" + Seed + " · WorldSize=Large(384²)）", true);
        yield return TestHarnessApi.EnterTestRun(cfg, 1f);
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null) { yield return null; if (Time.realtimeSinceStartup - t0 > 120f) break; }
        var map = WorldManager.Instance.ActiveMap;
        var grid = GridSystem.Instance;
        if (map == null || grid == null) { Line("世界/网格未就绪", false); Finish(); yield break; }
        yield return new WaitForSeconds(0.6f);

        // ---------- ① 图正常 ----------
        int nPlain = 0, nTree = 0, nMine = 0, nMountain = 0, nWater = 0, nRes = 0;
        for (int i = 0; i < map.features.Length; i++)
        {
            var f = map.features[i];
            if (f == FeatureType.Plain) nPlain++;
            else if (f == FeatureType.Tree) nTree++;
            else if (f == FeatureType.Mine) nMine++;
            else if (f == FeatureType.Mountain || f == FeatureType.SnowMountain) nMountain++;
            else if (f == FeatureType.River || f == FeatureType.Ocean) nWater++;
            else nRes++;
        }
        Line("①a 地图要素：" + map.width + "×" + map.height + " 总格=" + map.features.Length
             + " | Plain=" + nPlain + " Tree=" + nTree + " Mine=" + nMine + " 山=" + nMountain + " 水=" + nWater + " 一次性=" + nRes
             + " | climateZones=" + (map.climateZones != null ? map.climateZones.Length : -1),
             map.features.Length > 0 && map.climateZones != null && map.climateZones.Length == map.width * map.height
             && nMine > 0 && nPlain > 0);

        // ---------- ③ 派生一致抽样 ----------
        int sampled = 0, mismatch = 0;
        var rng = new System.Random(12345);
        for (int k = 0; k < 400; k++)
        {
            int x = rng.Next(map.width), y = rng.Next(map.height);
            var c = new GridCoord(x, y);
            var expect = ExpectWalk(map.features[y * map.width + x]);
            if (grid.GetWalkFlags(c) != expect) mismatch++;
            sampled++;
        }
        Line("③ 派生一致抽样：抽样=" + sampled + " 与地表物独立推导不一致=" + mismatch + "（0＝一致）", mismatch == 0);

        // ---------- ①b 能走通（GridSystem 层 BFS：spawn0 → 各 spawn） ----------
        bool allReach = true; string reachStr = "";
        if (map.kingdomSpawns != null && map.kingdomSpawns.Count > 1)
        {
            for (int i = 1; i < map.kingdomSpawns.Count; i++)
            {
                bool r = Reachable(grid, map.kingdomSpawns[0], map.kingdomSpawns[i]);
                allReach &= r;
                reachStr += "[" + i + "]=" + r + " ";
            }
        }
        Line("①b 走通（GridSystem.IsWalkable BFS · spawn0 → 其余 " + (map.kingdomSpawns != null ? map.kingdomSpawns.Count - 1 : 0) + " 个出生点）："
             + (reachStr.Length > 0 ? reachStr : "无多出生点") + " ⇒ " + (allReach ? "全连通" : "有不可达"),
             (map.kingdomSpawns == null || map.kingdomSpawns.Count <= 1) ? true : allReach);

        // ---------- ② allowedTerrain 语义（矿山红线） ----------
        var mineDef = Resources.Load<BuildingDef>("Buildings/mine");
        Line("② 前置：mine.asset 载入=" + (mineDef != null) + " allowedTerrain=" + Desc(mineDef) + " footprint=" + (mineDef != null ? mineDef.footprint.ToString() : "-")
             + " cost(金/石/木/粮)=" + (mineDef != null ? (mineDef.cost.gold + "/" + mineDef.cost.stone + "/" + mineDef.cost.wood + "/" + mineDef.cost.food) : "-"),
             mineDef != null && mineDef.allowedTerrain != null && mineDef.allowedTerrain.Length == 1 && mineDef.allowedTerrain[0] == FeatureType.Mine);

        // 找 **2×2 全 Mine 簇**（未被占/无障碍）
        GridCoord? mineOrigin = null;
        for (int y = 0; y < map.height - 1 && mineOrigin == null; y++)
            for (int x = 0; x < map.width - 1; x++)
                if (IsCluster(map, x, y) && IsFree(grid, x, y, 2, 2)) { mineOrigin = new GridCoord(x, y); break; }
        // 找 **2×2 全 Plain 平地**（可走、未被占）
        GridCoord? plainOrigin = null;
        for (int y = 0; y < map.height - 1 && plainOrigin == null; y++)
            for (int x = 0; x < map.width - 1; x++)
                if (IsPlain2x2(map, x, y) && IsFree(grid, x, y, 2, 2)) { plainOrigin = new GridCoord(x, y); break; }

        Line("②a 采样点：矿山 2×2 簇=" + (mineOrigin.HasValue ? mineOrigin.Value.ToString() : "未找到")
             + " | 平地 2×2=" + (plainOrigin.HasValue ? plainOrigin.Value.ToString() : "未找到"),
             mineOrigin.HasValue && plainOrigin.HasValue);

        if (mineOrigin.HasValue)
        {
            var sub = grid.CellToSub(mineOrigin.Value, 0, 0);
            var r1 = PlacementValidator.ValidatePlacement(mineDef, sub, GateOrientation.Horizontal, 0);
            Line("②b 在**矿山**上放 mine：ok=" + r1.ok + " reason=" + r1.reason + "（期望 ok=True）", r1.ok);
        }
        if (plainOrigin.HasValue)
        {
            var sub2 = grid.CellToSub(plainOrigin.Value, 0, 0);
            var r2 = PlacementValidator.ValidatePlacement(mineDef, sub2, GateOrientation.Horizontal, 0);
            Line("②c 在**非矿山（平地）**上放 mine：ok=" + r2.ok + " reason=" + r2.reason + "（期望 ok=False · reason=Terrain）",
                 !r2.ok && r2.reason == PlacementFailReason.Terrain);
        }

        _log.AppendLine(""); Flush();
        Line("==== 汇总：PASS=" + _pass + " FAIL=" + _fail + " ====", _fail == 0);
        Finish();
    }

    static WalkFlags ExpectWalk(FeatureType f)
    {
        switch (f)
        {
            case FeatureType.Plain: case FeatureType.Tree: case FeatureType.Mine:
            case FeatureType.OreVein: case FeatureType.StonePile: case FeatureType.WoodPile:
                return WalkFlags.TerrainWalkable;
            case FeatureType.River: case FeatureType.Ocean:
                return WalkFlags.Water;
            default: return WalkFlags.None;
        }
    }

    static bool IsCluster(MapData m, int x, int y)
    {
        for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
            if (m.features[(y + dy) * m.width + (x + dx)] != FeatureType.Mine) return false;
        return true;
    }
    static bool IsPlain2x2(MapData m, int x, int y)
    {
        for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
            if (m.features[(y + dy) * m.width + (x + dx)] != FeatureType.Plain) return false;
        return true;
    }
    static bool IsFree(GridSystem g, int x, int y, int w, int h)
    {
        for (int dy = 0; dy < h; dy++) for (int dx = 0; dx < w; dx++)
        {
            var c = new GridCoord(x + dx, y + dy);
            if (g.GetOccupant(c) != null || g.IsObstacle(c)) return false;
        }
        return true;
    }

    /// <summary>GridSystem 可走层 BFS（只看 IsWalkable，四邻）。</summary>
    static bool Reachable(GridSystem g, Vector2Int a, Vector2Int b)
    {
        int w = g.MapWidth, h = g.MapHeight;
        if (a.x < 0 || a.y < 0 || b.x < 0 || b.y < 0 || a.x >= w || a.y >= h || b.x >= w || b.y >= h) return false;
        var vis = new bool[w * h];
        var q = new Queue<int>();
        int s = a.y * w + a.x, t = b.y * w + b.x;
        vis[s] = true; q.Enqueue(s);
        while (q.Count > 0)
        {
            int cur = q.Dequeue();
            if (cur == t) return true;
            int cx = cur % w, cy = cur / w;
            Try(g, vis, q, cx + 1, cy); Try(g, vis, q, cx - 1, cy); Try(g, vis, q, cx, cy + 1); Try(g, vis, q, cx, cy - 1);
        }
        return false;
    }
    static void Try(GridSystem g, bool[] vis, Queue<int> q, int x, int y)
    {
        if (x < 0 || y < 0 || x >= g.MapWidth || y >= g.MapHeight) return;
        int i = y * g.MapWidth + x;
        if (vis[i]) return;
        vis[i] = true;
        if (g.IsWalkable(new GridCoord(x, y))) q.Enqueue(i);
    }

    static string Desc(BuildingDef d)
    {
        if (d == null || d.allowedTerrain == null) return "null";
        var sb = new StringBuilder("[");
        for (int i = 0; i < d.allowedTerrain.Length; i++) sb.Append(d.allowedTerrain[i] + (i < d.allowedTerrain.Length - 1 ? "," : ""));
        return sb.Append("]").ToString();
    }

    static void Line(string s, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + s);
        if (ok) _pass++; else _fail++;
        UnityEngine.Debug.Log("[HH294S2] " + (ok ? "✓ " : "✗ ") + s);
        Flush();
    }

    static void Finish()
    {
        _log.AppendLine("==== 收尾（ExitTestRun + QuitSmoke）PASS=" + _pass + " FAIL=" + _fail + " ====");
        Flush();
        try { TestHarnessApi.ExitTestRun(); } catch { }
        try { SmokeApi.QuitSmoke(); } catch { }
    }

    static void Flush()
    {
        try
        {
            if (_logPath == null)
                _logPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh294_slice2_probe.log"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_logPath));
            System.IO.File.WriteAllText(_logPath, _log.ToString());
        }
        catch { }
    }
}
#endif