using UnityEngine;

/// <summary>
/// 君主专属数据，继承自 UnitData。
/// 基础属性(maxHp/attack/defense/walkSpeed/runSpeed)来自父类，
/// 子类仅新增初始国家资源。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/RulerData", fileName = "NewRulerData")]
public class RulerData : UnitData
{

    [Header("初始国家资源")]
    public int initialGold = 100;
    public int initialStone = 100;
    public int initialWood = 100;
    public int initialFood = 100;
}
