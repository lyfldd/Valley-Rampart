# `HH.340` `M5-C` 并存接线交付报告

- **性质**：施工批；只证明新协议**并存运行链成立**，不声称游戏任务系统已可用。
- **基线**：HEAD=`64774e4b313c4eaa97f25e624e2752934a2fa891`；M5-A=`ea6ad129`；M5-B=`9432e4fc`。
- **取号**：登记账本实读水位 `HH.338`，实存 `HH.339` 报告已存在；本报告拟用 `HH.340`，账本未改、未预留。
- **范围**：`06_任务层.md:154` 第①步「并存」。
- **行尾**：本批 9 个授权生产路径均为 LF、无 BOM；`TaskCardProtocol.cs` 现 564 行、28329 B。

## 一、改动面与锚点

| 路径 | 变更 | 实读锚点 | 行数/字节 |
|---|---|---|---:|
| `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskCardProtocol.cs` | 修改 | `TaskLifecycleStats:220`；`TaskCard():277`；`basePriority:286`；`retryMax:313`；`unreachableBlockedUntilTick:321`；`lifecycleStats:323`；状态统计写入 `:410-452,500-515,544` | 564 / 28329 |
| `Valley Rampart/Assets/_Game/Data/TaskProtocolPolicyConfig.cs` | 新增 | 枚举 `:4,10`；SO `:19`；字段 `:21-25` | 27 / 896 |
| `.../TaskProtocolPolicyConfig.cs.meta` | 新增 | Unity MonoImporter | 12 / 243 |
| `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskProtocolRuntime.cs` | 新增 | 类 `:10`；策略加载 `:42`；有效优先级 `:69`；提交 `:76`；王国桶 `:91-97`；冷却 `:108-116`；M5-B 编排 `:123-212` | 245 / 8854 |
| `.../TaskProtocolRuntime.cs.meta` | 新增 | Unity MonoImporter | 12 / 243 |
| `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskProtocolIssuer.cs` | 新增 | 类 `:7`；造卡 `:30`；提交 `:49`；Issue `:72` | 88 / 2780 |
| `.../TaskProtocolIssuer.cs.meta` | 新增 | Unity MonoImporter | 12 / 243 |
| `Valley Rampart/Assets/Resources/Config/TaskProtocolPolicyConfig.asset` | 新增 | `defaultRetryMax:1`、`tickIntervalSeconds:1`、`unreachableCooldownTicks:5`、`domain:0`、`statistics:0` | 20 / 517 |
| `.../TaskProtocolPolicyConfig.asset.meta` | 新增 | NativeFormatImporter | 9 / 189 |

### `TaskProtocolIssuer` 一句话设计

L3/L4 上层持有本批新增的 `TaskProtocolIssuer`；它写入 `TaskCard.basePriority`、从配置写入 `retryMax`，再把卡片交给新运行时，不持有旧 `TaskScheduler` 或地图对象。

## 二、权威 30 类型零接线扫描

机械枚举目录：

```text
Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/
```

机械枚举正则：

```text
^\s*(?:public\s+)?(?:(?:sealed|abstract|readonly|partial)\s+)*(?:class|struct|interface|enum)\s+[A-Za-z_]\w*
```

全量清单：

```text
TaskIssuerKind
TaskIssuerRef
TaskTargetKind
TaskTargetRef
TaskCountMode
TaskCountSpec
TaskPreconditionMode
TaskPrecondition
TaskProgressMode
TaskProgressSpec
TaskRewardSink
TaskRewardSpec
TaskEffectKind
TaskTargetEffect
TaskFailPolicy
TaskAbortReason
TaskUnassignReason
TaskSuspendReason
TaskCard
ITaskRestoreValidator
TaskLifecycleState
TaskTransitionError
TaskLifecycleRules
TaskBindingResult
TaskPairStatus
TaskTargetKey
TaskReservation
TaskPairRecord
TaskSweepReport
TaskBindingManager
```

排除清单：

```text
TaskCardProtocol.cs
TaskLifecycleRules.cs
TaskBindingTypes.cs
TaskBindingManager.cs
TaskProtocolPolicyConfig.cs
TaskProtocolRuntime.cs
TaskProtocolIssuer.cs
```

扫描作用域：`Valley Rampart/Assets/_Game/**/*.cs`，排除上述 7 个文件；结果：**312 个文件扫描，0 命中，0 文件命中**。

`TaskTargetKey` 实存确认：`TaskBindingTypes.cs:48`。

## 三、验收判据与复现读数

### 3.1 Unity MCP / 配置

CoplayDev `manage_asset` 对 ScriptableObject 返回不支持创建；随后使用同一 CoplayDev MCP 的 `execute_code`（CodeDom；Roslyn 不可用）创建并验证资产。

资产探针原文：

```text
assetPath=Assets/Resources/Config/TaskProtocolPolicyConfig.asset assetExists=True resourcesLoaded=True exact=True values={defaultRetryMax:1,tickIntervalSeconds:1,unreachableCooldownTicks:5,domain:Kingdom,statistics:PerTask}
```

结论：`Resources.Load<TaskProtocolPolicyConfig>("Config/TaskProtocolPolicyConfig")` 成功。

### 3.2 并存运行链探针

临时探针：`Assets/Editor/Smoke/M5C_TaskProtocolProbe.cs`。  
去向：已删除 `.cs` 与 `.meta`，不进入生产链、不提交。

探针覆盖：

