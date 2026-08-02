// ============================================================================
//  M2 Headless 模拟器 - SimFormation 军队级编队控制器（复刻 FormationController）
//  04_模拟器规格.md §三 保真度契约：
//    - 槽位分配：FormationController.cs:277 近战外侧弓手内侧，方向×±1
//    - 军令强度：FormationController.cs:47-56 4.5 正常/6.0 瞬时/保底 1s（formationOrderBoostDuration）
//    - 减员即时触发 + 防抖 1s 重排（§15.3，casualtyDebounce=1）
//    - 阵型切换防抖 1s（§5.3，switchDebounce=1）+ 切阵型瞬时提强度（3.0.1_8 §七）
//    - 将军阵亡 -> 编队解散（§7.4）
//  v0：军队意图用剧本脚本驱动（场景 JSON intentScript：t=5s SetIntent(Charge)），
//  DecideAutoIntent 留空壳（v1 接核内 FormationDecisionCore.DecideIntent + SimHeat，04 §五）。
//  确定性：槽位排序用稳定序（Unity List.Sort 非稳定，sim 固定为稳定序——04 §七 确定性优先）。
// ============================================================================

using System.Collections.Generic;

/// <summary>编队成员运行时记录（对应壳 FormationMember；Role 用 profession.isRanged 区分近战/弓手）。</summary>
public sealed class SimFormationMember
{
    public SimUnit Unit;
    public Vector2IntX SlotOffset;
}

/// <summary>
/// 编队控制器（军队级，v0 剧本驱动）。
/// 输出物是刺激源（FollowStimulus 槽位军令），个体三层管线复用（"两类东西"纪律）。
/// </summary>
public sealed class SimFormation
{
    /// <summary>标准满编规模（FormationDef.StandardSize = 1 将军 + 3 近战 + 2 弓手）。</summary>
    public const int StandardSize = 6;

    public int Gid;
    public Faction Faction;
    public SimUnit GeneralUnit;                    // 将军（锚点）；无将军编队为 null
    public SimSlotData[] Slots;
    public int Direction = 1;                      // 阵型朝向：1=向右进攻（默认），-1=向左；AssignSlots offset.x *= dir
    public TacticIntent DefaultIntent = TacticIntent.Defense;

    private readonly TuningSnapshot _config;
    private readonly SimClock _clock;
    private readonly SimEventBus _events;
    private readonly SimHeat _heat;

    private readonly List<SimFormationMember> _members = new List<SimFormationMember>();
    private TacticIntent _currentIntent;
    private float _lastSwitchTime;
    private float _lastCasualtyTime;
    private bool _pendingReform;
    private float _boostUntil;                     // 切阵型瞬时提强度截止（3.0.1_8 §七）
    private float _royalUntil;                     // 君主令截止（3.0.1_8 §6.6）
    private SimIntentEventData[] _intentScript;    // v0 剧本脚本（场景 JSON）

    /// <summary>当前意图。</summary>
    public TacticIntent CurrentIntent => _currentIntent;

    /// <summary>成员数。</summary>
    public int MemberCount => _members.Count;

    /// <summary>成员列表（只读，tick 采样/指标用）。</summary>
    public System.Collections.Generic.IReadOnlyList<SimFormationMember> Members => _members;

    /// <summary>有效军令强度（boost 保底期内 6.0，否则 4.5；FormationController.cs:83-85）。</summary>
    public float EffectiveOrderIntensity => _clock.Now < _boostUntil
        ? _config.formationOrderBoost
        : _config.formationOrderIntensity;

    /// <summary>是否君主令生效期（3.0.1_8 §6.6）。</summary>
    public bool IsRoyalCommandActive => _clock.Now < _royalUntil;

    public SimFormation(int gid, Faction faction, SimUnit general, SimSlotData[] slots,
                        int direction, TacticIntent defaultIntent, SimIntentEventData[] intentScript,
                        TuningSnapshot config, SimClock clock, SimEventBus events, SimHeat heat)
    {
        Gid = gid;
        Faction = faction;
        GeneralUnit = general;
        Slots = slots;
        Direction = direction;
        DefaultIntent = defaultIntent;
        _intentScript = intentScript;
        _config = config;
        _clock = clock;
        _events = events;
        _heat = heat;
        _currentIntent = defaultIntent;            // 初始意图（不经过 SetIntent 防抖）
    }

    // ===== 成员管理 =====

    /// <summary>添加成员（SimWorld 装配时调；对应 FormationController.AddMember）。</summary>
    public void AddMember(SimUnit unit)
    {
        var member = new SimFormationMember { Unit = unit, SlotOffset = Vector2IntX.zero };
        _members.Add(member);
    }

