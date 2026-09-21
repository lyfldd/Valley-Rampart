# HH.319 · `M1-F` · `U-16` 批 —— **形态裁定**（`D815` · 2026-09-21）

> 被裁：执行端《形态报选》（**只读复核 · `Assets/**` 一行未动 · 未落码**，停在裁定门前）。
> 结论：⭐ **案① 准 ＋ 加料 A／B 全采 ＋ 本端加料 C（新）＋ 2 处锚点自纠 ＋ 1 处表达式补正**。
> 交付物：`HH.319_M1-F-U16批` 施工任务书（`D815` 定稿）——**与本裁定同批发出**（提示词不落档）。
> ⚠️ 本裁定**取代** `D814` 中「修法已定 甲-b」的形态结论（粒度不变，**落点改为案①**）。

---

## 一 · 复核结论：报选 **12 项逐条实证**（含 2 处勘正）

| # | 执行端主张 | 判 | 本端实证锚点 |
|---|---|---|---|
| 1 | `NavigateTo` 调用面 **6 处** | ✅ 逐点一致 | `BehaviorExecutor.cs:115/152/184/211/229/299` |
| 2 | 威胁移动亦经 `MoveTowards:115`、`FullRetreat` 经 `:152` ⇒ 「甲-b 两副作用都消解」**不成立**；`:128` 战术短撤**不经** `NavigateTo` | ✅ | `StimulusTypes.cs:87` `FocusType => FocusType.Position` ／ `L2PostureDecider.cs:169` ／ `BehaviorExecutor.cs:128` `_controller.Move(dir.normalized, run: true)` |
| 3 | `ExecuteWorkAt` 到达判定取 `cmd.TargetPos` ⇒ 与移动目标脱节；**焦点另源时 `:172-178` 误触 `HarvestCarry`** | ✅（**不止"脱节"**） | `BehaviorExecutor.cs:160-163`（`Vector2X.Distance(…, cmd.TargetPos)`）／`:172-178`（`cmd.HarvestTarget` 消费） |
| 4 | 威胁挂起**只在 `Working`** ⇒ `MovingToSource` 期无挂起判据 | ✅ | `TaskScheduler.cs:461 case TaskState.Working` → `:464 brain.ThreatFactor > GetAbandonThreshold(brain)` |
| 5 | 勘正 (a)：吸附先例在 `TestFixtureApi:**250**`（`:243` 是方法注释行） | ✅ **本端引错行号 ⇒ 认** | `TestFixtureApi.cs:243` ＝ `/// <summary>实体注入…` 注释 ／ `:250` ＝ `SpawnPosSnapper.SnapWorld(world, …)` |
| 6 | 勘正 (b)：`BehaviorExecutor` **不是 `AI.Core` 的类** ⇒ 我上轮「接缝纪律」**论据不成立** | ✅ **本端论据被推翻 ⇒ 认** | 全库 `asmdef` **仅 1 个**（`Assets/_Game/Systems/AI.Core/AI.Core.asmdef`）；`BehaviorExecutor` 住 `_Game/Systems/AI/Execution/`、`using UnityEngine`（`:1`）、用 `Vector2` ⇒ 与 `TaskScheduler` **同程序集** |
| 7 | 案① 附带好处：`_executor.Execute` **全库唯一调用点** | ✅ | repo-wide 扫描 `.cs` ⇒ **恰 1 处** `NPCBrain.cs:483` |
| 8 | 案① 代价：同工双派**确有可能** | ✅ | `ScheduleCenterStub.cs:142` 只查 `worker.IsIdleForTask` ＋ `:143 AddTaskStimulus`（⛔ **不进** `_npcTaskMap`） |
| 9 | `stale.Add(` 13 处 ＝ **有效 6／惰性 6／另容器 1** | ✅ **逐点一致** | 有效 `:414`(BrainLost)`/419`(Dead)`/430`(SourceInvalid)`/452`(Timeout)`/571`(Timeout)`/581`(Unknown)；惰性 `:492/507/522/540/546/567`（**均紧跟 `Complete`** ⇒ `ClearNpc:646 Remove` ⇒ `:588 TryGetValue` **恒假**）；另容器 `:213`（`OnBuildingDied` 自有 list）⇒ `:218` 直 `Abandon` |
| 10 | 件1 复位清单**无遗漏** | ✅ | 解绑出口**仅 2 个**：`Complete:605`／`Abandon:641`（`:602`／`:638` 已写 `false`）＋ 兜底 `NPCBrain:249 ResetForReuse` |
| 11 | 加料 A 的依据（⛔ 不含 `Cautious`） | ✅ | `StimulusTypes.cs:41` 注释**逐字**「Cautious = 2, // 谨慎：维持工作，抑制低优先刺激」 |
| 12 | 加料 B 的依据 | ✅ | `L2PostureDecider.cs:169` `ctx.ArrivedAtFocus ? Idle : MoveTowards` ⇒ 到达态**直接决定模块**；`:704` 赋值点在场；`FocusDecision.Focus` 类型 ＝ **`IStimulus`**（`DecisionStructs.cs:18`）⇒ `is ThreatStimulus` **合法**（先例 `NPCBrain:133/134`） |

