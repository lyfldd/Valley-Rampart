using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D 地图生成规则（2_1 §5.2 生成管线）。全部静态方法，注入 System.Random 保证确定性（R4）。
/// features 为唯一功能源（2_1 §1.3）；本类只写 map.features / climateZones / spawns / naturalBuildings，
/// 不实例化 Building（归 2_2）、不渲染（归 2_10）。
///
/// **HH.272 重构（2_1_R1 方案）**：
///   ① 气候层＝种子生长 + 噪声扰动 + 碎片清理（逐格存，替代 16×16 硬方块逐块 roll）
///   ② 资源层＝坑位模型 + 权重表归一化配额（T=120/大区块 × 难度系数）+ 保底 B_i
///   ③ 主城净空区（footprint 3×3 外扩 R=4 ⇒ 11×11）
///   ④ 山脉化（脊线生成 + 沿线扩宽 ⇒ 带状），替代逐格概率散点
///   ⑤ 死链路清理
///   ⑥ 湖/冰河删除（PlaceLakes 整段 + FeatureType.Lake 全量连带）
/// </summary>
public static class MapGenRules
{
    public const int ChunkSize = 16;   // 大区块边长（doc 1 §3.1 固定 16×16）

    // ===== 资源类型位序（与 MapGenRulesConfig.resourceWeights 的 5 位一一对应）=====
    public const int ResTree = 0, ResStone = 1, ResWood = 2, ResOre = 3, ResMine = 4;
    public const int ResourceKindCount = 5;
    /// <summary>位序 0..4 对应的 FeatureType。</summary>
    public static readonly FeatureType[] ResourceKindFeature =
    {
        FeatureType.Tree, FeatureType.StonePile, FeatureType.WoodPile, FeatureType.OreVein, FeatureType.Mine
    };

    // ===== 坑位模型（2_1 §3.4 ／ 2_1_R1 §二第二层 ／ HH.291 A4）=====
    /// <summary>每 Cell 拆 2×2 子坑位（格内**固定偏移** ⇒ 天然不重叠）⇒ **一格最多 4 个资源**。</summary>
    public const int PitsPerCell = 4;

    /// <summary>生成期坑位账：每 Cell 的 2×2 子坑位**占用位掩码**（bit0..3＝子坑位 0..3；0＝无资源）。
    /// 跨 `PlaceResourceQuota`（步骤 4）与 `EnsureChunkResourceQuota`（步骤 6.6）**两趟存活**；
    /// 长度＝`width*height`，由 `FillFeatures` 按图重建。
    /// **口径**：这是**生成期计数器**（`2_1_R1 §二` 验收线 4「坑位不重叠（**计数器实证**）」）——
    /// `MapData.features` 仍是逐格唯一功能源 ⇒ 同格 4 坑位**必须同型**（混合型无法表达，会被静默丢弃）。
    /// **矿洞例外**：`Mine` 为 2×2 Cell 粒度，其 4 格掩码置满（不可再被其他资源占用），
    /// 而**配额结算按 Cell 计**（1 簇＝2×2 Cell＝4 单位，与改前 `TryStampMineCluster` 同口径）。</summary>
    static byte[] _pitMask;

    /// <summary>位掩码中已占坑位数（0..4）。</summary>
    static int PitCountOfMask(int mask)
    {
        int c = 0;
        while (mask != 0) { c += mask & 1; mask >>= 1; }
        return c;
    }

    /// <summary>某 Cell 的生成期坑位数（0..4）；无账（未生成）返 0。</summary>
    static int PitCountOfCell(int cell)
        => _pitMask != null && cell >= 0 && cell < _pitMask.Length ? PitCountOfMask(_pitMask[cell]) : 0;

    /// <summary>**验收读数（生成期口径）**：扫描坑位账 × 当前 `features`，只统计**最终仍为资源格**的 Cell
    /// （净空区/水域清除过的格不计）。`excludeMine=true` 时排除矿洞格（矿洞按 2×2 Cell 例外，其掩码恒满）。
    /// 返回：每 Cell 最大坑位数 / 满格（＝4）计数 / 分布直方图（下标 0..4 的计数）/ 坑位总数。</summary>
    public static void ReadPitStats(MapData map, bool excludeMine,
                                    out int maxPerCell, out int fullCellCount, out int[] histogram, out int totalPits)
    {
        maxPerCell = 0; fullCellCount = 0; totalPits = 0;
        histogram = new int[PitsPerCell + 1];
        if (map == null || map.features == null || _pitMask == null || _pitMask.Length != map.features.Length)
            return;
        for (int i = 0; i < map.features.Length; i++)
        {
            var f = map.features[i];
            bool isRes = false;
            for (int t = 0; t < ResourceKindCount; t++)
                if (f == ResourceKindFeature[t]) { isRes = true; break; }
            int n = isRes ? PitCountOfMask(_pitMask[i]) : 0;
            if (excludeMine && f == FeatureType.Mine) n = 0;
            histogram[Mathf.Clamp(n, 0, PitsPerCell)]++;
            if (n <= 0) continue;
            totalPits += n;
            if (n > maxPerCell) maxPerCell = n;
            if (n >= PitsPerCell) fullCellCount++;
        }
    }

    /// <summary>**验收读数（生成期口径）**：某格当前坑位数（0..4）。</summary>
    public static int PitCountAt(MapData map, int x, int y)
        => map == null || x < 0 || y < 0 || x >= map.width || y >= map.height
           ? 0 : PitCountOfCell(Idx(map, x, y));

    /// <summary>特征物是否可走（生成期判定，未灌 GridSystem 前用）。</summary>
    public static bool IsWalkableFeature(FeatureType f)
    {
        switch (f)
        {
            case FeatureType.Plain: case FeatureType.Tree: case FeatureType.Mine:
            case FeatureType.OreVein: case FeatureType.StonePile: case FeatureType.WoodPile:
                return true;
            default: return false;   // Mountain/SnowMountain/River/Ocean 阻挡
        }
    }

    public static int Idx(MapData m, int x, int y) => y * m.width + x;
    public static int ChunkW(MapData m) => Mathf.Max(1, m.width / ChunkSize);
    public static int ChunkH(MapData m) => Mathf.Max(1, m.height / ChunkSize);

    /// <summary>温度带查询。**HH.272 件①**：`climateZones` 由「按大区块存」改为**逐格存**
    /// （随机形状无法用 `(x/16,y/16)` 反查）；对外签名不变 ⇒ 调用方零改动。</summary>
    public static ClimateZone ZoneOf(MapData m, int x, int y)
        => m.climateZones[y * m.width + x];

    // ===== 件12（D618/DZ-084/D621）：矿山锚点 2×2 成簇工具 =====
    public const int OceanThickness = 2;      // 海洋边缘厚度（PlaceOcean 与资源落位内缩边界共用）
    public const int MineClusterSide = 2;     // 矿山簇边长（对齐 mine.footprint 2×2，D613）

    /// <summary>块是否**完整**由指定特征填满（轴对齐 side×side，D621①：L 形连通不算）。</summary>
    static bool HasFullBlock(MapData map, int ox, int oy, FeatureType need, int side)
    {
        if (ox < 0 || oy < 0 || ox + side > map.width || oy + side > map.height) return false;
        for (int dy = 0; dy < side; dy++)
            for (int dx = 0; dx < side; dx++)
                // 【HH.294 片4补正·R2】裸读收口：改走 `MapGate.ReadAt`（带 `map` 参数的读门）
                if (MapGate.ReadAt(map, ox + dx, oy + dy) != need) return false;
        return true;
    }

    /// <summary>块是否全为 Plain（可盖章空位）。</summary>
    static bool IsClearBlock(MapData map, int ox, int oy, int side)
    {
        if (ox < 0 || oy < 0 || ox + side > map.width || oy + side > map.height) return false;
        for (int dy = 0; dy < side; dy++)
            for (int dx = 0; dx < side; dx++)
                // 【HH.294 片4补正·R2】裸读收口：改走 `MapGate.ReadAt`
                if (MapGate.ReadAt(map, ox + dx, oy + dy) != FeatureType.Plain) return false;
        return true;
    }

    static void StampBlock(MapData map, int ox, int oy, FeatureType need, int side)
    {
        for (int dy = 0; dy < side; dy++)
            for (int dx = 0; dx < side; dx++)
                MapGate.GenesisWrite(map, Idx(map, ox + dx, oy + dy), need);   // 【HH.294 片4·4-A】造世界口（0→1，不算增）
    }

    /// <summary>某格是否处于「完整 side×side 同特征块」内（D618 验收：无孤立矿山格 的判定反向口径）。</summary>
    public static bool InFullBlock(MapData map, int x, int y, FeatureType need, int side)
    {
        for (int oy = -(side - 1); oy <= 0; oy++)
            for (int ox = -(side - 1); ox <= 0; ox++)
                if (HasFullBlock(map, x + ox, y + oy, need, side)) return true;
        return false;
    }

    // ========================================================================
    //  步骤 3：气候层——群系形状（种子生长 + 噪声扰动 + 碎片清理）
    // ========================================================================

