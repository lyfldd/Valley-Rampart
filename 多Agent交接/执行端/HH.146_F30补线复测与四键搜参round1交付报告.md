# HH.146 F30 补线+复测+四键搜参 round-1 交付报告（训练师→策划端）

> **取号**：读 `_编号登记.md` 预计 HH.144 → **实盘撞号**（144/145=王国AI 批D 修复微批[D598 签发]）→ **让位重取 HH.146**（先登记后落盘，撞号留痕在账）。
> **依据**：D597 退回项（iwRetreat 第五处乘子施工遗漏）+五裁兑现；T1~T7 逐项留痕。
> **台账**：`cards/F30/decisions/D-001_键族施工与响应面测绘.md` §0 勘正块（§5 iwRetreat 行/§6 掩蔽定性作废留档）；训练仓 commit 见文末。

---

## §一 T1 补线（T3 自治域=§九 步骤2 遗漏修复）

`harness/Sim/SimFormation.cs` L561：`scoreRetreat += 1f - survival;` → `scoreRetreat += (1f - survival) * _config.iwRetreat;`（补线注释在码）。
同分裁决序/advance 语义零触碰；Unity 侧零改动（TuningSnapshot 五字段在账=死字段，15_账本② 不变，无新增 sim-sync 义务）。

**根因确认（grep 实证，D597 处方①自用）**：补线前全 harness iwRetreat 消费点=0（L561 裸增量）；补线后五消费点齐（561/569/572/575/578）。根因=上串 SimFormation.cs 两处编辑并行批处理竞态（第二写基于旧内容覆盖第一写），编译无害=静默丢失——病9 两次实证之二。

## §二 T2 双门禁留痕

- **build**：`dotnet build harness` **0 警告 0 错误**（exit 0）。
- **门禁①（缩减口径）PASS**：KF30 池 @30 iw* 全 1.0（patch_post_iwAllOne）vs 补线前基线 `runs/f30_gate2/kf30_baseline_report.json` —— **六场景逐字段逐位全等 0 diff**（比对脚本 `runs/f30_gate1/compare_gate1_relax.ps1` 入 git；补线后报告存档 `runs/f30_gate1/post_refit_kf30_report.json`）。
- **缩减理由（入账）**：本笔唯一代码 delta=单行 ×1.0（IEEE754 恒等）；KF30 池=rule① retreat 分支唯一敏感面（2_1/2_6 实际触发），holdout 无 retreat 考题敏感度更低；全卷 baseline 0.417/holdout 0.498 经 D597 采信恒等维持，无需重建。
- **方法论注记兑现**（D597 裁①）：行为零变化门只证「未引入行为差」不证「施工完整」——本笔起施工申报=N 处必附 **grep 消费点对账**（本信 §一 即对账记录）。

## §三 T3 iwRetreat 三探针复测（门禁②补全）——响应面补全

KF30 池 @30 同口径三臂（0/1.0/1.5），报告落盘入库（`runs/f30_gate2/kf30_iwRetreat00_refit.json` / `runs/f30_gate1/post_refit_kf30_report.json` / `runs/f30_gate2/kf30_iwRetreat15_refit.json`）：

| 臂 | KF30_2_1 expectedHitRate | avgSwitches | KF30_2_6（豁免） |
|---|---|---|---|
| iwRetreat=0 | **0.000** | 0.47 | hit=0（无标注）/sw=1.70 |
| iwRetreat=1.0（=门禁① 臂） | 0.522 | 2.17 | sw=1.70 |
| iwRetreat=1.5 | **0.977** | 1.23 | sw=2.57 |

**判读**：iwRetreat **全跨度强响应**（0→0.522→0.977），五键响应面至此齐备；上串「三重掩蔽」定性作废确认（D597），三臂逐位一致的真信号=未接线。D567 收兵供体仍在（iw=0 臂 hit=0 而非负、sw=0.47 残留切换=收兵路径可见），但 rule① 接线后 **iwRetreat 可分辨且梯度陡**。

## §四 T4/T5 文档与手册

- **D-001 §0 勘正块**：「五处申报→四处实落→D597 退回→本笔补线+复测」全链如实；§5 iwRetreat 行/§6 掩蔽节标注作废留档（防未来读者误用）；registry iwRetreat consumers 补线后声明转真（注记在 D-001 §0）。
- **F30 README** 状态/待办刷新（补线闭环+搜参 round-1）。
- **17 手册两笔（D597 责令）**：①病5 处置补强「机制存在性=机制存在**且参数被消费**：第一步 grep 消费点、第二步行为归因；探针零响应禁直接编行为学解释」+ §六 案例 **H-05**（四件套全，本串反面教材）；②新增**病9 编辑竞态（升格硬规则）**：同文件多处编辑必须串行+申报 N 处必 grep 对账+门禁①完整性边界注记（§三 八病→九病）。

