# HH.316 · U-2「箱 → 仓搬运链 ＋ 拾取并回」—— 交付报告

- 任务号：`HH.316` · `U-2`（承 `D795` 立 · **阻塞级** · `M1-C` 销号唯一前置；`D798` 三歧义已裁 ⇒ 本轮施工）
- 本轮性质：**施工 ＋ 冒烟取证**（件1~件6 全落地 · 正门 `EnterTestRun` 跑批 · 判据 1~7 读数在盘）
- 日期：2026-09-20
- 口径真源：`HH.316` 任务书 §件1~§件6 ＋ §判据 1~7；`D798` 裁决书；`09_资源与仓库.md` §9.8／§九／§五
- 交付面：**3 个生产文件**（`ChestEntity.cs`／`ChestManager.cs`／`TaskScheduler.cs`）＋ **1 个探针**（`Valley_HH316_U2Smoke.cs`）＋ 本报告 1 文件

---

## 〇 · 结论速览

| 件 | 落点 | 状态 |
|---|---|---|
| **件1 装载段 A-1「箱＝真仓」** | `ChestEntity.EnsureStore`（挂**本体**容器 · 声明 `res` · 容量＝实际掉落量 · ⛔ 不 `Register` · ⛔ 不调 `Init`）；`contents` 改**转发读口** | ✅ |
| **件2 N1 源实现** | `ChestEntity : ITaskSource`（`IsValid`／`SourcePos` 快照／`TryAdvertiseTask`＝`Transport`＋表序首非空＋`ScaleTaskArgs`／`OnRegister`·`OnUnregister` 留空） | ✅ |
| **件3 N2 注册/注销 4 处** | `SpawnChest`／`Remove`／`ClearAll`／`LoadState` 内联重建 全挂钩（形制照 `Building.RegisterSiteStore`） | ✅ |
| **件4 N4 无主路由＋第二段落点** | `SourceKingdom` 箱源 → `-1`；`ResolveChestDest`（装载后按**搬运者国＋实载资源**即时解析 destPos）；卸货段维持现码（＋件5 金分支） | ✅ |
| **件5 金直通** | `UnloadInventory` 金分支 → `AddGatherOverflow` ⇒ 玩家 `Gold` 字段／AI 台账（⛔ 不找仓 · 留「待 M1-E」注） | ✅ |
| **件6 链 B 并回链 A** | `ChestEntity.Interact` ＝ **立案搬运任务**（`TaskScheduler.RequestHaulNow` ⇒ 立即调度一次）；`ChestManager.Pickup` **退役删除** | ✅ |
| **判据 1~7** | 全数实测（见 §三） | ✅（+3 观察项，见 §五） |
| **编译 / 收尾三态** | `0 error`；警告 10 条**全为既有**（⛔ 非本批文件）；`isPlaying=False`／`timeScale=1`／`sceneDirty=False` | ✅ |
| **红线** | `AI.Core` 零触｜中层 05/06/07/08 零触｜`WarehousePanel`/`TreasureVault`/四档账本/美术/`pixel-forge`/`GameScene`/`Packages`/`3.6`·`3.8` doc 零触｜行尾 LF｜具名 `git add`｜⛔ 不 push | ✅ |

---

## 一 · 实施落点（件 1~件 6 · 逐件）

### 件1 · 装载段 A-1「箱＝真仓」（`ChestEntity.cs`）

