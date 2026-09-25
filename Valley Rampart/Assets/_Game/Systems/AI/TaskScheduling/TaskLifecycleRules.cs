// ============================================================================
//  `M5-A` 新协议 · 生命周期状态机规则（设计依据：`06_任务层.md` §四 八阶段）
//
//  性质：**纯规则**（静态谓词 ＋ 合法性表）—— 零状态、零 using（⛔ 不引用任何命名空间）、零对象引用。
//  分工：本文件只判**合法性**；状态写入与各阶段入口在卡片侧（唯一改状态点）。
//  判据 3（`06 §九` 不居中态）在此**可机械验证**：任一阶段对任一受入动作 ⇒
//    「合法」或「返回明确错误码」，⛔ 不存在"静默半途"。
// ============================================================================

/// <summary>
/// `06 §四` 全生命周期八阶段（`:73-106`）。
/// 别名对照（文档用词 ↔ 本枚举）：`[3] 执行中（Working）` ＝ `Executing`；`[4] 判定（Check）` ＝ `Resolving`。
/// </summary>
public enum TaskLifecycleState : byte
{
    Created = 0,   // [0] 生：卡片刚构造（未准入）
    Pending = 1,   // [1] 待派：等匹配（能力 ∩ 优先级）
    Assigned = 2,  // [2] 已派：已绑定工人
    Executing = 3, // [3] 执行中：耗时推进
    Resolving = 4, // [4] 判定：按 `progressSpec` 判完成
    Done = 5,      // [5] 完成（终态）
    Aborted = 6,   // [6] 终止（终态：失败／取消／作废）
    Restore = 7,   // [7] 读档：一律重验
}

/// <summary>转移拒绝码（非法转移 ⇒ 返回本码且状态零变化 · `06 §四` 未列的箭头一律非法）。</summary>
public enum TaskTransitionError : byte
{
    None = 0,                  // 合法（唯一成功值）
    UnknownState = 1,          // 阶段值不在枚举域
    IllegalEdge = 2,           // 阶段对不被允许
    IllegalReason = 3,         // 阶段对允许，但该原因码不被该阶段允许
    TerminalSource = 4,        // 源为终态（Done／Aborted）——终态不可再转移
    NotRestorable = 5,         // 该阶段不参与读档重验（`06 §四 :104` 只恢复"进行中"）
    NoRestoreContext = 6,      // 非 Restore 阶段调用读档出口
    RetryPolicyUnconfigured = 7, // `retryMax < 0`（默认次数＝待裁 `06 §十-3`）⇒ 拒绝消费
    RetryExhausted = 8,        // retry 用尽（`06 §五 :123`）
    AlreadySuspended = 9,      // 已处 `⏸` 挂起
    NotSuspended = 10,         // 未挂起
    WorkerUnbound = 11,        // 未绑定工人（`workerId == 0`）
    ReasonRequired = 12,       // 终止／回待派／挂起必须给原因码（⛔ 不接受 None）
}

/// <summary>`06 §四` 八阶段的合法性表与边界谓词。⛔ 无状态、⛔ 不改任何数据。</summary>
public static class TaskLifecycleRules
{
    /// <summary>八阶段枚举域（供机械遍历 · 顺序同 `06 §四 [0]~[7]`）。</summary>
    public static readonly TaskLifecycleState[] AllStates = new TaskLifecycleState[]
    {
        TaskLifecycleState.Created,
        TaskLifecycleState.Pending,
        TaskLifecycleState.Assigned,
        TaskLifecycleState.Executing,
        TaskLifecycleState.Resolving,
        TaskLifecycleState.Done,
        TaskLifecycleState.Aborted,
        TaskLifecycleState.Restore,
    };

    /// <summary>`06 §四 [5] Done`／`[6] Aborted` ＝ 终态（其后再无箭头 ⇒ 边界可机械验证）。</summary>
    public static bool IsTerminal(TaskLifecycleState s)
    {
        return s == TaskLifecycleState.Done || s == TaskLifecycleState.Aborted;
    }

    /// <summary>`06 §四 [7]` 可参与读档重验者 ＝「进行中」四阶段（`:103`）。</summary>
    public static bool IsRestorable(TaskLifecycleState s)
    {
        return s == TaskLifecycleState.Pending || s == TaskLifecycleState.Assigned
            || s == TaskLifecycleState.Executing || s == TaskLifecycleState.Resolving;
    }

    /// <summary>阶段值是否在枚举域内。</summary>
    public static bool IsDefined(TaskLifecycleState s)
    {
        return (byte)s <= (byte)TaskLifecycleState.Restore;
    }

