# HH.319 · M1-F · F-1 批（水仓化 ＋ 真搬运链）· 交付报告

- 任务号：`HH.319` · `M1-F` 的 **`F-1`** 批（差异：`09#44` ＋ `09` §4.3 D 组）
- 依据：施工任务书（`F-1`）＋ 裁决书 `D807`（Q1~Q6 ＋ §二-1 国别过滤 ＋ `Well.droppable`）
- 日期：2026-09-21 · 执行端（TraeCode）
- 红线：本批**限水域** ✓；⛔ 未动 `WorkerInventory`／`UnitController`／`DamageSystem`（多资源化属 `F-2`）；⛔ 未动退款公式／`DeathCause`／产金端／存档 schema
- 收尾：⛔ 未 push（本报告自行 commit）；⛔ 未代提交策划端账本

---

## 〇 结论速览

| 项 | 结果 |
|---|---|
| 件 1~8 | **全部落地**（8/8 · 逐件见 §一） |
| 编译 | **0 error**（10 warning 全部既有，所在文件均非本批改动；`Codely unity_editor.refresh` 读数 `errors=0`） |
| csproj 同步 | ✅ 已跑（删 `WaterNetwork.cs` 后 · `Rider ProjectGeneration.Sync`） |
| 资产 | ✅ 2 处（`Well.droppable: 0` ✓ ／ `farm.warehousePaths` 加 `res_fluid.water` ✓）；`GameScene.unity` **零改动**（`waterCarryAmount: 10` 对 `int` 天然兼容 ⇒ 判据 8 零值变） |
| 判据 1~10 | **9 项通过**（判据 1/2/3/5/6/7/8/9/10）；⚠️ **判据 4 逻辑通过但读数环境不稳**（见 §三-判据4） |
| 判据 11 回归 | ⚠️ **部分**：`HH316` 跑到 `§D` 通过（§E~§H 未及，编辑器停）；`HH315`／`HH317`／`2_20B_M7` ⛔ 未跑 |
| ⭐ 报裁 | **3 条**（§四）：① 探针环境不可控（3b 端到端）② WaterHaul（C 档）刺激竞争观察 ③ 判据 9「井仓满 ⇒ Transport 直达农场」与"农场缺水 WaterHaul"**功能重叠** |

---

## 一 件 1~8 落地清单（改前 ⇒ 改后）

### 件 1 · 水落码（`Q1`）
| 文件 | 改前 | 改后 |
|---|---|---|
| `GameEvents.cs:196+`（**CRLF** · python 二进制按行替换） | `MagicAmmo`（末项无逗号 · 13 项） | `MagicAmmo,` ＋ 注释 2 行 ＋ `Water`（**枚举值 13 · 末尾追加**） |
| `ResourceCatalog.cs:32-35` 注释 | 「已有 13 项」／「B 组／D 组标签未入本表」 | 「A 组 13 ＋ D 组水 1 ＝ 14 项」／D 组**已落** ＋ A 组 `medkit` 未落 ＋ B 组归 `#43` |
| `ResourceCatalog.cs:53+` | — | `new Entry(ResourceType.Water, new[] { "res_fluid.water" }, 1, "水")` |

### 件 2 · 水井产水入本仓（`Q2`/`Q5`）
- `TickWaterToNetwork(int)` ⇒ **`TickWaterToStorage()`**：`_storage.IsFullFor(Water)` 停产（DR-8）⇒ 复用 `_mainAccumulator` ＋ `FloorToInt` ⇒ `_storage.Add(Water, n)`（放到满为止）。
- ⭐ **保留"免工自产"**：`Tick` 的 `_isWell` 早返回**保留**（⛔ 不落 `HasWorkerAssigned` 守卫）。
- `IsWell`／`_isWell` 注释：入网 ⇒ **入本仓**；AI 桶语义消解（每国自己的井 ⇒ 自己的仓）。

### 件 3 · 农场耗水从本仓（`Q4`）
- `TryConsumeFarmWater()`：`WaterNetwork.ConsumeWater(2f,…)` ⇒ **`_storage.CanTake(Water,2)` → `TakeOut(Water,2)`**；不足 ⇒ 停产 ＋ 头顶"缺水"（形制不变 · 顺带补 `_building != null` 守卫）。

