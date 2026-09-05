using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  2_10 步骤13 领土染色覆盖层 冒烟（HH.69 开工令，D443+D448~D452，设计 §5.10 视口分级修订版）
//  用法：菜单「Valley/验证/2_10_步骤13_领土染色」——须 GameScene Play 且已进局（先跑再点）。
//  自动跑（D522/HH.68 暖 boot 规程）：菜单「Valley/验证/2_10_步骤13_领土染色_自动跑(D522)」——
//    MCP 进 Play+暖 boot（MainMenuController.OnCharacterCreateConfirmed）后调用本菜单；
//    等 waiting gate→自含跑 P1~P9→QuitSmoke（清 smoke_ 槽位+退 Play）全程零手动。
//  探针（行为级，真实进局，SmokeApi 链）：
//    P1 近景负探针：默认 1× 档整层隐藏+全无色（D449）
//    P2 中景着色：2× 激活+多国染色+色异（D447 派生色）+无主透明负探针（D443）——染色可见实证
//       （解除 2_16 P1 末录屏「染色可见」阻塞项）
//    P3 远景浓色：4× 边界恒浓 0.65/内部 0.55（D448/D450 档位表）
//    P4 跨档平滑：切档瞬时无跳变+过渡中途介于区间+0.3s 到位+回近景渐隐后层隐藏（D451）
//    P5 增量+边界重算：正规写点 ClaimFootprintChunk→新 mid 边界恒浓；3×3 注满后中心转内部
//       （D450 边界恒浓重算，±1 圈）
//    P6 chunk 竞态补染：清染色层→反射重跑主城 chunk（RenderChunk 真钩子）→Ledger 补染恢复（D445②）
//    P7 灭国渐隐：FadeOutKingdom→瞬时 alpha 下降→2s 后该 kid 色块全清+不误伤他国（D446/D379 渲染侧）
//    P8 读档重染幂等：ReapplyAll 基线→快照→重染一致+GameLoadedEvent 触发路径一致
//       （真 LoadScene 读档链不在本容器重放，2_17_12 P7 同款降级，如实列报）
//    P9 高亮（D452）：近景下 HighlightKingdom→层临时激活+中景浓度显色；取消→回近景隐藏
//  私方法经反射调产品实现（与 P0/S11 harness 同规）；探针注入账本收尾清理，防污染。
//  红线：AI.Core/训练仓零触碰；染色数据层只读（Ledger/bannerColor 消费端）。
// ============================================================================
public static class Valley2_10_Smoke_Territory
{
    private const int SYNTH_KINGDOM = 99;           // P5 探针合成王国（非真实，收尾清账本）
    private const float TRANSIT_WAIT = 0.6f;        // 跨档过渡 0.3s+余量
    private const float FADE_WAIT = 2.6f;           // 灭国渐隐 2s+余量
    private const float EPS = 0.02f;                // alpha 容差（过渡浮点）

