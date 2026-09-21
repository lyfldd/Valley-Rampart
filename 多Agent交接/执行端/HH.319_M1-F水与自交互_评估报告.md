# HH.319 · M1-F「水与自交互」· 只读评估 ＋ 报裁报告

- 任务号：`HH.319` · `M1-F`（`M1` 第六子片 · 承 `M1-A`～`M1-E` ⇒ 完成度 5/7）
- 差异：`09#44`（`:490`）＋ `09` §4.3 D 组（`:198-208`）＋ `09` §十五（`:531-593`）
- 本轮性质：**只读评估 ＋ 报裁**（⛔ `Assets/**` 一行未动 · ⛔ 未开工 · ⛔ 未 push · ⛔ 未代提交策划端账本）
- 日期：2026-09-21 · 执行端（TraeCode）

---

## 〇 结论速览（`Q1`~`Q9` 建议表 ＋ 总判断）

| # | 议题 | ⭐ 本端建议 | 牵动资产/场景 | 代价 |
|---|---|---|---|---|
| `Q1` | `ResourceType.Water` 落码 | **末尾追加为第 14 项**（枚举值 13）＋ `ResourceCatalog` 加一行 ⇒ ⚠️ **"两处"不是全部代价**（另有 6 类连带面 · 见 §四-Q1） | ⚠️ `Well.asset` 立即生效 | 见 §四-Q1 表 |
| `Q2` | 水的体积 | ⭐ **1**（⛔ 非 0）—— 保现状「井满 100 停产」语义；体积 0 会新增**第二个**容量特例 | ⚠️ 无（表值）；农场仓容量被水挤占属数值面 | 表一行 |
| `Q3` | `res_fluid.water` 定案 | ⭐ **本片定案（去 ⏳）**；水井保持全标签（已写）；农场**加 `res_fluid.water`**（⛔ 不用族前缀 `res_fluid`） | ⚠️ `farm.asset:32-33` 必改 | 资产一行 |
| `Q4` | 整数化的类型面 | ⭐ **`waterCarryAmount`／`waterThreshold` 双双改 `int`**（`ProducerConfig.rate` 保持 float ⇒ 速率≠量） | ⚠️ `GameScene.unity:2065`（值 `10` 兼容 ⇒ 零值变） | 2 字段 + 场景重序列化 |
| `Q5` | 产量表达 | ⭐ **⛔ 不改 `well.asset` schema** —— 用现成 `rate`（点/秒）：「每 N 秒 1 点」⇔ `rate = 1/N`；`rate=4` 保持＝现状逐位一致 | ⛔ 无（本片不动数值） | 零 |
| `Q6` | 两枚举去留 | ⭐ **`KingdomDestType.WaterNetwork` 退役（改为死值占位）／`KingdomTaskType.WaterHaul` 保留**（换 args 语义为「取水→送仓」两段式） | 无 | 见 §四-Q6 |
| `Q7` | 与 `U-11` 边界 | ⭐ **本片只做"删＋回调"，记账随 `U-11`**；⚠️ 但须把「吃」钉为 `U-11` **第一出口**（本片引入的新消亡口） | 无 | 注释位 + 声明 |
| `Q8` | `U-12` 两问 | ① **非兽人死亡不掉背包 ＝"缺口"**（契约 `09` §9.7 说可掉）⇒ 本片**只清不落**（最小）／泛化留掉落口径批；② `ResetForReuse` 补 `WorkerInventory.Clear()` | 无 | ≈ 6 行 |
| `Q9` | 本片边界 | ⭐ **只做「吃 ＋ 自交互工具框架」**；穿/脱装备 与 用工具 ⇒ **留后片**（`res_equipment` 标 🔸 待实现 ＋ 装备位＝新结构） | 无 | 面收窄 |

**⚠️ 三处会牵动 `.asset`/`.unity` 的落点（施工前必须一次裁清）**：
1. `Well.asset:32-33` `warehousePaths: [res_fluid.water]` —— `Q1` 落枚举 ⇒ **立即生效**（水井仓开始收水）；
2. `farm.asset:32-33` `warehousePaths: [res_food.grain]` —— **不收水** ⇒ `Q2/Q3` 定案后必改，否则水搬不进农场；
3. `GameScene.unity:2065` `waterCarryAmount: 10` —— `Q4` 改 `int` 会触该文件（值兼容）。

**一个总判断**：本片是 `M1` 中**行为变更最大**的一片 —— 但**结构改动集中在三处已有先例的复用**：
① 水的"仓化"＝`StorageComponent`（`M1-A` 已铺好）＋ 资产两行；
② 水的"搬运化"＝照 `M1-C` 件1 `HaulToSiteArgs`（取货仓 → 目的地仓）**同形复用**；
③ NPC 自交互＝照 `05` 交互层形式（两端 ＋ 回调）＋ `IWarehouse` 已有 CRUD。
⇒ 真正的**新机制 ≈ 0**；风险全在**行为变更的公示与口径**（水要人搬、吃要先看背包）。

---

## 一 · 契约回读（✅ 复核零偏差）

`09_资源与仓库.md` 逐条回读（⛔ 不引二手）：

| # | 契约 | 复核 |
|---|---|---|
| `#44` | `WaterNetwork` 独立体系 ⇒ **水按资源处理（普通仓 ＋ 工人搬 ＋ 整数）** | ✅ `:490` |
| D-① | ⭐ 取消"网"容器形态 ⇒ 水进**普通仓**（水井有仓、农场有仓）⇒ 全层只剩一种容器 | ✅ `:206` |
| D-② | ⭐ 水流转 ＝ **水井仓 →（工人搬）→ 农场仓**；⚠️ **行为变更**（农场会因缺人搬水而停产） | ✅ `:207` |
| D-③ | ✅ **量整数化（已定）**：消耗 2 点/次；产量用「每 N 秒产 1 点」表达；⛔ 不保留连续量特例 | ✅ `:208` |
| D-标签 | `res_fluid.water` **⏳ 标签待定** | ✅ `:202`（`Q3`） |
| §15.1 | 载体＝`WorkerInventory`（单资源 ⇒ ⭐ 多资源容器）；存档 v5 两标量 ⇒ 改字典 | ✅ `:539`／`:542` |
| §15.2 | 「自交互」＝同一套形式（交互者＋被交互者＋各自回调）；⛔ **不进 `InteractionManager`** | ✅ `:551-563` |
| §15.3 | 三动作全部复用 §七 CRUD：吃＝**§7.2 删＋§十-5 消亡记账**（⭐ 吃＝「删＋消亡」**不是转移**）／穿脱＝§7.4 转移／用工具＝同"吃" | ✅ `:565-573` |
| §15.4 | 容量：Porter 20／Worker 10／士兵 10／其他 10；保留 `carryCapMul`；⚠️ 装备也占容量（体积 1） | ✅ `:575-585` |
| §15.5 | ⚠️ 代价说明：现状＝**系统替 NPC 吃** ⇒ ✅ 已定「分两步」：**第一步＝通路先通**（仓多资源化＋自交互就位；吃走**背包优先、国库兜底**）⇒ **零行为风险**；第二步＝取食任务 | ✅ `:587-593` ⇒ ⭐ **本片范围＝第一步** |
| §十-4 | ⭐ **「单位一律带仓」** —— 现状"缺了才补挂"（`GetOrAddInventory`）⇒ 改为一律带 | ✅ `:452` ⇒ ⚠️ **任务书未列 · 本端列报（见 §六-1）** |
| §十-5 | 消亡记账（含金）＝ 事件＋日志 | ✅ `:453` ⇒ `Q7` |

