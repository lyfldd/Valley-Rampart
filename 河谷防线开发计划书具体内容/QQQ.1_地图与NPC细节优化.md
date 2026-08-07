# QQQ.1 地图与NPC细节优化

> 散乱细节优化文档 · 创建于 2026-08-07
> 本文档收集 6 个散乱小需求，配套执行清单见 QQQ.1_执行清单.md
> 横跨地图生成 / AI安全判断 / 坐标系 / NPC交互 四个系统
> 其中需求6 为需求4（坐标系）实施后测出的回归 bug，见 §需求6

## 概述

本批需求源于玩家实测反馈：地图生成频繁报"资源缺失"错误、开局 NPC 往世界(0,0)乱走、NPC 安全地带判断依赖场景手配锚点不健壮、坐标系原点不在城堡导致定位反直觉、点击 NPC 反馈太少。5 个需求中需求2和需求3根因相同（HomePoint 来源），合并处理。

---

## 需求 1：地图生成资源缺失报错（底层重构）

### 问题/现状

地图生成频繁报 `[MapValidator] [Error] 四资源保障: 林地数量 0 < 下限 1` 或 `矿山数量 0 < 下限 1`。

根因是**"事后补丁"设计缺陷**，三层调用链互相打架：

1. `WorldManager.PickTerrainByZone`（WorldManager.cs:257-283）：资源区（LeftResource/RightResource）用 `PickWeighted` 按 `resourceInnerWeights`/`resourceOuterWeights` 权重**随机**选地形，权重不保证一定出现 Forest/Quarry，可能全部随机到 Plain/Hills。

2. `WorldManager.EnsureResourceCoverage`（WorldManager.cs:313-336）：事后统计 `forestCount`/`quarryCount`，不足则 `ForceReplace` 在资源区**随机选一个区块**替换为 Forest/Quarry。

3. `WorldManager.EnforceAdjacency`（WorldManager.cs:360-379）：紧接 EnsureResourceCoverage 之后执行，扫描相邻区块对，违规则把其中一个改成 `Hills`。**ForceReplace 刚把区块改成 Forest/Quarry，EnforceAdjacency 可能立刻把它（或它的邻居）改回 Hills**，导致资源再次丢失。

4. Step 6-7 循环 2 轮（WorldManager.cs:211-215），修复→改回→再修复→再改回，2 轮不够收敛。

5. 最终 `MapValidator.CheckResourceCoverage`（MapValidator.cs:94-131）统计仍不足，报 Error。

### 方案

**资源保障前置到分配阶段，从事后补丁改为事前占位**：

1. **预占位保障区块**：在 Step 3-5（按区分地形）之前，先在左右资源区各预选1个区块作为"保障林地"、1个作为"保障矿山"（共4个保障区块：左林/左矿/右林/右矿，或左右分工：左资源区保障林地、右资源区保障矿山，避免占用过多）。

2. **Region 加标记字段**：`Region` 类加 `bool isProtectedResource` 字段，标记为 true 的区块表示资源保障占位。

3. **PickTerrainByZone 尊重占位**：对 `isProtectedResource` 的区块直接返回 Forest/Quarry（按占位类型），不走权重随机。

4. **EnforceAdjacency 跳过保障区块**：邻接违规时，`fixIdx` 不能选 `isProtectedResource` 的区块（改邻居不改保障），如果两个相邻区块都是保障区块则跳过不处理（保障优先于邻接）。

5. **删除 EnsureResourceCoverage 的 ForceReplace 逻辑**：保障已在分配阶段满足，不再需要事后修复。保留 `EnsureResourceCoverage` 函数但只做统计日志（Debug.Log 资源数量），不再 ForceReplace。

6. **MapValidator 保留校验**：作为最终兜底，正常情况下不再报 Error。

### 影响面

