using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 威胁评定结果（3.0.1 第五节）。
/// 包含威胁等级 + 保护因子 + 调试用数据。
/// </summary>
public struct ThreatAssessmentResult
{
    /// <summary>最终威胁等级 0-3</summary>
    public ThreatLevel Level;
    /// <summary>原始威胁因子 X（0-1），调试用</summary>
    public float RawFactor;
    /// <summary>是否有友军保护（≥protectionFriendThreshold 个友军在附近）</summary>
    public bool HasProtection;
    /// <summary>附近敌人数</summary>
    public int NearbyEnemyCount;
    /// <summary>附近友军数</summary>
    public int NearbyAllyCount;
}

/// <summary>
/// 威胁评定函数（3.0.1 第五节）。
/// 多因子评定子系统，机制乙的核心计算。
///
/// 因子清单（首版）：
///   - 敌人因子：最近敌人距离 / 视野内敌人数量
///   - 友军因子：附近友军数量（负向抵消）
///   - 自身因子：血量比例
///   - 时间因子：昼夜
///   - 地形因子：首版跳过（验证场景无城墙）
///
/// 带滞回的累积判定（6.4 输入侧滞回）：
///   升级快（0.3s 确认）、降级慢（0.5s 确认），中间滞回带保持当前等级。
/// </summary>
public class ThreatAssessor
{
    private ThreatLevel _currentLevel = ThreatLevel.None;
    private float _upgradeTimer;
    private float _downgradeTimer;
    private float _lastDamagedTime = -999f;
    private ThreatLevel _pendingLevel;

    /// <summary>当前威胁等级</summary>
    public ThreatLevel CurrentLevel => _currentLevel;

    /// <summary>标记自身被攻击（事件驱动，触发威胁 3）。</summary>
    public void OnDamaged(float currentTime)
    {
        _lastDamagedTime = currentTime;
    }

    /// <summary>
    /// 更新威胁评定。
    /// 输入当前环境数据，输出带滞回的威胁等级。
    /// </summary>
    public ThreatAssessmentResult Update(
        float rawFactor,
        int nearbyEnemyCount,
        int nearbyAllyCount,
        float hpRatio,
        bool isNight,
        AttentionTuningConfig config,
        NpcProfessionDef profession,
        float currentTime)
    {
        // === 保护因子 ===
        bool hasProtection = nearbyAllyCount >= config.protectionFriendThreshold;

        // === 事件驱动的威胁 3 覆盖 ===
        // 自身被攻击 / 血量过低 -> 威胁 3（致命）
        float timeSinceDamage = currentTime - _lastDamagedTime;
        bool recentlyDamaged = timeSinceDamage < config.threatDecayTime;
        bool lowHp = hpRatio < 0.3f;

        // === 计算目标威胁等级 ===
        ThreatLevel targetLevel;

        if (nearbyEnemyCount == 0 && !recentlyDamaged)
        {
            // 无敌人且未被攻击 -> 威胁 0
            targetLevel = ThreatLevel.None;
        }
        else if (recentlyDamaged || lowHp)
        {
            // 自身被攻击或血量过低 -> 威胁 3（致命）
            targetLevel = ThreatLevel.Lethal;
        }
        else
        {
            // 敌人数量碾压 -> 威胁 3
            if (nearbyEnemyCount >= 5)
            {
                targetLevel = ThreatLevel.Lethal;
            }
            else
            {
                // 基于 rawFactor 的等级映射（带滞回）
                targetLevel = DetermineLevelWithHysteresis(rawFactor, config);
            }
        }

        // === 滞回确认 ===
        // 威胁 3 的事件驱动覆盖是即时的，不走滞回
        if (targetLevel == ThreatLevel.Lethal &&
            (recentlyDamaged || lowHp || nearbyEnemyCount >= 5))
        {
            _currentLevel = ThreatLevel.Lethal;
            _upgradeTimer = 0f;
            _downgradeTimer = 0f;
        }
        else
        {
            ApplyHysteresis(targetLevel, config, currentTime);
        }

        // 威胁衰减：未被攻击且无敌人时，从 3 逐步降回
        if (_currentLevel == ThreatLevel.Lethal && !recentlyDamaged && !lowHp && nearbyEnemyCount < 5)
        {
            // 事件驱动的 3 已过期，降回基于因子的等级
            _currentLevel = DetermineLevelWithHysteresis(rawFactor, config);
        }

        return new ThreatAssessmentResult
        {
            Level = _currentLevel,
            RawFactor = rawFactor,
            HasProtection = hasProtection,
            NearbyEnemyCount = nearbyEnemyCount,
            NearbyAllyCount = nearbyAllyCount
        };
    }

