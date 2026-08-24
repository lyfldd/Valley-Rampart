# HH.22 2_14 步骤8/10 波次与传送门调度架构 · 待裁决备忘录

> 类型：待决策 → 已裁决（用户质询"问1没有拍板吗"触发复核：三问文档证据闭环，策划端收口）
> 状态：✅已裁决（2026-08-24 复核收口，裁决表见末节；用户保留最终推翻权）
> 日期：2026-08-24 · 发起端：执行端 · 策划端核验附注已并入 · 关联：2_14 实施计划步骤8/10 / D258/D261 / 0.6 §审查决策记录

---

## 一、现状（已实盘核验，三文件已读）

### 1.1 双轨并行——核心问题

当前**两套灾害触发判定同时存在**，这是本 HH 的真正病灶：

| | 旧轨（活跃，2_8 遗产） | 新轨（死线，2_14 步骤4 产物） |
|---|---|---|
| 判定者 | `WaveDirector.ShouldTriggerDisasterThisNight()`（:32-58） | `PortalDisasterTrigger`（步骤4 新建） |
| 触发规则 | disasterEveryNDays 保底 + disasterGuaranteeNDays 强制 + 概率递增 | minDaysBeforeFirst 发展期 + triggerProbability × 难度系数 + forceTriggerAfterDays 保底 + maxPortalPerNight 上限 |
| 参数源 | WaveConfig / KingdomConfig | PortalDisasterConfig（SO） |
| 出口 | `SpawnDisaster()` 直出占位怪（Undead_Warrior） | 发布 `PortalDisasterTriggeredEvent` → **零订阅者** |
| 入口 | `DayCycleSettlement.OnPhaseChanged`（:36-37，活的） | 无（死线） |

**不收拢单轨的后果**：同夜双重判定、参数两套口径、夜袭节奏不可控、存档字段两份。

### 1.2 各环节缺口

| 环节 | 现状 | 缺口 |
|---|---|---|
| 触发判定 | PortalDisasterTrigger 做概率/保底/发展期/上限判定，发布事件 | 事件无订阅者；与旧轨判定并存 |
| 波次调度 | WaveDirector 每晚按旧轨直出占位怪 | 未接 2_14 怪物/传送门；"正常夜晚无波次"（步骤10）未落地 |
| 波次参数 | WaveConfig 有 baseWaves/wavePerDifficulty/strength/方向聚合/错峰 | 缺 6:3:1 配比字段（D258） |
| 传送门生成 | 无管理器（PortalDisasterManager 未建） | 仅冒烟脚手架 StartPortalSmokeTest |
| 确定性 | WaveDirector 出怪用 **UnityEngine.Random**（:103 波间/组间） | PortalDisasterTrigger 已全程 System.Random(seed)（R4 纪律）——**并入时必须统一种子化，否则步骤12 sim 对拍锁死** |

---

## 二、计划书已定案（不可现场重选的部分）

- **D261（0.6 决策记录）**：波次并入 WaveDirector 内部方法 `SpawnPortalDisasterWaves()`，**本篇不新建调度类**——注意这是定案，不是"倾向"
- **步骤10 标题**：与 WaveDirector 联动修改——旧"每夜流程"改为"灾害触发时流程"，**正常夜晚无波次**
- **D258**：波次怪物配比 近战:远程:精英 = 6:3:1（占位，SO 可配）
- **D97**：波次数 = baseWaves(2) + 难度×wavePerDifficulty(1) → Easy 3 / Normal 4 / Hard 5
- **so-data-driven 铁律**：数值入 SO，禁硬编码

---

## 三、待裁决三问（附策划端倾向，非终裁）

> ⚠️ 本节为咨询稿原貌保留；**终裁见末节**——复核结论：三问全部有定案依据（§二所列即证据），倾向已转正，无需外咨询即可开工。

### 问1：职责边界——谁生成传送门 / 判定权归谁

> **实质**：不只是"谁生成传送门"，而是**双概率源收拢**——判定权必须归一家。

| 方案 | 内容 | 代价 |
|---|---|---|
| **单轨收拢（策划端倾向）** | 判定权=PortalDisasterTrigger（R4 确定性已达标，含发展期/上限等 2_14 全量规则）；生成+波次=WaveDirector.SpawnPortalDisasterWaves（D261 定案）；链路=事件桥接（Trigger 发事件→WaveDirector 订阅）；旧 ShouldTriggerDisasterThisNight 判定+入口退役 | ① WaveDirector 换种子化随机（本来就该换）；② 正常夜晚不再有敌袭=玩家可感知节奏变更（**这是本问唯一需要人拍板的点**） |
| 双轨保留 | 旧轨照跑 + 新轨补订阅 | 同夜双判定/参数两套/存档两份——不可接受的长期债 |

