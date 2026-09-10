# HH.164 HH.159 件1 矿石落库链缺口 + 件2 D614 同步缺口 + 两笔口径请裁

> 类型：待决策
> 状态：✅已裁决（D617，2026-09-10；七项全清）
> 日期：2026-09-10 · 发起端：执行端 · 关联清单/文档：HH.159（D610/D612/D613/D614）· HH.159_实施清单 · 0.6 §一百三十八~一百四十三
> 账本：HH.164（2026-09-10 先登记后落盘）· 完成报告=HH.160（预留）
> 纪律：本信为「执行端不确定/需策划在动笔前回答」的事项上报，**未自行拍板**；相关件按 🚧 挂起待裁。

---

## 一、做了什么（执行端填，带证据）

HH.159 按实施清单 §五 施工序推进，已完成并自证（编译 0 新增错误 + grep/Unity 实盘双锚点）：

| 件 | 状态 | 证据 |
|---|---|---|
| 件6 T6.2/T6.3 | ✅ | `WarehouseHelper.TrySettle/CanAfford` 补 `ResourceType.Metal`（L39/L45/L67）；`BuildingMenuPanel` 置灰+点击守卫改走 `WarehouseHelper.CanAfford` |
| 件7 T7.1/T7.2 | ✅ | `CampUpgrader.cs` L74-78 过时注记改；`GuardDeploymentSystem.cs` L64 勘正（2_13 批B 已接线）；`TreasureVault` 特食/肉折损注 + `AIEconomySettlement` 非经济资源无消费端注 + 水井拦截注记勘正（含 typo `kingidId`）；grep 旧文本零命中 |
| 件4 T4.1/T4.2/T4.3 | ✅ | DZ-077：13 处无守卫发布补 `HasSubscribers` 守卫 + 两死定义删（`UnitAttackEvent`/`BuildingProductionTickEvent`）+ `LODSystem` L335 恒 0 残式清理；DZ-078：`BuildingFactory` econStorageOnly 排除 `outputResource==Gold`；DZ-079：`HappinessSystem.HasBuilding(id, kingdomId)` 新重载 + `SatietySystem` 改 per-kingdom 调用 |
| 件2 资产侧 | ✅（11 笔） | 11 笔 footprint 逐笔改毕（含 mine 1×1→2×2）；新建 `portal.asset`（faction=Monster/role=Special/2×2，`.meta` 已生成）。Unity `Resources.Load<BuildingDef>` 实盘直读 12 条全部新值命中 |
| 件1/件2 代码侧 | 🚧 | 见下（阻塞） |

**事故自曝（已自纠）**：同一条消息内对**同一文件**并行发多个 SearchReplace，触发 L-12 违规 → 5 处回显成功但未落盘（SelectionController×3 / NPCBrain×1 / SaveManager×1），经**写后必验（L-02）grep 当场抓出**，已改串行逐处重做并复验通过。拟入 HH.160 教训核查行。

---

## 二、现状与阻塞

### 阻塞 1：件1「Ore 落库链」在设计上是空的（清单假设被实盘证伪）

清单 §三/§四 断言「Ore 搬运链已在位…**国库槽已在场**——禁止重复建设，grep 复核即可」。**实盘 grep 结果与之不符**：

| 环节 | 实盘事实（file:line / 证据） | 结论 |
|---|---|---|
| 国库槽 | `TreasureVault.cs` L17-22 `Managed` = {Stone, Wood, Food, SpecialFood, Meat, Metal, Crystal, FireOil}——**无 Ore**；L99-106 `Deposit` 对未纳管类型直接 `return 0` | ❌ 无 Ore 桶，入国库即静默丢（仅一行日志） |
| 国库真源缓存 | `KingdomManager.cs` L394-402 `TreasuryStone…TreasuryMetal`+`TreasuryCrystal/TreasuryFireOil`——**无 `TreasuryOre`** | ❌ 无桶 |
| 存档 | `KingdomSaveData` 仅 `treasuryStone…treasuryFireOil` | ❌ 无 `treasuryOre` 字段（即便加桶也不持久化） |
| 采集/搬运溢出 | `TaskScheduler.cs` L628-636 `AddGatherOverflow` 玩家侧 → `RulerController.ModifyResource(Ore,…)` → 同上 `Deposit` 落空；卸货 L716 `WarehouseRegistry.FindNearestAvailable(Ore,…)` 无同类型仓库 → 必走溢出链 | ❌ 采集/搬运的 Ore 同样落空 |
| 已在场（清单所指） | `ResourceCarryConfig`（Ore=10）、`BuildingPanel.cs` L511 / `WarehousePanel.cs` L199 / `TradePanel.cs` L36 显示名、`BlacksmithDef` 比率 | ⚠️ **只有「搬运 + 显示」，没有「落库」** |

