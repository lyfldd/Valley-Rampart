using UnityEngine;

// 2_14 普通怪行为驱动器（实施计划步骤6 / D252：普通怪挂 MonsterAI，不进决策核训练）。
// 步骤6 最小行为：出击——感知内最近玩家单位，进射程肉搏/远程，否则追击。
//   守门/掠夺/撤退/价值×距离目标选择（MonsterMode 完整态机）= 步骤7。
// 攻击：射程内 RegisterAttack，DamageSystem 按 CD tick 驱动后续攻击（镜像 UnitController.StaticAttackThink）。
// 确定性 R4：无 UnityEngine.Random / 事件序外状态，属性纯读 MonsterDef。
[RequireComponent(typeof(MonsterController))]
public class MonsterAI : MonoBehaviour
{
    private MonsterController _mc;

    private void Awake() => _mc = GetComponent<MonsterController>();

    private void Update()
    {
        if (_mc == null || !_mc.IsAlive || DamageSystem.Instance == null) return;

        float cell = (GridSystem.Instance != null && GridSystem.Instance.Config != null)
            ? GridSystem.Instance.Config.cellSize.x : 1.28f;

        IDamageable target = _mc.FindNearestHuman(_mc.VisionRadiusCells * cell);
        if (target == null) return;   // 无目标待命（出击目标选择=步骤7）

        var profile = _mc.BuildAttackProfile();
        float attackRangeWorld = profile.range * cell;

        if (Vector2.Distance(_mc.transform.position, target.GetPosition()) <= attackRangeWorld)
        {
            // 射程内开火（远程保持，近战慢推贴身）
            DamageSystem.Instance.RegisterAttack(_mc, target, profile);
            if (!profile.isRanged)
                _mc.MoveTowards(target.GetPosition(), speedOverride: _mc.WalkSpeed * 0.1f);
        }
        else
        {
            _mc.MoveTowards(target.GetPosition());
        }
    }
}