# HH.285 `G2-2` ㉕机器实产 · 门判定读数 交付报告（第二步·跑局读数）

> 类型：交付报告｜状态：🟡 **已交付·R1 结构性未达＝按派工纪律停手列报（不造数·不调参找补·L-30）**
> 日期：2026-09-15 · 执行端 · 承 HH.284 开工回执（§0 gap 表已过目·「按任务书施工」放行）
> 取号：**HH.285**（账本水位线 284→285 · `D640 #10` 禁预留）
> HEAD 基线：`2eb02b1d` · 未 push
> 档位：SEED=73621 ／ 槽 `hh285_obs1`（新槽·**未覆盖 p1_run9**）／ 120 日熔断（HH.203 同档）／ 考跑直通 15x

---

## 〇、一句话结论

**「㉕战争机器 AI 实产」在 D120 全窗口内 0 top / 0 派发 / 0 实体＝结构性未达**；根因链完整闭合＝**①环3 需求驱动为空**（唯一军事期国 k3 `MachineDemand need=0.000`＝无邻接威胁∧姿态未达 Alert）＋**②环1 自建被压制**（㉔ 恒 0.5 占位在军事期 16 日内从未 top，被 ⑯ 将军链全程压制＝**DZ-105 实证升级**）＋**③经济面缺木**（k3 wood=16 < ㉔成本 20 ⇒ feasible=False）。**门 `G2-2` 不置 ✅；停手列报，参照 DZ-068 治法（内源节拍/门槛映射层）由策划端立新批。**

---

## §一、四组读数总表（L-34 五列）

### R1 ㉕「战争机器 AI 实产」

| 列 | 内容 |
|---|---|
| 可判定最早时点 | k3 军事期首日（~D104）——此前 ㉕ 被 `minStage=Military` 硬门挡住（stageGateBlocked=True，D2~D103 全程实读） |
| 命中即停 | **未触发**：既非门达标（top㉕ 从未首达），也非空转停（空转判据前置＝top 首达，未发生）⇒ 跑满 D120 熔断 |
| 服务验收句 | 「㉕战争机器 AI 实产」＝**未取得任何读数**：top㉕=0（四国全程）／派发 成/败/限=0/0/0（四国全程）／机器实体 0（四国终局） |
| 作用域 | 全 AI 国 k1~k4（玩家 id=0 排除）；仅 k3 入军事期（1/4），其 16 日军事窗口为 ㉕ 的全部可评窗口 |
| 口径来源／排除项 | 评分面=census `top=`（DiagMilitary DumpAction 尾插逐日实读）＋派发面=`[SiegeProduction]`/`[KingdomBrain]` 流解析＋存量面=`GetPlacedMachineCountByKingdom`（与 Feasible ②同源）；排除项=玩家国、HH.282 会话历史日志（Editor.log 跨批累积已甄别） |

**关键证据（k3 军事期逐日实读·D117~D120 同构）**：

```
[DiagMilitary] D120 k3 ㉕造机器 needKind=MachineDemand need=0.000 feasible=False stageGateBlocked=False ...
```

- `need=0.000`：`MachineDemand` 三前置实读＝快照在✔＋**军事期在✔**＋**threatM ‖ postureM ＝ false** ⇒ **k3 无邻接威胁且姿态未达 Alert** ⇒ **HH.284 §一 环3「孤立 AI 国结构性不入池」预言在本局实证**（`SituationSnapshot.Threats` 填充无战士阈值口径缺陷的真实行为后果）
- `feasible=False`：Feasible ①厂前置（`CountActiveDef(k3, SiegeWorkshop)=0 < 1`）拦截

### R2 ⑦「专属兵落地读数」

| 列 | 内容 |
|---|---|
| 可判定最早时点 | 终局即判（非命中即停型·HH.284 §3.2 预告） |
| 命中即停 | 不适用 |
| 服务验收句 | 7 专属兵落地读数＝**全 0**：终局 R2 `k3 Warrior=4 专属兵 Ber=0 Wolf=0 Mus=0 Bed=0 Ran=0 Win=0 Deer=0（28~34） 机器 Mor=0 Vine=0 Ram=0 Bal=0`（k1/k2/k4 全字段 0） |
| 作用域 | 全 AI 国在册单位（UnitRegistry·排除玩家/怪物） |
| 口径来源／排除项 | `EffectiveOccupation` 逐单位计数（28~34 专属兵域+35~37 机器域+Warrior 对照）；k1/k2/k4 未入军事期（无训练入口前置） |