---

## 二 · 现状实读（任务书 §二 逐条复核 ＋ 勘正）

### 2.1 水（现独立体系）

| # | 任务书说法 | 本端实读 | 判定 |
|---|---|---|---|
| 1 | `WaterNetwork.cs` 165 行／`Singleton, ISaveable`／capacity 100／`_stored`＋`_aiStoredByKingdom` | ✅ 逐行一致：`:19` `Singleton<WaterNetwork>, ISaveable`；`:25` `capacity=100`；`:28` `_stored`；`:31` `_aiStoredByKingdom`；`:65 AddWater(float,int)`／`:89 ConsumeWater(float,int)`／`:41 IsBucketFull`／`:48 GetStored`；`:150-164` `WaterNetworkSaveData`（v2）；`:21 SaveId="WaterNetwork"`；`:22 LoadPhase=Global` | ✅ |
| 2 | `ProducerComponent`：`TickWaterToNetwork :105-116`／`TryConsumeFarmWater :122-131`／`:38-39` 水井特判 | ✅ 一致；`ProductionSystem.cs:36` 每秒遍历调 `Tick()`；`:108` 桶满停产；`:110 FloorToInt`＋`:113` 累计器扣减（**已整数产出**）；`:127 ConsumeWater(2f, kingdomId)`；`:129` 缺水气泡 | ✅ |
| 3 | `TaskScheduler`：WaterHaul case `:657-664`／`KingdomDestType.WaterNetwork :1021`／水源 `:1067`／`waterCarryAmount :47` | ✅ 一致；⭐ **勘正见 2.3-①**（水源解析结果**未被消费**） | ⚠️ |
| 4 | `Building.cs`：`waterThreshold :1461`／农场缺水判据 `:1544` ⇒ 派 WaterHaul＋destType `:1547` | ✅ 逐行一致 | ✅ |
| 5 | `KingdomTask.cs:11 WaterNetwork`（第 4 项）／`WorldLifecycle.cs:37 ResetState` | ✅ 一致（枚举项序：`None,Treasury,NearestWarehouse,WaterNetwork,SpecificBuilding,UnitMagazine`）；`KingdonTaskType.WaterHaul` 定义在 `TaskPriorityConfig.cs:43`（⛔ 非 `KingdomTask.cs`） | ✅（附注） |
| 6 | Editor 引用面 8 文件 | ✅ 一致 ＋ 本端补 1（见 2.3-②）：`TestFixtureApi:92-98/162`／`Valley2_17_Smoke_11:58-72`／`Valley2_17_Smoke_2b:133-138`／`Valley_HH73_Smoke_Water`（8 处读写）／`Valley_HH76_Smoke:56/73`／`Valley_P1_Observer:66`（日志过滤串）／`Valley_TestHarnessDemo:129/137`／`R3_SupplyChain:97` | ✅ |

### 2.2 NPC 自仓

| # | 任务书说法 | 本端实读 | 判定 |
|---|---|---|---|
| 1 | `WorkerInventory.cs` 95 行；`carriedType/carriedAmount` public＋`[Tooltip]` `:18-22`；API `:25/:28/:32/:48/:62/:72/:79/:81/:90/:93` | ✅ 逐行一致（行号零偏移） | ✅ |
| 2 | `TaskScheduler` 对 `inv` 使用面 28 处 | ✅ 逐点复核见 §三-3（本端整编为 **30 处**·含 2 处任务书未列） | ⚠️ |
| 3 | `UnitController.cs` 1835 行／CRLF；v5 `SaveState:492-494`／`LoadState:545-551`／`UnitSaveData:1825-1827`；`ResetForReuse:332-360` 无 `WorkerInventory` | ✅ 逐行一致；`:459 GetOrAddInventory`（缺了才补挂）；⚠️ **CRLF 需复核**（本端未做编码探测 ⇒ 施工时先测，见 §三-4） | ✅ |
| 4 | `SatietySystem`：`OnNewDay :126+`；进食段 `:210-223` | ✅ 一致；入口 `DayCycleSettlement.cs:54-55`（日结步骤 5，首项）；AI 侧扣款 `:220 kingdom.Spend(...)` | ✅ |

### 2.3 ⭐ 勘正与新发现（任务书未列 · 5 条）

**① ⭐⭐ `WaterHaul` 的"挑水"是**表演**：水源解析结果未被消费**
- `Building.TryAdvertiseTask:1546` 发布时 **`task.source = 农场自身`**；
- `ResolveDest:1021-1023` 把 `destPos` 解析为**最近水井**（`ResolveWaterSource:1068-1082`）；
- 但 `UpdateAssignedTasks:459-529` 的 `Working → MovingToDest` **只有三个分支**（`Transport`／`AmmoReload`／`Build+HaulToSiteArgs`），**WaterHaul 走 `else ⇒ Complete`**（`:523-527`）；
- ⇒ `Complete:575` ⇒ `ExecuteCompletion:657-664` ⇒ **`AddWater(waterCarryAmount, kingdomId)` 直接入桶**。
- ⇒ **实读结论**：工人走到**农场**站 2 秒，水**凭空**出现在该国桶里；`destPos`（水井）**从未被走到**、水井**从不在流程中**。
- ⇒ ⭐ 这抬高了本片的"行为变更"幅度：不是"水从自动变人工搬运"，而是**从"半假搬运"变"真搬运"**（要新增装载段＋卸货段＋真终点）。

**② Editor 引用面补 1：`Valley_HH73_Smoke_Water.cs`（8 处 `GetStored/ConsumeWater/AddWater`）**＋ `R3_SupplyChain.cs:97` 审计器把水标为「ProducerComponent(水→隐藏桶 WaterNetwork；**非 ResourceType 桶**)」⇒ `Q1` 落枚举后该行文字变假 ⇒ 须勘正。

