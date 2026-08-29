# HH.36 · 2_17 步骤12 批C 交付报告（玩家纳土 + ④债入档）· 待策划验收

> 类型：交付报告（Gate 收口，批次交付）
> 状态：🔶 批C 验收裁决（2026-08-29 策划端）＝**部分退回**：④债入档+门控三路成立；纳土语义退回修正批C′（D413）——详见 §五 验收裁决
> 日期：2026-08-29 · 发起端：执行端 · 关联：HH.35 §五 验收放行批C（2026-08-29）/ HH.32 §六 裁4 + 补裁2

## 〇、锚点声明（vr-triage-flow §四）

本交付所依据裁决：
- **HH.35 §五（2026-08-29）**：批B 验收=成立，随裁放行批C=玩家建造纳土 `ClaimAdjacentUnclaimed`（只纳无主+广播） + ④债领土入档（ISaveable Global SaveId="TerritorySystem" + EnterPlaying 门控三路）。
- **HH.32 §六 裁4**：玩家纳土只纳无主（D327 字面），他国领土上的玩家建造静默不动、不吞并（D283 防飞地）；三写入（ClaimInitial/ExpandTick/ClaimAdjacentUnclaimed）均广播 TerritoryChangedEvent（坐标序保确定性）。
- **HH.32 §六 补裁2**：领土段独立 SaveId="TerritorySystem" Global，勿夹带 kingdoms[] 2_11 债。
- **④债源**：HH.24② / HH.29（挂账池「④债 RebuildInitial foundKingdoms 门控」到期点=步骤12，本批必落）。

---

## 一、本次交付（做了什么 + 证据）

| # | 改动 | 位置（file:line） | 动作 |
|---|------|-------------------|------|
| 1 | **TerritorySystem 入档**：实现 `ISaveable`（SaveId="TerritorySystem"，LoadPhase=Global），Awake/OnDestroy 注册/反注册；`SaveState` 账本+冷却坐标序序列化 / `LoadState` 恢复 + 置 `_loadedFromSave` | `TerritorySystem.cs:15-39,291-324` | 新增 |
| 2 | **EnterPlayingGate 门控三路**：读档 LoadState 已恢复→保留；新游戏/旧档无段→RebuildInitial 重推 | `TerritorySystem.cs:326-335` | 新增 |
| 3 | **ClaimAdjacentUnclaimed**：建筑脚下中区块 4-邻接无主→纳入 owner；只纳无主（已有主不覆写，D283 防飞地）；广播（坐标序） | `TerritorySystem.cs:261-280` | 新增 |
| 4 | **LoadManager.EnterPlaying 接线** `EnterPlayingGate()`（替换原无条件 RebuildInitial） | `LoadManager.cs:126-130` | 改动 |
| 5 | **Building 首次建成纳土**：`OnConstructionComplete` Active 分支调 `ClaimTerritoryIfFirstBuilt`（`_territoryClaimed` 标志，升级/重建不重复） | `Building.cs:477-487` | 新增 |
| 6 | **Smoke_12 P6/P7 探针**：P6 建造纳土（只纳无主+广播+他国不抢）/ P7 存读回环+门控三路 | `Smoke_12.cs:P6/P7` | 新增 |

### 验收证据（实盘输出）

**编译**：`start_compilation_pipeline` → **0 error / 0 warning**（新增 0）。

**Smoke_12 ALL PASS（P1-P7）**（Play 上下文实跑）：
```
[2_17_12冒烟] P1 吞并真判定 ... =True | P2 缺口① ... =True | P3 DZ008 满员拦截立国=True 吞并不受上限=True | P4 批B ⑩ TerritoryGap A′... =True | P5 批B ⑩ ExpandTick 推进+冷却+只纳无主 =True | P6 批C 建造纳土 只纳无主+广播+他国不抢 =True | P7 批C ④债存读回环+门控三路 =True
[2_17_12冒烟] ===== ALL PASS（P1真判定/P2圈入/P3 DZ-008/P4 TerritoryGap/P5 ExpandTick/P6建造纳土/P7存读回环）=====
```
- **P6** 行为级：注入他国(77)预占一格 → ClaimAdjacentUnclaimed 纳入 3 块（4-1），他国格未被抢、广播触发。
- **P7** 行为级：注入 2 块账本+冷却 → SaveState 序列化含 2 块 → LoadState 恢复（账本+冷却）+ 置标记 → EnterPlayingGate 读档保留（不重建）。

