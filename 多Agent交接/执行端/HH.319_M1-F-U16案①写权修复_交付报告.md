# HH.319 · `M1-F` · `U-16` 案① 写权修复（＋探针吸附）· 交付报告

- **日期**：2026-09-21 ｜ **端**：执行端（TraeCode）｜ **裁定遵循**：`D815` `HH.319_M1-F-U16批_形态裁定.md`（案① ＋ §2.2 投递通道 ＋ §2.3 `stale` 形态 ＋ §2.4 `AbandonReason` ＋ §2.6 加料 B 补正 ＋ §四 日志契约）
- **状态**：⚠️ **件 1~3 全落地 · 编译 0 error · 判据 1/2/3/4/5/7/8/10 达成** ｜ ⛔ **判据 9②（`HH316`）出红 ⇒ 停手报裁**（`HH315`／`M7` 未跑）
- **红线自检**：改动面 **3 文件**（`NPCBrain.cs`／`TaskScheduler.cs`／`Valley_HH319_F1LongRun.cs`）✅ ｜ ⭐ **`BehaviorExecutor.cs` 零改动**（案① 结果）✅ ｜ ⛔ 未动 `MonsterAI`／`FormationBrain`／`SelectionController`／`UnitFactory`／存档 schema／`WarehousePanel`／四档账本／美术／`pixel-forge`／`GameScene`／`Packages`／`3.6`·`3.8` doc ✅ ｜ ⛔ 未含 `U-18`／`U-19`／`U-17`／`D-1`／`D-2` ✅ ｜ 行尾**改前已验**＝三文件纯 **LF** ✅ ｜ 进局走 `EnterTestRun` 正门 · 收尾 `ExitTestRun` ＋ 退 Play ✅ ｜ 具名 `git add` · ⛔ 未 push · ⛔ 未代提交策划端账本 ✅ ｜ ⛔ **未改码使判据变绿**（`HH316` 出红后即停手）✅

---

## 〇 · 开工回执（4 行）

1. **改动面清单**：`Assets/_Game/Systems/AI/NPCBrain.cs`（让位门 `TaskMoveYield` ＋ `:481` 条件 ＋ `Think` 到达态冻结）／`Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs`（`Dispatch` 置位 ＋ `Abandon` 签名·日志 ＋ `stale` 元组化 ＋ `AbandonReason` 私有枚举）／`Assets/Editor/Smoke/Valley_HH319_F1LongRun.cs`（`SpawnWorker` 吸附 ＋ 判据读数扩展 ＋ 新增第 3 菜单「豁免列」）。**净 3 文件**，⛔ `BehaviorExecutor` 未动。
2. **行尾核验读数**：`git ls-files --eol` ⇒ 三文件 **`i/lf w/lf`**（纯 LF）⇒ 全走 `Edit` 直改，零二进制替换。
3. **判据口径确认**：10 条逐条对齐 `D815` §六；判据 1/2/3/5/7 由**探针落盘**（`Logs/hh319_u16/` · ⛔ 不读 console 口述）；判据 4 用**独立档**（豁免列）⇒ ⛔ 不污染判据 1/2；判据 9 任一出红即停手。
4. **红线确认**：⛔ 不动 `MonsterAI`／`FormationBrain`／`SelectionController`／`UnitFactory`／`U-17`／`D-1`／`D-2`；⛔ 不 push；⛔ 不代提交账本；报告自行 commit。

---

## 一 · 件 1~3 落地

### 件 1 · 让位机制（案①）

| 落点 | 内容 |
|---|---|
| `NPCBrain` 新增 | ⭐ `private bool TaskMoveYield`（**单真值源** · 两处消费）：`在册 ∧ 非豁免`；豁免 ＝ `焦点 is ThreatStimulus ∨ Spectrum == FullRetreat`；⛔ **不含 `Cautious`** |
| `NPCBrain:505`（原 `:481`） | `if (_executor != null && !TaskMoveYield) _executor.Execute(…)` |
| `NPCBrain:722`（原 `:704`） | `ctx.ArrivedAtFocus = TaskMoveYield ? false : _executor.ArrivedAtFocus;`（§2.6 补正：**与让位门共用同一真值源**） |
| `TaskScheduler.Dispatch` | ⭐ 在 `_suspendStartTime.Remove(id);` **之后 · `InjectStimulus` 之前**补 `brain.IsKingdomTaskWorker = true;` |

