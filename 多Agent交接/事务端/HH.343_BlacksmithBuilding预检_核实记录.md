# HH.343 · 第 4 源 `BlacksmithBuilding` 独立只读预检窗口 · 事务端独立核实记录

> 事务端（砚）· 2026-09-30 · 对象 ＝ 执行端交付报告 `多Agent交接/执行端/HH.343_BlacksmithBuilding预检窗口_交付报告.md`
> 性质：**独立核实**（⛔ 不改判定内容、⛔ 不判绿、⛔ 不补证据、⛔ 不动源码/资产/场景/协议、⛔ 不 push、⛔ 不取新 D 号）
> 唯一判据来源 ＝ `多Agent交接/策划端/HH.341_M5-D乙加_小源④_BlacksmithBuilding预检任务书.md`（`ddc57869`）＋ 阶段任务书
> **总判定 ＝ 「通过」**（6 项交付标识齐 · 3 项统计闭合 · 4 步闸门齐 · 零改动成立）＋ **2 处具名勘正建议（`N-A`／`N-B` · 不退回）** ⇒ 回呈主策划另裁施工窗口
> 格式：每项 ＝ 判据／复现命令／**实盘读数**／影响面／结论（通过 · 补正 · 停手）

---

## 〇、核实基线与被核实对象（全部独立复算）

| 项 | 报告/任务书给定 | 本端独立复算 | 结论 |
|---|---|---|---|
| 当前 HEAD | `c38ea25c` | `c38ea25c1b8ed2a5e73004a1082409b5039d2231`（`git rev-parse HEAD`） | 通过 |
| 开工基线 HEAD | `4989c00e416869c0c8253e21f3f8d682ca2361d1` | `git rev-parse 4989c00e` ＝ 同值 | 通过 |
| 报告盘值 | 270 行 / 46460 B / sha256 `7B19BA78…` | `wc -l`＝270 ／ `wc -c`＝46460 ／ `sha256sum`＝`7b19ba78…c441e68` | 通过 |
| 报告 git blob（c38ea25c） | `1b29b0a9…` | `git rev-parse c38ea25c:<报告>` ＝ `1b29b0a93be510e7b26e8f18d26ed8e4ba94d9e4` | 通过 |
| 提交链 | `ff4c1e2f`(268) → `ee9462b3` → `c38ea25c` | `ff4c1e2f`＝`f87e4a3d…`（`git show \| wc -l`＝268）／`ee9462b3`＝`b24c6059…`（270）／`c38ea25c`＝`1b29b0a9…`（270） | 通过 |
| 证据 `stat.py` | 24504 B / `d8619854…` | `ls`＝24504 ／ `sha256sum`＝`d8619854…d2d96b70` ／ `certutil`＝同 | 通过 |
| 证据 `evidence.txt` | 61880 B / 388 行 / `710E50E8…` | `wc -c`＝61880 ／ `wc -l`＝388 ／ `sha256sum`＋`certutil` 双口径 ＝ `710e50e8…eed80a53` | 通过 |
| 证据 `gatecheck.py` | 12412 B / `2a2431ba…` | `ls`＝12412 ／ 双口径 sha256 ＝ `2a2431ba…c3904f0` | 通过 |
| 证据 `gate.txt` | 71601 B / `3D8FA86F…` | `ls`＝71601 ／ 双口径 sha256 ＝ `3d8fa86f…db65a27` | 通过 |

⚠️ 证据四件位于 `Valley Rampart/Logs/`，被 `.gitignore:89` 整体忽略 ⇒ **只能磁盘直读**（本端未用 `git show` 取，⛔ 未覆盖，核实全程只读）。

---

## 一、A 六项交付标识（任务书 §十一-4）

