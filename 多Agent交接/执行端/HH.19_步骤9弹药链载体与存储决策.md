# HH.19 步骤9 弹药链：弹药载体与存储归属决策

> 类型：待决策
> 状态：✅已裁决（2026-08-23 策划端，A×4 + 三项圈定 + 口径补遗，见下）
> 日期：2026-08-23 · 发起端：执行端 · 关联清单/文档：2_12 实施计划步骤 9（D207~D212）/ 设计 §5.9

## 一、做了什么（执行端填，带证据）

1. **Play 铁闭环补跑 ✅ 三件套全 PASS**（已回写 [HH.18 §2 补跑收口节](./HH.18_阶段4核心收口完成与待办登记.md)）：脚本驱动真实场景流（MainMenu→SetNewGame→GameScene）+ 存档→读档往返（含防 no-op 内存扰动），国库 6 资源含铁逐项相等 / 人口 9 / 无多余单位；"国库就绪时序"待查撤销（根因=旧 harness 未驱动 NewGame，非代码 bug）。
2. **用户拍板 B 方向**：按序步骤 9→10→11 开工。
3. **步骤 9 开工前勘察完成**（未动代码），存量与缺口摘要：
   - `SiegeProductionSystem`（Systems/Kingdom/SiegeProductionSystem.cs:15-194）：`ProduceMachine/ProduceAmmo/ResupplySiegeUnit` **全工程零调用方**（仅 GameBootstrap 触发单例 + TeardownManager 重置）；`_ammoStock` 全局弹药账（ProjectileType 键，仅石/火/魔三键）。
   - `UnitController` 弹仓机制已就位（3.7 B1）：`AmmoStone/AmmoFireball/AmmoMagic` + `ConsumeAmmo`/`SelectAmmo` 耗尽停火/惜用（:145-153/:733-772）；**石弹补给=定时器假搬运** `TickAmmoResupply`（:718-730，注释自陈"模拟工人搬运往返"）；火/魔补给接口无调用方。
   - 投掷机厂 `SiegeWorkshop.asset` 存在但 `producer.rate=0` → 不挂产能/存储组件、不发任务；识别靠字符串 `def.id=="SiegeWorkshop"`（无 `isSiegeWorkshop` 标记，先例=`isBlacksmith`+专属组件）。
   - 塔（ArrowTower/CrossbowTower/MagicTower）为 isStatic 单位，`ammoMax=0`=无限弹药，未入弹药系统（D211 要求统一）。
   - 搬运链全程 `ResourceType` 载体：`WorkerInventory` 单资源背包 / `StorageComponent` / `IWarehouse` / `ResourceCarryConfig`（Metal 尚无条目）；`KingdomTaskType`/`KingdomDestType` 无弹药/填弹任务与"单位弹仓"终点。
   - `ResourceType` 不含弹药；`AmmoDef`（Data/AmmoDef.cs）为行为模板（穿透/AOE/弹道），非经济载体；`SiegeProductionConfig.asset` 已有三种弹药造价（各 1 原料）。

## 二、现状与阻塞

步骤 9 验收="投掷机厂产弹药→搬运→入仓→填弹仓→发射耗原料闭环；弹仓空停火；塔统一装填"。达成前存在**数据载体级分叉**：弹药以什么身份进入"生产→仓储→搬运→装填"物流链。选错返工面大（枚举/存档/搬运任务/步骤10 贸易接缝全跟着走），属 HH 开门判据第 1 类，提交裁决。

## 三、待决策事项（选项 + 推荐 + 影响）

### 决策点 1：弹药数据载体——进 `ResourceType` 还是走独立 `ProjectileType` 通道？

- **A（推荐）：`ResourceType` 尾部追加 3 弹种**（如 StoneAmmo/FireballAmmo/MagicAmmo），弹药成为可搬运资源，全量复用仓库/搬运/凑单/箱子/存档链；真源=`StorageComponent` 仓库；`SiegeProductionSystem._ammoStock` 全局账退役（或降级为查询门面）。
  - 理由：与步骤 8 后"真源=仓库、全局池退役"架构方向一致（HH.8/HH.16）；设计 D212"入仓库存储"、D142"箱子搬运"（ChestEntity 内容物=ResourceType）、D219"市场可买弹药"三条自然成立；Metal 先例（HH.13 裁决 A：枚举扩展+消费方同步）。
  - 影响：需核验 `ResourceType` 消费面（ResourcePack 建造成本不含弹药=零改动；HUD 不显示=零改动；存档随 StorageComponent 自动）；sim 训练仓 ResourceType 需同源追加——**登记待办交训练仓**（HH.15 模式，执行端不代做）。
