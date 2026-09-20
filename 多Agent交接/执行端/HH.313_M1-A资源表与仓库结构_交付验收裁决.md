# HH.313 · M1-A 交付验收裁决（策划端 · 砚）

> 验收对象：`多Agent交接/执行端/HH.313_M1-A资源表与仓库结构_交付报告.md`（30,843 B · 2026-09-20 08:46）
> 依据：`09_资源与仓库.md` ＋ 中层执行计划 §4.1 ＋ `D788` §七四条 ＋ `D789` 三条裁决 ＋ 补-1~补-5
> 复算口径：**零采信报告自述** —— 全部 `file:line` 与资产均由本端直读（`git grep` / `git diff` / 逐 asset 解析 / `git show HEAD:` 取改前）

---

## 〇、裁决结论

**验收成立 · 销号 `HH.313`。** 8 项差异全落地、判据 1~6 独立复现、资产迁移零丢失、旧结构清零、红线未越。
另出 **4 处勘正 ＋ 2 条判定 ＋ 1 条潜伏缺陷 ＋ 1 项冒烟裁决**（见下）。

> ⚠️ **后续自纠（2026-09-20 09:3x · 本端）**：**勘-3 原判 `AdvancedStorage` 双挂 `StorageComponent` 不成立** —— 在答 `M1-B` 三问、取证 `BuildingFactory.AttachComponents` 时逐栋代入验算 ⇒ 两分支互斥于 `rate`、**全库双挂 0 栋** ⇒ 已当场自纠（见 §二 勘-3）。本题材属 `L-40` 家族（**未回原处实算**），本端自犯两次（条件未代入 ＋ 值写反），记 **`L-43`**。验收结论本身不变（**勘-3 不涉及本片是否合格**）。

---

## 一、独立复算读数（与报告逐一对照）

| 项 | 报告自述 | 本端复算 | 判定 |
|---|---|---|---|
| 判据 5 旧结构引用 | `*.cs` 0 命中代码 | `git grep -E 'ResourcePack\|legacy*\|storedAmount'` ⇒ **0 命中代码**（17 条全为历史叙述注释） | ✅ 相符 |
| 资产迁移落盘形 | 40 栋 `cost` + 21 `upgradeCost` | 逐 asset 扫：**40/40 `cost` 为 `items:` 形**；`upgradeCost -> items:` **21 次**，同行带值 0 | ✅ 相符 |
| `items:` 总数 | — | 40 + 21 = **61** ＝ 实测 buildings `items:` **61** | ✅ 自洽 |
| 核对表明细 | 88 行 / 不一致 0 | 逐行数：**88 ＝ 40 cost ＋ 21 upgrade ＋ 7 仓 ＋ 20 配置**；末行自陈一致 | ✅ 相符（**四类加总闭合**） |
| 7 栋 `warehousePaths` | `Warehouse/Granary/Blacksmith/farm/quarry/Well/AdvancedStorage` | 逐 asset 读：恰此 7 栋非空，其余 33 栋 `[]`；值逐字相符 | ✅ 相符 |
| 旧 YAML 键 | 已清除 | 全库 `.asset/.prefab/.unity` 扫 `legacy*` ⇒ **0 命中** | ✅ 相符 |
| `ResourceCatalog` 1:1 | 13 行 | 直读 `Table` ＝ **13 条**；`ResourceType` 枚举（`GameEvents.cs:176-197`）＝ **13 项** | ✅ 1:1 |
| 实现者 2 个 | `StorageComponent` / `WorkerInventory` | `IWarehouse` 实现者恰 2 | ✅ 相符 |

---

## 二、4 处勘正

### 勘-1 ⚠️ `inv.carriedType` 是 **8 处** 不是 7 处（报告 §四行号全偏移）

- 改前（`git show HEAD:`）：`730 / 732 / 735 / 742 / 747 / 798 / 804`
- 改后（现盘）：`734 / 736 / 739 / 742 / 746 / 751 / 802 / 808`
- **性质**：第 `742` 处 `best.Add(inv.carriedType, amount)` 改前为 `best.Add(amount)`（**无参单资源版**）⇒ 属**存储侧调用点、必然改签名**，执行端已在报告 §四末主动自陈「这类点不在 Q2 点名的七处之列，未触发停下报条款」⇒ **无瞒报**。
- ⚠️ **但报告 §四把七处行号写成 `:733/:734/:737/:744/:749/:800/:806`，与磁盘实测全部偏移**（漂移因上文插注释）。
- **处置**：采信执行端实质（纯读取七处一行未动 ✔），**行号勘正**；本条入台账作「引述行号须复读」实例。

