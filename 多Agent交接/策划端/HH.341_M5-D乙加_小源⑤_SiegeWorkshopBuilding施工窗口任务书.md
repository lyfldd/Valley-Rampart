# HH.341_M5-D乙加：小源⑤ `SiegeWorkshopBuilding` 施工窗口任务书

> **主策划签发｜2026-09-30｜D925**  
> 性质：**受限施工窗口**。HH.346 预检四项均取得，现准开最小协议接缝施工；本文件不是源级判绿。  
> 任务书号：**HH.347**（先登记后落盘）。完成报告号不得预留，由事务端按 `vr-id-ledger` 实时取号。

## 〇、裁定先行

1. **准开第 5 源施工窗口**，当前源只允许 `SiegeWorkshopBuilding.cs` 与必要的 `TaskScheduler.cs` 最小接缝。
2. 本窗口必须补齐组件侧 `D/A/B/E/C` 五类接缝，并统一 `HasWorkerOnDuty` 为“组件源或宿主 `Building` 源任一在岗即忙”；`Building.cs` 的 `Transport` 接缝仍不在本窗补。
3. 本源真实产弹仍以 `ProductionSystem → ITickable → Tick → Produce → store.Add` 为唯一生产证据；`Complete`、`Done`、`ExecuteCompletion` 的 `ProducerComponent` 空操作不得冒充产弹。
4. **R2（V19 子仓存档缺口）是源级判绿硬门**，但不纳入本窗允许面；R3-V17 是条件后续项，当前 `levels: []` 不阻断本源协议验收，但在启用非空等级前必须闭合。R2 须另立存档兼容性工单并完成验证后，才能判第 5 源绿及五小源收口。
5. 本窗施工完成后的状态只能写“施工完成·待进局、跨档、L-95、基线及事务核实”；不得写“源级通过”“判绿”。

## 一、派工必读

- `.codely-cli/skills/vr-role-brief/SKILL.md` §一、§二末“派工指派”；
- `.codely-cli/skills/vr-triage-flow/SKILL.md` §6.1、§七；
- `.codely-cli/skills/agent-handoff/SKILL.md` §三、§六；
- `.codely-cli/skills/vr-id-ledger/SKILL.md` §二、§三；
- `.codely-cli/skills/vr-planner-leadership/SKILL.md` §4.1、§4.2；
- `.codely-cli/skills/beyond-options/SKILL.md`；
- `HH.341_M5-D乙加_小源阶段任务书.md` §2.2、§四～§十一；
- `D922_HH.343_BlacksmithBuilding施工窗口准开裁决与执行工单.md`；
- `HH.346_SiegeWorkshopBuilding预检_独立核实报告.md`；
- 测试基线台账 §二百三十八～§二百四十一、`D923` 的 V4 与组件侧接缝先例。

本批相关教训：`L-88`（能力断言必须由生产链实测承载）、`L-90`（派工前整文件复扫缺口）、`L-95`（Play 三段）、`L-96`（追加/替换双验）、`L-97`（否定性统计五查）、`L-98`（两臂世界时长与暖机可比）、`L-100`（改动面由实际变更集合反推）。

## 二、允许面与禁止面

### 2.1 允许面

- `Valley Rampart/Assets/_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs`；
- `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs`，仅限本源组件侧 `D/A/B/E/C` 接缝与必要类型判定；
- 独立回退点、编译产物和 `Valley Rampart/Logs/hh341_small_siege_*` 证据前缀；
- 本源施工报告及事务端核实报告。

### 2.2 禁止面

- `Building.cs`、`BuildingFactory.cs`、`BuildingSaveData.cs`、`StorageComponent.cs`、`ProductionSystem.cs`、`TreasureVault.cs`、`SiegeProductionSystem.cs`；
- `ITaskScheduler.cs`、`TaskCardProtocol.cs`、`TaskLifecycleRules.cs`、`TaskBindingTypes.cs`、`TaskBindingManager.cs`、`TaskProtocolRuntime.cs`、`TaskProtocolIssuer.cs`；
- 场景、Prefab、`Assets/Resources/**/*.asset` 旧键、`AI.Core`、训练仓、其他四个小源、四本账本；
- `ExecuteCompletion` 专用生产副作用、`taskTimeout`、完成时长、`SourceKingdom`、重试政策和 `Building.currentWorkers`；
- `git add -A`、整目录覆盖、整文件回滚和 push。

发现 R2/V17 修复必须扩大到禁止面时，立即停手回呈；不得把兼容性修复偷偷并入本窗。

## 三、施工能力句与最小接缝

