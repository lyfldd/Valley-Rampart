using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图可视化器。
/// 把 MapData 转成 2D Sprite 可视化（不依赖美术资源，程序化生成 Texture2D）。
///
/// 所有对象都直接挂在 MapVisualization 根节点下（避免 scale 继承问题）。
/// 用名字前缀 R0_/R1_/... 标识归属哪个大区块。
///
/// 层级结构：
///   MapVisualization
///     ├── Baseline_y=-3 (sortingOrder=2)
///     ├── R0_Region_Coast_LeftExtreme (sortingOrder=0, 最底层)
///     ├── R1_Region_Hills_LeftExtreme (sortingOrder=0)
///     ├── R0_Building_Mine (sortingOrder=1, 在 Region 上面)
///     ├── R0_Rift (sortingOrder=1)
///     └── ...
///
/// 大区块尺寸 = 参考图大小（约 36×12 世界单位）。
/// </summary>
public class MapVisualizer : MonoBehaviour
{
    [SerializeField] private bool visualizeOnGenerate = true;

    private Transform _root;
    private Texture2D _whiteTex;  // 缓存白色纹理

    private void OnEnable()
    {
        EventBus.Subscribe<MapGeneratedEvent>(OnMapGenerated);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<MapGeneratedEvent>(OnMapGenerated);
    }

    private void OnMapGenerated(MapGeneratedEvent evt)
    {
        if (!visualizeOnGenerate) return;
        Visualize();
    }

    public void Visualize()
    {
        // Play 模式不画建筑标记（BuildingFactory 已创建真实建筑）；非 Play 画（无 BuildingFactory）
        Visualize(WorldManager.Instance?.ActiveMap, !Application.isPlaying);
    }

    /// <summary>可视化指定 MapData（编辑模式预览用，默认画建筑标记）。</summary>
    public void Visualize(MapData map)
    {
        Visualize(map, true);
    }

