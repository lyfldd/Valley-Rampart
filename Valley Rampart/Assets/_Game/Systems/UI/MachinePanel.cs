using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 战争机器生产面板（HH.111 玩家生产入口批，P8 首案玩家侧处置 D558④/D564）。
/// 由 BuildingPanel 的「生产」按钮（SiegeWorkshop 功能区）SetTarget 后 Push 入栈，关闭 Pop（TrainingPanel 同构）。
///
/// 显示：本族可用机器列表（族检过滤=SiegeProductionSystem.IsMachineAllowed 单源，他族机器不可见）+
/// 每项名称/造价/当前余量；置灰条件=上限满/资源不足（M3：UI 判定仅置灰，真校验以 ProduceMachine 返回为准）。
/// 点「生产」→ 进入放置模式（MachinePlacementEntry 虚拟栈条目，BuildModeEntry 先例）→ 点合法格生成。
///
/// 数据源单源纪律（任务书件1.2 禁抄表）：名称=TrainingPanel.OccName 同源/造价=PeekMachineCost 同源/
/// 族检=IsMachineAllowed 单源转发/上限余量=GetMachineLimit−GetPlacedMachineCount。
///
/// 渲染载体：运行时动态构建 UIDocument+PanelSettings（ToastManager 先例——零场景/prefab 资产改动，git 面干净）；
/// 刷新策略：事件驱动（UnitDiedEvent 机器阵亡余量变/RulerResourceChangedEvent 扣费置灰变）+打开时本地 Refresh
/// （TrainingPanel 刷新策略先例）。
/// </summary>
public class MachinePanel : Singleton<MachinePanel>, IUIPanel
{
    /// <summary>四族机器候选（白名单真源在 SiegeProductionSystem.IsRaceAllowedMachine，此处只枚举候选集）。</summary>
    private static readonly Occupation[] MachineCandidates =
        { Occupation.Ballista, Occupation.Mortar, Occupation.VineCatapult, Occupation.Ram };

    private Building _building;      // 所属战争机器工坊
    private bool _built;             // 动态 UI 是否已构建
    private bool _visible;

    // ===== UI 元素引用 =====
    private VisualElement _root;
    private Label _titleLabel;
    private Label _limitLabel;
    private VisualElement _rows;
    private Button _closeButton;

    // ===== 对外 API（由 BuildingPanel.OnMachineProduceClicked 调 SetTarget → Push）=====

    /// <summary>设置所属战争机器工坊。打开前调用。</summary>
    public void SetTarget(Building building)
    {
        _building = building;
    }

    // ===== IUIPanel =====

    public void Open(Interactor ctx)
    {
        EnsureBuilt();
        _visible = true;
        Refresh();
        SetVisible(true);
    }

    public void Close()
    {
        _visible = false;
        SetVisible(false);
        _building = null;
    }

    public void Refresh()
    {
        if (!_built) return;
        if (_titleLabel != null)
            _titleLabel.text = _building != null && _building.def != null
                ? $"{_building.def.displayName}（Lv.{_building.level}）生产"
                : "战争机器生产";

        RebuildLimit();
        RebuildRows();
    }

    // ===== Unity 生命周期 =====

