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
}

/// <summary>单条训练定义（转职链：from → to，消耗金 + 时长）。</summary>
[Serializable]
public struct TrainingDef
{
    [Tooltip("训练设施建筑 id（训练所=TrainingGround，练兵场=Barracks）")]
    public string buildingId;
    public Occupation fromOccupation;   // 起始职业（如 Unemployed）
    public Occupation toOccupation;     // 目标职业（如 Worker/Porter）
    public int costGold;                // 训练消耗金（§10 金1）
    public int costCrystal;             // 训练消耗水晶（§10 法师/治疗师 水晶1；无则0）
    public int costDays;                // 训练时长（天，§10 1天）
}