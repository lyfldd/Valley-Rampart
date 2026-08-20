using UnityEngine;

/// <summary>
/// 地图生成调试绘制（2_1 步骤 12，Gizmos 版，不依赖美术）。
/// 画温度带大区块色块（§5.4 配色）+ 出生点金块 + 威胁刷点红块。
/// 挂到场景任意 GameObject；正式渲染归 2_10。
/// </summary>
public class MapGenDebugDrawer : MonoBehaviour
{
    [SerializeField] private bool drawClimateZones = true;
    [SerializeField] private bool drawSpawns = true;
    [SerializeField] private bool drawThreats = true;
    [SerializeField] private float spawnGizmoSize = 2.5f;

    private void OnDrawGizmos()
    {
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        var grid = GridSystem.Instance;
        if (map == null || grid == null || grid.Config == null) return;

        if (drawClimateZones) DrawClimateZones(map, grid);
        if (drawSpawns) DrawPoints(map, grid, map.kingdomSpawns, new Color(1f, 0.84f, 0f, 0.9f), spawnGizmoSize);
        if (drawThreats && map.threatSpawns != null)
        {
            for (int i = 0; i < map.threatSpawns.Count; i++)
            {
                var c = new GridCoord(map.threatSpawns[i].coord.x, map.threatSpawns[i].coord.y);
                Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.9f);
                Gizmos.DrawCube(grid.CoordToWorld(c), new Vector3(spawnGizmoSize * 0.8f, spawnGizmoSize * 0.8f, 0.1f));
            }
        }
    }

    void DrawClimateZones(MapData map, GridSystem grid)
    {
        int cw = MapGenRules.ChunkW(map);
        int ch = Mathf.Max(1, map.height / MapGenRules.ChunkSize);
        float chunkW = grid.Config.cellSize.x * MapGenRules.ChunkSize;
        float chunkH = grid.Config.cellSize.y * MapGenRules.ChunkSize;

        for (int cy = 0; cy < ch; cy++)
            for (int cx = 0; cx < cw; cx++)
            {
                var zone = map.climateZones[cx + cy * cw];
                int cellX = cx * MapGenRules.ChunkSize + MapGenRules.ChunkSize / 2;
                int cellY = cy * MapGenRules.ChunkSize + MapGenRules.ChunkSize / 2;
                if (cellX >= map.width) cellX = map.width - 1;
                if (cellY >= map.height) cellY = map.height - 1;
                Gizmos.color = ClimateColor(zone);
                Gizmos.DrawCube(grid.CoordToWorld(new GridCoord(cellX, cellY)), new Vector3(chunkW, chunkH, 0.02f));
            }
    }

    void DrawPoints(MapData map, GridSystem grid, System.Collections.Generic.List<Vector2Int> pts, Color color, float size)
    {
        if (pts == null) return;
        Gizmos.color = color;
        for (int i = 0; i < pts.Count; i++)
        {
            var c = new GridCoord(pts[i].x, pts[i].y);
            Gizmos.DrawCube(grid.CoordToWorld(c), new Vector3(size, size, 0.1f));
        }
    }

    /// <summary>温度带配色（2_1 §5.4）。</summary>
    Color ClimateColor(ClimateZone zone)
    {
        switch (zone)
        {
            case ClimateZone.Tropical: return new Color(0.85f, 0.8f, 0.3f, 0.35f);     // 暖黄绿
            case ClimateZone.Subtropical: return new Color(0.4f, 0.75f, 0.3f, 0.35f);  // 绿
            case ClimateZone.Temperate: return new Color(0.7f, 0.65f, 0.35f, 0.35f);   // 黄绿/褐
            case ClimateZone.Cold: return new Color(0.6f, 0.75f, 0.9f, 0.35f);         // 蓝白
            default: return Color.gray;
        }
    }
}
