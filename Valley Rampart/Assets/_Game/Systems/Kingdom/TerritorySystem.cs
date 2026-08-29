using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 2_17 步骤6：领土账本（唯一真源 + 染色事件 + 推进）。
/// - 唯一真源（D342）：`Dictionary<中区块, kingdomId>` 是本系统独占真源；KingdomState.领土句柄仅只读视图，不缓存（防双源漂移）。
/// - 初始圈入（D343）：初始建筑外扩 1 中区块（Chebyshev 3×3）。
/// - 覆盖玩家(id=0)+AI 王国(1..N)；自然建筑 kingdomId==-1 不计入。
/// - 推进（步骤12 批B/批C′）：AI 推边界 ExpandTick（批B）；玩家/AI 建造纳脚下格 ClaimFootprintChunk（批C′）；吞并归 CampUpgrader。
/// - 存档（步骤12 批C ④债）：ISaveable Global 段（SaveId="TerritorySystem"，独立勿夹带 kingdoms[] 2_11 债）。
///   门控三路：读档 LoadState 恢复 / 新游戏 EnterPlaying RebuildInitial / 旧档无段兜底 RebuildInitial。
/// </summary>
public class TerritorySystem : Singleton<TerritorySystem>, ISaveable
{
    /// <summary>存档段 id（Global 段独立；勿并入 kingdoms[]——HH.32 补裁2）。</summary>
    public string SaveId => "TerritorySystem";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    private readonly Dictionary<Vector2Int, int> _territory = new Dictionary<Vector2Int, int>();
    /// <summary>跨存档-进入门控：LoadState 已恢复账本 → 标记，EnterPlaying 不再 RebuildInitial 覆盖。</summary>
    private bool _loadedFromSave;
    // ⑩推边界冷却（批B：运行时态 + 批C 入档持久化）
    private readonly Dictionary<int, int> _lastExpandDay = new Dictionary<int, int>();

    protected override void Awake()
    {
        base.Awake();
        if (SaveManager.Instance != null)
            SaveManager.Instance.RegisterSaveable(this);
    }

