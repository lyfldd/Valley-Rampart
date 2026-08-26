using UnityEngine;

/// <summary>
/// 建造控制器（3.3 第四节 + 2_2 步骤3：2D 微格吸附 + 朝向旋转 + 绿/红预览）。
/// 管理建造模式、ghost 预览、放置校验、放置执行。
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
    // 多格占地 + 旋转（3.3.4 批次8 + 2_2：rotatable 才可转，城门/桥）
    private Vector2Int _footprint = Vector2Int.one;
    private GateOrientation _playerOrientation = GateOrientation.Horizontal; // 玩家 R 键选择（仅自由段生效）

    public bool IsInBuildMode => _inBuildMode;
    public int CastleLevel => _castleLevel;        // 3.3.4 批次7
    /// <summary>
    /// 建造是否解锁（QQQ.3 B8-1 / LC-B1 修复：从 KingdomManager.CastleLevel 派生，不依赖 BuildingActivatedEvent）。
    /// 修复点：读档重建只发 BuildingPlacedEvent 不发 BuildingActivatedEvent ⇒ 主城已修复但 _buildUnlocked 仍 false ⇒ 建造菜单永久软锁。
    /// 改为派生后，读档恢复 CastleLevel≥1 即解锁，彻底解耦。
    /// </summary>
    public bool IsBuildUnlocked
    {
        get
        {
            if (_buildUnlocked) return true;   // 事件已解锁（新局/即时），快速返回
            return KingdomManager.Instance != null && KingdomManager.Instance.CastleLevel >= 1;
        }
    }

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
        if (!IsBuildUnlocked) { Debug.Log("[BuildController] 建造未解锁（需先修复主城）"); return; }

        // 已在建造模式则先退出
        if (_inBuildMode) ExitBuildMode();

        _selectedDef = def;
        _footprint = new Vector2Int(
            def.footprint.x > 0 ? def.footprint.x : 1,
            def.footprint.y > 0 ? def.footprint.y : 1);
        _playerOrientation = GateOrientation.Horizontal;   // 旋转重置
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

        // R 键旋转 ghost（2_2：仅 rotatable 可转，城门/桥）
        if (Input.GetKeyDown(KeyCode.R) && _selectedDef.rotatable)
        {
            _playerOrientation = _playerOrientation == GateOrientation.Horizontal
                ? GateOrientation.Vertical : GateOrientation.Horizontal;
            // 重建 ghost（footprint w/h 互换）
            if (_ghost != null) { Destroy(_ghost); _ghost = null; }
            CreateGhost(_selectedDef);
        }

        // ghost 跟随鼠标 + 微格吸附 + 绿/红反馈（2_2 步骤3/9）
        if (_ghost != null && Camera.main != null && GridSystem.Instance != null)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var subOpt = GridSystem.Instance.WorldToSubCoord(mouseWorld);
            if (subOpt.HasValue) // doc1 改造：null=越界，跳过 ghost 吸附与反馈
            {
                GridCoord sub = subOpt.Value;
                var orient = EffectiveOrientation(sub);
                _ghost.transform.position = GhostWorldPos(sub, orient);

                // 绿/红反馈（预览色走 BuildConfig SO，缺配置用默认）
                var check = PlacementValidator.ValidatePlacement(_selectedDef, sub, orient);
                var cfg = PlacementValidator.BuildConfig;
                Color okColor = cfg != null ? cfg.previewColorOk : new Color(0, 1, 0, 0.5f);
                Color badColor = cfg != null ? cfg.previewColorBad : new Color(1, 0, 0, 0.5f);
                if (_ghostRenderer != null)
                    _ghostRenderer.color = check.ok ? okColor : badColor;
            }
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

    /// <summary>
    /// 有效朝向（2_2 §3.4）：城门自动推断墙走向（墙优先于玩家 R 键，仅自由段玩家可转）；
    /// 其余 rotatable 建筑用玩家选择，非 rotatable 恒 Horizontal。
    /// </summary>
    GateOrientation EffectiveOrientation(GridCoord sub)
    {
        if (_selectedDef == null) return GateOrientation.Horizontal;
        if (!_selectedDef.rotatable) return GateOrientation.Horizontal;
        if (_selectedDef.isGate)
        {
            var origin = GridSystem.Instance.SubToCell(sub);
            var inferred = PlacementValidator.InferGateOrientation(origin, _playerOrientation);
            return inferred ?? _playerOrientation;   // 拐角（null）时按玩家朝向走，校验会拒
        }
        return _playerOrientation;
    }

    /// <summary>当前朝向下的占地 w×h。</summary>
    Vector2Int OrientedFootprint(GateOrientation orient)
    {
        int w = _footprint.x > 0 ? _footprint.x : 1;
        int h = _footprint.y > 0 ? _footprint.y : 1;
        if (_selectedDef != null && _selectedDef.rotatable && orient == GateOrientation.Vertical)
            return new Vector2Int(h, w);
        return new Vector2Int(w, h);
    }

    /// <summary>ghost 世界坐标：origin 格 + footprint 中心偏移（多格建筑视觉居中）。</summary>
    Vector3 GhostWorldPos(GridCoord sub, GateOrientation orient)
    {
        var grid = GridSystem.Instance;
        var origin = grid.SubToCell(sub);
        var fp = OrientedFootprint(orient);
        Vector2 originWorld = grid.CoordToWorld(origin);
        float cellW = grid.Config != null ? grid.Config.cellSize.x : 1.28f;
        float cellH = grid.Config != null ? grid.Config.cellSize.y : 0.64f;
        return originWorld + new Vector2((fp.x - 1) * 0.5f * cellW, (fp.y - 1) * 0.5f * cellH);
    }

    void TryPlace()
    {
        if (_selectedDef == null || Camera.main == null || GridSystem.Instance == null) return;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var subOpt = GridSystem.Instance.WorldToSubCoord(mouseWorld);
        if (!subOpt.HasValue) return; // doc1 改造：越界返回 null，不可放置
        GridCoord sub = subOpt.Value;

        GateOrientation orient = EffectiveOrientation(sub);
        // 玩家手工建造 = 指令通道建造门面 kingdomId=0（2_17 步骤7）：
        // AI 后续经同一运程成门面 TryBuild 下指令，校验/扣费/落成规则与玩家完全一致（D331/D345）。
        if (!TryBuild(_selectedDef, sub, orient, 0)) return;

        // Shift 连放，否则退出（走栈 Pop，触发 BuildModeEntry.Close -> ExitBuildMode + 回菜单）
        if (!Input.GetKey(KeyCode.LeftShift))
        {
            if (UIManager.Instance != null) UIManager.Instance.Pop();
            else ExitBuildMode(); // 兜底
        }
    }

    /// <summary>
    /// 指令通道建造门面（2_17 步骤7 / D331/D345）：玩家(AI 统一)与 AI 共用同入口。
    /// kingdomId=0=玩家（走 WarehouseHelper 王国仓库多仓凑单/金直通国库）；&gt;0=AI（走自身 KingdomState 五经济国库台账）。
    /// 选址合法性（领内/前置/资源）走 PlacementValidator 同一套 = 镜像原则。
    /// </summary>
    public bool TryBuild(BuildingDef def, GridCoord sub, GateOrientation orient, int kingdomId = 0)
    {
        if (def == null || GridSystem.Instance == null) return false;
        var grid = GridSystem.Instance;

        var check = PlacementValidator.ValidatePlacement(def, sub, orient, kingdomId);
        if (!check.ok)
        {
            Debug.Log($"[BuildController] 放置校验失败: {check.reason}");
            return false;
        }
        GridCoord coord = check.snappedOrigin;

        // 扣资源（门面：玩家→王国仓库凑单；AI→KingdomState.Spend 台账制，无事件）
        if (!CanPayBuild(kingdomId, def.cost)) return false;
        if (!PayBuild(kingdomId, def.cost)) return false;

        // 放置即改造：工具建筑（采石场/农场）建在资源格上，覆盖原资源节点
        // A+（HH.2）：树/矿不再建 Building 实体；改为数据覆盖该格 feature（Tree/Mine→Plain）+ 刷新渲染
        if (ResourceNodeMapping.RequiresResourceNode(def.id)
            && WorldManager.Instance != null)
        {
            WorldManager.Instance.TryConsumeResourceNode(coord);
        }

        // 实例化 Building（世界坐标 = footprint 中心）
        Vector3 worldPos = BuildingWorldPos(grid, sub, def, orient);
        GameObject go;
        if (def.prefab != null)
        {
            go = Instantiate(def.prefab, worldPos, Quaternion.identity);
        }
        else
        {
            // 无 prefab 时创建空壳 + 占位视觉（3.3.4 问题12）
            go = new GameObject($"Building_{def.id}_{coord.x}_{coord.y}");
            go.transform.position = worldPos;
            BuildingVisual.ApplyPlaceholder(go, BuildingType.None, def.role);
        }

        var b = go.GetComponent<Building>();
        if (b == null) b = go.AddComponent<Building>();
        var fp = OrientedFootprint(def, orient);
        b.Init(def, coord, true, fp);
        b.kingdomId = kingdomId;   // 2_17 步骤7：建造门面——AI 建造归属该国（玩家 0）
        b.StartConstructing();     // 建造走 Constructing 进度（玩家手工与 AI 同一条链）

        // 确保 Collider2D（size 局部 1x1，由 localScale 统一缩放，3.3.4 修复误触+碰撞盒）
        if (go.GetComponent<Collider2D>() == null)
        {
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
        }

        grid.MarkOccupiedFootprint(coord, fp.x, fp.y, b);

        // 桥：置 Bridge 位（水面可走）+ 接链 bridgeId（2_2 §3.5）
        if (def.isBridge)
        {
            grid.SetBridge(coord, fp.x, fp.y, true);
            b.bridgeId = FindAdjacentBridgeId(coord, fp) ?? System.Guid.NewGuid().ToString("N");
        }

        BuildingRegistry.Instance?.Register(b);
        BuildingFactory.Instance.AttachComponents(b, def);  // 建造也挂组件（3.3.4 批次5）

        // 城门：挂 GateController（开关切换 footprint 阻挡，2_2 §3.4）
        if (def.isGate && go.GetComponent<GateController>() == null)
            go.AddComponent<GateController>();

        EventBus.Publish(new BuildingPlacedEvent(b));

        Debug.Log($"[BuildController] 建造{(kingdomId > 0 ? $"AI国{kingdomId}" : "玩家")} {def.id} at cell ({coord.x},{coord.y}) fp {fp.x}x{fp.y}");

        return true;
    }

    /// <summary>建造国库抽象（2_17 步骤7）：玩家(0)→WarehouseHelper.CanAfford；AI→KingdomState.CanAfford 五经济资源。</summary>
    private static bool CanPayBuild(int kingdomId, ResourcePack cost)
    {
        if (kingdomId <= 0) return WarehouseHelper.CanAfford(cost);
        var ks = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(kingdomId) : null;
        return ks != null && ks.CanAfford(cost);
    }

    /// <summary>建造扣费（2_17 步骤7）：玩家(0)→WarehouseHelper.TrySettle；AI→KingdomState.Spend（台账制，无事件）。</summary>
    private static bool PayBuild(int kingdomId, ResourcePack cost)
    {
        if (kingdomId <= 0) return WarehouseHelper.TrySettle(cost);
        var ks = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(kingdomId) : null;
        if (ks == null) return false;
        ks.Spend(cost);
        return true;
    }

    /// <summary>给定 def/朝向下的占地 w×h（门面：不依赖当前选中态 _selectedDef/_footprint）。</summary>
    static Vector2Int OrientedFootprint(BuildingDef def, GateOrientation orient)
    {
        int w = def.footprint.x > 0 ? def.footprint.x : 1;
        int h = def.footprint.y > 0 ? def.footprint.y : 1;
        if (def.rotatable && orient == GateOrientation.Vertical) return new Vector2Int(h, w);
        return new Vector2Int(w, h);
    }

    /// <summary>建筑落点世界坐标（footprint 中心；门面用，不依赖 _selectedDef/_footprint）。</summary>
    static Vector3 BuildingWorldPos(GridSystem grid, GridCoord sub, BuildingDef def, GateOrientation orient)
    {
        var origin = grid.SubToCell(sub);
        var fp = OrientedFootprint(def, orient);
        Vector2 originWorld = grid.CoordToWorld(origin);
        float cellW = grid.Config != null ? grid.Config.cellSize.x : 1.28f;
        float cellH = grid.Config != null ? grid.Config.cellSize.y : 0.64f;
        return originWorld + new Vector2((fp.x - 1) * 0.5f * cellW, (fp.y - 1) * 0.5f * cellH);
    }

    /// <summary>找邻接桥段的 bridgeId（无邻接桥返回 null，开新链）。</summary>
    static string FindAdjacentBridgeId(GridCoord origin, Vector2Int fp)
    {
        var registry = BuildingRegistry.Instance;
        if (registry == null) return null;
        for (int dx = -1; dx <= fp.x; dx++)
        {
            var nb = registry.GetAt(new GridCoord(origin.x + dx, origin.y - 1))
                  ?? registry.GetAt(new GridCoord(origin.x + dx, origin.y + fp.y));
            if (nb != null && nb.def != null && nb.def.isBridge && !string.IsNullOrEmpty(nb.bridgeId)) return nb.bridgeId;
        }
        for (int dy = -1; dy <= fp.y; dy++)
        {
            var nb = registry.GetAt(new GridCoord(origin.x - 1, origin.y + dy))
                  ?? registry.GetAt(new GridCoord(origin.x + fp.x, origin.y + dy));
            if (nb != null && nb.def != null && nb.def.isBridge && !string.IsNullOrEmpty(nb.bridgeId)) return nb.bridgeId;
        }
        return null;
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
            // 多格占地视觉（3.3.4 批次8 + 2_2：按当前朝向 footprint 缩放）
            var fp = OrientedFootprint(_playerOrientation);
            float cellW = GridSystem.Instance != null && GridSystem.Instance.Config != null ? GridSystem.Instance.Config.cellSize.x : 1.28f;
            float cellH = GridSystem.Instance != null && GridSystem.Instance.Config != null ? GridSystem.Instance.Config.cellSize.y : 0.64f;
            _ghost.transform.localScale = new Vector3(fp.x * cellW, fp.y * cellH, 1);
        }

        if (_ghostRenderer != null)
        {
            var cfg = PlacementValidator.BuildConfig;
            _ghostRenderer.color = cfg != null ? cfg.previewColorOk : new Color(0, 1, 0, 0.5f);
        }
    }
}
