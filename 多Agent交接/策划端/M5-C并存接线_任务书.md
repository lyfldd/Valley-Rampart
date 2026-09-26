# `M5-C` 施工任务书 · 新任务协议并存接线

> ⚠️ **事务端派工附注（2026-09-25 · 砚）** —— 以下是事务端核实结论，⛔ **不改任务书正文**；开工前必读，与正文冲突时**以本附注为准**。
>
> **1. 【扫描名单不全 · 权威清单 30 个】** 正文 §六.2 的名单只列 **15 个**；`Assets/_Game/Systems/AI/TaskScheduling/` **机械枚举实得新协议类型 30 个**。
>
> ⚠️ **本附注前一版写"29 个"并称 `TaskTargetKey` 未找到 —— 那是事务端的扫描缺陷（正则漏了 `readonly` 修饰符）**。执行端回执给出 `TaskBindingTypes.cs:48 public readonly struct TaskTargetKey` ⇒ **确认实存**，事务端认账。
> ⚠️ 执行端回执的 29 个清单同样**漏 1 个**：`TaskLifecycleRules`（`TaskLifecycleRules.cs:45`）。
>
> ⇒ **权威清单 ＝ 30 个**：`TaskIssuerKind`／`TaskIssuerRef`／`TaskTargetKind`／`TaskTargetRef`／`TaskCountMode`／`TaskCountSpec`／`TaskPreconditionMode`／`TaskPrecondition`／`TaskProgressMode`／`TaskProgressSpec`／`TaskRewardSink`／`TaskRewardSpec`／`TaskEffectKind`／`TaskTargetEffect`／`TaskFailPolicy`／`TaskAbortReason`／`TaskUnassignReason`／`TaskSuspendReason`／`TaskCard`／`ITaskRestoreValidator`／`TaskLifecycleState`／`TaskTransitionError`／`TaskLifecycleRules`／`TaskBindingResult`／`TaskPairStatus`／`TaskTargetKey`／`TaskReservation`／`TaskPairRecord`／`TaskSweepReport`／`TaskBindingManager`
> （⛔ 排除旧面 4 个：`ITaskScheduler`／`TaskScheduler`／`GatherTaskArgs`／`ScaleTaskArgs`）
>
> ⇒ 判据 2 的"0 命中"**以此 30 个为准**；开工报告须附**机械枚举输出**（目录 ＋ 正则 ＋ 全量清单 ＋ 排除清单），⛔ 不得手列，⛔ 不接受"至少包含"。
>
> **2. 【文档勘正列报】** `最高优先级文档/06_任务层.md:59` 标题写「任务卡片（**五组**）」，但表内实为 **7 行**（身份／归属方／做什么／条件与进度／结果／执行中／意外）⇒ 陈标题。⛔ 本批**不改** `06`（红线 §四.8），但须在交付报告里**列报**。
>
> **3. 【读数失效声明 · 必写】** 本批将改动 `TaskCardProtocol.cs`（M5-A 载体）⇒ `HH.339` 报告中的「**M5-A 两文件 blob 与 HEAD 逐位相同**（`e4613adfab6e`／`ae7aee83643a`）」**自本批起失效**。开工报告须**显式声明该读数作废**，⛔ 否则下游拿旧读数会对出**假退化**。
>
> **4. 【开工回执必含 · 补】** 回执缺一不予放行：① §五 五条政策的**原文复述**；② 允许改动范围的**最终文件清单**（含新增 `.asset` 与 `.meta`）；③ 造卡入口（`TaskProtocolIssuer` 从哪来、谁持有）的**一句话设计说明**。
>
> **5. 【探针授权 · 沿用惯例】** 判据 §六.1「新入口可以提交一张 `TaskCard`」需构造入口，而 §三 授权面未列探针 ⇒ 按项目惯例（`HH.330`：改动面含「1 探针 · 测试件已删」）**允许新增 Editor 探针 1 个**，落 `Assets/Editor/Smoke/`；交付报告须列明其**去向**（删除 ／ 保留但不入提交）。⛔ 探针不得进生产链、不得作为生产码入库。
>
> **6. 【Unity MCP · 正向指令】** Unity 侧操作**照 skill `unity-mcp-first` 走**：默认用 **`mcp_unityMCP`（CoplayDev）**——编译 `refresh_unity(compile=request)`、读控制台 `read_console(types=["error"])`、资产 CRUD `manage_asset`；`mcp_unity-bridge`（Codely）用于 `execute_csharp_script` 等其独有能力及**故障切换兜底**。⛔ 本任务书**不作 MCP 可用性预设**（负向预设＝自我实现预言，见该 skill §「MCP 疑似失效处置」）。

---

**状态**：待执行
**范围**：仅执行 `06_任务层.md:154` 第①步「并存」
**本轮不改仓库、不取号、不预留 HH 号。交付报告号由执行端按实时水位取号。**

---