---

## 二 · 裁定（6 项）

### 2.1 `Q-形态` ⇒ ⭐ **准案①**（`NPCBrain` 整块跳过 ＋ 豁免判据）

```
if (_executor != null && !(IsKingdomTaskWorker && !豁免))    // 让位门
    _executor.Execute(in _lastCmd, Time.deltaTime, GetCellSize());
豁免 = 焦点 is ThreatStimulus  ‖  _lastCtx.PostureDecision.Spectrum == BehaviorSpectrum.FullRetreat
```

- ⭐ 与 `N1` 的结论一致（**恢复被架空的历史机制** · 净增 ≈1~3 行）；位移与到达判定**同进同退**（不留隐性态）。
- ⭐ **准加料 A**（豁免加宽至 `FullRetreat`）：⛔ **不含 `Cautious`** —— 依据 ①`StimulusTypes:41` 注释；②**机制层**（见加料 C）。
- ⭐ **准加料 B**（到达态冻结）—— ⚠️ **本端补正见 2.6**。
- ⛔ 不选案②（只挡位移 ⇒ §一-3 **脱节 ＋ 误触**未解）；⛔ 不选案③（牵动 `TaskScheduler` 状态机语义 ⇒ 单列后续批）。

### 2.2 `Q-投递通道` ⇒ ⭐ **案① 下不需要**

`_executor.Execute` 全库唯一调用点 ＝ `NPCBrain:483` ⇒ 判断写在调用点，**`BehaviorExecutor` 一行不改**（本端实证）。
📌 **备案准**：若后续改案②/③ ⇒ 采 **形参式** `Execute(in cmd, dt, cellSize, bool navigateAllowed)`（现签名 `:59` 三参 ⇒ 加一参即可；编译期强制、⛔ 不用字段式隐式契约）。

### 2.3 `Q-stale 形态` ⇒ ⭐ **准** `List<(int id, AbandonReason reason)>`

单列表、保序、值类型零额外分配。⛔ 否决"按 reason 分多列表"（打乱现状"按收集序 `Abandon`"的隐性保序）。
⚠️ 惰性 6 处**标 `Inert` 注释即可，⛔ 不删码**（删 ＝ 行为变更，超出本批）。

### 2.4 `Q-AbandonReason` ⇒ ⭐ **准** `TaskScheduler.cs` 内 **private enum**

`Abandon` 是 `private`、零跨文件引用面 ⇒ 定义在 `TaskScheduler.cs` 内，**零跨文件影响**。
📌 ⚠️ 但**判据 6 要读 `reason`** ⇒ ⭐ **探针只从日志读**（⛔ 不得为此把枚举公开）⇒ **日志格式升为判据契约**（见 §四）。

### 2.5 `Q-件1 复位` ⇒ **准**（无遗漏 · ⛔ 不在 `ClearNpc` 补）

置位点 ＝ `Dispatch` 的 `:362`（`_suspendStartTime.Remove(id);`）**之后、`:363 InjectStimulus` 之前**；`DispatchExternal:334` 走 `Dispatch` ⇒ 自动覆盖 ✓。复位点已全在（见 §一-10）。

### 2.6 `Q-加料 B 表达式` ⇒ ⭐ **本端补正：与让位门共用同一个局部布尔**

执行端拟 `ctx.ArrivedAtFocus = IsKingdomTaskWorker ? false : _executor.ArrivedAtFocus;`
⚠️ 该式在**豁免窗口内也强制 false**，与"豁免期执行路径应与改前逐位一致"相悖（`FullRetreat` 期 `ArrivedAtFocus` 会影响 `L2` 模块选择）。
⇒ **改裁**：把它与让位门**提取为同一个局部布尔**，两处共用：

```
bool 让位 = IsKingdomTaskWorker && !豁免;
if (_executor != null && !让位) _executor.Execute(...);
…
ctx.ArrivedAtFocus = 让位 ? false : _executor.ArrivedAtFocus;   // 仅"真跳过"时冻结
```

理由：**一处条件、两处消费** ⇒ ⛔ 不会出现"两处条件漂移"（同族教训 `L-43`/`L-44`：判定类结论必须回到同一真值源）。

---

