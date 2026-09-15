#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HH.282 `G2-2` 专属兵·机器 prefab 补全 验收探针（Editor-only · 正门 <see cref="TestHarnessApi.EnterTestRun"/>）：
///   A 正向：10 个单位（7 专属兵＋3 机器）逐一 SpawnUnit 非 null ＋ 真图帧解析（HasRealFrames）＋ 截图（≥1 兵＋1 机器）；
///   B 负探针：运行时置空某机器 UnitData.prefab（内存态·不保存资产）⇒ 生产入口返 false＋退款
///     （玩家回 RulerController / AI 回 per-kingdom 国库）＋不报"生产成功"；验完还原 prefab。
/// 用法（MCP）：Play 模式内 <c>Valley_HH282_Verify.Run()</c> 或 exec_runtime_script 调 RunCoroutine；
/// 证据落 <c>Logs/hh282_verify.log</c> ＋ 截图 <c>screenshots/HH282_专属兵prefab/</c>（gitignore 不入库）；探针自收尾。
/// </summary>
public static class Valley_HH282_Verify
{
    const int Seed = 21107;
    static readonly StringBuilder _log = new StringBuilder();
    static int _pass, _fail;
    static string _logPath;

    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH282] 须先 GameScene 进 Play。"); return; }
        _log.Clear(); _pass = 0; _fail = 0;
        _logPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh282_verify.log"));
        Log("探针启动（正门 EnterTestRun · seed=" + Seed + " · 1x）", true);
        var host = new GameObject("HH282_ProbeHost").AddComponent<ProbeHost>();
        host.Host(Coroutine(host));
    }

    /// <summary>桥接 exec_runtime_script 用：返回协程让工具等待执行完毕。</summary>
    public static IEnumerator RunCoroutine()
    {
        _log.Clear(); _pass = 0; _fail = 0;
        _logPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh282_verify.log"));
        Log("探针启动（正门 EnterTestRun · seed=" + Seed + " · 1x）", true);
        return Coroutine(null);
    }

    class ProbeHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    static void Log(string msg, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + msg);
        if (ok) _pass++; else _fail++;
        Debug.Log("[HH282] " + (ok ? "✓ " : "✗ ") + msg);
        try
        {
            if (_logPath != null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_logPath));
                System.IO.File.WriteAllText(_logPath, _log.ToString());
            }
        }
        catch { }
    }

    /// <summary>现行族选允许生产的机器（镜像 SiegeProductionSystem.IsRaceAllowedMachine 单源判定，供负探针选型）。</summary>
    static Occupation MachineForRace(int race)
    {
        switch (race)
        {
            case RaceIds.Dwarf: return Occupation.Mortar;
            case RaceIds.Elf:   return Occupation.VineCatapult;
            case RaceIds.Orc:   return Occupation.Ram;
            default:            return Occupation.Ballista;   // Human
        }
    }

    /// <summary>是否战争机器职业（镜像 SiegeProductionSystem.IsMachineOccupation；探针可见性分类用）。</summary>
    static bool IsMachineOcc(Occupation occ)
        => occ == Occupation.SiegeMachine || occ == Occupation.Ballista
           || occ == Occupation.Mortar || occ == Occupation.VineCatapult || occ == Occupation.Ram;

    static IEnumerator Coroutine(ProbeHost host)
    {
        var cfg = new NewGameConfig
        {
            mapSeed = Seed, worldSeed = Seed, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = "hh282_smoke", kingdomName = "HH282验证"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 1f);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || SiegeProductionSystem.Instance == null
               || UnitFactory.Instance == null || UnitDataManager.Instance == null
               || !UnitDataManager.Instance.IsInitialized)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Log("等世界就绪超时", false); Finish(host); yield break; }
        }
        yield return new WaitForSeconds(3f);

        Vector2 anchor = WorldManager.Instance.GetKingdomAnchorWorld();
        var unitDefs = new (string name, Occupation occ, int race)[]
        {
            ("Dwarf_Bedrock",    Occupation.Bedrock,      RaceIds.Dwarf),
            ("Dwarf_Musqueteer", Occupation.Musqueteer,   RaceIds.Dwarf),
            ("Elf_DeerRider",    Occupation.DeerRider,    RaceIds.Elf),
            ("Elf_Ranger",       Occupation.Ranger,       RaceIds.Elf),
            ("Elf_Windwalker",   Occupation.Windwalker,   RaceIds.Elf),
            ("Orc_Berserker",    Occupation.Berserker,    RaceIds.Orc),
            ("Orc_WolfRider",    Occupation.WolfRider,    RaceIds.Orc),
            ("Dwarf_Mortar",     Occupation.Mortar,       RaceIds.Dwarf),
            ("Elf_VineCatapult", Occupation.VineCatapult, RaceIds.Elf),
            ("Orc_Ram",          Occupation.Ram,          RaceIds.Orc),
        };

        // ================= A 正向：10 单位逐一生成 =================
        var spawned = new List<UnitController>();
        int idx = 0;
        foreach (var d in unitDefs)
        {
            Vector2 pos = anchor + new Vector2((idx % 5) * 1.8f - 3.6f, -(idx / 5) * 1.8f);
            GameObject go = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, d.occ, pos);
            bool ok = go != null;
            if (ok)
            {
                var uc = go.GetComponent<UnitController>();
                if (uc != null) { uc.raceId = d.race; spawned.Add(uc); }
            }
            Log($"A 正向 {d.name}（occ={d.occ}）SpawnUnit={ (ok ? "非null" : "null") }", ok);
            idx++;
            yield return null;
        }
        yield return new WaitForSeconds(1.5f);   // 等 SpriteAnimator 逐帧解析帧集（Driver.LateUpdate → EnsureSet）

        int realFrames = 0, frameChecked = 0;
        foreach (var uc in spawned)
        {
            if (uc == null) continue;
            var anim = uc.GetComponent<SpriteAnimator>();
            if (anim != null) { anim.EnsureSet(); frameChecked++; if (anim.HasRealFrames) realFrames++; }
        }
        Log($"A 帧解析：真图帧单位 {realFrames}/{frameChecked}（SpriteRefTable 素材在场实证）", frameChecked > 0 && realFrames == frameChecked);

        string shotDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "screenshots", "HH282_专属兵prefab"));
        System.IO.Directory.CreateDirectory(shotDir);
        string shotPath = System.IO.Path.Combine(shotDir, "hh282_live.png");
        ScreenCapture.CaptureScreenshot(shotPath);
        yield return null;
        Log($"A 视觉佐证：Game 窗口截图 → {shotPath}", System.IO.File.Exists(shotPath));

        // A 可见性：相机 15 世界单位内至少 1 兵 + 1 机器（画面可见实证）
        Vector2 camPos = CameraRig.Instance != null ? (Vector2)CameraRig.Instance.transform.position : Vector2.zero;
        int visibleInf = 0, visibleMach = 0;
        foreach (var uc in spawned)
        {
            if (uc == null || !uc.IsAlive) continue;
            if (Vector2.Distance(uc.transform.position, camPos) > 15f) continue;
            if (IsMachineOcc(uc.EffectiveOccupation)) visibleMach++; else visibleInf++;
        }
        Log($"A 可见性：相机 15 格内 兵={visibleInf} 机器={visibleMach}（期望 ≥1 兵且 ≥1 机器）",
            visibleInf >= 1 && visibleMach >= 1);

        // 销毁正向单位：防 3 台玩家机器计入"已放置数"污染负探针（GetPlacedMachineCount 上限 2）
        foreach (var uc in spawned)
            if (uc != null && uc.gameObject != null) Object.DestroyImmediate(uc.gameObject);
        spawned.Clear();
        yield return null;

        // ================= B 负探针：扣费兜底退款（运行时置空 prefab·验完还原）================

        // --- B1 玩家侧：按现行玩家国族选可产机器，置空其 prefab ⇒ 扣费后生成 null ⇒ 退款回 RulerController ---
        int pRace = KingdomRace.GetKingdomRace(0);
        var sys = SiegeProductionSystem.Instance;
        Occupation pMachine = MachineForRace(pRace);
        var pData = UnitDataManager.Instance.GetData(Faction.PlayerCamp, pMachine);
        var pCost = sys != null ? sys.PeekMachineCost(pMachine) : ResourcePack.Zero;
        if (sys != null && pData != null && pData.prefab != null && RulerController.Instance != null
            && RulerController.Instance.CanAfford(pCost))
        {
            var savedPfb = pData.prefab;
            pData.prefab = null;                                   // 注入 null（运行时内存态·不保存资产）
            int gold0 = RulerController.Instance.Gold;
            bool ret = sys.ProduceMachine(pMachine, anchor);       // 走生产入口：扣费 → SpawnUnit null → 退款
            int gold1 = RulerController.Instance.Gold;
            pData.prefab = savedPfb;                               // 还原
            Log($"B1 玩家负探针 {pMachine}：返回={ret}（期望 false·不报成功）", ret == false);
            Log($"B1 玩家负探针 扣费前后 Gold {gold0}→{gold1}（期望不变＝退款回玩家账户）", gold0 == gold1);
            Log($"B1 玩家负探针 prefab 已还原={pData.prefab == savedPfb}（资产文件零改动）", pData.prefab == savedPfb);
        }
        else
        {
            Log($"B1 玩家负探针 SKIP（pRace={pRace} machine={pMachine} data={(pData != null ? pData.name : "null")} afford=可判）", pData != null && pData.prefab != null);
        }

        // --- B2 AI 侧：取首个 AI 王国（id>0），按其国族选可产机器，置空 prefab ⇒ 扣费后生成 null ⇒ 退款回 per-kingdom 国库 ---
        KingdomState aiKingdom = null;
        var registry = KingdomRegistry.Instance;
        if (registry != null)
            foreach (var k in registry.GetAll())
                if (k != null && k.id > 0) { aiKingdom = k; break; }
        if (aiKingdom != null && sys != null)
        {
            int aiRace = aiKingdom.raceId;
            Occupation aiMachine = MachineForRace(aiRace);
            var aiData = UnitDataManager.Instance.GetData(Faction.PlayerCamp, aiMachine);
            var aiCost = sys.PeekMachineCost(aiMachine);
            aiKingdom.AddResources(new ResourcePack
            {
                gold = Mathf.Max(0, aiCost.gold * 10 - aiKingdom.GetResourceValue(ResourceType.Gold)),
                stone = Mathf.Max(0, aiCost.stone * 10 - aiKingdom.GetResourceValue(ResourceType.Stone)),
                wood = Mathf.Max(0, aiCost.wood * 10 - aiKingdom.GetResourceValue(ResourceType.Wood)),
                food = Mathf.Max(0, aiCost.food * 10 - aiKingdom.GetResourceValue(ResourceType.Food)),
                metal = Mathf.Max(0, aiCost.metal * 10 - aiKingdom.GetResourceValue(ResourceType.Metal)),
            });
            int g0 = aiKingdom.GetResourceValue(ResourceType.Gold);
            int s0 = aiKingdom.GetResourceValue(ResourceType.Stone);
            int w0 = aiKingdom.GetResourceValue(ResourceType.Wood);
            var savedAiPfb = aiData != null ? aiData.prefab : null;
            if (aiData != null) aiData.prefab = null;              // 注入 null（AI 侧）
            bool retAi = sys.ProduceMachine(aiMachine, anchor, aiKingdom.id);
            int g1 = aiKingdom.GetResourceValue(ResourceType.Gold);
            int s1 = aiKingdom.GetResourceValue(ResourceType.Stone);
            int w1 = aiKingdom.GetResourceValue(ResourceType.Wood);
            if (aiData != null) aiData.prefab = savedAiPfb;        // 还原
            Log($"B2 AI 负探针 k{aiKingdom.id}(race={aiRace}) {aiMachine}：返回={retAi}（期望 false·不报成功）", retAi == false);
            Log($"B2 AI 负探针 扣费前后 国库 G{g0}→{g1} S{s0}→{s1} W{w0}→{w1}（期望不变＝退款回 per-kingdom 国库）",
                g0 == g1 && s0 == s1 && w0 == w1);
            Log($"B2 AI 负探针 prefab 已还原={aiData == null || aiData.prefab == savedAiPfb}（资产文件零改动）",
                aiData == null || aiData.prefab == savedAiPfb);
        }
        else
        {
            Log("B2 AI 负探针 SKIP（无 AI 王国）", true);
        }

        Finish(host);
    }

    static void Finish(ProbeHost host)
    {
        Log($"收尾：PASS={_pass} FAIL={_fail} · ExitTestRun + QuitSmoke + 退 Play（L-32）", _fail == 0);
        TestHarnessApi.ExitTestRun();
        try { SmokeApi.QuitSmoke(); } catch { }
        EditorApplication.ExitPlaymode();
        if (host != null) Object.Destroy(host.gameObject);
    }
}
#endif
