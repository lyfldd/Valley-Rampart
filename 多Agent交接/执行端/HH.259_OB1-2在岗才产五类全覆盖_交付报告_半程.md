# HH.259「产能须在岗·五类全覆盖（OB1-2）」交付报告（半程·独立部分）

> 类型：交付报告（半程）｜状态：✅ **已裁决（D704·2026-09-13·主策划端）**——原状态：🟡 待验收（本半程）＋🔴 **A 批待策划端勘正**
> 日期：2026-09-13 · 发起端：执行端 · 关联：**HH.257 施工任务书**（D703）／HH.258 开工回执／D677／D702／D673／台账 `DZ-123`/`DZ-145`
> 取号：HH.259（账本水位线 258→259·独立单行 commit `cf86ccf`）
> Gate：`G1-2`

---

## 一、本半程交付（含证据）

### 1. 死分支/存档残留全链删除（D677 预裁）—— ✅ 已交付·编译 0 error

| 面 | 落点 | 处置 | 证据 |
|---|---|---|---|
| 金矿死分支 | `ProducerComponent.cs` | 删 Tick 内金矿分支＋`TickGoldToTreasury()`＋`_goldAccumulator` | 现文件已无 `TickGoldToTreasury`/`_goldAccumulator`（grep 0 命中） |
| 副产子系统 | `ProducerComponent.cs` | 删 `_hasByproduct`/`_byproductType`/`_byproductAmount`/`_byproductCapacity`/`_byproductAccumulator`／`UpdateByproductConfig()`／`RestoreByproduct()`／`ByproductType`/`Amount`/`Capacity`/`Full`／Tick 内副产累加块 | grep 0 命中 |
| 存档字段 | `BuildingSaveData.cs:29-31` | 删 `byproductType`/`byproductAmount` | grep 0 命中 |
| 存档读写点 | `Building.cs` `SaveState`/`LoadState` | 删 623-624 写入点＋679-680 读取点（连带删两处已无用的 `producer` 局部） | grep 0 命中 |
| 工厂调用点 | `BuildingFactory.cs:408-409` | 删 `producer.RestoreByproduct(...)` | grep 0 命中 |
| **保留（D677 非删面）** | `MineByproductComponent` | `byproductCrystal/FireOil/Ore` 三子仓 + `SaveByproductState`/`RestoreByproductState` **逐字未动** | grep 残留仅此（`MineByproductComponent.cs:24/136`、`Building.cs:678`） |

**删前 grep 实证（D677 核项·L-29 阳性对照）**：
- Producer 副产判据 `outputResource == Ore` 的生产路径**唯一可能命中者＝`ore_vein.asset`**（`outputResource: 4`＝Ore），但它 `isResourceNode: 1` ⇒ 被 `BuildingFactory.cs:295` 的 `!def.isResourceNode` **排除产能分支**（不挂 `ProducerComponent`）⇒ **无生产路径消费**。（全 38 个建筑 asset 的 `outputResource` 已逐值核：仅 `ore_vein`=4/Ore。）
- 其余 `byproductType/byproductAmount` 消费者**仅存档读写链路**（即本次删除面本身）。

**编译门**：`refresh_unity(scope=scripts, compile=request)` → `read_console(types=error)`＝**0 条**；复证 `execute_code`→`isCompiling=False isUpdating=False isPlaying=False`（未进 Play）。

### 2. `15_账本` 承接账（水井豁免 D673 ＋ sim 协议差异 D702）—— ✅ 已交付

- 落点：`ai决策大脑强化训练/15_训练侧harness与Unity端差距文档.md` 新增 **「一·补二十九 产能「在岗才产」口径 与 sim 协议差异（OB1-2 承接账·D702/D673）」**（①sim 协议差异：`SimEconomy`「分配即产」vs Unity「动态在岗 `Working`」·D702 V5+·sim-sync 零义务；②水井豁免理由：隐藏资源·不占存储·走水网·代码不动；③待落地项追踪）。
- 提交：**训练仓 commit `c8995095`**（该仓为嵌套独立 git 仓·被主仓 `.gitignore` 忽略；**只提该 1 文件** `+24/-1`，未卷该仓既有在飞改动）。
- 口径依据：用户本会话裁「A 我直接写 `15_账本`」（HH.258 §待确认项）——认「❌不改训练仓」＝不改 harness 代码/数据，账本文档可写。

