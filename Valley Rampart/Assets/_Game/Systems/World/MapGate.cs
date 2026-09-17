using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⭐ <b>地图四门</b>（`03_地图即数据库` §六~§九 · `HH.294` 片 4）。
///
/// <b>为什么存在</b>：地图不是「一堆对象」，是「一张表」。`MapData.features` 是**唯一功能源**，
/// 任何代码**裸读写它**都会让「同一件事两个源」——本类把读写收口到四个门。
///
/// <b>四门</b>：
/// <list type="bullet">
///   <item><b>增</b> <see cref="PlaceResourceNode"/>（重生时机；造世界 0→1 走 <see cref="GenesisWrite"/>）</item>
///   <item><b>删</b> <see cref="RemoveResourceNode"/>（**唯一入口，不区分死因**：采走／被覆盖一视同仁）</item>
///   <item><b>查</b> <see cref="TryGetCell"/>／<see cref="QueryCells"/>／<see cref="QueryOccupants"/>／<see cref="CountFeatures"/></item>
///   <item><b>改</b> <see cref="SetFeature"/>（字段变更）／<see cref="ReplaceFeature"/>（存在替换·对外**只发一条** `Replaced`）</item>
/// </list>
///
/// <b>⛔ 五条不许</b>（`04_上层接入指南` §六）：上层不得直接摸 `map.features[]`；不得自己算可走；
/// 不得把上层概念当条件；不得要求下层加专用接口；属性变化不走下层。
///
/// <b>可走不是「改」的对象</b>（`03` §9.2）：它是**派生缓存**（地表＋占格＋桥）⇒ 改它的**来源**，
/// 由 <see cref="GridSystem.RefreshCellFromFeature"/> 重算。**禁开「改可走」入口**。
/// </summary>
public static class MapGate
{
    // ==========================================================================
    //  0. 写内核 —— ⭐ 全库**唯一** `map.features[...] = ` 赋值点（生产码）
    //     判据：`git grep -n -E "\.features\s*\[[^]]*\]\s*=[^=]" -- 'Assets/_Game/**/*.cs'`
    //     改后应只命中本节（1 行）。
    // ==========================================================================

    /// <summary>⭐ 唯一写口。除本行外，生产码（`Assets/_Game/**`）**无任何** `features[...] =` 赋值。</summary>
    private static void WriteRaw(MapData map, int index, FeatureType f) => map.features[index] = f;

    /// <summary>
    /// <b>造世界专用口（0→1）</b>：`03` §6.1「造地形／气候／山脉**不算增**」——
    /// 生成规则（连通／地块大小／山脉）**不污染增的规则表**，故单列此口，仅 `MapGenRules` 生成期使用。
    /// ⛔ 运行期任何代码**不得**走此口（运行期一律走四门）。
    /// </summary>
    public static void GenesisWrite(MapData map, int index, FeatureType f)
    {
        if (map == null || map.features == null) return;
        if (index < 0 || index >= map.features.Length) return;
        WriteRaw(map, index, f);
    }

    /// <summary><b>指定图的读口</b>（带 `map` 参数）：供**拿得到 map 的调用方**（渲染铺格 / 生成管线）走门，
    /// 避免它们裸读 `map.features[...]`。判据 2 的「上层裸读面」由此收口。</summary>
    public static FeatureType ReadAt(MapData map, int x, int y)
    {
        if (map == null || map.features == null) return FeatureType.Plain;
        if (x < 0 || y < 0 || x >= map.width || y >= map.height) return FeatureType.Plain;
        int i = y * map.width + x;
        return i >= 0 && i < map.features.Length ? map.features[i] : FeatureType.Plain;
    }

    /// <summary><b>造世界专用读口（0→1）</b>：生成管线内 `map` 尚未挂进 `WorldManager._world`（<see cref="ActiveMap"/> 为 null），
    /// 故生成期一律用带 `map` 参数的口，别走运行期门。实现与 <see cref="ReadAt"/> 同源（同一处读）。</summary>
    public static FeatureType GenesisRead(MapData map, int x, int y) => ReadAt(map, x, y);

