# HH.335 · 两空类摘除 · 验收裁决

- 签发：策划端｜**D868**｜2026-09-25
- 被验：`多Agent交接/执行端/HH.335_M4-C两空类摘除_交付报告.md`
- 派工：上一任主策划会话里直接签发的提示词（四件、不许动清单、判据 1–5、一条停手），未落档，未取 D 号。本号一并补登。
- 总判：**验收通过。** `M4-C` 整片收口。

## 直读

1. 三文件 numstat 与报告一致：`BuildingComponents.cs` 3+/18-，`Valley_HH329_M4AProbe.cs` 1+/5-，`BuildingMappingTable.cs` 2+/2-。diff 只含派工四件。
2. `SpawnerComponent`、`RiftComponent`、`comp.rift` 在 `Assets` 下 0 命中。键表 8 个（`BuildingComponents.cs:183-190`），`Register` 8 条，`CastleCore` 的来源守卫还在。
3. `ore_vein`、`stone_pile`、`wood_pile` 各一行 `comp.pickup`（`:17`），`PickupComponent` 类在（`:30`）。`castle.asset:18` 仍是 `comp.castle_core`。`BuildingFactory.cs:69` 仍是全库唯一的 `table.Get(BuildingType.CastleCore)`。
4. 映射表资产 8 行，type 1–6、11、12。「11 种」0 命中，「8 条」与数据一致。
5. 编译：DLL 时间晚于两份源文件。控制台只有 Visual Scripting 包的既有异常。
6. 三文件都是 LF。`GameScene.unity` 的改动是 09-15 的，不属本批，不提交。
7. 两个删掉的类都不在同名文件里，场景和 prefab 不可能序列化引用它们。`rift.asset` 已在 `HH.334` 删除。

## 列报处置

1. `BuildingComponents.cs:10` 文件头仍写「留接口空壳：Pickup/Spawner/Combat/Rift/CastleCore」。收下，搭 `HH.336` 改。
2. 控制台异常是包噪声，不处理。
3. `Valley_HH329_M4AProbe.cs:79` 的 id 表仍含 `rift`、`portal`。收下，`HH.336` 连探针一起删（见下）。

## 裁

1. **`M4-C` 整片收口。** `08#34` 由「三删一留」改为「两删两留」：删 `SpawnerComponent`、`RiftComponent`；留 `PickupComponent`（三栋数据行指着它，`D865` 裁 4）和 `CastleCoreComponent`。「主城装配器」只作文档称呼，不改类名。
2. **`isConsumable` 连带清理作废。** 前提是删 Pickup，前提已不成立。三栋资产仍是 `isConsumable: 1`，`R3_SupplyChain.cs:124` 在读它。`08` §4.1 把它归「数据栏（可重生）」，不是死字段。
3. **`BuildingType.Rift` 的废弃注释**（`08#36` 已定，`GridTypes.cs:143` 未做）搭 `HH.336`。
4. **下一片开 `M4-B` 首片：两个死字段。** `D867` 写「`M4-B` 不开」，本号改裁只开这一小片。理由在下节。

## 验收三问

1. 为什么会发生：派工判据 1 只扫全名 `SpawnerComponent`／`RiftComponent`，又只准改一句注释。文件头用简称 `Spawner/Rift` 复述旧状态，扫不到，也不许改。
2. 单次失误还是流程漏洞：流程漏洞，签发侧。`M4-C` 同片第二次出现注释残留（`HH.334` 的「11 种」是第一次）。
3. 教训库缺条目还是没查：没查。`L-78` 已要求清零判据走双轨，文件头注释列在轨② 里。本号在 `L-78` 下补一条实例，不新立。`HH.336` 的判据已写入轨②。

## 下一步

`HH.336`：删 `InteractableType`（字段＋枚举）与 `isMineByproduct`。

- `InteractableType`：`08#32` 已定删（`D761`），全库 `.cs` 只有定义本身。
- `isMineByproduct`：`M4-A` 后挂载改读 `comp.mine_byproduct`，生产码 0 读点。Editor 侧 3 处：`R3_SupplyChain.cs:100`、`Valley_HH291_MapGenProbe.cs:383`、`Valley_HH329_M4AProbe.cs:188/233`。
- `HH329` 探针只用来回放 `M4-A` 改前的 if 链，字段删后无法回放，也没有外部复用。整文件删，`D864` 的日志留着。
- 搭车：`BuildingComponents.cs:10` 文件头、`GridTypes.cs:143` 的 `Rift` 废弃注释。

不动：另外 8 个布尔、`isConsumable`、`IsFortification`、各枚举、资产 YAML。

不选的方向：

- `isBlacksmith`／`isSiegeWorkshop`／`isBridge`／`isGate` 要改成「类型 ID ＋ 能力」，得先有能力表（`M6`）。
- `M5` 卡在 `06` §十-2／-4 未拍。
- `M4-D`／`M4-E` 按 `D863` 延后。
