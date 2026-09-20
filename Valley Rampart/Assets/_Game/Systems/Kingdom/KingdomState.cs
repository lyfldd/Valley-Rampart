using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 王国运行时态（2_16 步骤1，D303/D311/D314/D385）。
/// 纯数据容器（非 MonoBehaviour），由 KingdomRegistry 持有。
/// 归属地基字段：id / name / bannerColor / foundedDay / personality 五轴 / 模板来源。
///
/// 五轴 personality（D311）：0=好战 1=经济 2=防守 3=扩张 4=外交，0~1 相互独立不归一化。
/// KingdomDef 句柄（步骤4 建 KingdomDef 后补）；王座锚点句柄（步骤5 挂 ThroneAnchor 后补）。
/// resources 过渡账本由 KingdomFoundry 在步骤5 写入——baseStockpile 占位，2_17 步骤2
/// WarehouseRegistry per-kingdom 化落地时迁移吸收（AI 在 2_17 前无脑不消费资源，零风险）。
/// </summary>
public class KingdomState
{
    /// <summary>王国唯一 id（0=玩家；单调递增不复用，D385）。</summary>
    public int id;

    /// <summary>国名（玩家接 KingdomManager.KingdomName，2_13 已落）。</summary>
    public string name;

    /// <summary>王旗色（染色数据，2_16 只出数据不渲染，渲染归 2_10）。</summary>
    public Color bannerColor;

    /// <summary>立国日（第一代=地图生成日；动态=插旗日）。</summary>
    public int foundedDay;

    /// <summary>性格五轴（0=好战 1=经济 2=防守 3=扩张 4=外交），0~1 独立不归一化（D311）。</summary>
    public float[] personality = new float[5];

    /// <summary>模板来源：第一代=KingdomDef id；-1=无来源（玩家 / 全无来源流民占位基线）；动态=来源国混合。</summary>
    public int templateSourceId = -1;

    /// <summary>
    /// 国族（2_20 M2/D467 国家种族=其人口种族，构造性不变）：AI 第一代=KingdomDef.raceId 模板映射；
    /// 动态立国=D471 插旗定族（营地成员多数派）；玩家=选族暂存 NewGameConfig.raceId（M5 绑定，本批默认 Human）。
    /// 入档=KingdomEntryData.raceId；旧档缺字段 → JsonUtility 缺省解析=0=Human（旧档全 Human 世界语义正确，零迁移器改动）。
    /// </summary>
    public int raceId = RaceIds.Human;

    /// <summary>是否为玩家王国（id=0，D303）。</summary>
    public bool IsPlayer => id == 0;

    /// <summary>起始国库过渡账本（baseStockpile D300；由 Foundry 步骤5 写入）。
    /// ⭐ `M1-A`／`09#50`：改「资源量列表」（五经济资源台账：金/石/木/粮/铁）。</summary>
    public ResourceList resources;

    // ===== 矿洞副产台账（DZ-072a，D562 / HH.107 件2）=====
    // 水晶/火油为副产稀缺资源，单列两桶不进 ResourcePack 五经济资源（AddWater/WaterNetwork 专用桶先例语义；
    // 造价/退还语义不涉，避免扩全局结构）。由 TaskScheduler.AddGatherOverflow/副产搬运分流入账，
    // TrainingSystem.PayRecruit 消费（P8 配对审：AI 侧转职水晶检解锁）。不入档（KingdomState=运行时态，每局 Foundry 重建）。
    /// <summary>副产水晶存量（AI 国库台账；玩家(0) 不用此桶——玩家走 TreasureVault）。</summary>
    public int crystal;
    /// <summary>副产火油存量（AI 国库台账；玩家(0) 不用此桶——玩家走 TreasureVault）。</summary>
    public int fireOil;
    /// <summary>矿石存量（AI 国库台账；T1.8/D609：矿石复活链 AI 侧独立桶，照水晶/火油先例——玩家(0) 不用此桶，走 TreasureVault.Ore）。</summary>
    public int ore;

    // ===== 科技解锁态 per-kingdom（2_17 步骤11 序0 schema 预留 + 序8 落地载体）=====
    // D330 第二步：CastleUnlockTable 解锁态 per-kingdom（动态立国科技不继承 D295）。
    // 载体迁址路径：KingdomManager.ModuleLevels（全局单例，玩家）→ 每王国一份 dict[id]。
    // 索引=ModuleType 枚举（CastleUnlockTable：Civil/Production/Livelihood/Military/Commerce，5 模块；
    // Science 已退役 D461 → 索引5 空置保留，数组长度恒 6=schema 零变更）。
    // 玩家 id=0 桶与原 KingdomManager.ModuleLevels 转置一致；AI 各国独立不串。
    // ⚠️ schema 完全拆分（KingdomSaveData kings[] 数组化）归 2_11 统一迁移（实施计划 L55：kingdoms[] 拆桶旧档展开单元素）。
    // 本步仅载体预留：KingdomState 持有本王国解锁态数组，供序8/序10 ExecuteTech 消费；未入存量存档（随 kingdoms[] 迁移入档）。

