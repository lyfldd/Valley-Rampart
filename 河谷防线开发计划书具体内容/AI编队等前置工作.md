# AI 编队等前置工作（临时文档）

> 2026-08-01 审计定稿。用途：把 3.0.1_3（AI 协作）、3.0.1_LOD、母文档 §7.4（对象池）三份今天定稿的设计**落到场景上可见**之前，必须完成的提前工作总表。含：①三份文档对照代码的全面审计结论（虚构项/缺陷/需占位项）；②战斗测试场景改造清单；③将军占位方案；④依赖链修订建议。编队 P0 场景走查通过后本文归档。
>
> 审计范围：`3.0.1_3_AI协作.md`、`3.0.1_LOD性能架构.md`、`3.0.1注意力机制与刺激源.md` §7.4，对照 `Valley Rampart/Assets/_Game` 全部 .cs 与 GameScene.unity / GridConfig.asset。

---

## 一、审计结论（三份文档 vs 代码）

### 1.1 新发现勘误（§十三/§九 两轮核查之外的漏网项）

| # | 位置 | 问题 | 实情（代码证据） | 处理 |
|---|------|------|----------------|------|
| E1 | 3.0.1_3 §5.2 | "与城墙地形因子/allyFactor 同构"——**"城墙地形因子"虚构** | rawFactor 实际仅 5 项：dist/count/hp/ally/time（ThreatAssessment.cs:74-86），无任何地形因子 | 删"城墙地形因子"，同构对象就是 allyFactor（友军保护因子，:83） |
| E2 | 3.0.1_3 §15.2 | "减员 → SafetyScore 降 → 威胁因子相对放大"——**传导链虚构** | SafetyScore 是设计期烘焙静态值（§3.6 零实时评分），运行时没有"降"。真实链：减员 → 成员 allyCount 降 → allyFactor 升 → 个体 rawFactor 升 → 易触发谱系撤退 | 改写真传导链；结论（涌现式撤退）不变，机制仍成立 |
| E3 | 3.0.1_3 §15.5 | "死人到对象池回收 1.5s 时 Reset 清全状态"行文像现成机制 | 对象池是母文档 §7.4 未实施设计；但 ClearFormationState 白嫖的 IMemoryComponent.Reset() **已实装**（IMemoryComponent.cs:39，3 个实现） | 勘误：ClearFormationState 不阻塞于对象池，随编队 P0 直接做；对象池回收联动是后续叠加 |
| E4 | 3.0.1_3 §14.6 vs LOD §3.2 | **因子编号撞车**：enemyTypeFactor 自称"第 6 项"，heatFactor 自称"第八项"，都建立在未落地的 7 项目标态上 | 代码现状 5 项硬编码（ThreatAssessment.cs:89-93）；3.0.1_1 的 enemyPower/selfPower 未落地（LOD §九已勘误） | 合并改造前先出**统一因子表**（§三.4），编号一次定死，两份文档对齐 |
| E5 | 3.0.1_3 §14.4 | "按威胁因子分档选阵型"——**威胁因子是谁的没说** | 选阵型是 FormationController（军队级）决策；rawFactor 是个体级计算（ThreatAssessor.CalculateRawFactor） | 补占位决策：P0 用**将军自身 rawFactor**（将军在阵中最具代表性，零新增计算）；P1 可升级编队聚合（均值/max） |

### 1.2 设计瑕疵（非错误，建议改进）

