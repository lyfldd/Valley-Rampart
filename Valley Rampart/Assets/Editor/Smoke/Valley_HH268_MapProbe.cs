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
///   J ④ 层序双证：J1 结构（Territory 层序严格 < Feature/单位/建筑 ⇒ D443 探针 8）＋ J2 视觉 A/B
///     （染色铺红菱：seqA 现层序 vs seqB 反证 order=50 压实体）＋ J3 正向（单位置于特征块正前方 ⇒ 可见）
/// 证据：`Logs/hh268_map_{tag}.log` ＋ `Logs/hh268_{tag}_*.png`。
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
        var tms = Object.FindObjectsOfType<Tilemap>(true);   // includeInactive：近景档 Territory 层整体 SetActive(false)（D449/D451）
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

        // A2（HH.268 落地判据）：三 Tilemap 锚点/层序 **运行中实名读数** + 断言（L-29：不靠文本 grep）
        var tTerr = FindTm(tms, "Tilemap_Territory");
        bool terrWasActive = tTerr != null && tTerr.gameObject.activeSelf;
        if (tTerr != null && !terrWasActive) tTerr.gameObject.SetActive(true);   // 量测前临时激活（测毕还原；同帧内同步读数）
        Vector3 ancG = gGround != null ? gGround.tileAnchor : new Vector3(-9f, -9f, -9f);
        Vector3 ancF = gFeat != null ? gFeat.tileAnchor : new Vector3(-9f, -9f, -9f);
        Vector3 ancT = tTerr != null ? tTerr.tileAnchor : new Vector3(-9f, -9f, -9f);
        int ordG = OrderOf(gGround), ordF = OrderOf(gFeat), ordT = OrderOf(tTerr);
        var rTerr = tTerr != null ? tTerr.GetComponent<TilemapRenderer>() : null;
        _log.AppendLine("   [①] tileAnchor      Ground=" + ancG + "  Feature=" + ancF + "  Territory=" + ancT);
        _log.AppendLine("   [④-1′] sortingOrder Ground=" + ordG + "  Territory=" + ordT + "  Feature=" + ordF);
        Log("① 三处 tileAnchor 同批归一 (0,0,0)",
            ancG == Vector3.zero && ancF == Vector3.zero && ancT == Vector3.zero);
        Log("④-1′ 层序实读 = Ground −1 ｜ Territory 0 ｜ Feature 1",
            ordG == -1 && ordT == 0 && ordF == 1);

        foreach (var c in Object.FindObjectsOfType<Camera>())
            Info("   Camera '" + c.name + "' ortho=" + c.orthographic + " size=" + c.orthographicSize
                 + " sortMode=" + c.transparencySortMode + " sortAxis=" + c.transparencySortAxis);

        _log.AppendLine("── B. ①判据：三 Tilemap GetCellCenterWorld − GridToIso（应 →0；基线 = (0,+0.32)）");
        var probeCells = new Vector3Int[] { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(3, 5, 0) };
        double maxAbs = 0;
        foreach (var tmx in new Tilemap[] { gGround, gFeat, tTerr })
        {
            if (tmx == null) { Log("① 缺 Tilemap（null）", false); continue; }
            double mOne = 0;
            foreach (var c in probeCells)
            {
                Vector3 cc = tmx.GetCellCenterWorld(c);
                Vector2 iso = MapRenderService.GridToIso(new GridCoord(c.x, c.y));
                double dx = cc.x - iso.x, dy = cc.y - iso.y;
                mOne = System.Math.Max(mOne, System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)));
                _log.AppendLine("   " + tmx.name + " cell " + c + " tileCenter=" + cc.ToString("0.####")
                    + " iso=" + iso.ToString("0.####") + " delta=(" + dx.ToString("0.####") + "," + dy.ToString("0.####") + ")");
            }
            maxAbs = System.Math.Max(maxAbs, mOne);
            Log("① " + tmx.name + " 锚点偏差 maxAbs=" + mOne.ToString("0.####") + " 世界单位（判据 →0 / ≤1px=0.01）", mOne <= 0.0100001);
        }
        if (tTerr != null && !terrWasActive) tTerr.gameObject.SetActive(false);   // 还原近景档「整层隐藏」

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
            // ④ 层序「前后」对照：临时回旧链（Ground 0 ｜ Territory 5 ｜ Feature 10），测毕还原为落地链（−1／0／1）
            int oT = OrderOf(tTerr);
            rGround.sortingOrder = 0;
            if (rTerr != null) rTerr.sortingOrder = 5;
            rFeat.sortingOrder = 10;
            yield return null; yield return null; yield return null;
            int dcOldChain = DrawCalls();
            rGround.sortingOrder = oG; rFeat.sortingOrder = oF;
            if (rTerr != null) rTerr.sortingOrder = oT;
            yield return null; yield return null; yield return null;
            int restored = DrawCalls();

            Info("   drawCalls  Chunk(基线)=" + baseDc + "  Feature=Individual=" + featInd + "  两层=Individual=" + bothInd
                 + "  旧链(0/5/10)=" + dcOldChain + "  还原落地链(−1/0/1)=" + restored);
            Log("F 候选 (e) 代价（② 销项留痕）：Individual 代价实测在场（Δ_feat=" + (featInd - baseDc) + " / Δ_both=" + (bothInd - baseDc)
                + "）", featInd > baseDc);
            Log("F ④-1′ 层序前后 Δ=" + (restored - dcOldChain) + "（旧链 " + dcOldChain + " ⇒ 落地链 " + restored + "）",
                rGround.mode == mG && rFeat.mode == mF && rGround.sortingOrder == oG && rFeat.sortingOrder == oF
                && OrderOf(tTerr) == oT);
        }

        _log.AppendLine("── G. ③运行中实例计数（每个 2×2 占位块应恰 1 张 feat_mine）");
        if (gFeat != null && map != null)
        {
            var cb = gFeat.cellBounds;
            int mineTiles = 0, otherTiles = 0;
            for (int y = cb.yMin; y < cb.yMax; y++)
                for (int x = cb.xMin; x < cb.xMax; x++)
                {
                    var sp = gFeat.GetSprite(new Vector3Int(x, y, 0));
                    if (sp == null) continue;
                    if (sp.name.StartsWith("feat_mine")) mineTiles++; else otherTiles++;
                }

            var consumed = new HashSet<long>();
            int ok1 = 0, multi = 0, zero = 0;
            for (int y = cb.yMin; y < cb.yMax; y++)
                for (int x = cb.xMin; x < cb.xMax; x++)
                {
                    if (x < 0 || y < 0 || x + 1 >= map.width || y + 1 >= map.height) continue;
                    if (map.features[y * map.width + x] != FeatureType.Mine) continue;
                    long k = (long)x * 100000 + y;
                    if (consumed.Contains(k)) continue;
                    bool full = map.features[y * map.width + x + 1] == FeatureType.Mine
                             && map.features[(y + 1) * map.width + x] == FeatureType.Mine
                             && map.features[(y + 1) * map.width + x + 1] == FeatureType.Mine;
                    if (!full) continue;
                    consumed.Add(k); consumed.Add((long)(x + 1) * 100000 + y);
                    consumed.Add((long)x * 100000 + y + 1); consumed.Add((long)(x + 1) * 100000 + y + 1);
                    // 「已加载」判据＝该格地皮 tile 在场（地皮全覆盖 ⇒ 等价于该格已铺格）；四格全在场才算一个完整可比块
                    if (gGround == null || gGround.GetTile(new Vector3Int(x, y, 0)) == null
                        || gGround.GetTile(new Vector3Int(x + 1, y, 0)) == null
                        || gGround.GetTile(new Vector3Int(x, y + 1, 0)) == null
                        || gGround.GetTile(new Vector3Int(x + 1, y + 1, 0)) == null) continue;
                    int cnt = 0;
                    for (int dy = 0; dy < 2; dy++)
                        for (int dx = 0; dx < 2; dx++)
                        {
                            var sp = gFeat.GetSprite(new Vector3Int(x + dx, y + dy, 0));
                            if (sp != null && sp.name.StartsWith("feat_mine")) cnt++;
                        }
                    if (cnt == 1) ok1++; else if (cnt > 1) multi++; else zero++;
                }
            int blocks = ok1 + multi + zero;
            _log.AppendLine("   特征层已加载区：feat_mine tile=" + mineTiles + " ／其他特征 tile=" + otherTiles
                + "；四格全加载的完整 2×2 占位块=" + blocks + " ⇒ 恰 1 张=" + ok1 + " ／多张=" + multi + " ／零张=" + zero
                + "（tile 与块数之差=" + (mineTiles - ok1) + "，为跨加载边界的块）");
            Log("③ 运行中实例计数：**四格全加载的完整块 " + blocks + " 个 ⇒ 恰 1 张=" + ok1 + "／多张=" + multi + "／零张=" + zero
                + "**（逐格旧法应为 " + (blocks * 4) + " 张）；feat_mine tile 总数=" + mineTiles,
                blocks > 0 && multi == 0 && zero == 0);
        }

        _log.AppendLine("── H. ⑤ NPC 头顶语句字号实测（世界尺寸）");
        yield return MeasureSpeech();

        _log.AppendLine("── I. ③⑤ 近景截图（运行中临时改相机，测毕还原）");
        yield return Closeup("mine", true);
        yield return Closeup("speech", false);

        _log.AppendLine("── J. 层序双证（D443 探针8 负探针 ＋ 正向：单位在特征块正前方可见）");
        yield return LayerProbe(gGround, gFeat, tTerr);

        Flush();
    }

    /// <summary>近景截图：临时关掉 CameraRig、把相机对准「最近的 Mine 占位块中心」或「最近的单位头顶气泡」，拍完还原。</summary>
    static IEnumerator Closeup(string what, bool toMine)
    {
        var cam = Camera.main;
        if (cam == null) { Log("近景截图：无 Main Camera", false); yield break; }
        var rig = Object.FindObjectOfType<CameraRig>();
        bool rigWas = rig != null && rig.enabled;

        Vector3 target; float ortho;
        if (toMine)
        {
            var map = WorldManager.Instance.ActiveMap;
            var tms = Object.FindObjectsOfType<Tilemap>();
            var gG = FindTm(tms, "Tilemap_Ground");
            Vector3 camPos = cam.transform.position;
            float bestD = float.MaxValue; bool found = false; Vector3 bestP = camPos;
            if (map != null && gG != null)
            {
                var used = new HashSet<long>();
                for (int y = 0; y < map.height; y++)
                    for (int x = 0; x < map.width; x++)
                    {
                        if (map.features[y * map.width + x] != FeatureType.Mine) continue;
                        long k = (long)x * 100000 + y;
                        if (used.Contains(k) || x + 1 >= map.width || y + 1 >= map.height) continue;
                        if (map.features[y * map.width + x + 1] != FeatureType.Mine
                            || map.features[(y + 1) * map.width + x] != FeatureType.Mine
                            || map.features[(y + 1) * map.width + x + 1] != FeatureType.Mine) continue;
                        used.Add(k); used.Add((long)(x + 1) * 100000 + y);
                        used.Add((long)x * 100000 + y + 1); used.Add((long)(x + 1) * 100000 + y + 1);
                        if (gG.GetTile(new Vector3Int(x, y, 0)) == null) continue;
                        Vector2 wp = MapRenderService.GridToIso(new GridCoord(x, y)) + new Vector2(0f, 0.32f);
                        float d = (new Vector3(wp.x, wp.y, 0f) - camPos).sqrMagnitude;
                        if (d < bestD) { bestD = d; bestP = new Vector3(wp.x, wp.y, camPos.z); found = true; }
                    }
            }
            if (!found) { Log("近景截图(" + what + ")：未找到已加载 Mine 块", false); yield break; }
            target = bestP; ortho = 2.0f;
        }
        else
        {
            Vector3 cc = cam.transform.position;
            UnitController best = null; float bd = float.MaxValue;
            foreach (var u in Object.FindObjectsOfType<UnitController>())
            {
                if (u == null || u.transform == null) continue;
                float d = (u.transform.position - cc).sqrMagnitude;
                if (d < bd) { bd = d; best = u; }
            }
            if (best == null) { Log("近景截图(" + what + ")：未找到单位", false); yield break; }
            OverheadSpeech.Show(best.transform, "村民长名A0");
            OverheadSpeech.Show(best.transform, "村民长名A1");
            target = best.transform.position + new Vector3(0f, 0.9f, 0f);
            ortho = 2.0f;
        }

        float orthoWas = cam.orthographicSize;
        if (rig != null) rig.enabled = false;
        cam.transform.position = new Vector3(target.x, target.y, cam.transform.position.z);
        cam.orthographicSize = ortho;

        // ③ 对照标记：在「占位块中心」画小白菱形（sortingOrder 200）⇒ 截图里看 merge sprite 锚点是否与之重合
        GameObject mark = null;
        if (toMine)
        {
            mark = new GameObject("hh268_centerMark");
            mark.transform.position = new Vector3(target.x, target.y, 0f);
            mark.transform.localScale = new Vector3(0.18f, 0.18f, 1f);
            var sr = mark.AddComponent<SpriteRenderer>();
            sr.sprite = MapRenderService.CreateIsoDiamondSprite(new Color(1f, 0.1f, 0.1f, 1f));
            sr.sortingOrder = 200;
        }
        yield return null; yield return null;
        yield return Shot("Logs/hh268_" + _tag + "_closeup_" + what + ".png");
        if (mark != null) Object.Destroy(mark);
        cam.orthographicSize = orthoWas;
        if (rig != null) rig.enabled = rigWas;
        yield return null;
    }

    /// <summary>⑤：把当前 production 字号的气泡挂到「离相机最近」的单位上，读 MeshRenderer 世界包围盒（供"可读且不遮地图"取值）。</summary>
    static IEnumerator MeasureSpeech()
    {
        var cam = Camera.main;
        Vector3 cc = cam != null ? cam.transform.position : Vector3.zero;
        UnitController best = null; float bestD = float.MaxValue;
        foreach (var u in Object.FindObjectsOfType<UnitController>())
        {
            if (u == null || u.transform == null) continue;
            float d = (u.transform.position - cc).sqrMagnitude;
            if (d < bestD) { bestD = d; best = u; }
        }
        if (best == null) { Log("⑤ 未找到单位，无法实测字号", false); yield break; }
        var host = best.transform;
        for (int i = 0; i < 3; i++) OverheadSpeech.Show(host, "村民长名A" + i);
        yield return null; yield return null;

        int found = 0; var detail = new StringBuilder();
        foreach (var t in Object.FindObjectsOfType<TextMesh>())
        {
            if (t == null || t.gameObject.name != "OverheadSpeech") continue;
            var mr = t.GetComponent<MeshRenderer>();
            Vector3 sz = mr != null ? mr.bounds.size : Vector3.zero;
            found++;
            if (detail.Length < 260)
                detail.Append(" [cs=").Append(t.characterSize).Append(" fs=").Append(t.fontSize)
                      .Append(" worldH=").Append(sz.y.ToString("0.####")).Append(" worldW=").Append(sz.x.ToString("0.####")).Append("]");
        }
        Info("   挂点单位=" + best.name + "（距相机 " + Mathf.Sqrt(bestD).ToString("0.##") + "）；气泡 worldH/detail=" + detail);
        Info("   参照：格高 0.64 世界；相机 ortho=8.64（屏高 17.28 世界）");
        Log("⑤ 头顶气泡实测在场 " + found + " 个（缩小后应 ≈0.1~0.35 世界高：可读且不遮图）", found > 0);
    }

    /// <summary>HH.268 ④ 层序双证：
    /// J1 结构（运行中实名读数）：Territory 层序严格低于 Feature/单位/建筑 ⇒ D443 探针 8（染色不盖实体）；
    /// J2 视觉 A/B：在 Mine 块周围临时铺红菱染色片 ⇒ seqA（现层序·染色在实体之下）vs seqB（反证·染色 order=50 压实体），
    ///    两张对照图证明层序真的决定该处像素；测毕清格还原（含被覆盖的原 tile）。
    /// J3 正向：把最近单位移到该 Mine 块「正前方」（格 (bx,by−1) ⇒ iso Y 更小 ⇒ 更靠前）⇒ 截图应见单位在特征物之前。</summary>
    static IEnumerator LayerProbe(Tilemap gG, Tilemap gF, Tilemap gT)
    {
        // ---- J1 结构 ----
        int ordT = OrderOf(gT), ordF = OrderOf(gF), ordU = int.MaxValue, ordB = int.MaxValue;
        foreach (var u in Object.FindObjectsOfType<UnitController>())
        { if (u == null) continue; var sr = u.GetComponent<SpriteRenderer>(); if (sr != null) ordU = Mathf.Min(ordU, sr.sortingOrder); }
        foreach (var b in Object.FindObjectsOfType<Building>())
        { if (b == null) continue; var sr = b.GetComponent<SpriteRenderer>(); if (sr != null) ordB = Mathf.Min(ordB, sr.sortingOrder); }
        int ordUs = ordU == int.MaxValue ? -1 : ordU, ordBs = ordB == int.MaxValue ? -1 : ordB;
        _log.AppendLine("   层序实名读数：Territory=" + ordT + "  Feature=" + ordF + "  单位(min)=" + ordUs + "  建筑(min)=" + ordBs);
        Log("D443 探针8（负探针·结构）：Territory(" + ordT + ") 严格低于 Feature(" + ordF + ")/单位(" + ordUs + ")/建筑(" + ordBs
            + ") ⇒ 染色在任意深度下都不可能盖住特征物/单位/建筑",
            ordT >= 0 && ordT < ordF && ordUs >= 0 && ordT < ordUs && ordBs >= 0 && ordT < ordBs);

        int bx, by; Vector3 center;
        if (!FindNearestMineBlock(gG, out bx, out by, out center))
        { Log("J 层序视觉：未找到已加载 Mine 占位块（视觉段跳过）", false); yield break; }

        var cam = Camera.main;
        var rig = Object.FindObjectOfType<CameraRig>();
        var overlay = Object.FindObjectOfType<TerritoryOverlay>();
        if (cam == null) { Log("J 层序视觉：无 Main Camera", false); yield break; }
        bool rigWas = rig != null && rig.enabled, overlayWas = overlay != null && overlay.enabled;
        float orthoWas = cam.orthographicSize;
        if (overlay != null) overlay.enabled = false;                  // 冻结染色层自身刷新 ⇒ A/B 只受层序影响
        if (rig != null) rig.enabled = false;
        if (gT != null && !gT.gameObject.activeSelf) gT.gameObject.SetActive(true);
        cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);
        cam.orthographicSize = 2.0f;
        yield return null; yield return null;

        // ---- J2 视觉 A/B ----
        var rT = gT != null ? gT.GetComponent<TilemapRenderer>() : null;
        int ordTSaved = OrderOf(gT);
        var red = ScriptableObject.CreateInstance<Tile>();
        red.sprite = MapRenderService.CreateIsoDiamondSprite(new Color(1f, 0.12f, 0.12f, 0.6f));
        var cells = new List<Vector3Int>();
        var saved = new Dictionary<long, TileBase>();
        for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
            {
                var pos = new Vector3Int(bx + dx, by + dy, 0);
                saved[(long)pos.x * 100000 + pos.y] = gT.GetTile(pos);
                cells.Add(pos);
                gT.SetTile(pos, red);
            }
        yield return null; yield return null;
        yield return Shot("Logs/hh268_" + _tag + "_layer_seqA_territory_below.png");
        if (rT != null) rT.sortingOrder = 50;                           // 反证：染色提到实体之上
        yield return null; yield return null;
        yield return Shot("Logs/hh268_" + _tag + "_layer_seqB_territory_above_control.png");
        if (rT != null) rT.sortingOrder = ordTSaved;
        Log("J2 视觉 A/B 落盘：seqA（现层序·染色 < 实体）／seqB（反证·染色 order=50 压实体）", true);
        foreach (var pos in cells) gT.SetTile(pos, saved[(long)pos.x * 100000 + pos.y]);
        Object.Destroy(red);
        yield return null;

        // ---- J3 正向 ----
        UnitController uu = null; float bd = float.MaxValue;
        foreach (var u in Object.FindObjectsOfType<UnitController>())
        { if (u == null || u.transform == null) continue; float d = (u.transform.position - center).sqrMagnitude; if (d < bd) { bd = d; uu = u; } }
        if (uu != null)
        {
            Vector3 was = uu.transform.position;
            Vector2 front = MapRenderService.GridToIso(new GridCoord(bx, by - 1));
            uu.transform.position = new Vector3(front.x, front.y, was.z);
            cam.transform.position = new Vector3(front.x, front.y, cam.transform.position.z);
            yield return null; yield return null;
            var srU = uu.GetComponent<SpriteRenderer>();
            _log.AppendLine("   J3 单位 '" + uu.name + "' order=" + (srU != null ? srU.sortingOrder : -9999)
                + " ／ Feature order=" + ordF + "（同序 ⇒ 靠全局 CustomAxis(0,1,0) 的 Y 深度分胜负）；置于格 (" + bx + "," + (by - 1) + ") = 特征块正前方");
            yield return Shot("Logs/hh268_" + _tag + "_unit_in_front_of_feature.png");
            uu.transform.position = was;
        }
        else Log("J3 正向：未找到单位（跳过）", false);

        cam.orthographicSize = orthoWas;
        if (rig != null) rig.enabled = rigWas;
        if (overlay != null) overlay.enabled = overlayWas;
        yield return null;
    }

    static int OrderOf(Tilemap t)
    {
        if (t == null) return -9999;
        var r = t.GetComponent<TilemapRenderer>();
        return r != null ? r.sortingOrder : -9999;
    }

    /// <summary>找「离相机最近的、已加载（锚格地皮 tile 在场）的 Mine 2×2 占位块」；center ＝ 块中心（锚格 iso ＋ (0,+0.32)，与 merge sprite 落点同式）。</summary>
    static bool FindNearestMineBlock(Tilemap gG, out int bx, out int by, out Vector3 center)
    {
        bx = by = -1; center = Vector3.zero;
        var map = WorldManager.Instance.ActiveMap;
        var cam = Camera.main;
        if (map == null || gG == null || cam == null) return false;
        float best = float.MaxValue; bool found = false;
        var used = new HashSet<long>();
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                if (map.features[y * map.width + x] != FeatureType.Mine) continue;
                long k = (long)x * 100000 + y;
                if (used.Contains(k) || x + 1 >= map.width || y + 1 >= map.height) continue;
                if (map.features[y * map.width + x + 1] != FeatureType.Mine
                    || map.features[(y + 1) * map.width + x] != FeatureType.Mine
                    || map.features[(y + 1) * map.width + x + 1] != FeatureType.Mine) continue;
                used.Add(k); used.Add((long)(x + 1) * 100000 + y);
                used.Add((long)x * 100000 + y + 1); used.Add((long)(x + 1) * 100000 + y + 1);
                if (gG.GetTile(new Vector3Int(x, y, 0)) == null) continue;
                Vector2 wp = MapRenderService.GridToIso(new GridCoord(x, y)) + new Vector2(0f, 0.32f);
                float d = (new Vector3(wp.x, wp.y, 0f) - cam.transform.position).sqrMagnitude;
                if (d < best) { best = d; bx = x; by = y; center = new Vector3(wp.x, wp.y, 0f); found = true; }
            }
        return found;
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
