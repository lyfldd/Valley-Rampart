using UnityEngine;

// ============================================================================
//  野性敌意配置（2_20 §十二 D468，HH.51 种族1 批C；so-data-driven 铁律：数值全 SO 化）
//  强度闸门（用户拍板 SO 占位+实测回调）：无国×异族（含国民）无条件攻击的强度参数全部走本 SO。
//  传送门怪物（Faction.Monster）不进种族矩阵（2_14 现行不变，D428 阵营解耦不动）——本 SO 不含怪物参数。
//  资产路径：Resources/Config/WildnessConfig.asset（探针⑤ Resources.Load 路径探针同路径）。
// ============================================================================

[CreateAssetMenu(menuName = "ValleyRampart/WildnessConfig")]
public class WildnessConfig : ScriptableObject
{
    [Header("全局开关（默认开；关闭=零野性攻击，探针⑤负向）")]
    public bool enabled = true;

    [Header("野性索敌半径（格单位）——D477 勘定 2026-09-02：全库距离约定=doc1 §1.6 格单位，\n微格域仅寻路/阻挡无感官语义，原「8 微格」系笔误；禁用微格域（HH.51 解禁注红线）")]
    public float wildAggroRadiusCells = 8f;

    [Header("野人战力=同职工人 60%（占位，Play 实测回调）：攻击力=Worker 基线×本系数，射程/冷却镜像 Worker 基线")]
    [Range(0.1f, 1f)]
    public float wildStrengthRatio = 0.6f;

    /// <summary>加载（Resources.Load 路径=探针⑤；缺资产 → 调用方按"关闭"处理，不产生野性攻击）。</summary>
    public static WildnessConfig Load() => Resources.Load<WildnessConfig>("Config/WildnessConfig");

    /// <summary>
    /// 野性是否生效（开关开 + 资产在场）。全局守卫单一入口，调用方禁止各自判 null。
    /// </summary>
    public static bool IsActive => _cached != null && _cached.enabled;
    private static WildnessConfig _cached;

    /// <summary>带缓存的加载（Resources.Load 幂等，缓存防逐 tick 查找）。</summary>
    public static WildnessConfig Cached
    {
        get
        {
            if (_cached == null) _cached = Load();
            return _cached;
        }
    }

    /// <summary>
    /// Worker 战力基线（野人战力标定基准=同职工人）：运行时查 PlayerCamp_Worker 职业资产。
    /// 查表失败返回 null（调用方回退：不产生野性攻击）。
    /// 实盘缺口注（HH.51 验收）：Worker 资产 attack/attackRange/attackCD=0（和平职业正常值），
    /// 「×60%」公式退化 → 调用方按 Max 下限兜底（attack≥1/range≥1/cd≥0.5，行为硬规则 D468 落地优先）；
    /// 数值待 Play 回调——改 Worker 资产 or 本 SO 增绝对基线字段，策划端拍板。
    /// </summary>
    public static NpcProfessionDef ResolveWorkerBaseline()
    {
        var data = UnitDataManager.Instance != null
            ? UnitDataManager.Instance.GetData(Faction.PlayerCamp, Occupation.Worker)
            : null;
        return data as NpcProfessionDef;
    }
}
