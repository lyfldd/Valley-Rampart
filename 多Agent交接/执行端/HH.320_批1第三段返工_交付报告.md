# `HH.320` 批 1 · **第三段（返工 4 件）** 交付报告

- **执行端**：TraeCode｜**日期**：2026-09-23｜**性质**：返工（件 A/B/C/D · ⛔ 未扩大范围）
- **依据**：`多Agent交接/策划端/HH.320_批1第二段_交付验收裁决.md`（`D847`）｜任务书 §十（`D846`）｜口径真源 `最高优先级文档/07_伤害层.md` §二/§七
- **基线对齐**：`git log --oneline -3` ⇒ **`817162c0`**（`D847` 裁决落账）← **`e19896a8`**（第二段施工）← `4eaf3a58`（`D846`）⇒ 与「基线 `e19896a8`」一致｜⛔ 未 rebase／未 reset／未 push。
- **本段只改 2 个生产文件**：`ProjectileManager.cs`（＋20/−5）／`CombatRules.cs`（＋7/−1）＋ 探针 1 档｜⛔ 未动 `AI.Core`／`GridMath.DistCells`／`M3` 三项／`DZ-4`／`TaskScheduler.cs`／`GameScene.unity`／`Packages`／美术／训练仓｜⛔ **零数值改动**（`range`/`attackRange`/`VisionRadiusCells`/`perceptionRadius` 一律未动）。

---

## §0 · 一句话结论

**四件全落地**：① `件 6` 早退位置已修（**唯一早退点**＝单位与建筑皆无命中），② **生产链实测三例转正**（B1 建筑 Δ=25 / B2 建筑 Δ=25 / B3 底座外 Δ=0 ＋ B3′ 同点敌对单位 Δ=21 = 到达性对照），③ `件 2` 第 13 处平衡读数已补（世界半径 **−44.1%**，格轴 **1.789N → N**），④ 自相矛盾注释已勘正；**编译 0 error**。

---

## §一 · 件 A · `件 6` 早退位置修正（`ProjectileManager.OnProjectileArrived`）

### 1.1 改动（最终行号 · 基线 `e19896a8` → 本段）

| 位置 | 改动 |
|---|---|
| `ProjectileManager.cs:211`（**已删**） | ⛔ `if (candidates.Count == 0) return; // miss` ⇒ **删除**（它把建筑链一并挡在门外） |
| `ProjectileManager.cs:210-222` | 新增 ⭐ 注释块：① 候选**只含 `UnitController`**、建筑不在其中；② 优先序**单位命中优先 → 建筑底座命中 → 两者皆无 ⇒ miss**；③ 唯一早退点在下方 |
| `ProjectileManager.cs:216-221` | `attackerFaction` 取值**加空引用防御**（本行现已**无条件执行**，须容忍弹道飞行期攻击者被销毁的 Unity 假 null）：`is UnitController … != null` / `is Building … != null` / 否则 `Faction.None` |
| `ProjectileManager.cs:252-254` | ② 建筑兜底（**位置未变**，仍在单位循环之后） |
| `ProjectileManager.cs:256-258` | ⭐③ **唯一早退点**：`if (bestTarget == null) return;`（半空转早退已废） |
| `ProjectileManager.cs:311` `CheckWallBlock` 调用点（`:205-206`） | ⛔ **未动**（越墙判定仍先于命中，语义逐字保留） |

### 1.2 语义核对（⛔ 三条例行约束）

- ① 早退条件＝「**既无单位候选命中，又无建筑底座候选**」⇒ ✓（`:258` 位于 `FindBuildingAtLanding(:254)` 之后）。
- ② 优先序不变 ⇒ ✓（`if (bestTarget == null)` 才走建筑；单位命中时**不查建筑**）。
- ③ 越墙判定位置与语义不变 ⇒ ✓（`CheckWallBlock` 仍为 `OnProjectileArrived` 第一步，`return` 语义未变）。
- ④ 注释已补 ⇒ ✓（`:210-222` 说清优先序与「为何不能在候选为空时早退」）。

---

## §二 · 件 B · **生产链实测**（三例 ＋ 对照）

