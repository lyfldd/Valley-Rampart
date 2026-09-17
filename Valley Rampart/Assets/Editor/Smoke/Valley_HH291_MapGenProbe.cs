#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.291 地图生成口径修复批 · 验收探针（生成层直调 ＋ 实机正门）
//  用法：
//   - 编辑态：Exec `Valley_HH291_MapGenProbe.RunAll()`  ⇒ A2/A3/A4/A5/A6(生成侧)/M1/M2/M6/M7/确定性/耗时
//   - Play 态：Exec `Valley_HH291_MapGenProbe.RunLive()` ⇒ A6 运行期(实例数/ProducerComponent/石入库) + M5 性能
//  证据落盘：`Logs/hh291_probe.log`
//
//  口径声明：
//   - 生成层直调＝按 `WorldManager.GenerateMap` **同序同参**（唯一差异＝AI 模板传 null；HH.291 起无步骤 7）。
//   - 「坑位口径」＝**生成期计数器**（`2_1_R1 §二` 验收线 4）：非矿洞 1 坑位 = 1 资源实例；
//     矿洞例外＝2×2 Cell 粒度（1 簇＝4 单位）。
//   - 「改前」列＝**解析值**（改前权重 `mine w=0.60/0.50/0.35/0.60` ＋ 无丰度、同公式复算），非实测。
// ============================================================================
public static class Valley_HH291_MapGenProbe
{
    const int SEED = 21107;
    const string TAG = "[HH291]";
    const string SLOT = "hh291_live";

    static readonly StringBuilder _log = new StringBuilder();
    static readonly string[] BandName = { "热带", "亚热带", "温带", "寒带" };
    static readonly float[] OldMineW = { 0.60f, 0.50f, 0.35f, 0.60f };
    static readonly float[][] OldOtherW =
    {
        new float[] { 0.20f, 0.45f, 0.45f, 0.50f },
        new float[] { 0.20f, 0.40f, 0.40f, 0.45f },
        new float[] { 0.25f, 0.40f, 0.40f, 0.40f },
        new float[] { 0.60f, 0.45f, 1.00f, 0.55f },
    };

    public static MapGenRulesConfig Cfg() => Resources.Load<MapGenRulesConfig>("Grid/MapGenRulesConfig");

    /// <summary>按 WorldManager 同序同参生成一张图（生成层直调）。</summary>
    public static MapData Build(int seed, int w, int h, int difficulty, bool water = true, bool spawns = true)
    {
        var cfg = Cfg();
        var map = new MapData
        {
            mapId = 0, seed = seed, width = w, height = h,
            features = new FeatureType[w * h],
            climateZones = new ClimateZone[w * h],
            kingdomSpawns = new List<Vector2Int>(),
            threatSpawns = new List<SpawnDef>(),
            naturalBuildings = new List<NaturalBuilding>()
        };
        var rng = new System.Random(seed);
        MapGenRules.FillClimateZones(rng, map, cfg);
        MapGenRules.FillFeatures(rng, map, cfg, difficulty);
        if (spawns) MapGenRules.PlaceKingdomSpawns(rng, map, cfg, WorldSize.Medium, 2, null);
        MapGenRules.ClearKingdomZones(map, cfg);
        MapGenRules.EnsureChunkResourceQuota(rng, map, cfg, difficulty);
        MapValidator.ValidateConnectivity(map);
        if (water)
        {
            MapGenRules.PlaceWater(rng, map, WorldSize.Medium);
            MapValidator.ValidateConnectivity(map);
        }
        MapGenRules.PlaceThreatSpawns(rng, map, cfg, difficulty);
        // 【HH.294 片 6-2 收尾 同步】清场后（实体派生路径与对照开关已删）⇒ `naturalBuildings` **恒空**
        //   （一次性三型转纯数据·不再派生 Building）。本探针的 nb 读数只涉 `Mine`（HH.293 B1 起恒 0）与
        //   `SameMap` 逐项比对（两侧同为空 ⇒ 仍一致）⇒ 判定语义不变。
        MapGenRules.DeriveNaturalBuildings(map);
        return map;
    }

    // ============================== 读数 ==============================

    static float TOfBand(MapGenRulesConfig cfg, int band, int difficulty, bool useOld)
    {
        float T = cfg != null ? cfg.poolCapBase : 96f;   // 【HH.294 片 5】`resourcesPerChunkBase` 已删 ⇒ 改读 `poolCapBase`（cap 基准）
        if (!useOld) T *= cfg != null ? cfg.GetBandAbundance((ClimateZone)band) : 1f;
        int di = Mathf.Clamp(difficulty - 1, 0, 2);
        float scale = cfg != null && cfg.difficultyResourceScale != null && cfg.difficultyResourceScale.Length > di
            ? cfg.difficultyResourceScale[di] : 1f;
        return T * scale;
    }

    static float ShareOf(MapGenRulesConfig cfg, int band, int idx, bool useOld)
    {
        float[] r = new float[5]; float sum = 0f;
        for (int i = 0; i < 5; i++)
        {
            float w;
            if (useOld) w = i == 4 ? OldMineW[band] : OldOtherW[band][i];
            else { var w5 = cfg != null ? cfg.GetResourceWeights((ClimateZone)band) : null; w = w5 != null ? w5[i] : 0.5f; }
            r[i] = 1f - Mathf.Clamp01(w); sum += r[i];
        }
        return sum > 0f ? r[idx] / sum : 0f;
    }

    static float MineShare(MapGenRulesConfig cfg, int band, bool useOld) => ShareOf(cfg, band, 4, useOld);

    static readonly string[] KindName = { "树", "石堆", "木堆", "矿脉", "矿洞" };

