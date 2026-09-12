# HH.231｜② `HH.203`「P1 终验收·七考重验」交付报告（长局 120 日档·seed 73621／`p1_run9`）

> 类型：**交付报告** · 状态：🟡**待验收** · **所属 Gate＝`G2-1`（军事期可达）**
> 作业依据：**`HH.203` 任务书（D657）＋ D680 §逐项裁决② ＋ D683 起跑确认（含 6 项裁示＋4 项机械动作）**
> 执行端（TraeCode·Unity 轨）· 2026-09-12 · 取号：遵 D640 #10（水位线 HH.230 → **HH.231**，独立单行 commit `6760972`）
> 起跑串 `2dde99a`（机械动作：`ENABLE_J8_BUILDOK=false`／`SLOT=p1_run9`）

---

## 一、判定结论

# ✅ **PASS**——判定线 `D589`「净状态 **≥2 AI 自主至军事期**」**达成**（**达标收工 @D94**）

| 项 | 实测 |
|---|---|
| 收工档（原文） | `===== HH.80 三考收工 =====` / **`达标收工：≥2 AI 军事期（4,1）@D94`** / `终速=0 存盘 p1_run9=True 时间=14:56:08` |
| 达标国① | **k4（玄岩国·r2 矮人）** 首次 `stage=Military` **@D78**（`worker=8 warrior=4 militaryTarget=5 drive=0.0595 targetGateHit@D14`） |
| 达标国② | **k1（森语国·r1 精灵）** 首次 `stage=Military` **@D94**（`worker=11 warrior=4 militaryTarget=5 drive=0.0422 targetGateHit@D24`） |
| 收工方式 | **达标即停**（`_military.Count ≥ MILITARY_STOP_COUNT=2`；**未**触熔断/未触灭绝停） |
| 时长 | 起跑 `14:18:49` → 收工 `14:56:08` ＝ **≈37.3 现实分钟**（94 游戏日 ≈ **23.8 s/日**@15x） |

---

## 二、起跑档与倍率分列（`L-09`／`L-32`）

| 项 | 实读 |
|---|---|
| 正门 | `TestHarnessApi.EnterTestRun`（判负封死＋T11 野怪静默＋考跑档直通）✅ |
| 档位 | `SEED=73621`（`:29`）／`SLOT="p1_run9"`（`:30`）／`USE_DIAG_WINDOW=false`（`:38`）⇒ `ActiveCircuitDay=120`／`MILITARY_STOP_COUNT=2`（`:32`） |
| 判据启用集 | `ENABLE_J7_WALLTOP=false`（`:54`·D680）／`ENABLE_J8_BUILDOK=false`（`:90`·D683 裁 B）／`ENABLE_J2_STONECOLD=false`（`:84`）⇒ 实际参与＝**M 达标／熔断／J1／J3／J6(标记)／灭绝停跑** |
| 就绪检查 | 两道 fail-fast 全过（`P1Observer.IsRunning`＋`DiagMilitary.IsRunning`，`:114-125`） |
| **测试段倍率** | **`Time.timeScale = 15`**（跑中实测，`execute_code` @D90：`timeScale=15 currentDay=90 isPlaying=True`） |
| **观察者所见段倍率** | **`Time.timeScale = 1`／`isPlaying = False`**（收工后实测；`Finish()` 真暂停＋`ExitTestRun()`＋退 Play）✅ |
| 存档 | `SaveManager.Save("p1_run9")` = **True**（`p1_run8`／HH.214 存档槽**未动**·D683 裁 D 兑现） |

---

## 三、`L-34` 五列判据表 · **实测行**

| 判据 | 可判定最早日（预案） | **实测（本跑）** | 命中/停止 | 服务哪条验收句 | 作用域 | 口径来源与排除项 |
|---|---|---|---|---|---|---|
| **M 达标** | D60 | **k4@D78／k1@D94** | ✅**命中 @D94**（累计 2 国）⇒ **PASS 收工即停** | `D589`「≥2 AI 自主至军事期」（正向） | **全批（跨 ≥2 国）** | 源＝镜像 `[DiagMilitary] … stage=Military` 行＋容器 `_military`；**排除项**＝非 AI 国不计、未用夹具注入（"自主"语义） |
| **熔断** | D120 | **未达**（D94 先达标） | 未触发 | 同上（负向） | 全批 | `ActiveCircuitDay=120`（`USE_DIAG_WINDOW=false` 实测）；排除项＝被其他判据提前停者（本次无） |
| **J1 死滞** | D5~D10 | **无 `deadlock:` 标记**（活国 `drive=0.0422~0.0595 > 0`） | **未命中**（零误停） | 支撑（非本验收句） | 全批 any-国（实际仅活国） | 判定 `Valley_DiagMilitary.cs:61`（阈值 `:43`＝5）／读数 `:257`；**排除项＝`workerCount+warriorCount<=0` 国**（容器 `:247` guard） |
| **J3 零入库** | D15 | 活国无 `noIncome:`；灭绝国 `noIncome:63` **被排除** | **未命中**（零误停） | 支撑 | 全批 any-国 | 判定 `:67`（阈值 `:45`＝15）／读数 `:252`（六资源当日入库 `allZero`）；**排除项＝同上灭绝 guard** |
| J2 石链僵死 | — | **未启用**（`ENABLE_J2_STONECOLD=false`） | — | （服务句 HH.220 已销号） | — | 口径缺陷未解（开局无产能期） |
| J6 退避机制面 | 全程 | **机制面＝True**（`avoid=` 非空 **204 行**） | 仅标记（不停止） | ①-b 机制面（非本批） | 全批 | 源 `avoid=`（含 `(Env)/(Self)`）；排除项＝未达门槛条目 |
| J7 霸占解除 | — | **未启用**（`ENABLE_J7_WALLTOP=false`·D680/D683） | — | ①-b 端到端（非本批） | — | 无同段参照 |
| J8 防退化 | — | **未启用**（`ENABLE_J8_BUILDOK=false`·D683 裁 B） | — | ①-b 防退化（非本批） | — | 基线源 seed 64513 ⇒ 跨种子 |
| 灭绝停跑 | 全程 | **未触发**（2/4 存活） | — | 支撑 | 全批 | `workerCount+warriorCount==0` |

