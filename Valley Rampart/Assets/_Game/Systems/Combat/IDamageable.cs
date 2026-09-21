using UnityEngine;

/// <summary>
/// 可受伤抽象接口（3.4 伤害管线核心契约）。
/// UnitController 和 Building 都实现此接口，统一走 DamageSystem 集中调度。
///
/// 职责边界：
///   - TakeDamage(int finalDamage, IDamageable source = null) 只负责扣血，不做伤害计算（公式在 DamageSystem）
///   - Heal(int) 预留接口，UnitController 已实装，Building 补空实现（建筑不回血）
///   - HP 链路全 int（UI/存档零影响），float 只在 DamageSystem 内部运算
///   - Defense 供 DamageSystem 百分比减伤计算（护甲/(护甲+K)），复用 UnitData.defense / CombatConfig.defense
///
/// 详见 3.4_伤害管线设计.md 第 6.1 节、决策 5。
/// </summary>
public interface IDamageable
{
    /// <summary>当前血量（int）。</summary>
    int CurrentHp { get; }

    /// <summary>最大血量（int）。</summary>
    int MaxHp { get; }

    /// <summary>护甲值（供 DamageSystem 百分比减伤计算，复用 defense 字段）。</summary>
    int Defense { get; }

    /// <summary>世界坐标位置（用于空间分区查目标、投射物到达检测）。</summary>
    Vector2 GetPosition();

    /// <summary>阵营（用于敌我识别/Faction 二元判定）。</summary>
    Faction GetFaction();

    /// <summary>
    /// 受到伤害，只扣血。伤害已由 DamageSystem 算好+取整（见 5.3），此处不做公式。
    /// 血量≤0 时触发 Die。
    /// ⭐ `M1-D`（`D802` `Q1`）：`source` ＝ **致死一击的施加者**（可选 · 默认 null）——
    ///   战斗路径由 `DamageSystem.ApplyDamage`（`:412` 主路径 / `:406` 盾卫转移）传入；
    ///   环境/持续伤害/调试处决/拆除路径 ⇒ null（⇒ 无 source 调用点编译零改动）。
    /// </summary>
    void TakeDamage(int finalDamage, IDamageable source = null);

    /// <summary>
    /// 恢复血量。UnitController 已实装（含存活校验+UnitHpChangedEvent）；
    /// Building 补空实现（建筑首版不回血，后续对接资源系统）。
    /// </summary>
    void Heal(int amount);
}
