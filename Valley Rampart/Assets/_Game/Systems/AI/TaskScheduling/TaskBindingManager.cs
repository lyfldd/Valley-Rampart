using System;
using System.Collections.Generic;

// ============================================================================
//  `M5-B` 新协议 · 配对表 ＋ 预定表 ＋ 释放规则（**管理器唯一真源**）
//
//  文档锚：`06 §六 :128-134` —— 配对表＝唯一真源 · ⭐ 纪律「**任务在 ⇒ 预留在**」「**任务亡（完成／取消／作废）
//            ⇒ 预定【立刻释放】**」＋ **兜底：预定带超时**
//          `05 §六 :139-153` —— 配对只在管理器 · **双方零引用**（`A` 与 `T` 都不持对方引用）· 对象侧零字段
//          `06 §四 :73-106` —— 八阶段：配对／预定的建立点与释放点
//
//  硬边界（本片）：
//    · ⛔ **未接入旧生产链**（本类在跑的世界里**零调用点**；接线与切换归 `M5-C`）
//    · ⛔ 不持任何运行时对象引用：两张表只存 `long`／`int`／**值 struct**／`string`（★ 表内**不存卡片引用**）
//    · ⛔ 不落优先级／`retry`／分域／统计政策（超时阈值由**调用方给**，本片只落**机制**）
//    · ⛔ 不建单例、不注册全局（「单管理器」判据归 `M5-C`）
//
//  ⭐ 不变量（本片机械验证）：
//    **P1** 配对 ⊆ 预定（无有效预定 ⇒ 不配对 · `NoReservation`；反之预定消失 ⇒ 配对**级联释放**）
//    **P2** 同目标**占用模式一致**：`multiAllowed == false` 的目标**至多一行**；多行时各行**必须均** `multiAllowed == true`
//           （⛔ 不允许「独占行 ＋ 多人行」混占 · `ReservationConflict`）
//    **P3** 终态零残留：终态出口调用 `ReleaseOnTerminal` 后该任务两表全空（**由调用方驱动** —— 见交付报告）
//    **P4** 一工人至多一配对（`WorkerBusy`）
//
//  ⭐ 释放矩阵（`06 §六 :132`「同生共死」· 逐条对应卡片出口）：
//    `Done`／`Aborted`            ⇒ 配对**释放** ＋ 预定**释放**（`ReleaseOnTerminal`）
//    `Unassign`（回 `[1]` 待派）   ⇒ 配对**释放** ＋ 预定**保留**（任务仍在 ⇒ `Unpair`）
//    `[7] Restore` 进入            ⇒ 配对**释放**（读档后工人绑定不可信）＋ 预定**保留＋待复验**（`EnterRestore`）
//    `[7]` 重验成立（回原阶段）      ⇒ 预定**保留**（清待复验标记 · `RestoreValidated`）
//    `[7]` 重验不成立（→ `[6]`）     ⇒ 预定**释放**（走 `ReleaseOnTerminal`）
//    预定超时（管理器兜底）          ⇒ 预定**释放** ＋ 配对**级联释放**（`SweepExpired`）
//    显式放出目标（目标变化等）      ⇒ 预定**释放** ＋ 配对**级联释放**（`ReleaseReservation` · P1）
//    `⏸` 挂起／恢复                  ⇒ **均不动本表**（`06 §四 :88`「挂起（**保留绑定**）」）
// ============================================================================

/// <summary>
/// `M5-B` 任务绑定管理器 —— 持有**配对表**与**预定表**（`06 §六 :129` 唯一真源）并执行释放规则。
/// ⚠️ 纯 C# 类：⛔ 非 Unity 组件、⛔ 非单例、⛔ 不订阅事件、⛔ 不引用任何运行时对象。
/// ⚠️ 管理器**不认识卡片对象**（表内只存 `taskId`）⇒ 入口以**卡片为参数做交叉校验**（阶段/工人号一致），
///    但**不保存卡片引用**：卡片被回收也不会在表内留下悬空引用。
/// </summary>
public sealed class TaskBindingManager
{
    // ── 表 1：预定（`taskId → 行`）＋ 目标索引（同目标冲突判定／按时长回收）──
    private readonly Dictionary<long, TaskReservation> _reservations = new Dictionary<long, TaskReservation>();
    private readonly Dictionary<TaskTargetKey, List<long>> _reservationsByTarget = new Dictionary<TaskTargetKey, List<long>>();

    // ── 表 2：配对（`taskId → 行`）＋ 工人索引（一工人至多一配对）──
    private readonly Dictionary<long, TaskPairRecord> _pairs = new Dictionary<long, TaskPairRecord>();
    private readonly Dictionary<int, long> _workerToTask = new Dictionary<int, long>();

