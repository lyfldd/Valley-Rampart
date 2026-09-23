# `HH.320` · 伤害链口径统一批（批 1）· 交付报告 · **第二段施工**

- **执行端**：TraeCode｜**日期**：2026-09-23｜**性质**：第二段**施工**（放行后 7 件）
- **依据**：任务书 §九（`D844`）＋ §9.4（件 3′）＋ ⭐ **§十（`D846` 裁定）**＋ 口径真源 `最高优先级文档/07_伤害层.md` §二/§七｜前置 `HH.320_..._交付报告.md`（第一段 · `3526c3e3`）
- **基线对齐**：`git log --oneline -3` ⇒ **`4eaf3a58`**（`D846` 裁定）← `3526c3e3`（第一段）← `f09d7e8f` ⇒ 与任务书「基线 `4eaf3a58`」一致；⛔ 未 rebase／未 reset／未 push。
- ⛔ **第一段报告文件名未覆盖**（本文件为**另存后缀**：`..._交付报告_第二段施工.md`）。
- **红线遵守**：⛔ 未动 `AI.Core`（`git diff --stat -- Assets/_Game/Systems/AI.Core` **空**）／⛔ `GridMath.DistCells` 未改语义（寻路/派工域调用面不变）／⛔ 未动 `M3` 本体三项与 `DZ-4`／⛔ 未改 `range`/`attackRange` 数值／⛔ 未碰训练仓、`GameScene.unity`、`Packages`、美术脏点。

---

## §0 · 首段结论（三行）

1. **7 件全部落地**：12 个生产文件 `+449/−127`；**编译 0 error**；`AI.Core` 零触。
2. **判据 1~18 全部取得实测读数**（含两向对照／三反例／三形状成本／同 seed 复跑逐字节一致）；**视线形状 ＝ 带测法**（`D846` §10.2 目标导向 ⇒ 零堆分配 · 稳态零 GC · 零新基建），三形状对照读数见 §三。
3. **两处须策划端知悉的实质发现**：① `UnitRegistry.GetAllUnits()` **不是零分配**（`IEnumerable` ⇒ `HashSet` 结构枚举器**装箱**；§10.2 所记「返回内部 List」与实盘不符）⇒ 本批新增零分配枚举口；② 件 6 接线后，**远程首次能命中建筑**（此前 `OnProjectileArrived` 只收单位候选 ⇒ 远程对建筑**恒 miss**）＝ 顺带闭合的真实缺陷。

---

## §一 · 改动清单（`file:line` · 全部为**最终行号**）

### 件 1 · 换算分域（唯一口）

| 落点 | 内容 |
|---|---|
| `GridMath.cs:43` | `CellStep`（格步长世界量 ＝ 0.5√(cellW²+cellH²) ＝ **0.715542**） |
| `GridMath.cs:50` | `Bind(Vector2)` —— **唯一绑定口**（真源 ＝ `GridConfig.cellSize`；`M1` 处置） |
| `GridMath.cs:89` | ⭐ `DistVisual(a,b)` ＝ `Vector2.Distance / CellStep`（**件 1 唯一换算口**） |
| `GridMath.cs:93/96` | `VisualToWorld` / `WorldToVisual`（双向换算唯一口 · ⛔ 禁调用方自行 `×0.7155`） |
| `GridMath.cs:101` | `PathBandHalf`（带半宽世界量 ＝ `cellW/2` ＝ 0.64；与视觉格关系 0.8944 格） |
| `GridMath.cs:108/118` | `SubWindowForVisualRadius` / `CellWindowForVisualRadius`（**超集窗**唯一口 · 现配置 ⌈5·R⌉ / ⌈1.25·R⌉） |
| ⛔ 未动 | `DistCells`／`DirCells` 语义与算式（现读 `_cellW/_cellH`，值 ＝ 编译期默认 ⇒ **行为不变**） |

### 件 2 · 换算统一（13 处 ＋ 伤害链 DistCells 6 处）

