using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  3.0.1_5 多将军协作与管理 - FormationManager 编队注册表（E 键作战面板后端接口）
//  详见 3.0.1_5_多将军协作与管理方向.md §六
//  先接口后 UI：本类是作战面板的纯后端，UI 只调用公开方法，不直接访问 FormationController。
//  职责：编队注册表（查询/按 id 选队/多选集合）/ 批量军令下发 / 中区块编队上限登记
// ============================================================================

/// <summary>
/// 编队管理器（§六，Singleton）。
/// FormationController 在 OnEnable/OnDisable 自动注册/注销（编队解散/将军阵亡自然移除）。
/// 作战面板（P2 UI）通过本类：查询所有编队 → 混合双选（点击/框选/Ctrl 多选）→ SetIntentFor 批量军令。
/// 中区块登记（§五）：CountInMidRegion / CanAddInMidRegion 保证单中区块最多 4 编队。
/// </summary>
public class FormationManager : Singleton<FormationManager>
{
    // ===== 编队注册表 =====
    private readonly List<FormationController> _formations = new List<FormationController>();

    /// <summary>当前选中编队 id 集合（作战面板多选状态）</summary>
    private readonly HashSet<int> _selected = new HashSet<int>();

    /// <summary>全部活跃编队（只读视图）</summary>
    public IReadOnlyList<FormationController> AllFormations => _formations;
    /// <summary>活跃编队数</summary>
    public int FormationCount => _formations.Count;
    /// <summary>选中编队数</summary>
    public int SelectedCount => _selected.Count;

    /// <summary>编队 id（用 GameObject 实例 id，稳定且唯一）</summary>
    public int FormationId(FormationController fc) => fc != null ? fc.GetInstanceID() : 0;

    // ===== 注册 / 注销（FormationController.OnEnable/OnDisable 自动调）=====

    public void Register(FormationController fc)
    {
        if (fc == null || _formations.Contains(fc)) return;
        _formations.Add(fc);
    }

    public void Unregister(FormationController fc)
    {
        if (fc == null) return;
        _formations.Remove(fc);
        // 编队销毁时同步清理选中状态
        int id = FormationId(fc);
        if (_selected.Remove(id))
            PublishDeselected(id);
    }

    // ===== 查询 =====

    /// <summary>按 id 查编队（未找到返回 null）</summary>
    public FormationController GetById(int id)
    {
        for (int i = 0; i < _formations.Count; i++)
        {
            if (_formations[i] != null && _formations[i].GetInstanceID() == id)
                return _formations[i];
        }
        return null;
    }

    // ===== 选择（作战面板混合双选：点击单选 / 左键框选 / Ctrl 多选）=====

    /// <summary>选中单个编队（追加到选择集合；已选中则忽略）</summary>
    public void Select(int id)
    {
        if (_selected.Add(id))
        {
            if (EventBus.HasSubscribers<FormationSelectedEvent>())
                EventBus.Publish(new FormationSelectedEvent(id));
        }
    }

    /// <summary>取消选中单个编队</summary>
    public void Deselect(int id)
    {
        if (_selected.Remove(id))
            PublishDeselected(id);
    }

    /// <summary>清空全部选中（框选新区域/关闭面板时）</summary>
    public void ClearSelection()
    {
        foreach (int id in new List<int>(_selected))
            Deselect(id);
    }

    /// <summary>是否选中</summary>
    public bool IsSelected(int id) => _selected.Contains(id);

    /// <summary>选中 id 集合（只读视图，供 UI 渲染）</summary>
    public IReadOnlyCollection<int> SelectedIds => _selected;

    private void PublishDeselected(int id)
    {
        if (EventBus.HasSubscribers<FormationDeselectedEvent>())
            EventBus.Publish(new FormationDeselectedEvent(id));
    }

    // ===== 批量军令（作战面板：选中编队下发攻/守/撤）=====

    /// <summary>对全部选中编队下发意图（多选批量军令）</summary>
    public void SetIntentForSelected(TacticIntent intent)
    {
        SetIntentFor(_selected, intent);
    }

    /// <summary>对指定 id 集合下发意图（SetIntentFor 批量下发）</summary>
    public void SetIntentFor(IEnumerable<int> ids, TacticIntent intent)
    {
        if (ids == null) return;
        int count = 0;
        foreach (int id in ids)
        {
            var fc = GetById(id);
            if (fc == null) continue;
            fc.SetIntent(intent);
            count++;
        }
        if (count > 0)
            Debug.Log($"[FormationManager] 批量军令 {intent}：下发 {count} 编队。");
    }

    /// <summary>对全部编队下发意图（全选——单边限制由 UI 层约束，后端按传入集合执行）</summary>
    public void SetIntentAll(TacticIntent intent)
    {
        for (int i = 0; i < _formations.Count; i++)
        {
            if (_formations[i] != null)
                _formations[i].SetIntent(intent);
        }
        Debug.Log($"[FormationManager] 全编队军令 {intent}：{_formations.Count} 编队。");
    }

    // ===== 中区块编队上限登记（3.0.1_5 §五：单中区块最多 4 编队）=====

    /// <summary>统计某世界坐标所在中区块的活跃编队数</summary>
    public int CountInMidRegion(Vector2 worldPos)
    {
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return 0;
        var coordOpt = GridSystem.Instance.WorldToCoord(worldPos);
        if (!coordOpt.HasValue) return 0; // doc1 改造：越界返回 null，编队数记 0
        int mid = GridSystem.Instance.CellToMidRegionIndex(coordOpt.Value.x);
        int count = 0;
        for (int i = 0; i < _formations.Count; i++)
        {
            var fc = _formations[i];
            if (fc == null || fc.Anchor == null) continue;
            var c2Opt = GridSystem.Instance.WorldToCoord(fc.Anchor.position);
            if (!c2Opt.HasValue) continue; // doc1 改造：锚点越界不计入
            if (GridSystem.Instance.CellToMidRegionIndex(c2Opt.Value.x) == mid)
                count++;
        }
        return count;
    }

    /// <summary>中区块是否还能容纳新编队（单中区块最多 maxPerMid 个编队）</summary>
    public bool CanAddInMidRegion(Vector2 worldPos, int maxPerMid = 4)
    {
        return CountInMidRegion(worldPos) < maxPerMid;
    }
}
