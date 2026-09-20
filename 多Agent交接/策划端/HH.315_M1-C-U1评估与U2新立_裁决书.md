# HH.315 · `M1-C` · `U-1` 只读评估 ＋ `U-2` 新立 —— 裁决书

- 任务号：`HH.315` · `U-1` 评估（承 `cf206746` `D794` 验收「⚠️ 有条件通过 · ⛔ 不销号」）
- 被裁文书：`多Agent交接/执行端/HH.315_M1-C-U1评估_交付报告.md`（commit `d4c5fd7e` · 326 行）
- 本端性质：**只裁决 · ⛔ 零代码零资产**
- 日期：2026-09-20（`D795`）
- 取证手法：**判据三直读** —— ① 现码 `file:line` ② `.asset` 真资产逐值 ③ 枚举/常量定义处 ④ 全库调用面机械扫（⛔ 不采信报告转述）

---

## 〇 · 本端独立取证（18 项 · 全部自读）

### 0.1 `U-1` 两落点现码（✅ 与报告逐字一致）

| 落点 | 现行行 | 算式（实读） |
|---|---|---|
| 落点 1 `BuildRefundPack()` | `Building.cs:878-892` | `:881 costSum = def.cost.TotalCount` ／ `:882 invested = totalInvested>0 ? totalInvested : costSum` ／ `:883 baseSum = Mathf.Max(1, costSum)` ／ ⭐ **`:885 for (i < def.cost.items.Length)`**（遍历集＝仅基础造价）／ `:888 amount = Mathf.FloorToInt((float)invested * e.amount / baseSum)`（⛔ 无 `hp/maxHp`） |
| 落点 2 `GetRepairCost()` | `Building.cs:1055-1075` | `:1066 baseSum = Mathf.Max(1, costSum)` ／ `:1071 amount = Mathf.RoundToInt((float)total * e.amount / baseSum)` ⇒ 同型 |

⭐ **共同结构** ＝ **分子「累计投入（含升级）」／分母「仅基础造价」／遍历集「仅 `def.cost.items`」**。报告 §1.1 陈述**成立**。

### 0.2 `totalInvested` 根因证据（`Building.cs:140-146` 实读）

```
累计投入资源量（2_12 步骤7 / D155：修复成本 = 累计投入 × SO 比例）。
建造首付/升级费累加；拆除返还（D162）读它；修复按它算 D155。入档（BuildingSaveData）。
用单一近似总量（resourcepack 求和）而非四资源分账——返还/修复粗糙按比例缩放同一 pack。
```

⇒ ⭐ 报告 §1.5 的 **L3 真根因成立**：`totalInvested` **自 `D162` 起就是「标量近似」**，字段注释**自己写着**"粗糙按比例缩放同一 pack"。`U-1` 不是算法笔误，是**近似设计的到期**。

### 0.3 `.asset` 逐值（真资产 · 复算 `U-1` 样本）

| 资产 | `cost.items` | `levels[0].upgradeCost` | `levels[1].upgradeCost` |
|---|---|---|---|
| `House.asset:20-23/46-52/54-60` | `{type:2 → Wood, 4}` | `{Stone 6, Wood 10}` | `{Stone 16, Wood 24}` |
| `Warehouse.asset:20-25/49-55/57-63` | `{Gold 4, Stone 4}` | `{Stone 10, Wood 10}` | `{Stone 20, Wood 24}` |
| `Well.asset:20-23` | `{Stone 6}` | `levels: []` | — |

（`ResourceType`：`Gold=0, Stone=1, Wood=2` —— 按 `ResourceCatalog` 表序实读，⛔ 非记忆）

### 0.4 枚举／常量定义处（⭐ 本轮最关键 · 一次性改判两处）

