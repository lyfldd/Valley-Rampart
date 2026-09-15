# HH.282 `G2-2` 专属兵·机器 prefab 补全 开工回执（生产链兜底 ＋ 10 prefab 落地）

> 类型：开工回执｜状态：🟢 **已接单·可施工**（排雷全自答·无结构性阻塞·无请裁项）
> 日期：2026-09-15 · 执行端 · 依据：HH.282 任务书（D726 签发）· Gate=`G2-2`
> 取号：**HH.282**（占本号 · 与任务书同号 · HH.272 先例 · `D640 #10` 禁预留）
> 交付报告：按水位线另取（预计 HH.283）
> HEAD 基线：`2fdf4fa2`（D726 任务书签发）· 未 push

---

## 〇、一句话结论

**件1（10 prefab 落地＋挂接）＝模板与结构选型已定，可施工**（7 兵走步兵模板 `Human_Player_Warrior` 组件集；3 机器走机器模板 `Human_Player_SiegeMachine`）。**件2（生产链兜底）＝唯一扣费未校验点为 `SiegeProductionSystem` 两个重载，修复方案已定**（玩家退款走 `RulerController.Refund`，AI 走 per-kingdom `KingdomState.Refund`）。**无请裁项、无结构性不可达**（M7：判据全部可实测）。

---

## §一、排雷 M1~M7 逐条自答

| # | 雷 | 自答（实读） |
|---|---|---|
| **M1** | `SpawnUnit` 两个签名 | ✅ **两处都已实读**（见 §二）。`SiegeProductionSystem` 调的是**门面重载** `SpawnUnit(Faction, Occupation, Vector2, kingdomId)`（`:135`），其内部再调数据重载（`:57`）。**两者返回值语义相同＝`GameObject`**（非 bool），`null`＝未生成（数据缺/prefab 缺）；不抛异常、不吞。修复只需在门面调用点判 `null`。 |
| **M2** | AI 账户 ≠ 玩家账户 | ✅ **已实读两个账户**：玩家＝`RulerController.Refund(cost, 1.0f)`（`RulerController.cs:287`，逐项 `ModifyResource(+true)` 退还）；AI＝`KingdomState.Refund(cost, 1.0f)`（`KingdomState.cs:162`，走 `AddResources` 收口台账＋经济诊断入账）。**两处分别退各自账户**，不混。 |
| **M3** | 对象池掩盖缺失 | ✅ **不掩盖**。`SpawnUnit` 在**触池之前**（`UnitFactory.cs:65-69`）对 `data.prefab==null` 提前 `return null`——`_instancePool.Get(data)`（`:72`）在 null 分支之后才执行，池内有无该类型都不影响 null 判定。负探针置空 prefab ⇒ 必返 null，池无影响。**另**：10 个目标单位此前从未成功生成过 ⇒ 池内本无其桶。 |
| **M4** | 机器 vs 步兵组件不同 | ✅ **实读两模板组件集**（`GetComponentsInChildren<Component>(true)` 资产实读）：步兵 `Human_Player_Warrior`＝`Transform/SpriteRenderer/Rigidbody2D/UnitController/BoxCollider2D/DamageFeedback/NPCBrain`（**含 NPCBrain**）；机器 `Human_Player_SiegeMachine`＝同上去 **无 NPCBrain**（SiegeMachine 变体 `m_RemovedComponents` 移除）+ `m_LocalScale=2`。⇒ 3 机器照 `Human_Player_SiegeMachine` 模板、7 兵照 `Human_Player_Warrior` 模板。 |
| **M5** | `Elf_DeerRider` artId 连写 | ✅ **运行时帧键已正确**：`SpriteAnimator.ArtToken(Occupation.DeerRider)="deerrider"`（`SpriteAnimator.cs:437` 连写）⇒ 帧键 `unit_elf_deerrider_{idle,walk,run,attack}` 精确匹配 `SpriteRefTable`（素材实盘在场，`UnitsFrames.spriteatlas` 已含 `unit_elf_deerrider_*`）。**prefab 不需存 artId**——帧集由 `SpriteAnimator` 按 `(raceId, occ)` 运行时从 `SpriteRefTable` 解析（`ResolveSet`），`UnitController.Initialize` 自动补挂 `SpriteAnimator`。 |
| **M6** | 可能不止 10 个缺 | ✅ **已全量扫描**：`UnitData` 35 个 SO 中 `prefab` 为空的恰 10 个（7 专属兵＋3 机器），与任务书 §二.1 逐数吻合。**其余 25 个（含通用机器 `SiegeMachine`/`Ballista`）均已挂接**。施工中若再发现更多缺 ⇒ 列报不扩围。 |
| **M7** | 承 `L-29`/`L-30` | ✅ **判据全部可实测**：①`prefab:{fileID:0}` 计数＝grep/资产枚举实证；②逐单位生成＝正门探针逐单位断言 `SpawnUnit!=null`＋截图；③负探针＝扣费前后读数对比（`L-29` 实读非文本 grep）；④AI 侧同验＝国库读数；⑤回归＝编译 0 error＋四族四轮冒烟；⑥数值零改＝git diff 只出现 `prefab:` 行；⑦写后验＝git status。**无结构性不可达**。 |

