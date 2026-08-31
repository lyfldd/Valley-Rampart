# HH.43 · 2_17 步骤13（SimMode）三批交付报告（Gate 收口，待策划验收）

> 类型：交付报告（Gate 收口，三批合并交验）
> 状态：⏳待策划验收
> 日期：2026-08-31 · 发起端：执行端 · 关联：`HH.42`（Gate 四点全裁 A）· `0.6_审查决策记录.md` §四十三 D453~D456 · `2_17_AI王国脑与自主成长.md` §3.3（D333/D334/D344/D347）· `2_17_AI王国脑与自主成长_实施计划.md` 步骤13
> 锚点：Gate 已裁（HH.42 · commit 1212af8，策划端回写）；本批三 commit = 批A `13e13ab` / 批B `e0f389f` / 批C `32ea494`（均本会话实测后提交）

## 〇、恢复与前置状态

- 恢复四连已按序执行：开发计划书工作日志（HH.42 行）→ _交接索引（HH.42 行）→ _任务队列（13 行=🚧 Gate 已裁，按批A/B/C 实施）→ HH.42 报告 §三方案 + "策划裁决"节（四点全裁 A）+ 0.6 §四十三（D453~D456）。
- **关键事实（卫生指令落实）**：进入会话时发现**前会话（上下文丢失）已将批A/B/C 代码全部落盘但未提交**（5 文件 modified + 6 文件 untracked）。本会话未盲信工作树，先 `git diff HEAD` 全量核读三批代码，再静态核验全部 API 引用与资产约定，最后 Unity 实测后**按批逐一提交**。
- 工作树额外含**策划端美术批1 未提交 doc 5 份**（_任务队列 美术1 行 / 3.1.2 §10.2 / 0.6 §四十四 D457~D458 / 2_20 §7.1 / 2_20.1 族色），非本步内容，本会话**未触碰未提交**，留策划端回写（见 §六 诚实对账）。

## 一、批A 判定地基（commit 13e13ab，7 文件）

| 文件 | 改动 |
|------|------|
| `SimModeConfig.cs` + `.meta` | 新建 SO（so-data-driven 铁律，禁魔法数）：`offscreenDaysToAbstract=2` / `combatHotspotForceFine=true`，对齐实施计划 §三 SimModeConfig 表（D333/D344 仅两字段） |
| `SimModeConfig.asset` + `.meta` | 资产落 `Assets/_Game/Resources/Config/Kingdoms/SimModeConfig.asset`（Play `Resources.Load("Config/Kingdoms/SimModeConfig")` 实测加载成功，无缺配置警告） |
| `SimModeManager.cs` | P0 恒 Fine 占位 → 真实判定（D333/D344/D347 步①/D456）：逐 AI 王国，领土∩活跃带→Fine 立即 / 领土内战斗热点→Fine 强制（战斗锁，事件驱动立即+日判兜底双路）/ 连续 `offscreenDaysToAbstract` 日未覆盖→Abstract 迟滞；`_uncoveredDays` per-kingdom 运行时态不入档（D456，MapGenerated 清空）；`GetMode` 读 KingdomState.simMode（玩家/未知恒 Fine） |
| `LODSystem.cs` | 补 `IsActivelyCovered(Vector2Int mid)`（查 `_activeBandSet`，D344 视野=活跃带与相机缩放无关；无中心兜底同 RenderActiveBands 哲学）+ `HasActiveCombatHotspot(mid)`（热度>热点阈值，与 ComputeActiveCenters D77 同判据，供战斗锁）；`_midStates` 落 D455 口径注释（MapGenerated 整体清空即回收，不逐条——保护同 seed 确定性+热度记忆） |
| `DayCycleSettlement.cs` | 五步①接线真实判定（D347 步①）：`SimModeManager.EvaluateAllKingdoms()` 写各 AI 王国 simMode；读档默认 Fine 续跑（D456） |

## 二、批B 休眠/唤醒（commit e0f389f，1 文件）