### 3. HH.258 开工回执（含结构性报裁）—— ✅ 已交付

- `多Agent交接/执行端/HH.258_OB1-2在岗才产五类全覆盖_开工回执.md`（commit `65ec932`）：sim-sync 结论复述 D702＝零义务；§0 L-30 全链门 gap 表 8 环逐环勾选；**M3 决定性发现**（三组件均无 `Production` 派工链 ⇒ `HasWorkerAssigned` 不可达）；M5 触发；报裁 R1~R4。

### 本会话 commit 串（**未 push**）

| commit | 内容 |
|---|---|
| `6598d0b` | 取号 HH.258（水位线 256→258，补记 HH.257） |
| `65ec932` | HH.258 开工回执＋索引＋在途区（+147） |
| `2535016` | 死分支全链删除（4 files·`+6/-119`） |
| `c8995095` | （训练仓）`15_账本` 承接账（`+24/-1`） |
| `cf86ccf` | 取号 HH.259 |

---

## 二、本半程**未交付**（及原因）

| # | 项 | 状态 | 原因 |
|---|---|---|---|
| 1 | **三处新增在岗门**（铁匠铺／投掷机厂／矿洞副产） | ❌ 未做 | HH.258 §四：三组件**均无 `Production` 派工链** ⇒ 仅补门将恒停产（与验收线 1~3「有在岗⇒正常产」及 M5 保底冲突）。用户裁 R2＝**A（补派工链＋落门）**，R1＝**先做独立部分**（本会话停手于三处门）。 |
| 2 | **A 批（补派工链＋落门）** | 🔴 待勘正 | 见 §三（需策划端重定范围/验收/派工语义）。 |
| 3 | **三行为「恒产→在岗」下游文档承接账** | ⏳ 待 A 落地 | 涉 `D562/HH.107`／`D609/D199~201`／`HH.19/D207~212` 下游文档与「恒产」表述清残——**须待门控落地后改，否则 doc-vs-code 反向分叉**（现已在 `15_账本` ③ 留追踪注记）。 |
| 4 | **三处正负探针**（验收线 1~3） | ⏳ 待 A 落地 | 依赖门控＋派工链。 |
| 5 | **M5 AI 保底逐条核** | ⏳ 待 A 落地 | 施工前已预判触发（HH.258 §五），落地后须实跑核。 |

---

## 三、待决策：A 批（补派工链＋三处在岗门）**勘正请示**

> 用户本会话已裁 **R2＝A**（补派工链＋落门）。但 A **超出 HH.257 任务书 §一 范围**（原＝「三处补在岗门」·验收线 7「`Assets/_Game/**` 仅本批文件·上限 4 cs」），故按 `vr-triage-flow`/`execute-checklist` 须**先勘正再施工**。请策划端裁以下 3 点：

1. **范围勘正**：授权动 `Building.cs`（`TryAdvertiseTask` 增「专属产能组件（`BlacksmithBuilding`/`SiegeWorkshopBuilding`）也发 `Production` 任务·source＝建筑」）＋ `MineByproductComponent.cs`（增 `Production` 广告）。⇒ 改动面上限由 **4 cs → 6 cs** 重定。
   - 请裁 **矿洞副产**的派工语义：其 `TryAdvertiseTask` **同一 tick 只能返一个任务**（`TaskScheduler` 每源每 tick 调一次）——选项：**(a·推荐)** 无工人在岗 ⇒ 发 `Production`（worker 到场`Working`→副产），有工人在岗 ⇒ 优先 `Transport`（搬运链照旧）；**(b)** 副产门改为「任意任务（Production **或** Transport）在该组件 `Working`」；**(c)** 其它。