### 件 4 · 农场广告搬水任务（`Q3`/`Q6` ＋ 国别过滤）
- `Building.cs:1460-1461`：`waterThreshold` **`float ⇒ int`** ＋ Tooltip 改「**农场仓** Water < 此值」。
- `Building.TryAdvertiseTask` ④ 段：判据改**农场仓水量** ⇒ ⭐ **新增 `FindNearestSameKingdomWellWithWater()`**（`def.id=="Well"` ∧ `Active` ∧ **同 `kingdomId`** ∧ **仓内 `Water>0`**）⇒ `task.source = 井`（第一段位移）／`destType = SpecificBuilding` ＋ `destPos = 农场`（第二段位移）／`args = HaulWaterArgs{target=农场仓, need=缺口}`；⛔ 无候选不发布。
- `KingdomTask.cs`：**新增 `HaulWaterArgs`**（⛔ 未新增 `KingdomTaskType`）；`KingdomDestType.WaterNetwork` ⇒ **退役保占位**（⛔ 不删不重编号 · 注释标明）。

### 件 5 · 任务链两段（TaskScheduler）
- Working 分支：**新增** `WaterHaul && HaulWaterArgs` ⇒ 复用 `LoadInventoryFromSource`（源＝水井仓 ⇒ `GetComponent<StorageComponent>` 直接命中）⇒ 转 `MovingToDest`。
- MovingToDest：**新增** `DepositWaterToFarm(brain, task, wa)`（照 `DepositToSite` 形制：`target.Add(Water,n)` ＋ 溢出 **退回水井仓**（`ReturnOverflow`）⇒ ⛔ 不丢资源）。
- ⭐ **删** `case KingdomTaskType.WaterHaul`（完成 ⇒ `AddWater` 凭空入桶）—— 残留路径整段移除。
- ⭐ **删** `ResolveDest` 的 `case KingdomDestType.WaterNetwork` ＋ **整段删** `ResolveWaterSource`（⛔ 不过滤国别的旧解析 ⇒ 缺陷随删除消失）。
- `waterCarryAmount` **`float ⇒ int`**（Tooltip 标：⛔ 不再参与搬水链 · 装载上限改由 `StorageComponent.GetCarryAmount(Water)` 决定 · 字段保留占位＝场景序列化稳定）。

### 件 6 · `WaterNetwork` 整体退役
- **删除** `Assets/_Game/Systems/Kingdom/WaterNetwork.cs` ＋ `.cs.meta`（165 行）。
- `WorldLifecycle.cs:37` 调用 ⇒ 删（注释说明：水已入水井仓 ⇒ ⛔ 无全局桶可残留）。
- 引用面 **16 文件**全部清理（生产 6 ＋ Editor 9 ＋ 注释 3）；⛔ 剩余出现**全部是注释/枚举占位**（`grep` 读数：`WaterNetwork` 0 处代码引用；`\bwn\b` 0 处）。
- ⚠️ 旧档兼容：`SaveManager.HasSaveable("WaterNetwork")=False`（结构级读数 ⇒ 未注册 payload 不会被解析 ⇒ 不报错）。

### 件 7 · 资产（经 Unity · ⛔ 未手改 YAML）
- `Well.asset`：**加 `droppable: 0`**（水＝工位缓冲 · ⛔ 非战利品）。
- `farm.asset`：`warehousePaths: [res_food.grain, res_fluid.water]`。
- `GameScene.unity`：⛔ **零改动**（`waterCarryAmount: 10` 对 `int` 合法 ⇒ 无需重序列化；判据 8 已给兼容读数）。
- ⛔ `well.producer.rate` 未动（`Q5`）。

### 件 8 · 注释勘正（6 处 · 零行为）
`TaskPriorityConfig.cs:43`（搬水→仓）／`AIDebugSpawnController.cs` 6 行（`WaterNetwork` 产水／水网缺水／`ConsumeWater` 等）／`KingdomState.cs:50`（专用桶先例）／`Building.cs:1477`（方法头 ③）／`Building.cs:1460` Tooltip ／`R3_SupplyChain.cs:97`（"非 ResourceType 桶"⇒"水井仓 · `ResourceType.Water`"）＋ `ProducerComponent` 3 处（`_isWell`/`IsWell`/`Init`）。

---

## 二 探针口径演进登记（任务书 §件6 要求单列）

