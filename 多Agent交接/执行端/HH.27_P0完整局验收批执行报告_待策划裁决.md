# HH.27 P0 完整局验收批执行报告（确定性状态机验收 · 终版 · P0 收官成立）

> 类型：P0 收官验收批执行报告（终版）
> 状态：✅已裁决（P0 收官成立）· 评审已翻页
> 日期：2026-08-27 · 发起端：执行端 · 关联：HH.26 / HH.25 / HH.24 / 2_17_AI王国脑与自主成长.md
> 前置：HH.26 裁决全准 → HH.27 策划①②③ → 二轮甄定 → **A3 widow-window 根因实锤（GO 三修复）→ 真绿** → B3 定性（harness 时序假差）→ 玩家核码终裁 P0 收官成立

> **效力脚注（收入侧归步骤14）**：pump 收入侧为 harness 抽象结算（D281 ApplyAbstractSettlement 预演值），产品收入侧归 2_17 步骤14 实现；本批验收判 A1/A2/B4/B5 收入口径按此声明不降级。

---

## 〇、一句话

**P0 收官成立。** A3 根因实锤为 **widow-window（复位窗口期污染打 GameOver）**，GO 三修复后同 seed 两纯轮逐字节一致（A3 真绿）；B3 定性为 **harness 时序假差**（SAVE_DAY 中途插棒致日结算 ±1 + 在途储仓丢失，非产品读档缺陷）→ 带红挂 2_11 独立回归卡不阻塞；B1/B4/B5 招工→成长段卡"流浪汉池空"=pump 环境断层 → 归人工 Play 黄旗；epsilon「产品 AI 木材漂移」与「TrainingSystem L305」合一登记为确定性全库扫描小卡。**P0 章节正式翻页，下一站 P1 步骤10（效用补全 + Faction 收编 sim-sync 报备）。**

---

## 一、A3 根因实锤：widow-window（窗口期污染打 GameOver）→ GO 三修复 → 真绿

### 1.1 追凶结果（二分到 window 机制，否决前期所有污染假说）

A3 采用同 seed 两纯轮（R1/R2）逐字节快照链比对。前期四轮追凶均未变（①VagrantCamp 播种 / ②Registry.ResetState 双层残留 / ③残余清点 / ④二点协议），最终靠 **②' 三点基线 dump**（pre-reset / post-reset / post-Init）锁定：

- **pre-reset**：实时世界有 K=4 / Building / ActiveMap（编辑器里挂着的人工 Play 会话）
- **post-reset**：全清零 → 否决"extra teardown 漏清"（世界级 teardown 有效）
- **post-Init**：**仅 r1=GameOver**，r2/r3=Playing → 锁定 widow-window

**机制（玩家核码实锤，凶器单一）**：harness 贴实时会话触发时，复位链先清工人=0 → `yield return null` 让出一帧 → 旧 ThroneAnchor（未失能、Playing 轮询达标）`Update L82 IsKingdomLost=true` → `SetState(GameOver)` → r1 post-Init 即 GameOver → **非法跳过 `surviveMinDays=2` 门控**（ScriptStageMachine L83）→ day1 即 Develop（phase1/build4/wood52）；r2/r3 干净 → Playing → day1=Survive。这就是 R1/R2 day1 分叉（R1 Develop/build4/wood52 vs R2 Survive/build1/wood60）的真正来源。

### 1.2 GO 三修复（均为 harness-only，产品逻辑零改动）

| # | 修复 | 作用 |
|---|------|------|
| GO① | 复位链头部 `GameStateManager.SetState(MainMenu)` | 关闭 ThroneAnchor 轮询窗口（其只在 Playing/Paused 轮询）→ 旧锚不再能打出 GameOver |
| GO② | `DisarmDisasters()` 前置 `InitializeNewGame`（保留后置一次防新锚） | 旧锚先失能，闭环窗口；新锚仍被后置防一次 |
| GO③ | ResetState 后反射强制 `_dayTimer=0` | 防实时 Play 残留秒数白嫖推进（ResetState 只回 day1 不清 _dayTimer） |

### 1.3 A3 真绿证据（GO 修复后重跑）

```
post-reset : State=MainMenu                                   ← GO①生效，窗口关闭
post-Init  : r1/r2/r3 全 State=Playing  timer≈0.32/f0        ← widow 已关，r1 不再 GameOver
A3确定性逐字节=OK                                            ← 两纯轮逐字节一致
RD2-①轮间清点=OK(b=2684/2684/2684 u=22/22/22)               ← 非退化兜底
RD2-②存读v2门控=OK(loadVer=2 走 B 全权重建)
时间线     : R1 = SDDDDD... = R2（同为 45×D 停 Develop）      ← 成长行为收敛一致
A3 wood二分: 末一致日=行44 首差日=行-1（已不再分叉）          ← 漂移闭合
u=22/22/22 非退化
```

> 尾声附注：真绿后 wood 结论从"漂移 52 vs 60"收敛为全对齐；仅剩的「成长都停 Develop」/「train0」源自 B1 流浪汉池空（§三），非 A3 确定性范畴。

---

## 二、B3 定性：harness 时序假差（非产品读档缺陷）→ 挂 2_11 独立回归卡

### 2.1 定性（玩家核码证据链）

roundtrip 轮 r3 的存读点插在 **simDay=25 当天日结算之前、前夜态与日结算之间** → SAVE_DAY 中途插棒：

- **日结算 ±1 错位**：读档恢复前夜状态（day24→25 结算尚未发生），断言到的"结算结果"与纯轮对齐帧差 1。
- **在途储仓丢失**：读档 `LoadState` 把 resources 整包从存档恢复，**当日已产出但尚未入账的储仓在途量随重建被清**→ 木材读数差异。