**③ ⭐ 水井/农场的"仓"既存且已挂（任务 2 的答案·实读坐实）**
- 挂仓判据＝`BuildingFactory.AttachComponents:214-238`：`econStorageOnly`（rate≤0＋cap>0＋Economy＋非金）或 **`rate>0 && kind==Resource && !isResourceNode`** ⇒ 挂 `StorageComponent.Init`（:231）；
- `Well.asset:35-38` `producer{kind:0, rate:4, capacity:0}` ⇒ 命中第二条 ⇒ **水井挂仓**；`RefreshCapacity:100-102`：`capacity>0 ? … : 100` ⇒ **水井仓＝100**（⭐ 与 `WaterNetwork.capacity=100` **数值巧合相等** ⇒ `Q2` 的关键论据）；
- `farm.asset:35-38` `{kind:0, rate:2, capacity:100}` ⇒ **农场挂仓**（cap 100）；
- ⇒ **"水井有仓、农场有仓"在 `M1-A` 之后已结构性成立** —— 缺的只是**标签**（`Q3`）与**水的搬运链**。

**④ ⭐ `_isWell` 特判有**第二职责**：免工自产**
- `ProducerComponent.Tick:68-74`：`_isWell` ⇒ `TickWaterToNetwork` **早返回**（不判 `HasWorkerAssigned`）；
- `Tick:79`：通用主产**要求 `HasWorkerAssigned`**（无工人不产）；
- ⇒ 若简单删 `_isWell` 让水井走通用分支 ⇒ 水井会变成"**需工人才能产水**"（行为变化，且与水井"自动产水"的历史语义冲突）；
- ⇒ ⭐ `Q1`/施工须钉：**"水井免工自产"是否保留**（本端建议**保留**，但把落点从"入网"改为"入本仓"）；`_isWell` 现还被 `Building.TryAdvertiseTask:1512` 用于"水井不派生产任务"⇒ 同一判据两用。

**⑤ `Well.droppable` 未显式声明 ⇒ 默认 `true`**
- `StorageComponent.droppable`（`M1-D` 件4）默认 true；`Well.asset` **无 `droppable: 0` 行** ⇒ 水井被打掉 ⇒ **仓里的水掉箱**。
- ⚠️ 09 未规定"水井被打掉水怎么办"（与 `M1-E` 的"国库掉金"论证同型，但水是**隐藏资源**）⇒ 列报待裁（§六-3）。

---

## 三 · 任务 1~6 逐条结论（依据 `file:line`）

### 3.1 任务 1 —— `WaterNetwork` 完整生产读写面 ＋ 退役替代落点

**读写面全扫（16 文件 · 生产 6 ＋ Editor 9 ＋ 注释 3）**

| 向 | 落点 | 说明 | 本片 |
|---|---|---|---|
| 写·产 | `ProducerComponent.TickWaterToNetwork:105-116` | `IsBucketFull` 拒 ⇒ 累计器 ＋ `FloorToInt` ⇒ `AddWater` | ⭐ 改 |
| 写·搬 | `TaskScheduler.ExecuteCompletion:657-664` | WaterHaul 完成 ⇒ `AddWater(waterCarryAmount, kingdomId)` | ⭐ 改 |
| 写·耗 | `ProducerComponent.TryConsumeFarmWater:122-131` | 每秒 1 次 `ConsumeWater(2f, kid)`；失败 ⇒ 停产＋气泡 | ⭐ 改 |
| 写·重置 | `WorldLifecycle.cs:37` | 换局 `ResetState()`（HH.92 M10 清偿） | ⭐ 删 |
| 写·档 | `WaterNetwork.SaveState:107-120`／`LoadState:122-139` | `WaterNetworkSaveData`（stored/capacity/aiBuckets） | ⭐ 删 |
| 写·测试 | `TestFixtureApi:92-98`（AI 桶注满）；`Valley_HH73_Smoke_Water:160/165` | Edit/Play 探针注水 | ⭐ 改 |
| 读·判 | `WaterNetwork.IsBucketFull:41`（`ProducerComponent:108`）／`IsFull:36`／`Stored:34` | 桶满停产／玩家读口 | ⭐ 删 |
| 读·判 | `Building.TryAdvertiseTask:1544` `GetStored(kingdomId) < waterThreshold` | 农场缺水判据 | ⭐ 改 |
| 读·观 | `GetStored:48` ×Editor 8 文件（`TestFixtureApi:162`／`Smoke_11:65…`／`Smoke_2b:136/138`／`HH73` ×6／`HH76:73`／`TestHarnessDemo:129/137`） | 探针读数 | ⭐ 改（编译面） |
| 读·日志 | `Valley_P1_Observer:66` `"[WaterNetwork]"` 过滤串 | 观测台日志锚 | ⭐ 改 |
| 读·审计 | `R3_SupplyChain.cs:97` | ChainAudit 把水列为"非 ResourceType 桶" | ⭐ 改 |
| 注释 | `KingdomState.cs:50`／`AIDebugSpawnController.cs:430-431`／`Building.cs:1477` | 历史叙述 | ⭐ 勘正 |

**退役替代落点清单（谁产／存哪／谁耗）**

| 职责 | 现落点 | ⭐ 退役后落点 | 机制复用 |
|---|---|---|---|
| **产** | `TickWaterToNetwork` → 入网 | `ProducerComponent.Tick` ⇒ **`_storage.Add(Water, n)`**（本仓） | 主产累计器（`_mainAccumulator`）**已是整数产出** ⇒ 仅换落点 |
| **存** | `_stored`／`_aiStoredByKingdom` | **水井自己的 `StorageComponent`**（每国自己的井 ⇒ **AI 桶天然消解**） | `M1-A` 多资源容器 ＋ `BuildingSaveData.storageContents` |
| **搬** | `WaterHaul` ⇒ `AddWater`（凭空） | **水井仓 →背包→ 农场仓**（两段式） | ⭐ `M1-C` 件1 `HaulToSiteArgs`（`pickup` ＋ `destPos` ＋ `need`）**同形复用** |
| **耗** | `ConsumeWater(2f, kid)` | **农场仓 `CanTake/Take(Water,2)`** | §7.2 先问后扣 |
| **判** | `GetStored(kid) < waterThreshold` | **农场仓 `GetAmount(Water) < waterThreshold`** | `StorageComponent.GetAmount` |
| **档** | `WaterNetworkSaveData`（Global） | 随 `BuildingSaveData.storageContents` | ⭐ **零新存档面** |
| **重置** | `WorldLifecycle:37` | 删（仓随建筑重建） | — |

⭐ **可整体退役**：`WaterNetwork.cs`（165 行）＋ `WaterNetworkSaveData` ＋ `WorldLifecycle:37` ＋ 9 个 Editor 引用 ＋ 3 处注释。

### 3.2 任务 2 —— 水井/农场是否已有仓 ＋ 谁负责挂

