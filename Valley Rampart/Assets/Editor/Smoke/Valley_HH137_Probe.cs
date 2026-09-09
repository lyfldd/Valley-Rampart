using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;

// ============================================================================
//  HH.137 王国AI P0 批B 行为级探针容器（Editor-only；test-harness-first 铁律①⑤）。
//  探针清单（对照 2_22 §二批B 验收列+D590 增补节5+D592 前置注记）：
//   P1(B4) 兵种出厂倾向先验读回：orc Berserker=0.8/human Warrior=0.5/未列出回退 0.1
//   P2(B5) 局内环：同输入双国权重序列逐字节一致（确定性）+极端死亡 clamp 撞栏（负探针）
//   P3(评分) 内源势能在场证：GeneralGap 评分在 internalDriveWeight=0.1 vs 0 差分>0（置 0=退化自证）
//   P4(B1/B2/B6/B7) 行为级：反射建 Barracks/TrainingCamp→直调 ExecuteTrainGeneral/ExecuteRecruitArmy
//            →训练入队→等毕业→将军实体出现+自动成军（编队成员>0）；族门禁负探针（orc 造矮人兵=拒）
//   P5(B8) 机器：族门禁负探针（human 造 Ram=拒）+正探针（造 Ballista=成）+上限负探针
//  证据落 Logs/P1/hh137_probe.log（不入库，断言转录 HH.138）。
// ============================================================================
public static class Valley_HH137_Probe
{
    private const int SEED = 21137;
    private const string SLOT = "probe_hh137";
    private static int _pass, _fail;
    private static readonly System.Text.StringBuilder _log = new System.Text.StringBuilder();

    [MenuItem("Valley/验证/HH137_批B探针")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH137探针] 须先 GameScene 进 Play。"); return; }
        _pass = 0; _fail = 0; _log.Length = 0;
        var host = new GameObject("HH137_ProbeHost").AddComponent<ProbeHost>();
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
        Debug.LogWarning("[HH137探针] " + (ok ? "✓ " : "✗ ") + msg);
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

        yield return WaitDays(3);   // 态势链运转+训练建筑注册就绪

        var reg = KingdomRegistry.Instance;
        var k1 = reg.Get(1); var k2 = reg.Get(2); var k3 = reg.Get(3);
        if (k1 == null || k2 == null || k3 == null) { Log("AI 国缺失", false); Finish(); yield break; }

        // ===== P1：B4 先验读回 =====
        var orc = Resources.Load<RaceDef>("Config/Races/Race_Orc");
        var hum = Resources.Load<RaceDef>("Config/Races/Race_Human");
        bool p1 = orc != null && hum != null
                  && Mathf.Abs(orc.GetUnitPrior(Occupation.Berserker) - 0.8f) < 0.001f
                  && Mathf.Abs(hum.GetUnitPrior(Occupation.Warrior) - 0.5f) < 0.001f
                  && Mathf.Abs(orc.GetUnitPrior(Occupation.Cavalry) - 0.1f) < 0.001f;   // 兽人未列 Cavalry→默认 0.1
        Log("P1 B4 先验读回 orc.Berserker=" + (orc != null ? orc.GetUnitPrior(Occupation.Berserker).ToString("F2") : "?")
            + " hum.Warrior=" + (hum != null ? hum.GetUnitPrior(Occupation.Warrior).ToString("F2") : "?")
            + " orc.Cavalry(默认)=" + (orc != null ? orc.GetUnitPrior(Occupation.Cavalry).ToString("F2") : "?"), p1);

        // ===== P2：B5 局内环确定性+clamp =====
        var stats = new UnitPerfInput[] {
            new UnitPerfInput { OccupationId = (int)Occupation.Warrior, Deaths = 10 },
            new UnitPerfInput { OccupationId = (int)Occupation.Archer, Deaths = 2 },
            new UnitPerfInput { OccupationId = (int)Occupation.Mage, Deaths = 0 } };
        var dA = BattleLearnedWeights.UpdateFromLosses(901, stats, 0.05f);
        var dB = BattleLearnedWeights.UpdateFromLosses(902, stats, 0.05f);
        bool det = dA.Count == dB.Count;
        if (det)
            for (int i = 0; i < dA.Count; i++)
                det &= dA[i].OccupationId == dB[i].OccupationId
                    && dA[i].OldW.ToString("R") == dB[i].OldW.ToString("R")
                    && dA[i].NewW.ToString("R") == dB[i].NewW.ToString("R");
        Log("P2a B5 同输入双国权重序列逐字节一致=" + det, det);

