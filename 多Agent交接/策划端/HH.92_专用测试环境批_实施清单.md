# HH.92 专用测试环境批 · 实施清单（策划亲自侦察定制版）

> 策划端定制：2026-09-07（D549 配套；任务书=HH.92_专用测试环境批_任务书.md）
> 本清单基于策划端**实盘代码侦察**（SmokeApi/WorldLifecycle/Valley_HH80_Run/TimeManager/UnitFactory/GameBootstrap/GameOver 链/ResetState 全集 grep），非任务书复述——执行端按本清单施工，凡与代码实况冲突处以代码实况为准并列报。
> 完整性校验（铁律5）见 §五；排期（7 天倒计时）见 §六。

## §〇 开工前置（P0，硬门槛）

| # | 检查 | 不满足则 |
|---|------|---------|
| P0-1 | **度量衡批 HH.89 已收口**（git log 含其 commit + `WorldConfig.asset` time.secondsPerDay=360 + `SyncConfigFromWorld` 全项目零命中）——两批均动 TimeManager，基线错位=白修 | 等收口，不开工 |
| P0-2 | git status 工作区确认（美术件 4 个 modified 属域外勿动；HH.42 家族纪律） | 列报 |
| P0-3 | T-pre 侦察任务：**D111 停跑真实链路定性**——策划端 grep 实锤全项目仅 2 处 `SetState(GameState.GameOver)`：GameBootstrap.cs L170（**读档失败**路径，非玩家死亡）+ ThroneAnchor.cs L82（D441 已退役组件=疑似未挂载死代码）。五考 D111「玩家灭绝收口」的真实停跑机制**未定案**——施工第 2 日前完成：grep 玩家城堡毁灭/王国灭亡监听链+裸 Play 复现，定位后向策划端列报，T10 据此封死 | 不定性不施工 T10 |

## §一 排雷图（历史教训 × 本批施工点，策划端预判——每条都有实锤出处）

| # | 雷 | 出处/侦察 | 本批对应 |
|---|-----|----------|---------|
| M1 | **SetGameSpeed 四档吸附 {0.5,1,2,3}**——15x 请求被 SnapToSpeed 吸到 3x，「1 秒=1 游戏小时」挂不上去 | TimeManager.cs kGameSpeeds/SnapToSpeed（本轮侦察） | T1 |
| M2 | **战斗降速打断加速**——EnemyEnteredRegionEvent→EnterCombatSlow 强制 1x+ExitCombatSlow 恢复 _pendingScale，直接设 Time.timeScale 会被拉回；考跑加速必须走 TimeManager 内部专用通道跳过该链 | TimeManager.cs Update/OnEnemyEnteredRegion（本轮侦察） | T1 |
| M3 | **maximumDeltaTime 默认 0.33s 钳制**——掉帧时 deltaTime 被钳、加速静默失效（跑得越快越要保帧率） | Unity 默认值，项目零设置（本轮侦察） | T3 |
| M4 | **Update 仅 GameState.Playing 推进**——任何非 Playing 态时间停摆；AdvanceTime 的 while 跨天循环在 15x+maximumDeltaTime=1.0 下单帧游戏时间 ≤15s<360s 永不跨天（安全），但 maximumDeltaTime 不得设 >24s/天/15=24s | TimeManager.cs AdvanceTime（本轮侦察） | T3 |
| M5 | **注册链时序 bug**（D541 挂账）——UnitFactory.SpawnUnit L88 `controller.Initialize(data)` 先于 L90 `controller.kingdomId = kingdomId`，事件/注册早于归属写入；fixture 注入单位全 kingdomId=0=夹具全废 | UnitFactory.cs L57-95（本轮侦察定位到行） | **T5 第一施工位** |
| M6 | **裸构 vs 生产链**——HH.61 P4 实证：AddComponent 裸建单位物理查询不可用；建筑/单位注入必须走生产 Prefab 实例化链 | HH.61/D508 冒烟纪律 | T6/T7 |
| M7 | **资源三源双账本**——国库（KingdomState）/仓库（WarehouseHelper）/AI 水桶（WaterNetwork）三源不同步=AI 第一步就渴死饿死而非执行被测行为 | HH.73/D454/D537 | T8 |
| M8 | **生育/招工前置挡**——幸福桶/饱食/房容底数不足=AI 生育招工全被门槛挡（三考卡死点同款）；注入王国若底数为 0 则观察不到任何 AI 判断 | HH.80/D542 | T9 |
| M9 | **离屏切 Abstract**——注入点在活跃带外第 8 日切 Abstract 冻结发展（offscreen=7），观察对象「假死」 | D548 五考 offscreen 实证 | T12 |
| M10 | **WorldLifecycle 复位缺口（策划本轮侦察实锤）**：全项目 13 个单例有 ResetState，编排已覆盖 10+Clear 系——**KingdomBrainRegistry.ResetState（L54）与 WaterNetwork.ResetState（L141）未被调用**=换局后 AI 决策注册+AI 水桶跨轮残留（高危）；OverheadSpeechManager（L120）低危表现层 | grep ResetState 全集 × WorldLifecycle.cs 编排清单 | **T13** |
| M11 | **观察器白名单盲区**——新 API/新日志 tag 不进 Valley_P1_Observer 白名单=零观测（HH.86 1g 教训） | HH.86/DZ-065 | T16/全件日志 |
| M12 | **玩家默认路径 NRE 风险**——kingdomId=0 默认玩家+玩家锚单源+RulerController/SelectionController/镜头跟随；无玩家模式必须普查这些路径（硬移除玩家王国=引爆 NRE，故采用幽灵化） | D445 锚点单源摸底/D345 | T10 |
| M13 | **45 日大世界 Load 卡死**（D541 风险清单）——fixture 场景用小世界（Small）绕开大读档路径 | HH.79 §三.5 | T6/T18 |
| M14 | **自含容器禁挂 EnterGame**（HH.64 口径）——合成王国/远域 fixture 会破 EnterGame 假设；真实进局链才走 SmokeApi.EnterGame | HH.64 段B#1 | T18 |
| M15 | **确定性红线**——fixture 注入定序（王国/建筑/单位固定序）禁未种子 Random；同 seed 两轮输出必须一致 | 项目铁律 | T6/T7/T18 |
| M16 | **HH.42 家族**——交付前 git diff 自查+grep 双锚点复验=注册类施工标准动作 | HH.42 第 1~9 笔 | 全程 |

