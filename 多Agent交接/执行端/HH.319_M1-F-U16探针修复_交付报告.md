# HH.319 · `M1-F` · `U-16` 取证探针修复批（P1~P3）· 交付报告

- **日期**：2026-09-21 ｜ **端**：执行端（TraeCode）｜ **裁定遵循**：`D812`（Q4 准：先修探针 ⇒ 落盘复跑 ⇒ 再定生产改法）
- **状态**：✅ **件 1~3 全落地 · 编译 0 error · 三跑落盘 · 判据 1~5 全部留档取得** ⇒ ⭐ **甲′ 由"唯一自洽假说"升为"已证"**（并**细化为两个失败模式**）
- **红线自检**：⛔ `Assets/_Game/**` **一行未动**（`git diff --stat -- …/_Game` ＝ **空**）✅ ｜ 改动面 ＝ `Assets/Editor/Smoke/Valley_HH319_F1LongRun.cs` **单文件** ✅（＋**连带补入**其 `.meta`：该 `.meta` 自 `D809` 起一直 `untracked`，本批**声明并一并纳入**，属 `.meta` 同步纪律补账，⛔ 非新增资产）｜ ⛔ 未动既有判据语义（口径演进**已单列登记**，见 §一-D）✅ ｜ 行尾**改前已验** ＝ 纯 **LF**（Edit 直改）✅ ｜ 进局走 `TestHarnessApi.EnterTestRun` 正门 · 收尾 `ExitTestRun` ＋ 退 Play（editor 状态已核：`playMode=stopped` · `scene.dirty=false`）✅ ｜ 具名 `git add` ✅ ｜ ⛔ 未 push ✅ ｜ ⛔ 未代提交策划端账本 ✅

---

## 一 · 件 1~3 落地

### 件 1 · P1 采样锚定（消灭"对象未锚定"）

- ⛔ **弃用**「距井最近者 `nearest`」当锚；改为**按「被派 npcId」逐帧打点**（`[HH319U16·seq]`，每帧一行一 npc）。
- 被派 npcId **两路取锚**：① 每帧扫 `_npcTaskMap` 的 `WaterHaul` 条目（抓得到"活到采样帧"的）② 在 console 钩子里抓 `[TaskScheduler] 派发 … type=WaterHaul` 日志（⭐ 抓到 ① **抓不到**的"派发帧内即被清"那批——本轮的关键）。两路并集 ＝ `_tracked`（上限 6，防日志爆炸）。
- `E2` **保留**（判据语义未改），并新增 **每帧命中/未命中计数**：`E2 命中帧` ／ `E2 未命中帧（有被派 npc 但无一在册）`，落 summary。
- 旧「距井最近者」序列**降为旁读**（`[HH319U16·nearby]`，每 1 真实秒一行），⛔ 不再作判据/归因锚。

### 件 2 · P2 直接证甲′（反射只读）

每个被派工人输出一行：

```
[npcId, 在册(taskMap), 在态(stateMap), PathFollower._state, _consecutiveFails, _destination,
 站位坐标, 站位格可走性(+成因), dest格可走性, 距dest, ⭐A*复演=Status]
```

- 私有面反射**只读**：`PathFollower._state`(`PathFollower.cs:19`·经公开属性 `State`)／`_consecutiveFails`(`:23`)／`_destination`(`:17`)。
- `IsSubWalkable` ＋ ⭐ **`IsObstacleSub` 成因鉴别**（`GridSystem.cs:254 / :313`，两个都是公开读口、零反射）：`False（占格物阻断：<名>）` ／ `False（地形不可走）`。
- ⭐ **`A*复演`**：现场调 `PathfindingService.FindPathImmediate(单位位, SpawnPosSnapper.SnapWorld(SubCoordToWorld(WorldToSubCoord(dest))))` —— **与生产 `NavigateToSource` 同调用面**（纯查询、无副作用）⇒ **一条读数即可区分甲′（`Unreachable`）vs 乙（`Ready` 但被拉走）**。

### 件 3 · P3 console 全量落盘（第一优先）

