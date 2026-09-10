# HH.149 王国AI P0 批E 完成报告（E1 存档面+E2 sim 镜像+E3 factor_registry+E4 冒烟收口）

> 日期：2026-09-10 · 持有端：执行端（TraeCode·Unity 轨）
> 取号：`_编号登记.md` HH.149 完成报告同笔预留（HH.148 行备注）
> 依据：HH.148 任务书（D600 签发）· 任务真源=2_22 P0 实施清单 §五批E（E1~E4 原文+验收标准列）+§十.1/§十一（E4 冒烟九项探针）
> 验收：策划端（实盘复核纪律）；红线=AI.Core 零直改+同 seed 确定性+玩家侧零改动+阶段机零触碰+常设底线三级不动+容器正门 L-17

---

## 一、交付构成（git 面：主仓 4 commit + 训练仓 1 commit）

| 件 | 主仓 commit | 内容 |
|----|-----------|------|
| E1 | `a5bd58b` | 存档面：王国脑运行时状态入档+读档恢复（兵种表现统计=损毁流水/姿态档位+滞回基准/权重现值）；模块自治 version=1 旧档可载不炸；读档补建脑三层（GameLoadedEvent+静态枢纽即时落+工厂钩子）；CoreBootstrap 兜底注册 |
| E2 | `03f9703` | sim 镜像 S-1 对拍锚（Unity 侧）：SnapshotAggregator 纯函数聚合（TTL 窗口过滤+兵种表现统计确定性升序），与 sim 镜像同源对称=同输入同输出 |
| E3 | 训练仓 `9fbb237c` | factor_registry S-3 登记：registry_overrides.json 7 行起步值（η/floor/cap+四族 unitPrior）；15_账本 S-1/S-2 行+B8 机器行动 sim 无对应语义注记（D570 差距非阻断） |
| E4 | `474488a` | Smoke_2_22P0 冒烟容器（九项探针+双跑确定性）+KingdomBrain.MachineCount A2/D570 军力现状含机器接线修复（P9f 实锤缺口） |

## 二、锚点声明（全批红线继承，commit 级核验）

- **AI.Core 零直改**：E2 走跨仓协同镜像（训练仓 `harness/KingdomBrain/` 独立实现），主仓 `AI.Core` 目录零 diff-tree；Unity 侧快照/权重静态枢纽（SituationHub/PostureHub/BattleLearnedWeights）纯 C# 零 UnityEngine 引用=E2 对称纪律。
- **同 seed 确定性**：run1/run2 双跑 32 P 行逐字节一致（唯一差异=tag 双跑标识，设计内；时间戳行不入比对）——实证见 §六。
- **玩家侧零改动**：TryTrain 玩家桶/建造入口/UI/TrainingSystem 玩家分支均未动；探针仅触碰 AI 王国（k1~k3）。
- **阶段机零触碰**：ScriptStageMachine 未动；探针 `scriptPhase=Military` 注入=探针级临时态（P3a/P9a），用毕复原（P3c 后/P9a 后各复原）。
- **常设底线三级不动**：FocusController 未动。
- **容器正门 L-17（D600 勘正兑现）**：E4 一律 `TestHarnessApi.EnterTestRun` 正门+`ExitTestRun` 收尾，禁 SmokeApi.EnterGame 裸跑；L-17 变体③三件套（快照→改→复原→撤守卫前终检）落 P7 读档段与 Finish。
- **存档模块自治版本化（2_11 纪律）**：全局 saveVersion 零 bump；KingdomBrainSave 模块 `SavePayload.version=1`；纯加字段走 JsonUtility 缺省兼容=旧档无本条目自动跳过=结构保证零迁移器改动。

## 三、E1 存档面交付（任务书验收句逐条）

1. **入档三件套**：兵种表现统计（KingdomBrain._lossLog+_pendingLosses → CollectLosses 导出 KingdomBrainSaveLossEntry）+姿态档位（Current+LastChangeDay）+权重现值（BattleLearnedWeights.TrySnapshot 只读快照）→ KingdomBrainSave.SaveState 遍历 KingdomBrainRegistry 全脑收集。
2. **读档恢复**：LoadState 静态枢纽即时落（权重 SetWeight clamp 安全栏 0.2~2.5/姿态档）→ GameLoadedEvent 补建无脑王国脑（KingdomBrainFactory.Create）→ 全部 AI 王国 ApplyPendingRestore（RestoreSavedState 清现流水后按存序回填+Posture.Restore 落滞回基准）。
3. **旧档可载不炸（硬性验收）**：模块 version=1+JsonUtility 缺省兼容；E4 P7 探针实锤（旧档无本条目→SaveManager 零兼容负担→结构保证），P7d 断言。
4. **探针强验证**（E4 P7）：存前注入确定性损毁流水 1 条→存（姿态=Alert 权重 Archer=1.7 流水=1）→破坏（权重=1 姿态=None）→读档→权重 1.7 复原/姿态 Alert 复原/流水 1==1 复原——三件套实锤（非 0==0 弱验证）。

