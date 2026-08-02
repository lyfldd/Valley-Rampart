// ============================================================================
//  M2 Headless 模拟器 - SimWorld 世界编排（8 步 tick 循环 + IWorldQuery + 胜负判定）
//  04_模拟器规格.md §二：tick 循环（每 0.1s 固定顺序——判定顺序是保真的命脉）：
//    1. clock 步进
//    2. 感知刷新（每 2 tick=0.2s）：线性扫 SimGrid，阵营过滤+距离精判（复刻 PerceptionSystem）
//    3. think（每 tick；v0 不分片）：BuildBaseContext → 记忆Tick → L1 → 因子补算 → L2 → rawFactor → L3
//    4. 攻击注册/注销（复刻 UpdateCombatRegistration 的射程判定）
//    5. 伤害时间轮：CD 到点者结算（近战即时 / 远程 hitscan 延迟 1 tick）
//    6. 移动积分：executor.Execute(dt=0.1) → x += speed×dt（锁 1D）+ 抵达判定 0.3×cellSize
//    7. 死亡结算：hp≤0 → 移除 + overkill 表清理 + 编队减员通知
//    8. 事件落盘：JSONL append
//  确定性（04 §七）：
//    - 布阵 RNG（runSeed=场景seed+局号，决策点 2，仅开局抖动）/ 决策 RNG（场景 seed，漫游）
//    - 单位按 spawn 序遍历；结算按注册顺序；JSONL InvariantCulture 定长浮点
//  胜负判定：annihilation（一方全灭）；maxDuration 到按存活数（多者胜，相同平局）。
// ============================================================================

using System.Collections.Generic;

/// <summary>单局运行结果（acceptance/determinism 聚合用）。</summary>
public sealed class SimRunResult
{
    public string Winner;              // "Human" / "Undead" / "Draw"
    public float Duration;             // 结束时刻（秒）
    public int AliveHuman;
    public int AliveUndead;
    public float ArcherGrappledTime;   // 弓手被贴身总时长（秒）
    public int ArcherAlive;            // 局末 Human 弓手存活数（白嫖存活率指标）
    public float SlotDevMean;          // 槽位偏差均值（世界单位）
    public int AbandonChaseCount;      // 放弃追击次数
    public int TotalAttacks;           // 攻击命中次数（attack 事件数）
    public int KillsHuman;             // 人类杀敌数
    public int KillsUndead;            // 亡灵杀敌数
    // ===== M3 扩展（06 §M3 / 04 §八 行为类指标）=====
    public int RetreatTacticalCount;   // 战术短撤次数（谱系4 tactical）
    public int RetreatStrategicCount;  // 战略撤退次数（谱系4 strategic）
    public float RetreatFirstTime;     // 首次撤退时刻（无撤退 = -1）
    public int FormationBreakCount;    // 编队解散（破阵）次数（D4，全编队聚合）
    public float FormationBreakFirstTime; // 首次破阵时刻（无破阵 = -1）
    public Dictionary<string, int> DeathsByProfession; // 死亡职业分布（M3 report behavior 块）
}

/// <summary>
/// 世界编排（1D 数轴，x 单位=世界坐标，cellSize=2.26）。
/// 每局一个实例（Run() 执行整局），内部组件全部 per-run 新建（无跨局状态泄漏）。
/// </summary>
public sealed class SimWorld
{
    private readonly SimConfig _config;
    private readonly SimScenarioData _scenario;
    private readonly int _runIndex;
    private readonly string _logPath;

    /// <summary>调试追踪（每 50 tick 打印存活单位位置/焦点/谱系；仅 run0 开启，验收数据不受影响）。</summary>
    public bool DebugTrace;

    // ===== 组件 =====
    private SimClock _clock;
    private SimGrid _grid;
    private SimHeat _heat;
    private SimEventBus _events;
    private SimWorldQueryAdapter _world;
    private SimDamage _damage;
    private SimLogger _logger;

    private SimRng _rngFormation;   // 布阵 RNG（runSeed = 场景seed + 局号，决策点 2）
    private SimRng _rngDecision;    // 决策 RNG（场景 seed，漫游；04 §七）

    // ===== 单位（spawn 序遍历，04 §七）=====
    private readonly List<SimUnit> _units = new List<SimUnit>();
    private readonly List<SimFormation> _formations = new List<SimFormation>();

    // ===== 每局统计 =====
    private string _winner = "Draw";
    private float _duration;
    private bool _ended;
    private float _grappledTime;
    private float _slotDevAcc;
    private int _slotDevN;
    private int _abandonCount;
    private int _attackCount;
    private int _killsHuman;
    private int _killsUndead;

