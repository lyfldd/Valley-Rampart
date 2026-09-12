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
    // ✅ DZ-136 已复原（HH.224 裁决回执 / D670 加硬条件）：CIRCUIT_BREAK_DAY **90→120**、MILITARY_STOP_COUNT **1→2**
    //    ⇒ **长局档已就绪**（未复原不得开长局：120 日判定线会被 90 截断＝D585/D589 截断混淆同族）。
    //    ⚠️仍属"每批定案"项（非档位）：SEED/SLOT 保持 64513/p1_fix1b，下一批起跑前须按任务书设新 seed/槽
    //    （**换 seed 须同步改 `JUDGE_FOCUS_KINGDOM`**，见下方注释）。
    private const int SEED = 64513;          // HH.217 短局：复用 HH.214 定案 seed（同世界修前/修后对照；D664 裁准）
    private const string SLOT = "p1_fix2";    // HH.224 对照跑独立槽（禁覆盖 p1_run6/6b/7/8、p1_fix1/1b）
    private const int CIRCUIT_BREAK_DAY = 120;
    private const int MILITARY_STOP_COUNT = 2;   // ✅已复原（DZ-136）：长局判定档=≥2 AI 军事期（HH.217 短局自证档 1 已废止）

    // HH.224（D670/D675 裁）：**对照/诊断跑专用窗口**——独立常量，**不回改上表主档位**（防 DZ-136 复发）。
    //   本批（修前/修后同 seed 对照，对照段 D1~D60）= USE_DIAG_WINDOW=true；
    //   ⚠️**长局判定跑（七考重验）起跑前须置 false**（否则 120 日判定线被 60 截断＝D585/D589 截断混淆同族）。
    private const int DIAG_CIRCUIT_DAY = 60;
    private const bool USE_DIAG_WINDOW = true;
    private static int ActiveCircuitDay => USE_DIAG_WINDOW ? DIAG_CIRCUIT_DAY : CIRCUIT_BREAK_DAY;

    // HH.224 同 seed 修前基线（源＝`p1_fix1` seed64513 D1~D60，**同日志源口径**；HH.225 §四 实测）：
    //   census top=BuildWall 日数 k1 37／k2 47／k3 9／k4 47（census 段 59 日）；建造焦点落地总数 32。
    private static readonly int[] BASE_WALL_TOP = { 0, 37, 47, 9, 47 };   // 索引=kingdomId
    private const int BASE_BUILD_OK = 32;

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

    // HH.224 批（D670/D675）：**本批判据启用集＝J6/J7/J8（见 CheckBackoffJudges）＋ J1/J3 兜底**。
    //   ⚠️ **J2（石链僵死·指定国 k3）本批关闭**（开关见下）：
    //   ① 规则依据：J2 的服务句＝HH.220「石链诊断」（该批已销号），与本批验收句（退避生效／wall 占比下降／
    //      落地不降）**不同级、不服务** ⇒ 依 D669「判据须与其服务验收句同级＋同作用域」本不该在本批启用集；
    //   ② 实测依据（HH.224 对照跑 p1_fix2 @D12 被 J2 止损）：同 seed **修前** `p1_fix1` k3 D2~D14 `stone` 恒 25、
    //      `Stone prod=0/in=0`（D30 才开链）⇒ J2 的「可判定最早日 D10」**未排除"开局无产能期"常态**，
    //      修前同样会 ~D11~D12 停 ⇒ **非修后回归**，属 J2 口径缺陷（已报裁）。
    //   ⇒ 后续批若需石链止损：置 true 启用，并**先解决"开局无产能期"口径**（否则以既有常态误停）。
    //   （用 static readonly 而非 const：const=false 会使 `if` 分支变"不可达代码"触发 CS0162 警告）
    private static readonly bool ENABLE_J2_STONECOLD = false;

    private static readonly JudgeCfg[] JUDGES = BuildJudges();

    private static JudgeCfg[] BuildJudges()
    {
        var list = new System.Collections.Generic.List<JudgeCfg>();
        // J1 死滞（全批级）：负探针「死滞复现」；正向世界 drive>0 ⇒ 不触发（兜底）
        list.Add(new JudgeCfg { Kind = DiagMilitary.JudgeKind.Deadlock, OnlyKingdom = false, Kingdom = 0 });
        // J2 石链僵死（指定国 k3）：**本批关闭**（见上注）
        if (ENABLE_J2_STONECOLD)
            list.Add(new JudgeCfg { Kind = DiagMilitary.JudgeKind.StoneCold, OnlyKingdom = true, Kingdom = JUDGE_FOCUS_KINGDOM });
        // J3 异常即停（全批级）：六资源全零入库 ≥15 日
        list.Add(new JudgeCfg { Kind = DiagMilitary.JudgeKind.NoIncome, OnlyKingdom = false, Kingdom = 0 });
        return list.ToArray();
    }

    private static readonly List<int> _military = new List<int>();
    private static volatile bool _done;

    // HH.224/D675 在线判据 J6/J7/J8 状态（口径＝**日志源**，与修前基线同源 ⇒ L-35 口径一致）
    private static readonly int[] _wallTopDays = new int[5];   // census top=BuildWall 日数（索引=kingdomId）
    private static readonly int[] _censusSamples = new int[5];  // census 采样数（分母；索引=kingdomId）
    private static int _buildOkDays;                            // 建造焦点落地总数
    private static bool _avoidSeen;                             // J6：退避因子读数首次出现（机制面已证）

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
        System.Array.Clear(_wallTopDays, 0, _wallTopDays.Length);      // HH.224：J7/J8 证据清零（防跨批污染）
        System.Array.Clear(_censusSamples, 0, _censusSamples.Length);
        _buildOkDays = 0; _avoidSeen = false;
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
        if (condition == null) return;

        // ── HH.224/D675：J7/J8 证据累计（口径＝日志源，与修前基线同源）──
        if (condition.Contains("建造焦点落地：")) _buildOkDays++;
        if (condition.Contains("[DiagMilitary]") && condition.Contains(" top="))
        {
            int ck = ParseKingdomId(condition);
            if (ck > 0 && ck < _censusSamples.Length)
            {
                _censusSamples[ck]++;
                if (condition.Contains("top=BuildWall")) _wallTopDays[ck]++;
            }
        }
        if (!_avoidSeen && condition.Contains("avoid=") && condition.Contains("[DiagMilitary]"))
        {
            int i = condition.IndexOf("avoid=", System.StringComparison.Ordinal);
            string rest = i >= 0 ? condition.Substring(i + 6) : "";
            int sp = rest.IndexOf(' ');
            if (sp > 0) rest = rest.Substring(0, sp);
            if (!string.IsNullOrEmpty(rest.Trim()))
            {
                _avoidSeen = true;
                Debug.LogWarning("[HH80跑] ⚑ J6 退避机制面已证（首次 avoid 非空：" + rest.Trim() + "）");
            }
        }

        if (!condition.Contains("剧本阶段 → 军事")) return;
        int kid = ParseKingdomId(condition);
        if (kid > 0 && !_military.Contains(kid))
        {
            _military.Add(kid);
            Debug.LogWarning("[HH80跑] ⚑ 军事期达标 k" + kid + "（累计 " + _military.Count + "）");
        }
    }

    /// <summary>从日志行取 ` k<id>` 中的 id（无 ⇒ -1）。</summary>
    private static int ParseKingdomId(string s)
    {
        int idx = s.IndexOf(" k", System.StringComparison.Ordinal);
        if (idx < 0) return -1;
        int start = idx + 2; int end = start;
        while (end < s.Length && char.IsDigit(s[end])) end++;
        int kid;
        return int.TryParse(s.Substring(start, end - start), out kid) ? kid : -1;
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
            if (CheckBackoffJudges(day, out judgeWhy)) { Finish(judgeWhy); break; } // HH.224：J7 霸占解除／J8 防退化（§八判据表）
            if (day >= ActiveCircuitDay) { Finish("窗口满：D" + ActiveCircuitDay + "（DIAG窗口=" + USE_DIAG_WINDOW + "）AI 存活 " + aiAlive + "/" + aiTotal + "，已达标=" + string.Join(",", _military) + "；J6 退避机制面=" + _avoidSeen + " wallTop(k1/k2/k4)=" + _wallTopDays[1] + "/" + _wallTopDays[2] + "/" + _wallTopDays[4] + " 落地=" + _buildOkDays + " ⇒ 取对照证据"); break; }
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

    /// <summary>HH.224/D675：J7（霸占解除·端到端）/ J8（防退化·负向）在线判据。
    /// 口径＝**日志源**（与修前基线 `p1_fix1` 同源）；J7 作用域＝**指定国 k1/k2/k4**；样本 &lt;30 不判（防早停误裁）。</summary>
    private static bool CheckBackoffJudges(int day, out string why)
    {
        why = null;
        for (int k = 1; k <= 4; k++)
        {
            if (k == 3) continue;                       // J7 排除 k3（其 wall 占比本就低 9/59）
            if (BASE_WALL_TOP[k] <= 0) continue;
            int n = _censusSamples[k];
            if (n < 30) continue;
            float now = _wallTopDays[k] / (float)n;
            float half = 0.5f * (BASE_WALL_TOP[k] / 60f);
            if (now <= half)
            {
                why = "判据命中：J7 霸占解除（k" + k + " wallTop " + _wallTopDays[k] + "/" + n + "=" + now.ToString("F2")
                    + " ≤ 基线半值 " + half.ToString("F2") + "）@D" + day;
                return true;
            }
        }
        int nn = _censusSamples[1];
        if (nn >= 30)
        {
            float exp = BASE_BUILD_OK * (nn / 60f);
            if (_buildOkDays <= 0.5f * exp)
            {
                why = "判据命中：J8 防退化报警（建造落地 " + _buildOkDays + " ≤ 基线同段半值 " + (0.5f * exp).ToString("F1")
                    + "，样本 " + nn + "）⇒ 停手报裁 @D" + day;
                return true;
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
