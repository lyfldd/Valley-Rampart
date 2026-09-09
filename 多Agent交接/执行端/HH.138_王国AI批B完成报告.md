# HH.138 王国AI P0 批B 完成报告（建军链+内源节拍完整落地）

> 日期：2026-09-09 · 持有端：执行端（TraeCode·Unity 轨）
> 上游：HH.137 开工回执（D592 批A 销号解锁）· 任务真源=2_22 P0 清单 §二批B
> 验收：策划端（实盘复核纪律）；红线=AI.Core 零直改+同 seed 确定性+玩家侧零改动+常设底线三级不动

---

## 一、交付构成（git 面 14 改+4 新增，553+/25-）

| 类 | 文件 | 内容 |
|----|------|------|
| 新增 | `Systems/AI/KingdomBrain/BattleLearnedWeights.cs` | B5：局内环自整定（纯 C# 零 UnityEngine=E2 对称纪律；wᵢ←wᵢ×exp(η×(perf−均值)) clamp[0.2,2.5]+归因条目） |
| 新增 | `Editor/Smoke/Valley_HH137_Probe.cs` | 批B 行为级探针容器（EnterTestRun 正门+L-16 捕获器） |
| 改 | `Systems/Building/TrainingSystem.cs` (+61) | B1：`TryTrainFromKingdomPool` AI 桶系统级入口（与玩家同链 TryTrain：国库扣费/族门禁/建筑等级/generalLimit L436）；B7：AI 将军毕业自动成军（BindGeneral→RecruitStandard；**玩家(0)路径零触碰**） |
| 改 | `Systems/AI/KingdomBrain/KingdomBrain.cs` (+183) | B6：ExecuteRecruitArmy 双环选招（候选域 D570 过滤+建筑前置联动+多样性惩罚预留）；B2：ExecuteTrainGeneral；B8：ExecuteProduceMachine（厂前置守卫+本族选型）；B5：日 tick 权重更新+归因日志；BuildSituation 填充内源势能三输入 |
| 改 | `Systems/AI/KingdomBrain/UtilityScorer.cs` | B2/B8：NeedKind 尾插 MachineDemand+三军事缺口 case 叠加内源势能项；`InternalDrive` helper；UtilityAction 尾插 5 枚举（TrainGeneral/BuildBarracks/BuildTrainingCamp/BuildSiegeWorkshop/ProduceMachine） |
| 改 | `Systems/AI/KingdomBrain/SituationSnapshot.cs` | 内源势能三输入字段（DriveEconomic/DrivePopPressure/DriveStorage；现算口径注记 R-A1 接替） |
| 改 | `Data/Kingdoms/SituationConfig.cs`+asset | D590 增补节②③：内源势能权重四参 SO 化（**0.1/0.5/0.3/0.2=数值禁区保守下沿，只接结构不调值**；可训练标量预留） |
| 改 | `Data/Races/RaceDef.cs`+四族 asset | B4：unitPriors 兵种出厂倾向先验（UnitPrior 结构+GetUnitPrior+默认 0.1 底）+四资产回填（Human 10/Elf 7/Dwarf 6/Orc 5 条起步值=执行端按乘数表占位推导，D519 训练自会改写） |
| 改 | `Data/BuildingIds.cs` | 散点收口纪律：Barracks/TrainingCamp/SiegeWorkshop 常量 |
| 改 | `Resources/Config/Kingdoms/UtilityActionConfig.asset` | 行动条目尾插 5（id22~26：⑯训练将军 minStage=Military/⑰a 兵营/⑰b 训练营/㉔ 投掷机厂/㉕ 造机器 need=MachineDemand） |
| 改 | `Systems/Kingdom/SiegeProductionSystem.cs` (±1) | **DZ-083c 注释义务**：L184 过时注释更新（触发方=2_22 P0 批B ㉔㉕ 已落地，D570 转正） |

**AI.Core 零触碰**（diff-tree 文件级）·**玩家侧零改动**（TryTrainFromPool/TryTrain 玩家桶/CompleteTraining 玩家分支/建造入口/UI 均未动；训练链新增=AI 桶并行入口）·**常设底线三级零触碰**（FocusController 未动）·**阶段机结构零触碰**（ScriptStageMachine 未动，增补节④边界）。

## 二、D590 增补节+D592 前置注记兑现（内源节拍完整落地）

1. **结构**（增补节②）：军事三缺口 case（GeneralGap/FormationGap/UnitTypeGap）NeedScore=缺口分+`InternalDrive`（三输入线性加权×SO 权重）；**叠加不整体 clamp**——缺口满格 1.0 时势能仍有边际，无缺口时势能独立给行动压力（无袭扰环境不恒死滞核心）。
2. **数据源**：快照三输入=BuildSituation 现算轻量口径（经济=gold/100、人口=(worker+warrior)/20、仓储=food/50 clamp01；R-A1 真值块落地后接替，交接注记在字段头）。
3. **数值禁区**（增补节③/D592）：权重 0.1 保守下沿+份额 0.5/0.3/0.2 落 SO，本批零调值。
4. **P4a 口径承接**（D592）：探针 P3 实测 GeneralGap 评分 w=0.1→1.032 / w=0→1.000——缺口存在度与内源项权重可辨。
5. **负探针**（增补节5/D592）：internalDriveWeight 置 0→评分退化为纯缺口=1.000（死滞复现自证，P3 差分面）。

