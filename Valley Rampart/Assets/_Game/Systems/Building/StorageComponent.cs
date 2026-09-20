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

    /// <summary>存量（资源 → 量）。⭐ 多资源并存（§5.2 硬规则 3）。</summary>
    private readonly Dictionary<ResourceType, int> _items = new Dictionary<ResourceType, int>();

    /// <summary>存储变化事件（QQQ.2 §需求7 / DR-15：WarehousePanel 订阅实时刷新）。关闭退订避免泄漏。</summary>
    public event System.Action<StorageComponent> OnStorageChanged;

    // ===== 生命周期 =====

    public void Init(Building building)
    {
        if (building == null || building.def == null) return;
        // ⭐ 收什么＝数据行（`09` §三／判据 1）；⛔ 本片起不再由 def.outputResource 决定容器类型。
        SetDeclaredPaths(building.def.warehousePaths);
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

    // ===== 收取（IHarvestable · `M1-G` 改两段式搬运，本片保持原路径）=====

    /// <summary>是否有可取内容（`IHarvestable`）。</summary>
    public bool IsReadyToHarvest() => TotalCount > 0;

    /// <summary>全量收取转入国库（玩家手动收取路径 · `BuildingPanel` 收按钮）。</summary>
    public int Harvest()
    {
        int total = 0;
        foreach (var type in ResourceCatalog.AllTypes)
        {
            int amount = GetAmount(type);
            if (amount <= 0) continue;
            total += amount;
            RulerController.Instance?.ModifyResource(type, true, amount);
        }
        _items.Clear();
        if (total > 0) OnStorageChanged?.Invoke(this);
        return total;
    }

    // ===== 搬运携带量（3.5.3 §3.1 / 3.5 前置缺口 §2.2；P1-8）=====

    private static ResourceCarryConfig _carryConfig;

    /// <summary>
    /// ⏭️ **过渡实现**（多资源仓下的单值查询）：取本仓**首个非空资源**（按资源表序＝确定性）的携带量。
    /// ⚠️ 本方法与 <see cref="HarvestCarry"/> 同为 `09#40`「删 `HarvestCarry` 直通国库」的**待删对象**（归 `M1-G`）；
    /// 本片（`M1-A`）**只改结构不动行为** ⇒ 保留签名以免牵动 `TaskScheduler:707`／`ScheduleCenterStub:130`。
    /// </summary>
    public int GetCarryAmount() => GetCarryAmount(PrimaryStoredType());

    /// <summary>按类型取携带量（`ResourceCarryConfig` SO 数据驱动）。</summary>
    public int GetCarryAmount(ResourceType type)
    {
        if (_carryConfig == null)
            _carryConfig = Resources.Load<ResourceCarryConfig>("Config/ResourceCarryConfig");
        return _carryConfig != null ? _carryConfig.GetCarryAmount(type) : 10;
    }

    /// <summary>本仓首个非空资源（资源表序 ⇒ 确定性；空仓 ⇒ 默认 Gold 占位）。
    /// ⏭️ 多资源仓下的**过渡读口**（搬运广告等单资源假设点的落点，归 `M1-G` 收口）。</summary>
    public ResourceType PrimaryStoredType()
    {
        foreach (var type in ResourceCatalog.AllTypes)
            if (GetAmount(type) > 0) return type;
        return ResourceType.Gold;
    }

    /// <summary>
    /// ⏭️ **过渡实现**（`M1-G` 删）：搬一次（≤携带量）入国库，返回实际搬走量；剩余留待下轮。
    /// 多资源仓下取**首个非空资源**（资源表序 ⇒ 确定性）；行为与旧单资源仓逐个搬运等价。
    /// </summary>
    public int HarvestCarry()
    {
        var type = PrimaryStoredType();
        int amount = Mathf.Min(GetAmount(type), Mathf.Max(1, GetCarryAmount(type)));
        if (amount <= 0) return 0;
        int taken = TakeOut(type, amount);
        if (taken > 0) RulerController.Instance?.ModifyResource(type, true, taken);
        return taken;
    }

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
