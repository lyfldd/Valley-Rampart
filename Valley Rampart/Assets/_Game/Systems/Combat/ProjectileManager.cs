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
        float errorRadius = _config.projectileErrorRadius;
        if (errorRadius > 0f)
        {
            targetPos += Random.insideUnitCircle * errorRadius;
        }

        float distance = Vector2.Distance(startPos, targetPos);
        float duration = distance / Mathf.Max(0.1f, profile.projectileSpeed);

        GameObject visual = GetFromPool();
        visual.transform.position = startPos;
        visual.SetActive(true);
        // 3.7 P1.2：按弹药类型着色（复用单一 sprite，色变区分箭/弩/石/火/魔）
        var sr = visual.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = GetProjectileColor(profile.projectileType);

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

        if (candidates.Count == 0) return; // miss

        // Faction 二元判定：过滤非己方单位
        Faction attackerFaction = p.attacker.GetFaction();
        IDamageable bestTarget = null;
        float bestDist = float.MaxValue;

        foreach (var unit in candidates)
        {
            if (unit == null || unit.CurrentHp <= 0) continue;
            if (unit.GetFaction() == attackerFaction) continue; // 己方跳过
            if (unit.GetFaction() == Faction.None) continue;    // 无阵营跳过

            float dist = GridMath.DistCells(p.targetPos, unit.GetPosition());
            if (dist <= hitRadiusCells && dist < bestDist)
            {
                bestDist = dist;
                bestTarget = unit;
            }
        }

        // 命中 -> 走伤害计算（委托 DamageSystem）
        if (bestTarget != null)
        {
            DamageSystem.Instance?.ApplyDamage(p.attacker, bestTarget, p.attack);

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
    /// </summary>
    private bool CheckWallBlock(ProjectileData p)
    {
        if (p.ballisticType == BallisticType.HighArc) return false; // 高抛越墙
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return false;

        // doc1 改造：WorldToCoord 返回 GridCoord?（null=越界），越界视为不被墙挡
        var startOpt = GridSystem.Instance.WorldToCoord(p.startPos);
        var endOpt = GridSystem.Instance.WorldToCoord(p.targetPos);
        if (!startOpt.HasValue || !endOpt.HasValue) return false;
        int startCell = startOpt.Value.x;
        int endCell = endOpt.Value.x;
        if (startCell == endCell) return false;

        int dir = startCell < endCell ? 1 : -1;
        for (int cx = startCell + dir; cx != endCell; cx += dir)
        {
            for (int y = 0; y <= 1; y++)
            {
                var list = GridSystem.Instance.GetUnitsInCell(new GridCoord(cx, y));
                foreach (var unit in list)
                {
                    var uc = unit as UnitController;
                    if (uc == null || uc.fortification == null) continue;
                    var fort = uc.fortification;
                    if (p.arcHeightCells > fort.heightCells) continue; // 弧高够 → 越过该工事
                    // 被挡：穿透等级决定对墙伤害（3.6 §5.1）
                    if (p.pierceLevel >= fort.defenseLevel)
                        DamageSystem.Instance?.ApplyDamage(p.attacker, uc, p.attack);
                    return true;
                }
            }
        }
        return false;
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
        int subRange = Mathf.Max(0, Mathf.CeilToInt(radiusCells * subDiv));

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
