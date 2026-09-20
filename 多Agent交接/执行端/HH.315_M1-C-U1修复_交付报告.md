# HH.315 · M1-C · U-1 修复（丙′）—— 交付报告

- 任务号：HH.315 · U-1 修复（承 `d4c5fd7e` 只读评估 ⇒ `D795` 裁决「准丙′」）
- 改动面：**4 文件**（`Building.cs` ／ `BuildingSaveData.cs` ／ `RulerController.cs` ／ `KingdomState.cs`）· `+136/−50`
- 编译：Unity **0 error**（`refresh_unity` ⇒ `success`；`read_console(error)` ⇒ 0 条；`Assembly-CSharp.dll` 时间戳已刷新，新方法反射可见）
- 日期：2026-09-20
- 判据：**1~7 全绿**（读数见 §三；原始读数由 Unity MCP 内存态执行产出）

---

## 〇 结论速览

| 件 | 落码 | 关键读数 |
|---|---|---|
| **件1** 退还改「逐阶段累加」 | ✅ | House L1 **{Wood:14,Stone:6}**（改前 {Wood:20}）／满级 **{Wood:38,Stone:22}**（改前 {Wood:60}）／Warehouse L1 **{Gold:4,Stone:14,Wood:10}**（改前 {Gold:14,Stone:14}） |
| **件2** 修复费同法 ＋ U-3 | ✅ | 修复费三次 **{Wood:2}→{Wood:2}→{Wood:2}**（改前复刻 **2→3→4** ⇒ 复利消失） |
| **件3** 注释勘正 | ✅ **8 处**（Building.cs 5 ＋ BuildingSaveData 1 ＋ RulerController 1 ＋ KingdomState 1）· 零行为 | ⚠️ 另 1 处（`BuildingFactory.cs:346`）**未获授权 ⇒ 未改**，见 §五 N-2 |
| **件4** `totalInvested` 标注「备而未用」 | ✅ | 生产码**零读点**（全库 grep：仅 写入 3 处 ＋ 入档/日志 ＋ 注释） |
| ⭐ **施工中新发现** | — | **N-1**：`_pendingUpgrade`/`_pendingRepair` **未入档** ⇒ 读档后中途升级**不升级**（既有 M1-C 缺口）＋ 本修复须加「在投配方比对」兜底；**须裁**（补字段＝动存档格式） |

---

## 一 · 件1 落码（`BuildRefundPack` ⇒ 丙′）

### 1.1 新口径

退还量 ＝ **已支付阶段造价（逐类型精确累加）** ＋ **在投阶段已到料**：

| 阶段 | 计入 |
|---|---|
| 建造（`def.cost`） | 已完工 **或** 在投且已付清（`!_awaitingMaterials`） |
| 已完成升级 `Σ_{i<level-1} levels[i].upgradeCost` | 全额 |
| 在投升级**已付清**（料齐但进度未满 ⇒ `level` 未 ++） | 该次全额 |
| 在投阶段**未付清**（`_awaitingMaterials`） | 该阶段「**金**」（下单即扣 · 金-A）＋ **工地仓已到料** `siteContents` |
| 在投修复**已付清** | 修复费 ＝ 已完成阶段造价 × `ratio` |

落点：[Building.cs:878-892](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L878-L892)（`BuildRefundPack`）＋ [`:894-940`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L894-L940)（`PaidStageCost`）＋ [`:942-948`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L942-L948)（`CurrentStageCost`）＋ [`:950-985`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L950-L985)（`InProgressStageIsBuild` ／ `SamePack` ／ `RepairCostRatio`）

⛔ **退役**：`invested × e.amount / baseSum`（累计投入 ÷ 基础造价占比摊）—— **不再读 `totalInvested`**。
⛔ **不改签名**（`private ResourceList BuildRefundPack()`）；⛔ 掉箱 `Faction.None` 不变（[`:870-877`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L870-L877) 未动）。

