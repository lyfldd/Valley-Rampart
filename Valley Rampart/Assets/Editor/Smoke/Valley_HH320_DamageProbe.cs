using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================================
//  HH.320 批1 第二段 · 伤害链口径统一批 —— 探针容器（两个菜单）
//    A「Valley/验证/HH320 伤害链口径_数学面(A)」：Edit Mode 即可（判据 2 数学面 / 12 / 13 / 16 表 / 17 / 18）
//    B「Valley/验证/HH320 伤害链口径_进局面(B)」：须先 Play（正门 TestHarnessApi.EnterTestRun）
//        判据 4~11：山壁/建筑占格/工事(墙·塔)/水/Locked/高抛/近战 ＋ 三形状成本对照
//  ⛔ 本容器**不改生产码**；收尾 ExitTestRun；产物落盘 仓库根 Logs/hh320_*.txt
//  ⚠️ 判据档只报读数，不写判定字样（判定须人工裁定）。
//  ⚠️ 构造法声明：进局面段＝① 真暂停（Time.timeScale=0 防世界漂移）② 试区"清场隔离"（同国射程内无敌）
//     ③ 靶单位/工事单位为 `UnitFactory.SpawnUnit` 产物（工事经**公开字段 `fortification` 赋值**，
//     生产路径可达性：`UnitData/{Wall,Gate,ArrowTower,...}.asset` → `UnitController:409-413` 同字段拷贝）。
// ============================================================================
public static class Valley_HH320_DamageProbe
{
    const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly StringBuilder SB = new StringBuilder();
    static string _outPath;
    static readonly List<IDamageable> _scanBuf = new List<IDamageable>();

    static void L(string s)
    {
        Debug.Log("[HH320] " + s);
        SB.AppendLine(s);
    }