## 三、行为级探针实录（Logs/P1/hh137_probe.log；五轮自纠链）

- **r1=9/13**：P4 前置失败（AI 3 日资源不足：k1 石 13<兵营 20→PlacementValidator 资源门拒；现场 execute_code 诊断实锤）。
- **r2=7/13**：注资源后暴露第二层——**TryBuild=施工启动非竣工**（IsActive=false→FindKingdomBuilding 只认 Active→执行方法早退）；顺带修 P4d 假阳性（k3 兵营缺席早退≠门禁判定）。
- **r3=11/15**：施工等待接入后 ⑯入队 0→1 + **将军毕业=1+编队成员=5（B7 成军链全通）**；新暴露 k1=精灵（族选型假设错）→P5b 拒=正确行为。
- **r4=12/15**：P5 动态本族选型修正后全绿（族门禁负/本族 VineCatapult 正/上限负）；训练营选址失败浮出（空间竞争）。
- **r5=14/16**：⑦双面验证——负面（k1 训练营缺席→无候选不空转=**咬合设计生效正面断言**）+正面（k3 空间松建训练营→双环选招入队 0→1）。
- **终态 2 FAIL=k1 训练营选址空间竞争**（AI 领地拥挤=六考已知表型；⑰评分导向持续尝试+扩张后有空间=设计语义内；⑦行为级由 k3 等价覆盖）——**非产品缺陷，如实列报**。
- 关键断言摘录：P1 先验读回（orc.Berserker=0.80/hum.Warrior=0.50/未列出回退 0.10）；P2a 同输入双国权重序列逐字节一致+P2b 极端死亡 clamp 撞 0.20 下限；P4b ⑯入队+P4e 将军 1+编队 5；P5a 精灵×Ram(兽人专属)=拒+P5b 本族 VineCatapult=成+P5c 机器数≤上限。

## 四、既有冒烟零退化回归

| 容器 | 结果 | 备注 |
|------|------|------|
| Smoke_5（D348） | **ALL PASS** | 军事评分域 |
| Smoke_14（抽象经济八探针） | **ALL PASS** | KingdomBrain/日结链 |
| 2_20B_M7（训练域六轮） | **6/6 ALL PASS** | TrainingSystem 被批B 触碰=必跑 |
| Smoke_9（评分器） | 与批A 后形态一致（#19 存量红维持=HH.131 件3 待修；**无新增红**） | #4/#3 OK |
| 2_20C/2_13_C | 未跑列报 | SiegeProductionSystem 仅注释行变更+UI 域零触碰（改动面正交） |

## 五、列报（5 项，归策划端裁决/知悉）

1. **【兵源池兜底语义】**：AI Resident 池常空（⑥招流浪→Worker、生育归资源 P0）——ExecuteRecruitArmy/ExecuteTrainGeneral 内置兜底=Worker 先 SetOccupation(Resident)（AI 编制内调配）再入训练链。玩家链零触碰。请裁：接受为 AI 侧常态 or 待资源 P0 生育链修复后收窄。
2. **【k1 训练营选址空间竞争】**：AI 领地拥挤下 ⑰选址失败=持续重试（Bump false）——七考观察项（扩张后自解 vs 需 aiBuildRadius 调参=调优域）。
3. **【B5 结算节拍】**：P0=日 tick 窗口结算（七日滚动下同一死亡事件衰减式影响，η=0.05 占位）；战斗结算事件 2_18 接入后切换事件驱动（接口签名不变）。
4. **【B4 起步值】**：四族 unitPriors=执行端按乘数表占位推导（D519 策划不保留配比解释权），训练自会改写；factor_registry S 行（可训练标量预留）登记候选。
5. **【2_20C/2_13_C 回归未跑】**：改动面正交（SiegeProductionSystem 仅注释/UI 零触碰）；如需全量补跑请裁。

## 六、教训核查

- **L-02**：资产三件 execute_code 后读回断言（drive=0.1/acts=26/id22 唯一/id26need=MachineDemand/orc 首条 Berserker:0.8）——正面。
- **L-13/L-17**：五轮探针全走正门+编译双通道；新文件导入坑三次被磁盘直验拦截（.meta 检查常态化）。
- **L-15**：锚点开工前直读——三处清单假设修正（TryTrainFromPool 玩家桶专属/邻接 API 缺口上批已报/兵营 id 字面量散点收口 BuildingIds）。
- **新教训候选（待策划裁入册）**：**"TryBuild=施工启动非竣工"**——建筑 Active 翻转需施工完成，探针/容器断言"建筑在场"必须等施工（r2 教训，建议 L-20）。
- r1→r5 自纠链：资源门→施工态→族选型→空间竞争，每轮现场 execute_code 诊断实锤后改探针——方法论正面延续。

## 七、账本回执

- HH.137：🟡占用→开工回执义务清偿（随本批交付待验收）
- HH.138：🔵预留→🟡已落盘（本报告，待策划端验收销号）
- D 水位维持 D592（本批无新裁决）
- 批C 姿态层解锁预告：C1 三档消费 SituationConfig.crisisLine/recoveryLine（批A 已落字段）+㉕ MachineDemand 批C 警戒/动员档细化（注释锚在 case 内）

## 十、策划裁决区（验收时回写）

（预留——验收结论/列报裁决/销号回写）
