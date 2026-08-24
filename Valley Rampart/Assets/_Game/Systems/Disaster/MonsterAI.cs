using UnityEngine;

// 2_14 普通怪行为驱动器（实施计划步骤7 段① / D252：普通怪挂 MonsterAI，不进决策核训练）。
// 完整四态：Raiding(出击默认) / Guarding(守门回援) / Retreating(撤退) / Looting(掠夺)。
//   Raiding : 价值×距离选高价值建筑（D83，CombatRules.TargetScore 首次接线）+ 走 A* 寻路；到目标→Looting。
//   Guarding: 订阅 PortalAttackedEvent，按 guardRecallRatio（确定性哈希 R4）回援传送门，打靠近门的玩家单位。
//   Retreating: HP<retreatHpRatio→退回传送门；被拦截/近战受击→继续战斗（无逃脱）。
//   Looting : 到达资源点→停留 lootingStaySeconds→携带 carryResource 回传送门→资源消失（吸收入口）。
// 寻路走 PathFollower(A*)，非成本场（2_6 CostFieldBuilder 未落地，计划 R6 认可"未就绪只输出不消费"退避）。
// 确定性 R4：回援选取用 npcId 稳定哈希；无 UnityEngine.Random 进决策。
[RequireComponent(typeof(MonsterController))]
public class MonsterAI : MonoBehaviour
{
    private MonsterController _mc;
    private PathFollower _pf;
    private IDamageable _raidTarget;      // 当前出击目标（建筑）
    private bool _carryingHome;           // 掠夺完成携带资源回门中（回门即资源被吸收）
    private float _lootStart;
    private float _guardUntil;

    private const float GuardHoldSeconds = 4f;          // 守门保持时长（抵门且无威胁后回出击）
    private const float LootRadiusCells = 1.2f;         // 进入掠夺的接近半径（格）

    private void Awake()
    {
        _mc = GetComponent<MonsterController>();
        _pf = GetComponent<PathFollower>() ?? gameObject.AddComponent<PathFollower>();  // AddComponent 即时 Awake 绑定 _unit
    }

    private void OnEnable()
    {
        EventBus.Subscribe<PortalAttackedEvent>(OnPortalAttacked);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<PortalAttackedEvent>(OnPortalAttacked);
    }

    private void Update()
    {
        if (_mc == null || !_mc.IsAlive || DamageSystem.Instance == null) return;

        // 撤退判定（高优先级）：HP<阈值 → 退回传送门
        if (_mc.mode != MonsterMode.Retreating &&
            _mc.MaxHp > 0 && _mc.CurrentHp <= _mc.MaxHp * _mc.RetreatHpRatio)
        {
            _mc.mode = MonsterMode.Retreating;
            _pf.SetDestination(_mc.HomePortalPos);
        }

        // 段② Q1-B（D252）：精英（Brute）挂 NPCBrain——MonsterAI 只做规则态机切换（HP撤退=上方判定 / 守门=OnPortalAttacked），
        // 不做移动/攻击驱动（由 NPCBrain 决策核接管，见 FactorContext 壳层模式开关注入）；切换后把 mode 同步给 NPCBrain。
        if (_mc.IsElite)
        {
            var brain = GetComponent<NPCBrain>();
            if (brain != null) brain.SetMonsterMode(_mc.mode);
            return;
        }

        switch (_mc.mode)
        {
            case MonsterMode.Raiding:  UpdateRaiding();   break;
            case MonsterMode.Guarding: UpdateGuarding();  break;
            case MonsterMode.Retreating: UpdateRetreating(); break;
            case MonsterMode.Looting:  UpdateLooting();   break;
        }
    }

    // ===== Raiding：价值×距离选高价值建筑 → A* 追击 → 到点掠夺 =====
    private void UpdateRaiding()
    {
        // 携带资源回门中：直达传送门，到达即资源被门吸收（不落箱）
        if (_carryingHome)
        {
            _pf.SetDestination(_mc.HomePortalPos);
            if (_mc.IsArrived(_mc.HomePortalPos)) _carryingHome = false;
            return;
        }

        // 红链接：被拦截才战（不主动寻守卫）；近战接触玩家单位→开战（Slinger 远程不贴身不纠缠）
        var prof = _mc.BuildAttackProfile();
        if (!prof.isRanged)
        {
            var contact = _mc.FindNearestHuman(prof.range * CellSize());
            if (contact != null && DistCells(contact.GetPosition()) <= prof.range)
            {
                DamageSystem.Instance.RegisterAttack(_mc, contact, prof);
                return;   // 被拦截先打，暂不推进
            }
        }

        // 价值×距离选高价值建筑（D83，CombatRules.TargetScore 首次接线）
        IDamageable t = PickBuildingTarget();
        if (t == null) { _pf.Stop(); return; }

        if (!ReferenceEquals(t, _raidTarget))
        {
            _raidTarget = t;
            _pf.SetDestination(t.GetPosition());
        }

        float dist = DistCells(t.GetPosition());
        if (dist <= prof.range)
            DamageSystem.Instance.RegisterAttack(_mc, t, prof);   // 袭击建筑

        if (dist <= LootRadiusCells) EnterLooting();             // 到点掠夺
    }

