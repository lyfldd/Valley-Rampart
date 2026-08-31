# HH.42 · 2_17 步骤13（SimModeManager）设计报告（Gate 前置，待策划裁决）

> 类型：策划报告请求（Gate 前置）
> 状态：⏳待裁决
> 日期：2026-08-31 · 发起端：执行端 · 关联：`2_17_AI王国脑与自主成长.md` §3.3（D332~D335/D344）· `2_17_AI王国脑与自主成长_实施计划.md` 步骤13 · HH.30 §6.5 · HH.41（Q9 阶段 P 验收）
> 前置：Q9 阶段 P 已验收（HH.41 ✅），13 解除阻塞；主仓 @043455c+a684b94（领先 origin 2 commit）工作树干净

## 〇、锚点声明

- **指令源**：任务书 13（2_17 步骤13 SimMode，当前队列下一任务）。勘察以重读代码为准（file:line 标注，非凭记忆）。
- **开工方式判断：Gate 前置**——勘察发现 ≥4 个需策划裁决的决策点（任务书点名 Satiety 归属"勘察后报策划端定"），按先例步骤8/12：先报设计报告过 Gate 再动代码。
- **已执行（不依赖 Gate）**：Q9 两探针补跑完成，证据见 §二（销挂账行，队列勿自行改）。
- **未动代码**：本报告纯勘察+方案，未改任何产品代码。

---

## 一、恢复与勘察结论

### 1.1 恢复状态
- HH.41 ✅ 已验收（2026-08-31 策划端实盘复核，Q9 阶段 P 完工，13 解除阻塞）。
- ⚠️ **索引/队列滞后发现（报策划端补登，执行端不改）**：`_交接索引.md` 未登记 HH.41（停在 HH.40，且 HH.40 出现两行重复）；`_任务队列.md` 当前队列仍停在 Q1 批C 旧态、Q9→✅ 与 13 解除阻塞未反映。

### 1.2 步骤13 现状（勘察证据）
| 项 | 现状 | 证据 |
|----|------|------|
| SimModeManager | P0 恒 Fine 占位：`GetMode(int)=>SimMode.Fine`；枚举 `{Fine=0,Abstract=1}` 已有 | `Systems/AI/KingdomBrain/SimModeManager.cs` L12-25 |
| DayCycleSettlement 五步① | SimMode 判定注释占位（恒 Fine），②TickKingdomBrains 已接 | `DayCycleSettlement.cs` L36-38/L94-109 |
| KingdomBrain.Tick | 已按 `GetMode != Fine → return` 短路（Abstract 不 tick 骨架已就位） | `KingdomBrain.cs` L76-77 |
| KingdomState.simMode | 字段已有（王国脑运行时态，注释声明"不入档"） | `KingdomState.cs` L65-66 |
| LODSystem 活跃带 | `_activeBandSet` 私有、仅 `ActiveBandCount` 公开；**无 `IsActivelyCovered(mid)` 查询（残核关账缺口→本步补）** | `LODSystem.cs` L41-50/L373-401；实施计划追记二 L40 |
| SimModeConfig SO | **不存在**（实施计划 §三 字段表：offscreenDaysToAbstract=2 / combatHotspotForceFine=true） | Glob 无 SimModeConfig.cs |
| NPCBrain 休眠钩 | 无 per-kingdom 门（仅 LOD 频率驱动 Think/感知）；`_controller.kingdomId` 可访问 | `NPCBrain.cs` L385-445 |

### 1.3 到期债现状（勘察证据）
| 债 | 现状 | 证据 |
|----|------|------|
| **Siege 完整 AI 生产链**（HH.30 §6.5，步骤13 顺裁） | `ProduceMachine(type,pos,kingdomId)` AI 分支返回 false + 日志占位；per-kingdom 计数 `GetPlacedMachineCountByKingdom` 已落 | `SiegeProductionSystem.cs` L89-139 |
| **Satiety AI 国库进食**（HH.30 §6.5，13/14 二选一） | `OnNewDay` 只处理玩家（kingdomId==0），AI 完全不参与饱食/进食（AI 饱食恒不变）；分桶均值已落（步骤11 批2） | `SatietySystem.cs` L117-147 |
| **2_4③ MidChunkLodState 回收**（审查清单缺口径） | `_midStates` 惰性登记只增不减（仅 OnMapGenerated 清空）；中区块总量受地图硬上限 | `LODSystem.cs` L29/L74-84/L316-322；`文档设计缺陷核查_汇总总清单` L33③/L292 |