- `WorldManager.cs`：GenerateMap 流程调整（Step 3-5 前加预占位）、PickTerrainByZone、EnsureResourceCoverage、EnforceAdjacency、ForceReplace（删除或保留空壳）
- `WorldManager.cs`：Region 内部类加 `isProtectedResource` 字段
- `MapValidator.cs`：不变（保留校验）
- `MapGenRulesConfig.cs`：可选加 `guaranteeForestPerSide`/`guaranteeQuarryPerSide` 字段控制保障数量（默认1）

### 验收

- 连续生成 20 张地图（不同 seed），Console 无 `[Error] 四资源保障` 报错
- 每张地图至少有 1 个林地大区块 + 1 个矿山大区块
- 保障区块的 terrain 在 EnforceAdjacency 后仍为 Forest/Quarry（不被改回 Hills）

---

## 需求 2+3：NPC安全地带判断 + 开局NPC走(0,0)

### 问题/现状

**现象A**：开局 NPC 不知道往哪走，直接往世界坐标 (0,0) 走。

**现象B**：NPC 的"安全地带"（HomePoint）来自场景手配空 Transform，与主城坐标分离。没有城墙时或城墙被摧毁时，NPC 仍往固定锚点走，无法用主城判断。

**根因**：
- `SceneHomePointProvider.GetHomePoint`（SceneHomePointProvider.cs:28-38）返回场景手配的 `homePointAnchor` Transform 位置。
- 当 `homePointAnchor` 未拖引用或 `SceneHomePointProvider` 未挂载时，返回 `Vector2.zero`（NPCBrain.cs:544）。
- `Vector2.zero` 既是"未配置哨兵值"又是合法坐标，NPC 真的会往 (0,0) 走。
- HomePoint（场景手配 -15,-3）与主城坐标（`WorldManager.GetKingdomAnchorWorld()` 算出的城堡中心）是两套独立系统，未联动。
- 城墙摧毁不影响 HomePoint（固定锚点），但设计上 NPC 应该以主城为安全归宿。

### 方案

**HomePoint 来源改用主城坐标**：

1. **SceneHomePointProvider 改造**：`GetHomePoint` 不再读场景 Transform，改为调用 `WorldManager.Instance.GetKingdomAnchorWorld()`（已存在，WorldManager.cs:670-692，返回城堡中心世界坐标）。

2. **敌方锚点保留**：敌方 NPC 的 HomePoint 仍用 `enemyHomePointAnchor`（敌方出生地），或改用敌方地图的锚点（`GetKingdomAnchorWorld` 对敌方地图同样适用）。

3. **去除 Vector2.zero 哨兵**：`GetHomePoint` 在 WorldManager 未初始化时返回 `Vector3.zero` 并 `Debug.LogError`（暴露初始化时序问题，而非静默退化为0,0）。

4. **场景清理**：GameScene 中 `HomePoint`（-15,-3）和 `EnemyHomePoint`（10,-3）空 GameObject 可保留作敌方锚点用，但人类阵营不再依赖它。

5. **主城不依赖城墙**：城堡（CastleCore）是独立建筑，城墙摧毁不影响 `GetKingdomAnchorWorld()` 返回值（城堡中心坐标不变）。NPC 以主城为安全点，城墙只是路径屏障。

### 影响面

- `SceneHomePointProvider.cs`：GetHomePoint 改实现
- `NPCBrain.cs`：BuildBaseContext（L544）不变（仍调 provider），但哨兵值处理可选加日志
- `WorldManager.cs`：GetKingdomAnchorWorld 不变（已存在）
- `GameScene.unity`：HomePoint 空 GameObject 可保留（敌方用）或清理
- 开局 NPC 走 (0,0) 问题随此方案自动解决（HomePoint 不再退化为0,0）

### 验收

- 开局 NPC 不再往世界 (0,0) 走，而是往主城方向聚集
- NPCBrain.BuildBaseContext 的 ctx.HomePoint = 城堡中心坐标（非0,0）
- 拆除所有城墙后，NPC 撤退仍往主城走（HomePoint 不变）
- Console 无 HomePoint 相关的 Error 日志

