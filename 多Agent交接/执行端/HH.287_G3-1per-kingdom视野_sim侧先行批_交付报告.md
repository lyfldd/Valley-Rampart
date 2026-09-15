# HH.287 「`G3-1` per-kingdom 视野 · sim 侧先行批」交付报告（**第二步·施工**）

> 类型：交付报告｜状态：🟡 **待主策划端验收**
> 取号：**HH.287**（水位线 286→287·先登记后落盘；`_编号登记.md` 已登记）
> 日期：2026-09-15 ｜ 端：**训练端（训练师施工）** ｜ 裁决端：**主策划端**（跨域·`3.1 §七 P-007`）
> 依据：口径设计稿 [HH.286_..._口径设计稿_报裁.md](file:///c:/Users/trs/Desktop/Valley%20Rampart/多Agent交接/执行端/HH.286_G3-1per-kingdom视野_sim侧先行批_口径设计稿_报裁.md) §十一 裁决区（**`D731`：5 项请裁全准 ＋ 放行第二步**）｜任务书（§一 已在 `D731` 勘正）｜开工回执 [HH.286_..._开工回执.md](file:///c:/Users/trs/Desktop/Valley%20Rampart/多Agent交接/执行端/HH.286_G3-1per-kingdom视野_sim侧先行批_开工回执.md)
> 红线条目遵守：`Core`／`KingdomBrain` 零触碰（已裁定 Core 零改动）· 不动 `champion`／`Holdout`／`AGENTS.md`／`FactorRegistry` · 主仓 `Assets/**` 零触碰 · 不为过判据造特制场景（`L-30`）· 判据写明口径来源与排除项（`L-35`）· 探针读数不写 `report.json`

---

## 〇、结论速览

| 项 | 结果 |
|---|---|
| 施工三块 | ✅ **全落**（①§四 12 点改造面 ②三变体探针 ③三件前置） |
| 门禁① 编译 | ✅ `dotnet build harness -warnaserror` **0 警告 0 错误** |
| 门禁② 对照 | ✅ `benchmark --suite v9 --battles 100`（**读锚不写锚**）**锚键集实读 148/148 逐共有场景 `subScore Δ` max = 0.000（0 项 >0.0005）**· `NoRegression=True`· 总分 0.422→0.422 |
| 门禁③ 确定性 | ✅ `determinism` **6/6 逐字节一致**（3 锚场景 3/3 ＋ 3 新探针场景 3/3） |
| 门禁④ holdout | ✅ 0.498→0.498（**Δ0.000**·`regressed=False`·4 场景） |
| 判定线 ① | ✅ **PASS**（桶键 `[0,1,2]`＝per-kingdom 分桶成立·桶长钳制 `DZ-158` 已解） |
| 判定线 ② | ✅ **PASS**（`k1∩k2` 全程 = 0）；阴性对照 ✅（重叠变体读数 = 204 ⇒ 读数能报非空） |
| 判定线 ③ | ✅ **PASS**（AI 探索桶覆盖玩家格 = 0）；阴性对照 ✅（玩家入视变体 = 1 且 AI 可见玩家 = 2） |
| 双端同源 | ✅ **MD5 前/后表逐行一致**（`changedLines=0`）⇒ `Core` 零改动确认 |
| `15_账本` | ✅ 「一·补」已登记（迷雾行升级 ＋ 新增块：Unity 适配点/`enableFog` sim-only 字段注记） |
| 位移声明 | ✅ **不改锚·不进池**（探针目录；五步中"构成冻结/重建锚"不适用） |
| 列报 | ⚠️ 2 项（存量 7 文件双端差异·非本批不修／**雾滤对「单位自身感知」空转**观测）＋ 施工中补清单外缺口 2 处 |

---

## 一、施工面（三块）逐项回执

### 1.1 ①§四 12 点改造面（逐点落点·全部实际落地）

| # | 落点（改动后） | 状态 |
|---|---|---|
| 1 | `SimWorld._fogExplored`＝`HashSet<long>[2]` → **`Dictionary<int, HashSet<long>>`（键＝`kingdomId`·`FogBucket()` 按需建桶·无长度上界）** | ✅ [SimWorld.cs:220-229](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L220-L229)／[:257-266](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L257-L266) |
| 2 | `IsExplored(int kingdomId, Vector2X)`（保留 `!enableFog ⇒ true`；`NoKingdomId`／无桶 ⇒ false） | ✅ [:248-255](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L248-L255) |
| 3 | `RevealCircle(int kingdomId, …)`（保留 `!enableFog ⇒ return`；`NoKingdomId ⇒ 不揭示不建桶`） | ✅ [:268-279](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L268-L279) |
| 4 | `RevealUnitVision(u) => RevealCircle(u.KingdomId, u.Position, u.Profession.perceptionRadius * cellSize)`（半径未改） | ✅ [:286-287](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L286-L287) |
| 5 | 构造桶初始化 → 惰性建桶（原 2 桶预建循环删除） | ✅ [:339](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L339) |
| 6 | `SimBrain` 雾过滤：`IsExplored(_self.KingdomId, e.Position)`；**`myFaction` 保留**供 `QueryNearby`（`M4` 双轨） | ✅ [SimBrain.cs:376-378](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimBrain.cs#L376-L378) |
| 7 | `SimUnit.KingdomId`（构造自 `spec.KingdomId`） | ✅ [SimUnit.cs:20](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimUnit.cs#L20)／[:51](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimUnit.cs#L51) |
| 8 | `SimUnitSpec.KingdomId`（默认 `SimWorld.NoKingdomId`） | ✅ [SimScenario.cs:76](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L76) |
| 9 | 场景 `units[].kingdomId` 解析 ＋ **派生兜底**（`DeriveKingdomId`） | ✅ [SimScenario.cs:177-186](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L177-L186)／[:347-361](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L347-L361)／DTO [:446](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L446) |
| 10 | `enableFog` **场景级开关**（顶层可选字段·只读消费·缺省不写键 ⇒ 走 `false` 默认路径） | ✅ [SimScenario.cs:140-141](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L140-L141)／DTO [:397](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L397) |
| 11 | **只读探针出口**：`FogBucketKeys()`／`FogExploredCellCount()`／`FogOverlapCellCount()`／`ProbeUnits()`／`RunTicksForProbe()` ＋ CLI `fog-probe` | ✅ [SimWorld.cs:289-338](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L289-L338)／[SimBrain.cs:362-373](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimBrain.cs#L362-L373)／[Program.cs:375-491](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Program.cs#L375-L491) |
| 12 | 不动面确认：`CheckEnd`／参与阵营集／`SimPerception`／`ScenarioGenV2`／`CardPool`／`ScenarioGenerator` | ✅ 零改动（`Faction` 仍为敌我/胜负唯一口径） |

### 1.2 ②三变体探针（探针目录·不进池）

| 变体 | 文件 | 作用 |
|---|---|---|
| F1 正例 | [probe_fog_k2_far.json](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Scenarios/v9_probe/probe_fog_k2_far.json) | k0 玩家（x−68/−60）／k1 王国A（x−4/4）／k2 王国B（x60/68）：三国视野互不重叠 |
| F2 阴性对照① | [probe_fog_k2_overlap.json](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Scenarios/v9_probe/probe_fog_k2_overlap.json) | k1/k2 拉近（−4/4 vs 16/24）⇒ 应有交集 |
| F3 阴性对照② | [probe_fog_player_in_sight.json](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Scenarios/v9_probe/probe_fog_player_in_sight.json) | 玩家入 k1 视野（−8/−2 vs 4/10）⇒ AI 应能看见玩家 |

> 三变体均 `enableFog: true`·`cellSize 2.26`·`maxDuration 5.0`·6 单位（三国各 2）·无编队（散兵）；`perceptionRadius=8` 格 ⇒ 视野半径 18.08 世界单位。

### 1.3 ③三件前置（`D731` 11.2 附加约束逐条遵守）

| 前置 | 落地 | 约束核对 |
|---|---|---|
| C-2 探针场景 | `harness/Scenarios/v9_probe/`（**不进池**：v9 套件 glob 为 `harness/Scenarios/v9/*.json` 非递归 ＋ `Cards/**/K*_2_*.json`，见 [Program.cs:622-627](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Program.cs#L622-L627)） | ✅ 不改锚·不进池＋**位移声明**（见 §五） |
| C-3 `enableFog` 开关 | 场景 JSON 顶层可选字段 → `SimScenario.Load` **只读消费** | ✅ 不写该键的既有场景走 `false` 默认路径（零回归）；**`15_账本` 已登记**（sim-only·Unity 无对应物·零回灌义务） |
| C-4 读数出口 | `fog-probe` 独立子命令 ＋ `SimWorld` 只读访问器 | ✅ **不新增 `report.json` 字段**；**不进 `RunTrain` 路径、不参与 `benchmark`**（`case "fog-probe"` 独立分支） |

---

## 二、门禁四件（命令 · 读数 · 判据）

### 门禁① 编译门
`dotnet build harness -warnaserror` ⇒ **已成功生成·0 个警告·0 个错误**（11.38s→后续增量 5.79s）。

### 门禁② 套件对照（**读锚不写锚**）
`dotnet run --project harness -- benchmark --suite v9 --battles 100`
- **执行前置审读**：锚在场（[results/baseline/v9/report.json](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/results/baseline/v9/report.json)：`total 0.422`／`battlesPerScenario 100`／`seed -1283596485`／`timestamp 2026-09-12`）⇒ `!File.Exists(baselineReport)` 为假 ⇒ **不触发建档分支**（未写锚）；候选输出另起目录 `results/20260915_123052_bench/`。
- **C-5 口径实读（贴出键集）**：锚 `score.subScores` 键数 **148**；候选键数 **148**；**交集 148**（零单侧键）。⇒ 任务书原「148/151」与 `00 §8.3` 原「144」**统一以实读 148 为准**。
- **逐共有场景 `subScore` 差**：**max |Δ| = 0.000**；**`Δ > 0.0005` 的场景数 = 0**；`Δ ≠ 0` 的场景数 = 0；总分 0.422→0.422（Δ 0.000）。
- **`NoRegression=True`**（无场景退化 >5%）。
- **holdout 同卷**：Δ 0.000·`regressed=False`。
- ⚠️ **verdict 字段读法**：`verdict.json` 的 `Decision=rejected` 系**"总分未升"**（三条件中 `总分升=False`；本批**非提分目标·是零回归目标**）——**不是退化**（`无场景退化=True`／`holdout不退=True`）。原始日志：`runs/hh286_gate2_bench.log`（2748 行）。

### 门禁③ 确定性
`determinism <场景> 2` 逐场景两次同 seed 逐字节比对 ⇒ **6/6 通过**：
- 锚三场景 **3/3**：`W9_T-K_20260907_0`（46539/45986 B）／`F26/KF26_2_0`（78194/82804 B）／`holdout_v9/H9_chokepoint_21039824`（43031/20575 B）；
- **新探针三场景 3/3**：`probe_fog_k2_far`（3209/3209 B）／`probe_fog_k2_overlap`（3213/3213 B）／`probe_fog_player_in_sight`（4478/5217 B）。
- 原始日志：`runs/hh286_gate3_determinism.log`。

### 门禁④ holdout
`holdout_v9`（4 场景·独立 seed 段）：锚 **0.498** → 候选 **0.498**（**Δ 0.000**·`regressed=False`）⇒ **不退** ✅（≥4 场景满足）。

---

## 三、判定线 ①②③ ＋ 阴性对照（探针读数原文）

**F1 `probe_fog_k2_far`（正例·`fog-probe --ticks 10`）**
```
[单位] 1|Human_Player_Warrior|PlayerCamp|0| 2|…|PlayerCamp|0 | 3/4|…|AiKingdom|1 | 5/6|…|AiKingdom|2
[t1..t10] 桶=[0,1,2] 格数={0:340,1:357,2:357} k1∩k2=0 任意两国交集上限=0 AI探索含玩家格=0 AI可见玩家=0 几何内敌=0 过滤后敌=0
[判定] ①探索集 per-kingdom 分桶：桶键=[0,1,2] 含 k1&k2=是 ⇒ PASS
[判定] ②k1∩k2 交集空：全程最大=0 ⇒ PASS
[判定] ③AI 不读玩家集：AI 探索桶覆盖玩家格的王国数上限=0 ⇒ PASS
[对照] 任意两国交集上限=0 · AI 可见玩家上限=0 ⇒ 阴性对照读数有效性：全程零   ←（F1 本应全零·对照由 F2/F3 提供）
[观测] 雾滤咬掉敌数累计=0
```

**F2 `probe_fog_k2_overlap`（阴性对照·② 应报非零）**
```
[t1..t10] 桶=[0,1,2] 格数={0:340,1:357,2:357} k1∩k2=204 … AI可见玩家=0
[判定] ②k1∩k2 交集空：全程最大=204（@t1）⇒ FAIL      ← 预期（对照成立：读数能报"非空"）
[对照] 任意两国交集上限=204 ⇒ 阴性对照读数有效性：可报非零
```

**F3 `probe_fog_player_in_sight`（阴性对照·③ 应报非零）**
```
[t1..t10] 桶=[0,1,2] k1∩k2=0 任意两国交集上限=255~306 AI探索含玩家格=1 AI可见玩家=2 几何内敌=4 过滤后敌=4
[判定] ③AI 不读玩家集：AI 探索桶覆盖玩家格的王国数上限=1（@t1）⇒ FAIL   ← 预期（对照成立）
[对照] … AI 可见玩家上限=2 ⇒ 阴性对照读数有效性：可报非零
```

**读数口径与排除项（`L-35`·逐条）**
| # | 读数 | 口径来源 | 排除项 |
|---|---|---|---|
| R1 | 分桶键集/格数 | `FogBucketKeys()`／`FogExploredCellCount()` | 不数 `Faction` 键；不数空桶 |
| R2 | `k1∩k2` | `FogOverlapCellCount(1,2)`·**前 10 tick 逐 tick 取最大** | 不含第 11 tick 后；不跨 seed/局数 |
| R3a | ③「AI 不读玩家集」 | `IsExplored(k_ai, 玩家单位位置)`（**AI 侧自行揭示才算 true**） | 玩家侧揭示不计入 AI 侧 |
| R3b | AI 可见性 | **雾过滤后** `_nearbyEnemies` 中 `kingdomId==0` 计数（`ProbeNearbyEnemyCountOfKingdom(0)`） | 不数几何半径内总数（未探索/半径外不计） |
| R4 | 空转观测 | 探针侧几何复算「半径内敌数」vs `ProbeNearbyEnemyTotal()`（过滤后条数） | 同 tick 同位置快照 |

> 探针退出码：`PASS` 全绿 = 0；`FAIL` = 1；`enableFog=false` = 2（读数无效，防误用）。F2/F3 退出码 1 属**预期**（阴性对照）。

---

## 四、双端同源对账（MD5 前/后两份表 · `Core` 零改动确认）

- 口径：`harness/Core/**/*.cs` ↔ `Valley Rampart/Assets/_Game/Systems/AI.Core/**/*.cs`（同相对路径）逐文件 `Get-FileHash -Algorithm MD5`。
- 表文件：**改动前** `runs/hh286_md5_before.txt`／**改动后** `runs/hh286_md5_after.txt`（各 34 文件）。
- **前后逐行对照：`changedLines = 0`**（含两侧哈希与 equal 标志）⇒ 逐文件哈希**完全未变** ⇒ 证伪手段通过：**本批确未触碰 `Core`**（`AI.Core` 亦零触碰）。
- 明细：**27 相等 / 7 不等**，且前后一致。

> **⚠️ 列报（存量·非本批引入·不修）**：7 个既已不等的文件＝`Config/ProfessionSnapshot.cs`／`Config/TuningSnapshot.cs`／`Decision/DecisionStructs.cs`／`Decision/L2PostureDecider.cs`／`Decision/L3CommandComputer.cs`／`Memory/FactorContext.cs`／`Memory/HitCooldownStateMachine.cs`；**非行尾噪声**（CRLF 归一化后仍不等，字节数亦不同，如 `FactorContext` : harness 5451 B vs Unity 7214 B；代码行（去注释/空行）差 4~37 行）。方向与在册待回灌项（如 `U4` 野性 7 参数 ⬜待 Unity）疑似相关，**未逐一定性**；本批 `Core` 零触碰故不修 ⇒ **建议策划端另排对账/回灌批裁决**。

---

## 五、位移声明（`C-2` 必出项）

> **本批不改锚、不进池**。理由：①探针三场景为**验证性质**，性质同先例 `harness/Scenarios/escape_eval/e01_envelop_left_heavy.json`（"验证性质、不进现有 suite"）；②进池将变动套件构成 ⇒ 需走 `D-013 §四` 全套五步（生成→位移声明→构成冻结→**重建锚**→门禁②自洽），与"零回归"验收线互相拉扯；③套件 glob 实读确认 `v9_probe/` 不被扫入（[Program.cs:622-627](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Program.cs#L622-L627)）。
> ⇒ 五步中"构成冻结／重建锚"**不适用**；本声明即"为何无需重建锚"的取证。锚目录 `results/baseline/v9/` 本次**只读**（未写入）。

---

## 六、`15_账本` 登记回执（含 `C-3` 字段登记）

落点：[15_训练侧harness与Unity端差距文档.md](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/15_训练侧harness与Unity端差距文档.md)
1. **「一·补」表格「迷雾视野」行**升级：标注 `HH.286（2026-09-15）已升为 per-kingdom 真源`（分桶键／`DZ-158` 解钳制／场景级 `enableFog`）＋ **Unity 适配点 ⬜ 待 Unity**。
2. **新增登记块**（`一·补` 内）：sim 侧真源语义五项（分桶键／视野半径未改／过滤规则／开关语义／`Faction`-`kingdomId` 双轨）＋ Unity 适配点（`VisionSystem` per-kingdom ＋ `kingdomId` 来源＝`UnitController`/`KingdomState`）＋ **`enableFog` 字段登记＝sim-only·Unity 无对应物·零回灌义务** ＋ 门禁读数 ＋ 存量 7 文件观察 ＋ 探针载体与位移声明 ＋ 空转观测。

---

## 七、观测与列报（诚实清单）

### 7.1 施工中补的**清单外缺口** 2 处（`execute-checklist` 铁律 3 列报）
1. **布阵抖动重建 spec 丢字段**（真缺口·首跑即暴露）：`SimWorld.Build()` 把 `SimUnitSpec` 拷贝为 `jittered` 新实例时未带 `KingdomId` ⇒ 首轮探针**全单位退化为 `NONE`**（AI 全盲、桶全空、判定线①FAIL）。已在 [SimWorld.cs:399](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L399) 补 `KingdomId = spec.KingdomId`。
> 该缺口属设计稿 §四 清单未列项（清单列的是"字段/签名/解析"，未列"spec 二次构造点"）⇒ 已按"必需"补并在此列报。
2. **另两处 spec 构造点未派生**：夜战波次单位（[SimWorld.cs:1125](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L1125)）与经济士兵（[DayNightTransition.cs:56](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Economy/DayNightTransition.cs#L56)）均补 `DeriveKingdomId(faction)`（均属 `Sim/`＋`Economy/`，非 `Core`；fog 关时零影响）。

### 7.2 口径细化 1 处（设计稿文字 → 实现）
`SimUnitSpec.KingdomId` 默认值取 **`SimWorld.NoKingdomId`（`int.MinValue`）**，**不是**设计稿 §1.1 字面的 `-1`：因 `-1` 已被"`Monster` 派生桶"占用，用 `int.MinValue` 作"未标注/无王国"哨兵可避免**同值双义**。实际路径上该默认值不生效（`SimScenario.Load` 恒填派生值）。

### 7.3 **观测（重要·供后续判据设计参考）**：雾滤对「单位自身感知」为空转
- **结构推导**：`Tick` 内 `if (_config.enableFog) IterateUnits(RevealUnitVision)` 先于 `IterateUnits(UpdatePerception)`（[SimWorld.cs:915-917](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L915-L917)），且 `RevealUnitVision` 半径**恒等于**感知查询半径（同一表达式 `perceptionRadius × cellSize`）⇒ **任何落在 AI 单位感知半径内的敌人，在同一 tick 必已被该单位自己揭示** ⇒ 雾滤 `RemoveAll(未探索)` **不可能咬掉任何目标**。
- **读数佐证**：三变体「雾滤咬掉敌数累计」**＝0**；F3 中「几何内敌＝4／过滤后敌＝4」（该局 AI 与玩家相距 4~18 世界单位·全在半径内）。
- **结论**：per-kingdom 化后**现行生效面 ＝ 分桶隔离**（判定线 ①③ 已验）；「未探索区敌不可见」这条语义要真正咬合，需另裁"reveal 半径 < 感知半径"之类的语义改造 —— **本批不做**（超范围·不擅自扩面）。已写入 `15_账本`。
- **附带**：`_fogExplored` 旧桶长=2 的越界 bug（`DZ-158`）此前之所以长期无人察觉，正因"雾滤空转＋`enableFog` 默认关"双重掩盖 —— 本批两者均已显式化（开关有入口＋空转有读数）。

### 7.4 存量列报
见 §四 末：双端同源 7 文件既已不等（**非本批·不修**）。另：训练仓工作面存在**非本批脏项** `17_训练作战手册_陷阱图谱与灵活应对.md`（+1 行，会话开工前即在）⇒ **未纳入本批 commit**。

---

## 八、守线证据与文件清单

**零触碰面核对**：`harness/Core/**`（含 `ProfessionSnapshot`／`AttentionSystem` 等）零改动（MD5 前后一致佐证）；`harness/KingdomBrain/**` 零改动；`champion/`／`Holdout/`／`AGENTS.md`／`FactorRegistry`／`harness.csproj` 零改动；主仓 `Assets/**` 零改动。

**本批训练仓改动**（`git diff --stat`·无 CRLF 全量膨胀）：
```
15_训练侧harness与Unity端差距文档.md      |  12 +      （行26 升级 ＋ 新增登记块）
harness/Economy/DayNightTransition.cs    |   1 +
harness/Program.cs                       | 123 ++++
harness/Sim/SimBrain.cs                  |  21 +-
harness/Sim/SimScenario.cs               |  24 ++
harness/Sim/SimUnit.cs                   |   2 +
harness/Sim/SimWorld.cs                  |  94 +++---
8 files changed, 249 insertions(+), 19 deletions(-)
```
＋ 新增场景 3 件（`harness/Scenarios/v9_probe/probe_fog_k2_far.json`／`probe_fog_k2_overlap.json`／`probe_fog_player_in_sight.json`）。

**证据载体（未跟踪·留档不删）**：`runs/hh286_gate2_bench.log`（门禁②全文）／`runs/hh286_gate3_determinism.log`（门禁③）／`runs/hh286_md5_before.txt`／`runs/hh286_md5_after.txt`／`results/20260915_123052_bench/`（候选 report/verdict/holdout）／`runs/fog_probe/*.jsonl`（探针局日志）。

**提交纪律**：具名 `git add`（不含 `results/**`、`runs/**`、非本批脏项）·**未 push** · 主仓 `多Agent交接/执行端/HH.287_..._交付报告.md` ＋ `_交接索引.md` 行。

---

## 九、待办 / 请主策划端处置

1. **验收本报告**（门禁四件全绿 ⇒ 建议判 `G3-1` sim 侧成立；Unity 侧仍待后适配批）。
2. **§7.3 空转观测**是否升格为独立裁定项（若要语义咬合，需改 reveal/感知半径关系 ⇒ 新批）。
3. **§四 存量 7 文件双端差异**是否另排对账/回灌批（涉 `sim-sync` 铁律①的现状缺口）。
4. **本批不做**（已裁）：`RunTrain` 硬编码 `BuildSuiteV8` ＋ `champion` json tuning 段缺野性 9 参数（主策划端另排挂账）。
5. **Unity 后适配批**：按 `15_账本` 登记的五项语义 ＋ `VisionSystem` per-kingdom ＋ `kingdomId` 来源（`UnitController`/`KingdomState`）。

---

> 落盘：训练端（训练师）｜2026-09-15｜**取号 HH.287（水位线 286→287）**｜**未 push**

---

## 策划端裁决区（`D734`，2026-09-15，主策划端）

> 裁决文号 **D734**（`0.6 §二百六十二`）· Gate=`G3-1` · **`HH.287`（第二步施工）＝✅ 验收成立** · **但训练端"空转观察"确立 `G3-1` 设计缺口 ⇒ 另裁（`P-008`）**

### 1. 施工三块＝✅ 全落实（逐点实核）

| 块 | 实读 |
|---|---|
| ① §四 改造面 | `SimWorld.cs:224` **`_fogExplored` → `Dictionary<int, HashSet<long>>`** ✅（`:223` 键语义文档化：`0`=玩家／`>0`=AI国／`-1`=野怪／`NoKingdomId`=未标注）；`:249` `IsExplored(int kingdomId, …)` ✅；`:269` `RevealCircle(int kingdomId, …)` ✅；`:287` `RevealUnitVision` 用 `u.KingdomId` ✅；**`:258-266` `FogBucket` 按需建桶·无长度上界** ⇒ **`DZ-158` 桶长钳制已解** ✅；`SimBrain.cs:393-394` 雾滤改 `_self.KingdomId` 而 **`myFaction` 保留**供 `QueryNearby`（`:388-389`）⇒ **M4 双轨正确** ✅；`SimUnit`/`SimUnitSpec.KingdomId` ＋ 场景解析与 `DeriveKingdomId` 兜底 ✅ |
| ② 三变体探针 | `harness/Scenarios/v9_probe/` 三 JSON（各 16 行）✅ 落**探针目录·不进池**；F1 `far` ①②③ 全 PASS（桶键 `[0,1,2]`·`k1∩k2=0`·AI 覆盖玩家格 `0`）／F2 `overlap` `k1∩k2=204`（②阴性对照成立）／F3 `player_in_sight` 覆盖 `1`·AI 可见玩家 `2`（③阴性对照成立）✅ |
| ③ 三件前置 | 场景级 `enableFog`（`:246` `FogEnabled => _config.enableFog`·缺省 false）✅／只读 CLI `fog-probe`（`Program.cs` +123）✅／只读访问器 `FogBucketKeys():292`·`FogExploredCount:301`·`FogOverlapCount:306` ✅ **未写 `report.json`** ✅ |

**零回归有保证**：`:251` `if (!_config.enableFog) return true;` **短路保留** ✅（另 `:252` `NoKingdomId ⇒ false`／`:253` 无桶 ⇒ `false`）。

### 2. 门禁四件＝✅ 全绿（采信读数·策划端无法复跑）

①`-warnaserror` **0 警 0 错**（最终提交树复跑）／②对照 **读锚不写锚**·锚键集实读 **148/148**·逐共有场景 **Δ max 0.000**（>0.0005 者 0 项）·`NoRegression=True`·`0.422→0.422`／③`determinism` **6/6 逐字节一致**（3 锚＋3 新探针）／④`holdout` **0.498→0.498**（`Δ0.000`·`regressed=False`）。
- **`verdict=rejected` 判读确认**＝**"总分未升"非退化** ✅（本批非提分目标）。
- **双端同源**：MD5 前/后表**逐行一致**（`changedLines=0`）⇒ **`Core` 零改动确认** ✅（`D731` C-1 裁定预期达成）。
- **位移声明**已出（不改锚·不进池）✅；`15_账本` 已登记（迷雾行升级＋Unity 适配点＋`enableFog` sim-only 字段·零回灌义务）✅。

### 3. ⭐ 训练端"空转观察"＝本批最重要发现 ⇒ **确立 `G3-1` 设计缺口**（另裁）

**根因坐实（策划端实读复核）**：`RevealUnitVision`（`:287`）用 **`u.Profession.perceptionRadius`** 标记探索，而 `SimBrain`（`:384`）用**同一 `perceptionRadius`** 查询敌人，`:394` 再用 `IsExplored` 过滤 ⇒ **"感知半径内的敌人"必然落在"本 tick 刚标记的格"内** ⇒ **过滤咬掉数恒 0** ⇒ **雾滤对"单位自身感知"这条路径＝结构性空转** ✅（训练端观察正确）。

**⇒ 对判定线 ③ 的重新定性**：F1 中「AI 覆盖玩家格 = 0」**pass 但机制不同** —— 它是**几何性**的（远距 ⇒ 玩家本就不在 AI 感知半径内），**不是雾滤性的**。**⇒ 现行生效面 ＝ 分桶隔离**（k1/k2 探索集互不可见）；**"看不到已探索区外的敌人"这一语义未生效**。

**⇒ 三取向与裁定**：

| 取向 | 内容 | 裁定 |
|---|---|---|
| **A** | **接受现状 ＋ 改口径**：交付物＝**分桶隔离**（各王国视野互不可见）；判定线 ③ 口径改为「**k1/k2 探索集隔离 ＋ 各王国独立不可见**」 | **✅ 推荐（拟裁 A）** —— 分桶隔离本身即有交付价值（AI 各国各看各的）＋B/C 均超本批范围＋**Unity 后适配必须对齐同一口径** |
| **B** | 探索集**脱离感知半径**（如按区块标记 ⇒ 表达"我到过的区域"） | 需**独立设计稿**（引入区块/记忆语义）⇒ 超本批 |
| **C** | 感知改「探索集 ∧ 感知半径」双条件 | **结构性不可达**：除非"探索标记半径 < 感知半径"，否则与现状同为自标记自消费 |

**⇒ 落**：`3.1 §七` 发 **`P-008`**（设计层待裁·跨域·训练端已列报）；**Unity 后适配批（下批）的口径声明须含本项**（即 Unity 侧 `VisionSystem` 亦按 A 口径：分桶隔离为准，不承诺"雾滤咬人"）。

### 4. 其余列报裁定

1. **清单外补 2 处**（布阵抖动重建 spec 丢 `KingdomId` ⇒ 全单位退化 NONE·已修；夜战波次＋经济士兵 spec 未派生·已补）⇒ **合理**（首跑即暴露·实读见 `:398`/`:1124` 注释）✅
2. **存量 7 个 MD5 既已不等**（双端 34 文件中·非行尾噪声·疑与在册 `U4` 类待回灌相关）⇒ **收下建议·另排对账批**（不属本批·`D734` 记）；**不阻本批验收** ✅
3. **训练仓 2 个脏项**（`17_作战手册`／`AttentionSystem.cs` stat-dirty）⇒ **记**（非本批·须清，防混入下批 commit）
4. **取号与落盘**：`HH.287` 先登记后落盘 ✅；训练仓 `479c9ed7`（**10 files +306/−19**·实核）、主仓 `e81af209`（2 files）·**未 push** ✅

### 5. 后续

- **`G3-1` sim 侧 ＝ ✅ 完成**（`DZ-158` 闭环）。
- **Unity 后适配批**：另批（`3.1 §六 G3-1` 已标"另批"）⇒ 口径按 **A 案**（待 `P-008` 定案）。
- **`HH.284` 第二步 A 案**（执行端·已放行 `D733`）与本批并行，无耦合。
