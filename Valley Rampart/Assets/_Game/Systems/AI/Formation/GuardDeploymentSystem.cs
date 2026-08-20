using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 守卫覆盖区丢失事件（2_8 §5.2B D193/R4）。守卫被击退/资源点失守时发布，
/// 威胁升级消费方：（原"破城判定"改此，D63 围合移除后改守卫区域告警）。
/// </summary>
public readonly struct GuardRegionLostEvent
{
    public readonly Building ResourceNode;
    public GuardRegionLostEvent(Building resourceNode) { ResourceNode = resourceNode; }
}

/// <summary>
/// 守卫区域（2_8 步骤6 输出，D265 双接口；GuardRegionChangedEvent 成本场订阅消费的矩形载体）。
/// 守卫覆盖区围绕高价值资源点（Building.def.isResourceNode）。
/// </summary>
[System.Serializable]
public struct GuardRegion
{
    /// <summary>守卫覆盖矩形（格坐标，围绕资源点警戒半径）</summary>
    public RectInt rect;
    /// <summary>产出/守卫归属阵营（谁工人多/守卫强谁占优，产出归在场工人阵营）</summary>
    public Faction faction;

    public GuardRegion(RectInt rect, Faction faction) { this.rect = rect; this.faction = faction; }

    /// <summary>该格是否落在守卫覆盖区内。</summary>
    public bool Contains(GridCoord coord) => rect.Contains(new Vector2Int(coord.x, coord.y));
}

/// <summary>
/// 守卫部署系统（2_8 步骤6，§5.2B D190~D195）。守卫部署与资源点争夺的行为规则层。
///
/// 交互入口（右键派兵驻守）归 2_13；本篇落行为规则 + 双接口输出：
///   - IsGuarded(GridCoord)->bool            （2_7 安全度/成本场守卫低点 D86 消费）
///   - GetGuardRegions()->IReadOnlyList       （2_6 成本场订阅 GuardRegionChangedEvent 消费）
/// 守卫丢失（守卫被击退/资源点失去覆盖）→ 发 GuardRegionLostEvent 威胁升级（R4/D63）。
///
/// 首版为数据跟踪服务（无 MonoBehaviour 生命周期），部署入口由脚本化/debug 驱动；
/// 守卫点行为（自动迎战 D83）交由已有攻击链路（NPCBrain.UpdateCombatRegistration）处理。
/// 风险回退（§六）：守卫系统未接入 2_13 入口 → IsGuarded 默认 false（无守卫区域），行为不破坏。
/// </summary>
public static class GuardDeploymentSystem
{
    private static readonly List<GuardRegion> _regions = new List<GuardRegion>();
    // 与 _regions 并行的资源点引用（失守事件需要 Building；GuardRegion 只承载 rect+faction）。
    private static readonly List<Building> _nodes = new List<Building>();
    private static GuardConfig _config;
    private static GuardConfig Config => _config != null ? _config : (_config = GuardConfig.Instance ?? CreateDefault());

    private static GuardConfig CreateDefault()
    {
        var cfg = ScriptableObject.CreateInstance<GuardConfig>();
        _config = cfg;
        return cfg;
    }

    /// <summary>当前守卫区域数。</summary>
    public static int Count => _regions.Count;

    /// <summary>全部守卫区域（D265 输出，双接口之一）。</summary>
    public static IReadOnlyList<GuardRegion> GetGuardRegions() => _regions;

    /// <summary>
    /// 该格是否处于任一守卫覆盖区（2_7 安全度消费，D86 成本场守卫低点）。
    /// </summary>
    public static bool IsGuarded(GridCoord coord)
    {
        for (int i = 0; i < _regions.Count; i++)
            if (_regions[i].Contains(coord)) return true;
        return false;
    }

    /// <summary>该格是否处于指定阵营的守卫覆盖区。</summary>
    public static bool IsGuardedBy(GridCoord coord, Faction faction)
    {
        for (int i = 0; i < _regions.Count; i++)
            if (_regions[i].faction == faction && _regions[i].Contains(coord)) return true;
        return false;
    }

