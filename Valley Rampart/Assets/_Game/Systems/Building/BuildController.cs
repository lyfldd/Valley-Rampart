using UnityEngine;

/// <summary>
/// 建造控制器（3.3 第四节）。管理建造模式、ghost 预览、放置校验、放置执行。
/// Singleton，由 UI 建造按钮调 EnterBuildMode(def) 进入，ESC 或放置后退出。
/// </summary>
public class BuildController : Singleton<BuildController>
{
    private BuildingDef _selectedDef;
    private GameObject _ghost;
    private SpriteRenderer _ghostRenderer;
    private bool _inBuildMode;

    // 主城解锁/等级（3.3.4 批次7）
    private bool _buildUnlocked = false;
    private int _castleLevel = 0;
    // 多格占地 + R 键旋转（3.3.4 批次8）
    private Vector2Int _footprint = Vector2Int.one;
    private bool _rotated = false;

    public bool IsInBuildMode => _inBuildMode;
    public int CastleLevel => _castleLevel;        // 3.3.4 批次7
    public bool IsBuildUnlocked => _buildUnlocked; // 3.3.4 批次7

    // ===== Unity 生命周期（3.3.4 批次7）=====

    private void OnEnable()
    {
        EventBus.Subscribe<BuildingActivatedEvent>(OnBuildingActivated);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BuildingActivatedEvent>(OnBuildingActivated);
    }

    private void OnBuildingActivated(BuildingActivatedEvent evt)
    {
        if (evt.Building != null && evt.Building.sourceType == BuildingType.CastleCore)
        {
            _buildUnlocked = true;
            _castleLevel = evt.Building.level;
            Debug.Log($"[BuildController] 主城激活，解锁建造菜单，主城等级={_castleLevel}");
        }
    }

    /// <summary>进入建造模式（选中某 BuildingDef）。由建造菜单按钮调。</summary>
    public void EnterBuildMode(BuildingDef def)
    {
        if (def == null) return;

        // 建造解锁校验（3.3.4 批次7）
        if (!_buildUnlocked) { Debug.Log("[BuildController] 建造未解锁（需先修复主城）"); return; }

        // 已在建造模式则先退出
        if (_inBuildMode) ExitBuildMode();

        _selectedDef = def;
        _footprint = def.footprint;  // 多格占地（3.3.4 批次8）
        _rotated = false;            // 旋转重置（3.3.4 批次8）
        _inBuildMode = true;
        InputManager.Instance?.SetMode(InputMode.Build);

        // 创建 ghost（半透明预览）
        CreateGhost(def);
        Debug.Log($"[BuildController] 进入建造模式: {def.id}");
    }

    /// <summary>退出建造模式。</summary>
    public void ExitBuildMode()
    {
        if (!_inBuildMode) return;
        _selectedDef = null;
        _inBuildMode = false;
        if (_ghost != null) Destroy(_ghost);
        _ghost = null;
        _ghostRenderer = null;
        InputManager.Instance?.SetMode(InputMode.Normal);
        Debug.Log("[BuildController] 退出建造模式");
    }

