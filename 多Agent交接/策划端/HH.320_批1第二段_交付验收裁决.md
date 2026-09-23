# `HH.320` 批 1 · **第二段施工** 交付验收裁决（`D847` · 2026-09-23）

- **被验**：`e19896a8`（第二段施工）｜**基线** `4eaf3a58`（`D846` 裁定）｜**验收端**：策划端（砚）
- **交付面（本端独立复算）**：`git show --numstat e19896a8` ⇒ **14 文件** ＝ 生产 `12`（`+449/−127`）＋ 探针 `2`（`Valley_HH320_DamageProbe.cs` `+644` ＋ `.meta`）＋ 报告 `1`（`+371`）；`git log --oneline 4eaf3a58..HEAD` ⇒ **单次提交** ✓；`git diff --stat 4eaf3a58..e19896a8 -- Assets/_Game/Systems/AI.Core` ⇒ **空**（零触 ✓）；未 push ✓。

## 一、判据复核（18 条 · 本端逐项独立复算，⛔ 未采信转述）

| # | 判据 | 本端复算 | 裁 |
|---|---|---|---|
| 1 | `R5` 清零 | 本端重跑 `git grep` ⇒ 残留 **6 行**（`MonsterController:151/213`、`UnitController:1072/1103/1164/1206/1241`）与报告**逐条一致**；**伤害链射击路径零命中** ✓；`aoeWorld:1072` 本端直读 ＝ `IsHighValueTarget`（**目标价值面**）⇒ 报告分类正确 | ✅ |
| 2 | 射程两向对照 | 产物原文核：`gx/gy` 逐值相同 ✓；`k=6 → 5.999999 可打` ／ `k=7 → 6.999999 不可` ✓；生产路径 `+4 True / +7 False / 复位 True` ＋ 边界比 `0.9999998` ✓ | ✅ |
| 3 | 寻路/派工零回归 | 本端重跑：`TaskScheduler:332/1088/1137/1238`（派工）＋ `Building.cs:1635`（资源）⇒ 域内不变 ✓；**伤害链 −6** ✓ | ✅（⚠️ 微瑕见 §三-3） |
| 4 | 隔山壁 | 产物给出**逐微格命中明细**（`168..171(c=42,16)Mountain:w0`）＋ 正门 `SetFeature(Mountain)=True / (Plain)=True` ＋ 拆后恢复 ✓ **鉴别力自证** | ✅ |
| 5 | 隔建筑占格 | `BuildingBlocked=True ⇒ False` ＋ **清理后对照 True** ✓ | ✅ |
| 6 | 隔工事单位 | **两例**：`Wall(blocksMovement=1,h=2)` 与 `ArrowTower(blocksMovement=0,h=3)` **均 False** ✓ ⇒ `P3`「全含」实测 | ✅ |
| 7 | 隔河照常 | `River ⇒ True · flagWater=True` ✓ 反例可证 | ✅ |
| 8 | 隔 `Locked` 照常 | `True · flagLocked=True · 该格可走=False` ⇒ ⭐ **构造有效**（证「位已置但仍不挡」）✓ | ✅ |
| 9 | 高抛豁免 | **三向**：`Lob=False / HighArc=True / 还原 Lob=False` ✓ | ✅ |
| 10 | 近战不查 | 同一几何 `isRanged=false → True` ／ `true → False` ✓ | ✅ |
| 11 | 三形状成本 | `①带测法 1.90 µs` ／ `②逐微格 56.9 µs`（30×）／ `③索引式 0.64 µs` **但收集一次 0.851 ms** ⇒ ⭐ **采带测法正当** ✓；⭐ **口径声明诚实**（`GC.CollectionCount` ≠ 零分配 ⇒ 未用作证据）—— 判定方法学 ✅ **嘉许** | ✅ |
| 12 | 菱形底座 | ⛔ **只达数学面**（`InDiamondBase` 三例：中心/Δg=(1.4,0)/Δg=(1.6,0)/对角 (1.4,1.4) ✓ 且对角例证明旧口径会误判 ⇒ 鉴别力好）—— ⛔ **无生产链联测** | ⛔ **见 §二** |
| 13 | 散布＋确定性 | 同 seed `len=1517 hash=9CC5A50A` **两次一致** ＋ `distinct=64/64` ✓；异 seed 不同 ✓；击退角 `777×2 一致 / 778 不同` ✓；⭐ **本端直证种源** ＝ `WorldSeed()`（`map.seed` → `WorldManager.MapSeed` → `1`，`CombatRules.cs:38-45`）⇒ 「种源＝世界种子」**成立** ✓ | ✅ |
| 14 | `R4` 清零 | 本端重跑 `Systems/Combat` ⇒ **8 条全为注释** ✓（`CombatRules:13/27/52/63/68/69`／`DamageSystem:629`／`ProjectileManager:98`） | ✅ |
| 15 | 编译/`AI.Core` | 报告 `errors 0`；`AI.Core` diff **空**（本端复核）✓ | ✅ |
| 16 | 平衡对照 | 弓箭手轴向 **8 格 → 6 格**（−25%／连续 −29.3%）✓ 本端复算一致；⛔ 但**未含感知域** ⇒ 见 §二 | ⚠️ |
| 17 | `M1` 双源 | 本端重跑 `GridMath.CellW/CellH` 引用 ⇒ **生产码零外部引用**（仅新探针引用）✓；`Bind` 于 `GridSystem.Awake:52` 调用 ✓；`Bound: 数学面 False / Play True` ✓ | ✅ |
| 18 | `subRange` 收口 | 本端复算闭式 `k=(cw²+ch²)/(2·cw·ch)=1.25` ⇒ `⌈1.25·R·subDiv⌉` ⇒ `R=0.25,div=4 ⇒ 2` ✓；`0.5→3 / 1.0→5 / 2.0→10` ✓；暴力核 `max|Δg|=1.249964` ⇒ `n=2` 超窗 **0** ✓；三份拷贝收口为单一口 ✓ | ✅ |

