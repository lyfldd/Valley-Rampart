# HH.318 · M1-E「金币降普通资源」· 只读评估 ＋ 报裁报告

- 任务号：`HH.318` · `M1-E`（`M1` 第五子片 · 承 `M1-A`～`M1-D` ⇒ 完成度 4/7）
- 差异：`09#47` ＋ `09` §4.3 A 组（结构层 `B1~B8` 已定案）
- 本轮性质：**只读评估 ＋ 报裁**（⛔ `Assets/**` 一行不动 · ⛔ 未开工 · ⛔ 未 push · ⛔ 未代提交策划端账本）
- 日期：2026-09-21 · 执行端

---

## 〇 结论速览（`Q0` ＋ `Q1`~`Q8` 建议表）

| # | 议题 | ⭐ 本端建议 | 代价 |
|---|---|---|---|
| `Q0` | 批次账边界 | **成立**：`M1-B` 实四件**不含 `#43`/`#52`** ⇒ 裁「`#52` **按金面并入 `M1-E`**（非金面留 `M1-G`）／`#43` 独立挂账」 | 见 §三 |
| `Q1` | 金进哪个仓 | **国库容器扩收金**（`VaultPaths` 加 `res_currency`）· ⛔ 不开第二容器 | 零（体积 0） |
| `Q2` | 「体积 0」 | ⭐ **是设计**（非巧合）：`StorageComponent:149` 注释直指金币用例 ＋ `ResourceCatalog:39` `Volume=0` 已落码 | 零 |
| `Q3` | 「目标仓」形态 | ⭐ **尾参默认值法**：`ModifyResource(type, isAdd, amount, int kingdomId = 0)` ＋ 内部 `TreasureVault.Get(kingdomId)` ⇒ **现有 ~60 生产调用点零改** | 新增共用 helper ≈ 80 行 |
| `Q4` | `Gold` 退役次序 | **五步安全序**（先断 `GetResourceValue` 分叉 ⇒ 再转 `Gold` 门面）；**保留 `Gold` 只读门面 ⇒ 6 处 UI 读点零改** | 见 §四-Q4 |
| `Q5` | 存档迁移 | ⭐ **做轻量迁移桥**（读档 `data.gold>0 ⇒ Deposit`）· ⛔ 不写完整迁移器（与 `M1-A` 口径不矛盾） | +10 行 |
| `Q6` | AI 退化行为 | 读=0／写=**丢弃＋`LogWarning`**（对齐「国已注销 ⇒ 丢弃+日志」先例）· ⛔ 不引第二缓冲字段 | 零结构 |
| `Q7` | ×1.5 同批 | ⭐ **同批落地**（`TrySpawnOrcLoot` 内·只乘金·`RoundToInt`） | +3 行 |
| `Q8` | `U-7`／`O-2` | `U-7` ⭐ **必须并入**（否则金可能卸进水井）／`O-2` **金侧自动消解·材料侧归 `M1-G`** | 见 §四-Q8 |

**一个总判断**：本片**结构改动小、语义改动大** —— 因 `M1-A` 已把仓体系（标签/体积/单容器/按国查）全部铺好，金"降为普通资源"的落码面 ≈ **`RulerController` 内部 6 处 ＋ `TreasureVault` 1 行 ＋ 两个连带点（`UnloadInventory`/`ResolveChestDest`）**；⛔ 不需动 30+ 个调用方（靠默认参数与只读门面保住）。

---

## 一 · 契约回读（§4.3 A 组 `B1~B8` · 复核 ✅）

`09_资源与仓库.md:163-177` 逐条复核，与任务书转述**零偏差**：

| `B#` | 契约 | 复核 |
|---|---|---|
| `B1` | 每国一个国库金仓挂主城；主城倒 ⇒ 掉箱 | ✅ `:165` |
| `B2` | 体积＝0 ⇒ 进仓不占容量（"不占容量的首个用例"） | ✅ `:166` |
| `B3` | 金无上限＝`B2` 必然推论·⛔ 不单开字段 | ✅ `:167` |
| `B4` | 可被搬出；抢国库＝打掉掉箱（复用 §九·零新机制） | ✅ `:168` |
| `B5` | 不能开采（只靠系统结算：税/贸易/退款） | ✅ `:169` |
| `B6` | 花出去的金＝消亡（§7.2 ⇒ §十-5 消亡记账必须含金） | ✅ `:170` |
| `B7` | `TreasureVault`＝国库仓本体（9 子仓塌 1·`Instance` 按国查） | ✅ `:171`（`M1-A` 已落） |
| `B8` | ⭐ **扣费＝统一一个 API**（`RulerController` 三件 ↔ `KingdomState` 两件 **合并**·统一走仓 CRUD·**带目标仓参数**） | ✅ `:172` |
| 尾 | `Gold:48` 退役 ⇒ 读口改 `GetResourceValue(Gold)`；`RulerController` ⇒ 只读门面；`ModifyResource` 金分支退役；金迁进仓存档；AI 抽象日结依赖主城（退化留执行端设计） | ✅ `:173-176` |

