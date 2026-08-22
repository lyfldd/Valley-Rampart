# HH.8 2_12 步骤3 仓库统一抽象 IWarehouse 范围待策划裁决

> 类型：待决策（重构序范围）
> 状态：✅已裁决（2026-08-22 策划端，见文末回写）
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
| 1. 分批策略 | **A（地基+核心路径），附次序修正+红线**。⑴**兼容壳带退役时间表**：ModifyResource 壳加 `TODO(2_12步骤N)：随X迁移退役` 标注 + 步骤3 验收行注记"金路径=临时壳"——壳每多活一个阶段，迁移面多一圈调用方；⑵**红线：壳禁止双写**——壳只做转发到 IWarehouse，真源单一，新旧两套账并行记账=资源复制 bug 以最隐蔽方式出现；⑶验收缩围注记："RulerController 迁移归步骤8 完成，本步验收=IWarehouse 地基+搬运/卸货/凑单走接口" | Ruler 全迁移+Metal 是步骤8 铁匠铺配套，提前做=踩存档/对拍地雷；壳无退役表=永久债 |
| 2. ResourceAmount/Metal 归属 | **A：ResourceAmount 本卡新增（接口地基必需）；Metal 不新增**，Transform 签名按设计稿落地但 Metal 分支留 TODO(步骤8)——签名先立住，sim 同步时接口形状不漂移 | Metal 连 D200 Transform 加工链（铁匠铺），现在加=空悬无生产无消费的类型 + 污染存档序列与对拍 |
| 3. sim D255 同步 | **A：双侧同卡对齐，附项目纪律**。策划核实：harness 侧零 IWarehouse 痕迹、2_9 侧仅文档——双侧均新增，无"对齐旧物"，本卡一次立双侧是最便宜时刻。纪律：⑴sim 侧改动走训练仓自身门禁（改 harness 须先 commit→改→双门禁→过则 commit，CODELY.md 训练边界铁律）；⑵**接口文件放 harness/Core（决策核同源区）而非 Sim/**——D255 要的是决策核可见同一抽象，非模拟器私有；⑶Unity `Systems/Kingdom/IWarehouse.cs` 与 sim 接口**签名逐字对齐**，后续任何单侧改签名必须记 HH 回策划（同源契约变更=双方裁决） | B 留对拍间隙=给北极星主线埋已知债 |

### 分歧裁决记录（有分歧时必填）
- 执行端意见：三项均推荐 A（分批/ResourceAmount-only/双侧同步）。
- 策划端意见：三项均选 A（给定选项已最优，无需自造），各附补强：壳退役时间表+禁双写红线（决策1）、Transform 签名立住防漂移（决策2）、接口入 Core 区+签名逐字对齐+单侧改签名须 HH（决策3）。
- 裁决：A×3 + 补强 · 依据：AI 北极星（D255 同源=决策核可见；真源单一）/ 单人 1 月规模（壳退役表防债滚雪球）/ 兼容风险（Metal 提前入枚举踩存档序列）

### 衍生产物
- 新建清单/文档：无新增（步骤3 验收行按裁决补三条注记：金路径临时壳 / RulerController 归步骤8 / Ruler 迁移验收缩围）