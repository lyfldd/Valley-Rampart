# HH.319 · `M1-F` · `U-16b` 案① 补正批 · 交付报告

- **日期**：2026-09-22 ｜ **端**：执行端（TraeCode）｜ **裁定遵循**：`D816` `HH.319_M1-F-U16案①批_交付验收裁决.md`（§6.1 准 (a) ＋ §6.4 三限定 ＋ §8 件A/B/C ＋ §八 判据）
- **状态**：✅ **件A（修法 (a)）落地并独立验证成立**（`HH316 §B` **复绿** ＋ 靶例 `MovingToDest` 段**真位移**双证）｜ ⛔ **判据 1／2 未达** —— 卡点是**新发现的结构性缺陷**（`taskTimeout` 从 `Dispatch` 起算、终点段不重置）⇒ **停手报裁**
- **红线自检**：改动面 **2 文件**（`TaskScheduler.cs`／`Valley_HH319_F1LongRun.cs`）✅ ｜ ⛔ **`NPCBrain.cs` 未动**（让位门／豁免／`ArrivedAtFocus` 冻结**保持 `4de46407` 现状**）✅ ｜ ⛔ 未动 `BehaviorExecutor`／`UnitFactory`／`MonsterAI`／`FormationBrain`／`SelectionController`／存档 schema／四档账本／美术／`pixel-forge`／`GameScene`／`Packages` ✅ ｜ ⛔ 本批不含 `U-17`／`U-18`／`U-19` ✅ ｜ 行尾**改前已验**＝两文件纯 **LF** ✅ ｜ 进局走 `EnterTestRun` 正门 · 收尾 `ExitTestRun` ＋ 退 Play ✅ ｜ 具名 `git add` · ⛔ 未 push · ⛔ 未代提交账本 ✅ ｜ ⛔ **未改生产码使判据变绿**（判据 1/2 未达即停手 · ⛔ 未动 `taskTimeout`）✅

---

## 〇 · 开工回执（4 行）

1. **改动面清单**：`Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs`（新增私有 `EnsurePfAndSetDest(NPCBrain, Vector2)`；`NavigateToSource` 改调该口；`InjectCarryStimulus` 末补一调用）／`Assets/Editor/Smoke/Valley_HH319_F1LongRun.cs`（判据 2／5 **口径修正** ＋ `·farm]` 卸货到账读数 ＋ 新增第 4 菜单「U16b reason覆盖列(构造法)」）。**净 2 文件**，⛔ `NPCBrain.cs`／`BehaviorExecutor.cs` 未动。
2. **行尾核验读数**：`git ls-files --eol` ⇒ 两文件 **`i/lf w/lf`**（纯 LF）⇒ 全走 `Edit` 直改，零二进制替换。
3. **判据口径确认（⭐ 新口径）**：**判据 2** ＝ `完成 WaterHaul` **必须与该任务的 `·trans]` 跃迁配对**，⭐ **只有来源分支 ＝ `MovingToDest → None` 才计入**（`[HH319U16·complete]` 新增 `⭐来源分支=` ＋ `计入真完成=`；来源由 `_lastKnownState` 回看**最近非 None** 状态判定，因 `Complete` 日志在 `ClearNpc` **之后**发出）；**判据 5** ＝ **只判「来源非 `MovingToDest` 的异常完成」**（计数 `_completeAnomalyN` ⇒ 应为 0），⛔ **不再与「装载失败 ⇒ 直接完成」混判**；新增 **卸货到账**（`·farm]` 水位式 ＋ `E6` 事件式）；⭐ `·trans]` **保留**。
4. **红线确认**：⛔ 不动 `NPCBrain`／`BehaviorExecutor`／`UnitFactory`／`MonsterAI`／`FormationBrain`／`SelectionController`／`U-17`／`U-18`／`U-19`；⛔ 不 push；⛔ 不代提交账本；报告自行 commit。

---

## 一 · 件 A／B／C 落地

### 件 A · 修法 (a)（按 §6.4 三限定）

