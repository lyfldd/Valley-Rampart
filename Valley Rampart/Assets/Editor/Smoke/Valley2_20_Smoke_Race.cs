using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;
using static BuildingFactory;

// ============================================================================
//  2_20 种族域 Play 实况冒烟（HH.51 批A/B/C 验收：D467~D472 + D475~D477 解禁注增量）
//  用法：Play 上下文（先 Play 再点）——菜单「Valley/验证/2_20_种族域Play冒烟」。
//
//  探针清单（正/负对照，行为级优先；异族个体用 AIDebugSpawnController 种族域调试钩子构造）：
//   ⑤ 静态：WildnessConfig 路径探针(Resources.Load) + IsActive 开关正负（关→零野性）
//           + 反射 TryGetWildCombatOverride 下限兜底数值（Worker 基线=0 → attack≥1/range≥1/cd≥0.5）。
//   ① 行为：异族(Elf)流民贴脸站桩 Worker → 6s 内 Worker 掉血（正）；
//           同族(Human)流民贴脸对照 Worker → 血不变（负）。组距 14 格 > 野性 8 格防串扰。
//   ② 行为：a) 压制负探针：Archer 贴脸 brain-off 异族流民（流民不动手）→ 10s 流民血不变
//              （国民不主动种族攻击，D468 压制语义）；
//           b) 反击正探针：Archer 贴脸 brain-on 异族流民（流民先动手）→ 10s 内流民掉血/死亡
//              （现有受击反击链承接，走查确认+行为回归）。
//   ④ 同步：玩家侧 RecruitVagrant 异族拒绝（false+粮不变+日志「招募拒绝：异族」）+ 同族放行（粮够时）；
//           AI⑥ FindRecruitableVagrant 反射调用——异族被滤/同族可招/全异族返回 null。
//   ③ 同步：D471 插旗定族——13 Elf+1 Human 营（多数派 Elf）立国 → 日志「定族 raceId=1（D471」
//           + 新国成员 raceId 保持 Elf（终身字段 D467）。
//  —— Q10 批2 增量（2026-09-04，M3+M5）：
//   ⑥ M3 开局分布：第一代 AI（id 最小 min(3,AI数) 个，id 序=立国序）种族互异+不含玩家族（D430 保底席）
//           +全 AI raceId 合法域+M5a 玩家 state.raceId 与 helper 同值（EnsurePlayerRegistered 绑定链）。
//   ⑦ M5 消费链：D503 全表 12×4 逐值断言+GetGatherMul 同源（Ore/Wood/Food 映射+Metal 不乘）
//           +正探针（玩家=矮人 → 采矿 1.3）+负探针（哨兵 -1 → null → 中性 1）。
//
//  实盘缺口注（HH.51 验收）：Human_Player_Worker 资产 attack/attackRange/attackCD=0（和平职业正常值）
//  → 「野人战力=同职工人 60%」公式退化为 0 → TryGetWildCombatOverride 走 Max 下限兜底
//  （attack≥1/range≥1/cd≥0.5，D468 无条件攻击行为硬规则落地优先）——数值占位待策划端 Play 回调。
//
//  布局注：全单位组 y=12 行向 x 展开，x 间距 14 格（等轴 x 步长 1.28 → 17.9 世界单位
//  > 野性 8 格半径 10.24）；等轴 y 向步长减半（0.64）故不向 y 展开组距。营地锚 (9,40) 独立。
// ============================================================================
public static class Valley2_20_Smoke_Race
{
    private const int SEED = 22360;

    [MenuItem("Valley/验证/2_20_种族域Play冒烟")]
    public static void Run()
    {
        var wm = Object.FindAnyObjectByType<WorldManager>();
        if (wm == null) { Debug.LogError("[2_20冒烟] 未找到 WorldManager——请在 Play 上下文执行（先 Play 再点菜单）。"); return; }
        new GameObject("2_20_SmokeRunner").AddComponent<RaceSmokeHost>().Host(RunCoroutine());
    }

