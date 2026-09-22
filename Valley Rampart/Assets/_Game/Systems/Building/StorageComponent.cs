using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 存储组件 —— ⭐ **多资源容器**（`09_资源与仓库.md` §五 ＋ `09#36` · `M1-A`）。
///
/// 结构（§5.1）：**仓库 ＝ 标签（收什么）＋ 字段（容量 · 等级）**。
///   · **标签**＝**路径前缀声明**（§三 · 同一个机制三种写法：通用仓 `res`／分类仓 `res_material`／专属仓 `res_material_ore`），
///     数据源＝`BuildingDef.warehousePaths`（⛔ **不再由 `def.outputResource` 决定容器类型**）；
///   · **一条容量线**（§5.2 硬规则 1）：装什么都占它，**占用 ＝ Σ(该资源数量 × 该资源体积)**（硬规则 2）。
///   ⛔ **不做单一资源仓**（硬规则 3）—— 前缀匹配上的资源都能进同一个仓。
///
/// 实现 `IHarvestable`（玩家手动收取，资源转入国库 · `M1-G` 改两段式）＋ `IWarehouse`（`09` §七 CRUD）。
/// 存档：`BuildingSaveData.storageContents`（条目列表 · 旧档可作废 `D788`）。
/// </summary>
public class StorageComponent : MonoBehaviour, IBuildingComponent, IHarvestable, IWarehouse
{
    /// <summary>
    /// 收什么（**仓库声明路径** · §三）：可多条；空 ⇒ 通用仓 `res`（全部资源）。
    /// ⭐ 加一个仓／改它收什么 ＝ **只改这一行数据**（`09` §十一 判据 1）。
    /// </summary>
    [Tooltip("仓库声明路径（可多条；空＝通用仓 res）。例：res_material＝全部材料；res_material_ore＝只收矿石")]
    public string[] warehousePaths;

    /// <summary>**一条容量线**（§5.2 硬规则 1）：装什么都占它。</summary>
    public int capacity = 100;

    /// <summary>
    /// ⭐ `M1-D` 件4（`09` §九 `:350`／§9.7 · `D803` 裁）：「**能否掉落**」标签（**第二维** · 与「收什么」正交）——
    /// 本仓内容在宿主生命周期结束（`Building.Die`）时是否转为掉落箱。
    /// · **默认 `true` ＝ 默认可掉**（`D803`）；须拦的仓（人口仓/水仓等）由数据行显式置 `false`；
    /// · ⛔ 与 `Accepts`（前缀匹配）**零耦合** —— 只被「掉箱抽取器」（`Building.DropStorageToChest`）读取；
    /// · ⛔ **不入档**（随资产/创建点重建恢复 ⇒ 无存档形状变化）。
    /// </summary>
    public bool droppable = true;

    /// <summary>存量（资源 → 量）。⭐ 多资源并存（§5.2 硬规则 3）。</summary>
    private readonly Dictionary<ResourceType, int> _items = new Dictionary<ResourceType, int>();