---

## §二、`SpawnUnit` 两个重载签名与返回值语义实读

### 重载 A（数据直供）· `UnitFactory.cs:57`

```csharp
public GameObject SpawnUnit(UnitData data, Vector2 position, int kingdomId = 0)
```

- 返回 **`GameObject`**；`null` 两种来源：`data==null`（`:59-63`，LogError）／`data.prefab==null`（`:65-69`，LogError）。
- 非 null 时完成：池取/实例化 → 绑 `UnitController.Initialize(data)` → `NPCBrain.Init`（若 data 为 `NpcProfessionDef`）。
- **读档路径** `SpawnFromSave`（`:182`）经此重载，已判 `go != null` 再 `OverrideSaveId`。

### 重载 B（Faction/Occupation 门面）· `UnitFactory.cs:135`

```csharp
public GameObject SpawnUnit(Faction faction, Occupation occupation, Vector2 position, int kingdomId = 0)
```

- 返回 **`GameObject`**；先 `UnitDataManager.GetData(faction, occupation)`，查表失败 ⇒ data=null ⇒ 内层返回 null ⇒ 透传 null。
- `kingdomId>0` 时生成后覆写 `Faction.AiKingdom`（2_17 步骤10）。
- **`SiegeProductionSystem` 两个重载调用的都是它**（`Faction.PlayerCamp` + type + kingdomId）。

> **M1 结论**：两个重载返回值均为 `GameObject`，`null` 语义一致＝「未生成」。修复点在门面调用点判 `null` 即可，无需改 `UnitFactory` 本体。

---

## §三、`SpawnUnit` 全量调用点清单（grep 全库 · 每个标注「是否校验返回值」）

> 口径：**生产/建造入口**（扣费语义）是本批兜底目标；**读档/自然生成/调试/探针**不入生产链，但逐条列清校验状态。

### A. 生产链（扣费）——本批必须修

| 调用点 | 扣费? | 是否校验返回值 | 处置 |
|---|---|---|---|
| `SiegeProductionSystem.cs:173`（玩家机器） | ✅ `RulerController.Spend` | ❌ **未校验**·无条件 return true·报"成功" | **本批修**：null ⇒ `RulerController.Refund`＋LogError＋return false |
| `SiegeProductionSystem.cs:216`（AI 机器 overload） | ✅ `kingdom.Spend` | ❌ **未校验**·无条件 return true·报"成功" | **本批修**：null ⇒ `kingdom.Refund`＋LogError＋return false |

### B. 读档 / 自然生成 / 调试 / 探针（非扣费）——逐条列明现状

| 调用点 | 是否校验返回值 | 备注 |
|---|---|---|
| `UnitFactory.cs:138`（门面内部调数据重载） | ✅ 透传 null | 门面自身不吞 |
| `UnitFactory.cs:182`（`SpawnFromSave` 读档） | ✅ `go != null` 再 OverrideSaveId | 读档无扣费 |
| `MonsterController.cs:150`（`MonsterSpawner.Spawn`） | ✅ `go==null ⇒ return null` | 怪物生成免费 |
| `KingdomFoundry.cs:113`（AI 首代工人） | ✅ `workerGo != null` | 免费直出 |
| `PopulationSystem.cs:232`（开局实体） | ✅ `go==null ⇒ LogError+return false` | 免费 |
| `PopulationSystem.cs:330`（玩家生育 Child） | ✅ `childGo != null` | 免费 |
| `PopulationSystem.cs:434`（AI 生育 Child） | ✅ `childGo != null` | 免费 |
| `VagrantCampSystem.cs:366`（流民补员） | ✅ `go==null ⇒ return false` | 免费 |
| `VagrantCampSystem.cs:397`（初始流民/自然刷点） | ✅ `go==null ⇒ return false` | 免费 |
| `AIDebugSpawnController.cs:270`（调试生成） | ✅ `go==null ⇒ 失败结果` | 免费调试工具 |
| `TestFixtureApi.cs:191`（测试门面） | ✅ `... != null` | 测试 |
| Editor/Smoke 探针 ~20 处（`Valley_OB12_*`/`Valley2_17_*`/`Valley2_20*`/`Valley2_21A`/`Valley2_16`/`Valley_HH109/107/115/131/159/264`/`Smoke_2_22P0`/`TestFixtureApi`） | ✅ 探针各自判空/断言/清理 | 测试载体·无扣费 |

> **结论**：全库 `UnitFactory.SpawnUnit` 调用点 22 处（生产 2 ＋ 非生产 20）。**唯一扣费未校验＝`SiegeProductionSystem` 两处**，其余非扣费路径均已校验或免费无退款语义。**本批代码改动面＝`SiegeProductionSystem.cs` 1 文件 2 处**。