| # | 落点（最终行号） | 动作 |
|---|---|---|
| 1 | `UnitController.cs:1112-1113` | `FindNearestEnemyInRange` ⇒ 射程经 `VisualToWorld` 入参（⛔ 去 `× GetCellSize()`） |
| 2 | `UnitController.cs:1161` | 乘员机器 `fireTarget` 同改 |
| 3 | `UnitController.cs:1171` | 开火前距离复核 ⇒ `DistVisual ≤ attackRange` |
| 4 | `BuildingComponents.cs:75-76` | 炮塔射程 `def.combat.range` **直接**为视觉格 |
| 5 | `BuildingComponents.cs:117` | 扫描窗 ⇒ `SubWindowForVisualRadius`（**超集**；⛔ 旧 `⌈rangeWorld/cellSize⌉` 非超集） |
| 6 | `BuildingComponents.cs:132` | 命中过滤 ⇒ `DistVisual ≤ rangeVisual` |
| 7 | `ProjectileManager.cs:305` | 带半宽 ⇒ `GridMath.PathBandHalf`（⛔ 去 `cellSize.x*0.5f`） |
| 8 | `DamageSystem.cs:305` | 射程判定 ⇒ `DistVisual > profile.range` |
| 9 | `DamageSystem.cs:441` | 庇护半径 ⇒ `DistVisual ≤ shelterRadiusCells` |
| 10 | `DamageSystem.cs:477` | 溅射半径 ⇒ `DistVisual ≤ aoeRadiusCells` |
| 11 | `GroundEffectManager.cs:214` | 地面效果半径 ⇒ `DistVisual ≤ radiusCells` |
| 12 | `MonsterAI.cs:210` | 怪物自建私有 `DistCells` ⇒ **薄转调 `GridMath.DistVisual`**（本类 5 处调用点随之同口径） |
| 13 | `MonsterController.cs:93` | `FindNearestHuman` 入参改**视觉格**（经 `VisualToWorld` 喂 `QueryNearby`） |
| ★ | `ProjectileManager.cs:235/263` ＋ `DamageSystem.cs:305/441/477` ＋ `GroundEffectManager.cs:214` | **伤害链 `GridMath.DistCells` 6 处 → `DistVisual`**（`D846` §10.4 裁「−6」） |

### 件 3′ · 目标选择视线接线（仅远程）

| 落点 | 内容 |
|---|---|
| `UnitController.cs:1121-1143` | `FindNearestEnemy(rangeWorld, isRanged)`：**逐个候选过滤无视线者**（⛔ 非整体放弃）＋ 距离排序改 `DistVisual` ＋ `needSight = isRanged && ballisticType != HighArc` |
| `UnitController.cs:1140` | 视线判据调用（唯一口） |
| `BuildingComponents.cs:134` | 炮塔**全远程** ⇒ 逐个候选过滤 |
| `MonsterAI.cs:199` | `PickBuildingTarget(isRanged)`：远程逐个候选过滤；`ignoreOccupant = b`（终点实体自身占格豁免） |
| ⛔ 近战 | `isRanged == false` **不查视线**（用户口径）；`HighArc` 豁免（`UnitController.cs:1136`） |

### 件 4 · 视线阻挡集（唯一口 · 与件 3′ 共用）

| 落点 | 内容 |
|---|---|
| `CombatRules.cs:130` | ⭐ `HasLineOfSight(from, to, ignoreOccupant)` —— **唯一口**（零迭代器/零 List/零装箱） |
| `CombatRules.cs:141-171` | ① 山壁（`GridSystem.IsSightBlockingFeature`）② 建筑占格（`WalkFlags.BuildingBlocked` 位测 · 含废墟）③ 工事单位（**与弹道链同一函数**） |
| `GridSystem.cs:141` | `IsSightBlockingFeature` ＝ **显式枚举** `{Mountain, SnowMountain}`（`P1`） |
| `GridSystem.cs:149` | ⭐ **一致性哨兵** `VerifySightBlockingFeatureSet()`（9 值全枚举对拍「显式集合 ⟺ 派生位」；`#if UNITY_EDITOR` ⇒ 运行时零开销；`GridSystem.cs:54` 调用） |
| `CombatRules.cs:177` | ⭐ `FindFortificationBlocker(...)` —— **工事阻挡唯一口**（`D846` `P4` 同源铁律） |
| `ProjectileManager.cs:306-310` | `CheckWallBlock` 改调唯一口（弧高豁免逐字保留；**对墙伤害语义未丢**） |
| ⛔ 不挡 | 水（`River/Ocean`）／`Locked`（工地）／桥（`Bridge`）—— 本函数**不读**这三类 |

### 件 6 · 多格建筑 ＝ 菱形底座命中

| 落点 | 内容 |
|---|---|
| `CombatRules.cs:222` | ⭐ `InDiamondBase(point, centerWorld, footprint, cellSize)`（格坐标下轴对齐矩形 ⟺ 世界空间等轴菱形底座；边界含） |
| `ProjectileManager.cs:246` ＋ `:324` | 到达判定**新增**建筑底座命中（单位命中优先 ⇒ 既有语义零回归；3×3 `BuildingRegistry.GetAt`（`O(1)`×9 · 零分配）+ 精确底座复核） |

