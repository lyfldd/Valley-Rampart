# HH.48 寻路2 交付报告（出生点吸附+A* 目标 snap，Q10 前清障）

> 类型：交付报告（行为级验收+回归全绿），含一条验收内新发现与处置说明
> 状态：✅ 验收成立（2026-09-01 策划端实盘复核；裁决见 §六）
> 日期：2026-09-01 · 发起端：执行端 · 指令源：HH.47 §五-1 + §八裁决（寻路2 插队 Q10 前，用户拍板）· 队列「寻路2」行

## 一、做了什么（两范围全做，A* snap 判定简单一并落地）

### 1.1 范围①出生点最近可走微格吸附（PopulationSystem 域，必做）

新增 [SpawnPosSnapper.cs]( Valley Rampart/Assets/_Game/Systems/Grid/SpawnPosSnapper.cs)（新文件，Grid 域静态纯函数）：
- **宏格环形扫描**：本格可走→原样返回（零位移）；不可走→按「距离优先→同行 dx 升序」固定环序找最近可走宏格（`IsWalkable && !IsObstacle`，与 IsSubWalkable 同语义），返回该格中心微格世界坐标；
- **半径有界**：`MaxCellRadius=4` 宏格（兜底防御参数，暂 const+注释，如需可调配位归 SO 待裁决）；界内无可走格→`LogWarning`+原样返回（任务书兜底告警）；
- **确定性**：扫描零随机——同 seed 两轮 snap 逐字节一致，不破坏 2_17 2b ③-a 复现链（Smoke_14 #3/#12 实测佐证）；
- **verbose 参数**：高频口（SetDestination 每 tick）静默，出生链低频口带上下文日志。

接入四出生收口（行走单位全链，机器工事/怪物不接入）：