    // ===== M3 扩展统计（行为类指标，04 §八）=====
    private int _retreatTactical;
    private int _retreatStrategic;
    private float _retreatFirstTime = -1f;
    private int _formationBreakCount;
    private float _formationBreakFirstTime = -1f;
    private readonly Dictionary<string, int> _deathsByProfession = new Dictionary<string, int>();

    // 谱系切换跟踪（跨 tick 状态）
    private readonly Dictionary<SimUnit, BehaviorSpectrum> _prevSpectrum = new Dictionary<SimUnit, BehaviorSpectrum>();
    private readonly Dictionary<SimUnit, bool> _abandonActive = new Dictionary<SimUnit, bool>();

    public SimWorld(SimConfig config, SimScenarioData scenario, int runIndex, string logPath)
    {
        _config = config;
        _scenario = scenario;
        _runIndex = runIndex;
        _logPath = logPath;
    }

    // ===== 装配（每局一次）=====

    private void Build()
    {
        // 决策点 2：每局 seed = 场景seed + 局号（布阵级 RNG，非决策可见）
        int runSeed = _scenario.Seed + _runIndex;
        // 初始遍历方向 = run seed 奇偶（消除固定时序先手，见 IterateUnits 注释）
        _forward = (runSeed & 1) == 0;

        _clock = new SimClock(_config.damageTickInterval);
        _grid = new SimGrid(_config.cellSize, _config.midRegionCellCount, _config.cellStackLimit);
        _heat = new SimHeat(_config.cellSize, _config.midRegionCellCount,
                            _config.tuning.heatHitGain, _config.tuning.heatEnemyEnterGain,
                            _config.tuning.heatDecayRate);
        _events = new SimEventBus();
        _world = new SimWorldQueryAdapter(_grid, _heat, _clock);
        _damage = new SimDamage(_config, _clock, _events, _heat);
        _rngFormation = new SimRng(runSeed);
        _rngDecision = new SimRng(_scenario.Seed);

        // 热度范围初始化（覆盖单位分布的中区块）
        float minX = float.MaxValue, maxX = float.MinValue;
        foreach (var spec in _scenario.Units)
        {
            if (spec.X < minX) minX = spec.X;
            if (spec.X > maxX) maxX = spec.X;
        }
        _heat.InitRange(minX, maxX);

        // 单位创建（spawn 序；布阵抖动 ±0.5 格，决策点 2）
        foreach (var spec in _scenario.Units)
        {
            var unit = new SimUnit(spec);
            // 布阵级 RNG（非决策可见）：开局位置抖动 ±0.5 格（决策点 2，100 局方差来源）
            float jitter = _rngFormation.Range(-0.5f, 0.5f) * _config.cellSize;
            unit.SetPosition(new Vector2X(unit.Position.x + jitter, 0f));

            var brain = new SimBrain(unit, _config.tuning, _world, _clock, _damage);
            unit.Brain = brain;
            unit.Executor = new SimExecutor(unit, brain, _rngDecision, _config.tuning);
            brain.Executor = unit.Executor;

            _units.Add(unit);
            _grid.TryEnter(unit, unit.Position.x);
        }

        // 编队装配（将军 = 锚点，不进成员列表——对应 FormationController.RecruitStandard 只招士兵）
        foreach (var fdata in _scenario.Formations)
        {
            SimUnit general = fdata.GeneralUnitId >= 0 ? FindUnit(fdata.GeneralUnitId) : null;
            var formation = new SimFormation(
                fdata.Gid, fdata.Faction, general, fdata.Slots, fdata.Direction,
                fdata.DefaultIntent, fdata.IntentScript, _config.tuning, _clock, _events, _heat);
            foreach (var spec in _scenario.Units)
            {
                // 将军不进成员（锚点自跟随偏移会死锁：成员目标=锚点+偏移，锚点=自身）
                if (spec.FormationGid == fdata.Gid && !spec.IsGeneral)
                    formation.AddMember(FindUnit(spec.Id));
            }
            formation.InitScript();
            formation.ApplyFormation();
            _formations.Add(formation);
        }

        // 事件订阅
        _events.UnitDied += OnUnitDied;
        _events.UnitDamaged += OnUnitDamaged;
        _events.Spectrum += OnSpectrum;
        _events.Retreat += OnRetreat;
        _events.FormationIntent += OnFormationIntent;
        _events.AbandonChase += OnAbandonChase;
        _events.FormationBreak += OnFormationBreak;

        _logger = new SimLogger(_logPath);
        _logger.RunStart(_scenario.Name, _scenario.Seed, _runIndex, CountAlive(Faction.Human_Player), CountAlive(Faction.Undead));
    }

