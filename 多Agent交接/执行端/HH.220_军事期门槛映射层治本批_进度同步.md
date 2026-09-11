# HH.220｜军事期门槛映射层治本批 · 进度同步（会话收工前落盘）

> 类型：**进度同步（半程）** · 状态：🟡**未完工**（用户指令：先保存，明日续）
> 执行端（TraeCode·Unity 轨）· 2026-09-11 夜 · 锚点：**HH.215 任务书（D663）**｜**HH.216 开工回执（D664 放行实施）**
> 取号：遵 D640 #10（水位线 → **HH.220**，独立单行 commit `636caed`）

---

## 一、已完成（带证据）

### 1. 实施 3 处（业务：`UtilityScorer.cs`）
| # | 落点 | 内容 |
|---|---|---|
| 1 | `D348Target(...)` | **尾插** `float internalDrive = 0f`；公式＝`floor + ⌈威胁×scale + max(0,drive)⌉ + stageFactor`（威胁面数学不变） |
| 2 | `MilitaryTargetFromThreat(...)` | **尾插** `internalDrive = 0f` 透传（纯探针默认 0 ⇒ 既有 5 参调用零退化） |
| 3 | `MilitaryTarget(k,cfg)` | 传**同源 helper** `InternalDrive(k)`（L-31 禁另抄） |

`internalDriveWeight` **保持 0.1 未动**（数值禁区）；含 D664 语义注记（drive＝阶跃项、权重不敏感、禁据此调权）。

### 2. 观测域（列报）
- `Valley_DiagMilitary.cs`：状态行 **+`drive=` 实读**（只读反射）。
- `Valley2_17_Smoke_5.cs`：**新增 `InternalDriveToGate` 正负例**（drive0.1⇒4≥门 / drive1e-4⇒4 / drive0⇒3<门 / 威胁面回归）。

### 3. 容器级（列报，R10 域内）
`Valley_HH80_Run.cs`：`SEED=64513`（复用 HH.214 定案）／`SLOT="p1_fix1"→"p1_fix1b"`／`CIRCUIT_BREAK_DAY=90`／**新增 `MILITARY_STOP_COUNT=1`**（短局机制自证 ⇒ 见 1 军事实达即收工；**⚠️七考重验批须复原 120/2**）。

### 4. 门禁（全绿）
| 项 | 结果 |
|---|---|
| 编译 | **0 错**（新 DLL 22:01:49/50） |
| `Smoke_2_22P0` | **run1/run2 = 32/0 ×2**（逐行一致，仅 tag/时间差） |
| `Smoke_2_23RP0` | **run1/run2 = 27/0 ×2**（逐行一致） |
| `Valley2_17_Smoke_5` | **ALL PASS**（含新 case：`内源势能接入=OK(…drive0.1=4≥4/drive1e-4=4/drive0=3<门/威胁面回归)`） |
| L-22／L-32 | 每轮退 Play 重进；两次收工均实测 `isPlaying=False` |