| 定义处 | 实读 | 推论 |
|---|---|---|
| `BuildingDef.cs:168` | `public enum ProduceKind { Resource, Unit }` ⇒ **`Resource = 0`** | Well `producer.kind: 0` ⇒ **＝ `Resource`** |
| `ResourceCatalog.cs:167-174` | `Normalize`：`declared 空 ⇒ AllOnly`＝`["res"]`（通用仓） | 空声明 ⇒ 通用仓 |
| `ResourceCatalog.cs:102-110` | `PathMatches`：段边界 `_`／`.`（`:99` 例：`res` 命中 `res_material.stone`）⇒ `res` 亦命中 `res_currency.gold` | `res` 是 `res_currency.*` 的上位前缀 |
| `StorageComponent.cs:85-93` | `RefreshCapacity`：`capacity = def.producer.capacity > 0 ? Max(1, Round(...)) : 100` | ⭐ **`producer.capacity = 0` ⇒ 仓容 100** |
| `StorageComponent.cs:135-142` | `CanAccept`：`if (vol <= 0) return int.MaxValue;`（注释「§4.2 金币用例」） | ⭐ **金体积 0 ⇒ 任何 `Accepts(Gold)` 的仓都能收，与容量无关** |
| `BuildingFactory.cs:214-238` | 分支① `rate<=0 && capacity>0 && role==Economy && outputResource!=Gold`；分支② `rate>0 && kind==Resource && !isResourceNode`（② 内 `isSiegeWorkshop` **改挂专属组件，⛔ 跳过通用 `StorageComponent`**） | 见 §三 勘正 |

### 0.5 全库机械扫（`warehousePaths` 40 栋）

- **非空声明 7 栋**：`AdvancedStorage→res_material.wood`／`Blacksmith→res_material.metal`／`farm→res_food.grain`／`Granary→res_food.grain`／`quarry→res_material.stone`／`Warehouse→res_material.wood`／⭐ `Well→res_currency.gold`
- **空声明 33 栋**（`warehousePaths: []`）
- ⛔ **全库无任何一栋声明 `res`**（须留意 §三 勘正 2）

### 0.6 同族算式回扫（`L-45`）

`git grep "baseSum|SumCostOf|IsRefundResource"` ⇒ **生产码同族算式仅 2 处**：`Building.cs:888` ＋ `:1071`（`:911`／`:1052` 为「已删」说明注释）。`SumCostOf`／`IsRefundResource` **零生产命中**（已随 `M1-C` 件 2 退役）。
⇒ 报告 §5.1「无第三处未被发现者」**成立** ✅

### 0.7 箱子链（`U-2` · 三段全实读）

| 段 | 实读 | 判定 |
|---|---|---|
| `ChestEntity.cs:11` | `class ChestEntity : MonoBehaviour, IInteractable` | ⛔ **非 `ITaskSource`**（报告 ④ 成立） |
| `ChestEntity.cs:58-63` | `Interact(ctx) { ChestManager.Instance.Pickup(this, ctx); return InteractionResult.None; }` | ⭐ **返回值恒 `None`，`Pickup` 结果被丢弃** |
| `ChestManager.cs:130-136` | `public ResourceList Pickup(...) { var got = chest.contents; Remove(chest); return got; }`（注释：「**返回值=拾取到的资源包（供调用方入背包）**」） | ⭐ **已取走 ＋ 已 `Destroy`，只把结果交出去** |
| `ChestManager` 全库引用点 | `DamageSystem:623/633`／`WorldLifecycle:45`／`MapGate:618-621`／`MonsterController:62/67`／`ChestEntity:61/71`／`Building:866/868/904/906`／`TreasureVault:117/122` | ⛔ **`TaskScheduler` 零引用**（报告 ④ 成立） |
| `MapGate.cs:577-626` | `PickAt` 只是**选择**（返回 `MapPickHit.source`），⛔ 不做拾取动作 | 玩家点箱 ⇒ 上层调 `Interact` |
| `ChestManager.cs:36-48` | `Update` 过期扫描 ⇒ `curDay - bornDay >= expire ⇒ Remove(c)`（⛔ **不洒落**） | 过期＝销毁内容 |