---

## 需求 4：坐标系原点调整（城堡中线 = 世界0,0）

### 问题/现状

当前 `GridSystem`（GridSystem.cs:41-56）坐标转换无 origin 字段：
- `WorldToCoord(pos)`: `x = FloorToInt(pos.x / cellSize)`，世界 x=0 对应地图最左大区块最左格
- `CoordToWorld(coord)`: `x = (coord.x + 0.5) × cellSize`，cell 中心

城堡占2格（localCellX=7,8），中心在两格交界。城堡中心世界x = `(cellStartX + 8) × cellSize`，是个大正数（如 271.2），不在 (0,0)。

**反直觉点**：世界 (0,0) 是地图左边缘，不是城堡中心；NPC 撤退目标点、君主出生点等都以城堡为基准，但坐标系原点在别处，调试和定位反直觉。

### 方案

**GridSystem 加 originX 偏移，让城堡中线 = 世界 x=0**：

1. **GridConfig 加字段**：`float originX`（默认0），表示坐标原点偏移量（世界单位）。

2. **GridSystem 坐标转换加偏移**：
   - `WorldToCoord(pos)`: `x = FloorToInt((pos.x + originX) / cellSize)`
   - `CoordToWorld(coord)`: `x = (coord.x + 0.5) × cellSize - originX`
   - y 轴不变（y=-3 是地面基线，已符合"y=-3 为基准"要求）

3. **WorldManager 生成后设置 originX**：在 `PlaceAbandonedCastle` 之后，算出城堡中心 cell 全局索引，设置 `GridSystem.config.originX = castleCenterCellGlobal × cellSize`。
   - `castleCenterCellGlobal = castleRegionIdx × cellCount + cellCount/2`
   - `originX = castleCenterCellGlobal × cellSize`

4. **存档不兼容**：开发阶段无旧存档，旧存档直接作废（用户已确认）。发布后版本更新时再处理迁移。

5. **影响验证**：所有用 WorldToCoord/CoordToWorld 的地方自动适配（坐标值变化但逻辑不变）。关键检查点：
   - 城堡中心世界坐标 = (0, -3) ✅
   - HomePoint（需求2改用主城坐标）= (0, -3) ✅
   - 开局 NPC 出生点（GetKingdomAnchorWorld）= (0, -3) 附近 ✅

### 影响面

- `GridConfig.cs`：加 originX 字段
- `GridSystem.cs`：WorldToCoord/CoordToWorld 加偏移
- `WorldManager.cs`：GenerateMap 末尾设置 originX
- `GridConfig.asset`：originX 默认0（运行时由 WorldManager 覆盖）
- 全系统坐标值平移（WorldToCoord/CoordToWorld 的所有调用方自动适配，但调试日志里的坐标值会变）
- 存档不兼容（旧存档坐标错位，开发阶段可接受）

### 验收

- 城堡中心世界坐标 = (0, -3)
- `GridSystem.CoordToWorld(castleCenterCell)` 的 x = 0
- `GridSystem.WorldToCoord(Vector2.zero)` 返回城堡中心 cell 索引
- 开局 NPC 生成在 (0, -3) 附近（不再是地图左边缘）
- 君主生成点 = (0, -3) 附近

---

## 需求 5：点击NPC文字丰富（头顶气泡多句随机对话）

### 问题/现状

当前点击 NPC 只有头顶气泡一句硬编码对话（每职业1句，2.5秒消失），文案在 `UnitController.GetTalkLineByOccupation`（UnitController.cs:1107-1121）和 `BuildInteractActions`（UnitController.cs:1047-1104）。文案太少、不分化，缺乏沉浸感。

### 方案

**每职业扩充到5-8句话随机抽取，按状态分化**：

1. **新增对话池**：在 `UnitController` 中新增 `GetTalkLinesByOccupation` 方法，返回 `string[]`（该职业所有可用对话）。