⭐ **防双重退还**：在投阶段只计「**已到料** `siteContents`」而非需求全额 —— 否则与 `DropSiteStoreToChest()`（[`:988-999`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L988-L999)）掉出的同一批料重复计入。

### 1.2 ⭐ 落码中发现并处理的一处**读档失效**（→ §五 N-1）

`_pendingUpgrade` / `_pendingRepair` **未入档** ⇒ 直接依赖它们会在读档后判错"在投阶段是谁"。故 `InProgressStageIsBuild()` 采用四级判据：

| 级 | 判据 | 说明 |
|---|---|---|
| ① | `_pendingUpgrade \|\| _pendingRepair` ⇒ **非**建造 | 运行期权威 |
| ② | `level > 1` ⇒ **非**建造 | 已有完成升级 ⇒ 建造必已完成 |
| ③ | `SamePack(_siteNeed, SiteNeedOf(def.cost))` ⇒ **是**建造 | 读档兜底：比「在投配方」 |
| ④ | `_siteNeed.IsZero` ⇒ **是**建造 | 无料可搬（纯金造价／AI 台账直扣直建／零造价） |

⚠️ **③④ 的前提已实测坐实**（机械扫描全库真资产）：
- **12 栋含升级资产 · 各级升级去金配方 vs 建造去金配方 碰撞数 ＝ 0**
- **21 级升级 · 去金配方为空者 ＝ 0 处**（⇒ ④ 不会把升级误判为建造）

⇒ 当前资产集下**精确**；注释中已写「新增资产若碰撞则须先补 `_pending*` 入档」。

---

## 二 · 件2 落码（`GetRepairCost` ⇒ 同法 ＋ U-3）

[Building.cs:1136-1150](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1136-L1150)：

```csharp
public ResourceList GetRepairCost()
{
    if (def == null) return ResourceList.Empty;
    var full = PaidStageCost();                 // ＝ 已支付阶段造价（逐类型精确累加）
    if (full.IsZero) return ResourceList.Empty;
    return full * RepairCostRatio();            // 逐类型 Mathf.RoundToInt（ResourceList.operator * 单源）
}
```

- 旧：`invested(=totalInvested) × ratio` 再按 `def.cost` 占比摊 ⇒ **基数含历史修复花费 ⇒ 复利**。
- 新：基数 ＝ `PaidStageCost()`（与修复历史**无关**）⇒ **U-3 复利消失**（读数见 §三 判据 6）。
- `RepairCostRatio()` 单源（`RepairConfig` SO · 缺省 `0.5` 与原实现一致）。

---

## 三 · 判据 1~7 实测读数

> **取证方式**：Unity MCP `mcp_unityMCP.execute_code`（**内存态执行 · ⛔ 不落任何脚本文件 · ⛔ 不动 `Assets/**` 以外任何文件**）
> **被测对象**：**生产码原方法**（`BuildRefundPack` 经反射直调 · `GetRepairCost` ／ `StartConstructing` ／ `TryUpgrade` ／ `SiteStore.Deposit` ／ `OnConstructionComplete` ／ `SaveState` ／ `LoadState` ／ `Demolish` 皆真实调用）
> **改前对照**：探针内「旧口径复刻」（`floor(invested × e.amount / baseSum)` ／ `round(invested×ratio)` 再按 `def.cost` 占比摊）—— ⛔ **非生产码调用**，仅作对照

### 判据 1~5（分资源明细）

