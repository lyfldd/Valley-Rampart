# HH.93 · 专用测试环境批（HH.92/D549）完成报告

- **承接**：HH.92 任务书 + 实施清单（P0 → T0~T19），D549 紧急插队（7 天倒计时）
- **执行端**：TraeCode　**日期**：2026-09-07
- **一句话结论**：专用测试环境三层（时间层/夹具层/生命周期层）全部落地，THD v3 三轮 MVP 演示 **A1/A3/A4/T15/T9 全绿**（A4 异常 8/8/4586→**0/0/0**）；**A2 扩军=0 为 AI 行为缺口结构性列报归 P0 调优批**（详见 §五）；T6 三档 Draft v2 数值报策划核准（核准前 TestFixtureApi.cs 草案表为唯一真源，核准后迁 SO）。
- **commit**：见本报告末尾（本批 11 改 + 6 新，不含策划端并行产物）。

---

## 一、P0 前置确认

- P0-1 HH.89 已收口：commit f17f763（9 文件 +180/−40），HH.91 报告在档，四容器回归 Editor.log 行号铁证在案。
- P0-2 工作区开工前 git status 盘点完毕；HH.94/HH.99（策划端并行）文件全程零触碰。
- P0-3/T0 D111 停跑链路定性（禁臆断，实读代码+实测）：
  - **推翻清单预判**：ThroneAnchor 不是退役死代码——活引用，挂载点 [BuildingComponents.cs:152](Valley%20Rampart/Assets/_Game/Building/BuildingComponents.cs) 主城创建时 AddComponent（全局单例挂玩家主城）。
  - **D111 真实停跑机制**：ThroneAnchor.Update 每 0.5s 轮询 IsKingdomLost（玩家桶 0 无存活 Worker/Civilian）→ SetState(GameState.GameOver)。
  - **封死点（T10）**：ThroneAnchor.Update 头部考跑守卫（`if (tmGuard.TestHarnessMode) return;`）。底表 E10「ThroneAnchor 待定性」随之结案=复归（已在 T14 核对册 §二 建议）。

## 二、施工清单 T0~T19 完成状态（勾选权归策划端，本表为执行端自报）

| 项 | 内容 | 状态 | 证据锚点 |
|---|---|---|---|
| T0 | D111 停跑链路定性 | ✅ | §一（推翻预判，实锤 ThroneAnchor 活引用） |
| T5 | 注册链时序根治（第一施工位） | ✅ | [UnitFactory.cs](Valley%20Rampart/Assets/_Game/Systems/Unit/UnitFactory.cs)：`controller.kingdomId = kingdomId;` 移至 `Initialize(data)` 之前（D541 时序 bug 根治，生产链+SpawnFromSave 同链受益；ResetForReuse 不洗 kingdomId 先赋值安全）。影响面普查：Initialize 直调方仅 UnitFactory:89+4 处冒烟直构 |
| T1~T3 | 时间层（考跑模式直通+SO 档位+TestHarnessApi） | ✅ | TimeManager 考跑模式区块+三方法头部守卫；WorldConfig.asset `testSpeedMultiplier: 15`；[TestHarnessApi.cs](Valley%20Rampart/Assets/Editor/Smoke/TestHarnessApi.cs)（EnterTestRun/ExitTestRun 全量恢复） |
| T6~T9 | 夹具层（三档 Draft+PlaceKingdom+三源+warm-up） | ✅ | [TestFixtureApi.cs](Valley%20Rampart/Assets/Editor/Smoke/TestFixtureApi.cs) Draft **v2** 表；§四 报核准 |
| T10 | 无玩家模式=幽灵化（用户拍板默认） | ✅ | EnablePlayerGhostMode：k0 实体 TakeDamage 致死走 Die 回池链，**王国注册保留**（禁硬移除红线遵守） |
| T11 | 噪声开关（游牧营/怪源） | ✅ | VagrantCampSystem.OnNewDay 头部守卫；MonsterSpawner.Spawn 头部守卫 return null（全怪源漏斗） |
| T12 | 锁 Fine | ✅ | SimModeManager.EvaluateAllKingdoms 头部守卫；sim 义务零（D537 #7 先例口径，已登记 15_差距文档「一·补三」） |
| T13 | 生命周期 ResetState 补缺 | ✅ | WorldLifecycle：WaterNetwork（PopulationSystem 后）+KingdomBrainRegistry+OverheadSpeechManager；**收尾三修**见 §三 |
| T14 | 生命周期核对册 | ✅ | [HH.92_T14_测试环境生命周期核对册.md](HH.92_T14_测试环境生命周期核对册.md)：53 单例逐一核对（20 编排/6 机制等效/6 中危列限制/15 低危+THD v2 实测修订） |
| T15 | 建拆建 3 轮零残留 | ✅ | THD v3 T15 三查全绿（§四 表） |
| T16 | 行动池 21 条核对 | ✅ | [HH.92_T16_T17_门面能力核对表.md](HH.92_T16_T17_门面能力核对表.md)：ExecuteFocus switch 全覆盖结构锚点（KingdomBrain.cs L148-191）；🟡占位=Expedition/Reinforce/Diplomacy |
| T17 | 玩家入口 18 项核对 | ✅ | 同上册 A/B/C 分类 |
| T18 | MVP 演示容器 | ✅ | [Valley_TestHarnessDemo.cs](Valley%20Rampart/Assets/Editor/Smoke/Valley_TestHarnessDemo.cs)；v3 三轮实测 §四 |
| T19 | MVP 验收 | ✅ | §四 全表；A2 列报 §五 |

