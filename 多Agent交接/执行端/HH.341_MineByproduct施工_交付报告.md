# HH.341 小源③ `MineByproductComponent` 施工窗口 · 交付报告

- 执行端交付 · 2026-09-29 ｜ 依据＝主策划 `D920` 裁定（台账 §二百三十四 · commit `c4c3b744`）｜ 预检报告 commit `0032a78e`
- ⚠️ **本版＝`D920` 事务端核实退回后的「补正轮」（commit `4288223e` 为退回版）**：补正三项 **N-A 锚点漂移勘正／N-B `D920`② 降级＋产出读数重报／N-C `H7` 替代证据撤销＋SNAP 读数重报** ⇒ **全部落 §13**；历史正文（§1–§12）中受裁决影响的三处结论（§3 `H7`／`H8`、§4-②、§10-6）已按裁定**就地改写并指向 §13**，⛔ 未静默删除原判断（原文对应关系逐项见 §13.3-④ 与 §13.7-3）
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
| 结论 | ⚠️ **本版＝`D920` 退回后的补正轮**：施工完成（源码零改动保留）＋ 补正三项已落 §13 · **未判绿** · 待事务端复核 ＋ 主策划复询判绿（⛔ 不自评通过；补正完成 ≠ 判绿） |

## 2. 逐文件代码清单（file:line 实测 · 纯新增 274 行）

| 文件 | 区间 | 改动性质 |
|---|---|---|
| `Valley Rampart/Assets/_Game/Systems/Building/MineByproductComponent.cs` | `:1`（using）｜协议区 `:246-501`（ProtocolSlot 卡池／Rt/LogProtocolOnce/CreateSlot/接缝 A-E 五回调/SealAllOnDestroy/FinalizeSlot/FindSlotForWorker/FindPendingSlot/MirrorCheck/MapUnassignReason）｜`OnDestroy` `:512` 兜底封口 | 源侧协议接缝（ChestEntity `872ec5d5` 同型；卡池＝一源多卡；D 段**无在途豁免**——旧链在途例外仅 ChestEntity 享有） |
| `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs` | D‴ `:282-284`｜A‴ `:440-442`｜B‴ `:562-564`｜E″ `:736-738`｜C‴ `:787-789` | 仅增加 `if (task.source is MineByproductComponent mb) mb.OnProtocolXxx(...)` 五处类型分支；⛔ 调度语义／源选择／`taskTimeout`／`Complete` 判据／`SourceKingdom` 路由／Ore 台账路由分支零改动（基线复算 §7 为证）。⭐ **Ore 分支锚点（`D920` 补正 N-A 口径）**＝**基线锚：`TaskScheduler.cs:1038`；本批新增 15 行后，工作树对应位置为 `TaskScheduler.cs:1053`。两处代码内容逐字相同，本批未改该代码块** |

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
| H7 | Transport 卸货到账（k0 实例内） | ⛔ **未取得**。仅保留两项直读：①`SNAP.k0CryBank=0`（**91 次全窗**，基数＝`^## S SNAP_ROW` 全量 91 行，取值域 `{0: 91}`，`SNAP.t` 19.31→514.15）②`== [P1] 停止 reason=window420 … transLeft=True`（`probe.txt:798`，`transLeft` 由「Transport 离册」置真，⛔ 非入库）。⭐ **原「替代证据＝同型源 k1/k3 `done≈35/8`」经 `D920` 补正 N-C 撤销**（该读数为误读，详见 §13.3） | NPC 寻路/`taskTimeout=30` 预算（**既有政策值**） | 归档证据集内＝P1 窗 **420.0 游戏秒**（`probe.txt:798`）；⚠️ v2 轮原始输出未单独归档 ⇒ 不在本证据集内 | 到库完成样本出现 | **未取得 · 已登记** |
| H8 | Production「在岗才产」产出 | ⚠️ **`D920` 补正 N-B 降级：本假设仅「合法无产出」支成立，「真实产出」支未取得**。k0 被测矿 `PROD.oilGain`／`PROD.oreGain` 取值域 **`{0: 75}`／`{0: 75}`**（基数 75 条 `PROD_ROW`）⇒ 实证「合法无产出」；「真实产出」无任何 gain 字段直读支撑（全文件 227 次 gain 读数中非零仅 71 次且全为 `PROD.cryGain=-5`＝被搬出）。详见 §13.2 | ProductionSystem→Tick→在岗门 | — | 在岗期间出现 `oilGain>0` 或 `oreGain>0` 直读 | ⚠️ **部分成立（仅合法无产出支）· 真实产出支未取得** |

## 4. 生产进局报告（真实 TaskCard · 两分支 · D920 四条验收）

- **① Production 卡到达 Working**：卡8 t=25.10 Assigned(worker22) → t=38.09 **Executing**（接缝 B）→ Working 在册（DISP `Production/mine#/Working`）✔
- **② Tick() 产出或合法无产出**：⚠️ **`D920` 补正 N-B 降级＝仅「合法无产出」成立**。k0 被测矿 `PROD.oilGain=0`／`PROD.oreGain=0`（75/75 全量零计数，`probe.txt:34-794`）＝低速率（0.05/s）∧ 在岗时长不足的**合法无产出** ✔；⭐ **原写法「k3 矿 `Cry/Oil/Ore=5/5/5`、k1 矿 `2→3→4` 真实增量」撤销**——`5/5/5` 实为 **k3 矿存量快照**（`SNAP.mine` 字段 `Cry_5_Oil_5_Ore_5`，非 gain 直读、非被测实例），且 **k1 矿存量 91/91 恒 `0/0/0`，从未出现 `2→3→4`**（详见 §13.2）
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