⚠️ **两处契约边角（本端提请钉死）**：
1. `:172` 写「`RulerController.CanAfford:270`/`Spend:277`/`Refund:287`」——⭐ **行号漂移**（`M1-A` 后实为 `:270`/`:283`/`:298`）⇒ 引用时以名称为准。
2. `:177` 「产金端/消费端落点留**逐建筑功能**讨论」⇒ 本片**不做**"哪座建筑产金"（现状＝`TaxSystem` 商业税 `:117` ＋ 贸易 `TradeSystem:101` ＋ 退款（`M1-C` 后已退役）＝**全部系统结算** ✓ 与 `B5` 自洽）。

---

## 二 · 现状实读（任务书 §二 逐条复核 ＋ 勘正）

| # | 任务书说法 | 本端实读 | 判定 |
|---|---|---|---|
| 1 | `RulerController.cs:48 Gold` 裸字段 | ✅ `:48` `public int Gold { get; private set; }`（外部可读·仅类内可写） | ✅ |
| 2 | `:309-314 GetResourceValue`·`:311` 金分叉 | ✅ 逐行一致（`:311 if (type == Gold) return Gold;`） | ✅ |
| 3 | `:228-255 ModifyResource`·金分支 `:233-241`／非金 `:243-254`·`:244` 写死单例 | ✅ 逐行一致；⭐ `:244 var tv = TreasureVault.Instance;`（**= `Get(0)` 玩家国**·⛔ 无目标仓参数） | ✅ |
| 4 | `:270/:283/:298` 已通用化 | ✅ `M1-A` 已逐条目化（`ResourceList`）⇒ **这三件本身即"一套"** | ✅ |
| 5 | `TreasureVault` `_byKingdom`（`:53`）＋单容器（`:59-65`·⛔ 不调 `Init`）＋ `VaultPaths` 不含金 | ✅ `:25 VaultPaths = { res_material, res_food }`（⛔ 不含 `res_currency`）；`:28` 字典；`:59-65` 子物体 `Vault`＋`SetDeclaredPaths`＋`WarehouseRegistry.Register`（⛔ 不 `Init`） | ✅ |
| 6 | `KingdomState.resources`（`:47`）／`crystal:54`／`fireOil:56`／`ore:58` | ✅ 逐行一致（注释自陈"过渡账本…2_17 步骤2 迁移吸收"） | ✅ |
| 7 | 金生产端＝系统结算；消费端＝"扣掉就没" | ✅ 生产：`TaxSystem:117`（商业税）／`TradeSystem:101`（卖物得金）；消费：`TradeSystem:130`／`RanchSystem:98`／`TrainingSystem:507`／建造升级 | ✅ |

### 勘正与新发现 4 条（任务书未列）

1. ⭐ **`WarehouseHelper` 是"金扣费"的玩家主路径**（建造/升级/训练）：`:39-46`（`CanAfford` 金分支 → `RulerController`）／`:65-66`（`Spend` 金分支）。其类注释 `:13` **已自陈**「金仍走 `RulerController` 直通（行为不变 ——「扣费统一一个 API」归 `M1-E`，`09#47`）」⇒ **`M1-E` 的施工面必须含本文件**（合并目标之一）。
2. ⭐ **`UnloadInventory` 金直通分支**（`TaskScheduler:797-804`）与 **`ResolveChestDest` 金跳过找仓**（`:836-841`）：注释已标「⏭️ 待 `M1-E`（金进国库仓）落地后再切仓路径」⇒ **两处连带施工点**（⛔ 否则金永远走不到国库仓）。
3. ⭐ **AI 国也有国库容器**：`CastleCoreComponent.Init:156-157` 挂 `TreasureVault` **无国别守卫**（第一代 `KingdomFoundry.PlaceBuildings` 含 castle；动态立国 `PlaceCampCastle:460`）⇒ `TreasureVault.Get(kid)` 对 AI 国**结构可达** ✓（`M1-A` 的 `_byKingdom` 已按国隔离）⇒ **"AI 金进仓"无需新建结构**。
4. ⭐ **水井不产金**（澄清 `U-7` 边界）：`Well.asset:49 outputResource: 0`（＝`Gold` 枚举 0）是**占位值**，但 `ProducerComponent:38-47` 有**水井特判**（`_isWell` ⇒ 产水入网/桶·跳过产金分支）＋ `D144` 已让非井 Gold 建筑 `rate=0` ⇒ ⛔ **不存在"水井产金"**；`U-7` 严格是**仓库标签误配**（不影响产出）。

