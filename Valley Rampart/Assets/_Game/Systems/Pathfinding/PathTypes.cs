// ============================================================================
//  2_6 P0a 微格 A*：寻路结果类型（纯 C#，零 UnityEngine 依赖，确定性优先）。
//  坐标 = 微格 GridCoord（doc 1）。本 P0a 阶段放默认 Assembly-CSharp，
//  asmdef 隔离（Pathfinding.Core，sim 共用）归 2_6 确定性审计（步骤11/P2）。
// ============================================================================

/// <summary>路径求解状态。</summary>
public enum PathStatus
{
    Pending,       // 已请求未完成（未来异步用）
    Ready,         // 完整路径
    Partial,       // 展开数达上限截断（取已展开最优路径）
    Unreachable,   // 目标不可达（附最近可达点）
    Cancelled      // 已取消
}

/// <summary>寻路结果（池化候选；waypoints 为微格序列，含起点不含重复）。</summary>
public class PathResult
{
    public PathStatus status;
    public GridCoord[] waypoints;   // 微格序列（from → to；Partial/Unreachable 为截断/最近可达点前缀）
    public int version;             // 失效检测（网格变更计数）
    public bool reachedExactGoal;
}

/// <summary>异步票据（2_6 P0b 服务化；P0a FindPathImmediate 同步不出票）。</summary>
public struct PathTicket
{
    public int id;
    public byte priority;
    public bool HasResult;      // 2_6 P0b：已完成（Result 有效；false=未入队/未就绪）
    public PathResult Result;   // 2_6 P0b：求解结果（失败/越界=null）
}