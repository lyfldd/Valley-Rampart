using System;
using UnityEngine;

/// <summary>
/// 搬运携带量配置（3.5.3 §3.1 / 3.5_前置缺口 §2.2；P1-8）。
/// 担当文档所称「ResourceCarryDef SO」：按资源类型区分工人一次搬运量。
///   木/石/矿 = 10，粮 = 20，水晶/火油 = 5，面包(SpecialFood)/肉 = 10。
/// 数据驱动（so-data-driven 铁律）：携带量为可调数值，进 SO，禁止硬编码。
/// WorkerTask 与 StorageComponent 经 Resources.Load 查本表填充携带量。
/// 资产路径：Resources/Config/ResourceCarryConfig.asset。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/ResourceCarryConfig", fileName = "ResourceCarryConfig")]
public class ResourceCarryConfig : ScriptableObject
{
    [Tooltip("各资源类型一次搬运携带量（木/石/矿=10，粮=20，水晶/火油=5，面包/肉=10）")]
    public ResourceCarryEntry[] entries;

    [Tooltip("未配置资源类型的默认携带量（兜底，防查表返回 0 导致搬运停滞）")]
    public int defaultCarryAmount = 10;

    /// <summary>查某资源类型一次搬运携带量；未配置返回 defaultCarryAmount（>0 保证）。</summary>
    public int GetCarryAmount(ResourceType type)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].resourceType == type) return Mathf.Max(1, entries[i].carryAmount);
        }
        return Mathf.Max(1, defaultCarryAmount);
    }
}

/// <summary>单条携带量定义（资源类型 → 携带量）。</summary>
[Serializable]
public struct ResourceCarryEntry
{
    public ResourceType resourceType;
    public int carryAmount;
}