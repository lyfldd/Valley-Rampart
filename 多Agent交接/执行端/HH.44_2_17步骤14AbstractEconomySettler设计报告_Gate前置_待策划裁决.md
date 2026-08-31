# HH.44 · 2_17 步骤14（AbstractEconomySettler）开工前置 Gate 设计报告（待策划裁决）

> 类型：策划报告请求（Gate 前置，先报告后代码）
> 状态：⏳待裁决
> 日期：2026-08-31 · 发起端：执行端 · 关联：`2_17_AI王国脑与自主成长.md` §3.3/§3.4（D335/D336/D400）· `2_17_AI王国脑与自主成长_实施计划.md` 步骤14+⑤-3 · `2_20_四族种族体系总纲.md` §六 · `2_20.1_专属兵种行为与种族数值.md` §二 · HH.42 §四（D453）· `15_训练侧harness与Unity端差距文档.md`「一·补二」· Q6 报告 A4/种子②（D401）

## 〇、锚点声明

- **指令源**：任务书 14（2_17 步骤14 AbstractEconomySettler，当前队列）。开工前置=设计要点报告 Gate 交裁，**Gate 未裁禁动代码**——D453/2_20 挂载/种子② 三处待裁在 §三 列出，裁决后按批实施。
- **所依据文档/commit 基准（2026-08-31 实盘核读，非凭记忆）**：
  - `2_17_AI王国脑与自主成长.md` §3.3（D335 对账规则/D336 抽象结算引擎/D400 抽象态全民流失）、§3.4（训练统筹两阶段）、§5.2（AbstractEconomySettler 逻辑层）、§六 冒烟 #9/#12
  - `2_17_AI王国脑与自主成长_实施计划.md` ⑤-3 段（L44）、2b 落地注（L52）、步骤14（L235-242）、§二 第三步+新建（L296-309）、§五 验收标准
  - `2_20_四族种族体系总纲.md` §六（AI 王国种族化 D421「AbstractEconomySettler 未实施——种族经济修正随 2_17 步骤14 设计期一并纳入」）
  - `2_20.1_专属兵种行为与种族数值.md` §二（经济乘数挂载点映射表 D434）+ 2_20 实施清单 §三 L73（双向注记：步骤14 设计时引用 2_20 §五）
  - HH.42 §四·决策点1（D453 Satiety 归 14 裁决原文）+ 0.6 §四十三（D453~D456）
  - `15_训练侧harness与Unity端差距文档.md`「一·补二」（2026-08-26 登记 ⚠️，对账时点标注义务）
  - Q6 报告 A4/种子②（D401 学院退役，Q6 复核维持「随 2_17 步骤14 顺裁」）
  - HH.43 验收回写（4f782db，步骤13 三批成立；「执行端欠 2_17 实施计划尾部状态行推进随下次提交补」——本串清偿）
- **诚实分层**：本报告=静态勘察 + 方案（Gate 前置，未动产品代码、未动设计文档/0.6/2_20 文档）；行为级探针方案见 §四，裁决后实施并跑。

---

## 一、恢复与勘察结论

### 1.1 恢复状态
- HH.43 ✅（4f782db，步骤13 SimMode 三批验收成立）→ 队列 14 当前任务（开工前置=设计要点报告 Gate 交裁）→ 本报告。
- 工作树干净（git status 实测），HEAD=4f782db；队列/_交接索引 只读（状态由策划端转）。

### 1.2 步骤14 现状（勘察证据，file:line 实核）

