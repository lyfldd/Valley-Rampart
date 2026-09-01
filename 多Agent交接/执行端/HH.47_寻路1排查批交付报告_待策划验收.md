# HH.47 寻路1 排查批交付报告（P0 插队清障，Q10 前必清）

> 类型：排查报告（根因定性+四步证据+修复+回归），含附带发现待裁决
> 状态：✅ 验收成立（2026-09-01 策划端实盘复核；三裁决见 §八）
> 日期：2026-09-01 · 发起端：执行端 · 指令源：HH.46 §三-1+§八裁决（🔴寻路不可达=Q10 前插队 P0）· 队列「寻路1」行

## 一、根因定性（一句话版）

**GridSystem 对 `IPathGrid.IsWalkable` 的实现违反接口契约**：契约（IPathGrid.cs §18）要求入参为**微格坐标**（"跨格地形逐微格判定"），实现却把微格坐标直接当**宏格**查表——微格域 1024×1024（256×divisor4）远超宏格域 256×256，**sub≥256 的查询全部 InBounds 失败→WalkFlags.None→不可走**，A* 除 sub<256（左下 64×64 格）外**全图不可达**。属**长期潜伏的代码 bug，非环境、非 2_13 回归**（divisor=4 自 57843c1「doc1 网格空间层+2_1 落地」即是；错误实现随 2_6 P0a 微格 A* 引入后一直以"宏格语义恰好撑住 cell 级调用方"的形态潜伏）。

## 二、四步排查证据（按任务书指令序）

### 步骤① 真实进局 Console：网格/地形在场性 → 「环境未就绪」假说**排除**

GameScene 实 Play + `LoadManager.InitializeNewGame`（与 GameBootstrap.StartNewGame 同链）后实测：
- `[WorldManager] 地图生成: 256x256, 出生点=5, 威胁点=10, 自然建筑=2751`、`世界已装配: 网格=256x256` 在场；
- 即时探针：`GridInst=True gridConfig=True w=256 h=256 | ResourcesLoad(GridConfig)=True | ActiveMap=True mapW=256 | anchor=(0.00, 81.92)`——**网格尺寸/Config/地形数据/城堡锚点全部正常解析**；
- 修复前症状复现：`[NPCBrain] PathFailed 兜底: 单位 npcId=29(Resident) 寻路不可达((0.00, 0.00)) → 转 Idle` ×全单位×每 tick 重试（Console 累计 6.5 万条）。

### 步骤② PathFailed 具体失败原因 → 代码级钉死