---

## 一 · ⚠️ 本端自纠（`D794` 裁决书算术错 · 本端认责）

**原判**（`cf206746` 裁决书 §二）：`House` 满级「应 `{Wood:54, Stone:22}`」。
**实读**（§0.3）：`Wood = 4 + 10 + 24 = ` **38**；`Stone = 6 + 16 =` **22**；Σ ＝ **60** ＝ `totalInvested`（自洽）。
⇒ **原判 54 错**（54 ＋ 22 ＝ 76 ≠ 60 ⇒ 该行**内部不自洽**，疑把 `levels[1]` 的 Stone 16 记进了 Wood）。执行端 §1.4 勘正**成立**，本端接受并回写。

⚠️ **影响范围**：仅「应为」基数；**`U-1` 成立性不变**（`House` L1 行 `{Wood:14, Stone:6}` 原判正确）；**验收结论不变**。
⚠️ **根因**：本端当时按"倍数反推常数"（`MEMORY` 已列的禁项）⇒ `54 = 60 - 6` 之类的减法凑数，⛔ 未直读 `levels[]` 逐项。**已入 §六 `L-46`。**

---

## 二 · 报告成立项（逐条 · 本端已独立复现）

| # | 报告结论 | 本端复现 |
|---|---|---|
| 1 | `U-1` 两落点成立 | ✅ §0.1 |
| 2 | ⭐ **甲 ≡ 乙，且退化为恒等式** | ✅ **数学成立**：若分母与分子同源（皆＝"投入总额"）⇒ `floor(a × e.amount / a) ≡ e.amount` ⇒ **退还恒等于 `def.cost` 原样**，与投入/等级无关。⚠️ 且**最致命的是分母怎么改都不动「遍历集＝`def.cost.items`」**（§0.1 `:885`）⇒ 升级类型**永不出现** ⇒ **张冠李戴照旧** |
| 3 | 甲／乙 不可用；丙 方向正确 | ✅（裁定见 §五 `C-1`） |
| 4 | 丙′ 须加投料中态分流 | ✅ `Demolish` 无状态守卫（`:844-849` 只挡 `Dead/Placing`）⇒ 投料中可拆 ⇒ 不分流则**双重退还**（`BuildRefundPack` 全额 ＋ `DropSiteStoreToChest` 仓内容） |
| 5 | 代价：1 文件 2 方法 · 零新增字段 · 不触存档 | ✅ 所需输入 `level`／`siteContents` **均已在场**（`BuildingSaveData.cs:57/59/61/63/65`） |
| 6 | `Warehouse` L1「张冠李戴」最干净证据 | ✅ `cost={Gold:4,Stone:4}` ＋ `levels[0]={Stone:10,Wood:10}` ⇒ 应退 `{Gold:4,Stone:14,Wood:10}`；现码 `baseSum=8, invested=28` ⇒ `Gold 14 ／ Stone 14` ⇒ **Wood 10 凭空消失、Gold 凭空多 10** |
| 7 | 同族算式全库仅 2 处 | ✅ §0.6 |
| 8 | `U-2` ④「箱无工人搬运链」 | ✅ `ChestEntity` 非 `ITaskSource` ＋ `TaskScheduler` 零 `ChestManager` 引用 ＋ `KingdomTaskType` 无取箱类 |
| 9 | 弹药不外溢 | ✅ 40 栋造价零弹药（与 `D794` 读数一致） |
| 10 | `L-45` 回扫无第三处 | ✅ §0.6 |

---

## 三 · ⛔ 报告须勘正项（2 条 · 本端实读推翻）

### 勘正 1（⚠️ 报告内部自相矛盾 · 以实读为准）

**报告 §5.4 ③ 原文**：「全库声明 `res_currency` 的仓**仅 `Well.asset:33`**，而 Well 是**死仓**（`ProducerComponent.cs:69-76` `_isWell` 早返回 ⇒ **不挂 `StorageComponent`**）」