- ⭐ **载体＝生产链**：`DamageSystem.RegisterAttack` → `ExecuteAttack` → `ProjectileManager.SpawnProjectile` → `ProjectileManager.Update` → `OnProjectileArrived(:202)` → `FindBuildingAtLanding(:336)` → `DamageSystem.ApplyDamage`。
- 产物：`Logs/hh320_buildhit_20260923_170840.txt`（终版 · 末行 `# 封存 17:08:40`）；另有过程档 `165944`（首跑·全 0）、`170409`（诊断跑）。
- 环境：seed `20321` · `worldSize=Small` · 速档 **1×**（弹道推进需真实时间）· 隔离场址（footprint 全域空 ＋ 12 格内无敌）· 建筑＝`Buildings/farm`（**非工事** · 2×2 · AI 国归属）· 射手＝`Archer`(PlayerCamp) @ 建筑中心沿 −gx **3 格轴**（`DistVisual=3.000` ≤ range 6）。

### B1 · 落点附近**无任何单位** ＋ 落点在建筑底座内 ⇒ **命中建筑 ✓**

| 项 | 内容 |
|---|---|
| 位置 | `ProjectileManager.cs:202`（到达）→ `:359` `QueryNearbyUnits`（候选）→ `:336` `FindBuildingAtLanding`（底座）→ `DamageSystem.ApplyDamage` |
| 实测输出 | `[B1] 散布关 · 落点=建筑中心(0.64, 25.92)（底座内）· 生产候选数 发射时=0 到达后=0（期望 0/0）· RegisterAttack=True PM.active=15 ⇒ 建筑 HP 65 → 40（Δ=25）` |
| 期望/实际 | 期望 Δ>0 ⇒ **实际 Δ=25** ✅ |
| 结论 | **「落点落在菱形底座内 ⇒ 命中该建筑」在生产链上成立**（候选为空**不再**早退） |

### B2 · 落点附近**有友方单位**（同阵营 ⇒ 被过滤）＋ 落点在底座内 ⇒ **建筑兜底生效 ✓**

| 项 | 内容 |
|---|---|
| 实测输出 | `[B2] 散布关 · 落点=建筑中心（底座内）· 友方单位(同阵营 PlayerCamp)@落点 生产候选数 发射时=1 到达后=1（期望 ≥1）· RegisterAttack=True ⇒ 建筑 HP 40 → 15（Δ=25）｜友方 HP=100（期望不受伤）` |
| 期望/实际 | 期望建筑 Δ>0 ⇒ **实际 Δ=25**；友方未受伤（100→100）✅ |
| 结论 | **候选存在但被阵营过滤后，兜底仍命中建筑**（「逐个候选过滤 ⇒ 全被滤 ⇒ 落建筑」路径成立） |

### B3 · 对照（鉴别力）· 落点**底座外**（+2 格轴 · 射程内）＋落点放**友方**单位 ⇒ **对建筑 miss ✓**

| 项 | 内容 |
|---|---|
| 实测输出 | `[B3] 散布关 · 落点=建筑中心 +2 格轴 (1.92, 26.56)（InDiamondBase=False 期望 False · 射程内=True（发射前实读））· 生产候选数=1（友方 1 ⇒ 到达时被阵营过滤）· RegisterAttack=True ⇒ 建筑 HP 15 → 15（Δ=0）｜落点友方 HP 100 → 100` |
| 期望/实际 | 期望建筑 Δ=0 ⇒ **实际 Δ=0** ✅ |
| 结论 | 该例构造为「候选被过滤 ⇒ **建筑兜底确实被求值**」⇒ 返回 `null` ⇒ 证明 **miss 来自底座判据为 False**（⛔ 非「兜底没跑」） |

### B3′ · 同点**到达性**对照 · 同一落点改放**敌对**单位 ⇒ 单位倒伤 ⇒ **弹道确已到达该落点 ✓**

| 项 | 内容 |
|---|---|
| 实测输出 | `[B3′] 散布关 · 同落点(底座外)改放敌对单位 · 生产候选数=2 · RegisterAttack=True ⇒ 该单位 HP 110 → 89（期望 Δ>0）｜建筑 HP 15 → 15（期望 Δ=0）` |
| 期望/实际 | 单位 Δ=21 ✅（`CalculateDamage(25, 单位防御)`＝21 ⇒ 与伤害链口径自洽）；建筑 Δ=0 ✅ |
| 结论 | **B3 的 miss 既非「弹道未到」也非「伤害链失效」** —— 同一点敌/友两态对照 ⇒ 判据确有**鉴别力** ✅ |

