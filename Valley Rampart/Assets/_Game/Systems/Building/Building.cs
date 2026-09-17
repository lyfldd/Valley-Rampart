using System.Collections.Generic;
using UnityEngine;
using static ResourceRespawnSystem;

/// <summary>建筑生命周期状态（3.3.4 批次3）。</summary>
public enum BuildingState
{
    Placing,        // 放置中（ghost）
    Constructing,   // 建造中（脚手架，不产出/不战斗）
    Active,         // 活跃（产出/战斗/可交互）
    Dead,           // 死亡（待销毁）
    Abandoned,      // 废弃（主城初始，可修复但不产出）
    Ruined          // 废墟（占格+阻挡 D154，修复=同建造 D156，先修复回原级才能升级 D157）
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
public class Building : MonoBehaviour, IInteractable, IDamageable, ISaveable, ITaskSource, IGridOccupant, MapGate.IAnchorConsumer
{
    // ===== 【HH.294 片4·4-E】MapGate.IAnchorConsumer（锚点消费记录，`03` §五／§7.8 对偶）=====

    /// <summary>我消费掉的锚点位置（null ＝ 未消费）。</summary>
    public GridCoord? ConsumedAnchorCoord
        => HasConsumedAnchor ? new GridCoord(anchorCoordX, anchorCoordY) : (GridCoord?)null;

    /// <summary>我消费掉的锚点地表类型。</summary>
    public FeatureType ConsumedAnchorFeature
        => anchorFeature >= 0 ? (FeatureType)anchorFeature : FeatureType.Plain;

    /// <summary>写入／清除锚点引用（返还时清 null ⇒ 各字段回 -1）。</summary>
    public void SetConsumedAnchor(GridCoord? coord, FeatureType feature)
    {
        if (coord.HasValue)
        {
            anchorCoordX = coord.Value.x;
            anchorCoordY = coord.Value.y;
            anchorFeature = (int)feature;
        }
        else
        {
            anchorCoordX = -1;
            anchorCoordY = -1;
            anchorFeature = -1;
        }
    }

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
    public GridCoord coord;                    // footprint 左上格（2D）
    public Vector2Int footprint = Vector2Int.one; // 占地 w×h（小区块，2_2）
    public bool isObstacle = false;

    /// <summary>建筑是否阻挡通行（对应 isObstacle，2_14 A⁻）。</summary>
    public bool IsGridObstacle => isObstacle;

    // ===== 【HH.294 片4·4-E】锚点消费记录（`03` §五：「锚点＝某个东西身上的一个字段」；
    //   建筑需记住「我消费了哪个锚点（类型 ＋ 位置）」——这属「属性自己装」）=====
    /// <summary>我消费掉的锚点位置（null ＝ 未消费任何锚点）。入档（BuildingSaveData）。</summary>
    [HideInInspector] public int anchorCoordX = -1;
    [HideInInspector] public int anchorCoordY = -1;
    /// <summary>我消费掉的锚点地表类型（(int)FeatureType；-1 ＝ 无）。入档。</summary>
    [HideInInspector] public int anchorFeature = -1;

    /// <summary>是否有未返还的锚点（供 `MapGate.ReturnAnchor` 与探针判读）。
    /// 判据用 `anchorFeature > 0`（可消费锚点恒为 Tree=1／Mine=4 ⇒ **0=Plain 与 -1 皆视为无**，
    /// 这样旧档缺字段（默认 0）不会误判为「有锚点」）。</summary>
    public bool HasConsumedAnchor => anchorCoordX >= 0 && anchorCoordY >= 0 && anchorFeature > 0;

    /// <summary>桥链 id（2_2 §3.5：1×N 桥段共享同一 bridgeId；运行时派生，不入档）。</summary>
    [HideInInspector] public string bridgeId;

    /// <summary>城门朝向（2_2 §3.4）：w>=h 为横门，反之为竖门。</summary>
    public GateOrientation GateOrientation
        => footprint.x >= footprint.y ? GateOrientation.Horizontal : GateOrientation.Vertical;

    /// <summary>
    /// 城门开关切换占用阻挡（2_2 §3.4，GateController 调）：
    /// 开门=不阻挡（isObstacle=false + 重标 footprint 清 BuildingBlocked），关门=阻挡。
    /// occupant 注册保持不变，只切 BuildingBlocked 位。
    /// </summary>
    public void SetGateBlocking(bool blocked)
    {
        isObstacle = blocked;
        if (GridSystem.Instance != null)
            GridSystem.Instance.MarkOccupiedFootprint(coord, Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y), this);
    }

    // ===== 来源 =====
    [Header("来源")]
    public BuildingType sourceType = BuildingType.None;
    public bool isPlayerBuilt = true;
    // ===== 2_16 步骤2：王国归属（D329 门面，默认 0=玩家；AI/动态王国由 Foundry 传入非 0 id）=====
    [Tooltip("王国归属 id（0=玩家；AI/动态王国=KingdomRegistry 分配的 id）。2_16 步骤2，随 BuildingSaveData 存档恢复")]
    public int kingdomId;

    // ===== 配置与运行时状态（3.3 主体）=====
    [Header("配置")]
    public BuildingDef def;
    public Faction faction = Faction.None;

    [Header("运行时状态")]
    public int level = 1;
    public int hp;
    public int maxHp;
    public ResourceGrade grade = ResourceGrade.Normal;
    /// <summary>
    /// 累计投入资源量（2_12 步骤7 / D155：修复成本 = 累计投入 × SO 比例）。
    /// 建造首付/升级费累加；拆除返还（D162）读它；修复按它算 D155。入档（BuildingSaveData）。
    /// 用单一近似总量（resourcepack 求和）而非四资源分账——返还/修复粗糙按比例缩放同一 pack。
    /// </summary>
    [Tooltip("累计投入（建造+升级累加）。D155 修复成本基数 / D162 拆除返还基数")]
    public int totalInvested;