> ⚠️ **本节数值＝退回版（commit `4288223e`）实测，自 `D920` 补正轮起已失效**，保留原文不删；补正轮新值见 **§13.6**（并按该节说明随交付消息报出）。

1. **mtime**：证据文件 `…/hh341_small_mine_byproduct_gate.txt` 写入后 `mtime=2026-09-29 15:26:33.081`（len=6920）；本报告写入前＝不存在（新建），pass1 实测 mtime=2026-09-29 15:28:28.476（len=11937），终版 mtime 于落盘后实测、登记于证据文件 §10.1（自持值物理上无法于写入前预知）
2. **磁盘重读**：两文件均 `Get-Content -Raw` 磁盘重读 ✔
3. **hash/逐字比对**：证据文件 sha256=`38B8ED96…C3B`（§10 前状态）；报告终版 sha256 见 §10.1 回填 ✔
4. **git show 对比**：commit 落地后 blob 与磁盘 `git hash-object` 字节级比对，结果与 commit hash、收工 HEAD 登记于证据文件 §10.2

## 10. 未做项显式清单（⛔ 不得留空）

1. ⛔ **未改 Ore 台账路由分支**（AI(>0) 卸货仅覆盖 `Crystal`／`FireOil`，`Ore` 落通用卸货分支＝未解决的既有路由项，原样保留）。⭐ **锚点（`D920` 补正 N-A 口径）＝基线锚：`TaskScheduler.cs:1038`；本批新增 15 行后，工作树对应位置为 `TaskScheduler.cs:1053`。两处代码内容逐字相同，本批未改该代码块**；本源 Ore 的广告/取料/玩家侧卸货已随两分支链路实际走过（Ore 子仓产出的搬运走同链），AI 侧 Ore 卸货未专门验证（登记）
2. ⛔ 未新增本源专用 `ExecuteCompletion` 副作用分支（空操作保留）
3. ⛔ 未扩 `ITaskScheduler`、未重写调度内核、未动 `taskTimeout`/`Complete` 判据/`SourceKingdom`/去重/规模派工语义
4. ⛔ 未碰 `ITaskScheduler.cs`/`ChestManager.cs`/协议六文件/`Building.cs`/`UnitController.cs`/场景/Prefab/`AI.Core`/旧资产键/四本账本/最高优先级文档/后续三源/HH.342 废止草稿
5. ⛔ 未申请第 4 源 `BlacksmithBuilding`；⛔ 未 push；⛔ 源码改动未入提交
6. **未取得项（L-97 登记 · `D920` 补正 N-C 后口径）**：k0 实例的 Transport 卸货到账完成样本（H7）——归档证据集内 P1 窗 **420.0 游戏秒**（`probe.txt:798`）无到库读数，`SNAP.k0CryBank` **91/91 恒 0**；⭐ **原「附替代证据＝同型源 k1/k3 的 Transport 完成计数」已撤销**（该组读数为误读，见 §13.3），⛔ 不再作为任何支撑。全量三件套见 §13.3。

## 11. 自报瑕疵（不软化）

1. **探针 v1 落格教训**：Mine 簇选择首版取全图扫描序首个（远角，与工人距离超 `taskTimeout` 预算）⇒ v2 改「距 k0 锚点最近」；v2 的距离打印值（3.89）与几何推算存在口径疑点（`FootprintCenterWorld` 与 `GetKingdomAnchorWorld` 疑似坐标空间差异）——未影响落格决策有效性（v2/v3 实测到岗正常），登记坐标口径疑点供核
2. 探针脚本两次编译失败（`isUpdated` 误用/`Object[]` 误标/`onBookMineSrc` 笔误）——均桥端 REPL 即时报错、当轮修正重跑，未污染证据（失败轮 run_id=mine_state2~4/mine_probe3 无落盘证据采纳）
3. 首轮 v1 探针的 P3「全局归零」判据设计不当（其他活动源噪声）⇒ v3 改实例级判据；v1/v2 报告未交付即作废，不构成证据污染
4. MCP 工具注册缺席处置：本会话双 Unity MCP 均未注册工具句柄 ⇒ 按 unity-mcp-first D697/D698 终端探活双活（CoplayDev 8080 healthy/Codely 桥 56785 ready）后，**CoplayDev 走 HTTP keep-alive 直调**（manage_editor/read_console）、**Codely 走 TCP 直连**（exec_runtime_script 协程探针）——与历轮同实效

## 12. 源级结论

**施工完成 · 未判绿 · 已按事务端退回完成补正（N-A／N-B／N-C），待事务端复核 ＋ 主策划复询判绿**（⛔ 执行端不自评通过；补正完成 ≠ 判绿）。
D920 验收四条（补正后口径）：① ✔（卡8 全周期）② ⚠️ **仅「合法无产出」成立，「真实产出」支未取得**（N-B 降级）③ ✔ ④ ✔；未取得项 1 条（H7 k0 实例卸货到账样本）**替代证据已撤销**（N-C）。

---

## 13. `D920` 退回补正轮（2026-09-29 · 三项 · ⛔ 零改源码）

