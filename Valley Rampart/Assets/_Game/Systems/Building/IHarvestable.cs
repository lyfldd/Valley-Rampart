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
