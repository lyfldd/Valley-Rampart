using UnityEngine;

/// <summary>
/// 税收配置（2_17 步骤11 批1·AI 开征独立税率表）。
///
/// 语义迁移来源：KingdomConfig「税收系统」区块（headTaxPerPerson / commercialTaxPerLevel / lowHappinessTaxFloor）。
/// 因 HH.30 玩家零回归纪律，KingdomConfig 字段保留不删（HappinessSystem.GetTaxCoefficient 仍读它），
/// 本 SO 供 TaxSystem 报税读取；玩家(id=0) 分支用玩家税率，AI(id&gt;0) 分支用 AI 税率。
///
/// 资产路径：Resources/Config/TaxConfig.asset（Play Mode 用 Resources.Load 加载）；
/// 原生缺省值与 KingdomConfig 现值一致（0.5/1/0.5），保证资产缺失时玩家分支逐位等价。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/TaxConfig", fileName = "TaxConfig")]
public class TaxConfig : ScriptableObject
{
    [Header("税收系统（§六；2_17 步骤11 批1 从 KingdomConfig 迁移语义，占位）")]
    [Tooltip("玩家(id=0) 人头税（金/人口/日，§10 0.5；语义同 KingdomConfig.headTaxPerPerson）")]
    public float headTaxPerPerson = 0.5f;

    [Header("玩法已掌握-so-data-driven 铁律：新系统数据走 SO，禁止硬编码魔法数值")]
    [Tooltip("玩家(id=0) 商业建筑（市场）每级每日建筑税基数（语义同 KingdomConfig.commercialTaxPerLevel）")]
    public int commercialTaxPerLevel = 1;

    [Tooltip("幸福 0 时税收保底系数（§六 幸福0收0.5倍；当前仅玩家幸福系数用，Happiness 仍读 KingdomConfig）")]
    public float lowHappinessTaxFloor = 0.5f;

    [Header("AI(id>0) 税率（2_17 步骤11 批1 占位，批2 经济训练可调）")]
    [Tooltip("AI 人头税（金/工人/日，占位；用 kingdom.workerCount 作人口口径）")]
    public float headTaxPerPersonAI = 0.5f;

    [Tooltip("AI 商业建筑（市场）每级每日建筑税基数（占位）")]
    public int commercialTaxPerLevelAI = 1;
}