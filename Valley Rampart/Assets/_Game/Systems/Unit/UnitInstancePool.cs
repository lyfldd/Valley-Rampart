using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  3.0.1 母文档 §7.4 对象池改造 - 实例层 UnitInstancePool
//  详见 3.0.1注意力机制与刺激源.md §7.4
//  按 UnitData（SO 引用）分桶的 GameObject 实例池（尸体原料：SetActive(false)，不占格子不思考）
//  2_3 步骤0：分桶 key 由 string（prefab 名拼）改为 UnitData 引用（SO 直接引用，消除命名耦合）。
//  出池路径总是走 UnitFactory.SpawnUnit（重新 Initialize + brain.Init），状态天然全新，
//  因此回池只需 SetActive(false)，无需手动 Reset（Reset 契约为 P1 IdleRegistry 逻辑层准备）。
// ============================================================================

/// <summary>
/// UnitInstancePool（§7.4 实例层）。
/// 按 UnitData（SO 引用）分桶（工人/士兵/弓箭手/亡灵…），出生取桶、死亡回桶。
/// 出池路径总是走 UnitFactory.SpawnUnit（重新 Initialize + brain.Init），状态天然全新。
/// </summary>
public class UnitInstancePool
{
    private readonly Dictionary<UnitData, Queue<GameObject>> _buckets = new Dictionary<UnitData, Queue<GameObject>>();

    /// <summary>预热：按 UnitData 预实例化 count 个（战斗尖峰零 Instantiate）。</summary>
    public void Prewarm(UnitData data, int count, Transform parent)
    {
        if (data == null || data.prefab == null || count <= 0) return;
        if (!_buckets.TryGetValue(data, out var queue))
        {
            queue = new Queue<GameObject>();
            _buckets[data] = queue;
        }
        for (int i = 0; i < count; i++)
        {
            var go = Object.Instantiate(data.prefab, parent);
            go.name = data.name;
            go.SetActive(false);
            queue.Enqueue(go);
        }
        Debug.Log($"[UnitInstancePool] 预热 {data.name} ×{count}（当前桶 {queue.Count}）");
    }

    /// <summary>取桶（无则 null，调用方 Instantiate 兜底）。</summary>
    public GameObject Get(UnitData data)
    {
        if (_buckets.TryGetValue(data, out var queue) && queue.Count > 0)
            return queue.Dequeue();
        return null;
    }

    /// <summary>回桶（SetActive(false)，不销毁不重置——出池重新初始化）。</summary>
    public void Return(UnitData data, GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        if (!_buckets.TryGetValue(data, out var queue))
        {
            queue = new Queue<GameObject>();
            _buckets[data] = queue;
        }
        queue.Enqueue(go);
    }

    /// <summary>桶内数量（调试/统计用）。</summary>
    public int Count(UnitData data)
    {
        return _buckets.TryGetValue(data, out var queue) ? queue.Count : 0;
    }
}