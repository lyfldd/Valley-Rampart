# HH.283 `G2-2` 专属兵·机器 prefab 补全 交付报告（10 prefab 落地 ＋ 生产链兜底退款）

> 类型：交付报告｜状态：🟢 **待验收**
> 日期：2026-09-15 · 执行端 · 依据：HH.282 任务书（D726）· Gate=`G2-2`
> 取号：**HH.283**（账本水位线 282→283 · `D640 #10` 禁预留）
> 开工回执：占 HH.282 号（已提交 `dd16ab11`）
> HEAD 基线：`8457e8e4`（本批实施）· **未 push**

---

## 〇、一句话结论

**件1（10 prefab 落地＋挂接）＝已施工并实机验证**（`UnitData` 中 `prefab:{fileID:0}` 计数 **0/35**；正门进局 10 单位逐一 `SpawnUnit` **全非 null**、真图帧 **10/10**、相机 15 格内可见 **兵=7 机器=3**，截图 `hh282_live.png` 1.93MB）。**件2（生产链兜底）＝已施工并实机验证**（玩家 Ballista 负探针：扣费 -8金-15石 → 生成 null → **退款 +8金+15石 回 100/100** ＋ 不报成功＋LogError；AI k1 VineCatapult 负探针：**退款回 per-kingdom 国库** G100→100 S150→150 W40→40 ＋ 不报成功＋LogError）。**回归全绿**：编译 0 error / `AI.Core` 零触 / 四族四轮冒烟不退化（2_20B_M7 六轮 + 2_20_Smoke_Race 全 ALL PASS）/ 收尾三态。**数值零改**：10 个 `UnitData` diff 仅 `prefab:` 行。

---

## §一、施工面清单（2 件 · 33 files）

| # | 文件 | 内容 |
|---|---|---|
| 1 | `Assets/Resources/UnitPrefabs/` 新增 **10 prefab**（＋meta） | 7 兵：`Dwarf_Bedrock`/`Dwarf_Musqueteer`/`Elf_DeerRider`/`Elf_Ranger`/`Elf_Windwalker`/`Orc_Berserker`/`Orc_WolfRider`；3 机器：`Dwarf_Mortar`/`Elf_VineCatapult`/`Orc_Ram`。步兵照 `Human_Player_Warrior` 模板（含 NPCBrain），机器照 `Human_Player_SiegeMachine` 模板（**无 NPCBrain**·scale 2）——组件集资产实读核验（M4 落位）。族别主题色 tint（3.1.2 D457/D458：矮人铜橙 0.70/0.40/0.15·精灵翠绿 0.20/0.60/0.30·兽人暗红 0.60/0.10/0.10） |
| 2 | `Assets/Resources/UnitData/` 10 个 `.asset` | 仅填 `prefab:` 字段（数值字段逐字未动·见 §七 验收线6） |
| 3 | `Assets/_Game/Systems/Kingdom/SiegeProductionSystem.cs` | 两个重载各加 `SpawnUnit==null ⇒ 退款原账户 + LogError + return false`（玩家 `RulerController.Refund`；AI `kingdom.Refund` per-kingdom 国库） |
| 4 | `Assets/Editor/Smoke/Valley_HH282_Verify.cs`（＋meta） | 验收探针（新建·正门 EnterTestRun）：正向 10 单位＋帧解析＋可见性＋截图＋玩家/AI 负探针（运行时注入 null·自还原） |

**改动面外声明**：`SpriteRefTable` / `ArtImportPipeline` / `GameScene.unity` / `MapGenRules.cs` / `AI.Core` / 训练仓 **零触碰**。

---

## §二、验收线 1｜prefab 落地（计数实证）

| 判据 | 证据 |
|---|---|
| 10 个 `.prefab` 在场 | 资产枚举（`AssetDatabase.FindAssets("t:Prefab", "Assets/Resources/UnitPrefabs")`）：10 个新 prefab 全部在场（路径清单见 §一） |
| `Resources/UnitData/*.asset` 中 `prefab: {fileID: 0}` 计数 = 0 | **MCP 资产枚举实证：`UnitData prefab=null count = 0 / 35`**（35 个 SO 全量扫描，10 个目标已挂接，其余 25 个原已挂接） |

**挂接验证**：10 个 `UnitData.prefab` 均非 null（探针输出 `prefab=Dwarf_Bedrock` 等逐单位）。

---

## §三、验收线 2｜实际可生成（核心·正门实机）

