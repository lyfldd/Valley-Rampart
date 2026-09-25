using System;

// ============================================================================
//  `M5-A` 新协议 · 任务卡片（设计依据：`06_任务层.md` §三 五组字段）
//
//  性质：**纯数据 ＋ 状态转移** —— 本文件只依赖 `System`（⛔ 不引用图形引擎命名空间）；卡片 ⛔ 不持任何对象引用。
//  本片边界（硬约束）：
//    · 只新增新协议的数据类型与状态转移逻辑；⛔ **未接入旧生产链**
//      （＝ `06 §八` 迁移三步之①「并存」的**载体**；跑不跑得起来归 `M5-C`）
//    · ⛔ 不动旧调度器／旧任务对象／旧枚举（迁移归 `M5-B`／`M5-C`／`M5-D`）
//    · ⛔ 不建能力目录（归 `M6`）；卡片只持**能力名字符串**（`05 §四` 交互 ID 口径）
//    · `targetRef` ＝ **值数据**（格坐标 ＋ 类型键），⛔ 不持对象引用（`06 §三 :65`）
//  暂定占位（⛔ **非政策裁定** · 见交付报告 §六）：`priority` 默认 `0` ／ `retryMax` 默认 `-1`（未配置）／
//    `onFail` 默认 `Unspecified`。四类待裁政策（`06 §十-2~-5`）本片一律**不填最终值、不设默认行为**。
// ============================================================================

// ───────────────────────────── ① 身份组（`06 §三 :63`）─────────────────────────────

/// <summary>`06 §三` 身份组：派单方类别（结构性最小集；更细来源待 `M5-C` 切换期扩）。</summary>
public enum TaskIssuerKind : byte
{
    Unspecified = 0, // 未填（默认）
    Kingdom = 1,     // 王国（L4 系统层）
    Player = 2,      // 玩家
    System = 3,      // 系统／调试
}

/// <summary>`06 §三` 身份组：派单方（谁派的 · 用于调度与去重）。**值数据**，⛔ 不持对象引用。</summary>
public struct TaskIssuerRef
{
    public TaskIssuerKind kind;
    public int issuerId;   // 派单方编号（口径归 `M5-C` 切换期；0 ＝ 未填）
}

// ───────────────────────────── ② 归属组（`06 §三 :64`）─────────────────────────────

// 归属＝`06 §三`「属于哪个王国／玩家」。⚠️ 本片只落**字段**，⛔ 不定分域粒度（待裁 `06 §十-4`）。
// 口径对齐现码：`kingdomId == 0` ＝ 玩家；`-1` ＝ 无国／中立。

// ───────────────────────────── ③ 做什么组（`06 §三 :65`）───────────────────────────

/// <summary>`06 §三` 目标类别（坐标 ＋ 类型 的「类型」段）。</summary>
public enum TaskTargetKind : byte
{
    None = 0,          // 未填
    Building = 1,      // 建筑
    WorldResource = 2, // 世界资源点（树／石堆／矿脉／水）
    Unit = 3,          // 单位
    Ground = 4,        // 地面／格（如"格翻 Plain"）
}

/// <summary>
/// `06 §三` `targetRef`（**坐标 ＋ 类型** · `:65` 明示「⛔ 不是引用」）。
/// ⭐ **值数据**：全部字段为基元／枚举 ⇒ 双方谁都不持对方引用（`05 §六 :139-153` 配对零引用）。
/// ⚠️ 语义：`cellX`/`cellY` ＝ **格坐标**（非世界坐标）；「坐标 → 还存在吗」的解析走下层的门
///    （`06 §二 :39`），⛔ 卡片不解析、不缓存对象。
/// </summary>
public struct TaskTargetRef
{
    public TaskTargetKind kind;
    public int cellX;
    public int cellY;
    public int typeKey;   // 具体类型键（kind 决定解读；键编码口径归 `M5-C` 切换期，本片不裁）
}

/// <summary>`06 §三` `count`：1 次／N 次／直到满足（`:65`）。</summary>
public enum TaskCountMode : byte
{
    Once = 0,           // 1 次（默认）
    Times = 1,          // N 次（读 `times`）
    UntilSatisfied = 2, // 直到满足（读 `progressSpec`）
}

