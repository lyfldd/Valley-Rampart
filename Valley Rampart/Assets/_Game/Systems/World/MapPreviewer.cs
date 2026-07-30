using UnityEngine;

/// <summary>
/// 地图预览器（3.3.4）。[ExecuteInEditMode] 组件，拖入场景即可在非 Play 模式生成+可视化地图。
/// Inspector 设置 seed/size/difficulty，右键"生成地图预览"或勾选 autoGenerate 自动刷新。
///
/// 需场景含 WorldManager + MapVisualizer（GameScene 已有）。
/// 只生成 MapData + 可视化，不实例化 Building（编辑模式安全）。
/// </summary>
[ExecuteInEditMode]
public class MapPreviewer : MonoBehaviour
{
    [Header("地图参数")]
    public int seed = 12345;
    public WorldSize size = WorldSize.Medium;
    public int difficulty = 1;

    [Header("存档加载")]
    [Tooltip("存档槽 ID（如 slot_1）。从存档读取 seed/size/difficulty 预览地图")]
    public string slotId = "slot_1";

    [Header("自动生成")]
    [Tooltip("勾选后改参数自动重新生成（可能卡顿，大地图慎用）")]
    public bool autoGenerate = false;

    /// <summary>生成地图预览（ContextMenu + 其他脚本可调）。</summary>
    [ContextMenu("生成地图预览")]
    public void GeneratePreview()
    {
        var wm = FindObjectOfType<WorldManager>();
        if (wm == null)
        {
            Debug.LogWarning("[MapPreviewer] 场景里没有 WorldManager，无法生成");
            return;
        }

        wm.GenerateMapForPreview(seed, size, difficulty);

        // 编辑模式手动触发可视化（事件订阅可能不生效，且 Instance 可能指向旧对象）
        var mv = FindObjectOfType<MapVisualizer>();
        if (mv != null) mv.Visualize(wm.ActiveMap);  // 直接传 map，不依赖 WorldManager.Instance
        else Debug.LogWarning("[MapPreviewer] 场景里没有 MapVisualizer");

        Debug.Log($"[MapPreviewer] 地图已生成: seed={seed}, size={size}, difficulty={difficulty}");
    }

    /// <summary>清除预览（删除 MapVisualization 根节点）。</summary>
    [ContextMenu("清除预览")]
    public void ClearPreview()
    {
        var mv = FindObjectOfType<MapVisualizer>();
        if (mv != null) mv.ClearVisualization();
    }

    /// <summary>从存档加载地图预览（读 WorldSaveData 的 seed/size/difficulty，3.3.4）。</summary>
    [ContextMenu("从存档加载预览")]
    public void LoadFromSave()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, "Saves", slotId + ".json");
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning($"[MapPreviewer] 存档不存在: {path}");
            return;
        }
        try
        {
            string json = System.IO.File.ReadAllText(path);
            var root = JsonUtility.FromJson<GameSaveRoot>(json);

            // 找 WorldManager 模块
            ModuleSaveEntry worldEntry = null;
            foreach (var m in root.modules)
                if (m.saveId == "WorldManager") { worldEntry = m; break; }
            if (worldEntry == null)
            {
                Debug.LogWarning("[MapPreviewer] 存档无 WorldManager 模块");
                return;
            }

            var data = JsonUtility.FromJson<WorldSaveData>(worldEntry.json);
            seed = data.worldSeed != 0 ? data.worldSeed : data.mapSeed;
            size = data.worldSize > 0 ? (WorldSize)data.worldSize : WorldSize.Medium;
            difficulty = data.difficulty > 0 ? data.difficulty : 2;

            Debug.Log($"[MapPreviewer] 从存档 {slotId} 读取: seed={seed}, size={size}, difficulty={difficulty}，时间={root.saveTime}");
            GeneratePreview();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MapPreviewer] 读存档失败: {e}");
        }
    }

    private void OnValidate()
    {
        if (autoGenerate) GeneratePreview();
    }
}
