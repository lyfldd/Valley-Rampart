using UnityEngine;

// 2_14 传送门实体（实施计划步骤5 / 设计稿 §2.3/§2.4）
// 职责：HP / 2×2 占格+阻挡 / 白天无敌 / 被打反向强化 / 召唤出怪锚点 / 摧毁清占格。
// A⁻ 决策：实现 IGridOccupant 以 2×2 入占格表 + IsGridObstacle=true（D154 同建筑占格语义），
//   Portal 非 Building 却可被 GridSystem.IsOccupied/IsObstacle/GetOccupant 查询。
// 视觉（崩塌动画/召唤动画）归 2_10；存档归 2_11；此处只做逻辑实体。
public class Portal : MonoBehaviour, IDamageable, IGridOccupant
{
    [Tooltip("传送门属性 SO（缺省从 Resources/Config/Disaster 加载）")]
    [SerializeField] private PortalDef def;

    public int hp;
    public int maxHp;
    public GridCoord gridPos;                    // 占格左上角（小区块坐标）
    public Vector2Int footprint = new Vector2Int(2, 2);
    public PortalState state = PortalState.Active;
    public int survivedNights;                   // 已存活夜晚数（烈度递减计算）

    // IGridOccupant：传送门占格且阻挡通行（D154）。
    public bool IsGridObstacle => true;

    // ===== IDamageable =====
    public int CurrentHp => hp;
    public int MaxHp => maxHp;
    public int Defense => 0;
    public UnityEngine.Vector2 GetPosition() => transform.position;
    public Faction GetFaction() => Faction.Undead;

    private bool _summonSpedUp;                  // 本夜是否已进入反向强化档

    public PortalDef Def => def;

    /// <summary>当前召唤间隔（被打反向强化：正常→summonIntervalOnHit，P0 二档；新夜在 OnSurviveNight 重置）。</summary>
    public float CurrentSummonInterval =>
        (_summonSpedUp && def != null) ? def.summonIntervalOnHit : (def != null ? def.summonInterval : 30f);

    /// <summary>是否占用（Destroying 前皆占用，供管理器查活跃传送门）。</summary>
    public bool IsActivePortal => state != PortalState.Destroying;

    private void OnEnable()
    {
        EventBus.Subscribe<TimePhaseChangedEvent>(OnPhaseChanged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<TimePhaseChangedEvent>(OnPhaseChanged);
    }

    /// <summary>初始化并占格（无视 footprint 是否已clear，由放置校验步骤13先行保证）。</summary>
    public void Initialize(GridCoord origin, PortalDef portalDef)
    {
        def = portalDef;
        gridPos = origin;
        if (def != null)
        {
            footprint = def.footprint;
            int difficulty = DifficultyManager.Instance != null ? DifficultyManager.Instance.CurrentDifficulty : 2;
            maxHp = def.GetBaseHp(difficulty);
        }
        else
        {
            footprint = new Vector2Int(2, 2);
            maxHp = 1000;
        }
        hp = maxHp;
        state = PortalState.Active;
        RegisterOccupancy();
    }

    private void OnPhaseChanged(TimePhaseChangedEvent evt)
    {
        // 设计§4.4：白天无敌 + 不召唤；夜晚恢复可攻击/可召唤
        state = evt.NewPhase == TimePhase.Day ? PortalState.DayProtected : PortalState.Active;
    }

    // ===== 召唤：收编入 WaveDirector.SpawnPortalDisasterWaves（2_14 步骤8 单轨收拢）。本实体不再自驱召唤 =====

    /// <summary>占格登记（2×2 + 阻挡）。摧毁由 DestroyPortal 释放。</summary>
    public void RegisterOccupancy()
    {
        if (GridSystem.Instance == null) return;
        GridSystem.Instance.MarkOccupiedFootprint(gridPos, footprint.x, footprint.y, this);
    }

    private void ReleaseOccupancy()
    {
        if (GridSystem.Instance == null) return;
        GridSystem.Instance.FreeFootprint(gridPos, footprint.x, footprint.y);
    }

    // 设计§2.4 被打规则：白天无敌 → 反向强化（召唤间隔减半 + 广播增援）→ HP≤0 摧毁
    public void TakeDamage(int finalDamage)
    {
        if (state == PortalState.DayProtected || state == PortalState.Destroying) return; // 白天不可攻击
        hp = Mathf.Max(0, hp - finalDamage);

        if (!_summonSpedUp && def != null)
        {
            _summonSpedUp = true;
            // 反向强化：CurrentSummonInterval 改用 summonIntervalOnHit + 广播增援（回援守门，步骤7 订阅）
            EventBus.Publish(new PortalAttackedEvent(transform.position));
        }

        if (hp <= 0) DestroyPortal();
    }

    public void Heal(int amount)
    {
        if (state == PortalState.Destroying) return;
        hp = Mathf.Min(maxHp, hp + amount);
    }

    /// <summary>新夜结算：存活天数+，重置反向强化档（烈度递减）。</summary>
    public void OnSurviveNight()
    {
        survivedNights++;
        _summonSpedUp = false;
    }

    private void DestroyPortal()
    {
        if (state == PortalState.Destroying) return;
        state = PortalState.Destroying;
        ReleaseOccupancy();
        EventBus.Publish(new PortalDestroyedEvent(transform.position));
        // 崩塌动画归 2_10；此处直接销毁（2_10 接入后改由动画延时回调）
        Destroy(gameObject);
    }
}