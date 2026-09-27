# HH.341 · 小源首源 `ConstructionSiteStore` —— **D892 速度链只读定位** 交付报告

> 执行端 ｜ 2026-09-27 ｜ 沿用 `HH.341`（⛔ 未取新号、⛔ 未写 D 号）｜ 裁定：`D892`（本轮＝专项定位实际速度来源）
> 性质：⭐ **只读定位**（⛔ 不判绿、⛔ 不出修法、⛔ 不自行定性根因）｜ ⛔ 未提交、未 push
> 证据：`Valley Rampart/Logs/hh341_small_construction_site_speed_{probe.txt,probe.cs,probe.json,git.txt}`

---

## 〇、结论速览（只报读数）

| 项 | 读数 |
|---|---|
| **闭合链** | `data.walkSpeed=3` → `ScaleUnitSpeed(3)=0.716` → `WalkSpeed=**0.716**` → `EffectiveSpeed=**0.716**` → `step=0.716×dt` → `MoveTowards` ⇒ **各环数值闭合、⛔ 无异常缩放** |
| ⭐ **实际断点（读数指向，⛔ 不定性）** | ① `Time.deltaTime` 实测 **0.295~1.8859 s/帧（fps≈0.5~3.4）**；② 段4/段5 `PathFollower.State=**Idle**` ∧ `_wpIndex=**0**` ∧ 位移 **0**（**路径已耗尽但 `wState` 仍 `MovingToSource`**） |
| 段内测速（⭐ 取代全窗均速） | 段1 **0.082**／段2 **0.289**／段3 **0.343**／段4 **0.088**／段5 **0**（世界单位/秒）；段时长 30.09~31.26s；`Following` 有效时长 3.6~12.8s |

---

## 一、六项必确认（逐项 `file:line` ＋ 原始读数）

### 1️⃣ 本局走哪条路径 —— **新建路径** ✅`[实读]`
- 本局 = `EnterTestRun` **新建局**；实测 `ScaleUnitSpeed(data.walkSpeed) = 0.716` **与运行时 `WalkSpeed = 0.716` 相等** ⇒ 生效路径 = `UnitController.cs:390-393`（新建），⛔ 非 `:526-529`（读档）。
- **⚠️ 校正 D891 推算**：`data.walkSpeed` 实测 **= 3**（⛔ 非 5）⇒ `ScaleUnitSpeed(3)=0.716×3/3=0.716`；D891 的「0.716×5/3≈1.193」前提**不成立**。

### 2️⃣ 各单位速度实际值 ✅`[实读]`
| 来源 | 读数 |
|---|---|
| `MovementConfig.asset`（`MovementConfig.cs:34-35` 公式） | `npcSpeed=**0.716**` `unitSpeedRefData=**3**`（`Instance=True`） |
| `UnitData`（运行时 `uc.Data`） | `walkSpeed=**3**` `runSpeed=**6**` `faction=PlayerCamp` |
| 运行时 `UnitController` | `WalkSpeed=**0.716**` `RunSpeed=**1.432**`（`:392-393` 后） |
| 存档字段（`UnitSaveData.walkSpeed`） | **未取得**（本局为新建局，读档路径 `:528` 未生效 ⇒ 非本局生效链）⚠️ |

### 3️⃣ 修正因子 ✅`[实读]`／⚠️`[未取得]`
| 因子 | 读数 |
|---|---|
| `RaceDef(k0).moveSpeedMul` | **1**（`rd0 != null`） |
| `Frenzy.SpeedMul()` | **未取得**（按名反射 `SpeedMul` 无命中 ⇒ 标未取得，⛔ 不推导） |
| Slow（`_slowFactor`／`_slowUntil`） | `_slowFactor=**0**`、`_slowUntil=**0**`、`now=38.5` ⇒ **不在减速期** |

### 4️⃣ `PathFollower` 实际读数 ✅`[实读]`／⚠️`[未取得]`
| 项 | 读数 |
|---|---|
| `_speedOverride` | **0.716**（⚠️ 非 0 ⇒ 会走 `EffectiveSpeed(speedOverride)`，但该值**恰等于** `WalkSpeed` ⇒ **不构成额外降速**） |
| `State` | `Following`（段1~3）→ **`Idle`（段4/段5）** |
| `_wpIndex` | 1→6→31→37→62（**递增 = 路径推进正常**）；⚠️ 段4/段5 = **0** |
| `_consecutiveFails` | **0**（全程 ⇒ 无 PathFailed/无重寻） |
| `waypoints.Length` | **未取得**（`_path` 为 `PathResult` 值类型，按名反射未命中）⚠️ |

### 5️⃣ 逐 Build 段内测速（⛔ 不用全窗平均） ✅`[实读]`
| 段 | 段开始（派发 T0 = `_taskStartTime`） | 段时长（≈超时点） | **段内位移** | **段内均速** | `Following` 有效时长 | 采样 `dt` | 段内格轨迹 |
|---|---|---:|---:|---:|---:|---|---|
| 1 | t=40.42（T0=40.42233） | 31.26s | 2.560 | **0.082** | 3.6s | **1.8859** | (66,62) 起点 |
| 2 | t=74.34（T0=74.34082） | 30.09s | 8.689 | **0.289** | 10.4s | 1.3396→0.4041 | (71,67)→(77,72) |
| 3 | t=123.67（T0=123.668） | 30.20s | 10.354 | **0.343** | 12.8s | 0.6858→0.3276→0.6120 | (95,80)→(104,92) |
| 4 | t=155.15（T0=155.1549） | 30.84s | 2.720 | **0.088** | 3.6s | 0.2952→1.7062 | (109,96)，`State=Idle` |
| 5 | t=196.60（T0=196.6009） | （进行中） | 0.000 | — | — | 0.7322 | (109,96)，`State=Idle` |