---

## 三 · ⭐ `Q0` 批次账边界（`#43`/`#52` 未落地坐实）

### 3.1 实读证据链

| 证据 | 读数 |
|---|---|
| `M1-B` 交付 `db864acc` · 6 files | 探针×2 ＋ `BuildingFactory.cs` ＋ `StorageComponent.cs` ＋ `IWarehouse.cs` ＋ 报告 ⇒ ⛔ **无 `KingdomState`／`PopulationSystem`** |
| `HH.314` 验收裁决 §五（四件） | 件1 账本汇总量（事件契约四段）／件2 读口分类（`Query`/`SumByPrefix`/`FreeSpace` 12 项）／件3 `DZ-1`（`WarehousePanel` 合计虚增）／件4 `DZ-2`（读档容量刷新）⇒ **全是「账本事件契约 ＋ 容量刷新」** |
| `09#43` 定义（`:487`） | 现状「`PopulationSystem.PopulationCount => _entities.Count`」⇒ 目标「**账本为读口**」= **人口进账本**（B 组）⇒ ⛔ 未落 |
| `09#52` 定义（`:494`） | 现状「`KingdomState` 持第二本账」⇒ 目标「**统一走仓**」⇒ ⛔ 未落（本端实读：`KingdomState.cs:47/54/56/58` 四桶俱在） |
| 旁证 | `M1-C` 裁决 §97.9.2 第 3 条「`09#52` 不在本片 6 项」 |

### 3.2 建议裁法（本端）

> ⭐ **`#52` 拆分并入**：**「金」面并入 `M1-E`**（理由：`B8`「扣费＝统一一个 API」在语义上**必然同时覆盖 AI 侧**（否则 AI 扣金仍走台账、玩家走仓 ⇒ 两套更分叉）；且金是**单资源** ⇒ 面可控）。**「非金」面**（石/木/粮/铁 ＋ 副产三桶 `crystal/fireOil/ore` 也迁仓）⇒ **留 `M1-G`（兜底/账本口径批）**。
> 理由（为何不当批全并）：`#52` 全并＝AI 侧全部资源读写面改道（`GetResourceValue`／`AddResources`／`Spend`／`AIEconomySettlement`／`AbstractEconomySettlement.ApplyDelta`／`TaxSystem`／`RanchSystem`／`SiegeProductionSystem`／`KingdomBrain`／`BuildController`／`EconomyDiagnosis` **≈ 20 处**＋副产三桶语义）⇒ 与 `M1-E` 的"金"面异质，回归风险大且**无契约新增要求**。
> ⭐ **`#43` 独立挂账**：人口＝B 组（`09` §4.3 B 组明标「⛔ 与金无关，不在本片」）⇒ 与 `M1-E` 零交集 ⇒ 建议**随 `M1-B` 的账本读口课题另立子片**（或并入 `M1-F`/`M1-G`，由策划端定）。

⚠️ **中间态说明**（供裁量）：拆分后 AI 国短暂呈「金在仓 ＋ 非金在台账」——本端认为**可接受**：因为① 读口统一（`KingdomState.GetResourceValue` 转发 ⇒ 调用面不变）② `M1-G` 收尾后即归一 ③ 若全并，`M1-E` 的回归面从"金"扩到"AI 全经济"，得不偿失。

---

## 四 · `Q1`~`Q8` 逐条建议

### `Q1` 金进哪个仓 —— **国库容器扩收金**（⛔ 不开第二容器）

- **建议**：`TreasureVault.VaultPaths` ⇒ `{ "res_material", "res_food", "res_currency" }`（加**族前缀**，与既有两族同写法；⛔ 不写死 `res_currency.gold`——将来多币种零改）。
- **理由**：① `09` 同时说「每国一个国库金仓」＋「`TreasureVault` ＝ 国库仓本体（**9 子仓塌 1**）」⇒ 最简自洽解＝**金是国库容器里的一个资源**（"金仓"＝国库的金）；② `B3`「金无上限⛔不单开字段」——若另开容器，则"无上限"需另做容量豁免（多一套机制）；同在国库容器里，`Volume=0` 已天然"无上限"（`CanAccept ⇒ MaxValue`）；③ `M1-D` 已让国库容器**整体可掉**（`Building.DropVaultToChest`）⇒ 金随之掉箱 = `B4`「抢国库」**零新机制** ✓。
- **代价**：0（一行数组）＋ `:18-19` 类注释「本容器不收金」须勘正。

### `Q2` 「体积 0」 —— ⭐ **是设计，非巧合**（已落码·无需改）

