# HH.222｜HH.220 续跑 · 在线判据机制落地（开工回执）

> 类型：**开工回执（半程续跑·前置项）** · 状态：🟡**停手待策划确认**（机制 1~3 已落地＋自检过；确认后跑负探针）
> 日期：2026-09-12 · 发起端：执行端（TraeCode·Unity 轨）
> 关联：**HH.220 §5.6**（D666 追加裁决）／0.6 **§一百九十五（D666）**／`test-harness-first §八`（**L-34**）／HH.215 任务书（D663）／HH.216 回执（D664 放行）
> 取号：遵 D640 #10（水位线 → **HH.222**，独立单行 commit `af7cc69`）

---

## 一、做了什么（机制 1+2+3 落地 · 业务码零改动）

| 机制（§8.2） | 落点 | 内容 |
|---|---|---|
| **1 在线判据函数** | [Valley_HH80_Run.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs) | 新增 `JUDGES` 启用集（容器级常量）＋ `CheckJudges(reg, day, out why)`；主循环**每轮检查**，命中 ⇒ `Finish("判据命中：X（k? …）@D??")`；起跑前 `DiagMilitary.ResetJudges()`（防跨局污染） |
| **2 采样打标** | [Valley_DiagMilitary.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_DiagMilitary.cs) | 判据**单源**：`JudgeKind` 枚举 ＋ `JudgeState`（streak）＋ `UpdateJudges(k,bcfg,target,drive)`；状态行新增 **`verdict=`**（形如 `ok\|targetGateHit@D14\|stoneCold:12\|deadlock:5\|noIncome:9\|ANOMALY:stoneChain`）＋ **`chSrc=`**（世界资源点任务源注册数） |
| **3 异常即停（止损）** | 同左 | 支撑量异常（石链僵死／六资源零入库）**当日**打 `ANOMALY=` 并**当场判停**；容器在同一循环消费 ⇒ 不再"跑满窗口" |

**阈值单源**（禁双份魔法数）：`JudgeDeadlockDays=5`／`JudgeStoneColdDays=10`／`JudgeNoIncomeDays=15`／`JudgeChannelMinDay=3`
**`CountWorldResourceSources()`**＝只读反射 `TaskScheduler._sources`（按类型名 `Gather/Byproduct/Resource` 计数；不可达返回 -1）。本批**仅打点不判停**，供 HH.221 判据③用。

**日志两层（§8.1 落地）**：**目标日志**＝`target=`／`warrior=`／`剧本阶段迁移`；**支撑日志**＝`stone=`／`wood=`／`in=`／`prod=`／`chSrc=`／`census 过滤计数`／**`verdict=`**。

---

## 二、在线判据表（**已填 · 照此实现与运行**）

| # | 判据 | 可判定最早日 | 命中即停 | 本批 | 实现锚点（优先级） |
|---|---|---|---|---|---|
| **J1** | **死滞**：`drive≤0` 且 `target<门(4)` 连续 **≥5 日** | **D5~D10** | 判"**死滞复现**"并停 | ✅**开** | `JudgeKind.Deadlock`／`TryJudge` ① |
| **J2** | **石链僵死**：`stone` 连续 **≥10 日不增** **且** 石入库 `in(Stone)=0` | **D10** | 判"**石链僵死**"并停（**止损**） | ✅**开** | `JudgeKind.StoneCold` ② |
| **J3** | **异常**：六资源**全零入库**连续 ≥15 日 | D15 | 判 `ANOMALY` 并停 | ✅**开** | `JudgeKind.NoIncome` ③ |
| **J4** | **通道未落地**：世界资源点任务源注册数 ≤0（D≥3） | **D3** | 判"通道未落地"并停 | ⛔**本批关**（HH.221 用：本批预期"未落地"，开则**误停**） | `JudgeKind.ChannelAbsent` ④ |
| **J5** | **机制面已证**：`target≥门` 首达 | D3~D14 | 机制已证 ⇒ 可停 | ⛔**本批关**（负探针 `drive≡0` 不可能触发；正向/七考批按需开） | `JudgeKind.TargetGateHit` ⑤ |

**预期**：负探针（SO `internalDriveWeight=0`）由 **J1 于 D5 命中**（**≈5 分钟**，对比 HH.220 白跑 51 日 ≈21 分钟）。
**若未中断 ⇒ 机制 1/3 未接上，如实回报**（禁"跑完再看"）。

---

## 三、自检证据（可复现）

| 项 | 证据 |
|---|---|
| 编译 | **0 错**（`EditorUtility.scriptCompilationFailed=False`；`Library/ScriptAssemblies/Assembly-CSharp-Editor.dll` 重建 09:17:20） |
| API 反射核验 | `DiagMilitary.TryJudge=True`／`ResetJudges=True`／`CountWorldResourceSources=True`；`JudgeKind=None,Deadlock,StoneCold,TargetGateHit,NoIncome,ChannelAbsent`；阈值 `deadlock=5／stoneCold=10／noIncome=15`；容器 `JUDGES=True`／`CheckJudges=True` |
| 回归冒烟 | `Valley2_17_Smoke_5` **ALL PASS**（含 HH.217 drive 正负例：`drive0.1=4≥4／drive1e-4=4／drive0=3<门`） |
| 红线自检 | **业务码零改动**（仅 `Assets/Editor/*` 两文件＋容器常量）；**AI.Core 零触碰**；**未跑局/未进 Play**（本轮无 Play 会话 ⇒ L-32 天然满足）；**未 push**；枚举无新增（L-28 N/A） |

---

## 四、待确认（1 项需裁 ＋ 2 项报备）

1. **判据启用集**（需确认）：本批 **J1+J2+J3 开 / J4+J5 关**（理由见 §二）。
   —— 若要求 **J5 也默认开**（"`target≥门` 即停"作通用默认），将影响后续**正向批**的「剧本阶段 → 军事」腿取证（D664 验收句②）⇒ 需你定"机制面已证即停"与"下游落地确认"的取舍。
2. 报备：容器常量保持 `SEED=64513`／`SLOT="p1_fix1b"`／`CIRCUIT_BREAK_DAY=90`（**保留为兜底熔断**）／`MILITARY_STOP_COUNT=1`；实际预期 **J1@D5 自动中断**。
3. 报备：**机制 4**（≥30 日每 10 日阶段小结）按 D666 **本批不做**，随七考重验批强制。

---

## 五、下一步（确认后执行）

1. **负探针**：SO `internalDriveWeight` 临时置 0 → 槽 `p1_fix1b`（正门 `EnterTestRun`＋15x）→ **预期 J1@D5 自动中断** → 复原 0.1 ＋ `git status` 该文件无 diff；报告写明**实际中断日**。
2. **(B)(C) 正式诊断报告**：根因＋`file:line`＋「**是否结构性**」；(C) 必须明确「与 HH.189 wall181 **同因/异因**」（禁预设）；并入 HH.220 §4.6 三探针（③⑨逐行动读数／`Gather` 事件计数／k1 领土石源计数）。
3. **交付报告**（按账本实时水位线取号）⇒ 策划端验收。
（**HH.221「AI 资源权力对等批」为独立批**——本批不越界实施，仅按 §一 打完 `chSrc=` 通道读数。）

---
*执行端 2026-09-12（HH.222，取号 `af7cc69`）。停手待策划确认「§四-1 判据启用集」后即跑负探针。*
