# HH.319 · `M1-F` · `U-16` 案① 批 —— **交付验收裁决**（`D816` · 2026-09-21）

> 被验：`4de46407`（**3 代码文件** ＋ 交付报告；⭐ `BehaviorExecutor.cs` **零改动** ✓ 与案① 预期一致）
> **总判**：⭐ **件 1~3 落地逐落点全对**（本端实证 10 项）｜⛔⭐ **判据 1／2／5 本端否决其「达成」**（**原始日志推翻**）｜✅ 判据 3／4／8／10 达成｜⚠️ 判据 6／7 部分
> ⛔ **`U-16` 不销号**；⭐ **修法裁定 ＝ (a)**（附 3 处限定）｜⛔ **(d)／(e) 否决**｜⚠️ 本端认 **1 处裁决漏项**（`D815` 漏了 `MovingToDest` 段）

---

## 一 · 件 1~3 落地（逐落点实证 · 全对）

| # | 项 | 判 | 本端实证 |
|---|---|---|---|
| 1 | `NPCBrain.TaskMoveYield` ＝ **单真值源**（两处消费） | ✅ | 属性体：`!IsKingdomTaskWorker ⇒ false`；`焦点 is ThreatStimulus ⇒ false`；`Spectrum == FullRetreat ⇒ false`；否则 `true` |
| 2 | ⭐ **§2.6 补正逐字落地** | ✅ | `ctx.ArrivedAtFocus = TaskMoveYield ? false : _executor.ArrivedAtFocus;`（⛔ 未重复条件） |
| 3 | Execute 门 | ✅ | `if (_executor != null && !TaskMoveYield) _executor.Execute(...)` |
| 4 | `Dispatch` 置位点 | ✅ | `brain.IsKingdomTaskWorker = true;` 在 `_suspendStartTime.Remove(id);` **之后 · `InjectStimulus` 之前** |
| 5 | `AbandonReason` **private enum · 7 值** | ✅ | `External/Dead/Unreachable/SourceInvalid/Timeout/BrainLost/Unknown`；⛔ **无 `Inert`** ✓ |
| 6 | `Abandon` 日志**四要素 ＋ 容 null** | ✅ | `Debug.Log($"[TaskScheduler] Abandon {task?.type} → npcId {npcId} reason={reason}")` |
| 7 | 5 调用点 reason 映射 | ✅ | `External/Dead/Unreachable/SourceInvalid` ＋ `:591` **取自表项** |
| 8 | `stale` 元组化 ＋ 惰性标 `Inert` | ✅ | `List<(int id, AbandonReason reason)>`；惰性 6 处注释 `Inert（惰性）`；⛔ 未删码 |
| 9 | 探针吸附 | ✅ | `SpawnWorker` 首行 `pos = SpawnPosSnapper.SnapWorld(pos, "hh319_worker");`（带 `context` ✓） |
| 10 | 红线 | ✅ | 3 文件；⛔ 未 push；⛔ 未代提交账本；报告自行 commit |

---

## 二 · ⛔⭐⭐ 判据 2 的「达成」被**原始日志**推翻（本端决定性取证）

执行端报「`完成 WaterHaul` ＝ **2 条** ⇒ 达成」。**本端直读 `Logs/hh319_u16/hh319_u16_15x.log`（4.9 MB）** ⇒ ⛔ **这 2 条不是真搬运**：

```
f1996  [HH319U16·complete] type=WaterHaul npc=21 第1次 在册=False Istask=False
f1996  [HH319U16·seq] npc=21 站位=(-5.92,40.88) dest=(6.00,40.96) 距dest=0.00   ← 站在【水井】·⛔ 非农场
f2010  [HH319U16·seq] npc=21 state=Working 在册=True 任务=WaterHaul 站位=(-5.76,40.80)
f2011  [HH319U16·trans] npc=21 Working|在册=True → None|在册=False              ← ⭐ 从 Working 直落 None
```