| 探针 | 改动 | 备注 |
|---|---|---|
| `TestFixtureApi.cs` | **+3 helper**（`ReadKingdomWaterInWells`／`AddWaterToKingdomWells`／`TakeWaterFromKingdomWells`）；`PlaceKingdom` 注水段 ⇒ **给该国水井仓注满**；`ReadThreeSources` 水读数 ⇒ 仓化 | 仓化三口的唯一入口（⛔ 不各写一份） |
| `Valley_HH73_Smoke_Water.cs` | 全部读数 ⇒ 仓化三口；`DumpBuckets(WaterNetwork)` ⇒ `DumpWells`；头注释加**演进说明** | 判据语义等价（"AI 桶"⇒"AI 国井仓"） |
| `Valley_HH76_Smoke.cs` | `GetStored(kid)` ⇒ 井仓读数；"AI 桶蓄水" ⇒ "井仓蓄水" | — |
| `Valley2_17_Smoke_11.cs` | 原「`WaterNetwork` B′ 双语义（玩家桶扣2/AI桶恒false/零染）」⇒ **改「水仓化结构探针」**（每口井有仓 ∧ `Accepts(Water)`） | 演进说明写进文件头 ＋ 判据行 |
| `Valley2_17_Smoke_2b.cs` | 原「AI 水井不产水入网」⇒ **改「AI 井产水入本国井仓（⛔ 不充玩家）」**（双侧读数） | — |
| `Valley_TestHarnessDemo.cs` | 「旧水桶=0」⇒「旧世界井仓水=0」 | — |
| `Valley_P1_Observer.cs` | 删 `"[WaterNetwork]"` 日志过滤锚（注释留痕） | — |
| `R3_SupplyChain.cs` | 审计文本勘正 | — |
| ⭐ **新增** `Valley_HH319_M1F_Probe.cs` | 本批判据探针（判据 1~10 ＋ 鉴别力行 ＋ 诊断采样） | 菜单：`Valley/验证/HH319_M1F水仓化探针` |

---

## 三 判据 1~11 逐条读数（含前置鉴别力）

> 读数取自 `HH319_M1F_Probe` 各轮运行（最终轮 **9/10**）。每行「鉴别力」为该判据在**改前**会读成什么样。

| # | 判据 | 读数 | 判定 |
|---|---|---|---|
| 1 | 落码 | `enumVal=13 Contains=True Vol=1 Path=res_fluid.water Well收水=True farm收水=True` | ✅ |
| 2 | 井产水入本仓（含满仓停产） | `t0=0 →2s后=8/16（应增） · 注满=100（=cap100）→1.5s后=100（持平＝停产）` | ✅ |
| 3 | ⭐⭐ 真搬运两段 | `[3a广告]=True[WaterHaul/源=Well/k0]` ＋ `[3c单元级]=True[井仓 100→90 · 装载=10 → 卸货后背包=0 · 农场仓 0→10]` ｜`[3b走调度器·附加读数] 派WaterHaul=False（环境抢占 · 见 §四-①）` | ✅（3a＋3c） |
| 4 | 农场耗水从本仓 | ⭐ **v6（含完整链）**：`起水=4/粮=0 → 水扣光：水=0/粮=4（耗4水·产4粮 ⇒ 每次事件扣2） → 水尽后3s：水=0/粮=4（持平＝缺水停产）`；⚠️ v8 复跑 `水=4/粮=0`（**农场无工人在场** ⇒ 全程无事件） | ⚠️ 逻辑通过 · 环境不稳 |
| 5 | ⭐ 国别过滤 | `AI国k4：(a)AI井有水(100) ⇒ 选中=Well/k4（⛔ 非 k0）｜(b)AI井清空 ⇒ 选中=null（过滤生效）｜玩家井水量=100（诱饵在场）` | ✅ |
| 6 | `WaterNetwork` 退役 | `类型在场=False`（全库零引用编译通过）／`HasSaveable("WaterNetwork")=False` | ✅（结构级） |
| 7 | 资产 | `well.droppable=False 箱数 0→0（井掉箱调用后不变）井水 100→100（不掉）｜对照 farm 有水 ⇒ 箱数 0→1` | ✅ |
| 8 | 整数化 | `waterCarryAmount=Int32/10 waterThreshold=Int32/20`（场景值 10 兼容 ⇒ 零值变） | ✅ |
| 9 | 井仓满广告面（读数项） | `井仓=100/100 广告=True 类型=Transport 终点=NearestWarehouse` | ✅（读数 · 见 §四-③） |
| 10 | 存档往返 | `存档时(玩家井仓)=100 改后=70 读回=100（≈存档值±产水增量 · ≠改后值）` | ✅ |
| 11 | 回归 | ⚠️ `HH316`：`§A` 起局 ✓／`§B` 判据1「箱⇒取货⇒到账」✓ ＋ 判据4「多资源箱逐轮搬完=True」✓／`§C` 靶例双箱逐条目到账 ✓（木 106→109）／`§D` 金走仓进 Vault ✓（`金(Vault)=80→91 注入仓金=0`）；⚠️ `§E~§H` **未及**（编辑器停）｜`HH315`／`HH317`／`2_20B_M7` ⛔ **未跑** | ⚠️ 部分 |

