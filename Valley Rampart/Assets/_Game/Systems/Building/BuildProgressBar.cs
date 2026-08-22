using UnityEngine;

/// <summary>
/// 建筑头顶施工进度条（2_12 步骤7B / D117，D256 终审更新，原 2_13 F8 移交）。
/// 挂在 Building 下，Constructing/Ruined/Upgrading 态显示，Active/其他态隐藏。
/// 视觉：世界空间 SpriteRenderer 双色条（底槽灰 + 填充绿），填充条 localScale.x 随进度从左往右增长。
/// 数据源：宿主 Building.constructProgress（0~1）。渲染归 2_10 替换（当前 SpriteFactory 纯色占位）。
/// 本组件懒创建子物体，SetProgress 每帧驱动显隐与进度；位置由宿主 Building 定位到足迹顶部。
/// </summary>
public class BuildProgressBar : MonoBehaviour
{
    /// <summary>宿主建筑（用于进度数据）。</summary>
    public Building building;

    /// <summary>进度条世界宽度。由宿主按 footprint × cellSize 设置后锁定。</summary>
    public float barWidth = 2.0f;

    /// <summary>进度条世界高度。由宿主设置后锁定。</summary>
    public float barHeight = 0.18f;

    private SpriteRenderer _bg;
    private SpriteRenderer _fill;
    private Transform _bgT;
    private Transform _fillT;
    private bool _init;

    private static Sprite _bgSprite;
    private static Sprite _fillSprite;

    /// <summary>初始化进度条尺寸（宿主首次调用传入世界宽高），幂等。</summary>
    public void Init(float worldWidth, float worldHeight)
    {
        barWidth = Mathf.Max(0.1f, worldWidth);
        barHeight = Mathf.Max(0.05f, worldHeight);
        EnsureCreated();
    }

    /// <summary>每帧驱动：progress∈[0,1]，visible=需要显示的态。位置由宿主在调用前摆好。</summary>
    public void SetProgress(float progress, bool visible)
    {
        EnsureCreated();
        bool show = visible && progress > 0.0001f;
        if (_bg != null) _bgT.gameObject.SetActive(show);
        if (_bg != null) _bg.gameObject.SetActive(show);
        if (_fill != null) _fill.gameObject.SetActive(show);
        if (!show) return;

        float p = Mathf.Clamp01(progress);
        _fillT.localScale = new Vector3(barWidth * p, barHeight, 1f);
        // 左端固定：中心缩放需把填充条 x 左移(1-p)/2 个全宽
        _fillT.localPosition = new Vector3(-(barWidth * (1f - p)) * 0.5f, 0f, 0f);
    }

    private void EnsureCreated()
    {
        if (_init) return;

        // 底槽
        var bgGo = new GameObject("ProgressBar_BG");
        bgGo.transform.SetParent(transform, false);
        _bg = bgGo.AddComponent<SpriteRenderer>();
        if (_bgSprite == null) _bgSprite = SpriteFactory.CreateSquare(8, new Color(0.15f, 0.15f, 0.15f, 0.9f));
        _bg.sprite = _bgSprite;
        _bg.sortingOrder = 10;
        _bgT = bgGo.transform;
        _bgT.localScale = new Vector3(barWidth, barHeight, 1f);

        // 填充
        var fillGo = new GameObject("ProgressBar_Fill");
        fillGo.transform.SetParent(transform, false);
        _fill = fillGo.AddComponent<SpriteRenderer>();
        if (_fillSprite == null) _fillSprite = SpriteFactory.CreateSquare(8, new Color(0.35f, 0.85f, 0.35f, 0.95f));
        _fill.sprite = _fillSprite;
        _fill.sortingOrder = 11;
        _fillT = fillGo.transform;

        _init = true;
    }
}