---

## §四、10 个 prefab 结构选型说明

| 模板 | 组件集（资产实读） | 适用 |
|---|---|---|
| **步兵模板** `Human_Player_Warrior.prefab` | `Transform`＋`SpriteRenderer`＋`Rigidbody2D`＋`UnitController`＋`BoxCollider2D`＋`DamageFeedback`＋`NPCBrain`（变体追加 DamageFeedback 等） | **7 专属兵**：`Dwarf_Bedrock`/`Dwarf_Musqueteer`/`Elf_DeerRider`/`Elf_Ranger`/`Elf_Windwalker`/`Orc_Berserker`/`Orc_WolfRider` |
| **机器模板** `Human_Player_SiegeMachine.prefab` | 同上去 **无 `NPCBrain`**（`m_RemovedComponents` 已移除）＋ `m_LocalScale=2` | **3 机器**：`Dwarf_Mortar`/`Elf_VineCatapult`/`Orc_Ram` |

- **实施方式**：`PrefabUtility.InstantiatePrefab(模板)` → 改名（与 SO 同名）→ `PrefabUtility.SaveAsPrefabAsset("Assets/Resources/UnitPrefabs/{Name}.prefab")`。生成物为模板的变体，与现有 24 个 `Human_Player_*.prefab` 的「基类 Ruler/Warrior → 变体」嵌套约定一致。
- **视觉**：帧集由运行时 `SpriteAnimator` 按 `(raceId, occ)` 从 `SpriteRefTable` 解析（`unit_{race}_{token}_{state}`／机器 `machine_{race}_{type}`），prefab 只保留占位 SpriteRenderer＋**族别主题色 tint**（沿用现有 prefab「带色变体」设计语言；色源＝3.1.2 D457/D458 各族主题色，非自造）：
  - 矮人（Bedrock/Musqueteer/Mortar）：铜橙 `(0.70, 0.40, 0.15)`
  - 精灵（DeerRider/Ranger/Windwalker/VineCatapult）：翠绿 `(0.20, 0.60, 0.30)`
  - 兽人（Berserker/WolfRider/Ram）：暗红 `(0.60, 0.10, 0.10)`
- **挂接**：10 个 `UnitData.prefab` 字段填新 prefab 引用；数值字段零改。

---

## §五、sim-sync 核查结论

**零 `AI.Core` 义务。** 本批改动面：
1. `Assets/Resources/UnitPrefabs/` 新增 10 个 `.prefab`（资产，非决策核）；
2. `Assets/Resources/UnitData/*.asset` 仅填 `prefab` 引用字段（数值零改）；
3. `Assets/_Game/Systems/Kingdom/SiegeProductionSystem.cs`（生产链返回值校验＋退款——**位于 `Systems/Kingdom/`，非 `Systems/AI.Core/`**）。

三处均**不涉及**共享决策核（`AI.Core` ↔ 训练仓 `harness/Core`）／`TuningSnapshot`/`ProfessionSnapshot`/`FactorContext`／champion/factor_registry 三方同步。`SiegeProductionSystem` 为 Unity 侧生产入口，sim 侧无对应文件（训练场景不生产机器单位）。**无新增决策输入、无行为语义变更、无 sim 补实现义务**。

---

## §六、施工方案与验证计划

1. **件1**：bridge `exec_editor_script` 批量建 10 prefab → 挂 10 `UnitData.prefab` → `SaveAssets` → 编译 0 error。
2. **件2**：改 `SiegeProductionSystem.cs` 两处（玩家/AI 各判 null ⇒ 退款原账户＋LogError＋return false）→ 编译 0 error。
3. **验收探针**（Editor/Smoke，正门 `EnterTestRun`）：逐单位 SpawnUnit 断言非 null＋截图（≥1 兵＋1 机器）＋负探针（临时置空 prefab ⇒ 扣费前后对比退款）＋AI 侧同验。
4. **负探针还原**：置空后必须还原，回执声明还原后状态（git diff 回空）。
5. **回归**：编译 0 error / `AI.Core` 零触 / 四族四轮冒烟（`2_20_Smoke_Race`＋`2_20B_Smoke_M7`）/ 收尾三态。

---

## §七、红线复核

- 只补 prefab＋兜底校验：不动 `UnitData` 数值字段／`SpriteRefTable`／`ArtImportPipeline` ✅
- 不碰 `GameScene.unity`（HH.271/275 场）／`MapGenRules.cs`（HH.272 场）／`AI.Core`（必触则停手列报）✅
- 禁自造素材（缺图列报阻塞）✅
- 负探针临时改动必须还原并声明 ✅
- 具名 `git add`／禁 `-A/-u/.`／不 push／改前重读磁盘 ✅
- `_任务队列.md` 一行不写；王国 AI 专向队列 3.1 §六 由策划端更新 ✅

---

> 执行端｜2026-09-15｜HH.282 开工回执·排雷全自答·无请裁项·可施工
