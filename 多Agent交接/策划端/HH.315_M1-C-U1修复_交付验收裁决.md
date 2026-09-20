# HH.315 · `M1-C` · `U-1` 修复（丙′）—— 交付验收裁决

- **裁决端**：策划端（砚）　**裁决日**：2026-09-20（`D796`）
- **被验代码**：commit `75b5f3c4`（代码 4 文件 · `+136/−50`）／报告 1 文件（`+202`）
- **被裁文书**：`多Agent交接/执行端/HH.315_M1-C-U1修复_交付报告.md`
- **裁决依据**：判据三直读 —— ①`09` §16.2／§16.1-4 ②代码 `file:line`（本端逐行复读）③`.asset` 逐值（前轮已读）
- **裁决结论**：**⚠️ 有条件通过 · ⛔ 不销号**（判据 **1·2·3·5·6·7** 成立；⭐ **新发现 `U-4`** 须修 1 笔）

---

## 〇 · 本端独立取证（判据三直读 · ⛔ 零采信转述）

| # | 取证项 | 实读落点 | 读数 |
|---|---|---|---|
| 1 | 改动面 | `git show --stat 75b5f3c4` | 5 文件（代码 4 ＋ 报告 1）｜代码 `+136/−50` ＝ 报告所称一致 ✅ |
| 2 | 退还主口 | `Building.cs:890-902 BuildRefundPack` | `pack = PaidStageCost()`；`if (_awaitingMaterials)` ⇒ `+= GoldOnlyOf(CurrentStageCost())` ＋ `+= _siteStore.Contents` |
| 3 | 核心 helper | `Building.cs:911-941 PaidStageCost` | `inProgress = state==Constructing`；`paid = inProgress && !_awaitingMaterials`；`isBuild = inProgress && InProgressStageIsBuild()`；`isRepair = inProgress && _pendingRepair`；①`if (!isBuild \|\| paid) += def.cost`；②③`n = Max(0, level-1)`（在投已付清且非建非修 ⇒ `n++`）⇒ 累加 `levels[0..n-1]`；④`if (isRepair && paid) += pack * RepairCostRatio()` |
| 4 | 在投配方 | `Building.cs:947-953 CurrentStageCost` | 修复 ⇒ `GetRepairCost()`；升级 ⇒ `levels[level-1].upgradeCost`；否则 ⇒ `def.cost` |
| 5 | 在读档兜底 | `Building.cs:966-972 InProgressStageIsBuild` | 四级：`_pending*` ⇒ 非建造／`level>1` ⇒ 非建造／`_siteNeed.IsZero` ⇒ 是建造／`SamePack(_siteNeed, SiteNeedOf(def.cost))` |
| 6 | 单源工具 | `Building.cs:256-267 SiteNeedOf`（去金）／`:271-282 GoldOnlyOf`（取金）／`:975-981 SamePack`／`:984-985 RepairCostRatio` | 互补单源 ✅；`RepairCostRatio` 缺省 `0.5` |
| 7 | 修复费 | `Building.cs:1148-1154 GetRepairCost` | `PaidStageCost() * RepairCostRatio()`；⛔ 不读 `totalInvested` ✅ |
| 8 | ⭐ 掉箱调用面 | `Building.cs:877`（`FinishDemolish` 内）＋ `:1247`（`Die` 内） | **两处**；`:1245` 注释自陈"`FinishDemolish` 已先行掉箱并清空 ⇒ 此处幂等" |
| 9 | ⭐ `_siteStore.Contents` 读点 | `:899`（进 refundPack）／`:994`（掉箱）／`:1051`（入档） | ⛔ **同批料被 `:899` 与 `:994` 各读一次** |
| 10 | `BuildRefundPack` 调用面 | 全库 grep | **唯一调用点 `:869`** ✅ |
| 11 | N-1 后果① | `Building.cs:691 OnConstructionComplete` | `if (_pendingUpgrade && …) { level++; …; _pendingUpgrade = false; }` ⇒ ⭐ **读档后 `_pendingUpgrade=false` ⇒ 材料已付、等级不涨** ✅ 报告成立 |
| 12 | 退役确认 | 全库 grep `baseSum`／`SumCostOf`／`IsRefundResource` | 生产码**零命中**（旧口径已整体退役）✅ |

---

## 一 · 判据复核（本端逐值独立复算）

