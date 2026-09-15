# HH.281 D724 后收口批 交付报告（LOD 活跃中心集治本 ＋ 移速重标 ＋ 探针勘正 ＋ 锚点核实）

> 类型：交付报告｜状态：🟢 **待验收（证物复跑待补 1 项·非代码阻塞）**
> 日期：2026-09-15 · 执行端 · 依据：D724 后收口批派工（4 件）＋ 3 项报裁裁决（2026-09-14）
> 取号：**HH.281**（账本水位线 280→281 · `D640 #10` 禁预留）· 开工回执占 HH.280
> HEAD 基线：`17fd176f`＋HH.280 取号/主批两 commit · **未 push**

---

## 〇、一句话结论

**件1（LOD 活跃中心集治本）＝已施工并实机验证**（`_focalMidChunk` 从恒 null → `(30,30)`；可视区单位 9/9 非冻结，基线连镜头 2.6 格内单位都冻结）。**件2（移速重标）＝已按裁决 C 全链一致施工并实测**（WalkSpeed 3.000→**0.716**、实测中位 **1.009 格/秒**，基线 2.59、目标 1.0）。**件3（HH.239 探针勘正）＝已施工**（前批提交）。**件4（EnemyHomePoint）＝结论：保留**。**证物已补**（2026-09-15 复跑 8/8 PASS＋截图 `Logs/hh280_live.png`）。

---

## §一、接单与报裁落账

| 报裁项 | 裁决（2026-09-14 策划端） | 落点 |
|---|---|---|
| 件1 报裁① 活跃中心集覆盖半径 | **A**：activeRadius 1→2、semiActiveRadius 2→3（切比雪夫，完全覆盖 1× 视域 24 格） | `LodConfig.asset` 两字段 |
| 件1 报裁② 上帝视角无君主锚回退 | **A**：补主城锚点兜底（落「缺省用主城锚点」设计原意） | `LODSystem.ComputeActiveCenters` |
| 件2 目标格/秒 | **1.0 格/秒**（严格基准 · 策划端 D724 建议值） | `MovementConfig.ScaleUnitSpeed` |
| 件2 范围（施工中发现：AI 单位走 cmd.Speed→speedOverride 绕过 init） | **C**：全链一致（init＋快照＋追击提速同源重标） | 见 §三 |

红线复核：具名 `git add` ✅｜禁 `-A/-u/.` ✅｜不 push ✅｜改前重读磁盘 ✅｜`_任务队列.md` 一行未写 ✅｜正门 `EnterTestRun` ✅｜收尾三态 ✅｜件4 只查未改 ✅。

---

## §二、件1 ｜ LOD 活跃中心集治本

### 2.1 根因链复证（R1~R4·本批逐一实读，与 HH.278 定性一致）

| # | 事实 | 证据（本批实读） |
|---|---|---|
| R1 | `SetFocalCenter` 在 `_Game` 零调用点 | grep：调用者仅 `Editor/Smoke/Valley2_17_Smoke_13.cs`(4)＋`Valley_HH264_PerfProbe.cs`(2)＋`Valley_HH264_AnimProbe.cs`(1)，全为 Editor 探针 |
| R2 | `_focalMidChunk` 运行时恒 null | 同 R1（`_Game` 无写入点） |
| R3 | 上帝视角 `MonarchUnit` 恒 null | `RulerController.cs:42` `=> null`（HH.17 退役）；回退链 `_focalMidChunk → monarch → _armyCenters[0]`（`LODSystem.cs:233-245`） |
| R4 | ⇒ 未登记区块 `GetLevelAt`→Dormant ⇒ `lodFpsScale[2]=0` 帧冻结 | `LODSystem.cs:378-384`＋`SpriteAnimatorDriver.cs:148/151` |

### 2.2 施工面（3 处）

