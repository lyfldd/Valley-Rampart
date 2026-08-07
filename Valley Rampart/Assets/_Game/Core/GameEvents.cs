using UnityEngine;

// ===== 玩家输入事件 =====

// 玩家移动事件。由 InputManager 在 Playing 状态下每帧发布。
// position 当前未使用（预留3D扩展），moveDir 为二维移动方向向量。
public readonly struct PlayerMoveEvent
{
    public readonly Vector3 position;
    public readonly Vector3 moveDir;

    public PlayerMoveEvent(Vector3 pos, Vector3 dir)
    {
        position = pos;
        moveDir = dir;
    }
}

// 已废弃：使用 ConfigsLoadedEvent 替代（引导书 3.2 节）
// 原用途：通知 UnitData 静态配置加载完成。现由 LoadManager 统一发布 ConfigsLoadedEvent。
public readonly struct UnitDataLoadedEvent
{
    public readonly bool IsSuccess;
    public readonly int TotalCount;

    public UnitDataLoadedEvent(bool isSuccess, int count)
    {
        IsSuccess = isSuccess;
        TotalCount = count;
    }
}

// ===== 游戏状态事件 =====

// 游戏状态变化事件。由 GameStateManager.SetState 发布。
// 订阅者可据此响应状态切换（如 UI 切换面板、系统启用/禁用等）。
public readonly struct GameStateChangedEvent
{
    public readonly GameState OldState;
    public readonly GameState NewState;

    public GameStateChangedEvent(GameState oldState, GameState newState)
    {
        OldState = oldState;
        NewState = newState;
    }
}

// ===== 单位事件 =====

// 死因枚举（3.4 决策 23）。区分被击杀与玩家拆除，击杀统计只认 Killed。
public enum DeathCause
{
    Killed,      // 被击杀（战斗致死）
    Demolished   // 玩家拆除
}

// 单位死亡事件。由 UnitController/Building 在 HP 降至 0 或拆除时发布。
// Unit/Killer 类型为 IDamageable，建筑被打爆也走此事件（BuildingDestroyedEvent 退役）。
// RulerController 订阅检测君主阵亡；TopLeftHUD 订阅清君主引用；
// DamageSystem 订阅做注册表死亡清理（决策 24）；BuildingPanel 订阅关面板。
// Cause 区分被击杀/拆除：击杀统计/全灭判定只认 Killed，忽略 Demolished。
public readonly struct UnitDiedEvent
{
    public readonly IDamageable Unit;       // 死亡者（UnitController 或 Building）
    public readonly Faction Faction;        // 死亡者阵营
    public readonly Vector2 Position;       // 死亡位置（掉落/反馈用）
    public readonly IDamageable Killer;     // 击杀者（可为 null，如环境伤害/拆除）
    public readonly DeathCause Cause;       // 死因：Killed=被击杀，Demolished=玩家拆除

    public UnitDiedEvent(IDamageable unit, Faction faction, Vector2 position,
                         IDamageable killer, DeathCause cause)
    {
        Unit = unit;
        Faction = faction;
        Position = position;
        Killer = killer;
        Cause = cause;
    }
}

// ===== 资源事件 =====

// 资源类型枚举。对应君主国家持有的四种基础资源。
public enum ResourceType
{
    Gold,   // 金币
    Stone,  // 石材
    Wood,   // 木材
    Food,   // 食物
    // ===== 3.5 新资源（末尾追加保持旧值稳定；存建筑存储，不入君主国库）=====
    Ore,        // 矿石（矿洞主产；→仓库）
    Crystal,    // 法力水晶（矿洞 Lv2 副产）
    FireOil,    // 火油（矿洞 Lv3 副产）
    // ===== 3.5 P1 粮大类子资源（末尾追加；§13.11 特殊食物/肉）=====
    SpecialFood,// 特殊食物（粮食加工坊：粮×2→1；饱食+8/幸福+1；贸易额度6/4天）
    Meat        // 肉（牧场屠宰制；饱食+20/幸福+3；贸易额度4/4天）
}

// 君主资源变化事件。由 RulerController.ModifyResource 发布。
// UI 层订阅此事件刷新资源显示，无需每帧轮询。
public readonly struct RulerResourceChangedEvent
{
    public readonly ResourceType Type;
    public readonly int OldValue;
    public readonly int NewValue;

    public RulerResourceChangedEvent(ResourceType type, int oldValue, int newValue)
    {
        Type = type;
        OldValue = oldValue;
        NewValue = newValue;
    }
}

