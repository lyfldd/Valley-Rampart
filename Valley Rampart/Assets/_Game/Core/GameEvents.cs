using UnityEngine;

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

public readonly struct UnitDiedEvent
{
    public readonly UnitController Unit;

    public UnitDiedEvent(UnitController unit)
    {
        Unit = unit;
    }
}

public enum ResourceType
{
    Gold,
    Stone,
    Wood,
    Food
}

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

// ===== 单位生成事件 =====
// UnitController.Initialize 完成时发布。UI/仇恨/存档系统可据此把握单位就绪时机。

public readonly struct UnitSpawnedEvent
{
    public readonly UnitController Unit;

    public UnitSpawnedEvent(UnitController unit)
    {
        Unit = unit;
    }
}

// ===== 单位血量变化事件 =====
// 受伤与治疗统一走此事件，血条 UI 据此刷新，无需每帧轮询。

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

// ===== 单位属性变化事件 =====
// Buff/装备/升级系统修改属性后发布，UI 据此刷新攻击/防御/血量上限等显示。

public enum UnitAttributeType
{
    MaxHp,
    Attack,
    Defense,
    WalkSpeed,
    RunSpeed
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

// ===== 时间系统 =====

/// <summary>时段（昼夜划分）。</summary>
public enum TimePhase
{
    Night,  // 夜晚
    Dawn,   // 黎明
    Day,    // 白天
    Dusk    // 黄昏
}

/// <summary>季节。影响昼夜比例（夏白天长，冬白天短）。</summary>
public enum Season
{
    Spring, // 春
    Summer, // 夏
    Autumn, // 秋
    Winter  // 冬
}

/// <summary>天数变化事件。新一天开始时发布。</summary>
public readonly struct TimeDayChangedEvent
{
    public readonly int OldDay;
    public readonly int NewDay;
    public readonly Season Season;     // 新一天对应的季节（可能已跨季）

    public TimeDayChangedEvent(int oldDay, int newDay, Season season)
    {
        OldDay = oldDay;
        NewDay = newDay;
        Season = season;
    }
}

/// <summary>时段变化事件。白天↔夜晚等切换时发布，影响敌人刷新/光照等。</summary>
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

/// <summary>季节变化事件。昼夜比例随之改变。</summary>
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

// ===== 存档系统事件 =====

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
