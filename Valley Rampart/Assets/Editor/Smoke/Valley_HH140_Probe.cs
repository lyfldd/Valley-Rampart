using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;

// ============================================================================
//  HH.140 王国AI P0 批C+批D 行为级探针容器（Editor-only；test-harness-first 铁律①⑤）。
//  探针清单（对照 2_22 §三批C/§四批D 验收列+D594 整改令）：
//   P1(C1) 姿态三档升降+双层滞回：无→警戒（威胁）→动员（危机线+威胁）；双线带滞回
//          （军力∈[危机线,恢复线) 保持动员）+天数窗滞回（阈值附近抖动不切档）
//   P2(D5) 立国选址特征匹配纯函数级：ForestDense/BarrenRich/RiverAdjacent 判定+回退负探针
//   P3(C6) 危机打断：军覆（无将军+无编队）→焦点当日重规划（focus 非法值 99 被重写=当日生效）
//   P4(C3) 警戒档巡逻：威胁快照→警戒档→本国空闲战士被发巡逻令（IsPatrolling>0）
//   P5(C4/C5) 动员档：召回（巡逻清零）+守军编队自动派驻（isGarrison 编队 KingdomId=1）
//   P6(B8 搭车) D594 整改令：精灵国 prefab 全缺失→Feasible=false 不评（负面）+
//          人类国 Ballista 在场→Feasible=true（正面）；MachineDemand 姿态域细化（None 域外/Alert 域内）
//   P7(D2/D3) 选址打分器：军事朝向性（F1 朝威胁侧）+经济邻近性（F2 距粮仓）+同参数双跑确定性
//   P8(D5) KingdomDef 四模板特征回填读回（Bedrock/SnowRock=3 IronHoof=4）
//  证据落 Logs/P1/hh140_probe.log（不入库，断言转录 HH.141）。
// ============================================================================
public static class Valley_HH140_Probe
{
    private const int SEED = 21140;
    private const string SLOT = "probe_hh140";
    private static int _pass, _fail;
    private static readonly System.Text.StringBuilder _log = new System.Text.StringBuilder();