    /// <summary>
    /// 存储变化事件（QQQ.2 §需求7 / DR-15：WarehousePanel 订阅实时刷新）。关闭退订避免泄漏。
    ///
    /// ⭐ **契约语义（`M1-B` 件1 固化 · `09` §7.5-⑦）**：
    ///   · **何时发** —— 只在**真实发生量变**的写口发：<see cref="Add"/>／<see cref="TakeOut"/>／
    ///     <see cref="Clear"/>／<see cref="TrimToCapacity"/>／<see cref="Harvest"/>（量 &gt; 0）／
    ///     <see cref="RestoreContents"/>。**空转/幂等路径不发**（`amount ≤ 0`／标签拒收／容量为 0／取到 0）。
    ///   · **发几次** —— **每次写口调用各发 1 次**；`Add`／`TakeOut` **部分成功也发**；
    ///     <see cref="RestoreContents"/> 逐条目经 `Add` ⇒ **每条目 1 次 ＋ 收尾 1 次（N＋1 次）**；
    ///     <see cref="Harvest"/> 整批 1 次。⛔ **无节流** —— 每秒生产／搬运会等频触发，
    ///     **节流由订阅方自理**（`09` §7.5-⑦「必须节流」）。
    ///   · **载荷** —— 只带**本仓引用**，⛔ **不带 diff**（改了哪个资源、改了多少都不知道）
    ///     ⇒ 订阅方**必须全量重读**（<see cref="Query"/>／<see cref="TotalCount"/>／
    ///     <see cref="UsedSpace"/>／<see cref="GetAmount"/>…），⛔ **不得增量累加**
    ///     （会与 <see cref="TrimToCapacity"/>／<see cref="RestoreContents"/> 的批量写口失同步）。
    ///   · **读值时效** —— 汇总量为**现算**（`M1-B` 件1 决定：**不加缓存**）⇒ 回调内读到的**即最新值**，
    ///     ⛔ 无需等下一帧。（⚠️ 若将来加缓存：<see cref="FreeSpace"/> 与 <see cref="UsedSpace"/>
    ///     为**同算式双出口**，**必须同走缓存**，否则两条读数会分叉。）
    /// </summary>
    public event System.Action<StorageComponent> OnStorageChanged;

    // ===== 生命周期 =====

    public void Init(Building building)
    {
        if (building == null || building.def == null) return;
        // ⭐ 收什么＝数据行（`09` §三／判据 1）；⛔ 本片起不再由 def.outputResource 决定容器类型。
        SetDeclaredPaths(building.def.warehousePaths);
        droppable = building.def.droppable;   // ⭐ `M1-D` 件4：能否掉落＝数据行（第二维 · 默认 true）
        RefreshCapacity();
        // 2_12 步骤8.4：加入王国仓库注册表（替代 WarehouseHelper 全场景扫描）
        WarehouseRegistry.Register(this);
    }

    void OnDestroy()
    {
        WarehouseRegistry.Unregister(this);
    }

    /// <summary>设定本仓声明（子仓/程序化仓专用：国库仓、矿洞副产仓、厂级弹药仓等）。</summary>
    public void SetDeclaredPaths(string[] paths)
    {
        warehousePaths = WarehousePaths.Normalize(paths);
    }

    /// <summary>归一后的声明路径（空 ⇒ 通用仓 {@link WarehousePaths.All}）。</summary>
    public string[] DeclaredPaths => WarehousePaths.Normalize(warehousePaths);

    /// <summary>本仓能否收该资源（标签＝路径 前缀匹配 · §三）。</summary>
    public bool Accepts(ResourceType type) => ResourceCatalog.Accepts(DeclaredPaths, type);

    /// <summary>
    /// 刷新存储容量：def.producer.capacity × 等级缩放（3.5.4 数据卡：仓库/粮仓 Lv2/Lv3 容量↑）。
    /// 建造/读档/升级后调用。
    /// </summary>
    public void RefreshCapacity()
    {
        var b = GetComponent<Building>();
        var def = b != null ? b.def : null;
        if (def == null) return;
        capacity = def.producer.capacity > 0
            ? Mathf.Max(1, Mathf.RoundToInt(def.producer.capacity * b.LevelScale()))
            : 100;
    }

    // ===== 查（§7.5）=====

    /// <summary>单项存量（§7.5-①）。</summary>
    public int GetAmount(ResourceType type) => _items.TryGetValue(type, out var v) ? v : 0;

    /// <summary>件数合计（§7.5-④「件数」口径）。</summary>
    public int TotalCount
    {
        get
        {
            int sum = 0;
            foreach (var kv in _items) sum += kv.Value;
            return sum;
        }
    }