### A① 开工 HEAD 实测值
- 判据：报告 §1 给「开工 `git rev-parse HEAD` 实测」值。
- 复现：`git rev-parse 4989c00e`；`grep -n 'S BASELINE' Valley Rampart/Logs/hh341_small_blacksmith_precheck_evidence.txt`。
- 实盘读数：`4989c00e416869c0c8253e21f3f8d682ca2361d1`；evidence.txt 行 213 `## S BASELINE step=git rev-parse HEAD rc=0 value=4989c00e416869c0c8253e21f3f8d682ca2361d1`。
- 影响面：**值正确**。
- 结论：**通过（值）**；⚠️ **归位指针瑕疵 ⇒ 见 `N-B`**。

### A② 工作树状态全量逐行
- 判据：`git status --short --untracked-files=all` ⇒ `total=241 modified=5 untracked=236` ＋ `## S STATUS_LINE` 全录。
- 复现：`git status --short --untracked-files=all | wc -l` 等；`grep -c 'STATUS_LINE' gate.txt`。
- 实盘读数：`total=241`（`modified=5`／`untracked=236`，`awk substr` 分类：`M`×5、`??`×236）＝ gate.txt 行 6 `## S STATUS total=241 modified=5 untracked=236 rc=0` ✓；`STATUS_LINE` 全录 241 行（gate.txt 行 7–247）✓；modified 5 ＝ `GameScene.unity`／`packages-lock.json`／`pixel-forge/index.html`／`pixel-forge/server/forge.mjs`／`pixel-forge/server/serve.mjs`。
- 影响面：**全量逐行照录正确**。
- 结论：**通过**；⚠️ **报告 §2-2 文字归组失实 ⇒ 见 `N-A`**。

### A③ §2.1/§2.2 允许面逐条「本轮是否触碰」
- 判据：报告 §2-3 表 19 行 ＋ gate `## S FORBID`／`## S AGG rowset=FORBID`。
- 复现：`grep -c 'S FORBID file=' gate.txt`。
- 实盘读数：报告 §2-3 表逐条「未触碰（只读）」；gate.txt 行 250–268 ＝ **19 行 `## S FORBID`**，每行 `blob_identical=TRUE`；行 269 `## S AGG rowset=FORBID … denom=19 raw=19 excl=0 valid=19 domain=blob_identical_TRUE:19|…FALSE:0`。
- 影响面：允许/禁止面逐条已核。
- 结论：**通过**。

### A④ `GameScene.unity` hash 只登记（属 `O-14` 挂账）
- 判据：只登记 sha256、不回滚。
- 复现：`certutil -hashfile "Valley Rampart/Assets/Scenes/GameScene.unity" SHA256`。
- 实盘读数：`9bb5aaf7cb9c81bc915e197648c1ca7da64aab28b853d9a439abc6af385f0c1e` ＝ 报告 §2-4 与 gate 行 274 `## S O14 … hash_object=9BB5AAF7…` **一致**；`git hash-object`（blob id）＝ `4a86f26f…`（与 sha256 异算法，**不是**同一读数）。⚠️ gate 字段名 `hash_object` 实存 sha256（**命名与值不符 · 值正确**）。
- 影响面：登记值正确；`O-14` 继续挂账未动。
- 结论：**通过**（附字段命名瑕疵登记）。

### A⑤ 证据文件行集三数
- 见 §二 B 项。

### A⑥ 卡片适用性判定（§5.1/§5.2 二择一 ＋ 证据）
- 判据：报告 §3.4 二择一。
- 复现：直读 `BlacksmithBuilding.cs` 全文 ＋ `TaskScheduler.cs` 相关区间。
- 实盘读数：判定 ＝ **任务卡源**（§5.1）；四要件逐条有 `file:line` —— 真实任务对象 `BlacksmithBuilding.cs:92 new KingdomTask(Production, this)` ✓；调度器收集/派发 `TaskScheduler.cs:322→:327→:339→:365→:420-452` ✓；四能力链 ✓（见 §五）；源有效性与收口 `IsValid :77` ＋ 三条收口 ✓。
- 影响面：判定与证据链一致。
- 结论：**通过**。

---

## 二、B 三项统计闭合（任务书 §十一-4）

