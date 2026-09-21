# HH.317 · M1-D 掉落与战利品 · 补取证报告（三项 ＋ 顺手件）

- 任务号：HH.317 · `M1-D`（承 `D802` 报裁裁决 · 本单＝补取证小单）
- 本轮性质：**只读取证**（⛔ `Assets/**` **一行未动**；⛔ 未开工；⛔ 未 push；⛔ 不代提交策划端账本）
- 日期：2026-09-21
- 取证手段：静态实读（Read／Grep／Glob）＋ 资产 YAML 直读；⛔ 未跑 Unity、⛔ 未进局
- 交付面：本报告 1 文件（⛔ 无代码、⛔ 无资产、⛔ 无探针文件）
- 依据：`策划端/HH.317_M1-D掉落与战利品_报裁裁决.md`（`D802` · `Q1`~`Q9` 全裁）＋ 小单三项口径

---

## 〇 结论速览

| 件 | 数据 | 判定 |
|---|---|---|
| **件 1 · `Q1` 调用面** | ① `IDamageable` 实现者 ＝ **3** ② `TakeDamage(` 调用点 ＝ **17**（生产 **4** ＋ Editor **13**；另**定义 4**）③ 天生无 source 的生产点 ＝ **2** | ⭐ **≤20 ⇒ 甲案**（两口径全 ≤20，结论一致） |
| **件 2 · `Q5` 标签形状** | 形状 1（独立维度）／形状 2（保留前缀）双对照 ＋ 三判据逐条 ＋ **载体清单**（40 资产声明 ＋ 生产代码声明 5 处 ＋ 实例挂载 6 路径 ＋ **2 个标签盲区**） | ⭐ 两形状均满足 ⓒ；**ⓑ「未选即不打」暴露"默认值三态"问题**（见 §2.4 · 请裁） |
| **件 3 · `Q8` 怪物掉落** | `CarryResource` 生产写入点 **仅 1**（`InitMonster:40`）／消费 **仅 1**（`DropLoot`）／掠夺链**零资源面**（`_carryingHome` 无吸收实现）；`lootResource` **3/3 资产缺省 ＝ `Food`** | ⭐ **语义判定 ＝「掉落数量数值参数」＝ 凭空生成（`#45` 同族）** ⇒ 建议**改**（怪物无仓 ⇒ 无掉落箱）＋ 附**行为变化声明草案**（供回写 `09`） |
| **件 4 · 顺手件** | `ChestManager.cs:11`／`:219` 两处「三调用方」旧注释 —— **现场坐实** | ✅ 列入施工批（本轮不改） |

---

## 一 · 件 1：`Q1` 调用面（决定甲／丁）

### 1.1 ① `IDamageable` 实现者 ＝ **3**（全库声明处）

| # | 实现者 | 声明行 | 写法 |
|---|---|---|---|
| 1 | `UnitController` | `UnitController.cs:24` | `class UnitController : MonoBehaviour, ISaveable, IDamageable, IUnitHandle, IClickInteractable, ITaskSource` |
| 2 | `Building` | `Building.cs:29` | `class Building : MonoBehaviour, IInteractable, IDamageable, ISaveable, ITaskSource, IGridOccupant, MapGate.IAnchorConsumer` |
| 3 | `Portal` | `Portal.cs:8` | `class Portal : MonoBehaviour, IDamageable, IGridOccupant, ISaveable` |

- 两种写法（`,\s*IDamageable` 与 `:\s*IDamageable`）均已含盖；**Editor 侧零实现者**（探针只用现成实体）。
- ⚠️ 继承实现（非直接声明）：`MonsterController : UnitController`（子类）——**⛔ 不新增签名义务**（未覆写 `TakeDamage`，全库 `override.*TakeDamage` **0 命中**）。
- ⚠️ `Portal` 为小单未预见项：**甲案下其 `TakeDamage`／`Heal` 签名须同步**（`:170`／`:185`；其死亡走 `DestroyPortal()` **不发 `UnitDiedEvent`** ⇒ 与掉落链无关，仅签名对齐）。

### 1.2 ② `TakeDamage(` 全库调用点 ＝ **17**（含 Editor）＋ 定义 4

**调用点 17**（`\.TakeDamage\(` 全量 · 逐文件 count 复核）：

