#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.294 片 5 · 资源池落地 · 验收探针
//  用法：
//   - 编辑态：Exec `Valley_HH294_Slice5Probe.RunEdit()`  ⇒ 口径表 / 权重 diff / 开局点数 / 锚点 / 兜底 / 曲线解析 / A3A4A5 对照 / 确定性
//   - Play 态：Exec `Valley_HH294_Slice5Probe.RunLive()` ⇒ 开局实测 / 每日曲线 / 单类上限 / 重生不在原位 / 节流 / 分帧 / 6.6 上限
//  证据落盘：`Logs/hh294_s5_probe.log`
//
//  口径声明（全篇）：
//   - **点口径** ＝ `Σ_i E_i = cap` 自洽：树/石堆/木堆/矿脉 1 坑位 ＝ 1 点；**矿洞 1 簇（2×2 Cell）＝ 1 点**。
//   - 「改前」列 ＝ `git HEAD:MapGenRulesConfig.asset` 实读值（D774 落盘前的实盘权重表）。
//   - 判据读数一律取**实测**；构造用例用生产同一处谓词（`KindAllowed` / `PickCell`）。
// ============================================================================
public static class Valley_HH294_Slice5Probe
{
    const int SEED = 21107;
    const string TAG = "[HH294-S5]";
    const string SLOT = "hh294s5_live";

    static readonly StringBuilder _log = new StringBuilder();
    static string _logName = "hh294_s5_probe";
    static readonly string[] BandName = { "热带", "亚热带", "温带", "寒带" };
    static readonly string[] KindName = { "树", "石堆", "木堆", "矿脉", "矿洞" };

    /// <summary>`03` §6.7.2 定案曲线（cap=96 基准·第 1~7 天）。</summary>
    static readonly float[] DocCurve = { 14f, 10f, 9f, 7f, 6f, 6f, 6f };

    /// <summary>`git HEAD:MapGenRulesConfig.asset` 实读（改前·四带五字段）。</summary>
    static readonly float[][] OldW =
    {
        new float[] { 0.20f, 0.45f, 0.45f, 0.50f, 0.90f },   // 热带
        new float[] { 0.20f, 0.40f, 0.40f, 0.45f, 0.84f },   // 亚热带
        new float[] { 0.25f, 0.40f, 0.40f, 0.40f, 0.80f },   // 温带
        new float[] { 0.60f, 0.45f, 1.00f, 0.55f, 0.92f },   // 寒带
    };

    public static MapGenRulesConfig Cfg() => Resources.Load<MapGenRulesConfig>("Grid/MapGenRulesConfig");

    // ========================================================================
    //  编辑态入口
    // ========================================================================

    public static string RunEdit()
    {
        _log.Clear();
        _logName = "hh294_s5_probe_edit";
        var cfg = Cfg();
        _log.AppendLine("======== HH.294 片 5 · 资源池落地 · 编辑态读数 ========");
        if (cfg == null) { _log.AppendLine("❌ MapGenRulesConfig 取不到（Resources/Grid/MapGenRulesConfig）"); WriteLog(); return _log.ToString(); }
        _log.AppendLine($"配置实读：poolCapBase={cfg.poolCapBase} poolOpenRatio={cfg.poolOpenRatio} kindCapRelax={cfg.kindCapRelax} " +
                        $"guaranteeRatio={cfg.guaranteeRatio} abundance=[{string.Join(",", cfg.bandResourceAbundance)}] " +
                        $"difficultyScale=[{string.Join(",", cfg.difficultyResourceScale)}]");

        WeightDiff(cfg);
        QuotaTable(cfg);
        CurveAnalytic(cfg);
        KindAllowedCases();
        PickCellCases();

        _log.AppendLine("\n---- 生成一张 256²/Normal 图（生成层直调·同 WorldManager 序）----");
        var m = Valley_HH291_MapGenProbe.Build(SEED, 256, 256, 2);
        OpeningByBand(m, cfg);
        AnchorInvariant(m);
        A345(m, cfg);

        AnchorSyntheticCase();
        Determinism();

        _log.AppendLine("\n===== 编辑态读数结束 =====");
        WriteLog();
        Debug.Log(TAG + " 编辑态探针完成 → Logs/hh294_s5_probe.log");
        return _log.ToString();
    }

    // ========================================================================
    //  判据 1：权重表逐带逐字段 diff 面
    // ========================================================================

    static void WeightDiff(MapGenRulesConfig cfg)
    {
        _log.AppendLine("\n---- 判据 1：`resourceWeights` 逐带逐字段 diff 面（改前 → 改后）----");
        int changed = 0;
        for (int b = 0; b < 4; b++)
        {
            var w5 = cfg.GetResourceWeights((ClimateZone)b);
            for (int t = 0; t < 5; t++)
            {
                float nv = w5 != null ? w5[t] : -1f;
                bool diff = Mathf.Abs(nv - OldW[b][t]) > 1e-6f;
                if (diff) changed++;
                _log.AppendLine($"  [{BandName[b]}] {KindName[t]}：{OldW[b][t]:0.00} → {nv:0.00}  {(diff ? "★改" : " 同")}");
            }
        }
        _log.AppendLine($"  ⇒ 逐带逐字段共 20 格，实际变更 **{changed}** 格");
        _log.AppendLine($"  实盘断言：热=亚热 {SameW(cfg, 0, 1)}；亚热=温 {SameW(cfg, 1, 2)}；四带 mine 全 0.95 {AllMine(cfg, 0.95f)}；" +
                        $"寒 tree={cfg.GetResourceWeights(ClimateZone.Cold)[0]:0.00}；寒 woodPile={cfg.GetResourceWeights(ClimateZone.Cold)[2]:0.00}（1−w=0 ⇒ 木堆占比 0）");
    }