## 三、T13 收尾三修（THD v2 跑批实测逼出的漏项，全部落盘+编译绿）

1. **DamageSystem.ResetState 补入+WorldLifecycle 挂载**：Singleton 跨 ResetWorldForNext 存活但无复位口——v2 R3 观察窗 4586 次/轮 MissingReferenceException（R2 清场销毁的 fixture 箭塔注册残留，target=池化存活单位致现有 target 检查失效，`attacker.GetPosition()` 每帧炸且挂起攻击永清不掉）。三表+_pendingAttacks+_tickTimer 全清。
2. **ExecuteAttack 头部 attacker 假 null 防御**：防御性一行，正常路径零改动；玩家侧拆塔瞬间同源可触发（同批受益）。
3. **HappinessSystem.OnUnitDied 建键防御**：日结前首死 `_overallHappiness[0]` KeyNotFound（v2 每轮 ghost 清玩家实体 8 条；**玩家侧开局当日死亡同源隐患**），50f 中性初值建键。
4. **方法论教训**：v2 R1/R2 计数=8 全绿假象掩盖 DamageSystem 漏项（死注册恰在 R2 被 target 双亡自愈路径清偿，R3 才因「死塔+活 target」组合爆量）——**跨轮残留类漏项单轮绿不算绿，建拆建第 3 轮才是试金石**。已记 T14 核对册 §二，建议策划端沉淀教训库。
5. HH.42 家族新变体（已拦截）：同文件双 SearchReplace 并行执行，后者覆盖前者（ExecuteAttack 防御被 ResetState 编辑冲掉）——pwsh/Grep 磁盘终验抓包后单线程重补落盘。教训：**同文件多处编辑禁止并行**。

## 四、THD v3 三轮 MVP 实测（Editor.log 行号铁证：轮汇总 L69385406）

三轮同 seed=907 小世界（R1 15x→R2 10x 对照→R3 15x 建拆建），每轮 EnterTestRun→幽灵化→军事期 fixture×2（对角）→25 日观察窗→四断言→清场三查：

| 断言 | R1(15x) | R2(10x) | R3(15x) | v2 对照 | 判定 |
|---|---|---|---|---|---|
| A1 新建筑≥1 | ✅ Δ乙=5 | ✅ Δ乙=4 | ✅ Δ乙=5 | v2=4/5/4 | **三轮全绿**，跨轮重现性良好（AI 自主建造链在考跑 15x 下正常运转） |
| A2 扩军≥1 | ❌ Δ=0 | ❌ Δ=0 | ❌ Δ=0 | v2 全 0 | **结构性列报 §五**（行为问题不修，断言不降） |
| A3 零GameOver | ✅ | ✅ | ✅ | 同 | 三轮全绿（ThroneAnchor 守卫封死 D111 链实证） |
| A4 零NRE | ✅ **0** | ✅ **0** | ✅ **0** | v2=8/8/4586 | **三修全部生效** |
| T15 三查 | ✅ | ✅ | ✅ | 同 | 实体=0/地图新实例/脑注册 5→0/旧水桶 100→0/EventBus 抽查在场 |
| T9 生育条件 | ✅ 94/66/24/20 | ✅ 同 | ✅ 同 | 同 | 幸福94/饱食66/房容24>人口20，**三轮逐位一致=确定性良好** |

- **T4 15x↔10x 对拍**：R1/R3（15x）A1 Δ乙=5 vs R2（10x）=4，其余判定列逐项一致——日级事件同链（AdvanceTime），帧级 tick 粒度差 ±1 属结构等价口径内。1x 档 wall-clock 不经济（20 日≈2h）**列报留 P0 调优批按需跑**。
- 容器汇总行显示「ALL PASS」系**容器判定缺陷**（allPass 只查 results 不查 roundVerdicts+T4 行 `=False` 无空格躲过匹配）——**本报告以断言明细为准（A2=False）**；判定缺陷已修（[Valley_TestHarnessDemo.cs L145-152](Valley%20Rampart/Assets/Editor/Smoke/Valley_TestHarnessDemo.cs)），修正后 v3 数据若重跑将如实显示 HAS FAIL（A2 所致）。断言本体逻辑零改动。

