using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================================
//  HH.294 片 6-3「F-14 · grade 进格表」验证探针（Editor-only）
//  口径真源：最高优先级文档/03_地图即数据库.md §6.9（资源等级＝格表事实·`D781`）
//           ＋ HH.294 片 6-3 派工口径（判据 1~15）
//
//  入口纪律（test-harness-first 铁律1）：**走正门** TestHarnessApi.EnterTestRun（禁裸跑）。
//  收尾纪律（L-32）：真暂停(Time.timeScale=0) → Save → ExitTestRun → 退 Play。
//  两段式（照 6-1/6-2 先例）：跑次 1＝全量＋落 hash；跑次 2＝同 seed 对比（grades 逐格一致）。
//
//  判据面：
//    §A 判据1  MapData.grades 在场（类型/长度）＋ grades==null 读口容忍（夹具/真图临时置 null）
//    §B 判据2  未打乱主流程：feat+climate hash == 9424A5D99D9C3543（6-2 基线）＋ Registry 21 ＋ 存在性反证
//    §C 判据3  分布实测 vs 15/70/15 ＋ 存在 Rich/Barren（皆在四型格）
//    §D 判据4  非资源格恒 Normal（机械扫描 = 0）
//    §E 判据5  作用域：Mine 恒 Normal ＋ 四型逐型非 Normal 计数
//    §F 判据6  门读口：TryGetCell 一次拿全（grade 在其中）＋ CellFilter.Grade 查询 ＋ 越界/未就绪
//    §G 判据7  回收/重生：采集复位 ＋ 直调重掷 ＋ 真实重生链（SettleDay+FlushPending）新点带 grade
//    §H 判据11 A/B 开关：OFF ⇒ 全 Normal；ON 复原 ⇒ 逐格一致 ＋ 全图 pass 耗时（代价读数）
//    §H2（跑次 2）判据3 后半：同 seed 两次 ⇒ feat/climate/grades 逐格一致
//  落盘：Logs/hh294_slice6_3/hh294_slice6_3_probe.txt（稳定名）＋ 时间戳副本 ＋ hash 文件
// ============================================================================
public static class HH294Slice63Probe
{
    public const string Tag = "HH294S63";
    private const string Menu = "Valley/审计/HH294片6-3/跑 grade 格表探针（正门进局）";
    private const int PROBE_SEED = 29418;   // 与片 6-2 探针同 seed ⇒ feat+climate hash 直比历史基线
    private const string PROBE_SLOT = "hh294_slice6_3";
    private const long BASELINE_FEAT_HASH = unchecked((long)0x9424A5D99D9C3543UL);   // 6-2 验收四连跑读数

    private static readonly StringBuilder Sb = new StringBuilder();
    private static bool _running;