    private void OnEnable()
    {
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
        EventBus.Subscribe<RulerResourceChangedEvent>(OnResourceChanged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
        EventBus.Unsubscribe<RulerResourceChangedEvent>(OnResourceChanged);
    }

    private void OnUnitDied(UnitDiedEvent evt)
    {
        if (_visible) Refresh();
    }

    private void OnResourceChanged(RulerResourceChangedEvent evt)
    {
        if (_visible) Refresh();
    }

    // ===== 动态 UI 构建（ToastManager 先例：运行时创建，零场景资产）=====

    private void EnsureBuilt()
    {
        if (_built) return;

        var doc = GetComponent<UIDocument>();
        if (doc == null)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.referenceResolution = new Vector2Int(1920, 950);
            settings.sortingOrder = 30000;   // 低于 ToastManager(32000)，toast 恒置顶
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = settings;
        }

        _root = doc.rootVisualElement;
        if (_root == null)
        {
            Debug.LogWarning("[MachinePanel] rootVisualElement 未就绪，延后构建。");
            return;
        }
        _root.Clear();
        _root.pickingMode = PickingMode.Ignore;   // 外层不拦截，面板本体自收点击

        var panel = new VisualElement();
        panel.name = "machine-panel";
        panel.AddToClassList("machine-panel");
        panel.style.position = Position.Absolute;
        panel.style.top = 120;
        panel.style.left = 360;
        panel.style.width = 380;
        panel.style.backgroundColor = new Color(0.12f, 0.1f, 0.08f, 0.96f);
        panel.style.borderTopLeftRadius = 8;
        panel.style.borderTopRightRadius = 8;
        panel.style.borderBottomLeftRadius = 8;
        panel.style.borderBottomRightRadius = 8;
        panel.style.borderTopWidth = 1;
        panel.style.borderBottomWidth = 1;
        panel.style.borderLeftWidth = 1;
        panel.style.borderRightWidth = 1;
        panel.style.borderTopColor = new Color(0.45f, 0.36f, 0.2f, 0.9f);
        panel.style.borderBottomColor = new Color(0.45f, 0.36f, 0.2f, 0.9f);
        panel.style.borderLeftColor = new Color(0.45f, 0.36f, 0.2f, 0.9f);
        panel.style.borderRightColor = new Color(0.45f, 0.36f, 0.2f, 0.9f);
        panel.style.paddingTop = 10;
        panel.style.paddingBottom = 12;
        panel.style.paddingLeft = 14;
        panel.style.paddingRight = 14;
        panel.pickingMode = PickingMode.Position;
        _root.Add(panel);

        // 标题栏（可拖动；TrainingPanel/BuildingPanel 先例 UIDragHelper）
        var handle = new VisualElement();
        handle.name = "drag-handle";
        handle.AddToClassList("machine-drag-handle");
        handle.style.flexDirection = FlexDirection.Row;
        handle.style.justifyContent = Justify.SpaceBetween;
        handle.style.alignItems = Align.Center;
        handle.style.marginBottom = 6;
        panel.Add(handle);

        _titleLabel = new Label("战争机器生产");
        _titleLabel.AddToClassList("machine-title");
        _titleLabel.style.fontSize = 18;
        _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _titleLabel.style.color = new Color(0.92f, 0.85f, 0.65f);
        handle.Add(_titleLabel);

        _closeButton = new Button(OnCloseClicked) { name = "machine-close-button", text = "×" };
        _closeButton.AddToClassList("machine-close");
        _closeButton.style.fontSize = 16;
        _closeButton.style.width = 24;
        handle.Add(_closeButton);

        // 上限余量行
        _limitLabel = new Label();
        _limitLabel.AddToClassList("machine-limit");
        _limitLabel.style.fontSize = 13;
        _limitLabel.style.color = new Color(0.75f, 0.75f, 0.7f);
        _limitLabel.style.marginBottom = 8;
        panel.Add(_limitLabel);

        // 机器行容器
        _rows = new VisualElement();
        _rows.name = "machine-rows";
        _rows.AddToClassList("machine-rows");
        panel.Add(_rows);

        UIDragHelper.Attach(panel, handle);
        _built = true;
        Debug.Log("[MachinePanel] 面板已构建（运行时动态 UIDocument，ToastManager 先例）。");
    }

    // ===== 数据行构建（数据源单源：族检/造价/余量全走 SiegeProductionSystem 门面）=====

    /// <summary>余量行：GetMachineLimit−GetPlacedMachineCount（同源）。</summary>
    private void RebuildLimit()
    {
        if (_limitLabel == null) return;
        var sys = SiegeProductionSystem.Instance;
        if (sys == null) { _limitLabel.text = "生产系统未就绪"; return; }
        _limitLabel.text = $"机器余量 {sys.GetPlacedMachineCount()}/{sys.GetMachineLimit()}";
    }