    void Update()
    {
        if (!_inBuildMode || _selectedDef == null) return;

        // R 键旋转 ghost（3.3.4 批次8）
        if (Input.GetKeyDown(KeyCode.R))
        {
            _rotated = !_rotated;
            // 旋转：w/h 互换
            int w = _footprint.y > 0 ? _footprint.y : 1;
            int h = _footprint.x > 0 ? _footprint.x : 1;
            _footprint = new Vector2Int(w, h);
            // 重建 ghost
            if (_ghost != null) { Destroy(_ghost); _ghost = null; }
            CreateGhost(_selectedDef);
        }

        // ghost 跟随鼠标 + 吸附区块中心 + 绿/红反馈
        if (_ghost != null && Camera.main != null && GridSystem.Instance != null)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var coord = GridSystem.Instance.WorldToCoord(mouseWorld);
            Vector3 snapPos = GridSystem.Instance.CoordToWorld(coord);
            _ghost.transform.position = snapPos;

            // 绿/红反馈
            bool valid = PlacementValidator.Validate(_selectedDef, coord);
            if (_ghostRenderer != null)
                _ghostRenderer.color = valid ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);
        }

        // 左键放置
        if (Input.GetMouseButtonDown(0))
        {
            TryPlace();
        }

        // 右键退出（ESC 由 UIManager 栈统一处理：关栈顶 BuildModeEntry）
        if (Input.GetMouseButtonDown(1))
        {
            UIManager.Instance?.Pop();
        }
    }

    void TryPlace()
    {
        if (_selectedDef == null || Camera.main == null || GridSystem.Instance == null) return;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var coord = GridSystem.Instance.WorldToCoord(mouseWorld);

        // 校验用 def.footprint（非旋转后的 _footprint）；多格旋转校验留待 footprint 不可变改造（3.3.4 批次8）
        if (!PlacementValidator.Validate(_selectedDef, coord))
        {
            Debug.Log("[BuildController] 放置校验失败");
            return;
        }

        // 扣资源
        if (RulerController.Instance != null)
        {
            if (!RulerController.Instance.CanAfford(_selectedDef.cost)) return;
            RulerController.Instance.Spend(_selectedDef.cost);
        }

        // 放置即改造：工具建筑建在资源点上，销毁原资源点（3.3.4 批次6）
        if (ResourceNodeMapping.RequiresResourceNode(_selectedDef.id) && BuildingRegistry.Instance != null)
        {
            var node = BuildingRegistry.Instance.GetAt(coord);
            if (node != null && node.def != null && node.def.isResourceNode)
            {
                node.Die();  // 销毁资源点（FreeFootprint + Unregister + Destroy）
            }
        }

        // 实例化 Building
        Vector3 worldPos = GridSystem.Instance.CoordToWorld(coord);
        GameObject go;
        if (_selectedDef.prefab != null)
        {
            go = Instantiate(_selectedDef.prefab, worldPos, Quaternion.identity);
        }
        else
        {
            // 无 prefab 时创建空壳 + 占位视觉（3.3.4 问题12）
            go = new GameObject($"Building_{_selectedDef.id}_{coord.x}");
            go.transform.position = worldPos;
            BuildingVisual.ApplyPlaceholder(go, BuildingType.None, _selectedDef.role);
        }

        var b = go.GetComponent<Building>();
        if (b == null) b = go.AddComponent<Building>();
        b.Init(_selectedDef, coord, true);
        b.StartConstructing();  // 玩家建造走 Constructing 进度（3.3.4 批次3）

        // 确保 Collider2D（size 局部 1x1，由 localScale 统一缩放，3.3.4 修复误触+碰撞盒）
        if (go.GetComponent<Collider2D>() == null)
        {
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
        }

        GridSystem.Instance.MarkOccupiedFootprint(coord, b.cellWidth, b);
        BuildingRegistry.Instance?.Register(b);
        BuildingFactory.AttachComponents(b, _selectedDef);  // 玩家建造也挂组件（3.3.4 批次5）
        EventBus.Publish(new BuildingPlacedEvent(b));

        Debug.Log($"[BuildController] 放置 {_selectedDef.id} at cell {coord.x}");

        // Shift 连放，否则退出（走栈 Pop，触发 BuildModeEntry.Close -> ExitBuildMode + 回菜单）
        if (!Input.GetKey(KeyCode.LeftShift))
        {
            if (UIManager.Instance != null) UIManager.Instance.Pop();
            else ExitBuildMode(); // 兜底
        }
    }

    void CreateGhost(BuildingDef def)
    {
        if (def.prefab != null)
        {
            _ghost = Instantiate(def.prefab);
            _ghostRenderer = _ghost.GetComponentInChildren<SpriteRenderer>();
        }
        else
        {
            // 无 prefab 时用占位视觉（3.3.4 问题12）
            _ghost = new GameObject("BuildGhost");
            _ghostRenderer = BuildingVisual.ApplyPlaceholder(_ghost, BuildingType.None, def.role);
            // 多格占地视觉（3.3.4 批次8，用 _footprint 支持旋转）
            int w = _footprint.x > 0 ? _footprint.x : 1;
            int h = _footprint.y > 0 ? _footprint.y : 1;
            _ghost.transform.localScale = new Vector3(w, h, 1);
        }

        if (_ghostRenderer != null)
            _ghostRenderer.color = new Color(0, 1, 0, 0.5f);
    }
}