## 〇、基准事实

- [事实] 当前 HEAD 为 `64774e4b`；`M5-A` 为 `ea6ad129`，`M5-B` 为 `9432e4fc`。
- [事实] `06_任务层.md:154` 原文为：
  `迁移路径：① 并存（新协议跑新东西，老源不动）→ ② 切换（老源逐个改造成能力声明）→ ③ 清理（删枚举、删老源）`

## 一、契约依据与冲突处理

- [事实] `最高优先级文档/06_任务层.md:31`：`上层（L3/L4）：委托任务、撤回任务、给优先级`
- [事实] `最高优先级文档/06_任务层.md:63`：`taskId ｜ issuer（谁派的）｜ priority`
- [事实] `最高优先级文档/06_任务层.md:69`：`retry ｜ onFail（换人／放弃／降级）`
- [事实] `最高优先级文档/06_任务层.md:140-142`：`分域：内部按大区块（或王国）分桶` ／ `节流：沿用现有 tickInterval = 1s` ／ `不要多个管理器`
- [事实] `最高优先级文档/06_任务层.md:173-176` 仍列四项待议：优先级归属、retry 与不可达冷却、分域粒度、统计粒度。
- [裁定] 妹妹最新拍板**覆盖**上述待议项；执行端**不得自行重新解释，也不得修改设计文档**。
- [事实] "修复(S) > 建造(A) …"的来源是 `河谷防线开发计划书具体内容/3.5_王国经营体系.md:731`，**不是** `06` 的"§3.5"。本批只落优先级归属，**不迁移旧映射表**。

## 二、五项政策落值表

> ⚠️ 新文件的行号属于**施工目标锚**，不是当前仓库事实；落盘后必须**回读真实行号**写进交付报告。

| 项 | 对应 `06 §十` | 定值 | 当前实读锚点 | `M5-C` 施工落点 |
|---|---|---|---|---|
| priority 归属 | `-2` | **C**：上层给 `basePriority`，调度器算 `EffectivePriority` | `TaskCardProtocol.cs:231` 当前为 priority 占位；旧链计算在 `TaskScheduler.cs:1308`、`:1339` | 将 `TaskCard.priority` 改为 `basePriority`。新增 `TaskProtocolIssuer`，由其写入 `TaskCard.basePriority`。新增 `TaskProtocolRuntime.GetEffectivePriority(TaskCard)`，**只读派生，不回写** `basePriority`。 |
| retry 默认次数 | `-3` | **B**：`retryMax = 1`，总尝试 2 次 | `TaskCardProtocol.cs:258` 当前默认 `-1`；拒绝未配置在 `TaskLifecycleRules.cs:36` | 新增 `TaskProtocolPolicyConfig.defaultRetryMax = 1`；新卡创建时写入 `TaskCard.retryMax`。`TryConsumeRetry()` 的终止边界保留。 |
| 不可达冷却 | `-3` | **B**：5 秒 ＝ 5 × `tickInterval` | `TaskCardProtocol.cs:265` 当前只有 `unreachableStreak`；清除口在 `:448` | 配置 `tickIntervalSeconds = 1f`、`unreachableCooldownTicks = 5`。在卡片生命周期辅助栏新增 `unreachableBlockedUntilTick`，由新运行时控制重新可广告时机。 |
| 分域粒度 | `-4` | **A**：王国级 | `TaskCardProtocol.cs:234` 已有 `kingdomId`；旧链同国闸在 `TaskScheduler.cs:318`、`:331` | 新运行时只按 `kingdomId` 分桶；**不新增 `chunkId`，不接 `TerritorySystem._territory`**。明确偏离 `06:140` 的"大区块"主句，理由是零迁移、单管理器、当前 AI 规模。 |
| 统计聚合粒度 | `-5` | **B**：按任务实例汇总生命周期计数 | `TaskBindingManager.cs:60` 明确现有表规模口不是统计口径 | 在 `TaskCardProtocol.cs:266` 附近新增 `TaskLifecycleStats lifecycleStats`，统计挂在**单张卡片**上；⛔ 禁止新增全局汇总表作为本批主统计口径。 |

### 新增配置载体

[执行要求] 新增：

- `Valley Rampart/Assets/_Game/Data/TaskProtocolPolicyConfig.cs`
- `Valley Rampart/Assets/Resources/Config/TaskProtocolPolicyConfig.asset`
- 通过 `Resources.Load<TaskProtocolPolicyConfig>("Config/TaskProtocolPolicyConfig")` 读取。
- ⛔ **不使用 Inspector 拖引用**。
- 若配置加载失败，**立即停手**。

配置字段必须至少包含：

```
defaultRetryMax = 1
tickIntervalSeconds = 1
unreachableCooldownTicks = 5
domain = Kingdom
statistics = PerTask
```

## 三、允许改动范围

**允许**：

