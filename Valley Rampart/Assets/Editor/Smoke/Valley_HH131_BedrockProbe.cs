using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;

// ============================================================================
//  HH.131 件1 磐石（Bedrock）20→10 行为级补测（T8.1；Editor-only 冒烟容器）
//  规格真源：多Agent交接/策划端/HH.131_六考后零碎包_任务书.md §二 件1
//  入口=HH.92 测试环境正门 TestHarnessApi.EnterTestRun（L-17 正门纪律，动单位生命周期）；
//  收尾 TestHarnessApi.ExitTestRun + SmokeApi.QuitSmoke。
//
//  替身直调法（绕开 Bedrock prefab 缺失：Dwarf_Bedrock.asset prefab=null，生产链不可实例化）：
//    1) spawn 一个在场单位（生产 Prefab 实例化，禁裸 AddComponent——2_13 P4）；
//    2) Play 态内存载入磐石三值：内存克隆 Dwarf_Bedrock（Occ31）NpcProfessionDef
//       → 借用在场生产 prefab（Warrior）→ 直经 UnitFactory.SpawnUnit 生产链实例化
//       （克隆=内存实例态，SO 资产零污染；armorK 取 DamageConfig 实时值）；
//    3) 直调 DamageSystem.ApplyDamage(20, isRanged:true)。
//
//  断言=实收值对照 HH.115 容器 T6 公式复算基准 ±1（不硬编码终值，以 DamageConfig 实时链计算为准）。
//  探针：P1 三值载入读回 / P2 远程 20 实收=公式基准±1 / P3 同单位近战链（45% 不触发）/
//        P4 非磐石对照单位远程无 45%（证明减伤=磐石特有）。
//  禁写回 SO 资产（内存克隆，测试后单位销毁）。
// ============================================================================
public static class Valley_HH131_BedrockProbe
{
    private const int SEED = 21131;
    private const string SLOT = "probe_hh131";
    private const string TAG = "[HH131磐石]";
    private static int _pass, _fail;
    private static readonly System.Text.StringBuilder _log = new System.Text.StringBuilder();

