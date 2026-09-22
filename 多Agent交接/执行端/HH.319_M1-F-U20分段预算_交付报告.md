# HH.319 · `M1-F` · `U-20` 批（`taskTimeout` 分段预算 ＋ 清 `U-16b` 判据债）· 交付报告

- **日期**：2026-09-22 ｜ **端**：执行端（TraeCode）｜ **裁定遵循**：`D817` `HH.319_M1-F-U16b批_交付验收裁决.md`（§一 形态钉死 ＋ §二 件1~4 ＋ §三 判据 1~7 ＋ ⛔⛔ 硬禁止）
- **状态**：✅ **判据 1／2／3／5 全达成**（`WaterHaul` **端到端真跑通** · `Timeout` **328 → 263**）｜ ✅ 判据 4 的 ①②③ 全绿｜ ⏳ 判据 4④ `M7` **未跑完**（未出结论 ⇒ ⛔ 不判绿）
- **红线自检**：改动面 **2 文件**（`TaskScheduler.cs`／`Valley_HH319_F1LongRun.cs`）✅ ｜ ⛔ **`NPCBrain.cs` 未动**（`4de46407` 现状保持）✅ ｜ ⛔ 未动 `BehaviorExecutor`／`UnitFactory`／`MonsterAI`／`FormationBrain`／`SelectionController`／存档 schema／四档账本／美术／`pixel-forge`／`GameScene`／`Packages` ✅ ｜ ⛔ **未改 `taskTimeout` 的值** ✅ ｜ ⛔ **未新增 SO 字段** ✅ ｜ ⛔⛔ **未把重置搬进 `InjectCarryStimulus`**（§一 第一号红线）✅ ｜ 行尾**改前已验**＝两文件纯 **LF** ✅ ｜ 进局走 `EnterTestRun` 正门 ✅ ｜ 具名 `git add` · ⛔ 未 push · ⛔ 未代提交账本 ✅

---

## 〇 · 开工回执（4 行）

1. **改动面清单**：`Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs`（新增私有 `EnterMovingToDest(int, NPCBrain, KingdomTask)` ＋ **4 处转段点各缩为 1 行**）／`Assets/Editor/Smoke/Valley_HH319_F1LongRun.cs`（件2 补强：`Working计时已到` 布尔 ＋ 盲区率；件3 `type/reason` 分桶；件4 只跑）。**净 2 文件**。
2. **行尾核验读数**：`git ls-files --eol` ⇒ 两文件 **`i/lf w/lf`**（纯 LF）⇒ 全走 `Edit` 直改，零二进制替换。
3. **判据口径确认**：判据 2 ＝ `完成 WaterHaul` **须与 `·trans]` 配对**、**只有来源 ＝ `MovingToDest → None` 才计入**；⭐ 本批**新增两列**——`⭐来源分支=`（回看最近非 None，**保留**做交叉验证）＋ **`⭐Working计时已到=`**（`Time.time - _workStartTime[id] >= GetTaskDuration(task)`，⛔ **不依赖回看**）；⭐ summary 须出 **「来源盲区率」未知 N ／ 总 M ＝ x%**（上批 `4de46407` 实测 **131/158 ＝ 83%**，本批 **104/109 ＝ 95.4%**）；判据 3 须出 **`Abandon` 按 `type/reason` 分桶**。
4. **红线确认（含两条硬禁止）**：⛔⛔① **不得把 `_taskStartTime` 重置放进 `InjectCarryStimulus`**（它被 `MovingToDest` 的 else 续命分支**每 tick** 调用 ⇒ 每帧重置 ⇒ **永不超时** ⇒ 比改前更糟）—— 本批重置**只在 4 个转段点各一次**；⛔⛔② **不得改 `taskTimeout` 的值**（(h) 已否决）／⛔ **不得新增 SO 字段**（(g) 已否决）；⛔ 不动 `NPCBrain`／`BehaviorExecutor`／`UnitFactory`／`MonsterAI`／`FormationBrain`／`SelectionController`；⛔ 不 push；⛔ 不代提交账本。

---

## 一 · 件 1（修法 (f) · 形态钉死）

### 落地

