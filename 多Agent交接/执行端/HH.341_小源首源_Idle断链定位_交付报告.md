# HH.341 · 小源首源 `ConstructionSiteStore` —— **D894「Idle ↔ MovingToSource 断链」只读定位** 交付报告

> 执行端 ｜ 2026-09-27 ｜ 沿用 `HH.341`（⛔ 未取新号、⛔ 未写 D 号）｜ 裁定：`D894`（采纳 (b) ＋ 准扩大只读反射 ＋ 驳回读档对照）
> 性质：**只读定位**（⛔ 不判绿、⛔ 不出修法）｜ ⛔ 未提交、未 push ｜ 依据 `D894` 三分栏要求

---

## 〇、判据结论（**已证 / 已排 / 未取得** 三分栏）

| 分栏 | 内容 |
|---|---|
| ⛔ **已证断链** | **无**。臂 R 全程 `Idle ∧ MovingToSource 同时出现的采样数 = **0**`（`D894` 要求"段内 `State=Idle` ∧ `wState=MovingToSource` ∧ 位移≈0" ⇒ **该组合未出现**） |
| ⭐ **已排（本轮范围内 · 有强读数支撑）** | 停滞机制**不在"到达判定"层**：段3 停滞时 **两套距离一致地远超阈值** —— `DistToDest=**3.074**`（跟随器判据，`IsArrived(_destination)` ⇒ 假）、`DistToSourcePos=**3.392**`（调度器判据，阈值 0.384）⇒ **两侧都认为"未到达"**，**不存在"一侧以为到了、另一侧以为没到"**；实际停滞态为 `PathFollower.State=**Repathing**`（`PathFollower.cs:145-150` 下一路径点 `!IsSubWalkable` ⇒ Repathing）＋ `_wpIndex=**0**`（恒不推进）＋ 段内位移 **0.000** ＋ `_consecutiveFails=**0**` |
| ⚠️ **未取得** | ①**臂 C（1x）全部读数**（bridge 调用 `Operation timed out`，产物未生成）；②第 6/7 项**调用计数**（`NavigateToSource`(`:460`)/`EnsurePfAndSetDest`(`:478`)/`BehaviorExecutor.NavigateTo`(`:331-338`)/`Brake()`(`:344-349`)）——**无改码不可埋点 ⇒ 结构性不可得**；③`Frenzy.SpeedMul()`（按名反射未命中，本轮仍 `<未取得>`）；④`_path.waypoints.Length`（`Rd` 取到 `GridCoord[]` 类型名，未取长度） |

⚠️ **鉴别力声明**：两臂**预期不同**（15x 下 `taskTimeout=30` 对应 **2 现实秒**；1x 下对应 **30 现实秒**，且 1x 的 `Time.deltaTime` 反映真实帧间隔）⇒ 双速对照**具备鉴别力**；但**臂 C 未取得** ⇒ **对照未完成**，本报告结论**仅基于臂 R**。

---

## 一、本批读数（臂 R · 15x · seed 424242 · 同目标/同取料仓/同遴选口径）

**运行态**：`timeScale=15` ✓；对照工人 `npc`（口径同 `D891`：idle ∧ 起步格可走 ∧ `workerState=None`）；`_speedOverride=0.716`、`WalkSpeed=0.716`、`_slowFactor=0`、`Frenzy.SpeedMul=<未取得>`。

### ⭐ 三点对照（`D894` 第 1/3 项）
| 点 | 值 |
|---|---|
| `task.SourcePos`（调度器判据基准） | **(8.32, 66.24)** |
| `SpawnPosSnapper.SnapWorld(SourcePos)`（预吸附点） | **(8.32, 65.92)** |
| `PathFollower._destination`（最终吸附点） | **(8.32, 65.92)** |
| **Δ(_destination, SourcePos)** | **0.320**（全程恒定） |

⇒ **吸附确实发生**（y 方向偏移 0.32 世界单位），`_destination` 与 `SnapWorld` 结果**逐位一致**。