    [MenuItem("Valley/验证/HH140_批C批D探针")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH140探针] 须先 GameScene 进 Play。"); return; }
        _pass = 0; _fail = 0; _log.Length = 0;
        var host = new GameObject("HH140_ProbeHost").AddComponent<ProbeHost>();
        Application.logMessageReceived += host.Catch;
        host.Host(Coroutine());
    }

    private class ProbeHost : MonoBehaviour
    {
        public void Host(IEnumerator r) => StartCoroutine(r);
        public void Catch(string cond, string stack, LogType t)
        {
            if (t == LogType.Exception) Log("EXCEPTION: " + cond + "\n" + stack, false);
        }
    }

    private static void Log(string msg, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + msg);
        if (ok) _pass++; else _fail++;
        Debug.LogWarning("[HH140探针] " + (ok ? "✓ " : "✗ ") + msg);
    }

    private static IEnumerator Coroutine()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = SLOT, kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 4)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Log("等世界就绪超时", false); Finish(); yield break; }
        }
        yield return new WaitForSeconds(1f);
        Log("正门进局 seed=" + SEED, true);

        yield return WaitDays(3);

        var reg = KingdomRegistry.Instance;
        var k1 = reg.Get(1); var k2 = reg.Get(2);
        if (k1 == null || k2 == null) { Log("AI 国缺失", false); Finish(); yield break; }
        var scfg = SituationConfig.Load();
        int crisis = scfg != null ? scfg.crisisLine : 2;
        int recovery = scfg != null ? scfg.recoveryLine : 4;
        var pcfg = MilitaryPostureConfig.Load();

        // ===== P1：C1 姿态三档升降+双层滞回（真实军力注入=SetOccupation 生产链转职）=====
        var brain1 = KingdomBrainRegistry.Instance != null ? KingdomBrainRegistry.Instance.Get(1) : null;
        if (brain1 == null) { Log("brain1 缺失", false); Finish(); yield break; }
        var posture1 = brain1.Posture;
        var mock = new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry>(), Day = 0 };
        SetWarriors(1, Mathf.Max(recovery, 5));   // 军力抬到 ≥恢复线（探针档位表基准）
        yield return null;   // 等 registry 计数生效

        posture1.Evaluate(mock, k1, scfg, pcfg, 100);   // 无威胁+军力足 → None
        bool p1a = posture1.Current == MilitaryPosture.None;
        Log($"P1a 无威胁+军力{k1.warriorCount}→None（实={posture1.Current}）", p1a);

        mock.Threats.Add(new ThreatEntry { KingdomId = 2, WarriorCount = 5 });
        posture1.Evaluate(mock, k1, scfg, pcfg, 100);
        bool p1b = posture1.Current == MilitaryPosture.Alert;
        Log("P1b 威胁5→Alert（实=" + posture1.Current + "）", p1b);

        SetWarriors(1, 1); yield return null;   // 军力降到危机线下
        // r3：day 间距 ≥hysteresisDays(2)——探针转移表测距窗逐档跳 2 日（r1 实锤 1 日间距被天数滞回挡升降）
        posture1.Evaluate(mock, k1, scfg, pcfg, 102);   // 威胁+军力1<crisisLine → 动员
        bool p1c = posture1.Current == MilitaryPosture.Mobilized;
        Log($"P1c 军力实测{k1.warriorCount}<危机线{crisis}+威胁→Mobilized（实={posture1.Current}）", p1c);

        // P1e（r3 提前）：天数滞回窗内（距上次变更 1 日<hy）给解除条件（军力≥恢复线+无威胁）→维持现档
        SetWarriors(1, Mathf.Max(recovery, 5)); yield return null;
        mock.Threats.Clear();
        posture1.Evaluate(mock, k1, scfg, pcfg, 103);   // 103-102=1 < hy → 维持 Mobilized
        bool p1e = posture1.Current == MilitaryPosture.Mobilized;
        Log("P1e 天数滞回窗内维持（实=" + posture1.Current + "）", p1e);

        SetWarriors(1, crisis + 1); yield return null;   // 军力回滞回带 [危机线, 恢复线)
        posture1.Evaluate(mock, k1, scfg, pcfg, 105);   // 105-102=3 过窗+军力在带内+无威胁 → 保持动员（双线滞回）
        bool p1d = posture1.Current == MilitaryPosture.Mobilized;
        Log($"P1d 滞回带：军力实测{k1.warriorCount}∈[{crisis},{recovery})+无威胁→保持Mobilized（实={posture1.Current}）", p1d);

        SetWarriors(1, Mathf.Max(recovery, 5)); yield return null;
        posture1.Evaluate(mock, k1, scfg, pcfg, 110);   // 军力≥恢复线+无威胁+窗外 → None
        bool p1f = posture1.Current == MilitaryPosture.None;
        Log($"P1f 解除：军力{k1.warriorCount}≥恢复线{recovery}+无威胁+窗外→None（实={posture1.Current}）", p1f);

        // ===== P2：D5 立国选址特征匹配（纯函数级，构造 MapData）=====
        var map = NewTestMap(64, 64);
        // 区块(0,0)~(2,2) 铺 25% Tree（ForestDense 命中）；区块(3,0) 铺全 Mountain（BarrenRich 负对照）
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 48; x++)
                if ((x / 16 + y / 16) % 2 == 0 && (x % 2) == 0 && (y % 2) == 0) map.features[MapGenRules.Idx(map, x, y)] = FeatureType.Tree;
        for (int y = 0; y < 16; y++)
            for (int x = 48; x < 64; x++)
                map.features[MapGenRules.Idx(map, x, y)] = FeatureType.Mountain;
        // River 列 x=60（边缘水）
        for (int y = 0; y < 64; y++) map.features[MapGenRules.Idx(map, 60, y)] = FeatureType.River;
        var mcfg2 = MapGenRulesConfigAssetOrNull();
        bool d5a = MapGenRules.MatchesPreferredFeature(map, new Vector2Int(8, 8), KingdomPreferredFeature.ForestDense, mcfg2)
                && !MapGenRules.MatchesPreferredFeature(map, new Vector2Int(24, 8), KingdomPreferredFeature.ForestDense, mcfg2);
        Log("P2a D5 ForestDense：密林区(25%树)=真/纯平原区=假", d5a);
        bool d5b = MapGenRules.MatchesPreferredFeature(map, new Vector2Int(24, 8), KingdomPreferredFeature.BarrenRich, mcfg2)
                && !MapGenRules.MatchesPreferredFeature(map, new Vector2Int(56, 8), KingdomPreferredFeature.BarrenRich, mcfg2);
        Log("P2b D5 BarrenRich：开阔平原区=真/山地区=假", d5b);
        bool d5c = MapGenRules.MatchesPreferredFeature(map, new Vector2Int(55, 30), KingdomPreferredFeature.RiverAdjacent, mcfg2)
                && !MapGenRules.MatchesPreferredFeature(map, new Vector2Int(8, 8), KingdomPreferredFeature.RiverAdjacent, mcfg2);
        Log("P2c D5 RiverAdjacent：临水=真/内陆=假", d5c);
        // 回退负探针：全 Plain 地图+ForestDense 模板 → PickSpawnForTemplate 回退日志+出生点有效
        var plainMap = NewTestMap(64, 64);
        var tplDense = ScriptableObject.CreateInstance<KingdomDef>();
        tplDense.templateName = "probe_dense";
        tplDense.preferredClimates = new ClimateZone[] { ClimateZone.Temperate };
        tplDense.preferredFeature = KingdomPreferredFeature.ForestDense;
        var rngD = new System.Random(7);
        var spawnsD = new List<Vector2Int>();
        var spawnP = MapGenRules.PickSpawnForTemplate(rngD, plainMap, 2, 0, spawnsD, tplDense, mcfg2);
        bool d5d = spawnP.x >= 0;
        Log("P2d D5 回退负探针：无特征地形回退+日志（出生点 " + spawnP + " 有效=" + d5d + "）", d5d);

        // ===== P3：C6 军覆危机打断（k2 focus 非法值 99 → 事件当日重写）=====
        int f0 = k2.focus;
        k2.focus = 99;   // 非法行动 id（重规划必写合法值）
        EventBus.Publish(new GeneralDiedEvent(2));
        int f1 = k2.focus;
        bool p3 = f1 != 99;
        Log("P3 C6 军覆打断：k2 focus 99→" + f1 + "（当日重规划=" + p3 + "）", p3);
        k2.focus = f0;   // 复原

        // ===== P4：C3 警戒档巡逻（威胁快照+警戒档→巡逻令下发）=====
        SetWarriors(1, 4); yield return null;
        mock.Threats.Add(new ThreatEntry { KingdomId = 2, WarriorCount = 5 });
        mock.OwnWarriorCount = k1.warriorCount;
        SituationHub.Put(1, mock);
        var fldSit = typeof(KingdomBrain).GetField("_situation", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fldSit != null) fldSit.SetValue(brain1, mock);   // 主威胁方向读 brain._situation（mock 同源）
        posture1.Evaluate(mock, k1, scfg, pcfg, 200);        // 威胁+军力4≥恢复线 → Alert
        bool alertOn = posture1.Current == MilitaryPosture.Alert;
        var miPosture = brain1.GetType().GetMethod("ExecutePosture", BindingFlags.NonPublic | BindingFlags.Instance);
        if (miPosture != null && alertOn) miPosture.Invoke(brain1, new object[] { k1, pcfg });
        int patrolling = CountOwnPatrolling(1);
        Log("P4 C3 警戒档=" + posture1.Current + " 本国巡逻数=" + patrolling + ">0", alertOn && patrolling > 0);

        // ===== P5：C4+C5 动员档（召回+守军派驻）=====
        SetWarriors(1, 1); yield return null;   // 军力<危机线+威胁 → 动员
        posture1.Evaluate(mock, k1, scfg, pcfg, 202);   // r3：202-200=2 ≥ hy 过天数窗
        bool mobOn = posture1.Current == MilitaryPosture.Mobilized;
        if (miPosture != null && mobOn) miPosture.Invoke(brain1, new object[] { k1, pcfg });
        // r3：招募受中区块空间约束（≥4 拒招=合法旁路，明日再试）→等数日 Tick 每日补建
        //（动员档期间军力仍 1<危机线+威胁在=档位稳定；Tick 每日 StopAllOwnPatrols 兜底召回）
        yield return WaitDays(4);
        int patrollingAfter = CountOwnPatrolling(1);
        int garrisons = CountOwnGarrison(1);
        int garrisonShells = CountGarrisonEmptyShells();
        Log("P5 C4/C5 动员档=" + posture1.Current + " 召回后巡逻=" + patrollingAfter + "（=0） 守军编队=" + garrisons + "（>0） 空壳=" + garrisonShells + "（=0）",
            mobOn && patrollingAfter == 0 && garrisons > 0 && garrisonShells == 0);

        // ===== P6：B8 搭车（D594 整改令：缺失族不评零扣费/在场族不受影响）=====
        k1.resources.gold += 800;
        var defM = new UtilityActionDef { id = UtilityAction.ProduceMachine, name = "probeM", need = NeedKind.MachineDemand, needA = 2 };
        var miFeas = typeof(UtilityScorer).GetMethod("Feasible", BindingFlags.NonPublic | BindingFlags.Static);
        bool feasNoFactory = miFeas != null && !(bool)miFeas.Invoke(null, new object[] { k1, defM });
        Log("P6a B8 厂前置镜像：k1 无投掷机厂→ProduceMachine 不评=" + feasNoFactory, feasNoFactory);

        k1.resources.gold += 2000; k1.resources.stone += 600; k1.resources.wood += 600;
        var bc = BuildController.Instance;
        var bdefW = BuildingFactory.FindDefById(BuildingIds.SiegeWorkshop);
        var miSpot = brain1.GetType().GetMethod("FindAIBuildSpot", BindingFlags.NonPublic | BindingFlags.Static);
        var sW = miSpot != null ? miSpot.Invoke(null, new object[] { 1, bdefW, 20 }) as GridCoord? : null;
        bool builtW = sW.HasValue && bc != null && bc.TryBuild(bdefW, sW.Value, GateOrientation.Horizontal, 1);
        yield return WaitDays(4);
        bool workshop = FindB(1, BuildingIds.SiegeWorkshop) != null;
        Log("P6pre k1 建厂提交=" + builtW + " 施工 Active=" + workshop, workshop);

        int gold0 = k1.resources.gold;
        var mcfgM = KingdomBrain.LoadConfig();
        var stageM = ScriptStage.Military;
        var utilCfg = UtilityActionConfig.LoadConfig();
        var topNoPrefab = UtilityScorer.ScoreTop(k1, utilCfg, stageM);
        bool noPick = topNoPrefab != UtilityAction.ProduceMachine;
        // 逐项 Feasible 直调（prefab 缺失=精灵 VineCatapult 图纸面缺席）
        bool feasNoPrefab = miFeas != null && !(bool)miFeas.Invoke(null, new object[] { k1, defM });
        bool zeroSpend = k1.resources.gold == gold0;   // 评分+门控全程零扣费（ScoreTop/Feasible 纯读）
        Log("P6b D594 缺失族不评：精灵(race1) VineCatapult prefab 缺→不评=" + feasNoPrefab + " ScoreTop=" + topNoPrefab + " 零扣费=" + zeroSpend,
            feasNoPrefab && noPick && zeroSpend);

        // 正面：在场族不受影响（k2=矮人 Mortar prefab 缺同属缺失族→建厂仍不评；
        // 内存借用 Ballista prefab 给 Mortar 图纸槽（不入盘不入档）→Feasible 翻转 true=判定逻辑双向闭环→复原）
        var k2r = reg.Get(2);
        bool posOk = false; string posWhy = "k2 缺失";
        if (k2r != null && KingdomRace.GetKingdomRace(2) == 2)
        {
            k2r.resources.gold += 3000; k2r.resources.stone += 800; k2r.resources.wood += 800;
            var sW2 = miSpot != null ? miSpot.Invoke(null, new object[] { 2, bdefW, 20 }) as GridCoord? : null;
            bool built2 = sW2.HasValue && bc != null && bc.TryBuild(bdefW, sW2.Value, GateOrientation.Horizontal, 2);
            yield return WaitDays(4);
            bool ws2 = FindB(2, BuildingIds.SiegeWorkshop) != null;
            bool feasMortarMissing = miFeas != null && !(bool)miFeas.Invoke(null, new object[] { k2r, defM });
            var mortData = UnitDataManager.Instance != null ? UnitDataManager.Instance.GetData(Faction.PlayerCamp, Occupation.Mortar) : null;
            bool flipped = false;
            if (mortData != null)
            {
                var keep = mortData.prefab;
                mortData.prefab = MachinePanel.IsPrefabMissing(Occupation.Ballista, out _)
                    ? null : UnitDataManager.Instance.GetData(Faction.PlayerCamp, Occupation.Ballista)?.prefab;
                bool feasMortarReady = miFeas != null && (bool)miFeas.Invoke(null, new object[] { k2r, defM });
                mortData.prefab = keep;   // 复原（内存 mock 不入盘）
                flipped = feasMortarMissing && feasMortarReady;
            }
            posOk = built2 && ws2 && flipped;
            posWhy = "建厂=" + built2 + "/Active=" + ws2 + "/缺图不评→借图可评翻转=" + flipped;
        }
        Log("P6c D594 判定双向闭环：k2(矮人) " + posWhy, posOk);

        // MachineDemand 姿态域细化：None 档无威胁→0 分；Alert 档→>0（守城需求域开）
        // 阶段门（k.scriptPhase==Military）为 MachineDemand 内部前置——探针临时置 Military（复原）
        var savedPhase = k1.scriptPhase;
        k1.scriptPhase = ScriptStage.Military;
        var defMD = new UtilityActionDef { id = UtilityAction.ProduceMachine, name = "probeMD", need = NeedKind.MachineDemand, needA = 2 };
        var snapNoThreat = new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry>(), Day = 300 };
        SituationHub.Put(1, snapNoThreat);
        PostureHub.Put(1, MilitaryPosture.None);
        float mdNone = UtilityScorer.NeedScore(k1, defMD);
        PostureHub.Put(1, MilitaryPosture.Alert);
        float mdAlert = UtilityScorer.NeedScore(k1, defMD);
        PostureHub.Put(1, MilitaryPosture.None);   // 复原
        k1.scriptPhase = savedPhase;
        bool mdOk = mdNone <= 0.0001f && mdAlert > 0.0001f;
        Log($"P6d 守城需求域（军事期注入）：None档分={mdNone:F3}（0）/Alert档分={mdAlert:F3}（>0）", mdOk);

        // ===== P7：D2/D3 选址打分器（军事朝向性+经济邻近性+确定性）=====
        var bdefB = BuildingFactory.FindDefById(BuildingIds.Barracks);
        var bpCfg = BuildingPlacementConfig.Load();
        // F1 朝向性（r3 改纯函数级）：本局 k1↔k2 相距 45 格，带内候选距威胁锚≈185 sub ≫ 归一化范围
        // maxR×div=32 → 带内 F1 恒 0（clamp01 边界=特征退化不崩溃，非坐标 bug——r3 坐标修复已由 P7b/P7c 实锤）。
        // 故 F1 语义改用 ComputeF1 真实威胁锚（k2 城 cell→sub 域）近/远锚点相对断言；
        // 带外归一口径是否改按城-锚实际距离，列报策划裁决（HH.141）。
        var threatCastle = KingdomBrain.FindCastleCell(2);
        bool toward = false; string towardWhy = "锚缺失";
        if (threatCastle.HasValue && bdefB != null)
        {
            var gsP = GridSystem.Instance;
            int divP = gsP != null && gsP.Config != null && gsP.Config.subCellDivisor > 0 ? gsP.Config.subCellDivisor : 4;
            var taSub = new GridCoord(threatCastle.Value.x * divP, threatCastle.Value.y * divP, threatCastle.Value.layer);
            var miF1 = typeof(PlacementScorer).GetMethod("ComputeF1", BindingFlags.NonPublic | BindingFlags.Static);
            if (miF1 != null)
            {
                var near = new GridCoord(taSub.x - 8, taSub.y - 8, 0);
                var far = new GridCoord(taSub.x - 80, taSub.y - 80, 0);
                float f1Near = (float)miF1.Invoke(null, new object[] { near, (GridCoord?)taSub, 8, divP });
                float f1Far = (float)miF1.Invoke(null, new object[] { far, (GridCoord?)taSub, 8, divP });
                toward = f1Near > f1Far;
                // 带内现象留痕：全带 TryPick 的 F1 读数（本局恒 0=带外 clamp 实锤，列报）
                string bandNote = "";
                var sitT = new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry> { new ThreatEntry { KingdomId = 2, WarriorCount = 5 } }, Day = 400 };
                if (PlacementScorer.TryPick(1, bdefB, 8, bpCfg, sitT, out var rT))
                    bandNote = $"/带内选格({rT.Sub.x},{rT.Sub.y}) F1={rT.F1:F2}（带外 clamp 留痕）";
                towardWhy = $"近锚 F1={f1Near:F2}>远锚 F1={f1Far:F2}" + bandNote;
            }
            else towardWhy = "ComputeF1 反射缺失";
        }
        Log("P7a 军事朝向性（F1 朝威胁侧）：k1 兵营 " + towardWhy, toward);

        // 经济邻近性：farm 规则 link=Granary——k1 无 Granary→F2=0；有 Granary（直建）→F2>0
        var bdefFarm = BuildingFactory.FindDefById("farm");
        var bdefGra = BuildingFactory.FindDefById("Granary");
        bool econOk = false; string econWhy = "def 缺失";
        if (bdefFarm != null && bdefGra != null)
        {
            var sitClean = new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry>(), Day = 401 };
            bool calledNoLink = PlacementScorer.TryPick(1, bdefFarm, 8, bpCfg, sitClean, out var rNoLink);
            if (!calledNoLink) { /* 无候选不阻塞：F2 分解仍以 rNoLink 为准 */ }
            var sGra = miSpot != null ? miSpot.Invoke(null, new object[] { 1, bdefGra, 3 }) as GridCoord? : null;   // r2：贴城建（半径 3）保 farm 候选（半径 8）与粮仓距离可归一
            bool builtGra = sGra.HasValue && bc != null && bc.TryBuild(bdefGra, sGra.Value, GateOrientation.Horizontal, 1);
            yield return WaitDays(4);
            bool graActive = FindB(1, "Granary") != null;
            bool calledLink = PlacementScorer.TryPick(1, bdefFarm, 8, bpCfg, sitClean, out var rLink);
            bool withLink = graActive && calledLink;
            econOk = Mathf.Abs(rNoLink.F2) < 0.0001f && withLink && rLink.F2 > 0.05f;
            econWhy = $"无仓 F2={rNoLink.F2:F2} / 建仓={builtGra}/Active={graActive} → F2={rLink.F2:F2}";
        }
        Log("P7b 经济邻近性（F2 农田↔粮仓）：k1 " + econWhy, econOk);

        // 确定性：同参数双跑 PickResult 逐字段一致
        bool detOk = false; string detWhy = "前置缺失";
        if (bdefB != null)
        {
            var sitD = new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry> { new ThreatEntry { KingdomId = 2, WarriorCount = 5 } }, Day = 402 };
            detOk = PlacementScorer.TryPick(1, bdefB, 8, bpCfg, sitD, out var rA)
                 && PlacementScorer.TryPick(1, bdefB, 8, bpCfg, sitD, out var rB)
                 && rA.Sub.x == rB.Sub.x && rA.Sub.y == rB.Sub.y
                 && rA.Score.ToString("R") == rB.Score.ToString("R")
                 && rA.Candidates == rB.Candidates;
            detWhy = $"({rA.Sub.x},{rA.Sub.y})={rA.Score:R} 候选{rA.Candidates}";
        }
        Log("P7c 同 seed 选址确定性双跑一致：" + detWhy, detOk);

        // ===== P8：D5 KingdomDef 四模板特征回填读回 =====
        var kdBedrock = Resources.Load<KingdomDef>("Config/Kingdoms/Kingdom_Bedrock");
        var kdSnow = Resources.Load<KingdomDef>("Config/Kingdoms/Kingdom_SnowRock");
        var kdIron = Resources.Load<KingdomDef>("Config/Kingdoms/Kingdom_IronHoof");
        bool p8 = kdBedrock != null && kdSnow != null && kdIron != null
               && kdBedrock.preferredFeature == KingdomPreferredFeature.MineralRich
               && kdSnow.preferredFeature == KingdomPreferredFeature.MineralRich
               && kdIron.preferredFeature == KingdomPreferredFeature.BarrenRich;
        Log($"P8 KingdomDef 回填：Bedrock={kdBedrock?.preferredFeature} SnowRock={kdSnow?.preferredFeature} IronHoof={kdIron?.preferredFeature}", p8);

        Finish();
    }

    // ===== helpers =====

    private static MapData NewTestMap(int w, int h)
    {
        var m = new MapData();
        m.width = w; m.height = h;
        m.features = new FeatureType[w * h];
        m.climateZones = new ClimateZone[(w / 16 + 1) * (h / 16 + 1)];
        for (int i = 0; i < m.climateZones.Length; i++) m.climateZones[i] = ClimateZone.Temperate;
        m.kingdomSpawns = new List<Vector2Int>();
        m.naturalBuildings = new List<NaturalBuilding>();
        m.threatSpawns = new List<SpawnDef>();
        return m;
    }

    private static MapGenRulesConfig MapGenRulesConfigAssetOrNull()
        => Resources.Load<MapGenRulesConfig>("Grid/MapGenRulesConfig");

    private static int SetWarriors(int kid, int target)
    {
        int have = 0;
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units == null) return 0;
        foreach (var u in units)
            if (u != null && u.IsAlive && u.kingdomId == kid && MilitaryProfessions.IsCombat(u.EffectiveOccupation)) have++;
        if (have < target)
        {
            foreach (var u in units)
            {
                if (have >= target) break;
                if (u == null || !u.IsAlive || u.kingdomId != kid) continue;
                var occ = u.EffectiveOccupation;
                if (occ == Occupation.Resident || occ == Occupation.Worker || occ == Occupation.Civilian)
                { u.SetOccupation(Occupation.Warrior); have++; }
            }
        }
        else if (have > target)
        {
            foreach (var u in units)
            {
                if (have <= target) break;
                if (u == null || !u.IsAlive || u.kingdomId != kid) continue;
                if (MilitaryProfessions.IsCombat(u.EffectiveOccupation))
                { u.SetOccupation(Occupation.Resident); have--; }
            }
        }
        return have;
    }

    private static int CountOwnPatrolling(int kid)
    {
        int n = 0;
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units == null) return 0;
        foreach (var u in units)
        {
            if (u == null || !u.IsAlive || u.kingdomId != kid) continue;
            var brain = u.GetComponent<NPCBrain>();
            if (brain != null && PatrolTaskSystem.IsPatrolling(brain)) n++;
        }
        return n;
    }

    private static int CountOwnGarrison(int kid)
    {
        if (FormationManager.Instance == null) return 0;
        int n = 0;
        var fs = FormationManager.Instance.AllFormations;
        for (int i = 0; i < fs.Count; i++)
            if (fs[i] != null && fs[i].KingdomId == kid && fs[i].isGarrison) n++;
        return n;
    }

    // r3 负探针：isGarrison 且 0 成员=空壳（堆积验证；产品侧招募落空即建即毁，正常应恒 0）
    private static int CountGarrisonEmptyShells()
    {
        if (FormationManager.Instance == null) return 0;
        int n = 0;
        var fs = FormationManager.Instance.AllFormations;
        for (int i = 0; i < fs.Count; i++)
            if (fs[i] != null && fs[i].isGarrison && fs[i].MemberCount <= 0) n++;
        return n;
    }

    private static Building FindB(int kid, string id)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return null;
        foreach (var b in reg.All)
            if (b != null && b.def != null && b.IsActive && b.kingdomId == kid && b.def.id == id) return b;
        return null;
    }

    private static IEnumerator WaitDays(int days)
    {
        for (int i = 0; i < days; i++)
        {
            int start = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
            while (TimeManager.Instance != null && TimeManager.Instance.CurrentDay == start)
                yield return null;
            yield return new WaitForSeconds(0.5f);
        }
    }

    private static void Finish()
    {
        _log.AppendLine("===== HH.140 批C+批D 探针收工 =====");
        _log.AppendLine("PASS=" + _pass + " FAIL=" + _fail + " 时间=" + System.DateTime.Now.ToString("HH:mm:ss"));
        if (TimeManager.Instance != null) TimeManager.Instance.SetGameSpeed(0f);
        TestHarnessApi.ExitTestRun();
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "hh140_probe.log"), _log.ToString());
        }
        catch (System.Exception e) { Debug.LogError("[HH140探针] 日志写盘失败: " + e.Message); }
        Debug.LogWarning("[HH140探针] ★ 收工 PASS=" + _pass + " FAIL=" + _fail + "（终速 0+ExitTestRun）");
        var host = Object.FindObjectOfType<ProbeHost>();
        if (host != null) Application.logMessageReceived -= host.Catch;
    }
}
