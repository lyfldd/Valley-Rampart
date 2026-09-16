#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// HH.294 片1-A（＝`HH.293` 并入项）验证读数（Editor-only · 编辑态直调，不需 Play）。
///
/// 覆盖 `HH.293` 三项判据：
///   B1：改后同 seed 生成 **`Mine` 的 nb 数 = 0**（改前 425）＋ 派生白名单回到三型
///   B3：⭐ **存在至少 1 个大区块，其矿洞簇数 = 0**（存在性反证）＋ 达标口径「Mine nb = 0」
///       〔⚠️ **HH.294 片3 R2（D768 §39.3）**：原判据②「全图矿洞簇数 < 大区块数」＝**形式错·结构性不可达**
///        （实盘 `round(E/4) ≥ 1` 恒成立 ⇒ 全图簇数恒 > 大区块数 ⇒ 该聚合判定行**已撤下、不得再落盘**）〕
///   ＋ 确定性：同 seed 两次生成**逐格一致**（features+climateZones）
/// 用法（MCP）：`Valley_HH294_HH293Probe.Run();`（编译期/编辑态）
/// </summary>
public static class Valley_HH294_HH293Probe
{
    const int Seed = 21107;

    public static string Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("======== HH.294 片1-A（HH.293 并入项）读数 ========");
        sb.AppendLine("环境: unity=" + Application.unityVersion + " isPlaying=" + Application.isPlaying
                      + " seed=" + Seed + " 口径=Normal(diff2)｜256²+384² 两图 + 确定性复验");

        foreach (int size in new int[] { 256, 384 })
        {
            var map = Valley_HH291_MapGenProbe.Build(Seed, size, size, 2);
            int mineCells = 0;
            for (int i = 0; i < map.features.Length; i++) if (map.features[i] == FeatureType.Mine) mineCells++;

            int nbMine = 0, nbTotal = 0;
            foreach (var nb in map.naturalBuildings)
            {
                nbTotal++;
                if (nb.feature == FeatureType.Mine) nbMine++;
            }

            // 矿洞簇（连通分量·4 邻）→ 按「簇左上格所属大区块」归块
            int cs = MapGenRules.ChunkSize;
            int cw = (map.width + cs - 1) / cs, ch = (map.height + cs - 1) / cs;
            var perChunk = new Dictionary<int, int>();
            var visited = new bool[map.features.Length];
            int clusters = 0;
            var q = new Queue<int>();
            for (int i = 0; i < map.features.Length; i++)
            {
                if (map.features[i] != FeatureType.Mine || visited[i]) continue;
                clusters++;
                int topLeft = i;
                visited[i] = true; q.Enqueue(i);
                while (q.Count > 0)
                {
                    int cur = q.Dequeue();
                    if (cur < topLeft) topLeft = cur;
                    int x = cur % map.width, y = cur / map.width;
                    Enq(map, visited, q, x + 1, y); Enq(map, visited, q, x - 1, y);
                    Enq(map, visited, q, x, y + 1); Enq(map, visited, q, x, y - 1);
                }
                int lx = topLeft % map.width, ly = topLeft / map.width;
                int key = (ly / cs) * cw + (lx / cs);
                perChunk[key] = perChunk.TryGetValue(key, out var v) ? v + 1 : 1;
            }
            int zeroChunks = 0, minV = int.MaxValue, maxV = 0;
            for (int k = 0; k < cw * ch; k++)
            {
                int v = perChunk.TryGetValue(k, out var vv) ? vv : 0;
                if (v == 0) zeroChunks++;
                if (v < minV) minV = v;
                if (v > maxV) maxV = v;
            }

            sb.AppendLine("---- [" + size + "²/Normal(diff2) · seed=" + Seed + "] ----");
            sb.AppendLine("  B1：Mine 格=" + mineCells + " ｜ Mine nb=" + nbMine + "（改前 HH.291 A6=425·判据须 0）｜ 其余 nb 总数=" + nbTotal);
            sb.AppendLine("  B3：矿洞簇（连通分量）总数=" + clusters + " ｜ 大区块数=" + (cw * ch)
                          + " ｜ 簇数=0 的区块数=" + zeroChunks + "（判据须 ≥1）"
                          + " ｜ 每区块簇数 min=" + (minV == int.MaxValue ? 0 : minV) + " max=" + maxV
                          + " 均值=" + (clusters / (double)(cw * ch)).ToString("0.00"));
            sb.AppendLine("  ⇒ B3 判据①「存在 0 簇区块」=" + (zeroChunks >= 1 ? "成立 ✅" : "不成立 ❌")
                          + " ｜ 达标口径（**D768 §39.3**：原判据②「全图簇数 < 大区块数」已判为**形式错·结构性不可达**"
                          + "〔实盘 `round(E/4) ≥ 1` 恒成立 ⇒ 全图簇数恒 > 大区块数〕，**已撤下、不再落盘**）："
                          + "「Mine nb = 0」=" + (nbMine == 0 ? "达标 ✅" : "不达标 ❌"));
        }

        // 确定性复验（同 seed 两次 · 256²）
        var a1 = Valley_HH291_MapGenProbe.Build(Seed, 256, 256, 2);
        var a2 = Valley_HH291_MapGenProbe.Build(Seed, 256, 256, 2);
        bool same = Valley_HH291_MapGenProbe.SameMap(a1, a2, out var why);
        sb.AppendLine("---- [确定性] 同 seed 两次：" + (same ? "逐格一致 ✅（features+climateZones+spawns+nb）" : "不一致  " + why));

        sb.AppendLine("======== 读数完毕 ========");
        WriteLog(sb.ToString());
        Debug.Log("[HH294-1A]\n" + sb.ToString());
        return sb.ToString();
    }

    /// <summary>落盘（**HH.294 补正 P4**：原仅 `Debug.Log` ⇒ 读数在磁盘上不可复核，撞 `L-02`「读数落盘缺口」家族）。
    /// 路径 `Valley Rampart/Logs/hh294_hh293_probe.log`（与同批探针族同口径）。</summary>
    static void WriteLog(string text)
    {
        try
        {
            string path = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh294_hh293_probe.log"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, "HH.294 片1-A（HH.293 并入项）读数 · " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + text);
        }
        catch { }
    }

    static void Enq(MapData m, bool[] vis, Queue<int> q, int x, int y)
    {
        if (x < 0 || y < 0 || x >= m.width || y >= m.height) return;
        int i = y * m.width + x;
        if (vis[i] || m.features[i] != FeatureType.Mine) return;
        vis[i] = true; q.Enqueue(i);
    }
}
#endif