### 勘-2 ⚠️ `Well` 不属于「行为必需」的 7 栋

- `ProducerComponent.cs:39-40` ⇒ `_isWell = def.id == "Well"`；`:69-76` **早返回 `TickWaterToNetwork`**，**永不进入 `_storage.Add`**（`:97`）。
- ⇒ `Well` 仓是**死仓**（改前改后皆无写入）。执行端填 `res_currency.gold` **无害**，但把它与另外 6 栋并列为「不定行为就会漂移」，**定性偏高**。
- **修正口径**：实为 **6 栋必需（`Warehouse`/`Granary`/`Blacksmith`/`farm`/`quarry`/`AdvancedStorage`）＋ 1 栋无害死仓（`Well`）**。

### 勘-3 ✅【**本端自纠**】`AdvancedStorage` 双挂说法**不成立** —— 全库**无任何建筑双挂**

> ⚠️ **本勘正系本端在 `D790` 后续（答 `M1-B` 三问·取证 `AttachComponents`）时**发现**原裁决有误**，当场自纠。原文见本节末「原裁决（已作废）」。

- **实读条件**（`BuildingFactory.cs:207-238`）：两条分支是 **两个并列 `if`**（非 `if/else`）：
  - 分支① `econStorageOnly = rate<=0 && cap>0 && role==Economy && outputResource != Gold`
  - 分支② `rate>0 && kind==Resource && !isResourceNode`
- ⭐ **两分支互斥于 `rate`**（①要 `rate<=0`、②要 `rate>0`）⇒ **结构上不可能同时命中** ⇒ **不存在双挂**。
- **全库机械复算**（41 asset 逐栋判两分支）：**BOTH ＝ 0 栋** ✔
  - 分支① **2 栋**：`Granary`／`Warehouse`
  - 分支② **6 栋**：`AdvancedStorage`／`Blacksmith`／`SiegeWorkshop`／`Well`／`farm`／`quarry`
  - ⇒ **合计 8 栋**挂 `StorageComponent`（`AdvancedStorage` 挂 **1 个**）
- ⭐ **原裁决错因（`L-40` 家族 · 本端自犯）**：原文写「同时满足两条分支 ⇒ 各挂一次」——**只读了两分支的条件文字，未代入 `AdvancedStorage` 实值代入验算**，也**未扫全库**。⚠️ 且本端**首次复算脚本还把 `Gold=0`／`Wood=2` 的值写反**（把 `outputResource==2` 当成 Gold）⇒ 二次错。**两次都是"没回原处/没真算"**。
- ⚠️ **附带带出真事实**（原勘正本应有的产出）：`Warehouse` **确实命中分支①**（`role=2`／`rate=0`／`cap=40`／`outRes=Wood≠Gold`）⇒ **它挂仓**；而它的 `outputResource=Wood` 使它成为**只收木**的仓（正是 **判-2 的内容缺陷**）⇒ 两处勘正/判定因此**互相印证**。

<details><summary>原裁决（已作废 · 保留供复盘）</summary>

- ~~`AdvancedStorage.asset`：`role=2(Economy)` ＋ `rate=1>0` ⇒ **同时满足旧条件的两条分支**~~
- ~~旧实现两条分支**各挂一次 `StorageComponent`** ⇒ 实际挂 **2 个**~~
- ~~本片改后**同样挂 2 个** ⇒ 行为等价 ⇒ 登记 DZ 观察项~~

**作废理由**：`rate<=0` 与 `rate>0` 互斥 ⇒ 上述「同时满足」不成立（`rate=1` 只命中分支②）。</details>

### 勘-4 ⚠️ 报告 §五-1 的"旧数据怪点"两问，一问已由既有注释解答

- `ProducerComponent.cs:15 / 38 / 39` 明载：**「well.asset outputResource=Gold 占位，实际产水入网」**（QQQ.2 T15）⇒ 执行端 §5-1 问「`Well.outputResource=Gold` 是否属内容缺陷」⇒ **答：不是缺陷，是既有占位约定**。
- **处置**：`Well` 归勘-2；占位值清理挂内容批（`10` 能力表 `store` 能力落地时）。

---

## 三、2 条判定

### 判-1 ✅ 认可 §5-1 的口径扩张（追加迁移 `warehousePaths`）—— **裁 A**

