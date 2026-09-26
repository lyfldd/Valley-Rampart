# `HH.340` `M5-C` 补证交付报告

- **性质**：对 `deee1b32` 的补证。不取新号。不改生产码。本报告不覆盖 `HH.340_M5-C并存接线_交付报告.md`。
- **本批不判绿、不销号**，不放行依赖 `HH.340` 绿灯的迁移结论。
- **时刻**：补证收工 Console 打点 `2026-09-26 10:26:24.424 +08:00`（Unity `execute_code` 返回）。

## 〇、开工回执

**范围**：只补 `HH.340`（`deee1b32a587dd97355074c3381df8e523e29588`）的证据。契约仍以 `最高优先级文档/06_任务层.md:154` 第①步「并存」为界；②切换、③清理不在本批。

**红线**：不改生产码；不改 `_编号登记.md`／`_任务队列.md`／`_当前快照.md`／`测试基线台账.md`；不写「errors=0」；不销号；探针若重跑先归档；若 NRE 判为本批引入则停手报裁、不在本批修。

**五项政策原文（任务书 §五，本批只复核读数，不改落点）**：

1. **priority**：上层 `TaskProtocolIssuer` 写 `TaskCard.basePriority`；`TaskProtocolRuntime` 只读计算 `EffectivePriority`；**禁止回写**。
2. **retry**：`retryMax = 1`，总尝试次数为初次 1 次加重试 1 次。
3. **不可达冷却**：`5s = 5 × tickInterval`，当前 `tickInterval = 1s`。
4. **分域**：王国级；明示偏离 `06:140` 的大区块主句；**不接 chunk**。
5. **统计**：按单个 `TaskCard` 汇总生命周期计数，**不建全局主统计表**。

**最终允许改动**：

| 路径 | 处置 |
|---|---|
| `多Agent交接/执行端/HH.340_M5-C_补证_交付报告.md` | 新增，本批唯一入库文件 |
| `Logs/_archive_20260926_102624/` | 本轮探针原文副本。上一跑无探针文件可搬。不入库 |
| `Assets/**` 生产码与 `TaskProtocolPolicyConfig.asset` | 零改动 |

**拟用号**：不取新号。沿用 `HH.340`。

**本环境 MCP（开工时实查）**：

- 已注册命名空间 `user-unityMCP`。本批实际调用：`read_console`、`execute_code`。schema 已读、本批未调用：`refresh_unity`（无脚本改动，未请求编译）、`manage_asset`（禁止改已建成资产）。
- `execute_code.compiler` 实取值：`roslyn` 失败；`auto` 回落为 `codedom`；`codedom` 可执行。
- `GetDynamicTools` 模式 `unity-bridge|execute_csharp|unity_console`：**命中 0**。本会话没有 Codely `execute_csharp_script`。

## 一、阻断项：NullReferenceException

### 1.1 原报告那条单句的完整堆栈

原交付时 `read_console` 只有：

```text
NullReferenceException: Object reference not set to an instance of an object
```

该编辑器会话的日志是 `Editor-prev.log`（会话起始行：`[Licensing::IpcConnector] Successfully connected to the License Client on channel: "LicenseClient-trs" at "2026-09-25T00:52:39.4567141Z"`）。按 `\n` 分行，这条异常出现 **7** 次，**7** 次的下一行都是同一帧。首见整行：

```text
NullReferenceException: Object reference not set to an instance of an object
  at UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadManager.OnBeforeSerialize () [0x00001] in <ffb70ee66fe4403f8ccd5607b2cfd687>:0 
```

位置：`%LOCALAPPDATA%\Tuanjie\Editor\Editor-prev.log:56703` 与 `:56704`。其后没有第二帧。`Symbol file LoadedFromMemory is not a mono symbol file` 紧跟在空行之后。7 次里，含 `Assets/` 或 `TaskProtocol` 的帧数 = **0**。

### 1.2 前后对照