> 本轮性质＝**证据与报告补正**，非代码回滚。`MineByproductComponent.cs`（+259）与 `TaskScheduler.cs`（+15）**原样保留未提交**；⛔ 未覆盖任何既有证据文件（补正统计器与输出均为 `_r2` 新后缀独立文件）。
> 统计口径：基数一律＝`^## S <行型>` 锚定的采样行；字段正则锚**全名**（`(?<![\w.])` 边界），⛔ 无 `\S+` 跨字段；每张表标**行集来源**；均报**原始→排除→有效**三数；⭐ **本端自报读数一律用整数、⛔ 不用「≈」**（本节内出现的 `≈` **只出现在引述被撤销的原写法处**）。

### 13.1 N-A｜Ore 分支锚点漂移勘正

**裁决**：报告与 `gate.txt` 的工作树 `:1038` 引用不得再当作原条件行。**改法**：报告 §2 表格与 §10-1 两处（该两处为报告内 `1038` 的全部命中）已改为「基线锚 `:1038` ＋ 工作树 `:1053` ＋ 逐字相同」三段式；`gate.txt` 经全量 grep **无 `:1038` 命中**（原文只以「`:1038` 零改动」形式出现在报告，`gate.txt` 未引用该锚点）⇒ 无需改证据文件（⛔ 亦不得改，见本轮边界第 5 条）。

**复核读数（实测命令输出）**：

| 项 | 读数 | 出处／复现命令 |
|---|---|---|
| `git diff HEAD --numstat -- TaskScheduler.cs` | **15 增 / 0 删** | `git diff HEAD --numstat -- "Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs"` |
| 全部 diff hunk 头（5 个） | `-279,6 +279,9`｜`-434,6 +437,9`｜`-553,6 +559,9`｜`-724,6 +733,9`｜`-772,6 +784,9` ⇒ **5 × (+3) ＝ +15，最大删除端行号 777 ⇒ 均 < 1030** | 同上命令的 `^@@` 过滤 |
| 工作树 `:1038` 实际内容 | `// 步骤11：切注册表（WarehouseRegistry.FindNearestAvailable …）`＝**注释行** | `TaskScheduler.cs:1038`（工作树直读） |
| 工作树 Ore/AI 条件行位置 | **`:1053`** `if (wkingdom > 0 && (type == ResourceType.Crystal \|\| type == ResourceType.FireOil))` | `TaskScheduler.cs:1053` |
| HEAD 同一条件行位置 | **`:1038`**（逐字同上行） | `git show HEAD:…TaskScheduler.cs` 第 1038 行 |
| 代码块逐字对拍 | `diff <(git show HEAD:F \| sed 1038..1060) <(sed 1053..1075 F)` ＝ **无输出 ⇒ 23/23 行逐字相同** | 见本行命令（本轮实测 `IDENTICAL(23行)`） |
| 两文件命中复验 | ⭐ **结构结论（不随编辑漂移）**：把该锚点当 **Ore 条件行**引用的位置只剩 **2 处，均在 §1–§12**（§2 逐文件表格 ＋ §10-1 未做项），二者均已写成三段式；其余提及全在 §13 内，属**本条勘正叙述自身**；`gate.txt`＝**零引用**（原文未提该锚点）。⛔ **不在正文写死 `grep -c` 数字**：描述该计数的行本身会被下一次 grep 计入（⭐ 本轮实证自指漂移——同一命令两次跑出不同行数），故现盘计数以定稿后生成的 `hh341_small_mine_byproduct_gatecheck_r2.txt` 为准 | 复现：`python "Valley Rampart/Logs/hh341_small_mine_byproduct_gatecheck_r2.py"` |

**归因**：漂移由本批 5 处接缝各 +3 行（合计 +15）在 1030 之前插入所致，属**行号位移**，⛔ 非内容改动。

### 13.2 N-B｜D920② 降级 ＋ 产出读数按 L-97 重报

**裁决（照此执行）**：「真实产出」支不采信；D920② 降级为 **仅实证「合法无产出」；k3／k1 真实产出分支未取得**。

**① 观测窗（精确起止 · 分段）**

| 段 | 行号区间 | `SNAP.t` 起→止（游戏秒） | 该段 `PROD_ROW` 数 | 原始行出处 |
|---|---|---|---|---|
| P1 生产面观察窗 | `probe.txt:34`–`:794` | **19.31 → 436.96** | **75** | `probe.txt:19`（窗起 `t=19.31 / 窗口 420 游戏秒`）、`probe.txt:798`（`[P1] 停止 reason=window420 t=439.35 游戏秒=420.0`） |
| P2 跨档段 | `probe.txt:814`–`:953` | 450.65 → 507.46 | **0**（`PROD_ROW` 仅在 P1 循环内发射） | `probe.txt:955`（`游戏秒=60.2`） |
| P3 源移除段 | `probe.txt:966`–`:969` | 510.87 → 514.15 | **0** | `probe.txt:968`（`游戏秒=3.3`） |

⚠️ `PROD.t` 单调不减已验（75 值逐一比较，`stat_r2.txt` 第 `PROD_ROW N=75 … 单调不减=True` 行）。

**② 筛选条件与行集来源**

