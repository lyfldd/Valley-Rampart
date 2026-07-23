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

// 单位死亡事件。由 UnitController 在 HP 降至 0 时发布。
// RulerController 订阅此事件检测君主阵亡，触发 GameOver。
public readonly struct UnitDiedEvent
{
    public readonly UnitController Unit;

    public UnitDiedEvent(UnitController unit)
    {
        Unit = unit;
    }
}

// ===== 资源事件 =====

// 资源类型枚举。对应君主国家持有的四种基础资源。
public enum ResourceType
{
    Gold,   // 金币
    Stone,  // 石材
    Wood,   // 木材
    Food    // 食物
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

// 单位受伤事件。由战斗系统在伤害结算后发布。
// ActualDamage 为扣除防御后的实际伤害值。
public readonly struct UnitDamagedEvent
{
    public readonly UnitController Unit;
    public readonly UnitController Source;
    public readonly int ActualDamage;

    public UnitDamagedEvent(UnitController unit, UnitController source, int actualDamage)
    {
        Unit = unit;
        Source = source;
        ActualDamage = actualDamage;
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