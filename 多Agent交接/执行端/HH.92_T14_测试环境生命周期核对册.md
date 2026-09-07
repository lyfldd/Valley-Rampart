# HH.92 · T14 测试环境生命周期核对册（D549 件C-8）

> 日期：2026-09-07 · 产端：执行端（HH.92 施工批）
> 底册：`报告/设计审查/全模块覆盖审计_模块注册底表_2026-09-06.md`（315 文件=283 _Game+32 Editor 工装分账）
> 口径：只核**运行时单例+静态状态类**（Editor/Smoke 32 件工装不核，分账已明）；核对面=启动入口/复位口/WorldLifecycle.ResetWorldForNext 编排覆盖/换局残留风险/处置。
> 处置三级：✅补 ResetState（小，本批已做）/📋列已知限制（中，建议后续小批）/🧊低危（自愈或表现层，不动）。

## 一、编排覆盖核对表（Singleton 全集 53 个逐一核对）

### 1.1 已编排（WorldLifecycle.ResetWorldForNext 直调，20+3）

| 模块 | 复位口 | 备注 |
|---|---|---|
| TimeManager | ResetState | 含 HH.92/T1 新增 TestHarnessMode=false 复原 |
| DifficultyManager | ResetState | |
| RulerController / KingdomManager / PopulationSystem | ResetState | |
| **WaterNetwork** | ResetState | **T13 本批补入**（AI 水桶跨轮残留=M10 实锤缺口；正式局换局同受益） |
| RanchSystem / SiegeProductionSystem | ResetState | |
| GridSystem | ClearAll | |
| BuildingFactory | ClearAllBuildings | Destroy 延迟帧末（陷阱2：后须 yield 一帧） |
| ChestManager | ClearAll | |
| KingdomRegistry | ResetState | |
| **KingdomBrainRegistry** | ResetState | **T13 本批补入**（AI 决策注册跨轮残留=M10 实锤；换局后 Count=0 断言过，见 THD 三查） |
| **OverheadSpeechManager** | ResetState | **T13 本批补入**（表现层低危，同批补齐） |
| **DamageSystem** | ResetState | **T13 收尾本批补入**（THD v2 R3 实证漏项：攻击注册三表+_pendingAttacks 跨轮残留，详见 §二） |
| VagrantCampSystem / MapRenderService / TerritoryOverlay | ResetState/ClearAllTiles/ClearOverlay | |
| UnitRegistry | Clear | |
| WorldManager | ResetState | 清 ActiveMap 解锁二次建局 |
| SaveManager | ResetSessionState | 会话级（不清文件） |

### 1.2 编排等效覆盖（无独立 ResetState，机制性复位）

| 模块 | 机制 |
|---|---|
| GameStateManager | Singleton 无 ResetState——编排 SetState(Loading) 即复位（陷阱1 口径） |
| InputManager | DisableInput（编排①） |
| SimModeManager._uncoveredDays | MapGeneratedEvent 事件自清（SimModeManager.cs L52-57）✓ |
| BuildingRegistry | 建筑 Destroy→OnDestroy 自动 Unregister（随场景自清） |
| AttentionSystem(AI.Core) | 非单例（每 NPCBrain 私有），随 TeardownScene 销毁单位自清（编排注释在案） |
| LoadManager/TeardownManager | 流程控制器，阶段态由下一轮流程重置 |

### 1.3 无复位口 · 残留风险分级（未编排单例）

**📋 中危 · 列已知限制（建议后续小批补 ResetState；本批不动=防扩批）**

| 模块 | 残留态 | 影响 | 建议落点 |
|---|---|---|---|
| TaskScheduler | 任务字典引用旧单位 | 换局后旧任务引用悬空（有 null 守卫兜底但账面脏） | P0 调优批顺手件 |
| FormationManager | 编队引用旧单位 | 同上 | 同上 |
| WaveDirector | 波次节律/计数 | 换局后波次时序漂移；考跑侧已由 MonsterSpawner 守卫压制（T11） | 同上 |
| ProjectileManager | 飞行中投射物 | 换局瞬间弹道残影（1~2 帧内自灭） | 低收益缓办 |
| SceneHomePointProvider / WanderAnchorPool | 旧图世界坐标锚点池 | fixture/换局后 AI 漫游锚点指旧位置（小世界换图坐标域可能重叠） | 同 P0 顺手件 |
| ResourceRespawnSystem | 重生节律态 | 新图资源由地图生成重置，系统态待核（中） | 待核 |

