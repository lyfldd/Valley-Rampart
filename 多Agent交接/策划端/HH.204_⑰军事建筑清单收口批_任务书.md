# HH.204 任务书：⑰ 军事建筑清单收口批（三军事向专属营 need 口径 + 死码删除）

> 签发：策划端 2026-09-11（**D657**）｜ 执行端：TraeCode ｜ 性质：**代码/资产收口批**（限 `UtilityActionConfig.asset` ＋ `Systems/AI/KingdomBrain/KingdomBrain.cs`）
> 依据：**D657 裁②④**（HH.202 §四 实证：三军事向专属营 run2/run3 **命中 0**）· 设计稿 `2_22` **B3**（⑰ 军事建筑清单含军事向专属；「选型=种族＋快照缺口」）/L106 · 缺陷台账 **DZ-103**
> 教训引用（钩子1）：**L-28**（枚举尾插/asset need 直读）／L-29（阳性对照）／L-31（双向咬合）／（R10）完成报告号禁预留
> 完成报告：执行端按账本实时水位线取号（**D640 #10 禁预留**）
> 顺序：**本批（验收成立）→ HH.203 七考重验**（串行；本批为七考"净状态"口径的收口前置）

## 一、修什么（D657 裁②④）

1. **三军事向专属营 need 改真实缺口**：`UtilityActionConfig.asset` 的 **`WarCamp`（id19）／`ArcheryRange`（id21）／`WarAcademy`（id18）** 三个行动的 `need: 17`（=`ExclusiveGap`）→ **`need: 22`（=`MilitaryBuildingGap`）**。
   - 与 id23/24（Barracks/TrainingCamp）同口径——`MilitaryBuildingGap` case 已含「**该建筑在场 ⇒ 0**」守卫，族门禁仍在 `Feasible`（`BuildingDef.raceId`）⇒ **零/极小代码**。
   - **`LeyForge`（id20，经济向）永久保持 `ExclusiveGap`，禁动**（§3.7 边界注：熔炉不配 F1/不属军事清单）。
2. **死码 `ExecuteRecruitWarrior`（`KingdomBrain.cs:854`）全量删除**（D656 挂账结转）：删前 **grep 确认零调用点**（现况：全库仅定义处 1 命中、零引用）；连同其墓碑注释块一并移除。
3. **asset need 字段直读证据**（L-28）：修后附 `WarCamp/ArcheryRange/WarAcademy` 三行 `need` 实读值；枚举序 `…ExclusiveGap=17 … MachineDemand=21 → MilitaryBuildingGap=22` **中间位未重排**。

## 二、禁（红线）

- **禁参数微调找补**（D563③）——本批是**口径收口**（占位 0.5 → 真实缺口），不是调 K/权重。
- **AI.Core 零直改**（本批在王国脑效用层，与 AI.Core 不同程序集——同 D656 核查）。
- **禁动 `LeyForge`**；**禁改 `MilitaryBuildingGap` case 本身语义**（只换 asset 引用）。
- 确定性/玩家侧零改动；写-改-commit 同串、只提本串、不 push、**收工退 Play**（L-32）。

## 三、必带证据（验收句）

1. **落地实证**：同 seed 短局（正门 `EnterTestRun`＋15x，槽如 `p1_diag3`）证 **三军事向专属营（按本国族匹配的那一个）落地 > 0**（资产直读计数 + 建造焦点落地行）；**答 DZ-103**。
2. **未过建/正向对照**：对应本国无该建筑时 `need>0` 且 census 能被选中（`top=BuildWarCamp/BuildArcheryRange/BuildWarAcademy` 之一 ≥1）。
3. **死码删除证据**：`ExecuteRecruitWarrior` grep **零命中**（含定义处）；编译 0 错。
4. **asset 直读**：三行 `need` 实读值（=22）＋枚举未重排（见 §一.3）。
5. 既有冒烟零退化（`Smoke_2_22P0` 32/0＋`Smoke_2_23RP0` 27/0）；`git status Assets` 仅本批文件。

## 四、批序

**开工回执 → 停手待策划确认 → 实施 → 交付报告（含§三证据）→ 策划端验收**；验收成立 ⇒ **HH.203 七考重验**起跑。

---
*签发：策划端 2026-09-11（**D657**）。0.6 §一百八十六 已落档；DZ-103 收口。*
