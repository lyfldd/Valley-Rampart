# `HH.319` · `M1-G-1c`（收口续批）· 交付报告（⭐ **部分完成 · 停手待续**）

- **日期**：2026-09-22 ｜ **端**：执行端（TraeCode）｜ **依据**：`D827`（件C/D/E/F ＋ 报告）
- **基线对齐**：`git log --oneline -1` ＝ `2f9f562b`（策划端落账）；⭐ 本端代码面基线 ＝ **`1db3a34d`**（件G）｜`git status --short` ⇒ ⛔ **无本端之外的新代码改动**（脏项仅既有 `GameScene.unity`／`Packages`／`pixel-forge`／文档）
- **状态**：✅ **件C（11 处口径同步）＋ 件F（字段清理）已落 · 编译 0 error**（commit `92390c35`）｜ ⛔ **件D 探针／件E 跑局回归 未落** ⇒ ⭐ **判据一条未验 ⇒ 不判绿**
- **红线**：⛔ 零 `AI.Core` 改动 ✅ ｜ ⛔ 未 push ✅ ｜ ⛔ 未动 `GameScene.unity`（`O-14`）✅ ｜ ⛔ 未碰美术／`pixel-forge`／`Packages` ✅ ｜ ⛔ 未改 `taskTimeout` 值 · ⛔ 未新增 SO 字段 ✅

---

## 一 · 件C · 注释口径同步（改动清单 · 逐 文件:行 ＋ 改前/改后）

| # | 位置 | 改前（旧口径陈述） | 改后 |
|---|---|---|---|
| 1-2+10 | `StorageComponent.cs:267-277`（区块头 ＋ `IsReadyToHarvest` docstring） | 「…（`IsReadyToHarvest` 判据 ＋ `Harvest`/`HarvestCarry` 落点）」；消费者列 `ScheduleCenterStub:100/:118` ＋ `BuildingPanel:187/:405` | 区块头改「**在役落点仅 `Harvest()`**」；docstring **重写**：⭐ **现役消费者 ＝ 仅 Editor 探针**；生产面**不再据此灰化按钮** |
| 3 | `StorageComponent.cs:309`（`CanRulerAccept` docstring） | 「与落点一致（本类 `Harvest`/`HarvestCarry` 的去向）」 | 「与**在役落点 `Harvest()`** 一致」 |
| 4 | `StorageComponent.cs:341`（`PrimaryStoredType` docstring） | 「调用方 `HarvestCarry` 会先问国库能否收」 | ⚠️ 明注「**该调用方已随 `#40` 删除**」⇒ 现役同族守卫在 `Harvest()` 与链 A 落点 |
| 5 | `AIDebugSpawnController.cs:432` | 「…（`StorageComponent.HarvestCarry`）」 | 改「**两段式**（`LoadInventoryFromSource` ⇒ `UnloadInventory`）」 |
| 6 | `Valley_HH314_M1B_Smoke.cs:25`（**在役探针**） | 「§G …＋ `HarvestCarry` 真搬运口」 | 改「原真搬运口**已随 `#40` 删除** ⇒ 本档 §G 改走在役 `Harvest()`（读数口径已变 · ⛔ 不可与旧档对拍）」 |
| 7 | `BuildingPanel.cs:409-413` | 「启用（:187）与守卫（本行）同用 `IsReadyToHarvest` ⇒ 纯水井仓自动变灰（正确处理）」＝**事实错误** | ⭐ **整块合并重写**（⛔ 不再保留任何旧陈述）：判据 ＝ `TotalCount > 0`（启用／守卫）· **水井仓可点** · 「无可用仓」由**任务层 `Abandon`** 承担 |
| 8 | `ScheduleCenterStub.cs:16-17`（头注职责 1／2 条） | 「1. 搬运派发（3.3.5）…」「2. 防重复：`_transporting`」 | 均 `~~划删~~` ＋ 注明「随 `U-15` 根除项／件F 删除」 |
| 9 | `Valley_HH319_WaterAccount.cs:8-9`（头注） | 引**已删** `DispatchTransport` ＋ `HarvestCarry` | 改注为**历史复现档**（以在役 `Harvest()` 复现"写死玩家国库"的旧口径影响面）｜⭐ **实读后报**（见 §三-⑥） |
| 11 | `GameEvents.cs`（`UnitOccupationChangedEvent` docstring） | 无「覆盖范围」句 | ⭐ 补「**覆盖全部职业变更（含成长／招募）**」＋ 九处均走本事件（语义分组见 §二） |

⚠️ **行尾**：`GameEvents.cs` ＝ **CRLF** ⇒ 全部走 **python 二进制**（含一次 EOL 违规自纠：追加行误用 LF ⇒ 文件曾变 `w/mixed` ⇒ 已**全文件规范化为 CRLF**，终态复核 ＝ **`w/crlf`** ✅）；其余 6 个文件 ＝ 纯 LF ⇒ 用 Edit。