### ⭐ 两套到达距离（`D894` 第 4 项 · 同帧并取）
| 时点 | `DistToDest`（跟随器） | `GridDistToDest`（格归一化） | `DistToSourcePos`（调度器 · 阈值 0.384） | 判读 |
|---|---:|---:|---:|---|
| 段1 t=52.02 | 22.884 | 34.078 | 23.184 | 双方均"未到达" |
| 段2 t=78.93 | 15.758 | 24.157 | 16.070 | 同上 |
| 段2 t=98.20 | 7.450 | 11.641 | 7.770 | 同上 |
| **段3 全程（4 采样）** | **3.074** | **4.780** | **3.392** | **双方仍均"未到达"；位移 0.000** |

### 段内测速与状态（`D894` 第 2/5 项）
| 段 | 时长 | 位移 | 均速 | `PF.State` | `_wpIndex` | `fails` | `_path.status` |
|---|---:|---:|---:|---|---:|---:|---|
| 1 | 31.35s | 8.362 | 0.267 | `Following` | 12 | 0 | **`Partial`** |
| 2 | 30.61s | 19.059 | 0.623 | `Following` | 6→105 | 0 | `Ready` |
| **3** | 30.64s | **0.000** | **0.000** | **`Repathing`（全程）** | **0（恒）** | **0** | `Ready` |

**汇总**：`PF 状态变化次数=44`；**`Idle ∧ MovingToSource 同现采样=0`**；卡 `id=1 → Aborted(RetryExhausted) fails=2`；表 `预定=0 配对=0`。

---

## 二、读数级复现命令（⛔ 零改码）

```powershell
# 1) 进 Play（bridge editor 模式会被拒 ⇒ 以"被拒"反推已在 Play；或先跑 exitplay 再 ping）
pwsh -NoProfile -File 'Logs\_bridge_call.ps1' -Payload 'Valley Rampart\Logs\hh341_small_construction_site_ping.json'
# 2) 臂 R（MODE = "R15x" ⇒ speedOverride 用配置 15x）
pwsh -NoProfile -File 'Logs\_bridge_call.ps1' -Payload 'Valley Rampart\Logs\hh341_small_construction_site_idle_probe.json' -TimeoutMs 900000
# 3) 臂 C：把探针内 `const string MODE = "R15x"` 改为 `"C1x"`（⇒ SPEED_OVERRIDE=1f）后重跑步骤 1–2
# 产物：Valley Rampart\Logs\hh341_small_construction_site_idle_{R15x|C1x}.txt
```

---

## 三、`file:line` 锚点（本轮判读依据）

| 锚点 | 内容 |
|---|---|
| `PathFollower.cs:117-121` | `Following ⇒ FollowNext()` 驱动 |
| `PathFollower.cs:129-133` | `IsArrived(_destination)` ⇒ **`Complete()`**（`:157-163` ⇒ `_state=Idle`、`_path=null`、`_wpIndex=0`）——"Idle"的合法来源之一 |
| `PathFollower.cs:134-140` | `_wpIndex >= waypoints.Length`（路径耗尽）⇒ 直走 `_destination`；到达 ⇒ `Complete()`；否则 `RequestPath()` |
| **`PathFollower.cs:145-150`** | ⭐ 下一路径点 `!grid.IsSubWalkable(wp)` ⇒ **`_state=Repathing`**、`_stateStartTime=now`（**本轮停滞态**） |
| `PathFollower.cs:108-115` | `Repathing` 冷却后 `RequestPath()`（重寻；对应本轮 `状态变化 44 次`） |
| `PathFollower.cs:42-52` | `SetDestination` 内 **`SpawnPosSnapper.SnapWorld`** ⇒ `_destination`（本轮 Δ=0.320 来源） |
| `PathFollower.cs:85-93` | `Stop()` ⇒ Idle（**与 `Complete()` 签名同为 void ⇒ 需靠 `_path/_wpIndex` 差异区分**） |
| `PathFollower.cs:175-192` | `FailOnce()`：`fails >= maxConsecutiveFails` ⇒ `Failed` ＋ `PathFailedEvent`（本轮 `fails=0` ⇒ **未触发**） |
| `TaskScheduler.cs:161-165` | `GetWorkerState`（`wState` 读数口） |
| `TaskScheduler.cs:1564-1569`（D894 给定） | 调度器侧到达阈值 `0.3 × 1.28 = 0.384` 世界单位（本轮 `DistToSourcePos` 最小 3.392 ⇒ 未达） |