    /// <summary>本王国科技解锁态（6 模块等级；玩家桶=原 KingdomManager.ModuleLevels 转置；AI=本国）。</summary>
    public int[] moduleLevels;

    /// <summary>本王国主堡等级（0=废墟未修复，1-6；玩家桶=KingdomManager.CastleLevel 转置；AI 由王国脑/领土驱动）。</summary>
    public int castleLevel;

    // ===== 王国脑运行时态（2_17 步骤8，D317~D320/D322/D337/D338）=====
    // 由 KingdomBrainRegistry/KingdomBrain 于日 tick 写入，不入档（读档时王国脑由 Foundry 创建钩子重建）。
    // AI(id>0) 非空；玩家(id=0) 无脑 → scriptPhase=null（D338，情报面板不展示玩家剧本阶段）。

    /// <summary>剧本阶段（存活→发育→扩张→军事，单向 D318；玩家=null）。王国脑日 tick 同步。</summary>
    public ScriptStage? scriptPhase;

    /// <summary>国策焦点（0=无国策；&lt;0=常设底线焦点 FocusGranary/FocusDefense；&gt;0=步骤9 UtilityAction id）。</summary>
    public int focus;

    /// <summary>演算粒度（SimModeManager 判定；P0 恒 Fine，D333）。</summary>
    public SimMode simMode;

    // ===== 抽象结算运行时态（2_17 步骤14 批B；不入档 D456 同哲学，读档默认重建/派生）=====
    /// <summary>最近一次抽象结算的王国均饱食（-1=无抽象历史）。唤醒对账（D335/D460）：
    /// Abstract→Fine 首次日结由 SatietySystem 把实体饱食拉平到本值（确定性无跳变）。</summary>
    public float lastAbstractAvgSatiety = -1f;

    // ===== 人口（2_17 步骤4 台账转派生，实体=唯一真源）=====
    // ①真源演进规则（§〇 追记裁决①）：步骤3 实体化后工人/战士已为实体，步骤4 起由本属性对存活实体
    // 按 kingdomId 派生，不再由 Foundry 手写台账——防台账与实体双真源漂移（读档双份卡同族教训）。
    // 玩家=桶0，AI=各自桶；流浪汉领域外不计。原 public int workerCount/warriorCount 字段退役为只读派生。

    /// <summary>人口·工人数（按本王国存活 Worker/Porter/Civilian 实体派生）。</summary>
    public int workerCount => PopulationSystem.AliveWorkerCount(id);
    /// <summary>人口·战士数（按本王国存活军事职业实体派生）。</summary>
    public int warriorCount => PopulationSystem.AliveWarriorCount(id);

    // ===== 领土句柄（2_17 步骤6，D342 唯一真源=TerritorySystem）=====
    // 只读视图、不缓存数据——每次实时问 TerritorySystem，防双真源漂移。
    /// <summary>本王国领土中区块集合（无则空；真源在 TerritorySystem）。</summary>
    public IReadOnlyCollection<Vector2Int> Territory =>
        TerritorySystem.Instance != null ? TerritorySystem.Instance.GetKingdomTerritory(id) : Array.Empty<Vector2Int>();

    /// <summary>读取某轴性格（越界返回 0.5 中性闭合，全无来源基线，D295 占位）。</summary>
    public float GetPersonality(int axis)
    {
        if (personality == null || axis < 0 || axis >= personality.Length) return 0.5f;
        return personality[axis];
    }

    // ===== 国库真源读 API（2_17 步骤 2a：KingdomState.resources 转正为 AI 国库台账）
    // 2_17 §〇 追记② 裁 B：AI 经济=台账制（与 P0 人口台账同哲学），独立于 RulerController/WarehouseRegistry/
    // TreasureVault（玩家物流专用）。语义镜像 PlayerRuler.CanAfford/Spend/Refund（弹药不参与造价，仅五经济资源）。
    // 2_17 前 AI 无脑不消费，本 API 由王国脑（步骤 8+）消费；确定性、无事件发布（台账制）。

    /// <summary>是否负担得起该资源包（原子校验；弹药不参与造价）。
    /// ⭐ `M1-A`：逐条目判（旧「五经济资源四连 &&」⇒ 通用列表），条目不在台账内者按 0 判（与旧口径一致：
    /// 旧 `ResourcePack` 表达不了的资源在旧实现里同样不被校验）。</summary>
    public bool CanAfford(ResourceList cost)
    {
        if (cost.items == null) return true;
        for (int i = 0; i < cost.items.Length; i++)
        {
            var e = cost.items[i];
            if (e.amount > 0 && GetResourceValue(e.type) < e.amount) return false;
        }
        return true;
    }