根因同链：专属兵入口＝三营（WarCamp/ArcheryRange/WarAcademy·minStage=3），k3 军事期 16 日内三营 `feasible=False`（兽人战营 25木>16 缺木；战争学院/射箭场族门禁不符）⇒ 无训练入口 ⇒ 0 落地。

### R3 军事期复证（D684）

| 列 | 内容 |
|---|---|
| 可判定最早时点 | 逐日实读 |
| 命中即停 | 不适用（本批未配军事期停止判据——D666「同级+同作用域」纪律） |
| 服务验收句 | **1/4 AI 军事期**：k3 @~D104 入 Military（warrior 5，worker 7）；k1 Develop／k2 Expand／k4 Expand 至终局 |
| 作用域 | 全 AI 国 |
| 口径来源／排除项 | `k.scriptPhase`＋`k.warriorCount` 每日实读（容器行）＋census 佐证；排除玩家 |

⚠️ **与 D684（HH.203 @同 seed 73621）的差异**：本局 1/4（HH.203 当时 ≥2 军事期）。单样本不下退化结论，列报供对照（两次跑局的ⒶⓇ资源分布/邻接结构同 seed 同局态，差异可能来自 2_20 之后的经济/军事判据演进——如需定论应由策划端决定是否开对照批）。

### R4 厂前置（环1 实机对照）

| 列 | 内容 |
|---|---|
| 可判定最早时点 | 逐日实读 |
| 命中即停 | 不适用 |
| 服务验收句 | **四国 fac=0 全程**：SiegeWorkshop 在场数 0（终局亦 0）；`machines=0/2`（上限读数正确＝base 2·厂 0 级） |
| 作用域 | 全 AI 国（口径镜像 `UtilityScorer.CountActiveDef`＝kingdomId+IsActive+def.id，L-31 同源） |
| 口径来源／排除项 | `BuildingRegistry` 只读遍历＋`DiagMilitary` 存量行逐日输出；排除非 Active 态 |

---

## §二、根因链闭合（三层复合·逐层证据）

```
㉕ 实产 0（R1）
 ├─ 环3 需求驱动为空（评分面·NeedScore）
 │   └─ k3 军事期 ㉕ need=0.000 ⇒ threatM=false（无邻接威胁·快照填充口径）∧ postureM=false（姿态<Alert）
 │       ⇒ 即便厂在场 ㉕ 也得 0 分 —— HH.284 环3「有条件下可达」在本局条件不成立
 ├─ 环1 自建被压制（评分面·DZ-105 本体）
 │   └─ k3 军事期 16 日 ㉔ score 恒 0.500（need 0.5 × axis 1.00 × stageW 1.00）
 │       × census top 恒 TrainGeneral（⑯ 将军链 6 次入队运转正常）
 │       ⇒ 恒定占位分永远排不过军事缺口动作（⑰c/d/e need=0.802 实读对照）
 │       ⇒ DZ-105「态势驱动口径待裁」由观察项升为实质阻断实证
 └─ 经济面缺木（执行面·Feasible）
     └─ k3 @D120 gold=166 stone=225 wood=16（wood in=0·verdict stoneCold:38/ANOMALY:stoneChain）
         ⇒ ㉔ cost 30/20/20木 中 wood 16<20 ⇒ feasible=False（infeasible=5 常驻：㉔+㉕+⑰c族+⑰d缺木+⑰e族）
         ⇒ 即便评分被修，执行面仍被木链枯竭挡死
```

**「若发现结构性不可达」条款判定**：评分侧代码环（HH.284 环2/4/5）全部已闭、无死锁；未达根因＝**需求驱动口径（DZ-105/环3）× 经济面（木链）**复合，属 **DZ-068 同族（内源节拍/门槛映射层）**。按派工纪律：**停手列报，不自行造数、不调参找补，由策划端立新批**。

---

## §三、配套挂账结论（本批读数直接定性）