---

## 二、Q9 前置探针补跑（行为级证据，销挂账）

> 挂账来源：HH.41 §5.1 让渡 2「Unity Play 行为探针未实跑」。本次 Unity 会话补跑，**两探针 ALL PASS**。请策划端据此销挂账行（队列勿自行改）。

### 2.1 探针① 传送门出 Monster 正/负（走正式链路，Small 128×128 seed=777）
- **链路**：`LoadManager.InitializeNewGame` 建真实世界 → 发布 `PortalDisasterTriggeredEvent(3, Vector2.zero)` → WaveDirector 订阅→建门+6:3:1 波次（`WaveDirector.cs` L56-98）→ MonsterSpawner 出怪。
- **证据（Console 实盘）**：
  - `[WaveDirector] 灾害发生，新建传送门@(-53.94, -33.99)，第3天。`
  - `[UnitRegistry] 注册: Monster_Monster，当前共 9 个单位` + `[UnitController] 初始化: Monster_Monster (HP: 71/71, ATK: 7, DEF: 0)`
  - `[Q9探针①] 传送门阵营Monster=OK 出怪Monster数=1 (OK) 负探针[全场景Undead单位数]=0 (OK) 阵营分布[f1=8 f4=1 ]`
  - `[Q9探针①] ===== ALL PASS（传送门Monster正/负探针）=====`
- **判定**：传送门 `GetFaction()==Faction.Monster` ✅；出怪 `(int)Data.faction==(int)Faction.Monster`（f4）✅；建图后/出怪后全场景 Undead（f2）0 命中 ✅（P5.1 运行时实证）。

### 2.2 探针② 含 Undead 旧档读档过滤（真全阶段 SaveManager.Save→Load 链路）
- **构造**：SaveManager.Save（v3）→ 文件注入 `UnitSaveData{faction=2(Undead), occupation=Warrior}` 条目（saveId=`Unit_q9probe_undead_1`，phase=Scene）→ `saveVersion=2` 降级 → Load。
- **证据（Console 实盘）**：
  - `[Q9探针②] save=True (v3)` / `文件注入完成 saveVersion=2 模块数=740 Scene数=728`
  - `[UnitFactory] 旧档单位 Undead_Warrior 查表失败过滤（P5.3 D432），丢弃第 1 个（saveId=Unit_q9probe_undead_1）。`
  - `[Q9探针②] load=OK FilteredSaveUnitCount=1 (OK) 读档后Undead单位数=0 (OK) 读档后单位总数=9`
  - `[Q9探针②] ===== ALL PASS（含Undead旧档读档过滤 FilteredSaveUnitCount 在场）=====`
- **判定**：读档不炸 ✅；查表失败→丢弃+`FilteredSaveUnitCount` 递增=1 ✅；计数日志在场 ✅；读档后 0 Undead 实体 ✅（P5.3 运行时实证）。注：`GetData` 未命中会打一条 `[UnitDataManager] 找不到数据: [Undead_Warrior]` LogError，属预期（过滤器捕获），非读档失败。

---

## 三、步骤13 技术方案草案（供策划端审，裁决后细化实施）

### 3.1 范围界定（与步骤14 的边界）
- **步骤13 = 切换基建**：SimMode 判定（D333/D344）+ 休眠/唤醒（D334）+ LODSystem 查询 + SimModeConfig SO +（存档口径裁决）。
- **步骤14 = 抽象结算+对账**：AbstractEconomySettler（纯 C# 公式同构 D336）+ 差值补删/队列续接（D335）。**步骤13 中 Abstract 王国=冻结态（无脑/无工人活动/无经济结算），步骤14 才补抽象经济**——故冒烟 #9/#10 的"账本无差"部分归步骤14 验收，步骤13 只验切换机制与零回归。

