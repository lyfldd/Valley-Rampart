# `HH.320` · 伤害链口径统一批（批 1）· 任务书

- **签发**：策划端（砚）｜**依据**：`D843` ｜ **日期**：2026-09-23
- **由来**：`2026-09-23` 用户口径变更（4 轮讨论）——「射程要**投影上是圆**」「落点判定走**视觉 2.5D**」「命中底座即命中」
- **口径真源**：`最高优先级文档/07_伤害层.md` §二 空间口径 ＋ §七 判据 10~12（`D843` 修订）／`改造计划/2_5_伤害管线与战斗.md` **头部修订块**
- **基线**：`ef7c72a6`（`M1` 全片闭合）｜**行尾**：待验
- ⚠️ **批 2（`M3` 本体：分帧分片／旧事件退役／建筑致死走 `Remove` 门 ＋ `DZ-4`）⛔ 不在本批** ⇒ 批 1 验收后另派。

---

## 〇 · 一句话

**同一场战斗、同一段距离，代码里有三套换算法** ⇒ 收成一套（**世界空间欧氏圆**），并把与之绑死的**视线 / 遮挡 / 命中底座 / 随机源**一并归位。

---

## 一 · 现状（策划端实读 · 逐条 `file:line`）

| # | 事实 | 锚点 |
|---|---|---|
| 1 | 换算内核 ＝ 标准 **2:1 等轴**：`world = ( 0.64·(gx−gy) , 0.32·(gx+gy) )` | `GridSystem.cs:177-178` |
| 2 | 「格单位」＝ `√((Δx/1.28)²+(Δy/0.64)²)`（各向同性 ⇒ **世界呈 2:1 横扁椭圆**） | `GridMath.cs:20-25`／`GridMathCore.cs:9` |
| 3 | 判定侧 ＝ **格单位**（`DistCells > profile.range`） | `DamageSystem.cs:301` |
| 4 | 命中侧 ＝ **格单位**（`DistCells ≤ hitRadiusCells`）＋ 微格候选 `radiusCells × subDiv` | `ProjectileManager.cs:232/256/343` |
| 5 | ⛔ 索敌/开火侧 ＝ **世界标量**：`attackRange × GetCellSize()`，而 `GetCellSize() = cellSize.x = 1.28` | `UnitController.cs:1103-1107/1112/1154/1162` |
| 6 | ⛔ 建筑炮塔同式：`def.combat.range × cellSize.x`（＋ `cellRange = ceil(rangeWorld/cellSize)`） | `BuildingComponents.cs:75/100/104` |
| 7 | ⚠️ AI 侧有开关：`useGrid ? attackRange : attackRange × cs`（`AIDistConfig.useGridUnits`） | `NPCBrain.cs:970` |
| 8 | ⛔ **定量后果（码面推演）**：世界半径 `1.28R` ⇒ 横向 **R 格** ✅ ／ 纵向 **2R 格** ⛔ ⇒ **纵向射程 2 倍** | 由 #1＋#5 推出 |
| 9 | 视线 ＝ 按**格可走性**判：不可走含 `BuildingBlocked│Locked│Water`（`Bridge` 豁免）⇒ ⛔ **河/工地会挡箭** | `CombatRules.cs:58/68`＋`GridSystem.cs:273-276` |
| 10 | 遮挡**双链**：目标选择走 `HasLineOfSight`（微格 Bresenham · 受 `DamageConfig.cs:60` 开关管）／到达判定走 `CheckWallBlock`（世界带状＋弧高） | `CombatRules.cs:55-71`／`ProjectileManager.cs:291-331` |
| 11 | ⛔ `HighArc` 在**到达判定**已豁免（`:293`），但在**目标选择**仍被视线拦 ⇒ **越墙能力形同虚设** | 同上 |
| 12 | ⛔ 弹道落点散布用 `UnityEngine.Random`（`R4` 违规） | `ProjectileManager.cs:97-101` |
| 13 | ⛔ 击退角度同款 `R4` 违规 | `CombatRules.cs:36` |
| 14 | `2_5` 已立 `R5`「禁手写标量 cellSize」⇒ ⚠️ **实读仍违规 7 处（伤害链内）** | `2_5:103` |