正门 `TestHarnessApi.EnterTestRun`（seed=21107/Medium/diff2/1x）后，对 10 个单位逐一 `UnitFactory.SpawnUnit(Faction.PlayerCamp, occ, pos)`：

| 判据 | 本次读数（`Logs/hh282_verify.log`） | 结论 |
|---|---|---|
| 逐单位 `SpawnUnit` 非 null | **10/10** 全非 null（7 兵＋3 机器逐一 PASS） | ✅ |
| 真图帧解析（SpriteRefTable 素材在场实证） | **10/10** `HasRealFrames`（`SpriteAnimator.EnsureSet` 运行时实读） | ✅ |
| 画面可见 | 相机 15 世界格内 **兵=7 机器=3**（全可见） | ✅ |
| 截图 | `screenshots/HH282_专属兵prefab/hh282_live.png`（**1,933,753 字节**·Game 窗口实拍） | ✅ |

---

## §四、验收线 3｜兜底生效（核心·负探针扣费前后对照）

**负探针方式**：运行时将某机器 `UnitData.prefab` 置 null（**内存态·不保存资产**，验完还原）→ 走生产入口 `ProduceMachine` → 扣费后 `SpawnUnit` 返 null → 触发本批兜底。

**玩家侧（Ballista·现行玩家族 Human 白名单允许）**——控制台实录：

```
[RulerController] 金 -8，当前: 92          ← Spend 扣费
[RulerController] 国库 Stone -15，当前: 85  ← Spend 扣费
[UnitFactory] UnitData 'Ballista' 未挂 prefab 引用，无法生成。   ← SpawnUnit 返 null
[RulerController] 金 +8，当前: 100          ← Refund 退款（回玩家账户）
[RulerController] 国库 Stone +15，当前: 100 ← Refund 退款
[SiegeProduction] 生产失败：Ballista 生成返回 null（UnitData.prefab 缺失或未挂接），已退款 金8 石15 木0  ← 不报成功 + 明确错误日志
```

- `ProduceMachine` 返回 **false**（不报"生产成功"）✅
- 扣费前后 Gold **100→100**（Spend -8 后 Refund +8，净零）＝退款回玩家账户 ✅
- **prefab 已还原**（`prefab 已还原=True`·资产文件零改动）✅

---

## §五、验收线 4｜AI 侧同验（退 per-kingdom 国库·非玩家账户）

**AI 侧（k1·国族 Elf → VineCatapult 白名单允许）**——控制台实录：

```
[UnitFactory] UnitData 'Elf_VineCatapult' 未挂 prefab 引用，无法生成。   ← SpawnUnit 返 null
[SiegeProduction] k1 生产失败：VineCatapult 生成返回 null（UnitData.prefab 缺失或未挂接），已退款 金10 石15 木0  ← 不报成功 + AI 退款日志
```

- `ProduceMachine(..., kingdomId=1)` 返回 **false** ✅
- 扣费前后 k1 国库 **G100→100 S150→150 W40→40**（Spend 后 Refund 净零）＝**退款回 per-kingdom 国库**（M2：AI 账户≠玩家账户，只退玩家＝半修——本批两账户分退）✅
- **prefab 已还原**（`prefab 已还原=True`）✅

**M3 复核**：`UnitFactory.SpawnUnit` 的 `prefab==null` 早退在 `_instancePool.Get` 之前（`UnitFactory.cs:65-72`），池内有无该类型均不影响 null 判定——负探针不被对象池掩盖。

---

## §六、验收线 5｜回归

| 判据 | 证据 |
|---|---|
| 编译 0 error | `refresh_unity(compile=request)`＋`read_console(types=[error])`：**0 编译错误**（仅 3 条既有存量：`ruler` missing script／PanelSettings 无样式／233 node options——均非本批引入，首读即存在） |
| `AI.Core` 零触 | `git status -- "Valley Rampart/Assets/_Game/Systems/AI.Core/" "Valley Rampart/ai决策大脑强化训练/"`＝**空**（零改动） |
| 四族四轮冒烟不退化 | `2_20B_M7`：**6/6 轮 ALL PASS**（人类/精灵/矮人/兽人 seed22360 ＋ 矮人 seed7841 ＋ 兽人 seed31337，每轮 P1~P13＋清场负探针）；`2_20_Smoke_Race`：**ALL PASS**（种族域 D467~D472 行为级探针）；两者均含 P1「十兵种/机器资产可载」探针（`UnitData 全 Load 非 null`） |
| 收尾三态（L-32） | `ExitTestRun`＋`SmokeApi.QuitSmoke`＋`EditorApplication.ExitPlaymode`（探针与两烟测均自收尾·编辑器已退 Play） |