- **B：复用 `ProjectileType` 独立通道**（`_ammoStock` 升格"国库弹药仓"，新造专用任务类型衔接厂→账→弹仓）。
  - 理由：设计 §5.9 SO 表"AmmoDef/SiegeProductionConfig（沿用）"措辞；不动枚举与 sim 契约，侵入面小。
  - 影响：平行物流通道与 D43"一切资源流动走仓库操作"语义张力；全局账户模式与 HH.8 退役方向相悖；步骤 10 贸易（D219 弹药可买）需另做一套接缝；箱子实体（7C 已落，内容物=ResourceType）承载不了弹药。

### 决策点 2：弹药存储归属（若采纳决策点 1-A）

- **A（推荐）：国库不纳管**——`TreasureVault.Managed` 维持 6 资源不动；弹药存投掷机厂自身存储 + 仓库建筑（`StorageComponent`）。
  - 理由：不动"国库 6 资源"验收面（刚经铁闭环验证）；弹药=军事加工品，语义不属国库；开局无弹药需求——战争机器/塔出生初始装填满仓（UnitController:296-301 既有机制）作缓冲，弹药压力出现时玩家已有仓库建筑。
- **B：国库 Managed 扩容**（6→9，开局即可入库）。
  - 影响：国库子仓库/容量/存档/读档恢复面扩大；铁闭环验收口径"6 资源"需改口径。

### 决策点 3：假搬运定时器（`UnitController.TickAmmoResupply` 石弹自动回充）处置

- **A（推荐）：Unity 侧退役定时器**，石弹与火/魔统一走"工人装填"真实链（补给真耗仓库弹药存量）。
  - 理由：D207~D212 设计意图=真实物流；留定时器则弹药经济名义化（补给无成本）；sim 侧快照参数不动（保同源），行为差登记训练仓待办。
  - 风险：战争机器/塔弹药经济行为变化，平衡靠实测与 SO 调参（弹仓容量占位 10~20 发，设计 §5.9）。
- **B：保留定时器作兜底**（无装填任务时自动慢速回充）。
  - 影响：双通道并行，弹药消耗成本被兜底稀释，难平衡；但保留无后勤时的体验下限。

### 决策点 4（勘察新增）：弹药是否进箱子（D207"工人搬运弹药（箱子 D142）"）

> 背景：`ChestEntity.contents` 是固定 5 字段的 `ResourcePack`（WorldConfig.cs:134-159），不是泛型 ResourceType 容器。本 HH 初稿"箱子内容物=ResourceType"表述失实，已更正。弹药要能装箱 → `ResourcePack` 扩 3 字段 + `IsZero`/`operator+`/`operator*`/`CanAfford`/`Spend`/`Refund` 联动 6 处（同 HH.13 Metal 扩容模式，防静默丢弹药）。

- **A（推荐）：按设计落地，弹药可装箱**——`ResourcePack` 同步扩 3 弹种字段（6 处联动），厂→仓搬运背包满时正常落箱，D142 统一容器语义完整。
  - 影响：`ResourcePack` 字段面 5→8；步骤 11 溢出箱与步骤 10 贸易（若弹药可卖）天然兼容。
- **B：弹药不落箱**——搬运只走背包直送，背包满则拒收/留源；`ResourcePack` 不动。
  - 理由：省 6 处联动；装填任务（仓库→弹仓）单次携带量通常一趟够用，落箱是边缘路径。
  - 影响：偏离设计 D207/D142 文本，需策划明确豁免；厂→仓运输背包满时资源留源不落地。

## 四、下一步建议

- 裁决回写后按裁决实施步骤 9（装填任务类型/终点、投掷机厂专属组件仿 `BlacksmithBuilding` 先例、塔 `ammoMax` 资产回填等属裁决方向内执行细节，不再请示）。
- **裁决等待期并行选项**（已向用户提议）：步骤 10 市场贸易（贸易框架+Metal 档位+额度+商店/税务所移除；"弹药可交易"行随决策点 1 落地）与步骤 11 箱子溢出对接（与弹药独立）可先行，保持进度。
- sim 侧（无论采纳何案）：训练仓同步待办登记，交接训练仓会话（HH.15 模式）。

---

## 策划裁决（策划端回写，2026-08-23）

