# HH.110 存档域小批完成报告

> 编号：HH.110（账本预留号，随 HH.109 同笔）｜ 施工批次：HH.109《存档域小批》（D564：DZ-074 Chest 全链入档 + DZ-075 训练队列入档/清场泄漏修）
> 执行端：TraeCode ｜ 完成日期：2026-09-08 ｜ 状态：**施工完毕待验收**（验收主判据 P1~P4 行为级全绿已达成，见 §三）
> 任务书真源：`多Agent交接/策划端/HH.109_存档域小批_任务书.md`

---

## 一、施工清单（件1~3 全落）

### 件1 DZ-074 Chest 全链入档 ✅

| 施工点 | 锚点 | 内容 |
|---|---|---|
| 新 payload 契约 | [ChestSaveData.cs](../Valley%20Rampart/Assets/_Game/Systems/World/ChestSaveData.cs)（新） | `ChestSavePayload{version=1, List<ChestSaveEntry>}`；单条目=cellX/cellY/bornDay/ownerFaction/contents（ResourcePack 全 int struct 直序列化）。**无开启交互态字段**——ChestEntity 无此态（任务书「若有」核实为无） |
| ChestManager 挂 ISaveable | [ChestManager.cs](../Valley%20Rampart/Assets/_Game/Systems/World/ChestManager.cs) L24/L27/L33 | `SaveId="ChestManager"`；`LoadPhase=Scene`（列报①）；Awake 注册（KingdomManager L74 先例） |
| SaveState | 同上 L151 | 遍历 _chests 逐箱序列化；fake-null 死引用不入档（读档侧自愈） |
| LoadState 先清后建 | 同上 L175 | **ClearAll()→逐箱内联重建**（幂等，M2）；重建不走 SpawnChest——bornDay 用存档原值（过期锚点不重置）、不做单格上限检查（存档态即合法态） |

### 件2 DZ-075 训练队列入档 + 清场泄漏修 ✅

| 施工点 | 锚点 | 内容 |
|---|---|---|
| 新 payload 契约 | [TrainingSaveData.cs](../Valley%20Rampart/Assets/_Game/Systems/Building/TrainingSaveData.cs)（新） | `TrainingSavePayload{version=1, entries}`；条目=buildingSaveId/unitSaveId/buildingId+from/to Occupation（TrainingDef 反查三键，SO 改动不破档）/startDay/inTraining/kingdomId/effCostDays |
| TrainingSystem 挂 ISaveable | [TrainingSystem.cs](../Valley%20Rampart/Assets/_Game/Systems/Building/TrainingSystem.cs) L18/L21/L259 | `SaveId="TrainingSystem"`；`LoadPhase=Scene`（M2 硬条款，列报①）；Awake 注册 |
| SaveState 死键过滤 | 同上 L139~ | building fake-null/条目 unit null 或 !IsAlive → 不入档（**旧档残留死键首次存档自愈**）；单位丢失条目作废**不退款**（D564 裁定口径，列报⑥） |
| LoadState 先清后恢复 | 同上 L172~ | `_queues.Clear()`（防跨局 DontDestroyOnLoad 残留——读档链 ContinueFromSave 仅 TeardownScene 不清队列）→ 按 buildingSaveId/unitSaveId 经 `SaveManager.TryGetSaveable` 反查实例（阶段 1.5 已重建注册，M2 时序核对成立）→ TrainingDef 按 buildingId+from/to 三键反查（**不重验门禁**——存档态即合法态，重验会吞档） |
| **SaveManager.TryGetSaveable（支撑件）** | [SaveManager.cs](../Valley%20Rampart/Assets/_Game/Systems/Save/SaveManager.cs) L182 | 反查 API（列报④）；只增不改既有语义（M1）；幽灵引用视同未注册 |
| **清场泄漏修=方案B** | [WorldLifecycle.cs](../Valley%20Rampart/Assets/_Game/Systems/Loading/WorldLifecycle.cs) L44 + TrainingSystem.ResetState L244 | `ResetWorldForNext` 编排在 `ClearAllBuildings()` 后插 `TrainingSystem.Instance.ResetState()`（HH.93 T13 同族编排先例）；ResetState 清 `_queues`+`_recentDeaths`（溃败补充窗口） |

