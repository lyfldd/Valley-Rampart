using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 投射物集中管理器（3.4 第四节）。
///
/// 核心设计：逻辑命中 vs 视觉弹道分离。
///   - 发射时刻（逻辑层，一次）：锁定目标位置 + 算飞行时间 + 入池取投射物
///   - 飞行中（视觉层，每帧）：抛物线 t 插值位置（3 个浮点运算），不做碰撞检测
///   - 到达时刻（逻辑层，一次）：位置检测 1 格半径内非己方单位，命中最近（MaxHits=1）
///
/// 关键边界：投射物发射后独立存在，不追踪原目标，不追踪发射者。
/// 详见 3.4_伤害管线设计.md 第四节、决策 9+10+12+20。
/// </summary>
public class ProjectileManager : Singleton<ProjectileManager>
{
    // ===== 配置（从 DamageConfig SO 加载，Play 模式实时拖滑块调参）=====

    private DamageConfig _config;

    private float ArcHeight => _config.arcHeight;
    private float HitRadiusCells => _config.hitRadiusCells;

    // 纯视觉/池参数不变序列化，const 即可（不需要频繁调）
    private const float ProjectileScale = 0.3f;
    private const int SortingOrder = 5;
    private const int InitialPoolSize = 16;

    // 投射物池上限（2_5 步骤6：改读 DamageConfig.projectilePoolSize，SO 可调，默认 128；溢出丢最旧）
    private int ProjectilePoolSize =>
        _config != null && _config.projectilePoolSize > 0 ? _config.projectilePoolSize : 128;

    [Tooltip("投射物 Sprite。未指定时运行时创建黄色小方块。")]
    [SerializeField] private Sprite _projectileSprite;

    // ===== 投射物数据（纯数据，无 MonoBehaviour，集中 Update）=====

    private struct ProjectileData
    {
        public Vector2 startPos;
        public Vector2 targetPos;
        public float speed;
        public float elapsed;
        public float duration;
        public IDamageable attacker;
        public int attack;
        public GameObject visual;

        // ===== 弹药（3.6 §三：穿透/AOE/弹道/效果）=====
        public int pierceLevel;
        public BallisticType ballisticType;
        public float arcHeightCells;
        public float aoeRadiusCells;
        public float aoeFalloff;
        public GroundEffectType effectType;
        public float effectRadiusCells;
        public float effectDuration;
        public float effectTickInterval;
        public float effectPower;
        public int effectMaxTargets;
        // 3.7 P1.2 弹药美术占位：弹丸类型（决定出池着色，复用单一 sprite + 色变）
        public ProjectileType projectileType;
        // 2_20 M7 火枪二段贯穿（D494）：贯穿额外目标数（0=不贯穿）；贯穿目标伤害=攻击×0.6
        public int pierceThroughCount;
    }

    private readonly List<ProjectileData> _active = new();
    private readonly Queue<GameObject> _pool = new();
    private Sprite _runtimeSprite;

    // ===== 生命周期 =====

    protected override void Awake()
    {
        base.Awake();
        _config = Resources.Load<DamageConfig>("Config/DamageConfig");
        if (_config == null)
            Debug.LogError("[ProjectileManager] 未找到 DamageConfig！请确保 Resources/Config/DamageConfig.asset 存在。");
        EnsureSprite();
        PreFillPool();
    }

    // ===== 发射（由 DamageSystem 远程分流调用）=====

