using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 王国情报面板（2_13 批D 四职责承接②：2_15 §4.1 / D287 可视化三层——点开 AI 王国看数据）。
/// 字段（2_15 契约）：人口/军力/资源/关系/剧本阶段；另承接①幸福可见性（0.6 §三十五附带登记①
/// "幸福三层惩罚可见性→2_13 UI 层加反馈不加系统"）——以幸福值+三惩罚因子数值可见承接，
/// 阈值越限播报/图标形式让渡请裁决（HH.46）。
/// 入口：列国名单行点击（KingdomListPanel.OpenKingdomIntel）；点选 AI 王国实体入场路径让渡
/// （SelectionController 点选只收己方，实体点击入口归交互层后续）。
/// 让渡登记（HH.46）：①关系列——外交系统未建（2_18 域）；②剧本阶段列——ScriptStageMachine 在场
/// 但 KingdomState 未携带阶段字段，接线归 2_17 域增量；③AI 国幸福值——per-kingdom 幸福 getter
/// 未公开（仅三因子 API 公开），归 2_17 域；④战争迷雾可见度口径归 2_18/2_13 细化（本面板现为全知）。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class KingdomIntelPanel : MonoBehaviour, IUIPanel
{
    private int _kingdomId = -1;
    private bool _bound;
    private bool _visible;

    private Label _name;
    private Label _days;
    private Label _castle;
    private Label _pop;
    private Label _military;
    private Label _resources;
    private Label _happiness;
    private Label _taxFactor;
    private Label _growthFactor;
    private Label _moraleFactor;
    private Label _relation;
    private Label _stage;
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

    public void Refresh() => Fill();

    // ===== 对外 API =====

    /// <summary>指定展示的王国（列国名单行点击调用；须在 Push 之前）。</summary>
    public void ShowKingdom(int kingdomId)
    {
        _kingdomId = kingdomId;
        if (_visible) Fill();
    }

    // ===== 内部 =====

    private void Fill()
    {
        var state = (_kingdomId >= 0 && KingdomRegistry.Instance != null)
            ? KingdomRegistry.Instance.Get(_kingdomId)
            : null;
        var tm = TimeManager.Instance;
        var hs = HappinessSystem.Instance;

        Set(_name, state != null ? state.name : "未知王国");
        Set(_days, state != null && tm != null ? $"第 {Mathf.Max(1, tm.CurrentDay - state.foundedDay + 1)} 天" : "—");
        Set(_castle, state != null ? $"Lv {state.castleLevel}" : "—");
        Set(_pop, state != null && PopulationSystem.Instance != null ? state.workerCount.ToString() : "—");
        Set(_military, state != null && PopulationSystem.Instance != null ? state.warriorCount.ToString() : "—");
        Set(_resources, state != null
            ? $"金{state.resources.gold} 木{state.resources.wood} 石{state.resources.stone} 粮{state.resources.food} 铁{state.resources.metal}"
            : "—");
        // 幸福：玩家实值；AI per-kingdom 幸福 getter 未公开 → "—"（让渡 2_17 域）
        Set(_happiness, state != null && state.IsPlayer && hs != null ? $"{hs.OverallHappiness:F0}" : "—");
        // 幸福三惩罚因子（0.6 §三十五登记① 承接：税收减少/人口增长减少/士气低，per-kingdom API 现役）
        Set(_taxFactor, state != null && hs != null ? $"×{hs.GetTaxCoefficient(state.id):F2}" : "—");
        Set(_growthFactor, state != null && hs != null ? $"×{hs.GetPopulationGrowthFactor(state.id):F2}" : "—");
        Set(_moraleFactor, state != null && hs != null ? $"×{hs.GetRetreatThresholdModifier(state.id):F2}" : "—");
        Set(_relation, "—");    // 让渡：外交系统未建（2_18 域）
        Set(_stage, "—");       // 让渡：剧本阶段字段未接 KingdomState（2_17 域增量）
    }

    private static void Set(Label label, string text)
    {
        if (label != null) label.text = text;
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
        if (!_bound) Bind();
        SetVisible(false);
    }

    private void Bind()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        var root = doc.rootVisualElement;

        _name = root.Q<Label>("intel-name");
        _days = root.Q<Label>("intel-days");
        _castle = root.Q<Label>("intel-castle");
        _pop = root.Q<Label>("intel-pop");
        _military = root.Q<Label>("intel-military");
        _resources = root.Q<Label>("intel-resources");
        _happiness = root.Q<Label>("intel-happiness");
        _taxFactor = root.Q<Label>("intel-tax-factor");
        _growthFactor = root.Q<Label>("intel-growth-factor");
        _moraleFactor = root.Q<Label>("intel-morale-factor");
        _relation = root.Q<Label>("intel-relation");
        _stage = root.Q<Label>("intel-stage");
        _closeButton = root.Q<Button>("kingdom-intel-close-button");
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