| 项 | 现状 | 证据 |
|----|------|------|
| AIEconomySettlement（2b） | **已落地**：日结搬运五经济资源入 AI 国库+固定排序（⑤-3 硬性 a）+15 账本「一·补二」登记 | `AIEconomySettlement.cs` 全文；`DayCycleSettlement.cs` L75 |
| SimMode 判定 | 步骤13 已落地：真实判定写 KingdomState.simMode，不入档（D456） | `SimModeManager.cs`；`KingdomState.cs` L66 |
| Abstract 王国现状 | KingdomBrain.Tick 非 Fine 短路（P1 步骤14 交给 AbstractEconomySettler）；NPCBrain 冻结 Abstract 王国非军事单位 | `KingdomBrain.cs` L76-77；`NPCBrain.cs` L396-430 |
| AbstractEconomySettler | **不存在**（待新建） | Glob 无此文件 |
| Satiety | 只处理玩家（kingdomId==0）；AI 完全不进食；`_avgSatiety` per-kingdom 分桶已落（供 AI 下游读） | `SatietySystem.cs` L117-147/L134 |
| sim 侧经济公式 | `harness/Economy/SimEconomy.cs`（QQQ.5 私有副本：EconomyTick 采集+饱食消耗 / DailySettle 生育+招募+训练+税收） | `SimEconomy.cs` 全文 |
| **15 账本「一·补二」前向引用失真** | 账本 L49 称训练侧 `harness/Economy/AbstractEconomySettler`——**该文件不存在**，实际公式源=SimEconomy.cs（QQQ.5 私有副本） | 15 账本 L49-56；harness/Economy/ 目录 |
| Academy 退役（种子②） | 仅登记未动（在役）：AcademyBuilding.cs/ResearchProjectList/ResearchCompletedEvent/Academy.asset/Module_Science.asset/研究 UI | 全库 grep 50 命中；Q6 A4 |
| 2_17 实施计划状态行 | 仍停「步骤13 待实施待裁决」（HH.43 漏推）→ **本串补推** | 实施计划 L4/L407 |

### 1.3 勘察发现（诚实申报）
- **发现①（15 账本前向引用失真）**：15 账本「一·补二」登记的训练侧文件 `harness/Economy/AbstractEconomySettler` 实盘不存在；sim 侧经济公式真源= `harness/Economy/SimEconomy.cs`（DailySettle/EconomyTick）。**本步镜像对象=SimEconomy 公式，非同名文件**——需策划端在裁决中确认口径，并同意 15 账本原文措辞修正（见 Q5）。
- **发现②（Workshop 同属 Science 模块）**：种子② 退役范围不止 Academy——`Workshop.asset`（工坊）moduleType=5（Science），描述「研究（金+时长）」，与 Academy 同为研究建筑；仅 BuildingPanel L453 引用（研究按钮分支），无 KingdomDef 模板引用、无世界放置。D401 原文「六模块收敛为五模块」→ Workshop 应一并退役（见 Q3）。
- **发现③（moduleLevels 存档数组）**：`KingdomState.moduleLevels` 为 per-kingdom 存档 int[]（索引=ModuleType），Science 是枚举尾值（index 5，删除无重排）。退役 Science 时数组长度处置=存档兼容决策点（见 Q3）。

---

## 二、步骤14 技术方案草案（供策划端审，裁决后细化实施）

### 2.1 范围界定（与步骤13/2b 的边界）
- **步骤13 = 切换基建**（已收口）：SimMode 判定+休眠唤醒。**步骤14 = 抽象结算+对账（本步）**：AbstractEconomySettler（纯 C# 公式镜像 sim）+ D453 Satiety AI 进食统一 + D335 对账（切换无跳变）+ ⑤-3 对账时点标注清偿 + 种子② Academy 退役顺裁 + 2_20 种族经济修正挂载预留。
- **2b AIEconomySettlement = Fine 王国收入侧日结路由**（已落地，保持不动）；本步在其上**分叉**：Abstract 王国改走 AbstractEconomySettler。