---

## 二 · 范围（7 件）

### 件 1 · 换算分域（核心）

- ⭐ **新增唯一口**（建议形状）：`GridMath.DistVisual(Vector2 a, Vector2 b)` ⇒ 返回**视觉格数** ＝ `Vector2.Distance(a,b) / CellDiagHalf`，其中
  `CellDiagHalf = 0.5f × Mathf.Sqrt(CellW² + CellH²)`（＝**0.7155**，即格对角线之半）
- ⭐ **语义**：`DistVisual ≤ range` 即"**正轴向射程恰为 `range` 格**"（符合「射程 N 格」直觉）；⚠️ 对角方向比轴向远 ≈**11.8%**（预期内）
- ⚠️ **分域铁律**：**`GridMath.DistCells` ⛔ 不动**（寻路/派工/邻近查询仍用格单位）
- ⚠️ **字段语义不变**：`range`／`attackRange` 仍表示"格"，⛔ **不改数值**（换算法不换配置）
- ⚠️ **先报方案**：`ProjectileManager.cs:343` 的**微格候选换算**（`radiusCells × subDiv`）在与"视觉格"对齐时须保证**超集** ⇒ 先给方案再施工

### 件 2 · `R5` 违规 7 处统一（伤害链内）

| 落点 | 动作 |
|---|---|
| `UnitController.cs:1112`／`:1154`／`:1162` | 改走 `DistVisual`（索敌/开火射程） |
| `BuildingComponents.cs:75`／`:100`／`:104` | 同上（建筑炮塔） |
| `ProjectileManager.cs:303` | 带半宽 `cellSize.x*0.5` ⇒ 改走世界量（并报其与 `DistVisual` 的口径关系） |

⚠️ **本批只管伤害链**；**同族待治（⛔ 不塞本批，登记留痕）**：`GetCellSize()` 在感知/编队/冲锋等处的使用面（`UnitController.cs:536/651/710/888/891/933/944/1036/1142/1590/1676` 等）⇒ 单列挂账。

### 件 3 · 遮挡双链合一

- 目标选择与到达判定必须用**同一套阻挡语义**（件 4 定义）
- `CheckWallBlock` **保留**其"工事被击中 ⇒ 按穿透等级对墙伤害"职责 ⇒ ⛔ 不得丢语义
- ⚠️ 须列清两处的**全调用面**后再改

### 件 4 · 视线阻挡位（定义阻挡集合）

- ⭐ **只挡：山壁（地形硬阻挡）＋ 建筑**
- ⛔ **不挡：水（河/湖）、`Locked`（工地）、桥**
- 落地方式（建议）：⛔ **不新增独立数组** ⇒ 直接把 `HasLineOfSight` 的判据从 `IsSubWalkable` 换成「**该微格所属格**：① 地形硬阻挡（山壁）② 有建筑占格（`BuildingBlocked`）」
- ⭐ **必给反例读数**：隔河可射 ／ 隔工地可射 ／ 隔山壁不可射

### 件 5 · 高抛豁免视线

- `HighArc` 在**目标选择阶段亦豁免**视线（与 `ProjectileManager.cs:293` 对齐）
- ⚠️ **全调用面须查**（不可只改一处）

### 件 6 · 多格建筑 ＝ 菱形底座命中

- 语义：落点落在**以建筑 origin 为中心、按 `footprint` 张成的等轴菱形底座**内 ⇒ 命中（⛔ 不按「到 pivot 圆心距」）
- ⚠️ 数学式由执行端给（须报推导）；⭐ **判据须给三例**：中心 ／ 底座内 ／ 底座外

### 件 7 · 随机源（`R4`）

