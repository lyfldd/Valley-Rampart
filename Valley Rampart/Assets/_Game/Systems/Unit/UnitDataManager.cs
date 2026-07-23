using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单位数据管理器。同步加载 Resources/UnitData/ 下所有 UnitData SO。
/// 新增单位配置 = 往文件夹扔一个 .asset 文件，无需改代码。
/// </summary>
public class UnitDataManager : Singleton<UnitDataManager>
{
    private readonly Dictionary<string, UnitData> _dataCache = new Dictionary<string, UnitData>();
    public int Count => _dataCache.Count;
    public bool IsInitialized { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        // 不再 Awake 自动 LoadAll —— 改由 LoadManager.LoadStaticConfigs() 显式调
        // 也不再发 UnitDataLoadedEvent —— 改由 LoadManager 发 ConfigsLoadedEvent
    }

    /// <summary>
    /// 同步加载 Resources/UnitData/ 下所有 UnitData 资产。
    /// 由 LoadManager 阶段1 显式调用。
    /// </summary>
    public void LoadAll()
    {
        if (IsInitialized)
        {
            Debug.Log("[UnitDataManager] 已加载过，跳过。");
            return;
        }

        Debug.Log("[UnitDataManager] 开始加载单位数据...");

        UnitData[] allData = Resources.LoadAll<UnitData>("UnitData");

        foreach (var data in allData)
        {
            if (data == null) continue;

            string key = $"{data.faction}_{data.occupation}";

            if (_dataCache.ContainsKey(key))
            {
                Debug.LogWarning($"[UnitDataManager] 重复配置 Key: {key}，已跳过。");
                continue;
            }

            _dataCache.Add(key, data);
        }

        IsInitialized = true;
        Debug.Log($"[UnitDataManager] 加载完成，共 {_dataCache.Count} 个单位配置。");
    }

    /// <summary>
    /// 按 Faction + Occupation 获取单位数据。
    /// </summary>
    public UnitData GetData(Faction faction, Occupation occupation)
    {
        string key = $"{faction}_{occupation}";

        if (_dataCache.TryGetValue(key, out UnitData data))
        {
            return data;
        }

        Debug.LogError($"[UnitDataManager] 找不到数据: [{key}]。可用: {string.Join(", ", _dataCache.Keys)}");
        return null;
    }

    /// <summary>
    /// 获取所有已加载的单位数据。
    /// </summary>
    public IEnumerable<UnitData> GetAllData()
    {
        return _dataCache.Values;
    }
}
