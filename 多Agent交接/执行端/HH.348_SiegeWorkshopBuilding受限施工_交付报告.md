# HH.348 小源⑤ `SiegeWorkshopBuilding` 受限施工 · 交付报告

> 类型：进度同步（交付报告） · 状态：🟡 已交付 · 待事务核实
> 日期：2026-09-30 · 发起端：执行端 · 关联：任务书 `HH.341_M5-D乙加_小源⑤_SiegeWorkshopBuilding施工窗口任务书.md`（`D925` 签发）／预检 `HH.346`／裁决 `D923`（小源④接缝先例）
> 取号：**HH.348**（事务端按 `vr-id-ledger` 实时水位线登记，commit `25d9628c`；执行端未动账本）
> 性质：HH.347 施工轮交付。报告状态＝**施工完成 · 待进局专用取证补齐、跨档、L-95、基线及事务核实**。⛔ 本报告不写「源级通过」「判绿」。

## 一、开工／收工基线（实测）

| 项 | 值 |
|---|---|
| 开工 HEAD | `4bca76c3`（`4bca76c31fda94bad4ebec15acb956271ff57458`）· 开工实测 |
| 收工 HEAD | `4bca76c3` · 收工实测（**与开工相同 ⇒ 本轮源码/报告零 commit**；报告落盘 commit 为取号后的本批动作） |
| 回退点 | `Valley Rampart/Logs/hh341_small_siege_rollback/` 两副本：`SiegeWorkshopBuilding.cs.pre-HH347`（**12,744 B**，与预检 HH.346 §9.3 指纹一致）＋ `TaskScheduler.cs.pre-HH347`（**99,945 B**） |
| `GameScene.unity` | 开工 `git status` 即为 `M`（**开工前既有挂账 `O-14` 载体**）；收工 blob `git hash-object`＝`4a86f26f6a7c2aab3c440903ddfa80020ddec9a3`（只登记；本轮未执行任何场景保存／还原／checkout） |
| 生产域改动面 | 见 §六（`_Game` 域 diff 有且仅有两目标文件 ＋ 两个开工前既有未跟踪项，等集收据） |

## 二、施工内容（允许面内 · 两文件）

### 2.1 `Valley Rampart/Assets/_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs`（numstat +272/-2 · 279→548 行）

- **组件侧补 D/A/B/E/C 五类协议回调**（形态照 `D923` 小源④ `BlacksmithBuilding` 先例逐型复制）：
  - `ProtocolSlot` 卡池（**一源多卡 · 循环型常驻源**：完成/失败只收口该卡，⛔ 不封源，源照旧逐 tick 广告）；
  - `CreateSlot`：统一经 `TaskProtocolIssuer`（卡名 `SiegeWorkshopBuilding_Task`；归属＝宿主 `kingdomId`，对齐 `SourceKingdom` Component 分支路由；`deadline=+∞`）；
  - `OnProtocolDispatched`（A：幂等选卡→`rt.Assign` 配对）、`OnProtocolArrived`（B：`Assigned→Executing`，**⛔ 不等于产出、⛔ 不等于 Complete**）、`OnProtocolAbandoned`（C：源失效⇒`TargetRemoved` 封口；否则 `rt.Unassign` 回待派，`failAttempts≥2`⇒`RetryExhausted`）、`OnProtocolSourceInvalidated`（D：置失效＋全部非终态卡封口，⛔ 无在途豁免）、`OnProtocolTaskCompleted`（E：收敛 `Done` 并释放配对/预定）；
  - `SealAllOnDestroy` 销毁兜底（`OnDestroy` 前置调用，⛔ 不在已销毁对象上留预定残留）；
  - `FinalizeSlot`／`FindSlotForWorker`／`FindPendingSlot`／`MirrorCheck`（过渡镜像逐次对拍）／`MapUnassignReason`。
- **`HasWorkerOnDuty` 统一为双查**（`SiegeWorkshopBuilding.cs:253-262`）：`HasWorkerAssigned(this) || (_building != null && HasWorkerAssigned(_building))`——组件源或宿主 `Building` 源任一在岗即忙；对照 `BlacksmithBuilding.cs:70-71` 双查形态（`D925` 统一口径）。
- 4 个协议计数器（`_lastWorkerId`／`_totalCards`／`_doneCards`／`_abortedCards`）加 `[SerializeField]`（`:318-321`）：验收期桥/Inspector 只读可见化，行为零参与（**自报瑕疵 1**：超出先例最小形态，去留待主策划裁定，见 §八 Q2）。

### 2.2 `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs`（numstat +17/-0 · 1677→1694 行）

