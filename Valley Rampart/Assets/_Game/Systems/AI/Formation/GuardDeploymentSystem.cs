using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 守卫锚点资源点（数据句柄，A+ HH.2 口径）。树/矿/矿脉均为 features 数据承载，
/// 仅 OreVein 保留 Building 实体；守卫锚点一律以格坐标 + feature 类型表示，不依赖实体。
/// LostEvent/守卫部署全部走本句柄，不再引用 Building（HH.3 §六 + HH.6 裁决二）。
/// </summary>
public struct GuardResourceNode
{
    /// <summary>资源点所在格坐标（features 派生的格，非实体世界坐标）。</summary>
    public GridCoord coord;
    /// <summary>资源点 feature 类型（Tree/Mine/OreVein，A+ 口径可部署集合）。</summary>
    public FeatureType feature;
    /// <summary>守卫归属阵营。</summary>
    public Faction faction;
    /// <summary>区域显示名（FeatureDisplayName 派生，供告警）。</summary>
    public string name;

    public GuardResourceNode(GridCoord coord, FeatureType feature, Faction faction, string name)
    {
        this.coord = coord;
        this.feature = feature;
        this.faction = faction;
        this.name = name;
    }
}

/// <summary>
/// 守卫覆盖区丢失事件（2_8 §5.2B D193/R4）。触发语义（HH.3 §六 + HH.6 裁决二重定义）：
/// 数据化后 Mine/Tree 无实体不再被"击退"，资源点"失去覆盖"判定改为两种：
///   (a) 守卫被击退/撤离（RemoveGuardRegion 显式触发，保留 OreVein 实体路径）；
///   (b) 资源点 feature 被消耗（TryConsumeResourceNode 覆盖为 Plain，由
///       GuardDeploymentSystem.HandleResourceConsumed 自动触发）。
/// 载荷为数据句柄 GuardResourceNode，非 Building。
/// </summary>
public readonly struct GuardRegionLostEvent
{
    public readonly GuardResourceNode ResourceNode;
    public GuardRegionLostEvent(GuardResourceNode resourceNode) { ResourceNode = resourceNode; }
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
    // 与 _regions 并行的资源点句柄（失守事件携带 GuardResourceNode；GuardRegion 只承载 rect+faction）。
    private static readonly List<GuardResourceNode> _nodes = new List<GuardResourceNode>();
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
    /// pos：玩家右键落点，就近吸附到高价值资源点（A+ 口径：features 的 Tree/Mine/OreVein 格）部署一个守卫区域。
    /// 已覆盖的资源点不重复部署（幂等）。
    /// </summary>
    public static void DeployGuard(Vector2 pos)
    {
        GuardResourceNode? node = FindNearestResourceNode(pos);
        if (node == null)
        {
            Debug.LogWarning("[GuardDeploymentSystem] DeployGuard 失败：落点附近无高价值资源点（features: Tree/Mine/OreVein）");
            return;
        }
        DeployGuardAt(node.Value);
    }

    /// <summary>直接对指定资源点句柄部署守卫区域（幂等：已有覆盖则忽略）。</summary>
    public static void DeployGuardAt(GuardResourceNode node)
    {
        var grid = GridSystem.Instance;
        if (grid == null) return;

        // 幂等：已覆盖的资源点不重复部署（按格坐标比对，兼容实体路径）
        for (int i = 0; i < _nodes.Count; i++)
            if (_nodes[i].coord == node.coord) return;

        GridCoord center = node.coord;

        float warnCells = Config != null ? Config.guardWarnRadiusCells : 5f;
        int r = Mathf.Max(1, Mathf.CeilToInt(warnCells));
        var region = new GuardRegion(
            new RectInt(center.x - r, center.y - r, r * 2 + 1, r * 2 + 1),   // 中心居中，含资源点所在格
            node.faction != Faction.None ? node.faction : Faction.Human_Player
        );
        _regions.Add(region);
        _nodes.Add(node);
        Debug.Log($"[GuardDeploymentSystem] 已部署守卫区域: {node.name} @ {center}（半径 {r} 格，阵营 {region.faction}）");
    }

    /// <summary>兼容入口：由 OreVein Building 实体构造资源点句柄后部署（OreVein 既有 feature 也有实体）。</summary>
    public static void DeployGuardAt(Building node)
    {
        if (node == null || !node.IsActive || node.def == null) return;
        var grid = GridSystem.Instance;
        if (grid == null) return;
        var coordOpt = grid.WorldToCoord(node.transform.position);
        if (coordOpt == null) return;
        GuardResourceNode h = new GuardResourceNode(
            coordOpt.Value, FeatureType.OreVein, node.faction,
            !string.IsNullOrEmpty(node.def.displayName) ? node.def.displayName : node.def.id
        );
        DeployGuardAt(h);
    }

    /// <summary>
    /// 守卫覆盖的资源点 feature 被消耗时调（A+ 口径：TryConsumeResourceNode 把 Tree/Mine 覆盖为 Plain）。
    /// 语义重定义：资源点"失去覆盖"判定之一——数据化后资源点被建筑覆盖即失去守卫意义，
    /// 移除守卫区域并触发 GuardRegionLostEvent（HH.3 §六 / HH.6 裁决二）。
    /// </summary>
    public static void HandleResourceConsumed(GridCoord coord)
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (_nodes[i].coord == coord)
            {
                RemoveGuardRegion(i);
                return;
            }
        }
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
        GuardResourceNode node = _nodes[index];
        GuardRegion region = _regions[index];
        _regions.RemoveAt(index);
        _nodes.RemoveAt(index);
        Debug.Log($"[GuardDeploymentSystem] 守卫区域丢失: {node.name} @ {region.rect}（威胁升级）");
        EventBus.Publish(new GuardRegionLostEvent(node));
    }

    /// <summary>按资源点格坐标移除守卫区域（失守判定的便捷入口）。</summary>
    public static void RemoveGuardRegion(GridCoord coord)
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (_nodes[i].coord == coord)
            {
                RemoveGuardRegion(i);
                return;
            }
        }
    }

    /// <summary>按 OreVein Building 实体移除守卫区域（兼容实体路径）。</summary>
    public static void RemoveGuardRegion(Building node)
    {
        if (node == null) return;
        var grid = GridSystem.Instance;
        if (grid == null) return;
        var coordOpt = grid.WorldToCoord(node.transform.position);
        if (coordOpt == null) return;
        RemoveGuardRegion(coordOpt.Value);
    }

    /// <summary>清理全部守卫区域（场景切换/重载时调）。</summary>
    public static void Clear()
    {
        _regions.Clear();
        _nodes.Clear();
    }

    // ===== A+ 口径：高价值资源点 = features 数据集的 Tree/Mine/OreVein 格（HH.2 数据化）=====

    /// <summary>该 feature 是否为可守卫资源点（A+ 口径：Tree/Mine/OreVein，Mine/Tree 亦可部署）。</summary>
    public static bool IsGuardResourceFeature(FeatureType f)
        => f == FeatureType.Tree || f == FeatureType.Mine || f == FeatureType.OreVein;

    /// <summary>feature → 守卫区域显示名（断名兜底：Tree→树木区/Mine→矿洞区/OreVein→矿脉区）。</summary>
    public static string FeatureDisplayName(FeatureType f)
    {
        switch (f)
        {
            case FeatureType.Tree:    return "树木区";
            case FeatureType.Mine:    return "矿洞区";
            case FeatureType.OreVein: return "矿脉区";
            default:                  return f.ToString();
        }
    }

    /// <summary>
    /// 查找 pos 最近的高价值资源点（A+ 口径：直查 WorldManager.ActiveMap.features 数据索引，
    /// Tree/Mine/OreVein），返回数据句柄而非 Building 实体（HH.3 §六 / HH.6 裁决二）。
    /// </summary>
    public static GuardResourceNode? FindNearestResourceNode(Vector2 pos)
    {
        var world = WorldManager.Instance;
        var map = world != null ? world.ActiveMap : null;
        var grid = GridSystem.Instance;
        if (map == null || map.features == null || grid == null) return null;

        GuardResourceNode? best = null;
        float bestSq = float.MaxValue;
        int width = map.width, height = map.height;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                FeatureType f = map.features[y * width + x];
                if (!IsGuardResourceFeature(f)) continue;
                var c = new GridCoord(x, y);
                float sq = ((Vector2)grid.CoordToWorld(c) - pos).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = new GuardResourceNode(c, f, Faction.Human_Player, FeatureDisplayName(f));
                }
            }
        }
        return best;
    }
}