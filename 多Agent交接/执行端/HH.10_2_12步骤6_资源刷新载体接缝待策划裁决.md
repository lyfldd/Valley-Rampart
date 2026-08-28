# HH.10 2_12 步骤6 全资源刷新——树数据化后的"刷新对象"接缝待策划裁决

> 类型：待决策（接缝语义澄清）
> 状态：✅已裁决（2026-08-22 策划端，见文末回写）
> 日期：2026-08-22 · 发起端：执行端 · 关联：2_12 步骤6（实施计划 L161-181）/ 设计稿 §5.3（L276-280）/ D54/D61/D108 / A+ 数据化（HH.2）

## 一、现状（执行端系统调研证据）
设计稿 §5.3 定义：**树 = 一次性可刷新**，`BuildingType.Tree` 保留，`isConsumable=true`（采集消失），采后 N 天重生（SO `treeRespawnDays`），N 天到原坑位/附近 1 格内再生成。木为**唯一持续来源**。

**但 A+ 数据化后，实际实现与设计稿脱节**（search 子代理全仓调研）：
1. **Tree 无 Building 实体也无可采集链路**：`MapGenRules.cs:288` 只给 OreVein 留实体（`if (f != FeatureType.OreVein) continue`），Tree 只是 features 数据格（装饰持续节点，`MapGenRules.cs:277-280` 注释）。
2. **工人无法砍树**：TaskScheduler 的 Gather 只认 `source as Building` 实体；`Building.TryAdvertiseTask`（Building.cs:696-709）要求 `isConsumable && isBeingGathered`；`Building.OnGatherCompleted` 要求 `def.isConsumable`（Building.cs:586），而 Tree/Mine 注释明示 `isConsumable=false`。→ 树永远走不到采集链路。
3. **木的真实来源是 WoodPile 实体**：`BuildingType.WoodPile`（可采集一次性资源，`isConsumable=true`），走完整采集→背包→入库链路（BuildingPanel→StartGather→TaskScheduler→OnGatherCompleted）。**WoodPile 与 Tree 是两个东西**，且 WoodPile 不在设计稿 §5.3"树刷新"语义里。
4. **Tree 唯一消耗路径是"建筑覆盖"**：`WorldManager.TryConsumeResourceNode`（WorldManager.cs:199-218）把 Tree/Mine → Plain，但只由玩家放工具建筑触发（BuildController.cs:212），**非工人采集、不记录时间戳、无重生**。
5. **无任何重生机制**：`TreeRespawn` 全仓 grep 0 匹配；`ResourceRespawnSaveData`（D108）不存在。

## 二、接缝本质（需策划澄清的核心）
设计稿步骤6 假设"树可采集→消失→刷新"，但 A+ 后 **Tree 根本不可采集**（无工人砍树链路），"木=唯一持续来源"当前的实现载体是 WoodPile 实体而非 Tree 格。

**"全资源刷新要刷新什么"存在多种理解，直接决定实现路径：**

- **理解甲（设计稿字面）**：Tree 格可采集消失→N 天后重生。→ 需**先补"工人砍树"链路**（Tree 加采集），否则刷新无对象。改动面大（动 TaskScheduler 采集源），超出"刷新系统"。
- **理解乙（贴近现状）**：刷新的是**可采集的一次性实体**（OreVein/WoodPile/StonePile，`OnGatherCompleted` 采集销毁后对象池回收）→ 到点重新生成实体。Tree 格不刷新（它本就不消耗）。→ 保守，改动小，但违反设计稿 §5.3"树刷新"字面。
- **理解丙（双路径，你预埋警告）**：**Tree/StonePile/WoodPile 走数据格翻转**（feature Tree→Plain→N 天→Plain→Tree，需先有采集侧记录），**OreVein 走实体重建**。→ 需要对 Tree 建立"采集即消耗 feature"的数据化采集链路（当前缺失）。

## 三、待决策项
1. **木来源与刷新载体**：A+ 后"木=唯一持续来源"到底是 **Tree 格（需补砍树链路，刷新才有对象，甲/丙）** 还是 **WoodPile 实体（刷新=实体重生，乙）**？若为 Tree，需先立项"树采集链路"（TaskScheduler 采集源扩到 feature Tree）。
2. **步骤6 落地双路径范围**：若按丙（你预埋的警告），Tree/StonePile/WoodPile 的数据格翻转需要**消耗侧记录入口**——但当前唯一消耗入口是 `TryConsumeResourceNode`（建筑覆盖，非采集）。需定：是否本轮补"资源点采集→feature 记录"的消耗记录，供刷新 reverse。
3. **存档结构（D108）**：你已定"数据路径(Tree)与实体路径(OreVein)分开设计，别混一个 list"——需确认 Entity 路径（OreVein 实体 index）与 Data 路径（Tree 格 index）的存档字段分离方案。

