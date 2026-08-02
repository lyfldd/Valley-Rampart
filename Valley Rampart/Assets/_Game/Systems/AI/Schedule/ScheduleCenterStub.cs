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

    [Header("测试用任务配置（旧占位，P1 移除）")]
    [Tooltip("测试用砍树任务位置（白天派发 B 级任务）")]
    public Transform treeTarget;

    private readonly HashSet<StorageComponent> _transporting = new HashSet<StorageComponent>();
    private float _assignTimer;

    private void Update()
    {
        _assignTimer += Time.deltaTime;
        if (_assignTimer < assignInterval) return;
        _assignTimer = 0f;

        // 昼夜节律：夜间停发 B/C 户外任务（§7 输入端一致性）
        if (IsNight()) return;

        DispatchTransport();
    }

    /// <summary>夜间判定（TimeManager 未挂载=白天，行为不变）</summary>
    private bool IsNight()
    {
        return TimeManager.Instance != null
            && (TimeManager.Instance.CurrentPhase == TimePhase.Night
                || TimeManager.Instance.CurrentPhase == TimePhase.Dusk);
    }

    /// <summary>
    /// 搬运派发（3.3.5）：遍历产能建筑，存储达标且无搬运中任务的 → 派空闲工人。
    /// 任务完成（Harvest 清空存储）→ 标记自动释放。
    /// </summary>
    private void DispatchTransport()
    {
        // 清理已完成搬运标记（建筑存储被 Harvest 清空 / 建筑销毁）
        if (_transporting.Count > 0)
            _transporting.RemoveWhere(s => s == null || s.storedAmount <= 0);

        var storages = FindObjectsOfType<StorageComponent>();
        if (storages.Length == 0) return;

        // 一次收集空闲工人（避免每建筑重复 FindObjectsOfType）
        var npcs = FindObjectsOfType<NPCBrain>();
        if (npcs.Length == 0) return;

        foreach (var storage in storages)
        {
            if (storage == null) continue;
            if (!storage.IsReadyToHarvest()) continue;      // 无产出不搬
            if (_transporting.Contains(storage)) continue;  // 已有搬运中任务，防重复派发

            // 找空闲工人（IsIdleForTask：无进行中任务/战斗）
            NPCBrain worker = null;
            for (int i = 0; i < npcs.Length; i++)
            {
                if (npcs[i] != null && npcs[i].IsIdleForTask)
                {
                    worker = npcs[i];
                    break;
                }
            }
            if (worker == null) return;  // 无空闲工人，等下 tick

            // 派发搬运任务（B 级，目标=建筑位置，issuer=StorageComponent 供 L3 透传 HarvestTarget）
            var building = storage.GetComponent<Building>();
            Vector2 pos = building != null ? (Vector2)building.transform.position : (Vector2)storage.transform.position;
            worker.AddTaskStimulus(new TaskStimulus(
                TaskPriority.B, Vector2XUnity.FromUnity(pos), transportIntensity,
                expiry: Time.time + transportExpiry, issuer: storage));
            _transporting.Add(storage);
            Debug.Log($"[调度中心] 派发搬运任务 → {worker.name} @ {pos}（{storage.resourceType} 存量 {storage.storedAmount}）");
        }
    }

    /// <summary>是否搬运中（BuildingPanel 显示收取状态用）</summary>
    public bool IsTransporting(StorageComponent storage)
    {
        return storage != null && _transporting.Contains(storage);
    }

    /// <summary>设置跟随锚点（旧测试占位，P1 统一派发随军任务时实现）</summary>
    public void AssignFollow(NPCBrain npc, UnitController anchor, TaskPriority priority, float intensity)
    {
        // 3.3.5 本轮只做搬运方向；跟随/砍树等任务统一派发留 P1
    }
}