| # | 样本 | 报告读数 | **本端复算**（按 `:911-941` 算法逐步） | 判 |
|---|---|---|---|---|
| **1** | House L1（`level=2`·Active） | `{Wood:14,Stone:6}` | `isBuild=false`⇒②`+=def.cost{Wood:4}`；`n=1`⇒`+=levels[0]{Stone:6,Wood:10}` ⇒ **{Wood:14,Stone:6}** | ✅ |
| **2** | House 满级（`level=3`） | `{Wood:38,Stone:22}` | `n=2`⇒`levels[0]+levels[1]` ＝ `{Stone:6,Wood:10}+{Stone:16,Wood:24}` ＋ `{Wood:4}` ⇒ **{Wood:38,Stone:22}** | ✅ |
| **3** | Warehouse L1 | `{Gold:4,Stone:14,Wood:10}` | `{Gold:4,Stone:4}+{Stone:10,Wood:10}` ⇒ **{Gold:4,Stone:14,Wood:10}** | ✅ |
| **4** | House 未升级（空对照） | `{Wood:4}` | 与改前同值 ⇒ **无鉴别力**（报告已显式标注 ✅） | ⚠️ 认可 |
| **5** | 升级投料中 `Stone 3/16` | `{Wood:4,Stone:3}` | `paid=false`⇒①`+=def.cost`＝{Wood:4}；`n=0`⇒⛔ 不加升级；`_awaitingMaterials`⇒`GoldOnlyOf(levels[0])`＝**空** ＋ `_siteStore.Contents{Stone:3}` ⇒ **{Wood:4,Stone:3}** | ✅ |
| **6** | 修复费 ×3 | `{Wood:2}`／`{Wood:2}`／`{Wood:2}` | `PaidStageCost(){Wood:4} × 0.5`＝{Wood:2} 恒定（基数与修复历史无关）⇒ **U-3 复利消失** | ✅ |
| **7** | 存档往返 4 态 | 逐值 True | 7c 首次建造投料中：`isBuild=true`（`SamePack` 命中）⇒ ①`!isBuild\|\|paid`＝**false**⇒⛔不加 `def.cost` ⇒ `PaidStageCost`＝空；再 `+= siteContents{Wood:3}` ⇒ **{Wood:3}**（⛔ 非 {Wood:7}） | ✅ |
| **8** | 升级料齐未完工 | `{Wood:14,Stone:6}` | `paid=true`、`isBuild=false`⇒①`+def.cost`；`inProgress&&paid&&!isBuild&&!isRepair`⇒`n++`＝1 ⇒ `+=levels[0]` ⇒ **{Wood:14,Stone:6}** | ✅ |
| 9 | `farm` 纯金建造中 | `{Gold:50}` | `_siteNeed.IsZero`⇒`isBuild=true`；`paid=false`⇒①不加；`n=0`；`awaiting`⇒`GoldOnlyOf(def.cost){Gold:50}` ⇒ **{Gold:50}** | ✅ |
| 10 | AI 式直建 | `{Wood:4}` | 同判据 4 路径 | ✅ |

⇒ ⭐ **算法实现与读数逐值自洽，本端复算全部命中**；`PaidStageCost` 的分支设计（①的 `!isBuild || paid` 守卫、②③的 `n++` 条件、④的 ratio 单源）**正确**。

### 1.1 ⚠️ 但判据集**缺一个维度** —— 见 §二

判据 5／7a 测的是 **`BuildRefundPack()` 的返回值**，⛔ **不是"拆除后落箱总量"** ⇒ **漏掉与 `DropSiteStoreToChest()` 的交互**。

---

## 二 · ⭐⭐ 本端新发现 `U-4`（本片**新引入** · 投料中态拆除 ⇒ 双重退还）

### 2.1 实读链条

```
FinishDemolish()  [Building.cs:867-879]
  :869  var refundPack = BuildRefundPack();          // ⭐ 若 _awaitingMaterials ⇒ 含 _siteStore.Contents（:899）
  :873  SpawnChest(coord, refundPack, Faction.None); // 落在同一 coord
  :877  DropSiteStoreToChest();                      // ⭐ 又取 _siteStore.Contents（:994）掉一次 ⇒ Clear()
  :878  Die(DeathCause.Demolished);                  // :1247 再掉 ⇒ 已清空 ⇒ 幂等 ✅
```

- `_siteStore.Contents` 被 **`:899` 与 `:994` 各读一次** ⇒ **同一批料进两个箱子**。
- ⚠️ `SpawnChest` **不去重**（`EnforceCellLimit` 只在该格超上限时移除**最早**的箱）⇒ 两箱并存。

