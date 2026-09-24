# `HH.321` 件 1 复派 · **复验收尾 · 探针运行记录**（读数面）

- **执行端**：TraeCode｜**日期**：2026-09-24｜**性质**：收尾（⛔ 未改生产码/资产）
- **依据**：`D854`（复验收口 · `策划端/HH.321_件1复派_独立复验裁决.md` §八）｜**基线**：`b751e996`（链 `1f2474c8 ← b751e996 ← 89f601c7 ← 36f7a078 ← 5cddd09e` 实对齐 ✓）｜⛔ 未 push
- **探针**：`Valley Rampart/Assets/Editor/Smoke/Valley_D853_ReVerifyProbe.cs`（本项起归执行端维护）｜菜单 `Valley/验证/D853 复验 件1独立路径`（源码 `:98`）
- **编辑环境**：Tuanjie 2022.3.62t7（`Tuanjie.exe` PID 30236）｜`mcp-for-unity-server` v3.4.7（`http://127.0.0.1:8080/mcp`）
- ⭐ **产物**：`Logs/d853_verify_20260924_193532.txt`（`# 封存 19:35:32` · 5036 B）｜⛔ 引用一律带此完整时间戳

---

## §0 · 编译与运行前置

| 项 | 读数 |
|---|---|
| 首编 | ⚠️ 1 error（**探针自身**）：`Assets/Editor/Smoke/Valley_D853_ReVerifyProbe.cs(202,76): error CS8361（插值内三元须加括号）` |
| 处置 | 仅改探针 `:202`（去掉该插值内三元）⇒ ⛔ 未碰生产码/资产 |
| 复编 | `read_console{types:[error],filter_text:"error CS"}` ⇒ **Retrieved 0 log entries**（＝ `error CS` **0 条**） |
| 非 CS 的 error 级条目（14 条） | 3 条引擎/环境噪声（`ruler` 脚本缺失 · `No Theme Style Sheet` · `105 node options failed to load`）＋ 探针自身日志 ＋ **R5 期望的守卫 LogError 1 条**（原文：`[DamageSystem] DamageConfig.maxAttacksPerFrame=0 ≤ 0 非法（会零判定 ＋ _pendingAttacks 无界增长）⇒ 本次按 1 处理，请改为 ≥ 1。`） |
| 进入 Play | `manage_editor play` ⇒ `isPlaying=True scene=GameScene` |
| 退 Play | `manage_editor stop` ⇒ `isPlaying=False`（⛔ 无余留世界） |

**环境行（产物原文）**：`seed=20321 timeScale=1 建筑在册=21 单位在册=33 cap=100`
**场址/布置（产物原文）**：`band=(52,16) 24×2 格空置且 12 格内无敌`｜`R1塔@(58,16) 工事=True HP=135｜R2塔@(62,16)｜R3空仓塔@(66,16) 工事=True｜R3材料塔@(70,16)｜R4 farm@(73,16) 工事=False`

---

## §一 · 五臂：**事先写死的预期** vs **实测**（预期原文取自源码 `[预期·事先写死]` 行）

### R1 · 判据 1（击毁生产链 · 事件内即时读）

| 面 | 事先预期 | 实测（产物原文） |
|---|---|---|
| 事件内 注册表空／占格空 | `True/True` | `注册表空=True 占格空=True` |
| 事件内 实体可用／位对上 | `True/True` | `实体可用=True 位对上=True` |
| 帧后已销毁 | `True` | `帧后已销毁=True` |
| 事件数／Cause | —— | `事件数=1 Cause=Killed` |
| 旧判据判别式 `state==Dead`（⛔ 码面） | `True` | `True` |
| 击前基线 | —— | `注册表=1 占格=1 state=Active｜ApplyDamage 返回=952380` |

⇒ **逐项与事先预期一致**。

### R2 · 判据 2（门幂等 ＋ 反向鉴别 ＋ ⭐ 边界臂）

| 面 | 事先预期 | 实测（产物原文） |
|---|---|---|
| 门第 1 次 | 双空 `True/True` · 门不销毁 | `注册表空=True 占格空=True｜门后实体仍在=True` |
| 复调 · 判据实参即时读 | `True ∧ True` | `` `reg.GetAt(b.coord)==null`=True ∧ `grid.GetOccupant(b.coord)==null`=True `` ⇒ 盘上路径 `:233 return`（⛔ 码面） |
| 复调后 五面／计数／异常 | 零变化 · 异常 0 | `注册表空=True 占格空=True 实体仍在=True｜注册表计数 24→24｜异常新增=0` |
| 已销毁引用臂 | 异常 0 | `异常新增=0` |
| ⭐ **边界臂** 判据实参 | `False`（⇒ 门执行） | `GetAt(A2.coord)==null`=**False** |
| ⭐ 边界臂 `nb` 占格 | **被清空 ＝ False** | `nb 占格仍在=False` |
| ⭐ 边界臂 `nb` 注册表项 | **保持 ＝ True** | `nb 注册表项仍在=True` |
| ⭐ 边界臂 `nb` 实体 | `True` | `nb 实体仍在=True` |

⇒ ⭐ **边界臂三项逐项命中事先预期**（`FreeFootprint`（`GridSystem.cs:445-450` 无归属判定）清掉后注册者占格；`BuildingRegistry.Unregister:39` 归属判定使其注册表项保持）⇒ ⛔ 停手条件②未触发。

### R3 · 判据 3（`Die()` 二次调 · 空仓臂 ＋ ⭐ 材料阶段臂）

