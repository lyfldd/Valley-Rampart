using System;
using UnityEngine;

/// <summary>
/// 牧场养殖配置（3.5 §13.10；查表类型 SO，数据驱动）。
/// 动物表：幼崽价 / 生长天数 / 产肉量（屠宰制，一次性产出）。
/// 资产路径：Resources/Config/RanchConfig.asset。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/RanchConfig", fileName = "RanchConfig")]
public class RanchConfig : ScriptableObject
{
    [Tooltip("牧场动物定义（按 AnimalType 匹配）")]
    public AnimalDef[] animals;
}

/// <summary>动物类型（§13.10 兔/鸡/猪/牛）。</summary>
public enum AnimalType
{
    Rabbit,   // 兔：1金/2天/1肉
    Chicken,  // 鸡：1金/3天/2肉
    Pig,      // 猪：2金/5天/3肉
    Cow       // 牛：4金/8天/5肉
}

/// <summary>单种动物定义（幼崽价/生长/产肉，§13.10）。</summary>
[Serializable]
public struct AnimalDef
{
    public AnimalType type;
    public string displayName;
    public int youngCost;   // 幼崽价（金）
    public int growDays;    // 生长天数（成年后顺延）
    public int meatYield;   // 宰杀产肉/次
}