    protected override void OnDestroy()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.UnregisterSaveable(this);
        base.OnDestroy();
    }

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

    // ===== 步骤12 批B：⑩推边界（AI 领土推进，D326/D327；HH.32 裁2 欲望与容量分离）=====

    /// <summary>非初始占区数（D343 初始圈之外、由推边界新增的领土中区块数；⑩ TerritoryGap 欲望分母）。</summary>
    public int NonInitialTerritoryCount(int kingdomId)
    {
        var initial = CollectMidRing(kingdomId);
        int n = 0;
        foreach (var c in GetKingdomTerritory(kingdomId))
            if (initial == null || initial.Count == 0 || !initial.Contains(c)) n++;
        return n;
    }

    /// <summary>
    /// ⑩ AI 领土日 tick（D326 确定性 + D327 额度硬容量门）。DayCycleSettlement 步骤3 接线。
    /// 遍历 AI 王国 id 升序（D326 同日多国按 kingdomId 升序）；每王国：冷却/额度硬门 → 邻接无主可走候选 → 推 1~2 块 → 写账本 → 广播。
    /// 只纳无主 + 4-邻接（D283 防飞地）；玩家(id=0)无脑 D338 不参与。
    /// </summary>
    public void ExpandTick()
    {
        if (GridSystem.Instance == null || KingdomRegistry.Instance == null) return;
        var reg = KingdomRegistry.Instance;
        var bcfg = KingdomBrain.LoadConfig();
        int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;

        var ids = new List<int>();
        foreach (var k in reg.GetAll())
            if (k != null && !k.IsPlayer) ids.Add(k.id);
        ids.Sort();   // D326 确定性升序

        int cd = Mathf.Max(1, bcfg.expandCooldownDays);
        int capBase = bcfg.expandCapacityBase;
        int capMax = Mathf.Max(0, bcfg.expandCapacityMax);
        int perMin = Mathf.Max(1, bcfg.expandPerDayMin);
        int perMax = Mathf.Max(perMin, bcfg.expandPerDayMax);

        for (int i = 0; i < ids.Count; i++)
        {
            int id = ids[i];
            var k = reg.Get(id);
            if (k == null) continue;

            // D327 硬容量门：capacity = clamp(β + 工人 − 非初始占区, 0, 上限)；≤0 不再推进
            int nonInit = NonInitialTerritoryCount(id);
            int capacity = Mathf.Clamp(capBase + k.workerCount - nonInit, 0, capMax);
            if (capacity <= 0) continue;

            // 冷却：距上次推进 ≥ 冷却日
            if (_lastExpandDay.TryGetValue(id, out int last) && day - last < cd) continue;

            // 邻接无主可走候选（4 邻接，D326）
            var gains = CandidateExpandCells(id);
            if (gains.Count == 0) continue;

            // 单日推 1~2 块（不超额度剩余）
            int take = Mathf.Clamp(Mathf.Min(gains.Count, capacity), perMin, perMax);
            var claimed = new List<Vector2Int>(take);
            for (int j = 0; j < take; j++)
            {
                _territory[gains[j]] = id;
                claimed.Add(gains[j]);
            }
            _lastExpandDay[id] = day;
            EventBus.Publish(new TerritoryChangedEvent(id, claimed));
        }
    }

    /// <summary>某王国当前领土 4 邻接的无主可走中区块候选（D326 坐标序排序；只纳无主）。</summary>
    private List<Vector2Int> CandidateExpandCells(int id)
    {
        var ownedList = GetKingdomTerritory(id);   // 已按坐标序
        var owned = new List<Vector2Int>(ownedList);
        var ownedSet = new HashSet<Vector2Int>(owned);
        var cand = new HashSet<Vector2Int>();
        for (int i = 0; i < owned.Count; i++)
        {
            var c = owned[i];
            TryAddNeighbor(cand, ownedSet, c.x + 1, c.y);
            TryAddNeighbor(cand, ownedSet, c.x - 1, c.y);
            TryAddNeighbor(cand, ownedSet, c.x, c.y + 1);
            TryAddNeighbor(cand, ownedSet, c.x, c.y - 1);
        }
        var list = new List<Vector2Int>(cand);
        list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return list;
    }

    private void TryAddNeighbor(HashSet<Vector2Int> cand, HashSet<Vector2Int> owned, int x, int y)
    {
        var c = new Vector2Int(x, y);
        if (owned.Contains(c) || _territory.ContainsKey(c)) return;   // 已有主/已含 → 不候选（只纳无主）
        if (MidChunkWalkable(c)) cand.Add(c);                          // 域推进门槛（可走率 ≥ SO 阈值）
    }

    /// <summary>中区块内可走格占比 ≥ SO 阈值（目标域推进门槛；D326 只推进可走域）。</summary>
    private bool MidChunkWalkable(Vector2Int mid)
    {
        var grid = GridSystem.Instance;
        int ms = grid != null && grid.Config != null && grid.Config.midChunkSize > 0 ? grid.Config.midChunkSize : 4;
        var bcfg = KingdomBrain.LoadConfig();
        float thr = bcfg.expandWalkableRatioMin;
        int total = ms * ms, walk = 0;
        for (int dx = 0; dx < ms; dx++)
            for (int dy = 0; dy < ms; dy++)
                if (grid.IsWalkable(new GridCoord(mid.x * ms + dx, mid.y * ms + dy, 0))) walk++;
        return total > 0 && (float)walk / total >= thr;
    }

    // ===== 步骤12 批C′ ④债：建造纳脚下格 + 存档入档 =====

    /// <summary>
    /// 玩家/AI 建造纳土（D327 + HH.32 裁4，批C′）：建筑建成 → 建筑**脚下中区块本身**纳入该王国
    ///（2_17 设计 L165/L282 字面"纳该中区块"；批C′ 裁决：非 4-邻接、脚下反而不纳的漂移）。
    /// 无主→纳入+广播；有主（含他国 id=0）→**静默零变更**（裁4：他国领地不吞并、无领土变更）。
    /// AI 也纳脚下格：事实占有不分阵营，1 格/栋不构成绕容量门（裁2 额度硬门只在 ExpandTick 的 D327 容量门）。
    /// 升级/重建由调用方 `_territoryClaimed` 门控，不重复纳入。
    /// </summary>
    public void ClaimFootprintChunk(int kingdomId, GridCoord coord)
    {
        var grid = GridSystem.Instance;
        if (grid == null) return;
        Vector2Int mid = grid.CellToMidChunk(coord);
        // 脚下格本身纳土：有主（含他国 id=0）→ 静默零变更（裁4），不吞并不扩张
        if (_territory.ContainsKey(mid)) return;
        _territory[mid] = kingdomId;
        EventBus.Publish(new TerritoryChangedEvent(kingdomId, new List<Vector2Int> { mid }));
    }

    // ===== 批次C ④债：存档入档（ISaveable Global 段，独立 SaveId="TerritorySystem"）=====

    public SavePayload SaveState()
    {
        var cells = new List<TerritoryCellSave>(_territory.Count);
        foreach (var kv in _territory)
            cells.Add(new TerritoryCellSave { x = kv.Key.x, y = kv.Key.y, owner = kv.Value });
        cells.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        var data = new TerritorySaveData
        {
            cells = cells,
            lastExpandDay = new List<DaySave>(_lastExpandDay.Select(kv => new DaySave { kingdomId = kv.Key, day = kv.Value }))
        };
        return new SavePayload
        {
            typeName = typeof(TerritorySaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(TerritorySaveData).AssemblyQualifiedName) return;
        var data = JsonUtility.FromJson<TerritorySaveData>(payload.json);
        if (data?.cells == null) return;
        _territory.Clear();
        foreach (var c in data.cells)
            _territory[new Vector2Int(c.x, c.y)] = c.owner;
        _lastExpandDay.Clear();
        if (data.lastExpandDay != null)
            foreach (var d in data.lastExpandDay)
                _lastExpandDay[d.kingdomId] = d.day;
        _loadedFromSave = true;   // 门控：EnterPlaying 不再 RebuildInitial 覆盖存档领土
        Debug.Log($"[TerritorySystem] 从存档恢复领土 {_territory.Count} 块");
    }

    /// <summary>
    /// EnterPlaying 门控（④债三路，LoadManager 调用）：
    /// - 读档已 LoadState 恢复 → 保留（不动，避免 RebuildInitial 用当前建筑重推覆盖演进结果）；
    /// - 新游戏 / 旧档无段（_loadedFromSave==false）→ RebuildInitial 重推（D343 从初始建筑算初始圈）。
    /// </summary>
    public void EnterPlayingGate()
    {
        if (_loadedFromSave) { _loadedFromSave = false; return; }   // 读档已恢复，恢复后清零标记（下次新游戏走重推）
        RebuildInitial();
    }
}

/// <summary>领土单中区块存档数据（坐标序排序保确定性）。</summary>
[Serializable]
public class TerritoryCellSave
{
    public int x, y, owner;
}

/// <summary>⑩推边界冷却存档（跨读档保持冷却语义）。</summary>
[Serializable]
public class DaySave
{
    public int kingdomId, day;
}

/// <summary>TerritorySystem 存档载荷（账本 + 冷却）。</summary>
[Serializable]
public class TerritorySaveData
{
    public List<TerritoryCellSave> cells;
    public List<DaySave> lastExpandDay;
}