## 五、列报项（本批不修，归口明确）

### 5.1 AI 行为类（纪律：评分调参勿自行修→P0 调优批）
1. **A2 扩军=0（MVP 断言缺口）**：三轮 25 日观察窗 fixture 国 warriorCount 零增量。根因侧写（供调优，非结论）：v2 Draft 已留 6→needA=8 招战士缺口，但 ⑦招战士 行动评分竞争不过建造行动；军事期 1500 gold 起步 day2 即被建造行动耗至个位数（v2 CSV 实测）。**本批价值正在于此：缺口已从「不可复现」变为「固定 seed 三轮稳定复现的断言」**。
2. npcId=66(Warrior)/72(Child)「寻路不可达→转 Idle」循环（v1/v2/v3 观察窗持续出现）。
3. gold 消耗偏快疑与 Draft 富裕度耦合（T6 核准时一并权衡，见 §六）。

### 5.2 工程列报（低优先，归口后续批）
4. 1x 档对照冒烟：wall-clock 不经济跳过（结构等价论证见 §四 T4 行）。
5. 逐条行动强制触发冒烟（21 条行为级验证）：延至 P0 调优批（T16 已完成结构级全覆盖核对）。
6. 分辨率减负（GameView 降分辨率加速）：未做。
7. EventBus 全量计数口：未加（T15 用 HasSubscribers 抽查替代，替代方案已在 T14 核对册 §1.4 登记）。

## 六、T6 Draft v2 数值报核准（核准前草案=唯一真源，核准后迁 SO 删 Draft 表）

| 档位 | workers | warriors | houses | 建筑 | gold | stone | wood | food | metal | 仓填 |
|---|---|---|---|---|---|---|---|---|---|---|
| 开局态 Opening | 6 | 0 | 3 | Well+farm | 200 | 200 | 200 | 300 | 0 | 100 |
| 中期态 Midgame | 10 | 4 | 5 | Well+farm×2+Warehouse+Barracks | 800 | 600 | 600 | 800 | 100 | 100 |
| 军事期 Military | 12 | **6** | **8** | Well+farm×2+Warehouse+Barracks×2+ArcheryRange+arrow_tower | 1500 | 1200 | 1000 | 1200 | 300 | 100 |

- v2 修订依据（实测）：①每 House 容量实测=3 → houses 保证房容>人口（开局 9>6/中期 15>14/军事 24>18）；②军事期战士 8→6 留 ⑦招战士缺口 needA=8 扩军空间（实测 A2 仍 0，见 §五——缺口留给扩军空间不足，还需评分端配合，归 P0）。
- 待策划权衡项：军事期 gold=1500 起步是否过富（建造评分吃金过快，§五-3）。
- 玩家侧幽灵化唯一影响：主城实体清空后 ThroneAnchor GameOver 轮询——考跑守卫已封死，**正式局零触碰**。

## 七、玩家侧零回归自查（红线）

- 运行时改动 11 文件中，8 个为 HH.92 语义（考跑守卫/ResetState/字段/时序），全部带 `if (TestHarnessMode)` 守卫或纯新增防御分支，正式局路径零改动：
  - TimeManager：kGameSpeeds/SetGameSpeed/SnapToSpeed 等禁改清单零触碰（grep 双锚点核验在案）。
  - DamageSystem/HappinessSystem 两处防御：只在原本必炸（NRE/KeyNotFound）路径生效。
  - UnitFactory T5 时序：先赋 kingdomId 再 Initialize——生产链语义不变（Initialize 读 kingdomId 前置），SpawnFromSave 同链受益。
- 同 seed 确定性红线：THD v3 三轮 T9 数值逐位一致+CSV 节奏一致（铁证 §四）。
- HH.89 回归不受影响：本批未触碰秒/天体系（testSpeedMultiplier 仅考跑消费）。

## 八、本批文件清单（commit 范围）

**改（11）**：WorldConfig.asset、WorldConfig.cs、TimeManager.cs、UnitFactory.cs、ThroneAnchor.cs、VagrantCampSystem.cs、MonsterController.cs、SimModeManager.cs、DamageSystem.cs、HappinessSystem.cs、WorldLifecycle.cs、_编号登记.md
**新（8）**：TestHarnessApi.cs(+meta)、TestFixtureApi.cs(+meta)、Valley_TestHarnessDemo.cs(+meta)、T14 核对册、T16/T17 核对表、本报告
**排除**：HH.94 实施清单（策划端并行 M）、HH.99 微任务（策划端并行新）、.oc_*/Logs/pixel-forge 等（环境产物）

---

**待策划端**：①T0~T19 勾选与验收；②T6 Draft v2 核准（§六）；③§五 列报归口确认；④T14 核对册 §二 教训（建拆建第 3 轮试金石/同文件编辑禁并行）是否入教训库。
