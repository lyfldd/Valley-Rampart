# HH.315 · `M1-C` · `U-8` 只读评估 —— 交付验收裁决

- 裁决日：2026-09-21（`D800`）
- 被裁文书：`多Agent交接/执行端/HH.315_M1-C-U8评估_评估报告.md`
- 本轮性质：**评估验收 ＋ 口径裁决**（⛔ 本端不碰代码）
- 验收方式：判据三直读（合同原文 `09` §16.1~§16.3 ／ 代码 `file:line` 实读 ／ 档位字段直读）
- 总判：✅ **评估通过 · 建议全采** —— ⚠️ **含 1 条对本端 `D799` 判断的成立勘正**（本端认责）＋ 1 条新增后果补强

---

## 〇 验收集（本端独立取证 9 项）

| # | 本端实读 | 结果 |
|---|---|---|
| 1 | `Building.cs:1349-1350 IsValid` 真值 | **三项 OR** ⇒ 报告勘正成立（见 §一） |
| 2 | `Building.cs:1182 TakeDamage` 首守卫 | `state != Active ⇒ return` ⇒ 拆除中（仍 Active）**照常受伤** ✅ |
| 3 | `Building.cs:1197-1212 EnterRuined` | `:1199 state = Ruined` ＋ `:1207 Unregister(this)`；⛔ **不清 `_demolishing`**；⛔ **不注销工地仓源**（本端补） |
| 4 | `Building.cs:848-857 Demolish` ＋ `:514-523 StartRebuildFromRuins` | 双向皆不清 `_demolishing` ⇒ **闩锁无解** ✅ |
| 5 | `TaskScheduler.cs:152-159 CountAssignedWorkers` | 源级、⛔ 无 state/args 过滤 ⇒ `E2` 成立 ✅ |
| 6 | `BuildingPanel.cs:210-211 OnEnable/OnDisable` | 只订阅 `BuildingUpgraded`／`UnitDied` ⇒ ⛔ **无 `BuildingRuinedEvent`** ⇒ `E3` 边角可达成立 ✅ |
| 7 | `BuildingFactory.cs:189-193` ＋ `:292` ＋ `:155` | 存档带 `state`（`SaveState:1037`）⇒ `initialState` 真恢复 ⇒ **§六 缺口成立** ✅ |
| 8 | `KingdomTask.cs:94-97 DemolishTaskArgs` | ⭐ 有 `public Building target` ⇒ **`Q4` 活读形态可行** ✅ |
| 9 | `CanDemolish` 全库消费面 | 3 处（定义 `:840`／`Demolish:850`／`BuildingPanel:171`·`:393`）⇒ 加 state 判据**安全**（见 §五 `E3`） |

---

## 一 · ⭐ 本端自纠（落点 B）—— 报告的勘正成立，错在本端

本端 `D799` 派工提示词写：

> 「`IsValid` 不含 `Ruined` ⇒ 即使放宽注册守卫，`TaskScheduler.Tick:223` 仍会把 Ruined 源清出 `_sources` ⇒ **完整修法必须同时改状态集**」

**实读 `Building.cs:1349-1350`**：

```csharp
public bool IsValid => this != null
    && (state == BuildingState.Active || _awaitingMaterials || _demolishing);
```

**三项 OR** ⇒ 只要 `_demolishing == true`，**任意 state 下 `IsValid` 均为 true**。

⇒ ⭐ **报告 §1.3 的勘正完全成立**：
- `Ruined + 拆除中 ⇒ IsValid = true` ⇒ `Tick:223` **不会**清它 ⇒ **`Ruined` 无需补**
- 「`Ruined` 可拆但 `IsValid=false`」只在**尚未开拆**时成立 —— 而那时它**本来也不该在册**（`EnterRuined:1207` 已显式注销）
- 真缺口在另一格：**「施工中（料齐）」`IsValid=false`**，且**只惩罚案乙**（案乙在进入投料态即注册 ⇒ 料齐瞬间 `_awaitingMaterials=false` ⇒ 下 tick 被清 ⇒ 其注册是一次性假象）
- ⇒ **案甲不需要动 `IsValid`**（注册发生在 `_demolishing=true` 之后，天然自足）

