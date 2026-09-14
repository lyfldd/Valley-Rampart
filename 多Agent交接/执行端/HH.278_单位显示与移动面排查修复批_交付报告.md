# HH.278 单位显示与移动面排查修复批 交付报告（移速基线／Game 无动画定性／Scene NPC 定位）

> 类型：交付报告｜状态：🟡 待验收（**含 1 项停手待裁**）
> 日期：2026-09-14 · 执行端 · Gate：`G4-1`
> 取号：**HH.278**（账本水位线 **277→278** · `D640 #10` 禁预留）· 回执占 HH.274
> HEAD 基线：`7536d14b` · **未 push**
> 依据：`HH.274_单位显示与移动面排查修复批_任务书.md`（D719）＋ `HH.274_…_开工回执.md`（本批）＋ D723 派工总纲

---

## 〇、一句话结论

**件1 已出实测基线并停手待裁**（实测 **2.51~3.36 格/秒·中位 2.59**，超基准 ≈2.6 倍；**非**任务书所估 7 倍——实盘资产为 `3/6` 而非代码默认 `5/10`）。
**件2 定性闭环＝「驱动未跑」的变体**：驱动实例在场、帧数据 32/32 齐全、回退链完好，**但全部单位被 LOD 判 Dormant ⇒ `lodFpsScale[2]=0` ⇒ 帧播放被完全冻结**；根因＝**`LODSystem.SetFocalCenter` 在游戏代码中零调用点**（`_focalMidChunk` 运行时恒 `null`，实测坐实），且其 null 回退锚＝`MonarchUnit`，而**上帝视角下君主实体退役** ⇒ 活跃中心集近空。
**件3 定性＝孤儿对象**：场景内唯一「既是单位又不在图上」的对象是 **`VFriendly`**（`Data=NULL`／无 `SpriteAnimator`／无 `NPCBrain`／`SpriteRenderer.sprite=null`／位置 (0,0,0)／**全库 C# 零引用**）；`参考图`／`平原` 为**已禁用空对象**，排除。

---

## 一、件1 移速重标 → **停手待裁**

### 线1｜移速实测（改前）

| 项 | 读数 |
|---|---|
| **实测净位移速** | **2.508 / 2.570 / 2.591（中位）/ 2.664 / 3.131 / 3.358 格/秒**（34 单位，**28 个在移动**，窗口 **3.21 s**，`timeScale=1`） |
| 逐单位明细（节选） | `Worker/r0 10.78 格 → 3.358`／`Worker/r0 9.92 格 → 3.087`／`Vagrant/r2 10.06 格 → 3.131`／`Resident/r0 9.52 格 → 2.964`／`Worker/r1 8.25 格 → 2.570` |
| 运行时 `WalkSpeed` | **3.000**（28/28） |
| 实盘 `Data.walkSpeed / runSpeed` | **3.000 / 6.000** |
| 设计基准 | `MovementConfig.npcSpeed = 0.716` 世界单位/秒 ＝ **1 格/秒** |
| **判定** | **≈ 超基准 2.6 倍**（净位移口径）／**理论上限 4.19 格/秒**（＝`3.0 ÷ 0.7155`） |

**换算式实证**：`UnitController.cs:1224`＋`:1244` 世界步长＝`GridDistClampDir(dir) × speed × dt`；`GridDistClampDir`（`:1302-1308`）在**格空间**归一 ⇒ 沿格轴世界步长 0.7155。实测/理论 = 2.59/4.19 = **62%**（差额＝路径拐弯与到达停顿）。

**勘正 1**：任务书 §一③ 记「`UnitData.cs:79` `walkSpeed = 5f`／`:82` `runSpeed = 10f` ⇒ 5÷0.716≈7 格/秒」——代码默认值 5f/10f **实读无误**，但**实盘资产覆盖为 3/6** ⇒ 「≈7 格/秒」**与实机不符**（实测 ≈2.6 格/秒）。

### 线2｜怪物不退化

