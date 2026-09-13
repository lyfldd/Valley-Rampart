using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地面效果管理器（3.6 §3.4 介质层）。
/// Burn 灼烧场（每 tick 敌对伤害）/ Slow 减速场（区域减速）/ Heal 治疗场（范围内有限个）。
/// 由投射物命中落地生成（ProjectileManager → SpawnEffect），Update 统一结算。
/// </summary>
public class GroundEffectManager : Singleton<GroundEffectManager>
{
    private class Effect
    {
        public Vector2 pos;
        public IDamageable source;
        public GroundEffectType type;
        public float radiusCells;   // 2_5 步骤7：半径改格单位（不再存 world=格×cellSize）
        public float duration;
        public float tickInterval;
        public float power;
        public int maxTargets;
        public float elapsed;
        public float nextTick;
        public GameObject visual;   // HH.239 T14：命中特效真图（SpriteRefTable fx_* 旁挂键；缺图 = null 不回退不崩）
    }

    private readonly List<Effect> _effects = new();

    /// <summary>投射物命中后落地效果（3.6 §3.4）。</summary>
    public void SpawnEffect(Vector2 pos, IDamageable source, GroundEffectType type,
        float radiusCells, float duration, float tickInterval, float power, int maxTargets)
    {
        if (type == GroundEffectType.None || radiusCells <= 0f || duration <= 0f) return;

        _effects.Add(new Effect
        {
            pos = pos,
            source = source,
            type = type,
            radiusCells = radiusCells,          // 直接存格单位（2_5 步骤7）
            duration = duration,
            tickInterval = Mathf.Max(0.1f, tickInterval),
            power = power,
            maxTargets = maxTargets,
            elapsed = 0f,
            nextTick = 0f,
            visual = SpawnVisual(pos, type, radiusCells),
        });
    }

    /// <summary>命中特效可视（HH.239 T14）：真图表 <c>fx_*</c> 旁挂键（**不动** GroundEffectDef —— 撞 H3 AI.Core 红线）。
    /// 缺图（无键/表缺失）⇒ 返回 null = 静默无特效（不崩）。</summary>
    private static GameObject SpawnVisual(Vector2 pos, GroundEffectType type, float radiusCells)
    {
        string artId = type switch
        {
            GroundEffectType.Burn => "fx_fireball",   // 火弹命中 → Burn 场
            GroundEffectType.Slow => "fx_magic",      // 魔弹命中 → Slow 场
            _ => null,                                 // Heal 无素材（映射表 §十一 仅 3 张命中特效）
        };
        if (artId == null) return null;

        var table = ValleyRampart.Rendering.SpriteRefTable.Instance;
        if (table == null || !table.TryGet(artId, out var spr) || spr == null) return null;

        var go = new GameObject($"GroundEffect_{type}");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spr;
        sr.sortingOrder = 0;   // 地皮层（Region:0），压在单位/建筑之下
        // 直径对齐效果直径（2×radiusCells 格宽）
        float cellW = GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize.x : 1.28f;
        float targetDia = Mathf.Max(0.1f, radiusCells * 2f * cellW);
        float sprW = Mathf.Max(0.0001f, spr.bounds.size.x);
        float k = targetDia / sprW;
        go.transform.localScale = new Vector3(k, k, 1f);
        return go;
    }

    /// <summary>销毁特效可视（含异常终结/清场路径）。</summary>
    private static void DestroyVisual(Effect e)
    {
        if (e == null || e.visual == null) return;
        if (Application.isPlaying) Destroy(e.visual); else DestroyImmediate(e.visual);
        e.visual = null;
    }

    /// <summary>清空全部效果（生命周期清算/异常终结；防可视残留泄漏）。</summary>
    public void ClearAllEffects()
    {
        for (int i = _effects.Count - 1; i >= 0; i--) DestroyVisual(_effects[i]);
        _effects.Clear();
    }

    private void OnDestroy() => ClearAllEffects();

