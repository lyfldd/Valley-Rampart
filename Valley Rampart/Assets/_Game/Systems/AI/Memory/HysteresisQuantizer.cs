using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - 多级滞回量化器
//  详见 3.0.1_2_输入输出决定层设计.md §9 / §10
//  决策3：多级双阈值滞回量化器（威胁 3 对阈值 -> 0-3 级 / 保护 1 对 -> 二元，同一组件）
// ============================================================================

/// <summary>
/// 多级双阈值滞回量化器（§9）。
/// 两实例：威胁（UpThresholds=[0.25,0.5,0.75]/Down=[0.15,0.4,0.65] -> level 0-3）
///         保护（Up=[3]/Down=[1]，数组长 1 的特例 -> level 0-1）。
/// 升阈 > 降阈，差值 = 滞回带，带内保持原级。
/// 吃上一帧输入（brain 持有的 lastRaw 缓存，非本帧），0.1s 滞后被 0.3s 确认吸收。
/// </summary>
public struct HysteresisQuantizer
{
    /// <summary>升阈数组：威胁 [0.25,0.5,0.75] / 保护 [3]</summary>
    public float[] UpThresholds;
    /// <summary>降阈数组：威胁 [0.15,0.4,0.65] / 保护 [1]</summary>
    public float[] DownThresholds;
    /// <summary>升级确认 0.3s（母文档 §9）</summary>
    public float ConfirmUp;
    /// <summary>降级确认 0.5s（母文档 §9）</summary>
    public float ConfirmDown;

    private int _level;
    private float _upTimer;
    private float _downTimer;
    private int _pendingLevel;

    /// <summary>当前离散等级（威胁 0-3 / 保护 0-1）</summary>
    public int Level => _level;

    /// <summary>
    /// 更新量化器。返回新 level。
    /// input = 上一帧 rawFactor（威胁）或 nearbyAllyCount（保护）。
    /// dt = ThinkInterval (0.1s)。
    /// </summary>
    public int Update(float input, float dt)
    {
        if (UpThresholds == null || UpThresholds.Length == 0) return _level;

        // 计算目标等级（无滞回基准）
        int target = ComputeTargetLevel(input);

        if (target > _level)
        {
            // 升级方向：需持续确认
            _upTimer += dt;
            _downTimer = 0f;
            _pendingLevel = target;
            if (_upTimer >= ConfirmUp)
            {
                _level = target;
                _upTimer = 0f;
                _downTimer = 0f;
            }
        }
        else if (target < _level)
        {
            // 降级方向：需持续确认（更慢）
            _downTimer += dt;
            _upTimer = 0f;
            _pendingLevel = target;
            if (_downTimer >= ConfirmDown)
            {
                _level = target;
                _upTimer = 0f;
                _downTimer = 0f;
            }
        }
        else
        {
            // 目标与当前相同，重置计时器
            _upTimer = 0f;
            _downTimer = 0f;
        }

        return _level;
    }

    /// <summary>
    /// 基于输入计算目标等级（无滞回基准）。
    /// 升阈数组长度 N -> 最大等级 N（如威胁 3 对阈值 -> 0-3 共 4 级）。
    /// input < DownThresholds[0] -> 0；DownThresholds[i] <= input < UpThresholds[i] -> 滞回带保持；
    /// input >= UpThresholds[i] -> 至少 i+1 级。
    /// </summary>
    private int ComputeTargetLevel(float input)
    {
        int n = UpThresholds.Length;
        // 从高到低找第一个满足的升阈
        int target = 0;
        for (int i = 0; i < n; i++)
        {
            if (input >= UpThresholds[i])
                target = i + 1;
            else
                break;
        }

        // 滞回带：若 input 在 [DownThresholds[level-1], UpThresholds[level-1]) 内，保持当前级
        // 即若当前级 > target 且 input 仍 >= 当前级对应的降阈，则不降
        if (_level > target && DownThresholds != null && _level - 1 < DownThresholds.Length)
        {
            float downThreshold = DownThresholds[_level - 1];
            if (input >= downThreshold)
                return _level;  // 仍在滞回带内，保持当前级
        }

        return target;
    }

    /// <summary>重置（对象池复用时调）</summary>
    public void Reset()
    {
        _level = 0;
        _upTimer = 0f;
        _downTimer = 0f;
        _pendingLevel = 0;
    }
}