    /// <summary>本族可用机器行重建：族检过滤（他族机器不显示=白名单负向不可见）。</summary>
    private void RebuildRows()
    {
        if (_rows == null) return;
        _rows.Clear();

        var sys = SiegeProductionSystem.Instance;
        if (sys == null)
        {
            var miss = new Label("生产系统未就绪");
            miss.AddToClassList("machine-empty");
            _rows.Add(miss);
            return;
        }

        int playerRace = KingdomRace.GetKingdomRace(0);
        var ruler = RulerController.Instance;
        int limit = sys.GetMachineLimit();
        int placed = sys.GetPlacedMachineCount();
        bool limitFull = placed >= limit;

        int shown = 0;
        foreach (var occ in MachineCandidates)
        {
            // 族检过滤（M1 单源只读调用：共通退役槽/他族机器一律不显示）
            if (!SiegeProductionSystem.IsMachineAllowed(playerRace, occ)) continue;
            shown++;

            var cost = sys.PeekMachineCost(occ);
            bool afford = ruler != null && ruler.CanAfford(cost);

            var row = new VisualElement();
            row.AddToClassList("machine-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 6;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.backgroundColor = new Color(0.2f, 0.17f, 0.12f, 0.8f);
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;
            _rows.Add(row);

            var nameCol = new VisualElement();
            nameCol.style.flexDirection = FlexDirection.Column;
            row.Add(nameCol);

            var name = new Label(TrainingPanel.OccName(occ));   // 名称单源（任务书件1.2）
            name.AddToClassList("machine-name");
            name.style.fontSize = 15;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.color = new Color(0.9f, 0.88f, 0.8f);
            nameCol.Add(name);

            var costLabel = new Label(CostText(cost));
            costLabel.AddToClassList("machine-cost");
            costLabel.style.fontSize = 12;
            costLabel.style.color = afford ? new Color(0.7f, 0.75f, 0.6f) : new Color(0.85f, 0.45f, 0.35f);
            nameCol.Add(costLabel);

            var btn = new Button(() => OnProduceClicked(occ)) { text = "生产" };
            btn.AddToClassList("machine-btn");
            btn.style.width = 64;
            btn.style.fontSize = 13;
            // 置灰条件：上限满/资源不足（M3：仅置灰，真校验以 ProduceMachine 返回为准）
            btn.SetEnabled(!limitFull && afford);
            row.Add(btn);
        }

        if (shown == 0)
        {
            var none = new Label("本族暂无可用机器（白名单过滤后为空）");
            none.AddToClassList("machine-empty");
            _rows.Add(none);
        }
    }

    /// <summary>造价文案（非零项拼接，与 BuildingPanel 升级按钮口径一致）。</summary>
    private static string CostText(ResourcePack c)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (c.gold > 0) parts.Add($"金{c.gold}");
        if (c.stone > 0) parts.Add($"石{c.stone}");
        if (c.wood > 0) parts.Add($"木{c.wood}");
        if (c.food > 0) parts.Add($"粮{c.food}");
        if (c.metal > 0) parts.Add($"铁{c.metal}");
        return parts.Count > 0 ? string.Join(" ", parts) : "免费";
    }

    // ===== 数据/判定单源（HH.111 探针 P2/P5 与面板共用同一函数=行为级锚不漂移）=====

    /// <summary>
    /// 本族可见机器列表（族检过滤后）：共通退役槽/他族机器一律不显示（白名单负向不可见）。
    /// 判定真源=SiegeProductionSystem.IsMachineAllowed 单源转发（M1）；面板行构建与冒烟探针共用。
    /// </summary>
    public static System.Collections.Generic.List<Occupation> GetVisibleMachineList(int race)
    {
        var list = new System.Collections.Generic.List<Occupation>();
        for (int i = 0; i < MachineCandidates.Length; i++)
            if (SiegeProductionSystem.IsMachineAllowed(race, MachineCandidates[i]))
                list.Add(MachineCandidates[i]);
        return list;
    }

    /// <summary>
    /// prefab 缺失预检（P5 图纸口径）：UnitData 未登记/已登记 prefab 空 → true+原因文案。
    /// 预检不过不进放置不扣费（ProduceMachine 校验链零触碰=M3）；面板点击与冒烟探针共用。
    /// </summary>
    public static bool IsPrefabMissing(Occupation occ, out string reason)
    {
        var data = UnitDataManager.Instance != null
            ? UnitDataManager.Instance.GetData(Faction.PlayerCamp, occ)
            : null;
        if (data == null) { reason = "图纸未登记"; return true; }
        if (data.prefab == null) { reason = "图纸绘制中（美术批7）"; return true; }
        reason = null;
        return false;
    }

    // ===== 生产动作 =====

    /// <summary>
    /// 点「生产」：prefab 缺失预检（图纸口径 toast，不进放置不扣费）→ 进放置模式
    /// （MachinePlacementEntry 虚拟栈条目，BuildModeEntry 先例；本面板留栈底，放置关闭后返回）。
    /// </summary>
    private void OnProduceClicked(Occupation occ)
    {
        if (IsPrefabMissing(occ, out string why))
        {
            ToastManager.Instance?.Show($"「{TrainingPanel.OccName(occ)}」{why}", 3f);
            Debug.Log($"[MachinePanel] {occ} prefab 缺失，拦截进放置（P5 图纸口径）。");
            return;
        }

        UIManager.Instance?.Push(new MachinePlacementEntry(occ, this),
            new Interactor(Faction.PlayerCamp, Vector3.zero));
    }

    private void OnCloseClicked()
    {
        UIManager.Instance?.CloseCurrent();
    }

    private void SetVisible(bool visible)
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        // 面板本体隐藏（root 的 pickingMode=Ignore 不拦截全局点击）
        var panel = doc.rootVisualElement.Q<VisualElement>("machine-panel");
        if (panel != null)
            panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