2. **验收勘正**：验收线 7「零改动面」重定；验收线 1~3 正/负探针在 A 落地后跑（须附 `L-34` 五列在线判据表·若跑局）。
3. **承接账时点**：`D562/HH.107`／`D609`／`HH.19` 下游文档的「恒产」改写在 A 落地后由本端一并执行（同批），是否照准？

---

## 四、红线自检 / 边界声明

- **改动面**：主仓仅 **4 个 cs**（`ProducerComponent`/`BuildingSaveData`/`Building`/`BuildingFactory`）＋文档；**`AI.Core`/sim/训练仓代码/SO/`TaskScheduler` 主体路由 零触碰**。
- **训练仓**：仅改 `15_账本` 文档（经用户裁 A）；该仓**既有在飞改动未卷**（只 `git add` 本文件）。
- **未进 Play / 未跑局**：未触发 `EnterTestRun`，无跑批（L-32/L-34 无关）；`isPlaying=False`。
- **写-改-commit 同串·只提本批文件·未 push**。
- **改前重读**：`ProducerComponent.cs` 全文实读后再改（agent-handoff §六）。
- **证据口径（L-35）**：断言的"口径来源"＝磁盘实读 `file:line`＋`grep` 阳性对照；排除项＝未跑局（纯静态＋编译门）。
- **教训**：`L-29`（grep 阳性对照）／`L-24`/`L-25`（判据三直读）／`L-30`（第 4 环全链门）／`L-21`（出口存在 ≠ 出口可达）／`L-35`。

---

## 五、下一步建议

1. **策划端裁 §三 3 点**（范围/验收/矿洞副产派工语义）。
2. 裁后可即时施工 A：`Building.TryAdvertiseTask` 加分支 → `MineByproductComponent` 加 `Production` 广告 → 三处落 `HasWorkerAssigned` 门 → 编译 → 三处正负探针（正门 `EnterTestRun`＋退 Play）→ AI 保底逐条核（M5）→ 三行为承接账清残 → 交付报告。
3. 若策划端认为 A 应独立立批：则本端按 §二 表把本批（死分支删除＋`15_账本` 承接账）结算验收，A 另签任务书。

---

> 执行端（TraeCode）｜2026-09-13｜本报告＝HH.257/OB1-2 **半程交付**；A 批待裁。

---

## 六、策划裁决（策划端回写 · D704 · 2026-09-13）

> **判据三直读＝已过（实读非采信转述）**：**①设计稿全链**＝HH.257 任务书（D703）＋`2_24 §8.1` 定案表/§8.2/§6.x V5 行＋HH.258/259 逐字＋教训库原文；**②代码落点实读**＝`Building.cs:933/953-961`／`BuildingFactory.cs:295-322`／`TaskScheduler.cs:138-149/38-47/254/846-848/577-583/1101-1105`／`ProducerComponent.cs:24/79`；**③独立复核**＝删除面 7 符号 grep **零命中**／保留面 `byproductCrystal/FireOil/Ore` 在场（`Building.cs:607/623-626/678`＋`BuildingSaveData.cs:31-33`＋`MineByproductComponent.cs:130/136`）／`git show --stat 2535016`＝**恰 4 files `+6/-119`**／全 37 建筑 asset `outputResource` 逐值核——`4`(Ore) **唯一** `ore_vein.asset:39`／未 push。

### 6.1 半程交付验收＝✅ **成立·销号**

