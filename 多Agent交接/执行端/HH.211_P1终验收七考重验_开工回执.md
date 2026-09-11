# HH.211｜P1 终验收 · 七考重验 · 开工回执（协议核对闸）

> 执行端（TraeCode·Unity 轨）· 2026-09-11
> 状态：🟡**起跑中**（协议复述＋seed 报备＋容器级改动列报 已落；按 **D661** 放行＋用户「正式起跑」指令执行）
> 锚点：**HH.203 任务书（D657／0.6 §一百八十六）**为唯一权威 ｜ 前置＝**HH.208 ⑰收口批验收成立（D661）**｜ 协议母本＝HH.186 回执（**D647 裁 B′**）＋HH.122 §三·补（D585 正门主线）
> 取号：遵 D640 #10（⚠️**撞号处置**：原取号 **HH.210**（独立 commit `1b861ef`，20:45:26）撞策划端 **`f060c5e`**（20:41:15，美术端原 HH.188→HH.210，**先落盘**）⇒ 按 vr-id-ledger「**先落盘者保留，后取者顺延**」**顺延 HH.211**（勘正独立单行 commit **`e5b91e2`**，水位线 HH.210→HH.211））；完成报告届时按实时水位线另取（**禁预留**）
> 判定线（D589）：**净状态下「≥2 个 AI 王国自主推进至军事期」＝ P1 终验收 PASS**

---

## 一、协议六项逐条复述（含实读锚点）

