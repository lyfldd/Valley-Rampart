# HH.332 · M4-C 引用核对 · 裁决

- 签发：策划端｜**D866**｜2026-09-25
- 被裁：`多Agent交接/执行端/HH.332_M4-C引用核对_交付报告.md`
- 总判：**核对采信。四栋可以删，但必须连映射表三行一起摘。** 施工见 `HH.333`。

## 直读

1. `BuildingMappingTable.asset:28-33`：type 7 的 guid 是 `4cf205a222b91c34d9a404b969ce423b`，type 8 是 `5f0f3cef7dde9984cbbfc8305b0ef942`，type 10 是 `3625478e6de9ba740b90db1bf0111f24`。与 `Logs/hh332_refscan.log` 里宝箱、遗迹、裂隙的 `AssetPathToGUID` 逐位相同。表里没有 `portal` 的 `52463a62a124bf148b737cb3e7d5decb`。
2. `BuildingMappingTable.Get` 的生产调用只有 `BuildingFactory.cs:69` 的 `Get(BuildingType.CastleCore)`。`BuildLookup` 会把每行装进字典，没有第二处 `Get`。
3. `portal.asset:59` 的 `sourceType` 是 10，与裂隙相同。映射表 type 10 指向 `rift.asset`，不指向 `portal.asset`。
4. 日志：反向扫描候选 9816，实扫 `Assets/` 1224，跳过 8592 是 `Assets/` 以外。四栋在 `Assets/` 内的引用方只有映射表三行；`portal` 为 0。`GameScene` 命中 0。

## 裁

1. **编辑器引用核对成立。** `.meta` 的 41 字节 guid 行不是 YAML 里的 32 位 guid。`HH.331`「meta 零命中就是没人引用」作废。
2. **三行映射是资产引用，不是创建路径。** 运行时只拿主城那行。删资产而不改表，会留下三处丢失引用。
3. **`HH.333` 放行。** 只删这四栋资产，并摘掉映射表 type 7、8、10。`BuildingVisual` 只删按 def id 的四个 case（`:56`、`:57`、`:69`、`:70`）。`BuildingType` 那个 switch（`:82-84`）留着。枚举项不删。
4. **不动：** 食品工坊、牧场、`ChestEntity`、`ChestManager`、`bld_ruins`、`bld_portal`、`Portal.cs`、`feat_treasure_box`、`TreasureVault`、`RiftComponent`、`PickupComponent`、`SpawnerComponent`、矿脉、石堆、木堆。

## 验收三问

1. 引用从哪来：片 6-2 之后地图不再按这张表生成自然建筑，表上旧行还在。
2. 执行端没有失误。他们改口了自己上一批的 guid 结论，并交出了编辑器读数。
3. 教训库不新立。`D865` 已经拒绝用 `.meta` 文本判引用。

## 下一步

执行端按 `HH.333` 施工。`M4-B`、`M4-D`、`M4-E`、`M5` 不开。