    private void Update()
    {
        if (_effects.Count == 0) return;

        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            var e = _effects[i];
            e.elapsed += Time.deltaTime;

            if (e.elapsed >= e.duration)
            {
                DestroyVisual(e);
                _effects.RemoveAt(i);
                continue;
            }

            if (Time.time >= e.nextTick)
            {
                e.nextTick = Time.time + e.tickInterval;
                Tick(e);
            }
        }
    }

    private void Tick(Effect e)
    {
        switch (e.type)
        {
            case GroundEffectType.Burn:
                TickBurn(e);
                break;
            case GroundEffectType.Slow:
                TickSlow(e);
                break;
            case GroundEffectType.Heal:
                TickHeal(e);
                break;
        }
    }

    /// <summary>灼烧：区域内敌对单位每 tick 受 power 伤害（走伤害管线，触发免伤/死亡）。</summary>
    private void TickBurn(Effect e)
    {
        var units = QueryUnitsInRadius(e.pos, e.radiusCells);
        Faction sourceFaction = e.source != null ? e.source.GetFaction() : Faction.None;

        foreach (var unit in units)
        {
            if (unit == null || unit.CurrentHp <= 0) continue;
            if (unit.GetFaction() == sourceFaction || unit.GetFaction() == Faction.None) continue;
            if (DamageSystem.Instance == null) continue;
            DamageSystem.Instance.ApplyDamage(e.source, unit, Mathf.Max(1, Mathf.RoundToInt(e.power)));
        }
    }

    /// <summary>减速：区域内敌对单位减速（取最大系数）。</summary>
    private void TickSlow(Effect e)
    {
        var units = QueryUnitsInRadius(e.pos, e.radiusCells);
        Faction sourceFaction = e.source != null ? e.source.GetFaction() : Faction.None;

        foreach (var unit in units)
        {
            if (unit == null || unit.CurrentHp <= 0) continue;
            if (unit.GetFaction() == sourceFaction || unit.GetFaction() == Faction.None) continue;
            if (unit is UnitController uc)
                uc.ApplySlow(e.power, e.tickInterval + 0.1f);
        }
    }

    /// <summary>治疗：区域内友军按低血优先，最多 maxTargets 个（3.6 Heal 场"有限个"）。</summary>
    private void TickHeal(Effect e)
    {
        var units = QueryUnitsInRadius(e.pos, e.radiusCells);
        Faction sourceFaction = e.source != null ? e.source.GetFaction() : Faction.None;

        // 友军按血量升序（低血优先）
        units.Sort((a, b) =>
        {
            float ra = a.MaxHp > 0 ? (float)a.CurrentHp / a.MaxHp : 1f;
            float rb = b.MaxHp > 0 ? (float)b.CurrentHp / b.MaxHp : 1f;
            return ra.CompareTo(rb);
        });

        int healed = 0;
        foreach (var unit in units)
        {
            if (unit == null || unit.CurrentHp <= 0) continue;
            if (unit.GetFaction() != sourceFaction) continue;
            if (e.maxTargets > 0 && healed >= e.maxTargets) break;
            unit.Heal(Mathf.Max(1, Mathf.RoundToInt(e.power)));
            healed++;
        }
    }

    /// <summary>查 worldPos 半径（格单位）内单位（doc1 微格主表 D70，2_5 步骤3/7，格单位精确过滤）。</summary>
    private List<UnitController> QueryUnitsInRadius(Vector2 worldPos, float radiusCells)
    {
        var result = new List<UnitController>();
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return result;

        int subDiv = GridSystem.Instance.Config.subCellDivisor;
        var centerOpt = GridSystem.Instance.WorldToSubCoord(worldPos);
        if (!centerOpt.HasValue) return result; // doc1 改造：越界返回 null，返回空列表
        GridCoord center = centerOpt.Value;
        int subRange = Mathf.Max(0, Mathf.CeilToInt(radiusCells * subDiv));

        for (int dy = -subRange; dy <= subRange; dy++)
        {
            for (int dx = -subRange; dx <= subRange; dx++)
            {
                var list = GridSystem.Instance.GetUnitsInSubCell(new GridCoord(center.x + dx, center.y + dy));
                foreach (var unit in list)
                {
                    if (unit == null) continue;
                    if (GridMath.DistCells(worldPos, unit.GetPosition()) > radiusCells) continue;
                    result.Add(unit);
                }
            }
        }
        return result;
    }
}
