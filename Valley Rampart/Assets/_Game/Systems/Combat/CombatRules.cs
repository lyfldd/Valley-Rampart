using UnityEngine;

/// <summary>
/// 3.6 战斗规则工具（静态）：击飞模型（动能 + θ 随机路线）。
/// 全局常量暂内联（3.6 §7.3 不过度解耦：击飞参数训练校准前先固定，后续按需 SO 化）。
///
/// 【`HH.320` 批 1 · `D843`/`D844`/`D846`】本类新增/改造四件（均为**唯一口**）：
///   ⭐ <see cref="HasLineOfSight"/>：**视线阻挡唯一判据**（件 3′ 目标选择 ＋ 件 4 阻挡集共用）；
///   ⭐ <see cref="FindFortificationBlocker"/>：**工事阻挡唯一口** —— 视线链与弹道链
///      （`ProjectileManager.CheckWallBlock`）**必须调用同一函数**（`D846` `P4` 同源铁律：
///      两链各写一套 ⇒「选得到／打不到」必然复现）；
///   ⭐ <see cref="NextSpreadOffset"/> ＋ `ComputeKnockback` 的 θ：**种子派生 `System.Random`**（`R4`，
///      ⛔ 伤害链 `UnityEngine.Random` 零命中）；
///   ⭐ <see cref="InDiamondBase"/>：多格建筑**菱形底座**命中（件 6，⛔ 非「到 pivot 圆心距」）。
/// </summary>
public static class CombatRules
{
    // ===== 击飞模型全局参数（3.6 §5.4，占位可调）=====
    public const float KnockbackGravity = 9.8f;        // g（伪物理）
    public const float KnockbackThetaMin = 30f;        // 抛射角下限（度）
    public const float KnockbackThetaMax = 75f;        // 抛射角上限（度）
    public const float KnockbackForceScale = 6f;       // 冲击力基础（格）
    public const float ToughnessReduceScale = 0.02f;   // 韧性削减系数（高韧性 → v0 小 → 飞得近矮）

    // ========================================================================
    //  【`R4` · 伤害链确定性随机源（唯一口 · 种子派生 `System.Random`）】
    //  纪律：⛔ 伤害链禁用 `UnityEngine.Random`（全局流 ⇒ 破「同 seed 复跑逐字一致」）。
    //  先例：`VagrantCampSystem.NewDayRng:39`（`new System.Random(seed ^ (day*7919))`）。
    //  本口取「世界种子」为种源；每局起点由 `DamageSystem.ResetState`（HH.92/T13 清场链）重置。
    // ========================================================================

    static System.Random _rng;

    /// <summary>伤害链随机流（惰性播种；种子源 ＝ 当前世界种子 ⇒ 同 seed 恒复现）。</summary>
    static System.Random Rng => _rng ??= new System.Random(WorldSeed());

    /// <summary>世界种子（真源 `map.seed`；回退 `WorldManager.MapSeed`；再回退 1）。</summary>
    static int WorldSeed()
    {
        var wm = WorldManager.Instance;
        var map = wm != null ? wm.ActiveMap : null;
        if (map != null && map.seed != 0) return map.seed;
        if (wm != null && wm.MapSeed != 0) return wm.MapSeed;
        return 1;
    }

    /// <summary>【唯一重置口】重播随机流：`seed != 0` ⇒ 用给定种子（探针定点复现／对拍用）；
    /// `seed == 0` ⇒ 取当前世界种子（换局/清场用）。</summary>
    public static void ResetCombatRandom(int seed = 0) => _rng = new System.Random(seed != 0 ? seed : WorldSeed());

    /// <summary>⭐ 弹道落点散布偏移（**保留散布** · 换种子派生源）：以落点为中心的**均匀圆盘**采样
    /// （`r = R·√u`、`θ = 2πv` 与 `Random.insideUnitCircle` 同分布）。`errorRadius ≤ 0` ⇒ 返回零向量（不散布）。
    /// ⛔ 不得在调用方自行掷随机（唯一口）。</summary>
    public static Vector2 NextSpreadOffset(float errorRadius)
    {
        if (errorRadius <= 0f) return Vector2.zero;
        double u = Rng.NextDouble(), v = Rng.NextDouble();
        float rad = (float)System.Math.Sqrt(u) * errorRadius;
        float ang = (float)(v * System.Math.PI * 2.0);
        return new Vector2(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad);
    }