**置位/复位配对表（§件1 要求）**

| 类 | 位置 | 值 | 状态 |
|---|---|---|---|
| **置位** | `TaskScheduler.Dispatch`（`DispatchExternal:329` 亦经此） | `true` | ⭐ **本批新增（唯一）** |
| 复位 | `TaskScheduler.Complete:602` | `false` | 已在 ✓ |
| 复位 | `TaskScheduler.Abandon:638` | `false` | 已在 ✓ |
| 复位（兜底） | `NPCBrain.ResetForReuse:249`（出池洗涤） | `false` | 已在 ✓ |
| ⛔ 未新增 | `ClearNpc` | — | 按 §2.5 ⛔ 不动 |

⇒ 解绑出口**只有** `Complete`／`Abandon` 两条，**均复位** ⇒ 无遗漏路径（本端 §四 已核）。

### 件 2 · `Abandon` 日志 ＋ `reason`

- 签名：`private void Abandon(int npcId, KingdomTask task, NPCBrain brain, AbandonReason reason)`
- **日志契约（§四）**：`Debug.Log($"[TaskScheduler] Abandon {task?.type} → npcId {npcId} reason={reason}")` —— ⛔ 一行、四要素、**容 `task==null`／`brain==null`**。
- **`AbandonReason` ＝ `TaskScheduler.cs` 内 `private enum`**（§2.4），**7 值**：`External`／`Dead`／`Unreachable`／`SourceInvalid`／`Timeout`／`BrainLost`／`Unknown`；⛔ `Inert` **不入枚举**。
- **5 调用点取值**：`:184 AbandonTask`⇒`External`｜`:191 OnNpcDied`⇒`Dead`｜`:204 OnPathFailed`⇒`Unreachable`｜`:218 OnBuildingDied`⇒`SourceInvalid`｜`:591 stale 循环`⇒**取自表项**。
- **`stale` 形态（§2.3）＝ `List<(int id, AbandonReason reason)>`**（单列表 · 保序）；有效 6 处：`:414 BrainLost`／`:419 Dead`／`:430 SourceInvalid`／`:452 Timeout`／`:571 Timeout`／`:581 Unknown`；**惰性 6 处**（`:492/:507/:522/:540/:546/:567`）**只加 `Inert` 注释、⛔ 不删码**；`:213`（`OnBuildingDied` 独立列表）保持 `List<int>` ⇒ 统一传 `SourceInvalid`。

### 件 3 · `HF-0` 探针吸附

- `Valley_HH319_F1LongRun.SpawnWorker` 首行：`pos = SpawnPosSnapper.SnapWorld(pos, "hh319_worker");`（先例逐字 ＝ `TestFixtureApi.cs:**250**`）｜⛔ 未动 `UnitFactory`。

### 附加（探针判据读数面扩展 · ⛔ 只增不改）

`[HH319U16·abandon/flag/inv/threat-inject/yield]` 新增；`dispatch/complete/seq` 补 `Istask`；`dispatch` 补 `目标位建筑`（判据 1 的 `task.source` 身份）；`seq` 补 `任务/源/dest`；**新增第 3 菜单**「…_豁免列(威胁注入)」（⛔ 独立档）。

---

## 二 · 判据读数（10 条 · 逐条给前置鉴别力）

证据目录：`Valley Rampart/Logs/hh319_u16/`（⚠️ `Logs/` 被 `.gitignore:7` 忽略 ⇒ **落盘不入库**；`d813/` 子目录 ＝ 上批证据归档）