活体探针（存活单位上直测）：
- `FindPathImmediate(单位pos, 邻格)` / `(pos, 城堡)` / `(pos, 原点)` **全部 `Unreachable` wps=1**（A* 起点展开后无可走邻居，立即枯竭）——不是"无结果 null"、不是"起点越界 null"，是**邻居全判不可走**；
- 单位 sub 坐标=(948,912) 等（>256），`SubToCell` 后宏格 `IsWalkable=True`——**同一格"宏格视角可走、微格视角不可走"**，矛盾指向 [AStarSolver.cs L47]( Valley Rampart/Assets/_Game/Systems/Pathfinding/AStarSolver.cs#L47) `grid.IsWalkable(nb)`（微格入参）与 [GridSystem.cs IsWalkable]( Valley Rampart/Assets/_Game/Systems/Grid/GridSystem.cs#L235)（宏格 InBounds 查表）的**坐标域错位**；
- 契约比对：IPathGrid.cs 注释明写 `bool IsWalkable(GridCoord subCoord)`="微格可走（跨格地形逐微格判定）"→ 实现违约实锤。

### 步骤③ 裸 Play（编辑器直开 GameScene）对照 → 「进局链差异」**排除**（全局回归实锤）

- 修复前同探针在「GameScene 直开+程序化进局链」下复现同样全 Unreachable——与 Menu 点击进局无关；
- **过程伪影归因（诚实登记）**：首轮探针曾在 `InitializeNewGame` 与 `SpawnInitialEntities` 之间插入一帧，[ThroneAnchor.cs]( Valley Rampart/Assets/_Game/Systems/Kingdom/ThroneAnchor.cs) 0.5s 轮询在「Playing 后、开局人口生成前」空窗期判「工人全灭→GameOver」并冻结 timeScale=0（活体探针反证：`存活=37 死亡=0`）。真实 GameBootstrap 为同帧同步连续调用**不存在该空窗**，正常流程不受影响；此为排查工装伪影，非产品 bug（但暴露 ThroneAnchor 对"Playing 态+0 工人"瞬时态无宽限，见 §五-3）。
- 步骤④ checkout 二分：**未启用**——②已代码级钉死根因；辅证：32ea494→4f782db 两锚点均为 docs 提交，其间代码批（2_17 步骤14 Satiety/AbstractEconomy/Academy）不触 GridSystem/寻路层；divisor=4 与错误实现均早于两锚点长期在场。

## 三、修复方案（小修，未超 Gate）

[GridSystem.cs]( Valley Rampart/Assets/_Game/Systems/Grid/GridSystem.cs) 两处（+13/-2，唯一代码改动）：
1. **新增显式接口实现** `bool IPathGrid.IsWalkable(GridCoord subCoord) => IsSubWalkable(subCoord);`——A* 经接口拿到微格语义（sub→cell 映射+障碍判定），与 PathFollower.FollowNext 动态阻挡判定同语义；宏格 `IsWalkable` 公开方法**原语义保留**（IsFootprintClear/建造校验等 cell 级消费方零影响）。
2. **IsDiagonalMoveAllowed 防穿角改 `IsSubWalkable(a)&&IsSubWalkable(b)`**——入参同为微格坐标，原宏格查表同越界问题（该法仅 A* 消费，全库 grep 实证）。

## 四、回归结果（三冒烟+行为级复跑全绿）

| 回归项 | 结果 | 证据 |
|--------|------|------|
| 真实进局链复跑（修复后） | ✅ | 37 单位 600 帧：**Failed=0 / Following=22 / Repathing=5**，state=Playing 无 GameOver；PathFailed 刷屏消失；游走目的地恢复正常坐标（(0,0) 消失） |
| AI 生产链（行为级） | ✅ | `[TaskScheduler] 完成 Production 任务 → npcId 2/18/22/23`（寻路→作业→完成闭环）；调度中心搬运任务正常派发；ChainPatrol 持续揭雾（单位在走） |
| Smoke_14（2_17 步骤14） | ✅ | `[2_17_14冒烟] ===== ALL PASS（P1~P6 + #9/#12）=====` |
| Smoke_2_13_C | ✅ | `[2_13_C冒烟] ===== ALL PASS（P1~P7） =====` |
| Smoke_2_13_D | ✅ | `[2_13_D冒烟] ===== ALL PASS（P1~P6） =====` |
| 编译 | ✅ | 0 error（16 warning 全为存量，非本次引入） |

**sim-sync 分级：F 级（不涉）**——全库 grep 实证 IPathGrid/AStarSolver 仅 Unity 侧 PathfindingService/PathFollower 消费，AI.Core 决策核/sim 无寻路网格依赖；未触 TuningSnapshot/ProfessionSnapshot/FactorContext/champion/SO，无 sim 补实现义务。

## 五、附带发现与待裁决事项

1. **【次生发现·请裁决是否另批】出生点落不可走格口袋+A* 不校验目标格**——seed=20260901 实测 1/9 玩家居民（npcId 29）出生环落在 flags=None 不可走 patch（cell 126,119，自身/4 邻全不可走），且 A* 从不校验目标格可走性→单位可被游走目标引导**走进**不可走格→永久困死（每 12-20s 重试刷 PathFailed）。建议拆两小项：a) 出生点最近可走微格吸附（PopulationSystem 域）；b) A* 目标格 snap/校验（2_6 P0b 服务化域）。**本批未动**（超寻路1 边界，涉出生链行为变更）。
2. **【观察登记】SceneHomePointProvider 非Monster 一律返回玩家城堡锚**——AI 王国工人 HomePoint=玩家城堡（本次探针实证 AI 工人 homePt=(0,81.92)）。王国语义 HomePoint 归 2_15/2_16 域，不影响本批，建议挂账。
3. **【ThroneAnchor 瞬时态宽限缺失·低危挂账】**「Playing 态+0 玩家桶工人」首轮轮询即 GameOver（本次由排查工装帧空窗触发）。真实流程同帧连续调用不触发；但若未来出现异步刷兵/读档时序改动即成雷。建议加"开局 N 秒宽限/人口就绪事件门控"，归 2_12 域，待裁决。
4. **【测试残留登记】** 排查用存档槽 `smoke_path1` 已被伪影局 GameOver 标记"已结束"，仅测试工装可见，不影响正式流程。