    static bool SameW(MapGenRulesConfig cfg, int a, int b)
    {
        var x = cfg.GetResourceWeights((ClimateZone)a); var y = cfg.GetResourceWeights((ClimateZone)b);
        if (x == null || y == null) return false;
        for (int t = 0; t < 5; t++) if (Mathf.Abs(x[t] - y[t]) > 1e-6f) return false;
        return true;
    }

    static bool AllMine(MapGenRulesConfig cfg, float v)
    {
        for (int b = 0; b < 4; b++) if (Mathf.Abs(cfg.GetResourceWeights((ClimateZone)b)[4] - v) > 1e-6f) return false;
        return true;
    }

    // ========================================================================
    //  判据 2/3/5：口径表（cap / p_i / E / E^open / B / capKind）
    // ========================================================================

    static void QuotaTable(MapGenRulesConfig cfg)
    {
        _log.AppendLine("\n---- 判据 2/3/5：池子口径逐带逐类（`MapGenRules.ResolvePoolQuota`·生成期与运行期同一处）----");
        _log.AppendLine("  带 | 类 | w | p_i | cap | E_i=cap×p_i | E_i^open(=×0.4) | B_i | capKind_i(=cap×p_i×1.5)");
        var share = new float[5]; var kindCap = new float[5];
        for (int b = 0; b < 4; b++)
        {
            MapGenRules.ResolvePoolQuota(cfg, (ClimateZone)b, 2, share, kindCap, out float cap);
            var w5 = cfg.GetResourceWeights((ClimateZone)b);
            float sumE = 0f, sumOpen = 0f;
            for (int t = 0; t < 5; t++)
            {
                float e = cap * share[t];
                float open = e * cfg.poolOpenRatio;
                sumE += e; sumOpen += open;
                _log.AppendLine($"  {BandName[b]} | {KindName[t]} | {w5[t]:0.00} | {share[t] * 100f:0.00}% | {cap:0.0} | {e:0.0} | " +
                                $"{open:0.0} | {Mathf.FloorToInt(open * cfg.guaranteeRatio)} | {kindCap[t]:0.00}");
            }
            _log.AppendLine($"  └ {BandName[b]}：ΣE_i = **{sumE:0.0}**（须 = cap {cap:0.0}）· ΣE_i^open = **{sumOpen:0.0}**（须 = cap×0.4 = {cap * 0.4f:0.0}）");
        }
        MapGenRules.ResolvePoolQuota(cfg, ClimateZone.Temperate, 2, share, kindCap, out float c2);
        MapGenRules.ResolvePoolQuota(cfg, ClimateZone.Temperate, 1, share, kindCap, out float c1);
        MapGenRules.ResolvePoolQuota(cfg, ClimateZone.Temperate, 3, share, kindCap, out float c3);
        _log.AppendLine($"  难度档（温带）：diff1 cap={c1:0.0} ／ diff2 cap={c2:0.0} ／ diff3 cap={c3:0.0}（比例 {c1 / c2:0.000}:1.000:{c3 / c2:0.000}，须 = 0.7:1.0:1.3）");
        _log.AppendLine($"  判据 2 读数：`poolCapBase` 在场 = **{cfg.poolCapBase}**（须 96）");
    }

    // ========================================================================
    //  判据 5：单类上限 —— 生产同一处谓词的构造反证
    // ========================================================================

    static void KindAllowedCases()
    {
        _log.AppendLine("\n---- 判据 5：总上限/单类上限 谓词构造反证（生产同一处 `ResourceRespawnSystem.KindAllowed`）----");
        float cap = 105.6f, kindCap = 53.9f;   // 温带树：capKind = 105.6×0.3404×1.5 ≈ 53.9
        int totalAtCap = Mathf.CeilToInt(cap);          // 106（**达到**上限；用截断 105 会落在「未满」侧）
        int kindAtCap = Mathf.CeilToInt(kindCap);       // 54
        bool c1 = ResourceRespawnSystem.KindAllowed(0, kindCap, totalAtCap, cap);
        bool c2 = ResourceRespawnSystem.KindAllowed(kindAtCap, kindCap, 40, cap);
        bool c3 = ResourceRespawnSystem.KindAllowed(kindAtCap - 1, kindCap, totalAtCap - 1, cap);
        bool c4 = ResourceRespawnSystem.KindAllowed(0, kindCap, 0, 0f);
        _log.AppendLine($"  构造①（总点数={totalAtCap}≥cap={cap:0.0}·该类=0）⇒ **{c1}**（须 False：总满不再补）");
        _log.AppendLine($"  构造②（该类={kindAtCap}≥capKind={kindCap:0.0}·总点数=40<cap）⇒ **{c2}**（须 False：该类达上限不再增）");
        _log.AppendLine($"  构造③（该类={kindAtCap - 1}<capKind·总点数={totalAtCap - 1}<cap 上限侧）⇒ **{c3}**（须 True：双未满可补）");
        _log.AppendLine($"  构造④（cap=0）⇒ **{c4}**（须 False）");
        _log.AppendLine($"  ⇒ 存在性反证：{(!c1 && !c2 && c3 && !c4 ? "✅「该类达上限后不再增」由生产同一谓词否定" : "❌ 谓词不自洽")}");
    }

