using System;

/// <summary>
/// 人口存档数据（3.5 实施计划 §2.3；PopulationSystem，Global）。
/// 字段与文档逐一对齐。
/// </summary>
[Serializable]
public class PopulationSaveData
{
    public int saveDataVersion = 1;
    public int populationCount;              // 人口数（无性别，每 2 人 5 天）
    public int birthCooldownDays;            // 生育冷却倒计时
    public float avgSatiety;                 // 平均饱食（幸福/生育条件输入，P0 占位）
    public float avgHappiness;               // 平均幸福
}