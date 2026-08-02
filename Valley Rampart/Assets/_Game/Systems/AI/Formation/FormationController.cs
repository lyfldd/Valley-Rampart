using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  3.0.1_3 AI 协作 - FormationController 军队级编队控制器
//  详见 3.0.1_3_AI协作.md §5.4 / §14 / §15
//  挂载点：将军 NPC（Occupation.General）或城墙锚点 GameObject（无将军守城编队）
//  锚点抽象（§1.1）：Anchor = 将军 NPC Transform 或城墙预设点（静态），不绑死将军
// ============================================================================

/// <summary>
/// 编队成员运行时记录。
/// </summary>
public struct FormationMember
{
    public NPCBrain Brain;          // 成员大脑（下发军令入口）
    public UnitController Unit;     // 成员单位（存活检测/死亡监听）
    public Occupation Role;         // 兵种（Warrior 近战 / Archer 弓手）
    public Vector2Int SlotOffset;   // 当前分配的槽位偏移（cell 单位）
}

/// <summary>
/// FormationController（§5.4，军队级组件）。
/// 有状态：当前三元组 / 槽位分配表 / 成员列表 / 锚点。
/// 职责：编队招募 / 查表选阵 / 下发 FormationStimulus（槽位军令）/ 阵型切换 / 解散。
///
/// 不住个体三层管线（军队级决策）；输出物是刺激源，符合"两类东西"纪律。
/// 将军阵亡 → 组件销毁 → 编队解散（指挥链断裂自然涌现，§7.4）。
///
/// P0 简化：
///   - 阵型手配 3 条（防守/进攻/守城），不切换候选表（§3.6 P1）
///   - 招募绕开 ScheduleCenterStub 空壳自管（§十三已决）
///   - 减员即时触发 + 防抖 1s 重排（§15.3）
///   - 进攻推进：将军 brain 的威胁焦点驱动（敌人进感知即 MoveTowards 推进）；P1 改 TaskStimulus 注入推进目标
/// </summary>
public class FormationController : MonoBehaviour
{
    // ===== 配置 =====
    [Header("编队配置")]
    [Tooltip("阵型查找表 SO（P0 手配防守/进攻/守城各一条）")]
    public FormationTable formationTable;

    [Tooltip("军令强度（S 级军令基础强度，需 > 工作任务 B 级 + 安全归巢 D 级）")]
    public float orderIntensity = 4.5f;

    /// <summary>军令强度归一化基准（3.0.1_8 协作因子用：FormationFactor = orderIntensity / 此值）</summary>
    public const float OrderIntensityBase = 4.5f;

    [Tooltip("阵型切换防抖时间（秒，§15.3 即时触发+防抖）")]
    public float switchDebounce = 1f;

    [Tooltip("减员重排防抖时间（秒，§15.3）")]
    public float casualtyDebounce = 1f;

    [Header("守城锚点（无将军编队用，§14.7）")]
    [Tooltip("是否无将军守城编队（true=城墙锚点模式，false=将军 NPC 模式）")]
    public bool isGarrison = false;

    // ===== 运行时状态 =====
    private UnitController _generalUnit;            // 将军单位（isGarrison=true 时为 null）
    private Transform _anchor;                       // 锚点 Transform（将军或城墙点）
    private readonly List<FormationMember> _members = new List<FormationMember>();
    private TacticIntent _currentIntent = TacticIntent.Defense;
    private BattleLine _currentLine = BattleLine.Single;
    private FormationDef _currentFormation;
    private float _lastSwitchTime;
    private float _lastCasualtyTime;
    private bool _pendingReform;
    // 阵型朝向：1=向右进攻（默认），-1=向左进攻。AssignSlots 时 offset.x *= _formationDirection
    private int _formationDirection = 1;

    /// <summary>当前意图</summary>
    public TacticIntent CurrentIntent => _currentIntent;
    /// <summary>当前锚点</summary>
    public Transform Anchor => _anchor;
    /// <summary>将军单位（守城编队为 null）</summary>
    public UnitController GeneralUnit => _generalUnit;
    /// <summary>成员数</summary>
    public int MemberCount => _members.Count;

