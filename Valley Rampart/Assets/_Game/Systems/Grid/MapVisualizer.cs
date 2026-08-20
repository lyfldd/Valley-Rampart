using UnityEngine;

/// <summary>
/// 地图可视化器（改造计划 doc 1 §2.6：2D 版，正式渲染归 2_10，本片为调试底图）。
/// 把 MapData.features（唯一功能源）逐格取色画成一张 W×H 像素 Texture2D，
/// 铺满 GridSystem 坐标范围（中心原点），不依赖美术资源。
/// </summary>
public class MapVisualizer : MonoBehaviour
{
    [SerializeField] private bool visualizeOnGenerate = true;

    private GameObject _quad;

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
        Visualize(WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null);
    }

    /// <summary>可视化指定 MapData（features 色块底图）。</summary>
    public void Visualize(MapData map)
    {
        if (map == null || map.features == null || map.width <= 0 || map.height <= 0)
        {
            Debug.LogWarning("[MapVisualizer] 无有效地图数据，无法可视化");
            return;
        }
        var grid = GridSystem.Instance;
        if (grid == null) return;

        ClearVisualization();

        // W×H 像素纹理：逐格按特征物取色（features 唯一功能源）
        var tex = new Texture2D(map.width, map.height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var pixels = new Color[map.width * map.height];
        for (int y = 0; y < map.height; y++)
        {
            for (int x = 0; x < map.width; x++)
            {
                int i = y * map.width + x;
                pixels[i] = GetFeatureColor(map.features[i]);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        // 铺满整图的四边形（中心原点：角点由 CoordToWorld 推）
        Vector2 min = grid.CoordToWorld(new GridCoord(0, 0));
        Vector2 max = grid.CoordToWorld(new GridCoord(map.width - 1, map.height - 1));
        var cellSz = grid.Config != null ? grid.Config.cellSize : Vector2.one;
        Vector2 center = new Vector2((min.x + max.x) / 2f, (min.y + max.y) / 2f);
        Vector2 size = new Vector2(map.width * cellSz.x, map.height * cellSz.y);

        _quad = new GameObject("MapVisualization");
        _quad.transform.SetParent(transform);
        _quad.transform.localPosition = new Vector3(center.x, center.y, 0f);
        _quad.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = _quad.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, map.width, map.height), new Vector2(0.5f, 0.5f), 1f);
        sr.sortingOrder = -100;  // 底图最底层

        Debug.Log($"[MapVisualizer] 2D 可视化完成: {map.width}x{map.height}");
    }

    public void ClearVisualization()
    {
        if (_quad != null)
        {
            if (Application.isPlaying) Destroy(_quad);
            else DestroyImmediate(_quad);
            _quad = null;
        }
    }

    Color GetFeatureColor(FeatureType feature)
    {
        switch (feature)
        {
            case FeatureType.Plain: return new Color(0.5f, 0.6f, 0.4f);
            case FeatureType.Tree: return new Color(0.2f, 0.5f, 0.2f);
            case FeatureType.Mountain: return new Color(0.45f, 0.4f, 0.35f);
            case FeatureType.SnowMountain: return new Color(0.85f, 0.88f, 0.92f);
            case FeatureType.Mine: return new Color(0.5f, 0.4f, 0.5f);
            case FeatureType.OreVein: return new Color(0.6f, 0.5f, 0.45f);
            case FeatureType.StonePile: return new Color(0.55f, 0.55f, 0.55f);
            case FeatureType.WoodPile: return new Color(0.5f, 0.4f, 0.25f);
            case FeatureType.River: return new Color(0.25f, 0.5f, 0.8f);
            case FeatureType.Lake: return new Color(0.2f, 0.45f, 0.75f);
            case FeatureType.Ocean: return new Color(0.1f, 0.3f, 0.65f);
            default: return Color.gray;
        }
    }
}