- `StorageComponent.cs:145-152`：`CanAccept` 对 `vol<=0` **直接 `return int.MaxValue`**，且 `:149` 注释**明文**写「体积 0 ＝ 不占容量（§4.2 金币用例）」。
- `ResourceCatalog.cs:39`：`Gold … Volume 0`；`:36` 注释「一切资源＝1，仅金币＝0（不占容量）」。
- ⇒ 判据链完整：**表值（0）⇒ 容量算式豁免（MaxValue）⇒ UsedSpace 不计（`VolumeOf`×量=0）⇒ `IsFull` 永不因金满** ⇒ `B2`/`B3` 已结构性成立；⛔ 本片无需碰。

### `Q3` 「目标仓」参数形态 —— ⭐ **尾参默认值法**（波及面 ≈ 0）

**读数（波及面清单 · 件 5 完整版）**：

| API | 生产调用点 | Editor 调用点 | 本片是否需改 |
|---|---|---|---|
| `Gold` 读（外部） | 6：`TopLeftHUD:82`／`ResourceHUD:96`／`TrainingPanel:260`／`GameOverPanel:87`／`TradeSystem:128`／`RanchSystem:97` | 若干探针 | ⛔ **零改**（保留 `Gold` 门面 ⇒ 见 `Q4`） |
| `Gold` 写（外部） | **0**（全走 `ModifyResource`） | 0 | — |
| `ModifyResource` | **17**（定义 1 ＋ 内调 2 ＋ 外部 14：`TradeSystem×4`／`RanchSystem×3`／`TrainingSystem×3`／`StorageComponent×3`／`TaxSystem`／`VagrantCampSystem`／`SatietySystem`／`SiegeWorkshopBuilding`／`TaskScheduler`／`AIDebugUIManager`） | ~18（7 文件） | ⛔ **零改**（默认参数）＋ **金参数 6 处**内部转仓（不改签名） |
| `GetResourceValue` | ~20（`RulerController` 内 16 ＋ 外部 4：`SatietySystem:213`／`HappinessSystem:221`／`RanchSystem:103`／`TrainingSystem:493`） | ~10 | ⛔ **零改**（只改 `RulerController` 内部 1 行） |
| `CanAfford/Spend/Refund`（玩家） | ~18（`WarehouseHelper:43/66/91`／`SiegeProductionSystem:169-177`／`KingdomManager:187-189`／`MachinePanel:232`／`PlacementValidator:156`／`MachinePlacement:151`／`BuildingPanel×7`／`BuildingMenuPanel:272`／`BuildController:352/361/369`） | ~10 | ⛔ **零改**（薄壳转发） |
| `CanAfford/Spend/Refund`（AI） | ~12（`SiegeProductionSystem:214-226`／`SatietySystem:220`／`RanchSystem:104/181`／`TrainingSystem:514`／`PlacementValidator:162`／`BuildController:363/372`／`KingdomBrain:883/1120`） | ~6 | ⛔ **零改**（内部转发仓） |

- **建议形态**：
  ```
  ① RulerController.ModifyResource(type, isAdd, amount, int kingdomId = 0)   // 尾参默认玩家 ⇒ 全库调用点零改
  ② 内部：TreasureVault.Get(kingdomId)?.Deposit/Take(...)                    // 取代写死的 Instance
  ③ 新增共用静态 helper（如 KingdomTreasury.CanAfford/Spend/Refund(int kingdomId, ResourceList)）
     ⇒ RulerController 与 KingdomState 两侧**薄壳转发**（契约 B8「只留一套」的落点）
  ④ 金分支（RulerController:233-241）退役；KingdomState 金条目转发仓、非金留台账（Q0 边界）
  ```
- **理由**：契约 `B8` 的实质要求是"**只有一套实现**"（⛔ 不是"改 30 个调用点"）。用**默认参数＋薄壳**可同时满足"统一实现"与"调用面零改"——⭐ 这也是本片最省的路径。
- **代价**：新增 helper ≈ 80 行；两侧转发 ≈ 20 行；`ModifyResource` 金分支删除 ≈ −9 行。

### `Q4` `Gold` 退役次序 —— ⭐ **五步安全序**（防递归死循环）

> ⚠️ 递归点坐实：`:311 GetResourceValue` 读 `Gold`；若先把 `Gold` 改成 `=> GetResourceValue(Gold)` ⇒ **无限递归**。

