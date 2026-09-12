using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  通用「执行失败退避」表（HH.224 焦点霸占治本批 / D670 裁决 · 红线① sim-sync 免除）
//  职责：per-kingdom × **per-action** 失败计数/冷却 ⇒ 评分面乘子（`UtilityScorer.ScoreTop` 消费）。
//
//  病因（HH.223 §3.2 / D670 破框裁）："选中但执行失败却不降权"⇒行动永久霸占焦点；
//    三样本 wall 失败 181/181、103/106、17 中 wall5 ⇒ **通用病灶**（非只 ⑨ wall）。
//  设计约束（D675 逐项裁 ＋ 硬约束三条）：
//    · 载体＝**静态字典**（对齐 `KingdomBrain.s_dispatch` 先例 `:69`；运行时态不入档=读档后归零属已知边界）。
//    · 键＝**kingdomId × actionId**（禁 per-call-site 粒度——过细会掩盖根因）。
//    · ①**只乘 `score`**：本类不被 `NeedScore`/`Feasible` 引用（否则 2_23RP0 P4b~P4e 断言必 FAIL ＋ 违 D525 §3.7）。
//    · ②**须有 Reset 入口并与 `ResetDispatchStats` 同点清零**（→ `KingdomBrain.ResetDispatchStats` 内调用）。
//    · ③**退避必须自愈 ＋ 有上限**：成功即复位；need 变化（目标变化/资源跨门槛）即复位；
//         冷却到期即复位；达 `hardCapFails` 锁下限并打 **BACKOFF_CAP** 升级报裁标记（防"死循环→静默永久弃建"）。
//
//  形态先例（**非等价路径**）：`AI.Core/Memory/HitCooldownStateMachine.cs`（单位级受击冷却·降权＋自愈三态）。
//  边界（HH.225 §二 实测）：本层双端零镜像 ⇒ 不进 `AI.Core`／不走 sim-sync；本批**未经 sim 门禁**。
// ============================================================================
public static class ActionBackoff
{
    /// <summary>退避升级报裁标记（DiagMilitary `verdict=` 消费；达硬上限仍失败时出现）。</summary>
    public const string CapMarker = "BACKOFF_CAP";

    /// <summary>自愈容差（结构参数）：need 变化超过本阈值才判"状态已变"⇒ 复位。
    /// 容差必要性＝部分 need 为连续量（粮裕日/占用率等）会日间抖动，零容差会导致退避**永远无法成立**。</summary>
    public const float NeedSelfHealEps = 0.05f;

    private struct State
    {
        public int fails;         // 连续失败次数（成功/自愈即清零）
        public int lastFailDay;   // 最近一次失败日（冷却自愈判据）
        public float snapNeed;    // 失败当时 need 快照（状态变化自愈判据）
        public bool hasSnap;
        public bool lastEnv;      // 最近一次失败的**分型**＝环境让渡型（HH.226 追加项③·仅标注，不改行为）
    }

    /// <summary>失败分型（HH.226 追加项③／D678 裁：退避**不问归因**照常计数，仅在 `avoid=` 读数标注分型）。
    /// <para>`Env`＝**环境让渡型**（如 ⑥「流浪池无同族可招」——HH.28 裁决① 归让渡，AI 无法以自身行动消除）；
    /// `Self`＝**行动自身型**（资源不足／前置建筑缺失／无合法落点／门面校验未过 等）。</para></summary>
    public enum FailKind { Self = 0, Env = 1 }

    private static readonly Dictionary<long, State> _t = new Dictionary<long, State>();

    private static long Key(int kingdomId, int actionId)
        => ((long)kingdomId << 16) | (uint)(actionId & 0xFFFF);