- 行集来源＝`hh341_small_mine_byproduct_probe.txt`，谓词 `^## S PROD_ROW`，**基数 N=75**（原始 75 → 排除 0 → **有效 75**）。
- ⛔ **不含全部矿实例**：探针发射器只读单一变量 `mine`（`hh341_small_mine_byproduct_probe.cs:369`，`mine.GetStore(FireOil/Ore/Crystal).GetAmount(...)`），该变量在 `probe.txt:15` 由 `CreateBuildingInstance` 落格＝**`inst_-70370` k0@(65,60) 矿**（`probe.txt:16 CFG_ROW CFG.mine=inst_-70370_bld_mine#k0@(65,60)`）。⇒ **k1／k2／k3 三座矿在 `PROD_ROW` 行集内计数＝0**。
- 全文件行型普查（原始 → 有效，`stat_r2.txt` 前 12 行）：`LOG_ROW 800`｜`DISP_ROW 542`｜`CARD_ROW 166`｜`SNAP_ROW 91`｜`PROD_ROW 75`｜`BRANCH_ROW 63`｜`WARM_ROW 4`｜`FILL_ROW 1`｜`CFG_ROW 1`；`^## S ` 合计 **1743**，非 `## S` 行 **41**（＝注释/段落行，⛔ 不入任何字段计数）。

**③ 完整取值域与计数（含零计数 · 分母＝75）**

| 字段（精确名） | 原始 | 排除 | 有效 | 取值域（值:计数，**含零计数值**） |
|---|---|---|---|---|
| `PROD.cry`（Crystal 存量） | 75 | 0 | 75 | `20:4`｜`15:71` |
| `PROD.oil`（FireOil 存量） | 75 | 0 | 75 | **`0:75`** |
| `PROD.ore`（Ore 存量） | 75 | 0 | 75 | **`0:75`** |
| `PROD.oilGain` | 75 | 0 | 75 | **`0:75`** |
| `PROD.oreGain` | 75 | 0 | 75 | **`0:75`** |
| `PROD.cryGain` | 75 | 0 | 75 | `0:4`｜`-5:71` |

**全文件 gain 字段扫网（跨所有行型）**：gain 读数共 **227** 次（75×3 于 `PROD_ROW` ＋ `probe.txt:798` 的 `oilGain/oreGain` ＋ 其余汇总行）；**非零 gain ＝ 71 次，取值集合仅 {`PROD.cryGain=-5`}** ⇒ ⭐ **全网 `oilGain`／`oreGain` 非 0 产出行＝0 条**（与事务端实测一致）。

**⑤ 证明「增量」的逐行读数（原始行，非末状态）**

| 行号 | 原始读数 |
|---|---|
| `probe.txt:34` | `## S PROD_ROW PROD.t=19.31 PROD.cry=20 PROD.oil=0 PROD.ore=0 PROD.oilGain=0 PROD.oreGain=0 PROD.cryGain=0（基线 cry0=20 oil0=0 ore0=0 ·…）` |
| `probe.txt:40` | `PROD.t=27.19 … oilGain=0 oreGain=0 cryGain=0` |
| `probe.txt:43` | `PROD.t=33.22 … oilGain=0 oreGain=0 cryGain=0` |
| `probe.txt:772` | `PROD.t=431.10 PROD.cry=15 … oilGain=0 oreGain=0 cryGain=-5` |
| `probe.txt:794` | `PROD.t=436.96 PROD.cry=15 … oilGain=0 oreGain=0 cryGain=-5` |
| `probe.txt:798` | `== [P1] 停止 reason=window420 t=439.35 游戏秒=420.0 … doneTotal=1 abTotal=5 oilGain=0 oreGain=0 cryDelta=-5` |

⇒ ⭐ **k0 被测矿：产出增量直读全窗为 0（Oil/Ore 存量恒 0）；`cryGain=-5` 是「被搬出」的负向位移，⛔ 不得作产出证据** ⇒ **D920② 仅「合法无产出」支成立**。

**⑥ `5/5/5` 定性 ＝ 存量（⛔ 非产出实证）**

- 字符串 `5/5/5` 在 `probe.txt` 命中 **0**（实测）；`gate.txt` 命中 **1**（`:44`，结论句）。
- 实际盘面形态＝`Cry_5_Oil_5_Ore_5`，出现 **7 次**，全部位于 `SNAP_ROW` 的 **`SNAP.mine` 存量字段**内，载体＝**k3 矿 `inst_-72342`**（P2 重建后实例）。
- ⛔ 该字段是 `GetStore(...).GetAmount(...)` 的**存量快照**，探针**未对 k3 建任何 gain 字段** ⇒ 按裁决**明确标注为「存量」**，⛔ 不作产出实证。
- **原报告另一处更严重的失实**：`k1 矿 2→3→4 真实增量` **无任何盘面对应**——`SNAP.mine` 内 k1 存量组合域＝**`{0|0/0: 91}`（91/91 恒 0/0/0，从未变过）**；`2/3/4` 序列实际属于 **k3**（`inst_-69810`，见下）。
- **唯一可见的矿内存量增长序列（载体 k3，登记为「观察线」，⛔ 不自升为 D920② 实证）**：

| 行号 | `SNAP.t` | k3 实例 | 存量变化（`Cry/Oil/Ore`） |
|---|---|---|---|
| `probe.txt:32` | 19.31 | `-69810` | 首见 `0/0/0` |
| `probe.txt:471` | 286.59 | `-69810` | `0/0/0 → 1/1/1` |
| `probe.txt:567` | 330.98 | `-69810` | `1/1/1 → 2/2/2` |
| `probe.txt:641` | 371.33 | `-69810` | `2/2/2 → 3/3/3` |
| `probe.txt:731` | 413.99 | `-69810` | `3/3/3 → 4/4/4` |
| `probe.txt:927` | 491.83 | `-72342`（重建） | `4/4/4 → 5/5/5` |

  组合域全量（分母 91，含零计数）：`k0 {20|0/0: 4, 15|0/0: 86}`｜`k1 {0|0/0: 91}`｜`k2 {0|0/0: 91}`｜`k3 {0|0/0: 47, 1|1/1: 8, 2|2/2: 7, 3|3/3: 8, 4|4/4: 14, 5|5/5: 7}`。
  ⚠️ **限制**：k3 属 **AI 王国**矿，其存量上升的**来源未取证**（可能是 `MineByproductComponent.Tick()` 产出，也可能是搬运入册等其他写入路径；本轮探针未对 k3 建 gain 计数、亦未记 `ProductionSystem` 侧写入日志）⇒ 按 `D920` 口径 **k3/k1「真实产出」分支＝未取得**，本表只作**后续专项的线索**，⛔ 不充抵 D920②。