换算：会话起始 ISO `2026-09-25T00:52:39.4567141Z` 对应 licensing 戳 `1790297559`。每条 NRE 取其后 15 行内第一个 `at <十位戳>`，加上秒差。`M5-C` 文件首次导入行 = `Editor-prev.log:67362`（`Start importing Assets/Editor/Smoke/M5C_TaskProtocolProbe.cs ...`）。提交 `deee1b32` 的 `CommitDate` = `Sat Sep 26 00:27:32 2026 +0800`。

| # | 日志行 | UTC | +0800 | 相对 `M5-C` 导入行 67362 |
|---:|---:|---|---|---|
| 1 | 56703 | 2026-09-25T12:51:31Z | 2026-09-25 20:51:31 | 之前 |
| 2 | 61292 | 2026-09-25T14:57:15Z | 2026-09-25 22:57:15 | 之前 |
| 3 | 64450 | 2026-09-25T15:46:04Z | 2026-09-25 23:46:04 | 之前 |
| 4 | 67357 | 2026-09-25T15:53:52Z | 2026-09-25 23:53:52 | 导入行之前 5 行，同一次域重载 |
| 5 | 70285 | 2026-09-25T15:58:11Z | 2026-09-25 23:58:11 | 之后 |
| 6 | 73056 | 2026-09-25T15:58:58Z | 2026-09-25 23:58:58 | 之后 |
| 7 | 76578 | 2026-09-25T16:02:22Z | 2026-09-26 00:02:22 | 之后，仍早于提交 00:27:32 |

当前编辑器会话是另一份日志：`Editor.log` 第 2 行 `at "2026-09-26T01:48:16.3799375Z"`。该文件全文 `NullReferenceException` 命中数 = **0**（文件字节 447083，扫描时点为本补证会话）。

补证本轮现场 Console（`read_console`，`types=["error"]`，`count="100"`，`include_stacktrace=true`）：

开工前（探针之前；这次返回没有 Unity 时钟字段。编辑器会话已在 `09:48:16 +0800` 启动）：

`format=plain` 原文：

```text
{"success":true,"message":"Retrieved 1 log entries.","data":["287 node options failed to load and were skipped."]}
```

`format=detailed` 的类型、消息、文件、行（`stackTrace` 后文是被跳过的节点清单，MCP 在 `Sy` 处截断，此处不整段粘贴）：

```text
{"success":true,"message":"Retrieved 1 log entries.","data":[{"type":"Error","message":"287 node options failed to load and were skipped.","file":"./Library/PackageCache/com.unity.visualscripting@1.9.4/Editor/VisualScripting.Flow/Options/UnitBase.cs","line":131}]}
```

同一参数再滤 `filter_text=NullReferenceException`、`format=json`：

```text
{"success":true,"message":"Retrieved 0 log entries.","data":[]}
```

收工后，`2026-09-26 10:26:24.424 +08:00`，`format=plain` 原文与开工前逐字相同：`Retrieved 1 log entries`，数据为 `287 node options failed to load and were skipped.`。紧接着的 `format=detailed` 仍是 `type=Error`、同一 `file`、`line=131`。过滤 `NullReferenceException` 的 0 条是开工前那一次；收工后的 plain/detailed 全文里没有这串异常类名。

源码对照：`UnitBase.cs:113` 拼出 `{failedOptions.Count} node options failed to load and were skipped.`，`:131` 是 `Debug.LogWarning(sb.ToString());`。MCP 把它标成 `Error`。它不是 `NullReferenceException`，也不是 `HH.340` 的脚本诊断。

### 1.3 归因

**结论：非本批引入。** 不在本批修改。

