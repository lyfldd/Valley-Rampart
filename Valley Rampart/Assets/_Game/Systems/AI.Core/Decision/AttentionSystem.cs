// ============================================================================
//  AI.Core Decision - 五层注意力系统（从壳 AttentionSystem.cs 迁入）
//  03_大脑提取与双适配工程.md 迁移 6 步·步4。
//  接缝替换：
//    接缝 3：GridSystem.Instance.Config.cellSize 单例直取 -> IWorldQuery 注入（SetWorldQuery）；
//    接缝 4：SetConfig(AttentionTuningConfig) -> SetConfig(TuningSnapshot)；
//    Vector2 -> Vector2X；Mathf -> MathfX。
//  ⚠️ GetTopStimuliForDebug 迁出核（StimulusDebugInfo 属 AIDebug 壳，见壳
//     AttentionSystemDebugExtensions.cs），核保持零 AIDebug 依赖。
// ============================================================================

using System.Collections.Generic;

/// <summary>
/// 机制甲·五层注意力系统（3.0.1 第三节）。
///
/// 职责：
///   - 维护各层刺激源列表
///   - 持续评分排序，维护强度排行榜
///   - 跨层层压制：高层任何活跃项 > 低层所有项
///   - 输出第一名作为候选焦点
///   - 仅当第一名变化时才通知机制乙
///
/// 每层内部按 intensity 排序，层间按 AttentionLayer 枚举顺序压制。
/// </summary>
public class AttentionSystem
{
    private readonly List<ThreatStimulus> _threatStimuli = new List<ThreatStimulus>();
    private readonly List<TaskStimulus> _taskStimuli = new List<TaskStimulus>();
    private readonly List<PerceptionStimulus> _perceptionStimuli = new List<PerceptionStimulus>();

    // 3.0.1_2 新增动态刺激源（class，零装箱）：Safety/Follow/HoldPosition
    private readonly List<SafetyStimulus> _safetyStimuli = new List<SafetyStimulus>();
    private readonly List<FollowStimulus> _followStimuli = new List<FollowStimulus>();
    private readonly List<HoldPositionStimulus> _holdStimuli = new List<HoldPositionStimulus>();

    // 3.0.1_4 漫游刺激源（class，零装箱）
    private readonly List<WanderStimulus> _wanderStimuli = new List<WanderStimulus>();

    // ===== 3.0.1_4 破阵博弈（§4.3，NPCBrain 每 tick 注入职业参数）=====
    private int _breakCourage = 50;
    private int _breakObedience = 50;
    private bool _isBreaking;              // 当前是否破阵（滞回状态）
    private float _breakUpTimer;
    private float _breakDownTimer;

    /// <summary>全局调参快照（破阵阈值/漫游等用，NPCBrain Init 时设置；接缝 4）</summary>
    private TuningSnapshot _config;

    /// <summary>世界查询端口（接缝 3：槽位化跟随算 cellSize；壳传 UnityWorldQueryAdapter）</summary>
    private IWorldQuery _worldQuery;

    /// <summary>设置全局调参快照（NPCBrain Init 时调；原吃 SO，现吃快照）</summary>
    public void SetConfig(TuningSnapshot config) => _config = config;

    /// <summary>注入世界查询（接缝 3：GridSystem 单例 -> IWorldQuery）</summary>
    public void SetWorldQuery(IWorldQuery worldQuery) => _worldQuery = worldQuery;

    private Focus _currentFocus;
    private bool _focusChanged;
    /// <summary>当前选中的刺激源实例（3.0.1_2：供 L1FocusEvaluator 取 IStimulus，Focus.Source 存的是业务引用非刺激源本身）</summary>
    private IStimulus _currentStimulus;

    /// <summary>当前焦点（排行榜第一名）</summary>
    public Focus CurrentFocus => _currentFocus;

    /// <summary>当前选中的刺激源实例（ThreatStimulus/TaskStimulus/SafetyStimulus 等）</summary>
    public IStimulus CurrentStimulus => _currentStimulus;

    /// <summary>本轮更新焦点是否变化（机制乙据此决定切换判断 or 强度调节）</summary>
    public bool FocusChanged => _focusChanged;

