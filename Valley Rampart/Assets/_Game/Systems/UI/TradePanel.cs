using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 市场贸易面板（3.5 §七 / 实施计划 P1 步骤3（贸易）；UI Toolkit）。
/// 挂在 SampleScene 的 TradePanel GameObject 上（UIDocument）。实现 IUIPanel，由 BuildingPanel 的「贸易」按钮 Push 入栈，关闭 Pop。
///
/// 显示：市场等级 + 各已解锁资源行（名称 / 持有量+剩余额度 / 卖出 / 买入）。
///   - 国库资源（粮/木/石/特殊食物/肉）：可卖出换金 / 花金买入。
///   - 非国库资源（矿/水晶/火油，存建筑存储）：显示「需先收取」禁用。
///   - 不对称兑换损失 + 梯度额度由 TradeSystem / KingdomManager 处理，本面板只展示与调用。
///
/// 刷新策略：事件驱动（RulerResourceChangedEvent 更新持有量）+ 操作后本地 Refresh。
/// 所属系统：TradeSystem（兑换）/ KingdomManager（额度）/ RulerController（国库）。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class TradePanel : MonoBehaviour, IUIPanel
{
    private Building _market;          // 所属市场建筑（提供市场等级）
    private bool _bound;               // 防止重复绑定
    private bool _visible;             // 面板是否显示（事件回调里判断）

    // ===== UI 元素引用 =====
    private VisualElement _root;
    private Label _titleLabel;
    private VisualElement _resourceList;
    private Button _closeButton;

    /// <summary>可参与贸易的资源表（金不可自兑，剔除）。顺序即档位顺序。</summary>
    private static readonly (ResourceType type, string name)[] s_tradeableResources =
    {
        (ResourceType.Food, "粮"),
        (ResourceType.Wood, "木"),
        (ResourceType.Stone, "石"),
        (ResourceType.Ore, "矿"),
        (ResourceType.Crystal, "水晶"),
        (ResourceType.FireOil, "火油"),
        (ResourceType.SpecialFood, "特殊食物"),
        (ResourceType.Meat, "肉"),
        // ===== 2_12 步骤10：档位扩到 13（Metal + 3 弹药，D219 买紧缺）=====
        (ResourceType.Metal, "金属"),
        (ResourceType.StoneAmmo, "石弹"),
        (ResourceType.FireballAmmo, "火弹"),
        (ResourceType.MagicAmmo, "魔弹"),
    };

    // ===== 对外 API（由 BuildingPanel.OnTradeClicked 调用 SetTarget → Push）=====

    /// <summary>设置所属市场建筑（提供市场等级）。打开前调用。</summary>
    public void SetTarget(Building market)
    {
        _market = market;
    }

    // ===== IUIPanel =====

    public void Open(Interactor ctx)
    {
        if (!_bound) Bind();
        _visible = true;
        Refresh();
        SetVisible(true);
    }

    public void Close()
    {
        _visible = false;
        SetVisible(false);
        _market = null;
    }

    public void Refresh()
    {
        if (_market == null) return;
        int marketLevel = Mathf.Max(1, _market.level);
        if (_titleLabel != null) _titleLabel.text = $"市场贸易（Lv.{marketLevel}）";
        RebuildResourceList(marketLevel);
    }

    // ===== Unity 生命周期 =====

    private void OnEnable()
    {
        EventBus.Subscribe<RulerResourceChangedEvent>(OnResourceChanged);
        if (!_bound) Bind();
        SetVisible(false);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<RulerResourceChangedEvent>(OnResourceChanged);
        Unbind();
    }

    private void Start()
    {
        if (!_bound) Bind();
    }

    // ===== 事件回调（资源变化 → 刷新持有量）=====

    private void OnResourceChanged(RulerResourceChangedEvent evt)
    {
        if (_visible) Refresh();
    }

    // ===== 资源列表构建 =====

    /// <summary>重建资源交易列表：仅显示当前市场档位已解锁的资源。</summary>
    private void RebuildResourceList(int marketLevel)
    {
        if (_resourceList == null) return;
        _resourceList.Clear();
        var ts = TradeSystem.Instance;
        if (ts == null) return;

        for (int i = 0; i < s_tradeableResources.Length; i++)
        {
            var item = s_tradeableResources[i];
            if (!ts.IsTierUnlocked(item.type, marketLevel)) continue; // 未解锁档位不显示
            _resourceList.Add(CreateTradeRow(item.type, item.name, marketLevel));
        }
    }

    /// <summary>创建单个资源交易行。</summary>
    private VisualElement CreateTradeRow(ResourceType type, string displayName, int marketLevel)
    {
        var row = new VisualElement();
        row.AddToClassList("trade-row");

        // 名称
        var nameLabel = new Label { text = displayName };
        nameLabel.AddToClassList("trade-row-name");
        row.Add(nameLabel);

        // 持有量 + 剩余额度（额度按资源档位独立）
        int holding = RulerController.Instance != null ? RulerController.Instance.GetResource(type) : 0;
        var holdLabel = new Label { text = $"{holding} · 额度{QuotaOf(type)}" };
        holdLabel.name = "holding";
        holdLabel.AddToClassList("trade-row-holding");
        row.Add(holdLabel);

        // 国库资源可交易；非国库资源（矿/水晶/火油存建筑存储）显示「需先收取」
        if (TradeSystem.IsTreasuryResource(type))
        {
            int level = TradeSystem.GetResourceLevel(type);
            int sellRate = SellRateOf(level);
            int buyRate = BuyRateOf(level);

            var sellBtn = new Button(() => OnSellClicked(type, sellRate, marketLevel)) { text = $"卖{sellRate}→1金" };
            sellBtn.AddToClassList("trade-btn");
            sellBtn.AddToClassList("trade-btn--sell");
            row.Add(sellBtn);

            var buyBtn = new Button(() => OnBuyClicked(type, buyRate, marketLevel)) { text = $"1金→买{buyRate}" };
            buyBtn.AddToClassList("trade-btn");
            buyBtn.AddToClassList("trade-btn--buy");
            row.Add(buyBtn);
        }
        else
        {
            var hint = new Label { text = "存建筑存储，需先收取" };
            hint.AddToClassList("trade-row-hint");
            row.Add(hint);
        }

        return row;
    }

    // ===== 交易操作 =====

    private void OnSellClicked(ResourceType type, int amount, int marketLevel)
    {
        if (TradeSystem.Instance == null) return;
        int gold = TradeSystem.Instance.SellToGold(type, amount, marketLevel);
        if (gold <= 0) Debug.Log($"[TradePanel] 卖出 {type} 失败（额度不足 / 持有不足 / 未解锁）");
        Refresh();
    }

    private void OnBuyClicked(ResourceType type, int amount, int marketLevel)
    {
        if (TradeSystem.Instance == null) return;
        int got = TradeSystem.Instance.BuyWithGold(type, amount, marketLevel);
        if (got <= 0) Debug.Log($"[TradePanel] 买入 {type} 失败（额度不足 / 金不足 / 未解锁）");
        Refresh();
    }

    // ===== 按钮绑定 / 解绑 =====

    private void Bind()
    {
        if (_bound) return;
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        _root = doc.rootVisualElement;

        _titleLabel = _root.Q<Label>("trade-title");
        _resourceList = _root.Q<VisualElement>("trade-resource-list");
        _closeButton = _root.Q<Button>("trade-close-button");

        if (_closeButton != null) _closeButton.clicked += OnCloseClicked;

        // 标题栏拖动（可拖动窗口，不破坏关闭按钮点击）
        var panel = _root.Q<VisualElement>("trade-panel");
        var handle = _root.Q<VisualElement>("drag-handle");
        if (panel != null && handle != null) UIDragHelper.Attach(panel, handle);

        _bound = true;
    }

    private void Unbind()
    {
        if (!_bound) return;
        if (_closeButton != null) _closeButton.clicked -= OnCloseClicked;
        _bound = false;
    }

    private void OnCloseClicked()
    {
        UIManager.Instance?.CloseCurrent();
    }

    // ===== 辅助 =====

    /// <summary>该资源档位今日剩余额度（KingdomManager.TradeQuotaRemaining）。</summary>
    private static int QuotaOf(ResourceType type)
    {
        int level = TradeSystem.GetResourceLevel(type);
        var km = KingdomManager.Instance;
        if (km == null || level < 1 || level > 13) return 0;
        return km.TradeQuotaRemaining[level - 1];
    }

    /// <summary>卖出兑换率：多少单位换 1 金（KingdomConfig，默认 4）。</summary>
    private static int SellRateOf(int resourceLevel)
    {
        var cfg = KingdomManager.Instance != null ? KingdomManager.Instance.TradeConfig : null;
        return cfg != null ? cfg.GetTradeSellRate(resourceLevel) : 4;
    }

    /// <summary>买入兑换率：1 金换多少单位（TradeConfig，默认 3）。</summary>
    private static int BuyRateOf(int resourceLevel)
    {
        var cfg = KingdomManager.Instance != null ? KingdomManager.Instance.TradeConfig : null;
        return cfg != null ? cfg.GetTradeBuyRate(resourceLevel) : 3;
    }

    private void SetVisible(bool visible)
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}