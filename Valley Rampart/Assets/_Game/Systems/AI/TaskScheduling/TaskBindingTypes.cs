using System;

// ============================================================================
//  `M5-B` 新协议 · 配对与预定的值类型（设计依据：`06_任务层.md` §六 配对与预定）
//
//  性质：**纯值数据** —— 本文件只依赖 `System`（⛔ 不引用图形引擎命名空间）；⛔ 不持任何运行时对象引用。
//  分工：本文件只放**值类型**；两张表与释放规则在 `TaskBindingManager`。
//  文档锚：`06 §六 :128-134`（配对表＝唯一真源 · ⭐「任务在 ⇒ 预留在」「任务亡 ⇒ 预定立刻释放」＋ 超时兜底）
//          `05 §六 :139-153`（配对只在管理器 · 双方零引用 · 对象侧零字段）
//          `05 §八 :177-195`（「可多人」是**对象侧字段** ⇒ 本片只作**入参**，⛔ 不在管理器内造策略）
// ============================================================================

/// <summary>
/// `06 §六` 配对/预定操作的返回码（`Ok` ＝ 唯一成功值）。
/// ⚠️ **幂等**与**冲突**分列（⛔ 不复用同码）：幂等 ⇒ `AlreadyReserved`／`AlreadyPaired`／`NotReserved`／`NotPaired`
///    （**零变化**）；冲突 ⇒ `ReservationConflict`／`WorkerBusy`／`PairConflict`（拒绝且**零变化**）。
/// </summary>
public enum TaskBindingResult : byte
{
    Ok = 0,                  // 成功
    AlreadyReserved = 1,     // 幂等：同任务同目标已有预定
    AlreadyPaired = 2,       // 幂等：同任务同工人已配对
    ReservationConflict = 3, // 冲突：该目标已被**他任务**独占预定（本目标不许多人）
    WorkerBusy = 4,          // 冲突：该工人已配对给**他任务**
    PairConflict = 5,        // 冲突：本任务已有配对且**工人不同**（须先解绑）
    NoReservation = 6,       // 前置：本任务无有效预定（不变量 P1）
    NotReserved = 7,         // 幂等：释放时本就无预定
    NotPaired = 8,           // 幂等：解绑时本就无配对
    StateMismatch = 9,       // 卡片阶段/字段与本操作不符（管理器交叉校验卡片 · 零变化）
    InvalidTarget = 10,      // `targetRef.kind == None`（无目标）
    WorkerRequired = 11,     // `workerId == 0`
}

/// <summary>
/// `06 §六 :129` 配对表「状态」列。
/// ⚠️ `⏸` 挂起**不改本表**（`06 §四 :88`「挂起（**保留绑定**）」⇒ 行不变）；本片只区分「绑定中」。
/// </summary>
public enum TaskPairStatus : byte
{
    None = 0,
    Bound = 1,
}

/// <summary>
/// 目标**值键**（同目标判定键：类别 ＋ 格坐标 ＋ 类型键）。
/// ⭐ 纯值（`readonly struct`）：⛔ 不持目标对象、⛔ 不持任何引用 ⇒ **悬空不可能**（`05 §六 :149`）。
/// </summary>
public readonly struct TaskTargetKey : IEquatable<TaskTargetKey>
{
    public readonly byte kind;
    public readonly int cellX;
    public readonly int cellY;
    public readonly int typeKey;

    public TaskTargetKey(byte kind, int cellX, int cellY, int typeKey)
    {
        this.kind = kind;
        this.cellX = cellX;
        this.cellY = cellY;
        this.typeKey = typeKey;
    }

    /// <summary>由协议目标引用（值数据）取值键。</summary>
    public static TaskTargetKey Of(TaskTargetRef target)
    {
        return new TaskTargetKey((byte)target.kind, target.cellX, target.cellY, target.typeKey);
    }

    public bool Equals(TaskTargetKey other)
    {
        return kind == other.kind && cellX == other.cellX && cellY == other.cellY && typeKey == other.typeKey;
    }

    public override bool Equals(object obj)
    {
        return obj is TaskTargetKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        int h = kind;
        h = (h * 397) ^ cellX;
        h = (h * 397) ^ cellY;
        h = (h * 397) ^ typeKey;
        return h;
    }

    public override string ToString()
    {
        return "k" + kind + "@" + cellX + "," + cellY + "#" + typeKey;
    }
}

/// <summary>
/// `06 §六 :129/132` **预定行**：{ 任务 ↔ 目标 · 能力 · 超时 }（任务管理器为**唯一真源**）。
/// ⚠️ `deadline` 由**调用方给**（⛔ 本片不落秒数口径 · 见交付报告「未完成项」）；
///    `deadline = +∞` ⇒ 未设（不作超时兜底 · `06 §六 :133` 的兜底需调用方给 `SweepExpired` 的 `now`）。
/// ⚠️ `multiAllowed` ＝ **对象侧**「可多人」声明（`05 §八 :179`）⇒ ⛔ 不是管理器策略，只作入参。
/// </summary>
public struct TaskReservation
{
    public long taskId;
    public TaskTargetRef target;
    public string ability;      // 能力名（`05 §四 :79` 口径）；值标识，非对象引用
    public float deadline;      // +∞ ＝ 未设
    /// <summary>⭐【D1 补齐批 ⑤】显式「是否设了 deadline」（与卡片侧同源语义）；`false` ⇒ `SweepExpired` **不回收**。
    ///  ⛔ 不作 `-1` 哨兵、⛔ 不靠 `IsInfinity` 猜。</summary>
    public bool hasDeadline;
    public bool multiAllowed;   // 对象侧「可多人」声明
    public bool restorePending; // 读档「待复验」标记（`EnterRestore` 置位 · `RestoreValidated` 清位）
}

/// <summary>
/// `06 §六 :129` **配对行**：{ 交互者 A ↔ 目标 T · 能力 · 状态 }。
/// ⭐ 只存 `workerId`（**值标识**）＋ `TaskTargetRef`（**值数据**）⇒ 双方零引用（`05 §六 :149`）。
/// </summary>
public struct TaskPairRecord
{
    public long taskId;
    public int workerId;
    public TaskTargetRef target;
    public string ability;
    public TaskPairStatus status;
}

/// <summary>`06 §六 :133` 超时兜底回收报告（只报数 · ⛔ 非统计口径 · 不落历史）。</summary>
public struct TaskSweepReport
{
    public int reservationCount; // 回收的预定数
    public int pairCount;        // 级联释放的配对数
}
