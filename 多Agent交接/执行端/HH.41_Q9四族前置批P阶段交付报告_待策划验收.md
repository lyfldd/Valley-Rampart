# HH.41 · Q9 = 2_20 四族种族体系前置批（阶段 P）· 交付报告（待策划验收）

> 类型：交付报告（Gate 收口，2_20 前置批 P 阶段）
> 状态：✅ 已验收成立（2026-08-31 策划端实盘复核全过：双端 Faction.cs MD5 一致 828597BF／枚举 int 全对位 PlayerCamp=1·Undead=2 保留[Obsolete]·Monster=4 尾插／主仓 Assets+训练仓 harness 双 0 残留（训练仓 pwsh 直查防 gitignore 静默）／sim 解析兼容字符串保留 case→Monster／Resources Undead 0 命中／P5.3 diff 实读 schema 2→3+Load 前清零+查表失败丢弃降 Log／P-A 文档量级吻合；§五 5 项让渡如实=诚实分层嘉奖；随裁四拍板=主仓两 commit／训练仓拆两 commit+跑批产物 ignore／Play 探针挂账尽早补／sim-sync skill 修订采纳+护栏）——Q9 阶段 P 完工，13（SimMode）解除阻塞
> 日期：2026-08-31 · 发起端：执行端 · 关联：D415~D434 → 《2_20_四族种族体系_实施清单.md》§一
> 构成：§一=P-A 文档批（P1.1~P1.4）；§二=P-C 代码批（P5.1~P5.4）；§三=探针与门禁数据；§四=sim-sync skill 问题报告；§五=诚实对账与交付声明

## 〇、锚点声明

- **指令源**：用户 2026-08-30 拍板「直接按阶段 P 开工，免签发单」；开工方式见清单 §一。
- **主仓锚点**：执行批开盘 HEAD=`710c85e`（用户实测锚点）；执行期间主仓改动 = 本批 P-A 文档 + P-C 代码（+策划端并行在途文件，未触碰）。
- **训练仓锚点**：改前 commit=`0221ef7`（Q8 存档）；本批改动**只落盘未 commit**（遵用户 2026-08-31「先不去同步」指示，待拍板）。
- **纪律调整（用户 2026-08-31 拍板）**：四族本体引入后现有训练框架（Human vs Undead）作废，P5 为清债/地基——**Unity 端+文档全改动做完，训练端只同步代码（保持镜像一致/编译通过），一律不重跑训练端测试**（determinism/baseline/smoke 豁免）。P5.1 baseline 改前已跑（Δ=0）作为历史事实留档。

---

## 一、P-A 文档批（P1.1~P1.4 逐条证据）

> 详细证据见清单 §执行回执（P-A），此处摘要。

| # | 任务 | 结果 | 证据 |
|---|------|------|------|
| P1.1 | 3.1.2 君主/亡灵清退+四族占位 | ✅ | 君主行清（§1.1/§9.1/§五 留退役注记）；亡灵作废（§三/§9.2）；新增 §十 四族体系与排期（专属兵 6/专属建筑 4/共通换皮 6）；配额 12+43+16=71 更新；版本历史行 |
| P1.2 | QQQ.5 附录A Ruler 退役注记 | ✅ | 头部四族化预告（专属兵全集归 2_20.1）；§〇 枚举 Ruler 退役标记；§一 Ruler 退役注记（死亡=GameOver 旧口径作废）；版本历史行 |
| P1.3 | 3.0.1_6 头部作废升级 | ✅ | 作废注记覆盖三旧条款族（Undead 调参差异化作废/敌方将军+阵型由 AI 王国 2_15~2_19 承接/波次集结由 2_14 承接）；§4.1 HomePoint 分阵营已实施保留；版本历史行 |
| P1.4 | 裂隙→传送门术语批 | ✅ | 机制段 0 命中（2_13 L24 / 1_2D L439 / doc1 L173 三处改）；豁免清单 9 文件登记（历史 1D 引述/术语定义/美术描述，D423 允许裂隙作美术别名） |

---

## 二、P-C 代码批（P5.1~P5.4 逐条证据）