| 挂账 | 本批定性 | 证据 |
|---|---|---|
| **DZ-105**（id25 SiegeWorkshop 态势驱动口径待裁） | **🔴 实质阻断实证（建议升级处置）**：①恒 0.5 占位与军事缺口（0.802）无联动，k3 军事期 16 日全程被 ⑯ 压制、top 从未易主；②同窗口 ㉕ need=0（威胁/姿态双空）——㉔与㉕均缺「战争态势」驱动，两条行动在安静邻接下永久失活。**口径裁决需求坐实**（内源节拍/门槛映射层，参照 DZ-068 治法） | D117~D120 ㉔/㉕ DumpAction 逐日实读＋census top 序列 |
| **DZ-103**（三军事向专属营观察项） | **保持观察项·附首轮实机证据**：三营「制度面已闭」（asset `need:22` 生效——⑰c/d/e 实读 `needKind=MilitaryBuildingGap need=0.802` 评分正常）但实机 0 落地；本局根因＝族门禁（2/3 营与 k3 兽人不符·设计如此）＋缺木（WarCamp 25木>16）。**非评分面回归**，木链修复后需再观察 | D119/D120 ⑰c/d/e DumpAction＋k3 资源行 |

---

## §四、改动面声明（观测域·业务代码零改动）

| 文件 | 性质 | 内容 |
|---|---|---|
| `Assets/Editor/Smoke/Valley_DiagMilitary.cs` | Editor-only 观测域增量（改） | 尾插 ㉔/㉕ 两条 DumpAction＋1 行 R4/R1 存量读数＋`CountSiegeWorkshop` 镜像 helper（口径镜像 `UtilityScorer.CountActiveDef`·L-31）；**零业务写** |
| `Assets/Editor/Smoke/Valley_HH285_Observe.cs` | Editor-only 观测容器（新） | 正门 EnterTestRun／逐日只读 tick／日志流解析／R1 命中即停（门达标·空转30日·ANOMALY）／D120 熔断／终局 R2＋截图＋全量恢复退 Play（L-32） |
| `Assets/_Game/**` / `AI.Core` | **零改动·零触** | — |

编译：`refresh_unity(compile=request)` ＋ `read_console(types=[error])` ＝ **0 error**（唯一条目为良性编辑器消息）。csproj 同步见 §六 写后验。

---

## §五、原始证据路径

| 证据 | 路径 |
|---|---|
| 容器逐日镜像（R1~R4 全读数＋证据行） | `Valley Rampart/Logs/hh285_obs.log`（72KB·副本 `screenshots/HH285_㉕门判定读数/hh285_obs.log`） |
| 终局状态（console 大缓冲教训兜底） | `Valley Rampart/Logs/hh285_status.log` |
| console 全量（含 DiagMilitary census/DumpAction 明细） | `C:\Users\trs\AppData\Local\Tuanjie\Editor\Editor.log`（本编辑器会话累积·含 HH.282 历史段已甄别） |
| 终局截图（k3 视野） | `Valley Rampart/Logs/hh285_live.png` ＋ 副本 `screenshots/HH285_㉕门判定读数/hh285_live.png`（46KB） |

采样方式备注：跑局中途 CoplayDev `read_console` 在 15x 高载下持续 2s 超时，按 unity-mcp-first 故障切换规则改读 Editor.log（console 落盘同源流·逐字一致）采样；镜像文件双保险。

---

## §六、写后验

- 本批相关路径 `git status`：`Assets/Editor/Smoke/` 仅上表 2 文件（1 改 1 新）；`Assets/_Game/` **全空**；`Logs/`、`screenshots/` 均在忽略清单
- hash 已贴：本次 commit 见账本（报告落库后回填）；**未 push**
- 无临时改动需还原（容器/探针为交付物本体，负探针条款不适用本批——本批无资产改动）

---

## §七、待策划端

1. **DZ-105 升级裁决**：恒 0.5 占位 → 态势驱动口径（本批已给首个实机阻断证据＋同族 ㉕ 双失活证据）
2. **新批立项**：参照 DZ-068 治法（内源节拍/门槛映射层）治「安静邻接下 ㉔/㉕ 双失活」＋木链枯竭对军事建造链的阻断
3. **R3 与 D684 差异**（1/4 vs ≥2 军事期）：是否开对照批定论
4. 本批通过**不得读作 `G2-2` 全绿**——「㉕战争机器 AI 实产」仍无读数，门维持未置 ✅

---

> 执行端｜2026-09-15｜HH.285 第二步（跑局读数）完成：D120 全窗口 R1=0/0/0 结构性未达·根因链三层闭合（环3 驱动空×DZ-105 占位压制×缺木）·DZ-103/105 定性已给·停手列报待新批