- `Application.logMessageReceived` 镜像 ⇒ `Logs/hh319_u16/hh319_u16_{tag}.log`（`StreamWriter` ＋ `AutoFlush`，收尾 `# 封存`）。
- `EventBus.Subscribe<PathFailedEvent>` **只读计数**（按 npcId 分桶）＋ 每条事件一行 `[HH319U16·pathfail]`（含 `dest`、`在册`、`PathFollower` 读数）。
- 结构化抓取 `[TaskScheduler] 派发`／`完成` ⇒ `[HH319U16·dispatch]`／`·complete]` 行（含**同帧快照**）。
- ⚠️ **落盘方式"报选"**：项目**已有先例**（`Valley_P1_Observer.cs:109-119` ＝ 白名单 tag ＋ Error/Exception 全量镜像；`Smoke_2_22P0.cs:781-785` ＝ 整篇 `_log` 写盘）。本批**沿用先例并加严**：
  **白名单前缀**〔`[TaskScheduler]` `[调度中心]` `[HH319` `[NPCBrain]` `[EventBus]` `[SpawnPosSnapper]`〕**＋关键词**〔`WaterHaul` `PathFailed`〕**＋ `Error/Warning/Exception/Assert` 全量**。
  ⛔ **未用字面"全量 console"**（理由：每行 IO ＋ 噪音淹没判据行）—— 但**判据所需的 `[TaskScheduler]` 全部行（派发/完成）、`PathFailed`、探针 `seq` 均已覆盖**（见 §二 取证）。
- ⚠️ 本节**只写文件、不再 `Debug.Log`**（防 `logMessageReceived` 重入）。

### 件 1~3 附加（口径演进三件套登记 · 单列）

| # | 演进 | 类型 | 影响 |
|---|---|---|---|
| ① | seq 行由「距井最近者」⇒ **按被派 npcId** | **口径演进** | ⛔ 旧读数 `st=None · 距井 5.25` **不可与新读数直接对拍**；判据语义未变 |
| ② | 新增 `[HH319U16·dispatch/seq/trans/pathfail/summary/nearby/track/place]` 系列行 | **只增不改** | 零影响 |
| ③ | `E2/E3/E4/E5/E6` 与汇总判定 | **一字未改** | 零影响 |
| ④ | 【二跑补读】`Walkable()` 由 `True/False` ⇒ **带成因鉴别** | **只增信息** | r1 存档为 `hh319_u16_15x_r1.log`；两跑 `站位格可走=False` 同口径扩展 |
| ⑤ | `[HH319U16·dispatch] 第N次` ＝ 该 npc 的**总派发序号**（含所有任务类型），⛔ 非 WaterHaul 专数 | 语义说明 | WaterHaul 专数见 summary 计数 ＋ 原始行 grep |

---

## 二 · 判据读数（⛔ 全部留档 · 无一条来自"读 console 后口述"）

### 二-0 三跑清单（证据路径）

| 跑 | tag | 观测 | 帧数 | 游戏秒 | 派发 `WaterHaul` | 完成 `WaterHaul` | E2 命中帧/未命中 | PathFailedEvent | 落盘 |
|---|---|---|---|---|---|---|---|---|---|
| r1 | `15x` | 45 真实秒 | 1186 | 696.5 | **64** | **0** | **0 / 1142** | **6013** | `Logs/hh319_u16/hh319_u16_15x_r1.log`（2.6 MB） |
| — | `1x` | 180 真实秒 | 8383 | 184.0 | **14** | **0** | **0 / 8144** | **28496** | `Logs/hh319_u16/hh319_u16_1x.log`（9.0 MB） |
| r2 | `15x` | 45 真实秒 | 969 | 693.2 | **35** | **0** | **256 / 653** | **4022** | `Logs/hh319_u16/hh319_u16_15x.log`（2.2 MB，含成因鉴别） |

### 判据 1 ⭐ 单一判别式 —— ✅ **`完成 WaterHaul` ＝ 0（三跑一致）** ⇒ **甲′ 坐实**

```
=== 完成 WaterHaul（原始生产行，[TaskScheduler] 前缀）===
r1 = 0 条 ／ 1x = 0 条 ／ r2 = 0 条
```
（对照：同期 `[TaskScheduler] 完成` 其它类型合计 **165 / 49 / 139** 条 ⇒ **`Complete` 路径本身工作正常**，⛔ 非日志缺失。）
⚠️ 陷阱提示：裸 grep 字符串 `完成 WaterHaul` 会命中**本批 summary 自身的文本**（r1 命中 2 条）⇒ 判定须用 **`[TaskScheduler] 完成 WaterHaul`** 前缀。

### 判据 2 ⭐⭐ 逐帧 state 时序 —— ✅ **"派发帧内即被清"已证（含同帧配对铁证）**

**（a）时序形态**（`[HH319U16·trans]`，npc34 · 1× 与 15× 两档一致）：

