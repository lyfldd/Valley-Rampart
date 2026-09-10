# HH.160 · HH.159 插队合并微批（件1~件12）完成报告

> 类型：执行端交付报告（**非裁决**，请策划端验收）
> 依据：HH.159_插队合并微批_任务书（含 §九·补 D619/D621）· HH.159_实施清单（52 条）· D609/D613/D614/D617/D618/D621/D625
> 状态：🟡 待验收（T9.2 记 🟡半成：入口 ✅ / 行进 ❌ 归 DZ-081）
> 口径：全部证据为磁盘/运行期实测（file:line + 日志原文），无凭记忆申报
> 编号：本报告 = HH.160（账本 🔵预留行，验收时销号）

---

## 〇、一句话结论

**件1~件12 已全部落地并通过验证**：代码面编译新增错误 **0**；四容器回归 **ALL PASS（[FAIL]=0）**；同 seed 双跑**行为字段全等**；三支新探针（T8.1/ T11.6/ 件9）共 **22 PASS / 2 FAIL**，其中 1 个 FAIL 为**我自己引入的顺序 bug（已修并复跑 PASS）**，其余 **1 个 FAIL 归 DZ-081（非本批）**。抓出 2 个上游缺陷（raceId 缺写 → 已按 D625① 本批补线；预置链静默重叠 → 归重构批 T4）。

---

## 一、git 面构成（6 笔，只提本批文件，未 push）

| commit | 内容 | 规模 |
|---|---|---|
| `115920b` | 件1~件11 代码+静态验收交付（含资产/文档） | 92 文件 2913+/258- |
| `7ecbfa0` | 件12（T12.1+T12.2 同趟）：矿山 2×2 成簇 + 水域避让 + MineralRich 阈值重标定 | 2 文件 148+/20- |
| `9a13c45` | 件9 三项派生（D619 §9.1）：切国残影清理 + 巡逻静态表跨局清空 + 文本变量补正解 | 4 文件 11+ |
| `25d6cd1` | 收口跑批②：T8.1 容器 + T11.6 探针 + 件9 探针（新建三容器）+ P7b 顺序 bug 修复 | 7 文件 770+ |
| `715e280` | DZ-086（D625①）：AI 工人个体族补线 raceId=国族 | 1 文件 9+/1- |
| *（训练仓）* | M7 15_账本 补二十六（**未提交**，随训练轨下一笔） | 1 文件 16+ |

---

## 二、逐件自证（按件分组；条号以实施清单 §一 为准）

### 件1 矿石复活链（T1.1~T1.8）✅
| 条 | 落点 | 证据 |
|---|---|---|
| T1.8 | `TreasureVault.Managed+=Ore` / `KingdomManager.TreasuryOre`(Load L387·属性 L406·Reset L434) / `KingdomSaveData.treasuryOre` 尾插 / `TreasureVault.Init` L73 回填 / `KingdomState.ore`+GetAmount+`AddGatherOverflow case Ore` / `RulerController.Ore` | grep 五文件 anchor；编译 0 错 |
| T1.1/T1.2 | `BlacksmithBuilding.Tick`→`Transform(Ore,Metal,…)`；`StorageComponent.Transform` 守卫 `@in!=Ore\|\|@out!=Metal`，扣 `ruler.Ore` | [StorageComponent.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs) |
| T1.3 | `BlacksmithDef.oreToMetalRatio` cs+asset；全库 grep `stoneToMetalRatio` **零命中** | 实盘 `MetalFrom(6)=3` |
| T1.4 | 第三子仓七处（CreateSubStores/Tick/GetStore/Save3元组/Restore3参/TryAdvertise/OnDestroy）+ `BuildingSaveData.byproductOreAmount` 尾插 + `Building` Save/Load | 实盘 `byproductOreRate=0.05/Capacity=20` |
| T1.5 | `ore_vein.asset outputResource=Ore` | 实盘直读 |
| T1.6/T1.7 | 注释/文案勘正（含 T1.7 验收「全库石→Metal 零命中」下**扩清 5 处相邻过时注释**） | `Blacksmith.asset desc=矿→Metal`；`[^矿]石→Metal` 零命中 |

