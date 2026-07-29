using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BuildingType → BuildingDef 映射表（3.3.1 P6 方案A）。
/// BuildingFactory 用此表把地图生成的 BuildingPlaceholder（type+grade+isConsumable）
/// 转为运行时 Building 实例，查到对应 BuildingDef 后按 gradeScale 缩放属性。
///
/// 配置资产放 Resources/Buildings/BuildingMappingTable.asset。
/// 11 种 BuildingType 各对应一个 BuildingDef；grade 缩放由 BuildingDef.gradeScale 处理，无需每组合一个 asset。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/BuildingMappingTable", fileName = "BuildingMappingTable")]
public class BuildingMappingTable : ScriptableObject
{
    [Tooltip("11 种 BuildingType → BuildingDef 映射。未列出的类型 BuildingFactory 会跳过并警告。")]
    public MappingEntry[] entries;

    private Dictionary<BuildingType, BuildingDef> _lookup;

    /// <summary>按 BuildingType 查 BuildingDef。未配置返回 null。</summary>
    public BuildingDef Get(BuildingType type)
    {
        if (_lookup == null) BuildLookup();
        _lookup.TryGetValue(type, out var def);
        return def;
    }

    void BuildLookup()
    {
        _lookup = new Dictionary<BuildingType, BuildingDef>();
        if (entries == null) return;
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e.def != null)
                _lookup[e.type] = e.def;
        }
    }
}

/// <summary>映射表条目。</summary>
[Serializable]
public struct MappingEntry
{
    public BuildingType type;   // 地图占位类型（Tree/Mine/Farmland/...）
    public BuildingDef def;     // 对应的 BuildingDef SO
}
