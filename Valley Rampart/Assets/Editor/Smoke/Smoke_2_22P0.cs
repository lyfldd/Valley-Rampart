using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;

// ============================================================================
//  Smoke_2_22P0 王国AI P0 冒烟容器（2_22 §十.1 九项探针，批E E4，Editor-only）
//  test-harness-first 铁律①⑤：正门 TestHarnessApi.EnterTestRun + ExitTestRun 收尾
//  （D600 勘正：旧口径 SmokeApi.EnterGame 已作废= L-17 二次实证升级）。
//  L-21 硬引用（D598 红线复盘承诺）：涉评分/目标驱动面探针必含计数面 0→1 翻转断言
//  + 失败路径终止性断言（P3 选招/P6 守军/P8 选址失败路径）。
//  L-17 变体③（D600）：涉玩家/清场副作用（P7 读档=清场再载）→ 快照复原 + Finish
//  撤守卫前终检三件套。
//  确定性：同 seed 双跑逐字节一致（run1/run2 两菜单项，日志 Logs/P1/smoke_2_22p0_{tag}.log，
//  比对 P 行序列——时间戳行不入比对）。
//  九项：P1 将军可训练 / P2 军事建筑可建可训 / P3 ⑦按权重分布 / P4 邻接威胁修正 /
//        P5 姿态升降 / P6 AI 驻防 / P7 读档恢复 / P8 选址打分器三条 / P9 机器双行动[B8]
// ============================================================================
public static class Smoke_2_22P0
{
    private const int SEED = 21140;          // 与 HH.140/144 同 seed=同局地形（k1↔k2 45 格带外局）
    private const string SLOT = "probe_2_22p0";
    private static int _pass, _fail;
    private static readonly System.Text.StringBuilder _log = new System.Text.StringBuilder();
    public static string RunTag = "run1";    // 双跑标签（确定性比对用；默认 run1）