**需要拍板的核心**：确认"正常夜晚无波次、仅灾害夜有传送门出怪"的节奏变化（玩家可感知）。

### 问2：6:3:1 配比落法

| 方案 | 判定 |
|---|---|
| **WaveConfig 加 waveCompositionRatio（策划端倾向）** | ✅ SO 铁律正解 |
| 硬编码 | ❌ 直接违 so-data-driven 红线 |

**此问实无悬念**——若咨询意见推翻 SO 化，需走决策修订流程（0.6 追记），非执行端现场改。

### 问3：本批范围——步骤8+10 同批 vs 只做8

| 方案 | 分析 |
|---|---|
| **同批（策划端倾向）** | 步骤10 本身就是"WaveDirector 联动修改"——分开做=步骤8 交付时新旧轨并行出第三轨，且验收项"正常夜晚无波次"属步骤10，结构性不可拆 |
| 只做8 | 交付形态=三轨并存，无独立验收意义 |

**此问属计划书结构**，咨询若推翻同批需给替代拆法并走决策修订。

---

## 四、策划端核验附注（转述时必带的三修正）

1. **"计划书倾向前者"→已定案**：D261 是决策记录原文，不是待选倾向；推翻需走修订流程
2. **双概率源**：Q1 的真正赌注是"判定权归谁"（1.1 表），不只是生成职责
3. **确定性冲突**：WaveDirector 用 UnityEngine.Random vs Trigger 用 System.Random(seed)——并入必须统一种子化，否则 2_14 步骤12（怪物入训 sim 对拍）从根上锁死

---

## 五、裁决后开工序（预定，供参考）

1. 单轨收拢：Trigger 判定 → 事件 → WaveDirector.SpawnPortalDisasterWaves（含传送门生成）
2. WaveDirector 随机源种子化（R4，world seed 派生）
3. WaveConfig.waveCompositionRatio SO 化（6:3:1 占位）
4. 旧轨退役：ShouldTriggerDisasterThisNight 判定 + DayCycleSettlement 入口改接事件
5. Play 冒烟：正常夜×2 无波次 → 灾害夜传送门+波次 6:3:1 → 段①四态复验
6. 步骤9（强度曲线）/11（昼夜切换）随后

---

## 策划裁决（2026-08-24 复核收口）

> 触发：用户质询"问1没有拍板吗"。复核结论：三问全部有定案依据（§二所列计划书/决策记录原文 + 08-21 Q1 数值裁决），非真分叉；初稿标"待裁决"系过度保守。现收口如下，用户保留最终推翻权。

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 1 判定权与生成职责 | **单轨收拢**：判定权=PortalDisasterTrigger（步骤4 定稿载体，唯一概率源，R4 确定性达标）；波次=WaveDirector.SpawnPortalDisasterWaves（D261 定案）；传送门生成归 2_14 侧生成订阅（步骤4 事件注释"步骤5 传送门生成订阅" + 步骤13 PortalDisasterManager 放置）；旧轨退役=ShouldTriggerDisasterThisNight 判定 + DayCycleSettlement 直连入口 + Undead 占位波 + Portal 临时召唤循环收编 | 三层各有定案原文（见§二/§三）；"既有敌袭"=占位怪待替换，正常夜无波次是步骤10 排定变更，非风险待确认 |
| 1-附 参数收拢（现裁） | **结构以 §2.1 为准**（minDaysBeforeFirst / 概率×D237 / 保底 / maxPortalPerNight 上限）；**数值以 08-21 Q1 裁决迁移：Base=0.2、保底 3 天**（forceTriggerAfterDays 占位 7→3）；WaveConfig 旧概率参数随旧轨退役；验收线=Easy 无独立分支、表面节奏靠保底 | 08-21 裁决=数值口径终案，§2.1=结构终案，两者正交可合并 |
| 2 配比落法 | **SO 化：WaveConfig.waveCompositionRatio**（D261"复用 WaveConfig 参数"既定路径） | D258"占位"+ so-data-driven 铁律，无第二选项 |
| 3 本批范围 | **步骤8+10 同批** | 步骤8 无 10 = §1.1 双轨病本身；"正常夜晚无波次"验收项属步骤10，结构性不可拆 |

**开工序**：按 §五执行（单轨收拢 → WaveDirector 随机源种子化 → 配比 SO 化 → 旧轨退役 → Play 冒烟含段①四态+段②精英复验 → 步骤9/11）。

> 附注更正：初稿"咨询期间段② 停工待命"为陈旧信息——段② 已收官（HH.21）；本裁决后步骤8+10 即可开工。若已约外咨询，带本更新版去亦无妨，但策划端结论：证据闭环，无需等待。
