using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 建筑注册表（3.3 主体 + 2_2 footprint 索引）。单例，Runtime Set 风格存储所有活跃 Building 实例。
/// BuildingFactory / BuildController 注册（RegisterFootprint 全覆盖格反查），Die / TeardownManager 注销。
/// 查询：按任意格（含 footprint 覆盖格）查 / 矩形区域查 / 遍历全部。
/// </summary>
public class BuildingRegistry : Singleton<BuildingRegistry>
{
    private readonly List<Building> _all = new List<Building>();
    private readonly Dictionary<GridCoord, Building> _byCoord = new Dictionary<GridCoord, Building>();

    public int Count => _all.Count;
    public IReadOnlyList<Building> All => _all;

    /// <summary>注册建筑（2_2：footprint 覆盖格全量登记反查）。重复注册忽略。</summary>
    public void Register(Building b)
    {
        if (b == null || _all.Contains(b)) return;
        _all.Add(b);
        RegisterFootprintCells(b);
    }

    /// <summary>注销建筑（按 b.coord + b.footprint 清除全部覆盖格）。
    /// 【`F-15` 修法小批·`F15-A`】**加归属判定**：仅当该格当前指向**自己**时才删 —— 改前为**无条件** `Remove`，
    /// 会把**后注册者**在该格的反查项一并抹掉（后写者"查不到"）。⛔ 未动 `_all.Remove(b)`（只删自己·已正确）
    /// 与 `Register`／`RegisterFootprintCells`（**后写者胜保持为现行语义**）。
    /// 【`HH.312` 收尾批】A/B 对照开关已于本批删除；改前读数落盘 `HH.311` 报告／`Logs/hh310_f15/`。</summary>
    public void Unregister(Building b)
    {
        if (b == null) return;
        _all.Remove(b);
        int w = Mathf.Max(1, b.footprint.x), h = Mathf.Max(1, b.footprint.y);
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
            {
                var k = new GridCoord(b.coord.x + dx, b.coord.y + dy);
                if (_byCoord.TryGetValue(k, out var cur) && cur == b) _byCoord.Remove(k);
            }
    }

    /// <summary>按任意格查建筑（主格与 footprint 覆盖格均命中同一 Building）。</summary>
    public Building GetAt(GridCoord coord)
    {
        _byCoord.TryGetValue(coord, out var b);
        return b;
    }

    /// <summary>矩形区域查询（2_2）：任一覆盖格落在 rect 内即命中。</summary>
    public List<Building> GetInRect(RectInt rect)
    {
        var result = new List<Building>();
        for (int y = rect.yMin; y < rect.yMax; y++)
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                var b = GetAt(new GridCoord(x, y));
                if (b != null && !result.Contains(b)) result.Add(b);
            }
        return result;
    }

    /// <summary>清空注册表（跨岛切换/回主菜单时调）。</summary>
    public void Clear()
    {
        _all.Clear();
        _byCoord.Clear();
    }

    /// <summary>footprint 覆盖格逐格登记。
    /// 【`F-15` 修法小批·`F15-B` **注释勘正**】改前原文为「重复格以**先**注册者为准，正常流程校验已排除重叠」——
    /// **与实现相反**，且后半已被 `F-15` 证伪。实况三点：
    /// ① **重复格 ＝ 后注册者胜**（实读本方法末行为**无条件覆写** `_byCoord[...] = b`，非"先注册者"；
    /// ② **生成期"预置落点"不查占格** ⇒ 实盘存在 **9 座**建筑 footprint 相互重叠（`F-15`·`03` §6.5）；
    /// ③ **共享格归属不可恢复**（`_byCoord` 与 `GridSystem._occupants` **均单槽存储** ⇒ 前一个占用者已被覆盖丢失）。
    /// 口径源＝`03` §6.5（`D782`·取向＝「允许重叠 ＋ 显式化」）。</summary>
    private void RegisterFootprintCells(Building b)
    {
        int w = Mathf.Max(1, b.footprint.x), h = Mathf.Max(1, b.footprint.y);
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                _byCoord[new GridCoord(b.coord.x + dx, b.coord.y + dy)] = b;
    }
}
