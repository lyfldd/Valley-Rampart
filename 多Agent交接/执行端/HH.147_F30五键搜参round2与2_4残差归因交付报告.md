# HH.147 F30 round-2 五键搜参+2_4 残差归因+勘正三笔交付报告（训练师→策划端）

> 类型：进度同步+待决策
> 状态：⏳待裁决
> 日期：2026-09-10 · 发起端：执行端（训练师）· 关联：D-001 §10 / F30 README / 17 手册 H-06
> **取号**：读 `_编号登记.md`——146 已销号、144/145=王国AI 批D 微批占用→**HH.147**（先登记后落盘，账本 🟡占用）。
> **依据**：D599 下串（round-2 五键搜参[iwRetreat 解禁]+2_4 残差归因立案准+勘正三笔责令+F30 README 刷新+17 手册候选评估）。

---

## §一 T1 勘正三笔兑现（D599 责令，轻量）

1. **compare_gate1_relax.ps1 默认 PostPath**（D599 责令①）：`results/probe-f30/report.json`（probe-k 单文件覆盖位=易变载体）→ 改指 `runs/f30_gate1/post_refit_kf30_report.json`（补线后报告，稳定存档），并加注释说明弃用原因。**无参复跑实证**：
   ```
   compared scenarios: 6
   GATE1-REDUCED RESULT: PASS — KF30 池逐场景逐字段全等（×1.0 恒等实盘成立）
   ```
   （策划端 FAIL 陷阱根除：无参复跑即 PASS 6/6，不再撞 probe-f30 易变载体。）
2. **results/probe-f30/report.json 工作区覆盖处置**（D599 责令②）：工作区=未提交 c4 落盘覆盖 → **`git restore` 回上串提交版**（iwCharge05 单场景探针）；c4 证据已在 `runs/f30_search/kf30_c4.json` 在库无损。revert 后 `git status --short results/probe-f30/report.json` 干净=无残留陷阱。
3. **D-001 §0 块头旧号残留**（D599 责令③）：「HH.144 兑现」→「HH.146 兑现」（取号撞 144 让位重取 146 的旧号残留；§0 块头+状态行两处）。grep 对账：D-001 HH.144 残留=0，HH.146 命中=2。

> 勘正三笔全兑现，grep/实盘双验证（L-02）。

## §二 T2 round-2 五键搜参（D599② 解禁，训练自治）

**判据**（D599②/④ 口径）：卡指标 intentStats（mean expectedHitRate over 5 annotated KF30 scenes + avgSwitches 平滑度）+评分面无退化门 Δ≤±0.003（@100 vs F30 baseline 0.417/holdout 0.498）。起点=c3。**批量探针证据逐候选独立落盘**（`runs/f30_search_r2/kf30_*.json`，H-06 处方，不再单文件覆盖）。

**五键网格（KF30 池 @30）**：

| 候选 | iwR | iwSupp | iwC | iwS | iwD | 2_1 | 2_2 | 2_3 | 2_4 | 2_5 | mean hit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| c3（起点） | 1.0 | 1.0 | 0.5 | 1.5 | 0.5 | 0.977 | 0.758 | 0.924 | 0.450 | 1.000 | 0.8218 |
| **r2-A（胜者）** | 1.0 | 1.0 | 0.5 | 1.5 | **0** | 0.977 | **0.982** | **0.956** | 0.450 | 1.000 | **0.8729** |
| r2-B（R=0.5） | 0.5 | 1.0 | 0.5 | 1.5 | 0.5 | 0.522 | 0.758 | 0.924 | 0.450 | 1.000 | 0.7307 |
| r2-C（R=1.5） | 1.5 | 1.0 | 0.5 | 1.5 | 0.5 | 0.977 | 0.758 | 0.924 | 0.450 | 1.000 | 0.8218 |

**判读**：
- **r2-A（iwDefense 0.5→0）胜出**：mean 0.8218→**0.8729（+0.0511）**，由 2_2（0.758→0.982）+2_3（0.924→0.956）驱动；平滑度改善（2_2 sw 1.28→0.96、2_3 sw 0.80→0.62）；2_1/2_4/2_5 不变。**机制**：2_2 支援编队到达热点后 advance 清空→value 跌下 chargeValueGate(0.6)→Charge 热域项被掐→Defense(0.5) 接管=0.758 根源；iwDefense=0 使该窗口 best≤0→⑤维持 Stay Charge；2_5 value 恒 0.2 Charge 恒被门控→Defense 靠⑤携带维持 1.0（无退化）。
- **iwRetreat（D599② 解禁首探）**：0.5→2_1 塌缩 0.522（retreat=0.417<Defense=0.5，下界≈1.0）；1.5 无增益（2_1 饱和 0.977）→ **维持 1.0**。iw=0 行为学探针点 round-1 已证（2_1 hit=0=rule① 供体真掐断）。
- **iwSupport**：无梯度维持 1.0（D597② 独占域；probe6 实证 0 即摧毁 2_2/2_3）。**iwSally**：边际耗尽维持 1.5（c3≡c4）。**iwCharge**：r2-A 基座无独立梯度维持 0.5。
- **评分面无退化门：PASS**——r2-A @100 全卷 **total=0.417（vs F30 baseline Δ=0.000）/holdout 0.499（Δ=+0.001）/regression=0/verdict=candidate**；报告 `runs/f30_search_r2/gate_r2A_score/`。
- **现值口径**：round-2 最优=卡内 patch r2-A（useIntentTable=false+iwDefense=0，余 c3 同），**champion 单源不动**（终值收敛后走受控重注册+15_账本，届时另行列报）。**语义注记**：iwDefense=0 属 D599②「意图抑制」边界值（KEYS §二），收敛值语义待策划端采信。