### 3.2 实施草案（三批）
- **批 A·判定地基**：LODSystem 补 `IsActivelyCovered(Vector2Int mid)`（查 `_activeBandSet`）+ 战斗热点中区块查询（供战斗锁）；新建 `SimModeConfig` SO（offscreenDaysToAbstract=2 / combatHotspotForceFine=true）+ asset；SimModeManager 落地真实判定——逐 AI 王国：领土中区块 ∩ 活跃带 → Fine（立即）；领土内战斗热点 → Fine（强制）；连续 N 日未覆盖 → Abstract（迟滞）；否则维持。DayCycleSettlement 五步①接线真实判定。
- **批 B·休眠/唤醒（D334）**：NPCBrain 对 Abstract 王国非军事单位（Worker/Porter/Civilian）停 Think + 停寻路/移动（实体常驻不销毁，原地冻结）；**军事单位（Warrior 等）不受影响（D281 军队永远真实体）**；唤醒即续（位置/任务/进度保留）。
- **批 C·债清偿**：按 §四 裁决后落地（Siege 生产链 / Satiety 进食 / MidChunkLodState 回收 / 存档 SimMode）+ 新冒烟 `Valley2_17_Smoke_13`（覆盖冒烟 #17 缩放不变 Fine 集 / 切换迟滞 / 战斗锁 / 军事不冻结 / 零回归）。

### 3.3 冒烟验收映射（实施计划步骤13 验收）
- 冒烟 #17：缩放全图 → Fine 集合不变（D344：视野=LOD 活跃带，天然不随缩放变——验证 SimModeManager 读 LODSystem 而非相机）。
- 冒烟 #9 切换机制：#13 部分——出视野 2 日切 Abstract / 入视野立即 Fine / 反复切 10 次无异常（账本无差全量部分待步骤14 对账）。
- 冒烟 #10 战斗锁：Abstract 王国领土内出现战斗热点 → 立即切 Fine。

---

## 四、待裁决决策点（每项：选项 + 推荐 + 影响）

### 决策点 1：Satiety AI 国库进食归属（13/14 二选一，任务书点名报策划定）
- 现状：AI 单位完全不参与饱食/进食（`SatietySystem.OnNewDay` 只处理 kingdomId==0）；AI 饱食恒不变。
- 选项：
  - **A（推荐）归步骤14 AbstractEconomySettler 统一落地**：AI 进食语义与抽象结算/对账同批实现——Fine 王国实体工人进食扣 `KingdomState.resources.Food`，Abstract 王国按计数公式进食（D336 同构）；一次统一"AI 民生闭环"，避免步骤13 单独动 Satiety 民生系统造成回归面。
  - B：归步骤13 单独落地——步骤13 就让 AI Fine 王国实体工人进食扣 AI 国库（保 Fine 王国经济即时正确）；但步骤14 Abstract 侧仍需另做计数进食，语义拆两处。
- 推荐 **A**：Satiety 进食本质是"AI 经济闭环"内容，与步骤14 AbstractEconomySettler 同域（HH.30 §6.5 原文"步骤13/14 AbstractEconomySettler 一并裁"）；且步骤13 核心是切换机制，把民生内容并批避免两处割裂。影响：Fine 王国 AI 工人在步骤14 前仍不进食（现状延续），无回归。
- 影响：选 A → 本步 Satiety 仅保持现状（分桶均值已在），债挂账延至步骤14；选 B → 本步扩展 Satiety 改造面。

### 决策点 2：Siege 完整 AI 生产链（HH.30 §6.5，任务书"随 13 清偿"）
- 现状：`ProduceMachine(type,pos,kingdomId)` AI 分支 false 占位；per-kingdom 上限计数已落。
- 问题：AI 无战争机器 UtilityAction（15 项行动无此候选），AI 也不建 SiegeWorkshop（不在 ①~⑥ 建造集）——"完整 AI 生产链"触发方不明确。
- 选项：
  - **A（推荐）能力打通 + 触发延后军事期**：ProduceMachine 的 kingdomId 分支真正实现（AI 国库扣费 + per-kingdom 上限 + 生成带 kingdomId 单位），但**不接触发方**（无 UtilityAction、无脑焦点）——留 2_18 军事期内容一并议触发（与"AI 城堡升级动作恒1级随军事期议"同节奏）。本步只清偿"能力层债务"（当前 false 占位=半退役态）。
  - B：新增 UtilityAction（如 ⑯造战争机器）接脑焦点，本步即让 AI 自生产——但 AI 无 SiegeWorkshop 前置、军事期未到、范围膨胀。
- 推荐 **A**：满足"清偿"（消除 false 占位死代码）+ 不越过军事期内容边界（D320 军事期≈20~30 日才进，2_18 才开战）。影响：最小改动（一个 overload 真实现+冒烟），无新 UtilityAction。
- 影响：选 A → 本步 Siege 债清偿为"能力真实现，触发挂军事期"；选 B → 步骤13 范围显著扩大。

