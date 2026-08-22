using UnityEngine;

/// <summary>
/// 工人内置背包（QQQ.4 T8，需求5 资源生命周期：采集/搬运先入背包 → 搬运到仓库 → 玩家收取入国库）。
/// 携带量按资源类型（ResourceCarryConfig SO：木/石/矿=10，粮=20，水晶/火油=5，数据驱动，见 3.5 搬运携带量）。
/// 挂 Worker prefab（UnitFactory.SpawnUnit 兜底 AddComponent）；存档由 UnitController v5 代理（carriedType/carriedAmount）。
/// 背包规则：单资源类型不可混装（背包满/类型不符拒绝存储）。
/// </summary>
public class WorkerInventory : MonoBehaviour, IWarehouse
{
    [Tooltip("当前背包资源类型（空背包=默认）")]
    public ResourceType carriedType;

    [Tooltip("当前背包资源量")]
    public int carriedAmount;

    /// <summary>背包是否为空。</summary>
    public bool IsEmpty => carriedAmount <= 0;

    /// <summary>背包是否已满（按当前资源类型容量）。</summary>
    public bool IsFull => carriedAmount >= GetCarryCapacity();

    /// <summary>当前携带容量（按背包资源类型查 ResourceCarryConfig；空背包用默认 10）。</summary>
    public int GetCarryCapacity()
    {
        var cfg = Resources.Load<ResourceCarryConfig>("Config/ResourceCarryConfig");
        return cfg != null ? cfg.GetCarryAmount(carriedType) : 10;
    }

    /// <summary>
    /// 存入资源（同类型可追加；超容量拒绝并返回实际存入量；类型不符返回 0）。
    /// </summary>
    public int TryStore(ResourceType type, int amount)
    {
        if (amount <= 0) return 0;
        if (!IsEmpty && carriedType != type) return 0;   // 单资源背包：不可混装
        int cap = GetCarryCapacity();
        int room = cap - carriedAmount;
        if (room <= 0) return 0;
        int stored = Mathf.Min(amount, room);
        carriedType = type;
        carriedAmount += stored;
        return stored;
    }

    /// <summary>清空背包并返回全部资源量（卸货/入国库用）。</summary>
    public int UnloadAll()
    {
        int amount = carriedAmount;
        carriedAmount = 0;
        return amount;
    }

    // ===== IWarehouse 实现（2_12 步骤3，移动仓库，签名逐字对齐 sim harness/Core）=====
    public ResourceAmount Query() => new ResourceAmount(carriedType, carriedAmount);

    public bool CanTake(ResourceType t, int amt) => !IsEmpty && carriedType == t && carriedAmount >= amt;

    public int Take(ResourceType t, int amt)
    {
        if (!CanTake(t, amt)) return 0;
        int taken = Mathf.Min(amt, carriedAmount);
        carriedAmount -= taken;
        return taken;
    }

    public void Deposit(ResourceType t, int amt) => TryStore(t, amt);

    /// <summary>背包不加工，返回 0（签名对齐用；加工只在加工建筑）。</summary>
    public int Transform(ResourceType @in, ResourceType @out, int amt) => 0;
}