**错的性状**：本端把「`Ruined` 未拆除时 `IsValid=false`」这个**局部真值事实**，直接外推成「修法必须动 `IsValid`」的**机制结论** —— ⛔ 没列真值表、没看 `_demolishing` 那一列。

⇒ 立 **`L-52`**：**判定「某状态集有缺口」前必须列真值表（含全部参与项）**，⛔ 不得只看"目标状态缺失"那一格 —— 多参与项 `||`／`&&` 表达式中，单一维度的缺失**常被另一维度掩盖**（本端把 `_demolishing` 的掩盖当成了缺口）。

---

## 二 · 报告成立项（全采）

| 项 | 本端核 | 说明 |
|---|---|---|
| `A1` 注册守卫 `Building.cs:1445` | ✅ | 逐字一致；唯一调用点 `:720` |
| `A2` `TaskScheduler:95` 同型守卫 | ✅ | 坐实；**准「不同批」**（见 §四 `Q2`） |
| `C` `Demolish()` 不注销工地仓源 | ✅ | ＋**精确化认可**：「双源同册」当前仅「升级·投料中拆除」可达（首次建造/重建本体根本不在册）⇒ **修完 `U-8` 才成普遍面** ⇒ **必须同批** |
| `D` 工时错配（2s vs 6s） | ✅ | `:43 workDuration = 2f`／`:1287-1295` 仅 Gather 走源侧／`:505-509` 落 else 零动作释放 —— 三条全对 |
| 附带项（`ExecuteCompletion` 无 `Build` 分支 ⇒ 不必改） | ✅ | 同意：⛔ 勿给 `Build` 加分支绕 `D`（会把"建筑自推"改成"调度器代推"，动契约面） |
| 触发面 §二 表（11 行） | ✅ | ⭐ **3 条精确化全认可**：`(i)` #5 升级·投料中今天**已能拆** ⇒ ⛔ 不得算进修复范围（否则判据失去鉴别力）；`(ii)` `Ruined` UI 不可达；`(iii)` 边角可达（陈旧面板） |
| 生产可达性（§二 表） | ✅ | `InteractionManager:88-98` 无 state 门 ＋ `IsInteractable:290` 零消费者 ＋ 面板对 `Constructing` 无分支 ⇒ ⭐ **`U-8` 不是构造法产物**，符合 `L-51` |
| §四 碰头面 | ✅ | 去重＝`(source, type)` 二元组；非 `Transport` 任务 `slots=1` ⇒ 拆除 1 人 ＋ 搬料 1 人**可并发** ⇒ `C` 同批的必要性论证成立 |
| §五 `C` 处置四条论证 | ✅ | `UnregisterSiteStore:562-567` ⛔ 不动 `_items`；`DropSiteStoreToChest:995-1006` 与在册**正交** ⇒ **准** |
| §六 读档缺口 | ✅ | 本端另证：`SaveState:1037 state = (int)state` 入档 ＋ `SpawnFromSave:292 → :155` ⇒ **非 Active 存档态真能恢复** ⇒ 缺口成立（⚠️ 连带勘正见 §三） |
| `E1`／`E2`／`E3`／`E4` | ✅ | `E1` 成立且**本端补两条更重后果**（见 §三）；`E2`／`E3`／`E4` 成立 |

---

## 三 · 须补强／勘正 2 条

### 三-1 ⭐ `E1` 的后果比报告描述更重（本端补两条）

报告写「重建立即永久冻结」。本端补：

**① `EnterRuined` 不注销工地仓源**
`:1197-1212` 只 `Unregister(this)`（**本体**）⇒ `_awaitingMaterials` 仍 true ⇒ `ConstructionSiteStore.IsValid`（`:163`）仍 true ⇒ ⭐ **工人继续给废墟送料**（同族缺陷，报告未提）。

**② 重建路径会**吞料**（不可逆损失）**
`StartRebuildFromRuins:514-523` → `BeginMaterialPhase` → `need` 非空 ⇒ `RegisterSiteStore()` ⇒ 工人送料 ⇒ 料齐 ⇒ `OnSiteMaterialsReady:574-581` ⇒ **`_siteStore.Clear()`**:578 —— 但 `_demolishing` 仍 true ⇒ `Update:594` 永走拆除分支 ⇒ **`FinishDemolish` 永不执行**（`HasAssignedWorker()` 本体 false）⇒ ⭐ **材料被清空 ＋ 建筑永久冻结**。

