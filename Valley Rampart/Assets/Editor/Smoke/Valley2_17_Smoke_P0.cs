using System.Collections;
using System.Text;
using UnityEngine;

// ============================================================================
//  2_17 P0 完整局验收 —— 确定性状态机验收（纯逻辑 pump 单套件）
//  HH.27 策划裁定：①名谓"确定性状态机验收"（pump 推日、断言状态时间线）；
//  ②灾变域三禁准予且登记；③pump 约束=反射 TimeManager.AdvanceTime 走完整事件链、
//  禁止直接调 DayCycleSettlement.OnDayChanged、GameState 必须 Playing、
//  SetSecondsPerDay 走公开 API（L296）。
//
//  pump 确定性协议（HH.27 ③，防活世界不确定性污染 A3 逐字节）：
//     SetSecondsPerDay(TEST_SPD 公开 API) + SetTimeScale(0)（timeScale=0 → Update 的
//     Time.deltaTime=0，_dayTimer 不自行累积）→ 每步反射 AdvanceTime(TEST_SPD)
//     → while(_dayTimer>=secondsPerDay) AdvanceDay() → TimeDayChangedEvent
//     → DayCycleSettlement 五步权威结算（真链）。纯反射 push，零帧级秒不确定性。
//
//  覆盖（HH.26：A 四判据 + B 残⚪合并）：
//     A3 两纯轮逐字节一致（确定性状态机验收本命判据）
//     B3+C6 存读回环含脑态（D25 Save→Load，快照链从读档后续推并两纯轮/存读轮比对）
//     B1 正向玩家招募 + 两国并行（pump 内主动 RecruitVagrant + K1/K2 trainOk>0）
//     B4 剧本三段封顶时间线（S→D→E 序列，无 M=军事未达 P0 封顶符合设计）
//     B5 评分→派遣双证分列（⑥招工人实体化 trainOk / 建造落成 buildOk）
//     A4 玩家零回归（结构性守卫：玩家无 Brain / 招募不吞玩家单位）
//  A1/A2 诚实让渡（HH.27 ①③明示）：纯 pump 下"工人走位驱动的产出闭环"断链——
//     ProducerComponent 产出依赖 TaskScheduler 逐帧派工/走位到达，pump 无帧不产；
//     故经济增长/成长到达度、无停滞判据本套件采集时间线证据但**不据此伪造 PASS**，
//     该让渡项归人工 Play。B2 供水=农场入账>0 同依赖逐帧产出，一并登记让渡。
//
//  职责归位声明（HH.27 ①）：NavMesh 真实走位/逐帧表现属表现层，不在 P0 验收面；
//  真实游走归人工 Play，本套件不伪造逐帧证据。
// ============================================================================
public static class Valley2_17_Smoke_P0
{
    private const int SEED = 20260828;
    private const string SLOT_RT = "smoke_p0_rt";
    private const int CAP_DAYS = 45;
    private const int SAVE_DAY = 25;
    private const int PLAYER_RECRUIT_DAY = 13;
    private const float TEST_SPD = 60f;

    [UnityEditor.MenuItem("Valley/验证/2_17_P0_完整局验收")]
    public static void RunFromMenu()
    {
        if (!Application.isPlaying) { Debug.LogError("[P0完整局] 须在 Play 上下文执行。"); return; }
        new GameObject("P0_SmokeRunner").AddComponent<P0Host>().Host(Driver());
    }