- 弹道落点散布 **保留**，随机源改**种子派生的 `System.Random`**（照 `VagrantCampSystem.NewDayRng:39` 先例）
- `CombatRules.cs:36` 击退角度 **同批**同改
- ⛔ 伤害链 `UnityEngine.Random` 零命中

---

## 三 · 判据（可验证 · 逐条）

1. **`R5` 清零**：机械扫描 ⇒ 伤害链内 `attackRange × cellSize` ／ `range × cellSize` ／ 标量 `GetCellSize()` **零命中**（附扫描输出 ＋ **别名后门核查**）
2. **射程两向对照**：⭐ **横向 R 格内可打 / R 格外不可打** ＋ ⭐ **纵向同距离同结果**（改前纵向 2R 的偏差消失）
3. **视线反例**：隔河 **可射** ／ 隔工地 **可射** ／ 隔山壁 **不可射**（各 ≥1 例 · 含对照）
4. **高抛**：高抛对墙后目标 **可选**（对照：低抛不可选）
5. **菱形底座**：三例读数（中心 / 底座内 / 底座外）
6. **散布 ＋ 确定性**：⭐ **同 seed 复跑两次逐字节一致** ＋ 散布确实存在（多点不同落点）
7. **`R4` 清零**：伤害链 `UnityEngine.Random` **零命中**（含 `CombatRules.cs:36`）
8. **寻路/派工零回归**：`GridMath.DistCells` 调用面与读数**不变**
9. **编译 0 error** ／ `AI.Core` **零触**
10. **平衡对照**：须给「改前 / 改后」纵向射程覆盖对照读数 ⇒ ⛔ **不得声称"无影响"**

---

## 四 · 红线

- ⛔ **不动 `AI.Core`**（`sim` 镜像**本批零义务** —— 用户 `2026-09-23` 裁定「不管 sim」）
- ⛔ **不改 `GridMath.DistCells`**（寻路/派工在用）
- ⛔ **不动 `M3` 本体三项**（分帧分片／旧事件退役／`Remove` 门）＋ ⛔ **不碰 `DZ-4`**
- ⛔ **不改 `range`／`attackRange` 数值**（只换换算）
- ⛔ 不碰训练仓／不碰 `GameScene.unity` 等既有脏点
- 具名 `git add`；**不 push**；**不 rebase / 不 reset**

---

## 五 · 排雷

| # | 雷 |
|---|---|
| **M1** | `GridMath.cs:13-14` 的 `CellW/CellH` 是**硬编码常量**，而 `GridConfig.cellSize` 是**资产** ⇒ **双源** ⇒ 本批须一并处置（改读配置 **或** 加一致性断言） |
| **M2** | `ProjectileManager.cs:343` 微格候选 ⇒ 必须是**超集**（否则漏命中） |
| **M3** | 件 3 合并时 ⛔ **不得丢失**「穿透等级 ⇒ 对墙伤害」 |
| **M4** | 件 5 须查**调用面**，⛔ 只改一处 |
| **M5** | `HasLineOfSight` 调用面 ＋ 开关 `DamageConfig.remoteNeedsLineOfSight` **当前值**须先报 |
| **M6** | 改判定后**战斗平衡会变**（纵向 2R→R）⇒ 须给对照读数，⛔ 不得称"无影响" |
| **M7** | `DZ-149` 同款残留：`BuildingComponents.cs:97` 注释仍写「y 地面+飞行两层」⇒ 注释须勘正 ＋ ⛔ **核实是否仍有代码残留** |
| **M8** | ⚠️ **"世界圆 ＝ 屏幕圆"的前提**是相机**正交 ＋ 无旋转** ⇒ 开片前须核；若不成立 ⇒ **停手报裁** |

---

## 六 · 开片前实核清单（先报，后施工）

1. 件 1 **微格候选换算方案**（超集证明）
2. `GetCellSize()` **全调用面分类**（伤害链 vs 非伤害链）
3. `HasLineOfSight` ／ `CheckWallBlock` **全调用面**
4. `DamageConfig.remoteNeedsLineOfSight` **当前值**（实际生效态）
5. 工事（`fortification`）**是否改变格可走性**（决定件 3/4 的交集）
6. 建筑占格**是否置 `BuildingBlocked`**（件 4 判据依据）
7. ⚠️ **相机是否正交 ＋ 无旋转**（M8）