⛔ **后半句错**：
- `ProducerComponent._isWell` 早返回的语义是「**不产金**」（`QQQ.2 T15`：water 入网、跳过产金），⛔ **与「是否挂 `StorageComponent`」无因果**；
- 挂仓判定在 `BuildingFactory.AttachComponents`（`§0.4`）⇒ Well `rate=4>0` ＋ `kind:0 = ProduceKind.Resource`（`BuildingDef.cs:168` 实读）＋ `isResourceNode:0` ⇒ ⭐ **命中分支②，确实 `AddComponent<StorageComponent>()`**；
- 且 `RefreshCapacity`：Well `producer.capacity: 0` ⇒ ⭐ **仓容实为 100**（`StorageComponent.cs:90-92`）；
- 且 `CanAccept`：金 `vol=0` ⇒ ⭐ **`int.MaxValue`**（`:139`）⇒ 金能收。

⇒ ⭐ **`Well` 是一个「容量 100 · 只收 `res_currency.gold` · 已注册 `WarehouseRegistry`」的活仓**（`:61 WarehouseRegistry.Register`）。

⚠️ **注意**：执行端在本轮**对话里已自纠**（「原判『金无仓可收』作废 —— 实测 `Well` 是真挂仓」），但**报告正文（`d4c5fd7e`）仍写着错误版本** ⇒ 报告与自纠冲突，**以报告为准的读者会被误导**。⇒ 须回写报告勘正。

### 勘正 2（「通用仓 `res` 亦收金」论据不成立 · 但结论侥幸成立）

**报告／自纠口径**：「通用仓 `res` 亦收金」。
**实读**（§0.5）：**全库 40 栋，⛔ 无一栋声明 `res`**；`Normalize` 只把**空声明**变成 `["res"]`，而空声明 33 栋**能不能挂仓**取决于 `AttachComponents` 两支 —— 实读结果：
- 走分支① 的 `Granary`／`Warehouse` ⇒ 二者 `warehousePaths` **非空**（`res_food.grain`／`res_material.wood`）⇒ 空声明**不上台**；
- ⚠️ 唯一"空声明 ＋ 走分支②"的候选是 **`SiegeWorkshop`**，但分支② 内它**改挂 `SiegeWorkshopBuilding`，注释明写「跳过通用 `StorageComponent` 挂载」** ⇒ 通用仓**不存在**。

⇒ **「通用仓 `res`」这条论据 ⛔ 不成立**；但 **「金有仓可收」的结论仍成立** —— 唯一实际来源是 **`Well`**（`res_currency.gold` · 容量 100）。
⇒ ⭐ **`U-2` ③「无仓接受金」＝ 不成立**（须撤销），但**须换成正确论据**（`Well`，⛔ 不是"通用仓"）。

---

## 四 · ⭐⭐ 本端新发现（`U-2` 深化 —— 比报告定的"卡在箱里"更严重）

### 4.1 `Pickup` 的返回值**全库零消费方**

| 事实 | `file:line` |
|---|---|
| `Pickup` **返回** `ResourceList`（注释明写「供调用方入背包」） | `ChestManager.cs:128/135` |
| `Pickup` 内部**已 `Remove(chest)`**（`Destroy` GameObject） | `ChestManager.cs:134` |
| ⭐ **全库唯一调用点 ＝ `ChestEntity.cs:61`，且返回值被丢弃**（`Interact` 恒返 `InteractionResult.None`） | `ChestEntity.cs:58-63` |
| `MapGate.PickAt` 只**选择**（返回 `source`），⛔ 不做拾取 | `MapGate.cs:577-626` |

⇒ ⭐ **箱子内容物**（不只是金）**没有任何「入背包」的代码路径** ⇒ **玩家一点拾取，内容物连同实体一起蒸发**。

### 4.2 三条出口全灭 ⇒ 退还物**必然丢失**

