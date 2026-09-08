# HH.107 任务书：资源供给端补链批（DZ-072a + DZ-073 + DZ-076 搭车）

> 签发：策划端 2026-09-08 ｜ 立项：D562（0.6 §九十二）｜ 账本：HH.107 🟡 / HH.108 🔵预留（完成报告）
> 接收方：执行端（TraeCode）
> 优先级：**高优——P1 六考前清偿**（7 条转职条目永久卡死=兵种进度阻塞面）
> 上游：HH.106 生命周期审计（D559）→ 策划端验收 D561（报告 §十五 七点裁决）｜ 本任务书=详细施工计划，**照此执行勿自由发挥**；与 HH.100 P0 调优批并行，开工前先做文件面划界（见 M10）

---

## 〇、背景与已裁决策（勿再议，照做）

1. **缺陷本体**：DZ-072🔴 水晶/火油产端全断（P8 反向「有消费无供给」首案）——消费端实锤：TrainingConfig 转职 7 条目 costCrystal=1（含火枪手30/磐石卫士31/游侠32）+SiegeWorkshopBuilding 火弹耗火油/魔弹耗水晶（L184-185）；供给端零：mine 副产链双断+贸易 `IsTreasuryResource` 拒收（TradeSystem L85/L116 注释自认「买入路由后置」）。
2. **原设计意图已出土**（D562 策划端实读）：ProducerComponent.UpdateByproductConfig L91-113 =「矿洞副产」完整实现——主产 Ore 的矿洞 Lv2 起副产水晶、Lv3 起副产火油+KingdomConfig.byproductCrystalCapacity/FireOilCapacity 参数在场。断因=mine.asset 实配偏离（outputResource=Stone 非 Ore+isResourceNode=1 连 ProducerComponent 都不挂）。
3. **D561/D562 已裁决策（Gate 前置清偿，本批直接照做）**：
   - 方向=**a 修 mine 产链**（否 b 入贸易/否 c 拆需求）
   - **mine 双身份语义=A2 变体（用户拍板）**：mine 保持采集点身份不动+专属副产组件恒产水晶/火油
   - **Ore 僵尸枚举本批不复活**（避免新造「有产无消」；Ore 处置另议）
   - 副产**无等级门槛**（mine levels=[] 无升级档，原 Lv2/Lv3 门槛不适用）；稀缺性=慢 rate，解锁节奏归 P0 调优观察
4. **验收硬条款=P8 配对审**（D561③）：修供给端必须同时验消费端解锁行为级——7 转职+弹药原料「实际可达」是本批验收主判据，产出来没人吃到=本批 FAIL。

---

## 一、施工详单（件1~4）

### 件1 mine 专属副产组件（核心）

1. **BuildingDef 加字段** `public bool isMineByproduct = false;`（字段尾插+默认 false=既有 asset 反序列化零影响；**禁动既有字段顺序**——M3）。Tooltip 写明「矿洞副产（DZ-072a，D562）：true=挂 MineByproductComponent 恒产水晶/火油入建筑存储」。
2. **mine.asset** 加一行 `isMineByproduct: 1`（**isResourceNode: 1 与 outputResource: 1 两字段禁改**——M1 红线：WanderStimulusProvider L181/GuardDeploymentSystem L45/Building.cs L582 拆除守卫/BuildingPanel L169 全消费 this flag，动=石头采集链断）。
3. **BuildingFactory.cs** AttachComponents 分支：仿 isSiegeWorkshop 先例（L295-297），isMineByproduct=true → 挂 `MineByproductComponent`（新组件，放 Systems/Building/，仿 SiegeWorkshopBuilding 结构）。
4. **MineByproductComponent** 逻辑：
   - Tick（由 ProductionSystem 调度——**核对 ProductionSystem 的组件调度枚举是否需要补本组件**，仿 SiegeWorkshopBuilding L41 先例；漏挂=组件永不 Tick，冒烟会抓）。
   - 每秒产水晶入建筑 StorageComponent（若 mine 无 StorageComponent 挂载——核对 mine def 走没走通用 Storage 挂载分支 L288-290：producer.rate>0+kind=Resource 但 isResourceNode 排除→**mine 现状无 StorageComponent**，组件需自管容量或同批补挂通用 Storage，执行端按现有 StorageComponent 接口选最小方案，列报选择）。
   - 火油同组件第二槽并行产（两资源独立容量，互不挤占）。
   - **参数 SO 化（so-data-driven 铁律）**：rate 两笔新增进 KingdomConfig（byproductCrystalRate/byproductFireOilRate，建议初值 0.05/s 量级=慢产保稀缺；capacity 复用既有 byproductCrystalCapacity=20/byproductFireOilCapacity）——**终值 P0 调优批调，本批只求链通**。
   - **失败可见性**：存储满停产分频日志（仿 SiegeWorkshopBuilding L144 口径）。
   - ResetState：随建筑销毁自然清（组件随 GameObject）——WorldLifecycle 编排无需新增（建筑域已有清场链），列报确认即可。
