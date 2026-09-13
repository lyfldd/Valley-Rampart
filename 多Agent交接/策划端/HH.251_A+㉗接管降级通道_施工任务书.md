# HH.251 A+ 施工批「㉗↔③ 双轨解耦·㉗ 接管降级通道」（Gate=G1-1 资源对等）

> 签发：主策划端（团结 Cowork）｜2026-09-13｜**Gate = `G1-1`（资源对等）**
> 决策依据：**D699**（0.6 §二百二十八 · HH.249 验收）＋ **D690**（0.6 §二百一十九　A+ 立批方案）＋ **D694**（探针口径修正）
> 前置（执行端必读）：**HH.244 任务书** ＋ **HH.249 交付报告**（探针结论）· **HH.240 任务书** · 《断链校验台_设计稿》§四 · 缺陷台账 **`DZ-148`**（D690/D694/D699 三行）

---

## 〇、一句话目标

**让 ㉗（即时采集）在其立项动机场景真正可用**：㉗ 现挂在**通道B**（有产能产出低），但其立项动机是**通道A（无产能）**——石场景因 `FindTriageDef(Stone)="quarry"` 非空恒走通道A ⇒ ㉗ 永久让位（结构性不可达，`DZ-148`）。本批做 **㉗↔③ 双轨解耦**：**通道A 且 `Feasible(③)==false` 时，㉗ 接管（降级通道）**——即无产能且建产能不可行时，直接派发 ㉗ 采世界资源，不空转。

## 一、由来与必要性（判据面已坐实·D699）

- HH.249（探针口径修正后）实测：㉗ 实际派发 **`Wood=2／Stone=0`**（同 seed 两跑次一致·D1~D60 全程）⇒ `DZ-148` **证成**（石场景结构性不可达坐实）。`DiagTriage Stone=BuildCapacity`（158/101 次）＝通道A 恒判定；**㉗ Env 阻断=0** ⇒ 排除「领土内无可采」竞争解释（世界给定物非根因，是**入口条件**结构性挡死）。
- 若不修：`G1-1`（资源对等）判据面口子**在石场景恒伪**（㉗ 不可达），A+ 批为 `G1-1` 收口路径必要一环。
- 教训引用：`L-21`（出口存在≠出口可达）、`L-35`（判据口径）、`L-30`（判定线硬条件）。
- 红线守卫：本批**只改 `ExecuteWorldGatherFocus`/`DecideTriage` 的让位分支**；不触碰 `census top=` 评分面、不触碰 ③ 本体逻辑、不触碰 sim。

## 二、方案（D690/DZ-148 既有裁决 · 本任务书细化）

### 修哪里（主体·2 处）

1. **`KingdomBrain.ExecuteWorldGatherFocus`（`:1348-1352`）**：现逻辑＝`DecideTriage(...) != NoOp ⇒ Bump(ok:true) return`（让位）。
   **改为**：让位前先判 `DecideTriage != NoOp` **且** `Feasible(③)==false`（即通道A 的建产能行动**实际不可行**——`BuildCapacity` feasible 恒伪）⇒ **㉗ 接管（降级通道）**，继续走下方 `Advertise` 派发（`Wood` 已有成功先例＝代码路径存在）。
   - guard 语义：`DecideTriage == NoOp`（原通道B 命中）→ 维持现状（㉗ 本就可达）；`DecideTriage != NoOp ∧ Feasible(③)==true` → 维持让位（可建则建，正确）；`DecideTriage != NoOp ∧ Feasible(③)==false` → **㉗ 接管**（本批新增）。
2. **`DecideTriage`（`:1380-1396` 纯函数·不要动其排序/返回）**：**不改**——ℹ 双轨解耦点放在调用方 `ExecuteWorldGatherFocus`（㉗ 的让位分支），保持 `DecideTriage` 纯函数语义与 `R-C4` 确定性断言不变。

### 判据/观测（可判定最早日＋命中即停·§8.6 四列）