**同级/同作用域核对**：M 达标＝端到端级判据配端到端验收句（`D589`）✅；熔断＝同句负向 ✅；J1/J3 标为**支撑**且**命中亦停**（止损语义，`L-34`）✅；J2/J7/J8 均**未启用**（机制级/前批判据不服务本批验收句·`§8.6`）✅。

**D683 裁 C 行为验证**：k4 于 D78 达标后**未停**、继续跑至 k1@D94 累计 2 国才停 ⇒ **与裁示一致**（若按原草案"1 国即停"会在 D78 收工，无法判「≥2」；`D669` 防切证据条款正向生效）。

---

## 四、两层日志摘录

### 4.1 目标层

```
[D78][Warning] [DiagMilitary] D78 k4 stage=Military worker=8 warrior=4 gold=37 food=1113 stone=4993 wood=16 militaryTarget=5 drive=0.0595 verdict=ok|targetGateHit@D14 chSrc=0/4 avoid=
[D89][Warning] [DiagMilitary] D89 k1 stage=Expand   worker=10 warrior=3 gold=26 food=55  stone=7 wood=4  militaryTarget=4 drive=0.0530 verdict=ok|targetGateHit@D24|stoneCold:10|ANOMALY:stoneChain
[D94][Warning] [DiagMilitary] D94 k1 stage=Military worker=11 warrior=4 gold=43 food=0   stone=7 wood=4  militaryTarget=5 drive=0.0422 verdict=ok|targetGateHit@D24|stoneCold:15|ANOMALY:stoneChain
（收工档）达标收工：≥2 AI 军事期（4,1）@D94
```

### 4.2 支撑层（末段）

```
[D94][Warning] [DiagMilitary] D94 k2 stage=Expand  worker=0 warrior=0 gold=46 food=0 stone=5  wood=40 militaryTarget=2 drive=0.0230 verdict=ok|targetGateHit@D6|stoneCold:92|noIncome:63|ANOMALY:stoneChain|BACKOFF_CAP chSrc=0/4 avoid=RecruitWorker:0.25(Env)
[D94][Warning] [DiagMilitary] D94 k3 stage=Develop worker=0 warrior=0 gold=46 food=0 stone=25 wood=40 militaryTarget=2 drive=0.0230 verdict=ok|stoneCold:92|noIncome:61|ANOMALY:stoneChain|BACKOFF_CAP chSrc=0/4 avoid=RecruitWorker:0.25(Env)
[D94][Warning] [DiagMilitary] D94 k4 census defTotal=26 stageFiltered=0 noNeed=13 infeasible=8 axisFiltered=1 ev=0 top=Diplomacy
（k1 退避读数样例）D90 k1 … avoid=BuildBlacksmith:0.75(Self)   ／ k2/k3 … avoid=RecruitWorker:0.25(Env)
```

---

## 五、机制 4（`D666 §8.4`）阶段小结全量（本跑新增落地·实测）

```
[14:22:30] seed=73621 槽=p1_run9 D10  AI存活=4/4 已达标=[]    J6退避机制面=True wallTop(k1/k2/k4)=0/5/0   建造落地=6
[14:26:30] seed=73621 槽=p1_run9 D20  AI存活=4/4 已达标=[]    J6退避机制面=True wallTop(k1/k2/k4)=0/15/6  建造落地=8
[14:30:31] seed=73621 槽=p1_run9 D30  AI存活=4/4 已达标=[]    J6退避机制面=True wallTop(k1/k2/k4)=7/25/15 建造落地=10
[14:34:31] seed=73621 槽=p1_run9 D40  AI存活=2/4 已达标=[]    J6退避机制面=True wallTop(k1/k2/k4)=14/33/25 建造落地=21
[14:38:31] seed=73621 槽=p1_run9 D50  AI存活=2/4 已达标=[]    J6退避机制面=True wallTop(k1/k2/k4)=22/33/27 建造落地=29
[14:42:31] seed=73621 槽=p1_run9 D60  AI存活=2/4 已达标=[]    J6退避机制面=True wallTop(k1/k2/k4)=30/33/27 建造落地=41
[14:46:31] seed=73621 槽=p1_run9 D70  AI存活=2/4 已达标=[]    J6退避机制面=True wallTop(k1/k2/k4)=33/33/27 建造落地=51
[14:50:31] seed=73621 槽=p1_run9 D80  AI存活=2/4 已达标=[4]   J6退避机制面=True wallTop(k1/k2/k4)=33/33/27 建造落地=55
[14:54:31] seed=73621 槽=p1_run9 D90  AI存活=2/4 已达标=[4]   J6退避机制面=True wallTop(k1/k2/k4)=33/33/27 建造落地=56
```