## 二 · `#42` 9 处清单 ＋ 语义分组（⭐ 为何"成长/招募"三处也发同一事件）

| 组 | 处 | 语义 | 是否需写 |
|---|---|---|---|
| **A 训练转职**（真转职） | `TrainingSystem:100`（训练完成 ⇒ `toOccupation`）／`:141`（取消/回退 ⇒ `Resident`） | ⭐ **正牌转职** | ✅ 须写 |
| **B 结算/建制** | `KingdomBrain:991`／`:1019`（AI 降级 ⇒ `Resident`）／`AbstractEconomySettlement:112`（⇒ `Vagrant` 流民化）／`KingdomFoundry:396`（⇒ `Worker` 实体化/招募） | AI 编成与经济结算 | ✅ 须写（同属"职业变更 ⇒ 下游配额/统计应感知"） |
| **C 成长/招募**（⚠️ 语义不同 ⇒ 单列） | `PopulationSystem:506`（`Child⇒Resident`）／`:532`（`⇒Worker`）／`VagrantCampSystem:251`（`Vagrant⇒Resident` 收编） | **成长／招募**（⛔ 非训练转职） | ⭐ **仍发同一事件** —— 理由：本事件语义 ＝ 「**该单位的职业字段发生变更**」（**状态变更通知**），⛔ 不区分"变更来源"；⭐ 分组只用于**消费方按需过滤**（如"只关心训练转职"的 UI 可筛 `from/to` 组合）⇒ ⛔ **不为来源另立事件类型**（否则 3 种事件 × 9 处调用，契约面爆炸） |

⇒ ⭐ **单点写入**：9 处**均不改**（事件由 `UnitController.SetOccupation` 单点发布 ⇒ 覆盖全部）。

## 三 · 报告要求项（逐条答）

**① 件C 改动清单** ⇒ §一（11 处逐 文件:行）✅
**② 豁免清单（逐条列名 ＋ 理由）**：

| 类别 | 逐条 | 理由 |
|---|---|---|
| 墓碑注 | `ScheduleCenterStub:66/:67/:86/:87/:93/:94`／`BehaviorExecutor:171/:172`／`TaskScheduler:742`／`StorageComponent:349`（＋本批新增若干） | **说明"已删/为何删"** ⇒ 属**迁移论证**，⛔ 非"按旧口径陈述现状" |
| 红线面 | `NPCBrain:728`（注释命中旧方法名） | ⛔ 红线「不动 `NPCBrain.cs`（让位门 `4de46407`）」⇒ **豁免** |
| 历史复现档 | `Valley_HH319_U15Probe:92/:97/:101`／`Valley_HH319_WaterAccount:9/:84/:85` | 探针**刻意复现旧落点**取证（止血批留档）⇒ 改名会**丢了取证意义**；已加"历史档"注 |
| 判据说明串 | `ChainAuditSpec:61`（字符串内命中） | 判据**说明文本**，⛔ 非可执行引用 |

⇒ ⭐ 判据 3 口径 ＝ **存活型引用（可执行代码）＝ 0** ＋ **在役源文件旧描述注 ＝ 0**（本批已达成，见 §一）；豁免面**逐条列名如上**（⛔ 非笼统"注释豁免"）。

**③ `#42` 9 处清单 ＋ 语义分组** ⇒ §二 ✅
**④ `UnitOccupationChangedEvent` 零订阅方 ⇒「为何现在发」**（`D824` §一-1 欠项）：
- ⭐ **契约要求**（`09` §十二 `:527-529`）「转职**补一条写入**」⇒ 本批**兑现契约欠项**（⛔ 非"为订阅方而发"）；
- ⭐ **备而未用是刻意的**：事件是**单点观测口** —— 下游（UI 刷新／AI 配额重算／统计）**按需订阅**，⛔ 本批**不预设订阅方**（防"为凑订阅方而加伪消费"）；
- ⚠️ **诚实标注**：**0 订阅 ＝ 当前无行为收益**（纯契约面兑现 ＋ 后续接入点就绪）。
**⑤ ⚠️ `ExecuteCompletion` Transport 支忽略 `UnloadInventory` 返回值（与到达分支 `Abandon` 处置不同）⇒ 差异说明**：
- **到达分支**（`MovingToDest` 到达）：卸货失败 ⇒ **`Abandon(DestFull)`**（⭐ 这一趟**目的未达成** ⇒ 释放工人 ＋ 记录）；
- **`ExecuteCompletion`**（`Transport` 完成兜底）：`UnloadInventory(brain, task)` **忽略返回值** ⇒ ⚠️ 差异**原因**：该处是 **`Complete` 前的"满背包兜底卸货"**（`DZ-072a` 根除死循环用），**任务本身已完成** ⇒ 其语义是"**顺手卸一下**"，卸不掉由**下一次装载前的先卸空出口**自愈（件6）；⛔ 若在此 `Abandon` 会**撤销已完成的 `Complete`**（自相矛盾）。
- ⭐ 结论：**差异是刻意的**，但 ⚠️ **本端建议**下批在注释中显式写明该理由（本批未改码 ⇒ 仅报告）。
**⑥ ⚠️ `Valley_HH319_WaterAccount` 探针逻辑是否仍成立（实读后报）**：
- 实读：该档以 `Harvest()`（原 `HarvestCarry`）**复现"落点写死玩家国库"**这一动作，测"**水是否会因该动作被取走 / 去哪**"；
- ⭐ **逻辑仍成立**（`Harvest()` 在役 且 同样写死玩家国库 id=0 ⇒ 被测量依然存在）；
- ⛔ **但结论面变窄**：原被测链（`DispatchTransport → HarvestCarry`）**已整段退役** ⇒ 其读数**只能当"手动收取口对水的影响"**，⛔ **不可当作"搬运链对水的影响"**（已加注 · 见 §一 第 9 项）。
**⑦ `AddGatherOverflow` 逐调用点三列表（`L-71` 实例 2）**：

