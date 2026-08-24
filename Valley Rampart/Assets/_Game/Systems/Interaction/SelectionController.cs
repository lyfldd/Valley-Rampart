using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2_13 交互层：上帝视角选择控制器（设计文档 §5.2）。玩家=上帝视角操作整个王国。
///
/// 交互划分（P0）：
///   - 左键 down 时序 → 由 InteractionManager 处理 IInteractable（建筑/资源点面板派发，保留既有）。
///   - 本类自治轮询：左键 down 记框选起点 / up 判定「点选 or 框选」、右键 down = 指令/取消。
///     down/up 时序分离，不冲突。
///   - 点选：命中己方单位 → 加入 Selected（可对接集列表）；命中建筑 → SelectedBuilding。
///   - 框选：屏幕矩形 → 世界矩形 → Physics2D.OverlapAreaAll → 收集己方 UnitController（faction 过滤）。
///   - 右键：有选中 → 指令入口（移动归 2_8，P0 占位日志）；空选中右键 → 视作取消/平移。
///
/// P0 阈值 const 占位（≤ §5.5 SelectionConfig.dragThresholdPx=5，SO 化置后续）。
/// 摄像机中键 pan / 滚轮 zoom 已由 CameraRig（2_10）自理，本类不重复。
/// </summary>
public class SelectionController : Singleton<SelectionController>
{
    [Header("框选阈值（像素，P0 占位；§5.5 SelectionConfig.dragThresholdPx 待 SO 化）")]
    [Tooltip("拖拽距离 < 此值判点选，否则判框选")]
    public float dragThresholdPx = 5f;

    [Tooltip("选择检测层级（默认 ~0 同 InteractionManager）")]
    public LayerMask selectableMask = ~0;

    /// <summary>当前选中的己方单位（框选/点选，点击单次选择则仅 1 项）。</summary>
    public List<UnitController> Selected { get; } = new List<UnitController>();

    /// <summary>点选的建筑（独立于单位选择）。</summary>
    public Building SelectedBuilding { get; private set; }

    /// <summary>是否正拖拽（框选候选）。</summary>
    public bool IsDragging { get; private set; }

    public bool HasSelection => Selected.Count > 0 || SelectedBuilding != null;

    private Vector2 _dragStartScreen;

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        // 相机/层级由各次 Update 惰性取（Camera.main 场景摄像机）
    }

    private void Update()
    {
        // Build/Dialog 模式下不响应上帝视角选择
        if (InputManager.Instance != null && InputManager.Instance.CurrentMode != InputMode.Normal)
            return;
        if (UIManager.Instance != null && UIManager.Instance.HasPanelOpen)
            return;

        // 左键 down：候选框选起锚
        if (Input.GetMouseButtonDown(0))
        {
            IsDragging = true;
            _dragStartScreen = (Vector2)UnityEngine.Input.mousePosition;
            return;
        }

        // 左键 up：判定 点选 or 框选
        if (Input.GetMouseButtonUp(0) && IsDragging)
        {
            IsDragging = false;
            Vector2 upPos = UnityEngine.Input.mousePosition;
            if (Vector2.Distance(_dragStartScreen, upPos) < dragThresholdPx)
                ClickSelect(upPos);
            else
                BoxSelect(upPos);
            return;
        }

        // 右键 down：指令（选中→移动入口；空选→取消/平移）
        if (Input.GetMouseButtonDown(1))
        {
            IssueRightClick();
        }
    }

    /// <summary>清空全部选区。</summary>
    public void ClearSelection()
    {
        Selected.Clear();
        SelectedBuilding = null;
    }

    /// <summary>点选单个单位（外部/InteractionManager 命中单位时亦可调）。</summary>
    public void SelectUnit(UnitController unit)
    {
        SelectedBuilding = null;
        Selected.Clear();
        if (unit != null) Selected.Add(unit);
    }

    /// <summary>点选建筑（记录 SelectedBuilding；清单位选择）。</summary>
    public void SelectBuilding(Building building)
    {
        Selected.Clear();
        SelectedBuilding = building;
    }

    /// <summary>右键指令：选中单位集 → 移动到目标点（PathFollower 寻路，2_3/2_6）；仅选中建筑 → 取消选择。
    /// 空选 → 取消选择/平移。微操（D115）/守卫（D116）/跟随（D2）类型分派归 2_8（P1+，此处占位）。</summary>
    public void IssueRightClick()
    {
        if (!HasSelection)
        {
            // 空选右键：视作取消/平移（中键已 pan；此处仅清空并交回交互）
            ClearSelection();
            return;
        }
        Vector2 world = CursorToWorld();

        if (Selected.Count > 0)
        {
            foreach (var u in Selected)
            {
                if (u == null || !u.IsAlive) continue;
                var pf = u.GetComponent<PathFollower>();
                if (pf != null) pf.SetDestination(world);
            }
            Debug.Log($"[Selection] 右键移动指令：{Selected.Count} 单位 → {world}");
            // TODO(2_8)：右键目标类型分派——选中工人+资源点=PrioritizeHarvest(D115)、
            //   选中士兵+高价值点=DeployGuard(D116)、右键己方单位=Follow(D2)。
            // 深度覆盖（防 NPCBrain 即时重抢行为）归 2_8 任务/编队层。
        }
        else
        {
            Debug.Log($"[Selection] 右键：选中建筑 {SelectedBuilding?.name ?? "无"}（无移动目标，取消选择）");
        }

        ClearSelection();   // 移动下发后取消当前框选（上帝视角惯例）
    }

    // ===== 内部：点选 / 框选 =====

    private void ClickSelect(Vector2 screenPos)
    {
        Vector2 world = ScreenToWorld(screenPos);
        var hit = Physics2D.OverlapPoint(world, selectableMask);
        Selected.Clear();
        SelectedBuilding = null;

        if (hit != null)
        {
            // 己方单位优先（框选/点选仅收己方，R2）
            var unit = hit.GetComponentInParent<UnitController>();
            if (unit != null && unit.GetFaction() == Faction.Human_Player)
            {
                Selected.Add(unit);
                return;
            }
            // 建筑
            var building = hit.GetComponentInParent<Building>();
            if (building != null)
            {
                SelectedBuilding = building;
                return;
            }
        }
        // 点空白：选择侧清空（面板关闭由 InteractionManager down 时序处理）
    }

    private void BoxSelect(Vector2 endScreen)
    {
        Vector2 s0 = ScreenToWorld(_dragStartScreen);
        Vector2 s1 = ScreenToWorld(endScreen);
        float minX = Mathf.Min(s0.x, s1.x), maxX = Mathf.Max(s0.x, s1.x);
        float minY = Mathf.Min(s0.y, s1.y), maxY = Mathf.Max(s0.y, s1.y);
        var cols = Physics2D.OverlapAreaAll(new Vector2(minX, minY), new Vector2(maxX, maxY), selectableMask);

        Selected.Clear();
        SelectedBuilding = null;
        foreach (var c in cols)
        {
            var unit = c.GetComponentInParent<UnitController>();
            if (unit != null && unit.GetFaction() == Faction.Human_Player && !Selected.Contains(unit))
                Selected.Add(unit);
        }
        Debug.Log($"[Selection] 框选 {Selected.Count} 个己方单位");
    }

    // ===== 坐标辅助 =====

    private Vector2 CursorToWorld() => ScreenToWorld(UnityEngine.Input.mousePosition);

    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (cam == null) return Vector2.zero;
        return cam.ScreenToWorldPoint(screenPos);
    }
}