**安全次序**（每步可编译＋可跑探针）：
1. **`TreasureVault` 扩收金**（`VaultPaths` ＋`res_currency`）——结构就位（⛔ 无行为变化）。
2. **断分叉**：`GetResourceValue` 的金分枝改走 `TreasureVault.Get(kingdomId)?.GetAmount(Gold)`（`:311` 删）。此时 `Gold` 字段仍是**真源**（写侧未动）⇒ ⚠️ **双真源窗口** ⇒ 本步须与第 3 步**同一提交**（下方说明）。
3. **改写侧**：`ModifyResource` 金分支（`:233-241`）退役 ⇒ 走仓；同步改 `:358`（难度初始化）／`:197`（`ResetState`）／`:415`（`LoadState`）三处写点。
4. **转门面**：`Gold` 属性改 `=> GetResourceValue(ResourceType.Gold)`（只读兼容读口·⛔ 字段删除）⇒ **6 处 UI 读点零改**。
5. **存档**：`RulerSaveData.gold` 停写（字段保留占位·见 `Q5`）＋ 迁移桥。
- ⚠️ **第 2+3 步必须原子**（同一提交）：否则"读仓·写字段"期间玩家金会**读 0**（探针须覆盖：改后 `ResourceHUD` 读数非 0 ＋ 扣费后减）。

### `Q5` 存档迁移 —— **做轻量迁移桥**（⛔ 不写完整迁移器）

| 方案 | 做法 | 代价 | 本端倾向 |
|---|---|---|---|
| 甲 **迁移桥** | `LoadState` 时 `if (data.gold > 0) TreasureVault.Get(0)?.Deposit(Gold, data.gold)`（AI 侧同理从 `KingdomEntryData.resources` 取金分量） | ≈ 10 行·零 bump | ⭐ **建议** |
| 乙 弃档（`M1-A` 口径） | 旧档金归 0（`D788` §4「旧档可作废」） | 0 行 | 备选 |

- **理由**：`M1-A` 选"弃档"是因**非金结构变了**（`ResourcePack` 8 桶 ⇒ `ResourceList`＋桥已退役）；而金的迁移是**同构搬运**（单 `int` ⇒ 仓内一个条目）⇒ 成本极低、收益是玩家最敏感资源的连续性。⛔ 两案都**不写版本 bump**。
- ⚠️ 衔接：若裁甲，`RulerSaveData.gold` **保留字段但停写**（旧档读取仍有效）；若裁乙，字段可删（但会破坏旧档反序列化容错 ⇒ 仍建议保留占位）。

### `Q6` AI 侧退化行为 —— **读 0／写丢弃＋告警**

- **结构事实**（可达性读数）：AI 主城**存在**（第一代立国预置 `castle`；动态立国 `PlaceCampCastle:460`）⇒ `CastleCoreComponent.Init:156` 挂 `TreasureVault` ⇒ **常态：`Get(kid)` 可达** ✓；主城 `Die`（`Destroy`）⇒ `TreasureVault.OnDestroy:135-143` 注销 ⇒ `Get(kid)=null` ⇒ **退态可达**。
- **建议**：
  · **读**：`Get(kid)==null ⇒ 0`（UI/诊断显示 0·不崩）。
  · **写**（税/贸易/退款入账）：`丢弃 ＋ Debug.LogWarning("[M1-E] k{id} 国库未就绪，金 {amount} 不入账（主城未生成/已毁）")` —— 对齐**既有先例**「`TaskScheduler:714` 国已注销 ⇒ 丢弃+日志（资敌防线）」。
  · ⛔ **不引入** `KingdomState.pendingGold` 类缓冲（第二真源·与 `M1-A` 禁双写红线同族）。
- **理由**：主城没了 ⇒ AI 经济停摆**正是设计后果**（`B1` 主城倒 ⇒ 国库掉箱 ⇒ 玩家抢走）；缓冲兜底会违背"抢国库"语义。⚠️ 行为变化（AI 主城被毁后其收入不再累积）⇒ **须在交付时显式声明**（照 `M1-D` 的公示纪律）。

### `Q7` ×1.5 同批 —— ⭐ **建议同批**

- **理由**：① `M1-E` 后金能进死者背包（工人搬金＝普通资源 ⇒ `WorkerInventory` 可携金）⇒ **有生效对象**；② 契约 `§9.9` 已定口径（判据=击杀者是兽人·**只乘金**·多出 0.5 属**生成**不受守恒约束）；③ 留"注释位"跨批⇒易漏（`L-51` 家族）。
- **落点**：`DamageSystem.TrySpawnOrcLoot`（`M1-D` 已把 `pack` 组装收口在此·`:642`）：若 `pack` 含 `Gold` 且 `killer.raceId==Orc` ⇒ `Gold = Mathf.RoundToInt(gold * 1.5f)`（`RoundToInt` 与 `Refund` 同口径）。
- **代价**：≈ 3 行 ＋ 探针 1 条（正：兽人杀带金死者 ⇒ 箱金=⌈1.5×⌉；负：非兽人 ⇒ 不乘）。

### `Q8` `U-7`／`O-2`