### 5. ✅ **正向短局实证成立**（seed 64513，槽 `p1_fix1`，15x，D65 提前收工，26.6 分钟）
- **`[KingdomBrain] k3 剧本阶段 → 军事 (Day 65)`**（**修前同 seed 该行 0 条**）；
- 收工档：`达标收工：≥1 AI 军事期（3）@D65`；
- **`militaryTarget` 修后常态 ＝ 4**（逐国分布：k1 3×22/4×42；k2 3×12/4×52；k3 3×22/4×41/**5×1**；k4 2×17/3×12/4×35）——**修前 HH.214 同 seed 全程 2~3、从未 ≥4**；
- 首达门时点＝**D14**（k2/k4，`drive=0.0500`）；k3 跨门窗：D64 `warrior=3,target=4` → **D65 `stage=Military, warrior=4, target=5`**；
- `drive` 实测 **0.0440~0.0880 >0**（判定依赖实证）。

### 6. 负探针（**半程·待续**）
SO `SituationConfig.asset` `internalDriveWeight` 临时置 **0** ⇒ 槽 `p1_fix1b`：**D1~D50 实读 `drive=0.0000`、`target` 全程 2~3（从未 ≥4＝死滞复现）**；因收工于 D50/90 停止 ⇒ **「无 →军事」整窗腿未跑满（待明日补）**。

### 7. (B)(C) 诊断（**初步取证，正式报告待明日**）
- **(B) 资源链**：`[DiagCapacity] D30` ⇒ **k3 `Stone:prod=1/in=100`** 而 **k1/k2/k4 `Stone:prod=0/in=0`**（Wood 亦 prod=0）；k1 census **③BuildCapacity ＝ 0 次**（从未选建产能）⇒ 石产能缺口不自愈。saves 直读（`p1_fix1.json`，正则统计 58 条建筑）：**k1 ＝ castle/farm/House/mine×1/Warehouse×4/Well/wall×1（无 quarry）** vs **k3 ＝ Barracks×2/quarry×3/TrainingCamp×3/wall×9** ⇒ 对照鲜明（k3 有石产能→建军链通；k1 无→⑦ 无建筑前置）。
- **(C) 焦点霸占**：⑨ asset 实读 `need=WallGap`／**`needA=8`**／`costStone=2、costWood=2`／`buildTargetCap=0`；`WallGap need = clamp01((needA−have)/needA)`（**存量缺口型单调·无失败退避**）＋[KingdomBrain.cs:1193](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L1193) 选址失败仅「明日再试」⇒ k1 wall 仅 1 座（have≪8 ⇒ need 长期 ≈0.875~1.0）；census top `BuildWall` k1 **42**/k2 **52**/k4 **52** 天（k3 仅 9）。⇒ 与 HH.189「选址失败 181 次」**同链**（占顶＝need 单调 × 几何特定失败 × 无退避）；**是否「同因」待明日以失败计数/运行数据正式定论（禁预设，D663 明令）**。

## 二、明日待办（按序）

1. **负探针补齐**：SO `internalDriveWeight` 置 0 → 槽 `p1_fix1b` 跑满 90 日 ⇒ 取「`target` 全程 ≤3 ＋ **无 →军事**」整窗证据 → **SO 复原＋git 校验**。
2. **(B)(C) 正式诊断报告**（根因＋`file:line`＋「是否结构性」结论；(C) 明确「同因/异因」）。
3. **交付报告**（按账本实时水位线取号）＋ 回写（账本/索引/工作日志/队列）⇒ 策划端验收 ⇒ 验收成立后 **七考重验（长局 ≥2 AI）**。

## 三、当前状态与环境（明日恢复入口）

| 项 | 状态 |
|---|---|
| Play | **已退**（`isPlaying=False`；L-32 兑现） |
| SO 测试件 | **已复原 `internalDriveWeight: 0.1`**（`git status` 该文件**无 diff**） |
| 容器常量 | `SEED=64513`／`SLOT="p1_fix1b"`／`CIRCUIT_BREAK_DAY=90`／`MILITARY_STOP_COUNT=1`（**七考重验批须复原 120/2**） |
| 未提交改动 | 本批 **4 文件**（`UtilityScorer.cs`／`Valley_DiagMilitary.cs`／`Valley2_17_Smoke_5.cs`／`Valley_HH80_Run.cs`）——随本进度同步**同串 commit**（D640 #9 保护半成品） |
| 证据位置 | `Logs/P1/p1_log_20260911_231630.log`（正向·D65）／`p1_log_20260911_234416.log`（负探针·至 D50）／`hh80_run_status.log`／`smoke_2_22p0_run1\|2.log`／`smoke_2_23rp0_run1\|2.log`；`Saves/p1_fix1_day005~065`×13＋`p1_fix1.json`／`p1_fix1b_day005~050`×10 |
| 红线自检 | 业务改动仅 `UtilityScorer.cs`（3 处·结构接线非调参）；AI.Core 零触碰；未 push；SO/枚举无新增（L-28 N/A） |

---
*执行端 2026-09-11 夜（HH.220）。明日从 §二-1 起续。*