## 四、E2 sim 镜像（跨仓协同，sim-sync 纪律全文=先 commit→改→双门禁）

| 对拍面 | 内容 | 证据 |
|-------|------|------|
| S-1 快照 | SituationSnapshot 同源镜像（KingdomId/Day/OwnWarriorCount/Threats/Losses/UnitPerformance/GeneralCount/FormationCount/OwnedCombatOccupations/Drive 三输入/PeaceDays/Dirty+占位块）+SnapshotAggregator 镜像（FilterWindow/BuildUnitPerformance 排除建筑损毁+OccupationId 升序） | `S1_windowCount=4 S1_unitPerfF7=7:2 9:1` 双端逐字一致 |
| S-2 权重自整定 | BattleLearnedWeights **逐字节同源镜像**（MD5 0C1971A65E0E532280AAA88251967E96 双端相等）；MirrorProbe 固定输入（OccupationId 7/9/12/32，Deaths 12/3/1/5，η=0.05） | `S2_deltasF7=7:0.8920031 9:0.9718329 12:0.9905214 32:0.9534969` 值级一致（F7 规范格式规避 .NET R 格式差异） |
| 双门禁 | 训练仓 `dotnet build` 0 警告 0 错误；同 seed 双跑确定性一致 | 编译 exit 0+确定性实录 |
| 15_账本 | S-1/S-2 行登记（MD5+对拍值到账）+B8 机器行动 sim 无对应语义注记（D570 差距非阻断）+S-3 草案登记说明+B4（HH.138）「factor_registry S 行」未落本笔一并清偿 | 训练仓 15_账本 §一·补二十一 |

## 五、E3 factor_registry S-3 登记（本批不进训练）

`registry_overrides.json` added 区尾插 7 行（JSON 校验 added=16 parse OK）：
- `tuning.learnedWeightEta`=0.05 / `learnedWeightFloor`=0.2 / `learnedWeightCap`=2.5（group=learnedWeights，harness=false）
- `tuning.unitPrior_Human/Elf/Dwarf/Orc`=0.5/0.8/0.7/0.8（group=unitPriors，harness=false，consumers=Unity:RaceDef.GetUnitPrior）
- 登记即草案（S-3 起步值），训练师按既定流程自会改写（D519 语义）。

## 六、E4 冒烟容器（探针实录：九项全 PASS + 确定性双跑）

**容器**：`Assets/Editor/Smoke/Smoke_2_22P0.cs`（Editor-only 静态类，双菜单 run1/run2，seed=21140，正门 EnterTestRun，L-16 捕获器，日志 `Logs/P1/smoke_2_22p0_{tag}.log`）。

**九项探针终态（run1 16:50:24 / run2 17:19:50 均 PASS=32 FAIL=0）**：

| 探针 | 断言内容 | 实录关键值 |
|------|---------|-----------|
| P1 将军可训练 | 失败路径①无兵营不空转/P1b 建兵营/P1c Active/P1d 入队计数面 0→1/P1e 成军 | 将军 0→1 编队成员=5 |
| P2 军事建筑可建可训 | 训练营落地/兵种产出/失败路径②兵源池空拒 | 入队=True 军事 1→2，队列 0→0 |
| P3 ⑦按权重分布 | **L-21 计数面翻转**（选招落地 2 次=最高权重兵种优先）+安全栏 clamp+失败路径③穷国不空转 | 序列=Archer,Archer；权重∈[0.2,2.5]；队列 0→0 |
| P4 邻接威胁修正 | 差分法：构邻接对→邻接加兵 +1→非邻接加兵不变→拆对还原 | nm 0→1→1→0 |
| P5 姿态升降 | 威胁注入→警戒档+巡逻>0；威胁清空→无档 | 警戒=True 巡逻=2；None=True |
| P6 AI 驻防 | 动员→守军编队>0（L-21）+空壳=0（失败终止） | 编队=1 空壳=0 |
| P7 读档恢复 | E1 三件套（权重/姿态/流水）+L-17 变体③（快照复原+Finish 终检） | 权重 1.7==1.7 姿态 Alert==Alert 流水 1==1 |
| P8 选址打分器三条 | F1 军事朝向/F2 经济邻近/同 seed 确定性+P8d 失败路径终止性 | F1 0.956>0.556；F2 0.969>0.000；选格(712,512) 双跑一致；半径 0 TryPick=False |
| P9 机器双行动 | P9a 态势触发（军事期注入）/P9b 建工坊/P9c 族门禁负/P9d 正（prefab 内存借用）/P9e 上限负/P9f 军力现状含机器 | MachineDemand 0→0.800；族门禁拒；本族 VineCatapult=成；机器 2≤2；快照 MachineCount=2==落位=2 |

**确定性逐字节比对**（`smoke_2_22p0_run1.log` vs `run2.log` P 行序列脚本比对）：run1 32 条/run2 32 条，唯一差异 @0=`tag=run1` vs `tag=run2`（双跑标识，设计内），其余 31 条探针行**逐字相同**——同 seed 确定性成立。