// ===== 战斗事件 =====

// 单位攻击事件。由 UnitController 在发起攻击时发布。
// 包含原始伤害值（RawDamage），实际伤害由防御公式计算后发布 UnitDamagedEvent。
public readonly struct UnitAttackEvent
{
    public readonly UnitController Attacker;
    public readonly UnitController Target;
    public readonly int RawDamage;

    public UnitAttackEvent(UnitController attacker, UnitController target, int rawDamage)
    {
        Attacker = attacker;
        Target = target;
        RawDamage = rawDamage;
    }
}

// 单位受伤事件（3.4 决策 3）。由 DamageSystem 在伤害结算后发布。
// 复用此事件（原零订阅），不新建 UnitHitEvent。Unit/Source 类型改 IDamageable，建筑也走此事件。
// 3.0.1 ThreatStimulus 订阅此事件触发威胁 3。
// 节流：DamageSystem 维护 victim->lastEventTime 字典，同一 victim 每 0.5s 最多发一次（决策 7）。
public readonly struct UnitDamagedEvent
{
    public readonly IDamageable Unit;        // 受击者（UnitController 或 Building）
    public readonly IDamageable Source;      // 攻击方（可为 null，如环境伤害）
    public readonly int ActualDamage;        // 取整后伤害（int，DamageSystem 算好传入）
    public readonly Vector2 Position;        // 受击位置（3.0.1 威胁评定/反馈层用）

    public UnitDamagedEvent(IDamageable unit, IDamageable source, int actualDamage, Vector2 position)
    {
        Unit = unit;
        Source = source;
        ActualDamage = actualDamage;
        Position = position;
    }
}

// 单位生成事件。UnitController.Initialize 完成时发布。
// UI/仇恨/存档系统可据此把握单位就绪时机。
public readonly struct UnitSpawnedEvent
{
    public readonly UnitController Unit;

    public UnitSpawnedEvent(UnitController unit)
    {
        Unit = unit;
    }
}

// 单位血量变化事件。受伤与治疗统一走此事件，血条 UI 据此刷新，无需每帧轮询。
public readonly struct UnitHpChangedEvent
{
    public readonly UnitController Unit;
    public readonly int OldHp;
    public readonly int NewHp;
    public readonly int MaxHp;

    public UnitHpChangedEvent(UnitController unit, int oldHp, int newHp, int maxHp)
    {
        Unit = unit;
        OldHp = oldHp;
        NewHp = newHp;
        MaxHp = maxHp;
    }
}

// 单位属性变化事件。Buff/装备/升级系统修改属性后发布，UI 据此刷新攻击/防御/血量上限等显示。
public enum UnitAttributeType
{
    MaxHp,      // 最大血量
    Attack,     // 攻击力
    Defense,    // 防御力
    WalkSpeed,  // 步行速度
    RunSpeed    // 跑步速度
}

public readonly struct UnitAttributeChangedEvent
{
    public readonly UnitController Unit;
    public readonly UnitAttributeType AttributeType;

    public UnitAttributeChangedEvent(UnitController unit, UnitAttributeType attributeType)
    {
        Unit = unit;
        AttributeType = attributeType;
    }
}

// ===== 时间系统事件 =====

// 时段枚举（昼夜划分）。影响敌人刷新频率、光照强度等。
public enum TimePhase
{
    Night,  // 夜晚
    Dawn,   // 黎明
    Day,    // 白天
    Dusk    // 黄昏
}

// 季节枚举。影响昼夜比例（夏白天长，冬白天短），由 TimeManager 管理。
public enum Season
{
    Spring, // 春
    Summer, // 夏
    Autumn, // 秋
    Winter  // 冬
}

// 天数变化事件。新一天开始时由 TimeManager 发布。
// Season 字段为新一天对应的季节（可能已跨季）。
public readonly struct TimeDayChangedEvent
{
    public readonly int OldDay;
    public readonly int NewDay;
    public readonly Season Season;

    public TimeDayChangedEvent(int oldDay, int newDay, Season season)
    {
        OldDay = oldDay;
        NewDay = newDay;
        Season = season;
    }
}

