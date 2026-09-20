# HH.315 · M1-C（建造／升级／修复／拆除 全走仓）交付报告

- 任务号：HH.315（承 HH.314 · M1-B 已验收销号 · D791；裁决 D793「全按执行端倾向」）
- 归属模块：中层 `09_资源与仓库.md` · 施工片 `M1-C`（差异 `09#48`／`#53`／`#54`／`#55`／`#56`／`#64`）
- 状态：**四件全部落码** · 判据 1~7 已实测取证 · 无待裁决项
- 编译：Unity 0 error（`refresh_unity` ⇒ `success:true`；`read_console(error)` ⇒ 0 条）
- 冒烟：同局冒烟走 HH.92 正门 `TestHarnessApi.EnterTestRun`（seed=31518 · 槽 `hh315_m1c` · Small/difficulty=2 · 15x）
- 交付 commit：`7c8a7f1f`（13 文件 · `+1474/−84`）
- 证据载体：`Valley Rampart/Logs/hh315_m1c/hh315_m1c_smoke.txt`（2026-09-20 15:22:29）
- 日期：2026-09-20

---

## 〇 结论速览（四件）

| 件 | 任务书要求 | 落码 | 关键落点 |
|---|---|---|---|
| 件1 | 四动作改「投料 ⇒ 等时间」 | ✅ | 新建 `ConstructionSiteStore.cs`（224 行）＋ `Building.BeginMaterialPhase` ＋ `TaskScheduler` 两段搬料 ＋ `BuildController.PayOrderCost` |
| 件2 | 拆除退还改「全退」 | ✅ | `Building.BuildRefundPack`（退役 `hp/maxHp` 比例 ＋ 四资源口径）＋ `GetRepairCost` 同源化 ＋ `SumCostOf`／`IsRefundResource` 零调用删除 |
| 件3 | 退还改「随建筑本体掉箱」 | ✅ | `Building.FinishDemolish` ⇒ `ChestManager.SpawnChest(coord, refundPack, Faction.None)`（退役 `RulerController.Refund` 直入国库） |
| 件4 | 拆除加耗时与工人 | ✅ | 新增 SO 字段 `BuildConfig.demolishBaseSeconds`（=6）＋ `Building.Demolish` 只进拆除态 ＋ `Update` 内「工人到场」门控推进 |

**改动面**：10 个生产码／资产文件 ＋ 1 个 Editor 冒烟容器（＋2 `.meta`）＋ 本报告。`WarehousePanel.cs`／`TreasureVault.cs`／账本／美术／pixel-forge／`GameScene.unity`／`Packages`／`3.6`·`3.8` doc **一行未动**。

---

## 一 件1 · 四动作改「投料 ⇒ 等时间」（`09#53`／`#64`）

### 1(a) 新建工地仓：`ConstructionSiteStore`（裁决 1 案 A）

[ConstructionSiteStore.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/ConstructionSiteStore.cs)（新建 · 224 行）—— 独立子物体容器，⛔ **不复用** `StorageComponent`、⛔ 不入 `WarehouseRegistry`、⛔ 无仓库标签；容量 ＝ 配方量本身（阈值拦截）。