### B1′ · **散布照旧（生产值）** 读数 ＋ 正对照（诚实披露）

| 项 | 内容 |
|---|---|
| 实测输出 | `[诊断0·散布档] projectileErrorRadius=1.5（世界单位 ⇒ ≈2.10 格轴）· hitRadiusCells=0.25 · 底座半宽=1/1 格` ／ `[B1′·散布照旧(1.5)] 连打 4 发 ⇒ 建筑 HP 90 → 90 命中发数=0/4 逐发=[0:90→90][1:90→90][2:90→90][3:90→90]` |
| 正对照 | `[B1-正对照·手工ApplyDamage] 返回=25 建筑 HP 90 → 65（Δ=25）`（⇒ 伤害链对建筑有效，故障面不在伤害段） |
| 归因 | `projectileErrorRadius=1.5`（世界单位）≈ **2.10 格轴** ≫ 2×2 底座半宽 **1 格** ／ ≫ 单位命中半径 **0.25 视觉格** ⇒ **生产档下单发落点常落在底座外** ⇒ 单发 miss 属**散布后果**（⛔ 非 `件 6` 失效）；⇒ 为使「落点在底座内」这一**前提可判**，三例在**散布关**（运行期字段 · 收尾还原）下执行 |

---

## §三 · 件 C · `件 2` 第 13 处（`MonsterController.FindNearestHuman`）平衡读数

- 落点：`MonsterController.cs:85-93`（入参 `rangeWorld → rangeVisual`）＋ 调用面 `MonsterAI.cs:85/115/128`（**全库 3 处 · 均已同步**）。
- 口径：**改前** 世界半径 ＝ `N × CellSize()`（`cellSize.x` ＝ **1.28**）；**改后** 世界半径 ＝ `N × 格步长`（`GridMath.VisualToWorld` ＝ **0.7155**）。
- `MonsterDef` 资产实值（本端实读 `Resources/Disaster/{Brute,Raider,Slinger}.asset`）＋ 逐项对照：

| 项 | N（资产实值） | 改前·世界半径 | 改前·沿格轴有效格数 | 改后·世界半径 | 改后·沿格轴有效格数 | Δ世界半径 |
|---|---|---|---|---|---|---|
| `Slinger.attackRangeCells` | 6 | **7.68** | 10.73 | **4.293** | **6** | **−44.1%** |
| `Slinger.visionRadiusCells` | 10 | **12.80** | 17.89 | **7.155** | **10** | **−44.1%** |
| `Raider.attackRangeCells` | 1.2 | **1.536** | 2.147 | **0.859** | **1.2** | **−44.1%** |
| `Raider.visionRadiusCells` | 8 | **10.24** | 14.31 | **5.724** | **8** | **−44.1%** |
| `Brute.attackRangeCells` | 1.2 | **1.536** | 2.147 | **0.859** | **1.2** | **−44.1%** |
| `Brute.visionRadiusCells` | 12 | **15.36** | 21.47 | **8.586** | **12** | **−44.1%** |

- ⭐ **读数口径**：改前/改后均为 **1 个统一系数**（1.28 vs 0.7155）⇒ 全体怪物的**感知/索敌世界半径一律 −44.1%**、**沿格轴有效格数 1.789N → N**（＝1.28/0.7155 ＝ **1.789**）。
- ⛔ **本段未改任何数值**；「是否重标 `VisionRadiusCells`/`prof.range`」＝ **待用户拍**（裁决 §五建议：先不重标 · 记为已知平衡影响；若要观感相当 ⇒ 重标系数 **×1.789**）。
- ⚠️ 载体声明：本节为**码面算术读数**（两口径公式 ＋ 资产实值），**⛔ 未做生产链实测**（本项只需系数对照，无需进局；且红线禁改数值）。

---

## §四 · 件 D · 注释勘正（`L-63`）

