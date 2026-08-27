using System;
using UnityEngine;

// ============================================================================
//  效用行动配置（2_17 步骤9，D321/D323/D345/D346）
//  15 条行动定义 SO 落点（P0 先落 8 项：①~⑥+⑬⑭，D345）。
//  每条 = 阶段可见性门控(minStage) + 性格轴映射(axis=0好战1经济2防守3扩张4外交,D311) +
//         需求强度缺口函数(need+needA/B) + 阶段权重(stageWeight[])。
//  四因子评分（D323）：score = 需求强度 × 性格权重 × 可行性(二值 D346) × 阶段权重。
//  资产路径：Resources/Config/Kingdoms/UtilityActionConfig.asset；阶段A手配、阶段B入训(---§3.4)。
// ============================================================================

/// <summary>
/// 效用行动配置（2_17 步骤9）。服务 UtilityScorer 对可见候选打分。
/// actions 默认预填 P0 8 项（阶段可见性/轴映射/缺口函数参数/阶段权重占位）：
///  ①建住宅 ②建仓库 ③建产能 ④强化采集（存活起）⑤屯粮（发育起）⑥招工人（存活起）⑬重建 ⑭防御（全阶段）
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/Kingdoms/UtilityActionConfig", fileName = "UtilityActionConfig")]
public class UtilityActionConfig : ScriptableObject
{
    [Tooltip("P0 行动子集定义（D345：①~⑥+⑬⑭）；P1 由步骤10 补全 15 条。建造类 buildingId/cost 与 Resources/Buildings 同名 def 资产一致（数据驱动双保险：评分可行性门控与门面执行同口径）")]
    public UtilityActionDef[] actions = new UtilityActionDef[]
    {
        new UtilityActionDef { id = UtilityAction.BuildHouse,     name = "建住宅",   minStage = ScriptStage.Survive,  axis = (int)PersonalityAxis.Economy,     axisWeight = 1f, need = NeedKind.HouseGap,        needA = 10, needB = 0, stageWeight = new float[]{1,1,1,1}, buildingId = "House",     costWood = 4 },
        new UtilityActionDef { id = UtilityAction.BuildWarehouse, name = "建仓库",   minStage = ScriptStage.Survive,  axis = (int)PersonalityAxis.Economy,     axisWeight = 1f, need = NeedKind.WarehouseGap,     needA = 250, needB = 0, stageWeight = new float[]{1,1,1,1}, buildingId = "Warehouse", costGold = 4, costStone = 4 },
        new UtilityActionDef { id = UtilityAction.BuildCapacity,  name = "建产能",   minStage = ScriptStage.Survive,  axis = (int)PersonalityAxis.Belligerence, axisWeight = 1f, need = NeedKind.CapacityGap,      needA = 8, needB = 0, stageWeight = new float[]{1,1,1,1}, buildingId = "quarry",    costGold = 50 },
        new UtilityActionDef { id = UtilityAction.BoostHarvest,   name = "强化采集", minStage = ScriptStage.Survive,  axis = (int)PersonalityAxis.Economy,     axisWeight = 1f, need = NeedKind.HarvestGap,       needA = 0, needB = 3, stageWeight = new float[]{1,1,1,1}, buildingId = "farm",      costGold = 50 },
        new UtilityActionDef { id = UtilityAction.Grain,          name = "屯粮",     minStage = ScriptStage.Develop,  axis = (int)PersonalityAxis.Economy,     axisWeight = 1f, need = NeedKind.GrainGap,         needA = 2, needB = 1, stageWeight = new float[]{1,1,1,1}, buildingId = "Granary",   costWood = 4 },
        new UtilityActionDef { id = UtilityAction.RecruitWorker,  name = "招工人",   minStage = ScriptStage.Survive,  axis = (int)PersonalityAxis.Expansion,   axisWeight = 1f, need = NeedKind.RecruitWorkerGap, needA = 10, needB = 0, stageWeight = new float[]{1,1,1,1} },
        new UtilityActionDef { id = UtilityAction.Rebuild,        name = "重建",     minStage = ScriptStage.Survive,  axis = (int)PersonalityAxis.Defense,     axisWeight = 1f, need = NeedKind.RebuildGap,       needA = 0, needB = 0, stageWeight = new float[]{1,1,1,1} },
        new UtilityActionDef { id = UtilityAction.Defense,        name = "防御姿态", minStage = ScriptStage.Survive,  axis = (int)PersonalityAxis.Defense,     axisWeight = 1f, need = NeedKind.DefenseNeed,      needA = 0, needB = 0, stageWeight = new float[]{1,1,1,1} },
    };

    /// <summary>按 id 查行动定义（无 → null）。</summary>
    public UtilityActionDef? Find(UtilityAction id)
    {
        if (actions == null) return null;
        foreach (var a in actions)
            if (a.id == id) return a;
        return null;
    }

    /// <summary>载入效用配置（缺 asset 时回退默认预填实例；so-data-driven 禁魔法数）。</summary>
    public static UtilityActionConfig LoadConfig()
    {
        var cfg = Resources.Load<UtilityActionConfig>("Config/Kingdoms/UtilityActionConfig");
        return cfg != null ? cfg : ScriptableObject.CreateInstance<UtilityActionConfig>();
    }
}

/// <summary>
/// 单条效用行动定义（D321/D323 字段载体；SO 内可序列化）。
/// score = need(needA/needB 参数化缺口单调分) × personality[axis]×axisWeight(性格权重) × feasibility(D346 二值) × stageWeight[]。
/// </summary>
[Serializable]
public struct UtilityActionDef
{
    /// <summary>行动 id（UtilityAction 枚举值；正 id，0=None）。</summary>
    public UtilityAction id;

    /// <summary>显示名（冒烟断言/诊断）。</summary>
    public string name;

    /// <summary>阶段可见性门控（D321：仅可见才评分；不参与打分连续性）。</summary>
    public ScriptStage minStage;

    /// <summary>性格轴映射（0好战 1经济 2防守 3扩张 4外交；D311 五轴）。</summary>
    public int axis;

    /// <summary>性格轴乘入系数（独立线性乘入 D311；默认1=中性，&lt;1 压该轴；0=不随轴）。</summary>
    public float axisWeight;

    /// <summary>需求强度缺口函数类型（D323 单调缺口）。</summary>
    public NeedKind need;

    /// <summary>缺口函数参数（needA=目标/基线；needB=阈值/日；逐 need 语义见 UtilityScorer）。</summary>
    public float needA;

    /// <summary>缺口函数次参数（同上）。</summary>
    public float needB;

    /// <summary>阶段权重（索引 ScriptStage 0存活/1发育/2扩张/3军事）。</summary>
    public float[] stageWeight;

    // ===== 执行派遣映射（2_17 完整局批次；建造类有效，非建造类空/0）=====

    /// <summary>建造目标 BuildingDef id（Resources/Buildings 资产 id，如 "House"/"Granary"；非建造类=""）。</summary>
    public string buildingId;

    /// <summary>建造成本镜像（与 BuildingDef.cost 同步抄录；评分 Feasible 门控用，执行以门面真 def 校验双保险）。</summary>
    public int costGold, costStone, costWood, costFood;
}

/// <summary>性格五轴索引（D311：0~1 独立不归一化）。</summary>
public enum PersonalityAxis : byte { Belligerence = 0, Economy = 1, Defense = 2, Expansion = 3, Diplomacy = 4 }