- **已有仓** ✅ 二者皆有（实读依据见 §二 2.3-③）；
- **谁挂**：`BuildingFactory.AttachComponents:207-254`（判据）+ `StorageComponent.Init:63-72`（`SetDeclaredPaths(def.warehousePaths)` ＋ `droppable` ＋ `RefreshCapacity` ＋ `WarehouseRegistry.Register`）；⛔ 非 prefab 挂载（`Building` 与仓皆 `AddComponent` · `BuildingFactory:113/219/231`）；
- **标签现状**：`Well = [res_fluid.water]`（`M1-E` 已改，靠"枚举未落"保持无害）／`farm = [res_food.grain]`（**不收水**）；
- ⚠️ **容量后果**（`Q2`）：水体积 1 ⇒ 农场仓 100 格内"水＋粮"共存；水体积 0 ⇒ 农场仓水不占格，但**水井仓无上限**（水井永不因满停产 ⇒ 与现状 `capacity=100` 语义相悖）。

### 3.3 任务 3 —— `WorkerInventory` 多资源化波及面全扫（逐点标"本片是否需改"）

**A. `TaskScheduler.cs` · 30 处（任务书 28 ＋ 本端补 2）**

| # | 落点 | 用法 | 类别 | 本片需改？ |
|---|---|---|---|---|
| 1 | `:738-746 GetInventory` | 取/补挂 | 载体 | ⛔ 零改 |
| 2 | `:679 TryStore`（Gather 完成） | 入包 | 装载 | ⭐ **改**（需按量/容量，多资源语义） |
| 3 | `:681 AddGatherOverflow`（溢出） | 兜底 | 溢出 | ⭐ 改（溢出量口径改逐条目） |
| 4 | `:758 IsEmpty`（箱源就地卸空） | 判空 | 判定 | ⛔ 语义不变（空＝无条目） |
| 5 | `:775 TryStore`（`LoadInventoryFromSource`） | 入包 | 装载 | ⭐ 改（箱内容物逐条目 ⇒ 一次一种 or 全收） |
| 6 | `:786 IsEmpty`／`:787 UnloadAll`（`UnloadInventory`） | 卸货 | 卸货 | ⭐ **改**（`UnloadAll` 单 int ⇒ 需逐条目卸） |
| 7 | `:800/:804/:807/:810/:814/:819 carriedType`（副产分流 ＋ 卸货落点） | 类型直读 | 语义核心 | ⭐ **改**（≥6 处：卸货路由必须逐条目） |
| 8 | `:837 carriedType`（`ResolveChestDest` 落点解析） | 类型直读 | 解析 | ⭐ 改 |
| 9 | `:864 IsEmpty／carriedType`（`LoadSiteMaterials` 混装防护） | 判定 | 判定 | ⭐ **改**（"混装防护"逻辑本身消失 ⇒ 可整段删） |
| 10 | `:873 TryStore`（`LoadSiteMaterials`） | 入包 | 装载 | ⭐ 改（多资源不再"类型不符恒拒"） |
| 11 | `:888 IsEmpty／:889 carriedType／:890 UnloadAll`（`DepositToSite`） | 卸货 | 卸货 | ⭐ 改 |
| 12 | `:924 IsFull`（`LoadAmmoToBackpack`） | 判满 | 判定 | ⭐ 改（一条容量线满） |
| 13 | `:943 TryStore`（弹药装载） | 入包 | 装载 | ⭐ 改 |
| 14 | `:957 IsEmpty／:961 UnloadAll／:962/:968 carriedType`（`UnloadAmmoToMagazine`） | 卸货 | 卸货 | ⭐ 改 |
| 15 | ⭐ **本端补**：`WaterHaul` 新增装载段（Working 分支 `:523-527` else ⇒ 需插 `case`） | — | 新增 | ⭐ **新增** |
| 16 | ⭐ **本端补**：`MovingToDest:538-544` 需插"送水入农场仓"分支 | — | 新增 | ⭐ **新增** |

**B. `TaskScheduler` 之外（⭐ 任务书未列 · 3 文件）**

| # | 落点 | 用法 | 本片需改？ |
|---|---|---|---|
| 1 | `DamageSystem.TrySpawnOrcLoot:640-657`（`M1-D` 刚落地） | 死者背包掉箱：`inv.carriedType`／`inv.carriedAmount`／`inv.carriedAmount=0` | ⭐ **必改**（多资源 ⇒ 掉 `Query()` 全量；`ResourceList.Of` 组装改逐条目） |
| 2 | `UnitController:492-494／545-551`（v5 代理） | 存/读两标量 | ⭐ **必改**（`Q8`/§三-4） |
| 3 | `WorkerInventory` 自身 `:25/:28/:35/:51-57/:64-67/:75/:79/:84-85` | 单资源实现内部 | ⭐ **重写为多资源**（照 `StorageComponent` 样板） |
| 4 | Editor：`Valley_HH316_U2Smoke:649-654`（`carriedType` 直读）／`Valley_HH317_M1D_Probe`／`Valley_HH315_M1C_Smoke` | 探针 | ⭐ 编译面必改（探针口径随契约演进 ⇒ 声明即可） |
| 5 | `WorkerTask.GetCarryAmount:30-35`／`StorageComponent.GetCarryAmount:297-305`（按类型携带量） | 携带量查表 | ⚠️ 若容量改"一条线（职业）" ⇒ 两处须改（`Q`/§15.4） |

**C. 与 `TaskScheduler` 的迁移方案（本端建议）**

> ⭐ **照 `StorageComponent` 样板重写 `WorkerInventory` 内部，但保留 7 个自有 API 的"兼容面"**：
> 1. `_items` 字典 ＋ 一条容量线（`JobCapacity`）＋ 体积（`ResourceCatalog.VolumeOf`）；
> 2. `IsEmpty`（无条目）／`IsFull`（`UsedSpace >= Capacity`）语义保持；
> 3. ⭐ **`TryStore`／`Deposit`／`Take`／`Query` 直接可用**；`UnloadAll()` ⇒ **改返回 `ResourceList`**（调用面 4 处：`:787/:890/:961` ＋ 探针 ⇒ 逐点改）；
> 4. ⭐ **`carriedType`／`carriedAmount` 收窄为"首个条目"只读门面**（`=> Query()` 首条）—— 但 ⚠️ **`DamageSystem:657` 的 `inv.carriedAmount = 0`（写）与 `UnitController:549` 的写** 必须改为 `Clear()`／`Restore(ResourceList)`；
> 5. ⭐ **卸货段（`UnloadInventory` 等）改逐条目循环**：`foreach (var e in inv.Query()) { 找仓 + Add + 溢出分流 }`；
> 6. ⭐ **装载段（`LoadInventoryFromSource`/`LoadSiteMaterials`/`LoadAmmoToBackpack`）** 加"单条目装载"语义（一次搬一种资源，`need` 上限）—— 与现有 `PrimaryStoredType()` 过渡读口一致。
> 7. ⚠️ **必须同时处理"混装防护"退役**（`:758/:864` 的两处"先卸空再取货"）：多资源背包下不再需要 ⇒ 删（否则是死代码）。

