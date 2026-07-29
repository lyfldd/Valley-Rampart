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

    public bool IsInBuildMode => _inBuildMode;

    /// <summary>进入建造模式（选中某 BuildingDef）。由建造菜单按钮调。</summary>
    public void EnterBuildMode(BuildingDef def)
    {
        if (def == null) return;

        // 已在建造模式则先退出
        if (_inBuildMode) ExitBuildMode();

        _selectedDef = def;
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

        // ESC / 右键退出
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            ExitBuildMode();
        }
    }

    void TryPlace()
    {
        if (_selectedDef == null || Camera.main == null || GridSystem.Instance == null) return;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var coord = GridSystem.Instance.WorldToCoord(mouseWorld);

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

        // 实例化 Building
        Vector3 worldPos = GridSystem.Instance.CoordToWorld(coord);
        GameObject go = _selectedDef.prefab != null
            ? Instantiate(_selectedDef.prefab, worldPos, Quaternion.identity)
            : new GameObject($"Building_{_selectedDef.id}_{coord.x}");
        go.transform.position = worldPos;

        var b = go.GetComponent<Building>();
        if (b == null) b = go.AddComponent<Building>();
        b.Init(_selectedDef, coord, true);

        // 确保 Collider2D
        if (go.GetComponent<Collider2D>() == null)
        {
            float cs = GridSystem.Instance.Config.cellSize;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(cs * b.cellWidth, cs);
        }

        GridSystem.Instance.MarkOccupiedFootprint(coord, b.cellWidth, b);
        BuildingRegistry.Instance?.Register(b);
        EventBus.Publish(new BuildingPlacedEvent(b));

        Debug.Log($"[BuildController] 放置 {_selectedDef.id} at cell {coord.x}");

        // Shift 连放，否则退出
        if (!Input.GetKey(KeyCode.LeftShift))
            ExitBuildMode();
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
            // 无 prefab 时创建半透明方块作 ghost
            _ghost = new GameObject("BuildGhost");
            float cs = GridSystem.Instance != null ? GridSystem.Instance.Config.cellSize : 32f;
            int w = def.footprint.x > 0 ? def.footprint.x : 1;
            _ghost.transform.localScale = new Vector3(w, 1, 1);
            _ghostRenderer = _ghost.AddComponent<SpriteRenderer>();
        }

        if (_ghostRenderer != null)
            _ghostRenderer.color = new Color(0, 1, 0, 0.5f);
    }
}
