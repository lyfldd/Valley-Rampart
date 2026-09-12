using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.80 P1 考跑正式容器（Editor-only；HH.71 协议；D585 正门版=test-harness-first 铁律④补接）
//  用法：GameScene Play → 先启动 P1 观测器（Valley/观测/P1_启动观测）→ 本菜单。
//  职责：EnterTestRun 正门进局（D585 全守卫=判负封死+T11 野怪静默+考跑档直通 15x；
//  玩家真实局态挂机=T10 幽灵化 OFF）→独立监控循环
//  （自订阅 logMessageReceived 数「剧本阶段 → 军事」——不掏 P1Observer 私有字段，
//  接口纪律）→ 达标（≥2 AI 军事期）或熔断（day≥120）或灭绝全灭 → SetGameSpeed(0)
//  停速+Save 封盘+ExitTestRun 全量恢复+写状态文件 Logs/P1/hh80_run_status.log（console 大缓冲教训）。
//  每 20 日评审打点（协议）→ 镜像日志+CSV 承担，本容器只做终局三停（达标/熔断/灭绝）。
//  HH.222（D666 裁 / test-harness-first §八 机制 1+3；L-34）：**在线判据·命中即停**已接入——
//  每轮读 DiagMilitary 判据快照（死滞／石链僵死止损／零入库异常），命中即 Finish("判据命中：X @D??")（见 JUDGES 常量）。
//  红线：零玩家干预（本容器只开考跑档+Save，不建造不训练不输资源）。
// ============================================================================
public static class Valley_HH80_Run
{
    // P1 七考重验（HH.203/D657；D661 放行）：六考原值 SEED=69496 / SLOT="p1_run6b"、七考原值 SEED=73621 / SLOT="p1_run7"
    // 均已 git 留证（`git log -p -- Assets/Editor/Smoke/Valley_HH80_Run.cs` 可复原；p1_run6/p1_run6b/p1_run7/p1_run8 原封勿覆盖）。
    // HH.217 治本批（D663 裁 A+ / D664 放行）：**短局机制自证**——同 seed（64513，HH.214 定案）对照、槽 p1_fix1、
    // 90 日窗口（D664 裁：k3 每兵 ~7~10 日 ⇒ warrior=4 ≈ D67~70，60 日不足）、**见 1 军事实达即提前收工**（MILITARY_STOP_COUNT=1）。
    // ⚠️后续七考重验批须复原：SEED=新定案值 / SLOT=新槽 / CIRCUIT_BREAK_DAY=120 / MILITARY_STOP_COUNT=2。
    private const int SEED = 64513;          // HH.217 短局：复用 HH.214 定案 seed（同世界修前/修后对照；D664 裁准）
    private const string SLOT = "p1_fix1b";   // 治本批独立槽（禁覆盖 p1_run6/6b/7/8；p1_fix1=正向 / p1_fix1b=负探针 weight=0）
    private const int CIRCUIT_BREAK_DAY = 90;
    private const int MILITARY_STOP_COUNT = 1;   // HH.217：短局机制自证 ⇒ 1 个 AI 达军事期即收工（七考须复原为 2）

    // HH.222（D666 已裁 / test-harness-first §八 机制 1+3；教训 L-34）＝**在线判据·命中即停**：
    //   支撑/目标日志每日在流（探针 `verdict=`）⇒ 容器每轮检查，命中即 Finish("判据命中：X @D??")，不跑满窗口。
    //   ⚠️机制 4（≥30 日每 10 日阶段小结）本批不做，随七考重验批强制（D666）。
    //
    // 🔴 HH.222 追加裁（D666 §①②·策划端）＝「判据须与其服务验收句**同级 ＋ 同作用域**」：
    //   · 同级＝机制级验收句才配 J5（TargetGateHit）；端到端级（如「→军事」）不配 ⇒ 故本批 J5 关。
    //   · 同作用域＝判据判定范围须与验收句范围一致（全批级 / 指定国级）⇒ 由下表 `OnlyKingdom` 表达。
    //   ⚠️正向批陷阱（策划端抓到）：k1/k2/k4 石链本就死（HH.220 实证）⇒ 若 J2 为「全批任一」会在 ~D10 停掉全批、
    //     切掉 k3 的「→军事」证据（D664 验收句②）⇒ 故 **J2 固定为「指定国」= JUDGE_FOCUS_KINGDOM（本 seed 唯一可至军事期者）**。
    private struct JudgeCfg { public DiagMilitary.JudgeKind Kind; public bool OnlyKingdom; public int Kingdom; }