/// <summary>`06 §三` `count` 载体。</summary>
public struct TaskCountSpec
{
    public TaskCountMode mode;
    public int times;   // 仅 mode=Times 读取；0 ＝ 未填
}

// ─────────────────────────── ④ 条件与进度组（`06 §三 :66`）────────────────────────

/// <summary>`06 §三` `precondition` 判据类别（读对方：不满足 ⇒ 不能做 · `05 §四 :87`）。</summary>
public enum TaskPreconditionMode : byte
{
    None = 0,       // 无条件（默认）
    FieldAtLeast = 1, // 字段 ≥ threshold
    FieldAtMost = 2,  // 字段 ≤ threshold
    FlagSet = 3,      // 标志位为真（threshold ≥ 1 视为真）
}

/// <summary>
/// `06 §三` `precondition` 载体（`06 §三 :66`「⭐ 与 `05` 的『进度』栏同源」）。
/// ⚠️ 取 0..N 条以覆盖 `05 §三 :66` 的 **∧ 复合条件**（如「等级 &lt; 上限 ∧ 资源够」）：
///    **全部成立**才可做；`fieldKey` 的键空间与 `progressSpec` 同源（键编码归 `M5-C`）。
/// </summary>
public struct TaskPrecondition
{
    public TaskPreconditionMode mode;
    public int fieldKey;
    public float threshold;
}

/// <summary>
/// `06 §三` `progressSpec` 完成规则类别 —— 值集**照抄 `05 §四 :95-103` 现码 7 行**的完成规则形态：
/// 计时到（采集／生产）／到达并做完一次（搬运／搬水／装填）／字段到某值（搬料／拆除）。
/// </summary>
public enum TaskProgressMode : byte
{
    FieldReaches = 0,  // 字段 → targetValue（搬料 Remaining→0／拆除 _demolishProgress→1）
    TimerElapsed = 1,  // 计时到卡片 `duration`（采集 gatherSeconds／生产 workDuration）
    ArrivalOnce = 2,   // 到达落点并做完一次（搬运／搬水／装填）
}

/// <summary>`06 §三` `progressSpec`（看哪个字段 → 到什么值）：⛔ 不造通用进度条（`05 §五 :107-125`）。</summary>
public struct TaskProgressSpec
{
    public TaskProgressMode mode;
    public int fieldKey;      // 仅 mode=FieldReaches 读取：看哪个字段（键编码归 `M5-C`）
    public float targetValue; // 仅 mode=FieldReaches 读取：到什么值
}

// ───────────────────────────── ⑤ 结果组（`06 §三 :67`）────────────────────────────

/// <summary>`06 §三` `rewardTo`（产物进谁 · `05 §四 :89`「产物去哪」）。</summary>
public enum TaskRewardSink : byte
{
    None = 0,         // 无产物（默认）
    WorkerCarry = 1,  // 进交互者背包（`05 §二 :50-51` 采集「背包 +5」）
    OwnerTreasury = 2,// 进归属方国库
    OwnerStore = 3,   // 进归属方仓库
    TargetStore = 4,  // 进目标仓（`05 §二 :53` 生产「产物进建筑仓」）
}

/// <summary>`06 §三` `rewardTo` 载体（`06 §三`：回调照着做 · `05 §七 :170`）。</summary>
public struct TaskRewardSpec
{
    public TaskRewardSink sink;
    public int typeKey;   // 资源／产物键（键编码归 `M5-C`）
    public int amount;    // 数量（0 ＝ 按现码惯例由下游决定 · 本片不裁）
}

/// <summary>`06 §三` `targetEffect`（目标怎么变 · `05 §四 :90`「改对方（哪个字段、改多少）」）。</summary>
public enum TaskEffectKind : byte
{
    None = 0,        // 无目标变化（默认）
    FieldDelta = 1,  // 字段 ±value（`05 §二 :50-55` 数量 −5／HP −d／弹仓 +n／建造度 +p）
    FieldSet = 2,    // 字段 ＝ value
    TypeChange = 3,  // 类型变（`05 §二 :52` 满 ⇒ 类型变 · 走 Replace）
    Remove = 4,      // 消失（`05 §二 :50` 空了消失）
    Deposit = 5,     // 入仓（`05 §二 :51/:53` 目标仓 +n）
}

