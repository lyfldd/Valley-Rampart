using UnityEngine;

/// <summary>
/// 贸易系统（3.5 §七 / 实施计划 P1 步骤3（贸易）；Singleton）。
///
/// 规则（§七）：
///   - 商人档位随市场等级（KingdomConfig.marketMaxTradeLevel：Lv1 粮↔金 / Lv2 水晶火油 / Lv3 全开）。
///   - 资源等级：1粮/2木/3石/4矿/5金/6水晶/7火油/8特殊食物/9肉。
///   - 不对称兑换损失：卖出按 tradeSellUnitsPerGold（粮4单位→1金），买入按 tradeBuyUnitsPerGold（1金→粮3单位）。
///   - 梯度额度 + 长周期防刷：KingdomManager.TradeQuotaRemaining / TryConsumeTradeQuota（已就绪）。
///
/// 数据层实现：卖出/买入针对国库资源（粮/木/石/特殊食物/肉/金）。矿/水晶/火油存建筑存储，
/// 需先 Harvest 到国库（搬运路由后置，见输出说明）。
/// </summary>
public class TradeSystem : Singleton<TradeSystem>
{
    private TradeConfig _config;

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        _config = Resources.Load<TradeConfig>("Config/TradeConfig");
    }

    private TradeConfig Cfg()
    {
        if (_config == null) _config = Resources.Load<TradeConfig>("Config/TradeConfig");
        return _config;
    }

    /// <summary>资源类型 → 贸易等级（§七 / §13.11）。不可交易返回 0。</summary>
    public static int GetResourceLevel(ResourceType type)
    {
        return type switch
        {
            ResourceType.Food => 1,
            ResourceType.Wood => 2,
            ResourceType.Stone => 3,
            ResourceType.Ore => 4,
            ResourceType.Gold => 5,
            ResourceType.Crystal => 6,
            ResourceType.FireOil => 7,
            ResourceType.SpecialFood => 8,
            ResourceType.Meat => 9,
            // ===== 2_12 步骤10：档位扩到 13（HH.19 预圈；Metal + 3 弹药）=====
            ResourceType.Metal => 10,       // 金属（铁匠铺/工事/兵种强化，国库持有 D199）
            ResourceType.StoneAmmo => 11,   // 石弹（弹药买紧缺 D219）
            ResourceType.FireballAmmo => 12,
            ResourceType.MagicAmmo => 13,
            _ => 0
        };
    }

    /// <summary>市场等级是否解锁该资源档位（§3.5 商业）。</summary>
    public bool IsTierUnlocked(ResourceType type, int marketLevel)
    {
        int level = GetResourceLevel(type);
        if (level <= 0) return false;
        var cfg = Cfg();
        int max = cfg != null && cfg.marketMaxTradeLevel != null && marketLevel - 1 < cfg.marketMaxTradeLevel.Length
            ? cfg.marketMaxTradeLevel[marketLevel - 1]
            : 4;
        return level <= Mathf.Max(1, max);
    }

    /// <summary>该资源是否由国库直接持有（可卖出/买入；矿/水晶/火油存建筑存储）。</summary>
    public static bool IsTreasuryResource(ResourceType type)
    {
        return type == ResourceType.Food || type == ResourceType.Wood
            || type == ResourceType.Stone || type == ResourceType.SpecialFood
            || type == ResourceType.Meat
            || type == ResourceType.Metal;   // D199/D219 Metal：国库持有，可买可卖（买紧缺优先）
    }

    /// <summary>
    /// 卖出资源换金（§七 不对称兑换损失）。返回所得金币。
    /// 校验：国库资源 + 档位解锁 + 额度充足 + 资源存量充足。
    /// </summary>
    public int SellToGold(ResourceType type, int amount, int marketLevel)
    {
        var cfg = Cfg();
        if (cfg == null || RulerController.Instance == null) return 0;
        if (amount <= 0) return 0;
        if (!IsTreasuryResource(type)) { Debug.Log("[TradeSystem] 矿/水晶/火油存建筑存储，需先 Harvest（搬运路由后置）"); return 0; }
        if (!IsTierUnlocked(type, marketLevel)) { Debug.Log($"[TradeSystem] 市场 Lv{marketLevel} 未解锁该档位"); return 0; }

        int level = GetResourceLevel(type);
        if (KingdomManager.Instance == null || !KingdomManager.Instance.TryConsumeTradeQuota(level, amount))
        {
            Debug.Log($"[TradeSystem] 贸易额度不足（资源等级{level}）");
            return 0;
        }
        if (RulerController.Instance.GetResource(type) < amount) return 0;

        int sellRate = cfg.GetTradeSellRate(level);
        int goldGained = amount / sellRate;   // 不对称损失：4粮→1金
        if (goldGained <= 0) return 0;

        RulerController.Instance.ModifyResource(type, false, amount);
        RulerController.Instance.ModifyResource(ResourceType.Gold, true, goldGained);
        Debug.Log($"[TradeSystem] 卖出 {amount} {type} → {goldGained} 金（市场Lv{marketLevel}，等级{level}）");
        return goldGained;
    }

    /// <summary>
    /// 花金买资源（§七 不对称兑换损失）。返回买入数量。
    /// 校验：国库资源 + 档位解锁 + 额度充足 + 国库金充足。
    /// </summary>
    public int BuyWithGold(ResourceType type, int amount, int marketLevel)
    {
        var cfg = Cfg();
        if (cfg == null || RulerController.Instance == null) return 0;
        if (amount <= 0) return 0;
        if (type == ResourceType.Gold) return 0;
        if (!IsTreasuryResource(type)) { Debug.Log("[TradeSystem] 矿/水晶/火油存建筑存储，买入路由后置"); return 0; }
        if (!IsTierUnlocked(type, marketLevel)) { Debug.Log($"[TradeSystem] 市场 Lv{marketLevel} 未解锁该档位"); return 0; }

        int level = GetResourceLevel(type);
        if (KingdomManager.Instance == null || !KingdomManager.Instance.TryConsumeTradeQuota(level, amount))
        {
            Debug.Log($"[TradeSystem] 贸易额度不足（资源等级{level}）");
            return 0;
        }

        int buyRate = cfg.GetTradeBuyRate(level);
        int goldCost = (amount + buyRate - 1) / buyRate;   // 向上取整保证刻度
        if (RulerController.Instance.Gold < goldCost) return 0;

        RulerController.Instance.ModifyResource(ResourceType.Gold, false, goldCost);
        RulerController.Instance.ModifyResource(type, true, amount);
        Debug.Log($"[TradeSystem] 买入 {amount} {type}，耗金 {goldCost}（市场Lv{marketLevel}，等级{level}）");
        return amount;
    }
}