### 件3 冒烟探针 ✅（容器=[Valley_HH109_Smoke_SaveDomain.cs](../Valley%20Rampart/Assets/Editor/Smoke/Valley_HH109_Smoke_SaveDomain.cs)，菜单 Valley/验证/HH109_存档域小批）

见 §三 探针证据。

### 配套

- **L-05 白名单**：[Valley_P1_Observer.cs](../Valley%20Rampart/Assets/Editor/Smoke/Valley_P1_Observer.cs) 补 `[ChestManager]` tag（新日志域=读档重建观测）。

---

## 二、M1~M5 排雷自查

| # | 雷 | 结果 |
|---|---|---|
| M1 | schema 稳定 | ✅ 全局 `saveVersion=3` 未动（grep `CurrentSaveVersion` 仅 SaveManager 常量定义处）；两个新 payload 均模块自治 `version=1`；既有 SaveId/LoadPhase 零改动（git diff 复核：SaveManager 仅新增 TryGetSaveable 方法体） |
| M2 | 读档时序 | ✅ 建筑/单位在阶段 1.5 由 Spawner 重建并 OverrideSaveId+RegisterSaveable → TrainingSystem（阶段 2 Scene）反查必然可及；MapGenerated 宝箱幂等见列报② |
| M3 | 落盘幻觉 | ✅ 每笔编辑 grep 双锚点（§五）+git diff 自查（§六）+**L-13 双通道**（read_console 编译 0 错 + execute_code 类型探活 7/7 True：Training/Chest ISaveable、TryGetSaveable、ResetState、双 payload、容器类型） |
| M4 | 文件面划界 | ✅ git status 划界：基线干净（HH.107 已 commit 3d8fcb0）；本批 git 面 5 改 3 新全在界内（§六）；**Building.cs 本批零改动**（清场修选方案 B，M4 列报协调点解除） |
| M5 | 冒烟口径 | ✅ 生产链直建（SpawnChest 唯一落箱入口/TryTrain 唯一入队入口/CreateBuildingInstance）；**容器走测试环境正门 TestHarnessApi.EnterTestRun**——初版误走 SmokeApi.EnterGame 引发「河谷失守」判负事故，已修复并升列为报告点（§七，用户点名报策划） |

---

## 三、验收主判据证据（P1~P4 行为级，第六跑 ALL PASS）

```
[P1] 破坏态 Count=2→读后=2（期望2）；箱A一致=True 箱B一致=True =True
     （cell/contents 八字段/bornDay/ownerFaction 逐项；破坏态=Remove箱A+另放金99箱→读档后存档态覆盖=幂等直证）
[P2] 存档日=2 恢复一致=True（条目2+startDay/effCostDays/inTraining 逐项；目标=Warrior） 续训完成=True 排队晋升=True =True
[P3] 三轮建拆建（第3轮判定）R1:入队=True 清前=2 清后=0 死键=0 R2:清前=1 清后=0 R3:清前=1 清后=0 =True
[P3W] ResetWorldForNext 整合：入队=True 清后=0 死键=0（编排行接通） =True
判定：ALL PASS
```

- **P1**：两箱（异格/异内容/异阵营）→Save→破坏态→Load→逐字段一致 ✅（DZ-074 行为级直证）
- **P2**：在训+排队两条→快进半程→Save→**清场复刻**（TeardownScene+ClearAllBuildings=真实读档链 ContinueFromSave 语义单场景复刻）→Load→startDay/effCostDays/inTraining 镜像一致（**进度=游戏日跨读档连续**）→续快进→在训条目转职完成+排队条目晋升完成 ✅（DZ-075 行为级直证）
- **P3**：三轮 [入队→ClearAllBuildings+ResetState→零死键]（**L-11 口径：生命周期类 ≥3 轮、第 3 轮为判定口径**）+ResetWorldForNext 全链整合验证 ✅
- **P4 四容器回归**：2_20 复跑 ALL PASS（首跑仅②d 一项 FAIL——受击竞态噪声，复跑全绿=L-14 处置，语义面与本批文件零交集）/ 2_20B 六轮 ALL PASS（含跨轮 ActiveMap 新实例+探针实体零残留）/ 2_20C ALL PASS / 2_13_C ALL PASS（P1~P7）→ **玩家零回归硬红线达成**
- **编译**：0 错 0 新警（存量 5 警均在 2_17/2_21A/2_20 旧容器文件，非本批文件）

