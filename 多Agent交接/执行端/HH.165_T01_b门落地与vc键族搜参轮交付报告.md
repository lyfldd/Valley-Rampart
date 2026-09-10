# HH.165 T01 b 门落地 + vc 键族搜参轮交付报告（**含 STOP 请裁**）

- **日期**：2026-09-10
- **交付端**：执行端·训练轨（TraeCode 训练师会话）
- **依据**：D615 裁决（①采纳 b「无可攻击静态目标」门/a/c/d 否决 ②vc 搜参轮开工准，前置=b 门落地 ③扩容重建 baseline 报备时点维持 D560④/D607b）+ 用户本串任务书（STOP 条件 3）
- **编号**：HH.165（编号登记先查后取：HH.164 为止已占，165 空闲）
- **训练仓 commit**：`db4c0eb5`（40 文件，1983+/11−）

## §一 交付范围与结论一句话

**b 门落地成立（双门禁全过，零行为退化）**；**vc 键族搜参轮已跑完并测绘响应面，但停于「评分面无退化门」**——按用户 STOP 条件 3 停并请裁（§八）。

## §二 b 门落地（D615 §一「实现硬条款」逐条兑现）

1. **单一事实源**：`SimWorld.HasAttackableStaticEnemy(Faction)` = 「存在仍可攻击的静态敌对目标（`IsAlive && IsStatic && Faction∉{本方, None}`）」。
   - **胜利口径与既有 `CheckEnd/CountAlive`（一方全灭，含静态工事）同源**——非新口径，是既有胜负判定的下沉复用。
   - **两通道共用同一判定**（D615「禁只修散兵」）：个体级 `SimBrain.ApplySoloConsolidation` + 编队级 `SimFormation.UpdateConsolidation`。
2. **门语义**：门成立 → 本 tick 不触发；已收兵态**同门强制解除**（同一判定：门不成立即收兵语义不成立）；门成立期间计时器清零（静态清除后须重新满足 `consolidationConfirmTicks`，防边界抖动）。
3. **零污染**：未改比值口径（a 已否决）、未改收兵目标（c 已否决）、未引入血量量级依赖（a 否决理由）、未改评分公式。
4. **build 门禁**：`dotnet build harness` **0 警告 0 错误** ✅

## §三 b 门正负探针（T01 卡池 @30/场景，取证性质；探针场景落 `cards/T01/probe_scenarios/`）

| 场景 | 构成 | 用途 |
|---|---|---|
| `KT01_2_90` | `W9_T01_20260907_2` 逐字副本（人类 5 散兵 vs Monster Gate+3 远程） | 混编局·敌方静态在场 |
| `KT01_2_91` | 同 2_90 但移除 Monster Gate | 混编局·静态清除 |
| `KT01_2_93` | `KT01_2_2` 逐字副本 + 追加 1 个远置 Monster Wall | 门因子隔离（同场景 ± 敌方静态） |

| 判据 | 读数 | 结论 |
|---|---|---|
| ① 混编局 Gate 在场 → 不触发 | `KT01_2_90` 键开 `soloTrig=0`（**旧码同场景 = 37**）；win 恢复 0.40（旧码键开 0.367 微退化被门消除） | ✅ 正探针 |
| ② 静态清除后 → 触发 | **门因子隔离**：`KT01_2_2`（无敌方静态）15 ↔ `KT01_2_93`（同场景仅追加远置敌方 Wall）0——同 seed/同 patch，唯一变量=门 | ✅ 正探针 |
| ③ 纯动态局 → 行为回归不变 | 原生池 `KT01_2_0/2_1/2_2` 键开读数（win/annih/kd/duration/damage）与旧码 arm2 **逐位全等**；同码键关重跑 vs arm5 **差异项=0**（确定性） | ✅ 负探针 |

- 诚实附注：`KT01_2_91`（移除 Gate 的孪生体）`soloTrig` 亦为 0，**非门残留**——该场景动力学自身不产生「低比值持续 ≥2s 且敌未灭」窗口（门在该局恒 false，路径畅通）。
- 报告载体（逐候选独立落盘=H-06）：`results/probe-t01/report_arm5_gate_keyoff.json`、`report_arm6_gate_keyon.json`、`report_vc_c{1..6}.json`。

## §四 双门禁（AGENTS 铁律 6；v9 全卷 @100，`results/b_gate_check/`）

