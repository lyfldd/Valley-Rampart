using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  王国立国/出生服务（2_16 步骤5，D284/D290/D293/D304/D309/D310/D315）
//  FoundFirstGeneration：第一代立国核心——
//    消费 map.kingdomTemplates[1..N]（步骤3 已放置出生点并抽模板，放置/立国同模板保确定性）
//    → 按错峰档预置建筑 + 人口台账 + 起始国库账本 → Registry 注册 → 播报事件（步骤7 汇总一条）。
//  挂载点：WorldManager.GenerateMap 末尾（步骤3 同 rng 派生链，确定性）。
//
//  已知偏差（执行端判断，§二 跨片登记）：
//    - 人口以 KingdomState 台账计数落 workerCount/warriorCount，不实例化单位实体——
//      Faction 枚举无 AI 王国专属阵营（仅 None/Human_Player/Undead），实例化 Human_Player 单位
//      会被 PopulationSystem/HappinessSystem 计入玩家人口污染玩家指标；2_17 引入 AI 阵营后补实体。
//    - AI 王座只放 castle 建筑（带 kingdomId），不挂 ThroneAnchor 组件——ThroneAnchor 是全局单例，
//      多王国同挂会覆写玩家王座锚；AI 灭亡判定归 2_19（届时 ThroneAnchor 改 per-kingdom）。
//    - 围墙环（困难档 D304）：最小矩形环 + 1 城门缺口；遇阻挡格跳段自然缺口（不强制填平）。
//    - templateSourceId 暂留 -1（KingdomDef 无 int id，模板→来源映射归 P2 细化）。
// ============================================================================
public static class KingdomFoundry
{
    /// <summary>
    /// 第一代立国。difficulty=1/2/3（Easy/Normal/Hard）→ 错峰档 index=difficulty-1。
    /// rng 沿用地图生成派生链（R4 确定性，禁 UnityEngine.Random）。
    /// </summary>
    public static void FoundFirstGeneration(System.Random rng, MapData map, int difficulty)
    {
        if (map == null || map.kingdomSpawns == null || map.kingdomSpawns.Count < 2) return;
        var cfg = Resources.Load<KingdomFoundingConfig>("Config/Kingdoms/KingdomFoundingConfig");
        var registry = KingdomRegistry.Instance;
        if (cfg == null || cfg.staggerTiers == null || cfg.staggerTiers.Length == 0 || registry == null) return;

        int tierIndex = Mathf.Clamp(difficulty - 1, 0, cfg.staggerTiers.Length - 1);
        var tier = cfg.staggerTiers[tierIndex];
        if (tier == null) return;

        int foundedDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;
        int aiFounded = 0;

        var templates = map.kingdomTemplates;   // 步骤3 写入：index0=null（玩家），1..N=AI 模板
        for (int i = 1; i < map.kingdomSpawns.Count; i++)
        {
            var tpl = templates != null && i < templates.Count ? templates[i] : null;
            if (tpl == null)
            {
                Debug.LogWarning($"[KingdomFoundry] 出生点 {i} 无模板（MapGenRules 未绑定），跳过立国。");
                continue;
            }

            var state = registry.RegisterNewKingdom(
                PickName(rng, tpl), tpl.bannerColor, foundedDay, templateSourceId: -1);

            // 四维性格 ±第一代扰动 → clamp（D290/D311）
            state.personality = Perturb(rng, tpl.GetPersonalityArray(),
                cfg.firstGenPerturbation, cfg.personalityClampMin, cfg.personalityClampMax);

            // 人口台账（AI 无专属 Faction，不实例化单位实体，见文件头偏差说明）
            state.workerCount = Mathf.Max(0, tier.workerCount);
            state.warriorCount = Mathf.Max(0, tier.warriorCount);

            // 起始国库过渡账本（AI 2_17 前无脑不消费，零风险；ResourcePack 为 struct 无需判空）
            state.resources = tier.stockpile;

            // 建筑预置（王座 castle + 错峰前 buildingCount-1 个产能）+ 困难档围墙环
            PlaceBuildings(rng, map, map.kingdomSpawns[i], tpl, tier, state.id, cfg, difficulty, foundedDay);

            aiFounded++;
        }

        Debug.Log($"[KingdomFoundry] 第一代立国完成：难度档 {tier.tierName}(difficulty={difficulty}), 立国 {aiFounded} 个 AI 王国, " +
                  $"Registry.Count={registry.Count}（含玩家）.");
    }

