using UnityEngine;

/// <summary>
/// 摄像机全局配置（2_10 步骤3 CameraRig 调参入口，SO 数据驱动）。
/// 资产：Resources/Config/CameraConfig.asset。统一按世界单位/格换算，禁用魔法数字。
///
/// 视域约定（策划拍板 D19）：1× 档横向视域约 24 格 → 世界宽 ≈ 24 × 1.28 = 30.72，纵向约 13 菱形行。
/// orthographicSize = (视域世界高)/2，视觉高受 Screen 纵横比影响，初始以 1× 横向 24 格为准调。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/CameraConfig", fileName = "CameraConfig")]
public class CameraConfig : ScriptableObject
{
    private static CameraConfig _instance;

    /// <summary>懒加载（缺资产用类默认值兜底，需在 Resources/Config 放资产）。</summary>
    public static CameraConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<CameraConfig>("Config/CameraConfig");
            return _instance;
        }
    }

    [Header("缩放（档位吸附 / R3 防锯齿摩尔纹）")]
    [Tooltip("zoom 档位（整数倍，1×=默认视域）。档位吸附，不连续缩放")]
    public float[] zoomLevels = new float[] { 1f, 2f, 4f };

    [Tooltip("默认档位（zoomLevels 下标，初始=1×）")]
    public int defaultZoomIndex = 0;

    [Header("移动")]
    [Tooltip("键盘/中键拖拽平移速度（世界单位/秒，1× 档基准）")]
    public float panSpeed = 20f;

    [Tooltip("边缘滚屏总开关。默认关闭（体验上易形成\"跟随鼠标\"错觉）；将来在设置页勾选开启")]
    public bool enableEdgeScroll = false;

    [Tooltip("边缘滚屏区宽度（屏幕像素）")]
    public float edgeScrollWidth = 16f;

    [Tooltip("边缘滚屏速度系数（×panSpeed）")]
    public float edgeScrollScale = 0.6f;

    [Header("视域与边界")]
    [Tooltip("1× 档视域横向宽（格数，策划拍板 24）")]
    public int viewWidthCells = 24;

    [Tooltip("边界外扩（格数），clamp margin 起点")]
    public int boundaryMarginCells = 2;

    [Tooltip("初始 Focus 主城锚点时该档位（= defaultZoomIndex，预留）")]
    public int homeFocusZoomIndex = 0;

    /// <summary>1× 档对应 orthographicSize（世界高一半）。按横向 24 格约占满屏宽反推。</summary>
    public float DefaultOrthoSize
    {
        get
        {
            // 等轴下 1 格横轴世界宽 = cellSize.x；24 格 ≈ 24×cellSize.x 世界宽。
            // 以默认 GameView 纵横比(约 16:9) 估竖宽 ≈ 横向 × 9/16 → orthoSize = 该高/2。
            float worldW = viewWidthCells * MapRenderService.DefaultCellSize.x;
            float worldH = worldW * 9f / 16f;
            return worldH * 0.5f;
        }
    }

    /// <summary>指定档位缩放因子（基于默认档缩放）。</summary>
    public float ZoomFactor(int zoomIndex)
    {
        if (zoomLevels == null || zoomLevels.Length == 0) return 1f;
        int i = Mathf.Clamp(zoomIndex, 0, zoomLevels.Length - 1);
        return zoomLevels[i];
    }
}