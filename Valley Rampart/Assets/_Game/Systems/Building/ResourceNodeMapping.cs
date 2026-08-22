using System.Collections.Generic;

/// <summary>
/// 资源点↔工具建筑映射（3.3.4 批次6 资源点+工具模型）。
/// 资源点是前置（自身不产出），工具建筑激活后才产出。放置即改造。
/// 映射：采石场↔矿洞、农场↔农田（木已无产能建筑 2_12，=一次性树）。
/// </summary>
public static class ResourceNodeMapping
{
    private static readonly Dictionary<string, BuildingType> _toolToNode = new Dictionary<string, BuildingType>
    {
        { "quarry",     BuildingType.Mine },      // 采石场 -> 矿洞
        { "farm",       BuildingType.Farmland },  // 农场 -> 农田
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
