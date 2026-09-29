# HH.341 小源③ `MineByproductComponent` 施工窗口 · 交付报告

- 执行端交付 · 2026-09-29 ｜ 依据＝主策划 `D920` 裁定（台账 §二百三十四 · commit `c4c3b744`）｜ 预检报告 commit `0032a78e`
- 唯一正文＝`多Agent交接/策划端/HH.341_M5-D乙加_小源阶段任务书.md`
- **源码改动 ⛔ 不入提交**（保留工作区由事务端核实后统一提交）；本报告 commit 只含本报告 1 文件（具名 add ＋ `commit -F`）；⛔ 未 push
- 证据前缀＝`Valley Rampart/Logs/hh341_small_mine_byproduct_*`

---

## 1. 开工回执

| 项 | 值 |
|---|---|
| 当前源 | `MineByproductComponent`（第 3 源） |
| 前置基线 | **开工 `HEAD` 实测**＝`c4c3b744dac54885e066888208c1cc3bb9dae44c`（D920 落账提交） |
| 独立回退点 | 回退手段＝`git show <rev>:<path>` 覆盖还原（未执行）；`GameScene.unity` 工作树 hash `4a86f26f`（O-14 继续挂账，只登记） |
| 允许面（§六逐条） | 当前源 `MineByproductComponent.cs`＝**已改**；接缝 `TaskScheduler.cs`＝**已改**（仅类型分支）；协议六文件＝**未触碰**；`ITaskScheduler.cs`＝未触碰；`ChestManager.cs`／场景／Prefab／`AI.Core`／后续三源／HH.342 废止草稿＝**未触碰**；证据目录＝已写新文件（本前缀） |
| 写动作 | 生产域 2 文件（见 §2）；场景/Prefab/资产 0 |
| 结论 | 施工完成 · 待事务端核实（⛔ 不自评通过） |

## 2. 逐文件代码清单（file:line 实测 · 纯新增 274 行）

| 文件 | 区间 | 改动性质 |
|---|---|---|
| `Valley Rampart/Assets/_Game/Systems/Building/MineByproductComponent.cs` | `:1`（using）｜协议区 `:246-501`（ProtocolSlot 卡池／Rt/LogProtocolOnce/CreateSlot/接缝 A-E 五回调/SealAllOnDestroy/FinalizeSlot/FindSlotForWorker/FindPendingSlot/MirrorCheck/MapUnassignReason）｜`OnDestroy` `:512` 兜底封口 | 源侧协议接缝（ChestEntity `872ec5d5` 同型；卡池＝一源多卡；D 段**无在途豁免**——旧链在途例外仅 ChestEntity 享有） |
| `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs` | D‴ `:282-284`｜A‴ `:440-442`｜B‴ `:562-564`｜E″ `:736-738`｜C‴ `:787-789` | 仅增加 `if (task.source is MineByproductComponent mb) mb.OnProtocolXxx(...)` 五处类型分支；⛔ 调度语义／源选择／`taskTimeout`／`Complete` 判据／`SourceKingdom` 路由／`:1038` 零改动（基线复算 §7 为证） |

提交面证明：`git status` 工作区保留上述 2 个 modified 文件未提交；报告 commit 仅 1 文件。

## 3. ⭐ C5 假设—证据矩阵（D898 口径）

| # | 候选假设 | 判别性证据（原始/派生） | 底层依赖 | 轮次上限 | 证伪条件 | 结果 |
|---|---|---|---|---|---|---|
| H1 | 五类接缝在真实派发链被调用 | 卡状态转移序列 `Assigned→Executing→Done/Pending/Abort` 全部由回调驱动（CARD_ROW 原始） | 调度器 Dispatch/UpdateAssignedTasks/Complete/Abandon/Tick 清扫 | 观察窗 420 游戏秒 | 卡状态无转移或全靠轮询 | **成立** |
| H2 | 建卡走 `TaskProtocolIssuer`（非手填卡） | 卡带 `retry=0/1`、`hasDL=False`、taskId 由 `NextProtocolTaskId` 分配（原始）；手填卡缺省 `retryMax=-1` 可判别 | Issuer.Create/Submit 契约 | — | 出现 `retryMax=-1` 卡 | **成立** |
| H3 | 占用权威＝`TaskBindingManager`，镜像对拍一致 | `SNAP.invVio=0`（每快照）＋零「过渡镜像不一致」日志（原始） | VerifyInvariants/ProtocolMirrorConsistent | — | 任一快照 invVio>0 或镜像日志出现 | **成立** |
| H4 | Transport 装载走副产子仓分支（`:999-1003`） | `CRY delta=-5`（20→15，派生自存储读数）＋工人背包 `Crystal:5`（DISP 原始） | LoadInventoryFromSource→GetStore | — | 存量不降或背包无货 | **成立** |
| H5 | 重试上限 2＝初次 1＋重试 1；超限封口 | 卡10/11 `consumed=1→Pending`；再败后 `ab` 封口（ab=5 含 RetryExhausted）并重派新建卡（total 4→6）（原始） | TryConsumeRetry/IsLegalUnassign | 420 游戏秒 | 未释放即重派/尝试数>2 | **成立** |
| H6 | 源失效 ⇒ 全部非终态卡封口（无在途豁免） | P3 `srcZero3s`：死者实例在册归零＋全局预定 3→2≤基线＋mines 4→3（原始） | Die→OnDestroy/Unregister/Tick 清扫 | 60 游戏秒 | 死者在册残留或预定不降 | **成立** |
| H7 | Transport 卸货到账（k0 实例内） | `k0CryBank=0`（420 游戏秒全窗）⇒ **未取得**（L-97 观测范围：k0 源 Transport 卡全量，取值域=装载后到库前超时）；替代证据＝同型源 k1/k3 Transport 完成（done 35/8 计数）＋v2 轮同装载证据 | NPC 寻路/`taskTimeout=30` 预算（**既有政策值**） | 两轮共 720 游戏秒 | 到库完成样本出现 | **未取得 · 已登记** |
| H8 | Production「在岗才产」产出 | k3/k1 矿 `Cry/Oil/Ore=5/5/5` 等真实增量（原始）；k0 矿 oilGain=oreGain=0＝低速率（0.05/s）∧ 在岗时长不足的**合法无产出**（Q3 允许，已记录） | ProductionSystem→Tick→在岗门 | — | 在岗期间无任何矿产出增量 | **成立** |

