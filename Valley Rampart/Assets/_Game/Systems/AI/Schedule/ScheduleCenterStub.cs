using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - 调度中心（⭐ `M1-G-1c` 件A-N1 · `D829` 勘正：原注「3.3.5 补全：资源流转**搬运派发**」
//   已不成立 ⇒ 本类现职 ＝ **昼夜节律 ＋ 战争机器乘员派发 ＋ 随军派遣**）
//  详见 3.0.1_2_输入输出决定层设计.md §7 / §11 + 3.3.5_资源流转与搬运系统.md
//  类名保留 ScheduleCenterStub（兼容场景引用），功能为**战争机器乘员 ＋ 随军派遣**方向
//  （⛔ 原文「功能已补全为正式调度中心（**搬运方向**）」已删 —— 搬运已统一归 `TaskScheduler` 链 A）。
//  昼夜节律：夜间停发 B/C 户外任务（输入端一致性要求）
//  ⚠️ 原注「搬运：检测产能建筑存储达标 → 派空闲工人 AddTaskStimulus（issuer=StorageComponent）」
//     **已随 `U-15` 根除项删除**（该行为今已不存在 —— 见下方 `<summary>` 已退役职责段）。
//     ⚠️ 本句为**自然语言旧述、不含任何退役符号** ⇒ 机械符号扫描**必漏**（`L-78`：清零判据须**双轨**）。
//  任务生命周期：刺激带 expiry（工人被打断没去 → 过期自然消失 → 下 tick 重派）
// ============================================================================

/// <summary>
/// 调度中心（§7 输入端一致性 + 3.3.5 资源流转）。
/// 职责：
///   1. 昼夜节律：夜间停发户外任务（防"调度中心夜间刚派活、威胁层就撤退"两系统打架）
/// ⚠️ 已退役职责（⭐ `M1-G-1c` 件A1 · `D828`：**旧句直接删除**，⛔ 不以 `~~` 形式保留 —— XML doc 不渲染删除线）：
///   · 「搬运派发（3.3.5）：产能建筑 `IsReadyToHarvest` → 找空闲工人 → `TaskStimulus` 注入」
///     ⇒ 随 `U-15` **根除项**删除（链 B 整段退役 ⇒ 搬运统一由**链 A**：`Building` ④ `Transport` 广告 ·
///       有阈值 ⇒ `TaskScheduler` **两段式** `LoadInventoryFromSource` → `UnloadInventory` 承担）；
///   · 「防重复：`_transporting` 标记（建筑存储清空后释放）」⇒ 随件 F-1 删除（字段 **0 引用**）。
/// ⚠️ 原「后续扩展：砍树/建造/随军任务**统一走本中心派发**（P1）」承诺**已撤回**（件A4 · `L-63`）——
///   ⭐ **措辞收窄（件A-N2 · `D829`）**：**搬运／`KingdomTask` 派发**统一归 `TaskScheduler`（`GetPriority`
///   同 SO 同回退）；⚠️ 但**本类并未整删派发** —— **在役派发口 ＝ `DispatchCrew()`**（战争机器乘员 ·
///   `:82` `Update` 内调用；邻文件 `Building.cs:349`／`UnitController.cs:1220` 的 docstring 亦明写
///   「供调度中心（`ScheduleCenterStub.DispatchCrew`）按缺口派工人」）。
///   ⚠️ **本端勘正（`D829` A-N2 收窄）**：`AssignFollow`（`:215`）**不是**在役派发口 —— 其实读为
///   **空体占位存根**（**0 调用** 全库 ＋ 方法体为空 ＋ 自注「旧测试占位，P1 统一派发随军任务时实现」）
///   ⇒ ⛔ 不得与 `DispatchCrew` 并列称"在役"（`L-76`：概括句射程须与实际职责**逐条**对齐）。
/// </summary>
public class ScheduleCenterStub : MonoBehaviour
{
    [Header("派发节奏配置")]
    // ⭐ `M1-G-1c` 件F-2（`D827`）：`transportIntensity`／`transportExpiry` **已删**（随链 B 退役 ·
    //   实读 0 引用 ⇒ 仅剩声明）；⚠️ `assignInterval` **保留**（仍被 `Update` 使用）。
    [Tooltip("派发间隔（秒）")]
    public float assignInterval = 1f;

