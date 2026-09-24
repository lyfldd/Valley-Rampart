# `HH.329` `M4-A` 挂载与 tick 数据化 任务书

- **签发**：策划端（砚）｜**`D863`**｜**日期**：2026-09-24
- **依据**：`HH.328` 实核｜`08` §3.3／§7.3｜`D863` 裁断
- **范围**：只做 `M4-A`。不做 `M4-B`／`M4-C`／`M4-D`／`M4-E`。`M4-F` 已闭合，不要再做。

## 〇、一句话

`AttachComponents` 仍是 9 处 `if`（`BuildingFactory.cs:238-285`）。`ProductionSystem` 仍点名 4 个类，每秒 tick 一次。本批把挂载改成**数据行上的组件列表**，把 tick 改成**遍历 `ITickable`**。同一栋建筑挂上的组件集合必须和改前一致。

## 一、要做的两件事

1. `BuildingDef` 增加显式组件列表（`08` §7.3：数据行写挂哪些组件）。`AttachComponents` 改为遍历这份列表再 `AddComponent`。现在的 9 处 `if` 不再决定挂什么。
2. 现有会被 tick 的 4 个类（`ProducerComponent`、`BlacksmithBuilding`、`SiegeWorkshopBuilding`、`MineByproductComponent`）实现 `ITickable`。`ProductionSystem.TickAll` 改为遍历 `ITickable`。节奏仍是每秒一次。

## 二、等价

同一份 `BuildingDef`，改前 `AttachComponents` 会挂上的组件类型集合，改后必须相同。包括：经济空仓、投掷机厂不挂通用 `StorageComponent`、铁匠挂 `BlacksmithBuilding` 而不挂 `ProducerComponent`、矿洞副产、`attack > 0` 挂战斗组件、`isConsumable` 挂 `PickupComponent`、裂隙、主城核。

`StorageComponent` 在两个分支里各出现一次，是两段互斥条件，不是同一栋挂两个仓。改后也不许同一栋挂两个 `StorageComponent`。

## 三、判据

1. 抽至少 8 栋现成 def（须含：经济仓、投掷机厂、铁匠、普通产能、矿洞副产、有攻击、可消耗物、主城核）。改前集合与改后集合逐栋相同。
2. `ProductionSystem` 生产码里不再出现这 4 个类名。新增一个带 `Tick` 的测试组件、把它写进某一数据行后，不改 `ProductionSystem` 也会被 tick 到。测完删掉测试组件。
3. tick 间隔仍是 1 秒。`ProducerComponent` 每秒往仓里加产量的行为不变。
4. `TryAdvertiseTask` 的 diff 为 0。`TaskScheduler` 的 diff 为 0。`05` 未改。
5. 不新增 `BuildingAbilityCatalog`。不删 `InteractableType`。不删 6 栋空资产。不删 `SpawnerComponent`。

## 四、红线

- ⛔ 不改 `TryAdvertiseTask`、`TaskScheduler`、`WorkAt`。
- ⛔ 不改 `05` §四那 7 行的完成规则。在岗计时仍在 `TaskScheduler`。产量仍由组件自己在 `Tick` 里累加。
- ⛔ 不把 `IsFortification` 改成读能力表。不建能力表。
- ⛔ 不删除资产、不删除 `CastleCoreComponent`。
- ⛔ 不碰工作区里已有的脏文件（`GameScene.unity`、`Packages`、美术未跟踪文件）。
- 不 commit，不 push。交付报告另取号，先登记再落盘。

## 五、停手

1. 要保持「挂上的组件集合不变」，就必须改 `TryAdvertiseTask` 或 `TaskScheduler`。
2. 某栋 def 改前与改后的组件集合对不上，且你说不清是哪一个 `if` 被漏进列表。先停，把这栋和两个集合列出来。
3. 发现 `ITickable` 已经有人实现。照实报，不要再造第二个接口。

## 六、交付

改动清单（`file:line`）＋ 判据 1 的逐栋对照 ＋ 判据 2 的「测试组件被 tick 到」读数 ＋ `TryAdvertiseTask`／`TaskScheduler` 的 diff 为 0 的证据。