```
[HH319U16·trans] f359 t60.38 +2.59s npc=34 (首次) → None|在册=False     ← ⭐ 唯一一条跃迁行（1518 行 seq 全程同值）
[HH319U16·seq]   f359 …      npc=34 state=None 在册=False | pfState=Failed fails=58  站位=(-1.00,42.56) …
[HH319U16·seq]   …           （1142 行 1×15× 同构 · 站位恒 (-1.00,42.56) · fails 单调 58→1648）
```
⇒ `Assigned` 态**在探针可见范围内从未出现**（`Dispatch:358` 置 `Assigned` ⇒ `:365` 打日志之间即被清）。

**（b）同帧快照铁证**（r2 · npc34）：

```
[HH319U16·dispatch] f428 t87.38 type=WaterHaul npc=34 第19次 在册(taskMap)=False 在态(stateMap)=False
  | pfState=Failed fails=79 站位=(-1.00,42.56) 站位格可走=False（地形不可走） dest=(-5.92,40.88) dest格可走=True 距dest=5.20
  ⭐A*复演=Unreachable（单位位 → snap(dest微格) · 与 NavigateToSource 同调用面）

[HH319U16·pathfail] f428 t87.38 npc=34 第77次 dest=(-5.92,40.88) 在册(taskMap)=False 在态(stateMap)=False
  | pfState=Failed fails=79 …（同一帧 f428 · 同一游戏时刻 t87.38）
```

⭐⭐ **量化**：r2 的 35 条 `WaterHaul` 派发行中，**30 条**在同一 `frameCount`／同一 `Time.time` 上存在 npc34 的 `PathFailedEvent`（`dest=(-5.92,40.88)` ＝ **水井**，即 `NavigateToSource` 的 `SubCoordToWorld(WorldToSubCoord(SourcePos))` 落点）。

**码链闭合（`Dispatch` 内 · 逐行可验）**：
`TaskScheduler.cs:357` 置 `_npcTaskMap[id]` → `:358` 置 `_npcStateMap[id]=Assigned` → `:364 NavigateToSource`（`PathFollower.SetDestination` ⇒ **同步 A\*** `FindPathImmediate`）→ 返回 `Unreachable` ⇒ `PathFollower.FailOnce()`（`:175-192`）`_consecutiveFails≥3` ⇒ **`:183` `EventBus.Publish(PathFailedEvent)` 同步** ⇒ `TaskScheduler.OnPathFailed`（`:198-205`）⇒ `Abandon`（`:630-642`）⇒ `ClearNpc`（`:644-652`）**清掉两条目** → `:365` 才打「派发」日志。
⇒ ⭐ **"派发"日志存在 ≠ 任务在册**；记录在**同帧、同一次 `Dispatch` 调用内**已被清。

### 判据 3 ⭐ §四 缺口钉死 —— ✅ 首次派发时 `_consecutiveFails` **≥ 3（两档皆然）** ⇒ **甲′ 结论不变**

| 跑 | 依据行 | 读数 |
|---|---|---|
| r1 | `npc=34 type=Transport 首次派发时 _consecutiveFails=17 pfState=Failed 在册=False f331 t45.38` | **17 ≥ 3** |
| 1× | `npc=34 type=WaterHaul 首次派发时 _consecutiveFails=240 pfState=Failed 在册=False f703 t18.78` | **240 ≥ 3** |

⇒ ⛔ **"E2 未命中"不是 console 淹没**（本批已落盘：`E2 命中帧=0`，而 `E2 未命中帧=1142/8144`）⇒ **上一轮"记录在派发帧内即被清"的推论被独立证据确认**，归因**不翻转**。

### 判据 4 ✅ 两档对照 —— **同因**；倍率只改"真实耗时"，不改机制

| 维度 | 1× | 15×（r1） | 判 |
|---|---|---|---|
| 游戏时间 | 184.0 s | 696.5 s | — |
| `WaterHaul` 派发密度（每**游戏**秒） | 0.076 | 0.092 | ⭐ 同量级 ⇒ **同因**（倍率不影响"是否派/是否清"） |
| npc34 状态 | 全程 `None·在册=False` · 站位恒 `(-1.00,42.56)` · `A*复演=Unreachable` | 同 | 同因 |
| `完成 WaterHaul` | 0 | 0 | 同因 |
| PathFailedEvent／游戏秒 | 154.9 | 8.6 | ⚠️ 差 18× —— **已解释**：该事件是**每帧驱动**（Executor 每帧 `SetDestination`）；1× 帧率 45.6 fps vs 15× 1.7 fps ⇒ 帧数差即失败数差，⛔ 非机制差异 |

### 判据 5 ✅ `PathFailedEvent` 计数（按 npc）＋ `_consecutiveFails` 末值