    /// <summary>击退抛射角（30°~75° 均匀；`R4`：⛔ 原 `UnityEngine.Random.Range`）。</summary>
    static float NextKnockbackThetaDeg()
        => KnockbackThetaMin + (float)Rng.NextDouble() * (KnockbackThetaMax - KnockbackThetaMin);

    /// <summary>⭐ 伤害链**概率判定唯一口**（`R4`）：`true` 概率 ＝ `p`（clamp 0..1）。
    /// ⛔ 调用方不得自行 `UnityEngine.Random.value`（全局流 ⇒ 破「同 seed 复跑逐字一致」）。
    /// 现役消费者：冲锋击飞方向反转（原 `Random.value &gt; 0.8f`）。</summary>
    public static bool RollChance(float p) => Rng.NextDouble() < Mathf.Clamp01(p);

    /// <summary>
    /// 击飞结算（3.6 §5.4 定稿 + 2026-08-05 训练师解耦）：动能定最大限度 + θ 均匀随机 30°~75° 定路线。
    ///   v0    = 冲击力 × (1 - 韧性削减系数 × 韧性)
    ///   L_max = v0² / g；L = L_max × sin(2θ)
    /// 输出击飞距离（世界单位）与滞空时长。
    /// 改动③（对齐 sim ChargeSweep）：冲击力固定 6f（不再随 chargeDamage 缩放——chargeDamage 一参两用会
    ///   在伤害改 40 时把击飞也削到地板）；被撞「懵」滞空固定 1.2s（原 Clamp(0.3*dist,0.3,2.5)）。
    /// `HH.320` 件 7：θ 随机源改 <see cref="NextKnockbackThetaDeg"/>（种子派生 · `R4`）。
    /// </summary>
    public static void ComputeKnockback(float chargeDamage, float toughness,
        out float distanceWorld, out float duration)
    {
        // 冲击力固定 6f（sim 2026-08-05 解耦：chargeDamage 只决定伤害，不决定击飞动能）
        float impactForce = KnockbackForceScale;
        if (impactForce < 0.1f) impactForce = 0.1f;

        float v0 = impactForce * Mathf.Max(0.1f, 1f - ToughnessReduceScale * toughness);
        float lMax = (v0 * v0) / KnockbackGravity;

        // θ 均匀随机 30°~75°：θ 大 → 高而近；θ 小 → 远而矮（"这次撞高、下次撞远"）
        float theta = NextKnockbackThetaDeg() * Mathf.Deg2Rad;
        float distFactor = Mathf.Sin(2f * theta);

        float distance = Mathf.Max(0.5f, lMax * distFactor);
        duration = 1.2f;                                             // 被撞「懵」固定 1.2s（改动③）
        distanceWorld = distance;
    }

    // ===== 2_5 目标选择 + 视线（D83/D3）=====

    /// <summary>
    /// 价值×距离目标评分（2_5 步骤4，D83）：score = valueWeight×目标价值 − distWeight×距离(格单位)，取最高。
    /// 近战：攻击范围内候选按此评分（无视线要求）；远程：射程圆内 + HasLineOfSight。
    /// </summary>
    public static float TargetScore(float targetValue, float distCells,
        float valueWeight, float distWeight)
        => valueWeight * targetValue - distWeight * distCells;

    // ========================================================================
    //  【件 3′／件 4】**视线阻挡唯一口**（`D844` 阻挡集 ＋ `D846` `P4` 同源铁律）
    // ========================================================================

