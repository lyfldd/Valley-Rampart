using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2_13 交互层：上帝视角选择控制器（设计文档 §5.2）。玩家=上帝视角操作整个王国。
///
/// 交互划分（2026-09-01 批B 完善）：
///   - 左键 down/up 自治轮询（Legacy Input）：down 记框选起点 / up 判定「点选 or 框选」。
///     保留轮询原因：框选需 down+up 双点时序，Input System Button 仅 performed（按下）无 release
///     performed，事件化需 inputactions 交互结构扩展——漂移报策划（HH.46 清单）。
///   - 右键：订阅 RightClickPressedEvent（批A InputManager 发布，含屏幕坐标）→ 世界坐标 → 统一指令分派
///     （Follow(D2)/PrioritizeHarvest(D115)/DeployGuard(D116)/MoveTo）。
///   - 点选：命中己方单位 → Selected；命中建筑 → SelectedBuilding（仅己方 kingdomId==0，2_17 守门员）。
///   - 框选：屏幕矩形 → 世界矩形 → Physics2D.OverlapAreaAll → 收集己方单位（kingdomId==0 双条件过滤）。
///   - dragThresholdPx 读 SelectionConfig SO（数值双落；默认 5px）。
///   - 摄像机中键 pan / 滚轮 zoom / WASD 平移由 CameraRig（2_10）自理，本类不重复。
/// </summary>
public class SelectionController : Singleton<SelectionController>
{
    [Tooltip("选择检测层级（默认 ~0 同 InteractionManager）")]
    public LayerMask selectableMask = ~0;

    private SelectionConfig _config;

    /// <summary>当前选中的己方单位（框选/点选）。</summary>
    public List<UnitController> Selected { get; } = new List<UnitController>();

    /// <summary>点选的建筑（独立于单位选择）。</summary>
    public Building SelectedBuilding { get; private set; }

    /// <summary>是否正拖拽（框选候选）。</summary>
    public bool IsDragging { get; private set; }

    public bool HasSelection => Selected.Count > 0 || SelectedBuilding != null;

    /// <summary>框选阈值（像素；SelectionConfig SO，so-data-driven）。</summary>
    public float DragThresholdPx => (_config != null ? _config.dragThresholdPx : 5);

    private Vector2 _dragStartScreen;