/// <summary>`06 §三` `targetEffect` 载体。</summary>
public struct TaskTargetEffect
{
    public TaskEffectKind kind;
    public int fieldKey;   // 改哪个字段（键编码归 `M5-C`）
    public float value;    // 改多少／改到多少
}

// ───────────────────────────── ⑦ 意外组（`06 §三 :69`）────────────────────────────

/// <summary>`06 §三` `onFail`（换人／放弃／降级 · `:69`）。`Unspecified` ＝ 默认未指定（⛔ 非政策裁定）。</summary>
public enum TaskFailPolicy : byte
{
    Unspecified = 0,   // 未指定（默认 · 待 `M5-C` 按交互表填）
    ReplaceWorker = 1, // 换人
    Abandon = 2,       // 放弃
    Degrade = 3,       // 降级
}

/// <summary>
/// `06 §四` 终止（`[6]`）原因码 —— 值集**逐条对应文档箭头**：
/// `[0] :77` 卡片不合法 ／ `[1] :80-84` 目标没了・目标变了・目标用尽・过期・归属方灭亡 ／
/// `[3] :92-93` 目标没了・目标变了 ／ `[4] :99` 条件已不满足 ／
/// `[5]* 06 §五 :118/:120/:123` 建筑死亡・外部撤回・retry 用尽 ／ `[7] :105` 读档重验不成立。
/// </summary>
public enum TaskAbortReason : byte
{
    None = 0,
    CardInvalid = 1,      // [0] 能力名不存在／目标无效（`06 §四 :77`）
    TargetRemoved = 2,    // [1]/[3] 目标没了（Removed 广播）
    TargetReplaced = 3,   // [1]/[3] 目标变了（Replaced）∧ 重验不匹配
    TargetExhausted = 4,  // [1] 目标用尽（条件不满足）
    Expired = 5,          // [1] 过期（taskExpiry）
    OwnerLost = 6,        // [1] 归属方灭亡 ⇒ 整批作废
    ConditionUnmet = 7,   // [4] 未完成 ∧ 条件已不满足
    BuildingDied = 8,     // `06 §五 :118` 建筑死亡 ⇒ 相关任务作废
    ExternalAbandon = 9,  // `06 §五 :120` 外部撤回
    RestoreRejected = 10, // [7] 读档重验不成立 ⇒ 直接作废（不恢复）
    RetryExhausted = 11,  // `06 §五 :123` retry 用尽 ⇒ 终止
}

/// <summary>
/// `06 §四` 回待派（`[1]`）原因码 —— 逐条对应文档箭头：
/// `[2] :87-89` 工人死 ⇒ 解绑重派 ／ 到不了 ⇒ 解绑＋**标记不可达** ／ `[3] :95` 超时 ⇒ 强制放弃回 [1] ／
/// `[4] :98` 未完成 ∧ 还要继续 ⇒ 回 [1] 重新派。
/// </summary>
public enum TaskUnassignReason : byte
{
    None = 0,
    WorkerDied = 1,  // [2]/[3] 工人死（OnNpcDied）
    Unreachable = 2, // [2]/[3] 路径失败 ⇒ 解绑 ＋ 标记不可达（`06 §五 :123`）
    Timeout = 3,     // [3] 超时（taskTimeout）
    Replan = 4,      // [4] 回 [1] 重新派
}

/// <summary>`06 §四` `⏸` 挂起原因（挂起**保留绑定**·不改变阶段 · `:88`／`:94`）。</summary>
public enum TaskSuspendReason : byte
{
    None = 0,
    ThreatPreempted = 1, // 工人被抢（OnThreatSusp）
}

// ═════════════════════════════════ 任务卡片 ═════════════════════════════════