#### B① 逐行求和 ＝ AGG
- 判据：`SRC_SELF` 各 pattern 求和 `26→4→22`；`FILEKIND` `109＝code75/comment18/blank16`。
- 复现：`PYTHONIOENCODING=utf-8 python "Valley Rampart/Logs/hh341_small_blacksmith_precheck_stat.py" --dry`（⛔ 只读，**不带 `--dry` 会覆写证据** ⇒ 本端只用 `--dry`，⛔ 未覆写）。
- 实盘读数：`SRC_SELF` 10 pattern 分项 `SchedRef 6/0/6`＋`NewKT 1/0/1`＋`ITaskSrcDecl 3/2/1`＋`Advertise 1/0/1`＋`RegisterOrUnregister 2/0/2`＋`HasWorkerAssigned 5/2/3`＋`Tickable 2/0/2`＋`Saveable 5/0/5`＋`OnDestroy 1/0/1`＋`ProtocolToken 0/0/0` ⇒ 求和 `raw 26 / excl 4 / valid 22` ＝ `## S AGG rowset=SRC_SELF_SUM … raw=26 excl=4 valid=22` ✓（本端脚本复算逐项一致）；`## S AGG rowset=SRC_SELF_FILEKIND … raw=109 excl=34 valid=75 domain=code:75|comment:18|blank:16` ⇒ `75+18+16=109` ✓、`excl 34＝18+16` ✓。
- 影响面：求和闭合。
- 结论：**通过**。

#### B② 跨口径不混用
- 判据：两口径并列、⛔ 不混用。
- 复现：核 evidence 行 2／129／200 等。
- 实盘读数：`splitlines=109` vs `nl_count=108`（META_FILE 行 2）**并列** ✓；「命中行数」vs「匹配次数」**并列**（`ProtocolRuntimeAny 命中行数 17 ≠ 匹配次数 21`，行 129；`ALLREF 命中行数 14 ≠ 匹配次数 688`，行 200）✓；`ALLREF` 生产域 `_Game 9` ＋ Editor 域 `5` 与生成物桶 `674`（字节级）**分列**、⛔ 未把全 Assets 对照数当生产调用面 ✓。
- 影响面：口径并列清晰。
- 结论：**通过**。

#### B③ 零计数显式
- 判据：零计数一律显式，⛔ 不用「零接缝」四字替代明细。
- 复现：核 evidence 零计数行。
- 实盘读数：`ProtocolToken 0→0→0`（行 27 `zero_flag:1`）✓；`TS_REF.ClsName 0→0→0`（行 54 `zero_flag:1`）✓；`TaskCard`／`hasDeadline` 各 `0`（行 111／130 `zero_flag:1`）✓；`DEFDATA` 键行 `1/37` 资产（行 137）✓；`SEAM_SUM` `domain=…|BlacksmithBuilding:0` ＋ `note=本源命中_0_为显式零计数`（行 90）✓；`Saveable` 混合 pattern 下「存盘三标识子 token 命中 0」**在报告 §4 口径⑤具名**（evidence Saveable 5 行逐行 `token=_metalAccumulator` 可核）✓。
- 影响面：零计数可核。
- 结论：**通过**。

---

## 三、C 四步闸门（任务书 §九 · **第 4 步由本端独立复算，不采信报告叙述值**）

| 步 | 判据 | 复现命令 | 实盘读数 | 结论 |
|---|---|---|---|---|
| 1 mtime 变化 | 写入前/后 mtime 变 | gate `## S GATE_FILE`／`## S GATE` | stat.py `11:12:17.425`／evidence `11:15:01.105`／gatecheck `11:21:41.397`／gate `11:25:40.345`；evidence 本窗口被写 2 次（早 `11:01:31` → 末 `11:15:01`）已登记 | 通过 |
| 2 磁盘重读 | ⛔ 不复用内存缓冲 | gate `## S GATE_REREAD` | 四件首尾锚点逐字登记（`GATE_REREAD tag=EVIDENCE head/tail_anchor…`） | 通过 |
| 3 sha256＋长度 | 第二口径独立复算 | `sha256sum` ＋ `certutil -hashfile … SHA256` | 四件双口径**逐一相等**（见 §〇表） | 通过 |
| 4 提交 vs 磁盘 | `ls-tree` oid ＝ 磁盘 blob；`files=1` | `git ls-tree -r c38ea25c \| grep HH.343`；`git hash-object <报告>`；`git show --name-only --format= c38ea25c \| wc -l` | `ls_tree_oid`＝`1b29b0a93be510e7b26e8f18d26ed8e4ba94d9e4` ＝ `disk_recomputed_git_blob_id`（`git hash-object` 磁盘报告）＝ 同值 ⇒ **byte-identical**；`files_in_commit=1`（`single_file=TRUE`）；`## S GATE_COMMIT_SCOPE … files_in_commit=1` ✓ | 通过 |