**实现面直读（唯一口/铁律）**：`CombatRules.HasLineOfSight:130`（内联 Bresenham · ⛔ 无迭代器/无 `List`）✓｜`FindFortificationBlocker:177-209`（**两链共用** · `GetUnitsEnumerator()` 零分配 · `applyArcHeight` 区分两链 · **只选阻挡者不结算伤害** ⇒ 语义零丢）✓｜`IsSightBlockedSub:163-169`（① 山壁 ② `BuildingBlocked` 位测 ＋ `ignore` 豁免；⛔ 不读 `Water/Locked/Bridge`）✓｜`GridSystem.IsSightBlockingFeature:141`（**显式枚举**）＋ `VerifySightBlockingFeatureSet:149`（**9 值全枚举对拍 ＋ LogError** · `#if UNITY_EDITOR`）✓｜`InDiamondBase:222-229`（格坐标轴对齐矩形 ⟺ 世界菱形 · 边界含）✓｜`FillUnitsInRect:577-583`（**单遍 `_unitSubCells` ＋ `RectInt.Contains`** · buffer 复用零分配）✓｜`FindBuildingAtLanding:324-344`（3×3 `BuildingRegistry.GetAt` ＋ 阵营过滤 ＋ 底座复核 · `O(1)×9`）✓

## 二、⛔ 不判绿（两条 · 均为实质项）

### 2-1 ★ **`件 6` 未达成 ⇒ 返工**（`OnProjectileArrived` 早退挡在建筑兜底之前）