    private void Awake()
    {
        if (isGarrison)
        {
            _anchor = transform;  // 守城编队锚点 = 挂载 GameObject 自身
        }
    }

    private void OnEnable()
    {
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
    }

    /// <summary>
    /// 绑定将军（将军 NPC 生成后调）。
    /// 将军 NPC 挂此组件，isGarrison=false，_generalUnit=将军自身。
    /// </summary>
    public void BindGeneral(UnitController general)
    {
        _generalUnit = general;
        _anchor = general != null ? general.transform : _anchor;
        // 3.0.1_LOD §1.2：军队锚点注册（活跃带双中心，将军位置点亮活跃带）
        if (general != null && LODSystem.Instance != null)
            LODSystem.Instance.RegisterArmyCenter(general.transform);
    }

    /// <summary>
    /// 初始化为守城编队（无将军，绑城墙锚点）。
    /// 用于运行时 AddComponent 后设置 isGarrison + 锚点（Awake 时 isGarrison 默认 false，_anchor 未设，需显式调）。
    /// </summary>
    public void InitGarrison(Transform wallAnchor)
    {
        isGarrison = true;
        _anchor = wallAnchor != null ? wallAnchor : transform;
        _generalUnit = null;
    }

    // ===== 招募（§1.2，绕开 ScheduleCenterStub 空壳自管）=====

    /// <summary>
    /// 招募编队成员（P0 简化：从场景查 Human_Player 阵营士兵，按满编 (3 近战 + 2 弓手) 招）。
    /// 满编 6 = 1 将军 + 3 近战 + 2 弓手。
    /// 守城编队（isGarrison=true）招 5 成员（无将军，城墙锚点）。
    /// </summary>
    public void RecruitStandard()
    {
        int targetMelee = 3;  // 不含将军
        int targetArcher = 2;
        if (isGarrison)
        {
            // 守城编队无将军，多招 1 近战补将军槽
            targetMelee = 4;
        }

        int haveMelee = CountRole(Occupation.Warrior);
        int haveArcher = CountRole(Occupation.Archer);

        // 查场景内未编队的 Human_Player 士兵
        var candidates = FindIdleSoldiers();
        foreach (var brain in candidates)
        {
            if (_members.Count >= FormationDef.StandardSize - (isGarrison ? 0 : 1)) break;

            Occupation role = brain.GetComponent<UnitController>().Data.occupation;
            if (role == Occupation.Warrior && haveMelee < targetMelee)
            {
                AddMember(brain, Occupation.Warrior);
                haveMelee++;
            }
            else if (role == Occupation.Archer && haveArcher < targetArcher)
            {
                AddMember(brain, Occupation.Archer);
                haveArcher++;
            }
        }

        ApplyFormation();
        Debug.Log($"[FormationController] 招募完成：{MemberCount} 成员（近战 {haveMelee}/弓手 {haveArcher}），锚点={(_anchor != null ? _anchor.name : "null")}");
    }

    /// <summary>添加成员并下发军令</summary>
    private void AddMember(NPCBrain brain, Occupation role)
    {
        var unit = brain.GetComponent<UnitController>();
        var member = new FormationMember
        {
            Brain = brain,
            Unit = unit,
            Role = role,
            SlotOffset = Vector2Int.zero,
        };
        _members.Add(member);
    }

    /// <summary>查找场景内未编队的 Human_Player 士兵（Warrior/Archer）</summary>
    private List<NPCBrain> FindIdleSoldiers()
    {
        var result = new List<NPCBrain>();
        var allBrains = FindObjectsByType<NPCBrain>(FindObjectsSortMode.None);
        foreach (var brain in allBrains)
        {
            if (brain == null) continue;
            var unit = brain.GetComponent<UnitController>();
            if (unit == null || unit.Data == null) continue;
            if (unit.Data.faction != Faction.Human_Player) continue;
            if (unit.Data.occupation != Occupation.Warrior && unit.Data.occupation != Occupation.Archer) continue;
            if (brain.HasFormationSlot) continue;  // 已编队
            result.Add(brain);
        }
        return result;
    }

    // ===== 阵型应用（查表 + 槽位分配 + 下发军令）=====