    // 超时回收复用缓冲（⛔ 非统计；仅避免每次扫描新分配）
    private readonly List<long> _sweepBuf = new List<long>(8);

    // ═════════════════════════════ 只读查询（契约读口）═════════════════════════

    /// <summary>当前预定行数（表规模读口 · ⛔ 非统计口径）。</summary>
    public int ReservationCount { get { return _reservations.Count; } }

    /// <summary>当前配对行数（表规模读口 · ⛔ 非统计口径）。</summary>
    public int PairCount { get { return _pairs.Count; } }

    /// <summary>取某任务的预定行（无 ⇒ false）。</summary>
    public bool TryGetReservation(long taskId, out TaskReservation reservation)
    {
        return _reservations.TryGetValue(taskId, out reservation);
    }

    /// <summary>取某任务的配对行（无 ⇒ false）。</summary>
    public bool TryGetPair(long taskId, out TaskPairRecord pair)
    {
        return _pairs.TryGetValue(taskId, out pair);
    }

    /// <summary>该工人是否已被占用（配对了某任务）。</summary>
    public bool IsWorkerBusy(int workerId)
    {
        return _workerToTask.ContainsKey(workerId);
    }

    /// <summary>某目标当前被几个任务预定（冲突判定读口）。</summary>
    public int ReservationCountForTarget(TaskTargetRef target)
    {
        List<long> list;
        if (!_reservationsByTarget.TryGetValue(TaskTargetKey.Of(target), out list)) return 0;
        return list.Count;
    }

    // ═════════════════════════════ 表 1：预定 ═════════════════════════════

    /// <summary>
    /// 建立预定（`06 §六 :132`「任务在 ⇒ 预留在」· 建立点＝卡片进入 `[1] 待派`）。
    /// ⚠️ 卡片**必须**处于 `Pending`（其余阶段 ⇒ `StateMismatch`；终态 ⇒ 同样 `StateMismatch`）：
    ///    预定一旦建立即由「任务在」维持，⛔ 不需要（也不允许）在后续阶段重复建立。
    /// ⚠️ `targetAllowsMultiple` ＝ **对象侧**「可多人」声明（`05 §八 :179`）；`false` 时同目标**独占**。
    /// ⚠️ `deadline &lt;= 0` 或 `NaN` ⇒ 视为**未设**（`+∞`）。
    /// </summary>
    public TaskBindingResult Reserve(TaskCard card, bool targetAllowsMultiple, float deadline)
    {
        if (card == null) return TaskBindingResult.StateMismatch;
        if (card.state != TaskLifecycleState.Pending) return TaskBindingResult.StateMismatch;
        if (card.targetRef.kind == TaskTargetKind.None) return TaskBindingResult.InvalidTarget;

        long taskId = card.taskId;
        TaskReservation existing;
        if (_reservations.TryGetValue(taskId, out existing))
        {
            // 幂等：同任务同目标 ⇒ 零变化；同任务**换目标** ⇒ 冲突（⛔ 不隐式改目标，须先 `ReleaseReservation`）
            return SameTarget(existing.target, card.targetRef)
                ? TaskBindingResult.AlreadyReserved
                : TaskBindingResult.ReservationConflict;
        }

        TaskTargetKey key = TaskTargetKey.Of(card.targetRef);
        if (HasOtherHolder(key, taskId))
        {
            // P2（占用模式必须一致）：本次「不许多人」**或**既有行中含「不许多人」⇒ 冲突。
            // ⛔ 不允许「独占行与多人行混占同一目标」——那会让「可多人」这一对象侧声明自相矛盾。
            if (!targetAllowsMultiple || HasExclusiveHolder(key, taskId))
                return TaskBindingResult.ReservationConflict;
        }

        TaskReservation row = new TaskReservation();
        row.taskId = taskId;
        row.target = card.targetRef;
        row.ability = card.ability;
        row.deadline = (float.IsNaN(deadline) || deadline <= 0f) ? float.PositiveInfinity : deadline;
        row.hasDeadline = !(float.IsNaN(deadline) || deadline <= 0f || float.IsPositiveInfinity(deadline));   // ⭐⑤ 显式载体
        row.multiAllowed = targetAllowsMultiple;
        row.restorePending = false;
        _reservations[taskId] = row;
        IndexAdd(key, taskId);
        return TaskBindingResult.Ok;
    }

