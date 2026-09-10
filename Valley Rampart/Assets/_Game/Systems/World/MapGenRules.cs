using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D 地图生成规则（2_1 §5.2 生成管线）。全部静态方法，注入 System.Random 保证确定性（R4）。
/// features 为唯一功能源（2_1 §1.3）；本类只写 map.features / climateZones / spawns / naturalBuildings，
/// 不实例化 Building（归 2_2）、不渲染（归 2_10）。
/// </summary>
public static class MapGenRules
{
    public const int ChunkSize = 16;   // 大区块边长（doc 1 §3.1 固定 16×16）

    // ===== §3.3 特征物密度表（固定设计表，内置保证确定性）=====
    // 每温度带一组权重；空格=Plain。水域（River/Lake/Ocean）不在填充步，归 PlaceWater。
    private struct FW { public FeatureType f; public float w; }
    private static readonly FW[][] ClimateFeatureTable = new FW[][]
    {
        // Tropical（热带）：平原多、树多、无雪山
        new FW[]{ new FW{f=FeatureType.Plain,w=60}, new FW{f=FeatureType.Tree,w=20},
                  new FW{f=FeatureType.Mountain,w=5}, new FW{f=FeatureType.Mine,w=5},
                  new FW{f=FeatureType.OreVein,w=3}, new FW{f=FeatureType.StonePile,w=2}, new FW{f=FeatureType.WoodPile,w=2} },
        // Subtropical（亚热带）
        new FW[]{ new FW{f=FeatureType.Plain,w=55}, new FW{f=FeatureType.Tree,w=20},
                  new FW{f=FeatureType.Mountain,w=10}, new FW{f=FeatureType.SnowMountain,w=2}, new FW{f=FeatureType.Mine,w=8},
                  new FW{f=FeatureType.OreVein,w=2}, new FW{f=FeatureType.StonePile,w=1}, new FW{f=FeatureType.WoodPile,w=1} },
        // Temperate（温带）：矿洞最多
        new FW[]{ new FW{f=FeatureType.Plain,w=45}, new FW{f=FeatureType.Tree,w=20},
                  new FW{f=FeatureType.Mountain,w=15}, new FW{f=FeatureType.SnowMountain,w=5}, new FW{f=FeatureType.Mine,w=10},
                  new FW{f=FeatureType.OreVein,w=2}, new FW{f=FeatureType.StonePile,w=1}, new FW{f=FeatureType.WoodPile,w=1} },
        // Cold（寒带）：雪山为主
        new FW[]{ new FW{f=FeatureType.Plain,w=35}, new FW{f=FeatureType.Tree,w=5},
                  new FW{f=FeatureType.Mountain,w=10}, new FW{f=FeatureType.SnowMountain,w=35}, new FW{f=FeatureType.Mine,w=5},
                  new FW{f=FeatureType.OreVein,w=1}, new FW{f=FeatureType.StonePile,w=1} },
    };

    /// <summary>特征物是否可走（生成期判定，未灌 GridSystem 前用）。</summary>
    public static bool IsWalkableFeature(FeatureType f)
    {
        switch (f)
        {
            case FeatureType.Plain: case FeatureType.Tree: case FeatureType.Mine:
            case FeatureType.OreVein: case FeatureType.StonePile: case FeatureType.WoodPile:
                return true;
            default: return false;   // Mountain/SnowMountain/River/Lake/Ocean 阻挡
        }
    }

    public static int Idx(MapData m, int x, int y) => y * m.width + x;
    public static int ChunkW(MapData m) => Mathf.Max(1, m.width / ChunkSize);
    public static ClimateZone ZoneOf(MapData m, int x, int y)
        => m.climateZones[(x / ChunkSize) + (y / ChunkSize) * ChunkW(m)];

    // ===== 件12（D618/DZ-084/D621）：矿山锚点 2×2 成簇工具 =====
    public const int OceanThickness = 2;      // 海洋边缘厚度（PlaceOcean 与矿山簇撒布边界共用；原为 PlaceOcean 内字面量 2）
    public const int MineClusterSide = 2;     // 矿山簇边长（对齐 mine.footprint 2×2，D613）

    /// <summary>块是否**完整**由指定特征填满（轴对齐 side×side，D621①：L 形连通不算）。</summary>
    static bool HasFullBlock(MapData map, int ox, int oy, FeatureType need, int side)
    {
        if (ox < 0 || oy < 0 || ox + side > map.width || oy + side > map.height) return false;
        for (int dy = 0; dy < side; dy++)
            for (int dx = 0; dx < side; dx++)
                if (map.features[Idx(map, ox + dx, oy + dy)] != need) return false;
        return true;
    }