**勘正前**（`CombatRules.cs:175` · `e19896a8`）：
```
    /// 性能：单遍 `UnitRegistry.GetAllUnits()`（**返回内部 List 引用 ⇒ 零分配**），`O(N)`＋早退候选判定。
```
**勘正后**（`CombatRules.cs:175-180` · 本段）：
```
    /// 性能：单遍 `UnitRegistry.GetUnitsEnumerator()`（`HashSet<T>` **结构枚举器** ⇒ 零装箱/零分配），`O(N)`＋早退候选判定。
    ///   ⚠️ **勘正（`L-63` · `D847` §三-1）**：⛔ **不得**走 `UnitRegistry.GetAllUnits()` ——
    ///   其**返回类型**为 `IEnumerable<UnitController>`，而 `UnitRegistry.cs:11` 内部实为
    ///   **`HashSet<UnitController>`** ⇒ 接口 `foreach` 会把结构枚举器**装箱** ⇒ **每次 1 次堆分配**。
    ///   （本行曾误写「返回内部 List 引用 ⇒ 零分配」＝ 类型名错 ＋ 忽略接口装箱 ⇒ 与下文 `:188-190` 自相矛盾，
    ///   现按事实改写。）真零分配口 ＝ `UnitRegistry.GetUnitsEnumerator()`（`UnitRegistry.cs:57`）。
```
- ⚠️ 三项事实（与裁决 §三-1 一致）：① `_aliveUnits` 声明 ＝ `HashSet<UnitController>`（`UnitRegistry.cs:11`）；② `GetAllUnits()` 返回 `IEnumerable<UnitController>` ⇒ 接口 `foreach` ⇒ 结构枚举器**装箱**；③ 真零分配口 ＝ `GetUnitsEnumerator()`（`:57`，本函数实现体 `:195` 已在用）。
- ⛔ **只改本处**；同源文本面（`PopulationSystem:356/514`、`SatietySystem:141`、`HH.77` 报告等）**未批量改**（裁决已登记为文本面批量待勘正）。

---

## §五 · 判据表（⭐ **读数列 / 能力列** 分列 · `L-88`）

> 规则：**能力列**（「能命中建筑」这类断言）**只接受生产链实测读数**；⛔ 数学面／纯函数／静态扫描／码面推断一律**不得**填入该列（本段执行）。

| # | 项 | 读数列（读数 · 载体） | 能力列（仅生产链实测） |
|---|---|---|---|
| A | 早退位置修正 | 改动 `ProjectileManager.cs:211`（删）→ `:256-258`（唯一早退点）；`git diff` `+20/−5` | —— |
| B1 | 落点=建筑中心 · **无单位** | 候选 0/0 · 建筑 HP 65→40（Δ=25）· `Logs/hh320_buildhit_…170840.txt` | ✅ **命中建筑**（生产链） |
| B2 | 落点=中心 · **有友方**（被过滤） | 候选 1/1 · 建筑 HP 40→15（Δ=25）· 友方 100→100 | ✅ **建筑兜底生效**（生产链） |
| B3 | 落点=**底座外**(+2 格轴) · 友方在该点 | `InDiamondBase=False` · 射程内=True · 建筑 Δ=0 · 友方 Δ=0 | ✅ **对建筑 miss（鉴别力）** |
| B3′ | 同点改**敌对** | 单位 HP 110→89（Δ=21）· 建筑 Δ=0 | ✅ **弹道到达该落点**（生产链） |
| B1′ | 散布照旧（1.5） | 4 发 0/4 命中 · 逐发 `[90→90]×4` | —— |
| B-正对照 | 手工 `ApplyDamage` | 返回 25 · 建筑 90→65（Δ=25） | ✅ 伤害链对建筑有效 |
| C | 怪物感知/索敌半径 | 世界 **1.28N → 0.7155N（−44.1%）**；格轴 **1.789N → N**；6 行逐项表见 §三（码面算术＋资产实值） | ——（红线禁改数值 · 无需进局） |
| D | 注释勘正 | `CombatRules.cs:175-180` 前后原文见 §四 | —— |
| 编译 | 生产 ＋ 探针 | `unity_editor refresh` ⇒ **errors 0**（warnings 全为存量） | —— |
| 红线 | 触面 | 本轮改 **2 生产文件**（`ProjectileManager.cs`／`CombatRules.cs`）＋ 探针 1 档；`AI.Core` **零触**；⛔ 未动 `GridMath.DistCells`／`M3`／`DZ-4`／`TaskScheduler.cs`／`GameScene.unity`／`Packages`／美术／训练仓 | —— |

---

## §六 · 构造法声明 · 探针自纠 · 披露