| 域 | 文件 | 行 | 备注 |
|---|---|---|---|
| 生产 | `DamageSystem.cs` | `:412`（主路径）／`:406`（盾卫庇护转移） | ⭐ **有 source 语义**（`ApplyDamage` 形参 `source` 在手） |
| 生产 | `SatietySystem.cs` | `:233`（饥饿扣血） | 环境/持续伤害 ⇒ **天生无 source** |
| 生产 | `AIDebugUIManager.cs` | `:817`（残编测试处决） | 调试 UI ⇒ **天生无 source** |
| Editor | `Valley_HH159_UIProbe.cs` | `:182` | 测试处决 |
| Editor | `Valley2_21A_Smoke.cs` | `:159`／`:160`／`:276`／`:345` | 测试（半血构造／处决／批量处决） |
| Editor | `Valley2_20_Smoke_Race.cs` | `:178`／`:195`／`:207`／`:222`／`:447`／`:524`／`:721` | 测试（批量处决） |
| Editor | `TestFixtureApi.cs` | `:136` | 测试（处决） |

**定义/声明 4**（不计入调用）：`IDamageable.cs:36`（接口）／`UnitController.cs:576`／`Building.cs:1238`／`Portal.cs:170`。
（另有注释提及 1 处：`IDamageable.cs:8`，不计。）

### 1.3 ③ 生产码调用点 ＝ **4** ＋ 无 source 点分类

**生产码 4 处**（排除 `Assets/Editor/**`）：

| 分类 | 点 | 甲案下传参 |
|---|---|---|
| 战斗主路径（有 source） | `DamageSystem.cs:412` | ⭐ 传 `source`（`ApplyDamage` 形参） |
| 战斗转继（有 source） | `DamageSystem.cs:406` | ⭐ 传**原 `source`**（⛔ 非 `victim` —— `D802` 已警示；此处转移伤害的施加者仍是原攻击者） |
| 环境/持续伤害（天生无 source） | `SatietySystem.cs:233` | 传 `null`（默认参数 ⇒ 可不动） |
| 调试处决（天生无 source） | `AIDebugUIManager.cs:817` | 传 `null`（默认参数 ⇒ 可不动） |

**天生没有 source 的点（完整清单）＝ 生产 2 ＋ Editor 13 ＝ 15 处**：
- 环境/持续：`SatietySystem:233`
- 调试/测试处决：`AIDebugUIManager:817` ＋ Editor 13 处（全部为 `TakeDamage(999999)`／`TakeDamage(CurrentHp)` 式处决或血量构造）
⇒ ⭐ **这 15 处正是"改了签名却漏传"的防区**；采用 **`= null` 默认参数**（接口＋3 实现均写）⇒ 本 15 处**编译零改动**（见 1.5）。

**间接路径（调 `ApplyDamage` 而非 `TakeDamage`，不受甲案影响）**：`ProjectileManager.cs:244/261/329`（投射物命中/穿透/阻挡）／`GroundEffectManager.cs:148`（地面效果 DOT）／`DamageSystem.cs:324`（单目标）／`:475`（AOE 溅射）／`NPCBrain.cs:1684`（冲锋）——**均 `source` 在手** ⇒ 甲案下自动正确（唯一"下游断点"就是 `ApplyDamage → target.TakeDamage(:412)` 这一处）。

### 1.4 甲／丁判定 ⇒ ⭐ **甲案**

- 口径 ①：全库调用点（含 Editor）＝ **17 ≤ 20** ⇒ 甲
- 口径 ②：生产码调用点 ＝ **4 ≤ 20** ⇒ 甲
- ⇒ **两口径结论一致：甲案**。（且 1.5 的默认参数手法使**实际改动点 ≈ 7 处**，成本远低于"17 处调用点"的表象。）

### 1.5 甲案精确改动面（预估 · 供施工批）

