using UnityEngine;

/// <summary>
/// 王国运行时态（2_16 步骤1，D303/D311/D314/D385）。
/// 纯数据容器（非 MonoBehaviour），由 KingdomRegistry 持有。
/// 归属地基字段：id / name / bannerColor / foundedDay / personality 五轴 / 模板来源。
///
/// 五轴 personality（D311）：0=好战 1=经济 2=防守 3=扩张 4=外交，0~1 相互独立不归一化。
/// KingdomDef 句柄（步骤4 建 KingdomDef 后补）；王座锚点句柄（步骤5 挂 ThroneAnchor 后补）。
/// resources 过渡账本由 KingdomFoundry 在步骤5 写入——baseStockpile 占位，2_17 步骤2
/// WarehouseRegistry per-kingdom 化落地时迁移吸收（AI 在 2_17 前无脑不消费资源，零风险）。
/// </summary>
public class KingdomState
{
    /// <summary>王国唯一 id（0=玩家；单调递增不复用，D385）。</summary>
    public int id;

    /// <summary>国名（玩家接 KingdomManager.KingdomName，2_13 已落）。</summary>
    public string name;

    /// <summary>王旗色（染色数据，2_16 只出数据不渲染，渲染归 2_10）。</summary>
    public Color bannerColor;

    /// <summary>立国日（第一代=地图生成日；动态=插旗日）。</summary>
    public int foundedDay;

    /// <summary>性格五轴（0=好战 1=经济 2=防守 3=扩张 4=外交），0~1 独立不归一化（D311）。</summary>
    public float[] personality = new float[5];

    /// <summary>模板来源：第一代=KingdomDef id；-1=无来源（玩家 / 全无来源流民占位基线）；动态=来源国混合。</summary>
    public int templateSourceId = -1;

    /// <summary>是否为玩家王国（id=0，D303）。</summary>
    public bool IsPlayer => id == 0;

    /// <summary>起始国库过渡账本（baseStockpile D300；由 Foundry 步骤5 写入。2_17 步骤2 WarehouseRegistry per-kingdom 迁移吸收前，AI 无脑不消费，零风险）。</summary>
    public ResourcePack resources;

    /// <summary>人口台账（AI 王国无专属 Faction，P0 以台账计数落 worker/warrior 数，不实例化单位，避免污染玩家人口/单位系统；2_17 引入 AI 阵营后补单位实体）。</summary>
    public int workerCount;
    /// <summary>人口台账·战士数。</summary>
    public int warriorCount;

    /// <summary>读取某轴性格（越界返回 0.5 中性闭合，全无来源基线，D295 占位）。</summary>
    public float GetPersonality(int axis)
    {
        if (personality == null || axis < 0 || axis >= personality.Length) return 0.5f;
        return personality[axis];
    }
}