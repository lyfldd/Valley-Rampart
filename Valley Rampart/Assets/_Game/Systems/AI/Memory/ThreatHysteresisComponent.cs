using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - 威胁滞回量化器组件
//  详见 3.0.1_2_输入输出决定层设计.md §9 / 决策3
//  多级双阈值滞回量化器（威胁 3 对阈值 -> 0-3 级），IMemoryComponent 包装
// ============================================================================

/// <summary>
/// 威胁滞回量化器组件（§9，IMemoryComponent 实现）。
/// 包装 HysteresisQuantizer（威胁实例：Up=[0.25,0.5,0.75]/Down=[0.15,0.4,0.65] -> level 0-3）。
/// 吃上一帧 rawFactor（ctx.LastRaw，brain 上一帧管线产物缓存），0.1s 滞后被 0.3s 确认吸收。
/// </summary>
public class ThreatHysteresisComponent : IMemoryComponent
{
    private HysteresisQuantizer _quantizer;

    public ThreatHysteresisComponent(AttentionTuningConfig config)
    {
        _quantizer = new HysteresisQuantizer
        {
            UpThresholds = config.threatUpThresholds,
            DownThresholds = config.threatDownThresholds,
            ConfirmUp = config.threatUpgradeConfirmTime,
            ConfirmDown = config.threatDowngradeConfirmTime,
        };
    }

    /// <summary>当前威胁等级 0-3</summary>
    public ThreatLevel CurrentLevel => (ThreatLevel)_quantizer.Level;

    public void Tick(float dt, in FactorContext ctx)
    {
        // 量化器吃上一帧 rawFactor（ctx.LastRaw，由 NPCBrain ⓪ 填入）
        // 此时本帧 rawFactor 未算（管线未执行），读上一帧缓存正确
        _quantizer.Update(ctx.LastRaw, dt);
    }

    public void FillContext(ref FactorContext ctx)
    {
        ThreatLevel quantizedLevel = (ThreatLevel)_quantizer.Level;

        // stateThreatBias 落地修正（§13.3）：
        // 不叠 rawFactor（会与量化器滞回打架），改为直接抬等级保持警戒。
        // Caution 态：ctx.ThreatLevel = max(quantizerLevel, Alert)
        // 这样 Caution 期间至少保持威胁 1（警戒姿态），但不负责原地（原地靠 HoldPositionStimulus）
        if (ctx.IsCaution && quantizedLevel < ThreatLevel.Alert)
            ctx.ThreatLevel = ThreatLevel.Alert;
        else
            ctx.ThreatLevel = quantizedLevel;
    }

    public System.Collections.Generic.IReadOnlyList<IStimulus> GetActiveStimuli()
        => StimulusPool.Empty;  // 量化器不注入刺激源

    public void Reset() => _quantizer.Reset();
}
