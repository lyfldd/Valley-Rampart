# HH.36 · 2_17 步骤12 批C 交付报告（玩家纳土 + ④债入档）· 待策划验收

> 类型：交付报告（Gate 收口，批次交付）
> 状态：🚧 批C 已完成，待策划验收（验收通过 → 队列 Q1 步骤12 全量批A/B/C 收官）
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

> 状态建议回写：HH.36 待策划验收；队列 Q1 步骤12 批C 完工（策划验收后置）；索引登记。