    /// <summary>块内是否含指定特征（成簇防重叠用）。</summary>
    static bool BlockHasFeature(MapData map, int ox, int oy, FeatureType need, int side)
    {
        for (int dy = 0; dy < side; dy++)
            for (int dx = 0; dx < side; dx++)
                if (map.features[Idx(map, ox + dx, oy + dy)] == need) return true;
        return false;
    }

    /// <summary>块是否全为 Plain（可盖章空位）。</summary>
    static bool IsClearBlock(MapData map, int ox, int oy, int side)
    {
        if (ox < 0 || oy < 0 || ox + side > map.width || oy + side > map.height) return false;
        for (int dy = 0; dy < side; dy++)
            for (int dx = 0; dx < side; dx++)
                if (map.features[Idx(map, ox + dx, oy + dy)] != FeatureType.Plain) return false;
        return true;
    }

    static void StampBlock(MapData map, int ox, int oy, FeatureType need, int side)
    {
        for (int dy = 0; dy < side; dy++)
            for (int dx = 0; dx < side; dx++)
                map.features[Idx(map, ox + dx, oy + dy)] = need;
    }

    /// <summary>某格是否处于「完整 side×side 同特征块」内（D618 验收：无孤立矿山格 的判定反向口径）。</summary>
    public static bool InFullBlock(MapData map, int x, int y, FeatureType need, int side)
    {
        for (int oy = -(side - 1); oy <= 0; oy++)
            for (int ox = -(side - 1); ox <= 0; ox++)
                if (HasFullBlock(map, x + ox, y + oy, need, side)) return true;
        return false;
    }

    /// <summary>矩形内是否含指定特征（DZ-084 水域避让；越界格不计）。</summary>
    static bool RectHasFeature(MapData map, int ox, int oy, int bw, int bh, FeatureType need)
    {
        for (int y = oy; y < oy + bh; y++)
            for (int x = ox; x < ox + bw; x++)
                if (InB(map, x, y) && map.features[Idx(map, x, y)] == need) return true;
        return false;
    }

    // ===== 步骤 3：温度带权重铺（按大区块）=====
    public static void FillClimateZones(System.Random rng, MapData map, MapGenRulesConfig cfg)
    {
        int cw = ChunkW(map), ch = Mathf.Max(1, map.height / ChunkSize);
        for (int cy = 0; cy < ch; cy++)
            for (int cx = 0; cx < cw; cx++)
                map.climateZones[cx + cy * cw] = RollClimate(rng, cfg);
    }

    static ClimateZone RollClimate(System.Random rng, MapGenRulesConfig cfg)
    {
        float total = 0f;
        var weights = new float[4];
        for (int i = 0; i < 4; i++) { weights[i] = cfg != null ? cfg.GetClimateWeight((ClimateZone)i) : 1f; total += weights[i]; }
        if (total <= 0f) return (ClimateZone)rng.Next(4);
        float roll = (float)rng.NextDouble() * total;
        for (int i = 0; i < 4; i++) { roll -= weights[i]; if (roll <= 0f) return (ClimateZone)i; }
        return ClimateZone.Temperate;
    }