    public static void FillClimateZones(System.Random rng, MapData map, MapGenRulesConfig cfg)
    {
        int w = map.width, h = map.height, n = w * h;
        var zones = map.climateZones;
        if (zones == null || zones.Length != n)
            throw new InvalidOperationException(
                $"[MapGenRules] climateZones 须为**逐格**数组（期望 {n}，实得 {zones?.Length ?? 0}）——见 HH.272 件①。" +
                "大区块口径已废止（2_1_R1 §二第一层）。");

        int typMin = cfg != null ? Mathf.Max(1, cfg.clusterSizeTypicalMin) : 96;
        int typMax = cfg != null ? Mathf.Max(typMin, cfg.clusterSizeTypicalMax) : 384;
        int maxSize = cfg != null ? Mathf.Max(typMax, cfg.clusterSizeMax) : 384;
        int minSize = cfg != null ? Mathf.Max(1, cfg.clusterSizeMin) : 4;

        // 低频噪声场（确定性·不消耗 rng 主链之外的语义）
        var noise = BuildNoiseField(rng, w, h);

        // ---- 1) 种子数 = 面积 / 平均簇大小；配额 = 面积 / 种子数（归一化 ⇒ Σ配额 = n）----
        int avg = Mathf.Max(1, (typMin + typMax) / 2);
        int seedCount = Mathf.Max(1, n / avg);
        int baseQuota = n / seedCount;
        int remQuota = n - baseQuota * seedCount;      // 余数摊给前几个种子（Σ配额 ≡ n）

        var lab = new int[n];
        for (int i = 0; i < n; i++) lab[i] = -1;

        var cBand = new List<int>(seedCount);          // 每簇温度带
        var cSize = new List<int>(seedCount);          // 每簇体积
        var cQuota = new List<int>(seedCount);         // 每簇目标体积（want 停靠）
        var cFront = new List<List<int>>(seedCount);
        var active = new Queue<int>();
        var stamp = new int[n];                        // 前沿去重：某格已入 owner 前沿 ⇒ 不再重复入

        // 去重版前沿入队：只入「自由且未在本簇前沿」的邻格 ⇒ frontier 保持唯一（PickByNoise 更快）
        // 标记 = owner+1（stamp 初值 0 与簇 0 冲突）
        void PushF(List<int> f, int cell, int owner)
        {
            int o = owner + 1;
            int cx = cell % w, cy = cell / w;
            if (cx + 1 < w) { int ni = cy * w + cx + 1; if (lab[ni] == -1 && stamp[ni] != o) { stamp[ni] = o; f.Add(ni); } }
            if (cx - 1 >= 0) { int ni = cy * w + cx - 1; if (lab[ni] == -1 && stamp[ni] != o) { stamp[ni] = o; f.Add(ni); } }
            if (cy + 1 < h) { int ni = (cy + 1) * w + cx; if (lab[ni] == -1 && stamp[ni] != o) { stamp[ni] = o; f.Add(ni); } }
            if (cy - 1 >= 0) { int ni = (cy - 1) * w + cx; if (lab[ni] == -1 && stamp[ni] != o) { stamp[ni] = o; f.Add(ni); } }
        }

        int Spawn(int cell, int band, int quota)
        {
            int cid = cBand.Count;
            cBand.Add(band);
            cSize.Add(1);
            cQuota.Add(quota);
            var f = new List<int>(64);
            cFront.Add(f);
            lab[cell] = cid;
            PushF(f, cell, cid);
            active.Enqueue(cid);
            return cid;
        }

        // ---- 1b) 种子均匀撒布（抖动网格 ⇒ 无种子死区 ⇒ 全图可达；替代纯随机散点）----
        //   **强 4-着色**：同带种子在 3×3 邻域内（直邻+对角）必不同带 ⇒ 同带簇相隔 ≥ 一格异带 ⇒
        //   生长期同带簇不接触 ⇒ 无「同带缝隙」，各簇独立按配额长满。
        int gx = (int)Mathf.Sqrt(seedCount), gy = gx;
        if (gx * gy < seedCount) gx++;          // 网格容量 ≥ seedCount（≥1 槽/种子）
        if (gx * gy < seedCount) gy++;
        float stepX = w / (float)gx, stepY = h / (float)gy;
        int jitterX = Mathf.Max(1, (int)(stepX * 0.4f));
        int jitterY = Mathf.Max(1, (int)(stepY * 0.4f));
        var seedBands = new int[seedCount];
        for (int s = 0; s < seedCount; s++)
        {
            var used = new bool[4];
            if (s % gx > 0) used[seedBands[s - 1]] = true;        // 左邻
            if (s >= gx) used[seedBands[s - gx]] = true;          // 上邻
            if (s >= gx && s % gx > 0) used[seedBands[s - gx - 1]] = true;   // 左上对角
            if (s >= gx && s % gx < gx - 1) used[seedBands[s - gx + 1]] = true; // 右上对角
            seedBands[s] = RollClimateExcluding(rng, cfg, used);
        }
        for (int s = 0; s < seedCount; s++)
        {
            int ix = s % gx, iy = s / gx;
            int sx = Mathf.Clamp((int)((ix + 0.5f) * stepX) + rng.Next(-jitterX, jitterX + 1), 0, w - 1);
            int sy = Mathf.Clamp((int)((iy + 0.5f) * stepY) + rng.Next(-jitterY, jitterY + 1), 0, h - 1);
            int start = sy * w + sx;
            if (lab[start] != -1) start = FindNearestFree(lab, w, h, sx, sy);
            if (start < 0) break;
            Spawn(start, seedBands[s], baseQuota + (s < remQuota ? 1 : 0));
        }

        // ---- 2) 轮转生长：两段式（同带并入封顶校验贯穿）。
        //   Pass A（want 停靠）：各簇长到配额即停 ⇒ 体积均衡 ≈ n/seedCount ≈ 240，落在典型区间 [96,384] 内。
        //   Pass B（maxSize 封顶）：**配额已满但前沿未空的簇重新激活**，以竞争方式接管残留格
        //     （余量 ≈ 384−240，充足 ⇒ 无需兜底合并 ⇒ 不产超限巨簇）。
        //   噪声决定扩张优先级 ⇒ 边界自然蜿蜒。----
        void GrowthPass(int cap)
        {
            active.Clear();
            for (int c = 0; c < cBand.Count; c++) if (cSize[c] < cap) active.Enqueue(c);
            while (active.Count > 0)
            {
                int cid = active.Dequeue();
                if (cSize[cid] >= cap) continue;           // 到停靠线 ⇒ 退役
                var f = cFront[cid];
                int placed = -1;
                while (f.Count > 0)
                {
                    int pick = PickByNoise(rng, f, noise);
                    int cell = f[pick];
                    f[pick] = f[f.Count - 1];
                    f.RemoveAt(f.Count - 1);
                    if (lab[cell] != -1) continue;         // 已被别簇占走
                    if (MergedSizeIfClaim(lab, w, h, cell, cid, cBand, cSize) > maxSize) continue;
                    placed = cell; break;
                }
                if (placed < 0) continue;                  // 前沿耗尽 ⇒ 该簇退役（残留交下一段/兜底）
                lab[placed] = cid; cSize[cid]++;
                PushF(f, placed, cid);
                if (cSize[cid] < cap) active.Enqueue(cid);
            }
        }
        GrowthPass(baseQuota);                               // Pass A 配额均衡（封顶 240）
        GrowthPass(maxSize);                                 // Pass B 接管残留（封顶 384）

        // ---- 3) 残隙收尾（BFS）：每个空格并入「邻接格数最多」且并入后 ≤ 上限的邻接簇；
        //      全超限 ⇒ 取**并后体积最小**者（短暂超限且分散，交步骤 4 裁剪）。全图无未分配、无新碎片簇。----
        {
            var q = new Queue<int>();
            var seen = new bool[n];
            var nbIds = new List<int>(4);
            for (int i = 0; i < n; i++) if (lab[i] != -1) PushUnassignedNeighbors(q, seen, lab, w, h, i);
            while (q.Count > 0)
            {
                int cell = q.Dequeue();
                if (lab[cell] != -1) continue;
                int cx = cell % w, cy = cell / w;
                nbIds.Clear();
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int id = lab[ny * w + nx];
                    if (id < 0) continue;
                    if (!nbIds.Contains(id)) nbIds.Add(id);
                }
                int best = -1, bestCnt = -1; long bestMerged = long.MaxValue;
                for (int k = 0; k < nbIds.Count; k++)
                {
                    int id = nbIds[k];
                    int cnt = 0;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                        int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int nid = lab[ny * w + nx];
                        if (nid == id) cnt++;
                    }
                    long m = MergedSizeIfClaim(lab, w, h, cell, id, cBand, cSize);
                    if (m <= maxSize && (cnt > bestCnt || (cnt == bestCnt && m < bestMerged)))
                    { best = id; bestCnt = cnt; bestMerged = m; }
                }
                if (best < 0)
                {
                    // 全超限 ⇒ 并后体积最小者（分散超限交裁剪）
                    best = -1; bestMerged = long.MaxValue;
                    for (int k = 0; k < nbIds.Count; k++)
                    {
                        long m = MergedSizeIfClaim(lab, w, h, cell, nbIds[k], cBand, cSize);
                        if (m < bestMerged) { bestMerged = m; best = nbIds[k]; }
                    }
                }
                if (best < 0) { zones[cell] = ClimateZone.Temperate; continue; }   // 理论不可达
                lab[cell] = best;
                cSize[best]++;
                PushUnassignedNeighbors(q, seen, lab, w, h, cell);
            }
        }

        // 写回逐格温度带
        int leftover = 0;
        for (int i = 0; i < n; i++)
        {
            if (lab[i] < 0) { leftover++; zones[i] = ClimateZone.Temperate; continue; }
            zones[i] = (ClimateZone)cBand[lab[i]];
        }
        if (leftover > 0)
            Debug.LogWarning($"[MapGenRules] 气候层生长残留未分配={leftover}（期望 0·HH.272 件①）。");

        // ---- 4) 收尾至不动点：碎片并入（&lt; minSize 整簇并入邻接最多带）↔ 上限裁剪
        //      （裁剪可能切出碎片、并入可能撑超 ⇒ 交替；当前碎片/超限极少 ⇒ 1~3 轮即收敛）。
        //      判停用返回值（merged==0 且 rounds==0 ⇒ 干净），省去每轮多余的连通性复检。----
        int capRounds = 0;
        for (int outer = 0; outer < 8; outer++)
        {
            int merged = MergeFragments(zones, w, h, minSize, maxSize);
            capRounds = CapOversizedClusters(zones, w, h, maxSize);
            if (merged == 0 && capRounds == 0) break;
        }

        ReportClimateShape(zones, w, h, minSize, maxSize, capRounds, 0);
    }

    /// <summary>「把 cell 并入 cid」后的分量体积：自身 + 四邻**同带**簇体积（按簇 id 去重）。
    /// &gt; 上限 ⇒ 该格须拒绝（否则同带两簇被一格桥接成一簇 ⇒ 超限）。</summary>
    static long MergedSizeIfClaim(int[] lab, int w, int h, int cell, int cid, List<int> cBand, List<int> cSize)
    {
        int cx = cell % w, cy = cell / w;
        int band = cBand[cid];
        long m = cSize[cid] + 1;
        var ids = new int[4]; int nn = 0;
        for (int d = 0; d < 4; d++)
        {
            int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
            int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
            int id = lab[ny * w + nx];
            if (id < 0 || id == cid) continue;
            if (cBand[id] != band) continue;
            bool dup = false;
            for (int k = 0; k < nn; k++) if (ids[k] == id) { dup = true; break; }
            if (dup) continue;
            ids[nn++] = id;
            m += cSize[id];
        }
        return m;
    }

    /// <summary>低频噪声场（值域 0..1）：多分量正弦叠加，波长 ~80~300 格 ⇒ 决定生长优先级。</summary>
    static float[] BuildNoiseField(System.Random rng, int w, int h)
    {
        const int comps = 3;
        var fx = new float[comps]; var fy = new float[comps];
        var ph = new float[comps]; var am = new float[comps];
        for (int c = 0; c < comps; c++)
        {
            fx[c] = (float)(rng.NextDouble() * 0.055 + 0.018);
            fy[c] = (float)(rng.NextDouble() * 0.055 + 0.018);
            ph[c] = (float)(rng.NextDouble() * Math.PI * 2.0);
            am[c] = 1f / (c + 1);
        }
        var f = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = 0f, tw = 0f;
                for (int c = 0; c < comps; c++)
                {
                    v += am[c] * (float)Math.Sin(x * fx[c] + y * fy[c] + ph[c]);
                    tw += am[c];
                }
                f[y * w + x] = Mathf.Clamp01((v / tw) * 0.5f + 0.5f);
            }
        return f;
    }

    /// <summary>噪声加权挑 frontier 项（权重 = (noise+0.05)^3）。frontier 大时抽样 64 个取最优（保 O(1)）。</summary>
    static int PickByNoise(System.Random rng, List<int> frontier, float[] noise)
    {
        int cnt = frontier.Count;
        if (cnt <= 512)
        {
            float sum = 0f;
            for (int i = 0; i < cnt; i++) { float t = noise[frontier[i]] + 0.05f; sum += t * t * t; }
            float roll = (float)rng.NextDouble() * sum;
            for (int i = 0; i < cnt; i++)
            {
                float t = noise[frontier[i]] + 0.05f;
                roll -= t * t * t;
                if (roll <= 0f) return i;
            }
            return cnt - 1;
        }
        int best = rng.Next(cnt); float bestV = noise[frontier[best]];
        for (int k = 0; k < 64; k++)
        {
            int idx = rng.Next(cnt);
            float v = noise[frontier[idx]];
            if (v > bestV) { bestV = v; best = idx; }
        }
        return best;
    }

    static int FindNearestFree(int[] label, int w, int h, int cx, int cy)
    {
        if (label[cy * w + cx] == -1) return cy * w + cx;
        int maxR = Mathf.Max(w, h);
        for (int r = 1; r <= maxR; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    int i = y * w + x;
                    if (label[i] == -1) return i;
                }
        return -1;
    }

    /// <summary>把自由邻格入队（残隙 BFS 扩散用；seen 防重复入队）。</summary>
    static void PushUnassignedNeighbors(Queue<int> q, bool[] seen, int[] label, int w, int h, int cell)
    {
        int cx = cell % w, cy = cell / w;
        for (int d = 0; d < 4; d++)
        {
            int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
            int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
            int ni = ny * w + nx;
            if (label[ni] != -1 || seen[ni]) continue;
            seen[ni] = true; q.Enqueue(ni);
        }
    }

    // ---- 4-连通簇标注（气候层公用）----

    static int[] _labelQueue;   // LabelComponents 复用 BFS 缓冲（MapGen 单线程串行 ⇒ 安全复用）

    static int LabelComponents(ClimateZone[] zones, int w, int h, int[] label, List<int> compSize, List<ClimateZone> compBand)
    {
        int n = w * h;
        for (int i = 0; i < n; i++) label[i] = -1;
        compSize.Clear(); compBand.Clear();
        if (_labelQueue == null || _labelQueue.Length < n) _labelQueue = new int[n];
        var q = _labelQueue;
        int cc = 0;
        for (int i = 0; i < n; i++)
        {
            if (label[i] != -1) continue;
            var band0 = zones[i];
            int c = cc++;
            label[i] = c;
            int head = 0, tail = 0;
            q[tail++] = i;
            int sz = 0;
            while (head < tail)
            {
                int cur = q[head++]; sz++;
                int cx = cur % w, cy = cur / w;
                if (cx + 1 < w) { int ni = cur + 1; if (label[ni] == -1 && zones[ni] == band0) { label[ni] = c; q[tail++] = ni; } }
                if (cx > 0)     { int ni = cur - 1; if (label[ni] == -1 && zones[ni] == band0) { label[ni] = c; q[tail++] = ni; } }
                if (cy + 1 < h) { int ni = cur + w; if (label[ni] == -1 && zones[ni] == band0) { label[ni] = c; q[tail++] = ni; } }
                if (cy > 0)     { int ni = cur - w; if (label[ni] == -1 && zones[ni] == band0) { label[ni] = c; q[tail++] = ni; } }
            }
            compSize.Add(sz); compBand.Add(band0);
        }
        return cc;
    }

    /// <summary>碎片并入：size &lt; minSize 的 4-连通簇整簇并入邻接带。
    /// **封顶感知**：目标带「并入后 Σ(同带邻接分量体积) + 碎片体积 ≤ 上限」优先（体积最小者）；
    /// 全超限 ⇒ 取同带邻接体积最小者（接受短暂超限，交裁剪）⇒ 减少并入→裁剪振荡。
    /// 单遍 O(n)（先收集碎片格，再逐碎片统计邻接分量）。</summary>
    static int MergeFragments(ClimateZone[] zones, int w, int h, int minSize, int maxSize)
    {
        int n = w * h;
        var label = new int[n];
        var sz = new List<int>();
        var bd = new List<ClimateZone>();
        int cc = LabelComponents(zones, w, h, label, sz, bd);
        if (cc == 0) return 0;

        var fragCells = new List<int>[cc];
        for (int i = 0; i < n; i++)
        {
            int c = label[i];
            if (sz[c] >= minSize) continue;
            if (fragCells[c] == null) fragCells[c] = new List<int>(8);
            fragCells[c].Add(i);
        }

        var target = new int[cc];
        for (int c = 0; c < cc; c++) target[c] = -1;
        int merged = 0;
        var nbComp = new List<int>(4);
        for (int c = 0; c < cc; c++)
        {
            var cells = fragCells[c];
            if (cells == null) continue;
            merged++;
            nbComp.Clear();
            for (int k = 0; k < cells.Count; k++)
            {
                int i = cells[k];
                int cx = i % w, cy = i / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int nc = label[ny * w + nx];
                    if (nc == c) continue;
                    if (!nbComp.Contains(nc)) nbComp.Add(nc);
                }
            }
            var bandVol = new long[4];
            for (int k = 0; k < nbComp.Count; k++) bandVol[(int)bd[nbComp[k]]] += sz[nbComp[k]];
            int best = -1; long bestVol = long.MaxValue;
            for (int b = 0; b < 4; b++)
            {
                if (bandVol[b] <= 0) continue;
                if (bandVol[b] + cells.Count <= maxSize && bandVol[b] < bestVol) { best = b; bestVol = bandVol[b]; }
            }
            if (best < 0)
            {
                bestVol = long.MaxValue;
                for (int b = 0; b < 4; b++)
                    if (bandVol[b] > 0 && bandVol[b] < bestVol) { bestVol = bandVol[b]; best = b; }
            }
            if (best < 0) best = 0;
            target[c] = best;
        }

        for (int i = 0; i < n; i++)
        {
            int t = target[label[i]];
            if (t >= 0) zones[i] = (ClimateZone)t;
        }
        return merged;
    }

    /// <summary>上限裁剪（设计「任一簇 &gt; 384 ⇒ 切分」）：每轮对**全部超限簇**的边界格，逐格转移给
    /// 「异带且接收后 ≤ 上限」的邻接分量（体积最小者优先 ⇒ 分散、不撑爆单邻带）；
    /// 全不可 ⇒ 取体积最小邻带（短暂超限交下一轮）。转移格必邻接目标带 ⇒ 不产生新碎片。
    /// 单轮内 sz 为快照（目标可能被多格撑超 ⇒ 下轮再处理）⇒ 典型 1~3 轮收敛。
    /// **（本版为唯一干净收敛版：曾试桥格切割/叶格优先/活体积追踪，均致碎片残留或 512 轮卡死，HH.272 复盘）**</summary>
    static int CapOversizedClusters(ClimateZone[] zones, int w, int h, int maxSize)
    {
        int n = w * h;
        var label = new int[n];
        var sz = new List<int>();
        var bd = new List<ClimateZone>();

        for (int iter = 0; iter < 512; iter++)
        {
            int cc = LabelComponents(zones, w, h, label, sz, bd);
            bool hasOver = false;
            for (int c = 0; c < cc; c++) if (sz[c] > maxSize) { hasOver = true; break; }
            if (!hasOver) return iter;

            int moved = 0;
            for (int i = 0; i < n; i++)
            {
                int ci = label[i];
                if (sz[ci] <= maxSize) continue;          // 非超限簇的格不动
                int cx = i % w, cy = i / w;
                // 第一优先：异带且接收后仍 ≤ 上限 的分量（体积最小者 ⇒ 分散）
                int bestB = -1; long bestSz = long.MaxValue;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    int cn = label[ni];
                    if (cn == ci) continue;
                    long s = sz[cn];
                    if (s + 1 <= maxSize && s < bestSz) { bestSz = s; bestB = (int)bd[cn]; }
                }
                if (bestB < 0)
                {
                    // 全不可 ⇒ 取体积最小异带分量（短暂超限交下一轮）
                    bestSz = long.MaxValue;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                        int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int ni = ny * w + nx;
                        int cn = label[ni];
                        if (cn == ci) continue;
                        long s = sz[cn];
                        if (s < bestSz) { bestSz = s; bestB = (int)bd[cn]; }
                    }
                }
                if (bestB < 0) continue;                  // 无邻带（理论不可达）
                zones[i] = (ClimateZone)bestB;
                moved++;
            }
            if (moved == 0) return iter;                  // 无法切（防御）
        }
        return 512;
    }

    /// <summary>形状自检（可证伪点）：碎片/超限簇计数，非 0 即告警。</summary>
    static void ReportClimateShape(ClimateZone[] zones, int w, int h, int minSize, int maxSize, int capRounds, int outerRounds)
    {
        int[] label = new int[w * h];
        var sz = new List<int>(); var bd = new List<ClimateZone>();
        int cc = LabelComponents(zones, w, h, label, sz, bd);
        int frag = 0, over = 0, maxSeen = 0;
        for (int c = 0; c < cc; c++)
        {
            if (sz[c] < minSize) frag++;
            if (sz[c] > maxSize) over++;
            if (sz[c] > maxSeen) maxSeen = sz[c];
        }
        if (frag > 0 || over > 0)
            Debug.LogWarning($"[MapGenRules] 气候层形状自检：簇={cc} 碎片(<{minSize})={frag} 超限(>{maxSize})={over} 最大={maxSeen} 切割轮={capRounds} 外层轮={outerRounds}（期望 碎片/超限 均为 0，HH.272 件①）。");
        else
            Debug.Log($"[MapGenRules] 气候层形状自检通过：簇={cc} 最大={maxSeen} 切割轮={capRounds} 外层轮={outerRounds}（碎片 0／超限 0，HH.272 件①）。");
    }

    /// <summary>排除指定带后按 climateWeights 加权摇温度带（HH.272 件① 种子 4-着色用）。
    /// 全部被禁（防御）⇒ 取第一个未禁带。</summary>
    static int RollClimateExcluding(System.Random rng, MapGenRulesConfig cfg, bool[] exclude)
    {
        float total = 0f; var wts = new float[4];
        for (int b = 0; b < 4; b++)
        {
            float w = cfg != null ? cfg.GetClimateWeight((ClimateZone)b) : 1f;
            wts[b] = (exclude[b] || w <= 0f) ? 0f : w;
            total += wts[b];
        }
        if (total <= 0f)
        {
            for (int b = 0; b < 4; b++) if (!exclude[b]) return b;
            return 0;
        }
        float roll = (float)rng.NextDouble() * total;
        for (int b = 0; b < 4; b++) { roll -= wts[b]; if (roll <= 0f) return b; }
        return 3;
    }

    /// <summary>大区块的主导温度带（该区块内出现格数最多者；并列取固定序）。配额表按此查。</summary>
    public static ClimateZone DominantZoneOfChunk(MapData map, int cx, int cy)
    {
        var cnt = new int[4];
        int x0 = cx * ChunkSize, y0 = cy * ChunkSize;
        for (int y = y0; y < y0 + ChunkSize && y < map.height; y++)
            for (int x = x0; x < x0 + ChunkSize && x < map.width; x++)
                cnt[(int)ZoneOf(map, x, y)]++;
        int best = 0;
        for (int b = 1; b < 4; b++) if (cnt[b] > cnt[best]) best = b;
        return (ClimateZone)best;
    }

    // ========================================================================
    //  步骤 4：特征物填充（山脉化 ＋ 坑位模型 ＋ 归一化配额 ＋ 保底）
    // ========================================================================

    /// <summary>特征物填充。**HH.272 件②④**：① 山脉化（脊线 + 扩宽 ⇒ 带状，替代逐格概率散点）；
    /// ② 资源按「坑位模型 + 权重表归一化配额」落位（`cap = poolCapBase × 丰度 × 难度`，开局目标 `E_i^open`，保底 B_i）。</summary>
    public static void FillFeatures(System.Random rng, MapData map, MapGenRulesConfig cfg, int difficulty)
    {
        int n = map.width * map.height;
        for (int i = 0; i < n; i++) MapGate.GenesisWrite(map, i, FeatureType.Plain);   // 【HH.294 片4·4-A】造世界口
        _pitMask = new byte[n];                      // HH.291 A4：坑位账按图重建（生成期口径）

        PlaceMountainRidges(rng, map, cfg);          // 件④：山脉化
        PruneMountainSpecks(map, cfg);               // 件④：山脉簇最小尺寸约束（≥4 格）
        PlaceResourceQuota(rng, map, cfg, difficulty, skipClearZone: false);   // 件②：配额落格
    }

    /// <summary>山脉化（件④）：按温度带「总格数占比」反推脊线条数（脊线数 = 目标格数 / (平均宽度 × 平均长度)），
    /// 每条脊线为 4-邻域折线（走向随机 + 长度受控），沿线按随机宽度向两侧扩宽 ⇒ 带状山体；
    /// 相邻脊线自然相接 ⇒ 合并为连绵大山脉。密度：寒带最多、热带最少（`mountainCellRatio`）。</summary>
    static void PlaceMountainRidges(System.Random rng, MapData map, MapGenRulesConfig cfg)
    {
        int cw = ChunkW(map), ch = ChunkH(map);
        int wMin = cfg != null ? Mathf.Max(1, cfg.mountainRidgeWidthMin) : 1;
        int wMax = cfg != null ? Mathf.Max(wMin, cfg.mountainRidgeWidthMax) : 2;
        int lMin = cfg != null ? Mathf.Max(2, cfg.mountainRidgeLengthMin) : 6;
        int lMax = cfg != null ? Mathf.Max(lMin, cfg.mountainRidgeLengthMax) : 20;
        float avgW = (wMin + wMax) * 0.5f;
        float avgL = (lMin + lMax) * 0.5f;

        for (int cy = 0; cy < ch; cy++)
            for (int cx = 0; cx < cw; cx++)
            {
                var band = DominantZoneOfChunk(map, cx, cy);
                float ratio = cfg != null ? cfg.GetMountainCellRatio(band) : 0.1f;
                if (ratio <= 0f) continue;
                int targetCells = Mathf.RoundToInt(ChunkSize * ChunkSize * ratio);
                int ridges = Mathf.Max(1, Mathf.RoundToInt(targetCells / (avgW * avgL)));

                for (int r = 0; r < ridges; r++)
                {
                    // 起点/行进范围对海洋带内缩（`OceanThickness+1`）⇒ PlaceOcean 不会切碎山脊（件④·防碎片）
                    int inset = OceanThickness + 1;
                    int x = cx * ChunkSize + rng.Next(inset, ChunkSize - inset);
                    int y = cy * ChunkSize + rng.Next(inset, ChunkSize - inset);
                    int dir = rng.Next(4);                       // 0=+x 1=-x 2=+y 3=-y
                    int len = rng.Next(lMin, lMax + 1);
                    for (int step = 0; step < len; step++)
                    {
                        if (rng.NextDouble() < 0.22) dir = rng.Next(4);   // 折线转弯 ⇒ 走向自然
                        int width = rng.Next(wMin, wMax + 1);
                        for (int t = 0; t < width; t++)
                        {
                            int px = x + (dir <= 1 ? 0 : t);
                            int py = y + (dir >= 2 ? 0 : t);
                            StampMountain(rng, map, px, py, cfg);
                        }
                        if (dir == 0) x++; else if (dir == 1) x--;
                        else if (dir == 2) y++; else y--;
                        if (x < inset || y < inset || x >= map.width - inset || y >= map.height - inset) break;
                    }
                }
            }
    }

    /// <summary>盖章山体：按**该格自身温度带**决定 Mountain / SnowMountain（寒带全雪、热带无雪）。
    /// 判定随机取注入的 <paramref name="rng"/>（同 seed 逐格一致 · 验收线 7）。</summary>
    static void StampMountain(System.Random rng, MapData map, int x, int y, MapGenRulesConfig cfg)
    {
        if (x < 0 || y < 0 || x >= map.width || y >= map.height) return;
        var band = ZoneOf(map, x, y);
        float snow = cfg != null ? cfg.GetMountainSnowRatio(band) : (band == ClimateZone.Cold ? 1f : 0.3f);
        MapGate.GenesisWrite(map, Idx(map, x, y),
            rng.NextDouble() < snow ? FeatureType.SnowMountain : FeatureType.Mountain);   // 【HH.294 片4·4-A】造世界口
    }

    /// <summary>山脉簇最小尺寸约束（件④）：4-连通簇 &lt; `mountainClusterMinSize` 的山体碎片回落 Plain。</summary>
    static void PruneMountainSpecks(MapData map, MapGenRulesConfig cfg)
    {
        int minSize = cfg != null ? Mathf.Max(1, cfg.mountainClusterMinSize) : 4;
        int w = map.width, h = map.height, n = w * h;
        var isMt = new bool[n];
        for (int i = 0; i < n; i++)
            isMt[i] = map.features[i] == FeatureType.Mountain || map.features[i] == FeatureType.SnowMountain;
        var label = new int[n];
        for (int i = 0; i < n; i++) label[i] = -1;
        var q = new Queue<int>();
        int pruned = 0;
        for (int i = 0; i < n; i++)
        {
            if (!isMt[i] || label[i] != -1) continue;
            label[i] = 0; q.Clear(); q.Enqueue(i);
            var cells = new List<int>();
            while (q.Count > 0)
            {
                int cur = q.Dequeue(); cells.Add(cur);
                int cx = cur % w, cy = cur / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (!isMt[ni] || label[ni] != -1) continue;
                    label[ni] = 0; q.Enqueue(ni);
                }
            }
            if (cells.Count < minSize)
            {
                for (int k = 0; k < cells.Count; k++) { MapGate.GenesisWrite(map, cells[k], FeatureType.Plain); pruned++; }   // 【HH.294 片4·4-A】造世界口
            }
        }
        if (pruned > 0)
            Debug.Log($"[MapGenRules] 山脉化：清除 <{minSize} 格山体碎片 {pruned} 格（件④·4-连通最小尺寸约束）。");
    }

    /// <summary>资源配额落格（件②）。每大区块：`cap = poolCapBase × 地带丰度 × 难度系数`（`D774`/`D773`）；
    /// 权重表归一化 `p_i=(1−w_i)/Σ(1−w_j)`；**落格目标＝开局目标** `E_i^open = cap × p_i × poolOpenRatio`；
    /// 保底 `B_i = floor(E_i^open × guaranteeRatio)`（矿洞 ＝ 0）。
    /// **坑位模型（HH.291 A4）**：每 Cell 拆 2×2 子坑位（格内固定偏移·天然不重叠），**一格最多 4 个资源**
    /// （同格**同型**——features 逐格唯一功能源）；矿洞例外＝**2×2 Cell 粒度 ＝ 1 点**（先占整块，再在剩余 Cell 填坑位）。</summary>
    static void PlaceResourceQuota(System.Random rng, MapData map, MapGenRulesConfig cfg, int difficulty, bool skipClearZone)
    {
        int cw = ChunkW(map), ch = ChunkH(map);
        var kindCount = new int[ResourceKindCount];
        var candidates = new List<int>(ChunkSize * ChunkSize);
        var e = new float[ResourceKindCount];
        var b = new int[ResourceKindCount];
        var kindCap = new float[ResourceKindCount];
        var share = new float[ResourceKindCount];

        for (int cy = 0; cy < ch; cy++)
            for (int cx = 0; cx < cw; cx++)
            {
                for (int t = 0; t < ResourceKindCount; t++) kindCount[t] = 0;
                var band = DominantZoneOfChunk(map, cx, cy);
                ComputeQuota(cfg, band, difficulty, e, b, kindCap, share, out _);

                // 候选 Cell：Plain ＋ 避开海洋带（PlaceOcean 后不会被覆写）＋ （保底时）避开主城净空区
                candidates.Clear();
                int x0 = cx * ChunkSize, y0 = cy * ChunkSize;
                for (int y = y0; y < y0 + ChunkSize && y < map.height; y++)
                    for (int x = x0; x < x0 + ChunkSize && x < map.width; x++)
                    {
                        if (x < OceanThickness || y < OceanThickness
                            || x >= map.width - OceanThickness || y >= map.height - OceanThickness) continue;
                        if (map.features[Idx(map, x, y)] != FeatureType.Plain) continue;
                        if (skipClearZone && IsInKingdomClearZone(map, x, y, cfg)) continue;
                        candidates.Add(Idx(map, x, y));
                    }
                Shuffle(rng, candidates);
                int cursor = 0;

                // 矿洞：2×2 整块优先（先占整块，再在剩余 Cell 填坑位）
                // **HH.293 B3（D739）**：删「每大区块恒 ≥1 簇」地板（原 `if (mineClusters <= 0 && b[ResMine] > 0) mineClusters = 1;`）
                //   —— 该地板把矿山锚点密度锁死在 1 簇/区块（4 格/256 格），与权重无关；拆除后可为 0。
                // ⚠️ 本项**只拆下限、不承诺"最少"**（要达成「4× 视域 2~3 个」须靠 `R-03` 方案 A′）。
                // **HH.294 片 5**：`e[ResMine]` 单位＝**点**（1 簇 ＝ 1 点）⇒ 直接取整，不再 `÷4`（改动前 `÷4` 系「簇＝4 坑位」旧口径）。
                int mineClusters = Mathf.RoundToInt(e[ResMine]);
                for (int k = 0; k < mineClusters; k++)
                    if (TryStampMineCluster(map, candidates, rng, cfg)) kindCount[ResMine]++;

                // 其余资源：按**型**打包落位（HH.291 A4 坑位模型：每 Cell 至多 4 个**同型**坑位；
                //   features 为逐格唯一功能源 ⇒ 同格必须同型，混合型无法表达）。
                //   空格仍由 candidates（已 shuffle）供位 ⇒ 空间散布不变。
                for (int t = 0; t < ResMine; t++)
                {
                    int remaining = Mathf.RoundToInt(e[t]);
                    while (remaining > 0)
                    {
                        // 跳过被矿洞占掉/已落资源的候选格
                        while (cursor < candidates.Count && map.features[candidates[cursor]] != FeatureType.Plain) cursor++;
                        if (cursor >= candidates.Count) break;
                        int cell = candidates[cursor++];
                        int slots = Mathf.Min(PitsPerCell, remaining);
                        PlaceResourceCell(map, cell, t, slots, rng);
                        kindCount[t] += slots;
                        remaining -= slots;
                    }
                }

                // 保底 B_i：逐区块统计实际数量（**点口径**：非矿＝坑位；矿洞＝簇），不足则补足（矿洞按簇补）
                for (int t = 0; t < ResourceKindCount; t++)
                {
                    if (b[t] <= 0) continue;
                    int have = kindCount[t];   // 本片起矿洞亦以「点（簇）」计 ⇒ 不再 ×4
                    int need = b[t] - have;
                    while (need > 0)
                    {
                        if (t == ResMine)
                        {
                            if (!TryStampMineCluster(map, candidates, rng, cfg)) break;
                            kindCount[ResMine]++; need -= 1;
                        }
                        else
                        {
                            while (cursor < candidates.Count && map.features[candidates[cursor]] != FeatureType.Plain) cursor++;
                            if (cursor >= candidates.Count) break;
                            int cell = candidates[cursor++];
                            int slots = Mathf.Min(PitsPerCell, need);
                            PlaceResourceCell(map, cell, t, slots, rng);
                            kindCount[t] += slots; need -= slots;
                        }
                    }
                }
            }
    }

    /// <summary>坑位选位：每 Cell 拆 2×2 子坑位（格内固定偏移）。返回子坑位序号 0..3。</summary>
    static int PickSubSlot(System.Random rng) => rng.Next(4);

    /// <summary>**落一格 N 个坑位**（N∈1..4·同型·HH.291 A4）：`PickSubSlot` 的返回值**真正参与落位**——
    /// 以返回序号为起点占一个**未占用**子坑位（已占则顺移到下一个），回合内 `slots` 次 ⇒ 坑位天然不重叠。
    /// 写入 `features`（同格同型）＋生成期坑位账。返回实际落位坑位数。</summary>
    static int PlaceResourceCell(MapData map, int cell, int kind, int slots, System.Random rng)
    {
        int mask = 0;
        for (int s = 0; s < slots; s++)
        {
            int slot = PickSubSlot(rng);
            while ((mask & (1 << slot)) != 0) slot = (slot + 1) & 3;   // 该坑位已占 ⇒ 顺移下一个空坑位
            mask |= 1 << slot;
        }
        MapGate.GenesisWrite(map, cell, ResourceKindFeature[kind]);   // 【HH.294 片4·4-A】造世界口
        if (_pitMask != null && cell >= 0 && cell < _pitMask.Length) _pitMask[cell] = (byte)mask;
        return slots;
    }

    static bool TryStampMineCluster(MapData map, List<int> candidates, System.Random rng, MapGenRulesConfig cfg)
    {
        int side = MineClusterSide;
        // 从候选里找一块完整 side×side 全 Plain（随机起点 ⇒ 不总贴同一角）
        int start = candidates.Count > 0 ? rng.Next(candidates.Count) : 0;
        for (int k = 0; k < candidates.Count; k++)
        {
            int cell = candidates[(start + k) % candidates.Count];
            int ox = cell % map.width, oy = cell / map.width;
            if (ox + side > map.width || oy + side > map.height) continue;
            if (ox < OceanThickness || oy < OceanThickness
                || ox + side > map.width - OceanThickness || oy + side > map.height - OceanThickness) continue; // 整块避开海洋带（否则 PlaceOcean 切碎簇 ⇒ 孤立矿格）
            if (InClearZone(map, ox, oy, side, cfg)) continue;      // 整块避开主城净空区（步 6.6 补足路径）
            if (!IsClearBlock(map, ox, oy, side)) continue;
            StampBlock(map, ox, oy, FeatureType.Mine, side);
            MarkBlockPitsFull(map, ox, oy, side);   // HH.291 A4：矿洞例外＝4 格各自整格占用（不可再被其他资源占用）
            return true;
        }
        return false;
    }

    /// <summary>把 side×side 区块记入坑位账为「整格占满」（矿洞例外·HH.291 A4）。</summary>
    static void MarkBlockPitsFull(MapData map, int ox, int oy, int side)
    {
        if (_pitMask == null) return;
        for (int dy = 0; dy < side; dy++)
            for (int dx = 0; dx < side; dx++)
            {
                int i = Idx(map, ox + dx, oy + dy);
                if (i >= 0 && i < _pitMask.Length) _pitMask[i] = (1 << PitsPerCell) - 1;
            }
    }

    /// <summary>Fisher–Yates（确定性·注入 rng）。</summary>
    static void Shuffle<T>(System.Random rng, List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T t = list[i]; list[i] = list[j]; list[j] = t;
        }
    }

    /// <summary>⭐ 池子口径解算（`03` §6.7 · `HH.294` 片 5）—— 生成期与运行期**共用一处**：
    /// · `cap(band, difficulty) = poolCapBase × GetBandAbundance(band) × difficultyResourceScale[difficulty−1]`（`D773` 裁：难度乘在 cap 上）
    /// · `p_i = (1−w_i) / Σ(1−w_j)`（**复用** `resourceWeights`；`D774` 定案表）
    /// · `capKind_i = cap × p_i × kindCapRelax`（单类上限·松弛 1.5）
    /// ⚠️ **单位口径（本片落地式）**：`点` —— 树/石堆/木堆/矿脉各 1 坑位 ＝ 1 点；**矿洞 1 簇（2×2 Cell）＝ 1 点**。
    ///   依据：`Σ_i E_i = cap × Σ_i p_i = cap`（`03` §6.7.1 定案表自洽）；「矿洞 2.2（≈2 簇）」。
    /// </summary>
    public static void ResolvePoolQuota(MapGenRulesConfig cfg, ClimateZone band, int difficulty,
                                        float[] share, float[] kindCap, out float cap)
    {
        cap = cfg != null ? Mathf.Max(1f, cfg.poolCapBase) : 96f;
        cap *= cfg != null ? cfg.GetBandAbundance(band) : 1f;              // HH.291 A3（R-01）：地带丰度
        int di = Mathf.Clamp(difficulty - 1, 0, 2);
        float scale = cfg != null && cfg.difficultyResourceScale != null && cfg.difficultyResourceScale.Length > di
            ? cfg.difficultyResourceScale[di] : 1f;
        cap *= Mathf.Max(0.01f, scale);                                    // D773：难度系数乘在 cap 上
        float relax = cfg != null ? Mathf.Max(0f, cfg.kindCapRelax) : 1.5f;

        var w5 = cfg != null ? cfg.GetResourceWeights(band) : null;
        var r = new float[ResourceKindCount];
        float sumR = 0f;
        for (int i = 0; i < ResourceKindCount; i++)
        {
            float wi = w5 != null ? Mathf.Clamp01(w5[i]) : 0.5f;
            r[i] = 1f - wi;
            sumR += r[i];
        }
        for (int i = 0; i < ResourceKindCount; i++)
        {
            share[i] = sumR > 0f ? r[i] / sumR : 0f;
            kindCap[i] = cap * share[i] * relax;
        }
    }

    /// <summary>配额解算（`03` §6.7 池子模型）：`e[i] = E_i^open = cap × p_i × poolOpenRatio`（**开局落格目标**·40%）
    /// ⇒ `b[i] = B_i = floor(E_i^open × guaranteeRatio)`（矿洞＝0）；另出 `cap`（总上限）与 `kindCap[]`（单类上限）。
    /// ⚠️ `e[]` 由改前的「满池 E_i」改为「**开局目标** `E_i^open`」——生成期落格与步骤 6.6 的 `target` **同取此值**（`D773` `P-4`）。</summary>
    static void ComputeQuota(MapGenRulesConfig cfg, ClimateZone band, int difficulty,
                             float[] e, int[] b, float[] kindCap, float[] shareBuf, out float cap)
    {
        ResolvePoolQuota(cfg, band, difficulty, shareBuf, kindCap, out cap);
        float open = cfg != null ? Mathf.Clamp01(cfg.poolOpenRatio) : 0.4f;
        float ratio = cfg != null ? Mathf.Clamp01(cfg.guaranteeRatio) : 0.5f;
        for (int i = 0; i < ResourceKindCount; i++)
        {
            e[i] = cap * shareBuf[i] * open;
            b[i] = Mathf.FloorToInt(e[i] * ratio);
        }
        // **HH.293 B3（D739）**：矿洞退出 `B_i` 保底 —— 否则 `b[ResMine] > 0` 会把已拆的密度地板顶回来。
        b[ResMine] = 0;
    }

    // ========================================================================
    //  区块点数统计（`03` §6.8 A 类 · 按锚点归属 · HH.294 片 5 · 4-C）
    // ========================================================================

    static readonly bool[] _claimBuf = new bool[ChunkSize * ChunkSize];
    static readonly int[] _countBufHint = new int[ResourceKindCount];

    /// <summary>⭐ <b>区块点数统计（按锚点归属）</b>：该大区块内各类资源**点数**
    /// （非矿 ＝ 该格坑位数；**矿洞 1 簇（2×2）＝ 1 点**）。
    /// <b>跨界不重算</b>：以「左上角锚点」为准 —— 逐格 row-major 认领式扫描，遇到未认领的 Mine 且以它为左上角的
    /// 2×2 全为 Mine ⇒ 记 1 点并认领该 4 格；跨区块边界的簇仅在**锚点所在区块**被记一次（邻区块因不构成
    /// 完整 2×2 块而不记）。返回写入点数总和。</summary>
    public static int CountChunkPoints(MapData map, int cx, int cy, int[] dst)
    {
        if (dst == null) return 0;
        for (int t = 0; t < ResourceKindCount; t++) dst[t] = 0;
        if (map == null || map.features == null) return 0;
        int total = 0;
        int x0 = cx * ChunkSize, y0 = cy * ChunkSize;
        int cw = Mathf.Min(ChunkSize, map.width - x0), chh = Mathf.Min(ChunkSize, map.height - y0);
        if (cw <= 0 || chh <= 0) return 0;
        System.Array.Clear(_claimBuf, 0, _claimBuf.Length);

        for (int dy = 0; dy < chh; dy++)
            for (int dx = 0; dx < cw; dx++)
            {
                int x = x0 + dx, y = y0 + dy;
                var f = MapGate.ReadAt(map, x, y);
                int kind = -1;
                for (int t = 0; t < ResourceKindCount; t++)
                    if (f == ResourceKindFeature[t]) { kind = t; break; }
                if (kind < 0) continue;
                int ci = dy * ChunkSize + dx;
                if (kind == ResMine)
                {
                    if (_claimBuf[ci]) continue;                       // 已被本簇认领
                    // 锚点判定用**整图**口径（`HasFullBlock` 自带界判定）⇒ 跨区块边界的簇由其锚点所在区块记 1 点，
                    // 邻区块内的残余格不构成完整 2×2 块 ⇒ 不记（`03` §6.8 A 类「跨界不重算」）。
                    if (!HasFullBlock(map, x, y, FeatureType.Mine, MineClusterSide)) continue;
                    for (int sy = 0; sy < MineClusterSide; sy++)
                        for (int sx = 0; sx < MineClusterSide; sx++)
                        {
                            int ux = dx + sx, uy = dy + sy;
                            if (ux < cw && uy < chh) _claimBuf[uy * ChunkSize + ux] = true;
                        }
                    dst[ResMine] += 1; total += 1;                     // 矿洞：1 簇 ＝ 1 点
                }
                else
                {
                    int n = PitCountOfCell(Idx(map, x, y));
                    dst[kind] += n; total += n;
                }
            }
        return total;
    }

    /// <summary>便利口：整图按区块统计点数总和（供探针对照；`dstAll` 长度须 ≥ `ChunkW×ChunkH`）。</summary>
    public static int CountAllChunkPoints(MapData map, int[] dstAll)
    {
        int cw = ChunkW(map), ch = ChunkH(map), sum = 0;
        for (int cy = 0; cy < ch; cy++)
            for (int cx = 0; cx < cw; cx++)
            {
                int v = CountChunkPoints(map, cx, cy, _countBufHint);
                if (dstAll != null) { int ci = cy * cw + cx; if (ci < dstAll.Length) dstAll[ci] = v; }
                sum += v;
            }
        return sum;
    }

    // ========================================================================
    //  步骤 6.5：主城净空区（footprint 3×3 外扩 R=4 ⇒ 11×11）
    // ========================================================================

    /// <summary>某格是否落在任一主城净空区（footprint 半宽 1 ＋ 外扩 R）。</summary>
    public static bool IsInKingdomClearZone(MapData map, int x, int y, MapGenRulesConfig cfg)
    {
        if (map.kingdomSpawns == null || map.kingdomSpawns.Count == 0) return false;
        int r = cfg != null ? Mathf.Max(0, cfg.kingdomClearRadius) : 4;
        int half = 1 + r;
        for (int i = 0; i < map.kingdomSpawns.Count; i++)
        {
            var sp = map.kingdomSpawns[i];
            if (x >= sp.x - half && x <= sp.x + half && y >= sp.y - half && y <= sp.y + half) return true;
        }
        return false;
    }

    /// <summary>步骤 6.5：主城净空区——`kingdomSpawns` 全体 11×11 内 `Tree/Mine/OreVein/StonePile/WoodPile`
    /// 一律置 `Plain`（水域保留；山体不在清单内 ⇒ 保留）。返回清除格数。
    /// **矿洞按「整簇清除」**：`Mine` 为 2×2 Cell 粒度，若只清簇内一部分会留下孤立矿格
    /// （其后被 `PruneOrphanMineCells` 降级并告警 ⇒ 上游缺陷）⇒ 与净空区相交的整块一并清除。</summary>
    public static int ClearKingdomZones(MapData map, MapGenRulesConfig cfg)
    {
        int cleared = 0;
        // ① 矿洞：与净空区相交的完整 2×2 块整体清除
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                if (map.features[Idx(map, x, y)] != FeatureType.Mine) continue;
                if (!HasFullBlock(map, x, y, FeatureType.Mine, MineClusterSide)) continue;
                bool touch = false;
                for (int dy = 0; dy < MineClusterSide && !touch; dy++)
                    for (int dx = 0; dx < MineClusterSide && !touch; dx++)
                        if (IsInKingdomClearZone(map, x + dx, y + dy, cfg)) touch = true;
                if (!touch) continue;
                StampBlock(map, x, y, FeatureType.Plain, MineClusterSide);
                cleared += MineClusterSide * MineClusterSide;
            }
        // ② 其余资源：区内一律置 Plain（水域保留）
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                if (!IsInKingdomClearZone(map, x, y, cfg)) continue;
                int i = Idx(map, x, y);
                var f = map.features[i];
                if (f == FeatureType.Tree || f == FeatureType.OreVein || f == FeatureType.StonePile || f == FeatureType.WoodPile)
                { MapGate.GenesisWrite(map, i, FeatureType.Plain); cleared++; }   // 【HH.294 片4·4-A】造世界口
            }
        return cleared;
    }

    /// <summary>步骤 6.6：逐区块配额**补足**（在净空区之后跑，避免"保底把资源塞回净空区"）。
    /// 统计本区块各资源实际数量（**点口径 · 按锚点归属**：非矿＝坑位；矿洞＝簇，跨界不重算 ⇒ `CountChunkPoints`），
    /// 不足 `target_i = max(round(E_i^open), B_i)` 时在区块内补足（跳过净空区/海洋带）；
    /// 补足同样按**一格至多 4 个同型坑位**打包。
    /// ⚠️ **`D773` `P-4` 裁**：`target` 取**开局目标** `E_i^open`（**不是**满池 `E_i`）⇒ 6.6 只负责"不出现空区块"，
    /// 不再把 40% 开局顶穿到满池。</summary>
    public static void EnsureChunkResourceQuota(System.Random rng, MapData map, MapGenRulesConfig cfg, int difficulty)
    {
        int cw = ChunkW(map), ch = ChunkH(map);
        var e = new float[ResourceKindCount];
        var b = new int[ResourceKindCount];
        var kindCap = new float[ResourceKindCount];
        var share = new float[ResourceKindCount];
        var have = new int[ResourceKindCount];
        var candidates = new List<int>(ChunkSize * ChunkSize);

        for (int cy = 0; cy < ch; cy++)
            for (int cx = 0; cx < cw; cx++)
            {
                ComputeQuota(cfg, DominantZoneOfChunk(map, cx, cy), difficulty, e, b, kindCap, share, out _);
                // 【HH.294 片 5·4-C】统计改「**按锚点归属**」：矿洞整簇只在其锚点（左上角）所在区块记 1 点 ⇒ 跨界不重算。
                CountChunkPoints(map, cx, cy, have);

                int x0 = cx * ChunkSize, y0 = cy * ChunkSize;
                candidates.Clear();
                for (int y = y0; y < y0 + ChunkSize && y < map.height; y++)
                    for (int x = x0; x < x0 + ChunkSize && x < map.width; x++)
                    {
                        int i = Idx(map, x, y);
                        var f = map.features[i];
                        if (x < OceanThickness || y < OceanThickness
                            || x >= map.width - OceanThickness || y >= map.height - OceanThickness) continue;
                        if (f != FeatureType.Plain) continue;
                        if (IsInKingdomClearZone(map, x, y, cfg)) continue;
                        candidates.Add(i);
                    }
                Shuffle(rng, candidates);
                int cursor = 0;

                // 矿洞：按簇补足（`e[ResMine]` 单位＝点（簇）⇒ 不再 ÷4）
                // **HH.293 B3（D739）**：删「mineWant ≤ 0 且 b>0 ⇒ 1」地板（第二处，与 FillFeatures 同源）
                int mineWant = Mathf.RoundToInt(e[ResMine]);
                int mineHave = have[ResMine];
                while (mineHave + 1 <= mineWant)
                {
                    if (!TryStampMineCluster(map, candidates, rng, cfg)) break;
                    mineHave++;
                }
                for (int t = 0; t < ResMine; t++)
                {
                    int target = Mathf.Max(Mathf.RoundToInt(e[t]), b[t]);   // e[] ＝ 开局目标 E_i^open（P-4）
                    int need = target - have[t];
                    while (need > 0)
                    {
                        while (cursor < candidates.Count && map.features[candidates[cursor]] != FeatureType.Plain) cursor++;
                        if (cursor >= candidates.Count) break;
                        int cell = candidates[cursor++];
                        int slots = Mathf.Min(PitsPerCell, need);   // HH.291 A4：一格填至多 4 个同型坑位
                        PlaceResourceCell(map, cell, t, slots, rng);
                        need -= slots;
                    }
                }
            }
    }

    // ===== 步骤 6：王国出生点（2_16 步骤3：温度带匹配 D288/D292/D298/D302）=====
    /// <summary>
    /// 王国出生点放置：spawns[0]=玩家主城（温带保底 D302），spawns[1..N]=AI 王国按模板偏好气候带匹配（D298），
    /// 全偏好带失败回退全局兜底+日志（D292）。D41 间距与 NearestWalkable 兜底保留。模板绑定写入 map.kingdomTemplates。
    /// </summary>
    public static void PlaceKingdomSpawns(System.Random rng, MapData map, MapGenRulesConfig cfg,
                                          WorldSize size, int aiCount, List<KingdomDef> templates)
    {
        int count = 1 + Mathf.Max(0, aiCount);
        int minDist = cfg != null ? cfg.GetSpawnMinDistance(size) : 32;
        int margin = ChunkSize;
        var spawns = new List<Vector2Int>();
        var templateBindings = new List<KingdomDef>();

        // spawns[0] = 玩家主城：仅温带选点（D302，可走约 75% 的带），不再全域随机
        Vector2Int player = PickWalkableInClimate(rng, map, margin, ClimateZone.Temperate, 0, spawns);
        if (player.x < 0)
        {
            Debug.LogWarning("[MapGenRules] 玩家主城温带保底失败，回退全域随机可走格（D302 兜底）。");
            player = PlaceRandomWalkable(rng, map, margin, spawns, minDist);
        }
        spawns.Add(player);
        templateBindings.Add(null);

        // AI 王国：按各自 KingdomDef.preferredClimates 数组按序匹配（+preferredFeature 特征过滤 D5）
        for (int i = 0; i < aiCount; i++)
        {
            var tpl = templates != null && i < templates.Count ? templates[i] : null;
            spawns.Add(PickSpawnForTemplate(rng, map, margin, minDist, spawns, tpl, cfg));
            templateBindings.Add(tpl);
        }

        map.kingdomSpawns = spawns.Count > 0 ? spawns : map.kingdomSpawns;
        map.kingdomTemplates = templateBindings;
    }

    /// <summary>
    /// AI 出生点：偏好带按序匹配+preferredFeature 特征过滤（D5，D316 悬空转正→DZ-080），全部失败回退全局兜底+日志（D292）。
    /// 两轮结构：第一轮=带内特征匹配（真实过滤）；第二轮=带内忽略特征（回退+日志=D5 验收负探针锚）。
    /// public static（批D 探针 P2d 回退负探针直调依赖）。
    /// </summary>
    public static Vector2Int PickSpawnForTemplate(System.Random rng, MapData map, int margin, int minDist,
                                                  List<Vector2Int> spawns, KingdomDef tpl, MapGenRulesConfig cfg)
    {
        var feature = tpl != null ? tpl.preferredFeature : KingdomPreferredFeature.None;
        if (tpl != null && tpl.preferredClimates != null && tpl.preferredClimates.Length > 0)
        {
            // 第一轮：偏好带内带特征匹配（D316 原设计=preferredFeature 真实过滤）
            if (feature != KingdomPreferredFeature.None)
            {
                for (int b = 0; b < tpl.preferredClimates.Length; b++)
                {
                    var p = PickWalkableInClimate(rng, map, margin, tpl.preferredClimates[b], minDist, spawns, feature, cfg, filterFeature: true);
                    if (p.x >= 0) return p;
                }
                // 回退（D5 验收负探针锚=无特征地形回退+日志）：带内忽略特征继续选点（D292 回退模式复用）
                Debug.LogWarning($"[MapGenRules] 模板 {tpl.templateName} 偏好带内特征 {feature} 无匹配候选，回退忽略特征带内选点（D292/D5）。");
            }
            // 第二轮（或 None 无特征过滤直进）：带内纯气候匹配
            for (int b = 0; b < tpl.preferredClimates.Length; b++)
            {
                var p = PickWalkableInClimate(rng, map, margin, tpl.preferredClimates[b], minDist, spawns);
                if (p.x >= 0) return p;
            }
            Debug.LogWarning($"[MapGenRules] 模板 {tpl.templateName} 偏好气候带均失败，回退全局无可走格兜底（D292）。");
        }
        return PlaceRandomWalkable(rng, map, margin, spawns, minDist);
    }

    /// <summary>在指定气候带内随机抽可走格（带间距校验；可选 preferredFeature 特征过滤 D5）。找不到返回 (-1,-1)。</summary>
    static Vector2Int PickWalkableInClimate(System.Random rng, MapData map, int margin, ClimateZone climate,
                                            int minDist, List<Vector2Int> spawns,
                                            KingdomPreferredFeature feature = KingdomPreferredFeature.None,
                                            MapGenRulesConfig cfg = null, bool filterFeature = false)
    {
        int guard = 0;
        while (guard++ < 3000)
        {
            int x = rng.Next(margin, Mathf.Max(margin + 1, map.width - margin));
            int y = rng.Next(margin, Mathf.Max(margin + 1, map.height - margin));
            if (ZoneOf(map, x, y) != climate) continue;
            if (minDist > 0 && TooClose(spawns, new Vector2Int(x, y), minDist)) continue;
            var p = NearestWalkable(map, x, y);
            if (p.x < 0) continue;
            if (filterFeature && !MatchesPreferredFeature(map, p, feature, cfg)) continue;   // D5 特征过滤
            return p;
        }
        return new Vector2Int(-1, -1);
    }

    /// <summary>
    /// 立国选址特征匹配判定（2_22 P0 批D / D5，D316 原设计语义+M4 尾插枚举四特征全实现）：
    /// RiverAdjacent=候选点半径内存在水格（River/Ocean）；ForestDense/MineralRich/BarrenRich=
    /// 候选点所在大区块（ChunkSize=16）内 Tree/Mine/Plain 格占比达阈值（MapGenRulesConfig）。
    /// None=恒命中（不过滤）。
    /// </summary>
    public static bool MatchesPreferredFeature(MapData map, Vector2Int p, KingdomPreferredFeature feature, MapGenRulesConfig cfg)
    {
        switch (feature)
        {
            case KingdomPreferredFeature.RiverAdjacent:
            {
                int r = cfg != null ? Mathf.Max(1, cfg.featureScanRadiusCells) : 8;
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = p.x + dx, y = p.y + dy;
                    if (!InB(map, x, y)) continue;
                    var f = map.features[Idx(map, x, y)];
                    if (f == FeatureType.River || f == FeatureType.Ocean) return true;
                }
                return false;
            }
            case KingdomPreferredFeature.ForestDense:
                return ChunkFeatureRatio(map, p, FeatureType.Tree)
                       >= (cfg != null ? cfg.forestDensityThreshold : 0.10f);
            case KingdomPreferredFeature.MineralRich:
                return ChunkFeatureRatio(map, p, FeatureType.Mine)
                       >= (cfg != null ? cfg.mineralDensityThreshold : 0.05f);
            case KingdomPreferredFeature.BarrenRich:
                return ChunkFeatureRatio(map, p, FeatureType.Plain)
                       >= (cfg != null ? cfg.barrenDensityThreshold : 0.60f);
            default:
                return true;   // None/未知=不过滤
        }
    }

    /// <summary>候选点所在大区块内指定特征物占比（D5 区块密度类特征判定核；越界格不计入分母）。</summary>
    public static float ChunkFeatureRatio(MapData map, Vector2Int p, FeatureType need)
    {
        int cx = (p.x / ChunkSize) * ChunkSize, cy = (p.y / ChunkSize) * ChunkSize;
        int total = 0, hits = 0;
        for (int y = cy; y < cy + ChunkSize; y++)
        for (int x = cx; x < cx + ChunkSize; x++)
        {
            if (!InB(map, x, y)) continue;
            total++;
            if (map.features[Idx(map, x, y)] == need) hits++;
        }
        return total > 0 ? hits / (float)total : 0f;
    }

    /// <summary>全局随机可走格兜底（D292/D41 间距校验）。</summary>
    static Vector2Int PlaceRandomWalkable(System.Random rng, MapData map, int margin,
                                          List<Vector2Int> spawns, int minDist)
    {
        int guard = 0;
        while (guard++ < 3000)
        {
            int x = rng.Next(margin, Mathf.Max(margin + 1, map.width - margin));
            int y = rng.Next(margin, Mathf.Max(margin + 1, map.height - margin));
            if (minDist > 0 && TooClose(spawns, new Vector2Int(x, y), minDist)) continue;
            var p = NearestWalkable(map, x, y);
            if (p.x < 0) continue;
            return p;
        }
        return new Vector2Int(margin, margin);
    }

    static bool TooClose(List<Vector2Int> spawns, Vector2Int p, int minDist)
    {
        for (int i = 0; i < spawns.Count; i++)
            if (Vector2Int.Distance(spawns[i], p) < minDist) return true;
        return false;
    }

    /// <summary>就近找可走格（螺旋外扩，R6）。找不到返回 (-1,-1)。</summary>
    public static Vector2Int NearestWalkable(MapData map, int cx, int cy)
    {
        // 【HH.294 片4补正·R2】裸读收口：本函数**运行期可达**（`KingdomFoundry`×7／`VagrantCampSystem`×3／
        //   `MineByproductComponent:234` 等）⇒ 两处裸读改走 `MapGate.ReadAt`；原式 `InB(map,…) &&` **界保护已在**，
        //   `ReadAt` 越界返 Plain 的差异路径在此不可达 ⇒ 等价。
        if (InB(map, cx, cy) && IsWalkableFeature(MapGate.ReadAt(map, cx, cy))) return new Vector2Int(cx, cy);
        int maxR = Mathf.Max(map.width, map.height);
        for (int r = 1; r <= maxR; r++)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;   // 只查当前环
                    int x = cx + dx, y = cy + dy;
                    if (InB(map, x, y) && IsWalkableFeature(MapGate.ReadAt(map, x, y))) return new Vector2Int(x, y);
                }
        }
        return new Vector2Int(-1, -1);
    }

    static bool InB(MapData m, int x, int y) => x >= 0 && y >= 0 && x < m.width && y < m.height;

    // ===== 步骤 7「资源就近补」已删除（HH.291 A5 / R-02 前半）=====
    // 原 `EnsureNearbyResources`（每主城就近补 Tree 1×1／Mine 2×2 簇／StonePile 1×1）＋专用参数
    // `resourceGuaranteeRadius` 一并退役 —— 改由「开局资源」兜底（另批）。
    // **保留**：`EnsureChunkResourceQuota`（步骤 6.6·逐区块配额补足）仍生效 ⇒ 不得出现"整片区域无资源"。
    // （`EnsureBlock`／`BlockHasFeature` 为该链路专用，随之一并删除；`InClearZone` 仍被 `TryStampMineCluster` 使用 ⇒ 保留。）

    /// <summary>side×side 块是否与主城净空区相交（HH.272 件③ 保底避让）。</summary>
    static bool InClearZone(MapData map, int ox, int oy, int side, MapGenRulesConfig cfg)
    {
        for (int dy = 0; dy < side; dy++)
            for (int dx = 0; dx < side; dx++)
                if (IsInKingdomClearZone(map, ox + dx, oy + dy, cfg)) return true;
        return false;
    }

    // ===== 步骤 9：海洋边缘 + 河流（HH.272 件⑥：湖泊/冰河已正式删除）=====
    public static void PlaceWater(System.Random rng, MapData map, WorldSize size)
    {
        PlaceOcean(map);
        int riverCount = size == WorldSize.Small ? 1 : size == WorldSize.Medium ? 2 : 3;
        for (int i = 0; i < riverCount; i++) PlaceRiver(rng, map);
        PruneOrphanMineCells(map);   // 件12/D618 兜底：水域后清孤立矿山格（正常应为 0 命中）
    }

    /// <summary>清孤立矿山格（D618 验收「不留孤立格」的兜底与可证伪点）：不处于完整 2×2 的 Mine 格降级为 Plain。
    /// 正常路径应为 0（成簇撒布不产孤立格 + 水域避让不切碎簇 + 簇撒布已避开海洋带）；非 0 即上游有未见覆写路径。</summary>
    static void PruneOrphanMineCells(MapData map)
    {
        int pruned = 0;
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                if (map.features[Idx(map, x, y)] != FeatureType.Mine) continue;
                if (InFullBlock(map, x, y, FeatureType.Mine, MineClusterSide)) continue;
                MapGate.GenesisWrite(map, Idx(map, x, y), FeatureType.Plain);   // 【HH.294 片4·4-A】造世界口
                pruned++;
            }
        if (pruned > 0)
            Debug.LogWarning($"[MapGenRules] 清孤立矿山格 {pruned} 个（D618；期望=0，非 0 说明上游存在未见覆写路径）。");
    }

    static void PlaceOcean(MapData map)
    {
        int thickness = OceanThickness;
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
                if (x < thickness || y < thickness || x >= map.width - thickness || y >= map.height - thickness)
                    MapGate.GenesisWrite(map, Idx(map, x, y), FeatureType.Ocean);   // 【HH.294 片4·4-A】造世界口
    }

    /// <summary>主干河：从一边界随机走到对边界，标记 River（不做分支 D34）。</summary>
    static void PlaceRiver(System.Random rng, MapData map)
    {
        bool horizontal = rng.NextDouble() < 0.5;
        int x, y;
        if (horizontal) { x = 0; y = rng.Next(map.height / 4, map.height * 3 / 4); }
        else { x = rng.Next(map.width / 4, map.width * 3 / 4); y = 0; }

        int guard = 0;
        while (InB(map, x, y) && guard++ < map.width + map.height + 200)
        {
            var cur = map.features[Idx(map, x, y)];
            // DZ-084：不覆盖矿山簇（河穿矿=该格岩石出露，河在此断开——D618 认可语义）
            // HH.272 件④：亦**不覆盖山/雪山**（2_1 §3.6「绕行特殊地形·不穿雪地」；否则山脊被河切断 ⇒ 产 <4 格碎片）
            if (cur != FeatureType.Mine && cur != FeatureType.Mountain && cur != FeatureType.SnowMountain)
                MapGate.GenesisWrite(map, Idx(map, x, y), FeatureType.River);   // 【HH.294 片4·4-A】造世界口
            // 朝对岸推进 + 随机侧移
            if (horizontal) { x++; if (rng.NextDouble() < 0.4) y += rng.Next(-1, 2); }
            else { y++; if (rng.NextDouble() < 0.4) x += rng.Next(-1, 2); }
            x = Mathf.Clamp(x, 0, map.width - 1);
            y = Mathf.Clamp(y, 0, map.height - 1);
            if (horizontal && x >= map.width - 1) break;
            if (!horizontal && y >= map.height - 1) break;
        }
    }

    // ===== 步骤 10：威胁刷点（SpawnDef）=====
    public static void PlaceThreatSpawns(System.Random rng, MapData map, MapGenRulesConfig cfg, int difficulty)
    {
        int perKingdom = cfg != null ? Mathf.Max(1, cfg.threatsPerKingdom) : 2;
        int minChunkDist = cfg != null ? Mathf.Max(1, cfg.threatMinChunkDistance) : 2;
        int minCellDist = minChunkDist * ChunkSize;
        map.threatSpawns.Clear();

        foreach (var sp in map.kingdomSpawns)
        {
            for (int n = 0; n < perKingdom; n++)
            {
                int guard = 0; Vector2Int p = default; bool found = false;
                while (!found && guard++ < 300)
                {
                    int x = rng.Next(ChunkSize, Mathf.Max(ChunkSize + 1, map.width - ChunkSize));
                    int y = rng.Next(ChunkSize, Mathf.Max(ChunkSize + 1, map.height - ChunkSize));
                    if (Vector2Int.Distance(new Vector2Int(x, y), sp) < minCellDist) continue;
                    var w = NearestWalkable(map, x, y);
                    if (w.x < 0) continue;
                    p = w; found = true;
                }
                if (!found) continue;

                Vector2 dir = new Vector2(sp.x - p.x, sp.y - p.y);
                if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
                map.threatSpawns.Add(new SpawnDef
                {
                    coord = p,
                    direction = dir,
                    strength = Mathf.Clamp(difficulty, 1, 3),
                    faction = Faction.Monster   // 阶段4前只怪物波次（D38）
                });
            }
        }
    }

    // ===== 步骤 11：naturalBuildings（【HH.294 片 6-2 收尾】目标态：恒空·不再派生实体）=====

    /// <summary>【HH.294 片 6-2 收尾·旧路径清场（`D779` 残余 `S1`）】**目标态：资源点＝格表**——
    /// 一次性三型（`OreVein`／`WoodPile`／`StonePile`）与树一致**不再派生 `Building` 实体**
    /// （改前双写态与 A/B 对照开关已随本批删除；改前读数落盘于 `HH.307` 报告 §一）。
    /// 本方法保留作**契约槽位**（`WorldManager.cs:202` 与 2 处 Editor 探针仍调用）——
    /// 语义＝清空 `naturalBuildings`（该字段保留·`WorldState.cs:31` 契约槽位·现恒空）。</summary>
    public static void DeriveNaturalBuildings(MapData map)
    {
        map.naturalBuildings.Clear();   // 【HH.294 片 6-2 收尾】目标态：不再派生实体（资源点＝格表）
    }

    // ===== 步骤 12：资源等级分配（`03` §6.9 · `HH.294` 片 6-3 · 兑现 `F-14`）=====

    /// <summary>⭐ A/B 对照开关（`HH.294` 片 6-3）：`false` ⇒ <see cref="AssignResourceGrades"/> 全图落 `Normal`
    /// （等价「grade 事实未赋值」态 ⇒ 改前读数**同一 build 内可实测**）。默认 `true`。</summary>
    public static bool AssignGradesEnabled = true;

    /// <summary>按 <see cref="MapGenRulesConfig.gradeWeights"/>（**枚举序** `{Barren, Normal, Rich}`）掷一个等级。
    /// 权重和归一（配置可调）；缺配置／权重非法 ⇒ `Normal`（不抛）。
    /// ⚠️ 调用方自备 **rng 流**（生成期＝`seed` 派生的**独立**流；运行期＝`ResourceRespawnSystem` 自己的 grade 流）——
    /// ⛔ 禁共用主流程 `rng`（共用会位移后续全部随机结果 ⇒ 整张地图变样、既有校准全废）。</summary>
    public static ResourceGrade RollGrade(MapGenRulesConfig cfg, System.Random rng)
    {
        var w = cfg != null ? cfg.gradeWeights : null;
        if (w == null || w.Length < 3 || rng == null) return ResourceGrade.Normal;
        float sum = 0f;
        for (int i = 0; i < 3; i++) if (w[i] > 0f) sum += w[i];
        if (sum <= 0f) return ResourceGrade.Normal;
        double r = rng.NextDouble() * sum;
        if (r < w[0]) return ResourceGrade.Barren;
        if (r < w[0] + w[1]) return ResourceGrade.Normal;
        return ResourceGrade.Rich;
    }

    /// <summary>⭐ 生成期**后处理 pass**（`03` §6.9「分配时机」）：扫全图 —— 凡 `features[i]` 命中四型
    /// （<see cref="MapGate.IsGradeFeature"/>：`Tree`/`OreVein`/`WoodPile`/`StonePile`）⇒ 按权重掷等级；
    /// 其余（含 `Mine` 与非资源格）**显式落 `Normal`**（⚠️ 禁依赖 `default`＝`Barren`）。
    ///
    /// <b>调用点纪律</b>：须排在**全部 features 写步之后**（净空区／配额补足／水域／复跑连通都会改 features）
    /// ⇒ 只有**最终存活**的资源格才带等级（`03` §6.9）。`gradeRng` **必须是独立 rng 流**（由 `seed` 派生）。</summary>
    public static void AssignResourceGrades(MapData map, MapGenRulesConfig cfg, System.Random gradeRng)
    {
        if (map == null || map.features == null) return;
        int n = map.features.Length;
        if (map.grades == null || map.grades.Length != n) map.grades = new ResourceGrade[n];
        bool on = AssignGradesEnabled;
        for (int i = 0; i < n; i++)
        {
            var g = on && MapGate.IsGradeFeature(map.features[i]) ? RollGrade(cfg, gradeRng) : ResourceGrade.Normal;
            MapGate.GenesisWriteGrade(map, i, g);   // 走门内写内核（`MapGate.WriteGradeRaw`）
        }
    }
}
