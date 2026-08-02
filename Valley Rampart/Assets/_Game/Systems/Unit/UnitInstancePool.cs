using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  3.0.1 母文档 §7.4 对象池改造 - 实例层 UnitInstancePool
//  详见 3.0.1注意力机制与刺激源.md §7.4
//  按 prefab/职业分桶的 GameObject 实例池（尸体原料：SetActive(false)，不占格子不思考）
//  P0 = 门面（UnitFactory）+ 分桶 + 预热 + 延迟回收 + 出池自然重置（重新 Initialize/Init）
// ============================================================================

/// <summary>
/// UnitInstancePool（§7.4 实例层）。
/// 按 prefab key 分桶（工人/士兵/弓箭手/亡灵…），出生取桶、死亡延迟 1.5s 回桶。
/// 出池路径总是走 UnitFactory.SpawnUnit（重新 Initialize + brain.Init），状态天然全新，
/// 因此回池只需 SetActive(false)，无需手动 Reset（Reset 契约为 P1 IdleRegistry 逻辑层准备）。
/// </summary>
public class UnitInstancePool
{
    private readonly Dictionary<string, Queue<GameObject>> _buckets = new Dictionary<string, Queue<GameObject>>();

    /// <summary>预热：按 prefab key 预实例化 count 个（战斗尖峰零 Instantiate）。</summary>
    public void Prewarm(string prefabKey, GameObject prefab, int count, Transform parent)
    {
        if (prefab == null || count <= 0) return;
        if (!_buckets.TryGetValue(prefabKey, out var queue))
        {
            queue = new Queue<GameObject>();
            _buckets[prefabKey] = queue;
        }
        for (int i = 0; i < count; i++)
        {
            var go = Object.Instantiate(prefab, parent);
            go.name = prefabKey;
            go.SetActive(false);
            queue.Enqueue(go);
        }
        Debug.Log($"[UnitInstancePool] 预热 {prefabKey} ×{count}（当前桶 {queue.Count}）");
    }

    /// <summary>取桶（无则 null，调用方 Instantiate 兜底）。</summary>
    public GameObject Get(string prefabKey)
    {
        if (_buckets.TryGetValue(prefabKey, out var queue) && queue.Count > 0)
            return queue.Dequeue();
        return null;
    }

    /// <summary>回桶（SetActive(false)，不销毁不重置——出池重新初始化）。</summary>
    public void Return(string prefabKey, GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        if (!_buckets.TryGetValue(prefabKey, out var queue))
        {
            queue = new Queue<GameObject>();
            _buckets[prefabKey] = queue;
        }
        queue.Enqueue(go);
    }

    /// <summary>桶内数量（调试/统计用）。</summary>
    public int Count(string prefabKey)
    {
        return _buckets.TryGetValue(prefabKey, out var queue) ? queue.Count : 0;
    }
}