---

## 四、合规声明与自报瑕疵

**合规**：
- **零改码**：`git diff --name-only -- Valley Rampart/Assets/_Game` ⇒ **0**；`Assets` 全域仅既有脏点 `Scenes/GameScene.unity`（mtime 2026-09-15）；Assets 未跟踪新增 **10**（均为会话前既有探针/美术残留，⛔ 非本批）。
- ⛔ 未改 `taskTimeout`／两段预算／`Complete` 判据；⛔ 未并 `DZ-7`；⛔ 未开读档局；⛔ 未碰巨兽/场景/资产/`AI.Core`/账本；⛔ 未代写策划端账本（`D767`）。
- **回退点**：HEAD **`cf1d0c4d`**（本批无生产码改动 ⇒ ⛔ 无需回退动作）。
- 日志：`Valley Rampart/Logs/hh341_small_construction_site_idle_{R15x.txt,probe.cs,probe.json}`（⛔ 非仓库根 `Logs/`）。

**关于"探针位置"的声明（自报）**：`D894` 授权"新增只读探针放 `Assets/Editor/Smoke/` ＋ `.meta`"；本批实现选择＝**bridge 顶层只读脚本**（落 `Valley Rampart/Logs/`），⛔ **未写入 Assets** ⇒ 因此**不触发生产程序集变更**、`_Game` 与资产 diff 均为 0。若策划端要求 Editor 探针**存档留证**，可另落盘（⛔ 仍不改生产码）。

**自报瑕疵（3 条）**：
1. **臂 C 未取得**（bridge `Operation timed out`，产物未生成）⇒ 双速对照**未完成**，本报告结论仅基于臂 R，**置信度受限**；
2. **第 6/7 项调用计数未取得**：`NavigateToSource`/`EnsurePfAndSetDest`/`NavigateTo`/`Brake` 的调用计数需**埋点** ⇒ 在零改码约束下**结构性不可得**（仅能用 `状态变化 44 次`、`_wpIndex` 轨迹等**间接**读数近似）；
3. 本轮**未复现** `D893` 的 `State=Idle` 观测 ⇒ 停滞态出现 `Repathing` 与 `Idle` **两种**形态，本报告**未判定两者关系**（⛔ 不自行定性）。

---

## 五、源级结论
⛔ 首源**仍未通过**（`Complete` 四项未取得 · 本批只读定位）⇒ 保持「**停手待裁**」。
本批如实交付：三分栏（已证＝无／已排＝"停滞不在到达判定层，而在 `Repathing` 动态阻挡路径层"／未取得＝4 项），并**未申请下一个源**。

## 【请裁】
1. **臂 C 是否必须补**（若必须：请裁是否允许**放宽本次 bridge 超时**或**缩短臂 C 采样段数**，⛔ 仍零改码）；
2. **第 6/7 项调用计数**：是否授权**新增只读计数探针**（需改探针侧挂接，⛔ 不改生产码）——否则该项应记为**结构性不可得**；
3. 下一轮定位方向：本批读数指向 **`PathFollower.cs:145-150`（`IsSubWalkable` 动态阻挡判定）+ 重寻无效（`_wpIndex` 恒 0）** ⇒ 是否按此定向下发（仍在**首源协议状态机之前**）。