⇒ 危害升级：⛔ 不再是"功能冻结"，而是 **"材料不可逆损失 ＋ 建筑永久不可用"**（存档报废级）。

### 三-2 `N-3` 注释的"实测"＝构造法读数（`L-51` 实例）

`Building.cs:1073-1074`（`U-4` 件 3 加的防御性注释）写：

> 「⛔ 勿直接对"已存在且 `state` 未按存档置入"的实例调本方法（否则该实例 `state` 沿用旧值 · **实测**：升级料齐未完工的建筑读档后 `state` 退化为 `Active`）」

⚠️ 本端实读：**生产读档路径下 `state` 会按存档恢复** —— `SaveState:1037`（`state = (int)state` 入档）→ `BuildingFactory:292`（`state = (DataState)data.state`）→ `:155`（`b.state = initialState`）⇒ ⛔ **不会退化**。

⇒ 该"实测"现象只可能来自**对已存在实例直调 `LoadState`** 的样本（＝构造法，绕过了生产入口）⇒ 注释**前提写对了**（"勿直接对已存在实例调"），但把**构造法读数写成"实测"**，易被后续读者误当生产路径行为。

⇒ 裁：**并入 `U-8` 批的注释勘正**（零行为）：改为「（**构造法样本**：对已存在实例直调 ⇒ `state` 沿用旧值）」。
⇒ ⭐ 这是 `L-51`（判据样本绕过生产入口须显式声明）的**第二个实例** ⇒ 与本条 `L-52` 同批入库。

---

## 四 · `Q1`~`Q7` 裁决

### `Q1` 注册时机 ⇒ ⭐ **准案甲**（`Demolish()` 内即时注册）

理由（报告的 5 条全采，本端加第 6 条）：
1. 全状态覆盖且 **⛔ 不碰任何状态集**（§一真值表已证 `_demolishing` 自足）
2. 代价 1 文件 ≈4 行 vs 案乙 2~4 文件 ＋ **必须超出任务书清单补 3 项**
3. ⛔ 无遍历开销、⛔ 无入册/清册抖动
4. ⭐ 案乙引以为据的"投料态本体在册"**零消费者**（投料由工地仓广告／施工期 `:1377` 早退／拆除由案甲覆盖）⇒ **纯成本**
5. ⭐ 案乙把"多点状态集同步"从 1 处扩到 **4 处** —— 而**这正是 `U-8` 的成因模式**（本端 `D799` 提示词还建议"必须同批改状态集"，实为把成因模式当修法）
6. ⭐ 本端新增：案乙的"料齐被 `Tick:223` 静默清源（⛔ 不回调 `OnUnregister`）"会**新增一条静默路径**（`E4` 同族）⇒ 案甲不引入新静默面

### `Q2` `TaskScheduler:95` 是否同步放宽 ⇒ ✅ **准「⛔ 不放宽」**

本端复核报告 §1.2 的 4 条限定 —— 全部成立（`:35 HasInstance` ⇒ 任何访问即建单例；该循环只在创建瞬间跑一次；对读档路径本就无效）。
⇒ ⚠️ **留痕**：`:95` 与 `Building.cs:1445` 是**两处同型守卫**（同一语义写两遍）⇒ 立观察项 **`O5`**：未来若有人改注册语义，**须同查两处**（⛔ 本片不动）。

### `Q3` `Demolish()` 注销工地仓源 ⇒ ✅ **准「注销（必做）＋ 硬化（建议）」**

⭐ **硬化落点本端裁：`Building.cs:574 OnSiteMaterialsReady()` 首行加 `if (_demolishing) return;`**（⛔ 不选 `ConstructionSiteStore:135`）
- 理由：语义更准（"正在拆 ⇒ ⛔ 不走完工收口"），且能**同时**封住「已在途恰好到货」路径；改 `:135` 只封"再卸料"，封不住在途
- ⚠️ 注意 `:576` 已有 `if (!_awaitingMaterials) return;` ⇒ 新守卫加在**首行**（先于它）
- ⛔ 不动 `DropSiteStoreToChest`／退款公式（`U-1`／`U-4` 域，已销号）✅ 准

### `Q4` 拆除工时 ⇒ ✅ **准「改」**，⭐ **落地形态本端裁「活读」**