    /// <summary>
    /// 显式释放预定（幂等）：⚠️ **级联释放该任务配对**（否则违反不变量 **P1**：配对必须挂在有效预定上）。
    /// ⚠️ 供「目标变化／改用例」等**需要主动放弃目标**的路径（接线归 `M5-C`）；
    /// ⛔ 终态与超时走 `ReleaseOnTerminal`／`SweepExpired`（两者同样配对＋预定一并处置）。
    /// </summary>
    public TaskBindingResult ReleaseReservation(long taskId)
    {
        bool touched = false;
        TaskReservation row;
        if (_reservations.TryGetValue(taskId, out row)) { RemoveReservation(taskId, row); touched = true; }
        TaskPairRecord pair;
        if (_pairs.TryGetValue(taskId, out pair)) { RemovePair(taskId, pair); touched = true; } // 级联（P1）
        return touched ? TaskBindingResult.Ok : TaskBindingResult.NotReserved;
    }

    // ═════════════════════════════ 表 2：配对 ═════════════════════════════

    /// <summary>
    /// 建立配对（`06 §六 :129`{ 交互者 ↔ 目标 · 能力 · 状态 } · 建立点＝卡片进入 `[2] 已派`）。
    /// 前置（**逐条**）：① 卡片处于 `Assigned`；② `workerId != 0`；③ `card.workerId == workerId`（**交叉校验**，
    /// 防「表与卡不一致」）；④ 该任务**已有有效预定**（不变量 **P1**；否则 `NoReservation`）。
    /// 幂等／冲突：同任务同工人 ⇒ `AlreadyPaired`（零变化）；同任务**不同工人** ⇒ `PairConflict`；
    /// 该工人已被**他任务**占用 ⇒ `WorkerBusy`（不变量 **P4**）。冲突与幂等一律**零变化**。
    /// </summary>
    public TaskBindingResult Pair(TaskCard card, int workerId)
    {
        if (card == null) return TaskBindingResult.StateMismatch;
        if (card.state != TaskLifecycleState.Assigned) return TaskBindingResult.StateMismatch;
        if (workerId == 0) return TaskBindingResult.WorkerRequired;
        if (card.workerId != workerId) return TaskBindingResult.StateMismatch;

        long taskId = card.taskId;
        TaskReservation reservation;
        if (!_reservations.TryGetValue(taskId, out reservation)) return TaskBindingResult.NoReservation;

        TaskPairRecord existing;
        if (_pairs.TryGetValue(taskId, out existing))
        {
            return existing.workerId == workerId ? TaskBindingResult.AlreadyPaired : TaskBindingResult.PairConflict;
        }

        long holder;
        if (_workerToTask.TryGetValue(workerId, out holder) && holder != taskId) return TaskBindingResult.WorkerBusy;

        TaskPairRecord row = new TaskPairRecord();
        row.taskId = taskId;
        row.workerId = workerId;
        row.target = reservation.target;
        row.ability = reservation.ability;
        row.status = TaskPairStatus.Bound;
        _pairs[taskId] = row;
        _workerToTask[workerId] = taskId;
        return TaskBindingResult.Ok;
    }

    /// <summary>
    /// 解绑（配对释放 · **预定保留**）：`06 §四 :87-89/:94-95`「工人死／到不了／超时 ⇒ 解绑，回 `[1]` 待派」。
    /// ⚠️ 卡片须已回 `Pending`（= `Unassign` 已完成）；幂等：本就无配对 ⇒ `NotPaired`（零变化）。
    /// </summary>
    public TaskBindingResult Unpair(TaskCard card)
    {
        if (card == null) return TaskBindingResult.StateMismatch;
        if (card.state != TaskLifecycleState.Pending) return TaskBindingResult.StateMismatch;

        TaskPairRecord row;
        if (!_pairs.TryGetValue(card.taskId, out row)) return TaskBindingResult.NotPaired;
        RemovePair(card.taskId, row);
        return TaskBindingResult.Ok;
    }

    // ═════════════════════════ 终态出口 ／ 读档出口 ═════════════════════════

    /// <summary>
    /// **终态出口**：`[5] Done` ／ `[6] Aborted`（含 `[7]` 重验不成立后作废）⇒ **配对释放 ＋ 预定释放**
    /// （`06 §六 :132`「任务亡 ⇒ 预定**立刻释放**」· 同生共死）。
    /// ⚠️ 卡片须为终态（否则 `StateMismatch`）；**幂等**：两表本就无该任务 ⇒ `NotReserved`（零变化）。
    /// ⭐ 该口是**终态零残留（P3）**的唯一驱动点。
    /// </summary>
    public TaskBindingResult ReleaseOnTerminal(TaskCard card)
    {
        if (card == null) return TaskBindingResult.StateMismatch;
        if (!card.IsTerminal) return TaskBindingResult.StateMismatch;

        bool touched = false;
        TaskPairRecord pair;
        if (_pairs.TryGetValue(card.taskId, out pair)) { RemovePair(card.taskId, pair); touched = true; }
        TaskReservation reservation;
        if (_reservations.TryGetValue(card.taskId, out reservation)) { RemoveReservation(card.taskId, reservation); touched = true; }
        return touched ? TaskBindingResult.Ok : TaskBindingResult.NotReserved;
    }

