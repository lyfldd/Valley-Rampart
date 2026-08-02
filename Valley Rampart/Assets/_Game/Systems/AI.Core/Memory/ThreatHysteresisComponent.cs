// ============================================================================
//  AI.Core Memory - 威胁滞回量化器组件（从壳 Memory/ThreatHysteresisComponent.cs 迁入）
//  03_大脑提取与双适配工程.md 迁移 6 步·步4。
//  接缝替换（接缝 4）：构造参数 AttentionTuningConfig -> TuningSnapshot。
// ============================================================================

using System.Collections.Generic;

/// <summary>
/// 威胁滞回量化器组件（§9，IMemoryComponent 实现）。
/// 包装 HysteresisQuantizer（威胁实例：Up=[0.25,0.5,0.75]/Down=[0.15,0.4,0.65] -> level 0-3）。
/// 吃上一帧 rawFactor（ctx.LastRaw，brain 上一帧管线产物缓存），0.1s 滞后被 0.3s 确认吸收。
/// </summary>
public class ThreatHysteresisComponent : IMemoryComponent
{
    private HysteresisQuantizer _quantizer;

    public ThreatHysteresisComponent(TuningSnapshot config)
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