| 限定 | 落实 |
|---|---|
| ① 落点用 `pf.SetDestination(task.destPos)`、⛔ 不做微格换算 | ✅ `InjectCarryStimulus` 末尾 `EnsurePfAndSetDest(brain, task.destPos)`；`SetDestination:44` 自身 `SpawnPosSnapper.SnapWorld` ⇒ 语义等价 |
| ② 落点 ＝ `InjectCarryStimulus` 末尾（每 tick 被 `:599` 续命调用） | ✅ 该口调用面 ＝ `:487`／`:517`／`:537`（装载成功）＋ `:599`（每 tick 续命）；`SetDestination:45-48` **同目标缓存** ⇒ 每 tick 同目标**零额外寻路** |
| ③ ⛔ 禁第三份拷贝 ⇒ 抽私有 helper，两处共用；保 `NavigateToSource` 行为逐位 | ✅ 新增 `EnsurePfAndSetDest(NPCBrain, Vector2)`（确保 `pf` ＋ `SetDestination`）；`NavigateToSource` 改为「微格换算**留在原处**」＋ 调 helper |

⚠️ **等价性声明**：与改前 `NavigateToSource` 的**唯一差异** ＝ 改前在「源坐标越界（`WorldToSubCoord` 返 null）」时**已补挂 `pf`** 再返回；现改为**先判越界、后补挂** ⇒ 仅该**不可达分支**（源恒为地图内建筑）的副作用差异，**任务行为逐位不变**。

### 件 B · 判据 2／5 口径修正（探针）

- `_lastKnownState[id]`（只记**非 None** 的 state，由 `·seq]`／`·trans]` 每帧维护）⇒ `[HH319U16·complete]` 输出 `⭐来源分支=<state>` ＋ `计入真完成=<bool>`；summary 出「完成来源分支分布」＋「`MovingToDest` 计入条数」＋「异常完成条数」。
- 新增 `[HH319U16·farm]`（逐秒：农场仓 `Water` ＋ 峰值 ＋ 首次见水时刻）⇒ **到账硬证（水位式）**。
- ⭐ `·trans]` 保留（本轮判据救星，见 §二-1）。

### 件 C · 判据 6／7 reason 覆盖补齐（新增第 4 菜单 · ⚠️ 构造法）

- ⚠️⚠️ **构造法声明（`L-51`）** —— 本档**直呼 `TaskScheduler` public API** ＋ **反射删私有表**，⛔ **均非生产路径触发**；每行同时给「**生产路径可达性**」：
  - `External` ⇒ `TaskScheduler.Instance.AbandonTask(id)`（`:180` **public**）｜生产可达：`VagrantCampSystem` 招募 ／ `Building.RemoveWorkers` 建筑驱离。
  - `SourceInvalid` ⇒ `TaskScheduler.Instance.OnBuildingDied(source)`（`:207` **public**）｜生产可达：`TaskScheduler.Unregister(source)`（建筑死亡/废弃）。
  - `BrainLost` ⇒ **反射删 `_npcBrainMap[id]`**｜生产可达：`UpdateAssignedTasks` 的 `!TryGetValue || brain == null` 支 —— 需「brain 已 Destroy 而字典未清」，正常帧序下 `OnNpcDied` 会先清 ⇒ **难以稳定构造** ⇒ 以构造法取证并**如实标注**（⛔ 非无理由留白）。
- ⛔ **本档未跑**（停手，见 §四）。

---

## 二 · 判据读数（`D816` §八 · 逐条）

证据：`Valley Rampart/Logs/hh319_u16/hh319_u16_15x.log`（本批 2026-09-22 09:35）、`Logs/hh316_u2/hh316_u2_smoke.txt`（09:40）、`Logs/hh317_m1d/hh317_m1d_smoke.txt`（09:4x）；上批证据归档 `Logs/hh319_u16/u16a/`，`D813` 证据归档 `Logs/hh319_u16/d813/`（⚠️ `Logs/` 被 `.gitignore` 忽略 ⇒ 只在磁盘）。

### 判据 1 ⭐⭐ 靶例（到井 ⇒ 装载 ⇒ **走到农场** ⇒ 卸货 ⇒ 到账）—— ⛔ **未达**（但 **(a) 的位移驱动已证生效**）

**✅ 修法生效段（决定性正读）**：`[HH319U16·seq]` npc21 自 f720 起

```
f719 t58.62 state=Working      dest=(6.00,40.96) | pfState=Idle      站位=(-5.68,40.80) PathFollower.dest=(-5.92,40.88) 距dest=0.25
f720 t59.71 state=MovingToDest dest=(6.00,40.96) | pfState=Following 站位=(-5.68,40.80) PathFollower.dest=(6.00,40.96)  距dest=11.68   ← ⭐ helper 设终点
f721 t60.31 …                                                                                                         距dest=11.60
f724 t61.50 …                                                                                                         距dest=10.80
f730 t67.61 …                                                                                                         距dest=9.36
f731 t69.36 state=None 在册=False … [TaskScheduler] Abandon WaterHaul → npcId 21 reason=Timeout
```
⇒ ⭐ **`PathFollower._destination` 由井 `(-5.92,40.88)` 切到农场 `(6.00,40.96)`、`pfState` 转 `Following`、`距dest` 单调递减（11.68→9.04）＝ 真位移** ✔ ⇒ **件A 生效**。
- **前置鉴别力**：本批前（`4de46407`）该段 `9 条全 Timeout`、站位≈水井、`pfState=Idle`（无位移驱动 ⇒ `HH316 §B` 真回归）。