| 跑 | PathFailedEvent 按 npc | 合计 |
|---|---|---|
| r1 | npc9×1683 npc34×1646 npc3×1535 npc30×705 npc32×444 | **6013** |
| 1× | npc9×8569 npc34×8534 npc3×8422 npc32×1502 npc33×1469 | **28496** |
| r2 | npc9×1464 npc3×1392 npc34×634 npc32×368 npc29×157 npc31×6 npc33×1 | **4022** |

末值（1×）：`npc=34 fails=8536 pfState=Failed 站位格可走=False（地形不可走） … A*复演=Unreachable`
末值（r2 · 活着的被派 npc）：`npc=35 fails=0 pfState=Following 站位格可走=True … A*复演=Ready` ／ `npc=21 fails=0 pfState=Idle … Ready` ／ `npc=22 fails=0 pfState=Following … Ready`

---

## 三 · 归因结论 —— ✅ **甲′ 坐实**，并**细化为两个失败模式**

### 甲′-a「困死型」：出发格不可走 ⇒ A\* 恒 `Unreachable` ⇒ **派发帧内 `Abandon`**

- 铁证读数：`站位=(-1.00,42.56) 站位格可走=False（地形不可走）` ＋ `dest格可走=True` ＋ ⭐`A*复演=Unreachable`；`_consecutiveFails` **单调增**（58→1648 / 240→8536）⇒ `SetDestination` **不复位** `_consecutiveFails`（`PathFollower.cs:42-52` 无重置；注释 `:189`「由外部 Stop/SetDestination 重置」**与码不符**）⇒ 累计 ≥3 后**每次派发都同帧被清**（无限空转）。
- `站位=(-1.00,42.56)` **＝探针 `SpawnWorker(anchor + (-1f,1.6f))`（`:62`）的出生点**（一-C 推断的 `距井 5.25` 由此得来，完全吻合）⇒ ⭐ **本批靶例的首因＝探针把工人 spawn 在不可走格上**（`UnitFactory.SpawnUnit` 不走 `SpawnPosSnapper`，见 §四-1）。

### 甲′-b「超时型」（⭐ 本轮新发现 · r2 才暴露）：路径可达但 **30 游戏秒内到不了井** ⇒ `taskTimeout` 放弃

r2 的 `E2 命中帧=256 ≠ 0` ⇒ **确有 `WaterHaul` 记录活过了采样帧**（被派 npc 扩至 `[34,35,21,22]`）。这三位是**健康工人**（`站位格可走=True` · `fails=0` · `A*复演=Ready`），其记录清除周期**精确等于 `taskTimeout`**：

| npc | 派发 → 清除（游戏秒） | 依据 |
|---|---|---|
| 35 | **30.98** / 30.83 / 30.72 / 30.41 | `trans` f1009 t504.21 → f1062 t535.19（余同） |
| 21 | **30.78** / 34.5 | f1124 t566.15 → f1187 t596.93 |
| 22 | **31.5** | f1277 t656.48 → f1331 t687.98 |

⇒ 排除 `Complete`（判据 1＝0）与 `PathFailedEvent`（三人 `fails=0`、不在 PathFailed 名单）后，**只剩 `TaskScheduler.cs:450-453` 的 `MovingToSource` 超时支路**（`taskTimeout=30f`，`:45`）⇒ 判定为**超时放弃**。
⇒ 与 `D808` §二-1「`WaterHaul` vs `Production` 刺激竞争」**同族**：`dest` 读数随时间指向**农场／其它任务点**（如 `npc35` 末值 `dest=(6.00,40.96)` ＝ **农场**）⇒ ⚠️ **指向"Executor/刺激面把工人拉走"**，但⛔ **本轮未做直接取证**（需 `BehaviorExecutor`/attention 读数）⇒ 列报待补。

### 与 `D812` 原结论的差异（如实列报）

- ✅ **不变**：`U-16` 归因仍是 **甲′**（寻路/到达失败 ⇒ 静默 `Abandon`）⇒ **F-1 搬水链逻辑本身无错**，`U-16` 不阻塞 `F-1` 销号的判断**维持**。
- ⚠️ **修订**：`D812` 把"记录在派发帧内即被清"表述为**唯一形态**；本批证明它**只对困死工人成立**，健康工人走的是**甲′-b 30 秒超时**形态 ⇒ 两者**并列**，"派而不执行"有**两个独立成因**。

---

## 四 · 新发现 / 列报（⛔ 本端不裁）