    // 控制组 1~9（2_13 步骤11C D274）：Ctrl+数字=保存当前选中集，数字=恢复选中集（选区级真响应；
    // 2_8 编队层后续可接管为军令组语义）
    private readonly Dictionary<int, List<UnitController>> _groups = new Dictionary<int, List<UnitController>>();

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        _config = SelectionConfig.Load();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<RightClickPressedEvent>(OnRightClickPressed);
        EventBus.Subscribe<NumberKeyPressedEvent>(OnNumberKeyPressed);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<RightClickPressedEvent>(OnRightClickPressed);
        EventBus.Unsubscribe<NumberKeyPressedEvent>(OnNumberKeyPressed);
    }

    private void Update()
    {
        // Build/Dialog 模式下不响应上帝视角选择
        if (InputManager.Instance != null && !InputManager.Instance.IsInteractionEnabled)
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
            if (Vector2.Distance(_dragStartScreen, upPos) < DragThresholdPx)
                ClickSelect(upPos);
            else
                BoxSelect(upPos);
        }
    }

    /// <summary>右键事件入口（批A InputManager 发布）：屏幕坐标 → 世界坐标 → 统一指令分派。</summary>
    private void OnRightClickPressed(RightClickPressedEvent evt)
    {
        if (!IsInteractionBlocked())
            IssueRightClick(ScreenToWorld(evt.screenPos));
    }

    /// <summary>交互是否被面板/模式阻断（与左键自治轮询同守门）。</summary>
    private bool IsInteractionBlocked()
    {
        if (UIManager.Instance != null && UIManager.Instance.HasPanelOpen) return true;
        return false;
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

    /// <summary>
    /// 右键统一指令分派（D115/D116/D2/MoveTo）：
    ///   1) 右键己方单位 → Follow（D2：跟随目标移动；发事件 + 直移保底，持续跟随 2_8 编队层）；
    ///   2) 选中含士兵 + 右键高价值资源点 → DeployGuard（D116：发事件 + 接线 GuardDeploymentSystem.DeployGuard=消费端就绪真部署）；
    ///   3) 全 Worker + 右键资源点 → PrioritizeHarvest（D115：发事件，2_8 TaskScheduler 消费挂账）；
    ///   4) 否则 → MoveTo（直移保底 + 发 UnitCommandEvent，2_8 接管后以事件为准）；
    ///   空选/仅建筑 → 取消选择。
    /// </summary>
    public void IssueRightClick(Vector2 world)
    {
        var alive = new List<UnitController>(Selected.Count);
        foreach (var u in Selected)
            if (u != null && u.IsAlive) alive.Add(u);

        if (alive.Count == 0)
        {
            // 空选或仅选中建筑：取消选择（中键已 pan；此处清空并交回交互）
            ClearSelection();
            return;
        }

        // 1) Follow（D2）：右键落在己方单位上（非选中者）
        var hit = Physics2D.OverlapPoint(world, selectableMask);
        var targetUnit = hit != null ? hit.GetComponentInParent<UnitController>() : null;
        if (targetUnit != null && targetUnit.GetFaction() == Faction.PlayerCamp && targetUnit.kingdomId == 0
            && !alive.Contains(targetUnit))
        {
            EventBus.Publish(new FollowCommand(alive, targetUnit));
            // 保底：直移到目标当前位；持续跟随语义归 2_8 编队层（挂账）
            foreach (var u in alive)
            {
                var pf = u.GetComponent<PathFollower>();
                if (pf != null && targetUnit != null) pf.SetDestination((Vector2)targetUnit.transform.position);
            }
            Debug.Log($"[Selection] D2 跟进：{alive.Count} 单位 → {targetUnit.name}");
            ClearSelection();
            return;
        }

        // 目标分派前：高价值资源点判定（GuardDeploymentSystem 就近吸附，Tree/Mine/OreVein）
        bool nearResource = GuardDeploymentSystem.FindNearestResourceNode(world).HasValue;
        bool hasSoldier = alive.Exists(IsSoldier);
        bool allWorkers = alive.Count > 0 && alive.TrueForAll(IsWorker);

        // 2) DeployGuard（D116）：士兵 + 高价值点
        if (hasSoldier && nearResource)
        {
            EventBus.Publish(new GuardDeployCommand(alive, world));
            GuardDeploymentSystem.DeployGuard(world);   // 消费端已就绪=真部署（2_13 预埋护栏接口）
            Debug.Log($"[Selection] D116 守卫部署：{alive.Count} 单位 → {world}");
            ClearSelection();
            return;
        }

        // 3) PrioritizeHarvest（D115）：全工人 + 资源点
        if (allWorkers && nearResource)
        {
            EventBus.Publish(new PrioritizeHarvestCommand(alive, world));
            Debug.Log($"[Selection] D115 优先采集：{alive.Count} 工人 → {world}（2_8 TaskScheduler 消费挂账）");
            ClearSelection();
            return;
        }

        // 4) MoveTo：直移保底 + 发事件
        foreach (var u in alive)
        {
            var pf = u.GetComponent<PathFollower>();
            if (pf != null) pf.SetDestination(world);
        }
        EventBus.Publish(new UnitCommandEvent(alive, world));
        Debug.Log($"[Selection] 右键移动指令：{alive.Count} 单位 → {world}");
        ClearSelection();
    }

    // ===== 职业判定（D115 工人 / D116 士兵，对齐实施计划步骤 11）=====

    private static bool IsWorker(UnitController u)
        => u != null && u.EffectiveOccupation == Occupation.Worker;

    private static bool IsSoldier(UnitController u)
    {
        if (u == null) return false;
        switch (u.EffectiveOccupation)
        {
            case Occupation.Warrior:
            case Occupation.Archer:
            case Occupation.Mage:
            case Occupation.Healer:
            case Occupation.Cavalry:
            case Occupation.General:
                return true;
            default:
                return false;
        }
    }

    // ===== 内部：点选 / 框选 =====

    private void ClickSelect(Vector2 screenPos)
    {
        Vector2 world = ScreenToWorld(screenPos);
        var hit = Physics2D.OverlapPoint(world, selectableMask);
        // Shift+点击=加选/减选（2_13 步骤11C P1 输入档 D274；未按 Shift=常规重选）
        bool shiftHeld = UnityEngine.InputSystem.Keyboard.current != null &&
                         UnityEngine.InputSystem.Keyboard.current.shiftKey.isPressed;
        if (!shiftHeld)
        {
            Selected.Clear();
            SelectedBuilding = null;
        }

        if (hit != null)
        {
            // 己方单位优先（框选/点选仅收己方，R2）
            var unit = hit.GetComponentInParent<UnitController>();
            // 2_17 步骤3 双条件过滤（守门员）：仅玩家王国(kingdomId==0)单位可被选中——AI 工人以外籍身份(kingdomId>0)出场时
            // 不得被玩家选中下右键指令（GetFaction() 对 AI 工人仍返 PlayerCamp，须以 kingdomId 区分）
            if (unit != null && unit.GetFaction() == Faction.PlayerCamp && unit.kingdomId == 0)
            {
                if (shiftHeld)
                {
                    // Shift 加选/减选：已选则移除，未选则加入
                    if (!Selected.Remove(unit)) Selected.Add(unit);
                }
                else
                {
                    Selected.Add(unit);
                }
                return;
            }
            // 建筑：2_16 步骤7 补丁B——仅可选中己方王国（kingdomId==0），防玩家框选 AI 建筑下指令
            var building = hit.GetComponentInParent<Building>();
            if (building != null && building.kingdomId == 0)
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
            // 2_17 步骤3：框选同做双条件过滤（仅玩家 kingdomId==0 单位，防纳 AI 工人）
            if (unit != null && unit.GetFaction() == Faction.PlayerCamp && unit.kingdomId == 0 && !Selected.Contains(unit))
                Selected.Add(unit);
        }
        Debug.Log($"[Selection] 框选 {Selected.Count} 个己方单位");
    }

    // ===== 控制组（2_13 步骤11C D274：Ctrl+数字=保存，数字=调用）=====

    private void OnNumberKeyPressed(NumberKeyPressedEvent evt)
    {
        if (IsInteractionBlocked()) return;

        if (evt.WithCtrl)
        {
            // 保存：当前选中集（深拷贝；死亡单位在调用时过滤）
            _groups[evt.Index] = new List<UnitController>(Selected);
            Debug.Log($"[Selection] 控制组 {evt.Index} 保存：{_groups[evt.Index].Count} 单位");
        }
        else if (_groups.TryGetValue(evt.Index, out var group))
        {
            // 调用：恢复选中集（剔除已死亡单位）
            Selected.Clear();
            SelectedBuilding = null;
            foreach (var u in group)
                if (u != null && u.IsAlive && !Selected.Contains(u))
                    Selected.Add(u);
            Debug.Log($"[Selection] 控制组 {evt.Index} 调用：{Selected.Count} 单位存活");
        }
        else
        {
            Debug.Log($"[Selection] 控制组 {evt.Index} 为空，忽略调用");
        }
    }

    // ===== 坐标辅助 =====

    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (cam == null) return Vector2.zero;
        return cam.ScreenToWorldPoint(screenPos);
    }
}