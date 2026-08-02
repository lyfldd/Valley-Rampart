using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - 因子上下文
//  详见 3.0.1_2_输入输出决定层设计.md §9
//  承载四段状态：⓪ 世界/自身原始状态 -> ② 记忆组件写入 -> ③ 管线中间产物
//  纯管线只读消费，记忆组件只写自己的字段段
// ============================================================================

/// <summary>
/// 因子上下文：三层裁决管线的统一数据载体（§9）。
/// NPCBrain ⓪ 组装世界/自身原始状态 -> ① 记忆组件 Tick -> ② FillContext 写入 -> ③ 管线只读消费。
/// </summary>
public struct FactorContext
{
    // ===== ⓪ NPCBrain 组装：世界/自身原始状态（只读）=====
    public IDamageable Self;
    public NpcProfessionDef Profession;
    public AttentionTuningConfig Config;
    public Vector2 SelfPos;
    public float HpRatio;
    public bool IsNight;
    /// <summary>夜晚因子 0-1（黄昏/黎明渐变，用于 SafetyStimulus 放大）</summary>
    public float NightFactor;
    public int NearbyEnemyCount;
    public int NearbyAllyCount;
    public float NearestEnemyDist;
    public float PerceptionWorldRadius;
    public float AttackWorldRange;
    public float CellSize;
    public float CurrentTime;
    public Vector2 HomePoint;
    /// <summary>brain 上一帧 rawFactor 缓存（量化器消费，0.1s 滞后被确认吸收）</summary>
    public float LastRaw;
    /// <summary>BehaviorExecutor 反馈：是否到达焦点目标</summary>
    public bool ArrivedAtFocus;
    /// <summary>3.0.1_LOD §3.2 区块威胁热度（环境型威胁因子，NPCBrain 从 LODSystem 读）</summary>
    public float RegionHeat;

    // ===== 3.0.1_3 编队槽位（守阵追击 clamp 用，§4.1）=====
    /// <summary>编队槽位世界坐标（锚点+SlotOffset×cellSize）；非编队成员=zero</summary>
    public Vector2 FormationSlotWorld;
    /// <summary>是否编队成员（有槽位绑定）</summary>
    public bool HasFormationSlot;

    // ===== 3.0.1_8 综合因子（分层仲裁，连续 0-1，不压档）=====
    /// <summary>威胁因子（连续 0-1）：距离/数量/战力/溯源/集火/夜间 的加权和（小因子→综合因子耦合 OK）</summary>
    public float ThreatFactor;
    /// <summary>协作因子（连续 0-1）：编队军令约束强度（有编队≈1，无编队=0）。切阵型=此因子提高</summary>
    public float FormationFactor;
    /// <summary>归巢因子（连续 0-1）：离家/夜晚/受伤 加权合成（3.0.1_8 §五）。撤退 = 威胁压过协作 AND 归巢驱力强（编队成员）</summary>
    public float SafetyFactor;
    /// <summary>放弃任务因子（连续 0-1）：追击成本>收益 时升高（3.0.1_8 §六）。高 → 放弃追击回归编队（防风筝）</summary>
    public float AbandonTaskFactor;
    /// <summary>工作因子（连续 0-1）：当前任务投入强度（3.0.1_8 §八，焦点=TaskStimulus 按优先级归一化）。高 → L2 抗打断</summary>
    public float WorkFactor;

    // ===== ② 记忆组件 FillContext 写入 =====
    public HitCooldownState CurrentState;
    public int HitCount;
    /// <summary>Caution 态威胁加成（叠加到 rawFactor，目标落威胁 1 区间 [0.25,0.5) 保持警戒）</summary>
    public float StateThreatBias;
    /// <summary>Caution 态任务类刺激折扣（让 HoldPosition 胜出）</summary>
    public float StateTaskDiscount;
    /// <summary>有效敏感度（Probe 态 ×1.5）</summary>
    public float EffectiveSensitivity;
    /// <summary>ThreatHysteresisComponent 输出：威胁等级 0-3</summary>
    public ThreatLevel ThreatLevel;
    /// <summary>ProtectionHysteresisComponent 输出：是否有友军保护</summary>
    public bool HasProtection;

    // ===== ③ 管线中间产物 =====
    /// <summary>L3 算完后写，下 tick 量化器消费</summary>
    public float RawFactor;
    public FocusDecision FocusDecision;
    public PostureDecision PostureDecision;

    /// <summary>Caution 态判定（便捷访问）</summary>
    public bool IsCaution => CurrentState == HitCooldownState.Caution;
}