    /// <summary>评分面乘子（`ScoreTop` 的 `score` 乘入本值）。
    /// 无记录/未达门槛 ⇒ 1.0（出厂等价）；**含自愈判定**（need 变化 ⇒ 当场复位并返回 1.0）。</summary>
    public static float Factor(int kingdomId, UtilityAction a, float curNeed)
    {
        int aid = (int)a;
        var cfg = ActionBackoffConfig.Load();
        if (aid <= 0 || !cfg.EnabledOf(aid)) return 1f;

        long key = Key(kingdomId, aid);
        State s;
        if (!_t.TryGetValue(key, out s)) return 1f;

        // 自愈①：状态变化（need 变化超出容差 ⇒ 目标变化／资源跨门槛改变了缺口）⇒ 复位
        if (s.hasSnap && Mathf.Abs(curNeed - s.snapNeed) > NeedSelfHealEps)
        {
            _t.Remove(key);
            return 1f;
        }

        int th = Mathf.Max(1, cfg.ThresholdOf(aid));
        if (s.fails < th) return 1f;

        float f = 1f - (s.fails - th + 1) * Mathf.Max(0f, cfg.stepPerFail);
        float lo = Mathf.Clamp01(cfg.MinFactorOf(aid));
        return Mathf.Clamp(f, lo, 1f);
    }

    /// <summary>**达退避硬上限 ⇒ 强制让位**判据（HH.226 追加项①-b／D678 破框裁：选择层实现，**非 `Feasible`**）。
    /// **自愈回池**同源：成功／need 变化／冷却到期 ⇒ 条目已清 ⇒ 本方法返回 false（自动回池）。
    /// <para>语义修正（D678 勘正①）：`hardCapFails` **不参与因子计算**——因子下限由 `Factor` 的 `Clamp(f,lo,1)`
    /// 对**所有 `fails≥th`** 生效（出厂值下 `fails=5` 即达下限 0.25）；`hardCapFails` 的语义＝**升级报裁触发线 ＋ 让位门线**。</para></summary>
    public static bool IsCapped(int kingdomId, UtilityAction a, float curNeed)
    {
        int aid = (int)a;
        var cfg = ActionBackoffConfig.Load();
        if (aid <= 0 || !cfg.EnabledOf(aid)) return false;

        long key = Key(kingdomId, aid);
        State s;
        if (!_t.TryGetValue(key, out s)) return false;
        // 自愈回池（与 Factor 同口径）：need 变化超容差 ⇒ 状态已变 ⇒ 复位并回池
        if (s.hasSnap && Mathf.Abs(curNeed - s.snapNeed) > NeedSelfHealEps)
        {
            _t.Remove(key);
            return false;
        }
        return s.fails >= Mathf.Max(1, cfg.hardCapFails);
    }

    /// <summary>上报"真失败"（**不得**用于"已达目标/正常态"的 `Bump(ok:false)`——见 HH.225 §一 落点清单分流）。
    /// `kind` 仅作**分型标注**（HH.226 追加项③），不改变计数/退避行为（D678 裁：退避不问归因）。</summary>
    public static void ReportFail(int kingdomId, UtilityAction a, int day, float needNow, FailKind kind = FailKind.Self)
    {
        int aid = (int)a;
        var cfg = ActionBackoffConfig.Load();
        if (aid <= 0 || !cfg.EnabledOf(aid)) return;

        long key = Key(kingdomId, aid);
        State s;
        if (!_t.TryGetValue(key, out s)) s = new State();
        s.fails++;
        s.lastFailDay = day;
        s.snapNeed = needNow;
        s.hasSnap = true;
        s.lastEnv = kind == FailKind.Env;
        _t[key] = s;

        // 硬上限（硬约束③）：达上限仍失败 ⇒ 锁下限 ＋ 一次性升级报裁标记
        int cap = Mathf.Max(1, cfg.hardCapFails);
        if (s.fails == cap)
            Debug.LogWarning($"[ActionBackoff] k{kingdomId} 行动 {a} 连续失败达硬上限 {cap} ⇒ 因子锁下限 {cfg.MinFactorOf(aid):F2}（升级报裁 {CapMarker}；防「死循环→静默永久弃建」）");
    }

