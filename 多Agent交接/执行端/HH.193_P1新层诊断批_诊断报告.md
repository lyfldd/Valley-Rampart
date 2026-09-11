# HH.193｜P1 新层诊断批 · 建军链（⑦⑯⑰）被选中面诊断报告

> 执行端（TraeCode·Unity 轨）· 2026-09-11
> 状态：✅ 已验收（D654，2026-09-11 主策划端；根因＝⑦ 选中即空转／⑰ need 恒占位；立建军链修复批）
> 锚点：**HH.190 任务书（D649）** ｜ **HH.192 开工回执（D651 裁）** ｜ 背景＝HH.189（D648）
> 性质：**只读诊断 ＋ 观测域增量**；**业务代码零改动**
> **一句话结论**：**⑦ 不是"不被选中"，而是"选中即空转"**——诊断局 census `top=RecruitWarrior` **38 次（居首）**，但 `⑦招战士落地＝0`；根因＝**⑦ 执行面建筑前置（批B 引入）↔ ⑰ 建兵营评分面用 `ExclusiveGap` 占位（与"缺兵营"无关）二者咬合失败** ⇒ 建军链**结构性死锁**（D648「结构性不可达」成立，且**载体是"空转"而非"零选中"**）。

---

## 一、T1 观测口补口（交付 ＋ 修证）

### 1.1 交付件（全部 Editor-only 观测域）

| 文件 | 性质 | 内容 |
|---|---|---|
| `Assets/Editor/Smoke/Valley_DiagMilitary.cs` | **新增** | 只读反射探针：`NeedScore` 公开调用；`Feasible`／`CountProductionOf`／`DecideTriage` **反射只读**；订阅 `DaySettledEvent` 逐日输出六 tag |
| `Assets/Editor/Smoke/Valley_HH80_DiagRun.cs` | **新增** | 短程诊断容器：正门 `EnterTestRun` ＋ `SEED=48903` ＋ `SLOT=p1_diag` ＋ D30 窗口 ＋ 双 fail-fast（观测器／探针未启即中止）＋ `hh80_diag_status.log`（独立名，不覆盖七考档） |
| `Assets/Editor/Smoke/Valley_P1_Observer.cs` | **改**（白名单 +7 tag） | 加 `[P1观察]`（修勘正(b)根因）＋ `[DiagMilitary]`/`[DiagTriage]`/`[DiagCapacity]`/`[DiagBias]`/`[DiagChain]`/`[TreasureVault]` |

**只加不减**：白名单原 31 tag 全保留（集中管理口径不变，D563②）。

### 1.2 修证（对 HH.189 §二④ 勘正的根治）

- 七考镜像「检查点」＝**0**（白名单未含 `[P1观察]`）；**本局＝7 行**，样例：
  `[D5][Log] [P1观察] 检查点 D5: p1_run7_day005=True 回存p1_run7=True`
- ⇒ **T1 首项目标（让检查点可判读）达成**；且该行**当场暴露了下面 §五 的槽覆盖事故**（跑局槽是 `p1_diag`，回存却写 `p1_run7`）。

### 1.3 不可达项（如实列报）

- `[TreasureVault]` **探针行 0 命中**：`TreasureVault.Instance` 在推理期为 `null`（仅 `国库就绪` 行存在）⇒ **AI/主城 Vault 积压量仍不可判读**（与 HH.189 §六-2 一致，本次**未能补齐**，需另裁 API 面）。
- `[DiagBias]`：`ResourceBiasConfig` 类型未在 `Assembly-CSharp` 命中 ⇒ 按 D651「反射不可达⇒列报」处理（派工活权重**仍不可判读**）。

## 二、T2 历史对照（必答：为何旧局能产兵、七考不能）

**答：代码版本不同——09-07 局跑于批B 之前，那时 ⑦ 是"工人直转 Warrior"，无建筑前置。**