    /// <summary>
    /// 阶段对合法性 —— **逐条对应 `06 §四` 的箭头**（⛔ 未列者一律非法）：
    /// `[0]→[1]` 准入 ／ `[0]→[6]` 卡片不合法丢弃 ／ `[1]→[2]` 匹配到工人 ／ `[1]→[6]` 五类作废 ／
    /// `[2]→[3]` 到达 ／ `[2]→[1]` 解绑重派 ／ `[2]→[6]` 作废 ／
    /// `[3]→[4]` 完成一次交互 ／ `[3]→[1]` 超时强制放弃 ／ `[3]→[6]` 取消 ／
    /// `[4]→[3]` 未完成还要继续 ／ `[4]→[1]` 重新派 ／ `[4]→[5]` 完成 ／ `[4]→[6]` 条件已不满足 ／
    /// `{进行中四阶段}→[7]` 读档重验（`:103`）／ `[7]→{进行中四阶段}` 重验成立恢复 ／ `[7]→[6]` 重验不成立作废。
    /// ⚠️ `Done`／`Aborted` 出边为 0；`⏸` 挂起/恢复**不是阶段对**（同阶段 + 标记）。
    /// </summary>
    public static bool IsLegalEdge(TaskLifecycleState from, TaskLifecycleState to)
    {
        bool toRestore = to == TaskLifecycleState.Restore;
        switch (from)
        {
            case TaskLifecycleState.Created:
                return to == TaskLifecycleState.Pending || to == TaskLifecycleState.Aborted;
            case TaskLifecycleState.Pending:
                return to == TaskLifecycleState.Assigned || to == TaskLifecycleState.Aborted || toRestore;
            case TaskLifecycleState.Assigned:
                return to == TaskLifecycleState.Executing || to == TaskLifecycleState.Pending
                    || to == TaskLifecycleState.Aborted || toRestore;
            case TaskLifecycleState.Executing:
                return to == TaskLifecycleState.Resolving || to == TaskLifecycleState.Pending
                    || to == TaskLifecycleState.Aborted || toRestore;
            case TaskLifecycleState.Resolving:
                return to == TaskLifecycleState.Executing || to == TaskLifecycleState.Pending
                    || to == TaskLifecycleState.Done || to == TaskLifecycleState.Aborted || toRestore;
            case TaskLifecycleState.Restore:
                return IsRestorable(to) || to == TaskLifecycleState.Aborted;
            default:
                return false; // Done／Aborted（终态）＋ 未定义值
        }
    }

    /// <summary>合法性判定（含域检查与终态守卫）⇒ `None` ＝ 合法。</summary>
    public static TaskTransitionError Validate(TaskLifecycleState from, TaskLifecycleState to)
    {
        if (!IsDefined(from) || !IsDefined(to)) return TaskTransitionError.UnknownState;
        if (IsTerminal(from)) return TaskTransitionError.TerminalSource;
        if (!IsLegalEdge(from, to)) return TaskTransitionError.IllegalEdge;
        return TaskTransitionError.None;
    }

    /// <summary>
    /// 终止原因 × 源阶段白名单（`06 §四`／§五 逐条）：
    /// · **全局**（任何进行中阶段）：`BuildingDied`（§五 建筑死亡 · `:118`）／`ExternalAbandon`（§五 外部撤回 · `:120`）
    /// · `[1] Pending`：目标没了／目标变了／目标用尽／过期／归属方灭亡（`:82-84`）＋ retry 用尽（§五 `:123`）
    /// · `[2] Assigned`／`[3] Executing`：目标没了／目标变了（`:92-93`）／过期／归属方灭亡
    /// · `[4] Resolving`：条件已不满足（`:99`）／目标没了／目标变了／目标用尽／过期／归属方灭亡
    /// ⚠️ `RestoreRejected` 只走 `RejectRestore`（`[7]` 出口）；`CardInvalid` 只走 `Reject`（`[0]` 出口）。
    /// </summary>
    public static bool IsLegalAbort(TaskLifecycleState from, TaskAbortReason reason)
    {
        if (reason == TaskAbortReason.None) return false;
        if (reason == TaskAbortReason.BuildingDied || reason == TaskAbortReason.ExternalAbandon)
        {
            return IsRestorable(from); // 全局两类：进行中四阶段皆可
        }
        switch (from)
        {
            case TaskLifecycleState.Pending:
                return reason == TaskAbortReason.TargetRemoved
                    || reason == TaskAbortReason.TargetReplaced
                    || reason == TaskAbortReason.TargetExhausted
                    || reason == TaskAbortReason.Expired
                    || reason == TaskAbortReason.OwnerLost
                    || reason == TaskAbortReason.RetryExhausted;
            case TaskLifecycleState.Assigned:
            case TaskLifecycleState.Executing:
                return reason == TaskAbortReason.TargetRemoved
                    || reason == TaskAbortReason.TargetReplaced
                    || reason == TaskAbortReason.Expired
                    || reason == TaskAbortReason.OwnerLost;
            case TaskLifecycleState.Resolving:
                return reason == TaskAbortReason.TargetRemoved
                    || reason == TaskAbortReason.TargetReplaced
                    || reason == TaskAbortReason.TargetExhausted
                    || reason == TaskAbortReason.Expired
                    || reason == TaskAbortReason.OwnerLost
                    || reason == TaskAbortReason.ConditionUnmet;
            default:
                return false;
        }
    }

    /// <summary>
    /// 回待派原因 × 源阶段白名单（`06 §四` 三处箭头）：
    /// `[2]`：工人死／到不了（`:87-89`）｜`[3]`：工人死／到不了／超时（`:94-95`）｜`[4]`：重新派（`:98`）。
    /// </summary>
    public static bool IsLegalUnassign(TaskLifecycleState from, TaskUnassignReason reason)
    {
        if (reason == TaskUnassignReason.None) return false;
        switch (from)
        {
            case TaskLifecycleState.Assigned:
                return reason == TaskUnassignReason.WorkerDied || reason == TaskUnassignReason.Unreachable;
            case TaskLifecycleState.Executing:
                return reason == TaskUnassignReason.WorkerDied || reason == TaskUnassignReason.Unreachable
                    || reason == TaskUnassignReason.Timeout;
            case TaskLifecycleState.Resolving:
                return reason == TaskUnassignReason.Replan;
            default:
                return false;
        }
    }
}
