using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 箱子管理器（2_12 步骤7C + 步骤11 / D269 统一资源容器 D142 的唯一归属者）。
/// 负责：生成（由触发源调用）、拾取接口、命中重置、过期扫描、单格数量上限（D222）。
/// 生成触发源归属：工人背包满（D221）→ 8 调度层；怪物掉落（D213）→ 2_14；仓库溢出（D222）→ 步骤11。
/// 搬运/拾取入背包 → 8 调度层；渲染 → 2_10。
/// 存档（DZ-074 / HH.109 件1）：ISaveable Scene 阶段——箱子=场景动态对象（参照 Building/单位先例）；
/// LoadState 先 ClearAll 后重建（幂等，M2：兼防 DontDestroyOnLoad 跨局残留；读档链不重跑地图生成
/// 故无「地图生成宝箱双份」风险——⭐ SpawnChest 全工程 **5 处**运行时调用方：
/// DamageSystem:633（战利品箱 · M1-D 重写）／TreasureVault:122（国库溢出）／MonsterController:67（⚠️ M1-D 已退役）／
/// Building:916（拆除退还掉箱）／Building:1047（工地仓掉箱）。
/// ⚠️ **防漂移注**：新增/删除调用方须同步本注（本注已因 M1-C 新增两处而失真过一次 ⇒ `M1-D` 件10 勘正）。
///
/// ⭐ `HH.316` · `U-2`（`D798`）：
///   · **件3 注册/注销挂钩**：落箱即注册为 `TaskScheduler` 任务源（箱实体自身实现 `ITaskSource` ·
///     一箱一源）／`Remove`/`ClearAll` 显式注销 —— ⛔ 关键清算**不放** `OnUnregister`（`Tick`①
///     清无效源不回调 · R5），形制照 `Building.RegisterSiteStore/UnregisterSiteStore`。
///   · **件1 内容物真源 ＝ 箱容器**（`ChestEntity.Store`）⇒ 本类 `contents` 读点全走箱转发读口（禁双写）。
///   · ⛔ **`Pickup` 已退役**（`D798` 裁 ①「链 B 并回链 A」）：`09` §9.8「掉落箱自身**不提供『捡』的能力**,
///     它就是个仓」⇒ 玩家手点 ＝ 立案搬运任务（`ChestEntity.Interact`），⛔ 无「取走内容 + 移除实体」接口。
/// </summary>
public class ChestManager : Singleton<ChestManager>, ISaveable
{
    /// <summary>实例是否已存在（Instance 判空，供外部安全访问）。</summary>
    public static bool HasInstance => Instance != null;

    private readonly List<ChestEntity> _chests = new List<ChestEntity>();

    /// <summary>存活箱子总数（供调试/上限判定）。</summary>
    public int Count => _chests.Count;

    // ===== ISaveable（DZ-074 / HH.109 件1）=====
    public string SaveId => "ChestManager";
    /// <summary>Scene 阶段（列报选型）：箱子为场景动态对象，GridSystem/TimeManager（Global）已先行恢复，
    /// CoordToWorld 与过期扫描锚点可用；参照 Building(L34)/单位 同阶段先例。</summary>
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Scene;

    private void Awake()
    {
        base.Awake();   // Singleton：自动创建 + DontDestroyOnLoad
        if (_instance != this) return;
        SaveManager.Instance.RegisterSaveable(this);   // KingdomManager L74 先例（重复注册由 RegisterSaveable 去重拦截）
    }

    private void Update()
    {
        // 过期扫描（D148）：bornDay + expireDays &lt; 当前天 → 移除
        if (_chests.Count == 0) return;
        int curDay = Mathf.RoundToInt(TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1);
        float expire = ChestConfig.Instance != null ? ChestConfig.Instance.expireDays : 3f;
        for (int i = _chests.Count - 1; i >= 0; i--)
        {
            var c = _chests[i];
            if (c == null) { _chests.RemoveAt(i); continue; }
            if (curDay - c.bornDay >= expire) Remove(c);
        }
    }

