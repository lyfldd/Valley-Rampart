using UnityEngine;

// ============================================================================
//  RaceDef 种族模板 SO（2_20 实施清单 M1 / 总纲 §五.1 D420/D421/D426/D429）
//  字段权威=2_20.1 §二「经济乘数挂载点映射表」（唯一映射权威）+§四「五轴出厂基准占位表」。
//  全部数值进 SO（so-data-driven 铁律），禁散落硬编码；消费侧读取点见 2_20.1 §二（M5/M8/M6/M7 逐点实装）。
//  占位口径（2_20.1 §6.1）：全部数值为占位，P0 端到端调优后回调。
//    缺值挂账（HH.54 §三.2，2026-09-03）：2_20.1 无完整各族乘数值表——除两条散点锚
//    （兽人 trainCostMul 0.85=2_20.1 §二注 / 矮人 mineMul 1.30=2_20.1 §三熔炉行）外，
//    其余乘数一律 1.0 中性占位（语义=该乘数暂不生效），真值表待策划端补批后单点回填。
//  资产：Resources/Config/Races/（Race_Human/Race_Elf/Race_Dwarf/Race_Orc）。
// ============================================================================

/// <summary>
/// 种族模板（ScriptableObject，每族一份，2_20 M1）。
/// 王国级机械消费载体：AI 王国出生定族/玩家开局选族 → per-kingdom 挂 RaceDef（总纲 §五.1）；
/// 个体 raceId 人口属性不走本 SO（D467，UnitController.raceId 终身字段，种族1 已落）。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/Races/RaceDef", fileName = "RaceDef")]
public class RaceDef : ScriptableObject
{
    // ===== 身份 =====
    [Header("身份")]
    [Tooltip("种族 id（int 空间对齐 RaceIds 常量：0=Human 1=Elf 2=Dwarf 3=Orc，与 2_13 M10 选族索引同空间）")]
    public int raceId = RaceIds.Human;
    [Tooltip("种族显示名（调试/日志用，勿作逻辑分支依据）")]
    public string raceName;

    // ===== 性格五轴出厂基准（D426：0~1 相互独立不归一化；KingdomDef per-kingdom 扰动在其上偏离）=====
    [Header("性格五轴出厂基准（索引 0=好战 1=经济 2=防守 3=扩张 4=外交；D426）")]
    [Tooltip("好战基准 0~1（消费=M8 王国脑策略倾向；玩家 Kingdom 五轴不参与 AI 消费，纯数据）")]
    public float militant = 0.5f;
    [Tooltip("经济基准 0~1")]
    public float economic = 0.5f;
    [Tooltip("防守基准 0~1")]
    public float defensive = 0.5f;
    [Tooltip("扩张基准 0~1")]
    public float expansionist = 0.5f;
    [Tooltip("外交基准 0~1")]
    public float diplomatic = 0.5f;

    /// <summary>读取五轴出厂基准（→ D426 合并逻辑：基准×KingdomDef 扰动，M8 实装；数组序对齐 KingdomDef.GetPersonalityArray）。</summary>
    public float[] GetBaselinePersonalityArray()
    {
        return new float[] { militant, economic, defensive, expansionist, diplomatic };
    }

    // ===== 军事修正（字段权威=2_20.1 §二；共通职业数值 ×族修正，1.0=中性）=====
    [Header("军事修正（×乘，1.0=中性；消费点见 2_20.1 §二）")]
    [Tooltip("军训成本%（训练入口扣费结算处；兽人 0.85=散点锚）")]
    public float trainCostMul = 1.0f;
    [Tooltip("训练时长%（<1 加速；训练时长计算处；战争学院全局-25% 在此叠乘）")]
    public float trainSpeedMul = 1.0f;
    [Tooltip("近战攻%（DamagePipeline 攻方 ATK 生成处，近战路径）")]
    public float meleeAtkMul = 1.0f;
    [Tooltip("远程攻%（DamagePipeline 攻方 ATK 生成处，远程路径——近战远程分离纪律）")]
    public float rangedAtkMul = 1.0f;
    [Tooltip("移速%（UnitController 移速初始化，MovementConfig 叠加）")]
    public float moveSpeedMul = 1.0f;

    // ===== 经济修正（字段权威=2_20.1 §二；1.0=中性）=====
    [Header("经济修正（×乘，1.0=中性；消费点见 2_20.1 §二）")]
    [Tooltip("采矿%（TaskScheduler 生产 Tick+采集入库两处同乘；地脉熔炉 +40% 同点叠加；矮人 1.30=散点锚）")]
    public float mineMul = 1.0f;
    [Tooltip("伐木%（同 mineMul 消费点）")]
    public float lumberMul = 1.0f;
    [Tooltip("粮产%（同 mineMul 消费点）")]
    public float farmMul = 1.0f;
    [Tooltip("建造速度%（协作施工 tick 进度增量处，2_12 建造链）")]
    public float buildSpeedMul = 1.0f;
    [Tooltip("建筑血量%（Building.Init 初始化乘点；已建成建筑不吃追溯，新建生效）")]
    public float buildingHpMul = 1.0f;
    [Tooltip("携带上限%（工人 ResourceCarryConfig 叠加，TaskScheduler 卸货处）")]
    public float carryCapMul = 1.0f;

    // ===== 地形偏好（D429 特征物载体；匹配实现归 2_16 D316 P2 步骤12，本批字段就位不生效）=====
    [Header("地形偏好（D429 特征物枚举；真实匹配=P2 步骤12，本批只挂配置）")]
    [Tooltip("偏好特征物（人类无偏好=None；精灵=ForestDense 矮人=MineralRich 兽人=BarrenRich）")]
    public KingdomPreferredFeature preferredFeature = KingdomPreferredFeature.None;
    [Tooltip("偏好特征物区产出加成%（软偏好；非偏好区无惩罚。P2 步骤12 前恒不加成）")]
    public float gatherBonusOnPreferred = 0f;

    // ===== 专属内容引用（总纲 §五.1；M6/M7 落资产后在此挂接）=====
    [Header("专属内容引用")]
    [Tooltip("专属建筑引用（每族 1 栋 D419：人类战争学院/兽人战营/矮人地脉熔炉/精灵射箭场；M6 落资产后挂接，现 null 挂账）")]
    public BuildingDef exclusiveBuildingDef;
    [Tooltip("专属兵种引用数组（NpcProfessionDef，每族 2 个 D490 修订版+机器 4 台 D496/D497 归 exclusiveUnitDefs 或机器位另挂，M7 定型；现空挂账）")]
    public NpcProfessionDef[] exclusiveUnitDefs;
}
