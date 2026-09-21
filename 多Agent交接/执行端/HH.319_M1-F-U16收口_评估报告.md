# HH.319 · `M1-F` · `U-16`「`WaterHaul` 派而不执行」收口 · 只读评估报告 ＋ 报裁

- **日期**：2026-09-21 ｜ **端**：执行端（TraeCode）｜ **性质**：**只读评估**（⛔ `Assets/**` 一行未动 · 含 Editor）
- **上游**：`D813`（探针批 `29c6aeaa` 验收）⇒ `U-16` 归因「甲′」已证 ＋ 细分两模式；本批＝**在已证归因上定修法**
- **红线自检**：`Assets/**` 零写 ✅ ｜ ⛔ 不开工 ✅ ｜ ⛔ 不 push ✅ ｜ ⛔ 未代提交策划端账本 ✅ ｜ ⛔ 未碰 `U-17`／`MonsterAI`／`FormationBrain`／`SelectionController`（仅核波及）✅ ｜ 本报告自行 commit
- **结论速览**：Q-A 判 **走 `:89`（但须先修写权）**；Q-B 报选 **甲**（并用**新发现**证明其实现路径**现成**）；⭐ 新增 2 条机制级读数（`IsKingdomTaskWorker` 恒 `false`／任务刺激**同强度＋先入者胜＋续命降权**）与 1 条 A\* 起点读数，**共同改写甲′-b 的修法落点**

---

## 一 · 复核：本端已取证的事实（逐条给判）

| # | 本端主张 | 复核判 | 依据 |
|---|---|---|---|
| 1 | `dest` 被两来源交替覆盖 | ✅ 坐实 | 探针批落盘 `[HH319U16·seq]`：npc35 `dest=(-27.62,31.32)` → `(-6.00,40.96)` → 末值 `(6.00,40.96)`；`task` 仍为 `WaterHaul`(`source=水井`) |
| 2 | `SetDestination` 写入方 ＝ **5 方 / 12 调用点** | ✅ **逐点核对一致** | `BehaviorExecutor.cs:339`／`FormationBrain.cs:166`／`TaskScheduler.cs:395`／`MonsterAI.cs:49,76,100,113,137,149,174`（7）／`SelectionController.cs:265,302` ＝ **12** ✔ |
| 3 | 形参 `priority` **预留未实现** | ✅ **坐实** | 定义 `PathFollower.cs:42` `SetDestination(Vector2, byte priority = 0)`；实体内 `:42-52` **零读取 `priority`**，且 `RequestPath()`/`SetPath()`/`FailOnce()` **均不接收它** ⇒ **全库无消费方** |
| 4 | `BehaviorExecutor` ＝ 命令执行者（不自决定目标） | ✅ 坐实（＋**本端补全上游**） | `ExecuteWorkAt:184 NavigateTo(cmd.TargetPos, …)` → `NavigateTo:333-340 SetDestination`；`cmd` 来源＝`L3CommandComputer.Compute(in PostureDecision, in FactorContext)`（`AI.Core/Decision/L3CommandComputer.cs:22-33`，`case MoveTowards: cmd.TargetPos = posture.Focus.TargetPos`）⇒ **目标由上层焦点决定** |
| 5 | `06:75-106` 状态机契约明文 | ✅ 逐行坐实 | `06_任务层.md:87` 工人死／`:88` 工人被抢⇒挂起／**`:89` 到不了⇒解绑＋标记不可达（防死循环）**／`:95` 超时⇒强制放弃回 [1]；另 `:118` 异常表同款（标 **❌ 新增**）· `:169` 待议 #3「`retry` 默认次数与"不可达"的冷却」 |
| 6 | `Abandon:630-642` 零日志、零标记 | ✅ 坐实 | `TaskScheduler.cs:630-642` 仅 `IsKingdomTaskWorker=false` ＋ `RemoveTaskStimulus` ＋ `ClearNpc`；对照 `Complete:606` **有** `[TaskScheduler] 完成 …` |
| 7 | `05:207`「真跑通的交互只有一种」 | ✅ 逐字坐实 | `05_交互层.md:207`「真跑通的交互**只有一种** —— `WorkAt → ExecuteWorkAt → HarvestCarry()`（走过去 + 搬走）」 |