**修复链实录（五轮自纠，每轮留痕）**：
- r1 首跑 28/4：P2c 兵源池空（将军训练耗 Resident）→EnsureResidentPool 兜底；P4a 反射构邻接对失败（k1 边境被圈死）→三对兜底；P9a MachineDemand 阶段前置→scriptPhase 临时注入；P9e 机器 prefab 缺→Ballista 内存借用（不入盘）；**P9f 实锤产品缺口**=KingdomBrain.BuildSituation MachineCount 恒 0 未接线 → 产品级修复（SiegeProductionSystem 单源计数）。
- r2 31/1：P3a ⑦选招 0 落地（诊断根因：D348 软帽=2+workerCount 约束+成军耗尽 Worker→门控拦截=产品正确语义）；P4b 邻接差分未生效（k2 AI 国无战士=差分对象缺失）。
- r3 32/0：P4b 修（三国保底+非战斗差分起点）✓；P3a 修（TopUpResidentPool 补训练兵源）仍 0 落地。
- r4 32/0：P3diag 实证门控值（i=0 warrior=2<target=4 破但 phase 被 AI 覆盖+训练毕业需 2-3 天>WaitDays(1)）。
- r5 **32/0 全 PASS**：P3a 三修=每轮重注入 Military 阶段（抬 stageFactor）+TopUpWorkerPool 抬软帽+WaitDays(3) 等毕业——「未达目标」前提恢复=选招执行。

## 七、既有冒烟零退化回归表

| 容器 | 结果 | 备注 |
|------|------|------|
| Smoke_14（抽象经济） | **ALL PASS** | P1~P6+#9/#12 全 True（17:22） |
| 2_13_C（流程UI与输入档） | **ALL PASS** | P1~P7 全 True（17:38） |
| 2_20B（训练域六轮） | **裸局中断列报** | 存量裸局容器（SmokeApi.EnterGame 无加速/无守卫），首轮进局后 Play 退出=HH.150 备注「六轮侥幸零判负」风险实证；**批E 代码零改动**；迁移验证归 HH.150 补跑（HH.150 §四协调口径） |
| 2_20C（抽象经济 M8M9） | **M8 全 PASS + M9 裸局不可完成列报** | M8=P1~P3 种族性格分离全 PASS（实录：orc 好战 0.76>hum 0.46；人类零回归均值 0.397≈0.40）；M9 端到端多日消费裸局无加速（360s/天）不可完成；**批E 代码零改动**；迁移验证归 HH.150 |

**HH.150 时序协调（协调提示落实）**：HH.150 尚未开工（排期=批E 后），批E E4 回归按「当前状态」跑（§四协调口径）——2_20B/2_20C 迁移验证由 HH.150 完成后补跑一轮，届时本报告回归表将随 HH.151 复核刷新。

## 八、列报（5 项，归策划端裁决/知悉）

1. **【确定性容器使用纪律（建议固化）】**：E4 双跑实证——**run2 必须退出 Play 重进（编辑器干净态）与 run1 对称**；若同 Play 内连跑（ResetWorldForNext 重建），清场不彻底导致初始世界不同（P1e 编队成员 run1=5 vs 残留态 run2=11 实证），逐字节比对 FAIL。容器注释已记（`smoke_2_22p0_{tag}.log` 头注释）。请裁：固化到 17 手册双跑纪律 or 容器内加 run1/run2 互斥守卫。
2. **【2_20B/2_20C 裸局回归受限】**：存量裸局容器（HH.150 #1/#2 待迁移正门）在批E 回归时无法稳定完成——2_20B 首轮后 Play 退出（裸局判负风险实证）、2_20C M9 无加速不可完成。批E 代码零改动（红线），迁移验证归 HH.150 补跑。**本项为环境限制非批E 缺陷**。
3. **【KingdomBrain.MachineCount A2 接线修复（产品级）】**：E4 P9f 首跑实锤「B8 造机器链落地后军力现状 MachineCount 恒 0 未接线」——BuildSituation 已改接 `SiegeProductionSystem.GetPlacedMachineCountByKingdom`（单源计数），D570「落地前恒 0」差距关闭；P9f 终态 2==2 实证。
4. **【P3a 门控注入语义】**：ExecuteRecruitArmy 首闸=warriorCount≥MilitaryTarget(D348 软帽 2+工人数) 即返=**产品正确语义**（已达兵力目标不选招）；探针注入 Military 阶段（抬 stageFactor）+TopUpWorkerPool（抬软帽）创造「未达目标」前提，探针级临时态用毕复原。**非产品逻辑改动**。
5. **【B8 机器 prefab 依赖】**：三族机器 prefab 缺（美术批7 未交付），探针正探针用人类 Ballista prefab 内存借用（不入盘不入档，判定后复原）；缺图族造机=扣费无生成=D582 认可语义（与产品一致）。

---
*批E 四件全交付，Smoke_2_22P0 九项探针 run1/run2 双跑全 PASS+逐字节一致，Smoke_14/2_13_C 回归全 PASS；2_20B/2_20C 裸局受限列报（HH.150 协调）。王国AI P0（2_22）至此批A~E 全数收口，请策划端验收销号。*
