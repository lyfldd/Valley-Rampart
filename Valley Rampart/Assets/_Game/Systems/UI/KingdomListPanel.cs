using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 列国名单面板（2_13 批D 四职责承接③）。
/// 指派源：2_16 D305（开局汇总播报"本大陆已有 N 国并存"，点击展开列国名单归 2_13）；
///         2_10 D452（名单点击→临时显色+高亮+聚焦）。
/// 数据源：KingdomRegistry.GetAll()（2_16 步骤1 现役，只读）。
/// 行为：行点击 → FocusOnKingdom 聚焦 + OpenKingdomIntel 推送情报面板。
/// 让渡登记（HH.46）：①染色高亮（D452 HighlightKingdom 半边）——TerritoryOverlay（2_10 步骤13）未实施，
/// 接口位预留（2_13实施 L137）；②"播报点击展开"入口——ToastManager 点击回调扩展未建，
/// 现行入口=TopLeftHUD「列国」按钮；③AI 抽象国（无实体）聚焦跳过。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class KingdomListPanel : MonoBehaviour, IUIPanel
{
    private bool _bound;
    private bool _visible;
    private ScrollView _list;
    private Label _emptyHint;
    private Button _closeButton;

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
    }

    /// <summary>重建名单行（打开时/立国事件时调用）。</summary>
    public void Refresh()
    {
        if (_list == null) return;
        _list.Clear();
        if (KingdomRegistry.Instance == null)
        {
            if (_emptyHint != null) _emptyHint.style.display = DisplayStyle.Flex;
            return;
        }
        var kingdoms = KingdomRegistry.Instance.GetAll();
        if (_emptyHint != null) _emptyHint.style.display = kingdoms.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        for (int i = 0; i < kingdoms.Count; i++)
            _list.Add(BuildRow(kingdoms[i]));
    }

    // ===== 对外 API（行点击真路径；D305 播报点击展开将来复用同一入口）=====

    /// <summary>聚焦某王国：玩家=主城锚点（WorldManager.GetKingdomAnchorWorld）；AI=该国任一存活单位；无实体=跳过记日志。</summary>
    public void FocusOnKingdom(int kingdomId)
    {
        var rig = Object.FindObjectOfType<CameraRig>();
        if (rig == null) { Debug.LogWarning("[KingdomListPanel] CameraRig 不在场，聚焦跳过"); return; }

        Vector2? pos = null;
        if (kingdomId == 0 && WorldManager.Instance != null)
            pos = WorldManager.Instance.GetKingdomAnchorWorld();
        if (!pos.HasValue)
        {
            var units = Object.FindObjectsOfType<UnitController>();
            for (int i = 0; i < units.Length; i++)
            {
                var u = units[i];
                if (u != null && u.kingdomId == kingdomId && u.IsAlive) { pos = u.transform.position; break; }
            }
        }

        if (pos.HasValue)
        {
            rig.FocusOn(pos.Value);
            Debug.Log($"[KingdomListPanel] 聚焦王国 {kingdomId} → ({pos.Value.x:F1}, {pos.Value.y:F1})");
        }
        else
        {
            Debug.Log($"[KingdomListPanel] 王国 {kingdomId} 暂无实体可聚焦（抽象国聚焦让渡；染色高亮随 TerritoryOverlay 承接）");
        }
    }

    /// <summary>打开某王国情报面板（行点击后半程；情报承接④）。</summary>
    public void OpenKingdomIntel(int kingdomId)
    {
        var intel = Object.FindObjectOfType<KingdomIntelPanel>();
        if (intel == null)
        {
            Debug.LogWarning("[KingdomListPanel] 未找到 KingdomIntelPanel（场景缺 KingdomIntelUI 挂载）");
            return;
        }
        intel.ShowKingdom(kingdomId);
        UIManager.Instance?.Push(intel, new Interactor(Faction.PlayerCamp, Vector3.zero));
    }

    // ===== 内部 =====

    private VisualElement BuildRow(KingdomState state)
    {
        var row = new Button { name = $"kingdom-row-{state.id}" };
        row.AddToClassList("kingdom-row");

        var swatch = new VisualElement();
        swatch.AddToClassList("kingdom-swatch");
        swatch.style.backgroundColor = state.bannerColor;
        row.Add(swatch);

        var label = new Label(ComposeRowText(state));
        label.AddToClassList("kingdom-row-label");
        row.Add(label);

        int captured = state.id;    // 闭包捕获
        row.clicked += () => OnRowClicked(captured);
        return row;
    }

    private string ComposeRowText(KingdomState state)
    {
        int days = TimeManager.Instance != null
            ? Mathf.Max(1, TimeManager.Instance.CurrentDay - state.foundedDay + 1)
            : 1;
        string forces = PopulationSystem.Instance != null
            ? $"工{state.workerCount} 战{state.warriorCount}"
            : "人口 —";
        return $"{state.name} · {forces} · 城堡{state.castleLevel} · 存续 {days} 天";
    }

    private void OnRowClicked(int kingdomId)
    {
        FocusOnKingdom(kingdomId);
        OpenKingdomIntel(kingdomId);
    }

    private void OnKingdomFounded(KingdomFoundedEvent evt)
    {
        if (_visible) Refresh();    // 动态立国 → 名单即时刷新（KingdomRegistry 只发此一事件）
    }

    private void OnCloseClicked()
    {
        if (UIManager.Instance != null && ReferenceEquals(UIManager.Instance.Peek(), this))
            UIManager.Instance.CloseCurrent();
        else
            SetVisible(false);
    }

    // ===== 生命周期 / 绑定 =====

    private void OnEnable()
    {
        EventBus.Subscribe<KingdomFoundedEvent>(OnKingdomFounded);
        if (!_bound) Bind();
        SetVisible(false);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<KingdomFoundedEvent>(OnKingdomFounded);
        if (!_bound) return;
        if (_closeButton != null) _closeButton.clicked -= OnCloseClicked;
        _bound = false;
    }

    private void Bind()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        var root = doc.rootVisualElement;

        _list = root.Q<ScrollView>("kingdom-list");
        _emptyHint = root.Q<Label>("kingdoms-empty");
        _closeButton = root.Q<Button>("kingdom-list-close-button");
        if (_closeButton != null) _closeButton.clicked += OnCloseClicked;

        _bound = true;
    }

    private void SetVisible(bool visible)
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
