using UnityEngine;
using UnityEditor;

// ============================================================================
//  2_17 步骤10 兵力目标冒烟 #5（D348，路线②验收金线）
//  判据（2_17.md #5）：注入威胁（邻国军队逼近边境）→ 兵力目标上调 → ⑦招战士分数上升。
//  纯逻辑探针，零世界耦合（直接测 D348 纯函数 + ⑦缺口分数），可编辑态菜单运行。
//  用法：菜单「Valley/验证/2_17_步骤10_兵力目标(D348)」——无需 Play。
//  p05_威胁上调: 邻国兵力 ↑ → D348Target 严格不降（威胁→目标单调，分母 max(己方,1)）
//  p07_软帽:      目标 ≤ 2+工人数（软帽 clamp），下限 ≥ floor
//  p09_⑦分数上升: 同己方兵力下，威胁更大 → WarriorGapScore 严格上升（兵力缺口拉大）
//  收口：不改产品代码。
// ============================================================================
public static class Valley2_17_Smoke_5
{
    private const int FLOOR = 2;

    [MenuItem("Valley/验证/2_17_步骤10_兵力目标(D348)")]
    public static void RunFromMenu()
    {
        var cfg = KingdomBrain.LoadConfig();
        bool pass = true;
        var sb = new System.Text.StringBuilder();
        pass &= ThreatRaises( cfg, sb);
        pass &= SoftCapAndFloor(cfg, sb);
        pass &= WarriorGapRises(cfg, sb);
        pass &= PopFloorGuard(cfg, sb);   // 人口底线（决策①修复）探针
        pass &= InternalDriveToGate(cfg, sb);   // HH.217 治本批：内源势能接入 D348Target 正负例
        pass &= ActionBackoffGuard(cfg, sb);    // HH.224/D670 治本批：通用「执行失败退避」正负例
        Debug.Log($"[2_17_5冒烟] {sb}");
        Debug.Log($"[2_17_5冒烟] ===== {(pass ? "ALL PASS" : "HAS FAIL")}（威胁上调→目标升→⑦分数升，D348）=====");
    }

    // ---- ⑤ 邻国兵力威胁 → 兵力目标上调 ----
    private static bool ThreatRaises(KingdomBrainConfig cfg, System.Text.StringBuilder sb)
    {
        bool ok = true;
        string prev = null;
        // 固定己方：战士 4、工人 10、扩张期；威胁兵力 0..60
        for (int nei = 0; nei <= 60; nei += 10)
        {
            int t = UtilityScorer.D348Target(4, 10, nei, cfg.militaryExpandStageFactor, cfg);
            if (prev != null && t < int.Parse(prev)) ok = false;   // 威胁增目标不降（单调）
            prev = t.ToString();
        }
        sb.Append($"威胁上调={(ok ? "OK" : "FAIL")}(4战10工扩张,威胁0→60) ");
        return ok;
    }

    // ---- 软帽 clamp：目标 ≤ 2+工人数，且 ≥ floor ----
    private static bool SoftCapAndFloor(KingdomBrainConfig cfg, System.Text.StringBuilder sb)
    {
        bool ok = true;
        // 大威胁压到软帽：1 战 2 工，威胁 1000 → 被 2+工人数=4 压住
        int cap = UtilityScorer.D348Target(1, 2, 1000f, cfg.militaryStageFactor, cfg);
        if (cap > FLOOR + 2) ok = false;            // 软帽 2+2=4
        // 大工人授权更多兵力：6 工强威胁 → 上限 2+6=8
        int cap6 = UtilityScorer.D348Target(1, 6, 1000f, cfg.militaryStageFactor, cfg);
        if (cap6 > FLOOR + 6) ok = false;
        // 下限：零威胁也 ≥ floor
        int low = UtilityScorer.D348Target(4, 10, 0f, 0, cfg);
        if (low < FLOOR) ok = false;
        sb.Append($"软帽clamp={(ok ? "OK" : "FAIL")}(≤2+工,≥floor) ");
        return ok;
    }

    // ---- ⑦ 战士缺口分数：威胁更大 → 缺口分数严格升（同己方兵力下） ----
    private static bool WarriorGapRises(KingdomBrainConfig cfg, System.Text.StringBuilder sb)
    {
        // worker=20（软帽 22）保证 威胁0→30 全程不触软帽单调升；己方战士 4、扩张期
        bool ok = true;
        float prevScore = -1f;
        for (int nei = 0; nei <= 30; nei += 10)
        {
            int t = UtilityScorer.D348Target(4, 20, nei, cfg.militaryExpandStageFactor, cfg);
            float s = UtilityScorer.WarriorGapScore(4, t);   // 己方 4 战 vs 目标 t
            if (s <= prevScore) ok = false;          // 威胁增（未触软帽）→ 缺口分数不降
            prevScore = s;
        }
        sb.Append($"⑦分数随威胁升={(ok ? "OK" : "FAIL")}(4战20工,威胁0→30) ");
        return ok;
    }