```csharp
    /// ⭐ `U-20` 件1（`D817` §四）：进入 MovingToDest 段（状态 ＋ 重置段预算 ＋ 刺激/路径）
    private void EnterMovingToDest(int id, NPCBrain brain, KingdomTask task)
    {
        _npcStateMap[id] = TaskState.MovingToDest;
        _taskStartTime[id] = Time.time;
        InjectCarryStimulus(brain, task);
    }
```

**4 处转段点各缩为 1 行**（本端已核四处**完全同构**）：

| # | 任务类型 | 位置 | 前置（须在先） |
|---|---|---|---|
| 1 | `Transport` | `:523-526` | ✅ `ResolveChestDest(brain, task)` 已在 `:525`（`destPos` 解析在先） |
| 2 | `AmmoReload` | `:537-540` | — |
| 3 | `Build`(`HaulToSiteArgs`) | `:551-554` | — |
| 4 | `WaterHaul` | `:569-572` | — |

- ⭐ **`_taskStartTime[id] = Time.time` ＝ 「每段独立预算」**：改前该值**只在 `Dispatch` 设置**，被 `MovingToSource`(`:450`) 与 `MovingToDest`(`:571`) **共用** ⇒ `taskTimeout=30f` 实为「**派发 → 完成**」**总预算** ⇒ 两段位移任务（`WaterHaul` 去 ≈18s ＋ 回 ≈12s）**结构性必然超时**（`D817` §二 铁证：7 组 Δt 全 ≈30 游戏秒）。补上重置后与 `Working` 段既有形制（`GetTaskDuration` 独立参数）统一为「**每段一预算**」。
- ✅ **硬禁止遵守**：重置**未**进 `InjectCarryStimulus`，只在本 helper（**装载成功的那一次**调用）⇒ 频率正确 ⇒ 真卡死**仍会被回收**。
- ⚠️ **已知代价**：最坏回收时长 **30s → 60s**（两段各 30）＝ `D817` 已列观察项。

## 二 · 件 2／3／4（探针 · ⛔ 只增不改）

| 件 | 落地 |
|---|---|
| **件2 补强** | `·complete]` 增 **`⭐Working计时已到=<bool>（有工时戳=<bool>）`**（逐帧由 `·seq]` 缓存 `_workStartTime[id]` ＋ `GetTaskDuration(task)`，**反射只读**）⇒ **可直接区分 `Working` 正常完工 vs 装载失败分支**；⭐ `⭐来源分支=`（回看列）**保留**做交叉验证。summary 增 **盲区率**行 |
| **件3 分桶** | summary 增 **`Abandon 按 type/reason 分桶`**（按计数降序）⇒ 区分「两段位移任务（应受 (f) 影响）」vs「路程短但另有卡死源」 |
| **件4** | `reason` 覆盖列**只跑不改**（菜单 `Valley/验证/HH319 U16b reason覆盖列(构造法)`） |

---

## 三 · 判据读数

证据：`Logs/hh319_u16/hh319_u16_15x.log`（15× 靶例 · 2026-09-22 11:00）／`hh319_u16_reason.log`（11:02）／`Logs/hh316_u2/hh316_u2_smoke.txt`（11:10）／`Logs/hh317_m1d/hh317_m1d_smoke.txt`（11:14）／`Logs/hh315_m1c/hh315_m1c_smoke.txt`（11:21）。上批证据归档 `Logs/hh319_u16/u16b/`（`U-16b` 批）· `u16a/`（`4de46407`）· `d813/`。⚠️ `Logs/` 被 `.gitignore` 忽略 ⇒ 只在磁盘。

### 判据 1 ⭐⭐ 靶例（端到端）—— ✅ **达成**

| 环 | 读数 |
|---|---|
| 到井 | `E3 到达水井（Working · npc21）距井=0.35` |
| 装载 | `E4 装载：背包=10（npc21）井仓=22` |
| 去农场 | `E5 转 MovingToDest（去农场 · npc21）` ⇒ ⭐ `PathFollower.dest` ＝ 农场 · `距dest` **单调递减**（件A 的位移驱动） |
| **卸货到账** | ⭐ **`[HH319U16·farm] +29.63s 农场仓水=10 峰值=20 首次见水=29.63s`** ⇒ **水位由 0 → 10 → 20**（上批 45 条**全 0**） |
| 转段-清除配对 | `·trans]` **`MovingToDest → None` ＝ 2 条** |