    /// <summary>占用量（§5.2 硬规则 2：Σ(数量 × 体积) · §7.5-④「占用」口径）。</summary>
    public int UsedSpace
    {
        get
        {
            int sum = 0;
            foreach (var kv in _items) sum += kv.Value * ResourceCatalog.VolumeOf(kv.Key);
            return sum;
        }
    }

    /// <summary>剩余格数（容量线 − 占用）。</summary>
    public int FreeSpace => capacity - UsedSpace;

    /// <summary>仓是否已满（§5.2：占用达容量线 ⇒ 体积≥1 的资源都进不来）。</summary>
    public bool IsFull => UsedSpace >= capacity;

    /// <summary>该资源是否再也放不进（§7.5-⑤ `CanAccept` 的布尔面）。</summary>
    public bool IsFullFor(ResourceType type) => CanAccept(type) <= 0;

    /// <summary>
    /// 还能装几个该资源（§7.5-⑤）：`floor(剩余容量 ÷ 体积)`；**体积 0 ⇒ 不占容量（无上限）**。
    /// ⛔ 返回"还能装几个"，不是"剩几格"。
    /// </summary>
    public int CanAccept(ResourceType type)
    {
        if (!Accepts(type)) return 0;
        int vol = ResourceCatalog.VolumeOf(type);
        if (vol <= 0) return int.MaxValue;          // 体积 0 ＝ 不占容量（§4.2 金币用例）
        int free = capacity - UsedSpace;
        return free <= 0 ? 0 : free / vol;
    }

    /// <summary>
    /// 按**前缀子树**汇总（§7.5-② · 判据 4）：给 `res_material` ⇒ 一次拿到该子树全部资源合计。
    /// </summary>
    public int SumByPrefix(string pathPrefix)
    {
        if (string.IsNullOrEmpty(pathPrefix)) return 0;
        int sum = 0;
        foreach (var kv in _items)
        {
            var paths = ResourceCatalog.PathsOf(kv.Key);
            for (int i = 0; i < paths.Length; i++)
                if (ResourceCatalog.PathMatches(pathPrefix, paths[i])) { sum += kv.Value; break; }
        }
        return sum;
    }

    /// <summary>该仓全部存量条目（§7.1 查 · 按资源表序＝**确定性顺序**）。</summary>
    public List<ResourceAmount> Query()
    {
        var list = new List<ResourceAmount>(_items.Count);
        foreach (var type in ResourceCatalog.AllTypes)
            if (_items.TryGetValue(type, out var v) && v > 0) list.Add(new ResourceAmount(type, v));
        return list;
    }

    /// <summary>存量快照（存档用 · 确定性顺序 · 空仓 ⇒ 空列表非 null）。</summary>
    public ResourceList Contents
    {
        get
        {
            var list = ResourceList.Empty;
            foreach (var type in ResourceCatalog.AllTypes)
                if (_items.TryGetValue(type, out var v) && v > 0) list = list.Set(type, v);
            return list;
        }
    }

    // ===== 增（入仓 · §7.1）=====

    /// <summary>
    /// 入仓（§7.1）：**落到满为止（部分成功）**，返回实际入仓量。
    /// 步骤：① 检查标签（前缀匹配）② 检查容量 ③ 放入。⛔ 不整笔拒绝。
    /// </summary>
    public int Add(ResourceType type, int amount)
    {
        if (amount <= 0) return 0;
        if (!Accepts(type)) return 0;                       // ① 标签不匹配 ⇒ 拒收（§7.1 步 1）
        int can = CanAccept(type);                          // ② 容量按体积算
        if (can <= 0) return 0;
        int added = Mathf.Min(amount, can);
        _items[type] = GetAmount(type) + added;
        OnStorageChanged?.Invoke(this);
        return added;
    }

    /// <summary>`IWarehouse.Deposit`：同 <see cref="Add"/>（§7.1），返回实际入仓量。</summary>
    public int Deposit(ResourceType type, int amt) => Add(type, amt);

    // ===== 删（出仓 · §7.2）=====