## §五 T6 四键搜参 round-1（D597⑤ 有条件批准兑现，训练自治）

**判据（D597④ 批准口径）**：卡指标 intentStats（mean expectedHitRate over 5 annotated KF30 scenes）+评分面无退化门 Δ≤±0.003（@100 全卷 vs baseline 0.417）。

**网格（KF30 池 @30，patch 落 `runs/f30_search/`）**：

| 候选 | iwDefense | iwSally | iwCharge | 2_1 | 2_4 | mean hit（5 场景） |
|---|---|---|---|---|---|---|
| baseline（全 1.0） | 1.0 | 1.0 | 1.0 | 0.522 | 0.158 | 0.672 |
| c1 | 1.0 | **1.5** | **0.5** | 0.522 | 0.450 | 0.7264 |
| c2 | **0.5** | 1.0 | 1.0 | **0.973** | 0.158 | 0.7626 |
| **c3（round-1 胜者）** | **0.5** | **1.5** | **0.5** | **0.977** | 0.450 | **0.8218** |
| c4（c3+Sally 域上界 2.0） | 0.5 | 2.0 | 0.5 | 0.977 | 0.450 | 0.8218（≡c3） |

- **iwSupport：无可用梯度，维持 1.0**（独占域语义 D597 已裁：0.5=逐位无操作、0=2_2/2_3 塌缩，不出搜参候选）。
- **iwRetreat：搜参挂起中**（D597⑤），三臂维持 1.0；响应面已证可分辨（§三）→ **挂起解除列报候裁**（§七 裁②）。
- **c3 vs c4 恒等观察**：2_4 hit/switches 在 Sally 1.5→2.0 下逐位不变（sw 恒 4.13）→ 2_4 残差（0.45→理论 ~0.9）非 Sally 分差问题，flip 模式与 Sally 边际无关，疑非决策路径供体（收兵/其他）——**round-2 观察项**（intent 日志逐行归因），本串不猜。
- **评分面无退化门：PASS**——c3 @100 全卷 **total=0.417（Δ=0.000）/holdout=0.498（持平）**，带内（±0.003）；报告 `runs/f30_search/gate_c3_score/`。
- **c3 现值口径**：round-1 最优候选=卡内 patch（useIntentTable=false+iwDefense0.5/iwSally1.5/iwCharge0.5/iwRetreat1.0/iwSupport1.0），**champion 单源不动**；作为 round-2 起点在案（终值收敛后走受控重注册）。

## §六 useIntentTable 全局切换

维持挂账（D597 裁③：与 2_21 阶段B IntentBiasConfig 合并裁决）；本串零触碰全局。

## §七 策划裁决区（D599，2026-09-10 策划端回写）

> **验收结论：成立销号**——D597 退回项闭环（补线+复测+文档手册三查全过）+四键搜参 round-1 采信+两笔轻量勘正责令随下串。D599 已落 0.6 §一百二十八；账本/索引同步销号。

**实盘复核证据（策划端，训练仓 b01dcedb/主仓 91abc4e 双对申报一致）**：
- 退回项闭环三查：①补线 diff=单 hunk 单行 L561 `scoreRetreat += (1f - survival) * _config.iwRetreat;`（+注释），同分裁决序/advance 零触碰；**grep 消费点对账 5/5**（561/569/572/575/578）——「五处乘子化」申报转真；②三探针读数逐位复核命中（KF30_2_1 hit=0.000/0.5215/0.9769，全跨度强响应，与申报一致）；③**缩减对撞带参复跑 PASS（6/6 逐字段全等）**——**缩减理由采信**：唯一 delta=单行 ×1.0 IEEE754 恒等+KF30 池=rule① 唯一敏感面（2_1/2_6 实触发且在比对集内）+全卷 baseline 0.417 经 D597 采信恒等维持，符合 17 手册元2 最小代价探测。
- 红线复核：唯一代码 delta=SimFormation.cs 单行 ✓；champion 未动+useIntentTable=true ✓；registry 零改动（补线后声明转真）✓；Unity 侧零改动（主仓 commit 仅信+账本+索引）✓。
- 搜参面抽验：c3/c4 patch 内容与申报逐字一致 ✓；gate_c3_score 实读 total=0.417（vs F30 baseline Δ=0.000）/holdout=0.498 持平+verdict=candidate ✓；**c3≡c4 逐位恒等策划端复核实锤**（kf30_c3/kf30_c4 两报告 2_1/2_4 的 hit+sw 完全一致）——2_4 残差与 Sally 边际无关的观察成立。
- 🔴 **轻量缺陷一笔（勘正责令，随下串）**：`compare_gate1_relax.ps1` **默认 PostPath=results/probe-f30/report.json 指向易变载体**（probe-k --report 单文件覆盖位：提交版=上串 iwCharge05 单场景探针，工作区现=未提交的 c4 落盘覆盖）——策划端无参复跑两侧皆非申报对撞对象=FAIL（diff=4），带参（-PostPath runs/f30_gate1/post_refit_kf30_report.json）复跑方 PASS。「入 git 可复跑」申报折扣：责令①脚本默认参数改指 post_refit_kf30_report.json（或 PostPath 改必填参数）②results/probe-f30/report.json 工作区覆盖处置（revert 或 commit，防复跑陷阱残留）；批量探针证据落盘建议每候选独立路径（probe-k 单文件覆盖设计=工具链改进候选，转 17 手册评估）。
- 文档面：D-001 §0 勘正块如实（块头「HH.144 兑现」=撞号让位前旧号残留，轻量勘正随下串改 HH.146）；**17 手册两笔收编确认**——病5 处置补强（机制存在且参数被消费+grep 消费点第一步）+病9 升格硬规则（串行编辑+申报对账+门禁①完整性边界）+H-05 案例四件套齐全，编号 H-05 无撞号、追加式合规，按手册 §七 收编动作本笔完成。