---

## 七 · 交付物

- 改动清单（含 `file:line`）
- ⭐ **逐条判据的实测读数**（⛔ 禁形容词）
- ⭐ **两向对照**（横向 / 纵向）＋ **菱形底座三例** ＋ **视线三反例**
- ⭐ **同 seed 两次散布一致性**读数（逐字节对照）
- 机械扫描输出（`R5` ／ `R4` ／ `UnityEngine.Random` **零命中**）＋ **别名后门核查**
- **改前 / 改后**战斗覆盖对照读数
- 编译结果 ＋ `AI.Core` 零触证据

---

## 八 · 后续（不在本批）

- **批 2**：`M3` 本体（`M3-A` 分帧分片 ／ `M3-B` 旧事件退役 ／ `M3-C` 建筑致死走 `Remove` 门）＋ `DZ-4`（`DamageSystem` 假 null 守卫 · ⚠️ 高优）
- **同族挂账**：非伤害链的 `GetCellSize()` 标量口径（感知/编队/冲锋）

---

## 九 · `D844` 实核裁定（增补 · ⭐ **与上文冲突处以本节为准**）

> 依据：`多Agent交接/执行端/HH.320_伤害链口径统一批_开片前实核_报告.md`（提交 `aaa67eb6`）＋ 策划端**独立复算**（`HasLineOfSight` 零调用／`losEnabled` 实名／四处 `GetCellSize` 回退值／`subCellDivisor=4`）。

**✅ 前提通过**：相机 **正交 ＋ 零旋转**（`GameScene.unity:919` `orthographic: 1`／`:946` `m_LocalRotation {0,0,0,1}`）⇒ ⭐ **`M8` 不触发**，本批口径方向成立。

### 9.1 逐条裁定（原 §待裁 6 条）

| # | 待裁 | 裁定 |
|---|---|---|
| **1** | 目标选择视线链缺位 | ⭐ **采 (a)**：**本批只收口「到达判定」**（＝现状）⇒ **件 3 降级**为「登记：目标选择视线接线待批」｜**件 5 降级**（目标选择侧**无对象**；到达判定侧现状**已豁免** ⇒ 无需改）｜⚠️ **「远程无视线不索敌」当前未生效** ⇒ **是否接线 ＝ 待用户拍**（＝新增功能 · 另立判据 · ⛔ 不夹带进本批） |
| **2** | 件 4 阻挡集是否并入工事 | ⭐ **并入** ⇒ 阻挡集 ＝ **山壁 ＋ 建筑占格 ＋ 工事单位**（`fortification`）。⭐ 理由：实测**工事不改变格可走性**（`blocksMovement` 仅被 `UnitController.IsBlockedByFortification:1339/1352` 消费）⇒ 不显式并入会造出「工事挡弹道不挡视线」｜⭐ **唯一判据函数**供两链共用（目标选择链接线时直接复用） |
| **3** | 废墟保留占格 ⇒ 挡视线？ | ⭐ **算阻挡位**（`EnterRuined:1299` 保留占格）⇒ ⚠️ 由此产生「**废墟挡视线、不挡弹道**」的不对称 ⇒ ⭐ **接受 ＋ 登记为已知不对称**（`D844` 记账）⛔ 不作为缺陷 |
| **4** | 判据 8 字面不可达成 | ⭐ **改判据措辞**（本端认账）：判据 8 ＝ 「**`GridMath.DistCells` 在「寻路/派工」域的调用面与读数不变**」，⛔ 非"全库调用总数"（伤害链 3 处改走 `DistVisual` ⇒ `DistCells` −3 ＝ **预期**） |
| **5** | 同批边界 3 组 | ⭐ **全部纳入本批**（均属伤害链距离）：`DamageSystem.cs:436/471`（庇护／溅射）／`GroundEffectManager.cs:212`（地面效果）／`MonsterAI.cs:196/203-206`（怪物索敌）⇒ ⭐ **件 2 范围由 7 处扩为 13 处**；⚠️ 另注：`MonsterAI.cs:203` 是**自建私有 `DistCells`** ⇒ 须一并统一 |
| **6** | 判据 2「R 格」计数口径 | ⭐ **钉死**：「**`R` 格 ＝ 沿地图轴向（`gx` 或 `gy` 单轴）的格坐标差**」⇒ 判据须用**格坐标**取点，⛔ 不用世界单位／像素<br>　`(cx + R, cy)` ⇒ **可打**（`DistVisual == R` · 边界含）｜`(cx + R + 1, cy)` ⇒ **不可打**<br>　`(cx, cy + R)` ⇒ **可打**｜`(cx, cy + R + 1)` ⇒ **不可打**<br>⛔ 禁用「屏幕横向格宽」类取点（同一世界圆折 `R` / `0.559R` / `1.118R` 格 ⇒ 口径不唯一） |