⭐ **铁证**：完成瞬间工人在**水井**、态＝`Working`，且 `·trans]` 显示跃迁**`Working → None`**、⛔ **从未经过 `MovingToDest`**。
⇒ 这 2 条落的是 `TaskScheduler` 的 **`Working` 期「装载失败 ⇒ 直接 `Complete`」**分支（水井仓空／装载未命中），**水未到农场、未卸货、未到账**。
⇒ ⛔ **判据 2 「完成 > 0」＝假达成**（计数 > 0 但来源分支错）。

⚠️ **同时反向确认归因**：真到达 `MovingToDest` 的 **9 条全部 `Timeout`**，且读数形态一致 ——

```
[HH319U16·abandon] type=WaterHaul npc=20 reason=Timeout | pfState=Idle fails=0 站位=(-5.92,41.04) dest=(-5.92,40.88) 距dest=0.16
```
⇒ 工人**停在水井边**、`pfState=Idle`（**没在走**）⇒ ⭐ **`MovingToDest` 段无位移驱动** ⇒ 30 秒 `Timeout` ✓✓ **归因成立**。

---

## 三 · 判据 1／5 随之否决

- **判据 1（靶例）** ⇒ ⛔ **未达成**：E5「转 `MovingToDest` 距农场 12.08」**之后无到达**（9 条 `Timeout` 为证）；无卸货／到账读数。
- **判据 5（到达态冻结）** ⇒ ⛔ **口径不成立**：执行端写「⛔ 未出现无往返的提前 `Complete`」—— ⚠️ 而 §二 那 2 条**正是**"无往返的提前 `Complete`"。
  ⚠️ 但须分清：**成因不同** —— 那 2 条是「装载失败 ⇒ 完成」的**既有分支**，⛔ **不是**加料 B 要防的「`ArrivedAtFocus` 冻结 ⇒ 恢复瞬间误触 `HarvestCarry`」。
  ⇒ **裁定**：判据 5 的**鉴别力对象未被反证**，但其**读数口径不成立** ⇒ **重写口径**：只判「是否存在**由 `ArrivedAtFocus` 误触**引起的 `Complete`／`HarvestCarry`」，**且必须与 `·trans]` 的跃迁来源配对**。

---

## 四 · ⛔ 本端认账：`D815` 的裁决漏了 `MovingToDest` 段

案① 的论证我只覆盖了 **`MovingToSource` 段**（`Dispatch` 里 `NavigateToSource:408-418` **直接设源路径** ⇒ 让位不伤它），⛔ **漏了 `MovingToDest` 段**：该段的位移**从来只有 Executor 一条路**（`InjectCarryStimulus:1068-1078` **只注刺激**）⇒ 让位一成，该段**立刻失去位移驱动**。
⇒ ⭐ **本端裁决漏项，认**（`D815` §一 未枚举"被收回的驱动原先承担的全部职责段"）。

---

## 五 · 归因裁定

| 项 | 判 | 依据 |
|---|---|---|
| 执行端归因「`MovingToDest` 段无人设路径」 | ✅ **成立**（本端独立证实） | `TaskScheduler` 内 `SetDestination` **全库仅 `:417` 一处**（在 `NavigateToSource` 内）；`InjectCarryStimulus` **无 SetDestination**；`TaskStimulus.FocusType => WorkPosition`（`StimulusTypes:118`）＋ `L2PostureDecider:172` ⇒ 改前靠 Executor 的 `MoveTowards` 提供位移 |
| ⚠️ 「`WaterHaul` 完成系**顺路补偿**（被别的派发带到终点）」 | ⛔ **定性勘正** | **不是**"顺路到达终点"（那也算真到达）⇒ 实为 **`Working` 期装载失败 ⇒ 直接 `Complete`**（§二 铁证）⇒ ⭐ **「偶然成分」的**方向**判反了** |
| 「案① 让位是回归成因」 | ✅ 成立 | `HH316 §B` 基线（`_161202`）`箱余={}` · 判据4=**True**；本批（`_234808`）`箱余={Stone:4,Wood:6}` 跨 4000 帧恒定 · 判据4=**False**；且本批 `在途:W0/S0/G3` ⇒ **装载成功、搬运段不动**（⛔ 非装载失败） |

