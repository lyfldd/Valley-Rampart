using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 占位 sprite 注册中心。按 key 获取，自动缓存。
/// 美术到位后：把 Get 改为先 Resources.Load("Art/" + key)，找不到再生成占位。
/// </summary>
public static class PlaceholderSprites
{
    private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    /// <summary>按 key 获取 sprite。优先从缓存取，没有则生成。</summary>
    public static Sprite Get(string key)
    {
        if (_cache.TryGetValue(key, out Sprite s)) return s;
        s = CreatePlaceholder(key);
        _cache[key] = s;
        return s;
    }

    /// <summary>预生成所有占位 sprite（启动时调一次，避免运行时卡顿）。</summary>
    public static void PreloadAll()
    {
        string[] keys = {
            "monarch", "villager", "monster_small", "monster_boss",
            "soldier_infantry", "soldier_archer", "soldier_cavalry",
            "tower_arrow", "tower_catapult", "trap", "wall",
            "farm", "mine", "lumbermill", "shipyard", "castle",
            "projectile_arrow", "projectile_stone", "rift", "unknown",
            // 3.3.4 问题12：地图资源点 + 一次性采集占位
            "tree", "farmland", "treasure_box", "ruins",
            "ore_vein", "stone_pile", "wood_pile", "scaffold"
        };
        foreach (var key in keys) Get(key);
        Debug.Log($"[PlaceholderSprites] 预生成 {keys.Length} 个占位 sprite");
    }

    private static Sprite CreatePlaceholder(string key)
    {
        switch (key)
        {
            // ===== 单位 =====
            case "monarch":           return SpriteFactory.CreateSquare(64, new Color(0.2f, 0.4f, 0.8f));
            case "villager":          return SpriteFactory.CreateSquare(32, new Color(0.9f, 0.8f, 0.2f));
            case "monster_small":     return SpriteFactory.CreateTriangle(32, Color.red);
            case "monster_boss":      return SpriteFactory.CreateSquare(64, new Color(0.5f, 0f, 0f));

            // ===== 王国兵（多兵种）=====
            case "soldier_infantry":  return SpriteFactory.CreateSquare(32, new Color(0.6f, 0.1f, 0.1f));
            case "soldier_archer":    return SpriteFactory.CreateSquare(32, new Color(0.9f, 0.5f, 0f));
            case "soldier_cavalry":   return SpriteFactory.CreateSquare(32, new Color(0.5f, 0.2f, 0.6f));

            // ===== 防御建筑 =====
            case "tower_arrow":       return SpriteFactory.CreateRect(32, 64, new Color(0.2f, 0.6f, 0.2f));
            case "tower_catapult":    return SpriteFactory.CreateSquare(48, new Color(0.1f, 0.4f, 0.1f));
            case "trap":              return SpriteFactory.CreateTriangle(32, new Color(0.6f, 0.5f, 0.1f));
            case "wall":              return SpriteFactory.CreateSquare(32, new Color(0.5f, 0.5f, 0.5f));

            // ===== 资源建筑 =====
            case "farm":              return SpriteFactory.CreateSquare(48, new Color(0.7f, 0.8f, 0.2f));
            case "mine":              return SpriteFactory.CreateCircle(48, new Color(0.3f, 0.3f, 0.3f));
            case "lumbermill":        return SpriteFactory.CreateSquare(48, new Color(0.1f, 0.4f, 0.1f));
            case "shipyard":          return SpriteFactory.CreateSquare(64, new Color(0.2f, 0.6f, 0.6f));
            case "castle":            return SpriteFactory.CreateSquare(128, new Color(0.9f, 0.8f, 0.2f));

            // ===== 投射物 + 特殊 =====
            case "projectile_arrow":  return SpriteFactory.CreateSquare(4, Color.white);
            case "projectile_stone":  return SpriteFactory.CreateCircle(8, Color.gray);
            case "rift":              return SpriteFactory.CreateCircle(64, new Color(0.3f, 0f, 0.3f));

            // ===== 3.3.4 问题12：地图资源点（原始资源点，自身不产出）=====
            case "tree":              return SpriteFactory.CreateSquare(48, new Color(0.1f, 0.5f, 0.1f));   // 绿
            case "farmland":          return SpriteFactory.CreateSquare(48, new Color(0.8f, 0.7f, 0.1f));   // 黄
            case "treasure_box":      return SpriteFactory.CreateSquare(32, new Color(1f, 0.9f, 0.2f));     // 亮黄
            case "ruins":             return SpriteFactory.CreateSquare(48, new Color(0.4f, 0.3f, 0.5f));   // 灰紫

            // ===== 3.3.4 问题12：一次性采集点 =====
            case "ore_vein":          return SpriteFactory.CreateSquare(48, new Color(0.3f, 0.3f, 0.35f));  // 深灰
            case "stone_pile":        return SpriteFactory.CreateSquare(40, new Color(0.6f, 0.6f, 0.6f));   // 浅灰
            case "wood_pile":         return SpriteFactory.CreateSquare(40, new Color(0.5f, 0.35f, 0.2f));  // 棕

            // ===== 3.3.4 批次3：脚手架（建造中视觉）=====
            case "scaffold":          return SpriteFactory.CreateSquare(48, new Color(0.6f, 0.45f, 0.25f, 0.6f)); // 半透明棕

            // ===== 未配置警告 =====
            default:
                Debug.LogWarning($"[PlaceholderSprites] 未配置 key: {key}，用品红方块占位");
                return SpriteFactory.CreateSquare(32, Color.magenta);
        }
    }
}