### 2.2 实施草案（三批，策划裁决后按批实施）
- **批A · AbstractEconomySettler 公式引擎 + 分叉接线**：新建纯 C# `Systems/AI/KingdomBrain/AbstractEconomySettler.cs`（零 Unity 引用，操作自有 DTO + 返回结算增量）——每抽象王国日 tick：人口×生产率→资源（镜像 SimEconomy 采集公式，含 ⑤-3 固定排序硬性 a）+ 每日耗粮（镜像 DailyFoodNeed）+ 抽象态全民流失（D400 追写：居民无粮连续 N 日-1、战士断粮解散）+ 训练/招募队列推进；薄 Unity 适配层（DayCycleSettlement AI 段分叉）负责 KingdomState↔DTO 翻译+增量应用；AIEconomySettlement.Tick() 加 `simMode==Abstract` 跳过（防双写）；15 账本「一·补二」回填+对账时点标注（Q5）。
- **批B · D453 Satiety AI 国库进食统一**：SatietySystem.OnNewDay 扩展 AI **Fine** 王国逐实体进食（复用 SettleUnit，国库源改 `KingdomState[k].resources.Food`，玩家走 RulerController 原样零回归）；**Abstract** 王国实体休眠不逐位结，由 AbstractEconomySettler 按计数公式进食（镜像 sim 饱食消耗+断粮扣均饱食），维护 per-kingdom avgSatiety 桶；唤醒对账=切回 Fine 首次日结算把实体饱食拉平到抽象结算均值（D335/D453 同口径，确定性）。
- **批C · 种子② Academy 退役（asset/引用/文档三面）+ 完整冒烟 Smoke_14**：按 §三 Q3 方案实施；15 账本「一·补二」对账时点标注若批A 已落则本批收尾核验；新建 `Valley2_17_Smoke_14.cs`（对齐 Smoke_13 哲学，覆盖 §四 探针）。

### 2.3 冒烟验收映射（实施计划步骤14 验收 + 任务书预告）
- 冒烟 #9（SimMode 反复切 10 次账本无差）全量部分=本步收口（步骤13 只验切换机制，账本无差归步骤14）。
- 冒烟 #12（同 seed 全程含抽象结算路径逐字节一致）。
- 任务书四探针：抽象不僵死数值合理 / 玩家零回归 / 抽象段固定排序确定性 / P0 基线全绿——见 §四。

---

## 三、报告必答五问（策划裁决用；每点给推荐+理由+影响面）

### 决策点 1：AbstractEconomySettler 与 2b AIEconomySettlement 的关系
- **现状**：2b AIEconomySettlement 已把 AI 建筑 Storage 产出日结入 AI 国库（Fine 王国真实体产出路由）；Abstract 王国现状=无脑 tick+工人冻结，经济静止。
- **选项**：
  - **A（推荐）并存+分叉调用**：Fine 王国走 AIEconomySettlement（实体结算，现状路径保持零回归）；Abstract 王国走 AbstractEconomySettler（公式镜像 sim）。DayCycleSettlement AI 段按 per-kingdom simMode 分叉——Fine→`AIEconomySettlement.Tick()`（新增跳过 Abstract），Abstract→`AbstractEconomySettler.SettleKingdom(k)`。两分支同日在同一结算点跑，粒度一致（王国脑「花昨日结存」HH.24 裁决① 两分支统一）。
  - B：替代——AbstractEconomySettler 统一接管全部 AI 王国结算，AIEconomySettlement 退役。动 Fine 王国已验收路径，回归面大，且实体产出=真实仓储搬运语义被公式替代，D335「军队永远真实体」等实体对账被架空。
  - C：同一引擎双模式参数（Fine=实体搬运开关/Abstract=公式开关）。把 Unity 依赖（Storage/Building）与纯 C# 公式耦合进一个类，违背 D336 零 Unity 引用。
- **推荐 A**：符合 D336「每抽象王国日 tick」+ 2b「Fine 实体真产」双语义；2b 已验收路径不动=零回归；Abstract 公式独立纯 C# 可移植 sim（阶段 B 复用）。
- **纯 C# 零 Unity 引用怎么落**（D336 硬约束）：AbstractEconomySettler **自身不引用 UnityEngine**——操作自有 DTO（`KingdomEconomySnapshot`/`SettlementDelta`），公式纯整数/float 运算；Unity 适配层（薄，放 DayCycleSettlement 或新 `AbstractEconomyBridge`）负责 KingdomState（Unity 域）↔ DTO 翻译 + 增量应用（AddResources/扣 Food/推进队列）。**文件放 `Systems/AI/KingdomBrain/AbstractEconomySettler.cs`，不放进 AI.Core 目录**——AI.Core 是双份拷贝区（与 harness/Core 同源 MD5 一致），而本文件 sim 侧无同名对应物（sim 公式源=SimEconomy.cs，属 harness/Economy 私有副本非双份拷贝），放进 AI.Core 会制造「需同步」误判（见 Q5）。
- **影响面**：AIEconomySettlement 加一行 Abstract 跳过（行为级冒烟覆盖）；DayCycleSettlement AI 段分叉（玩家零接触）；新 DTO/适配层文件+meta。

