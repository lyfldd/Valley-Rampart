using UnityEngine;

/// <summary>
/// 运行时建筑实例。持有 BuildingDef 配置引用 + 运行时状态（level/hp/grade）。
/// 实现 IInteractable 接入统一交互派发。
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

    /// <summary>关联的 UI 面板（运行时注入，可为 null）。</summary>
    private IUIPanel _panel;

    /// <summary>当前是否可被交互（建造中态可禁用）。</summary>
    public bool IsInteractable => true;

    // ===== 初始化 =====

    /// <summary>玩家建造初始化（由 BuildController.Place 调）。</summary>
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

    // ===== IInteractable =====

    public InteractionResult Interact(Interactor ctx)
    {
        // 打开 BuildingPanel（首版用 FindObjectOfType 找场景面板，后期可改为注入）
        var panel = FindObjectOfType<BuildingPanel>();
        if (panel != null)
        {
            panel.SetTarget(this);
            return InteractionResult.ShowUI(panel);
        }
        return InteractionResult.None;
    }

    /// <summary>注入 UI 面板（备用，首版用 BuildingPanel.Instance 单例）。</summary>
    public void SetPanel(IUIPanel panel) { _panel = panel; }

    // ===== 升级 =====

    /// <summary>升级（由 BuildingPanel 调，资源已校验）。</summary>
    public bool TryUpgrade()
    {
        if (def == null || def.levels == null || def.levels.Length == 0) return false;
        if (level - 1 >= def.levels.Length) return false; // 已满级

        var lv = def.levels[level - 1];
        level++;
        maxHp = Mathf.RoundToInt(maxHp * lv.statScale);
        hp = maxHp;
        EventBus.Publish(new BuildingUpgradedEvent(this, level - 1, level));
        return true;
    }

    // ===== 战斗（3.4/3.5 对接）=====

    public void TakeDamage(int amount)
    {
        hp = Mathf.Max(0, hp - amount);
        if (hp <= 0) Die();
    }

    void Die()
    {
        GridSystem.Instance?.FreeFootprint(coord, cellWidth);
        BuildingRegistry.Instance?.Unregister(this);
        EventBus.Publish(new BuildingDestroyedEvent(this));
        Destroy(gameObject);
    }
}