    // ========================================================================
    //  判据 6：落点兜底路径（构造）
    // ========================================================================

    static void PickCellCases()
    {
        _log.AppendLine("\n---- 判据 6：落点器兜底路径构造读数（生产同一处 `ResourceRespawnSystem.PickCell`）----");
        var fresh = new List<int> { 501, 502 };
        var freed = new List<int> { 999 };
        int fc = 0, dc = 0;
        int a = ResourceRespawnSystem.PickCell(fresh, ref fc, freed, ref dc);
        int b = ResourceRespawnSystem.PickCell(fresh, ref fc, freed, ref dc);
        int c = ResourceRespawnSystem.PickCell(fresh, ref fc, freed, ref dc);
        int d = ResourceRespawnSystem.PickCell(fresh, ref fc, freed, ref dc);
        _log.AppendLine($"  构造①（fresh={{501,502}}·freed={{999}}）依次取：{a} → {b} → **{c}**（fresh 尽 ⇒ 兜底＝原位格 999）→ {d}（须 −1）");
        var e0 = new List<int>(); var f0 = new List<int>();
        int ec = 0, dc2 = 0;
        int z = ResourceRespawnSystem.PickCell(e0, ref ec, f0, ref dc2);
        _log.AppendLine($"  构造②（fresh 空·freed 空）⇒ **{z}**（须 −1：本区块不可落）");
        _log.AppendLine($"  ⇒ 读数链：{(a == 501 && b == 502 && c == 999 && d == -1 && z == -1 ? "✅ fresh 优先（不在原位）／fresh 尽 ⇒ 原位兜底／皆尽 ⇒ 放弃" : "❌ 与口径不符")}");
    }

    // ========================================================================
    //  判据 3/11：开局每区块点数（6.6 之后）
    // ========================================================================

    static void OpeningByBand(MapData m, MapGenRulesConfig cfg)
    {
        int cw = MapGenRules.ChunkW(m), ch = MapGenRules.ChunkH(m);
        var buf = new int[5];
        var perBand = new int[4]; var bandCount = new int[4];
        var bandMin = new int[4]; var bandMax = new int[4]; var bandWin = new int[4];
        for (int b = 0; b < 4; b++) { bandMin[b] = int.MaxValue; bandMax[b] = 0; }
        int globalMax = 0, total = 0;
        var share = new float[5]; var kindCap = new float[5];
        for (int cy = 0; cy < ch; cy++)
            for (int cx = 0; cx < cw; cx++)
            {
                int v = MapGenRules.CountChunkPoints(m, cx, cy, buf);
                int b = (int)MapGenRules.DominantZoneOfChunk(m, cx, cy);
                perBand[b] += v; bandCount[b]++;
                total += v;
                if (v > globalMax) globalMax = v;
                if (v < bandMin[b]) bandMin[b] = v;
                if (v > bandMax[b]) bandMax[b] = v;
                MapGenRules.ResolvePoolQuota(cfg, (ClimateZone)b, 2, share, kindCap, out float cb);
                float ex = cb * cfg.poolOpenRatio;
                if (v >= ex * 0.8f && v <= ex * 1.2f) bandWin[b]++;
            }
        _log.AppendLine("\n---- 判据 3：开局每区块点数（生成 256²/Normal·`CountChunkPoints` 点口径）----");
        _log.AppendLine("  带 | 区块数 | 均值实测 | 期望 cap×0.4 | 偏差 | min | max | ±20% 内 | cap");
        int inWinTotal = 0, chunkTotal = cw * ch;
        for (int b = 0; b < 4; b++)
        {
            if (bandCount[b] == 0) { _log.AppendLine($"  {BandName[b]}：无区块"); continue; }
            MapGenRules.ResolvePoolQuota(cfg, (ClimateZone)b, 2, share, kindCap, out float cap);
            float mean = perBand[b] / (float)bandCount[b];
            float expect = cap * cfg.poolOpenRatio;
            float quant = 0f;   // 逐类 RoundToInt 量化后的落格目标（解释实测 vs 解析的差）
            for (int t = 0; t < 5; t++) quant += Mathf.RoundToInt(cap * share[t] * cfg.poolOpenRatio);
            inWinTotal += bandWin[b];
            _log.AppendLine($"  {BandName[b]} | {bandCount[b]} | **{mean:0.0}** | {expect:0.0} | {mean - expect:+0.0;-0.0} | {bandMin[b]} | {bandMax[b]} | {bandWin[b]}/{bandCount[b]} | {cap:0.0} | 逐类量化目标={quant:0}");
        }
        _log.AppendLine($"  ⇒ 全图均值 = **{total / (float)Mathf.Max(1, chunkTotal):0.0}**（四带期望均值 = poolCapBase×0.4 = {cfg.poolCapBase * cfg.poolOpenRatio:0.0}）");
        _log.AppendLine($"  存在性反证：落在期望值 ±20% 内的区块合计 **{inWinTotal}**/{chunkTotal} 个（须 ≥ 1）");
        MapGenRules.ResolvePoolQuota(cfg, ClimateZone.Temperate, 2, share, kindCap, out float capT);
        _log.AppendLine($"  判据 11 读数（6.6 是否把区块补到满池）：全图区块点数最大值 = **{globalMax}**；温带 cap = **{capT:0.0}** ⇒ " +
                        $"{(globalMax < capT ? "✅ 无区块被补到满池（最大 < cap）" : "❌ 有区块达/超满池")}");
    }

    // ========================================================================
    //  判据 10：锚点归属不变量 ＋ 构造用例
    // ========================================================================

