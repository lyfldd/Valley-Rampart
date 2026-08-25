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

    // ===== 国库真源读 API（2_17 步骤 2a：KingdomState.resources 转正为 AI 国库台账）
    // 2_17 §〇 追记② 裁 B：AI 经济=台账制（与 P0 人口台账同哲学），独立于 RulerController/WarehouseRegistry/
    // TreasureVault（玩家物流专用）。语义镜像 PlayerRuler.CanAfford/Spend/Refund（弹药不参与造价，仅五经济资源）。
    // 2_17 前 AI 无脑不消费，本 API 由王国脑（步骤 8+）消费；确定性、无事件发布（台账制）。

    /// <summary>是否负担得起该资源包（五经济资源全部满足，原子校验）。弹药不参与。AI/动态王国国库查询。</summary>
    public bool CanAfford(ResourcePack cost)
    {
        return resources.gold >= cost.gold
            && resources.stone >= cost.stone
            && resources.wood >= cost.wood
            && resources.food >= cost.food
            && resources.metal >= cost.metal;
    }

    /// <summary>按类型读取国库某资源（军工/需求强度缺口函数消费）。</summary>
    public int GetResourceValue(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Gold: return resources.gold;
            case ResourceType.Stone: return resources.stone;
            case ResourceType.Wood: return resources.wood;
            case ResourceType.Food: return resources.food;
            case ResourceType.Metal: return resources.metal;
            default: return 0;
        }
    }

    /// <summary>扣除资源包（调用前需先 CanAfford；台账制直接减字段，不进玩家事件链）。</summary>
    public void Spend(ResourcePack cost)
    {
        resources.gold -= cost.gold;
        resources.stone -= cost.stone;
        resources.wood -= cost.wood;
        resources.food -= cost.food;
        resources.metal -= cost.metal;
    }

    /// <summary>按比例退还资源包（拆除退款 ratio=0.5 等；metal 随比退还，不静默丢铁）。</summary>
    public void Refund(ResourcePack cost, float ratio = 1.0f)
    {
        resources.gold += Mathf.RoundToInt(cost.gold * ratio);
        resources.stone += Mathf.RoundToInt(cost.stone * ratio);
        resources.wood += Mathf.RoundToInt(cost.wood * ratio);
        resources.food += Mathf.RoundToInt(cost.food * ratio);
        resources.metal += Mathf.RoundToInt(cost.metal * ratio);
    }

    /// <summary>国库入账（产出/采集入台账；加总）。</summary>
    public void AddResources(ResourcePack gain)
    {
        resources.gold += gain.gold;
        resources.stone += gain.stone;
        resources.wood += gain.wood;
        resources.food += gain.food;
        resources.metal += gain.metal;
    }
}