5. **存档**：水晶/火油入建筑存储→走 BuildingSaveData 既有 storage 序列化（核对 storage 字段是否含任意 ResourceType——**若 BuildingSaveData 存储结构只认四基础资源，列报并给最小扩法**，禁静默丢）。

### 件2 搬运/国库路由（DZ-073 + 副产落库路径）

1. **DZ-073 主修**：TaskScheduler L698/L702（UnloadInventory 溢出+无仓兜底→RulerController.ModifyResource 硬编码玩家国库）+L778（DepositAmmoBack 同病）→ 照抄 AddGatherOverflow 分流模式（L664-690）：kingdomId>0 入本国国库，=0 玩家原路径逐位零回归。
2. **按缺陷模式全消费面扫描（D561 方法论，硬性条目）**：`RulerController.ModifyResource` 全部兜底型调用点 grep 逐点定性（per-kingdom or 玩家专属）——不止上述两处，逐点列报处置（修/标注玩家专属理由）。禁「按 DZ 条目只修点名两处」（HH.86 尾巴教训）。
3. **副产落库路由验证**：mine 存储的水晶/火油→Porter/Transport 搬运任务是否自动生成（搬运任务按存储资源类型生成还是白名单？grep 搬运任务生成条件）——若搬运链不认水晶/火油，**列报最小扩法**（搬运白名单加两资源 or 通用化），本批必须打通「mine 存储→国库/AI 国库」全程，禁只产不运。
4. AI 侧：AI 领土内地图矿洞（isPlayerBuilt=0 自动带组件）→AI 工人搬运→AI 国库——per-kingdom 路由照抄 AddWater/TickGold 先例语义；**AI 建造池不动**（mine 是自然点，AI 供给依赖领土分布——无矿洞 AI=已知限制观察项列报，不做地图生成保证）。

### 件3 DZ-076 退役删除（搭车，半小时级）

1. 删 TimeManager L131（Subscribe）/L137（Unsubscribe）/L361-364（OnEnemyEnteredRegion）。
2. 删 GameEvents L590-601 `EnemyEnteredRegionEvent` 死定义（全库零发布+零存活订阅后删——删前 grep 复核零引用）。
3. **EnterCombatSlow 本体列报处置**：其唯一触发链=被删事件→删后变死方法。执行端列报（方法保留供未来设计评审 vs 同删），策划端验收时裁；**EnterCombatSlow 本体调用面若另有触发（复核确认零），禁误删**。
4. L-09 风险面注记：删除后考跑加速「战斗降速打断」隐患永久消除（写进完成报告）。

### 件4 冒烟+收口

