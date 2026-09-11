# HH.162 T01 复开结构先行交付报告（个体级 VictoryConsolidation 落地+四臂取证）

- **日期**：2026-09-10
- **交付端**：执行端·训练轨（TraeCode 训练师会话）
- **依据**：D607 收口信号（「T01 复开已按 D560 四拍板+D566③ 前置链放行——『接近-保持』机制层结构方案属 harness 自治域可直接开工」）+ D560③「结构先行再参数化」+ D567③ VictoryConsolidation 处方个体移植
- **编号**：HH.162（159/160/161 已被并行会话占用，取号先登记后落盘合规）

## §一 交付范围

T01 复开机制层结构先行落地：**个体级 VictoryConsolidation（胜利收兵）判定通道**——补齐编队级 VC（f84da7a1）对无编队散兵的覆盖空洞。纯 harness 自治域，Unity 零触碰。

## §二 结构落地明细（build 0 错 0 警）

1. **新键**：`TuningSnapshot.vcSoloEnabled`（bool）——DefaultTuning=false（零回归线）；champion 未动（缺字段由 SimChampion 容错守卫兜底 false，实盘日志在卷：`tuning 缺字段 1 个（vcSoloEnabled）→ 用代码默认值`）。
2. **判定通道**：`SimBrain.ApplySoloConsolidation`（ThinkCore 管线 ApplyProfessionFactors 后压轴消费）：
   - 触发：战力比（敌/我 ΣCurrentHp，感知面动态单位，静态/工事不入比值）< disengageRatio（复用 champion 0.25）持续 consolidationConfirmTicks（复用 2.0s）防抖；
   - 执行：朝 HomePoint 收拢（打带跑=攻击注册独立管线不动，射程内照打）；
   - 解除：战力回升 ≥0.25 或最近敌重入 reEngageRadius（复用 6.0）——粘性防振荡；
   - 排除：FollowIsActive/HasFormationSlot/_reformActive/_formationAdvanceActive（军令/编队成员归编队级 VC 管，两通道互斥不叠加）；
   - **负探针硬条款=结构性保证**：战力比 ≥ 阈值或无敌可见永不触发（防变相逃跑）。
3. **零新键原则**：复用 champion 三标量（disengageRatio 0.25/consolidationConfirmTicks 2.0/reEngageRadius 6.0），仅加 1 bool 开关。
4. **仪器面**（仪器先于训练）：`soloConsolidationTriggered/soloConsolidationActiveEnd` 两计数器全链入 SimRunResult→SimWorld（CountSoloConsolidation）→SimMetrics→SimReporter（正式 report.json）+Program.cs probe-k 快报（probeEntries）。

## §三 覆盖空洞实证（直读，零跑批成本）

baseline v9 report.json 六 T01 场景（W9_T01×3+KT01_2×3）`consolidationTriggered=0` **全场**——编队级 VC 只覆盖 formationGid≠-1 单位，T01 散兵全 formationGid=-1=无判定实例。对照 KF27/KF30 池 consolTrig>0 正常触发=编队级结构在位、覆盖面缺口坐实。此即 D567②「胜利即收兵语义缺失」的结构定位精化。

## §四 四臂取证（@30 局/场景，挂「取证性质」标签=D607 a 纪律兑现）

| 臂 | 配置 | 结果 | 判读 |
|---|---|---|---|
| arm1 键关 | champion 原值 KT01 池三场景 | win 0.978/kd 1.222 | vs HEAD 在库报告逐位全等+同日重跑逐位一致（时间戳除外）=**零回归线+确定性 PASS** |
| arm2 键开 | patch T01_vcsolo_on KT01 池 | win 0.978/kd 1.167 | 2_0/2_1 逐位不变+soloTrig=0（**负探针安全**）；2_2 微响应 kd 3.633→3.467（soloTrig>0 触发后解除） |
| arm3 键关 W9 | W9_T01 三场景临时副本（跑后即删，卡池零污染） | win 0.800/annih 0.844/kd 0.226 | 病理局 _92（win 0.4/kd 0.678）在卷 |
| arm4 键开 W9 | 同上+patch | win 0.789/annih 0.833/kd 0.220 | 病理局 **soloTrig=37（≈1.2 次/局触发面真实）**但 win 0.4→0.367 微退化 |

- 报告载体（独立落盘=H-06 口径）：`results/probe-t01/report_arm{1,2}_key{off,on}.json`、`report_arm{3,4}_w9_key{off,on}.json`、`report_arm1_rerun.json`。
- 13:37 并行会话 M 件备份：`results/probe-t01/report_1337_parallel.bak.json`（并行纪律：原样保全零触碰，不纳入本串 commit）。

