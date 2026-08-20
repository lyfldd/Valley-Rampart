using System;
using System.Collections.Generic;

// ============================================================================
//  2_6 P0a 微格 A*：确定性最小堆（开放列表）。
//  存 (GridCoord, f)。允许同点重复入堆（懒删除：过期条目由 A* 侧比较 fScore 丢弃）。
//  平局 f 相同 → 入堆序号小者优先（确定性 R3，禁字典遍历序参与）。
// ============================================================================

/// <summary>确定性最小堆（开放列表）。A* 侧对 pop 出的 f 与当前 fScore 比较判断是否过期。</summary>
public class BinaryHeap
{
    private struct Entry { public GridCoord c; public float f; public int seq; }
    private readonly List<Entry> _heap = new List<Entry>();
    private int _seq;

    public int Count => _heap.Count;

    public void Push(GridCoord c, float f)
        => PushEntry(new Entry { c = c, f = f, seq = _seq++ });

    public bool TryPop(out GridCoord c, out float f)
    {
        if (_heap.Count == 0) { c = default; f = 0f; return false; }
        var top = _heap[0];
        c = top.c;
        f = top.f;
        // 堆顶移除（末尾交换）
        int last = _heap.Count - 1;
        _heap[0] = _heap[last];
        _heap.RemoveAt(last);
        if (_heap.Count > 0) SiftDown(0);
        return true;
    }

    public void Clear() { _heap.Clear(); _seq = 0; }

    private void PushEntry(Entry e)
    {
        _heap.Add(e);
        int i = _heap.Count - 1;
        while (i > 0)
        {
            int p = (i - 1) / 2;
            if (Less(_heap[i], _heap[p])) { Swap(i, p); i = p; }
            else break;
        }
    }

    private void SiftDown(int i)
    {
        int n = _heap.Count;
        while (true)
        {
            int l = 2 * i + 1, r = 2 * i + 2, s = i;
            if (l < n && Less(_heap[l], _heap[s])) s = l;
            if (r < n && Less(_heap[r], _heap[s])) s = r;
            if (s == i) break;
            Swap(i, s);
            i = s;
        }
    }

    private void Swap(int a, int b)
    {
        var t = _heap[a]; _heap[a] = _heap[b]; _heap[b] = t;
    }

    private static bool Less(Entry a, Entry b)
    {
        if (Math.Abs(a.f - b.f) > 1e-9f) return a.f < b.f;
        return a.seq < b.seq;   // 平局按入堆序号小优先（确定性）
    }
}