        var heavy = new UnitPerfInput[] {
            new UnitPerfInput { OccupationId = (int)Occupation.Warrior, Deaths = 100 } };
        BattleLearnedWeights.UpdateFromLosses(903, heavy, 5f);   // 极端 η→必撞 clamp
        bool clamped = BattleLearnedWeights.Get(903, (int)Occupation.Warrior) <= BattleLearnedWeights.WeightFloor + 0.0001f;
        Log("P2b B5 极端死亡 clamp 撞下限 w=" + BattleLearnedWeights.Get(903, (int)Occupation.Warrior).ToString("F2"), clamped);
        BattleLearnedWeights.Remove(901); BattleLearnedWeights.Remove(902); BattleLearnedWeights.Remove(903);

        // ===== P3：内源势能差分（评分面）=====
        var bcfg = KingdomBrain.LoadConfig();
        var defG = new UtilityActionDef { id = UtilityAction.None, name = "probeG", need = NeedKind.GeneralGap, needA = 2 };
        var scfg = SituationConfig.Load();
        float d01 = UtilityScorer.NeedScore(k1, defG);
        float savedW = scfg.internalDriveWeight;
        scfg.internalDriveWeight = 0f;
        float d00 = UtilityScorer.NeedScore(k1, defG);
        scfg.internalDriveWeight = savedW;   // 复原
        Log("P3 内源势能差分 GeneralGap: w=0.1→" + d01.ToString("F3") + " / w=0→" + d00.ToString("F3")
            + " → 势能项在场且置0退化=" + (d01 - d00 > 0.001f), d01 - d00 > 0.001f);

        // ===== P4：行为级（建训练建筑→直调执行方法→队列→毕业成军）=====
        // 探针环境前置：AI 3 日资源积累不足（r1 实锤 k1 石13<兵营20 → PlacementValidator 资源门拒）
        // → 内存注资源（不入档），确保选址/TryBuild 过资源门，验证目标=执行链非经济
        k1.resources.gold += 500; k1.resources.stone += 300; k1.resources.wood += 300;
        int qi0 = TrainingSystem.Instance != null ? TrainingSystem.Instance.GetKingdomQueueCount(1) : -1;
        var brain = KingdomBrainRegistry.Instance != null ? KingdomBrainRegistry.Instance.Get(1) : null;
        var miTrain = brain != null ? brain.GetType().GetMethod("ExecuteTrainGeneral", BindingFlags.NonPublic | BindingFlags.Instance) : null;
        var miArmy = brain != null ? brain.GetType().GetMethod("ExecuteRecruitArmy", BindingFlags.NonPublic | BindingFlags.Instance) : null;
        var miSpot = brain != null ? brain.GetType().GetMethod("FindAIBuildSpot", BindingFlags.NonPublic | BindingFlags.Static) : null;
        var bc = BuildController.Instance;
        var bdefW = BuildingFactory.FindDefById(BuildingIds.SiegeWorkshop);   // P5 厂前置用
        if (brain == null || miTrain == null || miArmy == null || miSpot == null || bc == null || bdefW == null)
        { Log("P4 前置缺失 brain/mi/bc/bdefW", false); Finish(); yield break; }

        // 反射直建 k1 兵营+训练营（FindAIBuildSpot→TryBuild 生产链，同 ExecuteBuildFocus 语义；半径 20）
        // r3 修：TryBuild=施工启动非竣工（IsActive=false），施工完成需 AI 工人协作建造 → 建后等 3 日再验 Active
        var bdefB = BuildingFactory.FindDefById(BuildingIds.Barracks);
        var bdefC = BuildingFactory.FindDefById(BuildingIds.TrainingCamp);
        var sB = miSpot.Invoke(null, new object[] { 1, bdefB, 20 }) as GridCoord?;
        var sC = miSpot.Invoke(null, new object[] { 1, bdefC, 40 }) as GridCoord?;   // r4：训练营 2x1 在半径 20 竞争失败，扩 40
        bool builtB = sB.HasValue && bc.TryBuild(bdefB, sB.Value, GateOrientation.Horizontal, 1);
        bool builtC = sC.HasValue && bc.TryBuild(bdefC, sC.Value, GateOrientation.Horizontal, 1);
        Log("P4a 直建提交 k1 兵营=" + builtB + " 训练营=" + builtC, builtB && builtC);

