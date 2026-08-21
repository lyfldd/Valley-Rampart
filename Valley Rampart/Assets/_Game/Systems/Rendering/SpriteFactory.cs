using UnityEngine;

namespace ValleyRampart.Rendering
{
/// <summary>
/// 等轴占位 sprite 生成工具（2_10 步骤4，迁移自 Core/SpriteFactory.cs 并 2D 化）。
/// 旧 1D 占位工厂保留在 Core（供 BuildingVisual 等 1D key 消费方），本类只负责等轴菱形/菱体与 artId 占位。
/// 美术到位后改 PlaceholderSprites.Get 为美工资产查询即可整体替换，逻辑零改动。
///
/// 尺寸规范（美术资源规范 §4.2，PPU=100，D27）：
///   - 1 小区块地基 = 128×64px 等轴菱形 → 世界 1.28×0.64。
///   - sprite 总高 = 地基高(64×footprintH) + 高度层数×32。
///   - sprite 总宽 = footprint宽×128。
///   - pivot = 总宽/2, y=地基底（地基菱形底面中心，锚点铁律 §1.1）。
/// </summary>
public static class SpriteFactory
{
    /// <summary>生成等轴菱形瓦片（无高度，如地皮/水）。宽=footprintW×128，高=footprintH×64 @PPU100。pivot=底面中心。</summary>
    public static Sprite CreateIsoTile(int footprintW, int footprintH, Color color)
    {
        int w = footprintW * 128;
        int h = footprintH * 64;
        return CreateIsoDiamond(w, h, color);
    }

    /// <summary>生成带高度层的等轴菱体（建筑/树）。底部 2:1 菱形地基 + 上部立方体侧面。pivot=底面对称中心。</summary>
    public static Sprite CreateIsoCube(int footprintW, int footprintH, int heightLayer, Color color)
    {
        int w = footprintW * 128;
        int groundH = footprintH * 64;
        int wallH = heightLayer * 32;
        int totalH = groundH + wallH;
        return CreateIsoCubeSprite(w, groundH, wallH, color);
    }

    // ===================== 内部纹理生成 =====================

    /// <summary>2:1 菱形（w:h=2:1），pivot 底面中心（几何中心）。</summary>
    private static Sprite CreateIsoDiamond(int w, int h, Color color)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var px = new Color[w * h];
        float ty = (h - 1) * 0.5f;   // 垂直中线
        for (int y = 0; y < h; y++)
        {
            float denom = Mathf.Max(1f, ty);
            float rowHalf = w * 0.5f * (1f - Mathf.Abs(y - ty) / denom);
            float half = Mathf.Max(0f, rowHalf);
            for (int x = 0; x < w; x++)
                px[y * w + x] = Mathf.Abs(x - w * 0.5f) <= half ? color : Color.clear;
        }
        tex.SetPixels(px);
        tex.Apply();
        // pivot 底面中心：菱形几何中心即"地板中心"（等轴瓦片 pivot 约定）
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>等轴菱体：底部菱形地基 + 上部立方体侧面（左右翼 + 顶面斜线），pivot 底面中心。</summary>
    private static Sprite CreateIsoCubeSprite(int w, int groundH, int wallH, Color color)
    {
        int totalH = groundH + wallH;
        var tex = new Texture2D(w, totalH, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var px = new Color[w * totalH];
        // 立方体占位：模拟等轴立方的可见三个面（左右侧面 + 顶面）
        // 底部菱形顶点范围：-w/2..w/2 在 y=groundH
        float halfW = w * 0.5f;
        Color left = Shade(color, 0.8f);
        Color right = Shade(color, 1.0f);
        Color top = Shade(color, 1.25f);
        for (int y = 0; y < totalH; y++)
        {
            // 底部菱形地基（.5 透明度区分）
            if (y < groundH)
            {
                float ty = (groundH - 1) * 0.5f;
                float rowHalf = w * 0.5f * (1f - Mathf.Abs(y - ty) / Mathf.Max(1f, ty));
                float half = Mathf.Max(0f, rowHalf);
                for (int x = 0; x < w; x++)
                {
                    if (Mathf.Abs(x - w * 0.5f) <= half)
                        px[y * w + x] = new Color(color.r, color.g, color.b, 0.5f);
                }
                continue;
            }
            // 上部墙身：以顶宽 halfW*0.5 收窄模拟立方两侧，中间填色
            int yy = y - groundH;                 // 0..wallH-1
            float t = wallH > 0 ? (float)yy / wallH : 0f;
            // 顶面宽 = halfW，底(墙顶)= sideOffset
            float wallHalfAtY = halfW * 0.5f + (halfW * 0.5f) * t; // 下宽上窄
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Abs(x - halfW);
                if (d <= wallHalfAtY)
                {
                    // 左半→左面，右半→右面，中心→顶面前缘
                    if (d <= wallHalfAtY * 0.3f) px[y * w + x] = top;
                    else if (x < halfW) px[y * w + x] = left;
                    else px[y * w + x] = right;
                }
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        // pivot 底面中心：Texture2D 原点在左下，pivot=(0.5, 0) 表示底部中心（y 方向 0 在图像底部）
        return Sprite.Create(tex, new Rect(0, 0, w, totalH), new Vector2(0.5f, 0f), 100f);
    }

    private static Color Shade(Color c, float k)
    {
        return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
    }
}
}