### ⭐ 一-A · 三条**本端新增**读数（决定修法落点 · ⛔ 上轮未取）

**N1 ⭐⭐⭐「让位机制已存在，但被架空」——`NPCBrain.IsKingdomTaskWorker` 恒为 `false`**

```477:484:Valley Rampart/Assets/_Game/Systems/AI/NPCBrain.cs
        // Execute 每帧调用（用最近一次 Think 产出的 cmd，持续移动）
        // 王国任务工人（T-K/T-R）：移动由 WorkerTask 独占，跳过普通 Executor 移动，
        // 否则 Working 时工人被普通决策核（wander/逃跑）拉离采集点（对齐 SimBrain.IsKingdomTaskWorker）。
        // 注意：Think 仍跑（保证 ThreatFactor 刷新），只跳过移动 Execute。
        if (_executor != null && !IsKingdomTaskWorker)
        {
            _executor.Execute(in _lastCmd, Time.deltaTime, GetCellSize());
        }
```

- 全库该字段写入点**只有 3 处，全部写 `false`**：`TaskScheduler.Complete:602`／`Abandon:638`／`NPCBrain.ResetForReuse:249`；其余只是 6 个 prefab 的序列化 `0` 与 1 个读点。
- ⇒ ⛔ **无一处写 `true`** ⇒ `:481` 的"让位"分支**永不生效** ⇒ **Executor 每帧都在写移动**。
- ⇒ ⭐ **候选甲不是"新增机制"，而是"恢复被架空的历史机制"**：`TaskScheduler` 头部注释 `:16-17` 自陈退役理由＝「本类**不设置** `brain.IsKingdomTaskWorker=true`（靠 `TaskStimulus` 让 NPC 移动，Executor 消费，**移动独占不再需要**）」—— ⭐ **D813 恰好证伪了该前提**（刺激驱动的移动**会被抢**）。

**N2 ⭐⭐⭐「任务刺激同强度 ＋ 先入者恒胜 ＋ 续命反被降权」**

```268:280:Valley Rampart/Assets/_Game/Systems/AI.Core/Decision/AttentionSystem.cs
        // TaskStimulus（struct，应用 stateTaskDiscount）
        for (int i = 0; i < _taskStimuli.Count; i++)
        {
            float eff = _taskStimuli[i].Intensity * _taskDiscount;
            if (eff > bestIntensity)      // ⚠️ 严格大于 ＋ 按列表序 ⇒ 同强度时【先入者胜】
            {
                bestIntensity = eff;
                var ts = _taskStimuli[i];
                best = new Focus(AttentionLayer.Task, ts.Position, ts.Intensity, ts.Source,
                                 ts.FocusType, ts.TargetPos, ts.Intensity);
```
- `AddStimulus(TaskStimulus) => _taskStimuli.Add(s)`（纯追加 · 列表序＝注入序）；`RemoveTaskStimuli(source)` 按 `Source` 引用移除。
- `KingdomTask.intensity` **默认 `1f`**（`KingdomTask.cs:33`），全库 `new KingdomTask(type, source)` **均走默认** ⇒ ⭐ **所有任务刺激强度恒等 1.0**。
- ⇒ **任务层内比较退化为"列表序"**（⛔ `Priority` 在 `SelectTopTaskLayer` 中**完全未参与**；`priorityWeightS/A/B/C` 只用于"甲层评分"，非本处）。
- ⇒ ⭐⭐ 且 `InjectStimulus`（`:368-379`）每次都 **`RemoveTaskStimulus(source)` ＋ `AddTaskStimulus(...)`** ⇒ **在册任务每 Tick 续命时把自己的刺激挪到列表尾** ⇒ **在册任务刺激系统性排在其它刺激之后** ⇒ 其它同强度刺激**恒胜**。
- ⇒ ⭐ 这解释了 D813 的 `dest` 覆盖读数**为何是"稳定偏向别处"而非"随机抖动"**，也解释了 `甲′-b`（健康工人 30 s 到不了）。