    // ===== 阵型应用（查表 + 槽位分配 + 下发军令）=====

    public void ApplyFormation()
    {
        if (Slots == null || Slots.Length == 0) return;
        AssignSlots(Slots);
        DispatchOrders();
    }

    /// <summary>
    /// 槽位分配（复刻 FormationController.AssignSlots，§3.2 R2 残编紧凑）。
    /// 两轮填充：
    ///   第一轮：近战填 MeleeOnly/GeneralOnly/Any 槽，按 |x| 外侧优先（防残编弓手顶前排）
    ///   第二轮：弓手填剩余 RangedOnly/Any 槽，按距锚点 |x| 从近到远（残编弓优先填靠后安全位）
    /// 方向翻转：offset.x *= Direction（1=右/-1=左）
    /// ⚠️ 确定性：Unity 用 List.Sort 非稳定，sim 用带索引的稳定排序固定并列 |x| 槽位顺序。
    /// </summary>
    private void AssignSlots(SimSlotData[] slots)
    {
        var melee = new List<SimFormationMember>();
        var archer = new List<SimFormationMember>();
        foreach (var m in _members)
        {
            if (m.Unit == null || !m.Unit.IsAlive) continue;
            if (m.Unit.Profession.isRanged) archer.Add(m);
            else melee.Add(m);
        }

        bool[] occupied = new bool[slots.Length];
        int dir = Direction;

        // 第一轮：近战按 |x| 外侧优先填 MeleeOnly/GeneralOnly/Any 槽
        var meleeSlots = new List<int>();
        for (int i = 0; i < slots.Length; i++)
        {
            SlotRole role = slots[i].Role;
            if (role == SlotRole.MeleeOnly || role == SlotRole.GeneralOnly || role == SlotRole.Any)
                meleeSlots.Add(i);
        }
        meleeSlots.Sort(Comparer<int>.Create((a, b) =>
        {
            int cmp = MathfX.Abs(slots[b].X).CompareTo(MathfX.Abs(slots[a].X));   // |x| 降序
            return cmp != 0 ? cmp : a.CompareTo(b);                                // 同 |x| 保数组序（稳定）
        }));

        int meleeIdx = 0;
        foreach (int i in meleeSlots)
        {
            if (meleeIdx >= melee.Count) break;
            var m = melee[meleeIdx++];
            m.SlotOffset = new Vector2IntX(slots[i].X * dir, slots[i].Y);
            occupied[i] = true;
        }

        // 第二轮：弓手填剩余 RangedOnly/Any 槽，按距锚点 |x| 从近到远
        var archerSlots = new List<int>();
        for (int i = 0; i < slots.Length; i++)
        {
            if (occupied[i]) continue;
            SlotRole role = slots[i].Role;
            if (role == SlotRole.RangedOnly || role == SlotRole.Any)
                archerSlots.Add(i);
        }
        archerSlots.Sort(Comparer<int>.Create((a, b) =>
        {
            int cmp = MathfX.Abs(slots[a].X).CompareTo(MathfX.Abs(slots[b].X));   // |x| 升序
            return cmp != 0 ? cmp : a.CompareTo(b);
        }));

        int archerIdx = 0;
        foreach (int i in archerSlots)
        {
            if (archerIdx >= archer.Count) break;
            var m = archer[archerIdx++];
            m.SlotOffset = new Vector2IntX(slots[i].X * dir, slots[i].Y);
            occupied[i] = true;
        }
    }

    /// <summary>下发军令（对每个成员 SetFormationSlot；FormationController.DispatchOrders）。</summary>
    private void DispatchOrders()
    {
        // 锚点 = 将军 SimUnit；无将军（守城）用第一个成员占位（P0 简化，FormationController.cs:350-356）
        SimUnit anchorUnit = GeneralUnit;
        if (anchorUnit == null && _members.Count > 0)
            anchorUnit = _members[0].Unit;

        foreach (var m in _members)
        {
            if (m.Unit == null || !m.Unit.IsAlive || m.Unit.Brain == null) continue;
            m.Unit.Brain.SetFormationSlot(anchorUnit, TaskPriority.S, EffectiveOrderIntensity,
                                          m.SlotOffset, IsRoyalCommandActive);
        }
    }

    // ===== 阵型切换（§5.3 将军唯一决策权；v0 由剧本脚本触发）=====

    /// <summary>切换战术意图（复刻 FormationController.SetIntent：防抖 + 瞬时提强度）。</summary>
    public void SetIntent(TacticIntent intent)
    {
        if (intent == _currentIntent) return;
        if (_clock.Now - _lastSwitchTime < _config.formationSwitchDebounce) return;   // 防抖 1s

        _currentIntent = intent;
        _lastSwitchTime = _clock.Now;
        // 3.0.1_8 §七：切阵型瞬时提强度（保底期内军令 4.5→6.0）
        _boostUntil = _clock.Now + _config.formationOrderBoostDuration;
        ApplyFormation();
        PublishIntentEvent();
    }