## §五 混编局语义缺口列报（D-010，待裁不擅动）

病理局键开微退化根因=**战力比口径在混编局（动态敌+静态工事）暴露**：动态敌快灭→比值<0.25 触发收兵→但 annihilation 胜利条件需拆 Gate（静态 800hp 不入比值）→收兵回家被塔磨=「变相逃跑」条款在混编场景真实暴露。四候选修法（a 比值纳入静态 hp/b 加无可攻击静态目标门/c 收兵目标改最近可攻击敌/d 维持现状等搜参补偿）列报 D-010 与策划端，建议与 vc 键族搜参轮合并裁决。**键默认 false，champion/生产链路零影响。**

## §六 附带清偿与文档

1. **registry 四行补登记**（`schemas/factor_registry.example.json` group=consolidation）：vc 三标量（**f84da7a1 落地时只入 champion 未入 registry 的 D567 遗漏，本笔一并清偿**）+vcSoloEnabled——vc 键族搜参轮提案合法性前置。
2. **15_账本一·补二十三**：结构键登记+Unity 回灌义务（vcSoloEnabled=Unity 死字段，回灌归属 2_18 咬合面，D567③ 口径）。
3. **T01 卡资料刷新**：`取证件三_个体级收兵结构与四臂取证_HH161.md`（取证件一/二续篇）+`decisions/D-010`+README（状态🚧→结构先行达成+待办刷新+D-008/009/010 台账链接补齐）。

## §七 T01 复开条件对账（D560④+D607）

| 前置 | 状态 |
|---|---|
| ①v9 分档表+优势档行为流 | ✅ D567② 验收（取证_v9分档表_D566.md 两件） |
| ②机制层方案落地 | ✅ 本笔（个体收兵通道+编队级在位=「胜利即收兵」语义补齐） |
| ③结构先行→tuning 键→受控重注册 | 结构✅本笔/键✅本笔（bool 开关+三标量在册）/搜参轮→受控重注册=下串 |
| ④扩容重建报备 | ⏳ vc 键族收敛后报备，新 baseline 一次到位（D595②），F28/T03/F27 以新锚重估（D607 b） |

## §八 红线自检

- Unity 仓零触碰（纯 harness/训练仓变更）；评分公式/ObjectiveFunction 零触碰。
- champion/tuning.champion.json 未动（vcSoloEnabled 缺字段容错兜底=零回归线；受控重注册待搜参轮）。
- 卡池零污染（W9 副本跑后即删，git 状态核验）；卡内场景文件零改动。
- probe-t01 13:37 并行会话 M 件零触碰（备份保全，不纳入 commit）。
- 跑批全部挂「取证性质」标签（验收判据跑批留待 vc 收敛后新口径一次到位，D607 a/D595②）。
- 同文件多处编辑竞态 slip 一次（SimRunResult 字段声明被并行 SearchReplace 吃掉→build CS0117 拦截→串行重写修复）——病9 同型，自纠披露。

## §九 待裁事项（策划端）

1. D-010 混编局语义缺口四候选修法（a/b/c/d）——建议与 vc 键族搜参轮合并裁决。
2. vc 键族搜参轮开工准（T01 卡内 patch，vcSoloEnabled+三标量，取证→收敛→验收判据跑批）。
3. 扩容重建 baseline 报备时点（D560④，vc 收敛后）。

> 训练师 2026-09-10 交付 · 待策划端验收裁决

---

## 策划裁决（策划端回写，2026-09-10）

> 裁决依据：D-010 列报 + 取证件三 §四 + **实盘复核**（主仓 HEAD 0604c48=HH.162 git-plan-sync，ahead 3 未推送；训练仓 HEAD 4b4f9afd；harness 注释 HH.162=**10 处**/HH.159=**0 残留**；`results/probe-t01/` 四臂报告 `report_arm{1,2}_key{off,on}`/`report_arm{3,4}_w9_key{off,on}`/`report_arm1_rerun`+`report.json` 在卷；主计划书工作日志 HH.162 行在册）——**申报零失实，验收成立**。

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| ① D-010 混编局语义缺口四候选修法 | **采纳 b（加「无可攻击静态目标」门）；a/c/d 否决** | §一 |
| ② vc 键族搜参轮开工 | **准予开工，前置=b 门落地** | §二 |
| ③ 扩容重建 baseline 报备时点 | **维持 D560④/D607b，时点=vc 收敛后一次到位** | §三 |

### §一 采纳 b（加「无可攻击静态目标」门）

