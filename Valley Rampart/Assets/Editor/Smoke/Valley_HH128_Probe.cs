using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;

// ============================================================================
//  HH.128 王国AI P0 批A 行为级探针容器（Editor-only；test-harness-first 铁律①⑤：
//  EnterTestRun 正门全守卫+15x 上限；ExitTestRun 收尾）。
//  探针清单（对照 2_22 P0 清单批A 验收列+HH.128 开工回执 §二）：
//   P1(A1/A2) 统一国情快照在场：3 AI 各一+Day 对齐+三块结构字段非空
//   P2(A5)    邻接修正：反射构相邻对→k2 加兵 MilitaryTarget 升（正）+非邻接 k3 加兵不变（负）
//   P3(A2)    事件不即时改快照（负）+脏标记→次日快照 Dirty=true→再次日清零
//   P4(A4)    军事缺口读快照：GeneralGap/FormationGap/UnitTypeGap>0（AI 缺军）+快照缺席回退 0
//   P5(A6)    GeneralDiedEvent 上浮→次 tick 快照 Dirty=true
//  红线：探针局不入档不污染（槽 probe_hh128 独立；warriorCount 操纵即改即还原）。
//  证据落 Logs/P1/hh128_probe.log（Logs 不入库，断言结果转录 HH.129 报告）。
//  L-16：Host 挂 logMessageReceived 异常捕获器（协程死亡可见性）。
// ============================================================================
public static class Valley_HH128_Probe
{
    private const int SEED = 21128;          // 探针局独立 seed（非历史局非考跑 seed）
    private const string SLOT = "probe_hh128";
    private static int _pass, _fail;
    private static readonly System.Text.StringBuilder _log = new System.Text.StringBuilder();