- **理由**：判据 2 要求「只改一行数据即改收什么」；若 `warehousePaths` 留空 ⇒ `WarehousePaths.Normalize` 把空判为通用仓 `res` ⇒ **7 栋从"收一种"变"收全部"**（行为漂移，且 `WarehouseRegistry.FindNearestAvailable` 落点全变）。
- 执行端按**逐字复刻 `BuildingFactory.AttachComponents` 挂仓条件**读旧填新 ⇒ **本端复算逐栋相符**：

| 栋 | role | rate | cap | output（实读） | 新声明 | 旧条件 |
|---|---|---|---|---|---|---|
| Warehouse | 2 Economy | 0 | 40 | Wood(2) | `res_material.wood` | econ ✔ |
| Granary | 2 Economy | 0 | 60 | Food(3) | `res_food.grain` | econ ✔ |
| Blacksmith | 1 | 0.5 | 250 | Metal(9) | `res_material.metal` | prod ✔ |
| farm | 1 | 2 | 100 | Food(3) | `res_food.grain` | prod ✔ |
| quarry | 1 | 5 | 100 | Stone(1) | `res_material.stone` | prod ✔ |
| AdvancedStorage | 2 Economy | 1 | 400 | Wood(2) | `res_material.wood` | prod ✔ |
| Well | 4 | 4 | 0 | Gold(0) | `res_currency.gold` | prod ✔（死仓·勘-2） |

- ⚠️ **注意**：那 33 栋空声明建筑**按旧条件本就不挂仓**（`econStorageOnly` 要求 `role==Economy`、`prod` 要求 `rate>0`）⇒ 空声明**不产生行为漂移** ✔ ⇒ 本片的追加迁移**范围恰为必需的 6 栋 ＋ 无害 1 栋**。
- **⇒ 裁 A（认可）**。因未先报裁而自行执行，**记一次流程提醒**（本片任务书 §五要求「不动行为做不下去 ⇒ 停下报」；执行端**已在本报告 §五-1 主动上报**，视为合规，不追责）。

### 判-2 ✅ `Warehouse.outputResource=Wood` 属**真内容缺陷**

- `Warehouse` 定位＝通用仓库，而 `outputResource=Wood` 让它变成**只收木**（`09` §5.3 目标态应为 `res_material`／`res`）。
- **处置**：**判为内容缺陷**，归**内容批**改那一行数据（**不牵动代码** —— 恰是判据 1 的收益兑现）。

---

## 四、⭐ 新发现潜伏缺陷（本片引入 · 当前不可达）

**`WarehousePanel.RebuildStorageList:131`** —— 容量列重复计入：

```csharp
foreach (var type in ResourceCatalog.AllTypes) {
    if (!storage.Accepts(type)) continue;
    if (totals.TryGetValue(type, out var t))
        totals[type] = (t.stored + storage.GetAmount(type), t.cap + storage.capacity);  // ← 每个可接受资源都加一次整仓容量
    ...
}
```

- 旧代码以 `ResourceType` 为键 ⇒ 每仓天然 1 行 ⇒ **无此问题**；本片改为「按可接受资源展开」⇒ 通用仓 `res` 会把**同一整仓容量累加 13 次** ⇒ 容量列虚增。
- **当前不可达**（三条证据）：
  1. 挂仓的 **8 栋**（`Granary`／`Warehouse`／`AdvancedStorage`／`Blacksmith`／`SiegeWorkshop`／`Well`／`farm`／`quarry`）**全为专属/分类声明**（非空且非裸 `res`）⇒ 每仓只展开 1 行 ⇒ 不触发累加；
  2. 33 栋空声明者**按旧条件不挂仓** ⇒ 无 `StorageComponent` 进面板（⚠️ `market` 亦不挂 —— `outRes=Gold` 被 DZ-078 排除）；
  3. `TreasureVault` 容器挂**主城子物体** ⇒ `b.GetComponent<StorageComponent>()`（只查自身）**取不到**。
- **处置**：**不阻塞本片**。登记 **DZ 缺陷**，归 **`M1-B`**（账本读口重评时一并修 —— 该片本就要碰面板读口）。

---

## 五、⏭️ 「未跑 in-game 冒烟」裁决

**不另派冒烟。** 理由：

