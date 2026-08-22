/// <summary>资源存量（单一资源查询）。用于 IWarehouse.Query。</summary>
public struct ResourceAmount
{
    public ResourceType type;
    public int amount;

    public ResourceAmount(ResourceType t, int a) { type = t; amount = a; }
}

/// <summary>仓库抽象：资源不凭空位移，一切获取/消耗走仓库操作。</summary>
/// <remarks>
/// 实现：王国仓库（StorageComponent，iWarehouse 侧）、资源容器（树/石堆/木堆/矿脉）、工人背包（WorkerInventory，移动仓库）。
/// 同源契约：与 sim harness/Core 的 IWarehouse **签名逐字对齐**（2_9 sim 对拍 + 2_12 D43/D51/D255）。单侧改签名必须记 HH 回策划。
/// </remarks>
public interface IWarehouse
{
    ResourceAmount Query();                    // 存量查询
    bool CanTake(ResourceType type, int amt);  // 可否取
    int Take(ResourceType type, int amt);      // 减（Remove）：从本仓库拿走（金币=瞬时即取即用，无搬运）
    void Deposit(ResourceType type, int amt);  // 增（Add）：NPC 从别处拿进来（泛型"拿"Transfer）
    int Transform(ResourceType @in, ResourceType @out, int amt); // 改（Transform）：就地加工增值
}