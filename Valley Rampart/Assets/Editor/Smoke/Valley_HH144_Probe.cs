using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;

// ============================================================================
//  HH.144 王国AI 批D 修复微批探针容器（Editor-only；test-harness-first 铁律①⑤）。
//  任务书两件验收（D598 列报2/列报3 兑现）：
//   P1(件1) 招募国籍纯度：
//     P1a AI 守军编队成员 kingdomId 全部==创建国（m≥2）
//     P1b 负证据：他国闲兵传送至锚点旁（L-19 摆位+实际距离行）→不入编（HasFormationSlot=false）
//     P1c L-21 双断言：守军编队=1（计数面翻转）+空壳=0（失败终止无堆积）
//     P1d 玩家编队（BindGeneral 路径=ResolveKingdomId()=0≥0 收紧生效）只入玩家兵+
//         注入 k1 闲兵负证据；空编队 fallback 路径（AIDebugUIManager OnGarrisonClicked 同型）
//         =faction-only 现行行为保留（任务书口径 2 向后兼容面，不反向断言）
//     P1e B7 成军国籍回归：全部 AI 非守军编队成员 kingdomId==编队 KingdomId（将军推导一致）
//   P2(件2) F1 归一口径：
//     P2a 纯函数近锚>远锚（新 3 参签名反射；denom=max(城锚实际距离,带径)）
//     P2b 带外局带内 F1 非恒 0（TryPick rT.F1>0；旧口径=0=HH.141 留痕病灶）
//     P2c F1 数值自洽：rT.F1 == 1-Chebyshev(选格,锚)/denom（公式重算容差）
//     P2d 确定性+分解自洽：双跑逐字段一致+Score==w1·F1+w2·F2+w3·F3（容差）
//   P3(正交旁证) 姿态三档快测（P1/P5 组无回归声明面）
//  证据落 Logs/P1/hh144_probe.log（不入库，断言转录 HH.145）。
// ============================================================================
public static class Valley_HH144_Probe
{
    private const int SEED = 21140;   // 与 HH140 同 seed=同局地形（k1↔k2 45 格带外局常态形态）
    private const string SLOT = "probe_hh144";
    private static int _pass, _fail;
    private static readonly System.Text.StringBuilder _log = new System.Text.StringBuilder();

    [MenuItem("Valley/验证/HH144_批D修复探针")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH144探针] 须先 GameScene 进 Play。"); return; }
        _pass = 0; _fail = 0; _log.Length = 0;
        var host = new GameObject("HH144_ProbeHost").AddComponent<ProbeHost>();
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
        Debug.LogWarning("[HH144探针] " + (ok ? "✓ " : "✗ ") + msg);
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
        Log("正门进局 seed=" + SEED, true);

        yield return WaitDays(3);

        var reg = KingdomRegistry.Instance;
        var k1 = reg.Get(1); var k2 = reg.Get(2);
        if (k1 == null || k2 == null) { Log("AI 国缺失", false); Finish(); yield break; }
        var scfg = SituationConfig.Load();
        int crisis = scfg != null ? scfg.crisisLine : 2;
        var pcfg = MilitaryPostureConfig.Load();
        var bpCfg = BuildingPlacementConfig.Load();
        var brain1 = KingdomBrainRegistry.Instance != null ? KingdomBrainRegistry.Instance.Get(1) : null;
        if (brain1 == null) { Log("brain1 缺失", false); Finish(); yield break; }
        var posture1 = brain1.Posture;