    [Header("战争机器乘员（改动②：工人操作战争机器）")]
    [Tooltip("操作任务刺激强度（B 级，同搬运档位）")]
    public float crewTaskIntensity = 2f;
    [Tooltip("操作任务有效期（秒）：到点续命重派，保持工人值守")]
    public float crewTaskExpiry = 3f;

    [Header("测试用任务配置（旧占位，P1 移除）")]
    [Tooltip("测试用砍树任务位置（白天派发 B 级任务）")]
    public Transform treeTarget;

    // ⭐ `M1-G-1c` 件F-1（`D827`）：原 `_transporting`（防重复标记）**已删** —— 实读 **0 引用**
    //   （其唯一消费者链 B 已随 `U-15` 根除项删除）。
    // 机器（单位/建筑）-> 已派工人的名单（续命其操作任务，防堆叠）
    private readonly Dictionary<object, List<NPCBrain>> _crewAssignments = new Dictionary<object, List<NPCBrain>>();
    private float _assignTimer;

    // ⭐ `M1-G-1c` 件A4（`D828` §A4 **准删**）：原 `_priorityConfig` ＋ `Awake()` 读取（3.5 §8.3 优先级 SO）
    //   已删 —— 依据：① 本类 `GetPriority` **0 调用** ② ⭐ **承接方在场且等价**：
    //   `TaskScheduler.GetPriority` ＝ **同 SO**（`Config/TaskPriorityConfig`）＋ **同回退**（`TaskPriority.B`）
    //   ③ 「备而未用」的依据**已被推翻**（原注承诺"本中心统一派发"⇒ 该职责已随 `U-15` 根除项整段删除 · `L-63`）。
    // ⚠️ `using System.Collections.Generic` **保留**（`_crewAssignments` 仍需要）。

    private void Update()
    {
        _assignTimer += Time.deltaTime;
        if (_assignTimer < assignInterval) return;
        _assignTimer = 0f;

        // 昼夜节律：夜间停发 B/C 户外任务（§7 输入端一致性）
        if (IsNight()) return;

        // ⭐ `M1-G-1c` 件A4（`D828`）：原「3.5 §8.3 优先级派发…届时**统一走 `DispatchByPriority`** 派发，
        //   本中心只按优先级排序」**承诺已撤回** —— 本中心的自建派发职责已整段删除（链 B 退役），
        //   ⭐ **措辞收窄（件A-N2 · `D829`）**：**搬运／`KingdomTask` 派发**统一归 `TaskScheduler`
        //   （`GetPriority` 同 SO `Config/TaskPriorityConfig` ＋ 同回退 `TaskPriority.B`）⇒ ⛔ 本类**不再**承诺
        //   "统一派发"；⚠️ 但**本类并未整删派发** —— 下一行 `DispatchCrew()` **仍在役**（`AssignFollow` 为**空体占位**）。
        // ⭐⭐ `M1-G-1` 件4（`U-15` 根除项 · `D824` §一-4）：**原 `DispatchTransport()` 调用已删** ——
        //   链 B（本中心自建"搬运刺激 ＋ `BehaviorExecutor:HarvestCarry` 直通国库"）随 `#40` 一并退役；
        //   ⭐ 搬运统一由**链 A**（`Building` ④ `Transport` 广告 · **有阈值** ⇒ `TaskScheduler` 两段式）承担。
        DispatchCrew();
    }

    // ⭐ `M1-G-1c` 件A4（`D828` §A4 **准删**）：原 `GetPriority(KingdomTaskType)` **已删** —— 本类 0 调用
    //   （全库仅 `TaskScheduler.GetPriority` 在役 · 同 SO ＋ 同回退）。