- **首见时间**：`2026-09-25 20:51:31 +0800`（`Editor-prev.log:56703`，邻近戳 `1790340691`）。早于 `M5-C` 导入行 `67362`，也早于提交 `2026-09-26 00:27:32 +0800`。
- **栈落点**：`UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadManager.OnBeforeSerialize () [0x00001] in <ffb70ee66fe4403f8ccd5607b2cfd687>:0`。这是编辑器内建程序集（哈希 `<ffb70ee66fe4403f8ccd5607b2cfd687>`），不是 `Assets/_Game` 下的文件。
- **归属依据**：7/7 次都在 `Reloading assemblies` / `Begin MonoManager ReloadAssembly` 窗口里，帧只有 Package Manager UI 的 `OnBeforeSerialize`。第 1–3 次发生时，日志里还没有 `TaskProtocolRuntime.cs`、`TaskProtocolIssuer.cs`、`TaskProtocolPolicyConfig.cs`、`M5C_TaskProtocolProbe.cs` 的 `Start importing` 行。第 4–7 次栈不变。当前会话 `Editor.log` 的 NRE 命中数为 0。

## 二、五项政策逐项探针

上一跑探针 `Assets/Editor/Smoke/M5C_TaskProtocolProbe.cs` 已不在仓库。全库检索 `M5C_TaskProtocolProbe`：**命中 1 个文件**（原交付报告里的提及），**探针 `.cs`／`.meta`／日志产物命中 0**。没有上一跑文件可搬进 `Logs/_archive_*`。本轮用 `execute_code(compiler=codedom)` 在内存里重跑，不写 `Assets`，不改 `TaskProtocolPolicyConfig.asset`。原文副本在 `Logs/_archive_20260926_102624/HH340_policy_probe.txt`（不入库）。

两次 `execute_code` 的 `compiler` 字段都是 `codedom`。下面「改前 → 改后」是同一次调用里的字段读数。

### 2.1 priority

```text
priority beforeBase=0 submit=Ok afterBase=40 effective=40 baseAfterRead=40
```

`Create` 后 `basePriority=0`。`Submit(..., 40)` 返回 `Ok`，`basePriority` 变为 `40`。`GetEffectivePriority` 返回 `40`。调用后再读 `basePriority` 仍是 `40`。

### 2.2 retry

```text
retry retryMaxAtCreate=1 retryMaxAfterSubmit=1 beforeCount=0 first=None midCount=1 midStat=1 second=RetryExhausted afterCount=1 afterStat=1
```

`Create` 时 `retryMax=1`（配置 `defaultRetryMax=1`）。消费前 `retryCount=0`、`retryConsumedCount=0`。第一次 `TryConsumeRetry` 返回 `None`，`retryCount=1`，`retryConsumedCount=1`。第二次返回 `RetryExhausted`，两个计数都停在 `1`。

### 2.3 不可达冷却

```text
cooldown blockedBefore=0 eligibleBefore=True assign=None unassign=None blockedAfter=105 e0=False e1=False e2=False e3=False e4=False e5=True
```

标记前 `unreachableBlockedUntilTick=0`，tick `100` 时可派发。`Assign` 与 `Unassign(Unreachable, 100)` 都返回 `None`。标记后边界 = `105`（`100 + unreachableCooldownTicks`）。tick `100` 至 `104` 不可派发（5 个 tick），tick `105` 恢复。配置读数：`tickIntervalSeconds=1`，`unreachableCooldownTicks=5`。

### 2.4 分域

```text
domain k1Before=0 k2Before=0 k1AfterA=1 k2AfterA=0 k1AfterBoth=1 k2AfterBoth=1
```

`kingdomId=1` 与 `kingdomId=2` 各提交一张卡。先提交 A 时桶 1 = 1、桶 2 = 0；再提交 B 后两桶都是 1。配置 `domain=Kingdom`。本轮没有 `chunkId` 参数，也没有调用 `TerritorySystem`。

### 2.5 统计

```text
stats Acreated=1 Bcreated=1 Apending=1 Bpending=1 AretryBefore=0 BretryBefore=0 consumeA=None AretryAfter=1 BretryAfter=0 BpendingAfter=1
```

两张卡 `createdCount` 都是 `1`，`pendingCount` 都是 `1`。只对 A 调用 `TryConsumeRetry`：A 的 `retryConsumedCount` 从 `0` 变为 `1`，B 保持 `0`，B 的 `pendingCount` 仍是 `1`。配置 `statistics=PerTask`。

