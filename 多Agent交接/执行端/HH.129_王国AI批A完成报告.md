# HH.129 王国AI P0 批A 完成报告（快照骨架+态势层军事块+邻接修正+内源节拍地基）

> 日期：2026-09-09 · 持有端：执行端（TraeCode·Unity 轨）
> 上游：HH.128 开工回执（D589 解锁；任务真源=2_22 P0 实施清单 §一批A）
> 验收：策划端（实盘复核纪律）；红线=AI.Core 零直改+同 seed 确定性+玩家侧零改动+常设底线三级不动

---

## 一、交付构成（git 面 5 改+5 新增，351+/5-）

| 动作 | 文件 | 内容 |
|------|------|------|
| 新增 | `Assets/_Game/Systems/AI/KingdomBrain/SituationSnapshot.cs` | A1：统一国情快照（D528 三块结构+D517 五件聚合+D589 内源节拍时间场）+SituationHub 中枢+MilitaryProfessions 判定；**纯 C# 零 UnityEngine（验收 grep 零命中）** |
| 新增 | `Assets/_Game/Data/Kingdoms/SituationConfig.cs` | A3：态势层配置 SO（威胁窗/统计窗/损毁 TTL/脏标记 TTL/危机线/恢复线） |
| 新增 | `Assets/_Game/Resources/Config/SituationConfig.asset`(+meta) | A3 资产：Resources.Load 实测可载，六字段读回全对（3/10/7/1/2/4） |
| 新增 | `Assets/Editor/Smoke/Valley_HH128_Probe.cs`(+meta) | 批A 行为级探针容器（EnterTestRun 正门+L-16 捕获器+六探针组） |
| 改 | `Assets/_Game/Systems/Kingdom/TerritorySystem.cs` (+58) | A5：AreKingdomsAdjacent/GetAdjacentKingdoms（中区块 4 邻接触+懒缓存）+5 写点失效 |
| 改 | `Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs` (+61/-5) | A5：NeighborMilitary 全体求和→邻接求和；A4：NeedKind 尾插三枚举+NeedScore 三 case（读快照） |
| 改 | `Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs` (+183/-5) | A2：Tick 子步①（态势层重建→Hub.Put）+BuildSituation（五件聚合+确定性排序）+4 事件订阅+损失流水/脏标记/peaceDays 递推+CountGenerals |
| 改 | `Assets/_Game/Core/GameEvents.cs` (+25) | A6：GeneralDiedEvent+FormationDisbandedEvent（readonly struct 尾插） |
| 改 | `Assets/_Game/Systems/AI/Formation/FormationController.cs` (+29) | A6：将军阵亡/编队解散双发布点+ResolveKingdomId/IsGeneralAlive/KingdomId 公开属性 |

**AI.Core 零触碰**（红线①：`Systems/AI.Core/` grep 零改动）·**玩家侧零改动**（建造入口/UI/校验 grep 零命中）·**常设底线三级零触碰**（FocusController 底线段未动）。

## 二、逐项验收证据（对照清单 §一验收列）

