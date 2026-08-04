using UnityEngine;

/// <summary>
/// 工事（3.6 §4.4，独立资产）。城墙/城门/拒马/塔统一走本类，消灭 UnitData 与 Buildings 双份定义。
/// 等级 = int 不设上限（解耦原则 1）；弹射物交互按穿透 vs 防御等级运行时规则（原则 2）。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/FortificationDef", fileName = "FortificationDef")]
public class FortificationDef : ScriptableObject
{
    [Header("防御等级（3.6 §5.1）")]
    [Tooltip("防御等级（int，不设上限）。弹射物穿透 < 防御 → 不造成伤害；≥ → 造成伤害")]
    public int defenseLevel = 1;

    [Tooltip("近战打墙减免（等级函数，如 min(level×20%, 60%)；Lv1 20% / Lv2 40%）")]
    public float meleeDamageReduce = 0f;

    [Header("阻挡")]
    [Tooltip("挡移动（城墙/拒马=是，塔=否）")]
    public bool blocksMovement = false;

    [Tooltip("可通行（城门开合；false=常闭）")]
    public bool passable = false;

    [Tooltip("弹道高度（格）：拒马矮 0.5 / 城墙高 2 / 塔高 3。弹道弧高 > 本值 → 越墙")]
    public float heightCells = 1f;

    [Header("拒马减速（3.7 §4.4：拒马=减速带，不硬挡；敌方经过减速，友方正常）")]
    [Tooltip("拒马减速系数（0-1：0.5=半速。仅 Barricade 职业资产生效）")]
    public float barricadeSlowFactor = 0.5f;
    [Tooltip("拒马减速持续（秒：离开拒马格后恢复所需时间）")]
    public float barricadeSlowDuration = 0.5f;

    [Header("战斗")]
    [Tooltip("塔的弹药（箭塔=Arrow 等）；墙/拒马=null")]
    public AmmoDef ammo;

    [Tooltip("工事血量（可被消耗：近战 + 高穿透弹药）")]
    public int maxHp = 100;
}
