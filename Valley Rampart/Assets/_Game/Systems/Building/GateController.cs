using UnityEngine;

/// <summary>
/// 城门状态机（3.5.4 §8.4 P1-13 + 2_2 §3.4 改造）。
/// 昼夜自动开关：夜晚开始 -> Closed（不可通行）；白天开始 -> Open（可通行）。
/// 玩家可手动覆盖：切换 Open↔Closed 置 playerOverride=true；昼夜事件不再自动切换。
/// playerOverride 超时重置：每次昼夜事件后玩家若 gateOverrideTimeoutMinutes 分钟未操作 -> 清除覆盖恢复自动。
///
/// 两种宿主模式（2_2）：
///   ① Building 模式（2_2 城门建筑，def.isGate）：开关 = footprint 阻挡切换
///      （Building.SetGateBlocking：关门=BuildingBlocked 阻挡 / 开门=可走），发 GateStateChangedEvent（2_6 repath）。
///   ② UnitController 模式（旧 1D 实体化工事门，挂 FortificationDef）：
///      开关写入 UnitController.FortificationPassableOverride（运行时覆盖，不污染共享 SO），
///      挡移动判定见 UnitController.IsBlockedByFortification。
/// </summary>
public class GateController : MonoBehaviour
{
    /// <summary>城门开合状态（0=关闭=障碍，1=开启=可通行）。</summary>
    public enum GateState { Closed, Open }

    [Header("城门状态（3.5.4 §8.4）")]
    [Tooltip("当前开合状态")]
    public GateState State = GateState.Open;
    [Tooltip("玩家是否手动覆盖（true=玩家控制，昼夜事件不再自动切换）")]
    public bool playerOverride;
    [Tooltip("玩家最后一次手动操作时刻（现实秒，Time.time），用于超时重置")]
    public float lastPlayerActionTime;

    private UnitController _unit;
    private Building _building;

    private void Awake()
    {
        _unit = GetComponent<UnitController>();
        _building = GetComponent<Building>();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<TimePhaseChangedEvent>(OnPhaseChanged);
        ApplyCurrent();   // 初始按当前时段应用（OnEnable 时 CurrentPhase 已就绪）
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<TimePhaseChangedEvent>(OnPhaseChanged);
    }

    private void Update()
    {
        // playerOverride 超时重置（3.5.4 §8.4：每次昼夜事件后玩家 N 分钟未操作 -> 清除覆盖恢复自动）
        if (!playerOverride) return;
        float timeout = GetOverrideTimeoutSeconds();
        if (timeout <= 0f) return;
        if (Time.time - lastPlayerActionTime > timeout)
        {
            playerOverride = false;
            // 恢复自动：按当前时段立即应用一次
            ApplyAutoByPhase(TimeManager.Instance != null ? TimeManager.Instance.CurrentPhase : TimePhase.Day);
            Debug.Log("[GateController] 玩家覆盖超时，恢复昼夜自动开关");
        }
    }

    /// <summary>玩家手动切换 Open ↔ Closed（BuildingPanel/交互调）：置 playerOverride，记录操作时刻。</summary>
    public void ToggleOpenClosed()
    {
        State = State == GateState.Open ? GateState.Closed : GateState.Open;
        playerOverride = true;
        lastPlayerActionTime = Time.time;
        ApplyCurrent();
    }

    /// <summary>时段切换 -> 昼夜自动开关（playerOverride 时跳过）。</summary>
    private void OnPhaseChanged(TimePhaseChangedEvent evt)
    {
        if (playerOverride) return;   // 玩家覆盖时昼夜事件不再自动切换
        ApplyAutoByPhase(evt.NewPhase);
    }

    /// <summary>按时段决定自动开合：夜晚 -> Closed；白天/黎明 -> Open。</summary>
    private void ApplyAutoByPhase(TimePhase phase)
    {
        if (phase == TimePhase.Night)
            State = GateState.Closed;
        else
            State = GateState.Open;
        ApplyCurrent();
    }

    /// <summary>按宿主模式应用当前 GateState（Building=footprint 阻挡切换；Unit=通行覆盖位）。</summary>
    private void ApplyCurrent()
    {
        if (_building != null && _building.def != null && _building.def.isGate)
        {
            // 2_2：关门=阻挡（BuildingBlocked）；开门=可走。occupant 注册不变。
            _building.SetGateBlocking(State == GateState.Closed);
            EventBus.Publish(new GateStateChangedEvent(_building, State == GateState.Open));
            return;
        }
        ApplyToUnit();
    }

    /// <summary>旧 1D 模式：把当前 GateState 写入 UnitController.FortificationPassableOverride（Open=可通行）。</summary>
    private void ApplyToUnit()
    {
        if (_unit == null) return;
        _unit.FortificationPassableOverride = State == GateState.Open;
    }

    /// <summary>覆盖超时时长（秒）：FortificationDef.gateOverrideTimeoutMinutes；未配置回退 5 分钟。</summary>
    private float GetOverrideTimeoutSeconds()
    {
        if (_unit != null && _unit.fortification != null && _unit.fortification.gateOverrideTimeoutMinutes > 0f)
            return _unit.fortification.gateOverrideTimeoutMinutes * 60f;
        return 5f * 60f;
    }
}