// 每日结算完成事件（QQQ.3 B8-2 / LC-G5 修复，D10）。由 DayCycleSettlement 在结算全部完成后发布。
// SaveManager 自动存档改订阅本事件，使"结算先、存档后"的顺序显式化，不依赖 EventBus 订阅先后。
// 修复点：旧逻辑 SaveManager 在主菜单场景先订阅 TimeDayChangedEvent，DayCycleSettlement 进 GameScene 才订阅，
// 从主菜单进游戏时自动存档先于结算执行，存档抢到"结算前"状态；读档后当天结算永不补跑。
public readonly struct DaySettledEvent
{
    public readonly int Day;
    public DaySettledEvent(int day) { Day = day; }
}

// 时段变化事件。白天↔夜晚等切换时由 TimeManager 发布。
// 订阅者可据此触发敌人刷新、光照切换、BGM 变化等。
public readonly struct TimePhaseChangedEvent
{
    public readonly TimePhase OldPhase;
    public readonly TimePhase NewPhase;

    public TimePhaseChangedEvent(TimePhase oldPhase, TimePhase newPhase)
    {
        OldPhase = oldPhase;
        NewPhase = newPhase;
    }
}

// 季节变化事件。由 TimeManager 在跨季时发布。
// 昼夜比例随之改变，DifficultyManager 也可能据此调整难度系数。
public readonly struct SeasonChangedEvent
{
    public readonly Season OldSeason;
    public readonly Season NewSeason;

    public SeasonChangedEvent(Season oldSeason, Season newSeason)
    {
        OldSeason = oldSeason;
        NewSeason = newSeason;
    }
}

// 难度系数变化事件。每过一季由 DifficultyManager 发布，供 WaveManager/战斗系统消费。
// 难度系数影响敌人波次强度、资源产出等。
public readonly struct DifficultyChangedEvent
{
    public readonly float OldFactor;
    public readonly float NewFactor;

    public DifficultyChangedEvent(float oldFactor, float newFactor)
    {
        OldFactor = oldFactor;
        NewFactor = newFactor;
    }
}

// ===== 存档系统事件 =====

// 游戏保存完成事件。由 SaveManager.Save 发布。
public readonly struct GameSavedEvent
{
    public readonly string SlotId;
    public readonly bool IsSuccess;

    public GameSavedEvent(string slotId, bool isSuccess)
    {
        SlotId = slotId;
        IsSuccess = isSuccess;
    }
}

// 游戏加载完成事件。由 SaveManager.Load 发布。
public readonly struct GameLoadedEvent
{
    public readonly string SlotId;
    public readonly bool IsSuccess;

    public GameLoadedEvent(string slotId, bool isSuccess)
    {
        SlotId = slotId;
        IsSuccess = isSuccess;
    }
}

// ===== 加载系统事件 =====

// 静态配置加载完成事件（阶段1 结束）。由 LoadManager 发布。
// 替代已废弃的 UnitDataLoadedEvent，涵盖所有静态配置（UnitData/RulerData/DifficultyConfig 等）。
public readonly struct ConfigsLoadedEvent
{
    public readonly bool IsSuccess;
    public ConfigsLoadedEvent(bool isSuccess) { IsSuccess = isSuccess; }
}

// ===== 地图生成事件 =====

// 地图生成完成事件。由 WorldManager 在 GenerateMap 返回后发布。
// BuildingFactory（建造系统⬜）订阅此事件触发 Building 实例化，
// 摄像机订阅此事件设边界，GridSystem 订阅此事件填充区块。
public readonly struct MapGeneratedEvent
{
    public readonly int MapId;
    public readonly bool IsPlayerHome;

    public MapGeneratedEvent(int mapId, bool isPlayerHome)
    {
        MapId = mapId;
        IsPlayerHome = isPlayerHome;
    }
}

// ===== 全局输入事件 =====

// 玩家按下 ESC 键事件。由 InputManager 发布，UI 系统订阅以弹出/关闭暂停菜单等。
// CurrentState 携带按下时的游戏状态，UI 层据此决定行为。
public readonly struct EscapePressedEvent
{
    public readonly GameState CurrentState;

    public EscapePressedEvent(GameState currentState)
    {
        CurrentState = currentState;
    }
}