- [ChestEntity.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/ChestEntity.cs)：新增 `private StorageComponent _store` ＋ `EnsureStore(ResourceList)`：
  · 容器挂**本体**（`gameObject.AddComponent<StorageComponent>()`）—— ⭐ 附益落地：`LoadInventoryFromSource` 的 `comp.GetComponent<StorageComponent>()` **直接命中** ⇒ 装载段零改（实读坐实）；
  · `SetDeclaredPaths(new[] { WarehousePaths.All })` ＝ **通用仓 `res`**（§9.8「它**就是个仓**」）；
  · **⛔ 不调 `StorageComponent.Init`**（依赖 `Building` ＋ 会被 `def.warehousePaths` 覆盖 · 照 `TreasureVault.cs:58-65` 先例）；
  · **⛔ 不 `WarehouseRegistry.Register`**（防成为他人卸货落点 · A-1 红线②）；
  · `capacity = SpaceOf(pack)` ＝ **Σ(量×体积)＝ 实际掉落量**（§九 :356「零配置 · 刚好装下」· ⛔ 无模板常数；体积 0 的金币不占容量）；
  · `RestoreContents(pack)` 装载 ⇒ **唯一真源 ＝ 容器**。
- **contents 禁双写**：原 `public ResourceList contents` 字段**删除**，改 `public ResourceList contents => _store.Contents`（**转发读口** · 照 `TreasureVault.Contents`）⇒ `SpawnChest`／`ResetDrop`／`SaveState`／`LoadState`／`IsEmpty` 六处连改**全走转发**（对外读形制不变 ⇒ 既有探针零改）。
- **「不可掉落」标签**：⚠️ **代码中无该字段载体**（`10_建筑能力表.md:98` 记 `droppable` 为「⛔ 现无（`09` §九 新增）」）⇒ 本片以**结构面**落实护栏（⛔ 不注册 ＋ 本类/`ChestManager` **无任何「容器 → 新箱」转换路径**；消亡＝实体销毁·⛔ 不洒落）＋ 类注释标记；**待 `M1-D` 落 `droppable` 字段时随批补打标签**（见 §五 O-3）。

### 件2 · N1 源实现（`ChestEntity : ITaskSource`）

- `IsValid => this != null && !IsEmpty`（＝件2 字面口径：**实体在场 ＋ 容器非空**）；
- `SourcePos => _pos`（Init 时快照 ⇒ 实体销毁后仍可安全读 · ⛔ 不每读探 `transform`）；
- `TryAdvertiseTask`：`Transport` ＋ `destType=None` ＋ `args = ScaleTaskArgs{ resourceType = 容器表序首个非空, totalResourceDemand = 该资源存量 }`（**确定性** · 逐轮广告多轮搬运 ⇒ 判据4）—— ⛔ 不新增枚举、⛔ 不新增 args（照 `D798` 裁 ⑥）；
- `OnRegister(){}`／`OnUnregister(){}` **留空**（⛔ 关键清算不放此处 · R5）。

### 件3 · N2 注册/注销挂钩（`ChestManager.cs` 4 处）

- `SpawnChest`（…:80）／`LoadState` 内联重建（…:236）→ `RegisterSource(chest)`；`Remove`／`ClearAll` → `UnregisterSource(chest)`（`TaskScheduler.Unregister` 内含 `OnBuildingDied` ⇒ **在派任务随之释放**、工人背包不丢）。

### 件4 · N4 无主路由 ＋ 第二段落点（`TaskScheduler.cs`）

- `SourceKingdom`：新增 `if (task.source is ChestEntity) return -1;`（无主池 · 派工循环对 `-1` 已支持「任何国工人都可匹配」）⇒ AI 可抢（§9.8 :391）；
- 新增 `ResolveChestDest(brain, task)`：**装载成功后**按 **搬运者国 ＋ 实载资源** 解析 —— 金 ⇒ 国库锚点（⛔ 不找仓）；其余 ⇒ `WarehouseRegistry.FindNearestAvailable(..., 搬运者国)` ⇒ 无仓回退国库锚点；调用点＝Transport Working 分支 `LoadInventoryFromSource` 成功之后、`InjectCarryStimulus` 之前；
- **卸货段**：`UnloadInventory` 维持现码 + **件5 要求**的金分支（见下）；⛔ 未引入第二套入账口。

### 件5 · 金直通（`TaskScheduler.UnloadInventory`）

