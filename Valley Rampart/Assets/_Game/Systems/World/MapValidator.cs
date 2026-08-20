using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D 地图校验（2_1 §5.2 步骤5/步骤9 复跑）。连通性 flood-fill + 打通走廊。
/// 直接改 map.features（把路径上的阻挡格改 Plain），保证所有出生点彼此可达。
/// 岛屿可被水隔断（§3.5），但王国出生点必须连通。
/// </summary>
public static class MapValidator
{
    /// <summary>
    /// 连通性校验：以第一个出生点为源 flood-fill，不可达的出生点打通走廊。
    /// 返回打通的走廊数。水域放置后应复跑（2_1 步骤9）。
    /// </summary>
    public static int ValidateConnectivity(MapData map)
    {
        if (map == null || map.kingdomSpawns == null || map.kingdomSpawns.Count == 0) return 0;

        int carved = 0;
        var source = map.kingdomSpawns[0];

        for (int i = 1; i < map.kingdomSpawns.Count; i++)
        {
            var target = map.kingdomSpawns[i];
            if (IsReachable(map, source, target)) continue;
            CarveCorridor(map, source, target);
            carved++;
        }

        // 死区检测留作 P2（当前以出生点连通为准）
        return carved;
    }

    static int Idx(MapData m, int x, int y) => y * m.width + x;
    static bool InB(MapData m, int x, int y) => x >= 0 && y >= 0 && x < m.width && y < m.height;

    /// <summary>从 source 到 target 是否可走连通（只走可走特征物，4 邻域）。</summary>
    public static bool IsReachable(MapData map, Vector2Int source, Vector2Int target)
    {
        int w = map.width, h = map.height;
        var visited = new bool[w * h];
        var q = new Queue<int>();
        int s = Idx(map, source.x, source.y);
        visited[s] = true; q.Enqueue(s);
        int t = Idx(map, target.x, target.y);

        while (q.Count > 0)
        {
            int cur = q.Dequeue();
            if (cur == t) return true;
            int cx = cur % w, cy = cur / w;
            TryEnq(map, visited, q, cx + 1, cy, w, h);
            TryEnq(map, visited, q, cx - 1, cy, w, h);
            TryEnq(map, visited, q, cx, cy + 1, w, h);
            TryEnq(map, visited, q, cx, cy - 1, w, h);
        }
        return false;
    }

    static void TryEnq(MapData m, bool[] visited, Queue<int> q, int x, int y, int w, int h)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        int i = y * w + x;
        if (visited[i]) return;
        if (!MapGenRules.IsWalkableFeature(m.features[i])) return;
        visited[i] = true; q.Enqueue(i);
    }

    /// <summary>打通走廊：BFS 允许穿越任意格找最短路径，把路径上的阻挡格改 Plain。</summary>
    static void CarveCorridor(MapData map, Vector2Int source, Vector2Int target)
    {
        int w = map.width, h = map.height, n = w * h;
        var parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = -1;
        var q = new Queue<int>();
        int s = Idx(map, source.x, source.y);
        int t = Idx(map, target.x, target.y);
        parent[s] = s; q.Enqueue(s);

        while (q.Count > 0 && parent[t] == -1)
        {
            int cur = q.Dequeue();
            int cx = cur % w, cy = cur / w;
            EnqAny(parent, q, cx + 1, cy, cur, w, h, n);
            EnqAny(parent, q, cx - 1, cy, cur, w, h, n);
            EnqAny(parent, q, cx, cy + 1, cur, w, h, n);
            EnqAny(parent, q, cx, cy - 1, cur, w, h, n);
        }

        if (parent[t] == -1) return;   // 理论不可达（地图全空则不会发生）

        // 回溯路径，把阻挡格改 Plain（保留海洋不挖穿边缘）
        int node = t;
        while (node != s)
        {
            if (!MapGenRules.IsWalkableFeature(map.features[node]) && map.features[node] != FeatureType.Ocean)
                map.features[node] = FeatureType.Plain;
            node = parent[node];
        }
    }

    static void EnqAny(int[] parent, Queue<int> q, int x, int y, int from, int w, int h, int n)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        int i = y * w + x;
        if (parent[i] != -1) return;
        parent[i] = from; q.Enqueue(i);
    }
}
