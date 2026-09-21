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
    /// 累计投入资源量（**件数标量** · 2_12 步骤7 / D155 遗留）。入档（`BuildingSaveData`）。
    /// ⚠️ **`M1-C` · U-1 修复后：备而未用（仅入档 `SaveState` ＋ 日志）** —— 曾作「修复成本基数（D155）／
    ///   拆除返还基数（D162）」，现两处均改由 <see cref="PaidStageCost"/>（**逐类型精确累加**）派生 ⇒
    ///   本字段**零读点**（处置已裁：**(a) 保留**，⛔ 不动 `BuildingSaveData` 格式）。
    /// ⚠️ **根因留档（U-1）**：本字段自 `D162` 起就是「**单一近似总量**（`resourcepack` 求和）**而非分资源账**」，
    ///   旧口径再拿它当分子、拿 `def.cost`（**仅基础造价**）当分母去「按占比摊同一 pack」⇒ 一旦有升级投入
    ///   （分子含升级、分母不含）即**把升级资源折成基础资源类型**（资源张冠李戴）。⛔ **勿再据此做退还/修复算式**。
    /// </summary>
    [Tooltip("累计投入件数（D155/D162 遗留）。M1-C·U-1 后备而未用（仅入档/日志），⛔ 勿再作退还/修复基数")]
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

    // ===== ⭐ `M1-C` 件1／件4：投料态 ＋ 拆除态 =====

    /// <summary>投料未齐（`09` §16.1 ①~③）：Constructing 态但**进度不推进**，等工人把料搬进工地仓。</summary>
    private bool _awaitingMaterials;

    /// <summary>本次投料需求（配方量；金已在下单时直扣 ⇒ ⛔ 不含金 · 裁决 2 金-A）。入档。</summary>
    private ResourceList _siteNeed = ResourceList.Empty;

    /// <summary>工地仓（裁决 1 案 A：独立子物体容器 · ⛔ 不复用 `StorageComponent`）。</summary>
    private ConstructionSiteStore _siteStore;

    /// <summary>工地仓是否已注册进 `TaskScheduler`（防重复 Register/Unregister 抖动）。</summary>
    private bool _siteRegistered;

    /// <summary>拆除中（`09` §16.3-3：拆除要耗时与工人 ⇒ 与建造对称）。</summary>
    private bool _demolishing;

    /// <summary>拆除进度 0→1（复用 `constructProgress` 的推进形态 · 裁决自陈-5）。</summary>
    private float _demolishProgress;

    /// <summary>是否处于「投料未齐」态（工地仓据此判 `IsValid`）。</summary>
    public bool IsSiteAwaitingMaterials => _awaitingMaterials;

    /// <summary>是否正在拆除（有进度 · 非瞬时）。</summary>
    public bool IsDemolishing => _demolishing;

    /// <summary>拆除进度（0→1；供探针读）。</summary>
    public float DemolishProgress => _demolishProgress;

    /// <summary>工地仓（未投料/已料齐 ⇒ null）。</summary>
    public ConstructionSiteStore SiteStore => _siteStore;

    /// <summary>本次投料需求（含已到料前的配方量；供探针/存档读）。</summary>
    public ResourceList SiteNeed => _siteNeed;

    /// <summary>累计投入累加口（⭐ 裁决自陈-3：金在下单时记 ＋ 料在入工地仓时记）。</summary>
    public void AddInvested(int amount)
    {
        if (amount > 0) totalInvested += amount;
    }

    /// <summary>投料需求 ＝ 造价**去金**（裁决 2 金-A：金仍「下单即扣」⇒ ⛔ 不进工地仓）。
    /// 纯金造价（`farm`／`quarry`／`market`）⇒ 空列表 ⇒ 无料可搬 ⇒ **即时开工**（裁决已认可）。
    /// </summary>
    public static ResourceList SiteNeedOf(ResourceList cost)
    {
        if (cost.items == null || cost.items.Length == 0) return ResourceList.Empty;
        var r = ResourceList.Empty;
        for (int i = 0; i < cost.items.Length; i++)
        {
            var e = cost.items[i];
            if (e.type == ResourceType.Gold) continue;   // 金-A：金不入工地仓
            if (e.amount > 0) r = r.Set(e.type, e.amount);
        }
        return r;
    }

    /// <summary>造价里的**金**部分（裁决 2 金-A：金仍「下单即扣」）。
    /// 与 <see cref="SiteNeedOf"/> **互补**（去金／取金单源，⛔ 不各写一遍）。</summary>
    public static ResourceList GoldOnlyOf(ResourceList cost)
    {
        if (cost.items == null || cost.items.Length == 0) return ResourceList.Empty;
        var r = ResourceList.Empty;
        for (int i = 0; i < cost.items.Length; i++)
        {
            var e = cost.items[i];
            if (e.type != ResourceType.Gold) continue;
            if (e.amount > 0) r = r.Set(ResourceType.Gold, e.amount);
        }
        return r;
    }
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

        // 2_12 步骤7 / D155：累计投入件数（⚠️ `M1-C` · U-1 后**备而未用** · 见字段注释）。
        // ⭐ `M1-C` 件1（裁决自陈-3）：投料口径下「投入」＝**实际投进去的量**
        //   （金在下单时记 ＋ 料在入工地仓时记）⇒ 此处一律**从 0 起算**，
        //   ⛔ 不再按下单时的 `def.cost` 预记（否则拆除全退会退「从未搬进去的料」）。
        //   地图预置建筑（`isPlayerBuilt=false`）恒 0（既有语义不变）。
        totalInvested = 0;

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

    /// <summary>开始建造/修复（进入 Constructing 态，显示脚手架）。⛔ 无投料（直建/探针路径）。</summary>
    public void StartConstructing() => StartConstructing(ResourceList.Empty);

    /// <summary>
    /// ⭐ `M1-C` 件1（`09` §16.1 ①~③）：**投料 ⇒ 等时间**。
    /// `need` 非空 ⇒ 进入 Constructing 态但**进度不推进**，等工人把料搬进工地仓；
    /// 料齐 ⇒ `OnSiteMaterialsReady()` 清标志 ⇒ 进度才开始推进。
    /// `need` 为空（或去金后为空 · 纯金造价）⇒ 无料可搬 ⇒ **即时开工**（裁决 2 已认可）。
    /// </summary>
    public void StartConstructing(ResourceList need)
    {
        _pendingUpgrade = false;
        _pendingRepair = false;
        state = BuildingState.Constructing;
        constructProgress = 0f;
        BeginMaterialPhase(need);
        UpdateVisual();
    }

    /// <summary>2_12 步骤7 / D156：从废墟开始重建（若在 Ruined 态）。同建造流程（仓库凑单→协作施工），完成时满血回原级。</summary>
    public void StartRebuildFromRuins() => StartRebuildFromRuins(ResourceList.Empty);

    /// <summary>⭐ `M1-C` 件1（`09` §16.3-2：修复与建造**同构**）：废墟重建走「投料 ⇒ 等时间」。</summary>
    public void StartRebuildFromRuins(ResourceList need)
    {
        if (state != BuildingState.Ruined) return;
        _pendingRepair = true;
        _pendingUpgrade = false;
        state = BuildingState.Constructing;
        constructProgress = 0f;
        BeginMaterialPhase(need);
        UpdateVisual();
    }

    // ===== ⭐ `M1-C` 件1：投料态管理（工地仓 创建／注册／料齐收口）=====

    /// <summary>进入投料阶段：去金 ⇒ 建/复用工地仓 ⇒ 料齐则即时开工，否则注册任务源等工人搬料。</summary>
    private void BeginMaterialPhase(ResourceList need)
    {
        _siteNeed = SiteNeedOf(need);
        if (_siteNeed.IsZero)
        {
            // 无料可搬（纯金造价 / 零造价 / 直建）⇒ 不建工地仓、不挂任务 ⇒ 进度立即推进
            _awaitingMaterials = false;
            UnregisterSiteStore();
            return;
        }
        var store = EnsureSiteStore();
        store.SetNeed(_siteNeed);
        _awaitingMaterials = !store.IsSatisfied;
        if (_awaitingMaterials) RegisterSiteStore();
        else OnSiteMaterialsReady();
    }

    /// <summary>惰性创建工地仓（子物体容器 · 照 `TreasureVault` 先例；⛔ 不挂 `StorageComponent`）。</summary>
    private ConstructionSiteStore EnsureSiteStore()
    {
        if (_siteStore != null) return _siteStore;
        var go = new GameObject("SiteStore");
        go.transform.SetParent(transform, false);
        _siteStore = go.AddComponent<ConstructionSiteStore>();
        _siteStore.Init(this, _siteNeed);
        return _siteStore;
    }

    private void RegisterSiteStore()
    {
        if (_siteStore == null || _siteRegistered) return;
        if (TaskScheduler.HasInstance) { TaskScheduler.Instance.Register(_siteStore); _siteRegistered = true; }
    }

    private void UnregisterSiteStore()
    {
        if (_siteStore == null || !_siteRegistered) return;
        if (TaskScheduler.HasInstance) TaskScheduler.Instance.Unregister(_siteStore);
        _siteRegistered = false;
    }

    /// <summary>
    /// ⭐ 料齐（`09` §16.1 ③）：注销搬料源 ＋ 材料**转成「建筑本体」**（`09` §16.1-3：
    /// 不可搬出 · ⛔ 不占产出容量 · 只在生命周期结束时掉出）⇒ 工地仓清空，投入量已由
    /// `ConstructionSiteStore.Deposit → AddInvested` 逐笔记账（自陈-3）。
    /// </summary>
    public void OnSiteMaterialsReady()
    {
        // ⭐ `U-8` 件3 硬化（D800 `Q3`）：正在拆 ⇒ ⛔ 不走完工收口 —— 封「已在途恰好到货 ⇒ `IsSatisfied`
        //   ⇒ `Clear()` 清空已到料」窗口（唯一触发点 ＝ `ConstructionSiteStore.Deposit:141`）。
        //   ⚠️ 内容物此后仍随 `FinishDemolish → DropSiteStoreToChest` 掉箱（⛔ 不丢）⇒ 与件7「留仓」口径一致。
        if (_demolishing) return;
        if (!_awaitingMaterials) return;
        UnregisterSiteStore();
        _siteStore?.Clear();
        _awaitingMaterials = false;
        Debug.Log($"[Building] {def?.id} 料齐 ⇒ 开工（投入={totalInvested}·进度开始推进）");
    }

    private void Start()
    {
        // 地图预置建筑初始化视觉（含 Abandoned 暗化 + 占位缩放）
        UpdateVisual();
    }

    private void Update()
    {
        // ⭐ `M1-C` 件4（`09` §16.3-3）：拆除**有耗时与工人**（与建造对称）——
        //   仅在**有工人到场**时推进（`09` §16.3.1「工人侧按进度推进」）；到 1 ⇒ 真拆（掉箱 ＋ 生命周期结束）。
        //   ⚠️ 拆除分支**先于**建造分支：拆除中的建筑可能仍处 `Constructing`（拆一个投料中的工地）。
        //   ⚠️ `U-8` 件10（`E2`）加注：本门控实为「**有人接单**」—— `HasAssignedWorker` 用源级
        //     `CountAssignedWorkers`（`TaskScheduler.cs:152-159`，⛔ 无 state 过滤）⇒ 含 `Assigned`／
        //     `MovingToSource` 期（工人可能还在路上）＋ 拆除前已在派的残留任务，⛔ **非**「工人已到场 Working」。
        //   ⚠️ `U-8` 件10（`O7`）：拆除标称 `DemolishDuration()`=6s（`BuildConfig.demolishBaseSeconds`），
        //     实测因「途中推进 ＋ 每轮 ≥1 tick 空档 ＋ 需 ≥2 轮派工」与标称不等（读数见交付报告判据 7）。
        if (_demolishing)
        {
            if (HasAssignedWorker())
            {
                _demolishProgress += Time.deltaTime / Mathf.Max(0.01f, DemolishDuration());
                if (_demolishProgress >= 1f)
                {
                    _demolishProgress = 1f;
                    FinishDemolish();
                    return;
                }
            }
        }
        // 施工/废墟进度推进（仅 Constructing 态推进进度；暂停时 deltaTime=0 天然停）
        else if (state == BuildingState.Constructing)
        {
            // ⭐ `M1-C` 件1：**投料未齐 ⇒ 进度不推进**（等工人把料搬进工地仓 · `09` §16.1 ②③）
            if (!_awaitingMaterials)
            {
                constructProgress += Time.deltaTime / Mathf.Max(0.01f, EffectiveDuration());
                if (constructProgress >= 1f)
                {
                    constructProgress = 1f;
                    OnConstructionComplete();
                    return;   // 转 Active 后下方会隐藏进度条（state 已变）
                }
            }
        }
        UpdateProgressBar();
    }

    /// <summary>是否已有工人到场（拆除进度门控 · `09` §16.3.1）。
    /// ⚠️ `U-8` 件10（`E2`）加注：本门控用**源级** `CountAssignedWorkers`（`TaskScheduler.cs:152-159`，
    ///   ⛔ 无 state／args 过滤）⇒ 语义实为「**有人接单**」（含 `Assigned`／`MovingToSource` 期 —— 工人可能还在
    ///   路上）＋ 拆除前已在派的 `Production`／`Transport` 残留任务也计入，⛔ **非**「工人已到场 `Working`」。
    ///   ⛔ 本批不改门控语义（`D800` `Q5` 只裁 `DemolishDuration` 的 `n` 过滤）；精化（改 `Working`／按 args 过滤）＝另报裁。</summary>
    private bool HasAssignedWorker()
        => TaskScheduler.HasInstance && TaskScheduler.Instance.CountAssignedWorkers(this) > 0;

    /// <summary>
    /// 拆除时长（`so-data-driven`：进 SO `BuildConfig.demolishBaseSeconds`；与建造对称用同一协作系数 k）。
    /// ⛔ 不硬编码魔法数值。
    /// ⭐ `U-8` 件5（`D800` `Q5` · **形态甲**）：`n` **按 `DemolishTaskArgs` 过滤**（只计拆除任务）——
    ///   防「拆除前已在派的 `Production`／`Transport` 残留任务」被源级计数误算成拆除协作工人（虚增 n ⇒ 缩短时长）。
    ///   ⚠️ 本片维持「拆除＝单人」（派工对非 `Transport` 任务 `slots=1` ＋ 按 `(source,type)` 去重 ⇒ 正常路径
    ///   `n≤1`）⇒ ⭐ **`k` 协作分支当前恒不生效**（显式保留 ＋ 加注：`k` 与建造侧共用
    ///   `BuildConfig.cooperativeBuildK`，⛔ 不删）。多工人协作 ＝ 另案（观察项 `O6`）。
    /// ⚠️ `U-8` 件10（`O7`）：标称 6s，实测受「门控含派工/走动期 ⇒ 途中推进」等影响（读数见交付报告判据 7）。
    /// </summary>
    public float DemolishDuration()
    {
        if (_buildConfig == null)
            _buildConfig = Resources.Load<BuildConfig>("Config/BuildConfig");
        float baseSeconds = _buildConfig != null ? Mathf.Max(0.01f, _buildConfig.demolishBaseSeconds) : 6f;
        if (_buildConfig == null || _buildConfig.cooperativeBuildK <= 0f) return baseSeconds;
        int n = TaskScheduler.HasInstance
            ? TaskScheduler.Instance.CountAssignedWorkers(this, IsDemolishTask)   // ⭐ 件5：只计拆除任务
            : 1;
        if (n <= 1) return baseSeconds;
        return Mathf.Max(0.01f, baseSeconds / (1f + (n - 1) * _buildConfig.cooperativeBuildK));
    }

    /// <summary>⭐ `U-8` 件5：只计「拆除任务」（`args is DemolishTaskArgs`）—— `DemolishDuration` 的 `n` 过滤谓词。
    /// ⚠️ 静态（⛔ 不捕获 `this`）⇒ Roslyn 缓存委托 ⇒ 无每帧分配。</summary>
    private static bool IsDemolishTask(KingdomTask t) => t != null && t.args is DemolishTaskArgs;

    /// <summary>
    /// 2_12 步骤7B / D117：驱动头顶施工进度条。
    /// Constructing/Upgrading→显示 constructProgress；Ruined→显示空"待重建"条；Active/其他→隐藏。
    /// 位置：footprint 中部上方（建筑头顶）。惰性创建 BuildProgressBar，尺寸=footprint 世界宽。
    /// </summary>
    private void UpdateProgressBar()
    {
        // Constructing/Upgrading（_pendingUpgrade 亦为 Constructing 态）显示 constructProgress；Ruined 显示空"待重建"条。
        bool constructingOrUpgrading = state == BuildingState.Constructing;   // Upgrading 复用 Constructing 态(_pendingUpgrade=true)
        // ⭐ `M1-C` 件4：拆除中（仍 Active 态）也显示进度条（拆除＝有耗时 · 与建造对称）
        bool show = constructingOrUpgrading || state == BuildingState.Ruined || _demolishing;
        float progress = _demolishing ? _demolishProgress : (constructingOrUpgrading ? constructProgress : 0f);

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
                storage.TrimToCapacity();   // ⭐ M1-A：多资源仓的「压回容量线」（原单资源 min 的等价语义）
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
        _pendingRepair = false;
        state = BuildingState.Constructing;
        constructProgress = 0f;
        // ⭐ `M1-C` 件1／`09` §16.3-1：升级与建造**同构**（投料 ⇒ 等时间）。
        //   投入改在「入工地仓」时记账（裁决自陈-3）⇒ ⛔ 此处不再预记 totalInvested。
        var uc = def.levels[level - 1].upgradeCost;
        BeginMaterialPhase(uc);
        UpdateVisual();
        return true;
    }

    // ===== 拆除（⭐ `M1-C` 件2／件3／件4：全退 ＋ 随本体掉箱 ＋ 有耗时与工人）=====

    /// <summary>
    /// ⭐【HH.294 片4·4-H】**「可否拆」＝数据栏判据**（`03` §7.5「本层不判'能不能删'⇒ 业务规则归**高级层／数据栏**」）。
    /// 改前该三字段组合**写在 UI 里现算**（`BuildingPanel.cs:169`）——判定归数据，UI 只读结果。
    /// 与 `Demolish()` 的守卫**同源**（同一表达式，禁两处各写一遍）。
    /// ⭐ `U-8` 件8（`E3` · `D800`）：补 `state != Ruined` 判据 —— ① 消除「数据层可拆／UI 隐藏按钮」的口径分裂
    ///   （`BuildingPanel.cs:79` 对 `Ruined` 隐藏拆除按钮 ⇒ 本判据此前与 UI **不同源**）；
    ///   ② 顺手封 `E1` 陈旧面板路径（`BuildingPanel` ⛔ 不订阅 `BuildingRuinedEvent` ⇒ 面板开着时建筑被打毁
    ///   ⇒ 拆除按钮仍在 ⇒ `BuildingPanel.cs:393` 的 `if (!_target.CanDemolish) return;` 在此拦下）。
    /// ⚠️ 附报（观察项 · ⛔ 本片不改）：`Ruined` 的清理出口此后只剩「重建」（玩家无路径彻底清掉废墟）。
    /// </summary>
    public bool CanDemolish => isPlayerBuilt && def != null && def.isDestructible && !def.isResourceNode
        && state != BuildingState.Ruined;

    /// <summary>
    /// 拆除入口（由 `BuildingPanel` 调）。
    /// ⭐ `M1-C` 件4（`09` §16.3-3「拆除要耗时与工人 ⇒ 与建造对称」）：本方法**只进入拆除态**；
    /// 真正拆除在进度到 1（且**有工人到场**）时由 `FinishDemolish()` 执行。
    /// ⛔ 退役旧行为：原「瞬时 `Die` ＋ 按 `hp/maxHp` 比例直接退国库」。
    /// ⭐ `U-8` 件1（案甲 · `D800` `Q1`）：本方法内**就地补注册本体为任务源** —— 非 `Active` 态建筑此前
    ///   不在 `_sources`（注册守卫只收 `Active`）⇒ 无工人可派 ⇒ `_demolishProgress` 恒 0 ⇒ 卡死。
    /// ⭐ `U-8` 件3（`D800` `Q3`）：拆除中**注销工地仓源**（⛔ 不再广告搬料；⛔ 内容物不动 ⇒ 仍由 `FinishDemolish` 掉箱）。
    /// ⚠️ **顺序约束**：`EnsureRegistered()` 必须在 `_demolishing = true` **之后**调用 ——
    ///   `IsValid`（本文件 `:1349-1350`）为三项 `||`（`Active || _awaitingMaterials || _demolishing`）
    ///   ⇒ 只有置真后才保证**任意 state** 下 `IsValid=true` ⇒ `TaskScheduler.Tick:223` 不会把本体清出在册表。
    /// </summary>
    public void Demolish()
    {
        if (!CanDemolish) return;
        if (_demolishing) return;                                    // 幂等
        if (state == BuildingState.Dead || state == BuildingState.Placing) return;
        _demolishing = true;
        _demolishProgress = 0f;
        // ⭐ `U-8` 件3（D800 `Q3`）：拆除中 ⛔ 不再广告搬料（注销工地仓源）。
        //   ⚠️ `UnregisterSiteStore` 只从 `_sources` 移除 ＋ 清 `_siteRegistered`，⛔ 不动 `_items`
        //   ⇒ 内容物安全（唯一掉箱口 `DropSiteStoreToChest` 与「是否在册」正交 · `U-4` 职责分离不变）。
        UnregisterSiteStore();
        // ⭐ `U-8` 件1（案甲）：就地补注册（幂等）—— ⚠️ 必须在 `_demolishing = true` 之后（见方法头注顺序约束）。
        EnsureRegistered();
        UpdateVisual();
        Debug.Log($"[Building] {def?.id} 开始拆除（时长≈{DemolishDuration():F1}s · 需工人到场推进 · `09` §16.3-3）");
    }

    /// <summary>
    /// ⭐ `M1-C` 件4 收口：拆除进度到 1 ⇒ 真拆。
    /// · 件2（`09` §16.2）：退还量 ＝ **全退**（⛔ 不随受损减少 —— `hp/maxHp` 比例机制**退役**）
    ///   ＋ **逐阶段造价逐类型累加**（⛔ 不再只摊 金/石/木/粮 · `09#48`；⛔ 亦不再是「累计投入 ÷ `def.cost` 占比摊」
    ///   —— 该旧口径见 `BuildRefundPack` 注释所述 **U-1 资源张冠李戴**，已随本片修复退役）。
    /// · 件3（`09` §16.1-4）：退还 **随「建筑本体」掉箱**（⛔ 退役 `RulerController.Refund` 直入国库路径
    ///   ⇒ ⚠️ 行为变化：拆房后退的材料**落在箱子里，要工人搬回**）。
    /// </summary>
    private void FinishDemolish()
    {
        var refundPack = BuildRefundPack();
        // ⭐ 件3：随本体掉箱（无主箱 · `09` §9.8）；⭐ `M1-D`/#57：`SpawnChest` 已去 `faction` 形参（D802 Q7）
        if (!refundPack.IsZero && ChestManager.HasInstance)
        {
            ChestManager.Instance.SpawnChest(coord, refundPack);
            Debug.Log($"[Building] {def?.id} 拆除退还掉箱 @({coord.x},{coord.y})：{refundPack}"
                      + "（⛔ 国库不即时增加 · 需工人搬回 · `09` §16.1-4）");
        }
        DropSiteStoreToChest();   // 工地仓内容物掉箱（`09` §16.3-4）——⭐ `U-4`：仓内容物的**唯一**掉箱口
        Die(DeathCause.Demolished);
    }

    /// <summary>
    /// ⭐ `M1-C` · **U-1 修复（丙′）**：退还量 ＝ **已支付阶段造价（逐类型精确累加）**。
    /// ⛔ 退役旧口径「累计投入 × 占比摊」（`invested × e.amount / baseSum`）—— 该口径**分母只用基础造价**
    /// 而分子含升级投入 ⇒ **升级投入被折进基础资源类型**（实测：House L1 应 {Wood:14,Stone:6} 实退 {Wood:20}；
    /// Warehouse L1 应 {Gold:4,Stone:14,Wood:10} 实退 {Gold:14,Stone:14} ＝ **资源张冠李戴**）。
    /// ⭐ 新口径：各阶段造价（`def.cost` ／ `levels[i].upgradeCost`）**本身就是逐类型精确量**
    /// ⇒ **直接逐阶段累加**，⛔ 不需要"按占比摊"（零舍入误差）；⛔ **不再读 `totalInvested`**（标量近似）。
    /// ⭐ **`U-4` 修复 · 职责分离**：本方法**只退「已支付造价」**，⛔ **不含工地仓内容物** ——
    ///   仓内容物由 <see cref="DropSiteStoreToChest"/> **唯一负责**掉箱（`FinishDemolish` 两者皆调
    ///   ⇒ 旧版在此叠加 `_siteStore.Contents` 会与它**双计同一批料**：实测 House 升级投料中 Stone 3/16
    ///   ⇒ 落箱合计 {Wood:4,Stone:6} ＝ Stone 多退 3）。
    ///   ⚠️ `DropSiteStoreToChest` 必须保留：`Die` 路径（工地被打毁）靠它 ⇒ 材料不丢。
    /// ⛔ 不改签名；掉箱 `Faction.None` 不变。
    /// </summary>
    private ResourceList BuildRefundPack()
    {
        if (def == null) return ResourceList.Empty;
        var pack = PaidStageCost();
        if (_awaitingMaterials)
        {
            // 在投阶段：该阶段**已支付**的「金」（下单即扣 · 裁决 2 金-A）。
            // ⛔ 不含已到料 —— 仓内容物由 `DropSiteStoreToChest()` 唯一掉箱（`U-4`：此处叠加即双计）。
            pack = pack + GoldOnlyOf(CurrentStageCost());
        }
        return pack;
    }

    /// <summary>
    /// ⭐ U-1 修复：**已支付阶段**的累计造价（逐类型精确累加）。
    /// ＝ `def.cost`（建造）＋ `Σ_{i=0}^{level-2} levels[i].upgradeCost`（已完成升级）
    ///   ＋〔在投升级**已付清**（料齐但进度未满 ⇒ `level` 尚未 ++）⇒ 计该次〕
    ///   ＋〔在投修复**已付清** ⇒ 计修复费〕。
    /// ⛔ 不读 `totalInvested`（其自 `D162` 起为**标量近似** · 见字段注释）；⛔ 不"按占比摊"。
    /// </summary>
    private ResourceList PaidStageCost()
    {
        var pack = ResourceList.Empty;

        // 阶段态：`Constructing` ⟺ 有阶段在投（首次建造／升级／修复，三者由 `_pending*` 区分）
        bool inProgress = state == BuildingState.Constructing;
        bool paid = inProgress && !_awaitingMaterials;             // 在投阶段已付清（料齐／无料）
        bool isBuild = inProgress && InProgressStageIsBuild();
        bool isRepair = inProgress && _pendingRepair;

        // ① 建造阶段：已完工（非"在投的首次建造"）或在投且已付清
        if (!isBuild || paid) pack = pack + def.cost;

        // ② 已完成升级 ＋ ③ 在投升级已付清（`level` 未 ++ ⇒ 单独补计）
        int n = Mathf.Max(0, level - 1);
        if (inProgress && paid && !isBuild && !isRepair) n++;
        if (def.levels != null)
        {
            for (int i = 0; i < n && i < def.levels.Length; i++)
            {
                var uc = def.levels[i].upgradeCost;
                if (uc.items == null) continue;
                for (int k = 0; k < uc.items.Length; k++)
                    if (uc.items[k].amount != 0) pack = pack.Add(uc.items[k].type, uc.items[k].amount);
            }
        }

        // ④ 在投修复已付清（进度未满）：修复费 ＝ 已完成阶段造价 × ratio（与 `GetRepairCost` 同源）
        if (isRepair && paid) pack = pack + pack * RepairCostRatio();
        return pack;
    }

    /// <summary>
    /// 当前**在投阶段**的完整配方（供 `BuildRefundPack` 取其「金」部分）。
    /// 升级 ⇒ `levels[level-1]`；废墟重建 ⇒ `GetRepairCost()`；否则 ⇒ 首次建造 `def.cost`。
    /// </summary>
    private ResourceList CurrentStageCost()
    {
        if (_pendingRepair) return GetRepairCost();
        if (!InProgressStageIsBuild() && def.levels != null && level - 1 < def.levels.Length)
            return def.levels[level - 1].upgradeCost;
        return def.cost;
    }

    /// <summary>
    /// 在投阶段是否为**首次建造**（＝「建造阶段尚未付清」）。判据优先级：
    /// ① 运行期 `_pendingUpgrade` / `_pendingRepair` 标志（权威 · 二者为「后续阶段」）；
    /// ② `level > 1` ⇒ 已有完成升级 ⇒ 建造必已完成；
    /// ③ **旧档兜底**：`_pendingUpgrade` / `_pendingRepair` 为**旧档兼容**（⭐ `U-8` 件9（`U-5`）起新档已入档
    ///    ⇒ 本兜底只对「新档之外」＝旧档生效）
    ///    ⇒ 用「在投配方 `_siteNeed`」比对 `SiteNeedOf(def.cost)`（相等 ⇒ 首次建造）；
    /// ④ `_siteNeed` 为空 ⇒ **无料可搬**（纯金造价／AI 台账直扣直建／零造价）⇒ 亦按首次建造处理。
    /// ⚠️ ③④ 依赖「升级配方 ≠ 建造配方」且「无『去金为空』的升级」：本端机械扫描全库
    ///    **12 栋含升级资产 · 各级 0 碰撞** ＋ **21 级升级 · 去金为空 0 处**（实测）
    ///    ⇒ 当前资产集下**精确**；新增资产若碰撞则须先补 `_pending*` 入档。
    /// </summary>
    private bool InProgressStageIsBuild()
    {
        if (_pendingUpgrade || _pendingRepair) return false;
        if (level > 1) return false;
        if (_siteNeed.IsZero) return true;
        return SamePack(_siteNeed, SiteNeedOf(def.cost));
    }

    /// <summary>两条资源量列表是否逐类型等价（`ResourceList` 无 `Equals` ⇒ 单源小工具）。</summary>
    private static bool SamePack(ResourceList a, ResourceList b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (b.Get(a.items[i].type) != a.items[i].amount) return false;
        return true;
    }

    /// <summary>修复费比例（`RepairConfig` SO · 缺省 0.5 · 单源：`GetRepairCost` ／ `PaidStageCost` 共用）。</summary>
    private static float RepairCostRatio()
        => RepairConfig.Instance != null ? Mathf.Clamp01(RepairConfig.Instance.repairCostRatio) : 0.5f;

    /// <summary>
    /// 工地仓内容物掉箱（`09` §16.3-4「建造中的建筑（工地）有 HP · 能被打 · 打毁 ⇒ 仓里材料掉箱」）。
    /// 拆除投料中的工地同理（材料不凭空消失）。
    /// </summary>
    private void DropSiteStoreToChest()
    {
        if (_siteStore == null) return;
        var contents = _siteStore.Contents;
        if (contents.IsZero) return;
        _siteStore.Clear();
        if (ChestManager.HasInstance)
        {
            ChestManager.Instance.SpawnChest(coord, contents);   // ⭐ `M1-D`/#57：去 faction 参数（箱无主）
            Debug.Log($"[Building] {def?.id} 工地仓材料掉箱 @({coord.x},{coord.y})：{contents}（`09` §16.3-4）");
        }
    }

    /// <summary>
    /// ⭐ `M1-D` 件5（`09#49` · §九「对象生命周期结束 ⇒ 它的仓变成掉落箱」· `D802` `Q3` `Die` 路径）：
    /// **产出仓**（本体 `StorageComponent`）内容 ⇒ 掉箱（**一容器一箱** · 与工地仓/国库仓分箱 · `D802` `Q6`）。
    /// ⚠️ 件4 标签：`droppable == false` ⇒ 跳过（不可掉落仓内容不回吐；默认 `true`＝默认可掉 · `D803` 裁）。
    /// ⚠️ 空仓 ⇒ 不生成实体（§九 生成条件）；⛔ 与 `DropSiteStoreToChest`（工地仓）各自独立（`U-4` 职责分离不变）。
    /// </summary>
    private void DropStorageToChest()
    {
        var storage = GetComponent<StorageComponent>();
        if (storage == null || !storage.droppable) return;         // 件4：不可掉落标签（默认可掉）
        var contents = storage.Contents;
        if (contents.IsZero) return;                               // 空仓 ⇒ 不落箱
        storage.Clear();
        if (ChestManager.HasInstance)
        {
            ChestManager.Instance.SpawnChest(coord, contents);
            Debug.Log($"[Building] {def?.id} 产出仓内容掉箱 @({coord.x},{coord.y})：{contents}（`09` §九 / `M1-D` 件5）");
        }
    }

    /// <summary>
    /// ⭐ `M1-D` 件5：**国库仓**（`TreasureVault` 子容器）内容 ⇒ 掉箱（`09` §9.7「国库金仓 ✅ 可掉」）。
    /// ⚠️ 金币当前不在仓（金进仓随 `M1-E`）⇒ 本路径当前承载材料/粮；空容器 ⇒ 不落箱（§九 生成条件）。
    /// </summary>
    private void DropVaultToChest()
    {
        var vault = GetComponent<TreasureVault>();
        if (vault == null) return;
        var contents = vault.Contents;
        if (contents.IsZero) return;                               // 空仓 ⇒ 不落箱
        vault.ResetAll();
        if (ChestManager.HasInstance)
        {
            ChestManager.Instance.SpawnChest(coord, contents);
            Debug.Log($"[Building] {def?.id} 国库仓内容掉箱 @({coord.x},{coord.y})：{contents}（`09` §9.7 / `M1-D` 件5）");
        }
    }

    // ===== `SumCostOf` —— ⭐ `M1-C` 件2 随「全资源口径」同源化**已删** =====
    //   改前口径：`includeMetal=false` ⇒ 旧「四资源」（金/石/木/粮）；`true` ⇒ 旧「五资源」（＋铁）。
    //   改后（裁决 4-a 同源化）：`LoadState` 兜底／`BuildingFactory.SpawnFromSave` 兜底改用 `def.cost.TotalCount`
    //     ⇒ 本方法**零调用方**（全库 grep：仅本注释命中）⇒ 按裁决 4「零调用即删」同源处理删除。
    //   ⚠️ 现有 40 栋 `cost` 只含 金/石/木/粮/铁 ⇒ 两种口径**读数逐值相同**（本删除零行为差异）。
    //   ⭐ `M1-C` · U-1 修复后：`BuildRefundPack`（拆除退还）／`GetRepairCost`（修复费）**已改由 `PaidStageCost()`
    //     逐类型累加** ⇒ ⛔ **不再有 `costSum` 分母**（旧「累计投入 ÷ `def.cost` 占比摊」口径整体退役）。

    // ===== ISaveable 实现（3.5 实施计划 P0 步骤3）=====

    public SavePayload SaveState()
    {
        var storage = GetComponent<StorageComponent>();
        // ⭐ M1-A：国库（子物体容器）随建筑存档显式存取（判据 6：仓内容与容量线逐项一致）
        var vault = GetComponent<TreasureVault>();
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
            // ⭐ `U-8` 件9（`U-5`）：在投阶段标志入档（尾插零 bump · 旧档缺字段→false ⇒ ③④ 兜底仍生效）
            pendingUpgrade = _pendingUpgrade,
            pendingRepair = _pendingRepair,
            sourceType = (int)sourceType,
            storageContents = storage != null ? storage.Contents : ResourceList.Empty,
            treasuryContents = vault != null ? vault.Contents : ResourceList.Empty,
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
            anchorFeature = anchorFeature,
            // ⭐ `M1-C` 件1／件4（判据 7：新档能存能读 —— 工地仓内容物／投料进度一并）·尾插零 bump
            siteNeed = _siteNeed,
            siteContents = _siteStore != null ? _siteStore.Contents : ResourceList.Empty,
            awaitingMaterials = _awaitingMaterials,
            demolishing = _demolishing,
            demolishProgress = _demolishProgress
        };
        return new SavePayload
        {
            typeName = typeof(BuildingSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    /// <summary>
    /// ISaveable 读档（`SaveManager` 阶段 2 按 `saveId` 分发给**已注册实例**）。
    /// ⚠️ **`N-3` 防御性留档**：本方法⛔ **不恢复 `state`** —— `state` 由**创建时** `initialState` 置入
    ///   （真实读档路径：`BuildingFactory.SpawnFromSave` 先 `InstantiateFromDef(..., (BuildingState)data.state)`
    ///   ⇒ `BuildingFactory:155 b.state = initialState`；本方法随后被阶段 2 分发调用）。
    ///   ⇒ ⛔ **勿直接对"已存在且 `state` 未按存档置入"的实例调本方法**（否则该实例 `state` 沿用旧值）。
    ///   ⭐ `U-8` 件10 勘正：旧注"实测：升级料齐未完工的建筑读档后 `state` 退化为 `Active`"系**构造法样本**
    ///   （对已存在实例直调 ⇒ `state` 沿用旧值）；**生产路径**下 `state` 逐值往返
    ///   （`SaveState:1037 state=(int)state` → `BuildingFactory:292 state=(BuildingState)data.state` →
    ///   `:155 b.state = initialState`）⇒ ⛔ **不退化**（非 `Active` 存档态确实存在 ⇒ 件1/件2 的补注册为必需）。
    /// </summary>
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

        // 2_12 步骤7 / D155：累计投入件数恢复（⚠️ `M1-C` · U-1 后**备而未用** ⇒ 仅存档往返保真，⛔ 不入任何算式）。
        //   旧档缺字段(data.totalInvested=0，且玩家建筑无默认) → 兜底按 `def.cost.TotalCount` 计（保持旧档读数不变）。
        totalInvested = data.totalInvested > 0
            ? data.totalInvested
            : (isPlayerBuilt && def != null ? def.cost.TotalCount : 0);

        // ⭐ `M1-C` 件1／件4：投料态与拆除态恢复（判据 7：新档能存能读 —— 工地仓内容物／进度一并）
        _demolishing = data.demolishing;
        _demolishProgress = Mathf.Clamp01(data.demolishProgress);
        // ⭐ `U-8` 件9（`U-5`）：在投阶段标志恢复（尾插零 bump · 旧档缺字段→false ⇒ `InProgressStageIsBuild` ③④ 兜底仍生效）
        _pendingUpgrade = data.pendingUpgrade;
        _pendingRepair = data.pendingRepair;
        _siteNeed = data.siteNeed;
        _awaitingMaterials = false;
        if (data.awaitingMaterials && !_siteNeed.IsZero)
        {
            var site = EnsureSiteStore();
            site.SetNeed(_siteNeed);
            site.RestoreContents(data.siteContents);
            _awaitingMaterials = !site.IsSatisfied;
            if (_awaitingMaterials) RegisterSiteStore();
        }

        // 【HH.294 片4·4-E】锚点消费记录恢复（旧档缺字段默认 0 ⇒ 与坐标 0 混淆，故按「有锚点类型」判有效）
        anchorCoordX = data.anchorFeature >= 0 ? data.anchorCoordX : -1;
        anchorCoordY = data.anchorFeature >= 0 ? data.anchorCoordY : -1;
        anchorFeature = data.anchorFeature >= 0 ? data.anchorFeature : -1;

        var storage = GetComponent<StorageComponent>();
        if (storage != null) storage.RestoreContents(data.storageContents);

        // ⭐ M1-A：国库内容恢复（旧档 `treasuryContents` 为空 ⇒ 由 KingdomManager 旧桥兜底已退役 ⇒ 空仓）
        var vault = GetComponent<TreasureVault>();
        if (vault != null) vault.RestoreContents(data.treasuryContents);

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

        // ⭐ `U-8` 件2：读档时正在拆除 ⇒ 补注册本体（存档态非 `Active` ⇒ `BuildingFactory:189` 只收 `Active`
        //   ⇒ 本体未在册）；⛔ 不补则 `Demolish():851` 幂等守卫使玩家再也点不动 ⇒ **永久冻结**（比重试更糟）。
        //   ⚠️ 形态与件1 同源（同一句复用）；⛔ 不改 `BuildingFactory:189` 守卫（把存档字段语义塞进工厂＝职责串层）。
        if (_demolishing) EnsureRegistered();
    }

    /// <summary>
    /// 2_12 步骤7 / D155：修复/废墟重建成本（库存储入时点调用，勿入每帧路径）。
    /// ⭐ `M1-A`：返回类型 `ResourcePack` ⇒ `ResourceList`。
    /// ⭐ `M1-C` 件2（裁决 4-a）：与 `BuildRefundPack` **同源化**（`SumCostOf`／`IsRefundResource` 一并退役）。
    /// ⭐ `M1-C` · **U-1 修复（丙′）＋ U-3**：基数改 **`PaidStageCost()`（已支付阶段造价 · 逐类型精确累加）**
    ///   × `RepairConfig.repairCostRatio`（⛔ 不再"按 `def.cost` 占比摊"、⛔ **不再读 `totalInvested`**）。
    ///   ⇒ **U-3（修复费复利）消失**：旧基数 `totalInvested` 含**历史修复花费**（`BuildingPanel:322/340` 的
    ///   `AddInvested` ＋ 修复料逐笔入账）⇒ 每次重建递增 `C → 1.5C → 2.25C → 3.375C…`；新基数与修复历史无关。
    /// </summary>
    public ResourceList GetRepairCost()
    {
        if (def == null) return ResourceList.Empty;
        var full = PaidStageCost();
        if (full.IsZero) return ResourceList.Empty;
        return full * RepairCostRatio();   // 逐类型 `Mathf.RoundToInt`（`ResourceList.operator *` 单源）
    }

    // ===== 战斗（3.4 实现 IDamageable）=====

    /// <summary>⭐ `M1-D`（`D802` `Q1`）：致死一击施加者（`TakeDamage` 的 `source` 暂存 · `Die` 发布事件时填 `Killer`）。
    /// ⚠️ `Die` 侧按 `cause` 判：仅 `Killed` 用本字段；`Demolished`（拆除）恒 `null`
    /// （防「先被打过、后被拆除」把旧攻击者带进事件）。</summary>
    private IDamageable _lastDamageSource;

    /// <summary>是否工事（2_12 步骤7 / D165）：城墙/城门/桥/防御塔被破直接销毁，不进废墟。主城(Special)例外进废墟可修。</summary>
    public bool IsFortification
        => def != null && (def.role == BuildingRole.Wall || def.isGate || def.isBridge
                           || (def.role == BuildingRole.Defense && sourceType != BuildingType.CastleCore));

    /// <summary>
    /// 受到伤害，只扣血。伤害已由 DamageSystem 算好+取整。
    /// 血量≤0：工事(D165)直接销毁；非工事(含主城 D163)进废墟(Ruined)可修复，不直接判负。
    /// 非 Active 态不受伤。
    /// </summary>
    public void TakeDamage(int amount, IDamageable source = null)
    {
        if (state != BuildingState.Active) return; // 非 Active 不受伤
        _lastDamageSource = source;                 // ⭐ `M1-D` 件1：Killer 链（致死一击施加者 · 工事 `Die(Killed)` 用）
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

        // ⭐ `U-8` 件6（`E1`）互斥口径：**生命周期事件优先于玩家指令**（`09` §16.3.1「耐久到 0 ⇒ 触发生命周期结束」）。
        //   `Demolish()` ⛔ 不改 `state` ⇒ 拆除中仍 `Active` ⇒ `TakeDamage:1182` 照常受伤 ⇒ 打空进本方法；
        //   ⛔ 若不清 `_demolishing`：闩锁无解（`Demolish:851` 幂等 ＋ `StartRebuildFromRuins:514-523` 亦不清）
        //   ⇒ `Update:594` 永走拆除分支 且 `HasAssignedWorker()` 恒 false ⇒ **重建永久冻结** ＋ 料齐时 `Clear()` 吞料。
        //   ⛔ 否决「`Demolish()` 侧互斥」形态（本场景是"先开拆、后被打毁"，该形态不解决）。
        _demolishing = false;
        _demolishProgress = 0f;

        // 在册工人撤出（废墟不供职；工人在内可能被打/被卡，撤出存活）
        EscapeWorkers();
        // 训练中断回退（若为训练建筑）
        if (TrainingSystem.Instance != null)
            TrainingSystem.Instance.OnBuildingDestroyed(this);
        // 从任务调度器注销（不再是 Active 源）；保持 GridSystem 占格 → D154 阻挡持续
        if (TaskScheduler.HasInstance) TaskScheduler.Instance.Unregister(this);
        // ⭐ `U-8` 件7（`E1`）：废墟态 ⛔ 不再广告搬料（否则工人给废墟继续送料）。
        //   ⚠️ 内容物 **留仓**（⛔ 不 `Clear()`）：`Ruined` ⛔ 不是生命周期结束（可重建 · D154「废墟不 Free 占格 · 可修复」）
        //   ⇒ 材料留在工地仓等重建 ⇒ 重建时 `BeginMaterialPhase → EnsureSiteStore` 复用同一实例（`:548`）
        //   ⇒ `SetNeed` 重置需求 ＋ 旧 `_items` 仍在 ⇒ 够则即时开工、差则继续搬（正是留仓的价值）。
        //   ⛔ 不动 `DropSiteStoreToChest` / `Die` 路径（工地被打毁掉箱走 `Die:1259`）。
        UnregisterSiteStore();

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

        // ⭐ `M1-C`：工地仓收口（`09` §16.3-4「工地被打毁 ⇒ 仓里材料掉箱」）。
        //   ① 注销搬料源（在派搬料任务由调度器自动放弃）② 剩余材料掉箱（材料不凭空消失）。
        //   ⚠️ 拆除路径 `FinishDemolish` 已先行掉箱并清空 ⇒ 此处幂等（`Contents` 空 ⇒ 不重复落箱）。
        UnregisterSiteStore();
        DropSiteStoreToChest();
        // ⭐ `M1-D` 件5（`09#49` · `D802` `Q3`/`Q6`）：**产出仓 ＋ 国库仓** 分箱掉箱
        //   （工地仓＝独立第三路，上句已办 ⇒ 三路互不重叠）。⛔ `EnterRuined`（非工事被打爆）仓留存
        //   —— 生命周期未结束（可修 · D154）⇒ 不在此路径（Q3 负向判据）。
        DropStorageToChest();
        DropVaultToChest();

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
            cause == DeathCause.Killed ? _lastDamageSource : null,   // Killer（⭐ M1-D：工事被破=致死一击施加者；拆除恒 null）
            cause              // Cause（Killed=被击杀，Demolished=玩家拆除）
        ));

        Destroy(gameObject);
    }

    // ===== 【HH.294 片 6-2·6-D】`OnGatherCompleted()` / `FeatureOf()` / `StartGather()` **随实体退役 已删** =====
    //   改前职责链（一次性资源点）：`BuildingPanel` 采集按钮 → `StartGather`（锁 `isBeingGathered`）
    //     → `TryAdvertiseTask` 采集分支 → 调度器派工 → 完成回调 `OnGatherCompleted`
    //     （＝ 释放占格＋注销注册表＋守卫失去＋`HandleEntityDepleted`＋对象池回收）。
    //   改后（数据寻址）：玩家 = `PrioritizeHarvestCommand` → `ResourceRespawnSystem.ConfirmResourceGather`
    //     → `WorldGatherSource` → 完成 = `ResourceRespawnSystem.HandleCellGathered`
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
    [Tooltip("农场缺水阈值（**农场仓** Water 量 < 此值 发布 WaterHaul · ⭐ `M1-F` 起读本仓；整数化 `D807` Q4）")]
    public int waterThreshold = 20;

    /// <summary>任务源世界坐标（建筑坐标）。</summary>
    public Vector2 SourcePos => transform.position;

    /// <summary>任务源是否有效（未被销毁且已 Active）。
    /// ⭐ `M1-C`：**投料态工地**（`Constructing` ＋ 料未齐）与**拆除态**也是合法源
    /// —— 前者广告「搬料任务」、后者广告「拆除任务」；两者 `TryAdvertiseTask` 内部按态分流，
    /// ⛔ 不会漏出 Production/Transport/WaterHaul。</summary>
    public bool IsValid => this != null
        && (state == BuildingState.Active || _awaitingMaterials || _demolishing);

    /// <summary>
    /// 按建筑类型声明任务（QQQ.2 §10.3 / DR-16）：
    ///   ① 生产建筑无工人在场且未满 → Production（destType=None）
    ///   ② 有存储且存量 ≥ capacity×transportThreshold → Transport（destType=NearestWarehouse）
    ///   ③ 农场缺水（**农场仓** Water < waterThreshold）→ WaterHaul（源＝最近**同国**有水**水井** ·
    ///      destType=SpecificBuilding 终点＝本农场 · `M1-F` 件4 真搬运）
    /// 军事/其他不在此扩。无条件返回 false。
    /// 【HH.294 片 6-2·6-D】原「①一次性资源点被确认采集 → Gather」分支**随实体退役已删**
    ///   —— 采集任务改由 `WorldGatherSource` 广告（数据寻址·唯一天然资源采集源）。
    /// </summary>
    public bool TryAdvertiseTask(out KingdomTask task)
    {
        task = null;

        // ⭐ `M1-C` 件4（`09` §16.3-3）：**拆除中 ⇒ 只广告「拆除任务」**（工人到场推进拆除进度），
        //   ⛔ 不再广告 Production/Transport/WaterHaul（拆除中的建筑不该继续生产／搬运）。
        if (_demolishing)
        {
            task = new KingdomTask(KingdomTaskType.Build, this);
            task.destType = KingdomDestType.None;   // 原地劳作：工人走到建筑本体（SourcePos）
            task.args = new DemolishTaskArgs { target = this };
            return true;
        }

        // ⭐ `M1-C` 件1：**投料态工地由 `ConstructionSiteStore` 广告搬料任务**（⛔ 本体不广告）
        //   ⇒ 非 Active 态一律返回 false，防 Constructing 态漏出 Production/Transport/WaterHaul。
        if (state != BuildingState.Active) return false;

        // 2_17 修复卡β：删除补丁D广告守卫(L900)。AI 王国建筑(kingdomId>0)照常发布任务；
        // 防"AI 任务流向玩家 worker"的补丁D意图已由 TaskScheduler.Tick 池隔离路由结构性达成——
        // 任务源归属国 tKingdom 只派给同国 idleKingdom 工人，广告侧守卫成永久双轨，此处清理收编。
        // 无主自然建筑(-1)仍按原流程发布（采集），路由时降级先到先得池（见 TaskScheduler）。
        var producer = GetComponent<ProducerComponent>();
        var storage = GetComponent<StorageComponent>();
        var sched = TaskScheduler.Instance;

        // ① 采集：一次性资源点（isConsumable）被玩家确认采集 → Gather 任务 —— 【片 6-2·6-D】已删（见方法头注）

        // ② 生产：无工人在场（Working）且存储未满 → 生产任务（水井除外：⭐ `M1-F` 起免工自产入**本仓**，不派生产任务）
        if (producer != null
            && !producer.IsWell
            && (storage == null || !storage.IsFullFor(producer.OutputResource))
            && (sched == null || !sched.HasWorkerAssigned(this)))
        {
            task = new KingdomTask(KingdomTaskType.Production, this);
            task.destType = KingdomDestType.None;
            return true;
        }

        // ③ 搬运：存储达标且存量>0 → 搬运任务（2_8 步骤3 / D95：把资源总需求附带进 task.args，调度器据此规模派工）
        // ⭐ M1-A：多资源仓 ⇒ 按「首个非空资源」（资源表序·确定性）判达标并附其类型/量（对齐 StorageComponent 过渡读口）
        if (storage != null && storage.capacity > 0 && storage.TotalCount > 0)
        {
            var t = storage.PrimaryStoredType();
            int stored = storage.GetAmount(t);
            if (stored > 0 && stored >= storage.capacity * transportThreshold)
            {
                task = new KingdomTask(KingdomTaskType.Transport, this);
                task.destType = KingdomDestType.NearestWarehouse;
                task.args = new ScaleTaskArgs
                {
                    resourceType = t,
                    totalResourceDemand = stored
                };
                return true;
            }
        }

        // ④ 搬水：仅农场（产粮耗水）在**自己仓**水不足时发搬水任务（采石/矿洞不耗水，不派）。
        //  ⭐ `M1-F` 件4（`09#44` · `09` §4.3 D 组 · `D807`）：改**真搬运** ——
        //    源＝**最近同国有水水井**（`SourcePos` ⇒ 第一段位移＝去水井取水）、
        //    终点＝本农场（第二段位移＝卸水入农场仓）；args 携带卸水落点与缺口量。
        //  ⚠️ **国别过滤必带**（`D807` §二-1）：⛔ 只选**同国**且 `Water > 0` 的 Active 水井
        //    —— 退役的 `ResolveWaterSource` 不过滤国别（半假搬运掩盖）；真搬运下 AI 农场会挑玩家井水（历史同族缺陷）。
        //  ⭐ 无候选（同国无水井/井全空）⇒ 不发布（避免下发即失败的空跑）。
        if (producer != null && producer.OutputResource == ResourceType.Food && storage != null)
        {
            int waterHave = storage.GetAmount(ResourceType.Water);
            if (waterHave < waterThreshold)
            {
                var well = FindNearestSameKingdomWellWithWater();
                if (well != null)
                {
                    task = new KingdomTask(KingdomTaskType.WaterHaul, well);   // ⭐ 源＝水井（第一段位移）
                    task.destType = KingdomDestType.SpecificBuilding;
                    task.destPos = transform.position;                          // 终点＝本农场（第二段位移）
                    task.args = new HaulWaterArgs
                    {
                        target = storage   // 卸水落点＝农场仓（⭐ `D809` 件3：水不需要 `need` —— 与 Transport 同构）
                    };
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// ⭐ `M1-F` 件4：**最近同国有水水井**（搬水任务第一段位移目标 · `D807` §二-1 国别过滤必做）。
    /// 判据：`def.id == "Well"` ＋ `state == Active` ＋ **同 `kingdomId`** ＋ **仓内 `Water > 0`**。
    /// ⛔ 不过滤 `kingdomId` ＝ 退役 `ResolveWaterSource` 的缺陷（真搬运下会让 AI 农场挑玩家井水）。
    /// ⚠️ 本端全扫 `BuildingRegistry`（照 `ResolveWaterSource` 旧形制 · 搬水为低频任务广告路径）。
    /// </summary>
    private Building FindNearestSameKingdomWellWithWater()
    {
        if (BuildingRegistry.Instance == null) return null;
        var all = BuildingRegistry.Instance.All;
        Building best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < all.Count; i++)
        {
            var w = all[i];
            if (w == null || w.def == null || w.def.id != "Well" || w.state != BuildingState.Active) continue;
            if (w.kingdomId != kingdomId) continue;                              // ⭐ 国别过滤（D807 §二-1）
            var ws = w.GetComponent<StorageComponent>();
            if (ws == null || ws.GetAmount(ResourceType.Water) <= 0) continue;   // ⛔ 无水井不候选
            float d = GridMath.DistCells(w.transform.position, transform.position);
            if (d < bestDist) { bestDist = d; best = w; }
        }
        return best;
    }

    /// <summary>注册到调度器回调（Building 纳入任务派发）。</summary>
    public void OnRegister() { }

    /// <summary>从调度器注销回调。</summary>
    public void OnUnregister() { }

    /// <summary>建筑转 Active 时注册到任务调度器（IsValid 才注册）。
    /// ⚠️ `U-8` 件10（`O5` · `D800` 裁本片不动）：`TaskScheduler.cs:86-98`（守卫 `:95`）有**同型守卫**
    ///   （单例创建时的 `Active` 补注册 · 同一语义写两遍）⇒ 未来若改注册语义**须同查两处**；
    ///   ⛔ 本片维持其 `Active` 语义（`Q1` 案甲 ⛔ 否决案乙的"四处状态集同步"）。</summary>
    private void RegisterWithTaskScheduler()
    {
        // 2_17 修复卡β：删除补丁D注册侧守卫(L968)。AI 王国建筑(kingdomId>0)也登记为任务源——
        // 补丁D"AI 任务不流向玩家"意图由路由层结构性达成，注册侧守卫与广告侧 L900 同为待清的永久双轨。
        // 玩家(0)/自然(-1)/AI(>0) 一律注册，派工归属由 TaskScheduler.Tick 池隔离路由决定。
        if (TaskScheduler.HasInstance && state == BuildingState.Active)
            TaskScheduler.Instance.Register(this);
    }

    /// <summary>⭐ `U-8` 件1（案甲 · `D800` `Q1`）：确保本体已在任务源表（幂等 · ⛔ **无 state 条件**）。
    /// 供非 `Active` 场景补注册：① `Demolish()`（任意可拆态；⚠️ 须在 `_demolishing = true` **之后**调 —— `IsValid` 依赖它）；
    /// ② `LoadState()`（读档时正在拆除 ⇒ `BuildingFactory:189` 只收 `Active` ⇒ 本体未注册 ⇒ ⛔ 不补则永久冻结）。
    /// ⛔ 不改 `RegisterWithTaskScheduler` 的 `Active` 语义（两者职责分离）；
    /// ⛔ 不新增 `IsRegistered` 查询口 —— `TaskScheduler.Register`（`TaskScheduler.cs:118-122`）经 `_sources.Add`
    /// 天然幂等（重复调用安全 ⇒ 新建查询 API 零收益）。</summary>
    private void EnsureRegistered()
    {
        if (TaskScheduler.HasInstance) TaskScheduler.Instance.Register(this);
    }
}
