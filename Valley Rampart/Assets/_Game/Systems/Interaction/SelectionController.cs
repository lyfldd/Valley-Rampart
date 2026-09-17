using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2_13 交互层：上帝视角选择控制器（设计文档 §5.2）。玩家=上帝视角操作整个王国。
///
/// 交互划分（2026-09-01 批B 完善）：
///   - 左键 down/up 事件化（输入1 清障批 2026-09-01，HH.46 §八裁决）：订阅 LeftClickPressedEvent（down 记
///     框选起点）/ LeftClickReleasedEvent（up 判定「点选 or 框选」，leftClickRelease press(behavior=1)
///     ReleaseOnly 交互提供 release performed）——legacy Input 轮询退役；右键（批A）不动。
///   - 右键：订阅 RightClickPressedEvent（批A InputManager 发布，含屏幕坐标）→ 世界坐标 → 统一指令分派
///     （Follow(D2)/PrioritizeHarvest(D115)/DeployGuard(D116)/MoveTo）。
///   - 点选：命中己方单位 → Selected；命中建筑 → SelectedBuilding（仅己方 kingdomId==0，2_17 守门员）。
///   - 框选：屏幕矩形 → 世界矩形 → 单位索引区域查询（GridSystem.FillUnitsInRect·HH.294 片 6-1）→ 收集己方单位（kingdomId==0 双条件过滤）。
///   - dragThresholdPx 读 SelectionConfig SO（数值双落；默认 5px）。
///   - 摄像机中键 pan / 滚轮 zoom / WASD 平移由 CameraRig（2_10）自理，本类不重复。
/// </summary>
public class SelectionController : Singleton<SelectionController>
{
    // 【HH.294 片 6-1】`selectableMask`（原 `~0` 全层物理掩码）已随拾取改道**退役**：
    //   点拾取走 MapGate.PickAt（`03` §8.6）；框选走单位索引区域查询（GridSystem.FillUnitsInRect）；
    //   回退开关见 `MapGate.UseLegacyPhysicsPick`。

    private SelectionConfig _config;
    /// <summary>框选 scratch buffer（复用零分配·HH.294 片 6-1）。</summary>
    private readonly List<UnitController> _boxBuffer = new List<UnitController>(64);

    /// <summary>当前选中的己方单位（框选/点选）。</summary>
    public List<UnitController> Selected { get; } = new List<UnitController>();

    /// <summary>点选的建筑（独立于单位选择）。</summary>
    public Building SelectedBuilding { get; private set; }

    /// <summary>是否正拖拽（框选候选）。</summary>
    public bool IsDragging { get; private set; }

    // ===== 巡逻设定模式（DZ-081 / HH.133 件2，玩家侧入口；AI 侧零接线）=====
    private bool _patrolSetup;
    private readonly List<Vector2> _patrolWaypoints = new List<Vector2>();
    private const int PatrolMaxPoints = 4;
    private const int PatrolMinPoints = 2;