    private static IEnumerator Driver()
    {
        var tm = TimeManager.Instance;
        var lm = LoadManager.Instance;
        var sm = SaveManager.Instance;
        if (tm == null || lm == null || sm == null) { Debug.LogError("[P0完整局] 单例缺失。"); yield break; }

        RoundData r1 = null, r2 = null, r3 = null;
        float savedSpd = tm.SecondsPerDay;             // 存真实初值（pump 前）
        yield return RunPump(false, x => r1 = x);
        yield return RunPump(false, x => r2 = x);
        yield return RunPump(true, x => r3 = x);   // 存读回环轮
        if (r1 == null || r2 == null || r3 == null)
        { Debug.LogError("[P0完整局] 有轮未跑（InitializeNewGame/Playing 守卫失败），中止。"); yield break; }

        tm.SetSecondsPerDay(savedSpd);   // 恢复默认
        Time.timeScale = 1f;

        // ===================== 断言汇总 =====================
        var c = new System.Collections.Generic.List<string>();

        // A3 两纯轮逐字节一致
        bool a3 = r1.seq == r2.seq;
        c.Add($"A3确定性逐字节={(a3 ? "OK" : "FAIL")}");

        // A4 玩家零回归（结构性）
        bool a4 = PlayerGuardsOk();
        c.Add($"A4玩家零回归={(a4 ? "OK" : "FAIL")}");

        // B1 正向玩家招募 + 两国并行
        bool b1 = r1.playerRecruitOk && r2.playerRecruitOk
                  && r1.k1Train > 0 && r1.k2Train > 0 && r2.k1Train > 0 && r2.k2Train > 0;
        c.Add($"B1正向招募并行={(b1 ? "OK" : "FAIL")}(p{r1.playerRecruitOk}/{r2.playerRecruitOk} k1t{r1.k1Train}/{r2.k1Train} k2t{r1.k2Train}/{r2.k2Train})");

        // B3+C6 存读回环含脑态：存读轮序列 == 纯轮序列（D 起一致）
        bool bc = r1.seq == r3.seq;
        c.Add($"B3+C6存读回环含脑态={(bc ? "OK" : "FAIL")}");

        // B4 剧本三段封顶时间线
        bool b4 = r1.reachedExpand && !r1.hasMilitary && r2.reachedExpand && !r2.hasMilitary;
        c.Add($"B4剧本三段封顶={(b4 ? "OK" : "FAIL")}(R1{(r1.reachedExpand ? "E" : "-")}{(!r1.hasMilitary ? "+无军事" : "+误M")} R2{(r2.reachedExpand ? "E" : "-")}{(!r2.hasMilitary ? "+无军事" : "+误M")})");

        // B5 派遣双证分列
        bool b5 = r1.k1Build > 0 && r1.k1Train > 0;
        c.Add($"B5派遣双证分列={(b5 ? "OK" : "FAIL")}(K1 build{r1.k1Build} train{r1.k1Train})");

        bool corePass = a3 && a4 && b1 && bc && b4 && b5;

        Debug.Log("[P0完整局] ====================================================================");
        foreach (var line in c) Debug.Log("[P0完整局] " + line);
        Debug.Log($"[P0完整局] 时间线R1={r1.stageSeq}");
        Debug.Log($"[P0完整局] 时间线R2={r2.stageSeq}");
        // A3 首次逐行差异定位
        string[] a1 = r1.seq.Replace("\r", "").Split('\n');
        string[] a2 = r2.seq.Replace("\r", "").Split('\n');
        for (int i = 0; i < System.Math.Min(a1.Length, a2.Length); i++)
        {
            if (a1[i] != a2[i]) { Debug.Log($"[P0完整局] A3首差@行{i}: R1[{a1[i]}]  R2[{a2[i]}]"); break; }
        }
        Debug.Log($"[P0完整局] ===== {(corePass ? "ALL PASS(状态面)" : "HAS FAIL")} =====");
        Debug.Log("[P0完整局] A1/A2/B2 经济产出闭环属走位驱动，pump 无帧不产→时间线证据已收，正式判定按 HH.27 让渡归人工 Play");
        Debug.Log("[P0完整局] 玩家死亡/GameOver 链路（ThroneAnchor 被禁）本批次未验，留独立回归（HH.27 ②登记）");
    }

