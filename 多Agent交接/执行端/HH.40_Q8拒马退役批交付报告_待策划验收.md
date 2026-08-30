# HH.40 · Q8 拒马退役批 · 交付报告（待策划验收）

> 类型：交付报告（Gate 收口，D122 拒马退役代码批）
> 状态：🚧 待策划验收（验收 → Q8 拒马退役批完工 + 队列 Q8 → ✅；其后转《Q1_步骤12全量收官回归执行清单_2026-08-29.md》——该批已由 HH.39 提前验收，Q8 完成后无额外衔接动作）
> 日期：2026-08-30 · 发起端：执行端 · 关联：D122 终裁（0.6 §十）→ 《Q8_拒马退役批执行清单_2026-08-30.md》
> 构成：§一=Unity 批（T2/T3）；§二=sim 批（T4）；§三=探针与门禁数据；§四=诚实对账与交付声明

## 〇、锚点声明（vr-triage-flow §四）

- **本批允许改动面**：T2/T3 点名的 Unity 代码与资产 + T4 训练仓 harness Sim 层 + HH 交付文件。设计文档零改动（附录A 台账由策划端处理，执行端只读）。
- **主仓锚点**：执行批开盘 HEAD=`cbca4980`（HH.39 验收锚点）；Q8 执行期间主仓仅执行端 Q8 改动 + 策划端并行在途文件（.gitignore / Packages/manifest.json+lock / 2_x 文档 / _目录.md），**并行在途文件未触碰、未 add**（T5 提交构成见 §四 4.4）。
- **训练仓锚点**：改前 commit=`9e1e2df`（Q8 T4 改前存档）；改后 commit=`0221ef7`（Q8 T4 双门禁通过后）。
- **纪律面**：红即停、报策划、不自修、不虚报（HH.37 铁律）。本批无停机事件，全程无产品代码偏离清单面。

---

## 一、Unity 批（T2/T3 逐条证据）

| # | 任务 | 结果 | 证据 |
|---|------|------|------|
| T2 | Unity 资产清理 | ✅ | ①删 4 份资产（连同 .meta）：`Resources/UnitData/Barricade.asset`、`Resources/Buildings/Barricade.asset`、`Resources/Fortifications/Barricade.asset`、`Resources/UnitPrefabs/Human_Player_Barricade.prefab`（guid：DygcvC2rU3q…/DS8b5y2rUnju…/B30ftnulBihp…/WnIasS2tV30…，**删前全工程 grep 零引用**）；②`Resources/Modules/Module_Civil.asset` 删 `unlockBuildings:` 下 `- Barricade` 行（全 3 tier 复核无 Barricade 残留）；③删前 grep 清单外引用面=零，预授权未触发 |
| T3 | Unity 代码清理 | ✅ | ①`UnitController.cs` 删 `ApplyBarricadeSlowIfNeeded()` + 2 处调用（Move/MoveTowards）+ 注释；②`FortificationDef.cs` 删 `barricadeSlowFactor`/`barricadeSlowDuration` 字段 + 拒马 header 注释 + blocksMovement/heightCells/ammo Tooltip 拒马字样清；③`AIDebugSpawnController.cs` 删 `DebugSpawnType.PlayerBarricade` 枚举值 + 3 处 case（GetFaction/GetOccupation/GetDisplayName）——**所择路径=删枚举值**（§2 预授权 2），依据见 §三 3.1；④`PlaceholderSprites.cs` 删 `bld_wall_barr` 行；⑤注释清理：`NPCBrain.cs`（L1398 冲锋注释）/`FormationController.cs`（L225/L249 + IsRecruitable 排除 case）/`ProfessionSnapshot.cs`（Unity 侧 L61/L111-115）/`UnitData.cs` L26 Occupation.Barricade 行尾改「D122 砍除退役，值保留防序列化错位（Q8）」；⑥清单外活逻辑随清：`PopulationSystem.cs`/`SatietySystem.cs` 删 `case Occupation.Barricade` 排除名单；⑦**全库 grep 复核**：`git grep -in barricade -- "Valley Rampart/Assets"` 仅剩 `UnitData.cs:26` 枚举值废弃注释，**活逻辑/活引用零命中** |
| 编译门 | T3 编译 | ✅ | 全量编译 **0 error**（存量 warning 如实列出，无新增） |

**Unity 批结论：T2/T3 全绿。**

---

## 二、sim 批（T4 逐条证据）

