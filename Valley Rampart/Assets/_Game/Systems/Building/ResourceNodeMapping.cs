using System.Collections.Generic;

/// <summary>
/// 资源点↔工具建筑映射（3.3.4 批次6 资源点+工具模型）。
/// 资源点是前置（自身不产出），工具建筑激活后才产出。放置即改造。
/// 映射：采石场↔矿洞（木已无产能建筑 2_12，=一次性树）。
///
/// **HH.272 件⑤（死链路清理）**：`farm → BuildingType.Farmland` 映射已摘除——
/// 农田＝玩家建筑（建在可耕 Plain 上），**不是资源锚点**；原映射使 `IsResourceNodeAvailable(Farmland)`
/// 恒真（撞 `L-26` 家族）。两处同摘（本表 ＋ `WorldManager.IsResourceNodeAvailable`），
/// 否则农场将因 `needsNode=true` 却判 false 而**永久不可放置**（回归）。
/// </summary>
public static class ResourceNodeMapping
{
    private static readonly Dictionary<string, BuildingType> _toolToNode = new Dictionary<string, BuildingType>
    {
        { "quarry",     BuildingType.Mine },      // 采石场 -> 矿洞
    };

    /// <summary>工具建筑 id 对应的资源点 BuildingType（null=不需要资源点）。</summary>
    public static BuildingType? GetResourceNode(string toolId)
    {
        if (toolId != null && _toolToNode.TryGetValue(toolId, out var t)) return t;
        return null;
    }

    /// <summary>该建筑是否需要建在资源点上。</summary>
    public static bool RequiresResourceNode(string defId) => defId != null && _toolToNode.ContainsKey(defId);
}