| 项 | 判定 | 依据 |
|---|---|---|
| **死分支/存档残留全链删除**（D677） | **✅ 成立** | 独立复核：`TickGoldToTreasury`/`_goldAccumulator`/`_hasByproduct`/`UpdateByproductConfig`/`RestoreByproduct`/`byproductType`/`byproductAmount` **全 7 符号 grep 零命中**；保留面（矿洞副产三子仓＋`Save/RestoreByproductState`）在场；`git show --stat 2535016`＝恰 4 files `+6/-119`（与报告一致）；**删前核项已闭**——全 37 asset `outputResource` 逐值核，`4`(Ore) **唯一** `ore_vein.asset:39` 且 `isResourceNode:1` 被产能分支排除 ⇒ 无生产路径消费。 |
| **编译门** | **✅ 成立** | 0 error（存量分离）·未进 Play。 |
| **`15_账本` 承接账**（sim 协议差异 D702＋水井豁免 D673） | **✅ 成立** | 训练仓独立 commit `c8995095`（只提该 1 文件）；口径依据＝用户裁「A 我直接写 `15_账本`」——认「❌不改训练仓**代码/数据**」，账本文档可写。 |
| **零改动面 / 未 push** | **✅ 成立** | 主仓仅 4 cs＋文档；commit 串 `6598d0b`/`65ec932`/`2535016`/`c8995095`/`cf86ccf` 未 push。 |

### 6.2 `§三` 三项勘正 ＋ 附带第 4 问（逐条裁）

| # | 请示项 | **裁决** | 理由 / 口径 |
|---|--------|---------|-------------|
| 1 | **范围勘正**（动 `Building.cs`＋`MineByproductComponent.cs`·4cs→**6cs**） | **准 A 的执行；范围重定＝授权「三处产能组件自身获得 `Production` 广告能力」；❌ 不授权动 `Building.cs`**（含 `TryAdvertiseTask` 分支结构）。**改动面上限重定＝A 段 3 cs／OB1-2 全批唯一 cs 面 7**（4 删除面＋3 门面）。**报告「6cs」算错**（漏计 `BlacksmithBuilding`/`SiegeWorkshopBuilding` 两个门文件）。 | ①`Building.TryAdvertiseTask` 是**全建筑共用派工枢纽**，加专属组件类型判别＝耦合 + 副作用波及全库；②`TaskScheduler.cs:1101-1105` 已内建 `Component→父建筑` 归属路由 ⇒ 组件自持**零新增机制**（先例 `MineByproductComponent`）；③合 2_24「去特判/单轨」。**可行性已实读闭合**（`:846-848`/`:577-583`/`:690`）⇒ 非空口否决。 |
| 2 | **验收勘正**（零改动面重定＋探针） | **准**。线 7 重定＝白名单 7 cs；「零改动面」明文＝`AI.Core`/sim/训练仓代码/数据 SO/`TaskScheduler` 主体路由/`Building.TryAdvertiseTask` 分支结构。线 1~3＝负探针＋正探针＋**同 seed 修前/修后对照**（M7 两态×有无在岗·**硬断言"无在岗产量恒 0"**）＋`L-34` 五列在线判据表＋`L-35` 口径来源与排除项·**走正门**。**新增硬验收＝§8.1 第 5 条 AI 保底两段门**（前段＝施工前只读「AI 工人占用账＋三产面可达性预判」·L-30 式 gap 表含**值 gap＋时间 gap**·**预判不过⇒报裁不得施工**；后段＝落地后实测三处产量 >0＋`stone in>0`＋⑦/⑰ 可进池·**结构性不可达⇒报裁不停工**）。 | 线 7 原文「上限 4 cs」与 §一 面数**自相矛盾**（签发侧缺陷·本裁一并改）；AI 保底前段门理由＝**HH.221 血案**（"A 落地后才知判定线不可达"）不得重演。 |
| 3 | **矿洞副产派工语义**（同 tick 单任务） | **裁 (a) ＋两条钉死**：**(a-1)** 门＝`HasWorkerAssigned(本组件)`（任意任务类型 `Working` 均算在场；与 (b) 的**门是同一个**，仅广告序不同）；**(a-2)** 广告序＝**硬 if/else**（无在岗⇒发 `Production`；有在岗⇒走既有三仓 `Transport` 判定），**禁同 tick 双发**（`TaskScheduler.cs:254`）。**铁匠铺另计**＝门须 `HasWorkerAssigned(组件) ‖ HasWorkerAssigned(_building)`（因 ③搬运广告 source=建筑）。 | (a) 的"有在岗优先 `Transport`"若不做硬互斥，会退化为"有在岗则不补派 `Production`"⇒ 生产任务 2s 完成即离场 ⇒ 门反复开合、副产断续。 |
| 4 | **承接账时点** | **准**：A 落地后同批清残（`D562/HH.107`、`D609/D199~201`、`HH.19/D207~212` 下游文档＋`15_账本`）。 | doc-vs-code 反向分叉风险（报告已自陈）；**清残判据须写「误表述语义零残留」并给正确措辞正例**（`L-26`·禁纯关键词零命中）。 |

