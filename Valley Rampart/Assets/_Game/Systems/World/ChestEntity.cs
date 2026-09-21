using UnityEngine;

/// <summary>
/// 掉落箱子实体（2_12 步骤7C + 步骤11 / D269 统一资源容器 D142）。
/// 落地资源统一用箱子承载：生成→落格可拾、限期消失、命中破碎洒内容、任意阵营可拾。
/// 本类为**数据容器 + 交互面**，详见 ChestManager（唯一归属者）。渲染归 2_10（"treasure_box" sprite），
/// 移动/拾取动画归 2_3，搬运调度归 2_8（`HH.316` 起实现 `ITaskSource`），存档归 2_11（ISaveable）。
/// 继承 MonoBehaviour，不逃入 Building 建筑管线（无造价/无 FSM/不产出的轻量可拾物）。
///
/// ⭐ `HH.316` · `U-2`（`D798` 裁 A-1「箱＝真仓」＋ 裁 ④「一箱一源」）：
///   · **内容物唯一真源 ＝ 容器**（`StorageComponent` 挂**本体** · 声明通用 `res` · 容量＝**实际掉落量** ·
///     ⛔ 不 `Register` · ⛔ 不调 `Init`）⇒ <see cref="contents"/> 改**转发读口**（⛔ 无独立字段 ⇒ 禁双写）；
///   · 实现 `ITaskSource`（一箱一源）：广告 `Transport` 任务 ⇒ 工人（**任何王国** · 无主先到先得）取货搬回；
///   · `Interact`（玩家手点）＝ **立案一个搬运任务**（`09` §9.8 :390）⇒ ⛔ 不提供「捡」的能力／⛔ 无独立入账口。
///   · 「不可掉落」（§九 :358 护栏 ⇒ ⛔ 防"箱再掉箱"递归）：容器**不入注册表** ＋ 本类/`ChestManager`
///     **无任何「容器 → 新箱」转换路径**（消亡＝实体销毁 · ⛔ 不洒落 ⇒ 无递归面）。
/// </summary>
public class ChestEntity : MonoBehaviour, IInteractable, ITaskSource
{
    /// <summary>挂格坐标（楼层=l0 微格）。</summary>
    public GridCoord cell;

    /// <summary>生成的绝对天数，过期 = 生成天 + ChestConfig.expireDays（D148）。</summary>
    public float bornDay;

    /// <summary>⚠️ `M1-D`/#57（`D802` `Q7`）：**语义已废**（新箱恒 `None` · `SpawnChest` 已去 `faction` 参数）——
    /// 字段**保留仅存档保真**（`ChestSaveEntry.ownerFaction` 读写不动 · ⛔ 不改存档格式）。</summary>
    public Faction ownerFaction = Faction.None;

    /// <summary>HP=1 一击碎（D247），破碎后内容物返回地面可再拾。</summary>
    public int hp = 1;

    /// <summary>箱＝仓（`D798` 裁 A-1）：内容物容器（挂**本体** ⇒ `LoadInventoryFromSource` 的
    /// `comp.GetComponent&lt;StorageComponent&gt;()` 直接命中 ⇒ 装载段零改）。</summary>
    private StorageComponent _store;

    /// <summary>箱位置快照（广告/派发用 —— ⛔ 不每读探 `transform` ⇒ 实体销毁后仍可安全读）。</summary>
    private Vector2 _pos;

    private SpriteRenderer _renderer;
    private bool _initialized;

    /// <summary>
    /// 内容物读口（⭐ **转发容器** · ⛔ 无独立字段 ⇒ 存档/搬运/过期只有一处真源；
    /// 形制照 `TreasureVault.Contents`）。未初始化 ⇒ 空。
    /// </summary>
    public ResourceList contents => _store != null ? _store.Contents : ResourceList.Empty;

    /// <summary>内容物容器（箱＝仓 · 未初始化 ⇒ null）。供 `ChestManager`／探针读。</summary>
    public StorageComponent Store => _store;

    /// <summary>初始化（幂等，仅首次生效）。cell/contents/bornDay 由 ChestManager.SpawnChest 预先填。</summary>
    public void Init(GridCoord c, ResourceList pack, float day)
    {
        if (_initialized) return;
        cell = c;
        bornDay = day;
        _pos = transform.position;
        EnsureStore(pack);
        _initialized = true;
    }

    /// <summary>
    /// 建容器并装载内容物（`D798` 裁 A-1 四条红线）：
    /// ① 声明**通用仓** `res`（§9.8 :389「它**就是个仓**」）· ⛔ 不调 `StorageComponent.Init`
    ///    （依赖 `Building` 且会被 `def.warehousePaths` 覆盖本声明 · 照 `TreasureVault.cs:58-65` 先例）；
    /// ② ⛔ **不 `WarehouseRegistry.Register`**（防成为他人卸货落点 · 结构性隔离）；
    /// ③ **容量 ＝ 实际掉落量**（§九 :356「零配置 · 刚好装下」· Σ(量×体积) ⇒ ⛔ 不设模板常数）；
    /// ④ 装载走 `RestoreContents`（逐条目 `Add`）—— 唯一真源 ＝ 容器 ⇒ ⛔ 无 `contents` 字段双写。
    /// </summary>
    private void EnsureStore(ResourceList pack)
    {
        if (_store == null)
        {
            _store = gameObject.AddComponent<StorageComponent>();
            _store.SetDeclaredPaths(new[] { WarehousePaths.All });
        }
        _store.droppable = false;   // ⭐ `M1-D` 件4 护栏（`09` §九 :358「箱子的仓打『不可掉落』标签」⇒ ⛔ 防"箱再掉箱"递归）
        _store.capacity = SpaceOf(pack);
        _store.RestoreContents(pack);
    }