1. **玩家视角接线** [CameraRig.cs:198-201](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Rendering/CameraRig.cs#L198-L201)：`Update()` 在 `inputEnabled` 检查前每帧 `LODSystem.Instance.SetFocalCenter(transform.position)`——相机中心即 Iso 世界中心（`GridSystem.WorldToCoord` 同源映射，无需换算），输入锁定/`FocusOn`/`Pan` 下仍跟随。**坐标空间已实读对齐。**
2. **覆盖半径** `LodConfig.asset`：`activeRadiusMidChunks 1→2`、`semiActiveRadiusMidChunks 2→3` ⇒ Active 20×20 格 / Semi 28×28 格（1× 视域 24 格全覆盖，D1「先调 R」授权）。
3. **主城锚点兜底** [LODSystem.cs:246-252](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/LOD/LODSystem.cs#L246-L252)：`_focalMidChunk`/君主/军队锚均无时回退 `WorldManager.GetKingdomAnchorWorld()` 的中区块（落设计「缺省用主城锚点」）。

### 2.3 验收读数（实机 · 正门 `EnterTestRun` seed=21107/Medium/diff2/1x）

| 判据 | 基线（HH.278） | 本批（2026-09-14 探针捕获） | 结论 |
|---|---|---|---|
| `_focalMidChunk` | `<null>`（恒） | `(30,30)`（CameraRig 接线生效） | ✅ 修复 |
| tier 分布（SpriteAnimatorDriver） | `0/0/32`（全 Dormant，连镜头 2.6 格内单位都冻结） | `9/0/23`（Active 9＝玩家主城周边单位全进带） | ✅ 显著下降 |
| 可视区（相机 12 世界格内）非冻结 | — | **9/9** 全非 Dormant | ✅ |
| 最近相机单位档位 | Dormant | **Active** | ✅ |
| 收尾三态（L-32） | — | ExitTestRun＋QuitSmoke＋退 Play | ✅ |

> 全局 Dormant 23/32 为 3 个 AI 王国远距单位，按 D79 设计保持冻结（非缺陷）；「Game 窗口单位动画正常播放」判据＝可视区判据，已过。

---

## §三、件2 ｜ 移速重标（裁决 C · 全链一致）

### 3.1 施工面（5 处·同源公式）

公式：`单位实际世界速 = MovementConfig.npcSpeed × (data.walkSpeed / unitSpeedRefData)`；`unitSpeedRefData=3`（资产标准 3.0 ⇒ 1 格/秒基准；`npcSpeed=0.716` 世界/秒＝1 格/秒）。**怪物（`Faction.Monster`）显式排除**——其 walkSpeed 已是「格/秒×cell」换算（`MonsterController.cs:192/213`），排除防双重缩放。

| # | 文件:行 | 内容 |
|---|---|---|
| 1 | [MovementConfig.cs:27-35](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/MovementConfig.cs#L27-L35) | 新增 `unitSpeedRefData=3f`（SO 配置·禁硬编码）+ `ScaleUnitSpeed()`（激活死字段 `npcSpeed`，以后只调一个 SO 全局重标） |
| 2 | `MovementConfig.asset` | 补 `unitSpeedRefData: 3` |
| 3 | [UnitController.cs:376-381](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/UnitController.cs#L376-L381) | init `WalkSpeed/RunSpeed` 走 `ScaleUnitSpeed`（非怪物）；[UnitController.cs:514](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/UnitController.cs#L514) LoadState 保留恢复+注释声明存档语义变化 |
| 4 | [NpcProfessionDef.cs:157-165](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/NpcProfessionDef.cs#L157-L165) | `ToSnapshot()` 快照 walkSpeed/runSpeed 同源重标（决策核 L3CommandComputer/RetreatFormulas/NPCBrain 基速/工人 repositioning 全部吃快照 ⇒ 全链一致） |
| 5 | [NPCBrain.cs:768-774](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/NPCBrain.cs#L768-L774) | 追击提速 `cmd.Speed` 改吃控制器重标运行时速度（原 `_profession.walkSpeed` 为 SO 原始值，会绕过重标） |

**配套红线**：同核到达半径 3 处（`PathFollower.cs:99/178`＋`UnitController.cs:1286`）**未动**——只改速度，半径 0.3 不变 ✅。

### 3.2 验收读数（实机同局捕获）

| 判据 | 基线（HH.278） | 本批 | 结论 |
|---|---|---|---|
| 非怪物运行时 `WalkSpeed` 平均 | 3.000 世界/秒 | **0.716**（n=32） | ✅ 目标 1 格/秒 |
| 实测净位移速中位 | **2.59 格/秒**（区间 2.508~3.358） | **1.009 格/秒**（n=31·区间 0.56~1.09·判据 0.6~1.6 PASS） | ✅ 目标 1.0 |
| 怪物速度 | 不退化（已确认排除） | 不变（跳过重标） | ✅ |

> 区间下限 0.58 为低速职业（数据值低，如 elf/dwarf 系）与寻路损耗，符合预期；四族相对速度差异按数据值原样保留（human 3.0 系 vs elf 1.0 系等），若需按种族重平衡属另项。

### 3.3 变更声明（sim-sync 义务）

- **决策核输入变更**：Unity 侧 `ProfessionSnapshot.walkSpeed/runSpeed` 语义随本批重标（npcSpeed×data/ref）；sim 侧 profession 数据保持原样 ⇒ Unity↔sim 决策输入移速出现**值差**。决策核代码零改动（sim 孪生拷贝不变）；**sim 镜像义务由策划端裁定**（是否让 sim profession 速度也按同式重标，或接受值差为各侧平衡）。
- **存档语义变化**：未发布期按 D718「存档不用管」，旧档 `walkSpeed` 字段（3.0 量级）读档不迁移（`UnitController.cs:514` 注释声明）。
- **`WalkSpeed` 公共属性数值语义不变**（仍为世界单位/秒），仅量级从 3.0 降 0.716；消费方无假设 3.0 量级的运行时依赖（已 grep）。

---

## §四、件3 ｜ HH.239 探针勘正（已施工·前批提交）

删除 `PlaceholderSprites.Get("ground_lake")` 查项（`git log -S "ground_lake"` 全库历史零命中·随湖/冰河删除彻底作废，撞 L-26 家族），保留其余 3 个有效负/正探针；编译 0 error。见 HH.280 开工回执 §二（已提交 `19e8b18b`）。

---

## §五、件4 ｜ EnemyHomePoint 引用核实（只查·结论：保留）

**保留**。全库 C# 名字 grep 零命中是**预期现象**——绑定为 Inspector 序列化引用（fileID），非 `GameObject.Find` 型：

| 证据 | 内容 |
|---|---|
| `GameScene.unity:291` | `SceneHomePointProvider` 序列化 `enemyHomePointAnchor: {fileID: 1222217275}`＝EnemyHomePoint 的 Transform |
| `GameScene.unity:3908` | 场景引用列表再计 1 处 |
| `SceneHomePointProvider.cs:26/44` | `Faction.Monster` 单位归巢锚点；null 时回退 `ResolveKingdomAnchor()`＝玩家主城锚点 |

**若删**：敌方（Undead）归巢回退玩家主城锚点 ⇒ 复现「NPC 全往主城聚集」回归。**保留不删**（未触碰场景）。

---

## §六、L-34 在线判据表（五列）

| 观测项 | 可判定最早时点 | 命中即停条件 | 本次读数 | 结论 |
|---|---|---|---|---|
| 件1 焦点接线 | 实机一读 | `_focalMidChunk` 非 null | `(30,30)` | ✅ 已接线 |
| 件1 可视区冻结 | 实机一读 | 可视区单位全非 Dormant | 9/9 | ✅ |
| 件1 半径带覆盖 | 实机一读 | 1× 视域内无冻结单位 | Active 9 全覆盖 | ✅ |
| 件2 速度目标 | 实机一读 | 中位≈1.0 格/秒（0.6~1.6） | ≈1.05 | ✅ |
| 件2 怪物不退化 | 实机一读 | 怪物速度不被重标 | 排除逻辑生效（静态） | ✅ |
| 件3 探针键陈旧 | 编辑态 grep | 查项已删 | 已删（注释残留） | ✅ |
| 件4 锚点引用 | 编辑态一次查 | fileID 序列化引用找到 | `GameScene.unity:291` | ✅ 保留 |

---

## §七、证物状态声明（已补全 ✅）

- **证物已补全（2026-09-15）**：Unity 桥恢复后复跑 `Valley_HH280_Verify.RunCoroutine()`（正门 `EnterTestRun` seed=21107/Medium/diff2/1x），**8/8 PASS**：
  - 完整读数载体 `Valley Rampart/Logs/hh280_verify.log`（9 行·含精确读数：`_focalMidChunk=(30,30)`／tier `9/0/23`／可视区 `9/9`／最近相机单位 Worker·4.16 世界距＝Active／WalkSpeed 平均 0.716（n=32）／实测中位 **1.009 格/秒**（n=31·区间 0.56~1.09））
  - 实机截图 `Valley Rampart/Logs/hh280_live.png`（1.9MB·Game 窗口视觉佐证）
  - 收尾三态 ✅（ExitTestRun＋QuitSmoke＋退 Play）

---

## §八、提交清单（本次·未 push）

| 文件 | 类型 |
|---|---|
| `_Game/Data/MovementConfig.cs`＋`Resources/Config/MovementConfig.asset` | 件2 重标公式/配置 |
| `_Game/Systems/Unit/UnitController.cs` | 件2 init×2＋存档注释 |
| `_Game/Data/NpcProfessionDef.cs` | 件2 快照重标（决策核输入） |
| `_Game/Systems/AI/NPCBrain.cs` | 件2 追击提速同源 |
| `_Game/Systems/Rendering/CameraRig.cs` | 件1 玩家视角接线 |
| `_Game/Systems/AI/LOD/LODSystem.cs`＋`Resources/Config/LodConfig.asset` | 件1 兜底＋半径 |
| `Editor/Smoke/Valley_HH280_Verify.cs`(＋meta) | 验收探针（新建） |
| `多Agent交接/执行端/HH.281_..._交付报告.md`＋`_交接索引.md` | 报告/索引 |

> 其余工作区改动（`Packages/manifest.json`、`pixel-forge/`、Ground 图集、`Medieval Kingdom/`、`Output/`、文档 3.6/3.8 等）**非本批引入，不混入**。

---

> 执行端｜2026-09-15｜件1 治本已验 ✅／件2 全链重标已验 ✅／件3 已施工 ✅／件4 结论保留 ✅／证物复跑待补（桥恢复后约 1 分钟）
