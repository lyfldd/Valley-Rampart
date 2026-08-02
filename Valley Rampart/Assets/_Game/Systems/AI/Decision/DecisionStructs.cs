using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - 三层裁决管线数据结构
//  详见 3.0.1_2_输入输出决定层设计.md §9
//  L1 输出 FocusDecision -> L2 输出 PostureDecision -> L3 输出 BehaviorCommand
//  全部 struct 零 GC，符合纯计算管线契约
// ============================================================================

/// <summary>
/// L1 焦点评分层输出（§9）。
/// 五层压制 + 层内强度排序 -> 焦点（目标位置 + 刺激类型）。
/// </summary>
public struct FocusDecision
{
    /// <summary>焦点刺激源（struct 刺激赋值到接口会装箱 1 次/tick，P0 接受，P1 tagged union 优化）</summary>
    public IStimulus Focus;
    /// <summary>焦点类型，L2 三维表查表用</summary>
    public FocusType Type;
    /// <summary>焦点位置（FollowAnchor 时每 tick 刷新）</summary>
    public Vector2 TargetPos;
    /// <summary>层内强度分</summary>
    public float Score;
    /// <summary>是否有效</summary>
    public bool IsValid;

    public static FocusDecision Invalid => default;
}

/// <summary>
/// L2 姿态裁决层输出（§9）。
/// 谱系 × 焦点类型 × 到达态 -> 行为模块 + 参数来源。
/// </summary>
public struct PostureDecision
{
    public FocusDecision Focus;
    /// <summary>谱系 0/2/4（复用现有 BehaviorSpectrum 枚举，1/3 留口，不破坏 AISwitchRecord）</summary>
    public BehaviorSpectrum Spectrum;
    /// <summary>选定的行为模块</summary>
    public BehaviorModule Module;

    /// <summary>
    /// 移动目标：
    /// 谱系 4 战略 = HomePoint（位置）；
    /// 谱系 4 战术 = 受击反方向**单位向量**（L2 算不出落点，只定方向；L3 用它×retreatDistance 算 BehaviorCommand.TargetPos）。
    /// 战术方向来源：活跃 ThreatStimulus.enemy 位置反方向（enemy ref 母文档 §2.2 已有，P0 不等受击方向记忆）。
    /// </summary>
    public Vector2 MoveTarget;

    /// <summary>谱系 4 子裁决结果：true=战术短撤（MoveTarget=方向，L3 填 Distance）/ false=战略（MoveTarget=HomePoint 位置）</summary>
    public bool IsTacticalRetreat;

    /// <summary>战术短撤方向来源敌人引用（ThreatStimulus.Enemy 反方向），null=战略撤退</summary>
    public IDamageable TacticalRetreatEnemy;
}

/// <summary>
/// L3 参数计算层输出 -> BehaviorExecutor（§9）。
/// 因子公式 -> 速度/距离/时长/阈值等连续参数。
/// </summary>
public struct BehaviorCommand
{
    public BehaviorModule Module;
    public Vector2 TargetPos;

    /// <summary>仅战术短撤用：远离受击方向（IsTacticalRetreat=true）</summary>
    public Vector2 Direction;
    /// <summary>仅战术短撤用：retreatDistance（撞墙/到达即停）</summary>
    public float Distance;

    // 注：战术短撤时 Executor 走 Direction+Distance 分支（撞墙停语义）；
    // TargetPos（= self.pos + Direction×Distance，L3 填）仅供调试面板显示预期落点

    /// <summary>仅 FollowAnchor 用</summary>
    public UnitController Anchor;
    public float Speed;
    /// <summary>警戒/驻留时长</summary>
    public float Duration;
    /// <summary>仅跟随用</summary>
    public float KeepDistance;

    // ===== 3.0.1_3 编队槽位（FollowAnchor 槽位化 + 守阵追击 clamp 用）=====

    /// <summary>
    /// 槽位偏移（cell 单位，FollowStimulus.SlotOffset 透传）。
    /// 非 zero 时 ExecuteFollowAnchor 走槽位化语义：目标 = 锚点位置 + SlotOffset × cellSize。
    /// </summary>
    public Vector2Int SlotOffset;

    /// <summary>
    /// 槽位世界坐标（锚点位置 + SlotOffset × cellSize，L3 算好填）。
    /// 守阵追击 clamp 用：MoveTowards 目标被钳制在 slotWorld ± chaseRange 内（§4.1）。
    /// </summary>
    public Vector2 SlotWorld;

    /// <summary>是否编队槽位化（SlotOffset 非 zero）</summary>
    public bool IsFormationSlot => SlotOffset.x != 0 || SlotOffset.y != 0;

    // ===== 3.3.5 资源流转：搬运目标（WorkAt 模块用）=====

    /// <summary>
    /// 搬运目标（IHarvestable）。TaskStimulus.Source 为 StorageComponent 时透传，
    /// Executor.WorkAt 到达目标后调用 Harvest() 把本地存储转国库（自动搬运闭环）。
    /// 非搬运任务（普通工作）为 null。
    /// </summary>
    public IHarvestable HarvestTarget;

    // ===== 3.0.1_4 §6.3 漫游（Wander 模块用）=====

    /// <summary>
    /// 漫游半径（世界单位，L3 算 = wanderRadiusCells × cellSize）。
    /// Executor 在 cmd.TargetPos（= HomePoint 漫游中心）周围取随机点。
    /// </summary>
    public float WanderRadius;
}
