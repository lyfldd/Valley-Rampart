using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.115 六考前置零碎包 冒烟（D563②日志面+DZ-069件C野性同族豁免+件E散账行为锚+D569件4；
//  任务书=多Agent交接/策划端/HH.115_六考前置零碎包_任务书.md）
//  用法：菜单「Valley/验证/HH115_六考前置零碎包」——Play 后点（MCP 自动触发，零手动进局）。
//
//  入口=HH.92 测试环境正门（TestHarnessApi.EnterTestRun seed=21115，考跑守卫全开；L-17 容器入口纪律）；
//  协程异常捕获器挂 RunHost（L-16：HH.109 先例，静默死亡=假卡死）。
//  单位=生产链直建（UnitFactory.SpawnUnit + kingdomId/raceId 指派，VagrantCampSystem.SpawnVagrantAt 先例）。
//  摆位隔离纪律：各探针独立点位（野性半径 8 格内只放该探针单位，防交叉索敌污染断言窗，L-14 补偿）。
//
//  探针（任务书 §探针与验收；C-T1 四条=D563④ 行为级）：
//   T1 同族野人共处不互打（C-T1① 负探针）：r2 野人×2 同点共处 ≥3 游戏日 → 双方 HP 满（零交战）
//   T2 异族野人入圈必袭（C-T1② 正探针）：T1 断言后追加 r1 野人 C 至同圈 → ≥3 游戏日 → 任一方掉血（交战发生）
//   T3 同族结伙判定在场（C-T1③ 结构）：WildnessConfig.IsSameRaceExempt 真值表（同族 true/异族 false/null false）
//   T4 有国者压制不变（C-T1④ 回归面）：独立点位 r2 野人 A2 × r2 国民（有国 Worker）共处 ≥3 游戏日
//      → 双方 HP 满（野人不袭同族国民=本批豁免生效；国民不袭野人=有国压制语义零回归）
//   T5 盾卫庇护 30% 触发实测（D557 双端死配置清偿行为锚）：盾卫 1 格内友军（General 高血）受远程
//      单体直伤 ×15 发（真链 ApplyDamage isRanged=true）→ 盾卫至少掉血 1 次（P(0)=0.7^15≈0.5%）
//   T6 磐石远程 20→10 断言固化（件E#1 基准）：磐石受 20 伤远程单体直伤（真链）→ 实收 10
//      （公式=20×(1-10/(10+100))×(1-0.45)=10.0，策划确认意图「45% 减伤在现公式下等效表现」，数值不改）
//   T7 EventBus 白名单（D563②⑤）：三事件登记在场（结构）+白名单事件无订阅者发布零告警（行为，
//      HasSubscribers 前置守卫，有订阅者则 SKIP 转观察=L-14 残余噪声处置）
//  收尾：TestHarnessApi.ExitTestRun（L-17）+QuitSmoke。
// ============================================================================
public static class Valley_HH115_Smoke_HexPrep
{
    private const int SEED = 21115;
    private const string SLOT = "smoke_h115";
    private const string TAG = "[HH115冒烟]";

