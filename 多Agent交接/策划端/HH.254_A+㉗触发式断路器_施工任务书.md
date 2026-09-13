# HH.254 A+ 施工批「㉗ 触发式断路器」（Gate=`G1-1`）

> 签发：主策划端｜2026-09-13｜**决策依据＝D700**（0.6 §二百二十九 · HH.253 验收）＋ D699 ＋ D690 ＋ D531①
> 前置必读：**HH.251 任务书＋HH.252 回执＋HH.253 报告（§十 裁决区）** 全链逐字 ＋ `FocusController.cs` 全文 ＋ `ActionBackoff` ＋ 台账 `DZ-148` 三行
> 本批相关教训引用：**L-30（§0 全链门 gap 表＝强制勾选）**／L-21／L-31／L-35
> 教训核查：无其他命中

---

## 〇、一句话目标

把 ㉗ 接管从「评分层入口」（HH.253 已落·已证）升格为**常设底线级触发式断路器**：断供∧通道A∧③不可行∧该 rt 无在册源 ⇒ **当日强制 `SetFocus(27)`（跳防抖）派发**——消除「入口已开、落地未达」的最后一环（3 日防抖吞 one-shot 窗口），`DZ-148` 验收句（石场景 ㉗ 可派发）就此可达。

## 一、设计语义（D700 裁 A·用户拍板）

1. **位序（D700 授权改 D322 底线序）**：`FocusController.Update` 常设底线段内插入——**粮警（grainAlarm）之后、人口（popAlarm）之前**。新序＝粮 → ㉗断路器 → 人口 → 被攻。插入属 D322 红线序变更，授权出处＝本任务书（D700）；执行端开工回执须显式复述。
2. **触发条件（全 AND·同源判据，L-31 禁另造）**：
   ① `ResolveTriageResource(eco, dcfg)` 有断供 r；② `TryMapWorldResource(r)`＝石/木；③ `DecideTriage(eco, dcfg, r) == BuildCapacity`（通道A）；④ `UtilityScorer.Feasible(kingdom, capDef) == false`（同源 HH.253 已 internal 化的 `Feasible`）；⑤ **该 rt 本国无在册源**（one-shot 自限——注册一次后条件⑤不再成立，天然退避）；⑥ 工人>0 预检（workerCount==0 派发无工人可派＝空转，口径见 M4）。
3. **行为**：条件全成立 ⇒ `SetFocus(kingdom, (int)UtilityAction.GatherWorldResource, day)`（底线路径同款·跳防抖）⇒ 当日 `ExecuteFocus` 路由 `ExecuteWorldGatherFocus` ⇒ 走 **HH.253 已落的接管分支**（同源判据自动成立）⇒ `Advertise` 派发。**复用而非复制**：触发判据与接管分支判据同一组函数，禁出现第二套可行性/通道判定。
4. **护栏**：Env（领土能注册候选=0）⇒ 既有 `ReportActionFail(Env)`＋`ActionBackoff` 护栏（HH.228/D680 口径）防每日空触发；触发打点与「㉗接管」打点分径（M8）。
5. **双通道并存**：HH.253 评分层入口放宽**保留不动**——argmax 日 ㉗ 仍可按评分竞争胜出；触发路径只是绕过竞争，不删除竞争路径。

## 二、§0 L-30 全链门 gap 表（强制勾选·随开工回执落盘）

| 门环 | 需要的证据 | 当前证据 | gap |
|---|---|---|---|
| 条件成立日存在 | 断供∧通道A∧③不可行∧无在册源的天 ≥1 | HH.253 实证 D7/D8/D13（k4）三日；「无在册源」首日必然成立 | ✅已闭 |
| 候选入池（旁证） | ㉗ need>0 且世界有候选 | 跑次 C need=1.000 已证（触发路径不依赖入池） | ✅已闭 |
| 焦点切换 | 触发日 `kingdom.focus==27` | 底线路径 `SetFocus` 直写、跳防抖（既有代码路径）——施工后由打点证明 | 🔜施工后实证 |
| 执行路由 | `ExecuteFocus` case 27 | `KingdomBrain.cs:630-632` 唯一调用点在场 | ✅已闭 |
| 接管分支放行 | 通道A∧③不可行 ⇒ 不 return | HH.253 已落（diff 实读） | ✅已闭 |
| Advertise 派发 | 领土内候选>0 | Env 阻断=0（三跑次）；候选 77~89 | ✅已闭 |
| 观测口径 | 「㉗采集下发：Stone」日志 | HH.249 正则已在场 | ✅已闭 |

## 三、排雷图（M1~M10）