    /// <summary>夜间判定（TimeManager 未挂载=白天，行为不变）</summary>
    private bool IsNight()
    {
        return TimeManager.Instance != null
            && (TimeManager.Instance.CurrentPhase == TimePhase.Night
                || TimeManager.Instance.CurrentPhase == TimePhase.Dusk);
    }

    // ⭐⭐ `M1-G-1` 件4（`U-15` **根除项** · 双链合一 · `D824` §一-4）：**原 `DispatchTransport()`
    //   与 `IsTransporting()` 整段已删**（链 B ＝ 阈值-less 的"清理派发 ＋ 直通国库落点"）。
    //   ⭐ 依据（本端派工前实读 · 已验收）：两链的差异**只在阈值** ——
    //     · **链 A**（在役）＝ `Building.TryAdvertiseTask` ④ ⇒ `new KingdomTask(Transport, this)`，
    //       判据 `stored >= capacity * transportThreshold` ⇒ 走 `TaskScheduler` **两段式**（`LoadInventoryFromSource`
    //       → `EnterMovingToDest` → `UnloadInventory`）；
    //     · **链 B**（本段删除）＝ 本类判据 `!IsReadyToHarvest()`（**无阈值**）⇒ `AddTaskStimulus(issuer: storage)`
    //       ⇒ `BehaviorExecutor` 到达即 `HarvestCarry()`（**源仓直通国库**）。
    //   ⚠️ 连带已改：`BuildingPanel:183-187`（原读 `IsTransporting` ⇒ 改读 `TaskScheduler.HasWorkerAssigned(源)`）。
    //   ⚠️ 本类**未整删**：`DispatchCrew`（在役 · `Update` 调用 ＋ 邻文件 docstring 引用）**仍在役**；
    //      ⚠️ 但 `AssignFollow` **不在役**（`:215` 空体占位 · 0 调用）—— 本行原并列写法已由 `D829` A-N2 收窄勘正。

    /// <summary>
    /// 战争机器乘员派发（改动② 工人操作战争机器，方式 A：工人主动去操控）。
    /// 对每个缺工人的友方机器（单位 Ballista / 建筑 Catapult，crewRequired>0）派空闲工人到机器旁值守：
    ///   机器被动检查（UnitController.CrewMachineThinkCore / Building.HasEnoughCrew）数到工人即解锁发射/移动。
    /// 敌情门控（不锁死工人）：仅当机器附近有敌（HasNearbyEnemy）才派/续工；无敌情则释放工人，机器只自主开火。
    /// 已派工人每 tick 续命其操作任务（remove+add 单任务，刷新 expiry，防堆叠），机器被毁/无敌情/满编则释放。
    /// </summary>
    private void DispatchCrew()
    {
        // ① 释放 已无效 / 已满编 / 无敌情 的机器工人（移除其操作任务源）
        if (_crewAssignments.Count > 0)
        {
            var stale = new List<object>();
            foreach (var kv in _crewAssignments)
            {
                var m = kv.Key;
                int deficitNow = CrewDeficitOf(m);
                if (m == null || deficitNow <= 0 || !HasThreat(m))
                {
                    for (int i = 0; i < kv.Value.Count; i++)
                        if (kv.Value[i] != null) kv.Value[i].RemoveTaskStimulus(m);
                    stale.Add(m);
                }
            }
            for (int i = 0; i < stale.Count; i++) _crewAssignments.Remove(stale[i]);
        }

        // ② 对每个 有敌情 + 缺工人 的机器派/续命工人
        bool any = false;
        var units = FindObjectsOfType<UnitController>();
        for (int i = 0; i < units.Length; i++)
        {
            var m = units[i];
            if (m == null || !m.IsCrewMachine) continue;
            if (!m.HasNearbyEnemy()) continue;         // 敌情门控：无敌情不派工
            int deficit = m.CrewDeficit();
            if (deficit <= 0) continue;
            any = true;
            AssignOrRenewCrew(m, m.transform.position, deficit);
        }
        var buildings = FindObjectsOfType<Building>();
        for (int i = 0; i < buildings.Length; i++)
        {
            var b = buildings[i];
            if (b == null || b.def == null || b.def.crewRequired <= 0) continue;
            if (!b.HasNearbyEnemy()) continue;         // 敌情门控：无敌情不派工
            int deficit = b.CrewDeficit();
            if (deficit <= 0) continue;
            any = true;
            AssignOrRenewCrew(b, b.transform.position, deficit);
        }
        if (!any) return;
    }