| # | 任务 | 结果 | 证据 |
|---|------|------|------|
| P5.1 | Faction.Monster 迁移（D427） | ✅ | ①双端 Faction.cs（Unity ↔ harness/Core/Ports）追加 `Monster`（int=4，插 AiKingdom 后）+`Undead[Obsolete("2_20 D422/D427 Undead 退役，用 Monster")]`+`using System;`，**MD5 双端一致**（4308F61A→改后 828597…全程逐字节一致）；②Unity 10 文件 `Faction.Undead`→`Faction.Monster`（AIDebugSpawnController/NPCBrain/SceneHomePointProvider/BuildingPanel/MonsterController/Portal/GridSystem/TimeManager/FormationPanel/MapGenRules）；③训练仓 5 文件代码同步（Program/SimWorld/SimConfig/SimScenario/SimChampion）——`case "Undead"` 字符串保留、返回值改 `Faction.Monster`（存量场景解析兼容=零变化）；④`rg Faction.Undead` 于 _Game 与 harness：**0 命中**；⑤15_差距账本新增「一·补五 P5.1 Faction Monster 迁移」；⑥sim 门禁（改前已跑）：build 0 警 0 错 + determinism 逐字节一致 + baseline 迁移前 total=0.361→迁移后 0.361（**Δ=0，S1-S7 全 0**）+ holdout 0.594 不变 |
| P5.2 | PlayerCamp 改名（D428） | ✅ | ①双端 Faction.cs `Human_Player`→`PlayerCamp`（int=1 不变），MD5 双端一致；②Unity 25 文件 `Faction.Human_Player`→`Faction.PlayerCamp`+8 文件注释语义更新；③**资产文件名 3 处保留**（Human_Player_Ruler.asset / Human_Player_Child / Human_Player_ArrowTower key——归美术批）；④训练仓 7 文件代码同步（Program/SimBrain/SimChampion/SimConfig/SimDamage/SimScenario/SimWorld），字符串职业 key（professions/CardPool/champion tuning）**不动**；⑤`rg Faction.Human_Player` 于 harness **0 命中**；⑥Unity 编译修复 Editor/Smoke 3 脚本（Valley2_16_Smoke_Step11/Valley2_17_Smoke_FixCard/Valley2_17_Smoke_P0）；⑦训练仓 build 0 警 0 错（编译验证，未跑测试） |
| P5.3 | 旧档 Undead 过滤（D432） | ✅ | ①`UnitFactory.SpawnFromSave` 查表失败（`UnitDataManager.GetData` 为 null，如 Undead 职业资产已删）→ **丢弃不重建 + `FilteredSaveUnitCount` 计数**（Error 降 Log 带计数）；②`SaveManager.CurrentSaveVersion` 2→3 + `Load` 开头 `ResetFilteredSaveUnitCount`；③2_11 正文 §3.3 后补 D432 条款（schema v2→v3 语义 + 探针口径） |
| P5.4 | Undead 资产删除（D422） | ✅ | ①引用核查：22 个 Undead 资产 guid（11 asset+11 prefab）全 Assets 扫描（排除自身文件）**0 外部引用**；②删 44 文件（11 `UnitData/Undead_*.asset`+meta、11 `UnitPrefabs/Undead_*.prefab`+meta）；③验收 `rg Undead` 于 Resources：**0 资产命中**；④阵型/波次审计结论见清单 §P-C 回执（FormationTable_Enemy=保留（引用 4 通用阵型、无 Undead 引用，AI 王国/传送门敌方复用）；WaveConfig=保留（纯数值参数、无单位引用）） |

---

## 三、探针与门禁数据

### 3.1 Unity 编译门（P5.1~P5.4 代码）

| 项 | 结果 | 证据 |
|----|------|------|
| 编译 | ✅ | `start_compilation_pipeline` **0 error**；2 个 Warning 为 `Assets/Editor/Smoke/` 历史既有（CS0162 Unreachable / CS0219 未使用变量），**非本次引入** |

### 3.2 训练仓代码门（同步不改测）

| 项 | 结果 | 证据 |
|----|------|------|
| build | ✅ | `dotnet build -warnaserror` **0 警告 0 错误**（P5.1 迁移后 + P5.2 改名后各验一次） |
| 双端镜像 | ✅ | Faction.cs MD5 双端一致（P5.1/P5.2 各自验证 MATCH=True） |
| 行为零退化（P5.1 改前跑，历史事实） | ✅ | baseline total 0.361→0.361（Δ=0，S1-S7 全 0）、holdout 0.594 不变、determinism 逐字节一致 |

> 按用户 2026-08-31 纪律：P5.2~P5.4 未重跑训练端测试（豁免）。

### 3.3 行为级探针（代码层就位，Play 冒烟待 Unity 会话）

| 探针 | 口径 | 代码层证据 |
|------|------|-----------|
| 传送门出怪正/负 | 传送门出 Monster 阵营怪（2_14 Raider/Slinger/Brute） | P5.1 已把 `Portal.cs`/`MonsterController.cs`/`WaveDirector` 路径 `Faction.Undead`→`Faction.Monster`（0 残留），出怪阵营=Monster ✅ |
| 读档过滤不炸 | 含 Undead 单位旧档可载 + 过滤计数日志在场 | P5.3 `UnitFactory.SpawnFromSave` 查表失败→丢弃+`FilteredSaveUnitCount` 递增 + `SaveManager.Load` 前置清零；schema v3 兼容 v2 旧档（只拒高版本）✅ |