    static int UnitsOfChunkCell(MapData m, int x, int y)
    {
        var f = m.features[MapGenRules.Idx(m, x, y)];
        for (int t = 0; t < 5; t++)
            if (f == MapGenRules.ResourceKindFeature[t])
                return t == 4 ? 1 : MapGenRules.PitCountAt(m, x, y);
        return 0;
    }

    /// <summary>A2/A3/M1/M2/M7：四带配额对照表。</summary>
    public static string BandQuotaRead(MapData m, MapGenRulesConfig cfg, int difficulty, string label)
    {
        int cw = MapGenRules.ChunkW(m), ch = MapGenRules.ChunkH(m);
        var chunks = new int[4]; var pits = new int[4, 5]; var cells = new int[4]; var area = new int[4];
        var kindCells = new int[4, 5];   // D736 §四：运行期可采实体数（非矿=格数/矿=Cell 数）
        for (int cy = 0; cy < ch; cy++)
            for (int cx = 0; cx < cw; cx++)
            {
                int b = (int)MapGenRules.DominantZoneOfChunk(m, cx, cy);
                chunks[b]++;
                for (int y = cy * 16; y < cy * 16 + 16 && y < m.height; y++)
                    for (int x = cx * 16; x < cx * 16 + 16 && x < m.width; x++)
                    {
                        area[b]++;
                        var f = m.features[MapGenRules.Idx(m, x, y)];
                        for (int t = 0; t < 5; t++)
                            if (f == MapGenRules.ResourceKindFeature[t])
                            { pits[b, t] += t == 4 ? 1 : MapGenRules.PitCountAt(m, x, y); cells[b]++; kindCells[b, t]++; break; }
                    }
            }
        var sb = new StringBuilder();
        sb.AppendLine($"  [{label}] 四带配额对照表（坑位口径·非矿=坑位/矿=Cell单位）");
        sb.AppendLine("   带 | 区块 | T公式 | T实测 | 树 | 石堆 | 木堆 | 矿脉 | 矿洞u | 合计 | mine实测占比 | mine解析(改后) | mine解析(改前) | 资源格 | 占格率");
        for (int b = 0; b < 4; b++)
        {
            if (chunks[b] == 0) { sb.AppendLine($"   {BandName[b]}：无区块"); continue; }
            int total = 0; for (int t = 0; t < 5; t++) total += pits[b, t];
            float tNew = TOfBand(cfg, b, difficulty, false), tOld = TOfBand(cfg, b, difficulty, true);
            sb.AppendLine($"   {BandName[b]} | {chunks[b]} | {tNew:0} | {total / (float)chunks[b]:0.0} | {pits[b, 0]} | {pits[b, 1]} | {pits[b, 2]} | {pits[b, 3]} | " +
                          $"{pits[b, 4]} | {total} | {(total > 0 ? pits[b, 4] * 100f / total : 0f):0.0}% | {MineShare(cfg, b, false) * 100f:0.00}% | {MineShare(cfg, b, true) * 100f:0.00}% | " +
                          $"{cells[b]} | {(area[b] > 0 ? cells[b] * 100f / area[b] : 0f):0.0}%");
            sb.AppendLine($"        └ 改前 T={tOld:0}（mine w={OldMineW[b]:0.00}·无丰度）→ 改后 T={tNew:0}" +
                          $"（abundance={(cfg != null ? cfg.GetBandAbundance((ClimateZone)b) : 1f):0.0}）· 矿洞={pits[b, 4]}u（≈{pits[b, 4] / 4.0:0.0} 簇）");
        }
        float mean = 0f; for (int b = 0; b < 4; b++) mean += TOfBand(cfg, b, difficulty, false); mean /= 4f;
        sb.AppendLine($"  [{label}] **M7**：T×abundance 四带均值 = **{mean:0.0}**（基准 poolCapBase={cfg.poolCapBase}，难度 {difficulty}）");

        // D736 §四：双列对照——「生成期配额」vs「运行期可采实体数」·逐类对照改前
        //   改前模型（1 格 = 1 资源实例）：配额=可采（1:1）；改后（坑位模型）：可采实体=容纳该类的格数。
        //   ⚠ 禁称「资源总量不变」（D736 §四）——本表即该结论的证据载体。
        sb.AppendLine($"  [{label}] 运行期可采实体数对照（D736 §四 双列口径·非矿=格=实体／矿洞=2×2 簇）");
        sb.AppendLine("   带 | 类别 | 改前·配额 | 改前·可采 | 改后·配额(坑位) | 改后·可采 | 可采倍率");
        for (int b = 0; b < 4; b++)
        {
            if (chunks[b] == 0) continue;
            float tNew = TOfBand(cfg, b, difficulty, false), tOld = TOfBand(cfg, b, difficulty, true);
            for (int t = 0; t < 5; t++)
            {
                float oldQ = tOld * ShareOf(cfg, b, t, true) * chunks[b];          // 改前·格（1 格=1 实例）
                float oldH = t == 4 ? oldQ / 4f : oldQ;                            // 改前·可采（矿按 2×2 簇）
                float newH = t == 4 ? kindCells[b, 4] / 4f : kindCells[b, t];      // 改后·可采（实测）
                sb.AppendLine($"   {BandName[b]} | {KindName[t]} | {oldQ:0.0} | {oldH:0.0} | {pits[b, t]} | " +
                              $"{newH:0.0} | {(oldH > 0f ? newH / oldH : 0f):0.00}×");
            }
        }
        return sb.ToString();
    }

