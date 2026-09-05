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

    // ===== QQQ.2 T15：水井特判（well.asset outputResource=Gold 占位，实际产水入网）=====
    private bool _isWell;

    // ===== 3.5 步骤6：副产 =====
    private bool _hasByproduct;
    private ResourceType _byproductType = ResourceType.Gold;   // 有效副产类型（Crystal/FireOil）
    private int _byproductAmount;
    private int _byproductCapacity = 20;
    private float _byproductAccumulator;                       // 已产矿累计（达到阈值 +1 副产）

    // ===== 3.5 P2：金矿直接产金入国库（§13.14 金=货币不占存储）=====
    private float _goldAccumulator;                            // 金矿产金累计（rate 为小数/秒，达 1 金才入账）

    // ===== QQQ.3 B8-7 / LC-B9：主产累计器（修复低速率建筑永远不产出）=====
    // 问题：`Mathf.RoundToInt(_rate)` 对 rate<0.5/s 恒为 0，主产无累计器 → 永远不产出。
    // 对齐金矿 _goldAccumulator 模式：累加 rate，达 1 才产出整数并扣减。
    private float _mainAccumulator;

    /// <summary>副产类型（无副产返回 Gold）。</summary>
    public ResourceType ByproductType => _byproductType;

    /// <summary>当前是否有工人 Working（QQQ.2 T9/DR-4：仅 Working 算在场，DR-19）。</summary>
    public bool HasWorkerAssigned => TaskScheduler.Instance != null && TaskScheduler.Instance.HasWorkerAssigned(_building);
    /// <summary>是否水井（QQQ.2 T15：水井自动产水入网，不派生产任务）。</summary>
    public bool IsWell => _isWell;
    /// <summary>主产资源类型（QQQ.2 T15：农场 outputResource=Food 才发挑水任务）。</summary>
    public ResourceType OutputResource => _resourceType;
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
        _resourceType = building.def.outputResource;
        _storage = building.GetComponent<StorageComponent>();
        _hasByproduct = false;
        _goldAccumulator = 0f;
        _mainAccumulator = 0f;

        // QQQ.2 T15：水井特判（well.asset outputResource=Gold 占位，实际产水入网；跳过金矿分支）
        _isWell = building.def.id == "Well";
        RefreshRate();
        UpdateByproductConfig();

        // D144（2_12 步骤10）：产金统一走 TaxSystem 商业税——市场/金矿不再隐性现产金（商业税为唯一产金来源）
        // 故此 outputResource=Gold 产金分支退役：金不再于此入账（taxOfficeGoldPerDay/goldMineGoldPerDay 已从 KingdomConfig 移除）
        if (!_isWell && _resourceType == ResourceType.Gold)
        {
            _rate = 0f;   // 产金退役（D144），市场商业税在 TaxSystem.OnNewDay 统一收取
        }
    }

    /// <summary>
    /// 刷新产能：rate × gradeScale × 等级缩放（3.5.4 数据卡：Lv2/Lv3 效率↑）。
    /// 建造/读档/升级后调用（QQQ.3 修复：升级后产能不再恒为 Lv1 值）。
    /// </summary>
    public void RefreshRate()
    {
        if (_building == null || _building.def == null) return;
        _rate = _building.def.producer.rate
                * _building.def.GetGradeScale(_building.grade)
                * _building.LevelScale();
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

        // QQQ.2 T15：水井产水入网（DR-14：rate=4 水/秒；水为隐藏资源不占存储，UI 不显示）
        if (_isWell)
        {
            // D535（HH.73 供水修复批）：原 2_17 批3a「AI 井恒不产水」拦截解除，改按归属路由——
            // AI 井(kingdomId>0) 产水入本国 AI 桶（WaterNetwork.AddWater 重载），玩家井(kingdomId=0)
            // 逐位走原玩家桶路径（HH.30 零回归）；AI 桶满停产对齐玩家 IsFull 语义。
            TickWaterToNetwork(_building.kingdomId);
            return;
        }

        // 金矿：金为货币不占存储，直接产金入国库（3.5 P1 独立产金机制，与税收并存，不受工人约束）
        if (_resourceType == ResourceType.Gold)
        {
            TickGoldToTreasury();
            return;
        }

        // QQQ.2 T9 / DR-4：无工人不产——生产建筑需有工人 Working 执行生产任务才产出（DR-19 仅 Working 算在场）。
        // 调度器在 NPC 中断/死亡/被招募走时自动清除指派（OnUnitDied/EscapeWorkers 已接），建筑不会无限判定"有人"。
        if (!HasWorkerAssigned) return;

        // QQQ.2 T15 / DR-9 + DR-18：农场产粮需耗水——1秒1次产出事件，每次产出耗 2 水（缺水停产 + 头顶冒"缺水"提示）
        if (_resourceType == ResourceType.Food && !TryConsumeFarmWater()) return;

        // 主产（QQQ.3 B8-7 / LC-B9：用累计器，低速率也产出）
        if (_storage != null && !_storage.IsFull)
        {
            // 2_20 M5/D420：种族生产乘数（Production 侧主产累加；资源→mul 映射 D506③，
            // 与 TaskScheduler Gather 入库侧同源 KingdomRace.GetGatherMul 防漂移；副产 Crystal/FireOil 不乘）
            float gatherMul = _building != null ? KingdomRace.GetGatherMul(_building.kingdomId, _resourceType) : 1f;
            _mainAccumulator += _rate * gatherMul;
            int produce = Mathf.FloorToInt(_mainAccumulator);
            if (produce > 0)
            {
                _mainAccumulator -= produce;
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

    /// <summary>
    /// 水井产水入网（QQQ.2 T15 / DR-14：rate=4 水/秒）。水为隐藏资源不占 Storage，
    /// 按归属入桶（D535：kingdomId=0 玩家桶逐位原逻辑；>0 入 AI 桶），桶满停产避免浪费（DR-8）。
    /// </summary>
    private void TickWaterToNetwork(int kingdomId)
    {
        if (WaterNetwork.Instance == null) return;
        if (WaterNetwork.Instance.IsBucketFull(kingdomId)) return;   // 桶满 → 停产（玩家/AI 同语义，D535）
        _mainAccumulator += _rate;
        int water = Mathf.FloorToInt(_mainAccumulator);
        if (water > 0)
        {
            _mainAccumulator -= water;
            WaterNetwork.Instance.AddWater(water, kingdomId);   // =0 走玩家桶（原单参重载逐位等价），>0 入 AI 桶
        }
    }

    /// <summary>
    /// 农场产粮耗水（QQQ.2 T15 / DR-9 + DR-18：每次产出耗 2 水）。
    /// ConsumeWater(2) 成功才允许本秒产出；失败则停产 + 头顶冒"缺水"提示。
    /// </summary>
    private bool TryConsumeFarmWater()
    {
        if (WaterNetwork.Instance == null) return false;
        // 2_17 步骤11 批3a（B′）：农田耗水按建筑归属路由——玩家(kingdomId=0) 耗玩家网水（原逻辑）；
        // AI(kingdomId>0) 耗 AI 桶水（恒 0 → AI 农田缺水停产，堵 "AI 农田吃玩家网水" 泄漏面）。
        if (WaterNetwork.Instance.ConsumeWater(2f, _building != null ? _building.kingdomId : 0)) return true;
        // 缺水停产 + 头顶冒"缺水"图标提示（OverheadSpeech 复用气泡机制）
        OverheadSpeech.Show(_building.transform, "缺水", duration: 1.2f);
        return false;
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