**后果**：若照 T1.5 让 `ore_vein` 改产 Ore、照 T1.1/T1.2 让铁匠铺吃 Ore，**矿石会在入库点被静默丢弃**，Ore→Metal 链不通；T1.4（mine Ore 第三子仓）同型——子仓可产，但搬运落点仍缺失。

### 阻塞 1 附：Ore 生命周期八格（AGENTS.md §〇）现状盘点

| # | 环节 | 现状 |
|---|---|---|
| 1 | 诞生 | mine 伴生（T1.4 待建）/ ore_vein 采集（T1.5 待改产）——**生成端可建** |
| 2 | 归属 | ❌ **无归属容器**（国库无 Ore 桶；仓库 `StorageComponent` 按 `resourceType` 单类型，世界无 Ore 型仓库） |
| 3 | 更新 | mine 子仓 Tick（T1.4 待建）✅可；采集溢出/卸货 ❌ 落空 |
| 4 | 挂起/恢复 | N/A（无容器，无从谈挂起） |
| 5 | 正常终结 | 铁匠铺 `StorageComponent.Transform(Ore→Metal)`（T1.1/T1.2 待改）❌ 依赖环节 2 |
| 6 | 异常终结 | ❌ **静默丢弃**（`Deposit` 返回 0，`ModifyResource` 仅打日志）——违 AGENTS.md「失败可见」 |
| 7 | 清算 | ❌ 无 |
| 8 | 持久化 | mine 子仓侧 `BuildingSaveData.byproductOreAmount`（T1.4 已含）；**国库侧 ❌ 无字段** |

→ **八格中 2/5/6/7/8 空格**，即「有生成、无归属、无终结、无持久化」——纯生命周期断链（P8/幽灵引用家族）。

**连带疑点（一并请裁）**：`ProducerComponent.cs` L87-111 `UpdateByproductConfig` 以 `def.outputResource == ResourceType.Ore` 判「矿洞」并挂水晶/火油副产（旧 Lv2/Lv3 门槛设计）——D562 后 mine 已改 `outputResource=Stone`+独立 `MineByproductComponent`，该分支在现网**已不可达**（mine 为 `isResourceNode` 被 Factory 产能分支排除）。属历史残留，本批未动，请裁「删/留注释」。

### 阻塞 2：件2 D614（House）清单体未同步

- 账本水位线与 0.6 §一百四十三（D614）**已落**：House footprint 1×1→2×2 + 房容 ×4=12/20/32 + 房容 SO 化入 KingdomConfig + 任务 45→48（新增 T2.15/T2.16/T2.17），且 0.6 声称「HH.159 任务书件2 + 实施清单（已扩 48 条）」「spec 三件套（已同步）」。
- **实盘不符**：`HH.159_实施清单.md` 仅**头部裁决链**加了 D614，body 仍为 **45 条 / 11 笔**，**无 T2.15~T2.17、无 48 条**；`.trae/specs/clear-hh159-backlog/` 亦未同步。疑为同文件部分写入/落盘未全（HH.42 家族变体）。
- 我方可自处理的：**spec 三件套属执行端产物，我可自行同步**（待裁决事项 3 确认口径后执行）。**实施清单属策划端单写者产出，我不改**（agent-handoff §六 #6）。

---

## 三、待决策事项（每项：选项 + 推荐 + 影响）

### 决策 1（件1 核心）：Ore 落库链如何补？