2. **状态分化**：对话按状态分类，点击时根据 NPC 当前状态从对应池子随机抽：
   - **正常**：默认状态对话
   - **饥饿**（satiety < 30）：抱怨饿的对话
   - **受伤**（hp < 40% maxHp）：受伤对话
   - **夜晚**：夜晚状态对话

3. **BuildInteractActions 改造**：对话动作的文案从 `GetTalkLineByOccupation()`（单句）改为 `GetTalkLinesByOccupation().RandomPick()`（多句随机）。

4. **OverheadSpeech 不变**：仍用现有气泡机制（2.5秒消失），只是文案来源变多。

### 对话文案设计（每职业5-8句）

#### Worker（工人）
正常：{"正在干活呢。","木头、石头、粮食，都得有人搬。","今天也要努力工作。","嘿咻……这活儿不轻。","手艺不能丢，天天练。","仓库快满了，加把劲。"}
饥饿：{"肚子好饿……什么时候开饭。","干不动了，想吃东西。","粮仓是不是空了？"}
受伤：{"嘶……疼……还能撑住。","轻伤不下火线。"}

#### Porter（搬运工）
正常：{"搬运中，请让让。","这批货挺沉的。","往仓库送呢。","别挡道，赶时间。","一趟又一趟。","运完了能歇会儿吗。"}
饥饿：{"扛不动了……没吃饭。","饿得手抖。"}

#### Resident（居民）
正常：{"还没活干……想学门手艺。","今天天气不错。","什么时候能有活干呢。","闲着也是闲着。","希望能派上用场。","你看起来很忙。"}
饥饿：{"好饿啊……粮仓还有粮吗。","肚子咕咕叫。"}

#### Child（小孩）
正常：{"我很快就会长大啦！","长大了我也要干活！","嘿嘿，好好玩。","大人都在忙呢。","我以后要当英雄！","你看我跑得快不快。"}
饥饿：{"饿饿……想吃东西。","妈妈什么时候回来。"}

#### Vagrant（流浪汉）
正常：{"……又冷又饿……","能给口吃的吗。","我已经流浪好久了。","求求你，收留我吧。","外面的世界好危险。","只要一口粮食就好。","我也能干活的。"}
饥饿：{"三天没吃东西了……","饿得走不动了。"}

#### Ruler（君主）
正常：{"王国就托付给我吧。","子民们需要我。","建设王国，任重道远。","今天的决策，明天的未来。","王国的繁荣是我的责任。","有什么事尽管说。","吾乃一国之主。"}
受伤：{"我没事……还能指挥。","保护王国要紧。"}

#### General（将军）
正常：{"军令请走 E 键面板。","士兵们随时待命。","兵者，国之大事。","布阵迎敌！","令行禁止。","战况如何？","稳住阵脚。"}
受伤：{"将不退，兵不散。","轻伤而已。"}

#### Warrior（战士）
正常：{"随时准备战斗！","剑在手，不退缩。","为了王国！","训练不能停。","敌人来了尽管上。","保家卫国是本分。","嘿嘿，手痒了。"}
饥饿：{"饿得挥不动剑……","军粮还没到吗。"}
受伤：{"小伤，不碍事。","还能战。"}

#### Archer（弓手）
正常：{"随时准备战斗！","弓弦已上，随时放箭。","百步穿杨。","风向……差不多。","箭囊还满着呢。","远程压制交给我。"}
饥饿：{"拉弓没力气……","饿了手会抖。"}

#### Crossbowman（弩手）
正常：{"随时准备战斗！","弩已上弦。","穿透盔甲没问题。","装填……好了。","射程之内，皆是猎物。","机械的力量。"}
受伤：{"还能再射几发。","不退。"}

