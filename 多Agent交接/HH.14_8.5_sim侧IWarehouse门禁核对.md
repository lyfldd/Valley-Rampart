# HH.14 8.5 sim 侧 IWarehouse 门禁收尾核对（HH.8 握手）

> 类型：进度同步（8.5 核对完成，待策划知会确认）
> 状态：⏳待策划知会（2026-08-23 执行端）
> 日期：2026-08-23 · 发起端：执行端 · 关联清单/文档：2_12 实施计划步骤8（HH.12 范围末项）/ HH.8 / HH.13
> 关系：本核对独立于 HH.13（8.3 Metal 成本数据结构决策），不受其阻塞。

## 一、做了什么（执行端，带证据）

步骤8 收尾的 sim 侧 `IWarehouse` 门禁核对（HH.8 握手所立同源契约：两侧签名逐字对齐，单侧改签名须记 HH 回策划）。逐项比对了 Unity 运行时与 sim 两份接口的当前快照：

| 核对项 | Unity 侧 | sim 侧 | 结论 |
|--------|----------|--------|------|
| `ResourceType` 枚举 | `Assets/_Game/Core/GameEvents.cs`（Metal 末尾追加） | `harness/Core/IWarehouse.cs`（Metal 末尾追加，见其行7 注记） | ✅ 同值，均末尾追加、旧值稳定 |
| `ResourceAmount` struct | `Systems/Kingdom/IWarehouse.cs` | `harness/Core/IWarehouse.cs` | ✅ 逐字一致 |
| `IWarehouse` 五方法签名 `Query/CanTake/Take/Deposit/Transform` | 同上 | 同上 | ✅ 逐字一致 |

**关键澄清（Transform）**：步骤8 中 8.2 在 Unity 侧 `StorageComponent.Transform` **实现了** Metal 分支（石→Metal D200）——这是**接口实现**，非**接口签名**改动；`int Transform(ResourceType, ResourceType, int)` 签名两侧未变（均在 2_12 步骤3 已立）。故**无需记 HH 回策划**。

**sim 侧现状说明**：`harness/Core/` 目前仅含接口定义（`IWarehouse.cs`），无具体仓库实现——仓库流动/金属加工的同源对拍真值在 2_9 模拟器（SimEconomy2D）与步骤14 全同源审计处落地，步骤8 内部不涉及 sim 逻辑实现，Metal 分支真值留待彼时补。

## 二、现状与阻塞

- 8.5 核对**已通过**，无签名漂移、无接口变更，无阻塞。
- 剩余步骤8 链路：8.3 消费方（已挂 HH.13 待决策）→ 8.4 RulerController 退役三件套 → 8.5（本报告，✅完成）→ 最终 Play 石→Metal 闭环验证 + 编译 0 error + git-plan-sync 提交（含开发计划书 4 行工作日志）。

## 三、待决策事项

无（本报告为进度同步 + 知会）。唯请策划确认一点：
1. **Metal 加工同源真值挂 2_9/步骤14**（非步骤8）——即本步只在 Unity 侧实现、sim 侧接口对齐即可，不补 sim 行为实现。
   - A（推荐）：同意——Metal 分支 sim 真值随 2_9 经济环/步骤14 全同源审计一并落地。
   - B：需在步骤8 内补 sim 侧 Metal 加工行为（超步骤8 原范围，需评估）。

## 四、下一步建议

1. 取策划对 HH.13（决策1 数据结构，推荐 A）/决策2（数值占位）裁决 → 落地 8.3 消费方。
2. 其后按 HH.12 范围执行 8.4 RulerController 退役三件套 → 最终 Play + 编译 + git-plan-sync 提交。

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 8.5 核对结论（接口签名逐字对齐、无变更、无需记 HH） | （确认 / 需补） | .. |
| Metal 加工 sim 真值挂 2_9/步骤14（A） | （确认 / 需步骤8内补） | .. |

### 分歧裁决记录（有分歧时必填）
- 执行端意见：.. · 策划端意见：..
- 裁决：.. · 依据：AI 北极星 / 三支柱 / 单人规模 / 兼容风险

### 衍生产物
- 新建设计文档：..
- 新建清单任务：..