### 件2 footprint + 房容（T2.1~T2.17）✅
- **T2.1~T2.12**：12 笔 2×2 正方形化（含 **mine** D613 / **House** D614），逐资产实盘直读核验 ✓
- **T2.13**：`portal.asset` 建档（footprint 2×2）✓
- **T2.14**：六模板 `baseBuildingDefIds`（castle/House/farm/Well/mine/Warehouse/quarry）**7 def 资产全部存在** ✓；⚠️ 见 §五-2 隐患
- **T2.15**：选址回归 = 四容器 + 双跑（§三）✓
- **T2.16**：房容 ×4 → `KingdomConfig.houseCapacityByLevel=[12,20,32]` SO 化，读口实盘 `GetHouseCapacity(1/2/3)=12/20/32` ✓
- **T2.17**：夹具 `houses 3/5/8→1/2/2`，实盘 cap>pop 三者 pass 且余量最小（+6/+10/+6）✓

### 件3 2_24 批1（T3.1~T3.5）✅
- T3.3 / T3.5 **本次实装**（SatietySystem 主体判定 `kingdomId`；WarehouseHelper 三重载 + `GatherWarehouses(kingdomId)`）
- T3.1 / T3.2 / T3.4 **核验级**（前批已实装：HH.86 件2a/2d、2_17 步骤11 批2）→ 列报「台账与现状不符」第 3 处

### 件4 DZ-077/078/079（T4.1~T4.3）✅ / 件5 文档核对（T5.1）✅
- 件4 四文件落地 + LODSystem 显式 0；件5 核对一致（附 1 处小瑕：3.1.3 L131 修订行仍写「变更 10 笔」未随 D613 更 11，L132 已用「11→12」承接）

### 件6 A 组协同（T6.1~T6.3）✅
- **T6.1 降级**（D617 决策2=B）：三资产值**未动** + 注记 2 处（`BuildingDef` tooltip / `WanderStimulusProvider`），写明三类语义差异 + DZ-054 以实盘 4 消费面修正销案

### 件7 B 组注释卫生（T7.1~T7.3）✅
- **T7.3 降级**（牵连超预期）：`ProducerComponent` 死分支**留最小注释 + 台账登记**（非硬删——牵连 `BuildingSaveData.byproductType/byproductAmount` 存档面 + `Building` Save/Load + `BuildingFactory` restore 三处）

### 件8 HH.131（T8.1~T8.3）✅
- **T8.1**：新建 `Valley_HH131_BedrockProbe.cs`，**8 PASS / 0 FAIL**；实收=公式基准全 ±1 内（P2 10/10、P3 18/18、P4 17/17、P4b 18/18）
- T8.2：P1 观察器白名单补 combat/GameOver tag（只加不删）✓
- T8.3：Smoke_9 #19 三型分型，ALL PASS 且仍有判别力 ✓

### 件9 HH.133（T9.1~T9.4）🟡
- T9.1 ✅（选族卡 USS）/ T9.4 ✅（D240 对比度）/ T9.3 ✅（阶段列 + 幸福列 + 染色联动）
- **T9.2 🟡半成**：入口接线 ✅（`SelectionController`→`PatrolTaskSystem.StartPatrol` 真链派发成立、负探针 AI 巡逻 0/24）；**行进 ❌ 归 DZ-081**（见 §五-3）
- 派生三项（D619 §9.1）：高亮残影清理 ✅（P7b 复跑 PASS）/ 巡逻静态表跨局清空 ✅ / `--color-text-secondary` 补正解 ✅

### 件10 HH.132（T10.1~T10.4）✅
- 新建 Editor-only `LifecycleAudit.cs`（菜单 L131 / 豁免表 L38）；幂等（两次报告 SHA256 一致）；R1 三类报告（①0/②0/③2）+ 54 基线对账差异列报；R2A 违例 1（`DamageSystem`）、R2B 0；T1~T3 注入全检出 + **注入删净零残留**

