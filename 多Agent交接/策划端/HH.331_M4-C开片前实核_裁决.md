# HH.331 · M4-C 开片前实核 · 裁决

- 签发：策划端｜**D865**｜2026-09-25
- 被裁：`多Agent交接/执行端/HH.331_M4-C开片前实核_交付报告.md`
- 总判：**实核采信。停手 3 正确。不放行删除。**

## 直读

1. `08` #34／#39／§九-6，中层计划 §4.4 的 `M4-C` 行。
2. 菜单链：`BuildingMenuPanel.cs:209` 收 `isPlayerBuilt`；`:230` 按 `moduleType` 分栏；`:233` 走 `KingdomManager.IsBuildingUnlocked`。`KingdomManager.cs:240-242` 特殊建筑要求模块等级 ≥ 首次解锁 tier。`FindSpecialUnlockTier:287-288` 读的是 tier 字段，不是数组下标。
3. `Module_Production.asset:34-36`：tier 2 的 `unlockBuildings` 是 `FoodWorkshop`、`Ranch`。两栋资产 `moduleType: 1`（`ModuleType.Production`）、`isPlayerBuilt: 1`。`BuildingFactory.CreateBuildingInstance` 的 `isConsumable` 形参只在 `:90` 声明，方法体内无读取。`SpawnerComponent` 全库只在 `BuildingComponents.cs:36`。

## 裁

1. **`FoodWorkshop` 与 `Ranch` 划出删除名单。** 生产模块等级 ≥ 2 时，建造菜单会列出它们。`08` §九-6「直接删」和把它们算进死资产，与现码冲突。不删玩家能造的建筑。它们没有 `components` 栏，功能空，留给能力表，不在清理批里拆掉。
2. **`rift`／`portal`／`ruins`／`treasure_box` 仍无创建路径。** 地图生成、地图预置、AI 模板、AI 行动面都不创建它们。这个判断采信。本裁仍不放行删除：报告已说明本仓库的 guid 文本对不上，字符串「零引用」不能当删除许可证。同名活物（`ChestEntity`、`bld_ruins`、`bld_portal`、`Portal.cs`）不动。
3. **`SpawnerComponent` 是孤儿类。** 无键、无数据行、无挂载。可以单删这个类。本裁不另开工。
4. **`PickupComponent`／`RiftComponent` 的 `Init` 仍空，但数据行已经指着它们。** 不删类。`CastleCoreComponent` 留下（`Init` 挂 `ThroneAnchor` 与 `TreasureVault`）。来源守卫维持 `D864`。

## 验收三问

1. 停手从哪来：2018-09-18 的「删壳」写进了 `08`，资产和菜单都还在。食品工坊与牧场进了生产模块解锁表。
2. 执行端没有失误。他们按停手条件停了。文档把「已裁删」写成了既成事实，这是策划侧的旧句。
3. 教训库不新立。本裁按 `L-63` 改写 `08` 与中层计划里的删除句。

## 下一步

不派删除批。`M4-B`／`D`／`E` 与 `M5` 仍不开。