### 3.4 任务 4 —— `UnitController` v5 存档代理改字典的兼容方案

**现状**：`UnitSaveData`（:1825-1827）两个 `int` 标量；`SaveState:492-494` 写；`LoadState:545-551` 读（`data.saveDataVersion >= 5` 分支）。

| 案 | 做法 | 代价 | 本端倾向 |
|---|---|---|---|
| **甲 ⭐ 桥 ＋ 尾插字段** | `UnitSaveData` 尾插 `ResourceList inventory`（`saveDataVersion=7`）；写侧**停写**两标量（字段保占位）；读侧 `v>=7` 读新字段、`v==5/6` **建桥**（`carriedType/carriedAmount ⇒ 单条目 Restore`） | ≈ 15 行 · **零 bump**（`JsonUtility` 缺字段默认） | ⭐ **建议**（`RulerSaveData.gold` 先例＝"桥"而非弃档；`M1-A` 弃档是因结构换代，此处是**同构搬运**） |
| 乙 弃档 | 旧档背包归空（`D788` §4「未发布 ⇒ 旧档可作废」） | 0 行 | 备选 |
| 丙 直接改两标量为字典 | `UnitSaveData` 换 `ResourceAmount[]` 字段名不变 | ⚠️ 旧档 `carriedType:int` 反序列化进 `ResourceAmount[]` **会静默失败**（类型不匹配） | ⛔ 不建议 |

- ⚠️ **CRLF 复核**：任务书标 `UnitController.cs` **CRLF=1827 行 ⇒ 禁 `Edit`/`Write`，须 python 二进制按行替换**。本端未做编码探测（只读轮次），⇒ **施工第一步须先验**（`file`／`git diff --stat`／python 判 `b'\r\n'`），确认后再定工具链。
- ⚠️ `UnitSaveData` 是**每单位一条** ⇒ 新增 `ResourceList` 会增大存档；实测建议：空背包 ⇒ **不写 `items`**（`ResourceList.Empty` ⇒ `items: []`），可接受。

### 3.5 任务 5 —— 自交互工具的落点（与 `05` 形式对齐）

**`05` 形式（实读原文）**：`IInteractable.Interact(Interactor ctx) → InteractionResult`（`IInteractable.cs:8-12`）；`05` §七「执行与完成回调」＝ 条件复核 → 耗时 → 改双方 → 判进度 → **完成 ⇒ 广播（交互者，被交互者，能力）**；§五「进度栏写死」（每条交互自己定"看哪个字段、到什么值算完"）。

**§15.2 的边界**：⛔ 不进 `InteractionManager`（`InteractionManager.cs:82-123` 是**点击驱动**：射线命中 ⇒ `Interact(ctx)` ⇒ UI/动作）；⛔ 不要距离/配对。

**⭐ 本端落点提案（三件）**：

| 件 | 落点 | 内容 |
|---|---|---|
| ① **被交互者** | `WorkerInventory`（已是 `IWarehouse`） | 它就是"NPC 身体里那个仓"；三个动作 → 全部落到已有 CRUD（`CanTake/Take`＝吃/用工具；`Deposit/Take` 双向＝穿脱装备） |
| ② **自交互动作**（新） | 新增 **`NpcSelfInteraction`（静态工具类 · ⛔ 不挂组件、不进 `InteractionManager`）** | `Eat(UnitController)`＝`CanTake(Food, cost)` → `Take` → **回调 `Satiety += cfg.foodRestoreGrain`**；`UseTool(...)`；返回 `SelfInteractionResult`（对齐 `InteractionResult` 的 `kind` 形状，⛔ 不新增枚举负担） |
| ③ **"进度"（`05` §五 硬编码到底）** | 写死在动作内 | 吃："删成功一次"即完成；无耗时（现状系统替吃亦零耗时 ⇒ 本片零行为风险） |