1. ⭐ **站位格不可走的两个成因（本批新增鉴别读数）**：
   - `npc34`：**地形不可走**（`False（地形不可走）`）⇒ 探针 `SpawnWorker` 未走 `SpawnPosSnapper` ⇒ **探针自污染**（建议下一批改吸附 —— ⚠️ 会改靶例 ⇒ 请裁）。
   - `npc3`／`npc9`：**占格物阻断** —— `False（占格物阻断：Building_castle_29_88）` ／ `（Building_castle_105_16）`，站位 `(-37.76,38.08)` ／ `(56.96,39.36)` ⇒ ⛔ **非探针造成**（远在 AI 国城堡格上）⇒ **世界生成/人口落位未吸附**的候选缺陷。
2. ⭐ **困死面远不止 `WaterHaul`**：三跑共见 **7 名工人**（npc3/9/29/31/32/33/34）长期困在不可走格，**6013~28496 次 PathFailedEvent** ⇒ 建议**独立立项**（严重度高于 `U-16`）。
3. ⚠️ **`WaterHaul` 实际优先级 ＝ `B`** —— 原始行 `[TaskScheduler] 派发 WaterHaul 任务 → npcId 34 @ (-6.00, 40.96)（优先级 B）` ⇒ 与既有记录／`D808` 的「**C 档**」**不符** ⇒ **勘正**（若按"C 档"做竞争分析会失真）。
4. ⚠️ **`Abandon` 全程无日志**（`:630-642`）＋ `NPCBrain.OnPathFailed` 对 `Worker/Porter` 直接 `return`（`NPCBrain.cs:369`）⇒ "派而不执行"**在 console 上完全不可见** —— 这是 `U-16` 长期无法定位的**根因之一**（建议 `Abandon` 加一条带原因的日志）。
5. ⚠️ `npc34` 在 r2 `t403.95` 起 `uc=null（已亡/被回收）` ⇒ 与 `U-15` 轮 `HH315` 观测到的「玩家工人持续衰减」同族。
6. ⚠️ **证据入库口径**：`Valley Rampart/Logs/` 被 `.gitignore:7` 忽略 ⇒ 三份留档**只在磁盘、不入库**（与 `hh317_m1d` 等先例一致）⇒ 有丢失风险，报告已固化全部关键读数。
7. ⚠️ 旁读口径提示：本批 summary 里 `E2 命中帧` 在 r1/1× 为 **0**、r2 为 **256** ⇒ 说明**"E2 是否命中"本身随派发对象而变**，⛔ 不可用单一跑次的 E2＝0 反推全局。

---

## 五 · 未完成项与风险

1. **甲′-b 的直接成因未取证**（Executor/attention 抢占面）⇒ 若裁决需要，须补一件读数（`BehaviorExecutor` 当前焦点目标／attention 最高刺激），**仍属 Editor 侧零生产改动**。
2. `npc3/npc9` 站城堡格的成因（世界生成 vs 建筑后放置）**未取证** ⇒ 属**新立项**范围。
3. 本批为 **探针修复批**：⛔ **未动任何生产码**，故 `U-16` 的**处置（生产改法）仍待裁决**（`D812` 已锁"先取证 ⇒ 再定改法"，本批完成取证侧）。
4. ⚠️ r2 为**口径演进后的补跑**，其计数（35 派发／4022 PathFailed）与 r1（64／6013）**不可直接对拍**（npc34 在 r2 中段死亡＋RNG/时序差异）⇒ 判据以"**共性结论**"为准（0 完成／同帧清除／≥3 fails／30 s 超时）。

## 六 · 结论

- 件 1~3：✅ **全落地** · 编译 **0 error** · 改动面 ＝ **1 个 Editor 文件**（`Assets/_Game` 零改动）。
- 判据 1~5：✅ **全部留档取得**；⭐ **判据 1＝0 · 判据 2＝同帧配对 30/35 · 判据 3＝≥3（17 / 240）⇒ 甲′ 坐实**。
- ⭐ 净增益：① 甲′ 由假说升为**已证**；② 新分离出**甲′-b（30 s `taskTimeout`）**形态并用帧距**量化**；③ 新增"站位格不可走**成因鉴别**"⇒ 分清**探针自污染**（npc34 地形格）与**生产候选缺陷**（npc3/9 站城堡格）；④ 建立 `Logs/hh319_u16/` 留档面（`D812` 的 P3 缺口已闭合）；⑤ 勘正 `WaterHaul` 优先级读数（B 非 C）。
- ⛔ 本批**不改生产**、不提处置方案 ⇒ **停手报裁**。