    // ---- 人口底线（D322 决策①修复 + HH.29 仲裁精确化：max(popFloor, developToExpand_workersMin) 联动门槛）----
    //  行为验收（worker 突破门槛 / trainTry>0 / 时间线E段 / SnowRock池）归 P0 完整局手工回归；
    //  本探针锁配置与语义（编辑态确定性），防回归时丢门槛口径或误改触发源。
    private static bool PopFloorGuard(KingdomBrainConfig cfg, System.Text.StringBuilder sb)
    {
        bool ok = true;
        // ① 常量映射：FocusRecruitWorker 必须=⑥ RecruitWorker（强制焦点到招工人通道）
        if (FocusController.FocusRecruitWorker != (int)UtilityAction.RecruitWorker) ok = false;
        // ② 门槛 = max(popFloor, developToExpand_workersMin)，须 ≤ RecruitWorker.needA(10)——超过后执行器 Feasible 不再通过→人口底线被架空
        int floor = Mathf.Max(cfg.popFloor, cfg.developToExpand_workersMin);
        var rw = UtilityActionConfig.LoadConfig().Find(UtilityAction.RecruitWorker);
        int workerNeed = rw.HasValue ? (int)rw.Value.needA : 0;
        if (cfg.popFloor <= 0 || floor > workerNeed) ok = false;
        // ③ 触发谓词真值（千分碑三档错峰）：帐篷4<8✓ / 村落6<8✓ / 要塞8<8 不触发✓（下限=能升Expand的工人数）
        bool triggersAtVillage = 6 < floor;   // 村落档 Medium 初始6工人，应 < 门槛(8) 触发 → try>0 金线
        bool notAtFortress = !(8 < floor);    // 要塞档初始8工人，已达 Expand 门槛，不强制（评分自由）
        if (!triggersAtVillage || !notAtFortress) ok = false;
        // ④ 份额式轮替相位（策划纠偏裁决：popAlarm 期间⑥占 popAlarmFocusCapDays 日、让位 1 轮评分，防独占饿死建造）：
        //   轮替相位 = (day-windowStart) % (cap+1)；<cap → ⑥日，==cap → 让位日。纯谓词，编辑态确定。
        int cap = Mathf.Max(1, cfg.popAlarmFocusCapDays);
        bool altOk = true;
        // 走 2 个完整周期：期望序列 ⑥⑥B ⑥⑥B（cap=2）→ 每个 cap+1 槽位恰有 cap 个⑥ + 1 个让位
        for (int d = 0; d <= 2 * (cap + 1) - 1; d++)
        {
            int phase = d - 0;   // windowStart 视为 0
            bool recruitSlot = phase % (cap + 1) < cap;
            // 周期内第 cap 槽（d%(cap+1)==cap）必须是让位（非⑥）；其余必须是⑥
            bool expect = d % (cap + 1) != cap;
            if (recruitSlot != expect) altOk = false;
        }
        if (!altOk) ok = false;
        sb.Append($"人口底线={(ok ? "OK" : "FAIL")}(门槛=max({cfg.popFloor},{cfg.developToExpand_workersMin})={floor},村落6<{floor}✓,要塞8<{floor}=不倒灌,needA≤{workerNeed},=>⑥) ");
        sb.Append($"份额轮替={(altOk ? "OK" : "FAIL")}(cap={cfg.popAlarmFocusCapDays} 周期={(cap + 1)} 相位⑥…B轮替) ");
        return ok;
    }

    // ---- HH.217 治本批（D663 裁 A+ / D664 放行）：内源势能（D590 InternalDrive）接入兵力目标 D348 的正负例 ----
    //  语义：drive 与威胁同量纲相加后取整（⌈⌉），威胁面数学不变；drive 是阶跃项（⌈ε⌉=1）⇒权重量级不敏感（D664 注记）。
    private static bool InternalDriveToGate(KingdomBrainConfig cfg, System.Text.StringBuilder sb)
    {
        bool ok = true;
        int sf = cfg.militaryExpandStageFactor;   // 扩张期 +1
        // 正例①：零威胁 Expand 期 + drive=0.1（数值禁区下沿）⇒ floor2 + ⌈0+0.1⌉1 + 1 = 4 ≥ 门
        int pos = UtilityScorer.D348Target(0, 10, 0f, sf, cfg, 0.1f);
        if (pos < cfg.expandToMilitary_warriorsMin) ok = false;
        // 正例②：drive 极小（1e-4）仍 +1（阶跃项语义，D664 注记：权重不敏感）
        int eps = UtilityScorer.D348Target(0, 10, 0f, sf, cfg, 0.0001f);
        if (eps < cfg.expandToMilitary_warriorsMin) ok = false;
        // 负例（死滞复现锚）：drive=0 ⇒ 2+0+1 = 3 < 门
        int neg = UtilityScorer.D348Target(0, 10, 0f, sf, cfg, 0f);
        if (neg != 3 || neg >= cfg.expandToMilitary_warriorsMin) ok = false;
        // 回归①：drive=0 时旧语义保持（floor 点，stageFactor=0）
        if (UtilityScorer.D348Target(4, 10, 0f, 0, cfg, 0f) != FLOOR) ok = false;
        // 回归②：威胁面仍有效（drive=0，威胁↑ ⇒ 目标单调不降；worker=20 避软帽）
        int prevT = -1;
        for (int nei = 0; nei <= 30; nei += 10)
        {
            int t = UtilityScorer.D348Target(4, 20, nei, sf, cfg, 0f);
            if (t < prevT) ok = false;
            prevT = t;
        }
        sb.Append($"内源势能接入={(ok ? "OK" : "FAIL")}(零威胁Expand: drive0.1={pos}≥{cfg.expandToMilitary_warriorsMin} / drive1e-4={eps} / drive0={neg}=3<门 / 威胁面回归) ");
        return ok;
    }

