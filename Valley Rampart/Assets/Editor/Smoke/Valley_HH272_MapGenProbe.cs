using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// ============================================================================
//  HH.272 地图生成重构批 · 验收探针（生成层直调 + 实机快照）
//
//  用途：把任务书 §三 12 条验收线转成**可复算的数值证据**，写入 Logs/hh272_probe.log。
//  用法：
//    - 编辑态（生成层直调，非进局）：Exec `Valley_HH272_MapGenProbe.RunAll()`
//      ⇒ 128² 小图（M1 计划）＋ 256² 三难度 ＋ 同 seed 确定性 ＋ 性能
//    - Play 态（实机正门）：Exec `Valley_HH272_MapGenProbe.SnapshotLive()`
//      ⇒ 对 WorldManager.Instance.ActiveMap 做同套读数
//
//  口径声明：本探针的「生成层直调」按 WorldManager.GenerateMap 的**同序同参**调用 MapGenRules
//  （唯一差异＝AI 模板列表传 null ⇒ 仅影响 AI 出生点的偏好带匹配，不影响气候/资源/山脉/净空区）。
// ============================================================================
public static class Valley_HH272_MapGenProbe
{
    const int SEED = 21107;

    public static MapGenRulesConfig Cfg() => Resources.Load<MapGenRulesConfig>("Grid/MapGenRulesConfig");

    /// <summary>按 WorldManager 同序同参生成一张图（生成层直调）。</summary>
    public static MapData Build(int seed, int w, int h, int difficulty, bool water = true, bool spawns = true)
    {
        var cfg = Cfg();
        var map = new MapData
        {
            mapId = 0, seed = seed, width = w, height = h,
            features = new FeatureType[w * h],
            climateZones = new ClimateZone[w * h],
            kingdomSpawns = new List<Vector2Int>(),
            threatSpawns = new List<SpawnDef>(),
            naturalBuildings = new List<NaturalBuilding>()
        };
        var rng = new System.Random(seed);
        MapGenRules.FillClimateZones(rng, map, cfg);
        MapGenRules.FillFeatures(rng, map, cfg, difficulty);
        int aiCount = 2;
        if (spawns) MapGenRules.PlaceKingdomSpawns(rng, map, cfg, WorldSize.Medium, aiCount, null);
        MapGenRules.ClearKingdomZones(map, cfg);
        MapGenRules.EnsureChunkResourceQuota(rng, map, cfg, difficulty);
        // HH.291 A5：步骤 7「资源就近补」已退役（`EnsureNearbyResources` 删除 ⇒ 本行同步移除）
        MapValidator.ValidateConnectivity(map);
        if (water)
        {
            MapGenRules.PlaceWater(rng, map, WorldSize.Medium);
            MapValidator.ValidateConnectivity(map);
        }
        MapGenRules.PlaceThreatSpawns(rng, map, cfg, difficulty);
        // 【HH.294 片 6-2 收尾 同步】清场后（实体派生路径与对照开关已删）⇒ `naturalBuildings` **恒空**
        //   （资源点＝格表）；本探针不读其内容（只 Mine 相关读数·恒 0 语义不变）。
        MapGenRules.DeriveNaturalBuildings(map);
        return map;
    }

    // ======================= 读数 =======================