- 现有新协议文件：`...\TaskCardProtocol.cs`
- 新增：`...\TaskProtocolPolicyConfig.cs`、`...\TaskProtocolRuntime.cs`、`...\TaskProtocolIssuer.cs`、对应 `.meta`、`TaskProtocolPolicyConfig.asset` 及 `.meta`

[执行要求] `TaskBindingManager.cs` **只允许被调用，不得改动其既有释放契约**。

## 四、红线

1. `AI.Core` **零触碰**：代码、注释、调用面、资产均不得新增或修改。
2. **只做并存**；不做切换，不做清理。
3. **不删**旧枚举、旧广告源、旧接口。
4. **不动**：`KingdomTask`／`WorkerTask`／`ITaskSource`／`ITaskScheduler`／`KingdomTaskType`／旧 `TaskScheduler`。
5. 不改旧 `TaskScheduler.cs` 的 `Instance|HasInstance` 调用面。
6. 不把 `EffectivePriority` 写回 `basePriority`。
7. 不把 `TaskBindingManager` 的表规模读数冒充统计聚合。
8. 不编辑策划账本、`06`、`05` 或其他契约文档。
9. 不声称"游戏任务系统已可用"；本批只证明**新协议并存运行链成立**。

## 五、开工前逐条复述

执行端开工报告必须**原文复述**：

1. **priority**：上层 `TaskProtocolIssuer` 写 `TaskCard.basePriority`；`TaskProtocolRuntime` 只读计算 `EffectivePriority`；**禁止回写**。
2. **retry**：`retryMax = 1`，总尝试次数为初次 1 次加重试 1 次。
3. **不可达冷却**：`5s = 5 × tickInterval`，当前 `tickInterval = 1s`。
4. **分域**：王国级；明示偏离 `06:140` 的大区块主句；**不接 chunk**。
5. **统计**：按单个 `TaskCard` 汇总生命周期计数，**不建全局主统计表**。

## 六、验收判据

### 1. 新协议能力

- 新入口可以提交一张 `TaskCard`。
- `basePriority` 能被上层写入。
- 运行时能读出 `EffectivePriority`，且**调用前后 `basePriority` 不变**。
- 新卡能从配置得到 `retryMax = 1`。
- 初次尝试成功，第一次重试可消费，第二次重试返回 `RetryExhausted`。
- `Unreachable` 后连续 5 个 tick 不重新进入可派发集合，第 5 个 tick 后恢复资格。
- 两个不同 `kingdomId` 的任务统计、分域和生命周期**互不混淆**。
- 两张卡的 `lifecycleStats` **独立计数**。
- `TerritorySystem._territory` 保持**未接线**。

### 2. 旧链基线不得退化

以下读数必须与开工前**完全一致**：

- `TaskScheduler.Instance|HasInstance`：**54 行 / 15 文件**
- `new KingdomTask(`：**14 行 / 9 文件**
- `ITaskSource`：**9 类实现者**
- `KingdomTaskType`：**11 项枚举**
- 新协议类型在旧生产链中：**0 命中**

新协议零接线扫描名单**至少包含**：
`TaskCard`／`TaskLifecycleState`／`TaskLifecycleRules`／`TaskTransitionError`／`TaskTargetRef`／`TaskAbortReason`／`TaskUnassignReason`／`TaskSuspendReason`／`TaskBindingManager`／`TaskBindingResult`／`TaskReservation`／`TaskPairRecord`／`TaskTargetKey`／`TaskSweepReport`／`ITaskRestoreValidator`

⚠️ 扫描时必须**排除新协议自身文件和本批新增运行时文件**，并给出**排除清单**。

### 3. 机械验证

交付报告必须附：

- `git diff --name-only`
- `git diff --check`
- 编译 `errors=0`
- 配置 `Resources.Load` 成功读数
- 五项政策逐项探针
- 旧链基线前后对照
- 新协议旧链命中扫描
- `AI.Core` 扫描结果
- **断言总数、执行数、失败数**，⛔ 不能只报"失败 0"

## 七、停手条件

命中任一条**立即停手**，写 `HH` 报裁：

- 需要修改旧 `TaskScheduler` 才能接入。
- 需要触碰 `AI.Core`。
- 旧链任一基线读数**下降或调用面改变**。
- 新协议类型在旧链出现**非零命中**。
- `TaskProtocolPolicyConfig` 无法通过 `Resources.Load` 读取。
- 发现 `basePriority` 被调度器**回写**。
- 发现冷却需要**新的未裁政策**。
- 发现必须迁移旧 `ITaskSource`、旧枚举或 `KingdomTask` 才能继续。
- 需要修改 `06`、账本、资产旧键或场景。
- 编译出现**本批新增错误**。
- 需要用进局实测才能证明的"游戏能力"被要求在本批结论中宣称。

---

[裁定] `M5-C` **现在可以开工**，但只释放以上**并存**范围；②切换与③清理**继续锁死**。
