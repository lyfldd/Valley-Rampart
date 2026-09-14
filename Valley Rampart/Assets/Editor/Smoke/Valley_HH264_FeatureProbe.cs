#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// HH.264 **G 段**：特征物接线**运行期验收（视觉客观化）**（Editor-only · 正门 `TestHarnessApi.EnterTestRun`）。
///
/// 依据：HH.264 任务书 G1~G4 ＋ 验收线 8/9（D710）：**禁**以"表里有键/计数"充作"真图生效"——
/// 必须给**运行中** `Tile.sprite` 的**实名**（含 artId 前缀）＋**截图**。
///   G1/G4：`Tree/Mountain/SnowMountain/Mine/OreVein/StonePile/WoodPile` 逐型取**已加载格**的 `Tile.sprite.name`；
///   G2：miss ⇒ 占位（sprite 名为空）＋一次性告警（`[MapRenderService] 特征物无真图` 条数）；
///   G3：表内 `feat_*` 键 vs 素材实盘**逐项对齐**（缺键/多键列报·禁静默）。
/// 用法（MCP）：Play 模式内 `Valley_HH264_FeatureProbe.Run()`；自收尾（ExitTestRun+QuitSmoke+退 Play）。
/// 证据：`Logs/hh264_feature_probe.log` ＋ 截图 `Logs/hh264_g_visual_*.png`。
/// </summary>
public static class Valley_HH264_FeatureProbe
{
    const int Seed = 21107;
    const string FeatDir = "Assets/_Game/Art/Buildings/neutral/features";
    static readonly StringBuilder _log = new StringBuilder();
    static string _logPath;
    static int _pass, _fail;
    static int _warnCount;      // [MapRenderService] 特征物无真图 告警条数（G2 禁刷屏）

    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH264G] 须先 GameScene 进 Play。"); return; }
        _log.Clear(); _pass = 0; _fail = 0; _warnCount = 0;
        Log("探针启动（正门 EnterTestRun · seed=" + Seed + " · 1x）", true);
        var host = new GameObject("HH264_FeatureProbeHost").AddComponent<ProbeHost>();
        Application.logMessageReceived += host.Catch;
        host.Host(Coroutine(host));
    }

    class ProbeHost : MonoBehaviour
    {
        public void Host(IEnumerator r) { StartCoroutine(r); }
        public void Catch(string cond, string stack, LogType t)
        {
            if (cond.Contains("特征物无真图")) _warnCount++;
            else if (t == LogType.Exception) Debug.LogError("[HH264G][PROBE-EXC] " + cond + "\n" + stack);
        }
    }

    static void Log(string msg, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + msg);
        if (ok) _pass++; else _fail++;
        Debug.Log("[HH264G] " + (ok ? "✓ " : "✗ ") + msg);
        Flush();
    }

    static void Flush()
    {
        try
        {
            if (_logPath == null)
                _logPath = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh264_feature_probe.log"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_logPath));
            System.IO.File.WriteAllText(_logPath, _log.ToString());
        }
        catch { /* 取证失败不影响探针 */ }
    }

    static IEnumerator Coroutine(ProbeHost host)
    {
        // ---- G3（不依赖运行时）：表内 feat_* 键 vs 素材实盘逐项对齐 ----
        AlignKeys();

        var cfg = new NewGameConfig
        {
            mapSeed = Seed, worldSeed = Seed, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = "smoke_hh264g", kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 1f);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null || KingdomRegistry.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Log("等世界就绪超时", false); Finish(host); yield break; }
        }
        yield return new WaitForSeconds(1.5f);

        var map = WorldManager.Instance.ActiveMap;
        var tmGo = GameObject.Find("Tilemap_Feature");
        var tm = tmGo != null ? tmGo.GetComponent<Tilemap>() : null;
        if (tm == null || map == null) { Log("Tilemap_Feature / MapData 缺失", false); Finish(host); yield break; }

        // 视野拉到最远档（CameraConfig.zoomLevels={1,2,4}）＋ 聚焦树簇，保证特征物在框
        var rig = CameraRig.Instance;
        GridCoord? treeCell = FindCell(map, FeatureType.Tree, 6);
        if (rig != null)
        {
            rig.ZoomTo(2);
            if (treeCell.HasValue) rig.FocusOn(MapRenderService.GridToIso(treeCell.Value));
        }
        yield return new WaitForSeconds(2.0f);   // 等 chunk 铺开 + 相机 clamp 完成

        // ---- G1/G2/G4：运行中逐型取 Tile.sprite 实名 ----
        var map2 = WorldManager.Instance.ActiveMap;   // 相机移动后重新取（chunk 可能新增）
        var perFt = new Dictionary<FeatureType, string>();
        var perFtCount = new Dictionary<FeatureType, int>();
        int total = 0, real = 0, placeholder = 0;
        var distinct = new SortedDictionary<string, int>();
        var bb = tm.cellBounds;
        for (int x = bb.xMin; x < bb.xMax; x++)
        {
            for (int y = bb.yMin; y < bb.yMax; y++)
            {
                var sp = tm.GetSprite(new Vector3Int(x, y, 0));
                if (sp == null) continue;
                total++;
                string nm = sp.name ?? "";
                if (!distinct.ContainsKey(nm)) distinct[nm] = 0;
                distinct[nm] = distinct[nm] + 1;
                if (nm.StartsWith("feat_")) real++; else placeholder++;
                if (x < 0 || y < 0 || x >= map2.width || y >= map2.height) continue;
                var ft = map2.features[y * map2.width + x];
                if (!perFt.ContainsKey(ft)) { perFt[ft] = nm; perFtCount[ft] = 1; }
                else perFtCount[ft] = perFtCount[ft] + 1;
            }
        }
        _log.AppendLine("  ── 特征物层运行期抽样（Tilemap_Feature cellBounds=" + bb + "）");
        _log.AppendLine("     tile 总数=" + total + " 真图(feat_* 实名)=" + real + " 占位(sprite 名空)=" + placeholder);
        _log.AppendLine("     distinct sprite 名=" + distinct.Count + " → " + Join(distinct));
        Log("G1 特征物层真图占比：tile=" + total + " 真图=" + real + " 占位=" + placeholder
            + "（真图 > 0 且占比 > 50% ⇒ 特征物线已接真图）", total > 0 && real > 0 && real * 2 > total);

        // 逐型实名（G1 判据：Tree/Mine/OreVein/StonePile/WoodPile 须 feat_* 前缀）
        var mustReal = new[] { FeatureType.Tree, FeatureType.Mine, FeatureType.OreVein, FeatureType.StonePile, FeatureType.WoodPile };
        foreach (var ft in new[] { FeatureType.Plain, FeatureType.Tree, FeatureType.Mountain, FeatureType.SnowMountain,
                                   FeatureType.Mine, FeatureType.OreVein, FeatureType.StonePile, FeatureType.WoodPile,
                                   FeatureType.River, FeatureType.Ocean })
        {
            string nm = perFt.ContainsKey(ft) ? perFt[ft] : "(本视野内无该型)";
            int n = perFtCount.ContainsKey(ft) ? perFtCount[ft] : 0;
            bool isMust = System.Array.IndexOf(mustReal, ft) >= 0;
            _log.AppendLine("     " + ft + " → sprite=\"" + nm + "\" n=" + n);
            if (isMust)
                Log("G4 运行中实名：" + ft + " → Tile.sprite.name=\"" + nm + "\"（须 feat_ 前缀·n=" + n + "）", nm.StartsWith("feat_"));
        }

        // ---- G2：一次性告警条数（Mountain/SnowMountain 无素材 ⇒ 各 1 条，禁刷屏）----
        _log.AppendLine("     [MapRenderService] 特征物无真图 告警条数=" + _warnCount);
        Log("G2 缺图一次性告警：连续多帧铺格后告警条数=" + _warnCount + "（≤2＝每缺键 1 条·禁刷屏）", _warnCount <= 2);

        // ---- 双渲染面核查：OreVein/WoodPile/StonePile 另有 Building 实体（E1 证据）----
        var reg = BuildingRegistry.Instance;
        int nbTot = 0; var nbNames = new SortedDictionary<string, int>();
        if (reg != null)
        {
            foreach (var b in reg.All)
            {
                if (b == null) continue;
                if (b.sourceType != BuildingType.OreVein && b.sourceType != BuildingType.WoodPile && b.sourceType != BuildingType.StonePile) continue;
                nbTot++;
                var sr = b.GetComponent<SpriteRenderer>();
                string nm = (sr != null && sr.sprite != null) ? sr.sprite.name : "null";
                string k = b.sourceType + "=" + nm;
                if (!nbNames.ContainsKey(k)) nbNames[k] = 0;
                nbNames[k] = nbNames[k] + 1;
            }
        }
        _log.AppendLine("  ── 同格 Building 实体（一次性资源另走建筑路径）total=" + nbTot + " → " + Join(nbNames));

        // ---- 截图（G4/D710 视觉客观化：须 1 张，覆盖特征物层）----
        yield return Shot("Logs/hh264_g_visual_features.png");

        _log.AppendLine("==== HH264-G 汇总：PASS=" + _pass + " FAIL=" + _fail + " ====");
        Debug.Log("[HH264G][SUMMARY]\n" + _log);
        Finish(host);
    }

    /// <summary>G3：表内 feat_* 键 vs 素材实盘逐项对齐（缺键/多键列报·禁静默）。</summary>
    static void AlignKeys()
    {
        var table = ValleyRampart.Rendering.SpriteRefTable.Instance;
        var keys = new SortedSet<string>();
        if (table != null)
            foreach (var id in table.GetAllArtIds())
                if (id.StartsWith("feat_")) keys.Add(id);

        var files = new SortedSet<string>();
        string abs = System.IO.Path.GetFullPath(FeatDir);
        if (System.IO.Directory.Exists(abs))
            foreach (var f in System.IO.Directory.GetFiles(abs, "*.png"))
                files.Add(System.IO.Path.GetFileNameWithoutExtension(f));

        var onlyFile = new List<string>(); var onlyTable = new List<string>();
        foreach (var f in files) if (!keys.Contains(f)) onlyFile.Add(f);
        foreach (var k in keys) if (!files.Contains(k)) onlyTable.Add(k);

        _log.AppendLine("── G3 键空间对齐（" + FeatDir + "）");
        _log.AppendLine("   素材实盘 " + files.Count + " 个：" + string.Join(", ", new List<string>(files).ToArray()));
        _log.AppendLine("   表内 feat_* 键 " + keys.Count + " 个：" + string.Join(", ", new List<string>(keys).ToArray()));
        _log.AppendLine("   仅实盘有（未落表）：" + (onlyFile.Count == 0 ? "无" : string.Join(", ", onlyFile.ToArray())));
        _log.AppendLine("   仅表内有（素材缺）：" + (onlyTable.Count == 0 ? "无" : string.Join(", ", onlyTable.ToArray())));
        Log("G3 feat_* 键逐项对齐：实盘=" + files.Count + " 表=" + keys.Count
            + " 未落表=" + onlyFile.Count + " 表内缺素材=" + onlyTable.Count, onlyFile.Count == 0 && onlyTable.Count == 0);
        bool mt = false, sm = false;
        foreach (var s in files) { if (s.StartsWith("feat_mountain")) mt = true; if (s.StartsWith("feat_snow")) sm = true; }
        if (!mt || !sm)
            _log.AppendLine("   ⚠ 列报（G3 禁静默）：Mountain/SnowMountain 无 `feat_*` 素材 ⇒ 走占位（映射表 §十.2 未定义，待策划裁）");
        Flush();
    }

    static GridCoord? FindCell(MapData m, FeatureType want, int radius)
    {
        int cx = m.width / 2, cy = m.height / 2;
        for (int r = 0; r <= radius; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = cx + dx * 3, y = cy + dy * 3;
                    if (x < 0 || y < 0 || x >= m.width || y >= m.height) continue;
                    if (m.features[y * m.width + x] == want) return new GridCoord(x, y);
                }
        for (int y = 0; y < m.height; y++)
            for (int x = 0; x < m.width; x++)
                if (m.features[y * m.width + x] == want) return new GridCoord(x, y);
        return null;
    }

    static IEnumerator Shot(string rel)
    {
        string abs = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", rel));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(abs));
        if (System.IO.File.Exists(abs)) System.IO.File.Delete(abs);
        ScreenCapture.CaptureScreenshot(rel);
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        bool ok = System.IO.File.Exists(abs);
        _log.AppendLine("  截图 " + rel + " 落盘=" + ok);
        Log("G4 截图取证：" + rel + " 落盘=" + ok, ok);
    }

    static string Join<T>(IDictionary<T, int> d)
    {
        var sb = new StringBuilder();
        foreach (var kv in d) sb.Append(kv.Key + "×" + kv.Value + " ");
        return sb.ToString().Trim();
    }

    static void Finish(ProbeHost host)
    {
        _log.AppendLine("==== HH264-G 收尾：PASS=" + _pass + " FAIL=" + _fail + " ====");
        Flush();
        Application.logMessageReceived -= host.Catch;
        TestHarnessApi.ExitTestRun();
        SmokeApi.QuitSmoke();
    }
}
#endif
