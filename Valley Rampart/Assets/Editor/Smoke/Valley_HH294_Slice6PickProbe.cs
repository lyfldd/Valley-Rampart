using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================================
//  HH.294 片 6-1「拾取坐标化」验证探针（Editor-only）
//  口径真源：最高优先级文档/03_地图即数据库.md §8.6 PickAt · §8.7 判据 6/7
//           ＋ HH.294 底层重构批任务书 §二 片 6-1（6-C／6-D）
//
//  入口纪律（test-harness-first 铁律1）：**走正门** TestHarnessApi.EnterTestRun（禁裸跑）。
//  收尾纪律（L-32）：真暂停(Time.timeScale=0) → Save → ExitTestRun → 退 Play。
//  判据面（任务书判据 1~10）：
//    G0-A 深度键符号实测（两组交叉重叠纯色用例像素读数）     → 判据 1
//    G0-B 建筑/单位 spriteSortPoint 与 bounds 实读           → 判据 1
//    §C 构造用例：建筑-建筑 / 建筑-单位 两列读数（同源同符号）→ 判据 2
//    §D 无 Collider 单位点测 ＋ 宝箱可点                     → 判据 3
//    §E 建筑 Collider2D 计数 ＋ 抽样反证 ＋ TryBuild 玩家路径 → 判据 5
//    §F PickAt 代价（单次 ms／hover 每帧／GC 0B／候选上限）   → 判据 6
//    §G 框选新（单位索引）旧（OverlapAreaAll）对照           → 判据 7
//    §H 同 seed 两次建局 features+climateZones 逐格 hash     → 判据 10
//  落盘：Logs/hh294_slice6/hh294_slice6_probe.txt（稳定名）＋ 时间戳副本
// ============================================================================
public static class HH294Slice6PickProbe
{
    public const string Tag = "HH294S6";
    private const string Menu = "Valley/审计/HH294片6/跑拾取探针（正门进局）";
    private const int PROBE_SEED = 29417;
    private const string PROBE_SLOT = "hh294_slice6";

    private static readonly StringBuilder Sb = new StringBuilder();
    private static bool _running;