### 件 7 · 随机源（`R4`）

| 落点 | 内容 |
|---|---|
| `CombatRules.cs:33-70` | 种子派生 `System.Random` 流（种源＝世界种子）＋ `ResetCombatRandom(seed=0)`（唯一重置口）＋ `NextSpreadOffset`（均匀圆盘，**散布保留**）＋ `RollChance` |
| `CombatRules.cs:69` | `ComputeKnockback` θ ⇒ `NextKnockbackThetaDeg()`（⛔ 去 `UnityEngine.Random.Range`） |
| `ProjectileManager.cs:102` | 落点散布 ⇒ `CombatRules.NextSpreadOffset`（⛔ 去 `Random.insideUnitCircle`） |
| `NPCBrain.cs:1707-1708` | ⚠️ **扫描发现第三处**：冲锋击飞反向 ⇒ `CombatRules.RollChance(0.2f)`（⛔ 去 `Random.value`；清单未列，本端一并改并登记） |
| `DamageSystem.cs:129` | `ResetState()` 增 `ResetCombatRandom()`（每局重播 ⇒ 同 seed 复跑逐字一致） |

### 件 18 · `subRange` 收口（三份拷贝 → 单一口）

| 落点 | 内容 |
|---|---|
| `ProjectileManager.cs:358` / `DamageSystem.cs:497` / `GroundEffectManager.cs:203` | 均改 `GridMath.SubWindowForVisualRadius(radiusCells, subDiv)`（参数**现算** · ⛔ 不写死 0.32/0.16） |

### 件 17 · `M1` 双源处置

| 落点 | 内容 |
|---|---|
| `GridSystem.cs:52` | `GridMath.Bind(config.cellSize)`（**改读配置**；未绑定回退编译期默认 ⇒ 行为不变；值漂移记 Log） |

### 披露的**清单外改动**（4 项 · 详见 §六-3）

| 落点 | 内容 |
|---|---|
| `UnitRegistry.cs:57` | `GetUnitsEnumerator()`（**零分配**枚举口） |
| `BuildingComponents.cs:117-120` | 炮塔扫描 ⇒ **单遍** `FillUnitsInRect`（⛔ 废逐格 `GetUnitsInCell`） |
| `NPCBrain.cs:1707-1708` | 第三处伤害链 `UnityEngine.Random` |
| `MonsterController.cs:85-93` | `FindNearestHuman` 入参语义（含其 `CellSize()` 标量去向） |

---

## §二 · 逐条判据实测读数

> 产物：`Logs/hh320_math_20260923_152153.txt`（数学面 · Edit Mode）／`Logs/hh320_live_20260923_160031.txt`（进局面 · 正门 `TestHarnessApi.EnterTestRun` · seed `20320` · 真暂停 `timeScale=0`）。

### 判据 1 · `R5` 清零（机械扫描 ＋ 别名后门）

扫描式：`git grep -n -E "attackRange \*|\.range \* .*[Cc]ellSize|[Cc]ellSize\(\)"` over `Systems/Combat` ＋ `UnitController` ＋ `BuildingComponents` ＋ `MonsterAI/MonsterController`：

```
MonsterController.cs:151  float cell = CellSize();                                 ← 移动面（移速换算）·挂账
MonsterController.cs:213  private static float CellSize()                           ← 定义体本身
UnitController.cs:1072    float aoeWorld = _professionSnapshot.aoeRadiusCells * 2f * GetCellSize();  ← AOE 密集判据/目标价值面 ·挂账
UnitController.cs:1103    private float GetCellSize()                               ← 定义体本身
UnitController.cs:1164    FindNearestEnemy(_professionSnapshot.perceptionRadius * GetCellSize(), false)  ← 感知面 ·挂账
UnitController.cs:1206    float crewRadius = _professionSnapshot.crewRadiusCells * GetCellSize();    ← 感知/编队面 ·挂账
UnitController.cs:1241    HasNearbyEnemy() => HasNearbyEnemy(_professionSnapshot.perceptionRadius * GetCellSize())  ← 感知面 ·挂账
```

⇒ **伤害链射击路径（索敌/开火/判定/命中/溅射/地面效果/怪物索敌）零命中** ✅；残留 6 行 = 方法定义 ＋ **同族待治面**（任务书 §件 2 明列：感知/编队 ＋ 本端新登记 2 处：`MonsterController.cs:151` 移速换算、`UnitController.cs:1072` AOE 判据）。
**别名后门核查**：`Buildings/UnitData` 侧以 `GridMath.CellW/CellH` 取用的短路亦已核（`GridMath.cs` 内 6 处 `_cellW/_cellH` 为**真源读写**，无第二份常量）；`ProjectileManager.cs:305` 旧式 `cellSize.x * 0.5f` **已入 `PathBandHalf` 单一口**。