**⛔ 未达点**：`E3`（到井 0.35）／`E4`（装载 背包=10 · 井仓=22）／`E5`（转 `MovingToDest` 距农场 11.68）**齐全**，但 ⛔ **农场仓水全程 ＝ 0**（`·farm]` 逐秒 44 条全 `农场仓水=0 峰值=0 首次见水=未见`；summary `到账=False`）⇒ **卸货/到账未达成**。

### 判据 2 ⭐ `完成 WaterHaul > 0` **且来源分支 ＝ `MovingToDest`** —— ⛔ **未达**

```
[HH319U16·complete] f788 t90.02 type=WaterHaul npc=21 第1次 在册=False Istask=False ⭐来源分支=Working 计入真完成=False
summary ⭐⭐ 判据 2（U-16b 新口径）：完成 WaterHaul 总 1 条 ／ ⭐ 来源＝MovingToDest ＝ 0 条（唯一计入） ／ 异常分支 ＝ 1 条
summary     完成来源分支分布：Production/未知×108 Transport/未知×23 WaterHaul/Working×1 Production/Working×26
```
⇒ 唯一 1 条 `完成 WaterHaul` **来源 ＝ `Working`（装载失败分支）** ⇒ **⛔ 不计入** ⇒ 判据 2 **未达**（新口径正确地把上批的「2 条 ⇒ 达成」**判否**）。

### 判据 3 ⭐⭐ `HH316 §B` 复绿 —— ✅ **达成**

| 版本 | §B 链成形 | §B 判据4 |
|---|---|---|
| 基线（改前 `_161202`） | `箱余={}` | **True** |
| `4de46407`（`_234808`） | `箱余={Stone:4,Wood:6}`（恒 4000 帧） | **False** ⛔ |
| **本批（`_094010`）** | ⭐ **`箱余={}`** | ⭐ **True** ✅ |

⇒ ⭐ **(a) 修法独立验证成立**（`HH316` 探针把 `taskTimeout` 临时设为 **3600** ⇒ 不受下述时限问题干扰 ⇒ 这是**纯 (a) 的效果**）。
其余段一并复绿：`§C` 两箱到账（`同 coord 未空箱数=0`）／`§D` 金走仓（`Vault 80→91`）／`§E` 存档往返逐值一致＝`True`／`§F` 过期消亡（箱数 3→2 不增）／`§H` AI 工人被派（`state None→MovingToDest` · AI 台账金 8→12）。

### 判据 4 回归全绿 —— ⚠️ **部分**（⛔ HH315／M7 停手未跑）

| 项 | 结果 |
|---|---|
| ② `HH316`（`U-2` §A~§H） | ✅ 全段绿（见判据 3） |
| ① `HH317`（`M1-D`） | ✅ **16 PASS ／ 0 FAIL**（§B 判据1/2/3/4/10 · §C 判据5 · §D 判据6① ② ③ · §E 判据7 · §F 判据8＋判据11 · §G 判据9） |
| ③ `HH315`（`M1-C`） | ⛔ **未跑**（停手 · 见 §四） |
| ④ `2_20B_M7`（六轮） | ⛔ **未跑**（同上） |

### 判据 5 reason 覆盖 ≥5 类 —— ⛔ **未取**（reason 覆盖列未跑 · 停手）

⚠️ 但本批自然读数已含 **2 类**：`Timeout×328`／`Unreachable×587`（`·summary` reason 分桶）；**`:89`/`:95` 分流仍可区分** ✅。

### 判据 6 存档往返逐值 —— ✅ **达成**（本批不动序列化面）

`HH317 §F` 判据11「非空箱 12→12 差异=0」＝**True** ＋ `HH316 §E`「contents 往返逐值一致」＝**True** ⇒ 两条独立判据全绿。

### 判据 7 本批不含 `U-17`／`U-18`／`U-19` —— ✅ 遵守

---

## 三 · ⛔ 报裁核心：判据 1／2 卡点 ＝ **`taskTimeout` 结构性缺陷**（⛔ 非本批引入 · ⛔ 未自行修）