**裁**：`GetTaskDuration` 内
```csharp
if (task.args is DemolishTaskArgs da && da.target != null)
    return Mathf.Max(0.01f, da.target.DemolishDuration());
```
⛔ 不用「快照」（`DemolishTaskArgs` 加字段存值）。

理由（⭐ 与报告"同形照搬"略有分歧，本端说明）：
- `gatherSeconds` 是**静态配置**（源侧一次填定）⇒ 快照合理；而 `DemolishDuration()` 是**每帧可变**（依赖 `CountAssignedWorkers` ＋ 协作系数 `k`）⇒ 快照会把工时**冻结在广告时刻**，与"建筑自推"的实际耗时漂离
- 拆除**需多轮派工**（D 面实测 ≥2 轮）⇒ 每轮都取最新值才与建筑侧一致
- ✅ `DemolishTaskArgs.target` 已在场（`:96`）⇒ 活读零新增字段（本端已实读）
- ✅ 保留 `secs > 0f` 同形兜底（`≤0 ⇒ workDuration`）

⚠️ **本端追加要求**：报告提的"慢通道副产物（改后多数由 `FinishDemolish` 先收口 ⇒ 任务被 `OnBuildingDied` 放弃）"＝ **预期行为** ✅，但**判据必须给该读数**（⛔ 不得只报"拆除完成时间"，须写明"任务被放弃（非完成）"）。

### `Q5` 协作人数 ＋ `k` 语义 ⇒ ✅ **准「维持 1 人」** ＋ ⭐ **本端加裁 1 条**

准：非 `Transport` 任务 `slots=1` ＋ `(source, type)` 去重 ⇒ 同时最多 1 个拆除任务；`09` §16.3-3／§16.3.1 ⛔ **未定工人数上限**，亦未定"单位工人一次干多久" ⇒ 属口径空缺（报告判定成立）。

⭐ **本端加裁：`DemolishDuration()` 的 `n` 须按 `DemolishTaskArgs` 过滤**（报告 `Q5` ④ 的"可选 1 行收紧" ⇒ **升为必做**）：
- 理由：`Q4` 改后 **工时直接依赖 `DemolishDuration()`**，而 `n` 现为源级计数 ⇒ 会被拆除前**残留的 `Production`／`Transport` 任务**抬高 ⇒ ⭐ **拆除被无关工人"加速"**（`E2` ①）。二者组成一条闭合错的路径 ⇒ 必须同批
- ⚠️ 收紧后 `n` 恒 1 ⇒ `k` 分支成**死分支** ⇒ **保留但加注**（⛔ 不删 —— 与建造侧同源，删了会破坏单源）
- ⭐ **多工人协作拆除 ⇒ ⛔ 本片不做**（需改派工去重／量纲，属行为变化）⇒ 立观察项 **`O6`**（若实测"拆大建筑太慢"再开片）

### `Q6` 投料态本体在册开销 ⇒ ✅ **准「可接受但 ⛔ 无必要」**

数据本端核过（`Tick:223` 一次 `IsValid` ＋ `:254` 一次早退 ⇒ O(1)/源/tick；对照 `tickInterval=1f`）⇒ ① 若走案乙，性能**不是**否决理由；② 真代价是**状态集一致性维护**；③ 案甲下该开销 ≈0。⇒ 维持 `Q1` 结论。

### `Q7` 与 `U-5` 交叉 ⇒ ✅ **准「成立（窄面）」**，⭐ **本端裁「同批」**（比报告建议更强）

本端复核报告推演表 —— 成立：
- `InProgressStageIsBuild()`（`:964-976`）兜底对「首次建造」✅ 正确、对「升级」✅ 正确（`_siteNeed` ≠ build pack）
- 对「**废墟重建**」❌ **无法区分**（只判 build／非 build）⇒ `_pendingRepair` 丢失 ⇒ `PaidStageCost` 的 `isRepair=false`（修复费不参与）＋ `CurrentStageCost` 走 `def.levels[level-1].upgradeCost`（按升级价退金）⇒ ⭐ **金路口径错配 ＋ 非金修复费缺失**