    /// <summary>A4：坑位存在性（能力句）＋上限反证。</summary>
    public static string PitRead(MapData m, string label, out bool ok)
    {
        MapGenRules.ReadPitStats(m, true, out int maxEx, out int fullEx, out int[] histEx, out int totalEx);
        int over = 0, nonMineFull = 0, resCells = 0;
        for (int y = 0; y < m.height; y++)
            for (int x = 0; x < m.width; x++)
            {
                int n = MapGenRules.PitCountAt(m, x, y);
                if (n > MapGenRules.PitsPerCell) over++;
                if (n > 0) resCells++;
                if (n >= MapGenRules.PitsPerCell && m.features[MapGenRules.Idx(m, x, y)] != FeatureType.Mine) nonMineFull++;
            }
        ok = maxEx >= MapGenRules.PitsPerCell && over == 0;
        return $"  [{label}] **A4 能力句**：非矿资源格 max坑位/格 = **{maxEx}**（须=4）· 满格(=4)计数 = **{fullEx}**（须>0）· 全域>4 = {over}（须0）" +
               $" ⇒ {(ok ? "✅" : "❌")}\n           坑位总数(非矿口径) = {totalEx} · 资源格 = {resCells}（非矿满格 = {nonMineFull}）· 直方图 0..4 = " +
               $"{histEx[0]}/{histEx[1]}/{histEx[2]}/{histEx[3]}/{histEx[4]}\n";
    }

    /// <summary>A5：逐区块仍有资源。</summary>
    public static string ChunkResourceRead(MapData m, string label, out bool ok)
    {
        int cw = MapGenRules.ChunkW(m), ch = MapGenRules.ChunkH(m), zero = 0, minV = int.MaxValue, maxV = 0; double sum = 0;
        for (int cy = 0; cy < ch; cy++)
            for (int cx = 0; cx < cw; cx++)
            {
                int v = 0;
                for (int y = cy * 16; y < cy * 16 + 16 && y < m.height; y++)
                    for (int x = cx * 16; x < cx * 16 + 16 && x < m.width; x++) v += UnitsOfChunkCell(m, x, y);
                if (v <= 0) zero++;
                if (v < minV) minV = v; if (v > maxV) maxV = v; sum += v;
            }
        ok = zero == 0;
        return $"  [{label}] **A5** 逐区块资源单位：区块={cw * ch} 空区块={zero}（须0） min={minV} 均={sum / (cw * ch):0.0} max={maxV}" +
               $" ⇒ {(ok ? "✅ 每区块仍有资源" : "❌ 有空区块")}\n";
    }

    /// <summary>A6（生成侧）：Mine 派生（每簇 1 nb·2×2）。
    /// ⚠️ **本段判据已作废（D737 · HH.293 B1 回退 ＋ `HH.294` 片 6-2 收尾清场）**：`Mine` 撤出派生白名单
    ///   且 `naturalBuildings` 现**恒空** ⇒ `nb` 恒 0（`ok` 恒 False 属预期·非缺陷）。
    ///   保留代码仅作**回退见证**（改前 A6 nb=426/425·改后 0）；判读改用 `Valley_HH294_HH293Probe.Run()`。</summary>
    public static string MineDeriveRead(MapData m, string label, out bool ok)
    {
        int nb = 0, bad = 0, other = 0;
        foreach (var b in m.naturalBuildings)
            if (b.feature == FeatureType.Mine) { nb++; if (b.w != MapGenRules.MineClusterSide || b.h != MapGenRules.MineClusterSide) bad++; }
            else other++;
        int mineCells = 0;
        for (int i = 0; i < m.features.Length; i++) if (m.features[i] == FeatureType.Mine) mineCells++;
        ok = nb > 0 && bad == 0 && nb * 4 == mineCells;
        return $"  [{label}] **A6 生成侧**：Mine nb = **{nb}**（w/h≠2×2 者 {bad}）· Mine 格 = {mineCells}（应=nb×4={nb * 4}）· 其余 nb = {other}" +
               $" ⇒ {(ok ? "✅" : "❌")}\n";
    }

    /// <summary>M6：spawn→最近 Mine 簇距离。
    /// 【HH.294 片 6-2 收尾·数据源改道（`D779` 残余 `S1`）】旧读口 `m.naturalBuildings`（现**恒空** ⇒ 读数退化为
    /// 恒「无 Mine」）⇒ 改读**格表**（features）：Mine 簇角 = 左/上邻非 Mine 的 Mine 格（2×2 簇重建·语义与原读口一致）。
    /// ⛔ 只改读口、不改判据存在性（本读数为信息项，不进「判定」行）。</summary>
    public static string SpawnMineDistanceRead(MapData m, string label)
    {
        var anchors = new List<Vector2Int>();
        for (int y = 0; y < m.height; y++)
            for (int x = 0; x < m.width; x++)
            {
                if (m.features[y * m.width + x] != FeatureType.Mine) continue;
                bool left = x > 0 && m.features[y * m.width + x - 1] == FeatureType.Mine;
                bool up = y > 0 && m.features[(y - 1) * m.width + x] == FeatureType.Mine;
                if (!left && !up) anchors.Add(new Vector2Int(x, y));   // 簇左上角（原读口口径＝nb.cellX/cellY）
            }
        var sb = new StringBuilder();
        int r = Cfg() != null ? Cfg().kingdomClearRadius : 4;
        sb.AppendLine($"  [{label}] **M6** spawn→最近 Mine 簇（切比雪夫·格·【片 6-2 收尾】改读格表 features）：Mine 簇={anchors.Count}·净空区半宽={1 + r}");
        for (int i = 0; i < m.kingdomSpawns.Count; i++)
        {
            var sp = m.kingdomSpawns[i]; int best = int.MaxValue;
            foreach (var a in anchors)
            { int d = Mathf.Max(Mathf.Abs(a.x - sp.x), Mathf.Abs(a.y - sp.y)); if (d < best) best = d; }
            sb.AppendLine($"    spawn[{i}]=({sp.x},{sp.y}) → {(best == int.MaxValue ? "无 Mine" : best + " 格")}");
        }
        return sb.ToString();
    }