### 判据 2 · 射程两向对照（⭐ 格坐标取点）

**数学面（`DistVisual ≤ R` · R=6）**：

| 方向 | Δ格 | DistVisual | 新门 | DistCells | 旧判定门 | 世界距离 | 旧索敌圆门 | 旧有效(①∩②) |
|---|---|---|---|---|---|---|---|---|
| gx | 5 | 4.999999 | 可 | 3.535532 | 可 | 3.577708 | 可 | 可 |
| gx | **6** | **5.999999** | **可** | 4.242639 | 可 | 4.293250 | 可 | 可 |
| gx | **7** | **6.999999** | **不** | 4.949745 | 可 | 5.008791 | 可 | 可 |
| gy | 5 | 4.999999 | 可 | 3.535532 | 可 | 3.577708 | 可 | 可 |
| gy | **6** | **5.999999** | **可** | 4.242639 | 可 | 4.293250 | 可 | 可 |
| gy | **7** | **6.999999** | **不** | 4.949745 | 可 | 5.008791 | 可 | 可 |

（gx/gy 两向**逐值相同** ⇒ 改前「纵向 2 倍」偏差消失 ✅。k=8..11 略，逐向同表；k=9 起旧判定门亦不 ⇒ 旧有效上限 = **8 格**。）
**边界含**：`[J2·边界比] k=6 DistVisual/R = 0.9999998`（严格 ≤）。
**生产路径（真索敌链 · 格中心取点）**：

```
[J2·生产路径] 靶@+4格=True(unit:Human_Player_Archer:AiKingdom) | 靶@+7格=False(null) | 复位=True
[J2·边界含]   靶@+6格(格中心) 索敌=True · DistVisual=5.999999 · R=6 · 比值=0.9999998 · 感知门命中数=1
```

### 判据 3 · 寻路/派工零回归（⭐ 限定域）

`git grep -n "GridMath.DistCells"` 剩余命中：`TaskScheduler.cs:332/1088/1137/1238`（**派工域**）／`Building.cs:1635`（仓库/井定位 ＝ 资源域）／1 处 Editor 探针 ⇒ **寻路/派工域调用面与读数不变** ✅；**伤害链 −6** ✅（＝ `D846` §10.4 裁定值）。

### 判据 4 · 隔山壁 ⇒ 不索敌（＋ 拆掉 ⇒ 成功）

```
[J4隔山壁·诊断] A=(15.36,18.24) B=(17.92,19.52) subA=(162,66) subB=(178,66)
                feature(mid)=Mountain · MapGate.ReadAt=Mountain · IsSightBlockingFeature=True · LOS=False
[J4隔山壁·诊断·路径] (168,66)c(42,16)Mountain:w0 (169..171,66)c(42,16)Mountain:w0   ← 逐微格命中
[J4] SetFeature(Mountain)=True SetFeature(Plain)=True
     | 无阻挡基线=True | 隔山壁=False(null) | 拆山壁后(正门 SetFeature→Plain)=True ⇒ **鉴别力自证**
```

### 判据 5 · 隔建筑占格 ⇒ 不索敌

```
[J5] 建筑=wall.asset 落地 · 占格BuildingBlocked=True · 隔建筑占格索敌=False(null)
[J5·清理后对照] 拆建筑后索敌=True ⇒ **鉴别力自证**
```

### 判据 6 · 隔工事单位 ⇒ 不索敌（两例）

```
[J6] 工事=Wall(blocksMovement=True heightCells=2)          索敌=False(null)
[J6] 工事=ArrowTower(blocksMovement=False heightCells=3)   索敌=False(null)   ← P3「全含」实测
```

### 判据 7 · 隔河（Water）⇒ 照常索敌（不挡 · 反例）

```
[J7] 隔河(River) 索敌=True(unit:...:AiKingdom) · flagWater=True
```

### 判据 8 · 隔 Locked（工地）⇒ 照常索敌

```
[J8] 隔 Locked 索敌=True · flagLocked=True · 该格可走=False（构造有效：Locked 已生效但仍不挡视线）
```

### 判据 9 · 高抛（HighArc）⇒ 照常索敌（豁免）

```
[J9] 隔山壁：低抛(Lob)=False(null) | 高抛(HighArc)=True(unit:...) | 还原 Lob=False(null) ⇒ 三向对照
```