    /// <summary><b>轻量单格读口</b>（§8.2 ①）：只要「这格是什么」，不带可走/占格/归属等事实 ⇒ O(1) 无分配。
    /// 与 <see cref="TryGetCell"/>（复合结构）分工：批量铺格／逐格扫描走本口，单点问全貌走复合口。</summary>
    public static FeatureType GetFeatureAt(int x, int y)
    {
        var map = ActiveMap;
        if (map == null || map.features == null) return FeatureType.Plain;
        if (x < 0 || y < 0 || x >= map.width || y >= map.height) return FeatureType.Plain;
        int i = y * map.width + x;
        return i >= 0 && i < map.features.Length ? map.features[i] : FeatureType.Plain;
    }

    /// <summary><b>轻量单格读口</b>（GridCoord 版）。</summary>
    public static FeatureType GetFeatureAt(GridCoord c) => GetFeatureAt(c.x, c.y);

    /// <summary>当前活动地图的尺寸（上层枚举前先问界；越界查询一律返回 Plain/false，不抛）。</summary>
    public static int MapWidth => ActiveMap != null ? ActiveMap.width : 0;
    public static int MapHeight => ActiveMap != null ? ActiveMap.height : 0;

    // ==========================================================================
    //  1. 查门（§八 · 五个基础件之 ①②③）
    // ==========================================================================

    /// <summary>一格的全部事实（§8.2 ① 格子属性 ＋ ② 格子上的存在）。**不返回可写引用**（判据 5）。</summary>
    public struct CellInfo
    {
        public GridCoord coord;
        public FeatureType feature;      // 地表物（唯一功能源）
        public ClimateZone climate;      // 温度带
        public bool walkable;            // 可走（派生缓存，来自 GridSystem）
        public int ownerKingdomId;       // 领土归属（-1 ＝ 无主）
        public bool hasOccupant;         // 格上有没有东西
        public bool occupantIsObstacle;  // 该东西阻不阻挡
        public int unitCount;            // 格上单位数

        public bool HasFeature(FeatureType f) => feature == f;
    }

    /// <summary>区域枚举条件（§8.3 条件＝选择器：勾选事实 ＋ 限定参数 ＋ 数量）。
    /// ⛔ 条件**只用下层知道的事实**——上层概念（敌人／威胁／值钱）由**上层翻译**成事实组合。</summary>
    public struct CellFilter
    {
        public bool useFeature;          // 勾「地表＝某型」
        public FeatureType feature;
        public bool useWalkable;         // 勾「可走／不可走」
        public bool walkable;
        public bool requireEmpty;        // 勾「格上无东西」
        public bool requireOccupied;     // 勾「格上有东西」

        public static CellFilter Feature(FeatureType f) => new CellFilter { useFeature = true, feature = f };
        public static CellFilter Walkable(bool walkable) => new CellFilter { useWalkable = true, walkable = walkable };
        public static CellFilter Empty() => new CellFilter { requireEmpty = true };

        public bool Accept(in CellInfo c)
        {
            if (useFeature && c.feature != feature) return false;
            if (useWalkable && c.walkable != walkable) return false;
            if (requireEmpty && c.hasOccupant) return false;
            if (requireOccupied && !c.hasOccupant) return false;
            return true;
        }
    }

