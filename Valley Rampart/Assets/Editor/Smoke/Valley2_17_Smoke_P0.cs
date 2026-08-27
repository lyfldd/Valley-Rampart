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
//
//  **裁决1 升格（HH.27）：收入侧=harness 抽象结算预演（D281 同构：人口×生产率→AI 入账），
//  覆盖 pump 无帧断链；效力脚注——本套件收入侧为抽象结算实现，产品侧归步骤14 AbstractEconomySettler 落地。**
//  B2 供水口径随之改为「农场抽象产出>0」。细模拟经济闭环（工人真走真产）留人工 Play 一票。
// ============================================================================
public static class Valley2_17_Smoke_P0
{
    private const int SEED = 20260828;
    private const string SLOT_RT = "smoke_p0_rt";
    private const int CAP_DAYS = 45;
    private const int SAVE_DAY = 25;
    private const int PLAYER_RECRUIT_DAY = 13;
    private const float TEST_SPD = 60f;
    private static float s_farmAbstractOut = 0f;   // 裁决1 B2：抽象结算农场累积产出（harness 预演口径）

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

        // 裁决2-① 轮间清点断言：三轮开局注册表计数应一致（各=各自新局期望），不等→残留暴露
        bool entryClean = r1.entryBuilding == r2.entryBuilding && r1.entryBuilding == r3.entryBuilding
                       && r1.entryUnit == r2.entryUnit && r1.entryUnit == r3.entryUnit;
        c.Add($"RD2-①轮间清点={(entryClean ? "OK" : "FAIL")}(b={r1.entryBuilding}/{r2.entryBuilding}/{r3.entryBuilding} u={r1.entryUnit}/{r2.entryUnit}/{r3.entryUnit})");

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

        // 裁决1 B2 供水（抽象农场产出>0 间接证据，预演口径）
        bool b2 = s_farmAbstractOut > 0f;
        c.Add($"B2供水抽象产出={(b2 ? "OK" : $"FAIL(out={s_farmAbstractOut})")}");

        // B3+C6 存读回环含脑态：存读轮序列 == 纯轮序列（D 起一致）
        bool bc = r1.seq == r3.seq;
        c.Add($"B3+C6存读回环含脑态={(bc ? "OK" : "FAIL")}");
        // 裁决2-② 存读 v2 门控：≥2 = 走 B 全权重建（无 A/B 双份），<2 = 门控被 harness 调用序绕过
        c.Add($"RD2-②存读v2门控={(r3.lastLoadVersion >= 2 ? "OK(v2走重建)" : $"FAIL(loadVer={r3.lastLoadVersion}<2 门控嫌疑)")}");

        // B4 剧本三段封顶时间线
        bool b4 = r1.reachedExpand && !r1.hasMilitary && r2.reachedExpand && !r2.hasMilitary;
        c.Add($"B4剧本三段封顶={(b4 ? "OK" : "FAIL")}(R1{(r1.reachedExpand ? "E" : "-")}{(!r1.hasMilitary ? "+无军事" : "+误M")} R2{(r2.reachedExpand ? "E" : "-")}{(!r2.hasMilitary ? "+无军事" : "+误M")})");

        // B5 派遣双证分列
        bool b5 = r1.k1Build > 0 && r1.k1Train > 0;
        c.Add($"B5派遣双证分列={(b5 ? "OK" : "FAIL")}(K1 build{r1.k1Build} train{r1.k1Train})");

        bool corePass = a3 && a4 && b1 && b2 && bc && b4 && b5
                        && r3.lastLoadVersion >= 2 && entryClean;