| # | 样本（入场态） | **改后（生产码）** | 改前复刻 | 判定 |
|---|---|---|---|---|
| **1** | House L1（`level=2` · `totalInvested=20` · Active） | **`{Wood:14, Stone:6}`** | `{Wood:20}` | ✅ 命中 |
| **2** | House 满级（`level=3` · `totalInvested=60`） | **`{Wood:38, Stone:22}`** | `{Wood:60}` | ✅ 命中 |
| **3** | Warehouse L1（`cost={Gold:4,Stone:4}` · 升级 `{Stone:10,Wood:10}` · `totalInvested=28`） | **`{Gold:4, Stone:14, Wood:10}`** | `{Gold:14, Stone:14}` | ✅ 命中（改前 Wood 全丢／Gold 超发 10） |
| **4** | House 未升级（空对照 · `totalInvested=4`） | `{Wood:4}` | `{Wood:4}` | ⚠️ **与改前同值 ⇒ 本组无鉴别力**（显式标注；其作用＝**空/零对照**，证明修复未破坏正常路径） |
| **5** | House 升级投料中（只投 `Stone 3/16` · `awaiting=True` · 工地仓 `{Stone:3}`） | **`{Wood:4, Stone:3}`** | `{Wood:7}` | ✅ 命中（**⛔ 非全额 ⇒ 无双重退还**） |

**追加 5 组（覆盖新分支 ＋ 无鉴别力组显式标注）**：

| # | 样本 | 改后 | 改前复刻 | 说明 |
|---|---|---|---|---|
| 6 | Warehouse 未升级（多类型基础） | `{Gold:4, Stone:4}` | `{Gold:4, Stone:4}` | ⚠️ 同值 · 无鉴别力（参考组） |
| 7 | House **首次建造投料中**（投 `2/4` · `awaiting=True`） | `{Wood:2}` | `{Wood:2}` | ⚠️ 同值 · **无鉴别力**；价值＝**新分支负向验证**：⛔ 未读成 `{Wood:6}`（base 4 ＋ 已到料 2 双计） |
| 8 | House **升级料齐未完工**（`awaiting=False` · `state=Constructing`） | `{Wood:14, Stone:6}` | `{Wood:20}` | ✅ 新分支（在投升级已付清 ⇒ 计全额） |
| 9 | `farm` 纯金建造中（`cost={Gold:50}` · 无料可搬） | `{Gold:50}` | `{Gold:50}` | ✅ 纯金路径（裁决 2 金-A） |
| 10 | AI 式直建（`StartConstructing(Empty)`） | `{Wood:4}` | `{Wood:4}` | ✅ AI-A 路径 |

### 判据 6（修复费不再递增）

| 轮次 | 入场 `totalInvested` | **改后** | 改前复刻 |
|---|---|---|---|
| 第 1 次 | 4 | **`{Wood:2}`** | `{Wood:2}` |
| 第 2 次 | 6 | **`{Wood:2}`** | `{Wood:3}` |
| 第 3 次 | 8 | **`{Wood:2}`** | `{Wood:4}` |

⇒ ⭐ **改后恒定 `{Wood:2}`**；改前复刻 `2 → 3 → 4` **递增** ⇒ **U-3（复利）消失**。
（`ratio=0.50` · 实读 `RepairConfig.asset`；链：`Ruined → GetRepairCost → StartRebuildFromRuins → 投料 → OnConstructionComplete` 全真实调用）

### 判据 7（存档往返逐值一致）

| 组 | 存档前 | 读档后 | 逐值一致 |
|---|---|---|---|
| 7a 升级**投料中** | `state=Constructing awaiting=True site={Stone:1} siteNeed={Stone:6,Wood:10} 退={Wood:4,Stone:1}` | 同左 | ✅ **True** |
| 7b 升级**料齐未完工**（关键组 · 走 ③ 兜底） | `state=Constructing awaiting=False totalInvested=20 退={Wood:14,Stone:6}` | 同左 | ✅ **True** |
| 7c **首次建造投料中** | `state=Constructing site={Wood:3} 退={Wood:3}` | 同左 | ✅ **True** |
| 7d 拆除中态 | `拆除中=True 进度=0.0000 退={Wood:4}` | 同左 | ✅ **True** |