1. **冒烟容器**（新建，仿 HH73 冒烟容器先例）：**自含容器禁挂 SmokeApi.EnterGame**（HH.64 口径——合成王国 fixture 假设）；矿洞=地图生成物，容器内直建 mine（BuildingFactory 直建+kingdomId 指定，仿 HH.79 直建营地先例）。
2. **探针 P1~P7（行为级，全绿=验收主判据）**：
   - P1 产出：玩家 mine 副产水晶入建筑存储（存储值增长可观测）
   - P2 落库：国库水晶增长（搬运链全程通）
   - P3 **消费端解锁（P8 配对审核心）**：水晶足额→火枪手(30) 转职成功（TrainingSystem TryTrain 全链）；负对照：水晶 0→转职拒（「水晶不足」日志）
   - P4 火油→弹药厂产火弹可行（容器直调 SiegeWorkshopBuilding 产弹验证原料扣除）
   - P5 **AI 同链**：AI 国（kingdomId>0）mine 副产→AI 国库水晶增长→AI 侧 PayRecruit 过水晶检（转职成功）
   - P6 负探针：无矿洞 AI 国水晶恒 0（地图依赖限制在案，不修地图生成）
   - P7 **石头采集零回归**：mine 石头 gather 照旧（采集量/搬运不变）
   - P8 DZ-073 验证：AI 工人搬运溢出→入 AI 国库（非玩家国库，国库差值断言）
3. 四容器回归（2_20/2_20B/2_20C/2_13_C）**玩家零回归硬红线**；编译 0 警 0 错。
4. 15_账本登记（执行端代登）：sim 无矿洞副产语义→差异注记（水晶/火油供给 Unity-only，sim 侧消费语义 W-K 批0 对齐口径归训练师）。
5. 完成报告=**HH.108**（教训核查行+git diff 自查前置）。

---

## 二、排雷图（M1~M10，逐条对照）

| # | 雷 | 处置 |
|---|---|---|
| M1 | mine.asset isResourceNode/outputResource 语义 | **两字段禁改**（isResourceNode 消费面 4 处+石头 gather 链）；只加 isMineByproduct 一行 |
| M2 | Ore 僵尸枚举 | 本批禁写任何 outputResource=Ore 配置、禁动 Ore 枚举位（int 铁律） |
| M3 | BuildingDef 字段序列化 | 新字段尾插+默认 false，禁动既有字段顺序/类型 |
| M4 | per-kingdom 路由 | 照抄先例（TickWaterToNetwork/AddGatherOverflow/TickGold），禁新发明路由形态 |
| M5 | ModifyResource 兜底面 | 全消费面扫描（件2.2），禁只修点名两处 |
| M6 | 冒烟容器口径 | 自含容器禁挂 SmokeApi.EnterGame；矿洞直建+kingdomId 指定 |
| M7 | Spawn/GetAllUnits 遍历雷区 | 维持 HH.76/78 纪律（快照副本，新增遍历先查） |
| M8 | HH.42 落盘幻觉 | 每笔编辑 grep 双锚点复验+git diff 自查=交付前置 |
| M9 | HH.100 并行划界 | 本批文件（ProducerComponent/BuildingDef/BuildingFactory/mine.asset/TaskScheduler/TimeManager/GameEvents/KingdomConfig+新组件）与 HH.100 在途面（评分/选址/野性域）开工前 git status 划界；TaskScheduler/TimeManager/GameEvents 若 HH.100 也在改→开工回执列报协调，写-改-commit 同串关窗 |
| M10 | 枚举/存档稳定铁律 | 新增 bool 字段+参数进 KingdomConfig=零 bump；禁碰任何枚举 int 值/存档 schema 版本 |

## 三、排期与验收

- 排期：**1~1.5 天**（件1 核心 0.5~1 天+件2 三行级+件3 半小时+件4 冒烟半天）
- 完成报告=**HH.108**（账本已预留）：含探针 P1~P8 全绿证据+ModifyResource 消费面扫描表+EnterCombatSlow 列报+15_账本登记回执+git diff 自查
- 策划端验收将查：探针行为级全绿/P8 配对审消费端解锁/mine.asset isResourceNode 逐字未动/M5 扫描表完整性/四容器玩家零回归/DZ-072/073 台账验收句

---

*签发：策划端 2026-09-08。账本占号 HH.107/108+D562 后落盘。施工疑义开 HH 信件询策划端，禁自由发挥。*