    /// <summary>是否处于巡逻路径点设定模式（单位操作 UI 按钮读口）。</summary>
    public bool IsPatrolSetupActive => _patrolSetup;

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
        EventBus.Subscribe<LeftClickPressedEvent>(OnLeftClickPressed);
        EventBus.Subscribe<LeftClickReleasedEvent>(OnLeftClickReleased);
        EventBus.Subscribe<RightClickPressedEvent>(OnRightClickPressed);
        EventBus.Subscribe<NumberKeyPressedEvent>(OnNumberKeyPressed);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<LeftClickPressedEvent>(OnLeftClickPressed);
        EventBus.Unsubscribe<LeftClickReleasedEvent>(OnLeftClickReleased);
        EventBus.Unsubscribe<RightClickPressedEvent>(OnRightClickPressed);
        EventBus.Unsubscribe<NumberKeyPressedEvent>(OnNumberKeyPressed);
    }

    // 左键 down 事件入口（输入1 清障批）：候选框选起锚（守门=面板开吞事件；模式/Playing 守门由 InputManager 发布侧承担）
    private void OnLeftClickPressed(LeftClickPressedEvent evt)
    {
        if (IsInteractionBlocked()) return;
        if (IsOverPatrolButton(evt.screenPos)) return;                   // 点 HUD 按钮不由世界层处理
        if (_patrolSetup) { AddPatrolWaypoint(evt.screenPos); return; }   // 设定模式：左键=记路径点
        IsDragging = true;
        _dragStartScreen = evt.screenPos;
    }

    // 左键 up 事件入口（输入1 清障批）：判定 点选 or 框选
    private void OnLeftClickReleased(LeftClickReleasedEvent evt)
    {
        if (IsInteractionBlocked()) return;
        if (_patrolSetup) { IsDragging = false; return; }                 // 路径点在按下时已记录
        if (IsOverPatrolButton(evt.screenPos)) { IsDragging = false; return; }
        if (!IsDragging) return;
        IsDragging = false;
        if (Vector2.Distance(_dragStartScreen, evt.screenPos) < DragThresholdPx)
            ClickSelect(evt.screenPos);
        else
            BoxSelect(evt.screenPos);
    }

    /// <summary>右键事件入口（批A InputManager 发布）：屏幕坐标 → 世界坐标 → 统一指令分派。</summary>
    private void OnRightClickPressed(RightClickPressedEvent evt)
    {
        if (IsInteractionBlocked()) return;
        if (_patrolSetup) { ConfirmPatrolSetup(); return; }               // 设定模式：右键=确认巡逻
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

    // ===== 巡逻设定模式（DZ-081 / HH.133 件2）=====

    /// <summary>
    /// 进入巡逻设定模式（单位操作 UI「巡逻」按钮调用）：左键依次点 2~4 个路径点，满 4 点自动确认；
    /// 或再点「巡逻」按钮/右键提前确认（≥2 点才生效）。仅作用于已选中己方单位——AI 侧零接线。
    /// </summary>
    public bool BeginPatrolSetup()
    {
        if (Selected.Count == 0)
        {
            Debug.LogWarning("[Selection] 巡逻：未选中己方单位，忽略");
            return false;
        }
        _patrolSetup = true;
        _patrolWaypoints.Clear();
        Debug.Log("[Selection] 进入巡逻设定模式：依次点击 2~4 个路径点（右键或再点「巡逻」确认）");
        return true;
    }

    /// <summary>确认发布巡逻：≥2 点则对每个已选中单位（有 NPCBrain）发路径点循环巡逻令，否则取消。</summary>
    public void ConfirmPatrolSetup()
    {
        if (!_patrolSetup) return;
        _patrolSetup = false;
        if (_patrolWaypoints.Count < PatrolMinPoints)
        {
            Debug.LogWarning($"[Selection] 巡逻取消：路径点不足 {PatrolMinPoints}（实际 {_patrolWaypoints.Count}）");
            _patrolWaypoints.Clear();
            return;
        }

        int issued = 0;
        for (int i = 0; i < Selected.Count; i++)
        {
            var u = Selected[i];
            if (u == null || !u.IsAlive) continue;
            var brain = u.GetComponent<NPCBrain>();
            if (brain == null) continue;
            PatrolTaskSystem.StartPatrol(brain, _patrolWaypoints);
            issued++;
        }
        Debug.Log($"[Selection] 巡逻令发布：{issued} 单位 × {_patrolWaypoints.Count} 路径点（遇敌自动转交战、敌清续巡）");
        _patrolWaypoints.Clear();
        ClearSelection();
    }

    /// <summary>取消巡逻设定模式（不发令）。</summary>
    public void CancelPatrolSetup()
    {
        if (!_patrolSetup) return;
        _patrolSetup = false;
        _patrolWaypoints.Clear();
        Debug.Log("[Selection] 巡逻设定已取消");
    }

    private void AddPatrolWaypoint(Vector2 screenPos)
    {
        if (_patrolWaypoints.Count >= PatrolMaxPoints)
        {
            Debug.LogWarning($"[Selection] 巡逻路径点已达上限 {PatrolMaxPoints}");
            return;
        }
        Vector2 world = ScreenToWorld(screenPos);
        _patrolWaypoints.Add(world);
        Debug.Log($"[Selection] 巡逻路径点 {_patrolWaypoints.Count}/{PatrolMaxPoints}：({world.x:F1}, {world.y:F1})");
        if (_patrolWaypoints.Count == PatrolMaxPoints)
        {
            Debug.Log("[Selection] 路径点满 4 → 自动确认巡逻");
            ConfirmPatrolSetup();
        }
    }

    /// <summary>指针是否落在 TopLeftHUD「巡逻」按钮上（面板↔屏幕像素归一；避免点按钮被当作路径点）。</summary>
    private static bool IsOverPatrolButton(Vector2 screenPos)
    {
        var hud = TopLeftHUD.Active;
        var btn = hud != null ? hud.PatrolButton : null;
        if (btn == null) return false;
        var panel = btn.panel;
        if (panel == null) return false;
        // 鼠标坐标为屏幕像素（底朝上），UI 为面板坐标（顶朝下）；ScaleWithScreenSize 下按面板实际尺寸归一
        var panelRect = panel.visualTree.worldBound;
        float sx = panelRect.width > 0f ? panelRect.width / Mathf.Max(1f, Screen.width) : 1f;
        float sy = panelRect.height > 0f ? panelRect.height / Mathf.Max(1f, Screen.height) : 1f;
        var panelPos = new Vector2(screenPos.x * sx, (Screen.height - screenPos.y) * sy);
        return btn.worldBound.Contains(panelPos);
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

        // 1) Follow（D2）：右键落在己方单位上（非选中者）——HH.294 片 6-1：改走 MapGate.PickWorld（渲染最前·坐标拾取）
        var hitSource = MapGate.PickWorld(world);
        var targetUnit = hitSource != null ? hitSource.GetComponentInParent<UnitController>() : null;
        // HH.81/D542 件1 配套：未入籍流浪（kingdomId=-1）放行——旧流浪挂玩家国(0)可被选中/跟随，
        // 置 -1 后此门须含 Vagrant，否则玩家招募点击交互（InteractAction）不可达=玩家招募链断。
        if (targetUnit != null && targetUnit.GetFaction() == Faction.PlayerCamp
            && (targetUnit.kingdomId == 0 || targetUnit.EffectiveOccupation == Occupation.Vagrant)
            && !alive.Contains(targetUnit))
        {
            if (EventBus.HasSubscribers<FollowCommand>())   // DZ-077：无订阅者不广播
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
            if (EventBus.HasSubscribers<GuardDeployCommand>())   // DZ-077：无订阅者不广播
                EventBus.Publish(new GuardDeployCommand(alive, world));
            GuardDeploymentSystem.DeployGuard(world);   // 消费端已就绪=真部署（2_13 预埋护栏接口）
            Debug.Log($"[Selection] D116 守卫部署：{alive.Count} 单位 → {world}");
            ClearSelection();
            return;
        }

        // 3) PrioritizeHarvest（D115）：全工人 + 资源点
        if (allWorkers && nearResource)
        {
            if (EventBus.HasSubscribers<PrioritizeHarvestCommand>())   // DZ-077：无订阅者不广播
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
        // DZ-077：无订阅者不广播（发事件，2_8 TaskScheduler 接管后以事件为准；直移保底在上）
        if (EventBus.HasSubscribers<UnitCommandEvent>())
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
        // HH.294 片 6-1：改走 MapGate.PickWorld（渲染最前·坐标拾取；双轨开关 MapGate.UseLegacyPhysicsPick）
        var hitSource = MapGate.PickWorld(world);
        // Shift+点击=加选/减选（2_13 步骤11C P1 输入档 D274；未按 Shift=常规重选）
        bool shiftHeld = UnityEngine.InputSystem.Keyboard.current != null &&
                         UnityEngine.InputSystem.Keyboard.current.shiftKey.isPressed;
        if (!shiftHeld)
        {
            Selected.Clear();
            SelectedBuilding = null;
        }

        if (hitSource != null)
        {
            // 己方单位优先（框选/点选仅收己方，R2）
            var unit = hitSource.GetComponentInParent<UnitController>();
            // 2_17 步骤3 双条件过滤（守门员）：仅玩家王国(kingdomId==0)单位可被选中——AI 工人以外籍身份(kingdomId>0)出场时
            // 不得被玩家选中下右键指令（GetFaction() 对 AI 工人仍返 PlayerCamp，须以 kingdomId 区分）。
            // HH.81/D542 件1 配套：未入籍流浪(kingdomId=-1)放行（玩家招募点击交互入口；AI 单位仍排除）。
            if (unit != null && unit.GetFaction() == Faction.PlayerCamp
                && (unit.kingdomId == 0 || unit.EffectiveOccupation == Occupation.Vagrant))
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
            var building = hitSource.GetComponentInParent<Building>();
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

        Selected.Clear();
        SelectedBuilding = null;

        // HH.294 片 6-1：框选改走**单位索引**区域查询（MapGate.FillUnitsInWorldRect；⛔ 不是 QueryCells 六项复合查询）。
        // 双轨旧路径（OverlapAreaAll）收容在门内（MapGate.UseLegacyPhysicsPick 开关）；本层零物理调用。
        _boxBuffer.Clear();
        var worldRect = new Rect(minX, minY, maxX - minX, maxY - minY);
        int candidates = MapGate.FillUnitsInWorldRect(worldRect, _boxBuffer);

        foreach (var unit in _boxBuffer)
        {
            if (unit == null) continue;
            // 2_17 步骤3：框选同做双条件过滤（仅玩家 kingdomId==0 单位，防纳 AI 工人）——
            // HH.81/D542 件1 配套：未入籍流浪(kingdomId=-1)放行（同点选门口径）。
            if (unit.GetFaction() == Faction.PlayerCamp
                && (unit.kingdomId == 0 || unit.EffectiveOccupation == Occupation.Vagrant) && !Selected.Contains(unit))
                Selected.Add(unit);
        }
        Debug.Log($"[Selection] 框选 {Selected.Count} 个己方单位（单位索引区域查询·HH.294 片 6-1；候选 {candidates}）");
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