- 五接缝落位，每处插在 `BlacksmithBuilding`（bs）分支之后，形态 `if (task.source is SiegeWorkshopBuilding swXxx) swXxx.OnProtocolXxx(...)`：
  - D 源失效：`:289-291`（`Tick` ① 清无效源内）
  - A 派发：`:453-455`（`Dispatch` 内）
  - B 到达：`:582-585`（`MovingToSource→Working` 转换内）
  - E 完成：`:764-767`（`Complete` 内，`ClearNpc` 之后）
  - C 放弃：`:822-824`（`Abandon` 内，`ClearNpc` 之后）
- ⛔ 调度器内核逻辑、`taskTimeout`、`Complete` 判据、`SourceKingdom` 路由（`:1565-1577` 族）、`ExecuteCompletion` 专用生产副作用、`ITaskScheduler.cs` 与协议六文件：**零触碰**（numstat `+17/-0` 为界，L-100 等集见 §六）。

## 三、静态检查

- 编译：两次 `refresh` 实测 **`errors=0`**；warnings 15~25 条**全为既有**（IUIPanel/ToastManager/ChestManager 等），**零条**归属两目标文件（两文件在编译输出中零出现）。
- `git diff --check` rc=0（仅既有 LF/CRLF 警告，两目标文件与 `GameScene.unity` 均为开工前既有点）。
- 行尾/BOM/NUL：两文件 BOM=False、NUL=False。
- 行数：本源 279→**548**；调度器 1677→**1694**。

## 四、真实进局取证（正门 · L-95 同工具 3 次封顶）

**执行通道**：复用**存量在库**正门容器 `Valley/验证/HH111_战争机器生产入口`（`Valley_HH111_Smoke_MachineEntry.cs`，HH.111 批产物——直建本源工坊 `BuildAt`→`CreateBuildingInstance`，容器内部 `TestHarnessApi.EnterTestRun(cfg, 60f)` 建局＋考跑守卫全开，两局 seed 21111 人类／21112 矮人；执行它不写任何文件，不触允许面）。⛔ 无裸跑 GameScene。

**观察窗 7 项声明（口径受限，如实声明）**：
1. 暖机起止：容器内建局（`EnterGame`→等就绪→`WaitForSeconds(0.3/0.5)`），墙钟约 3~8 秒；游戏秒按 60x 折算不可精确锚定（容器未打暖机日志）；
2. 轮询起止：执行端快照轮询 3 次均在局外窗口（见未取得 1）；
3. 触发零点：menu 触发时刻（`HH.348` 报告 §四 记录的 3 次跑批墙钟）；
4. 观测窗起止：容器自动收工（P1a~P5b 探针跑完即 `ExitTestRun+QuitSmoke`），两局全程 <20~40 墙钟秒；
5. 墙钟与游戏秒折算：`timeScale=60`（容器传入 `speedOverride=60f`）；⛔ 容器直调为主，游戏秒粒度读数未产出；
6. 收工条件：容器自身判据（HH.111 批 P1~P5），非本源专用判据；
7. 是否在任务段内：否（容器直调为主，非本源专用观察窗）。

**已取得（运行时）**：
1. 正门建局 ×3（`[TestHarness] EnterTestRun 完成：speed=60x maximumDeltaTime=1.0 shadows=Off vSync=0`）；
2. **三子仓就绪 ×3**：`[SiegeWorkshop] 厂级弹药仓就绪：3 个子仓，容量=30（HH.19 A×4）`（P1b 直建即 `CreateSubStores` 建仓）；
3. **外部产弹入口运行时**：`[SiegeWorkshop] 产 StoneAmmo ×5（耗 Stone 5）`（P1d 直调 `Produce`；国库石 85→80、弹仓 0→5、可取 2）——⚠️ 这是**外部入口读数，非在岗门内 Tick 产**（能力句 2 的「在岗门内」运行时面未取得，见下）；
4. 派发/放弃日志：局1 `派发 Production → npcId 3/15/9 @ (29.44,44.80)/(14.72,58.56)/(49.28,88.64)` ＋ `Abandon Production → npcId 3/15/9 reason=BrainLost`；局2 AI 国四源 `npcId 36/58/46/52` ＋ `reason=SourceInvalid`；
5. **世界重建面**：局1→局2 切换＝旧局全实体 `OnDestroy`（本源 `SealAllOnDestroy`＋`Unregister`→`OnBuildingDied` 路径被走过，全程静默成功、零异常日志）。

