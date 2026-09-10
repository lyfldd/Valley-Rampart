# HH.145 王国AI 批D 修复微批完成报告（守军招募 kingdomId 过滤 + F1 归一口径）

> 日期：2026-09-10 · 持有端：执行端（TraeCode·Unity 轨）
> 上游：HH.144 任务书（D598 列报2/列报3 裁决兑现）· 批E 前置
> 验收：策划端（实盘复核纪律）；红线=AI.Core 零直改+同 seed 确定性+阶段机零触碰+正门 L-17

---

## 一、交付构成（git 面 3 改 24+/7- + 探针新件 1 组）

| 类 | 文件 | 内容 |
|----|------|------|
| 改 | `Systems/AI/Formation/FormationController.cs` | **件1（D598 列报2 兑现）**：FindIdleSoldiers 增 kingdomId 过滤面——过滤锚=`ResolveKingdomId()`（override 优先）；`ownKid>=0` 时 `unit.kingdomId != ownKid` 不入池（faction 过滤保留=双重收紧）；`ownKid<0`（空编队无将军未 override）→fallback 现行 faction-only（向后兼容，防玩家 debug 成军链空壳化） |
| 改 | `Systems/AI/KingdomBrain/PlacementScorer.cs` | **件2（D598 列报3 兑现）**：F1 分母改=`max(主城-威胁锚实际 Chebyshev 距离, maxR×div)`（锚与主城均走既有 cell→sub 转换链同源计算，全候选同值环带外一次计算）；威胁≈0 置 0 口径不变、归一 [0,1] 越近越高、argmax/环带序/确定性红线不动；F2/F3 分母不动；文件头归一口径说明同步 |
| 改 | `Systems/AI/KingdomBrain/KingdomBrain.cs`（仅注释） | TrySpawnGarrisonSquad 口径注记更新：跨国招兵"列报观察"→"已修（HH.144 件1）"，防文档说 X 代码是 Y |
| 新增 | `Editor/Smoke/Valley_HH144_Probe.cs` | 微批探针容器（正门 EnterTestRun+ExitTestRun+捕获器+国籍纯度/F1 归一/复原安全阀断言面） |

**AI.Core 零触碰**（diff-tree 文件级）·**阶段机零触碰**·**同 seed 确定性**（P2d 双跑逐字段一致）。

## 二、逐链 grep 与玩家链核对义务（任务书口径 3/4）

FindIdleSoldiers 全库调用方=仅 RecruitStandard（L193）/RecruitReinforcement（L803）两入口，**无绕过调用方**：

| 调用链 | ResolveKingdomId() 路径 | 行为 |
|--------|--------------------------|------|
| B7 将军成军（TrainingSystem L114） | BindGeneral→将军 kid ≥0 | 收紧生效：只招将军国兵 |
| C4 AI 守军（KingdomBrain L578） | SetKingdomIdOverride=创建国 | 收紧生效：国籍纯度（探针 P1a 实锤） |
| 玩家编队补员（FormationPanel L332） | 既有成员/将军推导 ≥0 | 收紧生效：只招玩家兵（正向预期） |
| 编队自动补员（TryAutoRecruit L740） | 同上（编队非空才有减员） | 收紧生效 |
| **玩家 debug 空守军（AIDebugUIManager L784 OnGarrisonClicked）** | **空编队无将军未 override=-1** | **fallback faction-only 现行行为可达**（核对义务达成：新建→InitGarrison→立即 RecruitStandard，无将军无成员——正是任务书口径 2 保留路径，未擅改） |
| debug 样例链（AIDebugSpawnController L330） | BindGeneral→将军 kid | 收紧生效 |

## 三、行为级探针实录（Logs/P1/hh144_probe.log；四轮自纠链，终态 12/12 全绿）