1. 执行端能证明本源每张 `Production` 卡在既有任务协议运行时中完成源对象配对、预定、派发、到达、完成/放弃和释放；本源接缝落在组件侧，`Building` 本体 `Transport` 不补。
2. 执行端能证明到岗只把卡推进到 `Working`，真实产弹只能在 `Working` 期间由 `ProductionSystem → Tick → Produce → store.Add` 观察；`Complete` 与 `Done` 不作为产弹证据。
3. 执行端能证明 `HasWorkerOnDuty` 同时覆盖 `HasWorkerAssigned(this)` 与宿主 `HasWorkerAssigned(_building)`，任一源在岗即阻止并行生产；报告须分列组件源与本体源身份。
4. 执行端能证明 `D/A/B/E/C` 五类本源接缝逐类可达，源失效封 `TargetRemoved`，单卡完成只释放该卡且源仍可再次广告。
5. 执行端能证明轮产、三仓容量、原料不足和 `ExecuteCompletion` 空操作边界均不被接缝改写；合法无产出须给全量取值域和原始→排除→有效三数。
6. 执行端能证明跨档后任务资格按当前 tick 重建、卡 ID 不复用，并单独列出三子仓库存是否仍可恢复；存档缺口未修复前，该能力句不得判闭。

## 四、R2 存档兼容性硬门（另案）

`Building.SaveState:1100-1144` 目前只收本体仓、国库、矿洞副产和工地仓；三枚 `Ammo_*` 子仓不入 `BuildingSaveData`，全库也未见 `GetComponentsInChildren<StorageComponent>`。因此 v2 新档往返会丢失三仓弹药，v1 旧档迁移桥不能替代持续存档。

另案必须设计并实测：三仓资源路径与存档字段的确定性映射、旧档兼容、容量 clamp、重复加载幂等、保存前后逐仓数量守恒、读档后再次装填与生产资格重建。另案未闭合前，第 5 源不得判绿；不得用 v1 迁移桥“已消费”证明 v2 存档成立。

## 五、R3-V17 等级容量条件后续项（另案）

`CreateSubStores` 在 `Init` 时按当时 `LevelScale()` 建仓，而 `SpawnFromSave` 在 `CreateBuildingInstance` 之后才恢复 `level`；当前 `SiegeWorkshop.asset` 的 `levels: []` 使缺口暂未显性，但高等级档会按 Lv1 容量建仓。另案必须让读档/升级后的三仓容量与当前等级一致，并用非空等级配置做正反例验证；当前 `levels: []` 不阻断本源协议验收，但在启用非空等级前必须闭合，不得以空资产永久关闭该项。

R3-V17 与 D923 的 `V13` 分开立项：V13 处理开局原料/国库可达性，V17 处理等级恢复后的子仓容量时序，根因、文件面和验收读数不同，不合并为一个结论。

## 六、预检差异的施工边界

- 三子仓：每仓独立 `capacity`、`IsFullFor`、`CanAccept`、装卸、清空与跨档复算；
- 轮产：`_cycleOrder` 推进与产出成功/失败分离，不能因补接缝改变轮产节奏；
- 工人：组件源与宿主源分别记 `source.GetType()`、`task.type`、卡 ID 和在岗状态；
- 旧档迁移：只登记 v1 一次性桥的消费/清零/clamp，不把它当 v2 持续存档；
- 经济域：`AbstractEconomySettlement` 跳过 `isSiegeWorkshop` 只作为边界事实，不推导产弹结论。

## 七、验证与交付

必须依次完成：独立回退点与开工基线 → 最小接缝 → 编译与 `git diff --check` → 正门真实 Play → 跨档 → `L-95` 三段 → 生产域/禁止面复算 → 事务端独立核实。真实 Play 必须记录 `seed`、档位、暖机、观测窗游戏秒/墙钟秒、共同零点和收工条件；不得用静态链替代运行时产弹证据。

报告必须保留 `D/A/B/E/C` 五类 `file:line`、组件/本体双源分列、三仓读数、`Complete ≠ 产弹` 证据、R2/V17 状态和五行集 `L-97` 标签。观察项从 `V20` 起，沿用 `hh341_small_siege_*`，不得覆盖 `V14`～`V19c` 证据。

### 7.1 完成报告取号硬门

完成报告号不得写进本任务书、不得预留。事务端须先在 `_编号登记.md` 登记新 HH 号，再以该号命名并落盘报告；若当前窗口禁止写账本，则在取号前停手回呈，不得以“水位线＋1”先命名后补登。

## 八、收口条件

施工报告只能给“施工完成·待核实”。事务端核实、R2 存档兼容性、真实 Play/跨档/L-95、基线和改动面均闭合后，主策划另立裁决判第 5 源是否绿；R3-V17 在启用非空等级前另案闭合。第 5 源判绿后，五小源阶段才收口；本任务书不授权第 6 源。

---

**签发结论：D925＝准开 HH.347 第 5 源受限施工；R1 补登记保留 HH.346；R2 另案且为源级判绿硬门，R3-V17 为启用非空等级前的条件后续项；V13 不合并。**
