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
    private AttentionTuningConfig _config;

    /// <summary>挂载的编队控制器</summary>
    public FormationController Controller => _controller;

    /// <summary>初始化绑定（FormationController.BindGeneral 自动调用）</summary>
    public void Init(FormationController controller)
    {
        _controller = controller;
        _config = Resources.Load<AttentionTuningConfig>("Config/AttentionTuningConfig");
    }

    private void Update()
    {
        if (_controller == null) return;
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        // M7：决策间隔入训（读 SO config.fbDecisionInterval，未配置回退 Inspector 字段）
        _timer = _config != null ? _config.fbDecisionInterval : decisionInterval;
        Decide();
    }

    /// <summary>意图自决（秒级单次决策，SetIntent 自带 1s 防抖天然节流）。
    /// M1 决策核提取：任务价值 + 意图判定纯函数入核（FormationDecisionCore），壳只做输入采集 + 控制器副作用。</summary>
    private void Decide()
    {
        if (_controller.Anchor == null) return;
        Vector2 anchorPos = _controller.Anchor.position;

        // M7：FormationBrain 内置判定阈值入训（读 SO config，未配置回退 Inspector 字段）
        float engage = _config != null ? _config.fbHeatEngage : heatEngage;
        float charge = _config != null ? _config.fbHeatCharge : heatCharge;
        float retreatGate = _config != null ? _config.fbSurvivalRetreatGate : survivalRetreatGate;
        float searchRadius = _config != null ? _config.fbSupportSearchRadius : supportSearchRadius;
        float maxAge = _config != null ? _config.fbHotspotMaxAge : hotspotMaxAge;

        // 输入：本地热度 / 跨中区块热点 / 存活率 / 任务价值（壳采集，含单例 LODSystem）
        float heat = LODSystem.Instance != null ? LODSystem.Instance.GetHeatAt(anchorPos) : 0f;
        Vector2 hotspot = Vector2.zero;
        bool hasRemoteHotspot = LODSystem.Instance != null
            && LODSystem.Instance.TryGetNearestCombatHotspot(anchorPos, maxAge, searchRadius, out hotspot);
        float survival = _controller.MemberCount / (float)FormationDef.StandardSize;

        // 3.7 §4.3 Sally 输入：城墙健康度 + 最近敌距/位置（守城编队出城迎战判定）
        float wallHpRatio = EvaluateWallHpRatio();
        float enemyDist;
        Vector2 enemyPos;
        EvaluateClosestEnemy(out enemyDist, out enemyPos);

        // 决策核心（核内纯函数，可测试）：
        // ① 残编+被压->撤 ② 远程热点+本地无激战->支援 ③ 高价值+敌压近->冲锋
        // ③.5 守城+城墙健康+敌近->出城迎战(Sally) ④ 敌接近->防守 ⑤ 低热度->维持
        float value = FormationDecisionCore.EvaluateTaskValue(
            _controller.isGarrison,
            _controller.AdvanceTarget != Vector2.zero,
            heat, survival,
            _config != null ? _config.ToSnapshot() : default,
            retreatGate);
        var decision = FormationDecisionCore.DecideIntent(
            heat, survival, value, hasRemoteHotspot,
            _controller.isGarrison, wallHpRatio, enemyDist,
            engage, charge, retreatGate,
            _config != null ? _config.chargeValueGate : 0.6f,
            _config != null ? _config.sallyWallHpGate : 0.5f,
            _config != null ? _config.sallyEnemyDistGate : 20f);

        // 壳执行控制器副作用（推进方向 + 切意图）
        if (decision.ShouldAdvance)
        {
            // Sally：朝最近敌人推进（出城压上）；支援：朝远程战斗热点推进
            Vector2 target = decision.Intent == TacticIntent.Sally && enemyDist < float.MaxValue
                ? enemyPos
                : hotspot;
            _controller.SetAdvanceTarget(target);
        }
        if (decision.Valid)
            _controller.SetIntent(decision.Intent);
    }

    /// <summary>
    /// 3.7 §4.3：城墙健康度（0-1）。场景内 Wall/Gate 单位平均血量比；无城墙返回 1（视为健康）。
    /// 秒级决策调用，FindObjects 扫描开销可接受。
    /// </summary>
    private float EvaluateWallHpRatio()
    {
        float sum = 0f;
        int count = 0;
        var allUnits = FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        foreach (var u in allUnits)
        {
            if (u == null || u.Data == null) continue;
            Occupation occ = u.Data.occupation;
            if (occ != Occupation.Wall && occ != Occupation.Gate) continue;
            sum += u.CurrentHp / (float)Mathf.Max(1, u.MaxHp);
            count++;
        }
        return count > 0 ? sum / count : 1f;
    }

    /// <summary>3.7 §4.3：最近敌方单位距离（世界单位，相对锚点）与位置（Sally 推进目标）。</summary>
    private void EvaluateClosestEnemy(out float dist, out Vector2 pos)
    {
        dist = float.MaxValue;
        pos = Vector2.zero;
        if (_controller.Anchor == null) return;
        Vector2 anchorPos = _controller.Anchor.position;
        var allUnits = FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        foreach (var u in allUnits)
        {
            if (u == null || u.Data == null || u.Data.faction == _controller.faction) continue;
            float d = Vector2.Distance(anchorPos, u.transform.position);
            if (d < dist)
            {
                dist = d;
                pos = u.transform.position;
            }
        }
    }
}