    // ===== 主循环 =====

    /// <summary>跑完整局，返回结果。</summary>
    public SimRunResult Run()
    {
        Build();

        float dt = _config.damageTickInterval;
        while (!_ended)
        {
            Tick(dt);
        }

        _logger.RunEnd(_duration, _winner, CountAlive(Faction.Human_Player), CountAlive(Faction.Undead));
        _logger.Flush();
        _logger.Dispose();

        return new SimRunResult
        {
            Winner = _winner,
            Duration = _duration,
            AliveHuman = CountAlive(Faction.Human_Player),
            AliveUndead = CountAlive(Faction.Undead),
            ArcherGrappledTime = _grappledTime,
            ArcherAlive = CountAliveArchers(Faction.Human_Player),
            SlotDevMean = _slotDevN > 0 ? _slotDevAcc / _slotDevN : 0f,
            AbandonChaseCount = _abandonCount,
            TotalAttacks = _attackCount,
            KillsHuman = _killsHuman,
            KillsUndead = _killsUndead,
            RetreatTacticalCount = _retreatTactical,
            RetreatStrategicCount = _retreatStrategic,
            RetreatFirstTime = _retreatFirstTime,
            FormationBreakCount = _formationBreakCount,
            FormationBreakFirstTime = _formationBreakFirstTime,
            DeathsByProfession = new Dictionary<string, int>(_deathsByProfession),
        };
    }

    /// <summary>8 步 tick 循环（04 §二 固定顺序——判定顺序是保真的命脉）。
    /// 单位遍历方向每 tick 翻转：消除 spawn 序引入的系统性先手（S1 镜像不对称根因），
    /// 模拟 Unity MonoBehaviour Update 顺序不定的无系统先手；方向翻转是确定性模式（同 seed 同结果）。</summary>
    private void Tick(float dt)
    {
        // 1. clock 步进
        _clock.Step();
        float now = _clock.Now;

        // 2. 感知刷新（每 2 tick=0.2s；SimBrain 内部节流）
        IterateUnits(u => u.Brain.UpdatePerception(dt));

        // 3. think（每 tick；v0 不分片全量）
        IterateUnits(u => u.Brain.ThinkCore());

        // 4. 攻击注册/注销（复刻 UpdateCombatRegistration 的射程判定）
        IterateUnits(u => u.Brain.UpdateCombatRegistration());

        // 5. 伤害时间轮：CD 到点者结算（近战即时 / 远程 hitscan 延迟 1 tick）
        _damage.Tick(dt);

        // 6. 移动积分：executor.Execute(dt=0.1) → 锁 1D + 抵达判定 0.3×cellSize
        IterateUnits(u => u.Executor.Execute(in u.Brain.LastCmd, dt, _config.cellSize));

        // 6b. 空间分区同步：单位移动后更新所在格（Unity GridSystem.TryEnter 由 UnitController 移动后调用；
        //     感知粗筛依赖格子索引，不同步会导致"移动单位不可感知"——S2 残兵互瞪平局的根因）
        IterateUnits(u => _grid.TryEnter(u, u.Position.x));

        // 7. 死亡结算收尾（网格/编队/注册表已在第 5 步事件里即时清理）
        //    + 谱系/撤退/放弃追击事件 + 弓手贴身采样 + 编队脚本推进 + 胜负判定
        for (int i = 0; i < _formations.Count; i++)
            _formations[i].AdvanceScript(now);
        for (int i = 0; i < _formations.Count; i++)
            _formations[i].Update();
        SampleBrainEvents(now);
        SampleGrapple(dt);
        if (CheckEnd(now)) { _ended = true; return; }

        // 8. 事件落盘：tick 采样（每 tick）
        WriteTickSample(now);

        if (DebugTrace && _clock.TickCount % 50 == 0)
            DebugPrint(now);
    }