/// <summary>
/// `M5-A` 新协议任务卡片 —— `06 §三` **五组字段** ＋ `06 §四` 八阶段状态 ＋ **各阶段唯一入口**。
///
/// ⚠️ 本片性质：**只建载体**。⛔ 未接入旧生产链 ⇒ 本类在跑的世界里**零调用点**，
///    因此**不得**据此声称「游戏任务能力已改变」（迁移与接线归 `M5-C`）。
/// ⚠️ 本类 ⛔ 不持任何对象引用（`05 §六 :153`「对象侧零字段」）；配对表／预定（`06 §六`）归 `M5-B`。
/// ⚠️ 所有入口**先判合法性再改状态**：非法转移 ⇒ 返回错误码且**状态零变化**（可机械验证）。
/// </summary>
public sealed class TaskCard
{
    // ── ① 身份（`06 §三 :63`）──
    public long taskId;            // 调度与去重键（编号分配口径归 `M5-C`，本片不裁）
    public TaskIssuerRef issuer;   // 谁派的
    public int priority;           // ⚠️ **暂定占位**（默认 0）：数值口径与「由谁定」＝待裁 `06 §十-2`，本片⛔不算不读

    // ── ② 归属（`06 §三 :64`）──
    public int kingdomId;          // 0 ＝ 玩家；-1 ＝ 无国／中立（对齐现码口径）

    // ── ③ 做什么（`06 §三 :65`）──
    public string ability;         // 能力名／交互 ID（`05 §四 :79` 命名：`对象名_能力名`）
    public TaskTargetRef targetRef;// ⭐ 值数据（坐标 ＋ 类型），⛔ 不是引用
    public TaskCountSpec count;    // 1 次／N 次／直到满足

    // ── ④ 条件与进度（`06 §三 :66`）──
    public TaskPrecondition[] preconditions; // 0..N 条，全成立才可做（∧）
    public TaskProgressSpec progressSpec;    // 看哪个字段 → 到什么值（`05 §四`「进度」栏同源）
    public float duration;                   // 单次动作耗时（秒 · `05 §四 :91`）

    // ── ⑤ 结果（`06 §三 :67`）──
    public TaskRewardSpec rewardTo;    // 产物进谁
    public TaskTargetEffect targetEffect; // 目标怎么变

    // ── ⑥ 执行中（`06 §三 :68` · 派出后才有值）──
    public TaskLifecycleState state;   // 默认 Created（＝ `06 §四 [0]`）
    public int workerId;               // 0 ＝ 未绑定（对齐现码 `npcId != 0` 口径）
    public float workProgress;         // 本次执行进度（语义由该交互的 `progressSpec` 定）
    public float deadline = float.PositiveInfinity; // ∞ ＝ 未设（`taskExpiry`／`taskTimeout` 预算由管理器给）

    // ── ⑦ 意外（`06 §三 :69`）──
    public int retryCount;         // 已用重试次数（现状 0）
    public int retryMax = -1;      // ⚠️ **暂定占位**：-1 ＝ **未配置**（默认次数＝待裁 `06 §十-3`）；⛔ 本片不落默认次数
    public TaskFailPolicy onFail;  // 默认 Unspecified（⛔ 非政策裁定）

    // ── 生命周期辅助栏（非 `06 §三` 五组栏位 · 由状态机维护）──
    public TaskSuspendReason suspendReason;      // `⏸` 挂起标记（挂起不改变阶段）
    public TaskAbortReason abortReason;          // 终止原因（终态写入）
    public TaskUnassignReason lastUnassignReason;// 最近一次回待派原因
    public int unreachableStreak;                // 「标记不可达」计数（`06 §五 :123`）；⚠️ 冷却／重置口径＝待裁 `06 §十-3`
    public TaskLifecycleState stateBeforeRestore;// 进入 Restore 前的阶段（`06 §四 [7]`）

    /// <summary>是否处于 `⏸` 挂起（不改变阶段，仅冻结本次执行）。</summary>
    public bool IsSuspended { get { return suspendReason != TaskSuspendReason.None; } }

    /// <summary>是否终态（`06 §四 [5] Done`／`[6] Aborted`）——终态不可再转移。</summary>
    public bool IsTerminal { get { return TaskLifecycleRules.IsTerminal(state); } }