**🧊 低危（自愈/表现层，不动）**

| 模块 | 自愈机制 |
|---|---|
| HappinessSystem | _overallHappiness/_taxBurden 字典——日结重算覆盖全 KingdomRegistry，换局首日自愈。**[THD v2 实测修订]** 自愈前提=日结先跑过；日结前首死直接索引 `_overallHappiness[0]` KeyNotFound（ghost 清玩家实体每轮 8 条；玩家侧开局当日死亡同源隐患）——已补建键防御（50f 中性初值，§二） |
| SatietySystem / LODSystem | 逐单位读数/逐帧活跃带刷新 |
| SelectionController / InteractionManager / BuildController | 玩家交互态，UI 层（换局回菜单路径重置）；null 守卫在 |
| UIManager / ToastManager / CameraRig / GroundEffectManager | 表现层短时态 |
| TradeSystem / TaxSystem | 玩家域数值；TaxSystem 分国桶由日结写入 |
| DayCycleSettlement / UnitDataManager / ProductionSystem | 编排器/配置缓存/逐建筑 tick，无跨局账本 |

### 1.4 静态状态类抽查（非 Singleton）

| 类 | 静态态 | 判定 |
|---|---|---|
| EventBus | _subscribers 字典 | 订阅方=长生命周期系统，随 OnDestroy 退订；T15 三查以 HasSubscribers 抽查在场（全量计数口=列报替代方案） |
| KingdomFoundry / TestHarnessApi(Editor) / WarehouseRegistry | 无跨局账本（WarehouseRegistry 随 StorageComponent.OnDestroy 注销） | ✓ |
| Valley_HH89_Smoke_Measure 等冒烟容器 | 静态结果表 | 每次 Run 头部自清 ✓ |

## 二、缺口施工记录（本批）

- T13：WorldLifecycle 编排补 3 行（KingdomBrainRegistry/WaterNetwork/OverheadSpeechManager，按依赖序插位：水桶随 PopulationSystem 后，脑注册随 KingdomRegistry 同段）——补齐后 THD 三轮建拆建「脑注册 Count→0 + 旧水桶→0」断言全绿（见 HH.93 报告）。
- T13 收尾（THD v2 跑批实测逼出的漏项，三修）：
  1. **DamageSystem.ResetState 补入+编排挂载**（Singleton 跨 ResetWorldForNext 存活但无复位口——v2 R3 观察窗 4586 次/轮 MissingReferenceException：R2 清场销毁的 fixture 箭塔注册残留，target=池化存活单位致 L248 现有 target 防御失效，`attacker.GetPosition()` 每帧炸且挂起攻击永清不掉）。三表+_pendingAttacks+_tickTimer 全清，WorldLifecycle ⑤ 段挂载。
  2. **ExecuteAttack 头部 attacker 假 null 防御**（玩家侧拆塔瞬间同源可触发，防御性一行不改变正常路径）。
  3. **HappinessSystem.OnUnitDied 建键防御**（日结前首死 `_overallHappiness[0]` KeyNotFound，v2 每轮 ghost 清玩家实体 8 条；玩家侧开局当日死亡同源隐患）。
  - **方法论教训**：v2 R1/R2 计数=8 全绿假象掩盖了 DamageSystem 漏项（死注册恰在 R2 被 target 双亡自愈路径清偿，R3 才因「死塔+活 target」组合爆量）——跨轮残留类漏项单轮绿不算绿，建拆建第 3 轮才是试金石。
- 审计交叉收获：底表 E10「ThroneAnchor 待定性」由本批 T0 定性**结案**=活引用（BuildingComponents L152 主城挂载+D249 工人全灭轮询驱动 GameOver），非退役死代码——底表该行**已由策划端改「复归」结案（2026-09-07 D552 验收串：E10 两行修订+迁移标记，策划端 grep 复核挂载链后定案）**。

## 三、结论

- 编排覆盖：53 单例中 20 直调复位 + 6 机制等效 + 6 中危列限制 + 15 低危 + 其余非运行时账本类——**换局清场主链闭环**（T13 后无已知「单例静态字典跨轮污染」高危缺口）。
- 中危 6 项全部列报，归 P0 调优批顺手件/后续小批，本批未动（防批膨胀纪律）。