    /// <summary>⭐ **视线判定（唯一口）** —— 目标选择链（件 3′）与阻挡集（件 4）共用本函数，⛔ 不得第二份。
    ///
    /// **阻挡集**（`D844` §9.1-2 ＋ `D846` `P3`）：
    ///   ① **山壁**：`GridSystem.IsSightBlockingFeature`（显式 `{Mountain, SnowMountain}`）；
    ///   ② **建筑占格**：`WalkFlags.BuildingBlocked` 位测（⚠️ **废墟保留占格** ⇒ 算阻挡位 ——
    ///      `D844` §9.1-3 裁定「挡视线、不挡弹道」的不对称**已知并接受**，⛔ 不作为缺陷）；
    ///   ③ **工事单位**：与弹道链**同一函数** <see cref="FindFortificationBlocker"/>（`applyArcHeight=false`
    ///      ⇒ 不按弧高滤；`fortification != null` **全含**，含 `blocksMovement=0` 的三塔；城门开态亦照挡）。
    ///   ⛔ **不挡**：水（`River`/`Ocean`）／`Locked`（工地）／桥（`Bridge`）。
    ///
    /// **几何**：起终点**所在微格豁免**；越界保守放行 `true`（不误拦 · 保留原作语义）。
    /// **性能**：逐微格 Bresenham（`O(L)` × `O(1)` 位/表读）＋ 工事带测（`O(N)` 单遍 · 早退）
    ///   ⇒ **单次零堆分配**（⛔ 无迭代器/无 `List`）、稳态零 GC。
    /// **ignoreOccupant**：终点实体自身的占格豁免（多格建筑当目标时，弹道终点落在**自己身上** ⇒
    ///   不豁免会造出「打不到大建筑」假阴性）。非建筑目标传 `null`。
    /// </summary>
    public static bool HasLineOfSight(Vector2 from, Vector2 to, IGridOccupant ignoreOccupant = null)
    {
        var g = GridSystem.Instance;
        if (g == null || g.Config == null) return true;
        var a = g.WorldToSubCoord(from);
        var b = g.WorldToSubCoord(to);
        if (!a.HasValue || !b.HasValue) return true;   // 越界保守放行（原作语义）
        if (SubLineBlockedByCell(g, a.Value, b.Value, ignoreOccupant)) return false;
        // ③ 工事单位：**与弹道链同源**（同一函数 · 同带半宽）
        return FindFortificationBlocker(from, to, GridMath.PathBandHalf, applyArcHeight: false, arcHeightCells: 0f) == null;
    }

