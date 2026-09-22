using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - 调度中心（3.3.5 补全：资源流转搬运派发）
//  详见 3.0.1_2_输入输出决定层设计.md §7 / §11 + 3.3.5_资源流转与搬运系统.md
//  类名保留 ScheduleCenterStub（兼容场景引用），功能已补全为正式调度中心（搬运方向）。
//  昼夜节律：夜间停发 B/C 户外任务（输入端一致性要求）
//  搬运：检测产能建筑存储达标 → 派空闲工人 AddTaskStimulus（issuer=StorageComponent）
//  任务生命周期：刺激带 expiry（工人被打断没去 → 过期自然消失 → 下 tick 重派）
// ============================================================================

/// <summary>
/// 调度中心（§7 输入端一致性 + 3.3.5 资源流转）。
/// 职责：
///   1. 搬运派发（3.3.5）：产能建筑 IsReadyToHarvest → 找空闲工人 → TaskStimulus 注入
///   2. 防重复：_transporting 标记（建筑存储清空后释放）
///   3. 昼夜节律：夜间停发户外任务（防"调度中心夜间刚派活、威胁层就撤退"两系统打架）
/// 后续扩展：砍树/建造/随军任务统一走本中心派发（P1）。
/// </summary>
public class ScheduleCenterStub : MonoBehaviour
{
    [Header("搬运任务配置（3.3.5 资源流转）")]
    [Tooltip("搬运任务刺激强度（B 级，同砍树档位）")]
    public float transportIntensity = 2f;
    [Tooltip("搬运任务有效期（秒）：工人被打断没去 → 刺激过期 → 下 tick 重派")]
    public float transportExpiry = 5f;
    [Tooltip("搬运派发间隔（秒）")]
    public float assignInterval = 1f;

    [Header("战争机器乘员（改动②：工人操作战争机器）")]
    [Tooltip("操作任务刺激强度（B 级，同搬运档位）")]
    public float crewTaskIntensity = 2f;
    [Tooltip("操作任务有效期（秒）：到点续命重派，保持工人值守")]
    public float crewTaskExpiry = 3f;

    [Header("测试用任务配置（旧占位，P1 移除）")]
    [Tooltip("测试用砍树任务位置（白天派发 B 级任务）")]
    public Transform treeTarget;

    private readonly Dictionary<StorageComponent, HashSet<NPCBrain>> _transporting = new Dictionary<StorageComponent, HashSet<NPCBrain>>();
    // 机器（单位/建筑）-> 已派工人的名单（续命其操作任务，防堆叠）
    private readonly Dictionary<object, List<NPCBrain>> _crewAssignments = new Dictionary<object, List<NPCBrain>>();
    private float _assignTimer;

    // 3.5 §8.3：任务优先级映射 SO（修复S > 建造/生产A > 搬运B > 养殖/挑水/产金C），数据驱动
    private TaskPriorityConfig _priorityConfig;

    private void Awake()
    {
        _priorityConfig = Resources.Load<TaskPriorityConfig>("Config/TaskPriorityConfig");
    }

    private void Update()
    {
        _assignTimer += Time.deltaTime;
        if (_assignTimer < assignInterval) return;
        _assignTimer = 0f;

        // 昼夜节律：夜间停发 B/C 户外任务（§7 输入端一致性）
        if (IsNight()) return;

        // 3.5 §8.3 优先级派发：空闲工人优先接高优先级任务（S>A>B>C，同优先级 FIFO）。
        // 当前已落地搬运（B）；修复(S)/建造(A)/生产(A)/养殖/挑水/产金(C) 任务源在 P1 任务调度扩展中接入，
        // 届时统一走 DispatchByPriority 派发，本中心只按优先级排序 + 空闲工人优先高优先级。
        // ⭐⭐ `M1-G-1` 件4（`U-15` 根除项 · `D824` §一-4）：**原 `DispatchTransport()` 调用已删** ——
        //   链 B（本中心自建"搬运刺激 ＋ `BehaviorExecutor:HarvestCarry` 直通国库"）随 `#40` 一并退役；
        //   ⭐ 搬运统一由**链 A**（`Building` ④ `Transport` 广告 · **有阈值** ⇒ `TaskScheduler` 两段式）承担。
        DispatchCrew();
    }

    /// <summary>查任务类型优先级（TaskPriorityConfig SO；未配置回退 B）。供派发处统一取值，禁止硬编码优先级。</summary>
    private TaskPriority GetPriority(KingdomTaskType type)
    {
        return _priorityConfig != null ? _priorityConfig.Get(type) : TaskPriority.B;
    }

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
    //   ⚠️ 本类**未整删**：`DispatchCrew`（`:167` 级）／`AssignFollow`（`:263` 级）等仍在役。

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

    /// <summary>设置跟随锚点（旧测试占位，P1 统一派发随军任务时实现）</summary>
    public void AssignFollow(NPCBrain npc, UnitController anchor, TaskPriority priority, float intensity)
    {
        // 3.3.5 本轮只做搬运方向；跟随/砍树等任务统一派发留 P1
    }
}