    /// <summary>
    /// 发射投射物。位置驱动：锁定目标当前位置（非目标对象），算飞行时间。
    /// 发射后独立存在，不追踪原目标。
    /// </summary>
    public void SpawnProjectile(IDamageable attacker, IDamageable target, AttackProfile profile)
    {
        if (attacker == null || target == null) return;

        Vector2 startPos = attacker.GetPosition();
        Vector2 targetPos = target.GetPosition();

        // 弹道误差圆：以目标位置为中心，在误差半径内随机偏移落点
        // 【HH.320 件7 · R4】散布**保留**，随机源改「种子派生的 System.Random」（唯一口 CombatRules.NextSpreadOffset）
        //   —— 原 `Random.insideUnitCircle`（UnityEngine.Random 全局流）已退役：破「同 seed 复跑逐字一致」。
        float errorRadius = _config.projectileErrorRadius;
        if (errorRadius > 0f)
        {
            targetPos += CombatRules.NextSpreadOffset(errorRadius);
        }

        float distance = Vector2.Distance(startPos, targetPos);
        float duration = distance / Mathf.Max(0.1f, profile.projectileSpeed);

        GameObject visual = GetFromPool();
        visual.transform.position = startPos;
        visual.SetActive(true);
        // 3.7 P1.2：按弹药类型着色（复用单一 sprite，色变区分箭/弩/石/火/魔）
        // HH.239 T14 / D707 R4：弹药真图走 SpriteRefTable **ammo_*** 旁挂键（两个 SO 零改动）——
        // AmmoDef 字段会拉平进 ProfessionSnapshot（Systems/AI.Core/Config）⇒ 加 sprite 字段必撞 H3「AI.Core 禁触」。
        var sr = visual.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Sprite ammoArt = null;
            var table = ValleyRampart.Rendering.SpriteRefTable.Instance;
            if (table != null) table.TryGet(AmmoArtId(profile.projectileType), out ammoArt);
            if (ammoArt != null)
            {
                sr.sprite = ammoArt;
                sr.color = Color.white;                       // 真图不叠色变
                visual.transform.localScale = Vector3.one;     // 素材自带 PPU100 尺度
            }
            else
            {
                sr.sprite = _runtimeSprite;                    // 缺图回退：占位方块 + 色变区分（不崩）
                sr.color = GetProjectileColor(profile.projectileType);
                visual.transform.localScale = Vector3.one * ProjectileScale;
            }
        }

