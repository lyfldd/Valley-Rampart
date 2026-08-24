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
    private readonly List<AnimalEntry> _animals = new List<AnimalEntry>();

    /// <summary>牧场当前动物列表（读/遍历用，勿直接改，用 BuyCub/Slaughter）。</summary>
    public IReadOnlyList<AnimalEntry> Animals => _animals;

    /// <summary>当前动物数。</summary>
    public int AnimalCount => _animals.Count;

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
    public bool BuyCub(AnimalType type)
    {
        var def = GetAnimalDef(type);
        if (def == null) { Debug.Log($"[RanchSystem] 动物 {type} 未配置"); return false; }
        var d = def.Value;
        if (RulerController.Instance == null) return false;
        if (_animals.Count >= Capacity()) { Debug.Log($"[RanchSystem] 牧场已满（{Capacity()}）"); return false; }
        if (RulerController.Instance.Gold < d.youngCost) { Debug.Log($"[RanchSystem] 金不足，无法购买 {type} 幼崽"); return false; }

        RulerController.Instance.ModifyResource(ResourceType.Gold, false, d.youngCost);
        _animals.Add(new AnimalEntry { type = type, daysGrown = 0, isAdult = d.growDays <= 0 });
        Debug.Log($"[RanchSystem] 购买 {type} 幼崽（{d.youngCost}金），牧场 {_animals.Count}/{Capacity()}");
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

        for (int i = 0; i < _animals.Count; i++)
        {
            var entry = _animals[i];
            if (entry.isAdult) continue;   // 已成年不再生长

            var def = GetAnimalDef(entry.type);
            if (def == null) continue;
            int growDays = Mathf.Max(1, def.Value.growDays);

            // 喂粮：国库粮足则喂并生长；断粮停长
            if (RulerController.Instance.GetResource(ResourceType.Food) >= feedPerAnimal)
            {
                RulerController.Instance.ModifyResource(ResourceType.Food, false, feedPerAnimal);
                entry.daysGrown++;
                if (entry.daysGrown >= growDays)
                {
                    entry.isAdult = true;
                    Debug.Log($"[RanchSystem] {entry.type} 已成年（{entry.daysGrown}/{growDays}天）");
                }
            }
            else
            {
                Debug.Log($"[RanchSystem] {entry.type} 断粮，今日停长（{entry.daysGrown}/{growDays}天）");
            }
            _animals[i] = entry;
        }

        Debug.Log($"[RanchSystem] 每日喂粮结算完成（{_animals.Count} 头）");
    }

    /// <summary>
    /// 宰杀动物（屠宰制，一次性得肉；§13.10）。仅成年可宰。
    /// 产出 Meat 入国库（肉→饱食+20/幸福+3，由进食侧消费）。
    /// </summary>
    public bool Slaughter(AnimalEntry entry)
    {
        if (!entry.isAdult) { Debug.Log($"[RanchSystem] {entry.type} 未成年，不可宰杀"); return false; }
        if (!_animals.Remove(entry)) return false;
        if (RulerController.Instance == null) return false;

        var def = GetAnimalDef(entry.type);
        int meat = def != null ? def.Value.meatYield : 1;
        RulerController.Instance.ModifyResource(ResourceType.Meat, true, meat);
        Debug.Log($"[RanchSystem] 宰杀 {entry.type} → 得肉 {meat}，牧场剩 {_animals.Count}");
        return true;
    }

    /// <summary>按索引宰杀（供 UI 列表操作）。</summary>
    public bool SlaughterAt(int index)
    {
        if (index < 0 || index >= _animals.Count) return false;
        return Slaughter(_animals[index]);
    }

    // ===== ISaveable, Global =====

    public SavePayload SaveState()
    {
        var data = new RanchSaveData { saveDataVersion = 1 };
        data.animals = new List<AnimalEntrySaveData>(_animals.Count);
        foreach (var a in _animals)
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
        _animals.Clear();
        if (data.animals != null)
        {
            foreach (var a in data.animals)
                _animals.Add(new AnimalEntry { type = (AnimalType)a.type, daysGrown = a.daysGrown, isAdult = a.isAdult });
        }
        Debug.Log($"[RanchSystem] 读档恢复 {_animals.Count} 头动物");
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