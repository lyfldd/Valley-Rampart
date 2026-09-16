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
/// 交互入口（右键派兵驻守）=2_13 批B 已接线（SelectionController L175 → GuardDeploymentSystem.DeployGuard）；
/// 原「归 2_13」注记已过时（DZ-083b 勘正）。本篇落行为规则 + 双接口输出：
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
            node.faction != Faction.None ? node.faction : Faction.PlayerCamp
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

    /// <summary>清理全部守卫区域（场景切换/重载时调）。同时作废资源点索引（下次查询按需重建）。</summary>
    public static void Clear()
    {
        _regions.Clear();
        _nodes.Clear();
        _idxMap = null;
        _idxBuilt = false;
        _idxCells.Clear();
        _idxSeen.Clear();
    }

    // ===== A+ 口径：高价值资源点 = features 数据集的 Tree/Mine/OreVein 格（HH.2 数据化）=====

    // 【HH.294 补正 P1】资源点**索引**（右键派兵不再全图扫 features）
    //   改造前：`FindNearestResourceNode` 嵌套 for y<height / x<width 全扫 `map.features`
    //     （大图 384² = 147,456 格；`底层执行计划` 片3 若把 features 下移小格子则 ×16 = 2,359,296），
    //     且**每命中资源格**还调 `CoordToWorld`。触发频率＝**每次玩家右键派兵**
    //     （`SelectionController.cs:270` 判 `nearResource` ＋ `:279` `DeployGuard` 内再查一次 ⇒ 一次右键走两遍）。
    //   改造后：索引＝「**曾出现过可守卫资源点的格号**」（地块级，与地图面积解耦）；查询只遍历索引条目，
    //     **逐条回读实时 `features` 校验**（被消耗 ⇒ 跳过）；候选世界坐标**建索引时预换算**，
    //     查询内距离式与改造前**逐位同式**（`dx*dx + dy*dy`）。
    //   ⭐ 超集不变量（为什么索引只增不减仍正确）：运行期对 `features` 的资源点写入只有两处 ——
    //     `WorldManager.TryConsumeResourceNode:241`（资源→Plain，消耗）与
    //     `ResourceRespawnSystem.SetFeature:178`（重生写回**原格**）；前者由回读校验兜住，
    //     后者经 `NotifyFeatureWritten` 增量登记 ⇒ 索引恒为「当前可守卫资源点格」的**超集**，
    //     取最近结果与全扫**完全一致**（并列时按格号小者胜＝改造前 y*W+x 扫描序先者胜）。
    private static MapData _idxMap;
    private static bool _idxBuilt;
    private static readonly List<int> _idxCells = new List<int>();      // 候选格号（y*w+x），升序＝扫描序
    private static readonly List<Vector2> _idxWorld = new List<Vector2>();  // 与 _idxCells 并行：候选格世界坐标（建索引时一次换算，免每次查询重算）
    private static readonly HashSet<int> _idxSeen = new HashSet<int>();

    /// <summary>索引条目数（观测读口；＝一次 `FindNearestResourceNode` 的遍历上界）。</summary>
    public static int IndexedResourceCells => _idxCells.Count;

    /// <summary>按图重建资源点索引（幂等：同图已建则直接返回）。**内存装载期调一次**（`WorldManager` 建图后），
    /// 亦作懒兜底（探针/其他入口首查时自建）。代价＝单次全图 `features` 扫（1 次/图）。</summary>
    public static void RebuildResourceIndex(MapData map)
    {
        if (map == null || map.features == null) return;
        _idxMap = map;
        _idxBuilt = true;
        _idxCells.Clear();
        _idxWorld.Clear();
        _idxSeen.Clear();
        var f = map.features;
        int w = map.width;
        var grid = GridSystem.Instance;
        for (int i = 0; i < f.Length; i++)
            if (IsGuardResourceFeature(f[i]) && _idxSeen.Add(i)) AddEntry(i, w, grid);
    }

    /// <summary>【HH.294 补正 P1】`features` 写入登记（保证索引超集性）。
    /// 仅「非资源点 → 可守卫资源点」的写入需登记（重生写回原格）；消耗无需登记（回读会跳过）。</summary>
    public static void NotifyFeatureWritten(GridCoord cell, FeatureType f)
    {
        if (!_idxBuilt) return;                                   // 未建：后续首查整表重建，自然含该格
        var world = WorldManager.Instance;
        var map = world != null ? world.ActiveMap : null;
        if (map == null || map != _idxMap || map.features == null) return;   // 他图/换图：下次首查重建
        if (!IsGuardResourceFeature(f)) return;
        int i = cell.y * map.width + cell.x;
        if (i < 0 || i >= map.features.Length) return;
        if (_idxSeen.Add(i)) AddEntry(i, map.width, GridSystem.Instance);
    }

    /// <summary>追加一条候选（格号 ＋ 预换算世界坐标，与 `CoordToWorld` 同式）。</summary>
    private static void AddEntry(int idx, int width, GridSystem grid)
    {
        _idxCells.Add(idx);
        _idxWorld.Add(grid != null ? grid.CoordToWorld(new GridCoord(idx % width, idx / width)) : Vector2.zero);
    }

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
    /// 查找 pos 最近的高价值资源点（A+ 口径：`features` 的 Tree/Mine/OreVein），返回数据句柄而非 Building 实体
    /// （HH.3 §六 / HH.6 裁决二）。
    /// **【HH.294 补正 P1】按索引查询**：遍历上界＝`IndexedResourceCells`（索引条目数，地块级、与地图面积解耦），
    /// 不再全图扫 `W×H`；每条**回读实时 features** 校验（消耗即跳过），结果与全扫一致（并列取格号小者＝原扫描序先者）。
    /// </summary>
    public static GuardResourceNode? FindNearestResourceNode(Vector2 pos)
    {
        var world = WorldManager.Instance;
        var map = world != null ? world.ActiveMap : null;
        var grid = GridSystem.Instance;
        if (map == null || map.features == null || grid == null) return null;

        if (!_idxBuilt || _idxMap != map) RebuildResourceIndex(map);   // 懒兜底（正常由建图后预建）

        GuardResourceNode? best = null;
        float bestSq = float.MaxValue;
        int bestIdx = int.MaxValue;
        int width = map.width;
        var cells = _idxCells;
        var worlds = _idxWorld;
        for (int k = 0; k < cells.Count; k++)
        {
            int i = cells[k];
            if (i < 0 || i >= map.features.Length) continue;
            FeatureType f = map.features[i];              // 实时回读：功能源仍是 features，索引只作候选集
            if (!IsGuardResourceFeature(f)) continue;
            Vector2 wp = worlds[k];
            float dx = wp.x - pos.x, dy = wp.y - pos.y;   // 与改造前 ((Vector2)CoordToWorld(c) - pos).sqrMagnitude **逐位同式**
            float sq = dx * dx + dy * dy;
            if (sq < bestSq || (sq == bestSq && i < bestIdx))
            {
                bestSq = sq;
                bestIdx = i;
                best = new GuardResourceNode(new GridCoord(i % width, i / width), f, Faction.PlayerCamp, FeatureDisplayName(f));
            }
        }
        return best;
    }
}