    /// <summary>先问后扣的第一问（§7.2）。</summary>
    public bool CanTake(ResourceType type, int amt) => amt <= 0 || GetAmount(type) >= amt;

    /// <summary>
    /// 取走（≤ `amount`），返回实际取走量；内部触发 OnStorageChanged 通知 UI 刷新。
    /// </summary>
    public int TakeOut(ResourceType type, int amount)
    {
        if (amount <= 0) return 0;
        int have = GetAmount(type);
        int taken = Mathf.Min(amount, have);
        if (taken <= 0) return 0;
        if (taken >= have) _items.Remove(type);
        else _items[type] = have - taken;
        OnStorageChanged?.Invoke(this);
        return taken;
    }

    /// <summary>删（§7.2 尽力档）：`amount ≤ 0` ⇒ 直接返回 0。</summary>
    public int Take(ResourceType type, int amt) => TakeOut(type, amt);

    // ===== 改（加工 · §7.3）=====

    private static BlacksmithDef _blacksmithDefCache;

    /// <summary>
    /// 就地加工增值。**本片（`M1-A`）行为不动** —— 仍只支持 `Ore → Metal`（铁匠铺 D200/D609），
    /// 其余 in/out 组合返回 0；⭐ **配方已数据化为 <see cref="RecipeCatalog"/>（`09#46`）**，
    /// 「`Transform` 改走配方表」归 **`M6-B`**（本片只建表，⛔ 不改行为）。
    /// 流程：校验输入矿石足够（国库 Ore 真源）→ 扣矿 → 加 Metal 入本仓（容量内整批）。
    /// </summary>
    public int Transform(ResourceType @in, ResourceType @out, int amt)
    {
        if (@in != ResourceType.Ore || @out != ResourceType.Metal) return 0;   // 仅铁匠铺 Metal 加工（D609：矿石→Metal）
        if (amt <= 0) return 0;
        int room = CanAccept(ResourceType.Metal);
        if (room <= 0) return 0;

        if (_blacksmithDefCache == null)
            _blacksmithDefCache = Resources.Load<BlacksmithDef>("Config/BlacksmithDef");
        int ratio = _blacksmithDefCache != null && _blacksmithDefCache.oreToMetalRatio > 0
            ? _blacksmithDefCache.oreToMetalRatio
            : 2;   // 兜底占位 2:1

        int metal = Mathf.Min(Mathf.Max(1, amt / ratio), room);
        int oreNeeded = metal * ratio;
        var ruler = RulerController.Instance;
        if (ruler == null || ruler.Ore < oreNeeded) return 0;   // 矿石不足 → 整批不产（累计器保留，等矿攒够）

        ruler.ModifyResource(ResourceType.Ore, false, oreNeeded);
        return Add(ResourceType.Metal, metal);
    }

    // ===== 收取（`IHarvestable` · ⭐ `M1-G-1b`/`1c` 后现状）=====
    // ⭐ `U-15` 止血（`D809`/`D810`）＋ ⭐ `M1-G` 收口：本组的**去向是写死的（玩家国库 id=0）** ⇒
    //   判据层与落点层**必须双向过滤可收性**；⚠️ 「源仓直通国库」的另一端口已随 `#40` **删除**
    //   ⇒ **在役落点仅 `Harvest()`**（玩家手动收取路径）。