- **A（执行端推荐）**：本批补齐 Ore 国库链 = ①`TreasureVault.Managed` 增 `ResourceType.Ore` ②`KingdomManager.TreasuryOre`（含 `LoadState`/`ResetState` 同步）③`KingdomSaveData.treasuryOre` 尾插（旧档缺→0，零 schema bump）④`TreasureVault.Init` 读档回填一行。
  - 理由：与既有 Stone/Wood/Food/Metal/Crystal/FireOil 的国库模式**完全同构**，是 T1.1/T1.2/T1.4/T1.5 的**依赖闭合必要项**（否则件1 无验收价值）。
  - 影响：约 15 行；**改到存档字段**→ 需登记 15_账本（M7 sim 义务面扩大：KingdomConfig 三字段 + BuildingSaveData 尾插 + **KingdomSaveData.treasuryOre** + 变换语义注记）；四容器回归须覆盖读档。
- **B**：Ore 不入国库，改为「Ore 只走仓库 `StorageComponent` + 铁匠铺经 `WarehouseHelper` 读 Ore」。
  - 理由：不动存档。
  - 影响：改动面更大（要造 Ore 型仓库或让仓库多类型化），且与既有 `ruler.Stone` 单源模式**分裂**；铁匠铺 `Transform` 的原料校验逻辑要重写。
- **C**：暂缓 T1.1/T1.2/T1.4/T1.5（🚧），本批只做 T1.3（改名）/T1.6/T1.7（注释），另立「Ore 落库口径」小批。
  - 影响：件1 本批只完成 3/7，件1 验收降级；但不动存档、风险最低。

> 补充请示：若裁 A，请一并明确 **Ore 是否进 AI 台账**（`KingdomState.resources` 现为五经济资源，Ore 不在其中）——AI 侧 mine 伴生/矿脉采集的 Ore 是否同链入账，还是 AI 侧 Ore 直接内部消化不过账。

### 决策 2（件6 T6.1 / DZ-054）：一次性资源点 `isResourceNode` 是否对齐？

- 台账 DZ-054 称「消费面**仅 Demolish 守卫**」；实盘为 **4 处**：`WanderStimulusProvider.IsResourceDef`（→该 def 成为 NPC 游荡锚点候选）、`BuildingPanel.canDemolish`、`Building.Demolish`、`BuildingFactory` 产能分支（`!def.isResourceNode`）。
- 其中对 `stone_pile`/`wood_pile`：Demolish 与 Factory **无实际影响**（二者非 `isPlayerBuilt`、`rate=0`），但 **`WanderStimulusProvider` 会真实新增游荡锚点**（AI 行为变化）。
- **A**：按字面对齐（`stone_pile`/`wood_pile` 置 1），接受 AI 游荡行为变化。
- **B（执行端推荐）**：**维持现状**（ore_vein=1；stone/wood_pile=0），补注记说明三者语义差异（矿脉=一次性探明矿点 vs 石堆/木堆=开局过渡堆积），零 AI 行为变化；台账 DZ-054 以「消费面核实结论」修正销案。
- **C**：不动数值，改在 Demolish 消费点显式白名单化（改动更大）。
- 影响：A 会使 AI 工人游荡/守卫覆盖出现新目标点（可能改变早期 AI 行为，与「批内零行为漂移」期望冲突）；B 零风险但保留标志不一致事实（以注记说明）。

### 决策 3（件4 T4.1 尾项）：`LODSystem` L335 `RegionHeatChangedEvent.RegionIndex` 如何落地？

- 现状：原式 `s.threatHeat >= 0f ? 0 : 0`（恒 0 残式）；该事件**零消费者**（旧兼容面），`MidChunkLodState` 无「区域序号」字段（只有 `midChunk: Vector2Int`）。
- **A（已按此落地，推荐）**：清理残式，显式传 `0` + 注释「旧兼容事件零消费者，无索引语义」。最小改动。
- **B**：真值化——改 `UpgradeImmediate(s)` 签名增 `int regionIndex`（3 处调用点），用 `s.midChunk` 派生索引；需自定索引映射（`Vector2Int`→int）。
- 影响：B 改动面稍大且需新增映射约定；A 保持零行为、去噪。

### 决策 4（件2 同步）：D614 落地口径确认