- `MonsterController.cs:192/213` `walkSpeed = def.speedCellsPerSec * cell`（`MonsterDef.cs:18` 注释逐字＝「移速（**格/秒**）」）⇒ **怪物源＝格/秒·显式换算**，与 NPC 的**世界单位/秒**不同源。
- `MonsterController.cs:183/204` 走 `ScriptableObject.CreateInstance<>` **运行时实例**写入 ⇒ **不污染资产**。
- ⇒ **改 NPC 侧无连带**；唯一交叉点＝`MovementConfig.npcSpeed` 被 `PathFollower.cs:99/178`＋`UnitController.cs:1286` 作**到达半径**消费（改数值会连带改半径，非速度）⇒ 采 (b) 时须同核。
- 本轮同局**怪物数=0**（Day 1 无怪）⇒ 未取得怪物实测速；**如实声明**（不改怪物路径，故不构成回归风险）。

### 线3｜死字段

- **未改**（停手）⇒ `MovementConfig.npcSpeed` **仍为死字段**（消费点仅 3 处到达半径）。
- 采 (b) 后其消费点将由 3 → **4**（新增 `UnitController` 移速初始化）。

### **请裁 1 项｜件1 修法取向**

| 选项 | 内容 | 代价/风险 |
|---|---|---|
| **(a) 数据侧重标** | 改 `Resources/UnitData/` **35 个 `.asset`**（18 `Human_Player_*`＋3 Elf＋3 Dwarf＋3 Orc＋8 塔/墙/门/机器） | 多点漂移、易漏；(a) 下 `npcSpeed` 仍为死字段 |
| **(b) 代码侧接管**（任务书推荐） | `UnitController.cs:376`／`:510` 两处改 `MovementConfig.npcSpeed × 倍率`（倍率源 `RaceDef.moveSpeedMul`，已由 `EffectiveSpeed` 单点消费） | **2 行**；激活死字段；**副作用＝全局 NPC 手感一次性变更** |
| **★ 前置问题（须先定）** | **目标格/秒是多少？** 现状 **≈2.6**；严格按基准＝**1.0**（则全局 **降速 2.6 倍**，属**玩法级**变更）；折中＝**2.0**（`npcSpeed ≈ 1.43`） | —— |

> 执行端建议 **(b)** ＋ **目标值由策划端定**；在未定目标前**不施工**（避免「修了但手感全废」的返工）。

---

## 二、件2｜Game 窗口无动画 → **定性闭环**

### 线4｜四层排查（逐层读数）

| 层 | 检项 | 读数 | 结论 |
|---|---|---|---|
| **①日志层** | `SpriteAnimator._warned`（一次性告警登记）／`._missingSets`（负查询缓存） | **0 / 0** | **未发生「整套无真图」告警** ⇒ 排除「帧数据缺」 |
| **②帧数据层** | animator 总数／`HasRealFrames`／六态帧集可用数 | **32 / 32（无真图 0）**／`idle 32・walk 32・attack 32・run 32・loot 32・death 0` | 帧数据**齐全**（除 `death`，与 HH.276 件3 结论一致） |
| **③驱动层** | `SpriteAnimatorDriver` 实例／`ActiveCount`／`IsRegistered` | **在场 enabled=true / ActiveCount=27 / 27-32 注册** | 驱动**在跑**（非"没实例化"） |
| **④回退链** | `stateFallback`／`stateMode` | `[0,0,0,1,0,-1]`／`[0,0,1,0,2,2]` | 回退链**完好** ⇒ 排除「回退链吞掉」 |
| **④附带** | `lodFpsScale`／`defaultFps`／`lodRefreshInterval` | **`[1, 0.5, 0]`**／12／0.5 s | **tier2 ⇒ fps × 0** ← 嫌疑点 |

### 定性（三选一）＝ **「驱动未跑」的变体：驱动在跑，但被 LOD 冻结**

**机制（实读 `SpriteAnimatorDriver.cs:143-157`）**：
```
s.lodTimer -= dt;  if (s.lodTimer <= 0f) { s.lodTimer = cfg.lodRefreshInterval; s.lodTier = lod.GetLevelAt(pos); }
float scale = a.SpeedScale * cfg.LodScaleOf(s.lodTier);     // LodScaleOf = lodFpsScale[tier]
if (scale <= 0f) { /* 冻结档（Dormant）：停推进，仅保证当前帧在位（静态帧） */ }
```
⇒ **tier=2 时 `scale = 1 × 0 = 0` ⇒ 帧推进被完全跳过 ⇒ 画面定格**。

### 根因链（四条，全为实测/实读）