| 出口 | 实读 | 结果 |
|---|---|---|
| ① 玩家拾取 | `Interact` 丢弃返回值（`ChestEntity.cs:62`） | 🔴 **内容销毁（净丢失）** |
| ② 工人搬回 | `ChestEntity` 非 `ITaskSource`；`TaskScheduler` 零 `ChestManager` 引用 | ⛔ **该路径不存在**（`09` §16.1-4 明写「**要工人搬回**」⇒ **契约未实现**） |
| ③ 过期 | `ChestManager.cs:46 Remove(c)`（⛔ **不洒落**，与 `Strike→ResetDrop` 不同） | 🔴 **内容销毁** |

⇒ ⭐ **退还物 100% 丢失**（不是"卡在箱里等玩家"，玩家一碰就没了；不碰则到期消失）。

### 4.3 ⇒ 对 `M1-C` 的裁定性影响：**件 3 未闭合**

- `09#55`（件 3 依据）＝「退还**直接进国库** ⇒ 改**随本体掉箱**」；
- `09` §16.1-4 同段明写「落在箱子里，**要工人搬回**」⇒ **件 3 的完整语义 = 掉箱 ＋ 搬回**；
- 实读：**搬回链不存在**，且掉箱后的两条出口**都销毁内容**。
- ⇒ ⭐ **件 3 在功能上等价于「拆房退还全部作废」** —— 比改前（直入国库 · 必到账）**是倒退**。

⚠️ **`U-2` 缺陷本体是既有的**（`ChestEntity` 属 `D269` 时代，⛔ 非 `M1-C` 引入）；**但 `M1-C` 件 3 把它从"边缘路径"（怪物掉落／溢出）提升为"退还主路径"** ⇒ **本片有责任把它一并闭合**。

⇒ ⭐⭐ **`M1-C` 验收结论须再下调一档**：`D794` 定的「⚠️ 有条件通过（`U-1` 待修）」⇒ 本轮改为
> **⚠️ 有条件通过 · ⛔ 不销号**，且**待修项从 1 笔（`U-1`）增为 2 笔（`U-1` ＋ 件 3 依赖 `U-2`）**。

---

## 五 · `C-1` ~ `C-6` 裁决

