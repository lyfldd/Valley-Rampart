using UnityEngine;

/// <summary>
/// 2D 摄像机跟随。挂在 Main Camera 上。
/// 仅跟随玩家 X 轴移动，Y 轴固定为背景图中心 Y。
/// 视口高度 = 背景图高度；X 可视范围 = 背景图宽度 * 0.7。
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraSetup : MonoBehaviour
{
    [Header("跟随设置")]
    [Tooltip("跟随平滑度，值越大跟随越快（0=不跟随，1=瞬移）")]
    [SerializeField] private float followSpeed = 5f;

    [Tooltip("摄像机 Z 轴位置（相对背景）")]
    [SerializeField] private float cameraZ = -10f;

    [Header("背景图")]
    [Tooltip("背景图的 SpriteRenderer，用于获取中心点与长宽大小")]
    public SpriteRenderer backgroundSprite;

    [Tooltip("摄像机 X 可视范围占背景图宽度的比例")]
    [SerializeField] private float widthRatio = 0.7f;

    private Camera _camera;
    private Transform _target;

    /// <summary>QQQ.1 需求6（方案2）：背景是否已按 originX 一次性对齐到城堡 x=0。</summary>
    private bool _bgAligned;
    /// <summary>已应用的 originX（用于跨地图重生成时增量对齐背景）。</summary>
    private float _bgOriginX;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void Start()
    {
        ApplyCameraSize();
    }

    /// <summary>
    /// 根据背景图高度设置正交摄像机尺寸（视口高度 = 背景图高度）。
    /// </summary>
    private void ApplyCameraSize()
    {
        if (_camera == null || backgroundSprite == null) return;

        _camera.orthographic = true;
        // orthographicSize 为视口高度的一半
        _camera.orthographicSize = GetBackgroundHeight() * 0.5f;
    }

    /// <summary>
    /// 获取背景图的世界坐标中心点。
    /// 若未赋值背景图则返回 Vector3.zero。
    /// </summary>
    public Vector3 GetBackgroundCenter()
    {
        if (backgroundSprite == null) return Vector3.zero;
        return backgroundSprite.bounds.center;
    }

    /// <summary>
    /// 获取背景图在世界空间中的长宽大小（X = 宽，Y = 高）。
    /// 若未赋值背景图则返回 Vector2.zero。
    /// </summary>
    public Vector2 GetBackgroundSize()
    {
        if (backgroundSprite == null) return Vector2.zero;
        var bounds = backgroundSprite.bounds;
        return new Vector2(bounds.size.x, bounds.size.y);
    }

    /// <summary>
    /// 获取背景图在世界空间中的宽度。
    /// </summary>
    public float GetBackgroundWidth()
    {
        return GetBackgroundSize().x;
    }

    /// <summary>
    /// 获取背景图在世界空间中的高度。
    /// </summary>
    public float GetBackgroundHeight()
    {
        return GetBackgroundSize().y;
    }

    /// <summary>
    /// QQQ.1 需求6（方案2）：把背景 Sprite 左移 originX 与偏移后的世界对齐。
    /// 背景初始位置匹配旧坐标系（城堡在旧 x≈originX），整体左移 originX 后城堡中心落 x=0。
    /// 跨地图重生成（originX 变化）时增量对齐。
    /// </summary>
    void AlignBackgroundToMap()
    {
        // doc 1：originX 已删除，地图中心 = 世界原点，背景无需偏移对齐（重写归 2_10）
        _bgAligned = true;
        _bgOriginX = 0f;
    }

    private void LateUpdate()
    {
        // QQQ.1 需求6（方案2）：地图生成后把背景一次性左移 originX，使城堡中心落 x=0（与建筑/出生点一致）
        AlignBackgroundToMap();

        // 每帧检查目标是否有效（君主可能刚创建或已死亡）
        if (RulerController.Instance == null) return;

        var monarch = RulerController.Instance.MonarchUnit;
        if (monarch == null) return;

        _target = monarch.transform;

        Vector3 bgCenter = GetBackgroundCenter();

        // X：跟随玩家，并限制在背景宽度 * widthRatio 的可视范围内
        float targetX = _target.position.x;
        if (backgroundSprite != null)
        {
            float halfRangeX = GetBackgroundWidth() * widthRatio * 0.5f;
            targetX = Mathf.Clamp(targetX, bgCenter.x - halfRangeX, bgCenter.x + halfRangeX);
        }

        // Y：固定为背景图中心 Y
        float targetY = bgCenter.y;

        Vector3 targetPos = new Vector3(targetX, targetY, cameraZ);

        // 仅对 X 做平滑插值，Y/Z 直接锁定
        Vector3 pos = transform.position;
        pos.x = Mathf.Lerp(pos.x, targetPos.x, followSpeed * Time.deltaTime);
        pos.y = targetPos.y;
        pos.z = targetPos.z;
        transform.position = pos;
    }
}