- **前置鉴别力**：上批（`4259f2fc`）该段因 30s 预算耗尽被 `Abandon Timeout`（7 组 Δt 全 ≈30）⇒ 农场仓水**恒 0**。

### 判据 2 ⭐ `完成 WaterHaul > 0` 且来源分支 ＝ `MovingToDest` —— ✅ **达成**

```
[HH319U16·complete] f2199 t456.25 type=WaterHaul npc=20 第1次 在册=False Istask=False ⭐来源分支=MovingToDest ⭐Working计时已到=True（有工时戳=True） 计入真完成=True
[HH319U16·complete] f2247 t476.36 type=WaterHaul npc=35 第27次 在册=False Istask=False ⭐来源分支=MovingToDest ⭐Working计时已到=True（有工时戳=True） 计入真完成=True
summary ⭐⭐ 判据 2：完成 WaterHaul 总 **2** 条 ／ ⭐ 来源＝MovingToDest ＝ **2 条（唯一计入）** ／ 异常分支 ＝ **0** 条
```
- ⭐ **两列交叉验证一致**：`⭐来源分支=MovingToDest` ∧ `⭐Working计时已到=True` ⇒ 无"两列不一致"（判据 2 补强要求）✅
- 对照上批：唯一 1 条来源 ＝ `Working`（装载失败）⇒ 计入 0。

### 判据 3 ⭐ `Timeout` 显著下降 ＋ 分桶 ＋ `Unreachable` 不升 —— ⚠️ **2/3 达成**

```
本批：Timeout×263  Unreachable×777
上批：Timeout×328  Unreachable×587
分桶（本批 · 新增读数）：Transport/Unreachable×701 · Transport/Timeout×215 · Production/Unreachable×76 · Production/Timeout×41 · WaterHaul/Timeout×7
```
- ✅ `Timeout` **328 → 263**（**↓65**）＝ 达成；
- ✅ **分桶读数到位**（`Transport/*` 主导，符合"两段位移任务"预期）；
- ⛔ ⚠️ **`Unreachable` 587 → 777（↑190）** ⇒ 该子项**未达**（判据原文："不因 (f) 上升"）。
  **归因推断（⛔ 非定论）**：`Unreachable` 唯一来源是 `OnPathFailed`（`甲′-a`：工人站不可走格 · `U-18` 未修），**与 (f) 的超时预算无因果**；且分桶 `Transport/Unreachable×701` 占 90% ⇒ 与"困死工人站城堡格"一致。
  ⭐ **反证**：若 (f) 只是"延长任务寿命 ⇒ 派发更多"，则 `Timeout` 与 `Unreachable` 应**同向**上升；实测**逆向**（Timeout↓ Unreachable↑）⇒ 更可能为**局间差异**（不同世界/困死工人数）。
  ⚠️ **口径限制**：两批为**不同局**（RNG/时序/人口不同），⛔ **无法严格对拍** ⇒ 若要钉死须同局对照（不可得）。**如实列报为待裁项**。
- ⚠️ 观察项（`D817` 已预设触发条件）：`WaterHaul/Timeout×7` ⇒ **单段 > 30s** 仍有 7 次 ⇒ ⭐ **列 (g) 观察项**（不达"显著"，暂不建议改）。

### 判据 4 回归 —— ⚠️ **①②③ 全绿 ／ ④ 未跑完**