**N3 ⭐⭐「A\* 起点不可走 ⇒ 恒 `Unreachable`」**

```43:57:Valley Rampart/Assets/_Game/Systems/Pathfinding/AStarSolver.cs
            var offs = GridMathCore.NeighborOffsets8;
            for (int k = 0; k < offs.Length; k++)
            {
                GridCoord nb = new GridCoord(current.x + offs[k].x, current.y + offs[k].y, current.layer);
                if (!grid.IsWalkable(nb)) continue;
```
- `Solve` 把 `from` 直接入 open，扩展时**只校验邻居**；若**站位格与其 8 邻全不可走** ⇒ open 耗尽 ⇒ `MakeResult(Unreachable, …)`。
- 且 `PathfindingService.cs:30` 注释钉死「**起点不 snap** —— 路径起点必须=单位实际位置，防瞬移语义」。
- ⇒ ⭐ 与 D813 实测吻合：npc34 `站位格可走=False（地形不可走）` ＋ `A*复演=Unreachable`（**恒定**）⇒ **该单位永久失去一切寻路能力**（不止 `WaterHaul`）。
- ⚠️ 且 `NavigateTo:342` 的直线兜底只在 `_pathFollower == null` 时生效（`:335` 会自动补挂 ⇒ **兜底恒不可达**）。

---

## 二 · Q-A 判定：走 `06:89` 还是 `06:95`？

### 判：**走 `:89`**，但 **⛔ 顺序必须是"先修写权 ⇒ 再谈分流"**

**理由（三条，逐条可验）**

1. **契约意图**：`:89`「到不了（路径失败）⇒ 解绑 **＋ 标记不可达（防死循环）**」是契约作者为"不可达"预留的**解药**；`:95`「超时 ⇒ 强制放弃 ⇒ 回 [1]」**无任何防循环条款** ⇒ 单靠 `:95` **必然**循环（现状即此，D813 已量化 30.4~31.5 s/轮）。
2. **现象归属**：从工人视角，他的 30 s 全部花在"朝别处走"，**从未真正朝井走** ⇒ 客观结果＝"到不了"⇒ 语义落 `:89`，不落 `:95`（`:95` 是"已在工作中推进到超预算"的语义，其前置是 `[3] 执行中`，而本症**卡在 `[2] 已派`**）。
3. ⚠️ **但严格讲，`:89` 也覆盖不全**：`:89` 的前提是"**路径失败**"（A\* 判不可达）；而 `甲′-b` 的 A\* **返回 `Ready`**（D813：健康工人 `A*复演=Ready`、`fails=0`）⇒ 它既不是"路径失败"也不是"工作超时"，而是 **"被同层别的任务刺激抢走焦点"** ⇒ 契约 `:88`「⏸ 工人被抢」**只管威胁挂起（`OnThreatSusp`）**，⛔ **不覆盖"被同层任务刺激抢"** ⇒ ⭐ **这是契约的真实缺口**（见 Q3）。

**⇒ 落点判**：
- 修好写权（Q-B）后，`甲′-b` 应**结构性消失**（工人按任务路径走）；若仍超时，`:95` ＋ `retry` 上限（`06:118`）兜底即可，⛔ 无需新条款。
- `甲′-a`（起点不可走）⇒ **判 `:89`**，且必须**补齐"标记不可达"**（顺带核 1：未落地）。
- ⛔ **不建议**把 `甲′-b` 也塞进 `:89`：那会把"刺激仲裁"问题伪装成"寻路问题"，掩盖 N2 的真根因。

---

## 三 · Q-B 报选：`dest` 写权四案（逐案给正确性／代价／副作用）

