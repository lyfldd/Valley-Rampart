#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// HH.268 地图渲染层重构批 · 取证容器（Editor-only · **正门 `TestHarnessApi.EnterTestRun`**）。
///
/// 模式（同一协程内容，仅 tag 不同）：
///   `Baseline()`  —— 回执期只读取证（M1「先量现值再改」）
///   `Verify(tag)` —— 施工后逐项验收
/// 取证项：
///   A 层序/模式实读（E4 复核：Grid layout/cellSize、各 Tilemap 锚点/order/mode/sortOrder、相机 sortAxis）
///   B ①判据基线：`GetCellCenterWorld − GridToIso`（4 抽样点 + maxAbs）
///   C M1 现值快照（地皮/特征/树 的 tex/pivot/importer/PPU/mesh/filter ＋ culling bounds 现行有效值）
///   D ③判据：Mine 簇按「逐个 2×2 占位块」精确分解（贪婪读序，非局部邻居近似）
///   E DrawCall 基线（H6 读数 80.98）
///   F 候选 (e) 代价实测：运行中改 `TilemapRenderer.mode`（Chunk↔Individual）与 ④ 的 order 复位，
///     读 drawCalls/batches 差值；**只改运行时对象、不回写资产**，测毕还原。
///   G ③运行中实例计数：特征层中 `feat_mine` 实名 tile 数 vs 已加载区 Mine 占位块数（应 1:1）
/// 证据：`Logs/hh268_map_{tag}.log` ＋ `Logs/hh268_{tag}_overview.png`。
/// </summary>
public static class Valley_HH268_MapProbe
{
    const int Seed = 21107;
    static readonly StringBuilder _log = new StringBuilder();
    static string _logPath, _tag;
    static int _pass, _fail;

    public static void Baseline() { Start("baseline"); }
    public static void Verify(string tag) { Start(string.IsNullOrEmpty(tag) ? "verify" : tag); }