### 判据 10 · 近战 ⇒ 照常索敌（`isRanged=false` 不查视线）

```
[J10] 同一几何（隔山壁）FindNearestEnemy(isRanged=false)=True(unit:...) / isRanged=true=False(null)
```

### 判据 11 · 成本读数（三形状对照 ＋ 候选数 ＋ GC）

见 §三。

### 判据 12 · 菱形底座三例

```
[J12] footprint=(3,3) origin=(100,100) center=(1.28,64.64) 底座半宽=1.5格
[J12] 中心      InDiamondBase=True
[J12] 底座内 Δg=(1.4,0)  InDiamondBase=True   （世界距离=1.0018）
[J12] 底座外 Δg=(1.6,0)  InDiamondBase=False
[J12] 对角内 Δg=(1.4,1.4) InDiamondBase=True  （世界距离=0.8960 —— 若按旧「pivot 圆心距 < 半格」口径该点会判 False ⇒ 底座口径生效）
```

### 判据 13 · 散布 ＋ 确定性

```
[J13] 散布确定性子 len=1517 hash=9CC5A50A
[J13] 同 seed(12345) 复跑 len=1517 hash=9CC5A50A ⇒ 两次签名一致=True｜散点数 distinct=64/64（散布确实存在）
[J13] 异 seed(99999) len=1489 hash=026E552E ⇒ 与 12345 不同=True（鉴别力）
[J13·击退角] seed=777 两次 hash=ECB9B141 / ECB9B141 ⇒ 一致=True｜seed=778=A05F88F6 ⇒ 不同=True
```

### 判据 14 · `R4` 清零（含 `CombatRules` 击退角）

`git grep -n -E "UnityEngine\.Random|Random\.(value|Range|insideUnitCircle)" -- Assets/_Game/Systems/Combat` ⇒ 8 条命中**全为注释/文档串**（`CombatRules.cs:13/27/52/63/68/69`、`DamageSystem.cs:629`、`ProjectileManager.cs:98`）⇒ **实码 0 命中** ✅。
**范围外（已登记 · 不属伤害链）**：`NPCBrain.cs:851/857`（对白间隔）／`UnitController.cs:1504/1509`（对白文案）／`SpriteAnimator`／`CharacterCreationPanel`／`WorldManager:109`。

### 判据 15 · 编译 ＋ `AI.Core` 零触

- `unity_editor refresh`（Codely 桥 · 同步编译）⇒ **errors 0**（warnings 10~25 条全为存量：CS0114/CS0108/CS0252/CS0414/CS0162/CS0219/CS0618，无一来自本批新码）。
- `git diff --stat -- Assets/_Game/Systems/AI.Core` ⇒ **空**（零触 ✅）。

### 判据 16 · 平衡对照（接线前/后有效交战距离）

以弓箭手 `attackRange=6` 为例（改后 ＝ 实测 `DistVisual ≤ 6`；改前 ＝ 旧索敌门（世界圆 `6×1.28`）∩ 旧判定门（`DistCells ≤ 6`）**取小**）：

| 方向 | **改前有效上限** | **改后有效上限** | 变化 |
|---|---|---|---|
| 网格轴向 `gx`／`gy` | **8 格**（连续量 8.485） | **6 格**（边界含实测 5.999999） | **−25%（连续量 −29.3%）** |
| 网格对角 `(1,1)`（＝屏幕纵向） | 6 格 | 6.708 格（1.118R） | **+11.8%** |
| 网格反对角 `(1,−1)`（＝屏幕横向） | 6 格 | 3.354 格（0.559R） | **−44.1%** |

⇒ ⭐ **远程整体变弱（沿轴 −25%）、屏幕纵向略增** —— ⛔ **不声称「无影响」**；且接线后**隔山/隔建筑/隔工事不再索敌** ⇒ 实际有效交战距离**进一步收窄**（墙后目标不可选）。⚠️ 另：件 6 使远程**首次能命中建筑**（见 §六-4）⇒ 对建筑向的攻击效率由 0 → 正常，属**新增能力**。

### 判据 17 · `GridMath` 双源（`M1`）

处置 ＝ **改读配置 ＋ 漂移哨兵**：`GridSystem.Awake` ⇒ `GridMath.Bind(config.cellSize)`（`GridSystem.cs:52`）。读数：Edit Mode `Bound=False`（回退编译期默认 `1.28/0.64`）／Play 内 **`Bound=True`**（`CellStep=0.715542`）✅。**调用面代价 ≈ 0**（`CellW/CellH` 全库零外部引用，仅本类内部消费）。

### 判据 18 · `subRange` 收口（含超集证明）

