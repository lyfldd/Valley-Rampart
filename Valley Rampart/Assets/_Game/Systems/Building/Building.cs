using System.Collections.Generic;
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
/// 实现 IInteractable 接入统一交互派发；3.4 实现 IDamageable 统一走 DamageSystem。
///
/// 3.3.4 批次3：加入状态机 + 统一进度系统。建造/升级/修复都走 Constructing + 进度条，
/// 首版用"自动累计"（每秒+20%，5秒完成），3.10 后切"工人驱动"模式。
///
/// 3.4 重构：实现 IDamageable；Die 加 DeathCause 参数区分拆除/被击杀；
/// BuildingDestroyedEvent 退役，改发 UnitDiedEvent；补 Heal 空实现（建筑不回血）。
///
/// 地图预置建筑（树/矿/裂隙/主城）由 BuildingFactory 实例化，isPlayerBuilt=false；
/// 玩家建造由 BuildController 实例化，isPlayerBuilt=true。
/// </summary>
public class Building : MonoBehaviour, IInteractable, IDamageable, ISaveable, ITaskSource
{
    // ===== ISaveable（3.5 实施计划 P0 步骤3）=====
    /// <summary>全局唯一存档 ID（Building_{guid}）。Awake 分配，读档时由 BuildingFactory.SpawnFromSave 覆盖。</summary>
    public string SaveId { get; private set; }
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Scene;

    private void Awake()
    {
        if (string.IsNullOrEmpty(SaveId))
        {
            SaveId = $"Building_{System.Guid.NewGuid():N}";
            SaveManager.Instance?.RegisterSaveable(this);
        }
    }

    /// <summary>用存档里的 SaveId 覆盖 Awake 分配的新 GUID（读档时由 BuildingFactory 调）。</summary>
    public void OverrideSaveId(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        string oldId = SaveId;
        SaveId = id;
        SaveManager.Instance?.ChangeSaveId(oldId, id, this);
    }

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

    // ===== 3.5 P1-15 当前在册工人（3.5.3 §7.4 / 3.5.4 §8.5）=====
    // 本建筑当前服务的通用工人引用列表。建筑被摧毁时由 Die() 扫描 → 工人逃出存活。
    // 由 ScheduleCenter 派发工人时登记 / 工人离开时移除（P1 任务调度扩展接入）。
    [Tooltip("当前在册工人（建筑被摧毁时逃出存活）。ScheduleCenter 派工时登记")]
    public readonly List<UnitController> currentWorkers = new List<UnitController>();

    // ===== QQQ.2 T19：一次性资源点采集锁定（RES-A4 / DR-11）=====
    // 玩家点击确认采集后置 true（锁定防重复派发）；采集完成/中断后复位可再点击（RES-A2）。
    // 不入档（QQQ.3 D14：读档后回未锁态可重新点击，进度重置语义一致）。
    [Tooltip("是否已确认采集（一次性资源点锁定中，防连点/重复派发）")]
    public bool isBeingGathered;

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

    // ===== IDamageable 实现（3.4）=====
    // 封装 hp/maxHp 字段为 IDamageable 属性，内部代码仍用 hp/maxHp 字段直接操作。

    /// <summary>当前血量（封装 hp 字段）。</summary>
    public int CurrentHp => hp;

    /// <summary>最大血量（封装 maxHp 字段）。</summary>
    public int MaxHp => maxHp;

    /// <summary>护甲值（复用 BuildingDef.combat.defense，供 DamageSystem 减伤计算）。</summary>
    public int Defense => def != null ? def.combat.defense : 0;

    /// <summary>世界坐标位置。</summary>
    public Vector2 GetPosition() => transform.position;

    /// <summary>阵营。</summary>
    public Faction GetFaction() => faction;

