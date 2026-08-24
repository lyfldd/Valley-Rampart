using UnityEngine;

// ============================================================================
//  王国模板四维度定义（2_16 步骤4，§3.3 全量 / D290/D293/D315/D316）
//  占位数值全落 SO（so-data-driven 铁律），禁硬编码。
// ============================================================================

/// <summary>王国模板偏好特征物（D316 占位枚举：ForestDense 只建枚举不实现；本片仅按气候带放置）。</summary>
public enum KingdomPreferredFeature
{
    None,           // 无特征偏好（按气候带即可）
    RiverAdjacent,  // 河湾国：偏好落点邻河（P2 步骤12 实现）
    ForestDense     // 密林国：偏好高森林密度（P2 步骤12 实现，本片不实现）
}

/// <summary>
/// 王国模板（ScriptableObject，每模板一份，2_16 步骤4）。
/// 四维度：身份 / 地理 / 性格五轴 / 兵种 + 规模基准。
/// 规模基准（baseWorkers/baseWarriors/baseStockpile/baseBuildingDefIds）被错峰档 staggerTier 缩放（步骤5）。
/// 实例放 Resources/Config/Kingdoms/，由 KingdomTemplateLibrary 持有。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/Kingdoms/KingdomDef", fileName = "KingdomDef")]
public class KingdomDef : ScriptableObject
{
    // ===== 身份（D296 占位：namePool 动态立国命名池）=====
    [Header("身份")]
    [Tooltip("模板唯一 id（KingdomState.templateSourceId 关联；Registry 内唯一）")]
    public string templateName;
    [Tooltip("国名池（第一代从此抽取显示名；动态立国=来源国名+新词组合占位，D296）")]
    public string[] namePool;
    [Tooltip("王旗底色（染色数据，渲染归 2_10）")]
    public Color bannerColor;

    // ===== 地理（D288/D292/D298/D315/D316）=====
    [Header("地理")]
    [Tooltip("偏好气候带数组，按序匹配（D298）。优先在靠前的带内选点，末位失败回退最近带+日志（D292）")]
    public ClimateZone[] preferredClimates;
    [Tooltip("偏好特征物（D316 占位枚举；ForestDense 本片不实现，P2 步骤12 才做真实匹配）")]
    public KingdomPreferredFeature preferredFeature = KingdomPreferredFeature.None;

    // ===== 性格五轴（D305/D311：0~1 相互独立不归一化）=====
    [Header("性格五轴（索引 0=好战 1=经济 2=防守 3=扩张 4=外交）")]
    [Tooltip("好战 0~1")]
    public float militant = 0.5f;
    [Tooltip("经济 0~1")]
    public float economic = 0.5f;
    [Tooltip("防守 0~1")]
    public float defensive = 0.5f;
    [Tooltip("扩张 0~1")]
    public float expansionist = 0.5f;
    [Tooltip("外交 0~1")]
    public float diplomatic = 0.5f;

    // ===== 兵种侧重（D293；解锁态 per-kingdom 归 2_17 步骤11）=====
    [Header("兵种侧重")]
    [Tooltip("兵种技术标签（单位/建筑偏好，占位；2_17 步骤11 落地 per-kingdom 解锁态）")]
    public string[] preferredTechTags;
    [Tooltip("初始解锁列表（占位；本片不实现解锁态，随 2_17）")]
    public string[] initialUnlocks;

    // ===== 规模基准（D290/D293/D300：被错峰档 staggerTier 缩放，步骤5）=====
    [Header("规模基准（被错峰档缩放）")]
    [Tooltip("建筑基准清单（defId 数组占位；步骤5 按错峰档 buildingCount 取前 N 实例化）")]
    public string[] baseBuildingDefIds;
    [Tooltip("基准工人数（步骤5 按错峰档 scale 缩放）")]
    public int baseWorkers = 6;
    [Tooltip("基准战士数（步骤5 按错峰档 scale 缩放）")]
    public int baseWarriors = 2;
    [Tooltip("基准起始国库（baseStockpile 过渡账本，D300；错峰档再缩放，2_17 步骤2 WarehouseRegistry 迁移吸收）")]
    public ResourcePack baseStockpile;

    /// <summary>读取五轴性格（→ KingdomState.personality[5]，D311 独立不归一化）。</summary>
    public float[] GetPersonalityArray()
    {
        return new float[] { militant, economic, defensive, expansionist, diplomatic };
    }
}