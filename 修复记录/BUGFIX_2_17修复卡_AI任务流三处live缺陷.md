# BUGFIX：2_17 修复卡 AI 任务流三处 live 缺陷（α/β/γ）

> 产生背景：2_17「AI 王国脑与自主成长」步骤3 落地池隔离路由时暴露的三处 live 缺陷，修复后以行为级冒烟作证。

## 现象

- **α（自然采集死）**：自然建筑（`kingdomId=-1`，如 ore_vein）正常发布采集任务（`TryAdvertiseTask` 返回 Gather），但 `TaskScheduler.Tick` 路由按 `idleKingdom != tKingdom` 过滤时，`SourceKingdom` 返回 -1，玩家（0）工人永远无法与它匹配 → 玩家自然矿采集被打死。
- **β（AI 任务发布被守卫误伤）**：`Building.TryAdvertiseTask`（L900）与 `RegisterWithTaskScheduler`（L968）里的任务守卫会阻止 AI 生产建筑（`kingdomId>0`）发布/注册任务 → AI 王国无法自主生产。
- **γ（凑单/卸货跨国）**：`WarehouseRegistry` 凑单与卸货目标不加国过滤，同一场景玩家/AI 仓库并存时，任务可能把货卸进错误王国的仓库（威胁"玩家资源绝不流入 AI 库"红线）。

## 根因

- 池隔离路由只做了"工人归属国 == 源王国"的硬匹配，未为无主源（-1）留入口；SourceKingdom 对无主建筑返回 -1（哨兵），被当作普通王国 id 参与等值比较。
- 补丁 D"AI 任务不流向玩家"原本用广告/注册侧守卫表达，但守卫实现口径过宽，把 AI 自身的任务发布也一并拦截；实际该语义已由池隔离结构性达成。
- 凑单/卸货的仓库选择未按王国过滤。

## 修复点（文件 + 方法）

- **α**：`Assets\_Game\Systems\AI\TaskScheduling\TaskScheduler.cs` — `Tick()` 路由加先到先得池特例：

```csharp
// -1 无主源 = 先到先得池，任何国空闲工人可匹配
if (tKingdom >= 0 && idleKingdom[i] != tKingdom) continue;
```

即负王国 id 不参与等值过滤，放行任意国空闲工人。

- **β**：`Assets\_Game\Systems\Building\Building.cs` — 删除 `TryAdvertiseTask` L900 广告守卫 + `RegisterWithTaskScheduler` L968 注册侧守卫（AI 生产照常发布）。

- **γ**：`Assets\_Game\Systems\Kingdom\WarehouseRegistry.cs` — `GatherActive(int kingdomId)`、`FindNearestAvailable(type, worldPos, kingdomId)` 目标过滤改**同王国匹配**（第 3 参 = 工人归属国）；`WarehouseHelper.GatherWarehouses()` 同步走 `GatherActive(0)`。`TaskScheduler.UnloadInventory` 用 `wkingdom`（工人归属国）传参。

## 验证方式

新增 `Assets\Editor\Smoke\Valley2_17_Smoke_FixCard.cs`（菜单「Valley/验证/2_17_修复卡_行为级冒烟」，Play 上下文）行为级真派真产出五项：

- β：AI 生产建筑产出 tick（`storage.storedAmount` 增）；
- 零污染：玩家工人紧贴 AI 源仍 `TaskState.None`（池隔离生效，未越过红线）；
- γ：AI 库满仓 → Transport → AI 工人卸回 AI 仓库；
- α：自然矿（-1）`isBeingGathered` → 真派到玩家工人且玩家工人 Working；
- α：玩家工人真采到（Working）。

确定性"同一 seed 两轮一致"经实证在带活世界自动模拟下不达严格可复现，记为 harness 对活世界保真度上限、不改保证。

> ⚠️ 流程教训：**路由类改动当步必须带至少一条行为级正/负探针**。