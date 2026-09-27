# HH.341 · 小源首源 `ConstructionSiteStore` **零改码复跑**交付报告

> 执行端 ｜ 2026-09-27 ｜ 唯一正文：`HH.341_M5-D乙加_小源阶段任务书.md` ｜ 裁定：`D887`（取「② 零改码复跑」，**不得判绿**）
> ⛔ **本批零改码**（`_Game` diff 除前批遗留改动外无新增）｜ ⛔ 未提交、未 push ｜ 证据：`Valley Rampart/Logs/hh341_small_construction_site_retry*`

---

## 〇、结论速览（⛔ 诚实报"未取得"）

| 项 | 结果 |
|---|---|
| 轮1 | 1-A：**前置不足自主中止**（idle 工人=0 ⇒ 未触发、未产卡）；1-B：id=1 走 **Abort** 路封口 |
| 轮2 | id=1 走 **Abort** 路封口 |
| ⭐ **`Complete` 四项合格证据** | **两轮均未取得** |
| ⭐ **真实失败原因（关键更正）** | 两轮**均为 `[TaskScheduler] Abandon Build → Timeout`**（**⛔ 非 `Unreachable`/寻路失败**） |
| 源级结论 | **未通过 · 停手待裁**（按 `D887` 停手条件 2） |

---

## 一、每轮方案（含可达性依据 · 任务书 §六-1）

| 轮 | seed | 投料目标 | 触发前置（运行前确认） | 可达性依据 |
|---|---|---|---|---|
| 1-A | 20250927 | castle **(58,104)** k0 | 取料仓 √（`Vault Stone=100 Wood=100`）；**可派工 idle 工人 = 0** ⇒ ⛔ **中止（未触发）** | 无 idle 工人 ⇒ 触发也无人接 ⇒ 按设计不触发 |
| 1-B | 20250927 | castle (58,104) k0 | 等 **1s** 后 `idle=4`；取料仓 √ | 换前置条件：**等待 idle 工人出现**后再触发（1-A 的失败点） |
| 2 | **424242** | castle **(108,97)** k0 | 择机 **55s**：最近工人 **9.5 格**、`idle且≤10格=1`；取料仓 √（`Vault Stone=100`） | ①换 seed（D887 要求）②**择机触发**：轮询等待"工人距主城最近且空闲"的最佳时机（1-B 实测失败原因＝Timeout ⇒ 与距离/时序强相关） |

**为什么换 seed/目标有依据**：1-B 实测旧链 `Abandon Build → Timeout`（30s 走不到）⇒ 与**距离与时序**直接相关 ⇒ 轮2 同时收紧两处：换 seed（新地形/新主城位形）＋ **择机触发**（把"工人距目标 31.6→25.7→17.7→**9.5 格**"的收敛过程记为触发依据）。

---

## 二、⭐ 四项合格证据（任务书 §二）逐项读数

| 项 | 轮1-B | 轮2 | 判定 |
|---|---|---|---|
| ① 新卡触发 `Complete` 接缝 | ⛔ 未触发（无 `Done`） | ⛔ 未触发（无 `Done`） | **未取得** |
| ② 卡由 `Resolving` 收敛到 `Done` | ⛔ 卡终态＝`Aborted(RetryExhausted)` | ⛔ 同 | **未取得** |
| ③ 配对/预定/镜像归零 | ✓ `预定=0 配对=0`（终态后） | ✓ `预定=0 配对=0`（S80 起） | 取得（但属 **Abort 路**） |
| ④ 协议悬空/不变量＝0 | ✓ `不变量=0 悬空=0` | ✓ `不变量=0 悬空=0` | 取得（但属 **Abort 路**） |

**两轮逐卡读数**：

| 轮 | 目标 | 新卡 id | 派工 | 重试 | 终态 | 表归零 |
|---|---|---|---|---|---|---|
| 1-B | castle(58,104) | **id=1**（不复用上一局 id 值域外的既有卡 ✅ 该会话新卡） | `预定=1 配对=1` | `retry=1/1 cons=1 fails=2` | `Aborted(RetryExhausted) blk=318` | ✓ |
| 2 | castle(108,97) | **id=1**（新会话新序列） | S20 `预定=1 配对=1` → S40 `配对=0` → S60 `配对=1`（**重派**） | `retry=1/1 cons=1 fails=2` | `Aborted(RetryExhausted) blk=415` | ✓ |

⛔ **四项缺一 ⇒ 两轮均不算取得**。
✅ 附带取得（同批读数）：**重派证据**（轮2 S40→S60 配对 0→1，`blockedUntil=367` 后重派）、**Abort 封口**、**表归零**、**悬空/不变量 0**、`窗口 error/exception = 0`。

---

## 三、与上一局 `Unreachable` 的对比分析（任务书 §六-3）

| 维度 | 上一局（首源施工批 · seed=31418） | 轮1-B（20250927） | 轮2（424242） |
|---|---|---|---|
| 旧链真实原因（日志） | `Abandon Build → SourceInvalid`（料齐后收口）＋ 卡侧 `lastUnassign=Unreachable` | ⭐ **`Abandon Build → Timeout` ×3** | ⭐ **`Abandon Build → Timeout` ×2** |
| 卡侧 `lastUn` | `Unreachable` | `Unreachable` | `Unreachable` |
| 目标距离 | 未记录 | 工人↔(58,104) ≈ **24.8~28.7 格** | 触发时 **9.5 格** |
| 结果 | `Aborted(RetryExhausted)` | 同 | 同 |