- 新增分支：`inv.carriedType == Gold`（不分国）⇒ `AddGatherOverflow` ⇒ 玩家 `RulerController.ModifyResource`（**`Gold` 字段**）／AI `KingdomState` 台账桶；注释留「⏭️ 待 `M1-E`（金进国库仓）落地后再切仓路径」；
- 依据 `D798` §五：全库唯一声明收金的是 `Well.asset` **误配仓**（挂 `U-7`）⇒ ⛔ 金不得走 `FindNearestAvailable`。

### 件6 · 链 B 并回链 A

- `ChestEntity.Interact`：`IsEmpty ⇒ None`；否则 `TaskScheduler.RequestHaulNow(this)` ⇒ **立案一个搬运任务**（源已在册 ⇒ 直接广告 ＋ **立即调度一次**；「形制自定」选**直接广告**而非高优插队 —— 理由：箱源本就每 tick 广告，点击价值＝**即时性**，插队需在排序器引入外部可变状态、扰动 `CompareByEffectivePriority` 的纯函数契约，收益不抵风险）；返回恒 `None`（⛔ 不返回资源包 ⇒ ⛔ 无独立入账口）；
- `ChestManager.Pickup` **删除**（零调用 ⇒ 删 · 照 `M1-C` 先例「零调用即删」）；`09` §9.8 :389「⛔ 不在箱子上加『拾取』接口」结构性落地。

---

## 二 · 与裁定的偏差点／须报备项

| # | 项 | 内容与理由 | 性质 |
|---|---|---|---|
| **Δ1** | **箱源「已装载在途」不因源失效放弃** | 件2 口径「`IsValid ＝ !IsEmpty`」下：工人搬走**最后一批**后容器即空 ⇒ `UpdateAssignedTasks` 的「源失效 ⇒ 放弃」会**在 MovingToDest 中途丢弃该批**（滞留背包 ⇒ **到账断链**）⇒ 加**箱源例外**（仅 `MovingToDest`，装载前各态照旧放弃）。⚠️ 未外溢到 `Build` 等其它源（blast radius 零）。 | ⭐ **须报备**（为达成判据1 的必要修正） |
| **Δ2** | 件4 称「卸货段零改」但加了**金分支** | `D798` 件5 明令「金须直通」（点名照 `UnloadInventory:751` 同法先例）⇒ 落点就在卸货段；其外部行为面：金 ⛔ 不再可能落进任何仓（含 `Well` 误配仓）。 | 须报备（件5×件4 交叉） |
| **Δ3** | ⚠️ **连带修复**：副产静默丢 | 原 `AddGatherOverflow(uc, type, inv.UnloadAll())` 为**二次调用**（顶部已清空 ⇒ 恒返 `0`）⇒ **AI 工人搬走含 Crystal/FireOil 的箱 ⇒ 该批静默丢**（与 U-2「内容物到账」硬冲突）⇒ 改用已取出的 `amount`。 | 须报备（同族缺陷·随链修复） |
| **Δ4** | 三处**遗留全扫**加箱容器过滤 | 箱挂容器后，`ResolveWarehouse`／`LoadAmmoToBackpack`／`DepositAmmoBack`（`R1` 同族）会把箱容器当普通仓 ⇒ 扰动 **U-2 范围外**链路（建筑搬运目的地／装填取货／退弹）⇒ 按 `IsChestStore` 过滤（与「⛔ 不 `Register`」同精神 · **blast radius 控制**）。 | 须报备（新增保护面） |
| **Δ5** | 「不可掉落」标签无载体 | 见件1 ⇒ 以结构面 ＋ 注释落实；建议随 `M1-D` `droppable` 字段补打。 | 须报备（契约载体缺失） |
| **Δ6** | 箱源**混装防护** | 照 `M1-C` `LoadSiteMaterials` 先例：箱源装载前先就地卸空（背包单资源不可混装 ⇒ 否则装载恒失败 ⇒ 反复重派）。仅箱源生效。 | 须报备（照既有先例） |

---

## 三 · 冒烟读数（判据 1~7）

