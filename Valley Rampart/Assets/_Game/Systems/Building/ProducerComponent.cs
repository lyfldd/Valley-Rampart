using UnityEngine;

/// <summary>
/// 产能组件（3.3.4 批次5 + 3.5 实施计划 P0 步骤6）。
/// 每秒按 rate × gradeScale 产出资源写入本地 StorageComponent。
/// 3.5 步骤6：矿洞副产扩展——矿洞 Lv2 副产水晶、Lv3 副产火油（每 byproductOrePerUnit 矿 +1）。
/// 副产独立存本地计数器（ByproductAmount），主存储仍存主产资源（矿）。
/// 由 ProductionSystem 集中调度（每秒遍历），不自己 Update。
/// </summary>
public class ProducerComponent : MonoBehaviour, IBuildingComponent
{
    private Building _building;
    private StorageComponent _storage;
    private float _rate;
    private ResourceType _resourceType;

    // ===== 3.5 步骤6：副产 =====
    private bool _hasByproduct;
    private ResourceType _byproductType = ResourceType.Gold;   // 有效副产类型（Crystal/FireOil）
    private int _byproductAmount;
    private int _byproductCapacity = 20;
    private float _byproductAccumulator;                       // 已产矿累计（达到阈值 +1 副产）

    // ===== 3.5 P2：金矿直接产金入国库（§13.14 金=货币不占存储）=====
    private float _goldAccumulator;                            // 金矿产金累计（rate 为小数/秒，达 1 金才入账）

    /// <summary>副产类型（无副产返回 Gold）。</summary>
    public ResourceType ByproductType => _byproductType;
    /// <summary>副产已存数量。</summary>
    public int ByproductAmount => _byproductAmount;
    /// <summary>副产容量。</summary>
    public int ByproductCapacity => _byproductCapacity;
    /// <summary>是否已满。</summary>
    public bool ByproductFull => _hasByproduct && _byproductAmount >= _byproductCapacity;

    public void Init(Building building)
    {
        _building = building;
        if (building == null || building.def == null) return;
        _rate = building.def.producer.rate * building.def.GetGradeScale(building.grade);
        _resourceType = building.def.outputResource;
        _storage = building.GetComponent<StorageComponent>();
        _hasByproduct = false;
        _goldAccumulator = 0f;
        UpdateByproductConfig();

        // 金矿：金=货币不占存储，直接产金入国库（rate 由 KingdomConfig 每日产金换算，SO 可调）
        // 3.5 P1-21：税务所（原金矿）独立产金，与 TaxSystem 并存——优先 taxOfficeGoldPerDay，否则 goldMineGoldPerDay
        if (_resourceType == ResourceType.Gold)
        {
            var cfg = KingdomManager.Instance != null ? KingdomManager.Instance.Config : null;
            int perDay = 0;
            if (cfg != null && cfg.taxOfficeGoldPerDay > 0)
                perDay = cfg.taxOfficeGoldPerDay;                       // 税务所独立产金
            else if (cfg != null && cfg.goldMineGoldPerDay > 0)
                perDay = cfg.goldMineGoldPerDay;                        // 金矿产金（兜底）
            else
                perDay = 2;
            int secondsPerDay = cfg != null && cfg.kingdomSecondsPerDay > 0 ? cfg.kingdomSecondsPerDay : 180;
            _rate = perDay / (float)secondsPerDay;
        }
    }

    /// <summary>按当前建筑等级刷新副产配置（升级后自动切换水晶→火油）。</summary>
    private void UpdateByproductConfig()
    {
        if (_building == null || _building.def == null) { _hasByproduct = false; return; }
        // 仅矿洞（主产矿）有副产
        if (_building.def.outputResource != ResourceType.Ore) { _hasByproduct = false; return; }

        var config = KingdomManager.Instance != null ? KingdomManager.Instance.Config : null;
        int cap = config != null ? config.byproductCrystalCapacity : 20;
        if (_building.level >= 3)
        {
            _hasByproduct = true;
            _byproductType = ResourceType.FireOil;
            if (config != null) cap = config.byproductFireOilCapacity;
        }
        else if (_building.level >= 2)
        {
            _hasByproduct = true;
            _byproductType = ResourceType.Crystal;
        }
        else
        {
            _hasByproduct = false;
        }
        _byproductCapacity = Mathf.Max(1, cap);
    }

    /// <summary>每秒 tick（由 ProductionSystem 调用）。</summary>
    public void Tick()
    {
        if (_building == null || !_building.IsActive) return;
        UpdateByproductConfig();   // 升级后按新等级刷新副产类型

        // 金矿：金为货币不占存储，直接产金入国库（§13.14 金=货币无限，Override 本地存储）
        if (_resourceType == ResourceType.Gold)
        {
            TickGoldToTreasury();
            return;
        }

        // 主产
        if (_storage != null && !_storage.IsFull)
        {
            int produce = Mathf.RoundToInt(_rate);
            if (produce > 0)
            {
                _storage.storedAmount = Mathf.Min(_storage.capacity, _storage.storedAmount + produce);
                // 副产：按产矿数量累计
                if (_hasByproduct && !ByproductFull)
                {
                    int threshold = KingdomManager.Instance != null && KingdomManager.Instance.Config != null
                        ? KingdomManager.Instance.Config.byproductOrePerUnit : 5;
                    _byproductAccumulator += produce;
                    while (_byproductAccumulator >= threshold && _byproductAmount < _byproductCapacity)
                    {
                        _byproductAccumulator -= threshold;
                        _byproductAmount++;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 金矿产金直接入国库（3.5 P2，§13.14 金=货币不占存储）。rate 为小数/秒，
    /// 用累计器攒够整数金才入账，避免每秒事件刷屏。
    /// </summary>
    private void TickGoldToTreasury()
    {
        if (RulerController.Instance == null) return;
        _goldAccumulator += _rate;
        int gold = Mathf.FloorToInt(_goldAccumulator);
        if (gold > 0)
        {
            _goldAccumulator -= gold;
            RulerController.Instance.ModifyResource(ResourceType.Gold, true, gold);
        }
    }

    /// <summary>读档恢复副产（BuildingFactory.SpawnFromSave / Building.LoadState 调）。</summary>
    public void RestoreByproduct(int type, int amount)
    {
        _byproductType = (ResourceType)type;
        _byproductAmount = Mathf.Max(0, amount);
        _hasByproduct = (ResourceType)type == ResourceType.Crystal || (ResourceType)type == ResourceType.FireOil;
        UpdateByproductConfig();
    }
}