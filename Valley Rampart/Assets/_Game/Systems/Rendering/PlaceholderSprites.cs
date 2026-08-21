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
///   - bld_lumber（伐木场）**不生成**（2_12 作废），Get 返回 null。
///   - 地形高度（ground tile）默认 footprint 1×1 无高度 → CreateIsoTile。
/// 使用：美术到位后把本表占位替换为 ArtAsset 查询（SpriteRefTable，2_10 步骤10），逻辑零改动。
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
        // ===== ground_ 地皮（温度带 4 + 水系，1×1 无高）=====
        { "ground_tropical",     new Def(1, 1, 0, new Color(0.30f, 0.55f, 0.30f)) },
        { "ground_subtropical",  new Def(1, 1, 0, new Color(0.40f, 0.60f, 0.32f)) },
        { "ground_temperate",    new Def(1, 1, 0, new Color(0.36f, 0.58f, 0.38f)) },
        { "ground_cold",         new Def(1, 1, 0, new Color(0.75f, 0.80f, 0.78f)) },

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
        { "feat_wood_pile",        new Def(1, 1, 1, new Color(0.50f, 0.35f, 0.20f)) }, // 木堆
        { "feat_water_river",      new Def(1, 1, 0, new Color(0.28f, 0.50f, 0.70f)) }, // 河
        { "feat_water_lake",       new Def(1, 1, 0, new Color(0.25f, 0.45f, 0.68f)) }, // 湖
        { "feat_water_ocean",      new Def(1, 1, 0, new Color(0.15f, 0.35f, 0.62f)) }, // 海
        { "feat_water_ice",        new Def(1, 1, 0, new Color(0.75f, 0.85f, 0.90f)) }, // 冰河

        // ===== bld_ 人造建筑 =====
        { "bld_house",      new Def(2, 2, 2, new Color(0.75f, 0.55f, 0.30f)) },  // 民居 2×2
        { "bld_wall",       new Def(1, 1, 1, new Color(0.55f, 0.55f, 0.58f)) },  // 城墙 1×1
        { "bld_gate",       new Def(2, 1, 2, new Color(0.50f, 0.45f, 0.35f)) },  // 城门 2×1
        { "bld_tower",      new Def(1, 1, 3, new Color(0.55f, 0.50f, 0.40f)) },  // 箭塔 1×1 高3
        { "bld_castle",     new Def(3, 3, 4, new Color(0.80f, 0.70f, 0.30f)) },  // 主城 3×3 高4
        { "bld_farm",       new Def(2, 2, 0, new Color(0.72f, 0.68f, 0.20f)) },  // 农田 2×2 无高
        { "bld_mine_b",     new Def(2, 2, 1, new Color(0.40f, 0.42f, 0.48f)) },  // 矿场 2×2
        { "bld_wall_barr",  new Def(1, 1, 1, new Color(0.55f, 0.35f, 0.25f)) },  // 拒马 1×1 高1
        { "bld_bridge",     new Def(1, 1, 0, new Color(0.55f, 0.40f, 0.25f)) },  // 桥 1×1 无高
        { "bld_warehouse",  new Def(2, 2, 2, new Color(0.55f, 0.42f, 0.20f)) },  // 仓库 2×2
        { "bld_market",     new Def(2, 2, 2, new Color(0.70f, 0.50f, 0.30f)) },  // 市场 2×2
        { "bld_academy",    new Def(2, 2, 2, new Color(0.35f, 0.45f, 0.65f)) },  // 学院 2×2
        { "bld_treasury",   new Def(2, 2, 2, new Color(0.85f, 0.70f, 0.20f)) },  // 税务所 2×2

        // ===== mark_ 调试 =====
        { "mark_highlight", new Def(1, 1, 1, new Color(1f, 1f, 0.2f, 0.5f)) },   // 选择高亮
    };

    private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    /// <summary>取占位 sprite（命不中返回 null；bld_lumber 无定义→null 符合 2_12 作废）。</summary>
    public static Sprite Get(string artId)
    {
        if (string.IsNullOrEmpty(artId)) return null;
        if (_cache.TryGetValue(artId, out var s)) return s;
        if (!_defs.TryGetValue(artId, out var d)) return null;
        Sprite spr;
        if (d.layers <= 0)
            spr = SpriteFactory.CreateIsoTile(d.w, d.h, d.color);
        else
            spr = SpriteFactory.CreateIsoCube(d.w, d.h, d.layers, d.color);
        _cache[artId] = spr;
        return spr;
    }

    /// <summary>预生成全部 artId（LoadManager 加载期调用）。</summary>
    public static void PreloadAll()
    {
        foreach (var k in _defs.Keys) Get(k);
    }
}
}