- **码面实读**（`ProjectileManager.cs` 最终行号）：`:209` `candidates = QueryNearbyUnits(...)` ⇒ **`:211 if (candidates.Count == 0) return;   // miss`** ⇒ **`:245-246 if (bestTarget == null) bestTarget = FindBuildingAtLanding(...)`**。
- ⛔ **后果**：`QueryNearbyUnits` 只收 **`UnitController`**（`_unitSubCells`，`GridSystem.cs:347-353`）⇒ **建筑不在候选**（建筑占格走 `_occupants`/`WalkFlags`，另一套）⇒ ⭐ **落点附近一个单位都没有时**（＝ 对**非工事建筑**射箭的典型情形）**直接 `return` ⇒ 建筑兜底根本走不到 ⇒ 建筑仍 `miss`（零伤害）**。
- ⇒ **件 6 口径「落点落在菱形底座内 ⇒ 命中」在生产链上未达成**（仅在「落点附近恰有单位（且被过滤掉）」时生效）。
- ⛔ **且判据 12 只给了数学面三例**（`InDiamondBase` 纯函数读数），**无生产链联测** ⇒ 报告两处断言（§0-3②「**远程首次能命中建筑**」、§六-4「**顺带闭合的真实缺陷**」）属 ⛔ **能力类断言 × 载体为码面推断/数学面读数** ⇒ **不成立**（处置见 §四）。
- ⚠️ **注**：「**改前远程对建筑恒 miss**」这一**既有缺陷判断本身是成立的**（本端直读基线 `4eaf3a58` 版：`:207` 只调 `QueryNearbyUnits` ⇒ **`:209 if (candidates.Count == 0) return;`** ⇒ 建筑从不进候选）✓ ⇒ **缺陷真实**，只是**本批未修透**。

### 2-2 ⚠️ **`件 2` 第 13 处（清单外改动）· 平衡影响未评估**

- 落点：`MonsterController.FindNearestHuman` 入参语义（`rangeWorld` → `rangeVisual`）。
- **本端对照直读**：改前 `MonsterAI.cs:85/115/128` ⇒ `prof.range * CellSize()` ／ `_mc.VisionRadiusCells * CellSize()`（`CellSize()` ＝ `cellSize.x` ＝ **1.28**）；改后 ⇒ `prof.range` ／ `VisionRadiusCells` ⇒ 内部 `GridMath.VisualToWorld` ＝ **×0.7155** ⇒ ⭐ **怪物感知/索敌世界半径沿格轴 1.28 → 0.7155 ⇒ −44.1%**。
- ⛔ **判据 16 只给了弓箭手射程** ⇒ 该项**未入平衡对照** ⇒ 违反「行为变更须给前/后对照」的硬要求（`D845` §平衡对照）。
- ⭐ **改动方向本身正确**（清掉一处标量换算，属 `R5` 精神）；⛔ 但**影响面未评估即施工**。
- ⇒ 处置：**保留改动** ＋ **补对照读数** ＋ ⚠️ **「是否重标 `VisionRadiusCells`／`prof.range` 数值」列为待用户拍**（属玩法口径，代价见 §五-1）。

## 三、微瑕（不阻断 · 但须记）

1. **`CombatRules.cs:175` 注释与本函数实现自相矛盾**（⛔ 须勘正）：`:175` 写「单遍 `UnitRegistry.GetAllUnits()`（**返回内部 List 引用 ⇒ 零分配**）」，而 **`:188-190` 同函数内**写「⚠️ 零分配：⛔ **不得走** `GetAllUnits()`（`IEnumerable` ⇒ 结构枚举器**装箱**）」⇒ 同一函数两句对立 ⇒ 按 `L-63` **必须改写正文**。
   - ⚠️ **类型名错源头（本端追证）**：`UnitRegistry._aliveUnits` **自 `020035b1`（阶段 1）起即为 `HashSet<UnitController>`**（`git log -S` ⇒ 「曾为 `List<UnitController>`」**0 条**）⇒ ⭐ 项目内「**返回内部 List 引用**」的表述（`PopulationSystem:356/514`、`SatietySystem:141`、`HH.77` 报告等）**类型名从一开始就错**（⚠️ 其**危害描述**（遍历中 Spawn ⇒ 枚举失效）**仍成立** ⇒ 非缺陷，属**文本面陈旧**）⇒ ⛔ **不在本批范围**，登记为**文本面批量待勘正**。
