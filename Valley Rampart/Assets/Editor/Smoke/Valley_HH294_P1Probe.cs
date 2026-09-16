#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// HH.294 补正 P1 探针：**运行期全图扫处置**（`GuardDeploymentSystem.FindNearestResourceNode`）前后读数。
///
/// 口径（验收报告 §④-3 ／ 补正提示词 P1）：
///   改造前＝嵌套 `for y&lt;height / x&lt;width` 全扫 `map.features`（大图 384² = 147,456 格），
///   触发＝**每次玩家右键派兵**（`SelectionController.cs:270` 判 nearResource ＋ `:279` `DeployGuard` 内再查 ⇒ 一次走两遍）。
///
/// 本探针三类读数（同一局、同一张图、同一落点）：
///   A 老实现副本（逐字复制改造前实现 · 仅作基线）——遍历格数 = `W×H`
///   B 生产 `FindNearestResourceNode`（改造后＝按索引查询）——遍历格数 = 索引条目数（反射读，缺则退化 W×H）
///   C **真实右键路径** `SelectionController.IssueRightClick(pos)`（选中「远离所有资源点的士兵」）——一次右键 = B×2
///   ＋ D **结果一致性**：A 与 B 在同一点集逐点比对坐标，见证「改造后取点结果不漂移」
/// 用法（MCP · 需先 GameScene 进 Play；本探针内部走正门 `TestHarnessApi.EnterTestRun` 自建局）：
///   `Valley_HH294_P1Probe.Run("before");` ／ `Valley_HH294_P1Probe.Run("after");`
/// 证据：`Logs/hh294_p1_probe_&lt;tag&gt;.log`
/// </summary>
public static class Valley_HH294_P1Probe
{
    const int Seed = 21107;
    const int Reps = 15;        // 单调用重复次数（取中位）
    const int PairPoints = 240; // 结果一致性点数

    static readonly StringBuilder _log = new StringBuilder();
    static string _logPath;
    static string _tag;

    public static void Run(string tag = "run")
    {
        if (!UnityEngine.Application.isPlaying)
        {
            UnityEngine.Debug.LogError("[HH294P1] 须先 GameScene 进 Play（本探针内部走正门 EnterTestRun，但需处于 Play 态）。");
            return;
        }
        _tag = tag;
        _log.Clear();
        _logPath = null;
        var host = new GameObject("HH294_P1ProbeHost").AddComponent<ProbeHost>();
        host.Host(Coroutine());
    }

    class ProbeHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    static IEnumerator Coroutine()
    {
        var cfg = new NewGameConfig
        {
            mapSeed = Seed, worldSeed = Seed, difficulty = 2,
            worldSize = WorldSize.Large, selectedSlotId = "smoke_hh294p1", kingdomName = "河谷王国"
        };
        Line("==============================================================================");
        Line("HH.294 补正 P1 · 运行期全图扫处置读数 · tag=" + _tag + " · " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Line("口径：大图 384²·seed " + Seed + "·正门 EnterTestRun｜改造前实现见 A 段副本");
        Line("==============================================================================");

        yield return TestHarnessApi.EnterTestRun(cfg, 1f);
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) break;
        }
        var map = WorldManager.Instance.ActiveMap;
        var grid = GridSystem.Instance;
        if (map == null || grid == null) { Line("世界/网格未就绪 ⇒ 中止"); Finish(false); yield break; }
        yield return new WaitForSeconds(0.6f);

        // ---------- ① 规模与索引 ----------
        int w = map.width, h = map.height, all = w * h;
        int guardCells = 0;
        for (int i = 0; i < map.features.Length; i++)
            if (IsGuardFeature(map.features[i])) guardCells++;
        int idxCells = ReadIndexedCells();   // 反射：改造前该读口不存在 ⇒ -1
        Line("");
        Line("【① 规模】地图 " + w + "×" + h + " = " + all + " 格（W×H＝改造前单次遍历格数）");
        Line("        features 中可守卫资源格（Tree/Mine/OreVein） = " + guardCells);
        Line("        索引条目数 IndexedResourceCells = " + (idxCells < 0 ? "（该读口不存在＝改造前）" : idxCells.ToString())
             + " ⇒ 改造后单次遍历上界 = " + (idxCells < 0 ? "W×H = " + all : idxCells.ToString()));

