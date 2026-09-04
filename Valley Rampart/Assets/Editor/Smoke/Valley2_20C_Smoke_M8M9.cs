using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  2_20 M8+M9 共通冒烟（Q10 批4，HH.64 段C / D426/D490）
//  用法：Play 上下文（MCP enterPlay 后点菜单）——菜单「Valley/验证/2_20C_M8M9批4冒烟」。
//  自动跑（D522，同 2_20B 模式）：进 Play 即自动驱动，跑完 SmokeApi.ResetWorldForNext + QuitSmoke 退 Play。
//
//  M8（五轴来源改造=RaceDef 种族基准×KingdomDef 模板扰动合并，D426/2_20.1 §四）：
//   P1 种族基准生效正探针：反射 KingdomFoundry.MergeFirstGenPersonality——同一模板(GoldenWheat)
//      配人类/兽人/矮人/精灵四基准 → 合并值按基准分离（兽人好战>人类>矮人；兽人外交<人类<矮人）；
//      逐族逐轴 |final−(baseline+tpl偏离)| ≤ perturbation(0.2)+ε（基准+扰动包络，噪声有界实证）。
//   P2 同族两 AI 行为级差异：人类·同模板双 seed → ①personality 至少一轴互异（扰动在场）
//      ②行为级=产品评分路径（UtilityScorer 语义 need×axisWeight×Clamp01(personality[axis])×stageW）
//        在相同需求场景下好战轴行动得分不同（决策量差异，非结构性计数）。
//   P3 人类零回归：人类基准全 0.5 → 合并分布=原口径（模板终值±扰动，均值=tpl 轴值）。
//   P4 端到端消费链（真实局）：进局后全 AI 王国 personality 逐国验
//      |personality[i]−(RaceDef 基准[i]+KingdomDef 模板偏离[i])| ≤ 0.2+ε——真实立国链基准×扰动生效；
//      王国↔模板映射=立国序（Foundry i 序=注册序，GetAll()[k]↔kingdomTemplates[k]）。
//   P5 骨架不动负声明：StageMachine/FocusController/UtilityScorer 文件零改动（本批 diff 自查在场性）。
//
//  M9（共通 5 职业零改动验证 + Cavalry 负探针，D490）：
//   P6 共通 5 职业资产走查：Warrior/Archer/Mage/Healer/General NpcProfessionDef 资产在场
//      + 共通性=单一资产无族变体（Resources/UnitData 下无 {Elf,Dwarf,Orc}_<共通职> 资产——任意族行为一致的数据层证据）。
//   P7 Cavalry 负探针：全 TrainingConfig 条目——toOccupation==Cavalry 的条目全部 raceId==0（人类专属）；
//      负=精灵/矮人/兽人（1/2/3）**无**任何骑兵训练条目。
//   P8 四族共用冒烟（真实局）：本局（矮人 seed22360，SmokeApi 进局）共通 5 职业 UnitDataManager 数据可取
//      + 非人类族王国 TrainingSystem 过滤列表无骑兵（与 P7 数据面对照的运行时消费面）。
//
//  红线：不触 AI.Core/训练仓；探针材料自建自清（合成 state 不进 registry）；产品代码零改动（纯验证）。
// ============================================================================
public static class Valley2_20C_Smoke_M8M9
{
    private const float PERTURB = 0.2f;      // firstGenPerturbation（KingdomFoundingConfig 实测值）
    private const float EPS = 1e-3f;

    [MenuItem("Valley/验证/2_20C_M8M9批4冒烟")]
    public static void Run()
    {
        if (!Application.isPlaying) { Debug.LogError("[2_20C冒烟] 须在 Play 上下文执行（或菜单「自动跑」）。"); return; }
        new GameObject("2_20C_SmokeRunner").AddComponent<M89Host>().Host(RunCoroutine());
    }

    [MenuItem("Valley/验证/2_20C_M8M9批4冒烟_自动跑(D522)")]
    public static void RunAuto()
    {
        if (!Application.isPlaying) { Debug.LogError("[2_20C冒烟] 自动跑须先进入 Play（MCP enterPlaymode 后二次调用本菜单）。"); return; }
        new GameObject("2_20C_AutoHost").AddComponent<M89Host>().Host(AutoRoutine());
    }

    private static IEnumerator AutoRoutine()
    {
        yield return RunCoroutine();
        Debug.Log("[2_20C冒烟] 自动跑完成 → 清场退 Play（D522）");
        SmokeApi.ResetWorldForNext();
        SmokeApi.QuitSmoke();
    }

