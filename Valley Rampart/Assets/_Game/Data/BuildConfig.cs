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
}