2. **清单外改动 4 项已全部披露** ✓（含 `GetUnitsEnumerator`／炮塔扫描改单遍／`NPCBrain:1707` 第三处 `R4`／`MonsterController` 入参语义）⇒ ⭐ 披露完整，`L-51` 纪律**遵守**。
3. **判据 3 机械扫描漏列本批自建探针**：报告 §五(b) 列 `Valley_HH115_Smoke_HexPrep.cs:194` 并称「**1 处** Editor 探针」，实读 `git grep GridMath.DistCells` ⇒ **2 处**（另有 `Valley_HH320_DamageProbe.cs:142`，本批自建）⇒ 汇总数字**漏项**（域内结论不变 · 均为 Editor 面）。
4. **产物清单未含中间档**：报告列 2 档（`hh320_math_20260923_152153.txt` 3119 B ✓ ／ `hh320_live_20260923_160031.txt` 3131 B ✓ **本端实读大小与封存行均吻合**）；⚠️ 实存另有 4 个中间档（`hh320_live_20260923_153808/154637/155311/155653.txt`）⇒ §六-2 已如实说明两次自纠 ⇒ **可接受**，登记备查。
5. ⚠️ **产物在 `Logs/` 未入版本控制**（`e19896a8` 未含 Logs）⇒ 与历批一致（既有约定），非本批缺陷。

## 四、请裁 5 条 —— 逐条裁定

| # | 事项 | 裁定 |
|---|---|---|
| 1 | `BuildingComponents` 扫描改单遍（清单外） | ✅ **接受**（`FillUnitsInRect` 本端已直读：单遍 `O(N)`＋零分配缓冲；旧路 `O(N)/格 × 121~225` 属 `D485` 已判「不可接受」同族；窗口由**非超集**改**超集**方向正确） |
| 2 | `NPCBrain:1707` 第三处 `R4`（清单外） | ✅ **接受**（方向正确）。⚠️ **登记知悉项**：第三处并入**同一随机流** ⇒ ⭐ **消费顺序耦合**（新增/移除消费者会平移后续散布与击退序列）⇒ 同 seed 仍逐字一致 ⇒ ⛔ 不破 `R4`；⚠️ 将来若需与 `sim` **逐字对齐历史跑局**须注意（本批 sim 零义务 · 用户已裁） |
| 3 | `GetUnitsEnumerator()`（清单外） | ✅ **接受**（`P2` 硬约束①**所必需**；⛔ 并**勘正 `D846` §10.2 本端之误**，见 §四附） |
| 4 | 高抛豁免按**默认弹型**判定 | ✅ **接受**（⚠️ 但**真分裂点不在执行端所指处** ⇒ 立 `O-24`，见下） |
| 5 | `MonsterController.FindNearestHuman` 入参语义 | ⚠️ **接受改动 ＋ ⛔ 须补平衡读数**（见 §二-2）；⭐ 调用面本端复核：`FindNearestHuman` 全库仅 `MonsterAI:85/115/128` 3 处调用 ⇒ **同步完整** ✓ |

**§四附 · 本端认账（`D846` §10.2 表述错误 ⇒ 第二次同处失误）**：
- 我写「`GetAllUnits():47-50` **返回内部 `List` 引用 ⇒ 零分配**」—— ⛔ **双错**：① `:11` 声明为 **`HashSet<UnitController>`**（⛔ 未读声明行）② **即便为 `List`**，`GetAllUnits()` 返回类型是 `IEnumerable<UnitController>` ⇒ `foreach` 走接口 ⇒ **结构枚举器装箱 ⇒ 每次 1 次堆分配**（`List<T>.Enumerator` 同为 struct）。
- ⇒ 执行端 `GetUnitsEnumerator()`（`:57`，交出 `HashSet<T>.Enumerator` 结构枚举器）**是使 `P2` ① 可满足的必要改动** ⇒ 判**正当**，非 scope creep。
- ⭐ **根因**：本端 `L-86` ② 只写「**字段名**一律读声明行」⇒ ⚠️ **覆盖面不足**（未覆盖「**被引成员的声明/实现**」）⇒ **扩条**（见 §六）。