    /// <summary>⭐<b>「一次拿全一格」</b>（§8.2 ①②）：一格要的全部事实一次问完，上层不再拼 3~5 个调用。
    /// 越界／地图未就绪 ⇒ false。**返回复合结构（值），不返回可写引用**。</summary>
    public static bool TryGetCell(GridCoord coord, out CellInfo info)
    {
        info = default;
        var map = ActiveMap;
        var grid = GridSystem.Instance;
        if (map == null || grid == null) return false;
        if (coord.x < 0 || coord.y < 0 || coord.x >= map.width || coord.y >= map.height) return false;

        int i = coord.y * map.width + coord.x;
        info.coord = coord;
        info.feature = map.features != null && i < map.features.Length ? map.features[i] : FeatureType.Plain;
        info.climate = map.climateZones != null && i < map.climateZones.Length ? map.climateZones[i] : ClimateZone.Temperate;
        info.walkable = grid.IsWalkable(coord);
        var occ = grid.GetOccupant(coord);
        info.hasOccupant = occ != null;
        info.occupantIsObstacle = occ != null && occ.IsGridObstacle;
        info.ownerKingdomId = OwnerAt(coord);
        info.unitCount = grid.GetUnitCountInCell(coord);   // 无分配（§8.7 判据 4）
        return true;
    }

    /// <summary>⭐<b>区域枚举</b>（§8.2 ③）：范围（矩形）＋ 条件 ＋ 输出 buffer（**禁分配**：buffer 由调用方复用）。
    /// 返回写入条数。原 `FillUnitsInRect` 只服务单位 ⇒ 本口**通用**（地表／可走／空置／有物皆可筛）。</summary>
    public static int QueryCells(RectInt rect, CellFilter filter, List<CellInfo> buffer)
        => QueryCells(rect, filter, buffer, 0);