**判据 11 未跑项的处置建议**：由策划端裁"验收批补跑"或"本端续批补跑"（⛔ 本端不自行降低要求）。

### 前置鉴别力声明（逐条）
1. 改前 `Accepts(Water)` 对任何仓恒 `False`（枚举/表均无 `Water`）。
2. 改前水入 `WaterNetwork._stored` ⇒ **水井仓读数恒 0**。
3. 改前 `SourcePos=农场`（工人站农场 2 秒）＋ 水**凭空入桶**（井仓零变化 · 背包恒 0）；本批 3c 用**反射直调生产方法**给"装载/卸货"硬证（井仓 −10 ＝ 背包 +10 ＝ 农场仓 +10 · 零残留）。
4. 改前读 `WaterNetwork` 桶（农场仓水恒 0 · 无扣减）。
5. 退役 `ResolveWaterSource` **⛔ 不过滤国别** ⇒ 不过滤实现下 (b) 会选中玩家井 k0（本读数会显示 k0）；(a) 在玩家井更近时亦会选 k0。
6. 改前类型在场且 `SaveId` 已注册。
7. 改前 `Well.droppable` 缺省 `true` ⇒ 井仓有水**必掉箱**（箱数 +1）。
8. 改前两者为 `float`。
9. 见 §四-③（本项为"须给读数确认"项）。
10. 改前水在 `WaterNetworkSaveData`（`SaveId="WaterNetwork"` · `LoadPhase=Global`）；本批走 `BuildingSaveData.storageContents`（⭐ 零新存档面）。

---

## 四 ⚠️ 报裁（3 条 · ⛔ 本端未自行取舍）

### ① 探针环境不可控：判据 3b（走调度器的端到端）与判据 4 的读数不稳
- **现象链（8 轮探针迭代实录）**：
  - v4/v5：工人被**反向拉离水井**（距井 1.20 → 6.85）⇒ 定位为**旧 `Production` 刺激残留**（探针自身构造法注入）拉走工人；
  - v6：换**全新工人**后不再被拉走，但**卡在 `MovingToSource`（距井 1.29 未达 `ArrivalThreshold`）** ⇒ 永不进 Working ⇒ 装载不发生；
  - v7：warp 贴近（0.5）后**任务被别的派工抢占**（`_npcTaskMap` 未见 WaterHaul）；
  - v8：3a/3c 稳定通过，3b 仍为环境噪声（`派WaterHaul=False`），判据 4 因"农场无工人在场"读数空。
- **判定**：`LoadInventoryFromSource`／`DepositWaterToFarm`（装载/卸货）**逻辑正确性已由 3c 硬证**；**端到端（走调度器 + 真实寻路）**在本探针环境下不可稳定复现。
- **⚠️ 请裁**：是否需要"**真实长局（正常调度）专项复核**"搬水链（建议：并入验收批或 `F-2` 同局跑一次长局观察；本端不自行降低判据要求）。