#### `U-7`（`Well.asset:32-33`）—— ⭐ **必须并入**（阻塞级）

- **为什么必须**：`M1-E` 后金进仓 ⇒ `WarehouseRegistry.FindNearestAvailable(Gold, …)` 开始生效 ⇒ 而**全库唯一"声明收金"的仓就是水井**（`Well.asset:32-33` ⇒ `StorageComponent.Accepts(Gold)=true`）⇒ **工人搬金会卸进水井**（水井不是消费口 ⇒ 金"消失"于死仓）⇒ 直接破坏 `B1`（金进国库）。
- **⚠️ 修法的坑（本端实读）**：`WarehousePaths.Normalize([])` ⇒ **通用仓 `res`（收全部）**（`StorageComponent:19/86`）⇒ ⛔ **"删掉那行"是错的**（水井会变成收一切的通用仓，比误配更糟）。
- **三案（待裁）**：

| 案 | 做法 | 优 | 劣 |
|---|---|---|---|
| 甲 ⭐ | 声明 `res_fluid`（水**族**前缀·取自 §4.3 D 组「水井有仓」的未来形态） | 与 D 组（水将来走普通仓）**前瞻一致**；现在收不到任何东西（表内无水）⇒ 天然"打不着金" | 「族前缀」是从 D 组 `res_fluid.water`（⏳ 完整标签待定）**推断**（⛔ 未编造后缀，但仍是推断） |
| 乙 | 等水落码批（`M1-F`/`M1-G`）一并改 | 零推断 | ⛔ **`M1-E` 期间金会进水井** ⇒ 阻塞 |
| 丙 | 声明一个显式"不收"的哨兵路径 ＋ 注释 | 语义最直白 | 引入新字面（09 无此约定） |

⇒ 本端倾向**甲**（并请策划端核「`res_fluid` 族前缀」是否可先落）。
#### `O-2`（国库满 ⇒ 溢出装箱 ⇒ 搬回 ⇒ 再溢出）—— **金侧自动消解／材料侧归 `M1-G`**

- **回路机制坐实（玩家侧·材料）**：`TreasureVault.Deposit:105-112`（满 ⇒ `SpillToChest` 装箱）→ 工人搬箱（链 A）→ `UnloadInventory:805-818`（`FindNearestAvailable`：国库满 ⇒ `CanAccept=0` 排除）→ 无他仓 ⇒ `AddGatherOverflow:702-734` → 非金 ⇒ `ModifyResource(type,true)` → `TreasureVault.Deposit` → **又溢出** ⇒ ∞（工人被回路占用）。
- **契约依据**：`09 §8.2 :340`「『满则入国库兜底』**删除**（仓库满了就满了）」＝ `09#41`。
- **⭐ 金侧为何自动消解**：金 `Volume=0` ⇒ `CanAccept=MaxValue` ⇒ **国库对金永不"满"** ⇒ `Deposit` 金**不产生溢出** ⇒ 金不入回路。
- **建议**：**材料侧回路 ⛔ 不并入本片**（`#41` 是独立差异编号 ⇒ 属"兜底口径批"）⇒ **维持挂 `M1-G`**；`M1-E` 交付报告里**声明金侧已消解**。⚠️ 若策划端坚持并批 ⇒ 面 = `UnloadInventory` 兜底删除 ＋ `AddGatherOverflow` 收敛 ＋ `SpillToChest` 语义（**三处·跨 `M1-C`/`M1-D` 已验收代码**）⇒ 本端不建议混批（回归面太大）。

---

## 五 · 五组取证读数（任务书 §五）

### 1. 金的读写面全扫（生产 ＋ Editor · 逐点标「本片是否需改」）

**① `Gold` 字段**（`RulerController:48`）
| 点 | 方向 | 本片 |
|---|---|---|
| `:235/:237`（`ModifyResource` 金分支） | 写 | ⭐ **改**（走仓·退役分支） |
| `:145`（`ApplyRulerData` 初值） | 写 | ⭐ **改**（改走仓·与 `:358` 合并） |
| `:197`（`ResetState`） | 写 | ⭐ **改**（`TreasureVault.ResetAll` 已含金 ⇒ 删此行） |
| `:358`（难度初始） | 写 | ⭐ **改**（`Deposit(Gold, …)`） |
| `:415`（`LoadState`） | 写 | ⭐ **改**（迁移桥 ⇒ 入仓） |
| `:392`（`SaveState`） | 读 | ⭐ **改**（停写/或写仓快照不在此） |
| `:311`（`GetResourceValue` 分叉） | 读 | ⭐ **改**（断分叉＝`Q4` 步 2） |
| `:374`（日志） | 读 | 可保留（或改读仓） |
| 外部 6 处（`TopLeftHUD:82`／`ResourceHUD:96`／`TrainingPanel:260`／`GameOverPanel:87`／`TradeSystem:128`／`RanchSystem:97`） | 读 | ⛔ **零改**（`Q4` 第 4 步保门面） |
| Editor（`Valley_P1_Observer:225/242` 等） | 读 | ⛔ 零改（同上；`KingdomState.GetResourceValue` 转发） |