    /// <summary>按当前意图/构成查表，分配槽位并下发军令</summary>
    public void ApplyFormation()
    {
        if (formationTable == null)
        {
            Debug.LogError("[FormationController] formationTable 未配置！");
            return;
        }
        if (_anchor == null)
        {
            Debug.LogWarning("[FormationController] 锚点为空，跳过阵型应用。");
            return;
        }

        FormationDef def = isGarrison
            ? formationTable.LookupGarrison()
            : formationTable.Lookup(_currentIntent, _currentLine, CountRole(Occupation.Warrior), CountRole(Occupation.Archer));

        if (def == null)
        {
            Debug.LogError($"[FormationController] 阵型查表失败：intent={_currentIntent}");
            return;
        }

        _currentFormation = def;
        AssignSlots(def);
        DispatchOrders();
        Debug.Log($"[FormationController] 阵型应用：{def.displayName}（{MemberCount} 成员）");
    }

    /// <summary>
    /// 槽位分配（§3.2 R2 残编紧凑：空槽压队尾，按兵种填槽）。
    /// 两轮填充：
    ///   第一轮：近战填 MeleeOnly/GeneralOnly/Any 槽，**按 |x| 外侧优先**（防残编时弓手顶前排：
    ///     近战先占两端防线，弓手居中安全位——修 3.0.1_LOD 场景验证暴露的"弓手占第一位"）
    ///   第二轮：弓手填剩余 RangedOnly/Any 槽，按距锚点 |x| 从近到远排序（残编时弓优先填靠后安全位）
    /// 方向翻转：offset.x *= _formationDirection（1=右/-1=左）
    /// </summary>
    private void AssignSlots(FormationDef def)
    {
        // 分两组：近战（含将军算近战，但将军不在此列表）/ 弓手
        var melee = new List<FormationMember>();
        var archer = new List<FormationMember>();
        foreach (var m in _members)
        {
            if (m.Role == Occupation.Archer) archer.Add(m);
            else melee.Add(m);
        }

        bool[] occupied = new bool[def.slots.Length];
        int dir = _formationDirection;

        // 第一轮：近战按 |x| 外侧优先填 MeleeOnly/GeneralOnly/Any 槽
        var meleeSlots = new List<int>();
        for (int i = 0; i < def.slots.Length; i++)
        {
            SlotRole role = def.slots[i].role;
            if (role == SlotRole.MeleeOnly || role == SlotRole.GeneralOnly || role == SlotRole.Any)
                meleeSlots.Add(i);
        }
        meleeSlots.Sort((a, b) =>
            Mathf.Abs(def.slots[b].cellOffset.x).CompareTo(Mathf.Abs(def.slots[a].cellOffset.x)));

        int meleeIdx = 0;
        foreach (int i in meleeSlots)
        {
            if (meleeIdx >= melee.Count) break;
            var m = melee[meleeIdx++];
            m.SlotOffset = new Vector2Int(def.slots[i].cellOffset.x * dir, def.slots[i].cellOffset.y);
            ReplaceMember(m);
            occupied[i] = true;
        }

        // 第二轮：收集剩余 RangedOnly/Any 弓手槽，按距锚点 |x| 从近到远排序后填
        var archerSlots = new List<int>();
        for (int i = 0; i < def.slots.Length; i++)
        {
            if (occupied[i]) continue;
            SlotRole role = def.slots[i].role;
            if (role == SlotRole.RangedOnly || role == SlotRole.Any)
                archerSlots.Add(i);
        }
        archerSlots.Sort((a, b) =>
            Mathf.Abs(def.slots[a].cellOffset.x).CompareTo(Mathf.Abs(def.slots[b].cellOffset.x)));

        int archerIdx = 0;
        foreach (int i in archerSlots)
        {
            if (archerIdx >= archer.Count) break;
            var m = archer[archerIdx++];
            m.SlotOffset = new Vector2Int(def.slots[i].cellOffset.x * dir, def.slots[i].cellOffset.y);
            ReplaceMember(m);
            occupied[i] = true;
        }
    }

    /// <summary>替换成员记录（更新 SlotOffset）</summary>
    private void ReplaceMember(FormationMember updated)
    {
        for (int i = 0; i < _members.Count; i++)
        {
            if (ReferenceEquals(_members[i].Brain, updated.Brain))
            {
                _members[i] = updated;
                return;
            }
        }
    }