同一次返回里的配置原文：

```text
policy defaultRetryMax=1 tickIntervalSeconds=1 unreachableCooldownTicks=5 domain=Kingdom statistics=PerTask
```

**汇总**（不能代替上面五行）：本轮五项政策探针 **总数 5／执行数 5／失败数 0**。这不是把已删除探针的 `total=10` 再报一遍。

## 三、`52/15` → `54/15` 的工具差异

域都是仓库根下的 `Valley Rampart/Assets/_Game`（即 `Valley Rampart/Valley Rampart/Assets/_Game`）。模式都是 `TaskScheduler\.Instance|TaskScheduler\.HasInstance`。

**法 A**（PowerShell 默认代码页收 `rg` 管道）：

```text
$hits = @(rg -n --glob '*.cs' -e 'TaskScheduler\.Instance|TaskScheduler\.HasInstance' -- 'Valley Rampart/Assets/_Game')
```

`Get-Command rg`：`rg.exe`，`c:\Users\trs\AppData\Local\Programs\cursor\resources\app\node_modules\@vscode\ripgrep\bin\rg.exe`。

默认编码与数组长度：

```text
OutputEncoding WebName=gb2312 CodePage=936 BodyName=gb2312
array_count=52
```

`cmd /c` 把同一条 `rg` 的 stdout 落到文件：`RAW_LF=54`，`RAW_CR=6`，按 UTF-8 拆出的非空记录数 = **54**。54 条里含非 ASCII 的记录 **2** 条，且只有这 2 条：

```text
ResourceRespawnSystem.cs:484: ... // 三型＝改前实体口径
Building.cs:659: ... // ⭐ 件5：只计拆除任务
```

在代码页 936 的数组里，这 2 条各自和下一条粘成 **一个** 字符串（`prefixes=2`）。码点序列里没有 `U+000A`。粘连的下一条分别是：

```text
ResourceRespawnSystem.cs:484 的文本结束后直接是 ResourceRespawnSystem.cs:487
Building.cs:659 的文本结束后直接是 Building.cs:1317
```

所以数组长度是 `54 - 2 = 52`。文件数仍是 15，因为被吞的那一行所在文件还有别的命中。

**对照**：同一条 `@(rg ...)` 之前把 `[Console]::OutputEncoding` 设成 UTF-8，得到 `array_count=54`，含两个 `.cs:行号` 前缀的元素数 = **0**。

**法 B**（`Select-String` 按文件读，不经控制台代码页）：

```text
Select-String lines=54 files=15
```

同一脚本对两个根目录的「仅代码行」（整行 `//` 剔除）读数：

```text
DOMAIN C:\Users\trs\Desktop\Valley Rampart\Valley Rampart\Assets\_Game lines=54 files=15
DOMAIN C:\Users\trs\Desktop\Valley Rampart\Valley Rampart\Assets lines=149 files=27
```

机制：少掉的 2 不是源文件少了 2 行。`rg` 自己写出 54 个 LF。PowerShell 用代码页 936 把管道收成对象时，把 2 条含非 ASCII 的记录和各自的下一条收成了一个数组元素。UTF-8 代码页下这条合并消失。`Select-String` 的 54/15 与 `rg` 的 54 个 LF 一致。

## 四、扫描域

| 读数 | 域 | 标注 |
|---|---|---|
| **54 行 / 15 文件** | `Valley Rampart/Valley Rampart/Assets/_Game/**/*.cs` | 生产码。本批基线用这个数 |
| **149 行 / 27 文件** | `Valley Rampart/Valley Rampart/Assets/**/*.cs` | **含 Editor 探针／非生产域** |

