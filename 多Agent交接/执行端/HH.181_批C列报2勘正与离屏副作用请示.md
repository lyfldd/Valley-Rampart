# HH.181｜2_23 资源 P0 批C 列报2 勘正请示（C′ 前提勘正＋范围实勘＋离屏副作用）

> 执行端（Unity 轨）· 2026-09-11
> 状态：✅ 已裁决（**D644**，2026-09-11 主策划端；裁 A 收敛版＋姊妹项挂账 DZ-089）
> 依据：D643（0.6 §一百七十二，2026-09-11 批C 验收成立销号＋列报三裁：列报2 **🔴改裁 B′**＋附条 **C′**）· HH.180 §四列报2 · HH.180 §六（R-C5 契约）
> 裁定请求：C′ 的**实施口径**（因判据三直读发现 D643 前提细节与实际不符，且照抄 C′ ② 有未预期副作用）

## 一、D643 附条 C′ 原文（待实施项）

- ① `ProduceKind` 尾部追加 `None`（禁改中间位）
- ② 全库清点非产能建筑显式 `kind=None`（禁凭名单）
- ③ 正例探针「有 Granary 无 Farm 仍建 farm」
- ④ 契约 §六 同步
- 定性：「C′ ＝ 资源 P0 收口最后一步 ⇒ 七考前置附条（批C 销号不阻塞；**七考放行需 C′ 落盘**）」

## 二、判据三直读：勘正三条（逐条附 file:line 实读）

### ① 前提勘正 —— 资产**都有** `producer:` 段（D643 所述「granary/warehouse 无 producer: 段」不成立）

- 全库 40 个建筑资产（`Assets/Resources/Buildings/*.asset`，排除 `BuildingMappingTable.asset`）**逐个**含 `producer:` 段——`grep "^  producer:"` 命中 40/40。
- `Granary.asset:38-41`：`producer:` → `kind: 0` / `rate: 0` / `capacity: 60`；`outputResource: 3`（=Food）
- `Warehouse.asset:38-41`：`producer:` → `kind: 0` / `rate: 0` / `capacity: 40`；`outputResource: 2`（=Wood）
- **真实机制**＝`kind: 0`(=Resource，亦即枚举默认值) ＋ `rate: 0` ＋ `outputResource` 默认/显式落值 ⇒ 缺陷机理与 D643 结论**同向成立**，但触发路径是「**有 producer 段但 rate=0 未设 kind=None**」，非「无 producer 段致默认」。

### ② 范围实勘 —— 缺陷范围比 D643 所述（granary/warehouse 2 个）**宽**