    /// <summary>
    /// 生成一个箱子落到格上（D245 统一容器）。所有来源（工人背包满/怪物掉落/仓库溢出）经此落箱。
    /// 单格数量上限（D222）：超 `chestMaxPerCell` 时**最早箱洒落邻格**（⭐ `M1-D` 件6：原「移除不洒落」退役 · ⛔ 资源不丢）。
    /// ⭐ `M1-D`/#57（`D802` `Q7`）：**去 `faction` 形参** —— `09` §9.8「箱**无主** · ⛔ 不计入任何国总量」⇒ 参数语义已废；
    /// `ownerFaction` 字段保留（⛔ 不动存档格式）⇒ 值恒 `Faction.None`（仅存档保真）。
    /// </summary>
    /// <param name="cell">落点格坐标（微格/楼层 0）。</param>
    /// <param name="pack">内容物（⭐ `M1-A`／`09#50`：资源量列表；D145 容量同工人携带量）。</param>
    /// <returns>创建成功的箱子；内容空/坐标非法返回 null。</returns>
    public ChestEntity SpawnChest(GridCoord cell, ResourceList pack)
    {
        if (pack.IsZero) return null;
        float born = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;

        // 单格上限：该格已满 ⇒ 最早箱洒落邻格（M1-D 件6 · 保底不丢 ＋ 告警）
        EnforceCellLimit(cell);

        var go = new GameObject("Chest");
        go.transform.position = WorldPosOf(cell);
        var chest = go.AddComponent<ChestEntity>();
        chest.Init(cell, pack, born);   // ⭐ 件1：内容物装载进**箱容器**（唯一真源）
        chest.ownerFaction = Faction.None;   // ⭐ `M1-D`/#57：恒 None（签名去参 · 字段保留供存档保真）
        _chests.Add(chest);
        RegisterSource(chest);          // ⭐ 件3：落箱即注册（每 tick 广告搬运任务 · 任何王国先到先得）
        return chest;
    }

    /// <summary>⭐ `HH.316` 件3：注册为任务源（落箱/读档重建处调 · ⛔ 形制照 `Building.RegisterSiteStore`）。</summary>
    private static void RegisterSource(ChestEntity chest)
    {
        if (chest == null || !TaskScheduler.HasInstance) return;
        TaskScheduler.Instance.Register(chest);
    }

    /// <summary>⭐ `HH.316` 件3：显式注销（`Remove`／`ClearAll` 调）—— 箱销毁即释放指向它的在派任务
    /// （`Unregister` 内 `OnBuildingDied` 兜住；工人背包不丢）。⛔ 不依赖 `OnUnregister` 清算（R5）。</summary>
    private static void UnregisterSource(ChestEntity chest)
    {
        if (chest == null || !TaskScheduler.HasInstance) return;
        TaskScheduler.Instance.Unregister(chest);
    }

    /// <summary>该格箱子数（供 spawn 前判定上限/调试）。</summary>
    public int CountAt(GridCoord cell)
    {
        int n = 0;
        for (int i = 0; i < _chests.Count; i++)
            if (_chests[i].cell.Equals(cell)) n++;
        return n;
    }

    /// <summary>
    /// 格域矩形内宝箱（HH.294 片 6-1：PickAt 候选③来源；buffer 复用**零分配**）。返回写入条数。
    /// 口径与 <see cref="GridSystem.FillUnitsInRect"/> 一致（RectInt xMax/yMax exclusive）。
    /// </summary>
    public int FillChestsInCellRect(RectInt cellRect, List<ChestEntity> buffer)
    {
        if (buffer == null) return 0;
        int before = buffer.Count;
        for (int i = 0; i < _chests.Count; i++)
        {
            var c = _chests[i];
            if (c == null) continue;
            if (cellRect.Contains(new Vector2Int(c.cell.x, c.cell.y))) buffer.Add(c);
        }
        return buffer.Count - before;
    }

