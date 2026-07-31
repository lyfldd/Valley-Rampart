using UnityEngine;

/// <summary>
/// 临时攻击驱动器（3.4 P0 第 12 项，3.0.1 完成后删）。
///
/// 职责：查最近敌人 -> DamageSystem.RegisterAttack，无 AI/注意力/威胁/谱系。
/// 斩断 3.4 与 3.0.1 的实现顺序循环，让 3.4 主体可独立验证。
///
/// 挂在 NPC Prefab 上，由验证脚本/UnitFactory 调 Init 注入职业配置。
/// 3.0.1 NPCBrain 完成后替换此脚本。
/// 详见 3.4_伤害管线设计.md 第 1.4 节、决策 25。
/// </summary>
public class StubAttacker : MonoBehaviour
{
    private IDamageable _self;
    private NpcProfessionDef _profession;
    private IDamageable _currentTarget;
    private float _searchTimer;
    private float _searchInterval = 0.5f;

    /// <summary>初始化（由 UnitFactory 或验证脚本调）。</summary>
    public void Init(NpcProfessionDef profession)
    {
        _profession = profession;
        _self = GetComponent<IDamageable>();
    }

    private void Update()
    {
        if (_self == null || _profession == null) return;
        if (_self.CurrentHp <= 0) return;

        // 工人/农民无攻击能力（attack=0 或无职业配置时不攻击）
        if (_profession.attack <= 0) return;

        _searchTimer += Time.deltaTime;
        if (_searchTimer >= _searchInterval)
        {
            _searchTimer = 0f;
            SearchAndRegister();
        }
    }

    /// <summary>搜索最近敌方并注册攻击。</summary>
    private void SearchAndRegister()
    {
        IDamageable target = FindNearestEnemy();

        if (target == null)
        {
            // 无目标，取消注册
            if (_currentTarget != null)
            {
                DamageSystem.Instance?.Unregister(_self);
                _currentTarget = null;
            }
            return;
        }

        // 目标没变，不重复注册（DamageSystem 内部已在 CD 循环）
        if (target == _currentTarget) return;

        // 构造攻击配置
        var profile = new AttackProfile
        {
            attack = _profession.attack,
            range = _profession.attackRange,
            cd = _profession.attackCD,
            isRanged = _profession.isRanged,
            projectileSpeed = _profession.projectileSpeed
        };

        // 注册攻击（DamageSystem 处理首次立即打 + CD + 过度杀伤检查）
        bool success = DamageSystem.Instance != null
            && DamageSystem.Instance.RegisterAttack(_self, target, profile);

        if (success)
            _currentTarget = target;
        // 注册失败（过度杀伤已满），_currentTarget 不更新，下轮重新搜索
    }

    /// <summary>查攻击范围内最近的敌方单位（复用 UnitRegistry）。</summary>
    private IDamageable FindNearestEnemy()
    {
        if (UnitRegistry.Instance == null) return null;

        var enemies = UnitRegistry.Instance.GetEnemies(_self.GetFaction());
        if (enemies == null || enemies.Count == 0) return null;

        float rangeWorld = _profession.attackRange * GetCellSize();
        IDamageable nearest = null;
        float minDist = float.MaxValue;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.CurrentHp <= 0) continue;

            float dist = Vector2.Distance(_self.GetPosition(), enemy.GetPosition());
            if (dist <= rangeWorld && dist < minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }

        return nearest;
    }

    private float GetCellSize()
    {
        return GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize : 2.26f;
    }
}