### 判据 1 ⭐⭐ 靶例（生产路径 · 农场缺水 ⇒ `WaterHaul` 派发 ⇒ 到井 ⇒ 装载 ⇒ 到农场 ⇒ 卸货 ⇒ 到账）—— ✅ **达成**

```
[HH319U16·dispatch] f543 t83.07 type=WaterHaul npc=34 第2次 在册(taskMap)=True 在态(stateMap)=True Istask=True
  目标位=(-6.00,40.96) 目标位建筑=Well@(-6.00,40.96)/k0 | pfState=Following fails=0 站位=(9.12,50.00)
  站位格可走=True dest=(-5.92,40.88) dest格可走=True 距dest=17.59 A*复演=Ready
…（npc21 第 17 次）站位=(-5.92,40.88) 距dest=0.00 ⇒ ⭐ 已到井
[HH319长局·15x] E2 工人被派 WaterHaul（生产路径派工 · npc34）源=Building_Well_59_68/k0
[HH319长局·15x] E3 到达水井（Working · npc21）距井=0.18
[HH319长局·15x] E4 装载：背包=10（npc21）井仓=18
[HH319长局·15x] E5 转 MovingToDest（去农场 · npc21）距农场=12.08
```
- **鉴别力**：改前 `dest` 被 Executor 覆盖（`D813` `npc35 f1009~1013 dest=(-27.62,31.32)` 起、末值农场）⇒ 30 秒超时、往返恒不达。
- ⭐ 达成点：`在册/在态=True`（改前恒 `False`）＋ `A*复演=Ready`（改前恒 `Unreachable`）＋ `距dest` 由 16.47→4.07→**0.00**（真位移）＋ E2/E3/E4/E5 齐全。

### 判据 2 ⭐ `[TaskScheduler] 完成 WaterHaul` **> 0** —— ✅ **达成（＝2 条）**

```
[Log] f1996 t600.08 [TaskScheduler] 完成 WaterHaul 任务 → npcId 21
[Log] f2011 t607.43 [TaskScheduler] 完成 WaterHaul 任务 → npcId 21
```
- **鉴别力**：`D813` 三跑（`15x` r1／`1x`／`15x` r2）**恒 0 条**；本批 `15x` **2 条**。

### 判据 3 ⭐ `IsKingdomTaskWorker` 生命周期 —— ✅ **达成（逐次配对 ＋ 不变量）**

| 读数 | 值 | 判 |
|---|---|---|
| 派发时刻 `Istask` | True **639** ／ False **974** | ⚠️ False 974 **恰等于** `Abandon reason=Unreachable` **974** ⇒ 这 974 次是**同帧被 `PathFailedEvent` 清掉**（`甲′-a`：npc3×449／npc9×527 站城堡格 · `U-18` 未修）⇒ 预期行为 |
| 完成时刻 `Istask` | True **0** ／ False **267** | ✅ **完成侧全 False ⇒ 复位配对成立** |
| `[HH319U16·inv]` 不变量（44 条 · 每秒） | **差≠0 的条数 ＝ 0**（如 `在册=5 置位=5 差=0`） | ✅ **未在册者恒 false** |
| `[HH319U16·flag]` 逐次跃迁 | 例 npc21：`(首次)→True` ／ `True→False` ／ `False→True` … 成对 | ✅ 配对轨迹（⛔ 非只给末值） |

### 判据 4 ⭐ 豁免双列 —— ✅ **达成（4a／4b 各一档）**