---

## §七、验收线 6｜数值零改（diff 证据）

10 个 `UnitData` 资产 `git diff --stat`：**每文件恰 2 行**（1 删 1 增＝`prefab:` 行替换）。抽样 diff 实证（`Dwarf_Bedrock.asset`）：

```diff
-  prefab: {fileID: 0}
+  prefab: {fileID: 8451807468755188989, guid: 9943a7adc41bafd4ca7153f90facc024, type: 3}
```

`faction`/`occupation`/`walkSpeed`/`runSpeed`/`maxHp`/`attack`/… **逐字未变**（diff 上下文零数值行）。

---

## §八、验收线 7｜写后验

- 本批相关路径 `git status`：**已全部具名 add 并提交**（Commit A `8457e8e4`·33 files；开工回执 Commit `dd16ab11`；交付报告＋账本/索引/工作日志 Commit B `0bb0f15e`）——提交后本批相关路径无残留改动。
- **各笔 hash 已贴**：开工回执 `dd16ab11`｜实施 `8457e8e4`｜交付报告与文档同步 `0bb0f15e`。
- **未 push** ✅（红线）。

---

## §九、L-34 在线判据表（五列）

| 观测项 | 可判定最早时点 | 命中即停条件 | 本次读数 | 结论 |
|---|---|---|---|---|
| 验收线1 prefab 计数 | 编辑态资产枚举一次 | `prefab:{fileID:0}` 计数 = 0 | **0/35** | ✅ 已挂接 |
| 验收线2 逐单位生成 | 正门进局一次 | 10/10 `SpawnUnit` 非 null | **10/10** | ✅ 已生成 |
| 验收线2 画面可见 | 正门进局一次 | 相机 15 格内 ≥1 兵且 ≥1 机器 | **兵=7 机器=3** | ✅ 可见 |
| 验收线3 玩家兜底 | 负探针一次 | 扣费前后 Gold 相等＋ret=false＋不报成功 | Gold **100→100** | ✅ 退款生效 |
| 验收线4 AI 兜底 | 负探针一次 | 扣费前后国库相等＋ret=false | G100→100 S150→150 W40→40 | ✅ 退款生效 |
| 验收线5 回归 | 烟测跑批 | 四族四轮无 FAIL＋0 error | 2_20B 6/6 ALL PASS＋2_20 Race ALL PASS | ✅ 不退化 |
| 验收线6 数值零改 | git diff 一次 | 仅 `prefab:` 行变化 | 10 文件各 2 行 | ✅ 零改 |

---

## §十、负探针还原声明

- 负探针采用**运行时内存态注入**（`UnitData.prefab = null` 后即刻还原），**未保存资产、未写磁盘** ⇒ 无文件残留。探针自证 `prefab 已还原=True`（B1/B2 各一条 PASS）＋ 负探针后 `UnitData` 资产 `git diff` 与负探针前一致（仅 prefab 行挂接改动，无置空残留）。
- 首跑负探针发现探针自身缺陷（正向 3 台机器计入玩家已放置数 → B1 撞上限 2 未走扣费路径）→ **探针已修复**（截图后 `DestroyImmediate` 正向单位再跑负探针）→ 复跑 B1 走通「扣费→退款」全链（控制台 Spend/Refund 四行实锤）。

---

## §十一、提交清单（本批·未 push）

| Commit | 内容 |
|---|---|
| `dd16ab11` | HH.282 开工回执（占 HH.282 号·M1~M7 自答＋22 调用点清单＋sim-sync 零义务） |
| `8457e8e4` | 件1 10 prefab＋UnitData 挂接；件2 SiegeProductionSystem 兜底；验收探针 Valley_HH282_Verify（20/20 PASS） |
| （Commit B） | 交付报告（本文件）＋`_编号登记.md`（HH.283 取号）＋`_交接索引.md`＋`河谷防线_开发计划书.md` 工作日志 |

> 其余工作区改动（`Packages/manifest.json`、`pixel-forge/`、`Medieval Kingdom/`、`Output/`、Ground 图集、文档 3.6/3.8、`Logs/`、`screenshots/` 等）**非本批引入，不混入**。

---

## §十二、sim-sync 核查结论（交付复核）

