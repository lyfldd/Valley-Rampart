using UnityEngine;

// ============================================================================
//  王国脑工厂（2_17 步骤8，D337）
//  王国创建钩子统一经此：new KingdomBrain → Subscribe → Registry.Register → 播报事件。
//  KingdomFoundry 两处（FoundFirstGeneration / FoundFromCamp）各调用一次。
//  玩家(id=0)短路：不建脑、不注册（D338；KingdomRegistry 永不含 id=0，Registry 也如此）。
// ============================================================================

public static class KingdomBrainFactory
{
    /// <summary>
    /// 为某王国创建王国脑（玩家 id≤0 返回 null 不建脑，D338）。
    /// 日 tick 由 DayCycleSettlement 五步②驱动王国脑（D337；非自挂 Update）。
    /// </summary>
    public static KingdomBrain Create(int kingdomId)
    {
        if (kingdomId <= 0) return null;   // 玩家无脑（D338，短路①）

        var brain = new KingdomBrain(kingdomId);
        brain.Subscribe();   // 订阅被攻击事件（D340；Unsubscribe 由 Unregister 成对）

        var registry = KingdomBrainRegistry.Instance;
        if (registry != null)
            registry.Register(brain);

        if (brain.kingdomId > 0 && EventBus.HasSubscribers<KingdomBrainCreatedEvent>())
            EventBus.Publish(new KingdomBrainCreatedEvent(kingdomId));

        Debug.Log($"[KingdomBrainFactory] 王国(k{kingdomId}) 脑已创建并注册。");
        return brain;
    }
}