    /// <summary>是否处于破阵状态（威胁层压制编队，调试用）</summary>
    public bool IsBreaking => _isBreaking;

    // ===== 添加刺激源 =====

    public void AddStimulus(ThreatStimulus s) => _threatStimuli.Add(s);
    public void AddStimulus(TaskStimulus s) => _taskStimuli.Add(s);
    public void AddStimulus(PerceptionStimulus s) => _perceptionStimuli.Add(s);

    // 3.0.1_2 动态刺激源（class）重载
    public void AddStimulus(SafetyStimulus s) => _safetyStimuli.Add(s);
    public void AddStimulus(FollowStimulus s) => _followStimuli.Add(s);
    public void AddStimulus(HoldPositionStimulus s) => _holdStimuli.Add(s);
    // 3.0.1_4 漫游刺激源（class）重载
    public void AddStimulus(WanderStimulus s) => _wanderStimuli.Add(s);

    /// <summary>
    /// 按 IStimulus 运行时类型分发到对应列表（记忆组件 GetActiveStimuli 返回 IStimulus 接口用）。
    /// 仅支持 3.0.1_2 新增 class 刺激源；struct 刺激源走各自强类型重载。
    /// </summary>
    public void AddDynamicStimulus(IStimulus s)
    {
        if (s is SafetyStimulus safety) _safetyStimuli.Add(safety);
        else if (s is FollowStimulus follow) _followStimuli.Add(follow);
        else if (s is HoldPositionStimulus hold) _holdStimuli.Add(hold);
        else if (s is WanderStimulus wander) _wanderStimuli.Add(wander);
    }

    /// <summary>清空动态刺激源（Safety/Follow/HoldPosition/Wander，每 tick 重注前调）</summary>
    public void ClearDynamicStimuli()
    {
        _safetyStimuli.Clear();
        _followStimuli.Clear();
        _holdStimuli.Clear();
        _wanderStimuli.Clear();
    }

    // ===== 移除刺激源 =====