### 件11 HH.153（T11.1~T11.6）✅
- T11.1~T11.5：`talkRaceChance=0.4`（cs+asset 双在场，**不入 ToSnapshot**）+ 4 族 × 8 句族池 + 17 职业全量替换（`随时准备战斗` 零命中）+ 双层混合 + 38 静态池
- **T11.6**：初跑 3P/2F（AI raceId 恒 Human）→ 按 D625① 补线后**重跑 5 PASS / 0 FAIL**（24/24 与国族一致、Human 兜底 0、raceIds=[0,2,3] 非恒定）

### 件12 矿山锚点簇（T12.1~T12.2，D618/D621/DZ-084）✅
| 指标（同 seed 1/12345/54321/20260910，256² Medium） | 改前 | 改后 | 判定 |
|---|---|---|---|
| 总矿格数 | 4381/4531/4520/4419 | 4156/4280/4412/4132 | −2.4%~−6.5% ✓ D621②「±小比例」 |
| 孤立矿格 | 4377/4515/4520/4411（99.9%） | **0/0/0/0** | ✓ D618 验收① |
| 出生半径 48 完整 2×2 簇保底 | — | **4/4** | ✓ D618 验收② |
| MineralRich 命中/256 | 196.5（@0.05） | 205.8（@**0.039** 重标定） | ✓ D621③ |

实现要点：成簇=逐格语义平移（顶掉格内原特征）；`EnsureBlock` 判定写死**轴对齐 2×2 完整块**（D621①）+ side>1 二级兜底；`PlaceRiver`/`PlaceLakes` 避让 Mine（DZ-084）；水域后 `PruneOrphanMineCells` 兜底（实测 0 命中）；工具按 `side` 参数化（未硬编码 Mine）。

---

## 三、收口跑批证据（全部走 HH.92 正门 EnterTestRun / ExitTestRun）

| 项 | 结果 | 证据 |
|---|---|---|
| **四容器回归**（2_20 / 2_20B / 2_20C / Smoke_5） | **全 ALL PASS，`[FAIL]=0`** | 2_20 seed22360 全子探针 OK；2_20B **6/6 轮** ALL PASS；2_20C P0~P9 全 PASS；Smoke_5 全 OK |
| **同 seed 双跑**（2_20C seed 22360） | **行为字段全等**（L-22 成立） | 立国/分离/P4 端到端/P9 行为焦点（k1 好战 0.46→BuildWall、k2 0.12→BuildWall、k3 1.26→Expand）逐值同 |
| T8.1 磐石探针 | 8 PASS / 0 FAIL | 实收=公式基准 ±1 内；P4b 反证 45% 唯由 `rangedDamageReduce` |
| T11.6 弹话探针 | **5 PASS / 0 FAIL**（补线后） | 族池占比 0.392≈0.4；raceId 24/24 匹配、兜底 0 |
| 件9 UI 探针 | 9 PASS / 2 FAIL | P7b 修复后 PASS；P1/P2b FAIL 归 DZ-081 |

**编译**：全程新增错误 **0**（每笔提交前均 `refresh_unity(compile=request)` + `read_console(types=["error"])` 复核）。

---

## 四、M7 · 15_账本登记回执

已登记 `ai决策大脑强化训练\15_训练侧harness与Unity端差距文档.md` **一·补二十六**（**未提交**，留训练轨随其下一笔）：
- `KingdomConfig.byproductOreRate/byproductOreCapacity`（回灌待定，当前零回灌 U 系事实注记）
- `BuildingSaveData.byproductOreAmount` + `KingdomSaveData.treasuryOre`（尾插零 bump，M10）
- **变换链 Ore→Metal 语义注记**（sim 将来引入金属/矿石经济须对齐「原料=Ore 非 Stone」）
- `KingdomState.ore` AI 独立桶（不进五经济 ResourcePack）
- DZ-086 raceId 补线 / D621③ 阈值重标定（均 Unity 专属，零回灌义务）

---

## 五、新发现与待裁

