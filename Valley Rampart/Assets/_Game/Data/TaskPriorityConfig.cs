using System;
using UnityEngine;

/// <summary>
/// 任务优先级映射配置（3.5.3 §8.3；P1-9）。
/// 担当文档所称「TaskPriorityDef SO」：任务类型 → TaskPriority。
///   修复(S) > 建造/升级/生产/采集/加工(A) > 搬运(B) > 养殖/挑水/产金(C)。
/// 调度中心（ScheduleCenterStub）派活按 S > A > B > C 排序，同优先级 FIFO；
/// 空闲工人不足时高优先级任务先派，低优先级排队。
/// 数据驱动（so-data-driven 铁律）：优先级映射进 SO，禁止在派发处硬编码 TaskPriority.
/// 资产路径：Resources/Config/TaskPriorityConfig.asset。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/TaskPriorityConfig", fileName = "TaskPriorityConfig")]
public class TaskPriorityConfig : ScriptableObject
{
    [Tooltip("任务类型 → 优先级映射（Repair=S, Build/Produce=A, Transport=B, 养殖/挑水/产金=C）")]
    public TaskPriorityEntry[] entries;

    /// <summary>查某任务类型的优先级；未配置返回 B（兜底搬运档位）。</summary>
    public TaskPriority Get(KingdomTaskType type)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].taskType == type) return entries[i].priority;
        }
        return TaskPriority.B;
    }
}

/// <summary>王国任务类型（3.5.3 §8.3 映射键；QQQ.2 §10.1 扩展）。</summary>
public enum KingdomTaskType
{
    Repair,     // 修复/重建 → S（产能断链/废墟优先，防连锁崩溃）
    Build,      // 建造/升级 → A
    Produce,    // 生产/采集/加工 → A
    Transport,  // 搬运 → B
    Rancher,    // 养殖 → C
    WaterCarry, // 挑水 → C
    GoldMine,   // 产金（税务所）→ C
    // ===== QQQ.2 §10.1 扩展（末尾追加保持旧值稳定）=====
    Production, // 生产（农场/采石/矿洞，原地劳作；与 Produce 语义对齐，供 KingdomTask 用）
    WaterHaul,  // 搬水（水井→水网）
    Gather,      // 采集（一次性资源点）
    // ===== 2_12 步骤9 弹药（D207~D212，HH.19 A×4；末尾追加保持序列化稳定）=====
    AmmoReload  // 装填（战争机器/塔弹药：工人搬弹药填弹仓；运输档 B）
}

/// <summary>单条优先级映射（任务类型 → 优先级）。</summary>
[Serializable]
public struct TaskPriorityEntry
{
    public KingdomTaskType taskType;
    public TaskPriority priority;
}