    // ==================================================================
    //  单轮纯 pump：InitializeNewGame → SetSecondsPerDay → SetTimeScale(0)
    //  → 每步反射 AdvanceTime(TEST_SPD) 推一天 → 收敛状态快照链。
    // ==================================================================
    private static IEnumerator RunPump(bool withRoundtrip, System.Action<RoundData> done)
    {
        var tm = TimeManager.Instance;
        var lm = LoadManager.Instance;
        var sm = SaveManager.Instance;

        // 对齐"回主菜单→新开一局"的真实复位语义（必须在 InitializeNewGame 之前，
        // 否则新局注册 foundedDay 读到的是上一轮累积 CurrentDay）：
        //   KingdomRegistry 玩家占位/nextId 复位；TimeManager 回 day1；WorldManager 清地图种子。
        if (KingdomRegistry.Instance != null) KingdomRegistry.Instance.ResetState();
        if (TimeManager.Instance != null) TimeManager.Instance.ResetState();
        if (WorldManager.Instance != null) WorldManager.Instance.ResetState();
        KingdomBrain.ResetDispatchStats();

        lm.InitializeNewGame(new NewGameConfig
        {
            mapSeed = SEED, worldSeed = SEED, difficulty = 2,
            worldSize = WorldSize.Medium, kingdomName = "P0完整局B",
            selectedSlotId = withRoundtrip ? "smoke_p0_b_rt" : "smoke_p0_b"
        });
        yield return null;
        Debug.Log($"[P0完整局] 开局: RT={withRoundtrip} Day={tm?.CurrentDay} KCount={(KingdomRegistry.Instance?.GetAll()?.Count ?? -1)}");
        // HH.27 ③：pump 期间 GameState 必须 Playing（EnterPlaying/云端/加载末期可能有瞬态其他态，
        // 这里吞掉 by 强推 Playing——反射 AdvanceTime 不依赖 Update，推进有效）。
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(GameState.Playing);
        KingdomBrain.ResetDispatchStats();
        DisarmDisasters();   // HH.27 ②（追认准予）

        tm.SetSecondsPerDay(TEST_SPD);   // 公开 API（L296）
        // 确定性冻结：Time.timeScale=0（Unity 原生，绕开 SetTimeScale 的 {0.5,1,2,3} 档位分支）——
        // 使 TimeManager.Update 的 deltaTime 归零不再自推 _dayTimer；pump 反射 AdvanceTime 成为唯一推手
        Time.timeScale = 0f;

        var r = new RoundData();
        var sb = new StringBuilder();
        var lastEtch = "";

        for (int simDay = 1; simDay <= CAP_DAYS; simDay++)
        {
            if (simDay == PLAYER_RECRUIT_DAY && !r.playerRecruitAttempted)
            {
                DoPlayerRecruit(r);
                r.playerRecruitAttempted = true;
            }

            if (withRoundtrip && simDay == SAVE_DAY)
            {
                string pre = BuildEtch(tm);
                bool saved = sm.Save(SLOT_RT);
                yield return null;
                bool loaded = saved && sm.Load(SLOT_RT);
                string post = BuildEtch(tm);
                r.roundtripOk = loaded && pre == post;
                if (loaded) tm = TimeManager.Instance;   // 读档后重取引用（域内单例重建于同 Play 会话）
            }

            // 反射 AdvanceTime 走完整事件链推一天（真链）
            ReflectAdvance(tm);
            yield return null;   // 让同步事件链副作用（若有）落定（timeScale=0 下 yield null 仍跑下一帧）

            sb.Append(BuildEtch(tm)).Append('\n');

            var k1 = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(1) : null;
            if (k1 != null)
            {
                var st = k1.scriptPhase ?? ScriptStage.Survive;
                r.stageSeq += StageLetter(st);
                if (st == ScriptStage.Expand) r.reachedExpand = true;
                if (st == ScriptStage.Military) r.hasMilitary = true;
            }
        }

        r.k1Train = KingdomBrain.GetDispatch(1).trainOk;
        r.k1Build = KingdomBrain.GetDispatch(1).buildOk;
        r.k2Train = KingdomBrain.GetDispatch(2).trainOk;
        r.seq = sb.ToString();

        try { sm.Delete(SLOT_RT); } catch { /* 忽略 */ }

        done(r);
    }