    /// <summary>是否有「**可入国库**」的内容（`IHarvestable`）。
    /// ⭐ `M1-G-1c` 件C-10（`D827`）：**消费者清单已重写**（旧列 `ScheduleCenterStub:100/:118` 施已随
    ///   `U-15` 根除项删除；旧列 `BuildingPanel:187/:405` **已不再调用** —— 该面板判据已改 `TotalCount &gt; 0`）。
    /// ⇒ ⭐ **现役消费者 ＝ 仅 Editor 探针**（`Valley_HH319_U15Probe` 等止血取证档）；
    ///   ⚠️ 生产面**不再据此灰化按钮**（「水井仓可点 ⇒ 派搬运」）。
    /// ⚠️ 判据用**玩家国库**（id=0）—— 与 `Harvest()` 落点一致；⛔ **非按国逻辑**。
    /// ⚠️ ⛔ 不得用 `TotalCount &gt; 0` 做快路径（水井仓有水 ⇒ 件数 &gt; 0 但国库不收 ⇒ 必须逐资源问）。</summary>
    public bool IsReadyToHarvest()
    {
        foreach (var type in ResourceCatalog.AllTypes)
            if (GetAmount(type) > 0 && CanRulerAccept(type)) return true;
        return false;
    }

    /// <summary>收取**可入国库**的内容（玩家手动收取路径 · `BuildingPanel` 收按钮）。
    /// ⭐ `U-15` 止血：国库不接受的资源**留仓**（⛔ 不计入 total · ⛔ 不清除）；可收的**只取实际入库量**。
    /// ⚠️ 逐条直接扣减（不经 `TakeOut`）⇒ 保持「`Harvest` 整批 1 次 `OnStorageChanged`」的既有契约（`M1-B` 件1）。</summary>
    public int Harvest()
    {
        int total = 0;
        foreach (var type in ResourceCatalog.AllTypes)
        {
            int amount = GetAmount(type);
            if (amount <= 0) continue;
            if (!CanRulerAccept(type)) continue;          // ⭐ ① 标签不收 ⇒ 留仓（防转箱空转）
            int can = TreasuryCanAccept(type);            // ⭐ ② 容量上限 ⇒ 不制造 overflow（防 SpillToChest 装箱）
            int move = Mathf.Min(amount, can);
            if (move <= 0) continue;
            total += move;
            RulerController.Instance?.ModifyResource(type, true, move);
            if (move >= amount) _items.Remove(type);      // ⚠️ 只取走**实际入库**的那部分（⛔ 不得整型清）
            else _items[type] = amount - move;
        }
        if (total > 0) OnStorageChanged?.Invoke(this);
        return total;
    }

    /// <summary>⭐ `U-15`：国库能否收该资源（**标签面** · 无容量语义 · 转发 <see cref="TreasureVault"/>）。
    /// ⚠️ 写死**玩家国库**（id=0）—— 与在役落点 `Harvest()` 一致（⛔ 非按国逻辑）。</summary>
    private static bool CanRulerAccept(ResourceType type)
    {
        var tv = TreasureVault.Get(0);
        return tv != null && tv.Accepts(type);
    }

    /// <summary>⭐ `U-15`：国库还能收几个该资源（**容量面** · 体积 0 ⇒ `int.MaxValue`）。</summary>
    private static int TreasuryCanAccept(ResourceType type)
    {
        var tv = TreasureVault.Get(0);
        return tv != null ? tv.CanAccept(type) : 0;
    }

    // ===== 搬运携带量（3.5.3 §3.1 / 3.5 前置缺口 §2.2；P1-8）=====

    private static ResourceCarryConfig _carryConfig;

    // ⭐⭐ `M1-G-1` `#40`（`D824` §一-4／件 3）：**原无参 `GetCarryAmount()` 已删** ——
    //   其唯一调用面（`ScheduleCenterStub` 链 B）随 `U-15` 根除项一并删除 ⇒ 本无参版**零调用**。
    //   ⚠️ 有参版 `GetCarryAmount(ResourceType)` 保留（生产在用）。

    /// <summary>按类型取携带量（`ResourceCarryConfig` SO 数据驱动）。</summary>
    public int GetCarryAmount(ResourceType type)
    {
        if (_carryConfig == null)
            _carryConfig = Resources.Load<ResourceCarryConfig>("Config/ResourceCarryConfig");
        return _carryConfig != null ? _carryConfig.GetCarryAmount(type) : 10;
    }