| 编号 | 验收标准（清单原文） | 证据 |
|------|---------------------|------|
| A1 | 文件在场；三块结构字段齐；`using UnityEngine` 零命中 | 文件 8936B 在盘；军事真值+经济/人口占位字段齐；Grep 零命中 |
| A2 | Tick 子步序=①重建→②剧本→③评分（顺序断言）；兵种表现统计=确定性窗口平均；机器战力入口径（恒 0）；事件只置脏标记（负探针） | 探针 P1（4 AI 快照在场+Day 对齐）+P3a（事件后未到日 tick 快照未重建，同引用同 Day）+P3b/c（次 tick 刷新+Dirty=true→再次日清零）；BuildSituation 内 UnitPerformance 按 OccupationId 升序 Sort=确定性；MachineCount=0 注释锚 B8 接线位 |
| A3 | Resources.Load 可载；字段与 §八职责一致 | execute_code 实测 loaded=True 六字段全对（threat=3/perf=10/ttl=7/dirty=1/crisis=2/recovery=4） |
| A4 | 三类缺口函数在场并读快照；既有经济维度评分行为不退化 | 探针 P4a（GeneralGap=1.00/FormationGap=1.00/UnitTypeGap=1.00，AI 缺军态三缺口>0）+P4b（SituationHub.Clear 后=0.00 回退守卫）；经济 case 逐字未动；行动条目批A 不落=评分循环无 def 引用=零行为漂移 |
| A5 | 负探针：远距 AI 国加兵威胁分不变；2_17 回注落盘 | 探针 P2b-0（开局全不邻接 nm(1)=0）+P2a（构邻接对 adj(1,2)=True）+P2b 差分（基线 0→k2 注战士=1[邻接计入]→k3 注战士=1[非邻接不计]→拆对还原=0[净场]）；2_17 §3.1.3 回注=§六（随本报告 commit） |
| A6 | 新事件定义在场+发布点在场；事件→脏标记→次日快照更新 | GameEvents 两 struct 在场；发布点=FormationController.OnUnitDied 将军分支+DisbandAll；探针 P5（GeneralDiedEvent→次 tick Dirty=True） |

## 三、探针容器两轮实录（Logs/P1/hh128_probe.log，不入库断言转录）

- **r1（seed 21128）=12/14**：P2a/P2b FAIL。根因=**探针选格策略缺陷**（v1 只取首个 owner==1 格的右一格，该格贴玩家被 ContainsKey 拦截→claimed=false 零写入）；账本复刻 linked=0 实证**邻接 API 本体无缺陷**（现场三步诊断：dirty 置位生效/账本格读数/复刻重建 linked=0）。
- **r2（同 seed 21128）=14/14 全绿**：选格策略 v2（k1 全领土格坐标序×4 邻固定序找无主邻位）。
- **确定性旁证**：r1/r2 同 seed，P1 段四国快照读数逐字段一致（Day=4/peace=-1/generals=0/formations=0）+P2b-0/P3/P4/P5 断言值两轮全同。
- 编译双通道（L-13）：静态锚=read_console 0 error（warning 全存量零新增）；执行探活=探针容器真实进局执行 14 断言。

## 四、既有冒烟零退化回归（六容器实录）

| 容器 | 结果 | 备注 |
|------|------|------|
| Smoke_5（2_17 步骤10 兵力目标 D348） | **ALL PASS** | A5 直接相关域（D348Target/⑦缺口分数） |
| Smoke_9（2_17 步骤9 效用评分器） | 4 OK+1 FAIL（#19评分非空） | **存量红，已归因批A 零因果**（§六 列报1） |
| Smoke_14（2_17 步骤14 抽象经济 P1~P6+#9/#12） | **ALL PASS** | 八探针全 True |
| 2_20B_M7（六轮：四族+换 seed×2） | **6/6 ALL PASS** | 训练链域 |
| 2_20C_M8M9（批4） | **ALL PASS** | 机器域 |
| 2_13_C（批C UI P1~P7） | **ALL PASS** | UI 域 |

## 五、D589 设计输入兑现（列报2 内源节拍归域）

1. **态势层时间场**：`PeaceDays` 字段（自最近接触递推/接触清零/-1=未接触态）——探针 P1 实测在场。
2. **评分域内源化**：A4 三类军事缺口=**纯缺口驱动评分不乘威胁门控**（缺口即评分，威胁=0 依然评分）——批B 行动落地即消费，形成无袭扰环境下的内源建军压力。
3. **两段对照数据消费**：袭扰段 73311（外部刺激驱动样本）/正门段 69496（无刺激死滞样本）已作为探针判读背景入 HH.128 §三；翻案条款（P0 落地后无袭扰环境仍死滞再启动）随 §七 登记。
4. **边界声明**：阶段机 P0 不动（ScriptStageMachine 未触碰）；资源面死滞（⑥招工停滞）归 2_23 资源 P0——批A 零越界。

