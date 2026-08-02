// ============================================================================
//  AI.Core Memory - 保护因子滞回量化器组件（从壳 Memory/ProtectionHysteresisComponent.cs 迁入）
//  03_大脑提取与双适配工程.md 迁移 6 步·步4。
//  接缝替换（接缝 4）：构造参数 AttentionTuningConfig -> TuningSnapshot。
// ============================================================================

using System.Collections.Generic;

/// <summary>
/// 保护因子滞回量化器组件（§9，IMemoryComponent 实现，决策8）。
/// HysteresisQuantizer 第二实例：Up=[3]/Down=[1]（1-3 滞回带，母文档 §6.4）-> level 0-1 二元。
/// 输入 nearbyAllyCount，输出 ctx.HasProtection = (level >= 1)。
/// P0 不扩谱系（保护因子只参与"威胁 2 有保护->谱系 2 谨慎 / 无保护->谱系 4 撤退"判定）。
/// </summary>
public class ProtectionHysteresisComponent : IMemoryComponent
{
    // 保护阈值是整数友军数，量化器用 float 输入，此处直接转
    private int _upThreshold;
    private int _downThreshold;
    private int _level;
    private float _upTimer;
    private float _downTimer;

    public ProtectionHysteresisComponent(TuningSnapshot config)
    {
        // config.protectionUpThresholds=[3] / protectionDownThresholds=[1]
        _upThreshold = config.protectionUpThresholds != null && config.protectionUpThresholds.Length > 0
            ? config.protectionUpThresholds[0] : 3;
        _downThreshold = config.protectionDownThresholds != null && config.protectionDownThresholds.Length > 0
            ? config.protectionDownThresholds[0] : 1;
    }

    /// <summary>当前是否有保护（level >= 1）</summary>
    public bool HasProtection => _level >= 1;

    public void Tick(float dt, in FactorContext ctx)
    {
        // 保护因子无"吃上一帧"约束，直接读本帧 nearbyAllyCount
        int allyCount = ctx.NearbyAllyCount;
        int target = allyCount >= _upThreshold ? 1 : (allyCount < _downThreshold ? 0 : _level);

        if (target > _level)
        {
            _upTimer += dt;
            _downTimer = 0f;
            if (_upTimer >= ctx.Config.threatUpgradeConfirmTime)
            {
                _level = target;
                _upTimer = 0f;
            }
        }
        else if (target < _level)
        {
            _downTimer += dt;
            _upTimer = 0f;
            if (_downTimer >= ctx.Config.threatDowngradeConfirmTime)
            {
                _level = target;
                _downTimer = 0f;
            }
        }
        else
        {
            _upTimer = 0f;
            _downTimer = 0f;
        }
    }

    public void FillContext(ref FactorContext ctx)
    {
        ctx.HasProtection = _level >= 1;
    }

    public IReadOnlyList<IStimulus> GetActiveStimuli()
        => StimulusPool.Empty;  // 量化器不注入刺激源

    public void Reset()
    {
        _level = 0;
        _upTimer = 0f;
        _downTimer = 0f;
    }
}