```
[J18] SubWindowForVisualRadius(0.25,4)=2（收口前 ⌈0.25×4⌉=1）
[J18] 其他半径对照：0.5→3 | 1.0→5 | 2.0→10 | CellWindow(0.25)=1
[J18·暴力核] 3600 方向扫描（R_world=0.178885）max|Δgx|=max|Δgy|=1.249964 · 所需最大子格索引差=2 · n=2 · 超窗方向数=0
```

⇒ 闭式 `n = ⌈R_vis × subDiv × (cellW²+cellH²)/(2·cellW·cellH)⌉` ＝ 现配置 `⌈5·R_vis⌉`；**全角度暴力核零超窗** ⇒ 超集成立 ✅；三份拷贝已收口为**单一口**（`ProjectileManager:358`／`DamageSystem:497`／`GroundEffectManager:203`）。

---

## §三 · 三形状成本对照（判据 11 · `D846` §10.2 ④）

场景：seed `20320` 正门考跑 · 真暂停 · 全库单位 **36**、建筑 **21**、候选（射程内敌）**1**、路径微格数 **L≈24**（＝R6×subDiv4）。测量点＝射手→靶直线、**通视**情形（带测法最坏：需全表单遍）。

| 形状 | 单次实测 | GC0Δ（500 次） | 分配特征（静态） |
|---|---|---|---|
| **① 带测法 · 生产 `HasLineOfSight` 全段**（地形/建筑逐微格 ＋ 工事带测） | **1.90 µs** | **0** | 零 `List`／零迭代器／零装箱（`GetUnitsEnumerator` 结构枚举器） |
| ①b 仅工事段（唯一口 `FindFortificationBlocker`） | **1.20 µs** | **0** | 同上（`O(N)` 单遍 ＋ 早退） |
| **② 逐微格 `GetUnitsInSubCell`**（对照复刻 · 只测量） | **56.9 µs** | 0 | **每微格 1 次全表枚举 ＋ `new List`**（24 分配/次） |
| **③ 增量索引式**（一次收集工事子格 ⇒ 逐微格 `O(1)` 查） | **0.64 µs** | — | 收集一次 **0.851 ms**（N=36 · 集合 1 条）＋ 逐微格 `HashSet.Contains` |

- **比值**：②/① ≈ **30×**（① 已含地形/建筑段；仅工事段 ①b 亦 ②/①b ≈ 47×）。
- ⭐ **结论（形状选择）**：**采带测法**（`D846` §10.2 倾向 · 与 `CheckWallBlock` 同构 · 零新基建 · 零堆分配）—— 实测支持，⛔ **未采**增量索引（新引入漂移面，且收益 1.20→0.64 µs 不具决定性）。
- ⚠️ **读数口径声明**：`GC.CollectionCount(0)` 只反映**回收次数**，⛔ 不能证明「零分配」；「零堆分配」的正面证据 ＝ ① **静态**（唯一口内无 `List`/`new`/迭代器/LINQ，且已把装箱的 `GetAllUnits()` 换成结构枚举器）＋ ② 形状②对照（其分配来源 `new List` 系已知）。⚠️ `GC.GetAllocatedBytesForCurrentThread` 本轮读数恒 0（该口径在本环境**不可判**），故未用作证据。

---

## §四 · 视线形状实现要点（供验收复核）

```130:135:Valley Rampart/Assets/_Game/Systems/Combat/CombatRules.cs
    public static bool HasLineOfSight(Vector2 from, Vector2 to, IGridOccupant ignoreOccupant = null)
    {
        var g = GridSystem.Instance;
        if (g == null || g.Config == null) return true;
        var a = g.WorldToSubCoord(from);
        var b = g.WorldToSubCoord(to);
        if (!a.HasValue || !b.HasValue) return true;   // 越界保守放行（原作语义）
        if (SubLineBlockedByCell(g, a.Value, b.Value, ignoreOccupant)) return false;
        // ③ 工事单位：**与弹道链同源**（同一函数 · 同带半宽）
        return FindFortificationBlocker(from, to, GridMath.PathBandHalf, applyArcHeight: false, arcHeightCells: 0f) == null;
    }
```

- 起终点微格豁免；`ignoreOccupant` ＝ 终点实体自身占格豁免（多格建筑当靶时防「打不到自己」假阴性）。
- **同源铁律落地**：`ProjectileManager.CheckWallBlock` 与 `HasLineOfSight` **共用 `FindFortificationBlocker`**（唯一函数 · 同带半宽 `PathBandHalf`），弧高豁免仅弹道链启用（`applyArcHeight=true`）。