- ⚠️ **`§十-4「单位一律带仓」**（`:452`）与本片强相关：建议**同批落地**（`GetOrAddInventory` ⇒ 单位生成时 `AddComponent`；⛔ 否则"吃"在无背包单位上退化 ⇒ 但本片走"背包优先、国库兜底" ⇒ 亦可延后）⇒ **列报待裁**（§六-1）。
- ⚠️ **与 `05` 的张力**：`05` §十 说"`KingdomTaskType` 12 项枚举 ⇒ **取消**（能力名 ＋ 交互表替代）"；本片 `Q6` 讨论的正是"已有枚举的退役"⇒ ⚠️ 两件事要区分：**`05` 是未来态（能力表）**，本片当前在**枚举态**内做最小改动 ⇒ 建议 `Q6` 按"保枚举、换语义"处理（⛔ 不提前做能力表）。

### 3.6 任务 6 —— 整数化的全量类型面（float ⇒ int 会牵动什么）

| 面 | 现值 | 现类型 | 改动 | 牵动资产/场景？ |
|---|---|---|---|---|
| `WaterNetwork.capacity:25` | 100 | int | 退役 | ✗ |
| `WaterNetwork._stored:28`／`_aiStoredByKingdom:31` | float | float | 退役（入仓 ⇒ int） | ✗ |
| `AddWater/ConsumeWater/GetStored/Stored/IsFull` | float 签名 | float | 退役 | ✗ |
| `ProducerComponent._mainAccumulator:21` | — | float | **保持 float**（速率累加器 ⇒ `FloorToInt` ⇒ 天然整数产出） | ✗ |
| `TryConsumeFarmWater:127` `ConsumeWater(2f,…)` | 2f | float 字面量 | ⇒ `Take(Water, 2)`（int） | ✗ |
| `TaskScheduler.waterCarryAmount:47` | 10f | float | ⭐ **改 int** | ⭐ **`GameScene.unity:2065`**（值 `10` ⇒ 兼容） |
| `Building.waterThreshold:1461` | 20f | float | ⭐ **改 int** | ✗（`Building` 是 `AddComponent` 运行时对象 · 场景/prefab **零引用**：本端已 grep 全 `*.{unity,prefab,asset}` ⇒ 仅 `waterCarryAmount` 一处命中） |
| `Well.asset producer.rate:37` | 4 | **float**（`ProducerConfig.rate` 通用字段） | ⛔ **不改类型**（速率≠量；`Q5`） | ⚠️ 若改数值则动资产（本端建议不动） |
| 仓内量（`StorageComponent._items`） | int | int | ✓ 已整数 | ✗ |
| `ResourceCarryConfig`（按类型携带量） | int | int | ⭐ 语义换（`§15.4` 一条容量线：20/10/10/10） ⇒ ⚠️ 该 SO 可能整体退役/改形（**牵动 `ResourceCarryConfig.asset`**） | ⚠️ **第 4 处**（本端新增列报） |

⚠️ ⭐ **第 4 处资产牵动（本端新增）**：`§15.4` 要"一条容量线（按职业）"，而现容量按**资源类型**查 `ResourceCarryConfig`（木/石/矿 10 · 粮 20 · 水晶/火油 5）⇒ 三种改法：**(甲)** SO 改"按职业容量"（`ResourceCarryConfig.asset` 改形）；**(乙)** 容量进 `NpcProfessionDef`（职业 SO 加字段 ⇒ **牵动全部职业资产**）；**(丙)** 代码常量表（违反 `so-data-driven`）。⇒ **须裁**（本端建议甲：SO 语义换代，资产一处改，符合数据驱动）。

---

## 四 · `Q1`~`Q9` 逐条建议（理由 ＋ 代价）

### `Q1` 落 `ResourceType.Water` —— "末尾追加 ＋ 两侧同步"**不是全部代价**

- **建议**：`GameEvents.cs:197` 后追加 `Water`（枚举值 13 · 保旧值稳定）＋ `ResourceCatalog.Table` 追加一行（`:53` 后）；`ResourceCatalog.cs:35` 注释勘正（现写"D 组（水）… 落码归 M1-F"）。
- ⭐ **全部代价 8 类（本端实读清单）**：
  1. 枚举 ＋1（`GameEvents.cs`）；
  2. 表 ＋1 行（`ResourceCatalog.cs` · 含 `Volume`／`DisplayName="水"`）；
  3. **`Well.asset` 声明立即生效** ⇒ 水井仓开始收水（⭐ 这正是目标；但"施工前 Well 仓是死仓"的保护伞**消失**）；
  4. **`farm.asset` 必加水的声明**（否则水搬不进农场 ⇒ `Q3`）；
  5. **9 个 Editor 文件**引用面（编译 ＋ 语义：`R3_SupplyChain` 注释变假、7 个水探针要改口径）；
  6. `WaterNetwork` 整体退役面（§3.1 表）；
  7. `TaskScheduler` 任务链改造（§3.3-A 第 15/16 条 · 新增两段）；
  8. 注释勘正 3 处（`KingdomState:50`／`AIDebugSpawnController:430`／`Building:1477`）。
- **理由**：`09` §4.1 的"两处"是**"新增一个普通资源"的最小集**；水是从**独立体系降级**而来 ⇒ 必然带退役面。⛔ 不建议把退役面拆到别片（会造成"两套水并存"⇒ 双真源，与 `M1-A` 红线同族）。
- **代价**：见上（8 类）；工时集中在 5/6/7。

### `Q2` 水的体积 —— ⭐ **1**（⛔ 非 0）

- **理由（三条）**：
  1. ⭐ **保"井满 100 停产"语义**：`Well.asset producer.capacity=0` ⇒ 仓容量 100（`RefreshCapacity:100-102`），**与现 `WaterNetwork.capacity=100` 数值相等** ⇒ 体积 1 时，"水井产满 100 ⇒ 停产"**逐位复刻现状**；体积 0 ⇒ 水井仓**无上限** ⇒ 水井**永不因满停产**（新行为）；
  2. ⭐ **⛔ 不新增第二个"体积 0"特例**：`09` §4.2 定"一切资源＝1，**仅金币＝0**"，且金 0 的论证是"钱不占仓库格"的**唯一**用例（`M1-E` `Q2` 已坐实"是设计"）⇒ 水若也 0，则"体积 0"从**特例**变**惯例** ⇒ 稀释该字段的表达力；
  3. **农场仓的挤占是"设计取舍"**（`§15.4` 明说"装备也占容量 ⇒ 这正是容量是一条线要表达的取舍"）⇒ 水占农场格同族 ⇒ **不是缺陷**。
- **⚠️ 代价／连带（须裁）**：农场仓容量 100 装"粮＋水"⇒ 粮位被占 ≤ `waterThreshold`（默认 20）；若嫌挤，改农场 `capacity` 属**数值批**（⛔ 本片不动）。
- **⚠️ 反面后果（若裁 0）**：水井仓无上限 ⇒ `Q1` 第 3 条的"立即生效"会变成"水井无限蓄水" ⇒ 需另定"井满"判据（新机制）⇒ 本端不建议。

### `Q3` `res_fluid.water` 定案 —— ⭐ **本片定案**；水井全标签保持 ＋ 农场加全标签

- **建议**：`09` §4.3 D 组 `:202` 的 ⏳ **去掉**；`Well.asset:32-33` **保持** `res_fluid.water`；`farm.asset:32-33` **加** `- res_fluid.water`（两条 Paths）。
- **理由**：① `Q1` 落枚举后 `res_fluid.water` **立即可匹配**（`ResourceCatalog.PathMatches`：段边界 `_`/`.` ⇒ `res_fluid.water` 与 `res_fluid.water` 全等 ⇒ 命中 ✓）；② 农场是**消费方**，只该收水 ⇒ 用**全标签**（若用族前缀 `res_fluid`，将来加"油/药水/奶"等液体 ⇒ 农场会误收 ⇒ 与"农场有仓"的语义不符）；③ 水井也保持全标签（只收水 ⇒ 精确）。
- ⚠️ **与 `M1-E` 的口径对照（须显式记）**：`M1-E` 给国库用**族前缀**（`res_currency` ⇒ 多币种零改），本处建议给**全标签** ⇒ 两者不矛盾（国库＝通用容器 vs 农场＝专用工位仓），但**须在报告/文档声明**避免后读困惑。
- **代价**：`farm.asset` 一行 ＋ `09` 一处去 ⏳。

### `Q4` 整数化的类型面 —— ⭐ **`waterCarryAmount`／`waterThreshold` 改 `int`**

- **建议**：`TaskScheduler.waterCarryAmount: float ⇒ int`；`Building.waterThreshold: float ⇒ int`；**`ProducerConfig.rate` 保持 float**（它是"速率"不是"量"，且是 40 栋资产共用字段 ⇒ 改类型＝全资产重序列化）。
- **理由**：① `09` §4.3 D 组"本层统一整数口径 ⇒ ⛔ 不保留连续量特例" ⇒ 两个字段都是"**量**"（搬多少/存多少判缺水）⇒ 归整数；② 改类型代价实测**极小**：`waterThreshold` 全库无资产/场景引用（本端 grep 坐实）；`waterCarryAmount` **仅** `GameScene.unity:2065` 一处，且值 `10` 对 `int` **合法** ⇒ 零值变；
  ③ 若保留 float ⇒ 留"半整数面"（后续使用者可能写 `waterCarryAmount = 7.5f`）⇒ 与口径冲突。
- **代价**：2 行类型 ＋ `GameScene.unity` 会被 Unity 重序列化（该行文本不变）；⚠️ **施工须在 Unity 打开状态下改**（避免手改 YAML）。

### `Q5` 产量表达 —— ⭐ **⛔ 不改 `well.asset` schema**（用现成 `rate`）

- **建议**：`ProducerConfig.rate`（点/秒）表达「每 N 秒产 1 点」⇔ `rate = 1f / N`；**本片保持 `rate: 4`**（＝现状 4 点/秒，行为逐位一致）；若策划要"每 N 秒 1 点"的**具体 N** ⇒ 由**数值批**改 `well.asset` 的 `rate` 一行。
- **理由**：① 现状 `TickWaterToNetwork` 已是"累计器 ＋ `FloorToInt` ⇒ 整数点产出"（`:109-114`）⇒ **整数化在产量侧已天然成立**，无需新字段；② 新增 `secondsPerUnit` 会改 `BuildingDef` schema ⇒ **牵动全 40 栋资产重序列化**（得不偿失）；③ 语义等价：`rate=0.5` 即"每 2 秒 1 点"。
- **代价**：0（若数值批要调 ⇒ 一行资产）。
- ⚠️ **本端提请钉死**：D 组 `:208` 的「每 N 秒产 1 点」是**表达方式**（避免"每秒 0.x"的读法），⛔ 不等于要求"新增字段/改 schema"。

### `Q6` 两枚举去留 —— ⭐ `KingdomDestType.WaterNetwork` 退役（占位）／`KingdomTaskType.WaterHaul` 保留

- **现状**：`KingdomDestType.WaterNetwork`（`KingdomTask.cs:11` · 第 4 项）**只被 WaterHaul 用**（`Building:1547` 写 ＋ `TaskScheduler:1021` 解析）⇒ 水仓化后语义（"水网"这个对象）**不存在** ⇒ 该值**必退役**；⚠️ 但 `KingdomDestType` 是**序列化稳定面**（枚举序 5 项 ⇒ 删第 4 项会让后 2 项**整体左移**）；
- **建议**：**(甲) ⭐ 保留枚举项作为"死值"占位**（⛔ 不删、不重编号；注释标"`M1-F` 起退役 · 保留占位保序列化稳定"），代码侧不再写入/解析；**(乙)** 删项（⇒ 序列化面左移，若存档含 `destType` 值会错位）⇒ ⛔ 不建议。
- **`KingdomTaskType.WaterHaul` 保留**：理由 ① 它表达"搬水"这一**玩法动作**（`TaskPriorityConfig` 里是 C 档 ⇒ 与搬运 B 档区分 ⇒ 有语义价值）；② 换 args 类型（`HaulToSiteArgs` 同形）即可复刻 `M1-C` 的搬料链；③ ⛔ 删它则"搬水"混入 `Build`（语义污染）；⚠️ **代价**：`ResourceBiasConfig:66` 的 `WaterHaul ⇒ EcoResource.Food, enabled=false`（"水非五元"）保持有效（水仍非五元）✓。
- **代价**：`KingdomDestType` 一处注释 ＋ `Building:1547` 改（不再写 destType）＋ ⛔ 零枚举删除。

### `Q7` 与 `U-11` 的边界 —— ⭐ **本片只做"删＋回调"，记账随 `U-11`**

- **建议**：本片 `Eat` ＝ `CanTake → Take`（删）＋ `Satiety += restore`（回调）＋ **留一行 `// TODO(U-11): 消亡记账`**；⛔ 不落事件/日志；⚠️ **条件**：`U-11` 施工时**必须把"吃"列为其第一出口**（本片引入的消亡口）。
- **理由**：① `U-11` 已裁"另立独立小片"（`09` §7.2 `:289` 的消亡记账跨系统：箱到期/同格驱逐/清场/民生吃粮 ＋ 含 AI 侧）⇒ 本片若自建一套"吃的记账"，`U-11` 落地时还要回头统一格式（双改 ＋ 双真源风险）；② `§15.5` 明说第一步是"**通路先通 ＋ 零行为风险**"⇒ 加记账会引入"事件契约"这个新面（M1-F 已够重）；③ 现状"系统替吃"**也零记账** ⇒ 本片不加不构成**回归**（只是"新出口未记账"）。
- ⚠️ **风险（须声明）**：本片落地后，"吃背包粮"这条消亡**无账** ⇒ 在 `U-11` 前，经济账仍不平（与现状同性质）。
- **代价**：注释位 1 行 ＋ 交付报告显式声明 ＋ `U-11` 任务书须补"第一出口＝吃"。