> 策划端裁决前实盘核验：ResourceType 尾部追加先例（GameEvents.cs:85-100，Ore/Crystal/FireOil/Metal 四次追加 + "不入国库"注释先例）、ResourcePack 5 字段+联动面（WorldConfig.cs:134-159）、TreasureVault.Managed 固定 6 资源（TreasureVault.cs:16-20）、假搬运定时器（UnitController.cs:718-730）、设计 §5.9 原文（D207 明文"工人搬运弹药（箱子 D142）→ 入仓库存储（D212）"、§5.10 D219 买紧缺含弹药）、真训练仓 SimBrain.cs:39/975-979 确有 ProjectileType.Stone/Fireball/Magic 且确无 ResourceType（HH.15 待办仍未落盘，附录 sim 申报属实）。

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 1 弹药数据载体 | **采纳 A**：ResourceType 尾部追加 3 弹种 | ①设计四条文本（D207 搬运/D212 入仓/D142 箱子/D219 可买）均以"弹药=可搬运资源"为前提，B 独立通道四条全违背且箱子承载不了；②HH.8/16 架构方向="真源=仓库、全局池退役"，`_ammoStock` 正是待退役全局账；③Metal 先例（HH.13 A）+ 附录 156 处核验=消费面基本零改动；④AI 北极星：弹药并入统一资源经济学，sim 一套模型（D209 可训练） |
| 2 弹药存储归属 | **采纳 A**：国库不纳管 | ①代码既有先例：Ore/Crystal/FireOil 注释明示"存建筑存储，不入君主国库"（GameEvents.cs:91-94），魔弹/火弹原料（水晶/火油）本就属此类——弹药=同类加工品，归属一致；②国库 6 资源刚过铁闭环验收（HH.18），扩容=重开验收面+存档链，零玩法收益；③开局缓冲已有（出生满装填 UnitController:296-301）；④真源=厂存+通用仓，凑单兜底走 D51 |
| 3 假搬运定时器处置 | **采纳 A**：退役定时器，统一走工人装填真实链 | ①留定时器=补给零成本，弹药经济名义化，D207 真实物流+D210 每发耗原料意图落空，步骤9 验收"真耗仓库存量"过不了；②B 双通道并行稀释成本，平衡不可控；③AI 北极星：免费定时器让 AI 学到"弹药免费"，污染 2_9 经济环训练信号；④体验下限由出生满装填+SO 调参（弹仓 10~20 占位）保，不靠兜底通道 |
| 4 弹药是否进箱子 | **采纳 A**：按设计落地，弹药可装箱 | ①D207 原文"工人搬运弹药（**箱子 D142**）"——设计明文引用，B 需策划豁免而豁免无据；②步骤11 将接三触发源（SpawnChest 现零调用），弹药不装箱则厂→仓背包满路径搁浅弹药；③HH.13 Metal 模式已验证 6 处联动成本，主目的=防静默丢失；④ResourcePack 5→8 字段面可控 |

### 裁决口径补遗（执行端必读）

1. **命名锁定**：`StoneAmmo / FireballAmmo / MagicAmmo`（尾部追加保旧 int 值稳定；同时避开两侧 `ProjectileType.Stone/Fireball/Magic` 撞名——真训练仓 SimBrain.cs 实盘确有同名枚举，Unity 侧同）。
2. **`_ammoStock` 退役而非降级门面**：真源=StorageComponent 仓库（HH.8/16 方向收口）；`SiegeProductionSaveData` 弹药账随之退役，**旧档 ammoStock 读入时迁入厂内存储，不丢档**；`ResupplySiegeUnit` 直填接口随真实链退役（装填改走单位弹仓终点任务）；`ProduceAmmo` 改走厂 StorageComponent。
3. **三项圈定**（附录末"待策划圈定"）：①装箱范围=**全链路**（含步骤11 触发源接通后的厂→仓溢出落箱）；②命名=见口径 1；③携带量**独立配**——`ResourceCarryConfig` 资产追加 3 条占位（SO 铁律，不留代码默认魔法值），**占位 10/趟**。
4. **执行边界**（裁决方向内细节，不另请示）：塔统一装填（D211，塔 ammoMax 资产回填、弹仓 10 发占位）；SiegeWorkshop 专属组件/标记仿 isBlacksmith 先例 + producer 配置；新增 KingdomTaskType 装填任务 + KingdomDestType 单位弹仓终点；弹药品类切换（轮产/按需）属执行细节，保持 SO 驱动。
5. **sim 同步并入 HH.15 台账**（执行端登记不代做，交训练仓会话）：①ResourceType 落盘时**含 3 弹种一次到位**（与 Unity 同值尾部追加）；②决策3-A 产生的行为差（Unity 已退役 TickAmmoResupply vs sim SimUnit/SimBrain 石弹自动补给仍在）登记待办，**是否对齐真实后勤由训练仓会话按训练目标决策，不在本 HH 拍死**。
6. **步骤10/11 注记四大硬发现处置确认**：market.asset 隐性产金 + TaxSystem 大小写 bug + 商店/税务所残留 → 步骤10 D144 既定方向内（"商业只留市场"设计已裁决），bug 顺手修，**不需新 HH**；SpawnChest 三触发源未接 + ModifyResource 满仓静默缩水 → 步骤11 主体工作项，"本步必须堵"注记成立。均不改变本 HH 裁决。

