# HH.344 · 第 4 源 `BlacksmithBuilding` 施工窗口 · 事务端独立核实记录

> 事务端（砚）· 2026-09-30 · 对象 ＝ 执行端交付报告（提交 `fad50855` ＋ `810bd436`；终版 blob `79b27ea6`）
> 依据 ＝ `D922` 裁决与执行工单 ＋《M5-D 乙加 五小源阶段任务书》§七/§八/§十/§十一 ＋ `vr-planner-leadership §4.1` 判据三直读
> **总判定 ＝ 「施工合规 · 核实通过」**（边界严丝合缝 · 读数主体成立 · 证据可复算）＋ **3 处自述层瑕疵登记（⛔ 不退回）**
> ⛔ 本端不改判定内容、⛔ 不判绿（判绿权在主策划）、⛔ 不替执行端补证据、⛔ 不动源码/资产/场景、⛔ 不 push、⛔ 不取 D 号
> ⚠️ **生产码（`BlacksmithBuilding.cs +257`／`TaskScheduler.cs +18`）保留工作区未入库** —— 源码入库时点按先例（`D921` 判绿时才准提交）归主策划裁。

---

## 〇、基线与被核实对象（全部独立复算）

| 项 | 报称 | 本端实盘 | 结论 |
|---|---|---|---|
| HEAD | `810bd436` | `810bd4364f2a6e7ab2524b1509f304d178147fe9` | 通过 |
| 交付提交面 | 两笔单文件 | `fad50855`（1 file，216 插入）／`810bd436`（1 file，13 插入）＝ **各单文件** | 通过 |
| 报告 blob 对账 | `79b27ea6 == 79b27ea6` | `git ls-tree -r 810bd436` ＝ `git hash-object <磁盘>` ＝ `79b27ea6ba376d5086313aada1b550313d5ddd84` | 通过 |
| 报告盘值 | — | 229 行／29052 B／`sha256 22f29b48…` | — |

---

## 一、A 组 · 合规与边界（硬门槛）

| # | 判据 | 复现命令 | 实盘读数 | 结论 |
|---|---|---|---|---|
| 1 | **`O-14` 未碰** | `certutil -hashfile "Valley Rampart/Assets/Scenes/GameScene.unity" SHA256` | `9bb5aaf7cb9c81bc915e197648c1ca7da64aab28b853d9a439abc6af385f0c1e` ＝ **HH.343 预检基线逐位同** | 通过 |
| 2 | 允许面改动面 | `git diff --numstat -- <两文件>` | `BlacksmithBuilding.cs` **+257/−0**、`TaskScheduler.cs` **+18/−0** ＝ 报称一致 | 通过 |
| 3 | **允许面外生产域 diff ＝ 0** | `git diff --name-only -- 'Valley Rampart/Assets/_Game' \| grep -vE 'BlacksmithBuilding.cs\|TaskScheduler.cs'` | **空输出（rc=1）** | 通过 |
| 4 | 禁止面 blob（抽验 9 件） | `git rev-parse HEAD:<f>` vs `git hash-object <f>` | `ITaskScheduler`／`Building`／`ProductionSystem`／`StorageComponent`／`ChestManager`(真实路径 `Systems/World/`)／`BuildingFactory`／`BuildingComponents`／`Blacksmith.asset`／`BlacksmithDef.asset` —— **9/9 全等** | 通过 |
| 5 | `AI.Core` 零命中 | `git grep -n 'BlacksmithBuilding' -- '…/Systems/AI/Core'` | **rc=1 空** | 通过 |
| 6 | 四本账未碰 | — | 执行端 ⛔ 未写账本（HH.344 尚未入账 ⇒ 本端补登记，见 §六） | 通过 |
| 7 | 第 5 源未申请／未取 D 号／未 push | — | 报告 §12 声明；本地领先 `origin/main` **126**（未 push） | 通过 |

---

## 二、B 组 · 施工内容（对 `D922` ①②③④⑤）