| # | 事实 | 证据 |
|---|---|---|
| **R1** | **`LODSystem.SetFocalCenter` 在游戏代码中零调用点** | 全库 grep：调用者仅 `Editor/Smoke/Valley2_17_Smoke_13`／`Valley_HH264_AnimProbe`／`Valley_HH264_PerfProbe`（**均为 Editor 探针**）；`Assets/_Game/**` 内 **0 处** |
| **R2** | 运行时 `_focalMidChunk` **恒为 `null`** | 探针直读私有字段：`focalBefore = <null>`（且 `Assets/_Game` 内无任何 `focal` 写入点，仅 `LODSystem` 自身 4 处） |
| **R3** | null 回退锚＝`RulerController.MonarchUnit`，而**上帝视角下君主实体退役** | `LODSystem.cs:233-245`（`anchorMid = _focalMidChunk ?? monarch ?? _armyCenters[0]`）＋运行日志逐次复现 `[RulerController] 上帝视角：君主实体退役，不生成君主单位（HH.17 裁决）` |
| **R4** | ⇒ `GetLevelAt` 对绝大多数中区块返 **Dormant**，含距镜头中心极近者 | `LODSystem.cs:378-384`（未登记且不在 `_activeBandSet` ⇒ `LodLevel.Dormant`） |

### 实测对照（Dormant 冻结实证）

**A. tier 分布（含「相机贴住单位」的极端条件亦然）**

| 读次 | 条件 | tier 分布 0/1/2 | 备注 |
|---|---|---|---|
| ① | 相机贴 home ortho **2.5** 快照 | **0 / 0 / 32** | 其中 `Worker` 距镜头中心仅 **2.6 格**仍 Dormant |
| ② | ortho 8.64 | 0 / 0 / 32 | 同上 |
| ③ | 同局稍后 | 3 / 9 / 20 | 随威胁热点/军团中心变化 |
| ④ | 同局再后 | 4 / 7 / 21 ／ 5 / 7 / 20 | 稳定在 **≈20/32 = 63% Dormant** |

**B. 同窗口「帧是否推进」对照（1.0~1.15 s · **同态 + tier 两端一致** 才算干净样本）**

| 组 | 条件 | 干净样本 真帧推进 / 冻结 | 按 tier |
|---|---|---|---|
| **Ⅰ** | `focal = null`（**＝游戏真实态**） | **10 / 22** | **tier0 2/4・tier1 6/7・tier2 2/21** |
| **Ⅱ** | `focal = home(128,128)`（探针手动设） | **9 / 22** | **tier0 2/3・tier1 6/7・tier2 1/21** |

⇒ **tier2 组：21 个中 19 个（90.5%）完全不推进**；tier0/1 组以推进为主。
（Ⅰ/Ⅱ 对比同时说明：**仅"设焦点"不改变结果** —— 见下方「诚实声明」。）

**C. 正向对照（tier 变量确实由焦点驱动）**：手动调用 `SetFocalCenter(home)` 后，`_focalMidChunk` 由 `<null>` → `(32,32)`，tier 分布一度由 **0/0/32** 变为 **5/7/20** ⇒ **焦点→活跃中心集→tier 的链路是通的**。

### ⚠️ 诚实声明（本批未治本，且已定位缺口）

- **仅调用 `SetFocalCenter` 不能恢复动画**：组Ⅱ（设焦点）与组Ⅰ（null）的 tier 分布与帧推进比**基本一致**（tier2 仍 21/32）⇒ **「设焦点」是必要非充分**。
- 缺口在 **活跃中心集的构建口径**：`ComputeActiveCenters`（`LODSystem.cs:228-263`）＝「焦点中区块 ＋ 热度 > `hotspotThreshold` 的中区块（前 `maxCenters` 个）」，其**活跃带的覆盖半径**与**中区块登记口径（`_midStates`/`_activeBandSet` 何时生成）**未在本批查明。
- ⇒ **完整治本属代码面（`Systems/AI/LOD/` ＋ 玩家视角接线），且触碰 LOD 决策面** ⇒ 按本批红线「排查优先」**不在本批动手**，**建议另立批**（见列报 2）。

### 附：与件2 相关的 `death` 缺帧

六态帧集可用数中 **`death = 0`**（32 个 animator 全无 death 帧）⇒ 与 `HH.276` 件3 结论**完全一致**（`death` 0/49）。`SpriteAnimatorDriver.cs:130-141` 对「once 态无帧」走**立即完成回调、不动 sprite** ⇒ 死亡**不崩、即刻回收**，但**无死亡动画**。

---

## 三、件3｜Scene 内 NPC 不在地图上 → **孤儿对象（已定性）**