### 分歧裁决记录
无分歧——执行端四项推荐与策划端核验结论一致；给定选项已最优，无需自造（beyond-options 判定①：四案共同前提"弹药需经生产→存储→搬运→装填链"在设计文本成立）。

### 衍生产物
- 无新设计文档（四项裁决均在 2_12 设计 §5.9 既定方向内，属载体/归属的工程落点确认）。
- [HH.15 §三 sim 待办台账扩充](./HH.15_sim侧IWarehouse同步待办登记.md) 2 条：ResourceType 3 弹种同源追加 + TickAmmoResupply 行为差登记（2026-08-23 已回写）。
- 步骤 9 按本裁决直接开工；裁决等待期"并行步骤10/11"选项随裁决完成失效（用户如仍要求并行则另议）。

---

## 附录 A：决策点 1-A 影响面清单（2026-08-23 执行端勘察，全库 `ResourceType.` 156 处/33 文件逐一核验）

判定：**添加（扩展）**，同 Metal 先例（HH.13 裁决 A，尾部追加保旧 int 值稳定=全部"零改动"结论的前提）。

| # | 消费面 | 判定 | 要点（文件:行号） |
|---|--------|------|------------------|
| 1 | ResourcePack + CanAfford/Spend/Refund | 零改动 | WorldConfig.cs:134-159 固定 5 字段；建造/升级成本语义不含弹药。**例外：若决策点4采纳A → 需适配 6 处**（IsZero/operator+/×/CanAfford/Spend/Refund） |
| 2 | StorageComponent/IWarehouse/WarehouseRegistry | 零改动 | StorageComponent.cs:78-88 类型泛化；IWarehouse 签名不动（:13 同源契约注释，sim 登记另计）；厂内存储只需 SiegeWorkshop.asset 配 outputResource |
| 3 | ResourceCarryConfig | 代码零改动 | ResourceCarryConfig.cs:22-30 未命中回退默认 10；资产条目建议追加 3 条（so-data-driven，非阻塞） |
| 4 | TreasureVault.Managed | 2-A 零改动 / 2-B 需适配 | TreasureVault.cs:16-20 固定 6 种；2-B 则 6→9 + 存档链联动 |
| 5 | KingdomSaveData + 恢复链 | 2-A 零改动 | 弹药持久化走建筑链自动（BuildingSaveData.cs:28 storedAmount → Building.cs:578/:618）；2-B 照 Metal 先例 6 处适配。顺带发现：KingdomSaveData.cs:14"7 档"注释过时（实 9 档） |
| 6 | TradeSystem 档位 | 零改动 | TradeSystem.cs:33-48 switch 有 `=>0` 兜底=不可交易；D219 启用归步骤 10（届时加档位+额度 9→12+面板行） |
| 7 | HUD/UI | 零改动 | ResourceHUD.cs:104-119 `_=>null` 静默；WarehousePanel/BuildingPanel switch 有 default；TopLeftHUD 仅王国名/人口/金 |
| 8 | 任务链 | 运行时零改动 | TaskScheduler 全链类型泛化（:592-638 搬运两段）；**需新增（步骤9体内）**：KingdomTaskType 装填类型（TaskPriorityConfig.cs:32-44）+ KingdomDestType 单位弹仓终点（KingdomTask.cs:6-13） |
| 9 | ChestEntity/ChestManager | 见决策点4 | ChestEntity.cs:17 contents=ResourcePack 固定字段 |
| 10 | 全库穷举点 | 零改动 | 无 `Enum.GetValues` 反射穷举；5 处 switch/表达式全有兜底；int 序列化面（BuildingSaveData:30/UnitController v5 carriedType）尾部追加值稳定；`_ammoStock` 退役=步骤9主体 |

**sim 同源风险（实盘核验真训练仓 `ai决策大脑强化训练\`）**：
1. 训练仓真身**尚无 ResourceType**（HH.15 待办未落盘）——3 弹种追加必须并入该待办一次性同源落地，执行端登记不代做；
2. 命名撞名风险：训练仓已有 `ProjectileType.Stone/Fireball/Magic` 同源枚举 → 新成员命名建议锁定 `StoneAmmo/FireballAmmo/MagicAmmo` 后缀；
3. 行为差扩大：sim 侧石弹自动补给（SimUnit.cs:137/SimBrain.cs:971-979）与 Unity 真后勤链分叉（若决策点3采纳A）→ 行为差登记训练仓待办；
4. 不受影响：sim 经济模块（TrainingCostDef 裸字段）不依赖 ResourceType。

**待策划圈定**：① 决策点4 弹药装箱范围；② 3 弹种命名确认（建议 XxAmmo 后缀）；③ 弹药携带量是否独立配（默认回退 10 可运行）。