- `TaskProtocolIssuer` 提交 `TaskCard`
- `basePriority` 写入与 `GetEffectivePriority` 只读
- `retryMax=1`、第一次重试成功、第二次返回 `RetryExhausted`
- 王国分桶隔离
- M5-B `Reserve`／`Pair`／终态释放
- `Unreachable` 冷却边界
- 两张卡的 `lifecycleStats` 独立

复现输出原文：

```text
ASSERTIONS total=10 executed=10 failed=0
```

断言汇总：**总数 10／执行数 10／失败数 0**。

### 3.3 编译与控制台

CoplayDev `refresh_unity(mode=if_dirty, scope=all, compile=request, wait_for_ready=true)` 原文：

```text
{"success":true,"message":"Refresh recovered after Unity disconnect/retry; editor is ready.","error":null,"data":{"recovered_from_disconnect":true},"hint":null}
```

随后 `read_console(types=["error"])` 原文：

```text
{"success":true,"message":"Retrieved 1 log entries.","data":["NullReferenceException: Object reference not set to an instance of an object"]}
```

判读：

- Unity 刷新完成，`is_compiling=false`，未见本批 C# 编译诊断。
- Console 仍有 1 条未归因的既有 `NullReferenceException`；本批不将其伪报为 `errors=0`，也未发现其指向本批文件。
- 本批未运行进局测试；不据此宣称游戏能力可用。

### 3.4 旧链基线前后对照

旧文件未改；历史脚本复核得到任务书基线：

| 项 | 现读 | 任务书基线 | 判定 |
|---|---:|---:|---|
| `TaskScheduler.Instance|TaskScheduler.HasInstance` | 54 行 / 15 文件 | 54 / 15 | 相同 |
| `new KingdomTask(`（剔除整行注释） | 14 行 / 9 文件 | 14 / 9 | 相同 |
| `ITaskSource` 实现者 | 9 / 9 文件 | 9 | 相同 |
| `KingdomTaskType` 枚举项 | 11 | 11 | 相同 |

补充：此前 `rg` 包装输出的 52/15、13/10 是输出记录合并造成的下采样；用既有 `Select-String` 基线脚本复核后恢复为 54/15、14/9。旧调用面文件相对 HEAD 无改动。

### 3.5 AI.Core 与地图红线

- `AI.Core`：35 个文件扫描，0 个 M5-C 类型命中。
- `TerritorySystem._territory`：0 个源文件命中。
- 旧 `TaskScheduler`、`ITaskScheduler`、`KingdomTask`、`WorkerTask`、`ITaskSource`、`KingdomTaskType` 未修改。
- `TaskBindingManager.cs` 未修改，仅由新运行时调用既有释放口。

## 四、机械 Git 证据

`git diff --name-only`（跟踪文件）：

```text
Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskCardProtocol.cs
```

授权未跟踪文件为本报告 §一列出的其余 8 个生产路径；无探针残留。

`git diff --numstat`：

```text
75  9  Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskCardProtocol.cs
```

新增文件逐文件行数见 §一；`git diff --check`：**exit=0**。

## 五、停手核对

| 停手条件 | 结果 |
|---|---|
| 需要修改旧 `TaskScheduler` | 未命中 |
| 需要触碰 `AI.Core` | 未命中 |
| 旧链基线下降或调用面改变 | 未命中；旧文件无 diff |
| 新协议在旧生产链非零命中 | 未命中；30 类型扫描 0 |
| 配置无法 `Resources.Load` | 未命中；`resourcesLoaded=True` |
| `EffectivePriority` 回写 `basePriority` | 未命中；探针调用前后基值相同 |
| 需要新未裁政策 | 未命中；只落任务书五项定值 |
| 需要修改 `06`、账本、场景或旧资产键 | 未命中 |
| 本批新增编译错误 | 未发现；Unity 刷新成功，Console 另有 1 条未归因 NRE |

## 六、列报与未完成项

1. `最高优先级文档/06_任务层.md:59` 标题「五组」与表内 7 行不一致；本批未改。
2. `HH.339` 中「M5-A 两文件 blob 与 HEAD 逐位相同（`e4613adfab6e`／`ae7aee83643a`）」自本批起失效。
3. 旧生产链仍未切换、未清理；旧广告源和旧枚举仍在。
4. 未运行 Play Mode/进局测试；本批只证明编辑器侧并存运行链探针。
5. Console 1 条 `NullReferenceException` 未在本批归因；不把它冒充本批编译错误，也不宣称 Console 全清。

## 七、应登记项

```text
HH.340 | M5-C 并存接线交付报告 | 执行端 | 交付报告已落盘；账本不由执行端改写
改动：TaskCardProtocol.cs（75+/9-）＋8 个新增生产路径；
探针：M5C_TaskProtocolProbe.cs/.meta 已删除，不入提交；
验证：配置 Resources.Load 成功；断言总数10/执行10/失败0；30 类型旧链扫描0；
旧链：TaskScheduler.Instance|HasInstance 54/15，new KingdomTask 14/9，ITaskSource 9，KingdomTaskType 11；
编译：Unity refresh 成功，未见本批编译诊断；Console 另有1条未归因 NRE；
范围：未改 AI.Core、旧调度链、TaskBindingManager 既有契约、06、05、账本、队列、场景；未 push。
```

## 八、复现入口

```text
CoplayDev MCP:
refresh_unity(mode=if_dirty, scope=all, compile=request, wait_for_ready=true)
read_console(action=get, types=["error"], count="100", format="plain")
execute_code(action=execute, compiler=codedom)
```