1. **构造法（件 B）**：① 弹道档（`AttackProfile`：attack=25 / range=6 / speed=60 / Lob / 无 AOE）**为探针构造**，其入口走**生产链** `DamageSystem.RegisterAttack`；② 建筑/单位走**生产生成口**（`BuildingFactory.CreateBuildingInstance` / `UnitFactory.SpawnUnit`）；③ **散布关**＝运行期改 `ProjectileManager._config.projectileErrorRadius`＝**内存实例字段**（收尾**已还原 1.5** · ⛔ **未写资产文件**）；④ 速档 1×（弹道推进依赖 `Update`/`Time.deltaTime`）。
2. ⭐ **过程自纠 3 次（如实记录 · 三步都是探针侧缺陷）**：
   - ① 首跑三例全 0 → 加**链路诊断**（PM/DS 在场性 · `PM.active` 计数 · `RegisterAttack` 返回值 · 该段工事阻挡 · timeScale/deltaTime）⇒ 定位**不在**链路在场性；
   - ② 正对照（手工 `ApplyDamage` Δ=25）⇒ 排除伤害段 ⇒ 锁定**弹道命中判定**；
   - ③ 实读 `DamageConfig.projectileErrorRadius = **1.5**`（≈2.10 格轴）⇒ **散布把落点甩出底座/命中半径** ⇒ 得「散布照旧 0/4」与「散布关三例转正」两段读数。
3. ⚠️ **登记观察项（非本批缺陷）**：生产档下 `errorRadius 1.5`（世界单位）对 **2×2 建筑底座（半宽 1 格）** 与 **单位命中半径 0.25 视觉格** 而言都**很大** ⇒ 单发命中率天然低（建筑 ≈ 底座覆盖比、单位近乎"必须撞上"）。⇒ 是否属于预期手感 ＝ **玩法面待裁**（本端 ⛔ 未改任何散布/命中数值）。
4. ⚠️ 探针沿用第二段已登记的两条纪律：**格中心取点**（`CellToWorldF(x+0.5,y+0.5)`；⛔ 顶点会触发子格边界退化）＋ **靶身份判定**（`ReferenceEquals`）＋ 试区隔离。

---

## §七 · 编译 · 收尾 · 产物

| 项 | 读数 |
|---|---|
| 编译 | `unity_editor refresh` ⇒ **errors 0**（生产码 ＋ 探针） |
| 收尾三态 | 探针 `TestHarnessApi.ExitTestRun()` ✓ → 执行端 `unity_editor stop` ⇒ `playMode=stopped`（⛔ 无 1x 余留世界 · `L-32`） |
| 产物（Logs/ · 未入版本控制 · 既有约定） | **终版** `hh320_buildhit_20260923_170840.txt`（末行 `# 封存 17:08:40`）｜过程档 `…_165944.txt`（首跑全 0）／`…_170409.txt`（诊断跑） |
| 探针容器 | `Assets/Editor/Smoke/Valley_HH320_DamageProbe.cs`（新增菜单 `Valley/验证/HH320 件6建筑命中_生产链(第三段B)` ＋ `RunBuildHitCoroutine` 公开入口 · 便于 bridge 直跑） |
| 提交面 | 具名 `git add`（`ProjectileManager.cs`／`CombatRules.cs`／探针档／本报告）；**未 push** |

---

## §八 · 请裁 / 待确认（2 条）

| # | 事项 | 本端处置/建议 |
|---|---|---|
| 1 | `VisionRadiusCells`／怪物 `prof.range` 是否重标（§三 · 世界半径 −44.1%） | ⛔ 未改；若「观感须与改前相当」⇒ 系数 **×1.789**（属数值批，另签） |
| 2 | 生产档 `projectileErrorRadius = 1.5` 相对底座/命中半径偏大（§六-3） | ⛔ 未改；⚠️ 与 `件 6` 的「落点在底座内」前提存在**手感层面**的耦合（单发对建筑命中率低）⇒ 请裁是否另立观察/调参批 |

---

## §九 · 红线复核

- ⛔ 除件 A 外未改任何生产码逻辑（件 D 仅注释；件 B/C 为探针 ＋ 读数）。
- ⛔ 未动 `AI.Core`（本轮只改 `Systems/Combat` 2 文件）／`GridMath.DistCells`／`M3` 三项／`DZ-4`。
- ⛔ 未改任何 `range`／`attackRange`／`VisionRadiusCells`／`perceptionRadius` 数值。
- ⛔ 未碰训练仓／`GameScene.unity`／`Packages`／美术脏点／`TaskScheduler.cs`（工作区既有改动未动）。
- ⛔ 未 rebase／未 reset／未 push。