| 调用点 | 是否传玩家（`kingdomId <= 0`）可达 | 该点口径 |
|---|---|---|
| 采集溢出（`ExecuteCompletion` Gather 支 · 2 处） | ✅ 可达（玩家工人采集） | 玩家 ⇒ `RulerController.ModifyResource` **入玩家国库**；AI ⇒ 就近仓 → 台账 |
| `UnloadInventory` 副产支（Crystal/FireOil · AI 专用） | ⛔ 仅 AI（`wkingdom > 0` 守卫） | ⇒ **台账**（AI 经济＝台账制） |
| `ResolveChestDest`／箱源卸货 | ✅ 可达 | 同上分流 |
| `ReturnOverflow`（搬料/搬水余量兜底） | ✅ 可达 | 同上分流 |
| ⭐ **本批改动** | — | `UnloadInventory` 的**两处 `AddGatherOverflow` 兜底已删** ⇒ 该函数**不再走**此口（满/无仓 ⇒ **留背包**） |

⚠️ ⇒ ⭐ **玩家面「满 ⇒ 入国库」仍存在于其余 5 处**（采集溢出/副产/箱源/搬料/搬水余量）⇒ ⛔ **`#41` 的"删满则入国库兜底"仅覆盖 `UnloadInventory` 一处**（契约字面如此）⇒ ⭐ **提请裁：其余 5 处是否也须同口径处理**（本端**未动**）。

## 四 · 件F · `_transporting` 清理 ＋ 辅助面核查

| 字段 | 引用点（实读） | 处置 |
|---|---|---|
| `_transporting` | **0 引用**（唯一消费者链 B 已删） | ✅ **已删** |
| `transportIntensity` | **0 引用** | ✅ **已删** |
| `transportExpiry` | **0 引用** | ✅ **已删** |
| `assignInterval` | `Update`（派发节拍）**在用** | ✅ **保留** |
| ⭐ `GetPriority`（`:75`）／`_priorityConfig`（`:49/:53`） | ⭐ **0 调用**（仅声明＋加载＋定义自身） | ⛔ **未删** —— 依裁定 §D-3「成 0 调用 ⇒ **报裁**」⇒ ⭐ **请裁**（建议：随链 B 一并删，或保留作 `DispatchCrew` 未来接入口） |

## 五 · ⛔ 未落项（如实标注 · 不判绿）

| 件 | 状态 |
|---|---|
| **件D 探针扩展** | ⛔ **未落**：`·farm]` 逐帧序列／`DestFull` 计数（8 值分桶）／`O-17`／`O-18`（⭐ **"无空闲工人"场景点击** · 本批最关键缺口）／判据 9 `Q1` 派出类型／判据 10 `Q2` 留背包时序／判据 5 `#42` 9 处＋反向列／**A1**（`TotalCount>0` 纯水井仓可点 vs `IsReadyToHarvest=False` 两列差异）／**A2**（拆除中点击）／**A3**（`U15Probe:13/:94` 面板口径期望改注） |
| **件E 跑局 ＋ 四项回归** | ⛔ **未跑**（`HH315`／`HH316` 全段／`HH317`／`M7` 六轮）⇒ 判据 1/2/3/4/6/7/8 ＋ A1~A3 **一条未验**；⚠️ 跑前须先归档（`L-68`/`L-70`） |
| 报告未含项 | 上表已补 `ExecuteCompletion` 差异说明 ⑤／`AddGatherOverflow` 三列表 ⑦／豁免清单 ②；⚠️ **§三-⑦ 的"其余 5 处是否同口径"** 与 **件F-3 `GetPriority` 0 调用** 两项**请裁** |

⚠️ **预算消耗**：`GameEvents.cs`（CRLF）的 python 二进制 + EOL 违规自纠（全文件规范化）占比较高；件D/E 需另批。