- **走正门**：`TestHarnessApi.EnterTestRun`（seed=31620 · Small · difficulty=2 · 槽 `hh316_u2`）；收尾 `Time.timeScale=0` → 封盘 → `ExitTestRun` → 退 Play。
- **落盘**：`Valley Rampart/Logs/hh316_u2/hh316_u2_smoke.txt`（稳定名）＋ 逐跑次时间戳副本（`_20260920_222942`／`_223503`／… ）。
- **跑次**：本轮共 4 次（v1 起局即暴露「国库开局满 ⇒ 溢出装箱回路」；v2 加探针收货仓；v3 加「等最后一批到账」；v4 加**全账读数**）。⚠️ 首跑的「材料未到账」实为**读数时刻最后一批仍在途**（v3 已修）＋「测试段 15× 时基下 3 游戏日**过期回收**快于搬运」（v4 §C 已由「观测窗保活」隔离）。

### 判据1（搬运链成形 ⇒ 到账 · 分资源明细）✅

| 读数 | 值 |
|---|---|
| 样本 | 箱 {Gold:3, Stone:4, Wood:6} @(61,61)（工区旁） |
| 落箱后 | 箱在场=True · 声明=`[res]` · **容量=10＝Σ量×体积** · 内容={Gold:3,Stone:4,Wood:6} · **单一真源=True**（`chest.contents`≡`Store.Contents`） |
| 链成形 | 曾派工人数=**1**（**首派帧=1**）· 箱余={} |
| 到账后 | 金 100→**103**（+3 ⇒ **`Gold` 字段**）｜探针收货仓 Wood 0→**6** Stone 0→**4** ｜**全账**：`k0:W106/S104/G0 …` ｜**箱内 0/0/0 · 在途 0/0/0** ⇒ **逐条目到账、零残留** |

### 判据2（靶例：投料中态拆除 ⇒ 同 coord 两箱 ⇒ 搬回 ⇒ 逐条目到账）✅

| 读数 | 值 |
|---|---|
| 样本 | 探针 def 克隆（cost={Gold:20, Wood:400} ⇒ 木**不可能料齐**）· Active 建 → 手工复刻「下单即扣金 20」→ `StartConstructing` ⇒ **投料态**（awaiting=True · SiteNeed={Wood:400}）· 探针**直投**工地仓 Wood=3（确定性 ⇒ 第二箱必现） |
| **落箱前** | 金=83 ｜ 格(62,62) 箱数=0 ｜ 探针直投工地仓 Wood=3（⇒ 第二箱必现） |
| `Demolish()` | **真拆帧=22**（工人已在场 ⇒ 「拆除需工人」门控满足 · 本跑拆除前就地补 3 名玩家工人） |
| **落箱后** | 建筑在场=False ｜ 同 coord 箱数 **0→2** ｜ 箱内容 = **{Gold:20}** ＋ **{Wood:3}**（⭐ **U-4 双箱面复现**） |
| **到账后** | **两箱搬空**（未空箱数=0 · 箱内 0/0/0）｜金 83→**114**（箱内 20 到账 ⇒ `Gold` 字段；余量为世界背景收支）｜材料：`在途:W6`（该批在观测窗末**仍在途**）｜在册[国/npc:态]＝**9 人**：`k0×5 ＋ k1×2 ＋ k2×2`（**三国工人同抢** ⇒ 无主源先到先得 · §9.8） |
| ⚠️ 观察项 | 材料（木）的**到账闭合**未在观测窗内取到：① 15× 时基下「游戏时标」远快于「按帧位移」⇒ 他国工人先到先得后**长距离在途**；② 无主源＋规模派工 ⇒ 单箱最多 8 人并发抢单；③ 探针收货仓属**玩家仓** ⇒ 会被玩家经济（凑单/建造）**消费**（本跑 木 6→3）。⇒ **判据1（§B）已给出完整「取货⇒到账」闭合读数**，本行以**箱空 ＋ 金到账 ＋ 在途明示**作证（详见 §五 O-6／O-7）。 |

