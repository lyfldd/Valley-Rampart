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
    private const int MaxPoolSize = 200;

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

        _active.Add(new ProjectileData
        {
            startPos = startPos,
            targetPos = targetPos,
            speed = profile.projectileSpeed,
            elapsed = 0f,
            duration = duration,
            attacker = attacker,
            attack = profile.attack,
            visual = visual
        });
    }

    // ===== 集中 Update（所有投射物统一推进，非每个自己 MonoBehaviour）=====

    private void Update()
    {
        if (_active.Count == 0) return;

        float dt = Time.deltaTime;
        float cellSize = GetCellSize();
        float hitRadiusWorld = HitRadiusCells * cellSize;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var p = _active[i];
            p.elapsed += dt;
            float t = p.duration > 0f ? p.elapsed / p.duration : 1f;

            if (t >= 1f)
            {
                // 到达时刻：位置检测（一次，决策 9+12）
                OnProjectileArrived(p, hitRadiusWorld);
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
    private void OnProjectileArrived(ProjectileData p, float hitRadiusWorld)
    {
        // 查 GridSystem 附近格子的单位
        List<UnitController> candidates = QueryNearbyUnits(p.targetPos, hitRadiusWorld);

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

            float dist = Vector2.Distance(p.targetPos, unit.GetPosition());
            if (dist < bestDist)
            {
                bestDist = dist;
                bestTarget = unit;
            }
        }

        // 命中 -> 走伤害计算（委托 DamageSystem）
        if (bestTarget != null)
        {
            DamageSystem.Instance?.ApplyDamage(p.attacker, bestTarget, p.attack);
        }
        // 无命中 = miss（原目标跑了且无人补位）
    }

    /// <summary>查目标位置附近格子内的单位（空间分区，复用 GridSystem）。</summary>
    private List<UnitController> QueryNearbyUnits(Vector2 worldPos, float radiusWorld)
    {
        var result = new List<UnitController>();
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return result;

        float cellSize = GridSystem.Instance.Config.cellSize;
        GridCoord center = GridSystem.Instance.WorldToCoord(worldPos);
        int cellRange = Mathf.Max(1, Mathf.CeilToInt(radiusWorld / cellSize));

        // 查附近格子（1D 横版：x 范围，y=0 地面 + y=1 飞行）
        for (int dx = -cellRange; dx <= cellRange; dx++)
        {
            for (int y = 0; y <= 1; y++)
            {
                var coord = new GridCoord(center.x + dx, y);
                result.AddRange(GridSystem.Instance.GetUnitsInCell(coord));
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

        if (_pool.Count >= MaxPoolSize)
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
        sr.color = Color.yellow;
        go.transform.localScale = Vector3.one * ProjectileScale;
        go.transform.SetParent(transform);
        return go;
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

    private float GetCellSize()
    {
        return GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize : 2.26f;
    }
}