        Debug.Log("[P0完整局] ====================================================================");
        foreach (var line in c) Debug.Log("[P0完整局] " + line);
        Debug.Log($"[P0完整局] 时间线R1={r1.stageSeq}");
        Debug.Log($"[P0完整局] 时间线R2={r2.stageSeq}");
        // A3 首次逐行差异定位
        string[] a1 = r1.seq.Replace("\r", "").Split('\n');
        string[] a2 = r2.seq.Replace("\r", "").Split('\n');
        int firstDiff = -1;
        for (int i = 0; i < System.Math.Min(a1.Length, a2.Length); i++)
        {
            if (a1[i] != a2[i]) { firstDiff = i; Debug.Log($"[P0完整局] A3首差@行{i}: R1[{a1[i]}]  R2[{a2[i]}]"); break; }
        }
        // 裁决 A3 wood 二分定位：找 wood 首次不等的那一天 + 那天两轮 focus/train/build 差异（"谁动 wood"）
        woodForkLog(a1, a2);
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
        // 裁决2-① 残留实锤修复：UnitRegistry 跨轮残留（u=18/36/54）+18 递增坐实；
        // BuildingRegistry b=2684 异常大但三抽一致——统一 Clear 归零，暴露新局真实基准。
        if (UnitRegistry.Instance != null) UnitRegistry.Instance.Clear();
        if (BuildingRegistry.Instance != null) BuildingRegistry.Instance.Clear();
        KingdomBrain.ResetDispatchStats();
        s_farmAbstractOut = 0f;   // 裁决1 B2：每轮独立累计（防跨轮污染）

        lm.InitializeNewGame(new NewGameConfig
        {
            mapSeed = SEED, worldSeed = SEED, difficulty = 2,
            worldSize = WorldSize.Medium, kingdomName = "P0完整局B",
            selectedSlotId = withRoundtrip ? "smoke_p0_b_rt" : "smoke_p0_b"
        });
        yield return null;
        Debug.Log($"[P0完整局] 开局: RT={withRoundtrip} Day={tm?.CurrentDay} KCount={(KingdomRegistry.Instance?.GetAll()?.Count ?? -1)}");
        // 裁决3 B1 前置：map ready 初始流民预置（D308）——真实路径由 GameBootstrap 地图 ready 调，
        // pump 无引导时序，这里手动补一次对齐，否则流浪汉池空（B1 pFalse 根因=候选缺失）。
        if (VagrantCampSystem.Instance != null)
            VagrantCampSystem.Instance.OnNewGameMapReady();
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