    [MenuItem("Valley/验证/HH128_批A探针")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH128探针] 须先 GameScene 进 Play。"); return; }
        _pass = 0; _fail = 0; _log.Length = 0;
        var host = new GameObject("HH128_ProbeHost").AddComponent<ProbeHost>();
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
        Debug.LogWarning("[HH128探针] " + (ok ? "✓ " : "✗ ") + msg);
    }

    private static IEnumerator Coroutine()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = SLOT, kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg);   // 正门全守卫（判负封死+T11+15x SO 直通）

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 4)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Log("等世界就绪超时", false); Finish(); yield break; }
        }
        yield return new WaitForSeconds(1f);
        Log("正门进局 seed=" + SEED + " 槽=" + SLOT + "，AI 数=" + (KingdomRegistry.Instance.Count - 1), true);

        // ===== P1：等 3 日 → 快照在场 =====
        yield return WaitDays(3);
        int day = TimeManager.Instance.CurrentDay;
        bool p1 = true;
        var reg = KingdomRegistry.Instance;
        var all = reg.GetAll();
        for (int i = 0; i < all.Count; i++)
        {
            var k = all[i];
            if (k == null || k.IsPlayer) continue;
            SituationSnapshot s;
            bool ok = SituationHub.TryGet(k.id, out s) && s != null
                      && s.KingdomId == k.id && s.Day == day
                      && s.OwnedCombatOccupations != null && s.Threats != null && s.Losses != null;
            Log("P1 快照 k" + k.id + " 在场=" + (s != null) + " Day=" + (s != null ? s.Day : -1) + "/" + day
                + " peace=" + (s != null ? s.PeaceDays.ToString() : "?")
                + " generals=" + (s != null ? s.GeneralCount.ToString() : "?")
                + " formations=" + (s != null ? s.FormationCount.ToString() : "?"), ok);
            if (!ok) p1 = false;
        }

        // ===== P2：A5 邻接修正（反射构相邻对）=====
        var ts = TerritorySystem.Instance;
        var fDict = typeof(TerritorySystem).GetField("_territory", BindingFlags.NonPublic | BindingFlags.Instance);
        var fDirty = typeof(TerritorySystem).GetField("_adjacencyDirty", BindingFlags.NonPublic | BindingFlags.Instance);
        var k1 = reg.Get(1); var k2 = reg.Get(2); var k3 = reg.Get(3);
        if (ts == null || fDict == null || fDirty == null || k1 == null || k2 == null || k3 == null)
        { Log("P2 前置缺失 ts/dict/k1~k3", false); Finish(); yield break; }
        var terr = (Dictionary<Vector2Int, int>)fDict.GetValue(ts);
        // 选格策略 v2：扫 k1 全部领土格（坐标序）×4 邻（固定序）找第一个无主邻位——
        // v1 缺陷=只取首个 owner==1 格的右一格（HH.128 探针首跑 P2a FAIL 根因：该格贴玩家，
        // ContainsKey 拦截 claimed=false 零写入），不是邻接 API 缺陷（账本复刻 linked=0 实证）
        var own1 = new List<Vector2Int>();
        foreach (var kv in terr)
            if (kv.Value == 1) own1.Add(kv.Key);
        own1.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        int[] dxs = { 1, -1, 0, 0 }; int[] dys = { 0, 0, 1, -1 };
        Vector2Int midK1 = default, midAdj = default;
        bool found1 = own1.Count > 0, claimed = false;
        foreach (var c in own1)
        {
            bool placed = false;
            for (int d = 0; d < 4 && !placed; d++)
            {
                var cand = new Vector2Int(c.x + dxs[d], c.y + dys[d]);
                if (!terr.ContainsKey(cand)) { midK1 = c; midAdj = cand; placed = true; }
            }
            if (placed) { claimed = true; break; }
        }
        if (claimed) terr[midAdj] = 2;
        fDirty.SetValue(ts, true);
        bool adj12 = ts.AreKingdomsAdjacent(1, 2);
        bool adj13 = ts.AreKingdomsAdjacent(1, 3);   // 未构造对（若开局天然邻接则该项探针降级=记实际值）
        Log("P2a 反射构邻接对(" + midK1 + "+" + midAdj + ") claimed=" + claimed
            + " AreKingdomsAdjacent(1,2)=" + adj12 + " (1,3)=" + adj13, claimed && adj12);

        // A5 过滤逻辑直证：反射直调 private NeighborMilitary（差分法=邻接对开关，兵数注入走生产链
        // SetOccupation 转职 API【TrainingSystem 同款，零裸构 prefab】；即改即还原零污染）
        var miNM = typeof(UtilityScorer).GetMethod("NeighborMilitary", BindingFlags.NonPublic | BindingFlags.Static);
        if (miNM == null) { Log("P2b 反射 NeighborMilitary 缺失", false); Finish(); yield break; }
        int nm0 = (int)miNM.Invoke(null, new object[] { 1 });
        Log("P2b-0 开局全不邻接 nm(1)=" + nm0 + "（预期 0=非邻接国兵不入威胁分子）", nm0 == 0);

        UnitController u2 = FindAliveUnit(2), u3 = FindAliveUnit(3);
        if (u2 == null || u3 == null) { Log("P2b 前置缺失 k2/k3 活单位 u2=" + (u2 != null) + " u3=" + (u3 != null), false); Finish(); yield break; }
        var occ2 = u2.EffectiveOccupation; var occ3 = u3.EffectiveOccupation;

        u2.SetOccupation(Occupation.Warrior);   // 邻接国 k2 注入 1 战士
        int nm2 = (int)miNM.Invoke(null, new object[] { 1 });
        bool pos = nm2 == nm0 + 1;              // 正探针：邻接国兵计入
        u3.SetOccupation(Occupation.Warrior);   // 非邻接国 k3 注入 1 战士
        int nm3 = (int)miNM.Invoke(null, new object[] { 1 });
        bool neg = nm3 == nm2;                  // 负探针：非邻接国兵不计入（远距 AI 国加兵威胁分不变）

        // 净场还原：先拆邻接对再还原职业
        terr.Remove(midAdj);
        fDirty.SetValue(ts, true);
        u2.SetOccupation(occ2);
        u3.SetOccupation(occ3);
        int nm4 = (int)miNM.Invoke(null, new object[] { 1 });
        Log("P2b 邻接开关差分 nm: 基线=" + nm0 + " 构对+k2兵=" + nm2 + " +k3兵=" + nm3 + " 拆对+还原=" + nm4
            + " → 正(邻接计入)=" + pos + " 负(非邻接不计)=" + neg + " 净场还原=" + (nm4 == nm0),
            pos && neg && nm4 == nm0);

        // ===== P3：A2 事件不即时改快照（负）+脏标记消费链 =====
        SituationSnapshot s0;
        int day0 = SituationHub.TryGet(1, out s0) && s0 != null ? s0.Day : -1;
        EventBus.Publish(new KingdomAttackedEvent(1, -1));
        SituationSnapshot sImm;
        bool hasImm = SituationHub.TryGet(1, out sImm) && sImm != null;
        Log("P3a 事件后未到日 tick 快照未重建（Day " + (hasImm ? sImm.Day : -1) + "==" + day0 + "）", hasImm && sImm.Day == day0 && ReferenceEquals(sImm, s0));

        yield return WaitDays(1);
        SituationSnapshot s1;
        bool has1 = SituationHub.TryGet(1, out s1) && s1 != null;
        Log("P3b 次 tick 快照刷新 Day=" + (has1 ? s1.Day : -1) + "(>" + day0 + ") Dirty=" + (has1 && s1.Dirty),
            has1 && s1.Day > day0 && s1.Dirty);

        yield return WaitDays(1);
        SituationSnapshot s2x;
        bool has2 = SituationHub.TryGet(1, out s2x) && s2x != null;
        Log("P3c 再次日 Dirty 清零=" + (has2 && !s2x.Dirty), has2 && !s2x.Dirty);

        // ===== P5：A6 将军阵亡上浮 → 脏标记 =====
        EventBus.Publish(new GeneralDiedEvent(1));
        yield return WaitDays(1);
        SituationSnapshot s3;
        bool has3 = SituationHub.TryGet(1, out s3) && s3 != null;
        Log("P5 GeneralDiedEvent→次 tick Dirty=" + (has3 && s3.Dirty), has3 && s3.Dirty);

        // ===== P4：A4 军事缺口读快照（放尾段：含 Hub.Clear 负探针，下一 tick 自然恢复）=====
        var defG = new UtilityActionDef { id = UtilityAction.None, name = "probeG", need = NeedKind.GeneralGap, needA = 2 };
        var defF = new UtilityActionDef { id = UtilityAction.None, name = "probeF", need = NeedKind.FormationGap, needA = 1 };
        var defU = new UtilityActionDef { id = UtilityAction.None, name = "probeU", need = NeedKind.UnitTypeGap };
        float gScore = UtilityScorer.NeedScore(k1, defG);
        float fScore = UtilityScorer.NeedScore(k1, defF);
        float uScore = UtilityScorer.NeedScore(k1, defU);
        Log("P4a 军事缺口分数（AI 缺军态）GeneralGap=" + gScore.ToString("F2") + " FormationGap=" + fScore.ToString("F2")
            + " UnitTypeGap=" + uScore.ToString("F2") + " → 三缺口均>0",
            gScore > 0f && fScore > 0f && uScore > 0f);

        SituationHub.Clear();
        float gNoSnap = UtilityScorer.NeedScore(k1, defG);
        Log("P4b 快照缺席回退 0（Clear 后 GeneralGap=" + gNoSnap.ToString("F2") + "）", gNoSnap == 0f);

        Finish();
    }

    /// <summary>找某国任一活单位（P2b 职业注入载体；生产链注册表单位，非裸构）。</summary>
    private static UnitController FindAliveUnit(int kingdomId)
    {
        if (UnitRegistry.Instance == null) return null;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
            if (u != null && u.IsAlive && u.kingdomId == kingdomId) return u;
        return null;
    }

    /// <summary>等 N 个游戏日 tick（轮询 CurrentDay 变化+缓冲帧保 Brain.Tick 完成；15x≈24 挂钟秒/日）。</summary>
    private static IEnumerator WaitDays(int days)
    {
        for (int i = 0; i < days; i++)
        {
            int start = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
            while (TimeManager.Instance != null && TimeManager.Instance.CurrentDay == start)
                yield return null;
            yield return new WaitForSeconds(0.5f);   // 日结五步余波（Brain 五步②在 CurrentDay 翻转同流程）
        }
    }

    private static void Finish()
    {
        _log.AppendLine("===== HH.128 批A 探针收工 =====");
        _log.AppendLine("PASS=" + _pass + " FAIL=" + _fail + " 时间=" + System.DateTime.Now.ToString("HH:mm:ss"));
        if (TimeManager.Instance != null) TimeManager.Instance.SetGameSpeed(0f);
        TestHarnessApi.ExitTestRun();   // 正门收尾全量恢复
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "hh128_probe.log"), _log.ToString());
        }
        catch (System.Exception e) { Debug.LogError("[HH128探针] 日志写盘失败: " + e.Message); }
        Debug.LogWarning("[HH128探针] ★ 收工 PASS=" + _pass + " FAIL=" + _fail + "（终速 0+ExitTestRun；证据=Logs/P1/hh128_probe.log）");
        var host = Object.FindObjectOfType<ProbeHost>();
        if (host != null) Application.logMessageReceived -= host.Catch;
    }
}