1. 本片**纯结构改造**，判据 1~4/6 均为 Edit Mode 单元级**可复现读数**，冒烟增量有限；
2. `GameScene` 首启会牵动地图生成（注入 `System.Random` ⇒ 新增掷点整图变样）⇒ **冒烟成本高、对照价值低**（本片未动生成管线，属噪声）；
3. **合并**：验收 **`M1-B`** 时跑一次同局冒烟 —— 那时才有账本读口可验，冒烟才有判据。

---

## 六、验收三问（策划端自答）

1. **为什么会发生**：`warehousePaths` 的口径扩张，根因是**签发侧未预判**「新字段无同名旧载体 ⇒ 必需一次性填值」——任务书 §五只写了「不动行为做不下去 ⇒ 停下报」，未把 `warehousePaths` 列入交付物。
2. **单次失误 or 流程漏洞**：**流程漏洞**（签发侧）⇒ 新立正向惯例：**引入新数据字段时，签发侧必须同步给出「旧值如何回填」的迁移口径**（不论是否 `[FormerlySerializedAs]` 可覆盖）。
3. **教训库缺条目 or 没查**：**缺条目** ⇒ 新立 **`L-42`**：「`[FormerlySerializedAs]` 只能覆盖**改名**，覆盖不了**语义被新字段接管**」—— 判别法＝问「旧字段的值是否仍需在新字段里表达？」是 ⇒ 必须显式迁移。

---

## 七、落码口径落实核对（`D789` 补-1~补-5）

| 口径 | 落实 | 本端核对 |
|---|---|---|
| 补-1 `IWarehouse` 只需 `Query` 改 | ✅ | 直读：`CanTake/Take/Deposit/Transform` 签名与改前逐字相同 |
| 补-2 `FindNearestAvailable` 判据改 `CanAccept` | ✅ | 直读 diff：`resourceType != type \|\| capacity <= storedAmount` ⇒ `!Accepts(type) \|\| CanAccept(type) <= 0` |
| 补-3 缺省＝0 / 全 0 ⇒ 空列表 | ✅ | `ResourceList.Add(t,0)` 早返回；`Empty` 用 `Array.Empty` ⇒ `items != null` |
| 补-4 增列迁移核对表 | ✅ | `M1A_资产迁移核对表.txt` 88 行在场（工程根） |
| 补-5 `WorkerInventory` 过渡态标注 | ✅ | 类注释已标「内部仍是单资源」＋ §五-3 十一项假设点清单 |

---

## 八、红线核对

| 红线 | 本端复核 |
|---|---|
| ⛔ `GridTypes.cs` | ✅ 未碰（`git status` 无该文件） |
| ⛔ 地图四门 | ✅ 未碰 |
| ⛔ 存档迁移脚本 | ✅ 未写（`BuildingSaveData`/`ChestSaveData` 直接换代，`D788` §4） |
| ⛔ 双轨开关 | ✅ 无 `useXxx`；旧结构**删净**（非保留） |
| ⛔ 训练仓 | ✅ 未碰（`ai决策大脑强化训练/` 无改动） |
| ⛔ 不碰代码与资产（策划端红线） | ✅ 本端**零代码/零资产动**（仅读 + 落档） |

---

## 九、执行端下串

1. **本笔不 commit**（M1-A 改动仍留在工作区）⇒ 请按以下补正后**一并 commit**：
   - 报告 §四 勘正 `inv.carriedType` **8 处**行号（`734/736/739/742/746/751/802/808`）＋标注 `742` 属存储侧；
   - 报告 §五-1 补注 `Well` 为**死仓**（`ProducerComponent.cs:69-76` 早返回）⇒ 7 栋改述为「6 必需 ＋ 1 无害」；
   - 报告 §五-1 补注「`Well.outputResource=Gold` ＝ **既有占位约定**（`ProducerComponent.cs:15/38/39`），非内容缺陷」。
2. **新增 1 条 DZ**：`WarehousePanel:131` 容量重复计入（潜伏 · 归 `M1-B`）。
3. **新增 1 条 DZ**：`AdvancedStorage` 双 `StorageComponent` 重复挂载（既有 · 勘-3）。
4. **不做**：不跑冒烟（见 §五）；不写迁移脚本；不加开关。
5. 落盘后回执，本端派 **`M1-B`**（账本 ＋ 读口 ＋ 上述两条 DZ）。

---

## 十、落盘

- 台账 §九十五（本条）
- `_编号登记.md` 在途区 `HH.313` 状态行
- `_任务队列.md` M1-A 行
- `_策划教训库.md` 新立 **`L-42`**
- `.workbuddy/memory/2026-09-20.md`