    // ══════════════════════ 状态转移入口（每个合法箭头一个唯一入口）══════════════════════

    /// <summary>`[0] Created → [1] Pending`：卡片准入（合法）。⚠️ 能力名存在性／目标有效性校验归 `M5-C`
    /// （依赖能力表 · 归 `M6`）⇒ 本片只提供准入动作，⛔ 不做校验本体。</summary>
    public TaskTransitionError Accept()
    {
        return Move(TaskLifecycleState.Pending);
    }

    /// <summary>`[0] Created → [6] Aborted`：卡片不合法（能力名不存在／目标无效）⇒ 丢弃 ＋ 日志（`06 §四 :77`）。</summary>
    public TaskTransitionError Reject(TaskAbortReason reason)
    {
        TaskTransitionError p = Pre(TaskLifecycleState.Aborted);
        if (p != TaskTransitionError.None) return p;
        if (reason == TaskAbortReason.None) return TaskTransitionError.ReasonRequired;
        return Move(TaskLifecycleState.Aborted, reason);
    }

    /// <summary>`[1] Pending → [2] Assigned`：匹配到工人（`06 §四 :86`）。
    /// `deadline &lt;= 0` 或 NaN ⇒ 视为未设（∞）。</summary>
    public TaskTransitionError Assign(int workerId, float deadline)
    {
        TaskTransitionError p = Pre(TaskLifecycleState.Assigned);
        if (p != TaskTransitionError.None) return p;
        if (workerId == 0) return TaskTransitionError.WorkerUnbound;
        TaskTransitionError e = Move(TaskLifecycleState.Assigned);
        if (e != TaskTransitionError.None) return e;
        this.workerId = workerId;
        this.deadline = (float.IsNaN(deadline) || deadline <= 0f) ? float.PositiveInfinity : deadline;
        this.workProgress = 0f;
        this.suspendReason = TaskSuspendReason.None;
        return TaskTransitionError.None;
    }

    /// <summary>`[2] Assigned → [3] Executing`：工人到达（`06 §四 :90`）。</summary>
    public TaskTransitionError Arrive()
    {
        TaskTransitionError p = Pre(TaskLifecycleState.Executing);
        if (p != TaskTransitionError.None) return p;
        if (workerId == 0) return TaskTransitionError.WorkerUnbound;
        return Move(TaskLifecycleState.Executing);
    }

    /// <summary>`[3] Executing → [4] Resolving`：完成一次交互，转判定（`06 §四 :96`）。</summary>
    public TaskTransitionError BeginResolve()
    {
        return Move(TaskLifecycleState.Resolving);
    }

    /// <summary>`[4] Resolving → [3] Executing`：未完成 ∧ 还要继续 ⇒ 回 [3]（`06 §四 :98`）。</summary>
    public TaskTransitionError ContinueExecuting()
    {
        return Move(TaskLifecycleState.Executing);
    }

    /// <summary>`[4] Resolving → [5] Done`：完成 ⇒ 回调 · 结算 · 归档（`06 §四 :101`）。
    /// ⚠️ **释放配对**归 `M5-B`（配对表未建）；本片只清**卡内**绑定。</summary>
    public TaskTransitionError Complete()
    {
        TaskTransitionError e = Move(TaskLifecycleState.Done);
        if (e != TaskTransitionError.None) return e;
        workerId = 0;
        suspendReason = TaskSuspendReason.None;
        return TaskTransitionError.None;
    }

    /// <summary>`{Pending|Assigned|Executing|Resolving} → [6] Aborted`：失败／取消／作废（`06 §四 :82-84/:92-93/:99`）。
    /// ⚠️ 各阶段允许的原因码不同（见 `TaskLifecycleRules.IsLegalAbort`）。</summary>
    public TaskTransitionError Abort(TaskAbortReason reason)
    {
        TaskTransitionError p = Pre(TaskLifecycleState.Aborted);
        if (p != TaskTransitionError.None) return p;
        if (reason == TaskAbortReason.None) return TaskTransitionError.ReasonRequired;
        if (!TaskLifecycleRules.IsLegalAbort(state, reason)) return TaskTransitionError.IllegalReason;
        return Move(TaskLifecycleState.Aborted, reason);
    }