## 三 · 本端加料 C（新读数）＋ 2 处自纠

### 3.1 ⭐ 加料 C：`_taskDiscount` ＝ 「在册任务失焦」的**第三来源**（**有意设计**）

```
AttentionSystem.cs:271  float eff = _taskStimuli[i].Intensity * _taskDiscount;   // 任务刺激打折后参与层内竞争
AttentionSystem.cs:348  private float _taskDiscount = 1f;
AttentionSystem.cs:351  public void SetTaskDiscount(float discount) => _taskDiscount = discount;
NPCBrain.cs:735         _attention.SetTaskDiscount(ctx.StateTaskDiscount);      // ← 唯一调用方
```
⇒ ⭐ 「在册任务失去焦点」共有 **3 个来源**：① 刺激**过期** ② 续命 `Remove`＋`Add` **排尾降权**（＝**缺陷** · `U-19`）③ `_taskDiscount` **打折**（＝**有意设计**，随 `Caution` 态）。
⇒ **两条结论**：① `U-19` 机制清单**补第 3 条**（并在判据里**把 ③ 排除**，否则会把设计当缺陷改坏）；② **案① 的豁免判据 ⛔ 不得纳入 `Cautious`** —— 纳入即等于**撤销**该设计（与执行端结论**同向**，本端补的是**机制出处**）。

### 3.2 自纠 ①（**文件归属**）：`D814` 落账两个锚点只写了行号

| 锚点 | `D814` 写法 | **正确归属（本轮补全）** |
|---|---|---|
| `SelectTopTaskLayer` 的严格 `>` | `SelectTopTaskLayer:272` | **`Assets/_Game/Systems/AI.Core/Decision/AttentionSystem.cs:262`** 方法头（严格 `>` 在 **`:272`**；`:271` 为 `Intensity × _taskDiscount`） |
| `priorityWeight` 唯一消费 | `ComputeWorkFactor:1029` | **`Assets/_Game/Systems/AI/NPCBrain.cs:1025`** 方法头（消费在 **`:1029`**） |

⇒ ⛔ **两者都不在 `TaskScheduler.cs`** —— `D814` 只写行号未写文件名，属**可误引的落账缺陷** ⇒ 本轮补全为「**文件:行**」，并立教训 `L-64`。

### 3.3 自纠 ②：`U-19` 核心断言**独立复核仍成立**

`GetPriorityWeight` 全库 **3 处命中** ＝ 定义（`AttentionTuningConfig.cs:408`）＋ 注释 1 ＋ **唯一消费 `NPCBrain:1029`** ⇒ ⭐ 「`priorityWeight` **不参与焦点选择**」**成立** ✓。

---

## 四 · 日志契约（判据 6 的前提 · 本轮新增）

`Abandon` 日志须 **一行可 grep ＋ 四要素**（⛔ 不得只写 reason）：

```
[TaskScheduler] Abandon <task.type> → npcId <id> reason=<Reason>
```

- ⭐ 取值域 **7 个**：`External`／`Dead`／`Unreachable`／`SourceInvalid`／`Timeout`／`BrainLost`／`Unknown`（`:581 default` ⇒ `Unknown`）。
  ⚠️ `Inert` 是**惰性标记**，⛔ **不入日志枚举**（它那 6 处**本该永不到达**）。
- ⚠️ `:591` 循环内 `brain` 可能为 `null`（`:590 TryGetValue` 失败）⇒ 日志与 `reason` 组装**须容 null**。
- ⭐ 依据：`甲′-b` 落 `:452`（`Timeout`）／`甲′-a` 落 `:204 OnPathFailed`（`Unreachable`）⇒ **分流可被日志区分** ＝ 件2 目标达成。

---

## 五 · 本批范围与红线

- **改动面（3 文件）**：`NPCBrain.cs`（让位门 ＋ 豁免判据 ＋ `:704` 冻结）、`TaskScheduler.cs`（`Dispatch` 置位 ＋ `Abandon` 签名/日志/`stale` 结构 ＋ `AbandonReason` 私有枚举）、探针 `Assets/Editor/Smoke/Valley_HH319_F1LongRun.cs`（`SpawnWorker` 落位吸附）。
- ⭐ **`BehaviorExecutor.cs` 本批零改动**（案① 的直接结果）。
- ⛔ 本批**不含**：`U-18`（落位"甲/乙"未裁 ⇒ 落位修法不得预支）／`U-19`（改"在册优先"会与本批判据 1 争位）／`U-17`（降派发频率 ⇒ **污染读数**）／`D-1`／`D-2`。
- ⛔ 不动：`MonsterAI`／`FormationBrain`／`SelectionController`／`UnitFactory`／存档 schema／`WarehousePanel`／四档账本／美术／`pixel-forge`／`GameScene`／`Packages`／`3.6`·`3.8` doc。
- 行尾纪律：改前先验；CRLF／MIXED 走 python 二进制按行替换并**保留原行尾**。
- 进局冒烟走 `TestHarnessApi.EnterTestRun` 正门；收尾三态（残留 0／`isDirty=False`／根对象数）。
- 具名 `git add`；⛔ 不 push；⛔ 不代提交策划端账本；**报告自行 commit**。