⚠️ **报告 §9 第 4 步正文只列首版 `ff4c1e2f`（blob `f87e4a3d`）核对**（终版 `c38ea25c` 的核对在 gate 文件）；报告已显式声明「自指」限制（⛔ 不在正文写死自身终值）—— 与 `L-96` 自指族一致，**met**。

---

## 四、基线复算（任务书 §八 · 命令逐字 · HEAD 与 WT 两跑）

| 行集 | 期望 | 本端实盘（HEAD `4989c00e` / WT） | 结论 |
|---|---|---|---|
| `TaskScheduler\.(Instance\|HasInstance)` | 76 行/17 文件 | **76/17** ／ **76/17** | 通过 |
| `new KingdomTask\(` | 15 行/10 文件 | **15/10** ／ **15/10** | 通过 |
| 字面 `ITaskSrcDecl` | 2 行/2 文件 | **2/2**（`WorkerTask.cs:58`、`WorldGatherSource.cs:36`）／**2/2** | 通过 |
| 语义口径 `class X[[:space:]]*:.*ITaskSource` | 9 行/9 文件（含本源 `:12`） | **9/9**（含 `BlacksmithBuilding.cs:12`）／**9/9** | 通过 |
| `git diff --name-only HEAD -- 'Valley Rampart/Assets/_Game'` | 空 | **空输出（rc=0）**；`--numstat` 亦空 | 通过 |
| `git grep -n 'BlacksmithBuilding' -- …/Systems/AI/Core` | rc=1 空 | **rc=1 且空输出** | 通过 |
| 本源 `BlacksmithBuilding.cs` 的 `SchedRef` | 6 行 | `:68 :90 :99 :100 :106 :107` ＝ **6 行** | 通过 |
| 本源 `NewKT` | 1 行 | `:92` ＝ **1 行** | 通过 |

`ALLREF`（全 `Assets`）本端复算：`BuildingDef.cs 2`／`BuildingComponents.cs 1`／`MineByproductComponent.cs 1`／`BlacksmithBuilding.cs 1`／`SiegeWorkshopBuilding.cs 4`（生产域 9）＋ `R3_SupplyChain.cs 2`／`Valley_OB12_InGate_Probe.cs 3`（Editor 域 5）＋ `UnitOptions.db`（生成物桶单列）⇒ **命中行数 14 ＝ 生产 9 ＋ Editor 5** ✓。

**零改动证明**：`git diff --name-only HEAD -- _Game` 与 `--numstat` 均空 ✓＋ 19 禁止面 blob 等 ✓ ⇒ **生产面 diff ＝ 0**（不触发任务书「发现生产面 diff 非零即停手」条款）。

---

## 五、三个已知易失实点（逐条验，非泛读）

