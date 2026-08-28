using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 2_17 步骤6：领土账本（只读 + 染色事件，不推进）。
/// - 唯一真源（D342）：`Dictionary<中区块, kingdomId>` 是本系统独占真源；KingdomState.领土句柄仅只读视图，不缓存（防双源漂移）。
/// - 初始圈入（D343）：初始建筑外扩 1 中区块（Chebyshev 3×3，D343 未限定形状；推边界才用 D326 4-邻接）。
/// - 覆盖玩家(id=0)+AI 王国(1..N)；自然建筑 kingdomId==-1 不计入。
/// - P0 不推进：AI 推边界/玩家建造纳土/吞并归步骤12；本步只出账本 + 事件供其订阅。
/// 注入点：LoadManager.EnterPlaying（新游戏与读档两路建筑就位后统一重推；见 2_17 §〇 追记④债，步骤8/12 落地时加门控）。
/// </summary>
public class TerritorySystem : Singleton<TerritorySystem>
{
    private readonly Dictionary<Vector2Int, int> _territory = new Dictionary<Vector2Int, int>();

    /// <summary>账本唯一真源（只读视图）。</summary>
    public IReadOnlyDictionary<Vector2Int, int> Ledger => _territory;

    /// <summary>整张领土快照（供 2_10 染色 overlay；渲染归 2_10，本步只出数据）。</summary>
    public IReadOnlyDictionary<Vector2Int, int> GetAllTerritory() => _territory;

    /// <summary>某王国领土中区块集（KingdomState 领土句柄的底层查询；无则空集）。</summary>
    public IReadOnlyCollection<Vector2Int> GetKingdomTerritory(int kingdomId)
    {
        var result = new List<Vector2Int>();
        foreach (var kv in _territory)
            if (kv.Value == kingdomId)
                result.Add(kv.Key);
        // 排序保确定性（供染色/校验稳定）
        result.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return result;
    }

    /// <summary>是否某中区块归属某王国。</summary>
    public bool IsTerritory(Vector2Int mid, int kingdomId) =>
        _territory.TryGetValue(mid, out var k) && k == kingdomId;

    /// <summary>
    /// 从当前全部建筑重推初始领土（D343：初始建筑外扩 1 中区块，Chebyshev 3×3 并集）。
    /// 幂等、确定性：依 KingdomRegistry 建筑清单按 (kingdomId,x,y) 序扫描，中区块集排序后按 kingdomId 升序广播事件。
    /// P0 两路（新游戏/读档）都跑——读档从恢复建筑重推=正确（领土无存档）。
    /// </summary>
    public void RebuildInitial()
    {
        _territory.Clear();
        if (BuildingRegistry.Instance == null || BuildingRegistry.Instance.All == null)
            return;

        // 各王国 union 中区块（Chebyshev 1-ring：dx,dy ∈ {-1,0,1}）
        var perKingdom = new Dictionary<int, HashSet<Vector2Int>>();
        var buildList = new List<Building>();
        foreach (var b in BuildingRegistry.Instance.All)
            if (b != null && b.kingdomId >= 0)       // ⑤ 过滤：kingdomId==-1 自然建筑不计
                buildList.Add(b);
        buildList.Sort((a, b) =>
        {
            int c = a.kingdomId.CompareTo(b.kingdomId);
            if (c != 0) return c;
            c = a.coord.x.CompareTo(b.coord.x);
            if (c != 0) return c;
            return a.coord.y.CompareTo(b.coord.y);
        });

        for (int i = 0; i < buildList.Count; i++)
        {
            Building b = buildList[i];
            if (!(GridSystem.Instance != null)) continue;
            Vector2Int mid = GridSystem.Instance.CellToMidChunk(b.coord);
            if (!perKingdom.TryGetValue(b.kingdomId, out var cells)) { cells = new HashSet<Vector2Int>(); perKingdom[b.kingdomId] = cells; }
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    cells.Add(new Vector2Int(mid.x + dx, mid.y + dy));
        }

        // 确定性问题：按 kingdomId 升序写账本 + 广播事件（中区块域内排序）
        foreach (var k in perKingdom.Keys.OrderBy(x => x))
        {
            var cells = perKingdom[k].ToList();
            cells.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            foreach (var c in cells)
                _territory[c] = k;
            EventBus.Publish(new TerritoryChangedEvent(k, cells));
        }
    }

    // 计数器（验收/完整局用）
    /// <summary>某王国领土中区块数。</summary>
    public int KingdomCellCount(int kingdomId) => GetKingdomTerritory(kingdomId).Count;

    /// <summary>
    /// 2_17 步骤12 缺口① 补：动态立国初始圈入（D343 同款 3×3 中区块并集）。
    /// KingdomFoundry.FoundFromCamp 插旗后为新王国算初始圈（复用 CollectMidRing 同源逻辑），
    /// 写入账本 + 广播 TerritoryChangedEvent（坐标序排序保确定性，供 2_10 染色/校验稳定）。
    /// HH.33 §五 随裁修正：**只纳无主**（裁4/D327/D283 同源精神）——环内已有主格（含玩家 id=0）不覆写，
    /// 防边境立国静默夺取他国中区块；事件只广播实际纳入格。
    /// 幂等：无该王国建筑则空操作；全环已有主则不写不广播。
    /// </summary>
    public void ClaimInitial(int kingdomId)
    {
        var claimed = new List<Vector2Int>();
        foreach (var c in CollectMidRing(kingdomId))
        {
            if (_territory.TryGetValue(c, out int owner) && owner != kingdomId) continue;   // 只纳无主：他国已有主不覆写
            _territory[c] = kingdomId;
            claimed.Add(c);
        }
        if (claimed.Count == 0) return;
        claimed.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        EventBus.Publish(new TerritoryChangedEvent(kingdomId, claimed));
    }

    /// <summary>某王国全部建筑的中区块 Chebyshev 1-ring 并集（D343 3×3）。RebuildInitial 与 ClaimInitial 共用同源逻辑。</summary>
    private static HashSet<Vector2Int> CollectMidRing(int kingdomId)
    {
        var cells = new HashSet<Vector2Int>();
        if (BuildingRegistry.Instance == null || BuildingRegistry.Instance.All == null) return cells;
        if (GridSystem.Instance == null) return cells;
        foreach (var b in BuildingRegistry.Instance.All)
        {
            if (b == null || b.kingdomId != kingdomId) continue;   // 自然建筑 kingdomId==-1 不计
            Vector2Int mid = GridSystem.Instance.CellToMidChunk(b.coord);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    cells.Add(new Vector2Int(mid.x + dx, mid.y + dy));
        }
        return cells;
    }
}