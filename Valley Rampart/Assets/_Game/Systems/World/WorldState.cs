using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  世界状态单图化（改造计划 doc 1 §1.3 / §2.5 / §5.5）
//  单块大陆：WorldState 永远只有 1 张图；多图壳保留但不启用。
//  MapData 契约（2_1 §1.3）：features 唯一功能源 + climateZones 温度带 + naturalBuildings 视觉层。
// ============================================================================

/// <summary>敌人威胁来源点（doc 1 §5.5：2D 360° 来袭的静态刷点位）。</summary>
public struct SpawnDef
{
    public Vector2Int coord;         // 刷点格坐标
    public Vector2 direction;        // 威胁来袭方向（360° 归一化，格空间归一化 §1.6）
    public int strength;             // 波次规模（2_8 细化）
    public Faction faction;          // 阵营（玩家王国 / AI 王国【预留】 / 怪物）
}

/// <summary>一张地图 = 单块大陆。features 为唯一功能源（2_1 §1.3），terrain/walkFlags 由 GridSystem.PopulateFromMap 派生。</summary>
public class MapData
{
    public int mapId;                 // 冻结（恒 0）
    public int seed;
    public int width;                 // 格数
    public int height;
    public ClimateZone[] climateZones;        // 温度带，按大区块 16×16 存（长度 = width/16 × height/16）
    public FeatureType[] features;            // W×H，唯一功能源（可走/阻挡由此派生）
    public List<Vector2Int> kingdomSpawns;    // 王国出生点（0=玩家，1..N=AI 王国，2_1 生成）
    public List<SpawnDef>   threatSpawns;     // 敌人晚上刷点/威胁方向（2_1 写入、2_8 消费）
    public List<NaturalBuilding> naturalBuildings; // 自然建筑占位（features 派生的视觉层，供 2_2 实例化）
}

/// <summary>一个世界 = 一局游戏。单图。</summary>
public class WorldState
{
    public int worldSeed;
    public WorldSize worldSize;
    public int difficulty;
    public int activeMapId = 0;          // 恒 0（单图）
    public List<MapData> maps = new List<MapData>();
    public HashSet<int> conqueredMapIds = new HashSet<int>();  // 冻结（跨岛征服取消）

    /// <summary>当前活跃地图（单图）。</summary>
    public MapData ActiveMap => maps != null && activeMapId >= 0 && activeMapId < maps.Count ? maps[activeMapId] : null;

    /// <summary>单图无跨岛征服，恒 false。</summary>
    public bool IsCleared => false;
}