    // ===== 3.5 P1-15 当前在册工人（3.5.3 §7.4 / 3.5.4 §8.5）=====
    // 本建筑当前服务的通用工人引用列表。建筑被摧毁时由 Die() 扫描 → 工人逃出存活。
    // 由 ScheduleCenter 派发工人时登记 / 工人离开时移除（P1 任务调度扩展接入）。
    [Tooltip("当前在册工人（建筑被摧毁时逃出存活）。ScheduleCenter 派工时登记")]
    public readonly List<UnitController> currentWorkers = new List<UnitController>();

    // ===== QQQ.2 T19：一次性资源点采集锁定 —— 【HH.294 片 6-2·6-D】随实体退役 **已删** =====
    //   改前：`isBeingGathered`（玩家点确认置位 → `TryAdvertiseTask` 采集分支 → `StartGather`/`OnGatherCompleted` 复位）。
    //   本批：资源点转纯数据（6-A）⇒ 一次性三型无 Building 实体 ⇒ 采集锁定并入
    //   `ResourceRespawnSystem.ConfirmResourceGather` ＋ `WorldGatherSource`（源有效直至完成·调度器独占去重）。
    //   grep 证据（改后）：`isBeingGathered` 全库 0 命中（改前消费点：`Building` 本文件 3 处 ＋
    //   `TaskScheduler.cs:556` ＋ `BuildingPanel.cs:482`，三处随 6-D 同删）。

    // ===== 状态机 + 进度系统（3.3.4 批次3）=====
    [Header("生命周期")]
    public BuildingState state = BuildingState.Active;
    [Range(0f, 1f)] public float constructProgress;
    /// <summary>建造/升级进度时长（秒）。SO 铁律：运行时读取 BuildConfig.constructionBaseSeconds + 协作缩放（EffectiveDuration）。此字段仅作编辑器参考/回退。</summary>
    [Tooltip("基础施工时长（秒）。运行时以 BuildConfig.constructionBaseSeconds 为准（2_12 步骤4 C+）")]
    public float constructDuration = 5f;

    private static BuildConfig _buildConfig;

    /// <summary>
    /// 实际施工时长（2_12 步骤4 / HH.9 裁决 C+）：读取 BuildConfig SO 基础值 × 协作缩放。
    /// 公式 = base / (1 + (n-1)×k)，n=该建筑实际被派工人数（CountAssignedWorkers，不虚增理想工人数），
    /// k=BuildConfig.cooperativeBuildK。k=0 或 n≤1 退化为纯计时基础时长。
    /// </summary>
    public float EffectiveDuration()
    {
        if (_buildConfig == null)
            _buildConfig = Resources.Load<BuildConfig>("Config/BuildConfig");
        float baseSeconds = _buildConfig != null ? Mathf.Max(0.01f, _buildConfig.constructionBaseSeconds) : Mathf.Max(0.01f, constructDuration);
        float duration;
        if (_buildConfig == null || _buildConfig.cooperativeBuildK <= 0f)
        {
            duration = baseSeconds;
        }
        else
        {
            int n = TaskScheduler.Instance != null ? TaskScheduler.Instance.CountAssignedWorkers(this) : 1;
            if (n <= 1) duration = baseSeconds;
            else
            {
                float divisor = 1f + (n - 1) * _buildConfig.cooperativeBuildK;
                duration = baseSeconds / Mathf.Max(0.01f, divisor);
            }
        }
        // 2_20 M5/D420：种族建造速度修正（buildSpeedMul：时长÷mul，mul>1=更快；D503 表值；
        // 2_12 建造链唯一时长出口——协作缩放后统一除）
        var raceDef = KingdomRace.GetKingdomRaceDef(kingdomId);
        float buildMul = raceDef != null ? raceDef.buildSpeedMul : 1f;
        if (buildMul > 0f) duration /= buildMul;
        return Mathf.Max(0.01f, duration);
    }

    private bool _pendingUpgrade;   // 当前 Constructing 是升级而非首次建造
    private bool _pendingRepair;    // 2_12 步骤7 / D156：当前 Constructing 是从废墟重建（完成满血回原级，不升级）
    private bool _territoryClaimed; // 批次C：首次建成已纳土（升级/重建不再重复纳土）
    private SpriteRenderer _renderer;
    private BuildProgressBar _progressBar;   // 2_12 步骤7B / D117：头顶施工进度条（Constructing/Ruined/Upgrading 态显示，惰性创建）

    /// <summary>关联的 UI 面板（运行时注入，可为 null）。</summary>
    private IUIPanel _panel;