| 列 | 内容 |
|---|---|
| **可判定最早日＋命中即停** | D5 起跑；判定＝石场景出现「无产能＋`Feasible(③)==false`」天 ⇒ 当日 ㉗ 接管派发日志首达即判；跑满 D90 未达＝❌不成立 |
| **服务哪条验收句** | `DZ-148` 验收句：石场景（通道A 且 ③ 不可行）㉗ **可派发**（不再永久让位） |
| **作用域** | 全批 any-国；限 `KingdomBrain` 让位分支；`Assets/_Game/Systems/AI/KingdomBrain/` 元数据区 |
| **口径来源与排除项** | 锚点＝`㉗采集下发：Stone` 日志（复用 HH.249 探针正则）；排除项＝Env 阻断（领土内无可采）／`Feasible(③)` 不可判定天／消费侧入账（`HarvestCarry` 无日志） |

## 三、范围（不得越界）

- ✅ 修：`KingdomBrain.ExecuteWorldGatherFocus` 让位分支（`DecideTriage != NoOp` 子句旁增 `Feasible(③)==false` 判定）。
- ✅ 加：若需，新增一个只读日志打点在让位分支（`㉗接管：通道A且③不可行`）以支撑探针判据（属 `_Game` 观测面，须与 HH.249 探针正则对齐或同步 ChainAuditSpec）。
- ❌ 不修：`UtilityScorer`/`census top=` 评分面、`BuildCapacity`（③）本体、`DecideTriage` 纯函数、sim/训练仓。
- ❌ 不扩：不改 `FindTriageDef`/`triageCapacityDefs` 数据（`KingdomDiagnosisConfig.cs`）——那是另一条路径（数据层），本批复用既有"通道A 让位条件"。

## 四、红线段位

- ⚠️ **`KingdomBrain` 属 `Assets/_Game/`（非 AI.Core）**——但**决策核 sim-sync 纪律照常适用**：凡改 `DecideTriage`/派发条件，开工前须核同源决策在 sim 是否有镜像副本（`sim-sync` skill）；**本次只改 Unity 侧让位分支**，应先给 sim-sync 核查结论（镜像无对应逻辑则零义务，有则须同步）。
- **正门进局**：TestHarnessApi.EnterTestRun（铁律1）＋ 收工真暂停+Save+ExitTestRun+退 Play（L-32）。
- **只提本批文件**：写-改-commit 同串、不 push；完成报告按账本实时水位线取号（D640 #10 禁预留）。
- **Param 纪律（D695 教训）**：改动点涉及决策核参数语义，交付报告须含「新旧输入集一致性」核验（新 guard 加入后，原让位路径是否仍覆盖全部原行为）。

## 五、验收线

| # | 线 | 判法 |
|---|---|---|
| 1 | 石场景可达 | 正门跑局 D60~：出现「无产能＋③不可行」天 ⇒ ㉗ 接管派发首达有日志（同 seed 或新 seed 均可，**须标跑次**） |
| 2 | 原行为回归 | ①`DecideTriage==NoOp`（通道B）天 ⇒ ㉗ 照旧可达；②`DecideTriage!=NoOp ∧ ③可行` 天 ⇒ 照旧让位不派发（保 ③ 优先权）；两组对照缺一不可 |
| 3 | 零改动面 | `Assets/_Game/**` 仅本批内文件（KingdomBrain 让位分支＋观测打点）；AI.Core/sim 零触碰；`census top=` 评分面零改 |
| 4 | 编译 | 0 error（含存量分离） |
| 5 | 教训兑现 | 报告含「新旧输入集一致性」核验（D695）＋跑次标注（D694 勘正②）＋四列齐（§8.6） |

## 六、交付物

- 改 1~2 文件（`KingdomBrain.cs` 让位分支 ± 观测打点）＋ 交付报告（按水位线取号）。
- 报告必含：sim-sync 核查结论／新旧输入集对照 / 石场景接管实证（含跑次标注）／回归对照（通道B＋③可行两组）。

---

> **签发：主策划端（D699，2026-09-13）｜执行端接单后先写开工回执（按水位线取号）。**
> 关联：`DZ-148`（✅判据面坐实→待施工→本批施工后收口）／`HH.244/HH.249`（探针链闭合）／`G1-1`。