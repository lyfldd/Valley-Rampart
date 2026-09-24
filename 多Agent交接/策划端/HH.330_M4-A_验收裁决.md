# HH.330 · M4-A 挂载与 tick 数据化 · 验收裁决

- 签发：策划端｜**D864**｜2026-09-24
- 被验：`多Agent交接/执行端/HH.330_M4-A挂载与tick数据化_交付报告.md`
- 依据：`HH.329` 任务书｜`08` §3.3／§3.4／§7.3｜`HEAD` 上改前的 `AttachComponents`
- 总判：**验收通过**。`M4-A` 关掉。`M4-B`／`C`／`D`／`E` 不开。

## 判据三直读

1. 设计稿：`08` §2.2／§2.3（改前病灶）、§3.3／§3.4（目标流程）、§7.3（数据行显式列表）、§六判据 1／2／6。任务书 `HH.329` 全文。
2. 代码：改前函数取自 `git show HEAD` 的 `BuildingFactory.AttachComponents`（`:238` 起，9 处挂载）。现码 `BuildingFactory.cs:243-255`、`BuildingComponents.cs:21-25` 与 `:189-257`、`ProductionSystem.cs:13` 与 `:32-52`、`BuildingDef.cs:69`。四个实现者：`ProducerComponent.cs:8`、`BlacksmithBuilding.cs:12`、`SiegeWorkshopBuilding.cs:19`、`MineByproductComponent.cs:30`。`ITickable` 实现者全库只此 4 个。
3. 字段：40 个 `BuildingDef` 资产的 `components` 与产能／战斗／布尔字段。`Well.asset` `rate: 4`。`Warehouse.asset` 磁盘仍只有 `comp.storage`。

## 独立复算

用 `HEAD` 的 9 处条件，对当前 40 栋资产重放。运行时来源取各栋 `sourceType`。预测键序与数据行 **40/40 相同，差 0**。资产 diff 里除 `components` 行以外为 0。抽证：

| def | 改前（按 sourceType） | 数据行 |
|---|---|---|
| Warehouse | storage | 同 |
| SiegeWorkshop | siege_workshop（无通用 storage） | 同 |
| Blacksmith | storage, blacksmith（无 producer） | 同 |
| farm／Well | storage, producer | 同 |
| mine | mine_byproduct（无 storage） | 同 |
| arrow_tower | combat | 同 |
| ore_vein | pickup | 同 |
| castle（sourceType=CastleCore） | combat, castle_core | 同 |
| rift（sourceType=Rift） | rift | 同 |

`castle` 在来源为 `None` 时，改前集合只有 `combat`。注册表 `:220-223` 的守卫保住这条。

`TaskScheduler.cs` 与 `Building.cs` 相对 `HEAD` 的 diff 都是空。`ProductionSystem.cs` 生产码不含那 4 个类名。`_tickInterval` 仍是 `1f`。`Well` 的 `Tick` 把 `rate` 累进仓，`rate=4` 与日志 `Δ4` 一致（`Logs/hh329_m4a_probe.log`）。判据 2 的测试件已不在工程里；日志 `Logs/hh329_tick2_probe.log` 记 `0 → 1 → 2`，且 `Warehouse.asset` 未被写成测试键。`BuildingAbilityCatalog` 全库 0。`InteractableType` 仍在 `BuildingDef.cs:100`／`:179`。`SpawnerComponent` 仍在 `BuildingComponents.cs:36`。6 栋空资产都在。

## 列报 1

**留下** `comp.rift` 与 `comp.castle_core` 的 `sourceType` 守卫。

改前这两条读的是运行时 `b.sourceType`，不是数据行。玩家建造（`Building.cs:431` 置 `None`）和调试入口传 `None`。去掉守卫，`castle` 调试实例会多挂主城核。那是行为变化，超出本批「集合与改前相同」。纯数据行等到来源层再谈，本批不另立项。

列报 2、3、4 照收，不另动作。列报 5：文书与本批生产码一并具名提交。

## 验收三问

1. 守卫之问从哪来：旧条件读运行时来源，数据行写不下这条，执行端停手报了。
2. 执行端没有失误。`08` 与 `中层执行计划` 里「if 链仍在」是 `D863` 开工前的现状句，施工后失真，本裁改写（`L-63`）。
3. 教训库不新立。本批用了 `L-63`、`L-88`（40 栋是本端复算；tick 读数是日志，测试件按任务书已删）、`L-90`（现状句两处一起改）。

## 不在本批

`M4-B`／`C`／`D`／`E` 仍不开。`08` §六的能力可查、10 个布尔退役、死字段删除，都还没做。
