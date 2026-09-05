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
///  B′ 双语义（D535 修订后）：
///   ① AI 井产水入自己桶（HH.73 供水修复批 D535：原「AI 井恒不产水」拦截解除——
///      AI 水井产水经 AddWater(kingdomId) 入 AI 桶，AI 桶满停产对齐玩家语义）。
///   ② AI 农田耗水走自己桶（保留）：AI 农田产粮消耗 AI 桶水（批3a 消费端路由不动）——
///      泄漏面堵法从「断供」升级为「own 供水」，AI 农田吃玩家网水的通道仍封死。
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

    /// <summary>AI(id>0) 各王国水桶（批3a 桶结构 + HH.73/D535 供水链：AI 井产水入桶，AI 农田耗水出桶）。</summary>
    private readonly Dictionary<int, float> _aiStoredByKingdom = new Dictionary<int, float>();

    /// <summary>当前水网存量（玩家桶0，HH.30 零回归）。</summary>
    public float Stored => _stored;
    /// <summary>水网是否已满（玩家桶0，水井据此停产）。</summary>
    public bool IsFull => _stored >= capacity;

    /// <summary>
    /// 指定王国桶是否已满（D535：AI 桶满停产语义对齐玩家 IsFull；kingdomId=0 等价玩家 IsFull）。
    /// </summary>
    public bool IsBucketFull(int kingdomId)
    {
        if (kingdomId == 0) return _stored >= capacity;
        return _aiStoredByKingdom.TryGetValue(kingdomId, out var v) && v >= capacity;
    }

    /// <summary>指定王国桶当前水量（D535 公开读口：观测/探针/情报面；0=玩家桶）。</summary>
    public float GetStored(int kingdomId)
    {
        if (kingdomId == 0) return _stored;
        return _aiStoredByKingdom.TryGetValue(kingdomId, out var v) ? v : 0f;
    }

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
    /// 农场产粮消耗网内水（2_17 批3a：按王国归属；D535 语义②保留：AI 农田耗自己桶，
    /// 「AI 农田吃玩家网水」通道仍封死。供端=AI 水井产水入桶（HH.73 供水修复批）。
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
        // AI 桶：AI 水井产水入桶（D535）→ AI 农田耗自己桶水（批3a 路由不动）。
        _aiStoredByKingdom.TryGetValue(kingdomId, out var v);
        if (v < amount) return false;
        _aiStoredByKingdom[kingdomId] = v - amount;
        return true;
    }

    // ===== ISaveable, Global =====

    public SavePayload SaveState()
    {
        var data = new WaterNetworkSaveData { saveDataVersion = 2, stored = _stored, capacity = capacity };
        // HH.73/D535：AI 桶入档（additive 兼容旧档——旧档 v1 无 aiBuckets 字段 → 恢复为空 →
        // AI 桶从 0 起由水井重新蓄水自愈，零迁移成本；2_11 schema 口径）。
        foreach (var kvp in _aiStoredByKingdom)
            data.aiBuckets.Add(new WaterNetworkSaveData.AIBucket { kingdomId = kvp.Key, stored = kvp.Value });
        return new SavePayload
        {
            typeName = typeof(WaterNetworkSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 2
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(WaterNetworkSaveData).AssemblyQualifiedName) return;
        var data = JsonUtility.FromJson<WaterNetworkSaveData>(payload.json);
        capacity = data.capacity > 0 ? data.capacity : 100;
        _stored = Mathf.Clamp(data.stored, 0f, capacity);
        // AI 桶恢复（v2 起）：旧档（saveDataVersion<2）无该段 → _aiStoredByKingdom 保持空 → 水井自愈。
        if (data.saveDataVersion >= 2 && data.aiBuckets != null)
        {
            for (int i = 0; i < data.aiBuckets.Count; i++)
            {
                var b = data.aiBuckets[i];
                if (b.kingdomId <= 0) continue;   // 玩家桶走 _stored，AI 桶字典只收 id>0
                _aiStoredByKingdom[b.kingdomId] = Mathf.Clamp(b.stored, 0f, capacity);
            }
        }
        Debug.Log($"[WaterNetwork] 读档恢复水网 {_stored}/{capacity}（AI 桶 {_aiStoredByKingdom.Count} 国）");
    }

    public void ResetState()
    {
        _stored = 0f;
        _aiStoredByKingdom.Clear();   // HH.73/D535：AI 桶同参与清场（跨轮零残留完整性）
    }
}

/// <summary>水网存档数据。</summary>
[System.Serializable]
public class WaterNetworkSaveData
{
    public int saveDataVersion = 2;
    public float stored;
    public int capacity;
    /// <summary>AI 桶段（v2 起，additive；旧档缺省为空 → AI 桶 0 自愈）。</summary>
    public List<AIBucket> aiBuckets = new List<AIBucket>();

    /// <summary>单王国 AI 桶条目。</summary>
    [System.Serializable]
    public class AIBucket
    {
        public int kingdomId;
        public float stored;
    }
}