- 全程段内 `可走=True`、`wState=MovingToSource`、`WalkSpeed=0.716`、`Eff=0.716`（**每采样均在案**）。

### 6️⃣ 闭合链（raw → … → 位移）＋断点标注 ✅`[实读]`
```
data.walkSpeed = 3                                   （UnitData，实读）
  ↓ MovementConfig.ScaleUnitSpeed = npcSpeed × (data/unitSpeedRefData)   （MovementConfig.cs:34-35）
0.716 × (3/3) = 0.716                                （实读，与运行时相等）
  ↓ UnitController.cs:390-393（新建路径）
WalkSpeed = 0.716                                    （实读）
  ↓ EffectiveSpeed(base)（UnitController.cs:187-201；raceMul=1 / frenzy=未取得 / slow=0）
EffectiveSpeed(0.716) = 0.716                        （反射调用 private，实读）
  ↓ UnitController.cs:1273-1276
step = 0.716 × Time.deltaTime                        （dt 实测 0.295~1.8859 s）
  ↓ :1279 Vector2.MoveTowards → :1283 _rb.MovePosition
位移（实测段内 0.000~10.354 / 30s）
```
**⚠️ 断点标注（⛔ 只标位置，不定性）**：
- **速度链各环数值闭合**（无乘法异常、无二次缩放、无 slow/frenzy/race 衰减）；
- 观测到的两处**非速度链**因素：**(a) `Time.deltaTime` 极大（0.295~1.886 s/帧，fps 0.5~3.4）**；**(b) 段4/段5 `PathFollower` 已经 `State=Idle`、`_wpIndex=0`（路径耗尽）而旧链 `wState` 仍为 `MovingToSource`** ⇒ 该段内**无路径可跟随** ⇒ 位移 0 ⇒ 段预算耗尽。

---

## 二、三类归属（⛔ 不用「大概/应该」填空）

| 类别 | 项 |
|---|---|
| **已确认**（本次实读） | 新建路径生效；`npcSpeed=0.716`；`unitSpeedRefData=3`；`data.walkSpeed=3`；`data.runSpeed=6`；`WalkSpeed=0.716`；`RunSpeed=1.432`；`ScaleUnitSpeed(3)=0.716`；`RaceDef.moveSpeedMul=1`；`_slowFactor=0`；`_slowUntil=0`；`EffectiveSpeed(0.716)=0.716`；`PathFollower._speedOverride=0.716`；`State` 序列（Following→Idle）；`_wpIndex` 序列；`_consecutiveFails=0`；5 段的 T0/时长/位移/均速/Following 时长/dt 采样 |
| **未取得** | `Frenzy.SpeedMul()`（反射方法名未命中）；`waypoints.Length`（`_path` 为 `PathResult` 值类型，按名未命中）；`UnitSaveData.walkSpeed` 存档字段（本局新建 ⇒ 非生效链，未读） |
| **工具/窗口不足** | 段内 `dt` 为**采样瞬时值**（非段内均值）；本轮窗口 240s 覆盖 5 段（若需更多段需延长窗口）；`_path` 内部结构未展开 |

---

## 三、最小变量声明
- **保持不变**：`seed=424242`、投料目标（玩家主城）、取料仓（玩家国库）、政策值（`taskTimeout=30f` 等 ⛔ 未动）、代码/资产（⛔ 零改动）。
- **本批唯一变更**：新增**观测维度**（速度链 6 项 + 段内 0.2s 高频测速），⛔ 未改任何运行条件；⛔ 未使用受控替身；⛔ 未并入 `DZ-7`。

---

## 四、合规与回退点
- **零改码证据**：`git diff --name-only -- Valley Rampart/Assets/_Game` ⇒ **命中行数 0**；`Assets` 全域仅既有脏点 `Scenes/GameScene.unity`（mtime 2026-09-15，⛔ 非本批）。HEAD = **`9913ec0d`**（D892）。
- **回退点**：HEAD `9913ec0d`；本批**无代码改动** ⇒ ⛔ 无需回退动作。
- 日志落 `Valley Rampart/Logs/hh341_small_construction_site_speed_*`（⛔ 非仓库根 `Logs/`，⛔ 未覆盖他批证据）。
- ⛔ 未动 `Building`／`UnitController` 字节面／场景／资产／`AI.Core`／账本；⛔ 未改 `taskTimeout` 与两段预算；⛔ 未改 `Complete` 判据；⛔ 未声称 §九-6 达成。

## 五、源级结论
⛔ **仍未通过 · 保持「停手待裁」**（本批只读定位，⛔ 不判绿、⛔ 不出修法、⛔ 不定性根因）。
本批已如实取得 `D892` 要求的 6 项读数与闭合链，并指出两处**非速度链**的读数与断点位置。

## 【请裁】
1. ⭐ 读数显示：**速度链已闭合（0.716 → 0.716，无衰减）**，而 **`dt` 极大（0.5~3.4 fps）+ 段4/5 路径耗尽（`State=Idle`/`_wpIndex=0`）** ⇒ 请裁是否将下一轮定位于 **(a) 帧率/`Time.deltaTime` 环境** 或 **(b) `PathFollower` 路径耗尽后的"未到达"判定**（两者均在首源协议状态机**之前**）。
2. `Frenzy.SpeedMul()` 与 `waypoints.Length` 项**未取得**（反射名不符）⇒ 若仍需该两项，请裁是否允许**扩大只读反射范围**（⛔ 不改码）。
3. `UnitSaveData.walkSpeed` 存档字段本轮未读（新建局非生效链）⇒ 若需"新建 vs 读档"两路径对照，请裁是否另开**读档局**只读取样。