| 案 | 内容 | 能修 `甲′-a`？ | 能修 `甲′-b`？ | 代价 | 副作用／波及 | 判 |
|---|---|---|---|---|---|---|
| **甲** | **任务层优先**：派工期间执行层让位（**Executor 检查"本工是否有在册任务 ⇒ 不发移动命令"**） | ⛔ 不能（起点不可走与写权无关） | ✅ **能**（`dest` 不再被覆盖） | ⭐ **极低**：`IsKingdomTaskWorker` 复位代码**已在**（`Complete:602`/`Abandon:638`），只需在 `Dispatch:352-366` 补一次 `true` ⇒ **净新增 ≈1~3 行** | ⚠️ **两种实现粒度必须分开看**：<br>**甲-a（整块跳过 `_executor.Execute`）** ⇒ ① 连带跳过 `ExecuteWorkAt → HarvestCarry()`（`05:207` 唯一跑通交互）⇒ 与 `ScheduleCenterStub` 链**窄重叠**；② 任务在册期间**威胁逃逸失效**（`MovingToSource` 无威胁挂起，`UpdateAssignedTasks` 只在 `Working` 查 `ThreatFactor`）。<br>**甲-b（只在 `NavigateTo` 粒度让位：写入前判"在册任务 ⇒ 不写 PathFollower"，其余到达检测/`WorkAt`/威胁照旧）** ⇒ ✅ 两个副作用**都消解** | ⭐ **报选甲，且必须取"甲-b 粒度"** |
| **乙** | 执行层优先（现状）⇒ 任务层**不再自己 `SetDestination`**（只做刺激） | ⛔ | ⛔ **不能** | 最低（删 1 段） | ⚠️ 只是把"dest 被覆盖"换成"**焦点不稳**" —— N2 显示在册任务刺激**同强度＋排尾** ⇒ 恒输；且 `NavigateToSource` **删掉后连"最后一根保底"也没了** ⇒ 判据 3 只会更容易红 | ⛔ **不建议单用** |
| **丙** | **优先级仲裁**：启用 `priority` 形参（任务层传 `>0`、Executor/编队/玩家/怪物传 `0`，`PathFollower` 拒绝更低者） | ⛔ | ✅ 能 | 中：需在 `PathFollower` 增"当前优先级 ＋ 拒绝写"状态；**需为 5 方定义档位值** ⇒ 编队／玩家手动／怪物语义**都要裁**；⚠️ 还需设计"被拒时**不计** `FailOnce`"（否则被拒写入仍会 `_consecutiveFails++` ⇒ 制造假失败） | 波及 `FormationBrain:166`（编队整体移动）／`SelectionController:265,302`（玩家手动；⚠️ 现状本就被 Executor 每帧覆盖＝"保底"语义名不副实）／`MonsterAI` 7 点（**怪物走自己的行为链** ⇒ 建议整域豁免，见下） | ⭐ **次选**（与甲**语义等价、代价更高**；但它是唯一能顺带统一"编队/玩家/怪物"的通用口 —— 建议**留作后续**而非本批） |
| **丁** | 任务层独立移动通道（不复用 `PathFollower` / 独立 `_taskDest`） | ⛔ | ✅（绕开） | 高：与"`Executor` 是唯一移动执行者"的架构冲突 | ⚠️ 双通道并存会引入"两套到达判定"（`TaskScheduler.ArrivalThreshold` vs `BehaviorExecutor.arrivalThreshold`）⇒ 新分裂 | ⛔ **不建议** |

**与"怪物走自己的行为链"（`06:50` ⛔ 不委托）的冲突核查**：`MonsterAI` 的 7 个点**只操作自己的 `_pf`**（自身单位），⛔ 不写工人的 `PathFollower` ⇒ **与甲/丙不冲突**；若走丙，建议按 `06:50` 口径**整域豁免**（怪物不参与仲裁）。

**与编队的冲突核查**：`FormationBrain:166` 只对 `general`（将军）写 ⇒ 工人不在编队槽位时**零交集**；⚠️ 若将军同时被派任务（`CountAssignedWorkers` 路径）才会撞 ⇒ 概率低，列报即可。

---

## 四 · Q-C：`BehaviorCommand` 链 ＝ 与 `InjectStimulus` **同一条链的两个节点**（⛔ 非两条独立链）

