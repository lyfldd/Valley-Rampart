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
        var map = WorldManager.Instance?.ActiveMap;
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
        float cellSize = gridConfig != null ? gridConfig.cellSize : 4f;
        int rpc = gridConfig != null ? gridConfig.regionCellCount : 16;

        int M = map.regions.Count;

        // 获取参考图尺寸（作为大区块尺寸）
        float regionWidth, regionHeight;
        var refObj = GameObject.Find("参考图");
        if (refObj != null)
        {
            var refSr = refObj.GetComponent<SpriteRenderer>();
            if (refSr != null && refSr.sprite != null)
            {
                regionWidth = refSr.sprite.rect.width / refSr.sprite.pixelsPerUnit;
                regionHeight = refSr.sprite.rect.height / refSr.sprite.pixelsPerUnit;
            }
            else
            {
                regionWidth = 36.16f;
                regionHeight = 12.17f;
            }
        }
        else
        {
            regionWidth = 36.16f;
            regionHeight = 12.17f;
        }

        // 缓存白色纹理（Building 用）
        _whiteTex = CreateColoredTexture(Color.white, 1, 1);

        // === 基准线 y = -3 (sortingOrder=2, 最顶层) ===
        CreateBaseline(M, rpc, cellSize, regionWidth);

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

            // 资源点（白色小方块, sortingOrder=1, 在 Region 上面）
            if (region.resources != null)
            {
                foreach (var b in region.resources)
                {
                    float wx = (region.cellStartX + b.localCellX + 0.5f) * cellSize;
                    // 映射到新的宽度比例
                    float mappedWx = startX + (b.localCellX + 0.5f) / rpc * regionWidth;
                    CreateSpriteObj(_root, prefix + $"Building_{b.type}",
                        mappedWx, 0, 0.1f,
                        1.5f, 1.5f,
                        Color.white,
                        sortingOrder: 1);
                }
            }

            // 裂隙（红色, sortingOrder=1）
            if (region.riftCellX >= 0)
            {
                float mappedWx = startX + (region.riftCellX + 0.5f) / rpc * regionWidth;
                CreateSpriteObj(_root, prefix + "Rift",
                    mappedWx, 0, 0.2f,
                    2f, 3f,
                    Color.red,
                    sortingOrder: 1);
            }

            // 主城标记（金色, sortingOrder=1）
            if (region.zone == MapZone.Center)
            {
                var (center, extreme, resource) = WorldManager.Instance == null
                    ? (3, 1, 1)
                    : CalcZoneCountsProxy(M);

                int midIdx = extreme + resource + center / 2;
                if (i == midIdx || i == midIdx - 1)
                {
                    float mappedWx = startX + regionWidth / 2f;
                    CreateSpriteObj(_root, prefix + "CastleCore",
                        mappedWx, 2f, 0.2f,
                        3f, 4f,
                        new Color(1f, 0.84f, 0f),
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

    private void CreateBaseline(int regionCount, int regionCellCount, float cellSize, float regionWidth)
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

    (int, int, int) CalcZoneCountsProxy(int M)
    {
        int center = Mathf.Max(2, M / 3);
        if (center % 2 != 0) center++;
        int extreme = Mathf.Max(1, (M - center) / 4);
        int resource = (M - center - extreme * 2) / 2;
        if (resource < 1) resource = 1;
        return (center, extreme, resource);
    }

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
}
