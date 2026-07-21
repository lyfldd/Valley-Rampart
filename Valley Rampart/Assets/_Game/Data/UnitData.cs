using UnityEngine;

public enum Occupation
{
    Ruler, Archer, Warrior, Civilian
}

public enum Faction
{
    None, Human_Player, Undead
}

[CreateAssetMenu(menuName = "ValleyRampart/UnitData")]
public class UnitData : ScriptableObject
{
    [Header("身份设定")]
    public Faction faction;

    [Header("职业")]
    public Occupation occupation;

    [Header("移动速度")]
    public float walkSpeed = 5f;
    public float runSpeed = 10f;

    [Header("基础数值属性")]
    public int maxHp;
    public int attack;
    public int defense;
}
