using UnityEngine;

// ============================================================================
//  3.0.1_5 多将军协作与管理 - FormationBrain 军队级大脑（编队自主协作核心）
//  详见 3.0.1_5_多将军协作与管理方向.md §四
//  独立军队级组件，挂将军（或锚点）。意图自决：攻/守/撤/支援（不等君主下令）。
//  与个体 NPCBrain 完全解耦：士兵个体复用现有三层管线零改动，军队靠本组件自决。
//  决策节奏秒级（意图切换/阵型/价值评估），个体 0.1s 决定打谁/撤不撤。
// ============================================================================

/// <summary>
/// 军队级大脑（§4.1）。
/// 输入：ThreatHeat 热点分布（本地 + 跨中区块支援扫描）/ 编队存活率 / 任务价值。
/// 输出：写 FormationController（SetIntent 切意图 + SetAdvanceTarget 推进方向）。
///
/// 意图自决（§4.4 防看戏）：
///   ① 残编 + 被压 → 撤退（先保住有生力量）
///   ② 远处战斗热点（本地无激战）→ 支援（编队 B 支援编队 A：朝热点推进）
///   ③ 高价值 + 敌压近 → 冲锋压上（军队敢承受代价）
///   ④ 敌接近 → 防守
///   ⑤ 低热度 → 维持现状（不频繁切换）
///
/// 任务价值动态评估（§4.2）：锚点类型定基础值（攻城中高/守城中中/巡逻低）
/// + 动态修正（敌压近升价值 / 残编降价值）。价值高 → 军令压住个体撤退冲动。
/// </summary>
public class FormationBrain : MonoBehaviour
{
    [Header("决策节奏（秒级：意图切换/阵型/价值评估）")]
    [Tooltip("决策间隔（秒）。军队定位置+意图+价值（秒级），个体定打谁+撤不撤（0.1s）")]
    public float decisionInterval = 1f;

    [Header("支援感知（§4.1 跨编队协作）")]
    [Tooltip("支援搜索半径（世界单位）：此半径内出现战斗热点且本地无激战 → 切支援")]
    public float supportSearchRadius = 30f;
    [Tooltip("热点有效期（秒）：超过则不再朝旧热点移动")]
    public float hotspotMaxAge = 5f;

    [Header("意图阈值")]
    [Tooltip("本地威胁热度 ≥ 此值视为敌压近（防守/撤退判定线）")]
    public float heatEngage = 0.3f;
    [Tooltip("本地威胁热度 ≥ 此值且任务价值高 → 冲锋压上")]
    public float heatCharge = 0.6f;
    [Tooltip("残编率 < 此值且被压 → 撤退（保住有生力量）")]
    public float survivalRetreatGate = 0.4f;

    private FormationController _controller;
    private float _timer;

    /// <summary>挂载的编队控制器</summary>
    public FormationController Controller => _controller;

    /// <summary>初始化绑定（FormationController.BindGeneral 自动调用）</summary>
    public void Init(FormationController controller)
    {
        _controller = controller;
    }

    private void Update()
    {
        if (_controller == null) return;
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = decisionInterval;
        Decide();
    }

    /// <summary>意图自决（秒级单次决策，SetIntent 自带 1s 防抖天然节流）</summary>
    private void Decide()
    {
        if (_controller.Anchor == null) return;
        Vector2 anchorPos = _controller.Anchor.position;

        // 输入：本地热度 / 跨中区块热点 / 存活率 / 任务价值
        float heat = LODSystem.Instance != null ? LODSystem.Instance.GetHeatAt(anchorPos) : 0f;
        Vector2 hotspot = Vector2.zero;
        bool hasRemoteHotspot = LODSystem.Instance != null
            && LODSystem.Instance.TryGetNearestCombatHotspot(anchorPos, hotspotMaxAge, supportSearchRadius, out hotspot);
        float survival = _controller.MemberCount / (float)FormationDef.StandardSize;
        float value = EvaluateTaskValue(heat, survival);

        // ① 残编 + 被压 → 撤退（先保住有生力量）
        if (survival < survivalRetreatGate && heat > heatEngage)
        {
            _controller.SetIntent(TacticIntent.Retreat);
            return;
        }

        // ② 远处战斗热点 + 本地无激战 → 支援（编队 B 支援编队 A：朝热点推进）
        if (hasRemoteHotspot && heat < heatEngage)
        {
            _controller.SetAdvanceTarget(hotspot);
            _controller.SetIntent(TacticIntent.Charge);
            return;
        }

        // ③ 高价值 + 敌压近 → 冲锋压上（军队敢承受代价：任务价值高，个体撤退被军令压住）
        if (heat > heatCharge && value > 0.6f)
        {
            _controller.SetIntent(TacticIntent.Charge);
            return;
        }

        // ④ 敌接近 → 防守
        if (heat > heatEngage)
        {
            _controller.SetIntent(TacticIntent.Defense);
        }
        // ⑤ 低热度 → 维持现状（不频繁切换）
    }

    /// <summary>
    /// 任务价值动态评估（§4.2）：锚点类型定基础值 + 动态修正。
    /// 基础值：守城编队中（0.5）/ 将军有推进目标=攻城中高（0.8）/ 无目标=巡逻低（0.2）。
    /// 动态：敌压近升价值（战斗紧迫），残编降价值（保命优先）。
    /// </summary>
    private float EvaluateTaskValue(float heat, float survival)
    {
        float baseValue;
        if (_controller.isGarrison)
            baseValue = 0.5f;   // 守城中：固守待敌，被打狠才撤
        else if (_controller.AdvanceTarget != Vector2.zero)
            baseValue = 0.8f;   // 攻城中：带伤推进，个体撤退阈值被编队抵抗抬高
        else
            baseValue = 0.2f;   // 巡逻/待命：一触即撤，不恋战

        if (heat > 0.5f) baseValue += 0.2f;
        if (survival < survivalRetreatGate) baseValue -= 0.3f;
        return Mathf.Clamp01(baseValue);
    }
}