### 决策点 2：D453 Satiety AI 国库进食统一口径
- **现状**：SatietySystem.OnNewDay 只处理 kingdomId==0（玩家）；AI 完全不进食、饱食恒不变；_avgSatiety per-kingdom 分桶已落（AI 均饱食供王国脑/评分消费，但值恒为实体实时均值且无进食驱动）。
- **选项**：
  - **A（推荐）Fine 实体扣国库 + Abstract 计数进食，同批统一（D453 原文）**：
    - **Fine 王国**：SatietySystem.OnNewDay 扩展 AI Fine 王国——复用现有 SettleUnit 逐实体逻辑（饱食不满阈值→进食恢复，未进食→衰减/扣血/降幸福），但国库源从 `RulerController.Instance` 改为 `KingdomState[k].resources.Food`（玩家路径原样不动零回归）；每日结算后把每王国均饱食写 _avgSatiety 桶（现状已有）。
    - **Abstract 王国**：工人休眠不逐位结（SatietySystem 跳过 Abstract 王国实体——避免冻结实体被结算，与 NPCBrain 冻结语义一致）；由 AbstractEconomySettler 日 tick 按**计数公式**进食：镜像 sim `DailyFoodNeed`（生活职业×1/士兵×2/高耗×3/日）→ 国库 Food 足则扣、不足则断粮扣 per-kingdom 均饱食（镜像 sim 饱食消耗+FoodExhaustedDays 语义）→ 触发 D400 抽象态全民流失项（居民无粮连续 N 日-1、战士断粮解散）。
    - **唤醒对账（D335）**：切回 Fine 首次日结算，把实体饱食**统一拉平**到抽象结算维护的 kingdomAvgSatiety（确定性、无个体残差）——语义=抽象期集体进食结果一次性回写。
  - B：AI 进食只在 AbstractEconomySettler 计数层做（Fine 王国 AI 仍不进食）——D453 只满足一半，Fine 王国实体饥饿/进食缺失，与 Abstract 态衔接仍断。
  - C：Fine/Abstract 各自独立实现两套进食（Fine 实体、Abstract 计数，互不共享 avgSatiety 桶）——D453 明令「同语义一次对齐」，拆两处要改两遍。
- **推荐 A**：D453 原文「Fine 实体扣国库+Abstract 计数进食与 AbstractEconomySettler 同批统一（D336 同构语义一次对齐）」逐字落实；avgSatiety 桶单一来源（王国脑/评分消费不分裂）；唤醒拉平保证无跳变（D335）。
- **影响面**：SatietySystem 改动（AI Fine 分桶结算+SettleUnit 国库源参数化）——玩家路径零改动；AbstractEconomySettler 增进食/断粮/流失段；KingdomConfig 日耗粮表全局共享不改；D400 流失项入公式（**需策划确认流失参数 N 日 SO 化占位**——现状无此 SO，建议随批B 建 `AbstractEconomyConfig` 或并入既有 KingdomBrainConfig，待裁）。