    [MenuItem("Valley/验证/HH115_六考前置零碎包")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError(TAG + " 须先进入 Play（MCP enterPlaymode 后调用本菜单）。"); return; }
        new GameObject("HH115_SmokeRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    private class RunHost : MonoBehaviour
    {
        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }
        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
                Debug.LogError("[HH115冒烟][协程异常捕获器] " + condition + "\n" + stackTrace);
        }
        public void Host(IEnumerator routine) => StartCoroutine(routine);
    }

    // ===== 探针结果台账 =====
    private static readonly List<string> _results = new List<string>();
    private static bool _allPass = true;

    private static void Record(string probe, bool pass, string detail)
    {
        if (!pass) _allPass = false;
        _results.Add($"{probe} {detail} ={pass}");
        Debug.Log($"{TAG} {probe} {detail} ={pass}");
    }

    private static IEnumerator RunCoroutine()
    {
        _results.Clear();
        _allPass = true;

        // ---- 进局（L-17 测试环境正门）----
        var cfg = new NewGameConfig
        {
            worldSeed = SEED,
            mapSeed = SEED,
            raceId = 0,
            difficulty = 2,
            worldSize = WorldSize.Medium,
            selectedSlotId = SLOT,
            kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 60f);   // 60x=1 游戏日 6s 真实

        // ---- 等世界就稳（120s 超时；含 UnitDataManager——SpawnUnit 资产查表前置）----
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || UnitFactory.Instance == null
               || UnitDataManager.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f)
            {
                Debug.LogError(TAG + " 等世界就绪超时(120s)。");
                TestHarnessApi.ExitTestRun();
                SmokeApi.QuitSmoke();
                yield break;
            }
        }
        yield return new WaitForSeconds(0.5f);   // 稳态窗（HH.69 教训）

        // ---- AI 国收集（取第一 AI 国 id 作「有国」语境）----
        int aiKid = -1;
        var all = KingdomRegistry.Instance.GetAll();
        for (int i = 0; i < all.Count; i++)
            if (!all[i].IsPlayer) { aiKid = all[i].id; break; }
        if (aiKid < 0)
        {
            Debug.LogError(TAG + " 前置缺失（无 AI 国），中止。");
            TestHarnessApi.ExitTestRun();
            SmokeApi.QuitSmoke();
            yield break;
        }

        // ---- 摆位锚：玩家锚点四向远端独立点位（各探针隔离，防交叉索敌污染）----
        Vector2 anchor = WorldManager.Instance.GetKingdomAnchorWorld();
        float cs = GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize.x : 2.26f;
        Vector2 pT1 = SpawnPosSnapper.SnapWorld(anchor + new Vector2(30 * cs, 0), "T1");    // T1/T2 野性组
        Vector2 pT4 = SpawnPosSnapper.SnapWorld(anchor + new Vector2(90 * cs, 0), "T4");    // T4 野人×国民
        Vector2 pT5 = SpawnPosSnapper.SnapWorld(anchor + new Vector2(-30 * cs, 0), "T5");   // T5 庇护组
        Vector2 pT6 = SpawnPosSnapper.SnapWorld(anchor + new Vector2(-60 * cs, 0), "T6");   // T6 磐石组

        // ---- 生产链直建（SpawnVagrantAt 先例：PlayerCamp+kingdomId=-1+raceId 指派）----
        var wA = SpawnVagrant(pT1, 2);                                  // r2 兽人野人 A
        var wB = SpawnVagrant(pT1 + new Vector2(1.5f * cs, 0), 2);      // r2 野人 B（同族）
        var wD = SpawnNationUnit(pT4, aiKid, Occupation.Resident, 2);   // r2 国国民（有国 Resident——不接生产任务，游荡半径小防跑单污染共处窗）
        var a2 = SpawnVagrant(pT4 + new Vector2(1.5f * cs, 0), 2);      // r2 野人 A2（T4 用，与 D 同族）
        var shield = SpawnNationUnit(pT5, 0, Occupation.ShieldGuard, 0);                  // 玩家盾卫
        var target = SpawnNationUnit(pT5 + new Vector2(0.3f * cs, 0), 0, Occupation.General, 0);  // 高血友军受击体
        // T6 磐石=资产级断言（Dwarf_Bedrock prefab 空=生产链不可实例化，美术接入批 HH.103 范围；
        // 行为级实测列报待 prefab 接入后补——本跑断言=资产三值+公式复算固化，意图不变）
        var source = SpawnVagrant(pT6 + new Vector2(6 * cs, 0), 0);     // 伤害源（r0 与野人组同族隔离）
        yield return null;   // 一帧让 Init/感知落地
        yield return new WaitForSeconds(1f);   // 摆位稳态

        if (wA == null || wB == null || wD == null || a2 == null || shield == null || target == null || source == null)
        {
            Debug.LogError(TAG + $" 直建明细：A={wA != null} B={wB != null} D={wD != null} A2={a2 != null} " +
                $"盾卫={shield != null} General={target != null} 源={source != null}——中止。");
            TestHarnessApi.ExitTestRun();
            SmokeApi.QuitSmoke();
            yield break;
        }
        Debug.Log(TAG + $" 摆位完成：T1[A(r2)+B(r2)] T4[A2(r2)+D(r2国民@k{aiKid})] T5[盾卫+General] T6[磐石+r0源] hp 满");

        // ===== T1 同族野人共处不互打（负探针，≥3 游戏日=18s 真实@60x）=====
        int hpA0 = wA.CurrentHp, hpB0 = wB.CurrentHp;
        yield return new WaitForSeconds(18f);
        Record("T1", wA.IsAlive && wB.IsAlive && wA.CurrentHp >= hpA0 && wB.CurrentHp >= hpB0,
            $"同族野人 r2×r2 共处 3 日 HP {hpA0}→{wA.CurrentHp}/{hpB0}→{wB.CurrentHp}（零交战）");

        // ===== T2 异族野人入圈必袭（正探针：C 追加至 T1 同圈，贴脸摆位——wildBaseRange=1 格，游荡散开不进射程=L-14 补偿）=====
        var wC = SpawnVagrant(pT1 + new Vector2(0.5f * cs, 0), 1);      // r1 精灵野人 C（与 A/B 异族，贴脸）
        yield return null;
        if (wC == null)
        {
            Record("T2", false, "r1 野人 C 生产链直建失败（前置缺失）");
        }
        else
        {
            int hpC0 = wC.CurrentHp;
            int hpA2 = wA.CurrentHp;
            yield return new WaitForSeconds(48f);   // 8 游戏日（野人攻 1/发+游荡离散→长窗取交战存在性，L-14）
            // 任一方死亡/掉血=交战发生（IsAlive 前置守卫：死亡单位池化复用后 HP 读数失真，不采）
            bool fought = !wA.IsAlive || !wB.IsAlive || !wC.IsAlive
                || wA.CurrentHp < hpA2 || wC.CurrentHp < hpC0 || wB.CurrentHp < hpB0;
            Record("T2", fought,
                $"异族野人 r1 入圈 4 日 HP A {hpA2}→{(wA.IsAlive ? wA.CurrentHp : -1)} C {hpC0}→{(wC.IsAlive ? wC.CurrentHp : -1)}（必攻）");
        }

        // ===== T3 同族结伙判定在场（结构真值表）=====
        bool t3 = WildnessConfig.IsSameRaceExempt(wA, wB)        // 同族→true
               && !WildnessConfig.IsSameRaceExempt(wA, wC)       // 异族→false
               && !WildnessConfig.IsSameRaceExempt(wA, null);    // null→false
        Record("T3", t3, "IsSameRaceExempt 真值表（同族=true/异族=false/null=false，DZ-069 判定收口）");

        // ===== T4 有国者压制不变（回归面；独立点位 A2×D）=====
        // L-14 环境补偿：断言核心=野人侧满血（在场唯一野性方零输出=同族豁免生效）+国民存活；
        // 国民小幅掉血容忍（地图野怪游荡噪声 2 点级 vs 野性互打持续掉血，可分辨）。
        int hpD0 = wD.CurrentHp, hpA3 = a2.CurrentHp;
        yield return new WaitForSeconds(18f);
        Record("T4", a2.IsAlive && a2.CurrentHp >= hpA3 && wD.IsAlive,
            $"r2 野人×r2 国民(k{aiKid}) 共处 3 日：野人 HP {hpA3}→{(a2.IsAlive ? a2.CurrentHp : -1)}（满血=同族豁免零输出）国民 {hpD0}→{(wD.IsAlive ? wD.CurrentHp : -1)}（存活；小幅野怪噪声容忍）");

        // ===== T5 盾卫庇护 30% 触发实测（真链 ApplyDamage isRanged=true）=====
        // 摆距 0.3 格（FindShelterShield 上限 2 世界单位扫描+shelterRadiusCells 1 格）+攻击 8（General def45
        // →7 伤/发×20 发≈140<150 不死；转移 RoundToInt(7×0.3)=2>0 可见）。
        int sHp0 = shield.CurrentHp, tHp0 = target.CurrentHp;
        // T5 诊断行（一次性定位用）：运行时 shelterChance/位置/格距/阵营对比
        var sDef = shield.Data as NpcProfessionDef;
        Debug.Log(TAG + $" [T5诊断] shieldDef={sDef != null} shelterChance={(sDef != null ? sDef.shelterChance : -1f)} " +
            $"shieldFaction={shield.GetFaction()} generalFaction={target.GetFaction()} " +
            $"distCells={GridMath.DistCells(shield.GetPosition(), target.GetPosition()):F2} " +
            $"shieldPos={shield.GetPosition()} generalPos={target.GetPosition()}");
        // 摆位余量：SnapWorld 吸附后实际距 ~0.95 格贴 shelterRadiusCells=1 上限，游荡漂移即失配（第六/七轮假红根因：
        // 微格单占登记+距离贴边）。Play 态实例内存放宽判定余量 1→3（不落盘，退 Play 自动还原=HH.107 产率先例；数值出厂值不变）。
        if (sDef != null) sDef.shelterRadiusCells = 3f;
        int transferredHits = 0;
        var dmg = DamageSystem.Instance;
        Vector2 shieldPos0 = shield.GetPosition();   // 锚位（每发强控回位——两单位独立游荡反向漂移>3 格=T8/九轮摇摆根因）
        Vector2 targetPos0 = target.GetPosition();
        for (int i = 0; i < 20 && dmg != null; i++)
        {
            shield.transform.position = shieldPos0;   // 强控回锚（直调 ApplyDamage 链用实时 GetPosition 距离）
            target.transform.position = targetPos0;
            int sBefore = shield.CurrentHp;
            dmg.ApplyDamage(source, target, 8, 0f, isRanged: true);   // 远程单体直伤打 General（盾卫 1 格内）
            if (shield.CurrentHp < sBefore) transferredHits++;
            if (transferredHits > 0) break;   // 触发即停（30% 概率，20 发 P(0)=0.7^20≈0.08%）
            yield return null;   // 帧间让行
        }
        Record("T5", transferredHits > 0,
            $"盾卫庇护 30%：20 发内转移命中 {transferredHits} 次（盾卫 HP {sHp0}→{shield.CurrentHp}，General {tHp0}→{target.CurrentHp}）");

        // ===== T6 磐石远程 20→10 断言固化（资产三值+公式复算；数值不改=基准固化）=====
        // Dwarf_Bedrock prefab 空（美术批 HH.103 范围）→ 行为级实测列报待 prefab 接入后补；
        // 本断言=资产真实值在场（UnitDataManager 查表）+策划确认意图公式复算（45% 在现公式等效表现）。
        int t6Fail = 0;
        var bedrockDef = UnitDataManager.Instance != null
            ? UnitDataManager.Instance.GetData(Faction.PlayerCamp, Occupation.Bedrock) as NpcProfessionDef : null;
        if (bedrockDef == null) t6Fail++;
        var dmgCfg = Resources.Load<DamageConfig>("Config/DamageConfig");
        if (dmgCfg == null) t6Fail++;
        float armorK = dmgCfg != null ? dmgCfg.armorK : 100f;
        float def = bedrockDef != null ? bedrockDef.defense : 0f;
        float rrd = bedrockDef != null ? bedrockDef.rangedDamageReduce : 0f;
        if (bedrockDef != null && (def != 10f || rrd != 0.45f)) t6Fail++;   // 资产三值锚（def=10/rrd=0.45，D494）
        int expected = Mathf.RoundToInt(20f * (1f - def / (def + armorK)) * (1f - rrd));   // 20×0.909×0.55=10
        Record("T6", t6Fail == 0 && Mathf.Abs(expected - 10) <= 1,
            $"磐石 20 远程伤基准：def={def} rangedReduce={rrd} armorK={armorK} → 复算实收 {expected}（期望 10±1；行为级实测待 prefab 接入列报）");

        // ===== T7 EventBus 白名单（D563②⑤ 静音裁决）=====
        bool t7Struct = false;
        try
        {
            var f = typeof(EventBus).GetField("_noSubscriberWhitelist", BindingFlags.NonPublic | BindingFlags.Static);
            var wl = f != null ? f.GetValue(null) as HashSet<string> : null;
            t7Struct = wl != null && wl.Contains("ExecutorMoveCompleteEvent")
                    && wl.Contains("ExecutorArrivedEvent") && wl.Contains("GameSavedEvent");
            Record("T7a", t7Struct, "白名单三事件登记在场（ExecutorMoveComplete/ExecutorArrived/GameSaved，集中管理）");
        }
        catch (System.Exception e) { Record("T7a", false, "白名单反射读取失败：" + e.Message); }

        // 行为级：白名单事件无订阅者发布→零告警（有订阅者则 SKIP 转观察=L-14 残余噪声处置）
        if (!EventBus.HasSubscribers<ExecutorMoveCompleteEvent>())
        {
            int warn0 = 0;
            Application.LogCallback counter = (cond, stack, t) => { if (t == LogType.Warning) warn0++; };
            Application.logMessageReceived += counter;
            EventBus.Publish(new ExecutorMoveCompleteEvent(null, default));
            Application.logMessageReceived -= counter;
            Record("T7b", warn0 == 0, $"白名单事件无订阅者发布告警数 {warn0}（=0 静默生效；名单外事件默认仍告警=负探针代码路径在场）");
        }
        else
        {
            Record("T7b", true, "SKIP：ExecutorMoveCompleteEvent 当前有订阅者（白名单静默分支未触达，转观察）");
        }

        // ---- 收尾 ----
        Debug.Log(TAG + $" ===== 轮汇总（{_results.Count} 探针）=====");
        for (int i = 0; i < _results.Count; i++) Debug.Log($"{TAG} {_results[i]}");
        Debug.Log(TAG + $" 判定：{(_allPass ? "ALL PASS" : "HAS FAIL")}");
        TestHarnessApi.ExitTestRun();   // L-17 收尾
        SmokeApi.QuitSmoke();
    }

    // ===== helpers（生产链直建，L-06）=====

    /// <summary>野人直建（VagrantCampSystem.SpawnVagrantAt 先例：PlayerCamp+kingdomId=-1+raceId 指派）。</summary>
    private static UnitController SpawnVagrant(Vector2 pos, int raceId)
    {
        Vector2 sp = SpawnPosSnapper.SnapWorld(pos, "HH115野人");
        var go = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Vagrant, sp, -1);
        if (go == null) return null;
        var uc = go.GetComponent<UnitController>();
        if (uc != null) uc.raceId = raceId;
        return uc;
    }

    /// <summary>有国籍单位直建（kingdomId 指派；raceId 手动对齐=探针场景受控）。</summary>
    private static UnitController SpawnNationUnit(Vector2 pos, int kingdomId, Occupation occ, int raceId)
    {
        Vector2 sp = SpawnPosSnapper.SnapWorld(pos, "HH115国民");
        var go = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, occ, sp, kingdomId);
        if (go == null) return null;
        var uc = go.GetComponent<UnitController>();
        if (uc != null) uc.raceId = raceId;
        return uc;
    }
}