### 6.3 分歧裁决记录（`design-advisor` §分歧裁决专项 · 双方原文保留）

- **执行端意见（原文保留·未采纳）**：方案 A「在 `Building.TryAdvertiseTask` 增『专属产能组件（`BlacksmithBuilding`/`SiegeWorkshopBuilding`）也发 `Production` 任务·source＝建筑』分支」。
- **策划端意见**：不动共用派工枢纽，改由三处产能组件自持广告（source＝组件）。
- **裁决＝采策划端**。**依据**＝范围纪律（共用枢纽 blast radius ＞ 组件自持）／兼容风险（`Building.cs` 副作用波及全库建筑）／AI 北极星（两形态对 AI 等价）／成本（复用既有 `Component→父建筑` 路由·零新增机制）。
- **落败方意见保留存档＝本条**（不删除·留作复盘依据）。

### 6.4 验收三问（`vr-planner-leadership` 钩子2）＋ 教训

- **①为什么发生**＝签发侧（HH.257）把「**判定口范式在场**」（`ProducerComponent.cs:144` 可参照）当成「**判定口输入链在场**」；且把 `L-30` 第 4 环「门控后可达」标为"🔜施工后实证"——而该环的**前置条件（`Production` 广告源）结构性缺失、施工本身不产生它** ⇒ 施工后才验＝必然白工。
- **②单次失误 or 流程漏洞**＝**策划流程漏洞**（签发侧）。
- **③库缺条目 or 有条没查**＝「**从未入册**」该维度 ⇒ **`L-30` 家族＋1**（并入硬性检查项：gap 表须分列〔前置条件在场性｜签发前必闭〕与〔施工后可达性｜可留施工后〕）。**伴生认账**＝HH.257 §一 列 **7 面**（3 门＋4 删除面）而验收线 7 写「上限 4 cs」＝**签发侧自相矛盾**；防范动作补「改动面上限须按 §一 逐面点数核算」。
- **嘉奖**＝执行端在**开工回执阶段**即抓出阻断级结构事实（未等施工后才知）＋`file:line` 全列＋三处自毁/循环路径逐一推演＋守 M1「先报裁再施工」**零业务代码改动**。

### 6.5 衍生产物

- 新建设计文档：**无**（裁决即口径·回写 HH 与 `0.6 §二百三十三`）。
- 新建清单任务：**无新签任务书**——A 批以 **HH.257 尾部勘正块**为准（范围/验收线 7 勘正）；完成报告按账本实时水位线取号。
- 台账：`DZ-123` 加注；`DZ-145` **✅已清**；`DZ-135` 加注（§8.1 第 5 条前置门）；`DZ-124` 不受影响。

> 策划端（主策划端）｜2026-09-13｜回写五处：本区 · `0.6 §二百三十三` · 账本（D704） · `_任务队列.md` · `缺陷台账.md`（外＋`_交接索引.md`／`_策划教训库.md`／HH.257 勘正块／HH.258 §九）。
>
> **接管声明（入场卡 §四 条件1）**：`workbuddy 接管（2026-09-13）`——用户显式指定「你现在是主策划」⇒ 本会话按全权策划端执行裁决与回写；写-改-commit 同串·只提本批文件·未 push。