**`O-24` 新立（`ballisticType` 双赋值源 · 潜在不对称）**：**本端增量取证** —— `UnitController.BuildStaticProfile:869` ⇒ `ballisticType = p.ballisticType`（**职业快照的默认弹型**），而 `NPCBrain.cs:1314` ⇒ `ballisticType = ammo.ballisticType`（**`SelectAmmo` 选中弹的弹型**）⇒ ⭐ **同一条弹道属性、两条赋值源** ⇒ 视线豁免用「快照默认弹型」、`NPCBrain` 路径下的实际弹道用「选中弹型」⇒ ⚠️ **潜在分裂**。
- ⭐ **当前不可达（本端核过）**：`BallisticType{Straight=0, Lob=1, HighArc=2}`；`SelectAmmo`（`UnitController.cs:917-937`）仅在 `ammoMax>0`（战争机器）时切弹，且可选集 **恒 ∈ {Fireball, Magic, Stone}** ⇒ `AmmoDef` 三者 **ballisticType 均 ＝ 2（HighArc）** ⇒ **同型**；跨型组合仅见于 `Musket`(Straight=0) 而火枪非战争机器（`ammoMax=0` ⇒ `SelectAmmo` 直返默认）。
- ⇒ ⚠️ **登记挂账**：⛔ 不阻断本批；⭐ **新增弹种/新职业默认弹时须复核**（一旦出现「战争机器默认弹 ≠ 可选弹弹型」即**可达**）。

## 五、⚠️ 待用户拍（1 条 · 玩法口径）

**`VisionRadiusCells`（及怪物 `prof.range`）是否重标？** 件 2 统一口径后，怪物**感知/索敌世界半径沿格轴 −44.1%**（1.28→0.7155 系数）。

- **读数（本端已核）**：改前世界半径 ＝ `N × 1.28`；改后 ＝ `N × 0.7155`；沿格轴有效格数由 `1.789N` 收至 `N`。
- ⭐ **本端建议**：**先不重标，记为已知平衡影响**（理由：单人赛期规模，数值重标属玩法调参 ⇒ 等实机手感验证；且重标会牵动 `MonsterDef`/职业资产两处）。
- ⛔ 但若你要求「怪物观感须与改前相当」⇒ 重标系数 ＝ `×1.789`（＝ `1.28/0.7155`），我另签小批（**属数值批，非本批口径批**）。

## 六、教训落账

- ⭐ **新立 `L-88`** —— **能力类断言**（「**首次能**／**已修复**／**已闭合**」）的载体必须是**生产链实测读数**（⛔ 数学面读数、码面推断**不算**）。
  - 复发链：`L-70` 补条（"结论字样须与人工结论一致"）→ 补条②（`⇒ 判绿`）→ 补条③（`已移至…`，已裁**拆能力**）⇒ **本批第 4 次换载体复发**（⚠️ 前三次载体是**落盘串**，本次是**报告**）⇒ 按 `L-79` 补条⑤「加纪律治不住 ⇒ 拆能力」：**拆法 ＝ 判据表里把「能力断言」与「读数」分列**，能力断言的载体列**只接受**「生产链实测」类读数（数学面/纯函数/静态扫描一律不得填入该列）。
- ⭐ **扩 `L-86` ②** —— 「字段名一律读声明行」⇒ **「凡引用某成员的『类型／行为／成本』，须读其声明行与实现体」**（本批实例：未读 `UnitRegistry.cs:11` 的 `HashSet` 声明 ＋ 未推 `IEnumerable` 接口 foreach 的装箱后果）。
- ⚠️ **本端同类失误第二次出现在同一处（`S2` 成本判断）** ⇒ 按红线纪律完成复盘：根因＝**"读到了返回值，没读到类型声明"**（与上批「凭结构直觉」不同一因，但同一处 ⇒ 该处结论此后**一律以实测读数为准**，不再写"零分配"类推断）。

## 七、⛔ 本批结论

**不判绿（部分达成）**：判据 **17/18 达成**（本端独立复算全吻合）；⭐ **判据 12 只达数学面 ＋ 生产链早退阻塞 ⇒ `件 6` 返工**；⚠️ **`件 2` 第 13 处补平衡读数**。**`M1-G`／批 1 不销号**；批 2（`M3` 本体 ＋ `DZ-4`）⛔ 继续顺延（`L-79`：闸门项不得被非闸门项顺延 ⇒ 批 2 仍等批 1 收口）。
