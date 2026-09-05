using System;
using UnityEngine;

/// <summary>
/// 2_10 步骤13 领土染色 SO 配置（D443+D448~D452，so-data-driven 禁魔法数）。
/// zoomLods 对齐 CameraConfig.zoomLevels 下标（加档扩 SO 表，下标超出取末档，D448）。
/// </summary>
[CreateAssetMenu(menuName = "Valley/Territory Overlay Config", fileName = "TerritoryOverlayConfig")]
public class TerritoryOverlayConfig : ScriptableObject
{
    /// <summary>视口分级 per 档 {内部 mid alpha, 边界 mid alpha}（D448/D450：近景 0 全无色，边界恒高于同档内部）。</summary>
    [Serializable]
    public struct ZoomLod
    {
        public float interiorAlpha;
        public float boundaryAlpha;

        public ZoomLod(float interior, float boundary)
        {
            interiorAlpha = interior;
            boundaryAlpha = boundary;
        }
    }

    [Tooltip("视口分级表（下标对齐 CameraConfig.zoomLevels；默认 [0]={0,0} 近景全无色 / [1]={0.35,0.50} 中景 / [2]={0.55,0.65} 远景）")]
    public ZoomLod[] zoomLods =
    {
        new ZoomLod(0f, 0f),
        new ZoomLod(0.35f, 0.50f),
        new ZoomLod(0.55f, 0.65f),
    };

    [Tooltip("跨档 alpha 过渡时长秒（D451）")]
    public float fadeDurationSeconds = 0.3f;

    [Tooltip("灭国渐隐时长秒（D379 定值，D446）")]
    public float kingdomFadeDurationSeconds = 2.0f;

    [Tooltip("染色基色派生：饱和度倍率（D447，旗色数据零污染）")]
    [Range(0f, 4f)] public float colorSaturation = 1.0f;

    [Tooltip("染色基色派生：亮度倍率（D447）")]
    [Range(0f, 4f)] public float colorBrightness = 1.1f;

    [Tooltip("染色总开关初始值（2_13 设置页可消费）")]
    public bool enableOnStart = true;

    /// <summary>按档位下标取分级（下标超出/缺表取末档；D448 加档扩 SO 表）。</summary>
    public ZoomLod GetLod(int zoomIndex)
    {
        if (zoomLods == null || zoomLods.Length == 0) return new ZoomLod(0f, 0f);
        return zoomLods[Mathf.Clamp(zoomIndex, 0, zoomLods.Length - 1)];
    }

    /// <summary>中景档（HighlightKingdom 临时显色浓度，D452）。缺表回退设计默认 {0.35,0.50}。</summary>
    public ZoomLod MidLod
    {
        get
        {
            if (zoomLods != null && zoomLods.Length > 1) return zoomLods[1];
            return new ZoomLod(0.35f, 0.50f);
        }
    }

    /// <summary>载入配置（缺 asset 回退默认占位实例；KingdomBrain.LoadConfig 同款惯例）。</summary>
    public static TerritoryOverlayConfig Load()
    {
        var cfg = Resources.Load<TerritoryOverlayConfig>("Config/TerritoryOverlayConfig");
        return cfg != null ? cfg : CreateInstance<TerritoryOverlayConfig>();
    }
}