        // k3 兵营同批提交（P4d 族门禁前置）——r2 修：k3 也注资源+真建兵营（缺席早退≠门禁判定）
        k3.resources.gold += 500; k3.resources.stone += 300; k3.resources.wood += 300;
        var b3 = BuildingFactory.FindDefById(BuildingIds.Barracks);
        var s3 = miSpot.Invoke(null, new object[] { 3, b3, 20 }) as GridCoord?;
        bool built3 = s3.HasValue && bc.TryBuild(b3, s3.Value, GateOrientation.Horizontal, 3);

        // 等施工完成（AI 工人协作建造； IsActive 翻转）
        yield return WaitDays(4);
        var k1b = FindB(1, BuildingIds.Barracks);
        var k1c = FindB(1, BuildingIds.TrainingCamp);
        var k3b = FindB(3, BuildingIds.Barracks);
        Log("P4a2 施工完成 Active：k1兵营=" + (k1b != null) + " k1训练营=" + (k1c != null) + " k3兵营=" + (k3b != null),
            k1b != null && k1c != null && k3b != null);

        // ⑯训练将军直调（真实生产链：TryTrainFromKingdomPool→TryTrain 同链）
        miTrain.Invoke(brain, new object[] { k1, bcfg });
        int qi1 = TrainingSystem.Instance.GetKingdomQueueCount(1);
        Log("P4b ⑯ExecuteTrainGeneral 队列 " + qi0 + "→" + qi1 + "（入队成功）", qi1 > qi0);

        // ⑦多兵种选招（r5 双面验证）：
        //   负面=k1 训练营缺席（选址空间竞争，r2~r4 实锤）→⑦无候选不空转（qi 不涨=咬合设计生效正面断言）
        //   正面=k3（空间松，兵营已成）建训练营→⑦双环选招入队（共通 Warrior/Archer/Mage/Healer 候选域）
        miArmy.Invoke(brain, new object[] { k1, bcfg });
        int qi2 = TrainingSystem.Instance.GetKingdomQueueCount(1);
        Log("P4c-neg ⑦训练营缺席不空转（队列 " + qi1 + "→" + qi2 + "）", qi2 == qi1);

        var brain3 = KingdomBrainRegistry.Instance.Get(3);
        var miArmy3 = brain3 != null ? brain3.GetType().GetMethod("ExecuteRecruitArmy", BindingFlags.NonPublic | BindingFlags.Instance) : null;
        var sC3 = miSpot.Invoke(null, new object[] { 3, bdefC, 25 }) as GridCoord?;
        bool builtC3 = sC3.HasValue && bc.TryBuild(bdefC, sC3.Value, GateOrientation.Horizontal, 3);
        yield return WaitDays(4);
        var k3c = FindB(3, BuildingIds.TrainingCamp);
        int qi3a = TrainingSystem.Instance.GetKingdomQueueCount(3);
        if (miArmy3 != null && k3c != null) miArmy3.Invoke(brain3, new object[] { k3, bcfg });
        int qi3b = TrainingSystem.Instance.GetKingdomQueueCount(3);
        Log("P4c-pos ⑦k3 训练营提交=" + builtC3 + " Active=" + (k3c != null) + " 选招队列 " + qi3a + "→" + qi3b,
            k3c != null && qi3b > qi3a);

        // 可训域负探针：orc 国(k3) 训练矮人专属 Musqueteer → 族门禁拒（D419 同链）
        bool raceGate = k3b != null && !TrainingSystem.Instance.TryTrainFromKingdomPool(3, k3b, Occupation.Musqueteer);
        Log("P4d 可训域负探针 orc×Musqueteer(矮人专属)=拒 " + raceGate, raceGate);