---

## §五 · 机械扫描输出（本节为原样输出）

**(a) 判据 1（R5）** —— 见 §二·判据 1 代码块。
**(b) 判据 3（DistCells 域）**

```
Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs:332/1088/1137/1238   ← 派工域（不动）
Assets/_Game/Systems/Building/Building.cs:1635                              ← 资源域（仓库/井定位）
Assets/Editor/Smoke/Valley_HH115_Smoke_HexPrep.cs:194                       ← Editor 探针
```

**(c) 判据 14（R4）** —— 见 §二·判据 14。
**(d) 判据 15（AI.Core 零触）**：`git diff --stat -- Assets/_Game/Systems/AI.Core` ⇒ 空。
**(e) 本批 diff stat**：`12 files changed, 449 insertions(+), 127 deletions(-)`。

---

## §六 · 构造法声明 · 披露 · 偏离（⭐ `L-51` 纪律）

1. **进局面探针 ＝ 构造法**（⛔ ≠ 生产路径）：靶/工事单位由 `UnitFactory.SpawnUnit(Faction, Occupation, pos, kingdomId)` 生成（**生产生成口** ✓）；工事身份经**公开字段 `UnitController.fortification` 赋值**（生产路径可达性：`Resources/UnitData/{Wall,Gate,ArrowTower,CrossbowTower,MagicTower}.asset:50` 引 `Fortifications/*.asset` ⇒ `UnitController.cs:409-413` 从职业配置拷入**同字段** ⇒ **可达**）；山壁经正门 `MapGate.SetFeature`；`Locked` 位与弹型经**反射构造**（生产 `Locked` 写点当前为 0 处 ⇒ 无正门可走，如实声明）；全段 **`Time.timeScale=0` 真暂停**（防 15× 下世界漂移）。
2. **探针两次自纠（如实记录）**：
   - ① 首版**未做靶身份判定** ⇒ 读到他国单位导致 J4/J5 假「True」⇒ 已改**身份判定**（`ReferenceEquals(found, target)`）＋ **试区隔离**（射程＋4 格内无敌）。
   - ② 首版用 `CoordToWorld`（**格点＝菱形下顶点**）取点 ⇒ 顶点落在**子格边界**上，浮点 `floor` 翻行（`subB` 落 y=63 而非 64）⇒ 路径与屏蔽格错位 ⇒ 改 **`CellCenterWorld`（格中心）** 后全部转正。⚠️ 这属**探针构造缺陷**，⛔ 非生产缺陷；但**登记为一条知悉项**：单位恰好站在格点上时子格归属可差 1 格（浮点 1e-7 级），生产实况罕有精确站位。
3. **清单外改动（4 项 · 均已披露）**：
   - ⭐ **`UnitRegistry.GetUnitsEnumerator()`**（`UnitRegistry.cs:57`）：`P2` 硬约束①「零堆分配」**必须**它 —— 实读 `GetAllUnits():47-50` 返回 **`IEnumerable<UnitController>`**（内部为 **`HashSet`**）⇒ 接口 `foreach` 会**装箱结构枚举器**（**每次 1 次堆分配**）。⇒ ⚠️ **勘正 `D846` §10.2 所记「返回内部 `List` 引用 ⇒ 零分配」**：实为 `HashSet` ＋ 接口装箱；本批以**同集合的结构枚举器**口修掉（`CombatRules.cs:190`）。
   - ⭐ **`BuildingComponents` 炮塔扫描改单遍**（`:117-120`）：原「逐格 `GetUnitsInCell`」＝ `O(N)/格`（`D485` 已判「不可接受」的同族残留）；R5 窗口修正后格数由 121 ⇒ 225（`⌈1.25R⌉` 超集）会**进一步放大**该代价 ⇒ 改 **`FillUnitsInRect`（单遍 `O(N)` · 复用成员缓冲 ⇒ 稳态零分配）**。⚠️ 旧码耗时**未实测**（已改走），本节只给**码面计数**：旧 ≈ `(2⌈R⌉+1)²` 次全表枚举（R=5 ⇒ **121 次**）、新 ＝ **1 次**。
   - ⭐ **`NPCBrain.cs:1707-1708`**（冲锋击飞反向 `Random.value`）：**扫描发现**的伤害链第三处 `R4` 违规，清单未列，一并改 `RollChance`。
   - ⭐ **`MonsterController.FindNearestHuman` 入参语义**（`:85-93`）：为与 `MonsterAI` 的视觉格调用对齐而改（该函数原收**世界半径**，其旧调用点自行 `× CellSize()`）⇒ 顺带清掉一处标量换算。