149/27 比 54/15 多出的 **95 行 / 12 文件**全部在 `Assets/Editor/Smoke/`（与 Select-String 总数一致的 Python 正则分区；12 个文件：`Smoke_2_23RB.cs`、`Valley2_17_Smoke_FixCard.cs`、`Valley_DiagMilitary.cs`、`Valley_HH107_Smoke_Byproduct.cs`、`Valley_HH291_MapGenProbe.cs`、`Valley_HH294_Slice6_2Probe.cs`、`Valley_HH315_M1C_Smoke.cs`、`Valley_HH316_U2Smoke.cs`、`Valley_HH317_M1D_Probe.cs`、`Valley_HH319_F1LongRun.cs`、`Valley_HH319_M1F_Probe.cs`、`Valley_OB12_InGate_Probe.cs`）。不得把 149/27 当成生产调用面。

## 五、附：Roslyn 路径（不改资产）

`execute_code(compiler=roslyn)` 原文：

```text
{"success":false,"message":"Roslyn (Microsoft.CodeAnalysis) is not available. Install it via NuGet or use compiler='codedom'.","data":null}
```

`compiler=auto` 的返回里 `"compiler":"codedom"`。`compiler=codedom` 可以执行，本轮五项探针走的是这条。没有调用 `manage_asset`，没有改 `TaskProtocolPolicyConfig.asset`。Codely `execute_csharp_script` 本会话未注册。

## 六、停手核对

| 条件 | 结果 |
|---|---|
| NRE 判为本批引入 | 未命中。栈是 Package Manager UI，首见早于本批导入 |
| 需要改生产码才能补证 | 未命中。本批未改 `Assets` |
| 需要改策划账本 | 未命中。只在下面声明应登记项 |
| 需要改 `06` 或设计文档 | 未命中 |

## 七、未完成项

1. 本报告不判绿、不销号，不放行 `06:154` 的②切换或③清理。
2. 不把现场那条 Visual Scripting 日志写成编译已清。MCP 对它的 `type` 是 `Error`，源码 `:131` 是 `Debug.LogWarning`。
3. 已删除的 `M5C_TaskProtocolProbe` 源文件不在 git 里，本轮没有逐字重放它原来的 10 个断言名字。
4. 补证开工前那次 `read_console` 的返回里没有时钟字段；收工打点是 `2026-09-26 10:26:24.424 +08:00`。
5. 本批没有调用 `refresh_unity`。

## 八、应登记项

```text
HH.340 | M5-C 补证 | 执行端 | 补证报告已落盘；不覆盖原报告；不判绿；不销号
账本：执行端不改 _编号登记.md / _任务队列.md / _当前快照.md / 测试基线台账.md
NRE：非本批。首见 2026-09-25 20:51:31 +0800，Editor-prev.log:56703
栈：UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadManager.OnBeforeSerialize
    <ffb70ee66fe4403f8ccd5607b2cfd687>:0
探针：五项政策 总数5／执行5／失败0；52/15 为代码页936管道合并，rg 原始 LF=54，Select-String=54/15
域：54/15 = Assets/_Game 生产码；149/27 = 全 Assets，含 Editor/Smoke
提交面：仅本补证报告；未 push
```

## 九、主策划端终裁（D869，2026-09-26）

**HH.340 `M5-C` 补证验收成立，判绿并销号。**

事务端六项门槛全部复核通过：完整 NRE 栈落在 Unity 包管理器内部，首见早于本批导入与提交、当前会话为 0，故归为非本批引入的域重载期编辑器异常；五项政策逐项探针 `5/5/0`；`52/15→54/15` 已给出代码页 936 管道粘连的可复核根因；生产域 `Assets/_Game` 的基线为 `54/15`，全 `Assets` 的 `149/27` 明确排除为生产调用面。原实施面的 30 类型零接线、旧链基线、配置读取、探针清理与断言汇总继续采信。

D767 违纪定性维持：执行端曾代写策划账本；该在途改动已回退，策划端账本以实存重登记为准，不纳入 HH.340 交付面。该纪律项不阻断本批代码验收。

包管理器 NRE 作为观察项保留；重复出现时另立编辑器/包管理器异常排查。`06:154` 的②切换与③清理仍锁死，`HH.338`、`HH.339` 仍按各自报告独立验收。