- 请确认：① `HH.159_实施清单.md` body 未同步 D614 属**部分落盘**（应由策划端补 48 条 body + T2.15/T2.16/T2.17 规格）；② 执行端是否**即刻按 0.6 §一百四十三** 实施 House 2×2 + 房容 ×4=12/20/32 + 房容 SO 化入 `KingdomConfig`（含 T2.16 测试夹具 `TestFixtureTiersConfig`「每 House=3」重算、T2.17 HH81 P1 + `TestFixtureApi` 读数复核），**不等清单 body 补齐**？
  - 执行端倾向：**照 0.6 裁决立即实施**（裁决已明确四决策全清，清单 body 补齐属策划端回写义务，不构成实施前置）。

---

## 四、下一步建议

1. 决策 1 裁 **A** → 执行端按 ①~④ 补 Ore 国库链，再串行推进 T1.1→T1.2→T1.3→T1.4→T1.5→T1.6→T1.7（T1.5 与 T6.1 同文件 `ore_vein.asset` 串行），并登记 15_账本。
2. 决策 1 裁 **C** → 执行端只做 T1.3/T1.6/T1.7，件1 余项标 🚧，并在 HH.160 列报。
3. 决策 2/3 按裁决落地后，T6.1 与 T4.1 尾项即可销项。
4. 决策 4② 若准，执行端在等待期继续推进件3/件5/件9/件10/件11（与本阻塞零文件交集），并按 D614 实施 House 侧。

---

## 策划裁决（策划端回写，裁决前保持空白）