---

## 六 · 修法裁定

### 6.1 ⭐ **准 (a)** —— 任务层在 `MovingToDest` 段**也设路径**（与 `NavigateToSource` 对称）

### 6.2 ⛔ **(d) 否决**（执行端倾向 (a)＋(d) ⇒ **只取 (a)**）
`TaskMoveYield` 让位若**仅 `Working` 态** ⇒ `MovingToSource` 段重新交给 Executor ⇒ ⭐ **`甲′-b` 复发**（焦点是**别的任务源**（如 `Production` 指向农场）⇒ `Execute` ⇒ `NavigateTo(dest)` ⇒ `dest` 被覆盖 ⇒ 30 秒到不了井）—— 这正是 `D813` 实测的原始病征。
⚠️ 即 **(d) 是执行端自己已否决的 (b) 的**镜像**（(b)＝延后置位 ⇒ `MovingToSource` 暴露；(d)＝收窄让位 ⇒ 同一段暴露）⇒ ⛔ 同因否决。

### 6.3 ⛔ **(e) 否决（且其前提有误）**
执行端以为 `MovingToDest` 的**到达判定**也依赖 Executor。**实读纠正**：到达判定**本来就由任务层每 tick 做** —— `UpdateAssignedTasks` 的 `MovingToDest` 分支用 `Vector2.Distance(brain.transform.position, task.destPos) <= arrive` ⇒ `Complete`（⛔ 与 `ArrivedAtFocus` 无关，与 Executor 无关）。
⇒ ⭐ **(e) 想补的判定已存在** ⇒ ⛔ 不需新增机制；**只缺"位移驱动"** ⇒ **并入 (a)**。

### 6.4 (a) 的 **3 处限定**（施工必须遵守）

| # | 限定 | 依据 |
|---|---|---|
| ① | 落点用 **`pf.SetDestination(task.destPos)`**，⛔ **不做** `WorldToSubCoord`/`SubCoordToWorld` 微格换算 | `PathFollower.SetDestination:44` **自身已 `SpawnPosSnapper.SnapWorld` 吸附**（逐字在场）⇒ 语义等价、少一份拷贝 |
| ② | 放进 **`InjectCarryStimulus`**（每 tick 被 `:599` 续命分支调用）**是安全的** | `SetDestination:45-48` **同目标缓存**（`_state==Following && Distance ≤ DestEpsilonWorld ⇒ 不重寻`）⇒ 每帧同目标**零额外开销**（注释自陈"供 Executor 每帧同目标调用零开销"） |
| ③ | ⭐ **禁第三份拷贝** ⇒ 抽 `TaskScheduler` 内**私有 helper**（`EnsurePfAndSetDest(NPCBrain, Vector2)`），`NavigateToSource` 与 `InjectCarryStimulus` **共用** | 现存量：`TaskScheduler.NavigateToSource:413-417` ＋ 拟新增 ⇒ 会成第 2 份（`BehaviorExecutor.EnsurePathFollower` 为另文件）；⚠️ **须保 `NavigateToSource` 行为逐位不变**（微格换算留在原处，只把「确保 pf ＋ SetDestination」入 helper） |

⇒ **净增 ≈ 10 行**（helper ≈8 ＋ `InjectCarryStimulus` 内 1 调用 ＋ 签名/注释）。

---

## 七 · 判据 6／7 裁定（部分 ⇒ 修正批补齐）