### 决策点 3：种子② Academy 退役方案（D401 尾巴，Q6 A4 维持；本批顺裁）
- **现状**：全库 grep 50 命中研究子系统；Academy/Workshop 双建筑在役（ModuleType.Science 尾值 index5）；2_12 设计 L122「D401 已砍除+代码侧退役登记」；Q6 A4=「仅登记未动」维持随步骤14 顺裁。
- **退役范围（asset/引用/文档三面，勘察清单）**：
  - **asset 面**：`Academy.asset`+meta、`Workshop.asset`+meta、`Module_Science.asset`+meta、`ResearchProjectList.asset`+meta 删除（删前 grep 零引用实证——Workshop 仅 BuildingPanel L453，Academy 无 KingdomDef 模板引用）。
  - **引用/代码面**：`AcademyBuilding.cs`+meta（含 ResearchProject struct）删除；`ResearchProjectList.cs`+meta 删除；`GameEvents.cs` L474-483 ResearchCompletedEvent 删除；`BuildingFactory.cs` L304-305 Science 挂载行删除；`BuildingPanel.cs` 研究按钮分支（L453-468/OnResearchProjectClicked L580）移除；`BuildingMenuPanel.cs` ModuleOrder Science 行+L115 tab-science 移除（含 UXML/USS Science tab）；`KingdomManager.cs` ApplyResearch（L264-270）+GetResearchLevel（L257-261）+LoadModuleDefs「Module_Science」行（L108）+ResearchLevels 数组退役；`ModuleType.cs` Science 枚举删值（尾值 index5 无重排，安全）；`CastleUnlockTable.asset` Science（module:5）行删除。
  - **文档面**：`2_12_王国建筑系统迁移_实施计划.md` L4 头部「D401 尾巴…随 2_17 步骤14 顺裁」→ 改「已退役收尾（HH.44/裁决后实施）」；`2_12_王国建筑系统迁移.md` L31/L122/L132/L168/L477/L489 作废注可保留（历史语境）或加收尾注（执行端不改设计文档，报策划端处理）；Q6 A4 报告/挂账池 种子② 销行（策划端回写）；缺陷台账 DZ-011。
- **选项（深度）**：
  - **A（推荐）全量退役科研子系统（D401 原文「科技线整体移除+六模块收敛为五模块」）**：上述三面全做；`moduleLevels` 数组**保留长度 6、索引5 空置**（删 Science 枚举值后 `(int)ModuleType` 余 5 值仍 0-4，数组槽 5 不再被任何消费者读取=语义收敛已成立）——**零存档 schema 变更**（kingdoms[] 迁移归 2_11，本步不动 schema）。CastleUnlockTable Science 行删除后 GetModuleLevel(Science)=恒 0，KingdomBrain ⑧科技升级 techTargetModule 默认 Civil 不受影响。
  - B：只删 Academy（资产+组件+研究 UI），保留 Workshop+Science 模块槽位——D401「六模块收敛为五模块」未兑现，Workshop 成为无功能 Science 建筑残留。
  - C：全量退役+moduleLevels 数组缩容 6→5（含存档迁移）——最彻底但动 kingdoms[] 存档 schema，与 2_11 迁移时序冲突，超出本步零 schema 纪律。
- **推荐 A**：全量兑现 D401「科技线整体移除+六模块收敛为五模块」；枚举尾值删除安全（sim-sync §六：尾值删除非中间位，无 int 序列化错位）；moduleLevels 保留长度=零存档风险（语义收敛靠「无消费者」而非「数组缩容」）。
- **影响面**：约 10 个代码文件+5 资产+3 文档；玩家零接触（研究系统玩家侧本就已随 D401 在 2_12 砍除，仅代码残留）；**需策划确认**：Workshop 一并退役（发现②）+ moduleLevels 保留长度口径（发现③）。

### 决策点 4：2_20 种族经济修正挂载预留（划清边界防重复活）
- **现状**：2_20.1 §二 经济乘数挂载点映射表（D434）=Q10 实装权威：`mineMul/lumberMul/farmMul→TaskScheduler 生产 Tick（L566）+采集完成入库量（L593-597）两处同乘`；`buildingHpMul→Building.Init`；`meleeAtkMul/rangedAtkMul→DamagePipeline`；`buildSpeedMul→协作施工 tick 进度增量`；`carryCapMul→TaskScheduler 卸货 L655-659`。2_20 实施清单 §三 L73 双向注记「步骤14 设计时引用 2_20 §五」；2_20 §六「AbstractEconomySettler 未实施——种族经济修正随 2_17 步骤14 设计期一并纳入」。
- **选项**：
  - **A（推荐）本步只留公式乘点结构，Q10 实装真值**：AbstractEconomySettler 的产出公式（人口×生产率→资源）**预留 per-kingdom 经济乘数槽**——`EcoModifiers` DTO（mineMul/lumberMul/farmMul/buildSpeedMul，默认 1.0f）+ 乘法挂载点（产出计算处 `×ecoMineMul` 等），**不创建 RaceDef、不读 KingdomDef.raceId**（那是 M1/M2）；Unity 适配层当前填 1.0（占位），并在代码/报告标注「Q10-M5/M8 接入真值」；挂载点约定登记 15_账本或本报告，防 Q10 遗漏。Fine 王国侧任务书明确**不动**（TaskScheduler 采集双点/Building.Init/DamagePipeline 三处实体挂载点=Q10 实装，本步不碰）。
  - B：本步就接 KingdomDef.raceId→RaceDef 真值——提前实装 M1/M2 范围，RaceDef 尚未建，重复活+越权。
  - C：本步完全不预留——步骤14 公式定型后 Q10 再改公式乘点，M 批改动面扩大（抽象王国经济走未乘点公式，Q10 改两处）。