    /// <summary>本 seed（64513）唯一可至军事期的 AI＝k3（HH.220 实证：D65 →军事）；**换 seed 须一并改**。</summary>
    private const int JUDGE_FOCUS_KINGDOM = 3;

    private static readonly JudgeCfg[] JUDGES = new[]
    {
        // 判据 ／ 作用域（OnlyKingdom=false ⇒ 全批任一 AI；true ⇒ 仅该王国） ／ 服务的验收句
        new JudgeCfg { Kind = DiagMilitary.JudgeKind.Deadlock,  OnlyKingdom = false, Kingdom = 0 },                    // 负探针「死滞复现」＝全批级
        new JudgeCfg { Kind = DiagMilitary.JudgeKind.StoneCold, OnlyKingdom = true,  Kingdom = JUDGE_FOCUS_KINGDOM },   // 止损；指定国级（避免切掉他国的端到端证据）
        new JudgeCfg { Kind = DiagMilitary.JudgeKind.NoIncome,  OnlyKingdom = false, Kingdom = 0 },                    // 异常即停＝全批级
    };

    private static readonly List<int> _military = new List<int>();
    private static volatile bool _done;

    [MenuItem("Valley/验证/HH80_正式跑")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH80跑] 须先 GameScene 进 Play（且先启动 P1 观测器）。"); return; }
        // D647 异议2 fail-fast：观测器未启则中止（防 ≈48 分钟空跑无证据/无检查点）
        if (!P1Observer.IsRunning)
        {
            Debug.LogError("[HH80跑] ✗ 观测器未启动——请先点「Valley/观测/P1_启动观测」再起跑（D647 异议2 fail-fast）。已中止。");
            return;
        }
        // D656 硬条款2：显式传槽（观测器 MainSlot 已参数化，禁回落硬编码）——进局前设置
        P1Observer.SetMainSlot(SLOT);
        _military.Clear(); _done = false;
        DiagMilitary.ResetJudges();   // HH.222：清在线判据 streak（防跨局/跨批污染；探针未启时全部判据自然 NoData）
        Application.logMessageReceived += WatchMilitary;
        new GameObject("HH80_RunRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    private class RunHost : MonoBehaviour
    {
        public void Host(IEnumerator routine) => StartCoroutine(routine);
    }

    private static void WatchMilitary(string condition, string stackTrace, LogType type)
    {
        if (condition == null || !condition.Contains("剧本阶段 → 军事")) return;
        int idx = condition.IndexOf(" k", System.StringComparison.Ordinal);
        if (idx < 0) return;
        int start = idx + 2; int end = start;
        while (end < condition.Length && char.IsDigit(condition[end])) end++;
        int kid;
        if (int.TryParse(condition.Substring(start, end - start), out kid) && kid > 0 && !_military.Contains(kid))
        {
            _military.Add(kid);
            Debug.LogWarning("[HH80跑] ⚑ 军事期达标 k" + kid + "（累计 " + _military.Count + "）");
        }
    }

    private static IEnumerator RunCoroutine()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = SLOT, kingdomName = "河谷王国"
        };
        // HH.150 清残留：删除迁正门时遗留的裸局 SmokeApi.EnterGame+服务性等就绪块
        // （双建局冗余；EnterTestRun 内置等就绪，直接正门进局）
        // 正门进局（test-harness-first 铁律①/D585：EnterTestRun 全守卫=判负封死+T11 野怪静默+考跑档直通
        // [speedOverride=null→读 WorldConfig.time.testSpeedMultiplier SO 缺省 15]；玩家真实局态=T10 OFF）
        yield return TestHarnessApi.EnterTestRun(cfg);
        Debug.LogWarning("[HH80跑] 正门进局 seed=" + SEED + " 槽=" + SLOT + " 考跑守卫全开（D585 判负封死+T11 野怪静默，玩家真实局态挂机；P1 观测器须已在跑：镜像+CSV+检查点）");