- **r1=7/11**：P1b k2 闲兵缺失（day3 兵营未出兵）+P2b/c/d called=False（TryPick 0 候选）。
- **r2=9/11**：SetWarriors(2,1) 保底后 P1b 绿（k2 兵#7 摆位城旁 3.0 实距行+未入编）；加环带采样诊断行实锤 **r0..3 全拒（Blocked=35 Resource=14）**。
- **r3=9/11**：诊断扩至全带 r0..8=**289 格 ok=0（Blocked=157 Resource=132）+前置三件全 OK**——**破案：Resource 拒因=ValidatePlacement 尾部 CanAfford 国库门（L139-150），day10 k1 穷→全拒**；HH140 探针的 123 候选=P6b 段资源注入（gold+2000/stone+600/wood+600）后的产物，非地形差异。修复=P2 组前镜像 HH140 资源注入。
- **r4=11/11**：资源注入后 P2b F1=0.022（>0，旧口径恒 0=HH.141 留痕病灶复现验证）+P2c 数值自洽+P2d 双跑一致。
- **r5=12/12 全绿**（增补 P1f 复原安全阀，见 §五事故段）：P1a 守军国籍纯度（kid1 守军=1 成员=6 异籍=0）+P1b 负证据+P1c L-21 双断言（守军=1+空壳=0）+P1d 玩家 BindGeneral 路径（5 员全 kid=0+注入闲兵负证据）+P1e B7 回归（本局无将军毕业=空集真；BindGeneral 收紧路径由 P1d 同构覆盖）+**P1f 复原安全阀（玩家 Worker 存活=True）**+P2a~d+P3 正交旁证。

**P7c 断言值固化口径注记**：任务书要求"断言值按新口径重算后固化"——执行端选择**固化=公式级校验**（Score==w1·F1+w2·F2+w3·F3 权重分解重算，容差 1e-3）+双跑逐字段一致，而非硬编码地形相关绝对值（同 seed 局内建筑布局随探针前序段变化，绝对值易碎）；双跑一致红线维持。请验收复核此口径。

## 四、回归（任务书 §三）

| 容器 | 结果 | 备注 |
|------|------|------|
| 2_20B_M7 六轮 | **6/6 ALL PASS** | 招募过滤触碰面回归（本次跑完零判负——见 §五修复后状态） |
| Smoke_5（D348） | **ALL PASS** | 评分域正交确认 |
| Smoke_14/2_20C/2_13_C | **零改动声明免跑** | 触碰面=FormationController 招募过滤+PlacementScorer F1 单函数+KingdomBrain 仅注释；经济/抽象结算/UI 域零消费此三处，grep 自查无触碰面外溢 |

## 五、⚠️"河谷失守"事故归因与修复（用户现场目击，本批最高优先级申报）

**现象**：HH144 探针收尾+2_20B 触发前后，屏幕出现 GameOver"河谷失守"（Editor.log 实锤：`[ThroneAnchor] 工人全灭，王国覆灭 → GameOver（D249）`）。

**根因链（不是忘了正门——EnterTestRun/ExitTestRun 全程在场）**：

1. 判负守卫机制=ThroneAnchor.Update L74-75：`TimeManager.TestHarnessMode==true` 时封死判负轮询（EnterTestRun 置位/ExitTestRun 复位）。
2. **直接诱因=本批探针 P1d 段 `SetWarriors(0, 4)`**：玩家编队探针把玩家 Worker 转职 4 名（玩家开局工人本就少→转职后判负条件"玩家桶0 Worker/Civilian 全灭"在探针局内被满足）。
3. **守卫覆盖期内无事**（TestHarnessMode=ON 封死）；**Finish→ExitTestRun→DisableTestHarness→撤守卫瞬间，判负条件仍满足→轮询恢复 0.5s 内 GameOver**。
4. 2_20B 随后触发撞上 GameOver 冻结局（SmokeApi.EnterGame ActiveMap 幂等不重建），雪上加霜（非主因）。

