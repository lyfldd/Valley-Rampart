using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValleyRampart.Rendering
{
/// <summary>
/// artId 等轴占位 sprite 表（2_10 步骤4，迁移自 Core/PlaceholderSprites.cs 1D key 表 → artId 等轴表）。
/// 权威 artId 源 = 美术资源规范_等轴立方体瓦片.md（D37 唯一源）。
///
/// 规则：
///   - key = artId（feat_*/bld_*/ground_*/mark_*），占位期 = 菱形/几何 + 调试配色。
///   - pivot = 地基菱形底面中心（锚点铁律，§1.1）；heightLayer 决定 CreateIsoTile(无高)/CreateIsoCube(有高)。
///   - sprite 总高 = 地基高(64×footprintH) + 高度层数×32；总宽 = footprintW×128（§4.2）。
///   - 已作废建筑**不生成**（2_12），Get 返回 null。
///   - 地形高度（ground tile）默认 footprint 1×1 无高度 → CreateIsoTile。
/// 使用（HH.239/D707）：<see cref="Get(string,int)"/> = **单一注入点** —— 先查 <see cref="SpriteRefTable"/> 真图，
/// 未命中回退本表占位（缺图回退链；不崩不空白）。
/// </summary>
public static class PlaceholderSprites
{
    // footprint(宽×高格)、height 层、artId→基础色。数据与美术资源规范表一致。
    private sealed class Def
    {
        public int w, h, layers;
        public Color color;
        public Def(int w, int h, int layers, Color color) { this.w = w; this.h = h; this.layers = layers; this.color = color; }
    }

    private static readonly Dictionary<string, Def> _defs = new Dictionary<string, Def>
    {
        // ===== ground_ 地皮（温度带 4 + 水系 2，1×1 无高；R5 收敛：以素材实际 6 张为准）=====
        { "ground_tropical",     new Def(1, 1, 0, new Color(0.30f, 0.55f, 0.30f)) },
        { "ground_subtropical",  new Def(1, 1, 0, new Color(0.40f, 0.60f, 0.32f)) },
        { "ground_temperate",    new Def(1, 1, 0, new Color(0.36f, 0.58f, 0.38f)) },
        { "ground_cold",         new Def(1, 1, 0, new Color(0.75f, 0.80f, 0.78f)) },
        { "ground_ocean",        new Def(1, 1, 0, new Color(0.15f, 0.35f, 0.62f)) }, // R5 补键（原 feat_water_ocean）
        { "ground_river",        new Def(1, 1, 0, new Color(0.28f, 0.50f, 0.70f)) }, // R5 补键（原 feat_water_river）

        // ===== feat_ 自然特征 =====
        { "feat_tree_tropical",    new Def(1, 1, 2, new Color(0.18f, 0.60f, 0.30f)) }, // 棕榈（绿圆棕干→占位绿菱）
        { "feat_tree_subtropical", new Def(1, 1, 2, new Color(0.20f, 0.55f, 0.25f)) }, // 阔叶
        { "feat_tree_temperate",   new Def(1, 1, 2, new Color(0.15f, 0.45f, 0.20f)) }, // 针叶
        { "feat_tree_cold",        new Def(1, 1, 1, new Color(0.30f, 0.50f, 0.55f)) }, // 寒带矮树
        { "feat_mountain",         new Def(1, 1, 3, new Color(0.50f, 0.44f, 0.38f)) }, // 山
        { "feat_snowmountain",     new Def(1, 1, 3, new Color(0.85f, 0.88f, 0.92f)) }, // 雪山
        { "feat_mine",             new Def(2, 2, 1, new Color(0.42f, 0.40f, 0.44f)) }, // 矿洞 2×2
        { "feat_orevein",          new Def(1, 1, 1, new Color(0.50f, 0.38f, 0.50f)) }, // 矿脉
        { "feat_stone_pile",       new Def(1, 1, 1, new Color(0.60f, 0.60f, 0.60f)) }, // 石堆
        { "feat_deadwood",         new Def(1, 1, 1, new Color(0.50f, 0.35f, 0.20f)) }, // 枯木（D2 键改名 feat_wood_pile→feat_deadwood）
        { "feat_treasure_box",     new Def(1, 1, 1, new Color(1f, 0.9f, 0.2f)) },      // 宝箱（原 1D key treasure_box 迁移）

        // ===== bld_ 人造建筑 =====
        { "bld_house",      new Def(2, 2, 2, new Color(0.75f, 0.55f, 0.30f)) },  // 民居 2×2
        { "bld_wall",       new Def(1, 1, 1, new Color(0.55f, 0.55f, 0.58f)) },  // 城墙 1×1
        { "bld_gate",       new Def(2, 1, 2, new Color(0.50f, 0.45f, 0.35f)) },  // 城门 2×1
        { "bld_tower",      new Def(1, 1, 3, new Color(0.55f, 0.50f, 0.40f)) },  // 箭塔 1×1 高3
        { "bld_castle",     new Def(3, 3, 4, new Color(0.80f, 0.70f, 0.30f)) },  // 主城 3×3 高4（四族走 _aliases 归一）
        { "bld_farm",       new Def(2, 2, 0, new Color(0.72f, 0.68f, 0.20f)) },  // 农田 2×2 无高
        { "bld_mine_b",     new Def(2, 2, 1, new Color(0.40f, 0.42f, 0.48f)) },  // 矿场 2×2
        { "bld_bridge",     new Def(1, 1, 0, new Color(0.55f, 0.40f, 0.25f)) },  // 桥 1×1 无高
        { "bld_warehouse",  new Def(2, 2, 2, new Color(0.55f, 0.42f, 0.20f)) },  // 仓库 2×2
        { "bld_market",     new Def(2, 2, 2, new Color(0.70f, 0.50f, 0.30f)) },  // 市场 2×2
        { "bld_academy",    new Def(2, 2, 2, new Color(0.35f, 0.45f, 0.65f)) },  // 学院 2×2
        { "bld_treasury",   new Def(2, 2, 2, new Color(0.85f, 0.70f, 0.20f)) },  // 税务所 2×2
        // T8 补键（D1/D3 缺图面：素材已有真图，占位供缺图回退）
        { "bld_barracks",       new Def(2, 2, 2, new Color(0.62f, 0.45f, 0.28f)) },  // 练兵场
        { "bld_blacksmith",     new Def(2, 2, 2, new Color(0.45f, 0.42f, 0.45f)) },  // 铁匠铺
        { "bld_hospital",       new Def(2, 2, 2, new Color(0.85f, 0.85f, 0.80f)) },  // 医院
        { "bld_church",         new Def(2, 2, 2, new Color(0.80f, 0.78f, 0.72f)) },  // 教堂
        { "bld_siegeworkshop",  new Def(2, 2, 2, new Color(0.50f, 0.35f, 0.25f)) },  // 战争机器工坊
        { "bld_ranch",          new Def(2, 2, 1, new Color(0.65f, 0.55f, 0.30f)) },  // 牧场
        { "bld_well",           new Def(1, 1, 1, new Color(0.45f, 0.55f, 0.65f)) },  // 水井
        { "bld_crossbowtower",  new Def(1, 1, 3, new Color(0.45f, 0.42f, 0.35f)) },  // 弩塔
        { "bld_magictower",     new Def(1, 1, 3, new Color(0.45f, 0.35f, 0.65f)) },  // 魔法塔
        { "bld_portal",         new Def(2, 2, 2, new Color(0.40f, 0.25f, 0.55f)) },  // 传送门
        { "bld_vagrant_camp",   new Def(1, 1, 1, new Color(0.55f, 0.45f, 0.30f)) },  // 流浪汉营地（D662）
        { "bld_ruins",          new Def(1, 1, 1, new Color(0.40f, 0.30f, 0.50f)) },  // 废墟（原 1D key ruins）
        // 脚手架三档（D642；1×1 兼覆城门）
        { "bld_scaffold_1x1",   new Def(1, 1, 1, new Color(0.60f, 0.45f, 0.25f, 0.6f)) },
        { "bld_scaffold_2x2",   new Def(2, 2, 1, new Color(0.60f, 0.45f, 0.25f, 0.6f)) },
        { "bld_scaffold_3x3",   new Def(3, 3, 1, new Color(0.60f, 0.45f, 0.25f, 0.6f)) },
        // 四族专属建筑（2_20 M6；3.1.2 各族主题色 D457/D458 近似）
        { "bld_waracademy",     new Def(2, 2, 3, new Color(0.95f, 0.80f, 0.20f)) },  // 人类·金
        { "bld_warcamp",        new Def(2, 2, 3, new Color(0.60f, 0.10f, 0.10f)) },  // 兽人·暗红
        { "bld_leyforge",       new Def(2, 2, 3, new Color(0.70f, 0.40f, 0.15f)) },  // 矮人·铜橙
        { "bld_archeryrange",   new Def(2, 2, 3, new Color(0.20f, 0.60f, 0.30f)) },  // 精灵·翠绿

        // ===== mark_ 调试 =====
        { "mark_highlight", new Def(1, 1, 1, new Color(1f, 1f, 0.2f, 0.5f)) },   // 选择高亮
        { "unknown",        new Def(1, 1, 0, Color.magenta) },                    // 未配置兜底（醒目告警色）
    };

    // 归一表：族别/变体键 → 定义键（L-15 禁双源：不为每个族/变体重复定义占位色）
    private static readonly Dictionary<string, string> _aliases = new Dictionary<string, string>
    {
        { "bld_castle_human", "bld_castle" },
        { "bld_castle_orc",   "bld_castle" },
        { "bld_castle_dwarf", "bld_castle" },
        { "bld_castle_elf",   "bld_castle" },
    };

    private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    /// <summary>
    /// 取 sprite（单一注入点）：① 查 <see cref="SpriteRefTable"/> 真图 → ② 未命中回退占位生成。
    /// level&gt;0 时按 <c>{artId}_lv{level}</c> 分级取图（主城/普通建筑等级态）。
    /// 命不中返回 null（已作废建筑无定义→null 符合 2_12）。
    /// </summary>
    public static Sprite Get(string artId, int level = 0)
    {
        if (string.IsNullOrEmpty(artId)) return null;
        string cacheKey = level > 0 ? artId + "#lv" + level : artId;
        if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

        // ① 真图（SpriteRefTable 命中；R4 旁挂键 ammo_*/fx_* 亦在此链）
        Sprite result = null;
        var table = SpriteRefTable.Instance;
        if (table != null)
        {
            if (level > 0) table.TryGetLeveled(artId, level, out result);
            else table.TryGet(artId, out result);
        }

        // ② 未命中 → 占位回退（缺图回退链末端·不崩不空白）
        if (result == null)
        {
            string defKey = NormalizeDefKey(artId);
            if (_defs.TryGetValue(defKey, out var d))
                result = d.layers <= 0
                    ? SpriteFactory.CreateIsoTile(d.w, d.h, d.color)
                    : SpriteFactory.CreateIsoCube(d.w, d.h, d.layers, d.color);
        }

        if (result == null) return null;
        _cache[cacheKey] = result;
        return result;
    }

    /// <summary>分级 artId 归一为占位定义键（去 _lv{n} / 去 _变体号 / 走族别别名）。</summary>
    private static string NormalizeDefKey(string artId)
    {
        if (_defs.ContainsKey(artId)) return artId;
        if (_aliases.TryGetValue(artId, out var alias) && _defs.ContainsKey(alias)) return alias;

        string stripped = artId;
        int lv = stripped.LastIndexOf("_lv", StringComparison.Ordinal);
        if (lv > 0 && int.TryParse(stripped.Substring(lv + 3), out _))
        {
            stripped = stripped.Substring(0, lv);
        }
        else
        {
            int last = stripped.LastIndexOf('_');
            if (last > 0 && int.TryParse(stripped.Substring(last + 1), out _))
                stripped = stripped.Substring(0, last);
        }

        if (_defs.ContainsKey(stripped)) return stripped;
        if (_aliases.TryGetValue(stripped, out var alias2) && _defs.ContainsKey(alias2)) return alias2;
        return artId;
    }

    /// <summary>预生成全部 artId（LoadManager 加载期调用）。</summary>
    public static void PreloadAll()
    {
        foreach (var k in _defs.Keys) Get(k);
    }
}
}