    /// <summary>逐微格 Bresenham（含起终点枚举；起终微格豁免）。**零分配**（内联循环，非迭代器）。
    /// 任一中间微格命中 ①山壁／②建筑占格 ⇒ 返回 true（被挡）。</summary>
    static bool SubLineBlockedByCell(GridSystem g, GridCoord a, GridCoord b, IGridOccupant ignore)
    {
        int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            bool isEnd = (x0 == a.x && y0 == a.y) || (x0 == b.x && y0 == b.y);
            if (!isEnd && IsSightBlockedSub(g, new GridCoord(x0, y0), ignore)) return true;
            if (x0 == x1 && y0 == y1) return false;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    /// <summary>单微格视线阻挡判据（子格级 `O(1)` 读）：① 山壁（地表物显式枚举）② 建筑占格（位测）。
    /// ⛔ 不读 `Water`/`Locked`/`Bridge`（不挡）。占格物 == `ignore`（终点自身）⇒ 豁免。</summary>
    static bool IsSightBlockedSub(GridSystem g, GridCoord sub, IGridOccupant ignore)
    {
        if (GridSystem.IsSightBlockingFeature(g.GetFeatureAt(g.SubToCell(sub)))) return true;   // ① 山壁
        if ((g.GetWalkFlagsSub(sub) & WalkFlags.BuildingBlocked) != 0)                          // ② 建筑占格
            return ignore == null || !ReferenceEquals(g.GetOccupantSub(sub), ignore);
        return false;                                                                            // ⛔ 水/工地/桥不挡
    }

    /// <summary>⭐ **工事阻挡唯一口**（`D846` `P4` 同源铁律）：沿 `a→b` 取「**最靠起点**」的工事单位
    /// （点到线段距离 ≤ `bandHalf` · **排除起终点格** · `fortification != null` 全含）。
    /// `applyArcHeight=true` ⇒ **弧高 > 工事高度者跳过**（＝弹道链 `CheckWallBlock` 用法，逐字保留原语义）；
    /// `false` ⇒ 不按高度滤（＝视线链用法）。
    /// 性能：单遍 `UnitRegistry.GetUnitsEnumerator()`（`HashSet<T>` **结构枚举器** ⇒ 零装箱/零分配），`O(N)`＋早退候选判定。
    ///   ⚠️ **勘正（`L-63` · `D847` §三-1）**：⛔ **不得**走 `UnitRegistry.GetAllUnits()` ——
    ///   其**返回类型**为 `IEnumerable&lt;UnitController&gt;`，而 `UnitRegistry.cs:11` 内部实为
    ///   **`HashSet&lt;UnitController&gt;`** ⇒ 接口 `foreach` 会把结构枚举器**装箱** ⇒ **每次 1 次堆分配**。
    ///   （本行曾误写「返回内部 List 引用 ⇒ 零分配」＝ 类型名错 ＋ 忽略接口装箱 ⇒ 与下文 `:188-190` 自相矛盾，
    ///   现按事实改写。）真零分配口 ＝ `UnitRegistry.GetUnitsEnumerator()`（`UnitRegistry.cs:57`）。
    /// 本函数**只选阻挡者，不做伤害/穿透结算**（对墙伤害仍归 `CheckWallBlock` · ⛔ 不丢语义）。</summary>
    public static UnitController FindFortificationBlocker(Vector2 a, Vector2 b, float bandHalf,
                                                          bool applyArcHeight, float arcHeightCells)
    {
        if (UnitRegistry.Instance == null) return null;
        Vector2 ab = b - a;
        float abLenSq = ab.sqrMagnitude;
        if (abLenSq <= 0.0001f) return null;                 // 起终点重合：无区间
        float bandSq = bandHalf * bandHalf;

        UnitController blocker = null;
        float blockerT = float.MaxValue;
        // ⚠️ 零分配：⛔ 不得走 `GetAllUnits()`（`IEnumerable` ⇒ 结构枚举器**装箱** ⇒ 每次 1 次堆分配）
        //    ⇒ 走 `GetUnitsEnumerator()`（同一集合的结构枚举器）—— `P2` 硬约束①/②。
        var e = UnitRegistry.Instance.GetUnitsEnumerator();
        while (e.MoveNext())
        {
            var uc = e.Current;
            if (uc == null || uc.fortification == null) continue;
            if (applyArcHeight && arcHeightCells > uc.fortification.heightCells) continue;   // 弧高够 ⇒ 越过

            Vector2 pos = uc.transform.position;
            if (Vector2.SqrMagnitude(pos - a) <= bandSq) continue;    // 起点格不计
            if (Vector2.SqrMagnitude(pos - b) <= bandSq) continue;    // 终点格不计
            float t = Vector2.Dot(pos - a, ab) / abLenSq;
            if (t <= 0f || t >= 1f) continue;                         // 只算区间内
            Vector2 closest = a + ab * t;
            if (Vector2.SqrMagnitude(pos - closest) > bandSq) continue;   // 不在带内
            if (t >= blockerT) continue;                              // 只认最靠射手的那一枚
            blocker = uc;
            blockerT = t;
        }
        return blocker;
    }

    // ========================================================================
    //  【件 6】多格建筑 ＝ 菱形底座命中
    // ========================================================================

    /// <summary>⭐ **菱形底座命中判据（唯一口）**：`point` 是否落在「以建筑位置为中心、按 `footprint`（地块格）
    /// 张成的**等轴菱形底座**」内 ⇒ 命中（件 6 · ⛔ 非「到 `pivot` 的圆心距」）。
    ///
    /// **推导**：等轴换算 <see cref="GridSystem.WorldToCellF"/> 把每个「格」（世界空间菱形）映成
    /// 格坐标下的**单位正方形** ⇒ 「`footprint` 张成的菱形底盘」≡ 格坐标下的**轴对齐矩形**
    /// `|Δg| ≤ footprint/2`（逐轴）⇒ 逐轴比较即可，无需多边形/射线求交。边界含（`≤`）。
    /// 现配置 2:1 等轴下，该菱形在世界空间的半宽 ＝ `(W+H)·cellW/4`、半高 ＝ `(W+H)·cellH/4`。</summary>
    public static bool InDiamondBase(Vector2 point, Vector2 centerWorld, Vector2Int footprint, Vector2 cellSize)
    {
        Vector2 c = GridSystem.WorldToCellF(centerWorld, cellSize);
        Vector2 p = GridSystem.WorldToCellF(point, cellSize);
        float hw = Mathf.Max(1, footprint.x) * 0.5f;
        float hh = Mathf.Max(1, footprint.y) * 0.5f;
        return Mathf.Abs(p.x - c.x) <= hw && Mathf.Abs(p.y - c.y) <= hh;
    }
}
