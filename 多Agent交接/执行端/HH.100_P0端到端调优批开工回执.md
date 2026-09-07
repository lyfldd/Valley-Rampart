# HH.100 · P0 端到端调优批 开工回执

- **承接**：`河谷防线开发计划书具体内容/改造计划/P0_端到端调优_实施清单.md`（件A~E+§十 件F）· D552 解除阻塞
- **执行端**：TraeCode　**日期**：2026-09-07　**完成报告**：HH.101（账本已预留）
- **范围声明**：本批=**真值调参+快速修+行为级修复**（清单 M6 边界）；打分器重构/诊断层/行动池结构升级归王国AI P0 实施批（2_22/2_23），本批零触碰。

## 一、前置核对（清单 §一）

| # | 前置 | 状态 |
|---|------|------|
| P0-1 | HH.92 收口（MVP 验收） | ✅ D552 验收成立（账本销号；fixture 三档 SO 已按授权迁 `Resources/Config/TestHarness/TestFixtureTiers.asset`，读回逐字段对照核准表全绿；行为级由下次 THD 冒烟自然覆盖） |
| P0-2 | HH.89 收口 | ✅ 施工收口 commit f17f763（360s/天唯一真源）；⚠️ 注记：HH.91 报告在账本仍标「待验收销号」——按策划端接单口径（提示词明示阻塞解除）从宽放行，验收销号归策划端顺手项 |
| P0-3 | 诊断跑局基建 | ✅ TestFixtureApi.PlaceKingdom 三档+考跑档 15x+THD 演示容器（seed=907 稳定复现）；观察器白名单含本批新日志域（诊断日志域追加时随 A-T1 一并声明） |
| P0-4 | 开工回执报备 | ✅ 本信（§三 含件A 诊断跑局协议报备——**协议获批前不跑诊断局**） |

## 二、任务行认领与施工序

| 件 | 任务行 | 本串动作 |
|---|--------|---------|
| A | A-T1 静态抄表+A-2 假设树 | **先行施工中**（锚点见 §二.1；跑局部分待 §三 获批） |
| A | A-T2 诊断跑局三组+报告+处方 | 协议报备中（§三）；**未获裁决前零调参改动** |
| B | B-T1 选址诊断静态面 | 随 A-T1 后接续（wall 半径 8/keepClear/空位判定抄出） |
| C | C-T1~T3 野性同族豁免 | A-T1 后施工（行为级修复+四探针+种族1 验收链回归+15_账本注记） |
| D | DZ-071 | **零施工纯指针**（已随 HH.92 件A 清偿；本批 grep diff 零 TimeManager 命中=M2 自查项） |
| E | E-T5/T6/T7（T 级可先行） | 排在件C 后；E-T1/T4 待策划裁决；E-T2/T3/T8=P1 六考观察行不动 |
| F | F-T1~T4 | F-T1 并入件A 诊断跑局；F-T2 首日定性门（未定性不入调参）；F-T3 对照轮（T6 SO 已就位）；F-T4 强制触发冒烟（件A 评分输入就绪后） |

### §二.1 件A 静态抄表代码锚点（A-T1）

| 锚点 | 文件 | 抄什么 |
|------|------|--------|
| 阶段机 | [ScriptStageMachine.cs](../../Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/ScriptStageMachine.cs)（Survive→Develop L84 / Develop→Expand L88 起 / Expand→Military） | 跃迁条件全表（含 AND/OR/取反/streak）+ KingdomBrainConfig SO 阈值真值 |
| focus 门槛/防抖 | [FocusController.cs](../../Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/FocusController.cs)（L89-97 developToExpand_workersMax/popAlarm） | focus 候选门槛+防抖窗参数 |
| 评分器 | KingdomBrain.cs ExecuteFocus L148-191 + UtilityScorer（MilitaryTarget L253 消费点） | 14（Expand）评分链+军事目标函数 |
| 领土缺口 | TerritorySystem.ExpandTick（D326/D327 硬容量门） | needA/非初始占区（H1 Feasible 判定输入） |
| 五考数据 | Logs/P1 p1_run5 系列 CSV | 逐行判读卡死行 |

## 三、件A 诊断跑局协议报备（P0-4，待策划端获批后开跑）

1. **基线组**：`TestFixtureApi.PlaceKingdom(FixtureTier.Midgame)`（工 10/兵 4=五考 k2/k4 复刻态）×2 国对角，15x 考跑 20 游戏日，观察 Develop→Expand→Military 跃迁（推进=环境差异嫌疑；不推进=真死锁复现）。
2. **单变量组×4**（每探针独立局重摆+同 seed+对照基线保留，M7）：①兵 4→5 ②领土 +1 格 ③幸福 +5 ④focus 14 强制解锁（临时 flag，全可回滚）。
3. **判读产出**：A-1 表逐行判死活+A-2 四假设逐个判死/判活（禁跳过）+F-T2 寻路循环首日定性 → 诊断报告落盘 `报告/设计审查/军事期门槛诊断_2026-09-XX.md` → **调参处方呈报，获批前零调参**（A-T2 验收标准原文）。
4. **判死升级路径**：H4 成环/结构性缺陷 → 开 HH 升级策划端，本批降级诊断交付。
5. F-T3 gold 消歧对照轮（「无金可招 vs 评分不选」）挂单变量组之后复用同协议（T6 SO 迁移完成=基线真源就位）。

## 四、纪律自查声明

- **M2**：件D 零施工；本批 diff 禁 TimeManager 命中（每件 git diff 自查项）。
- **M6**：真值/参数域施工；FindAIBuildSpot/UtilityScorer 只动参数不动结构；接单 diff 面自查与 2_22 域隔离。
- **M7**：每探针独立局；同 seed；flag 全可回滚。
- **M8**：凡改值走 so-data-driven 三规（默认值+Resources 路径+Play Resources.Load 验证）。
- **M9**：调参终值冻结点=P1 六考开跑前；六考期间禁调参。
- **L-12**：同文件多处编辑禁并行（逐个串行+grep 双锚点终验）。
- **玩家侧零回归**：全批红线；件C 回归含种族1 验收链野性探针⑤+Smoke_12+四容器。

---

> 开工回执完 · 件A 静态抄表即时开工 · 诊断跑局候 §三 获批 · HH.101 完成报告预留（账本已登记）。