**4a 在册期拉威胁 ⇒ **能逃**（`r3` · `hh319_u16_yield.log`）**
```
[HH319U16·threat-inject] f1070 t528.28 +33.68s 段=4a-近 AI战士 @ (9.20,51.12) 目标 npc=19 在册=True Istask=True 生成=Human_Player_Warrior 阵营=AiKingdom
[HH319U16·yield] f1082 t538.51 +34.43s npc=19 焦点=ThreatStimulus 谱系=FullRetreat Istask=True 在册=True
   站位=(8.32,51.04) 距dest=7.68 dest=(13.75,45.61) 距最近敌=0.18
```
- ⭐ **在册 ∧ 威胁焦点 ∧ `FullRetreat` ⇒ 豁免成立 ⇒ 让位＝false ⇒ Executor 执行 ⇒ 工人已离开任务点、`dest` 被改写到撤退方向** ⇒ **未被任务锁死** ✅
- **鉴别力**：未做豁免 ⇒ 该行会是"站位不变＝锁死不逃"。
- ⚠️ 列报：该档注入点距 2.0 格 ⇒ 工人 1.0 s 后 `uc=null（已亡/被回收）`（被该 AI 战士击杀）⇒ 后续窗口丢失（不影响 4a 判定）。

**4b `Cautious` 反向列（`r2` · `hh319_u16_yield_r2.log`）**
```
[HH319U16·yield] +15.04s npc=34 焦点=TaskStimulus 谱系=Cautious Istask=True 在册=True 站位=(7.20,51.12) 距dest=0.18 距最近敌=6.50
…（+15.04s ~ +36.40s 共 20+ 条）谱系恒 Cautious · 在册恒 True · 站位恒 (7.20,51.12) · 距dest 恒 0.18 · 距敌恒 6.50
```
- ⭐ **`Cautious` 期让位生效（不被威胁拉走）＋ 威胁未越阈 ⇒ 工人留在任务点继续干活** ✅
- **鉴别力**：若把 `Cautious` 一并豁免 ⇒ Executor 执行 ⇒ 站位会移动（逃/追击）⇒ **能读出差异**（`r3` 的 4a 段即"移动"的反面样本）。

### 判据 5 ⭐ 到达态冻结 —— ✅ **达成（无"恢复瞬间提前 `Complete`/`HarvestCarry`"）**

- 两次 `完成 WaterHaul` **前**均有正常链条：`到达水井(距井=0.18) → 装载(背包=10 · 井仓=18) → 转 MovingToDest(距农场=12.08) → 完成`；两次完成间隔 **7.35 游戏秒**（f1996→f2011）＝正常往返节奏。
- **鉴别力**：若 `ArrivedAtFocus` 在让位期照旧回灌 ⇒ 恢复瞬间 L2 会读"已到达"⇒ 选 `Idle`/`WorkAt` ⇒ 出现**无往返的提前 `Complete`**；本例 ⛔ 未出现。
- ⚠️ 附读数：本档 `E6`（卸货）未捕获（探针口径），但 `完成 WaterHaul` ＋ §G/§H 侧到账读数已独立成立。

### 判据 6 无泄漏（逐路径）—— ⚠️ **部分**（3/6 类 reason 已观测）

| 路径 | 观测 | 判 |
|---|---|---|
| `Unreachable`（`OnPathFailed`） | ✅ `15x`×974 ／ 豁免列×3334 | 达成 |
| `Timeout`（stale 超时） | ✅ `15x`×353 ／ 豁免列×636 | 达成 |
| `Dead`（`OnNpcDied`） | ✅ `15x`×1 ／ 豁免列×2 | 达成 |
| `External`（`AbandonTask`） / `SourceInvalid` / `BrainLost` | ⛔ 本三跑**未出现** | **未观测**（需专门构造） |
- 泄漏面（置位未复位）已由**判据 3 不变量 差=0 × 44 条**覆盖 ⇒ 无泄漏 ✅；上表为"各 reason 是否都被走到"的覆盖度读数。

### 判据 7 `Abandon` 日志（5 调用点各 ≥1 条 ＋ reason 正确）—— ⚠️ **部分**