### ⭐ 关键更正（必须登记的读数偏差）
**卡侧 `lastUnassignReason=Unreachable` ≠ 真实失败原因**：真实原因是旧链的 **`Timeout`**。
根因＝源侧接缝的**阶段白名单降级**：`TaskLifecycleRules.IsLegalUnassign(Assigned, Timeout)=false` ⇒ 源侧 `MapUnassignReason` 在 `Assigned` 阶段把 `Timeout` 降级为 `Unreachable`（该降级是为避免首片实读教训中的 `Unassign(IllegalReason)`）。
⇒ **影响**：卡侧读数字段**丢失真实原因信息**。⛔ 本批零改码 ⇒ **不改**；**提请裁定**是否在后续批为协议侧补"真实原因留痕"（不属本批允许面变更）。

**为什么两轮仍未取得 `Complete`**：
- 旧链日志证明 **不是寻路不可达**（否则应为 `Unreachable`/`PathFailed`），而是 **`Time.time - _taskStartTime > taskTimeout(30s)`** 的**段预算超时**；
- 轮2 已把触发时机压到"工人距目标 **9.5 格**"，仍在 30s 内未完成第一段 ⇒ 证据指向**工人被反复打断/实际移速**（环境与时序），**⛔ 无代码缺陷证据**（与 `D887` 判断一致）。

---

## 四、合规与边界

- **零改码**：本批仅执行 bridge 脚本与 rg/读取；⛔ 未改 `_Game`／资产／场景／`AI.Core`／账本／政策值（`retryMax`／5 tick／`taskTimeout` 均未动）；⛔ 未扩 `ITaskScheduler`；⛔ 未动其余四源／两巨兽。
- 前置基线（应保持不变 · 本批未复扫因零改码）：`TaskScheduler.Instance|HasInstance` **64/17**、`new KingdomTask(` **15/10**、`ITaskSource` **9 类**。
- 证据目录：`hh341_small_construction_site_retry1_probe.txt`／`retry1b_probe.txt`／`retry2_probe.txt`（＋脚本与 payload；⛔ 未覆盖前批 `…_probe.txt`／`…_l95_seg*`）。
- ⛔ `L-95` 三段：本批为**复跑取证**，未新增 Play→退 Play 三段（前批三段已在案）；⛔ 未用任何降级替代品充当原文。

---

## 五、源级结论：**未通过 · 停手待裁**（按 `D887` 停手条件 2）

**失败判据**：`D887` §一/§二要求"两轮内取得 `Complete` 四项合格证据"；**两轮均未取得** ⇒ 触发 **§四-2 停手**。
**已具备的支撑证据**：两轮旧链日志均为 **`Abandon Build → Timeout`**（⛔ 非 `Unreachable`）⇒ 属**外部可达性/时序阻断**，**⛔ 无代码缺陷证据**。

**请裁其一（⛔ 执行端不自选边）**：
1. **接受**：以"Timeout 属环境阻断 + 三路（Abort／超时失败／读档）归零 + 悬空/不变量 0"作为本源通过口径（`Complete` 路另立专门的**受控可达性**取证，例如由策划端指定更大的段预算或指定目标类型）；
2. **继续取证**：授权第三轮，但⛔ 不得改码 ⇒ 只能进一步调整**触发时机/目标**（可尝试：等工人**紧贴**目标（≤3 格）再触发、或改用**玩家新建**的非废墟建筑作为投料目标——后者需策划端明确"是否允许新建建筑作为目标"）；
3. **改判据**：若策划端认定本源 `Complete` 路在现有政策（`taskTimeout=30s`）下**不可稳定取得**，则需重新定义本源验收口径（⛔ 执行端不得自行改判据）。

## 【应登记项】

```
HH.341 | 首源 ConstructionSiteStore 零改码复跑（D887 ②） | 执行端 | 轮1-A 前置中止（idle=0）/轮1-B(id=1 Abort)/轮2(seed424242 · 择机9.5格 · id=1 Abort) ⇒ **Complete 四项均未取得** | ⭐ 真实原因＝旧链 `Abandon Build → Timeout`（⛔ 非 Unreachable）；表归零/悬空0/不变量0/窗口 error 0 | **停手待裁**（D887 §四-2）
HH.341 | 读数偏差登记 | 执行端 | 卡侧 `lastUnassignReason=Unreachable` 与真实 `Timeout` 不符：源侧按阶段白名单把 `Assigned+Timeout` 降级为 `Unreachable`（避 `IllegalReason`）⇒ **卡侧丢失真实原因** | 本批零改码未改；请裁是否后续补"真实原因留痕"
HH.341 | 复跑前置门实测 | 执行端 | `ConstructionSiteStore` 一次投料期需"可取料仓 ＋ 可派工 idle 工人"；玩家侧唯一建筑为主城（**Abandoned**）⇒ 目标不可替换 ⇒ 可达性/时序完全由环境决定 | 供裁定第 2/3 选项依据
```