    /// <summary>`{Assigned|Executing|Resolving} → [1] Pending`：解绑回待派。
    /// `Unreachable` ⇒ 追加**标记不可达**计数（`06 §五 :123`）；⚠️ 冷却／重置口径＝待裁 `06 §十-3`。</summary>
    public TaskTransitionError Unassign(TaskUnassignReason reason)
    {
        TaskTransitionError p = Pre(TaskLifecycleState.Pending);
        if (p != TaskTransitionError.None) return p;
        if (reason == TaskUnassignReason.None) return TaskTransitionError.ReasonRequired;
        if (!TaskLifecycleRules.IsLegalUnassign(state, reason)) return TaskTransitionError.IllegalReason;
        if (workerId == 0) return TaskTransitionError.WorkerUnbound;
        if (!TaskLifecycleRules.IsLegalUnassign(state, reason)) return TaskTransitionError.IllegalReason;
        TaskTransitionError e = Move(TaskLifecycleState.Pending);
        if (e != TaskTransitionError.None) return e;
        workerId = 0;
        suspendReason = TaskSuspendReason.None;
        lastUnassignReason = reason;
        if (reason == TaskUnassignReason.Unreachable) unreachableStreak++;
        return TaskTransitionError.None;
    }

    /// <summary>`⏸` `{Assigned|Executing} → 同阶段 + 挂起`：工人被抢（`06 §四 :88/:94`）——**保留绑定**，阶段不变。</summary>
    public TaskTransitionError Suspend(TaskSuspendReason reason)
    {
        TaskTransitionError g = GuardLive();
        if (g != TaskTransitionError.None) return g;
        if (state != TaskLifecycleState.Assigned && state != TaskLifecycleState.Executing)
            return TaskTransitionError.IllegalEdge;
        if (reason == TaskSuspendReason.None) return TaskTransitionError.ReasonRequired;
        if (IsSuspended) return TaskTransitionError.AlreadySuspended;
        suspendReason = reason;
        return TaskTransitionError.None;
    }

    /// <summary>`⏸ → 同阶段`：威胁恢复（`06 §五 :119` 挂起／恢复）。</summary>
    public TaskTransitionError Resume()
    {
        TaskTransitionError g = GuardLive();
        if (g != TaskTransitionError.None) return g;
        if (!IsSuspended) return TaskTransitionError.NotSuspended;
        suspendReason = TaskSuspendReason.None;
        return TaskTransitionError.None;
    }

    /// <summary>`[7]` 入口：`{进行中四阶段} → Restore`（读档 ⇒ **一律重验** · `06 §四 :103-105`）。
    /// ⚠️ 本片**只定义态与接口**，⛔ 不接入存档系统（重验本体由 `ITaskRestoreValidator` 外部提供）。</summary>
    public TaskTransitionError EnterRestore()
    {
        TaskTransitionError g = GuardLive();
        if (g != TaskTransitionError.None) return g;
        if (!TaskLifecycleRules.IsRestorable(state)) return TaskTransitionError.NotRestorable;
        TaskLifecycleState from = state;
        TaskTransitionError e = Move(TaskLifecycleState.Restore);
        if (e != TaskTransitionError.None) return e;
        stateBeforeRestore = from;
        return TaskTransitionError.None;
    }

    /// <summary>`[7] Restore → 原阶段`：重验**成立** ⇒ 恢复（回到存档时阶段 · `06 §四 :104`）。</summary>
    public TaskTransitionError RestoreValidated()
    {
        TaskTransitionError g = GuardLive();
        if (g != TaskTransitionError.None) return g;
        if (state != TaskLifecycleState.Restore) return TaskTransitionError.NoRestoreContext;
        if (!TaskLifecycleRules.IsRestorable(stateBeforeRestore)) return TaskTransitionError.NotRestorable;
        TaskLifecycleState back = stateBeforeRestore;
        TaskTransitionError e = Move(back);
        if (e != TaskTransitionError.None) return e;
        stateBeforeRestore = TaskLifecycleState.Created;
        return TaskTransitionError.None;
    }

