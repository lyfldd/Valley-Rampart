# HH.8 2_12 步骤3 仓库统一抽象 IWarehouse 范围待策划裁决

> 类型：待决策（重构序范围）
> 状态：⏳待裁决
> 日期：2026-08-22 · 发起端：执行端 · 关联清单/文档：2_12 步骤3（实施计划 L67-99）/ 2_12 设计稿 §5.0（L208-247）/ D43/D51/D200/D255 / 2_9 sim 对拍

## 一、上下文（执行端已做的）
- 2_12 前置 7/7 通过（步骤1 核验闭合、步骤2 伐木场清理已提交 `7f77b05`）。
- 步骤3「仓库统一抽象 IWarehouse」是全库资源流动重构，我已调研完现状，确认它是**架构级改造**，触碰设计稿核心接口语义，需策划先定范围。

## 二、现状调研结论（证据）
- **Unity 侧无任何 IWarehouse/Warehouse**（仓库=带 StorageComponent 的 producer 建筑）。
- `StorageComponent`（Systems/Building/StorageComponent.cs）：storedAmount/capacity/Add/TakeOut/HarvestCarry，动态挂载。
- `RulerController`（Systems/Ruler/RulerController.cs）：全局资源池=Gold/Stone/Wood/Food/SpecialFood/Meat，`ModifyResource` 统一增/减（L407）、`Spend/CanAfford/Refund`。
- `WorkerInventory`（Systems/Unit/WorkerInventory.cs）：工人背包（carriedType/carriedAmount），采集先入背包，溢出才入国库。
- `ResourceType` 枚举（Systems/Core?/GameEvents.cs L85-98）：Gold/Stone/Wood/Food/Ore/Crystal/FireOil/SpecialFood/Meat，**无 Metal**。
- **ResourceAmount 结构体在代码中不存在**（仅设计稿接口签名里出现）。
- TaskScheduler 搬运链路：LoadInventoryFromSource/UnloadInventory/ResolveWarehouse（FindObjectsOfType<StorageComponent>）/ResolveTreasury（L590-701）。
- 2_9 sim：IWarehouse 仅设计文档（`2_9_..._实施计划.md` L326-335），D255 求 Unity 与 sim 共享同接口（同源对拍硬要求）。
- 影响面清单：A 核心（Storage/WorkerInv/Ruler/TaskScheduler）+ B 建造/升级/维修消耗（BuildController/BuildingPanel/BuildingMenuPanel/PlacementValidator/AcademyBuilding/KingdomManager/SiegeProduction）+ C 训练消耗（TrainingSystem）+ D 入/出（ProducerComponent/TradeSystem/Ranch/Satiety/Tax）+ E UI（WarehousePanel）+ F sim 对拍（需 ResourceAmount/Metal）+ G 新增类型（ResourceAmount/Metal/王国仓库集合）。

## 三、待决策事项（每项：选项 + 推荐 + 影响）

1. **步骤3 实施分批/裁剪程度**——全量"资源流动全走 IWarehouse + RulerController 迁移 + 多仓库凑单"（验收 L99）vs 最小可跑子集。
   - A（推荐）：**分批**。本卡先落**地基 + 核心路径**：新建 `ResourceAmount` + `IWarehouse` 接口，→ `StorageComponent`(王国仓库侧) / `WorkerInventory`(移动仓库) 实现接口，→ TaskScheduler 搬运/卸货与 ResolveWarehouse 走接口，→ 建造/训练消耗改多仓库凑单 Take。RulerController 金保留直通（货币不占存储），`ModifyResource` 暂作兼容壳。Metal/D200/枚举扩展留步骤8。
   - B：只建接口 + 两实现，不碰 TaskScheduler/RulerController/消费方（最小，多返工）。
   - C：一次性全量重构+枚举加 Metal/ResourceAmount+RulerController 全迁移（改动面最大，风险高）。

2. **`ResourceAmount` 结构 + Metal 资源的新增归属**——IWarehouse.Transform(石→Metal, D200) 签名引用 Metal，但枚举暂无。
   - A（推荐）：`ResourceAmount` 本卡新增（接口地基必需）；**Metal 不新增**，Transform 签名先不落地 Metal 分支（留步骤8 铁匠铺），避免改存档/对拍序列。
   - B：本卡一并加 Metal 到 ResourceType 末尾（影响存档兼容 + sim 对拍序列，需连带评估）。

3. **sim D255 同源对拍如何在步骤3 处理**——2_9 要求 Unity/sim 共享同 IWarehouse。
   - A（推荐）：本卡先在 Unity 侧建 IWarehouse/ResourceAmount，sim 侧同新增到 `ai决策大脑强化训练\harness`（黄金对拍），两侧精确对齐签名。
   - B：本卡只管 Unity 侧，sim 侧留 2_9 专项再对（有对拍间隙风险）。

## 四、下一步建议
- 策划裁决分批策略 + Metal/ResourceAmount 归属 + sim 同步方式后，执行端按序开工。
- 恢复入口：本 HH + 2_12 实施计划步骤3 + `河谷防线_开发计划书.md` 顶部工作日志。

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|


### 分歧裁决记录（有分歧时必填）
- 执行端意见：.. · 策划端意见：..
- 裁决：.. · 依据：..

### 衍生产物
- 新建清单/文档：{由策划端按裁决写入}