| 项 | `20260907_090903`（能产兵） | 七考 `20260911_142147` / 诊断局（不能） |
|---|---|---|
| 时代 | **批B 前**（批A `fb65eb2`＝09-09 19:56／批B `32396c7`＝**09-09 22:30**） | 批B 后 |
| ⑦ 执行路径 | **`ExecuteRecruitWarrior` 直转**（`w.SetOccupation(Warrior)`，[L854-879](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L854-L879)） | **`ExecuteRecruitArmy` 双环选招＋建筑前置**（[L890-921](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L890-L921)） |
| 实盘 | k2≥4（D69 起恒 4）／k4 达 4（D78-111）；**发育期即产兵** | **warrior 峰值＝0**（4 AI 全程） |
| 路由 | — | [L581-582](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L581-L582) `case RecruitWarrior → ExecuteRecruitArmy`；**旧直转方法现为死码** |

⇒ **"可达但脆弱"的精确形态＝批B 换路径后失去可用性**（09-07 达 4 → 09-09 退 3 → 七考 0，与 D651 §二 一致）。

## 三、T3 三面取证（核心）

### 3.1 ⑦ 招战士（`UtilityAction.RecruitWarrior`，id 7）

| 面 | 证据（`file:line` 实读 ＋ 实盘读数 @诊断局 D29） |
|---|---|
| **①评分面** | `minStage=Develop`／`need=RecruitWarriorGap(8)`／`needA=8`／`stageWeight=[0.5,1,1,1]`（`UtilityActionConfig.asset:130-145`）。实盘 **`need=1.000`**（恒满）、`axis=1×personality[0]`（0.187~1.000）、`stageW=1.00` ⇒ **`score=0.187~1.000`**；**census `top=RecruitWarrior` 38 次（全场第一）** |
| **②前置面** | `Feasible`（[UtilityScorer.cs:412-419](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L412-L419)）只查 金≥20／粮≥4／工>0／战士<target ⇒ 实盘 **`feasible=True`**（**未镜像建筑前置**） |
| **③可行性面** | **执行侧另有建筑硬前置**：[KingdomBrain.cs:913-914](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L913-L914) `FindKingdomBuilding(...)` 为 null ⇒ `continue`；[L918-921](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L918-L921) `candidates.Count==0` ⇒ **早退空转**。实盘 **`⑦招战士落地 = 0`**（30 日） |

### 3.2 ⑰a 建兵营（id 23）／⑰b 建训练营（id 24）

| 面 | 证据 |
|---|---|
| **①评分面** | `minStage=Expand`／**`need=ExclusiveGap(17)`**／`needA=1`／`stageWeight=[0,0.3,0.5,1]`（`UtilityActionConfig.asset:434-451`）；`ExclusiveGap` 实装（[UtilityScorer.cs:254-255](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L254-L255)）＝`HasExclusiveBuilding(k.id, buildingId) ? 0 : 0.5` ⇒ **与"是否缺兵营"无关的恒定占位 0.5**。实盘 `need=0.500／score=0.047~0.250`（**恒低于 ⑦ 的 0.187~1.000**） |
| **②前置面** | `Feasible` 建造通用支（[UtilityScorer.cs:462-483](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L462-L483)）：族门禁／uniquePerKingdom／`buildTargetCap`／逐项国库 ⇒ **实盘 k3 `feasible=True`，但 k1/k2 `feasible=False`**（金/石/木不足） |
| **③可行性面** | **[DiagCapacity] 实盘 k1 D30：`Stone:prod=0/in=0/out=0`（石料产能=0）**；建兵营 `cost=20金/10石/15木` ⇒ **石料枯竭国结构性不可行**；且 `buildTargetCap=1` |

### 3.3 ⑯ 训练将军（id 22）

- `minStage=Military`（`UtilityActionConfig.asset:415-428`）⇒ 实盘 `stageGateBlocked=True`（**设计如此**，下游行动，非缺陷）；`need=GeneralGap`（实盘 1.049，含 `InternalDrive` 内源势能）。