| # | 落点 | 改动 |
|---|---|---|
| 1 | `IDamageable.cs:36` | `void TakeDamage(int finalDamage)` ⇒ `void TakeDamage(int finalDamage, IDamageable source = null)` |
| 2 | `UnitController.cs:576` | 签名同步（带默认值）；`:589-591` `Die()` 传递 killer；`:680 Die()` ⇒ `:691` 事件 `Killer` 填真值（替换 `null` + 移除 `:678` 未兑现注释） |
| 3 | `Building.cs:1238` | 签名同步（带默认值）；⚠️ 是否把 source 传进 `Die`／`EnterRuined` ⇒ **施工期决定**（`Q3` 已裁 `EnterRuined` 仓留存 ⇒ 建筑侧 Killer 的掉落价值仅剩"工事 `Die` 掉箱"路径 · 无仓工事 ⇒ 价值待核） |
| 4 | `Portal.cs:170` | 签名同步（带默认值 · 不涉事件） |
| 5 | `DamageSystem.cs:412` | `target.TakeDamage(finalDamage, source);` |
| 6 | `DamageSystem.cs:406` | `shield.TakeDamage(…, source);`（**原 source**） |
| 7 | `SatietySystem:233`／`AIDebugUIManager:817`／Editor 13 | ⭐ **不动**（默认参数兼容）；若宁显式 ⇒ 传 `null` |

- ⚠️ **默认参数要求接口＋实现四处均写 `= null`**（否则经具体类型引用调用的点——如 `unit.TakeDamage(…)`（`SatietySystem`）／`enemy.TakeDamage(…)`（Editor）——会因缺参编译失败）。四处写全 ⇒ **17 处调用点除 #5/#6 外零改动**。

### 1.6 同族签名波及核查 ⇒ ⛔ **不波及 `Heal(int)`**

| 项 | 数据 |
|---|---|
| `IDamageable` 成员 | 2 个 void 方法（`TakeDamage`／`Heal`）＋ 4 个属性/方法（`CurrentHp`／`MaxHp`／`Defense`／`GetPosition`／`GetFaction`）——**无默认实现**（普通接口）⇒ 改 `TakeDamage` ⇒ **3 实现者必改**（已列 1.5） |
| `Heal` 波及 | ⛔ **无**——`Heal(int)` 独立方法，本批不动；供知悉其面：实现 3（`UnitController:598`／`Building:414` 空实现／`Portal:185`）＋调用 4（`SatietySystem:244`／`GroundEffectManager:187`／`NPCBrain:1364`／Editor `Valley_HH131_BedrockProbe:158`） |
| 其他 `TakeDamage` 变体 | ⛔ 无（无 `TakeDamage(float)`／无扩展方法／无方法组缓存／无反射调用） |

---

## 二 · 件 2：`Q5` 可掉落标签的形状

### 2.1 现状复核（✅ 与小单一致 · 附原文）

| 项 | 实读 |
|---|---|
| 一维确认 | `WarehousePaths`（`ResourceCatalog.cs:156-175`）**只有"收什么"**：`All = "res"`（`:159`）＋ `Normalize`（`:167-174`：空/全空串 ⇒ 通用仓 `AllOnly`）——**无任何"能否掉落"维度** |
| 匹配语义 | `PathMatches`（`:102-110`）：`resourcePath == declaredPath` 或 `StartsWith(declaredPath)` ＋ 段边界（`_`／`.`）⇒ 纯前缀匹配 |
| 资产非空声明 | **恰 7 栋**（40 栋建筑资产逐份实读）：`AdvancedStorage.asset:35`（`res_material.wood`）／`Blacksmith.asset:35`（`res_material.metal`）／`Granary.asset:33`（`res_food.grain`）／`farm.asset:33`（`res_food.grain`）／`Well.asset:33`（`res_currency.gold` · ⚠️ `R4` 误配）／`quarry.asset:33`（`res_material.stone`）／`Warehouse.asset:35`（`res_material.wood`）；其余 **33 栋 `warehousePaths: []`**（空 ⇒ 通用仓） |
| 消费面 | `Accepts` 系（`StorageComponent:79/137/190` · `WarehousePanel:129` · `ProducerComponent:86` · `MineByproductComponent:103` · `ConstructionSiteStore:217` · `Building:1464` · `WarehouseRegistry:66` · `SiegeWorkshopBuilding:139` · `TaskScheduler:763/980/1055`）⇒ **判据 ⓒ 的守护对象就是这一面** |

### 2.2 两种形状对照（本端未定 · 供裁）