## 4. 生产进局报告（真实 TaskCard · 两分支 · D920 四条验收）

- **① Production 卡到达 Working**：卡8 t=25.10 Assigned(worker22) → t=38.09 **Executing**（接缝 B）→ Working 在册（DISP `Production/mine#/Working`）✔
- **② Tick() 产出或合法无产出**：k3 矿 `Cry/Oil/Ore=5/5/5`、k1 矿 `2→3→4` 真实增量 ✔；k0 矿 oilGain=oreGain=0＝0.05/s 慢产 ∧ 在岗时长不足 ⇒ **合法无产出**（已记录）✔
- **③ 随后 Complete 或 Abort**：k0 卡8 Complete→接缝 E→Done（t=41.71）；卡9 Timeout→Abort 路径（Unassign+retry）；卡10/11 重试耗尽封口 ✔
- **④ 协议绑定与预定归零**：P3 死者实例归零＋全局预定 3→2≤基线；全程 `invVio=0` ✔
- 补充：Production 完成 **不**改称「到岗」——到岗（接缝 B/Executing）与完成（接缝 E/Done）分层取证，`ExecuteCompletion` 的 `ProducerComponent` 空操作保留未动（`D920` Q3）✔

## 5. 跨档报告

- Save→Load 成功；运行时重建（rtHash 变化、同一=False）；k0 矿实例重建（新 instanceId，存量 Cry=15 与档前一致）
- 读档后**重新广告并再派工**：60.2 游戏秒内新建卡＋Assigned（`redispatch60s`）✔；无悬空绑定（invVio=0）、无重复活动卡（live=1/total=1）
- id 口径登记：运行时重建 ⇒ id 计数重启（nextId=78），「新卡不复用旧卡 id」按「新运行时新建卡对象＋无旧卡引用」取证；绝对 id 不跨运行时比较（与「不复用绝对 tick」同族语义）

## 6. L-95 三段原文（分列 · 每段 ≤3 次同工具 · 无降级）

| 段 | 范围 | 原文结果 |
|---|---|---|
| 1 生产链 | 进局 435 游戏秒真实建局全程 | probe.txt `[ERR] 探针窗口内 error/exception 0 条` |
| 2 退 Play 后 | 清台后 fresh read | `hh341_small_mine_byproduct_l95_seg2_err.txt`＝Retrieved 0 log entries |
| 3 空白对照 | 只进 Play 不建局 20 秒 | `hh341_small_mine_byproduct_l95_seg3_err.txt`＝Retrieved 0 log entries |

既有 O-14 噪声（`ruler` 缺脚本/PanelSettings/287 node options）在本次清台后未复现，不计入本源新增。

## 7. 基线复算（同命令 HEAD vs 工作树 · `## S SCAN_ROW`）

- SchedRef：71 行/17 文件 → **76 行/17 文件**（+5 全部在当前源文件：Rt()×2、CreateSlot×1、MirrorCheck×2；文件数零新增）
- NewKT：15 行（非注释 14）→ **15 行（非注释 14）零漂移**；ITaskSource 声明：10 → **10 零漂移**
- 本源文件：`TaskScheduler.Instance|HasInstance` 5→10 行、`new KingdomTask(` 2→**2 行零漂移**（广告面零改动）
- AI.Core／场景／Prefab／旧资产键／账本命中＝**0**；下降/上升逐项解释如上（全部上升，归因=源侧协议回调对调度器的只读访问）