### 2.2 判定依据（为何非产品 defect）

- `RD2-② v2 门控=OK（loadVer=2 走 B 全权重建）`——产品读档重建自身语义自洽。
- 读档重建各模块（Kingdom/每王国 resources 整包恢复）在 `KingdomRegistry.LoadState` 等按此恢复，只要插棒时序正确即无损。
- 纯轮从不触发（纯轮无存读），仅 roundtrip 轮的"插棒时刻"敏感 → 属 harness 时序选择，非 LoadState 逻辑缺陷。

### 2.3 独立回归卡定义（写死，不阻塞 P0）

- **卡名**：`【回归】B3 存读 roundtrip 时序回环`——挂 **2_11（读档/LoadState 迭代）** 独立回归。
- **范围**：harness 存读点从"日结算前"改为"日结算后（或避开 SAVE_DAY 中间态）"，断言纯/回环两轮 day25+ 对齐。
- **接受红**：P0 收官带此红成立，B3 不作为 P0 阻塞项。

---

## 三、B1/B4/B5 定性：流浪汉池空（pump 环境断层）→ 归人工 Play 黄旗

- **根因**：pump 走完整 DayCycleSettlement，但地图流浪汉由 GlobalMap 初始预置 + 营地每日补员（依赖 ActiveMap/营地滤镜）驱动；纯 pump `InitializeNewGame` 无引导时序、ActiveMap 未产 → 流浪汉池空。
- **连锁**：玩家 `RecruitVagrant`（pFalse 根本因=候选缺失）与 AI `ExecuteRecruitWorker`（`FindRecruitableVagrant` 空→`train0`）全空转 → 不招工 → 人口不长 → 停 Develop → B4 未达 Expand。
- **判定**：pump 环境无法注入地图流浪汉的产品-环境断层，**单向推证到产品前**，broccoli 招募→成长段判此 N/A；已补 `UnitFactory.SpawnUnit(Occupation.Vagrant,...)` 程序化 spawn 通道可续验 B1 正向（pTrue 已过）。**归人工 Play 一票**（策划正式接单，见 §四黄旗）。

---

## 四、P0 收官形态（终裁）

### 4.1 绿色项（自动化已证）

- **A3 确定性（同 seed 两纯轮逐字节）= 绿**（§一）
- **A4 玩家零回归 = 绿**
- **B2 供水抽象产出 = 绿**
- **B1 正向招募通道 = pTrue**（含流浪汉程序化 spawn 兜底）
- **RD2-①轮间清点 = OK** / **RD2-②存读 v2 门控 = OK**

### 4.2 人工 Play 黄旗三条（策划正式接单，非阻塞）

1. **细模拟经济闭环**：工人真走真产（pump 为抽象结算，产物路径待真地图验证）
2. **招工→成长链**：流浪汉池空仅 pump 环境，真实地图 GlobalMap 预置链路待人工验证
3. **GameOver 路径**：灾变三禁（D315 guard 三处）未在完整局中走通人工验证

### 4.3 登记债两条（非阻塞，待后续批处理）

- **④债 Ruinit 门控**：归 2_17 步骤12/领土入档先到者（触发必落 foundKingdoms 门控 + 三处追记回写）
- **【确定性全库扫描】小卡**：全库 22 个 UnityEngine.Random 消费面批量治理（种子派生 System.Random，对齐 VagrantCamp D308）；已覆盖 `TrainingSystem.cs:305`（玩家桶，npcId 稳定最小选人已修）与 VagrantCampSystem（世界种子^当日 播种已修）

### 4.4 数据卫生结论（isFinished 排查）

实盘 grep 全部槽位 `isFinished` 字段：
- 真实槽位 `slot_1.json`：`isFinished=false`（currentDay=25）→ **无误标**
- `slot_verify.json`：`isFinished=false`（currentDay=1）→ 无误标
- `smoketest.json`：`isFinished=false`（currentDay=1）→ 无误标
- smoke 槽位（smoke_p0_b / smoke_p0_rt）：当前 Saves 目录**无残留文件**，无需清档

结论：**真实槽位零误标、无残留，数据卫生干净**（此前窗口 GameOver 均未落盘污染 isFinished）。

---

## 五、三问终裁记录（已裁决）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| A3 | 根因实锤 widow-window，GO 三修复后**真绿闭合**；epsilon 产品漂移归确定性小卡修后重跑，不留黄旗 | 二分到 window 机制、因果链齐（§一）；产品零改动，纯 harness 窗口污染 |
| B1/B4/B5 | 流浪汉池空=环境断层，招工→成长段判人工 Play；harness 补 spawn 通道续验 | 单向推证到产品前不判定产品 defect |
| B3 | **harness 时序假差**，接受带红做独立回归卡挂 2_11，不阻塞 P0 | SAVE_DAY 中途插棒 ±1 + 在途储仓丢失；产品 LoadState 语义自洽（v2 门控 OK） |

**P0 收官终裁：成立。** 执行序已闭环：isFinished 数据卫生（§4.4 干净）→ HH.27 终版回填（本文）→ git-plan-sync 收口（工作日志顶行 P0 收官 / 2_17 实施计划 P0 收官记录 / 状态 🚧→P0✅ / 版本历史）。

---

## 六、交接状态

- **P0 章节翻页**：2_17 P0 收官成立，历史留档于 CS 3.5 迭代记录。
- **下一站**：**P1 步骤10（效用补全 + Faction 收编 sim-sync 报备）**。
- **遗留**：人工 Play 黄旗三条逐一消旗（人工验证后回写）；确定性全库扫描小卡 + B3 独立卡 + ④债归步骤12 随各自批处理。

---