### 9.2 本端认账（签发侧缺陷 · 3 条）

1. ⛔ **字段名不存在**：`M5` ／ `07` §四 所写 `DamageConfig.remoteNeedsLineOfSight` ⇒ ⭐ **实名 `losEnabled`**（`DamageConfig.cs:61` · `.asset:28 = 1`）⇒ **已勘正**。
2. ⛔ **「遮挡双链」表述不实**：`HasLineOfSight` **全库零调用点**（自 `e1a6fdf2` 引入即死码）⇒ 实为**单链** ⇒ **已勘正**。
3. ⛔ **判据 8 未限定域** ⇒ **已勘正**（见 9.1 #4）。

⇒ ⭐ **根因**：签发时**未执行自家 `L-77` 双读数**（"在役须两条硬读数：方法体非空 ＋ 调用面 > 0"）⇒ 立 **`L-86`**。

### 9.3 本批最终范围（9.1 后 · 以此为准）

**件 1** 换算分域（世界欧氏圆 · `DistVisual`）｜**件 2** 换算统一 —— ⭐ **13 处**（原 7 ＋ 新增 6）｜**件 3** ⏳ **降级·登记**（不施工）｜**件 4** 视线阻挡集 ＝ **山壁＋建筑占格＋工事单位** ｜**件 5** ⏳ **降级**（无对象）｜**件 6** 菱形底座｜**件 7** 随机源（`R4`）。

⚠️ **仍须先报方案的两项**：件 1 微格候选超集换算（`subDiv = 4` 实读 ⇒ 子格 `(0.32, 0.16)`；`R=0.25` 视觉格时 `subRange` 应由 1 改 **2**）｜件 7 种子派生形状。

---

### 9.4 ⭐ `件 3′` · 目标选择视线接线（`D845` 拍板 · **并入本批施工 · 独立判据**）

> ⚠️ 原「件 3 双链合一」因实盘**目标选择链根本不存在**而降级（§9.1-1）；本件 ＝ 将其**复活为「接线」**（＝**新增功能**）。
> ⭐ **为何并批而不另立小批**：其落点与 **件 2** 是**同一批函数**（`UnitController.FindNearestEnemy*` ／ `BuildingComponents.FindNearestEnemyInRange`）⇒ 分两批＝同函数进两次场；⇒ **同批施工 ＋ 独立判据**（验收可分）。

**问题（现象）**：远程**只看距离**索敌 ⇒ 隔着山壁/建筑也把目标选上 ⇒ 弹道飞到一半被 `ProjectileManager.CheckWallBlock` 挡掉 ⇒ ⛔ 表现为「**弓箭手对着山壁一直射**」。

**接线形状（本端拍板 · 施工照此）**