    /// <summary>反射 TimeManager.AdvanceTime(TEST_SPD)：内部 while(_dayTimer>=secondsPerDay) AdvanceDay()→事件链。</summary>
    private static void ReflectAdvance(TimeManager tm)
    {
        var m = typeof(TimeManager).GetMethod("AdvanceTime",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (m != null) m.Invoke(tm, new object[] { TEST_SPD });
        else Debug.LogError("[P0完整局] 反射 AdvanceTime 失败（签名变更需修 harness）");
    }

    /// <summary>状态快照链锚（HH.27 ③：含 scriptPhase/focus/simMode=脑态 + 国库 + 人口 + 建筑 + 派遣）。</summary>
    private static string BuildEtch(TimeManager tm)
    {
        var sb = new StringBuilder();
        sb.Append((tm != null ? tm.CurrentDay : 0)).Append(':');
        var all = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.GetAll() : null;
        if (all != null)
        {
            for (int i = 0; i < all.Count; i++)
            {
                var k = all[i];
                if (k == null || k.IsPlayer) continue;
                sb.Append('k').Append(k.id)
                  .Append((int)(k.scriptPhase ?? ScriptStage.Survive)).Append(',')
                  .Append(k.focus).Append(',')
                  .Append((int)k.simMode).Append(';');
            }
        }
        var k1 = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(1) : null;
        if (k1 != null)
        {
            var d = KingdomBrain.GetDispatch(1);
            sb.Append("|K1g").Append((int)k1.GetResourceValue(ResourceType.Gold))
              .Append("/f").Append((int)k1.GetResourceValue(ResourceType.Food))
              .Append("/w").Append((int)k1.GetResourceValue(ResourceType.Wood))
              .Append("/s").Append((int)k1.GetResourceValue(ResourceType.Stone))
              .Append("|wk").Append(k1.workerCount)
              .Append("wa").Append(k1.warriorCount)
              .Append("b").Append(CountBuildings(1))
              .Append("|t").Append(d.trainOk).Append("B").Append(d.buildOk);
        }
        return sb.ToString();
    }

    private static void DoPlayerRecruit(RoundData r)
    {
        UnitController vagrant = null;
        var urs = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (urs != null)
            foreach (var u in urs)
            {
                if (u == null || !u.IsAlive) continue;
                if (u.kingdomId >= 0 || u.IsVagrantRecruited) continue;
                if (u.EffectiveOccupation != Occupation.Vagrant) continue;
                vagrant = u; break;
            }
        float before = RulerController.Instance != null ? RulerController.Instance.Food : -1;
        bool ok = vagrant != null && VagrantCampSystem.Instance != null
                  && VagrantCampSystem.Instance.RecruitVagrant(vagrant);
        r.playerRecruitOk = ok && before > 0
                            && RulerController.Instance != null
                            && RulerController.Instance.Food <= before
                            && vagrant.EffectiveOccupation == Occupation.Resident;
    }

    /// <summary>净化面：灾变域三禁（HH.27 ②）。登记：ThroneAnchor 禁=GameOver 链路本批未验留独立回归。</summary>
    private static void DisarmDisasters()
    {
        int n = 0;
        var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var mb = all[i];
            if (mb == null) continue;
            var tn = mb.GetType().Name;
            if (tn == "PortalDisasterTrigger" || tn == "WaveDirector" || tn == "ThroneAnchor") { mb.enabled = false; n++; }
        }
        if (n > 0) Debug.Log($"[P0完整局] 灾变域失能组件×{n}（防玩家死→时钟冻结污染判据）");
    }

    private static int CountBuildings(int kingdomId)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return 0;
        int n = 0;
        for (int i = 0; i < reg.All.Count; i++)
            if (reg.All[i] != null && reg.All[i].kingdomId == kingdomId && reg.All[i].IsActive) n++;
        return n;
    }

    private static char StageLetter(ScriptStage s) =>
        s == ScriptStage.Develop ? 'D' : s == ScriptStage.Expand ? 'E'
        : s == ScriptStage.Military ? 'M' : 'S';

    /// <summary>A4 结构性守卫：玩家 id=0 无 Brain、招募通道不吞玩家单位（编译期静态挑 kingdomId<0）＋玩家国存在。</summary>
    private static bool PlayerGuardsOk()
    {
        bool noBrain = KingdomBrainRegistry.Instance == null || KingdomBrainRegistry.Instance.Get(0) == null;
        bool playerPresent = KingdomRegistry.Instance != null && KingdomRegistry.Instance.Get(0) != null;
        return noBrain && playerPresent;
    }

    private class RoundData
    {
        public string seq = "";
        public string stageSeq = "";
        public bool playerRecruitAttempted, playerRecruitOk, roundtripOk;
        public bool reachedExpand, hasMilitary;
        public int k1Train, k1Build, k2Train;
    }

    private class P0Host : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}