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
    public Occupation Role;         // 兵种（3.7：全兵种入编，按角色族填槽）
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

    [Tooltip("编队阵营（3.0.1_6 §4.3：招募只招本阵营空闲士兵；敌方将军用 Undead + FormationTable_Enemy）")]
    public Faction faction = Faction.Human_Player;

    [Tooltip("阵型切换防抖时间（秒，§15.3 即时触发+防抖）")]
    public float switchDebounce = 1f;

    [Tooltip("减员重排防抖时间（秒，§15.3）")]
    public float casualtyDebounce = 1f;

    [Header("2_8 步骤4：编队 2D 槽位形状（§5.2；缺省 Resources/Config/FormationShapes）")]
    [Tooltip("槽位形状形参 SO（线/圆/楔形参数）。未配置回退 Resources.Load('Config/FormationShapes')，仍缺失用内置默认（线1/圆1/楔0.7）")]
    public FormationShapes formationShapes;

    [Header("守城锚点（无将军编队用，§14.7）")]
    [Tooltip("是否无将军守城编队（true=城墙锚点模式，false=将军 NPC 模式）")]
    public bool isGarrison = false;

    // ===== 运行时状态 =====
    private UnitController _generalUnit;            // 将军单位（isGarrison=true 时为 null）
    private Transform _anchor;                       // 锚点 Transform（将军或城墙点）
    private AttentionTuningConfig _config;           // 全局调参 SO（军令强度/提强度/保底，防硬编码）
    private readonly List<FormationMember> _members = new List<FormationMember>();
    private TacticIntent _currentIntent = TacticIntent.Defense;
    private BattleLine _currentLine = BattleLine.Single;
    private FormationDef _currentFormation;
    private float _lastSwitchTime;
    private float _lastCasualtyTime;
    private bool _pendingReform;
    /// <summary>阵型切换瞬时提强度截止时间戳（3.0.1_8 §七，Time.time 未到则军令用 boost 强度）</summary>
    private float _boostUntil;
    /// <summary>君主令截止时间戳（3.0.1_8 §6.6：SetRoyalIntent 置位，期内军令带 royal 标记，个体永不弃任务）</summary>
    private float _royalUntil;
    // 阵型朝向：1=向右进攻（默认），-1=向左进攻。AssignSlots 时 offset.x *= _formationDirection
    private int _formationDirection = 1;
    /// <summary>自主补员上次扫描时间戳（3.7 §4.2：周期扫描 + 防抖）</summary>
    private float _lastAutoRecruitTime;
    /// <summary>编队 2D 槽位形状形参（2_8 步骤4：Inspector 优先，缺省 Resources.Load 兜底）</summary>
    private FormationShapes _shapes;

    /// <summary>当前意图</summary>
    public TacticIntent CurrentIntent => _currentIntent;
    /// <summary>当前锚点</summary>
    public Transform Anchor => _anchor;
    /// <summary>将军单位（守城编队为 null）</summary>
    public UnitController GeneralUnit => _generalUnit;
    /// <summary>成员数</summary>
    public int MemberCount => _members.Count;
    /// <summary>编队成员数上限（标准满编 − 将军位；守城编队无将军，多 1 人补将军槽）</summary>
    public int MaxMemberCount => FormationDef.StandardSize - (isGarrison ? 0 : 1);
    /// <summary>有效军令强度（3.0.1_8 §七：切换瞬间提强度保底期内返回 boost 值，否则正常值；值住 AttentionTuningConfig）</summary>
    public float EffectiveOrderIntensity => Time.time < _boostUntil
        ? (_config != null ? _config.formationOrderBoost : 6f)
        : (_config != null ? _config.formationOrderIntensity : 4.5f);
    /// <summary>是否君主令生效期（3.0.1_8 §6.6：期内军令带 royal 标记）</summary>
    public bool IsRoyalCommandActive => Time.time < _royalUntil;
    /// <summary>锚点世界坐标（将军/城墙锚点；无锚点返回 zero，中区块编队上限登记用）</summary>
    private Vector2 AnchorWorldPos => _anchor != null ? (Vector2)_anchor.position : Vector2.zero;

    private void Awake()
    {
        _config = Resources.Load<AttentionTuningConfig>("Config/AttentionTuningConfig");
        // 2_8 步骤4：槽位形状形参 SO（Inspector 优先，Resources 兜底）
        _shapes = formationShapes != null ? formationShapes
            : Resources.Load<FormationShapes>("Config/FormationShapes");
        if (isGarrison)
        {
            _anchor = transform;  // 守城编队锚点 = 挂载 GameObject 自身
        }
    }

    private void OnEnable()
    {
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
        // 3.0.1_5 §六：编队注册表（作战面板查询/选中/批量军令的数据源）
        if (FormationManager.Instance != null)
            FormationManager.Instance.Register(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
        if (FormationManager.Instance != null)
            FormationManager.Instance.Unregister(this);
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
        // 3.0.1_5 §四：军队级大脑（意图自决：攻/守/撤/支援），将军编队自动挂载
        if (general != null)
        {
            var brain = GetComponent<FormationBrain>();
            if (brain == null) brain = gameObject.AddComponent<FormationBrain>();
            brain.Init(this);
        }
    }

    /// <summary>
    /// 初始化为守城编队（无将军，绑城墙锚点）。
    /// 用于运行时 AddComponent 后设置 isGarrison + 锚点（Awake 时 isGarrison 默认 false，_anchor 未设，需显式调）。
    /// 3.7 P1 审查修复：守城编队也挂 FormationBrain（Sally 出城迎战/守撤意图自决驱动）。
    /// </summary>
    public void InitGarrison(Transform wallAnchor)
    {
        isGarrison = true;
        _anchor = wallAnchor != null ? wallAnchor : transform;
        _generalUnit = null;
        // 守城编队无将军，但军队级大脑（意图自决）仍需挂载——否则守城意图恒 Defense，
        // 3.7 §4.3 Sally（守城+城墙健康+敌压近→出城）永远不会触发。
        var brain = GetComponent<FormationBrain>();
        if (brain == null) brain = gameObject.AddComponent<FormationBrain>();
        brain.Init(this);
    }

    // ===== 招募（§1.2，绕开 ScheduleCenterStub 空壳自管）=====

    /// <summary>
    /// 招募编队成员（3.7 §4.1 编队构成自适应：全兵种按角色族优先级填，不要求填满）。
    /// 角色族顺序：抗线（盾卫/重装/战士）→ 输出（弓手/弩手/法师/大法师）→ 辅助（治疗/主教）→ 机动（骑兵）。
    /// 上限 = MaxMemberCount（标准满编 − 将军位；守城编队无将军，多 1 人补将军槽）。
    /// 有可用兵就招，没有就残编——不硬编码固定 3 近 2 弓阵容。
    /// </summary>
    public void RecruitStandard()
    {
        // 3.0.1_5 §五：中区块编队上限登记（单中区块最多 4 编队，超出拒绝招募，底层空间约束）
        if (FormationManager.Instance != null && !FormationManager.Instance.CanAddInMidRegion(AnchorWorldPos))
        {
            Debug.LogWarning($"[FormationController] 中区块编队已满（≥4），拒绝招募（锚点 {AnchorWorldPos}）。");
            return;
        }

        int maxMembers = MaxMemberCount;

        // 查场景内未编队的同阵营可作战单位（全兵种，按角色族优先级排序）
        var candidates = FindIdleSoldiers();
        foreach (var brain in candidates)
        {
            if (_members.Count >= maxMembers) break;
            AddMember(brain, brain.GetComponent<UnitController>().Data.occupation);
        }

        ApplyFormation();
        Debug.Log($"[FormationController] 自适应招募完成：{MemberCount}/{maxMembers} 成员，锚点={(_anchor != null ? _anchor.name : "null")}");
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

    /// <summary>
    /// 查找场景内未编队的同阵营可作战单位（3.7 §4.1：全兵种，排除工人/君主/静态工事）。
    /// 返回按角色族优先级排序（抗线 → 输出 → 辅助 → 机动），招募时优先填抗线前排。
    /// </summary>
    private List<NPCBrain> FindIdleSoldiers()
    {
        var result = new List<NPCBrain>();
        var allBrains = FindObjectsByType<NPCBrain>(FindObjectsSortMode.None);
        foreach (var brain in allBrains)
        {
            if (brain == null) continue;
            var unit = brain.GetComponent<UnitController>();
            if (unit == null || unit.Data == null) continue;
            // 3.0.1_6 §4.3：招募只招本阵营空闲士兵（敌方将军招 Undead，不抢我方兵）
            if (unit.Data.faction != faction) continue;
            // 3.7：全兵种入编，但排除工人/君主/静态工事（机器/塔/拒马/墙/门不参与编队移动）
            if (!IsRecruitable(unit.Data.occupation)) continue;
            if (brain.HasFormationSlot) continue;  // 已编队
            result.Add(brain);
        }
        // 角色族优先级排序（抗线优先，残编时保前排抗线）
        result.Sort((a, b) =>
        {
            int pa = GetRolePriority(a.GetComponent<UnitController>().Data.occupation);
            int pb = GetRolePriority(b.GetComponent<UnitController>().Data.occupation);
            return pa.CompareTo(pb);
        });
        return result;
    }

    /// <summary>是否可编入（3.7：工人/君主/静态工事排除，其余全兵种可编）</summary>
    private static bool IsRecruitable(Occupation occ)
    {
        switch (occ)
        {
            case Occupation.Civilian:      // 工人：非战斗单位
            case Occupation.Ruler:         // 君主：玩家控制
            case Occupation.SiegeMachine:  // 静态机器/工事：不参与编队移动
            case Occupation.Ballista:
            case Occupation.ArrowTower:
            case Occupation.CrossbowTower:
            case Occupation.MagicTower:
            case Occupation.Barricade:
            case Occupation.Wall:
            case Occupation.Gate:
                return false;
            default: return true;
        }
    }

    /// <summary>角色族优先级（3.7 §4.1：越小越优先。抗线→输出→辅助→机动）</summary>
    private static int GetRolePriority(Occupation occ)
    {
        switch (occ)
        {
            case Occupation.ShieldGuard:
            case Occupation.HeavyWarrior:
            case Occupation.Warrior:
                return 0;   // 抗线
            case Occupation.Archer:
            case Occupation.Crossbowman:
            case Occupation.Mage:
            case Occupation.Archmage:
                return 1;   // 输出
            case Occupation.Healer:
            case Occupation.Bishop:
                return 2;   // 辅助
            case Occupation.Cavalry:
            case Occupation.General:
                return 3;   // 机动/统帅
            default: return 4;
        }
    }

    /// <summary>近战族（3.7：抗线 + 机动；槽位 MeleeOnly/GeneralOnly 填此类）</summary>
    private static bool IsMeleeRole(Occupation occ)
    {
        switch (occ)
        {
            case Occupation.Warrior:
            case Occupation.HeavyWarrior:
            case Occupation.ShieldGuard:
            case Occupation.Cavalry:
            case Occupation.General:
                return true;
            default: return false;
        }
    }

    /// <summary>远程族（3.7：输出 + 辅助；槽位 RangedOnly 填此类）</summary>
    private static bool IsRangedRole(Occupation occ)
    {
        switch (occ)
        {
            case Occupation.Archer:
            case Occupation.Crossbowman:
            case Occupation.Mage:
            case Occupation.Archmage:
            case Occupation.Healer:
            case Occupation.Bishop:
                return true;
            default: return false;
        }
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

        // 3.7：查表改传角色族数量（近战族/远程族），Lookup 按意图+构成匹配度自选阵型
        // T09（00 B9）：传阵型权重表（TuningSnapshot.formationWeights），Lookup 用训练权重×构成匹配度选阵型
        FormationDef def = isGarrison
            ? formationTable.LookupGarrison()
            : formationTable.Lookup(_currentIntent, _currentLine, CountRoleFamily(true), CountRoleFamily(false),
                                    _config != null ? _config.ToSnapshot().formationWeights : null);

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
    /// 槽位分配 2D 化（2_8 步骤4，§5.2）。
    /// 形状生成（格单位浮点，FormationShapes 形参）→ 角色族填充（近战外沿/前，弓手内/后）→
    /// 可走校验回缩（IsSubWalkable 微格）→ round 到整数 cell 存 SlotOffset（与 FollowStimulus 既有换算一致）。
    /// 朝向（D2）：敌人方向 = Anchor→AdvanceTarget；无目标 = 前进方向（_formationDirection）。
    /// 减员防抖重排由 Update 沿用 casualtyDebounce 触发。
    /// </summary>
    private void AssignSlots(FormationDef def)
    {
        // 3.7：按角色族分组（近战族=抗线+机动，远程族=输出+辅助；替代 P0 只分 Warrior/Archer）
        var melee = new List<FormationMember>();
        var archer = new List<FormationMember>();
        foreach (var m in _members)
        {
            if (IsRangedRole(m.Role)) archer.Add(m);
            else melee.Add(m);   // 近战族 + 未分类兜底算近战
        }

        // 2D 形状生成槽位（格单位，顺序=近战外沿/前，弓手内/后），已做可走校验回缩
        var slots = BuildSlots2D(def, melee.Count, archer.Count);

        int idx = 0;
        for (int k = 0; k < melee.Count && idx < slots.Count; k++)
        {
            var mem = melee[k];
            mem.SlotOffset = slots[idx++];
            melee[k] = mem;          // 结构体经列表索引器整体回写（CS1612）
            ReplaceMember(mem);
        }
        for (int k = 0; k < archer.Count && idx < slots.Count; k++)
        {
            var mem = archer[k];
            mem.SlotOffset = slots[idx++];
            archer[k] = mem;         // 结构体经列表索引器整体回写（CS1612）
            ReplaceMember(mem);
        }
        Debug.Log($"[FormationController] 槽位 2D 生成完成：shape={def.shape}（槽位 {slots.Count} 个，赋 {idx} 个）");
    }

    /// <summary>
    /// 按形状生成编队槽位（2_8 步骤4）。
    /// 返回整数 cell 偏移列表，顺序保证近战槽在前（外沿/前排），弓手槽在后（内/后撤排），
    /// 供 AssignSlots 按 近战→弓手 依次赋成员（天然保护弓手）。
    /// </summary>
    private List<Vector2Int> BuildSlots2D(FormationDef def, int meleeCount, int archerCount)
    {
        int n = meleeCount + archerCount;
        var raw = GenerateShapeOffsets(def.shape, n, meleeCount, archerCount);

        Vector2 anchorWorld = AnchorWorldPos;
        Vector2 cell = CellSize2();
        var result = new List<Vector2Int>(raw.Count);
        foreach (var off in raw)
        {
            // 可走校验 + 回缩（不可走向锚点回缩），再 round 到整数 cell
            Vector2 resolved = ResolveWalkable(anchorWorld, off, cell);
            result.Add(new Vector2Int(Mathf.RoundToInt(resolved.x), Mathf.RoundToInt(resolved.y)));
        }
        return result;
    }

    /// <summary>当前格尺寸（cellSize）</summary>
    private Vector2 CellSize2()
    {
        if (GridSystem.Instance != null && GridSystem.Instance.Config != null)
            return GridSystem.Instance.Config.cellSize;
        return new Vector2(1.28f, 0.64f);
    }

    /// <summary>
    /// 编队朝向（D2）：敌人方向 = Anchor→AdvanceTarget；无目标（AdvanceTarget≈zero）回退前进方向（_formationDirection）。
    /// SetAdvanceTarget 已按推进目标相对锚点维护 AdvanceTarget 与 _formationDirection。
    /// </summary>
    private Vector2 GetFormationForward()
    {
        Vector2 dir = Vector2.zero;
        if (AdvanceTarget != Vector2.zero && _anchor != null)
        {
            Vector2 d = AdvanceTarget - (Vector2)_anchor.position;
            if (d.sqrMagnitude > 0.0001f) dir = d.normalized;
        }
        // 无目标 = 前进方向（默认向右，1=右 / -1=左）
        if (dir == Vector2.zero)
            dir = _formationDirection >= 0 ? Vector2.right : Vector2.left;
        return dir;
    }

    /// <summary>垂直朝向的单位向量（阵线横向展开方向）</summary>
    private static Vector2 Perp(Vector2 forward) => new Vector2(-forward.y, forward.x);

    /// <summary>
    /// 按形状生成槽位格坐标（相对锚点，格单位浮点；含楔形 0.7 后撤）。
    /// 返回顺序：近战位在前（外沿/前排/外环），弓手位在后（内/后撤/内环）。
    /// 形参读 FormationShapes SO，缺失用内置默认（线 1 / 圆 1 / 楔 0.7）。
    /// </summary>
    private List<Vector2> GenerateShapeOffsets(FormationShape shape, int n, int meleeCount, int archerCount)
    {
        float spacing = _shapes != null ? _shapes.lineSpacingCells : 1f;
        float minR = _shapes != null ? _shapes.circleMinRadiusCells : 1f;
        float wedgeBack = _shapes != null ? _shapes.wedgeStepBackCells : 0.7f;
        Vector2 fwd = GetFormationForward();
        Vector2 perp = Perp(fwd);
        var list = new List<Vector2>(n);

        switch (shape)
        {
            case FormationShape.Circle:
            {
                // 将军居中（锚点），r=ceil(n/2π)，弓手内环/近战外环
                int r = Mathf.Max(1, Mathf.CeilToInt(n / (2f * Mathf.PI)));
                float outerR = Mathf.Max(minR, r);
                float innerR = Mathf.Max(minR * 0.5f, r - 1);
                float baseAng = Mathf.Atan2(fwd.y, fwd.x);
                for (int i = 0; i < meleeCount; i++)   // 近战外环
                {
                    float a = baseAng + i / (float)Mathf.Max(1, meleeCount) * Mathf.PI * 2f;
                    list.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outerR);
                }
                for (int i = 0; i < archerCount; i++)  // 弓手内环（错开半步防重叠）
                {
                    float a = baseAng + (i + 0.5f) / (float)Mathf.Max(1, archerCount) * Mathf.PI * 2f;
                    list.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * innerR);
                }
                break;
            }
            case FormationShape.Wedge:
            {
                // 朝向为轴，两翼逐排后撤：近战前排（后撤少），弓手后排（后撤多）
                for (int i = 0; i < n; i++)
                {
                    int row = i / 2;
                    int side = (i & 1) == 0 ? -1 : 1;
                    float h = (row + 1) * spacing;                 // 横向展开（两侧）
                    float back = (row + 1) * wedgeBack;            // 逐排后撤（沿朝向负向）
                    list.Add(fwd * (-back) + perp * (side * h));
                }
                break;
            }
            case FormationShape.Line:
            default:
            {
                // 线阵：垂直朝向直线展开，间距 spacing；先按 |along| 降序（近战外沿两端），弓手居中
                for (int i = 0; i < n; i++)
                {
                    float along = (i - (n - 1) * 0.5f) * spacing;
                    list.Add(perp * along);
                }
                break;
            }
        }

        // Line 需要按外沿优先排（近战两端）。Circle/Wedge 生成顺序已天然外→内 / 前→后。
        if (shape == FormationShape.Line)
        {
            // 距锚点越远越"外沿"，降序（近战先占两端，弓手居中）
            list.Sort((a, b) =>
                (b.x * b.x + b.y * b.y).CompareTo(a.x * a.x + a.y * a.y));
        }

        return list;
    }

    /// <summary>
    /// 槽位可走校验 + 回缩（2_7 §5.4 R2）。
    /// 目标世界位 = anchor + offset(格)×cellSize，经 WorldToSubCoord → IsSubWalkable；
    /// 不可走向锚点逐步回缩（t: 1→0.75→0.5→0.25→0），回缩到可走位返回（格单位近似）。
    /// </summary>
    private Vector2 ResolveWalkable(Vector2 anchorWorld, Vector2 offsetCells, Vector2 cell)
    {
        var grid = GridSystem.Instance;
        if (grid == null || grid.Config == null) return offsetCells;   // 无网格：直接给原偏移（不破坏旧行为）
        const float step = 0.25f;
        for (float t = 1f; t >= 0f - 0.001f; t -= step)
        {
            Vector2 off = offsetCells * t;
            Vector2 target = anchorWorld + new Vector2(off.x * cell.x, off.y * cell.y);
            var sub = grid.WorldToSubCoord(target);
            if (sub.HasValue && grid.IsSubWalkable(sub.Value)) return off;
        }
        return Vector2.zero;   // 全部不可走：回缩到锚点（将军位）
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
            // 3.0.1_8 §七：军令强度用 EffectiveOrderIntensity（切换保底期内 6.0，否则 4.5）
            // 3.0.1_8 §6.6：君主令期军令带 royal 标记（个体永不弃任务，收益封顶）
            m.Brain.SetFormationSlot(anchorUnit, TaskPriority.S, EffectiveOrderIntensity, m.SlotOffset, IsRoyalCommandActive);
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
        // 3.0.1_8 §七：切阵型瞬时提强度（保底期内军令 4.5→6.0，低威胁士兵强制归位保护弓手）
        _boostUntil = Time.time + (_config != null ? _config.formationOrderBoostDuration : 1f);
        ApplyFormation();
        Debug.Log($"[FormationController] 意图切换 -> {intent}（军令瞬时提强度 {EffectiveOrderIntensity:F1} 保底 {(_config != null ? _config.formationOrderBoostDuration : 1f)}s）");
    }

    /// <summary>切换战线形态（P1 由 ThreatHeat 方向分布驱动）</summary>
    public void SetBattleLine(BattleLine line)
    {
        if (line == _currentLine) return;
        _currentLine = line;
        ApplyFormation();
    }

    /// <summary>
    /// 君主令（3.0.1_8 §6.6）：君主下令不顾一切 → 切意图 + 军令带 royal 标记（个体永不弃任务，收益封顶）。
    /// duration 秒内生效，过期回落（重发军令清标记）。作战面板/君主指挥链调用。
    /// </summary>
    public void SetRoyalIntent(TacticIntent intent, float duration)
    {
        _royalUntil = Time.time + Mathf.Max(0f, duration);
        SetIntent(intent);
        Debug.Log($"[FormationController] 君主令：{intent}（{duration}s 内军令带 royal 标记，个体永不弃任务）");
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

        // 3.0.1_8 §七：阵型切换瞬时提强度过期 → 回落正常军令强度并重发（士兵侧 FollowStimulus.Intensity 同步回落）
        if (_boostUntil > 0f && Time.time >= _boostUntil && _members.Count > 0)
        {
            _boostUntil = 0f;
            DispatchOrders();
            Debug.Log($"[FormationController] 军令瞬时提强度过期，回落至 {(_config != null ? _config.formationOrderIntensity : 4.5f):F1}。");
        }

        // 3.0.1_8 §6.6：君主令过期 → 回落（重发军令清 royal 标记）
        if (_royalUntil > 0f && Time.time >= _royalUntil && _members.Count > 0)
        {
            _royalUntil = 0f;
            DispatchOrders();
            Debug.Log("[FormationController] 君主令过期，军令回落（royal 标记清除）。");
        }

        // 3.7 §4.2 自主补员：编队不满员 + 无受击静默期 → 周期扫描补员（战斗状态停止自主组队）
        float autoRecruitInterval = _config != null ? _config.formationAutoRecruitInterval : 5f;
        if (Time.time - _lastAutoRecruitTime >= autoRecruitInterval)
        {
            _lastAutoRecruitTime = Time.time;
            TryAutoRecruit();
        }

        // 进攻推进 P0 简化：将军 brain 自驱动（靠威胁焦点），此处不直接操控将军
        // P1：注入 TaskStimulus 到将军 brain（目标=AdvanceTarget）
    }

    /// <summary>
    /// 3.7 §4.2 自主补员入口：编队不满员 + 减员静默期已过 + 无待重排 → 走补员。
    /// 周期由 _config.formationAutoRecruitInterval 控制（Update 内节流）。
    /// </summary>
    private void TryAutoRecruit()
    {
        if (_members.Count >= MaxMemberCount) return;
        // 战斗状态停止自主组队：最近一次阵亡在静默期内 → 跳过（无受击 10s 语义，值住 SO）
        float quiet = _config != null ? _config.formationAutoRecruitQuietSeconds : 10f;
        if (Time.time - _lastCasualtyTime < quiet) return;
        // 减员防抖重排未完成 → 先重排再补员
        if (_pendingReform) return;
        RecruitReinforcement();
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
    /// 补充成员（3.7 §4.2 补员机制，全兵种自适应）。
    /// 走与初始招募同一流程（FindIdleSoldiers 全兵种 + 角色族优先级 + 不填满）。
    /// 手动触发（debug 热键）与自主补员（Update 周期扫描）共用。
    /// </summary>
    public void RecruitReinforcement()
    {
        var candidates = FindIdleSoldiers();
        if (candidates.Count == 0)
        {
            Debug.Log("[FormationController] 补员失败：无可招募的同阵营空闲单位。");
            return;
        }

        int maxMembers = MaxMemberCount;
        foreach (var brain in candidates)
        {
            if (_members.Count >= maxMembers) break;
            AddMember(brain, brain.GetComponent<UnitController>().Data.occupation);
        }
        ApplyFormation();
        Debug.Log($"[FormationController] 补员完成：{MemberCount}/{maxMembers} 成员");
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

    /// <summary>统计角色族成员数（3.7：melee=true 近战族 / false 远程族，查表选阵用）</summary>
    private int CountRoleFamily(bool melee)
    {
        int count = 0;
        foreach (var m in _members)
        {
            if (melee ? IsMeleeRole(m.Role) : IsRangedRole(m.Role)) count++;
        }
        return count;
    }
}