### 3.4 小结（压制关系）

**不是"⑦ 被其他行动压制"**（⑦ 恰恰是全场选中最多者）；**而是 ⑦ 选中后无产出、⑰ 上不来**：⑰ 恒定 0.5 占位分 × stageW 0.5 ＝ 0.25 上限，被 ⑨墙／②仓库／③产能等**真实缺口驱动**行动（need 可达 1.0）长期压制；即便⑰偶尔胜出，石料枯竭国亦不可行 ⇒ **兵营永不落地 ⇒ ⑦ 永远 candidates=0**。

## 四、T3-T4：wall 死循环共因验证（**未复现**）

- 七考：`建造焦点选址失败` **181 次全为 wall**；**诊断局：wall 仅 4 次，farm 6／quarry 4／Warehouse 2**（总计 16）⇒ **"wall 选址死循环"是七考局领地几何特定（种子/布局相关），非结构性共因**。
- 诊断局真实焦点占用形态＝**⑦ 空转 38 次**（census top 居首）＋ wall 13／Warehouse 14。
- ⇒ **T4 结论：原"共因"假设不成立**（据实修正）；放大因子应改判为 **⑦ 自身空转**。

## 五、T5 迁移条件 vs 2_22 军事 P0 设计稿

| 项 | 设计稿（`2_22_AI王国脑全景补全.md`） | 实现 | 判定 |
|---|---|---|---|
| 迁移条件 | L36「**战士≥4＋人口≥12**」 | `expandToMilitary_warriorsMin=4`／`populationMin=12`（`ScriptStageMachine.cs:96-101`） | ✅ **一致** |
| ⑦ 可见性 | L245「⑦⑧**发育期起可见**、门控删除」 | `minStage=Develop` | ✅ 一致 |
| ⑦ 语义 | L92「⑦招战士**扩为招兵**（双环权重，不再是 Warrior 直转）」 | `ExecuteRecruitArmy` 双环选招 | ✅ 一致 |
| **⑰ 需求口径** | L106「⑰建军事建筑（**选型=种族＋快照缺口**，选址走 §3.7 打分器）」 | **`ExclusiveGap` 专属建筑占位（恒定 0.5）** | ❌ **实现层偏离设计稿**（应按"缺什么军事建筑"的**快照缺口**驱动） |
| 断链目标 | L35「配兵三断点…不建军事建筑→无兵种生产入口」＝P0 要**打通** | 结果：从"直转可用但无兵种"退化为"**双环+建筑前置后完全不可用**" | ❌ **功能退化** |

## 六、根因结论（死锁链）

```
⑦ 需训练建筑在前(批B 建筑前置, L913-914)
        ↑
        │  要建 → ⑰ 但 ⑰ 评分 need = ExclusiveGap 占位 0.5（恒定，不反映"缺兵营"）
        │        ⇒ score ≤0.25，被 真实缺口行动(wall/warehouse/capacity need→1.0) 压制
        │        ⇒ 且石料枯竭国(Stone:prod=0)连成本门都过不了
        └── 兵营永不落地 ⇒ ⑦ candidates=0 ⇒ 空转(38/30日) ⇒ warrior 恒 0
                                                          ⇒ 军事期硬条件(≥4) 不可达 ⇒ 熔断
```
**性质**：**二行动"咬合"名义成立（批B 注释即写"由 ⑰ 缺口评分导向先建——两行动自然咬合"），但 ⑰ 的需求侧未按缺口实现 ⇒ 咬合空转**。批B 验收探针「⑦训练营缺席不空转（咬合设计生效）」**只验了 ⑦ 不空转的一半，未验 ⑰ 会被选中 ⇒ 验收盲区**（据此立 L-31 候选：<u>双向咬合必须双端各验一次</u>，详见 §九）。

## 七、建议方向（**不裁断**，供策划端决策）

