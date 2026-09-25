using UnityEngine;

/// <summary>
/// 建筑占位视觉辅助（3.3.4 问题12 / HH.239 T7~T9）。
/// 在 def.prefab 为 null 时，按 BuildingType/BuildingRole/def.id 求 **artId**，走
/// <c>PlaceholderSprites.Get(artId, level)</c> **单一注入点**：命中 SpriteRefTable 真图 → 未命中回退等轴占位。
/// BuildingFactory 实例化建筑、BuildController 创建 ghost 时共用，保证视觉一致。
/// 收敛：本类**不再返回 1D legacy key**（`"tree"/"castle"/"tower_arrow"/…` 已全部收敛为 artId，T7/D5）。
/// </summary>
public static class BuildingVisual
{
    /// <summary>族别名（RaceIds 空间 0=human/1=elf/2=dwarf/3=orc）。</summary>
    public static readonly string[] RaceNames = { "human", "elf", "dwarf", "orc" };

    public static string RaceName(int raceId) =>
        (raceId >= 0 && raceId < RaceNames.Length) ? RaceNames[raceId] : RaceNames[0];

    /// <summary>按 BuildingType + BuildingRole + def.id 取 artId（D37 唯一源；无 legacy key）。</summary>
    public static string GetPlaceholderKey(BuildingType type, BuildingRole role, string buildingId = "")
    {
        // 0. def.id 精确映射（Resources/Buildings/ 实盘 id，E6 逐一挂实·禁虚挂 L-15）
        if (!string.IsNullOrEmpty(buildingId))
        {
            switch (buildingId)
            {
                // 四族专属（2_20 M6）
                case "WarAcademy":   return "bld_waracademy";
                case "WarCamp":      return "bld_warcamp";
                case "LeyForge":     return "bld_leyforge";
                case "ArcheryRange": return "bld_archeryrange";
                // 普通建筑（T8 补键）
                case "House":          return "bld_house";
                case "farm":           return "bld_farm";
                case "farmland":       return "bld_farm";
                case "Warehouse":      return "bld_warehouse";
                case "market":         return "bld_market";
                case "mine":           return "bld_mine_b";
                case "quarry":         return "bld_mine_b";
                case "bridge":         return "bld_bridge";
                case "gate":           return "bld_gate";
                case "wall":           return "bld_wall";
                case "arrow_tower":    return "bld_tower";
                case "Barracks":       return "bld_barracks";
                case "TrainingCamp":   return "bld_barracks";
                case "TrainingGround": return "bld_barracks";
                case "Blacksmith":     return "bld_blacksmith";
                case "Hospital":       return "bld_hospital";
                case "Church":         return "bld_church";
                case "SiegeWorkshop":  return "bld_siegeworkshop";
                case "catapult":       return "bld_siegeworkshop";
                case "Ranch":          return "bld_ranch";
                case "Well":           return "bld_well";
                case "CrossbowTower":  return "bld_crossbowtower";
                case "MagicTower":     return "bld_magictower";
                case "magic_tower":    return "bld_magictower";
                case "VagrantCamp":    return "bld_vagrant_camp";
                case "Granary":        return "bld_warehouse";       // 缺图面 → 就近占位（§七 缺图回退）
                case "AdvancedStorage":return "bld_warehouse";
                case "FoodWorkshop":   return "bld_market";
                case "castle":         return "bld_castle";
                case "CastleCore":     return "bld_castle";
                // 地图资源点
                case "tree":           return "feat_tree_temperate";
                case "ore_vein":       return "feat_orevein";
                case "stone_pile":     return "feat_stone_pile";
                case "wood_pile":      return "feat_deadwood";
            }
        }
        // 1. 地图预置建筑按 BuildingType 选
        switch (type)
        {
            case BuildingType.Tree:        return "feat_tree_temperate";
            case BuildingType.Mine:        return "feat_mine";
            case BuildingType.Farmland:    return "bld_farm";
            case BuildingType.StonePile:   return "feat_stone_pile";
            case BuildingType.WoodPile:    return "feat_deadwood";
            case BuildingType.OreVein:     return "feat_orevein";
            case BuildingType.TreasureBox: return "feat_treasure_box";
            case BuildingType.Ruins:       return "bld_ruins";
            case BuildingType.Rift:        return "bld_portal";
            case BuildingType.CastleCore:  return "bld_castle";
            case BuildingType.VagrantCamp: return "bld_vagrant_camp";
        }
        // 2. sourceType=None 的玩家建造建筑，按 role 选
        switch (role)
        {
            case BuildingRole.Production: return "bld_mine_b";   // 生产建筑（采石/矿场/农场）
            case BuildingRole.Economy:    return "bld_warehouse";
            case BuildingRole.Defense:    return "bld_tower";
            case BuildingRole.Wall:       return "bld_wall";
            case BuildingRole.Special:    return "bld_portal";
        }
        return "unknown";
    }

    /// <summary>H2（硬条款）：<c>feat_tree_{climate}</c> 3 变体按**格坐标确定性哈希**选变体
    /// （同格恒定／同 seed 稳定／零 rng 流污染／零存档）。</summary>
    public static string TreeArtId(GridCoord? coord)
    {
        string climate = ClimateName(coord);
        int v = 0;
        if (coord.HasValue)
        {
            unchecked { v = (coord.Value.x * 73856093 ^ coord.Value.y * 19349663) % 3; }
            if (v < 0) v = -v;
        }
        return $"feat_tree_{climate}_{v + 1}";
    }

    /// <summary>格所在温度带名（MapGenRules.ZoneOf；缺图未就绪回退 temperate）。</summary>
    public static string ClimateName(GridCoord? coord)
    {
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null || !coord.HasValue || map.climateZones == null) return "temperate";
        switch (MapGenRules.ZoneOf(map, coord.Value.x, coord.Value.y))
        {
            case ClimateZone.Tropical:    return "tropical";
            case ClimateZone.Subtropical: return "subtropical";
            case ClimateZone.Cold:        return "cold";
            default:                      return "temperate";
        }
    }

    /// <summary>
    /// 给 go 挂/取占位 SpriteRenderer（sortingOrder=1 建筑层）。
    /// level&gt;0 = 等级态（主城 lv0~6 / 普通建筑 lv1~3）；raceId = 主城族别（RaceIds 空间）。
    /// </summary>
    public static SpriteRenderer ApplyPlaceholder(GameObject go, BuildingType type, BuildingRole role,
        string buildingId = "", int level = 0, int raceId = -1, GridCoord? coord = null)
    {
        if (go == null) return null;
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();

        string artId = GetPlaceholderKey(type, role, buildingId);
        // 主城按族（T11/D4）：bld_castle_{race} + level → bld_castle_{race}_lv{n}
        if (type == BuildingType.CastleCore || buildingId == "castle" || buildingId == "CastleCore")
            artId = "bld_castle_" + RaceName(raceId >= 0 ? raceId : RaceIds.Human);
        // 树 3 变体（H2 确定性哈希）
        else if (type == BuildingType.Tree || buildingId == "tree")
            artId = TreeArtId(coord);

        sr.sprite = global::ValleyRampart.Rendering.PlaceholderSprites.Get(artId, level);
        sr.sortingOrder = 1; // 建筑层（Region:0, Building:1, Baseline:2）
        return sr;
    }
}