    /// <summary>调试追踪：打印存活单位位置/焦点/谱系（分析期排查用，非验收数据）。</summary>
    private void DebugPrint(float now)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("[debug t=").Append(now.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append("]");
        for (int i = 0; i < _units.Count; i++)
        {
            var u = _units[i];
            if (!u.IsAlive) continue;
            sb.Append(" id=").Append(u.Id)
              .Append("(").Append(u.ProfessionName).Append(")x=").Append(u.Position.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))
              .Append(" hp=").Append(u.CurrentHp)
              .Append(" focus=").Append(u.Brain.LastContext.FocusDecision.Type)
              .Append(" mod=").Append(u.Brain.LastCmd.Module)
              .Append(" arr=").Append(u.Executor.ArrivedAtFocus)
              .Append(" spec=").Append(u.Brain.CurrentSpectrum);
        }
        System.Console.WriteLine(sb.ToString());
    }

    // ===== 事件采样（第 7 步）=====

    /// <summary>单位遍历方向（forward/backward 交替）。
    /// 初始方向由 run seed 奇偶决定：overkill 锁定等"顺序敏感"机制的战斗开始时刻方向被随机化，
    /// 100 局中 Human/Undead 先手机会均等（消除 spawn 序固定先手——S1 镜像不对称根因），
    /// 同 run seed 同方向（确定性，04 §七）。</summary>
    private bool _forward;

    private void IterateUnits(System.Action<SimUnit> action)
    {
        if (_forward)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                var u = _units[i];
                if (u.IsAlive) action(u);
            }
        }
        else
        {
            for (int i = _units.Count - 1; i >= 0; i--)
            {
                var u = _units[i];
                if (u.IsAlive) action(u);
            }
        }
        _forward = !_forward;   // 每步翻转（感知/think/注册/移动各自独立翻转，tick 内总体对称）
    }

    private void SampleBrainEvents(float now)
    {
        for (int i = 0; i < _units.Count; i++)
        {
            var u = _units[i];
            if (!u.IsAlive) continue;

            var ctx = u.Brain.LastContext;
            BehaviorSpectrum cur = u.Brain.CurrentSpectrum;

            // 谱系切换（RecordSwitchHistory 语义；首次无 from，跳过）
            if (u.Brain.RecordSwitchHistory() && _prevSpectrum.TryGetValue(u, out var prev))
            {
                _events.Publish(new SimSpectrumEvent { Unit = u, From = prev, To = cur });
                _logger.Spectrum(now, u, prev.ToString(), cur.ToString(), ctx.ThreatFactor, ctx.SafetyFactor);
            }
            _prevSpectrum[u] = cur;

            // 撤退事件（谱系 4 边沿）
            if (cur == BehaviorSpectrum.FullRetreat
                && _prevSpectrum.TryGetValue(u, out var p2) && p2 != BehaviorSpectrum.FullRetreat)
            {
                bool tactical = ctx.PostureDecision.IsTacticalRetreat;
                string reason = ctx.HitCount >= u.Profession.maxHitCount ? "hitCount>=max" : "threat";
                _events.Publish(new SimRetreatEvent { Unit = u, IsTactical = tactical, Reason = reason });
                _logger.Retreat(now, u, tactical ? "tactical" : "strategic", reason);
            }

            // 放弃追击（AbandonTaskFactor 升越 abandonThreshold 边沿；3.0.1_8 §六）
            bool abandoning = ctx.AbandonTaskFactor > _config.tuning.abandonThreshold;
            bool was = _abandonActive.TryGetValue(u, out bool existed) && existed;
            if (abandoning && !was)
            {
                _abandonActive[u] = true;
                _abandonCount++;
                _events.Publish(new SimAbandonChaseEvent { Unit = u });
                _logger.AbandonChase(now, u);
            }
            else if (!abandoning && was)
            {
                _abandonActive[u] = false;
            }
        }
    }

    /// <summary>弓手被贴身采样（S2 指标：敌方近战进入攻击距离即计被贴身时长）。</summary>
    private void SampleGrapple(float dt)
    {
        for (int i = 0; i < _units.Count; i++)
        {
            var unit = _units[i];
            if (!unit.IsAlive || !unit.Profession.isRanged) continue;   // 只统计弓手

            bool grappled = false;
            for (int j = 0; j < _units.Count && !grappled; j++)
            {
                var e = _units[j];
                if (!e.IsAlive || e.Faction == unit.Faction || e.Profession.isRanged) continue;
                float rangeWorld = e.Profession.attackRange * _config.cellSize;
                if (Vector2X.Distance(unit.Position, e.Position) <= rangeWorld)
                    grappled = true;
            }
            if (grappled)
                _grappledTime += dt;
        }
    }

    /// <summary>tick 采样落盘（第 8 步）：槽位偏差均值 + 双方存活数。</summary>
    private void WriteTickSample(float now)
    {
        float slotDev = 0f;
        int n = 0;
        for (int i = 0; i < _formations.Count; i++)
        {
            for (int j = 0; j < _formations[i].Members.Count; j++)
            {
                var m = _formations[i].Members[j];
                if (m.Unit == null || !m.Unit.IsAlive) continue;
                float slotWorldX = m.Unit.Brain.ResolveFormationSlotWorld().x;
                slotDev += System.Math.Abs(m.Unit.Position.x - slotWorldX);
                n++;
            }
        }
        if (n > 0)
        {
            _slotDevAcc += slotDev / n;
            _slotDevN++;
        }
        float mean = n > 0 ? slotDev / n : 0f;
        _logger.Tick(now, mean, CountAlive(Faction.Human_Player), CountAlive(Faction.Undead));
    }

    // ===== 事件处理（订阅）=====

    private void OnUnitDied(SimUnitDiedEvent evt)
    {
        _grid.Exit(evt.Unit);
        for (int i = 0; i < _formations.Count; i++)
            _formations[i].OnUnitDied(evt);

        if (evt.Killer != null)
        {
            if (evt.Killer.Faction == Faction.Human_Player) _killsHuman++;
            else if (evt.Killer.Faction == Faction.Undead) _killsUndead++;
        }

        // M3：死亡职业分布（report behavior 块；聚合端 SortedDictionary 固定序）
        _deathsByProfession.TryGetValue(evt.Unit.ProfessionName, out int d);
        _deathsByProfession[evt.Unit.ProfessionName] = d + 1;

        _logger.UnitDied(_clock.Now, evt.Unit, evt.Killer);
    }

    private void OnUnitDamaged(SimUnitDamagedEvent evt)
    {
        // 受击事件（节流后）-> HitCooldown + 受击溯源（NPCBrain.OnDamaged 路径）
        // attack 事件：每次 ApplyDamage 记录（overkill=目标被近战锁定数>1）
        evt.Victim.Brain.OnDamaged(evt.Source as SimUnit);
        _attackCount++;
        _logger.Attack(_clock.Now, evt.Source, evt.Victim, evt.Damage,
                       _damage.GetOverkillCount(evt.Victim) > 1);
    }

    private void OnSpectrum(SimSpectrumEvent evt) { /* 已直接写日志，占位 */ }

    /// <summary>撤退计数 + 首次时刻（M3 行为指标；谱系 4 边沿已在 SampleBrainEvents 触发）。</summary>
    private void OnRetreat(SimRetreatEvent evt)
    {
        if (evt.IsTactical) _retreatTactical++;
        else _retreatStrategic++;
        if (_retreatFirstTime < 0f) _retreatFirstTime = _clock.Now;
    }

    /// <summary>破阵计数 + 首次时刻（M3 D4；编队将军阵亡 -> DisbandAll 发布）。</summary>
    private void OnFormationBreak(SimFormationBreakEvent evt)
    {
        _formationBreakCount++;
        if (_formationBreakFirstTime < 0f) _formationBreakFirstTime = evt.Time;
    }

    private void OnAbandonChase(SimAbandonChaseEvent evt) { /* 已直接写日志，占位 */ }

    private void OnFormationIntent(SimFormationIntentEvent evt)
    {
        _logger.FormationIntent(_clock.Now, evt.Gid, evt.Intent.ToString(), evt.Heat, evt.Value);
    }

    // ===== 胜负判定（annihilation / maxDuration）=====

    private bool CheckEnd(float now)
    {
        int h = CountAlive(Faction.Human_Player);
        int u = CountAlive(Faction.Undead);

        if (h == 0 || u == 0)
        {
            _winner = h > 0 ? "Human" : (u > 0 ? "Undead" : "Draw");
            _duration = now;
            return true;
        }
        if (now >= _scenario.MaxDuration)
        {
            _winner = h > u ? "Human" : (u > h ? "Undead" : "Draw");
            _duration = _scenario.MaxDuration;
            return true;
        }
        return false;
    }

    // ===== 辅助 =====

    private int CountAlive(Faction faction)
    {
        int count = 0;
        for (int i = 0; i < _units.Count; i++)
        {
            var u = _units[i];
            if (u.IsAlive && u.Faction == faction) count++;
        }
        return count;
    }

    /// <summary>统计指定阵营存活弓手数（S2 白嫖存活率指标）。</summary>
    private int CountAliveArchers(Faction faction)
    {
        int count = 0;
        for (int i = 0; i < _units.Count; i++)
        {
            var u = _units[i];
            if (u.IsAlive && u.Faction == faction && u.Profession.isRanged) count++;
        }
        return count;
    }

    private SimUnit FindUnit(int id)
    {
        for (int i = 0; i < _units.Count; i++)
            if (_units[i].Id == id) return _units[i];
        return null;
    }
}
