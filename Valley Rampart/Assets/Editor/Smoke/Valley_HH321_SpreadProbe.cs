// ============================================================================
//  HH.321 段A · 散布配平实测探针（`projectileErrorRadius` 1.5 → 0.25）
//   ⭐ 载体＝生产链：`DamageSystem.RegisterAttack` → `ExecuteAttack` → `ProjectileManager.SpawnProjectile`
//      （散布 ＝ **唯一口** `CombatRules.NextSpreadOffset`，`ProjectileManager.cs:99-103`）
//      → `ProjectileManager.Update` → `OnProjectileArrived` → `DamageSystem.ApplyDamage`
//   ⭐ 确定性：`CombatRules.ResetCombatRandom(seed)` 为生产【唯一重置口】⇒ **每发前定点重置**
//      `seedBase + i` ⇒ 同一 seed 序列下「散布偏移 ⇒ 命中位串」可复现（同 seed 复跑逐字节一致）。
//   ⚠️ 构造法（探针侧 · 已声明）：
//      ① 射手/靶 `NPCBrain.enabled = false` ＋ 每发前 Pin（消除 AI 漂移 ⇒ 命中判据只由几何决定）
//      ② 靶每发前 `Heal(MaxHp)`（保活 · 避免死亡/重生路径污染读数）
//      ③ 开火后立刻 `Unregister`（防时间轮按 CD 自动补射 ⇒ 逐发一一对应）
//      ④ 「改前」臂 ＝ 运行期改 `_config.projectileErrorRadius = 1.5`（⛔ **不写资产** · 收尾还原）
//   ⛔ 本容器**不改生产码**；产物落 仓库根 `Logs/hh321a_*.txt`
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class Valley_HH321_SpreadProbe
{
    const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly StringBuilder SB = new StringBuilder();
    static readonly List<IDamageable> _scan = new List<IDamageable>();
    static string _outPath;

    static void L(string s)
    {
        Debug.Log("[HH321A] " + s);
        SB.AppendLine(s);
    }

    static void Flush(string tag)
    {
        SB.AppendLine("# 封存 " + System.DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        var dir = System.IO.Path.Combine(Application.dataPath, "..", "..", "Logs");
        System.IO.Directory.CreateDirectory(dir);
        _outPath = System.IO.Path.Combine(dir,
            "hh321a_" + tag + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".txt");
        System.IO.File.WriteAllText(_outPath, SB.ToString());
        Debug.Log("[HH321A] 落盘 " + _outPath);
    }

    // ===== FNV-1a（位串/序列 hash · 逐字节一致性用）=====
    static string Hash(string s)
    {
        unchecked
        {
            uint h = 2166136261u;
            foreach (char c in s) { h ^= c; h *= 16777619u; }
            return h.ToString("X8", CultureInfo.InvariantCulture);
        }
    }

    static string F(float v) => v.ToString("F6", CultureInfo.InvariantCulture);

    static Vector2 CellToWorldF(float gx, float gy)
        => GridSystem.CellToWorldF(gx, gy, new Vector2(GridMath.CellW, GridMath.CellH));

    static Vector2 CellCenterWorld(GridCoord c) => CellToWorldF(c.x + 0.5f, c.y + 0.5f);

    static void MoveUnit(UnitController u, Vector2 pos)
    {
        u.transform.position = pos;
        var rb = typeof(UnitController).GetField("_rb", BF)?.GetValue(u) as Rigidbody2D;
        if (rb != null) { rb.position = pos; rb.velocity = Vector2.zero; }   // ⚠️ 15× 速档下单帧游戏时间≈1s ⇒ 必须清速度（否则弹道飞行期漂移 >1 格）
        Physics2D.SyncTransforms();
    }

    static UnitController SpawnUnitOf(Faction f, Occupation occ, Vector2 pos, int kingdomId)
    {
        var go = UnitFactory.Instance.SpawnUnit(f, occ, pos, kingdomId);
        var uc = go != null ? go.GetComponent<UnitController>() : null;
        if (uc != null) MoveUnit(uc, pos);
        return uc;
    }

    /// <summary>关掉 AI 决策（⛔ 探针构造：消除漂移 ⇒ 命中判据纯几何）</summary>
    static void FreezeBrain(UnitController uc)
    {
        if (uc == null) return;
        var b = uc.GetComponent<NPCBrain>();
        if (b != null) b.enabled = false;
    }

    private class RunHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    [MenuItem("Valley/验证/HH321 段A 散布配平实测")]
    public static void RunSpread()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH321A] 须先进入 Play 后调用本菜单。"); return; }
        new GameObject("HH321A_Host").AddComponent<RunHost>().Host(RunSpreadCo());
    }

    /// <summary>供 bridge `exec_runtime_script` 直接 return 执行；菜单路径走 <see cref="RunSpread"/>。</summary>
    public static IEnumerator RunSpreadCoroutine() => RunSpreadCo();

    static IEnumerator RunSpreadCo()
    {
        const int SHOTS_UNIT = 80;      // 改后臂（单位）≥50
        const int SHOTS_UNIT_OLD = 120; // 改前臂（单位）
        const int SHOTS_BLD = 60;       // 改后臂（建筑）≥50
        const int SHOTS_BLD_OLD = 60;   // 改前臂（建筑）
        const int SAMPLES = 80;         // 散布抽样
        const float OLD_ERR = 1.5f;     // 改前资产值（对照臂 · 运行期）

        var cfg = new NewGameConfig
        {
            worldSeed = 20321, mapSeed = 20321, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = "smoke_hh321a", kingdomName = "散布配平"
        };
        Debug.Log("[HH321A] 阶段：EnterTestRun（15× 考跑加速）");
        yield return TestHarnessApi.EnterTestRun(cfg, 15f);
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || GridSystem.Instance == null || GridSystem.Instance.Config == null
               || BuildingRegistry.Instance == null || UnitFactory.Instance == null
               || ProjectileManager.Instance == null || DamageSystem.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 180f) { Debug.LogError("[HH321A] 等世界就绪超时。"); yield break; }
        }
        yield return new WaitForSeconds(0.5f);

        var grid = GridSystem.Instance;
        var pm = ProjectileManager.Instance;
        var rtCfg = typeof(ProjectileManager).GetField("_config", BF).GetValue(pm) as DamageConfig;
        if (rtCfg == null) { Debug.LogError("[HH321A] PM._config 取不到。"); TestHarnessApi.ExitTestRun(); yield break; }

        float assetErr = rtCfg.projectileErrorRadius;          // 资产运行期值（改后应为 0.25）
        float hitR = rtCfg.hitRadiusCells;                     // 0.25 视觉格
        float step = GridMath.CellStep;                        // 0.715542
        float hitRadiusWorld = hitR * step;                    // 0.1789 世界单位
        Vector2 cs = grid.Config.cellSize;

        L("=== HH.321 段A · 散布配平实测（生产链）===");
        L($"[环境] seed={WorldManager.Instance.ActiveMap.seed} 速档=15× timeScale={Time.timeScale} 单位在册={UnitRegistry.Instance.Count} 建筑在册={BuildingRegistry.Instance.Count}");
        L($"[配置实读] DamageConfig(运行期) projectileErrorRadius={F(assetErr)}（＝资产 DamageConfig.asset:23 现值）"
          + $" hitRadiusCells={F(hitR)} 视觉格 ⇒ 命中半径(世界)={F(hitRadiusWorld)} · 格步长={F(step)} · cellSize={cs}");

        // ── 解析预期（读数列 · 面积比）──
        float unitRateNew = Mathf.Clamp01(Mathf.Pow(hitRadiusWorld / assetErr, 2f));
        float unitRateOld = Mathf.Clamp01(Mathf.Pow(hitRadiusWorld / OLD_ERR, 2f));
        float bldArea = 2f * cs.x * cs.y;                       // 2×2 菱形底座面积（＝2×1.28×0.64＝1.6384）
        float diskNew = Mathf.PI * assetErr * assetErr;
        float diskOld = Mathf.PI * OLD_ERR * OLD_ERR;
        float bldRateNew = Mathf.Clamp01(bldArea / diskNew);
        float bldRateOld = Mathf.Clamp01(bldArea / diskOld);
        L($"[解析] 单位臂：改后 ({F(hitRadiusWorld)}/{F(assetErr)})² = {unitRateNew * 100f:F1}% ｜改前 ({F(hitRadiusWorld)}/{F(OLD_ERR)})² = {unitRateOld * 100f:F2}%");
        L($"[解析] 建筑臂（2×2 底座 面积={F(bldArea)}）：改后 盘面积={F(diskNew)} ⇒ {bldRateNew * 100f:F1}%（底座完全覆盖散布盘）"
          + $" ｜改前 盘面积={F(diskOld)} ⇒ {bldRateOld * 100f:F1}%");

        // ── 选址：A＝farm 落格（2×2）；A-(3,0)＝建筑射手位；U＝A+(0,6)＝单位靶位；U-(3,0)＝单位射手位 ──
        var anchor = grid.WorldToCoord(WorldManager.Instance.GetKingdomAnchorWorld());
        if (!anchor.HasValue) { Debug.LogError("[HH321A] 锚点格取不到。"); TestHarnessApi.ExitTestRun(); yield break; }
        GridCoord A = default; bool found = false;
        for (int ring = 24; ring <= 108 && !found; ring += 12)
            for (int dx = -24; dx <= 24 && !found; dx += 12)
                for (int dy = -24; dy <= 24 && !found; dy += 12)
                {
                    var c = new GridCoord(anchor.Value.x + dx, anchor.Value.y - ring + dy);
                    bool ok = true;
                    for (int fx = 0; fx < 2 && ok; fx++)
                        for (int fy = 0; fy < 2 && ok; fy++)
                        {
                            var cc = new GridCoord(c.x + fx, c.y + fy);
                            if (!grid.IsWalkable(cc) || grid.GetOccupant(cc) != null || grid.GetFeatureAt(cc) != FeatureType.Plain) ok = false;
                        }
                    foreach (var off in new[] { new GridCoord(-3, 0), new GridCoord(0, 6), new GridCoord(-3, 6) })
                    {
                        var cc = new GridCoord(c.x + off.x, c.y + off.y);
                        if (!grid.IsWalkable(cc) || grid.GetOccupant(cc) != null) ok = false;
                    }
                    if (!ok) continue;
                    Vector2 w = CellCenterWorld(c);
                    _scan.Clear();
                    PerceptionSystem.QueryNearby(w, GridMath.VisualToWorld(12f), Faction.PlayerCamp, true, _scan);
                    _scan.Clear();
                    PerceptionSystem.QueryNearby(w, GridMath.VisualToWorld(12f), Faction.AiKingdom, true, _scan);
                    if (_scan.Count == 0) { A = c; found = true; }
                }
        if (!found) { Debug.LogError("[HH321A] 找不到含 4 个可用格的场址。"); TestHarnessApi.ExitTestRun(); yield break; }
        var U = new GridCoord(A.x, A.y + 6);

        var bDef = Resources.Load<BuildingDef>("Buildings/farm");
        var fp = new Vector2Int(2, 2);
        Vector2 bCenter = GridSystem.FootprintCenterWorld(A, fp, Vector3.zero);
        bool built = BuildingFactory.Instance.CreateBuildingInstance(
            bDef, bDef.sourceType, A, fp, bCenter,
            isPlayerBuilt: false, grade: ResourceGrade.Normal, isConsumable: false,
            initialState: BuildingState.Active, kingdomId: 1);
        yield return null;
        var bld = grid.GetOccupant(A) as Building;
        if (!built || bld == null) { Debug.LogError("[HH321A] 建筑落成失败。"); TestHarnessApi.ExitTestRun(); yield break; }

        Vector2 sBPos = CellCenterWorld(new GridCoord(A.x - 3, A.y));       // 建筑射手位
        Vector2 uPos = CellCenterWorld(U);                                  // 单位靶位
        Vector2 sUPos = CellCenterWorld(new GridCoord(U.x - 3, U.y));       // 单位射手位
        var shooterB = SpawnUnitOf(Faction.PlayerCamp, Occupation.Archer, sBPos, 0);
        var shooterU = SpawnUnitOf(Faction.PlayerCamp, Occupation.Archer, sUPos, 0);
        var foe = SpawnUnitOf(Faction.PlayerCamp, Occupation.Warrior, uPos, 1);   // kingdomId=1 ⇒ AiKingdom（敌对）
        yield return null;
        FreezeBrain(shooterB); FreezeBrain(shooterU); FreezeBrain(foe);
        yield return null;
        if (bld != null) bld.hp = bld.maxHp;
        if (foe != null) foe.Heal(foe.MaxHp);

        L($"[布置] 建筑={bDef.name} 主格=({A.x},{A.y}) footprint=({fp.x},{fp.y}) 中心={bCenter} 阵营={bld.GetFaction()} HP={bld.CurrentHp}/{bld.MaxHp} 工事?={(bld.IsFortification ? "是(⛔异常)" : "否")}");
        L($"[布置] 建筑射手@{sBPos} DistVisual={F(GridMath.DistVisual(sBPos, bCenter))}（range=6）｜单位靶@{uPos}(格({U.x},{U.y})) HP={foe.CurrentHp}/{foe.MaxHp} 阵营={foe.GetFaction()}"
          + $"｜单位射手@{sUPos} DistVisual={F(GridMath.DistVisual(sUPos, uPos))}｜两靶间距(格轴)={F(GridMath.DistVisual(uPos, bCenter))}（⇒ 互不入 3×3 底座查窗）");
        L($"[布置] NPCBrain 已关：射手B={(shooterB != null)} 射手U={(shooterU != null)} 单位靶={(foe != null)}（⛔ 构造：消漂移）");

        var profile = new AttackProfile
        {
            attack = 25, range = 6f, cd = 1f, isRanged = true, projectileSpeed = 60f,
            projectileType = ProjectileType.Arrow, pierceLevel = 1,
            ballisticType = BallisticType.Lob, arcHeightCells = 0f,
            aoeRadiusCells = 0f, aoeFalloff = 0f, effectType = GroundEffectType.None,
        };

        // ══ K1 · 散布存在性 ＋ 确定性（生产唯一口抽样 · 紧环无让帧）══
        {
            var s1 = SampleOffsets(9001, assetErr, SAMPLES);
            var s2 = SampleOffsets(9001, assetErr, SAMPLES);
            string h1 = Hash(s1.serialized), h2 = Hash(s2.serialized);
            L($"[K1·散布存在性] 抽样 {SAMPLES} 个偏移（r={F(assetErr)}）⇒ distinct={s1.distinct}/{SAMPLES}"
              + $" r_min={F(s1.rMin)} r_max={F(s1.rMax)} r_mean={F(s1.rMean)} 零向量={s1.zeros} ⇒ 最大半径 ≤ r = {(s1.rMax <= assetErr + 1e-5f)}");
            L($"[K1·确定性] 同 seed 9001 复跑两次 ⇒ hash1={h1} hash2={h2} 逐字节一致={(h1 == h2)}"
              + $"\n[K1·序列1] {s1.serialized}\n[K1·序列2] {s2.serialized}");
            L($"[K1·异seed对照] seed 9002 ⇒ hash={Hash(SampleOffsets(9002, assetErr, SAMPLES).serialized)}（期望 ≠ 上）");
        }

        float savedErr = rtCfg.projectileErrorRadius;

        // ══ K2 · 单位命中率 · 改后（资产 0.25） ══
        rtCfg.projectileErrorRadius = assetErr;
        var r2 = new ArmResult();
        yield return UnitArm(SHOTS_UNIT, 7000, foe, shooterU, uPos, sUPos, profile, assetErr, hitRadiusWorld, r2);
        L($"[K2·单位·改后({F(assetErr)})] 命中 {r2.hits}/{SHOTS_UNIT} = {100f * r2.hits / SHOTS_UNIT:F1}%"
          + $"（解析 {unitRateNew * 100f:F1}%）｜影子预测 {r2.predHits}/{SHOTS_UNIT}｜逐发一致 {r2.match}/{SHOTS_UNIT}"
          + $"\n[K2·实测位串] {r2.bits}\n[K2·预测位串] {r2.predBits}");

        // ══ K3 · 单位命中率 · 改前（运行期 1.5 · 同一 seed 基 ⇒ 同一批方向） ══
        rtCfg.projectileErrorRadius = OLD_ERR;
        var r3 = new ArmResult();
        yield return UnitArm(SHOTS_UNIT_OLD, 7000, foe, shooterU, uPos, sUPos, profile, OLD_ERR, hitRadiusWorld, r3);
        L($"[K3·单位·改前({F(OLD_ERR)})] 命中 {r3.hits}/{SHOTS_UNIT_OLD} = {100f * r3.hits / SHOTS_UNIT_OLD:F2}%"
          + $"（解析 {unitRateOld * 100f:F2}%）｜影子预测 {r3.predHits}/{SHOTS_UNIT_OLD}｜逐发一致 {r3.match}/{SHOTS_UNIT_OLD}"
          + $"\n[K3·实测位串] {r3.bits}\n[K3·预测位串] {r3.predBits}");

        // ══ K4 · 建筑命中率 · 改后（资产 0.25） ══
        rtCfg.projectileErrorRadius = assetErr;
        var r4 = new ArmResult();
        yield return BldArm(SHOTS_BLD, 8000, bld, shooterB, bCenter, sBPos, profile, assetErr, fp, cs, r4);
        L($"[K4·建筑·改后({F(assetErr)})] 命中 {r4.hits}/{SHOTS_BLD} = {100f * r4.hits / SHOTS_BLD:F1}%"
          + $"（解析 {bldRateNew * 100f:F1}%）｜影子预测 {r4.predHits}/{SHOTS_BLD}｜逐发一致 {r4.match}/{SHOTS_BLD}"
          + $"\n[K4·实测位串] {r4.bits}\n[K4·预测位串] {r4.predBits}");

        // ══ K5 · 建筑命中率 · 改前（运行期 1.5） ══
        rtCfg.projectileErrorRadius = OLD_ERR;
        var r5 = new ArmResult();
        yield return BldArm(SHOTS_BLD_OLD, 8000, bld, shooterB, bCenter, sBPos, profile, OLD_ERR, fp, cs, r5);
        L($"[K5·建筑·改前({F(OLD_ERR)})] 命中 {r5.hits}/{SHOTS_BLD_OLD} = {100f * r5.hits / SHOTS_BLD_OLD:F1}%"
          + $"（解析 {bldRateOld * 100f:F1}%）｜影子预测 {r5.predHits}/{SHOTS_BLD_OLD}｜逐发一致 {r5.match}/{SHOTS_BLD_OLD}"
          + $"\n[K5·实测位串] {r5.bits}\n[K5·预测位串] {r5.predBits}");

        // ══ 还原散布档（探针收尾纪律 · ⛔ 资产未被写）══
        rtCfg.projectileErrorRadius = savedErr;
        L($"[还原] projectileErrorRadius 已还原为 {F(rtCfg.projectileErrorRadius)}（＝资产值）");

        // ══ A5 · 平衡面（改前/改后 对照）══
        L($"[A5·平衡面·单发命中率] 单位：改前 {100f * r3.hits / SHOTS_UNIT_OLD:F2}% → 改后 {100f * r2.hits / SHOTS_UNIT:F1}%"
          + $" ⇒ 提升 {(r2.hits / (float)SHOTS_UNIT) / Mathf.Max(1e-6f, r3.hits / (float)SHOTS_UNIT_OLD):F1}×（解析 {unitRateNew / unitRateOld:F1}×）");
        L($"[A5·平衡面·单发命中率] 2×2 建筑：改前 {100f * r5.hits / SHOTS_BLD_OLD:F1}% → 改后 {100f * r4.hits / SHOTS_BLD:F1}%"
          + $" ⇒ 提升 {(r4.hits / (float)SHOTS_BLD) / Mathf.Max(1e-6f, r5.hits / (float)SHOTS_BLD_OLD):F1}×（解析 {bldRateNew / bldRateOld:F1}×）");
        L($"[A5·注] 远程对**单位**的单发有效杀伤率由 ≈{unitRateOld * 100f:F2}% 抬到 ≈{unitRateNew * 100f:F1}%"
          + $" ⇒ 同 CD/同命中伤害下，远程对单位 DPS 同比 {unitRateNew / unitRateOld:F1}×（⛔ 非「无影响」· 属实质平衡变更）");

        // ══ 清理 ══
        if (bld != null) { grid.Free(A); BuildingRegistry.Instance?.Unregister(bld); Object.DestroyImmediate(bld.gameObject); }
        foreach (var u in new[] { foe, shooterU, shooterB })
            if (u != null) { UnitRegistry.Instance?.Unregister(u); Object.DestroyImmediate(u.gameObject); }
        yield return null;

        TestHarnessApi.ExitTestRun();
        L("[收尾] ExitTestRun 已执行（Play 由执行端外部 stop · ⛔ 不留余留世界）");
        Flush("spread");
    }

    class ArmResult { public int hits; public int predHits; public int match; public string bits; public string predBits; }

    /// <summary>
    /// ⭐ **影子复算**：用同 seed 的独立 `System.Random` 复现生产 `CombatRules.NextSpreadOffset` 的**前两抽**
    /// （`r=R·√u, θ=2πv`）⇒ 预测本发落点偏移。生产侧 `ResetCombatRandom(同 seed)` 后**首两抽同值**
    /// ⇒ 预测位串与实际位串应**逐发一致**（不一致即「落点 ≠ 目标位 ＋ 偏移」或命中判据另有分支）。
    /// </summary>
    static Vector2 PredictOffset(int seed, float radius)
    {
        var rng = new System.Random(seed);
        double u = rng.NextDouble(), v = rng.NextDouble();
        float rad = (float)System.Math.Sqrt(u) * radius;
        float ang = (float)(v * System.Math.PI * 2.0);
        return new Vector2(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad);
    }

    /// <summary>单位臂：SHOTS 发，逐发 重置随机种子 → 开火 → 撤注册 → **每帧重钉** → 等到达 → 记 HP 变化。
    /// ⭐ 同发影子预测位串（几何：|偏移| ≤ 命中半径）用于**逐发对拍**。</summary>
    static IEnumerator UnitArm(int shots, int seedBase, UnitController foe, UnitController shooter,
                               Vector2 pinPos, Vector2 shooterPin, AttackProfile profile,
                               float cfgErr, float hitRadiusWorld, ArmResult outR)
    {
        var bits = new StringBuilder();
        var predBits = new StringBuilder();
        int hits = 0, predHits = 0, match = 0;
        for (int i = 0; i < shots; i++)
        {
            if (foe == null || shooter == null) break;
            MoveUnit(foe, pinPos); MoveUnit(shooter, shooterPin);           // Pin（⛔ 构造：消 AI 漂移）
            foe.Heal(foe.MaxHp);
            int h0 = foe.CurrentHp;
            bool pred = PredictOffset(seedBase + i, cfgErr).magnitude <= hitRadiusWorld + 1e-6f;
            CombatRules.ResetCombatRandom(seedBase + i);                    // ⭐ 生产唯一重置口（定点复现）
            DamageSystem.Instance.RegisterAttack(shooter, foe, profile);    // ⭐ 生产链入口
            DamageSystem.Instance.Unregister(shooter);                      // 防时间轮按 CD 自动补射
            int guard = 0;
            while (guard < 4)
            {
                MoveUnit(foe, pinPos); MoveUnit(shooter, shooterPin);        // ⭐ 每帧重钉（15× 单帧≈1s 游戏时间）
                if (foe.CurrentHp != h0) break;
                yield return null; guard++;
            }
            MoveUnit(foe, pinPos); MoveUnit(shooter, shooterPin);
            bool hit = foe.CurrentHp < h0;
            bits.Append(hit ? '1' : '0'); predBits.Append(pred ? '1' : '0');
            if (hit) hits++;
            if (pred) predHits++;
            if (hit == pred) match++;
            yield return null;
        }
        outR.hits = hits; outR.predHits = predHits; outR.match = match;
        outR.bits = bits.ToString(); outR.predBits = predBits.ToString();
    }

    /// <summary>建筑臂：同上（靶为多格建筑 · `hp` 公开字段复位）；预测判据 ＝ `CombatRules.InDiamondBase(中心＋偏移)`。</summary>
    static IEnumerator BldArm(int shots, int seedBase, Building bld, UnitController shooter,
                              Vector2 pinPos, Vector2 shooterPin, AttackProfile profile,
                              float cfgErr, Vector2Int fp, Vector2 cs, ArmResult outR)
    {
        var bits = new StringBuilder();
        var predBits = new StringBuilder();
        int hits = 0, predHits = 0, match = 0;
        for (int i = 0; i < shots; i++)
        {
            if (bld == null || shooter == null) break;
            MoveUnit(shooter, shooterPin);                                  // Pin（⛔ 构造）
            bld.hp = bld.maxHp;                                             // ⛔ 复位（保活）
            int h0 = bld.CurrentHp;
            bool pred = CombatRules.InDiamondBase(pinPos + PredictOffset(seedBase + i, cfgErr), pinPos, fp, cs);
            CombatRules.ResetCombatRandom(seedBase + i);
            DamageSystem.Instance.RegisterAttack(shooter, bld, profile);
            DamageSystem.Instance.Unregister(shooter);
            int guard = 0;
            while (guard < 4)
            {
                MoveUnit(shooter, shooterPin);
                if (bld.CurrentHp != h0) break;
                yield return null; guard++;
            }
            bool hit = bld.CurrentHp < h0;
            bits.Append(hit ? '1' : '0'); predBits.Append(pred ? '1' : '0');
            if (hit) hits++;
            if (pred) predHits++;
            if (hit == pred) match++;
            yield return null;
        }
        outR.hits = hits; outR.predHits = predHits; outR.match = match;
        outR.bits = bits.ToString(); outR.predBits = predBits.ToString();
    }

    class SampleRes { public string serialized; public int distinct; public int zeros; public float rMin, rMax, rMean; }

    /// <summary>用生产唯一口 `CombatRules.NextSpreadOffset` 抽样（**紧环无让帧** ⇒ 流不被世界消费 ⇒ 可复现）。</summary>
    static SampleRes SampleOffsets(int seed, float radius, int n)
    {
        CombatRules.ResetCombatRandom(seed);
        var sb = new StringBuilder();
        var set = new HashSet<string>();
        float rMin = float.MaxValue, rMax = 0f, rSum = 0f; int zeros = 0;
        for (int i = 0; i < n; i++)
        {
            Vector2 o = CombatRules.NextSpreadOffset(radius);
            sb.Append(F(o.x)).Append(',').Append(F(o.y)).Append(';');
            set.Add(F(o.x) + "," + F(o.y));
            float r = o.magnitude;
            if (r < rMin) rMin = r;
            if (r > rMax) rMax = r;
            rSum += r;
            if (r <= 0f) zeros++;
        }
        return new SampleRes { serialized = sb.ToString(), distinct = set.Count, zeros = zeros, rMin = rMin, rMax = rMax, rMean = rSum / n };
    }
}