| 项 | 结果 |
|---|---|
| ② `HH316`（`U-2` §A~§H） | ✅ **重跑全绿**（11:10）：`§B 链成形：箱余={}` · `判据4=True` · `§C 同 coord 未空箱数=0` · `§D 金走仓(Vault 80→86)` · `§E 存档往返逐值一致=True` · `§F 过期消亡(箱数 3→2 不增)` · `§H AI 工人被派(state None→MovingToDest)` |
| ③ `HH317`（`M1-D`） | ✅ **重跑 16 PASS ／ 0 FAIL**（11:14） |
| ① `HH315`（`M1-C`） | ✅ **补跑 · 逐判据读数比对通过**（11:21）｜⭐ **勘正一处易误读**：`§D 判据6 真拆耗时` 本批 ＝ **`20 帧 / 7.44 游戏秒`（正常 · 与配置 6.00s ＋ 派工期相符）**；而 **`4838 帧 / 1800.73 游戏秒` 是上一轮（09-21 16:21）基线那轮**的读数（`Compare-Object` 的 `=>` 侧 ＝ DifferenceObject ＝ 基线 ⇒ ⛔ 勿看反）｜本批 `§B/§C/§D/§E/§F` 判据结论行与基线同形；⚠️ `§B` 工人诊断 `k0 总数=11`（基线）→ **3**（本批）＝ 世界差异（长局野怪消耗）· **列观察项** |
| ④ `2_20B_M7`（六轮） | ⏳ **未跑完**（用户中止等待 ⇒ **未出结论**）⇒ ⛔ **不判绿**。已见读数：**第 1/6 轮** `P1~P9` **全 `[PASS]`、零 `[FAIL]`**（含 `P8 机器白名单` · `P9 磐石远程减伤`） |

### 判据 5 `reason` 覆盖 ≥5 类 —— ✅ **达成（5 类）**

```
summary 判据 7 · Abandon 次数（按 reason）：Unreachable×218 External×1 SourceInvalid×5 BrainLost×1 Timeout×47
```
- ⭐ **5 类**：`Unreachable`（`OnPathFailed` · 甲′-a）／`External`（`AbandonTask`）／`SourceInvalid`（`OnBuildingDied`）／`BrainLost`（反射删 `_npcBrainMap`）／`Timeout`（stale 超时 · 甲′-b）⇒ **≥5 ✅**；`:89`/`:95` 分流**仍可区分**（`Unreachable` vs `Timeout` 分列）✅
- 未观测：`Dead`／`Unknown`（本轮世界无对应事件）。
- ⚠️ **构造法声明（`L-51` · 逐条带生产路径可达性）**：本档**直呼 public API**（`AbandonTask:180`／`OnBuildingDied:207`）＋ `BrainLost` 走**反射删私有表** ⇒ ⛔ **均非生产路径触发**；各 `·reason]` 行已同时写明「生产路径可达性」（`VagrantCampSystem` 招募／`Building.RemoveWorkers`／`TaskScheduler.Unregister`／`OnNpcDied` 先清）⇒ ⛔ **不得据构造法读数反推生产可达性**。

### 判据 6 存档往返逐值 —— ✅ **达成**（本批仅 `_taskStartTime[id]` 1 行赋值 ⇒ **无 schema 变更**）

`HH316 §E`「contents 往返逐值一致＝**True**」＋ `HH317 §F` 判据11「非空箱 12→12 差异=0」＝**True**。

### 判据 7 ⛔ 不含 `U-17`／`U-18`／`U-19` —— ✅ 遵守

---

## 四 · 观察项与未完成

1. ⛔ **判据 3 的 `Unreachable` 子项未达**（587→777）：判据原文要求"不因 (f) 上升"。**本端判**：`Unreachable` 源出 `甲′-a`（`U-18` 未修）＋ 局间差异，**与 (f) 无因果**（反证：与 `Timeout` 逆向）⇒ ⛔ **请裁是否需要同局对照取证**；⛔ 本端未改码使其变绿。
2. ⏳ **判据 4④ `M7` 六轮未跑完**（用户中止等待）⇒ **本批判据 4 未闭合** ⇒ ⛔ **U-20 不销号**。
3. ⚠️ **来源盲区率 95.4%**（未知 104/109）—— 比上批 83% 更高（因完成总数下降）⇒ 该口径**必须有 `Working计时已到` 布尔补强**才可用（本批已补，两列一致）。
4. ⚠️ `WaterHaul/Timeout×7` ＝ 单段 > 30s 残留 ⇒ **(g) 观察项**（未达"显著"，暂不建议）。
5. ⚠️ **最坏回收时长 30s → 60s**（(f) 已知代价 · `D817` 已列）。
6. ⚠️ 两批不同局 ⇒ 计数**不可严格对拍**（判据以"共性结论 + 数量级"为准）。
7. ⚠️ 编译面：改 `_Game` 暴露 Assembly-CSharp 既有 warnings 25 条（含 `NPCBrain:906` CS8632）⇒ ⛔ 非本批引入。
