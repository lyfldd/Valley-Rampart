using System;
using UnityEngine;

/// <summary>
/// 训练配置（3.5 实施计划 P0 步骤4）。训练定义进 SO（数据驱动），禁止硬编码。
/// 本轮覆盖训练所（生产 Lv1）：无职业 → 工人/搬运工（金1+1天，÷5）。
/// 资产路径：Resources/Config/TrainingConfig.asset。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/TrainingConfig", fileName = "TrainingConfig")]
public class TrainingConfig : ScriptableObject
{
    [Tooltip("全部训练定义（训练所/练兵场等按 from/to 匹配）")]
    public TrainingDef[] trainings;

    [Header("QQQ.2 §需求3 / DR-3 / DR-12：训练 UI 数据")]
    [Tooltip("该训练建筑可训练的职业白名单（如兵营→士兵/弓箭手，学院→法师）。TrainingPanel 弹职业选择用")]
    public Occupation[] supportedOccupations;

    [Tooltip("各目标职业的训练时长（天）。缺省按 DR-12：居民→工人 1 天 / →士兵 2 天 / →高阶 3 天")]
    public float[] trainDurationDays;   // 与 supportedOccupations 同序（按职业对齐）
}

/// <summary>单条训练定义（转职链：from → to，消耗金 + 时长）。</summary>
[Serializable]
public struct TrainingDef
{
    [Tooltip("训练设施建筑 id（训练所=TrainingGround，练兵场=Barracks）")]
    public string buildingId;
    public Occupation fromOccupation;   // 起始职业（如 Resident）
    public Occupation toOccupation;     // 目标职业（如 Worker/Porter）
    public int costGold;                // 训练消耗金（§10 金1）
    public int costCrystal;             // 训练消耗水晶（§10 法师/治疗师 水晶1；无则0）
    public int costMetal;               // 2_12 步骤8 D132：兵种强化消耗铁（重装战士/盾卫/骑兵 5/5/8 占位；0=无铁消耗）
    public int costDays;                // 训练时长（天，§10 1天）

    // ===== 2_20 M7 专属兵训练门禁（D419 唯一入口 + D490 共通槽退役）=====
    [Tooltip("可训练种族（RaceIds 0=Human 1=Elf 2=Dwarf 3=Orc）。-1=共通条目（全族可见）。专属兵=各族专属建筑/练兵场按此过滤——跨族不可训练（D419 唯一入口）")]
    public int raceId;
    [Tooltip("建筑等级门槛（0=无门槛）。练兵场 Lv2 直接训练=minBuildingLevel 2（QQQ.5 附录A「练兵场 Lv2」落地载体）")]
    public int minBuildingLevel;
}