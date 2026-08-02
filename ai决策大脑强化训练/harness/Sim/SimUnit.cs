// ============================================================================
//  M2 Headless 模拟器 - SimUnit 模拟单位（IUnitHandle + IMovable 端口实现）
//  04_模拟器规格.md §一：SimUnit[]：x/hp/attack/defense/range/cd/walkSpeed/faction/profession快照
//            / 决策核实例（L1/L2/L3+记忆组件）/ 编队槽位。
//  决策核唯一真身 AI.Core：SimUnit 只实现 Ports 接口（IUnitHandle/IMovable），不复制核代码。
//  无 y 层：位置 y 恒 0（1D 数轴，x 单位=世界坐标）。
// ============================================================================

/// <summary>
/// 模拟单位。IUnitHandle（决策核视角）+ IMovable（BehaviorExecutor 移动出口）。
/// 持有：职业快照 / 当前血量 / 位置 / 决策核实例（SimBrain）/ 执行器（SimExecutor）/ 编队槽位。
/// </summary>
public sealed class SimUnit : IUnitHandle, IMovable
{
    // ===== 静态标识 =====
    public int Id;
    public string ProfessionName;      // 日志 prof 字段
    public bool IsGeneral;             // 编队将军（锚点）

    // ===== 编队槽位（SimFormation 下发）=====
    public int FormationGid = -1;      // -1=无编队
    public bool IsFormationMember;     // 是否编队成员（有槽位绑定）
    public Vector2IntX SlotOffset;     // 槽位偏移（cell 单位）
    public Vector2X HomePoint;         // 归巢点（SafetyStimulus 用）

    // ===== 决策核实例 =====
    public SimBrain Brain;
    public SimExecutor Executor;

    // ===== 运行时状态 =====
    private ProfessionSnapshot _prof;  // faction 已按场景覆盖
    private Vector2X _pos;             // y 恒 0
    private int _hp;
    private bool _alive = true;

    public SimUnit(SimUnitSpec spec)
    {
        Id = spec.Id;
        ProfessionName = spec.ProfessionName;
        IsGeneral = spec.IsGeneral;
        _prof = spec.Profession;
        _pos = new Vector2X(spec.X, 0f);
        _hp = _prof.maxHp;
        HomePoint = new Vector2X(spec.HomeX, 0f);
    }

    // ===== IUnitHandle（决策核统一视角）=====

    public Vector2X Position => _pos;
    public Faction Faction => _prof.faction;
    public bool IsAlive => _alive;
    public int CurrentHp => _hp;
    public int MaxHp => _prof.maxHp;
    public int Attack => _prof.attack;
    public int Defense => _prof.defense;
    public float WalkSpeed => _prof.walkSpeed;
    public ProfessionSnapshot Profession => _prof;

    // ===== IMovable（BehaviorExecutor 移动出口，04 §三 速度插值 Lerp(dt/0.2) 消费方）=====

    /// <summary>朝目标移动 speed×dt（到目标即停；1D 锁 x，y 恒 0 无影响）。</summary>
    public void MoveTowards(Vector2X dest, float speed, float dt)
    {
        _pos = Vector2X.MoveTowards(_pos, dest, speed * dt);
    }

    /// <summary>停止移动（停在原地）。</summary>
    public void Stop()
    {
    }

    // ===== sim 内部写入口 =====

    /// <summary>扣血（SimDamage.ApplyDamage 调用，公式已在 SimDamage 算好）。</summary>
    public void Damage(int amount)
    {
        _hp -= amount;
    }

    /// <summary>标记死亡（hp 归零 + alive=false；Unity 侧销毁，感知/攻击立即跳过）。</summary>
    public void MarkDead()
    {
        _alive = false;
        _hp = 0;
    }

    /// <summary>设置位置（布阵/重置用）。</summary>
    public void SetPosition(Vector2X pos)
    {
        _pos = pos;
    }
}