| # | 判据 | 实盘读数 | 结论 |
|---|---|---|---|
| 1 | 接缝 `D/A/B/C＋E` 五类**均落地** | `TaskScheduler.cs` diff（`+18/−0`）＝ 5 处判定各增本源支：D `:287`、A `:448`、B `:574`、E `:752`、C `:806`；调用行 `:288/449/575/753/807` | 通过 |
| 2 | ⛔ 未动调度语义/政策值/非接缝 | diff **全新增、零删除**；`:539`（原 `:532`）`ChestEntity` 在途豁免**未动**；`taskTimeout=30f`／`workDuration=2f`／`SourceKingdom`／去重键／排序路由未改 | 通过 |
| 3 | `Done` **只封单卡不封源** | 本源 `OnProtocolTaskCompleted`：`_doneCards++; _slots.Remove(slot)`（不置 `_sourceInvalidated`）⇒ 源继续广告 | 通过 |
| 4 | 源真失效才 `D` ⇒ 全非终态卡 `TargetRemoved` | `OnProtocolSourceInvalidated`：`_sourceInvalidated=true` ＋ 逐槽 `FinalizeSlot(TargetRemoved)`；`OnDestroy → SealAllOnDestroy` 兜底 | 通过 |
| 5 | 建卡经 `TaskProtocolIssuer.Create/Submit` | 本源 `CreateSlot()` 走 `new TaskProtocolIssuer(rt, ref).Create(...).Submit(...)` ⛔ 未直接 `new TaskCard` 手填 | 通过 |
| 6 | `hasDeadline` **显式载体**（⛔ 非 `IsInfinity` 猜） | `Submit(card, 0, true, float.PositiveInfinity)` → `TaskCardProtocol.cs:364-365` 同点写入 `deadline=+∞ ∧ hasDeadline=false`；消费端 `TaskBindingManager.cs:282` 只读 `hasDeadline` | 通过 |
| 7 | `V4` 双层：任务收口 ＋ 真实产出；⛔ 未冒充、⛔ 无本源专用副作用 | 本源 diff **未触碰** `ExecuteCompletion`；产出证据全来自 `Tick → StorageComponent.Transform` | 通过 |
| 8 | `V8`：⛔ `Building.cs` 未动 | `Building.cs` blob 全等（§一-4） | 通过 |
| 9 | `_npcTaskMap` 仅镜像 | `MirrorCheck()` 逐次对拍 + `ProtocolMirrorConsistent` | 通过 |
| 10 | 双源并存未合并 | 本源只广告 `Production`；本体源 `Transport` 未见协议接缝（转 §五-1 待裁，⛔ 执行端未擅扩面） | 通过（含待裁项） |

---

## 三、C 组 · 证据充分性

| # | 判据 | 实盘读数 | 结论 |
|---|---|---|---|
| 1 | 基线复算（WT） | `SchedRef` **81/17** ✓（报称 81/17）；`NewKT` **15/10** ✓；语义 `ITaskSrcDecl` **9/9** ✓ | 通过 |
| 2 | 证据 9 件 `sha256` | 对报告 §9 自述**逐项一致**：`gate_pre DFD779AA…`／`gate_mid D13CB044…`／`gate_post E0A3FBA8…`／`cx_probe.txt FB5C5BBD…`／`.json D4BDDAF9…`／`v3.txt E8CD6EA2…`／`v3.json 84F27154…`／`l95b.log 2F21CEE5…`／`l95c.txt B0959551…` | 通过 |
| 3 | `L-95` 段②③ **原文在** | `l95c.txt` 断言 `playing=True activeMap=False bs=0 gs=Ready day=1 tick=29 ts=1`、`err=0 warn=0`、退出后复现同一条 `[SpriteAnimatorDriver]`；`l95b.log` 含该文案字节区域 | 通过 |
| 4 | `read_console` 故障处置 | 报告 §5.5 自报 3 次 `Unity did not respond within 2.0s`，⛔ 未降级结论、改双口径并列 ⇒ 合 `L-97` 「工具问题」三分 | 通过 |
| 5 | 停手条件自查（7 条） | 报告 §11 第 6 条自标「⚠️ 部分需事务端复核」⇒ 本端复核通过（三段齐／未强行降级／Error 双重排除） | 通过 |
| 6 | 四步闸门 | 报告 §13 四步回执齐（详见 `N-C′` 自指口径） | 通过 |
| 7 | `L-97` 全域取值域 | 报告 §5.2 给出 `subMetal/subCap/subFree/ore/rate/live/onBook*/reserv/pairs/invVio/tick/workerId/state/lastUnassignReason/abortReason` 全量取值域 ＋ `raw=530/excl=0/valid=530` | 通过 |
| 8 | `L-98` 观察窗 | 报告 §5.1 两臂同 seed／同规模／同正门／同 `timeScale=15`；§5.2 给零点 `t=3940.50`／三段时长（90.11＋60.04＋4.20＝154.35 游戏秒）／墙钟 10.92 s | 通过 |

---