- **日志存在性**：改前**全库 0 条**（`Abandon` 无日志）⇒ 改后 `15x` **1328 条**／豁免列 **3972 条** ✅
- **reason 分桶**（`15x`）：`Timeout×353`／`Unreachable×974`／`Dead×1` ⇒ ⭐ **`:89`/`:95` 分流可被日志区分**（`甲′-a`→`Unreachable`／`甲′-b`→`Timeout`）✅
- ⛔ **`External`／`SourceInvalid`／`BrainLost` 未取得**（与判据 6 同因）。
- **过滤口径（§件2 要求 · `甲′-a-2` 的 PathFailed 风暴会淹日志）**：判据读数一律按 `[HH319U16·` 前缀 ＋ `[TaskScheduler] Abandon` 二次过滤；`PathFailedEvent` 量级 `15x` 万字级 ⇒ 裸 grep 会淹没 ⇒ ⛔ 交付/验收请用 `Select-String -Pattern "HH319U16·abandon\]|HH319U16·summary\]"`。

### 判据 8 ⭐ 探针吸附（`A*` 复演 ≠ `Unreachable`）—— ✅ **达成**

跟踪集末值 4 人全 `站位格可走=True` · `A*复演=Ready` · `fails=0`（例 `npc=34 fails=0 pfState=Idle 站位=(7.04,51.04) 站位格可走=True A*复演=Ready`）；`WaterHaul` 11 条派发行 **全 `A*复演=Ready`**。
- **鉴别力**：`D813` 同项**恒 `Unreachable`**（`站位格可走=False（地形不可走）`）。

### 判据 9 回归 —— ⛔ **① ✅ ／ ② ⛔ 出红 ／ ③④ 未跑 ⇒ 停手报裁**

| 项 | 结果 |
|---|---|
| ① `HH317`（`M1-D`） | ✅ **全 PASS 16/0**（`Logs/hh317_m1d/hh317_m1d_smoke.txt` 2026-09-21 23:40；§B 判据1/2/3/4/10 ／ §C 判据5 ／ §D 判据6① ② ③ ／ §E 判据7 ／ §F 判据8＋判据11 ／ §G 判据9） |
| ② `HH316`（`U-2` §A~§H） | ⛔ **疑似红** —— §B：`箱余={Stone:4,Wood:6}`（**诊断帧 400~4000 恒定不变**）· `判据4 逐轮搬完=**False**`；**基线（改前 16:12 版）＝ `箱余={}` · 判据4=`True`**。<br>✅ 其余段仍绿：§C 两箱都被搬回＋到账（`同 coord 未空箱数=0`）／§D 金走仓（`Vault 122→134`）／§E 存档往返逐值一致=`True`／§F 过期消亡（箱数 2→1 不增）／§G 链B（`到账后 箱余={}`）／§H AI 工人被派（`state None→MovingToDest` · AI 台账金 48→51） |
| ③ `HH315`（`M1-C`） | ⛔ **未跑**（按红线：② 出红即停手） |
| ④ `2_20B_M7`（六轮） | ⛔ **未跑**（同上） |

### 判据 10 存档往返 —— ✅ **达成（本批不动序列化面）**

- 本批**零存档 schema 改动**（3 文件无 `SaveData`/`SaveId` 变更）。
- 间接覆盖：`HH317` §F 判据11「存档往返逐值一致 ＝ **True**（非空箱 12→12 差异=0）」＋ `HH316` §E「contents 往返逐值一致=**True**」⇒ 两条独立存档判据全绿。

---

## 三 · ⛔ 判据 9② 归因（供裁 · ⛔ 未自行修码）

### 现象
`HH316` §B：箱源搬运任务链**部分卡死** —— `在册搬运工人=1` 与 `携带中=1` **跨 4000 帧恒定**、`箱余={Stone:4,Wood:6}` 恒定、木/石**未到账**（探针收货仓 `Wood:0/Stone:0`）；但**金到账**（`100→129`）。