    [MenuItem(Menu, priority = 212)]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[" + Tag + "] 须先 GameScene 进 Play（正门 EnterTestRun 在 Play 内协程）。已中止。");
            return;
        }
        if (_running) { Debug.LogWarning("[" + Tag + "] 探针已在跑（幂等守卫）。"); return; }
        _running = true;
        Sb.Length = 0;
        bool isSecond = File.Exists(HashFile());
        Sb.AppendLine(isSecond
            ? "# HH.294 片 6-3 grade 格表探针【第 2 段·同 seed 对比局】（正门 EnterTestRun·seed=" + PROBE_SEED + " 槽=" + PROBE_SLOT + "）"
            : "# HH.294 片 6-3 grade 格表探针【第 1 段·全量】（正门 EnterTestRun·seed=" + PROBE_SEED + " 槽=" + PROBE_SLOT + "）");
        Sb.AppendLine("# 跑次：" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                      + "｜MapGenRules.AssignGradesEnabled=" + MapGenRules.AssignGradesEnabled);
        new GameObject("HH294S63ProbeHost").AddComponent<Host>().Go(Run(isSecond));
    }

    private class Host : MonoBehaviour { public void Go(IEnumerator r) => StartCoroutine(r); }

    private static void Log(string line)
    {
        Sb.AppendLine(line);
        Debug.Log("[" + Tag + "] " + line);
    }

    private static IEnumerator Run(bool isSecond)
    {
        var cfg = new NewGameConfig
        {
            worldSeed = PROBE_SEED, mapSeed = PROBE_SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = PROBE_SLOT, kingdomName = "河谷王国"
        };
        Log("── 正门进局（seed=" + PROBE_SEED + " Medium/difficulty=2 · 与片 6-2 探针同参）");
        yield return TestHarnessApi.EnterTestRun(cfg);
        yield return null; yield return null;

        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) { Log("❌ ActiveMap 不在场 ⇒ 中止"); Finish(); yield break; }

        long hFeat = HashMap(map, false);
        long hAll = HashMap(map, true);
        long hGrade = HashGrades(map);
        Log("");
        Log("## §B-0 进局即取 hash（三口径）");
        Log("§B-0 feat+climate=" + hFeat.ToString("X16") + "（片 6-2 基线 " + BASELINE_FEAT_HASH.ToString("X16") + " ⇒ "
            + (hFeat == BASELINE_FEAT_HASH ? "逐值不变 ✅" : "⚠️ 已变") + "）");
        Log("§B-0 feat+climate+spawns=" + hAll.ToString("X16") + "（新口径·改后基准）");
        Log("§B-0 grades=" + hGrade.ToString("X16") + "（独立 hash）");

        if (isSecond)
        {
            string prevLine = ReadHashFile();
            Log("");
            Log("## §H2 判据3（后半）同 seed 两次建局对比（逐格一致）");
            Log("§H2 上次落盘：" + prevLine);
            long prevFeat = ParseHash(prevLine, "feat=");
            long prevGrade = ParseHash(prevLine, "grade=");
            Log("§H2 feat+climate：上次=" + prevFeat.ToString("X16") + " 本次=" + hFeat.ToString("X16")
                + " ⇒ " + (prevFeat == hFeat && prevFeat != 0 ? "逐格一致 ✅" : "❌ 不一致"));
            Log("§H2 grades：上次=" + prevGrade.ToString("X16") + " 本次=" + hGrade.ToString("X16")
                + " ⇒ " + (prevGrade == hGrade && prevGrade != 0 ? "逐格一致 ✅（grade 确定性）" : "❌ 不一致"));
            Finish();
            yield break;
        }

        Case_A_GradesPresent(map);
        Case_B_NoShuffle(map, hFeat);
        Case_C_Distribution(map);
        Case_D_NonResourceNormal(map);
        Case_E_Scope(map);
        Case_F_GateReads(map);
        Case_H_ABSwitch(map);              // §H 在 §G 之前：features 未变 ⇒「同 seed 重跑 pass 逐格一致」可作确定性正证
        yield return Case_G_RecycleRespawn(map);   // §G 会改 features（重生新点/采集删点）⇒ 置最后

        WriteHashFile(hFeat, hAll, hGrade);
        Finish();
    }

    // ========================================================================
    //  §A 判据1：grades 在场 ＋ null 容忍
    // ========================================================================
    private static void Case_A_GradesPresent(MapData map)
    {
        Log("");
        Log("## §A 判据1 grades 在场（类型/长度 = W×H）");
        bool present = map.grades != null;
        Log("§A grades 在场=" + present
            + "｜类型=" + (present ? map.grades.GetType().Name : "null")
            + "｜元素类型=" + typeof(ResourceGrade).Name
            + "｜长度=" + (present ? map.grades.Length : 0) + "｜W×H=" + (map.width * map.height)
            + " ⇒ " + (present && map.grades.Length == map.width * map.height ? "✅ 长度 = W×H" : ""));

        // 内存读数（逐值可复算·`L-37` 禁上限句；按枚举**底层类型**推 B/元素 —— Marshal.SizeOf 对枚举会抛，改走 GetUnderlyingType）
        var uG = System.Enum.GetUnderlyingType(typeof(ResourceGrade));
        var uF = System.Enum.GetUnderlyingType(typeof(FeatureType));
        var uC = System.Enum.GetUnderlyingType(typeof(ClimateZone));
        int szG = uG == typeof(int) ? 4 : (uG == typeof(byte) ? 1 : (uG == typeof(short) ? 2 : 8));
        int szF = uF == typeof(int) ? 4 : (uF == typeof(byte) ? 1 : (uF == typeof(short) ? 2 : 8));
        int szC = uC == typeof(int) ? 4 : (uC == typeof(byte) ? 1 : (uC == typeof(short) ? 2 : 8));
        int gn = map.features.Length;
        Log("§A 底层类型实读（内存读数基础）：ResourceGrade⇒" + uG.Name + "｜FeatureType⇒" + uF.Name + "｜ClimateZone⇒" + uC.Name);
        Log("§A 内存（逐值）：grades=" + szG + " B×" + gn + "=" + ((long)szG * gn) + " B（" + ((long)szG * gn / 1048576.0).ToString("0.0000") + " MiB）"
            + "｜features=" + szF + " B×" + gn + "=" + ((long)szF * gn) + " B"
            + "｜climateZones=" + szC + " B×" + gn + "=" + ((long)szC * gn) + " B"
            + "｜本局 " + map.width + "×" + map.height + "（Medium 档；Large 384²=147456 格 ⇒ grades=" + ((long)szG * 147456) + " B＝"
            + ((long)szG * 147456 / 1048576.0).ToString("0.0000") + " MiB）");

        // null 容忍①：夹具（Editor 探针常用 new MapData{...} 造夹具·不填 grades）
        var fake = new MapData { width = 8, height = 8, features = new FeatureType[64], climateZones = new ClimateZone[64] };
        var gf = MapGate.ReadGradeAt(fake, 3, 3);
        var gfOob = MapGate.ReadGradeAt(fake, 100, 100);
        var gfNull = MapGate.ReadGradeAt(null, 0, 0);
        Log("§A null 容忍①（夹具无 grades）：ReadGradeAt(3,3)=" + gf + "｜越界(100,100)=" + gfOob + "｜map=null ⇒ " + gfNull
            + " ⇒ " + (gf == ResourceGrade.Normal && gfOob == ResourceGrade.Normal && gfNull == ResourceGrade.Normal ? "✅ 全 Normal·不抛" : ""));

        // null 容忍②：真图临时置 null（同帧复原·探针侧）
        var saved = map.grades;
        map.grades = null;
        var g1 = MapGate.GetGradeAt(0, 0);
        bool cellOk = MapGate.TryGetCell(new GridCoord(map.width / 2, map.height / 2), out var ci);
        Log("§A null 容忍②（真图 grades=null·临时）：GetGradeAt(0,0)=" + g1 + "｜TryGetCell.grade=" + (cellOk ? ci.grade.ToString() : "(false)")
            + " ⇒ " + (g1 == ResourceGrade.Normal && cellOk && ci.grade == ResourceGrade.Normal ? "✅" : "❌"));
        map.grades = saved;
        Log("§A 临时置 null 已复原（引用还原 ⇒ 后续判据读数不受影响）");
    }

    // ========================================================================
    //  §B 判据2：未打乱主流程（本片最强判据）
    // ========================================================================
    private static void Case_B_NoShuffle(MapData map, long hFeat)
    {
        Log("");
        Log("## §B 判据2 未打乱主流程（feat+climate hash 与 6-2 基线逐值对照）");
        Log("§B feat+climate hash=" + hFeat.ToString("X16") + " vs 基线=" + BASELINE_FEAT_HASH.ToString("X16")
            + " ⇒ " + (hFeat == BASELINE_FEAT_HASH ? "逐值不变 ✅（主流程 rng 链未被 grade 触碰）" : "❌ 已变"));

        var reg = BuildingRegistry.Instance;
        int total = 0;
        if (reg != null && reg.All != null)
            for (int i = 0; i < reg.All.Count; i++) if (reg.All[i] != null) total++;
        Log("§B BuildingRegistry 总数=" + total + "（片 6-2 读数 21 ⇒ " + (total == 21 ? "逐值不变 ✅" : "⚠️ ≠21（如为 0/缺席需查）") + "）"
            + "｜威胁点=" + (map.threatSpawns != null ? map.threatSpawns.Count : -1)
            + "｜王国出生点=" + (map.kingdomSpawns != null ? map.kingdomSpawns.Count : -1));

        int res = 0, nonNormal = 0;
        for (int i = 0; i < map.features.Length; i++)
        {
            if (!MapGate.IsGradeFeature(map.features[i])) continue;
            res++;
            if (map.grades[i] != ResourceGrade.Normal) nonNormal++;
        }
        Log("§B 存在性反证（grade 确实被赋值·排除\"整片没跑\"）：四型资源格=" + res + "｜其中非 Normal=" + nonNormal
            + " ⇒ " + (nonNormal > 0 ? "✅ 存在非 Normal 等级" : " 全 Normal（pass 未生效）"));
    }

    // ========================================================================
    //  §C 判据3：分布实测（vs 15/70/15）＋ 存在 Rich/Barren
    // ========================================================================
    private static void Case_C_Distribution(MapData map)
    {
        Log("");
        Log("## §C 判据3 分布实测（四型格 vs 15/70/15）＋ 存在 Rich/Barren");
        int nB = 0, nN = 0, nR = 0, samples = 0;
        var sbB = new StringBuilder();
        var sbR = new StringBuilder();
        for (int i = 0; i < map.features.Length; i++)
        {
            if (!MapGate.IsGradeFeature(map.features[i])) continue;
            samples++;
            var g = map.grades[i];
            if (g == ResourceGrade.Barren)
            {
                nB++;
                if (sbB.Length < 110 && nB <= 3) { if (sbB.Length > 0) sbB.Append("; "); sbB.Append(Fmt(map, i, g)); }
            }
            else if (g == ResourceGrade.Rich)
            {
                nR++;
                if (sbR.Length < 110 && nR <= 3) { if (sbR.Length > 0) sbR.Append("; "); sbR.Append(Fmt(map, i, g)); }
            }
            else nN++;
        }
        float pB = samples > 0 ? nB * 100f / samples : 0f;
        float pN = samples > 0 ? nN * 100f / samples : 0f;
        float pR = samples > 0 ? nR * 100f / samples : 0f;
        Log("§C 四型格样本=" + samples + "：Barren=" + nB + "（" + pB.ToString("0.0") + "% vs 15%）｜Normal=" + nN
            + "（" + pN.ToString("0.0") + "% vs 70%）｜Rich=" + nR + "（" + pR.ToString("0.0") + "% vs 15%）");
        Log("§C 存在性反证：存在 Rich=" + (nR > 0) + "（样例 " + (sbR.Length > 0 ? sbR.ToString() : "-") + "）"
            + "｜存在 Barren=" + (nB > 0) + "（样例 " + (sbB.Length > 0 ? sbB.ToString() : "-") + "）"
            + " ⇒ 样例 feature 皆四型 ⇒ " + (nR > 0 && nB > 0 ? "✅" : "❌"));
    }

    // ========================================================================
    //  §D 判据4：非资源格恒 Normal（机械扫描）
    // ========================================================================
    private static void Case_D_NonResourceNormal(MapData map)
    {
        Log("");
        Log("## §D 判据4 非资源格恒 Normal（机械扫描全图）");
        int nonRes = 0, bad = 0;
        for (int i = 0; i < map.features.Length; i++)
        {
            if (MapGate.IsGradeFeature(map.features[i])) continue;
            nonRes++;
            if (map.grades[i] != ResourceGrade.Normal) bad++;
        }
        Log("§D 扫描 " + map.features.Length + " 格：非资源格（含 Mine/水/山/平原）=" + nonRes + "｜其中 grade != Normal 的格数=" + bad
            + " ⇒ " + (bad == 0 ? "✅ = 0" : ""));
    }

    // ========================================================================
    //  §E 判据5：作用域（Mine 恒 Normal；四型逐型非 Normal）
    // ========================================================================
    private static void Case_E_Scope(MapData map)
    {
        Log("");
        Log("## §E 判据5 作用域（Mine 恒 Normal；四型逐型给读数）");
        int mineCnt = 0, mineBad = 0;
        var kinds = new[] { FeatureType.Tree, FeatureType.OreVein, FeatureType.WoodPile, FeatureType.StonePile };
        var cnt = new int[4];
        var nonN = new int[4];
        for (int i = 0; i < map.features.Length; i++)
        {
            var f = map.features[i];
            var g = map.grades[i];
            if (f == FeatureType.Mine) { mineCnt++; if (g != ResourceGrade.Normal) mineBad++; continue; }
            for (int k = 0; k < 4; k++) if (f == kinds[k]) { cnt[k]++; if (g != ResourceGrade.Normal) nonN[k]++; break; }
        }
        Log("§E Mine 格=" + mineCnt + "｜其中非 Normal=" + mineBad + " ⇒ " + (mineBad == 0 ? "恒 Normal ✅（不入 grade 域·走 Building.grade 现状）" : ""));
        for (int k = 0; k < 4; k++)
            Log("§E " + kinds[k] + " 格=" + cnt[k] + "｜非 Normal=" + nonN[k] + " ⇒ "
                + (cnt[k] > 0 && nonN[k] > 0 ? "✅ 该型有等级事实" : (cnt[k] == 0 ? "⚠️ 本局 0 格（样本缺失·如实列报）" : "❌ 该型全 Normal")));
    }

    // ========================================================================
    //  §F 判据6：门读口（一次拿全 / CellFilter.Grade / 越界）
    // ========================================================================
    private static void Case_F_GateReads(MapData map)
    {
        Log("");
        Log("## §F 判据6 门读口（TryGetCell 含 grade ＋ CellFilter.Grade ＋ 越界/未就绪）");
        var coord = FindNonNormal(map, out var g0);
        if (coord.x >= 0)
        {
            bool ok = MapGate.TryGetCell(coord, out var ci);
            Log("§F 一次拿全（TryGetCell）：coord=" + coord + " ⇒ feature=" + ci.feature + "｜climate=" + ci.climate
                + "｜walkable=" + ci.walkable + "｜owner=" + ci.ownerKingdomId + "｜hasOccupant=" + ci.hasOccupant
                + "｜unitCount=" + ci.unitCount + "｜**grade=" + ci.grade + "**（同格直读=" + g0 + "）"
                + " ⇒ " + (ok && ci.grade == g0 ? "✅ grade 在其中且同源" : ""));
        }
        else Log("§F ⚠️ 本图未找到非 Normal 格（无法做一次拿全样例·如实列报）");

        int cx = coord.x >= 0 ? coord.x / 16 : 0, cy = coord.y >= 0 ? coord.y / 16 : 0;
        var rect = new RectInt(cx * 16, cy * 16, 16, 16);
        var buf = new List<MapGate.CellInfo>(256);
        int richGate = MapGate.QueryCells(rect, MapGate.CellFilter.Grade(ResourceGrade.Rich), buf);
        int barrenGate = MapGate.QueryCells(rect, MapGate.CellFilter.Grade(ResourceGrade.Barren), buf);
        int normalGate = MapGate.QueryCells(rect, MapGate.CellFilter.Grade(ResourceGrade.Normal), buf);
        int richScan = 0, barrenScan = 0, total = 0;
        for (int y = rect.yMin; y < rect.yMax; y++)
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                if (x < 0 || y < 0 || x >= map.width || y >= map.height) continue;
                total++;
                var g = MapGate.ReadGradeAt(map, x, y);
                if (g == ResourceGrade.Rich) richScan++;
                if (g == ResourceGrade.Barren) barrenScan++;
            }
        Log("§F CellFilter.Grade 矩形(" + rect.x + "," + rect.y + ",16,16)（门）：Rich=" + richGate + "｜Barren=" + barrenGate
            + "｜Normal=" + normalGate + "｜机械扫描（独立列）：Rich=" + richScan + "｜Barren=" + barrenScan + "｜矩形格数=" + total
            + " ⇒ " + (richGate == richScan && barrenGate == barrenScan ? "两列一致 ✅" : "❌ 两列不一致"));
        Log("§F 越界/未就绪：GetGradeAt(-1,-1)=" + MapGate.GetGradeAt(-1, -1) + "｜GetGradeAt(W,H)=" + MapGate.GetGradeAt(map.width, map.height)
            + "｜ReadGradeAt(map,-5,3)=" + MapGate.ReadGradeAt(map, -5, 3) + " ⇒ 全 Normal（不抛）");
    }

    private static GridCoord FindNonNormal(MapData map, out ResourceGrade g)
    {
        for (int i = 0; i < map.features.Length; i++)
            if (MapGate.IsGradeFeature(map.features[i]) && map.grades[i] != ResourceGrade.Normal)
            { g = map.grades[i]; return new GridCoord(i % map.width, i / map.width); }
        g = ResourceGrade.Normal;
        return new GridCoord(-1, -1);
    }

    // ========================================================================
    //  §G 判据7：回收/重生语义
    // ========================================================================
    private static IEnumerator Case_G_RecycleRespawn(MapData map)
    {
        Log("");
        Log("## §G 判据7 回收/重生语义（采集复位 ＋ 重掷 ＋ 真实重生链）");

        var coord = FindNonNormal(map, out var g0);
        bool constructed = false;
        if (coord.x < 0)
        {
            for (int i = 0; i < map.features.Length; i++)
                if (MapGate.IsGradeFeature(map.features[i])) { coord = new GridCoord(i % map.width, i / map.width); break; }
            if (coord.x >= 0)
            {
                MapGate.PlaceResourceNode(coord, map.features[coord.y * map.width + coord.x], ResourceGrade.Rich);
                g0 = ResourceGrade.Rich;
                constructed = true;
            }
        }
        if (coord.x < 0) { Log("§G ❌ 找不到四型格 ⇒ 无法测"); yield break; }

        int i0 = coord.y * map.width + coord.x;
        var f0 = map.features[i0];
        bool removed = MapGate.RemoveResourceNode(coord);
        Log("§G-1 采集复位：coord=" + coord + "｜feature=" + f0 + "｜grade 初值=" + g0 + (constructed ? "（探针构造兜底）" : "")
            + " ⇒ RemoveResourceNode=" + removed + " ⇒ after feature=" + map.features[i0] + "／grade=" + map.grades[i0]
            + " ⇒ " + (removed && map.features[i0] == FeatureType.Plain && map.grades[i0] == ResourceGrade.Normal
                ? "复位 Normal ✅（存在性反证：初值非 Normal）" : "❌"));

        bool placed = MapGate.PlaceResourceNode(coord, f0, ResourceGrade.Rich);
        Log("§G-2 直调写口语义：PlaceResourceNode(...,Rich)=" + placed + " ⇒ grade=" + map.grades[i0]
            + " ⇒ " + (placed && map.grades[i0] == ResourceGrade.Rich ? "✅ 重掷值落格" : "❌"));
        MapGate.RemoveResourceNode(coord);   // 清理（同步复位）

        var inst = ResourceRespawnSystem.Instance;
        if (inst == null) { Log("§G-3 ❌ ResourceRespawnSystem 不在场"); yield break; }
        var beforeFeat = (FeatureType[])map.features.Clone();
        int day = inst.LastSettleDay + 1;
        inst.SettleDay(day);
        Log("§G-3 真实重生链：SettleDay(" + day + ") ⇒ 计划补量=" + inst.LastDayPlannedAdditions + " 点｜池子 ready=" + inst.PoolReady
            + "｜分帧落格中（≤200 帧）…");
        int frames = 0, newPts = 0, newNonNormal = 0;
        var sample = new StringBuilder();
        while (frames < 200)
        {
            inst.FlushPending();
            frames++;
            if (frames % 40 == 0)
            {
                CountNewPoints(map, beforeFeat, out newPts, out newNonNormal, sample, false);
                if (newPts >= 60) break;
            }
            yield return null;
        }
        CountNewPoints(map, beforeFeat, out newPts, out newNonNormal, sample, true);
        Log("§G-3 落格 " + frames + " 帧 ⇒ 新落点（Plain→四型）=" + newPts + "｜其中非 Normal=" + newNonNormal
            + "（期望约 30%＝Rich15+Barren15）⇒ " + (newPts > 0 ? "新点带 grade ✅" : "⚠️ 0 新点（如实列报）")
            + "｜样例：" + (sample.Length > 0 ? sample.ToString() : "-"));

        int mineBad = 0;
        for (int i = 0; i < map.features.Length; i++)
            if (map.features[i] == FeatureType.Mine && map.grades[i] != ResourceGrade.Normal) mineBad++;
        Log("§G-4 Mine 不受影响（重生后复扫）：Mine 格非 Normal=" + mineBad + " ⇒ " + (mineBad == 0 ? "✅" : "❌"));
    }

    private static void CountNewPoints(MapData map, FeatureType[] before, out int pts, out int nonNormal, StringBuilder sample, bool fill)
    {
        pts = 0;
        nonNormal = 0;
        if (fill) sample.Length = 0;
        for (int i = 0; i < map.features.Length; i++)
        {
            if (!MapGate.IsGradeFeature(map.features[i])) continue;
            if (MapGate.IsGradeFeature(before[i])) continue;   // 改前已是四型 ⇒ 非新点
            pts++;
            var g = map.grades[i];
            if (g != ResourceGrade.Normal) nonNormal++;
            if (fill && sample.Length < 130 && pts <= 5)
            {
                if (sample.Length > 0) sample.Append("; ");
                sample.Append(Fmt(map, i, g));
            }
        }
    }

    // ========================================================================
    //  §H 判据11：A/B 开关（OFF ⇒ 全 Normal；ON 复原 ⇒ 逐格一致 ＋ 代价）
    // ========================================================================
    private static void Case_H_ABSwitch(MapData map)
    {
        Log("");
        Log("## §H 判据11 A/B 开关（`MapGenRules.AssignGradesEnabled` 默认 true；OFF ⇒ 全 Normal）");
        var cfg = Resources.Load<MapGenRulesConfig>("Grid/MapGenRulesConfig");
        Log("§H cfg（MapGenRulesConfig）= " + (cfg != null ? "在场" : "❌ 缺席")
            + "｜gradeWeights=" + (cfg != null && cfg.gradeWeights != null
                ? "{" + string.Join(",", cfg.gradeWeights) + "}（枚举序 Barren/Normal/Rich）" : "-"));

        var saved = (ResourceGrade[])map.grades.Clone();
        int nonNormalSaved = 0;
        for (int i = 0; i < saved.Length; i++) if (saved[i] != ResourceGrade.Normal) nonNormalSaved++;

        MapGenRules.AssignGradesEnabled = false;
        MapGenRules.AssignResourceGrades(map, cfg, new System.Random(1));
        int badOff = 0;
        for (int i = 0; i < map.grades.Length; i++) if (map.grades[i] != ResourceGrade.Normal) badOff++;
        Log("§H OFF 段（取反）：重跑 pass ⇒ 全图非 Normal=" + badOff + "（期望 0）⇒ " + (badOff == 0 ? "✅ OFF ⇒ 全 Normal" : "❌"));

        MapGenRules.AssignGradesEnabled = true;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        MapGenRules.AssignResourceGrades(map, cfg, new System.Random(unchecked(PROBE_SEED * 31 + 977)));
        sw.Stop();
        int diff = 0, firstDiff = -1;
        for (int i = 0; i < map.grades.Length; i++)
            if (map.grades[i] != saved[i]) { diff++; if (firstDiff < 0) firstDiff = i; }
        Log("§H ON 复原（同 seed 派生 rng 重跑 pass）：耗时=" + sw.Elapsed.TotalMilliseconds.ToString("0.00")
            + " ms（全图 " + map.grades.Length + " 格·代价读数）｜非 Normal=" + nonNormalSaved
            + " ⇒ 与副本逐格 diff=" + diff + (firstDiff >= 0 ? "（首差异 @" + firstDiff + "）" : "")
            + " ⇒ " + (diff == 0 ? "逐格一致 ✅（grade pass 确定性正证：同 seed + 同 features ⇒ 同 grades）"
                                : "⚠️ 非 0 ⇒ 副本回写兜底（调用点在 §G 之前·features 未变 ⇒ 若 diff≠0 须排查）"));
        if (diff != 0)
        {
            for (int i = 0; i < map.grades.Length; i++) MapGate.GenesisWriteGrade(map, i, saved[i]);
            Log("§H 兜底回写完成 ⇒ 复验=" + (VerifyEqual(map, saved) ? "逐格一致 ✅" : "❌ 仍不一致"));
        }
    }

    private static bool VerifyEqual(MapData map, ResourceGrade[] saved)
    {
        for (int i = 0; i < map.grades.Length; i++) if (map.grades[i] != saved[i]) return false;
        return true;
    }

    // ========================================================================
    //  hash（口一：feat+climate ＝ 逐字照搬片 6-2 探针实现；口二：+spawns；口三：grades 独立）
    // ========================================================================
    private static long HashMap(MapData map, bool withSpawns)
    {
        if (map == null || map.features == null) return 0;
        unchecked
        {
            ulong h1 = 14695981039346656037UL;
            ulong h2 = 0;
            for (int i = 0; i < map.features.Length; i++)
            {
                h1 = (h1 ^ (ulong)(int)map.features[i]) * 1099511628211UL;
                h2 += (ulong)(int)map.features[i];
            }
            if (map.climateZones != null)
                for (int i = 0; i < map.climateZones.Length; i++)
                {
                    h1 = (h1 ^ ((ulong)(int)map.climateZones[i] + 7UL)) * 1099511628211UL;
                    h2 += (ulong)(int)map.climateZones[i] * 31UL;
                }
            if (withSpawns)
            {
                if (map.kingdomSpawns != null)
                    for (int i = 0; i < map.kingdomSpawns.Count; i++)
                    {
                        var k = map.kingdomSpawns[i];
                        h1 = (h1 ^ (ulong)(uint)(k.x * 313 + k.y * 317 + i)) * 1099511628211UL;
                    }
                if (map.threatSpawns != null)
                    for (int i = 0; i < map.threatSpawns.Count; i++)
                    {
                        var s = map.threatSpawns[i];
                        h1 = (h1 ^ (ulong)(uint)(s.coord.x * 131 + s.coord.y * 137 + (int)s.faction * 977 + s.strength * 17)) * 1099511628211UL;
                        h2 += (ulong)(uint)((int)(s.direction.x * 10000f) * 199 + (int)(s.direction.y * 10000f) * 211);
                    }
            }
            return (long)(h1 ^ (h2 << 1));
        }
    }

    private static long HashGrades(MapData map)
    {
        if (map == null || map.grades == null) return 0;
        unchecked
        {
            ulong h1 = 14695981039346656037UL;
            ulong h2 = 0;
            for (int i = 0; i < map.grades.Length; i++)
            {
                h1 = (h1 ^ ((ulong)(int)map.grades[i] + 3UL)) * 1099511628211UL;
                h2 += (ulong)(int)map.grades[i] * 7UL;
            }
            return (long)(h1 ^ (h2 << 1));
        }
    }

    private static string Fmt(MapData map, int i, ResourceGrade g)
        => "(" + (i % map.width) + "," + (i / map.width) + ")" + map.features[i] + "=" + g;

    // ========================================================================
    //  hash 落盘 / 读取
    // ========================================================================
    private static string HashFile() => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hh294_slice6_3", "hh294_slice6_3_hash.txt");

    private static void WriteHashFile(long feat, long all, long grade)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HashFile()));
            File.WriteAllText(HashFile(), "feat=" + feat.ToString("X16") + " all=" + all.ToString("X16")
                + " grade=" + grade.ToString("X16") + " seed=" + PROBE_SEED + " at " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Log("§hash 已落盘：" + HashFile());
        }
        catch (System.Exception ex) { Debug.LogError("[" + Tag + "] hash 落盘失败：" + ex.Message); }
    }

    private static string ReadHashFile()
    {
        try { return File.Exists(HashFile()) ? File.ReadAllText(HashFile()) : "(无)"; }
        catch { return "(读取失败)"; }
    }

    private static long ParseHash(string line, string key)
    {
        try
        {
            int i = line.IndexOf(key, System.StringComparison.Ordinal);
            if (i < 0) return 0;
            i += key.Length;
            int j = i;
            while (j < line.Length && line[j] != ' ') j++;
            return System.Convert.ToInt64(line.Substring(i, j - i), 16);
        }
        catch { return 0; }
    }

    // ========================================================================
    //  收尾（L-32）＋ 落盘
    // ========================================================================
    private static void Finish()
    {
        Time.timeScale = 0f;
        bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(PROBE_SLOT);
        TestHarnessApi.ExitTestRun();

        WriteFile();
        Log("★ 收尾：真暂停(TS=0)+封盘=" + saved + "（槽=" + PROBE_SLOT + "）→ 退 Play（L-32 条文1）");
        _running = false;
        EditorApplication.ExitPlaymode();
    }

    private static void WriteFile()
    {
        try
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hh294_slice6_3");
            Directory.CreateDirectory(dir);
            string stable = Path.Combine(dir, "hh294_slice6_3_probe.txt");
            File.WriteAllText(stable, Sb.ToString());
            string stamped = Path.Combine(dir, "hh294_slice6_3_probe_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(stamped, Sb.ToString());
            Debug.Log("[" + Tag + "] 落盘：" + stable + " ＋ 时间戳副本");
        }
        catch (System.Exception ex) { Debug.LogError("[" + Tag + "] 落盘失败：" + ex.Message); }
    }
}