| # | 协议项 | 执行端复述（实读） |
|---|---|---|
| 1 | **正门唯一入口** | [Valley_HH80_Run.cs:76](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs#L76) `yield return TestHarnessApi.EnterTestRun(cfg)`；**禁裸跑 GameScene／禁 `SmokeApi.EnterGame` 裸局**（L-17） |
| 2 | **守卫全开＋玩家真实局态** | 正门内置（判负封死 ON／T11 野怪静默 ON），**T10 幽灵化 OFF**（玩家实体在场挂机、零干预） |
| 3 | **加速 15x** | `ResolveConfiguredSpeed()` ← `WorldConfig.time.testSpeedMultiplier`＝**15**（正门直通）；**禁直接设 timeScale**（L-09）；上限 15x 不向上 |
| 4 | **5 日检查点** | `Valley_P1_Observer` `day%5==0` → `Save(MainSlot+"_dayXXX")` ＋**立即回正 `Save(MainSlot)`**；容器进局前 `P1Observer.SetMainSlot(SLOT)`（[Run:39](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs#L39)）；15x 下 5 日≈120 现实秒 |
| 5 | **120 日熔断** | 容器 `CIRCUIT_BREAK_DAY=120`；**提前收工**＝第 2 个 AI 「剧本阶段 → 军事」（`_military.Count>=2`，[Run:96](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs#L96)）；**灭绝停跑**＝AI 全灭（[Run:98](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs#L98)） |
| 6 | **收尾 `ExitTestRun`＋封盘** | [Finish](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs#L102-L121)：停速 → `Save(SLOT)` 封盘 → `ExitTestRun()` 全量恢复 → 写 `Logs/P1/hh80_run_status.log` |

## 二、容器级改动列报（R10 澄清域内；HH.203 §二.8 要求）

| 文件 | 改动 | 六考/七考原值（git 可复原） |
|---|---|---|
| `Valley_HH80_Run.cs` | `SEED` → **侦察定案新值**；`SLOT` → **`"p1_run8"`**（新命名空间，禁覆盖 `p1_run6`/`p1_run6b`/`p1_run7`） | 六考 `69496`/`p1_run6b`；七考 `73621`/`p1_run7`（`git log -p -- Assets/Editor/Smoke/Valley_HH80_Run.cs`） |
| `Valley_HH80_Run.cs` | **＋L-32 收尾补强**（D657 案 A 通用化）：`Finish()` 内 `SetGameSpeed(0f)` → **`Time.timeScale = 0f` 真暂停**（禁 `SetGameSpeed(0f)` 当暂停，条文3）＋**末尾 `EditorApplication.ExitPlaymode()`**（条文1；原容器缺此两处） | 原逻辑一字不动，仅收尾两处 |
| `Valley_HH80_Scout.cs` | `SEEDS` → 新候选 3 个（见 §三） | 七考候选 `{73621, 48903, 91777}` |
| `Valley_P1_Observer.cs` | **不改**（`MainSlot` 已参数化，由容器 `SetMainSlot` 传入；`IsRunning` 只读访问器已在） | — |

**业务代码（`Assets/_Game`＋产品资产/SO/场景）＝零改动**（`git status` 可验证）。

## 三、seed 报备（L-29：零命中 ＋ 阳性对照）

- **禁复用清单（逐字守）**：`73621`（七考）/`48903`（诊断局）＋ `22360/52705/16180/51713` ＋ 历史 `20273/52707/7841/31337/31415/27182/57721/21109/60221/90210` ＋ 六考 `48271/69496/81203` ＋ D45 `73311` ＋ 冒烟 seed（`21140`/`21111`/`21112` 等）。
- **新候选**：**`70403` / `82007` / `64513`** —— 全库 Grep **零命中**；**阳性对照**＝同 pattern 查 `73621` **命中 11 文件** ⇒ 检索有效（**L-29 兑现**）。
- **流程**：跑「`Valley/验证/HH80_侦察`」做四点结构性检查（①出生口袋 ②资源不可达 ③邻国过近 ④AI 模板/族分布）→ **定案 1 个并随起跑证据落盘报备**（R2：仅结构性缺陷才升级报裁）；侦察不计判定证据。
- **✅ 侦察定案（`Logs/P1/hh80_scout_result.log`，2026-09-11 20:46:41）**：**`64513` 定案** —— 4 AI〔**密林 r1 精灵**／**磐石 r2 矮人**／**铁蹄 r3 兽人**／**霜岩 r2 矮人**〕＋国距 **42.9/70.7/89.0/92.2/110.5/122.6 均衡无口袋**＋领土 mid 16~19＋营地 2＋流浪 6 ⇒ **四点检查无阻断（各局粮 40/工 6 正常；族池含兽人 r3 ⇒ ⑰d 可测）**；
  - `70403`（3 AI〔密林 r1／玄岩 r2／战歌 r3〕，min 41.7）＝**次优备选**（AI 数少一国）；
  - `82007`（4 AI，但 min **25.0 过近**＜27.6 否决阈）＝**否决**（出生口袋风险）。
  - **容器实值**：`Valley_HH80_Run.cs` `SEED=64513`／`SLOT="p1_run8"`；`Valley_HH80_Scout.cs` `SEEDS={70403,82007,64513}`。

## 四、观测域增量列报（随本批）

1. **同跑 `DiagMilitary` 只读探针**（`Valley_DiagMilitary.cs`，Editor-only 反射只读；HH.190/D651 产物）：补 **§三-2 建军链双端逐日证据**（⑦/⑯/⑰a/⑰b/⑰c/⑰d/⑰e 的 need/feasible/census top）——**只读、零世界写入、零行为漂移**（HH.208 已验证同款）。
2. **可选夹具定向补强（D661③）＝本轮不做**：判定线是「**自主**至军事期」⇒ 夹具注入会污染判定语义，故 `PlaceKingdom`＋阶段注入**不并入本判定跑**；如需三专属营「被选中」直证，另起独立短程夹具跑（非判定证据，可后续按需）。

## 五、自估时长与停止条件

- 时长：15x ⇒ 24 现实秒/游戏日 ⇒ 120 日 ≈ **48 现实分钟**（＋侦察 ≈5 分钟；提前达标则 <48 分钟）。
- **停止条件（沿 HH.186 §四 五条）**：①容器级阻断（含 45 日 Load 卡死）→ 停手如实列报报裁，不硬等 ②判据/协议冲突 → 停手写 HH ③结构性缺陷/灭绝/熔断 → 如实列报不注水 ④**跑局期施工零改动**（业务代码 diff ⇒ 立即自曝停手）⑤检查点/读档链异常 → 列报不修。

## 六、判读义务（收口必答）

D589 判定线 ＋ D563③（兽人首训 `effDays`／兵 0→N／翻案条款）＋ D569/D579（AI 台账水晶增长／Vault 积压）＋ **王国 AI P0 行为面**（⑤三通道分诊／③产能种类化／派工活权重／断链自愈；离屏 DZ-089·090 只观察）＋ **军事期判定面**（⑯⑰／⑦扩招兵／姿态三档）＋ 三军事向专属营落地（答 **DZ-103**）。

## 七、红线自检

- ✅ 正门 `EnterTestRun`＋15x（L-09／test-harness-first）；**收工退 Play**（L-32，含本批补强的 `ExitPlaymode`）
- ✅ 业务代码/AI.Core **零直改**；禁参数微调找补（D563③）；枚举尾插未动（L-28）
- ✅ 写-改-commit 同串、显式路径 add（禁 `git add -A`）、**不 push**；交付即 commit（D640 #9）
- ✅ 共享文档增量改（禁整文件覆盖）；改前重读磁盘（agent-handoff §六）
- ✅ seed 报备带阳性对照（L-29）；判定证据以 15x 段游戏日内产物为准（L-32 条文2）

## 八、起跑声明与计划

**停手闸说明**：HH.203 §四 的「停手待策划确认」已由 **D661**（"⇒ 验收成立 ⇒ HH.203 七考重验起跑"）满足，另叠加用户「正式起跑」指令 ⇒ 本回执落盘后**直接起跑**；若策划端对 §一~§四 复述或 seed 报备有异议，**请即示，我立即停跑并回滚容器常量**。

1. 落盘：本回执 ＋ 容器级改动（§二）＋ 索引/账本行 → **一次 commit**
2. 起跑：`Play(GameScene)` → `Valley/观测/P1_启动观测` ＋ `P1_打印当前状态`（在场自检） → `Valley/诊断/启动建军链诊断`（§四-1） → `Valley/验证/HH80_侦察`（seed 定案） → `Valley/验证/HH80_正式跑`
3. 收尾：退 Play（容器自带 ＋ 会话侧兜底）→ 证据落盘 → 交付报告（按水位线取号）

---
*执行端 2026-09-11（**HH.211**；原取号 HH.210 撞美术端改号 ⇒ 顺延，勘正 `e5b91e2`）。*