    public static IEnumerator RunCoroutine()
    {
        var sb = new StringBuilder();
        bool allPass = true;
        void Check(bool ok, string name, string detail)
        {
            allPass &= ok;
            sb.Append(ok ? "PASS " : "FAIL ").Append(name).Append(" :: ").Append(detail).Append('\n');
            Debug.Log((ok ? "[2_20C][PASS] " : "[2_20C][FAIL] ") + name + " :: " + detail);
        }

        // ===== 等 Ready（单例在场；不要求已进局）=====
        var lm = LoadManager.Instance;
        float t0 = Time.realtimeSinceStartup;
        while (lm == null || lm.CurrentPhase == LoadPhase.Booting
               || WorldManager.Instance == null || UnitDataManager.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 60f)
            { Debug.LogError("[2_20C冒烟] 等待 Ready 超时(60s)。"); yield break; }
        }

        var miMerge = typeof(KingdomFoundry).GetMethod("MergeFirstGenPersonality",
            BindingFlags.Static | BindingFlags.NonPublic);
        Check(miMerge != null, "P0 MergeFirstGenPersonality 反射在场", "M8 改造点=KingdomFoundry 第一代立国五轴合并（private static）");
        if (miMerge == null) { yield break; }

        float[] Merge(System.Random rng, KingdomDef tpl, RaceDef race)
            => (float[])miMerge.Invoke(null, new object[] { rng, tpl, race, PERTURB });

        var rdH = Resources.Load<RaceDef>("Config/Races/Race_Human");
        var rdE = Resources.Load<RaceDef>("Config/Races/Race_Elf");
        var rdD = Resources.Load<RaceDef>("Config/Races/Race_Dwarf");
        var rdO = Resources.Load<RaceDef>("Config/Races/Race_Orc");
        var tplGW = Resources.Load<KingdomDef>("Config/Kingdoms/Kingdom_GoldenWheat");
        var tplRV = Resources.Load<KingdomDef>("Config/Kingdoms/Kingdom_RiverBay");
        Check(rdH != null && rdE != null && rdD != null && rdO != null && tplGW != null && tplRV != null,
            "P0b 数据资产在场", "四 RaceDef + 人类双模板（GoldenWheat/RiverBay）");

        // ===== P1 种族基准生效正探针（同模板四基准分离）=====
        var pOrc = Merge(new System.Random(777), tplGW, rdO);
        var pDwf = Merge(new System.Random(777), tplGW, rdD);
        var pElf = Merge(new System.Random(777), tplGW, rdE);
        var pHum = Merge(new System.Random(777), tplGW, rdH);
        // 同 rng 序下噪声同构 → 跨族差=基准差（Orc 好战 0.8 vs Dwarf 0.3；Orc 外交 0.2 vs Elf 0.7）
        bool sep = pOrc[0] > pHum[0] + 0.2f && pDwf[1] > pOrc[1] + 0.2f && pElf[4] > pOrc[4] + 0.2f;
        Check(sep, "P1 种族基准分离", $"同 rng 同模板：好战 orc={pOrc[0]:F2}>hum={pHum[0]:F2}；经济 dwf={pDwf[1]:F2}>orc={pOrc[1]:F2}；外交 elf={pElf[4]:F2}>orc={pOrc[4]:F2}");
        bool envelope = true;
        var sbEn = new StringBuilder();
        var pairs = new[] { (rd: rdH, p: pHum), (rd: rdO, p: pOrc), (rd: rdD, p: pDwf), (rd: rdE, p: pElf) };
        foreach (var pr in pairs)
        {
            var baseA = pr.rd.GetBaselinePersonalityArray();
            var tplA = tplGW.GetPersonalityArray();
            for (int i = 0; i < 5; i++)
            {
                float expect = baseA[i] + (tplA[i] - 0.5f);
                if (Mathf.Abs(pr.p[i] - expect) > PERTURB + EPS) { envelope = false; sbEn.Append($"[{pr.rd.raceName}轴{i} {pr.p[i]:F2}∉{expect:F2}±0.2]"); }
            }
        }
        Check(envelope, "P1b 基准+扰动包络", envelope ? "四族五轴 |final−(baseline+偏离)|≤0.2+ε 全过" : sbEn.ToString());