| # | 问题 | 建议 |
|---|------|------|
| D1 | **LOD P0 与编队 P0 鸡生蛋**：LOD §六 P0-2 要"军队锚点注册表"，但注册方 FormationController 在编队 P0 才出生 | LOD P0 降级为**仅君主中心**先行（RulerController.Instance.MonarchUnit 现成），军队中心留接口；编队 P0 落地时 FormationController 注册进表。两份文档各补一句 |
| D2 | **依赖链把场景体现推迟两环**：原链"对象池 → LOD → 编队 P0"。但编队 §十三 自认 P0 手配单阵型可绕 LOD；E3 证明 ClearFormationState 不依赖对象池；6 人编队测试规模下 GC 尖峰无感 | 修订为：**前置工作（将军占位+场景改造）→ 编队 P0（场景可见）→ 对象池 P0 ∥ LOD P0（并行）→ 编队 P1**。LOD 只阻塞编队 P1（阵型切换/分档权重/补充机制热度判定）；对象池只服务规模压力 |
| D3 | §八 FormationStimulus 全新类型需在 AttentionSystem 加约 4 处 switch 分支；而 FollowStimulus 已存在且 FocusType.Anchor（NewStimuli.cs:50） | 实施时可选项：扩展 FollowStimulus 加 SlotOffset 字段可省 4 处分支。维持新类型（语义更干净）亦可——实施者自定，不强制 |
| D4 | §2.2 "编队成员为友军（Civilian 类）"易被读成"工人职业" | 已验证**实质正确**：UnitCategory.Civilian=友方类别（UnitController.cs:66，Human_Player→Civilian），GridConfig.asset 配 stackLimits: Enemy=5 / Civilian=**0=无上限**。措辞改"UnitCategory.Civilian（友方类别，stackLimit=0 无上限）"即可 |

### 1.3 已验证属实（白嫖清单抽查通过，非虚构）

- IStimulus 接口含 FocusType 属性（StimulusTypes.cs:64,72）；FollowStimulus 锚点型（NewStimuli.cs:50）
- ExecutorAnchorLostEvent（ExecutorEvents.cs:42，NPCBrain.cs:214 发布，无订阅者——FormationController 将是第一个）
- UnitDamagedEvent（GameEvents.cs:131，含 0.5s 节流，LOD §九隐藏坑 1 已标）
- AttentionSystem.AddDynamicStimulus（AttentionSystem.cs:56）；L2 FocusType.Anchor→FollowAnchor 路由（L2PostureDecider.cs:152）
- GridSystem.CoordToWorld（GridSystem.cs:50，地面层 y=-3 贴基线）
- NpcProfessionDef.isRanged（NpcProfessionDef.cs:25）；BehaviorSpectrum 谱系含 FullRetreat（StimulusTypes.cs:37）
- IMemoryComponent.Reset()（IMemoryComponent.cs:39；ThreatHysteresis/ProtectionHysteresis/HitCooldown 三实现）
- Think 10Hz + 5 组分片（NPCBrain.cs:77,164,240；AttentionTuningConfig thinkShardCount=5）
- ThreatAssessor：5 因子硬编码 + `enemyCount==0` 早退（ThreatAssessment.cs:70,89-93）——LOD/编队合并改造的两处前置（权重迁 SO + 删早退）属实
- 君主链路：RulerController + RulerData + Occupation.Ruler 完整；General/Commander 全库零匹配（将军需新建，见 §二）

### 1.4 cellSize 勘误债——结清

**GridConfig.asset 实际 cellSize = 2.26**（Resources/Grid/GridConfig.asset:15）。GridConfig.cs:13 类声明的 32f 只是从未生效的默认值，资产已覆盖。全项目消费方 fallback 2.26f（NPCBrain.cs:489 等 7 处）与资产一致。**结论：文档写 2.26 正确，无坑**；LOD §七"待勘误"项可标已决（以资产 2.26 为准）；母文档 §7.1 与 3.2 文档 GridConfig.cs:13 的 32f 是类默认值误读，建议把类默认值直接改成 2.26f 防再误读（一行改动，可随前置工作顺手做）。

---

## 二、将军占位（复用原占位机制，零新机制）

**结论：完全复用现有占位体系**——Ruler 基础 prefab + Variant 染色 + NPCBrain + NpcProfessionDef SO，与现有 5 个 NPC prefab 同一生产线（CombatSetupTools.CreateNpcPrefabs，CombatSetupTools.cs:154-198）。