---

## 四、DZ 台账验收句

- **DZ-074 ✅**：Chest 全链入档（D223 溢出装箱/TrySpawnOrcLoot 兽人战利品/MonsterController 怪物掉落三类来源经 SpawnChest 唯一入口统一覆盖）——存读档循环箱子不再蒸发（P1 行为级直证）。
- **DZ-075 ✅**：训练队列入档（_queues 全量入档+读档恢复+跨读档进度连续）+清场泄漏修（WorldLifecycle 编排 ResetState，三轮+整合双验证零死键）。

---

## 五、grep 双锚点交付前置复核（M3）

| 锚点 | 命中 |
|---|---|
| `SaveId => "ChestManager"` | ChestManager.cs:24 |
| `SaveId => "TrainingSystem"` | TrainingSystem.cs:19 |
| `RegisterSaveable(this)` | ChestManager.cs:33 / TrainingSystem.cs:259 |
| `TrainingSystem.Instance.ResetState()` 编排行 | WorldLifecycle.cs:44 |
| `TryGetSaveable` | SaveManager.cs:182（定义）+ TrainingSystem.cs:185/191（消费）+ 容器 L373 |
| `_queues.Clear()` | TrainingSystem.cs LoadState 先清 + ResetState |
| `先清后建（M2 幂等）` | ChestManager.cs L181/200 |

---

## 六、git diff 自查（文件面）

**5 改**：ChestManager.cs(+71)/TrainingSystem.cs(+131)/SaveManager.cs(+15)/WorldLifecycle.cs(+1)/Valley_P1_Observer.cs(+4)　**3 新**：ChestSaveData.cs / TrainingSaveData.cs / Valley_HH109_Smoke_SaveDomain.cs（+.meta 各一）
全部在 M4 界内；**Building.cs 零改动**；多文件编辑全程单线程串行+每笔即验（L-12）。

---

## 七、报告点：测试环境口径事故+裁决请求（用户点名报策划）

**事故实录**：容器初版按 HH.107 先例走 `SmokeApi.EnterGame`（真实玩家局）**未走测试环境入口**——P2 清场复刻（TeardownScene）清光玩家单位 → ThroneAnchor（考跑守卫未开）轮询 IsKingdomLost（玩家桶 0 工人全灭）→ **GameOver「河谷失守」**→ GameOverPanel 强制 timeScale=0 → 容器协程冻结 → QuitSmoke 未执行 → 会话污染（游戏空转至 128 天，用户现场目击）。

**处置**：容器已改走 `TestHarnessApi.EnterTestRun`（考跑守卫全开：ThroneAnchor 判负封死/全怪源静默/补员静默/sim 日判豁免/战斗降速豁免），复跑后 148 天局零判负、P1~P4 全绿。**HH.92 测试环境（D552 T0~T19）本体健在且有效**——事故根因是执行端未走正门，非测试环境缺失。

**裁决请求（三项）**：
1. **玩家缺席语义分型**：T10 幽灵化（无玩家模式）适用于 AI 行为观察类探针；**存读档类探针（训练/箱拾取=玩家侧系统行为级验证）需要玩家实体在场**——本批采用「考跑守卫封判负+保留玩家实体」口径。请裁决该分型口径是否成立/是否升为容器模板纪律。
2. **容器入口纪律**：凡动 GameState/清场/单位生命周期的容器是否一律强制 `EnterTestRun`（本次事故=HH.107 先例口径的家族性风险，HH.107 容器窗口短侥幸未触发）。若成立，HH.107 容器是否补改列六考观察。
3. **GameOverPanel timeScale=0 的容器防御**：判负面板弹出的 timeScale=0 会冻结一切 scaled 等待——容器侧已用考跑守卫规避，产品侧无需改动（确认口径）。