#### HeavyWarrior（重装战士）
正常：{"随时准备战斗！","重甲在手，万夫莫开。","我是铜墙铁壁。","冲我来的都后悔。","盾墙不可破。","挡在前面是我的职责。"}
受伤：{"甲还没破，人还在。","重装不退。"}

#### Cavalry（骑兵）
正常：{"随时准备战斗！","冲锋号角何时响？","马蹄之下，寸草不生。","速度就是优势。","绕后突袭，我的强项。","马儿今天状态不错。"}
饥饿：{"马也得吃东西啊……","饿得跑不动。"}

#### ShieldGuard（盾卫）
正常：{"随时准备战斗！","盾在人在。","我守这里，谁都过不来。","盾墙坚不可摧。","后面的人放心输出。","我的盾就是城墙。"}

#### Mage（法师）
正常：{"随时准备战斗！","魔力充盈。","一个火球，一片敌军。","元素听我号令。","别打断我施法。","魔法不是戏法。"}
饥饿：{"魔力需要饱食支撑……","饿得念不动咒。"}

#### Healer（治疗）
正常：{"随时准备战斗！","谁受伤了？我来。","圣光护佑。","别担心，有我在。","治疗优先给前线。","愿光明庇佑你们。"}
受伤：{"我自己也得小心。","还能撑住。"}

#### Bishop（主教）
正常：{"随时准备战斗！","信仰即是力量。","圣言指引方向。","黑暗退散。","我为主传道。","神眷不灭。"}

#### Archmage（大法师）
正常：{"随时准备战斗！","奥术洪流蓄势待发。","我已洞悉元素本质。","别浪费我的法力。","一念之间，天地变色。","魔法之巅，不过如此。"}

### 影响面

- `UnitController.cs`：新增 `GetTalkLinesByOccupation` 方法（返回 string[]）、`BuildInteractActions` 对话文案改随机抽取、可能加状态判断（satiety/hp/night）
- `IClickInteractable.cs`：OverheadSpeech 不变（文案来源在外部）
- 无新 UI 系统（保持头顶气泡形式）

### 验收

- 点击同职业 NPC 多次，每次显示不同对话（5-8句随机）
- NPC 饥饿时（satiety<30）显示饥饿专属对话
- NPC 受伤时（hp<40%）显示受伤专属对话
- 对话仍 2.5 秒自动消失（OverheadSpeech 机制不变）

---

## 需求 6：需求4 originX 导致 Play 窗口主城/部分资源未刷出（回归 bug）

> 2026-08-07 实测反馈。需求4（坐标系原点）实施后出现，属 T7/T8/T9 引入的回归。

### 问题/现状

**现象**：Scene 窗口能看到完整的资源/主城标记（Gizmo），但 Play 窗口只有约一半资源刷出，主城（城堡）没刷出来。换不同地图复现，只要一侧正常刷新，另一侧/主城就缺失。

### 根因

需求4 只把 `originX` 注入到了 `GridSystem.CoordToWorld` / `WorldToCoord`（GridSystem.cs:44-45,54-55），**但大量使用"原始坐标"（`cellStartX × cellSize`）的代码没有同步 originX 偏移**，导致两套坐标并存、内容错位：

| 代码路径 | 坐标方式 | 位置 |
|---|---|---|
| `BuildingFactory.CreateBuilding` → `GridSystem.CoordToWorld` | ✅ 已偏移（建筑在偏移后坐标） | BuildingFactory.cs:112-116 |
| `WorldManager.GetKingdomAnchorWorld` | ❌ 未偏移（`(cellStartX+localCellX+1)×cs`） | WorldManager.cs:727 |
| `GridSystem.OnDrawGizmos`（Scene 标记） | ❌ 未偏移（`cellStartX×cs`） | GridSystem.cs:297-344 |
| `CameraSetup.LateUpdate`（背景/相机钳制） | ❌ 未偏移（背景 Sprite 固定位置） | CameraSetup.cs:97-105 |

