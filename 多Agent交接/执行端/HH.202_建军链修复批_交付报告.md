# HH.202 交付报告：建军链修复批（⑦↔⑰ 死锁链修复）

> 执行端：TraeCode ｜ 2026-09-11 ｜ 编号：**HH.202**（按水位线取号，D640 #10 禁预留；取号 commit `af5b709`）
> 依据：HH.194 任务书（D655）· **D656 裁决**（sim-sync 成立＋裁①案A／裁②墓碑／硬条款2条／探针追加3条／范围只改 id23-24）
> 前置：HH.195 开工回执（sim-sync 核查＝不在镜像内可直改）｜HH.193 诊断报告（D654 根因＝⑦「选中即空转」死锁）
> 批序：实施 → 回归探针（含 a/b/c）→ 交付报告（本笔）→ 策划端验收

---

## 一、交付结论（验收句）

**三句全中（D656 §四口径）**——同 seed 48903 短局（正门，新槽 `p1_diag2`）行为级实证：

| 验收句 | 诊断局基线（修复前） | run2（60日） | run3（60日） |
|---|---|---|---|
| **⑰ 会被选中**（census top） | BuildBarracks **0** / BuildTrainingCamp **0** | **4 / 4** | **7 / 5** |
| **军事建筑落地 > 0** | 0 | Barracks 2＋TrainingCamp 3 | Barracks 4＋TrainingCamp 3 |
| **⑦ 招战士落地 > 0** | **0**（空转） | **10** 次 | **10** 次 |
| **warrior > 0** | 4 AI 全程 0 | k3 D60 warrior=**3** | k3 D60 warrior=**3** |

**L-31 双向咬合双端各验（本批排雷首位）**：
- **正向**＝⑰ 会被选中：诊断局 `BuildBarracks/BuildTrainingCamp` top **0 次** ⇒ 修复后 run2 **8 次**、run3 **12 次**
- **反向**＝⑦ 不空转：诊断局 `⑦招战士落地` **0**（选中 40 次全空转）⇒ 修复后 run2/run3 各 **10 次落地**且训练完成各 10 次、warrior 实体增长

---

## 二、实施面（D656 四项裁落地）

| # | 项 | 落地位置 | 口径 |
|---|---|---|---|
| ① | **need 机制＝案 A（缺口驱动）** | [UtilityScorer.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs) NeedKind 尾插＋新 case | `need = max(GeneralGap, FormationGap, UnitTypeGap)`；该建筑在场 ⇒ 0；三缺口本身已内嵌 InternalDrive |
| ② | **⑦ Feasible 镜像建筑前置（同源硬条款）** | UtilityScorer.cs `Feasible`＋[KingdomBrain.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs) 新 helper | 复用 `CollectRecruitCandidates/HasRecruitCandidate`（与 `ExecuteRecruitArmy` **同一 helper**，非手搓近似） |
| ③ | **MainSlot 参数化（空槽 fail-fast）** | [Valley_P1_Observer.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_P1_Observer.cs)＋两容器 SetMainSlot | 空串拒绝设置；Checkpoint 空槽 ⇒ LogError 跳过写档（禁回落硬编码 `p1_run7`） |
| ④ | **死码 ExecuteRecruitWarrior 保留＋墓碑注释** | KingdomBrain.cs L854 | 保留＋墓碑一行；**全量删除挂账**（D656 遗留，不单立批/不新开文档） |

**改动文件（`git status Assets` 仅本批 6 文件，零溢出）**：
```
 M Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs        （业务·主修）
 M Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs         （业务·辅修+墓碑+helper）
 M Assets/_Game/Resources/Config/Kingdoms/UtilityActionConfig.asset（资产·id23/24 need）
 M Assets/Editor/Smoke/Valley_P1_Observer.cs                    （观测域·MainSlot 参数化）
 M Assets/Editor/Smoke/Valley_HH80_Run.cs                       （观测域·显式传槽）
 M Assets/Editor/Smoke/Valley_HH80_DiagRun.cs                   （观测域·显式传槽＋DIAG_DAYS）
```
**编译 0 错**（21 警全为既有基线，零新增）。**AI.Core 零触碰**（sim-sync 核查：AI.Core 目录 grep `UtilityScorer|ExclusiveGap|RecruitArmy|KingdomBrain|NeedScore` 零命中）。

---

## 三、asset need 直读证据（D656 必附·L-28 枚举默认值陷阱）

