using System.Collections.Generic;

/// <summary>
/// 建筑注册表（3.3 主体）。单例，Runtime Set 风格存储所有活跃 Building 实例。
/// BuildingFactory / BuildController 注册，Die / TeardownManager 注销。
/// 查询：按 origin 坐标查 / 遍历全部。查任意格（含 footprint 覆盖格）用 GridSystem.GetOccupant。
/// </summary>
public class BuildingRegistry : Singleton<BuildingRegistry>
{
    private readonly List<Building> _all = new List<Building>();
    private readonly Dictionary<GridCoord, Building> _byCoord = new Dictionary<GridCoord, Building>();

    public int Count => _all.Count;
    public IReadOnlyList<Building> All => _all;

    /// <summary>注册建筑。重复注册忽略。</summary>
    public void Register(Building b)
    {
        if (b == null || _all.Contains(b)) return;
        _all.Add(b);
        _byCoord[b.coord] = b;
    }

    /// <summary>注销建筑。</summary>
    public void Unregister(Building b)
    {
        if (b == null) return;
        _all.Remove(b);
        _byCoord.Remove(b.coord);
    }

    /// <summary>按 origin 坐标查建筑。</summary>
    public Building GetAt(GridCoord coord)
    {
        _byCoord.TryGetValue(coord, out var b);
        return b;
    }

    /// <summary>清空注册表（跨岛切换/回主菜单时调）。</summary>
    public void Clear()
    {
        _all.Clear();
        _byCoord.Clear();
    }
}