- **推荐 A**：预留「乘点结构+占位 1.0」满足 2_20 §六「设计期一并纳入」；不越权 M1/M2；Q10 只做「真值接入+实体挂载点」，与 2_20.1 §二 映射表天然衔接，防重复活。
- **影响面**：AbstractEconomySettler 增 EcoModifiers 参数槽（默认 1.0 零行为差）；15_账本或报告登记挂载点约定；Q10-M5/M8 按 2_20.1 §二 实装（本步不代做）。

### 决策点 5：sim-sync 义务判定（T/F 分级 + 15 账本「一·补二」对账时点标注）
- **现状**：15 账本「一·补二」（2026-08-26 登记 ⚠️）：差异① AI 国库收入粒度（sim 瞬时无 Storage vs Unity 日结两段式）、差异② 入账时点/滞后（sim 即时 vs Unity 1 日滞后）；L56 对账时点标注义务「步骤 14 镜像公式时需显式对齐该粒度」。实施计划 ⑤-3「不落不算收口」。
- **判定**：
  - **本步不改训练仓任何文件**（harness/Core/Sim/Economy/champion/Holdout/Scenarios 零触碰）——训练边界禁改清单遵守。
  - **不改 AI.Core 决策核**（FactorContext/Faction/决策路径零改动）——AbstractEconomySettler 是**新 Unity 侧纯 C# 结算文件**，sim 侧无同名双份拷贝（sim 公式源=SimEconomy.cs 属 harness/Economy 私有副本，非 AI.Core↔harness/Core 双份区）。**不构成 T/F 分级对象**（sim-sync §六 分级针对「对 harness/共享核的改动」，本步无此类改动）。
  - **15 账本「一·补二」回填+对账时点标注=本步清偿义务（⑤-3 收口）**：回填两行状态 ⚠️→✅（Unity 侧镜像公式已完成）；对账时点标注落实=「Unity 侧两分支（AIEconomySettlement/AbstractEconomySettler）统一日结粒度（1 日），王国脑花昨日结存（HH.24 裁决①）两分支一致；sim 瞬时入账 vs Unity 1 日滞后差异保留为已知差异，阶段 B（sim 多王国化）由 sim 侧对齐或登记允许差异」。
  - **新增公式同构义务（文档级）**：15 账本追加「同构公式对照表」（Unity AbstractEconomySettler ↔ sim SimEconomy：采集产出/每日耗粮/税收/生育/流失 公式条目对照），供阶段 B 与 champion 回灌对账——**分级=T 级文档义务**（无 sim 行为改动、无可跑门禁对象）；策划若判存疑可升 F（届时在 sim 侧建对拍卡跑 baseline/holdout/determinism，但本步无 sim 改动，无可比基准）。
  - **15 账本 L49 前向引用失真修正**：原文「训练侧 harness/Economy/AbstractEconomySettler」→ 实际=「SimEconomy.cs（QQQ.5 私有副本）」。**此为对训练仓 15 账本的措辞修正，需策划端批准**（15 账本在训练仓内，执行端不直改训练仓——报策划端或按裁决执行）。
- **影响面**：零训练仓改动；15 账本回填（执行端可改——15 账本虽是训练仓文件，但 sim-sync §五「Unity 侧适配完成→回填对账表状态」授权执行端回填状态列，措辞修正另报策划）。

---

## 四、行为级验收探针方案（实施后跑，冒烟 Smoke_14 承载）