    /// <summary>
    /// 是否被足够工人操作（改动② 战争机器乘员：Catapult 建筑层 crew 机制）。
    /// crewRequired<=0 恒 true（不需工人）；否则统计 crewRadiusCells 半径内同阵营纯工人（attack<=0 且 roleFamily==None，复用 Civilian）。
    /// 供建筑/防御攻击驱动（CombatComponent 落点）在发射前门控——工人不足则停火停机。
    /// </summary>
    public bool HasEnoughCrew()
    {
        if (def == null || def.crewRequired <= 0) return true;
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return false;
        float cellSize = GridSystem.Instance.Config.cellSize;
        float crewRadius = def.crewRadiusCells * cellSize;
        GridCoord center = GridSystem.Instance.WorldToCoord(transform.position);
        int cellRange = Mathf.Max(1, Mathf.CeilToInt(crewRadius / cellSize));
        int count = 0;
        for (int dx = -cellRange; dx <= cellRange; dx++)
        {
            for (int y = 0; y <= 1; y++)
            {
                var units = GridSystem.Instance.GetUnitsInCell(new GridCoord(center.x + dx, y));
                foreach (var unit in units)
                {
                    var uc = unit as UnitController;
                    if (uc == null || !uc.IsAlive || uc.CurrentHp <= 0) continue;
                    if (uc.GetFaction() != faction) continue;
                    var nd = uc.Data as NpcProfessionDef;
                    if (nd != null && !(nd.attack <= 0 && nd.roleFamily == RoleFamily.None)) continue;
                    if (Vector2.Distance(transform.position, uc.transform.position) > crewRadius) continue;
                    count++;
                }
            }
        }
        return count >= def.crewRequired;
    }

    /// <summary>缺几名工人（0=已满编/不需工人）。供调度中心（ScheduleCenterStub.DispatchCrew）按缺口派工人到建筑旁。</summary>
    public int CrewDeficit()
    {
        if (def == null || def.crewRequired <= 0) return 0;
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return def.crewRequired;
        float cellSize = GridSystem.Instance.Config.cellSize;
        float crewRadius = def.crewRadiusCells * cellSize;
        GridCoord center = GridSystem.Instance.WorldToCoord(transform.position);
        int cellRange = Mathf.Max(1, Mathf.CeilToInt(crewRadius / cellSize));
        int count = 0;
        for (int dx = -cellRange; dx <= cellRange; dx++)
        {
            for (int y = 0; y <= 1; y++)
            {
                var units = GridSystem.Instance.GetUnitsInCell(new GridCoord(center.x + dx, y));
                foreach (var unit in units)
                {
                    var uc = unit as UnitController;
                    if (uc == null || !uc.IsAlive || uc.CurrentHp <= 0) continue;
                    if (uc.GetFaction() != faction) continue;
                    var nd = uc.Data as NpcProfessionDef;
                    if (nd != null && !(nd.attack <= 0 && nd.roleFamily == RoleFamily.None)) continue;
                    if (Vector2.Distance(transform.position, uc.transform.position) > crewRadius) continue;
                    count++;
                }
            }
        }
        return Mathf.Max(0, def.crewRequired - count);
    }

    /// <summary>附近是否有敌（有敌情才派工人操作，供调度中心做敌情门控，避免锁死工人）。</summary>
    public bool HasNearbyEnemy() => HasNearbyEnemy(def != null ? def.combat.range : 8f);