    // ===== 建筑预置 =====

    private static void PlaceBuildings(System.Random rng, MapData map, Vector2Int spawn, KingdomDef tpl,
                                       StaggerTier tier, int kingdomId, KingdomFoundingConfig cfg,
                                       int difficulty, int foundedDay)
    {
        int buildCount = Mathf.Max(1, tier.buildingCount);
        var defs = tpl.baseBuildingDefIds;

        // 建筑按错峰档取前 N 个（王座 city 在前，随后产能；ordering 见 KingdomDef 资产）
        int placed = 0;
        for (int k = 0; k < buildCount && k < (defs != null ? defs.Length : 0); k++)
        {
            var def = BuildingFactory.FindDefById(defs[k]);
            if (def == null)
            {
                Debug.LogWarning($"[KingdomFoundry] {tpl.templateName} 建筑 defId={defs[k]} 未找到，跳过（检查 KingdomDef 资产 baseBuildingDefIds）。");
                continue;
            }

            // 错峰布局：中心王座，其余在周围环带取点（同 rng 派生，确定性）
            Vector2Int cell = BuildingCell(map, spawn, k, buildCount);
            var fp = new Vector2Int(
                def.footprint.x > 0 ? def.footprint.x : 1,
                def.footprint.y > 0 ? def.footprint.y : 1);
            var coord = new GridCoord(cell.x, cell.y);

            if (BuildingFactory.Instance != null &&
                BuildingFactory.Instance.CreateBuildingInstance(
                    def, def.sourceType, coord, fp, FootprintCenterWorld(cell, fp),
                    isPlayerBuilt: false, grade: ResourceGrade.Normal, isConsumable: false,
                    initialState: BuildingState.Active, kingdomId: kingdomId))
                placed++;
        }

        // 围墙环（仅要塞档 D304：最小矩形环 + 1 城门缺口）
        if (tier.hasWallRing)
            PlaceWallRing(map, spawn, kingdomId, tpl);

        Debug.Log($"[KingdomFoundry] {tpl.templateName} 王国(kingdomId={kingdomId}) 建筑预置 {placed}/{buildCount} 座" +
                  (tier.hasWallRing ? ", 含围墙环" : "") + $"（旗色 {tpl.bannerColor}）");
    }

    /// <summary>错峰建筑取点：王座(0)落出生点，其余在 R 环带均布（保确定性）。</summary>
    private static Vector2Int BuildingCell(MapData map, Vector2Int spawn, int index, int buildCount)
    {
        if (index == 0) return MapGenRules.NearestWalkable(map, spawn.x, spawn.y);
        int r = 2 + (index - 1) / 4;             // 环半径，前 4 个在 r=2 环，后续扩环
        int slot = index - 1;                     // 环内序号
        int edge = slot % 4;                      // 0上 1右 2下 3左
        int step = slot / 4;
        int stepMax = r * 2;
        int x = spawn.x, y = spawn.y;
        switch (edge)
        {
            case 0: x = spawn.x - r + Mathf.Min(step, stepMax); y = spawn.y + r; break;
            case 1: x = spawn.x + r; y = spawn.y + r - Mathf.Min(step, stepMax); break;
            case 2: x = spawn.x + r - Mathf.Min(step, stepMax); y = spawn.y - r; break;
            default: x = spawn.x - r; y = spawn.y - r + Mathf.Min(step, stepMax); break;
        }
        return MapGenRules.NearestWalkable(map, x, y);
    }