1. **主项（结构）**：把 ⑰ 的 need 从 `ExclusiveGap` 占位改为**真实缺口**（对齐设计稿 L106「快照缺口」口径）——例如按「本国缺军事建筑种类数」驱动（`UnitTypeGap`/`FormationGap` 已读快照的同族做法）；使 ⑰ 在缺兵营时 need 可竞争过 wall。
2. **辅项（一致性）**：⑦ 的 `Feasible` 是否镜像建筑前置——保持现状（不镜像）会产生"选中即空转"的焦点浪费；镜像则与 ⑰ 咬合更紧。**两面各有代价，请裁**。
3. **资源面**：石料产能为 0 的国家（`Stone:prod=0`）应另有补石通道（与 2_23 资源 P0 断链自愈衔接）——**观察级线索，非本批结论**。
4. **禁参数微调找补**：`warriorsMin`／`④stageWeight` 等**数值不动**（D563③）；本批证据支持"结构改"而非"调参"。

## 八、不可判读项（如实列报）

| 项 | 原因 | 状态 |
|---|---|---|
| Vault 积压（D569/D579） | `TreasureVault.Instance` 为 null ⇒ 探针无效 | ❌ 仍不可判读（需 API 面另裁） |
| 派工活权重（R-B1） | `ResourceBiasConfig` 反射未命中 | ❌ 仍不可判读 |
| ⑤三通道分诊／断链自愈（工作侧） | `[DiagTriage]`/`[DiagChain]` **已可读**（样例 `k1 Gold=NoOp Stone=BuildCapacity …`／`k3 missingMustHave={kind=0 req=3 act=1}`），但**仅读到"结果"**，未读到 TaskScheduler 侧"派工活权重"过程 | 🟡 部分可判读（结果面✓／过程面✗） |
| ③产能种类化 | `[DiagCapacity]` 已可读（`Gold:prod=1/Food:prod=3/Stone:prod=0`） | ✅ 已补上 |

## 九、列报（请裁）

1. 🔴 **槽覆盖事故（观测域副作用，须列报）**：观测器 `MainSlot` **硬编码 `p1_run7`**（不随跑局容器 SLOT 变化）⇒ 诊断跑 D5~D30 的检查点写入 **`p1_run7_day005~030.json` ＋ 回存 `p1_run7.json` 主槽**（实证：`[P1观察] 检查点 D5: p1_run7_day005=True 回存p1_run7=True`）。
   - **影响面**：七考 **D5~D30 检查点存档 ＋ 主槽** 被覆盖（mtime 16:03~16:13）；**七考 D35~D120 检查点完好**（14:35~15:09）。
   - **判定影响**：**无**——HH.189 的判定证据是 **CSV／镜像日志／收工状态档**（均完好，且 CSV 按 session 分列未混）。
   - **建议**：观测器槽位改为「随当前跑局槽」（观测域增量，另立批）；本批**未擅动**（避免二次扰动）。
2. 🟡 **`[P1观察]` tag 入白名单**＝观测域增量（不改业务），已随本批落地并**当场修证**（0→7 行）。
3. 🟡 **`hh80_run_status.log` 同名覆盖**（HH.189 §六-3 已列）在本批未再触发（诊断容器用独立名 `hh80_diag_status.log`）——**建议沿用该做法**改七考容器（另批）。
4. 🟡 **L-31 候选教训**：「**双向咬合必须双端各验一次**」——批B 只验「⑦ 不空转」，未验「⑰ 会被选中」⇒ 咬合假设未被完整证伪。请裁是否入教训库。
5. 🟡 **T4 假设修正**：七考「wall 死循环 181 次」**未在独立种子复现**（wall 4 次／farm 6 次）⇒ 判为几何特定，**不作共因**。
6. ✅ **与 D652 同类扫描 🔴B1 的交叉核验（本批实盘否证）**：并行报告疑「`militaryTargetFloor` asset 未序列化 ⇒ ⑦ `Feasible` 恒 false（"⑦零触发"直接根因）」。**本批实盘读数明确否证该猜想**——诊断局三 AI 全程 `militaryTarget=**3**`（＝floor 2＋扩张期系数 1，`D348Target` 正常返回）且 `⑦ feasible=**True**`（`feasibleReachable=True` 即反射调用成功）；⑦ 的真实形态是 **`feasible=True` 但执行空转**（§三/§六）。⇒ 请策划端据此**销 B1 猜想的"直接根因"定位**（`militaryTargetFloor` 是否有序列化卫生问题可另行立项，但**不是**⑦零触发的因）。
7. 🟡 **⑦ 焦点浪费量化（供"是否镜像建筑前置"决策）**：诊断局 30 日中 ⑦ 夺取焦点 **38 次**（占可判读 census 行的最大份额），其中 **100% 空转**（落地 0）⇒ 若无其他机制兜底，**该行动每日都在消耗一次决策机会**（本局未见连带恶化，但值得在辅项权衡中计入）。