    /// <summary>可视化指定 MapData。showBuildingMarkers=false 时只画底图，不画建筑标记（Play 模式避免和 BuildingFactory 重复）。</summary>
    public void Visualize(MapData map, bool showBuildingMarkers)
    {
        if (map == null)
        {
            Debug.LogWarning("[MapVisualizer] 无活跃地图，无法可视化");
            return;
        }

        ClearVisualization();

        var rootGo = new GameObject("MapVisualization");
        rootGo.transform.SetParent(transform);
        _root = rootGo.transform;

        var gridConfig = Resources.Load<GridConfig>("Grid/GridConfig");
        float cellSize = gridConfig != null ? gridConfig.cellSize : 2.26f;
        int rpc = gridConfig != null ? gridConfig.regionCellCount : 16;

        int M = map.regions.Count;

        // 算法决定尺寸：大区块 = cellSize × regionCellCount
        float regionWidth = cellSize * rpc;  // 2.26 × 16 = 36.16
        float regionHeight = 12.17f;  // 固定高度（匹配参考图比例）

        // 缓存白色纹理（Building 用）
        _whiteTex = CreateColoredTexture(Color.white, 1, 1);

        // === 基准线 y = -3 (sortingOrder=2, 最顶层) ===
        CreateBaseline(M, regionWidth);

        // === 每个大区块 ===
        for (int i = 0; i < M; i++)
        {
            var region = map.regions[i];
            float startX = i * regionWidth;  // 用参考图宽度作为大区块宽度
            float endX = startX + regionWidth;
            string prefix = $"R{i}_";

            // 大区块底板 (sortingOrder=0, 最底层)
            CreateSpriteObj(_root, prefix + $"Region_{region.terrain}_{region.zone}",
                (startX + endX) / 2f, 0, 0,
                regionWidth, regionHeight,
                GetTerrainColor(region.terrain, region.plainSubState),
                sortingOrder: 0);

            // 小区块分隔线 (16条对齐, sortingOrder=2 在最上层)
            float cellWidth = regionWidth / rpc;
            for (int j = 0; j < rpc; j++)
            {
                float lineX = startX + j * cellWidth;
                CreateSpriteObj(_root, prefix + $"CellLine_{j}",
                    lineX, 0, 0.05f,
                    0.08f, regionHeight,
                    new Color(1f, 1f, 1f, 0.25f),
                    sortingOrder: 2);
            }

            // 资源点（按类型上色, sortingOrder=1, 在 Region 上面）
            // 跳过 CastleCore（由下方废弃城堡专用代码绘制）。Play 模式不画（BuildingFactory 已创建真实建筑）
            if (showBuildingMarkers && region.resources != null)
            {
                foreach (var b in region.resources)
                {
                    if (b.category == BuildingCategory.CastleCore) continue;

                    float mappedWx = startX + (b.localCellX + 0.5f) / rpc * regionWidth;
                    Color color = GetBuildingColor(b);
                    float size = GetBuildingSize(b);
                    CreateSpriteObj(_root, prefix + $"Building_{b.type}_{b.category}",
                        mappedWx, 0, 0.1f,
                        size, size,
                        color,
                        sortingOrder: 1);
                }
            }

            // 裂隙（红色, sortingOrder=1）。Play 模式不画（BuildingFactory 已创建真实 Rift）
            if (showBuildingMarkers && region.riftCellX >= 0)
            {
                float mappedWx = startX + (region.riftCellX + 0.5f) / rpc * regionWidth;
                CreateSpriteObj(_root, prefix + "Rift",
                    mappedWx, 0, 0.2f,
                    2f, 3f,
                    Color.red,
                    sortingOrder: 1);
            }

            // 废弃城堡（从 map data 读取，两格占位，灰色，sortingOrder=1）。Play 模式不画（BuildingFactory 已创建真实 CastleCore）
            if (showBuildingMarkers)
            foreach (var bp in region.resources)
            {
                if (bp.category == BuildingCategory.CastleCore)
                {
                    float cellW = regionWidth / rpc;
                    float castleW = cellW * bp.cellWidth;  // 占 cellWidth 格
                    float castleH = regionHeight * 0.6f;
                    // 居中：2 格城堡的中心 = 起始格 + 1（即两格中间）
                    float mappedWx = startX + (bp.localCellX + bp.cellWidth / 2f) * cellW;
                    CreateSpriteObj(_root, prefix + "AbandonedCastle",
                        mappedWx, 0f, 0.2f,
                        castleW, castleH,
                        new Color(0.5f, 0.5f, 0.5f),  // 灰色
                        sortingOrder: 1);
                }
            }
        }

        Debug.Log($"[MapVisualizer] 可视化完成: {M} 个大区块, " +
                  $"资源点 {CountResources(map)} 个, " +
                  $"裂隙 {CountRifts(map)} 个, " +
                  $"regionSize={regionWidth}x{regionHeight}");
    }

    /// <summary>创建一个 Sprite 对象，直接挂在 parent 下，世界坐标定位。</summary>
    /// <remarks>2026-07-29 修复：使用 1×1 像素纹理，确保 scale = 实际世界单位尺寸</remarks>
    private void CreateSpriteObj(Transform parent, string name,
        float x, float y, float z,
        float width, float height, Color color,
        int sortingOrder = 1)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = new Vector3(x, y, z);
        go.transform.localScale = new Vector3(width, height, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        // 使用 1×1 像素纹理，pixelsPerUnit=1，这样 scale 就是实际世界单位尺寸
        var tex = CreateColoredTexture(color, 1, 1);
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        sr.sortingOrder = sortingOrder;
        sr.drawMode = SpriteDrawMode.Simple;
    }

