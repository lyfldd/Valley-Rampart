// ============================================================================
//  AI.Core Ports - IHarvestable 搬运/收取接口（从壳 Building/IHarvestable.cs 迁入）
//  03_大脑提取与双适配工程.md 迁移 6 步·步3：
//  DecisionStructs.BehaviorCommand.HarvestTarget（L3 WorkAt 搬运闭环）引用本接口；
//  asmdef 边界要求（核不能引用壳 Assembly-CSharp 类型）故迁入核。
//  纯 C# 零引擎依赖，非决策逻辑——仅作为核数据结构的端口类型。
// ============================================================================

/// <summary>
/// 搬运/收取接口（3.3.4 批次5 产能闭环）。
/// StorageComponent 实现此接口。首版由玩家手动收取（BuildingPanel"收取"按钮），
/// 3.10 后由 NPC 工人轮询调用同一接口搬运到国库。
/// </summary>
public interface IHarvestable
{
    /// <summary>是否达到可收取阈值。</summary>
    bool IsReadyToHarvest();

    /// <summary>取走全部存储资源转入国库，返回取走量。</summary>
    int Harvest();
}