## 四、瑕疵登记 3 项（⛔ 不退回 · 请主策划裁是否加注）

### `N-A′` · 报告 §4 `ClsName_ALL` 的 WT 值**两口径均复现不出** ＋ pattern 标注与实际不符
- **判据**：报告 §4 标称 `` `ClsName_ALL`（`\bBlacksmithBuilding\b`） ``：HEAD `9 行/5 文件` → WT **`20/6`**（+11/+1）。
- **复现**：`git grep -nE '\bBlacksmithBuilding\b' -- 'Valley Rampart/Assets/_Game' | wc -l`；及直白 `git grep -n 'BlacksmithBuilding' -- …`。
- **实盘读数**：**标称 pattern（`\b`）WT ＝ 13 行**（≠20）；**等价直白口径 WT ＝ 21 行**（≠20）；HEAD 侧两口径均 **9/5**（＝报称）。
  - 逐文件（直白口径 WT）：`TaskScheduler.cs 10`（＝报称 10 ✓）、`BuildingDef.cs 2`、`BuildingComponents.cs 1`、`MineByproductComponent.cs 1`、`BlacksmithBuilding.cs **3**`（报称 **2**）、`SiegeWorkshopBuilding.cs 4`。
- **差异定位**：`BlacksmithBuilding.cs` WT 实为 **3**（类声明 `:12` ＋ 区头注释 ＋ **`ability="BlacksmithBuilding_Task"` 字符串行**）；报告记 2 ⇒ **少计 1 行**（`BlacksmithBuilding_Task` 因 `\b` 在 `g`/`_` 间不成立而漏计，说明报告标称 `\b` 与实际口径不一致）。总数 20 vs 21 的差 **即此 1 行**。
- **影响面**：**方向性结论不变** —— 「仅本源 ＋ `TaskScheduler.cs` 两文件被触碰、文件数 5→6」在两口径下**均成立**（无第三文件）。属 `L-97` 补条②（正则锚定/口径标签）＋ `L-02`（自述数字与真源脱钩）家族。
- **结论**：**补正建议（不退回）** —— 建议勘正为「直白口径 `21/6`」或删去 `\b` 标注并补 `ability` 行；或主策划裁「登记加注即可」。

### `N-B′` · 报告 §2 `E` 类「先例支数」表述**自相矛盾**
- **判据**：报告 §2 写「`E` 类先例为 **4 支**（`:740` 为 Mine 侧的 `WorldGather` 空位口径），本源补**第 5 支**」；同段后句又写「`SEAMCX E hitline=4` ……与 `pre` 阶段 `E3` 的差值 ＝ 本源新增 **1 支**」。
- **复现**：`git grep -n 'OnProtocolTaskCompleted' -- '…/TaskScheduler.cs'`。
- **实盘读数**：E 调用行 ＝ **4 行**：`:743` site／`:746` chest／`:749` mb／`:753` bs ⇒ **先例实为 3 支（`D920`/`D921` 轮：`731/734/737`）＋ 本源 1 支 ＝ 4**。⇒ **后半句正确**，**前半句「先例 4 支／本源补第 5 支」失实**。
- **影响面**：仅表述层；`E` 支数与「本源新增 1 支」结论**成立**。
- **结论**：**补正建议（不退回）**。

### `N-C′` · 报告 §13 四步闸门记的是**首版**读数、未标「首版」
- **判据**：报告 §13 步3 记 `216 行 / 27851 B / 1a35a2dc…`、步4 记 blob `f661f398…`。
- **实盘读数**：`fad50855` 首版 = 216 行（216 插入 ✓）；**当前盘（`810bd436` 补记后）＝ 229 行 / 29052 B / `sha256 22f29b48…` / blob `79b27ea6…`**。⇒ §13 所记为**首版**态，补记后未更新（正文旧值未标「首版」）。
- **影响面**：⛔ 不影响提交一致性（本端独立复算 `79b27ea6 == 79b27ea6` ✓）；§13 末尾已有「第 4 步对账以该笔提交的 blob 为准」声明 ⇒ 属 `L-96` 自指族**部分已声明**（同 `HH.343` 已识别形态）。
- **结论**：**登记加注即可**。

> ⭐ **执行端自报项（核实成立）**：报告 §12 末尾「工单 §证据与提交要求『按水位线取号、先登记后落盘』↔ 同一工单 §边界『⛔ 不取 D 号、⛔ 不碰四本账』」**口径确相冲突**（本端比对工单原文属实）⇒ 执行端按后者执行、报告以窗口号命名、⛔ 未碰账本。**处置**：HH.344 由**事务端补登记**（先落盘后补登，属既定协调模式）。