## 十、红线自检与门禁

- ✅ **业务代码零改动**：`Assets/_Game`（产品代码／SO／场景）**0 diff**；AI.Core **零直改**（未触及）
- ✅ 探针**只读**（`NeedScore` 公开调用；`Feasible`/`CountProductionOf`/`DecideTriage` 反射只读；**无写状态**）
- ✅ **禁参数微调找补**：本批未改任何数值
- ✅ 正门 `TestHarnessApi.EnterTestRun`（日志实证：`考跑模式 ON：timeScale 直通 → 15x`）＋ **15x 不向上**／禁直接设 timeScale（L-09）
- ✅ **L-29 阳性对照**：本批两次（`48903` 零命中 ← 对照 `69496` 命中等；`HH.193` 零命中 ← 对照 `HH.19[23]` 命中 7 文件）
- ✅ 收尾：停探针 → 停观测 → **退 Play（playMode=stopped）**；`Logs/P1` **不入库**
- **门禁**：编译 **0 错**（6 警全为既有 Editor 基线，本批 4 文件**零新警**）；诊断容器跑通（D30 收工，存盘 `p1_diag=True`）；**业务代码零改动**（`git status Assets` 仅 3 观测域文件＋2 `.meta`）

## 十一、证据清单

| 证据 | 路径／读数 |
|---|---|
| 诊断局镜像 | `Logs/P1/p1_log_20260911_160131.log`（D30·seed 48903·槽 p1_diag；六 tag 逐日可读） |
| 收工状态 | `Logs/P1/hh80_diag_status.log`（`诊断窗口到期 @D30`，`存盘 p1_diag=True`，16:13:09） |
| 日快照 | `Logs/P1/p1_snap.csv`（session `20260911_160131`，D1~D30，k1~k3 与 -1 行） |
| 关键读数 | census top：⑦38／Warehouse14／Wall13／Worker7／None6／Harvest4／Capacity2；`⑦落地=0`；落地建筑 wall6/Warehouse4/farm3/quarry2（**无 Barracks/TrainingCamp**） |
| T2 基线 | `Logs/P1/p1_snap.csv` session `20260907_090903`（k2/k4 warrior 达 4，D69+） |
| 侦察／七考档 | `hh80_scout_result.log`／`hh80_run_status.log`（p1_run7 熔断） |

## 十二、回写与提交

- 取号：`b13436d`（HH.192→HH.193 原子化，D640 #10）
- 本报告 ＋ 索引 HH.193 行 ＋ 队列「P1新层诊断批」行 ＋ 主计划书工作日志插行
- git 面：**业务代码零改动**；观测域 3 文件（2 新增＋1 改）＋ `meta`，随本串 commit；**未 push**

---

## 十三、策划裁决（主策划端 · D654，2026-09-11）

