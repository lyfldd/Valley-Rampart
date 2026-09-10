# HH.144 王国AI 批D 修复微批任务书（守军招募 kingdomId 过滤 + F1 归一口径）

> 签发：策划端（D598，2026-09-10）· 执行：执行端（TraeCode·Unity 轨）
> 依据：HH.141 列报 2/列报 3 裁决（0.6 §一百二十七 D598）· 批E 前置（2_22 §五 解锁条件）
> 取号：HH.144（读 _编号登记.md 直取；完成报告=**HH.145** 同笔预留）
> **本批相关教训引用（钩子1 排雷）**：**L-21**（目标驱动循环计数面可达性——件1 守军链即此域，探针必含计数面翻转+失败终止双断言）/ **L-20**（TryBuild=施工启动非竣工，涉建筑断言等 Active）/ **L-13**（编译双通道：静态锚点+执行探活；refresh 假成功变体=行为特征反射探活验证）/ **L-17**（容器强制 TestHarnessApi.EnterTestRun 正门+ExitTestRun 收尾）/ **L-16**（协程捕获器+Play 态重入须全清——HH.140 当日重入陷阱直接教训）/ L-19（摆位几何，涉位置邻近断言时回锚）

---

## 一、件1：FindIdleSoldiers kingdomId 招募过滤（列报2 裁决兑现）

**病灶（HH.141 实锤）**：`FormationController.FindIdleSoldiers`（L222-246）按 `unit.Data.faction != faction` 过滤（L232，3.0.1_6 §4.3「本阵营」字面=共享 camp 粒度）；AI 王国单位 faction 实测=PlayerCamp（归属面=kingdomId 既有模型）→AI 守军编队（fc.faction=ResolveKingdomFaction=PlayerCamp）招募池=玩家+全体 AI 国兵（m=6 编队 kid=0/0/3 实锤）。B7 将军成军/玩家编队同款既有模型。

**实施口径（D598 裁定）**：

1. `FindIdleSoldiers` 增 kingdomId 过滤面：过滤锚=`ResolveKingdomId()`（既有推导链，override 优先）；
2. 过滤规则：`ResolveKingdomId() >= 0` 时，`unit.kingdomId != ResolveKingdomId()` 的单位不入池（faction 过滤保留=双重收紧）；`ResolveKingdomId() < 0`（空编队无将军且未 override）→ fallback=现行 faction-only 行为（向后兼容，防玩家 debug 成军链空壳化）；
3. 受益链（同函数自动生效，逐链 grep 确认无绕过调用方）：AI 守军（override=创建国）/B7 将军成军（将军 kingdomId）/玩家编队（kid=0）/补员 `RecruitReinforcement`（L796 同函数）；
4. 玩家链核对义务：玩家编队创建链（AIDebugUIManager/SelectionController 域）若存在「空编队首次招募时无将军无成员」路径，确认 fallback 行为可达并列入报（不擅改玩家链语义）。

**验收句（探针义务）**：P5 组重跑扩展——

- AI 守军编队成员 `unit.kingdomId` **全部==创建国**（国籍纯度断言，m≥2 且含异族注入局=负证据设计：场景预置他国闲兵在锚点附近，断言其不入编）；
- 守军编队=1+空壳=0（L-21 计数面翻转断言维持）；
- 玩家编队只入玩家兵（kid=0，注入 AI 闲兵负证据）；
- B7 将军成军成员国籍=将军国（回归不破）。

## 二、件2：PlacementScorer.ComputeF1 归一口径（列报3 裁决兑现）

**病灶**：`ComputeF1`（PlacementScorer.cs L121-125）分母=带径 `maxR×div`（=32 sub @aiBuildRadius=8/div=4）；威胁锚距主城>带径（实测 k1↔k2 45 格=常态地图形态）时带内 F1 恒 0=w1 空转+军事朝向性（2_22 §3.7 验收判据）系统性不可达。

**实施口径（D598 裁定）**：

1. 分母改=**max(主城-威胁锚实际 Chebyshev 距离, maxR×div)**（锚与主城均 cell 域换算 sub 域后取距，与现锚点转换链同源）；
2. 威胁≈0 → F1 置 0 口径不变；归一仍 [0,1] 越近越高；argmax/环带序/确定性红线不动；
3. F2/F3 分母不动（本裁只涉 F1 语义=威胁方向朝向度）；
4. 探针断言同步：P7a（近锚>远锚保持；带外局新增「带内 F1 非恒 0 且朝锚侧梯度」断言）、P7c（Score 数值随口径变化，断言值按新口径重算后固化；同 seed 双跑一致红线维持）。

**验收句**：P7 组重跑全绿+P7c 双跑逐字段一致+P1/P5 组无回归（滞回/守军链与 F1 正交旁证）。

## 三、回归与收口

- 六容器抽查：2_20B 六轮+Smoke_5 全 PASS（评分类/编队类正交确认）；Smoke_14/2_20C/2_13_C 零改动声明可免跑（本批触碰面=FormationController 招募过滤+PlacementScorer 单函数，不涉经济/流程 UI/抽象经济），若 grep 自查发现触碰面外溢则补跑全量；
- 编译双通道（L-13）+diff-tree 构成申报+玩家侧零改动 grep 自查随报告；
- commit+git-plan-sync 随串。

## 四、红线承诺（继承 HH.140 批）

AI.Core 零直改 / 玩家侧零改动（本批 FindIdleSoldiers/ComputeF1 改动属 AI 侧消费面行为修正，玩家编队行为变化=跨国招兵修复的正向预期，须在报告单列「玩家侧行为影响声明」段）/ 同 seed 确定性 / 阶段机零触碰 / 容器正门 L-17。

## 五、交付序

实施（件1→件2）→编译双通道→探针容器（复用 Valley_HH140_Probe 扩展或新建 Valley_HH144_Probe，正门）→回归→HH.145 完成报告→策划端验收→批E 任务书签发。

执行端接单开工。
