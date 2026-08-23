using System;
using UnityEngine;

/// <summary>
/// 市场贸易全局数值配置（§七 / D216~D220，so-data-driven 铁律）。
/// 贸易全部参数集中于本 SO，禁硬编码魔法数值。从 KingdomConfig 迁出（2026-08-23 步骤10）。
/// 资产路径：Resources/Config/TradeConfig.asset，Play Mode 用 Resources.Load 加载。
/// 档位 1~13：1粮/2木/3石/4矿/5金/6水晶/7火油/8特食/9肉/10Metal/11石弹/12火弹/13魔弹。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/TradeConfig", fileName = "TradeConfig")]
public class TradeConfig : ScriptableObject
{
    [Header("市场贸易（§七/D216~D220）")]
    [Tooltip("默认卖出回退：多少单位换 1 金（粮 4）")]
    public int foodToGoldIn = 4;
    [Tooltip("默认买入回退：1 金换多少单位（粮 3）")]
    public int goldToFoodOut = 3;
    [Tooltip("各资源档位卖出兑换率（档位-1 索引，共 13）：多少单位换 1 金（粮4，越高级越少）D217 固定基准价")]
    public int[] tradeSellUnitsPerGold;
    [Tooltip("各资源档位买入兑换率（档位-1 索引，共 13）：1 金换多少单位（粮3）D217")]
    public int[] tradeBuyUnitsPerGold;
    [Tooltip("市场等级可交易最高资源档位（市场等级-1 索引；Lv↑买便宜卖贵=D217 等级修正；顶到 13）D218")]
    public int[] marketMaxTradeLevel = { 4, 9, 13 };
    [Tooltip("各资源档位每日买卖额度（档位-1 索引，共 13；D218 额度 + D220 每日全量重置）")]
    public TradeQuotaDef[] merchantQuotas;

    /// <summary>获取资源档位（1..13）对应的贸易额度配置。</summary>
    public TradeQuotaDef GetQuota(int resourceLevel)
    {
        if (resourceLevel < 1 || resourceLevel > 13) return TradeQuotaDef.Zero;
        if (merchantQuotas == null || resourceLevel - 1 >= merchantQuotas.Length) return TradeQuotaDef.Zero;
        return merchantQuotas[resourceLevel - 1];
    }

    /// <summary>卖出兑换率：档位 → 多少单位换 1 金（默认粮 4）。</summary>
    public int GetTradeSellRate(int resourceLevel)
    {
        if (tradeSellUnitsPerGold == null || resourceLevel < 1 || resourceLevel - 1 >= tradeSellUnitsPerGold.Length)
            return foodToGoldIn;
        return Mathf.Max(1, tradeSellUnitsPerGold[resourceLevel - 1]);
    }

    /// <summary>买入兑换率：档位 → 1 金换多少单位（默认粮 3）。</summary>
    public int GetTradeBuyRate(int resourceLevel)
    {
        if (tradeBuyUnitsPerGold == null || resourceLevel < 1 || resourceLevel - 1 >= tradeBuyUnitsPerGold.Length)
            return goldToFoodOut;
        return Mathf.Max(1, tradeBuyUnitsPerGold[resourceLevel - 1]);
    }
}

/// <summary>商人贸易额度条目（资源档位 → 每日买入/卖出额度）。D220 每日全量重置后不再按 refreshDays 多天递减少，字段保留兼容读取。</summary>
[Serializable]
public struct TradeQuotaDef
{
    public int resourceLevel;   // 1粮/2木/3石/4矿/5金/6水晶/7火油/8特食/9肉/10Metal/11弹/12弹/13弹
    public int amountPerCycle;  // 每日额度（D220 每日重置后即"每日可再交易量"）
    public int refreshDays;     // 保留（历史字段，D220 语义已改每日全量重置）

    public static TradeQuotaDef Zero => new TradeQuotaDef { resourceLevel = 0, amountPerCycle = 0, refreshDays = 0 };
}