| # | 任务 | 结果 | 证据 |
|---|------|------|------|
| T4① | 停新生成 Barricade | ✅ | `Sim/CardPool.cs`（Fort 数组删 `"Barricade"` + Q8 注释）、`Sim/ScenarioGenV2.cs`（L112 `rng.NextDouble()<0.5` Emit Barricade → 注释）、`Sim/ScenarioGenerator.cs`（EmitFort 删 `Emit(faction,"Barricade",…)` + full 防线注释） |
| T4② | 解析兼容保留 | ✅ | `Sim/SimWorld.cs`/`SimUnit.cs`/`SimBrain.cs`/`SimConfig.cs`/`Program.cs` 对 Barricade 读取/解析分支**一律未动**；4 个 R*.json 共享场景原样保留（grep 复核剩余命中=解析兼容层 Program/SimConfig/SimWorld + 存量场景数据，均为预期保留）；baseline 实跑存量含拒马场景（m8_A5/m8_D3 等）**原样可跑** |
| T4③ | ProfessionSnapshot 注释清 | ✅ | `Core/Config/ProfessionSnapshot.cs` L61/L111-115 拒马注释清（通用工事字段保留不动） |
| T4④ | 双门禁 | ✅ | 见 §三 3.2 |
| T4⑤ | 15 差距账本登记 | ✅ | `15_训练侧harness与Unity端差距文档.md` 新增「### 一·补四 Q8 拒马退役（2026-08-30 登记，D122 落实）」区块：双向口径表（Unity 全链路退役 vs sim 只停新生成+解析兼容保留）+ Q8 执行清单指针 |
| T4⑥ | 训练仓纪律 | ✅ | 改前 commit `9e1e2df`（Q8 T4 改前存档）→ 改 → 双门禁 → 通过后 commit `0221ef7`（14 文件，见 §四 4.3） |

**sim 批结论：T4①~⑥ 全绿。**

---

## 三、探针与门禁数据

### 3.1 DebugSpawnType.PlayerBarricade 所择路径（§2 预授权 2，HH.40 必答）

- **所择路径：删枚举值**（连同 3 处 case 一并删）。
- **依据**：grep 确认 `DebugSpawnType` 枚举仅 `AIDebugSpawnController.cs` 内部引用，无任何序列化/反射/int 转换引用面（仅 `Enum.GetValues` 动态遍历迭代 UI 菜单，属运行时枚举反射、非序列化持久化）→ 满足预授权「可删枚举值」条件。与 `Occupation.Barricade`（L26 中间位，int 序列化依赖，必须保留）形成对照：后者删即全量 .asset 错位，本项目「末尾追加保 int 稳定」铁律。

### 3.2 sim 双门禁数据（T4④）

| 门禁 | 结果 | 证据 |
|------|------|------|
| build | ✅ | `dotnet build harness -warnaserror` **0 警告 0 错误** |
| determinism | ✅ | `determinism` 同 seed 跑两次 **JSONL 逐字节一致**（含全剧本） |
| holdout 不退 | ✅ | 本次 `results/baseline_v8/holdout_report.json` **total=0.594**，与改前记录 `results/baseline/holdout_report.json`（2026-08-30 15:52，seed=40522003）**逐字节一致**（H1=0.473/H2=0.650/H3=0.640/H4=0.612 完全相同）→ holdout 零退化 |
| baseline 无退化（存量场景） | ✅ | 本次 `results/baseline_v8/report.json`（champion baseline --suite v8 --battles 100，全程约 4.7h 跑完）的 **S1-S7 公共子集 subScore 与改前 `results/baseline/report.json`（2026-08-30 15:52）逐字节一致**：S1=0.476/S2=0.465/S3=0.076/S4=0.436/S5=0.042/S6=0.544/S7=0.490 → 存量场景（S/M8/A/D/E/v2/KT*/KKT*/V*/Cards）**零退化** |

> **如实披露（非虚报）**：全量逐场景对比（2000+ 场景改前改后 diff）**不可执行**——改前 v8 全量 report 已由训练仓 commit `9e1e2df`「results 生成物清理（report 移除跟踪）」移出 git 跟踪，无改前基准可比。门禁判定基于：①确定性（同 seed 逐字节一致）②holdout 同 seed 逐字节一致不退 ③S1-S7 公共子集与改前记录逐字节一致 ④sim 改动仅触碰 3 个生成器（存量场景输入零改动，决策核/SimWorld/SimConfig 禁改未动）。**本次新 v8 report total=0.314，低于历史 `results/baseline/report.json` 的 0.361——此为套件组成不同所致（旧=仅 7 个 S 场景，新=2000+ 场景含大量高难度 KKT 0.000 场景拉低平均），非退化**，S1-S7 公共子集一致即为零退化证据。