    /// <summary>上报"真成功"（落地）⇒ 该 (国,行动) 计数清零（自愈之二）。</summary>
    public static void ReportSuccess(int kingdomId, UtilityAction a)
    {
        int aid = (int)a;
        if (aid <= 0) return;
        _t.Remove(Key(kingdomId, aid));
    }

    /// <summary>日推进（KingdomBrain 日 tick 调用）：冷却到期 ⇒ 复位（自愈之三·时间维度，覆盖"世界变了"）。</summary>
    public static void OnDayTick(int kingdomId, int day)
    {
        var cfg = ActionBackoffConfig.Load();
        int cd = Mathf.Max(1, cfg.cooldownDays);
        long lo = ((long)kingdomId << 16);
        long hi = lo | 0xFFFF;
        List<long> expired = null;
        foreach (var kv in _t)
        {
            if (kv.Key < lo || kv.Key > hi) continue;
            if (day - kv.Value.lastFailDay >= cd)
                (expired ?? (expired = new List<long>())).Add(kv.Key);
        }
        if (expired != null) for (int i = 0; i < expired.Count; i++) _t.Remove(expired[i]);
    }

    /// <summary>该国当前退避读数（`action:factor,…`，因子升序，仅列 <1 者；无 ⇒ 空串）。
    /// ⚠️口径（L-35）：**退避表内条目的因子快照**（`score` 实乘同源）；**排除项**＝不含 `NeedScore`/`Feasible` 返回值。
    /// 已被 `Factor()` 自愈复位（need 变化）的条目不在表内 ⇒ 读数与实乘一致。</summary>
    public static string Readout(int kingdomId, int max = 3)
    {
        var cfg = ActionBackoffConfig.Load();
        long lo = ((long)kingdomId << 16);
        long hi = lo | 0xFFFF;
        List<KeyValuePair<int, float>> list = null;
        foreach (var kv in _t)
        {
            if (kv.Key < lo || kv.Key > hi) continue;
            int aid = (int)(kv.Key & 0xFFFF);
            if (!cfg.EnabledOf(aid)) continue;
            int th = Mathf.Max(1, cfg.ThresholdOf(aid));
            if (kv.Value.fails < th) continue;
            float f = Mathf.Clamp(1f - (kv.Value.fails - th + 1) * Mathf.Max(0f, cfg.stepPerFail),
                                  Mathf.Clamp01(cfg.MinFactorOf(aid)), 1f);
            if (f >= 0.999f) continue;
            (list ?? (list = new List<KeyValuePair<int, float>>())).Add(new KeyValuePair<int, float>(aid, f));
        }
        if (list == null) return "";
        list.Sort((x, y) => x.Value != y.Value ? x.Value.CompareTo(y.Value) : x.Key.CompareTo(y.Key));
        var sb = new System.Text.StringBuilder();
        int n = Mathf.Min(max, list.Count);
        for (int i = 0; i < n; i++)
        {
            if (i > 0) sb.Append(',');
            int aid = list[i].Key;
            State st;
            bool env = _t.TryGetValue(Key(kingdomId, aid), out st) && st.lastEnv;
            sb.Append((UtilityAction)aid).Append(':').Append(list[i].Value.ToString("F2"))
              .Append(env ? "(Env)" : "(Self)");   // HH.226 追加项③：失败分型标注（不改行为）
        }
        return sb.ToString();
    }

    /// <summary>该国是否存在"达硬上限仍失败"的行动（升级报裁判据；DiagMilitary `verdict=` 消费）。</summary>
    public static bool AnyAtHardCap(int kingdomId)
    {
        var cfg = ActionBackoffConfig.Load();
        int cap = Mathf.Max(1, cfg.hardCapFails);
        long lo = ((long)kingdomId << 16);
        long hi = lo | 0xFFFF;
        foreach (var kv in _t)
            if (kv.Key >= lo && kv.Key <= hi && kv.Value.fails >= cap) return true;
        return false;
    }

    /// <summary>全清（跨局/跨轮归零；**已并入 `KingdomBrain.ResetDispatchStats` 同点清零**＝硬约束②）。</summary>
    public static void Reset() => _t.Clear();
}
