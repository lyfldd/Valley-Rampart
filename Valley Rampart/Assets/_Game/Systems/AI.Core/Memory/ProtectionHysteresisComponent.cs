// ============================================================================
//  AI.Core Memory - 保护因子滞回量化器组件（从壳 Memory/ProtectionHysteresisComponent.cs 迁入）
//  03_大脑提取与双适配工程.md 迁移 6 步·步4。
//  接缝替换（接缝 4）：构造参数 AttentionTuningConfig -> TuningSnapshot。
// ============================================================================

using System.Collections.Generic;

/// <summary>
/// 保护因子滞回量化器组件（§9，IMemoryComponent 实现，决策8）。
/// HysteresisQuantizer 第二实例：Up=[3]/Down=[1]（1-3 滞回带，母文档 §6.4）-> level 0-1 二元。
/// 3.7 升级：输入从 nearbyAllyCount（友军数）改为 protectPowerSum（保护力加权和），
/// 阈值 = protectThreshold（可训练）。保护 = 身边友军 protectPower 之和 ≥ 阈值，训练师学"谁当保护者"。
/// 输出 ctx.HasProtection = (level >= 1)。
/// </summary>
public class ProtectionHysteresisComponent : IMemoryComponent
{
    // 3.7 保护阈值（保护力加权和，float；滞回带用 protectThreshold 上下沿）
    private float _upThreshold;
    private float _downThreshold;
    private int _level;
    private float _upTimer;
    private float _downTimer;

    public ProtectionHysteresisComponent(TuningSnapshot config)
    {
        // 3.7 保护力加权和阈值（可训练）；滞回带 = 阈值 ± 0.4（保证有缓冲）
        float prot = config.protectThreshold > 0f ? config.protectThreshold : 1f;
        _upThreshold = prot;
        _downThreshold = MathfX.Max(0.1f, prot * 0.6f);
    }

    /// <summary>当前是否有保护（level >= 1）</summary>
    public bool HasProtection => _level >= 1;

    public void Tick(float dt, in FactorContext ctx)
    {
        // 保护因子无"吃上一帧"约束，直接读本帧保护力加权和
        float protectSum = ctx.ProtectPowerSum;
        int target = protectSum >= _upThreshold ? 1 : (protectSum < _downThreshold ? 0 : _level);

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
