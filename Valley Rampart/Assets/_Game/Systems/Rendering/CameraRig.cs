using UnityEngine;

/// <summary>
/// 2_10 步骤3 摄像机 rig：俯视正交 pan/zoom/clamp/FocusOn（替换退役 CameraSetup 1D 侧滚）。
///
/// - orthographic：俯视正交，无逻辑 Z（D105），zoom 档位吸附整数倍（R3 防锯齿）。
/// - 边界 clamp：地图（MapData.width/height）经 MapRenderService.GridToIso 得等轴矩形
///   + boundaryMarginCells 外扩；摄像机中心不越界。
/// - FocusOn：事件跳转（守卫告警来自 2_8）；初始 Focus=主城锚点 WorldManager.GetKingdomAnchorWorld
///   （2_12 王座/旗帜锚点契约前的小剧场期替代），1× 视域 24 格。
/// - ScreenToGrid：摄像机拾取 → 逻辑格（验收：误差 0）。
/// - 只控制摄像机，不触碰逻辑坐标（铁律）。
/// </summary>
public class CameraRig : Singleton<CameraRig>
{
    [Header("配置（Resources/Config/CameraConfig.asset 懒加载）")]
    [SerializeField] private CameraConfig config;

    [Header("输入（小剧场自动验收需锁定，防边缘滚屏/滚轮把相机推离主城）")]
    [Tooltip("false 时 Update 内 WASD/边缘滚屏/中键/滚轮全部失效；手动 FocusOn/Pan/Zoom 不受影响，便于验收编排稳定构图")]
    public bool inputEnabled = true;

    private Camera _cam;
    private int _zoomIndex;
    private Rect _mapRectLimit;        // 等轴地图矩形（世界单位，中心坐标原点对齐 GridSystem）
    private bool _mapReady;
    private bool _focusedOnce;

    /// <summary>当前档位下标（2_10 步骤13 染色视口分级轮询读点，D448；只读零行为变更）。</summary>
    public int ZoomIndex => _zoomIndex;

    /// <summary>是否已收到地图与网格可用信号（初始 Focus 主城锚点）。</summary>
    public bool MapReady => _mapReady;

    protected override void Awake()
    {
        base.Awake();
        config = CameraConfig.Instance;
        _cam = GetComponent<Camera>();
        if (_cam == null) Debug.LogError("[CameraRig] 需要挂在 Camera 上");
        _zoomIndex = config != null ? config.defaultZoomIndex : 0;
        if (_cam != null) _cam.orthographic = true;
    }

    private void OnEnable() => EventBus.Subscribe<MapGeneratedEvent>(OnMapGenerated);

    private void OnDisable() => EventBus.Unsubscribe<MapGeneratedEvent>(OnMapGenerated);