### 13.3 N-C｜H7 替代证据撤销 ＋ SNAP 读数按 L-97 重报

**裁决（照此执行）**：H7 替代证据（「同型源 k1／k3 的 `done≈35/36`」）**不成立、予以撤销**；H7 只保留「k0 Transport 卸货到账样本：**未取得**」＋「`k0CryBank=0` 的 **91 次全窗读数**」。⛔ 后续不得再用 k1／k3 的 `done` 计数替代 k0 样本。

**① 精确观测范围／筛选条件／分母**

- 行集来源＝`hh341_small_mine_byproduct_probe.txt`，谓词 `^## S SNAP_ROW`，**基数 N=91**（原始 91 → 排除 0 → **有效 91**）。
- 段分布（段标记字段 `SNAP.P*`，行号区间实测）：`P1` **75** 行（`:32`–`:792`）｜`P1-end` **1** 行（`:799`）｜`P2` **12** 行（`:814`–`:953`）｜`P2-end` **1** 行（`:956`）｜`P3` **1** 行（`:966`）｜`P3-end` **1** 行（`:969`）。
- ⚠️ 跨档致实例更换：`P1/P1-end` 实例集＝`-70370(k0), -69810(k3), -69612(k2), -69408(k1)`；`P2 起`＝`-72416(k0), -72342(k3), -72252(k2), -72162(k1)`（`probe.txt:805` 记录重建）⇒ **P1-end 与 P2-end/P3-end 的计数分属两个实例代次，⛔ 不得跨代相加或互证**。

**② `SNAP_ROW` 全局字段完整域（分母 91 · 含零计数）**

| 字段 | 有效 | 取值域（节选高频 ＋ 全量见 `stat_r2.txt`） |
|---|---|---|
| `SNAP.done` | 91 | `1:38`｜`8:6`｜`0:4`｜`2:4`｜`3:3`｜`4:3`｜`7:3`｜`22:3`｜`5:2`｜`6:2`｜`14:2`｜`9,10,11,12,13,15,16,17,18,19,20,21,23,24,25,26,27,28,29,30,31` 各 `1` |
| `SNAP.ab` | 91 | `0:15`｜`28:11`｜`3:7`｜`9:7`｜`33:6`｜`1:5`｜`4:5`｜`21:5`｜`25:4`｜`23:3`｜`34:3`｜`6:2`｜`22:2`｜`24:2`｜`29:2`｜**`35:2`**｜`2:1`｜`5:1`｜`7:1`｜`10:1`｜`11:1`｜`14:1`｜`19:1`｜`26:1`｜`27:1`｜`30:1`（合计 91 ✓） |
| `SNAP.total` | 91 | `7:7`｜`8:6`｜`10:6`｜`15:5`｜`25:5`｜`6:4`｜`3:3`｜`19:3`｜`2:2`｜`12:2`｜`16:2`｜`27:2`｜`44:2`｜`51:2`｜`54:2`｜`67:2`｜`36:1`｜…（余各 1） |
| `SNAP.invVio` | 91 | **`0:91`**（含零计数＝全零） |
| `SNAP.k0CryBank` | 91 | **`0:91`** |
| `SNAP.mines` | 91 | `4:90`｜`3:1` |

**③ 每个对象的精确 `done`／`ab`／`total`（整数 · ⛔ 无「≈」· 逐行原始）**

`P1-end`（`probe.txt:799`，`SNAP.t=439.35`）：

| 对象 | 状态 | 存量 Cry/Oil/Ore | live | total | done | ab |
|---|---|---|---|---|---|---|
| `inst_-70370` **k0**（被测源） | Active | 15/0/0 | 0 | 6 | **1** | **5** |
| `inst_-69810` k3 | Active | 4/4/4 | 0 | 45 | **30** | **15** |
| `inst_-69612` k2 | Active | 0/0/0 | 1 | 6 | **0** | **5** |
| `inst_-69408` k1 | Active | 0/0/0 | 0 | 10 | **0** | **10** |
| **全局** | — | — | 1 | **67** | **31** | **35** |

`P2-end`（`:956`，`t=510.87`）＝ k0`-72416` total 1/done 0/ab 0｜k3`-72342` 17/8/9｜k2`-72252` 1/0/0｜k1`-72162` 0/0/0｜全局 19/**8**/**9**
`P3`（`:966`，`t=510.87`，k0 已 `st=Dead`）＝ 同 `P2-end` 逐矿值｜全局 19/**8**/**9**
`P3-end`（`:969`，`t=514.15`，`mines=3`）＝ k3`-72342` 18/8/10｜k2`-72252` 1/0/0｜k1`-72162` 0/0/0（k0 已离场）｜全局 19/**8**/**10**