        // 终局三停监控：达标（≥2 AI 军事期）/熔断（D120）/灭绝（AI 全灭）
        while (!_done)
        {
            yield return new WaitForSeconds(5f);
            int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
            var reg = KingdomRegistry.Instance;
            int aiAlive = 0, aiTotal = 0;
            if (reg != null)
            {
                var all = reg.GetAll();
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].IsPlayer) continue;
                    aiTotal++;
                    if (all[i].workerCount + all[i].warriorCount > 0) aiAlive++;
                }
            }
            if (_military.Count >= MILITARY_STOP_COUNT) { Finish("达标收工：≥" + MILITARY_STOP_COUNT + " AI 军事期（" + string.Join(",", _military) + "）@D" + day); break; }
            string judgeWhy;
            if (CheckJudges(reg, day, out judgeWhy)) { Finish(judgeWhy); break; }   // HH.222：在线判据命中即停（§8.2 机制 1+3）
            if (day >= CIRCUIT_BREAK_DAY) { Finish("熔断：D" + CIRCUIT_BREAK_DAY + " 无 ≥2 AI 军事期（已达标=" + string.Join(",", _military) + "，AI 存活 " + aiAlive + "/" + aiTotal + "）"); break; }
            if (reg != null && aiTotal > 0 && aiAlive == 0) { Finish("灭绝停跑：AI 全灭 @D" + day + "（已达标=" + string.Join(",", _military) + "）"); break; }
        }
    }

    /// <summary>HH.222（D666 裁 / §8.2 机制 1+3）：在线判据检查——命中即返回停跑原因。
    /// 判据源＝DiagMilitary 每日 streak 快照（单源，禁另抄）；已灭绝国不判（灭绝有独立停条）。
    /// 作用域（D666 追加裁①②）：**外层遍历判据、内层按 `OnlyKingdom` 限定王国范围**——
    /// 全批级判据（OnlyKingdom=false）任一 AI 命中即停；指定国级（=true）仅该王国命中才停（防切掉他国端到端证据）。</summary>
    private static bool CheckJudges(KingdomRegistry reg, int day, out string why)
    {
        why = null;
        if (reg == null) return false;
        var all = reg.GetAll();
        for (int j = 0; j < JUDGES.Length; j++)
        {
            var cfg = JUDGES[j];
            for (int i = 0; i < all.Count; i++)
            {
                var k = all[i];
                if (k == null || k.IsPlayer) continue;
                if (cfg.OnlyKingdom && k.id != cfg.Kingdom) continue;   // 作用域过滤：指定国级
                if (k.workerCount + k.warriorCount <= 0) continue;
                string jd;
                if (DiagMilitary.TryJudgeOne(k.id, cfg.Kind, day, out jd))
                {
                    why = "判据命中：" + cfg.Kind + "（k" + k.id + " " + jd + "）@D" + day;
                    return true;
                }
            }
        }
        return false;
    }

    private static void Finish(string why)
    {
        _done = true;
        Application.logMessageReceived -= WatchMilitary;
        // L-32 条文3（D657 入库·HH.203 §二.7）：禁以 `SetGameSpeed(0f)` 当暂停（SnapToSpeed 吸附 0.5x 非暂停）⇒ 真暂停用 timeScale
        Time.timeScale = 0f;
        bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(SLOT);
        TestHarnessApi.ExitTestRun();   // 正门收尾：考跑态/maximumDeltaTime/渲染全量恢复（D585；封盘在后不丢档）
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== HH.80 三考收工 =====");
        sb.AppendLine(why);
        sb.AppendLine("终速=0 存盘 " + SLOT + "=" + saved + " 时间=" + System.DateTime.Now.ToString("HH:mm:ss"));
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "hh80_run_status.log"), sb.ToString());
        }
        catch (System.Exception e) { Debug.LogError("[HH80跑] 状态写文件失败: " + e.Message); }
        Debug.LogWarning("[HH80跑] ★ " + why + "——真暂停(TS=0)+封盘 " + SLOT + "=" + saved + "（现场保留：观测器镜像/CSV 在案；收工退 Play，L-32）");
        // L-32 条文1（D657 入库）：收工禁留「已恢复 1x」余留世界 ⇒ 容器末尾直接退 Play（同 SmokeApi.QuitSmoke 先例）
        EditorApplication.ExitPlaymode();
    }
}
