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

    /// <summary>⭐【HH.294 片 6-3·`03` §6.9】**grade 唯一写口**。除本行外，生产码（`Assets/_Game/**`）
    /// **无任何** `grades[...] =` 赋值。判据：`git grep -n -E "\.grades\s*\[[^]]*\]\s*=[^=]" -- 'Assets/_Game/**/*.cs'` 应只命中本行。</summary>
    private static void WriteGradeRaw(MapData map, int index, ResourceGrade g)
    {
        if (map == null || map.grades == null) return;
        if (index < 0 || index >= map.grades.Length) return;
        map.grades[index] = g;
    }

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

    /// <summary><b>造世界专用口（grade·0→1）</b>：仅生成期使用（`MapGenRules.AssignResourceGrades` 分配 pass ＋
    /// `WorldManager.GenerateMap` 建数组后显式填 `Normal`）。⚠️ 运行期写 grade 一律走**门内语义**：
    /// <see cref="SetFeature"/> 非四型复位 ／ <see cref="PlaceResourceNode"/> 重生重掷值 ／ <see cref="RemoveResourceNode"/> 删除复位。</summary>
    public static void GenesisWriteGrade(MapData map, int index, ResourceGrade g)
    {
        WriteGradeRaw(map, index, g);
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

    /// <summary><b>指定图的读口</b>（grade·带 `map` 参数·`03` §8.2 ①「资源等级」）：供拿得到 `map` 的调用方走门。
    /// ⚠️ 越界／地图未就绪／`grades == null`（Editor 探针用 `new MapData{...}` 造夹具不会填该字段）⇒ `Normal`，**不抛**。</summary>
    public static ResourceGrade ReadGradeAt(MapData map, int x, int y)
    {
        if (map == null || map.grades == null) return ResourceGrade.Normal;
        if (x < 0 || y < 0 || x >= map.width || y >= map.height) return ResourceGrade.Normal;
        int i = y * map.width + x;
        return i >= 0 && i < map.grades.Length ? map.grades[i] : ResourceGrade.Normal;
    }

    /// <summary><b>轻量单格读口</b>（grade·走 `ActiveMap`）：与 <see cref="GetFeatureAt(int,int)"/> 同构（O(1) 无分配）。
    /// 越界／未就绪／`grades == null` ⇒ `Normal`（不抛）。</summary>
    public static ResourceGrade GetGradeAt(int x, int y) => ReadGradeAt(ActiveMap, x, y);

    /// <summary><b>轻量单格读口</b>（grade·GridCoord 版）。</summary>
    public static ResourceGrade GetGradeAt(GridCoord c) => GetGradeAt(c.x, c.y);

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
        public ResourceGrade grade;      // 【HH.294 片 6-3·§6.9】资源等级（格表事实；非四型格恒 Normal）

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
        public bool useGrade;            // 【HH.294 片 6-3·§8.3 事实菜单「资源点 · 等级」】勾「资源等级＝某档」
        public ResourceGrade grade;

        public static CellFilter Feature(FeatureType f) => new CellFilter { useFeature = true, feature = f };
        public static CellFilter Walkable(bool walkable) => new CellFilter { useWalkable = true, walkable = walkable };
        public static CellFilter Empty() => new CellFilter { requireEmpty = true };
        public static CellFilter Grade(ResourceGrade g) => new CellFilter { useGrade = true, grade = g };

        public bool Accept(in CellInfo c)
        {
            if (useFeature && c.feature != feature) return false;
            if (useWalkable && c.walkable != walkable) return false;
            if (requireEmpty && c.hasOccupant) return false;
            if (requireOccupied && !c.hasOccupant) return false;
            if (useGrade && c.grade != grade) return false;
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
        // 【HH.294 片 6-3·§6.9】资源等级（格表事实）；grades == null ⇒ Normal（夹具容忍·不抛）
        info.grade = map.grades != null && i < map.grades.Length ? map.grades[i] : ResourceGrade.Normal;
        info.walkable = grid.IsWalkable(coord);
        var occ = grid.GetOccupant(coord);
        info.hasOccupant = occ != null;
        info.occupantIsObstacle = occ != null && occ.IsGridObstacle;
        info.ownerKingdomId = OwnerAt(coord);
        info.unitCount = grid.GetUnitCountInCell(coord);   // 无分配（§8.7 判据 4）
        return true;
    }

    /// <summary>⭐<b>区域枚举</b>（§8.2 ③）：范围（矩形）＋ 条件 ＋ 输出 buffer（**禁分配**：buffer 由调用方复用）。
    /// 返回写入条数。原 `FillUnitsInRect` 只服务单位 ⇒ 本口**通用**（地表／可走／空置／有物皆可筛）。
    ///
    /// <b>⚠️ 成本特征（`03` §8.7 判据 7 · `D772` 实测 · 禁每帧调用）</b>：
    /// 每格复合 **6 项**（地表／气候／可走／占格／归属／单位数）⇒ 成本 **O(格数 × 单位数)**；
    /// 实测 **2401 格 · N=23 ⇒ ≈7.46 ms**（45% 帧预算）、N=500 ⇒ 76.88 ms（463%）。
    /// 其中单位计数（`GetUnitCountInCell`·O(单位数)）占 ≈46%，余 ≈4 ms 来自归属查／占格查／组包
    /// ⇒ ⚠️ **仅把单位计数治本为 O(1) 仍约需 4 ms（24% 帧预算）**。
    /// ⛔ **禁在 `Update`／每帧路径调用**；**启用前须先治本**（块级单位计数 ⇒ O(1) → 按需取事实），
    /// 治本批须给「正确性读数 ＋ 代价读数」两栏。</summary>
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
                // 【HH.294 片4补正·M7】原为裸读 `map.features[y * map.width + x]`（**无** `i < Length` 保护，
                //   与同文件 `ReadAt:54`／`GetFeatureAt:69`／`TryGetCell:135` 口径不一）⇒ 改走本类 `ReadAt`（含界保护与长度保护）。
                if (ReadAt(map, x, y) == f) n++;
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
    /// <para>【`HH.294` 片 6-3·§6.9】**grade 语义**：新 feature **非四型** ⇒ grade 复位 `Normal`
    /// （新 feature 恰为四型时**不动** grade —— 由 <see cref="PlaceResourceNode"/> 的重掷值先写入）。</para>
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
        if (!IsGradeFeature(f)) WriteGradeRaw(map, i, ResourceGrade.Normal);   // 【片 6-3·§6.9】非四型 ⇒ grade 复位
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
    /// <para>【`HH.294` 片 6-3·§6.9】`grade` ＝ 重生**重掷**的等级（**由调用方掷入** —— 运行期门内无 rng 源，⛔ 门内禁掷）：
    /// 四型 ⇒ 先写入该值；非四型 ⇒ 由 <see cref="SetFeature"/> 复位 `Normal`（锚点返还等调用方的默认值即 `Normal`）。</para>
    /// </summary>
    public static bool PlaceResourceNode(GridCoord coord, FeatureType f, ResourceGrade grade = ResourceGrade.Normal)
    {
        var map = ActiveMap;
        if (map == null || map.features == null) return false;
        if (coord.x < 0 || coord.y < 0 || coord.x >= map.width || coord.y >= map.height) return false;
        int i = coord.y * map.width + coord.x;
        if (i < 0 || i >= map.features.Length) return false;
        if (IsGradeFeature(f)) WriteGradeRaw(map, i, grade);   // 【片 6-3】四型 ⇒ 落重掷值（非四型由 SetFeature 复位）
        return SetFeature(coord, f);
    }

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
        WriteGradeRaw(map, i, ResourceGrade.Normal);   // 【片 6-3·§6.9】删除 ⇒ grade 复位（与 SetFeature 非四型口径同源·此处显式声明）
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

    /// <summary>⭐【`HH.294` 片 6-3·`03` §6.9】**grade 作用域口径**：四型资源格
    /// （`Tree`／`OreVein`／`WoodPile`／`StonePile`）——**不含 `Mine`**（有实体·等级走 `Building.grade` 现状 ⇒ 两处都写＝双写）。
    /// 生成期分配 pass 与运行期写门（复位／重掷）**同一处口径**（探针可直调构造反证）。</summary>
    public static bool IsGradeFeature(FeatureType f)
        => f == FeatureType.Tree || f == FeatureType.OreVein || f == FeatureType.WoodPile || f == FeatureType.StonePile;

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
        // 【HH.294 片4补正·M9】原序为「先清引用（`SetConsumedAnchor(null,…)`）**后**判定 `IsResourceNodeFeature(f)`」
        //   ⇒ 若判定为假则**引用已清、锚点未返还**（静默丢锚点）。现改为**判定在前**：
        //   ① 判定不通过 ⇒ 直接返回，引用原样保留（不存在「引用没了、锚点也没还」）；
        //   ② 判定通过 ⇒ 已决定返还，引用必清（防「返还后的悬垂引用」再次触发写回）；
        //   ③ 返回值＝**实际是否发生写回**（该格若已是同值 ⇒ 增门幂等返 false，引用仍清）。
        if (!IsResourceNodeFeature(f)) return false;
        bool wrote = PlaceResourceNode(coord, f);          // 锚点返还**走增门**
        consumer.SetConsumedAnchor(null, FeatureType.Plain);
        return wrote;
    }

    // ==========================================================================
    //  6. 玩家侧拾取（`03` §8.6「PickAt ＝ 屏幕矩形 ＋ y 序」·HH.294 片 6-1）
    // ==========================================================================

    /// <summary>⭐ G0 实测符号结论（2026-09-17 · 探针 `Valley_HH294_Slice6PickProbe` §G0-A·构造用例像素读数）：
    /// `ProjectSettings/GraphicsSettings.asset:42-43` ＝ `m_TransparencySortMode: 3`（CustomAxis）
    /// ＋ `m_TransparencySortAxis: {x:0, y:1, z:0}` ⇒ 渲染叠放次序由世界 y 决定；
    /// 实测【**世界 y 越小 ⇒ 渲染越靠前（后画覆盖先画）**】——两组交叉用例一致：
    /// 组1（红 y=+0.5／蓝 y=−0.5）重叠中心像素=蓝；组2（红 y=−0.5／蓝 y=+0.5）重叠中心像素=红
    /// ⇒ 两次都是 y=−0.5 者盖住对方 ⇒ 与直觉（大 y 在前）**相反**，故符号位＝false。
    /// ⛔ 符号**由 G0 构造用例实测得出**（禁凭记忆断言）；<see cref="PickAt"/> 与渲染**共用同一键、同一符号**
    /// （`03` §8.7 判据 6「拾取同源」）。深度键取「各自**实际**渲染排序点」：
    /// 单位 `spriteSortPoint = Pivot`（`UnitController.cs:316`）⇒ 键＝SR 所属 `transform.position.y`；
    /// 建筑/宝箱**未设** `spriteSortPoint`（默认 `Center`·`BuildingVisual.cs:148` 只设 `sortingOrder`）
    /// ⇒ 键＝`SpriteRenderer.bounds.center.y`（两者二级投射点不同 ⇒ 按各自实际算，不假设统一）。</summary>
    public const bool DepthMajorYInFront = false;   // G0 实测（2026-09-17）：小 y 在前 ⇒ 大 y 在前不成立，置 false

    /// <summary>拾取命中（值类型·零分配）。source ∈ Building／UnitController／ChestEntity 之根；
    /// 上层按需 <see cref="Component.GetComponentInParent{T}"/> 取接口（IInteractable／IClickInteractable／UnitController），
    /// 与旧 `Physics2D.OverlapPoint` 后的用法完全同形。</summary>
    public struct MapPickHit
    {
        public Component source;      // 命中物根
        public int sortingOrder;      // 命中物渲染层带（读数/报告用）
        public float depthY;          // 命中物深度键（与渲染同源同符号·报告对照用）
    }

    /// <summary>⭐ 拾取候选邻域半径（格）—— 【HH.294 片 6-2 搭车 `R1`】**按最大建筑 sprite 半高重推**（改前固定 1）。
    ///
    /// <b>为什么改</b>：改前 `= 1`（3×3 格 ＝ **3.84×1.92** 世界单位）**小于**建筑 sprite 溢出 —— 实测
    /// `castle` sprite 4.00×**4.38** ⇒ 折算 **3.1×6.8 格**（`HH.305` 探针 §G0-B）⇒ 点其**可见上半部**落不进
    /// 候选集（「看到的点不到」·片 6-1 验收残余 `R1`）。
    ///
    /// <b>算法</b>：`ceil(最大建筑 sprite 半高 ÷ 半格高)`（取样＝`Resources/Buildings` 全部 `BuildingDef.prefab`
    /// 的 `SpriteRenderer`，含未激活子物体；按 prefab 层级 `lossyScale` 折算世界半高）——**一次性懒算 + 缓存**
    /// （按 `cellSize.y` 失效重算）；夹取到 `[1, <see cref="PickCellRadiusMax"/>]`，无样本 ⇒ 兜底 1（改前值）。
    ///
    /// <b>成本特征（同 `03` §8.7 判据 7 记档口径）</b>：候选建筑 ≤ **(2r+1)²** 次 `BuildingRegistry.GetAt`
    /// （O(1) 字典查）；单位/宝箱窗口同步放大（单位走 `FillUnitsInRect` O(单位数)、宝箱 O(宝箱数)）。
    /// 实测 r 值 ＋ 单次 `PickAt` 代价见 `HH.307` 报告（判据 7）。</summary>
    internal static int PickCellRadius
    {
        get
        {
            var grid = GridSystem.Instance;
            var map = ActiveMap;
            float cellH = grid != null && grid.Config != null ? grid.Config.cellSize.y : 0.64f;
            if (!ReferenceEquals(map, _pickRadiusMap))   // 换图 ⇒ 清缓存（半高按图/局重算）
            {
                _pickRadiusMap = map;
                _maxSpriteHalf = 0f;
                _pickRadiusCache = -1;
                _pickRadiusNextScan = 0f;
            }
            bool cellChanged = Mathf.Abs(_pickRadiusCellH - cellH) > 0.0001f;
            if (_pickRadiusCache <= 0 || cellChanged || Time.time >= _pickRadiusNextScan)
            {
                _pickRadiusNextScan = Time.time + PickRadiusScanInterval;
                _pickRadiusCellH = cellH;
                float half = ScanMaxSpriteHalf();
                if (half > _maxSpriteHalf) _maxSpriteHalf = half;   // 单调（同图内只增·防"建了更大的才变小"抖动）
                _pickRadiusCache = _maxSpriteHalf <= 0f
                    ? 1                                                            // 兜底＝改前值（无样本；零行为变更）
                    : Mathf.Clamp(Mathf.CeilToInt(_maxSpriteHalf / Mathf.Max(0.0001f, cellH * 0.5f)), 1, PickCellRadiusMax);
            }
            return _pickRadiusCache;
        }
    }

    /// <summary>候选窗口半径硬上限（成本上界的另一表述：候选建筑 ≤ (2×8+1)² ＝ 289 次 O(1) 字典查）。</summary>
    public const int PickCellRadiusMax = 8;

    /// <summary>半高重扫间隔（秒）：粘性单调 max ⇒ 首次扫定后基本不再变（重扫只为吸收"新建了更大 sprite 的建筑"）。</summary>
    private const float PickRadiusScanInterval = 10f;

    private static int _pickRadiusCache = -1;
    private static float _pickRadiusCellH = -1f;
    private static float _maxSpriteHalf;          // 全体在册建筑 sprite 世界半高的**单调**最大值
    private static MapData _pickRadiusMap;
    private static float _pickRadiusNextScan;

    /// <summary>
    /// 扫**全体在册建筑**的 sprite 世界半高取最大（见 <see cref="PickCellRadius"/>）。
    /// ️ 取样面是**运行时实体**（`BuildingRegistry` 的 `SpriteRenderer`），**不是** `BuildingDef.prefab`
    /// —— 真图经 `SpriteRefTable` 旁挂挂载（多数 def 的 `prefab` 为空）⇒ 读 prefab 会得 0（实测踩过）。
    /// 代价：单次 O(建筑数) 次 `GetComponentInChildren`；每 ≥10s 至多一次（读数见 HH.307 报告 §判据 7）。
    /// </summary>
    private static float ScanMaxSpriteHalf()
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return 0f;
        float maxHalf = 0f;
        var all = reg.All;
        for (int i = 0; i < all.Count; i++)
        {
            var b = all[i];
            if (b == null) continue;
            var sr = b.GetComponentInChildren<SpriteRenderer>();
            if (sr == null || sr.sprite == null) continue;
            float half = sr.sprite.bounds.size.y * Mathf.Abs(sr.transform.lossyScale.y) * 0.5f;
            if (half > maxHalf) maxHalf = half;
        }
        return maxHalf;
    }

    /// <summary>⭐ <b>玩家侧拾取</b>（`03` §8.6，位于「§八 门」内 ⇒ 本口属**门**的读面；上层只调门，
    /// 禁自建第二套坐标换算——内部一律走 <see cref="GridSystem"/> 换算口）。
    ///
    /// <b>「看到的＝点到的」</b>（`03` §8.7 判据 6）：返回**渲染上最靠前**的候选——
    /// ① 层带 `sortingOrder` 大者在前（宝箱 5 ＞ 建筑/单位 1，与 Unity 层带渲染次序一致）；
    /// ② 同层带按深度键（见 <see cref="DepthMajorYInFront"/> G0 符号结论）。
    ///
    /// <b>候选集（有界·⛔ 不走物理引擎·⛔ 禁全库遍历）</b>：
    /// ① 建筑：footprint 反查 3×3 邻域（<see cref="BuildingRegistry.GetAt"/> O(1)×9 ⇒ 候选 ≤9 座）；
    /// ② 单位：**单位索引**区域查询（<see cref="GridSystem.FillUnitsInRect"/>，cell 3×3 → 小格子域；buffer 复用零分配）；
    /// ③ 宝箱：<see cref="ChestManager.FillChestsInCellRect"/>（cell 匹配 3×3）。
    /// 包含判定＝实体主 <see cref="SpriteRenderer"/>（GetComponentInChildren 首个）的 `bounds`（世界 AABB）。
    ///
    /// <b>代价特征（判据面·同 `03` §8.7 判据 7 记档口径）</b>：单次 ＝ 9 次 O(1) 字典查 ＋ O(单位索引条目) 枚举
    /// ＋ O(宝箱数) 匹配；**稳态零 GC**（static buffer 复用，候选规模稳定后不扩容）。
    /// **hover 每帧调用**（`InteractionManager.UpdateHover`）已按此口径实测验收——单次 ms／GC 0 B／候选上限
    /// 见 HH.294 片 6-1 交付报告探针读数。</summary>
    public static bool PickAt(Vector2 worldPoint, out MapPickHit hit)
    {
        hit = default;
        var grid = GridSystem.Instance;
        if (grid == null || ActiveMap == null) return false;
        var cellOpt = grid.WorldToCoord(worldPoint);
        if (cellOpt == null) return false;
        var cell = cellOpt.Value;
        int r = PickCellRadius;

        bool has = false;

        // 候选① 建筑：footprint 反查 (2r+1)² 邻域（r＝PickCellRadius·按最大 sprite 半高重推·见该常量注释）
        //   —— 改前固定 3×3 且注释曾称「溢出半格」（**低估**：`castle` 实测溢出 3.4 格 ⇒ 那片「看到的点不到」）
        var reg = BuildingRegistry.Instance;
        if (reg != null)
        {
            _pickBuildings.Clear();
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    var b = reg.GetAt(new GridCoord(cell.x + dx, cell.y + dy));
                    if (b != null && !_pickBuildings.Contains(b)) _pickBuildings.Add(b);
                }
            for (int i = 0; i < _pickBuildings.Count; i++)
                ConsiderPick(_pickBuildings[i].GetComponentInChildren<SpriteRenderer>(), _pickBuildings[i], worldPoint, ref has, ref hit);
        }

        // 候选② 单位：单位索引区域查询（cell 3×3 → 小格子域；div 走 SubDiv 唯一除数）
        _pickUnits.Clear();
        int div = grid.SubDiv;
        var subRect = new RectInt((cell.x - r) * div, (cell.y - r) * div, (2 * r + 1) * div, (2 * r + 1) * div);
        grid.FillUnitsInRect(subRect, _pickUnits);
        for (int i = 0; i < _pickUnits.Count; i++)
        {
            var u = _pickUnits[i];
            if (u == null || !u.gameObject.activeInHierarchy) continue;
            ConsiderPick(u.GetComponent<SpriteRenderer>(), u, worldPoint, ref has, ref hit);
        }

        // 候选③ 宝箱（ChestManager 列表短；cell 匹配 3×3）
        if (ChestManager.HasInstance)
        {
            _pickChests.Clear();
            ChestManager.Instance.FillChestsInCellRect(new RectInt(cell.x - r, cell.y - r, 2 * r + 1, 2 * r + 1), _pickChests);
            for (int i = 0; i < _pickChests.Count; i++)
                ConsiderPick(_pickChests[i].GetComponent<SpriteRenderer>(), _pickChests[i], worldPoint, ref has, ref hit);
        }
        return has;
    }

    /// <summary>拾取调度（双轨开关分流·`底层执行计划` §五回退预案）：默认走 <see cref="PickAt"/>（坐标拾取）；
    /// <see cref="UseLegacyPhysicsPick"/>＝true 时回落旧物理路径（全层 OverlapPoint ＝ 改前 `mask = ~0` 等价口径）。</summary>
    public static Component PickWorld(Vector2 worldPoint)
        => UseLegacyPhysicsPick
            ? Physics2D.OverlapPoint(worldPoint)
            : (PickAt(worldPoint, out var h) ? h.source : null);

    /// <summary>
    /// 框选区域查询单位（HH.294 片 6-1·6-D④：**单位索引**区域查询，⛔ 不是 <see cref="QueryCells"/> 六项复合查询）。
    /// 新路径＝世界 AABB 四角 → 小格子域包围盒（走 <see cref="GridSystem"/> 换算口，禁就地展开）→
    /// <see cref="GridSystem.FillUnitsInRect"/>（buffer 复用**零分配**）；旧路径＝`Physics2D.OverlapAreaAll`
    /// 仅 <see cref="UseLegacyPhysicsPick"/> 回退态使用（引擎侧有数组分配）。返回写入条数。
    /// ⭐ 双轨旧路径在此**收容**：生产码 `Physics2D` 残留仅本文件（MapGate.cs）——上层（InteractionManager／SelectionController）零物理调用。
    /// </summary>
    public static int FillUnitsInWorldRect(Rect worldRect, List<UnitController> buffer)
    {
        if (buffer == null) return 0;
        int before = buffer.Count;
        if (UseLegacyPhysicsPick)
        {
            var cols = Physics2D.OverlapAreaAll(worldRect.min, worldRect.max);
            for (int i = 0; i < cols.Length; i++)
            {
                var u = cols[i] != null ? cols[i].GetComponentInParent<UnitController>() : null;
                if (u != null && !buffer.Contains(u)) buffer.Add(u);
            }
            return buffer.Count - before;
        }
        var grid = GridSystem.Instance;
        if (grid == null || grid.MapWidth <= 0) return 0;
        var a = SubClamp(grid, worldRect.min);
        var b = SubClamp(grid, new Vector2(worldRect.xMin, worldRect.yMax));
        var c = SubClamp(grid, new Vector2(worldRect.xMax, worldRect.yMin));
        var d = SubClamp(grid, worldRect.max);
        int sx0 = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
        int sx1 = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
        int sy0 = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
        int sy1 = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));
        grid.FillUnitsInRect(new RectInt(sx0, sy0, sx1 - sx0 + 1, sy1 - sy0 + 1), buffer);
        return buffer.Count - before;
    }

    /// <summary>世界点 → 小格子号（越界 clamp 到 [0, SubWidth/SubHeight-1]；走换算口不就地展开）。</summary>
    private static Vector2Int SubClamp(GridSystem grid, Vector2 world)
    {
        var sub = grid.WorldToSubCoord(world);
        if (sub.HasValue) return new Vector2Int(sub.Value.x, sub.Value.y);
        var cs = grid.Config != null ? grid.Config.cellSize : new Vector2(1.28f, 0.64f);
        int div = grid.SubDiv;
        Vector2 f = GridSystem.WorldToCellF(world, new Vector2(cs.x / div, cs.y / div));
        return new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(f.x), 0, grid.Width - 1),
                              Mathf.Clamp(Mathf.FloorToInt(f.y), 0, grid.Height - 1));
    }

    /// <summary>⭐ 双轨开关（`底层执行计划` §五 5-B 回退预案「门可先双轨，验收后再删」）：
    /// true＝旧物理拾取路径（依赖 Collider2D；⚠️ 6-C 删建筑挂载后旧路径对建筑/宝箱**失效**——完全回退须连
    /// `BuildingFactory`／`BuildController` 的挂载块一并 revert）；false（默认）＝坐标拾取。</summary>
    public static bool UseLegacyPhysicsPick = false;

    // 拾取 scratch buffer（主线程单线程消费；容量稳定后复用 ⇒ 稳态零 GC）
    private static readonly List<Building> _pickBuildings = new List<Building>(9);
    private static readonly List<UnitController> _pickUnits = new List<UnitController>(64);
    private static readonly List<ChestEntity> _pickChests = new List<ChestEntity>(16);

    /// <summary>单候选判定：渲染 bounds 含点 ⇒ 按「层带 → 深度键（G0 符号）」与当前最优比前。</summary>
    private static void ConsiderPick(SpriteRenderer sr, Component src, Vector2 p, ref bool has, ref MapPickHit best)
    {
        if (sr == null || !sr.enabled || !src.gameObject.activeInHierarchy) return;
        var b = sr.bounds;
        if (!b.Contains(new Vector3(p.x, p.y, b.center.z))) return;
        // 深度键＝各自实际渲染排序点（见 DepthMajorYInFront 注释）：Pivot ⇒ transform.y；Center（默认）⇒ bounds.center.y
        float key = sr.spriteSortPoint == SpriteSortPoint.Pivot ? sr.transform.position.y : b.center.y;
        int order = sr.sortingOrder;
        if (!has || IsInFrontOf(order, key, best.sortingOrder, best.depthY))
            best = new MapPickHit { source = src, sortingOrder = order, depthY = key };
        has = true;
    }

    /// <summary>a 是否渲染在 b 前面（层带大者前；同层带按 G0 实测符号——与渲染同源同符号）。</summary>
    private static bool IsInFrontOf(int orderA, float yA, int orderB, float yB)
    {
        if (orderA != orderB) return orderA > orderB;
        return DepthMajorYInFront ? yA > yB : yA < yB;
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