**④ 与全局读数的关系 ＝ ⭐ 冲突确认：`gate.txt:44` 三项读数为误读，本端承认并更正**

- **自洽性校验（本端复算）**：`P1-end` 逐矿求和 `total 6+45+6+10=67`、`done 1+30+0+0=31`、`ab 5+15+5+10=35` ⇒ **与全局 `67/31/35` 差＝0/0/0**（`P1`/`P2`/`P2-end`/`P3`/`P3-end` 各段末行同样差 0/0/0，见 `stat_r2.txt` 各段「与全局差」行）。⇒ **盘面自身闭合，误读在结论句层。**
- **更正三项**：

| `gate.txt:44` 原写法 | 实测 | 判定 |
|---|---|---|
| 「k3 矿 P1-end `done≈36`」 | k3 `P1-end done=30`（`total=45`）；全局 `P1-end done=31` ⇒ **单矿 done 不可能为 36（36 > 31）** | ⛔ **误读，撤销** |
| 「k1 矿 `done≈35`」 | k1 `P1-end done=0`、`ab=10`；`35` 是**全局 `ab`** 值 | ⛔ **误读（全局 ab 被当 k1 done），撤销** |
| 「P3-end `done=8`」 | 全局 `P3-end done=8`，且 k3 `P3-end done=8`（因 P1 那代实例计数不跨代） | ✅ 成立（但⛔ 不得用作 H7 替代证据） |

- ⚠️ **误读根因（本端自报）**：结论句在**汇总行（`非 ## S`）与 `SNAP_ROW` 全局字段**之间手工搬运数字，未做「逐矿求和＝全局」闭合校验；`ab=35` 与「k1 done≈35」数字撞车系直接把全局值挂到单矿。**这是 `L-97` 补条②（口径标签）＋ 补条③（行集来源）同族失效**，本轮起 `done/ab/total` 一律**逐矿直读 `SNAP.mine` 字段 ＋ 与全局闭合差校验**后才可入报告。
- ⭐ **H7 补正后最终口径**：**「k0 Transport 卸货到账样本＝未取得」＋「`SNAP.k0CryBank` 取值域 `{0: 91}`（91 次全窗）」两项**；⛔ 无任何替代证据；⛔ 不外推到「机制不支持」（`L-98`）。

**⑤ 三数汇总（本轮新增全部统计）**：`PROD_ROW` **75 → 0 → 75**｜`SNAP_ROW` **91 → 0 → 91**｜`SNAP.mine` 内嵌矿对象读数 **363 → 0 → 363**（91 行，每行对象数分布 `{4: 90, 3: 1}` ⇒ `90×4+1×3=363`；`含 SNAP.mine 字段的行数=91`）｜gain 字段 **227 → 156（零值） → 71（非零，全为 `cryGain=-5`）**。
⚠️ 注：`SNAP.mine` 为 `SNAP_ROW` 行内多对象复合格式，按「对象读数」计数而非按行；本轮所有字段计数仍只在 `^## S SNAP_ROW`／`^## S PROD_ROW` 行集内进行，未跨行型混列（`L-97` 补条③）。复现命令＝`python "Valley Rampart/Logs/hh341_small_mine_byproduct_objcount_r2.py"`（输出已落 `…_objcount_r2.txt`）。

**⑥ 复现命令（可直接照跑 · Windows 需 `PYTHONIOENCODING=utf-8`）**

```bash
python "Valley Rampart/Logs/hh341_small_mine_byproduct_stat_r2.py"        # N-B/N-C 字段域＋三数＋逐矿/全局闭合校验
python "Valley Rampart/Logs/hh341_small_mine_byproduct_stockdiff_r2.py"   # 矿内存量变更点逐行定位（5/5/5 定性）
```

输出已落盘（⛔ 未覆盖既有证据）：`Valley Rampart/Logs/hh341_small_mine_byproduct_stat_r2.txt`、`…_stockdiff_r2.txt`。

### 13.4 观察项登记（`D920` 已裁为观察项，⛔ 不构成本轮退回理由，本轮不为其补证）

1. **窗口差异**：报告/`gate.txt` 并存 **435**（`L-95` 段1 措辞）／**420**（`probe.txt:798` `游戏秒=420.0`）／**514.15**（`probe.txt:977` 总窗末点 `t_wrap`；`SNAP.t` 最大值）三种口径。⭐ 本轮实测具名：**P1 观测窗＝420.0 游戏秒**、**总窗（`t_zero 19.31 → t_wrap 514.15`）＝494.8 游戏秒**、「435」为段1 描述性措辞（非盘面字段）⇒ 后续统一引用 `probe.txt:798/:977` 两值。
2. **坐标口径**：`probe.txt:14` 打印 `距 k0 锚点=3.89 格距（锚点=0.00,40.96）`——`FootprintCenterWorld` 与 `GetKingdomAnchorWorld` 疑似不同坐标空间／口径，⭐ 本轮未取证，登记为观察项（不影响 v2/v3 落格决策有效性）。
3. **`gate.txt` 无 `:1038` 命中**：退回单称「报告与 `gate.txt` 多处引用 `:1038`」，实测**退回版报告（`git show 4288223e:<报告>`）＝ 2 处（`:26`／`:89`）、`gate.txt` ＝ 0 处** ⇒ N-A 勘正只在报告侧落地（⛔ 证据文件本轮不得改写）。
4. ⚠️ **`gate.txt`／`probe.txt` 均未被 Git 跟踪**（`git ls-files --error-unmatch` 报 `did not match any file`，`Valley Rampart/Logs/` 命中 `.gitignore`）⇒ 两文件的**不可篡改性强校验只能靠 sha256＋`mtime`＋字节数**，⛔ 不能靠 `git show`；本轮三者已实测并与退回单所载逐位一致（见 §13.6 第 3 步）。
5. ⚠️ **`L-97` 补条③（行集来源）在 §13.2/§13.3 已逐项落地**：每张表的基数行型、谓词、分母、排除原因均单列，且 `非 ## S` 的 41 行（段落/汇总/`== [P1] 停止` 等）**未混入任何字段计数**——`== [P1] 停止` 行仅作为「该行自身读数」单点引用。