    /// <summary>附近是否有敌（指定探测范围）。</summary>
    public bool HasNearbyEnemy(float rangeWorld)
    {
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return false;
        float cellSize = GridSystem.Instance.Config.cellSize;
        int range = Mathf.Max(1, Mathf.CeilToInt(rangeWorld / cellSize));
        GridCoord center = GridSystem.Instance.WorldToCoord(transform.position);
        for (int dx = -range; dx <= range; dx++)
        {
            for (int y = 0; y <= 1; y++)
            {
                var units = GridSystem.Instance.GetUnitsInCell(new GridCoord(center.x + dx, y));
                foreach (var unit in units)
                {
                    var uc = unit as UnitController;
                    if (uc == null || !uc.IsAlive) continue;
                    if (uc.GetFaction() == faction) continue;   // 非友方 = 敌
                    if (Vector2.Distance(transform.position, uc.transform.position) > rangeWorld) continue;
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 恢复血量（建筑不回血，空实现）。首版不触发，后续对接资源系统时按需实装。
    /// </summary>
    public void Heal(int amount) { }

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

    /// <summary>按 BuildingDef 应用属性（含 gradeScale 缩放）。public 供 BuildingFactory.SpawnFromSave 读档后按 grade 重算（QQQ.3 B8-5）。</summary>
    public void ApplyDef()
    {
        if (def == null) return;

        faction = def.faction;
        isObstacle = def.isObstacle;

        // HP：统一入口 = def.maxHp（3.5.1 E-S10）× gradeScale；防御建筑 combat.maxHp 与主层同值
        float scale;
        try { scale = def.GetGradeScale(grade); }
        catch { scale = 1f; }
        int baseHp = def.maxHp > 0 ? def.maxHp : 100;
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
        RegisterWithTaskScheduler();   // QQQ.2 T17：转 Active 注册任务源
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
        if (!isPlayerBuilt || def == null || !def.isDestructible || def.isResourceNode) return;
        float ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
        RulerController.Instance?.Refund(def.cost, ratio);
        Die(DeathCause.Demolished);
    }

    // ===== ISaveable 实现（3.5 实施计划 P0 步骤3）=====

    public SavePayload SaveState()
    {
        var storage = GetComponent<StorageComponent>();
        var producer = GetComponent<ProducerComponent>();
        var data = new BuildingSaveData
        {
            defId = def != null ? def.id : "",
            coordX = coord.x,
            level = level,
            hp = hp,
            maxHp = maxHp,
            faction = (int)faction,
            state = (int)state,
            cellWidth = cellWidth,
            sourceType = (int)sourceType,
            storedAmount = storage != null ? storage.storedAmount : 0,
            byproductType = producer != null ? (int)producer.ByproductType : 0,
            byproductAmount = producer != null ? producer.ByproductAmount : 0,
            grade = (int)grade   // QQQ.3 B8-5 / LC-B2：grade 入档
        };
        return new SavePayload
        {
            typeName = typeof(BuildingSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(BuildingSaveData).AssemblyQualifiedName) return;
        var data = JsonUtility.FromJson<BuildingSaveData>(payload.json);

        level = Mathf.Max(1, data.level);

        // QQQ.3 B8-5 / LC-B2：grade 恢复 + 重算属性（修复读档后产能永久降贫瘠档 rate×0.7）。
        // 先设 grade 再 ApplyDef ⇒ maxHp 按新等级重算；随后恢复保存的 hp（clamp 到新 maxHp，不因 ApplyDef 重置满血）。
        grade = (ResourceGrade)data.grade;
        ApplyDef();
        maxHp = Mathf.Max(1, data.maxHp);
        hp = Mathf.Clamp(data.hp, 0, maxHp);

        var storage = GetComponent<StorageComponent>();
        if (storage != null) storage.storedAmount = Mathf.Max(0, data.storedAmount);

        var producer = GetComponent<ProducerComponent>();
        if (producer != null) producer.RestoreByproduct(data.byproductType, data.byproductAmount);

        // 网格占用恢复（Spawning 已占用，此处兜底幂等）
        if (GridSystem.Instance != null)
            GridSystem.Instance.MarkOccupiedFootprint(coord, Mathf.Max(1, cellWidth), this);
    }

    // ===== 战斗（3.4 实现 IDamageable）=====

    /// <summary>
    /// 受到伤害，只扣血。伤害已由 DamageSystem 算好+取整。
    /// 血量≤0 触发 Die(Killed)。非 Active 态不受伤。
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (state != BuildingState.Active) return; // 非 Active 不受伤
        hp = Mathf.Max(0, hp - amount);
        if (hp <= 0) Die(DeathCause.Killed);
    }

    /// <summary>
    /// 死亡处理。3.4 改造：加 DeathCause 参数区分拆除/被击杀；
    /// 改发 UnitDiedEvent（BuildingDestroyedEvent 退役）。
    /// 3.5 P1-15：扫描 currentWorkers → 工人逃出存活（位置 +1 格偏移，变无任务状态，不死亡）。
    /// 3.5 P1-10：训练建筑摧毁 → 通知 TrainingSystem 释放训练中居民（回退无职业，资源不退）。
    /// </summary>
    public void Die(DeathCause cause = DeathCause.Killed)
    {
        state = BuildingState.Dead;

        // 3.5 P1-15：在册工人逃出存活（先于 FreeFootprint/Destroy 执行，避免引用失效）
        EscapeWorkers();

        // 3.5 P1-10：训练建筑摧毁 → 训练队列中断回退（居民存活、资源不退）
        if (TrainingSystem.Instance != null)
            TrainingSystem.Instance.OnBuildingDestroyed(this);

        GridSystem.Instance?.FreeFootprint(coord, cellWidth);
        BuildingRegistry.Instance?.Unregister(this);
        // QQQ.2 T17：从任务调度器注销（清指向本建筑的在派任务）
        if (TaskScheduler.HasInstance) TaskScheduler.Instance.Unregister(this);
        // QQQ.3 B8-9 / LC-B8：Die 显式注销 Saveable（否则 _saveables 留残留条目，本应主动清理而非等兜底）
        if (SaveManager.Instance != null) SaveManager.Instance.UnregisterSaveable(this);

        // 3.4：改发 UnitDiedEvent（建筑也走此事件，BuildingDestroyedEvent 退役）
        EventBus.Publish(new UnitDiedEvent(
            this,              // Unit (IDamageable)
            faction,           // Faction
            transform.position,// Position
            null,              // Killer（建筑被击杀时无特定击杀者，DamageSystem 可补充）
            cause              // Cause（Killed=被击杀，Demolished=玩家拆除）
        ));

        Destroy(gameObject);
    }

    /// <summary>
    /// QQQ.2 T19 / DR-11：一次性资源点采集完成生命周期（由 TaskScheduler.Gather 完成时调）。
    /// 三步：①释放网格占用（GridSystem.Free）②从 BuildingRegistry 移除 ③对象池 Despawn（不直接 Destroy）。
    /// 采集任务由调度器派发，工人 Working 计时到后触发；资源入国库已在调度器 ExecuteCompletion 完成。
    /// </summary>
    public void OnGatherCompleted()
    {
        if (def == null || !def.isConsumable) return;
        isBeingGathered = false;

        // ① 释放网格占用
        GridSystem.Instance?.FreeFootprint(coord, cellWidth);
        // ①b QQQ.2 T8 / DR-21：采集后空地加入闲逛锚点池（王国多锚点之一，持久）
        WanderAnchorPool.Instance.RegisterFreeSpot(transform.position);
        // ② 从注册表移除
        BuildingRegistry.Instance?.Unregister(this);
        // 从任务调度器注销（清可能残留的在派任务）
        if (TaskScheduler.HasInstance) TaskScheduler.Instance.Unregister(this);
        // 存档注销（防 SaveManager 残留条目）
        if (SaveManager.Instance != null) SaveManager.Instance.UnregisterSaveable(this);
        // ③ 对象池回收（DR-11：不直接 Destroy，与 UnitFactory 一致）
        if (BuildingFactory.Instance != null)
            BuildingFactory.Instance.ReturnBuildingToPool(this);
        else
            Destroy(gameObject);
        Debug.Log($"[Building] 一次性资源点 {def.id} 采集完成，已回收。");
    }

    /// <summary>
    /// QQQ.2 T19 / DR-11：玩家确认采集一次性资源点（BuildingPanel 采集按钮调）。
    /// 锁定 isBeingGathered（RES-A4 防连点/重复派发），调度器下 tick 派发 Gather 任务。
    /// </summary>
    public void StartGather()
    {
        if (def == null || !def.isConsumable || !IsActive) return;
        if (isBeingGathered) return;   // UI-A4：已确认/采集中，防连点
        isBeingGathered = true;
        Debug.Log($"[Building] 确认采集：{def.id}（预计 {def.gatherSeconds:F0} 秒），锁定待派发。");
    }

    /// <summary>
    /// 3.5 P1-15 工人逃出（3.5.3 §7.4 / 3.5.4 §8.5）：建筑被摧毁时当前在册工人存活。
    /// 每个工人：① 清除任务状态（变 Idle）② 位置 +1 格偏移逃离（避免卡在废墟格）③ 不死亡。
    /// 逃出后可被 ScheduleCenter 重新派发任务。
    /// </summary>
    private void EscapeWorkers()
    {
        if (currentWorkers == null || currentWorkers.Count == 0) return;
        float cellSize = GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize : 2.26f;
        int idx = 0;
        for (int i = currentWorkers.Count - 1; i >= 0; i--)
        {
            var w = currentWorkers[i];
            if (w == null) { currentWorkers.RemoveAt(i); continue; }
            if (!w.IsAlive) { currentWorkers.RemoveAt(i); continue; }

            // ① 清除任务状态（变 Idle）：任务记录在 TaskScheduler（WorkerTask 已内化，无组件）——放弃该工人的在派任务
            if (TaskScheduler.HasInstance && w.npcId != 0)
                TaskScheduler.Instance.AbandonTask(w.npcId);
            var brain = w.GetComponent<NPCBrain>();
            if (brain != null)
            {
                // 释放搬运任务（issuer=StorageComponent）与 crew 任务（issuer=本建筑），变 Idle 可被重新派发
                var storage = GetComponent<StorageComponent>();
                if (storage != null) brain.RemoveTaskStimulus(storage);
                brain.RemoveTaskStimulus(this);
            }

            // ② 位置 +1 格偏移逃离（逐工人递增偏移，避免重叠）
            int dir = (idx % 2 == 0) ? 1 : -1;   // 交替左右偏移
            int cells = (idx / 2) + 1;
            Vector2 escape = (Vector2)transform.position + new Vector2(dir * cells * cellSize, 0f);
            w.Teleport(escape);
            currentWorkers.RemoveAt(i);
            idx++;
        }
    }

    // ===== ITaskSource 实现（QQQ.2 §10.1/§10.3，DR-16）=====

    [Header("任务调度（QQQ.2 T17）")]
    [Tooltip("存储达标触发搬运阈值（存量 ≥ capacity×此值 发布 Transport）")]
    public float transportThreshold = 0.8f;
    [Tooltip("水网缺水阈值（Stored < 此值 农场发布 WaterHaul）")]
    public float waterThreshold = 20f;

    /// <summary>任务源世界坐标（建筑坐标）。</summary>
    public Vector2 SourcePos => transform.position;

    /// <summary>任务源是否有效（未被销毁且已 Active）。</summary>
    public bool IsValid => this != null && state == BuildingState.Active;

    /// <summary>
    /// 按建筑类型声明任务（QQQ.2 §10.3 / DR-16）：
    ///   ① 一次性资源点被确认采集（isBeingGathered）→ Gather（destType=Treasury）
    ///   ② 生产建筑无工人在场且未满 → Production（destType=None）
    ///   ③ 有存储且存量 ≥ capacity×transportThreshold → Transport（destType=NearestWarehouse）
    ///   ④ 农场缺水（水网 Stored<waterThreshold）→ WaterHaul（destType=WaterNetwork）
    /// 军事/其他不在此扩。无条件返回 false。
    /// </summary>
    public bool TryAdvertiseTask(out KingdomTask task)
    {
        task = null;
        var producer = GetComponent<ProducerComponent>();
        var storage = GetComponent<StorageComponent>();
        var sched = TaskScheduler.Instance;

        // ① 采集：一次性资源点（isConsumable）被玩家确认采集 → Gather 任务（QQQ.2 T19 / DR-11）
        if (def != null && def.isConsumable && isBeingGathered)
        {
            task = new KingdomTask(KingdomTaskType.Gather, this);
            task.destType = KingdomDestType.Treasury;
            var ga = new GatherTaskArgs
            {
                resourceType = def.outputResource,
                amount = sched != null ? sched.gatherAmount : 5,
                gatherSeconds = def.gatherSeconds
            };
            task.args = ga;
            return true;
        }

        // ② 生产：无工人在场（Working）且存储未满 → 生产任务（水井除外：自动产水入网，不派生产任务）
        if (producer != null
            && !producer.IsWell
            && (storage == null || !storage.IsFull)
            && (sched == null || !sched.HasWorkerAssigned(this)))
        {
            task = new KingdomTask(KingdomTaskType.Production, this);
            task.destType = KingdomDestType.None;
            return true;
        }

        // ③ 搬运：存储达标且存量>0 → 搬运任务
        if (storage != null && storage.capacity > 0 && storage.storedAmount > 0
            && storage.storedAmount >= storage.capacity * transportThreshold)
        {
            task = new KingdomTask(KingdomTaskType.Transport, this);
            task.destType = KingdomDestType.NearestWarehouse;
            return true;
        }

        // ④ 挑水：仅农场（产粮耗水）在水网缺水时发挑水任务（伐木/采石/矿洞不耗水，不派）
        if (producer != null && producer.OutputResource == ResourceType.Food
            && WaterNetwork.Instance != null && WaterNetwork.Instance.Stored < waterThreshold)
        {
            task = new KingdomTask(KingdomTaskType.WaterHaul, this);
            task.destType = KingdomDestType.WaterNetwork;
            return true;
        }

        return false;
    }

    /// <summary>注册到调度器回调（Building 纳入任务派发）。</summary>
    public void OnRegister() { }

    /// <summary>从调度器注销回调。</summary>
    public void OnUnregister() { }

    /// <summary>建筑转 Active 时注册到任务调度器（IsValid 才注册）。</summary>
    private void RegisterWithTaskScheduler()
    {
        if (TaskScheduler.HasInstance && state == BuildingState.Active)
            TaskScheduler.Instance.Register(this);
    }
}