**⛔ 未取得（如实登记；「未观测到」≠「不存在」）**：
1. **接缝 A/B/E 的本源归因快照**：派发日志无法归因本源——`Building.cs:1603-1609` Building 本体挂 `ProducerComponent` 时亦以本体为源广告 Production，局1 三次派发坐标互异 ⇒ 多源并存、归属未锁定；本源 `_totalCards` 等快照轮询 3 次均未抢到（工坊存活窗口 <8 墙钟秒 vs MCP 调用往返 2~5 秒）；
2. **在岗门内 Tick 产弹**（能力句 2/3 运行时面）、**E 接缝 Complete 收口**、**C 接缝回待派/封口行为**、**D 接缝真失效封口**（本轮无拆楼/注销场景，仅世界重建间接面）、**双查在岗的 Transport 互斥行为**：均未观测到（容器直调为主、局1 真实时长过短、工人未及到岗）；
3. **跨档 Save/Load 往返**：未取得（HH111 局2＝世界重建非读档；`Valley_HH109_Smoke_SaveDomain` 为 HH.109 批自验容器、不含本源；无合法执行通道构造本源读档往返）；
4. 能力句 6 按任务书 §〇-6 本就「R2 未闭合前不得判闭」——与本窗未取得项一致（另：三子仓跨档**不可恢复**为 R2 已知缺口，`Building.SaveState:1100-1144` 无本源字段、全库无 `GetComponentsInChildren<StorageComponent>`，HH.346 §1.7 已实证，本轮未重复取证）。

**L-95 三段说明**：同工具（HH111 容器复用）3 次尝试已封顶；未降级替代、未裸跑。生产面（HH111 局1 全链＋本源自动广告链环境）／影子面（协议卡快照）／空白对照（无本源专用对照段）中，影子面与专用对照段**未取得**——通道阻塞如实上报（§八 Q1）。

## 五、「Complete ≠ 产弹」证据（能力句 4）

- **结构直接证据**：`TaskScheduler.cs:882-885` Production 分支唯一动作＝`var prod = comp != null ? comp.GetComponent<ProducerComponent>() : null; if (prod != null) prod.Tick();`；本源宿主数据行 `Assets/Resources/Buildings/SiegeWorkshop.asset` 的 `components` 数组**仅 `comp.siege_workshop` 一个元素**（HH.346 §五 DEFDATA 实测），`BuildingComponents.cs:200` 该键只 `Add<SiegeWorkshopBuilding>` ⇒ 宿主 GameObject 不挂 `ProducerComponent` ⇒ `prod == null` ⇒ **该分支对本源为空操作**（`Complete`/`Done`/`ExecuteCompletion` 均不产弹）。
- 真实产弹唯一承载＝`ProductionSystem → ITickable → Tick():108-132 → Produce():135-152 → store.Add`，且必须发生在在岗门（`:119`，`HasWorkerOnDuty`）内；运行时佐证读数属 §四未取得 2。
- 接缝未改写边界（能力句 5 静态面）：轮产 `_cycleOrder` 推进（`:126-127`，索引推进先于产出、与产出成败无关）、三子仓独立容量/`IsFullFor`/`CanAccept`、原料不足门（`:144`）——本轮零触碰；调度器侧 numstat `+17/-0` 为界。

## 六、基线复算与改动面（L-100 等集收据）

统计器 `Valley Rampart/Logs/hh341_small_siege_build_stat.py`（**先空跑后实跑**，输出落盘 `hh341_small_siege_build_stat.txt`，`.py` 落文件执行纪律）：

| 行集（`## S` 行型） | pattern 原文 | 口径 | 读数 |
|---|---|---|---|
| `SW_BUILD` | `TaskScheduler\.(Instance\|HasInstance)` | 本源文件工作树逐行 | **11**（原 6＋协议块新增 5，锚 `:337,338,529,530,546`，可解释） |
| `SW_BUILD` | `new KingdomTask\(` | 同上 | **1**（`:280` 原行未动，持平） |
| `SW_BUILD` | `OnProtocol(Dispatched\|Arrived\|Abandoned\|SourceInvalidated\|TaskCompleted)` | 同上 | **各 1**（`:386/:406/:416/:452/:462`，⭐ 本源 `OnProtocol` ×5） |
| `TS_SEAM` | `SiegeWorkshop` | `TaskScheduler.cs` 工作树逐行 | **0→10**（5 接缝×2 行；HEAD=0 与预检 §2.3 `TS_REF 0→0→0` 对拍吻合） |
| `TS_SEAM` | `is SiegeWorkshopBuilding` | 同上 | **×5**（`is SiegeWorkshopBuilding` 类型判定 5 处） |
| `BASE` | `git show HEAD` | 对拍 | 本源 SchedRef HEAD=6／NewKT HEAD=1；TS 内全名自引用=0 为结构性正确（类内静态成员不写全名前缀） |
| `PROD` | `TaskScheduler\.(Instance\|HasInstance)` | `git grep` 全 `_Game` 工作树 | **86 ＝ 预检 81 ＋ 本源新增 5**（等差可解释） |
| `PROD` | `new KingdomTask\(` | 同上 | **15**（与预检 15 持平） |