### `Q8` `U-12` 两问

**① 非兽人死亡 ⇒ 背包是否掉箱**
- **实读**：`DamageSystem.TrySpawnOrcLoot:617-658`（`M1-D` 落地）**只在兽人击杀路径**（`killer.raceId == Orc`）掉背包；其余死亡 ⇒ `inv` 既不落箱也**不清空**（`:657` 的抽空只在兽人分支内）⇒ 池化复用 ⇒ **继承货物**（`U-12` 根因）。
- **契约**：`09` §9.7「NPC 背包 ✅ 可掉」＋ §九 总则"生命周期结束 ⇒ 仓变掉落箱" ⇒ **现实现是缺口**（非有意简化）。
- **本端建议（两案）**：
  - **甲 ⭐（最小 · 本片）**：`ResetForReuse` 补清背包（消池化继承）＋ 本片**不泛化掉箱**；把"非兽人死亡掉背包"作为**明确缺口**列入 `U-11`/掉落口径批（⚠️ 泛化涉及"任何死亡都掉箱" ⇒ 行为面大，且恰是 `U-11` 的"消亡"议题）。
  - **乙（更全）**：本片顺带把掉箱判据从"击杀者身份"改为"死者有仓内容"（复用 `M1-D` 的 `ChestManager`）⇒ 面大（所有死亡路径 ＋ 掉落口径全变）⇒ ⛔ 不建议挤进本片。
- **代价（甲）**：清包 ≈ 4 行；缺口须声明。

**② `ResetForReuse` 补清 `WorkerInventory`（方案）**
- **建议**：`WorkerInventory` 新增 `Clear()`（清 `_items` ＋ ⛔ 不发事件，对齐 `StorageComponent.Clear:351-355`）；在 `UnitController.ResetForReuse:332-360` **末尾**加：
  - ⚠️ 两种写法：(a) `GetComponent<WorkerInventory>()?.Clear()`（每次出池一次 `GetComponent`，可接受）；(b) `GetOrAddInventory().Clear()`（**会补挂组件** ⇒ 顺带兑现 `§十-4`"一律带仓"）⇒ ⭐ 本端倾向 **(b)**（一石二鸟，且与 `§十-4` 同向）。
