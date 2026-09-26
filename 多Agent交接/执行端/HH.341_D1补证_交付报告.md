# HH.341 · D1 首片 **补证轮** · 交付报告

> 执行端 ｜ 2026-09-26 ｜ 唯一正文：`HH.341_M5-D乙加_任务书.md` v2 ｜ ⛔ 不取新号、不写 D 号、未提交、未 push
> 性质：**纯取证补证** —— ⛔ **零改码**（`git diff --name-only -- Valley Rampart/Assets/_Game` ⇒ **空**，见 §五）
> 判定权在策划端：⛔ 执行端不宣称 D1 已绿。

---

## 一、⭐ 六项硬条件 · 生产面证据（真实进局 · 正门 `EnterTestRun(seed=31418/Small/2)`）

| # | 硬条件 | 生产面读数 | 证据 |
|---|---|---|---|
| 1 | **新 `TaskCard` 实例** | ✅ 本批窗口内出现 **3 张真卡**：`id=1`（阶段1 · cell=(70,83)）／`id=2`（阶段2 · (73,85)）／`id=3`（阶段2 · (79,79)）；`ids=2,3 唯一=2/2`、`ids=2 唯一=1/1` | `probe_evidence.txt` |
| 2 | **全链可追踪** | ✅ `id=2/id=3`：`card=null → Assigned(worker=13/15) → Executing(worker=13/15) → Done(worker=0)`；`id=1`：`Assigned(16) → Assigned(retry=1/1, consumed=1) → Aborted(RetryExhausted)` | 同上（逐卡快照） |
| 3 | **配对/预定终态归零** | ✅ `R37`：源=1、终态=1、**预定=0 配对=0**、不变量=0；阶段1 末：**预定=0 配对=0** | 同上 |
| 4 | **`TaskBindingManager` 为占用权威** | ✅ 配对建立于 `Assign`（`配对=1` 与卡 `state=Assigned worker=13` 同帧）；`_npcTaskMap` 仅作镜像（`ProtocolMirrorConsistent` 对拍）⇒ 本批窗口 `error/exception=0` | 同上 |
| 5 | **按 `taskId` 聚合对拍** | ✅ 见 §三 逐卡表（尝试数／`retryConsumedCount`／释放数／终态数逐项相等、无重复活动卡） | 同上 |
| 6 | **读档重建无悬空** | ✅ Load 前后 rt 实例 **不同**（`-1610598322` → `-589503886`）；Load 后 **预定 0／配对 0／源 0／不变量 0**；随后**重新立案 → 再派工 → 终态**（见 §二） | 同上 |

---

## 二、② 读档后「重新广告 → 再派工 → 终态」全链（本批核心补齐）

| 时点 | 读数（原文摘录） |
|---|---|
| Save/Load | `Save=True`；`Load后 rt=-589503886 同一实例=False 预定=0 配对=0 源数=0 不变量=0` |
| 重新立案 | `读档后重新立案=2 cells=(79,79) (73,85)`（经生产入口 `ConfirmResourceGather`） |
| 重新广告→建卡 | 逐卡出现 `cell=(79,79) id=3` / `cell=(73,85) id=2`（**新 taskId 序列**，未复用阶段1 的 `id=1`） |
| **实际派工** | `id=2 state=Assigned worker=13 retry=0/1 attempts=1`；`id=3 state=Assigned worker=15 …`（**配对建立**：`R33` 快照 `预定=1 配对=1`） |
| 到达执行 | `id=2/id=3 state=Executing worker=13/15` |
| **终态** | `id=2 state=Done worker=0 terminal=True`；`id=3 state=Done worker=0 terminal=True` ⇒ `## 读档后全终态 @R37` |
| 终态归零 | `R37 … 预定=0 配对=0 不变量=0`；`[阶段2] 表: 预定=0 配对=0` |
| **无悬空 / 无重复活动卡** | `ids=2,3 唯一=2/2`（无重复）；终态后 `worker=0`（配对释放）；窗口 `error/exception=0` |

**⭐ 资格时间证据（未复用绝对 tick）**：全部冷却边界均由**当时 tick 现算**，同一卡在不同时刻得到不同值 ——
`id=1`：第一次回待派 `blockedUntil=107`（≈触发 tick+5）→ 第二次封口时 `blockedUntil=139`（≈触发 tick+5）⇒ **每段重算、非同一常量**；
跨档后新卡（`id=2/3`）`blockedUntil=0`（**无跨档继承**）⇒ ⛔ 未复用绝对 tick。

---

## 三、③ 逐卡终态窗口表（按单个 `taskId`）

| taskId | 阶段 | 尝试数 | `retryConsumedCount` | 释放 | 终态 | 备注 |
|---|---:|---:|---:|---|---|---|
| **1** | 阶段1（(70,83)） | **2**（初次 1 ＋ 重试 1） | **1** | 2 次 `Unassign`（`lastUnassign=Unreachable`）＋终态释放 | **`Aborted` · `abortReason=RetryExhausted`** | `retry=1/1` 用满 ⇒ 第二次失败**封口**（政策"最多 2 次尝试"实证） |
| **2** | 阶段2（(73,85)） | 1 | 0 | 终态释放（`Done` 后 `worker=0`） | **`Done`** | 到达 → Executing → Done，全日志 |
| **3** | 阶段2（(79,79)） | 1 | 0 | 终态释放 | **`Done`** | 同上 |