### 13.5 已通过 6 项未重复劳动（本轮零改动，仅复核存在）

改动面等集（`+259`／`+15` 零删除，本轮 `git diff HEAD --numstat` 两文件复测一致）✓｜`gate.txt` **本轮未写入**（sha256 保持 `21635650…87670`，见 §13.6 第 3 步实测）✓｜`new KingdomTask(` 2→2、`ITaskSource` 5→5／16→16 ✓｜五接缝 `:282-284`/`:440-442`/`:562-564`/`:736-738`/`:787-789` 逐条为类型分支 ✓｜`k0CryBank` **91 次恒 0** ✓（保留为 H7 支撑）。
⛔ 未重跑探针、未重跑进局、未新建证据副本 —— 已通过项按退回单「不需重做」。

### 13.6 补正轮落盘核验闸门 4 步（`D911` 第 8 项口径）

| 步 | 动作 | 实测 |
|---|---|---|
| 1 | **`mtime` 前后变化** | 改前实测：len＝**11994**、sha256＝`CDB0766C…BD064` **与 `gate.txt §10.1` 记录逐位一致**（⇒ 该版本即退回版 `4288223e`，⛔ 无他方并发写）；`mtime` 字段值 `2026-09-29 15:29:39.948` 引自 `gate.txt §10.1`（本轮改前另实测 `st_mtime` 时间戳与之一致）。改后实测＝len／sha256 **不写入本文件**（自指值随本次写入即失效），由 `…_gatecheck_r2.py` 于**定稿后**实测并随交付消息与 commit 报出 |
| 2 | **磁盘重读** | 每处 `Edit` 后按磁盘重读复验（⛔ 未复用会话缓存）。⭐ **锚点复验**：该锚点的**条件行引用**只剩 §1–§12 两处（§2／§10-1）且均为三段式，§13 内其余提及皆为勘正叙述自身；`gate.txt` 零引用。⭐ **「≈」复验**：本端自报读数**零处**使用「≈」，正文出现的「≈」只可能在①引述被撤销的原写法（§3 `H7`、§13.3-① 裁决引述、-④ 对照表与误读根因）或②口径声明（§13 引言、§13.3-③、本行）两处语境。⛔ 两类命中行数均**不写死于正文**（自指漂移），现盘值见定稿后生成的 `…_gatecheck_r2.txt` 与交付消息 |
| 3 | **sha256 / 逐字比对** | 改前报告 sha256＝`CDB0766CF302085700ACC03A155BE4A8A950DD9D0986233D9EB656AB295BD064`（＝`gate.txt §10.1` 记录 ⇒ 证明改动面无他方并发写）；⭐ **改后新值随交付消息报出（旧值自本轮起失效）**；证据文件**未被本轮触碰**：`probe.txt` sha256＝`03D74E09236C87EF2C4E8B5E742BB2E4522D048CC6C304114A71AA3EFECFC5FA`（1784 行）、`gate.txt` sha256＝`21635650870B7F3746EB480646D021D2F83D185A5D773BBD074A62E9F2B87670`（8192 B）—— 二者与退回单所载**逐位一致** ⇒ ✅ **零覆盖** |
| 4 | **`git show <commit>:<path>` 对拍** | 本轮新 commit 落地后执行 `git rev-parse <new-commit>:<报告>` vs `git hash-object <报告磁盘文件>`，结果随交付消息报出；⛔ 旧 blob `320d90cb…`（＝commit `4288223e`）自本轮起**失效** |

**本轮边界复验（⛔ 六不做）**：`git diff HEAD --numstat -- Valley Rampart/Assets/_Game` ＝ **仅两文件、数值与开工时逐位一致（`TaskScheduler.cs 15/0`、`MineByproductComponent.cs 259/0`）⇒ 零源码改动**；`GameScene.unity` `git hash-object` ＝ **`4a86f26f6a7c2aab3c440903ddfa80020ddec9a3`**（与退回时一致，⛔ 未 `checkout`／未 `restore` ⇒ `L-99` 继续遵守）；⛔ 未 push；提交面＝**具名 add 报告单文件**。

### 13.7 补正轮自报瑕疵（不软化）