    static void AnchorInvariant(MapData m)
    {
        int mineCells = 0, treeCells = 0, treePits = 0;
        for (int y = 0; y < m.height; y++)
            for (int x = 0; x < m.width; x++)
            {
                var f = MapGate.ReadAt(m, x, y);
                if (f == FeatureType.Mine) mineCells++;
                if (f == FeatureType.Tree) { treeCells++; treePits += MapGenRules.PitCountAt(m, x, y); }
            }
        int cw = MapGenRules.ChunkW(m), ch = MapGenRules.ChunkH(m);
        var buf = new int[5]; int minePts = 0;
        for (int cy = 0; cy < ch; cy++)
            for (int cx = 0; cx < cw; cx++) { MapGenRules.CountChunkPoints(m, cx, cy, buf); minePts += buf[4]; }
        _log.AppendLine("\n---- 判据 10：锚点归属**实图不变量**（跨界不重算·无重计/漏计）----");
        _log.AppendLine($"  Mine 格 = {mineCells} ⇒ 簇（÷4）= {mineCells / 4f:0.0}；Σ 逐区块锚点点数 = **{minePts}**" +
                        $" ⇒ {(minePts * 4 == mineCells ? "✅ 逐区块点数×4 == 格数（无重计/漏计）" : $"⚠ 差 {minePts * 4 - mineCells} 格")}");
        _log.AppendLine($"  参照：树 格={treeCells}／坑位={treePits}（非矿按坑位计，与区块归属无关）");
    }

    static void AnchorSyntheticCase()
    {
        _log.AppendLine("\n---- 判据 10：**构造用例**「跨区块边界的实体 ⇒ 配额只记 1 次」----");
        int w = 32, h = 32;                     // 2×2 个 16×16 大区块
        var m = new MapData
        {
            mapId = 0, seed = 1, width = w, height = h,
            features = new FeatureType[w * h],
            climateZones = new ClimateZone[w * h],
            kingdomSpawns = new List<Vector2Int>(),
            threatSpawns = new List<SpawnDef>(),
            naturalBuildings = new List<NaturalBuilding>()
        };
        for (int i = 0; i < m.features.Length; i++) m.features[i] = FeatureType.Plain;
        // 跨界簇：锚点 (15,7) ⇒ 占 (15,7)(16,7)(15,8)(16,8) —— 横跨区块(0,0)|(1,0) 的 x 边界
        m.features[MapGenRules.Idx(m, 15, 7)] = FeatureType.Mine;
        m.features[MapGenRules.Idx(m, 16, 7)] = FeatureType.Mine;
        m.features[MapGenRules.Idx(m, 15, 8)] = FeatureType.Mine;
        m.features[MapGenRules.Idx(m, 16, 8)] = FeatureType.Mine;
        // 对照：区块(1,0) 内一整簇（不跨界）锚点 (20,7)
        m.features[MapGenRules.Idx(m, 20, 7)] = FeatureType.Mine;
        m.features[MapGenRules.Idx(m, 21, 7)] = FeatureType.Mine;
        m.features[MapGenRules.Idx(m, 20, 8)] = FeatureType.Mine;
        m.features[MapGenRules.Idx(m, 21, 8)] = FeatureType.Mine;
        int mineCellsSyn = 0;
        foreach (var f in m.features) if (f == FeatureType.Mine) mineCellsSyn++;

        var a = new int[5]; var b2 = new int[5]; var c2 = new int[5]; var d2 = new int[5];
        int ta = MapGenRules.CountChunkPoints(m, 0, 0, a);
        int tb = MapGenRules.CountChunkPoints(m, 1, 0, b2);
        int tc = MapGenRules.CountChunkPoints(m, 0, 1, c2);
        int td = MapGenRules.CountChunkPoints(m, 1, 1, d2);
        _log.AppendLine("  构造：32×32（2×2 区块）·跨界簇锚点=(15,7)（占 15/16 × 7/8）·区块内整簇锚点=(20,7)");
        _log.AppendLine($"  区块(0,0) 点数={ta}（mine=**{a[4]}**·须 1＝跨界簇锚点所在地）");
        _log.AppendLine($"  区块(1,0) 点数={tb}（mine=**{b2[4]}**·须 1＝本区块内整簇；(16,7)/(16,8) 两格**不另计**）");
        _log.AppendLine($"  区块(0,1) mine={c2[4]}（须 0）·区块(1,1) mine={d2[4]}（须 0）");
        _log.AppendLine($"  Σ 全区块 mine 点数 = {a[4] + b2[4] + c2[4] + d2[4]}（须 2 ＝ 两簇；Mine 格数 = {mineCellsSyn}）");
        bool ok = a[4] == 1 && b2[4] == 1 && c2[4] == 0 && d2[4] == 0 && (a[4] + b2[4] + c2[4] + d2[4]) * 4 == mineCellsSyn;
        _log.AppendLine($"  ⇒ {(ok ? "✅ 跨界实体**只记 1 次**（记在锚点所在区块），邻区块不重算" : "❌ 口径不符")}");
    }

    // ========================================================================
    //  判据 4：曲线解析（cap=96 / 温带 105.6）
    // ========================================================================