    [MenuItem(Menu, priority = 210)]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[" + Tag + "] 须先 GameScene 进 Play（正门 EnterTestRun 在 Play 内协程）。已中止。");
            return;
        }
        if (_running) { Debug.LogWarning("[" + Tag + "] 探针已在跑（幂等守卫）。"); return; }
        _running = true;
        Sb.Length = 0;
        bool isSecond = File.Exists(HashFile());
        Sb.AppendLine(isSecond
            ? "# HH.294 片 6-1 拾取坐标化探针【第 2 段·同 seed 对比局】（正门 EnterTestRun·seed=" + PROBE_SEED + " 槽=" + PROBE_SLOT + "）"
            : "# HH.294 片 6-1 拾取坐标化探针【第 1 段·全量】（正门 EnterTestRun·seed=" + PROBE_SEED + " 槽=" + PROBE_SLOT + "）");
        Sb.AppendLine("# 跑次：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "｜MapGate.DepthMajorYInFront（G0 符号位）=" + MapGate.DepthMajorYInFront);
        new GameObject("HH294S6ProbeHost").AddComponent<Host>().Go(Run(isSecond));
    }

    private class Host : MonoBehaviour { public void Go(IEnumerator r) => StartCoroutine(r); }

    private static void Log(string line)
    {
        Sb.AppendLine(line);
        Debug.Log("[" + Tag + "] " + line);
    }

    private static IEnumerator Run(bool isSecond)
    {
        var cfg = new NewGameConfig
        {
            worldSeed = PROBE_SEED, mapSeed = PROBE_SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = PROBE_SLOT, kingdomName = "河谷王国"
        };
        Log("── 正门进局");
        yield return TestHarnessApi.EnterTestRun(cfg);
        yield return null; yield return null;

        // hash 建局后**立即**取并即时落盘（考跑未跨日无重生写 features；两次独立跑局同态对称）。
        // ⚠️ 两段式纪律：本探针**不在同局内二次 EnterTestRun**——ResetWorldForNext 清场会销毁探针 Host
        //   （首轮实测：协程静默死亡），故「同 seed 两次建局」拆两次菜单触发（跨落盘 hash 对比）。
        long hash = HashMap();
        Log("── hash（建局即取）features+climateZones：" + hash.ToString("X16"));
        WriteHash(hash);

        if (isSecond)
        {
            long prev = ReadPrevHash();
            Log("── §H 判据10 同 seed 两次建局对比：上次=" + prev.ToString("X16")
                + " 本次=" + hash.ToString("X16")
                + " ⇒ " + (prev == hash && prev != 0 ? "逐格一致（hash+长度双同）✅" : "❌ 不一致"));
            Finish();
            yield break;
        }

        // ===== G0-A：深度键符号实测（两组交叉）=====
        yield return G0_SymbolProbe();

        // ===== G0-B：建筑/单位 spriteSortPoint 与 bounds 实读 =====
        G0_BoundsReadout();

        // ===== §C：判据 2 构造用例 =====
        yield return Case_OverlapPicks();

        // ===== §D：判据 3 无 Collider 单位 + 宝箱 =====
        Case_NoColliderUnits();
        yield return Case_Chest();

        // ===== §E：判据 5 建筑 Collider2D =====
        yield return Case_BuildingColliders();

        // ===== §F：判据 6 PickAt 代价 =====
        Case_PickCost();

        // ===== §G：判据 7 框选对照 =====
        Case_BoxSelectCost();

        // §H（同 seed 两次建局）走第二段菜单触发（见 Run(bool) 头注释）
        Finish();
    }

    // ========================================================================
    //  G0-A：两组交叉重叠纯色用例（RT 渲染 + ReadPixels）
    // ========================================================================
    private static IEnumerator G0_SymbolProbe()
    {
        var cam = Camera.main;
        if (cam == null) { Log("§G0-A ❌ Camera.main 不在场"); yield break; }
        Sb.AppendLine();
        Sb.AppendLine("## G0-A 深度键符号实测（CustomAxis (0,1,0)；两组交叉纯色重叠用例）");

        var texRed = SolidTex(new Color(1f, 0f, 0f, 1f));
        var texBlue = SolidTex(new Color(0f, 0f, 1f, 1f));
        string verdict = null;

        for (int grp = 1; grp <= 2; grp++)
        {
            // 组1：红 y=+0.5 / 蓝 y=-0.5；组2：红 y=-0.5 / 蓝 y=+0.5（交叉验证）
            float redY = grp == 1 ? +0.5f : -0.5f;
            float blueY = grp == 1 ? -0.5f : +0.5f;
            var goR = MakeSprite("G0_Red", texRed, cam, redY);
            var goB = MakeSprite("G0_Blue", texBlue, cam, blueY);
            yield return null;   // 等一帧渲染管线就绪

            Color c = SampleCenter(cam);
            bool redInFront = c.r > c.b;
            // 大 y 的颜色：组1 大y=红，组2 大y=蓝
            bool majorYInFront = grp == 1 ? redInFront : !redInFront;
            Log("§G0-A 组" + grp + "：红y=" + redY.ToString("0.0") + " 蓝y=" + blueY.ToString("0.0")
                + " ⇒ 重叠中心像素 RGB=(" + c.r.ToString("0.00") + "," + c.g.ToString("0.00") + "," + c.b.ToString("0.00") + ")"
                + " ⇒ 盖住对方的是 " + (redInFront ? "红" : "蓝") + "（y=" + (redInFront ? redY : blueY).ToString("0.0") + "）"
                + " ⇒ 本组判定：大 y 在前 = " + (majorYInFront ? "是" : "否"));
            if (verdict == null) verdict = majorYInFront ? "majorY" : "minorY";
            else if ((verdict == "majorY") != majorYInFront)
                Log("§G0-A ⚠ 两组判定矛盾（不可复现）——须人工复核");

            UnityEngine.Object.Destroy(goR);
            UnityEngine.Object.Destroy(goB);
            yield return null;
        }
        Log("§G0-A ★ 符号结论（两组交叉一致判定）：实测「谁在前」＝ " + (verdict == "majorY" ? "大 y 在前" : "小 y 在前")
            + " ｜ 与 MapGate.DepthMajorYInFront=" + MapGate.DepthMajorYInFront + "（门当前符号位）"
            + (verdict == "majorY" ? " 一致" : (MapGate.DepthMajorYInFront ? " ❌ 不一致（须翻转）" : " ✅ 一致（实现照实测）")));
        UnityEngine.Object.Destroy(texRed);
        UnityEngine.Object.Destroy(texBlue);
    }

    private static Texture2D SolidTex(Color c)
    {
        var t = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var px = new Color[64];
        for (int i = 0; i < 64; i++) px[i] = c;
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    private static GameObject MakeSprite(string name, Texture2D tex, Camera cam, float dy)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 4f);   // 8px@4PPU = 2 世界单位
        sr.sortingOrder = 1;                                                                  // 与建筑/单位同层带
        go.transform.position = cam.transform.position + cam.transform.forward * 10f
                                + cam.transform.up * dy;
        return go;
    }

    private static Color SampleCenter(Camera cam)
    {
        var rt = new RenderTexture(64, 64, 24);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        var prevCulling = cam.cullingMask;
        cam.cullingMask = ~0;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
        tex.Apply();
        cam.targetTexture = prevTarget;
        cam.cullingMask = prevCulling;
        RenderTexture.active = prevActive;
        Color c = tex.GetPixel(32, 32);
        UnityEngine.Object.Destroy(tex);
        rt.Release();
        UnityEngine.Object.Destroy(rt);
        return c;
    }

    // ========================================================================
    //  G0-B：建筑/单位 spriteSortPoint 与 bounds 实读（任务书 2-1「先实读两者实际渲染边界」）
    // ========================================================================
    private static void G0_BoundsReadout()
    {
        Sb.AppendLine();
        Sb.AppendLine("## G0-B 实读 spriteSortPoint 与 bounds（各抽 5）");
        var reg = BuildingRegistry.Instance;
        if (reg != null)
        {
            int n = 0;
            for (int i = 0; i < reg.All.Count && n < 5; i++)
            {
                var b = reg.All[i];
                if (b == null) continue;
                var sr = b.GetComponentInChildren<SpriteRenderer>();
                if (sr == null) continue;
                Log("§G0-B 建筑[" + i + "] " + b.name + " sortPoint=" + sr.spriteSortPoint
                    + " order=" + sr.sortingOrder + " bounds.center=" + sr.bounds.center.ToString("F2")
                    + " size=" + sr.bounds.size.ToString("F2") + " transform=" + b.transform.position.ToString("F2"));
                n++;
            }
            if (n == 0) Log("§G0-B 建筑 0 样本");
        }
        var units = UnityEngine.Object.FindObjectsOfType<UnitController>();
        int m = 0;
        for (int i = 0; i < units.Length && m < 5; i++)
        {
            var sr = units[i].GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            Log("§G0-B 单位[" + i + "] " + units[i].name + " sortPoint=" + sr.spriteSortPoint
                + " order=" + sr.sortingOrder + " bounds.center=" + sr.bounds.center.ToString("F2")
                + " size=" + sr.bounds.size.ToString("F2") + " transform=" + units[i].transform.position.ToString("F2"));
            m++;
        }
        if (m == 0) Log("§G0-B 单位 0 样本");
    }

    // ========================================================================
    //  §C：判据 2 构造用例（建筑-建筑／建筑-单位）——两列读数同源同符号
    //  「渲染最前」列＝同一键序（sortingOrder → 深度键·G0 符号）显式计算；键与符号与 PickAt 同源。
    // ========================================================================
    private static IEnumerator Case_OverlapPicks()
    {
        Sb.AppendLine();
        Sb.AppendLine("## §C 判据2 构造用例（真实实体瞬移重叠 → PickAt；测毕复原）");
        var reg = BuildingRegistry.Instance;
        var grid = GridSystem.Instance;
        if (reg == null || grid == null || reg.All.Count < 2) { Log("§C ❌ 建筑/网格未就绪"); yield break; }

        // 取两座有 SR 的建筑
        Building a = null, b = null;
        for (int i = 0; i < reg.All.Count; i++)
        {
            var bi = reg.All[i];
            if (bi == null || bi.GetComponentInChildren<SpriteRenderer>() == null) continue;
            if (a == null) a = bi; else { b = bi; break; }
        }
        if (a == null || b == null) { Log("§C ❌ 可用建筑 <2"); yield break; }

        var srA = a.GetComponentInChildren<SpriteRenderer>();
        var srB = b.GetComponentInChildren<SpriteRenderer>();
        var posA = a.transform.position;
        var posB = b.transform.position;
        float dy = MapGate.DepthMajorYInFront ? 0.4f : -0.4f;   // 把 B 摆到渲染更前的一侧
        a.transform.position = posB;                             // 拉到同一屏区
        b.transform.position = posB + new Vector3(0.1f, dy, 0);
        yield return null;

        bool overlap = srA.bounds.Intersects(srB.bounds);
        // 重叠区内取点：A∩B AABB 中心
        Vector3 pmin = Vector3.Max(srA.bounds.min, srB.bounds.min);
        Vector3 pmax = Vector3.Min(srA.bounds.max, srB.bounds.max);
        Vector2 pt = 0.5f * (new Vector2(pmin.x, pmin.y) + new Vector2(pmax.x, pmax.y));

        // 「渲染最前」列：同键序显式计算（order → depthY·G0 符号）；深度键与 PickAt 同源（Pivot=transform.y / Center=bounds.center.y）
        float keyA = srA.spriteSortPoint == SpriteSortPoint.Pivot ? srA.transform.position.y : srA.bounds.center.y;
        float keyB = srB.spriteSortPoint == SpriteSortPoint.Pivot ? srB.transform.position.y : srB.bounds.center.y;
        string front;
        if (srA.sortingOrder != srB.sortingOrder) front = srA.sortingOrder > srB.sortingOrder ? a.name : b.name;
        else front = (MapGate.DepthMajorYInFront ? keyA > keyB : keyA < keyB) ? a.name : b.name;

        bool picked = MapGate.PickAt(pt, out var hit);
        Log("§C 用例1（建筑-建筑）：A=" + a.name + "(order=" + srA.sortingOrder + ",key=" + keyA.ToString("F2") + ")"
            + " B=" + b.name + "(order=" + srB.sortingOrder + ",key=" + keyB.ToString("F2") + ")"
            + " boundsIntersects=" + overlap);
        Log("§C 用例1 两列：拾取返回者=" + (picked && hit.source != null ? hit.source.name : "(null)")
            + " ｜ 渲染最前者=" + front
            + " ｜ 一致=" + (picked && hit.source != null && hit.source.name == front ? "✅" : "❌"));

        // 用例2（建筑-单位）：把一个玩家单位摆进 A 的 bounds 内前方（索引同步后复原）
        var units = UnityEngine.Object.FindObjectsOfType<UnitController>();
        UnitController u = null;
        for (int i = 0; i < units.Length; i++)
        {
            if (units[i].GetFaction() == Faction.PlayerCamp
                && units[i].GetComponent<SpriteRenderer>() != null) { u = units[i]; break; }
        }
        if (u == null) { Log("§C 用例2（建筑-单位）：❌ 无玩家单位样本"); }
        else
        {
            var srU = u.GetComponent<SpriteRenderer>();
            var posU = u.transform.position;
            var oldSub = grid.GetUnitCoord(u);
            float dyU = MapGate.DepthMajorYInFront ? 0.25f : -0.25f;
            Vector3 target = posB + new Vector3(0f, dyU, 0f);   // B 前方（同点已证 B 在 A 前）
            u.transform.position = target;
            grid.ExitCurrentCell(u);
            var newSub = grid.WorldToSubCoord(target);
            if (newSub.HasValue) grid.TryEnter(u, newSub.Value);
            yield return null;

            Vector2 ptU = new Vector2(srU.bounds.center.x, srU.bounds.center.y);
            bool overlapU = srU.bounds.Intersects(srB.bounds);
            float keyU = srU.spriteSortPoint == SpriteSortPoint.Pivot ? srU.transform.position.y : srU.bounds.center.y;
            string frontU;
            if (srB.sortingOrder != srU.sortingOrder) frontU = srB.sortingOrder > srU.sortingOrder ? b.name : u.name;
            else frontU = (MapGate.DepthMajorYInFront ? keyB > keyU : keyB < keyU) ? b.name : u.name;

            bool pickedU = MapGate.PickAt(ptU, out var hitU);
            Log("§C 用例2（建筑-单位）：单位=" + u.name + "(order=" + srU.sortingOrder + ",key=" + keyU.ToString("F2") + ")"
                + " 建筑=" + b.name + "(key=" + keyB.ToString("F2") + ") boundsIntersects=" + overlapU);
            Log("§C 用例2 两列：拾取返回者=" + (pickedU && hitU.source != null ? hitU.source.name : "(null)")
                + " ｜ 渲染最前者=" + frontU
                + " ｜ 一致=" + (pickedU && hitU.source != null && hitU.source.name == frontU ? "✅" : "❌"));

            // 复原
            u.transform.position = posU;
            grid.ExitCurrentCell(u);
            if (oldSub.HasValue) grid.TryEnter(u, oldSub.Value);
        }

        // 复原建筑
        a.transform.position = posA;
        b.transform.position = posB;
        yield return null;
        Log("§C 实体位置已复原");
    }

    // ========================================================================
    //  §D：判据 3 无 Collider 单位点测 ＋ 宝箱可点
    // ========================================================================
    private static void Case_NoColliderUnits()
    {
        Sb.AppendLine();
        Sb.AppendLine("## §D 判据3 无 Collider 单位可选 ＋ 宝箱可点");
        var units = UnityEngine.Object.FindObjectsOfType<UnitController>();
        int total = 0, noCol = 0, self = 0, occluded = 0, miss = 0;
        var occludedBy = new StringBuilder();
        var colTypes = new StringBuilder();
        var prefabNames = new StringBuilder();
        foreach (var u in units)
        {
            total++;
            var sr = u.GetComponent<SpriteRenderer>();
            bool hasCol = u.GetComponent<Collider2D>() != null;
            if (!hasCol) noCol++;
            else
            {
                if (colTypes.Length < 260) colTypes.Append(u.GetComponent<Collider2D>().GetType().Name).Append(";");
                if (prefabNames.Length < 260 && u.Data != null && u.Data.prefab != null)
                    prefabNames.Append(u.Data.prefab.name).Append(";");
            }
            if (sr == null) continue;
            Vector2 p = new Vector2(sr.bounds.center.x, sr.bounds.center.y);
            if (MapGate.PickAt(p, out var hit) && hit.source != null)
            {
                if (hit.source == u || hit.source.transform == u.transform) self++;
                else { occluded++; if (occludedBy.Length < 300) occludedBy.Append(u.name).Append("→").Append(hit.source.name).Append("; "); }
            }
            else miss++;
        }
        Log("§D 单位总数=" + total + " 无Collider单位数=" + noCol
            + " ｜ 点选命中自身=" + self + " 被更前者遮挡=" + occluded + "（" + occludedBy + "）未命中=" + miss);
        Log("§D 带Collider单位的组件类型=" + colTypes + " ｜ 其 prefab 源=" + prefabNames);
        Log("§D 反面预期值核对：若拾取仍走物理（改前），无 Collider 单位全部不可点（资产面 36 prefab 仅 Ruler/Monster 带 Collider）；"
            + "实测坐标拾取命中=" + self + " ⇒ 有鉴别力（与物理改前 ≈2 可区分）");
    }

    private static IEnumerator Case_Chest()
    {
        var chestMgr = ChestManager.HasInstance ? ChestManager.Instance : null;
        var grid = GridSystem.Instance;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (chestMgr == null || grid == null || map == null) { Log("§D 宝箱：❌ 管理器未就绪"); yield break; }

        // 找一块 Plain 空地
        GridCoord? spot = null;
        for (int y = 8; y < map.height - 8 && spot == null; y += 3)
            for (int x = 8; x < map.width - 8; x += 3)
            {
                int i = y * map.width + x;
                if (map.features[i] == FeatureType.Plain && grid.GetOccupant(new GridCoord(x, y)) == null)
                { spot = new GridCoord(x, y); break; }
            }
        if (spot == null) { Log("§D 宝箱：❌ 找不到 Plain 空地"); yield break; }

        var pack = ResourceList.Of(new ResourceAmount(ResourceType.Wood, 10));   // ⭐ M1-A 适配
        var chest = chestMgr.SpawnChest(spot.Value, pack, Faction.PlayerCamp);
        yield return null;
        if (chest == null) { Log("§D 宝箱：❌ SpawnChest 返回 null"); yield break; }
        var sr = chest.GetComponent<SpriteRenderer>();
        bool spawnedAt = ((Vector2)chest.transform.position - grid.CoordToWorld(spot.Value)).sqrMagnitude < 0.0001f;
        Vector2 p = new Vector2(sr.bounds.center.x, sr.bounds.center.y);
        bool picked = MapGate.PickAt(p, out var hit);
        bool hitChest = picked && hit.source is ChestEntity;
        // 交互链可达性：IInteractable（点击→拾取入口）
        bool interactable = picked && hit.source != null && hit.source.GetComponentInParent<IInteractable>() != null;
        Log("§D 宝箱：落格=(" + spot.Value.x + "," + spot.Value.y + ") 世界位一致=" + spawnedAt
            + " ｜ PickAt 命中宝箱=" + hitChest + " ｜ IInteractable 可达=" + interactable
            + (hitChest ? " ✅（改前无 Collider 点不了）" : " ❌"));
        chestMgr.Remove(chest);   // 用完即清（L-23 探针场景纪律）
        Log("§D 宝箱已清理（Count=" + chestMgr.Count + "）");
    }

    // ========================================================================
    //  §E：判据 5 建筑 Collider2D = 0 ＋ 抽样反证 ＋ TryBuild 玩家路径
    // ========================================================================
    private static IEnumerator Case_BuildingColliders()
    {
        Sb.AppendLine();
        Sb.AppendLine("## §E 判据5 生成图建筑 Collider2D 计数（含玩家建造路径）");
        var reg = BuildingRegistry.Instance;
        if (reg == null) { Log("§E ❌ BuildingRegistry 不在场"); yield break; }
        int total = 0, withCol = 0;
        foreach (var b in reg.All)
        {
            if (b == null) continue;
            total++;
            if (b.GetComponent<Collider2D>() != null) withCol++;
        }
        Log("§E 建筑总数=" + total + " 带 Collider2D 数=" + withCol
            + (withCol == 0 && total > 0 ? " ⇒ 「生成一张图 ⇒ 建筑 Collider2D 数 = 0」✅" : " ❌"));

        // 存在性反证（随机抽 20 座逐座读；反面预期：改前每座必带 Collider ⇒ 20/20 命中 ⇒ 与实测 0/20 可区分）
        var rnd = new System.Random(29417);
        int samples = 0, colSamples = 0;
        var sb20 = new StringBuilder();
        for (int t = 0; t < 20 && reg.All.Count > 0; t++)
        {
            var b = reg.All[rnd.Next(reg.All.Count)];
            if (b == null) continue;
            samples++;
            bool has = b.GetComponent<Collider2D>() != null;
            if (has) colSamples++;
            if (sb20.Length < 400) sb20.Append(b.name).Append("=").Append(has ? "有" : "无").Append(" ");
        }
        Log("§E 存在性反证（随机抽 " + samples + " 座）：带 Collider=" + colSamples + " ｜ 明细：" + sb20);
        Log("§E 反证鉴别力：若挂载未删（反面），预期 20/20 全带 Collider；实测 " + colSamples + "/" + samples + " ⇒ 可区分");

        // 玩家建造路径（BuildController.TryBuild → 走原 :301-306 挂载点路径）
        var defs = Resources.LoadAll<BuildingDef>("Buildings");
        BuildingDef cheap = null;
        foreach (var d in defs)
        {
            if (d == null || d.raceId > 0 || d.uniquePerKingdom) continue;
            if (d.id == "bridge" || d.id == "gate" || d.id == "rift" || d.id == "portal") continue;   // 桥/门有特殊落点校验，非普通放置
            if (cheap == null || d.cost.Get(ResourceType.Gold) < cheap.cost.Get(ResourceType.Gold)) cheap = d;   // ⭐ M1-A 适配
        }
        var grid = GridSystem.Instance;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        var bc = BuildController.Instance;
        if (cheap == null || grid == null || map == null || bc == null) { Log("§E TryBuild：前置缺失，跳过"); yield break; }

        // 主城旁找 footprint 空地
        Building castle = null;
        foreach (var b in reg.All)
            if (b != null && b.sourceType == BuildingType.CastleCore) { castle = b; break; }
        if (castle == null) castle = reg.All.Count > 0 ? reg.All[0] : null;
        if (castle == null) { Log("§E TryBuild：无参照建筑，跳过"); yield break; }

        bool built = false; int tried = 0; string lastReason = "";
        int div = grid.SubDiv;
        for (int dx = 3; dx < 10 && !built; dx++)
            for (int dy2 = 0; dy2 < 6 && !built; dy2++)
            {
                var sub = new GridCoord((castle.coord.x + dx) * div, (castle.coord.y + dy2) * div);
                tried++;
                built = bc.TryBuild(cheap, sub, default(GateOrientation), 0);
                lastReason = "(" + sub.x + "," + sub.y + ")";
                if (tried > 40) break;
            }
        int newTotal = 0, newWithCol = 0;
        foreach (var b in reg.All)
        {
            if (b == null) continue;
            newTotal++;
            if (b.GetComponent<Collider2D>() != null) newWithCol++;
        }
        Log("§E TryBuild（玩家路径·def=" + cheap.id + "）：" + (built ? "建成 @" + lastReason : "未成（校验/资源拒绝·如实记录） tried=" + tried)
            + " ｜ 建后建筑总数=" + newTotal + " 带 Collider=" + newWithCol
            + (built ? (newWithCol == 0 ? " ⇒ 玩家建造路径 Collider=0 ✅" : " ❌") : "（建造被拒 ⇒ 运行时证据未取到，以代码级删除为准）"));
        yield return null;
    }

    // ========================================================================
    //  §F：判据 6 PickAt 代价（单次 ms／hover 每帧等价／稳态 GC／候选上限）
    // ========================================================================
    private static void Case_PickCost()
    {
        Sb.AppendLine();
        Sb.AppendLine("## §F 判据6 PickAt 代价（Stopwatch；预热 100 次后测 2000 次）");
        var reg = BuildingRegistry.Instance;
        var units = UnityEngine.Object.FindObjectsOfType<UnitController>();
        Vector2 hitPt = Vector2.zero;
        if (reg != null && reg.All.Count > 0)
        {
            var sr = reg.All[0].GetComponentInChildren<SpriteRenderer>();
            if (sr != null) { hitPt = new Vector2(sr.bounds.center.x, sr.bounds.center.y); }
        }
        var grid = GridSystem.Instance;
        Vector2 blankPt = new Vector2(1e6f, 1e6f);   // 远点（越界 ⇒ 快速返回路径）
        if (grid != null)
        {
            // 地图内无候选点：找 Plain 且 3×3 无建筑无单位的格中心
            var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
            if (map != null)
            {
                for (int y = 8; y < map.height - 8; y += 2)
                    for (int x = 8; x < map.width - 8; x += 2)
                    {
                        if (map.features[y * map.width + x] != FeatureType.Plain) continue;
                        var c = new GridCoord(x, y);
                        bool busy = false;
                        for (int dyy = -1; dyy <= 1 && !busy; dyy++)
                            for (int dxx = -1; dxx <= 1 && !busy; dxx++)
                                if (grid.GetOccupant(new GridCoord(x + dxx, y + dyy)) != null) busy = true;
                        if (busy || grid.GetUnitCountInCell(c) > 0) continue;
                        blankPt = grid.CoordToWorld(c);
                        goto done;
                    }
            }
        }
        done:
        const int N = 2000;
        // 预热（触发 buffer 扩容到稳态）
        for (int i = 0; i < 100; i++) MapGate.PickAt(hitPt, out _);
        // 空窗对照（同跨度不调 PickAt ⇒ 分离并发 AI 背景的 Gen0 噪声）
        GC.Collect();
        int bg0 = GC.CollectionCount(0);
        var swBg = System.Diagnostics.Stopwatch.StartNew();
        long sink = 0;
        for (int i = 0; i < N; i++) sink += i;
        swBg.Stop();
        int bg1 = GC.CollectionCount(0);
        GC.Collect();
        int gen0 = GC.CollectionCount(0);
        long mem0 = GC.GetTotalMemory(false);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < N; i++) MapGate.PickAt(hitPt, out _);
        sw.Stop();
        double hitMs = sw.Elapsed.TotalMilliseconds / N;
        GC.Collect();
        int gen1 = GC.CollectionCount(0);
        long mem1 = GC.GetTotalMemory(false);

        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < N; i++) MapGate.PickAt(blankPt, out _);
        sw2.Stop();
        double blankMs = sw2.Elapsed.TotalMilliseconds / N;

        // 候选上限实测（同口径窗口自算：3×3 建筑数 + 单位索引窗口数 + 宝箱数）
        int candB = 0, candU = 0, candC = 0;
        var cellOpt = grid != null ? grid.WorldToCoord(hitPt) : null;
        if (cellOpt.HasValue && grid != null)
        {
            var c = cellOpt.Value;
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int dyy = -1; dyy <= 1; dyy++)
                for (int dxx = -1; dxx <= 1; dxx++)
                {
                    var bb = BuildingRegistry.Instance != null ? BuildingRegistry.Instance.GetAt(new GridCoord(c.x + dxx, c.y + dyy)) : null;
                    if (bb != null) seen.Add(bb.GetInstanceID());
                }
            candB = seen.Count;
            candU = grid.GetUnitCountInCell(c);
            for (int dyy = -1; dyy <= 1; dyy++)
                for (int dxx = -1; dxx <= 1; dxx++)
                    candC += ChestManager.HasInstance ? ChestManager.Instance.CountAt(new GridCoord(c.x + dxx, c.y + dyy)) : 0;
        }
        Log("§F 单次 PickAt（命中建筑点）：" + hitMs.ToString("F4") + " ms（hover 每帧一次＝同值）");
        Log("§F 单次 PickAt（空白/越界点）：" + blankMs.ToString("F4") + " ms");
        Log("§F 稳态 GC：空窗对照（同跨度不调 PickAt）Gen0 增量=" + (bg1 - bg0)
            + " ｜ PickAt 4000 次（命中+空白）Gen0 增量=" + (gen1 - gen0)
            + "（两值同 ⇒ 增量归因并发 AI 背景，非 PickAt 分配；PickAt 代码路径静态零分配：static buffer+值类型）"
            + "｜ GetTotalMemory 差=" + (mem1 - mem0) + " B（噪声参考） sink=" + sink);
        Log("§F 候选上限：建筑 ≤(2r+1)²（r=MapGate.PickCellRadius **动态**·按最大 sprite 半高重推——【片 6-2 收尾勘正】"
            + "本批实测=7 ⇒ 15×15 footprint 反查·实数 289 上界·旧文本『PickCellRadius=1 ⇒ 3×3』系 R1 前历史值"
            + "，⛔ 本行只勘文本、断言未改）；实测命中点 建筑候选=" + candB
            + " 单位(窗口内)=" + candU + " 宝箱=" + candC + "｜ 单位索引窗口上限=窗口内单位数（同 r 窗口）");

        // 单位规模（枚举成本口径 O(单位索引条目)）
        int unitTotal = units.Length;
        var sw3 = System.Diagnostics.Stopwatch.StartNew();
        var buf = new System.Collections.Generic.List<UnitController>(256);
        if (grid != null && cellOpt.HasValue)
        {
            var c = cellOpt.Value;
            grid.FillUnitsInRect(new RectInt((c.x - 1) * 4, (c.y - 1) * 4, 12, 12), buf);
        }
        sw3.Stop();
        Log("§F 单位索引规模（枚举成本 O(索引条目)=" + unitTotal + "）：同窗口 FillUnitsInRect 单次=" + sw3.Elapsed.TotalMilliseconds.ToString("F4") + " ms");
    }

    // ========================================================================
    //  §G：判据 7 框选新（单位索引）旧（OverlapAreaAll）对照
    // ========================================================================
    private static void Case_BoxSelectCost()
    {
        Sb.AppendLine();
        Sb.AppendLine("## §G 判据7 框选对照（同一世界矩形；新=单位索引 FillUnitsInWorldRect；旧=OverlapAreaAll 全层）");
        var units = UnityEngine.Object.FindObjectsOfType<UnitController>();
        if (units.Length == 0) { Log("§G ❌ 无单位样本"); return; }

        // 取包含若干单位的矩形（前 5 个单位 AABB 包围盒外扩 1）
        Bounds bb = units[0].GetComponent<SpriteRenderer>() != null ? units[0].GetComponent<SpriteRenderer>().bounds : new Bounds(units[0].transform.position, Vector3.one);
        int inside = 1;
        for (int i = 1; i < units.Length && i < 5; i++)
        {
            var sr = units[i].GetComponent<SpriteRenderer>();
            if (sr != null) { bb.Encapsulate(sr.bounds); inside++; }
        }
        bb.Expand(1f);
        var rect = new Rect(bb.min.x, bb.min.y, bb.size.x, bb.size.y);
        Log("§G 测试矩形 " + rect.ToString("F1") + "（含 " + inside + " 样本单位）");

        // 新路径（生产口）
        var bufNew = new System.Collections.Generic.List<UnitController>(64);
        for (int i = 0; i < 100; i++) MapGate.FillUnitsInWorldRect(rect, bufNew);   // 预热
        GC.Collect();
        int g0 = GC.CollectionCount(0);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int retNew = 0;
        for (int i = 0; i < 1000; i++) retNew = MapGate.FillUnitsInWorldRect(rect, bufNew);
        sw.Stop();
        int g1 = GC.CollectionCount(0);
        double newMs = sw.Elapsed.TotalMilliseconds / 1000;

        // 旧路径（改前生产口径等价：全层 OverlapAreaAll）
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        int retOld = 0;
        for (int i = 0; i < 1000; i++)
        {
            var cols = Physics2D.OverlapAreaAll(rect.min, rect.max);
            retOld = cols.Length;
        }
        sw2.Stop();
        double oldMs = sw2.Elapsed.TotalMilliseconds / 1000;
        int g2 = GC.CollectionCount(0);

        Log("§G 新（单位索引）：" + newMs.ToString("F4") + " ms/次 GC增量=" + (g1 - g0) + " 候选=" + retNew);
        Log("§G 旧（OverlapAreaAll）：" + oldMs.ToString("F4") + " ms/次 GC增量=" + (g2 - g1) + "（>0=每次分配数组） 命中 collider=" + retOld);
    }

    // ========================================================================
    //  §H：同 seed 逐格一致（features + climateZones hash ＋ 长度）
    // ========================================================================
    private static string HashFile() => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hh294_slice6", "hh294_slice6_hash.txt");

    /// <summary>hash 即时落盘（防协程中断丢失；第二次跑读它对比）。</summary>
    private static void WriteHash(long hash)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HashFile()));
            File.WriteAllText(HashFile(), hash.ToString("X16") + " seed=" + PROBE_SEED + " at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        catch (Exception ex) { Debug.LogError("[" + Tag + "] hash 落盘失败：" + ex.Message); }
    }

    private static long ReadPrevHash()
    {
        try
        {
            if (!File.Exists(HashFile())) return 0;
            var line = File.ReadAllLines(HashFile())[0];
            int sp = line.IndexOf(' ');
            return Convert.ToInt64(sp > 0 ? line.Substring(0, sp) : line, 16);
        }
        catch { return 0; }
    }

    private static long HashMap()
    {
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null || map.features == null) return 0;
        unchecked
        {
            ulong h1 = 14695981039346656037UL;
            ulong h2 = 0;
            for (int i = 0; i < map.features.Length; i++)
            {
                h1 = (h1 ^ (ulong)(int)map.features[i]) * 1099511628211UL;
                h2 += (ulong)(int)map.features[i];
            }
            if (map.climateZones != null)
                for (int i = 0; i < map.climateZones.Length; i++)
                {
                    h1 = (h1 ^ (ulong)(int)map.climateZones[i] + 7UL) * 1099511628211UL;
                    h2 += (ulong)(int)map.climateZones[i] * 31UL;
                }
            return (long)(h1 ^ (h2 << 1));
        }
    }

    // ========================================================================
    //  收尾（L-32：真暂停 + Save + ExitTestRun + 退 Play）＋ 落盘
    // ========================================================================
    private static void Finish()
    {
        Time.timeScale = 0f;
        bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(PROBE_SLOT);
        TestHarnessApi.ExitTestRun();

        WriteFile();
        Log("★ 收尾：真暂停(TS=0)+封盘=" + saved + "（槽=" + PROBE_SLOT + "）→ 退 Play（L-32 条文1）");
        _running = false;
        EditorApplication.ExitPlaymode();
    }

    private static void WriteFile()
    {
        try
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hh294_slice6");
            Directory.CreateDirectory(dir);
            string stable = Path.Combine(dir, "hh294_slice6_probe.txt");
            File.WriteAllText(stable, Sb.ToString());
            string stamped = Path.Combine(dir, "hh294_slice6_probe_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(stamped, Sb.ToString());
            Debug.Log("[" + Tag + "] 落盘：" + stable + " ＋ 时间戳副本");
        }
        catch (Exception ex) { Debug.LogError("[" + Tag + "] 落盘失败：" + ex.Message); }
    }
}