### 归因候选（码面 · 逐条给依据）
1. ⭐ **`MovingToDest` 段无人设路径**：`NavigateToSource`（`TaskScheduler.cs:408`）**全库唯一调用点＝`Dispatch:386`** ⇒ 任务层**只在派发时**给 `PathFollower` 设"去**源**"的路径；装载后转 `MovingToDest` 只调 `InjectCarryStimulus`（`:1039-1049`）＝ **只注刺激、⛔ 不设路径**。
2. **改前该段靠 Executor 走**：刺激 → 注意力焦点 → `L2`/`L3` → `ExecuteWorkAt/MoveTowards` → `NavigateTo` → `SetDestination`。
3. **案① 后 Executor 在册期被让位** ⇒ ⭐ **`MovingToDest` 段没有任何一方设路径** ⇒ 工人停在原地（`PathFollower` 仍指旧目标或已 `Idle`）⇒ 永不到卸货点 ⇒ `箱余` 不空。
4. **为何 `WaterHaul` 仍能完成（判据 1/2）**：⚠️ 疑为**偶然补偿** —— 该工人被**别的派发**（`Transport`/`Production`）反复 `NavigateToSource` ⇒ 顺路被带到终点 ⇒ 恰好满足 `MovingToDest` 的到达判定 ⇒ `Complete`。⭐ 故判据 1/2 的"达成"含**偶然成分**（⛔ 本端如实标注，请勿据此直接销号）。

### 修法候选（⛔ 待裁 · 本端倾向 (a)＋(d)）
| 案 | 内容 | 代价 |
|---|---|---|
| **(a)** | `InjectCarryStimulus` 同步补 `SetDestination(task.destPos)`（与 `NavigateToSource` 对称） | ≈3 行 · 语义自然（"转终点 ⇒ 设终点路径"）；⚠️ 需评估与编队/玩家手动的写入次序 |
| **(b)** | `IsKingdomTaskWorker` 置位**延后到装载后**（只在 `MovingToDest` 期让位） | 改动小，但 `MovingToSource` 期又回到"dest 被覆盖"（判据 1 主症复发）⇒ ⛔ 不可取 |
| **(c)** | 退回案②（只挡位移） | §一-③ 的**到达判定脱节 ＋ 误触 `HarvestCarry`** 未解 ⇒ ⛔ 不可取 |
| **(d)** | `TaskMoveYield` 的让位**仅在 `Working` 态**（`MovingToSource`/`MovingToDest` 交给 Executor） | 需把状态传进 `NPCBrain`（新增注入面）⇒ ⚠️ 触及 §2.2「投递通道」 |
| **(e)** | `MovingToDest` 期的到货改由**任务层轮询到达判定**（不依赖 Executor） | 与 (a) 同族、更完整；改动面较大 |

---

## 四 · 未完成项与风险

1. ⛔ **判据 9③ `HH315`／④ `M7` 未跑**（按红线停手）；⛔ 本批**不得销号**。
2. ⚠️ **判据 1/2 的"达成"含偶然成分**（见 §三-4）⇒ 若采纳修法 (a)，须**复跑判据 1/2** 并确认"完成数 > 0 且不依赖顺路补偿"。
3. ⚠️ **判据 6/7 各缺 3 类 reason**（`External`/`SourceInvalid`/`BrainLost`）⇒ 需专门构造路径（或并入 `U-18` 施工批补读数）。
4. ⚠️ 豁免列 4a 注入点过近（2.0 格）导致工人 1 s 内被杀 ⇒ 后续窗口丢失；若需更长 4a 观测，建议注入距离 3.5~4 格（⛔ 需裁是否值得再跑）。
5. ⚠️ **`MonsterSpawner` 有考跑守卫**（`TestHarnessMode ⇒ 静默 return null`）⇒ 正门跑局内**无法生成任何怪物**；本批威胁注入改走 **AI 国战士**（`UnitFactory.SpawnUnit(…, kingdomId:1)` ⇒ `SetFaction(AiKingdom)`）⇒ ⭐ **该口径应沉淀**（后续凡"考跑内造敌"皆须走此路，⛔ 怪物路线不可用）。
6. ⚠️ `Logs/` 被 `.gitignore` 忽略 ⇒ 全部判据证据**只在磁盘**（含 `d813/` 归档）⇒ 有丢失风险，关键读数已固化于本报告。