## §三 T3 2_4 残差归因（D599④ 立案准，取证性质）

**证据链**（KF30_2_4 30 局 intent 日志逐行 + 决定性探针）：
1. **日志逐行**（@c3）：标准序列 `1:Sally 7:Charge 8:Sally 14:Charge`（sw=4，Sally↔Charge 振荡）；**3/30 局（run17/24/28）打满 90s 超时死锁**——t=15 后停 Charge 75s 零切换（hit 0.15）；wolf10 被推离→Sally 条件（enemyDist≤sallyEnemyDistGate=20）失效→高热下 Charge/Defense 非零分顶掉 ⑤。
2. **决定性实验**：probe5（iwCharge=0+iwDefense=0，support=1.0）→ **2_4 hit 仍 0.4496/sw 4.13 逐位不变**（flip 非热域 charge/defense 决定，加权路径该 regime 只可能 Sally 或 ⑤）；probe6（再 iwSupport=0）→ **2_4 hit 0.4496→0.9635、sw 4.13→1.00**（run0 仅 `1:Sally` 到 end=Sally 全程维持）。
3. **归因结论**：2_4 的 Charge flip 真凶=**加权核支持域项（scoreCharge += 1×iwSupport，hasRemoteHotspot && heat<engage）在 2_4 爬坡窗劫持 Sally 任务**——wolf 被推离/接敌空窗期本地 heat 跌下 engage + 存在远程热点 → rule② 支援语义把「本队出城迎战目标敌」误判为「远程战斗热点」；⑤idle 携带只在 iwSupport=0 激活。
4. **权重面可达性=不可达**：iwSupport 二元（>0 即劫持；=0 修 2_4 但摧毁 2_2[0.000]/2_3[0.000]）→ 5 场景卡指标无联合最优，**理论 ~0.9 不可达**。

**边界处置（D599④ 遵守）**：
- 归因指向**场景考题设计缺口**（爬坡窗+⑤idle 携带语义下 gap 不可达）→ **未擅动**；按 AGENTS.md 0.9 卡池流程候选（重生成+重建 baseline+台账）待策划端裁决。
- 同时暴露**结构语义面**（rule② 支持域 vs rule③.5 Sally 域碰撞：加权核无法区分「支援远程热点」与「本队 Sally 目标敌」）→ **列报策划端候裁，禁擅改结构**（本串结构零触碰）。
- **非收兵供体**：2_4 日志零 retreat 事件，VictoryConsolidation 未参与（排除结构域供体解释）。

## §四 T4 F30 README 刷新（D599② 口径）

- iwRetreat 搜参解除口径按 D599② 落定（round-2 五键+起点 c3+判据口径）；补线闭环态（HH.146）一并刷新（顺手清 README 残留旧号 HH.144→HH.146）。
- 状态/下一步/训练态口径/待办四节更新：round-2 完成+r2-A 现值+2_4 归因结论+收敛回灌待列报。

## §五 T5 17 手册候选评估（D599 验收三问转来）

「批量探针证据独立落盘路径」按四件套评估**齐全→入册 H-06**（17 手册 §六）：现象（probe-k --report 单文件覆盖位×批量搜参多候选证据矛盾，relax 脚本默认参 FAIL 陷阱+工作区残留）/根因（L-02 补课固定路径单文件设计未覆盖批量独立证据需求）/判据（批量跑批引用 results/probe-{card}/report.json 即随候选切换漂移）/处方（逐候选独立路径+脚本默认参改稳定存档或必填+治本=probe-k 增 --report <path>）。本串 round-2 已实践该处方（runs/f30_search_r2/ 逐候选落盘零覆盖残留）。

## §六 待决策事项（策划端裁决）

1. **2_4 场景考题设计缺口处置方向**——这决定 F30 卡指标 2_4 残差（0.45 vs 理论 ~0.9）的后续路径：
   - **A（推荐）**：按 AGENTS.md 0.9 卡池流程处理 2_4 场景（重生成+重建 baseline+台账）——若考题设计意图是「出城迎战全程 Sally」，则场景应避免 wolf 被推离 Sally 条件域触发 rule② 劫持（如敌速/推进距离/波次设计收紧）；代价=KF30 池变更影响 F30 baseline。
   - **B**：维持现状，将 2_4 的 ~0.45 作为「支持域语义边界下可达上限」接受，卡指标按可达口径重新锚定（需策划端认可 0.8729 为 round-2 收敛面）。
   - **C**：结构语义面介入（加权核区分「本队 Sally 目标」与「支援远程热点」/rule② 与 rule③.5 域分离）——归策划域结构裁决，本串不擅改。
2. **r2-A 现值采信**——iwDefense=0（意图抑制边界值）作为 round-2 收敛候选是否采信；采信后走受控重注册+15_账本（届时另行列报）。

## §七 红线自检与交付物

- **红线**：键族结构/评分公式/useIntentTable 全局切换零触碰；champion 单源未动；王国AI 批D（HH.144/145）文件零触碰（主仓只 add 本信+账本+索引）；探针零响应→grep 消费点先于行为归因（本串归因=代码级 grep+决定性探针实证，非臆测）。
- **代码**：无（纯证据/文档串；唯一脚本改动=relax 默认参勘正）。
- **证据**：`runs/f30_search_r2/`（patch_r2_A/B/C + kf30_r2_A/B/C + probe5/6 + gate_r2A_score/）；`runs/f30_gate1/compare_gate1_relax.ps1`（勘正后）。
- **文档**：D-001 §10（round-2 台账+2_4 归因）、F30 README、17 手册 §六 H-06、本信、账本（HH.147 🟡）、索引行。
- **commit**：训练仓+主仓各一笔（见信尾补记）；commit 后 git-plan-sync。

> 训练师 2026-09-10