- **根因=语义缺口（判定条件缺变体覆盖），非参数问题**：VC 意图=「胜利即收兵」，触发口径却用**动态单位战力比**——纯动态敌局「动态敌快灭」≈「已取胜」（故读作收兵）；混编局「动态敌快灭」≠「取胜」（静态 Gate 尚存），比值把「仗没打完」错译成「眼前敌人弱」→触发收兵回家、弃打 Gate=「变相逃跑」条款真实暴露。
- **d 否决**（维持现状等搜参补偿）：用参数补语义=库内 **L-10/H-01「指标先于参数」红线**；比值<0.25 属误触发，disengageRatio 低值→误触发更多、高值→纯局该收不收，两向无解。
- **a 否决**（比值纳入静态 hp）：污染「静态/工事不入比值」既有精算口径（§二 刻意设计）；且**依赖血量量级**——小分队对 800hp Gate 时比值未必回升过 0.25，病灶照旧（不可靠隐式门）；并把「工事=敌人战力」写进态势语义，回灌 Unity 连带污染 sim 同源。
- **c 否决**（收兵目标改最近可攻击敌）：只改「去哪」不改「该不该触发」；且全局改变 VC 行为语义，纯局亦被改动，与打带跑/军令互斥面产生新冲突，风险>收益。
- **b 采纳**：VC 触发处加**显式门「无可攻击静态目标」**（存在仍可攻击的静态目标时 VC 不触发）——直接对齐「胜利=所有可攻击目标已清除」；零污染比值口径、确定性可测、合 D560③「结构先行再参数化」。
- **实现硬条款**：门须定义在**触发语义层**——个体级 `SimBrain.ApplySoloConsolidation` 与编队级 VC **共享同一「胜利」判定**，禁只修散兵而编队通道同病复发；补正负探针（混编局 Gate 在场→不触发 / Gate 清除后→触发 / 纯动态局→回归不变）；键默认 false 维持至搜参轮。

### §二 vc 键族搜参轮开工准

- 范围=T01 卡内 patch（`vcSoloEnabled` + 三标量 `disengageRatio`/`consolidationConfirmTicks`/`reEngageRadius`），取证→收敛→验收判据跑批。
- **前置=结构先行（D560③）**：**b 门必须先落地**——在语义正确的触发面上搜参；否则搜的是坏面的最优值=白跑（D595②/L-10）。b 落地后 a/d 的「补偿」顾虑消除，搜参面干净。
- **硬纪律维持**：a) 键族落地前 T01 跑批=**取证性质**（落台账不作验收判据，验收判据跑批新口径一次到位，D595②）；b) 收敛后**受控重注册**（champion 单源）+ 15_账本登记。

### §三 扩容重建 baseline 报备时点（维持 D560④/D607b）

- **时点**=vc 收敛 + b 门落地 + 受控重注册**之后**，**一次到位**扩容重建 T01 卡池 + 重建新 baseline。
- **报备材料**（训练师提报、策划端抽查）：新池构成（gen-random 规模/去重口径）+ 新 baseline 规模与口径 + 旧锚→新锚位移声明。
- **锚点纪律**：F28/T03/F27 三池重估须以**新 baseline 为锚**（D607 b），不得沿用旧锚 0.417；T06 复活判定基于旧锚已成立（D607①），新锚落地后如涉 T06 逐项复验。

### §四 验收三问（vr-planner-leadership 钩子2）

1. **为什么会发生**：VC「胜利」定义只在纯动态敌局验证过，首次落地未对**目标构成变体**（动态敌/混编/纯工事）做语义核查=设计侧语义完备性检查缺项。
2. **单次失误 vs 流程漏洞**：**执行端零失误**（结构落地+四臂取证+覆盖空洞直读+如实列报混编局微退化）；暴露的是**策划/设计侧语义覆盖检查缺项**。
3. **教训库**：**入库 L-23**「机制落地须做目标构成变体语义核查」。

### §五 附带

- **L-12 再犯注记**：§八 自曝同文件多处编辑竞态（SimRunResult 字段被并行 SearchReplace 吃掉→CS0117 编译门兜住）——L-12 已二次实证（D552），本次靠**编译门**而非「每处编辑后立即验证」纪律主动拦截=防范动作未执行到位；记再犯不新开条；执行端下串补：同文件多编辑**单线程串行+每处后立即 grep/pwsh 直验**（L-12 原文）。
- **push**：主仓 ahead 3（9b9ea06/e813cc7/0604c48）+训练仓本地 commit（4b4f9afd）维持未推送（用户未授权）。
- **取号勘正复核**：注释残留 HH.159→HH.162（10 处/7 文件）实盘复核=HH.162 10 处 / HH.159 0 残留，成立。

> 策划端 2026-09-10 裁决回写 · **验收成立（D615）** · 执行端下串=①b 门落地（机制层结构）②vc 键族搜参轮③收敛后受控重注册→扩容重建 baseline 报备