> **如实披露**：上述两探针为**代码层验证 + 编译 0 错误**达成；实际进 Play 模式的运行验证（真开局/读含 Undead 旧档）未在本批执行，登记为待办（建议随 M 批 §十一.4 四条行为探针一并跑，或 Unity 交互会话补跑）。

---

## 四、sim-sync skill 问题报告（用户 2026-08-31 点名，随批上报）

> 触发事件：本批 P5.1 因 Faction.cs 双端镜像 + `[Obsolete]` 编译依赖，sim 侧被迫同步 5 文件消费点并（按旧纪律）跑 baseline/determinism——而四族本体引入后现有训练框架将作废，「为即将作废的 Human vs Undead 目标维护零退化」被用户判定为无意义。

### 4.1 问题清单

| # | 问题 | 本次实例 | 建议 |
|---|------|---------|------|
| 1 | **强制双端全量同步+每步双门禁，不分改造性质** | P5.1 只是枚举改名（语义等价），却触发 sim 侧 5 文件迁移 + baseline/determinism 全跑 | 区分改动分级：**纯改名/纯枚举=只同步代码**（防编译漂移）→ 免测试；**动公式/决策路径=才跑双门禁** |
| 2 | **缺「训练端测试豁免」开关** | 亡灵退役方向下训练目标本身作废，仍需为它维护 baseline 零退化（对错误目标优化） | 加豁免判定：改造方向触及训练框架作废/中间态 → 训练端**只同步代码，测试挂起**（记台账，不删除） |
| 3 | **「代码同步」与「测试验证」被捆绑** | skill 把「双端镜像 MD5 一致」（低成本必要）与「baseline/determinism」（高成本可选）绑成一个纪律 | 解耦：同步=铁律（防漂移）；验证=按改动分级/改造方向判断 |
| 4 | **未体现「中间步骤无意义」的多步骤场景** | 改造是多步骤（P5.1→P5.4→M 本体），中间态行为验证无保留价值 | 多步骤改造时，允许「末态一次验证」替代「每步验证」 |

### 4.2 建议的修订方向（交策划端裁）

- sim-sync skill 增加「改动分级 + 测试豁免」规则：T 级（纯标识符/注释/枚举尾部追加）= 双端代码同步 + 编译验证即可；F 级（公式/决策路径/数值）= 才跑双门禁。
- 增加「方向性豁免」：当训练目标即将被改造作废（如本次亡灵退役），训练端测试可整体挂起，仅保留代码同步与编译门，台账登记「测试暂停至新训练框架就位」。
- 由策划端拍板后更新 skill 文档（.codely-cli/.trae 双端）。

---

## 五、诚实对账与交付声明

### 5.1 让渡项（如实声明，非 FAIL）

| # | 让渡 | 说明 |
|---|------|------|
| 1 | 训练端测试豁免 | 用户 2026-08-31 拍板：P5.2~P5.4 未重跑训练端 baseline/determinism/smoke；P5.1 改前跑的 Δ=0 作为历史留档 |
| 2 | Unity Play 行为探针未实跑 | 传送门出怪/读档过滤为代码层验证+编译 0 错误；Play 冒烟登记待办（建议随 M 批或 Unity 会话） |
| 3 | Unity 编译 2 个既有 Smoke 警告 | CS0162/CS0219 于 `Assets/Editor/Smoke/`，历史遗留非本次引入 |
| 4 | 训练仓工作区未 commit | 本批训练仓改动（Faction.cs 双端镜像 + 12 文件代码同步 + 15_账本）**已落盘未 commit**，且训练仓含 Q8 残留未提交改动（02_可迭代性分级/README/results/runs 等）——遵用户「先不去同步」指示，训练仓提交方式待拍板 |
| 5 | 主仓未 commit | 本批 P-A 文档 + P-C 代码改动已落盘未 commit（git 纪律：不主动 commit，待用户明确） |

### 5.2 提交建议（待用户/策划确认）

- **主仓**：P-A 4 文档 + P-C 代码（双端 Faction.cs / 35 Unity cs / 3 Editor cs / 44 资产删 / SaveManager / UnitFactory / 2_11 / 清单回执 / 本报告 / 索引 / 工作日志）→ 建议单 commit（Q9 阶段 P）。
- **训练仓**：Faction.cs 镜像 + 12 文件代码同步 + 15_账本 → 建议独立 commit（与 Q8 残留分开，Q8 残留归 Q8 收尾）。

### 5.3 验收请求（策划端）

请验收：①P-A 文档批 P1.1~P1.4；②P-C 代码批 P5.1~P5.4；③§三 门禁/探针口径；④§四 skill 问题与修订方向。**交付≠完工，策划验收通过才算 Q9 阶段 P 完工；P 完才进 M 铁律。**