**零 `AI.Core` 义务（与开工回执一致·施工后复证）**：本批改动面＝`Resources/UnitPrefabs/`（资产）＋`Resources/UnitData/*.asset`（仅 prefab 引用）＋`Systems/Kingdom/SiegeProductionSystem.cs`（生产入口校验）。三处均**不在**共享决策核（`AI.Core`↔训练仓 `harness/Core`）／`TuningSnapshot`/`ProfessionSnapshot`/`FactorContext`／champion/factor_registry 三方同步面内；`SiegeProductionSystem` 为 Unity 侧生产入口，sim 无对应文件（训练场景不生产机器单位）。**无新增决策输入、无行为语义变更、无 sim 补实现义务。**

---

> 执行端｜2026-09-15｜件1 ✅ 已验（0/35·10/10·截图）｜件2 ✅ 已验（玩家/AI 双退款实锤）｜回归 ✅｜数值零改 ✅｜未 push

---

## §十三、策划端验收裁决区（`D727`，2026-09-15，主策划端）

> 裁决文号 **D727**（`0.6_审查决策记录.md` §二百五十五）· Gate=`G2-2`（**条目级销号·门不置 ✅**）

### 13.1 验收结论

| 项 | 结论 |
|---|---|
| **`HH.282` 开工回执** | ✅ **验收成立**（占 HH.282 号·commit `dd16ab11`） |
| **`HH.283` 交付报告** | ✅ **验收成立·销号**（commit `8457e8e4`／`0bb0f15e`／`74a07517`） |
| **台账 `DZ-097`** | ✅ **销号**（「prefab 补齐 **or** 兜底退款」——本批**两者全做**） |
| **`G2-2` Gate** | 🔴 **维持未置 ✅**（见 13.3） |

### 13.2 策划端独立取证（不采信转述·逐条实盘）

| # | 核验点 | 策划端实盘读数 |
|---|---|---|
| 1 | 四笔 commit | `git show --stat` 实核：`dd16ab11` 2 files（登记 1＋回执 137）／`8457e8e4` **33 files** 1051+/12−／`0bb0f15e` 4 files（报告 163＋索引/登记/工作日志）／`74a07517` 1 file（§八 hash 回填）。**顺序自洽** |
| 2 | 验收线1 计数 | **策划端独立复扫** `Resources/UnitData/*.asset` **35 个** ⇒ `prefab: {fileID: 0}` **= 0**（非采信报告口径） |
| 3 | 验收线2 证据物 | `Valley Rampart/Logs/hh282_verify.log` 在场·**20/20 PASS 逐条可读**；截图 `Valley Rampart/screenshots/HH282_专属兵prefab/hh282_live.png` **1,933,753 B 与报告逐位吻合**；**策划端开图目视**＝真图单位在场、族别 tint 可辨（暗红兽人／翠绿精灵／橙棕矮人），**非色块** |
| 4 | 验收线3/4 代码 | `SiegeProductionSystem.cs:174` 玩家 `RulerController.Refund(cost)`；`:223` AI `kingdom.Refund(cost)`；**对称性实读**＝`RulerController.Spend:277`/`Refund:287`（`ratio` 默认 `1.0f` 全额）＋`KingdomState.Spend:149`/`Refund:162`（走 `AddResources`）⇒ **M2 双账户分退成立·非半修** |
| 5 | M3 池不掩盖 | 独立复读 `UnitFactory.cs:65-69` 早退**确在** `_instancePool.Get`（`:72`）之前 ⇒ 主张成立 |
| 6 | M1 双签名 | `UnitFactory.cs:57`＋`:135` **返回值均为 `GameObject`** ⇒ **回执主动勘正任务书 M1 的猜测**（任务书猜「可能 `GameObject` vs `bool`」），**入正面样本** |
| 7 | 件1 结构（同类对照） | 10 个新 prefab **均为 `PrefabInstance` 变体**；源分组＝7 兵 → `062f3b37…`／3 机器 → `deb590c3…`（**两源不同 ⇒ M4 成立**）；源身份＝**`Human_Player_Warrior.prefab` 自身 / `Human_Player_SiegeMachine.prefab` 自身**（反查引用者 23／4 个，含既有兵种与对应 `UnitData`）；**写法与既有 `Ballista`/`Crossbowman` 变体完全同构**（无 removed/added 块·组件经父链继承）；`m_LocalPosition -1.37/-1.69` 有同源同值既有先例 ⇒ **非本批新引入偏移** |
| 8 | 件2 全量性 | 全库实扫 `SpawnUnit(` **38 处**（回执记 22 处＝约数合并）；非 Editor 业务路径逐条核对；**扣费面实证**＝`PopulationSystem.cs`／`VagrantCampSystem.cs`／`KingdomFoundry.cs` 独立 grep `Spend(`/`Refund(`/`CanAfford(` **零命中** ⇒ **「唯一扣费未校验＝`SiegeProductionSystem` 两处」成立** |
| 9 | 数值零改 | 10 个 asset 各 **2 行**（1 删 1 增·仅 `prefab:`）；`Dwarf_Bedrock.asset:15` 实读新引用在场、`walkSpeed 0.6`/`maxHp 165` 等逐字未动 |
| 10 | 写后验 | **策划端实跑 `git status --porcelain`**：61 条改动中**本批相关路径残留 = 0**（措辞「相关路径」精确；其余系并行批产物、报告 §十一 已声明不混入） |

