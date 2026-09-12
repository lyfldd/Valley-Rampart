using System;
using UnityEngine;

// ============================================================================
//  行动执行失败退避配置 SO（HH.224 焦点霸占治本批 / D670 裁决；so-data-driven 禁魔法数）
//  资产路径：Resources/Config/Kingdoms/ActionBackoffConfig.asset（ResourceBiasConfig 先例 R-B2）
//  消费方：ActionBackoff（退避表：KingdomBrain 日 tick 上报失败/成功/日推进；UtilityScorer.ScoreTop 读因子）
//
//  病因（HH.223 §3.2 / D670）："**选中但执行失败却不降权**" ⇒ 行动永久霸占焦点
//    （三样本 wall 失败 181/181、103/106、17 中 wall5；`WallGap` 纯状态函数·`Feasible` 只判资源与存量）。
//  修法（D670 自造 A+）：**通用「执行失败退避」**——per-kingdom × per-action 失败计数/冷却，
//    评分面 `score` 乘退避因子；**退避必须自愈**（状态变化即复位 ＋ 冷却到期复位）；**达上限仍失败 ⇒ 升级报裁**。
//
//  边界注记（HH.225 §二 实测）：王国脑层（`Systems/AI/KingdomBrain/`）**双端零镜像**——
//    Unity `AI.Core/`（34 文件）与训练仓 `harness/` 对 UtilityScorer/KingdomBrain/FocusController/
//    PlacementScorer/UtilityActionConfig/BattleLearnedWeights 均**零命中** ⇒ 本 SO **不进 AI.Core、不走 sim-sync**；
//    本批**未经 sim 门禁**（D675 裁决⑥，交付报告须写明）。
//
//  ⚠️下方数值＝**出厂占位值**（HH.225 §四 报审 · 待策划端裁）；执行端不据结果调值（禁参数微调找补 D563③）。
// ============================================================================

/// <summary>per 行动退避覆盖条目（可选；未列=用全局值）。</summary>
[Serializable]
public struct ActionBackoffOverride
{
    [Tooltip("目标行动（UtilityAction 值）")]
    public UtilityAction action;

    [Tooltip("覆盖：连续失败达 N 次开始退避（0=沿用全局）")]
    public int failThreshold;

    [Tooltip("覆盖：因子下限（0=沿用全局）")]
    public float minFactor;

    [Tooltip("是否启用该覆盖")]
    public bool enabled;
}

[CreateAssetMenu(menuName = "ValleyRampart/ActionBackoffConfig", fileName = "ActionBackoffConfig")]
public class ActionBackoffConfig : ScriptableObject
{
    [Header("总开关（false ⇒ 退避完全失效=零行为差异；出厂 true）")]
    public bool enabled = true;

    [Header("连续失败达 N 次 ⇒ 开始退避（出厂占位 3）")]
    public int failThreshold = 3;

    [Header("阈值档之上每多一次失败再降的幅度（出厂占位 0.25）")]
    public float stepPerFail = 0.25f;

    [Header("因子下限（>0 保留翻盘可能，不退到 0；出厂占位 0.25）")]
    public float minFactor = 0.25f;

    [Header("冷却天数：连续 N 日无新失败 ⇒ 复位为 1（自愈·防空转变永久弃建；出厂占位 5）")]
    public int cooldownDays = 5;

    [Header("硬上限：连续失败达此数 ⇒ 因子锁下限 ＋ 打退避上限标记（升级报裁）；出厂占位 12")]
    public int hardCapFails = 12;

    [Header("per 行动覆盖（可选；未列=用上方全局值）")]
    public ActionBackoffOverride[] overrides;

    /// <summary>某行动的失败门槛（覆盖优先；越界/未启用 → 全局）。</summary>
    public int ThresholdOf(int actionId)
    {
        var o = FindOverride(actionId);
        return o.HasValue && o.Value.failThreshold > 0 ? o.Value.failThreshold : failThreshold;
    }

    /// <summary>某行动的因子下限（覆盖优先；越界/未启用 → 全局）。</summary>
    public float MinFactorOf(int actionId)
    {
        var o = FindOverride(actionId);
        return o.HasValue && o.Value.minFactor > 0f ? o.Value.minFactor : minFactor;
    }

    /// <summary>某行动是否参与退避（总开关 × 覆盖 enabled）。</summary>
    public bool EnabledOf(int actionId)
    {
        if (!enabled) return false;
        if (overrides != null)
        {
            for (int i = 0; i < overrides.Length; i++)
                if (overrides[i].action != UtilityAction.None && (int)overrides[i].action == actionId && !overrides[i].enabled)
                    return false;
        }
        return true;
    }

    private ActionBackoffOverride? FindOverride(int actionId)
    {
        if (overrides == null) return null;
        for (int i = 0; i < overrides.Length; i++)
            if (overrides[i].enabled && overrides[i].action != UtilityAction.None && (int)overrides[i].action == actionId)
                return overrides[i];
        return null;
    }

    /// <summary>载入（缺 asset 时回退默认占位实例 ⇒ 出厂零配置仍生效；对齐 ResourceBiasConfig.Load 先例）。</summary>
    public static ActionBackoffConfig Load()
    {
        var cfg = Resources.Load<ActionBackoffConfig>("Config/Kingdoms/ActionBackoffConfig");
        return cfg != null ? cfg : CreateInstance<ActionBackoffConfig>();
    }
}