    /// <summary>`[7] Restore → [6] Aborted`：重验**不成立** ⇒ 直接作废（**不恢复** · `06 §四 :105`）。</summary>
    public TaskTransitionError RejectRestore(TaskAbortReason reason)
    {
        TaskTransitionError g = GuardLive();
        if (g != TaskTransitionError.None) return g;
        if (state != TaskLifecycleState.Restore) return TaskTransitionError.NoRestoreContext;
        if (reason == TaskAbortReason.None) return TaskTransitionError.ReasonRequired;
        return Move(TaskLifecycleState.Aborted, reason);
    }

    /// <summary>
    /// `retry` 消费口（`06 §五 :123`「retry 用尽 ⇒ 终止」）。
    /// ⚠️ **默认次数＝待裁 `06 §十-3`** ⇒ `retryMax &lt; 0` 时本口**拒绝消费**并返回
    /// `RetryPolicyUnconfigured`（⛔ 不静默按 0 或不限处理）。
    /// ⚠️ 本口只改计数，**不**自动触发终止／重派（编排归 `M5-C`）。
    /// </summary>
    public TaskTransitionError TryConsumeRetry()
    {
        TaskTransitionError g = GuardLive();
        if (g != TaskTransitionError.None) return g;
        if (retryMax < 0) return TaskTransitionError.RetryPolicyUnconfigured;
        if (retryCount >= retryMax) return TaskTransitionError.RetryExhausted;
        retryCount++;
        return TaskTransitionError.None;
    }

    /// <summary>清「不可达」标记（`unreachableStreak`）。⚠️ 清标时机＝待裁 `06 §十-3`；本片只给口，⛔ 不自动调用。</summary>
    public void ClearUnreachableMark()
    {
        unreachableStreak = 0;
    }

    // ───────────────────────────── 内部：唯一改状态点 ─────────────────────────────

    /// <summary>终态守卫（`06 §四 [5]/[6]` 出边为 0）：终态卡片的一切入口**先**返 `TerminalSource`。</summary>
    private TaskTransitionError GuardLive()
    {
        return TaskLifecycleRules.IsTerminal(state) ? TaskTransitionError.TerminalSource : TaskTransitionError.None;
    }

    /// <summary>
    /// 入口前置检查（改状态前）：① 终态守卫 ② **阶段对合法性**。
    /// ⚠️ 次序钉死：阶段合法性 **先于** 载荷检查（原因码／工人号）⇒ 拒绝码语义可预期：
    /// 非法阶段对 ⇒ `IllegalEdge`／`TerminalSource`；阶段对合法但载荷不合法 ⇒ `IllegalReason`／`WorkerUnbound`／`ReasonRequired`。
    /// </summary>
    private TaskTransitionError Pre(TaskLifecycleState to)
    {
        TaskTransitionError g = GuardLive();
        if (g != TaskTransitionError.None) return g;
        return TaskLifecycleRules.Validate(state, to);
    }

    /// <summary>唯一状态写入点：先验合法性，非法 ⇒ 零变化并返回错误码。</summary>
    private TaskTransitionError Move(TaskLifecycleState to, TaskAbortReason abortReason = TaskAbortReason.None)
    {
        TaskTransitionError e = TaskLifecycleRules.Validate(state, to);
        if (e != TaskTransitionError.None) return e;
        state = to;
        if (to == TaskLifecycleState.Aborted)
        {
            this.abortReason = abortReason;
            workerId = 0;
            suspendReason = TaskSuspendReason.None;
        }
        return TaskTransitionError.None;
    }
}

/// <summary>
/// `06 §四 [7]` **重验接口**（读档：目标在吗？工人在吗？条件成立吗？）。
/// ⚠️ 本片**只定义接口**，⛔ 不接入存档系统、⛔ 无实现（实现归 `M5-C`／存档面）。
/// </summary>
public interface ITaskRestoreValidator
{
    /// <summary>重验一张待恢复卡片：成立 ⇒ true；不成立 ⇒ false ＋ 给出作废原因（`06 §四 :105`）。</summary>
    bool Revalidate(TaskCard card, out TaskAbortReason rejectReason);
}