- **代价**：`WorkerInventory.Clear()` ≈ 4 行 ＋ `ResetForReuse` 1 行。

### `Q9` 本片边界 —— ⭐ **只做「吃 ＋ 自交互工具框架」**

- **建议**：本片落 ① 仓多资源化（`§15.1`）／② 自交互工具（`Eat` ＋ 框架）／③ 吃走"背包优先、国库兜底"（`§15.5` 第一步）／④ 水仓化 ＋ 真搬运链（`09#44`）。
  ⛔ **不做**：穿/脱装备（`§15.3` 第二行 · 需"装备位＝单件仓"**新结构** ＋ `res_equipment` 标「🔸 待实现」）／用工具（`§15.3` 第三行 · 需工具效果系统 ⇒ 依赖未落）／取食任务（`§15.5` 第二步 · 用户已定后做）。
- **理由**：① `res_equipment` 在 `09` §4.3 明标"🔸 待实现"（不在本片差异）；② "装备位＝一个单件仓"是**新容器形态**（§15.3 表原文）⇒ 需单独设计（且 `§15.4` "装备占容量"的容量口径要先定）；③ 本片已含两个大面（水 ＋ 背包多资源化）⇒ 边界收紧才能保回归。
- **代价**：本片不含装备 ⇒ `§15.3` 三动作**只落一个**（吃）⇒ 须在交付报告声明"框架就位、动作只落吃"。

---

## 五 · 资产/场景牵动面汇总（施工前一次裁清）

| 文件 | 行 | 现值 | 本片动作 | 依据 |
|---|---|---|---|---|
| `Assets/Resources/Buildings/Well.asset` | `:32-33` | `warehousePaths: [res_fluid.water]` | **保持**（`Q1` 落枚举 ⇒ 生效 ⇒ 水井仓收水） | `Q1`/`Q3` |
| `Assets/Resources/Buildings/farm.asset` | `:32-33` | `warehousePaths: [res_food.grain]` | ⭐ **加 `- res_fluid.water`** | `Q3` |
| `Assets/Scenes/GameScene.unity` | `:2065` | `waterCarryAmount: 10` | ⭐ 类型 `float ⇒ int`（值不变 · 经 Unity 重序列化） | `Q4` |
| `Assets/Resources/Config/ResourceCarryConfig.asset` | 全 | 按**资源类型**携带量 | ⚠️ `§15.4` 改"一条容量线（按职业）" ⇒ **改形/换代**（待裁：甲/乙/丙） | §15.4 |
| `Assets/Resources/Buildings/Well.asset` | `:37` | `rate: 4` | ⛔ **不动**（数值批） | `Q5` |
| `Assets/Resources/Buildings/Well.asset` | `droppable`（缺省） | 默认 `true` | ⚠️ 待裁：井被打掉水是否掉箱 | §六-3 |

---

## 六 · 列报（超范围观察 · ⛔ 本批不改 · 请裁）

1. ⭐ **`09` §十-4「单位一律带仓」未落地**（`UnitController.GetOrAddInventory:459-464` 仍是"缺了才补挂"）⇒ 与 `Q8-②`(b) 同向 ⇒ **可搭本片**（建议搭），或独立挂账。
2. ⭐ **`U-12` 的"非兽人死亡掉背包"＝缺口**（非简化）⇒ `Q8-①`；若本片不落 ⇒ 须显式挂账到 `U-11`/掉落口径批（含"资源未记账消亡"一句）。
3. ⚠️ **`Well.droppable` 未声明** ⇒ 水井被打掉 ⇒ 仓里的水掉箱；`09` 未规定 ⇒ 请裁（建议：水井 `droppable: 0`，理由：水是"工位缓冲"不是"战利品"）。
4. ⚠️ **玩家侧水的"读口/UI"未定**：现状水是"隐藏资源"（不显示）；仓化后水在**水井仓里**（`StorageComponent`）⇒ 若无 UI 入口，玩家"看不见水"（`WarehousePanel` 会显示水井仓的水 ✓ ⇒ 结构性可见）⇒ 请确认"水是否需要专门 HUD 行"（本端建议：不需要，走仓面板即可）。
5. ⚠️ **AI 侧水仓读口**：AI 消费面读台账（`M1-E` 已确立"AI 读台账不读 Vault"）⇒ AI 的水在**仓里**、不在台账 ⇒ ⚠️ 与 `#52` 非金面（`M1-G`）同族 ⇒ 本片**不接** AI 台账读水（现状 AI 也不读水）⇒ 声明。
6. ⚠️ **`§15.4` 容量（Porter 20／Worker 10／士兵 10／其他 10）的落点**未定（`ResourceCarryConfig` 改形 vs 职业 SO 加字段 vs 代码常量）⇒ 见 §五 第 4 行 ⇒ 请裁（本端建议甲）。
7. ⭐ **`carryCapMul` 的乘数语义**：`WorkerInventory.GetCarryCapacity:32-43` 现为 `cap × carryCapMul`（四舍五入）；改"一条容量线"后**同一乘数**继续作用于该线 ✓（`§15.4` 明文保留）⇒ 确认即可。
8. ⚠️ **`DamageSystem` 的 `×1.5`（`M1-E` 刚落）与多资源掉包的关系**：现逻辑"只乘金"（`:648-652`）⇒ 多资源后 `pack` 可能含多条目 ⇒ **乘算判据须复核**（仍只乘 `Gold` 条目 ✓ 建议保持 · 施工时一并确认）。

---

## 七 · 红线自查 ＋ 停手点

| 红线 | 自查 |
|---|---|
| ⛔ 本轮 `Assets/**` 一行不动 | ✅ 全部只读（`read_file`／`search_*`／`list_dir`） |
| ⛔ 不开工 | ✅ 未写任何生产码/资产 |
| ⛔ 不 push | ✅ 未 push（本报告单独 commit） |
| ⛔ 不代提交策划端账本 | ✅ 未触 `_编号登记.md`／`_任务队列.md`／`_当前快照.md`／`_测试基线台账.md`／`_策划教训库.md` |
| 报告落盘 | ✅ `多Agent交接/执行端/HH.319_M1-F水与自交互_评估报告.md` |

**停手点**：`Q1`~`Q9` ＋ §六 列报 8 条全部裁清后再出施工任务书；⚠️ 其中 **`Q2`／`Q3`／`Q4`／§五 第 4 行（容量落点）** 是不可留白项（决定 `.asset`/`.unity` 是否动、动哪几行）⇒ 建议**优先裁这 4 项**。

---

*执行端 · 2026-09-21 · `HH.319` 只读评估完毕（`Q1`~`Q9` 全部给出建议与代价 · 6 项任务逐条给依据 · `Assets/**` 零写）*