## 四、我的倾向
- **刷新载体 = OreVein 实体（已存在采集销毁链路，`OnGatherCompleted` 已调 HandleResourceConsumed）**，本轮落地**实体重生**最稳（乙）。
- **Tree 格刷新**：鉴于 Tree 当前不可采集（无消耗对象），"树刷新"无实际触发点。若策划坚持设计稿"Tree=木唯一持续来源"，则需**单独立项补树采集链路**（非本步范围）。
- 建议：步骤6 先做**实体路径重生（OreVein/WoodPile/StonePile）** 走通 D61 机制 + 存档（D108）；**Tree 数据格刷新**因采集链路缺失，挂债务卡或单独立项。

## 五、下一步建议
- 策划裁决：刷新载体（乙实体路径优先 / 需补树采集链路的甲/丙）、双路径范围、存档字段分离。
- 恢复入口：本 HH + 2_12 实施计划步骤6 + 工作日志。

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 1. 木来源与刷新载体 | **丙⁺ 双路径**：Tree=数据格（feature Tree→Plain→N天→翻转回，Tilemap UpdateCell 刷视觉，数千棵实体化=A+ 复辟不可接受）；OreVein/WoodPile/StonePile=实体路径（DeriveNaturalBuildings 扩到三类）。**策划补核实：WoodPile/StonePile 实体在当前 2D 图根本不生成**（DeriveNaturalBuildings 仅 OreVein，L287），"乙链路已存在"高估——链路代码在、生成源没有，**当前 2D 构建木头零来源**（开局 100 木耗尽即经济枯竭）。数量级安全：三类实体合计约 ~2000（=A+ 前 1.6 万的 1/8） | 乙=刷新空壳（木经济仍断，步骤6 验收行"砍树→消失→重生"无法 PASS）；甲单独立项否决——砍树链就写在步骤6 验收行里，不是新立项 |
| 2. Tree 采集记录入口 | **本步补齐，这是步骤6 本体工作非前置**：采集源抽象扩展（TaskScheduler Gather 目标从仅 Building 实体扩为实体或 feature 格，IGatherSource 或等价）；数据采集时序=工人到树格→采集耗时（SO 树≈2s 档）→木入 WorkerInventory→feature 翻 Plain+RecordDepleted(cell,Tree,day)+Tilemap 更新。**性能红线：出图 <1.5s（HH.2 基准+实体扩容余量），超线=实体路径越界，停手回 HH** | 验收行字面要求；拖走=改验收稿 |
| 3. 存档结构 D108 | **双列表分离**：EntityRespawnEntry（实体重建）与 DataRespawnEntry（格翻转）分开；SO 周期按类型分键（树 7 天/石矿更长，D61"树短石矿长"） | 两路径生命周期不同，混一个 list=迁移地狱 |

**附带**：北极星加分——sim 侧经济（T2.4）本就树抽象为数据，Unity 数据化采集拉近同源；2_9 对拍清单补"树存量/木产出"字段对齐。守卫互扰自查维持（步骤5 已验 OreVein 采集→LostEvent 链随本步复跑信标）。

### 分歧裁决记录（有分歧时必填）
- 执行端意见：倾向乙（实体路径最小改动，Tree 刷新挂债务卡或单独立项）。
- 策划端意见：破框否决乙——补核实 WoodPile/StonePile 无生成源，当前木经济全断，乙=给断腿经济装刷新；甲丙之辨消除——砍树链是步骤6 验收行本体，非范围外。
- 裁决：丙⁺ · 依据：AI 北极星（数据化树采集拉近 sim 同源）/ 设计完整性（木=唯一持续来源是经济主循环）/ 性能红线（~2000 实体 vs A+ 1.6 万，出图 <1.5s 守住 HH.2 战果）

### 衍生产物
- 新建清单/文档：2_9 对拍清单补"树存量/木产出"字段（随步骤6 收尾登记）；ResourceRespawnSaveData 双列表（D108，本步落地）