## 8. 编译与清洁检查

- Editor.log：`Reloading assemblies after forced synchronous recompile. CompileScripts: 8129.907ms`；**error CS=0**（warning 均既有归属：CS0414×3/CS0618/CS0162×2/CS0219×3/CS0472）
- `git diff --check`＝无空白错误；行尾 LF（库内既有 CRLF 转换警告属 Git 配置提示，非文件损坏）；无 BOM/NUL 问题（Write 工具 UTF-8 无 BOM）
- CoplayDev `read_console`（error）退 Play 后＝0 条

## 9. 落盘核验闸门 4 步

1. **mtime**：证据文件 `…/hh341_small_mine_byproduct_gate.txt` 写入后 `mtime=2026-09-29 15:26:33.081`（len=6920）；本报告写入前＝不存在（新建），pass1 实测 mtime=2026-09-29 15:28:28.476（len=11937），终版 mtime 于落盘后实测、登记于证据文件 §10.1（自持值物理上无法于写入前预知）
2. **磁盘重读**：两文件均 `Get-Content -Raw` 磁盘重读 ✔
3. **hash/逐字比对**：证据文件 sha256=`38B8ED96…C3B`（§10 前状态）；报告终版 sha256 见 §10.1 回填 ✔
4. **git show 对比**：commit 落地后 blob 与磁盘 `git hash-object` 字节级比对，结果与 commit hash、收工 HEAD 登记于证据文件 §10.2

## 10. 未做项显式清单（⛔ 不得留空）

1. ⛔ **未改 `TaskScheduler.cs:1038`**（AI(>0) Ore 台账路由缺口＝未解决的既有路由项，原样保留）；本源 Ore 的广告/取料/玩家侧卸货已随两分支链路实际走过（Ore 子仓产出的搬运走同链），AI 侧 Ore 卸货未专门验证（登记）
2. ⛔ 未新增本源专用 `ExecuteCompletion` 副作用分支（空操作保留）
3. ⛔ 未扩 `ITaskScheduler`、未重写调度内核、未动 `taskTimeout`/`Complete` 判据/`SourceKingdom`/去重/规模派工语义
4. ⛔ 未碰 `ITaskScheduler.cs`/`ChestManager.cs`/协议六文件/`Building.cs`/`UnitController.cs`/场景/Prefab/`AI.Core`/旧资产键/四本账本/最高优先级文档/后续三源/HH.342 废止草稿
5. ⛔ 未申请第 4 源 `BlacksmithBuilding`；⛔ 未 push；⛔ 源码改动未入提交
6. **未取得项（L-97 登记）**：k0 实例的 Transport 卸货到账完成样本（H7）——两轮 720 游戏秒观察窗内，k0 源 Transport 卡均于到库前超时/不可达（既有 taskTimeout=30 与寻路耦合）；同型源 k1/k3 的 Transport 完成计数与装载证据已取得，替代可信度请事务端裁定

## 11. 自报瑕疵（不软化）

1. **探针 v1 落格教训**：Mine 簇选择首版取全图扫描序首个（远角，与工人距离超 `taskTimeout` 预算）⇒ v2 改「距 k0 锚点最近」；v2 的距离打印值（3.89）与几何推算存在口径疑点（`FootprintCenterWorld` 与 `GetKingdomAnchorWorld` 疑似坐标空间差异）——未影响落格决策有效性（v2/v3 实测到岗正常），登记坐标口径疑点供核
2. 探针脚本两次编译失败（`isUpdated` 误用/`Object[]` 误标/`onBookMineSrc` 笔误）——均桥端 REPL 即时报错、当轮修正重跑，未污染证据（失败轮 run_id=mine_state2~4/mine_probe3 无落盘证据采纳）
3. 首轮 v1 探针的 P3「全局归零」判据设计不当（其他活动源噪声）⇒ v3 改实例级判据；v1/v2 报告未交付即作废，不构成证据污染
4. MCP 工具注册缺席处置：本会话双 Unity MCP 均未注册工具句柄 ⇒ 按 unity-mcp-first D697/D698 终端探活双活（CoplayDev 8080 healthy/Codely 桥 56785 ready）后，**CoplayDev 走 HTTP keep-alive 直调**（manage_editor/read_console）、**Codely 走 TCP 直连**（exec_runtime_script 协程探针）——与历轮同实效

## 12. 源级结论

**施工完成 · 待事务端核实**（⛔ 不自评通过；判绿须完成事务端独立核实＋主策划裁定）。
D920 验收四条：① ✔ ② ✔（k3/k1 真实产出＋k0 合法无产出记录）③ ✔ ④ ✔；未取得项 1 条（H7 卸货到账 k0 实例样本）已按 L-97 登记并附替代证据，请事务端核实与裁定。