        // ---------- ② 选「远离所有资源点的士兵」 ----------
        // 落点优先级：全图距资源点最远的格（抽样求取）→ 在其旁 4 单位处布一个 Warrior（**测试载体**，
        // 仅为把右键路径推进到 :279 `DeployGuard` 分支＝「一次走两遍」；本局 D1 玩家尚无士兵）。
        Vector2 farCell = FarCellWorld(map, grid);
        UnitController farSoldier = null; float farSoldierD = -1f;
        UnitController farAny = null; float farAnyD = -1f;
        int playerAlive = 0, soldierAlive = 0;
        ScanUnits(map, grid, ref farSoldier, ref farSoldierD, ref farAny, ref farAnyD, ref playerAlive, ref soldierAlive);
        Line("");
        Line("【② 采样点】玩家阵营存活单位 = " + playerAlive + "（其中士兵 = " + soldierAlive + "）");

        bool spawned = false;
        if (farSoldier == null && UnitFactory.Instance != null)
        {
            try
            {
                var go = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Warrior, farCell + new Vector2(4f, 0f));
                spawned = go != null;
                Line("        本局无存活士兵 ⇒ 在远离点旁布 1 个 Warrior（测试载体·仅本探针）：" + (go != null ? go.name : "失败"));
            }
            catch (System.Exception ex) { Line("        ⚠ Warrior 布点异常：" + ex.Message); }
            yield return null;
            yield return new WaitForSeconds(0.3f);
            ScanUnits(map, grid, ref farSoldier, ref farSoldierD, ref farAny, ref farAnyD, ref playerAlive, ref soldierAlive);
            Line("        重扫：玩家阵营存活单位 = " + playerAlive + "（士兵 = " + soldierAlive + "）");
        }

        UnitController sel = farSoldier != null ? farSoldier : farAny;
        Vector2 pos = farSoldier != null ? (Vector2)farSoldier.transform.position
                    : (farAny != null ? (Vector2)farAny.transform.position : farCell);
        if (farSoldier != null)
            Line("        远离资源点的士兵 = " + farSoldier.name + " @ " + Fmt(pos) + " ｜ 到最近可守卫资源点距离 = " + farSoldierD.ToString("0.00"));
        else
            Line("        ⚠ 仍无士兵 ⇒ 取「远离资源点的玩家单位」= " + (farAny != null ? farAny.name + "（" + farAny.EffectiveOccupation + "）" : "无")
                 + " @ " + Fmt(pos) + " ｜ 距离 = " + (farAnyD < 0 ? "-" : farAnyD.ToString("0.00"))
                 + (farAny == null ? "（兜底：全图距资源点最远格，spawn=" + spawned + "）" : ""));
        Line("        ⇒ 右键落点 = " + Fmt(pos));

        // ---------- ③ A/B 单调用读数 ----------
        Line("");
        Line("【③ 单次调用（" + Reps + " 次取中位 ms ＋ 遍历格数）】");
        double msA = TimeIt(Reps, () => LegacyFullScan(map, grid, pos));
        Line("        A 老实现副本（全图嵌套扫）          中位 = " + msA.ToString("0.000") + " ms ｜ 遍历格数 = " + all + "（W×H）");
        double msB = TimeIt(Reps, () => GuardDeploymentSystem.FindNearestResourceNode(pos));
        int travB = idxCells < 0 ? all : idxCells;
        Line("        B 生产 FindNearestResourceNode     中位 = " + msB.ToString("0.000") + " ms ｜ 遍历格数 = " + travB
             + (idxCells < 0 ? "（＝改造前口径 W×H）" : "（＝索引条目数）"));

        // ---------- ④ C 真实右键路径 ----------
        var sc = SelectionController.Instance;
        Line("");
        Line("【④ 真实右键路径 `SelectionController.IssueRightClick(pos)`（一次右键）】");
        if (sc == null)
        {
            Line("        ⚠ SelectionController.Instance 缺失 ⇒ C 段跳过");
        }
        else if (sel == null)
        {
            Line("        ⚠ 无可用选中单位 ⇒ C 段跳过（空选早退路径不触发 :270/:279）");
        }
        else
        {
            bool isSoldier = IsSoldier(sel);
            int passes = isSoldier ? 2 : 1;
            int guardBefore = GuardDeploymentSystem.Count;
            int aliveIter = 0;
            double msC = TimeIt(Reps, () =>
            {
                sc.Selected.Clear();
                sc.Selected.Add(sel);
                aliveIter = sc.Selected.Count;
                sc.IssueRightClick(pos);
            });
            int guardAfter = GuardDeploymentSystem.Count;
            Line("        选中单位 = " + sel.name + " ｜ 士兵判定 = " + isSoldier
                 + "（真 ⇒ :270 nearResource ＋ :279 DeployGuard 各一遍；假 ⇒ 仅 :270）");
            Line("        每轮 Selected 数 = " + aliveIter + " ｜ 守卫区域数 前=" + guardBefore + " 后=" + guardAfter + "（同格幂等 ⇒ 只增 ≤1）");
            Line("        C 中位 = " + msC.ToString("0.000") + " ms ｜ 遍历格数 = " + (travB * passes) + "（B×" + passes + " 遍）");
        }

        // ---------- ⑤ 结果一致性（A vs B） ----------
        Line("");
        Line("【⑤ 结果一致性：老实现副本 A 与生产 B 逐点比对（随机 " + PairPoints + " 点 ＋ 资源点贴脸 80 点）】");
        var rng = new System.Random(20260916);
        int diff = 0; string firstDiff = "";
        int withVal = 0;
        for (int k = 0; k < PairPoints + 80; k++)
        {
            Vector2 p;
            if (k < 80)
                p = grid.CoordToWorld(FindGuardCell(map, k * 37));
            else
                p = new Vector2((float)(rng.NextDouble() * 2400 - 1200), (float)(rng.NextDouble() * 2400 - 1200));
            var a = LegacyFullScan(map, grid, p);
            var b = GuardDeploymentSystem.FindNearestResourceNode(p);
            if (a.HasValue) withVal++;
            string sa = a.HasValue ? a.Value.coord + "/" + a.Value.feature : "null";
            string sb = b.HasValue ? b.Value.coord + "/" + b.Value.feature : "null";
            if (sa != sb) { diff++; if (firstDiff.Length == 0) firstDiff = " 首处不一致 p=" + Fmt(p) + " A=" + sa + " B=" + sb; }
        }
        Line("        有值点 = " + withVal + " / " + (PairPoints + 80)
             + " ｜ 不一致点数 = " + diff + (diff == 0 ? "  ⇒ 取点结果与全扫**逐点一致**" : firstDiff));

        Line("");
        Line("==============================================================================");
        Finish(true);
    }

    // ================= 老实现副本（逐字复制改造前 FindNearestResourceNode，仅作基线）=================

    static GuardResourceNode? LegacyFullScan(MapData map, GridSystem grid, Vector2 pos)
    {
        GuardResourceNode? best = null;
        float bestSq = float.MaxValue;
        int width = map.width, height = map.height;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                FeatureType f = map.features[y * width + x];
                if (!IsGuardFeature(f)) continue;
                var c = new GridCoord(x, y);
                float sq = ((Vector2)grid.CoordToWorld(c) - pos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = new GuardResourceNode(c, f, Faction.PlayerCamp, FeatureDisplayName(f)); }
            }
        }
        return best;
    }

    // ================= 辅助 =================

    static bool IsGuardFeature(FeatureType f)
        => f == FeatureType.Tree || f == FeatureType.Mine || f == FeatureType.OreVein;

    static string FeatureDisplayName(FeatureType f)
    {
        switch (f)
        {
            case FeatureType.Tree: return "树木区";
            case FeatureType.Mine: return "矿洞区";
            case FeatureType.OreVein: return "矿脉区";
            default: return f.ToString();
        }
    }

    static bool IsSoldier(UnitController u)
    {
        if (u == null) return false;
        switch (u.EffectiveOccupation)
        {
            case Occupation.Warrior: case Occupation.Archer: case Occupation.Mage:
            case Occupation.Healer: case Occupation.Cavalry: case Occupation.General: return true;
            default: return false;
        }
    }

    /// <summary>扫描场景内玩家阵营存活单位，取「距最近可守卫资源点最远」的士兵/单位。</summary>
    static void ScanUnits(MapData map, GridSystem grid, ref UnitController farSoldier, ref float farSoldierD,
        ref UnitController farAny, ref float farAnyD, ref int playerAlive, ref int soldierAlive)
    {
        playerAlive = 0; soldierAlive = 0;
        farSoldier = null; farSoldierD = -1f; farAny = null; farAnyD = -1f;
        var units = Object.FindObjectsOfType<UnitController>();
        foreach (var u in units)
        {
            if (u == null || !u.IsAlive || u.GetFaction() != Faction.PlayerCamp) continue;
            playerAlive++;
            float d = NearestGuardResourceDist(map, grid, u.transform.position);
            if (d > farAnyD) { farAnyD = d; farAny = u; }
            if (IsSoldier(u)) { soldierAlive++; if (d > farSoldierD) { farSoldierD = d; farSoldier = u; } }
        }
    }

    /// <summary>反射读 `GuardDeploymentSystem.IndexedResourceCells`（改造前不存在 ⇒ -1）。</summary>
    static int ReadIndexedCells()
    {
        var p = typeof(GuardDeploymentSystem).GetProperty("IndexedResourceCells", BindingFlags.Public | BindingFlags.Static);
        if (p == null) return -1;
        try { return (int)p.GetValue(null); } catch { return -1; }
    }

    static float NearestGuardResourceDist(MapData map, GridSystem grid, Vector2 pos)
    {
        float best = float.MaxValue;
        for (int i = 0; i < map.features.Length; i++)
        {
            if (!IsGuardFeature(map.features[i])) continue;
            int x = i % map.width, y = i / map.width;
            float d = ((Vector2)grid.CoordToWorld(new GridCoord(x, y)) - pos).magnitude;
            if (d < best) best = d;
        }
        return best == float.MaxValue ? -1f : best;
    }

    /// <summary>全图距资源点最远的格（兜底落点；抽样网格求距，避免 384²×384² 全对全）。</summary>
    static Vector2 FarCellWorld(MapData map, GridSystem grid)
    {
        int bestI = 0; float bestD = -1f;
        int step = Mathf.Max(1, map.width / 48);
        for (int y = 0; y < map.height; y += step)
            for (int x = 0; x < map.width; x += step)
            {
                var p = grid.CoordToWorld(new GridCoord(x, y));
                float d = NearestGuardResourceDistSampled(map, grid, p, step);
                if (d > bestD) { bestD = d; bestI = y * map.width + x; }
            }
        return grid.CoordToWorld(new GridCoord(bestI % map.width, bestI / map.width));
    }

    static float NearestGuardResourceDistSampled(MapData map, GridSystem grid, Vector2 pos, int step)
    {
        float best = float.MaxValue;
        for (int y = 0; y < map.height; y += step)
            for (int x = 0; x < map.width; x += step)
            {
                if (!IsGuardFeature(map.features[y * map.width + x])) continue;
                float d = ((Vector2)grid.CoordToWorld(new GridCoord(x, y)) - pos).magnitude;
                if (d < best) best = d;
            }
        return best == float.MaxValue ? -1f : best;
    }

    static GridCoord FindGuardCell(MapData map, int nth)
    {
        int seen = 0;
        for (int i = 0; i < map.features.Length; i++)
        {
            if (!IsGuardFeature(map.features[i])) continue;
            if (seen++ >= nth % Mathf.Max(1, CountGuardCells(map))) return new GridCoord(i % map.width, i / map.width);
        }
        return new GridCoord(map.width / 2, map.height / 2);
    }

    static int CountGuardCells(MapData map)
    {
        int c = 0;
        for (int i = 0; i < map.features.Length; i++) if (IsGuardFeature(map.features[i])) c++;
        return c;
    }

    static double TimeIt(int reps, System.Action act)
    {
        double[] ms = new double[reps];
        var sw = new Stopwatch();
        act();   // 预热
        for (int r = 0; r < reps; r++)
        {
            sw.Restart(); act(); sw.Stop();
            ms[r] = sw.Elapsed.TotalMilliseconds;
        }
        System.Array.Sort(ms);
        return ms[reps / 2];
    }

    static string Fmt(Vector2 v) => "(" + v.x.ToString("0.0") + ", " + v.y.ToString("0.0") + ")";

    // ================= 日志 =================

    static void Line(string s)
    {
        _log.AppendLine(s);
        UnityEngine.Debug.Log("[HH294P1] " + s);
    }

    static void Finish(bool ok)
    {
        _log.AppendLine("==== 收尾（ExitTestRun + QuitSmoke）" + (ok ? "" : " · 未完成") + " ====");
        Flush();
        try { TestHarnessApi.ExitTestRun(); } catch { }
        try { SmokeApi.QuitSmoke(); } catch { }
    }

    static void Flush()
    {
        try
        {
            if (_logPath == null)
                _logPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    UnityEngine.Application.dataPath, "..", "Logs", "hh294_p1_probe_" + _tag + ".log"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_logPath));
            System.IO.File.WriteAllText(_logPath, _log.ToString());
        }
        catch { }
    }
}
#endif
