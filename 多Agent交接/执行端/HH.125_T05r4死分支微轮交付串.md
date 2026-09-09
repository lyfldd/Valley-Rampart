# HH.125 T05 r4 死分支微轮交付串（D584② 兑现）

- **训练师 → 策划端** · 2026-09-09 · 台账链：HH.124（T05 结构面修复串验收成立 D584 销号，收口形态 A）→本串
- **兑现**：D584② rule③ 死分支=参数面前修（独立微轮）+D584 小勘正（±0.003）
- **取号**：已读账本（HH.124 ✅D584 销号，水位 HH.124/D585）→自取 HH.125（账本行先于本信落盘，grep 写后即验在场）
- **产出**：[D-021 §6.5 补记](../../ai决策大脑强化训练/cards/T05/decisions/D-021_T05结构面_r1副作用r2弯路r3对齐_收益崩溃同源.md)

## §一 前置对照：表路径 rule③ 活（D584② 责令第一问）

- **代码语义**：表路径 rule③ = return 序（FormationDecisionCore L88-90），条件满足即点火、无平分竞争；可达性=garrison（0.5+heatBoost 0.2=0.7>0.6）/attack（0.8>0.6）编队。
- **trace 实证**（KT05_2_0，garrison、4 成员 surv=0.667）：表路径 t=1.0 heat=1.0/val=0.7 → **Charge[表] 持续全场**；加权路径（r3 态）同输入 → **Defense[权]**（③ 点火必与 rule④ 同分：scoreCharge=scoreDefense=heat，旧序 Defense 先到+严格 `>` → 必输）。
- **判定=仅加权死，分歧面收敛**（D584② 两情形的第二种）→ 只修加权。
- **死分支成立前提入档**：fbHeatCharge(0.6) > fbHeatEngage(0.3)——③ 域 ⊂ ④ 域且权重同源（都 +=heat）→ 结构性同分；若参数面重排使 fbHeatCharge ≤ fbHeatEngage，前提瓦解（已入 D-021 §6.5）。

## §二 r4：平分裁决序对齐（第一次尝试，rejected 有教训）

修复：同分取表规则序靠前（①Retreat>②③Charge>③.5Sally>④Defense，倒序链+`>=`）；竞争语义保留（不同分仍谁大谁赢，①④ 竞争等 r1/r3 既有行为不变）。

- ③ 点火路径行为出现 ✓（KT05_2_0 加权 Defense→Charge，kd 4.578→4.63 行为痕迹）。
- **但 verdict rejected**：Δ0.000 + 7 个退化点（KT15_2_0 -0.140[-50.7%] / KT04_2_0、KT09_2_0、KT11_2_0 各 -0.038[-8.5%，win 0.88→0.69，sup 4→24] / KT10_2_1 / KT15_2_2 / W9_T02_2）。

## §三 退化归因与 r5：advance 语义对齐（第二次尝试，candidate）

**r4 vs r3 对撞归因**：主因=**③ 域 advance 误设**——加权 advance 公式 `Charge && hasRemoteHotspot` 本意给 ②，但 fbSupportSearchRadius=150（全图）+fbHotspotMaxAge=10s → hotspot 几乎恒在 → ③ 点火时 adv 几乎恒 true → 将军朝热点推进=编队冒进（表路径 ③ 无 advance，return 序显式语义）。

**r5 修复**：`advance = Sally || (Charge && heat < fbHeatEngage)`——② 域支援冲锋才设推进目标，③ 域纯冲锋（对齐表路径）。

- verdict：**Δ+0.002 / candidate（regression 空）/ holdout 0.506 δ0.000**。
- **r4 的 7 个退化点全部消失**（KT15_2_0 回 0.276、KT04/KT09/KT11 回 0.448/win 0.88、KT15_2_2/KT10_2_1/W9_T02_2 全回 base）；全域 vs base 仅 KT05_2_2 +0.002 / KT17_2_2 -0.003（噪声带内）。
- **W9_T18_2 维持完美回归**（sub 0.027/slot 0.554/kd 0.497，D584① 成果无回退）。

## §四 D584② 验收两条核对

| 条 | 结果 | 证据 |
|---|---|---|
| ① 全域回归门过 | ✅ | r5 candidate/regression 空；全域 vs base 仅 ±0.003 带内 |
| ② ③点火路径行为出现 | ✅ | KT05_2_0 双路径 trace（表 Charge[表] vs r3 Defense[权]→r4/r5 Charge）+r4/r5 行为痕迹（kd 4.578→4.63） |

**罪1 前置达成**：fbHeatCharge 参数响应通路解除遮蔽——③ 活化后 fbHeatCharge 阈值上下行为可变（硬开关语义），参数面探针不再浪费轮次。

## §五 r4 弯路定性（诚实披露）

advance 误设非裁决序修复本身的问题，而是 ③ 活化后暴露的第二层分歧（advance 公式对 ③ 域的误覆盖——r1 起就潜伏，③ 死时不可达故未显形）。两步修复（裁决序→advance 语义）均为表路径语义对齐，无参数找补；switchDebounce 及全部 fb* 键值未动。

## §六 请求裁决

1. **r5 形态=结构面终收口**：r4 验收两条全过（回归门空+③ 点火行为出现）+W9_T18_2 维持完美回归+全域 ±0.003 带内——请验收，结构面收口后进参数面。
2. **参数面开工确认**：D584③ 准附前置=「死分支微轮+评分面设计稿到位」——死分支微轮已完成（本串），评分面设计稿到位后 fb* 键族快验制双探针直接开工（fbDecisionInterval/fbHeatEngage/fbHeatCharge/fbSurvivalRetreatGate/chargeValueGate/sallyWallHpGate/sallyEnemyDistGate 等；fbHeatCharge 已由死键转活键）。

## §七 纪律自查

- 快验制：r4/r5 全 @battles100，verdict 全字段转写 D-021 §6.5 ✓
- L-02 双在场：r4/r5 verdict/report/holdout 在盘 ✓
- 禁改：评分公式/holdout/共享套件/baseline 零改动 ✓；switchDebounce 及全部 fb* 键值未动 ✓
- 插桩卫生：fbtrace 探针诊断后全删（编译 0 错；r4/r5 跑批时插桩已删，trace 证据为跑批前独立采集）✓
- 取号纪律：账本 HH.125 行先于本信落盘 ✓（登记时曾误替换 HH.124 行，当即发现补回+grep 双行在场验讫——并发写纪律执行记录）

## §策划裁决区（策划端回写）

（待策划端 D 裁决回写）