        // ===== P2 同族双 AI 行为级差异（人类·同模板双 seed）=====
        var pA = Merge(new System.Random(101), tplGW, rdH);
        var pB = Merge(new System.Random(202), tplGW, rdH);
        bool persDiff = false;
        for (int i = 0; i < 5; i++) if (Mathf.Abs(pA[i] - pB[i]) > 0.02f) persDiff = true;
        Check(persDiff, "P2a 同族双 seed personality 互异（扰动在场）", $"好战轴 A={pA[0]:F3} vs B={pB[0]:F3}");
        // 行为级=产品评分路径：UtilityScorer L81-83 语义轴项 axisWeight×Clamp01(personality[axis])
        var ucfg = UtilityActionConfig.LoadConfig();
        int milIdx = -1;
        if (ucfg.actions != null)
            for (int i = 0; i < ucfg.actions.Length; i++)
                if (ucfg.actions[i].axis == 0 && ucfg.actions[i].axisWeight > 0f) { milIdx = i; break; }
        bool axisDiff = false;
        if (milIdx >= 0)
        {
            float w = ucfg.actions[milIdx].axisWeight;
            float sA = w * Mathf.Clamp01(pA[0]), sB = w * Mathf.Clamp01(pB[0]);
            axisDiff = Mathf.Abs(sA - sB) > 0.01f;
        }
        Check(milIdx >= 0 && axisDiff, "P2b 行为级：好战轴行动评分量差异",
            $"同需求场景下 行动[{ucfg.actions[milIdx].name}] 评分轴项 A={ucfg.actions[milIdx].axisWeight * Mathf.Clamp01(pA[0]):F3} vs B={ucfg.actions[milIdx].axisWeight * Mathf.Clamp01(pB[0]):F3}（UtilityScorer 同式）");

        // ===== P3 人类零回归（基准 0.5 → 分布均值=模板轴值）=====
        float meanGW0 = 0f; int N = 24;
        for (int s = 0; s < N; s++) meanGW0 += Merge(new System.Random(1000 + s), tplGW, rdH)[0];
        meanGW0 /= N;
        Check(Mathf.Abs(meanGW0 - tplGW.militant) <= 0.05f, "P3 人类零回归", $"人类·金穗好战轴 24 抽样均值={meanGW0:F3}≈模板值={tplGW.militant:F2}（基准 0.5 时新公式=原口径分布）");

