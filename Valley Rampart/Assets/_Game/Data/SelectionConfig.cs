using UnityEngine;

/// <summary>
/// 上帝视角选择配置（2_13 步骤3 / §5.5 SelectionConfig，D414）。
/// 数值双落：.cs 默认值 + asset 序列化值（so-data-driven 铁律）；Play 用 Resources.Load 同路径。
/// 资产路由：_Game/Resources/Config/SelectionConfig.asset（对齐既有 Config 布局）。
/// 消费方：SelectionController（dragThresholdPx 框选阈值 / onlyFriendly 框选仅收己方 / highlightColor 选中高亮色）。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/Config/SelectionConfig", fileName = "SelectionConfig")]
public class SelectionConfig : ScriptableObject
{
    [Header("框选/点选判定")]
    [Tooltip("拖拽距离（像素）< 此值判点选，≥ 判框选（实施计划 §5.5 默认 5px）")]
    public int dragThresholdPx = 5;

    [Tooltip("框选仅收己方（玩家王 kingdomId==0；2_16 步骤7 补丁2 现行，2026-08-29 裁决修订 D414）")]
    public bool onlyFriendly = true;

    [Header("选中高亮")]
    [Tooltip("选中单位高亮色（SpriteRenderer.color 覆盖）")]
    public Color highlightColor = Color.yellow;

    /// <summary>Resources.Load 统一入口（失败回退默认值=数值双落验证口径）。</summary>
    public static SelectionConfig Load()
    {
        var c = Resources.Load<SelectionConfig>("Config/SelectionConfig");
        return c != null ? c : CreateInstance<SelectionConfig>();
    }
}