`ResourceType` 实读（[GameEvents.cs:176-190](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Core/GameEvents.cs#L176-L190)）：`Gold=0 / Stone=1 / Wood=2 / Food=3 / Ore=4 … Metal=9`。

[MapProduceToEco](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L492-L505) 只卡 `kind != Resource → -1`，**缺 rate>0 守卫** ⇒ 所有 `rate=0` 建筑一律按 `outputResource` 计入产能盘点：

| 资产 | rate | outputResource | 现被计为产能 | 备注 |
|------|------|----------------|--------------|------|
| Granary | 0 | 3 = Food | **Food** ❌ | 掩蔽通道A（D643 所指） |
| Warehouse | 0 | 2 = Wood | **Wood** ❌ | 掩蔽通道A/R-C2 |
| wood_pile | 0 | 2 = Wood | **Wood** ❌ | 采集点 |
| stone_pile | 0 | 1 = Stone | **Stone** ❌ | 采集点 |
| Barracks / castle / wall / gate / House / Church / market / Hospital / TrainingCamp / WarCamp / WarAcademy / LeyForge / ArcheryRange / SiegeWorkshop / AdvancedStorage(rate=1) 等 **~25 个** | 0 | 0 = **Gold（默认值）** | **Gold** ❌ | 数量最大的一族 |
| ore_vein | 0 | 4 = Ore | —（非五元 → -1）✔ | 不受影响 |

- **口径不一致实锤（源头）**：[BuildingFactory.cs:295](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs#L295) 的组件挂载**有** `rate > 0f` 守卫（故 Granary 实际无 ProducerComponent），而 `MapProduceToEco` **无** ⇒ 同一语义两处实现漂移，诊断侧被污染。
- 旁证：`TaskScheduler.cs:1045-1046` 注释已自述「须 producer.kind==Resource，防 outputResource 默认值 0 把无产出建筑误判为产金（口径对齐批A MapProduceToEco）」——**道出了同一个雷，但沿用了同一处缺守卫的写法**。

### ③ 离屏副作用 —— 照 C′ ② 全库改 `kind=None` 会**连带改动离屏 AI 经济**（D643 未预见）

- [AbstractEconomySettlement.cs:197](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/AbstractEconomySettlement.cs#L197)：`if (def == null || def.producer.kind != ProduceKind.Resource) continue;` —— 与诊断侧**同一道 kind 门**。
- [AbstractEconomySettler.cs:143](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/AbstractEconomySettler.cs#L143)：`int capacity = b.ConcurrentCapacity > 0 ? b.ConcurrentCapacity : b.Level;` —— 容量 0 时**回退 Level**（≥1）。
- 合成结论：Granary/Warehouse（`concurrentWorkers: 0` ⇒ capacity 回退 Level）与 wood_pile/stone_pile **当前在离屏（Abstract）经济中真实产出 Food/Wood/Stone**（`FarmDaily/LumberjackDaily` 计算路径 L148-167）。
- ⇒ 若按 C′ ② 全库置 `kind=None`：上述建筑**离屏停产** ⇒ **离屏 AI 经济行为变更**（非纯收口）。属设计变更面，须**先由策划端裁决**「离屏经济是否也应排除仓储/资源点」，并配套确定性双跑 + P1 回归验证；否则触碰「零行为漂移」红线。

## 三、请裁：C′ 实施口径（三选一）

| 口径 | 内容 | 代价/风险 |
|------|------|-----------|
| **A 代码守卫法**（执行端建议） | ① `ProduceKind` 尾追 `None`（纯扩展，旧值 0/1 稳定）② `MapProduceToEco` 增 `def.producer.rate <= 0f → -1`（对齐 BuildingFactory L295 既有语义）③ 资产 `kind` **本轮不改**（②的资产显式声明另立待裁）④ 正例探针「有 Granary 无 Farm 仍建 farm」⑤ 契约 §六 同步 | **零行为漂移**（离屏经济不受影响）＋确定性不变；缺陷（通道A/R-C2 被仓储/非产能掩蔽）**彻底修掉**；代价＝「字段默认值＝有效值」的声明层面陷阱仅靠 rate 守卫兜住，不消除（新资产仍默认 Resource，但 rate=0 已被守卫拦住） |
| **B 严格照 C′**（含资产全量） | A 全部 ＋ 全库非产能建筑显式 `kind: None` | 须**先裁**「离屏经济是否排除仓储/资源点」；落盘后须补确定性双跑 + P1 回归 + 位移声明；**行为变更面**＝Granary/Warehouse/wood_pile/stone_pile 离屏停产（可能缩 AI 经济） |
| **C 先裁后动** | 本报告待裁，暂不实施 | 七考前置附条 C′ 悬空 ⇒ 七考放行受阻（D643：「七考放行需 C′ 落盘」） |

## 四、执行端立场与自检

- **未动代码/资产**：本报告为纯请示，工作区零新增改动（除账本取号行、本报告、索引/工作日志回写）。
- 建议 A 的理由：缺陷的**正确性内核**在「诊断侧把非产能当产能」＝`rate` 语义；`kind` 是**声明侧**。以 rate 守卫在消费点收口＝最小改动、零副作用，且与 `BuildingFactory.cs:295` 既有口径**收敛一致**（消除两处漂移）。② 的资产显式声明若确需，建议**另立待裁**并与其离屏语义一并决议。
- 待策划端另裁的姊妹项：`AbstractEconomySettlement.cs:197` 是否同样补 `rate > 0f` 守卫（＝离屏侧同族缺陷，与本报告 ③ 同源）。
- 教训关联：D643 已立 **L-28**「枚举/字段默认值＝有效值 ⇒ 沉默误判」；本报告为其实例注记（批C 列报2 勘正）＋新证据（同族缺陷存在于**两处**消费点，且资产**都有** producer 段——「无 producer 段」为初判表象偏差）。

## 五、回写与提交

- 取号：账本水位线 `HH.180→HH.181` ＋ HH.181 行（**独立 commit `64bdeaa`**，遵 D640 #10 取号原子化）
- 本报告 ＋ 索引（HH.181 行）＋ 主计划书工作日志插行（**随本串第二笔 commit**）
- 边界：策划端零代码/零资产；训练仓不代提；不 push

---

## 六、策划裁决（主策划端 · **D644**，2026-09-11）

> **先认账**：D643 的根因表述**错了**——我上轮 `^producer:` grep **少了 2 空格缩进** ⇒ 假阴性 ⇒ 误断「granary/warehouse 无 `producer:` 段」。**真实机制＝资产有 `producer:` 段（`kind:0`/`rate:0`）而 `MapProduceToEco` 缺 `rate>0` 守卫**。执行端**判据三直读勘正上级根因** —— **顶级正面样本，记嘉奖**。
> **判据三直读（本裁，带阳性对照：41 资产 / 40 含 `producer:`）＝已过**（①设计稿＝D643／HH.181 全文；②代码实读＝`MapProduceToEco` L492-505／`BuildingFactory.cs:295`／`AbstractEconomySettlement.cs:197`／`AbstractEconomySettler.cs:143`／`TaskScheduler.cs:1045`／`GameEvents.cs:176-190`；③字段直读＝**farm `rate=2`／quarry `rate=5`**（真产能）vs **granary/warehouse/wood_pile/stone_pile/market `rate=0`**、`outputResource` 逐值）。

**判：执行端三条勘正全部成立**（①前提②范围③离屏副作用）。

| 请求 | 裁决 |
|---|---|
| C′ 实施口径 | ✅ **裁 A（收敛版）**＝**只加 rate 守卫**（零漂移）；**否 B**（触离屏行为变更）；**C 不取**（会阻塞七考） |
| 姊妹项（`AbstractEconomySettlement.cs:197` 同补 rate 守卫？） | 🔴 **本轮不擅动**（＝行为变更）⇒ **立 `DZ-089` 挂账 + 独立裁决** |

### 裁 A（C′ 收敛版 · 规格）

1. **核心（必需）**：`MapProduceToEco` L494 加**真产能守卫**，**口径对齐 `BuildingFactory.cs:295`** —— `def == null || def.producer.kind != ProduceKind.Resource || def.producer.rate <= 0f || def.isResourceNode → -1`。⇒ 诊断侧只计**真产能建筑** ⇒ **解 通道A/R-C2 被仓储/非产能掩蔽**；**零行为漂移**（`BuildingFactory` 本就未给 rate=0/`isResourceNode` 建筑挂 `ProducerComponent`，实际产出未变）。
2. **`ProduceKind.None` 尾追＝本轮不做**（无消费者＝死值；真防护已由 ① 提供）；**资产 `kind` 显式声明＝撤回**（否则触离屏停产）。
3. **探针**：**正例**「有 Granary 无 Farm 时 通道A/R-C2 仍选建 farm」；**负例**「farm/quarry 仍计产能；granary/warehouse/wood_pile/stone_pile/**Gold-默认族(~25)** 均**不再计**」。
4. **契约 §六 同步**：`Production` 计数口径＝`rate>0 && kind==Resource && !isResourceNode`。
5. **门禁**：编译 0 警 0 错 ＋ `Smoke_2_23RP0` **22/0**（＋新正/负探针）＋ `Smoke_2_22P0` **32/0 零退化** ＋ **确定性**（诊断块纯函数同 seed 同输出）＋ `AI.Core` git status 空 ＋ 死表零动。
6. ⇒ **A 零漂移 ⇒ C′ 落盘即「2_23 资源 P0」收官 ⇒ P1 七考放行**。

### 姊妹项挂账（`DZ-089`）

- `AbstractEconomySettlement.cs:197` **同缺 `rate>0` 守卫**（与诊断侧同族）⇒ 仓储/资源点**在离屏 Abstract 经济中真实产 Food/Wood/Stone** ⇒ **离屏侧与工厂侧口径漂移**（三处口径：工厂 / 诊断 / 离屏）。
- **独立裁决前置**：先确「**离屏经济是否应排除仓储/资源点**」的**设计稿口径** ＋ 确定性双跑 + P1 回归 + 位移声明；**不在 C′ 内擅动**（触零行为漂移红线）。

**验收三问（钩子2，前置＝判据三直读已过）**：①**发生**＝**策划端**（我）D643 根因表述错——`^producer:` 检索**少缩进** ⇒ 假阴性 ⇒ 误断「无 producer 段」；真机制＝`MapProduceToEco` 缺 rate 守卫（与 `BuildingFactory:295` 漂移）；②**定性＝策划侧失误**（判据三直读② 的**执行质量**不足：检索手段错且**未做阳性对照**）；③**教训核查＝新增 `L-29`「检索假阴性」**（今日**二次实证**：①`Select-String "**/*.cs"` 不递归 ②`^producer:` 少缩进）⇒ 按 §四 **升硬性检查项**；＋ **L-28 实例注记**（真实触发路径＝「有 `producer` 段但 `rate=0` 未设 `kind=None`」，非「无段致默认」）。

**嘉奖**＝执行端**不盲从附条 ＋ 判据三直读勘正上级根因 ＋ 主动报「停手待裁」 ＋ 建议 A（最小改动/零漂移）** —— **顶级正面样本**（反事实：若照抄 C′ ② 全库改 `None` ⇒ 离屏停产＝行为变更、触红线）。

**边界**＝策划端**零代码/零资产动**（仅裁决＋文档回写）；**执行端下串**＝按裁 A 落地 C′（rate 守卫 + 正/负探针 + 契约 §六），**七考放行以其落盘为准**。