**改动面等集收据（含未跟踪，L-100）**——本轮收工 `git status` 限定 `_Game` 域全集：
1. `M Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs`（+17/-0）——**本轮施工**；
2. `M Valley Rampart/Assets/_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs`（+272/-2）——**本轮施工**；
3. `?? Valley Rampart/Assets/_Game/Art/Ground/New Palette.prefab`（＋`.meta`）——**开工前既有未跟踪项，⛔ 非本轮引入**（开工首条 `git status --porcelain` 即已列出；经事务端核实归属）；
4. `?? Valley Rampart/Assets/_Game/Art/Ground/ground_tropical.asset`（＋`.meta`）——**开工前既有未跟踪项，⛔ 非本轮引入**（同上）。

⇒ 改动面集合（含未跟踪）与本报告清单**等集**；除两目标文件外零源码/资产/场景/协议六文件/账本写入。

## 七、自报瑕疵（3 条）

1. `[SerializeField]`×4 为验收可见化辅助改动，超出 D923 先例最小形态（保留在工作区，去留待主策划裁定 §八 Q2）；
2. 首次进局未预判容器跑批速度（两局 <20 墙钟秒），浪费 L-95 第 1 次尝试且错过快照轮询；第 2 次误判「容器未执行」（实为 console clear-then-read 后固定 seed 确定性日志逐字一致）；第 3 次提前挂窗仍未抢到——**同工具 3 次用尽即停**，未降级替代、未裸跑；
3. 观察窗 7 项中「收工条件」以容器自动判据为准、非本源专用窗（§四口径受限声明）；快照轮询 3 次起止时刻在局外窗口，未取得有效轮内读数。

## 八、请裁与事务端处置回录

| # | 决策点 | 事务端处置（本轮回录） | 后续 |
|---|---|---|---|
| Q1 | 接缝 A/B/E 归因快照等未取得项的取证通道（一次性探针文件授权／事务端核实轮代跑） | **属主策划裁定**，执行端不自选边、不据此改码 | 待裁定后另行派工 |
| Q2 | `[SerializeField]`×4 去留 | **属主策划裁定**（同上） | 待裁定后另行派工 |
| Q3 | 未取得项是否阻塞受理 | **不阻塞受理，但阻塞判绿**；报告「未观测到 ≠ 不存在」写法正确，保持 | 事务端核实后由主策划裁定是否补验 |

## 九、待办与收口

- 本报告以 **HH.348** 落盘并具名 commit（仅报告 1 文件，不 push）；**源码改动保留工作区**，由事务端统一核实提交（沿 `D921`／`D923` 先例）。
- 状态＝**施工完成 · 待进局专用取证补齐、跨档、L-95、基线及事务核实**；⛔ 不写「源级通过」「判绿」。
- R2（三子仓存档缺口）／V17（容量时序）已另立工单，不入本窗（⛔ 未触碰）。

---

*报告完（首版）。证据：`Valley Rampart/Logs/hh341_small_siege_rollback/`（回退点两副本）· `hh341_small_siege_v20_run_window.txt`（进局零点）· `hh341_small_siege_v20_run1_console.txt`（首次跑批取证）· `hh341_small_siege_build_stat.py`／`_stat.txt`（统计器＋落盘）。*

---

## 裁定加注（2026-10-01 · 事务端按裁定补注）

> 本件原文保留不改；下列为后续裁定结果的加注。

- **裁定结果**：第 5 源 `SiegeWorkshopBuilding` **本轮不判绿** —— 结构、落库、边界、五接缝、`V15`、五项基线均支持；`R2` 缺口承认存在；4 组运行时读数未取得。
- **唯一下一动作**：下一授权轮立 **`R2` 三子仓存档兼容闭环 ＋ 一次性本源探针联合工单**（目标号 `HH.349`／`D926`；⚠️ 本轮不登记、不预留）。
- **本件状态**：施工合规 · 核实受理 ⇒ 队列项 **⏸阻塞**（`R2` 硬门 ＋ 4 组运行时读数未取得）。
- 台账详见 `测试基线台账.md` **§二百四十三**。
