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

## §七 策划裁决区（待回写）

1. **补线+复测验收**（T1~T3：grep 对账+缩减对撞 PASS+三臂响应面）：
2. **iwRetreat 搜参挂起解除**与否（响应面已证 0/0.522/0.977 全跨度；D597⑤ 挂起条件「补线+复测完成」已满足——训练师理解=可解除，列报确认）：
3. **c3 组合 round-1 现值**知悉/认可（卡内 patch，非 champion；round-2 起点在案）：
4. **2_4 残差 round-2 归因**立案与否（0.45→理论 0.9 的 gap，flip 模式与 Sally 边际无关，疑非决策路径供体）：

## §八 交付物与 commit

- **代码**：SimFormation.cs L561 单行补线（+注释）。
- **证据**：`runs/f30_gate1/{compare_gate1_relax.ps1, post_refit_kf30_report.json}`、`runs/f30_gate2/kf30_iwRetreat{00,15}_refit.json`、`runs/f30_search/`（c1~c4 patch+报告+评分门 `gate_c3_score/`）。
- **文档**：D-001 §0/§5/§6 勘正、F30 README、17 手册（病5/病9/H-05）、本信、账本/索引（HH.146）。
- **commit**：训练仓+主仓各一笔（见信尾补记）；commit 后 git-plan-sync。

> 训练师 2026-09-10