### 2.2 逐值反演（House 升级投料中 · `Stone 3/16`）

| 步 | 内容 |
|---|---|
| `BuildRefundPack()` | `{Wood:4} + {} + {Stone:3}` ＝ **{Wood:4, Stone:3}** |
| `SpawnChest` | 落箱 **{Wood:4, Stone:3}** |
| `DropSiteStoreToChest` | 又落箱 **{Stone:3}** |
| ⛔ **合计** | **{Wood:4, Stone:6}** ⇒ **Stone 多退 3**（玩家从未搬进的那份） |

### 2.3 归属判定

- ⛔ **非既有缺陷**：改前 `BuildRefundPack` **不读** `_siteStore.Contents`（`:899` 是本片新增）⇒ 改前该路径只有 `DropSiteStoreToChest` 掉一次 ⇒ **无重复**。
- ⇒ ⭐ **`U-4` ＝ `75b5f3c4` 新引入**（触发条件＝**投料中态拆除** —— 恰是丙′ 要处理的场景）。
- ⚠️ 执行端注释写「按**已到料**（⛔ 非需求全额）—— 否则与 `DropSiteStoreToChest()` 掉出的同一批料**双重退还**」⇒ ⭐ **它意识到了重复风险，但只排除了"未到料部分"，未排除"已到料部分"** ⇒ 已到料仍被计两次。

### 2.4 裁决：修法 **(a)** —— `BuildRefundPack` **不再叠加 `siteContents`**

| 方案 | 内容 | 本端判 |
|---|---|---|
| **(a)** ⭐ **准** | `BuildRefundPack` 去掉 `:899` 的 `+= _siteStore.Contents`（`GoldOnlyOf` 保留）；工地仓内容**唯一**由 `DropSiteStoreToChest` 掉出 | **职责分离**：`BuildRefundPack` ＝ 「退还**已支付的造价**」；`DropSiteStoreToChest` ＝ 「工地仓**内容物**掉箱」。⚠️ 后者**必须保留**（`Die` 路径靠它 ⇒ 工地被打毁时材料不丢） |
| (b) | `FinishDemolish` 在 `_awaitingMaterials` 时**跳过** `DropSiteStoreToChest` | ⛔ 使 `FinishDemolish` 出现"有时掉有时不掉"的分支（更脆）；且与 `Die` 的幂等注释语义冲突 |

⇒ ⚠️ **修 (a) 后判据 5 的读数会变**：`BuildRefundPack()` 返回 **`{Wood:4}`**（仅造价部分）；而**落箱总量**仍 ＝ `{Wood:4} + {Stone:3}` ＝ **`{Wood:4, Stone:3}`**（正确）⇒ ⭐ **判据须改「落箱总量」口径**（见 §五 `L-48`）。

---

## 三 · `N-1` ~ `N-3` 裁决

| # | 争点 | 裁决 |
|---|---|---|
| **`N-1`** | `_pendingUpgrade`／`_pendingRepair` **未入档** ⇒ ① 读档后中途升级完成**不升级**（`:691` 实读坐实 · **既有 `M1-C` 缺口**）② 本修复须加读档兜底（已用四级判据覆盖） | ⭐ **准：另立独立小片**（尾插两字段 · 零 bump · 与 `M1-C` 自身先例一致）⇒ 立 **`U-5`**。⚠️ **不阻塞 `M1-C` 销号**（窄场景：仅"读档时正在投料/施工的升级/修复"；且退还算式已有兜底）。⚠️ **但须保留四级兜底的注释警告**（"新增资产若配方碰撞须先补 `_pending*` 入档"）—— 这正是兜底的**失效条件**，⛔ 不得删。 |
| **`N-2`** | `BuildingFactory.cs:346` 同款陈注释（未列入授权 ⇒ 未改） | ✅ **准补 1 行**（零行为）。⚠️ 并入 `U-4` 补丁同批（同文件域）。 |
| **`N-3`** | `Building.LoadState` 不恢复 `state`（依赖 `SpawnFromSave:292/155`） | ✅ **本片不动**；但 ⭐ 裁：**须在 `LoadState` 加防御性注释**（写明"`state` 由创建时 `initialState` 置入 · ⛔ 勿直接对已存在实例调本方法"）—— 零行为。⚠️ 并入 `U-4` 补丁同批。 |

---

## 四 · `C-1` ~ `C-5` 裁决（执行端请裁汇总）