⚠️ **探针取证值声明（生产值 vs 取证值）**：
1. **⛔ 生产值变更 ＝ 0**（未改任何 SO／资产／探针文件；无临时开关；无 `taskTimeout` 类放宽）。
2. 探针为**内存态**：`new GameObject` ＋ `AddComponent<Building>()`，逐组 `DestroyImmediate`；**收尾三态**：残留 `U1*` ＝ **0** ／ `GameScene.isDirty` ＝ **False** ／ 根对象 ＝ **53**（＝基线）。
3. ⚠️ `OnConstructionComplete` 在 **Edit 模式**下走到视觉路径（`KingdomRace` → `KingdomRegistry` 单例）会抛「`DontDestroyOnLoad` 仅限 Play 模式」——该异常发生在 **`level++` ／ `state=Active` ／ `_pending*` 清零之后** ⇒ **不影响本判据输入**；脚本内已 try/catch 并逐组标注。
4. ⚠️ `Building.LoadState` **不恢复 `state`**（真实路径由 `BuildingFactory.SpawnFromSave:292` 取 `data.state` → `CreateBuildingInstance(initialState:)` → `:155 b.state = initialState` 恢复）⇒ 探针在读档前**显式 `s2.state = s1.state`** 以复刻真实路径（已声明）。
5. 判据 1~6 的「改前复刻」列 ＝ 探针内旧口径复刻函数（⛔ 非生产码）。

---

## 四 · 件3 注释勘正（8 处 · 零行为）

