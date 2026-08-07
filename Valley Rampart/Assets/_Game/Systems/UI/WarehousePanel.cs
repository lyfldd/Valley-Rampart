using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 全局仓储面板（QQQ.2 §需求7 / DR-15）。挂在场景的 WarehousePanel GameObject 上（UIDocument）。
/// 实现 IUIPanel，由 TopLeftHUD 的「仓库」按钮 Push 入栈，关闭 Pop。
///
/// 汇总所有带 StorageComponent 的建筑，按资源类型显示「仓库总量 / 容量」。
/// 与国库（RulerController 主资源）区分：本面板显示各仓库建筑里的存量分布。
///
/// 刷新机制（DR-15）：订阅每个 StorageComponent.OnStorageChanged 事件实时刷新；
/// Open 时订阅、Close 时退订，避免事件泄漏。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class WarehousePanel : MonoBehaviour, IUIPanel
{
    private bool _bound;
    private bool _visible;

    // ===== UI 元素引用 =====
    private VisualElement _root;
    private VisualElement _storageList;
    private Label _emptyHint;
    private Button _closeButton;

    // 已订阅的 StorageComponent（Open 时收集并订阅，Close 时退订）
    private readonly List<StorageComponent> _subscribed = new List<StorageComponent>();

    // ===== IUIPanel =====

    public void Open(Interactor ctx)
    {
        if (!_bound) Bind();
        _visible = true;
        SubscribeAll();
        Refresh();
        SetVisible(true);
    }

    public void Close()
    {
        _visible = false;
        UnsubscribeAll();
        SetVisible(false);
    }

    public void Refresh()
    {
        if (!_visible) return;
        RebuildStorageList();
    }

    // ===== Unity 生命周期 =====

    private void OnEnable()
    {
        if (!_bound) Bind();
        SetVisible(false);
    }

    private void OnDisable()
    {
        UnsubscribeAll();
        Unbind();
    }

    private void Start()
    {
        if (!_bound) Bind();
    }

    // ===== 订阅 / 退订（DR-15：实时刷新 + 关闭退订防泄漏）=====

    private void SubscribeAll()
    {
        if (BuildingRegistry.Instance == null) return;
        var all = BuildingRegistry.Instance.All;
        for (int i = 0; i < all.Count; i++)
        {
            var b = all[i];
            if (b == null) continue;
            var storage = b.GetComponent<StorageComponent>();
            if (storage == null) continue;
            if (!_subscribed.Contains(storage))
            {
                storage.OnStorageChanged += OnStorageChanged;
                _subscribed.Add(storage);
            }
        }
    }

    private void UnsubscribeAll()
    {
        for (int i = 0; i < _subscribed.Count; i++)
        {
            if (_subscribed[i] != null) _subscribed[i].OnStorageChanged -= OnStorageChanged;
        }
        _subscribed.Clear();
    }

    private void OnStorageChanged(StorageComponent storage)
    {
        if (_visible) Refresh();
    }

    // ===== 列表构建：按资源类型汇总各仓库 =====

    private void RebuildStorageList()
    {
        if (_storageList == null) return;
        _storageList.Clear();

        // 汇总：资源类型 → (存, 容)
        var totals = new Dictionary<ResourceType, (int stored, int cap)>();
        if (BuildingRegistry.Instance != null)
        {
            var all = BuildingRegistry.Instance.All;
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b == null) continue;
                var storage = b.GetComponent<StorageComponent>();
                if (storage == null || storage.capacity <= 0) continue;
                if (totals.TryGetValue(storage.resourceType, out var t))
                    totals[storage.resourceType] = (t.stored + storage.storedAmount, t.cap + storage.capacity);
                else
                    totals[storage.resourceType] = (storage.storedAmount, storage.capacity);
            }
        }

        bool any = false;
        foreach (var kv in totals)
        {
            if (kv.Value.cap <= 0) continue;   // 忽略无容量的条目
            var row = new VisualElement();
            row.AddToClassList("wh-row");

            var name = new Label { text = ResName(kv.Key) };
            name.AddToClassList("wh-row-name");
            row.Add(name);

            var val = new Label { text = $"{kv.Value.stored} / {kv.Value.cap}" };
            val.AddToClassList("wh-row-value");
            row.Add(val);

            _storageList.Add(row);
            any = true;
        }

        if (_emptyHint != null)
            _emptyHint.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;
    }

    // ===== 按钮绑定 / 解绑 =====

    private void Bind()
    {
        if (_bound) return;
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        _root = doc.rootVisualElement;

        _storageList = _root.Q<VisualElement>("wh-storage-list");
        _emptyHint = _root.Q<Label>("wh-empty-hint");
        _closeButton = _root.Q<Button>("wh-close-button");

        if (_closeButton != null) _closeButton.clicked += OnCloseClicked;

        var panel = _root.Q<VisualElement>("wh-panel");
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

    private static string ResName(ResourceType t)
    {
        switch (t)
        {
            case ResourceType.Gold: return "金币";
            case ResourceType.Stone: return "石材";
            case ResourceType.Wood: return "木材";
            case ResourceType.Food: return "食物";
            case ResourceType.Ore: return "矿石";
            case ResourceType.Crystal: return "水晶";
            case ResourceType.FireOil: return "火油";
            case ResourceType.SpecialFood: return "特殊食物";
            case ResourceType.Meat: return "肉";
            default: return t.ToString();
        }
    }

    private void SetVisible(bool visible)
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}