1. **落点 4 处 —— ⛔ 仅远程生效**：
   - `UnitController.FindNearestEnemy(rangeWorld)`（`:1121`）⇒ 加 `isRanged` 形参 ＋ ⭐ **逐个候选过滤无视线者**（⛔ **不得**"任一无视线即整体放弃" ⇒ 须**跳过该候选、继续找次近的有视线者**）
   - `UnitController.cs:1154`（`fireTarget`）／`:1162`（开火二次判定）⇒ **同源**（与索敌同判据，⛔ 不得两套）
   - `BuildingComponents.FindNearestEnemyInRange`（`:98`）⇒ 建筑炮塔**全远程** ⇒ 直接过滤
   - `MonsterAI.cs:196/203-206` ⇒ 按 `prof.isRanged` 过滤
   - ⛔ **近战（`isRanged == false`）不过滤**（用户口径：「近战不要视线」）
2. **判据函数 ＝ 唯一口** `CombatRules.HasLineOfSight(from, to)`（**改造后**）
   - **阻挡集**（`D844` 裁定）：**山壁** ＋ **建筑占格（`WalkFlags.BuildingBlocked`）** ＋ **工事单位**
   - ⛔ **不挡**：水（`Water`）／`Locked`（工地）／桥（`Bridge`）
    - ⚠️ **工事单位的查法（性能硬约束 · ⭐ `D846` 令式撤回）**：⛔ **原令式「沿 Bresenham 逐微格查 `GetUnitsInSubCell`」＋「禁全库遍历 `UnitRegistry`」已撤回** —— 实测 `GridSystem.GetUnitsInSubCell`（`:500-506`）＝ **全表枚举 ＋ 每次 `new List`**（`O(N)` **＋分配**）⇒ 照令 ＝ `O(N)×L`，**比被禁项差 L 倍**；而 `UnitRegistry.GetAllUnits()` ~~返回内部 `List` 引用 ⇒ 零分配~~ ⛔ **【`D847` 勘正 · 本端之误】**：`:11` 声明为 **`HashSet<UnitController>`**（⛔ 非 `List`），且返回类型是 `IEnumerable<UnitController>` ⇒ `foreach` 走接口 ⇒ **结构枚举器装箱 ⇒ 每次 1 次堆分配** ⇒ **并不零分配**；⭐ 真零分配口 ＝ `UnitRegistry.GetUnitsEnumerator():57`（同集合结构枚举器 · `D847` 采）（⚠️ 遍历期禁增删 · `HH.76/D539` 雷区纪律）⇒ ⭐ **改为目标导向约束**：① 单次视线检查 **零堆分配**（⛔ 禁 `new List`／LINQ）② 稳态 **零 GC** ③ **每次索敌的视线检查总耗时 ＋ 候选数**须入报告 ④ ⭐ **须给对照读数**：「**带测法**（复用 `CheckWallBlock:308-324` 同构：遍历 `GetAllUnits()` ＋ 点到线段距离 ≤ 半带宽 ＋ 早退）」**vs**「逐微格法」**vs**「增量反查索引」⇒ ⛔ **不得照抄旧令式**
   - ⚠️ **山壁的判据来源**：须实读 `FeatureType` 中属"地形硬阻挡"的取值（⛔ 不得凭猜 ⇒ 先报清单）
3. **高抛豁免**：调用方按 `ballisticType == HighArc` **跳过**视线检查
4. **成本**：候选数受 `PerceptionSystem.QueryNearby` 限制 ＋ 工事沿路径查 ⇒ ⭐ **须给"单次索敌的视线检查耗时 ＋ 候选数"读数**

**本件判据（独立于件 1~7）**

1. ⭐ **隔山壁 ⇒ 不索敌**；**对照**：同位置**拆掉**山壁 ⇒ **索敌成功** ⇒ ⭐ **鉴别力自证**
2. **隔建筑占格 ⇒ 不索敌**
3. **隔工事单位 ⇒ 不索敌**
4. ⭐ **隔河（水）⇒ 照常索敌**（**不挡** · 反例可证）
5. **隔 `Locked`（工地）⇒ 照常索敌**
6. **高抛（`HighArc`）⇒ 照常索敌**（豁免）
7. ⭐ **近战 ⇒ 照常索敌**（`isRanged=false` 不查视线）
8. **成本读数**：单次索敌耗时 ＋ 候选数