    /// <summary>
    /// 部署守卫入口（2_13 右键派兵护栏最终接入；首版脚本化/debug 驱动）。
    /// pos：玩家右键落点，就近吸附到高价值资源点（def.isResourceNode）部署一个守卫区域。
    /// 已覆盖的资源点不重复部署（幂等）。
    /// </summary>
    public static void DeployGuard(Vector2 pos)
    {
        Building node = FindNearestResourceNode(pos);
        if (node == null)
        {
            Debug.LogWarning("[GuardDeploymentSystem] DeployGuard 失败：落点附近无高价值资源点（def.isResourceNode）");
            return;
        }
        DeployGuardAt(node);
    }

    /// <summary>直接对指定资源点部署守卫区域（幂等：已有覆盖则忽略）。</summary>
    public static void DeployGuardAt(Building node)
    {
        if (node == null || !node.IsActive) return;
        var grid = GridSystem.Instance;
        if (grid == null) return;

        // 幂等：已覆盖的资源点不重复部署
        for (int i = 0; i < _nodes.Count; i++)
            if (ReferenceEquals(_nodes[i], node)) return;

        var centerOpt = grid.WorldToCoord(node.transform.position);
        if (centerOpt == null) return;
        GridCoord center = centerOpt.Value;

        float warnCells = Config != null ? Config.guardWarnRadiusCells : 5f;
        int r = Mathf.Max(1, Mathf.CeilToInt(warnCells));
        var region = new GuardRegion(
            new RectInt(center.x - r, center.y - r, r * 2 + 1, r * 2 + 1),   // 中心居中，含资源点所在格
            node.faction != Faction.None ? node.faction : Faction.Human_Player
        );
        _regions.Add(region);
        _nodes.Add(node);
        Debug.Log($"[GuardDeploymentSystem] 已部署守卫区域: {node.def?.id} @ {center}（半径 {r} 格，阵营 {region.faction}）");
    }

    /// <summary>
    /// 移除守卫区域（守卫被击退/资源点失效时调，触发守卫丢失威胁升级）。占位实现。
    /// index：GetGuardRegions() 索引。
    /// </summary>
    public static void RemoveGuardRegion(int index)
    {
        if (index < 0 || index >= _regions.Count)
        {
            Debug.LogWarning("[GuardDeploymentSystem] RemoveGuardRegion：索引越界");
            return;
        }
        Building node = _nodes[index];
        GuardRegion region = _regions[index];
        _regions.RemoveAt(index);
        _nodes.RemoveAt(index);
        Debug.Log($"[GuardDeploymentSystem] 守卫区域丢失: {node?.def?.id} @ {region.rect}（威胁升级）");
        if (node != null)
            EventBus.Publish(new GuardRegionLostEvent(node));
    }

    /// <summary>按资源点引用移除守卫区域（失守判定的便捷入口）。</summary>
    public static void RemoveGuardRegion(Building node)
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (ReferenceEquals(_nodes[i], node))
            {
                RemoveGuardRegion(i);
                return;
            }
        }
    }

    /// <summary>清理全部守卫区域（场景切换/重载时调）。</summary>
    public static void Clear()
    {
        _regions.Clear();
        _nodes.Clear();
    }

    /// <summary>查找 pos 最近的高价值资源点（def.isResourceNode 且 Active）。</summary>
    public static Building FindNearestResourceNode(Vector2 pos)
    {
        if (BuildingRegistry.Instance == null) return null;
        Building best = null;
        float bestSq = float.MaxValue;
        foreach (var b in BuildingRegistry.Instance.All)
        {
            if (b == null || b.def == null || !b.def.isResourceNode || !b.IsActive) continue;
            float sq = ((Vector2)b.transform.position - pos).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = b; }
        }
        return best;
    }
}