4. ⚠️ **件 6 的行为新增（须策划端知悉）**：改前 `OnProjectileArrived` **只收单位候选**（`QueryNearbyUnits`）⇒ **远程对建筑恒 miss**（远程打建筑**零伤害**）。件 6 接线后**首次**具备「落点落在建筑菱形底座 ⇒ 命中」能力。本批按「**单位命中优先 · 无单位命中 ⇒ 建筑底座命中**」实现（既有语义零回归）。
5. ⚠️ **本端自决的口径细节（供复核）**：高抛豁免按**职业快照的默认弹型** `ballisticType` 判定（`UnitController.cs:1136`）；若单位实际选弹（`SelectAmmo`）切到非默认弹型，则以默认弹型为准 —— ⛔ 若要求「按实际弹型」，须另裁（涉及在索敌期预读选弹结果）。
6. ⚠️ **城门开态照挡**（`P3` 已裁）：`Gate.passable=1` 与 `FortificationPassableOverride`（昼夜开关）**均不参与视线**；与 `CheckWallBlock` 同口径。
7. ⚠️ **已知不对称（`D844` §9.1-3 已接受）**：废墟（`EnterRuined` 保留占格）挡视线、不挡弹道；非工事建筑占格挡视线、不挡弹道（弹道只由工事实体挡）⇒ 本批 ⛔ 未改，按裁定登记为**已知并接受**。
8. ⛔ **同族待治（只登记不施工 · 本批实测清单）**：`UnitController` 感知/编队面（`:1072/1164/1206/1241`）｜`VisionSystem.cs:28`（`world → 格` 换算）｜`Building.HasNearbyEnemy:388`（`rangeWorld / cellSize` 标量 + 逐格扫描）｜`MonsterController.cs:151/213`（移速换算 `CellSize()`）｜派工域 `TaskScheduler`（`DistCells` 不动）｜`ProjectileManager.QueryNearbyUnits`（保留逐微格 `GetUnitsInSubCell`，命中路径，本批未改）。

---

## §七 · 编译 · 收尾三态 · 产物

| 项 | 读数 |
|---|---|
| 编译 | `unity_editor refresh` ⇒ **errors 0**（warnings 全为存量） |
| 收尾 | 探针 `TestHarnessApi.ExitTestRun()` ✓ ⇒ 执行端 `unity_editor stop` ⇒ `playMode=stopped`（⛔ 无 1x 余留世界 · `L-32`） |
| 产物 | `Logs/hh320_math_20260923_152153.txt`（3119 B）／`Logs/hh320_live_20260923_160031.txt`（3131 B · 末行 `# 封存 16:00:31`） |
| 探针容器 | `Assets/Editor/Smoke/Valley_HH320_DamageProbe.cs`（＋其 `.meta` · **零生产码**） |

---

## §八 · 请裁 / 待确认

| # | 事项 | 本端处置 |
|---|---|---|
| 1 | **`BuildingComponents` 扫描改单遍**（清单外） | 已改并披露（§六-3）；若策划端要求「只做换算、不动扫描」，可回退（代价：`O(N)/格 × 225`，本端不建议） |
| 2 | **`NPCBrain:1706` 第三处 `R4`** | 已改（扫描发现）；若判「非伤害链」可回退（⛔ 但判据 14 的「伤害链零命中」口径将留一条） |
| 3 | **`GetUnitsEnumerator()`** | 已加（`P2` 零分配硬约束所必需）；⚠️ 同时勘正 `D846` §10.2 关于 `GetAllUnits` 的描述 |
| 4 | 高抛豁免按**默认弹型**判定（§六-5） | 本端自决；如需「按实际选弹」请裁 |
| 5 | `MonsterController.FindNearestHuman` 入参语义（§六-3） | 已改；调用点仅 `MonsterAI` 3 处（同批已同步） |

---

## §九 · 本轮红线遵守复核

- ⛔ `AI.Core` 零触（diff 空）／⛔ `GridMath.DistCells` 语义未动（寻路/派工域调用面不变）／⛔ `M3` 三项与 `DZ-4` 未碰／⛔ `range`/`attackRange` **数值零改**（`git diff` 仅换算式）／⛔ 训练仓零碰。
- ⛔ 未碰既有脏点（`GameScene.unity`／`Packages`／`TaskScheduler.cs`／美术 8 图／pixel-forge）：本批**不含**上述文件名的写入（`TaskScheduler.cs` 的工作区改动为**开片前既有**，本批未动它 —— `git diff` 可核）。
- **未 push**；具名 `git add`（见提交）。