### ② ⭐ v5 观察：`WaterHaul`（C 档）与 `Production`（B 档）**刺激竞争**
- **读数**：同一工人身上同时存在"指向**水井**的 WaterHaul 刺激（C 档）"与"指向**农场**的 Production 刺激（B 档）"时，工人**被 B 档拉向农场**（距井单调增大）。
- **改前为何未暴露**：改前 `WaterHaul.source = 农场` ⇒ 两刺激**同向**（都指向农场）⇒ 冲突不可见；改后 `source = 水井` ⇒ 两刺激**反向** ⇒ 冲突显形。⚠️ 与 `D807 §二-1`（缺陷被"半假搬运"掩盖）**同族**。
- **影响面（本端未断言 · 列读数）**：真实局中"新派 WaterHaul 的工人"若身上**仍残留** Production 刺激（`taskExpiry=5s` 内）⇒ 该趟搬水可能被拖慢/推不动；`taskTimeout=30s` 后任务放弃 ⇒ 下 tick 重派。
- **⚠️ 请裁**：是否提高 `WaterHaul` 优先级（`TaskPriorityConfig`）／或在派工侧清旧刺激／或维持现状（靠重派自愈）。

### ③ ⭐ 判据 9 读数：井仓满 ⇒ `Transport` 直达农场，与"农场缺水 WaterHaul"**功能重叠**
- **读数**：`井仓=100/100 ⇒ well.TryAdvertiseTask=True · 类型=Transport · 终点=NearestWarehouse`。
- **链路**：井仓满（≥`capacity×0.8`）⇒ 井自己广告 `Transport` ⇒ `ResolveWarehouse(want=Water)`：全库**唯一收水仓 ＝ farm**（本批改标签后）⇒ 水被搬去农场 ⇒ **无人挑水时水也能到农场**。
- **⚠️ 请裁**（⛔ 本端未自行取舍）：
  - 甲：视为**合理兜底**（水井主动"送水"，搬水链因此更不易断）；
  - 乙：视为**打架**（削弱"农场缺水 ⇒ 需工人挑水"的设计意图 ⇒ 若要保留挑水玩法，需限制井仓的 Transport 广告或收水仓集合）。

---

## 五 红线自查

| 红线 | 自查 |
|---|---|
| 改动面**限水域** | ✅ 18 文件（生产 11 ＋ Editor 8 ＋ 资产 2 − 交集）；⛔ 未动 `WorkerInventory`／`UnitController`／`DamageSystem` |
| ⛔ 不动退款公式／`DeathCause`／产金端／存档 schema | ✅ 未动；存档面＝仅"水随 `BuildingSaveData.storageContents` 走"（零 schema 变化） |
| ⛔ 不碰 `WarehousePanel`／四档账本／美术／pixel-forge／`Packages`／3.6·3.8 doc | ✅ 未碰 |
| `GameScene`（除 `waterCarryAmount` 类型） | ✅ **零改动**（类型兼容 ⇒ 未重序列化） |
| 行尾纪律（改前先验） | ✅ 逐文件验证：仅 `GameEvents.cs` 为 **CRLF** ⇒ 走 python 二进制按行替换（⛔ 未 Edit）；其余 LF 走 Edit；资产经 Unity（⛔ 未手改 YAML） |
| 进局走正门 `TestHarnessApi.EnterTestRun` | ✅ 探针与回归冒烟均走正门（`ExitTestRun`＋`QuitSmoke` 收尾） |
| 具名 `git add`／⛔ 不 push／⛔ 不代提交策划端账本 | ✅ 见 §六 |
| ⚠️ 阻塞歧义 ⇒ 先停手报裁 | ✅ §四 三条（⛔ 未自选处置） |

---

## 六 交付与落盘

- 报告：`多Agent交接/执行端/HH.319_M1-F-F1水仓化与真搬运_交付报告.md`（本文件 · 自行 commit）
- 证据：
  - 判据探针：`Valley Rampart/Assets/Editor/Smoke/Valley_HH319_M1F_Probe.cs`（菜单 `Valley/验证/HH319_M1F水仓化探针` · 可复跑）
  - Console 判据读数：`[HH319探针]` 行（9/10 ＋ 逐条鉴别力）
  - 回归：`[HH316U2]` 行（`§A~§D` 通过）
- 挂账（⛔ 本端未改）：
  - **判据 11 余项**（`HH315`／`HH317`／`2_20B_M7` ＋ `HH316` 后段）⇒ 待补跑；
  - **§四 三条报裁** ⇒ 待策划端裁；
  - `F-2`（`WorkerInventory` 多资源化 ＋ 自交互）⇒ 按 `D807` 切分**不在本批**。

---

*执行端 · 2026-09-21 · `HH.319` `F-1` 批施工完毕（件 1~8 全落地 · 编译 0 error · 判据 9/10 ＋ 回归部分 · 3 条报裁待裁）*