**直读** [UtilityActionConfig.asset](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Resources/Config/Kingdoms/UtilityActionConfig.asset#L434-L460)：
```
L434  - id: 23
L435    name: "建兵营"
L439    need: 22          ← 原 17（ExclusiveGap），已改
L453  - id: 24
L454    name: "建训练营"
L458    need: 22          ← 原 17（ExclusiveGap），已改
```

**未被 Unity 重排的证明**（枚举序 int 值直读 [UtilityScorer.cs L52-L83](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L52-L83)）：
```
HouseGap=0 … WellGap=15, MetalGap=16, ExclusiveGap=17, GeneralGap=18,
FormationGap=19, UnitTypeGap=20, MachineDemand=21, MilitaryBuildingGap=22（尾插）
```
⇒ 中间位全未动（L-28 合规），asset 存 22 精确指向 `MilitaryBuildingGap`。

**运行时印证**：DiagMilitary 每日行 `⑰a建兵营 needKind=MilitaryBuildingGap need=1.02~1.04`（诊断局旧行为 `need` 恒占位 ⇒ 本次直读为真实缺口值）。

---

## 四、回归探针证据（批序②）

**容器**：正门 `TestHarnessApi.EnterTestRun`（15x）＋`P1Observer`（观测器）＋`DiagMilitary`（诊断探针）**双在场 fail-fast**；seed **48903**、槽 **p1_diag2**。

**诊断局基线对照（同 seed，修复前跑）**：`p1_log_20260911_160131.log`
- census top：`RecruitWarrior 40（居首）` / BuildWall 14 / BuildWarehouse 14 / **BuildBarracks 0 / BuildTrainingCamp 0**
- ⇒ ⑦ 被选中 40 次全空转、⑰ 从未被选中＝**死锁原形**（D654 根因复现）

**run1（30日·窗口不足）**：`p1_log_20260911_170102.log`
- ⑰ 被选中 ✓（BuildBarracks 4 / BuildTrainingCamp 4）、Barracks 落地 2 / TrainingCamp 3
- **⑦ 选招 0 / 训练完成 0**（兵营 D28 才竣工 ⇒ ⑦ 可行窗口仅 2 日且金尽）
- ⇒ 判定 2/3，**窗口不足**（见 §七 列报）

**run2（60日·判定句全中）**：`p1_log_20260911_172103.log`
- ⑰ top：BuildBarracks 4 / BuildTrainingCamp 4；落地：Barracks 2 / TrainingCamp 3
- ⑦ 选招落地 **10** 次、训练完成 **10** 次；D60 k3 `warrior=3`

**run3（60日·判定句全中）**：`p1_log_20260911_174750.log`（同 seed 退 Play 重进＝L-22 合规）
- 落地实证行：`k3 建造焦点落地：Barracks @ (324,688)`（D21）/`@(300,696)`（D22）/k1 D59/D60 两座；`TrainingCamp @ (324,700)`（D27）等 3 座
- ⑦：`k3 ⑦多兵种选招落地：→ Warrior（@TrainingCamp）` D33~D42 共 **10** 次
- 训练：`Resident → Warrior（TrainingCamp）` **10** 次；k3 warrior D36=1→D42=2→D54=**3**
- D60 三国：k1 warrior=0 / k2 warrior=0 / **k3 warrior=3**

**⑰ 会被选中（L-31 正向，census top 分布计数）**：
| session | BuildBarracks | BuildTrainingCamp | RecruitWarrior |（空转）|
|---|---|---|---|---|
| 诊断局（修复前） | 0 | 0 | 40 | — |
| run2（修复后） | 4 | 4 | 11 | — |
| run3（修复后） | 7 | 5 | 11 | — |

**专属营落地实证（DZ-103·D656 §六 数据）**：run2/run3 全程 `WarCamp/ArcheryRange/WarAcademy` **命中 0** ⇒ 三军事向专属营（ExclusiveGap 0.5）本批未改、未落地，**据数据待裁是否同改**；LeyForge（经济向）永久保持 ExclusiveGap 未动。

**（b）口径明示**：本批为 **机制级**验证（"任一 AI"聚合＝k3 走通即达标）；**本批不重跑七考**，修复后另起七考重验（P1 终验收 D589）。

---

## 五、确定性对照（batch §四.3 · 诚实列报）

同 seed 48903、同容器、**退 Play 重进**双跑（run2 vs run3），逐日快照 `p1_snap.csv` 对照：

```
run2 行=296  run3 行=296  差异行=176  首个差异日=D2
```

⇒ **长局非逐值确定**：D2 起 worker/资源列开始漂移（真实时间耦合的生产/派工时序），**此前六考/七考从未做过长局双跑对照，本批首次发现**。**诚实列报为"非逐值确定"**，不作"逐字节一致"断言。

**但判定句关键事件两跑独立复现**（⑰ top / 落地 / ⑦选招 10 次 / 训练完成 10 次 / k3 warrior=3）⇒ **修复行为稳健**，非偶然。

---

## 六、既有冒烟零退化（任务书 §四.2）

| 容器 | 结果 | 基线 | 判定 |
|---|---|---|---|
| `Smoke_2_22P0` run1 | **32 PASS / 0 FAIL** | 32/0 | ✓ 零退化 |
| `Smoke_2_23RP0` run1 | **27 PASS / 0 FAIL** | 27/0 | ✓ 零退化 |

证据：`Logs/P1/smoke_2_22p0_run1.log`（18:33:25）／`Logs/P1/smoke_2_23rp0_run1.log`（18:38:53）。

---

## 七、列报项（执行端主动列报）

1. **run1 30 日窗口不足 ⇒ 容器观测窗口调整**：`Valley_HH80_DiagRun.DIAG_DAYS` 30→60。
   - 依据：run1 实测兵营 D28 才竣工，⑦ 可行窗口仅 2 日且金尽 ⇒ 30 日无法观测 ⑦ 落地。
   - 性质声明：**容器级观测配置，非游戏机制参数** ⇒ **D563③（禁参数微调找补）不适用**（同 D647 裁 B′「原地改容器常量」纪律）。
2. **长局非逐值确定性发现**（见 §五）——首次发现，供策划端知悉。
3. **⑦ 落地集中于 k3**（k1/k2 D60 warrior=0）：属"任一 AI 聚合"口径内达标；k1/k2 差异面（资源/焦点竞争）未深挖，如需扩面另批。
4. **diag2 槽未写入物理存档文件**（`p1_diag2*` Glob 零命中）——收工时 `Save(p1_diag2)` 返回见容器状态档；如需留档另裁。
5. **【呈现层事故】收工后 1x 余留世界**——**测试段倍率 ≠ 观察者所见段倍率**，详见 **§十**（本轮首次定位根因；提请登记教训）。

---

## 八、红线遵守自查

| 红线 | 状态 |
|---|---|
| 禁参数微调找补（D563③） | ✓ 机制修复（need 语义），未调 K/权重/阈值 |
| AI.Core 零直改 | ✓ 零触碰（sim-sync 核查零命中） |
| 业务代码与资产之外零改动 | ✓ `git status Assets` 仅本批 6 文件（3 业务 + 3 观测域） |
| 正门 `EnterTestRun`＋15x（L-09） | ✓ 全跑批走正门；console 证 `考跑模式 ON → 15x` |
| L-22 多轮退 Play 重进 | ✓ run2/run3 之间退 Play 重进 |
| L-29 检索阳性对照 | ✓ 见 §二/§八 grep 结论（sim-sync 核查与改动面核查均带对照） |
| L-31 双向咬合双端各验 | ✓ 见 §一（正向⑰＋反向⑦） |
| 写-改-commit 同串／只提本串／不 push | ✓ 见 §九 |

---

## 九、落盘与提交

- 取号登记：`多Agent交接/_编号登记.md` 水位线 HH.201→**HH.202**（独立 commit `af5b709`）
- 本报告：`多Agent交接/执行端/HH.202_建军链修复批_交付报告.md`
- 业务/观测域改动 + 本报告：同一 commit（显式路径，**禁 `git add -A`**，不 push）
- 收尾：退 Play

---

## 十、【呈报·呈现层事故】收工后 1x 余留世界（测试倍率 ≠ 观察者所见倍率）

> 本条为**执行端主动呈报的呈现层事故**（非判定缺陷、非数据缺陷），应策划端要求专章说明，**供后续所有执行端引以为戒**。

### 10.1 现象
本轮冒烟/诊断跑收工后，Game 视图出现一个「**以 1 倍速计时**」的世界：画面时钟一秒一秒跳、日期缓慢推进。观察者（策划端/用户）据此判断「**测试没开加速**」「**验收是按外部 1x 时间倍数进行的**」——**与事实相反**：测试段全程 **15x**（console 硬证 `[TimeManager] 考跑模式 ON：timeScale 直通 → 15x`；墙钟反算 16 分钟跑完 ≈40 游戏日 = 24 秒/游戏日 = 360÷15，恰为 15x）。

**同类历史对照**：此前批次观察者看到的是 15x 世界（因为在**测试进行中**观看）；本轮观察者在**收工后**观看，于是看到 1x 余留世界。⇒ **同一套正确实现，因观察时机不同呈现两种倍率**，这是事故的触发条件。

### 10.2 根因（精确覆盖链，本轮首次定位）
收尾序列存在**倍率覆盖**，且容器**不退 Play**：

| 步 | 动作 | timeScale 实际值 |
|---|---|---|
| 1 | 容器 `Finish()` 调 `TimeManager.SetGameSpeed(0f)`（本意＝暂停） | 经 `SnapToSpeed` 吸附到最近档 **0.5**（**非 0**；当前 15≠0 ⇒ 真的写入）⇒ **0.5** |
| 2 | 紧接 `TestHarnessApi.ExitTestRun()` → [TestHarnessApi.cs L73](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/TestHarnessApi.cs#L63-L75) `if (Application.isPlaying) Time.timeScale = 1f;` | **1**（**覆盖**步1） |
| 3 | 容器**不调 `EditorApplication.ExitPlaymode()`** | **1x 余留世界持续空跑**，直至人工退 Play |

两个子缺陷叠加：**(a) `SetGameSpeed(0f)` 语义不是暂停**（吸附到 0.5，`SnapToSpeed` 档位仅 {0.5,1,2,3}）；**(b) `ExitTestRun` 无条件把 timeScale 归 1**，使收尾的"暂停"意图失效。

**实证**：`Smoke_2_23RP0` 日志第 20 行 `[DBG] ... timeScale=0.5`（步1 吸附到 0.5 的现场证据）；收工后运行时直读 `UnityTime.timeScale=1 | harness=False`（退 Play 前实测，见会话记录）。

### 10.3 影响面（分清"数据"与"观感"）
- **判定证据不受影响（已复核）**：本批全部证据取自**游戏日内**产物——`p1_log_*.log`（DiagMilitary 逐日行/census/落地行）、`p1_snap.csv`（day/kid/资源列）、冒烟日志 PASS/FAIL——均采集于 **15x 段**。墙钟倍率只决定"多久跑完"，不改变**游戏日内**的任何读数。⇒ **验收结论仍成立**。
- **但观察者视角被系统性误导**：任何"看画面/看时钟"的人会得出「未加速」「验收按 1x 时间口径」的错误结论；更实际的是**拖时间的隐性成本**——观察者被迫在 1x 世界里等待/误判进度，等于把 15x 的加速收益在收尾段全部抹掉，还叠加"以为要重新按 1x 验收"的沟通与时间损耗。
- **与 L-09 不冲突**：L-09（禁直接设 timeScale、须走正门 15x）在**测试段**正确生效；问题出在**收尾段**（正门退出把倍率还原，本该随之收尾退 Play）。

### 10.4 防范建议（提请裁决；本批**不擅自改**）
- **方案 A（推荐）**：容器 `Finish()` 末尾**直接 `EditorApplication.ExitPlaymode()`**——复用 [SmokeApi.QuitSmoke](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/SmokeApi.cs#L44-L57) 既有先例（"点一次菜单全程自动化闭环"），观察者根本看不到余留世界。
- **方案 B**：收尾顺序改为 `ExitTestRun()` → **再** `SetGameSpeed(0f)`，并修正暂停语义（0 档真暂停，或直接 `Time.timeScale = 0f`）。
- **方案 C**：`ExitTestRun` 增 `bool keepPaused` 参数，收尾保留暂停态（避免"归 1"覆盖）。
- **规范条文（无论选哪案，建议入册）**：
  1. **凡跑局容器收工，禁止把"已恢复 1x"的余留世界留在 Play 里**（退 Play 或真暂停，二选一）。
  2. **交付报告必须显式区分并写明"测试段倍率"与"观察者所见段倍率"**——禁止只写"测试 15x"而让读者以为收工后画面也是 15x（本次事故的信息根因）。
  3. `SetGameSpeed(0f)` **不得**被当作暂停使用（吸附语义 ⇒ 0.5x）。

### 10.5 教训建议
建议策划端登记 **L-32 候选**（呈现层误导：**正门倍率在收尾段被覆盖 ⇒ 观察者按 1x 余留世界误判测试状态**）。性质＝**呈现/交付纪律缺口**（非验收缺陷、非数据缺陷）；本批执行端自曝、如实列报。

---

## 十一、请裁（策划端）

1. **验收成立**：三句全中＋L-31 双向＋冒烟零退化 ⇒ 建军链修复批交付是否验收？（D657）
2. **DZ-103**：三军事向专属营（WarCamp/ArcheryRange/WarAcademy）本批未改、实证 0 落地 ⇒ 是否同改（下一批）或维持观察？
3. **七考重验**：本批明示不重跑，修复后另起七考（P1 终验收 D589）——是否放行？
4. **挂账**：死码 `ExecuteRecruitWarrior` 全量删除（D656 遗留）——是否指定批次或维持挂账？

---

*交付：执行端 TraeCode 2026-09-11（HH.202）。*