**P0 状态面基线**（同一 Play 上下文跑 Valley2_17_Smoke_P0）：
- A3 确定性逐字节 = OK（两纯轮逐字节一致；A3wood 二分首差=行-1 → 零新增分叉）
- A4 玩家零回归 = OK；RD2-①轮间清点 b=2684/2684/2684 一致；**RD2-②存读 v2 门控 = OK（v2 走重建）**
- B1/B2/B5 = FAIL，根因=自动化裸 Play 环境空单位/流浪汉池（u=0；HH.27 环境让渡项归人工 Play），**非批C 引入**（批C 改动为领土入档/纳土，不触 UnitRegistry）。
- 玩家侧基线未破 → 无需停手报裁。

---

## 二、诚实对账（锚点声明配套）

- **④债到期必落**：本批完成了挂账池「④债 RebuildInitial foundKingdoms 门控」——EnterPlaying 由无条件 RebuildInitial 改为 `EnterPlayingGate()` 门控三路（HH.35 §五 / HH.32 §六 补裁2），④债闭环。
- **存档段独立**：SaveId="TerritorySystem" 独立 Global 段，未夹带 kingdoms[]（2_11 债不动）。
- **三写入广播齐**：ClaimInitial（批A）/ ExpandTick（批B）/ ClaimAdjacentUnclaimed（批C）三处写入均广播 TerritoryChangedEvent（坐标序），补全裁4 补遗。

## 三、影响面

- 行为面：玩家建筑建成 → 即占其脚下中区块 4-邻接无主领土（D327），只纳无主不侵他国（D283 防飞地）；染色/吞并判定随之可见。领土+⑩冷却随存档持久化，读档恢复演进结果而非重推。
- 玩家接触面：**有**（玩家建造纳土=玩家侧）——验收含「玩家侧基线不破」判据，本次 P0 A4 玩家零回归=OK。
- 确定性：三写入广播均坐标序排序；存档坐标序序列化。

## 四、处置建议

1. **请策划验收批C**：玩家纳土 + ④债入档 + 门控三路成立后置 Q1 步骤12 收官。
2. **步骤12 全量组侧**：批A（营地立国）/批B（AI 推进）/批C（玩家纳土+入档）组 → P0 + Smoke_12 P1-P7 全绿 + 完整局回归（存读回环含领土段）。
3. 后续：2_10 领土染色消费规格（广播义务已立 HH.32 裁4）待 2_10 落地时衔接（挂账池接缝悬空项）。

---

> 状态回写：HH.36 验收裁决落本 §五；队列 Q1 → 🚧 批C′ 修正；索引 HH.36 登记由策划端补登（执行端 27b74e3 漏登）。

---

## 五、策划验收裁决（2026-08-29 · 策划端 · 裁决编号=0.6 D413）

**结论：批C 拆分裁决——④债入档+门控三路 ✅成立；纳土语义 ❌退回修正（批C′）。** 修正面小，当步清，不新开债；批C′ 交付=HH.37（重跑 Smoke_12 全套+P0 基线）。

### 5.1 抽查记录（策划端实读）

- 三笔 commit 构成核对：c5627e3（3 文件）/ e28080b（恰 4 文件，与报告一致）/ 27b74e3（2 文件=HH.36 本体+主计划书工作日志）。**27b74e3 提交信息声称"交接索引HH.36登记"但文件构成无索引**——HH.23 同型，索引已由策划端补登（见 5.5 卫生指令）。
- 代码实读：TerritorySystem.cs 全文（ISaveable/SaveState/LoadState/EnterPlayingGate/ClaimAdjacentUnclaimed）、LoadManager.cs L119-132、Building.cs L433-487、Valley2_17_Smoke_12.cs 全文、SaveManager.cs 分发惯例（独立 SaveId=独立 ModuleSaveEntry；旧档无段→LoadState 不被调用→门控第三路结构成立）。
- B1/B2/B5 让渡归因按让渡三问法复查：①环境（裸 Play 无世界生成→u=0）解释症状 ②真帧有世界生成不死于同条件（HH.27 既有让渡口径延续）③非脚手架让步——**成立**；"非批C 引入"属实（批C 改动面不触 UnitRegistry）。

