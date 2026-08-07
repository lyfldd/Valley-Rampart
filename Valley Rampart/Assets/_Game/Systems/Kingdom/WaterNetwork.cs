using UnityEngine;

/// <summary>
/// 全局隐藏水网（QQQ.2 §需求9/§10.5，DR-8）。单例 MonoBehaviour + ISaveable, Global。
///
/// 职责：水井产水入网（AddWater）、农场产粮消耗网内水（ConsumeWater）。
/// 水为内部循环资源，不显示在 UI（隐藏资源）。
/// 容量上限 100 水，超出后水井停产（避免浪费）。
/// </summary>
public class WaterNetwork : Singleton<WaterNetwork>, ISaveable
{
    public string SaveId => "WaterNetwork";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    [Tooltip("水网容量上限（超出后水井停产）")]
    public int capacity = 100;

    private float _stored;

    /// <summary>当前水网存量。</summary>
    public float Stored => _stored;
    /// <summary>水网是否已满（水井据此停产）。</summary>
    public bool IsFull => _stored >= capacity;

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        SaveManager.Instance.RegisterSaveable(this);
    }

    /// <summary>水井产水入网（DR-14：well.rate=4 水/秒）。已满则拒收。</summary>
    public void AddWater(float amount)
    {
        if (amount <= 0f) return;
        _stored = Mathf.Min(capacity, _stored + amount);
    }

    /// <summary>
    /// 农场产粮消耗网内水（DR-9/DR-18：每次产粮事件耗 2 水）。返回是否够扣。
    /// 不够则本秒不产（缺水停产）。
    /// </summary>
    public bool ConsumeWater(float amount)
    {
        if (amount <= 0f) return true;
        if (_stored < amount) return false;
        _stored -= amount;
        return true;
    }

    // ===== ISaveable, Global =====

    public SavePayload SaveState()
    {
        var data = new WaterNetworkSaveData { saveDataVersion = 1, stored = _stored, capacity = capacity };
        return new SavePayload
        {
            typeName = typeof(WaterNetworkSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(WaterNetworkSaveData).AssemblyQualifiedName) return;
        var data = JsonUtility.FromJson<WaterNetworkSaveData>(payload.json);
        capacity = data.capacity > 0 ? data.capacity : 100;
        _stored = Mathf.Clamp(data.stored, 0f, capacity);
        Debug.Log($"[WaterNetwork] 读档恢复水网 {_stored}/{capacity}");
    }

    public void ResetState()
    {
        _stored = 0f;
    }
}

/// <summary>水网存档数据。</summary>
[System.Serializable]
public class WaterNetworkSaveData
{
    public int saveDataVersion = 1;
    public float stored;
    public int capacity;
}