1. **【已按 D625① 本批处置】AI 第一代工人 raceId 恒 Human 兜底**（`KingdomFoundry.SpawnAiWorkers` 未写 `uc.raceId`）→ 已补线（`715e280`），登记 **DZ-086**；T11.6 由 3P/2F 转 5P/0F。
2. **【归重构批 T4】预置链静默重叠**：运行期实测 3 AI 国 `AI建筑数=21 重叠格数=11`（castle↔farm 每国 1 格、House↔Warehouse 每国 2~4 格）。根因=`KingdomFoundry.PlaceBuildings/BuildingCell` 固定环带取点、**不经 `IsFootprintClear` 校验**，`MarkOccupiedFootprint` 仅登记不拒建 ⇒ 「预置 6/6 座」日志只证实例化未抛异常，**不构成无重叠证据**。建议 T4 加落点校验/重取点 + 给「预置」日志补重叠计数（HH.155 §2.1 同一根因）。**本批只报不改。**
3. **【归 DZ-081，非本批】玩家巡逻「派发成立但不行进」**：`PatrolTaskSystem.Tick` 只注入 `TaskStimulus`、**未把巡逻登记进 TaskScheduler/Executor 派发链** ⇒ 无移动 ⇒ 单位回落 `Wander`（探针实测 `lastCmd.Module=Wander`、位移 0.32 格）。修复=结构面（①接 TaskScheduler 派发链 ②给玩家单位直接指令面 `MoveTowards`），挂 DZ-081 定档下批。P2b「敌清后回巡」同归。
4. **【登记表与代码不符（件10 R2 检出）】**：`DamageSystem` 为 `Singleton` 但未实现 `ISaveable`，而登记表 §3.1 反将其列入 ISaveable 全集 → 待策划端修正（已登记 DZ-085）。
5. **【登记表 §4 漂移】**：`GeneralDiedEvent`/`FormationDisbandedEvent` 在场未登；`UnitAttackEvent`/`BuildingProductionTickEvent`/`EnemyEnteredRegionEvent` 已删未回写。
6. **【小瑕·文档】3.1.3 L131** 修订行仍写「变更 10 笔」（未随 D613 更 11），L132 已用「11→12」承接，实质口径一致。
7. **【本批自曝·已修】P7b 顺序 bug**：切国复原上一国 mid 时须先摘高亮态（`_highlightKid=-1`）再 `SetMidColor`，否则 `ComputeTargetAlpha` 仍按 `kid==_highlightKid` 判高亮浓度 ⇒ 复原无效。由件9 探针抓出 → 修复复跑 PASS。

---

## 六、教训核查

- **L-12（同文件串行编辑）**：本批全程遵守（同文件多处编辑逐笔串行），**无复发**；对照上批并行 `SearchReplace` 丢写事故。
- **L-02（写后必验）**：两次实证有效——① 件12 首版「整簇全 Plain」限制致矿格数仅 588（实测抓出自纠）；② P7b 顺序 bug 由探针抓出。
- **L-01（就位≠生效）**：D621①（`EnsureOne` 判定须完整块，否则孤立格令保底静默失效）命中并已实现；T9.2「入口就位 ≠ 行进生效」正是本条新实例（🟡半成不给 ✅）。
- **L-17（正门）/ L-06（冒烟走生产链）**：本轮所有进局（四容器 + 双跑 + 三探针）均走 `TestHarnessApi.EnterTestRun` + `ExitTestRun`，**零裸跑**。
- 新教训候选：**「验证探针抓出修复者自身引入的顺序 bug」**（P7b）= L-02 的正向实证，是否入册请策划端定。

---

## 七、遗留与后续（不阻塞本报告验收）

- **DZ-081**：玩家巡逻行进 + 回巡（结构面修复，定档下批）
- **重构批 T4/T6**：预置链重叠守卫 + 矿山节点限位口径
- **策划端待回写**：登记表 §3.1/§4 修正（DZ-085）
- **训练仓**：15_账本 补二十六 待训练轨随笔提交
