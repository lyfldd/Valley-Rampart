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
    private bool _isPreloaded = false;

    // 3.0.1 §7.4 对象池：实例层（按 prefab 分桶），门面挂在 UnitFactory 现有生成路径
    private readonly UnitInstancePool _instancePool = new UnitInstancePool();

    /// <summary>实例池（供外部统计/调试）。</summary>
    public UnitInstancePool InstancePool => _instancePool;

    /// <summary>
    /// 同步预加载所有单位 Prefab。幂等：重复调用只加载一次。
    /// 由 LoadManager 阶段1 显式调用。
    /// </summary>
    public void PreloadAll()
    {
        if (_isPreloaded)
        {
            Debug.Log("[UnitFactory] 已预加载过，跳过。");
            return;
        }

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

        _isPreloaded = true;
        Debug.Log($"[UnitFactory] 预加载完成，共 {_prefabCache.Count} 个 Prefab。");
    }

    /// <summary>获取缓存的 Prefab（供 LoadManager 门面转发）。</summary>
    public GameObject GetPrefab(string key)
    {
        if (_prefabCache.TryGetValue(key, out var prefab))
        {
            return prefab;
        }
        Debug.LogError($"[UnitFactory] 找不到 Prefab: {key}。可用: {string.Join(", ", _prefabCache.Keys)}");
        return null;
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

        // 3.0.1 §7.4 对象池：优先取桶（池空才 Instantiate——战斗尖峰零分配）
        GameObject instance = _instancePool.Get(key);
        if (instance == null)
        {
            instance = Instantiate(prefab, position, Quaternion.identity);
            instance.name = key;
        }
        else
        {
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.identity;
            instance.SetActive(true);
        }

        // 绑定数据到控制器
        var controller = instance.GetComponent<UnitController>();
        if (controller != null)
        {
            controller.Initialize(data);
        }

        // 3.0.1: 如果有 NPCBrain 且 data 是 NpcProfessionDef，初始化 AI 大脑
        var brain = instance.GetComponent<NPCBrain>();
        if (brain != null && data is NpcProfessionDef npcDef)
        {
            brain.Init(npcDef);
        }

        return instance;
    }

    /// <summary>
    /// 3.0.1 §7.4 单位死亡回池（由 UnitController.Die 调用）。
    /// 立即 SetActive(false) 回桶（P0 简化：死亡动画停留表现 P2 再叠加延迟）。
    /// 出池时 SpawnUnit 会重新 Initialize + brain.Init，状态天然全新，无需手动 Reset。
    /// </summary>
    public void ReturnUnitToPool(UnitController unit)
    {
        if (unit == null) return;
        string key = unit.name;
        if (unit.Data != null)
            key = $"{unit.Data.faction}_{unit.Data.occupation}";
        _instancePool.Return(key, unit.gameObject);
    }

    /// <summary>
    /// 3.0.1 §7.4 预热（战斗尖峰零 Instantiate）。按 prefab key × 数量预实例化入桶。
    /// 数量为 0/缺 prefab 自动跳过。幂等可重复调（重复预热同 key 会继续叠加）。
    /// </summary>
    public void Prewarm(string prefabKey, int count)
    {
        if (count <= 0) return;
        if (!_prefabCache.TryGetValue(prefabKey, out var prefab)) return;
        _instancePool.Prewarm(prefabKey, prefab, count, transform);
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
