# HH.334 · 四栋空资产摘除 · 验收裁决

- 签发：策划端｜**D867**｜2026-09-25
- 被验：`多Agent交接/执行端/HH.334_M4-C四栋空资产摘除_交付报告.md`
- 总判：**验收通过。** 四栋资产和映射表三行已经摘掉。组件类没删，`M4-C` 不因此整片收口。

## 直读

1. `Resources/Buildings` 现 37 个 `.asset`。`rift`、`portal`、`ruins`、`treasure_box` 的资产和 `.meta` 都不在。
2. `BuildingMappingTable.asset` 八行：type 1–6、11、12。type 11 的 guid 是 `b0d1ffe0afa9a6644a4c18722efc0c67`（主城）。四个旧 guid 在 `Assets` 的 `.cs`／`.asset`／`.prefab`／`.unity` 里是 0 命中。
3. `BuildingVisual.cs` 的 def id switch 已无这四个 id。`BuildingType` switch 仍在 `:78` TreasureBox、`:79` Ruins、`:80` Rift。
4. `BuildingFactory.cs:69` 仍是 `table.Get(BuildingType.CastleCore)`，全库只这一处。
5. 仍在：`FoodWorkshop.asset`、`Ranch.asset`、`ore_vein.asset` 的 `comp.pickup`、`RiftComponent`（`BuildingComponents.cs:162`）、`Building.cs:782` 的 `bld_ruins`。

## 列报

三条都收下，不另派。探针残留、`BuildingMappingTable.cs` 仍写「11 种」、`R5_SixStage` 的名字别名，都不挡销号。下次碰到这些文件再改注释。

## 验收三问

1. 列报从哪来：任务书只准动资产、映射表和四个 def id case。
2. 执行端没有越界。删前又扫了一遍引用，停手条件没触发。
3. 教训库不新立。

## 下一步

不派下一片。`M4-B`、`M4-D`、`M4-E`、`M5` 不开。`SpawnerComponent`、`PickupComponent`、`RiftComponent` 仍留着。