D654（HH.193 建军链诊断报告）。**判据三直读**：`KingdomBrain.cs:913-914`（⑦ 执行侧建筑前置：缺建筑⇒candidates=0 早退）／`UtilityScorer.cs:254-255`（⑰ 建兵营 need=ExclusiveGap 占位 **0.5** 恒定）／`ExecuteRecruitArmy`（批B `32396c7` 换掉 ⑦ 旧直转 `ExecuteRecruitWarrior`）／census 实盘（⑦ top=38 但落地 0）。**判决：验收成立**。**根因认定（精确定位）**：⑦ **不是"不被选中"，而是"选中即空转"**——死锁链＝`⑰ need 恒 0.5（ExclusiveGap 占位、与"缺兵营"无关）⇒ score≤0.25 被真实缺口行动(need→1.0)长期压制 ⇒ 兵营永不落地 ⇒ ⑦ 建筑前置失败 ⇒ 空转 ⇒ warrior 恒 0 ⇒ 军事期硬条件(≥4)不可达`。**回归点已定位**＝批B（`32396c7`）把 ⑦ 由 `ExecuteRecruitWarrior`（工人直转、无建筑前置）换成 `ExecuteRecruitArmy`（双环+建筑前置），而设计稿 §⑰「选型=种族＋快照缺口」(L106) **未落实** ⇒ 旧直转路径成死码、新路径建筑前置无 ⑰ 承接 ⇒ **实现层背离设计稿（T5）**。**T2 必答已答**＝09-07 能产兵＝该局跑于批B 之前（旧直转路径）；**批B 引入的真实行为回归**。**🔴 交叉核验＝D652 的 B1 被实盘否证**：`militaryTarget=3`／`feasible=True` ⇒ **不是**「`militaryTargetFloor` 未序列化致 ⑦ Feasible 恒 false」⇒ **勘正 D652 分流表 B1 行的根因表述**（保留 B1 为"子策划假设·已被 HH.193 实盘否证"）。**T4 假设修正**＝七考 wall 181 未复现（本局 wall 4）⇒ **几何特定、非结构性共因** ⇒ 勘正 D648「放大项疑共因」表述。**裁**：①**立「建军链修复批」**——主修＝**⑰ need 改真实缺口**（弃 ExclusiveGap 占位 0.5，按设计稿 L106「种族＋快照缺口」）；辅修＝**⑦ Feasible 镜像建筑前置**（消除空转；策划端裁：**须镜像**，否则永远"选中即空转"）；**禁参数微调找补**（D563③）；**必带回归探针**＝同 seed 短局证「兵营落地＋⑦落地＋warrior>0」；红线＝若触及决策核（UtilityScorer/KingdomBrain）须核 `sim-sync` 同源义务。②**列报①槽覆盖事故** ⇒ 立 🔴：观测器 `MainSlot` 硬编码 `p1_run7` ⇒ 诊断跑覆盖 `p1_run7_day005~030.json`＋主槽（七考 D35~D120 与判定证据不受损＝非致命）；**定性＝观测域缺陷＋签发未预见（策划侧）**；处置＝观测器 **MainSlot 参数化**（并入修复批）。③**列报③ L-31 候选教训 ⇒ 采纳入库**。④余列报（T1 观测口兑现／T3 三面／T5 设计稿偏离）知悉。⑤**B1 否证** ⇒ 勘正 D652（见上）＋ `DZ-096` 描述勘正。**验收三问**：①发生＝**批B 实现变更（⑦ 换 `ExecuteRecruitArmy`）未同步落实 §⑰（设计稿 L106）⇒ 新建筑前置无承接** ⇒ **策划侧**（批B 验收盲区：D638 只验"⑦ 不空转/E-B5"、**未验"⑰ 会被选中"**）②策划侧 ③**缺条目 ⇒ 新增 `L-31`「双向咬合必须双端各验一次」**（采纳执行端候选）。**嘉奖**＝①census 精确定位（⑦ top=38 落地 0）②**实盘否证 D652 的 B1**（跨批交叉核验）③**T4 自我修正**（wall 未复现）④**T2 精确定位回归点**（批B `32396c7`）——顶级正面样本。