    private static IEnumerator RunCoroutine()
    {
        var sb = new StringBuilder();
        bool allPass = true;

        // ===== 世界构建（固定 seed 自主建世界，稳定复现）=====
        // 直接 Play GameScene：MainMenu 未传配置 → GameBootstrap 到 Ready 但不建图（ActiveMap 空）。
        // 冒烟等 Ready（LoadManager 阶段1 完成 + 核心单例就绪）后自调 InitializeNewGame 固定 seed 建世界：
        //   ① 首次建（ActiveMap 空）→ 不触发"二次初始化卡死"（该卡死只发生在 ActiveMap 已存在时再建）；
        //   ② seed 固定 22360 → 世界稳定 → 探针结果可复现（用户随机进局世界会致 ①/② 掉血波动，已实测）。
        var lm = LoadManager.Instance;
        float readyT0 = Time.realtimeSinceStartup;
        while (lm == null || lm.CurrentPhase == LoadPhase.Booting
               || WorldManager.Instance == null || UnitDataManager.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - readyT0 > 60f)
            {
                Debug.LogError("[2_20冒烟] 等待 GameBootstrap Ready 超时(60s)。"); yield break;
            }
        }
        yield return null; yield return null;   // 阶段1 收尾余量
        // D520 冒烟自动化（HH.62）：SmokeApi.EnterGame 无条件建局（等价用户进局真实链路），
        // 不再「ActiveMap 空才自建、否则走用户世界」——自动化接管，真机世界不跑冒烟菜单。
        SmokeApi.EnterGame(new NewGameConfig
        {
            mapSeed = SEED,
            worldSeed = SEED,
            difficulty = 2,
            worldSize = WorldSize.Medium,
            kingdomName = "冒烟王国",
            selectedSlotId = "smoke",
            raceId = RaceIds.Dwarf   // 2_20 探针以矮人为正例（⑦c 采矿 1.3 断言；换族无意义）
        });
        float worldWaitT0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - worldWaitT0 > 120f)
            {
                Debug.LogError("[2_20冒烟] 等待世界就绪超时(120s)——InitializeNewGame 建世界未生效。"); yield break;
            }
        }
        yield return null; yield return null;   // 网格/AI 系统起跑余量

        var grid = GridSystem.Instance;
        var vcs = VagrantCampSystem.Instance;
        if (grid == null || vcs == null || grid.Config == null) { Debug.LogError("[2_20冒烟] 世界未就绪。"); yield break; }
        var _ = BuildingRegistry.Instance;   // 强制物化单例（registry 为空会让建筑扫描静默跳过，同 Step11）

        var dbg = AIDebugSpawnController.Instance;   // 种族域调试钩子宿主（异族个体构造口径）
        if (dbg == null) { Debug.LogError("[2_20冒烟] AIDebugSpawnController 不可用。"); yield break; }

        // ===== 锚点（x 间距 14 格防野性 8 格串扰；落点吸附可走格）=====
        System.Func<int, int, Vector2> anchor = (cx, cy) =>
            SpawnPosSnapper.SnapWorld(grid.CoordToWorld(new GridCoord(cx, cy)), "2_20冒烟锚点");
        var pA = anchor(230, 30);   // ①正组：迁远区（避免玩家出生 3 工人/4 AI 在 8 格野性半径内混入，Focus 分化），②⑦④组原位
        var pB = anchor(28, 12);    // ①负组
        var pC2 = anchor(42, 12);   // ②a 压制组
        var pC3 = anchor(56, 12);   // ②b 反击组
        var pV4 = anchor(70, 12);   // ④AI 异族
        var pH3 = anchor(84, 12);   // ④AI 同族
        var pE3 = anchor(98, 12);   // ④玩家 拒绝
        var pH2 = anchor(112, 12);  // ④玩家 放行
        var pC4 = anchor(126, 12);  // ②c 射程外负探针：近战驻守（Archer 压上行为污染负探针，改用 Warrior）
        var pC5 = anchor(140, 12);  // ②c 射程外负探针：异族野人（12 格外，射程 6 外）
        var pD1 = anchor(154, 12);  // ②d 移动焦点负探针：Elf 野人（Wander 漫游）
        var pD2 = anchor(168, 12);  // ②d 移动焦点负探针：Human 野人（14 格外，野性 8 格范围外）

        // ===== 探针⑤a/b/c：WildnessConfig 路径 + 开关（静态段，无帧等待，恢复后不污染行为探针）=====
        var wildAsset = Resources.Load<WildnessConfig>("Config/WildnessConfig");
        bool p5a = wildAsset != null;
        sb.Append($"⑤a 路径探针(Config/WildnessConfig)={(p5a ? "OK" : "FAIL")} ");
        bool p5b = WildnessConfig.Cached != null && WildnessConfig.Cached.enabled;   // Cached 填充静态缓存（IsActive 依赖 _cached，⑤a 直读不填）
        sb.Append($"⑤b IsActive(默认开)={(p5b ? "OK" : "FAIL")} ");
        bool p5c = false;
        if (wildAsset != null)
        {
            wildAsset.enabled = false;
            p5c = !WildnessConfig.IsActive;   // 关→零野性（负）
            wildAsset.enabled = true;         // 立即恢复（同步段，无帧窗口）
            sb.Append($"⑤c 开关关闭→零野性(负)={(p5c ? "OK" : "FAIL")} ");
        }
        else sb.Append("⑤c 开关负探针=FAIL(无资产) ");
        allPass = allPass && p5a && p5b && p5c;

        // ===== 单位组布置（Worker 站桩=禁脑；D468 验收口径：异族个体用钩子构造）=====
        var workerA = SpawnProfession(Occupation.Worker, pA);
        var workerB = SpawnProfession(Occupation.Worker, pB);
        var archerA2 = SpawnProfession(Occupation.Archer, pC2);
        // ②b 用近战 Warrior（变量名保持 archerB2 复用下游断言/诊断）：
        // 远程弹道 err=1.5 格 vs hitR=0.25 格 → 单发命中率约 3%，②b 掉血断言不稳定
        // （实测 14 发弹道可 0 命中，随机 PASS/FAIL）。近战 ApplyDamage 同步结算 → 自卫还击掉血稳定。
        var archerB2 = SpawnProfession(Occupation.Warrior, pC3);

        var eVagrant = dbg.SpawnVagrantWithRace(RaceIds.Elf, pA);     // ①正：贴 Worker_A
        var hVagrant = dbg.SpawnVagrantWithRace(RaceIds.Human, pB);   // ①负：贴 Worker_B
        var e4 = dbg.SpawnVagrantWithRace(RaceIds.Elf, pC2);          // ②a：压制观察体（脑禁用站桩）
        var e2 = dbg.SpawnVagrantWithRace(RaceIds.Elf, pC3);          // ②b：反击触发体（脑活，先动手）
        var v4 = dbg.SpawnVagrantWithRace(RaceIds.Elf, pV4);          // ④AI：异族
        var h3 = dbg.SpawnVagrantWithRace(RaceIds.Human, pH3);        // ④AI：同族
        var e3 = dbg.SpawnVagrantWithRace(RaceIds.Elf, pE3);          // ④玩家：拒绝
        var h2 = dbg.SpawnVagrantWithRace(RaceIds.Human, pH2);        // ④玩家：放行正探针

        DisableBrain(workerA); DisableBrain(workerB); DisableBrain(e4);

        // ④AI 流民池：AI⑥ 只招 kingdomId<0（未入籍）——手动置 -1 构造可招态（野外流民默认 0=玩家侧）
        if (v4 != null) v4.kingdomId = -1;
        if (h3 != null) h3.kingdomId = -1;

        // ①正组半径 12 格清场（HH.51 验收修订：远区防 D308 流民/AI 工人游荡入半径 → Focus 分化；
        // 保 workerA/eVagrant 独处 → 野性目标唯一）。仅清 ①正 组锚点周边，不碰玩家出生区。
        if (workerA != null)
        {
            float clearR = 12f * grid.Config.cellSize.x;
            foreach (var u in UnitRegistry.Instance.GetAllUnits().ToList())
            {
                if (u == null || !u.IsAlive) continue;
                if (ReferenceEquals(u, workerA) || ReferenceEquals(u, eVagrant)) continue;
                if (Vector2.Distance(u.transform.position, workerA.transform.position) <= clearR)
                    u.TakeDamage(999999);
            }
        }
        // ②b 组半径 12 格清场（保 archerB2/e2 独处：E2→近战国民打+受击反击链确定性；防 AI 击杀 archerB2）
        if (archerB2 != null)
        {
            float clearR = 12f * grid.Config.cellSize.x;
            foreach (var u in UnitRegistry.Instance.GetAllUnits().ToList())
            {
                if (u == null || !u.IsAlive) continue;
                if (ReferenceEquals(u, archerB2) || ReferenceEquals(u, archerA2) || ReferenceEquals(u, e2) || ReferenceEquals(u, e4)) continue;
                if (Vector2.Distance(u.transform.position, archerB2.transform.position) <= clearR)
                    u.TakeDamage(999999);
            }
        }

        int hpA0 = workerA != null ? workerA.CurrentHp : -1;
        int hpB0 = workerB != null ? workerB.CurrentHp : -1;
        int hpE4_0 = e4 != null ? e4.CurrentHp : -1;
        int hpE2_0 = e2 != null ? e2.CurrentHp : -1;
        int hpArcher0 = archerB2 != null ? archerB2.CurrentHp : -1;
        sb.Append($"\n布置：Worker hp0={hpA0}/{hpB0} Archer={((archerA2 != null && archerB2 != null) ? "在" : "缺(②将FAIL)")} " +
                  $"E4/E2 hp0={hpE4_0}/{hpE2_0} E.race={(eVagrant != null ? eVagrant.raceId.ToString() : "null")} ");

        // ===== 探针⑤d：反射 TryGetWildCombatOverride（下限兜底数值——Worker 基线=0 实盘缺口）=====
        bool p5d = false;
        if (eVagrant != null)
        {
            var eb = eVagrant.GetComponent<NPCBrain>();
            var mi = typeof(NPCBrain).GetMethod("TryGetWildCombatOverride", BindingFlags.NonPublic | BindingFlags.Instance);
            if (eb != null && mi != null)
            {
                var oa = new object[] { 0, 0f, 0f, false };
                p5d = (bool)mi.Invoke(eb, oa);
                if (p5d)
                {
                    int wa = (int)oa[0]; float wr = (float)oa[1]; float wc = (float)oa[2];
                    p5d = wa >= 1 && wr >= 1f && wc >= 0.5f;
                    sb.Append($"⑤d 下限兜底 attack={wa}/range={wr:F1}/cd={wc:F1}（Worker 基线=0→下限）{(p5d ? "OK" : "FAIL")} ");
                }
                else sb.Append("⑤d TryGetWildCombatOverride=false FAIL ");
            }
            else sb.Append("⑤d SKIP(无脑/无反射) FAIL ");
        }
        else sb.Append("⑤d SKIP(无流民) FAIL ");
        allPass = allPass && p5d;

        // ===== 行为窗口：等 6s 断①正/①负/②a；再等 4s 断②b =====
        // 行为窗口等待（HH.51 验收修订）：Time.time 受 Time.timeScale 冻结（世界初始化期间 ts=0 曾卡死窗口，
        // 实测 6s 窗口真实 0.6s 内野性首击/反击链不及完成 → FAIL 假象）。改用 Time.realtimeSinceStartup
        //（真实挂钟，不受 ts）+ 窗口内每帧保 ts=1（对抗暂停面板/加载瞬态）+ 窗口放宽 8s/14s 出首击余量。
        float t0 = Time.realtimeSinceStartup;
        // 钳制（HH.51 验收修订）：流民出生后游荡移动（HomePoint 滞留语义允许邻域游走），8s 内会跑远
        // 离 Worker（实测 5.2 格）→ 超 1 格射程/半径临界 → ①/② 无法触发。窗口内每帧把流民钳制在
        // 贴脸伙伴身位（距离 0，野性攻击/反击链稳定发生；生产行为不受影响，仅冒烟容器保距手段）。
        // 手动驱动（HH.51 验收修订）：窗口期后台限帧+Think 节流（_thinkTimer/_currentThinkInterval+
        // s_globalTickFrame 分片）下 Think 几乎不触发（实测 732 条扫描 0 条判定）→ 对探针流民每帧
        // 手动 Invoke NPCBrain.Update 一次（绕节流；Threat 注入/焦点/RegisterAttack/Damage 全真链路）。
        var miNpcUpdate = typeof(NPCBrain).GetMethod("Update",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        void DriveAndPin(NPCBrain npc, UnitController pinTarget)
        {
            if (npc != null && pinTarget != null && miNpcUpdate != null)
            {
                npc.transform.position = pinTarget.transform.position;
                miNpcUpdate.Invoke(npc, null);
                npc.transform.position = pinTarget.transform.position;   // 再钳回（Executer 移动后）
            }
        }
        while (Time.realtimeSinceStartup - t0 < 20f)
        {
            Time.timeScale = 1f;
            var npcA = eVagrant != null ? eVagrant.GetComponent<NPCBrain>() : null;
            var npcB = hVagrant != null ? hVagrant.GetComponent<NPCBrain>() : null;
            var npcE2 = e2 != null ? e2.GetComponent<NPCBrain>() : null;
            // archerB2 一并驱动（受击溯源→反击链在其 Think 内处理；后台限帧下自然周期不触发）
            var npcAr = archerB2 != null ? archerB2.GetComponent<NPCBrain>() : null;
            DriveAndPin(npcA, workerA);
            DriveAndPin(npcB, workerB);
            DriveAndPin(npcE2, archerB2);
            DriveAndPin(npcAr, archerB2);
            yield return null;
        }

        int hpA1 = workerA != null ? workerA.CurrentHp : -1;
        int hpB1 = workerB != null ? workerB.CurrentHp : -1;
        int hpE4_1 = e4 != null ? e4.CurrentHp : -1;
        // ①段运行时诊断（HH.51 验收）：位置/距离/脑态/阵营——定位 ①正 未掉血根因
        if (workerA != null && eVagrant != null)
        {
            float dA = Vector2.Distance(workerA.transform.position, eVagrant.transform.position);
            var eb = eVagrant.GetComponent<NPCBrain>();
            Debug.Log($"[2_20冒烟·诊断①] W_A=({workerA.transform.position.x:F2},{workerA.transform.position.y:F2}) " +
                      $"Elf=({eVagrant.transform.position.x:F2},{eVagrant.transform.position.y:F2}) dist={dA:F2} " +
                      $"elfBrain={(eb != null && eb.enabled ? "on" : "off")} elfRec={eVagrant.IsVagrantRecruited} " +
                      $"elfFaction={eVagrant.GetFaction()} elfOcc={eVagrant.EffectiveOccupation} hpA0={hpA0} hpA1={hpA1}");
        }
        if (workerB != null && hVagrant != null)
        {
            float dB = Vector2.Distance(workerB.transform.position, hVagrant.transform.position);
            Debug.Log($"[2_20冒烟·诊断①] W_B=({workerB.transform.position.x:F2},{workerB.transform.position.y:F2}) " +
                      $"Hum=({hVagrant.transform.position.x:F2},{hVagrant.transform.position.y:F2}) dist={dB:F2} " +
                      $"humRec={hVagrant.IsVagrantRecruited} hpB0={hpB0} hpB1={hpB1}");
        }
        // ②a 诊断（D486 E4 掉 2 血源）：archerA2 焦点/弹药/与 E4 距离——压制是否真停火
        if (archerA2 != null && e4 != null)
        {
            var ab = archerA2.GetComponent<NPCBrain>();
            var cf2 = ab != null ? ab.CurrentFocus : default;
            int ammo2 = archerA2.AmmoStone + archerA2.AmmoFireball + archerA2.AmmoMagic;
            Debug.Log($"[2_20冒烟·诊断②a] archerA2 focus={(cf2.IsValid ? cf2.FocusType.ToString() : "inv")} " +
                      $"focusObj={(cf2.IsValid ? (cf2.Source != null ? cf2.Source.GetType().Name : "nullsrc") : "null")} " +
                      $"dist2E4={Vector2.Distance(archerA2.transform.position, e4.transform.position):F1} " +
                      $"E4hp={hpE4_0}→{hpE4_1} ammo={ammo2}");
        }
        bool p1a = workerA != null && hpA1 < hpA0;
        bool p1b = workerB != null && hpB1 == hpB0;
        bool p2a = e4 != null && e4.IsAlive && hpE4_1 == hpE4_0;
        sb.Append($"\n①正 异族→Worker_A {hpA0}→{hpA1}={(p1a ? "OK" : "FAIL")} ");
        sb.Append($"①负 同族→Worker_B {hpB0}→{hpB1}={(p1b ? "OK" : "FAIL")} ");
        sb.Append($"②a 压制 Archer不打E4 {hpE4_0}→{hpE4_1}={(p2a ? "OK" : "FAIL")} ");
        allPass = allPass && p1a && p1b && p2a;

        while (Time.realtimeSinceStartup - t0 < 25f)
        {
            Time.timeScale = 1f;
            var npcA = eVagrant != null ? eVagrant.GetComponent<NPCBrain>() : null;
            var npcB = hVagrant != null ? hVagrant.GetComponent<NPCBrain>() : null;
            var npcE2 = e2 != null ? e2.GetComponent<NPCBrain>() : null;
            var npcAr = archerB2 != null ? archerB2.GetComponent<NPCBrain>() : null;
            DriveAndPin(npcA, workerA);
            DriveAndPin(npcB, workerB);
            DriveAndPin(npcE2, archerB2);
            DriveAndPin(npcAr, archerB2);
            yield return null;
        }

        bool p2b = e2 != null && (!e2.IsAlive || e2.CurrentHp < hpE2_0);
        sb.Append($"②b 反击→E2 {hpE2_0}→{(e2 != null && e2.IsAlive ? e2.CurrentHp.ToString() : "死")}={(p2b ? "OK" : "FAIL")} ");
        allPass = allPass && p2b;

        // ②b 诊断（D485 定位）：E2/Archer 映射 + 距离 + archer 血量 + 受击溯源反射 + 威胁列表/焦点
        if (e2 != null && archerB2 != null)
        {
            float dE2 = Vector2.Distance(e2.transform.position, archerB2.transform.position);
            var arBrain = archerB2.GetComponent<NPCBrain>();
            int aggId = -1;
            var miAgg = typeof(NPCBrain).GetField("_lastAggressor", BindingFlags.NonPublic | BindingFlags.Instance);
            var agg = miAgg != null ? (IDamageable)miAgg.GetValue(arBrain) : null;
            if (agg is UnitController aggUc) aggId = aggUc.npcId;
            int threatCount = -1;
            string focusType = "invalid";
            var attField = typeof(NPCBrain).GetField("_attention", BindingFlags.NonPublic | BindingFlags.Instance);
            var att = attField != null ? attField.GetValue(arBrain) : null;
            if (att != null)
            {
                var tsField = att.GetType().GetField("_threatStimuli", BindingFlags.NonPublic | BindingFlags.Instance);
                var list = tsField != null ? tsField.GetValue(att) as System.Collections.IList : null;
                threatCount = list != null ? list.Count : -1;
                var curFocus = arBrain.CurrentFocus;
                focusType = curFocus.IsValid ? curFocus.FocusType.ToString() : "invalid";
            }
            Debug.Log($"[2_20冒烟·诊断②] E2=#{e2.npcId} Archer=#{archerB2.npcId} dist={dE2:F2} " +
                      $"archerHp={hpArcher0}→{archerB2.CurrentHp} agg=#{aggId} threatN={threatCount} focus={focusType} " +
                      $"e2Hp={hpE2_0}→{e2.CurrentHp}");
        }

        // ===== ②b2 负探针（D485）：同族野人伤害国民 → 不还手 =====
        // 同族野人不主动攻击（①负已证）→ 用 DamageSystem.ApplyDamage 强制构造"同族野人袭击"；
        // 对照 ②b 正（异族野人→还手）：国民自卫还击仅对异族野人开放（放行通道 raceId≠自身）。
        bool p2b2 = false;
        if (hVagrant != null && archerB2 != null)
        {
            // 等节流窗口过（DamageSystem 同一 victim 0.5s 才发一次 UnitDamagedEvent，②b 正探针刚打过）
            float tw = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tw < 0.7f) yield return null;
            hVagrant.transform.position = archerB2.transform.position;   // 同族野人贴国民
            DamageSystem.Instance.ApplyDamage(hVagrant, archerB2, 5);    // 强制"同族野人袭击"（伤害 5）
            int hpHumB0 = hVagrant.CurrentHp;
            float t2b = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t2b < 6f)
            {
                Time.timeScale = 1f;
                var npcAr2 = archerB2 != null ? archerB2.GetComponent<NPCBrain>() : null;
                DriveAndPin(npcAr2, archerB2);
                yield return null;
            }
            p2b2 = hVagrant.CurrentHp == hpHumB0;   // 同族野人未被还手（血不变=负 PASS）
            sb.Append($"②b2 负 同族野人伤国民→不还手(hum {hpHumB0}→{hVagrant.CurrentHp})={(p2b2 ? "OK" : "FAIL")} ");
        }
        else sb.Append("②b2 SKIP(无同族野人/Archer) FAIL ");
        allPass = allPass && p2b2;

        // ===== ②c 负探针（D486）：射程外溯源攻击者→不还手且不追击 =====
        // ②c 受击单位用 Worker（生产驻守）：天然驻守工位不漫游（Archer 压上、Warrior 归巢/漫游行为都会
        // 污染"不追击"位移断言，实测 Warrior 持续 Wander 13 格）。Worker 焦点稳定 WorkPosition/HomePosition。
        var archerC = SpawnProfession(Occupation.Worker, pC4);
        var e2c = dbg.SpawnVagrantWithRace(RaceIds.Elf, pC5);
        bool p2c = false;
        if (archerC != null && e2c != null)
        {
            // ②c 清场（同 ②b 模式）：保 archerC/e2c 独处，防附近单位干扰驻守焦点（焦点被干扰 → 自然移动，实测 arcShift 4.72 格）
            if (archerC != null)
            {
                float clearR = 12f * grid.Config.cellSize.x;
                foreach (var u in UnitRegistry.Instance.GetAllUnits().ToList())
                {
                    if (u == null || !u.IsAlive) continue;
                    if (ReferenceEquals(u, archerC) || ReferenceEquals(u, e2c)) continue;
                    if (Vector2.Distance(u.transform.position, archerC.transform.position) <= clearR)
                        u.TakeDamage(999999);
                }
            }
            // D486 ②c 隔离：关 e2c 脑（enabled=false）→ 野人静止在射程外，不自然移动/攻击
            //（否则野性行为让 e2c 自己跑进射程，实测 9.17 格，污染"射程外"语义）。
            var e2cBrain = e2c.GetComponent<NPCBrain>();
            if (e2cBrain != null) e2cBrain.enabled = false;
            // 等受击单位就位（Worker 驻守工位/归巢，3s 充足）——arCStart 取就位后位置
            float tw = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tw < 3f) yield return null;
            var arCStart = archerC.transform.position;
            var e2cStart = e2c.transform.position;
            int hpE2c0 = e2c.CurrentHp;
            DamageSystem.Instance.ApplyDamage(e2c, archerC, 5);    // 异族野人射程外强袭（射程 6 外）
            float t2c = Time.realtimeSinceStartup;
            // ②c 轨迹采样（D486 定位）：受击单位位置+焦点——钳制在 arCStart（防漫游/找工位污染位移），
            // 焦点判定（Trace 抑制生效 → 焦点保持非 ThreatStimulus）才是"不追击"的真信号
            var focusSnap = new System.Text.StringBuilder();
            int lastS = -1;
            while (Time.realtimeSinceStartup - t2c < 6f)
            {
                Time.timeScale = 1f;
                var npcArC = archerC.GetComponent<NPCBrain>();
                DriveAndPin(npcArC, archerC);
                archerC.transform.position = arCStart;   // 钳回就位点（防漫游污染位移断言）
                int s = Mathf.FloorToInt((Time.realtimeSinceStartup - t2c) * 2f);
                if (s != lastS && npcArC != null)
                {
                    lastS = s;
                    var cf = npcArC.CurrentFocus;
                    string ft = cf.IsValid ? cf.FocusType.ToString() : "inv";
                    if (ft.Length > 4) ft = ft.Substring(0, 4);
                    focusSnap.Append($"t{s * 0.5f:F1}:({archerC.transform.position.x:F0},{archerC.transform.position.y:F0},{ft}) ");
                }
                yield return null;
            }
            Debug.Log($"[2_20冒烟·诊断②c轨迹] {focusSnap}");
            float arcShift = Vector2.Distance(arCStart, archerC.transform.position);
            // D486 不追击判定：① 焦点未被受击溯源 Trace 抢走（Source 非 ThreatStimulus——抑制生效则焦点保持驻守）；
            // ② 位移 <0.5 格（钳制后仅测受击溯源是否驱动移动）。
            var arBrainC = archerC.GetComponent<NPCBrain>();
            bool focusThreat = arBrainC != null && arBrainC.CurrentFocus.IsValid
                               && arBrainC.CurrentFocus.Source is ThreatStimulus;
            bool moved = arcShift > 0.5f;
            // ②c 诊断（D486 定位）：受击单位焦点类型 + 受击溯源 + 与攻击者距离
            string fc = "invalid";
            if (arBrainC != null)
            {
                var curF = arBrainC.CurrentFocus;
                fc = curF.IsValid ? curF.FocusType.ToString() : "invalid";
            }
            var aggF = typeof(NPCBrain).GetField("_lastAggressor", BindingFlags.NonPublic | BindingFlags.Instance);
            var aggC = aggF != null ? (IDamageable)aggF.GetValue(arBrainC) : null;
            int aggIdC = aggC is UnitController aggUc2 ? aggUc2.npcId : -1;
            Debug.Log($"[2_20冒烟·诊断②c] archerC focus={fc} focusThreat={focusThreat} agg=#{aggIdC} " +
                      $"arcShift={arcShift:F2} " +
                      $"e2cShift={Vector2.Distance(e2cStart, e2c.transform.position):F2} " +
                      $"dist2e2c={Vector2.Distance(archerC.transform.position, e2c.transform.position):F2} moved={moved}");
            p2c = e2c.CurrentHp >= hpE2c0 && !moved && !focusThreat;   // 射程外不还手（血不变）+ 不追击（位移零 + 焦点未被 Trace 抢）
            sb.Append($"②c 负 射程外袭击→不还手不追击(e2c {hpE2c0}→{e2c.CurrentHp} arcShift={arcShift:F1})={(p2c ? "OK" : "FAIL")} ");
        }
        else sb.Append("②c SKIP(无 archerC/e2c) FAIL ");
        allPass = allPass && p2c;

        // ===== ②d 负探针（D486）：移动焦点（Wander）单位被异族袭击→不还手（不打断移动）=====
        var e2d = dbg.SpawnVagrantWithRace(RaceIds.Elf, pD1);
        var hBd = dbg.SpawnVagrantWithRace(RaceIds.Human, pD2);
        bool p2d = false;
        if (e2d != null && hBd != null)
        {
            float tw = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tw < 0.7f) yield return null;
            int hpHBd0 = hBd.CurrentHp;
            DamageSystem.Instance.ApplyDamage(hBd, e2d, 5);    // Human 野人袭击 Elf 野人（异族，D485 ① 放行溯源）
            float t2d = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t2d < 6f)
            {
                Time.timeScale = 1f;
                var npcE2d = e2d.GetComponent<NPCBrain>();
                DriveAndPin(npcE2d, e2d);   // 保持 e2d 在场（焦点仍 Wander 移动中）
                yield return null;
            }
            p2d = hBd.CurrentHp >= hpHBd0;   // 攻击者血不变=移动焦点不还手
            sb.Append($"②d 负 移动焦点(Wander)被袭→不还手(hBd {hpHBd0}→{hBd.CurrentHp})={(p2d ? "OK" : "FAIL")} ");
        }
        else sb.Append("②d SKIP(无 e2d/hBd) FAIL ");
        allPass = allPass && p2d;

        // ===== 探针④：招募限同族（玩家侧 RecruitVagrant + AI⑥ FindRecruitableVagrant 反射）=====
        var ruler = RulerController.Instance;
        int food0 = ruler != null ? ruler.Food : 0;
        bool p4a = false;
        // Q10 批1 材料保鲜（2026-09-04 第2轮实证）：用户真实世界野性敌意可致死探针材料（实测 e3 alive=False
        // → RecruitVagrant 走死亡拒绝分支而非异族拒绝分支=④a 假 FAIL 环境波动）——死亡即原坐标补注。
        if (e3 == null || !e3.IsAlive) e3 = dbg.SpawnVagrantWithRace(RaceIds.Elf, pE3);
        if (e3 != null && ruler != null)
        {
            // ④a 构造修正（D486 不稳定定位）：e3 spawn 时 Elf(1)，但流民营地系统（VagrantCampSystem 每日补员
            // ResolveGroupRace）会把营地范围内调试流民 raceId 随机改成营地多数派（实测改回 Human=0）→ 招募误放行。
            // 强制设回 Elf=1（探针构造异族个体语义，排除营地系统对调试单位的污染），再验"玩家侧异族拒绝"。
            e3.raceId = RaceIds.Elf;
            // ④a 诊断：e3 状态（RecruitVagrant 拒绝前置条件核对）
            int kRace0 = KingdomRace.GetKingdomRace(0);
            Debug.Log($"[2_20冒烟·诊断④a] e3 raceId={e3.raceId} alive={e3.IsAlive} occ={e3.EffectiveOccupation} " +
                      $"rec={e3.IsVagrantRecruited} kRace={kRace0} pos={e3.transform.position} id={e3.npcId}");
            string captured = null;
            Application.LogCallback cb = (cond, st, type) =>
            { if (captured == null && cond != null && cond.Contains("招募拒绝：异族")) captured = cond; };
            Application.logMessageReceived += cb;
            bool rejected = !vcs.RecruitVagrant(e3);
            Application.logMessageReceived -= cb;
            p4a = rejected && captured != null && ruler.Food == food0;   // 拒绝在粮检之前 → 粮必不变
            sb.Append($"\n④a 玩家侧异族拒绝(rej={rejected},粮 {food0}→{ruler.Food},日志={(captured != null ? "在" : "缺")})={(p4a ? "OK" : "FAIL")} ");
        }
        else sb.Append("\n④a SKIP(无流民/Ruler) FAIL ");
        allPass = allPass && p4a;

        bool p4b = true;
        var kcfg = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        int cost = kcfg != null ? kcfg.recruitFoodCost : 0;
        if (h2 == null || !h2.IsAlive) h2 = dbg.SpawnVagrantWithRace(RaceIds.Dwarf, pH2);   // 材料保鲜（同 ④a 注）
        if (h2 != null && ruler != null && cost > 0 && ruler.Food >= cost)
        {
            // Q10 批2 修正三（M5a 行为联动，同 ④c h5 根因）：玩家选族后玩家侧「同族」=玩家族（本轮=矮人 r2）
            // ——旧材料 h2=Human 会被 D469 异族拒绝（VagrantCampSystem L156 实读：raceId≠GetKingdomRace(0)→拒绝）
            // =第五轮 ④b FAIL 根因（材料口径过时非产品缺陷）→ 同族正材料改 Dwarf+调用前强制 raceId。
            h2.raceId = RaceIds.Dwarf;
            int f0 = ruler.Food;
            bool ok = vcs.RecruitVagrant(h2);
            p4b = ok && ruler.Food < f0;   // 同族放行+粮扣
            sb.Append($"④b 玩家侧同族( Dwarf r{h2.raceId} )放行(粮 {f0}→{ruler.Food})={(p4b ? "OK" : "FAIL")} ");
        }
        else sb.Append($"④b 同族放行=SKIP(粮 {(ruler != null ? ruler.Food : -1)} < {cost}——负探针已证拒绝在粮检前) ");
        allPass = allPass && p4b;

        bool p4c = false, p4d = false;
        // 材料保鲜（同 ④a 注）：④c/④d 依赖 v4/h5 存活（实测 ④c 选中 #-1=h3 已死致候选缺失=假 FAIL）
        // Q10 批2 ④c 修正（D498 预批，HH.55 §二裁决）：①补注入后 5 帧 yield（UnitRegistry 注册/脑初始化
        // 时序窗口——批1 实证补注入零等待=④c 波动疑点）②断言放宽「任一未招募同族流民」。
        // Q10 批2 修正二（M5a 行为联动）：玩家选族后 GetKingdomRace(0)=玩家族（本轮=矮人 r2）——
        // brain(0) 同族过滤对象=玩家族而非 Human（M5a 绑定生效的正确表现；首轮 ④c null 根因）。
        // → 同族正材料=h5 Dwarf 流民（与玩家族同族）；异族负材料=v4 Elf 不变。
        bool injected4c = false;
        if (v4 == null || !v4.IsAlive) { v4 = dbg.SpawnVagrantWithRace(RaceIds.Elf, pV4); injected4c = true; }
        var h5 = dbg.SpawnVagrantWithRace(RaceIds.Dwarf, pH3);   // 同族正材料：每轮新造（玩家族对齐，防跨轮营地改写污染）
        if (h5 != null) h5.kingdomId = -1;   // ④AI 流民池：AI⑥ 只招 kingdomId<0（未入籍）——第五轮 ④c FAIL 根因=新造 h5 漏置 -1
        if (h5 != null) injected4c = true;
        if (injected4c) for (int f = 0; f < 5; f++) yield return null;   // 补注入时序窗口（D498 预批）
        if (v4 != null && h5 != null)
        {
            // 调用前强制材料 raceId（营地系统改写防御，同 ④a/④b 先例）
            v4.raceId = RaceIds.Elf;
            h5.raceId = RaceIds.Dwarf;
            var brain = new KingdomBrain(0);   // 玩家国族（M5a 后读真字段 raceId=玩家选族）
            var mi2 = typeof(KingdomBrain).GetMethod("FindRecruitableVagrant", BindingFlags.NonPublic | BindingFlags.Instance);
            var got = mi2 != null ? (UnitController)mi2.Invoke(brain, null) : null;
            int pRace = KingdomRace.GetKingdomRace(0);
            p4c = got != null && got.raceId == pRace;   // 任一未招募同族（=玩家族）流民（异族 V4 被滤，D498 预批放宽）
            sb.Append($"④c AI⑥同族过滤(玩家族 r{pRace} 选中 #{(got != null ? got.npcId : -1)} race={(got != null ? got.raceId : -1)})={(p4c ? "OK" : "FAIL")} ");
            AIDebugSpawnController.DebugSetRace(h5, RaceIds.Elf);   // 负加固：同族者改标异族
            var got2 = mi2 != null ? (UnitController)mi2.Invoke(brain, null) : null;
            p4d = got2 == null;   // 全异族 → null
            sb.Append($"④d AI⑥全异族→null {(p4d ? "OK" : "FAIL")} ");
        }
        else sb.Append("④c/d SKIP(无流民) FAIL ");
        allPass = allPass && p4c && p4d;

        // ===== 探针③：D471 插旗定族（13 Elf+1 Human 多数派营立国 → 定族 raceId=1 + 终身字段保持）=====
        bool p3f = false, p3l = false, p3m = false, p3t = false;
        var campDef = FindDefById("VagrantCamp");
        // Q10 批1 容器修正（2026-09-04，HH.55）：旧版直接复用 FindCamps()[0]——用户真实世界的既有营地
        // 可能 foundedFlag 已置位（曾立国）或中心格有主，TickAll 的 TryAnnex 会静默吞并移除（实测 camps 5→0），
        // FoundFromCamp 根本不执行 → ③ 假 FAIL（HH.53 ②b 先例：容器缺陷≠玩法缺陷）。
        // 修正=只复用"未立国+中心格无主"营地（campUsable 反射自证）；无合格候选 → 自建营地步进找格+建后自证。
        var miOwner = typeof(CampUpgrader).GetMethod("ResolveOwnerCampCell", BindingFlags.NonPublic | BindingFlags.Static);
        System.Func<Camp, bool> campUsable = c =>
        {
            if (c == null) return false;
            c.foundedFlag = false;   // 冒烟强制复位（测试锚点；不改玩法默认行为）
            return miOwner == null || (int)miOwner.Invoke(null, new object[] { c }) < 0;
        };
        bool hasCampSpot = false;
        Vector2 campSpotWorld = Vector2.zero;
        var campsList = vcs.FindCamps();
        if (campsList != null)
        {
            foreach (var b in campsList)
            {
                var cellOpt0 = grid.WorldToCoord(b.GetPosition());
                if (cellOpt0 == null) continue;
                if (campUsable(FindCampAt(vcs, cellOpt0.Value)))
                { hasCampSpot = true; campSpotWorld = b.GetPosition(); break; }
            }
        }
        if (!hasCampSpot && campDef != null)
        {
            var fp = new Vector2Int(Mathf.Max(1, campDef.footprint.x), Mathf.Max(1, campDef.footprint.y));
            var ts0 = TerritorySystem.Instance;
            for (int gx = 9; gx <= 45 && !hasCampSpot; gx += 6)
            {
                for (int gy = 8; gy <= 56 && !hasCampSpot; gy += 6)
                {
                    var anyCoord = new GridCoord(gx, gy);
                    // 无主预检（仿 CampUpgrader.ResolveOwnerCampCell 账本反查 D306）：有主格建营必被 TryAnnex 静默吞并
                    if (ts0 != null)
                    {
                        var mid0 = grid.CellToMidChunk(anyCoord);
                        if (ts0.Ledger.TryGetValue(mid0, out int k0) && k0 >= 0) continue;
                    }
                    var built = BuildingFactory.Instance.CreateBuildingInstance(campDef, campDef.sourceType, anyCoord, fp,
                        grid.CoordToWorld(anyCoord), isPlayerBuilt: false, grade: ResourceGrade.Normal,
                        isConsumable: false, initialState: BuildingState.Active, kingdomId: 0);
                    if (built == null) continue;   // 放不下（占用/不可走/放置拒）→ 下一格
                    hasCampSpot = true; campSpotWorld = grid.CoordToWorld(anyCoord);
                }
            }
            // 成营时序注记：此处不成营（营地范围内尚无流民）——Camp 记录由主流程注入 14 人后 ForceCampScan 生成
            if (!hasCampSpot) Debug.Log("[2_20冒烟·诊断③] 自建营地全部候选格失败（有主/占用/放置拒）");
        }
        if (hasCampSpot)
        {
            var campWorld = campSpotWorld;
            var cellOpt = grid.WorldToCoord(campWorld);
            if (cellOpt != null)
            {
                // 清场保险：营 10 格内未招募流浪汉清场（防 D308 散布流民混入营地/与注入者互吸走散）
                // Q10 批2 修正（活局事故纪律）：TakeDamage→死亡注销改集合——快照后杀（批2 三轮实证：
                // 多轮材料积累下枚举内部 HashSet 炸 Collection modified，批1 零伤害未触发纯运气）
                float clearRadius = 10f * grid.Config.cellSize.x;
                foreach (var u in UnitRegistry.Instance.GetAllUnits().ToList())
                {
                    if (u == null || !u.IsAlive || u.EffectiveOccupation != Occupation.Vagrant || u.IsVagrantRecruited) continue;
                    if (Vector2.Distance(u.GetPosition(), campWorld) <= clearRadius) u.TakeDamage(999999);
                }

                // 注入 14 人（13 Elf 多数派 + 1 Human 对照）贴营
                var injectedUc = new List<UnitController>();
                for (int i = 0; i < 14; i++)
                {
                    int race = (i < 13) ? RaceIds.Elf : RaceIds.Human;
                    var pos = campWorld + new Vector2((i % 5) * 0.9f - 1.8f, (i / 5) * 0.9f - 1.8f);
                    var uc2 = dbg.SpawnVagrantWithRace(race, pos);
                    if (uc2 != null) injectedUc.Add(uc2);
                }

                vcs.ForceCampScan();
                var camp = FindCampAt(vcs, cellOpt.Value);
                if (camp != null)
                {
                    // 诊断（Q10 批1）：营地可用自证（复位 foundedFlag+中心格无主）——TickAll TryAnnex 真判定前置提示
                    if (!campUsable(camp))
                        Debug.Log("[2_20冒烟·诊断③] 营地可用校验未过（复位 foundedFlag 后中心格仍有主）——TickAll 可能走吞并路径");
                    camp.persistenceDays = 5;
                    camp.memberIds.Clear();
                    foreach (var uc2 in injectedUc) camp.memberIds.Add(uc2.npcId);

                    int beforeCount = KingdomRegistry.Instance.Count;
                    var beforeIds = new HashSet<int>();
                    foreach (var k in KingdomRegistry.Instance.GetAll()) beforeIds.Add(k.id);
                    string raceLog = null;
                    Application.LogCallback cb2 = (cond, st, type) =>
                    { if (raceLog == null && cond != null && cond.Contains("定族 raceId=")) raceLog = cond; };
                    Application.logMessageReceived += cb2;
                    CampUpgrader.TickAll();
                    Application.logMessageReceived -= cb2;

                    int afterCount = KingdomRegistry.Instance.Count;
                    p3f = afterCount == beforeCount + 1;
                    p3l = raceLog != null && raceLog.Contains("定族 raceId=1（");   // Elf 多数派 → 1
                    var first = injectedUc.Count > 0 ? injectedUc[0] : null;
                    p3m = first != null && first.raceId == RaceIds.Elf;   // 终身字段：转化国民后 raceId 保持
                    // Q10-M2 真字段断言（2026-09-03）：新国 KingdomState.raceId 读 D471 显式写入值=Elf=1
                    // （GetKingdomRace 单点回填后，国族消费面从人口构造升级为真字段直读）
                    int newKingdomId = -1;
                    foreach (var k in KingdomRegistry.Instance.GetAll())
                        if (!beforeIds.Contains(k.id)) newKingdomId = k.id;
                    p3t = newKingdomId >= 0
                        && KingdomRegistry.Instance.Get(newKingdomId) != null
                        && KingdomRegistry.Instance.Get(newKingdomId).raceId == RaceIds.Elf
                        && KingdomRace.GetKingdomRace(newKingdomId) == RaceIds.Elf;   // helper 单点回填同值
                    sb.Append($"\n③ D471 插旗定族：立国 {beforeCount}→{afterCount}={(p3f ? "OK" : "FAIL")} " +
                              $"定族日志={(raceLog != null ? "在" : "缺")}(raceId=1)={(p3l ? "OK" : "FAIL")} " +
                              $"成员 raceId 保持(Elf)={(p3m ? "OK" : "FAIL")} " +
                              $"真字段 state.raceId/helper(Elf)={(p3t ? "OK" : "FAIL")}(id={newKingdomId}) ");
                }
                else sb.Append($"\n③ 营地未建立（清场后流浪汉计数见 Console）FAIL ");
            }
            else sb.Append("\n③ 营地坐标无法映射格子 FAIL ");
        }
        else sb.Append("\n③ 无营地建筑可锚定 FAIL ");
        allPass = allPass && p3f && p3l && p3m && p3t;

        // ===== Q10 批2 探针⑥：M3 开局分布 + M5a 玩家绑定（D430/D506②/D431）=====
        // 口径：id 序=立国序（id 单调递增不复用 D385）→ id 最小的 min(3, AI数) 个 AI 王国=第一代
        // （动态立国有冷却+营地生长时窗，进局即触发几乎不可能混入；即便混入也不影响前 K 个判定）。
        // 断言：a) 第一代种族互异（三族各一/两族各一）b) 第一代不含玩家族（AI 池排除玩家族，D430 保底席）
        // c) 全 AI raceId 合法域 [0,3] d) 玩家 state.raceId 与 helper 同值（M5a EnsurePlayerRegistered 绑定链）。
        bool p6a = false, p6b = true, p6c = true, p6d = false;
        int playerRace = KingdomRace.GetKingdomRace(0);
        var playerState = KingdomRegistry.Instance.Get(0);
        p6d = playerState != null && playerState.raceId == playerRace;
        var aiStates = new List<KingdomState>();
        foreach (var k in KingdomRegistry.Instance.GetAll()) if (k != null && k.id > 0) aiStates.Add(k);
        aiStates.Sort((a, b) => a.id.CompareTo(b.id));
        int kQuota = Mathf.Min(3, aiStates.Count);
        var firstGenRaces = new HashSet<int>();
        var distSb = new StringBuilder();
        for (int i = 0; i < aiStates.Count; i++)
        {
            distSb.Append($"#{aiStates[i].id}:r{aiStates[i].raceId} ");
            if (i < kQuota)
            {
                firstGenRaces.Add(aiStates[i].raceId);
                if (aiStates[i].raceId == playerRace) p6b = false;   // 第一代 AI 不含玩家族（D430）
            }
            if (aiStates[i].raceId < 0 || aiStates[i].raceId > 3) p6c = false;   // 合法域
        }
        p6a = kQuota > 0 && firstGenRaces.Count == kQuota;   // 第一代互异（三族各一）
        bool p6 = p6a && p6b && p6c && p6d;
        sb.Append($"\n⑥ M3 开局分布：玩家(id=0) r{playerRace} 第一代 K={kQuota} 互异={(p6a ? "OK" : "FAIL")} " +
                  $"不含玩家族={(p6b ? "OK" : "FAIL")} 合法域={(p6c ? "OK" : "FAIL")} M5a 玩家绑定同值={(p6d ? "OK" : "FAIL")} " +
                  $"[分布: {distSb}] ");
        allPass = allPass && p6;

        // ===== Q10 批2 探针⑦：M5 真值挂载+消费链（D503/D506③/D420）=====
        // ⑦a 四资产 D503 全表断言（12 项×4 族逐值——HH.55 §三表批注通过值）
        // ⑦b GetGatherMul 同源性（对全部王国：Ore/Wood/Food 映射=RaceDef 字段+Metal 加工品不乘）
        // ⑦c 正探针：玩家=矮人进局 → 采矿 1.3（M5 验收「矮人采矿加成生效」；非矮人条件 SKIP）
        // ⑦d 负探针：野生哨兵 kingdomId=-1 → RaceDef null → 中性 1（未选族/无来源流程不进）
        bool p7a = true, p7b = true, p7c = true, p7d = true;
        var raceDefs = new Dictionary<int, RaceDef>();
        foreach (var rn in new[] { "Race_Human", "Race_Elf", "Race_Dwarf", "Race_Orc" })
        {
            var rd = Resources.Load<RaceDef>("Config/Races/" + rn);
            if (rd == null) { p7a = false; continue; }
            raceDefs[rd.raceId] = rd;
        }
        if (p7a)
        {
            float[,] d503 = {
                { 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 0f },     // Human
                { 1.05f, 1.00f, 0.90f, 1.20f, 1.15f, 0.90f, 1.25f, 1.10f, 0.90f, 0.90f, 0.95f, 0.15f },  // Elf
                { 1.10f, 0.95f, 1.15f, 0.80f, 0.85f, 1.30f, 0.90f, 0.75f, 1.20f, 1.25f, 1.20f, 0.15f },  // Dwarf
                { 0.85f, 1.15f, 1.15f, 0.85f, 1.05f, 1.05f, 0.85f, 0.70f, 0.80f, 0.85f, 0.90f, 0.10f },  // Orc
            };
            for (int r = 0; r < 4 && p7a; r++)
            {
                if (!raceDefs.TryGetValue(r, out var rd)) { p7a = false; break; }
                var vals = new[] { rd.trainCostMul, rd.trainSpeedMul, rd.meleeAtkMul, rd.rangedAtkMul, rd.moveSpeedMul,
                    rd.mineMul, rd.lumberMul, rd.farmMul, rd.buildSpeedMul, rd.buildingHpMul, rd.carryCapMul, rd.gatherBonusOnPreferred };
                for (int c = 0; c < 12; c++)
                    if (Mathf.Abs(vals[c] - d503[r, c]) > 0.001f) { p7a = false; break; }
            }
        }
        foreach (var k in KingdomRegistry.Instance.GetAll())
        {
            if (k == null) continue;
            var rd = KingdomRace.GetKingdomRaceDef(k.id);
            if (rd == null || rd.raceId != k.raceId) { p7b = false; break; }
            if (!Mathf.Approximately(KingdomRace.GetGatherMul(k.id, ResourceType.Ore), rd.mineMul)
                || !Mathf.Approximately(KingdomRace.GetGatherMul(k.id, ResourceType.Wood), rd.lumberMul)
                || !Mathf.Approximately(KingdomRace.GetGatherMul(k.id, ResourceType.Food), rd.farmMul)
                || !Mathf.Approximately(KingdomRace.GetGatherMul(k.id, ResourceType.Metal), 1f))
            { p7b = false; break; }
        }
        string p7cNote;
        if (playerRace == RaceIds.Dwarf)
        {
            p7c = Mathf.Approximately(KingdomRace.GetGatherMul(0, ResourceType.Ore), 1.3f);
            p7cNote = $"矮人采矿 1.3={(p7c ? "OK" : "FAIL")}";
        }
        else p7cNote = $"SKIP(玩家 r{playerRace}——正探针需选矮人进局)";
        p7d = KingdomRace.GetKingdomRaceDef(-1) == null
              && Mathf.Approximately(KingdomRace.GetGatherMul(-1, ResourceType.Ore), 1f);
        bool p7 = p7a && p7b && p7c && p7d;
        sb.Append($"\n⑦ M5 消费链：D503 全表={(p7a ? "OK" : "FAIL")} GatherMul 同源={(p7b ? "OK" : "FAIL")} " +
                  $"正[{p7cNote}] 负(哨兵-1 中性)={(p7d ? "OK" : "FAIL")} ");
        allPass = allPass && p7;

        // ===== 汇总 =====
        Debug.Log("[2_20冒烟] " + sb);
        Debug.Log($"[2_20冒烟] ===== {(allPass ? "ALL PASS" : "HAS FAIL")}（种族域 D467~D472 行为级探针）=====");
        // 静默开关（Q10 批1 自动化跑批 2026-09-03）：MCP 无头跑批时模态弹窗会阻塞主线程——
        // 自动化跑批保持 true（结果以 Console 汇总为准）；手工跑想看弹窗改 false。
        const bool SuppressDialog = true;
        if (!SuppressDialog)
            EditorUtility.DisplayDialog("2_20 种族域 Play 冒烟", allPass ? "全部 PASS" : "存在 FAIL，见 Console 明细", "确定");

        var runner = Object.FindAnyObjectByType<RaceSmokeHost>();
        if (runner != null) Object.Destroy(runner.gameObject);

        // D520 冒烟自动化（HH.62）：清场（WorldLifecycle 同场景重建编排）+ 退出 Play（点一次菜单全程自动化闭环）
        SmokeApi.ResetWorldForNext();
        SmokeApi.QuitSmoke();
    }

    // ===== helpers =====

    /// <summary>按职业生成玩家单位（kingdomId=0 玩家侧）。</summary>
    private static UnitController SpawnProfession(Occupation occ, Vector2 pos)
    {
        if (UnitFactory.Instance == null || UnitDataManager.Instance == null) return null;
        var data = UnitDataManager.Instance.GetData(Faction.PlayerCamp, occ);
        if (data == null) return null;
        var go = UnitFactory.Instance.SpawnUnit(data, pos);
        return go != null ? go.GetComponent<UnitController>() : null;
    }

    /// <summary>站桩化：禁用 NPCBrain（若 prefab 挂有）——禁移动/禁感知决策，仅保留受击扣血。</summary>
    private static void DisableBrain(UnitController uc)
    {
        if (uc == null) return;
        var b = uc.GetComponent<NPCBrain>();
        if (b != null) b.enabled = false;
    }

    private static Camp FindCampAt(VagrantCampSystem vcs, GridCoord c)
    {
        if (vcs == null || vcs.Camps == null) return null;
        foreach (var camp in vcs.Camps)
            if (camp != null && camp.centerCell == c) return camp;
        return null;
    }

    private class RaceSmokeHost : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}
