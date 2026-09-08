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
            // D563②：注册/注销逐条日志改汇总口径（一次性建局/清场打一行计数）——明细静默
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
            // D563②：汇总口径——注销明细静默，计数汇入清场/建局汇总行
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
    /// D563② 汇总口径：清场侧一次性打一行计数（注销明细已静默）。
    /// </summary>
    public void Clear()
    {
        int cleared = _aliveUnits.Count;
        _aliveUnits.Clear();
        Debug.Log($"[UnitRegistry] 清场汇总：本次注销 {cleared} 个单位（注册/注销明细已静默）");
    }

    /// <summary>
    /// D563② 汇总口径：建局侧一次性打一行计数快照（供开局实体生成等节点调用，替代逐单位注册日志）。
    /// </summary>
    public void LogCountSnapshot(string reason)
    {
        Debug.Log($"[UnitRegistry] 单位计数快照[{reason}]：当前注册 {Count} 个单位");
    }
}