| # | 争点 | 裁决 | 说明 |
|---|---|---|---|
| **`C-1`** | `U-1` 修法选型 | ✅ **准「丙′」**（＝丙 ＋ 投料中态分流）；⛔ **否决甲／乙** | 理由见 §二-2：分母怎么改都不动遍历集 ⇒ 甲乙只把"逐类型超发"换成"系统性少退"（`House` 满级少退 56／`Warehouse` L1 少退 20），**张冠李戴照旧**。丙 是**唯一**能同时修好「落点 1 ＋ 落点 2 ＋ 张冠李戴」者，且零新增字段／零存档变更／零旧档影响 |
| **`C-2`** | 丙′ 附带「修复费不再复利」 | ✅ **准 · 一并纳入丙′**，但 ⭐ **单列挂账 `U-3`** | 现码 `GetRepairCost` 以 `totalInvested` 为基数，而**修复花费本身被 `AddInvested` 累加**（`BuildingPanel.cs:322/340` ＋ 投料逐笔 `ConstructionSiteStore.cs:140`）⇒ 每次重建后修复费递增（`ratio=0.5` ⇒ `C → 1.5C → 2.25C → 3.375C…`）。⚠️ 这是**独立于 `U-1` 的第二个缺陷**（即使 `U-1` 用别的修法也存在）⇒ 立 `U-3` 留痕，但**实现上随丙′ 一次改到位**（同方法） |
| **`C-3`** | 丙′ 后 `totalInvested` 零读点 | ✅ **裁 (a) 保留** | 理由：① 仍**入档**（`Building.cs:949`／`BuildingSaveData.cs:45`）＋ 打日志（`:576`）⇒ 非死字段；② (b) **动存档格式** ⇒ 赛期内不划算；③ 后续 `M1-D`／AI 结算可能复用 ⇒ 标"**备而未用**"（⛔ 不预优化删除）。⚠️ **须同步改注释**：删「返还/修复基数」表述（转 `U-1` 修法后该字段不再是任何算式输入） |
| **`C-4`** | `U-2` 归属 | ✅ **另立独立片**（⛔ 不并入 `M1-C` 补丁片）；⚠️ **但升级为阻塞级**，且 **`M1-C` 件 3 判「未闭合」** | 理由：`U-2` ＝「箱 → 仓搬运链」＋「拾取入包」＝**新机制**（新 `ITaskSource` 实现 ＋ `KingdomTaskType` 扩展），⛔ 不是补丁级。⚠️ 与执行端"本片只留挂账"的差别：**件 3 依赖 `U-2` ⇒ 不能只挂账了事**，须在 `M1-C` 账上标"件 3 未闭合"。⇒ `M1-C` **保持 ⛔ 不销号**，且**待修项 2 笔** |
| **`C-5`** | 陈注释 6 处 | ✅ **准**（随 `U-1` 一并勘正 · 零行为） | 另**加 1 处**（执行端漏列）：⭐ `Building.cs:142-143` 字段注释「**用单一近似总量…返还/修复粗糙按比例缩放同一 pack**」—— 这是**根因文档**，丙′ 后**必须改写**（否则注释描述的是已退役机制） |
| **`C-6`** | 勘正 House 满级 38 | ✅ **准**，且 ⭐ **本端自纠**（§一） | 回写 `D794` 裁决书 ＋ 本轮裁决书留痕 |
| ⭐ **`C-7`** | （本端新增 · 裁报告勘正 2 条） | ✅ 报告 §5.4 ③ 须勘正：**`Well` 不挂 `StorageComponent` ＝ 错**（应＝挂；容量 100；金 `CanAccept` ＝ `MaxValue`）；「通用仓 `res` 亦收金」论据**不成立**（全库零 `res` 声明）⇒ 但 `U-2` ③ 结论**仍是"金有仓可收"**（来源＝`Well`） | 见 §三 |

---

## 六 · 新立挂账 ＋ 教训

### 6.1 挂账

| 编号 | 内容 | 归属 | 状态 |
|---|---|---|---|
| **`U-1`** | 拆除/修复退还「分子分母口径错配」超发·张冠李戴 | `M1-C` **补丁片** | ⏳ 待派（修法已裁＝丙′） |
| **`U-2`** | **箱 → 仓搬运链缺失 ＋ 拾取丢弃返回值 ⇒ 箱内容物必丢**（含退还物）；`09` §16.1-4「工人搬回」契约未实现 | **另立独立片**（⛔ 不并 `M1-C`） | ⏳ **待派·阻塞级**（`M1-C` 件 3 依赖之） |
| **`U-3`** | 修复费**复利**（修复花费被 `AddInvested` 累加 ⇒ `C→1.5C→2.25C…`） | 随 `U-1` 丙′ 同方法内一次改到位 | ⏳ 待派（与 `U-1` 同批） |
| **`DZ-3`** | （`D794` 已立）`U-1` 超发 · 归 `M1-C` 补丁片 | `M1-C` | ⏳ 待派（与 `U-1` 同一事，合并处置） |

⚠️ **`DZ-3` 与 `U-1` 是同一缺陷** ⇒ 派工时**合并为一笔**（`DZ-3` 状态改"已并入 `U-1` 处置"）。

### 6.2 教训