- `NPCBrain.cs` 增 `IsSimDormant()` + Update 冻结分支（D334/D281）：
  - **冻结**：Abstract 王国的非军事单位（Worker/Porter/Civilian）停 Think+停感知+停寻路/移动——进入冻结首帧 `StopAttacking()` 注销攻击注册 + `_executor.Stop()` 停 PathFollower，实体**常驻原地冻结**（不销毁）。
  - **不冻结**：军事单位（Warrior 等）**不受影响**（D281 军队永远真实体）；玩家（kingdomId=0）/无国籍不冻结。
  - **唤醒**：simMode 回 Fine 即原地续干（位置/任务/进度保留，无额外恢复逻辑）。

## 三、批C 债清偿 + 冒烟（commit 32ea494，3 文件）

- `SiegeProductionSystem.cs` `ProduceMachine(type,pos,kingdomId)` AI 分支 **false 占位 → 真实现（D454 能力打通）**：
  - AI 国库扣费（`KingdomState.CanAfford/Spend`，镜像玩家 Spend 语义，五经济资源原子校验）
  - per-kingdom 上限（`GetPlacedMachineCountByKingdom >= GetMachineLimit()` 拒）
  - 生成带 kingdomId 单位（`UnitFactory.SpawnUnit` 门面自动覆写 `Faction.AiKingdom` 阵营，2_17 步骤10）
  - **不接触发方**（无 UtilityAction、无脑焦点——触发方属 2_18 军事期内容一并议，HH.42 裁 A）；玩家(id=0) 缺省走原路径零回归
- `Valley2_17_Smoke_13.cs` + `.meta`：新自含冒烟（GameScene Play，菜单「Valley/验证/2_17_步骤13_SimMode」，对齐 Smoke_11/12 绕开 NewGame 引导链哲学；含收尾清理防污染）。

## 四、门禁与行为级证据（Console 实盘，本会话 Unity 实测）

### 4.1 编译
- Play 进入后 read_console errors 仅 1 条既有 UI 警告（`No Theme Style Sheet set to PanelSettings`，与本次无关），**无编译错误、无 NRE**；SimModeConfig Resources.Load 加载成功（无 `未找到 SimModeConfig` 警告）。

### 4.2 Smoke_13 六探针（Console 原文）
```
[2_17_13冒烟] P1 #17 活跃带视野 Fine集稳定（聚焦覆盖→Fine；带不变 Fine集不变） = True
[2_17_13冒烟] P2 #9 切换迟滞（出1日F/出2日A/入立即F，反复10次) = True
[2_17_13冒烟] P3 #10 战斗锁（事件驱动立即=True 日判热点兜底=True） = True
[2_17_13冒烟] P4 军事不冻结/工人冻结（D281/D334） = True
[2_17_13冒烟] P5 D454 ProduceMachine AI（扣费/上限/带kingdomId） = True
[2_17_13冒烟] P6 P0基线 玩家/未知王国恒Fine（A4） = True
[2_17_13冒烟] ===== ALL PASS（P1#17/P2#9迟滞/P3#10战斗锁/P4军事不冻结/P5 D454/P6基线）=====
```

### 4.3 P0 基线（`Valley/验证/2_17_P0_完整局验收`，Console 原文）
```
[P0完整局] 基线@post-Init(轮纯) State=GameOver timer=0.33 bByK{0:1,1:4,2:4,3:4} b=2684
[P0完整局] RD2-①轮间清点=OK(b=2684/2684/2684 u=22/22/22)
[P0完整局] A3确定性逐字节=OK
[P0完整局] A4玩家零回归=OK
[P0完整局] B1玩家招募=OK(pTrue/True)
[P0完整局] B1AItry黄旗: K1 try30/30 ok0/0 K2 try30/30 ok0/0 轮间一致=OK
[P0完整局] B2供水抽象产出=OK
[P0完整局] B3+C6存读回环含脑态=黄旗挂2_11(独立卡)(存读seqlen=2928 vs 纯轮seqlen=2904)
[P0完整局] RD2-②存读v2门控=OK(v2走重建)
[P0完整局] B4剧本三段封顶=OK(R1-+无军事 R2-+无军事)
[P0完整局] B5派遣双证=OK(K1 build15/15 K2 build15 trainK1 try30 ok0)
[P0完整局] A3wood二分: 末一致日=行44 首差日=行-1
[P0完整局] ===== ALL PASS(状态面) =====
```
- **确定性红线**：A3 两纯轮（seed=20260828）**逐字节一致 = OK**——且 P0 etch 链已含 `(int)k.simMode`（BuildEtch L326），证明 SimMode 判定已纳入确定性链且跨轮稳定（本批无确定性破坏）。
- **P0 基线 b=2684 无退化**：三轮开局 b=2684/2684/2684（RD2-① 轮间清点一致）。
- B3 存读回环黄旗挂 2_11 独立卡 = **既有已知项**（HH.27 裁决③ harness 时序假差），非本步回归；P0 B1AI try30/ok0 = HH.28 裁决① 环境让渡（评分现象），非本步回归。

