# HH.21 2_14 步骤7 段②精英 Brute NPCBrain 收官 + 段①段②合并待冒烟

> 类型：进度同步 + 待办登记（sim 行为差追加 HH.15，交训练仓会话）
> 状态：段②代码合拢并编译通过；合并 Play 冒烟待步骤8 传送门出现后验
> 日期：2026-08-24 · 发起端：执行端 · 关联：HH.20（D252/D411 施工序）/ 实施计划 2_14 步骤7段② / D252（精英进训练环）

---

## 一、段① 合拢回顾（承接上批，已 push 349bd98 + 6c53e3ba）

普通怪四态态机（MonsterAI）已交付：Raiding（价值×距离选建筑 D83）/ Guarding（PortalAttackedEvent 确定性回援 R4）/ Retreating（HP<retreatHpRatio 退门）/ Looting（停留掠夺携资源回门）。验收对照见实施计划。

## 二、段② 精英 Brute NPCBrain + MonsterMode 注入（本批）

**执行端按用户裁决实施（FactorContext 结构体零改动）：**

1. **Q2-A 桥接卡**：`MonsterSpawner.GetUnitData` 对 `isElite=true`（Brute）返回 `NpcProfessionDef`（从 MonsterDef.Brute 值桥接 hp/attack/attackRange/attackCD/isRanged/projectileSpeed/speedCells/speed vision），使 `UnitFactory.SpawnUnit` 的 `data is NpcProfessionDef` 分支自动 `brain.Init(npcDef)` —— NPCBrain.Init(NpcProfessionDef) 路径原生消费，零壳层侵入。
2. **Q1-B 壳层模式开关**：`NPCBrain` 加 `ConfigureMonster/SetMonsterMode/ApplyMonsterModeStimuli`（仅 `_isMonsterBrain` 时生效，普通单位零影响）；归巢点 `homePoint` 精英时＝传送门锚点 `_monsterHomePortal`（Guarding 回援 / Retreating 撤退 / SafetyStimulus 拉力统一到门）。**FactorContext 未加任何字段**，sim 侧无 schema 变更。
3. **精英态机让位**：`MonsterAI` 当 `_mc.IsElite` 时只做规则态机切换（HP 撤退判定 / OnPortalAttacked 切 Guarding）并 `SetMonsterMode` 同步给 NPCBrain，不直接驱动移动/攻击——移动/攻击姿态由 NPCBrain 决策核接管（精英进训练环 D252）。
4. **资产**：`Monster.prefab` 挂 `NPCBrain`（MCP execute_csharp_script 落）。普通怪（Raider/Slinger）data 是裸 UnitData（非 NpcProfessionDef）→ brain 不 Init → `_profession=null` 首行退出，MonsterAI 正常驱动；精英 Brute data 是 NpcProfessionDef → brain.Init 接管。隔离成立。
5. **编译**：0 error（Tuanjie get_state lastCompilation idle / console lastErrors 空）。

## 三、里程碑与遗留

- **段② 完成**（用户裁决 Q1-B 壳偏置 + Q2-A 桥接卡全部落地）。
- **sim 行为差待办**：已追加 HH.15（NPCBrain 精英 MonsterMode 行为差，交训练仓会话决策，勿默认照搬）。
- **段①+段② 合并 Play 冒烟**：**待步骤8 传送门出现后验**（HH.18 场景流纪律：切 GameScene 驱动 NewGame，直进 Play 无王国系统）。当前 Portal 已加临时召唤循环（步骤8 前推进），传送门出现后应可见：普通怪规则态机全链 + 精英走决策核。
- **红线核验**：建筑价值表（BuildingDef monsterTargetValue/monsterIsHighValue）与守卫战斗力权重（MonsterDef guardCombatWeight）均已 SO 化（段①）；FactorContext 段②零扩字段，sim 同步义务仅行为差（已登记 HH.15）。

## 四、交接待办

- [ ] **训练仓会话**：处理 HH.15 新增行为差待办（精英 Brute 决策核 MonsterMode 注入是否镜像），记台账。
- [ ] **执行端后续**：步骤8 波次构成（WaveDirector.SpawnPortalDisasterWaves D261）后驱动传送门出现 → 段①+段② 合并 Play 冒烟 → 校验四态基线 + 精英决策核行为。