    /// <summary>
    /// 基于原始因子 X 和滞回判定威胁等级。
    /// 滞回带：upgradeThreshold 和 downgradeThreshold 之间保持当前等级。
    /// </summary>
    private ThreatLevel DetermineLevelWithHysteresis(float x, AttentionTuningConfig config)
    {
        // 滞回带中间区域：保持当前等级
        float upgrade = config.threatUpgradeThreshold;
        float downgrade = config.threatDowngradeThreshold;

        // X 映射到等级（无滞回的基准）
        ThreatLevel baseLevel;
        if (x < downgrade)
            baseLevel = ThreatLevel.None;
        else if (x < upgrade)
            baseLevel = ThreatLevel.Alert;
        else if (x < 0.8f)
            baseLevel = ThreatLevel.Danger;
        else
            baseLevel = ThreatLevel.Lethal;

        return baseLevel;
    }

    /// <summary>
    /// 应用滞回确认（升级需持续确认，降级需持续确认）。
    /// </summary>
    private void ApplyHysteresis(ThreatLevel target, AttentionTuningConfig config, float currentTime)
    {
        if (target > _currentLevel)
        {
            // 升级方向：需要持续确认
            _upgradeTimer += Time.deltaTime;
            _downgradeTimer = 0f;

            if (_upgradeTimer >= config.threatUpgradeConfirmTime)
            {
                _currentLevel = target;
                _upgradeTimer = 0f;
            }
        }
        else if (target < _currentLevel)
        {
            // 降级方向：需要持续确认（更慢）
            _downgradeTimer += Time.deltaTime;
            _upgradeTimer = 0f;

            if (_downgradeTimer >= config.threatDowngradeConfirmTime)
            {
                _currentLevel = target;
                _downgradeTimer = 0f;
            }
        }
        else
        {
            // 目标与当前相同，重置计时器
            _upgradeTimer = 0f;
            _downgradeTimer = 0f;
        }
    }

    /// <summary>计算原始威胁因子 X（0-1）。</summary>
    public static float CalculateRawFactor(
        float nearestEnemyDist,
        int enemyCount,
        float hpRatio,
        int allyCount,
        bool isNight,
        NpcProfessionDef profession,
        AttentionTuningConfig config,
        float perceptionWorldRadius,
        float attackWorldRange)
    {
        if (enemyCount == 0 || nearestEnemyDist >= perceptionWorldRadius)
            return 0f;

        // 敌人距离因子（越近越高，0-1）
        float distFactor = 1f - Mathf.Clamp01(nearestEnemyDist / perceptionWorldRadius);

        // 敌人数量因子（越多越高，0-1，5 个满）
        float countFactor = Mathf.Clamp01(enemyCount / 5f);

        // 血量因子（越低越高，0-1）
        float hpFactor = 1f - Mathf.Clamp01(hpRatio);

        // 友军保护因子（越多友军越低，0-1）
        float allyFactor = 1f - Mathf.Clamp01((float)allyCount / config.protectionFriendThreshold);

        // 时间因子（夜晚 +0.1）
        float timeFactor = isNight ? 0.1f : 0f;

        // 加权合成
        float x = distFactor * 0.35f
                + countFactor * 0.15f
                + hpFactor * 0.2f
                + allyFactor * 0.2f
                + timeFactor * 0.1f;

        // 应用职业敏感度
        x *= profession.threatSensitivity;

        return Mathf.Clamp01(x);
    }

    /// <summary>重置状态（对象池复用时调）。</summary>
    public void Reset()
    {
        _currentLevel = ThreatLevel.None;
        _upgradeTimer = 0f;
        _downgradeTimer = 0f;
        _lastDamagedTime = -999f;
    }
}
