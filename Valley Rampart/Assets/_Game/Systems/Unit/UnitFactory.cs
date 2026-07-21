using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单位工厂。预加载 Prefab 并按需实例化。
/// Prefab 存放在 Resources/UnitPrefabs/ 下，按 "{faction}_{occupation}" 命名。
/// </summary>
public class UnitFactory : Singleton<UnitFactory>, ISaveableSpawner
{
    public string SaveIdPrefix => "Unit_";

    private readonly Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();

    /// <summary>
    /// 同步预加载所有单位 Prefab。
    /// 在 GameBootstrap 初始化阶段调用一次。
    /// </summary>
    public void PreloadAll()
    {
        Debug.Log("[UnitFactory] 预加载单位 Prefab...");

        GameObject[] prefabs = Resources.LoadAll<GameObject>("UnitPrefabs");

        foreach (var prefab in prefabs)
        {
            if (prefab == null) continue;

            string key = prefab.name;

            if (!_prefabCache.ContainsKey(key))
            {
                _prefabCache.Add(key, prefab);
                Debug.Log($"[UnitFactory] 已缓存: {key}");
            }
        }

        Debug.Log($"[UnitFactory] 预加载完成，共 {_prefabCache.Count} 个 Prefab。");
    }

    /// <summary>
    /// 根据 UnitData 创建单位实例。
    /// </summary>
    public GameObject SpawnUnit(UnitData data, Vector2 position)
    {
        if (data == null)
        {
            Debug.LogError("[UnitFactory] UnitData 为空，无法创建单位。");
            return null;
        }

        string key = $"{data.faction}_{data.occupation}";

        if (!_prefabCache.TryGetValue(key, out var prefab))
        {
            Debug.LogError($"[UnitFactory] 找不到 Prefab: {key}。请确保 Resources/UnitPrefabs/{key}.prefab 存在。");
            return null;
        }

        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        instance.name = key;

        // 绑定数据到控制器
        var controller = instance.GetComponent<UnitController>();
        if (controller != null)
        {
            controller.Initialize(data);
        }

        return instance;
    }

    /// <summary>
    /// 按 Faction + Occupation 直接创建单位。
    /// </summary>
    public GameObject SpawnUnit(Faction faction, Occupation occupation, Vector2 position)
    {
        UnitData data = UnitDataManager.Instance.GetData(faction, occupation);
        return SpawnUnit(data, position);
    }

    // ===== ISaveableSpawner 实现 =====

    public void SpawnFromSave(ModuleSaveEntry entry)
    {
        if (entry.typeName != typeof(UnitSaveData).AssemblyQualifiedName) return;

        // R3: 去重检查——如果该 SaveId 已存在（可能是上次读档残留），跳过创建
        if (SaveManager.Instance.HasSaveable(entry.saveId))
        {
            Debug.LogWarning($"[UnitFactory] SaveId '{entry.saveId}' 已存在，跳过重复创建。");
            return;
        }

        var data = JsonUtility.FromJson<UnitSaveData>(entry.json);
        var faction = (Faction)data.faction;
        var occupation = (Occupation)data.occupation;

        UnitData config = UnitDataManager.Instance.GetData(faction, occupation);
        if (config == null)
        {
            Debug.LogError($"[UnitFactory] 找不到配置: {faction}_{occupation}，跳过。");
            return;
        }

        Vector2 pos = new Vector2(data.posX, data.posY);
        GameObject go = SpawnUnit(config, pos);  // 触发 Initialize → 注册 ISaveable（新 GUID）

        if (go != null)
        {
            var controller = go.GetComponent<UnitController>();
            controller.OverrideSaveId(entry.saveId);  // 覆盖为存档里的 SaveId
        }
    }
}
