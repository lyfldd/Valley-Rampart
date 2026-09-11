using System;
using UnityEngine;

// ============================================================================
//  派工资源偏向活权重配置 SO（2_23 资源 P0 批B / R-B2，D529 §2.3；D634 三处口径）
//  资产路径：Resources/Config/ResourceBiasConfig.asset（so-data-driven 禁魔法数）。
//  消费方：TaskScheduler.EffectivePriority（R-B1 排序键「死表 × 活权重」乘子层）。
//
//  职责：per 资源偏向权重（出厂 1.0 占位）+ 线性公式参数（k/min/max）
//        + 任务类型→资源映射表（D634③：SO 可配，**禁硬编码**）。
//
//  公式（D634②，线性）：
//    weight(r) = clamp( bias[r] × (1 + k × 缺口率(r)), minWeight, maxWeight )
//    缺口率(r) = clamp01( max(0, −Flow.Net(r)) / max(1, Flow.Out(r)) )   ← 补-1 口径（见 HH.172 §五）
//    缺口信号 = EconomyBlock.Flow.Net(r) 负值（D634①）
//
//  结构（D519 模式预留 champion 所有权，同 BuildingPlacementConfig 先例）：
//    权重出厂占位 1.0，未来可归训练（factor_registry 草案登记 = 2_23 §八 S-B5；
//    **登记面属训练仓**，本批不进训练）。
//  边界注记（2_23 §八 S-B4）：活权重住 TaskScheduler（Unity 侧执行层）**零镜像、不进 AI.Core**
//    （D525 选址打分器同型注记）。
//
//  出厂值安全性（补-2/补-3）：maxWeight=1.25 ⇒ A×1.25=3.75 < S=4、B×1.25=2.5 < A=3、
//    C×1.25=1.25 < B=2 ⇒ **不跨死表档位**（红线「死表 S/A/B/C 语义保留不动」）；
//    minWeight=1.0 ⇒ 默认只升不降（出厂零配置 = 与既有排序零差异）。
// ============================================================================

/// <summary>任务类型 → 五元资源映射条目（D634③ SO 可配表）。</summary>
[Serializable]
public struct TaskBiasEntry
{
    [Tooltip("任务类型（键）")]
    public KingdomTaskType taskType;

    [Tooltip("映射到的五元资源（金/石/木/粮/铁）")]
    public EcoResource resource;

    [Tooltip("是否启用（false=显式关闭该条的兜底映射）")]
    public bool enabled;
}

[CreateAssetMenu(menuName = "ValleyRampart/ResourceBiasConfig", fileName = "ResourceBiasConfig")]
public class ResourceBiasConfig : ScriptableObject
{
    [Header("per 资源偏向权重（出厂 1.0 占位；索引=EcoResource：0金 1石 2木 3粮 4铁）")]
    public float[] bias = new float[] { 1f, 1f, 1f, 1f, 1f };

    [Header("线性公式参数（D634② weight = clamp(bias×(1+k×缺口率), min, max)）")]
    [Tooltip("缺口率放大系数 k（出厂 1.0 保守占位）")]
    public float k = 1.0f;

    [Tooltip("权重下限（出厂 1.0=只升不降，防偏向后低于死表原值）")]
    public float minWeight = 1.0f;

    [Tooltip("权重上限（出厂 1.25 < 4/3 ⇒ 保不跨死表档位；调大即允许跨档，零代码改）")]
    public float maxWeight = 1.25f;

    [Header("任务→资源映射表（SO 可配，禁硬编码；未命中或 enabled=false → 该任务不参与偏向）")]
    public TaskBiasEntry[] taskResourceMap = new TaskBiasEntry[]
    {
        // 参数/源建筑可推导的任务（Gather/Production/Transport）优先走实参推断，
        // 本表为其缺参兜底 + 无实参的直派任务（GoldMine 等）主映射。
        new TaskBiasEntry { taskType = KingdomTaskType.GoldMine,   resource = EcoResource.Gold, enabled = true },
        new TaskBiasEntry { taskType = KingdomTaskType.Rancher,    resource = EcoResource.Food, enabled = true },
        new TaskBiasEntry { taskType = KingdomTaskType.WaterCarry, resource = EcoResource.Food, enabled = false }, // 水非五元
        new TaskBiasEntry { taskType = KingdomTaskType.WaterHaul,  resource = EcoResource.Food, enabled = false }, // 水非五元
        new TaskBiasEntry { taskType = KingdomTaskType.AmmoReload, resource = EcoResource.Metal, enabled = false }, // 弹药非五元
    };

    /// <summary>取某资源偏向权重（索引越界/未配置 → 1.0 出厂等价）。</summary>
    public float BiasOf(EcoResource r)
    {
        int i = (int)r;
        if (bias == null || i < 0 || i >= bias.Length) return 1f;
        return bias[i];
    }

    /// <summary>按任务类型查兜底资源映射（线性扫，首命中即返；未命中/enabled=false → -1=不参与偏向）。
    /// 确定性：纯数组扫无随机。</summary>
    public int ResourceOfTaskType(KingdomTaskType type)
    {
        if (taskResourceMap != null)
        {
            for (int i = 0; i < taskResourceMap.Length; i++)
            {
                var e = taskResourceMap[i];
                if (e.enabled && e.taskType == type) return (int)e.resource;
            }
        }
        return -1;
    }

    /// <summary>载入（缺 asset 时回退默认占位实例 ⇒ 出厂零行为差异；对齐 SituationConfig.Load 先例）。</summary>
    public static ResourceBiasConfig Load()
    {
        var cfg = Resources.Load<ResourceBiasConfig>("Config/ResourceBiasConfig");
        return cfg != null ? cfg : CreateInstance<ResourceBiasConfig>();
    }
}