    private void Start()
    {
        // 已在地图生成后挂载/场景启动时补建一次边界（防事件早于本组件启用）
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map != null) OnMapGenerated(new MapGeneratedEvent(0, true));
    }

    private void OnMapGenerated(MapGeneratedEvent evt)
    {
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null || map.width <= 0 || map.height <= 0) return;
        BuildMapLimit(map);
        _mapReady = true;
        ApplyZoom();
        FocusHome();
    }

    // ========================================================================
    //  边界
    // ========================================================================

    /// <summary>由地图尺寸经等轴投影得俯视矩形（中心原点坐标，与 GridSystem 一致）。</summary>
    private void BuildMapLimit(MapData map)
    {
        // 四个角点逻辑坐标 → GridToIso 世界坐标（等轴菱形外接矩形）
        Vector2 c00 = MapRenderService.GridToIso(new GridCoord(0, 0));
        Vector2 cW0 = MapRenderService.GridToIso(new GridCoord(map.width - 1, 0));
        Vector2 c0H = MapRenderService.GridToIso(new GridCoord(0, map.height - 1));
        Vector2 cWH = MapRenderService.GridToIso(new GridCoord(map.width - 1, map.height - 1));
        float xMin = Mathf.Min(c00.x, cW0.x, c0H.x, cWH.x);
        float xMax = Mathf.Max(c00.x, cW0.x, c0H.x, cWH.x);
        float yMin = Mathf.Min(c00.y, cW0.y, c0H.y, cWH.y);
        float yMax = Mathf.Max(c00.y, cW0.y, c0H.y, cWH.y);
        _mapRectLimit = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    /// <summary>clamp 摄像机中心到地图矩形内（含边界 margin 扩展）。</summary>
    private void ClampToBounds()
    {
        if (_cam == null || !_mapReady) return;
        Vector2 p = transform.position;
        float margin = config != null ? config.boundaryMarginCells * MapRenderService.DefaultCellSize.x : 0f;
        float halfH = _cam.orthographicSize;
        // 半宽依赖纵横比
        float halfW = halfH * _cam.aspect;
        float xMin = _mapRectLimit.xMin - margin + halfW;
        float xMax = _mapRectLimit.xMax + margin - halfW;
        float yMin = _mapRectLimit.yMin - margin + halfH;
        float yMax = _mapRectLimit.yMax + margin - halfH;
        // 地图足够大则 clamp；局部过小则取中点
        float cx = xMax >= xMin ? Mathf.Clamp(p.x, xMin, xMax) : (_mapRectLimit.xMin + _mapRectLimit.xMax) * 0.5f;
        float cy = yMax >= yMin ? Mathf.Clamp(p.y, yMin, yMax) : (_mapRectLimit.yMin + _mapRectLimit.yMax) * 0.5f;
        transform.position = new Vector3(cx, cy, transform.position.z);
    }

    // ========================================================================
    //  公开操作
    // ========================================================================

    /// <summary>平移（世界单位 delta；边缘滚屏/中键拖拽传入）。</summary>
    public void Pan(Vector2 delta)
    {
        if (_cam == null || !_mapReady) return;
        transform.position += (Vector3)delta;
        ClampToBounds();
    }

    /// <summary>缩放步进（step=±1：升/降一档），档位吸附。</summary>
    public void Zoom(int step)
    {
        if (config == null || config.zoomLevels == null || config.zoomLevels.Length == 0) return;
        _zoomIndex = Mathf.Clamp(_zoomIndex + step, 0, config.zoomLevels.Length - 1);
        ApplyZoom();
        // 缩放改变视域半宽 → 重新 clamp
        ClampToBounds();
    }

    /// <summary>缩放到指定档位下标。</summary>
    public void ZoomTo(int zoomIndex)
    {
        if (config == null) return;
        _zoomIndex = Mathf.Clamp(zoomIndex, 0, Mathf.Max(0, config.zoomLevels.Length - 1));
        ApplyZoom();
        ClampToBounds();
    }

    /// <summary>事件跳转（守卫告警等）。直接定位，不做插值（小剧场优先可读性）。</summary>
    public void FocusOn(Vector2 worldPos)
    {
        if (_cam == null) return;
        transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
        ClampToBounds();
    }

    /// <summary>
    /// 初始 Focus=主城锚点。
    /// 注意：渲染层世界基准 = 等轴 Iso（格(0,0)→世界(0,0)，Tilemap IsometricZAsY 同基准），
    /// 而 WorldManager.GetKingdomAnchorWorld 用 GridSystem.CoordToWorld（中心原点）——两者基准不同。
    /// 故此处用 GridToIso(主城中心格) 换算到 Iso 世界基准，保证与渲染层/Tilemap 对齐（拾取/clamp 同基准）。
    /// </summary>
    private void FocusHome()
    {
        if (_focusedOnce) return;
        _focusedOnce = true;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        Vector2 anchor;
        if (map != null)
            anchor = MapRenderService.GridToIso(new GridCoord(map.width / 2, map.height / 2));
        else
            anchor = Vector2.zero;
        FocusOn(anchor);
        // 初始主城上方（露出城郊）：等轴 isoY 向下增长，向上=减小 y。偏移半屏高，让主城略靠下、上方地形可见。
        if (_cam != null)
        {
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, p.y - _cam.orthographicSize * 0.5f, p.z);
            ClampToBounds();
        }
    }

    private void ApplyZoom()
    {
        if (_cam == null || config == null) return;
        _cam.orthographicSize = config.DefaultOrthoSize / Mathf.Max(0.1f, config.ZoomFactor(_zoomIndex));
    }

    // ========================================================================
    //  拾取
    // ========================================================================

    /// <summary>屏幕坐标 → 等轴世界 → 逻辑格（验收：误差 0）。越界返回 null 由调用方处理。</summary>
    public GridCoord? ScreenToGrid(Vector2 screenPos)
    {
        if (_cam == null) return null;
        Vector3 world = _cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, _cam.nearClipPlane));
        GridCoord cell = MapRenderService.IsoToCell(new Vector2(world.x, world.y));
        var grid = GridSystem.Instance;
        if (grid != null && !grid.IsInBounds(cell)) return null;
        return cell;
    }

    // ========================================================================
    //  帧更新：边缘滚屏
    // ========================================================================

    private void Update()
    {
        if (_cam == null || !_mapReady) return;
        if (config == null) return;
        // 输入锁定（小剧场自动验收/截图）：禁用 WASD/边缘滚屏/中键/滚轮，避免鼠标滞留窗口边缘把相机推离主城
        if (!inputEnabled) return;

        // WASD 键盘平移（世界轴：W/S=上下，A/D=左右；等轴俯视下指屏幕上下/左右）
        Vector2 kbd = Vector2.zero;
        if (Input.GetKey(KeyCode.W)) kbd.y += 1f;
        if (Input.GetKey(KeyCode.S)) kbd.y -= 1f;
        if (Input.GetKey(KeyCode.A)) kbd.x -= 1f;
        if (Input.GetKey(KeyCode.D)) kbd.x += 1f;
        if (kbd != Vector2.zero)
            Pan(kbd.normalized * config.panSpeed * Time.deltaTime);

        // 边缘滚屏（鼠标贴边；键盘平移进行时不叠加边缘，避免跳动）
        // 由 config.enableEdgeScroll 开关（默认关）。将来源设置页勾选后开启，避免"跟随鼠标"错觉。
        if (config.enableEdgeScroll && kbd == Vector2.zero)
        {
            var mouse = Input.mousePosition;
            float w = Screen.width, h = Screen.height;
            float edge = config.edgeScrollWidth;
            float speed = config.panSpeed * config.edgeScrollScale;
            Vector2 delta = Vector2.zero;
            if (mouse.x < edge) delta.x -= speed;
            else if (mouse.x > w - edge) delta.x += speed;
            if (mouse.y < edge) delta.y -= speed;
            else if (mouse.y > h - edge) delta.y += speed;
            if (delta != Vector2.zero) Pan(delta * Time.deltaTime);
        }

        // 中键拖拽平移
        if (Input.GetMouseButton(2))
        {
            float axisX = Input.GetAxis("Mouse X");
            float axisY = Input.GetAxis("Mouse Y");
            if (Mathf.Abs(axisX) > 0.001f || Mathf.Abs(axisY) > 0.001f)
            {
                Vector2 drag = new Vector2(-axisX, -axisY) * config.panSpeed * Time.deltaTime;
                Pan(drag);
            }
        }

        // 滚轮缩放（档位吸附）
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0.001f) Zoom(1);
        else if (scroll < -0.001f) Zoom(-1);
    }
}