    // ===== 步骤 4：特征物填充（散点分布，空格=该带平原）=====
    /// <summary>特征物填充。**件12（D618/DZ-084）**：矿山锚点由「逐格散点」改为「2×2 轴对齐成簇撒布」——
    /// 逐格 pass 仍照旧表 roll（保其他特征概率与 rng 消耗不变），仅把 Mine 结果落为 Plain；
    /// 再由 <see cref="ScatterMineClusters"/> 以「每格概率 ÷ 簇面积」的簇概率盖章，使**总矿格数≈原值**（D621②）。</summary>
    public static void FillFeatures(System.Random rng, MapData map)
    {
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                var f = RollFeature(rng, ZoneOf(map, x, y));
                map.features[Idx(map, x, y)] = f == FeatureType.Mine ? FeatureType.Plain : f;   // Mine 改由成簇撒布（件12）
            }
        ScatterMineClusters(rng, map);
    }

    /// <summary>矿山锚点成簇撒布（件12/D618/D621②）：逐候选原点 roll「每格概率 ÷ 簇面积」，命中即整簇盖章。
    /// 盖章顶掉格内原特征（= 原逐格语义的簇化平移：原 Mine 格也是顶掉该格其它结果）；仅跳过已有 Mine 的格/块以防重复计入。
    /// 原点限在海洋带内缩区（故与 PlaceOcean 天然不冲突）⇒ 全图 Mine 格必属完整 2×2（每簇 4 格、簇间不重叠）。</summary>
    static void ScatterMineClusters(System.Random rng, MapData map)
    {
        int side = MineClusterSide;
        int hiX = map.width - OceanThickness - side;
        int hiY = map.height - OceanThickness - side;
        for (int y = OceanThickness; y <= hiY; y++)
            for (int x = OceanThickness; x <= hiX; x++)
            {
                if (map.features[Idx(map, x, y)] == FeatureType.Mine) continue;     // 已属前簇 → 跳（保每格只计一次）
                float p = MineCellProbability(ZoneOf(map, x, y));
                if (p <= 0f || rng.NextDouble() >= p / (side * side)) continue;    // 簇概率 = 每格概率 ÷ 簇面积
                if (BlockHasFeature(map, x, y, FeatureType.Mine, side)) continue;  // 与已有簇重叠 → 跳（簇间不重叠）
                StampBlock(map, x, y, FeatureType.Mine, side);
            }
    }

    /// <summary>该温度带的「每格矿山概率」= 表内 Mine 权重 ÷ 总权重（原始逐格口径；簇撒布再除以簇面积）。</summary>
    static float MineCellProbability(ClimateZone zone)
    {
        var table = ClimateFeatureTable[(int)zone];
        float total = 0f, mineW = 0f;
        for (int i = 0; i < table.Length; i++)
        {
            total += table[i].w;
            if (table[i].f == FeatureType.Mine) mineW += table[i].w;
        }
        return total > 0f ? mineW / total : 0f;
    }

    static FeatureType RollFeature(System.Random rng, ClimateZone zone)
    {
        var table = ClimateFeatureTable[(int)zone];
        float total = 0f;
        for (int i = 0; i < table.Length; i++) total += table[i].w;
        float roll = (float)rng.NextDouble() * total;
        for (int i = 0; i < table.Length; i++) { roll -= table[i].w; if (roll <= 0f) return table[i].f; }
        return FeatureType.Plain;
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
    /// RiverAdjacent=候选点半径内存在水格（River/Lake/Ocean）；ForestDense/MineralRich/BarrenRich=
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
                    if (f == FeatureType.River || f == FeatureType.Lake || f == FeatureType.Ocean) return true;
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
        if (InB(map, cx, cy) && IsWalkableFeature(map.features[Idx(map, cx, cy)])) return new Vector2Int(cx, cy);
        int maxR = Mathf.Max(map.width, map.height);
        for (int r = 1; r <= maxR; r++)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;   // 只查当前环
                    int x = cx + dx, y = cy + dy;
                    if (InB(map, x, y) && IsWalkableFeature(map.features[Idx(map, x, y)])) return new Vector2Int(x, y);
                }
        }
        return new Vector2Int(-1, -1);
    }

    static bool InB(MapData m, int x, int y) => x >= 0 && y >= 0 && x < m.width && y < m.height;

    // ===== 步骤 7：后置校验① 资源就近补 =====
    public static void EnsureNearbyResources(System.Random rng, MapData map, MapGenRulesConfig cfg)
    {
        int radiusChunks = cfg != null ? Mathf.Max(1, cfg.resourceGuaranteeRadius) : 3;
        int radiusCells = radiusChunks * ChunkSize;
        foreach (var sp in map.kingdomSpawns)
        {
            EnsureBlock(map, sp, radiusCells, FeatureType.Tree, 1);                    // 木（1×1）
            EnsureBlock(map, sp, radiusCells, FeatureType.Mine, MineClusterSide);      // 矿（件12/D618：簇保底，非单格）
            EnsureBlock(map, sp, radiusCells, FeatureType.StonePile, 1);               // 石（1×1）
            // 农田 = Plain 可建位，天然充足，不强制
        }
    }

    /// <summary>半径内若无指定特征的**完整 side×side 轴对齐块**，则在可走区就地补一整块。
    /// **D621①**：判定必须是「完整 side×side 块」（L 形连通不算）——若按「存在任一 need 格」判定，
    /// 一个孤立格就会让保底静默失效（其后又被清孤立清掉 ⇒ 保底彻底落空，L-01「就位≠生效」）。
    /// side=1 时与旧 EnsureOne 逐格语义完全等价（tree/stone_pile 行为零变化）。</summary>
    static void EnsureBlock(MapData map, Vector2Int sp, int radius, FeatureType need, int side)
    {
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
                if (HasFullBlock(map, sp.x + dx, sp.y + dy, need, side)) return;   // 已有完整块
        // 缺 → 一级：半径内找一块 side×side 全 Plain 空位补上（最干净，side=1 时即旧语义）
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
                if (IsClearBlock(map, sp.x + dx, sp.y + dy, side))
                {
                    StampBlock(map, sp.x + dx, sp.y + dy, need, side);
                    return;
                }
        // 二级（仅巨型簇 side>1，不改变 tree/stone 的 1×1 旧语义）：退而求「不含已有同特征」的完整位，保底不静默落空
        if (side > 1)
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    if (!BlockHasFeature(map, sp.x + dx, sp.y + dy, need, side))
                    {
                        StampBlock(map, sp.x + dx, sp.y + dy, need, side);
                        Debug.LogWarning($"[MapGenRules] 簇保底降级：出生点 {sp} 半径内无全 Plain 位，改用非空位盖章 {need} {side}×{side}（D618）。");
                        return;
                    }
        Debug.LogWarning($"[MapGenRules] 簇保底失败：出生点 {sp} 半径 {radius} 内无 {side}×{side} 完整空位可放 {need}（D618）；不硬塞。");
    }

    // ===== 步骤 9：海洋边缘 + 湖泊 + 河流 =====
    public static void PlaceWater(System.Random rng, MapData map, WorldSize size)
    {
        PlaceOcean(map);
        PlaceLakes(rng, map);
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
                map.features[Idx(map, x, y)] = FeatureType.Plain;
                pruned++;
            }
        if (pruned > 0)
            Debug.LogWarning($"[MapGenRules] 清孤立矿山格 {pruned} 个（D618；期望=0，非 0 说明上游存在未见覆写路径）。");
    }

    static void PlaceOcean(MapData map)
    {
        int thickness = OceanThickness;   // 海洋边缘厚度（件12：与矿山簇撒布内缩边界共用常量）
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
                if (x < thickness || y < thickness || x >= map.width - thickness || y >= map.height - thickness)
                    map.features[Idx(map, x, y)] = FeatureType.Ocean;
    }

    static void PlaceLakes(System.Random rng, MapData map)
    {
        int lakeCount = Mathf.Max(1, (map.width / 128));   // 256² → 2 个湖
        for (int n = 0; n < lakeCount; n++)
        {
            int bw = rng.Next(3, 6), bh = rng.Next(3, 6);
            int ox = rng.Next(ChunkSize, Mathf.Max(ChunkSize + 1, map.width - ChunkSize - bw));
            int oy = rng.Next(ChunkSize, Mathf.Max(ChunkSize + 1, map.height - ChunkSize - bh));
            if (NearAnySpawn(map, ox, oy, bw, bh, ChunkSize)) continue;   // 避开出生点 1 大区块
            if (RectHasFeature(map, ox, oy, bw, bh, FeatureType.Mine)) continue;   // DZ-084：避让矿山簇（禁切碎，整湖跳过保矩形完整）
            for (int y = oy; y < oy + bh; y++)
                for (int x = ox; x < ox + bw; x++)
                    if (InB(map, x, y)) map.features[Idx(map, x, y)] = FeatureType.Lake;
        }
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
            // DZ-084：不覆盖湖，也不覆盖矿山簇（河穿矿=该格岩石出露，河在此断开——D618 认可语义）
            if (cur != FeatureType.Lake && cur != FeatureType.Mine)
                map.features[Idx(map, x, y)] = FeatureType.River;
            // 朝对岸推进 + 随机侧移
            if (horizontal) { x++; if (rng.NextDouble() < 0.4) y += rng.Next(-1, 2); }
            else { y++; if (rng.NextDouble() < 0.4) x += rng.Next(-1, 2); }
            x = Mathf.Clamp(x, 0, map.width - 1);
            y = Mathf.Clamp(y, 0, map.height - 1);
            if (horizontal && x >= map.width - 1) break;
            if (!horizontal && y >= map.height - 1) break;
        }
    }

    static bool NearAnySpawn(MapData map, int ox, int oy, int bw, int bh, int pad)
    {
        foreach (var sp in map.kingdomSpawns)
            if (sp.x >= ox - pad && sp.x < ox + bw + pad && sp.y >= oy - pad && sp.y < oy + bh + pad)
                return true;
        return false;
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

    // ===== 步骤 11：naturalBuildings 派生（视觉层/一次性可采集实体，不反向改可走）=====
    // A+（HH.2）落地：树/矿/雪山不再派生 Building 实体——它们归 2_10 Tilemap 特征层渲染 +
    // features 数据承载（装饰持续节点），不再建 1.6 万个 GameObject（消灭加载 20s 根因）。
    // 仅真正一次性可采集的 OreVein 保留 Building 实体（走 BuildingPanel 采集销毁链路，2_12 不受影响）。
    public static void DeriveNaturalBuildings(MapData map)
    {
        map.naturalBuildings.Clear();
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                var f = map.features[Idx(map, x, y)];
                // HH.10 裁决三：一次性可采集实体扩到 OreVein/WoodPile/StonePile 三类。
                //   （此前仅 OreVein → WoodPile/StonePile 格存在但无实体，工人采不到，木/石断供。）
                //   Tree 走数据格采集（数据化，不建实体，防止 A+ 复辟），故不在此派生。
                if (f != FeatureType.OreVein && f != FeatureType.WoodPile && f != FeatureType.StonePile) continue;
                map.naturalBuildings.Add(new NaturalBuilding
                {
                    cellX = x, cellY = y, w = 1, h = 1,
                    feature = f,
                    climate = ZoneOf(map, x, y),
                    artId = f.ToString()
                });
            }
    }
}