    /// <summary>按类型读取国库某资源（军工/需求强度缺口函数消费）。DZ-072a：副产两桶并入（AI 可感知水晶/火油缺口）。
    /// ⭐ `M1-A`：五经济资源改读「资源量列表」`resources.Get(type)`（数值不变）；副产三桶原样。</summary>
    public int GetResourceValue(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Crystal: return crystal;    // DZ-072a 副产台账
            case ResourceType.FireOil: return fireOil;    // DZ-072a 副产台账
            case ResourceType.Ore: return ore;            // T1.8（D609）矿石台账桶
            default: return resources.Get(type);          // 金/石/木/粮/铁（及未来新增资源）
        }
    }

    /// <summary>扣除资源包（调用前需先 CanAfford；台账制直接减字段，不进玩家事件链）。
    /// 2_23 资源 P0 批A/R-A1（D630 收支口径 A+）：支出登记进经济诊断当日窗口（负数=出）。
    /// ⭐ `M1-A`：逐条目扣（五经济资源走 `resources` ＋ 副产三台账桶走各自字段）。</summary>
    public void Spend(ResourceList cost)
    {
        int g = 0, s = 0, w = 0, f = 0, m = 0;
        if (cost.items != null)
        {
            for (int i = 0; i < cost.items.Length; i++)
            {
                var e = cost.items[i];
                if (e.amount <= 0) continue;
                AddToLedger(e.type, -e.amount);
                AccumulateFlow(e.type, e.amount, ref g, ref s, ref w, ref f, ref m);
            }
        }
        EconomyDiagnosis.RegisterFlow(id, -g, -s, -w, -f, -m);
    }

    /// <summary>按比例退还资源包（metal 随比退还，不静默丢铁）。
    /// 2_23 资源 P0 批A/R-A1（D632 A′）：改走 AddResources 收口台账直写——本次退款计入经济诊断
    /// 入账窗口（一致性；本点为潜伏点：AI 生产调用点=0，玩家走 RulerController.Refund 独立通道）。
    /// ⚠️ `M1-C` 件2／件3 后**拆除退款已不由此路径**（改「随本体掉箱」⇒ `ChestManager.SpawnChest`）
    ///   ⇒ 陈注释「拆除退款 ratio=0.5 等」已勘正；`ratio` 形参在生产码中只剩默认值 `1.0` 调用
    ///   （唯一调用方 `SiegeProductionSystem`），形参去留归后续片（⛔ 本片不改签名）。</summary>
    public void Refund(ResourceList cost, float ratio = 1.0f)
    {
        AddResources(cost * ratio);   // ⭐ M1-A：逐条目按比例（Mathf.RoundToInt，与旧逐字段同口径）
    }

    /// <summary>国库入账（产出/采集入台账；加总）。
    /// 2_23 资源 P0 批A/R-A1（D630 收支口径 A+）：入账登记进经济诊断当日窗口
    /// （负值=出账，AbstractEconomySettlement.ApplyDelta 带符号增量走本口）。
    /// ⭐ `M1-A`：逐条目入账（五经济资源走 `resources` ＋ 副产三台账桶走各自字段）。</summary>
    public void AddResources(ResourceList gain)
    {
        int g = 0, s = 0, w = 0, f = 0, m = 0;
        if (gain.items != null)
        {
            for (int i = 0; i < gain.items.Length; i++)
            {
                var e = gain.items[i];
                if (e.amount == 0) continue;
                AddToLedger(e.type, e.amount);
                AccumulateFlow(e.type, e.amount, ref g, ref s, ref w, ref f, ref m);
            }
        }
        EconomyDiagnosis.RegisterFlow(id, g, s, w, f, m);
    }

    /// <summary>台账增减（⭐ M1-A 单源）：五经济资源走 `resources`（资源量列表）；
    /// 副产/矿石三台账桶（DZ-072a／T1.8 既有独立桶）走各自 int 字段 —— 行为逐位不变。</summary>
    private void AddToLedger(ResourceType type, int delta)
    {
        if (delta == 0) return;
        switch (type)
        {
            case ResourceType.Crystal: crystal += delta; break;
            case ResourceType.FireOil: fireOil += delta; break;
            case ResourceType.Ore: ore += delta; break;
            default: resources = resources.Add(type, delta); break;
        }
    }

    /// <summary>汇总五经济资源的本笔增量（供经济诊断窗口登记；非五资源不计）。</summary>
    private static void AccumulateFlow(ResourceType type, int amount, ref int g, ref int s, ref int w, ref int f, ref int m)
    {
        switch (type)
        {
            case ResourceType.Gold: g += amount; break;
            case ResourceType.Stone: s += amount; break;
            case ResourceType.Wood: w += amount; break;
            case ResourceType.Food: f += amount; break;
            case ResourceType.Metal: m += amount; break;
        }
    }
}