## §二 任务总表（T0~T19，铁律4：每条可执行可验收）

| 编号 | 任务 | 类型 | 依赖 | 验收标准 | 证据文件 |
|------|------|------|------|---------|---------|
| T0 | D111 停跑链路定性（P0-3）：grep 玩家城堡毁灭/王国灭亡监听链+裸 Play 复现，一页纸定性列报——两个 SetState(GameOver) 候选（GameBootstrap L170 读档失败路径/ThroneAnchor L82 退役残留）谁在 D111 实际生效 | 架构·侦察 | — | 定性结论+封死点明确 | grep 记录+列报 |
| T1 | TimeManager 考跑模式：新增 TestHarnessMode 标志+EnableTestHarness(speed) 直通挂档（**不走 SnapToSpeed 吸附**）+EnterCombatSlow/ExitCombatSlow 头部考跑守卫（M2）+ResetState 复原；**SetGameSpeed/SetTimeScale/kGameSpeeds 原逻辑逐位不动**（玩家 UI 零回归红线） | 架构 | P0-1 | 15x 挂档成功；野怪进感知 timeScale 不回落（行为探针）；grep 原 UI 链零改动 | TimeManager.cs |
| T2 | TimeConfigData 加 testSpeedMultiplier 字段（默认 1）+WorldConfig.asset 序列化 | 参数 | P0-1 | asset 在场（**read_file 直读+grep 双源**，HH.42 第 9 笔 Glob 幻觉教训） | WorldConfig.cs/.asset |
| T3 | TestHarnessApi 考跑一体开关（Editor/Smoke/TestHarnessApi.cs）：EnterTestRun=EnterGame→就绪→EnableTestHarness(15)→maximumDeltaTime=1.0f→无渲染减负（关阴影/VSync，**记原值**）；ExitTestRun 全量恢复（M3/M4） | 架构 | T1/T2 | 开关前后 Time.maximumDeltaTime/QualitySettings/timeScale 断言+恢复断言 | TestHarnessApi.cs |
| T4 | 15x/10x/1x 三档对照冒烟（2_20B 同 seed 三轮，判定列逐项对拍；**假红=判定列漂移即回退 10x 列报**） | 架构 | T3 | 探针零假红或回退列报+对照表 | 冒烟日志+对照表 |
| T5 | **注册链时序根治（第一施工位）**：SpawnUnit 的 kingdomId 赋值（L90）早于事件/注册发布——修法二选一（Initialize 加 kingdomId 参数 / 事件发布后移），先 grep Initialize 调用方+UnitSpawnedEvent 订阅方全集（D541 影响面 62 处） | 架构 | P0 | fixture 注入单位**当帧** kingdomId 正确；订阅方读到的归属全对；四容器回归 | UnitFactory.cs+普查清单 |
| T6 | TestFixtureApi.PlaceKingdom(tier, raceId, pos)：参照 KingdomFoundry.FoundFirstGeneration(L26) 注册链（kingdomId 派发/KingdomDef/RaceDef/染色 bannerColor）；三档=开局/中期/军事期；**档位数值 SO 化（禁硬编码清单值，拟好后报策划核准落资产）**；小世界（M13） | 架构 | T5 | 三档各建一国零报错+CSV 快照可见 | TestFixtureApi.cs+档位 SO |
| T7 | 建筑注入走生产链（BuildingFactory 正常链含占格/注册，M6 禁裸构）+派工行为探针 | 架构 | T6 | farm 注入后 Working 工人数>0 | TestFixtureApi.cs |
| T8 | 资源三源同步注入：KingdomState 国库五资源+仓库本地仓+WaterNetwork.AddWater(kingdomId)（M7） | 架构 | T6 | 三源读数断言一致 | TestFixtureApi.cs |
| T9 | 幸福/饱食/房容 warm-up：注入当日起生育/招工条件不被底数挡（≥KingdomConfig 阈值 60/50+军事期档房容满足）（M8） | 架构 | T6/T8 | 注入次日生育/招工条件判定为可满足（探针） | TestFixtureApi.cs |
| T10 | **无玩家模式默认 ON（用户拍板）**：玩家幽灵化=清玩家初始实体+按 T0 定性封死 GameOver 链+CameraRig 跟随判空+RulerController/SelectionController/锚点解析 NRE 普查（M12）+观察器/CSV 排除 k0；**禁硬移除玩家王国注册**（kingdomId=0 默认路径雷区） | 架构 | T0/T6 | 无玩家场跑 30 日零 GameOver 零 NRE 崩溃；CSV 无 k0 行 | TestFixtureApi.cs+NRE 普查列报 |
| T11 | 噪声开关（默认 ON）：一次性清（Monster/Vagrant 全清+营地注销）+**持续压制**（订阅日 tick 清新刷；VagrantCampSystem 自然刷点加考跑守卫——D541 件3 刷点链不关会复活） | 架构 | T6 | 默认 ON 下 30 日野怪/流浪数恒 0 | TestFixtureApi.cs |
| T12 | SimMode 锁 Fine 钩子：fixture 王国强制 Fine（防 M9 离屏切 Abstract 假死）——SimModeManager 考跑豁免集或注入点选活跃带内，二选一执行端定；**Unity-only 钩子列 15_账本**（不进 sim 语义） | 架构 | T6 | 离屏注入国 30 日全程 Fine | SimModeManager.cs+15_账本列报 |
| T13 | **WorldLifecycle 缺口补齐（M10 实锤）**：编排补 KingdomBrainRegistry.ResetState+WaterNetwork.ResetState+OverheadSpeechManager.ResetState 三行（按依赖序插位） | 架构 | — | 换局后 AI 水桶全零+决策注册空（断言） | WorldLifecycle.cs |
| T14 | 生命周期核对册：底表 315 文件（审计 P-0 产物）→运行时模块核对表（模块/启动入口/复位口/编排覆盖？/处置）+缺口施工；优先级 Systems>Rendering>Editor 工装 | 架构 | T13 | 核对册 .md 交付+缺口全处置或列报 | 核对册文档 |
| T15 | 建拆建 3 轮零残留三查：实体计数+EventBus 订阅计数（无计数口则列报替代方案）+单例静态字典抽查（冷却/缓存 Dict） | 架构 | T3/T13 | 三查断言 3 轮全绿 | 冒烟容器 |
| T16 | 行动池 21 条逐条触发核对表+冒烟（底册=审计 P-1 件1 行动池清单+UtilityActionConfig SO 实值；M11：新日志 tag 入观察器白名单） | 架构 | T6/T9 | 21 条全触发+执行证据在案 | 核对表+冒烟容器 |
| T17 | 玩家入口 18 项分类核对：逻辑入口（API 可测）逐条核/UI 入口考跑无 UI=列报不测 | 架构 | T16 | 分类核对表交付 | 核对表 |
| T18 | **MVP 演示容器 TestHarnessDemo**（真实进局链 SmokeApi.EnterGame+T10 幽灵化，非合成容器 M14）：无玩家场+2 AI 军事期 fixture 王国+15x 跑 20 日+断言（AI 自主扩军≥1+新建筑≥1+零 GameOver+零 NRE+CSV 20 行） | 架构 | T4/T6-T12/T16 | 四条断言全绿 | Editor/Smoke/TestHarnessDemo |
| T19 | 验收回归：MVP 四条演示+四容器回归（2_20/2_20B/2_20C/2_13_C）玩家侧零回归 | 架构 | T18 | ALL PASS | 日志+报告 HH.93 |