## 六、列报（4 项，归策划端裁决）

1. **【Smoke_9 #19评分非空=存量红，请裁】**：断言（HH.25 期）期望 f19.Update 后 LastTop≠None；但 FocusController popAlarm 份额式占位轮（HH.86 纠偏后形态）L117 `recruitedTurn` 命中时 `SetFocus(FocusRecruitWorker); return;` **不跑评分不设 LastTop**→新实例首日（工人<8 必 popAlarm+首相位=占位轮）LastTop 恒 None。归因链：①#3 性格分化直接调 ScoreTop=OK→评分器本体健康；②A4 新 case 无资产 def 引用→批A 改动不进入该路径；③FAIL 机理可在 FocusController L110-126 直读复现，与批A 改动零交集。**修复方向（供裁）**：a=冒烟断言改为分型（占位轮断焦点=⑥、让位轮断 LastTop）；b=占位轮补设 LastTop（产品面一行，语义=「占位轮评分未执行」如实反映则不动）。**本批不动**（批A 零因果+不越权）。
2. **【2_17 §3.1.3 回注】**：D515 承诺兑现——邻接格子级口径定案=「两国中区块存在共享边（4 邻接触）」（与 ExpandTick D283/D326 同族），随本报告 commit 落盘（见 §八）。
3. **【观察器白名单】**：批A 新日志面=零新增 Debug.Log tag（BuildSituation/事件 handler 无日志输出；探针容器日志不属产品面）——L-05 白名单核对通过，挂账池列报4 行（combat+GameOver tag）维持七考前提案，本批零追加。
4. **【Performance 备注】**：EnsureAdjacencyCache=O(N) 懒构建（93 格实测瞬时），领土写点置脏失效；UnitTypeGap 每次评分扫 TrainingConfig（资产小，日 tick 级调用频度）——七考 120 日长局如现性能信号再议缓存。

## 七、教训核查（钩子2/3）+翻案条款登记

- **L-02（写后必验）**：每笔施工后 PowerShell 磁盘直验（.meta 生成三验）+git diff 构成清点全批内——正面执行。
- **L-13（编译双通道）**：静态锚+execute_code 探活齐备后才声明编译通过——正面执行（新文件未导入被探活拦住两次）。
- **L-15（锚点直读）**：开工前 7 锚点实读，发现清单锚点漂移 1 笔（NeighborMilitary L206→L254）+缺口 1 笔（TerritorySystem 邻接 API 不存在=转施工件）——如实列报 HH.128 §四。
- **L-17（容器入口）**：HH.128 探针容器=EnterTestRun 正门+ExitTestRun 收尾+15x 上限——首次正面应用至 P0 施工探针。
- **翻案条款登记（D589）**：P0 全批落地后，无袭扰环境七考仍死滞→按 D589 原文再启动（本报告为登记载体，随 P0 后续批次顺延）。
- 新增教训：无（本批无新失误模式）。

## 八、2_17 回注（D515 承诺兑现）

> 2_17_AI王国脑与自主成长.md §3.1.3 追记：D515 邻接修正已于 2_22 P0 批A（HH.128/129）实施——NeighborMilitary 由「全体非玩家王国求和」改「邻接王国求和」；邻接格子级口径=两国中区块存在共享边（4 邻接触，TerritorySystem.AreKingdomsAdjacent，懒缓存+5 写点失效）。玩家国不计入威胁分子（原口径保留）。

## 九、账本回执

- HH.128：🟡占用→开工回执义务清偿（随本批交付待验收）
- HH.129：🔵预留→🟡已落盘（本报告，待策划端验收销号）
- D 水位维持 D589（本批无新裁决）

## 十、策划裁决区（验收时回写）

（预留——验收结论/列报裁决/销号回写）