    [MenuItem("Valley/验证/HH131_磐石行为级补测")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError(TAG + " 须先 GameScene 进 Play。"); return; }
        _pass = 0; _fail = 0; _log.Length = 0;
        var host = new GameObject("HH131_BedrockHost").AddComponent<ProbeHost>();
        Application.logMessageReceived += host.Catch;
        host.Host(Coroutine());
    }

    private class ProbeHost : MonoBehaviour
    {
        public void Host(IEnumerator r) => StartCoroutine(r);
        public void Catch(string cond, string stack, LogType t)
        {
            if (t == LogType.Exception) Log(TAG + " EXCEPTION: " + cond + "\n" + stack, false);
        }
    }

    private static void Log(string msg, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + msg);
        if (ok) _pass++; else _fail++;
        Debug.LogWarning(TAG + " " + (ok ? "✓ " : "✗ ") + msg);
    }

    private static IEnumerator Coroutine()
    {
        var cfg = new NewGameConfig
        {
            mapSeed = SEED, worldSeed = SEED, raceId = 2, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = SLOT, kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 30f);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || UnitDataManager.Instance == null || UnitFactory.Instance == null
               || DamageSystem.Instance == null || GridSystem.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Log("等世界就绪超时", false); Finish(); yield break; }
        }
        yield return new WaitForSeconds(0.5f);
        Log("正门进局 seed=" + SEED + "（30x）", true);

        // ---- 前置：磐石真资产 + DamageConfig 实时值 ----
        var umd = UnitDataManager.Instance;
        var bedrockDef = umd.GetData(Faction.PlayerCamp, Occupation.Bedrock) as NpcProfessionDef;
        var warriorDef = umd.GetData(Faction.PlayerCamp, Occupation.Warrior) as NpcProfessionDef;
        var dmgCfg = Resources.Load<DamageConfig>("Config/DamageConfig");
        if (bedrockDef == null || warriorDef == null || warriorDef.prefab == null || dmgCfg == null)
        {
            Log($"前置缺失 bedrockDef={bedrockDef != null} warriorDef={warriorDef != null} " +
                $"prefab={(warriorDef != null && warriorDef.prefab != null)} dmgCfg={dmgCfg != null}", false);
            Finish(); yield break;
        }
        float armorK = dmgCfg.armorK;
        Log($"前置：Dwarf_Bedrock（Occ31）def={bedrockDef.defense} rangedDamageReduce={bedrockDef.rangedDamageReduce} " +
            $"baseDamageReduce={bedrockDef.baseDamageReduce} prefab={(bedrockDef.prefab != null ? "有" : "缺失")}｜DamageConfig.armorK={armorK}", true);

        Vector2 anchor = WorldManager.Instance.GetKingdomAnchorWorld();
        float cs = GridSystem.Instance.Config != null ? GridSystem.Instance.Config.cellSize.x : 2.26f;

        // ---- 替身法步骤1+2：内存克隆磐石 def + 借生产 prefab → 生产链实例化 ----
        var bedrockSub = Object.Instantiate(bedrockDef);
        bedrockSub.name = "HH131_BedrockSubstitute";
        bedrockSub.prefab = warriorDef.prefab;   // 借在场生产 prefab（内存态，不入盘不入档）

        Vector2 posTarget = SpawnPosSnapper.SnapWorld(anchor + new Vector2(-25 * cs, 0), "HH131磐石");
        var goT = UnitFactory.Instance.SpawnUnit(bedrockSub, posTarget, 0);
        var target = goT != null ? goT.GetComponent<UnitController>() : null;
        if (target == null)
        {
            Log("替身单位生产链实例化失败（unit==null）", false);
            Finish(); yield break;
        }
        // 伤害源（非磐石职业，unitDamageMul 应为 1；隔离盾卫庇护——预制盾卫不在此点）
        var warriorSrcDef = umd.GetData(Faction.PlayerCamp, Occupation.Warrior) as NpcProfessionDef;
        Vector2 posSrc = SpawnPosSnapper.SnapWorld(anchor + new Vector2(-25 * cs, 10 * cs), "HH131源");
        var goS = UnitFactory.Instance.SpawnUnit(warriorSrcDef, posSrc, 0);
        var source = goS != null ? goS.GetComponent<UnitController>() : null;
        // 非磐石对照单位（生产链，真 Warrior）
        Vector2 posCtl = SpawnPosSnapper.SnapWorld(anchor + new Vector2(-25 * cs, -10 * cs), "HH131对照");
        var goC = UnitFactory.Instance.SpawnUnit(warriorSrcDef, posCtl, 0);
        var control = goC != null ? goC.GetComponent<UnitController>() : null;
        yield return null;
        yield return new WaitForSeconds(0.5f);

        if (source == null || control == null)
        {
            Log($"伤害源/对照单位直建失败 src={source != null} ctl={control != null}", false);
            Finish(); yield break;
        }
        var srcNd = source.Data as NpcProfessionDef;
        Log($"伤害源 unitDamageMul={(srcNd != null ? srcNd.unitDamageMul : -1)}（应=1，否则乘数污染）", srcNd != null && Mathf.Approximately(srcNd.unitDamageMul, 1f));

        // 摆位强控（直调链用实时 GetPosition 距离；盾卫庇护扫描半径 2 格，源离目标 ≥10 格）
        yield return null;

        // ===== P1 三值载入读回断言（内存态=磐石值）=====
        var rt = target.Data as NpcProfessionDef;
        bool p1 = rt != null
                  && rt == bedrockSub
                  && Mathf.Abs(rt.rangedDamageReduce - 0.45f) < 1e-4f
                  && rt.defense == 10
                  && target.Defense == 10
                  && Mathf.Abs(rt.rangedDamageReduce - bedrockDef.rangedDamageReduce) < 1e-4f;
        Log($"P1 三值载入读回：Data==内存克隆={rt == bedrockSub} rangedDamageReduce={rt?.rangedDamageReduce}（=0.45）" +
            $" defense={rt?.defense}（=10） 运行时Defense={target.Defense}（=10） → 内存态=磐石值", p1);

        // ===== P2 直调 ApplyDamage(20, isRanged:true) → 实收 = T6 公式基准 ±1 =====
        float def_ = bedrockDef.defense, rrd = bedrockDef.rangedDamageReduce;
        float baseReduce = bedrockDef.baseDamageReduce;
        float armorReduction = def_ / (def_ + armorK);
        int t6Baseline = Mathf.RoundToInt(20f * (1f - armorReduction) * (1f - rrd));   // HH.115 T6 同式（不硬编码）
        int chainBaseline = Mathf.Max(1, Mathf.RoundToInt(
            Mathf.Max(1, Mathf.RoundToInt(20f * (1f - armorReduction))) * (1f - Mathf.Clamp01(baseReduce + rrd))));
        int hp0 = target.CurrentHp;
        int actualRanged = DamageSystem.Instance.ApplyDamage(source, target, 20, 0f, isRanged: true);
        bool p2 = Mathf.Abs(actualRanged - t6Baseline) <= 1;
        Log($"P2 远程 20 实收={actualRanged} vs T6公式基准={t6Baseline}（差分={actualRanged - t6Baseline}，±1内）；" +
            $"链式基准={chainBaseline} HP {hp0}→{target.CurrentHp}", p2);

        // ===== P3 同单位近战链（isRanged=false：45% 减伤不触发）=====
        target.Heal(Mathf.Max(0, target.MaxHp - target.CurrentHp));
        int meleeBaseline = Mathf.Max(1, Mathf.RoundToInt(
            Mathf.Max(1, Mathf.RoundToInt(20f * (1f - armorReduction))) * (1f - Mathf.Clamp01(baseReduce))));
        int actualMelee = DamageSystem.Instance.ApplyDamage(source, target, 20, 0f, isRanged: false);
        bool p3 = Mathf.Abs(actualMelee - meleeBaseline) <= 1 && actualMelee > actualRanged;
        Log($"P3 近战链 实收={actualMelee} vs 近战基准={meleeBaseline}（±1）；远程={actualRanged}（45%触发→近战>远程）", p3);

        // ===== P4 非磐石对照单位（真 Warrior）远程 20 → 无 45% 减伤 =====
        var ctlNd = control.Data as NpcProfessionDef;
        float cDef = ctlNd != null ? ctlNd.defense : 0f;
        float cBaseReduce = ctlNd != null ? ctlNd.baseDamageReduce : 0f;
        float cArmorRed = cDef / (cDef + armorK);
        int ctlNoRrd = Mathf.Max(1, Mathf.RoundToInt(
            Mathf.Max(1, Mathf.RoundToInt(20f * (1f - cArmorRed))) * (1f - Mathf.Clamp01(cBaseReduce))));
        int ctlIfRrd = Mathf.Max(1, Mathf.RoundToInt(ctlNoRrd * (1f - 0.45f)));
        int actualCtl = DamageSystem.Instance.ApplyDamage(source, control, 20, 0f, isRanged: true);
        bool p4 = Mathf.Abs(actualCtl - ctlNoRrd) <= 1
                  && (ctlNoRrd == ctlIfRrd || actualCtl > ctlIfRrd);
        Log($"P4 非磐石对照（真{Occupation.Warrior} def={cDef} baseReduce={cBaseReduce}）：" +
            $"远程20实收={actualCtl} = 无rrd基准={ctlNoRrd}（±1）；若误加45%应={ctlIfRrd} → 实收{actualCtl}≠误加值 → 减伤=磐石特有", p4);

        // ===== P4b 精确对照：磐石 def 但内存清 0 rangedDamageReduce → 与磐石同护甲，无 45% =====
        var bedrockNoRrd = Object.Instantiate(bedrockDef);
        bedrockNoRrd.name = "HH131_BedrockNoRrd";
        bedrockNoRrd.prefab = warriorDef.prefab;
        bedrockNoRrd.rangedDamageReduce = 0f;   // 内存清 0（资产零污染）
        Vector2 posB = SpawnPosSnapper.SnapWorld(anchor + new Vector2(-25 * cs, 20 * cs), "HH131精确对照");
        var goB = UnitFactory.Instance.SpawnUnit(bedrockNoRrd, posB, 0);
        var ctl2 = goB != null ? goB.GetComponent<UnitController>() : null;
        yield return null;
        bool p4b = false;
        if (ctl2 != null)
        {
            int actualB = DamageSystem.Instance.ApplyDamage(source, ctl2, 20, 0f, isRanged: true);
            p4b = Mathf.Abs(actualB - actualMelee) <= 1 && actualB > actualRanged;
            Log($"P4b 精确对照（同磐石护甲 def=10，rangedDamageReduce 内存清0）：远程20实收={actualB} " +
                $"= 磐石近战链({actualMelee}) 且 > 磐石远程({actualRanged}) → 45% 唯由 rrd 字段贡献", p4b);
        }
        else Log("P4b 精确对照实例化失败", false);

        // ---- 收尾：探针单位清场（资产零污染：内存克隆随销毁消散）----
        if (goT != null) Object.Destroy(goT);
        if (goS != null) Object.Destroy(goS);
        if (goC != null) Object.Destroy(goC);
        if (goB != null) Object.Destroy(goB);
        yield return null;
        Finish();
    }

    private static void Finish()
    {
        _log.AppendLine("===== HH.131 磐石行为级补测收工 =====");
        _log.AppendLine("PASS=" + _pass + " FAIL=" + _fail + " 时间=" + System.DateTime.Now.ToString("HH:mm:ss"));
        if (TimeManager.Instance != null) TimeManager.Instance.SetGameSpeed(0f);
        TestHarnessApi.ExitTestRun();
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "hh131_bedrock_probe.log"), _log.ToString());
        }
        catch (System.Exception e) { Debug.LogError(TAG + " 日志写盘失败: " + e.Message); }
        Debug.LogWarning(TAG + " ★ 收工 PASS=" + _pass + " FAIL=" + _fail);
        var host = Object.FindObjectOfType<ProbeHost>();
        if (host != null) Application.logMessageReceived -= host.Catch;
    }
}
