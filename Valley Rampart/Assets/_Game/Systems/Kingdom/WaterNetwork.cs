using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局隐藏水网（QQQ.2 §需求9/§10.5，DR-8）。单例 MonoBehaviour + ISaveable, Global。
///
/// 职责：水井产水入网（AddWater）、农场产粮消耗网内水（ConsumeWater）。
/// 水为内部循环资源，不显示在 UI（隐藏资源）。
/// 容量上限 100 水，超出后水井停产（避免浪费）。
///
/// ===== 2_17 步骤11 批3a·per-kingdom 水桶（HH.30 策划 B′ 裁）=====
///  B′ 双语义锁死：
///   ① AI 井恒不产水（守卫已在 ProducerComponent.Tick 水井路径 L124，`kingdomId>0 return`）。
///   ② AI 农田耗水走自己桶（本批补 kingdomId 路由）：AI 农田产粮消耗 AI 桶水；
///      AI 桶水恒 0 → AI 农田停产 —— 与 AI 无供水链既有语义自洽，堵住 "AI 农田吃玩家网水" 泄漏面。
///  玩家(id=0) 桶 = 原单桶 _stored 语义逐位等价（HH.30 零回归，玩家 IsFull/Stored/AddWater 行为不变）。
/// </summary>
public class WaterNetwork : Singleton<WaterNetwork>, ISaveable
{
    public string SaveId => "WaterNetwork";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    [Tooltip("水网容量上限（超出后水井停产）")]
    public int capacity = 100;

    /// <summary>玩家(id=0) 水网存量（真源；AI 桶在 _aiStoredByKingdom）。</summary>
    private float _stored;

    /// <summary>AI(id>0) 各王国水桶（B′：AI 桶恒 0 → AI 农田停产；为未来 AI 供水链预留桶结构）。</summary>
    private readonly Dictionary<int, float> _aiStoredByKingdom = new Dictionary<int, float>();

    /// <summary>当前水网存量（玩家桶0，HH.30 零回归）。</summary>
    public float Stored => _stored;
    /// <summary>水网是否已满（玩家桶0，水井据此停产）。</summary>
    public bool IsFull => _stored >= capacity;

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        SaveManager.Instance.RegisterSaveable(this);
    }

    /// <summary>水井产水入网（DR-14：well.rate=4 水/秒）。已满则拒收。玩家桶0 走原逻辑。</summary>
    public void AddWater(float amount) => AddWater(amount, 0);

    /// <summary>水井产水入网（2_17 批3a：按王国归属；kingdomId=0 玩家、&gt;0 AI 各桶）。</summary>
    public void AddWater(float amount, int kingdomId)
    {
        if (amount <= 0f) return;
        if (kingdomId == 0)
        {
            _stored = Mathf.Min(capacity, _stored + amount);
        }
        else
        {
            _aiStoredByKingdom.TryGetValue(kingdomId, out var v);
            _aiStoredByKingdom[kingdomId] = Mathf.Min(capacity, v + amount);
        }
    }

    /// <summary>
    /// 农场产粮消耗网内水（DR-9/DR-18：每次产粮事件耗 2 水）。返回是否够扣。
    /// 不够则本秒不产（缺水停产）。玩家桶0 走原逻辑。
    /// </summary>
    public bool ConsumeWater(float amount) => ConsumeWater(amount, 0);

    /// <summary>
    /// 农场产粮消耗网内水（2_17 批3a：按王国归属）。AI 桶恒 0 → AI 农田缺水停产（B′ 语义）。
    /// </summary>
    public bool ConsumeWater(float amount, int kingdomId)
    {
        if (amount <= 0f) return true;
        if (kingdomId == 0)
        {
            if (_stored < amount) return false;
            _stored -= amount;
            return true;
        }
        // AI 桶：水恒 0（AI 井不产水，见 ProducerComponent 守卫）→ 无供应，缺水停产。
        // 桶结构保留供未来 AI 供水链；当前 AI 农田无条件缺水（语义自洽，堵泄漏）。
        _aiStoredByKingdom.TryGetValue(kingdomId, out var v);
        if (v < amount) return false;
        _aiStoredByKingdom[kingdomId] = v - amount;
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