---

## 五、回呈主策划（待裁项 · 承 `agent-handoff` 待决策格式）

1. **本体源 `Transport` 无协议接缝（报告 §10-1）** —— 本源为「组件源 `BlacksmithBuilding`（广告 `Production`）＋ 本体源 `Building`（广告 `Transport`）」双源并存；给 `Transport` 上接缝必须改 `Building.cs` ＝ **禁止面**。现状＝本体源任务只有旧链、无镜像卡。**选项**：(a) 维持本窗口只做组件源、本体源接缝另开窗口并先解禁 `Building.cs`；(b) 认可「循环常驻源接缝只挂组件侧」为五小源统一落点（先例：`MineByproductComponent` 同为组件侧）。**推荐 (b)**（与 `ChestEntity`/`MineByproduct` 同型、⛔ 不扩禁止面），影响＝本体源 `Transport` 继续旧链（登记观察项，本轮不阻塞判绿）。
2. **`V4` 长期口径（报告 §10-2）** —— 「本源完成动作 ＝ 空操作、产出真源在 `Tick`」是否定为**长期口径**（并写入文档）。**推荐 定为长期口径 + 写入 `08`／`06` 相关节**（与 `D920 Q3` Production 完成语义分层一脉）；影响＝后续源若结构相同可直接复用该口径，⛔ 不改 `TaskScheduler` 完成语义。
3. **`V13` 国库容量（报告 §10-3）** —— 开局 `CanAccept(Ore)=0`（Wood100/Stone100/Food34 占满 250 单容器）⇒ 铁匠铺原料门**天然不可满足**。**推荐 另开工单**（涉 `Blacksmith.asset`/主城容量口径 ＝ 本轮禁止面）；影响＝不修则铁匠铺生产链在真实开局**永不自举**（属玩法缺陷，非本源缺陷）。
4. **3 处自述层瑕疵（`N-A′`/`N-B′`/`N-C′`）处置** —— **推荐 登记加注即可**（不涉读数方向与提交一致性），或择一出勘正 commit。
5. **生产码入库时点** —— 报告 §12 称「待事务端核实后按裁决入库」。**推荐 循 `D921` 先例**：**判绿裁定时**才准具名提交本源 2 文件（`BlacksmithBuilding.cs` ＋ `TaskScheduler.cs`）；本轮核实 ⛔ 不入库。
6. **源级结论口径** —— 本轮止于 **「施工完成 · 已核实 · 待主策划裁定是否判绿」**（⛔ 未判绿、⛔ 未申请第 5 源 `SiegeWorkshopBuilding`）。

---

## 六、结论与落账

- **总判定 ＝「施工合规 · 核实通过」**：A 组 7 项／B 组 10 项／C 组 8 项全通过；边界零越界（`O-14` 未碰、允许面外 diff ＝ 0、禁止面全等）；读数主体可复算（基线 81/17·15/10·9/9、证据 9 件哈希逐项同）。
- ⚠️ 3 处自述层瑕疵登记（`N-A′` 统计口径／`N-B′` E 类表述／`N-C′` 自指），**⛔ 不退回**。
- ⛔ 本端不改判定、⛔ 不判绿；**状态 ＝ 施工完成 · 已核实 · 待主策划裁**。
- **落账**：本节 ＋ 台账 §二百三十九 ＋ `_编号登记.md`／`_任务队列.md`／`_交接索引.md`／`_当前快照.md`（含 **HH.344 补登记**，水位线 `HH.343 → HH.344`）。
- ⛔ 未 push；⛔ 未取 D 号；⛔ 未动源码/资产/场景。

（事务端 · 2026-09-30）


> ⚠️ **核实边界声明（诚实口径）**：本端**可独立复算**者 ＝ 提交面／blob／边界 diff／基线行集／证据文件 `sha256`／接缝与本源源码 diff（**均已逐项实盘复算**）；**不可复算**者 ＝ **进局读数**（v2/v3 两臂的 Metal/Ore 增量、计数器、跨档 ID 等 —— 需 Unity 正门重跑）。⇒ 本端所核 ＝ **「证据 9 件 `sha256` 与报告 §9 一致 ＋ 报告自述与证据原文一致」**，属**证据链完整性**，⛔ **不是读数级独立复现**。若主策划要求读数级复现，须另派跑批（`test-harness-first` 正门 · ⛔ 不得由本端代跑）。