## 五、诚实对账

1. **前会话落盘未提交**：三批代码于本会话前已完整落盘（上下文丢失所致），本会话未改写代码内容，仅：静态核验（全部 API 引用逐一 grep 实证：`KingdomState.Territory/CanAfford/Spend`、`LODSystem.SetFocalCenter/RegisterHeatEvent/Cfg`、`UnitFactory.SpawnUnit(Faction,Occ,Vec2,int)/ReturnUnitToPool`、`UnitRegistry.GetAllUnits`、`UnitDataManager.LoadAll`、`EventBus`、`GridSystem` 全套、`SiegeProductionSystem.GetMachineLimit/GetPlacedMachineCountByKingdom/Cfg`、Occupation.Ballista、HeatSource.Hit 等）+ meta/asset 资产约定核验（base64 meta 为团结引擎既有格式，与 LodConfig/KingdomConfig 等存量对同）+ Unity 实测 → 按批提交。**无虚报、无补写**。
2. **禁区遵守（全部落实）**：SatietySystem 一行未碰（D453 归步骤14）；存档 schema 未动、simMode 不入档（D456，`_uncoveredDays` 亦不入档）；队列/_交接索引/设计文档/0.6 只读未改（0.6 的 D457/D458 为策划端并行轨内容，非本端改动）；训练仓 `ai决策大脑强化训练/`、champion/、Holdout/、根仓 harness/Scenarios/ 全未动（本批无 sim-sync T 级义务，HH.42 §五.4 策划端已判；SimModeConfig 参数 sim 无消费面，不登记差距账本）。
3. **工作树遗留报策划端**：策划端美术批1 未提交 doc 5 份（_任务队列 美术1 行 / 3.1.2 §10.2 D457/D458 / 0.6 §四十四 / 2_20 §7.1 / 2_20.1 族色改铜橙）仍在工作树未提交，**建议策划端尽快按「写-改-commit 同串」关窗**（防并行覆盖，HH.30 §六血案同型）。执行端三批 commit 未夹带任何 doc。
4. **已知非本步现象**：P0 存读轮 `SpawnFromSave 冲突`（读档建筑双份）为 2_16 既有读档双份卡/独立回归项（B3 黄旗），非本批引入。

## 六、影响面

- **玩家零接触**：SimModeManager 对玩家（id=0）恒 Fine；NPCBrain 冻结仅作用于 Abstract **AI 王国**非军事单位；GetMode 未知王国恒 Fine。
- **存档零改动**：simMode/_uncoveredDays 均运行时态不入档（D456）；KingdomRegistry 存档 schema 未动。
- **Siege 触发方未接**（D454 能力打通）：AI 王国不会自发造战争机器（无 UtilityAction/SiegeWorkshop 前置），仅能力层就位，触发随 2_18 军事期议——本批行为零触发风险。
- **sim-sync**：无 T 级义务（策划端已判）；本批未触碰 AI.Core 决策核/champion 参数/FactorContext。

## 七、验收请求

- 请策划端按三 commit 构成核对 + Smoke_13 六探针行为级 + P0 基线（A3 逐字节 / b=2684 / A4）验收本批。
- 验收通过后请策划端回写：队列 13 行 → ✅ 并解除 14 阻塞；挂账池「Siege 能力层清偿（D454）」销行（触发行保留军事期）；`_交接索引` 登记 HH.43。
- 下一步（执行端视角）：步骤14 AbstractEconomySettler（D453 Satiety 归此 + D336 抽象结算 + D335 对账）→ 2_13 主批 → Q10 → P1。
