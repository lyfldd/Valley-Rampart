using UnityEngine;

/// <summary>
/// 兽人·狂战士击杀狂暴 buff（2_20 M7 D490 范式标杆：数值倾向+1 钩子+弱点，禁玩法剧本）。
/// 订阅 UnitDiedEvent：击杀者==自身 && Cause==Killed → 狂暴层数+1（上限 3），持续 5s 刷新时长。
/// 效果：攻速+30%/层（NPCBrain 构造 AttackProfile 时 cd 乘 CdMul）、移速+20%/层（UnitController.EffectiveSpeed 乘 SpeedMul）。
/// 用途开放（进攻/断后/守家皆可）=不规定玩法；数值全 SO 占位口径（2_20.1 §6.1 P0 调优）。
/// 组件由 UnitController.Initialize 挂载（occupation==Berserker 时），随单位生命周期回收。
/// </summary>
public class BerserkerFrenzy : MonoBehaviour
{
    public const int MaxStacks = 3;
    public const float Duration = 5f;
    public const float SpeedBonusPerStack = 0.2f;   // 移速 +20%/层
    public const float CdBonusPerStack = 0.3f;      // 攻速 +30%/层（cd ×(1-0.3n)）

    private int _stacks;
    private float _until;

    /// <summary>有效期内狂暴层数（过期视为 0——探针/显示只认生效层）。</summary>
    public int Stacks => _stacks > 0 && Time.time < _until ? _stacks : 0;

    private void OnEnable()
    {
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
    }

    private void OnUnitDied(UnitDiedEvent evt)
    {
        if (evt.Cause != DeathCause.Killed) return;                 // 只有战斗击杀触发（饿死/拆除不触发）
        if (evt.Killer == null) return;
        if (!ReferenceEquals(evt.Killer, GetComponent<UnitController>())) return;   // 击杀者必须=自身
        _stacks = Mathf.Min(MaxStacks, _stacks + 1);
        _until = Time.time + Duration;                              // 叠层刷新时长
    }

    /// <summary>移速乘数（狂暴生效时 1+0.2n，否则 1）。</summary>
    public float SpeedMul()
    {
        return Stacks > 0 ? 1f + Stacks * SpeedBonusPerStack : 1f;
    }

    /// <summary>攻速 CD 乘数（狂暴生效时 1-0.3n 加速，否则 1）。</summary>
    public float CdMul()
    {
        return Stacks > 0 ? Mathf.Max(0.1f, 1f - Stacks * CdBonusPerStack) : 1f;
    }
}
