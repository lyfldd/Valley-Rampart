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

        // 从 GridSystem 获取配置（若不可用用默认值）
        float cellSize = 1f;
        int cellsPerRegion = 16;
        if (GridSystem.Instance != null && GridSystem.Instance.Config != null)
        {
            cellSize = GridSystem.Instance.Config.cellSize;
            cellsPerRegion = GridSystem.Instance.Config.regionCellCount;
        }

        // 构建 MapDebugData
        var debug = new MapDebugData
        {
            mapId = map.mapId,
            seed = map.seed,
            bigTerrain = map.bigTerrain.ToString(),
            worldSize = worldSize.ToString(),
            difficulty = difficulty,
            regionCount = map.regions.Count,
            cellSize = cellSize,
            cellsPerRegion = cellsPerRegion,
            regions = new RegionDebugEntry[map.regions.Count]
        };

        for (int i = 0; i < map.regions.Count; i++)
        {
            var r = map.regions[i];
            var regionDebug = new RegionDebugEntry
            {
                idx = r.regionIndex,
                terrain = r.terrain.ToString(),
                plainSubState = r.plainSubState.ToString(),
                zone = r.zone.ToString(),
                isInner = r.isInner,
                cellStartX = r.cellStartX,
                cellCount = r.cellCount,
                riftCellX = r.riftCellX,
                cells = new CellDebugEntry[r.cellCount]
            };

            // 逐 cell 列出 buildings（含跨格建筑如 CastleCore）
            for (int c = 0; c < r.cellCount; c++)
            {
                var cellDebug = new CellDebugEntry { localX = c };
                var buildingList = new List<BuildingDebugInfo>();

                if (r.resources != null)
                {
                    foreach (var b in r.resources)
                    {
                        int width = b.cellWidth > 0 ? b.cellWidth : 1;
                        if (c >= b.localCellX && c < b.localCellX + width)
                        {
                            buildingList.Add(new BuildingDebugInfo
                            {
                                type = b.type.ToString(),
                                category = b.category.ToString(),
                                grade = b.grade.ToString(),
                                localCellX = b.localCellX,
                                cellWidth = b.cellWidth
                            });
                        }
                    }
                }

                cellDebug.buildings = buildingList.ToArray();
                regionDebug.cells[c] = cellDebug;
            }

            debug.regions[i] = regionDebug;
        }

        return JsonUtility.ToJson(debug, prettyPrint: true);
    }
}

// ============================================================================
//  调试导出序列化类（3.2.1 第 6.5 节）
// ============================================================================

[System.Serializable]
public class MapDebugData
{
    public int mapId;
    public int seed;
    public string bigTerrain;
    public string worldSize;
    public int difficulty;
    public int regionCount;
    public float cellSize;
    public int cellsPerRegion;
    public RegionDebugEntry[] regions;
}

[System.Serializable]
public class RegionDebugEntry
{
    public int idx;
    public string terrain;
    public string plainSubState;
    public string zone;
    public bool isInner;
    public int cellStartX;
    public int cellCount;
    public int riftCellX;
    public CellDebugEntry[] cells;
}

[System.Serializable]
public class CellDebugEntry
{
    public int localX;
    public BuildingDebugInfo[] buildings;
}

[System.Serializable]
public class BuildingDebugInfo
{
    public string type;
    public string category;
    public string grade;
    public int localCellX;
    public int cellWidth;
}