| 项 | 内容 | 依据 |
|----|------|------|
| prefab | `Human_Player_General.prefab` = Ruler Variant，染色金/紫（与士兵蓝/弓手青绿区分），挂 NPCBrain + DamageFeedback | 现有 Variant 机制，CombatSetupTools.cs:163-167 |
| 职业 SO | General NpcProfessionDef：courage 95 / 高 HP / 高 attack / attackRange 1 / isRanged false（近战） | 3.0.1_3 §1.1；CombatSetupTools.cs:81-117 同构 |
| 数据注册 | UnitDataManager 查表注册（faction=Human_Player, occupation 需加 General 枚举值） | UnitFactory.cs:101-105 查表路径 |
| 生成路径 | 仍走 UnitFactory.SpawnUnit → Instantiate | UnitFactory.cs:78 |
| 对象池衔接 | 母文档 §7.4 UnitInstancePool **按 prefab 分桶**——将军 prefab 天然独立一桶，池化落地后对调用方透明，**不阻塞将军占位**（先后顺序无关） | 用户所问"细分子对象池"即此设计；对象池未实施，当前 Instantiate 直建 |

---

## 三、前置工作清单（编队 P0 场景可见之前）

### 3.1 战斗测试场景改造（CombatTestSpawner.cs，伤害管线遗留 + AI 大脑已接入）

现状：2 近战 + 1 弓手 + 1 工人（旁观）vs 3 敌（含 1 远程），全部 y=-3 基线，玩家左敌右（CombatTestSpawner.cs:43-60）。**需要改**，清单：

| # | 改造项 | 细节 |
|---|--------|------|
| S1 | 我方改为标准满编 | 1 将军 + 3 近战 + 2 弓手（3.0.1_3 §1.2 满编 6 人 (3,2)）；工人保留旁观 |
| S2 | 敌方分方向刷 | 现 3 敌同侧 → 改为左单线先刷、右侧延迟增援（双线走查 §7.2 用）；跳跳怪无 prefab，用 Undead_Warrior 占位（破阵走查 §7.1 用） |
| S3 | 编组触发 | 生成完成后 FormationController（挂将军）执行招募编组（绕开 ScheduleCenterStub 空壳自管，§十三已决） |
| S4 | 军令下发占位 | P0 无君主 UI（指挥链 §6 的 S 级 TaskStimulus 由谁发？）→ debug 热键或自动演示序列：列队 → 防守交战 → 进攻推进 →（可选）撤退 |
| S5 | 城墙锚点占位 | 场景放 2 个空 Transform（左/右 WallAnchor）作守城编队静态锚点（§14.7）；**不放真城墙**（wall.asset prefab 未绑定 :48、建造系统未接场景）；可选：用 PlaceholderSprites "wall" 灰块摆两段视觉墙（BuildingVisual.cs:33-35 机制现成，成本极低） |

### 3.2 编队 P0 本体（3.0.1_3 §十 P0 全 8 项，无新增）

FormationDef / FormationStimulus（slotOffset）/ FollowAnchor 槽位化 / FormationController（招募·下发·解散·ClearFormationState）/ 手配防守阵型一条 / chaseRange clamp 2 cell / 手配进攻候选一条 + 将军带头推进 / 守城静态锚点阵型一条。

### 3.3 场景可见验证清单（今天工作的直接体现，逐项肉眼可验）

1. **列队**：6 人按手配防守阵型站位（弓后、近前、将军居中）
2. **跟随**：将军移动，全队槽位跟随（FollowAnchor + slotOffset）
3. **守阵交战**：敌进射程自动打、不追击（2 cell clamp，§4.1）
4. **破阵回援**：敌突入后排 → 前排无敌者回援 → 敌灭自动回槽（§4.2/§4.3）
5. **进攻推进**：切进攻意图 → 将军带头 MoveTowards → 全队压上（§14.2）
6. **守城编队**：无将军组绑城墙锚点，弓手上墙位 / 近战堵口（§14.7）
7. **减员**：死 1 近战 → 1s 防抖后残编重排（§15.3）；将军死 → 全体 ClearFormationState 解散回池（§15.5）
8. **补员**：脱战 10s 后从空闲池补满（§15.4；P0 热度判定未落地，可手动触发或固定计时兜底）