> 裁决日期 2026-09-10 · 裁决号 **D617**（0.6 §一百四十六）· 上报质量嘉奖（实盘 grep 证伪清单假设+八格盘点，L-01 家族正面样本）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 1. Ore 落库链 | **A**（同构补齐 Ore 国库链） | ①与 Crystal/FireOil（DZ-072a 刚扩）**全同构**，先例现成；②`RulerController.ModifyResource/GetResourceValue` 为 ResourceType 泛型（L226-299）→加桶即打通全链，~15 行最小改；③是 T1.1/T1.2/T1.4/T1.5 的**依赖闭合必要项**（不补则 T1.5 改产后 Ore 入国库静默丢=件1 无验收价值）。**否决 B**（与既有 `ruler.Stone` 单源模式分裂+须造 Ore 型仓库/多类型化，面更大）；**否决 C**（件1 只成 3/7+留半态「改产即丢」）。四步=①`TreasureVault.Managed += Ore` ②`KingdomManager.TreasuryOre`（含 LoadState/ResetState）③`KingdomSaveData.treasuryOre` 尾插（旧档缺→0，零 bump）④`TreasureVault.Init` 回填一行。**15_账本 M7 扩面**=+`KingdomSaveData.treasuryOre`。 |
| 1-附. AI 侧 Ore 是否入台账 | **入 KingdomState 独立桶**（照水晶/火油先例；**不进 ResourcePack 五经济资源**） | ①AI 国库政策明文「只认五经济资源（Gold/Stone/Wood/Food/Metal），Ore/Crystal/FireOil/特殊食物/肉/弹药不入」（`AIEconomySettlement` L16/L113）；②`KingdomState.crystal/fireOil`（L48-55）正是此先例=单列独立桶+`AddGatherOverflow` 入账+不入档，Ore 同族照抄加 `ore` 桶+`case Ore`；③保「不静默丢」（八格红线）。**列报**：AI 消费端随主体对称化（2_24 批2/2_23 批B）接入，当前=有产无消过渡态（挂账注记，非静默）。 |
| 2. isResourceNode 对齐 | **B**（维持现状+注记） | ①`isResourceNode` 实盘 4 消费面（`WanderStimulusProvider.IsResourceDef` L181 / `BuildingPanel.canDemolish` / `Building.Demolish` / `BuildingFactory` 产能分支），其中 `WanderStimulusProvider` 会使 def **成为 NPC 游荡锚点候选（真行为）**→A（三资产全置 1）会真实新增 AI 游荡锚点=批内行为漂移，**否决**；②C（Demolish 白名单化）改动更大且消费点本身无误，**否决**；③`ore_vein=1`（一次性探明矿点）vs `stone_pile/wood_pile=0`（开局过渡堆积）=**语义差异如实**非缺陷→补注记说明三者语义。**DZ-054 以实盘 4 消费面核实结论修正确销案**。 |
| 3. LODSystem RegionIndex | **A**（显式传 0+注释，已落地） | ①该事件**零消费者**（旧兼容面），`MidChunkLodState` 无「区域序号」字段（只有 `midChunk:Vector2Int`）；②B 真值化须自造 `Vector2Int→int` 映射约定=无消费端的规格劳动，**否决**；③A 去噪、零行为（L334-338 实盘在位）。 |
| 4. D614 落地口径 | **①确认部分落盘+已补齐；②准即刻按 0.6 §一百四十三实施** | ①报告时刻属实（账本/0.6 已落、实施清单 body 仍 45 条）——**已由 HH.163 串回写补齐**（本裁决复核 body 48 条在位；注：终版编号=T2.1~T2.12 变更/T2.13 传送门/T2.14 预置链/T2.15 选址/**T2.16 房容/T2.17 夹具**，「T2.15~T2.17」=顺延前口径）；②裁决=权威口径，清单 body 补齐属策划端回写义务，**不构成实施前置**→准即刻实施 House 2×2+房容 ×4=12/20/32+SO 化（含 T2.16 夹具重算/T2.17 读口复核）。**spec 三件套**按 D616 降级为时点快照，执行端自行同步/停逐笔同步。 |
| 补1. 件3 核查列报（B1-1/2/4 已实装 + B1-3 边缘语义） | **采信 + 认可修正** | ①**核查采信**：B1-1/B1-2/B1-4 **已被前批实装**（HH.86 件2a/2d、2_17 步骤11 批2）→HH.159 件3 对应任务**降级为核实/对拍**（禁重复实装）；②**认可 B1-3 边缘语义修正**：`kid>0 但国已注销` 旧=fallback 玩家国库（**主体串味/资敌**，正是 D545 主体对称要治的缺陷）→新=不进食（国已注销=单位随之消亡，语义自洽）。 |
| 补2. ProducerComponent Ore 死分支 | **删** | `ProducerComponent.UpdateByproductConfig` L91 `outputResource==Ore` 判矿洞（旧 Lv2/Lv3 副产门槛）在 D562 后**已不可达**（mine 改 `outputResource=Stone`+独立 `MineByproductComponent`）=死代码，且与 `MineByproductComponent` **双源误导**（保留=误导未来读者「矿洞副产在 ProducerComponent」）→删该分支（含 `_hasByproduct/_byproductType/_byproductCapacity` 死字段，grep 兜底）。若牵连面超预期→降级为留最小注释+台账登记。 |

### 分歧裁决记录（有分歧时必填）
- 执行端意见：决策 1 主张 A（同构补齐，否则件1 无验收价值）；决策 2 主张 B（保零行为漂移）。
- 策划端意见：**与执行端一致**——决策 1 采纳 A（唯一闭合路径）；决策 2 采纳 B（A 会经 `WanderStimulusProvider` 真实新增游荡锚点=行为漂移，违批内零漂移）；另补决策 1-附（AI 侧独立桶）与补 2（死分支删）两裁。
- 裁决：**决策 1=A / 1-附=入独立桶 / 2=B / 3=A / 4=准即刻实施 + 补1 采信认可 / 补2 删**。 · 依据：D617（0.6 §一百四十六）——逐条实盘复核实锚在卷（TreasureVault L17-22/L101、KingdomManager L394-402、KingdomSaveData、RulerController L226-299、KingdomState L48-55、AIEconomySettlement L16/L113、WanderStimulusProvider L181、LODSystem L334-338、ProducerComponent L91）。

### 衍生产物
- 新建/修订设计文档：0.6 §一百四十六（D617）· 账本（HH.164 销号+D617+水位线+D 区行）· 索引 HH.164 行 · 队列积压批行 · 缺陷台账 DZ-054 销案 · 本信裁决区。
- 新建清单任务：**T1.8**（Ore 国库链同构补齐+AI 独立桶）· **T7.3**（ProducerComponent 死分支清理）；**T6.1 降级**（维持现状+注记+DZ-054 修正销案）；**T3.1/T3.2/T3.4 标核验**（前批已实装）；任务计数 **48→50**。