### 13.3 `G2-2` 不置 ✅ 的理由（重要·防误读）

1. `3.1 §二` 门判据＝**「warrior 达门且落地数 > 0」**。本批**只解「prefab 缺失致扣费落空」**，**不解「⑦/⑰ 能否召到」**——`HH.203` 七考实测 4 AI 全程 `warriorCount` 峰值 = **0**，属**另一条账**（`DZ-068`／`DZ-663` 域）。
2. 门上仍有挂账：`DZ-094`／`DZ-048`／`DZ-102`／`DZ-103`／`DZ-105`。
⇒ **本批通过不得被读作 `G2-2` 全绿。**

### 13.4 三处边界入档（**非缺陷·执行端无责**——均系任务书原文要求即如此）

| # | 边界 | 说明 | 防范动作 |
|---|---|---|---|
| **1** | **判据作用域 < 缺陷作用域**（本批最重要·**`L-30` 家族 +1**） | `DZ-097` 病根在**生产链**（扣费→生成），而验收线2 验的是**工厂直调可造**（`Valley_HH282_Verify.cs:118` 实读＝`UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, d.occ, pos)`，**探针直调·非经 ⑦/⑰**）⇒ 本批通过只证「资产层已补全＋工厂可实例化」，**不证「生产链会实例化」** | ⓐ「资产层补全类」任务书验收线**须显式声明验的是哪一层**（资产/工厂 vs 生产链/行为）；ⓑ若 Gate 判据含链上语义 ⇒ 须同批或紧随批补链上读数，**不得以资产层通过冒充链上通过** |
| **2** | 截图证据面 | 截图可证「有真图单位在场」，但 **3 台机器（scale 2）在截图上不可分辨**（任务书要求「至少覆盖 1 兵＋1 机器」）；程序化读数（15 格内 兵=7 机器=3）支持在场 | 后续此类批**补一张机器特写** |
| **3** | 回执清单精度 | 回执 §三 自称「全库 **22 处**」实为**约数合并**（Editor 探针写「~20 处」未逐点枚举），较实测 **38 处**差额 16 | 执行端清单**逐点列行号**（防约数掩盖真实遗漏；本次两处未列者均无扣费，未致实质影响） |

### 13.5 验收三问（`vr-planner-leadership` 钩子2）

1. **为什么发生**：任务书 §四 验收线2 由**策划端**设计时，把「逐单位生成」落在**最易实测的工厂直调**上，未与被判缺陷的作用域（生产链）对齐。
2. **单次失误 vs 流程漏洞**：**流程漏洞（策划侧）** —— 属判据设计缺省检查项。
3. **教训库缺条目还是没查**：**`L-30` 有条但语义面未覆盖「作用域」维度** ⇒ 本次**扩语义面**，不新立条目（防库膨胀）。

### 13.6 其余裁定

- 执行端**全程无越权**：未自改台账状态／未写 `_任务队列.md`／未 push／具名 `add` ⇒ **嘉奖**；M1 主动勘正任务书猜测，**入正面样本**。
- **`_编号登记.md` HH 表头失步**（行内已登记 `HH.283`，表头「已用至」仍写 `HH.282`）⇒ **本次由策划端一并修正为 `HH.283`**（不另立账）。

### 13.7 待办（策划端已办）

- [x] `DZ-097` 销账（`缺陷台账.md`）
- [x] `0.6` §二百五十五（D727）
- [x] `_编号登记.md`（D727／HH.283 表头修正）
- [x] `3.1 §三/§六` `G2-2` 条目状态更新
- [x] `_策划教训库.md` `L-30` 家族 +1