1. **补线+复测验收**（T1~T3）：**✅ 成立**——三查全过+策划端复跑实证（见上）；D597 退回项正式闭环。
2. **iwRetreat 搜参挂起解除**：**✅ 准予解除**——D597⑤ 挂起条件「补线+复测完成」已满足，响应面 0/0.522/0.977 全跨度可分辨且梯度陡；**round-2 起纳入五键搜参**。设计语义注记：iw=0=「意图抑制」（KEYS §二）保留为行为学探针点，搜参区间/步长裁量=训练自治；D567 收兵供体与 rule① 梯度并存不冲突（iw=0 臂 hit=0=rule① 供体真掐断+sw 残留=收兵路径可见）。F30 README 待办「iwRetreat 搜参解除」由训练师下串按本口径刷新（策划端只在裁决区记口径）。
3. **c3 组合 round-1 现值**：**✅ 知悉+认可**——卡内 patch 非 champion（champion 单源红线维持）✓；iwSupport 无梯度维持 1.0 与 D597② 独占域裁决自洽；round-2 起点在案；终值收敛后按既定口径走受控重注册+15_账本。
4. **2_4 残差 round-2 归因**：**✅ 立案准**——c3≡c4 逐位恒等（策划端复核实锤）证明残差与 Sally 边际无关，flip 模式非决策路径单因子可解释，intent 日志逐行归因正当（取证性质）。**边界注记**：归因若指向结构域（收兵供体/其他系统供体）→列报策划端再裁禁擅改结构；若属场景考题设计缺口（爬坡窗+⑤idle 携带语义下理论 ~0.9 的 gap 是否可达）→按 AGENTS.md 0.9 卡池流程处置（重生成+重建 baseline+台账）。

**验收三问**：①缺陷为何发生=probe-k 落盘单文件覆盖设计 × 批量搜参多候选证据的矛盾+脚本默认参数未随对撞对象更新；②定性=执行端工具链单次 slip、非策划流程漏洞（策划端复跑无参→FAIL→带参→PASS 即拦截）；③教训库=无新增（钩子3），训练侧候选「批量探针证据独立落盘路径」转 17 手册评估由训练师入册。
**嘉奖**：D597 处方①自用（grep 对账写进 §一=教训复用闭环）+H-05 如实自曝（含根因两层归因）+三臂全跨度响应面设计+c4 对照实验（主动证伪自己 c3 的 Sally 边际假设=实验设计质量高）。

## §八 交付物与 commit

- **代码**：SimFormation.cs L561 单行补线（+注释）。
- **证据**：`runs/f30_gate1/{compare_gate1_relax.ps1, post_refit_kf30_report.json}`、`runs/f30_gate2/kf30_iwRetreat{00,15}_refit.json`、`runs/f30_search/`（c1~c4 patch+报告+评分门 `gate_c3_score/`）。
- **文档**：D-001 §0/§5/§6 勘正、F30 README、17 手册（病5/病9/H-05）、本信、账本/索引（HH.146）。
- **commit**：训练仓+主仓各一笔（见信尾补记）；commit 后 git-plan-sync。

> 训练师 2026-09-10 · 策划端裁决 2026-09-10（D599）
