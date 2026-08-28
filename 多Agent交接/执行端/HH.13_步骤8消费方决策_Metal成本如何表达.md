# HH.13 步骤8 消费方决策：Metal 成本如何表达（D131/D132）

> 类型：待决策
> 状态：✅已裁决（2026-08-23 策划端，见文末回写）
> 日期：2026-08-23 · 发起端：执行端 · 关联文档：2_12_王国建筑系统迁移_实施计划（步骤8 铁匠铺与 Metal 数据层 D199~D201，§5.8）
> 背景：HH.12 策划已放行步骤8，范围含「Metal 消费方（D131 工事升级 / D132 兵种强化，从仓库取）」。执行端按序开工，步骤8.1/8.2 完成，卡在 8.3 的 Metal 成本数据结构表达。

## 一、做了什么（执行端，带证据）

依 HH.12 裁决，Metal 前置并入步骤8 首个动作、随 BlacksmithDef SO 同 commit（未单独提交，留步骤8 收尾的 git-plan-sync 一并提交）。

| 步骤件 | 内容 | 证据 |
|--------|------|------|
| 8.1 Metal 枚举入库 | `ResourceType.Metal` 追加到 Unity `GameEvents.cs` 与 sim `harness/Core/IWarehouse.cs`（均末尾追加，旧值稳定）+ `Data/BlacksmithDef.cs`（SO，stoneToMetalRatio 占位 2:1）+ `Resources/Config/BlacksmithDef.asset`（MetalFrom 验证 ratio=2 OK） | 编译 0 error；asset 已 Resources.Load 验证 |
| 8.1 RulerController Metal 国库槽 | `Metal` 属性 + ResetState + GetResourceValue/SetResourceValue（过渡：实体资源暂走国库槽，8.4 退役迁移仓库时一并处置；禁双写红线遵守） | — |
| 8.2 铁匠铺 | 新建 `Systems/Kingdom/BlacksmithBuilding.cs`（Stone→Metal 就地加工 D200，metal 累计器，0.5/s）+ `StorageComponent.Transform` 兑现 Metal 分支（扣国库石、加 Metal 入仓，容量整批）+ `BuildingDef.isBlacksmith` 标记 + `ProductionSystem` 逐秒 tick + `BuildingFactory` isBlacksmith 挂 BlacksmithBuilding 替代 ProducerComponent + `Resources/Buildings/Blacksmith.asset`（rate 0.5、cap 250、cost 50金/60石、isBlacksmith=True） | 编译 0 error；asset 已创建并验证（id=Blacksmith out=Metal rate=0.5 cap=250 isBlacksmith=True） |

**当前进度**：步骤8.1✅ / 8.2✅；**8.3 消费方阻塞**；随后 8.4 RulerController 退役三件套 / 8.5 sim 门禁核对 / 最终 Play+编译+git-plan-sync 提交（含开发计划书 4 行工作日志）。

## 二、现状与阻塞

- **阻塞点**：Metal 消费方（D131 工事升级 / D132 兵种强化）要"从王国仓库取 Metal"，但**现有成本结构无法表达 Metal**，需策划拍板数据结构方向后才能动代码：
  - **工事升级成本** = `ResourcePack`（金/石/木/粮四元组），无 `metal` 字段 → 城墙/塔升级（D131）无法声明 Metal 消耗。
  - **训练成本** = `TrainingDef`（`costGold`/`costCrystal`/`costDays`），无 metal 字段 → 强化兵种训练（D132 重装战士/盾卫/骑兵）无法声明 Metal 消耗。
- **为什么执行端不能自己定**：扩展 `ResourcePack`/`TrainingDef` 是跨切核心数据结构改动，影响→工程面（RulerController.CanAfford/Spend/Refund、BuildingPanel 升级 UI、TryUpgrade 记账、资源事件）+ 数据面（D5"全 SO 无硬编码"红线、后续步骤14 SO 审计/步骤10 贸易 Metal 等级）+ 数值面（§5.13 工事升级未给 Metal 数值，需给占位）。项目协议禁止执行端擅自拍板此类设计决策。
- Metal 现暂以 `RulerController.Metal` 国库槽过渡（HH.12 注记：8.4 退役迁移仓库时一并处置）；消费方扣减路径随下表数据结构方案而定。

## 三、待决策事项（每项：选项 + 推荐 + 影响）

