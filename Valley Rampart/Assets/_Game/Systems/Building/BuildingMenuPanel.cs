using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 建造菜单面板（3.3 第六节 + 3.5 六模块 + 文档 2.1 流程）。挂 SampleScene 上。
/// 按 ModuleType（土木/生产/民生/军事/商业/科技）分组列出 isPlayerBuilt=true 的 BuildingDef，
/// 显示造价，点"建造"进入建造模式。列表用 ScrollView 支持真正滚动；标题栏可拖动窗口。
///
/// <para>刷新策略：事件驱动（B 键 ToggleBuildMenuPressedEvent）+ 打开时重建（EnsureDefsLoaded）。</para>
/// <para>解锁判定：KingdomManager.Instance.IsBuildingUnlocked(def)（模块级 + 特殊建筑）；KingdomManager 为 null 时回退显示全部 isPlayerBuilt。</para>
/// <para>通过 UIManager 打开/关闭，也支持 B 键快捷开关。</para>
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class BuildingMenuPanel : MonoBehaviour, IUIPanel
{
    private bool _buttonsBound;
    private bool _eventBound;
    private ModuleType _currentTab = ModuleType.Civil;

    // ===== 缓存引用 =====
    private Button _closeBtn;
    private ScrollView _buildingList;
    private readonly Dictionary<ModuleType, Button> _tabs = new Dictionary<ModuleType, Button>();
    private readonly List<BuildingDef> _allBuildable = new List<BuildingDef>();
    private bool _defLoaded;

    /// <summary>六大模块展示顺序（与 ModuleType 枚举一致）。</summary>
    private static readonly ModuleType[] ModuleOrder =
    {
        ModuleType.Civil,
        ModuleType.Production,
        ModuleType.Livelihood,
        ModuleType.Military,
        ModuleType.Commerce,
        ModuleType.Science,
    };

    // ===== IUIPanel =====

    public void Open(Interactor ctx)
    {
        if (!_buttonsBound) BindButtons();
        EnsureDefsLoaded();
        SwitchTab(ModuleType.Civil);
        RefreshAllEnabled();
        SetVisible(true);
    }

    public void Close()
    {
        SetVisible(false);
    }

    public void Refresh()
    {
        RefreshAllEnabled();
    }

    // ===== 标签页切换 =====

    private void SwitchTab(ModuleType module)
    {
        _currentTab = module;
        // 更新 tab 高亮
        foreach (var kv in _tabs)
        {
            if (kv.Value == null) continue;
            RemoveClass(kv.Value, "tab-button--active");
            if (kv.Key == module) AddClass(kv.Value, "tab-button--active");
        }
        // 重建列表
        RebuildList();
    }

    // ===== Unity 生命周期 =====

    private void OnEnable()
    {
        if (!_buttonsBound) BindButtons();
        if (!_eventBound) BindEvent();
        SetVisible(false);
    }

    private void OnDisable()
    {
        UnbindButtons();
        UnbindEvent();
    }

    private void Start()
    {
        if (!_buttonsBound) BindButtons();
        if (!_eventBound) BindEvent();
    }

    // ===== 按钮绑定 =====

    private void BindButtons()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        var root = doc.rootVisualElement;

        _closeBtn = root.Q<Button>("menu-close");
        if (_closeBtn != null) _closeBtn.clicked += OnCloseClicked;

        _buildingList = root.Q<ScrollView>("building-list");

        _tabs[ModuleType.Civil] = root.Q<Button>("tab-civil");
        _tabs[ModuleType.Production] = root.Q<Button>("tab-production");
        _tabs[ModuleType.Livelihood] = root.Q<Button>("tab-livelihood");
        _tabs[ModuleType.Military] = root.Q<Button>("tab-military");
        _tabs[ModuleType.Commerce] = root.Q<Button>("tab-commerce");
        _tabs[ModuleType.Science] = root.Q<Button>("tab-science");

        foreach (var module in ModuleOrder)
        {
            if (_tabs[module] != null)
            {
                var m = module; // 局部捕获，避免闭包共享
                _tabs[module].clicked += () => SwitchTab(m);
            }
        }

        // 标题栏拖动（可拖动窗口，不破坏关闭按钮点击）
        var panel = root.Q<VisualElement>("menu-panel");
        var handle = root.Q<VisualElement>("drag-handle");
        if (panel != null && handle != null) UIDragHelper.Attach(panel, handle);

        _buttonsBound = true;
    }

    private void UnbindButtons()
    {
        if (!_buttonsBound) return;
        if (_closeBtn != null) _closeBtn.clicked -= OnCloseClicked;
        foreach (var kv in _tabs)
        {
            if (kv.Value == null) continue;
            kv.Value.clicked -= null;
        }
        _buttonsBound = false;
    }

    // ===== B 键事件 =====

    private void BindEvent()
    {
        EventBus.Subscribe<ToggleBuildMenuPressedEvent>(OnToggleBuildMenu);
        _eventBound = true;
        Debug.Log("[BuildingMenuPanel] 已订阅 ToggleBuildMenuPressedEvent");
    }

    private void UnbindEvent()
    {
        if (!_eventBound) return;
        EventBus.Unsubscribe<ToggleBuildMenuPressedEvent>(OnToggleBuildMenu);
        _eventBound = false;
    }

    private void OnToggleBuildMenu(ToggleBuildMenuPressedEvent e)
    {
        Debug.Log("[BuildingMenuPanel] 收到 ToggleBuildMenuPressedEvent");
        var ui = UIManager.Instance;
        if (ui == null)
        {
            Debug.LogWarning("[BuildingMenuPanel] UIManager.Instance 为 null");
            return;
        }
        // 栈顶是本菜单则关闭，否则打开
        if (ui.Peek() == this)
        {
            Debug.Log("[BuildingMenuPanel] 关闭菜单");
            ui.Pop();
        }
        else
        {
            Debug.Log("[BuildingMenuPanel] 打开菜单");
            ui.Push(this, new Interactor(Faction.Human_Player, Vector3.zero));
        }
    }

    // ===== 加载可建造列表 =====

    private void EnsureDefsLoaded()
    {
        if (_defLoaded) return;
        _allBuildable.Clear();
        var assets = Resources.LoadAll<BuildingDef>("Buildings");
        if (assets != null)
        {
            foreach (var def in assets)
            {
                if (def != null && def.isPlayerBuilt)
                    _allBuildable.Add(def);
            }
        }
        _defLoaded = true;
    }

    // ===== 重建建筑列表 =====

    private void RebuildList()
    {
        if (_buildingList == null) return;
        var container = _buildingList.contentContainer;
        container.Clear();

        var ruler = RulerController.Instance;

        int count = 0;
        foreach (var def in _allBuildable)
        {
            if (def.moduleType != _currentTab) continue;
            // 解锁判定：3.5 改用 KingdomManager 模块级/特殊建筑判定；KingdomManager 为 null 时回退显示全部
            if (KingdomManager.Instance != null && !KingdomManager.Instance.IsBuildingUnlocked(def)) continue;
            BuildCard(def, ruler);
            count++;
        }

        if (count == 0)
        {
            var empty = new VisualElement { name = "empty-hint-wrap" };
            empty.AddToClassList("empty-list-hint");
            // 主城未修复（castleLevel=0）时明确引导，避免"空面板"无解释
            bool castleNotRepaired = KingdomManager.Instance != null && KingdomManager.Instance.CastleLevel < 1;
            string hint = castleNotRepaired
                ? "请先修复主城（点击废弃城堡）以解锁建造"
                : "该模块暂无可用建筑（需升级主城 / 模块等级解锁）";
            var lbl = new Label(hint) { name = "empty-hint", text = hint };
            lbl.AddToClassList("empty-list-hint-text");
            empty.Add(lbl);
            container.Add(empty);
        }

        // 滚动回顶部，避免切换 tab 后停留在旧位置
        _buildingList.verticalScroller.value = 0f;
    }

    private void BuildCard(BuildingDef def, RulerController ruler)
    {
        var card = new VisualElement { name = $"card-{def.id}" };
        card.AddToClassList("building-card");

        // Header: 名称 + 建造按钮
        var header = new VisualElement { name = "header" };
        header.AddToClassList("building-card__header");
        var nameLabel = new Label(def.displayName) { name = $"name-{def.id}" };
        nameLabel.AddToClassList("building-card__name");
        var buildBtn = new Button { name = $"build-{def.id}", text = "建造" };
        buildBtn.AddToClassList("building-card__build-btn");
        buildBtn.SetEnabled(ruler != null && ruler.CanAfford(def.cost));
        buildBtn.clicked += () => OnBuildClicked(def);
        header.Add(nameLabel);
        header.Add(buildBtn);
        card.Add(header);

        // 描述
        if (!string.IsNullOrEmpty(def.description))
        {
            var desc = new Label(def.description) { name = $"desc-{def.id}" };
            desc.AddToClassList("building-card__desc");
            card.Add(desc);
        }

        // 属性行
        var statRow = new VisualElement { name = "stats" };
        statRow.AddToClassList("building-card__stats");
        AddStatCell(statRow, "占地", $"{def.footprint.x}x{def.footprint.y}");
        if (def.combat.maxHp > 0) AddStatCell(statRow, "HP", def.combat.maxHp.ToString());
        if (def.combat.attack > 0) AddStatCell(statRow, "攻击", def.combat.attack.ToString());
        if (def.producer.rate > 0f) AddStatCell(statRow, "产能", $"{def.producer.rate:F1}/s");
        card.Add(statRow);

        // 造价行
        var costRow = new VisualElement { name = "cost" };
        costRow.AddToClassList("building-card__cost-row");
        AddCostItem(costRow, "金", def.cost.gold, ResourceType.Gold, ruler);
        AddCostItem(costRow, "石", def.cost.stone, ResourceType.Stone, ruler);
        AddCostItem(costRow, "木", def.cost.wood, ResourceType.Wood, ruler);
        AddCostItem(costRow, "粮", def.cost.food, ResourceType.Food, ruler);
        card.Add(costRow);

        _buildingList.contentContainer.Add(card);
    }

    // ===== 辅助 =====

    private void AddStatCell(VisualElement parent, string label, string value)
    {
        var cell = new VisualElement();
        cell.AddToClassList("building-card__stat-cell");
        var l = new Label(label) { name = "label" };
        l.AddToClassList("building-card__stat-label");
        var v = new Label(value) { name = "value" };
        v.AddToClassList("building-card__stat-value");
        cell.Add(l);
        cell.Add(v);
        parent.Add(cell);
    }

    private void AddCostItem(VisualElement parent, string label, int amount, ResourceType type, RulerController ruler)
    {
        if (amount <= 0) return;
        var item = new VisualElement { name = $"cost-{label}" };
        item.AddToClassList("cost-item");
        var icon = new VisualElement { name = "icon" };
        icon.AddToClassList("cost-item__icon");
        icon.AddToClassList($"cost-item__icon--{CostIconClass(type)}");
        var val = new Label($"{label} {amount}") { name = "value" };
        val.AddToClassList("cost-item__value");
        bool sufficient = ruler != null && ruler.HasAmount(type, amount);
        if (!sufficient) val.AddToClassList("cost-item__value--insufficient");
        item.Add(icon);
        item.Add(val);
        parent.Add(item);
    }

    private static string CostIconClass(ResourceType t)
    {
        switch (t)
        {
            case ResourceType.Gold: return "gold";
            case ResourceType.Stone: return "stone";
            case ResourceType.Wood: return "wood";
            case ResourceType.Food: return "food";
            default: return "gold";
        }
    }

    private static void AddClass(VisualElement ve, string c)
    {
        if (ve != null && !ve.ClassListContains(c)) ve.AddToClassList(c);
    }

    private static void RemoveClass(VisualElement ve, string c)
    {
        if (ve != null && ve.ClassListContains(c)) ve.RemoveFromClassList(c);
    }

    // ===== 刷新按钮可用性（资源变化时）=====

    private void RefreshAllEnabled()
    {
        if (_buildingList == null) return;
        var ruler = RulerController.Instance;
        EnsureDefsLoaded();
        // 重建列表（包含建造按钮启用状态）
        RebuildList();
    }

    // ===== 事件 =====

    private void OnCloseClicked()
    {
        UIManager.Instance?.CloseCurrent();
    }

    private void OnBuildClicked(BuildingDef def)
    {
        if (def == null) return;
        if (RulerController.Instance != null && !RulerController.Instance.CanAfford(def.cost))
        {
            Debug.LogWarning("[BuildingMenuPanel] 资源不足: " + def.id);
            return;
        }
        // 菜单出栈（隐藏）+ 建造模式入栈（ESC 后返回菜单）
        var ui = UIManager.Instance;
        if (ui != null)
        {
            ui.Pop();  // 关闭菜单
            ui.Push(new BuildModeEntry(def, this), new Interactor(Faction.Human_Player, Vector3.zero));
        }
        else
        {
            BuildController.Instance?.EnterBuildMode(def);
        }
    }

    private void SetVisible(bool visible)
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}