```
[TaskScheduler.InjectStimulus]   ← 出"题"（TaskStimulus，Layer=Task, FocusType=WorkPosition）
        ▼
AttentionSystem.SelectTopTaskLayer  ← 选焦点（⭐ N2：同强度先入者胜 ＋ 续命把在册刺激挪到队尾）
        ▼
L2 三维表 / L3CommandComputer.Compute   ← cmd.TargetPos = posture.Focus.TargetPos
        ▼
NPCBrain.Update:483 _executor.Execute(cmd)   ← **每帧**（Think 才分片；Execute 不分片）
        ▼
BehaviorExecutor.ExecuteWorkAt:184 → NavigateTo:333-340 → **PathFollower.SetDestination（每帧写）**
```

- ⇒ ⭐ **`target` 的产出方 ＝ 注意力选出的焦点**（`L3CommandComputer.cs:32`），⛔ 不是 Executor 自己决定。
- ⇒ ⭐ 而 `TaskScheduler.NavigateToSource:395` 是**绕过上链的旁路直写**（每次派发仅 1 次）。
- ⇒ **谁在和谁争**：**主链（每帧）** vs **旁路（一次）** —— 且主链的焦点**可能根本不是这个任务**（N2）⇒ 旁路写完当帧即被主链覆盖 ⇒ 与 D813 读数完全一致。
- ⚠️ 补：`ScheduleCenterStub` 是**第三条**注入方（`issuer=StorageComponent`，⛔ 不进 `_npcTaskMap`）⇒ 它**只走主链**，其"到达即 `HarvestCarry`"依赖 `ExecuteWorkAt` ⇒ ⭐ **这正是甲-a 粒度会打折它的原因**。

---

## 五 · 顺带核（承 `D813` · 只读）

1. ⛔ **`06:89`「标记不可达」全库未落地**（已核）：`_Game` 内 `不可达` 全部命中均**无关** —— `MapValidator.cs:14/92`（地图连通性）／`MapGenRules.cs:326/630/1373`（分区兜底）／`PathTypes.cs:13`（`PathStatus.Unreachable` 枚举本体）／`AStarSolver.cs:60`（注释）／`MineByproductComponent.cs:207`（历史注释）／`PlacementScorer.cs:84`（评分注释）／`UnitController.cs:685`（注释）／**`NPCBrain.cs:378`（兜底告警日志，且对 `Worker/Porter` 直接 `return`）** ⇒ **不存在任何"不可达标记/冷却表"数据结构** ⇒ ✅ **未落地坐实**。
2. **`Abandon` 加日志**（`D812` 已裁"必做"）—— ⛔ 本批只报落点：`TaskScheduler.cs:630-642`；建议**同时加 `reason` 形参**，取值 `Unreachable` / `Timeout` / `SourceInvalid` / `BrainLost` / `Dead` / `External` / `Stale` ⇒ 改签名 **1** 处 ＋ 调用点 **5** 处（`:184` `AbandonTask` ／ `:191` `OnNpcDied` ／ `:204` `OnPathFailed` ／ `:218` `OnBuildingDied` ／ `:591` `UpdateAssignedTasks` 的 stale 循环）＋ 新日志 **1** 行；⭐ 此改动**正好是把 `:89`/`:95` 分流落地的前提**。
3. **D-1 / D-2 与本批边界** ⇒ ⛔ **本批不含**（详 Q 后附）。D-1 会**降低派发密度** ⇒ 与"修后复跑"的对照读数冲突（与 `U-17` 同理）；D-2 已被 `D813` 裁"须晚于 `U-16`"。
4. ⚠️ 历史同族先例（⭐ 强烈建议施工时参考）：`MineByproductComponent.cs:206-207` 注释自陈 —— 「禁用 `NearestWarehouse`：其解析落 Vault 子仓物理中心=主城占格中心（`isObstacle`，AI 城另有围墙环）→**工人永不可达=搬运死循环（HH.107 十轮实证）**」⇒ **项目对"到不了"的既有解法 ＝ 修正目标坐标使其可达**（`TreasuryGatePos()`），⛔ 而非黑名单标记。⇒ 对 `甲′-a`：目标已由 `SetDestination:44` 内部 `SpawnPosSnapper.SnapWorld` 吸附（`dest格可走=True` 已证），**病灶在"起点"不在"目标"** ⇒ 对应修法应针对**落位/自救**（见 Q4）。