⇒ ⭐ **裁「`U-5` 与 `U-8` 同批」**（⛔ 不采报告的"同批或同批登记"）：
- 理由：⭐ **修完 `U-8` 才使该偏差可达**（今天读档后的在投修复建筑＝卡死 ⇒ 拆不动）⇒ ⛔ 不得把一个"已知会错"的口径留在**可达面**上
- ⚠️ 但 `U-5` 批**只做「尾插 2 字段 ＋ 读档恢复」**，⛔ **不动退款公式**（`PaidStageCost`／`CurrentStageCost` 属 `U-1` 域，`D796` 已销号 ⇒ 乱动＝重开收口件）

---

## 五 · `E1`~`E4` 裁决

### `E1` ⇒ ⭐⭐ **裁「同批必做」**（⛔ 不挂账）

**为什么不能挂账**（三条）：
1. ⭐ **`U-8` 修复会显著放大它** —— 拆除窗口从"近乎瞬时"变成"≥6s ＋ 需工人"，而 `09` §16.3-4 明定「**建造中的建筑有 HP · 能被打**」⇒ 拆除中被打毁是**设计内场景**，⛔ 不是边角
2. **后果不可逆**（材料损失 ＋ 建筑永久不可用 ⇒ 存档报废级，见 §三-1）
3. **同域** —— `M1-C` 件 4（拆除有耗时）× 既有 `EnterRuined` 的交叉，与 `U-8` 同一功能域

**修法本端裁（报告"不预设修法"，故须本端定）**：

**件 `E1-a` · 互斥口径（主体）** —— `EnterRuined()` 内、`:1199` 之后加：
```csharp
_demolishing = false;
_demolishProgress = 0f;
```
- 语义 ＝ ⭐ **「被打成废墟」优先于「正在拆」**（生命周期事件压过玩家指令）
- 依据：`09` §16.3.1「建筑侧 耐久到 0 ⇒ **触发生命周期结束**」⇒ ⛔ 两者不该并存；玩家"拆"的意图已被外力中断 ⇒ 清指令是自然语义
- ⛔ **否决**报告的另一选项（`Demolish()` 侧互斥：`state == Ruined` 时拒绝）—— 因为 `E1` 是"**先开拆、后被打毁**"，不是"先废墟、后拆除"，该选项**不解决 `E1`**

**件 `E1-b` · 注销工地仓源** —— `EnterRuined()` 内加 `UnregisterSiteStore()`（封 §三-1 ① 的"工人给废墟送料"）
- ⚠️ **内容物处置本端裁「留仓」**（⛔ 不掉箱）：`Ruined` **不是**生命周期结束（可重建），符合 `D154`「废墟不 `Free` 占格 · 可修复」⇒ 材料留在工地仓等重建
- ⛔ ⚠️ 但**留仓 ＋ `E1-a` 清拆除态** ⇒ 重建后 `_demolishing=false` ⇒ `Update` 正常走 `Constructing` 分支 ⇒ **不再吞料**
- ⚠️ **本端另加**：`E1-b` 的注销须**不影响** `DropSiteStoreToChest`（`U-4` 分工）⇒ 只调 `UnregisterSiteStore()`（⛔ 不 `Clear()`）—— 二者正交（§二 §五 已核）

### `E2` ⇒ ✅ **准「加注」**；⭐ `n` 收紧已升入 `Q5`；门控精化 ⛔ 本片不做

- ✅ `CountAssignedWorkers` 源级、无过滤 ⇒ `n` 可被残留任务抬高 ✅ （已并入 `Q5` 必做）
- ✅ 门控 `HasAssignedWorker` 含 `Assigned`／`MovingToSource` 期（＝"有人接单"≠"工人到场"，对照 `:591-593` 注释"有工人到场"）⇒ **加注**
- ⛔ **门控精化（`Assigned` → `Working`）本片不做** —— 属行为变化（会改拆除手感 ＋ 与 `Q4` 工时改动叠加难判因）⇒ 立观察项 **`O7`**
- ⚠️ **但判据必须如实报该偏差**：`Q4` 改后"进度在工人途中亦推进"⇒ **实测拆除时间会短于 `DemolishDuration()` 标称值** ⇒ 判据 7 须写明"标称 6s／实测 X s（含途中推进，`O7` 域）"

### `E3` ⇒ ⭐ **裁「统一到数据层」**：`CanDemolish` 加 `state != BuildingState.Ruined`

