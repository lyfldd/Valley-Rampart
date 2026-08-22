using UnityEngine;

/// <summary>
/// 建造系统全局配置（2_2 §三 SO 配置）。预览高亮色 + 桥段上限。
/// 资产：Resources/Config/BuildConfig.asset。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/BuildConfig", fileName = "BuildConfig")]
public class BuildConfig : ScriptableObject
{
    [Header("预览高亮（2_2 步骤9）")]
    [Tooltip("ghost 可放置时的颜色")]
    public Color previewColorOk = new Color(0f, 1f, 0f, 0.5f);
    [Tooltip("ghost 不可放置时的颜色")]
    public Color previewColorBad = new Color(1f, 0f, 0f, 0.5f);

    [Header("桥（2_2 §3.5）")]
    [Tooltip("单条桥链最长段数（防无限拼桥，占位可调）")]
    public int bridgeMaxSegments = 8;

    [Header("协作施工（2_12 步骤4 / HH.9 裁决 C+）")]
    [Tooltip("建筑基础施工时长（秒）。替代 Building.constructDuration 硬编码 5f；升级/修复也按此基础时长")]
    public float constructionBaseSeconds = 5f;
    [Tooltip("协作施工加成系数 k：实际施工时长 = base / (1 + (n-1)×k)，n=该建筑实际被派工人数。k=0 退化为纯计时")]
    [Range(0f, 1f)] public float cooperativeBuildK = 0.25f;
}