---

## 六 · 报裁（Q1~Q5）

### Q1 `Q-A` 判定 —— ⭐ **走 `:89`**（＋补"标记不可达"），**但顺序 ＝ 先修写权**

- 建议①：**先修 `dest` 写权**（Q2），否则改 `:89`/`:95` 都只是"换一种循环"。
- 建议②：`甲′-a` ⇒ `:89`（**必补"标记不可达"**：⭐ 建议取"**源侧／点侧冷却**"形态 —— 与 `MineByproductComponent` 先例同精神，标"该任务点对该工人不可达 N 秒"，⛔ 不标"工人永久不可用"）。
- 建议③：`甲′-b` ⇒ **不落 `:89`**（其 A\* 为 `Ready`，非路径失败）；修好写权后自然消解；残留超时走 `:95` ＋ `retry` 上限。
- 代价：0（判定本身）；落地代价见 Q2/Q4。

### Q2 `Q-B` 报选 —— ⭐ **甲（且必须是「甲-b 粒度」：`BehaviorExecutor.NavigateTo` 级让位）**

- 理由：① 修 `甲′-b` 有效且**净新增代码最少**（复位代码已在）；② ⛔ 避开甲-a 的两个副作用（打断 `05:207` 唯一跑通交互 ／ 威胁逃逸失效）；③ N1 证明这是**恢复被架空的历史机制**，⛔ 非新增架构。
- ⚠️ 前提：`Dispatch` 置位／`Complete`·`Abandon` 复位必须**成对**（否则重演 `LC-N2` 工人永久瘫痪）。
- 次选 **丙**（优先级仲裁）：语义等价、代价更高，但**唯一能统一"编队/玩家/怪物"**的一口 ⇒ 建议**留作后续批**（且需先裁 5 方档位值）。
- ⛔ **乙/丁不建议**。
- 代价：甲-b ≈「`NavigateTo` 前加 1 个判定 ＋ `Dispatch` 置位」，**1 文件 2 处量级**；✅ **不牵动** `06` 契约（属 L2↔L3 执行细节，`06` 未管）。

### Q3 修法是否需动契约 —— ⛔ **不需要新增"不可达"条款（已够用）**；⭐ **但需补 1 句"同工双任务"仲裁**

- ✅ **够用**：`:89`（不可达＋防死循环）／`:118`（异常表：解绑＋标记不可达，`retry` 用尽⇒终止）／`:169` 待议 #3（`retry` 次数与"不可达"冷却）⇒ **"到不了"的语义与防循环要求契约已写明** ⇒ 本批**不必改契约**，属"**实现欠账**"（顺带核 1）。
- ⭐ **须补的一句**（缺口坐实于 N2）：`06` §四 `[2] 已派` 分支旁补 ——
  > **「同一工人同一时刻只应有一个活跃任务刺激；在册任务的刺激不得因重注而降权，其它任务源在在册期内让位」**
  ⚠️ 该句落点须裁：**落 `06`**（任务层内竞争，符合"任务层管住过程"的边界）／落 `03`（注意力层）。本端建议**落 `06`**（`05`/`06` 是任务链的边界文档，注意力属实现细节）。

### Q4 `甲′-a` 与 `甲′-b` 是否分开修 —— ⭐ **必须分开，且顺序 ＝ 先 a 后 b**

- **不可合修的理由**：① 根因互斥（a＝"出发点非法"· 位面 ／ b＝"写权冲突"· 权面）；② ⛔ **只修 b ⇒ npc34 仍困死 ⇒ 判据永远红**（靶例恒不可达）；③ ⛔ **只修 a ⇒ `dest` 仍被覆盖 ⇒ 健康工人照样 30 s 超时**（`甲′-b` 不变）。
- **拆分与归属**：
  - `甲′-a-1`「**探针 `SpawnWorker` 未吸附**」（D813 已证：`站位格可走=False（地形不可走）`）⇒ ⛔ **探针侧**，属**本批判据可信度的前提** ⇒ 建议**先修**（否则靶例永远不可达）。
  - `甲′-a-2`「**单位落位不可走 ⇒ 永久失去寻路能力**」（非探针工人 npc3/9 站城堡格 · D813 三跑 7 人 · PathFailed 4022~28496）⇒ ⭐ **生产侧独立立项**（影响面远超 `WaterHaul`）。
  - `甲′-b`「`dest` 写权冲突（＋ N2 刺激降权）」⇒ **本片正题**。