    /// <summary>气候层形状读数（验收线 1 / 7）。</summary>
    public static string ZoneRead(MapData m, MapGenRulesConfig cfg, string label)
    {
        int w = m.width, h = m.height, n = w * h;
        int minSize = cfg != null ? cfg.clusterSizeMin : 4;
        int maxSize = cfg != null ? cfg.clusterSizeMax : 384;
        var label2 = new int[n];
        for (int i = 0; i < n; i++) label2[i] = -1;
        var q = new Queue<int>();
        var sizes = new List<int>();
        var fill = new List<double>();
        var bands = new List<int>();
        int cc = 0;
        for (int i = 0; i < n; i++)
        {
            if (label2[i] != -1) continue;
            int band0 = (int)m.climateZones[i];
            int c = cc++;
            label2[i] = c; q.Clear(); q.Enqueue(i);
            int sz = 0, minx = int.MaxValue, maxx = -1, miny = int.MaxValue, maxy = -1;
            while (q.Count > 0)
            {
                int cur = q.Dequeue(); sz++;
                int cx = cur % w, cy = cur / w;
                if (cx < minx) minx = cx; if (cx > maxx) maxx = cx;
                if (cy < miny) miny = cy; if (cy > maxy) maxy = cy;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (label2[ni] != -1 || (int)m.climateZones[ni] != band0) continue;
                    label2[ni] = c; q.Enqueue(ni);
                }
            }
            sizes.Add(sz); bands.Add(band0);
            double bw = maxx - minx + 1, bh = maxy - miny + 1;
            fill.Add(sz / (bw * bh));
        }
        int frag = 0, over = 0, maxSeen = 0, minSeen = int.MaxValue; double sum = 0, sumFill = 0, maxFill = 0;
        for (int c = 0; c < cc; c++)
        {
            if (sizes[c] < minSize) frag++;
            if (sizes[c] > maxSize) over++;
            if (sizes[c] > maxSeen) maxSeen = sizes[c];
            if (sizes[c] < minSeen) minSeen = sizes[c];
            sum += sizes[c]; sumFill += fill[c];
            if (fill[c] > maxFill) maxFill = fill[c];
        }
        // 边界格占比（有异带邻格的格）
        int edge = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int band0 = (int)m.climateZones[y * w + x];
                bool isEdge = false;
                if (x + 1 < w && (int)m.climateZones[y * w + x + 1] != band0) isEdge = true;
                if (!isEdge && x - 1 >= 0 && (int)m.climateZones[y * w + x - 1] != band0) isEdge = true;
                if (!isEdge && y + 1 < h && (int)m.climateZones[(y + 1) * w + x] != band0) isEdge = true;
                if (!isEdge && y - 1 >= 0 && (int)m.climateZones[(y - 1) * w + x] != band0) isEdge = true;
                if (isEdge) edge++;
            }
        int[] bandCnt = new int[4];
        for (int i = 0; i < n; i++) bandCnt[(int)m.climateZones[i]]++;
        var sb = new StringBuilder();
        sb.AppendLine($"  [{label}] 簇数={cc}  碎片(<{minSize})={frag}  超限(>{maxSize})={over}  size min/avg/max={minSeen}/{sum / cc:0.0}/{maxSeen}");
        sb.AppendLine($"  [{label}] bbox 填充度 avg={sumFill / cc:0.000} max={maxFill:0.000}（硬方块=1.000）· 边界格={edge}（占 {edge * 100.0 / n:0.0}%）");
        sb.AppendLine($"  [{label}] 带分布 T/S/Tm/C = {bandCnt[0]}/{bandCnt[1]}/{bandCnt[2]}/{bandCnt[3]}");
        return sb.ToString();
    }

    /// <summary>资源层读数（验收线 2/3/4/5）。</summary>
    public static string ResRead(MapData m, MapGenRulesConfig cfg, int difficulty, string label)
    {
        int cw = MapGenRules.ChunkW(m), ch = MapGenRules.ChunkH(m);
        float T = cfg != null ? cfg.poolCapBase : 96f;   // 【HH.294 片 5】`resourcesPerChunkBase` 已删 ⇒ 改读 `poolCapBase`（cap 基准）
        int di = Mathf.Clamp(difficulty - 1, 0, 2);
        float scale = cfg != null && cfg.difficultyResourceScale != null && cfg.difficultyResourceScale.Length > di
            ? cfg.difficultyResourceScale[di] : 1f;
        float target = T * scale;
        float lo = target * 0.85f, hi = target * 1.15f;

        int tot = 0, inWin = 0, maxPerCell = 0, cellsWIthRes = 0;
        var perType = new int[5];
        int guardViol = 0;
        var chunkTotals = new int[cw * ch];
        for (int cy = 0; cy < ch; cy++)
            for (int cx = 0; cx < cw; cx++)
            {
                var band = MapGenRules.DominantZoneOfChunk(m, cx, cy);
                // 与 MapGenRules.ComputeQuota 同式复算（见 MapGenRulesConfig.resourceWeights）
                var w5 = cfg.GetResourceWeights(band);
                float sumR = 0f; var r = new float[5];
                for (int i = 0; i < 5; i++) { r[i] = 1f - Mathf.Clamp01(w5[i]); sumR += r[i]; }
                var b = new int[5];
                for (int i = 0; i < 5; i++) b[i] = Mathf.FloorToInt(target * (r[i] / sumR) * (cfg != null ? cfg.guaranteeRatio : 0.5f));
                var have = new int[5];
                for (int y = cy * 16; y < cy * 16 + 16 && y < m.height; y++)
                    for (int x = cx * 16; x < cx * 16 + 16 && x < m.width; x++)
                    {
                        var f = m.features[y * m.width + x];
                        for (int t = 0; t < 5; t++)
                            if (f == MapGenRules.ResourceKindFeature[t]) { have[t]++; break; }
                    }
                int ctot = 0;
                for (int t = 0; t < 5; t++) { ctot += have[t]; perType[t] += have[t]; }
                chunkTotals[cy * cw + cx] = ctot;
                tot += ctot;
                if (ctot >= lo && ctot <= hi) inWin++;
                for (int t = 0; t < 5; t++)
                {
                    int haveCell = t == 4 ? have[4] : have[t];
                    if (haveCell < b[t]) guardViol++;
                }
            }
        // 每 Cell 资源数（坑位口径）
        for (int i = 0; i < m.features.Length; i++)
        {
            bool isRes = false;
            for (int t = 0; t < 5; t++) if (m.features[i] == MapGenRules.ResourceKindFeature[t]) { isRes = true; break; }
            if (!isRes) continue;
            cellsWIthRes++;
            if (1 > maxPerCell) maxPerCell = 1;
        }
        int minC = int.MaxValue, maxC = int.MinValue; double sumC = 0;
        for (int i = 0; i < chunkTotals.Length; i++)
        {
            if (chunkTotals[i] < minC) minC = chunkTotals[i];
            if (chunkTotals[i] > maxC) maxC = chunkTotals[i];
            sumC += chunkTotals[i];
        }
        var sb = new StringBuilder();
        sb.AppendLine($"  [{label}] T×难度={target:0}  窗口[{lo:0},{hi:0}]  区块数={chunkTotals.Length}  落在窗口内={inWin}");
        sb.AppendLine($"  [{label}] 资源总数={tot}（均 {sumC / chunkTotals.Length:0.0}/区块，min={minC} max={maxC}）  上限偏差={Mathf.Max(0f, (maxC - target) / target) * 100f:0.0}%");
        sb.AppendLine($"  [{label}] 逐型: Tree={perType[0]} Stone={perType[1]} Wood={perType[2]} Ore={perType[3]} Mine格={perType[4]}");
        sb.AppendLine($"  [{label}] 保底违反区块-型次={guardViol}（0=每区块每型 ≥ B_i）  每 Cell 资源数 max={maxPerCell}（≤4 ⇒ 坑位不重叠）");
        return sb.ToString();
    }

    /// <summary>山脉化读数（验收线 11）。</summary>
    public static string MountainRead(MapData m, MapGenRulesConfig cfg, string label)
    {
        int w = m.width, h = m.height, n = w * h;
        var isMt = new bool[n];
        int tot = 0;
        for (int i = 0; i < n; i++)
        {
            isMt[i] = m.features[i] == FeatureType.Mountain || m.features[i] == FeatureType.SnowMountain;
            if (isMt[i]) tot++;
        }
        var label2 = new int[n];
        for (int i = 0; i < n; i++) label2[i] = -1;
        var q = new Queue<int>();
        int cc = 0, fragCnt = 0, maxSz = 0, minSz = int.MaxValue; double sumSz = 0, sumAr = 0, sumFill = 0;
        for (int i = 0; i < n; i++)
        {
            if (!isMt[i] || label2[i] != -1) continue;
            int c = cc++;
            label2[i] = c; q.Clear(); q.Enqueue(i);
            int sz = 0, minx = int.MaxValue, maxx = -1, miny = int.MaxValue, maxy = -1;
            while (q.Count > 0)
            {
                int cur = q.Dequeue(); sz++;
                int cx = cur % w, cy = cur / w;
                if (cx < minx) minx = cx; if (cx > maxx) maxx = cx;
                if (cy < miny) miny = cy; if (cy > maxy) maxy = cy;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (!isMt[ni] || label2[ni] != -1) continue;
                    label2[ni] = c; q.Enqueue(ni);
                }
            }
            if (sz < (cfg != null ? cfg.mountainClusterMinSize : 4)) fragCnt++;
            if (sz > maxSz) maxSz = sz;
            if (sz < minSz) minSz = sz;
            sumSz += sz;
            double bw = maxx - minx + 1, bh = maxy - miny + 1;
            double ar = bw / bh; if (ar < 1) ar = 1.0 / ar;
            sumAr += ar; sumFill += sz / (bw * bh);
        }
        var sb = new StringBuilder();
        sb.AppendLine($"  [{label}] 山体格={tot}（占 {tot * 100.0 / n:0.00}%）  4-连通簇数={cc}（≥2:1 长宽比者见下）  碎片={fragCnt}（期望 0）");
        if (cc > 0)
            sb.AppendLine($"  [{label}] 簇 size min/avg/max={minSz}/{sumSz / cc:0.0}/{maxSz}  平均长宽比={sumAr / cc:0.00}  平均 bbox 填充度={sumFill / cc:0.000}");
        return sb.ToString();
    }

    /// <summary>可走率 / 水域占比 / 连通性（验收线 6/11③/12）。</summary>
    public static string WalkRead(MapData m, string label, bool waterPlaced)
    {
        int n = m.features.Length;
        int walk = 0, river = 0, ocean = 0, mtn = 0, res = 0;
        for (int i = 0; i < n; i++)
        {
            var f = m.features[i];
            if (MapGenRules.IsWalkableFeature(f)) walk++;
            if (f == FeatureType.River) river++;
            if (f == FeatureType.Ocean) ocean++;
            if (f == FeatureType.Mountain || f == FeatureType.SnowMountain) mtn++;
            for (int t = 0; t < 5; t++) if (f == MapGenRules.ResourceKindFeature[t]) { res++; break; }
        }
        // 连通性：可走域 4-连通分量（size ≥ 16 视为有效区），出生点是否同域
        var comp = new int[n];
        for (int i = 0; i < n; i++) comp[i] = -1;
        var q = new Queue<int>();
        int cc = 0, big = 0; var sizes = new List<int>();
        int w = m.width, h = m.height;
        for (int i = 0; i < n; i++)
        {
            if (!MapGenRules.IsWalkableFeature(m.features[i]) || comp[i] != -1) continue;
            int c = cc++; comp[i] = c; q.Clear(); q.Enqueue(i);
            int sz = 0;
            while (q.Count > 0)
            {
                int cur = q.Dequeue(); sz++;
                int cx = cur % w, cy = cur / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (comp[ni] != -1 || !MapGenRules.IsWalkableFeature(m.features[ni])) continue;
                    comp[ni] = c; q.Enqueue(ni);
                }
            }
            sizes.Add(sz); if (sz >= 16) big++;
        }
        string spawnSame = "n/a";
        if (m.kingdomSpawns != null && m.kingdomSpawns.Count > 0)
        {
            var root = comp[m.kingdomSpawns[0].y * w + m.kingdomSpawns[0].x];
            bool same = true;
            for (int i = 0; i < m.kingdomSpawns.Count; i++)
                if (comp[m.kingdomSpawns[i].y * w + m.kingdomSpawns[i].x] != root) same = false;
            spawnSame = same ? "同域 ✅" : "不同域 ❌";
        }
        var sb = new StringBuilder();
        sb.AppendLine($"  [{label}] 可走率={walk * 100.0 / n:0.00}%（水域已放={waterPlaced}）  水域: River={river} Ocean={ocean} 占比={(river + ocean) * 100.0 / n:0.00}%（**无 Lake**）");
        sb.AppendLine($"  [{label}] 山/雪={mtn}（占 {mtn * 100.0 / n:0.00}%）  资源格={res}  可走域分量={cc}（≥16 格的有效域={big}）  出生点{spawnSame}");
        return sb.ToString();
    }

    /// <summary>主城净空区读数（验收线 6）。</summary>
    public static string ClearZoneRead(MapData m, MapGenRulesConfig cfg, string label)
    {
        int r = cfg != null ? cfg.kingdomClearRadius : 4;
        int half = 1 + r;
        var sb = new StringBuilder();
        sb.AppendLine($"  [{label}] 净空区边长={half * 2 + 1}×{half * 2 + 1}（R={r}）  出生点数={m.kingdomSpawns.Count}");
        for (int i = 0; i < m.kingdomSpawns.Count; i++)
        {
            var sp = m.kingdomSpawns[i];
            int bad = 0, water = 0, mtn = 0, plain = 0, resN = 0;
            for (int y = sp.y - half; y <= sp.y + half; y++)
                for (int x = sp.x - half; x <= sp.x + half; x++)
                {
                    if (x < 0 || y < 0 || x >= m.width || y >= m.height) continue;
                    var f = m.features[y * m.width + x];
                    if (f == FeatureType.Tree || f == FeatureType.Mine || f == FeatureType.OreVein
                        || f == FeatureType.StonePile || f == FeatureType.WoodPile) { bad++; resN++; }
                    if (f == FeatureType.River || f == FeatureType.Ocean) water++;
                    if (f == FeatureType.Mountain || f == FeatureType.SnowMountain) mtn++;
                    if (f == FeatureType.Plain) plain++;
                }
            sb.AppendLine($"    spawn[{i}]=({sp.x},{sp.y})  区内资源格(禁)={bad}  平原={plain}  水={water}  山={mtn}");
        }
        return sb.ToString();
    }

    /// <summary>逐格一致性（验收线 7 确定性）。</summary>
    public static bool SameMap(MapData a, MapData b, out string why)
    {
        why = "";
        if (a.width != b.width || a.height != b.height) { why = "尺寸不同"; return false; }
        for (int i = 0; i < a.features.Length; i++)
            if (a.features[i] != b.features[i] || a.climateZones[i] != b.climateZones[i])
            { why = $"首差 idx={i} feat {a.features[i]}/{b.features[i]} zone {a.climateZones[i]}/{b.climateZones[i]}"; return false; }
        if (a.kingdomSpawns.Count != b.kingdomSpawns.Count) { why = "出生点数不同"; return false; }
        for (int i = 0; i < a.kingdomSpawns.Count; i++)
            if (a.kingdomSpawns[i] != b.kingdomSpawns[i]) { why = $"出生点[{i}] 不同"; return false; }
        return true;
    }

    // ======================= 入口 =======================

    /// <summary>编辑态全量读数（生成层直调）。</summary>
    public static string RunAll()
    {
        var cfg = Cfg();
        var sb = new StringBuilder();
        sb.AppendLine("================ HH.272 地图生成重构批 · 验收读数 ================");
        sb.AppendLine($"配置：poolCapBase={cfg.poolCapBase} guaranteeRatio={cfg.guaranteeRatio} " +
                      $"难度系数=[{string.Join(",", cfg.difficultyResourceScale)}] kingdomClearRadius={cfg.kingdomClearRadius}");
        sb.AppendLine($"      簇 min/typMin/typMax/max={cfg.clusterSizeMin}/{cfg.clusterSizeTypicalMin}/{cfg.clusterSizeTypicalMax}/{cfg.clusterSizeMax} " +
                      $"山体占比=[{string.Join(",", cfg.mountainCellRatio)}] 雪占比=[{string.Join(",", cfg.mountainSnowRatio)}]");

        // ---- M1：128² 小图先验 ----
        sb.AppendLine("\n---- [M1] 128² 小图（先验证再上 256²）----");
        var swSmall = System.Diagnostics.Stopwatch.StartNew();
        var m128 = Build(SEED, 128, 128, 2);
        swSmall.Stop();
        sb.AppendLine($"  生成耗时={swSmall.Elapsed.TotalMilliseconds:0.0} ms");
        sb.Append(ZoneRead(m128, cfg, "128² 气候"));
        sb.Append(ResRead(m128, cfg, 2, "128² 资源"));
        sb.Append(MountainRead(m128, cfg, "128² 山脉"));
        sb.Append(WalkRead(m128, "128² 可走", true));
        sb.Append(ClearZoneRead(m128, cfg, "128² 净空区"));

        // ---- 256² Normal ----
        sb.AppendLine("\n---- [256²/Normal(diff2)] ----");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var mN = Build(SEED, 256, 256, 2);
        sw.Stop();
        sb.AppendLine($"  全管线生成耗时={sw.Elapsed.TotalMilliseconds:0.0} ms（设计 §5.5-5：256² 初始化 < 100ms）");
        sb.Append(ZoneRead(mN, cfg, "256²N 气候"));
        sb.Append(ResRead(mN, cfg, 2, "256²N 资源"));
        sb.Append(MountainRead(mN, cfg, "256²N 山脉"));
        sb.Append(WalkRead(mN, "256²N 可走", true));
        sb.Append(ClearZoneRead(mN, cfg, "256²N 净空区"));

        // ---- 难度三档 ----
        sb.AppendLine("\n---- [验收线 5] 难度三档（同 seed，资源比应 ≈ 0.7 : 1.0 : 1.3）----");
        var counts = new int[3];
        for (int d = 1; d <= 3; d++)
        {
            var md = Build(SEED, 256, 256, d);
            int c = 0;
            for (int i = 0; i < md.features.Length; i++)
                for (int t = 0; t < 5; t++) if (md.features[i] == MapGenRules.ResourceKindFeature[t]) { c++; break; }
            counts[d - 1] = c;
            sb.AppendLine($"  difficulty={d}（{(d == 1 ? "Easy" : d == 2 ? "Normal" : "Hard")}）资源格={c}");
        }
        if (counts[1] > 0)
            sb.AppendLine($"  归一化比 = {counts[0] / (double)counts[1]:0.000} : 1.000 : {counts[2] / (double)counts[1]:0.000}（目标 0.700 : 1.000 : 1.300）");

        // ---- 确定性 ----
        sb.AppendLine("\n---- [验收线 7] 同 seed 逐格一致 ----");
        var a1 = Build(SEED, 256, 256, 2);
        var a2 = Build(SEED, 256, 256, 2);
        sb.AppendLine("  " + (SameMap(a1, a2, out var why) ? "逐格一致 ✅（features + climateZones + spawns）" : $"不一致 ❌ {why}"));
        var a3 = Build(SEED + 1, 256, 256, 2);
        sb.AppendLine("  " + (SameMap(a1, a3, out _) ? "⚠️ 异 seed 出同图（异常）" : "异 seed 出异图 ✅"));

        var dir = Path.Combine(Application.dataPath, "../../Logs");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "hh272_probe.log"), sb.ToString());
        Debug.Log("[HH272] 探针完成 → Logs/hh272_probe.log");
        return sb.ToString();
    }

    /// <summary>Play 态（实机正门）快照：对 WorldManager.Instance.ActiveMap 做同套读数。</summary>
    public static string SnapshotLive()
    {
        var wm = WorldManager.Instance;
        var m = wm != null ? wm.ActiveMap : null;
        if (m == null) return "[HH272] ActiveMap 为空（未进局？）";
        var cfg = Cfg();
        var sb = new StringBuilder();
        sb.AppendLine("======== HH.272 实机快照（WorldManager.Instance.ActiveMap）========");
        sb.AppendLine($"  map {m.width}x{m.height} seed={m.seed} difficulty={(wm != null ? wm.Difficulty : -1)}");
        sb.Append(ZoneRead(m, cfg, "实机 气候"));
        sb.Append(ResRead(m, cfg, wm != null ? wm.Difficulty : 2, "实机 资源"));
        sb.Append(MountainRead(m, cfg, "实机 山脉"));
        sb.Append(WalkRead(m, "实机 可走", true));
        sb.Append(ClearZoneRead(m, cfg, "实机 净空区"));
        var dir = Path.Combine(Application.dataPath, "../../Logs");
        Directory.CreateDirectory(dir);
        File.AppendAllText(Path.Combine(dir, "hh272_probe.log"), sb.ToString());
        return sb.ToString();
    }
}