| 维度 | **形状 1：独立维度** | **形状 2：保留前缀** |
|---|---|---|
| 形态例 | `StorageComponent` 增 `bool droppable = true`（或 `noDrop`）＋ `BuildingDef` 增对应资产字段（`Init:58` 处注入） | `warehousePaths` 数组内加保留前缀，如 **`nodrop_population`**（`_` 段，与 `res_*` 不撞）⇒ 命中即不可掉 |
| 数据落点 | ① `BuildingDef.cs:48-53` 加字段（40 栋资产可零迁移——缺省值即默认）② 代码声明点 5 处按需赋值 ③ `StorageComponent` 加字段＋API | ① 零新增字段（数据行在**既有数组**加一项）② 零迁移（40 栋不动）③ 只加"读取"代码 |
| ⓐ §9.7 逐项表达 | ✅（bool 直给） | ✅（前缀存在性判定） |
| ⛑ 人口仓 ⛔／水仓 ⛔ | ✅ 可表达（打 `noDrop`） | ✅ 可表达（加 `nodrop_*`） |
| ⓒ 不影响 `Accepts` | ✅ 天然（不动路径面） | ✅ **需证明**：`nodrop_*` 不以 `res` 开头 ⇒ `PathMatches` 对一切资源路径（`res_*` 前缀）恒 false ⇒ 匹配面零扰动；`Normalize` 不改（数组非空 ⇒ 不触发兜底）⇒ **可证明成立**（建议施工附"全资源 × 全声明"矩阵实测兜底） |
| 主要代价 | ⚠️ `BuildingDef` 加字段 ⇒ **M6-F 迁移面 +1**（该栏已注明"过渡数据源，`10` 能力表 `store` 落地后迁入"· `BuildingDef.cs:52`） | ⚠️ **同数组混两维**（"收什么"＋"能否掉"）⇒ 新读者歧义风险；`warehousePaths` 语义注释须同步扩写 |
| 其他风险 | 代码声明点（箱/国库/弹药/副产）须逐一给值（防"忘给"⇒ 用默认） | 前缀命名须钉死（`nodrop_` vs `nodrop.`）；`SumByPrefix` 等**无第二个消费面**（前轮已核 `SumByPrefix` 0 外部调用方）⇒ 无连锁 |
| 本端倾向（供裁） | ⭐ **形状 1**（语义清晰 · 与"标签=路径"的**两维正交**；`M6-F` 迁移代价可接受——迁移时一并处理） | 备选（若以"零新增字段"为优先） |

### 2.3 三判据逐条（两形状均须满足 ⇒ 结论）

- ⓐ ✅／ⓑ ✅（机制层）／ⓒ ✅（形状 1 天然、形状 2 需附证明）——**但 ⓑ 的"未选即不打"另有"默认值"问题，见 2.4**。
- ⚠️ **"NPC 背包 ✅ 可掉"两项形状都无法经标签表达**——`WorkerInventory` **不是 `StorageComponent`**（独立 `IWarehouse` 实现，无 `DeclaredPaths`）⇒ 与 `D802` `Q4` 裁决"NPC 背包恒可掉 ⇒ 无需标签"**自洽**（不需要标签）。
- ⚠️ 同族盲区 ②：`ConstructionSiteStore`（工地仓）**无标签**（其注释自陈「⛔ 无『仓库声明标签』」· `:13`）且 `M1-C` **已实现其掉箱**（`DropSiteStoreToChest`）⇒ 统一"抽取器"实现时须显式纳管（防"无标签 ⇒ 查不到 ⇒ 被漏"）。

### 2.4 ⭐ 「未选即不打」如何落地（`ⓑ` 的隐藏问题 · 请裁）

现状：**"可掉落"无载体**（前轮结论），§9.7 清单是**仓级**语义（不是逐资源）。两种形状只能表达两态（可掉／不可掉）⇒ **"未选即不打"落在哪一态，须钉死**：