// 玩家按下 B 键时触发。
// 由 InputManager 发布，BuildingMenuPanel 订阅以开关建造菜单。
// IsOpen=true 时打开菜单，IsOpen=false 时关闭，Toggle 模式下该字段为 null，由订阅方自行取反。
public readonly struct ToggleBuildMenuPressedEvent
{
    public readonly bool? IsOpen;
    public ToggleBuildMenuPressedEvent(bool? isOpen = null) { IsOpen = isOpen; }
}

// ===== 建造系统事件（3.3 / 3.2.2 对接契约）=====

// 建筑放置完成事件。由 BuildController.Place（玩家建造）或 BuildingFactory.InstantiateFromMap（地图预置）发布。
// GridSystem 占用标记在发布前已完成；存档系统、UI、产能系统订阅此事件。
public readonly struct BuildingPlacedEvent
{
    public readonly Building Building;
    public BuildingPlacedEvent(Building building) { Building = building; }
}

// 建筑摧毁/拆除事件。[3.4 退役] 建筑死亡改走 UnitDiedEvent（Cause 区分 Killed/Demolished）。
// 保留定义仅为编译兼容，不再发布。BuildingPanel 已改订阅 UnitDiedEvent。
public readonly struct BuildingDestroyedEvent
{
    public readonly Building Building;
    public BuildingDestroyedEvent(Building building) { Building = building; }
}

// 建筑升级事件。由 BuildingPanel.Upgrade 发布。UI 订阅刷新面板。
public readonly struct BuildingUpgradedEvent
{
    public readonly Building Building;
    public readonly int OldLevel;
    public readonly int NewLevel;

    public BuildingUpgradedEvent(Building building, int oldLevel, int newLevel)
    {
        Building = building;
        OldLevel = oldLevel;
        NewLevel = newLevel;
    }
}

// 建筑产能 tick 事件。由产能系统每秒发布（3.3.2 第四节后置工作）。
// RulerController 订阅此事件结算资源产出。
public readonly struct BuildingProductionTickEvent
{
    public readonly Building Building;
    public BuildingProductionTickEvent(Building building) { Building = building; }
}

// 建筑激活事件（3.3.4 批次3）。建造/修复/升级完成时由 Building.OnConstructionComplete 发布。
// BuildController 订阅此事件解锁建造菜单（主城修复后）；产能系统订阅此事件启动产出。
public readonly struct BuildingActivatedEvent
{
    public readonly Building Building;
    public BuildingActivatedEvent(Building building) { Building = building; }
}

// ===== LOD 性能架构事件（3.0.1_LOD §3.5 / §六.P0.7）=====

// 区块威胁热度变化事件。由 LODSystem 在热度/等级变化时发布（§3.5 衔接 3.0.1_3 协作层留接口）。
// 将军/调度中心订阅可调兵支援；调试面板订阅可画热力图。无订阅者时不发布（EventBus 守卫）。
public readonly struct RegionHeatChangedEvent
{
    public readonly int RegionIndex;
    public readonly float Heat;
    public readonly LodLevel Level;

    public RegionHeatChangedEvent(int regionIndex, float heat, LodLevel level)
    {
        RegionIndex = regionIndex;
        Heat = heat;
        Level = level;
    }
}

// ===== 编队管理事件（3.0.1_5 §六 E 键作战面板后端）=====

// 编队选中事件。由 FormationManager 在选中时发布（作战面板/UI 订阅刷新高亮）。
public readonly struct FormationSelectedEvent
{
    public readonly int FormationId;
    public FormationSelectedEvent(int formationId) { FormationId = formationId; }
}

// 编队取消选中事件。由 FormationManager 在取消选中/清空选中时发布。
public readonly struct FormationDeselectedEvent
{
    public readonly int FormationId;
    public FormationDeselectedEvent(int formationId) { FormationId = formationId; }
}

// 敌人进入区块事件。由 GridSystem.TryEnter 在敌人跨 region 时发布（§1.3 威胁类事件升整 region）。
public readonly struct EnemyEnteredRegionEvent
{
    public readonly int RegionIndex;
    public readonly UnitController Enemy;

    public EnemyEnteredRegionEvent(int regionIndex, UnitController enemy)
    {
        RegionIndex = regionIndex;
        Enemy = enemy;
    }
}