**正面产出**：容器新增「协程异常捕获器」（Application.logMessageReceived 挂 Exception→显式落日志）——Unity 协程内异常静默终止不留痕，本批靠它定位到反射 NRE 卡点（QueueSnap L606 在错误类型上 GetField），建议入教训库（L-15 候选：**协程探针必须挂异常捕获器，静默死亡=假卡死假超时**）。

---

## 八、列报（策划端验收裁决项）

1. **SaveLoadPhase 选型**：ChestManager=Scene（箱子=场景动态对象，参照 Building/单位先例；GridSystem/TimeManager Global 先恢复，CoordToWorld/过期锚点可用）；TrainingSystem=Scene（M2 硬条款：建筑阶段 1.5 先重建→队列阶段 2 后恢复）。
2. **MapGenerated 宝箱幂等核对结论**：地图生成**不往 ChestManager 写入任何 ChestEntity**——SpawnChest 全工程仅 3 调用方（DamageSystem.L632 兽人战利品/TreasureVault.L133 溢出装箱/MonsterController.L67 怪物掉落），均运行时行为；GridTypes.TreasureBox tile 无实体化代码（仅 BuildingVisual sprite 映射）；读档链（ContinueFromSave）不重跑 InstantiateFromMap → **无双份风险**。落地方案=LoadState 先 ClearAll 后重建（幂等，兼防 Singleton DDOL 跨局残留）。
3. **清场泄漏修选型=方案 B**（WorldLifecycle 编排补 ResetState）：不选 Building.OnDestroy 的理由——①Die→Destroy→OnDestroy 双触发面（Die 已调 OnBuildingDestroyed）②清场语义归属编排层（HH.93 T13 家族一致）③Building.cs 零改动（M4 协调点解除）。
4. **SaveManager.TryGetSaveable 支撑件**：任务书外新增公开方法（TrainingSystem 反查建筑/单位必需）；只增不改（M1）。
5. **payload version=1 新条目零 bump**：全局 saveVersion=3 未动；旧档无本条目→分发跳过→零兼容负担。
6. **单位丢失条目处理**：LoadState 反查不到 unit（旧档读档过滤 P5.3 D432/单位已亡）→条目作废 LogWarning+**不退款**（D564「入档优于丢失退款」口径；退款链未定义，挂账）。
7. **SaveState 死键过滤自愈**：building fake-null/unit 死引用不入档——旧档残留死键（若有）首次存档洗掉。
8. **TrainingDef 反查不重验门禁**：存档态即合法态，重验会吞档（如升级后建筑降级的极端序）。
9. **2_20 首跑 ②d FAIL**：受击竞态噪声（L-14），复跑 ALL PASS；②d 语义面（野性/受击/NPCBrain 焦点）与本批文件零交集。

## 九、教训核查行

- L-01（AI 消费端断链）：本批为存档域，无新增 AI 消费面——不触发。
- L-02/M3（落盘幻觉）：grep 双锚点+git diff+L-13 双通道全程执行 ✅（本会话一次非 Play 态 Singleton.Instance 探活触发 DDOL 自建被即时识别纠正）。
- L-05（白名单盲区）：[ChestManager] 新 tag 已补白名单 ✅。
- L-06（裸构）：箱/训练/居民全走生产链（SpawnChest/TryTrain/SpawnUnit）✅。
- L-09（考跑加速）：改用 TestHarnessApi timeScale 直通，脱离 SetGameSpeed 四档吸附/降速钳制风险面 ✅。
- L-11（生命周期多轮）：P3 三轮建拆建+第 3 轮判定口径 ✅。
- L-12（同文件并行编辑）：全程串行+即验 ✅。
- L-13（Play 态双通道）：编译声明=静态锚点+execute_code 探活双通道 ✅。
- L-14（探针噪声）：P4 首跑 ②d FAIL 按「复跑取存在性证据」处置，非卡验收 ✅。
- **新增候选 L-15**（协程异常捕获器，§七正面产出）+**L-16 候选**（容器入口纪律：动 GameState/清场/单位生命周期的容器强制 EnterTestRun——本批判负事故根因，同类问题 HH.107 属侥幸未触发）。

## 十、账本回执

- HH.109 → 施工完毕待验收（本报告落盘后回写账本）
- HH.110 → 🔵已落盘（本报告）
- 待策划端验收销号；验收通过后 git commit 走 git-plan-sync。