**② `ModifyResource`**：生产 17 点（定义 1＋内调 2＋外部 14）
- ⭐ **需改（金参数）6**：`TaxSystem:117`（+金）／`TradeSystem:101`（+金）/`:130`（−金）／`RanchSystem:98`（−金）／`TrainingSystem:507`（−金）／`AIDebugUIManager:737`（可变）
- ⛔ 零改（非金）8：`TradeSystem:100/131`／`RanchSystem:176/203`／`TrainingSystem:508/509`／`StorageComponent:263/281/326`／`VagrantCampSystem:250`／`SatietySystem:218`／`SiegeWorkshopBuilding:173`／`TaskScheduler:708`
- Editor ~18 点（`Valley_HH109/107/111/316/OB12/2_17_Smoke×2`）⇒ ⛔ 零改（编译面：默认参数）
- ⚠️ **签名加尾参 ⇒ 两类全零改**；"需改"的 6 点只改**内部行为**（同一调用自动路由到该国的仓）。

**③ `GetResourceValue`**：`RulerController:309`（⭐ **改 1 行**）＋ 16 内部读口 ＋ 外部 4（`SatietySystem:213`／`HappinessSystem:221`／`RanchSystem:103`／`TrainingSystem:493`）＋ Editor ~10 ⇒ **除定义处，全零改**。

**④ `CanAfford/Spend/Refund`**：玩家 ~18 点 ＋ AI ~12 点（见 `Q3` 表）⇒ **全零改**（薄壳＋内部转发）。

### 2. AI 侧全扫（읽/写面）

| 面 | 点 | 备注 |
|---|---|---|
| `KingdomState.resources` 写 | `KingdomFoundry:72`（立国 stockpile）／`AddResources:182`／`Spend:151`／`Refund:173` | ⭐ **金条目要转仓**（`#52` 金面） |
| `KingdomState.resources` 读 | `GetResourceValue:144`（`resources.Get`）／`KingdomRegistry:148/191`（存档） | 金 ⇒ 转发仓（读口不变） |
| 副产三桶 `crystal:54/fireOil:56/ore:58` | 写：`TaskScheduler:720-722`；读：`GetResourceValue:141-143` | ⛔ 本片不动（`#52` 非金面 ⇒ `M1-G`） |
| `AbstractEconomySettler`（纯 C#） | `delta.Gold +=`（`:172` 人头税） | ⛔ 不动（纯函数·产出增量由适配层落账） |
| `AbstractEconomySettlement.ApplyDelta:227-236` | `k.AddResources(…Gold…)` | ⭐ **金条目转仓**（金进 AI 国库） |
| `AIEconomySettlement`（Fine 段） | `CollectEconomyResources:107-117`（白名单含 `Gold`）／`:55 AddResources` | ⭐ 金条目同上（AI 建筑产金恒无 ⇒ 行为不变·口径统一） |
| `EconomyDiagnosis` | `RegisterFlow:212`（金收支窗口）／`StockGold` | ⛔ 不动（诊断口径照旧·`KingdomState.Spend/AddResources` 转发后仍在窗口内） |
| AI 消费面 | `SiegeProductionSystem:214-226`／`SatietySystem:220`／`RanchSystem:104/181`／`TrainingSystem:514`／`BuildController:363/372`／`KingdomBrain:883/1120`／`PlacementValidator:162` | ⛔ 调用点零改（走 `KingdomState.Spend` 薄壳） |
| AI 税/入账 | `TaxSystem:162` | ⭐ 金转仓 |

### 3. 存档面（三者迁移关系）

| 载体 | 现字段 | 读写点 | `M1-E` 后 |
|---|---|---|---|
| 玩家金 | `RulerSaveData.gold`（`RulerController:425`） | `:392` 写／`:415` 读 | ⭐ **停写** ⇒ 迁入**玩家主城国库容器**（`BuildingSaveData.treasuryContents`） |
| AI 账本（含金） | `KingdomEntryData.resources`（`KingdomRegistry:233`） | `:148` 写／`:191` 读 | ⭐ **金分量迁出**（非金留台账·`Q0` 边界） ⇒ 迁入 **AI 主城国库容器** |
| 国库容器 | `BuildingSaveData.treasuryContents`（`:36`） | `Building:1125` 写／`:1226` 读／`BuildingFactory:344`（`SpawnFromSave`） | ✅ 载体已就位（`M1-A`）·金随它走 |

