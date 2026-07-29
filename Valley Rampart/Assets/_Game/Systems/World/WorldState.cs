using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ============================================================================
//  多地图世界状态（3.2 第 6 节 + 第 7.4 节）
//  一个 World = 一局游戏，含多张 Map（玩家初始 + 敌方王国）
// ============================================================================

/// <summary>一张地图 = 一个岛屿/内陆。含 M 个大区块。</summary>
public class MapData
{
    public int mapId;               // 唯一 ID（0=玩家初始，1..N=敌方王国）
    public int seed;                // 该地图的生成种子
    public BigTerrain bigTerrain;    // 大地形（岛屿/内陆）
    public List<Region> regions;    // M 个大区块
    public bool isPlayerHome;        // 是否玩家初始地图
    public bool isConquered;         // 是否已被玩家占领
}

/// <summary>一个世界 = 一局游戏。含多张地图。</summary>
public class WorldState
{
    public int worldSeed;
    public WorldSize worldSize;
    public int difficulty;
    public int activeMapId;              // 当前所在地图
    public List<MapData> maps = new List<MapData>();
    public HashSet<int> conqueredMapIds = new HashSet<int>();

    /// <summary>当前活跃地图。</summary>
    public MapData ActiveMap => maps.FirstOrDefault(m => m.mapId == activeMapId);

    /// <summary>是否全部敌方王国已征服（通关判定）。</summary>
    public bool IsCleared => conqueredMapIds.Count >= maps.Count - 1; // 除玩家初始外全征服

    // ===== 调试导出（3.2.1 第 6.5 节 MapDebugData）=====

    /// <summary>导出当前活跃地图为调试 JSON（3.2.1 第 6.5 节格式）。</summary>
    public string ToDebugJson()
    {
        var map = ActiveMap;
        if (map == null) return "{}";

        var sb = new System.Text.StringBuilder();
        sb.Append("{");
        sb.Append("\"mapId\":").Append(map.mapId).Append(",");
        sb.Append("\"seed\":").Append(map.seed).Append(",");
        sb.Append("\"bigTerrain\":\"").Append(map.bigTerrain).Append("\",");
        sb.Append("\"isPlayerHome\":").Append(map.isPlayerHome ? "true" : "false").Append(",");
        sb.Append("\"regions\":[");
        for (int i = 0; i < map.regions.Count; i++)
        {
            var r = map.regions[i];
            if (i > 0) sb.Append(",");
            sb.Append("{");
            sb.Append("\"idx\":").Append(r.regionIndex).Append(",");
            sb.Append("\"terrain\":\"").Append(r.terrain).Append("\",");
            sb.Append("\"plainSubState\":\"").Append(r.plainSubState).Append("\",");
            sb.Append("\"zone\":\"").Append(r.zone).Append("\",");
            sb.Append("\"isInner\":").Append(r.isInner ? "true" : "false").Append(",");
            sb.Append("\"cellStartX\":").Append(r.cellStartX).Append(",");
            sb.Append("\"cellCount\":").Append(r.cellCount).Append(",");
            sb.Append("\"riftCellX\":").Append(r.riftCellX).Append(",");
            sb.Append("\"resources\":[");
            if (r.resources != null)
            {
                for (int j = 0; j < r.resources.Count; j++)
                {
                    if (j > 0) sb.Append(",");
                    var b = r.resources[j];
                    sb.Append("{");
                    sb.Append("\"type\":\"").Append(b.type).Append("\",");
                    sb.Append("\"category\":\"").Append(b.category).Append("\",");
                    sb.Append("\"localCellX\":").Append(b.localCellX).Append(",");
                    sb.Append("\"grade\":\"").Append(b.grade).Append("\",");
                    sb.Append("\"isConsumable\":").Append(b.isConsumable ? "true" : "false");
                    sb.Append("}");
                }
            }
            sb.Append("]");
            sb.Append("}");
        }
        sb.Append("]");
        sb.Append("}");
        return sb.ToString();
    }
}