    /// <summary>君主令（3.0.1_8 §6.6：切意图 + 军令带 royal 标记，个体永不弃任务）。</summary>
    public void SetRoyalIntent(TacticIntent intent, float duration)
    {
        _royalUntil = _clock.Now + MathfX.Max(0f, duration);
        SetIntent(intent);
    }

    /// <summary>剧本脚本推进（SimWorld 每 tick 调；04 §五 v0 军队意图剧本驱动）。</summary>
    public void AdvanceScript(float now)
    {
        if (_intentScript == null) return;
        for (int i = 0; i < _intentScript.Length; i++)
        {
            var evt = _intentScript[i];
            if (evt.T >= 0f && now >= evt.T && !_consumed[i])
            {
                _consumed[i] = true;
                SetIntent(evt.Intent);
            }
        }
    }

    private bool[] _consumed;

    /// <summary>初始化剧本脚本消费标记（SimWorld 装配后调）。</summary>
    public void InitScript()
    {
        _consumed = _intentScript != null ? new bool[_intentScript.Length] : null;
    }

    // ===== 自动意图（v1 空壳：04 §五 v0 剧本驱动 / v1 接 FormationDecisionCore.DecideIntent + SimHeat）=====

    /// <summary>自动意图决策（v1 实装：任务价值 + 意图自决；v0 剧本驱动，留空壳）。</summary>
    public void DecideAutoIntent()
    {
        // v1：heat = _heat.GetHeatAt(anchor.x)；survival = MemberCount / StandardSize；
        //     value = FormationDecisionCore.EvaluateTaskValue(...)；
        //     decision = FormationDecisionCore.DecideIntent(...)；decision.Valid -> SetIntent / SetAdvanceTarget。
    }

    // ===== 减员管理（§15，订阅 SimUnitDiedEvent）=====

    public void OnUnitDied(SimUnitDiedEvent evt)
    {
        // 将军阵亡 -> 锚点丢失 -> 编队解散（§7.4 / §15.1）
        if (GeneralUnit != null && ReferenceEquals(evt.Unit, GeneralUnit))
        {
            DisbandAll();
            return;
        }

        // 非将军成员死亡 -> 移出列表 + 防抖重排（§15.3）
        for (int i = _members.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_members[i].Unit, evt.Unit))
            {
                _members.RemoveAt(i);
                _lastCasualtyTime = _clock.Now;
                _pendingReform = true;
                break;
            }
        }
    }

    /// <summary>每 tick 调（SimWorld 第 7 步后）：防抖重排 + boost/royal 过期回落。</summary>
    public void Update()
    {
        // 减员防抖重排（§15.3）
        if (_pendingReform && _clock.Now - _lastCasualtyTime >= _config.formationCasualtyDebounce)
        {
            _pendingReform = false;
            if (_members.Count > 0)
                ApplyFormation();
        }

        // 3.0.1_8 §七：切阵型瞬时提强度过期 -> 回落正常军令强度并重发
        if (_boostUntil > 0f && _clock.Now >= _boostUntil && _members.Count > 0)
        {
            _boostUntil = 0f;
            DispatchOrders();
        }

        // 3.0.1_8 §6.6：君主令过期 -> 回落（重发军令清 royal 标记）
        if (_royalUntil > 0f && _clock.Now >= _royalUntil && _members.Count > 0)
        {
            _royalUntil = 0f;
            DispatchOrders();
        }
    }

    // ===== 解散与状态清理（§15.5 ClearFormationState）=====

    public void DisbandAll()
    {
        foreach (var m in _members)
        {
            if (m.Unit != null && m.Unit.Brain != null)
                m.Unit.Brain.ClearFollowAnchor();
        }
        _members.Clear();
    }

    // ===== 事件发布 =====

    private void PublishIntentEvent()
    {
        float anchorX = GeneralUnit != null ? GeneralUnit.Position.x : 0f;
        float heat = _heat.GetHeatAt(anchorX);
        // v0 剧本驱动：value 用核内任务价值函数预热（isGarrison=false / 无推进目标 / 巡逻基值）
        float survival = _members.Count / (float)StandardSize;
        float value = FormationDecisionCore.EvaluateTaskValue(
            isGarrison: false, hasAdvanceTarget: false, heat, survival, _config,
            survivalRetreatGate: 0.4f);
        _events.Publish(new SimFormationIntentEvent
        {
            Gid = Gid,
            Intent = _currentIntent,
            Heat = heat,
            Value = value,
        });
    }
}