⚠️ **平衡影响须对照**：接线后**远程实际变弱**（打不到墙后）⇒ ⭐ 交付须给「接线前 / 后」的**有效交战距离**对照读数，⛔ 不得称"无影响"。

---

## 十、`D846` 方案报裁裁定（批 1 第一段 · 2026-09-23）

> 被裁：执行端第一段交付 **`3526c3e3`**（仅 1 文件 · `+232/−0` · ⛔ `Assets/**` 零写 · 未 push · 未进 Play）⇒ ⭐ **停手待裁正确**。
> 本端**逐项独立复算**（`git grep` ／ `file:line` 实读 ／ GUID 反查），⛔ **未采信转述**。

### 10.1 `S1` 山壁取值清单 ⇒ ✅ **采**（附强化）

- ✅ **核实成立**：`FeatureType` 9 值（`GridTypes.cs:77-86`）中**非可走且非水**者仅 **`Mountain`／`SnowMountain`**。
- ✅ **两处派生映射同集合**（本端实读）：`GridSystem.FeatureToWalkFlags:112-126`（`default: return WalkFlags.None`）／`MapGenRules.IsWalkableFeature:88-97`（`default: return false`）⇒ ⚠️ 二者皆走 **`default` ⇒ 「未知即挡」** ⇒ ⭐ **采「显式枚举 ＋ 一致性哨兵」**（已采纳执行端建议）。
- ✅ **唯一创建点** `MapGenRules.StampMountain:759`（声明）／`:764`（`MapGate.GenesisWrite` 写入行 · 执行端报 `:764` 指向**写入点** ⇒ ⭐ 更精确，**属读法不同 · 非错**）；`PlaceMountainRidges:747`／`PruneMountainSpecks:769`。⚠️ 只减不增（`PlaceRiver` 不覆盖山体）。
- ✅ **判据 4「拆山壁」走正门** `MapGate.SetFeature`（⛔ 不照抄探针裸写）—— 采纳。
- ⛔ **明确排除**：`River`／`Ocean`（水 · `D844` 不挡）／`Mine`（可走可建）。

### 10.2 `S2` 工事沿路径查 ⇒ ⭐⭐ **撤回本端令式 · 改目标导向约束**

- ⛔ **本端认账（双错）**：§9.4 第 2 条我写死「⭐ **沿 Bresenham 逐微格查** `GetUnitsInSubCell`」＋「⛔ **不得全库遍历** `UnitRegistry`」：
  1. ✅ **执行端「令式前提与实盘不符」成立**（本端复算）：`GridSystem.GetUnitsInSubCell:500-506` ＝ **全表枚举 `_unitSubCells` ＋ 每次 `new List`** ⇒ 照令 ＝ `O(N)×L` **且每微格一次分配**。
  2. ⛔ **我的禁令方向错**：`UnitRegistry.GetAllUnits():47-50` **返回内部 `List` 引用 ⇒ 零分配 O(N) 单遍** —— ⭐ 我凭「结构直觉」把**最优解**禁掉了。
- ⭐ **裁定 ＝ 撤回令式，改目标导向**：① **零堆分配** ② 稳态**零 GC** ③ 报告须给**每次索敌视线检查耗时 ＋ 候选数** ④ ⭐ **须给对照**：**带测法**（复用 `CheckWallBlock:308-324` 同构）**vs 逐微格法** **vs 增量反查索引**。
- ⭐ **本端倾向**：**带测法** —— 与现役 `CheckWallBlock` **同构**、**零新基建**、零分配；⚠️ 增量反查索引（改 `GridSystem` 加镜像索引）**新引入漂移面** ⇒ ⭐ **除实测证明带测法不可接受外，⛔ 不采**。
- ✅ **三项待确认，本端裁定**：
  1. **`fortification != null` 全含** ⇒ ✅ **是**（含 `blocksMovement=0` 的三塔 —— `heightCells=3` 的结构；且与 `CheckWallBlock:311` 同口径，它只滤 `fortification == null`）。
  2. **城门 `passable=1` 照挡** ⇒ ✅ **是**（与 `CheckWallBlock` 同口径；⚠️ 城门昼夜开关 `FortificationPassableOverride` **不参与视线**）。
  3. ⭐⭐ **新立铁律（两链共用）**：**视线阻挡判据必须与弹道阻挡判据同源** —— ⛔ 否则「选得到／打不到」不对称**必然复现**（即本次要除的病）。
