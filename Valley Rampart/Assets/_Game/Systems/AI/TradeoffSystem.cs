using UnityEngine;

/// <summary>
/// 机制乙·三维权衡系统（3.0.1 第四节）。
///
/// 两作用（4.1）：
///   - 强度调节：候选焦点 == 当前焦点 -> 按威胁×优先级×服从度算谱系等级
///   - 切换判断：候选焦点 != 当前焦点 -> 算强度差 + 阈值判断
///
/// 谱系映射（5.2）：
///   威胁 0 -> 谱系 0（全力）
///   威胁 1 -> 谱系 0（全力，注意力提权威胁层）
///   威胁 2 + 有保护 -> 谱系 2（谨慎）
///   威胁 2 + 无保护 -> 谱系 4（撤退）
///   威胁 3 -> 谱系 4（撤退）
///
/// 撤退阈值公式（4.2）：
///   阈值 = 基础 + 任务优先级加成 + 勇气加成 + 服从度加成 + 职业偏移
///   威胁等级 > 阈值 -> 撤退；威胁等级 ≤ 阈值 -> 按谱系映射
///
/// 防抖（6.4）：
///   - 状态侧驻留：进入谨慎/撤退后最小停留 N 秒
///   - 撤退安全确认：跑到威胁 0 区后至少待 N 秒才重新评估任务
/// </summary>
public class TradeoffSystem
{
    /// <summary>当前谱系</summary>
    public BehaviorSpectrum CurrentSpectrum { get; private set; } = BehaviorSpectrum.FullPower;

    /// <summary>已提交的焦点（NPC 实际在执行的）</summary>
    public Focus CommittedFocus { get; private set; }

    /// <summary>是否在撤退安全确认中（不能接受新任务）</summary>
    public bool InSafetyConfirmation { get; private set; }

    private float _spectrumEnterTime;
    private float _retreatSafeTimer;
    private bool _wasRetreating;

    /// <summary>
    /// 更新权衡系统。
    /// 由 NPCBrain 每思考间隔调用。
    /// </summary>
    /// <param name="candidateFocus">注意力系统输出的候选焦点</param>
    /// <param name="focusChanged">候选焦点是否变化</param>
    /// <param name="threat">威胁评定结果</param>
    /// <param name="currentTaskPriority">当前任务优先级（无任务时 null）</param>
    /// <param name="profession">职业配置</param>
    /// <param name="config">全局调参</param>
    /// <param name="currentTime">当前时间</param>
    public void Update(
        Focus candidateFocus,
        bool focusChanged,
        ThreatAssessmentResult threat,
        TaskPriority? currentTaskPriority,
        NpcProfessionDef profession,
        AttentionTuningConfig config,
        float currentTime)
    {
        // === 1. 切换判断（候选焦点 != 当前焦点） ===
        if (focusChanged)
        {
            bool shouldSwitch = EvaluateSwitch(candidateFocus);
            if (shouldSwitch)
            {
                CommittedFocus = candidateFocus;
            }
            // 不切换时保持原 CommittedFocus
        }

        // === 2. 谱系计算 ===
        BehaviorSpectrum targetSpectrum = CalculateSpectrum(threat, currentTaskPriority, profession, config);

        // === 3. 状态侧驻留防抖 ===
        if (targetSpectrum != CurrentSpectrum)
        {
            float dwellTime = GetMinDwellTime(CurrentSpectrum, config);
            if (currentTime - _spectrumEnterTime >= dwellTime)
            {
                CurrentSpectrum = targetSpectrum;
                _spectrumEnterTime = currentTime;
            }
        }

        // === 4. 撤退安全确认 ===
        UpdateSafetyConfirmation(threat, config, currentTime);
    }