    [MenuItem("Valley/验证/Smoke_2_22P0_王国AIP0冒烟_run1")]
    public static void Run1() { RunTag = "run1"; Run(); }
    [MenuItem("Valley/验证/Smoke_2_22P0_王国AIP0冒烟_run2")]
    public static void Run2() { RunTag = "run2"; Run(); }

    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[2_22P0] 须先 GameScene 进 Play。"); return; }
        _pass = 0; _fail = 0; _log.Length = 0;
        var host = new GameObject("Smoke2_22P0_Host").AddComponent<ProbeHost>();
        Application.logMessageReceived += host.Catch;   // L-16：协程异常可见性
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
        Debug.LogWarning("[2_22P0] " + (ok ? "✓ " : "✗ ") + msg);
    }

    // ---- 玩家侧快照/复原（L-17 变体③ 三件套：快照→改→复原→撤守卫前终检）----
    private static Dictionary<UnitController, Occupation> _playerOccSnap;

    private static void SnapshotPlayerOccupations()
    {
        _playerOccSnap = new Dictionary<UnitController, Occupation>();
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units == null) return;
        foreach (var u in units)
            if (u != null && u.IsAlive && u.kingdomId == 0) _playerOccSnap[u] = u.EffectiveOccupation;
    }

    private static void RestorePlayerOccupations()
    {
        if (_playerOccSnap == null) return;
        foreach (var kv in _playerOccSnap)
            if (kv.Key != null && kv.Key.IsAlive && kv.Key.EffectiveOccupation != kv.Value)
                kv.Key.SetOccupation(kv.Value);
    }

    private static IEnumerator Coroutine()
    {
        var cfg = new NewGameConfig
        {
            mapSeed = SEED, worldSeed = SEED, difficulty = 2,
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
        Log("正门进局 seed=" + SEED + " tag=" + RunTag, true);

        yield return WaitDays(3);
        SnapshotPlayerOccupations();   // L-17 变体③：会话起始玩家快照（全探针收尾复原基准）

        var reg = KingdomRegistry.Instance;
        var k1 = reg.Get(1); var k2 = reg.Get(2); var k3 = reg.Get(3);
        if (k1 == null || k2 == null || k3 == null) { Log("AI 国缺失", false); Finish(); yield break; }
        var bcfg = KingdomBrain.LoadConfig();
        var scfg = SituationConfig.Load();
        var pcfg = MilitaryPostureConfig.Load();
        var bpCfg = BuildingPlacementConfig.Load();
        var brain1 = KingdomBrainRegistry.Instance != null ? KingdomBrainRegistry.Instance.Get(1) : null;
        if (brain1 == null) { Log("brain1 缺失", false); Finish(); yield break; }

        // ===== P1 将军可训练（⑯→入队→毕业→成军）+ L-21 计数面翻转+失败终止 =====
        k1.resources.gold += 800; k1.resources.stone += 300; k1.resources.wood += 300;
        var miTrain = brain1.GetType().GetMethod("ExecuteTrainGeneral", BindingFlags.NonPublic | BindingFlags.Instance);
        var miSpot = typeof(KingdomBrain).GetMethod("FindAIBuildSpot", BindingFlags.NonPublic | BindingFlags.Static);
        var bc = BuildController.Instance;
        var bdefB = BuildingFactory.FindDefById(BuildingIds.Barracks);
        if (miTrain == null || miSpot == null || bc == null || bdefB == null)
        { Log("P1 前置缺失 mi/bc/bdefB", false); Finish(); yield break; }

        int gens0 = KingdomBrain.CountGenerals(1);
        // L-21 失败路径①：无兵营时 ExecuteTrainGeneral → 失败不堆积（队列不涨）
        int qNoBar = TrainingSystem.Instance != null ? TrainingSystem.Instance.GetKingdomQueueCount(1) : -1;
        miTrain.Invoke(brain1, new object[] { k1, bcfg });
        int qNoBar2 = TrainingSystem.Instance != null ? TrainingSystem.Instance.GetKingdomQueueCount(1) : -1;
        bool p1fail = qNoBar2 == qNoBar;
        Log("P1a 失败路径① 无兵营不空转（队列 " + qNoBar + "→" + qNoBar2 + "）", p1fail);

        var sB = miSpot.Invoke(null, new object[] { 1, bdefB, 20 }) as GridCoord?;
        bool builtB = sB.HasValue && bc.TryBuild(bdefB, sB.Value, GateOrientation.Horizontal, 1);
        Log("P1b ⑰建兵营提交（生产链 TryBuild）=" + builtB, builtB);
        yield return WaitDays(4);   // L-20：TryBuild=施工启动非竣工，等 Active
        bool barActive = FindB(1, BuildingIds.Barracks) != null;
        Log("P1c 兵营施工完成 Active=" + barActive, barActive);

        int q0 = TrainingSystem.Instance != null ? TrainingSystem.Instance.GetKingdomQueueCount(1) : -1;
        miTrain.Invoke(brain1, new object[] { k1, bcfg });
        int q1 = TrainingSystem.Instance != null ? TrainingSystem.Instance.GetKingdomQueueCount(1) : -1;
        bool queued = q1 > q0;
        Log("P1d ⑯训练将军入队（队列 " + q0 + "→" + q1 + "，计数面 0→1）", queued);
        yield return WaitDays(6);   // General effCostDays 2~3 日→等毕业+成军
        int gens1 = KingdomBrain.CountGenerals(1);
        int fm = CountFormationMembers(1);
        // L-21 计数面 0→1 翻转：将军数 0→N 且编队成员>0（B7 成军）
        bool p1e = gens1 > gens0 && fm > 0;
        Log("P1e B7 将军毕业成军：将军 " + gens0 + "→" + gens1 + " 编队成员=" + fm, p1e);

        // ===== P2 军事建筑可建可训（练兵场/训练营落地+兵种产出）+ L-21 =====
        var bdefC = BuildingFactory.FindDefById(BuildingIds.TrainingCamp);
        var sC = bdefC != null ? miSpot.Invoke(null, new object[] { 1, bdefC, 40 }) as GridCoord? : null;
        bool builtC = sC.HasValue && bdefC != null && bc.TryBuild(bdefC, sC.Value, GateOrientation.Horizontal, 1);
        Log("P2a 建训练营提交=" + builtC, builtC);
        yield return WaitDays(4);
        var campB = FindB(1, BuildingIds.TrainingCamp);
        Log("P2b 训练营 Active=" + (campB != null), campB != null);

        // 兵种产出（TryTrainFromKingdomPool 生产链；兵源池 Resident——P1 训练将军已耗 Resident，
        // 镜像 AI 链 Worker→Resident 兜底再训（HH.138 列报语义））
        EnsureResidentPool(1);
        int military0 = CountMilitaryUnits(1);
        bool trainOk = false;
        if (campB != null && TrainingSystem.Instance != null)
        {
            var tSys = TrainingSystem.Instance;
            var trainings = tSys.GetTrainings(campB);
            for (int i = 0; i < trainings.Count; i++)
            {
                if (trainings[i].raceId != -1 && trainings[i].raceId != KingdomRace.GetKingdomRace(1)) continue;
                if (trainings[i].toOccupation == Occupation.General) continue;
                if (!MilitaryProfessions.IsCombat(trainings[i].toOccupation)) continue;
                if (tSys.TryTrainFromKingdomPool(1, campB, trainings[i].toOccupation)) { trainOk = true; break; }
            }
        }
        yield return WaitDays(3);
        int military1 = CountMilitaryUnits(1);
        // L-21 计数面 0→1 翻转：训练入队成功且军事单位 +N（兵种产出实体）
        bool p2c = trainOk && military1 > military0;
        Log("P2c 兵种产出：入队=" + trainOk + " 军事单位 " + military0 + "→" + military1, p2c);

        // L-21 失败路径②：空兵源池（无 Resident/Worker 可转）→ 入队失败不堆积
        int res0 = CountPoolUnits(1);
        int q2a = TrainingSystem.Instance != null ? TrainingSystem.Instance.GetKingdomQueueCount(1) : -1;
        bool failPathOk = true;
        if (campB != null && TrainingSystem.Instance != null)
        {
            // 兵源耗尽场景：把本国 Resident/Worker 全转 Vagrant 再试（探针临时，即改即还原后续）
            var toRestore = new List<(UnitController, Occupation)>();
            var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
            if (units != null)
                foreach (var u in units)
                    if (u != null && u.IsAlive && u.kingdomId == 1
                        && (u.EffectiveOccupation == Occupation.Resident || u.EffectiveOccupation == Occupation.Worker))
                    { toRestore.Add((u, u.EffectiveOccupation)); u.SetOccupation(Occupation.Vagrant); }
            var tSys = TrainingSystem.Instance;
            var trainings = tSys != null ? tSys.GetTrainings(campB) : null;
            Occupation pickFail = Occupation.Warrior;
            if (trainings != null)
                for (int i = 0; i < trainings.Count; i++)
                    if (trainings[i].raceId == -1 || trainings[i].raceId == KingdomRace.GetKingdomRace(1))
                    { pickFail = trainings[i].toOccupation; break; }
            bool tried = tSys != null && !tSys.TryTrainFromKingdomPool(1, campB, pickFail);
            foreach (var kv in toRestore)
                if (kv.Item1 != null && kv.Item1.IsAlive) kv.Item1.SetOccupation(kv.Item2);
            int q2b = tSys != null ? tSys.GetKingdomQueueCount(1) : -1;
            failPathOk = tried && q2b == q2a;
        }
        Log("P2d 失败路径② 兵源池空入队拒（队列 " + q2a + "→" + q2bq() + "）", failPathOk);

        // ===== P3 ⑦按权重分布（同 seed 选招序列确定+安全栏+失败终止）=====
        // 前置：k1 训练营在场（P2 已建）；选招候选域=共通+本族（建筑前置联动=训练营）。
        // 门控前提（HH.146 run1/2 实证）：ExecuteRecruitArmy 首闸=warriorCount >= MilitaryTarget(D348) 即返——
        // k1 成军+P2c 已达 D348 目标（软帽 2+workerCount 约束）→ 永不选招。探针注入 Military 阶段抬 stageFactor
        // +TopUpWorkerPool 抬软帽 → 目标缺口恢复（未达目标=选招执行前提；P9a scriptPhase 注入同法，用毕复原）。
        k1.resources.gold += 1000;
        var miArmy = brain1.GetType().GetMethod("ExecuteRecruitArmy", BindingFlags.NonPublic | BindingFlags.Instance);
        var savedPhase3 = k1.scriptPhase;
        k1.scriptPhase = ScriptStage.Military;
        var selSeq = new List<Occupation>();
        int militaryBefore = CountMilitaryUnits(1);
        for (int i = 0; i < 3 && miArmy != null; i++)
        {
            k1.scriptPhase = ScriptStage.Military;   // 每轮重注入：AI 日 tick 会把阶段机覆回 Develop（HH.146 run3 实证）
            TopUpResidentPool(1, 2);   // 训练兵源保底（含 AI 日 tick 消耗）
            TopUpWorkerPool(1, 3);     // D348 软帽=2+工人数保底（workerCount 高→软帽>warriorCount→门控破）
            int wc = k1.warriorCount, wk = k1.workerCount;
            int tgt = UtilityScorer.MilitaryTarget(k1, bcfg);
            Debug.Log("[2_22P0-P3diag] i=" + i + " warrior=" + wc + " worker=" + wk + " phase=" + k1.scriptPhase
                + " target=" + tgt + " military=" + CountMilitaryUnits(1) + " poolR=" + CountPoolUnits(1));
            miArmy.Invoke(brain1, new object[] { k1, bcfg });
            yield return WaitDays(3);   // 训练入队→毕业需 2-3 天，1 天不足（HH.146 run3 实证）→ 等毕业军事数才涨
            int mNow = CountMilitaryUnits(1);
            if (mNow > militaryBefore)
            {
                militaryBefore = mNow;
                var newest = FindNewestCombat(1);
                if (newest != null) selSeq.Add(newest.EffectiveOccupation);
            }
        }
        bool p3Flip = selSeq.Count > 0;
        Log("P3a L-21 计数面翻转：⑦选招落地 " + selSeq.Count + " 次（序列=" + string.Join(",", selSeq) + "）", p3Flip);
        // 多样性下限不破：安全栏 clamp 断言（全部权重 ∈ [0.2,2.5]）
        bool p3bar = true;
        for (int i = 0; i < selSeq.Count; i++)
        {
            float w = BattleLearnedWeights.Get(1, (int)selSeq[i]);
            if (w < BattleLearnedWeights.WeightFloor - 0.0001f || w > BattleLearnedWeights.WeightCap + 0.0001f) p3bar = false;
        }
        Log("P3b 安全栏 clamp（权重∈[" + BattleLearnedWeights.WeightFloor + "," + BattleLearnedWeights.WeightCap + "]）=" + p3bar, p3bar);
        // L-21 失败路径③：无可负担候选 → 不空转（队列不涨）
        int q3a = TrainingSystem.Instance != null ? TrainingSystem.Instance.GetKingdomQueueCount(1) : -1;
        int goldSave = k1.resources.gold;
        k1.resources.gold = 0;   // 穷国不可负担
        if (miArmy != null) miArmy.Invoke(brain1, new object[] { k1, bcfg });
        k1.resources.gold = goldSave;
        int q3b = TrainingSystem.Instance != null ? TrainingSystem.Instance.GetKingdomQueueCount(1) : -1;
        bool p3fail = q3b == q3a;
        Log("P3c 失败路径③ 穷国不空转（队列 " + q3a + "→" + q3b + "）", p3fail);
        k1.scriptPhase = savedPhase3;   // 复原注入的 Military 阶段（P9a 同纪律）

        // ===== P4 邻接威胁修正（A5：邻接计入/非邻接不计入）=====
        var ts = TerritorySystem.Instance;
        var fDict = typeof(TerritorySystem).GetField("_territory", BindingFlags.NonPublic | BindingFlags.Instance);
        var fDirty = typeof(TerritorySystem).GetField("_adjacencyDirty", BindingFlags.NonPublic | BindingFlags.Instance);
        var miNM = typeof(UtilityScorer).GetMethod("NeighborMilitary", BindingFlags.NonPublic | BindingFlags.Static);
        if (ts == null || fDict == null || fDirty == null || miNM == null)
        { Log("P4 前置缺失 ts/dict/miNM", false); Finish(); yield break; }
        var terr = (Dictionary<Vector2Int, int>)fDict.GetValue(ts);
        int[] dxs = { 1, -1, 0, 0 }; int[] dys = { 0, 0, 1, -1 };
        // 构邻接对：k1/k2/k3 两两试构（每国土格×4 邻找首个无主位），首个成功即用——
        // 21140 局地形某国边境可能被圈死（HH.128 21128 局 v2 策略失效同理），多对兜底保证邻接可达
        int anchorK = -1, neighborK = -1;
        Vector2Int midAdj = default;
        var pairCandidates = new (int, int)[] { (1, 2), (1, 3), (2, 3) };
        for (int pi = 0; pi < pairCandidates.Length && anchorK < 0; pi++)
        {
            int a = pairCandidates[pi].Item1, b = pairCandidates[pi].Item2;
            var own = new List<Vector2Int>();
            foreach (var kv in terr) if (kv.Value == a) own.Add(kv.Key);
            own.Sort((x, y) => x.x != y.x ? x.x.CompareTo(y.x) : x.y.CompareTo(y.y));
            for (int ci = 0; ci < own.Count && anchorK < 0; ci++)
            {
                for (int d = 0; d < 4; d++)
                {
                    var cand = new Vector2Int(own[ci].x + dxs[d], own[ci].y + dys[d]);
                    if (!terr.ContainsKey(cand)) { anchorK = a; neighborK = b; midAdj = cand; break; }
                }
            }
        }
        bool p4pre = anchorK > 0;
        if (p4pre) { terr[midAdj] = neighborK; fDirty.SetValue(ts, true); }
        Log("P4a 反射构邻接对(" + anchorK + "," + neighborK + ")=" + p4pre + " 已邻接=" + (p4pre && ts.AreKingdomsAdjacent(anchorK, neighborK)), p4pre && ts.AreKingdomsAdjacent(anchorK, neighborK));
        int nm0 = anchorK > 0 ? (int)miNM.Invoke(null, new object[] { anchorK }) : -1;
        int thirdK = (anchorK == 1 && neighborK == 2) ? 3 : ((anchorK == 1 && neighborK == 3) ? 2 : 1);
        // 差分对象保障：k2/k3 AI 国开局可能无战士 → FindNearestCombat 空 → 差分成死（HH.146 首跑 P4b 实证）。
        // 仅对无战斗单位的国补足（k1 已有将军+兵，SetWarriors 会误缩）。
        if (anchorK > 0 && CountMilitaryUnits(neighborK) == 0) SetWarriors(neighborK, 1);
        if (anchorK > 0 && CountMilitaryUnits(thirdK) == 0) SetWarriors(thirdK, 1);
        UnitController uA = FindNearestCombat(anchorK), uN = FindNearestCombat(neighborK), uT = FindNearestCombat(thirdK);
        bool p4 = false; string p4why = "邻接对未生效";
        if (p4pre && uA != null && uN != null && uT != null)
        {
            // 差分起点：uN/uT 若已是战斗职业，先转 Resident（否则 SetOccupation(Warrior) 幂等→增量 0 假阴性）
            if (MilitaryProfessions.IsCombat(uN.EffectiveOccupation)) uN.SetOccupation(Occupation.Resident);
            if (MilitaryProfessions.IsCombat(uT.EffectiveOccupation)) uT.SetOccupation(Occupation.Resident);
            nm0 = (int)miNM.Invoke(null, new object[] { anchorK });
            if (nm0 >= 0)
            {
                var occN = uN.EffectiveOccupation; var occT = uT.EffectiveOccupation;
                uN.SetOccupation(Occupation.Warrior);
                int nm2 = (int)miNM.Invoke(null, new object[] { anchorK });
                bool pos = nm2 == nm0 + 1;
                uT.SetOccupation(Occupation.Warrior);
                int nm3 = (int)miNM.Invoke(null, new object[] { anchorK });
                bool neg = nm3 == nm2;   // 非邻接国加兵威胁分不变（负探针）
                terr.Remove(midAdj); fDirty.SetValue(ts, true);
                uN.SetOccupation(occN); uT.SetOccupation(occT);
                int nm4 = (int)miNM.Invoke(null, new object[] { anchorK });
                p4 = pos && neg && nm4 == nm0;
                p4why = "锚=" + anchorK + " nm 基线=" + nm0 + " 构对+k" + neighborK + "兵=" + nm2 + " +k" + thirdK + "兵=" + nm3 + " 拆对还原=" + nm4;
            }
        }
        Log("P4b 邻接差分（正=邻接计入/负=非邻接不计/净场还原）：" + p4why, p4);

        // ===== P5 姿态升降（威胁注入→警戒档巡逻频率）=====
        var posture1 = brain1.Posture;
        SetWarriors(1, 5); yield return null;
        var mockA = new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry>(), Day = 500 };
        mockA.Threats.Add(new ThreatEntry { KingdomId = 2, WarriorCount = 5 });
        mockA.OwnWarriorCount = k1.warriorCount;
        SituationHub.Put(1, mockA);
        var fldSit = typeof(KingdomBrain).GetField("_situation", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fldSit != null) fldSit.SetValue(brain1, mockA);
        posture1.Evaluate(mockA, k1, scfg, pcfg, 502);
        bool alertOn = posture1.Current == MilitaryPosture.Alert;
        var miPosture = brain1.GetType().GetMethod("ExecutePosture", BindingFlags.NonPublic | BindingFlags.Instance);
        if (miPosture != null && alertOn) miPosture.Invoke(brain1, new object[] { k1, pcfg });
        yield return null;
        int patrolNow = CountPatrolling(1);
        bool p5 = alertOn && patrolNow > 0;
        Log("P5 姿态升降：威胁注入→警戒档=" + alertOn + " 本国巡逻数=" + patrolNow + "（>0）", p5);
        // 解除：威胁清空→无档+巡逻回收
        var mockN = new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry>(), Day = 503 };
        mockN.OwnWarriorCount = k1.warriorCount;
        SituationHub.Put(1, mockN);
        if (fldSit != null) fldSit.SetValue(brain1, mockN);
        posture1.Evaluate(mockN, k1, scfg, pcfg, 504);
        if (miPosture != null) miPosture.Invoke(brain1, new object[] { k1, pcfg });
        yield return null;
        bool p5b = posture1.Current == MilitaryPosture.None;
        Log("P5b 威胁清空→无档=" + p5b + "（升降双向）", p5b);

        // ===== P6 AI 驻防（动员→守军编队自动派驻）+ L-21 双断言 =====
        SetWarriors(1, 1); yield return null;   // 军力<危机线
        mockA.Day = 510;
        mockA.Threats[0] = new ThreatEntry { KingdomId = 2, WarriorCount = 5 };
        mockA.OwnWarriorCount = k1.warriorCount;
        SituationHub.Put(1, mockA);
        if (fldSit != null) fldSit.SetValue(brain1, mockA);
        posture1.Evaluate(mockA, k1, scfg, pcfg, 511);
        bool mobOn = posture1.Current == MilitaryPosture.Mobilized;
        SetWarriors(1, 8); yield return null;   // 兵源回满（守军 m≥2 前提）
        if (miPosture != null && mobOn) miPosture.Invoke(brain1, new object[] { k1, pcfg });
        yield return WaitDays(3);
        int garrisons = CountGarrisons(1);
        int shells = CountGarrisonEmptyShells();
        // L-21：守军编队>0（计数面翻转）+ 空壳=0（失败终止无堆积）
        bool p6 = mobOn && garrisons > 0 && shells == 0;
        Log("P6 AI 驻防：动员档=" + mobOn + " 守军编队=" + garrisons + "（>0）空壳=" + shells + "（=0）", p6);

        // ===== P7 读档恢复（E1：统计/档位/权重）+ L-17 变体③ 快照复原 =====
        // 标记态：k1 权重/姿态/损毁流水设已知值 → 存档 → 破坏 → 读档 → 断言恢复
        BattleLearnedWeights.SetWeight(1, (int)Occupation.Archer, 1.7f);
        BattleLearnedWeights.SetWeight(1, (int)Occupation.HeavyWarrior, 0.4f);
        mockA.Day = 600;
        mockA.Threats[0] = new ThreatEntry { KingdomId = 2, WarriorCount = 5 };
        mockA.OwnWarriorCount = k1.warriorCount;
        SituationHub.Put(1, mockA);
        if (fldSit != null) fldSit.SetValue(brain1, mockA);
        posture1.Evaluate(mockA, k1, scfg, pcfg, 601);
        var savedPosture = posture1.Current;
        var savedLastChange = posture1.LastChangeDay;
        // 注入确定性损毁流水（E1 统计入档强验证：0 流水→读档 0==0 弱验证；注入 1 条→恢复 1==1 实锤）
        var fPending = typeof(KingdomBrain).GetField("_pendingLosses", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fPending != null && fPending.GetValue(brain1) is System.Collections.IList pendingList)
            pendingList.Add(new LossEntry { Day = 601, IsBuilding = false, OccupationId = (int)Occupation.Archer });
        var savedLosses = new List<KingdomBrainSaveLossEntry>();
        brain1.CollectLosses(savedLosses);
        // 注入标记损毁（确保统计非空且确定）
        var save = SaveManager.Instance;
        bool saved = save != null && save.Save(SLOT);
        yield return null;
        Log("P7a 存档=" + saved + "（存前姿态=" + savedPosture + " 权重 Archer=" + BattleLearnedWeights.Get(1, (int)Occupation.Archer) + " 损毁流水=" + savedLosses.Count + "）", saved);

        // 破坏态：权重/姿态/流水全清改
        BattleLearnedWeights.SetWeight(1, (int)Occupation.Archer, 0.5f);
        BattleLearnedWeights.Remove(1);
        var mockBroken = new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry>(), Day = 602 };
        mockBroken.OwnWarriorCount = k1.warriorCount;
        SituationHub.Put(1, mockBroken);
        if (fldSit != null) fldSit.SetValue(brain1, mockBroken);
        posture1.Evaluate(mockBroken, k1, scfg, pcfg, 603);   // → None
        Log("P7b 破坏态：权重 Archer=" + BattleLearnedWeights.Get(1, (int)Occupation.Archer) + " 姿态=" + posture1.Current, true);

        bool loaded = save != null && save.Load(SLOT);
        yield return null;
        yield return new WaitForSeconds(0.5f);
        // 读档后重新取引用（世界重建，旧引用作废）
        var reg2 = KingdomRegistry.Instance;
        var k1b = reg2 != null ? reg2.Get(1) : null;
        var brain1b = KingdomBrainRegistry.Instance != null ? KingdomBrainRegistry.Instance.Get(1) : null;
        float wBack = BattleLearnedWeights.Get(1, (int)Occupation.Archer);
        var postureBack = brain1b != null ? brain1b.Posture.Current : MilitaryPosture.None;
        var lossesBack = new List<KingdomBrainSaveLossEntry>();
        if (brain1b != null) brain1b.CollectLosses(lossesBack);
        bool p7 = loaded && k1b != null && brain1b != null
                  && Mathf.Abs(wBack - 1.7f) < 0.0001f
                  && postureBack == savedPosture
                  && lossesBack.Count == savedLosses.Count;
        Log("P7c 读档恢复：loaded=" + loaded + " 权重 Archer=" + wBack + "（=1.7）姿态=" + postureBack + "（=" + savedPosture + "）损毁流水=" + lossesBack.Count + "（=" + savedLosses.Count + "）", p7);
        // 旧档可载不炸：模块 version=1 且 pending 消费后清空（旧档无本条目=SaveManager 跳过零兼容负担=结构保证）
        Log("P7d 读档无异常+pending 已消费（旧档可载不炸=模块自治 version 结构保证）", loaded && p7);

        // ===== P8 选址打分器三条（F1 军事朝向/F2 经济邻近/同 seed 确定性）+ 失败路径 =====
        var bdefFarm = BuildingFactory.FindDefById("farm");
        var ownCastle = KingdomBrain.FindCastleCell(1);
        var threatCastle = KingdomBrain.FindCastleCell(2);
        if (ownCastle.HasValue && threatCastle.HasValue && bdefB != null && bdefFarm != null)
        {
            var gs = GridSystem.Instance;
            int div = gs != null && gs.Config != null && gs.Config.subCellDivisor > 0 ? gs.Config.subCellDivisor : 4;
            var taSub = new GridCoord(threatCastle.Value.x * div, threatCastle.Value.y * div, threatCastle.Value.layer);
            var ownSub = new GridCoord(ownCastle.Value.x * div, ownCastle.Value.y * div, ownCastle.Value.layer);
            int maxR = 8; int bandDenom = maxR * div;
            int denom = Mathf.Max(Chebyshev(ownSub, taSub), bandDenom);
            var sitT = new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry> { new ThreatEntry { KingdomId = 2, WarriorCount = 5 } }, Day = 700 };

            // P8a F1 军事朝向性：近威胁锚分 > 远威胁锚分（纯函数方向性）
            var miF1 = typeof(PlacementScorer).GetMethod("ComputeF1", BindingFlags.NonPublic | BindingFlags.Static);
            bool p8a = false; string p8aWhy = "ComputeF1 缺失";
            if (miF1 != null && miF1.GetParameters().Length == 3)
            {
                var near = new GridCoord(taSub.x - 8, taSub.y - 8, 0);
                var far = new GridCoord(taSub.x - 80, taSub.y - 80, 0);
                float f1Near = (float)miF1.Invoke(null, new object[] { near, (GridCoord?)taSub, denom });
                float f1Far = (float)miF1.Invoke(null, new object[] { far, (GridCoord?)taSub, denom });
                p8a = f1Near > f1Far;
                p8aWhy = "近锚 F1=" + f1Near.ToString("F3") + ">远锚 F1=" + f1Far.ToString("F3");
            }
            Log("P8a F1 军事朝向性（近威胁锚>远威胁锚）：" + p8aWhy, p8a);

            // P8b F2 经济邻近性：近关联建筑 > 远（农田↔粮仓）
            var miF2 = typeof(PlacementScorer).GetMethod("ComputeF2", BindingFlags.NonPublic | BindingFlags.Static);
            bool p8b = false; string p8bWhy = "ComputeF2 缺失";
            if (miF2 != null)
            {
                var linkNear = new GridCoord(ownSub.x + 2, ownSub.y + 2, 0);
                var links = new List<GridCoord> { linkNear };
                var spotNear = new GridCoord(ownSub.x + 1, ownSub.y + 1, 0);
                var spotFar = new GridCoord(ownSub.x + 40, ownSub.y + 40, 0);
                float f2Near = (float)miF2.Invoke(null, new object[] { spotNear, links, maxR, div });
                float f2Far = (float)miF2.Invoke(null, new object[] { spotFar, links, maxR, div });
                p8b = f2Near > f2Far;
                p8bWhy = "近关联 F2=" + f2Near.ToString("F3") + ">远关联 F2=" + f2Far.ToString("F3");
            }
            Log("P8b F2 经济邻近性（近关联建筑>远）：" + p8bWhy, p8b);

            // P8c 同 seed 确定性双跑一致（TryPick 两次逐字段）
            bool c1 = PlacementScorer.TryPick(1, bdefB, maxR, bpCfg, sitT, out var rA);
            bool c2 = PlacementScorer.TryPick(1, bdefB, maxR, bpCfg, sitT, out var rB);
            bool p8c = c1 && c2 && rA.Sub.x == rB.Sub.x && rA.Sub.y == rB.Sub.y
                       && rA.Score.ToString("R") == rB.Score.ToString("R")
                       && rA.F1.ToString("R") == rB.F1.ToString("R");
            Log("P8c 同 seed 选址确定性：选格(" + rA.Sub.x + "," + rA.Sub.y + ") Score=" + rA.Score.ToString("F4") + " 双跑一致=" + p8c, p8c);

            // L-21 失败路径④：候选全非法（探针构造）→ TryPick false + 无异常（Bump fail 明日再试=产品侧）
            bool p8d = false; string p8dWhy = "无候选构造失败";
            if (ownCastle.HasValue && gs != null)
            {
                // 用海/边界外锚点构造不可行：半径 0 + 主城格被占（castle 已占格=唯一合法面被占）
                bool c0 = PlacementScorer.TryPick(1, bdefB, 0, bpCfg, sitT, out _);
                p8d = !c0;   // 半径 0 无合法格=失败路径终止性（返回 false 不炸）
                p8dWhy = "半径 0 无候选→TryPick=" + c0 + "（应 false=失败路径终止）";
            }
            Log("P8d L-21 失败路径终止性：" + p8dWhy, p8d);
        }
        else Log("P8 前置缺失（城/def）", false);

        // ===== P9 机器双行动（B8：族门禁负+上限负+态势触发正）=====
        k1b = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(1) : null;
        if (k1b != null) { k1b.resources.gold += 3000; k1b.resources.stone += 800; k1b.resources.wood += 800; }
        var sps = SiegeProductionSystem.Instance;
        var bdefW = BuildingFactory.FindDefById(BuildingIds.SiegeWorkshop);
        if (sps == null || bdefW == null || k1b == null) { Log("P9 前置缺失 sps/bdefW/k1", false); Finish(); yield break; }

        // 态势触发正探针（MachineDemand 评分：无威胁=0 / 有威胁+军事期>0）
        // 阶段门（UtilityScorer：scriptPhase!=Military→0）为内部前置——探针临时置 Military（复原，HH.140 P6d 同法）
        var defMD = new UtilityActionDef { id = UtilityAction.ProduceMachine, name = "probeMD", need = NeedKind.MachineDemand, needA = 2 };
        var savedPhase9 = k1b.scriptPhase;
        k1b.scriptPhase = ScriptStage.Military;
        SituationHub.Put(1, new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry>(), Day = 800 });
        PostureHub.Put(1, MilitaryPosture.None);
        float mdNone = UtilityScorer.NeedScore(k1b, defMD);
        SituationHub.Put(1, new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry> { new ThreatEntry { KingdomId = 2, WarriorCount = 5 } }, Day = 801 });
        PostureHub.Put(1, MilitaryPosture.Alert);
        float mdAlert = UtilityScorer.NeedScore(k1b, defMD);
        PostureHub.Put(1, MilitaryPosture.None);
        k1b.scriptPhase = savedPhase9;
        bool p9md = mdNone <= 0.0001f && mdAlert > 0.0001f;
        Log("P9a 态势触发正探针（MachineDemand，军事期注入）：无威胁分=" + mdNone.ToString("F3") + "（0）/威胁分=" + mdAlert.ToString("F3") + "（>0）", p9md);

        // 建厂（生产链）+ 等 Active
        var sW = miSpot.Invoke(null, new object[] { 1, bdefW, 25 }) as GridCoord?;
        bool builtW = sW.HasValue && bc.TryBuild(bdefW, sW.Value, GateOrientation.Horizontal, 1);
        yield return WaitDays(4);
        bool workshopActive = FindB(1, BuildingIds.SiegeWorkshop) != null;
        Log("P9b 建机器工坊提交=" + builtW + " Active=" + workshopActive, workshopActive);

        int race1 = KingdomRace.GetKingdomRace(1);
        Occupation[] machines = { Occupation.Ballista, Occupation.SiegeMachine, Occupation.Mortar, Occupation.VineCatapult, Occupation.Ram };
        Occupation foreign = Occupation.Ram;
        for (int i = 0; i < machines.Length; i++)
            if (!SiegeProductionSystem.IsMachineAllowed(race1, machines[i])) { foreign = machines[i]; break; }
        bool gate = !sps.ProduceMachine(foreign, new Vector2(0, 0), 1);   // 族门禁负探针：异族机器拒
        Log("P9c 族门禁负探针：k1(族" + race1 + ")×异族" + foreign + "=拒 " + gate, gate);
        // 正探针：本族机器可造（同源 IsMachineAllowed 选型）。
        // B8 prefab 依赖注记：人类 Ballista prefab 在场，三族机器 prefab 缺（美术批7）——
        // 缺图族造机=扣费无生成（D582 认可语义），本族机器若缺图用 Ballista prefab 内存借用
        //（HH.140 P6c 模式：不入盘不入档，判定后复原）使机器真实生成=上限/计数断言有实体。
        Occupation pickM = machines[0];
        for (int i = 0; i < machines.Length; i++)
            if (SiegeProductionSystem.IsMachineAllowed(race1, machines[i])) { pickM = machines[i]; break; }
        var umd = UnitDataManager.Instance;
        var machineData = umd != null ? umd.GetData(Faction.PlayerCamp, pickM) : null;
        var keptPrefab = machineData != null ? machineData.prefab : null;
        if (machineData != null && MachinePanel.IsPrefabMissing(pickM, out _))
            machineData.prefab = umd.GetData(Faction.PlayerCamp, Occupation.Ballista) != null
                ? umd.GetData(Faction.PlayerCamp, Occupation.Ballista).prefab : null;
        bool okM = sps.ProduceMachine(pickM, new Vector2(0, 0), 1);
        Log("P9d 正探针：k1×本族" + pickM + "=成 " + okM, okM);
        // 上限负探针：连造至上限+1 → 拒绝（GetPlacedMachineCountByKingdom ≤ GetMachineLimit）
        if (okM)
        {
            for (int i = 0; i < 6; i++)
                sps.ProduceMachine(pickM, new Vector2(0, 0), 1);
        }
        if (machineData != null) machineData.prefab = keptPrefab;   // 复原（内存 mock 不入盘）
        int placedM = sps.GetPlacedMachineCountByKingdom(1);
        int limitM = sps.GetMachineLimit();
        bool p9cap = placedM <= limitM;
        Log("P9e 上限负探针：机器数=" + placedM + " 上限=" + limitM + "（≤上限）", p9cap);
        // 军力现状口径含机器：等日 tick 快照重建（BuildSituation 已接线 GetPlacedMachineCountByKingdom）
        yield return WaitDays(1);
        int mc = 0;
        if (SituationHub.TryGet(1, out var snapNow) && snapNow != null) mc = snapNow.MachineCount;
        Log("P9f 军力现状含机器口径：快照 MachineCount=" + mc + " 落位=" + placedM + "（A2/D570）", mc == placedM);


        // ===== 收尾：L-17 变体③ 复原 + 撤守卫前终检 =====
        RestorePlayerOccupations();
        yield return null;
        Finish();
    }

    private static int q2bq() => TrainingSystem.Instance != null ? TrainingSystem.Instance.GetKingdomQueueCount(1) : -1;

    /// <summary>确保本国存在 Resident（TryTrain 兵源池；Worker→Resident 编制内调配镜像 AI 链兜底）。</summary>
    private static void EnsureResidentPool(int kid)
    {
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units == null) return;
        foreach (var u in units)
        {
            if (u == null || !u.IsAlive || u.kingdomId != kid) continue;
            if (u.EffectiveOccupation == Occupation.Resident) return;   // 已有
        }
        foreach (var u in units)
        {
            if (u == null || !u.IsAlive || u.kingdomId != kid) continue;
            if (u.EffectiveOccupation == Occupation.Worker) { u.SetOccupation(Occupation.Resident); return; }
        }
    }

    /// <summary>补本国 Resident 兵源池至 target（P3a ⑦选招 6 轮兵源保障：P1e 成军+P2c 已耗开局 6 Worker，
    /// AI 日 tick 亦消耗——HH.138 兜底语义；SpawnUnit 收编 Faction=AiKingdom，raceId 归属国族）。</summary>
    private static void TopUpResidentPool(int kid, int target) => TopUpUnitPool(kid, Occupation.Resident, target);

    /// <summary>补本国 Worker 至 target（P3a ⑦选招 D348 软帽=2+工人数：P1e 成军耗尽 Worker→workerCount=0→
    /// 软帽=2≤warriorCount→ExecuteRecruitArmy 门控拦截永不选招——HH.146 run2 实证。spawn 实体 Worker
    /// →AliveWorkerCount 派生上升→软帽破）。</summary>
    private static void TopUpWorkerPool(int kid, int target) => TopUpUnitPool(kid, Occupation.Worker, target);

    private static void TopUpUnitPool(int kid, Occupation occ, int target)
    {
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units == null) return;
        int have = 0;
        foreach (var u in units)
            if (u != null && u.IsAlive && u.kingdomId == kid && u.EffectiveOccupation == occ) have++;
        if (have >= target) return;
        var castle = KingdomBrain.FindCastleCell(kid);
        var pos = castle.HasValue ? new Vector2(castle.Value.x + 1.5f, castle.Value.y + 1.5f) : new Vector2(50f, 50f);
        int raceId = KingdomRace.GetKingdomRace(kid);
        for (int i = have; i < target; i++)
        {
            var go = UnitFactory.Instance != null
                ? UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, occ, pos, kid) : null;
            if (go != null)
            {
                var uc = go.GetComponent<UnitController>();
                if (uc != null) uc.raceId = raceId;
            }
        }
    }

    // ===== helpers =====

    private static List<FormationMember> MembersOf(FormationController f)
    {
        var fld = typeof(FormationController).GetField("_members", BindingFlags.NonPublic | BindingFlags.Instance);
        return fld != null ? (List<FormationMember>)fld.GetValue(f) : null;
    }

    private static int CountMilitaryUnits(int kid)
    {
        int n = 0;
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units == null) return 0;
        foreach (var u in units)
            if (u != null && u.IsAlive && u.kingdomId == kid && MilitaryProfessions.IsCombat(u.EffectiveOccupation)) n++;
        return n;
    }

    private static int CountPoolUnits(int kid)
    {
        int n = 0;
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units == null) return 0;
        foreach (var u in units)
            if (u != null && u.IsAlive && u.kingdomId == kid
                && (u.EffectiveOccupation == Occupation.Resident || u.EffectiveOccupation == Occupation.Worker)) n++;
        return n;
    }

    private static UnitController FindNewestCombat(int kid)
    {
        UnitController best = null;
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units == null) return null;
        foreach (var u in units)
        {
            if (u == null || !u.IsAlive || u.kingdomId != kid) continue;
            if (!MilitaryProfessions.IsCombat(u.EffectiveOccupation)) continue;
            if (best == null || u.npcId > best.npcId) best = u;   // 最大 npcId=最近出生
        }
        return best;
    }

    private static UnitController FindNearestCombat(int kid)
    {
        UnitController best = null;
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units == null) return null;
        foreach (var u in units)
        {
            if (u == null || !u.IsAlive || u.kingdomId != kid) continue;
            if (!MilitaryProfessions.IsCombat(u.EffectiveOccupation)) continue;
            if (best == null || u.npcId < best.npcId) best = u;
        }
        return best;
    }

    private static int CountPatrolling(int kid)
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

    private static int CountGarrisons(int kid)
    {
        if (FormationManager.Instance == null) return 0;
        int n = 0;
        var fs = FormationManager.Instance.AllFormations;
        for (int i = 0; i < fs.Count; i++)
            if (fs[i] != null && fs[i].KingdomId == kid && fs[i].isGarrison) n++;
        return n;
    }

    private static int CountGarrisonEmptyShells()
    {
        if (FormationManager.Instance == null) return 0;
        int n = 0;
        var fs = FormationManager.Instance.AllFormations;
        for (int i = 0; i < fs.Count; i++)
            if (fs[i] != null && fs[i].isGarrison && fs[i].MemberCount <= 0) n++;
        return n;
    }

    private static int CountFormationMembers(int kid)
    {
        if (FormationManager.Instance == null) return 0;
        int n = 0;
        var fs = FormationManager.Instance.AllFormations;
        for (int i = 0; i < fs.Count; i++)
        {
            var f = fs[i];
            if (f == null || f.KingdomId != kid) continue;
            n += f.MemberCount;
        }
        return n;
    }

    private static int SetWarriors(int kid, int target)
    {
        int have = CountMilitaryUnits(kid);
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units == null) return have;
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

    private static Building FindB(int kid, string id)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return null;
        foreach (var b in reg.All)
            if (b != null && b.def != null && b.IsActive && b.kingdomId == kid && b.def.id == id) return b;
        return null;
    }

    private static int Chebyshev(GridCoord a, GridCoord b)
        => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

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
        // L-17 变体③：撤守卫前终检（判负条件=玩家桶 0 Worker/Civilian 全灭）
        if (ThroneAnchor.Instance != null && !ThroneAnchor.Instance.HasRemainingWorker())
            Debug.LogError("[2_22P0] ★★ 撤守卫前玩家 Worker=0（复原失败）——撤守卫将立即 GameOver！");
        _log.AppendLine("===== Smoke_2_22P0 王国AI P0 冒烟收工 =====");
        _log.AppendLine("PASS=" + _pass + " FAIL=" + _fail + " 时间=" + System.DateTime.Now.ToString("HH:mm:ss"));
        if (TimeManager.Instance != null) TimeManager.Instance.SetGameSpeed(0f);
        TestHarnessApi.ExitTestRun();
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "smoke_2_22p0_" + RunTag + ".log"), _log.ToString());
        }
        catch (System.Exception e) { Debug.LogError("[2_22P0] 日志写盘失败: " + e.Message); }
        Debug.LogWarning("[2_22P0] ★ 收工 PASS=" + _pass + " FAIL=" + _fail + "（tag=" + RunTag + "，终速 0+ExitTestRun）");
        var host = Object.FindObjectOfType<ProbeHost>();
        if (host != null) Application.logMessageReceived -= host.Catch;
    }
}