- ⭐ **`L-46`（本端自犯 · 自纠入库）**：**「按倍数反推常数」会造出内部不自洽的读数** —— 本端 `D794` 由总量 60 反推 `House` 满级 Wood 54（`54+22=76≠60` 当场矛盾却未被自检捕获）。⇒ 通则：**凡"应为/基准"类读数，必须直读定义源逐项加总，并做「Σ 自洽性」校验**（各项之和须等于总量）。
- ⭐ **`L-47`（新维度 · 从 `U-2` 沉淀）**：**「有返回值」≠「有消费方」** ⇒ 判"功能是否可用"须**扫返回值的全库调用面**；本例 `Pickup` 注释明写"供调用方入背包"，实际**全库零消费方** ⇒ 注释描述的是**意图**而非**实现**。⚠️ 通则：**引注释作证据时，必须同时验"注释描述的行为是否在场"**。
- ⭐ **`L-45`（`D794` 已立）实例入库**：`09#54`（退役 `hp/maxHp` 折扣）× `09#55`（退还改掉箱）**各暴露一个下游** ⇒ `U-1`（金额）＋ `U-2`（去向） ＝ **"退役一个机制，回扫其掩盖的下游"必须做到"金额 ＋ 去向"两层**。

---

## 七 · 落账清单

| 件 | 文件 | 动作 |
|---|---|---|
| ① | `多Agent交接/策划端/HH.315_M1-C-U1评估与U2新立_裁决书.md` | **新建**（本文 · 八节） |
| ② | `河谷防线开发计划书具体内容/测试基线台账.md` | 追加 **§九十九**（99.1 取证／99.2 自纠／99.3 成立项／99.4 勘正项／99.5 新发现／99.6 裁决／99.7 挂账／99.8 落账／99.9 状态） |
| ③ | `多Agent交接/_编号登记.md` | `HH.315` 行追加「`U-1` 评估已裁决（`D795`）」 |
| ④ | `多Agent交接/_任务队列.md` | `M1-C` 行更新（**件 3 未闭合** ＋ 待修 2 笔）；⭐ 新插 **`U-2`**／**`U-3`** 两挂账行；`DZ-3` 行标注"并入 `U-1`" |
| ⑤ | `多Agent交接/_当前快照.md` | 补 **`D795`** 段 |
| ⑥ | `多Agent交接/策划端/_策划教训库.md` | 详述区末尾新立 **`L-46`**／**`L-47`** ＋ `L-45` 实例 |
| ⑦ | `最高优先级文档/09_资源与仓库.md` | §16.1-4「要工人搬回」**标注"当前未实现（`U-2`）"**；§16.2 补「退还量 ＝ **逐阶段造价累加**（⛔ 非"按占比摊"）」；§十二 `#48`/`#54` 加「⚠️ `U-1` 修复中（丙′）」 |
| ⑧ | `多Agent交接/策划端/HH.315_M1-C建造升级修复拆除全走仓_交付验收裁决.md` | **回写自纠**（§二 `House` 满级 54 ⇒ 38） |
| ⑨ | 执行端报告 `HH.315_M1-C-U1评估_交付报告.md` | ⚠️ **⛔ 本端不代写** ⇒ 勘正 1／勘正 2 由执行端回写 |

---

## 八 · 状态

- ⛔ **本端本轮零代码零资产**（仅裁决 ＋ 账本文档）。
- ⛔ 未碰 `WarehousePanel`／`TreasureVault`／美术／`pixel-forge`／`GameScene`／`Packages`／`3.6`·`3.8` doc。
- **`M1-C` 状态**：**⚠️ 有条件通过 · ⛔ 不销号**（待修 **2 笔**：`U-1` 丙′ ＋ 件 3 依赖 `U-2`）。
- **▶️ 下一步（两笔可并行）**：
  1. **`U-1` 补丁片**（`Building.cs` 2 方法 ＋ 注释勘正）：按 **丙′**（丙 ＋ 投料中态分流）＋ 顺带 `U-3`（修复费不复利）＋ `C-5` 6 处陈注释；
  2. **`U-2` 独立片**（箱 → 仓搬运链 ＋ 拾取入包）：新 `ITaskSource` ＋ `KingdomTaskType` 扩展 ＋ `Interact` 消费返回值。
- **优先级**：⭐ **`U-2` 优先于 `M1-D`~`M1-G`**（退还功能整体失效 ⇒ 影响所有含金/材料建筑）。