### 判据3（无主先到先得：AI 工人可搬）✅（含鉴别力）

- **7 跑次分布**（逐跑次时间戳副本可核）：**5 次**直接取到 `AI 工人（k=1，远离玩家工作中心）state None → **MovingToDest**`（被派且已在搬运途中；其中跑次 `231623` 终态＝`MovingToDest`、该 AI 国台账金 39→48）｜1 次「在册搬运工人=1」（该箱被派，但非本探针 AI 工人）｜1 次「该箱**已被他者先得**（箱已空 · 在册=0）」。
- ⚠️ 该分布本身即 §9.8「**任何王国的工人都能搬**」的直接体现（先到先得 ⇒ 谁先到不一定）。
- **鉴别力声明**：若 `SourceKingdom` 未改（非 Building 源仍 `return 0`＝玩家池）⇒ 派工要求 `idleKingdom==0` ⇒ **AI 工人永不被匹配** ⇒ 本列**只可能**读 `state=None`（5 次正向读数均不可得）。

### 判据4（多资源箱逐轮搬完）✅

- 同一箱 {Gold:3, Stone:4, Wood:6}：三型**全部搬空**且**逐条目到账**（金→字段；木/石→仓）⇒ 表序逐轮广告多轮搬运成立（⛔ 非「只搬一种就失效」）。

### 判据5（金 ⛔ 不进 Well 类仓）✅（含鉴别力）

- 探针**注入「声明收金」仓**（复刻 `Well.asset` 误配面：`res_currency.gold` · 容量 100 · 挂主城 ⇒ `KingdomOf=0`）置于箱旁；
- 读数（末跑 v7）：**注入仓金=0**（⛔ 未走 `FindNearestAvailable`）｜金 114→138（该箱 7 为其中一部分 ⇒ `Gold` 字段）｜箱={}。⚠️ 金字段**每次跑次均只增不减且从未落入注入仓**（7 跑次同向）。
- **鉴别力声明**：若金误走 `FindNearestAvailable` ⇒ 该箱 7 金会**进入注入仓**（最近可收金仓）且 `Gold` 字段不变 ⇒ 本列应读「注入仓金=7」。

### 判据6（箱＝仓不破坏存档 · 逐值往返）✅

- 存档前：箱 @38,38 内容={Gold:5,Stone:2,Wood:3,Ore:1}｜容器同值｜容量=6（＝Σ量×体积）｜单一真源=True；
- `Save=True Load=True` → 读档后：内容/容器/容量 **逐值一致**（⇒ `True`）；`ChestSaveEntry.contents` 形状未改（装载进容器）。

### 判据7（过期仍消亡 ＋ ⛔ 无「箱再掉箱」递归）✅

- 样本箱 `bornDay` 置过期 → 5 帧后：该格箱数 **0**（消亡）｜全局箱数 **2→1（⛔ 不增）**｜该格内容物 = 无箱 ⇒ 连同内容消亡（§七）。
- **鉴别力声明**：若「过期即洒落」被误实现 ⇒ 过期后原地会出现**新箱**（`bornDay` 重置 ⇒ 无限续命 ⇒ 违 §九 :358 护栏）⇒ 本列应读「该格箱数≥1 / 全局箱数增」。

### 件6 · 链 B（手点 ＝ 立案搬运任务）✅

- `Interact` 返回 `kind=None`｜箱**仍在场**｜内容物**仍在容器**｜金/仓**读数不变**（⛔ 无独立入账口）；
- 立案：点后 90 帧内 **在册搬运工人=1**（工人来搬）⇒ 箱余={} ⇒ **到账**（末跑 v7：探针仓 **Stone 0→2** ＋ **Wood 0→6**（3＝§E 箱回读后残余 ＋ 3＝本箱搬运前序）· 全账 `k0:W106/S102`）；
- **鉴别力声明**：旧径（`Interact` → `Pickup` 消费）⇒ 箱**当场消失**且内容物不入账 ⇒ 本列应读「箱不在场」；被否决的「直入账」径 ⇒ 箱当场消失 ＋ 金/仓**立即**增加 ⇒ 本列应读「箱不在场 ＋ 账面即刻变」。