    private void EnforceCellLimit(GridCoord cell)
    {
        int max = ChestConfig.Instance != null ? Mathf.Max(1, ChestConfig.Instance.chestMaxPerCell) : 4;
        int at = CountAt(cell);
        if (at < max) return;

        // 找该格最早的箱子（遍历取 bornDay 最小者）
        ChestEntity oldest = null;
        for (int i = 0; i < _chests.Count; i++)
        {
            var c = _chests[i];
            if (c == null || !c.cell.Equals(cell)) continue;
            if (oldest == null || c.bornDay < oldest.bornDay) oldest = c;
        }
        if (oldest == null) return;

        // ⭐ `M1-D` 件6（`D802` `Q6`）：驱逐**改洒落邻格**（⛔ 资源不丢 —— 对齐 `SpillToChest`/`AddGatherOverflow`/
        //   `UnloadInventory` 三处同口径）；⛔ 旧「Remove 不洒落」退役。
        var pack = oldest.contents;
        if (pack.IsZero)
        {
            Remove(oldest);   // 空箱：无可洒落 ⇒ 直接移除
            return;
        }
        var spill = FindSpillCell(cell);
        if (spill.HasValue)
        {
            Remove(oldest);
            SpawnChest(spill.Value, pack);   // 落邻格（`bornDay` 重置＝迁移续命；原语义＝销毁 ⇒ 单调改善）
            Debug.Log($"[ChestManager] 单格上限：最早箱洒落 ({cell.x},{cell.y})→({spill.Value.x},{spill.Value.y}) 内容{pack}（M1-D 件6 · ⛔ 不丢）");
            return;
        }
        // ⛔ 保底（⛔ 不静默）：邻域（环 1~4）无可落格 ⇒ **暂不驱逐**（内容留存于最早箱；下次落箱再试）
        Debug.LogWarning($"[ChestManager] ⚠️ 单格上限：({cell.x},{cell.y}) 邻域无可落格 ⇒ 暂不驱逐（内容留存于最早箱 · M1-D 件6 保底）");
    }