    /// <summary>下发军令（对每个成员 SetFormationSlot）</summary>
    private void DispatchOrders()
    {
        // 锚点 = 将军 UnitController（将军 NPC 编队）或 null（守城编队，士兵 FollowAnchor 锚点为城墙点 Transform）
        // FollowStimulus.Anchor 是 UnitController 类型——守城编队无 UnitController 锚点，
        // P0 守城编队用将军 UnitController=null 会导致 FollowStimulus.Position=zero，
        // 故守城编队模式 P0 简化：仍需一个 UnitController 作锚点占位。
        // 解法：守城编队 isGarrison=true 时，_generalUnit 留空，但锚点 Transform = transform（城墙点），
        // 成员 FollowStimulus.Anchor 字段需要一个 UnitController——P0 占位用成员自身（自跟随，槽位偏移即绝对位置）。
        // P1 改造 FollowStimulus.Anchor 为 Transform 而非 UnitController（统一锚点抽象）。
        UnitController anchorUnit = _generalUnit;
        if (anchorUnit == null && isGarrison && _members.Count > 0)
        {
            // P0 守城编队占位：用第一个成员作锚点占位（槽位偏移相对该成员，效果近似城墙锚点）
            // ⚠️ 已知限制：该成员移动会带偏全队。守城场景成员应静止守槽，此限制可接受。
            anchorUnit = _members[0].Unit;
        }

        foreach (var m in _members)
        {
            if (m.Brain == null) continue;
            m.Brain.SetFormationSlot(anchorUnit, TaskPriority.S, orderIntensity, m.SlotOffset);
        }
    }

    // ===== 阵型切换（§5.3 将军唯一决策权）=====

    /// <summary>切换战术意图（君主军令触发，重查表重排）</summary>
    public void SetIntent(TacticIntent intent)
    {
        if (intent == _currentIntent) return;
        if (Time.time - _lastSwitchTime < switchDebounce)
        {
            Debug.Log($"[FormationController] 阵型切换防抖中，跳过（{Time.time - _lastSwitchTime:F2}s < {switchDebounce}s）");
            return;
        }
        _currentIntent = intent;
        _lastSwitchTime = Time.time;
        ApplyFormation();
        Debug.Log($"[FormationController] 意图切换 -> {intent}");
    }

    /// <summary>切换战线形态（P1 由 ThreatHeat 方向分布驱动）</summary>
    public void SetBattleLine(BattleLine line)
    {
        if (line == _currentLine) return;
        _currentLine = line;
        ApplyFormation();
    }

    // ===== 进攻推进（§14.2 将军带头）=====

    /// <summary>
    /// 进攻推进目标点（敌人位置 / 集结点）。
    /// P0 简化：FormationController 不直接驱动将军移动，靠将军 brain 的威胁焦点驱动（敌人进感知即 MoveTowards 推进）。
    /// 此方法暴露推进目标供调用方（CombatTestSpawner debug 热键）记录，P1 改为注入 TaskStimulus 到将军 brain。
    /// </summary>
    public Vector2 AdvanceTarget { get; private set; }

    /// <summary>设置推进目标（debug 用，P1 改 TaskStimulus 注入）</summary>
    public void SetAdvanceTarget(Vector2 target)
    {
        AdvanceTarget = target;
        // 根据推进目标相对锚点的 x 符号设置阵型朝向（1=右/-1=左）
        if (_anchor != null)
        {
            float dx = target.x - _anchor.position.x;
            _formationDirection = dx >= 0f ? 1 : -1;
        }
    }

    // ===== 减员管理（§15）=====

