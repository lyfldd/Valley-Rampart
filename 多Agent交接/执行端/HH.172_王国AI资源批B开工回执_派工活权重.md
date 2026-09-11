# HH.172 王国AI P0 资源批B 开工回执（派工活权重 R-B1/R-B2/R-B3）

> 日期：2026-09-11 · 持有端：执行端（TraeCode·Unity 轨）
> 上游：D634（0.6 §一百六十三）放行 · D633（§一百六十二）批B 解锁
> 任务真源＝`改造计划/2_23_AI王国脑总纲_资源P0实施清单.md` §二（R-B1/R-B2/R-B3，L41-45）
> 设计稿＝`2_23_AI王国脑总纲_资源第一性架构.md` §2.3（L115-126）／§2.2（L102-113）／§七 SO 清单（L232）／§八 S-B4/S-B5（L247-248）
> 完成报告＝**HH.173**（读水位线后同笔登记）

## 一、编号报备

| 编号 | 用途 | 状态 |
|------|------|------|
| HH.172 | 本开工回执 | 🟡 占用（D634 取号令；原拟 HH.171 撞训练端策划占用 ⇒ 顺延） |
| HH.173 | 批B 完成报告 | 🔵 预留（落盘前读账本实时水位线复核） |
| 水位线直读 | D＝**D635**（余额已至 D635）／HH＝**HH.172**（在途最大） | 2026-09-11 直读 |

## 二、🗂 本会话单写者声明（agent-handoff 并发写纪律）

本会话**只**写以下文件；其余文件一律不动（防并行会话覆盖）：

| # | 文件 | 动作 |
|---|------|------|
| 1 | `Assets/_Game/Data/Kingdoms/ResourceBiasConfig.cs` | 新建（R-B2） |
| 2 | `Assets/_Game/Resources/Config/ResourceBiasConfig.asset`（+`.meta`） | 新建（R-B2，经 AssetDatabase） |
| 3 | `Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs` | 改（R-B1 排序键 + R-B3 注记） |
| 4 | `Assets/Editor/Smoke/Smoke_2_23RB.cs`（+`.meta`） | 新建（行为级探针容器，Editor-only） |
| 5 | `多Agent交接/执行端/HH.172_*.md` | 本回执 |
| 6 | `多Agent交接/执行端/HH.173_*.md` | 完成报告（收尾新建） |
| 7 | `多Agent交接/_编号登记.md`·`_交接索引.md`·`河谷防线_开发计划书.md` | 增量回写（禁整文件覆盖） |

**不改**：`AI.Core/**`（红线）、`TaskPriorityConfig.cs/.asset`（死表本体）、`EconomyDiagnosis.cs`/`SituationSnapshot.cs`（批A 已交付，本批只读其数据）、`UtilityScorer.cs`、玩家侧任何文件。

## 三、批B 范围（清单 §二照录，验收标准不改）

| 编号 | 任务（清单原文摘要） | 验收标准（清单原文） |
|------|--------------------|--------------------|
| **R-B1** | TaskScheduler 排序键升级：新排序＝类型优先级（死表保留，S 级修复保命不动）× 资源偏向活权重（诊断层缺口信号）→ 距离；同 seed 同诊断信号→同排序（纯函数非随机） | 排序键三段式在场（死表×偏向→距离）；缺石世界采石任务有效优先级↑（行为级探针 E-B5）；同 seed 排序确定 |
| **R-B2** | 新建 `ResourceBiasConfig` SO：per 资源偏向权重，出厂 1.0 占位；结构按 D519 模式预留 champion 所有权（factor_registry 草案登记，本批不进训练） | SO 可载出厂 1.0；factor_registry 草案登记行在场（S-B5） |
| **R-B3** | 边界注记：活权重住 TaskScheduler（Unity 侧执行层）**不进 AI.Core**（D525 打分器同型）——15_账本登记「Unity 单侧消费」双形态注记（S-B4） | 15_账本 S-B4 注记行在场 |

## 四、三处口径（D634 逐字执行）

① **缺口信号** ＝ `EconomyBlock.Flow.Net(r)` **负值**（`Flow.Net(r) = In(r) − Out(r)`，r∈五元金/石/木/粮/铁）
② **偏向公式** ＝ 线性 `weight = clamp(bias[r] × (1 + k×缺口率), min, max)`；**S 级修复不受偏向影响**（保命硬红线）
③ **任务→资源映射** ＝ `ResourceBiasConfig` 内**可配映射表（SO）**——遵 so-data-driven 铁律，**禁硬编码**

## 五、🔶 口径补全声明（D634 未逐字给出、本批按最小可执行口径落地；**非改裁决**，列入完成报告 §列报待策划端过目/可调 SO）

