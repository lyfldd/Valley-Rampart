using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  粒度切换（SimMode）判定（2_17 步骤8 骨架；步骤13 落真实判定，HH.42 §三批A）
//  D333/D344：活跃带覆盖→Fine（立即）/ 连续 N 日未覆盖→Abstract（迟滞）/
//  领土内战斗热点→强制 Fine（战斗锁，事件驱动立即切，工人逃跑/救火必须真跑）。
//  实体策略=常驻+休眠（D334，批B 在 NPCBrain 落地停 Think/停寻路）；对账归步骤14（D335）。
//  simMode 为每日重判派生视图案（D347 五步①），不入档（D456；读档默认 Fine 续跑无跳变）。
//  SimMode 枚举属本系统，供 KingdomState.simMode 字段引用。
// ============================================================================

/// <summary>演算粒度（D333）。Fine=细模拟（实体驱动）；Abstract=抽象结算（纯 C# 日 tick）。</summary>
public enum SimMode : byte
{
    Fine = 0,      // 细模拟（王国脑 KingdomBrain 实体驱动）
    Abstract = 1   // 抽象结算（AbstractEconomySettler 纯 C#；P1 步骤14）
}

/// <summary>
/// 粒度切换管理器（2_17 步骤13 落真实判定）。
/// 每日判定（DayCycleSettlement 五步①）→ 写 KingdomState.simMode；KingdomBrain.Tick 经 GetMode 读。
/// </summary>
public class SimModeManager : Singleton<SimModeManager>
{
    private SimModeConfig _config;

    // 连续未被活跃带覆盖的日数（per-kingdom，运行时态不入档 D456；MapGenerated 清空）
    private readonly Dictionary<int, int> _uncoveredDays = new Dictionary<int, int>();

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        _config = Resources.Load<SimModeConfig>("Config/Kingdoms/SimModeConfig");
        if (_config == null)
            Debug.LogWarning("[SimModeManager] 未找到 SimModeConfig，回退默认（offscreenDays=2 / combatHotspotForceFine=true）");
        EventBus.Subscribe<UnitDamagedEvent>(OnCombatEvent);
        EventBus.Subscribe<EnemyEnteredChunkEvent>(OnEnemyEnteredChunk);
        EventBus.Subscribe<MapGeneratedEvent>(OnMapGenerated);
    }

    protected override void OnDestroy()
    {
        if (_instance != this) return;
        base.OnDestroy();
        EventBus.Unsubscribe<UnitDamagedEvent>(OnCombatEvent);
        EventBus.Unsubscribe<EnemyEnteredChunkEvent>(OnEnemyEnteredChunk);
        EventBus.Unsubscribe<MapGeneratedEvent>(OnMapGenerated);
    }

    private void OnMapGenerated(MapGeneratedEvent evt) => _uncoveredDays.Clear();

    /// <summary>查询某王国日 tick 演算粒度（读 KingdomState.simMode，由日 tick 判定写入）。玩家/未知王国恒 Fine。</summary>
    public SimMode GetMode(int kingdomId)
    {
        var k = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(kingdomId) : null;
        return k != null ? k.simMode : SimMode.Fine;
    }

    /// <summary>
    /// 每日判定（DayCycleSettlement 五步① 调用；D347 步①）。
    /// 逐 AI 王国：领土∩活跃带→Fine（立即）；领土内战斗热点→Fine（强制）；
    /// 连续 N 日未被覆盖→Abstract（迟滞）；否则维持。写 KingdomState.simMode。
    /// </summary>
    public void EvaluateAllKingdoms()
    {
        var reg = KingdomRegistry.Instance;
        var lod = LODSystem.Instance;
        if (reg == null || lod == null) return;
        var all = reg.GetAll();
        for (int i = 0; i < all.Count; i++)
        {
            var k = all[i];
            if (k.IsPlayer) continue;   // 玩家无脑恒 Fine（D338）
            k.simMode = EvaluateKingdom(k, lod);
        }
    }

    private SimMode EvaluateKingdom(KingdomState k, LODSystem lod)
    {
        bool covered = false;
        bool hotspot = false;
        bool forceFine = _config == null || _config.combatHotspotForceFine;
        if (k.Territory != null)
        {
            foreach (var mid in k.Territory)
            {
                if (lod.IsActivelyCovered(mid)) { covered = true; break; }
                if (forceFine && lod.HasActiveCombatHotspot(mid)) { hotspot = true; break; }
            }
        }
        if (covered || hotspot)
        {
            _uncoveredDays[k.id] = 0;   // 覆盖/战斗 → 复位连续未覆盖日数
            return SimMode.Fine;
        }
        int days = (_uncoveredDays.TryGetValue(k.id, out var d) ? d : 0) + 1;
        _uncoveredDays[k.id] = days;
        int threshold = _config != null ? Mathf.Max(1, _config.offscreenDaysToAbstract) : 2;
        return days >= threshold ? SimMode.Abstract : SimMode.Fine;
    }

    // ===== 战斗锁（D333）：事件驱动立即切 Fine（军队打到家门口，工人逃跑/救火必须真跑）=====

    private void OnCombatEvent(UnitDamagedEvent evt) => ForceFineIfInAbstractTerritory(evt.Position);

    private void OnEnemyEnteredChunk(EnemyEnteredChunkEvent evt)
    {
        if (evt.Enemy != null)
            ForceFineIfInAbstractTerritory(evt.Enemy.transform.position);
    }

    /// <summary>战斗事件位置落入某 Abstract 王国领土 → 立即强制 Fine + 复位日数（无需等日 tick）。</summary>
    private void ForceFineIfInAbstractTerritory(Vector2 pos)
    {
        if (_config != null && !_config.combatHotspotForceFine) return;
        var reg = KingdomRegistry.Instance;
        if (reg == null || TerritorySystem.Instance == null || GridSystem.Instance == null) return;
        var coord = GridSystem.Instance.WorldToCoord(pos);
        if (!coord.HasValue) return;
        var mid = GridSystem.Instance.CellToMidChunk(coord.Value);
        var all = reg.GetAll();
        for (int i = 0; i < all.Count; i++)
        {
            var k = all[i];
            if (k.IsPlayer || k.simMode != SimMode.Abstract) continue;   // 只处理当前 Abstract 王国
            if (k.Territory == null) continue;
            foreach (var t in k.Territory)
            {
                if (t == mid)
                {
                    k.simMode = SimMode.Fine;
                    _uncoveredDays[k.id] = 0;
                    Debug.Log($"[SimModeManager] k{k.id} 领土内战斗热点 → 战斗锁强制 Fine（立即）");
                    break;
                }
            }
        }
    }
}