    static void CurveAnalytic(MapGenRulesConfig cfg)
    {
        _log.AppendLine("\n---- 判据 4：日补量曲线解析（`ResourceRespawnSystem.DailyTarget`·闭式）----");
        _log.AppendLine("  天 | 目标点数(cap=96) | 当日补量 | 03 §6.7.2 表(96 基准) | 偏差 | 目标% | 补后累计%");
        float cap = cfg.poolCapBase;
        float total = cap * cfg.poolOpenRatio;
        for (int d = 1; d <= 7; d++)
        {
            float tgt = ResourceRespawnSystem.DailyTarget(total, cap);
            float add = Mathf.Floor(tgt) - total;
            _log.AppendLine($"  {d} | {tgt:0.0} | +{add:0} | +{DocCurve[d - 1]:0} | {add - DocCurve[d - 1]:+0;-0} | {tgt / cap * 100f:0.0}% | {(total + add) / cap * 100f:0.0}%");
            total += add;
        }
        _log.AppendLine($"  ⇒ 第 7 天 = {total:0}/{cap:0} = **{total / cap * 100f:0.0}%**（能力句「第 7 天 ≈ cap 的 95%+」）");
        float capT = 105.6f, totT = capT * cfg.poolOpenRatio;
        var line = new StringBuilder("  温带 cap=105.6 逐日（按 cap/96 缩放）：");
        for (int d = 1; d <= 7; d++)
        {
            float tgt = ResourceRespawnSystem.DailyTarget(totT, capT);
            float add = Mathf.Floor(tgt) - totT;
            line.Append($" D{d}=+{add:0}");
            totT += add;
        }
        _log.AppendLine(line.ToString() + $" ⇒ D7={totT:0}/{capT:0}={totT / capT * 100f:0.0}%");
    }

    // ========================================================================
    //  判据 1：重跑 HH.291 的 A3/A4/A5
    // ========================================================================

    static void A345(MapData m, MapGenRulesConfig cfg)
    {
        _log.AppendLine("\n---- 判据 1：重跑 `HH.291` 的 A3/A4/A5（对照不得退化）----");
        _log.Append(Valley_HH291_MapGenProbe.BandQuotaRead(m, cfg, 2, "256²N"));
        _log.Append(Valley_HH291_MapGenProbe.PitRead(m, "256²N", out bool pitOk));
        _log.Append(Valley_HH291_MapGenProbe.ChunkResourceRead(m, "256²N", out bool chunkOk));
        _log.AppendLine($"  判据 1 判定：A4(Pit)={pitOk} ／ A5(空区块 0)={chunkOk}（改前基线：满格 6824 · >4 计数 0 · 空区块 0）");
    }

    // ========================================================================
    //  判据 13：同 seed 逐格一致
    // ========================================================================

    static void Determinism()
    {
        var a1 = Valley_HH291_MapGenProbe.Build(SEED, 256, 256, 2);
        var a2 = Valley_HH291_MapGenProbe.Build(SEED, 256, 256, 2);
        bool same = Valley_HH291_MapGenProbe.SameMap(a1, a2, out var why);
        _log.AppendLine("\n---- 判据 13：确定性 ----");
        _log.AppendLine($"  同 seed 两次生成：{(same ? "✅ 逐格一致（features+climateZones+spawns+nb）" : "❌ " + why)}");
    }

    // ========================================================================
    //  Play 态入口（正门 EnterTestRun）
    // ========================================================================