## 六、git 在场（不 commit，策划端代执）

- 本批唯一代码改动：`Valley Rampart/Assets/_Game/Systems/Grid/GridSystem.cs`（+13/-2，diff 已实读核对纯净）；
- 工作树另有 `pixel-forge/server/occlient.mjs`、`serve.mjs`、`.tmp-models.mjs`（未跟踪）三处改动——**非本批产物**（疑似并行美术/工装会话产物），不纳入本批 commit 范围；
- 队列 `_任务队列.md` 未动（单写者纪律）：请策划端验收后更新「寻路1」行清偿。

## 七、下一步建议

1. 策划端验收本报告 → 代执 commit（建议 `fix(寻路1): IPathGrid.IsWalkable 微格契约修复——A* 全图不可达根因清偿（HH.47）`）；
2. §五-1/2/3 三项按域分流挂账池；
3. 「寻路1」清偿后 Q10 阻塞解除，可回主队列推进。
---

## 八、策划验收（2026-09-01 策划端实盘复核回写；验收成立）

> **实盘复核记录**：修复 diff 实读=声明逐字吻合（显式接口实现 `IPathGrid.IsWalkable => IsSubWalkable` C# 显式优先分发+宏格公开方法保留=cell 级消费方零影响，正统做法；IsDiagonalMoveAllowed 微格化）；AStarSolver L47 `grid.IsWalkable(nb)` 微格入参消费点+头注释「输入：IPathGrid（微格坐标）」+GridSystem.Width/Height=`_w×subCellDivisor` 接口宽高微格域三重佐证；**盲区检查通过**：GetEnterCost 恒返 1.0 无查表（无同类越界残留）、IsSubWalkable 实现正确（SubToCell+IsObstacle）；回归充分（真实进局 600 帧 Failed=0/Following=22+AI 生产闭环+三冒烟 ALL PASS）；sim-sync 不涉判定采信（AStarSolver/IPathGrid 仅 Unity 侧消费实证）；pixel-forge 四文件+3.1.3 一行=并行美术/工装会话产物不纳入确认。

| 决策点 | 裁决 |
|--------|------|
| §五-1 出生困死+A* 目标格 | **寻路2 插队（Q10 前，用户拍板）**：npcId 29 实测 1/9 居民永久困死为玩家可见缺陷+AI 出生同链共用 PopulationSystem——AI 单位踩中同样困死则 Q10 行为探针（AI 生产闭环）偶发假失败。范围=出生点最近可走微格吸附（PopulationSystem 域，必做）+A* 目标格 snap/校验（若实现简单一并，复杂则登记 2_6 P0b 域） |
| §五-2 SceneHomePointProvider | **照裁挂账**：AI 工人 HomePoint=玩家城堡（王国语义缺失），归 2_15/2_16 域，挂账池登记 |
| §五-3 ThroneAnchor 瞬时态 | **照裁挂账**：「Playing+0 工人」无宽限（低危，异步刷兵/读档时序改动即成雷），加开局 N 秒宽限/人口就绪事件门控归 2_12 域，挂账池登记 |
| 验收结论 | **寻路1 成立+Q10 寻路侧阻塞解除**（输入1/寻路2 仍在队）；根因定位质量嘉奖：四步证据链完整+工装伪影（帧空窗触发 ThroneAnchor 伪 GameOver）诚实归因并反证（活体 37 存活 0 死亡）——让渡归因三问的正面示范；测试残留 smoke_path1 槽工装可见无碍（§五-4 知情） |