⇒ **"边跑边判、禁跑完再看"已机器化落地**（每 10 游戏日一行写档，含达标/存活/霸占/落地）；本次全程**无 J1/J3 误停**。

---

## 六、自曝缺陷与列报

1. **🔴 可判定最早日预设偏低（`L-34` 口径来源的自我修正）**：预案定 M 可判定最早日＝**D60**（据 `HH.220` 实证 `64513` k3@D65），**实测首达 D78**（seed 73621 更晚）⇒ 多跑 18 游戏日 ≈**7 现实分钟**（**不影响判定**）。**教训口径**：`L-34` 的"可判定最早日"若取自**他 seed** 实证，须标注"本 seed 无史"并留更大余量（与 `D678`/`D683` 的"基线可比性前提"同族：**同 seed·同段·同档**）。
2. **灭绝国读数噪音**：k2/k3 于 D31~D40 间归零（`worker=0 warrior=0`）后，`DiagMilitary` **仍逐行输出**（`ANOMALY:stoneChain`／`BACKOFF_CAP`／`noIncome:63` 长挂）⇒ 属读数噪音（不影响判定）。**口径声明**：J1/J3 的"排除已灭绝国"由容器 `:247` guard（`workerCount+warriorCount<=0 ⇒ continue`）实现——**若不声明，极易被误读为"J3 失效（63≫15 却不停）"**。
3. **达标不蕴含经济健康**：达标国 **k1** 自身带 `ANOMALY:stoneChain`＋`stoneCold:15`，`stone=7/wood=4`（低水位）⇒ 军事期门槛达成与资源链健康**解耦** ⇒ **交 `G1-1`（资源对等）／`G1-3` 观察**。
4. **k4 末期 `census top=Diplomacy`**：外交行动登顶（外交系统 `G4-*` 未实装）⇒ 观察项（行动池含外交位、系统为空）。
5. **AI 存活 2/4**：k2/k3 早期灭绝（D31~D40 间）；本次**未触灭绝停跑**（需全灭）。灭绝动因未诊断（超出本批范围）⇒ 列报，建议归 `G2-3`（AI 灭亡判据）／`G1-4`。
6. **正例下判据零误停**：本次 J1（D5~D10）／J3（D15）**均未命中** —— 对 `D669`/`D670`「作用域＋同级」设计的**正向证据**（对比 `HH.224` 的 J2 误停、`D678` 的 J7 假阳性）。
7. **未重跑短窗**：按 D683 无需（本批＝长局判定批）。

---

## 七、红线自检

**只做 ②**（未越批）｜设计文档／队列**零改动**（只读）｜`AI.Core` **零直改**｜**枚举零新增**（不涉 `L-28`）｜**禁参数微调找补**（D563③：未调任何数值参数）｜**正门唯一入口**`TestHarnessApi.EnterTestRun`＋野怪守卫＋**15x**（`L-09`）｜**已退 Play 实测** `isPlaying=False`／`timeScale=1`（`L-32`）｜写-改-commit 同串·**只提本串·不 push**｜改前重读磁盘｜共享文档增量改｜**容器级改动全部列报**（`SEED`/`SLOT`/`J7`/`J8`/`JUDGE_FOCUS_KINGDOM`/机制 4/Scout `SEEDS`）｜**未经 sim 门禁**（全批容器级＋观测域，未触决策核；`AI.Core` 与训练仓零命中）。

---

## 八、证据清单

| 证据 | 位置 |
|---|---|
| 主镜像日志（36,362 行） | `Valley Rampart/Logs/P1/p1_log_20260912_141849.log` |
| 收工档 | `Valley Rampart/Logs/P1/hh80_run_status.log` |
| **阶段小结档（机制 4）** | `Valley Rampart/Logs/P1/hh80_run_stage.log` |
| 日快照 | `Valley Rampart/Logs/P1/p1_snap.csv`（session `20260912_141849`） |
| 存档 | `SaveManager.Save("p1_run9")`＝True（`p1_run8` 未动） |
| 侦察（同 seed） | `Valley Rampart/Logs/P1/hh80_scout_result.log`（seed 73621） |

---

*执行端 2026-09-12（HH.231 交付报告·判定＝**PASS**）。停手待策划端验收。*