    /// <summary>当前是否可被交互（Active/Abandoned/Ruined 可交互——Ruined 可点开面板重建，D156）。</summary>
    public bool IsInteractable => state == BuildingState.Active || state == BuildingState.Abandoned || state == BuildingState.Ruined;

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
        float cellSize = GridSystem.Instance.Config.cellSize.x;
        float crewRadius = def.crewRadiusCells * cellSize;
        var centerOpt = GridSystem.Instance.WorldToCoord(transform.position);
        if (!centerOpt.HasValue) return false; // doc1 改造：越界返回 null，无工人
        GridCoord center = centerOpt.Value;
        int cellRange = Mathf.Max(1, Mathf.CeilToInt(crewRadius / cellSize));
        int count = 0;
        for (int dy = -cellRange; dy <= cellRange; dy++)
        {
            for (int dx = -cellRange; dx <= cellRange; dx++)
            {
                var units = GridSystem.Instance.GetUnitsInCell(new GridCoord(center.x + dx, center.y + dy));
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
        float cellSize = GridSystem.Instance.Config.cellSize.x;
        float crewRadius = def.crewRadiusCells * cellSize;
        var centerOpt = GridSystem.Instance.WorldToCoord(transform.position);
        if (!centerOpt.HasValue) return 0; // doc1 改造：越界返回 null，无缺口
        GridCoord center = centerOpt.Value;
        int cellRange = Mathf.Max(1, Mathf.CeilToInt(crewRadius / cellSize));
        int count = 0;
        for (int dy = -cellRange; dy <= cellRange; dy++)
        {
            for (int dx = -cellRange; dx <= cellRange; dx++)
            {
                var units = GridSystem.Instance.GetUnitsInCell(new GridCoord(center.x + dx, center.y + dy));
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
        float cellSize = GridSystem.Instance.Config.cellSize.x;
        int range = Mathf.Max(1, Mathf.CeilToInt(rangeWorld / cellSize));
        var centerOpt = GridSystem.Instance.WorldToCoord(transform.position);
        if (!centerOpt.HasValue) return false; // doc1 改造：越界返回 null，视为无敌
        GridCoord center = centerOpt.Value;
        for (int dy = -range; dy <= range; dy++)
        {
            for (int dx = -range; dx <= range; dx++)
            {
                var units = GridSystem.Instance.GetUnitsInCell(new GridCoord(center.x + dx, center.y + dy));
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
        Init(def, coord, isPlayerBuilt,
             def != null ? new Vector2Int(Mathf.Max(1, def.footprint.x), Mathf.Max(1, def.footprint.y)) : Vector2Int.one);
    }

    /// <summary>2D 初始化（2_2）：footprintOverride 供城门旋转等运行时变体。</summary>
    public void Init(BuildingDef def, GridCoord coord, bool isPlayerBuilt, Vector2Int footprintOverride)
    {
        this.def = def;
        this.coord = coord;
        this.isPlayerBuilt = isPlayerBuilt;
        this.sourceType = BuildingType.None;
        this.grade = ResourceGrade.Normal;
        this.level = 1;
        this.footprint = footprintOverride.x > 0 && footprintOverride.y > 0 ? footprintOverride : Vector2Int.one;

        // 2_12 步骤7 / D155：玩家建造记录首付为累计投入（修复/拆除返还基数）。地图预置建筑 totalInvested=0。
        totalInvested = isPlayerBuilt && def != null
            ? def.cost.gold + def.cost.stone + def.cost.wood + def.cost.food
            : 0;

        ApplyDef();
        state = BuildingState.Active;
    }

    // 注：InitFromPlaceholder 已删除（改造计划 doc 1：BuildingPlaceholder 为 1D 概念，
    // 2D 地图预置建筑实例化由 BuildingFactory 按 NaturalBuilding 重写，归 2_2）。

    /// <summary>按 BuildingDef 应用属性（含 gradeScale 缩放）。public 供 BuildingFactory.SpawnFromSave 读档后按 grade 重算（QQQ.3 B8-5）。</summary>
    public void ApplyDef()
    {
        if (def == null) return;

        // HH.86/DZ-040 件2a：faction 按 kingdomId 派生（照抄 UnitFactory.SpawnUnit 既有先例：仅 >0 覆写 AiKingdom；
        // 玩家 0/自然 -1 保持 def.faction 原值=玩家与野外逐位不动）——旧=AI 建筑挂 PlayerCamp 全量断层
        //（塔不还击 AI 国/ catapult 不打本国敌人/玩家可交互开面板）。
        faction = kingdomId > 0 ? Faction.AiKingdom : def.faction;
        isObstacle = def.isObstacle;

        // HP：统一入口 = def.maxHp（3.5.1 E-S10）× gradeScale；防御建筑 combat.maxHp 与主层同值
        // 2_20 M5/D420：×种族建筑血量 buildingHpMul（归属国族；野生哨兵 -1 → null 中性；
        // 与 BuildingFactory 内联路径双路同乘——D420 每消费点唯一，本处+Factory 是同一消费点的两条初始化路）
        var raceDef = KingdomRace.GetKingdomRaceDef(kingdomId);
        float hpMul = raceDef != null ? raceDef.buildingHpMul : 1f;
        float scale;
        try { scale = def.GetGradeScale(grade); }
        catch { scale = 1f; }
        int baseHp = def.maxHp > 0 ? def.maxHp : 100;
        maxHp = Mathf.Max(1, Mathf.RoundToInt(baseHp * Mathf.Max(0.1f, scale) * hpMul));
        hp = maxHp;
    }

    /// <summary>
    /// 累计等级缩放系数（3.5.4 数据卡）：各已解锁 levels 档位 statScale 的连乘。
    /// Lv1=1；升到 Lv2 乘 levels[0].statScale；升到 Lv3 再乘 levels[1].statScale。
    /// 供 ProducerComponent.RefreshRate / StorageComponent.RefreshCapacity / HP 升级共用。
    /// </summary>
    public float LevelScale()
    {
        if (def == null || def.levels == null || def.levels.Length == 0) return 1f;
        float s = 1f;
        int n = Mathf.Min(level - 1, def.levels.Length);
        for (int i = 0; i < n; i++)
            s *= def.levels[i].statScale;
        return s;
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

    /// <summary>2_12 步骤7 / D156：从废墟开始重建（若在 Ruined 态）。同建造流程（仓库凑单→协作施工），完成时满血回原级。</summary>
    public void StartRebuildFromRuins()
    {
        if (state != BuildingState.Ruined) return;
        _pendingRepair = true;
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
        // 施工/废墟进度推进（仅 Constructing 态推进进度；暂停时 deltaTime=0 天然停）
        if (state == BuildingState.Constructing)
        {
            constructProgress += Time.deltaTime / Mathf.Max(0.01f, EffectiveDuration());
            if (constructProgress >= 1f)
            {
                constructProgress = 1f;
                OnConstructionComplete();
                return;   // 转 Active 后下方会隐藏进度条（state 已变）
            }
        }
        UpdateProgressBar();
    }

    /// <summary>
    /// 2_12 步骤7B / D117：驱动头顶施工进度条。
    /// Constructing/Upgrading→显示 constructProgress；Ruined→显示空"待重建"条；Active/其他→隐藏。
    /// 位置：footprint 中部上方（建筑头顶）。惰性创建 BuildProgressBar，尺寸=footprint 世界宽。
    /// </summary>
    private void UpdateProgressBar()
    {
        // Constructing/Upgrading（_pendingUpgrade 亦为 Constructing 态）显示 constructProgress；Ruined 显示空"待重建"条。
        bool constructingOrUpgrading = state == BuildingState.Constructing;   // Upgrading 复用 Constructing 态(_pendingUpgrade=true)
        bool show = constructingOrUpgrading || state == BuildingState.Ruined;
        float progress = constructingOrUpgrading ? constructProgress : 0f;

        if (!show)
        {
            if (_progressBar != null) _progressBar.SetProgress(0f, false);
            return;
        }

        if (_progressBar == null)
        {
            var go = new GameObject("BuildProgressBar");
            go.transform.SetParent(transform, false);
            _progressBar = go.AddComponent<BuildProgressBar>();
            _progressBar.building = this;
        }

        // 世界宽度 = footprint.x × cellSize；头顶 = footprint 顶 + 偏移
        float cellX = GridSystem.Instance != null && GridSystem.Instance.Config != null ? GridSystem.Instance.Config.cellSize.x : 2.26f;
        float cellY = GridSystem.Instance != null && GridSystem.Instance.Config != null ? GridSystem.Instance.Config.cellSize.y : 2.26f;
        float worldW = Mathf.Max(1f, footprint.x) * cellX;
        // 进度条挂在本物体 root 下，但父 localScale=footprint×cell（UpdateVisual）。需用 localScale=1/parent 抵消，使进度条以世界宽/高真实显示。
        float parentScale = Mathf.Max(0.0001f, transform.localScale.x);
        _progressBar.Init(worldW, barWorldHeight);
        float offY = (Mathf.Max(1f, footprint.y) * cellY) * 0.5f + 0.45f;
        // 抵消父缩放：父 scale = footprint.x*cellX（≈worldW），故进度条维持世界 1:1
        _progressBar.transform.localScale = new Vector3(1f / parentScale, 1f / parentScale, 1f);
        _progressBar.transform.localPosition = new Vector3(0f, offY, 0f);
        _progressBar.SetProgress(progress, true);
    }

    /// <summary>进度条世界高度（常量占位，2_10 美术可调）。</summary>
    private const float barWorldHeight = 0.18f;

    /// <summary>建造/升级/修复完成。升级则提级，修复则满血回原级，统一转 Active 并发激活事件。</summary>
    void OnConstructionComplete()
    {
        if (_pendingUpgrade && def != null && def.levels != null && level - 1 < def.levels.Length)
        {
            var lv = def.levels[level - 1];
            level++;
            maxHp = Mathf.RoundToInt(maxHp * lv.statScale);
            hp = maxHp;
            _pendingUpgrade = false;
            // 3.5.4：升级后刷新产能/容量（LevelScale 按新等级重算，升级效果真实生效）
            var prod = GetComponent<ProducerComponent>();
            if (prod != null) prod.RefreshRate();
            var storage = GetComponent<StorageComponent>();
            if (storage != null)
            {
                storage.RefreshCapacity();
                storage.storedAmount = Mathf.Min(storage.storedAmount, storage.capacity);
            }
            EventBus.Publish(new BuildingUpgradedEvent(this, level - 1, level));
        }
        else if (_pendingRepair)
        {
            // 2_12 步骤7 / D156/D159：废墟重建完成 → 满血回原级（不升级）。hp 已在此前置 0，恢复满。
            hp = maxHp;
            _pendingRepair = false;
            if (EventBus.HasSubscribers<BuildingRepairedEvent>())   // DZ-077：无订阅者不广播
                EventBus.Publish(new BuildingRepairedEvent(this));
        }
        state = BuildingState.Active;
        UpdateVisual();
        EventBus.Publish(new BuildingActivatedEvent(this));
        RegisterWithTaskScheduler();   // QQQ.2 T17：转 Active 注册任务源
        ClaimTerritoryIfFirstBuilt();  // 批C′：首次建成纳脚下格（升级/重建 `_territoryClaimed` 已真 → 跳过）
    }

    /// <summary>首次建成纳脚下格（2_17 步骤12 批C′，D327/HH.32 裁4）：建筑脚下中区块本身（无主纳入 / 有主食零变更）。</summary>
    void ClaimTerritoryIfFirstBuilt()
    {
        if (_territoryClaimed) return;   // 升级/重建/重复建成不重复纳土
        _territoryClaimed = true;
        if (TerritorySystem.Instance != null && kingdomId >= 0)
            TerritorySystem.Instance.ClaimFootprintChunk(kingdomId, coord);
    }

    /// <summary>按当前状态刷新视觉：Constructing 显示脚手架，其余显示正式图（真图优先）。占位 sprite 按 footprint w×h 缩放（2_2）。
    /// HH.239 T9/T11/T16：真图（SpriteRefTable 命中）自带 PPU100 像素尺度 + 底面中心 pivot ⇒ 不叠加 footprint 缩放（防拉伸失真）。</summary>
    void UpdateVisual()
    {
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        float cellW = GridSystem.Instance != null && GridSystem.Instance.Config != null ? GridSystem.Instance.Config.cellSize.x : 2.26f;
        float cellH = GridSystem.Instance != null && GridSystem.Instance.Config != null ? GridSystem.Instance.Config.cellSize.y : 2.26f;
        // 占位 sprite 是 1x1 世界单位，按 footprint w×h × cellSize 缩放到实际占地尺寸
        int w = Mathf.Max(1, footprint.x), h = Mathf.Max(1, footprint.y);
        Vector3 placeholderScale = new Vector3(w * cellW, h * cellH, 1);

        if (state == BuildingState.Constructing)
        {
            // 脚手架（按 footprint 三档选图；D642 1×1 兼覆城门）
            if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sprite = global::ValleyRampart.Rendering.PlaceholderSprites.Get(ScaffoldArtId(w, h));
            _renderer.sortingOrder = 1;
            _renderer.color = Color.white;
            transform.localScale = placeholderScale;
        }
        else if (state == BuildingState.Ruined)
        {
            // 2_12 步骤7 / D154：废墟占位（2_10 提供 ruinsTile 美术，此处用灰暗废墟占位）
            if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sprite = global::ValleyRampart.Rendering.PlaceholderSprites.Get("bld_ruins");
            _renderer.sortingOrder = 1;
            _renderer.color = new Color(0.45f, 0.42f, 0.4f, 1f);   // 灰暗废墟色调
            transform.localScale = placeholderScale;
        }
        else
        {
            // 正式视觉：artId → SpriteRefTable 真图 → 占位回退（T9）
            int raceId = kingdomId >= 0 ? KingdomRace.GetKingdomRace(kingdomId) : RaceIds.Human;
            BuildingVisual.ApplyPlaceholder(gameObject, sourceType, def != null ? def.role : BuildingRole.Special,
                def != null ? def.id : "", level, raceId, coord);
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.color = Color.white;
            bool realArt = global::ValleyRampart.Rendering.SpriteRefTable.Instance != null
                && global::ValleyRampart.Rendering.SpriteRefTable.Instance.Contains(_renderer.sprite);
            transform.localScale = realArt ? Vector3.one : placeholderScale;
            // Abandoned 态变暗提示废弃
            if (_renderer != null && state == BuildingState.Abandoned)
                _renderer.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        }
    }

    /// <summary>脚手架 artId（footprint 三档；3×3+ 归 3x3）。</summary>
    static string ScaffoldArtId(int w, int h)
    {
        int n = Mathf.Max(w, h);
        if (n >= 3) return "bld_scaffold_3x3";
        if (n == 2) return "bld_scaffold_2x2";
        return "bld_scaffold_1x1";
    }

    /// <summary>外部等级/族别变化后刷新视觉（HH.239 T11：主城按 (race, level) 取图）。</summary>
    public void RefreshVisual() => UpdateVisual();

    // ===== IInteractable =====

    public InteractionResult Interact(Interactor ctx)
    {
        // 非玩家阵营建筑不可交互（3.3.4 批次10 留口，首版直接拒绝敌方）
        if (faction != Faction.PlayerCamp && faction != Faction.None)
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
        // 2_12 步骤7 / D155：升级投入累加进累计投入（玩家已在外层扣款，此处记账更新 totalInvested）
        var uc = def.levels[level - 1].upgradeCost;
        totalInvested += uc.gold + uc.stone + uc.wood + uc.food + uc.metal;   // 2_12 步骤8 D131：含铁
        UpdateVisual();
        return true;
    }

    // ===== 拆除（按 HP 比例返还资源）=====

    /// <summary>
    /// ⭐【HH.294 片4·4-H】**「可否拆」＝数据栏判据**（`03` §7.5「本层不判'能不能删'⇒ 业务规则归**高级层／数据栏**」）。
    /// 改前该三字段组合**写在 UI 里现算**（`BuildingPanel.cs:169`）——判定归数据，UI 只读结果。
    /// 与 `Demolish()` 的守卫**同源**（同一表达式，禁两处各写一遍）。
    /// </summary>
    public bool CanDemolish => isPlayerBuilt && def != null && def.isDestructible && !def.isResourceNode;

    /// <summary>拆除建筑（由 BuildingPanel 调）。按累计投入×HP 比例返还（2_12 步骤7 / D162）。</summary>
    public void Demolish()
    {
        if (!CanDemolish) return;
        float ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
        // D162：返还 = 累计投入 × (当前HP/满HP)。累计投入按 def.cost 四资源占比摊回 pack。
        int invested = totalInvested > 0 ? totalInvested
            : (def.cost.gold + def.cost.stone + def.cost.wood + def.cost.food);
        int baseSum = Mathf.Max(1, def.cost.gold + def.cost.stone + def.cost.wood + def.cost.food);
        var refundPack = new ResourcePack
        {
            gold = Mathf.FloorToInt((float)invested * def.cost.gold / baseSum),
            stone = Mathf.FloorToInt((float)invested * def.cost.stone / baseSum),
            wood = Mathf.FloorToInt((float)invested * def.cost.wood / baseSum),
            food = Mathf.FloorToInt((float)invested * def.cost.food / baseSum)
        };
        RulerController.Instance?.Refund(refundPack, ratio);
        Die(DeathCause.Demolished);
    }

    // ===== ISaveable 实现（3.5 实施计划 P0 步骤3）=====

    public SavePayload SaveState()
    {
        var storage = GetComponent<StorageComponent>();
        // DZ-072a：矿洞副产组件双仓存量（无组件=零值元组）；T1.4（D609）：第三元=矿石
        var mineByprod = GetComponent<MineByproductComponent>();
        var mineByprodSaved = mineByprod != null ? mineByprod.SaveByproductState() : (crystal: 0, fireOil: 0, ore: 0);
        var data = new BuildingSaveData
        {
            defId = def != null ? def.id : "",
            coordX = coord.x,
            coordY = coord.y,
            footprintW = Mathf.Max(1, footprint.x),
            footprintH = Mathf.Max(1, footprint.y),
            level = level,
            hp = hp,
            maxHp = maxHp,
            faction = (int)faction,
            state = (int)state,
            sourceType = (int)sourceType,
            storedAmount = storage != null ? storage.storedAmount : 0,
            // DZ-072a：矿洞副产组件双仓存量入档（旧档缺字段→默认 0 零 bump）
            byproductCrystalAmount = mineByprodSaved.crystal,
            byproductFireOilAmount = mineByprodSaved.fireOil,
            // T1.4（D609/D617）：矿石伴生子仓存量入档（尾插，旧档缺→0 零 bump）
            byproductOreAmount = mineByprodSaved.ore,
            grade = (int)grade,   // QQQ.3 B8-5 / LC-B2：grade 入档
            totalInvested = totalInvested,  // 2_12 步骤7 / D155：累计投入入档
            kingdomId = kingdomId,  // 2_16 步骤8：王国归属入档（读档恢复 AI/玩家归属）
            // 【HH.294 片4·4-E】锚点消费记录入档（`03` §7.8：拆除时须能还回原锚点）
            anchorCoordX = anchorCoordX,
            anchorCoordY = anchorCoordY,
            anchorFeature = anchorFeature
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

        // 2_16 步骤8：读档恢复王国归属。自然建筑（一次性资源点 sourceType=Ore/Wood/Stone）读档侧一律强制 -1
        //（哨兵配套，旧档缺 kingdomId 默认 0，若不强制则自然建筑全变"玩家王国"，污染传送门排除集）——与 SpawnFromSave 同规则幂等。
        if (data.sourceType == (int)BuildingType.OreVein
            || data.sourceType == (int)BuildingType.WoodPile
            || data.sourceType == (int)BuildingType.StonePile)
            kingdomId = -1;
        else
            kingdomId = data.kingdomId;

        level = Mathf.Max(1, data.level);

        // 2D 占地恢复（2_2）：旧档缺字段 -> 兜底 def.footprint
        int fw = data.footprintW > 0 ? data.footprintW : (def != null && def.footprint.x > 0 ? def.footprint.x : 1);
        int fh = data.footprintH > 0 ? data.footprintH : (def != null && def.footprint.y > 0 ? def.footprint.y : 1);
        footprint = new Vector2Int(Mathf.Max(1, fw), Mathf.Max(1, fh));
        coord = new GridCoord(data.coordX, data.coordY);

        // QQQ.3 B8-5 / LC-B2：grade 恢复 + 重算属性（修复读档后产能永久降贫瘠档 rate×0.7）。
        // 先设 grade 再 ApplyDef ⇒ maxHp 按新等级重算；随后恢复保存的 hp（clamp 到新 maxHp，不因 ApplyDef 重置满血）。
        grade = (ResourceGrade)data.grade;
        ApplyDef();
        maxHp = Mathf.Max(1, data.maxHp);
        hp = Mathf.Clamp(data.hp, 0, maxHp);

        // 2_12 步骤7 / D155：累计投入恢复。旧档缺字段(data.totalInvested=0，且玩家建筑无默认) → 兜底按 def.cost 计
        totalInvested = data.totalInvested > 0
            ? data.totalInvested
            : (isPlayerBuilt && def != null ? def.cost.gold + def.cost.stone + def.cost.wood + def.cost.food : 0);

        // 【HH.294 片4·4-E】锚点消费记录恢复（旧档缺字段默认 0 ⇒ 与坐标 0 混淆，故按「有锚点类型」判有效）
        anchorCoordX = data.anchorFeature >= 0 ? data.anchorCoordX : -1;
        anchorCoordY = data.anchorFeature >= 0 ? data.anchorCoordY : -1;
        anchorFeature = data.anchorFeature >= 0 ? data.anchorFeature : -1;

        var storage = GetComponent<StorageComponent>();
        if (storage != null) storage.storedAmount = Mathf.Max(0, data.storedAmount);

        // DZ-072a：矿洞副产组件双仓存量恢复（旧档缺字段=0，零恢复=新产链起点）；T1.4（D609）：第三参=矿石
        var mineByprod = GetComponent<MineByproductComponent>();
        if (mineByprod != null) mineByprod.RestoreByproductState(data.byproductCrystalAmount, data.byproductFireOilAmount, data.byproductOreAmount);

        // 网格占用恢复（Spawning 已占用，此处兜底幂等；2_2：footprint w×h）
        if (GridSystem.Instance != null)
        {
            GridSystem.Instance.MarkOccupiedFootprint(coord, Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y), this);
            // 桥面位恢复（2_2 §3.5：桥段占用水格，Bridge 位豁免 Water 阻挡）
            if (def != null && def.isBridge)
                GridSystem.Instance.SetBridge(coord, Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y), true);
        }
    }

    /// <summary>
    /// 2_12 步骤7 / D155：修复/废墟重建成本（库存储入时点调用，勿入每帧路径）。
    /// = 累计投入 × RepairConfig.repairCostRatio，按 def.cost 的 金/石/木/粮/铁 比例分摊回 ResourcePack。
    /// 累计投入用 totalInvested（建造+升级累加）；旧档/地图预置无累计投入 → 回退 def.cost。
    /// 2_12 步骤8 D131：def.cost/分摊含铁，修复摊回不静默丢铁。
    /// </summary>
    public ResourcePack GetRepairCost()
    {
        int invested = totalInvested > 0 ? totalInvested
            : (def != null ? def.cost.gold + def.cost.stone + def.cost.wood + def.cost.food + def.cost.metal : 0);
        if (invested <= 0 || def == null) return ResourcePack.Zero;

        float ratio = RepairConfig.Instance != null ? Mathf.Clamp01(RepairConfig.Instance.repairCostRatio) : 0.5f;
        int total = Mathf.Max(1, Mathf.RoundToInt(invested * ratio));

        // 按 def.cost 五资源占比分摊（避免纯按总额使单一资源爆表）
        int baseSum = Mathf.Max(1, def.cost.gold + def.cost.stone + def.cost.wood + def.cost.food + def.cost.metal);
        return new ResourcePack
        {
            gold = Mathf.RoundToInt((float)total * def.cost.gold / baseSum),
            stone = Mathf.RoundToInt((float)total * def.cost.stone / baseSum),
            wood = Mathf.RoundToInt((float)total * def.cost.wood / baseSum),
            food = Mathf.RoundToInt((float)total * def.cost.food / baseSum),
            metal = Mathf.RoundToInt((float)total * def.cost.metal / baseSum)
        };
    }

    // ===== 战斗（3.4 实现 IDamageable）=====

    /// <summary>是否工事（2_12 步骤7 / D165）：城墙/城门/桥/防御塔被破直接销毁，不进废墟。主城(Special)例外进废墟可修。</summary>
    public bool IsFortification
        => def != null && (def.role == BuildingRole.Wall || def.isGate || def.isBridge
                           || (def.role == BuildingRole.Defense && sourceType != BuildingType.CastleCore));

    /// <summary>
    /// 受到伤害，只扣血。伤害已由 DamageSystem 算好+取整。
    /// 血量≤0：工事(D165)直接销毁；非工事(含主城 D163)进废墟(Ruined)可修复，不直接判负。
    /// 非 Active 态不受伤。
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (state != BuildingState.Active) return; // 非 Active 不受伤
        hp = Mathf.Max(0, hp - amount);
        if (hp <= 0)
        {
            if (IsFortification)
                Die(DeathCause.Killed);   // 工事被破直接销毁（D165，无废墟）
            else
                EnterRuined();            // 策略建筑/主城被破 → 废墟可修（D154/D163）
        }
    }

    /// <summary>
    /// 2_12 步骤7 / D154：建筑被击破 → 进入废墟态。保持 footprint 占用 + 阻挡（不 Free），
    /// 停止生产/不去 Active，等待修复（D156 同建造）。废墟不判负（D249：主城被破可修）。
    /// </summary>
    public void EnterRuined()
    {
        state = BuildingState.Ruined;

        // 在册工人撤出（废墟不供职；工人在内可能被打/被卡，撤出存活）
        EscapeWorkers();
        // 训练中断回退（若为训练建筑）
        if (TrainingSystem.Instance != null)
            TrainingSystem.Instance.OnBuildingDestroyed(this);
        // 从任务调度器注销（不再是 Active 源）；保持 GridSystem 占格 → D154 阻挡持续
        if (TaskScheduler.HasInstance) TaskScheduler.Instance.Unregister(this);

        UpdateVisual();
        if (EventBus.HasSubscribers<BuildingRuinedEvent>())   // DZ-077：无订阅者不广播
            EventBus.Publish(new BuildingRuinedEvent(this));
    }

    /// <summary>
    /// ⭐【HH.294 片4·4-D】**本层注销 · 唯一实现**（`03` §7.2「本层删的**不是"那个东西"，是本层赋予它的那几样**」）。
    /// 改前 `Die()` 与 `OnGatherCompleted()` **各写一遍同样的 4~5 项注销**（抄两遍）——此处合一。
    /// 顺序照 `03` §7.3 删门七步：**锚点返还（第 2 步）→ 释放占格（第 4 步）→ 注销（第 5 步）**。
    /// `footprintUnregister` ＝ 是否连注册表一起注销（采集路径自行处理，故由此开关区分）。
    /// </summary>
    private void ReleaseLayerOwnedState(bool unregisterFromRegistry)
    {
        // 第 2 步 · 锚点返还（§7.8 通用，非仅矿山）：当初消费掉的锚点写回原位 ⇒ 可再被引用
        if (HasConsumedAnchor)
            MapGate.ReturnAnchor(this);

        // 第 4 步 · 释放占格（2_2：footprint w×h 全块）
        if (GridSystem.Instance != null)
        {
            GridSystem.Instance.FreeFootprint(coord, Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
            if (def != null && def.isBridge)
                GridSystem.Instance.SetBridge(coord, Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y), false);
        }

        // 第 5 步 · 注销（本层索引）：注册表／任务调度器／存档
        if (unregisterFromRegistry) BuildingRegistry.Instance?.Unregister(this);
        // QQQ.2 T17：从任务调度器注销（清指向本建筑的在派任务）
        if (TaskScheduler.HasInstance) TaskScheduler.Instance.Unregister(this);
        // QQQ.3 B8-9 / LC-B8：显式注销 Saveable（否则 _saveables 留残留条目，本应主动清理而非等兜底）
        if (SaveManager.Instance != null) SaveManager.Instance.UnregisterSaveable(this);
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

        // 【HH.294 片4·4-D】本层注销（锚点返还 ＋ 释放占格 ＋ 注销）＝ 唯一实现
        ReleaseLayerOwnedState(unregisterFromRegistry: true);

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

    // ===== 【HH.294 片 6-2·6-D】`OnGatherCompleted()` / `FeatureOf()` / `StartGather()` **随实体退役 已删** =====
    //   改前职责链（一次性资源点）：`BuildingPanel` 采集按钮 → `StartGather`（锁 `isBeingGathered`）
    //     → `TryAdvertiseTask` 采集分支 → 调度器派工 → 完成回调 `OnGatherCompleted`
    //     （＝ 释放占格＋注销注册表＋守卫失去＋`HandleEntityDepleted`＋对象池回收）。
    //   改后（数据寻址）：玩家 = `PrioritizeHarvestCommand` → `ResourceRespawnSystem.ConfirmResourceGather`
    //     → `WorldGatherSource/TreeGatherSource` → 完成 = `ResourceRespawnSystem.HandleCellGathered`
    //     （格翻 Plain＋守卫失去[门内 `MapGate:308`]＋游荡锚点登记＋池子减 1 点）。
    //   语义承接（`D778` B-4 清单）：④ 游荡锚点登记 → `HandleCellGathered`（一次性三型·树不新增）；
    //     ⑤ 对象池回收 → 随实体退役（`BuildingFactory.ReturnBuildingToPool` 同删）；
    //     守卫失去通知 → 只留门内 `MapGate.cs:308`。
    //   grep 证据（改后）：`OnGatherCompleted|StartGather|isBeingGathered` 全库 0 命中。

    /// <summary>
    /// 3.5 P1-15 工人逃出（3.5.3 §7.4 / 3.5.4 §8.5）：建筑被摧毁时当前在册工人存活。
    /// 每个工人：① 清除任务状态（变 Idle）② 位置环形偏移逃离（避免卡在废墟格）③ 不死亡。
    /// 逃出后可被 ScheduleCenter 重新派发任务。
    /// </summary>
    private void EscapeWorkers()
    {
        if (currentWorkers == null || currentWorkers.Count == 0) return;
        var cfg = GridSystem.Instance != null ? GridSystem.Instance.Config : null;
        float cellW = cfg != null ? cfg.cellSize.x : 2.26f;
        float cellH = cfg != null ? cfg.cellSize.y : 2.26f;
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

            // ② 位置逃离偏移（2_2：2D 环形分布，避免卡废墟格/重叠）
            int ring = (idx / 8) + 1;                 // 第 1 圈 8 向，逐圈外扩
            int dirIdx = idx % 8;
            int ex = ring * (new int[] { 1, 1, 0, -1, -1, -1, 0, 1 }[dirIdx]);
            int ey = ring * (new int[] { 0, 1, 1, 1, 0, -1, -1, -1 }[dirIdx]);
            Vector2 escape = (Vector2)transform.position + new Vector2(ex * cellW, ey * cellH);
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
    ///   ① 生产建筑无工人在场且未满 → Production（destType=None）
    ///   ② 有存储且存量 ≥ capacity×transportThreshold → Transport（destType=NearestWarehouse）
    ///   ③ 农场缺水（水网 Stored<waterThreshold）→ WaterHaul（destType=WaterNetwork）
    /// 军事/其他不在此扩。无条件返回 false。
    /// 【HH.294 片 6-2·6-D】原「①一次性资源点被确认采集 → Gather」分支**随实体退役已删**
    ///   —— 采集任务改由 `WorldGatherSource`／`TreeGatherSource` 广告（数据寻址）。
    /// </summary>
    public bool TryAdvertiseTask(out KingdomTask task)
    {
        task = null;
        // 2_17 修复卡β：删除补丁D广告守卫(L900)。AI 王国建筑(kingdomId>0)照常发布任务；
        // 防"AI 任务流向玩家 worker"的补丁D意图已由 TaskScheduler.Tick 池隔离路由结构性达成——
        // 任务源归属国 tKingdom 只派给同国 idleKingdom 工人，广告侧守卫成永久双轨，此处清理收编。
        // 无主自然建筑(-1)仍按原流程发布（采集），路由时降级先到先得池（见 TaskScheduler）。
        var producer = GetComponent<ProducerComponent>();
        var storage = GetComponent<StorageComponent>();
        var sched = TaskScheduler.Instance;

        // ① 采集：一次性资源点（isConsumable）被玩家确认采集 → Gather 任务 —— 【片 6-2·6-D】已删（见方法头注）

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

        // ③ 搬运：存储达标且存量>0 → 搬运任务（2_8 步骤3 / D95：把资源总需求附带进 task.args，调度器据此规模派工）
        if (storage != null && storage.capacity > 0 && storage.storedAmount > 0
            && storage.storedAmount >= storage.capacity * transportThreshold)
        {
            task = new KingdomTask(KingdomTaskType.Transport, this);
            task.destType = KingdomDestType.NearestWarehouse;
            task.args = new ScaleTaskArgs
            {
                resourceType = storage.resourceType,
                totalResourceDemand = storage.storedAmount
            };
            return true;
        }

        // ④ 挑水：仅农场（产粮耗水）在本国水网缺水时发挑水任务（采石/矿洞不耗水，不派）
        // HH.86/DZ-044 件2c：旧读全图玩家桶（Stored=0 桶）——AI 农田在玩家桶满时永不发挑水=AI 农田断水；
        // 改读本国桶 GetStored(kingdomId)（GetStored(0)==Stored 旧语义等价，玩家逐位不动）。
        if (producer != null && producer.OutputResource == ResourceType.Food
            && WaterNetwork.Instance != null && WaterNetwork.Instance.GetStored(kingdomId) < waterThreshold)
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
        // 2_17 修复卡β：删除补丁D注册侧守卫(L968)。AI 王国建筑(kingdomId>0)也登记为任务源——
        // 补丁D"AI 任务不流向玩家"意图由路由层结构性达成，注册侧守卫与广告侧 L900 同为待清的永久双轨。
        // 玩家(0)/自然(-1)/AI(>0) 一律注册，派工归属由 TaskScheduler.Tick 池隔离路由决定。
        if (TaskScheduler.HasInstance && state == BuildingState.Active)
            TaskScheduler.Instance.Register(this);
    }
}
