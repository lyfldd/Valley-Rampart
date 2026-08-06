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
        DispatchTransport();
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

    /// <summary>
    /// 搬运派发（3.3.5 + 3.5 P1-8 分批）。遍历产能建筑，存储达标且未满配 → 派空闲工人。
    /// 分批：源建筑产出 &gt; 携带量时，按 ceil(stored/carry) 补派多个工人，每趟各搬一次携带量
    /// （BehaviorExecutor 到达调 HarvestCarry 限量搬，剩余留待下轮）。优先级查 TaskPriorityConfig（B）。
    /// </summary>
    private void DispatchTransport()
    {
        // 清理已无效搬运记录（建筑销毁 / 无产出 / 已派工人全失效）
        if (_transporting.Count > 0)
        {
            var stale = new List<StorageComponent>();
            foreach (var kv in _transporting)
            {
                var s = kv.Key;
                kv.Value.RemoveWhere(w => w == null);
                if (s == null || !s.IsReadyToHarvest() || kv.Value.Count == 0)
                    stale.Add(s);
            }
            for (int i = 0; i < stale.Count; i++) _transporting.Remove(stale[i]);
        }

        var storages = FindObjectsOfType<StorageComponent>();
        if (storages.Length == 0) return;

        // 一次收集空闲工人（避免每建筑重复 FindObjectsOfType）
        var npcs = FindObjectsOfType<NPCBrain>();
        if (npcs.Length == 0) return;

        TaskPriority transportPriority = GetPriority(KingdomTaskType.Transport);

        foreach (var storage in storages)
        {
            if (storage == null) continue;
            if (!storage.IsReadyToHarvest()) continue;      // 无产出不搬
            if (storage.storedAmount <= 0) continue;

            if (!_transporting.TryGetValue(storage, out var assigned))
            {
                assigned = new HashSet<NPCBrain>();
                _transporting[storage] = assigned;
            }
            assigned.RemoveWhere(w => w == null);

            // 分批：需要搬运批次数 = ceil(存量 / 携带量)；已派数不足则补派
            int carry = storage.GetCarryAmount();
            int batches = Mathf.Max(1, Mathf.CeilToInt(storage.storedAmount / (float)carry));
            int need = batches - assigned.Count;
            if (need <= 0) continue;

            var building = storage.GetComponent<Building>();
            Vector2 pos = building != null ? (Vector2)building.transform.position : (Vector2)storage.transform.position;

            for (int i = 0; i < npcs.Length && need > 0; i++)
            {
                var worker = npcs[i];
                if (worker == null || !worker.IsIdleForTask || assigned.Contains(worker)) continue;
                worker.AddTaskStimulus(new TaskStimulus(
                    transportPriority, Vector2XUnity.FromUnity(pos), transportIntensity,
                    expiry: Time.time + transportExpiry, issuer: storage));
                assigned.Add(worker);
                need--;
            }
            if (assigned.Count > 0)
                Debug.Log($"[调度中心] 派发搬运任务 → {assigned.Count} 工人 @ {pos}（{storage.resourceType} 存量 {storage.storedAmount}，分批{batches}）");
        }
    }

    /// <summary>是否搬运中（BuildingPanel 显示收取状态用）</summary>
    public bool IsTransporting(StorageComponent storage)
    {
        return storage != null && _transporting.ContainsKey(storage);
    }

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