    /// <summary>⭐<b>区域枚举 · 数量旋钮</b>（§8.3「数量（全部／一个／N）」）：<paramref name="maxCount"/> ≤ 0 ⇒ 不限。</summary>
    public static int QueryCells(RectInt rect, CellFilter filter, List<CellInfo> buffer, int maxCount)
    {
        if (buffer == null) return 0;
        var map = ActiveMap;
        if (map == null) return 0;
        int before = buffer.Count;
        int x0 = Mathf.Max(0, rect.xMin), y0 = Mathf.Max(0, rect.yMin);
        int x1 = Mathf.Min(map.width, rect.xMax), y1 = Mathf.Min(map.height, rect.yMax);
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                if (maxCount > 0 && buffer.Count - before >= maxCount) return buffer.Count - before;
                if (!TryGetCell(new GridCoord(x, y), out var info)) continue;
                if (!filter.Accept(in info)) continue;
                buffer.Add(info);
            }
        return buffer.Count - before;
    }

    /// <summary>⭐<b>区域枚举 · 半径范围</b>（§8.4「矩形＋半径两种」；半径版＝视野／爆炸）。
    /// 半径按**切比雪夫距离**（方形窗口，与等轴菱形格网的「环形」一致）；范围与条件同 <see cref="QueryCells"/>。</summary>
    public static int QueryCellsRadius(GridCoord center, int radius, CellFilter filter, List<CellInfo> buffer, int maxCount = 0)
        => QueryCells(new RectInt(center.x - radius, center.y - radius, radius * 2 + 1, radius * 2 + 1),
                      filter, buffer, maxCount);

    /// <summary>按**实体**枚举（§8.5 两个数据组之「按实体」）：区域内占格物引用（建筑／传送门等）。
    /// 返回写入条数；同一实体跨多格只收一次。</summary>
    public static int QueryOccupants(RectInt rect, List<IGridOccupant> buffer)
    {
        if (buffer == null) return 0;
        var map = ActiveMap;
        var grid = GridSystem.Instance;
        if (map == null || grid == null) return 0;
        int before = buffer.Count;
        int x0 = Mathf.Max(0, rect.xMin), y0 = Mathf.Max(0, rect.yMin);
        int x1 = Mathf.Min(map.width, rect.xMax), y1 = Mathf.Min(map.height, rect.yMax);
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                var occ = grid.GetOccupant(new GridCoord(x, y));
                if (occ == null) continue;
                bool dup = false;
                for (int k = before; k < buffer.Count; k++)
                    if (ReferenceEquals(buffer[k], occ)) { dup = true; break; }
                if (!dup) buffer.Add(occ);
            }
        return buffer.Count - before;
    }

    /// <summary>计数（§8.3「⭐ 计数：这片里有几个 X（**只要数、不要列表**）」）。**派生量，不入格表**。</summary>
    public static int CountFeatures(RectInt rect, FeatureType f)
    {
        var map = ActiveMap;
        if (map == null) return 0;
        int n = 0;
        int x0 = Mathf.Max(0, rect.xMin), y0 = Mathf.Max(0, rect.yMin);
        int x1 = Mathf.Min(map.width, rect.xMax), y1 = Mathf.Min(map.height, rect.yMax);
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                if (map.features[y * map.width + x] == f) n++;
        return n;
    }

    // ==========================================================================
    //  2. 改门（§九）
    // ==========================================================================

    /// <summary>
    /// <b>字段变更</b>（§9.1 第二条腿）：地表是一个**值**，直接覆写，不需要删增。
    /// ⚠️ 单值字段**只有两个**（归属／地表）；本口只管地表，归属走 `TerritorySystem`。
    /// ⛔ 不提供「改可走」——可走是派生缓存，本口只重算它。
    /// 返回是否实际改变（同值 ⇒ false，幂等）。
    /// </summary>
    public static bool SetFeature(GridCoord coord, FeatureType f)
    {
        var map = ActiveMap;
        var grid = GridSystem.Instance;
        if (map == null || map.features == null || grid == null) return false;
        if (coord.x < 0 || coord.y < 0 || coord.x >= map.width || coord.y >= map.height) return false;
        int i = coord.y * map.width + coord.x;
        if (i < 0 || i >= map.features.Length) return false;
        if (map.features[i] == f) return false;

        WriteRaw(map, i, f);
        GuardDeploymentSystem.NotifyFeatureWritten(coord, f);   // 索引超集性（HH.294 补正 P1）
        grid.RefreshCellFromFeature(coord, f);                  // 派生缓存重算（可走）
        if (MapRenderService.Instance != null) MapRenderService.Instance.UpdateCell(coord);
        return true;
    }

    /// <summary>
    /// <b>存在替换</b>（§9.3）：封装的「先删后增」，复用位置与占格，**对外只发一条 <see cref="MapReplacedEvent"/>**
    /// —— **不是** `Removed` ＋ `Added` 两条（否则中间态被别处读到、统计记成「拆一座＋建一座」）。
    /// 让高级层**自己决定**是换皮（保留实体）还是重建。
    /// 返回是否实际改变。
    /// </summary>
    public static bool ReplaceFeature(GridCoord coord, FeatureType newFeature)
    {
        var map = ActiveMap;
        if (map == null || map.features == null) return false;
        if (coord.x < 0 || coord.y < 0 || coord.x >= map.width || coord.y >= map.height) return false;
        int i = coord.y * map.width + coord.x;
        if (i < 0 || i >= map.features.Length) return false;
        var old = map.features[i];
        if (old == newFeature) return false;

        WriteRaw(map, i, newFeature);
        GuardDeploymentSystem.NotifyFeatureWritten(coord, newFeature);
        if (GridSystem.Instance != null) GridSystem.Instance.RefreshCellFromFeature(coord, newFeature);
        if (MapRenderService.Instance != null) MapRenderService.Instance.UpdateCell(coord);

        // ⭐ 一条，且仅一条（§9.3）
        if (EventBus.HasSubscribers<MapReplacedEvent>())
            EventBus.Publish(new MapReplacedEvent(coord, old, newFeature));
        return true;
    }

    // ==========================================================================
    //  3. 增门 / 4. 删门（§六 / §七）
    // ==========================================================================

    /// <summary>
    /// <b>增门（重生时机）</b>：资源点重生 ⇒ 格翻回原 feature（§6.3 时机②）。
    /// 与「铺」（生成期走 <see cref="GenesisWrite"/>）／「放置」共用同一套合法性口径，此处只做重生这一种。
    /// </summary>
    public static bool PlaceResourceNode(GridCoord coord, FeatureType f) => SetFeature(coord, f);

    /// <summary>
    /// ⭐<b>删门 · 唯一入口</b>（§7.3）：**不区分死因**（采走／被打烂／被建筑覆盖，本层一视同仁）。
    /// <b>合并了原两条删除路径</b>（`ResourceRespawnSystem.SetFeature(Plain)` ＋ `WorldManager.TryConsumeResourceNode`）——
    /// 二者曾各写一遍「格翻 Plain ＋ 刷新派生 ＋ 刷新渲染 ＋ 守卫失去」，现只此一处。
    /// 校验对偶（§7.4）：增＝位置**空闲**；删＝**它确实在**（不通过 ⇒ 幂等跳过，删两次等于删一次）。
    /// 返回是否实际删除（该格原为资源点且已翻 Plain）。
    /// </summary>
    public static bool RemoveResourceNode(GridCoord coord)
    {
        var map = ActiveMap;
        if (map == null || map.features == null) return false;
        if (coord.x < 0 || coord.y < 0 || coord.x >= map.width || coord.y >= map.height) return false;
        int i = coord.y * map.width + coord.x;
        if (i < 0 || i >= map.features.Length) return false;
        var f = map.features[i];
        if (!IsRemovableResourceFeature(f)) return false;   // 存在性校验：不是可删资源格 ⇒ 幂等跳过

        if (!SetFeature(coord, FeatureType.Plain)) return false;
        // 守卫锚点语义（HH.3 §六 / HH.6 裁决二）：资源点被采走/覆盖 ⇒ 守卫区域失去覆盖
        GuardDeploymentSystem.HandleResourceConsumed(coord);
        return true;
    }

    /// <summary><b>锚点口径</b>的可消耗资源点地表物（与 `WorldManager.TryConsumeResourceNode` 改前口径**逐字**一致：Tree／Mine）。
    /// ⚠️ 这是**锚点消费**（§五）与 `WorldManager.IsResourceNodeAvailable` 的口径，**不是**删门的存在性口径
    /// （删门口径见 <see cref="IsRemovableResourceFeature"/>）。</summary>
    public static bool IsResourceNodeFeature(FeatureType f) => f == FeatureType.Tree || f == FeatureType.Mine;

    /// <summary><b>删门口径</b>的「可删资源格」地表物：`Tree／Mine`（持续节点）
    /// ＋ `OreVein／WoodPile／StonePile`（一次性实体，采集销毁后格翻 Plain，见 `Building.OnGatherCompleted`
    /// → `ResourceRespawnSystem.HandleEntityDepleted`）。与 `ResourceRespawnSystem.SetFeature` 改前口径**逐字**一致
    /// （原实现是「`target == Plain` 即可写」，故凡资源类地表皆可删）。</summary>
    public static bool IsRemovableResourceFeature(FeatureType f)
        => f == FeatureType.Tree || f == FeatureType.Mine
        || f == FeatureType.OreVein || f == FeatureType.WoodPile || f == FeatureType.StonePile;

    // ==========================================================================
    //  5. 锚点消费 / 返还（§五 · §7.8）
    // ==========================================================================

    /// <summary>
    /// 锚点消费者（`03` §五：**锚点不是独立对象，是"某个东西身上的一个字段"**；
    /// 「建筑需记住'我消费了哪个锚点（类型 ＋ 位置）'——**这属"属性自己装"**」）。
    /// </summary>
    public interface IAnchorConsumer
    {
        /// <summary>我消费掉的锚点位置（null ＝ 未消费任何锚点）。</summary>
        GridCoord? ConsumedAnchorCoord { get; }
        /// <summary>我消费掉的锚点类型。</summary>
        FeatureType ConsumedAnchorFeature { get; }
        /// <summary>写入／清除锚点引用（返还时清 null）。</summary>
        void SetConsumedAnchor(GridCoord? coord, FeatureType feature);
    }

    /// <summary>
    /// ⭐ <b>锚点消费</b>（`03` §6.4 门内第 2 步「锚点校验（需 `anchorRequire` 非空；**锚点消亡走删**）」）：
    /// 把锚点格的地表**走删门**抹掉（**不是**在放置路径里内联抹 —— 那正是 `4-B` 要治的「顺手抹」），
    /// 并把「消费了哪个锚点（类型 ＋ 位置）」记到 <paramref name="consumer"/> 身上（属性自己装）。
    ///
    /// <b>⭐ 门内声明校验（`4-B` 治本 · 探针反证 A 实测暴露）</b>：本口**必须**带
    /// <paramref name="declaredAnchor"/>（＝该放置路径**声明的锚点需求**）⇒ 门内校验「该格地表与该声明相符」；
    /// 不符 ⇒ **拒绝且零改写**。这样即便放置路径的 `anchorRequire` 判定被绕过或写错，
    /// **未被声明的资源格（如 `Tree`，无任何建筑声明）也永不改写** ⇒ `4-B`「建在资源格上不顺手抹树」在**门层**成立。
    /// </summary>
    /// <param name="declaredAnchor">声明需求（须与 `ResourceNodeMapping.GetResourceNode(def.id)` 同源）。</param>
    public static bool ConsumeAnchor(GridCoord coord, IAnchorConsumer consumer, BuildingType declaredAnchor)
    {
        if (consumer == null) return false;
        var f = GetFeatureAt(coord);
        if (!IsResourceNodeFeature(f)) return false;                 // 不是可消费锚点 ⇒ 幂等跳过
        if (!MatchesDeclaredAnchor(f, declaredAnchor)) return false; // ⭐ 声明不符 ⇒ 拒绝（零改写）
        if (!RemoveResourceNode(coord)) return false;                 // 锚点消亡**走删门**（唯一入口）
        consumer.SetConsumedAnchor(coord, f);
        return true;
    }

    /// <summary>声明需求与实际地表的**相符判定**（`Tree↔BuildingType.Tree`／`Mine↔BuildingType.Mine`；
    /// 其余 ⇒ 不符）。这是 `03` §6.4「锚点校验」的落地式。</summary>
    public static bool MatchesDeclaredAnchor(FeatureType f, BuildingType declared)
        => (declared == BuildingType.Tree && f == FeatureType.Tree)
        || (declared == BuildingType.Mine && f == FeatureType.Mine);

    /// <summary>
    /// ⭐ <b>锚点返还</b>（`03` §7.8「任何"通过消费某个锚点"建起来的建筑，被删除时返还那个锚点」，
    /// 与 §五「消费」构成**完整对偶**；**通用，非仅矿山**）：
    /// 把当初消费掉的锚点**写回原位**（走增门），并清空消费者身上的锚点引用 ⇒ 该锚点**可再被引用**。
    /// 幂等：无锚点引用 ⇒ false。
    /// </summary>
    public static bool ReturnAnchor(IAnchorConsumer consumer)
    {
        if (consumer == null || consumer.ConsumedAnchorCoord == null) return false;
        var coord = consumer.ConsumedAnchorCoord.Value;
        var f = consumer.ConsumedAnchorFeature;
        consumer.SetConsumedAnchor(null, FeatureType.Plain);
        if (!IsResourceNodeFeature(f)) return false;
        return PlaceResourceNode(coord, f);                // 锚点返还**走增门**
    }

    // ==========================================================================
    //  内部
    // ==========================================================================

    private static MapData ActiveMap => WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;

    /// <summary>领土归属（§8.3「归属」事实）＝中区块口径，与 `TerritorySystem` 同源；无主 ＝ -1。</summary>
    private static int OwnerAt(GridCoord coord)
    {
        var ts = TerritorySystem.Instance;
        var grid = GridSystem.Instance;
        if (ts == null || grid == null) return -1;
        int ms = grid.Config != null && grid.Config.midChunkSize > 0 ? grid.Config.midChunkSize : 4;
        var mid = new Vector2Int(coord.x / ms, coord.y / ms);
        if (ts.Ledger == null) return -1;
        return ts.Ledger.TryGetValue(mid, out var owner) ? owner : -1;
    }
}