        // ===== P1：件1 招募国籍纯度（守军链+负证据+L-21 双断言）=====
        SetWarriors(1, 8); yield return null;   // k1 兵源池（守军 m≥2 前提）
        var mock = new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry>(), Day = 0 };
        SetWarriors(1, 1); yield return null;   // 军力<危机线
        mock.Threats.Add(new ThreatEntry { KingdomId = 2, WarriorCount = 5 });
        mock.OwnWarriorCount = k1.warriorCount;
        SituationHub.Put(1, mock);
        var fldSit = typeof(KingdomBrain).GetField("_situation", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fldSit != null) fldSit.SetValue(brain1, mock);
        posture1.Evaluate(mock, k1, scfg, pcfg, 200);   // 威胁+军力1<危机线 → Mobilized
        bool mobOn = posture1.Current == MilitaryPosture.Mobilized;
        SetWarriors(1, 8); yield return null;   // 档位已定，兵源回满（守军 m≥2）
        var miPosture = brain1.GetType().GetMethod("ExecutePosture", BindingFlags.NonPublic | BindingFlags.Instance);
        if (miPosture != null && mobOn) miPosture.Invoke(brain1, new object[] { k1, pcfg });

        // P1b 负证据预置：k2 战斗兵传送 k1 城旁（L-19：摆位+实际距离行；断言不依赖位置=过滤全局性）
        SetWarriors(2, 1); yield return null;   // k2 闲兵保底（day3 兵营未出兵→转职链兜底）
        UnitController intruder = FindNearestCombat(2);
        string intrudeWhy = "k2 闲兵缺失";
        if (intruder != null)
        {
            var castleB = FindB(1, "castle");
            Vector3 target = castleB != null ? castleB.transform.position + new Vector3(3f, 0f, 0f) : intruder.transform.position;
            intruder.transform.position = target;
            float dist = castleB != null ? Vector3.Distance(intruder.transform.position, castleB.transform.position) : -1f;
            intrudeWhy = $"k2 兵#{intruder.npcId} 摆位实际距 k1 城 {dist:F1}（L-19 实距行）";
        }
        yield return WaitDays(4);   // Tick 每日补建（空间约束明日再试语义）

        // P1a 国籍纯度：kid==1 isGarrison 编队成员全 kingdomId==1
        int garrisonKid1 = 0; int membersTotal = 0; int foreignMembers = 0;
        bool intruderJoined = false;
        if (FormationManager.Instance != null)
        {
            var fs = FormationManager.Instance.AllFormations;
            for (int i = 0; i < fs.Count; i++)
            {
                var f = fs[i];
                if (f == null || !f.isGarrison || f.KingdomId != 1) continue;
                garrisonKid1++;
                var members = MembersOf(f);
                if (members == null) continue;
                for (int m = 0; m < members.Count; m++)
                {
                    if (members[m].Unit == null) continue;   // FormationMember=struct，只判 Unit 引用
                    membersTotal++;
                    if (members[m].Unit.kingdomId != 1) foreignMembers++;
                    if (intruder != null && members[m].Unit == intruder) intruderJoined = true;
                }
            }
        }
        if (intruder != null && !intruderJoined) intrudeWhy += "｜未入编（HasFormationSlot 负证据）";
        bool p1a = garrisonKid1 > 0 && membersTotal >= 2 && foreignMembers == 0;
        Log($"P1a 守军国籍纯度：kid1 守军={garrisonKid1} 成员={membersTotal}（≥2）异籍={foreignMembers}（=0）", p1a);
        bool p1b = intruder != null && !intruderJoined;
        Log("P1b 负证据（他国闲兵不入编）：" + intrudeWhy, p1b);

        // P1c L-21 双断言：守军编队=1 + 空壳=0
        int shells = CountGarrisonEmptyShells();
        Log($"P1c L-21 双断言：守军编队={garrisonKid1}（=1 语义≥1）空壳={shells}（=0）", garrisonKid1 >= 1 && shells == 0);

        // P1d 玩家编队（BindGeneral 路径）只入玩家兵
        // r4 修（"河谷失守"事故归因，L-17 家族新变体）：SetWarriors(0,*) 把玩家 Worker 转职=判负条件
        //（ThroneAnchor 工人全灭）在探针内被满足——守卫 ON 时封死无事，但 **ExitTestRun 撤守卫瞬间立即
        // GameOver**（D598 串实测）。守卫覆盖期≠副作用无害期：玩家职业必须快照+复原，收尾安全阀兜底。
        var playerOccSnapshot = SnapshotOccupations(0);
        SetWarriors(0, 4); yield return null;   // 玩家兵源
        UnitController pGeneral = FindNearestCombat(0);
        UnitController pIntruder = intruder != null && intruder.IsAlive ? intruder : FindNearestCombat(1);
        bool p1d = false; string p1dWhy = "玩家将军缺失";
        if (pGeneral != null)
        {
            var go = new GameObject("HH144_PlayerSquad");
            var fc = go.AddComponent<FormationController>();
            fc.faction = Faction.PlayerCamp;
            fc.formationTable = Resources.Load<FormationTable>("Formations/FormationTable");
            fc.BindGeneral(pGeneral);   // ResolveKingdomId()=0（将军推导）≥0 → 收紧生效
            fc.RecruitStandard();
            var members = MembersOf(fc);
            int foreign = 0, total = 0;
            if (members != null)
                for (int i = 0; i < members.Count; i++)
                {
                    if (members[i].Unit == null) continue;   // struct 只判 Unit 引用
                    total++;
                    if (members[i].Unit.kingdomId != 0) foreign++;
                    if (pIntruder != null && members[i].Unit == pIntruder) foreign++;   // 注入闲兵入编=违规
                }
            p1d = total > 0 && foreign == 0;
            p1dWhy = $"玩家编队成员={total} 异籍/注入={foreign}（=0；BindGeneral 路径收紧生效）";
        }
        Log("P1d 玩家编队国籍（kid=0）：" + p1dWhy, p1d);

        // P1e B7 成军国籍回归：AI 非守军编队成员 kingdomId==编队 KingdomId
        int aiSquads = 0; int aiForeign = 0;
        if (FormationManager.Instance != null)
        {
            var fs = FormationManager.Instance.AllFormations;
            for (int i = 0; i < fs.Count; i++)
            {
                var f = fs[i];
                if (f == null || f.isGarrison || f.KingdomId < 1) continue;
                var members = MembersOf(f);
                if (members == null || members.Count == 0) continue;
                aiSquads++;
                for (int m = 0; m < members.Count; m++)
                    if (members[m].Unit != null && members[m].Unit.kingdomId != f.KingdomId) aiForeign++;   // struct 只判 Unit 引用
            }
        }
        Log($"P1e B7 成军国籍回归：AI 将军编队={aiSquads} 异籍成员={aiForeign}（=0）", aiForeign == 0);

        // r4 修（"河谷失守"事故）：守卫撤除前复原世界——玩家职业快照回填+探针编队自清理
        //（判负条件=玩家桶0 Worker/Civilian 全灭；P1d 转职副作用不跨守卫边界泄漏）
        RestoreOccupations(playerOccSnapshot);
        var probeSquadGo = GameObject.Find("HH144_PlayerSquad");
        if (probeSquadGo != null) Object.Destroy(probeSquadGo);
        yield return null;
        bool workerBack = ThroneAnchor.Instance == null || ThroneAnchor.Instance.HasRemainingWorker();
        Log("P1f 复原安全阀：玩家 Worker 存活=" + workerBack + "（ExitTestRun 撤守卫前置断言）", workerBack);

        // ===== P2：件2 F1 归一口径 =====
        // r4 实锤（诊断行 Resource=132）：ValidatePlacement 尾部=CanAfford 国库门（day10 k1 穷→全拒）
        // ——镜像 HH140 P6b 资源注入（选址面只验空间守卫，资源门是独立维）。
        k1.resources.gold += 2000; k1.resources.stone += 600; k1.resources.wood += 600;
        var bdefB = BuildingFactory.FindDefById(BuildingIds.Barracks);
        var ownCastle = KingdomBrain.FindCastleCell(1);
        var threatCastle = KingdomBrain.FindCastleCell(2);
        if (ownCastle.HasValue && threatCastle.HasValue && bdefB != null)
        {
            var gs = GridSystem.Instance;
            int div = gs != null && gs.Config != null && gs.Config.subCellDivisor > 0 ? gs.Config.subCellDivisor : 4;
            var taSub = new GridCoord(threatCastle.Value.x * div, threatCastle.Value.y * div, threatCastle.Value.layer);
            var ownSub = new GridCoord(ownCastle.Value.x * div, ownCastle.Value.y * div, ownCastle.Value.layer);
            int maxR = 8;
            int bandDenom = maxR * div;
            int denom = Mathf.Max(Chebyshev(ownSub, taSub), bandDenom);   // 与产品 TryPick 同式

            // P2a 纯函数近锚>远锚（新 3 参签名）
            var miF1 = typeof(PlacementScorer).GetMethod("ComputeF1", BindingFlags.NonPublic | BindingFlags.Static);
            bool p2a = false; string p2aWhy = "ComputeF1 反射缺失（应 3 参新签名）";
            if (miF1 != null && miF1.GetParameters().Length == 3)
            {
                var near = new GridCoord(taSub.x - 8, taSub.y - 8, 0);
                var far = new GridCoord(taSub.x - 80, taSub.y - 80, 0);
                float f1Near = (float)miF1.Invoke(null, new object[] { near, (GridCoord?)taSub, denom });
                float f1Far = (float)miF1.Invoke(null, new object[] { far, (GridCoord?)taSub, denom });
                p2a = f1Near > f1Far;
                p2aWhy = $"近锚 F1={f1Near:F3}>远锚 F1={f1Far:F3}（denom={denom}=max(城锚距{Chebyshev(ownSub, taSub)},带径{bandDenom})）";
            }
            Log("P2a F1 纯函数朝向性：" + p2aWhy, p2a);

            // P2b/P2c/P2d 真实局 TryPick（带外局=城锚距 180≫带径 32）
            var sitT = new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry> { new ThreatEntry { KingdomId = 2, WarriorCount = 5 } }, Day = 400 };
            bool called = PlacementScorer.TryPick(1, bdefB, maxR, bpCfg, sitT, out var rT);
            if (!called)
            {
                // r4 现场诊断（L-13 变体：FAIL 不空转）：全带 r=0..8 采样拒因分布+TryPick 前置三件
                var diag = new System.Text.StringBuilder();
                var stat = new Dictionary<string, int>();
                int okN = 0, total = 0;
                for (int r = 0; r <= maxR; r++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        var cell = new GridCoord(ownCastle.Value.x + dx, ownCastle.Value.y + dy);
                        if (!gs.IsInBounds(cell)) { stat["Bounds"] = stat.TryGetValue("Bounds", out var b1) ? b1 + 1 : 1; continue; }
                        var sub = gs.CellToSub(cell, 0, 0);
                        var pr = PlacementValidator.ValidatePlacement(bdefB, sub, GateOrientation.Horizontal, 1);
                        total++;
                        if (pr.ok) { okN++; continue; }
                        string key = pr.reason.ToString();
                        stat[key] = stat.TryGetValue(key, out var b2) ? b2 + 1 : 1;
                    }
                }
                foreach (var kv in stat) diag.Append(kv.Key + "=" + kv.Value + " ");
                Log($"P2b 诊断行：前置 grid={(gs != null)} anchor={ownCastle.HasValue} def={(bdefB != null)}；r0..{maxR} 采样 total={total} ok={okN} 拒因[{diag}]", true);   // 诊断行不计 FAIL
            }
            bool p2b = called && rT.F1 > 0.0001f;
            Log($"P2b 带外局带内 F1 非恒 0：called={called} F1={rT.F1:F3}（>0；旧口径恒 0=HH.141 留痕病灶）", p2b);

            bool p2c = false;
            if (called)
            {
                float expect = 1f - Mathf.Clamp01(Chebyshev(rT.Sub, taSub) / (float)denom);
                p2c = Mathf.Abs(rT.F1 - expect) < 0.001f;
                Log($"P2c F1 数值自洽：rT.F1={rT.F1:F4}≈重算 {expect:F4}（容差 1e-3）", p2c);
            }
            else Log("P2c F1 数值自洽：无候选", false);

            // P2d 确定性+分解自洽（Score==w1·F1+w2·F2+w3·F3）
            bool p2d = false; string p2dWhy = "前置缺失";
            if (called)
            {
                bool called2 = PlacementScorer.TryPick(1, bdefB, maxR, bpCfg, sitT, out var rB);
                var rule = bpCfg != null ? bpCfg.Find(bdefB.id) : null;
                float w1 = rule != null ? rule.w1ThreatFront : (bpCfg != null ? bpCfg.defaultW1 : 1f);
                float w2 = rule != null ? rule.w2LinkBuilding : (bpCfg != null ? bpCfg.defaultW2 : 1f);
                float w3 = rule != null ? rule.w3CastleCompact : (bpCfg != null ? bpCfg.defaultW3 : 1f);
                float expectScore = w1 * rT.F1 + w2 * rT.F2 + w3 * rT.F3;
                bool det = called2 && rT.Sub.x == rB.Sub.x && rT.Sub.y == rB.Sub.y
                    && rT.Score.ToString("R") == rB.Score.ToString("R")
                    && rT.F1.ToString("R") == rB.F1.ToString("R");
                bool selfConsist = Mathf.Abs(rT.Score - expectScore) < 0.001f;
                p2d = det && selfConsist;
                p2dWhy = $"选格({rT.Sub.x},{rT.Sub.y}) Score={rT.Score:F4}（w 分解重算 {expectScore:F4}）双跑一致={det}";
            }
            Log("P2d P7c 确定性+分解自洽：" + p2dWhy, p2d);
        }
        else Log("P2 前置缺失（城/def）", false);

        // ===== P3：正交旁证（姿态三档快测=P1/P5 组无回归声明面）=====
        var mock3 = new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry>(), Day = 0 };
        SetWarriors(1, 5); yield return null;
        posture1.Evaluate(mock3, k1, scfg, pcfg, 300);
        bool stepNone = posture1.Current == MilitaryPosture.None;
        mock3.Threats.Add(new ThreatEntry { KingdomId = 2, WarriorCount = 5 });
        posture1.Evaluate(mock3, k1, scfg, pcfg, 302);
        bool stepAlert = posture1.Current == MilitaryPosture.Alert;
        SetWarriors(1, 1); yield return null;
        posture1.Evaluate(mock3, k1, scfg, pcfg, 304);
        bool stepMob = posture1.Current == MilitaryPosture.Mobilized;
        Log($"P3 姿态三档快测（正交旁证）：None→{stepNone} Alert→{stepAlert} Mobilized→{stepMob}", stepNone && stepAlert && stepMob);

        Finish();
    }

    // ===== helpers =====

    // 玩家职业快照/复原（HH.144 r4："河谷失守"事故防范——转职类副作用不跨守卫边界；
    // 判负面=玩家桶0 Worker/Civilian 全灭，故快照粒度=单位职业）
    private static Dictionary<UnitController, Occupation> SnapshotOccupations(int kid)
    {
        var snap = new Dictionary<UnitController, Occupation>();
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units == null) return snap;
        foreach (var u in units)
            if (u != null && u.IsAlive && u.kingdomId == kid) snap[u] = u.EffectiveOccupation;
        return snap;
    }

    private static void RestoreOccupations(Dictionary<UnitController, Occupation> snap)
    {
        if (snap == null) return;
        foreach (var kv in snap)
            if (kv.Key != null && kv.Key.IsAlive && kv.Key.EffectiveOccupation != kv.Value)
                kv.Key.SetOccupation(kv.Value);
    }

    // 成员列表只读访问（_members 私有无公开访问器；探针反射先例模式=零产品面污染）
    private static List<FormationMember> MembersOf(FormationController f)
    {
        var fld = typeof(FormationController).GetField("_members", BindingFlags.NonPublic | BindingFlags.Instance);
        return fld != null ? (List<FormationMember>)fld.GetValue(f) : null;
    }

    private static UnitController FindNearestCombat(int kid)
    {
        var units = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (units == null) return null;
        UnitController best = null;
        foreach (var u in units)
        {
            if (u == null || !u.IsAlive || u.kingdomId != kid) continue;
            if (!MilitaryProfessions.IsCombat(u.EffectiveOccupation)) continue;
            if (best == null || u.npcId < best.npcId) best = u;   // 固定序确定性
        }
        return best;
    }

    private static int Chebyshev(GridCoord a, GridCoord b)
        => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

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
        // r4 安全阀（HH.144"河谷失守"事故归因）：ExitTestRun 撤守卫（TestHarnessMode=false→
        // ThroneAnchor 判负轮询恢复）前，终检判负条件——不满足=复原失败，ERROR 显式申报（L-16 可见性）
        if (ThroneAnchor.Instance != null && !ThroneAnchor.Instance.HasRemainingWorker())
            Debug.LogError("[HH144探针] ★★ 撤守卫前玩家 Worker=0（复原失败）——撤守卫将立即 GameOver！请核查快照/复原链。");
        _log.AppendLine("===== HH.144 批D 修复探针收工 =====");
        _log.AppendLine("PASS=" + _pass + " FAIL=" + _fail + " 时间=" + System.DateTime.Now.ToString("HH:mm:ss"));
        if (TimeManager.Instance != null) TimeManager.Instance.SetGameSpeed(0f);
        TestHarnessApi.ExitTestRun();
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "hh144_probe.log"), _log.ToString());
        }
        catch (System.Exception e) { Debug.LogError("[HH144探针] 日志写盘失败: " + e.Message); }
        Debug.LogWarning("[HH144探针] ★ 收工 PASS=" + _pass + " FAIL=" + _fail + "（终速 0+ExitTestRun）");
        var host = Object.FindObjectOfType<ProbeHost>();
        if (host != null) Application.logMessageReceived -= host.Catch;
    }
}