1. ⚠️ **本轮首版曾把 `SNAP.mine` 内嵌对象数写为 364（＝91×4 估算）**，随后以 `…_objcount_r2.py` 实测得 **363**（分布 `{4: 90, 3: 1}`，`P3-end` 行仅 3 矿）并就地更正 —— 属「先算后测」反模式（`L-93` 仪器校验同族），⛔ 未流入最终读数值。
2. ⚠️ **闸门第 1/4 步的自指值**（本报告改后 `mtime`／sha256／blob）物理上不能写入自身，按历轮同实效**随交付消息报出** ⇒ 报告内为「改前值 ＋ 指认」，非完整自持。
3. ⚠️ **原报告 §4-②「k1 矿 `2→3→4` 真实增量」为无盘面对应的失实写法**（k1 存量 91/91 恒 `0/0/0`；该序列实属 k3）—— 本轮已撤销并更正；此条系**上轮交付即存在的实质失实**，非本轮统计口径问题，主动列报。
4. ⚠️ **v2 轮原始输出未单独归档**（`Logs/` 内仅 `probe.txt`＝v3/`run_id=mine_probe4`）⇒ H7 观察窗口径本轮收窄为 **420.0 游戏秒**，⛔ 沿用「两轮共 720 游戏秒」表述（原写法证据不可复现）。
5. ⚠️ **`gate.txt:44` 结论句本轮未改写**（它是已提交证据文件，⛔ 覆盖证据为红线）⇒ N-A／N-C 的勘正**只落在报告 §13**，`gate.txt` 侧错误读数以 §13.3-④ 对照表**并列登记**，请事务端在核实时一并注记。

### 13.8 【请裁】（3 条）

1. **k3 存量增长序列的归属**：`probe.txt` 显示 k3（AI 王国矿）`Cry/Oil/Ore` 存量 `0/0/0 → 5/5/5` 单调增长（6 个变更点已逐行定位），但该字段为**存量快照**、探针未对 k3 建 gain 计数，写入来源未取证。⇒ 选项：**A** 只作线索、D920② 维持「真实产出支未取得」（推荐：与裁决一致，且不新增取证面）／**B** 追加一次只读专项，对 k3 建 gain 与 `ProductionSystem` 写入日志直读（代价＝再一轮进局）。**推荐 A**；影响＝B 会把 D920② 的举证面从「被测 k0 源」扩到「同型源」，与逐源独立验收口径冲突。
2. **H7（k0 卸货到账）的收口方式**：⛔ 替代证据已撤销，实测域内（420.0 游戏秒 · `k0CryBank` 91/91=0）未取得。⇒ 选项：**A** 维持「未取得」，与 `D920` Q4 已裁的「Ore 卸货路由项」并列挂观察（推荐）／**B** 申请一次受控可达性补证轮（近距工人＋短程目标，⛔ 不改 `taskTimeout`）／**C** 参照 `D911` 对 `CSS` 的「结构性阻塞」第三态处理。**推荐 A**（B 属新取证轮、需主策划定向；C 目前无「结构性」证据——本域内仅证明该窗未到账，⛔ 未证明机制不可达）。
3. **`gate.txt` 侧错误读数的处置**：⛔ 不回改证据文件正文（红线），⇒ 是否请事务端在台账对 `gate.txt:44` 以「原记录 ＋ 勘正注（指向报告 §13.3-④）」落地（`D906 Q6`／`D908 Q7` 先例）？**推荐**由事务端注记，执行端⛔ 不自行改证据。

### 13.9 补正轮产物清单（⛔ 零覆盖既有证据 · 全部新后缀 `_r2`）

| # | 文件（`Valley Rampart/Logs/`） | 字节 | 作用 |
|---|---|---|---|
| 1 | `hh341_small_mine_byproduct_stat_r2.py` | 6495 | N-B／N-C 主统计器（锚 `^## S <行型>` ＋字段全名 ＋三数 ＋逐矿／全局闭合差校验；先 `--dry` 空跑已过） |
| 2 | `hh341_small_mine_byproduct_stat_r2.txt` | 7118 | 上者输出（行型普查、`PROD_ROW`／`SNAP_ROW` 全量域含零计数、各段末行逐矿值与「与全局差」） |
| 3 | `hh341_small_mine_byproduct_stockdiff_r2.py` | 1218 | 矿内存量变更点逐行定位（`5/5/5` 定性依据） |
| 4 | `hh341_small_mine_byproduct_stockdiff_r2.txt` | 1190 | 上者输出（k3 六个变更点行号＋`SNAP.t`） |
| 5 | `hh341_small_mine_byproduct_objcount_r2.py` | 1060 | 内嵌对象数与 gain 读数计数（§13.3-⑤ 三数依据） |
| 6 | `hh341_small_mine_byproduct_objcount_r2.txt` | 185 | 上者输出（`363 → 0 → 363`、每行对象数分布 `{4:90, 3:1}`、gain `227/71/156`） |
| 7 | `hh341_small_mine_byproduct_gatecheck_r2.py` | 2269 | 闸门第 1~3 步实测器（`mtime`／len／sha256／blob ＋ `1038`／`≈` grep 复验 ＋ `diff --check` ＋ 跟踪性检查） |
| 8 | `hh341_small_mine_byproduct_gatecheck_r2.txt` | 见盘 | 上者输出（⭐ 定稿后重跑一次即为本轮终值记录，故字节数随重跑变化，不作确定值申报） |

⚠️ **自报瑕疵追加（commit message 计数失实）**：commit `41d5be12` 消息内写「新增补正统计器与输出 **5** 件」，⛔ 实测为 **8 件**（4 脚本＋4 输出，即上表）。消息已入库不回改（⛔ 不 `--amend`、⛔ 不重写历史），在此具名更正，**以本节清单为准**。
⚠️ 另：`_r2` 八件均落在 `Valley Rampart/Logs/`（命中 `.gitignore` ⇒ ⛔ 未被 Git 跟踪，与 `probe.txt`／`gate.txt` 同境），事务端复核须按 §13.4-4 以 **sha256＋`mtime`＋字节数** 三件核验，⛔ 不可依赖 `git show`。
