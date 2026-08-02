using UnityEngine;

// ============================================================================
//  3.0.1_LOD 性能架构 - 区块 LOD 状态
//  详见 3.0.1_LOD性能架构.md §1.1 / §1.4 / §3.1
//  region 大区块（16×16 cell）持有的运行时状态：level + threatHeat + idleTimer
// ============================================================================

/// <summary>区块 LOD 等级（§1.5 三区行为语义）。</summary>
public enum LodLevel
{
    Active,      // 活跃：10Hz 全强度思考
    SemiActive,  // 半活跃：2Hz 全量输入
    Sleeping     // 休眠：0.5Hz 砍谨慎态输入
}

/// <summary>
/// 单个 region 的 LOD 运行时状态（§1.1）。
/// 归属 LODSystem 管理，NPC 从所在 region 读 Think 频率。
/// </summary>
public class RegionLodState
{
    /// <summary>region 全局索引（0..M-1）</summary>
    public readonly int RegionIndex;

    public LodLevel Level = LodLevel.Sleeping;
    /// <summary>区块威胁热度 0-1（§3.1，clamp）</summary>
    public float ThreatHeat;
    /// <summary>无事件计时器（降级防抖，§1.4：热度归零且 30s 无事件 → 降一级）</summary>
    public float IdleTimer;

    /// <summary>最近战斗热点（受击位置，供同区 NPC 支援——§3.1 第二层"危险传开"的位置载体）</summary>
    public Vector2 CombatHotspot;
    /// <summary>热点时间戳（超过热点有效期后失效，NPC 不再朝旧热点移动）</summary>
    public float HotspotTime;

    public RegionLodState(int regionIndex)
    {
        RegionIndex = regionIndex;
    }
}