| 判据 | 现状 | 裁定 |
|---|---|---|
| 6 无泄漏（**逐路径**） | 观测到 `Unreachable`／`Timeout`／`Dead`（3/6） | ⭐ **泄漏面已由「判据 3 不变量 差=0 × 44 条」独立覆盖** ⇒ ✅ **无泄漏成立**；⚠️ 「各 reason 是否被走到」属**覆盖度** ⇒ 修正批补 |
| 7 日志（5 调用点各 ≥1 条） | 1328 条日志 · `Timeout×353`／`Unreachable×974`／`Dead×1` | ⚠️ **`:89`/`:95` 分流可区分 ✅**；⛔ `External`／`SourceInvalid`／`BrainLost` 未取得 ⇒ 修正批**定向触发**（见 §八-件C） |

⭐ 另：判据 3 的 `派发时刻 Istask=True 639 ／ False 974` **不是缺陷** —— ⭐ **本端复核成立**：`Dispatch` 内 `PathFailedEvent` **同步**触发 ⇒ `Abandon` 先复位 ⇒ `派发` 日志后读 ⇒ 读到 `False`；974 **恰等于** `Unreachable` 计数（`甲′-a`：`npc3/9` 站城堡格 · `U-18` 未修）⇒ **预期行为**，且**完成侧 True 0 ／ False 267 ＋ 不变量差=0** 才是配对成立的证明 ✓。

---

## 八 · 修正批（`U-16b`）范围与判据

### 件 A · 修法 (a)（按 §6.4 三限定）
### 件 B · 判据 2／5 **口径修正**（探针）
- 判据 2：`完成 WaterHaul` 须**与该任务的 `·trans]` 跃迁配对** ⇒ 只有 **`MovingToDest → None`** 才计入；⛔ 不得只给计数
- 判据 5：只判「由 `ArrivedAtFocus` 误触引起的 `Complete`／`HarvestCarry`」
- ⭐ 新增读数：**卸货到账**（农场仓水量增量）＋ `E6`
### 件 C · 判据 6／7 **reason 覆盖补齐**（探针直呼 public API ⇒ 须**声明"构造法"**）
- `External` ⇒ 直呼 `TaskScheduler.AbandonTask(npcId)`（`:180` **public**）
- `SourceInvalid` ⇒ 直呼 `TaskScheduler.OnBuildingDied(source)`（`:207` **public**）
- `BrainLost` ⇒ ⚠️ 若不可稳定构造 ⇒ **允许标「未观测 ＋ 理由」**（⛔ 不得无理由留白）

### 判据（修正批）
1 ⭐⭐ **靶例**：`WaterHaul` 端到端 **且** `·trans]` 出现 **`MovingToDest → None`** ＋ **农场仓水量增**（卸货到账）
2 ⭐ `完成 WaterHaul > 0` **且来源分支 ＝ `MovingToDest` 到达**（⛔ 非 `Working` 装载失败）
3 ⭐⭐ **`HH316 §B` 复绿**：`箱余={}` ＋ 判据4=**True**（本批红 ⇒ 必须转绿）
4 回归：`HH315`（`M1-C`）／`HH316` 全段／`HH317`／`2_20B_M7`（六轮）—— ⚠️ 本轮 `HH315`／`M7` **尚未跑**
5 reason 覆盖 ≥5 类（含 `External`／`SourceInvalid`）
6 存档往返逐值
7 ⛔ **`U-17`／`U-18`／`U-19` 仍不得混入**（降派发频率／改落位 ⇒ 污染读数）

---

## 九 · 状态

- ⛔ **`U-16` 不销号**（判据 1／2／3（HH316 面）未达）
- ⛔ **`F-1` 仍不销号**（`3b` 端到端未达）
- ⭐ 本批**件 1~3 ＝ 通过**（作为 `U-16b` 的基线保留）
- ⚠️ 证据面：`Logs/hh319_u16/`（4.9 MB）／`Logs/hh316_u2/`（本批 `_234808` vs 基线 `_161202`）⇒ ⚠️ `Logs/` 被 `.gitignore` ⇒ **只在磁盘**
