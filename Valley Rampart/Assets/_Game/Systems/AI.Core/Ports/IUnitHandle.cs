// ============================================================================
//  AI.Core Ports - IUnitHandle 单位句柄（接缝 1/2 的解法）
//  详见 03_大脑提取与双适配工程.md §二 接口草案（照抄可用）
//  核内只认此接口，不引用 UnitController/IDamageable。
//  Unity 侧：UnitController 实现之（IsAlive 包伪 null 检测）；
//  模拟器侧：SimUnit 实现之（直接返字段）。
// ============================================================================

/// <summary>
/// 单位句柄抽象（决策核的单位统一视角）。
/// 替代核内对 UnitController / IDamageable 的直接引用（接缝 1），
/// IsAlive 替代 UnityEngine.Object 伪 null 销毁检测（接缝 2）。
/// </summary>
public interface IUnitHandle
{
    /// <summary>世界坐标位置</summary>
    Vector2X Position { get; }

    /// <summary>阵营</summary>
    Faction Faction { get; }

    /// <summary>
    /// 是否存活/有效。
    /// Unity 实现包 uo != null 伪 null 检测；模拟器实现直接返字段。
    /// </summary>
    bool IsAlive { get; }

    /// <summary>当前血量</summary>
    int CurrentHp { get; }

    /// <summary>最大血量</summary>
    int MaxHp { get; }

    /// <summary>攻击力</summary>
    int Attack { get; }

    /// <summary>防御力</summary>
    int Defense { get; }

    /// <summary>步行速度</summary>
    float WalkSpeed { get; }

    /// <summary>职业属性快照（替代 NpcProfessionDef，接缝 4）</summary>
    ProfessionSnapshot Profession { get; }
}