        _active.Add(new ProjectileData
        {
            startPos = startPos,
            targetPos = targetPos,
            speed = profile.projectileSpeed,
            elapsed = 0f,
            duration = duration,
            attacker = attacker,
            attack = profile.attack,
            visual = visual,
            // 弹药（3.6 §三）
            pierceLevel = profile.pierceLevel,
            ballisticType = profile.ballisticType,
            arcHeightCells = profile.arcHeightCells,
            aoeRadiusCells = profile.aoeRadiusCells,
            aoeFalloff = profile.aoeFalloff,
            effectType = profile.effectType,
            effectRadiusCells = profile.effectRadiusCells,
            effectDuration = profile.effectDuration,
            effectTickInterval = profile.effectTickInterval,
            effectPower = profile.effectPower,
            effectMaxTargets = profile.effectMaxTargets,
            // 3.7 P1.2：弹药类型（出池着色用）
            projectileType = profile.projectileType,
            // 2_20 M7 火枪二段贯穿（D494）：攻方 SO 直接读（不入快照，AI.Core 零改动）
            pierceThroughCount = attacker is UnitController pmUc && pmUc.Data is NpcProfessionDef pmNd ? pmNd.pierceThroughCount : 0,
        });
    }

    // ===== 集中 Update（所有投射物统一推进，非每个自己 MonoBehaviour）=====

    private void Update()
    {
        if (_active.Count == 0) return;

        float dt = Time.deltaTime;
        float hitRadiusCells = HitRadiusCells; // 格单位（2_5 步骤6：命中半径不再 ×cellSize）

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var p = _active[i];
            p.elapsed += dt;
            float t = p.duration > 0f ? p.elapsed / p.duration : 1f;

            if (t >= 1f)
            {
                // 到达时刻：位置检测（一次，决策 9+12）
                OnProjectileArrived(p, hitRadiusCells);
                ReturnToPool(p.visual);
                _active.RemoveAt(i);
            }
            else
            {
                // 飞行中：抛物线插值（轻量，3 个浮点运算）
                Vector2 pos = Vector2.Lerp(p.startPos, p.targetPos, t);
                pos.y += ArcHeight * Mathf.Sin(Mathf.PI * t);
                p.visual.transform.position = pos;
                _active[i] = p; // struct 是值类型，必须写回列表（否则 elapsed 不累加）
            }
        }
    }

    // ===== 到达时刻检测 =====

    /// <summary>
    /// 到达时刻位置检测：查目标位置 1 格半径内非己方单位，命中最近（MaxHits=1）。
    /// 原目标跑了 + 无其他单位 = miss；有单位 = 误伤命中（Faction 二元判定）。
    /// </summary>
    private void OnProjectileArrived(ProjectileData p, float hitRadiusCells)
    {
        // 越墙判定（3.6 §5）：低抛被工事挡（弧高 ≤ 工事高度），穿透等级决定对墙伤害
        if (CheckWallBlock(p))
            return;

        // 查 GridSystem 附近微格的单位（doc1 微格主表 D70，2_5 步骤3）
        List<UnitController> candidates = QueryNearbyUnits(p.targetPos, hitRadiusCells);

        // ⭐【HH.320 件 6 返工（`D847` §2-1）】⛔ **不得**在此因「候选为空」早退 —— 原因与优先序：
        //   ① 本候选**只含 `UnitController`**（`QueryNearbyUnits` → `_unitSubCells`）；⚠️ **建筑不在候选内**
        //      （建筑占格走 `_occupants`/`WalkFlags` 另一套）⇒ 对**非工事建筑**射箭（落点附近通常一个单位都没有）
        //      会在这里被 return 掉 ⇒ 下方「建筑菱形底座兜底」**永不执行** ⇒ 远程对建筑**恒 miss（零伤害）**。
        //   ② 优先序（本批口径 · ⛔ 不得改）：**单位命中优先** → 无单位命中 ⇒ **建筑底座命中** → 两者皆无 ⇒ miss。
        //   ③ **唯一早退点**在下文「两者皆无 ⇒ miss」处（`bestTarget == null` 判定之后），⛔ 不在此处。

        // Faction 二元判定：过滤非己方单位
        // ⚠️ 本行现在**无条件执行**（不再被「候选为空」跳过）⇒ 须容忍攻击者已销毁（弹道飞行期 Unity 假 null）
        Faction attackerFaction = Faction.None;
        if (p.attacker is UnitController atkUnit && atkUnit != null) attackerFaction = atkUnit.GetFaction();
        else if (p.attacker is Building atkBuilding && atkBuilding != null) attackerFaction = atkBuilding.GetFaction();
        IDamageable bestTarget = null;
        float bestDist = float.MaxValue;

        foreach (var unit in candidates)
        {
            if (unit == null || unit.CurrentHp <= 0) continue;
            // D486 弹道命中放行（对齐 D485 ① 受击溯源通道）：未招募野人异族（raceId≠射手）视为敌对可命中。
            // 野人 EffectiveFaction 可能 = 射手阵营（PlayerCamp 可招募人口）→ 原"己方跳过"把野性敌人从弹道漏网；
            // 人口维度（raceId）判敌我，与野性敌意矩阵一致（防御塔/远程国民可射杀入侵野人）。
            var attackerUc = p.attacker as UnitController;
            bool hostileVagrant = unit.EffectiveOccupation == Occupation.Vagrant
                && !unit.IsVagrantRecruited
                && (attackerUc == null || attackerUc.raceId != unit.raceId);
            if (!hostileVagrant)
            {
                if (unit.GetFaction() == attackerFaction) continue; // 己方跳过
                if (unit.GetFaction() == Faction.None) continue;    // 无阵营跳过
            }

            // 【HH.320 件2】命中距离改**视觉格域**（`DistVisual`）：与射程/索敌同口径（⛔ 非格单位椭圆）
            float dist = GridMath.DistVisual(p.targetPos, unit.GetPosition());
            if (dist <= hitRadiusCells && dist < bestDist)
            {
                bestDist = dist;
                bestTarget = unit;
            }
        }

        // ⭐【件 6 · `D847` §2-1 返工修复】② 建筑兜底：落点落在**多格建筑菱形底座**内 ⇒ 命中该建筑
        //   （⛔ 非「到 pivot 圆心距」；① 单位命中优先 ⇒ 既有语义零回归）
        if (bestTarget == null)
            bestTarget = FindBuildingAtLanding(p.targetPos, attackerFaction);

        // ⭐③ **唯一早退点**：单位与建筑**都没命中**才是 miss
        //   （原「`candidates.Count == 0` 即 miss」已废 —— 它把建筑链一并挡在门外 ⇒ 件 6 在生产链上落空）
        if (bestTarget == null) return;

        // 命中 -> 走伤害计算（委托 DamageSystem）
        if (bestTarget != null)
        {
            // 2_20 M7：远程单体直伤走 isRanged=true（触发磐石远程减伤/盾卫庇护受方修正，D494/D492）
            DamageSystem.Instance?.ApplyDamage(p.attacker, bestTarget, p.attack, 0f, true);

            // 2_20 M7 火枪二段贯穿（D494）：命中主目标后贯穿额外目标，伤害=攻击×0.6（弹道方向粗略=命中点附近最近后续目标）
            if (p.pierceThroughCount > 0)
            {
                IDamageable throughTarget = null;
                float throughDist = float.MaxValue;
                foreach (var unit in candidates)
                {
                    if (unit == null || unit == bestTarget || unit.CurrentHp <= 0) continue;
                    if (unit.GetFaction() == attackerFaction || unit.GetFaction() == Faction.None) continue;
                    float dist = GridMath.DistVisual(p.targetPos, unit.GetPosition());   // 【HH.320 件2】视觉格域
                    if (dist <= hitRadiusCells && dist < throughDist) { throughDist = dist; throughTarget = unit; }
                }
                if (throughTarget != null)
                {
                    int throughDmg = Mathf.Max(1, Mathf.RoundToInt(p.attack * 0.6f));   // 贯穿传递 60%（D494）
                    DamageSystem.Instance?.ApplyDamage(p.attacker, throughTarget, throughDmg, 0f, true);
                }
            }

            // 溅射（3.6 §3.3 单段 AOE）：命中点半径内敌对单位
            if (p.aoeRadiusCells > 0f)
                DamageSystem.Instance?.ApplyImpact(p.attacker, bestTarget.GetPosition(),
                    p.attack, p.aoeRadiusCells, p.aoeFalloff);

            // 地面效果落地（3.6 §3.4：火弹灼烧场/魔弹减速场）
            if (p.effectType != GroundEffectType.None && GroundEffectManager.Instance != null)
                GroundEffectManager.Instance.SpawnEffect(
                    p.targetPos, p.attacker,
                    p.effectType, p.effectRadiusCells, p.effectDuration,
                    p.effectTickInterval, p.effectPower, p.effectMaxTargets);
        }
        // 无命中 = miss（原目标跑了且无人补位）
    }

    /// <summary>
    /// 越墙判定（3.6 §5 抛物线体系）：低抛（Straight/Lob）查射手→落点线段上的工事，
    /// 弧高 ≤ 工事高度 → 被挡（穿透够则对墙结算伤害，不够则无效）。
    /// 高抛（HighArc）直接越墙。返回 true=被挡。
    ///
    /// HH.243（DZ-149/D693）改 D485 单遍过滤法：旧实现逐格扫 `for(y=0;y<=1;y++)`——`GridCoord.y`
    /// 在 2.5D 已是**地图行号**（老"层"语义已迁 `layer` 字段）⇒ 只扫最南两行的工事，其余行工事
    /// 对低抛"隐形"（越墙判定失效）。改法＝沿弹道取**矩形带**内工事（点到线段距离 ≤ 半格宽，
    /// 排除起点/终点格）→ 取**最靠射手**的一枚 → 弧高/穿透判定**逐字保留**。
    /// 与旧行为等价性：原循环"弧高够 ⇒ continue 找后续工事"＝只认第一枚弧高不足的工事。
    /// </summary>
    private bool CheckWallBlock(ProjectileData p)
    {
        if (p.ballisticType == BallisticType.HighArc) return false; // 高抛越墙
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return false;
        if (UnitRegistry.Instance == null) return false;

        float bandHalf = GridMath.PathBandHalf;   // 【HH.320 件2】带半宽收口为单一世界量（＝屏幕横向半格 · 0.64）
        // ⭐【HH.320 件4 · D846 P4 同源铁律】阻挡者选取改走**唯一口**（与视线链 `CombatRules.HasLineOfSight` 同一函数）——
        //   两链各写一套 ⇒「选得到／打不到」必然复现（本批要除的病）。弧高豁免逐字保留（applyArcHeight=true）。
        UnitController blocker = CombatRules.FindFortificationBlocker(
            p.startPos, p.targetPos, bandHalf, applyArcHeight: true, arcHeightCells: p.arcHeightCells);

        if (blocker == null) return false;
        // 被挡：穿透等级决定对墙伤害（3.6 §5.1）—— ⛔ 本语义不得丢（件 3/件 4 红线）
        if (p.pierceLevel >= blocker.fortification.defenseLevel)
            DamageSystem.Instance?.ApplyDamage(p.attacker, blocker, p.attack, 0f, true);
        return true;
    }

    /// <summary>⭐【HH.320 件 6】**落点所在建筑**（多格建筑**菱形底座**命中）：
    /// 判据唯一口 ＝ <see cref="CombatRules.InDiamondBase"/>（格坐标下的轴对齐矩形 ≡ 世界空间等轴菱形底座，
    /// ⛔ 非「到 pivot 的圆心距」）。
    /// 查法：落点地块格 ±1 的 **3×3** `BuildingRegistry.GetAt`（`O(1)`×9 · **零分配**）——
    /// 底座按 sprite 居中张成（半宽 `footprint/2` 格）⇒ 落点格最多比 footprint 外扩 1 格 ⇒ 3×3 必覆盖候选。
    /// 过滤：同阵营／`None` 不计（与单位命中同一阵营口径）；非 `Active`／无 `def` 跳过。</summary>
    private Building FindBuildingAtLanding(Vector2 landing, Faction attackerFaction)
    {
        var grid = GridSystem.Instance;
        var reg = BuildingRegistry.Instance;
        if (grid == null || grid.Config == null || reg == null) return null;
        var cOpt = grid.WorldToCoord(landing);
        if (!cOpt.HasValue) return null;
        GridCoord c = cOpt.Value;
        Vector2 cellSize = grid.Config.cellSize;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var b = reg.GetAt(new GridCoord(c.x + dx, c.y + dy));
                if (b == null || b.def == null || !b.IsActive || b.CurrentHp <= 0) continue;
                var f = b.GetFaction();
                if (f == attackerFaction || f == Faction.None) continue;
                if (!CombatRules.InDiamondBase(landing, b.transform.position, b.footprint, cellSize)) continue;
                return b;
            }
        return null;
    }

    /// <summary>查目标位置附近微格内的单位（doc1 微格主表 D70，2_5 步骤3）。</summary>
    private List<UnitController> QueryNearbyUnits(Vector2 worldPos, float radiusCells)
    {
        var result = new List<UnitController>();
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return result;

        int subDiv = GridSystem.Instance.Config.subCellDivisor;
        var centerOpt = GridSystem.Instance.WorldToSubCoord(worldPos);
        if (!centerOpt.HasValue) return result; // doc1 改造：越界返回 null，返回空列表
        GridCoord center = centerOpt.Value;
        // 【HH.320 件18】**微格候选窗收口为单一口**（超集）：原 `radiusCells × subDiv` 非超集（R_vis=0.25 时须 2 而取 1
        //   ⇒ 判定圈内却不进候选 ＝ 漏命中）⇒ 改走 GridMath.SubWindowForVisualRadius（现配置 ⌈5·R_vis⌉ ⇒ 2）。
        int subRange = GridMath.SubWindowForVisualRadius(radiusCells, subDiv);

        for (int dy = -subRange; dy <= subRange; dy++)
        {
            for (int dx = -subRange; dx <= subRange; dx++)
            {
                result.AddRange(GridSystem.Instance.GetUnitsInSubCell(new GridCoord(center.x + dx, center.y + dy)));
            }
        }
        return result;
    }

    // ===== 对象池 =====

    private GameObject GetFromPool()
    {
        if (_pool.Count > 0)
            return _pool.Dequeue();

        // 池空，创建新投射物
        return CreateProjectileVisual();
    }

    private void ReturnToPool(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);

        if (_pool.Count >= ProjectilePoolSize)
        {
            // 超上限，直接销毁（降级处理）
            Destroy(go);
        }
        else
        {
            _pool.Enqueue(go);
        }
    }

    private void PreFillPool()
    {
        for (int i = 0; i < InitialPoolSize; i++)
        {
            var go = CreateProjectileVisual();
            go.SetActive(false);
            _pool.Enqueue(go);
        }
    }

    private GameObject CreateProjectileVisual()
    {
        EnsureSprite();
        var go = new GameObject("Projectile");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _runtimeSprite;
        sr.sortingOrder = SortingOrder;
        sr.color = Color.white;   // 3.7 P1.2：出池时按弹药类型着色，此处白底
        go.transform.localScale = Vector3.one * ProjectileScale;
        go.transform.SetParent(transform);
        return go;
    }

    /// <summary>
    /// 弹种 → 弹药 artId（H3 接线边界：只挂现役 <c>ProjectileType</c> 六值可消费素材；
    /// <c>ammo_musket</c>/<c>ammo_monster</c>/<c>ammo_mage_barrage</c> 入库不接线，挂 3.6.1 转正批）。
    /// </summary>
    private static string AmmoArtId(ProjectileType t)
    {
        switch (t)
        {
            case ProjectileType.Arrow:     return "ammo_arrow";
            case ProjectileType.Bolt:      return "ammo_arrow";       // 弩箭无独立素材（缺图面）→ 复用箭
            case ProjectileType.HeavyBolt: return "ammo_heavybolt";
            case ProjectileType.Stone:     return "ammo_stone";
            case ProjectileType.Fireball:  return "ammo_fireball";
            case ProjectileType.Magic:     return "ammo_magic";
            default:                       return null;
        }
    }

    /// <summary>
    /// 3.7 P1.2 弹药美术占位：按弹药类型返回占位色（复用单一 sprite + 色变区分弹型）。
    /// 色值区分度优先（观感次要），美术替换 sprite 后此映射可废弃。
    /// </summary>
    private static Color GetProjectileColor(ProjectileType type)
    {
        switch (type)
        {
            case ProjectileType.Arrow:    return new Color(1f, 0.9f, 0.3f);   // 黄：弓手箭
            case ProjectileType.Bolt:     return new Color(0.3f, 0.9f, 0.9f); // 青：弩箭
            case ProjectileType.HeavyBolt: return new Color(0.2f, 0.65f, 0.95f); // 深青：弩炮贯穿矢
            case ProjectileType.Stone:    return new Color(0.62f, 0.62f, 0.62f); // 灰：投石
            case ProjectileType.Fireball: return new Color(1f, 0.4f, 0.15f);  // 橙红：火弹（配 Burn 场）
            case ProjectileType.Magic:    return new Color(0.75f, 0.35f, 0.95f); // 紫：魔弹（配 Slow 场）
            default:                      return Color.yellow;                // 兜底
        }
    }

    private void EnsureSprite()
    {
        if (_projectileSprite != null)
        {
            _runtimeSprite = _projectileSprite;
            return;
        }

        // 运行时创建 4x4 黄色方块 sprite（验证用，美术后续替换）
        _runtimeSprite = CreateDefaultSprite();
    }

    private Sprite CreateDefaultSprite()
    {
        int size = 4;
        var tex = new Texture2D(size, size);
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), (float)size);
    }

    // ===== 辅助 =====
}
