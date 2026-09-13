using UnityEngine;

namespace ValleyRampart.Rendering
{
/// <summary>
/// SpriteAnimator 播放器配置（HH.264 A1 · 规格 §三 SO；承 `so-data-driven`——**硬编码值一律迁入本 SO**）。
///
/// 资产：<c>Resources/Config/Art/SpriteAnimatorConfig.asset</c>（由 <c>Editor/ArtImportPipeline</c> 生成，禁手改代码常量）。
/// 缺资产 ⇒ <see cref="Instance"/> 返回内存默认实例（出厂值＝规格 §三 表，缺资产不崩）。
/// </summary>
public class SpriteAnimatorConfig : ScriptableObject
{
    public const string ResourcePath = "Config/Art/SpriteAnimatorConfig";

    /// <summary>动画状态数（与 <c>SpriteAnimator</c> 状态常量一致：Idle/Walk/Attack/Run/Loot/Death）。</summary>
    public const int StateCount = 6;

    [Header("帧率（批5 规格 D259/D568）")]
    [Tooltip("默认 fps（无 per-state 覆写时）")]
    public float defaultFps = 12f;
    [Tooltip("per-state fps（Idle/Walk/Attack/Run/Loot/Death）")]
    public float[] fpsByState = { 12f, 12f, 12f, 12f, 12f, 12f };

    [Header("变速三轴（F-06）")]
    [Tooltip("attack 局部变速（对齐战斗 CD；1=不缩放）")]
    public float attackSpeedScale = 1f;
    [Tooltip("非坐骑 run＝walk 加速（D568「run 并 walk 代码加速」）")]
    public float runAsWalkSpeedScale = 1.5f;

    [Header("LOD 分层（F-13 · 对接既有 LodLevel，禁另建 LOD 系统）")]
    [Tooltip("fps 缩放：Active / SemiActive / Dormant（0 = 冻结静态帧）")]
    public float[] lodFpsScale = { 1.0f, 0.5f, 0f };
    [Tooltip("LOD 档位刷新间隔（秒）——禁逐帧逐单位查 Dictionary（M1）")]
    public float lodRefreshInterval = 0.5f;

    [Header("缺状态回退链（F-09）")]
    [Tooltip("per-state 回退态索引（Idle/Walk/Attack/Run/Loot/Death）；-1 = 无回退（走静态立绘→占位）")]
    public int[] stateFallback = { 0, 0, 0, 1, 0, -1 };

    [Header("playerMode（F-02）")]
    [Tooltip("per-state 播放模式：0=Loop 1=OnceReturn 2=OnceHold")]
    public int[] stateMode = { 0, 0, 1, 0, 2, 2 };
    [Tooltip("工人 Working 期 attack＝工作循环（不回落）；战斗 attack＝OnceReturn（F-02 三态分离）")]
    public bool workAttackLoop = true;

    private static SpriteAnimatorConfig _instance;
    private static bool _loadAttempted;

    /// <summary>单例（Resources 懒加载；缺资产 → 内存默认实例，字段＝规格出厂值）。</summary>
    public static SpriteAnimatorConfig Instance
    {
        get
        {
            if (!_loadAttempted)
            {
                _loadAttempted = true;
                _instance = Resources.Load<SpriteAnimatorConfig>(ResourcePath);
                if (_instance == null)
                {
                    _instance = CreateInstance<SpriteAnimatorConfig>();
                    Debug.Log("[SpriteAnimatorConfig] 未找到 Resources/" + ResourcePath
                              + " ⇒ 用内存出厂默认（H5/H6 批应随导入流水线落盘资产）");
                }
            }
            return _instance;
        }
    }

    /// <summary>清静态缓存（编辑器重建资产后调用；运行时勿用）。</summary>
    public static void ClearCache()
    {
        _loadAttempted = false;
        _instance = null;
    }

    /// <summary>per-state fps（越界/数组缺失回退 defaultFps）。</summary>
    public float FpsOf(int state)
        => (fpsByState != null && state >= 0 && state < fpsByState.Length && fpsByState[state] > 0f)
            ? fpsByState[state] : defaultFps;

    /// <summary>per-state 播放模式（0=Loop 1=OnceReturn 2=OnceHold）。</summary>
    public int ModeOf(int state)
        => (stateMode != null && state >= 0 && state < stateMode.Length) ? stateMode[state] : 0;

    /// <summary>per-state 回退态（-1 = 无回退）。</summary>
    public int FallbackOf(int state)
        => (stateFallback != null && state >= 0 && state < stateFallback.Length) ? stateFallback[state] : -1;

    /// <summary>LodLevel（0=Active 1=SemiActive 2=Dormant）→ fps 缩放。</summary>
    public float LodScaleOf(int lodTier)
        => (lodFpsScale != null && lodTier >= 0 && lodTier < lodFpsScale.Length) ? lodFpsScale[lodTier] : 1f;
}
}