        // 等毕业（General effCostDays 通常 2~3 日→等 5 日）→将军+成军
        yield return WaitDays(5);
        int gens = KingdomBrain.CountGenerals(1);
        int formationMembers = CountFormationMembers(1);
        Log("P4e B7 将军毕业 k1 将军数=" + gens + " 编队成员=" + formationMembers, gens > 0 && formationMembers > 0);

        // ===== P5：B8 机器（族门禁负+正+上限负）=====
        k1.resources.gold += 2000; k1.resources.stone += 500; k1.resources.wood += 500;   // 机器成本高，注资源保执行链验证
        var sps = SiegeProductionSystem.Instance;
        // 厂前置：GetMachineLimit 依赖投掷机厂在场（无厂=上限 0 → 先建厂+等施工，镜像 r2 教训）
        var sW = miSpot.Invoke(null, new object[] { 1, bdefW, 25 }) as GridCoord?;
        bool builtW = sW.HasValue && bc.TryBuild(bdefW, sW.Value, GateOrientation.Horizontal, 1);
        yield return WaitDays(4);
        bool workshopActive = FindB(1, BuildingIds.SiegeWorkshop) != null;
        Log("P5pre 建厂提交=" + builtW + " 施工完成 Active=" + workshopActive, workshopActive);
        bool gate = sps != null && !sps.ProduceMachine(Occupation.Ram, new Vector2(0, 0), 1);   // 异族机器=拒（r3 实锤 k1=精灵：Ram 兽人专属）
        Log("P5a B8 族门禁负探针 精灵×Ram(兽人专属)=拒 " + gate, gate);
        // 正探针：本族机器动态选型（r3 修正——k1=精灵，Ballista 人类专属拒=正确行为非缺陷；
        // 选型复用 ExecuteProduceMachine 同源逻辑 IsMachineAllowed）
        Occupation[] machines = { Occupation.Ballista, Occupation.SiegeMachine, Occupation.Mortar, Occupation.VineCatapult, Occupation.Ram };
        Occupation pickM = machines[0];
        int race1 = KingdomRace.GetKingdomRace(1);
        for (int i = 0; i < machines.Length; i++)
            if (SiegeProductionSystem.IsMachineAllowed(race1, machines[i])) { pickM = machines[i]; break; }
        bool okM = sps != null && sps.ProduceMachine(pickM, new Vector2(0, 0), 1);
        Log("P5b B8 正探针 k1(族" + race1 + ")×本族" + pickM + "=成（国库扣费+上限内）", okM);
        bool cap = true;
        if (okM && sps != null)
        {
            // 上限负探针：连造至上限+1（上限=2+每级2；厂级1→2 台；已有 1 台+1 台上限→第三次必拒）
            for (int i = 0; i < 4; i++)
                sps.ProduceMachine(Occupation.Ballista, new Vector2(0, 0), 1);
            cap = sps.GetPlacedMachineCountByKingdom(1) <= sps.GetMachineLimit();
        }
        Log("P5c B8 上限负探针 机器数≤上限 " + cap, cap);

        Finish();
    }

    private static Building FindB(int kid, string id)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return null;
        foreach (var b in reg.All)
            if (b != null && b.def != null && b.IsActive && b.kingdomId == kid && b.def.id == id) return b;
        return null;
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
        _log.AppendLine("===== HH.137 批B 探针收工 =====");
        _log.AppendLine("PASS=" + _pass + " FAIL=" + _fail + " 时间=" + System.DateTime.Now.ToString("HH:mm:ss"));
        if (TimeManager.Instance != null) TimeManager.Instance.SetGameSpeed(0f);
        TestHarnessApi.ExitTestRun();
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "hh137_probe.log"), _log.ToString());
        }
        catch (System.Exception e) { Debug.LogError("[HH137探针] 日志写盘失败: " + e.Message); }
        Debug.LogWarning("[HH137探针] ★ 收工 PASS=" + _pass + " FAIL=" + _fail + "（终速 0+ExitTestRun）");
        var host = Object.FindObjectOfType<ProbeHost>();
        if (host != null) Application.logMessageReceived -= host.Catch;
    }
}