- 代价：拆三段施工，但每段**判据独立、可分别留档**；合修则**无法归因**（这正是 `U-16` 前两轮反复的根因）。

### Q5 是否新增观察项/缺陷编号 —— ⭐ **建议新增 2 项，`U-16` 收口**

| 新编号（提议 · ⛔ 待策划端取号） | 内容 | 严重度理由 |
|---|---|---|
| **`U-18`** | 「`06:89` **标记不可达未落地** ＋ 单位站不可走格无自救 ⇒ 不可达单位**永久空转**」 | 三跑 7 名工人 · PathFailed **4022~28496** ⇒ 影响面远超 `WaterHaul`（含 `Production`/`Transport` 全任务类型） |
| **`U-19`** | 「注意力任务层**同强度先入者胜 ＋ 续命重注降权** ⇒ 在册任务刺激系统性输给其它任务源」（N2） | 是 `甲′-b` 的**机制根因**，独立于写权；不修则 `WaterHaul` 修好后仍可能被别的任务抢 |
| **`U-16`** | ⭐ **可收口**（归因已证 · 两模式已分离）⇒ 余下＝"修法执行"，建议挂 `U-18`/`U-19` 两个施工批下 | — |
| **`Abandon` 加日志＋`reason` 分流**（`D812` 已裁必做） | 建议**并入 `U-18` 施工批**（同一文件同一方法） | 是 `:89`/`:95` 分流的前提 |

**⭐ 建议施工序（本端建议 · ⛔ 待裁）**：`探针 SpawnWorker 吸附`（a-1）→ `U-18`（a-2 ＋ `Abandon` 日志/reason）→ `U-16 写权修复`（甲-b）→ 复跑留档 → `U-19`（刺激仲裁/契约补句）→ `D-1` → `D-2`。

**本批边界确认**：⛔ 不含 `D-1`（去重键错配）／⛔ 不含 `D-2`（广告次序短路，已裁须晚于 `U-16`）／⛔ 不含 `U-17`（优先级漏配 ⇒ 降派发频率 ⇒ 污染读数）；⛔ `MonsterAI`/`FormationBrain`/`SelectionController` 现有行为**一行不动**（仅核波及）。

---

## 七 · 未完成项与风险

1. ⚠️ **N2 的"先入者胜"未做逐帧实证**：本批为**静态码读 ＋ D813 落盘读数一致性**推断 ⇒ 若要钉死，需一条 attention 探针（打印该工 `_taskStimuli` 列表序 ＋ 当前焦点 `Source`）⇒ ⛔ 建议并入 `U-19` 的取证，而非本批。
2. ⚠️ `BehaviorExecutor` 的**写入频率**未精确钉死（D813 经验值 `_consecutiveFails` 增长 ≈1.0~1.4/**帧**；`Think` 为 10Hz 分片、`Execute` 每帧 ⇒ 量级一致但成因未逐点核）⇒ 列报待补。
3. ⚠️ 甲-b 落点需先确认「**"在册任务"查询口**」的可用形态（`TaskScheduler` 为单例，`BehaviorExecutor` 现为零引擎依赖的纯类 ⇒ ⚠️ 直接取单例会**破坏 `AI.Core` 接缝纪律**）⇒ ⭐ **建议改由 `NPCBrain` 注入布尔**（`IsKingdomTaskWorker` 的衍生态），⛔ 不让 `Executor` 直连单例。此点须在施工任务书中钉明。
4. ⚠️ `06` 契约补句（Q3）属**文档面** ⇒ 本批 ⛔ 未动文档，须策划端裁决后再由相应批落地。