1. **E 类完成接缝只有 3 调用行（无 `WorldGatherSource` 支）** —— 实盘：`TaskScheduler.cs:731/732`（`ConstructionSiteStore`）／`:734/735`（`ChestEntity`）／`:737/738`（`MineByproductComponent`）＝ **3 行**；`## S AGG … class=E … domain=…|WorldGatherSource:0`（显式零）。⇒ **未被「补成 4」，结论通过**。
2. **B 类 `:532` 非接缝（`ChestEntity` 在途豁免判定）须单列 `role=非接缝型判定`** —— 实盘：evidence 行 75 `## S SEAM class=B(到达) line=532 role=非接缝型判定 … text=if (!(task.source is ChestEntity && st == TaskState.MovingToDest))`；`## S AGG class=B … 调用行=4;判定行=5`（差额已定位）；五类 `4/4/4/3/4＝19` ＝ `## S AGG rowset=SEAM_SUM … raw=19 … |BlacksmithBuilding:0 note=本源命中_0_为显式零计数`。⇒ **单列＋闭合＋本源 0 显式，结论通过**。
3. **接缝行号随第 3 源入码整体位移 ⇒ 以当前盘逐行验** —— 本端在**当前盘** `git grep` 逐行核：
   - D 判定行 `274/277/280/283`（调用 `275/278/281/284`）✓
   - A 判定行 `432/435/438/441`（调用 `433/436/439/442`）✓
   - B 判定行 `554/557/560/563`（调用 `555/558/561/564`）＋ 非接缝 `532` ✓
   - E 判定行 `731/734/737`（调用 `732/735/738`）✓
   - C 判定行 `779/782/785/788`（调用 `780/783/786/789`）✓
   ⇒ **与报告 §5／evidence 逐行一致**；本色位移按第 3 源 `N-A` 先例判「**位移 vs 内容改动**」＝ 位移，⛔ 不据此判执行端失实。

---

## 六、具名勘正项（⛔ 不退回 · 请主策划裁是否要求执行端勘正 commit）

### `N-A` · 报告 §2-2「工作树状态」文字归组失实（⚠️ 自述层 · 不改任何读数）
- **判据**：报告 §2-2 行 29 写「其余 modified（`Packages/packages-lock.json`、`pixel-forge/*` ×3、`最高优先级文档/*` ×2）与全部 untracked 均为本轮开工前既有差异」。
- **复现**：`git -c core.quotepath=false status --short --untracked-files=all | grep -E '最高优先级文档|^ ?M '`。
- **实盘读数**：`modified(M) 总数 ＝ 5`（`GameScene.unity`／`packages-lock.json`／`pixel-forge/index.html`／`forge.mjs`／`serve.mjs`，**无 `最高优先级文档`**）；`最高优先级文档` **3 行全部为 `??`（untracked）**：`19_王国AI决策循环与打断契约.md`／`20_个体NPC决策与神经策略契约.md`／`底层生命周期契约优化审查.md`；`untracked(??) 总数 ＝ 236`。
- **影响面**：报告把 `最高优先级文档/*` **误归入 modified**，且数量写 **×2**（实际 **untracked ×3**）⇒ 若读者据此核 `modified` 列表会**对不上**（叙述 7 vs 实盘 5）。⚠️ 但 §2-1 的 `total=241/modified=5/untracked=236` **正确**，gate 全录正确，**零改动判据不受影响**。属 `L-02` 家族「报告自述层与真源脱钩」。
- **结论**：**补正建议（不退回）** —— 建议执行端勘正 §2-2 该句（归组改 `untracked`、数量改 `×3`），或主策划裁「登记加注即可」。

### `N-B` · 报告 §1 开工 HEAD 归位指针错误（⚠️ 自述层 · 值正确）
- **判据**：报告 §1 行 17 写「**开工 `git rev-parse HEAD` 实测**＝`4989c00e…`（分支 `main`；**证据登记于 gate 文件 `## S BASE`**，非推算）」。
- **复现**：`grep -n 'S BASE' Valley Rampart/Logs/hh341_small_blacksmith_precheck_gate.txt`；`grep -n 'S BASELINE' …evidence.txt`。
- **实盘读数**：`gate.txt:3 ## S BASE step=rev-parse HEAD rc=0 value=c38ea25c1b8ed2a5e73004a1082409b5039d2231` —— **gate 的 `## S BASE` 记的是闸门时 HEAD ＝ `c38ea25c`，非 `4989c00e`**；`4989c00e…` 实际登记于 **`evidence.txt:213 ## S BASELINE step=git rev-parse HEAD`**。⇒ 按报告指引去 gate `## S BASE` 找 `4989c00e` **找不到**。
- **影响面**：开工 HEAD **值正确**（`4989c00e` 确为开工基线、确在 evidence 登记），唯**指针指错文件/段**（gate `BASE` ＝ 闸门时 HEAD；开工 HEAD 在 evidence `BASELINE`）⇒ 证据链**可追溯性**受损，不影响读数与判定。
- **结论**：**补正建议（不退回）** —— 建议勘正归位为 `evidence ## S BASELINE`，或主策划裁「登记加注即可」。