| 成员 | 现行行 | 语义 |
|---|---|---|
| `IsValid` | [:163](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/ConstructionSiteStore.cs#L163) | `_building != null && _building.IsSiteAwaitingMaterials && !IsSatisfied` |
| `SourcePos` | [:166](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/ConstructionSiteStore.cs#L166) | ＝ `_pickupPos`（**取料仓位置** ⇒ 第一段位移） |
| `TryAdvertiseTask` | [:168](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/ConstructionSiteStore.cs#L168) | 内部 `FindPickup` 走 `WarehouseRegistry.GatherActive(kingdomId)` 取**最近收该资源且有货**之仓 |
| `Deposit` | [:112](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/ConstructionSiteStore.cs#L112) | 阈值拦截 ＋ 工地已关守卫 ＋ `AddInvested` 逐笔记账 |

**裁决 1 加严项**「容器须自 `Constructing` 态起可写」：`Building.IsValid` 放行 `_awaitingMaterials`（[Building.cs:1257-1258](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1257-L1258)），`EnsureSiteStore` 在 `StartConstructing` 内即时创建（[Building.cs:534-538](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L534-L538)）⇒ 下单后**同帧**即注册为任务源。

### 1(b) 投料态与进度门控（`Building`）

| 落点 | 现行行 | 说明 |
|---|---|---|
| `_awaitingMaterials` / `_siteNeed` / `_siteStore` | [:211-217](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L211-L217) | 投料态三字段 |
| `SiteNeedOf`（去金）／`GoldOnlyOf`（取金） | [:252](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L252)／[:267](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L267) | 单源互补，⛔ 不各写一遍 |
| `StartConstructing(need)` | [:496](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L496) | 进 `Constructing` ＋ `BeginMaterialPhase(need)` |
| `BeginMaterialPhase` | [:524](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L524) | 去金后为空 ⇒ 即时开工；否则建/复用工地仓 ＋ 注册任务源 |
| `OnSiteMaterialsReady` | [:570](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L570) | 注销任务源 ＋ `_siteStore.Clear()`（材料**转建筑本体**·⛔ 不占产出容量） |
| `Update` 进度门控 | [:603-616](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L603-L616) | `if (!_awaitingMaterials)` 才推进 `constructProgress` |
| 升级／修复同构 | [:510](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L510)（`StartRebuildFromRuins(need)`） | `09` §16.3-2「修复与建造同构」 |

### 1(c) 搬料任务：语义可区分（裁决收口 1 · ⛔ 不新增枚举）

[KingdomTask.cs:71-97](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/Tasks/KingdomTask.cs#L71-L97) 新增 `HaulToSiteArgs`（搬料）／`DemolishTaskArgs`（拆除）；**任务类型沿用 `KingdomTaskType.Build`**（原死值 · 生产码零调用方），**靠 args 类型区分语义**。

[TaskScheduler.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs)：

| 落点 | 现行行 | 说明 |
|---|---|---|
| `MovingToDest` 装载段 | [:469-473](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L469-L473) | `type == Build && args is HaulToSiteArgs` ⇒ `LoadSiteMaterials` |
| `Working` 卸料段 | [:501-502](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L501-L502) | ⇒ `DepositToSite`（阈值拦截在此生效） |
| `LoadSiteMaterials` | [:779](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L779) | 装载（含背包混装防护 · 见 §七） |
| `DepositToSite` | [:810](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L810) | 卸料 ＋ 超量回流 `ReturnOverflow` |

### 1(d) 下单时点扣费：金-A ＋ AI-A

[BuildController.cs:341-353](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildController.cs#L341-L353) 新增 `PayOrderCost`：

```csharp
private static bool PayOrderCost(int kingdomId, ResourceList cost)
{
    if (kingdomId <= 0) return WarehouseHelper.TrySettle(Building.GoldOnlyOf(cost));   // 玩家：只扣金
    if (!CanPayBuild(kingdomId, cost)) return false;
    return PayBuild(kingdomId, cost);                                                  // AI：台账全额直扣
}
```

[BuildController.cs:288-295](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildController.cs#L288-L295)：

```csharp
b.StartConstructing(kingdomId <= 0 ? def.cost : ResourceList.Empty);   // 裁决 3 AI-A：AI 立即开工
b.AddInvested(kingdomId <= 0 ? goldOnly.TotalCount : def.cost.TotalCount);
```

⭐ **裁决 3 要求「须留含 `09#52` 编号注释标注『已知第二账』」已落**（`BuildController.cs:290-294` 与 `:347-349` 两处）。

---

## 二 件2 · 拆除退还改「全退」（`09#48`／`#54`）

| 落点 | 现行行 | 改前 | 改后 |
|---|---|---|---|
| 退还量算式 | [Building.cs:877-891](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L877-L891) | `invested × (hp/maxHp) × e.amount / baseSum` | `invested × e.amount / baseSum`（⛔ **无 `hp/maxHp` 折扣**） |
| 资源摊口径 | 同上 | `IsRefundResource` 四资源（金/石/木/粮） | **`def.cost` 全部资源** |
| `costSum` 基数 | [Building.cs:880](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L880) | `SumCostOf(cost, includeMetal:false)` | `def.cost.TotalCount` |
| `:827` 同族第三落点（修复费） | [Building.cs:1055-1075](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1055-L1075) | 五资源（含 Metal · ⛔ 不含 Ore） | **`def.cost` 全部资源**（与 `BuildRefundPack` **同源化** · 裁决 4-a） |

### 2(a) 裁决收口 2「`SumCostOf` 6 处调用点须同步核」—— 逐处核完

`git grep SumCostOf HEAD`（改前）＝ **6 处调用点**（＋1 处定义 `:683`）：

| # | 改前落点 | 改后处理 | 改后现行行 |
|---|---|---|---|
| 1 | `Building.cs:362` | 兜底改 `def.cost.TotalCount` | [:1004](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1004)（`LoadState` 兜底） |
| 2 | `Building.cs:636` | 拆除入口改逐笔记账 `AddInvested` | [:244-247](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L244-L247) |
| 3 | `Building.cs:658` | 退还基数改 `def.cost.TotalCount` | [:880](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L880) |
| 4 | `Building.cs:775` | 兜底改 `def.cost.TotalCount` | [:1004](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1004) |
| 5 | `Building.cs:812` | 修复费基数改 `def.cost.TotalCount` | [:1058](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1058) |
| 6 | `BuildingFactory.cs:349` | 兜底改 `def.cost.TotalCount` | [:349](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs#L349) |

**核验**：改后全库 `SumCostOf` 生产码**零命中**（`git grep` 仅剩 3 处注释 + 探针内 `LegacySumCostOf`）。

### 2(b) 裁决 4「若 `IsRefundResource` 因此零调用 ⇒ 一并删除」—— 已删

改前 `Building.cs` 有 `static bool IsRefundResource(ResourceType)` 定义 ＋ 2 处调用（`:667` 退还摊、`:827` 修复费摊）。改后全库 `IsRefundResource` 生产码**零命中**（仅 [Building.cs:1052](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1052) 一行退役说明注释 ＋ 探针内 `LegacyIsRefundResource`）⇒ **已随 `SumCostOf` 一并删除**。

---

## 三 件3 · 退还改「随建筑本体掉箱」（`09#55`）

[Building.cs:861-873](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L861-L873) `FinishDemolish`：

```csharp
var refundPack = BuildRefundPack();
if (!refundPack.IsZero && ChestManager.HasInstance)
    ChestManager.Instance.SpawnChest(coord, refundPack, Faction.None);   // ⛔ 国库不即时增加
DropSiteStoreToChest();   // 工地仓内容物掉箱（09 §16.3-4）
Die(DeathCause.Demolished);
```

- 退役 `RulerController.Refund` 直入国库路径（生产码 `Refund` 本片零新增调用）。
- `Faction.None` ＝ 无主箱（`09` §9.8；先例 `DamageSystem.cs:633`）；`09#57` 参数去留归 `M1-D` ⇒ **⛔ 本片不改签名**。
- 口径裁 #4「工地被打毁 ⇒ 仓里材料掉箱」：[Building.cs:1163-1167](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1163-L1167)（`Die` 内幂等收口）。

---

## 四 件4 · 拆除加耗时与工人（`09#56`／`#64`）

| 落点 | 现行行 | 说明 |
|---|---|---|
| SO 字段 `demolishBaseSeconds` | [BuildConfig.cs:26-30](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/BuildConfig.cs#L26-L30) | `so-data-driven`：⛔ 不硬编码；落值 6（`BuildConfig.asset`） |
| `DemolishDuration()` | [Building.cs:628-637](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L628-L637) | 与建造**同用**协作系数 k：`base / (1 + (n−1)×k)` |
| `Demolish()` 只进拆除态 | [Building.cs:843-852](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L843-L852) | ⛔ 退役「瞬时 `Die`」；幂等 ＋ `Dead/Placing` 守卫 |
| `Update` 拆除推进（工人门控） | [Building.cs:589-601](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L589-L601) | `HasAssignedWorker()` 为真才推进；拆除分支**先于**建造分支 |
| `HasAssignedWorker` | [Building.cs:621-622](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L621-L622) | `CountAssignedWorkers(this) > 0` |
| 拆除任务广告 | [Building.cs:1275-1281](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1275-L1281) | 拆除中**只**广告 `DemolishTaskArgs`（⛔ 不再广告 Production/Transport/WaterHaul） |
| `IsValid` 放行拆除态 | [Building.cs:1257-1258](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1257-L1258) | `_demolishing` 也是合法任务源 |
| 进度条显示拆除进度 | [Building.cs:648-650](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L648-L650) | `_demolishing` 走 `_demolishProgress` |
| `BuildingPanel` 调用点 | [BuildingPanel.cs:395](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingPanel.cs#L395) | 注释同步（只进拆除态） |

---

## 五 判据 1~7 改前／改后对照读数

> 改后 ＝ **生产码实测**；改前 ＝ 探针内「旧口径复刻」（公式源自任务书 §二 现状实读 `:656`／`:667`／`:668`／`:672`／`:827`）。
> 原始读数载体：`Logs/hh315_m1c/hh315_m1c_smoke.txt`。

### 判据1「建造走仓」

样本 `House`（`cost={Wood:4}` · 非纯金 ⇒ 符合裁决 2 禁纯金样本要求）；工作中心格 87,43（＝取料仓所在格），工地 (30.1,41.9) ← 取料仓 (28.2,41.6) ＝ 直线 **1.9** 世界单位。

| 项 | 改后（生产码） | 改前（旧口径复刻） |
|---|---|---|
| `TryBuild` | `True`（origin=88,42） | — |
| 国库金 | 100 → **100**（House 无金） | 100 → 100 |
| 仓木 | 100 → **100**（非金 ⛔ 不下单即扣） | 100 → 96（下单即扣） |
| 下单后第 1 帧 `SiteStore` | **非空**（`SiteStore=True`） | `SiteStore=False` |
| `IsSiteAwaitingMaterials` | **True** | False |
| `SiteNeed` | `{Wood:4}` 缺口 **4** | — |
| `totalInvested` / `constructProgress` | **0** / **0.0000** | — / **0.2914**（进度立即推进） |
| **裁决 1 加严项**：第 1 笔搬运命中工地仓 | **帧=142**（下单后第 142 帧）· 该笔后 `totalInvested=4` · 料齐 `=True` | ⛔ 无工地仓（恒不可命中） |
| 料齐后 | `state=Active constructProgress=1.0000` | — |

### 判据2「阈值拦截（够阈值即停 · ⛔ 不多搬）」

| 步 | 期望 | 实测 |
|---|---|---|
| `need(Wood)=4` · `Deposit(3)` | 3 | **3** ⇒ 量=3 |
| `Deposit(999)` | 1（只收缺口） | **1** ⇒ 料齐清空后量=0 |
| 再 `Deposit(999)` | 0（工地已关拒收） | **0** |
| **是否超配方** | False | **False** |
| 多资源样本 `Warehouse`（金4/石4）· `SiteNeed={Stone:4}` · `Deposit(Stone,999)` | 4 | **4** |

### 判据3「拆除全退」

样本 `House @(85,41)` · `hp=50/100` · `totalInvested=0` ⇒ 兜底 `invested = def.cost.TotalCount = 4`。

| 项 | 改后（生产码） | 改前（旧口径复刻） |
|---|---|---|
| 掉落退还 | **`{Wood:4}`**（全退） | `{Wood:2}`（2 件 · `hp/maxHp` 折扣） |

### 判据4「全资源摊（含 Metal/Ore）」

⚠️ 现有 40 栋 `cost` 全无 `Metal` ⇒ 判据 4 零鉴别力 ⇒ 探针**临时克隆含 `Metal`/`Ore` 的 `BuildingDef`**（`def.cost={Wood:2,Metal:2,Ore:2}`）。

| 落点 | 改后 | 改前复刻 | Ore 出现 |
|---|---|---|---|
| 修复费（`:827` 同族第三落点） | **`{Wood:1,Metal:1,Ore:1}`** | `{Wood:2,Metal:2}` | 改后 **True** / 改前 **False** |
| 退还（`:667` 第二落点） | **`{Wood:2,Metal:2,Ore:2}`**（原地箱数 0→1） | `{Wood:6}` | Metal 改前 False / Ore 改前 False |

### 判据5「退还掉箱」

| 项 | 期望 | 实测 |
|---|---|---|
| 国库**非金** | **不变** | `{Wood:96}` → **`{Wood:96}`** ✅ |
| 原地箱子数 | ≥1 | **0 → 1** |
| 箱内容 | 全退量 | **`{Wood:4}`** |
| 国库金 | ⛔ 不即时增加 | 100 → **106**（⚠️ 见 §九 caveat-1） |

### 判据6「拆除耗时与工人」

| 项 | 改后（生产码） | 改前（旧口径 · 已退役） |
|---|---|---|
| `Demolish()` → 真拆 | 耗 **5 帧** / **8.46 游戏秒** / **0.53 真实秒** | **同帧完成（0 帧）** |
| 配置 | `DemolishDuration()=6.00s` @ `BuildConfig.demolishBaseSeconds`（测试段 deltaTime=1.55 s/帧） | ⛔ 无耗时字段 |
| 入口同帧态 | `IsDemolishing=True` · `DemolishProgress=0.0000` · `state=Active` · 在册工人=0 | — |
| **门控**（`kingdomId=99` 无工人） | 60 帧内进度 **0.0000 → 0.0000** · 在册工人 **0** ⇒ 「需工人到场」成立 | — |

### 判据7「存档：新档能存能读」

| 态 | 存档前 | 读档后 | 一致 |
|---|---|---|---|
| 投料态 | `awaiting=True` `siteNeed={Wood:4}` `siteContents={Wood:1}` `totalInvested=1` | 逐值相同 | **True** |
| 拆除态 | `IsDemolishing=True` `DemolishProgress=0.9130` | `0.9130`（精确一致） | **True** |

`Save=True` / `Load=True`。字段尾插（`BuildingSaveData` `+13`），⛔ **未 bump 版本、⛔ 未写迁移脚本**（口径裁 #2：判据只要求「新档能存能读」）。

---

## 六 裁决 5 条 ＋ 2 收口 ＋ 1 二次勘正 —— 落实对照

| # | 裁决 | 落实 | 证据 |
|---|---|---|---|
| 1 | 工地仓 ✅ A · 独立 `ConstructionSiteStore`（⛔ 不复用 `StorageComponent`）＋ 加严「下单后第 1 笔搬运命中工地仓」 | ✅ | §一(a)；§五判据1 加严行 **帧=142** |
| 2 | 金投料 ✅ 金-A（金仍下单即扣 ＋ 非金投料）；⚠️ 纯金 3 栋不作判据 1 样本 | ✅ | §一(d)；判据 1 样本改用 `House`（木4）／判据 2 多资源样本用 `Warehouse`（金4石4） |
| 3 | AI 路径 ✅ AI-A（台账直扣不动）＋ 须留含 `09#52` 编号注释标注「已知第二账」 | ✅ | §一(d)；`BuildController.cs:290-294` / `:347-349` |
| 4 | `:827` ✅ (a) 一并改（与 `:667` 同源化）；若 `IsRefundResource` 零调用 ⇒ 一并删除 | ✅ | §二(a)(b) |
| 5 | 主城升级 ✅ (ii) 不纳入，另立差异条目挂后续片 | ✅ | 本片 ⛔ 未动 `KingdomManager.TryUpgradeCastle` |
| 收口 1 | 搬料任务须可区分语义 · ⛔ 不新增枚举 | ✅ | §一(c)：`HaulToSiteArgs`／`DemolishTaskArgs` ＋ 复用 `KingdomTaskType.Build` |
| 收口 2 | `SumCostOf` 6 处调用点须同步核 | ✅ | §二(a) 6/6 逐处表 |
| 二次勘正 | `upgradeCost` 含 Metal 实为 **3 栋**（`arrow_tower`/`gate`/`wall`）；`Blacksmith.asset` 无 Metal | ✅ 认可（本端前报 4 栋有误）；本片未依赖该读数 —— 判据 4 走探针克隆 def | 本报告 §五判据4 |

---

## 七 施工中暴露并修复的 2 处生产码 bug ＋ 1 处乱码

1. **`ConstructionSiteStore.Deposit` 缺「工地已关」守卫 ⇒ 重复接受 ＋ 重复累加 `totalInvested`**
   冒烟 §C 实测暴露：料齐 ⇒ `OnSiteMaterialsReady` 调 `Clear()` ⇒ `Remaining` 复归配方量 ⇒ 在途工人再卸一笔被静默吞掉并**重复记账**（拆除全退会多退）。
   修复：`if (_building != null && !_building.IsSiteAwaitingMaterials) return 0;`（[ConstructionSiteStore.cs:112](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/ConstructionSiteStore.cs#L112) 起 `Deposit` 内）。
   复测：判据 2「再 `Deposit(999)` ⇒ **0**」＋「是否超配方=**False**」。

2. **搬料死循环（背包混装）**
   工人带异种资源（采粮/搬运未卸）⇒ `WorkerInventory.TryStore` 因「单资源背包不可混装」恒返 0 ⇒ `LoadSiteMaterials` 恒失败 ⇒ `Complete` ⇒ 下 tick 重派同一最近工人 ⇒ 死循环（`Build` 类型 `ExecuteCompletion` 无兜底）。
   修复（照 `Transport` 的 `UnloadInventory` 兜底先例）：`LoadSiteMaterials` 内先就地卸空再继续装载（[TaskScheduler.cs:779](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L779)）。

3. **`BuildingFactory.cs:349` 中文注释乱码**（前次 python 补丁写成 `// M1-C ?2??? 4-a ...`）
   该文件纯 CRLF（381 行）⇒ 按行尾纪律走 python **二进制按行替换**修复；核验 `CRLF 381 / bareLF 0`（守恒）。

---

## 八 未动面声明（`git show --stat 7c8a7f1f`）

```
7c8a7f1f HH.315 M1-C（建造/升级/修复/拆除 全走仓）四件落地
 .../Assets/Editor/Smoke/Valley_HH315_M1C_Smoke.cs  | 707 +++++++++++++++++++++
 .../Editor/Smoke/Valley_HH315_M1C_Smoke.cs.meta    |  11 +
 .../Assets/Resources/Config/BuildConfig.asset      |   1 +
 Valley Rampart/Assets/_Game/Data/BuildConfig.cs    |   6 +
 .../Systems/AI/TaskScheduling/TaskScheduler.cs     |  85 +++
 .../Assets/_Game/Systems/AI/Tasks/KingdomTask.cs   |  27 +
 .../_Game/Systems/Building/BuildController.cs      |  31 +-
 .../Assets/_Game/Systems/Building/Building.cs      | 407 ++++++++++--
 .../_Game/Systems/Building/BuildingFactory.cs      |   2 +-
 .../Assets/_Game/Systems/Building/BuildingPanel.cs |  33 +-
 .../_Game/Systems/Building/BuildingSaveData.cs     |  13 +
 .../Systems/Building/ConstructionSiteStore.cs      | 224 +++++++
 .../Systems/Building/ConstructionSiteStore.cs.meta |  11 +
 13 files changed, 1474 insertions(+), 84 deletions(-)
```

**⛔ 一行未动**（工作区另有并行会话未提交改动，本端具名 `git add` 13 文件，⛔ 未 `-A/-u/.`）：

| 面 | 说明 |
|---|---|
| `WarehousePanel.cs` / `TreasureVault.cs` | 任务书 §二 点名不改 |
| 账本（`_编号登记.md` / `_任务队列.md` / `测试基线台账.md` / `_当前快照.md`） | 按 `D767` 纪律，执行端 ⛔ 不代写策划端四档账本；应登记项见 §十 |
| 美术资源（`美术资源文件夹/**`）／`_Game/Art/Ground/**` | 并行会话 |
| `pixel-forge/**` | 并行会话 |
| `Assets/Scenes/GameScene.unity` | 并行会话（本片**零场景变更**） |
| `Packages/manifest.json` / `packages-lock.json` | 并行会话 |
| `3.6_军事统一管理.md` / `3.8_音效系统…md` / `3.6.1` / `3.8.1` | 并行会话 doc |
| `AI.Core/**` / 训练仓 | 零触 |

---

## 九 caveat 与残余（如实列报）

1. **判据5「国库金 100→106」归因未坐实**：金 +6 疑为**测试窗内其它活动**（日结／税收等）所致，⛔ **非拆房退还**；**非金 `{Wood:96}` → `{Wood:96}` 逐值不变已坐实**（退还走箱不经国库的判别面成立）。本端 ⛔ 不声称「金不变」。
2. **探针取证期间临时放宽 `TaskScheduler.taskTimeout` 30 → 3600**（游戏秒）：原值 30 游戏秒 @15x ≈ 30 帧，不足以让工人走到取料仓（实测 300 帧才推进 5.9 世界单位）。**收尾已恢复原值**（探针内存态 · ⛔ **非生产值变更**）。
3. **判据 4 依赖探针临时克隆 `def`**（现有 40 栋 `cost` 全无 `Metal` ⇒ 生产资产上零鉴别力）；改前／改后差值已由「Ore 出现 True/False」坐实，但**非生产资产实测**。
4. **判据 5 的箱数只验「原地」**：⛔ 未验「工人搬回」的后续链（归 `M1-D`／搬运链）。
5. `09#52`（AI 国第二本账 · 统一走仓）**未纳入本片** ⇒ AI 建造仍为台账直扣 ＋ 立即开工（裁决 3 AI-A 明示）。
6. 主城升级（`KingdomManager.TryUpgradeCastle:183-192`）**未纳入** ⇒ 仍为「扣费即生效」（裁决 5 (ii)：另立差异条目挂后续片）。

---

## 十 本端状态声明

- 本片四件**全部落码**，判据 1~7 已实测取证，编译 0 error，**无待裁决项**。
- **⛔ 未 push**（红线）。
- 本端 ⛔ **未碰策划端四档账本**（`D767` 纪律第 8 次遵守）。**应登记项**（供策划端落账）：
  1. `HH.315` 行状态：`🟡 已裁决·待施工` ⇒ **`🟡 已交付·待验收`**（`7c8a7f1f`）；
  2. `_任务队列.md` `M1-C` 派工行 ⇒ 收口为「已交付待验收」；
  3. `测试基线台账.md` 追加 `§九十八`（本片判据 1~7 改前/改后读数 ＋ 5 条裁决落实 ＋ 2 处 bug 修复 ＋ 2 处 caveat）；
  4. `_当前快照.md` 补 `D794` 段并推进水印；
  5. 差异 `09#48`／`#53`／`#54`／`#55`／`#56`／`#64` 六项 ⇒ 建议标「已施工待验收」；
  6. `09#52`（AI 第二本账）与主城升级两条 ⇒ 建议新立挂账条目（挂后续片）。
- 残余 `R1`~`R3`（§九 caveat 1~3）＋ 2 条未纳入面（`09#52`／主城升级）随本报告移交策划端。
