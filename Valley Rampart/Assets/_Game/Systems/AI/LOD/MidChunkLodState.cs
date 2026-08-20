using UnityEngine;

// ============================================================================
//  2_4 LOD 区块划分 - 中区块 LOD 状态（替代旧 RegionLodState / MidRegionHeat）
//  中区块 = 4×4 小区块（doc 1 §1.2，GridSystem.CellToMidChunk → Vector2Int）。
//  稀疏存储（D80）：LODSystem 只登记"活跃带附近 + 有热度"的中区块，其余默认休眠不落状态。
//  热度/战斗热点并入本类（热/热点按中区块聚合，跨编队可见）。
// ============================================================================

/// <summary>区块 LOD 等级（2D 中区块粒度；旧 Sleeping 改名 Dormant）。</summary>
public enum LodLevel
{
    Active,      // 活跃：10Hz 全强度思考
    SemiActive,  // 半活跃：2Hz 全量输入
    Dormant      // 休眠：0.5Hz 砍谨慎态输入（移动仍每帧，D79）
}

/// <summary>
/// 单个中区块的 LOD + 热度运行时状态（2_4 §5.1）。
/// 归属 LODSystem 管理，NPC 从所在中区块读 Think 频率 / 威胁热度 / 战斗热点。
/// </summary>
public class MidChunkLodState
{
    /// <summary>中区块坐标 (x/4, y/4)，GridSystem.CellToMidChunk 产出</summary>
    public readonly Vector2Int midChunk;

    public LodLevel Level = LodLevel.Dormant;
    /// <summary>无事件计时器（降档迟滞，demoteDelaySeconds 用）</summary>
    public float idleTimer;
    /// <summary>块威胁热度 0..heatMax（clamp）</summary>
    public float threatHeat;
    /// <summary>最近战斗热点（受击位置，供同中区块/邻块 NPC 支援）</summary>
    public Vector2 combatHotspot;
    /// <summary>热点时间戳（超过有效期后失效，不再朝旧热点移动）</summary>
    public float hotspotTime;
    /// <summary>最后活动 tick（记录活跃时间戳，可作调试/统计）</summary>
    public long lastActivityTick;

    public MidChunkLodState(Vector2Int midChunk)
    {
        this.midChunk = midChunk;
    }
}