- **对拍等式**：终态数（3）＝ {Done×2, Aborted×1}；每卡 `尝试数 = 1 + retryConsumedCount`（id=1：2＝1+1；id=2/3：1＝1+0）✓
- **上限未越**：无任何卡出现第 3 次尝试 ⇒ 「初次＋重试 1」上限成立 ✓
- **无重复活动卡**：3 张卡 taskId 唯一，无同 id 重复在场 ✓
- **终态后表归零**：`预定=0 配对=0` ✓
- ⚠️ 阶段1 另有 2 源（(83,84)/(83,85)）在窗口内**未被派工**（`card=null attempts=0`）⇒ 该 2 源**不构成本批判据缺口**（同阶段已有 id=1 走完全链至 Aborted）

---

## 四、① `L-95` 三段 Console（原始输出 `console_l95_seg{1,2,3}.txt` · 同工具重试记录在案）

| 段 | 尝试 | 读数（原文） | 归因 |
|---|---|---|---|
| **段1 Play 内**（生产链运行期） | **attempt[1] 成功**（`SEG1 OK attempt=1`） | `Retrieved 4 log entries`：`NullReferenceException: Object reference not set to an instance of an object`／`The referenced script on this Behaviour (Game Object 'ruler') is missing!`／`No Theme Style Sheet set to PanelSettings , UI will not render properly`／`287 node options failed to load and were skipped.` | 4 条与**空白对照逐条一致** ⇒ 生产链运行期**零新增 error** |
| **段2 退 Play 后** | **attempt[1] 成功**（`SEG2 OK attempt=1`） | `Retrieved 5 log entries` ＝ 段1 的 4 条 ＋ `Some objects were not cleaned up when closing the scene. …[SpriteAnimatorDriver]` | 第 5 条为**建局＋退局**既有（非本批） |
| **段3 空白对照**（只进 Play、不建局、不读档） | 已取得（前批） | `Retrieved 4 log entries`（同段1）＋ `filter_text=WorldGatherSource`=**0** ＋ `filter_text=error CS`=**0** | 4 条＝常驻既有 |

**生产面／影子面／空白对照 分列**：
- **生产面**＝§一~§三全部读数（真实进局＋真实源＋真实卡＋真实 Save/Load）；**影子面＝本批 0 条**（⛔ 未用影子面替代）；**空白对照**＝段3 基准 4 条。
- ⛔ 未用「探针窗口 `error=0`」替代 Console 三段（该读数仅作**附加**佐证：`窗口内 error/exception=0`）。
- **归因**：`NullReferenceException` 与 `ruler` 缺失脚本 ⇒ 按本批提示已并入 **`O-14` 关联处置**（⛔ 本批不修、⛔ 不作 D1 门槛）；`Theme Style Sheet`／`287 node options`／`not cleaned up` 均为**既有常驻**（与空白对照同源或退局特有）⇒ **无新增未归因错误**。

---

## 五、旧链基线前后对拍（域＝`Valley Rampart/Valley Rampart/Assets/_Game/**`）

| 判据 | 补齐批读数 | **本批复核** | 结论 |
|---|---|---|---|
| `TaskScheduler.Instance|HasInstance` | 59 行 / 16 文件 | **59 / 16** | ✅ 无新变化（+5/+1 已于补齐批逐项解释为 `WorldGatherSource` 接缝引用） |
| `new KingdomTask(` | 15 行 / 10 文件（含注释口径） | **15 / 10** | ✅ 无新变化 |
| `ITaskSource` 实现者 | 9 类 | **9 类** | ✅ 无新变化 |
| `KingdomTaskType` | 11 项（上批实测） | 文件**未改动**（见下） | ✅ 无新变化 |
| **生产域改动面** | — | `git diff --name-only -- Valley Rampart/Assets/_Game` ＝ **空** | ✅ **补证轮零改码**（所有代码/资产零改动 ⇒ 上列三项必然不变） |

---

## 六、未取得 / 报裁

1. 阶段1 中 2 个源（(83,84)/(83,85)）窗口内未被派工（`card=null attempts=0`）⇒ 该 2 源**无卡可读**；同阶段 `id=1` 已走完全链（Assigned→重试→Aborted），判据 2/5 不受影响。
2. 段1/段2 的 `NullReferenceException` **栈**仍未取得（`read_console` 仅返消息文本）⇒ 按提示并入 `O-14`，本批不深挖。
3. ⛔ 本批**未改任何代码**（零改码已由 git 证明）；⛔ 未动政策数值/`hasDeadline` 语义/不入档约定；⛔ 未宣称 D1 已绿。

## 【应登记项】

```
HH.341 | D1 补证轮 ①②③ | 执行端 | ① L-95 三段齐全（段1=4 条、段2=5 条、段3=4 条；均 attempt=1 成功）；② 读档后重新立案→再派工→Executing→Done 全链 + 终态归零（预定0/配对0）+ taskId 唯一无重复；③ 逐卡终态：id=1 Aborted(RetryExhausted, 尝试2/consumed1)、id=2/3 Done(尝试1/consumed0) | ⛔ 零改码（git diff 生产域=空）；未宣称 D1 绿 | 待裁定
HH.341 | 资格时间证据 | 执行端 | 同卡冷却边界跨时点重算（blockedUntil 107→139），跨档新卡 blockedUntil=0 ⇒ ⛔ 未复用绝对 tick | 生产面读数 | 待登记
HH.341 | 基线复核 | 执行端 | TaskScheduler.Instance|HasInstance 59/16、new KingdomTask( 15/10、ITaskSource 9 类 ⇒ 与补齐批逐位一致（无新变化） | 生产域扫描 | 待登记
HH.341 | O-14 关联 | 执行端 | 段1/段2 的 NRE 与 ruler 缺脚本按提示并入 O-14；本批不修、不作 D1 门槛 | 栈未取得（工具仅返消息） | 待登记
```