    /// <summary>移除指定来源的所有威胁刺激源（敌人死亡/离开时调）。</summary>
    public void RemoveThreatStimuli(object source)
    {
        if (source == null) return;
        for (int i = _threatStimuli.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_threatStimuli[i].Source, source) ||
                ReferenceEquals(_threatStimuli[i].Enemy, source))
            {
                _threatStimuli.RemoveAt(i);
            }
        }
    }

    /// <summary>移除指定来源的所有任务刺激源。</summary>
    public void RemoveTaskStimuli(object source)
    {
        if (source == null) return;
        for (int i = _taskStimuli.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_taskStimuli[i].Source, source))
            {
                _taskStimuli.RemoveAt(i);
            }
        }
    }

    /// <summary>清空所有威胁刺激源。</summary>
    public void ClearThreats() => _threatStimuli.Clear();

    /// <summary>清空所有刺激源。</summary>
    public void ClearAll()
    {
        _threatStimuli.Clear();
        _taskStimuli.Clear();
        _perceptionStimuli.Clear();
        ClearDynamicStimuli();
        _currentFocus = Focus.Invalid;
    }

    // ===== 更新（持续评分）=====

    /// <summary>
    /// 更新注意力系统：清理过期刺激源，重新评分排序，输出焦点。
    /// 由 NPCBrain 每隔思考间隔调用（dt=ThinkInterval 0.1s，滞回计时用思考节奏而非渲染帧率）。
    /// </summary>
    public void Update(float currentTime, float dt)
    {
        // 1. 清理过期刺激源
        RemoveExpired(_threatStimuli, currentTime);
        RemoveExpired(_taskStimuli, currentTime);
        RemoveExpired(_perceptionStimuli, currentTime);

        // 2. 评分排序，选出焦点
        Focus newFocus = SelectTopFocus(dt);

        // 3. 检测焦点是否变化
        _focusChanged = !FocusEquals(_currentFocus, newFocus);
        _currentFocus = newFocus;
    }

    /// <summary>
    /// 选出排行榜第一名（跨层层压制）。
    /// 3.0.1_2 扩展：任务层纳入 Safety/Follow/HoldPosition 动态刺激源竞争，
    /// 输出 Focus 含 FocusType/TargetPos/Score（扩展构造）。
    /// stateTaskDiscount（Caution 态任务折扣）由 NPCBrain 在 Update 前设置，本方法读取应用。
    /// </summary>
    private Focus SelectTopFocus(float dt)
    {
        _currentStimulus = null;  // 重置

        // 第 1 层：威胁（最高优先）
        // 3.0.1_4 §4.3 破阵博弈（替代 3.0.1_3 的威胁≤1级硬开关）：
        // 编队军令（FollowStimulus.IsFormationSlot）不再按等级一刀切压制，
        // 改为"威胁压力 vs 编队约束"连续博弈 + 双阈值滞回：
        //   breakScore > 升阈(1.0) 持续确认 -> 破阵（威胁层胜出）
        //   breakScore < 降阈(0.7) 持续确认 -> 归队（编队压制）
        //   带内保持当前状态，防焦点横跳。
        if (_threatStimuli.Count > 0)
        {
            // 检查是否有活跃的编队军令
            bool hasFormationSlot = false;
            float formationIntensity = 0f;
            for (int i = 0; i < _followStimuli.Count; i++)
            {
                if (_followStimuli[i].IsFormationSlot)
                {
                    hasFormationSlot = true;
                    formationIntensity = _followStimuli[i].Intensity;
                    break;
                }
            }

            var top = GetTopThreat();
            int threatLvl = top.ThreatLevel;

            // 编队军令存在时：破阵博弈（双阈值滞回），未破阵则编队优先（走任务层）
            if (hasFormationSlot)
            {
                float breakScore = ComputeBreakScore(top, threatLvl, formationIntensity);
                UpdateBreakHysteresis(breakScore, dt);

                if (!_isBreaking)
                {
                    Focus taskFocus = SelectTopTaskLayer();
                    if (taskFocus.IsValid)
                        return taskFocus;
                }
                // 破阵中：fall through 到威胁层胜出
            }

            // 非编队 -> 威胁层胜出（原逻辑）
            _currentStimulus = top;
            return new Focus(AttentionLayer.Threat, top.Position, top.Intensity, top.Source,
                             top.FocusType, top.Position, top.Intensity);
        }

        // 威胁层为空：破阵状态立即归队（无威胁可破阵，滞回计时清零）
        if (_isBreaking || _breakUpTimer > 0f || _breakDownTimer > 0f)
        {
            _isBreaking = false;
            _breakUpTimer = 0f;
            _breakDownTimer = 0f;
        }

        // 第 2 层：仇恨（首版留壳，无刺激源）

        // 第 3 层：任务（含 TaskStimulus + Safety/Follow/HoldPosition 动态刺激源）
        Focus taskFocus2 = SelectTopTaskLayer();
        if (taskFocus2.IsValid)
            return taskFocus2;

        // 第 4 层：感知
        if (_perceptionStimuli.Count > 0)
        {
            var top = GetTopPerception();
            _currentStimulus = top;
            return new Focus(AttentionLayer.Perception, top.Position, top.Intensity, top.Source,
                             top.FocusType, top.Position, top.Intensity);
        }

        // 第 5 层：好奇（首版留壳，无刺激源）

        return Focus.Invalid;
    }

    /// <summary>
    /// 任务层选焦点：TaskStimulus / SafetyStimulus / FollowStimulus / HoldPositionStimulus 竞争。
    /// 应用 stateTaskDiscount（Caution 态对 TaskStimulus 打折，让 HoldPosition 胜出）。
    /// </summary>
    private Focus SelectTopTaskLayer()
    {
        float bestIntensity = -1f;
        Focus best = Focus.Invalid;
        IStimulus bestStimulus = null;

        // TaskStimulus（struct，应用 stateTaskDiscount）
        for (int i = 0; i < _taskStimuli.Count; i++)
        {
            float eff = _taskStimuli[i].Intensity * _taskDiscount;
            if (eff > bestIntensity)
            {
                bestIntensity = eff;
                var ts = _taskStimuli[i];
                best = new Focus(AttentionLayer.Task, ts.Position, ts.Intensity, ts.Source,
                                 ts.FocusType, ts.TargetPos, ts.Intensity);
                bestStimulus = ts;  // struct 装箱
            }
        }

        // SafetyStimulus（class，不打折--归巢是兜底非任务执行）
        for (int i = 0; i < _safetyStimuli.Count; i++)
        {
            if (_safetyStimuli[i].Intensity > bestIntensity)
            {
                bestIntensity = _safetyStimuli[i].Intensity;
                var ss = _safetyStimuli[i];
                best = new Focus(AttentionLayer.Task, ss.Position, ss.Intensity, ss.Source,
                                 ss.FocusType, ss.Position, ss.Intensity);
                bestStimulus = ss;  // class 不装箱
            }
        }

        // FollowStimulus（class，不打折--跟随是持续行为）
        for (int i = 0; i < _followStimuli.Count; i++)
        {
            if (_followStimuli[i].Intensity > bestIntensity)
            {
                bestIntensity = _followStimuli[i].Intensity;
                var fs = _followStimuli[i];
                // 3.0.1_3：槽位化跟随时 TargetPos = 锚点位置 + SlotOffset × cellSize（cell 吸附）
                // 非编队跟随 SlotOffset=zero，TargetPos=锚点位置（原语义）
                // 接缝 3：cellSize 由 IWorldQuery 注入（原 GridSystem.Instance 单例直取）
                Vector2X followTarget = fs.Position;
                if (fs.IsFormationSlot && _worldQuery != null)
                {
                    float cs = _worldQuery.CellSize;
                    followTarget = fs.Position + new Vector2X(fs.SlotOffset.x * cs, fs.SlotOffset.y * cs);
                }
                best = new Focus(AttentionLayer.Task, fs.Position, fs.Intensity, fs.Source,
                                 fs.FocusType, followTarget, fs.Intensity);
                bestStimulus = fs;
            }
        }

        // HoldPositionStimulus（class，不打折--驻留是 Caution 态要胜出的）
        for (int i = 0; i < _holdStimuli.Count; i++)
        {
            if (_holdStimuli[i].Intensity > bestIntensity)
            {
                bestIntensity = _holdStimuli[i].Intensity;
                var hs = _holdStimuli[i];
                best = new Focus(AttentionLayer.Task, hs.Position, hs.Intensity, hs.Source,
                                 hs.FocusType, hs.Position, hs.Intensity);
                bestStimulus = hs;
            }
        }

        // WanderStimulus（class，3.0.1_4 §6.3，不打折--最低优先级漫游兜底）
        for (int i = 0; i < _wanderStimuli.Count; i++)
        {
            if (_wanderStimuli[i].Intensity > bestIntensity)
            {
                bestIntensity = _wanderStimuli[i].Intensity;
                var ws = _wanderStimuli[i];
                best = new Focus(AttentionLayer.Task, ws.Position, ws.Intensity, ws.Source,
                                 ws.FocusType, ws.Position, ws.Intensity);
                bestStimulus = ws;
            }
        }

        _currentStimulus = bestStimulus;
        return best;
    }

    /// <summary>任务折扣（Caution 态由 NPCBrain 设置，1f=不打折）</summary>
    private float _taskDiscount = 1f;

    /// <summary>设置任务折扣（NPCBrain 在 Update 前调，Caution 态传 stateTaskDiscount）</summary>
    public void SetTaskDiscount(float discount) => _taskDiscount = discount;

    // ===== 3.0.1_4 破阵博弈（§4.3）=====

    /// <summary>注入破阵博弈职业参数（NPCBrain 每 tick 在 Update 前调）</summary>
    public void SetBreakContext(int courage, int obedience)
    {
        _breakCourage = courage;
        _breakObedience = obedience;
    }

    /// <summary>
    /// 破阵评分（§4.3 归一化公式）：
    /// threatPressure = (Intensity/100) × (1 + threatLevel×levelWeight) × (0.5 + courage/100)
    /// formationHold  = max(0.1, formationIntensity/4.5) × (0.5 + obedience/100)
    /// breakScore = threatPressure / formationHold
    /// </summary>
    private float ComputeBreakScore(in ThreatStimulus top, int threatLvl, float formationIntensity)
    {
        float levelWeight = _config.breakLevelWeight;
        float formationBase = _config.formationOrderIntensity;

        float threatPressure = (top.Intensity / _config.threatIntensityMax)
                               * (1f + threatLvl * levelWeight)
                               * (0.5f + _breakCourage / 100f);
        float formationHold = MathfX.Max(0.1f, formationIntensity / formationBase)
                              * (0.5f + _breakObedience / 100f);
        return threatPressure / MathfX.Max(0.1f, formationHold);
    }

    /// <summary>
    /// 破阵双阈值滞回（§4.3 细节2）：升阈 breakThreshold / 降阈 breakReleaseThreshold，
    /// 带内保持当前状态 + 升级/降级持续确认，防阈值附近焦点横跳。
    /// dt = ThinkInterval（思考节奏，不依赖渲染帧率，可测试）。
    /// </summary>
    private void UpdateBreakHysteresis(float breakScore, float dt)
    {
        float upThreshold = _config.breakThreshold;
        float downThreshold = _config.breakReleaseThreshold;
        float confirmUp = _config.breakConfirmUp;
        float confirmDown = _config.breakConfirmDown;

        if (_isBreaking)
        {
            // 破阵中：持续低于降阈 -> 归队
            if (breakScore < downThreshold)
            {
                _breakDownTimer += dt;
                if (_breakDownTimer >= confirmDown)
                {
                    _isBreaking = false;
                    _breakDownTimer = 0f;
                }
            }
            else
            {
                _breakDownTimer = 0f;
            }
        }
        else
        {
            // 守位中：持续超过升阈 -> 破阵
            if (breakScore > upThreshold)
            {
                _breakUpTimer += dt;
                if (_breakUpTimer >= confirmUp)
                {
                    _isBreaking = true;
                    _breakUpTimer = 0f;
                }
            }
            else
            {
                _breakUpTimer = 0f;
            }
        }
    }

    private ThreatStimulus GetTopThreat()
    {
        int best = 0;
        for (int i = 1; i < _threatStimuli.Count; i++)
        {
            if (_threatStimuli[i].Intensity > _threatStimuli[best].Intensity)
                best = i;
        }
        return _threatStimuli[best];
    }

    private TaskStimulus GetTopTask()
    {
        int best = 0;
        for (int i = 1; i < _taskStimuli.Count; i++)
        {
            if (_taskStimuli[i].Intensity > _taskStimuli[best].Intensity)
                best = i;
        }
        return _taskStimuli[best];
    }

    private PerceptionStimulus GetTopPerception()
    {
        int best = 0;
        for (int i = 1; i < _perceptionStimuli.Count; i++)
        {
            if (_perceptionStimuli[i].Intensity > _perceptionStimuli[best].Intensity)
                best = i;
        }
        return _perceptionStimuli[best];
    }

    /// <summary>清理过期刺激源。</summary>
    private void RemoveExpired<T>(List<T> list, float currentTime) where T : IStimulus
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].Expiry > 0 && list[i].Expiry < currentTime)
            {
                list.RemoveAt(i);
            }
        }
    }

    /// <summary>比较两个焦点是否相同（同层 + 同来源）。</summary>
    private bool FocusEquals(Focus a, Focus b)
    {
        if (!a.IsValid && !b.IsValid) return true;
        if (a.IsValid != b.IsValid) return false;
        return a.Layer == b.Layer && ReferenceEquals(a.Source, b.Source);
    }

    // ===== 调试用 =====

    /// <summary>各层刺激源数量（调试用）。</summary>
    public int ThreatCount => _threatStimuli.Count;
    public int TaskCount => _taskStimuli.Count;
    public int PerceptionCount => _perceptionStimuli.Count;

    /// <summary>获取威胁层排行榜前 N 名（调试用）。</summary>
    public void GetTopThreats(List<ThreatStimulus> output, int maxCount)
    {
        output.Clear();
        // 简单复制后排序（调试用，不追求性能）
        var sorted = new List<ThreatStimulus>(_threatStimuli);
        sorted.Sort((a, b) => b.Intensity.CompareTo(a.Intensity));
        for (int i = 0; i < MathfX.Min(maxCount, sorted.Count); i++)
            output.Add(sorted[i]);
    }
}