## §三 关键施工详规（坑位级——执行端按此避雷）

### 3.1 件A 时间层

- **T1 修法**：TimeManager 加 `public bool TestHarnessMode { get; private set; }` + `EnableTestHarness(float speed)`（TestHarnessMode=true；`Time.timeScale = speed` **直通**；CurrentTimeScale 同步记录供观察器读）。`EnterCombatSlow()/ExitCombatSlow()/OnEnemyEnteredRegion()` 三个方法头部加 `if (TestHarnessMode) return;`。`ResetState()` 加 TestHarnessMode=false 复原。
- **禁改清单**：kGameSpeeds 数组、SetGameSpeed、SetTimeScale、SnapToSpeed、ClampToAllowedScale——玩家 UI 倍速链零字节改动（D547 度量衡批已动过 TimeManager，回归面敏感）。
- **坑**：_pendingScale 不要在考跑模式下写（防 Exit 后战斗降速恢复链读到考跑值）。

### 3.2 件B 夹具层（本批核心）

- **T5 是全批第一施工位**：不先修时序，T6 注入的人口全是 kingdomId=0（P2a 假阴性同款），后面全白做。修法二选一时先做影响面普查（D541 已列 62 处+HH.76 雷区 35 处底册在案）。
- **T6 参照物**：KingdomFoundry.FoundFirstGeneration（L26）就是「无中生有建王国」的现成完整链——fixture=手动参数版。三档数值**先拟草案报策划核准再落 SO**（军事期档直接决定 P0 调优批 DZ-068 验证基线，两个批要对齐）。
- **T8 陷阱**：WaterNetwork.AddWater(kingdomId) 口径（D537 批 3a 已开）——**AI 桶有上限**，注入前查 IsBucketFull；溢出分流别把玩家桶污染（D545 纪律）。
- **T10 无玩家模式施工序**：①先 T0 定性（**不要臆断**——两个 GameOver 候选一个是读档失败路径、一个是退役组件，真实链可能另有其人）②清实体用 UnitRegistry 遍历删 k0 单位（走 Die 回池链，防对象池脏）③NRE 普查重点：RulerController.BindExistingMonarch（读档链用，新建局可能不触）、ThroneAnchor 残留引用、CameraRig 跟随目标判空——普查结果**全量列报**，修不过来的列已知限制。
- **T11 持续压制**：只清一次没用——D541 件3 自然增长刷点+野怪刷新是持续源。刷点入口加考跑守卫（查 VagrantCampSystem/野怪 Spawn 入口）。
- **T12 与 sim-sync 边界**：锁 Fine 是 Unity 侧测试钩子，**不进 sim 决策语义**（sim 侧无此概念）——15_账本列报一条（同 D537 #7 先例），sim-sync 不产生 T 级义务。