| 方案 | 默认（不打标）行为 | 与 §9.7 的张力 | 风险 |
|---|---|---|---|
| **甲 · 默认可掉** | 不打 ⇒ **可掉** | 物资/国库/弹药 ✅ 天然满足（现状全掉落） | ⚠️ 水仓（`M1-F`）／人口仓（未来）落地时**必须记得打 `nodrop`**（否则默认可掉）⇒ 须写入其落地批承接账 |
| **乙 · 默认不可掉** | 不打 ⇒ **不可掉** | ⛔ 与"物资仓 ✅ 可掉"冲突（40 栋全要补打"可掉"标）⇒ 不推荐 | 全量补标 |
| **丙 · 三态**（可掉/不可掉/未标） | 未标 ⇒ **报警（§十-1 自检）或按甲处理** | 最贴合"未选即不打"字面（未选＝未标＝可被机器扫出） | 实现/资产成本最高 |

- ⭐ **本端建议（供裁）：方案甲 ＋ 文档注 ＋ 下游承接账**——理由：① 现状需"拦"的对象（人口/水仓）**当前均无实体**（人口＝账本 · `M1-B`；水＝`WaterNetwork` · `M1-F`）⇒"默认可掉"在本批**零行为风险**；② §9.7 四类"可掉"仓全部现存且天然可掉 ⇒ 无需逐仓补标；③ 把"**给水仓/人口仓打 `nodrop`**"写成 `M1-F` 的承接项（与 `D802` 的"回写 `09`"一并）。
- ⚠️ 若策划端认"未选即不打"必须**字面可表达**（"未选"与"可掉"要区分）⇒ 只有方案丙能满足 ⇒ 请明示。

### 2.5 载体清单（决定标签写在哪儿）

**A. 资产级声明（40 栋）** ⇒ 经 `StorageComponent.Init:58`（`SetDeclaredPaths(building.def.warehousePaths)`）注入：

| 类 | 数 | 明细 |
|---|---|---|
| 非空声明 | **7** | `AdvancedStorage`／`Blacksmith`／`farm`／`Granary`／`quarry`／`Warehouse`／`Well`（`file:line` 见 2.1） |
| 空声明（⇒ 通用仓 `res`） | **33** | 其余全部建筑资产 |

**B. 生产代码声明点（5 处 `SetDeclaredPaths`）**：

| # | 落点 | 声明 |
|---|---|---|
| 1 | `StorageComponent.cs:58`（本体 · 来自资产） | `def.warehousePaths` |
| 2 | `ChestEntity.cs:75`（箱） | `WarehousePaths.All`（通用仓） |
| 3 | `TreasureVault.cs:62`（国库子容器） | `VaultPaths`＝`res_material`＋`res_food`（⛔ 现不含金/弹药） |
| 4 | `SiegeWorkshopBuilding.cs:63`（弹药 ×3 子仓） | `PrimaryPathOf(type)`（逐弹专属） |
| 5 | `MineByproductComponent.cs:78`（副产 ×2~3 子仓） | `PrimaryPathOf(type)`（逐产专属） |

**C. `StorageComponent` 实例挂载路径（6 处）**：
`BuildingFactory.cs:219`（经济仓储类）／`BuildingFactory.cs:231`（生产类）／`ChestEntity.cs:74`（箱）／`TreasureVault.cs:61`（国库）／`SiegeWorkshopBuilding.cs:62`（弹药子仓）／`MineByproductComponent.cs:76`（副产子仓）。

**D. ⚠️ 标签盲区（2 处 · 不覆盖）**：
1. `WorkerInventory`（NPC 背包 · 独立 `IWarehouse` · 无 `DeclaredPaths`）⇒ 按 `Q4` 裁"恒可掉"（无需标签）✓
2. `ConstructionSiteStore`（工地仓 · 独立组件 · 自陈无标签）⇒ `M1-C` 已实现掉箱 ⇒ 抽取器须显式纳管（见 2.3）

### 2.6 件 2 结论摘要（供裁）

1. 两形状均满足 ⓐⓒ；ⓑ 的"未选即不打"需先裁**默认值三方案**（建议**甲 · 默认可掉**＋下游承接账）。
2. 本端倾向**形状 1**（独立字段）＋"接口＋3 实现＋数据行"三面改动；若优先"零新增字段"则**形状 2**（须附 `Accepts` 零扰动证明）。
3. 标签体系**天然不覆盖** `WorkerInventory` 与工地仓——两者的掉落由各自裁决/既有路径承担（须在施工清单显式列出，防"无标签即被漏"）。