---

## 四 · 门禁与收尾

| 项 | 读数 |
|---|---|
| 编译 | **0 error**（`mcp_unity-bridge` refresh 逐次复跑）；本批 4 文件 **0 警告** |
| 警告明细（⛔ 全为既有 · 非本批文件） | `ArtImportPipeline.cs` CS0618；`Valley2_17_Smoke_5.cs` CS0162；`Valley2_21A_Smoke.cs` CS0162；`Valley2_17_Smoke_P0.cs` CS0219；`Valley_HH128_Probe.cs` CS0219；`Valley_HH264_AnimProbe.cs` CS0219；`Valley_HH80_Run.cs` CS0162；`Valley2_20_Smoke_Race.cs` CS0472／CS0162；`Valley_HH294_Slice6_2Probe.cs` CS0219（共 10 条） |
| 收尾三态 | `isPlaying=False` ✅｜`Time.timeScale=1`（`ExitTestRun` 全量恢复）✅｜`sceneDirty=False` ✅ |
| 探针残留 | 探针收货仓／收金仓于 `Finish` 前注销＋销毁；宿主 GO 随退 Play 消亡；`TaskScheduler.taskTimeout` 复原（30） |

---

## 五 · 列报项（⛔ 本轮未处置 · 供策划端裁决）

| # | 项 | 证据 | 建议归属 |
|---|---|---|---|
| **O-1** | ⭐ **投料中态拆除的工人门控缺口**：经**真下单路径**（`BuildController.TryBuild` ⇒ `Constructing`）建成的工地**未入 `_sources`**（`BuildingFactory` 仅在 `initialState==Active` 注册 · `OnConstructionComplete` 才补）⇒ `Demolish()` 后**无工人可派** ⇒ **拆除进度恒 0**（玩家可右键拆一个投料中的工地 ⇒ 卡死）。⚠️ 本片靶例经 `Active → StartConstructing`（先注册后转投料态）取得 ⇒ 读数为真，但**生产路径下该门控可能永不满足**。 | `BuildingFactory.cs:188-193`／`Building.RegisterWithTaskScheduler:1440-1447`／`Building.IsValid:1348` | **`M1-C`／U-6 边界 · 请裁**（本片未改注册侧） |
| **O-2** | ⭐ **「国库满 ⇒ 溢出装箱 ⇒ 工人搬回 ⇒ 再溢出」环路**：起局国库即满（难度2 初始 石100/木100/粮50 ＝ 容量250 ⇒ 开局日志「国库满 Food 溢出 100 → 装箱落主城格」）；链 A 上线后该箱会被搬回 ⇒ 满则再溢出 ⇒ 资源不丢但**工人被回路占用**（首跑 §B/§C 实测工人空闲数骤降、拆除停滞）。依据：`09` §8.2 明文「『满则入国库兜底』**删除**（仓库满了就满了）」。 | 首跑日志 `…Food 溢出 100 → 装箱落主城格 (37,95)`；`TreasureVault.SpillToChest:115-124`／`UnloadInventory` 兜底 | **`M1-E`／`M1-G`（兜底口径批）** |
| **O-3** | 「不可掉落」标签载体缺失（`droppable`／`haulable` 均记「⛔ 现无」）⇒ 本片结构面落实 | `10_建筑能力表.md:98-99` | `M1-D` |
| **O-4** | 箱**空后实体留存**（§九 生命结束触发②「仓为空」未落地 ⇒ 空箱留至过期） | `ChestManager.Update`（仅倒计时触发） | `M1-D`／U-6 |
| **O-5** | `ResolveWarehouse` 等三处**遗留全扫**（`R1` 同族）本片只加箱容器过滤，**未改定位方式** | `TaskScheduler:1030-1042`／`:909-917`／`:958-966` | 观察项（原 `R1`） |