    static void Start(string tag)
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH268] 须先 GameScene 进 Play。"); return; }
        _log.Clear(); _pass = 0; _fail = 0; _tag = tag; _logPath = null;
        var host = new GameObject("HH268_MapProbeHost").AddComponent<ProbeHost>();
        host.Host(Coroutine(host));
    }

    class ProbeHost : MonoBehaviour { public void Host(IEnumerator r) { StartCoroutine(r); } }

    static void Log(string msg, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + msg);
        if (ok) _pass++; else _fail++;
        Debug.Log("[HH268] " + (ok ? "✓ " : "✗ ") + msg);
        Flush();
    }
    static void Info(string msg) { _log.AppendLine("      " + msg); Flush(); }

    static void Flush()
    {
        try
        {
            if (_logPath == null)
                _logPath = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh268_map_" + _tag + ".log"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_logPath));
            System.IO.File.WriteAllText(_logPath, _log.ToString());
        }
        catch { }
    }

    static IEnumerator Coroutine(ProbeHost host)
    {
        var cfg = new NewGameConfig
        {
            mapSeed = Seed, worldSeed = Seed, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = "smoke_hh268", kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 1f);
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null || KingdomRegistry.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Log("等世界就绪超时", false); Finish(host); yield break; }
        }
        yield return new WaitForSeconds(1.5f);

        yield return Snapshot();

        yield return Shot("Logs/hh268_" + _tag + "_overview.png");

        _log.AppendLine("==== HH268(" + _tag + ") 汇总：PASS=" + _pass + " FAIL=" + _fail + " ====");
        Debug.Log("[HH268][SUMMARY]\n" + _log);
        Finish(host);
    }

    // ===================== 快照 =====================

    static IEnumerator Snapshot()
    {
        var map = WorldManager.Instance.ActiveMap;
        var tms = Object.FindObjectsOfType<Tilemap>();
        var gGround = FindTm(tms, "Tilemap_Ground");
        var gFeat = FindTm(tms, "Tilemap_Feature");
        var rGround = gGround != null ? gGround.GetComponent<TilemapRenderer>() : null;
        var rFeat = gFeat != null ? gFeat.GetComponent<TilemapRenderer>() : null;

        _log.AppendLine("── A. 层序/模式实读（E4 复核）");
        foreach (var g in Object.FindObjectsOfType<Grid>())
            _log.AppendLine("   Grid '" + g.name + "' cellSize=" + g.cellSize + " layout=" + g.cellLayout + " swizzle=" + g.cellSwizzle);
        foreach (var tm in tms)
        {
            var r = tm.GetComponent<TilemapRenderer>();
            _log.AppendLine("   Tilemap '" + tm.name + "' cellSize=" + tm.cellSize + " tileAnchor=" + tm.tileAnchor
                + " order=" + (r != null ? r.sortingOrder : -9999)
                + " mode=" + (r != null ? r.mode.ToString() : "-")
                + " sortOrder=" + (r != null ? r.sortOrder.ToString() : "-")
                + " layer='" + (r != null ? r.sortingLayerName : "-") + "'");
        }
        foreach (var c in Object.FindObjectsOfType<Camera>())
            Info("   Camera '" + c.name + "' ortho=" + c.orthographic + " size=" + c.orthographicSize
                 + " sortMode=" + c.transparencySortMode + " sortAxis=" + c.transparencySortAxis);

        _log.AppendLine("── B. ①判据：GetCellCenterWorld − GridToIso（应 →0；基线 = (0,+0.32)）");
        double maxAbs = 0;
        if (gGround != null)
        {
            var probe = new Vector3Int[] { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(3, 5, 0) };
            foreach (var c in probe)
            {
                Vector3 cc = gGround.GetCellCenterWorld(c);
                Vector2 iso = MapRenderService.GridToIso(new GridCoord(c.x, c.y));
                double dx = cc.x - iso.x, dy = cc.y - iso.y;
                maxAbs = System.Math.Max(maxAbs, System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)));
                _log.AppendLine("   cell " + c + " tileCenter=" + cc.ToString("0.####") + " iso=" + iso.ToString("0.####")
                    + " delta=(" + dx.ToString("0.####") + "," + dy.ToString("0.####") + ")");
            }
        }
        Log("① 锚点偏差 maxAbs=" + maxAbs.ToString("0.####") + " 世界单位（判据 →0 / ≤1px=0.01）", maxAbs <= 0.0100001);

        _log.AppendLine("── C. M1 现值快照（几何/导入设置）");
        string[] geo = new string[] { "Assets/_Game/Art/Ground/ground_temperate.png",
                                      "Assets/_Game/Art/Buildings/neutral/features/feat_mine.png",
                                      "Assets/_Game/Art/Buildings/neutral/features/feat_tree_temperate_1.png" };
        foreach (var p in geo)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            var st = new TextureImporterSettings();
            if (ti != null) ti.ReadTextureSettings(st);
            if (s != null)
                _log.AppendLine("   " + System.IO.Path.GetFileName(p) + " tex=" + s.texture.width + "x" + s.texture.height
                    + " pivot=" + s.pivot + " worldRect=" + s.bounds.size.x.ToString("0.###") + "x" + s.bounds.size.y.ToString("0.###")
                    + " | importer pivot=" + st.spritePivot + " align=" + st.spriteAlignment
                    + " mesh=" + (st.spriteMeshType == SpriteMeshType.Tight ? "Tight" : "FullRect")
                    + " filter=" + st.filterMode + " ppu=" + st.spritePixelsPerUnit + " mode=" + st.spriteMode);
        }
        Info("   现行 culling bounds  Ground=" + Cull(rGround) + "  Feature=" + Cull(rFeat) + "（DetectChunkCullingBounds=Auto ⇒ 运行时自动算）");
        Info("   现行 sortOrder     Ground=" + (rGround != null ? rGround.sortOrder.ToString() : "-") + "  Feature=" + (rFeat != null ? rFeat.sortOrder.ToString() : "-"));

        _log.AppendLine("── D. ③判据：Mine 占位块精确分解（贪婪读序 2×2）");
        int mineCells = 0, mineBlocks = 0, leftover = 0;
        var counts = new Dictionary<FeatureType, int>();
        if (map != null)
        {
            int W = map.width, H = map.height;
            var used = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var f = map.features[y * W + x];
                    if (!counts.ContainsKey(f)) counts[f] = 0;
                    counts[f]++;
                    if (f != FeatureType.Mine) continue;
                    mineCells++;
                    if (used[y * W + x]) continue;
                    bool full = x + 1 < W && y + 1 < H
                        && map.features[y * W + x + 1] == FeatureType.Mine && !used[y * W + x + 1]
                        && map.features[(y + 1) * W + x] == FeatureType.Mine && !used[(y + 1) * W + x]
                        && map.features[(y + 1) * W + x + 1] == FeatureType.Mine && !used[(y + 1) * W + x + 1];
                    if (!full) { leftover++; continue; }
                    used[y * W + x] = used[y * W + x + 1] = used[(y + 1) * W + x] = used[(y + 1) * W + x + 1] = true;
                    mineBlocks++;
                }
            Info("   全图格数=" + (W * H) + "  Tree=" + C(counts, FeatureType.Tree) + " Mountain=" + C(counts, FeatureType.Mountain)
                 + " SnowMountain=" + C(counts, FeatureType.SnowMountain) + " OreVein=" + C(counts, FeatureType.OreVein)
                 + " StonePile=" + C(counts, FeatureType.StonePile) + " WoodPile=" + C(counts, FeatureType.WoodPile));
        }
        Log("③ Mine 格=" + mineCells + " ⇒ 2×2 占位块=" + mineBlocks + "（合并后应渲 " + mineBlocks + " 张，逐格旧法=" + mineCells
            + " 张）；残余非整块格=" + leftover + "（应 0）", mineCells == mineBlocks * 4 && leftover == 0);

        _log.AppendLine("── E. DrawCall 基线（H6 读数=80.98）");
        int dc0 = DrawCalls();
        Info("   UnityStats.drawCalls=" + dc0 + " / batches=" + Batches());

        _log.AppendLine("── F. 候选 (e) 代价实测（运行中改 mode / order，测毕还原；**不落盘**）");
        if (rGround != null && rFeat != null)
        {
            var mG = rGround.mode; var mF = rFeat.mode;
            var oG = rGround.sortingOrder; var oF = rFeat.sortingOrder;

            yield return null; yield return null;
            int baseDc = DrawCalls();
            rFeat.mode = TilemapRenderer.Mode.Individual;
            yield return null; yield return null; yield return null;
            int featInd = DrawCalls();
            rGround.mode = TilemapRenderer.Mode.Individual;
            yield return null; yield return null; yield return null;
            int bothInd = DrawCalls();
            rGround.mode = mG; rFeat.mode = mF;
            yield return null; yield return null;
            rFeat.sortingOrder = 1;                       // ④：特征 10 → 1（与单位/建筑同序）
            yield return null; yield return null; yield return null;
            int featOrder1 = DrawCalls();
            rFeat.sortingOrder = oF;
            yield return null; yield return null; yield return null;
            int restored = DrawCalls();

            Info("   drawCalls  Chunk(基线)=" + baseDc + "  Feature=Individual=" + featInd + "  两层=Individual=" + bothInd
                 + "  Feature order 10→1=" + featOrder1 + "  还原=" + restored);
            Log("F 候选 (e) 代价：Individual 代价实测在场（Δ_feat=" + (featInd - baseDc) + " / Δ_both=" + (bothInd - baseDc)
                + "）；④ order 复位 Δ=" + (featOrder1 - baseDc), true);
            Log("F 测毕已还原（mode=" + rGround.mode + "/" + rFeat.mode + " order=" + rGround.sortingOrder + "/" + rFeat.sortingOrder + "）",
                rGround.mode == mG && rFeat.mode == mF && rGround.sortingOrder == oG && rFeat.sortingOrder == oF);
        }

        _log.AppendLine("── G. ③运行中实例计数（特征层 feat_mine 实名 tile vs 已加载区 Mine 占位块）");
        if (gFeat != null && map != null)
        {
            int featMineTiles = 0, otherFeatTiles = 0;
            var cb = gFeat.cellBounds;
            for (int y = cb.yMin; y < cb.yMax; y++)
                for (int x = cb.xMin; x < cb.xMax; x++)
                {
                    var sp = gFeat.GetSprite(new Vector3Int(x, y, 0));
                    if (sp == null) continue;
                    if (sp.name.StartsWith("feat_mine")) featMineTiles++; else otherFeatTiles++;
                }
            int loadedMineCells = 0, loadedMineBlocks = 0;
            var used2 = new HashSet<long>();
            for (int y = cb.yMin; y < cb.yMax; y++)
                for (int x = cb.xMin; x < cb.xMax; x++)
                {
                    if (x < 0 || y < 0 || x >= map.width || y >= map.height) continue;
                    if (map.features[y * map.width + x] != FeatureType.Mine) continue;
                    loadedMineCells++;
                    long k = (long)x * 100000 + y;
                    if (used2.Contains(k)) continue;
                    bool full = x + 1 < map.width && y + 1 < map.height
                        && map.features[y * map.width + x + 1] == FeatureType.Mine
                        && map.features[(y + 1) * map.width + x] == FeatureType.Mine
                        && map.features[(y + 1) * map.width + x + 1] == FeatureType.Mine;
                    if (!full) continue;
                    used2.Add((long)x * 100000 + y); used2.Add((long)(x + 1) * 100000 + y);
                    used2.Add((long)x * 100000 + y + 1); used2.Add((long)(x + 1) * 100000 + y + 1);
                    loadedMineBlocks++;
                }
            _log.AppendLine("   特征层已加载区：feat_mine tile=" + featMineTiles + " ／其他特征 tile=" + otherFeatTiles
                + "；地图同区 Mine 格=" + loadedMineCells + " 占位块=" + loadedMineBlocks);
            Log("③ 运行中计数：feat_mine tile=" + featMineTiles + " ⇒ 应 == 占位块数 " + loadedMineBlocks + "（逐格旧法=" + loadedMineCells + "）",
                loadedMineBlocks > 0 && featMineTiles == loadedMineBlocks);
        }

        Flush();
    }

    static int C(Dictionary<FeatureType, int> d, FeatureType f) { int v; return d.TryGetValue(f, out v) ? v : 0; }

    static string Cull(TilemapRenderer r)
    {
        if (r == null) return "n/a";
        var p = typeof(TilemapRenderer).GetProperty("chunkCullingBounds", BindingFlags.Public | BindingFlags.Instance);
        if (p == null) return "(no-api)";
        return p.GetValue(r, null).ToString();
    }

    static Tilemap FindTm(Tilemap[] all, string name)
    {
        foreach (var t in all) if (t.name == name) return t;
        return null;
    }

    static int DrawCalls() { try { return UnityStats.drawCalls; } catch { return -1; } }
    static int Batches() { try { return UnityStats.batches; } catch { return -1; } }

    static IEnumerator Shot(string rel)
    {
        string abs = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", rel));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(abs));
        if (System.IO.File.Exists(abs)) System.IO.File.Delete(abs);
        ScreenCapture.CaptureScreenshot(rel);
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        bool ok = System.IO.File.Exists(abs);
        Info("  截图 " + rel + " 落盘=" + ok);
        Log("截图取证：" + rel, ok);
    }

    static void Finish(ProbeHost host)
    {
        _log.AppendLine("==== HH268(" + _tag + ") 收尾：PASS=" + _pass + " FAIL=" + _fail + " ====");
        Flush();
        TestHarnessApi.ExitTestRun();
        SmokeApi.QuitSmoke();
    }
}
#endif