- ⚠️ **附本端增量取证（执行端未报）**：工事在役载体 ＝ **单位** —— `Resources/UnitData/{Wall,Gate,ArrowTower,CrossbowTower,MagicTower}.asset:50` 各引 `Resources/Fortifications/*.asset`（`FortificationDef`）⇒ ⭐ `CheckWallBlock` **今天确实生效**。实读五资产参数：

  | 资产 | `defenseLevel` | `blocksMovement` | `passable` | `heightCells` |
  |---|---|---|---|---|
  | `Wall` | 2 | 1 | 0 | 2 |
  | `Gate` | 1 | 1 | **1** | 2 |
  | `ArrowTower`／`CrossbowTower`／`MagicTower` | 1 | **0** | 0 | **3** |

### 10.3 `S3` 微格候选超集换算 ⇒ ✅ **采**

- ✅ **闭式成立**（本端独立复算）：`n = ⌈R_vis × subDiv × (cellW²+cellH²)/(2·cellW·cellH)⌉ = ⌈5·R_vis⌉` ⇒ `R_vis=0.25` ⇒ **`subRange` 1 → 2** ✓。
- ✅ **三份拷贝（本端机械扫描 · 恰 3 处）**：`ProjectileManager.cs:343`／`DamageSystem.cs:490`／`GroundEffectManager.cs:202` ⇒ ⭐ **收口为单一口 ＋ `subW`/`subH` 现算** 采纳。
- ⭐ **本端附带发现（同族第 4 处 · 语义不同 ⇒ ⛔ 登记不施工）**：`VisionSystem.cs:28  int range = Mathf.CeilToInt(radiusWorld / cs)` ＝ **world → 格** 换算（⛔ 非 `cells × subDiv`）⇒ 计入「同族待治」，本批不动。

### 10.4 `DistCells` 计数差（−3 vs −6）⇒ ⭐ **裁 `−6`（执行端正确）**

- ✅ **本端独立复算**：`git grep GridMath.DistCells` ⇒ **伤害链域恰 6 处** ＝ `DamageSystem.cs:301`／`:436`／`:471` ＋ `ProjectileManager.cs:232`／`:255` ＋ `GroundEffectManager.cs:212`。
- ⚠️ **不计入者（本端分类）**：`Building.cs:1635`（**仓库/井定位** ＝ 资源域）／`TaskScheduler.cs:332/1088/1137/1238`（**派工域** · 不动）／`MonsterAI.cs:86/103/117/131/196`（调**私有** `MonsterAI.DistCells:203` ⇒ ⛔ 非 `GridMath` ⇒ 不进本计数，但**自身须统一** ⇒ 见件 2）。
- ⇒ ⭐ **判据 3 最终措辞**：「`GridMath.DistCells` 在**寻路/派工域**调用面与读数**不变**；**伤害链 −6 属预期**」。
- ⚠️ **微瑕（不阻断）**：执行端报 `ProjectileManager 232/256` ⇒ 实读 **`:255`**（差 1 行）。

### 10.5 放行范围

⭐ **放行第二段 ＝ 件 1 ／ 2（13 处）／ 3′（判据 8 条单列）／ 4 ／ 6 ／ 7**。⛔ **不放行**「照令逐微格」；视线检查**形状由执行端按 §10.2 自选并报对照读数**。