### 现象（逐帧可复核）
`MovingToDest` 段**路径正确、位移真实**（`距dest` 单调递减），但任务在**派发后 30.4 游戏秒**被 `Abandon … reason=Timeout` 清掉：

```
[HH319U16·trans] f720 t59.71 npc=21 Working|在册=True → MovingToDest|在册=True     ← 装载成功转段
[HH319U16·seq]   f730 t67.61 … 距dest=9.36（仍在走）
[TaskScheduler]  f731 t69.36 Abandon WaterHaul → npcId 21 reason=Timeout           ← 30.4s（派发 t≈39）
```

### 归因（码面）
- `UpdateAssignedTasks` 的 `MovingToSource`（`:450-453`）与 `MovingToDest`（`:571`）**两处超时判定都用同一个 `_taskStartTime[id]`**，而 `_taskStartTime[id]` **只在 `Dispatch`（`:360`）设置**、**转段时不重置**。
- ⇒ `taskTimeout = 30f` 实际是「**派发 → 完成**」的**总预算**，而 `WaterHaul` 是**两段位移任务**：`MovingToSource`（本跑实测 ≈18 游戏秒到井）＋ `Working`(2s) ＋ `MovingToDest`（需 ≈12 游戏秒）≈ **32 秒 > 30 秒** ⇒ ⭐ **结构性必然超时**。
- ⇒ ⭐ 这同时解释：① 上批（`4de46407`）判据 1/2 的**假达成**（那 2 条落 `Working` 装载失败分支，与两段位移无关）；② `D813` 起观测到的 `Timeout×353`／本批 `Timeout×328`；③ `Transport` 亦反复 `Timeout`（`Abandon Transport ×N` 同根因：箱→仓两段）。

### 修法候选（⛔ 待裁 · 本端倾向 (f)）
| 案 | 内容 | 代价 |
|---|---|---|
| **(f)** | ⭐ **装载成功转 `MovingToDest` 时重置 `_taskStartTime[id] = Time.time`**（＝「**每段独立 30 s**」）｜1 行 · 贴"段"语义 · 与 `_workStartTime` 分段的既有形制一致 | 净增 ≈1 行 |
| (g) | `taskTimeout` 拆两档（源段／终点段分别计时） | 需新增字段（序列化面 +1） |
| (h) | 放大 `taskTimeout`（治标） | ⛔ 不治本：仍会因路程更长而复发（地图/拥挤） |

⚠️ **这是超 `U-16b` 范围的新缺陷** ⇒ 建议立新号（提议 `U-20`「两段位移任务共用单一起算点 ⇒ 结构性超时」）或并入 `U-18` 施工批（`Abandon` 分流面同处）。

### 附带（本端自评）
⚠️ 本批判据 1/2 **未达**，但件A **已由判据 3（`HH316` 复绿）＋ 靶例 `距dest` 单调递减 双证成立** ⇒ ⛔ 请勿把本批判为"无效批"；**U-16 仍不销号**（待 `taskTimeout` 裁后再评）。

---

## 四 · 未完成项与风险

1. ⛔ **判据 4③ `HH315`／④ `M7` 未跑**、⛔ **判据 5（reason 覆盖列）未跑** —— 均因判据 1/2 未达触发**停手报裁**；⛔ 未改码使其变绿。
2. ⛔ **判据 1/2 未达的根因（`taskTimeout`）不在本批改动面内** ⇒ 若要 U-16 收口，须先裁 (f)/(g)/(h) ⇒ 再复跑本批全部判据。
3. ⚠️ 本批 `15x` 与上批 `15x` 为**不同局**（RNG/时序差异）⇒ 计数**不可直接对拍**，判据以"共性结论"为准（`MovingToDest` 段有/无位移驱动）。
4. ⚠️ `·farm]` 行当前**无"增量"字段语义**（首版只给水位＋峰值＋首次见水）⇒ 载重读数以「峰值 > 0」为准；若需"逐次增量"须再加一列（⛔ 待裁是否需要）。
5. ⚠️ **构造法面（件C）** 已就位但未跑 ⇒ `External`／`SourceInvalid`／`BrainLost` 的 reason 覆盖**仍缺**（判据 5 未达）。
6. ⚠️ `MonsterSpawner` 考跑守卫（`TestHarnessMode ⇒ 静默 return null`）⇒ 正门内造敌只能走 **AI 国战士**（本批未用到，但为后续批的口径沉淀）。