- 理由：① 消除口径分裂（`CanDemolish:836-839` 注释自陈"与 `Demolish()` 守卫**同源** · 禁两处各写一遍" ⇒ **让它真同源**）；② ⭐ 顺手**封住 `E1` 的陈旧面板路径**（`BuildingPanel:393 OnDemolishClicked` 有 `if (!_target.CanDemolish) return;` ⇒ 加判据即拦）；③ 1 行；④ 消费面本端已核（3 处，安全）
- ⚠️ ⛔ **`IsInteractable:290` 死读口不属本片** ⇒ 立观察项 **`O8`**

### `E4` ⇒ ✅ **准「留痕备查」**（⛔ 本片不改）

`Tick:223` 直接 `_sources.Remove`（⛔ 无 `OnUnregister`／`OnBuildingDied`）＝既有 `R5` 同族。
- ⚠️ 与本案交点：**案乙**下"料齐被清"即本路径（静默）；**案甲**不依赖该路径做清算（走 `Die → ReleaseLayerOwnedState:1237` 显式注销）⇒ ⭐ **佐证 `Q1`**（并入 §四 `Q1` 理由 6）
- ⇒ 归 `M1-D`／`M1-G` 收口，本片仅留痕

---

## 六 · 施工批件清单（`U-8` 补丁批 · 合并后）

| # | 件 | 文件 | 性质 |
|---|---|---|---|
| 1 | `Q1` 案甲：`Demolish()` 内即时注册（复用/扩展 `RegisterWithTaskScheduler`，`Register` 天然幂等） | `Building.cs` | 功能 |
| 2 | §六 读档补注册：`LoadState` 恢复 `_demolishing` 之后"若 `_demolishing` ⇒ 确保注册"（⛔ 不动 `BuildingFactory:189` 守卫 —— 存档字段语义 ⛔ 勿塞进工厂） | `Building.cs` | 功能 |
| 3 | `Q3` `Demolish()` 调 `UnregisterSiteStore()` ＋ `OnSiteMaterialsReady:574` 首行加 `_demolishing` 硬化 | `Building.cs` | 功能 |
| 4 | `Q4` `GetTaskDuration` 活读 `DemolishTaskArgs.target.DemolishDuration()`（保留 `≤0` 兜底） | `TaskScheduler.cs` | 功能 |
| 5 | `Q5` `DemolishDuration()` 的 `n` 按 `DemolishTaskArgs` 过滤 ＋ `k` 死分支加注 | `Building.cs` | 功能 |
| 6 | `E1-a` `EnterRuined()` 清 `_demolishing`／`_demolishProgress` | `Building.cs` | 功能 |
| 7 | `E1-b` `EnterRuined()` 调 `UnregisterSiteStore()`（⛔ 不 `Clear()`） | `Building.cs` | 功能 |
| 8 | `E3` `CanDemolish` 加 `state != Ruined` | `Building.cs` | 功能 |
| 9 | `U-5` 同批：`_pendingUpgrade`／`_pendingRepair` **尾插入档 ＋ 读档恢复**（⛔ 不动退款公式） | `Building.cs` ＋ `BuildingSaveData.cs` | 功能 |
| 10 | `Q2`／`C` 加注 ＋ `N-3` 注释勘正（构造法读数）+ `E2` 门控加注（零行为） | `Building.cs` | 注释 |

⛔ **不得动**：`WarehousePanel`／`TreasureVault`／四档账本／美术／`pixel-forge`／`GameScene`／`Packages`／`3.6`·`3.8` doc；⛔ 不 push；⛔ 不代提交策划端账本。

---

## 七 · 判据要求（并入 `D799` §四 三项回归）

⛔ **全部走生产路径**（`L-51`：⛔ 禁构造法；若必须构造须显式声明 ＋ 单列"生产路径可达性"）⛔ **口径按 `L-48`：测"用户可见最终状态"**（⛔ 非中间函数返回值）

