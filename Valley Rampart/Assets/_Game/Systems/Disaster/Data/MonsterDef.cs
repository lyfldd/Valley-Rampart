using UnityEngine;

// 2_14 怪物属性 SO（设计 §3.2）
[CreateAssetMenu(menuName = "ValleyRampart/Disaster/MonsterDef")]
public class MonsterDef : ScriptableObject
{
    [Header("美术")]
    public GameObject prefab;                // 怪物 Prefab（含 MonsterController；普通怪挂 MonsterAI，D252）

    [Header("基础属性（§3.2）")]
    public MonsterType type;                 // 种类
    public int hp;                           // HP
    public int attack;                       // 攻击（Slinger 远程射程圆半径=6 格，D258）
    public float attackInterval = 2f;        // 攻击间隔（占位 2s，D258）
    public float attackRangeCells = 1.2f;    // 攻击射程（格；近战默认 1.2，Slinger=6）
    public bool isRangedAttack = false;      // 远程攻击（Slinger true，走射程圆 2_5）
    public float projectileSpeed = 0f;       // 弹速（远程用，世界单位/秒）
    public float speedCellsPerSec;           // 移速（格/秒）
    public int visionRadiusCells;            // 视野半径（格）
    public float valueWeight;                // 价值权重（目标选择用）
    public bool isElite = false;             // 精英标记（走 NPCBrain 同源，D252）

    [Header("行为（§3.3/§4.3）")]
    public float retreatHpRatio = 0.2f;          // 撤退血量阈值（HP<20% 尝试退回传送门）
    public int carryResource = 5;                // 掠夺资源量（每怪）

    [Header("回援（§4.1 守门）")]
    public float guardRecallRatio = 0.5f;        // 传送门被打时回援比例（占位 50%）
}