    // ===== Guarding：传送门被打 → 回援 + 攻击门边玩家单位 =====
    private void UpdateGuarding()
    {
        _pf.SetDestination(_mc.HomePortalPos);

        var guard = _mc.FindNearestHuman(_mc.VisionRadiusCells * CellSize());
        var prof = _mc.BuildAttackProfile();
        if (guard != null && DistCells(guard.GetPosition()) <= prof.range)
            DamageSystem.Instance.RegisterAttack(_mc, guard, prof);

        // 抵门且无门边威胁、守门保持期结束 → 回出击
        if (_mc.IsArrived(_mc.HomePortalPos) && guard == null && Time.time >= _guardUntil)
            _mc.mode = MonsterMode.Raiding;
    }

    // ===== Retreating：HP 低 → 退回传送门；被拦截/未脱战 → 继续战斗（无逃脱）=====
    private void UpdateRetreating()
    {
        var foe = _mc.FindNearestHuman(_mc.VisionRadiusCells * CellSize());
        var prof = _mc.BuildAttackProfile();
        // 近战被贴脸 → 继续战斗；远程仍朝门撤（远程无逃脱但可边撤边射的取舍归段②）
        if (!prof.isRanged && foe != null && DistCells(foe.GetPosition()) <= prof.range)
        {
            DamageSystem.Instance.RegisterAttack(_mc, foe, prof);
            return;
        }

        _pf.SetDestination(_mc.HomePortalPos);
        if (_mc.IsArrived(_mc.HomePortalPos)) _mc.mode = MonsterMode.Raiding;   // 到门休整回出击
    }

    // ===== Looting：停留掠夺 → 携带资源回门 =====
    private void UpdateLooting()
    {
        float stay = _mc.def != null ? _mc.def.lootingStaySeconds : 2.5f;
        if (Time.time - _lootStart >= stay)
        {
            _carryingHome = true;      // 携 carryResource 回传送门
            _mc.mode = MonsterMode.Raiding;
            _pf.SetDestination(_mc.HomePortalPos);
        }
    }

    private void EnterLooting()
    {
        _mc.mode = MonsterMode.Looting;
        _lootStart = Time.time;
        _pf.Stop();
    }

    // ===== PortalAttackedEvent → 守门回援（guardRecallRatio，确定性 R4）=====
    private void OnPortalAttacked(PortalAttackedEvent evt)
    {
        if (_mc == null || !_mc.IsAlive) return;
        if (_mc.mode == MonsterMode.Retreating || _mc.mode == MonsterMode.Looting) return;
        if (_mc.mode == MonsterMode.Guarding) { _guardUntil = Time.time + GuardHoldSeconds; return; }
        // 段② Q1-B：精英回援只切 Guarding 态，不直接驱动 PathFollower（NPCBrain 决策核归巢门=HomePortalPos 自动回援）。
        if (_mc.IsElite) { _mc.mode = MonsterMode.Guarding; _guardUntil = Time.time + GuardHoldSeconds; return; }

        float ratio = _mc.def != null ? _mc.def.guardRecallRatio : 0.5f;
        if (Deterministic01(_mc.npcId) < ratio)   // 稳定哈希：ratio 比例回援，禁 UnityEngine.Random
        {
            _mc.mode = MonsterMode.Guarding;
            _guardUntil = Time.time + GuardHoldSeconds;
            _pf.SetDestination(_mc.HomePortalPos);
        }
    }

    // ===== 目标选择 / 距离 / 哈希 工具 =====

    /// <summary>价值×距离选目标：遍历玩家建筑（跳过工事/墙/门），CombatRules.TargetScore(D83) 取最高。</summary>
    private IDamageable PickBuildingTarget()
    {
        if (BuildingRegistry.Instance == null) return null;
        float best = float.NegativeInfinity;
        IDamageable bestB = null;
        var w = _mc.def != null ? _mc.def.valueWeight : 1f;
        var d = _mc.def != null ? _mc.def.targetDistWeight : 1f;

        foreach (var b in BuildingRegistry.Instance.All)
        {
            if (b == null || !b.IsActive) continue;
            if (b.kingdomId != 0) continue;   // 2_16 步骤7 补丁C P0 收窄：怪物只袭玩家王国(0)。AI 无防御单位（人口=台账），避免单向拆 AI；2_17 引入 AI Faction 防御后放开
            if (b.def == null || b.def.monsterTargetValue <= 0f) continue;
            if (b.IsFortification) continue;   // 不打工事（墙/门），掠夺功能建筑
            float val = b.def.monsterTargetValue;
            float score = CombatRules.TargetScore(val, DistCells(b.GetPosition()), w, d);
            if (score > best) { best = score; bestB = b; }
        }
        return bestB;
    }

    /// <summary>与目标的世界距离换算成格距离。</summary>
    private float DistCells(Vector2 b)
    {
        return Vector2.Distance(_mc.transform.position, b) / Mathf.Max(0.001f, CellSize());
    }

    private float CellSize()
    {
        return (GridSystem.Instance != null && GridSystem.Instance.Config != null)
            ? GridSystem.Instance.Config.cellSize.x : 1.28f;
    }

    /// <summary>确定性 [0,1) 哈希（R4：回援选取比例用，禁 UnityEngine.Random）。</summary>
    private static float Deterministic01(int seed)
    {
        unchecked
        {
            uint x = (uint)seed * 2654435761u;
            x ^= x >> 13;
            x *= 0x7feb352du;
            return ((x & 0x7FFFFFFF) % 10000) / 10000f;
        }
    }
}