| # | 判据 | 鉴别力声明（须前置给出） |
|---|---|---|
| 1 | **首次建造·投料中**：真 `BuildController` 下单 ⇒ 面板点拆 ⇒ 有工人到场 ⇒ 进度 0→1 ⇒ 真拆 | 改前：进度**恒 0** |
| 2 | **首次建造·料齐施工中** ⇒ 同上 | 改前：恒 0 |
| 3 | **升级·投料中**（**回归** · ⭐ 改前已通过）⇒ 拆除正常 | ⚠️ 改前已过 ⇒ **只作回归**，⛔ 不得当 `U-8` 判据（否则零鉴别力） |
| 4 | **废墟重建·投料中/施工中** ⇒ 拆除正常 | 改前：恒 0 |
| 5 | ⭐ **读档续拆**：拆除中存档（态＝`Constructing`）⇒ 读档 ⇒ 能继续 0→1 | 改前：**永久冻结**（幂等守卫拦住） |
| 6 | **`C` 面**：投料中拆除期间 ⇒ 工地仓**不再**广告搬料 ＋ 已到料仍随 `FinishDemolish` 掉箱（`U-4` 判据 8′ 复跑） | 改前：持续送料 |
| 7 | **`D` 面**：`Demolish()` → 真拆 ≈**6s**、**≤2 轮**；⭐ 须写明"标称 6s／实测 X s"＋"任务被**放弃**（非完成）"＋`O7` 途中推进偏差 | 改前：≈7~8s、≥2 轮 |
| 8 | ⭐ `E1` 面：拆除中打空 ⇒ 进 `Ruined` ⇒ **能重建且能完工**（⛔ 不得冻结、⛔ 不得吞料） | 改前：重建立即永久冻结 ＋ 材料被 `Clear()` 吞 |
| 9 | ⭐ `E3` 面：`Ruined` 态 ⇒ `CanDemolish=false`（陈旧面板点击亦被拦） | 改前：`CanDemolish=true`（陈旧面板可点） |
| 10 | ⭐ `U-5` 面：读档后的"在投修复"⇒ 拆除退还**按修复口径**（⛔ 非升级价） | 改前：按 `upgradeCost` 退金（口径错配） |
| 11 | **存档往返逐值一致**（含 `_pending*` 两新字段） | — |

⭐ 另按 `D799` §四并入：① **`HH.315 M1-C` 冒烟**（`U-4` 判据 8′）② **`2_20B_M7`** ③ **1× 时基跑批**（验 `O-6` 材料到账闭合）。

---

## 八 · 落账

- 本裁决书（新建）
- `河谷防线开发计划书具体内容/测试基线台账.md` ⇒ **§一百零四**
- `多Agent交接/_编号登记.md` ⇒ `HH.315` 行状态
- `多Agent交接/_任务队列.md` ⇒ `M1-C` 行 ＋ `U-8` 行（⚠️ MIXED 行尾 ＋ 1 NUL ⇒ python 二进制按行改）
- `多Agent交接/_当前快照.md` ⇒ `D800` 段
- `多Agent交接/策划端/_策划教训库.md` ⇒ 新立 **`L-52`**（＋ `L-51` 第二实例留痕）
- `多Agent交接/策划端/HH.316_U2箱仓搬运链_交付验收裁决.md` ⇒ ⭐ **`U-8` 机理补正**（两处守卫 ＋ 撤 `IsValid` 判断）
- `最高优先级文档/09_资源与仓库.md` ⇒ §16.3 补 `E1` 互斥口径（拆除中被打毁 ⇒ 清拆除态）

---

## 九 · 状态

| | |
|---|---|
| `U-8` 评估 | ✅ **通过 · 全采** |
| 本端自纠 | ⚠️ **1 条**（落点 B 机制判断错 ⇒ 撤 ＋ 立 `L-52`） |
| `Q1`~`Q7` | ✅ 全准（`Q4` 落地形态／`Q5` 加裁／`Q7` 升为同批 ＝ 本端 3 处加强） |
| `E1`~`E4` | ✅ 全裁（`E1` **同批必做** · `E3` 加 1 行 · `E2`／`E4` 加注留痕） |
| **`M1-C` 待修** | ⭐ **1 笔 ＝ `U-8` 补丁批**（含 `Q1`~`Q5`／`E1`／`E3`／`U-5` 同批） |
| **`M1-C` 销号前置** | ⭐ 唯一 ＝ **`U-8` 补丁批验收通过** |
| 观察项新增 | `O5`（两处同型注册守卫）· `O6`（多工人协作拆除）· `O7`（门控含派工未到场期）· `O8`（`IsInteractable` 死读口） |
| ▶️ 下一步 | 出 `U-8` **施工**提示词 ⇒ 执行端落地 ⇒ 验收 ⇒ **`M1-C` 销号** ⇒ 开 `M1-D` |