    /// <summary>
    /// 切换判断：是否接受新焦点。
    /// 高层 > 低层（跨层压制），同层需强度差超过阈值。
    /// </summary>
    private bool EvaluateSwitch(Focus newFocus)
    {
        // 无当前焦点 -> 接受任何
        if (!CommittedFocus.IsValid)
            return newFocus.IsValid;

        // 新焦点无效 -> 保持当前
        if (!newFocus.IsValid)
            return false;

        // 撤退安全确认中 -> 不接受任务层以下的新焦点
        if (InSafetyConfirmation && newFocus.Layer > AttentionLayer.Hate)
            return false;

        // 跨层：低枚举值 = 高优先层
        if (newFocus.Layer < CommittedFocus.Layer)
            return true;   // 新焦点更高层 -> 切换
        if (newFocus.Layer > CommittedFocus.Layer)
            return false;  // 新焦点更低层 -> 不切换

        // 同层：强度差超过阈值才切换
        float diff = newFocus.Intensity - CommittedFocus.Intensity;
        return diff > 1f;
    }

    /// <summary>
    /// 谱系计算：威胁等级 + 保护因子 + 撤退阈值 -> 谱系 0/2/4。
    /// </summary>
    private BehaviorSpectrum CalculateSpectrum(
        ThreatAssessmentResult threat,
        TaskPriority? taskPriority,
        NpcProfessionDef profession,
        AttentionTuningConfig config)
    {
        // 计算撤退阈值
        float threshold = config.retreatThresholdBase;
        if (taskPriority.HasValue)
            threshold += config.GetRetreatBonus(taskPriority.Value);
        threshold += (profession.courage - 50f) / 50f;       // 勇气加成 -1~+1
        threshold += (profession.obedience - 50f) / 100f;    // 服从度加成 -0.5~+0.5
        threshold += profession.retreatThresholdOffset;       // 职业偏移

        float threatValue = (float)threat.Level;

        // 威胁 0/1 -> 全力执行
        if (threat.Level <= ThreatLevel.Alert)
            return BehaviorSpectrum.FullPower;

        // 威胁 2（危险）
        if (threat.Level == ThreatLevel.Danger)
        {
            // 威胁超过阈值 -> 撤退
            if (threatValue > threshold)
                return BehaviorSpectrum.FullRetreat;
            // 有保护 -> 谨慎
            if (threat.HasProtection)
                return BehaviorSpectrum.Cautious;
            // 无保护 -> 撤退
            return BehaviorSpectrum.FullRetreat;
        }

        // 威胁 3（致命）
        if (threatValue <= threshold)
            return BehaviorSpectrum.Cautious;  // 高阈值职业（S级军令）即使致命也扛住
        return BehaviorSpectrum.FullRetreat;
    }

    /// <summary>获取谱系最小驻留时间。</summary>
    private float GetMinDwellTime(BehaviorSpectrum spectrum, AttentionTuningConfig config)
    {
        switch (spectrum)
        {
            case BehaviorSpectrum.Cautious: return config.cautiousMinDwell;
            case BehaviorSpectrum.FullRetreat: return config.retreatMinDwell;
            default: return 0f;
        }
    }

    /// <summary>撤退安全确认更新。</summary>
    private void UpdateSafetyConfirmation(ThreatAssessmentResult threat, AttentionTuningConfig config, float currentTime)
    {
        if (CurrentSpectrum == BehaviorSpectrum.FullRetreat)
        {
            _wasRetreating = true;

            // 到达安全区（威胁 0）
            if (threat.Level == ThreatLevel.None)
            {
                _retreatSafeTimer += Time.deltaTime;
                if (_retreatSafeTimer >= config.safetyConfirmTime)
                {
                    InSafetyConfirmation = false;
                }
                else
                {
                    InSafetyConfirmation = true;  // 还在确认中
                }
            }
            else
            {
                _retreatSafeTimer = 0f;
                InSafetyConfirmation = true;  // 还在撤退中
            }
        }
        else
        {
            // 不在撤退态
            if (_wasRetreating && _retreatSafeTimer < config.safetyConfirmTime)
            {
                // 刚离开撤退态但安全确认未完成 -> 保持确认
                InSafetyConfirmation = true;
            }
            else
            {
                _wasRetreating = false;
                _retreatSafeTimer = 0f;
                InSafetyConfirmation = false;
            }
        }
    }

    /// <summary>重置状态（对象池复用时调）。</summary>
    public void Reset()
    {
        CurrentSpectrum = BehaviorSpectrum.FullPower;
        CommittedFocus = Focus.Invalid;
        InSafetyConfirmation = false;
        _spectrumEnterTime = 0f;
        _retreatSafeTimer = 0f;
        _wasRetreating = false;
    }
}