### 线5｜对象名＋组件清单＋位置＋定性

| 对象 | 路径/场景 | 位置 | 组件清单 | activeSelf | **定性** |
|---|---|---|---|---|---|
| **`VFriendly`** ⭐ | `FromGameScene/GameScene.unity` **L2082**（root，`NotAPrefab`） | **(0.000, 0.000, 0.000)** | `Transform` ＋ **`SpriteRenderer`（sprite=`null`）** ＋ **`Rigidbody2D`（Dynamic／gravityScale=1／simulated）** ＋ **`UnitController`**（`npcId=0`／`raceId=0`／`EffProfession=Civilian`／`enabled=true`／**`WalkSpeed=0`**／**`Data=NULL`**）；**无 `SpriteAnimator`／无 `NPCBrain`** | ✅ true | **孤儿对象**（编辑态空壳：无 UnitData ⇒ 无图/无动画/无 AI；**全库 C# 零引用**；位置在地图角点外） |
| `参考图` | GameScene root（L 命中转义名 `\u53C2\u8003\u56FE`） | (12.100, 26.850, 0) | **仅 `Transform`**（无任何渲染组件） | ❌ **false** | **已禁用的空标注对象** ⇒ **排除**（不可能"表现动画"） |
| `平原` | GameScene root（转义名） | (1.600, 25.000, 0) | **仅 `Transform`** | ❌ **false** | 同上 ⇒ **排除** |
| `MapRender/Units` | 容器 | (0,0,0) | `Transform` | ✅ true | 空容器（`childCount=0`）⇒ 与任务书记载一致 |

**判定依据（三项）**：
1. `VFriendly` **无 `UnitData`**（`Data=NULL`）⇒ `UnitController` 在 `UnitController.cs:376` 的 `data.walkSpeed` 取值链根本不会执行 ⇒ `WalkSpeed=0`；
2. **无 `SpriteAnimator`** ⇒ 不会被 `SpriteAnimatorDriver` 注册（实测 `IsRegistered=27/32`，`VFriendly` 不在活跃表）；
3. **C# 全库零引用**（grep `VFriendly` 于 `Assets/**/*.cs` **0 命中**）⇒ **代码永不初始化它**。

### 附：场景孤立对象普查（仅 `Transform`、无渲染、0 子物体）

| 对象 | activeSelf | 位置 | 说明 |
|---|---|---|---|
| `MapRender/Overlay` | ✅ | (0,0,0) | 容器（正常） |
| `MapRender/Units` | ✅ | (0,0,0) | 容器（正常） |
| `MapRender/Prefab_Building` | ✅ | (0,0,0) | 容器（正常） |
| **`参考图`** | ❌ | (12.1, 26.85, 0) | **残留标注（已禁用）** |
| **`平原`** | ❌ | (1.6, 25.0, 0) | **残留标注（已禁用）** |
| **`EnemyHomePoint`** | ✅ | (10.0, −3.0, 0) | 空对象，**名字像代码锚点**（本轮未 grep 其引用，见列报 3） |

**另发现**：场景内共 **2 个** 场景内 `UnitController` 对象＝`VFriendly` 与 **`ruler`**（pos `(-4.55, -3.00, 0)`，`EffProfession=Civilian`，`SpriteRenderer` 在场）—— 后者疑为「君主预置对象」（上帝视角下已退役不生成君主单位）。

### 需用户补充（任务书已问）

> 「那个能表现动画的 NPC」在 Hierarchy 中的**确切名字**。执行端自查结论：**最可能＝`VFriendly`**（场景内唯一"是单位却不在图上"的对象）；若用户所指为 Play 态下**另一**对象，请补名字/截图，执行端按 §二 同法（tier＋帧索引）复测。

---

## 四、`L-34` 在线判据表（五列）

| 观测项 | 可判定最早时点 | 命中即停条件 | 本次读数 | 结论 |
|---|---|---|---|---|
| 件1 移速现值 | 进局 +3s 内、`timeScale=1` | 取得 ≥10 个移动单位的净位移速 | 28/34 单位、中位 **2.591 格/秒** | **已定值 ⇒ 停（不施工，转报裁）** |
| 件2 是否帧数据缺 | 进局首帧 | `_warned > 0` | **0** | **排除①**，继续 |
| 件2 是否驱动未实例化 | 进局首帧 | `Driver == null` | **在场 ActiveCount=27** | **排除"无实例"**，继续 |
| 件2 是否被 LOD 冻结 | tier 首次刷新（0.5s 内） | tier2 占比 > 50% | **20~21/32 ≈ 63%** | **命中 ⇒ 立即定论**（不再追其它假设） |
| 件3 对象定性 | 编辑态一次查询 | 找到「是单位但不在图上」的对象 | `VFriendly`（`Data=NULL`·零引用） | **已定性** |