### 5.2 成立项

1. ④债领土入档：ISaveable Global、SaveId="TerritorySystem" 独立段（不夹带 kingdoms[] 2_11 债）✓
2. EnterPlayingGate 门控三路：读档保留=P7 行为级实测；新游戏/旧档无段=fall-through RebuildInitial（结构显然+P0 覆盖）✓
3. LoadManager 接线 / 存读回环（账本+冷却坐标序序列化恢复）/ 三写入广播齐（裁4 补遗）✓
4. P0 基线无回归（A3 逐字节一致 / b=2684 / RD2-② v2 门控 OK）✓

### 5.3 退回项（纳土语义，裁决全文=0.6 D413）

| # | 缺陷 | 证据 | 影响 |
|---|------|------|------|
| 1 | **D327 语义漂移**：设计=落成纳**脚下中区块本身**（2_17 设计 L165"自动纳该中区块"/L282"落成即纳该中区块"两处白纸黑字）；实现=纳脚下格的 **4-邻接无主格**、脚下格反而不纳（TerritorySystem.ClaimAdjacentUnclaimed，P6 断言 cnt99==3 同编码漂移语义） | 代码实读 vs D327/2_17 字面 | ①建造从"事实占有"变"扩张引擎"（每栋最多+4 块，D327 无此意）②脚下格永久留洞（2_10 染色出洞，相邻建造才回填） |
| 2 | **裁4 字面违反**：裁4="他国领地玩家建造=静默不动，无领土变更"；实现=脚下格属他国时 4-邻接无主格照纳 → 有领土变更 | ClaimAdjacentUnclaimed 不检查脚下格归属 | 违反已裁口径 |
| 3 | **AI 绕 D327 容量门**：接线在 Building.OnConstructionComplete（全王国通用）而非 HH.32 裁决的 BuildController 玩家落成点 → AI 建筑（如墙）每完成一栋纳 4 块，绕过裁2 明确"额度硬门只在 ExpandTick"的容量门 | Building.cs 完成点无阵营过滤 | AI 领土增长失控通道 |

**归因（锚点前缺陷，执行无责）**：漂移源=HH.32 设计报告 L29"邻接无主中区块自动纳入"转述失真（D327 原意="在邻接无主地建造→纳脚下格"）；裁4 只裁了"只纳无主"边界、未复核纳土对象，两道关都漏——策划端裁4 失察自认，不追溯问责执行端。
**教训**：P6 探针断言（cnt99==3）对齐了实现而非设计语义——探针绿≠语义对，探针断言必须先对齐设计字面。

### 5.4 批C′ 修正指令（按 D413 执行）

1. **纳土对象=建筑脚下中区块本身**：无主→纳入+广播（坐标序）；有主（他国/己方）→静默零变更（自动满足裁4）。
2. **阵营口径**：AI 建造也纳脚下格——事实占有不分阵营，1 格/栋与 ExpandTick 同量级且有建造成本天然节奏，不构成绕容量门的扩张引擎；接线维持 Building.OnConstructionComplete（此语义下的正确落点，无需改回 BuildController）。
3. **更名**：ClaimAdjacentUnclaimed 名不符实 → ClaimFootprintChunk（或同类名实相符命名；调用点+探针随改）。
4. **P6 断言翻转**：①脚下无主→纳入+广播 ②脚下有主（他国）→零变更（裁4 负探针）③邻接格不动。重跑 Smoke_12 全套+P0 基线。
5. **文档**：0.6 补 D413（策划端本裁已落）；HH.32 L29 L1 修订标记（漂移源）；2_17 设计文档不动（本就正确）。

### 5.5 卫生指令（索引漏登记）

27b74e3 提交信息与文件构成不符（声称索引登记、实际未动）——重申 HH.23 指令：**commit 信息只声明实际包含的文件改动**；涉及共享文件（索引/队列）回写时"写-改-commit 同串"，交付报告由策划端复核构成。本次 HH.36 索引行由策划端补登。