    public static bool SameMap(MapData a, MapData b, out string why)
    {
        why = "";
        if (a.width != b.width || a.height != b.height) { why = "尺寸不同"; return false; }
        for (int i = 0; i < a.features.Length; i++)
            if (a.features[i] != b.features[i] || a.climateZones[i] != b.climateZones[i])
            { why = $"首差 idx={i} feat {a.features[i]}/{b.features[i]}"; return false; }
        if (a.kingdomSpawns.Count != b.kingdomSpawns.Count) { why = "出生点数不同"; return false; }
        for (int i = 0; i < a.kingdomSpawns.Count; i++) if (a.kingdomSpawns[i] != b.kingdomSpawns[i]) { why = $"spawn[{i}] 不同"; return false; }
        // 【HH.294 片 6-2 收尾】`naturalBuildings` 恒空 ⇒ 本段比对恒等（**不失真**：主域 features/climateZones/spawns
        //   逐格/逐个比对仍是确定性判据主体）。保留段＝契约槽位比对（若未来该字段复活即自动生效）。
        if (a.naturalBuildings.Count != b.naturalBuildings.Count) { why = "nb 数不同"; return false; }
        for (int i = 0; i < a.naturalBuildings.Count; i++)
        {
            var x = a.naturalBuildings[i]; var y = b.naturalBuildings[i];
            if (x.cellX != y.cellX || x.cellY != y.cellY || x.feature != y.feature || x.w != y.w || x.h != y.h) { why = $"nb[{i}] 不同"; return false; }
        }
        return true;
    }

    // ============================== 入口1：编辑态 ==============================

    public static string RunAll()
    {
        _log.Clear();
        var cfg = Cfg();
        _log.AppendLine("======== HH.291 地图生成口径修复批 · 验收读数（生成层直调）========");
        _log.AppendLine($"配置：poolCapBase={cfg.poolCapBase} guaranteeRatio={cfg.guaranteeRatio} 难度系数=[{string.Join(",", cfg.difficultyResourceScale)}] clearR={cfg.kingdomClearRadius}");
        _log.AppendLine($"      A3 bandResourceAbundance=[{string.Join(",", cfg.bandResourceAbundance)}] ｜ A2 mine w 四带=[{cfg.resourceWeights[0].mine},{cfg.resourceWeights[1].mine},{cfg.resourceWeights[2].mine},{cfg.resourceWeights[3].mine}]");

        _log.AppendLine("\n---- [256²/Normal(diff2)] ----");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var mN = Build(SEED, 256, 256, 2);
        sw.Stop();
        _log.AppendLine($"  生成耗时(全管线·编辑器内)={sw.Elapsed.TotalMilliseconds:0.0} ms（HH.272 基线同口径 183.1ms）");
        _log.Append(BandQuotaRead(mN, cfg, 2, "256²N"));
        _log.Append(PitRead(mN, "256²N", out bool pitOk));
        _log.Append(ChunkResourceRead(mN, "256²N", out bool chunkOk));
        _log.Append(MineDeriveRead(mN, "256²N", out bool mineOk));
        _log.Append(SpawnMineDistanceRead(mN, "256²N"));

        _log.AppendLine("\n---- [128²/Normal(diff2)] ----");
        var m128 = Build(SEED, 128, 128, 2);
        _log.Append(BandQuotaRead(m128, cfg, 2, "128²"));
        _log.Append(PitRead(m128, "128²", out _));
        _log.Append(ChunkResourceRead(m128, "128²", out _));
        _log.Append(MineDeriveRead(m128, "128²", out _));

        _log.AppendLine("\n---- [确定性·同 seed 两次 / 异 seed] ----");
        var a1 = Build(SEED, 256, 256, 2); var a2 = Build(SEED, 256, 256, 2); var a3 = Build(SEED + 1, 256, 256, 2);
        bool same = SameMap(a1, a2, out var why);
        _log.AppendLine($"  同 seed 两次：{(same ? "逐格一致 ✅（features+climateZones+spawns；nb 恒空·该段恒等——见 SameMap 注）" : "不一致 ❌ " + why)}");
        _log.AppendLine($"  异 seed 出异图：{(!SameMap(a1, a3, out _) ? "✅" : "❌")}");

        _log.AppendLine("\n---- [难度三档·资源单位比（目标 0.7:1.0:1.3）] ----");
        var counts = new int[3];
        for (int d = 1; d <= 3; d++)
        {
            var md = Build(SEED, 256, 256, d); int c = 0;
            for (int y = 0; y < md.height; y++) for (int x = 0; x < md.width; x++) c += UnitsOfChunkCell(md, x, y);
            counts[d - 1] = c;
            _log.AppendLine($"  difficulty={d} 资源单位={c}");
        }
        if (counts[1] > 0)
            _log.AppendLine($"  归一化比 = {counts[0] / (double)counts[1]:0.000} : 1.000 : {counts[2] / (double)counts[1]:0.000}");

        _log.AppendLine($"\n===== 判定：PitOk={pitOk} ChunkOk={chunkOk} MineDeriveOk={mineOk}(作废项·恒预期 False) SameSeedOk={same} =====");
        WriteLog();
        Debug.Log(TAG + " 编辑态探针完成 → Logs/hh291_probe.log");
        return _log.ToString();
    }

