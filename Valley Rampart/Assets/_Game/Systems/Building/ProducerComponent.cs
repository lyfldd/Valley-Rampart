using UnityEngine;

/// <summary>
/// 产能组件（3.3.4 批次5 + 3.5 实施计划 P0 步骤6）。
/// 每秒按 rate × gradeScale 产出资源写入本地 StorageComponent。
/// 由 ProductionSystem 集中调度（每秒遍历），不自己 Update。
/// </summary>
public class ProducerComponent : MonoBehaviour, ITickable
{
    private Building _building;
    private StorageComponent _storage;
    private float _rate;
    private ResourceType _resourceType;

    // ===== QQQ.2 T15：水井特判（well.asset outputResource=Gold 占位，⭐ `M1-F` 起实际产水**入本仓**）=====
    private bool _isWell;

    // ===== QQQ.3 B8-7 / LC-B9：主产累计器（修复低速率建筑永远不产出）=====
    // 问题：`Mathf.RoundToInt(_rate)` 对 rate<0.5/s 恒为 0，主产无累计器 → 永远不产出。
    // 解法：累加 rate，达 1 才产出整数并扣减（小数余量保留待下轮）。
    private float _mainAccumulator;

    /// <summary>当前是否有工人 Working（QQQ.2 T9/DR-4：仅 Working 算在场，DR-19）。</summary>
    public bool HasWorkerAssigned => TaskScheduler.Instance != null && TaskScheduler.Instance.HasWorkerAssigned(_building);
    /// <summary>是否水井（⭐ `M1-F` 起：**免工自产 · 产水入本仓**；⛔ 不派生产任务 ⇒ `Building.TryAdvertiseTask` 用它排除）。</summary>
    public bool IsWell => _isWell;
    /// <summary>主产资源类型（QQQ.2 T15：农场 outputResource=Food 才发挑水任务）。</summary>
    public ResourceType OutputResource => _resourceType;

    public void Init(Building building)
    {
        _building = building;
        if (building == null || building.def == null) return;
        _resourceType = building.def.outputResource;
        _storage = building.GetComponent<StorageComponent>();
        _mainAccumulator = 0f;

        // QQQ.2 T15：水井特判（well.asset outputResource=Gold 占位 ⇒ ⭐ `M1-F` 起实际产水**入本仓**；跳过产金分支）
        _isWell = building.def.id == "Well";
        RefreshRate();

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

    /// <summary>每秒 tick（由 ProductionSystem 调用）。</summary>
    public void Tick()
    {
        if (_building == null || !_building.IsActive) return;

        // ⭐ `M1-F` 件2（`09#44` · `09` §4.3 D 组）：水井产水入**本仓**（普通仓 · ⛔ 不再入 `WaterNetwork`）。
        //   ⚠️ 保留"免工自产"（`D807` 裁）：早返回 ⛔ 不落下方 `HasWorkerAssigned` 守卫 ⇒ 水井不需工人。
        //   ⭐ AI/玩家分账天然成立（每国自己的井 → 自己的仓 ⇒ 原 `_aiStoredByKingdom` 桶语义消解）。
        if (_isWell)
        {
            TickWaterToStorage();
            return;
        }

        // QQQ.2 T9 / DR-4：无工人不产——生产建筑需有工人 Working 执行生产任务才产出（DR-19 仅 Working 算在场）。
        // 调度器在 NPC 中断/死亡/被招募走时自动清除指派（OnUnitDied/EscapeWorkers 已接），建筑不会无限判定"有人"。
        if (!HasWorkerAssigned) return;

        // QQQ.2 T15 / DR-9 + DR-18：农场产粮需耗水——1秒1次产出事件，每次产出耗 2 水（缺水停产 + 头顶冒"缺水"提示）
        if (_resourceType == ResourceType.Food && !TryConsumeFarmWater()) return;

        // 主产（QQQ.3 B8-7 / LC-B9：用累计器，低速率也产出）
        // ⭐ M1-A：本地仓已改多资源容器 ⇒ 按「本建筑产出资源」判满/入仓（体积口径 · 09 §5.2）
        if (_storage != null && !_storage.IsFullFor(_resourceType))
        {
            // 2_20 M5/D420：种族生产乘数（Production 侧主产累加；资源→mul 映射 D506③，
            // 与 TaskScheduler Gather 入库侧同源 KingdomRace.GetGatherMul 防漂移）
            float gatherMul = _building != null ? KingdomRace.GetGatherMul(_building.kingdomId, _resourceType) : 1f;
            _mainAccumulator += _rate * gatherMul;
            int produce = Mathf.FloorToInt(_mainAccumulator);
            if (produce > 0)
            {
                _mainAccumulator -= produce;
                _storage.Add(_resourceType, produce);   // 放到满为止（部分成功 · 09 §7.1）
            }
        }
    }

    /// <summary>
    /// 水井产水入本仓（`M1-F` 件2 · `09#44`：水按资源处理 · 普通仓）。
    /// 仓满（`IsFullFor(Water)`）⇒ 停产避免浪费（DR-8）—— ⭐ `D807` Q2：水体积 1 ⇒ 井仓满 100 停产，
    /// 与退役前 `WaterNetwork.capacity=100` 同语义（`Well.asset producer.capacity=0` ⇒ 仓容落 100）。
    /// </summary>
    private void TickWaterToStorage()
    {
        if (_storage == null) return;
        if (_storage.IsFullFor(ResourceType.Water)) return;   // 仓满 ⇒ 停产（DR-8）
        _mainAccumulator += _rate;                            // 复用同一累计器（整数点产出 · DR-14）
        int water = Mathf.FloorToInt(_mainAccumulator);
        if (water > 0)
        {
            _mainAccumulator -= water;
            _storage.Add(ResourceType.Water, water);          // 放到满为止（部分成功 · 09 §7.1）
        }
    }

    /// <summary>
    /// 农场产粮耗水（`M1-F` 件3 · `09` §4.3 D 组：**从农场自己的仓扣**）。
    /// 每次产出耗 **2 点**（整数化 · `D807` Q4）；不足 ⇒ 停产 ＋ 头顶冒"缺水"（形制不变）。
    /// </summary>
    private bool TryConsumeFarmWater()
    {
        if (_storage == null || !_storage.CanTake(ResourceType.Water, 2))
        {
            // 缺水停产 + 头顶冒"缺水"图标提示（OverheadSpeech 复用气泡机制）
            if (_building != null) OverheadSpeech.Show(_building.transform, "缺水", duration: 1.2f);
            return false;
        }
        _storage.TakeOut(ResourceType.Water, 2);
        return true;
    }
}