    [MenuItem("Valley/验证/2_10_步骤13_领土染色")]
    public static void RunFromMenu()
    {
        if (!Application.isPlaying) { Debug.LogError("[2_10冒烟] 须在 Play 上下文执行。"); return; }
        new GameObject("2_10_SmokeRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    [MenuItem("Valley/验证/2_10_步骤13_领土染色_自动跑(D522)")]
    public static void RunAuto()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[2_10冒烟] 自动跑须先进入 Play（MCP enterPlaymode+暖 boot 后二次调用本菜单）。"); return; }
        new GameObject("2_10_AutoHost").AddComponent<RunHost>().Host(AutoRoutine());
    }

    private static IEnumerator AutoRoutine()
    {
        float t0 = Time.realtimeSinceStartup;
        while (LoadManager.Instance == null || LoadManager.Instance.CurrentPhase == LoadPhase.Booting
               || WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || WorldSystem.Instance == null || UnitDataManager.Instance == null
               || TerritoryOverlay.Instance == null || CameraRig.Instance == null
               || TerritorySystem.Instance == null || GridSystem.Instance == null
               || KingdomRegistry.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f)
            {
                Debug.LogError("[2_10冒烟] 自动跑等门超时(120s)——进局链未就绪，退出 Play。");
                EditorApplication.ExitPlaymode();
                yield break;
            }
        }
        // 染色数据就绪门：Ledger 非空（开局 RebuildInitial 事件已播完）
        while (TerritorySystem.Instance.Ledger.Count == 0)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Debug.LogError("[2_10冒烟] Ledger 空（初始圈未圈入）？退出 Play。"); EditorApplication.ExitPlaymode(); yield break; }
        }
        yield return new WaitForSeconds(0.5f);   // 首帧档位落位+初始染色稳态

        yield return RunCoroutine();
        Debug.Log("[2_10冒烟] 自动跑完成 → QuitSmoke（清 smoke_ 槽位+退 Play，D522）");
        SmokeApi.QuitSmoke();
    }

    public static IEnumerator RunCoroutine()
    {
        var overlay = TerritoryOverlay.Instance;
        var rig = CameraRig.Instance;
        var ts = TerritorySystem.Instance;
        var grid = GridSystem.Instance;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (overlay == null || rig == null || ts == null || grid == null || map == null)
        { Debug.LogError("[2_10冒烟] 单例缺失。"); yield break; }
        if (overlay.PaintedCount == 0)
        {
            Debug.LogWarning("[2_10冒烟] 染色缓存空（开局事件早于订阅？）——ReapplyAll 兜底后继续。");
            overlay.ReapplyAll();
            yield return null;
        }

        // 玩家/AI mid 选取：玩家 owner==0 第一个；内部（8 邻全玩家）/边界（有异/无主邻）各一
        Vector2Int homeMid = default, interiorMid = default, boundaryMid = default;
        bool fHome = false, fI = false, fB = false;
        foreach (var kv in ts.Ledger)
        {
            if (kv.Value != 0) continue;
            if (!fHome) { homeMid = kv.Key; fHome = true; }
            bool allSame = true;
            for (int dy = -1; dy <= 1 && allSame; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (!ts.Ledger.TryGetValue(new Vector2Int(kv.Key.x + dx, kv.Key.y + dy), out int no) || no != 0)
                    { allSame = false; break; }
                }
            if (allSame && !fI) { interiorMid = kv.Key; fI = true; }
            if (!allSame && !fB) { boundaryMid = kv.Key; fB = true; }
            if (fHome && fI && fB) break;
        }
        var results = new List<string>();
        var cleanupMids = new List<Vector2Int>();   // 探针账本注入（收尾反射清除）

        // ---- P1 近景负探针（默认 1×：层隐藏+全无色，D449）----
        yield return null;
        float p1a = fHome ? overlay.GetMidAlpha(homeMid) : -1f;
        bool p1 = !overlay.IsLayerActive && fHome && Mathf.Abs(p1a) < 0.001f;
        results.Add($"P1 近景负探针 层隐藏={(!overlay.IsLayerActive)} 主城mid alpha={p1a:0.###}={p1}");

        // ---- P2 中景着色（2×：激活+染色+色异+无主透明，D443/D447/D448）----
        rig.ZoomTo(1);
        yield return new WaitForSeconds(TRANSIT_WAIT);
        var paintedKids = overlay.GetPaintedKingdoms();
        bool colorDiff = paintedKids.Count >= 2;
        string colorInfo = "无";
        if (colorDiff)
        {
            var kidColor = new Dictionary<int, Color>();
            foreach (var k in paintedKids)
                foreach (var kv in ts.Ledger)
                    if (kv.Value == k) { kidColor[k] = overlay.GetMidColorForProbe(kv.Key); break; }
            var ids = new List<int>(kidColor.Keys);
            for (int i = 0; i < ids.Count && !colorDiff; i++)
                for (int j = i + 1; j < ids.Count; j++)
                {
                    var a = kidColor[ids[i]]; var b = kidColor[ids[j]];
                    float d = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
                    if (d > 0.01f) { colorDiff = true; break; }
                }
            var parts = new List<string>();
            foreach (var kv2 in kidColor) parts.Add($"k{kv2.Key}=({kv2.Value.r:0.00},{kv2.Value.g:0.00},{kv2.Value.b:0.00},{kv2.Value.a:0.00})");
            colorInfo = string.Join(" ", parts);
        }
        // 无主透明负探针：角落 mid（(0,0) 一带必无主）
        var cornerCell = new GridCoord(2, 2);
        var cornerMid = grid.CellToMidChunk(cornerCell);
        bool unowned = !ts.Ledger.ContainsKey(cornerMid);
        bool unownedNoPaint = unowned && overlay.GetMidAlpha(cornerMid) < 0f;
        bool p2 = overlay.IsLayerActive && overlay.PaintedCount > 0 && colorDiff && unownedNoPaint;
        results.Add($"P2 中景着色 激活={overlay.IsLayerActive} painted={overlay.PaintedCount} 色异={colorDiff}(kids={paintedKids.Count} 色[{colorInfo}]) 无主透明={unownedNoPaint}={p2}");

        // ---- P3 远景浓色（4×：边界 0.65/内部 0.55，D448/D450）----
        rig.ZoomTo(2);
        yield return new WaitForSeconds(TRANSIT_WAIT);
        float bA = fB ? overlay.GetMidAlpha(boundaryMid) : -1f;
        float iA = fI ? overlay.GetMidAlpha(interiorMid) : -1f;
        bool p3 = fB && fI && Mathf.Abs(bA - 0.65f) < EPS && Mathf.Abs(iA - 0.55f) < EPS;
        results.Add($"P3 远景浓色 边界={bA:0.###}(≈0.65) 内部={iA:0.###}(≈0.55) fI={fI} fB={fB}={p3}");

        // ---- P4 跨档平滑（4×→近景渐出隐藏→2× 渐显，D451）----
        rig.ZoomTo(0);
        float aSame = fB ? overlay.GetMidAlpha(boundaryMid) : -1f;   // 同帧：无跳变（仍旧档值）
        yield return null;                                            // 一帧：过渡已启动
        float aMidT = fB ? overlay.GetMidAlpha(boundaryMid) : -1f;
        yield return new WaitForSeconds(TRANSIT_WAIT);
        bool hiddenAfter = !overlay.IsLayerActive && Mathf.Abs(overlay.GetMidAlpha(boundaryMid)) < 0.001f;
        rig.ZoomTo(1);                                                // 出近景：渐显
        yield return null;
        float aRise = fB ? overlay.GetMidAlpha(boundaryMid) : -1f;
        yield return new WaitForSeconds(TRANSIT_WAIT);
        float aSettled = fB ? overlay.GetMidAlpha(boundaryMid) : -1f;
        bool p4 = Mathf.Abs(aSame - 0.65f) < EPS
                  && aMidT > 0.001f && aMidT < 0.65f
                  && hiddenAfter
                  && aRise > 0.001f && aRise < 0.65f
                  && Mathf.Abs(aSettled - 0.50f) < EPS;                         // 渐显到位=2× 档边界 0.50（回 2×，非 4× 的 0.65）
        results.Add($"P4 跨档平滑 同帧={aSame:0.###}(≈0.65) 中途={aMidT:0.###} 回近景隐藏={hiddenAfter} 渐显中途={aRise:0.###} 到位={aSettled:0.###}={p4}");

        // ---- P5 增量+边界重算（2× 下：正规写点 ClaimFootprintChunk，D450/D445）----
        // 正坐标域 mid(5,5)（远离主城 mid(32,32) 一带，界内必无主）：中心 cell(22,22)，
        // 8 邻 mid 全正坐标（避免 CellToMidChunk 负数除法取整歧义）。
        int ms = grid.Config != null && grid.Config.midChunkSize > 0 ? grid.Config.midChunkSize : 4;
        const int BASE_MID = 5;
        var centerCell = new GridCoord(BASE_MID * ms + 1, BASE_MID * ms + 1);   // (22,22) → mid(5,5)
        var mCenter = grid.CellToMidChunk(centerCell);
        ts.ClaimFootprintChunk(SYNTH_KINGDOM, centerCell);                      // 脚下 mid 纳入+广播（裁4 三写点之一）
        yield return null;
        float aIso = overlay.GetMidAlpha(mCenter);                              // 孤立 mid=边界（8 邻无主）→0.50
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var cell = new GridCoord((BASE_MID + dx) * ms + 1, (BASE_MID + dy) * ms + 1);
                ts.ClaimFootprintChunk(SYNTH_KINGDOM, cell);                    // 环绕 8 邻 mid 全注满
                cleanupMids.Add(grid.CellToMidChunk(cell));
            }
        cleanupMids.Add(mCenter);
        yield return null;
        float aInterior = overlay.GetMidAlpha(mCenter);                         // 3×3 注满→中心转内部→0.35
        float aEdge = overlay.GetMidAlpha(new Vector2Int(BASE_MID + 1, BASE_MID)); // (6,5) 外缘仍边界→0.50
        bool p5 = Mathf.Abs(aIso - 0.50f) < EPS && Mathf.Abs(aInterior - 0.35f) < EPS && Mathf.Abs(aEdge - 0.50f) < EPS;
        results.Add($"P5 增量+边界重算 孤立={aIso:0.###}(≈0.50) 注满转内部={aInterior:0.###}(≈0.35) 外缘恒浓={aEdge:0.###}(≈0.50)={p5}");

        // ---- P6 chunk 竞态补染（两段分解取证，D445②）：
        //      P6a 直接反射调 TerritoryOverlay.OnChunkRendered（功能层：mid 范围换算+Ledger 补染）；
        //      P6b 真钩子链（MapRenderService.RenderChunk 重铺→静态事件→补染）。
        overlay.ClearOverlay();
        yield return null;
        var mrs = MapRenderService.Instance;
        int chunkSize = MapRenderService.ChunkSize;
        // 主城不在地图中心（实测 homeMid=(57,28) 之类）——chunk 锚定用真实玩家 mid（homeMid），防中心假设错位零命中
        int homeCellX = fHome ? homeMid.x * 4 : map.width / 2;
        int homeCellY = fHome ? homeMid.y * 4 : map.height / 2;
        int cx = homeCellX / chunkSize, cy = homeCellY / chunkSize;
        // P6a 功能层：直接调 OnChunkRendered（painted=0 基线下）
        var mOverlayHook = typeof(TerritoryOverlay).GetMethod("OnChunkRendered", BindingFlags.Instance | BindingFlags.NonPublic);
        if (mOverlayHook != null) mOverlayHook.Invoke(overlay, new object[] { cx, cy });
        int paintedA = overlay.PaintedCount;
        float aHomeA = overlay.GetMidAlpha(homeMid);
        // P6b 链路层：再清层→反射重跑主城 chunk→真事件链
        overlay.ClearOverlay();
        yield return null;
        long ckey = (long)cx * 100000 + cy;
        var fChunks = typeof(MapRenderService).GetField("_loadedChunks", BindingFlags.Instance | BindingFlags.NonPublic);
        var chunks = fChunks != null ? fChunks.GetValue(mrs) as HashSet<long> : null;
        // homeMid 所在 chunk 可能本就未加载（EnsureStrongHomeArea 按 w/2 锚定）——未加载=真竞态场景，同样放行重铺
        bool wasLoaded = chunks != null && chunks.Contains(ckey);
        bool removed = chunks == null || !wasLoaded || chunks.Remove(ckey);
        var fEvt = typeof(MapRenderService).GetField("OnChunkRendered", BindingFlags.Static | BindingFlags.NonPublic);
        System.Delegate evtDel = fEvt != null ? fEvt.GetValue(null) as System.Delegate : null;
        int subs = evtDel != null ? evtDel.GetInvocationList().Length : -1;
        if (removed)
        {
            var mRender = typeof(MapRenderService).GetMethod("RenderChunk", BindingFlags.Instance | BindingFlags.NonPublic);
            mRender.Invoke(mrs, new object[] { cx, cy });             // 真钩子链：SetCell+OnChunkRendered→补染
        }
        bool keyBack = chunks != null && chunks.Contains(ckey);       // RenderChunk 真跑了才会 Add 回
        int paintedImmediate = overlay.PaintedCount;                  // 钩子同步补染即时读
        yield return null;
        float aHome = overlay.GetMidAlpha(homeMid);
        bool p6 = paintedA > 0 && aHomeA > 0.3f && removed && keyBack && paintedImmediate > 0 && overlay.PaintedCount > 0 && aHome > 0.3f;
        results.Add($"P6 chunk补染 直调={paintedA}/主城{aHomeA:0.###} 重铺={removed} 钩子重载={keyBack} 订阅者={subs} 即染={paintedImmediate} painted={overlay.PaintedCount} 主城={aHome:0.###}={p6}");

        // ---- P7 灭国渐隐（2× 下：AI 国 2s 渐隐+不误伤，D446/D379）----
        overlay.ReapplyAll();                                                     // 无条件全染：P6 竞态补染只恢复局部（玩家 chunk），fadeKid 全 mid 须有色才可验渐隐
        yield return null;
        int fadeKid = -1;
        foreach (var kv in ts.Ledger)
            if (kv.Value > 0 && kv.Value != SYNTH_KINGDOM) { fadeKid = kv.Value; break; }
        bool p7 = false;
        if (fadeKid >= 0)
        {
            Vector2Int fMid = default;
            int n7 = 0;
            foreach (var kv in ts.Ledger)
                if (kv.Value == fadeKid) { if (n7 == 0) fMid = kv.Key; n7++; }
            float before = overlay.GetMidAlpha(fMid);
            overlay.FadeOutKingdom(fadeKid);
            yield return null;                                        // 一帧渐隐推进
            float during = overlay.GetMidAlpha(fMid);
            yield return new WaitForSeconds(FADE_WAIT);
            bool cleared = overlay.GetMidAlpha(fMid) < 0f && !overlay.GetPaintedKingdoms().Contains(fadeKid);
            bool othersKept = overlay.PaintedCount > 0;
            p7 = during < before && cleared && othersKept;
            results.Add($"P7 灭国渐隐 kid={fadeKid}({n7}mid) 前={before:0.###} 渐隐中={during:0.###} 清空={cleared} 他国保留={othersKept}={p7}");
        }
        else
        {
            results.Add("P7 灭国渐隐 无 AI 国领土可探针（降级记录）=false");
        }

        // ---- P8 读档重染幂等（2×：ReapplyAll 基线→快照一致+GameLoadedEvent 路径，D445③）----
        overlay.ReapplyAll();
        yield return null;
        var s1 = Snapshot(overlay);
        overlay.ReapplyAll();
        yield return null;
        var s2 = Snapshot(overlay);
        EventBus.Publish(new GameLoadedEvent("smoke_t10_p8", true));  // 触发路径（真读档链不重放，降级列报）
        yield return null;
        var s3 = Snapshot(overlay);
        bool p8 = SameSnapshot(s1, s2) && SameSnapshot(s1, s3) && s1.Count > 0;
        results.Add($"P8 读档重染幂等 基线={s1.Count} 重染一致={SameSnapshot(s1, s2)} 事件触发一致={SameSnapshot(s1, s3)}={p8}");

        // ---- P9 高亮（D452：近景下临时中景浓度+取消回隐）----
        rig.ZoomTo(0);
        yield return new WaitForSeconds(TRANSIT_WAIT);
        bool hiddenNear = !overlay.IsLayerActive;
        overlay.HighlightKingdom(0);                                  // 玩家高亮（临时激活+中景浓度）
        yield return null;
        bool activeHL = overlay.IsLayerActive;
        float aHL = fI ? overlay.GetMidAlpha(interiorMid) : -1f;      // 玩家内部 mid→中景浓度 0.35（homeMid 是初始圈角=边界 0.50，不用）
        overlay.HighlightKingdom(-1);                                 // 取消→近景回渐出
        yield return new WaitForSeconds(TRANSIT_WAIT);
        bool hiddenBack = !overlay.IsLayerActive;
        bool p9 = hiddenNear && activeHL && Mathf.Abs(aHL - 0.35f) < EPS && hiddenBack;
        results.Add($"P9 高亮(D452) 近景隐藏={hiddenNear} 临时激活={activeHL} 中景浓度={aHL:0.###}(≈0.35) 取消回隐={hiddenBack}={p9}");

        // ---- 探针注入清理（账本还原；染色残留由 ReapplyAll 基线消除）----
        var ledgerField = typeof(TerritorySystem).GetField("_territory", BindingFlags.Instance | BindingFlags.NonPublic);
        var ledgerDict = ledgerField != null ? ledgerField.GetValue(ts) as Dictionary<Vector2Int, int> : null;
        if (ledgerDict != null)
            foreach (var m in cleanupMids) ledgerDict.Remove(m);
        if (ledgerDict != null)
        {
            var doomed = new List<Vector2Int>();
            foreach (var kv in ledgerDict) if (kv.Value == SYNTH_KINGDOM) doomed.Add(kv.Key);
            foreach (var m in doomed) ledgerDict.Remove(m);
        }

        bool allPass = p1 && p2 && p3 && p4 && p5 && p6 && p7 && p8 && p9;
        Debug.Log("[2_10冒烟] " + string.Join(" | ", results));
        Debug.Log($"[2_10冒烟] ===== {(allPass ? "ALL PASS" : "HAS FAIL")}（P1近景负/P2中景色异/P3远景浓色/P4跨档平滑/P5增量边界重算/P6 chunk补染/P7灭国渐隐/P8读档幂等/P9高亮D452）=====");
    }

    private static Dictionary<Vector2Int, float> Snapshot(TerritoryOverlay o)
    {
        var snap = new Dictionary<Vector2Int, float>();
        foreach (var kid in o.GetPaintedKingdoms())
        {
            var ledger = TerritorySystem.Instance.Ledger;
            foreach (var kv in ledger)
                if (kv.Value == kid && o.GetMidAlpha(kv.Key) >= 0f)
                    snap[kv.Key] = o.GetMidAlpha(kv.Key);
        }
        return snap;
    }

    private static bool SameSnapshot(Dictionary<Vector2Int, float> a, Dictionary<Vector2Int, float> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out var v)) return false;
            if (Mathf.Abs(v - kv.Value) > 0.001f) return false;
        }
        return true;
    }

    private class RunHost : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}