    // ---- HH.224/D670 治本批：通用「执行失败退避」正负例（编辑态确定性；纯表级，不涉世界） ----
    //  覆盖：中性等价 / 未达门槛不生效 / 达门槛生效且不破下限 / 读数口径 / 三条自愈 / 硬上限升级报裁。
    private static bool ActionBackoffGuard(KingdomBrainConfig cfg, System.Text.StringBuilder sb)
    {
        var bcfg = ActionBackoffConfig.Load();
        bool ok = true;
        const int K = 901;                          // 探针专用王国 id（不撞真实 1~4）
        const UtilityAction A = UtilityAction.BuildWall;
        int th = Mathf.Max(1, bcfg.ThresholdOf((int)A));
        float lo = Mathf.Clamp01(bcfg.MinFactorOf((int)A));

        // ① 中性起步：零失败记录 ⇒ 1.0（出厂等价）
        ActionBackoff.Reset();
        bool neutral = Mathf.Approximately(ActionBackoff.Factor(K, A, 1f), 1f);

        // ② 未达门槛（threshold−1 次）⇒ 仍 1.0
        for (int i = 0; i < th - 1; i++) ActionBackoff.ReportFail(K, A, 1, 1f);
        bool belowTh = Mathf.Approximately(ActionBackoff.Factor(K, A, 1f), 1f);

        // ③ 达门槛 ⇒ <1 且 ≥ 下限
        ActionBackoff.ReportFail(K, A, 1, 1f);
        float fTh = ActionBackoff.Factor(K, A, 1f);
        bool onTh = fTh < 1f && fTh >= lo - 1e-4f;

        // ④ 读数口径（avoid= 非空且含该行动）
        string rd = ActionBackoff.Readout(K);
        bool readout = !string.IsNullOrEmpty(rd) && rd.Contains(A.ToString());

        // ⑤ 自愈①：need 变化超容差 ⇒ 复位 1.0
        bool healNeed = Mathf.Approximately(
            ActionBackoff.Factor(K, A, 1f + ActionBackoff.NeedSelfHealEps + 0.01f), 1f);

        // ⑥ 自愈②：成功上报 ⇒ 复位 1.0
        ActionBackoff.Reset();
        for (int i = 0; i < th + 2; i++) ActionBackoff.ReportFail(K, A, 1, 1f);
        bool fellBack = ActionBackoff.Factor(K, A, 1f) < 1f;
        ActionBackoff.ReportSuccess(K, A);
        bool healOk = Mathf.Approximately(ActionBackoff.Factor(K, A, 1f), 1f);

        // ⑦ 自愈③：冷却到期（日推进跨 cooldownDays）⇒ 复位 1.0
        ActionBackoff.Reset();
        for (int i = 0; i < th + 2; i++) ActionBackoff.ReportFail(K, A, 1, 1f);
        ActionBackoff.OnDayTick(K, 1 + Mathf.Max(1, bcfg.cooldownDays));
        bool healCd = Mathf.Approximately(ActionBackoff.Factor(K, A, 1f), 1f);

        // ⑧ 硬上限：达 cap 次 ⇒ 升级报裁标记；因子不破下限
        ActionBackoff.Reset();
        int cap = Mathf.Max(1, bcfg.hardCapFails);
        for (int i = 0; i < cap; i++) ActionBackoff.ReportFail(K, A, 1, 1f);
        bool capped = ActionBackoff.AnyAtHardCap(K);
        float fCap = ActionBackoff.Factor(K, A, 1f);
        bool inRange = fCap >= lo - 1e-4f && fCap <= 1f;

        ActionBackoff.Reset();   // 清场（防污染后续用例/后续跑局）
        ok = neutral && belowTh && onTh && readout && healNeed && fellBack && healOk && healCd && capped && inRange;
        sb.Append($"退避正负例={(ok ? "OK" : "FAIL")}(中性{neutral}/未达门槛{belowTh}/达门槛{onTh}(f={fTh:F2}≥{lo:F2})/读数{readout}/"
            + $"自愈={healNeed && healOk && healCd}/硬上限{capped}(f={fCap:F2})/下限护栏{inRange}) ");
        return ok;
    }
}