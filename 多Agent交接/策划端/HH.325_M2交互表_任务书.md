# `HH.325` `M2` 交互表（7 行）任务书

- **签发**：策划端（砚）｜**依据 `D861`**｜**日期**：2026-09-24
- **性质**：只填表（⛔ 不写生产码／不改资产／不改 `05`／`06`／`中层执行计划`）
- **前置**：`HH.324` 实核已裁决。还原批已通过，本批不再碰 `RequestHaulNow`。

## 〇、一句话

按 `D861` 裁 2，交出 **7 行交互表**。每格都要有当前工作区的 `file:line`。填不出就写「不可判」和缺什么，不要编。

## 一、固定 7 行（不得加减，不得重排）

| # | 行 | 现码锚点（`HH.324` 报告，行号以你实读为准） |
|---|---|---|
| 1 | 采集 | `WorldGatherSource` 广告 ＋ `ExecuteCompletion` 采集支 |
| 2 | 搬运 | `Building` 强制／阈值、`ChestEntity`、`MineByproductComponent` 的 `Transport` |
| 3 | 搬水 | `Building` 的 `WaterHaul` |
| 4 | 搬料 | `ConstructionSiteStore` 的 `HaulToSiteArgs`（`Build` 的一义） |
| 5 | 装填 | `UnitController` 的 `AmmoReload` |
| 6 | 生产 | `Building`／`BlacksmithBuilding`／`SiegeWorkshopBuilding`／`MineByproductComponent` 的 `Production` |
| 7 | 拆除 | `Building` 的 `DemolishTaskArgs`（`Build` 的另一义） |

`Build` 在枚举里仍是一项。表上第 4 行与第 7 行必须分开。枚举不要删。

不收入：`Repair`／`Produce`／`Rancher`／`WaterCarry`／`GoldMine`（生产码零构造）、`DispatchCrew`、`WorkerTask.DebugTaskSource`、`AIDebugSpawnController`。

## 二、每行 8 栏（`05` §四）

| 栏 | 怎么填 |
|---|---|
| 交互ID | `对象_能力`。先读 `10_建筑能力表.md` 已有能力名（`gather`／`store`／`convert`／`attack`／`occupy`／`provide`／`aura`／`tax`，及后加的 `modifier`）。这一行**就是**其中某一条，才复用那个词。对不上就用动作本名，备注写「10 无同名，本片不新增能力条」。 |
| 谁可以做 | 现码谁能接。现为职业白名单就写出白名单的 `file:line`。不要设计新名单。 |
| 目标须提供 | 现码广告成立时，目标侧要满足的条件。 |
| 条件 | 不满足就不能做的那一条，带 `file:line`。 |
| 进度 | **看哪个字段、到什么值算完**。必须是代码里的字段和值。`05` §五的示例（数量→0、建造度→100 等）只是旧例子；与现码不一致就写现码，并标「与 §五示例不一致」。不要造通用进度条。 |
| 产物去哪 | 改自己的那一侧（背包／仓／无）。 |
| 目标怎么变 | 改对方的字段和改多少。 |
| 耗时 | **单次动作**的时长。来源是 `GetTaskDuration` 或这条链实际用的工作时长。`tickInterval`／`taskExpiry`／`taskTimeout` 不要写进这一栏。 |

## 三、判据

1. 表恰好 7 行，顺序与 §一相同。第 4 行与第 7 行不是同一行。
2. 8 栏无一空白。不可判的格子写明缺哪段代码。
3. 每个「进度」格能指到字段名和完成值，并带 `file:line`。
4. `git diff` 的生产码（`Assets/**`、`.asset`、`.unity`）为 0。本批新增只有交付报告。

## 四、红线

- ⛔ 不改 `TaskScheduler.cs`、8 处 `TryAdvertiseTask`、`WorkAt`、`AI.Core`。
- ⛔ 不建 `BuildingAbilityCatalog`，不删 `KingdomTaskType`。
- ⛔ 不改 `最高优先级文档/**`。表写在交付报告里，由策划端再收进 `05`。
- ⛔ 不碰工作区里其他已脏文件。
- ⛔ 不编译、不跑局、不 commit、不 push。
- 交付报告编号：对着 `多Agent交接/_编号登记.md` 的在途行另取，先登记再落盘。本任务书不预留报告号。

## 五、停手

1. 要填某一格就必须改生产码。
2. 某行的「什么叫完成」在现码里不是一个字段到一个值，而且你说不清它看的是什么。
3. 按「有生产调用点 ＋ 有完成回调 ＋ 有消费方」又发现第 8 条在跑链。列出来，不要自行加进行。

## 六、交付

`多Agent交接/执行端/` 下一份交付报告：7 行表 ＋ 每格 `file:line` ＋ 判据 1～4 的命令输出 ＋ 零代码声明（`git status`／`git diff --stat`）。