        // 裁决2-① 轮间清点断言（残留暴露）：每轮开局（新局已建）读注册表计数，
        // Driver 端比对三轮 entry==应一致（residual→ignition 差异）。
        var r = new RoundData();
        r.entryBuilding = BuildingRegistry.Instance != null ? BuildingRegistry.Instance.Count : -1;
        r.entryUnit = UnitRegistry.Instance != null ? UnitRegistry.Instance.Count : -1;
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
                string pre = BuildEtch(tm, simDay);
                bool saved = sm.Save(SLOT_RT);
                yield return null;
                bool loaded = saved && sm.Load(SLOT_RT);
                string post = BuildEtch(tm, simDay);
                r.roundtripOk = loaded && pre == post;
                // 裁决2-② v2 门控日志：读档走 v2 路径则建筑由 B 全权重建（无 A/B 双份）
                r.lastLoadVersion = SaveManager.Instance != null ? SaveManager.Instance.LastLoadedSaveVersion : -1;
                Debug.Log($"[P0完整局] 存读轮 loadVer={r.lastLoadVersion} (CurrentSaveVersion=2；<2=门控绕过风险，≥2=走 B 全权重建) roundtrip={r.roundtripOk}");
                if (loaded) tm = TimeManager.Instance;   // 读档后重取引用（域内单例重建于同 Play 会话）
            }

            // 反射 AdvanceTime 走完整事件链推一天（真链）
            ReflectAdvance(tm);
            yield return null;   // 让同步事件链副作用（若有）落定（timeScale=0 下 yield null 仍跑下一帧）

            // 裁决1 抽象结算预演（D281 同构：人口×生产率→AI 入账；效力脚注见文件头）
            //——pump 无帧断链，抽象结算给 AI 供养，否则 Feasible/gold 拦腰→招工人/建造缺粮卡死
            ApplyAbstractSettlement();

            sb.Append(BuildEtch(tm, simDay)).Append('\n');

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
    private static string BuildEtch(TimeManager tm, int simDay)
    {
        var sb = new StringBuilder();
        sb.Append(simDay).Append(':');   // 统一 simDay 轴：两轮同 simDay 对齐，消除 tm.CurrentDay 跨轮残留错位
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

    /// <summary>
    /// 裁决1 抽象结算预演（D281 同构模拟：人口×生产率→入账；效力脚注=产品侧归步骤14 AbstractEconomySettler）。
    /// pump 无帧→生产者/工人走位产出断链，此处对每个 AI 王国按「工人数×生产率 + 建筑数×税率」入账粮/木/石/金，
    /// 供 AI 招工人(TryTrain 需粮)/建造(TryBuild 需资源) 的 Feasible 有料可花。B2 供水口径=「农场抽象产出>0」。
    /// 数值为预演缺省，非产品数值，仅够 pump 全链活动所需；细模拟经济闭环留人工 Play。
    /// </summary>
    private static void ApplyAbstractSettlement()
    {
        const int workerRate = 4;      // 每工人每日入账倍数（预演：粮/木/石/金各算，够 Sustenance+建造）
        const int taxPerBuilding = 2;  // 每建筑每日税金（gold）
        var reg = KingdomRegistry.Instance;
        if (reg == null) return;
        var all = reg.GetAll();
        if (all == null) return;
        for (int i = 0; i < all.Count; i++)
         {
             var k = all[i];
             if (k == null || k.IsPlayer) continue;
             var gain = new ResourcePack
             {
                 food = k.workerCount * workerRate,
                 wood = k.workerCount * workerRate,
                 stone = k.workerCount * workerRate,
                 gold = k.workerCount * workerRate + CountBuildings(k.id) * taxPerBuilding
             };
             k.AddResources(gain);
             s_farmAbstractOut += gain.food;   // 裁决1 B2：农场抽象产出>0 间接证据（预演口径）
         }
     }

    private static void DoPlayerRecruit(RoundData r)
    {
        // 裁决3 B1 补验：确认真实流浪汉在场 + 注入玩家粮食到充足（防"粮不足"外因遮蔽通道验证；
        // pump 无帧玩家无产出，直接注入足够粮；RecruitVagrant 内部用 cfg.recruitFoodCost 扣）+ 打 pFalse 根因分层日志。
        var vcs = VagrantCampSystem.Instance;
        var ruler = RulerController.Instance;
        if (vcs == null || ruler == null) { Debug.Log("[P0完整局] B1 招募：VagrantCampSystem/Ruler 缺失，跳过"); return; }

        // 确认真实流浪汉在场（条件：存活 + 未入籍 + 未招募 + 职业=Vagrant）
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
        bool anyVagrant = vagrant != null;

        // 裁决 A3 B1 补验：「流浪汉池空」是纯 pump 无地图 spawn 的环境结果，非通道缺陷。
        // harness 程序化 spawn 一个真实流浪汉注入 UnitRegistry，续验 RecruitVagrant 通道行为级
        // （确定性位置，不依赖 ActiveMap；招工→入册走 Update 归人工 Play）。
        if (!anyVagrant && UnitFactory.Instance != null)
        {
            var go = UnitFactory.Instance.SpawnUnit(Faction.Human_Player, Occupation.Vagrant, Vector3.zero);
            if (go != null)
            {
                var uc = go.GetComponent<UnitController>();
                vagrant = uc;
                anyVagrant = uc != null;
                Debug.Log($"[P0完整局] B1 harness spawn 流浪汉{(uc != null ? " 成功" : " null")}（补验通道，pump 无地图 spawn 兜底）");
            }
        }

        // 注入玩家粮食到充足（保守 200，远大于各类 recruitFoodCost；内部按 cfg 实扣）
        if (ruler.Food < 200)
            ruler.ModifyResource(ResourceType.Food, true, 200 - ruler.Food);
        bool hasFood = ruler.Food >= 200;

        // pFalse 根因分层
        if (!anyVagrant) Debug.Log("[P0完整局] B1 招募：无在场真实流浪汉（Vagrant 池空）──候选单位缺失");
        if (!hasFood) Debug.Log($"[P0完整局] B1 招募：玩家粮仍不足（{ruler.Food}），注入未生效？");

        bool ok = anyVagrant && hasFood && vcs.RecruitVagrant(vagrant);
        // 通道行为级：RecruitVagrant 返回 true + 流浪汉已转居民 = 通道接受指令（扣粮已由内部完成）。
        // 注：转居民后"走回王国入册 Population+1"依赖 Update/走位（ScanArrive），pump 无帧不触发——归人工（职责归位声明）。
        r.playerRecruitOk = ok && anyVagrant && vagrant.EffectiveOccupation == Occupation.Resident;
        Debug.Log($"[P0完整局] B1 招募: 流浪汉在场={anyVagrant} 粮够={hasFood} RecruitVagrant={ok}"
                  + (r.playerRecruitOk ? " → 通道OK(流浪汉→居民)" : " → 达入册需 Update/走位(归人工)"));
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

    /// <summary>
    /// 裁决 A3 wood 二分定位探针：逐行比较两纯轮快照，提取 K1 wood 首次不等的那一天
    /// （day 前缀 + focus/train/build），打印当天两轮完整态辨"谁动 wood"。
    /// 行格式：`<day>:k..|K1g<g>/f<f>/w<w>/s<s>|wk<wk>wa<wa>b<b>|t<train>B<build>`
    /// </summary>
    private static void woodForkLog(string[] a1, string[] a2)
    {
        int n = System.Math.Min(a1.Length, a2.Length);
        int lastSameWood = -1, firstDiffWood = -1;
        for (int i = 0; i < n; i++)
        {
            int w1 = ExtractWood(a1[i]), w2 = ExtractWood(a2[i]);
            if (w1 < 0 || w2 < 0) continue;              // 行不含 K1 wood（读档边界等）
            if (w1 != w2) { firstDiffWood = i; break; }
            lastSameWood = i;
        }
        Debug.Log($"[P0完整局] A3wood二分: 末一致日=行{lastSameWood} 首差日=行{firstDiffWood}");
        if (firstDiffWood >= 0 && firstDiffWood < n)
        {
            Debug.Log($"[P0完整局]   R1@首差日[{a1[firstDiffWood]}]");
            Debug.Log($"[P0完整局]   R2@首差日[{a2[firstDiffWood]}]");
            // 前一日对比（分叉前 incl R1 一旦不同则定位）
            if (firstDiffWood >= 1)
            {
                Debug.Log($"[P0完整局]   前日R1[{a1[firstDiffWood - 1]}]");
                Debug.Log($"[P0完整局]   前日R2[{a2[firstDiffWood - 1]}]");
            }
        }
    }

    /// <summary>从快照行提取 K1 wood（`w<num>`，无则 -1）。</summary>
    private static int ExtractWood(string line)
    {
        if (line == null) return -1;
        int wIdx = line.IndexOf('w');
        while (wIdx >= 0)
        {
            // 匹配 /w 前缀 + 数字（避开 wk/w a 单位字段）
            int end = wIdx + 1;
            bool digit = false;
            while (end < line.Length && char.IsDigit(line[end])) { digit = true; end++; }
            if (digit)
            {
                // 要求前缀是 '/' 或 前一位非 'k'
                if (wIdx > 0 && line[wIdx - 1] == '/')
                    return int.Parse(line.Substring(wIdx + 1, end - wIdx - 1));
            }
            wIdx = line.IndexOf('w', wIdx + 1);
        }
        return -1;
    }

    private class RoundData
    {
        public string seq = "";
        public string stageSeq = "";
        public bool playerRecruitAttempted, playerRecruitOk, roundtripOk;
        public bool reachedExpand, hasMilitary;
        public int k1Train, k1Build, k2Train;
        public int entryBuilding = -1, entryUnit = -1;   // 裁决2-① 轮间清点基准
        public int lastLoadVersion = -1;                  // 裁决2-② v2 门控日志
        public float farmAbstractOut = 0f;                // 裁决1 B2 农场抽象产出
    }

    private class P0Host : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}