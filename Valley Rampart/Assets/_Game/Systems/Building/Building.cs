using UnityEngine;

/// <summary>建筑生命周期状态（3.3.4 批次3）。</summary>
public enum BuildingState
{
    Placing,        // 放置中（ghost）
    Constructing,   // 建造中（脚手架，不产出/不战斗）
    Active,         // 活跃（产出/战斗/可交互）
    Dead,           // 死亡（待销毁）
    Abandoned       // 废弃（主城初始，可修复但不产出）
}

/// <summary>
/// 运行时建筑实例。持有 BuildingDef 配置引用 + 运行时状态（level/hp/grade/state）。
/// 实现 IInteractable 接入统一交互派发。
///
/// 3.3.4 批次3：加入状态机 + 统一进度系统。建造/升级/修复都走 Constructing + 进度条，
/// 首版用"自动累计"（每秒+20%，5秒完成），3.10 后切"工人驱动"模式。
///
/// 地图预置建筑（树/矿/裂隙/主城）由 BuildingFactory 实例化，isPlayerBuilt=false；
/// 玩家建造由 BuildController 实例化，isPlayerBuilt=true。
/// </summary>
public class Building : MonoBehaviour, IInteractable
{
    // ===== 占位 =====
    [Header("占位")]
    public GridCoord coord;
    public int cellWidth = 1;
    public bool isObstacle = false;

    // ===== 来源 =====
    [Header("来源")]
    public BuildingType sourceType = BuildingType.None;
    public bool isPlayerBuilt = true;

    // ===== 配置与运行时状态（3.3 主体）=====
    [Header("配置")]
    public BuildingDef def;
    public Faction faction = Faction.None;

    [Header("运行时状态")]
    public int level = 1;
    public int hp;
    public int maxHp;
    public ResourceGrade grade = ResourceGrade.Normal;

    // ===== 状态机 + 进度系统（3.3.4 批次3）=====
    [Header("生命周期")]
    public BuildingState state = BuildingState.Active;
    [Range(0f, 1f)] public float constructProgress;
    [Tooltip("建造/升级进度时长（秒）。首版自动累计模式。")]
    public float constructDuration = 5f;

    private bool _pendingUpgrade;   // 当前 Constructing 是升级而非首次建造
    private SpriteRenderer _renderer;

    /// <summary>关联的 UI 面板（运行时注入，可为 null）。</summary>
    private IUIPanel _panel;

    /// <summary>当前是否可被交互（Active/Abandoned 可交互）。</summary>
    public bool IsInteractable => state == BuildingState.Active || state == BuildingState.Abandoned;

    /// <summary>是否已完成建造（Active 态）。</summary>
    public bool IsActive => state == BuildingState.Active;

    // ===== 初始化 =====

    /// <summary>玩家建造初始化（由 BuildController.Place 调）。默认 state=Active，调用方按需 StartConstructing。</summary>
    public void Init(BuildingDef def, GridCoord coord, bool isPlayerBuilt = true)
    {
        this.def = def;
        this.coord = coord;
        this.isPlayerBuilt = isPlayerBuilt;
        this.sourceType = BuildingType.None;
        this.grade = ResourceGrade.Normal;
        this.level = 1;
        this.cellWidth = def != null ? def.footprint.x : 1;

        ApplyDef();
        state = BuildingState.Active;
    }

    /// <summary>地图预置建筑初始化（由 BuildingFactory 调）。保留以兼容手动调用；CreateBuilding 当前走内联初始化，不使用此方法。</summary>
    public void InitFromPlaceholder(BuildingDef def, BuildingPlaceholder ph, GridCoord coord)
    {
        if (def == null)
        {
            this.def = null;
            this.coord = coord;
            this.isPlayerBuilt = false;
            this.sourceType = ph != null ? ph.type : BuildingType.None;
            this.grade = ResourceGrade.Normal;
            this.cellWidth = ph != null && ph.cellWidth > 0 ? ph.cellWidth : 1;
            this.level = 1;
            this.maxHp = 100;
            this.hp = 100;
            state = BuildingState.Active;
            return;
        }
        this.def = def;
        this.coord = coord;
        this.isPlayerBuilt = false;
        this.sourceType = ph != null ? ph.type : BuildingType.None;
        this.grade = ph != null ? ph.grade : ResourceGrade.Normal;
        this.cellWidth = (ph != null && ph.cellWidth > 0) ? ph.cellWidth : (def.footprint.x > 0 ? def.footprint.x : 1);
        this.level = 1;

        ApplyDef();
        state = BuildingState.Active;
    }

    /// <summary>按 BuildingDef 应用属性（含 gradeScale 缩放）。</summary>
    void ApplyDef()
    {
        if (def == null) return;

        faction = def.faction;
        isObstacle = def.isObstacle;

        // HP：有 combat（maxHp>0）用 combat.maxHp × gradeScale，否则默认 100
        float scale;
        try { scale = def.GetGradeScale(grade); }
        catch { scale = 1f; }
        int baseHp = def.combat.maxHp > 0 ? def.combat.maxHp : 100;
        maxHp = Mathf.Max(1, Mathf.RoundToInt(baseHp * Mathf.Max(0.1f, scale)));
        hp = maxHp;
    }