    /// <summary>容量＝实际掉落量（Σ 量×体积；体积 0 条目不占容量 ⇒ 纯金箱容量 0 但照装不误）。</summary>
    private static int SpaceOf(ResourceList pack)
    {
        int sum = 0;
        if (pack.items != null)
        {
            for (int i = 0; i < pack.items.Length; i++)
            {
                int amt = pack.items[i].amount;
                if (amt > 0) sum += amt * ResourceCatalog.VolumeOf(pack.items[i].type);
            }
        }
        return sum;
    }

    void Awake()
    {
        Render();
    }

    /// <summary>渲染箱子占位（2_10 替换成真实瓦片）。</summary>
    private void Render()
    {
        if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
        _renderer.sprite = ValleyRampart.Rendering.PlaceholderSprites.Get("feat_treasure_box");
        _renderer.sortingOrder = 5;
    }

    /// <summary>箱子是否已空（容器空 ＝ §九「生命结束触发②」）。</summary>
    public bool IsEmpty => _store == null || _store.TotalCount <= 0;

    // ===== ITaskSource（⭐ `HH.316` 件2 · 一箱一源 · `D798` 裁 ④）=====

    /// <summary>源有效 ＝ 实体在场 ＋ **容器非空**（有货可搬）。⚠️ 装箱走**最后一批**的工人（已入
    /// `MovingToDest`）不受本失效牵连 —— 见 `TaskScheduler.UpdateAssignedTasks` 箱源例外注
    /// （否则该批滞留背包 ⇒ 到账断链）。</summary>
    public bool IsValid => this != null && !IsEmpty;

    /// <summary>箱位置（第一段位移目标 · 快照 ⇒ 销毁后仍可安全读）。</summary>
    public Vector2 SourcePos => _pos;

    /// <summary>
    /// 广告搬运任务（复用 `Transport` · ⛔ 不新增枚举；`args` 沿用 `ScaleTaskArgs` —— ⭐ 规模派工
    /// ⇒ 同一箱可**并发多工人**各取一趟（准 · 符合"先到先得"））。
    /// 取**资源表序首个非空**（确定性）⇒ 多资源箱**逐轮广告多轮搬运**（判据 4）。
    /// `destType = None`：箱**无主**（`SourceKingdom → -1`）⇒ 第二段落点须**装载成功后按
    /// 搬运者国 ＋ 实载资源**即时解析（件 4 · 广告时定不了）。
    /// </summary>
    public bool TryAdvertiseTask(out KingdomTask task)
    {
        task = null;
        if (_store == null || IsEmpty) return false;
        var type = _store.PrimaryStoredType();      // 资源表序首个非空（确定性）
        int have = _store.GetAmount(type);
        if (have <= 0) return false;
        task = new KingdomTask(KingdomTaskType.Transport, this);
        task.destType = KingdomDestType.None;
        task.args = new ScaleTaskArgs
        {
            resourceType = type,
            totalResourceDemand = have
        };
        return true;
    }

    public void OnRegister() { }

    /// <summary>⛔ 关键清算**勿放此处**：`TaskScheduler.Tick` ① 清无效源**不回调** `OnUnregister`（R5）
    /// ⇒ 注销/清算走 `ChestManager` 显式钩子（`Remove`／`ClearAll`）。照 `Building`／`ConstructionSiteStore` 先例留空。</summary>
    public void OnUnregister() { }

    // ===== IInteractable（⭐ `HH.316` 件6 · `D798` 裁 ①「链 B 并回链 A」）=====

    /// <summary>
    /// 玩家手点 ＝ **调用搬运任务的一种形式**（`09` §9.8 :390）⇒ **立案一个搬运任务**
    /// （立即触发一次调度 ⇒ 工人在场即来搬；无空闲工人则照常每 tick 广告、来日再来）。
    /// ⛔ **不提供「捡」的能力**（§9.8 :389「它**就是个仓**」）／⛔ **无独立入账口** —— 到账只走
    /// 链 A 卸货段（`UnloadInventory` → 就近同国仓；无仓 ⇒ `AddGatherOverflow` 兜底）。
    /// ⭐ `M1-E`：**金与材料同路**（原「金→`Gold` 字段」直通已退役 ⇒ 金进该国国库仓）。
    /// </summary>
    public InteractionResult Interact(Interactor ctx)
    {
        if (IsEmpty) return InteractionResult.None;   // 空箱 ⇒ 无货可搬（⛔ 不出任务）
        if (TaskScheduler.HasInstance) TaskScheduler.Instance.RequestHaulNow(this);
        return InteractionResult.None;                // ⛔ 不返回资源包（`D798` 已否决「Interact 内直接入账」）
    }

    // ===== 命中（D247）：HP=1 一击碎，内容物返回地面可再拾 =====

    /// <summary>命中（D247）：HP=1 一击碎 ⇒ 内容物原地重落箱（经 `ChestManager.ResetDrop` · 容器转发读口）。</summary>
    public void Strike()
    {
        if (--hp <= 0)
        {
            // 破碎：内容物原地重新落箱（可再拾取）
            if (ChestManager.HasInstance) ChestManager.Instance.ResetDrop(this);
        }
    }
}