### 决策1：Metal 成本用什么数据结构表达（核心，决定 8.3 全链路）
- **A（推荐）扩展数据模型**：`ResourcePack` 加 `metal` 字段（默认 0，新字段向后兼容、不改旧资产序列化）；`TrainingDef` 加 `costMetal`；`RulerController.CanAfford/Spend/Refund` 补 metal（含原子校验 + 按比例退款）；`BuildingPanel` 工事升级 UI 文案加"铁"、`Building.TryUpgrade` 的 totalInvested 记账含 metal；墙/塔升级资产给 metal 数值占位、强化兵种训练项给 costMetal。
  - 理由：工事升级统一走 ResourcePack（D131）、兵种强化走 TrainingDef（D132），两条消费路径都回到"从仓库取 Metal"，与 SO 数据驱动红线（D5）一致；一步到位达成步骤8 验收"工事升级/兵种强化消耗 Metal"，且为步骤10 贸易 Metal 等级、步骤14 SO 审计铺平。
  - 影响：跨 3 个数据类 + 1 单例资源账本 + 升级 UI + 若干资产数值；均向后兼容、低风险，属一次性结构定稿。
- **B 最小占位不改结构**：仅 `TrainingSystem` 加硬编码 metal 判定（强化兵种），工事升级用独立 metal 门（不碰 ResourcePack）。
  - 理由：改动最小。
  - 影响：两条消费路径语义割裂、工事升级不进统一成本管线，与 D5 相悖；后续步骤10/14 审计必返工，属于技术债。**不推荐**。

### 决策2：Metal 数值占位来源（在决策1 采纳 A 前提下顺带定，可合并进决策1 裁决）
- 现状：§5.13 只标注"城墙/城门/塔 可 Metal 升级 D131"，**未给每次升级的 Metal 数值**；强兵种也未给 costMetal。
- 建议（执行端）：工事升级 Metal 消耗占位 = 每级 10 Metal（城塔/城门同），强兵种 costMetal 占位 = 各 5 Metal，均 SO 可调、后续数据卡 D258 审计统一替换。请策划确认或给正式占位值。

## 四、下一步建议（裁决后恢复）

1. 取策划对 HH.13 决策1（数据结构，推荐 A）/决策2（数值占位）的裁决。
2. 裁决 A → 扩展 ResourcePack/TrainingDef + 消费方接入（墙/塔升级金属消耗 + 强兵种训练金属消耗）+ 资产数值占位；裁决 B → 最小改动。
3. 随后：8.4 RulerController 退役三件套（资源池迁移 + 退役 + GameOver 切换=ThroneAnchor.IsKingdomLost 轮询替 OnMonarchDied，步骤5 挂账在此清）→ 8.5 sim 侧 IWarehouse 门禁收尾核对（HH.8 握手，验证 sim harness/Core 是否有 Transform 实现参考，签名逐字对齐）→ Play 验证石→Metal 转化闭环 + 编译 0 error → git-plan-sync 提交（含开发计划书 4 行工作日志）。

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 决策1 Metal 成本数据结构 | **采纳 A（扩展数据模型），附实现精度要求**：执行端方案未提一处易漏点——**ResourcePack 的 operator + 与 operator \* 必须同步扩 metal**（WorldConfig.cs L142-155 现只算四元组），漏掉则 totalInvested 记账含 metal 但拆除返还 pack×0.5f **静默丢 metal**、修复成本摊回（D155）同理。验收清单加"拆除/修复含 metal 往返"一条。其余按 A 全案：ResourcePack.metal（默认0向后兼容）+ TrainingDef.costMetal + CanAfford/Spend/Refund 补 metal（原子校验+按比例退款）+ BuildingPanel 文案 + totalInvested 记账含 metal | B 硬编码违 D5 且路径割裂步骤10/14 必返工；A 是唯一正路，但运算符漏扩=隐形资源黑洞 |
| 决策2 Metal 数值占位 | **确认占位，一处微调**：工事升级每级 10 Metal（墙/门/塔同值，占位期不差分，D258 审计再分层）——确认；强兵种 costMetal 建议分层占位：重装战士 5 / 盾卫 5 / 骑兵 8（保留"高阶更贵"形状，审计改数值不改形状；嫌麻烦全 5 也可接受，非阻塞）。均入 SO，禁写死代码 | 占位全等值=平衡期整体推翻；分层占位保留结构 |

**8.4 前置提醒**：RulerController 退役三件套开工时，Metal 国库槽（过渡态）随其他实体资源一起迁 IWarehouse，Transform 扣国库石路径同步改走仓库接口——勿留国库槽死角导致退役不彻底。

### 分歧裁决记录（有分歧时必填）
- 执行端意见：决策1 推荐 A；决策2 建议 工事10/强兵种各5。
- 策划端意见：均同意方向，补两处精度（运算符同步扩 metal 防静默丢铁；兵种分层占位保形状）。
- 裁决：A + 运算符精度要求 / 占位确认+分层微调 · 依据：SO 铁律 D5（B 直接违） / 数据完整性（运算符漏扩=隐形黑洞） / D258 审计节奏（占位保形状省返工）

### 衍生产物
- 新建设计文档：无
- 新建清单任务：验收清单补"拆除/修复含 metal 往返"（随 8.3 落地）