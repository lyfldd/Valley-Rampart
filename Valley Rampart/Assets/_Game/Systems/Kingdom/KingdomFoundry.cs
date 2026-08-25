using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  王国立国/出生服务（2_16 步骤5，D284/D290/D293/D304/D309/D310/D315）
//  FoundFirstGeneration：第一代立国核心——
//    消费 map.kingdomTemplates[1..N]（步骤3 已放置出生点并抽模板，放置/立国同模板保确定性）
//    → 按错峰档预置建筑 + 直出实体工人 + 起始国库账本 → Registry 注册 → 播报事件（步骤7 汇总一条）。
//  挂载点：WorldManager.GenerateMap 末尾（步骤3 同 rng 派生链，确定性）。
//
//  人口口径（2_17 步骤3/4）：
//    - 步骤3 实体化：直出首代实体工人（Faction.Human_Player + kingdomId>0 冒充态，靠 kingdomId 守卫隔离）。
//    - 步骤4 台账转派生：workerCount/warriorCount 由 KingdomState 对实体按 kingdomId 派生（实体=唯一真源），
//      Foundry 不再手写台账——此处档位 workerCount 仅作生成指令数。AI 工人不入玩家人口（双条件守卫）。
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

            // 2_17 步骤3 实体化（裁①）+ 步骤4 台账转派生（①真源演进）：
            // 工人/战士直出实体，workerCount/warriorCount 不再手写台账——由 KingdomState 属性对实体按 kingdomId 派生
            // （实体=唯一真源）。此处 spawnWorkerCount 仅作档位生成指令，不写入台账。
            // 守卫已就位：选中/人口台账均 kingdomId 过滤、任务路由池隔离、怪物仅袭玩家建筑（kingdomId==0）→ AI 工人安全出场。
            int spawnWorkerCount = Mathf.Max(0, tier.workerCount);
            SpawnAiWorkers(map, map.kingdomSpawns[i], spawnWorkerCount, state.id);

            // 起始国库过渡账本（AI 2_17 前无脑不消费，零风险；ResourcePack 为 struct 无需判空）
            state.resources = tier.stockpile;

            // 建筑预置（王座 castle + 错峰前 buildingCount-1 个产能）+ 困难档围墙环
            PlaceBuildings(rng, map, map.kingdomSpawns[i], tpl, tier, state.id, cfg, difficulty, foundedDay);

            aiFounded++;
        }

        Debug.Log($"[KingdomFoundry] 第一代立国完成：难度档 {tier.tierName}(difficulty={difficulty}), 立国 {aiFounded} 个 AI 王国, " +
                  $"Registry.Count={registry.Count}（含玩家）.");

        // 2_16 步骤7 D305：开局汇总播报一条，不逐国刷屏（点击展开列国名单归 2_13）
        if (ToastManager.Instance != null)
            ToastManager.Instance.Show($"本大陆已有 {registry.Count} 国并存");
    }

    // ===== 2_17 步骤3 实体化：首代 AI 实体工人 =====

    /// <summary>
    /// 直出首代实体工人（裁①实体化过渡态：台账+实体双写）。
    /// index 均布确定取点、不消耗 rng 流 → 同 seed 逐字节一致（2b ③-a 确定性，见 WorkerCell）。
    /// 守卫已就位：选中/人口台账均 kingdomId 过滤、任务路由池隔离、水井/怪袭仅玩家建筑（kingdomId==0）→ AI 工人安全出场。
    /// 步骤4 人口系统 per-kingdom 时，台账转派生统计、实体=唯一真源（防止双真源漂移）。
    /// </summary>
    private static void SpawnAiWorkers(MapData map, Vector2Int spawn, int count, int kingdomId)
    {
        if (UnitFactory.Instance == null || count <= 0) return;
        int placed = 0;
        var grid = GridSystem.Instance;
        for (int k = 0; k < count; k++)
        {
            Vector2Int cell = WorkerCell(map, spawn, k);
            Vector3 world = (grid != null && grid.Config != null)
                ? (Vector3)grid.CoordToWorld(new GridCoord(cell.x, cell.y))
                : new Vector3(cell.x, cell.y, 0f);
            if (UnitFactory.Instance.SpawnUnit(Faction.Human_Player, Occupation.Worker, world, kingdomId) != null)
                placed++;
        }
        if (placed > 0)
            Debug.Log($"[KingdomFoundry] 王国(kingdomId={kingdomId}) 实体化工人 {placed}/{count}（台账已双写，2_17 步骤3）。");
    }

    /// <summary>首代工人确定取点：出生点 + index 均布环（足迹=1 独占格，spiral 展开；不消耗 rng 流，同 seed 一致）。</summary>
    private static Vector2Int WorkerCell(MapData map, Vector2Int spawn, int index)
    {
        if (index == 0) return MapGenRules.NearestWalkable(map, spawn.x, spawn.y);
        int r = 1 + (index - 1) / 8;              // 环半径（每环 8 方位，逐环外扩、不重叠）
        int slot = (index - 1) % 8;               // 0E 1SE 2N 3SW 4W 5NW 6S 7NE（方位位次固定）
        int x = spawn.x, y = spawn.y;
        switch (slot)
        {
            case 0: x += r; break;
            case 1: x += r; y += r; break;
            case 2: y += r; break;
            case 3: x -= r; y += r; break;
            case 4: x -= r; break;
            case 5: x -= r; y -= r; break;
            case 6: y -= r; break;
            default: x += r; y -= r; break;        // case 7: NE
        }
        return MapGenRules.NearestWalkable(map, x, y);
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

    // ===== 2_16 步骤11：动态立国（FoundFromCamp + ConvertVagrantsToWorkers + 性格混合 D295）=====

    /// <summary>
    /// 动态立国（D294/D311/D313/D314/D385）：把已达标营地（CampUpgrader 五条件已通过）转成正国。
    /// 流程：Registry 注册（id 续号 D385）→ 性格混合（D295）→ 转化流民为工人（统一管线 D306 出口A）→
    /// 营地中心插旗（castle 建筑带 kingdomId；不生成 ThroneAnchor——P0 全局单例会覆写玩家王座锚，per-kingdom 锚归 2_19）
    /// 冷却时间戳 MarkFounding（D312）→ 播报"流民在 X 建立新国家"（中性措辞）→ 移除营地记录（建筑保留可再聚）。
    /// </summary>
    public static void FoundFromCamp(Camp camp, System.Random rng)
    {
        var cfg = Resources.Load<KingdomFoundingConfig>("Config/Kingdoms/KingdomFoundingConfig");
        var registry = KingdomRegistry.Instance;
        if (camp == null || registry == null) return;
        int currentDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;

        // 来源国统计（D295/D308：流民 originKingdomId；全无来源 → 中性基线）
        var source = CollectSourceKingdom(camp, registry);

        string name = source.Item1 != null
            ? source.Item1.name + "·拓荒新域（D296占位）"
            : "无主拓荒之地";
        Color banner = source.Item1 != null ? source.Item1.bannerColor : default;

        var state = registry.RegisterNewKingdom(name, banner, currentDay,
            templateSourceId: source.Item1 != null ? source.Item1.id : -1);

        // 性格：来源国加权混合 + ±10% 扰动 → clamp（D295；全无来源基线=五轴 0.5 中性）
        float perturbation = cfg != null ? cfg.dynamicPerturbation : 0.10f;
        state.personality = BlendPersonality(camp.memberIds, registry, perturbation,
            cfg != null ? cfg.personalityClampMin : 0.05f, cfg != null ? cfg.personalityClampMax : 0.95f, rng);

        // 转化：流民→工人（人口守恒 D306 出口A，实体还是那批人）
        int converted = ConvertVagrantsToWorkers(camp.memberIds, state.id);
        // 2_17 步骤4 台账转派生：转化即实体改职业，workerCount 由 KingdomState 属性对王国实体派生，不再手写台账。

        // 营地中心插旗（castle 建筑带 kingdomId；ThroneAnchor 全局单例约束见上方注释）
        PlaceCampCastle(camp, state.id);

        registry.MarkFounding(currentDay);   // D312 冷却时间戳（只由动态立国更新）

        // 保证 _camps 同步（VagrantCampSystem 持记录，此处告知移除）
        if (VagrantCampSystem.Instance != null)
            VagrantCampSystem.Instance.RemoveCamp(camp);
        camp.foundedFlag = true;

        Debug.Log($"[KingdomFoundry] 动态立国: 「{name}」(id={state.id}) 于营地 ({camp.centerCell.x},{camp.centerCell.y})，" +
                  $"转化流民 {converted} → 工人, Registry.Count={registry.Count}（含玩家）, 冷却时间戳={currentDay}");
        if (ToastManager.Instance != null)
            ToastManager.Instance.Show($"流民在 ({camp.centerCell.x},{camp.centerCell.y}) 建立新国家「{name}」");
    }

    /// <summary>把营地流民统一转为某王国工人（D306 两出口共用：动态立国出口A / 吞并出口B）。
    /// SetOccupation(Worker) + kingdomId 标注 + 清流浪态（TaskScheduler 刺激清理 / IsVagrantRecruited/BirthCampPos 复位）；人口守恒（实体还是那批人）。</summary>
    public static int ConvertVagrantsToWorkers(List<int> memberIds, int kingdomId)
    {
        if (memberIds == null || memberIds.Count == 0 || UnitRegistry.Instance == null) return 0;
        var idSet = new HashSet<int>(memberIds);
        int converted = 0;
        foreach (var uc in UnitRegistry.Instance.GetAllUnits())
        {
            if (uc == null || !uc.IsAlive || !idSet.Contains(uc.npcId)) continue;
            if (uc.EffectiveOccupation != Occupation.Vagrant) continue;
            if (uc.IsVagrantRecruited) continue;
            uc.SetOccupation(Occupation.Worker);
            uc.kingdomId = kingdomId;
            uc.IsVagrantRecruited = true;            // 复位流浪态：不再计入营地/招募扫描
            uc.BirthCampPos = Vector2.zero;
            if (TaskScheduler.HasInstance && uc.npcId != 0)
                TaskScheduler.Instance.AbandonTask(uc.npcId);
            converted++;
        }
        return converted;
    }

    /// <summary>收集营地成员主导来源王国（出现次数最多的非 -1 originKingdomId；全 -1 返回 null）。</summary>
    static (KingdomState, int) CollectSourceKingdom(Camp camp, KingdomRegistry registry)
    {
        if (camp.memberIds == null || UnitRegistry.Instance == null) return (null, 0);
        var counts = new Dictionary<int, int>();
        foreach (var uc in UnitRegistry.Instance.GetAllUnits())
        {
            if (uc == null || !camp.memberIds.Contains(uc.npcId) || uc.originKingdomId < 0) continue;
            counts[uc.originKingdomId] = counts.TryGetValue(uc.originKingdomId, out var c) ? c + 1 : 1;
        }
        int bestId = -1, bestN = 0;
        foreach (var kv in counts) if (kv.Value > bestN) { bestN = kv.Value; bestId = kv.Key; }
        var st = bestId >= 0 ? registry.Get(bestId) : null;
        return (st, bestN);
    }

    /// <summary>性格混合（D295）：来源国加权混合（有来源流民占比）+ 无来源流民按 0.5 中性计入 + ±扰动 → clamp。</summary>
    static float[] BlendPersonality(List<int> memberIds, KingdomRegistry registry, float perturbation,
        float clampMin, float clampMax, System.Random rng)
    {
        var result = new float[5];
        if (memberIds == null || memberIds.Count == 0 || UnitRegistry.Instance == null)
        {
            for (int i = 0; i < 5; i++) result[i] = 0.5f;   // 空营地基线
            return result;
        }
        var sums = new float[5];
        int weight = 0;
        foreach (var uc in UnitRegistry.Instance.GetAllUnits())
        {
            if (uc == null || !memberIds.Contains(uc.npcId)) continue;
            var src = uc.originKingdomId >= 0 ? registry.Get(uc.originKingdomId) : null;
            for (int i = 0; i < 5; i++)
                sums[i] += src != null ? src.GetPersonality(i) : 0.5f;   // 无来源按中性计入
            weight++;
        }
        for (int i = 0; i < 5; i++)
        {
            float avg = weight > 0 ? sums[i] / weight : 0.5f;
            float n = avg + (float)((rng.NextDouble() * 2.0 - 1.0) * perturbation);
            result[i] = Mathf.Clamp(n, clampMin, clampMax);
        }
        return result;
    }

    /// <summary>营地中心插旗：生成 castle 建筑（带新王国 kingdomId）。ThroneAnchor 全局单例约束下不再额外生成锚实体（per-kingdom 锚归 2_19）。</summary>
    static void PlaceCampCastle(Camp camp, int kingdomId)
    {
        if (camp == null || BuildingFactory.Instance == null) return;
        var def = BuildingFactory.FindDefById("castle");
        if (def == null) { Debug.LogWarning("[KingdomFoundry] castle def 未找到，跳过营地铁旗建筑。"); return; }
        var fp = new Vector2Int(def.footprint.x > 0 ? def.footprint.x : 1,
                               def.footprint.y > 0 ? def.footprint.y : 1);
        var coord = camp.centerCell;
        var grid = GridSystem.Instance;
        Vector3 world = grid != null && grid.Config != null
            ? grid.CoordToWorld(coord) + new Vector2((fp.x - 1) * 0.5f * grid.Config.cellSize.x,
                                                     (fp.y - 1) * 0.5f * grid.Config.cellSize.y)
            : new Vector3(coord.x, coord.y, 0f);
        BuildingFactory.Instance.CreateBuildingInstance(
            def, def.sourceType, coord, fp, world,
            isPlayerBuilt: false, grade: ResourceGrade.Normal, isConsumable: false,
            initialState: BuildingState.Active, kingdomId: kingdomId);
        Debug.Log($"[KingdomFoundry] 营地 ({coord.x},{coord.y}) 插铁旗 castle 建筑（动态王国 id={kingdomId}）。");
    }
}