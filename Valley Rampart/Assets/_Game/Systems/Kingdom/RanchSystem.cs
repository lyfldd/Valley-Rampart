using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 牧场养殖系统（3.5 §13.10 / 实施计划 P1 步骤8；Singleton + ISaveable, Global）。
///
/// 全流程（屠宰制，一次性产出）：
///   买幼崽（商人：兔1/鸡1/猪2/牛4金）→ 投放牧场 → 每日喂粮（每动物1粮）→ 生长（兔2/鸡3/猪5/牛8天）→ 宰相杀一次性得肉。
/// 规则：屠宰制无自动繁殖（靠再买幼崽）；断粮停长（成年期顺延，不死亡）；牧场容量 10（KingdomConfig）。
///
/// 数据层实现：买/喂/宰为纯数据操作；牧民 NPC 喂养/管理/宰杀表演走 IWorkerTaskExecutor 后置。
/// 存档：动物列表（类型/生长天数）随 RanchSaveData 持久化。
/// </summary>
public class RanchSystem : Singleton<RanchSystem>, ISaveable
{
    public string SaveId => "RanchSystem";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    private RanchConfig _config;

    // ===== 2_17 步骤11 批2·牧场 per-kingdom 分桶（Singleton 门面 + 内部 Dictionary，玩家桶0=原全局语义逐位一致 HH.30）=====
    // 玩家(id=0) 桶 = 原 List _animals；AI(id>0) 各王国独立桶（结构+暂不生产：AI 牧场真实饲养归步骤13/14）。
    // 玩家无参访问（Animals/AnimalCount/Capacity/BuyCub/OnNewDay/Slaughter）读桶0，调用点零改动；AI 走 kingdomId 重载 + 扣 AI KingdomState.resources。
    // ⚠️ 存档仅玩家桶0 入档（RanchSaveData struct 不动，兼容 2_11 KingdomSaveData kingdoms[] 迁移职责）；AI 桶不入档（归后续迁移）。
    private readonly Dictionary<int, List<AnimalEntry>> _animals = new Dictionary<int, List<AnimalEntry>>();

    private List<AnimalEntry> GetList(int kingdomId)
    {
        if (!_animals.TryGetValue(kingdomId, out var list))
        {
            list = new List<AnimalEntry>();
            _animals[kingdomId] = list;
        }
        return list;
    }

    /// <summary>牧场当前动物列表（玩家桶0，读/遍历用，勿直接改，用 BuyCub/Slaughter）。</summary>
    public IReadOnlyList<AnimalEntry> Animals => GetList(0);

    /// <summary>某王国牧场动物列表（玩家0=玩家桶；AI 按 kingdomId）。</summary>
    public IReadOnlyList<AnimalEntry> AnimalsByKingdom(int kingdomId) => GetList(kingdomId);

    /// <summary>当前动物数（玩家桶0）。</summary>
    public int AnimalCount => GetList(0).Count;