| # | 位置 | 勘正内容 |
|---|---|---|
| 1 | [Building.cs:140-149](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L140-L149)（字段 ＋ Tooltip） | 根因留档：自 `D162` 起为「单一近似总量而非分资源账」⇒ 旧口径拿它当分子 ÷ `def.cost` 当分母**必致张冠李戴**；标注**备而未用** ＋ ⛔ 勿再作退还/修复基数 |
| 2 | [Building.cs:436](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L436) | 「累计投入（修复成本基数 / 拆除返还基数）」⇒ 改为「累计投入件数（U-1 后备而未用）」 |
| 3 | [Building.cs:862-864](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L862-L864) | `FinishDemolish`：「按 `def.cost` 全部资源**摊**」⇒ 「**逐阶段造价逐类型累加**」＋ 注明旧口径即 U-1 |
| 4 | [Building.cs:878-892](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L878-L892) | `BuildRefundPack` 文档：旧「占比摊」口径的缺陷与退役原因（含实测对照） |
| 5 | [Building.cs:977-982](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L977-L982)（`SumCostOf` 已删 段） | 补注：U-1 后**不再有 `costSum` 分母**（旧口径整体退役） |
| 6 | [Building.cs:1093-1094](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1093-L1094)（`LoadState`） | 「与 `BuildRefundPack`/`GetRepairCost` 的 `costSum` 同源」⇒ 改为「备而未用 ⇒ 仅存档往返保真，⛔ 不入算式」 |
| 7 | [BuildingSaveData.cs:44-46](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingSaveData.cs#L44-L46) | 同上（档字段注释） |
| 8 | [RulerController.cs:293-297](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Ruler/RulerController.cs#L293-L297) ＋ [KingdomState.cs:167-172](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/KingdomState.cs#L167-L172) | 陈注释「拆除退款 ratio=0.5（等）」⇒ 勘正：拆除退款**已不由此路径**（改随本体掉箱）⇒ `ratio` 形参生产码只剩默认值 `1.0` 调用；形参去留归后续片（⛔ 本片不改签名） |

**行尾纪律**：4 文件改前改后**均纯 LF**（实测 `Building.cs CRLF=0 bareLF=1436`／`RulerController 0/427`／`KingdomState 0/223`／`BuildingSaveData 0/67` · NUL 全 0）⇒ 走 Edit 直改，⛔ 未用 python 二进制。

---

## 五 · 施工中新发现（如实列报）

### N-1 ⭐ `_pendingUpgrade` / `_pendingRepair` **未入档**（**须裁**）

| 项 | 内容 |
|---|---|
| 事实 | `BuildingSaveData` **无** `pendingUpgrade` / `pendingRepair` 字段；`Building.LoadState` 亦不置位 |
| 后果 ①（**既有 M1-C 缺口**） | [Building.cs:691](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L691) `OnConstructionComplete` 靠 `_pendingUpgrade` 决定 `level++` ⇒ **读档后中途升级完成 ⇒ 不升级**（材料已付、等级不涨）；中途修复同理不置满血 |
| 后果 ②（本片） | U-1 修复需判「在投阶段是谁」⇒ 已用 §1.2 的四级判据兜底（③④），当前资产集**精确** |
| 本片处置 | ⛔ **未补字段**（补＝动 `BuildingSaveData` 格式 ⇒ 本片红线「⛔ 不动格式」）⇒ **报裁**：是否尾插 `pendingUpgrade` / `pendingRepair` 两字段（尾插零 bump，与 M1-C 自身先例一致）以**治本**（同时修掉后果 ①） |
| ⚠️ 风险窗口 | 仅「读档时正在投料/施工的**升级/修复**」⇒ ③④ 兜底已覆盖退款算式；后果 ①（不升级）**仍未修** |

### N-2 `BuildingFactory.cs:346` 陈注释（**未获授权 ⇒ 未改**）

[BuildingFactory.cs:346](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs#L346) 仍写「（D155 修复成本基数 / D162 拆除返还基数）」—— 与本片同款陈注释。⚠️ 任务红线「**只改 `Building.cs` ＋ 注释所列文件**」⇒ **未列入 ⇒ 本端未改**，请裁是否补一行勘正（零行为）。

### N-3 `Building.LoadState` 不恢复 `state`（**未改 · 潜在坑**）

[Building.cs:1032-1043](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1032-L1043) 的 `LoadState` 不置 `state`，依赖真实路径 `BuildingFactory.SpawnFromSave:292/155` 在**创建时**置 `initialState`。⇒ 若将来出现「直接对已存在实例调 `LoadState`」的调用方，`state` 会静默退化为 `Active`（本片**未改**，仅列报）。

---

## 六 · 未动面声明

**改动文件（4）**：

```
Valley Rampart/Assets/_Game/Systems/Building/Building.cs        | 167 ++++++------
Valley Rampart/Assets/_Game/Systems/Building/BuildingSaveData.cs|   5 +-
Valley Rampart/Assets/_Game/Systems/Kingdom/KingdomState.cs     |   7 +-
Valley Rampart/Assets/_Game/Systems/Ruler/RulerController.cs    |   7 +-
4 files changed, 136 insertions(+), 50 deletions(-)
```

**⛔ 一行未动**：`WarehousePanel` ／ `TreasureVault` ／ 四档账本 ／ 美术（`_Game/Art/**`）／ pixel-forge ／ `GameScene.unity` ／ `Packages` ／ `3.6`·`3.8` doc ／ `BuildingFactory.cs`（见 N-2）／ `BuildingPanel.cs` ／ `ConstructionSiteStore.cs` ／ `BuildController.cs` ／ `TaskScheduler.cs`。
**⛔ 未 push**；**⛔ 未代提交策划端账本文档**。

---

## 七 · 请裁汇总

| # | 争点 | 本端倾向 |
|---|---|---|
| **C-1** | N-1：是否尾插 `pendingUpgrade` / `pendingRepair` 入档（**治本** · 兼修「读档后中途升级不升级」） | ✅ 建议**另立小片**尾插（尾插零 bump）；本片 ⛔ 未动格式 |
| **C-2** | N-2：`BuildingFactory.cs:346` 陈注释补勘正 | ✅ 建议补（1 行 · 零行为） |
| **C-3** | N-3：`LoadState` 不恢复 `state` | ⚠️ 仅列报；是否补 `state` 恢复由后续片定 |
| **C-4** | `RulerController.Refund` / `KingdomState.Refund` 的 `ratio` 形参（生产码只剩默认值调用） | ⚠️ 建议随后续片一并裁（去留 · ⛔ 本片未改签名） |
| **C-5** | 判据 4／6／7（探针组）**无鉴别力**（与改前同值） | ✅ 已显式标注；其作用为**空对照 ＋ 新分支负向验证**（⛔ 未读成双计值） |
