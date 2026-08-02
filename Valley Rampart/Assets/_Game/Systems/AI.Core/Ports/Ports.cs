// ============================================================================
//  AI.Core Ports - 时间/随机/世界查询/移动 端口（接缝 3/5 的解法）
//  详见 03_大脑提取与双适配工程.md §二 接口草案（照抄可用）
// ============================================================================

using System.Collections.Generic;

/// <summary>
/// 时钟端口（接缝 5：Time.time 直调 -> 注入）。
/// Unity 实现：Time.time；模拟器实现：tick × dt。
/// </summary>
public interface IClock
{
    /// <summary>当前时间（秒）</summary>
    float Now { get; }
}

/// <summary>
/// 随机数端口（RNG 注入，支持种子化）。
/// Unity 实现：UnityEngine.Random.Range；模拟器实现：种子 System.Random。
/// </summary>
public interface IRngPort
{
    /// <summary>返回 [min, max) 区间随机浮点</summary>
    float Range(float min, float max);
}

/// <summary>
/// 世界查询端口（接缝 3：单例直取 -> 注入）。
/// Unity 实现：GridSystem + LODSystem 包一层；模拟器实现：SimWorld。
/// </summary>
public interface IWorldQuery
{
    /// <summary>世界格大小（GridSystem.Config.cellSize）</summary>
    float CellSize { get; }

    /// <summary>指定位置的区块威胁热度（LODSystem.GetHeatAt）</summary>
    float GetHeatAt(Vector2X pos);

    /// <summary>查询最近战斗热点（LODSystem.TryGetNearestCombatHotspot）</summary>
    bool TryGetHotspot(Vector2X pos, float maxAge, out Vector2X hotspot);

    /// <summary>查询指定格内的单位（GridSystem 空间分区）</summary>
    void QueryUnitsInCell(int cx, int cy, List<IUnitHandle> results);
}

/// <summary>
/// 可移动对象端口（BehaviorExecutor 的移动出口）。
/// Unity 实现：UnitController；模拟器实现：SimUnit。
/// </summary>
public interface IMovable
{
    /// <summary>朝目标移动 speed×dt</summary>
    void MoveTowards(Vector2X dest, float speed, float dt);

    /// <summary>停止移动</summary>
    void Stop();
}