**定性（L-17 家族新变体，建议入册）**：**守卫覆盖期≠测试副作用无害期**——转职/清场类副作用在守卫 ON 时被掩盖，ExitTestRun 撤守卫的瞬间=副作用裸奔时刻；凡副作用可能触碰判负条件（玩家工人/存档/清场）的探针，**收尾必须先复原世界再撤守卫**。

**修复（三层，本批已落）**：
1. P1d 前玩家职业快照 `SnapshotOccupations(0)`，断言完成后 `RestoreOccupations` 逐单位回填原职业；
2. 探针自建编队（HH144_PlayerSquad）自清理；
3. **P1f 复原安全阀断言**（玩家 Worker 存活=True，ExitTestRun 前置）+**Finish 终检**（撤守卫前 HasRemainingWorker=false→ERROR 显式申报，L-16 可见性纪律）。
4. 修复后重跑：12/12 全绿+P1f PASS+Editor.log 尾部零新增判负行（实锤闭环）。

**存量关联列报**：2_20B 容器仍用 `SmokeApi.EnterGame` 裸局（L-17 存量违规——L-17 入库时"存量容器补改"未覆盖它），本次六轮侥幸零判负（历史批B/批C 同）；**建议另立测试基建微批把 2_20B（及其余 SmokeApi.EnterGame 存量容器）迁正门**，或至少在 ExitTestRun 后强制退 Play 防跨容器状态纠缠。执行端不擅动任务书触碰面外容器，列报请裁。

## 六、玩家侧行为影响声明（任务书 §四 单列要求）

- **产品代码玩家侧零改动**：FindIdleSoldiers/ComputeF1 的改动对玩家链=**正向预期行为变化**——玩家编队招募池从"faction=PlayerCamp 共享面（含全体 AI 国兵）"收紧为"仅玩家国（kid=0）兵"（编队有将军或成员时）；空编队首招路径（debug 链）fallback 保留现行行为零变化。
- **探针局内玩家副作用**：P1d 曾转职玩家 4 Worker（探针测试面）——r5 起已快照+复原+安全阀三保险，跨守卫边界零泄漏（P1f 实锤）。
- 正式局无探针代码（Editor-only），存读档面零触碰。

## 七、教训核查

- **L-21（计数面可达性）**：P1c 守军=1+空壳=0 双断言维持+任务书硬引用兑现。
- **L-13（编译双通道+假成功变体）**：全部四轮重编译均以反射探活验证（ComputeF1 3p 新签名/HH144_Probe 类型在场）；refresh_unity disconnect/retry 响应=真实重编译信号的经验沿用；新文件 .meta 导入坑一次被 Test-Path 拦截。
- **L-16（重入可见性）**：每轮单次触发前确认退 Play；协程捕获器全程挂载。
- **L-19（摆位几何）**：P1b 传送摆位打实际距离行（3.0）+断言不依赖位置（过滤全局性）双保险。
- **L-15（悬空锚点）**：探针 GetMembersSnapshot API 先验 grep 拦截（不存在→反射 _members 先例模式），零返工。
- **新教训候选（本报告 §五，请裁入册）**：**守卫覆盖期≠副作用无害期**（L-17 变体）——ExitTestRun 撤守卫前必须复原世界至无判负风险态；探针转职/清场/存档类副作用需快照复原+收尾安全阀三件套。
- 诊断方法论正面延续：FAIL 不空转（环带采样拒因分布诊断行两连，Resource=CanAfford 破案）。

## 八、账本回执

- HH.144：🟡占用→义务清偿（本报告随附待验收）
- HH.145：🔵预留→🟡已落盘（本报告，待策划端验收销号）
- D 水位维持 D598（本批无新裁决需求；§五 事故归因+存量容器迁正门建议列报待裁）

## 九、批E 前置状态

两件修复兑现+回归收口=批E 解锁前置达成；批E（存档+sim 镜像+冒烟收口+E3 factor_registry+B4 15_账本注记）待策划端验收本批后签发。
