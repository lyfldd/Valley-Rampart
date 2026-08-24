using UnityEngine;

// 2_14 传送门实体（实施计划步骤5 / 设计稿 §2.3/§2.4）
// 职责：HP / 2×2 占格+阻挡 / 白天无敌 / 被打反向强化 / 召唤出怪锚点 / 摧毁清占格。
// A⁻ 决策：实现 IGridOccupant 以 2×2 入占格表 + IsGridObstacle=true（D154 同建筑占格语义），
//   Portal 非 Building 却可被 GridSystem.IsOccupied/IsObstacle/GetOccupant 查询。
// 视觉（崩塌动画/召唤动画）归 2_10；存档归 2_11；此处只做逻辑实体。
public class Portal : MonoBehaviour, IDamageable, IGridOccupant, ISaveable
{
    // ===== ISaveable（2_14 步骤14：传送门持久化，SaveManager 场景阶段）=====
    public string SaveId { get; private set; }
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Scene;

    private void Awake()
    {
        // 运行期为每扇门分配唯一 SaveId 并注册；读档时由 WaveDirector.SpawnFromSave 覆盖并恢复。
        if (string.IsNullOrEmpty(SaveId))
        {
            SaveId = $"Portal_{System.Guid.NewGuid():N}";
            SaveManager.Instance?.RegisterSaveable(this);
        }
    }

    /// <summary>用存档里的 SaveId 覆盖 Awake 分配的新 GUID（读档时由 WaveDirector spawner 调）。</summary>
    public void OverrideSaveId(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        string oldId = SaveId;
        SaveId = id;
        SaveManager.Instance?.ChangeSaveId(oldId, id, this);
    }

    public SavePayload SaveState()
    {
        var data = new PortalSaveData
        {
            portalGridX = gridPos.x,
            portalGridY = gridPos.y,
            portalHp = hp,
            portalSurvivedNights = survivedNights,
            portalState = (int)state
        };
        return new SavePayload
        {
            typeName = typeof(PortalSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(PortalSaveData).AssemblyQualifiedName) return;
        var data = JsonUtility.FromJson<PortalSaveData>(payload.json);
        // 占格位置由 spawner 读同一份 json 重建；此处恢复 HP/存活夜/状态（保证闭环）。
        hp = Mathf.Min(maxHp, Mathf.Max(0, data.portalHp));
        survivedNights = data.portalSurvivedNights;
        state = (PortalState)data.portalState;
    }

    private void OnDestroy()
    {
        if (!string.IsNullOrEmpty(SaveId))
            SaveManager.Instance?.UnregisterSaveable(this);
    }
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
            // 步骤9 强度曲线：传送门 HP = 基础(难度) × (1 + 天×growthRate)
            maxHp = Mathf.RoundToInt(def.GetBaseHp(difficulty) * PortalHpDayScale());
            // 难度系数(D236)亦作用于传送门韧性
            maxHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * PortalDifficultyScale(difficulty)));
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

    /// <summary>步骤9 传送门 HP 天数缩放 = (1 + 天×growthRate)，缺配置回退 1。</summary>
    private static float PortalHpDayScale()
    {
        var cfg = Resources.Load<PortalDisasterConfig>("Config/Disaster/PortalDisasterConfig");
        if (cfg == null) return 1f;
        int day = TimeManager.Instance != null ? Mathf.Max(1, TimeManager.Instance.CurrentDay) : 1;
        return 1f + day * cfg.growthRate;
    }

    /// <summary>步骤9 难度系数(D236) 作用于传送门韧性（Easy 0.7 / Normal 1.0 / Hard 1.3）。</summary>
    private static float PortalDifficultyScale(int difficulty)
    {
        var cfg = Resources.Load<PortalDisasterConfig>("Config/Disaster/PortalDisasterConfig");
        if (cfg == null) return 1f;
        return cfg.GetWaveCoefficient(Mathf.Max(1, difficulty));
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