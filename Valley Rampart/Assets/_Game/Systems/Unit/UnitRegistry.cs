using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单位注册中心。追踪场景中所有存活的单位。
/// 单位在 Initialize 时自动注册，Die 时自动注销。
/// 提供查询接口（按阵营、最近敌人等），供 AI、UI、存档使用。
/// </summary>
public class UnitRegistry : Singleton<UnitRegistry>
{
    private readonly HashSet<UnitController> _aliveUnits = new HashSet<UnitController>();

    /// <summary>
    /// 当前存活单位总数。
    /// </summary>
    public int Count => _aliveUnits.Count;

    /// <summary>
    /// 注册一个单位。由 UnitController.Initialize 自动调用。
    /// </summary>
    public void Register(UnitController unit)
    {
        if (unit == null) return;

        if (_aliveUnits.Add(unit))
        {
            Debug.Log($"[UnitRegistry] 注册: {unit.Data?.faction}_{unit.Data?.occupation}，当前共 {Count} 个单位");
        }
    }

    /// <summary>
    /// 注销一个单位。由 UnitController.Die 自动调用。
    /// </summary>
    public void Unregister(UnitController unit)
    {
        if (unit == null) return;

        if (_aliveUnits.Remove(unit))
        {
            Debug.Log($"[UnitRegistry] 注销: {unit.Data?.faction}_{unit.Data?.occupation}，当前剩 {Count} 个单位");
        }
    }

    /// <summary>
    /// 获取所有存活单位。
    /// </summary>
    public IEnumerable<UnitController> GetAllUnits()
    {
        return _aliveUnits;
    }

    /// <summary>
    /// 按阵营获取单位。
    /// </summary>
    public List<UnitController> GetUnitsByFaction(Faction faction)
    {
        var result = new List<UnitController>();

        foreach (var unit in _aliveUnits)
        {
            if (unit.Data != null && unit.Data.faction == faction)
            {
                result.Add(unit);
            }
        }

        return result;
    }

    /// <summary>
    /// 获取指定阵营的所有敌人（不同阵营的单位）。
    /// </summary>
    public List<UnitController> GetEnemies(Faction myFaction)
    {
        var result = new List<UnitController>();

        foreach (var unit in _aliveUnits)
        {
            if (unit.Data != null && unit.Data.faction != myFaction && unit.Data.faction != Faction.None)
            {
                result.Add(unit);
            }
        }

        return result;
    }

    /// <summary>
    /// 查找距离指定位置最近的敌方单位。
    /// </summary>
    public UnitController GetNearestEnemy(Vector3 position, Faction myFaction)
    {
        UnitController nearest = null;
        float minDist = float.MaxValue;

        foreach (var unit in _aliveUnits)
        {
            if (unit.Data == null) continue;
            if (unit.Data.faction == myFaction || unit.Data.faction == Faction.None) continue;

            float dist = Vector3.Distance(position, unit.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = unit;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 清空注册表（场景切换/重置时使用）。
    /// </summary>
    public void Clear()
    {
        _aliveUnits.Clear();
        Debug.Log("[UnitRegistry] 已清空所有注册单位");
    }
}
