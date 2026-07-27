using UnityEngine;

/// <summary>
/// Sprite 生成工具。用代码生成纯色 sprite，不依赖美术资源。
/// 美术到位后，改 PlaceholderSprites.Get 改为 Resources.Load 即可全替换。
/// </summary>
public static class SpriteFactory
{
    /// <summary>生成纯色方块 sprite。</summary>
    public static Sprite CreateSquare(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), pixelsPerUnit: size);
    }

    /// <summary>生成纯色圆形 sprite。</summary>
    public static Sprite CreateCircle(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        float center = size / 2f;
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                pixels[y * size + x] = dist <= radius ? color : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), pixelsPerUnit: size);
    }

    /// <summary>生成向下尖的三角 sprite（用于怪物/陷阱）。</summary>
    public static Sprite CreateTriangle(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            int halfWidth = (int)((size - y) / 2f);
            for (int x = 0; x < size; x++)
            {
                bool inTriangle = x >= halfWidth && x < size - halfWidth;
                pixels[y * size + x] = inTriangle ? color : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), pixelsPerUnit: size);
    }

    /// <summary>生成细长方块（用于箭塔）。</summary>
    public static Sprite CreateRect(int width, int height, Color color)
    {
        Texture2D tex = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f), pixelsPerUnit: Mathf.Max(width, height));
    }
}