    /// <summary>某王国动物数（玩家0=玩家桶；AI 按 kingdomId）。</summary>
    public int AnimalCountByKingdom(int kingdomId) => GetList(kingdomId).Count;

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        _config = Resources.Load<RanchConfig>("Config/RanchConfig");
        SaveManager.Instance.RegisterSaveable(this);
    }

    private RanchConfig Cfg()
    {
        if (_config == null) _config = Resources.Load<RanchConfig>("Config/RanchConfig");
        return _config;
    }

    /// <summary>牧场容量（KingdomConfig.ranchCapacity，默认 10）。</summary>
    public int Capacity()
    {
        var cfg = KingdomManager.Instance != null ? KingdomManager.Instance.Config : null;
        return cfg != null ? cfg.ranchCapacity : 10;
    }

    /// <summary>按动物类型取定义（无则 null）。</summary>
    public AnimalDef? GetAnimalDef(AnimalType type)
    {
        var cfg = Cfg();
        if (cfg == null || cfg.animals == null) return null;
        for (int i = 0; i < cfg.animals.Length; i++)
            if (cfg.animals[i].type == type) return cfg.animals[i];
        return null;
    }

    /// <summary>
    /// 购买幼崽并投放牧场（§13.10 商人购买）。校验仓库容量 + 金 → 扣费 → 加入动物。
    /// </summary>
    public bool BuyCub(AnimalType type) => BuyCub(0, type);   // 玩家桶0（原语义，HH.30）

    /// <summary>购买幼崽并投放某王国牧场（玩家=扣 Ruler 金；AI=扣 AI KingdomState.resources）。AI 真实圈养归步骤13/14，无调用点暂不触发。</summary>
    public bool BuyCub(int kingdomId, AnimalType type)
    {
        var def = GetAnimalDef(type);
        if (def == null) { Debug.Log($"[RanchSystem] 动物 {type} 未配置"); return false; }
        var d = def.Value;
        var list = GetList(kingdomId);
        if (list.Count >= Capacity()) { Debug.Log($"[RanchSystem] 牧场已满（{Capacity()}）"); return false; }

        if (kingdomId == 0)
        {
            if (RulerController.Instance == null) return false;
            if (RulerController.Instance.Gold < d.youngCost) { Debug.Log($"[RanchSystem] 金不足，无法购买 {type} 幼崽"); return false; }
            RulerController.Instance.ModifyResource(ResourceType.Gold, false, d.youngCost);
        }
        else
        {
            var k = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(kingdomId) : null;
            if (k == null || k.resources.gold < d.youngCost) { Debug.Log($"[RanchSystem] 王国[{kingdomId}] 金不足，无法购买 {type} 幼崽"); return false; }
            k.resources.gold -= d.youngCost;   // AI 台账扣金（五经济资源真源；肉/特食归 AbstractEconomySettler）
        }

        list.Add(new AnimalEntry { type = type, daysGrown = 0, isAdult = d.growDays <= 0 });
        Debug.Log($"[RanchSystem] 购买 {type} 幼崽（{d.youngCost}金，kingdomId={kingdomId}），牧场 {list.Count}/{Capacity()}");
        return true;
    }

    /// <summary>
    /// 每日结算（DayCycleSettlement 统一入口调用）：为每头动物喂粮（每动物1粮）→ 生长→成年。
    /// 断粮（国库粮不足）→ 该动物停止生长（成年期顺延，不死亡）。
    /// </summary>
    public void OnNewDay()
    {
        var kingdomCfg = KingdomManager.Instance != null ? KingdomManager.Instance.Config : null;
        int feedPerAnimal = kingdomCfg != null ? kingdomCfg.ranchFeedPerAnimal : 1;
        if (RulerController.Instance == null) return;

        // 2_17 步骤11 批2：分王国结算——玩家桶0 原语义（扣 Ruler 国库粮）；AI 桶按 kingdomId 扣 KingdomState.resources（暂无调用点，结构性）
        SettleBucket(0, GetList(0), feedPerAnimal);

        if (KingdomRegistry.Instance != null)
        {
            var all = KingdomRegistry.Instance.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                var k = all[i];
                if (k == null || k.IsPlayer) continue;
                SettleBucket(k.id, GetList(k.id), feedPerAnimal);
            }
        }

        Debug.Log($"[RanchSystem] 每日喂粮结算完成（玩家 {GetList(0).Count} 头）");
    }

    /// <summary>单王国牧场喂粮结算（玩家=扣 Ruler 粮；AI=扣 KingdomState.resources.food）。断粮停长不死亡。</summary>
    private void SettleBucket(int kingdomId, List<AnimalEntry> list, int feedPerAnimal)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            if (entry.isAdult) continue;   // 已成年不再生长

            var def = GetAnimalDef(entry.type);
            if (def == null) continue;
            int growDays = Mathf.Max(1, def.Value.growDays);

            // 喂粮：国库粮足则喂并生长；断粮停长
            if (FeedFromTreasury(kingdomId, feedPerAnimal))
            {
                entry.daysGrown++;
                if (entry.daysGrown >= growDays)
                {
                    entry.isAdult = true;
                    Debug.Log($"[RanchSystem] kingdomId={kingdomId} {entry.type} 已成年（{entry.daysGrown}/{growDays}天）");
                }
            }
            else
            {
                Debug.Log($"[RanchSystem] kingdomId={kingdomId} {entry.type} 断粮，今日停长（{entry.daysGrown}/{growDays}天）");
            }
            list[i] = entry;
        }
    }

    /// <summary>从王国国库扣粮（玩家走 RulerController 玩家口径；AI 走 KingdomState.resources）。返回是否喂粮成功。</summary>
    private bool FeedFromTreasury(int kingdomId, int amount)
    {
        if (kingdomId == 0)
        {
            if (RulerController.Instance == null) return false;
            if (RulerController.Instance.GetResource(ResourceType.Food) < amount) return false;
            RulerController.Instance.ModifyResource(ResourceType.Food, false, amount);
            return true;
        }
        var k = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(kingdomId) : null;
        if (k == null || k.resources.food < amount) return false;
        k.resources.food -= amount;
        return true;
    }

    /// <summary>
    /// 宰杀动物（屠宰制，一次性得肉；§13.10）。仅成年可宰。
    /// 产出 Meat 入国库（肉→饱食+20/幸福+3，由进食侧消费）。
    /// </summary>
    public bool Slaughter(AnimalEntry entry) => Slaughter(0, entry);   // 玩家桶0（原语义，HH.30）

    /// <summary>宰杀动物（屠宰制，一次性得肉）。玩家=肉进 Ruler 国库；AI=肉折入 KingdomState.resources.food（台账仅五经济资源，真实肉/特食归步骤13/14）。AI 无调用点暂不触发。</summary>
    public bool Slaughter(int kingdomId, AnimalEntry entry)
    {
        if (!entry.isAdult) { Debug.Log($"[RanchSystem] {entry.type} 未成年，不可宰杀"); return false; }
        var list = GetList(kingdomId);
        if (!list.Remove(entry)) return false;

        var def = GetAnimalDef(entry.type);
        int meat = def != null ? def.Value.meatYield : 1;
        if (kingdomId == 0)
        {
            if (RulerController.Instance == null) return false;
            RulerController.Instance.ModifyResource(ResourceType.Meat, true, meat);
        }
        else
        {
            var k = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(kingdomId) : null;
            if (k == null) return false;
            k.resources.food += meat;   // 占位：AI 肉产出真实落账归 AbstractEconomySettler
        }
        Debug.Log($"[RanchSystem] 宰杀 {entry.type} → 得肉 {meat}（kingdomId={kingdomId}），牧场剩 {list.Count}");
        return true;
    }

    /// <summary>按索引宰杀（玩家桶0，供 UI 列表操作）。</summary>
    public bool SlaughterAt(int index)
    {
        var list = GetList(0);
        if (index < 0 || index >= list.Count) return false;
        return Slaughter(0, list[index]);
    }

    // ===== ISaveable, Global =====

    public SavePayload SaveState()
    {
        // 2_17 步骤11 批2：仅玩家桶0 入档（RanchSaveData struct 不动，兼容 2_11 kingdoms[] 迁移职责；AI 桶不入档）
        var list = GetList(0);
        var data = new RanchSaveData { saveDataVersion = 1 };
        data.animals = new List<AnimalEntrySaveData>(list.Count);
        foreach (var a in list)
            data.animals.Add(new AnimalEntrySaveData { type = (int)a.type, daysGrown = a.daysGrown, isAdult = a.isAdult });
        return new SavePayload
        {
            typeName = typeof(RanchSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(RanchSaveData).AssemblyQualifiedName) return;
        var data = JsonUtility.FromJson<RanchSaveData>(payload.json);
        var list = GetList(0);
        list.Clear();
        if (data.animals != null)
        {
            foreach (var a in data.animals)
                list.Add(new AnimalEntry { type = (AnimalType)a.type, daysGrown = a.daysGrown, isAdult = a.isAdult });
        }
        Debug.Log($"[RanchSystem] 读档恢复 {list.Count} 头动物（玩家桶0）");
    }

    public void ResetState()
    {
        _animals.Clear();
    }
}

/// <summary>牧场单头动物运行时状态。</summary>
public struct AnimalEntry
{
    public AnimalType type;
    public int daysGrown;
    public bool isAdult;
}

/// <summary>牧场动物存档条目（§2.3 延伸）。</summary>
[System.Serializable]
public struct AnimalEntrySaveData
{
    public int type;
    public int daysGrown;
    public bool isAdult;
}

/// <summary>牧场存档数据（RanchSystem，Global）。</summary>
[System.Serializable]
public class RanchSaveData
{
    public int saveDataVersion = 1;
    public List<AnimalEntrySaveData> animals;
}