> 两处均**不触发**任务书硬补正条件（交付标识**未缺失**／统计**已闭合**／闸门**齐**／生产面 diff **＝0**）⇒ 本端**不退回执行端**，仅具名登记并回呈。

---

## 七、总判定与回呈

- **A 六项**：①值对（指针瑕疵 `N-B`）②全录对（归组失实 `N-A`）③逐条对 ④hash 对 ⑤三数对 ⑥判定对 ⇒ **齐**。
- **B 三项**：求和闭合 ✓／口径并列 ✓／零计数显式 ✓ ⇒ **闭合**。
- **C 四步**：齐（第 4 步本端独立复算通过）⇒ **齐**。
- **生产面**：`_Game` diff ＝ 0 ⇒ **零改动成立**。
- **总判定 ＝ 「通过」**：预检四项取得 · 判定＝任务卡源 · 接缝五类 19 行本源 0 · `sim-sync` 义务 0 · 零改动成立。
- ⛔ **本端不判绿、不写施工结论** ⇒ 状态 ＝ **预检已核实 · 待主策划裁定是否施工**。
- ⭐ **回呈主策划**（选项＋推荐＋影响 · 承 `agent-handoff` 待决策格式）：
  - **① 是否开第 4 源施工窗口**（推荐：**准开**，同 `ChestEntity`／`MineByproduct` 同型先例；影响＝解锁第 5 源前须完成 施工→进局回归→跨档→`L-95`→基线与事务核实）。
  - **② 接缝形态**（本源五类命中全 0；推荐：照第 2/3 源同型补 `D/A/B/C` 四类回调 ＋ `E` 完成接缝；⚠️ 本源**循环型常驻源（无源终态）＋ 双源并存**，`Done/封口` 语义须主策划定）。
  - **③ 完成判据归属（`V4`）**：`ExecuteCompletion` Production 支（`TaskScheduler.cs:864-867`）取 `ProducerComponent`，本源数据行无 `comp.producer`（`Blacksmith.asset:16-18`）⇒ 对本源**空操作**；本源实产唯一承载＝`ProductionSystem.cs:50→BlacksmithBuilding.cs:43-61→StorageComponent.cs:245-265`。
  - **④ 升级不刷产率（`V8`）**：`Building.cs:724-725` 升级路径只喂 `ProducerComponent`，本源 `_rate` 不随 `statScale` 更新（容量线 `:726-730` 却跟级）。
  - **⑤ §八 字面 `ITaskSrcDecl` 命令的 POSIX 字符类缺陷**（`[^\n{]` 实为排除反斜杠/字母 `n`/左花括号）⇒ 字面 2/2 vs 语义 9/9；本源 `:12` **只在语义口径**（本端复现确认）⇒ 后续源基线命令是否统一改语义口径。
  - **⑥ `N-A`／`N-B` 两处自述层勘正**是否要求执行端另出勘正 commit（推荐：**登记加注即可**，因不涉读数与判定）。

---

## 八、自持与合规声明
- 本端未执行任何源码/资产/场景/协议写动作；`git diff -- _Game` 空 ⇒ 生产面零改动。
- `O-14`（`GameScene.unity`）本轮**只登记 hash**，⛔ 未回滚、⛔ 未 `checkout --`、⛔ 未整文件 `restore`（`L-99`）。
- 证据四件**全程只读**（`--dry` 复算；本端另跑 `sha256sum`＋`certutil` 双口径），⛔ 未覆写、⛔ 未改 mtime。
- ⛔ 未 `git add -A`、⛔ 未 push、⛔ 未取新 D 号、⛔ 未申请第 5 源。