    /// <summary>⭐ `M1-D` 件6：渐扩环（半径 1~4）找「同格箱数 &lt; 上限」的邻格（确定性顺序 · 跳过本格 · 越界跳过）。</summary>
    private GridCoord? FindSpillCell(GridCoord origin)
    {
        int max = ChestConfig.Instance != null ? Mathf.Max(1, ChestConfig.Instance.chestMaxPerCell) : 4;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        for (int r = 1; r <= 4; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;   // 只扫第 r 环
                    int x = origin.x + dx, y = origin.y + dy;
                    if (map != null && (x < 0 || y < 0 || x >= map.width || y >= map.height)) continue;
                    var gc = new GridCoord(x, y);
                    if (CountAt(gc) < max) return gc;
                }
            }
        }
        return null;
    }

    /// <summary>格坐标 → 世界位置（对 Execure 等归一）。用楼阁层 0。</summary>
    private Vector3 WorldPosOf(GridCoord cell)
    {
        if (GridSystem.Instance != null) return GridSystem.Instance.CoordToWorld(cell);
        return new Vector3(cell.x, cell.y, 0f);
    }

    // ===== `Pickup`（D246 旧口径）已退役（⭐ `HH.316` · `D798` 裁 ①）=====
    //   原实现「取走内容 + 移除实体 + 返回资源包供调用方入背包」＝ `09` §9.8 :389 明拒的
    //   「在箱子上加『拾取』接口」⇒ 随链 B 并回链 A 删除；内容物出口只剩**工人搬运**（链 A 卸货段）。

    /// <summary>命中重置（D247：HP=1 一击碎，内容物原地重新落箱可再拾）。由 ChestEntity.Strike 调用。
    /// ⭐ `M1-D`/#57（`D802` `Q7`）：去 `faction` 参数 —— 新箱 `ownerFaction` 恒 `None`（⛔ 存档格式不动：
    /// 旧档 `ownerFaction` 值仅保真不复用；打碎＝`bornDay` 重置属 `D247` 既有行为 · `O-8` 观察项不动）。</summary>
    public void ResetDrop(ChestEntity chest)
    {
        if (chest == null) return;
        var pack = chest.contents;           // ⭐ 转发箱容器（唯一真源）
        var cell = chest.cell;
        Remove(chest);                       // 移除原实体（含任务源注销 · 件3）
        SpawnChest(cell, pack);              // 原地重落（内容保留）
    }

    /// <summary>移除箱子（过期/上限/破碎重置）。⭐ 件3：先注销任务源（在派任务随之释放），再销毁实体。</summary>
    public void Remove(ChestEntity chest)
    {
        if (chest == null) return;
        _chests.Remove(chest);
        UnregisterSource(chest);
        if (chest.gameObject != null) Destroy(chest.gameObject);
    }

    /// <summary>清空全部（开局/读档重建用）。⭐ 件3：逐箱注销任务源（⛔ 防跨局悬挂引用）。</summary>
    public void ClearAll()
    {
        for (int i = 0; i < _chests.Count; i++)
        {
            UnregisterSource(_chests[i]);
            if (_chests[i] != null && _chests[i].gameObject != null)
                Destroy(_chests[i].gameObject);
        }
        _chests.Clear();
    }

    // ===== ISaveable 实现（DZ-074 / HH.109 件1）=====

    public SavePayload SaveState()
    {
        var payload = new ChestSavePayload();
        for (int i = 0; i < _chests.Count; i++)
        {
            var c = _chests[i];
            if (c == null) continue;   // fake-null 死引用不入档（读档侧自愈）
            payload.chests.Add(new ChestSaveEntry
            {
                cellX = c.cell.x,
                cellY = c.cell.y,
                bornDay = c.bornDay,
                ownerFaction = (int)c.ownerFaction,   // ⭐ M1-D/#57：恒 None（字段保留仅存档保真 · ⛔ 格式不动）
                contents = c.contents
            });
        }
        return new SavePayload
        {
            typeName = typeof(ChestSavePayload).AssemblyQualifiedName,
            json = JsonUtility.ToJson(payload),
            version = payload.version
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(ChestSavePayload).AssemblyQualifiedName) return;
        var data = JsonUtility.FromJson<ChestSavePayload>(payload.json);
        if (data == null || data.chests == null) return;

        // 先清后建（M2 幂等）：防 DontDestroyOnLoad 跨局残留 + 读档重复重建双份；
        // 读档链不重跑地图生成（A 路径）故无地图宝箱双份风险（SpawnChest 调用点均运行时行为 · 清单见类头防漂移注）。
        ClearAll();

        for (int i = 0; i < data.chests.Count; i++)
        {
            var e = data.chests[i];
            var cell = new GridCoord(e.cellX, e.cellY);
            var pack = e.contents;
            if (pack.IsZero) continue;   // 空箱不重建（SpawnChest 同语义：内容空不落箱）

            // 内联重建（不走 SpawnChest：bornDay 用存档原值不过期重置；不做单格上限检查——存档态即合法态）
            var go = new GameObject("Chest");
            go.transform.position = WorldPosOf(cell);
            var chest = go.AddComponent<ChestEntity>();
            chest.Init(cell, pack, e.bornDay);   // ⭐ 件1：内容物装载进箱容器（存档形状不改 ⇒ 逐值往返）
            chest.ownerFaction = (Faction)e.ownerFaction;   // ⭐ M1-D/#57：按档原值恢复（仅存档保真 · ⛔ 格式不动）
            _chests.Add(chest);
            RegisterSource(chest);               // ⭐ 件3：读档重建同样入册（与落箱同形）
        }
        Debug.Log($"[ChestManager] 读档重建：{data.chests.Count} 个箱子（先清后建幂等）");
    }
}