    /// <summary>
    /// **读档入口**：`[7] Restore`（`06 §四 :103-105`）⇒ 配对**释放**（读档后工人绑定不可信）
    /// ＋ 预定**保留并转「待复验」**（任务名义上仍在 ⇒ `06 §六 :132`「任务在 ⇒ 预留在」；由重验结论定去留）。
    /// ⚠️ 卡片须处于 `Restore`；幂等：无预定 ⇒ `NotReserved`。
    /// </summary>
    public TaskBindingResult EnterRestore(TaskCard card)
    {
        if (card == null) return TaskBindingResult.StateMismatch;
        if (card.state != TaskLifecycleState.Restore) return TaskBindingResult.StateMismatch;

        bool touched = false;
        TaskPairRecord pair;
        if (_pairs.TryGetValue(card.taskId, out pair)) { RemovePair(card.taskId, pair); touched = true; }
        TaskReservation reservation;
        if (_reservations.TryGetValue(card.taskId, out reservation))
        {
            reservation.restorePending = true;
            _reservations[card.taskId] = reservation;
            touched = true;
        }
        return touched ? TaskBindingResult.Ok : TaskBindingResult.NotReserved;
    }

    /// <summary>
    /// **读档重验成立**：卡已出 `Restore` 且回到「进行中四阶段」⇒ 预定**保留**（清「待复验」标记）。
    /// ⚠️ **不重建配对**（工人须经 `Assign`／`Pair` 重新建立 ⇒ 归 `M5-C` 编排）。幂等：无预定 ⇒ `NotReserved`。
    /// </summary>
    public TaskBindingResult RestoreValidated(TaskCard card)
    {
        if (card == null) return TaskBindingResult.StateMismatch;
        if (!TaskLifecycleRules.IsRestorable(card.state)) return TaskBindingResult.StateMismatch;

        TaskReservation reservation;
        if (!_reservations.TryGetValue(card.taskId, out reservation)) return TaskBindingResult.NotReserved;
        reservation.restorePending = false;
        _reservations[card.taskId] = reservation;
        return TaskBindingResult.Ok;
    }

    // ═════════════════════════ 超时兜底（`06 §六 :133`）═════════════════════════

    /// <summary>
    /// **预定超时兜底回收**：回收全部 `deadline &lt;= now` 的预定（`deadline = +∞` 永不回收），
    /// 并**级联释放**其配对（否则违反不变量 **P1**）。
    /// ⚠️ `now` 由**调用方给**（⛔ 本片不落时基/秒数口径）；`reclaimedTaskIds` 可为 null，非 null 时追加回收任务号。
    /// ⚠️ 管理器**不持卡片引用** ⇒ 被回收任务对应的卡片状态**不由本口改动**：调用方（`M5-C`）须据回收名单处置
    ///    （回待派或作废）——本片只给读数。
    /// </summary>
    public TaskSweepReport SweepExpired(float now, List<long> reclaimedTaskIds)
    {
        TaskSweepReport report = new TaskSweepReport();
        _sweepBuf.Clear();
        foreach (KeyValuePair<long, TaskReservation> kv in _reservations)
        {
            // ⭐【D1 补齐批 ⑤】超时回收**只读显式载体** `hasDeadline`（⛔ 不再用 `IsInfinity` 猜；未设 ⇒ 永不回收）。
            if (kv.Value.hasDeadline && kv.Value.deadline <= now) _sweepBuf.Add(kv.Key);
        }
        for (int i = 0; i < _sweepBuf.Count; i++)
        {
            long taskId = _sweepBuf[i];
            TaskReservation reservation;
            if (!_reservations.TryGetValue(taskId, out reservation)) continue; // 同批内已释放
            RemoveReservation(taskId, reservation);
            report.reservationCount++;

            TaskPairRecord pair;
            if (_pairs.TryGetValue(taskId, out pair)) // 级联（P1：配对必须挂在有效预定上）
            {
                RemovePair(taskId, pair);
                report.pairCount++;
            }
            if (reclaimedTaskIds != null) reclaimedTaskIds.Add(taskId);
        }
        return report;
    }