### 3.3 件C 生命周期层

- **T13 插位**：KingdomBrainRegistry 插在 KingdomRegistry.ResetState 同段附近（AI 决策注册属王国域）；WaterNetwork 插在 PopulationSystem 之后（水依赖人口语义）。**这两行是本轮策划侦察实锤的存量缺口**——不止服务本批，正式局读档换局同样受益（列报时注明）。
- **T14 别贪全**：315 文件里 Editor/Smoke 32 件工装不核（分账已明）；只核运行时单例+静态状态类。核出缺口分三级：补 ResetState（小）/列已知限制（中）/立项（大）。
- **T15 EventBus 计数**：EventBus 若无订阅计数口，加一个 Editor-only 只读调试属性即可（禁改发布链）。

### 3.4 件D 门面 API 层

- **T16 核对表格式**（每行一条）：行动 id | 触发方式（评分自然触发/直接 API 强制）| 执行证据（日志 tag/状态变化字段）| 断言（探针写法）。MVP 允许「直接 API 强制触发」——但要标注哪些行动**从未被评分自然选中**（这个差集本身就是 P0 调优批的输入）。
- **T18 演示容器的 20 日观察窗**：军事期 fixture 注入后 AI 需要 2~5 日「消化」资源底数才出行为——断言窗口别从 D0 算，从注入后第 5 日算起。断言不达先查 fixture 档位（T6 核准时资源水位）再查 AI 评分——评分问题归 P0 调优批，勿在本批修 AI。