| 门 | 读数 | 判定 |
|---|---|---|
| build | 0 警告 0 错误 | ✅ |
| 无场景退化（>5%） | `verdict.NoRegression=True`（退化清单空） | ✅ |
| holdout 不退 | 0.4982 → 0.4982（Δ=0.000，`regressed=False`） | ✅ |

**逐场景实锤**：132 重叠场景中仅 **24 场景** `consolidationTriggered` 下降（全部编队级通道；如 `W9_T02_0` 63→0、`KT05_2_1` 100→0、`W9_T09_2` 93→17），**其余全部行为/评分指标（win/annih/kd/duration/damage/slot/retreat/protected/task/…）与 `subScore` 逐位不变** → **b 门对现网零行为代价**。

**机制侧观测（列报知悉）**：24 场景编队级 VC 触发被门拦下后零指标变化 → 编队级收拢执行在 v9 全卷无行为后果（与 D572「仪式性收拢」判定一致）；b 门拦下的正是「动态敌已灭、仅静态工事残存」时按**人数比**（`AliveEnemyThisTick` 含静态）误读出的收兵触发——语义误判被精准清除。

## §五 ⚠️ 口径勘正（**陈旧锚，请策划端知悉**）

`results/baseline/v9/report.json` = **132 场景**建档（meta.suite=suite_v1，2026-09-09），**当前套件 = 144 场景**——差额 12 个 = `KF30_2_1~6`（F30 新池）+ `KT06_2_S0~5`（T06 支援族），晚于 baseline 建档加入。

⇒ 内置 verdict 的 `total Δ=+0.006` 系**套件构成差**，**非 b 门效应**（b 门在 132 重叠面上零 delta，已逐位实证）。**该陈旧锚即 D615 §三「扩容重建 baseline 一次到位」要解决的问题**；F28/T03/F27 重估（D607 b）须待新锚。本串所有候选对照均以 **b 门后的 champion 同套件面**（`results/b_gate_check`）为锚，以隔离候选效应。

## §六 vc 键族搜参轮（v9 全卷 @100 逐候选独立落盘；T01 卡池 @30）

### 6.1 卡池行为读数（probe-k @30；`soloTrig` 合计）

| 候选 | patch 变更 | soloTrig | KT01_2_2 soloTrig | KT01_2_2 kd |
|---|---|---|---|---|
| c0 | `vcSoloEnabled=true` | 15 | 15 | 3.47 |
| c1 | +`disengageRatio` 0.15 | 6 | 6 | 3.67 |
| c2 | +`disengageRatio` 0.40 | 19 | 19 | 3.33 |
| c3 | +`consolidationConfirmTicks` 1.0 | 23 | 23 | 3.42 |
| c4 | +`consolidationConfirmTicks` 3.0 | 10 | 10 | 3.63 |
| c5 | +`reEngageRadius` 4.0 | 15 | 15 | 3.47 |
| c6 | +`reEngageRadius` 10.0 | 15 | 15 | 3.47 |

响应面方向自洽（比值↑/确认↓ → 触发↑）；**`reEngageRadius` 在 T01 池无响应**（c5/c6 与 c0 逐位一致）。

### 6.2 评分面（`results/vc_gate/<候选>`；变化场景数=与 b 门锚逐场景对比）

| 候选 | 编队级触发变化 | 个体级触发变化 | subScore 变化 | 内置裁决 | 退化场景（>5%） |
|---|---|---|---|---|---|
| c0 | 13 | 74 | 52 | **rejected** | 5 |
| c1 | 18 | 70 | 43 | rejected | 3 |
| c2 | 32 | 81 | 53 | rejected | 7 |
| **s1**（仅比值 0.15，开关关） | 15 | **0** | **0** | **candidate** | **0** |

### 6.3 五条结论