    private Sprite CreateColoredSprite(Color color, int w, int h)
    {
        // 使用 1×1 像素，pixelsPerUnit=1，这样 1 单位 scale = 1 世界单位
        return Sprite.Create(CreateColoredTexture(color, 1, 1),
            new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    private Texture2D CreateColoredTexture(Color color, int w, int h)
    {
        // 白色纹理复用
        if (color == Color.white && _whiteTex != null)
            return _whiteTex;

        var tex = new Texture2D(w, h);
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return tex;
    }

    private void CreateBaseline(int regionCount, float regionWidth)
    {
        float totalWidth = regionCount * regionWidth;
        CreateSpriteObj(_root, "Baseline_y=-3",
            totalWidth / 2f, -3f, 0.5f,
            totalWidth, 0.05f,
            new Color(1f, 1f, 0f),
            sortingOrder: 2);
    }

    public void ClearVisualization()
    {
        if (_root != null)
        {
            if (Application.isPlaying)
                Destroy(_root.gameObject);
            else
                DestroyImmediate(_root.gameObject);
            _root = null;
        }
        _whiteTex = null;
    }

    public void ExportDebugJson()
    {
        var world = WorldManager.Instance?.World;
        if (world == null) return;

        string json = world.ToDebugJson();
        string path = $"Assets/Resources/Debug/Maps/map_{world.activeMapId}_seed{world.worldSeed}.json";
        System.IO.File.WriteAllText(path, json);
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
        Debug.Log($"[MapVisualizer] 调试 JSON 导出: {path}");
    }

    // ===== 辅助 =====

    int CountResources(MapData map)
    {
        int c = 0;
        foreach (var r in map.regions)
            if (r.resources != null) c += r.resources.Count;
        return c;
    }

    int CountRifts(MapData map)
    {
        int c = 0;
        foreach (var r in map.regions)
            if (r.riftCellX >= 0) c++;
        return c;
    }

    Color GetTerrainColor(TerrainType terrain, PlainSubState subState)
    {
        switch (terrain)
        {
            case TerrainType.Plain:
                return subState == PlainSubState.Fertile
                    ? new Color(0.6f, 0.8f, 0.3f)
                    : new Color(0.5f, 0.6f, 0.4f);
            case TerrainType.Forest: return new Color(0.2f, 0.5f, 0.2f);
            case TerrainType.Quarry: return new Color(0.5f, 0.4f, 0.5f);
            case TerrainType.Hills:  return new Color(0.5f, 0.35f, 0.2f);
            case TerrainType.Coast:  return new Color(0.3f, 0.5f, 0.7f);
            case TerrainType.Snow:   return new Color(0.7f, 0.75f, 0.8f);
            case TerrainType.Wasteland: return new Color(0.7f, 0.6f, 0.3f);
            default: return Color.gray;
        }
    }

    /// <summary>Building 类型 → 可视化颜色。</summary>
    Color GetBuildingColor(BuildingPlaceholder b)
    {
        switch (b.category)
        {
            case BuildingCategory.ResourceProducer:
                // 持续性资源：按类型上色
                switch (b.type)
                {
                    case BuildingType.Tree: return new Color(0.3f, 0.7f, 0.3f);     // 绿色（树）
                    case BuildingType.Mine: return new Color(0.6f, 0.6f, 0.6f);     // 灰色（矿洞）
                    case BuildingType.Farmland: return new Color(0.8f, 0.8f, 0.3f); // 黄色（农田）
                    default: return Color.green;
                }
            case BuildingCategory.ResourcePickup:
                return Color.white;  // 一次性资源：白色
            case BuildingCategory.SpecialPoint:
                return new Color(1f, 0.84f, 0f);  // 金色（宝箱/遗迹）
            default:
                return Color.white;
        }
    }

    /// <summary>Building 类型 → 可视化尺寸。</summary>
    float GetBuildingSize(BuildingPlaceholder b)
    {
        switch (b.category)
        {
            case BuildingCategory.ResourceProducer: return 2f;      // 持续性资源：稍大
            case BuildingCategory.ResourcePickup:   return 1.2f;    // 一次性资源：小
            case BuildingCategory.SpecialPoint:     return 1.5f;    // 特殊点：中等
            default: return 1.2f;
        }
    }
}