| # | 补全项 | 本批落地口径 | 依据/理由 |
|---|--------|-------------|-----------|
| 补-1 | **缺口率定义**（公式中的「缺口率」D634 未给算式） | `缺口率(r) = clamp01( max(0, −Net(r)) / max(1, Out(r)) )`＝「支出未被收入覆盖的比例」；`Out=0` ⇒ 0 | 与①「净流量负值」同源（Net<0 才 >0）、天然落 [0,1]、纯函数无随机；`In=0 且 Out>0` ⇒ 1.0（全额缺口）符合语义 |
| 补-2 | **k / min / max 出厂值** | `k=1.0`、`min=1.0`、`max=1.25`（**出厂保守占位**，只接结构不调值） | 项目惯例（so-data-driven：出厂占位、实值归调优轮）；`max=1.25 < 4/3` ⇒ **不跨档**（A×1.25=3.75 < S=4；B×1.25=2.5 < A=3；C×1.25=1.25 < B=2）⇒ 兑现红线「死表 S/A/B/C 语义保留不动」 |
| 补-3 | **S 保命的结构性保证** | ①`Repair`(死表 S) **不乘权重**（有效优先级＝原值）；②非 S 有效优先级**硬上界 = S 值 − ε**（防配置越界架空死表 S） | D634「S 级修复不受偏向影响（保命硬红线）」＝结构性兑现，不依赖配置恰当 |
| 补-4 | **跨档与否** | 默认**不跨档**（见补-2 的 max 取值）；若策划端要求跨档，调 `maxWeight` 即可（SO 数据驱动，零代码改） | 红线「死表语义保留不动」的最严格读法＝默认安全侧 |
| 补-5 | **同 seed 确定性加固** | 排序比较器补**确定性次级键**（源坐标 y→x→任务类型）＝严格全序；修复既有 `List.Sort` 不稳 + `_sources` 为 `HashSet`（枚举序不保证）导致的同位任务潜在乱序 | 门禁「同 seed 排序确定」需**结构性**保证，不能靠输入序巧合 |
| 补-6 | **R-B3 跨仓边界** | `15_账本`「Unity 单侧消费」S-B4 注记**属训练仓**（独立 git）＋`factor_registry` 为 AGENTS.md 禁改域 ⇒ **执行端不代提**，由训练师随下笔提交（同 HH.159 件流程/D634 裁定） | D634 明文；执行端本仓只落 SO＋代码注记 |

## 六、锚点直读状态（施工前）

- **已实读**：`TaskScheduler.cs` L1-104（单例/配置字段/Awake）+ L205-288（Tick ①②③④⑤ 全段）+ L930-990（`RemainingSlots`/`GetPriority`/`SourceKingdom`/`GetTaskDuration`）；`TaskPriorityConfig.cs` 全文（`Get` 兜底 B）；`TaskPriorityConfig.asset`（entries 8 行，taskType 7/8/9 未配 ⇒ 兜底 B）；`StimulusTypes.cs` L19-25（`TaskPriority` S=4/A=3/B=2/C=1，**AI.Core 域只读**）；`EconomyDiagnosis.cs` L42-86（`EcoResource`/`ResourceFlow.Net`）+ L146-197（`EconomyBlock`）；`SituationSnapshot.cs` L102-107（`Economy`）/L120-129（`SituationHub.TryGet`）；`Building.cs` L910-990（`TryAdvertiseTask` 四类任务源）；`TreeGatherSource.cs` L36-51；`KingdomTask.cs` 全文（`GatherTaskArgs/ScaleTaskArgs`）；`BuildingPlacementConfig.cs`（D519/D525 模式先例）；`TestHarnessApi.cs`（正门）；`Smoke_2_22P0.cs`（探针容器范式/`Finish`/`WaitDays`）。
- **待施工前逐个复核**：无（上表已覆盖本批全部触达面）。
- **程序集**：`Assets` 下唯一 asmdef＝`AI.Core.asmdef`；`TaskScheduler`/`SituationHub`/`ResourceBiasConfig` 均属 `Assembly-CSharp` ⇒ **无跨程序集障碍**（D634 独立复核已确认）。

## 七、红线自查（逐条）

| 红线 | 本批兑现方式 |
|------|-------------|
| AI.Core 零直改 | 只**读** `TaskPriority` 枚举；`git diff` 对 `AI.Core/**` 必为空（收尾验） |
| S 级修复不受偏向影响 | 见补-3（双保险：不乘权重 + 硬上界） |
| 玩家侧零改动 | 偏向只作用于 AI 国（`kingdomId>0` 且快照存在）；玩家源（kingdomId=0）无快照 ⇒ 权重恒 1.0 ⇒ 零行为差异 |
| 同 seed 确定性 | 见补-5（全序比较器）+ 纯函数（无随机/无时间源） |
| 死表 S/A/B/C 语义保留不动 | `TaskPriorityConfig.cs/.asset` **零改动**；新排序＝死表值 × 偏向层（乘子默认≥1.0 且不跨档） |

## 八、门禁（缺一不可交付）

1. 编译 **0 警 0 错**（基线＝当前存量告警 15 条，新增须 0）
2. **行为级探针正/负双侧**：缺石世界采石任务有效优先级↑（E-B5 正）＋ S 保命/出厂零差异（负）＋ 同 seed 排序确定
3. **既有冒烟零退化**：`Smoke_2_22P0` run1 复跑（基线＝PASS 32 / FAIL 0）
4. `git diff HEAD` 全量 + 逐项在场性 grep 双自查
5. 排雷自查回报：L-01／L-02／L-12／L-17／L-20／L-21

## 九、停止条件（遇之停手＋开 HH 报策划）

需要改设计文档／与既有裁决或设计稿冲突／探针 PASS-FAIL 判据本身需裁 —— 三者任一即停。

---

执行端开工。下一步＝R-B2（SO）→ R-B1（排序键）→ R-B3（注记）→ 探针容器 → 门禁 → HH.173。