- **M1** §0 gap 表逐环勾选；任何环 gap 未闭 → 先报裁再施工。
- **M2** 底线序插入＝D322 红线序变更：开工回执显式声明授权出处（D700）；2_17 步骤9 焦点模型文档的底线序描述漂移 → **列报**（文档修订归策划端/文档端，执行端不代改）。
- **M3** per-rt 在册源查询 API 核查：`WorldGatherRegistry.CountOf(kingdomId)` 不分 rt ⇒ 条件⑤需 per-rt 口径——开工回执**先报可用 API 实况**（`HasCandidate`/内部分桶结构）；若无，新增**只读查询方法**于 `WorldGatherRegistry`（门面内，禁 AI.Core）。
- **M4** Env 护栏二选一（回执列报后施工）：(a) 触发条件加「领土内有该 rt 候选」预检（需只读候选计数）／(b) 容忍白焦点日＋既有 Env 退避。`workerCount==0` 预检口径一并定。
- **M5** 新旧输入集一致性（D695）：底线序插入后原三级行为逐案穷举（粮警日/人口日/被攻日/普通评分日 × 触发条件成立/不成立）——**触发条件不成立时零变化**为硬断言。
- **M6** 回归面：HH.253 两处改动（评分入口＋让位分支）**保留零再改**；`DecideTriage` 纯函数／`census top=` 结构／③ 本体／数据 SO／sim／AI.Core 零改。
- **M7** 时序：确认 `FocusController.Update`（日 tick 焦点段）先于 `ExecuteFocus`（执行段）同日生效（既有结构即如此，回执复述即可）。
- **M8** 观测打点：触发日加只读日志 `[KingdomBrain] kX ㉗断路器触发：{rt}（通道A∧③不可行∧无在册源）`；与「㉗接管」（接管入径）、「㉗采集下发」（派发落地）三分径。
- **M9** 验收复跑：**同 seed 73621 优先**（D520/D556 隔离变量先例；跑次标注 D694②）；新 seed 后备（HH.251 线1 原文容许）。判定＝**石场景「㉗采集下发：Stone」首达即停**（命中即停 L-34）；D90 熔断。
- **M10** 禁改清单：champion/Holdout/harness 共享套件/AGENTS.md；`UtilityActionConfig`/`KingdomDiagnosisConfig` 数据零改；评分入口与让位分支（HH.253 态）零再改。

## 四、范围（不得越界）

- ✅ 改：`FocusController.Update`（触发级插入＋触发判据，判据同源复用 `ResolveTriageResource`/`DecideTriage`/`UtilityScorer.Feasible`）；视 M3/M4 需要在 `WorldGatherRegistry` 增**只读**查询；只读打点 1 条。
- ❌ 不改：HH.253 已落两处／`DecideTriage`/`Feasible` 语义／`census top=` 结构／③ 本体／数据 SO／sim／训练仓／AI.Core。

## 五、红线段位

- sim-sync 核查结论先行（`FocusController`/`KingdomBrain` 属 `_Game`；harness 无镜像 ⇒ 预期零义务，回执正式复述）。
- 正门 `EnterTestRun`＋收尾真暂停+Save+`ExitTestRun`+退 Play（L-32）。
- 新旧输入集一致性核验（D695）／跑次标注（D694②）／§8.6 四列齐。
- 写-改-commit 同串、只提本批文件、不 push；完成报告按账本实时水位线取号（D640 #10 禁预留）。

## 六、验收线

| # | 线 | 判法 |
|---|---|---|
| 1 | 石场景落地 | 同 seed 复跑：条件日成立 ⇒ 当日「㉗采集下发：Stone」首达（「㉗断路器触发」打点佐证入径）——命中即停 |
| 2 | one-shot 自限 | 全窗每 rt 触发打点 ≤1 次；注册后条件⑤不再成立 ⇒ 无重复触发/重复派发 |
| 3 | 原行为回归 | ①三底线日（粮/人口/被攻）行为不变 ②触发条件不成立日＝HH.253 态逐位不变（argmax 竞争/防抖照旧）③通道B 木路径照旧（Wood 派发在场） |
| 4 | 零改动面 | `Assets/_Game/**` 仅本批内文件（FocusController±WorldGatherRegistry 只读查询）；AI.Core/sim 零触碰 |
| 5 | 编译 | 0 error（存量分离） |
| 6 | 教训兑现 | §0 gap 表勾选落盘＋新旧输入集＋跑次标注＋四列齐 |

## 七、交付物

改 1~2 文件 ＋ 交付报告。报告必含：sim-sync 核查／§0 gap 表逐环证据／M3/M4 二选一列报／新旧输入集对照／同 seed 三跑次对照（HH.253 跑次 C＝基线）／回归三组证据。

---

> 签发：主策划端（D700，2026-09-13）｜执行端接单后先写开工回执（按水位线取号）。
> 关联：`DZ-148`（🟡施工半程→本批收口）／HH.251/252/253 链／D531①／L-21/L-30/L-31/L-35。