    static void Flush(string tag)
    {
        SB.AppendLine("# 封存 " + System.DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        var dir = System.IO.Path.Combine(Application.dataPath, "..", "..", "Logs");
        System.IO.Directory.CreateDirectory(dir);
        _outPath = System.IO.Path.Combine(dir, "hh320_" + tag + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".txt");
        System.IO.File.WriteAllText(_outPath, SB.ToString());
        Debug.Log("[HH320] 落盘 " + _outPath);
    }

    // ========================================================================
    //  A · 数学面
    // ========================================================================

    [MenuItem("Valley/验证/HH320 伤害链口径_数学面(A)")]
    public static void RunMath()
    {
        var cs = new Vector2(GridMath.CellW, GridMath.CellH);
        L("=== A · HH.320 数学面 ===");
        L($"[S0] GridMath.Bound={GridMath.Bound} CellW={GridMath.CellW.ToString("F4", CultureInfo.InvariantCulture)}"
          + $" CellH={GridMath.CellH.ToString("F4", CultureInfo.InvariantCulture)}"
          + $" CellStep={GridMath.CellStep.ToString("F6", CultureInfo.InvariantCulture)}"
          + $" PathBandHalf={GridMath.PathBandHalf.ToString("F4", CultureInfo.InvariantCulture)}"
          + " | 期望 CellStep=0.715542 = 0.5*sqrt(1.28^2+0.64^2)");

        // ── 判据 2 ＋ 判据 16：射程两向（⭐ 格坐标取点 · ⛔ 不用像素/世界单位取点）──
        float R = 6f;
        Vector2 o = GridSystem.CellToWorldF(100f, 100f, cs);
        L($"[J2] 起点格(100,100) 世界={o} R={R} | 旧门①索敌世界圆半径 R*cellW={R * cs.x} | 旧门②判定格单位 DistCells<=R | 新门 DistVisual<=R");
        L("[J2/J16] 方向 | Δ格 | DistVisual | 新门 | DistCells | 旧判定门 | 世界距离 | 旧索敌圆门 | 旧有效(①∩②)");
        for (int k = 5; k <= 11; k++)
        {
            ReportAxis("gx", o, GridSystem.CellToWorldF(100f + k, 100f, cs), k, R, cs);
            ReportAxis("gy", o, GridSystem.CellToWorldF(100f, 100f + k, cs), k, R, cs);
        }
        for (int k = 6; k <= 7; k++)
        {
            Vector2 p = GridSystem.CellToWorldF(100f + k, 100f, cs);
            float dv = GridMath.DistVisual(o, p);
            L($"[J2·边界比] gx k={k} DistVisual/R={ (dv / R).ToString("R", CultureInfo.InvariantCulture) } dist={dv.ToString("R", CultureInfo.InvariantCulture)}");
        }

        // ── 判据 12：菱形底座三例 ──
        var fp = new Vector2Int(3, 3);
        var origin = new GridCoord(100, 100);
        Vector2 center = GridSystem.FootprintCenterWorld(origin, fp, cs);
        Vector2 inBase = center + new Vector2(1.4f * cs.x * 0.5f, 1.4f * cs.y * 0.5f);      // Δg=(1.4,0)
        Vector2 outBase = center + new Vector2(1.6f * cs.x * 0.5f, 1.6f * cs.y * 0.5f);     // Δg=(1.6,0)
        Vector2 corner = center + new Vector2(0f, 1.4f * cs.y);                             // Δg=(1.4,1.4)
        L($"[J12] footprint=(3,3) origin=(100,100) center={center} 底座半宽=1.5格");
        L($"[J12] 中心   point={center} InDiamondBase={CombatRules.InDiamondBase(center, center, fp, cs)}（期望 True）");
        L($"[J12] 底座内 Δg=(1.4,0) point={inBase} InDiamondBase={CombatRules.InDiamondBase(inBase, center, fp, cs)}（期望 True）"
          + $" 世界距离={Vector2.Distance(inBase, center).ToString("F4", CultureInfo.InvariantCulture)}");
        L($"[J12] 底座外 Δg=(1.6,0) point={outBase} InDiamondBase={CombatRules.InDiamondBase(outBase, center, fp, cs)}（期望 False）");
        L($"[J12] 对角内 Δg=(1.4,1.4) point={corner} InDiamondBase={CombatRules.InDiamondBase(corner, center, fp, cs)}（期望 True）"
          + $" 世界距离={Vector2.Distance(corner, center).ToString("F4", CultureInfo.InvariantCulture)}（若按 pivot 圆心距<半格则 False）");

        // ── 判据 18：subRange 超集（闭式 ＋ 全角度暴力核）──
        const int subDiv = 4;
        float rVis = 0.25f;
        int n = GridMath.SubWindowForVisualRadius(rVis, subDiv);
        int nOld = Mathf.Max(0, Mathf.CeilToInt(rVis * subDiv));
        L($"[J18] SubWindowForVisualRadius(0.25, 4)={n}（收口前 ⌈0.25×4⌉={nOld}） 期望 2");
        L($"[J18] 其他半径对照：0.5→{GridMath.SubWindowForVisualRadius(0.5f, subDiv)} | 1.0→{GridMath.SubWindowForVisualRadius(1f, subDiv)}"
          + $" | 2.0→{GridMath.SubWindowForVisualRadius(2f, subDiv)} | CellWindow(0.25)={GridMath.CellWindowForVisualRadius(0.25f)}");
        {
            var subSize = new Vector2(cs.x / subDiv, cs.y / subDiv);
            float rw = GridMath.VisualToWorld(rVis);
            int viol = 0, worstAxisNeed = 0;
            float maxAbsGx = 0f, maxAbsGy = 0f;
            for (int deg = 0; deg < 3600; deg++)
            {
                float a = deg * Mathf.Deg2Rad;
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rw;
                Vector2 g = GridSystem.WorldToCellF(d, subSize);      // 子格连续坐标位移（换算内核同形）
                float ax = Mathf.Abs(g.x), ay = Mathf.Abs(g.y);
                if (ax > maxAbsGx) maxAbsGx = ax;
                if (ay > maxAbsGy) maxAbsGy = ay;
                int need = Mathf.Max(Mathf.CeilToInt(ax), Mathf.CeilToInt(ay));
                if (need > worstAxisNeed) worstAxisNeed = need;
                if (need > n) viol++;
            }
            L($"[J18·暴力核] 3600 方向扫描（R_world={rw.ToString("F6", CultureInfo.InvariantCulture)}）"
              + $" max|Δgx|={maxAbsGx.ToString("F6", CultureInfo.InvariantCulture)} max|Δgy|={maxAbsGy.ToString("F6", CultureInfo.InvariantCulture)}"
              + $" 所需最大子格索引差={worstAxisNeed} n={n} 超窗方向数={viol}（期望 0）");
        }

        // ── 判据 13：散布 ＋ 确定性 ──
        L($"[J13] 散布确定性子：{SpreadSig(12345, 64, out int distinct1)}");
        L($"[J13] 同 seed 复跑：{SpreadSig(12345, 64, out int distinct2)} ⇒ 两次签名一致={SpreadSigCompare(12345, 64)} 散点数 distinct={distinct1}/{distinct2}");
        L($"[J13] 异 seed 对照（鉴别力）：{SpreadSig(99999, 64, out int distinct3)} distinct={distinct3}");
        {
            string a = KnockSig(777);
            string b = KnockSig(777);
            L($"[J13·击退角] seed=777 两次签名 {a} / {b} ⇒ 一致={a == b}");
            string c = KnockSig(778);
            L($"[J13·击退角] 异 seed=778 签名 {c} ⇒ 与 seed777 不同={a != c}");
        }

        // ── 判据 17：M1 双源处置读数 ──
        L($"[J17] M1：GridMath.Bound={GridMath.Bound}（Play 内由 GridSystem.Awake 绑定资产真源；此处未绑 ⇒ 回退编译期默认）"
          + $" CellW/CellH={GridMath.CellW}/{GridMath.CellH} CellStep={GridMath.CellStep}");

        Flush("math");
    }

    static void ReportAxis(string axis, Vector2 o, Vector2 p, int k, float R, Vector2 cs)
    {
        float dv = GridMath.DistVisual(o, p);
        float dc = GridMath.DistCells(o, p);
        float wd = Vector2.Distance(o, p);
        bool newGate = dv <= R;
        bool oldJudge = dc <= R;
        bool oldSearch = wd <= R * cs.x;
        L($"[J2/J16] {axis} | {k} | {dv.ToString("F6", CultureInfo.InvariantCulture)} | {(newGate ? "可" : "不")}"
          + $" | {dc.ToString("F6", CultureInfo.InvariantCulture)} | {(oldJudge ? "可" : "不")}"
          + $" | {wd.ToString("F6", CultureInfo.InvariantCulture)} | {(oldSearch ? "可" : "不")} | {(oldJudge && oldSearch ? "可" : "不")}");
    }

    static string SpreadSig(int seed, int count, out int distinct)
    {
        CombatRules.ResetCombatRandom(seed);
        var sb = new StringBuilder();
        var set = new HashSet<string>();
        for (int i = 0; i < count; i++)
        {
            Vector2 v = CombatRules.NextSpreadOffset(1f);
            sb.Append(v.x.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(v.y.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            set.Add(v.x.ToString("F4", CultureInfo.InvariantCulture) + "_" + v.y.ToString("F4", CultureInfo.InvariantCulture));
        }
        distinct = set.Count;
        return "len=" + sb.Length + " hash=" + sb.ToString().GetHashCode().ToString("X8");
    }

    static bool SpreadSigCompare(int seed, int count)
    {
        CombatRules.ResetCombatRandom(seed);
        var sb1 = new StringBuilder();
        for (int i = 0; i < count; i++) { Vector2 v = CombatRules.NextSpreadOffset(1f); sb1.Append(v.x.ToString("R", CultureInfo.InvariantCulture)).Append(','); }
        CombatRules.ResetCombatRandom(seed);
        var sb2 = new StringBuilder();
        for (int i = 0; i < count; i++) { Vector2 v = CombatRules.NextSpreadOffset(1f); sb2.Append(v.x.ToString("R", CultureInfo.InvariantCulture)).Append(','); }
        return sb1.ToString() == sb2.ToString();
    }

    static string KnockSig(int seed)
    {
        CombatRules.ResetCombatRandom(seed);
        var sb = new StringBuilder();
        for (int i = 0; i < 8; i++)
        {
            CombatRules.ComputeKnockback(80f, 3f, out float d, out float dur);
            sb.Append(d.ToString("R", CultureInfo.InvariantCulture)).Append('/');
        }
        return "hash=" + sb.ToString().GetHashCode().ToString("X8");
    }

    // ========================================================================
    //  B · 进局面（正门 TestHarnessApi.EnterTestRun）
    // ========================================================================

    [MenuItem("Valley/验证/HH320 伤害链口径_进局面(B)")]
    public static void RunLive()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH320] 须先进入 Play 后调用本菜单。"); return; }
        new GameObject("HH320_Host").AddComponent<RunHost>().Host(RunLiveCo());
    }

    private class RunHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    /// <summary>进局面协程的公开入口（供 bridge `exec_runtime_script` 直接 return 执行；菜单路径走 <see cref="RunLive"/>）。</summary>
    public static IEnumerator RunLiveCoroutine() => RunLiveCo();

    static IEnumerator RunLiveCo()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = 20320, mapSeed = 20320, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = "smoke_w320dmg", kingdomName = "伤害链"
        };
        Debug.Log("[HH320] 阶段：EnterTestRun 开始（建局中）");
        yield return TestHarnessApi.EnterTestRun(cfg, 15f);
        Debug.Log("[HH320] 阶段：EnterTestRun 返回（等世界就绪）");
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || GridSystem.Instance == null || GridSystem.Instance.Config == null
               || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 2
               || UnitFactory.Instance == null || UnitDataManager.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 180f) { Debug.LogError("[HH320] 等世界就绪超时。"); yield break; }
        }
        yield return new WaitForSeconds(0.5f);

        // ⭐ 真暂停（Time.timeScale=0）：防 15x 下 AI 单位漂移污染试区（收尾由 ExitTestRun 恢复 timeScale）
        Time.timeScale = 0f;
        yield return null;

        L("=== B · HH.320 进局面 ===");
        L($"[环境] seed={WorldManager.Instance.ActiveMap.seed} 单位在册={UnitRegistry.Instance.Count}"
          + $" 建筑在册={(BuildingRegistry.Instance != null ? BuildingRegistry.Instance.Count : -1)}"
          + $" subDiv={GridSystem.Instance.Config.subCellDivisor} cellSize={GridSystem.Instance.Config.cellSize}"
          + $" GridMath.Bound={GridMath.Bound} CellStep={GridMath.CellStep.ToString("F6", CultureInfo.InvariantCulture)}"
          + $" 真暂停(Time.timeScale)={Time.timeScale}");

        var grid = GridSystem.Instance;
        var anchor = WorldManager.Instance.GetKingdomAnchorWorld();
        var anchorCell = grid.WorldToCoord(anchor);
        if (!anchorCell.HasValue) { Debug.LogError("[HH320] 锚点格取不到。"); TestHarnessApi.ExitTestRun(); yield break; }

        // ── 试区选址：射手射程+4 格内**无敌**（隔离；靶单位除外）──
        float rangeProbe = 6f;      // Archer 默认；下面按实读快照复核
        GridCoord baseCell = default; bool siteFound = false; int tried = 0;
        for (int ring = 24; ring <= 96 && !siteFound; ring += 12)
            for (int dx = -24; dx <= 24 && !siteFound; dx += 12)
                for (int dy = -24; dy <= 24 && !siteFound; dy += 12)
                {
                    var c = new GridCoord(anchorCell.Value.x + dx, anchorCell.Value.y - ring + dy);
                    if (!grid.IsWalkable(c) || grid.GetOccupant(c) != null) continue;
                    if (grid.GetFeatureAt(c) != FeatureType.Plain) continue;
                    tried++;
                    Vector2 w = CellCenterWorld(c);
                    _scanBuf.Clear();
                    PerceptionSystem.QueryNearby(w, GridMath.VisualToWorld(rangeProbe + 4f), Faction.PlayerCamp, true, _scanBuf);
                    if (_scanBuf.Count == 0) { baseCell = c; siteFound = true; }
                }
        if (!siteFound) { Debug.LogError("[HH320] 找不到隔离试区。"); TestHarnessApi.ExitTestRun(); yield break; }

        var shooter = SpawnUnitOf(Faction.PlayerCamp, Occupation.Archer, CellCenterWorld(baseCell), 0);
        var shooterMelee = SpawnUnitOf(Faction.PlayerCamp, Occupation.Warrior, CellCenterWorld(new GridCoord(baseCell.x, baseCell.y - 6)), 0);
        if (shooter == null || shooterMelee == null) { Debug.LogError("[HH320] 造单位失败。"); TestHarnessApi.ExitTestRun(); yield break; }
        float range = ReadSnapshotFloat(shooter, "attackRange");
        L($"[布置] 试区格=({baseCell.x},{baseCell.y})（隔离扫描 tried={tried} 内无敌）"
          + $" 射手 attackRange={range.ToString("F3", CultureInfo.InvariantCulture)} isRanged={ReadSnapshotBool(shooter, "isRanged")}"
          + $" ballistic={ReadSnapshotValue(shooter, "ballisticType")} | 近战单位 isRanged={ReadSnapshotBool(shooterMelee, "isRanged")}");
        int dxT = Mathf.Max(2, Mathf.RoundToInt(Mathf.Clamp(range - 2f, 2f, 4f)));
        var targetCell = new GridCoord(baseCell.x + dxT, baseCell.y);
        var midCell = new GridCoord(baseCell.x + Mathf.Max(1, dxT / 2), baseCell.y);
        var target = SpawnUnitOf(Faction.PlayerCamp, Occupation.Archer, CellCenterWorld(targetCell), 1);
        if (target == null) { Debug.LogError("[HH320] 造目标失败。"); TestHarnessApi.ExitTestRun(); yield break; }
        yield return null;
        L($"[布置] 靶@格({targetCell.x},{targetCell.y}) 阵营={target.GetFaction()} 屏蔽格=({midCell.x},{midCell.y}) 靶距={dxT}格"
          + $" | 射程内敌数={EnemiesInRange(shooter, range)}");

        Debug.Log("[HH320] 阶段：J4~J10 视线组 开始");
        // ── 判据 4：隔山壁 ⇒ 不索敌；拆掉山壁（正门 MapGate.SetFeature）⇒ 索敌成功 ──
        bool b0 = Acquire(shooter, CellCenterWorld(baseCell), target, out string w0s);
        bool setM = MapGate.SetFeature(midCell, FeatureType.Mountain);
        yield return null;
        bool bMountain = Acquire(shooter, CellCenterWorld(baseCell), target, out string wMs);
        DiagLos(grid, baseCell, targetCell, midCell, "J4隔山壁");
        bool setP = MapGate.SetFeature(midCell, FeatureType.Plain);
        yield return null;
        bool bRestored = Acquire(shooter, CellCenterWorld(baseCell), target, out string wRs);
        L($"[J4] SetFeature(Mountain)={setM} SetFeature(Plain)={setP} | 无阻挡基线={b0}({w0s}) | 隔山壁={bMountain}({wMs}) | 拆山壁后={bRestored}({wRs})");

        // ── 判据 5：隔建筑占格 ⇒ 不索敌（真建筑 · 墙 isObstacle=1）──
        var wallDef = Resources.Load<BuildingDef>("Buildings/wall");
        Building wallB = null;
        if (wallDef != null)
        {
            var fpW = new Vector2Int(Mathf.Max(1, wallDef.footprint.x), Mathf.Max(1, wallDef.footprint.y));
            BuildingFactory.Instance.CreateBuildingInstance(wallDef, wallDef.sourceType, midCell, fpW, CellCenterWorld(midCell),
                isPlayerBuilt: false, grade: ResourceGrade.Normal, isConsumable: false,
                initialState: BuildingState.Active, kingdomId: 0);
            wallB = grid.GetOccupant(midCell) as Building;
        }
        yield return null;
        bool bBuilding = Acquire(shooter, CellCenterWorld(baseCell), target, out string wBs);
        string wallNote = wallB != null ? "wall.asset 落地" : "建墙失败";
        L($"[J5] 建筑={wallNote} 占格BuildingBlocked={ (grid.GetWalkFlags(midCell) & WalkFlags.BuildingBlocked) != 0 }"
          + $" 隔建筑占格索敌={bBuilding}({wBs})");
        if (wallB != null) { grid.Free(midCell); BuildingRegistry.Instance?.Unregister(wallB); Object.DestroyImmediate(wallB.gameObject); }
        yield return null;
        bool bAfterClean = Acquire(shooter, CellCenterWorld(baseCell), target, out string wCs);
        L($"[J5·清理后对照] 拆建筑后索敌={bAfterClean}({wCs})");

        // ── 判据 6：隔工事单位（Wall / blocksMovement=0 的塔 两例）──
        var wallFort = Resources.Load<FortificationDef>("Fortifications/Wall");
        var towerFort = Resources.Load<FortificationDef>("Fortifications/ArrowTower");
        var fWall = SpawnUnitOf(Faction.PlayerCamp, Occupation.Warrior, CellCenterWorld(midCell), 0);
        if (fWall != null) fWall.fortification = wallFort;
        yield return null;
        bool bFortWall = Acquire(shooter, CellCenterWorld(baseCell), target, out string wFW);
        string wallF = wallFort != null ? ("blocksMovement=" + wallFort.blocksMovement + " heightCells=" + wallFort.heightCells) : "资产缺";
        L($"[J6] 工事=Wall({wallF}) 索敌={bFortWall}({wFW})");
        if (fWall != null) fWall.fortification = towerFort;
        yield return null;
        bool bFortTower = Acquire(shooter, CellCenterWorld(baseCell), target, out string wFT);
        string towerF = towerFort != null ? ("blocksMovement=" + towerFort.blocksMovement + " heightCells=" + towerFort.heightCells) : "资产缺";
        L($"[J6] 工事=ArrowTower({towerF}) 索敌={bFortTower}({wFT})");
        if (fWall != null) fWall.fortification = null;
        yield return null;

        // ── 判据 7：隔河（Water）⇒ 照常索敌 ──
        MapGate.SetFeature(midCell, FeatureType.River);
        yield return null;
        bool bRiver = Acquire(shooter, CellCenterWorld(baseCell), target, out string wRv);
        L($"[J7] 隔河(River) 索敌={bRiver}({wRv}) flagWater={(grid.GetWalkFlags(midCell) & WalkFlags.Water) != 0}");
        MapGate.SetFeature(midCell, FeatureType.Plain);
        yield return null;

        // ── 判据 8：隔 Locked（工地）⇒ 照常索敌 ──
        SetLocked(midCell, true);
        yield return null;
        bool bLocked = Acquire(shooter, CellCenterWorld(baseCell), target, out string wLk);
        L($"[J8] 隔 Locked 索敌={bLocked}({wLk}) flagLocked={(grid.GetWalkFlags(midCell) & WalkFlags.Locked) != 0} 该格可走={grid.IsWalkable(midCell)}");
        SetLocked(midCell, false);
        yield return null;

        // ── 判据 9：高抛（HighArc）⇒ 照常索敌（豁免）──
        MapGate.SetFeature(midCell, FeatureType.Mountain);
        yield return null;
        bool bLowArc = Acquire(shooter, CellCenterWorld(baseCell), target, out string wLA);
        SetSnapshotValue(shooter, "ballisticType", BallisticType.HighArc);
        yield return null;
        bool bHighArc = Acquire(shooter, CellCenterWorld(baseCell), target, out string wHA);
        SetSnapshotValue(shooter, "ballisticType", BallisticType.Lob);
        yield return null;
        bool bRestoreArc = Acquire(shooter, CellCenterWorld(baseCell), target, out string wRA);
        L($"[J9] 隔山壁：低抛(Lob)={bLowArc}({wLA}) | 高抛(HighArc)={bHighArc}({wHA}) | 还原 Lob={bRestoreArc}({wRA})");

        // ── 判据 10：近战（isRanged=false）⇒ 照常索敌（⛔ 不查视线）──
        // ⚠️ 半径取 dxT+0.5 格：**留 0.5 格余量**避「恰好等半径」的浮点刀锋（探针构造纪律）
        float probeWorld = GridMath.VisualToWorld(dxT + 0.5f);
        bool meleeNoSight = InvokeFindNearest(shooter, probeWorld, false, target, out string wM0);
        bool rangedNeedSight = InvokeFindNearest(shooter, probeWorld, true, target, out string wM1);
        L($"[J10] 同一几何（隔山壁）FindNearestEnemy(isRanged=false)={meleeNoSight}({wM0})"
          + $" / isRanged=true={rangedNeedSight}({wM1})");
        MapGate.SetFeature(midCell, FeatureType.Plain);
        yield return null;

        // ── 判据 2（生产路径）：射程边界两向 ──
        var targetPos = CellCenterWorld(targetCell);
        bool inAtR = Acquire(shooter, CellCenterWorld(baseCell), target, out string wP1);
        var farCell = new GridCoord(baseCell.x + Mathf.CeilToInt(range) + 1, baseCell.y);
        MoveUnit(target, CellCenterWorld(farCell));
        yield return null;
        bool farOut = Acquire(shooter, CellCenterWorld(baseCell), target, out string wP2);
        MoveUnit(target, targetPos);
        yield return null;
        bool backIn = Acquire(shooter, CellCenterWorld(baseCell), target, out string wP3);
        L($"[J2·生产路径] 靶@+{dxT}格={inAtR}({wP1}) | 靶@+{Mathf.CeilToInt(range) + 1}格={farOut}({wP2}) | 复位={backIn}({wP3})");
        // 边界含（⭐ 恰好 R 格 · 格中心取点）：生产链实测 ＋ 浮点比值
        int rb = Mathf.RoundToInt(range);
        var edgeCell = new GridCoord(baseCell.x + rb, baseCell.y);
        Vector2 edgePos = CellCenterWorld(edgeCell);
        MoveUnit(target, edgePos);
        yield return null;
        bool edgeIn = Acquire(shooter, CellCenterWorld(baseCell), target, out string wE);
        float dvEdge = GridMath.DistVisual(CellCenterWorld(baseCell), edgePos);
        _scanBuf.Clear();
        PerceptionSystem.QueryNearby(CellCenterWorld(baseCell), GridMath.VisualToWorld(range), Faction.PlayerCamp, true, _scanBuf);
        L($"[J2·边界含] 靶@+{rb}格(格中心) 索敌={edgeIn}({wE}) DistVisual={dvEdge.ToString("R", CultureInfo.InvariantCulture)}"
          + $" R={range} 比值={ (dvEdge / range).ToString("R", CultureInfo.InvariantCulture) } 感知门命中数={_scanBuf.Count}");
        MoveUnit(target, targetPos);
        yield return null;

        Debug.Log("[HH320] 阶段：J11 成本组 开始");
        // 成本组准备：把工事单位挪到**路径外**并挂真工事（使索引式集合 ≥1 条 ⇒ 三形状同为"通视"场景）
        if (fWall != null) { fWall.fortification = wallFort; MoveUnit(fWall, CellCenterWorld(new GridCoord(baseCell.x, baseCell.y - 3))); }
        yield return null;
        // ── 判据 11：三形状成本对照 ＋ 候选数 ＋ GC 读数 ──
        CostRun(shooter, grid, CellCenterWorld(baseCell), CellCenterWorld(targetCell), midCell, range);
        Debug.Log("[HH320] 阶段：J11 成本组 完成");

        // ── 清理（探针构造物）──
        foreach (var u in new[] { target, fWall, shooter, shooterMelee })
            if (u != null) { if (UnitRegistry.Instance != null) UnitRegistry.Instance.Unregister(u); Object.DestroyImmediate(u.gameObject); }
        yield return null;
        L($"[清理] 余留在册单位={UnitRegistry.Instance.Count} 屏蔽格状态={MidDump(grid, midCell)}");

        TestHarnessApi.ExitTestRun();
        L("[收尾] ExitTestRun 已执行（Play 由执行端外部 stop · ⛔ 不留 1x 余留世界）");
        Flush("live");
    }

    // ===== 生产函数调用（反射 · 返回"是否命中**本探针靶**"＝身份判定，防他国单位污染读数）=====
    static bool Acquire(UnitController shooter, Vector2 at, UnitController expect, out string why)
    {
        MoveUnit(shooter, at);
        var m = typeof(UnitController).GetMethod("FindNearestEnemyInRange", BF);
        var r = m.Invoke(shooter, null) as IDamageable;
        why = Describe(r);
        return ReferenceEquals(r, expect);
    }

    static bool InvokeFindNearest(UnitController shooter, float rangeWorld, bool isRanged, UnitController expect, out string why)
    {
        var m = typeof(UnitController).GetMethod("FindNearestEnemy", BF);
        var r = m.Invoke(shooter, new object[] { rangeWorld, isRanged }) as IDamageable;
        why = Describe(r);
        return ReferenceEquals(r, expect);
    }

    static string Describe(IDamageable d)
    {
        if (d == null) return "null";
        if (d is UnitController uc) return $"unit:{(uc.Data != null ? uc.Data.name : "?")}:{uc.GetFaction()}";
        if (d is Building b) return $"bld:{(b.def != null ? b.def.name : "?")}:{b.GetFaction()}";
        return d.ToString();
    }

    /// <summary>⭐ 格**中心**世界坐标（＝格内连续坐标 (gx+0.5, gy+0.5)）。
    /// ⚠️ 为什么不用 `CoordToWorld`（格点＝菱形下顶点）：顶点落在子格边界上（连续坐标恰为整数），
    /// 浮点误差会让 `floor` 翻到相邻子格/相邻行 ⇒ 探针路径与屏蔽格错位（**构造法缺陷**，非生产缺陷）。
    /// 格中心严格落在格内 ⇒ 子格归属稳定。</summary>
    static Vector2 CellCenterWorld(GridCoord c)
        => GridSystem.CellToWorldF(c.x + 0.5f, c.y + 0.5f, new Vector2(GridMath.CellW, GridMath.CellH));

    static void MoveUnit(UnitController u, Vector2 pos)
    {
        u.transform.position = pos;
        var rb = typeof(UnitController).GetField("_rb", BF)?.GetValue(u) as Rigidbody2D;
        if (rb != null) rb.position = pos;
        Physics2D.SyncTransforms();
    }

    static UnitController SpawnUnitOf(Faction f, Occupation occ, Vector2 pos, int kingdomId)
    {
        var go = UnitFactory.Instance.SpawnUnit(f, occ, pos, kingdomId);
        var uc = go != null ? go.GetComponent<UnitController>() : null;
        if (uc != null) MoveUnit(uc, pos);
        return uc;
    }

    static int EnemiesInRange(UnitController shooter, float rangeVisual)
    {
        _scanBuf.Clear();
        PerceptionSystem.QueryNearby(shooter.transform.position, GridMath.VisualToWorld(rangeVisual), shooter.GetFaction(), true, _scanBuf);
        return _scanBuf.Count;
    }

    static object ReadSnapshotValue(UnitController uc, string field)
    {
        var snap = typeof(UnitController).GetField("_professionSnapshot", BF).GetValue(uc);
        return snap.GetType().GetField(field).GetValue(snap);
    }

    static float ReadSnapshotFloat(UnitController uc, string field) => (float)ReadSnapshotValue(uc, field);
    static bool ReadSnapshotBool(UnitController uc, string field) => (bool)ReadSnapshotValue(uc, field);

    static void SetSnapshotValue(UnitController uc, string field, object val)
    {
        var snapField = typeof(UnitController).GetField("_professionSnapshot", BF);
        var snap = snapField.GetValue(uc);                     // 装箱副本
        snap.GetType().GetField(field).SetValue(snap, val);
        snapField.SetValue(uc, snap);                          // 写回
    }

    static FieldInfo _wfField;

    static void SetLocked(GridCoord cell, bool on)
    {
        var grid = GridSystem.Instance;
        _wfField ??= typeof(GridSystem).GetField("_walkFlags", BF);
        var arr = _wfField.GetValue(grid) as WalkFlags[];
        if (arr == null) return;
        int div = grid.SubDiv, sw = grid.Width;
        for (int sy = 0; sy < div; sy++)
            for (int sx = 0; sx < div; sx++)
            {
                int idx = (cell.y * div + sy) * sw + (cell.x * div + sx);
                if (idx < 0 || idx >= arr.Length) continue;
                if (on) arr[idx] |= WalkFlags.Locked; else arr[idx] &= ~WalkFlags.Locked;
            }
    }

    static string MidDump(GridSystem grid, GridCoord midCell)
        => $"feature={grid.GetFeatureAt(midCell)} flags={(int)grid.GetWalkFlags(midCell)} occupant={(grid.GetOccupant(midCell) != null)}";

    /// <summary>诊断：LOS 内部逐段读数（生产函数 ＋ 逐微格枚举），用于定位「地形段未拦」的落点。</summary>
    static void DiagLos(GridSystem g, GridCoord aCell, GridCoord bCell, GridCoord midCell, string tag)
    {
        Vector2 A = CellCenterWorld(aCell), B = CellCenterWorld(bCell);
        var sA = g.WorldToSubCoord(A);
        var sB = g.WorldToSubCoord(B);
        var map = WorldManager.Instance.ActiveMap;
        L($"[{tag}·诊断] A={A} B={B} subA={sA} subB={sB}"
          + $" | grid.GetFeatureAt(mid)={g.GetFeatureAt(midCell)} MapGate.ReadAt(mid)={MapGate.ReadAt(map, midCell.x, midCell.y)}"
          + $" IsSightBlockingFeature={GridSystem.IsSightBlockingFeature(g.GetFeatureAt(midCell))}"
          + $" midFlags={(int)g.GetWalkFlags(midCell)} LOS={CombatRules.HasLineOfSight(A, B)}"
          + $" 同一实例={ReferenceEquals(map.features, null)}");
        if (!sA.HasValue || !sB.HasValue) { L($"[{tag}·诊断] 子格越界 ⇒ 生产函数走「越界保守放行」分支"); return; }
        int x0 = sA.Value.x, y0 = sA.Value.y, x1 = sB.Value.x, y1 = sB.Value.y;
        int ddx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int ddy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = ddx + ddy;
        var sb = new StringBuilder();
        int guard = 0;
        while (guard++ < 64)
        {
            var cell = g.SubToCell(new GridCoord(x0, y0));
            sb.Append('(').Append(x0).Append(',').Append(y0).Append(")c(").Append(cell.x).Append(',').Append(cell.y).Append(')')
              .Append(g.GetFeatureAt(cell)).Append(":w").Append((int)g.GetWalkFlagsSub(new GridCoord(x0, y0))).Append(' ');
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= ddy) { err += ddy; x0 += sx; }
            if (e2 <= ddx) { err += ddx; y0 += sy; }
        }
        L($"[{tag}·诊断·路径] {sb}");
    }

    // ===== 判据 11：三形状成本对照 =====
    static void CostRun(UnitController shooter, GridSystem grid, Vector2 a, Vector2 b, GridCoord midCell, float range)
    {
        int pathLen = Mathf.RoundToInt(range * grid.SubDiv);
        int candidates = ReadQueryCount(shooter);
        L($"[J11] 候选数(射程内敌数·生产缓冲)={candidates} 全库单位={UnitRegistry.Instance.Count} 建筑={BuildingRegistry.Instance?.Count ?? -1}"
          + $" 路径微格数L≈{pathLen}");

        // 形状①：带测法（生产 `HasLineOfSight` 全段）
        {
            int iter = 500;
            for (int i = 0; i < 12; i++) CombatRules.HasLineOfSight(a, b);
            int gc0 = System.GC.CollectionCount(0);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iter; i++) CombatRules.HasLineOfSight(a, b);
            sw.Stop();
            int gc = System.GC.CollectionCount(0) - gc0;
            L($"[J11·形状①带测法全段] HasLineOfSight ×{iter} 单次={ (sw.Elapsed.TotalMilliseconds * 1000.0 / iter).ToString("F2", CultureInfo.InvariantCulture) }µs GC0Δ={gc}");
        }
        // 形状①b：仅工事段（唯一口）
        {
            int iter = 500;
            float band = GridMath.PathBandHalf;
            for (int i = 0; i < 12; i++) CombatRules.FindFortificationBlocker(a, b, band, false, 0f);
            int gc0 = System.GC.CollectionCount(0);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iter; i++) CombatRules.FindFortificationBlocker(a, b, band, false, 0f);
            sw.Stop();
            int gc = System.GC.CollectionCount(0) - gc0;
            L($"[J11·形状①b仅工事段] FindFortificationBlocker ×{iter} 单次={ (sw.Elapsed.TotalMilliseconds * 1000.0 / iter).ToString("F2", CultureInfo.InvariantCulture) }µs GC0Δ={gc}");
        }
        var sa = grid.WorldToSubCoord(a);
        var sb2 = grid.WorldToSubCoord(b);
        // 形状②：逐微格 GetUnitsInSubCell（对照复刻 · 只测量不改生产）
        {
            int iter = 10;
            int gc0 = System.GC.CollectionCount(0);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int hits = 0;
            for (int i = 0; i < iter; i++)
            {
                if (sa.HasValue && sb2.HasValue)
                {
                    int x0 = sa.Value.x, y0 = sa.Value.y, x1 = sb2.Value.x, y1 = sb2.Value.y;
                    int ddx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
                    int ddy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
                    int err = ddx + ddy;
                    while (true)
                    {
                        var list = grid.GetUnitsInSubCell(new GridCoord(x0, y0));
                        for (int k = 0; k < list.Count; k++) if (list[k].fortification != null) hits++;
                        if (x0 == x1 && y0 == y1) break;
                        int e2 = 2 * err;
                        if (e2 >= ddy) { err += ddy; x0 += sx; }
                        if (e2 <= ddx) { err += ddx; y0 += sy; }
                    }
                }
            }
            sw.Stop();
            int gc = System.GC.CollectionCount(0) - gc0;
            L($"[J11·形状②逐微格] GetUnitsInSubCell ×{iter} 单次={ (sw.Elapsed.TotalMilliseconds * 1000.0 / iter).ToString("F1", CultureInfo.InvariantCulture) }µs GC0Δ={gc} hits={hits}");
        }
        // 形状③：索引式（一次收集工事子格 ⇒ 逐微格 O(1) 查）
        {
            var sw0 = System.Diagnostics.Stopwatch.StartNew();
            var fortSubs = new HashSet<GridCoord>();
            var e = UnitRegistry.Instance.GetUnitsEnumerator();
            while (e.MoveNext())
            {
                var uc = e.Current;
                if (uc == null || uc.fortification == null) continue;
                var sc = grid.WorldToSubCoord(uc.transform.position);
                if (sc.HasValue) fortSubs.Add(sc.Value);
            }
            sw0.Stop();
            int iter = 500;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iter; i++)
            {
                if (sa.HasValue && sb2.HasValue)
                {
                    int x0 = sa.Value.x, y0 = sa.Value.y, x1 = sb2.Value.x, y1 = sb2.Value.y;
                    int ddx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
                    int ddy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
                    int err = ddx + ddy;
                    while (true)
                    {
                        if (fortSubs.Contains(new GridCoord(x0, y0))) break;
                        if (x0 == x1 && y0 == y1) break;
                        int e2 = 2 * err;
                        if (e2 >= ddy) { err += ddy; x0 += sx; }
                        if (e2 <= ddx) { err += ddx; y0 += sy; }
                    }
                }
            }
            sw.Stop();
            L($"[J11·形状③索引式] 收集工事子格×1={sw0.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)}ms（集合={fortSubs.Count}）"
              + $" 逐微格查 ×{iter} 单次={ (sw.Elapsed.TotalMilliseconds * 1000.0 / iter).ToString("F2", CultureInfo.InvariantCulture) }µs");
        }
        var aCell = grid.WorldToCoord(a) ?? midCell;
        L($"[J11·地形段对照] 起点格 feature={grid.GetFeatureAt(aCell)} 屏蔽格={MidDump(grid, midCell)}");
    }

    static int ReadQueryCount(UnitController shooter)
    {
        var list = typeof(UnitController).GetField("_queryResults", BF).GetValue(shooter) as System.Collections.IList;
        return list != null ? list.Count : -1;
    }

    // ========================================================================
    //  B′ · 第三段 件 B · **件 6 生产链实测**（`D847` §2-1 返工验证）
    //   ⭐ 载体 ＝ 生产链：`DamageSystem.RegisterAttack` → `ExecuteAttack` → `ProjectileManager.SpawnProjectile`
    //      → `ProjectileManager.Update` → `OnProjectileArrived` → `FindBuildingAtLanding` → `DamageSystem.ApplyDamage`
    //   ⚠️ 构造法：弹道档（`AttackProfile`）为探针构造；建筑/单位走生产生成口
    //      （`BuildingFactory.CreateBuildingInstance` / `UnitFactory.SpawnUnit`）。
    //   ⚠️ 速档 ＝ 1×（弹道推进靠 `Update`/`Time.deltaTime` ⇒ 需真实时间）；弹速取 60 世界单位/秒
    //      ⇒ 飞行 ≈2 帧 ⇒ 单位漂移可忽略（B2/B3 的"靶仍在落点"前提成立）。
    // ========================================================================

    [MenuItem("Valley/验证/HH320 件6建筑命中_生产链(第三段B)")]
    public static void RunBuildHit()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH320B] 须先进入 Play 后调用本菜单。"); return; }
        new GameObject("HH320B_Host").AddComponent<RunHost>().Host(RunBuildHitCo());
    }

    /// <summary>供 bridge `exec_runtime_script` 直接 return 执行；菜单路径走 <see cref="RunBuildHit"/>。</summary>
    public static IEnumerator RunBuildHitCoroutine() => RunBuildHitCo();

    static IEnumerator RunBuildHitCo()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = 20321, mapSeed = 20321, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = "smoke_w320bh", kingdomName = "件6实测"
        };
        Debug.Log("[HH320B] 阶段：EnterTestRun（1× 速）");
        yield return TestHarnessApi.EnterTestRun(cfg, 1f);
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || GridSystem.Instance == null || GridSystem.Instance.Config == null
               || BuildingRegistry.Instance == null || UnitFactory.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 180f) { Debug.LogError("[HH320B] 等世界就绪超时。"); yield break; }
        }
        yield return new WaitForSeconds(0.5f);

        var grid = GridSystem.Instance;
        var dmgCfg = Resources.Load<DamageConfig>("Config/DamageConfig");
        float hitR = dmgCfg != null ? dmgCfg.hitRadiusCells : 0.25f;
        L("=== B′ · HH.320 件 6 生产链实测（`ProjectileManager.OnProjectileArrived`）===");
        L($"[环境] seed={WorldManager.Instance.ActiveMap.seed} 单位在册={UnitRegistry.Instance.Count} 建筑在册={BuildingRegistry.Instance.Count}"
          + $" timeScale={Time.timeScale} hitRadiusCells={hitR} cellSize={grid.Config.cellSize} 格步长={GridMath.CellStep.ToString("F6", CultureInfo.InvariantCulture)}");

        var anchorCell = grid.WorldToCoord(WorldManager.Instance.GetKingdomAnchorWorld());
        if (!anchorCell.HasValue) { Debug.LogError("[HH320B] 锚点格取不到。"); TestHarnessApi.ExitTestRun(); yield break; }

        // ── 建筑选择：非工事建筑（farm / Warehouse 皆非 Wall/Gate/Bridge/Defense）──
        var bDef = Resources.Load<BuildingDef>("Buildings/farm");
        if (bDef == null) bDef = Resources.Load<BuildingDef>("Buildings/Warehouse");
        if (bDef == null) { Debug.LogError("[HH320B] 找不到非工事建筑资产。"); TestHarnessApi.ExitTestRun(); yield break; }
        var fp = new Vector2Int(Mathf.Max(1, bDef.footprint.x), Mathf.Max(1, bDef.footprint.y));

        // ── 隔离选址：footprint 全域空 ＋ 副格可走 ＋ Plain ＋ 12 格内无单位 ──
        GridCoord bCell = default; bool found = false; int tried = 0;
        for (int ring = 24; ring <= 108 && !found; ring += 12)
            for (int dx = -24; dx <= 24 && !found; dx += 12)
                for (int dy = -24; dy <= 24 && !found; dy += 12)
                {
                    var c = new GridCoord(anchorCell.Value.x + dx, anchorCell.Value.y - ring + dy);
                    tried++;
                    bool ok = true;
                    for (int fx = 0; fx < fp.x && ok; fx++)
                        for (int fy = 0; fy < fp.y && ok; fy++)
                        {
                            var cc = new GridCoord(c.x + fx, c.y + fy);
                            if (!grid.IsWalkable(cc) || grid.GetOccupant(cc) != null || grid.GetFeatureAt(cc) != FeatureType.Plain) ok = false;
                        }
                    if (!ok) continue;
                    Vector2 w = CellCenterWorld(c);
                    _scanBuf.Clear();
                    PerceptionSystem.QueryNearby(w, GridMath.VisualToWorld(12f), Faction.PlayerCamp, true, _scanBuf);
                    _scanBuf.Clear();
                    PerceptionSystem.QueryNearby(w, GridMath.VisualToWorld(12f), Faction.AiKingdom, true, _scanBuf);
                    if (_scanBuf.Count == 0) { bCell = c; found = true; }
                }
        if (!found) { Debug.LogError("[HH320B] 找不到隔离场址。"); TestHarnessApi.ExitTestRun(); yield break; }

        // ── 落成建筑（归属 AI 国 ⇒ 对 PlayerCamp 射手的 `FindBuildingAtLanding` 是敌对 ✓）──
        Vector2 bCenter = GridSystem.FootprintCenterWorld(bCell, fp, Vector3.zero);   // 静态重载（⛔ 不得经实例调用）
        bool built = BuildingFactory.Instance.CreateBuildingInstance(
            bDef, bDef.sourceType, bCell, fp, bCenter,
            isPlayerBuilt: false, grade: ResourceGrade.Normal, isConsumable: false,
            initialState: BuildingState.Active, kingdomId: 1);
        yield return null;
        var bld = grid.GetOccupant(bCell) as Building;
        if (!built || bld == null) { Debug.LogError("[HH320B] 建筑落成失败。"); TestHarnessApi.ExitTestRun(); yield break; }
        L($"[布置] 建筑={bDef.name} footprint=({fp.x},{fp.y}) 主格=({bCell.x},{bCell.y}) 位置={bCenter} 阵营={bld.GetFaction()}"
          + $" HP={bld.CurrentHp}/{bld.MaxHp} IsActive={bld.IsActive} 工事?={(bld.IsFortification ? "是(⛔异常)" : "否")}"
          + $"（场址扫描 tried={tried} · 12 格内无敌）");

        // ── 射手：建筑中心沿 −gx 3 格（格轴 ⇒ DistVisual≈3 ≤ range6）──
        Vector2 bc = GridSystem.WorldToCellF(bCenter, grid.Config.cellSize);
        Vector2 sPos = GridSystem.CellToWorldF(bc.x - 3f, bc.y, grid.Config.cellSize);
        var shooter = SpawnUnitOf(Faction.PlayerCamp, Occupation.Archer, sPos, 0);
        if (shooter == null) { Debug.LogError("[HH320B] 造射手失败。"); TestHarnessApi.ExitTestRun(); yield break; }
        yield return null;
        L($"[布置] 射手@格轴偏移 -3 位置={sPos} 与建筑中心 DistVisual={GridMath.DistVisual(sPos, bCenter).ToString("F3", CultureInfo.InvariantCulture)}");

        var profile = new AttackProfile
        {
            attack = 25, range = 6f, cd = 1f, isRanged = true, projectileSpeed = 60f,
            projectileType = ProjectileType.Arrow, pierceLevel = 1,
            ballisticType = BallisticType.Lob, arcHeightCells = 0f,
            aoeRadiusCells = 0f, aoeFalloff = 0f,
            effectType = GroundEffectType.None,
        };

        // ══ 诊断 0：链路在场性 ＋ 越墙路径是否误挡 ＋ **散布档实读** ══
        float errR = ReadErrorRadius();
        L($"[诊断0] PM.Instance={ProjectileManager.Instance != null} DS.Instance={DamageSystem.Instance != null}"
          + $" PM.active={ActiveProjectiles()} 注册存在={RegExists(shooter)}"
          + $" 该段工事阻挡(CheckWallBlock 同参)={BlockerNote(sPos, bCenter)}"
          + $" 射手位置={shooter.GetPosition()} 射手IsAlive={shooter.IsAlive}");
        L($"[诊断0·散布档] DamageConfig.projectileErrorRadius={errR}（世界单位 ⇒ ≈{ (errR / GridMath.CellStep).ToString("F2", CultureInfo.InvariantCulture) } 格轴）"
          + $" · hitRadiusCells={hitR} · 建筑底座半宽={fp.x * 0.5f}/{fp.y * 0.5f} 格"
          + $" ⇒ ⚠️ 单发落点散布(半径 {errR})**远大于** 2×2 底座半宽(1 格) ⇒ 生产档下「落点在底座内」不成立（下 B1′ 为照旧读数）");

        // ══ B1′ · **散布照旧（生产值）** 连打 4 发 ⇒ 统计命中建筑次数（诚实读数 ＋ 正对照）══
        int hpB0 = bld.CurrentHp;
        int hitShots = 0;
        var shotLog = new StringBuilder();
        for (int i = 0; i < 4; i++)
        {
            int hA = bld.CurrentHp;
            bool ok = DamageSystem.Instance.RegisterAttack(shooter, bld, profile);
            yield return new WaitForSecondsRealtime(0.45f);
            int hB = bld.CurrentHp;
            if (hB < hA && ok) hitShots++;
            shotLog.Append($"[{i}:{hA}→{hB}]");
        }
        L($"[B1′·散布照旧(1.5)] 连打 4 发 ⇒ 建筑 HP {hpB0} → {bld.CurrentHp} 命中发数={hitShots}/4 逐发={shotLog}"
          + $"（⚠️ 落点被 1.5 世界单位散布甩出 2×2 底座是**常态** ⇒ 单发 miss 属散布后果，非件 6 失效）");

        // ══ B1-正对照：**手工** `DamageSystem.ApplyDamage`（分离"弹道到位"与"伤害链"两段）══
        int hpM0 = bld.CurrentHp;
        int dealt = DamageSystem.Instance.ApplyDamage(shooter, bld, 25, 0f, true);
        int hpM1 = bld.CurrentHp;
        L($"[B1-正对照·手工ApplyDamage] 返回={dealt} 建筑 HP {hpM0} → {hpM1}（Δ={hpM0 - hpM1}）"
          + $"｜Δ>0 ⇒ 伤害链对建筑有效（故障面只在弹道命中判定）");

        // ══ ⭐ 关闭散布（**探针构造 · 运行期字段 · 收尾还原**）：使"落点＝瞄准点"确定 ⇒ 才可判"落点在底座内" ══
        SetErrorRadius(0f);
        yield return null;
        L($"[构造] projectileErrorRadius 1.5 → {ReadErrorRadius()}（运行期改内存实例 · 收尾还原 1.5 · ⛔ 不改资产文件）");

        // ══ B1 · 散布关 · 落点附近**无任何单位** ＋ 落点=建筑中心（底座内）⇒ 期望命中建筑 ══
        int c1 = CandidatesAt(bCenter, hitR);
        int hp0 = bld.CurrentHp;
        bool fired1 = DamageSystem.Instance.RegisterAttack(shooter, bld, profile);
        yield return new WaitForSecondsRealtime(0.6f);
        int hp1 = bld.CurrentHp;
        int c1b = CandidatesAt(bCenter, hitR);
        L($"[B1] 散布关 · 落点=建筑中心{ bCenter }（底座内）· 生产候选数 发射时={c1} 到达后={c1b}（期望 0/0）"
          + $" · RegisterAttack={fired1} PM.active={ActiveProjectiles()}"
          + $" ⇒ 建筑 HP {hp0} → {hp1}（Δ={hp0 - hp1}）｜期望：Δ>0（命中建筑 · ⛔ 非 miss）");

        // ══ B2 · 散布关 · 落点附近**有友方单位**（同阵营 ⇒ 被过滤）＋ 落点在底座内 ⇒ 期望建筑兜底生效 ══
        var friend = SpawnUnitOf(Faction.PlayerCamp, Occupation.Civilian, bCenter, 0);
        yield return null;
        if (friend != null) MoveUnit(friend, bCenter);
        yield return null;
        int c2 = CandidatesAt(bCenter, hitR);
        int hp2 = bld.CurrentHp;
        bool fired2 = DamageSystem.Instance.RegisterAttack(shooter, bld, profile);
        yield return new WaitForSecondsRealtime(0.6f);
        int hp3 = bld.CurrentHp;
        int c2b = CandidatesAt(bCenter, hitR);   // 到达后复核（证"友方确在命中半径内且被过滤"）
        int friendHp = friend != null ? friend.CurrentHp : -1;
        L($"[B2] 散布关 · 落点=建筑中心（底座内）· 友方单位(同阵营 PlayerCamp)@落点 生产候选数 发射时={c2} 到达后={c2b}（期望 ≥1）"
          + $" · RegisterAttack={fired2}"
          + $" ⇒ 建筑 HP {hp2} → {hp3}（Δ={hp2 - hp3}）｜友方 HP={friendHp}（期望不受伤）｜期望：建筑 Δ>0（兜底生效）");

        // ══ B3 · 散布关 · 对照（鉴别力·核心）：落点**底座外**且落点上有**友方**单位（被阵营过滤）
        //      ⇒ 建筑兜底**确实被求值**，但因底座判据为 False ⇒ 返回 null ⇒ 全 miss ⇒ 建筑 Δ=0 ══
        Vector2 outPos = GridSystem.CellToWorldF(bc.x + 2f, bc.y, grid.Config.cellSize);   // farm 2×2 半宽 1 格 ⇒ +2 格轴在底座外
        var allyAtOut = SpawnUnitOf(Faction.PlayerCamp, Occupation.Civilian, outPos, 0);   // 同阵营 ⇒ 到达时被过滤
        yield return null;
        if (allyAtOut != null) MoveUnit(allyAtOut, outPos);
        yield return null;
        int c3 = CandidatesAt(outPos, hitR);
        bool insideBase = CombatRules.InDiamondBase(outPos, bCenter, fp, grid.Config.cellSize);
        bool inRange = GridMath.DistVisual(sPos, outPos) <= profile.range;
        int hp4 = bld.CurrentHp;
        int allyHp0 = allyAtOut != null ? allyAtOut.CurrentHp : -1;
        bool fired3 = allyAtOut != null
            ? DamageSystem.Instance.RegisterAttack(shooter, allyAtOut, profile)          // 瞄友方（被过滤）⇒ 只可能走建筑兜底
            : DamageSystem.Instance.RegisterAttack(shooter, bld, profile);
        yield return new WaitForSecondsRealtime(0.6f);
        int hp5 = bld.CurrentHp;
        int allyHp1 = allyAtOut != null ? allyAtOut.CurrentHp : -1;
        L($"[B3] 散布关 · 落点=建筑中心 +2 格轴 {outPos}（InDiamondBase={insideBase} 期望 False · 射程内={inRange}（发射前实读））"
          + $" · 生产候选数={c3}（友方 1 ⇒ 到达时被阵营过滤） · RegisterAttack={fired3}"
          + $" ⇒ 建筑 HP {hp4} → {hp5}（Δ={hp4 - hp5}）｜落点友方 HP {allyHp0} → {allyHp1}"
          + $"｜期望：两者 Δ=0（底座外 ⇒ 建筑兜底被求值但返 null ⇒ 全 miss · 鉴别力自证）");

        // ══ B3′ · 同点到达性对照：同一落点改放**敌对**单位 ⇒ 期望该单位 Δ>0（证弹道确已到达该落点）＋建筑仍 Δ=0 ══
        var foe = SpawnUnitOf(Faction.PlayerCamp, Occupation.Warrior, outPos, 1);          // kingdomId=1 ⇒ AiKingdom（敌对）
        yield return null;
        if (foe != null) MoveUnit(foe, outPos);
        yield return null;
        int c4 = CandidatesAt(outPos, hitR);
        int hp6 = bld.CurrentHp;
        int foeHp0 = foe != null ? foe.CurrentHp : -1;
        bool fired4 = foe != null ? DamageSystem.Instance.RegisterAttack(shooter, foe, profile) : false;
        yield return new WaitForSecondsRealtime(0.6f);
        int hp7 = bld.CurrentHp;
        int foeHp1 = foe != null ? foe.CurrentHp : -1;
        L($"[B3′] 散布关 · 同落点(底座外)改放敌对单位 · 生产候选数={c4} · RegisterAttack={fired4}"
          + $" ⇒ 该单位 HP {foeHp0} → {foeHp1}（期望 Δ>0 ＝ 弹道到达该落点）｜建筑 HP {hp6} → {hp7}（期望 Δ=0）");

        // ══ 还原散布档（探针收尾纪律）══
        SetErrorRadius(1.5f);
        L($"[还原] projectileErrorRadius 已还原为 {ReadErrorRadius()}（资产值 1.5 · 内存字段）");

        L($"[复算] 生产链路径：DamageSystem.RegisterAttack → ExecuteAttack → ProjectileManager.SpawnProjectile"
          + $" → Update → OnProjectileArrived(:202) → FindBuildingAtLanding(:336) → DamageSystem.ApplyDamage"
          + $"（修复点：ProjectileManager.cs:258 唯一早退点 · ⛔ 原 :211 候选空即 return 已废）");
        L($"[清理] 建筑={ (bld != null) } 射手={ (shooter != null) } 友方@{bCenter}={ (friend != null) } 友方@底座外={ (allyAtOut != null) } 敌单位@{outPos}={ (foe != null) }");

        // 清理探针构造物
        if (bld != null) { grid.Free(bCell); BuildingRegistry.Instance?.Unregister(bld); Object.DestroyImmediate(bld.gameObject); }
        foreach (var u in new[] { friend, allyAtOut, foe, shooter })
            if (u != null) { UnitRegistry.Instance?.Unregister(u); Object.DestroyImmediate(u.gameObject); }
        yield return null;

        TestHarnessApi.ExitTestRun();
        L("[收尾] ExitTestRun 已执行（Play 由执行端外部 stop · ⛔ 不留余留世界）");
        Flush("buildhit");
    }

    /// <summary>诊断/构造：读/写 `ProjectileManager._config.projectileErrorRadius`（**运行期内存实例** · ⛔ 不写资产）。</summary>
    static float ReadErrorRadius()
    {
        var pm = ProjectileManager.Instance;
        if (pm == null) return -1f;
        var cfg = typeof(ProjectileManager).GetField("_config", BF).GetValue(pm) as DamageConfig;
        return cfg != null ? cfg.projectileErrorRadius : -1f;
    }

    static void SetErrorRadius(float v)
    {
        var pm = ProjectileManager.Instance;
        if (pm == null) return;
        var cfg = typeof(ProjectileManager).GetField("_config", BF).GetValue(pm) as DamageConfig;
        if (cfg != null) cfg.projectileErrorRadius = v;
    }

    /// <summary>诊断：在飞投射物数（`ProjectileManager._active`）。</summary>
    static int ActiveProjectiles()
    {
        var pm = ProjectileManager.Instance;
        if (pm == null) return -1;
        var list = typeof(ProjectileManager).GetField("_active", BF).GetValue(pm) as System.Collections.IList;
        return list != null ? list.Count : -1;
    }

    /// <summary>诊断：该攻方是否在 `DamageSystem` 注册表内。</summary>
    static bool RegExists(UnitController u)
    {
        var ds = DamageSystem.Instance;
        if (ds == null) return false;
        var d = typeof(DamageSystem).GetField("_registrations", BF).GetValue(ds) as System.Collections.IDictionary;
        return d != null && d.Contains(u);
    }

    /// <summary>诊断：该段是否会被越墙判定拦下（`CheckWallBlock` 同参：弧高豁免开）。</summary>
    static string BlockerNote(Vector2 a, Vector2 b)
    {
        var blk = CombatRules.FindFortificationBlocker(a, b, GridMath.PathBandHalf, true, 0f);
        return blk != null ? ("有工事阻挡:" + blk.name) : "null（不挡）";
    }

    /// <summary>生产口读数：落点处 `ProjectileManager.QueryNearbyUnits` 候选数（＝早退判据的实参）。</summary>
    static int CandidatesAt(Vector2 pos, float hitRadiusCells)
    {
        var pm = ProjectileManager.Instance;
        if (pm == null) return -1;
        var m = typeof(ProjectileManager).GetMethod("QueryNearbyUnits", BF);
        var list = m.Invoke(pm, new object[] { pos, hitRadiusCells }) as System.Collections.IList;
        return list != null ? list.Count : -1;
    }
}