---

## 六 · 判据（10 条 · 走生产路径 · `L-51`／`L-48` · 每条附鉴别力）

1 ⭐⭐ **靶例（本批核心）**：生产路径（真 `BuildController`／`ScheduleCenterStub`）⇒ 农场缺水 ⇒ `WaterHaul` 派发 ⇒ 工人**走到水井**（打印 `task.source` 身份）⇒ 装载 ⇒ 走到农场 ⇒ 卸货 ⇒ 到账。
  鉴别力：改前 ＝ `dest` 被覆盖（`D813` 实测 `npc35 f1009~1013 dest=(-27.62,31.32)` ⇒ 末值农场）⇒ 30 秒超时。
2 ⭐ **`甲′-b` 结构性消失**：`[TaskScheduler] 完成 WaterHaul` **> 0**（`D813` 三跑恒 **0**）。鉴别力：改前恒 0。
3 ⭐ **`IsKingdomTaskWorker` 生命周期**：派发置 `true` ⇒ 完成/放弃复位 `false` ⇒ 须给**逐次派发-复位配对**读数（⛔ 不得只给末值）；⚠️ 并证**未在册者恒 `false`**。
4 ⭐ **豁免面（§二-2.1）双列**：
   - 4a **威胁豁免**：在册任务期拉威胁 ⇒ 工人**能逃**（或按 `06:88` 挂起）。鉴别力：未做豁免 ⇒ 本列读作"被任务锁死不逃"。
   - 4b ⭐ **`Cautious` 不豁免（反向列）**：威胁未越放弃阈值（`Cautious` 态）⇒ 工人**继续干活**。鉴别力：若把 `Cautious` 一并豁免 ⇒ 本列读作"逃" ⇒ **能读出差异**（依据 ＝ 加料 C `_taskDiscount`）。
5 ⭐ **到达态冻结（加料 B）**：让位生效期 `Executor.ArrivedAtFocus` **不参与 L2** ⇒ 一次完整 `WaterHaul` 中**不出现"恢复瞬间提前 `Complete`／`HarvestCarry`"**。鉴别力：不冻结 ⇒ 恢复后首帧可能读到"已到达"。
6 **无泄漏**：逐路径给读数 — `AbandonTask`／`OnNpcDied`／`OnPathFailed`／`OnBuildingDied`／stale 超时 ⇒ `IsKingdomTaskWorker` **全复 `false`**。
7 **`Abandon` 日志**：5 调用点各 **≥1 条**读数 ＋ `reason` 正确（按 §四 格式）；⚠️ 读数须按 tag 过滤（`甲′-a-2` 的 `PathFailed` 风暴 4022~28496 次/跑会淹日志 —— **报告须出"过滤口径"**）。
8 ⭐ **探针吸附**：`SpawnWorker` 落位**可走** ⇒ `A*` 复演 **≠ `Unreachable`**（`D813` 恒 `Unreachable`）。
9 **回归**：① `HH317`（`M1-D`）② `HH315`（`M1-C`）③ `HH316`（`U-2` §A~§H）④ `2_20B_M7`（六轮）。⚠️ 任一项出红 ⇒ **停手报裁**（⛔ 不得自行改码使其变绿）。
10 **存档往返**（本批预期**不动**序列化面 ⇒ 逐值）。

---

## 七 · 施工序（不变）

`HF-0 探针吸附`（并入本批件3）→ **`U-16` 批（本裁定）** → `U-18`（落位报选 ＋ 标记不可达）→ `U-19`（`InjectStimulus` 原地更新 ＋ `06` 补句）→ `D-1` → `D-2`。
⛔ `U-17` 须**晚于** `U-16` 复跑（降派发频率 ⇒ 污染读数）。

---

## 八 · 状态

- `U-16`：⭐ **形态已裁 ⇒ 准施工**（⛔ 仍不销号 —— 待施工 ＋ 判据 1/2 达成）。
- ⛔ `F-1` **仍不销号**（`甲′-b` 未修 ⇒ 判据 `3b` 端到端未达）。
- 关联：`D814`（收口评估验收 · 修法性质改写）／`D813`（探针批 · `甲′` 已证）／`b7e150b8`（执行端报选）。