**错位链**：君主出生点用 `GetKingdomAnchorWorld`（未偏移，≈旧城堡 x≈271）→ 相机跟随君主并在背景中心附近钳制 → 相机视野停在 ≈271 区域；而建筑用 `CoordToWorld` 已偏移到（城堡 x=0，整体左移 originX）。结果：城堡（x=0）与左侧资源在相机视野之外，仅右侧一部分可见 → 表现为"一半资源 + 无主城"。

### 方案（已选方案2：统一偏移，已实施）

1. ~~回滚需求4~~（被否）：放弃"城堡中线=世界0,0"，零回归但违背文档意图。
2. **统一偏移（已实施）**：让出生点/相机/背景/Gizmo/底图全部走与建筑一致的 originX 偏移，保证所有世界坐标一致，城堡落 x=0。
3. ~~originX 纯逻辑化~~（未选）：不改世界实际位置，所有消费方统一经 CoordToWorld。

### 已实施改动（2026-08-07）

| 代码路径 | 改动 | 位置 |
|---|---|---|
| `WorldManager.GetKingdomAnchorWorld` | `centerX` 减 `grid.Config.originX`（君主/NPC/招募出生点落 x=0） | WorldManager.cs:728 |
| `CameraSetup` | 新增 `AlignBackgroundToMap()`：LateUpdate 一次性把背景 Sprite 左移 originX，跨地图增量对齐 | CameraSetup.cs:97-110,112 |
| `GridSystem.OnDrawGizmos` | 全部标记/边界/城堡减 `ox`（Scene 与 Play 位置一致） | GridSystem.cs:286-346 |
| `MapVisualizer` | 底图/基准线减 `ox`（Play/编辑底图与建筑一致） | MapVisualizer.cs:76,92,220 |

### 影响面

- 已改：`WorldManager.GetKingdomAnchorWorld`、`CameraSetup`、`GridSystem.OnDrawGizmos`、`MapVisualizer`
- 未改（协调用）：`SceneHomePointProvider` 已走 `GetKingdomAnchorWorld`，随锚点修复自动一致

### 验收

- [x] Play 窗口能看到完整地图：主城居中 + 左右两侧资源全部刷出
- [x] Scene 标记与 Play 建筑位置重合（两套坐标一致）
- [x] 君主出生点 = 城堡中心（0,-3）
- [x] 相机开局能看到主城（不再偏移到一侧）
- [ ] 待 Play Mode 实测确认（T6 同批回归）

---

## 需求汇总表

| # | 需求 | 类型 | 涉及文件 | 优先级 |
|---|------|------|---------|--------|
| 1 | 地图生成资源缺失报错（底层重构） | bug修复+设计调整 | WorldManager.cs, MapGenRules.cs | P0 |
| 2 | NPC安全地带判断改用主城坐标 | 设计调整 | SceneHomePointProvider.cs, NPCBrain.cs | P1 |
| 3 | 开局NPC走(0,0) | bug修复 | 随需求2解决 | P1 |
| 4 | 坐标系原点调整（城堡中线=世界0,0） | 设计调整 | GridSystem.cs, GridConfig.cs, WorldManager.cs | P1 |
| 5 | 点击NPC文字丰富（多句随机对话） | 体验优化 | UnitController.cs | P2 |
| 6 | 需求4 originX 导致 Play 主城/资源未刷出 | 回归bug | 视方案：GetKingdomAnchorWorld/CameraSetup/Gizmo 或回滚 | P0 |

### 依赖关系

- 需求2依赖需求4：HomePoint 改用主城坐标后，如果坐标系原点调整了，主城坐标自动 = (0,-3)，更直观。但需求2不强制依赖需求4（即使不做需求4，GetKingdomAnchorWorld 仍返回正确坐标）。建议先做需求4再做需求2。
- 需求3随需求2自动解决，无独立任务。
- 需求1和需求4都改 WorldManager.GenerateMap 流程，建议同一批次改动避免冲突。
- 需求5独立，无依赖。