| 面 | 事先预期 | 实测（产物原文） |
|---|---|---|
| 空仓臂 Die#1／Die#2 | 事件 1 | `箱子 1→1（本格 0→0）事件=1` ／ `箱子 1→1（本格 0→0）事件=1｜异常=0` |
| 材料臂构造 | `IsSiteAwaitingMaterials=True` · `Deposit` 8 | `SiteNeedOf(need).IsZero=False`｜`IsSiteAwaitingMaterials=True`｜`Deposit(Wood,8) 返回=8`｜`工地仓 Wood=8` |
| 材料臂 **Die#1** | 工地仓 `8→0` · 出现掉箱 | `工地仓 Wood 8→0`｜`箱子 1→2 本格 0→1`｜`注册表空=True 占格空=True`｜`事件=1` |
| 材料臂 **Die#2** | 零变化 · 事件仍 1 | `工地仓 Wood=0`｜`箱子 2→2 本格 1→1`｜`事件=1`｜`注册表计数 24→23`（**恰减 1**）｜`异常=0` |

⇒ ⭐⭐ **「非空掉箱 × `Die()` 二次调」直接读数成立**（上一批为「空仓 ⇒ 未注入」：`IsSiteAwaitingMaterials=False`、`Deposit 返回=-1`）。

### R4 · 判据 4（`EnterRuined` 回归）

| 面 | 事先预期 | 实测（产物原文） |
|---|---|---|
| state | `Ruined` | `state=Ruined` |
| 占格／注册表／实体 | `True/True/True` | `占格仍在=True｜注册表仍在=True｜实体仍在=True` |
| 新增 `UnitDiedEvent` | `0` | `0` |

⇒ **逐项与事先预期一致**。

### R5 · 回归（`cap=0`）

| 面 | 事先预期 | 实测（产物原文） |
|---|---|---|
| `LogError` | 1 条 | `累计=1`（全文见 §0） |
| `pending` 逐帧 | 有界 | `0 / 3 / 0 / 0 / 0 / 0` |
| 靶 HP 逐帧 | 下降（未停摆） | `106/106/106 → 102/102/106 → 102/98/98 → 98/98/98 → 98/98/98 → 94/98/98` |

⇒ **逐项与事先预期一致**。

---

## §二 · 报告勘正（本项仅改 `多Agent交接/执行端/HH.321_件1复派_交付报告.md` · 依据 `…214655.txt`）

| # | 位置 | 改前 | 改后 |
|---|---|---|---|
| 1 | §三 判据 2 行 | 注册表计数 `23→22→22` | `24→23→23` |
| 1b | §六 判据表 第 2 行（**同一读数的回显**） | `重复调 23→22→22` | `重复调 24→23→23` |
| 2 | §六 回归行 | `pending 0/3/0/0/0/0` | `pending 0/3/0/1/0/0` |
| 3 | §0 判据 3 | `✅ **达成**（⚠️ 掉箱面鉴别力已声明）` | `✅ **达成（⚠️ 能力列不达 · 掉箱面空仓 · 见 §四）**` |
| 4 | §一 第 1 行锚点 | `BuildingFactory.cs:220-232 RemoveBuilding` | `BuildingFactory.cs:222-235 RemoveBuilding`（**判据 :229-233**） |
| 5 | §一 第 3 行锚点 | `Building.cs:1376-1379 Die() 入口` | `Building.cs:1376-1418 Die()`（**守卫 :1378**） |
| 6 | §二 表头 | 「与本批上一轮**同构造前后对照**」 | 「**跨 commit 同构造对照 · 三臂**」（＋三臂脚注：`…202008` False/False ／ `…202832` True/True ／ `…214655` True/True） |

---

## §三 · 环境观察（ℹ️ 不归属本臂）

- 退 Play 时引擎条目：`Some objects were not cleaned up when closing the scene. … [SpriteAnimatorDriver]` ⇒ ℹ️ 视觉组件族（本臂未构造该组件）· 记录不归属。
- 引擎噪声：`The referenced script on this Behaviour (Game Object 'ruler') is missing!`／`No Theme Style Sheet set to PanelSettings`／`105 node options failed to load and were skipped.` ⇒ ℹ️ 预置/环境面。

---

## §四 · §待裁 / 登记

| # | 项 | 读数依据 |
|---|---|---|
| 1 | ⭐ **`O-28` 推进建议**：「非空掉箱 × `Die()` 二次调」**已取得直接读数**（工地仓 `8→0`（第 1 次）／`=0`（第 2 次）＋ 箱子 `1→2→2` ＋ 本格 `0→1→1` ＋ 事件 `1`＋`1` ＋ 注册表计数恰减 1）⇒ 由「登记」推进到 **结案或立项** ＝ 待策划端裁定 | `Logs/d853_verify_20260924_193532.txt` R3 材料臂四行 |
| 2 | ⭐ **R2 边界臂读数**（`F-15` 单槽 · 后注册者占格被 `FreeFootprint` 清空 ／ 注册表项保持）⇒ 是否需单独登记 | 同上 R2 边界臂行 |
| 3 | 报告勘正 7 处（§二 表）已落盘，待策划端在 `D854` 面复核 | 本记录 §二 |
| 4 | ℹ️ `pending` 逐帧本批探针运行值 `0/3/0/0/0/0`（与 `…214655.txt` 的 `0/3/0/1/0/0` 不同轮次 · 各自独立） | 本记录 §一 R5 |
