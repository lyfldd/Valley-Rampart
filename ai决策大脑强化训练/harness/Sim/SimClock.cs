// ============================================================================
//  M2 Headless 模拟器 - SimClock 模拟时钟（IClock 端口实现）
//  04_模拟器规格.md §一：SimClock：tick=0.1s 步进；IClock.Now = tick×0.1。
//  确定性：Now 用 tickCount × tickInterval 乘法（整数×常量，无 float 累加舍入累积），
//  与 04 §七 确定性要求一致。
//  对应 Unity 侧：Time.time（NPCBrain/DamageSystem 全链路的时间来源）。
// ============================================================================

/// <summary>
/// 模拟时钟。每 Step() 前进一个 tick（0.1s）。
/// 决策核（IClock.Now）与伤害时间轮共用同一时钟，保证 tick 内 Now 一致。
/// </summary>
public sealed class SimClock : IClock
{
    private readonly float _tickInterval;
    private long _tickCount;

    public SimClock(float tickInterval)
    {
        _tickInterval = tickInterval > 0f ? tickInterval : 0.1f;
    }

    /// <summary>IClock 端口：当前时间（秒）= tick × tickInterval（04 §一）</summary>
    public float Now => (float)(_tickCount * _tickInterval);

    /// <summary>当前 tick 序号（0 起）。</summary>
    public long TickCount => _tickCount;

    /// <summary>步进一个 tick。</summary>
    public void Step() => _tickCount++;
}
