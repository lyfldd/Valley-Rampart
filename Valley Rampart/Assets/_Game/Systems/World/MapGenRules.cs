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
    public static void FillFeatures(System.Random rng, MapData map)
    {
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
                map.features[Idx(map, x, y)] = RollFeature(rng, ZoneOf(map, x, y));
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

    // ===== 步骤 6：王国出生点 =====
    public static void PlaceKingdomSpawns(System.Random rng, MapData map, MapGenRulesConfig cfg, WorldSize size, int aiCount)
    {
        int count = 1 + Mathf.Max(0, aiCount);
        int minDist = cfg != null ? cfg.GetSpawnMinDistance(size) : 32;
        int margin = ChunkSize;                       // 避开海洋边缘
        var spawns = new List<Vector2Int>();

        int guard = 0;
        while (spawns.Count < count && guard++ < 5000)
        {
            int x = rng.Next(margin, Mathf.Max(margin + 1, map.width - margin));
            int y = rng.Next(margin, Mathf.Max(margin + 1, map.height - margin));

            // 间距校验
            bool tooClose = false;
            for (int i = 0; i < spawns.Count; i++)
                if (Vector2Int.Distance(spawns[i], new Vector2Int(x, y)) < minDist) { tooClose = true; break; }
            if (tooClose) continue;

            var p = NearestWalkable(map, x, y);       // R6：落可走格，否则就近
            if (p.x < 0) continue;
            spawns.Add(p);
        }

        map.kingdomSpawns = spawns;
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
            EnsureOne(map, sp, radiusCells, FeatureType.Tree);        // 木
            EnsureOne(map, sp, radiusCells, FeatureType.Mine);        // 矿
            EnsureOne(map, sp, radiusCells, FeatureType.StonePile);   // 石
            // 农田 = Plain 可建位，天然充足，不强制
        }
    }

    /// <summary>半径内若无指定特征物，则在可走格就地补一个。</summary>
    static void EnsureOne(MapData map, Vector2Int sp, int radius, FeatureType need)
    {
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = sp.x + dx, y = sp.y + dy;
                if (InB(map, x, y) && map.features[Idx(map, x, y)] == need) return;   // 已有
            }
        // 缺 → 在半径内找一个可走 Plain 格补上
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = sp.x + dx, y = sp.y + dy;
                if (InB(map, x, y) && map.features[Idx(map, x, y)] == FeatureType.Plain)
                {
                    map.features[Idx(map, x, y)] = need;
                    return;
                }
            }
    }

    // ===== 步骤 9：海洋边缘 + 湖泊 + 河流 =====
    public static void PlaceWater(System.Random rng, MapData map, WorldSize size)
    {
        PlaceOcean(map);
        PlaceLakes(rng, map);
        int riverCount = size == WorldSize.Small ? 1 : size == WorldSize.Medium ? 2 : 3;
        for (int i = 0; i < riverCount; i++) PlaceRiver(rng, map);
    }

    static void PlaceOcean(MapData map)
    {
        int thickness = 2;   // 海洋边缘厚度
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
            if (map.features[Idx(map, x, y)] != FeatureType.Lake)   // 不覆盖湖
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
                    faction = Faction.Undead   // 阶段4前只怪物波次（D38）
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
                if (f != FeatureType.OreVein) continue;   // 仅一次性可采实体（OreVein）
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