⇒ **迁移关系一句话**：金从「**两个 Ruler/Kingdom 专用字段**」并入「**建筑存档的国库容器**」——三者变两者（玩家 Vault＋AI Vault），**`M1-A` 已把容器与存档铺好** ⇒ `M1-E` 只需"字段 ⇒ 容器"的搬运 ＋ 一次读档桥。

### 4. `U-7`／`O-2` 现状读数

- **`U-7`**：`Well.asset:32-33 warehousePaths: [res_currency.gold]`（M1-A 按 `outputResource` 占位值迁移的产物）／`:38 capacity: 0`（⇒ `RefreshCapacity` 落 100）／`:49 outputResource: 0 = Gold 占位`（但 `ProducerComponent:38-47` 水井特判 ⇒ **不产金**）⇒ **唯一收金仓**（`WarehouseRegistry.FindNearestAvailable(Gold)` 会命中它）。
- **`O-2`**：回路三件套（`SpillToChest`／`UnloadInventory`／`AddGatherOverflow`）见 §四-Q8；⭐ 金侧体积 0 ⇒ **结构性消解**；材料侧 ⇒ `M1-G`。

### 5. 波及面清单（改「目标仓」参数牵动多少调用点）

- **总面**：≈ **100 处 API 调用点**（生产 ≈ 60 ／ Editor ≈ 40）——⭐ **按尾参默认值法 ⇒ 需改动的调用点 ≈ 0**；真正改动集中在 **`RulerController` 内部 6 处 ＋ `TreasureVault` 1 行 ＋ `KingdomState` 金条目转发 2 处 ＋ 连带 2 处（`UnloadInventory`/`ResolveChestDest` 金直通退役）**。
- **⚠️ 连带施工点（本端新增列报·任务书未列）**：`TaskScheduler:797-804`（金直通分支）／`:836-841`（`ResolveChestDest` 金跳过找仓）／`ChestEntity:154-167` 注释（提到金 → `Gold` 字段·须同步勘正）⇒ 三处注释/分支随 `M1-E` 切换。

---

## 六 · 列报（超范围观察 · ⛔ 本批不改）

1. **`WarehouseHelper` 的 `kingdomId` 重载生产零调用**：`TrySettle(int, …):26`／`CanAfford(int, …):77` 有定义与文档，但生产调用点全走 `TrySettle(cost)`（`BuildingPanel:321`）或被 `kingdomId<=0` 守卫排除（`BuildController:352/361/369`）⇒ AI 侧扣费全部走 `KingdomState.*` ⇒ **该重载疑为"备而未用"**（`M1-G` 账本口径批可顺带收）。
2. **`Gold` 门面保留 vs 契约"字段退役"**：契约 `:173` 说"字段退役 ⇒ 读口改 `GetResourceValue(Gold)`"；本端建议**保留 `Gold` 为转发属性**（⇒ 6 处 UI 读点零改）——⚠️ 字面上"字段"（存储）确已退役，门面保留属实现选择 ⇒ 请裁（若要求彻底删 ⇒ 6 处读点改读 `GetResourceValue`，代价 +6 行）。
3. **`TreasureVault` 类注释 `:18-19` 与 `:93`**（"本片不做：金仍走直通 ⇒ 本容器不收金"／"金走 Ruler 直通不调用本类"）⇒ `M1-E` 施工时**须一并勘正**（否则留下反向注释）。
4. **AI 主城被毁后 AI 收入不再累积**（`Q6` 退化行为的行为变化）⇒ 交付时显式声明（对齐 `M1-D` 的"行为变化公示"纪律）。

---

## 七 · 红线自查 ＋ 停手点

| 红线 | 自查 |
|---|---|
| ⛔ `Assets/**` 一行不动 | ✅ 本轮**零写**（全部只读：`read_file`／`search_*`） |
| ⛔ 不开工 | ✅ 未写任何生产码 |
| ⛔ 不 push | ✅ |
| ⛔ 不代提交策划端账本 | ✅ 未触 `_编号登记.md`／`_任务队列.md`／`_当前快照.md`／`_测试基线台账.md`／`_策划教训库.md` |
| 报告落盘 | ✅ `多Agent交接/执行端/HH.318_M1-E金币降普通资源_评估报告.md`（本报告自行 commit） |

**停手点**：`Q0`~`Q8` 全裁后再出施工任务书；若 `Q0` 裁"`#52` 全并"或 `Q8` 裁"`O-2` 同批"⇒ 本端将先补一轮专项取证（面/回归读数）再施工。

---

*执行端 · 2026-09-21 · `HH.318` 只读评估完毕（`Q0`＋`Q1~Q8` 全部给出建议与代价 · 5 组取证齐 · `Assets/**` 零写）*