### 决策点 3：2_4③ MidChunkLodState 回收口径（审查清单缺口径，步骤13 联测定口径）
- 现状：`_midStates` 惰性登记只增不减（仅 OnMapGenerated 整体清空）；热度记忆保留。
- 约束：中区块总数受地图规模硬上限（128×64 格 ÷ 4×4 中区块 = 32×16 = 512 中区块；Medium 256×256 = 64×64 = 4096 中区块）——但实际登记只发生在"活跃带附近+有热度"的稀疏子集，远小于上限。
- 选项：
  - **A（推荐）保持现役登记不逐条回收，定口径=「MapGenerated 整体清空即回收」**：中区块稀疏登记有界（≤ 活跃带+历史热点，最终受地图中区块总量硬上限），单次会话内存可忽略；逐条回收（Dormant N 日+零热度移除）收益 < 复杂度风险（丢热度记忆/再登记开销/确定性破坏——移除后热度历史丢失影响战斗热点传播）。
  - B：Dormant 持续 N 日且零热度 → 从 `_midStates` 移除（回收+清热度记忆）。确定性风险：同 seed 行为取决于回收时机（时间相关），可能破坏逐字节一致。
- 推荐 **A**：符合"有界稀疏即够"原则（D80 稀疏登记本意）+ 保护确定性（热度记忆不随时间丢）。影响：本步补一行口径注释（LODSystem 头部或 _midStates 声明处），无代码逻辑改动。
- 影响：选 A → 仅落口径文档化；选 B → 引入回收逻辑+确定性风险，需专项冒烟。

### 决策点 4：存档记录 SimMode（设计 §3.3 与 KingdomState 注释矛盾）
- 矛盾：设计 §3.3 判定块写"存档记录 SimMode"；`KingdomState.cs` L56-57 注释"脑运行时态（scriptPhase/focus/simMode）由王国脑日 tick 写入，**不入档**"。
- 选项：
  - **A（推荐）不入档**：simMode 是每日常规重判的**视图派生态**（D347 五步①每日判定），非需持久化的事实；D334 实体常驻+存档含全部实体，读档默认 Fine 续跑无跳变（实体都在，不涉及"缺实体"对账）；与"王国脑运行时态不入档"既有哲学一致；避免动 KingdomRegistry 存档 schema（kingdoms[] 完整迁移本就归 2_11）。
  - B：KingdomEntryData 加 simMode 字节字段（旧档缺省=Fine），读档即恢复 Abstract——省一次首日重判；但需动存档 schema（与 2_11 迁移时序冲突风险）+ 与"不入档"注释矛盾需一并改。
- 推荐 **A**：读档首日 Fine 再判定，无行为差异（实体常驻保证），零 schema 风险。影响：设计 §3.3 的"存档记录 SimMode"一句需策划端修订或加注（执行端不改设计文档，报策划端处理）。
- 影响：选 A → 不改存档；选 B → 加字段+改设计注+2_11 时序协调。

---

## 五、下一步建议

1. **策划端裁决四点**（Gate）→ 回写本报告"策划裁决"节。
2. 裁决后执行端按 §3.2 三批实施：批A 判定地基 → 批B 休眠唤醒 → 批C 债清偿+新冒烟 `Smoke_13`。
3. **建议策划端顺手**：补登 `_交接索引.md` HH.41（当前缺失+HH.40 重复行）；`_任务队列.md` Q9→✅+13 解除阻塞；销 Q9 探针挂账行（§二 证据）。
4. 训练仓：本批 Unity 侧改动（SimModeManager/LODSystem/NPCBrain/Siege/Satiety）不涉 AI.Core 决策核公式，**预计无 sim-sync 同步义务**；如策划端判 SimModeConfig 或 Satiety 公式与 sim 同构面需登记差距账本，请裁决中指明。

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 1 Satiety 归属（13/14） |  |  |
| 2 Siege 生产链（能力打通 vs 接触发） |  |  |
| 3 MidChunkLodState 回收口径 |  |  |
| 4 存档记录 SimMode |  |  |

### 分歧裁决记录（有分歧时必填）
- 执行端意见：{..} · 策划端意见：{..}
- 裁决：{..} · 依据：{AI 北极星/三支柱/单人规模/兼容风险}

### 衍生产物
- 设计文档修订：{2_17 §3.3 "存档记录 SimMode" 句是否修订/加注}
- 新建清单任务：{2_17 实施计划 步骤13 分批回执}