| # | 探针 | 断言 | 映射 |
|---|------|------|------|
| P1 | **抽象不僵死+数值合理**：构造 AI 王国（含产能建筑+工人+国库）→ 切 Abstract（出视野 2 日）→ 数日观察 | AbstractEconomySettler 日结算产出/消耗进入合理区间（国库按镜像公式增长，工人休眠但公式产粮）；断粮日扣均饱食/触发流失项；**与 Fine 态切换前后国库无跳变**（D335 差值对账） | 冒烟 #9 全量部分 + 任务书探针 1 |
| P2 | **玩家零回归**：玩家(id=0) 不进 AbstractEconomySettler 路径 | AbstractEconomySettler 对 id=0 零调用/零副作用（负探针：玩家国库=玩家物流链路值，不被公式改写） | 任务书探针 2 / 冒烟 #11 |
| P3 | **同 seed 确定性**：抽象结算段固定排序（⑤-3 硬性 a 同款纪律） | 双轮（seed 固定）抽象结算逐字节一致；建筑遍历/队列序固定 | 冒烟 #12 + 任务书探针 3 |
| P4 | **P0 基线全绿** | A3 逐字节 / b=2684 / A4 零回归（同 HH.43 基线口径） | 任务书探针 4 |
| P5 | **D453 进食统一**：Fine 王国 AI 实体进食扣 AI 国库 Food+饱食恢复；Abstract 王国计数进食（国库 Food 减、avgSatiety 桶更新）；**唤醒拉平无跳变** | AI 国库 Food 因进食下降/实体饱食上升；Abstract 段 avgSatiety 桶被公式更新；切回 Fine 首次结算实体饱食=抽象均值 | D453/D335 行为级 |
| P6 | **种子② 退役零残留**：全库 grep Academy/Workshop/Module_Science/ResearchProject/ResearchCompleted 零命中（文档作废注/历史语境例外清单化）；编译 0error；无空引用；研究 UI 无入口 | grep 零残留 + 编译 0error + 行为级无 NRE | Q3 验收 |

---

## 五、诚实分层

1. **静态勘察为本，行为级待实施后跑**：§一/§三 全部为源码实读+文档核读结论（file:line 标注），未动任何产品代码；§四 探针在裁决后实施并跑，行为级证据届时随交付报告附 Console 实盘。
2. **发现③点（Workshop/存档数组）为勘察新增**：D401 原文「六模块收敛为五模块」隐含 Workshop 同退役，但 2_12 退役登记只列 Academy——本报告如实申报，待策划裁决确认深度（Q3）。
3. **15 账本前向引用失真（发现①）**：账本 L49 所称训练侧文件不存在，实为 SimEconomy——本报告如实申报，不擅自改训练仓文件，措辞修正报策划端批准（Q5）。
4. **本串 commit 构成声明**：HH.44 报告 + _交接索引登记 + 工作日志插行 + 2_17 实施计划状态行推进（清欠账），**零产品代码改动**；设计文档/0.6/2_20 文档只读未动（Academy 文档注等要改的写进 Q3 待裁）。

---

## 六、下一步建议

1. **策划端裁决五问**（Gate）→ 回写本报告「策划裁决」节。
2. 裁决后按 §2.2 三批实施：批A 公式引擎+分叉 → 批B Satiety 统一 → 批C Academy 退役+Smoke_14；每批独立提交（对齐步骤13 三批制）。
3. **建议策划端顺手**：确认 Q5 的 15 账本措辞修正（SimEconomy 指正）+ Q3 Workshop 深度；Academy 文档注收尾（2_12 设计五处）按裁决由策划端 L1 或随批C 处理。
4. 训练仓：本批不动；15 账本回填由执行端按 sim-sync §五 授权做（状态列），措辞修正等裁决。

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 1 AbstractEconomySettler 与 2b 关系 | | |
| 2 D453 Satiety 统一口径 | | |
| 3 种子② Academy 退役深度 | | |
| 4 2_20 经济修正挂载预留 | | |
| 5 sim-sync 义务判定+15 账本修正 | | |

### 分歧裁决记录
- 执行端意见：{..} · 策划端意见：{..}
- 裁决：{..} · 依据：{..}

### 衍生产物
- 新建设计文档：{..}
- 新建清单任务：{..}
