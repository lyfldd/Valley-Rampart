# HH.333 · 四栋空资产连映射行一起摘

- 签发：策划端｜**D866**｜2026-09-25
- 依据：`HH.332` 编辑器引用核对
- 范围：只动下面列的文件。不删食品工坊和牧场。不删组件类。

## 要做的

1. 删除这四个资产和它们的 `.meta`：
   - `Assets/Resources/Buildings/rift.asset`
   - `Assets/Resources/Buildings/portal.asset`
   - `Assets/Resources/Buildings/ruins.asset`
   - `Assets/Resources/Buildings/treasure_box.asset`
2. 从 `BuildingMappingTable.asset` 摘掉 type 7、type 8、type 10 三行。type 1–6、11、12 保留。type 11 仍指向主城。
3. 从 `BuildingVisual.cs` 的 **def id** switch 删掉这四条：`:56 portal`、`:57 rift`、`:69 ruins`、`:70 treasure_box`。
   `BuildingType` 那个 switch（`:82` TreasureBox、`:83` Ruins、`:84` Rift）**留着**。枚举不删。

## 不许动

- `FoodWorkshop.asset`、`Ranch.asset`、`Module_Production.asset`
- `ChestEntity.cs`、`ChestManager.cs`、`Portal.cs`、`TreasureVault.cs`
- `Building.cs:782` 的 `bld_ruins`（废墟图，不是 `ruins.asset`）
- `PlaceholderSprites` 里的 `bld_ruins`、`bld_portal`、`feat_treasure_box`
- `RiftComponent`、`PickupComponent`、`SpawnerComponent`、`BuildingComponentRegistry`
- `ore_vein.asset`、`stone_pile.asset`、`wood_pile.asset` 的 `comp.pickup`
- `GameScene.unity`、`Packages`、美术未跟踪文件

## 判据

1. 上述四个 `.asset` 路径不存在。映射表里不再出现这三个 guid：`4cf205a222b91c34d9a404b969ce423b`、`5f0f3cef7dde9984cbbfc8305b0ef942`、`3625478e6de9ba740b90db1bf0111f24`，也不出现 `52463a62a124bf148b737cb3e7d5decb`。
2. 映射表仍有 type 11，`BuildingFactory.cs:69` 仍是 `Get(BuildingType.CastleCore)`，且这仍是 `BuildingMappingTable.Get` 的唯一生产调用。
3. `ChestEntity.cs`、`Portal.cs`、`Building.cs` 里的 `bld_ruins` 还在。`FoodWorkshop.asset` 和 `Ranch.asset` 还在。`RiftComponent` 类还在。`ore_vein.asset` 仍有 `comp.pickup`。
4. def id switch 里不再有 `"portal"`、`"rift"`、`"ruins"`、`"treasure_box"`。`BuildingType.Rift`、`BuildingType.Ruins`、`BuildingType.TreasureBox` 的 case 还在。

## 停手

编辑器反向引用里，这四栋除了映射表以外还有别的引用方。停下，把路径列出来，不要删。

不 commit，不 push。交付报告另取号。