> 无「跑完再看」：四项均为**机制读数定论**，且件1 在得到目标值前**不进入施工**。

---

## 五、回归

- 编译 **0 error**（`get_state`：`isCompiling=false`、`console.lastErrors=[]`）
- 正门 `EnterTestRun` ＋ `ExitTestRun` ＋ 收尾三态：`isPlaying=false`／`isCompiling=false`／`sceneDirty=false` ✅（`L-32`）
- **零改动面** ⇒ 无需四族四轮重跑（本批未改任何代码/资产/场景；`AI.Core` 零触；`GameScene.unity` 仅**打开**未保存）

---

## 六、列报

1. **件1 勘正**：任务书所引 `UnitData.cs:79/82` 的 `5f/10f` 是**代码默认**，实盘资产为 **3/6** ⇒ 「≈7 格/秒」估算偏高，实机 **≈2.6 格/秒**。
2. **件2 治本建议另立批**：缺口＝`LODSystem` 活跃中心集口径（`ComputeActiveCenters` 覆盖半径／`_midStates` 登记时机）＋**玩家视角未接 `SetFocalCenter`**（`Assets/_Game` 零调用点）。**该批将触碰 LOD 决策面** ⇒ 建议签发时明确「允许改 `Systems/AI/LOD/` 与视角接线」及「是否顺带修上帝视角无君主锚回退」。**本批按红线未动手。**
3. **`EnemyHomePoint`（场景空对象）引用未查**：名字像代码锚点（`GameObject.Find` 型），本批未 grep；建议随件3 收口批一并核实「删 or 留」。
4. **件1 若采 (b)**：须同核 `MoveSpeed` 语义交叉点 3 处（`PathFollower.cs:99/178`＋`UnitController.cs:1286` 的**到达半径**），避免"改速度捎带改半径"。
5. **本轮同局怪物数=0**（Day 1 无怪）⇒ 线2「怪物不退化」**只完成静态核查**（不同源＋不入资产），未取实测速；如策划端认为必要，可指定 seed/日期使怪物在场后补测。
6. **`death` 帧 0/32** 与 `HH.276` 件3 交叉印证（全 49 角色缺 death 帧）⇒ 建议把 `death` 补图与 `HH.276` 件3 合并签发。

---

## 七、红线遵守 ＋ 写后验

| 红线 | 落实 |
|---|---|
| 不碰 `GameScene.unity` | ✅ 仅 `EditorSceneManager.OpenScene` 只读查询；末态 `sceneDirty=false`；`game_scene` 文件 0 行 diff |
| 不碰 `MapGenRules.cs` | ✅ 零触碰 |
| 本批不补美术图 | ✅ 仅出清单/结论 |
| **件1 修法先报裁** | ✅ **停手**，未改任何数值 |
| 不动 `AI.Core`／`cellSize`／`SpriteRefTable` 结构 | ✅ |
| **写后验** | 本批**零代码、零资产**改动；`Valley Rampart/` 下 `git status` 仅 `Logs/`（已忽略）；报告/回执/台账/索引见下 |
| **未 push** | ✅ |

**原始读数载体（不入库）**：`Valley Rampart/Logs/hh274_probe.log`（件1 移速逐单位）／`hh274_lod.log`／`hh274_ab.log`／`hh274_ab2.log`／`hh274_focus_a.txt`／`hh274_snap.txt`

**产出**
- `多Agent交接/执行端/HH.274_单位显示与移动面排查修复批_开工回执.md`
- `多Agent交接/执行端/HH.278_单位显示与移动面排查修复批_交付报告.md`（本文件）
- `多Agent交接/_编号登记.md`（水位线 277→278）
- `多Agent交接/_交接索引.md`（HH.278 行）

---

> 执行端 · 2026-09-14 · **本批含 1 项停手待裁（件1 修法取向＋目标格/秒）**；下串＝`HH.272 地图生成重构批`（D723 派工总纲序 3）