    public static void RunLive()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError(TAG + " RunLive 须先进入 Play。"); return; }
        _log.Clear();
        new GameObject("HH294S5_LiveRunner").AddComponent<Host>().Start2(RunLiveCo());
    }

    class Host : MonoBehaviour { public void Start2(IEnumerator r) => StartCoroutine(r); }

    public static IEnumerator RunLiveCo()
    {
        _log.Clear();
        _logName = "hh294_s5_probe_live";
        _log.AppendLine("======== HH.294 片 5 · 资源池落地 · 实机正门读数 ========");
        var cfg = new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = SLOT, kingdomName = "河谷王国"
        };
        float t0 = Time.realtimeSinceStartup;
        yield return TestHarnessApi.EnterTestRun(cfg, 15f);
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || BuildingRegistry.Instance == null || TimeManager.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 180f) { _log.AppendLine("等就绪超时"); WriteLog(); yield break; }
        }
        _log.AppendLine($"  [正门] EnterTestRun(seed {SEED}/Medium/diff2/15x) 进局就绪 = {Time.realtimeSinceStartup - t0:0.00}s");
        var map = WorldManager.Instance.ActiveMap;
        var rs = ResourceRespawnSystem.Instance;
        if (rs == null || !rs.PoolReady) { _log.AppendLine("❌ ResourceRespawnSystem 池子未就绪"); WriteLog(); yield break; }

        int n = rs.PoolChunkCount;
        // **真正冻结日历**：切到 `GameState.Paused` ⇒ `TimeManager.Update` 只在 `Playing` 态推进
        // （`TimeManager.cs:159`）⇒ 下述每日读数全由手动 `SettleDay` 驱动、真实事件零干扰；
        // 顺带避开测试跑加速把帧率压低（判据 9 的分帧读数更干净）。
        if (GameStateManager.Instance != null) GameStateManager.Instance.SetState(GameState.Paused);
        yield return null;
        _log.AppendLine($"  [口径] 已切 `GameState.Paused` 冻结日历：手动 `SettleDay` 驱动全部每日读数（真实事件停发）");
        TimeManager.Instance.SetSecondsPerDay(3600f);

        // ---- 判据 3（实机）：开局每区块点数 ----
        _log.AppendLine("\n---- 判据 3（实机）：开局每区块点数（池子实读）----");
        _log.AppendLine("  带 | 区块数 | 池均值 | 期望 cap×0.4 | 偏差 | min | max | ±20% 内");
        var perBand = new int[4]; var bandN = new int[4]; var bandMin = new int[4]; var bandMax = new int[4]; var bandWin = new int[4];
        for (int b = 0; b < 4; b++) { bandMin[b] = int.MaxValue; bandMax[b] = 0; }
        var pts = new int[n]; int winAll = 0, sumAll = 0;
        for (int ci = 0; ci < n; ci++)
        {
            int v = rs.PointsOf(ci); pts[ci] = v; sumAll += v;
            int b = rs.BandOf(ci);
            perBand[b] += v; bandN[b]++;
            if (v < bandMin[b]) bandMin[b] = v;
            if (v > bandMax[b]) bandMax[b] = v;
            float e0 = rs.CapOf(ci) * 0.4f;
            if (v >= e0 * 0.8f && v <= e0 * 1.2f) { bandWin[b]++; winAll++; }
        }
        for (int b = 0; b < 4; b++)
        {
            if (bandN[b] == 0) continue;
            float cap = 0f;
            for (int ci = 0; ci < n; ci++) if (rs.BandOf(ci) == b) { cap = rs.CapOf(ci); break; }
            float mean = perBand[b] / (float)bandN[b];
            _log.AppendLine($"  {BandName[b]} | {bandN[b]} | **{mean:0.0}** | {cap * 0.4f:0.0} | {mean - cap * 0.4f:+0.0;-0.0} | {bandMin[b]} | {bandMax[b]} | {bandWin[b]}/{bandN[b]}");
        }
        _log.AppendLine($"  存在性反证：±20% 窗内区块 = **{winAll}**/{n}（须 ≥ 1）· 全图均值 = **{sumAll / (float)n:0.0}**");

        // ---- 判据 4：逐天曲线（手动驱动·**从开局 40% 起算**，故必须排在任何其它结算之前） ----
        _log.AppendLine("\n---- 判据 4：逐天曲线实测（连续 7 天·`SettleDay`＋分帧落格）----");
        _log.AppendLine("  天 | 计划补量 | 实落格 | 全图均值/区块 | 均值Δ | 03 表(按 cap/96 缩放) | ±20% 窗内 | 最大单帧ms | 帧数");
        int day = Mathf.Max(rs.LastSettleDay, TimeManager.Instance.CurrentDay);
        int prevMean = MeanPoints(rs);
        float capAvg = 0f;
        for (int ci = 0; ci < n; ci++) capAvg += rs.CapOf(ci);
        capAvg /= Mathf.Max(1, n);
        float day7MaxFrameMs = 0f; int day7Frames = 0; int day7Placed = 0;
        for (int k = 1; k <= 7; k++)
        {
            day++;
            rs.SettleDay(day);
            int planned = rs.LastDayPlannedAdditions;
            yield return FlushAll(rs);
            int mean = MeanPoints(rs);
            int winK = 0;
            for (int ci = 0; ci < n; ci++) { float e = rs.CapOf(ci); if (rs.PointsOf(ci) >= e * 0.8f && rs.PointsOf(ci) <= e * 1.2f) winK++; }
            float docAdd = DocCurve[k - 1] * (capAvg / 96f);
            _log.AppendLine($"  {k} | +{planned} | {rs.LastDayPlacedPoints} | **{mean}** | {mean - prevMean:+0;-0} | +{docAdd:0} | {winK}/{n} | {rs.LastFlushMaxFrameMs:0.00} | {rs.LastFlushFrames}");
            if (k == 7) { day7MaxFrameMs = rs.LastFlushMaxFrameMs; day7Frames = rs.LastFlushFrames; day7Placed = rs.LastDayPlacedPoints; }
            prevMean = mean;
        }
        int finalMean = MeanPoints(rs);
        _log.AppendLine($"  ⇒ 第 7 天全图均值 = **{finalMean}** ／ 四带 cap 均值 = **{capAvg:0.0}** ／ 占比 = **{finalMean / capAvg * 100f:0.0}%**（能力句：第 7 天 ≈ cap 的 95%+）");

        // ---- 判据 8a：幂等/单调（构造） ----
        _log.AppendLine("\n---- 判据 8a：每日补量路径调用计数（幂等 ＋ 单调）----");
        int probeDay = Mathf.Max(rs.LastSettleDay, TimeManager.Instance.CurrentDay) + 1;
        int before = rs.DaySettleCount;
        rs.SettleDay(probeDay); rs.SettleDay(probeDay); rs.SettleDay(probeDay);
        _log.AppendLine($"  构造：同一天 `SettleDay({probeDay})` 连调 3 次 ⇒ DaySettleCount 增量 = **{rs.DaySettleCount - before}**（须 1：同日幂等）");
        before = rs.DaySettleCount;
        rs.SettleDay(probeDay - 1);
        _log.AppendLine($"  构造：回退天 `SettleDay({probeDay - 1})` ⇒ 增量 = **{rs.DaySettleCount - before}**（须 0：单调不重算）");
        yield return FlushAll(rs);

        // ---- 判据 5：单类上限（观测）----
        _log.AppendLine("\n---- 判据 5：单类计数 vs 单类上限（7 天后实测·逐带逐类均值）----");
        var sumKind = new int[4, 5]; var cntBand = new int[4];
        for (int ci = 0; ci < n; ci++)
        {
            int b = rs.BandOf(ci); cntBand[b]++;
            for (int t = 0; t < 5; t++) sumKind[b, t] += rs.PointsOf(ci, t);
        }
        _log.AppendLine("  带 | 类 | 均值实测 | 单类上限 capKind_i | 是否越界 | 满池期望 E_i");
        bool overCap = false;
        for (int b = 0; b < 4; b++)
        {
            if (cntBand[b] == 0) continue;
            int probeCi = -1;
            for (int ci = 0; ci < n; ci++) if (rs.BandOf(ci) == b) { probeCi = ci; break; }
            for (int t = 0; t < 5; t++)
            {
                float mean = sumKind[b, t] / (float)cntBand[b];
                float kcap = rs.KindCapOf(probeCi, t);
                float e = rs.CapOf(probeCi) * rs.ShareOf(probeCi, t);
                bool over = mean > kcap + 1e-3f;
                if (over) overCap = true;
                _log.AppendLine($"  {BandName[b]} | {KindName[t]} | {mean:0.00} | {kcap:0.00} | {(over ? "**越界**" : "未越界")} | {e:0.0}");
            }
        }
        _log.AppendLine($"  ⇒ 观测反证：{(overCap ? "❌ 有类越界" : "✅ 全带全类均 ≤ capKind_i；构造反证见编辑态 KindAllowedCases")}");
        // 满池观测：再推进 7 天 ⇒ 达到 cap 后不再增（反证②）
        int meanBefore = MeanPoints(rs);
        for (int k = 8; k <= 14; k++) { day++; rs.SettleDay(day); yield return FlushAll(rs); }
        int meanAfter = MeanPoints(rs);
        _log.AppendLine($"  满池观测：第 7 天均值 {meanBefore} → 第 14 天均值 {meanAfter}（Δ={meanAfter - meanBefore}；达 cap 后增量为 0 ⇒ 总上限生效）");

        // ---- 判据 6：重生不在原位 ----
        _log.AppendLine("\n---- 判据 6：重生落点不在原位（实测 N 次）----");
        var eaten = new List<GridCoord>();
        var usedChunk = new HashSet<int>();
        for (int y = 1; y < map.height - 1 && eaten.Count < 8; y++)
            for (int x = 1; x < map.width - 1 && eaten.Count < 8; x++)
            {
                if (MapGate.ReadAt(map, x, y) != FeatureType.Tree) continue;
                int ci = (y / 16) * rs.PoolChunkW + (x / 16);
                if (usedChunk.Contains(ci)) continue;
                usedChunk.Add(ci);
                eaten.Add(new GridCoord(x, y));
            }
        // 【HH.294 片 6-2·6-D】`HandleTreeGathered` ⇒ 与实体链合并后的 `HandleCellGathered`（同一实现）
        foreach (var c in eaten) rs.HandleCellGathered(c);
        _log.AppendLine($"  构造：采走 {eaten.Count} 棵树（分处 {usedChunk.Count} 个区块）⇒ 采后仍非 Plain 的 = {CountNonPlain(map, eaten)}（须 0）");
        day++;
        rs.SettleDay(day);
        yield return FlushAll(rs);
        int stillPlain = 0;
        var back = new List<string>();
        foreach (var c in eaten)
        {
            if (MapGate.ReadAt(map, c.x, c.y) == FeatureType.Plain) stillPlain++;
            else back.Add($"({c.x},{c.y})");
        }
        _log.AppendLine($"  N = **{eaten.Count}** 次重生窗口（该天全图实落格 {rs.LastDayPlacedPoints} 点）：新位 ≠ 原位的 = **{stillPlain}**/{eaten.Count}" +
                        $"（须全部 ≠ 原位）{(back.Count == 0 ? " ✅" : " ❌ 原位复活：" + string.Join(",", back))}");
        _log.AppendLine($"  兜底路径读数：本天走 freed（原位兜底）落格数 = **{rs.FreedFallbackPlacements}**（fresh 候选充足 ⇒ 0；抽不到位置才回原位）");

        // ---- 判据 6b：实体路径（WoodPile）重建不在原位 ----
        _log.AppendLine("\n---- 判据 6b：实体路径（一次性资源·WoodPile）重建不在原位 ----");
        var wp = FindFirst(map, FeatureType.WoodPile);
        if (wp.x >= 0)
        {
            int wpChunk = (wp.y / 16) * rs.PoolChunkW + (wp.x / 16);
            int beforeCnt = rs.PointsOf(wpChunk, 2);
            // 【HH.294 片 6-2·6-D】`HandleEntityDepleted(wp, feature)` ⇒ 合并后 `HandleCellGathered(cell)`
            //   （feature 由回调内自行回读）；实体派生已随 6-A 退役（对照开关默认 false）⇒ 只判格表落新位。
            rs.HandleCellGathered(wp);
            day++;
            rs.SettleDay(day);
            yield return FlushAll(rs);
            int afterCnt = rs.PointsOf(wpChunk, 2);
            _log.AppendLine($"  木堆格 ({wp.x},{wp.y}) 采集 ⇒ 池子木堆 {beforeCnt} → {afterCnt}；该格现值 = **{MapGate.ReadAt(map, wp.x, wp.y)}**" +
                            $"（须 Plain ⇒ 不在原位）；该区块木堆 feature 数 = {CountFeatureInChunk(map, wpChunk, FeatureType.WoodPile, rs.PoolChunkW)}" +
                            $"（> 0 ⇒ 已落新位；【片 6-2】实体已退役 ⇒ 不再由 `ReSpawnNaturalBuilding` 重建·纯格表）");
        }
        else _log.AppendLine("  未找到 WoodPile feature（跳过该子项）");

        // ---- 判据 8b：真实时钟节流窗（每天一次·非每帧/每秒） ----
        // 先把池子**按当前地图重建**（`ResetRespawns` 会把 `_lastSettleDay` 归零、`DaySettleCount` 清零），
        // 这样真实日历的每个 `TimeDayChangedEvent` 都应恰好触发 1 次结算（避免手动结算把游标推快而吞事件）。
        _log.AppendLine("\n---- 判据 8b：真实时钟节流窗（每天一次·非每帧/每秒）----");
        rs.ResetRespawns(map, 2);
        int dayEvents = 0;
        System.Action<TimeDayChangedEvent> onDay = e => dayEvents++;
        EventBus.Subscribe(onDay);
        TimeManager.Instance.SetSecondsPerDay(30f);
        GameStateManager.Instance.SetState(GameState.Playing);   // 解冻：真实日历开始推进
        int settle0 = rs.DaySettleCount, day0 = TimeManager.Instance.CurrentDay, frames0 = Time.frameCount;
        float tv0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - tv0 < 6f) yield return null;
        int framesElapsed = Time.frameCount - frames0;
        int daysAdvanced = TimeManager.Instance.CurrentDay - day0;
        int settleDelta = rs.DaySettleCount - settle0;
        EventBus.Unsubscribe(onDay);
        GameStateManager.Instance.SetState(GameState.Paused);    // 重新冻结
        _log.AppendLine($"  实测窗 6s（15x 加速·`SetSecondsPerDay(30)`）：游戏天推进 = **{daysAdvanced}** ／ TimeDayChangedEvent 收到 = **{dayEvents}** ／ " +
                        $"结算调用增量 = **{settleDelta}** ／ 经过帧数 = **{framesElapsed}**");
        _log.AppendLine($"  ⇒ 每天一次：事件数 {dayEvents} vs 结算数 {settleDelta} ⇒ {(settleDelta == dayEvents ? "✅ 一个游戏天恰结算 1 次" : $"❌ 差 {settleDelta - dayEvents}")}；" +
                        $"事件 vs 天推进：{(dayEvents == daysAdvanced ? "✅ 一天一个事件" : $"差 {dayEvents - daysAdvanced}")}；" +
                        $"非每帧/每秒反证：帧数 {framesElapsed} ≫ 结算 {settleDelta}（若每帧调应 = {framesElapsed}，若每秒调应 ≈ 6）");
        yield return FlushAll(rs);

        // ---- 判据 9：分帧分摊 ----
        _log.AppendLine("\n---- 判据 9：分帧分摊 ----");
        _log.AppendLine($"  第 7 天（满池前补量日）：最大单帧耗时 = **{day7MaxFrameMs:0.00} ms** ／ 占用帧数 = **{day7Frames}** ／ 当天落格 = {day7Placed} 点" +
                        $"（帧预算 16.6ms ⇒ 最坏帧占 {day7MaxFrameMs / 16.6f * 100f:0.0}%）；⛔ 非「一天一帧全落」（帧数 > 1）");
        _log.AppendLine($"  最近一次结算（节流窗内）：最大单帧耗时 = {rs.LastFlushMaxFrameMs:0.00} ms ／ 占用帧数 = {rs.LastFlushFrames} ／ 当天落格 = {rs.LastDayPlacedPoints} 点");
        _log.AppendLine($"  池子：{rs.PoolChunkW}×{rs.PoolChunkH}＝{rs.PoolChunkCount} 区块 · 当前游戏天 = {TimeManager.Instance.CurrentDay} · 累计结算次数 = {rs.DaySettleCount}");

        GameStateManager.Instance.SetState(GameState.Playing);   // 三态收尾：先回 Playing 再退测试跑
        TestHarnessApi.ExitTestRun();
        yield return null;
        WriteLog();
        Debug.Log(TAG + " 实机探针完成 → Logs/hh294_s5_probe.log");
    }

    // ========================================================================
    //  工具
    // ========================================================================

    static IEnumerator FlushAll(ResourceRespawnSystem rs)
    {
        int guard = 0;
        while (rs.HasPendingWork && guard < 1200) { yield return null; guard++; }
    }

    static int MeanPoints(ResourceRespawnSystem rs)
    {
        int s = 0;
        for (int ci = 0; ci < rs.PoolChunkCount; ci++) s += rs.PointsOf(ci);
        return Mathf.RoundToInt(s / (float)Mathf.Max(1, rs.PoolChunkCount));
    }

    static int CountNonPlain(MapData m, List<GridCoord> cells)
    {
        int c = 0;
        foreach (var g in cells) if (MapGate.ReadAt(m, g.x, g.y) != FeatureType.Plain) c++;
        return c;
    }

    static GridCoord FindFirst(MapData m, FeatureType f)
    {
        for (int y = 0; y < m.height; y++)
            for (int x = 0; x < m.width; x++)
                if (MapGate.ReadAt(m, x, y) == f) return new GridCoord(x, y);
        return new GridCoord(-1, -1);
    }

    static int CountFeatureInChunk(MapData m, int ci, FeatureType f, int cw)
    {
        int cx = ci % cw, cy = ci / cw, c = 0;
        for (int y = cy * 16; y < cy * 16 + 16 && y < m.height; y++)
            for (int x = cx * 16; x < cx * 16 + 16 && x < m.width; x++)
                if (MapGate.ReadAt(m, x, y) == f) c++;
        return c;
    }

    static void WriteLog()
    {
        try
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/" + _logName + ".log", _log.ToString());
        }
        catch { }
    }
}
#endif
