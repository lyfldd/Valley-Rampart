using UnityEngine;

/// <summary>
/// 建筑占位视觉辅助（3.3.4 问题12）。
/// 在 def.prefab 为 null 时，按 BuildingType/BuildingRole 用 PlaceholderSprites 生成彩色方块占位。
/// BuildingFactory 实例化建筑、BuildController 创建 ghost 时共用，保证视觉一致。
/// 美术到位后只需改 PlaceholderSprites.Get 内部加载逻辑即可全替换。
/// </summary>
public static class BuildingVisual
{
    /// <summary>按 BuildingType + BuildingRole 取占位 sprite key。</summary>
    public static string GetPlaceholderKey(BuildingType type, BuildingRole role)
    {
        // 1. 地图预置建筑按 BuildingType 选
        switch (type)
        {
            case BuildingType.Tree:        return "tree";
            case BuildingType.Mine:        return "mine";
            case BuildingType.Farmland:    return "farmland";
            case BuildingType.StonePile:   return "stone_pile";
            case BuildingType.WoodPile:    return "wood_pile";
            case BuildingType.OreVein:     return "ore_vein";
            case BuildingType.TreasureBox: return "treasure_box";
            case BuildingType.Ruins:       return "ruins";
            case BuildingType.Rift:        return "rift";
            case BuildingType.CastleCore:  return "castle";
        }
        // 2. sourceType=None 的玩家建造建筑，按 role 选
        switch (role)
        {
            case BuildingRole.Production: return "mine";   // 生产建筑（采石/矿场/农场）占位；木作废产能建筑(2_12)
            case BuildingRole.Economy:    return "castle";
            case BuildingRole.Defense:    return "tower_arrow";
            case BuildingRole.Wall:       return "wall";
            case BuildingRole.Special:    return "rift";
        }
        return "unknown";
    }

    /// <summary>给 go 挂/取占位 SpriteRenderer（sortingOrder=1 建筑层）。返回该 SpriteRenderer。</summary>
    public static SpriteRenderer ApplyPlaceholder(GameObject go, BuildingType type, BuildingRole role)
    {
        if (go == null) return null;
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PlaceholderSprites.Get(GetPlaceholderKey(type, role));
        sr.sortingOrder = 1; // 建筑层（Region:0, Building:1, Baseline:2）
        return sr;
    }
}
