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
        public float radiusWorld;
        public float duration;
        public float tickInterval;
        public float power;
        public int maxTargets;
        public float elapsed;
        public float nextTick;
    }

    private readonly List<Effect> _effects = new();

    /// <summary>投射物命中后落地效果（3.6 §3.4）。</summary>
    public void SpawnEffect(Vector2 pos, IDamageable source, GroundEffectType type,
        float radiusCells, float duration, float tickInterval, float power, int maxTargets)
    {
        if (type == GroundEffectType.None || radiusCells <= 0f || duration <= 0f) return;

        float cellSize = GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize.x : 2.26f;

        _effects.Add(new Effect
        {
            pos = pos,
            source = source,
            type = type,
            radiusWorld = radiusCells * cellSize,
            duration = duration,
            tickInterval = Mathf.Max(0.1f, tickInterval),
            power = power,
            maxTargets = maxTargets,
            elapsed = 0f,
            nextTick = 0f,
        });
    }

    private void Update()
    {
        if (_effects.Count == 0) return;

        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            var e = _effects[i];
            e.elapsed += Time.deltaTime;

            if (e.elapsed >= e.duration)
            {
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
        var units = QueryUnitsInRadius(e.pos, e.radiusWorld);
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
        var units = QueryUnitsInRadius(e.pos, e.radiusWorld);
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
        var units = QueryUnitsInRadius(e.pos, e.radiusWorld);
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

    /// <summary>查 worldPos 半径内单位（空间分区，复用 GridSystem）。</summary>
    private List<UnitController> QueryUnitsInRadius(Vector2 worldPos, float radiusWorld)
    {
        var result = new List<UnitController>();
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return result;

        float cellSize = GridSystem.Instance.Config.cellSize.x;
        var centerOpt = GridSystem.Instance.WorldToCoord(worldPos);
        if (!centerOpt.HasValue) return result; // doc1 改造：越界返回 null，返回空列表
        GridCoord center = centerOpt.Value;
        int cellRange = Mathf.Max(1, Mathf.CeilToInt(radiusWorld / cellSize));

        for (int dx = -cellRange; dx <= cellRange; dx++)
        {
            for (int y = 0; y <= 1; y++)
            {
                var coord = new GridCoord(center.x + dx, y);
                result.AddRange(GridSystem.Instance.GetUnitsInCell(coord));
            }
        }
        return result;
    }
}