### 3.4 威胁评定统一因子表（合并改造前置，E4 的解）

合并 3.0.1_3 §14.6 与 LOD §3.2 改造前，先把目标态定为 **9 项**（权重占位，全入 AttentionTuningConfig）：

```
rawFactor = distFactor      距离（现状保留）
          + countFactor      数量（现状保留）
          + enemyPowerFactor 敌方战力（3.0.1_1 目标态补落地）
          + hpFactor         血量（现状保留）
          + allyFactor       友军保护（现状保留；编队光环/城墙地形未来都走此项加成来源）
          + selfPowerFactor  自身战力（3.0.1_1 目标态补落地）
          + timeFactor       昼夜（现状保留）
          + enemyTypeFactor  怪物种类（3.0.1_3 §14.6 新增 + EnemyTypeThreatWeight SO）
          + heatFactor       区块热度（LOD §3.2 新增）
前置：权重迁 SO + 删 enemyCount==0 早退（ThreatAssessment.cs:70）——一次改完，不改两轮
```

---

## 四、依赖链修订建议（D2）

```
原链：对象池 P0 → LOD P0 → 编队 P0
建议：前置工作（§二 + §3.1）→ 编队 P0（场景可见）
      → 对象池 P0 ∥ LOD P0（并行，互不阻塞）
      → 编队 P1（被 LOD 阻塞的部分：阵型切换/ThreatWeightTier/补充热度判定/enemyTypeFactor 合并改造）
```

依据：编队 P0 手配单阵型绕开 ThreatHeat（§十三自认）；ClearFormationState 白嫖已实装的 IMemoryComponent 契约（E3）；招募绕开 stub 自管（§十三）；6 人编队规模对象池无收益。对象池 P0 的真正消费场景是敌人波次生成与 500+ 规模，与编队场景体现正交。

---

## 五、文档勘误回写清单（待用户确认后回写）

| 目标文档 | 回写内容 |
|---------|---------|
| 3.0.1_3_AI协作.md | E1（§5.2 删城墙地形因子）/ E2（§15.2 传导链改写）/ E3（§15.5 依赖措辞）/ E5（§14.4 补"将军自身 rawFactor"占位）/ D4（§2.2 措辞）/ D2（§十 优先级按新依赖链重排） |
| 3.0.1_LOD性能架构.md | D1（§六 P0-2 降级仅君主中心先行）/ §七"待勘误"标已决（cellSize=2.26 以资产为准） |
| 两份同步 | §3.4 统一因子表编号（9 项目标态），替换"第 6 项"/"第八项"各自表述 |
| 母文档 §7.4 | 无勘误（UnitInstancePool/IMemoryComponent.Reset 契约引用均属实）；D2 依赖链说明可选补注 |
| GridConfig.cs:13 | 类默认值 32f → 2.26f（防误读，一行）——代码改动，随前置工作开工时做 |

---

> _2026-08-01 定稿。审计新发现勘误 5 项（E1-E5）、设计建议 4 项（D1-D4）、白嫖清单 14 项全过、cellSize 勘误债结清（资产 2.26）。将军占位复用 Ruler Variant 生产线零新机制；场景改造 5 项（S1-S5）后编队 P0 全部 8 项可在 GameScene 直接走查。依赖链建议修订为"前置 → 编队 P0 → 对象池 ∥ LOD 并行"。_
>
> _修订记录（2026-08-01）：§五 文档勘误已全部回写——3.0.1_3 七处（E1/E2/E3/E4/E5/D4/D2）+ 修订记录；3.0.1_LOD 四处（D1/E4×2/cellSize 已决）+ 修订记录；母文档 §7.4 补依赖链正交说明。GridConfig.cs 类默认值改动留待代码开工。_