    /// <summary>要塞档围墙环（D304）：以出生点为中心的最小矩形环 + 1 城门缺口；遇阻挡格跳段自然缺口。</summary>
    private static void PlaceWallRing(MapData map, Vector2Int spawn, int kingdomId, KingdomDef tpl)
    {
        var wallDef = BuildingFactory.FindDefById("wall");
        var gateDef = BuildingFactory.FindDefById("gate");
        if (wallDef == null || gateDef == null)
        {
            Debug.LogWarning("[KingdomFoundry] 围墙/城门 def 未找到（期望 id=wall/gate），跳过围墙环。");
            return;
        }

        int r = Mathf.Max(4, 2 + 2);   // 环半径（错峰建筑最大外扩约 r=2，外扩一环保证包围）
        int top = spawn.y + r, bottom = spawn.y - r, left = spawn.x - r, right = spawn.x + r;

        // 记录已放置墙/门格，避免重复（near 兜底可能重层）
        var placed = new List<Vector2Int>();
        var fp = new Vector2Int(1, 1);

        // 上下两边（含角）
        for (int x = left; x <= right; x++)
        {
            TryPlacePerimeter(map, wallDef, new Vector2Int(x, top), constY: top, kingdomId, placed, fp);
            TryPlacePerimeter(map, wallDef, new Vector2Int(x, bottom), constY: bottom, kingdomId, placed, fp);
        }
        // 左右两边（不含角）
        for (int y = bottom + 1; y < top; y++)
        {
            TryPlacePerimeter(map, wallDef, new Vector2Int(left, y), constY: left, kingdomId, placed, fp);
            TryPlacePerimeter(map, wallDef, new Vector2Int(right, y), constY: right, kingdomId, placed, fp);
        }

        // 城门缺口：选下边中点（可走则放 gate，不可走则自然缺口=不放）
        var gateCell = MapGenRules.NearestWalkable(map, spawn.x, bottom);
        if (gateCell.x >= 0 && IsOnBottom(gateCell, spawn, r))
            PlaceAt(map, gateDef, gateCell.x, gateCell.y, kingdomId, fp);

        Debug.Log($"[KingdomFoundry] 围墙环: {tpl.templateName} r={r}, 墙+门 {placed.Count} 处（遇阻挡自然跳段,D304）。");
    }

    private static void TryPlacePerimeter(MapData map, BuildingDef def, Vector2Int coord, int constY,
                                          int kingdomId, List<Vector2Int> placed, Vector2Int fp)
    {
        var p = MapGenRules.NearestWalkable(map, coord.x, coord.y);
        if (p.x < 0) return;                                   // 无就近可走格 → 自然缺口（D304）
        if (Distance(p, constY) > 1) return;                   // 兜底跳太远 → 视为阻挡缺口，不硬拉墙
        if (placed.Contains(p)) return;
        PlaceAt(map, def, p.x, p.y, kingdomId, fp);
        placed.Add(p);
    }

    private static int Distance(Vector2Int p, int constCoord) =>
        Mathf.Abs(p.x - constCoord) + Mathf.Abs(p.y - constCoord);

    private static bool IsOnBottom(Vector2Int p, Vector2Int spawn, int r) =>
        p.y == spawn.y - r && Mathf.Abs(p.x - spawn.x) <= r;

    private static void PlaceAt(MapData map, BuildingDef def, int x, int y, int kingdomId, Vector2Int fp)
    {
        if (BuildingFactory.Instance == null) return;
        var coord = new GridCoord(x, y);
        BuildingFactory.Instance.CreateBuildingInstance(
            def, def.sourceType, coord, fp, FootprintCenterWorld(new Vector2Int(x, y), fp),
            isPlayerBuilt: false, grade: ResourceGrade.Normal, isConsumable: false,
            initialState: BuildingState.Active, kingdomId: kingdomId);
    }

    private static Vector3 FootprintCenterWorld(Vector2Int cell, Vector2Int fp)
    {
        var grid = GridSystem.Instance;
        if (grid == null || grid.Config == null) return new Vector3(cell.x, cell.y, 0f);
        return grid.CoordToWorld(new GridCoord(cell.x, cell.y)) +
               new Vector2((fp.x - 1) * 0.5f * grid.Config.cellSize.x,
                           (fp.y - 1) * 0.5f * grid.Config.cellSize.y);
    }

    // ===== 命名与性格扰动 =====

    /// <summary>从模板 namePool 抽取显示名（D296 占位；无池则退回模板名）。</summary>
    private static string PickName(System.Random rng, KingdomDef tpl)
    {
        var pool = tpl.namePool;
        if (pool == null || pool.Length == 0) return tpl.templateName;
        return pool[rng.Next(0, pool.Length)];
    }

    /// <summary>四维性格 ±perturbation 扰动后 clamp 到 [min,max]（D290/D311 独立不归一化）。</summary>
    private static float[] Perturb(System.Random rng, float[] baseVals, float perturbation,
                                   float clampMin, float clampMax)
    {
        var result = new float[5];
        for (int i = 0; i < 5; i++)
        {
            float b = i < baseVals.Length ? baseVals[i] : 0.5f;
            float n = b + (float)((rng.NextDouble() * 2.0 - 1.0) * perturbation);
            result[i] = Mathf.Clamp(n, clampMin, clampMax);
        }
        return result;
    }
}