    // ============================== 入口2：实机正门 ==============================

    public static void RunLive()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError(TAG + " RunLive 须先进入 Play。"); return; }
        _log.Clear();
        new GameObject("HH291_LiveRunner").AddComponent<Host>().Start2(RunLiveCo());
    }

    class Host : MonoBehaviour { public void Start2(IEnumerator r) => StartCoroutine(r); }

    /// <summary>实机正门读数协程（**公开**：供 bridge `exec_runtime_script` 直接 `return` 等待完成）。</summary>
    public static IEnumerator RunLiveCo()
    {
        _log.AppendLine("======== HH.291 实机正门读数（A6 运行期 ＋ M5 性能）========");
        var cfg = new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = SLOT, kingdomName = "河谷王国"
        };
        float t0 = Time.realtimeSinceStartup;
        yield return TestHarnessApi.EnterTestRun(cfg, 15f);
        float t1 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || BuildingRegistry.Instance == null || KingdomRegistry.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t1 > 120f) { _log.AppendLine("等就绪超时"); WriteLog(); yield break; }
        }
        while (Time.realtimeSinceStartup - t1 < 5f) yield return null;
        _log.AppendLine($"  [M5] 正门进局加载（EnterGame→就绪+5s）实测 = {Time.realtimeSinceStartup - t0:0.00}s（seed {SEED}/Medium/diff2/15x）");

        var map = WorldManager.Instance.ActiveMap;
        var reg = KingdomRegistry.Instance;

        int mineInst = 0, mineProducer = 0, mineByprod = 0, totalB = 0;
        foreach (var b in BuildingRegistry.Instance.All)
        {
            if (b == null) continue;
            totalB++;
            if (b.def != null && b.def.id == "mine")
            {
                mineInst++;
                if (b.GetComponent<ProducerComponent>() != null) mineProducer++;
                if (b.GetComponent<MineByproductComponent>() != null) mineByprod++;
            }
        }
        int nbMine = 0;
        foreach (var nb in map.naturalBuildings) if (nb.feature == FeatureType.Mine) nbMine++;   // 【片6-2收尾】nb 恒空 ⇒ 恒 0
        bool a6a = mineInst > 0;
        _log.AppendLine($"  **A6①** Mine Building 实例数 = **{mineInst}**（须>0）· 建筑实例总数={totalB} · 地图 Mine nb={nbMine}（恒 0）"
                        + $"（【片 6-2 收尾】`naturalBuildings` 恒空 ⇒ nb 恒 0·Mine 锚点现由 features 承载，见 M6 读口）");
        _log.AppendLine($"  **A6②** 组件在场：`ProducerComponent` = **{mineProducer}**/{mineInst} ｜ `MineByproductComponent` = **{mineByprod}**/{mineInst}");
        _log.AppendLine($"    口径勘正（实读）：`mine` 为 `isResourceNode=1` 采集点身份 ⇒ `BuildingFactory.AttachComponents:295`" +
                        $"（`!def.isResourceNode` 排除通用产能分支）**结构性不挂 `ProducerComponent`**；" +
                        $"副产链组件＝`MineByproductComponent`（`DZ-072a`/`D562`/`HH.107 件1`，`:316-319`）。");
        var mineDef = BuildingFactory.FindDefById("mine");
        _log.AppendLine($"  [A6 资产面] mine.isResourceNode={(mineDef != null ? mineDef.isResourceNode : false)} " +
                        $"isMineByproduct={(mineDef != null ? mineDef.isMineByproduct : false)} " +
                        $"outputResource={(mineDef != null ? mineDef.outputResource.ToString() : "null")} " +
                        $"producer.rate={(mineDef != null ? mineDef.producer.rate : -1):0.###} kind={(mineDef != null ? mineDef.producer.kind.ToString() : "null")} " +
                        $"footprint={(mineDef != null ? mineDef.footprint.ToString() : "null")}");
        _log.AppendLine($"  [A6 对照] 改前 mine 实例数 = 0（HH.2 A+ 起派生白名单不含 Mine；仅 `KingdomFoundry` 立国预置" +
                        $"＝本次实测 {mineInst} 的构成；【片 6-2 收尾】nbMine 恒 0（锚点不再派生实体）)");
        bool a6b = mineByprod > 0;   // 能力句改判：可挂组件在场（副产链）

        int aiKid = -1;
        foreach (var k in reg.GetAll()) if (!k.IsPlayer) { aiKid = k.id; break; }
        var center = FindAiCenter(aiKid, new Vector2Int(map.width / 2, map.height / 2));
        var probeB = aiKid >= 0 ? BuildAt(mineDef, center, 6, aiKid) : null;
        yield return null;
        var stComp = probeB != null ? probeB.GetComponent<StorageComponent>() : null;
        int kStone0 = (aiKid >= 0 && reg.Get(aiKid) != null) ? reg.Get(aiKid).resources.stone : -1;
        int kCrystal0 = (aiKid >= 0 && reg.Get(aiKid) != null) ? reg.Get(aiKid).crystal : -1;
        int kStoneMax = kStone0, kCrystalMax = kCrystal0;
        // A6③ 全局读数：全图 mine 副产子仓合计（对派遣落点鲁棒）＋ 台账
        int GenTotal(ResourceType rt)
        {
            int sum = 0;
            foreach (var b in BuildingRegistry.Instance.All)
            {
                if (b == null || b.def == null || b.def.id != "mine") continue;
                var c = b.GetComponent<MineByproductComponent>();
                if (c == null) continue;
                var st = c.GetStore(rt);
                if (st != null) sum += st.storedAmount;
            }
            return sum;
        }
        int dutyMines = 0;
        // 产率/日长加速（Play 态 SO 改动·收尾还原；HH107/OB12 先例）
        var kcfg = KingdomManager.Instance != null ? KingdomManager.Instance.Config : null;
        float oC = 0f, oF = 0f, oO = 0f;
        if (kcfg != null)
        {
            oC = kcfg.byproductCrystalRate; oF = kcfg.byproductFireOilRate; oO = kcfg.byproductOreRate;
            kcfg.byproductCrystalRate = 2f; kcfg.byproductFireOilRate = 2f; kcfg.byproductOreRate = 2f;
        }
        if (TimeManager.Instance != null) TimeManager.Instance.SetSecondsPerDay(15f);
        int c0g = GenTotal(ResourceType.Crystal), f0g = GenTotal(ResourceType.FireOil), o0g = GenTotal(ResourceType.Ore);
        int sp = 0;
        for (int i = 0; i < 12; i++)
        {
            var w = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Worker,
                (Vector2)probeB.transform.position + new Vector2(0.7f * (i % 4), 0.7f * (i / 4)), aiKid);
            if (w != null) sp++;
        }
        yield return null;
        int cMax = c0g, fMax = f0g, oMax = o0g;
        float w0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - w0 < 60f)
        {
            yield return new WaitForSeconds(1f);
            int d = 0;
            foreach (var b in BuildingRegistry.Instance.All)
                if (b != null && b.def != null && b.def.id == "mine"
                    && TaskScheduler.HasInstance && TaskScheduler.Instance.HasWorkerAssigned(b)) d++;
            if (d > dutyMines) dutyMines = d;
            int cg = GenTotal(ResourceType.Crystal), fg = GenTotal(ResourceType.FireOil), og = GenTotal(ResourceType.Ore);
            if (cg > cMax) cMax = cg; if (fg > fMax) fMax = fg; if (og > oMax) oMax = og;
            if (aiKid >= 0 && reg.Get(aiKid) != null)
            {
                if (reg.Get(aiKid).resources.stone > kStoneMax) kStoneMax = reg.Get(aiKid).resources.stone;
                if (reg.Get(aiKid).crystal > kCrystalMax) kCrystalMax = reg.Get(aiKid).crystal;
            }
            if (cMax > c0g || fMax > f0g || oMax > o0g) break;   // L-34 命中即停
        }
        bool a6c = (cMax > c0g) || (fMax > f0g) || (oMax > o0g);
        _log.AppendLine($"  **A6③ 入库读数**（全图 mine 副产子仓合计·补员={sp}·在岗 mine 峰值={dutyMines}/{mineInst}·60s 窗）：" +
                        $"水晶 {c0g}→**{cMax}** ｜ 火油 {f0g}→{fMax} ｜ 矿石 {o0g}→{oMax} ⇒ {(a6c ? "✅ 有读数" : "❌ 无读数")}");
        _log.AppendLine($"    **石（石入库）专列**：定点 mine 本地仓 `StorageComponent` = {(stComp == null ? "**不存在**（`isResourceNode` 排除分支 ⇒ mine 本体无仓）" : stComp.storedAmount.ToString())}" +
                        $" · 台账 stone {kStone0} → {kStoneMax}（增量 {kStoneMax - kStone0}）· 台账 crystal {kCrystal0} → {kCrystalMax}（副产搬运链落点）");
        _log.AppendLine($"    🔴 缺口声明（口径）：`mine` 走 `isResourceNode=1` 采集点通道 ⇒ `Building.TryAdvertiseTask:960` 的 Gather 分支要求 `isConsumable`（mine＝持续·不满足）／" +
                        $"`:975` 的 Production 分支要求 `ProducerComponent`（结构性不挂）⇒ **mine 实体无「产石」通道**（石来源＝`StonePile` 一次性采集 ＋ `quarry` 采石场）；" +
                        $"`mine.asset` 的 `producer.rate=0.3 / output=Stone` 在该身份下**不可达（死字段）** ⇒ 「石入库有读数」本批结构性不可达（列报 R2）。");

        // [R3 阳性对照·L-29] 绕过调度竞争，直派 1 名专用工人 ⇒ 分离「链本体/可派性」与「调度竞争未选中」。
        //   双试：A=探针 mine（2×2 占格）／B=对照 1×1 自然建筑（同调度器·同直派口径）⇒ 隔离「2×2 占格目标点」效应。
        //   仅解释 R3·不改判 A6③（石通道结构性不可达）。
        int idleW = 0, busyW = 0;
        foreach (var br in Object.FindObjectsOfType<NPCBrain>())
        {
            if (br == null) continue;
            var uc2 = br.GetComponent<UnitController>();
            if (uc2 == null || uc2.npcId == 0) continue;
            if (br.IsIdleForTask) idleW++; else busyW++;
        }
        string pcLine = "无（探针 mine 取不到）", ctrlLine = "无（未找到 1×1 对照建筑）";
        var byprodProbe = probeB != null ? probeB.GetComponent<MineByproductComponent>() : null;
        if (byprodProbe != null)
        {
            pcLine = "工人未生成（npcId=0）";
            // 落点取「邻近可走格」（防落点落在占格内＝探针自体假象）
            var spCell = MapGenRules.NearestWalkable(map, probeB.coord.x + 2, probeB.coord.y);
            Vector2 spawnAt = spCell.x >= 0 ? (Vector2)GridSystem.Instance.CoordToWorld(new GridCoord(spCell.x, spCell.y))
                                            : (Vector2)probeB.transform.position + new Vector2(1.2f, 0f);
            var pcw = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Worker, spawnAt, aiKid);
            yield return null;
            var pcb = pcw != null ? pcw.GetComponent<NPCBrain>() : null;
            var pcc = pcw != null ? pcw.GetComponent<UnitController>() : null;
            if (pcb != null && pcc != null && pcc.npcId != 0 && TaskScheduler.HasInstance)
            {
                var sched = TaskScheduler.Instance;
                var tk2 = new KingdomTask(KingdomTaskType.Production, byprodProbe) { destType = KingdomDestType.None };
                sched.DispatchExternal(pcb, tk2);
                int pc0 = byprodProbe.GetStore(ResourceType.Crystal).storedAmount;
                int pcMax = pc0, pcDuty = 0, pcState = -1; float pcDist = -1f; float pt = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - pt < 20f)
                {
                    yield return new WaitForSeconds(0.5f);
                    if (sched.HasWorkerAssigned(byprodProbe)) pcDuty = 1;
                    pcState = (int)sched.GetWorkerState(pcc.npcId);
                    pcDist = Vector2.Distance(pcw.transform.position, byprodProbe.SourcePos);
                    int v = byprodProbe.GetStore(ResourceType.Crystal).storedAmount;
                    if (v > pcMax) pcMax = v;
                    if (pcMax > pc0) break;   // L-34 命中即停
                }
                pcLine = $"水晶 {pc0}→**{pcMax}**（Δt≈{Time.realtimeSinceStartup - pt:0.0}s·在岗={pcDuty}·末态={pcState}·末距源={pcDist:0.00} 格）" +
                         $" ⇒ {(pcMax > pc0 ? "链本体可用 ✅" : "无读数 ❌")}";
            }

            // (B) 对照组：最近 1×1 自然建筑（有仓或产能组件者）
            Building ctrlB = null; float cd = float.MaxValue;
            foreach (var b in BuildingRegistry.Instance.All)
            {
                if (b == null || b.def == null || b.gameObject == probeB.gameObject) continue;
                if (b.def.footprint.x != 1 || b.def.footprint.y != 1) continue;
                if (b.GetComponent<StorageComponent>() == null && b.GetComponent<ProducerComponent>() == null) continue;
                float d2 = ((Vector2)b.transform.position - (Vector2)probeB.transform.position).sqrMagnitude;
                if (d2 < cd) { cd = d2; ctrlB = b; }
            }
            if (ctrlB != null)
            {
                var csp = MapGenRules.NearestWalkable(map, ctrlB.coord.x + 2, ctrlB.coord.y);
                Vector2 cSpawn = csp.x >= 0 ? (Vector2)GridSystem.Instance.CoordToWorld(new GridCoord(csp.x, csp.y))
                                            : (Vector2)ctrlB.transform.position + new Vector2(1.2f, 0f);
                var cw = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Worker, cSpawn, aiKid);
                yield return null;
                var cb = cw != null ? cw.GetComponent<NPCBrain>() : null;
                var cc = cw != null ? cw.GetComponent<UnitController>() : null;
                if (cb != null && cc != null && cc.npcId != 0 && TaskScheduler.HasInstance)
                {
                    var sched2 = TaskScheduler.Instance;
                    var tk3 = new KingdomTask(KingdomTaskType.Production, ctrlB) { destType = KingdomDestType.None };
                    sched2.DispatchExternal(cb, tk3);
                    int cDuty = 0, cState = -1; float cDist = -1f; float ct = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - ct < 20f)
                    {
                        yield return new WaitForSeconds(0.5f);
                        if (sched2.HasWorkerAssigned(ctrlB)) cDuty = 1;
                        cState = (int)sched2.GetWorkerState(cc.npcId);
                        cDist = Vector2.Distance(cw.transform.position, ctrlB.SourcePos);
                        if (cState == (int)TaskState.Working) break;   // L-34 命中即停
                    }
                    ctrlLine = $"{ctrlB.def.id}（1×1）在岗={cDuty}·末态={cState}·末距源={cDist:0.00} 格（Δt≈{Time.realtimeSinceStartup - ct:0.0}s）" +
                               $" ⇒ {(cDuty > 0 ? "达 Working ✅" : "未达 Working ❌")}";
                }
                else ctrlLine = "对照工人未生成（npcId=0）";
            }
        }
        _log.AppendLine($"  **R3 阳性对照**（直派 1 工绕过调度竞争·实测空闲工人 {idleW}／非空闲 {busyW}）");
        _log.AppendLine($"    (A) 探针 mine（2×2 占格）：{pcLine}");
        _log.AppendLine($"    (B) 对照 1×1 自然建筑：{ctrlLine}");

        // M5：帧时对照（含 mine 实例 vs 运行期销毁后）
        float dtOn = 0f, dtOff = 0f;
        yield return SampleAvgDeltaCo(4f, v => dtOn = v);
        int goOn = Object.FindObjectsOfType<GameObject>().Length;
        var mines = new List<Building>();
        foreach (var b in BuildingRegistry.Instance.All) if (b != null && b.def != null && b.def.id == "mine") mines.Add(b);
        for (int i = 0; i < mines.Count; i++) if (mines[i] != null) Object.Destroy(mines[i].gameObject);
        yield return new WaitForSeconds(2f);
        yield return SampleAvgDeltaCo(4f, v => dtOff = v);
        int goOff = Object.FindObjectsOfType<GameObject>().Length;
        _log.AppendLine($"  [M5] 帧时（同局同机位·逐帧采样）：含 mine {dtOn:0.###} ms/帧 ｜ 销毁后(对照态) {dtOff:0.###} ms/帧 ⇒ Δ={(dtOn - dtOff):0.###} ms/帧；" +
                        $"GameObject {goOn} → {goOff}（Δ={goOn - goOff}）");
        double cost = MeasureInstantiateCost(mineDef, 200);
        _log.AppendLine($"  [M5] 单座 mine 直建成本（同 CreateBuildingInstance 链·n=200）= {cost:0.###} ms/座 ⇒ A6 新增 {nbMine} 座外推 ≈ **{cost * nbMine:0.0} ms**（口径：编辑器内线性外推・非整链实测差）");

        _log.AppendLine($"\n===== 判定：A6①={a6a} A6②(副产组件在场)={a6b} A6③(副产入库读数)={a6c} ｜ 石入库＝结构性不可达（见上 🔴） =====");
        WriteLog();
        if (kcfg != null) { kcfg.byproductCrystalRate = oC; kcfg.byproductFireOilRate = oF; kcfg.byproductOreRate = oO; }
        TestHarnessApi.ExitTestRun();
        try { SmokeApi.QuitSmoke(); } catch (System.Exception e) { Debug.LogWarning(TAG + " QuitSmoke 异常：" + e.Message); }
        Debug.Log(TAG + " 实机探针收工（ExitTestRun+QuitSmoke）→ Logs/hh291_probe.log");
    }

    /// <summary>逐帧采样平均帧时（ms）。</summary>
    static IEnumerator SampleAvgDeltaCo(float seconds, System.Action<float> sink)
    {
        float start = Time.realtimeSinceStartup; double sum = 0; int n = 0;
        while (Time.realtimeSinceStartup - start < seconds) { sum += Time.unscaledDeltaTime; n++; yield return null; }
        sink(n > 0 ? (float)(sum * 1000.0 / n) : 0f);
    }

    static double MeasureInstantiateCost(BuildingDef def, int n)
    {
        if (def == null || BuildingFactory.Instance == null || n <= 0) return 0;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) return 0;
        var g = GridSystem.Instance;
        int left = n;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int r = 0; r < 600 && left > 0; r++)
        {
            int cx = (r % 24) * 10 + 5, cy = (r / 24) * 10 + 5;
            if (cx >= map.width - 12 || cy >= map.height - 12) break;
            var cell = MapGenRules.NearestWalkable(map, cx, cy);
            if (cell.x < 0) continue;
            var coord = new GridCoord(cell.x, cell.y);
            var fp = new Vector2Int(def.footprint.x > 0 ? def.footprint.x : 1, def.footprint.y > 0 ? def.footprint.y : 1);
            // 【HH.294 片3-A】中心点换算走 GridSystem 唯一内核（原先此处「就地展开」）
            Vector3 world = GridSystem.FootprintCenterWorld(coord, fp, new Vector3(coord.x, coord.y, 0f));
            if (BuildingFactory.Instance.CreateBuildingInstance(def, def.sourceType, coord, fp, world,
                    isPlayerBuilt: false, grade: ResourceGrade.Normal, isConsumable: false,
                    initialState: BuildingState.Active, kingdomId: -2)) left--;
        }
        sw.Stop();
        int done = n - left;
        var made = new List<Building>();
        foreach (var b in BuildingRegistry.Instance.All) if (b != null && b.kingdomId == -2) made.Add(b);
        for (int i = 0; i < made.Count; i++) if (made[i] != null) Object.Destroy(made[i].gameObject);
        return done > 0 ? sw.Elapsed.TotalMilliseconds / done : 0;
    }

    static Building BuildAt(BuildingDef def, Vector2Int center, int ringIdx, int kingdomId)
    {
        if (def == null || BuildingFactory.Instance == null || GridSystem.Instance == null) return null;
        var map = WorldManager.Instance.ActiveMap; var g = GridSystem.Instance;
        Vector2Int cell = MapGenRules.NearestWalkable(map, center.x, center.y);
        for (int r = 1; r <= 12 && r <= ringIdx; r++)
        {
            var cand = MapGenRules.NearestWalkable(map, center.x + r, center.y);
            if (cand != cell || r == ringIdx) { cell = cand; break; }
        }
        var fp = new Vector2Int(def.footprint.x > 0 ? def.footprint.x : 1, def.footprint.y > 0 ? def.footprint.y : 1);
        var coord = new GridCoord(cell.x, cell.y);
        // 【HH.294 片3-A】中心点换算走 GridSystem 唯一内核（原先此处「就地展开」）
        Vector3 world = GridSystem.FootprintCenterWorld(coord, fp, new Vector3(coord.x, coord.y, 0f));
        if (!BuildingFactory.Instance.CreateBuildingInstance(def, def.sourceType, coord, fp, world,
                isPlayerBuilt: false, grade: ResourceGrade.Normal, isConsumable: false,
                initialState: BuildingState.Active, kingdomId: kingdomId)) return null;
        var all = BuildingRegistry.Instance.All;
        for (int i = all.Count - 1; i >= 0; i--)
            if (all[i] != null && all[i].coord.x == coord.x && all[i].coord.y == coord.y) return all[i];
        return null;
    }

    static Vector2Int FindAiCenter(int kid, Vector2Int fallback)
    {
        foreach (var b in Object.FindObjectsOfType<Building>())
            if (b != null && b.kingdomId == kid && b.def != null && b.def.id == "castle")
                return new Vector2Int(b.coord.x, b.coord.y);
        return fallback;
    }

    static void WriteLog()
    {
        try
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Logs"));
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "hh291_probe.log"),
                _log.ToString() + "\n（" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "）\n");
            Debug.Log(TAG + " 读数已写 " + Path.Combine(dir, "hh291_probe.log"));
        }
        catch (System.Exception e) { Debug.LogError(TAG + " 日志写盘失败: " + e.Message); }
    }
}
#endif
