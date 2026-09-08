using System;
using System.Collections.Generic;

/// <summary>
/// 训练队列域存档载荷（DZ-075 训练队列入档 / HH.109 件2）。
/// TrainingSystem.SaveState 序列化全部队列条目（在训+排队，玩家/AI per-kingdom 统一覆盖）。
/// 裁定口径（D564）=入档优于丢失退款（玩家体验优先）；单位丢失条目作废不退款（退款链未定义，列报）。
/// 新增 payload 模块自治 version=1，全局 saveVersion 不动（M1 零 schema bump）。
/// 旧档无本条目 → SaveManager 分发跳过，LoadState 不被调用（零兼容负担）。
/// </summary>
[Serializable]
public class TrainingSavePayload
{
    /// <summary>payload 版本（模块自治迁移用）。</summary>
    public int version = 1;

    /// <summary>全部队列条目（扁平列表；建筑归属由 buildingSaveId 承载，读档按建筑分桶）。</summary>
    public List<TrainingQueueSaveEntry> entries = new List<TrainingQueueSaveEntry>();
}

/// <summary>单条训练队列存档条目。TrainingQueueEntry 的运行时引用（Building/UnitController/TrainingDef）
/// 全部换为稳定 id 入档：读档经 SaveManager 反查实例 + 训练配置表反查 def（SO 改动不破档）。</summary>
[Serializable]
public class TrainingQueueSaveEntry
{
    /// <summary>训练建筑存档 SaveId（"Building_{guid}"——读档阶段 1.5 已由 BuildingFactory.SpawnFromSave 重建并注册）。</summary>
    public string buildingSaveId;

    /// <summary>受训单位存档 SaveId（"Unit_{faction}_{occupation}_{guid}"——读档阶段 1.5 已由 UnitFactory.SpawnFromSave 重建并注册）。</summary>
    public string unitSaveId;

    /// <summary>TrainingDef 反查三键：buildingId + 起始职业 + 目标职业（GetTrainings 表内唯一匹配）。</summary>
    public string buildingId;
    public int fromOccupation;
    public int toOccupation;

    /// <summary>开始训练的绝对游戏日（进度单位=游戏日，跨读档连续——任务书件2.4）。</summary>
    public int startDay;

    /// <summary>true=占用槽位训练中；false=排队等待空槽。</summary>
    public bool inTraining;

    /// <summary>所属王国（2_17 步骤5 per-kingdom 记账；0=玩家，>0=AI）。</summary>
    public int kingdomId;

    /// <summary>种族/学院修正后的实际训练天数（入队定档副本，与运行时语义一致）。</summary>
    public int effCostDays;
}