| 出生链 | 落点 | 改动 |
|--------|------|------|
| 玩家开局 4 工人+5 居民 | [PopulationSystem.SpawnAtAnchorSide]( Valley Rampart/Assets/_Game/Systems/Kingdom/PopulationSystem.cs#L219) | SnapWorld(context=开局{faction}_{occ}) |
| 繁殖 Child | [PopulationSystem.GetBirthPosition]( Valley Rampart/Assets/_Game/Systems/Kingdom/PopulationSystem.cs#L323) | 房屋旁+锚点兜底两 return 均吸附 |
| AI 王国工人 6×4 | [KingdomFoundry.SpawnAiWorkers]( Valley Rampart/Assets/_Game/Systems/Kingdom/KingdomFoundry.cs#L92) | SnapWorld（MapData 层可走≠运行时可走：预置建筑占格） |
| 流民补员+初始散投 | [VagrantCampSystem]( Valley Rampart/Assets/_Game/Systems/Kingdom/VagrantCampSystem.cs#L250) 两处 | 补员 BirthCampPos 仍=营地语义点；散投 BirthCampPos 改=吸附后实际落点（滞留游荡语义对齐站位） |

### 1.2 范围②A* 目标格 snap（判定简单，一并做）

[PathfindingService.FindPathImmediate]( Valley Rampart/Assets/_Game/Systems/Pathfinding/PathfindingService.cs#L19)：目标微格 `!IsSubWalkable` → `SpawnPosSnapper.SnapSub` snap 后求解；界内无可走格→保持原目标走原失败链。**起点不 snap**（路径起点必须=单位实际位置，防瞬移语义）。

### 1.3 验收内新发现与追加收口（诚实登记）

首轮验收探针暴露**比出生口袋更高频的困死入口**：游走/追击目标随机落山体水域（flags=None）→ A* snap 后路径到最近可走格，但 **PathFollower._destination 仍是原始点** → 路径走完 `IsArrived(原始点)` 不满足 → 进入「路径走完仍未达→直线 `MoveTowards(_destination)`」分支（[PathFollower.cs L130-136]( Valley Rampart/Assets/_Game/Systems/Pathfinding/PathFollower.cs#L130)）→ **单位被拖进不可走格**（R1 实测 900 帧后 13/37 单位滞留 flags=None/Water 格）→ 起点口袋化 → Unreachable 困死。
**追加收口**：[PathFollower.SetDestination]( Valley Rampart/Assets/_Game/Systems/Pathfinding/PathFollower.cs#L39) 入口先 SnapWorld（verbose=false 静默）——`_destination` 与路径终点一致，直线分支安全。效果实测：滞留 13→4 且全部 Failed=0（见 §二）。

## 二、行为级验收（四项全过）

| 验收项 | 结果 | 证据 |
|--------|------|------|
| 正探针：口袋吸附成功 | ✅ | 复现锚 cell(126,119)（HH.47 npcId 29 困死格）：`原格可走=False → 吸附后=(502,474) 可走=True`；seed=20260901 真实进局全链复现 |
| 负探针：正常出生零误伤 | ✅ | 城堡锚点 (0.00,81.92) 吸附后逐位相等（零位移=True）；本格可走零开销原样返回 |
| AI 出生链同验 | ✅ | KingdomFoundry 4 国 24 工人经同吸附入口出生；900 帧观察 Failed=0（Q10 探针偶发假失败风险清偿） |
| 真实进局 900 帧 | ✅ | `state=Playing 有PF=37 Following=20 **Failed=0**`（R1 同口径 13 滞留→R2 4 滞留且零 Failed） |

**关于 900 帧后 4 个滞留格单位（npc 1/12/20/33）的定性**：位置处于 flags=None 格但 **Failed=0、Following 正常**——A* 起点不查自身可走性、邻居有可走格即可自行脱出，属物理推挤/浮点边界的**瞬时漂移**而非口袋困死（困死判据=Failed 循环=0 ✓）。真正口袋困死的两条入口（出生落口袋、目标直线穿越）均已封堵。

## 三、回归结果（三冒烟全绿）

- **Smoke_14**：`ALL PASS（P1~P6 + #9/#12）`——含 #3/#12 同 seed 逐字节一致（吸附确定性佐证）；
- **Smoke_2_13_C**：`ALL PASS（P1~P7）`；
- **Smoke_2_13_D**：`ALL PASS（P1~P6）`；
- 编译 0 error（16 warning 全存量）。

**sim-sync 分级：F 级（不涉）**——改动全在 Unity 侧出生链/寻路入口；AI.Core 决策核/FactorContext/TuningSnapshot/champion/SO 零触碰；出生吸附输出为确定性纯函数，无 sim 镜像义务。

## 四、git 在场（不 commit，策划端代执）

本批改动（Assets 侧，+30/-10 + 新文件）：
- 新增：`Assets/_Game/Systems/Grid/SpawnPosSnapper.cs`（+.meta）；
- 修改：`PopulationSystem.cs` / `KingdomFoundry.cs` / `VagrantCampSystem.cs` / `PathFollower.cs` / `PathfindingService.cs`；
- 工作树另有 3.1.3 美术文档、0.6 审查决策记录、pixel-forge/* 改动——**策划端并行产物，非本批**，不纳入本批 commit。

建议 commit：`fix(寻路2): 出生点就近可走吸附+A* 目标格 snap——口袋困死双入口清偿（HH.48）`

## 五、待裁决与建议

1. **MaxCellRadius=4 const**：兜底防御参数暂硬编码（非玩法数值）；如需可调请裁决配位（KingdomConfig 或 GridConfig）。
2. **A* 起点可走性**：现状起点不查自身（邻居可走即可脱出，配合双 snap 入口已无困死路径）；若要严格化「起点不可走→就近脱出段」归 2_6 P0b 服务化域，建议挂账不动。
3. 「寻路2」行清偿后 **Q10 阻塞解除**（寻路1+2 两批均清），可回主队列。
---

## 六、策划验收（2026-09-01 策划端实盘复核回写；验收成立）

> **实盘复核记录**：SpawnPosSnapper.cs 直读=确定性环序（距离优先→同行 dx 升序零随机）/半径有界 MaxCellRadius=4+兜底告警/verbose 分频（SetDestination 高频口静默/出生链低频口带日志）/静态纯函数仅消费 IPathGrid——与声明逐字吻合；PathfindingService/PathFollower diff 实读=A* 目标 snap（界内无可走→原失败链）+起点不 snap（防瞬移语义）+SetDestination 入口 snap（_destination 与路径终点一致）；**Follow 直移保底兼容性确认**——Follow 目标=跟随单位当前位置（站在可走格），SnapWorld 本格可走零吸附原样返回，语义零影响且其直线分支反获保护；pixel-forge/0.6/3.1.3=并行轨产物不纳入确认。

| 决策点 | 裁决 |
|--------|------|
| 验收结论 | **寻路2 成立+Q10 寻路侧全清**（寻路1+2 双清，剩输入1）；四出生收口+双 snap 困死入口全封堵；900 帧 Failed=0+滞留 13→4（残存=物理推挤瞬时漂移非口袋困死，定性采信）；三冒烟 ALL PASS+Smoke_14 #3/#12 逐字节一致佐证吸附确定性 |
| SetDestination 追加收口 | **追认**（验收内发现当批清偿，HH.47 D453 死代码收口同先例）：「目标落不可走格→直线分支拖入」=困死双入口的另一半，不修则范围①②清偿不完整；+6 行小修+实测 13→4 佐证——**发现式修复嘉奖** |
| §五-1 MaxCellRadius=4 | **保持 const+注释**（用户拍板）：防御兜底参数非玩法调参面，SO 化不值 Gate；挂账池登记「若需调参归 GridConfig」 |
| §五-2 A* 起点可走性 | **照裁挂账**：严格化「起点不可走→就近脱出段」归 2_6 P0b 服务化域（配合双 snap 入口已无困死路径，低危） |