## §四 跨文档依赖矩阵（铁律2）

| 本批任务 | 依赖 | 状态 |
|---------|------|------|
| P0-1/件A 全部 | HH.89 度量衡批收口（TimeManager 360 基线） | ⏳ 待收口（硬前置） |
| T6 档位数值 | P0 调优批目标态（DZ-068 验证基线对齐） | 协商：执行端拟→策划核准 |
| T14 核对册 | 审计 P-0 模块注册底表（c641a67 已入库） | ✅ 在库 |
| T16 底册 | 审计 P-1 件1 行动池 21 条+玩家入口 18 项（已入库） | ✅ 在库 |
| T12 | sim-sync 15_账本列报义务（Unity-only 钩子） | 本批内做 |
| T10/T18 | HH.71 观察器/HH.64 容器纪律（白名单/自含容器口径） | ✅ 在案 |
| 反向：P0 调优批（D548）验证 | 本批 T6 fixture+T4 考跑档 | 本批先行 |

## §五 完整性校验表（铁律5：任务书章节 → 清单任务）

| 任务书章节 | 清单任务 | 状态 |
|-----------|---------|------|
| 件A-1 timeScale SO 化（DZ-071 清偿） | T2 | ✅ |
| 件A-2 一体开关（maximumDeltaTime+无渲染） | T3 | ✅ |
| 件A-3 三档对照 | T4 | ✅ |
| 件B-4 PlaceMatureKingdom 三档 | T6（API 名 PlaceKingdom，语义同） | ✅ |
| 件B-5 噪声开关+玩家处理 | T10/T11（**用户插话升级：无玩家默认**） | ✅ |
| 件B-6 放置任意物 API | T6/T7（建筑+单位；野怪/资源点=T6 扩展参数） | ✅ |
| 件C-8 生命周期核对册 | T13/T14 | ✅ |
| 件C-9 注册链时序根治 | T5 | ✅ |
| 件C-10 建拆建零残留 | T15 | ✅ |
| 件D-11 行动池 21 条 | T16 | ✅ |
| 件D-12 玩家入口 18 项 | T17 | ✅ |
| §四 验收 MVP 四条 | T18/T19 | ✅ |
| （策划侦察新增，任务书未列） | T0 停跑链路定性/T1 战斗降速抑制/T8 三源/T9 warm-up/T12 锁 Fine | ✅ 新增件 |

## §六 排期（7 天倒计时）

| 日 | 内容 | 里程碑 |
|----|------|--------|
| Day1 | P0-1/P0-2/P0-3(T0)+T5+T1+T2+T3 | 时序根治+时间层完成 |
| Day2 | T4+T6+T7 | 三档对照结论+fixture 骨架 |
| Day3 | T8+T9+T10+T11+T12 | **夹具层全通（P0 调优批可取用）** |
| Day4 | T13+T14+T15 | 生命周期闭环 |
| Day5 | T16+T17+T18 | 演示容器可跑 |
| Day6 | T19+报告 HH.93 | MVP 验收 |
| Day7 | 缓冲（假红排查/回退窗口/策划验收） | — |

> 纪律重申：每件施工=git diff 自查+grep 双锚点；MVP 优先，P2 backlog 不阻塞验收；凡与代码实况冲突处以代码实况为准列报，勿按清单硬套。