1. **开关单独开启即在现评分面退化**：c0（仅 `vcSoloEnabled=true`）即致 5 场景 >5% 退化（`W9_T08_1` kd 6.07→2.23、`W9_T-K_1`/`W9_T-R_1` −40%、`KT-R_2_2` −10%、`KT10_2_1` −5.3%）；逐场景归因=**个体级触发 0→313~605**、编队级触发不变 ⇒ 退化由个体通道驱动。根因=**收兵=放弃击杀**，而现评分面无「保全式赢收益项」（**D572 立项欠账**）→ 收兵只减分不加分。
2. **触发面与退化幅度单调**：比值 0.15/0.25/0.40（c1/c0/c2）个体触发总量 11987/14485/17457，退化场景 3/5/7 ⇒ 响应面自洽，非噪声。
3. **三标量单独调整零退化**（s1）：15 场景编队级触发面变化但 **subScore 全卷零变化** + holdout Δ=0.000 ⇒ 标量"活但无害"。
4. **三标量非 T01 卡私有**：三键为编队级/个体级**共用键**（D567③ 原设计，registry consumers 双列）——单动标量即改他卡 VC 闸门（s1 实证 15 场景跨卡触发面变化）⇒「T01 卡内搜参」在键族层面**不成立**（跨卡耦合）。
5. **收敛结论**：vc 键族在现评分面下**不可收敛出可留用（无退化）候选** ⇒ **维持 champion 现值**（`vcSoloEnabled=false` + 三标量 0.25/2.0/6.0）；搜参轮**挂起**至评分面设计稿落地；b 门落地保留（§四 零退化）。

## §七 红线自检

未改评分公式；未改 VC 结构语义（b 门=触发处显式门，D615 §一 原文授权）；未触碰 champion/Holdout/`harness/Scenarios` 共享套件/AGENTS.md/`schemas/factor_registry.example.json`/Unity 仓；**未 push**；未越出 T01 卡片文件面（探针场景落 `cards/T01/probe_scenarios/`；跑批用卡池临时副本跑后即撤，卡池与 v9 共享套件零污染）；批外零混入（17_训练作战手册=并行会话在途 M 件不纳入；`results/f30_det_check_vc`/`runs` 不入库）；只提本批文件。

## §八 待裁事项（**STOP 条件 3 触发：双门禁不过且无法在 T01 文件面内自修**）

| # | 事项 | 请示 |
|---|---|---|
| 1 | **vc 键族收敛方向** | 开关类候选评分面无退化门不过（c0/c1/c2 rejected），**无法在 T01 文件面内自修**（可行修法均在面外）——请裁：**A) 维持 champion 现值+搜参挂起至评分面设计稿**（我方建议）/ B) 先出评分面「保全式赢收益路径」设计稿再复评搜参 / C) 其他 |
| 2 | **评分面设计稿时点**（D572 策划端欠账） | 该稿是「搜参收敛 / 受控重注册 / 扩容重建 baseline 报备」**三者共同前置**——请裁时点或挂账状态 |
| 3 | **键族拆分是否立项** | 三标量编队级/个体级共用 ⇒ T01 搜参天然扰动跨卡（§6.3.4）。拆分（个体级独立三标量键）可解耦，属**结构变更**、超 D615 b 门已裁范围——请裁是否立项（含 sim-sync/Unity 回灌义务评估） |
| 4 | **陈旧锚处理** | `results/baseline/v9`（132 场景）vs 现套件（144 场景）——本串已作口径勘正（§五），扩容重建时点按 D615 §三 待 vc 收敛；请确认「重估锚声明」随新锚一次到位 |
| 5 | **禁改域勘正申请（铁律 0）** | `schemas/factor_registry.example.json` 中 `tuning.vcSoloEnabled` 的 semantics 文本仍写「（HH.161）」；本串实际取号 **HH.162**。该文件属禁改域，执行端不擅动，列报由决策员处理 |

**本串未执行**（前置未达成）：受控重注册、扩容重建 baseline 报备（D615 §三 前置=vc 收敛）。

## §九 产物清单

- **代码**：`harness/Sim/SimWorld.cs`（`HasAttackableStaticEnemy`）/`harness/Sim/SimBrain.cs`（个体级接线）/`harness/Sim/SimFormation.cs`（编队级接线）
- **探针场景**：`cards/T01/probe_scenarios/KT01_2_{90,91,93}.json`
- **patch**：`patches/T01_vc_c{1..6}_*.json`、`patches/T01_vc_s1_disengage015_scalaronly.json`
- **报告**：`results/b_gate_check/{report,verdict,holdout_report}.json`、`results/vc_gate/{c0,c1,c2,s1}/{report,verdict,holdout_report}.json`、`results/probe-t01/report{,_arm5_gate_keyoff,_arm6_gate_keyon,_vc_c1..c6}.json`
- **文档**：`cards/T01/decisions/D-011_b门落地与vc键族搜参轮.md`、`cards/T01/README.md`、`15_训练侧harness与Unity端差距文档.md` 一·补二十四

> 执行端 2026-09-10 交付 · **b 门验收 + 搜参裁决请求**（STOP 条件 3）· 待策划端裁决