    /// <summary>本仓首个非空资源（资源表序 ⇒ 确定性；空仓 ⇒ 默认 Gold 占位）。
    /// ⏭️ 多资源仓下的**过渡读口**（搬运广告等单资源假设点的落点，归 `M1-G` 收口）。
    /// ⚠️ `M1-G-1c` 件C-4（`D827`）：原注「调用方 `HarvestCarry` 会先问国库能否收」**已作废** —— 该调用方**已随 `#40` 删除**；
    ///   现役同族守卫在 `Harvest()`（逐资源问 `CanRulerAccept`／`TreasuryCanAccept`）与链 A 落点（见 `StorageComponent` 墓碑注）。</summary>
    public ResourceType PrimaryStoredType()
    {
        foreach (var type in ResourceCatalog.AllTypes)
            if (GetAmount(type) > 0) return type;
        return ResourceType.Gold;
    }

    // ⭐⭐ `M1-G-1` `#40`（`09` §八「转移是分形的」· `D824` §一-4／件 3）：**原 `HarvestCarry()` 已删** ——
    //   它是「源仓 **直通国库·跳过背包**」的旁路；两段式（`TaskScheduler.LoadInventoryFromSource`
    //   源仓→背包 ＋ `UnloadInventory` 背包→仓）由同一任务驱动，**已在役** ⇒ 本项＝**删旁路**。
    //   ⭐ 两道守卫语义的**迁移落点**（⛔ 不得无声消失）：
    //     ① `CanRulerAccept`（**标签面**·国库收不收）⇒ 迁至**新落点判据** `WarehouseRegistry.FindNearestAvailable`
    //        的 `Accepts(type)`（`UnloadInventory` 内）＋ `Harvest()`（玩家手动收取）自留；
    //     ② `TreasuryCanAccept`（**容量面**）⇒ 迁至 `FindNearestAvailable` 的 `CanAccept(type) <= 0` 判据
    //        ＋ `Harvest()` 自留 —— 即「**先问后拿**」（`L-60`）在同一处承载。
    //   ⚠️ 且本批**同时删掉**「满则入国库兜底」（`UnloadInventory`）⇒ ⛔ 不再存在"绕过可收性写死国库"的支路。

    // ===== 存档（仓内容 ＋ 容量线 · 判据 6）=====

    /// <summary>读档恢复仓内容（clamp 到容量线内，防旧档/异常值越界）。</summary>
    public void RestoreContents(ResourceList contents)
    {
        _items.Clear();
        if (contents.items != null)
        {
            for (int i = 0; i < contents.items.Length; i++)
            {
                var e = contents.items[i];
                if (e.amount <= 0) continue;
                int added = Add(e.type, e.amount);
                if (added < e.amount)
                    Debug.LogWarning($"[StorageComponent] 读档 {e.type} 超容量 clamp {e.amount}→{added}");
            }
        }
        OnStorageChanged?.Invoke(this);
    }

    /// <summary>清空（重置/测试用）。</summary>
    public void Clear()
    {
        _items.Clear();
        OnStorageChanged?.Invoke(this);
    }

    /// <summary>
    /// 压回容量线（升级/容量变更后调用；超出部分按**资源表序**丢弃 —— 原单资源实现是 `min(存量, 容量)`，
    /// 多资源下等价语义＝占用压到容量内）。⭐ 容量只随等级上升 ⇒ 常规路径下为空操作。
    /// </summary>
    public void TrimToCapacity()
    {
        if (UsedSpace <= capacity) return;
        foreach (var type in ResourceCatalog.AllTypes)
        {
            if (UsedSpace <= capacity) break;
            int overflow = UsedSpace - capacity;
            int vol = Mathf.Max(1, ResourceCatalog.VolumeOf(type));
            int needDrop = Mathf.CeilToInt(overflow / (float)vol);
            TakeOut(type, needDrop);
        }
        OnStorageChanged?.Invoke(this);
    }
}
