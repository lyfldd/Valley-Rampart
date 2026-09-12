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
    private const int SEED = 73621;          // HH.230（D680 裁）：② 七考重验正跑档＝73621（七考原 seed 复用；HH.189 七考/`p1_run7` 同 seed）
    private const string SLOT = "p1_run9";    // HH.230／D683 裁 D：② 定案槽＝p1_run9（**不动 p1_run8／HH.214 存档槽**）
    private const int CIRCUIT_BREAK_DAY = 120;
    private const int MILITARY_STOP_COUNT = 2;   // ✅已复原（DZ-136）：长局判定档=≥2 AI 军事期（HH.217 短局自证档 1 已废止）

    // HH.224（D670/D675 裁）：**对照/诊断跑专用窗口**——独立常量，**不回改上表主档位**（防 DZ-136 复发）。
    //   本批（修前/修后同 seed 对照，对照段 D1~D60）= USE_DIAG_WINDOW=true；
    //   ⚠️**长局判定跑（七考重验）起跑前须置 false**（否则 120 日判定线被 60 截断＝D585/D589 截断混淆同族）。
    private const int DIAG_CIRCUIT_DAY = 60;
    private const bool USE_DIAG_WINDOW = false;   // ✅HH.228 收工已复原 false（短窗取证用毕；回主档 120/2 ⇒ ②七考重验前置②复位）
    private static int ActiveCircuitDay => USE_DIAG_WINDOW ? DIAG_CIRCUIT_DAY : CIRCUIT_BREAK_DAY;

    // HH.221（D685 裁⑤）：**本批短窗档**＝独立常量，**不回改上表主档位**（防 DZ-136 复发）。
    //   复用 seed 64513 ＋新槽 p1_gather1；窗口 **D5~D15**（A 正向实证：k1 型 Stone/Wood in>0）。
    //   ⚠️**长局判定跑（七考重验）起跑前须置 false**（否则 120 日档被 15 截断＝D585/D589 截断混淆同族）。
    //   另注：本批容器**不复用** HH.228 的 DIAG_CIRCUIT_DAY（其 60 日档服务 HH.228 验收句，非同段）。
    private const int GATHER_SEED = 64513;
    private const string GATHER_SLOT = "p1_gather1";
    private const int GATHER_CIRCUIT_DAY = 15;
    private static readonly bool USE_GATHER_WINDOW = false;   // ✅HH.221 短窗跑已毕复原 false（回主档 120/2）
                                                            //   （用 static readonly 而非 const：避免 `if` 分支不可达触发 CS0162）
    private static int ActiveBreakDay => USE_GATHER_WINDOW ? GATHER_CIRCUIT_DAY : ActiveCircuitDay;
    private static int ActiveSeed => USE_GATHER_WINDOW ? GATHER_SEED : SEED;
    private static string ActiveSlot => USE_GATHER_WINDOW ? GATHER_SLOT : SLOT;

    // HH.226 追加项②（D678 裁）：J7 **同段基线**——修复假阳性根因（原＝全窗 37/59 vs 判据早窗＝**不同段比较**）。
    //   常量＝修前 `p1_fix1`（seed64513·65 日）**逐日** `census top=BuildWall` 日号序列（同日志源口径）。
    //   J7 判定＝`修后同段计数/样本 ≤ 0.5 × 修前同段计数/样本`（双方窗口均取 D2~当前日）。
    //   ⚠️**仅当基线与本批 seed ＋窗口档同段时可启用**：HH.203（120 日档·另一 seed）**无同段参照** ⇒ 该批须重取基线
    //     或保持 J7 关（否则重演"以既有常态误停"）。
    private static readonly int[][] BASE_WALL_TOP_DAYS =
    {
        new int[0],                                              // 0 占位（无王国 id 0）
        new int[] { 24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,58,59,60,61,62,63,64,65 },   // k1 共 42 日
        new int[] { 14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,58,59,60,61,62,63,64,65 },   // k2 共 52 日
        new int[] { 24,25,28,29,30,56,57,58,59 },            // k3 共 9 日
        new int[] { 14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,58,59,60,61,62,63,64,65 },   // k4 共 52 日
    };
    private const bool ENABLE_J7_WALLTOP = false;  // HH.230（D680 裁②）：**关 J7**——120 档＋换 seed（73621）⇒ 无同段参照（`BASE_WALL_TOP_DAYS` 仅覆盖至 D65 且源 seed 64513）⇒ 开启＝跨段/跨世界比较＝误停风险

    // J8 防退化同段基线（源＝修前 `p1_fix1` 同日志源）：累计"建造焦点落地"数（D2~D31＝22 ／ D1~D60＝32）
    private const int BASE_BUILD_OK_D31 = 22;
    private const int BASE_BUILD_OK_D60 = 32;

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

    /// <summary>② 正跑档（seed 73621）的**侦察提议值**＝k1（HH.230：seed 73621 结构与 64513 同类；**本 seed 专属证据**＝HH.189 七考 @73621 中 **k1 为唯一跑满 D120 存活国**（工12／战0），k2/k3/k4 全灭 ⇒ 判 k1 为最可能首达军事期者）。
    /// ⚠️**待策划端确认**；且**当前为 inert**——J2（`ENABLE_J2_STONECOLD`）已关（其服务句=HH.220 石链诊断已销号＋"开局无产能期"口径缺陷未解）⇒ 本值不参与启用判据集。</summary>
    private const int JUDGE_FOCUS_KINGDOM = 1;

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

    // HH.230／D683（②七考重验裁示 B）：**关 J8（防退化）**——J8 基线取自 seed **64513** 修前 `p1_fix1`（`BASE_BUILD_OK_D31=22`／`_D60=32`），
    //   本批 seed＝**73621** ⇒ **跨种子比较**（与 D678 修 J7 **同一病根**）；D680「保留 J8」的前提（同 seed／同段）**已破**
    //   ⇒ 本批必须关（维持则可能以"他世界既有常态"误停整批 ≈45 分钟）。**否**重取基线（D683 裁）。
    //   （用 static readonly 而非 const：避免 `if` 分支不可达触发 CS0162）
    private static readonly bool ENABLE_J8_BUILDOK = false;

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
        // HH.221（D685 裁⑤）：**本批短窗档专属判据**（仅 USE_GATHER_WINDOW 时启用）——
        //   依据「判据须与其服务验收句同级＋同作用域」：此二条服务 A 正向实证（§0b 判据1/判据3），
        //   **不得**进主档（长局）：修前 `CountGatherSources()` 恒 0 ⇒ 无条件启用会在 D3 当场停掉任何长局（截断混淆同族）。
        if (USE_GATHER_WINDOW)
        {
            // §0b 判据1：世界资源点源注册数 ≥1（D1~D3 未注册 ⇒ "通道未落地"并停）
            list.Add(new JudgeCfg { Kind = DiagMilitary.JudgeKind.ChannelAbsent, OnlyKingdom = false, Kingdom = 0 });
            // §0b 判据2：k1 型 `Stone/Wood in > 0`（D5~D15；命中 ⇒ 资源对等达成·机制面可停）
            list.Add(new JudgeCfg { Kind = DiagMilitary.JudgeKind.WorldGatherIncome, OnlyKingdom = false, Kingdom = 0 });
            // §0b 判据3：采集通道全 0 且 in 连续 ≥10 日=0 ⇒ "通道僵死"并停（止损）
            list.Add(new JudgeCfg { Kind = DiagMilitary.JudgeKind.GatherStall, OnlyKingdom = false, Kingdom = 0 });
        }
        return list.ToArray();
    }

    private static readonly List<int> _military = new List<int>();
    private static volatile bool _done;

    // HH.224/D675 在线判据 J6/J7/J8 状态（口径＝**日志源**，与修前基线同源 ⇒ L-35 口径一致）
    private static readonly int[] _wallTopDays = new int[5];   // census top=BuildWall 日数（索引=kingdomId）
    private static readonly int[] _censusSamples = new int[5];  // census 采样数（分母；索引=kingdomId）
    private static int _buildOkDays;                            // 建造焦点落地总数
    private static bool _avoidSeen;                             // J6：退避因子读数首次出现（机制面已证）
    private static int _lastStageDay = int.MinValue;            // HH.230（D666 机制4）：阶段小结去重（每 10 日一次）

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
        // HH.228 补遗B／D682 fail-fast：**DiagMilitary 探针未启则中止**——防"静默空跑到窗口满、avoid=/census 判据证据全空"
        //   （首跑事故：漏启探针 ⇒ J6=False、wallTop=0/0/0、avoid= 0 行 ⇒ 证据作废）。口径同 `HH80_诊断跑`（`:39-43`）。
        if (!DiagMilitary.IsRunning)
        {
            Debug.LogError("[HH80跑] ✗ DiagMilitary 探针未启用，判据证据为空（avoid=/census/verdict 全空）——请先点「Valley/诊断/启动建军链诊断」再起跑（HH.228 补遗B fail-fast）。已中止。");
            return;
        }
        // D656 硬条款2：显式传槽（观测器 MainSlot 已参数化，禁回落硬编码）——进局前设置
        P1Observer.SetMainSlot(ActiveSlot);
        _military.Clear(); _done = false;
        System.Array.Clear(_wallTopDays, 0, _wallTopDays.Length);      // HH.224：J7/J8 证据清零（防跨批污染）
        System.Array.Clear(_censusSamples, 0, _censusSamples.Length);
        _buildOkDays = 0; _avoidSeen = false; _lastStageDay = int.MinValue;   // HH.230：阶段小结去重复位
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
            worldSeed = ActiveSeed, mapSeed = ActiveSeed, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = ActiveSlot, kingdomName = "河谷王国"
        };
        // HH.150 清残留：删除迁正门时遗留的裸局 SmokeApi.EnterGame+服务性等就绪块
        // （双建局冗余；EnterTestRun 内置等就绪，直接正门进局）
        // 正门进局（test-harness-first 铁律①/D585：EnterTestRun 全守卫=判负封死+T11 野怪静默+考跑档直通
        // [speedOverride=null→读 WorldConfig.time.testSpeedMultiplier SO 缺省 15]；玩家真实局态=T10 OFF）
        yield return TestHarnessApi.EnterTestRun(cfg);
        Debug.LogWarning("[HH80跑] 正门进局 seed=" + ActiveSeed + " 槽=" + ActiveSlot + " 窗口=" + ActiveBreakDay + " 日（本批短窗档=" + USE_GATHER_WINDOW + "）考跑守卫全开（D585 判负封死+T11 野怪静默，玩家真实局态挂机；P1 观测器须已在跑：镜像+CSV+检查点）");

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
            WriteStageIfDue(day, aiAlive, aiTotal);   // HH.230（D666 §8.4 机制4）：每 10 日阶段小结写档（长局"边跑边判"·禁跑完再看）
            if (day >= ActiveBreakDay) { Finish("窗口满：D" + ActiveBreakDay + "（DIAG窗口=" + USE_DIAG_WINDOW + " 本批短窗=" + USE_GATHER_WINDOW + "）AI 存活 " + aiAlive + "/" + aiTotal + "，已达标=" + string.Join(",", _military) + "；J6 退避机制面=" + _avoidSeen + " wallTop(k1/k2/k4)=" + _wallTopDays[1] + "/" + _wallTopDays[2] + "/" + _wallTopDays[4] + " 落地=" + _buildOkDays + " ⇒ 取对照证据"); break; }
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
        // J7（端到端·霸占解除）：**同段**基线＝修前逐日序列截到当前日（D678 裁；原全窗基线 ⇒ 假阳性）
        if (ENABLE_J7_WALLTOP)
        {
            for (int k = 1; k <= 4; k++)
            {
                if (k == 3) continue;                       // J7 排除 k3（其 wall 占比本就低 9/59）
                int n = _censusSamples[k];
                if (n < 30) continue;                       // 样本不足不判（防早停误裁）
                int curDay = n + 1;                          // census 自 D2 起每日一条 ⇒ 当前日＝样本数+1
                var series = BASE_WALL_TOP_DAYS[k];
                int baseCnt = 0;
                for (int i = 0; i < series.Length && series[i] <= curDay; i++) baseCnt++;
                if (baseCnt <= 0) continue;
                float now = _wallTopDays[k] / (float)n;
                float baseRate = baseCnt / (float)n;         // 修前**同段**占比
                if (now <= 0.5f * baseRate)
                {
                    why = "判据命中：J7 霸占解除（k" + k + " 同段 wallTop " + _wallTopDays[k] + "/" + n + "=" + now.ToString("F2")
                        + " ≤ 修前同段 " + baseCnt + "/" + n + "=" + baseRate.ToString("F2") + " 的半值）@D" + day;
                    return true;
                }
            }
        }
        // J8（防退化·负向）：同段基线（修前累计落地数按日插值）⇒ 全批级
        //   ⚠️HH.230／D683：**本批关闭**（见 `ENABLE_J8_BUILDOK` 注：基线源 seed 64513、本批 seed 73621 ⇒ 跨种子比较）
        if (ENABLE_J8_BUILDOK)
        {
            int nn = _censusSamples[1];
            if (nn >= 30)
            {
                float baseCnt2 = BaseBuildOkAt(nn + 1);
                if (_buildOkDays <= 0.5f * baseCnt2)
                {
                    why = "判据命中：J8 防退化报警（建造落地 " + _buildOkDays + " ≤ 修前同段 " + baseCnt2.ToString("F0")
                        + " 的半值，样本 " + nn + "）⇒ 停手报裁 @D" + day;
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>修前同段累计"建造焦点落地"数（源 `p1_fix1`；线性插值：D2~D31＝22 ／ D1~D60＝32）。</summary>
    private static float BaseBuildOkAt(int day)
    {
        if (day <= 2) return 0f;
        if (day <= 31) return BASE_BUILD_OK_D31 * (day - 2) / 29f;
        if (day >= 60) return BASE_BUILD_OK_D60;
        return BASE_BUILD_OK_D31 + (BASE_BUILD_OK_D60 - BASE_BUILD_OK_D31) * (day - 31) / 29f;
    }

    /// <summary>HH.230（D666 §8.4 机制4·② 七考重验批**强制项**）：**每 10 日阶段小结写档**（追加 `Logs/P1/hh80_run_stage.log`）。
    /// 目的＝长局"边跑边判、禁跑完再看"（`L-34`／`test-harness-first §八`）；内容＝当日存活/达标/霸占读数/落地/退避机制面。
    /// 判据检查本身由每轮 `CheckJudges`/`CheckBackoffJudges` 承担（命中即停＋回报）。</summary>
    private static void WriteStageIfDue(int day, int aiAlive, int aiTotal)
    {
        if (day <= 0 || day % 10 != 0 || day == _lastStageDay) return;
        _lastStageDay = day;
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "hh80_run_stage.log"),
                "[" + System.DateTime.Now.ToString("HH:mm:ss") + "] seed=" + ActiveSeed + " 槽=" + ActiveSlot + " D" + day
                + " AI存活=" + aiAlive + "/" + aiTotal + " 已达标=[" + string.Join(",", _military) + "]"
                + " J6退避机制面=" + _avoidSeen + " wallTop(k1/k2/k4)=" + _wallTopDays[1] + "/" + _wallTopDays[2] + "/" + _wallTopDays[4]
                + " 建造落地=" + _buildOkDays + System.Environment.NewLine);
        }
        catch (System.Exception e) { Debug.LogError("[HH80跑] 阶段小结写文件失败: " + e.Message); }
    }

    private static void Finish(string why)
    {
        _done = true;
        Application.logMessageReceived -= WatchMilitary;
        // L-32 条文3（D657 入库·HH.203 §二.7）：禁以 `SetGameSpeed(0f)` 当暂停（SnapToSpeed 吸附 0.5x 非暂停）⇒ 真暂停用 timeScale
        Time.timeScale = 0f;
        bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(ActiveSlot);   // HH.221 补：D656 硬条款2 显式传槽（短窗档曾误写死 SLOT ⇒ 污染 p1_run9）
        TestHarnessApi.ExitTestRun();   // 正门收尾：考跑态/maximumDeltaTime/渲染全量恢复（D585；封盘在后不丢档）
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== HH.80 三考收工 =====");
        sb.AppendLine(why);
        sb.AppendLine("终速=0 存盘 " + ActiveSlot + "=" + saved + " 时间=" + System.DateTime.Now.ToString("HH:mm:ss"));
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "hh80_run_status.log"), sb.ToString());
        }
        catch (System.Exception e) { Debug.LogError("[HH80跑] 状态写文件失败: " + e.Message); }
        Debug.LogWarning("[HH80跑] ★ " + why + "——真暂停(TS=0)+封盘 " + ActiveSlot + "=" + saved + "（现场保留：观测器镜像/CSV 在案；收工退 Play，L-32）");
        // L-32 条文1（D657 入库）：收工禁留「已恢复 1x」余留世界 ⇒ 容器末尾直接退 Play（同 SmokeApi.QuitSmoke 先例）
        EditorApplication.ExitPlaymode();
    }
}
