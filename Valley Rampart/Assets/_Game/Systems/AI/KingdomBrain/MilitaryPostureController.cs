using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  常设军事姿态层（2_22 P0 批C / C1，D518 §3.3 三档状态机）。
//  三档：无/警戒/动员——态势触发升降（带滞回防抖），不占国策焦点槽（焦点外常态行为，
//  与 FocusController 平行，KingdomBrain 持有实例）。
//
//  触发口径（2_22 §3.3 表+SituationConfig 批A 注释）：
//    警戒   = 邻接威胁非零（快照 Threats 任一国战士 ≥ alertMinThreatWarriors）
//    动员   = 军力危机线（warriorCount < SituationConfig.crisisLine）且威胁非零
//             （批A L28 注释"低于该值且威胁非零"口径；无威胁的低军力=发育常态不空转动员；
//             被宣战硬触发器=2_18 P1 接入预留挂点，本批不实现）
//    解除   = 动员→警戒/无需军力 ≥ recoveryLine（双线滞回）；警戒→无需威胁清空
//  滞回双层：①危机线/恢复线双线带（军力在两线之间抖动不切档）②hysteresisDays 天数窗。
//
//  行为驱动（消费方=KingdomBrain.Tick 子步③后 ExecutePosture）：
//    C3 巡逻 = 警戒档（PatrolTaskSystem AI 侧驱动，主威胁方向，目标数=alertPatrolCount）
//    C4 驻防+集结守军/召回 = 动员档（FormationController isGarrison 链+StopPatrol）
//
//  确定性：档位由确定性快照派生（同 seed 红线）；PostureHub 静态槽供评分域消费
//  （MachineDemand 守城需求细化，SituationHub 同型模式，纯 C# 数据面）。
// ============================================================================

/// <summary>军事姿态三档（D518；None=无常态军事行为）。</summary>
public enum MilitaryPosture
{
    None = 0,       // 无：威胁分布≈0，无常态军事行为
    Alert = 1,      // 警戒：邻接威胁非零 → 巡逻频率升（C3）
    Mobilized = 2,  // 动员：军力危机线 → 集结守军+停止远程派遣+召回（C4/C5）
}

/// <summary>姿态档静态槽（评分域/执行域跨类读取；SituationHub 同型模式，纯 C# 数据面）。</summary>
public static class PostureHub
{
    private static readonly Dictionary<int, MilitaryPosture> _map = new Dictionary<int, MilitaryPosture>();

    /// <summary>读某国当前档位（无记录 → None；评分侧纯读零副作用）。</summary>
    public static MilitaryPosture Get(int kingdomId) => _map.TryGetValue(kingdomId, out var p) ? p : MilitaryPosture.None;

    /// <summary>写入/覆盖某国档位（Evaluate 变档时调用）。</summary>
    public static void Put(int kingdomId, MilitaryPosture posture) => _map[kingdomId] = posture;

    /// <summary>王国灭亡/退订时移除（随 KingdomBrain.Unsubscribe 调用）。</summary>
    public static void Remove(int kingdomId) => _map.Remove(kingdomId);

    /// <summary>全清（harness 两轮间/新开局归零用，对齐 SituationHub.Clear 先例）。</summary>
    public static void Clear() => _map.Clear();
}

public class MilitaryPostureController
{
    private readonly int _kingdomId;

    /// <summary>当前档位（日 tick Evaluate 刷新；初始 None=八格口径无持久态，读档后首 tick 重评估）。</summary>
    public MilitaryPosture Current { get; private set; } = MilitaryPosture.None;

    /// <summary>上次档位变更日（hysteresisDays 天数滞回基准）。</summary>
    private int _lastChangeDay = int.MinValue / 2;

    /// <summary>上次档位变更日（E1 存档面读口；读档恢复滞回基准）。</summary>
    public int LastChangeDay => _lastChangeDay;

    public MilitaryPostureController(int kingdomId) { _kingdomId = kingdomId; }

    /// <summary>
    /// 读档恢复（E1）：档位+滞回基准直接落（含 PostureHub 静态槽同步）。
    /// 语义=回到存档时刻的档位态；防抖窗从 _lastChangeDay 续算（存档后首 tick 不误切档）。
    /// </summary>
    public void Restore(MilitaryPosture posture, int lastChangeDay)
    {
        Current = posture;
        _lastChangeDay = lastChangeDay;
        PostureHub.Put(_kingdomId, Current);
    }

    /// <summary>
    /// 每日档位评估（KingdomBrain.Tick 子步③后；消费当日快照+国库军力）。
    /// 滞回防抖：target==Current 直接返回；变更需距上次变更 ≥ hysteresisDays（天数窗负探针锚）。
    /// </summary>
    public void Evaluate(SituationSnapshot snap, KingdomState k, SituationConfig scfg, MilitaryPostureConfig pcfg, int day)
    {
        if (k == null) return;
        int crisis = scfg != null ? Mathf.Max(1, scfg.crisisLine) : 2;
        int recovery = scfg != null ? Mathf.Max(scfg.recoveryLine, crisis) : 4;
        int hy = pcfg != null ? Mathf.Max(0, pcfg.hysteresisDays) : 0;
        bool threat = HasThreat(snap, pcfg);

        // 目标档推导（裸判定，不含天数滞回）
        MilitaryPosture target;
        if (Current == MilitaryPosture.Mobilized)
        {
            // 动员保持/解除：军力 < 恢复线 → 保持动员（双线滞回）；≥ 恢复线 → 按威胁降警戒/无
            if (k.warriorCount < recovery) target = MilitaryPosture.Mobilized;
            else target = threat ? MilitaryPosture.Alert : MilitaryPosture.None;
        }
        else if (k.warriorCount < crisis && threat)
        {
            target = MilitaryPosture.Mobilized;   // 军力危机线+威胁非零（批A L28 口径）
        }
        else if (threat)
        {
            target = MilitaryPosture.Alert;
        }
        else
        {
            target = MilitaryPosture.None;
        }

        if (target == Current) return;
        if (day - _lastChangeDay < hy) return;   // 天数滞回窗：维持现档（负探针=阈值附近抖动不切档）
        Current = target;
        _lastChangeDay = day;
        PostureHub.Put(_kingdomId, Current);
        Debug.Log($"[MilitaryPosture] k{_kingdomId} 姿态档 → {Current}（day {day}，军力 {k.warriorCount}，威胁 {threat}）");
    }

    /// <summary>邻接威胁非零判定（快照 Threats 任一国战士数 ≥ 阈值）。</summary>
    private static bool HasThreat(SituationSnapshot snap, MilitaryPostureConfig pcfg)
    {
        if (snap?.Threats == null) return false;
        int min = pcfg != null ? Mathf.Max(0, pcfg.alertMinThreatWarriors) : 1;
        for (int i = 0; i < snap.Threats.Count; i++)
            if (snap.Threats[i].WarriorCount >= min) return true;
        return false;
    }
}