    /// <summary>成员死亡事件处理（§15.3 即时触发+防抖）</summary>
    private void OnUnitDied(UnitDiedEvent evt)
    {
        // 将军阵亡 → 锚点丢失 → 编队解散（§7.4 / §15.1）
        if (_generalUnit != null && ReferenceEquals(evt.Unit, _generalUnit))
        {
            Debug.Log("[FormationController] 将军阵亡，编队解散，全体批量清理状态。");
            DisbandAll();
            return;
        }

        // 非将军成员死亡 → 移出列表 + 防抖重排（§15.3）
        // 注：evt.Unit 是 IDamageable（无 Data 属性），用成员自身的 UnitController.Data 取职业
        for (int i = _members.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_members[i].Unit, evt.Unit))
            {
                Occupation role = _members[i].Role;
                _members.RemoveAt(i);
                _lastCasualtyTime = Time.time;
                _pendingReform = true;
                Debug.Log($"[FormationController] 成员阵亡（{role}），剩余 {MemberCount} 人，待防抖重排。");
                break;
            }
        }
    }

    private void Update()
    {
        // 减员防抖重排（§15.3）
        if (_pendingReform && Time.time - _lastCasualtyTime >= casualtyDebounce)
        {
            _pendingReform = false;
            if (_members.Count > 0)
            {
                ApplyFormation();
                Debug.Log($"[FormationController] 减员防抖到期，残编重排：{MemberCount} 人。");
            }
        }

        // 进攻推进 P0 简化：将军 brain 自驱动（靠威胁焦点），此处不直接操控将军
        // P1：注入 TaskStimulus 到将军 brain（目标=AdvanceTarget）
    }

    // ===== 解散与状态清理（§15.5 ClearFormationState）=====

    /// <summary>
    /// 解散编队，全体批量清理状态（§15.5）。
    /// 将军阵亡 / 君主手动解散时调。
    /// </summary>
    public void DisbandAll()
    {
        foreach (var m in _members)
        {
            ClearFormationState(m);
        }
        _members.Clear();
        Debug.Log("[FormationController] 编队解散，全体状态清理完成。");
    }

    /// <summary>
    /// 清除单个成员的编队状态（§15.5 ClearFormationState）。
    /// 清：军令刺激源（FollowStimulus 含 SlotOffset）+ 槽位绑定。
    /// 光环因子标记 P0 未实装（§5.2 P1 属性 buff 通道），留空。
    /// </summary>
    private void ClearFormationState(FormationMember member)
    {
        if (member.Brain == null) return;
        member.Brain.ClearFormationSlot();
    }

    /// <summary>
    /// 补充成员（§15.4 补充机制）。
    /// P0 简化：手动触发（debug 热键）；自动触发（region 热度低+无受击 10s）依赖 LOD，P1 落地。
    /// 走与初始招募同一流程（从空闲池/场景未编队士兵招）。
    /// </summary>
    public void RecruitReinforcement()
    {
        var candidates = FindIdleSoldiers();
        if (candidates.Count == 0)
        {
            Debug.Log("[FormationController] 补员失败：无可招募的空闲士兵。");
            return;
        }

        // 按当前缺员补
        int targetMelee = isGarrison ? 4 : 3;
        int targetArcher = 2;
        int haveMelee = CountRole(Occupation.Warrior);
        int haveArcher = CountRole(Occupation.Archer);

        foreach (var brain in candidates)
        {
            if (_members.Count >= FormationDef.StandardSize - (isGarrison ? 0 : 1)) break;
            var unit = brain.GetComponent<UnitController>();
            Occupation role = unit.Data.occupation;
            if (role == Occupation.Warrior && haveMelee < targetMelee)
            {
                AddMember(brain, Occupation.Warrior);
                haveMelee++;
            }
            else if (role == Occupation.Archer && haveArcher < targetArcher)
            {
                AddMember(brain, Occupation.Archer);
                haveArcher++;
            }
        }
        ApplyFormation();
        Debug.Log($"[FormationController] 补员完成：{MemberCount} 成员");
    }

    private void OnDestroy()
    {
        // 将军阵亡时 GameObject 销毁，OnDestroy 兜底全体清理（防 OnUnitDied 未触发或时序问题）
        if (_members.Count > 0)
        {
            DisbandAll();
        }
        // 3.0.1_LOD：注销军队锚点（将军死亡/组件销毁）
        if (_generalUnit != null && LODSystem.Instance != null)
            LODSystem.Instance.UnregisterArmyCenter(_generalUnit.transform);
    }

    // ===== 辅助 =====

    private int CountRole(Occupation role)
    {
        int count = 0;
        foreach (var m in _members)
        {
            if (m.Role == role) count++;
        }
        return count;
    }
}