---

## 三 · 件 3：`Q8` 怪物掉落（按 `L-54` 报法）

### 3.1 ① `CarryResource` 生产写入点 ＋ 消费面

| 面 | 实读 |
|---|---|
| **定义** | `MonsterController.cs:25` `public int CarryResource { get; private set; } = 5;`（运行时属性 · `private set`） |
| ⭐ **生产写入点 ＝ 1** | `MonsterController.cs:40` `CarryResource = monsterDef.carryResource;`（`InitMonster(monsterDef)` 内；唯一调用方 `:147` ← `MonsterSpawner`） |
| ⭐ **消费点 ＝ 1** | `MonsterController.DropLoot:61`（守卫 `<= 0`）＋ `:76`（`BuildLootPack` 数量）——**除 `DropLoot` 外零读者**（`MonsterAI` 不读：`:147` 注释中的 `carryResource` 是小写文字，实际只写 `_carryingHome` 标志） |
| SO 字段 `MonsterDef.carryResource` | 定义 `MonsterDef.cs:25`（"掠夺资源量（每怪）"）；资产取值：`Slinger.asset:28`／`Raider.asset:28`／`Brute.asset:28` **均 ＝ 5** |
| ⇒ 可达性 | **写入路径可达**（Spawner → `InitMonster` → 属性），且**唯一消费就是掉箱** ⇒ `CarryResource` 的**唯一实际作用 ＝ 掉落数量** |

### 3.2 ② `lootResource` 来源与全库取值分布

| 项 | 实读 |
|---|---|
| 定义 | `MonsterDef.cs:26-27`：`public ResourceType lootResource = ResourceType.Food;`（Tooltip 自陈"击杀掉落/掠夺的资源类型"） |
| 资产全量 | ⭐ **3 份**（`MonsterDef` script guid `2abb5acb…` 全库直查 ⇒ 仅 `Slinger`／`Raider`／`Brute`） |
| ⭐ 取值分布 | **3/3 资产无 `lootResource` 键**（逐份字段实读：`Slinger.asset:15-29` 只见 `type/hp/attack/…/carryResource/guardRecallRatio`；`Raider`／`Brute` 同）⇒ **全部缺省 ＝ `ResourceType.Food`** ⇒ **未逐型配**（`M1-A` 后"任意类型皆可承载"能力未被资产使用） |

### 3.3 ③ ⭐ 掠夺链"零资源面"坐实（语义判定的关键）

| 证据 | 实读 |
|---|---|
| 掠夺态进入 | `MonsterAI.EnterLooting:153-158`：`mode = Looting` ＋ 停表 —— **无资源动作** |
| 掠夺完成 | `MonsterAI.UpdateLooting:142-151`：计时到 ⇒ **只写标志** `_carryingHome = true` ＋ 回门（**未读 `CarryResource`、未取任何资源**） |
| 回门"吸收" | `MonsterAI.UpdateRaiding:73-79`：到门 ⇒ **仅** `_carryingHome = false` 重置 —— ⛔ **无入账、无记账、无事件、无"资源消失"实现**（`:8` 注释"资源消失（吸收入口）"**为空头承诺**） |
| Portal 侧 | `Portal.cs`（逐段实读 1-206）：**零资源处理**（只有 HP/守夜/召唤/摧毁） |
| ⇒ 结论 | ⭐ **"掠夺链"不产生任何资源面效果**（表现层空壳）⇒ `CarryResource` **不是"怪物身上带着的东西"**，而是**纯粹掉箱数量参数** ⇒ 属 `#45` 同族「**凭空生成**」 |

### 3.4 ④ 建议（含行为变化声明草案 · 供回写 `09`）

- **建议：改**（不保留）——依据链：`D802` `Q8` 裁「怪物无仓 ⇒ ⛔ 不产生掉落箱」＋本单 `§3.3` 已坐实"凭空"⇒ 命中 `Q8` 的"须随本批一起改（改为无仓 ⇒ 无掉落）＋ 显式声明行为变化"。
- **行为变化声明草案**（供策划端回写 `09`／`15_账本`）：