    /// <summary>机器（单位/建筑）当前是否有敌情；空/已毁返回 false（触发释放）。</summary>
    private bool HasThreat(object m)
    {
        if (m is UnitController u) return u != null && u.IsCrewMachine && u.HasNearbyEnemy();
        if (m is Building b) return b != null && b.def != null && b.def.crewRequired > 0 && b.HasNearbyEnemy();
        return false;
    }

    /// <summary>机器（单位/建筑）当前缺工人数；空/已毁返回 0（触发释放）。</summary>
    private int CrewDeficitOf(object m)
    {
        if (m is UnitController u) return u != null && u.IsCrewMachine ? u.CrewDeficit() : 0;
        if (m is Building b) return b != null && b.def != null && b.def.crewRequired > 0 ? b.CrewDeficit() : 0;
        return 0;
    }

    /// <summary>给机器续命/补充工人：续命已派工人（remove+add），不足则派空闲工人。</summary>
    private void AssignOrRenewCrew(object machine, Vector2 machinePos, int deficit)
    {
        if (!_crewAssignments.TryGetValue(machine, out var assigned))
        { assigned = new List<NPCBrain>(); _crewAssignments[machine] = assigned; }

        // 续命已派工人（remove+add 单任务，防堆叠；刷新 expiry 保持值守）
        for (int i = assigned.Count - 1; i >= 0; i--)
        {
            var w = assigned[i];
            if (w == null) { assigned.RemoveAt(i); continue; }
            w.RemoveTaskStimulus(machine);
            w.AddTaskStimulus(new TaskStimulus(
                TaskPriority.B, Vector2XUnity.FromUnity(machinePos), crewTaskIntensity,
                expiry: Time.time + crewTaskExpiry, issuer: machine));
        }

        // 补不足：派空闲工人（非战斗/无进行中任务）
        int need = deficit - assigned.Count;
        if (need <= 0) return;
        var npcs = FindObjectsOfType<NPCBrain>();
        for (int i = 0; i < npcs.Length && need > 0; i++)
        {
            if (npcs[i] == null || !npcs[i].IsIdleForTask) continue;
            npcs[i].AddTaskStimulus(new TaskStimulus(
                TaskPriority.B, Vector2XUnity.FromUnity(machinePos), crewTaskIntensity,
                expiry: Time.time + crewTaskExpiry, issuer: machine));
            assigned.Add(npcs[i]);
            need--;
        }
    }

    /// <summary>设置跟随锚点（⭐ **空体占位存根** · 全库 **0 调用** · P1 随军任务实现时补）。
    /// ⚠️ `M1-G-1c` 件A-N1（`D829`）：原方法内注「3.3.5 本轮**只做搬运方向**；跟随/砍树等任务统一派发留 P1」
    ///   **旧句已删** ——「只做搬运方向」**已不成立**（搬运已统一归 `TaskScheduler` 链 A；本类现职 ＝
    ///   昼夜节律 ＋ 战争机器乘员派发）⇒ ⚠️ 该句为**自然语言旧述、不含任何退役符号** ⇒ **机械符号扫描必漏**，
    ///   由 `L-78` **双轨②（退役职责清单 × 承载文件全文过）** 命中本处。</summary>
    public void AssignFollow(NPCBrain npc, UnitController anchor, TaskPriority priority, float intensity)
    {
        // （空体：占位存根 ⇒ ⛔ 本方法**不是**在役派发口 · 勿与 `DispatchCrew()` 并列称"在役"）
    }
}