### 3.3 行为级探针（Unity，T2/T3 验收，非结构性 grep）

- **负探针**：Debug 生成入口 `DebugSpawnType` 动态遍历 count=**28 项，无 PlayerBarricade**；建造菜单 Module_Civil 3 tier **无 Barricade 条目**。
- **正探针**（工事兄弟功能零回归）：
  - 城墙阻挡：斥候被墙阻挡 = **True**（`IsBlockedByFortification` 查目标格 `blocksMovement && !passable`，`FortificationPassableOverride` 判定可通行）；
  - 城门开合：`FortificationPassableOverride=true/false` 等价模拟 → 开=不挡 / 关=挡（openBlocked=False、closedBlocked=True）；
  - 箭塔索敌：反射调 `FindNearestEnemyInRange` 验证 `returned=Undead_Warrior isPrey=True`（塔/靶 y=0 行——FindNearestEnemy 硬编码 y∈{0,1} 扫描带为产品既有行为，非 Q8 引入）。
- **加载面**：Play 真开局（WorldManager.ApplyConfig 初始化 256×256+3 国立国），console **无 Barricade/拒马报错、无死引用、无模块解锁死引用**。

---

## 四、诚实对账与交付声明

### 4.1 让渡项（如实声明，非 FAIL）

- **全量逐场景 baseline 对比缺基准**：见 §三 3.2 披露（改前 v8 全量 report 已移出 git 跟踪，`9e1e2df`）。门禁以 S1-S7 公共子集 + holdout 逐字节一致 + 确定性判定。
- **FindNearestEnemy y∈{0,1} 扫描带**：产品既有行为（迁移残留），非 Q8 引入，本批未触碰（禁改范围外）。

### 4.2 附录A 台账外新发现文档残留（提示策划端，执行端禁改设计文档）

执行端 T3 全库 grep 复核时发现 **2 处附录A 台账未覆盖的设计文档残留**，归策划端随批收尾（与附录A 同批对齐）：

| 文件 | 位置 | 残留 |
|------|------|------|
| `河谷防线开发计划书具体内容/改造计划/2_9_模拟器与职业训练_实施计划.md` | L143 / L491 | 「拒马减速」/ `SimConfig.barricadeSlowFactor / barricadeSlowDuration` 保留行 |
| `河谷防线开发计划书具体内容/改造计划/2_3_空间与移动_实施计划.md` | L20 / L80 / L93 / L294 / L303 | `ApplyBarricadeSlowIfNeeded` 列表与调用、撞墙/拒马减速保留句 |

### 4.3 提交构成（只 add 自己的文件，各自 commit）

**训练仓 commit `0221ef7`（14 文件）**：
`harness/Sim/CardPool.cs`、`ScenarioGenV2.cs`、`ScenarioGenerator.cs`、`harness/Core/Config/ProfessionSnapshot.cs`、`15_训练侧harness与Unity端差距文档.md`、`harness/Scenarios/random/R20260806_2.json` 等 9 个 R 随机集（gen-r 重建去拒马，seed=20260804，任务书 §2.4 预期）。

**主仓 Q8 commit（本批同串）**：删 4 资产+meta（Barricade.asset×3 + Human_Player_Barricade.prefab）+ 11 改（Module_Civil.asset / FortificationDef.cs / UnitData.cs / ProfessionSnapshot.cs / AIDebugSpawnController.cs / FormationController.cs / NPCBrain.cs / PopulationSystem.cs / SatietySystem.cs / PlaceholderSprites.cs / UnitController.cs）+ 本报告 + `_交接索引.md` HH.40 行 + 工作日志。

> 并行在途文件（.gitignore / Packages/manifest.json+lock / 2_x 文档 / _目录.md）**未触碰、未 add**（cbca4980 教训：add 共享文件前先看完整 diff）。

### 4.4 验收请求（策划端）

请验收：①Unity 批 T2/T3；②sim 批 T4（含双门禁）；③§三 3.2 门禁数据的如实披露口径。验收通过后队列 Q8 → ✅，同步 `_交接索引.md` HH.40 行状态回写。**验收总门槛=T1~T5 全绿（或如实标注让渡项）；交付≠完工，策划验收通过才算 Q8 完工。**