| # | 争点 | 裁决 |
|---|---|---|
| `C-1` | `N-1` 是否尾插 `_pending*` 入档 | ✅ 准「另立小片」（＝ §三 `U-5`） |
| `C-2` | `N-2` 补勘正 | ✅ 准（并入 `U-4` 批） |
| `C-3` | `N-3` `state` 恢复 | ✅ 不动逻辑；补防御性注释（并入 `U-4` 批） |
| `C-4` | `RulerController.Refund`／`KingdomState.Refund` 的 `ratio` 形参去留 | ⚠️ **本片不动签名** ✅ 认可；去留归**后续片**（与 `U-2`／`M1-D` 同批评估） |
| `C-5` | 判据 4／6／7 无鉴别力组 | ✅ **认可**：已显式标注；且判据 7c 的**新分支负向验证**（⛔ 未读成 `{Wood:7}` 双计）**有独立价值** |

---

## 五 · 新立挂账 ＋ 教训

| 编号 | 内容 | 归属 | 状态 |
|---|---|---|---|
| **`U-4`** | ⭐ **投料中态拆除 ⇒ `siteContents` 双重退还**（`BuildRefundPack:899` 与 `DropSiteStoreToChest:994` 各计一次）· **本片新引入** | `M1-C` **补丁片**（修法已裁 ＝ (a)） | ⏳ **待派 · 阻塞销号** |
| **`U-5`** | `_pendingUpgrade`／`_pendingRepair` **未入档** ⇒ 读档后中途升级完成**不升级**（既有 `M1-C` 缺口） | **另立独立小片** | ⏳ 待派 · ⛔ 不阻塞 `M1-C` 销号 |
| `U-1`／`U-3` | 已修（本片 `75b5f3c4`） | — | ✅ 待 `U-4` 一并销号 |

⭐ **新立 `L-48`**：**判据必须测「用户可见的最终状态」，⛔ 不是「中间函数的返回值」** —— 判据 5／7a 测 `BuildRefundPack()` 返回值（＝中间量），**未测"拆除后落箱总量"** ⇒ 漏掉与 `DropSiteStoreToChest` 的叠加 ⇒ `U-4` 漏网。
- **同族**：与 `L-44`／`L-45` **同一病根**（"判据在某个维度上不可分辨"）⇒ ⭐ **`L-44`／`L-45`／`L-48` 合为通则**：**判据的定义域必须覆盖「该行为的全部产出面」**（金额 × 去向 × **叠加**）。
- **防范（可复用操作句）**：写"产出量"类判据时 ⇒ ①列出该行为**全部写入点**（本例：`SpawnChest` 的**每一处**）②读数取**这些写入点的合计**，⛔ 不取任何单个函数返回值。

---

## 六 · 落账

| 件 | 文件 | 动作 |
|---|---|---|
| ① | `多Agent交接/策划端/HH.315_M1-C-U1修复_交付验收裁决.md` | **新建**（本文） |
| ② | `河谷防线开发计划书具体内容/测试基线台账.md` | 追加 **§一百** |
| ③ | `多Agent交接/_编号登记.md` | `HH.315` 行追加「`U-1` 修复交付验收（`D796`）」 |
| ④ | `多Agent交接/_任务队列.md` | `M1-C` 行更新；⭐ 新插 **`U-4`**／**`U-5`** 两挂账行 |
| ⑤ | `多Agent交接/_当前快照.md` | 补 **`D796`** 段 |
| ⑥ | `多Agent交接/策划端/_策划教训库.md` | 详述区末尾新立 **`L-48`** |
| ⑦ | `.workbuddy/memory/2026-09-20.md` | 本笔 |

---

## 七 · 状态

- ⛔ 本端本轮**零代码零资产**（仅裁决 ＋ 账本文档）。
- **`M1-C` 状态**：**⚠️ 有条件通过 · ⛔ 不销号** ⇒ 待修 **2 笔**：**`U-4`**（本片引入 · 补丁）＋ **件 3 依赖 `U-2`**（另立片）。
  - ✅ `U-1`（金额口径）＋ `U-3`（复利）**已修且复算通过**。
- **▶️ 下一步**：
  1. **`U-4` 补丁**（同文件域打包）：`BuildRefundPack` 去 `:899` 叠加 ＋ `N-2`（`BuildingFactory:346` 注释）＋ `N-3`（`LoadState` 防御注释）⇒ 补**判据 8′「落箱总量」**；
  2. **`U-2` 独立片**（箱→仓搬运链 ＋ 拾取入包 · **阻塞级**）；
  3. **`U-5` 独立小片**（尾插 `_pending*` 两字段）。