    // ===== 状态机 + 进度系统（3.3.4 批次3）=====

    /// <summary>开始建造/修复（进入 Constructing 态，显示脚手架）。</summary>
    public void StartConstructing()
    {
        _pendingUpgrade = false;
        state = BuildingState.Constructing;
        constructProgress = 0f;
        UpdateVisual();
    }

    private void Start()
    {
        // 地图预置建筑初始化视觉（含 Abandoned 暗化 + 占位缩放）
        UpdateVisual();
    }

    private void Update()
    {
        if (state != BuildingState.Constructing) return;
        // 暂停时不推进（Time.deltaTime 在 timeScale=0 时为 0，天然支持）
        constructProgress += Time.deltaTime / Mathf.Max(0.01f, constructDuration);
        if (constructProgress >= 1f)
        {
            constructProgress = 1f;
            OnConstructionComplete();
        }
    }

    /// <summary>建造/升级/修复完成。升级则提级，统一转 Active 并发激活事件。</summary>
    void OnConstructionComplete()
    {
        if (_pendingUpgrade && def != null && def.levels != null && level - 1 < def.levels.Length)
        {
            var lv = def.levels[level - 1];
            level++;
            maxHp = Mathf.RoundToInt(maxHp * lv.statScale);
            hp = maxHp;
            _pendingUpgrade = false;
            EventBus.Publish(new BuildingUpgradedEvent(this, level - 1, level));
        }
        state = BuildingState.Active;
        UpdateVisual();
        EventBus.Publish(new BuildingActivatedEvent(this));
    }

    /// <summary>按当前状态刷新视觉：Constructing 显示脚手架，其余显示正式占位。占位 sprite 按 cellWidth 缩放。</summary>
    void UpdateVisual()
    {
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        float cellSize = GridSystem.Instance != null && GridSystem.Instance.Config != null ? GridSystem.Instance.Config.cellSize : 2.26f;
        // 占位 sprite 是 1x1 世界单位，按 cellWidth × cellSize 缩放到实际占地尺寸
        transform.localScale = new Vector3(Mathf.Max(1, cellWidth) * cellSize, cellSize, 1);

        if (state == BuildingState.Constructing)
        {
            // 脚手架（半透明棕方块）
            if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sprite = PlaceholderSprites.Get("scaffold");
            _renderer.sortingOrder = 1;
        }
        else
        {
            // 正式占位视觉
            BuildingVisual.ApplyPlaceholder(gameObject, sourceType, def != null ? def.role : BuildingRole.Special);
            _renderer = GetComponent<SpriteRenderer>();
            // Abandoned 态变暗提示废弃
            if (_renderer != null && state == BuildingState.Abandoned)
                _renderer.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        }
    }

    // ===== IInteractable =====

    public InteractionResult Interact(Interactor ctx)
    {
        // 非玩家阵营建筑不可交互（3.3.4 批次10 留口，首版直接拒绝敌方）
        if (faction != Faction.Human_Player && faction != Faction.None)
            return InteractionResult.None;

        // 打开 BuildingPanel（首版用 FindObjectOfType 找场景面板，后期可改为注入）
        var panel = FindObjectOfType<BuildingPanel>();
        if (panel != null)
        {
            panel.SetTarget(this);
            return InteractionResult.ShowUI(panel);
        }
        return InteractionResult.None;
    }

    /// <summary>注入 UI 面板（备用，首版用 BuildingPanel.Instance）。</summary>
    public void SetPanel(IUIPanel panel) { _panel = panel; }

    // ===== 升级（走 Constructing 进度，数据保留）=====

    /// <summary>升级（由 BuildingPanel 调，资源已校验）。进入 Constructing，完成时提级。</summary>
    public bool TryUpgrade()
    {
        if (def == null || def.levels == null || def.levels.Length == 0) return false;
        if (level - 1 >= def.levels.Length) return false; // 已满级
        if (state != BuildingState.Active) return false;  // 只有 Active 可升级

        _pendingUpgrade = true;
        state = BuildingState.Constructing;
        constructProgress = 0f;
        UpdateVisual();
        return true;
    }

    // ===== 拆除（按 HP 比例返还资源）=====

    /// <summary>拆除建筑（由 BuildingPanel 调）。按 HP 比例返还造价资源。</summary>
    public void Demolish()
    {
        if (!isPlayerBuilt || def == null || !def.isDestructible) return;
        float ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
        RulerController.Instance?.Refund(def.cost, ratio);
        Die();
    }

    // ===== 战斗（3.4/3.5 对接）=====

    public void TakeDamage(int amount)
    {
        if (state != BuildingState.Active) return; // 非 Active 不受伤
        hp = Mathf.Max(0, hp - amount);
        if (hp <= 0) Die();
    }

    public void Die()
    {
        state = BuildingState.Dead;
        GridSystem.Instance?.FreeFootprint(coord, cellWidth);
        BuildingRegistry.Instance?.Unregister(this);
        EventBus.Publish(new BuildingDestroyedEvent(this));
        Destroy(gameObject);
    }
}