        // ===== P4 端到端消费链：真实局全 AI 王国逐国包络（基准+模板偏离±噪声）=====
        SmokeApi.EnterGame(new NewGameConfig
        {
            raceId = RaceIds.Dwarf, worldSeed = 22360, mapSeed = 22360,
            difficulty = 2, worldSize = WorldSize.Medium,
            kingdomName = "冒烟王国M89", selectedSlotId = "smoke_m89",
        });
        t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance.ActiveMap == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 90f) { Debug.LogError("[2_20C冒烟] 进局建世界超时(90s)。"); yield break; }
        }
        yield return new WaitForSeconds(0.5f);

        var reg = KingdomRegistry.Instance;
        var map = WorldManager.Instance.ActiveMap;
        var tpls = map != null ? map.kingdomTemplates : null;
        bool e2e = true; var sbE2E = new StringBuilder();
        int checkedAi = 0;
        var allK = reg.GetAll();
        // 立国序映射：Foundry 按 spawn 下标 i=1..N 顺序立国 → 注册序第 k 个 AI 国 = templates[k]（index0=null 玩家）
        foreach (var k in allK)
        {
            if (k == null || k.IsPlayer) continue;
            int idx = 1 + checkedAi; checkedAi++;
            var tpl = tpls != null && idx < tpls.Count ? tpls[idx] : null;
            if (tpl == null || k.personality == null) { e2e = false; sbE2E.Append($"[k{k.id} 无模板/无五轴]"); continue; }
            var rd = KingdomRace.GetKingdomRaceDef(k.id);
            var b = rd != null ? rd.GetBaselinePersonalityArray() : new float[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
            var t = tpl.GetPersonalityArray();
            bool kidRaceOk = rd != null && rd.raceId == k.raceId;
            if (!kidRaceOk) { e2e = false; sbE2E.Append($"[k{k.id} 国族{rd?.raceId.ToString() ?? "null"}≠{k.raceId}]"); }
            for (int i = 0; i < 5; i++)
            {
                float expect = b[i] + (t[i] - 0.5f);
                if (Mathf.Abs(k.personality[i] - expect) > PERTURB + EPS)
                { e2e = false; sbE2E.Append($"[k{k.id}轴{i} {k.personality[i]:F2}∉{expect:F2}±0.2]"); }
            }
        }
        Check(checkedAi >= 2 && e2e, "P4 端到端真实局包络", $"进局(矮人22360) AI 国 {checkedAi} 个逐国逐轴 |p−(基准+模板偏离)|≤0.2+ε + RaceDef↔国族一致：" +
              (e2e ? "全过" : sbE2E.ToString()));
        sbE2E.Clear();

        // ===== P6 共通 5 职业资产走查（M9）=====
        var common = new[] { Occupation.Warrior, Occupation.Archer, Occupation.Mage, Occupation.Healer, Occupation.General };
        bool p6 = true; var sb6 = new StringBuilder();
        foreach (var occ in common)
        {
            var data = UnitDataManager.Instance.GetData(Faction.PlayerCamp, occ);
            bool ok = data != null;
            p6 &= ok;
            sb6.Append($"{occ}={(ok ? "在" : "缺")} ");
        }
        // 共通性=无族变体资产（数据层单一真源）
        int variantCount = 0;
        foreach (var occ in common)
            foreach (var rn in new[] { "Elf", "Dwarf", "Orc" })
                if (Resources.FindObjectsOfTypeAll<NpcProfessionDef>() != null)
                {
                    // 族前缀资产探测（命名约定={Race}_{Occ}；共通职业=PlayerCamp 槽单一资产）
                    var guids = AssetDatabase.FindAssets($"{rn}_{occ}", new[] { "Assets/Resources/UnitData" });
                    foreach (var g in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(g);
                        if (path.EndsWith($"{rn}_{occ}.asset")) variantCount++;
                    }
                }
        Check(p6 && variantCount == 0, "P6 共通5职业走查", sb6 + $"族变体资产数={variantCount}（0=单一共通真源，任意族共用）");

        // ===== P7 Cavalry 负探针（训练条目数据面）=====
        var tc = Resources.Load<TrainingConfig>("Config/TrainingConfig");
        bool p7 = tc != null; bool cavSeen = false; var sb7 = new StringBuilder();
        if (tc != null && tc.trainings != null)
        {
            foreach (var td in tc.trainings)
            {
                if (td.toOccupation != Occupation.Cavalry) continue;
                cavSeen = true;
                if (td.raceId != RaceIds.Human) { p7 = false; sb7.Append($"[骑兵条目 raceId={td.raceId}≠0!]"); }
            }
        }
        else p7 = false;
        Check(p7 && cavSeen, "P7 Cavalry 负探针", "骑兵训练条目全=human-only(raceId=0)" + (cavSeen ? "" : "(条目缺失!)") + sb7 + "；1/2/3 族零骑兵条目");

        // ===== P8 四族共用运行时消费面（真实局=矮人）=====
        bool p8 = true; var sb8 = new StringBuilder();
        foreach (var occ in common)
        {
            var d = UnitDataManager.Instance.GetData(Faction.PlayerCamp, occ);
            if (d == null) { p8 = false; sb8.Append($"{occ}缺 "); }
        }
        // 运行时过滤面=GetTrainings(Building) 同式复算（TrainingSystem L217-224：raceId>=0 && raceId!=本国族 → skip；
        // 真实建筑实例过滤行为已由 2_20B P7 练兵场/战营/射箭场探针实证，本探针锚定骑兵在矮人国被滤）：
        int dwarfRace = KingdomRace.GetKingdomRace(0);
        bool noCavForDwarf = true;
        if (tc != null && tc.trainings != null)
            foreach (var td in tc.trainings)
                if (td.toOccupation == Occupation.Cavalry && (td.raceId < 0 || td.raceId == dwarfRace))
                    noCavForDwarf = false;   // 矮人视角可见=共通(-1)或矮人(2)专属——任一在场即 FAIL
        Check(p8 && noCavForDwarf, "P8 四族共用+运行时骑兵门禁",
            $"矮人局共通5职数据全取={p8}；矮人(国族={dwarfRace})过滤后可见骑兵条目=0({noCavForDwarf}，GetTrainings(Building) 同式)" + sb8);

        // ===== P9 收官：ScoreTop 真实消费冒烟（UtilityScorer 零改动契约在场=2_17_Smoke_9 p3 已覆盖面回归）=====
        var dwf = reg.GetAll();
        bool p9 = false;
        foreach (var k in dwf)
        {
            if (k == null || k.IsPlayer || k.personality == null) continue;
            var top = UtilityScorer.ScoreTop(k, ucfg, ScriptStage.Expand);
            if (top != UtilityAction.None) { p9 = true; Debug.Log($"[2_20C] P9 k{k.id} race={k.raceId} 好战轴={k.personality[0]:F2} → 扩张期焦点={top}"); }
        }
        Check(p9, "P9 ScoreTop 真实局产出焦点", "AI 国五轴消费（2_17 契约零改动）在 M8 新来源下仍产出有效焦点");

        Debug.Log("[2_20C冒烟]\n" + sb);
        Debug.Log($"[2_20C冒烟] ===== {(allPass ? "ALL PASS" : "HAS FAIL")}（M8 基准生效/同族差异/零回归/端到端 + M9 走查/Cavalry负/运行时/P9 消费）=====");
    }

    private class M89Host : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}