> **`D213` 怪物掉落退役（`M1-D`）**：怪物（`Raider`／`Slinger`／`Brute`）被击杀**不再掉落资源箱**——掉落改由「死者仓＋标签」统一口径决定，怪物**无仓** ⇒ 不产生掉落箱。`MonsterDef.carryResource`／`lootResource` **不再是掉落来源**；掠夺链（`MonsterAI._carryingHome`）本就**无资源面实现**，本批**不新增**。⚠️ 连带：三份怪物资产的 `carryResource: 5` 与缺省 `lootResource=Food` 成为**无消费字段**。

- **附报 2 项（供裁）**：
  1. **`carryResource`／`lootResource` 字段去留**：保留（留待未来"给怪物仓"的可选方案）／删（`09#45` 精神）——本端建议**保留字段 ＋ 注释标注"当前无消费"**（删字段零收益且有资产迁移面）。
  2. ⚠️ **另一可选路径（仅列报，⛔ 不主张）**：若策划端希望保住"打怪有收获"玩法 ⇒ 可裁「`CarryResource` ＝ 怪物的**单资源仓**」（`InitMonster` 时把 SO 值装入一个不注册的 `StorageComponent`）⇒ 死亡即"死者仓掉箱"⇒ 与统一模型**同构不凭空**。⚠️ 但这**超出 `Q8` 字面**（`Q8` 已裁"无仓 ⇒ 不掉"）⇒ 若采纳须新裁一条口径。
- ⚠️ **测试面连带**：`Valley2_20B_Smoke_M7`（含战利品/怪物相关探针）与任何依赖"怪物掉箱"的冒烟断言，须随本批同步复核（防"改后旧断言红"误报为回归）。

---

## 四 · 件 4：顺手件（施工批清单 · 本轮只报不改）

| # | 落点 | 现状原文 | 应改为 | 现场坐实 |
|---|---|---|---|---|
| 1 | `ChestManager.cs:11` | 「…**SpawnChest 全工程仅 DamageSystem/TreasureVault/MonsterController 三运行时调用方**」 | 五调用方（`DamageSystem:633`／`TreasureVault:122`／`MonsterController:67`／`Building:916`／`Building:1047`） | ✅ 本轮逐行实读 |
| 2 | `ChestManager.cs:219` | 「…（**SpawnChest 三调用方**均运行时行为）」 | 同步为五调用方（或改"仅运行时行为（调用点清单见类头）"防再次漂移） | ✅ 本轮逐行实读 |

⇒ 列入施工批**注释勘正件**（⛔ 本轮不改）；⚠️ 建议同时给类头加"⚠️ 调用点清单以此处为准，改签名须全量 grep"防第三次漂移。

---

## 五 · 红线与在场性自查

| 红线 | 自查 |
|---|---|
| ⛔ `Assets/**` 一行不动 | ✅ 本轮**零写操作**（仅 Read／Grep／Glob）；工作区既存 `M` 项**非本端所出** |
| ⛔ 不开工／不 push | ✅ 未写代码、未建探针、未进局；无 git 写命令 |
| ⛔ 不代提交策划端账本（含本单报告自行 commit） | ✅ 报告落 `执行端/` 侧；未触碰 `_编号登记.md`／`_任务队列.md`／`_当前快照.md`／`_策划教训库.md`；无任何 commit |
| 阻塞歧义 | ✅ 无停手项；两处"请裁"（§2.4 默认值三方案 · §3.4 附报 2）属**供裁补充**，不阻塞本单交付 |

## 六 · 未覆盖项（诚实标注）

1. ⛔ 未跑运行时：`Accepts × nodrop` 零扰动建议施工时以"全资源 × 全声明"矩阵实测兜底（本报告为静态证明：`nodrop_*` 不匹配 `res_*` 前缀）。
2. ⛔ 训练仓未直读（不在本工作区）：`15_账本` 承接账核对仍为挂账（`D802` 已认可如实声明）。
3. ⚠️ `Raider.asset`／`Brute.asset` 的 `lootResource` 缺省结论由"全量 guid 搜索 3 命中 ＋ `lootResource` 于 `Assets/Resources` 0 命中"双证；未逐份全文粘贴（可按需补）。

---

*本端：执行端 · 2026-09-21 · 补取证完毕（三项＋顺手件），待策划端出施工任务书*