    // ═════════════════════════ 不变量自检（契约校验口）═════════════════════════

    /// <summary>
    /// 不变量自检：**P1**（配对 ⊆ 预定）／**P2**（独占目标至多一行）／索引一致性（工人索引／目标索引）。
    /// ⚠️ **P3 不在此列**（终态零残留需调用方驱动终态出口后自检 · 见交付报告）。
    /// ⛔ 非生产热路径（仅供验证/自检 · 可传 null 只取数）。
    /// </summary>
    public int VerifyInvariants(List<string> violations)
    {
        int count = 0;
        void Add(string msg)
        {
            count++;
            if (violations != null) violations.Add(msg);
        }

        // P1：配对必须挂在有效预定上
        foreach (KeyValuePair<long, TaskPairRecord> kv in _pairs)
        {
            if (!_reservations.ContainsKey(kv.Key)) Add("P1 违反：任务 " + kv.Key + " 有配对无预定");
        }

        // 工人索引一致性
        int workerIndexCount = 0;
        foreach (KeyValuePair<int, long> kv in _workerToTask)
        {
            workerIndexCount++;
            TaskPairRecord pair;
            if (!_pairs.TryGetValue(kv.Value, out pair) || pair.workerId != kv.Key)
                Add("W 索引违反：工人 " + kv.Key + " → 任务 " + kv.Value + " 无对应配对行");
        }
        if (workerIndexCount != _pairs.Count)
            Add("W 索引计数不一致：" + workerIndexCount + " ≠ 配对行 " + _pairs.Count);

        // 目标索引一致性 ＋ P2
        int targetIndexCount = 0;
        foreach (KeyValuePair<TaskTargetKey, List<long>> kv in _reservationsByTarget)
        {
            List<long> list = kv.Value;
            targetIndexCount += list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                TaskReservation reservation;
                if (!_reservations.TryGetValue(list[i], out reservation))
                {
                    Add("T 索引违反：目标 " + kv.Key + " 指向不存在任务 " + list[i]);
                    continue;
                }
                if (!TaskTargetKey.Of(reservation.target).Equals(kv.Key))
                    Add("T 索引违反：任务 " + list[i] + " 的目标键与索引不符");
                if (list.Count > 1 && !reservation.multiAllowed)
                    Add("P2 违反：目标 " + kv.Key + " 多行但存在「不许多人」行（任务 " + list[i] + "）");
            }
        }
        if (targetIndexCount != _reservations.Count)
            Add("T 索引计数不一致：" + targetIndexCount + " ≠ 预定行 " + _reservations.Count);

        return count;
    }

    // ───────────────────────────── 内部：表维护 ─────────────────────────────

    private static bool SameTarget(TaskTargetRef a, TaskTargetRef b)
    {
        return TaskTargetKey.Of(a).Equals(TaskTargetKey.Of(b));
    }

    private bool HasOtherHolder(TaskTargetKey key, long exceptTaskId)
    {
        List<long> list;
        if (!_reservationsByTarget.TryGetValue(key, out list)) return false;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != exceptTaskId) return true;
        }
        return false;
    }

    /// <summary>该目标上是否存在**他任务**的「不许多人」预定行（P2 占用模式一致性判定）。</summary>
    private bool HasExclusiveHolder(TaskTargetKey key, long exceptTaskId)
    {
        List<long> list;
        if (!_reservationsByTarget.TryGetValue(key, out list)) return false;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == exceptTaskId) continue;
            TaskReservation row;
            if (_reservations.TryGetValue(list[i], out row) && !row.multiAllowed) return true;
        }
        return false;
    }

    private void IndexAdd(TaskTargetKey key, long taskId)
    {
        List<long> list;
        if (!_reservationsByTarget.TryGetValue(key, out list))
        {
            list = new List<long>(2);
            _reservationsByTarget[key] = list;
        }
        list.Add(taskId);
    }

    private void IndexRemove(TaskTargetKey key, long taskId)
    {
        List<long> list;
        if (!_reservationsByTarget.TryGetValue(key, out list)) return;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != taskId) continue;
            list.RemoveAt(i);
            break;
        }
        if (list.Count == 0) _reservationsByTarget.Remove(key);
    }

    private void RemoveReservation(long taskId, TaskReservation row)
    {
        IndexRemove(TaskTargetKey.Of(row.target), taskId);
        _reservations.Remove(taskId);
    }

    private void RemovePair(long taskId, TaskPairRecord row)
    {
        long holder;
        if (_workerToTask.TryGetValue(row.workerId, out holder) && holder == taskId) _workerToTask.Remove(row.workerId);
        _pairs.Remove(taskId);
    }
}