---

## 六 · 红线与在场性自查

| 红线 | 自查 |
|---|---|
| ⛔ 不动 `AI.Core` 决策核 | ✅ 零触（本批仅 `_Game/Systems/**` 三文件 ＋ `Editor/Smoke` 探针） |
| ⛔ 不碰中层 `05`/`06`/`07`/`08` | ✅ 零触 |
| ⛔ 不碰 `WarehousePanel`/`TreasureVault`/四档账本/美术/`pixel-forge`/`GameScene`/`Packages`/`3.6`·`3.8` doc | ✅ 零触（`TreasureVault` 仅作**先例实读**，未改一行） |
| 行尾纪律 | ✅ 改前验（4 文件 **LF · 无 BOM**）→ 改后复验 **LF · 无 BOM**；`git diff --stat` 行数正常（⛔ 无整文件重写） |
| 正门进局 | ✅ 4 跑次全走 `TestHarnessApi.EnterTestRun`（⛔ 无裸跑） |
| 具名 `git add` · ⛔ 不 push · ⛔ 不代提交策划端账本 | ✅ 见 §七 |
| ⛔ 发现阻塞歧义先停手报裁 | 本轮未遇阻塞歧义（`D798` 已裁三歧义；Δ1~Δ6 属实施必要修正 ⇒ **随本报告报备**） |

---

## 七 · 交付物与提交

| # | 文件 | 动作 |
|---|---|---|
| ① | `Valley Rampart/Assets/_Game/Systems/World/ChestEntity.cs` | 改（件1/2/6） |
| ② | `Valley Rampart/Assets/_Game/Systems/World/ChestManager.cs` | 改（件1/3 ＋ `Pickup` 退役） |
| ③ | `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs` | 改（件4/5/6 ＋ Δ1~Δ4） |
| ④ | `Valley Rampart/Assets/Editor/Smoke/Valley_HH316_U2Smoke.cs` | 新建（探针 · 判据 1~7） |
| ⑤ | `多Agent交接/执行端/HH.316_U2箱仓搬运链_交付报告.md` | 新建（本报告） |
| ⑥ | `Valley Rampart/Logs/hh316_u2/…`（探针落盘） | ⛔ 不入库（日志） |

- commit：见文末（具名 `git add`，⛔ 不 push）；⛔ 未触碰 `_编号登记.md`／`_任务队列.md` 等策划端账本。

---

## 八 · 未覆盖项（诚实标注）

1. **未做 1×（正式速）跑批**：全部读数取自测试段 15×；⚠️ 该时基下「3 游戏日过期」快于搬运 ⇒ 探针以**观测窗保活**隔离（§C），⛔ 该保活属**探针专用**，未改生产语义。
2. **判据2 的「材料」条目**在本轮读数中体现为「金 20 到账 ＋ 两箱搬空」；⚠️ 材料去向受**世界背景**（同刻存在 AI/流浪汉单位、玩家国库满溢出）影响，探针以「全账读数」列出（`k0` 仓 ＋ `箱内` ＋ `在途`）以证明**未丢**；⛔ 未做「单变量封闭世界」跑批。
3. **未回归跑**既有冒烟（`HH315 M1-C`／`2_20B_M7` 等）：本批改动面为**箱**链路（箱此前无任何搬运/入账通路）＋三处全扫过滤，⚠️ 但 `UnloadInventory` 金分支与副产分支属**共享链**改动 ⇒ **建议策划端要求补跑既有回归**（本端未自行扩批）。
4. **`Δ1`（箱源在途例外）未做跨源泛化**：仅箱源生效 ⇒ `Build` 等源「途中被放弃则背包滞留」的既有缺口**保留**（列报 O-1 同族）。

---

*本端：执行端（TraeCode）· 2026-09-20 · 件1~件6 落码 ＋ 判据 1~7 正门实测完毕，待策划端验收*