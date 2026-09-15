# HH.282 `G2-2` 专属兵·机器 prefab 补全 任务书（生产链兜底 ＋ 10 prefab 落地）

> 类型：施工任务书｜状态：🟢 **已签发·待接单**
> 日期：2026-09-15 · 签发端：主策划端 · **Gate：`G2-2`（建军链全绿）**
> 关联：`3.1_王国AI路线图` **§六 `G2-2`**（🔴 高优先挂账）／台账 **`DZ-097`**（本批主体）／`HH.239`（美术接入·素材已就位）／`D724`·`D725`／`0.6 §二百五十四`
> 取号：HH.282（账本水位线 281→282）
> **由来**＝用户 2026-09-15 放行王国 AI 主线；按 `3.1 §十` 序取 **`G2-2` 建军链** 的**排队首项「专属兵·机器 prefab 补全」**（D708 既定序）。

---

## 〇、一句话目标

把「**扣费已完成却无生成、且无兜底退款**」这条**行为层断链**（`DZ-097`）闭合：**补 10 个缺失 prefab（7 专属兵 ＋ 3 机器）**，并在**生产入口加返回值校验 ＋ 兜底退款**，使任何 prefab 缺失都不再导致「扣费落空」。

---

## 一、作业依据（必读·逐字）

| # | 文档／实物 | 用处 |
|---|---|---|
| 1 | **台账 `DZ-097`**（`河谷防线开发计划书具体内容/缺陷台账.md` L150） | 本批主体：`UnitFactory.cs:65-69`（`prefab==null` 报错返回 null）／7 专属兵＋3 机器 prefab 缺／`SiegeProductionSystem.cs:170-177` 忽略返回值 |
| 2 | `3.1_王国AI路线图` §六 `G2-2`＋§十 | Gate 归属与推进序 |
| 3 | `Valley Rampart/Assets/Resources/UnitPrefabs/`（**现有模板**） | 参考 `Human_Player_{Worker,Warrior,Archer,HeavyWarrior}.prefab` 的结构 |
| 4 | `Valley Rampart/Assets/Resources/UnitData/`（35 个 SO） | `prefab` 字段待填的 10 个 |
| 5 | `Resources/Config/Art/SpriteRefTable.asset` | artId→sprite（**10 个的素材全部在场**·见 §二 表） |

---

## 二、策划端已取证（执行端复核·勿重推翻）

### 2.1 缺口精确清单（**实读 `Resources/UnitData/*.asset`：`prefab: {fileID: 0}` 者恰 10 个**）

| # | SO 文件 | 分类 | 素材 artId（**均在场** ✅） |
|---|---|---|---|
| 1 | `Dwarf_Bedrock.asset` | 专属兵 | `unit_dwarf_bedrock_{idle,walk,attack}`＋`portrait_dwarf_bedrock` |
| 2 | `Dwarf_Musqueteer.asset` | 专属兵 | `unit_dwarf_musqueteer_{idle,walk,attack}`＋portrait |
| 3 | `Elf_DeerRider.asset` | 专属兵 | `unit_elf_deerrider_{idle,walk,run,attack}`＋portrait |
| 4 | `Elf_Ranger.asset` | 专属兵 | `unit_elf_ranger_{idle,walk,attack}`＋portrait |
| 5 | `Elf_Windwalker.asset` | 专属兵 | `unit_elf_windwalker_{idle,walk,attack}`＋portrait |
| 6 | `Orc_Berserker.asset` | 专属兵 | `unit_orc_berserker_{idle,walk,attack}`＋portrait |
| 7 | `Orc_WolfRider.asset` | 专属兵 | `unit_orc_wolfrider_{idle,walk,run,attack}`＋portrait |
| 8 | **`Dwarf_Mortar.asset`** | **机器** | **`machine_dwarf_mortar`**（＋`_strip`） |
| 9 | **`Elf_VineCatapult.asset`** | **机器** | **`machine_elf_vinecatapult`**（＋`_strip`） |
| 10 | **`Orc_Ram.asset`** | **机器** | **`machine_orc_ram`**（＋`_strip`） |

> **⇒ 与 `DZ-097` 的「7 专属兵 ＋ 3 机器」逐数吻合**（机器＝`mortar`/`vinecatapult`/`ram`，其 artId 为 `machine_*` 前缀）。
> **注**：`DZ-097` 立账时或含 `SiegeMachine`/`Ballista` 等**通用机器**，现**已有 prefab**（实读）⇒ **本批范围＝上表 10 个**，不扩围。

### 2.2 断链两处（**实读**）

```csharp
// UnitFactory.cs:65-69 —— 缺 prefab ⇒ 报错 + 返回 null（无兜底）
if (data.prefab == null)
{
    Debug.LogError($"[UnitFactory] UnitData '{data.name}' 未挂 prefab 引用，无法生成。");
    return null;   // 彻底解耦：无命名回退
}

// SiegeProductionSystem.cs:170-177 —— 扣费后忽略返回值，无条件 return true
RulerController.Instance.Spend(cost);
if (UnitFactory.Instance != null)
{
    UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, type, spawnPos);   // ← 返回值被忽略
    Debug.Log($"[SiegeProduction] 生产 {type}（造价 …）");                // ← 报"成功"
    return true;                                                          // ← 无条件 true
}
return false;
```

**⇒ 即使本批把 10 个 prefab 补齐，只要有**任何**路径走到 `prefab==null`（新队种／新机器／数据回滚），**扣费仍会落空** ⇒ **必须同时补兜底**（`DZ-097` 的处理栏原文：「prefab 补齐 **or** 生产入口 null 校验兜底退款」——**本批两者都做**，因为只做一半仍留洞）。

---

## 三、范围（两件）

### 件1｜10 个 prefab 落地 ＋ 挂接

1. **新建 10 个 prefab**，置于 **`Assets/Resources/UnitPrefabs/`**（与现有 4 个模板同目录），**命名与 SO 同名**（如 `Dwarf_Mortar.prefab`）
2. **结构照现有模板**（`Human_Player_Worker.prefab` 等）——`SpriteRenderer`＋`UnitController`（及模板中已有的其他组件），**视觉走 `SpriteRefTable` 的 artId**（素材已在场，**禁自造素材**）
3. **挂接**：把 10 个 `UnitData.prefab` 字段填上（`prefab: {fileID: <新 prefab>, guid: …}`）
4. **3 个机器**（`mortar`/`vinecatapult`/`ram`）须与**通用机器**（`SiegeMachine` 等已就位者）**结构一致**——参考其 prefab，避免机器类特有的组件缺失

### 件2｜生产链兜底（**DZ-097 的第二半·不可省**）

1. **所有**调 `UnitFactory.SpawnUnit(...)` 的**生产/建造入口**，须**校验返回值**：
   - 返回 `null` ⇒ **不报"成功"**、**不吞掉** ⇒ **退款 + 明确错误日志**
2. **至少覆盖**（实读命中的）：
   - `SiegeProductionSystem.cs:170-177`（玩家机器生产）
   - `SiegeProductionSystem.cs` 的 **AI overload**（`:180+`，注释称 AI 走国库扣费 ⇒ 同型风险）
   - **`UnitFactory` 其余调用方须全量 grep 排查**（回执里给清单：每个调用点「是否校验返回值」）
3. **兜底语义**：**"扣费不落空"** —— 生成失败 ⇒ 资源回到原主（玩家走 `RulerController`；**AI 走 per-kingdom 国库**，**注意 AI 与玩家不是同一账户**，勿只退玩家）

---

## 四、验收线

| # | 线 | 判据 |
|---|---|---|
| **1** | **prefab 落地** | 10 个 `.prefab` 资产在场（给路径清单）；`Resources/UnitData/*.asset` 中 **`prefab: {fileID: 0}` 计数 = 0**（全量扫描实证） |
| **2** | **实际可生成（核心）** | **正门 `EnterTestRun`** 后，对**每个**缺少过的单位各生成一次，**`SpawnUnit` 返回非 null**（探针逐单位计数实证）；画面能见到实体（截图，**至少覆盖 1 兵 + 1 机器**） |
| **3** | **兜底生效（核心）** | **负探针**：**临时**把某一个 `UnitData.prefab` 置空（或注入 null）⇒ 走生产入口 ⇒ **资源被退回**（给扣费前后对比读数）＋ **不打印"生产成功"**；**验完须还原** |
| **4** | **AI 侧同验** | AI 机器生产路径同样受兜底保护（退 **per-kingdom 国库**，非玩家账户） |
| **5** | **回归** | 编译 **0 error**；`AI.Core` **零触**；四族四轮冒烟不退化；`EnterTestRun`＋`ExitTestRun`＋三态 ✅ |
| **6** | **数值零改** | `UnitData` 的**数值字段**（`walkSpeed`/`runSpeed`/`maxHp`/`attack`…）**逐字未变**——**本批只填 `prefab`**（给 diff 证据：只应出现 `prefab:` 行变化） |
| **7** | **写后验** | 本批相关路径 `git status` 全空；各笔 hash 已贴；**未 push** |

---

## 五、红线

1. 正门 `EnterTestRun`；收尾真暂停 ＋ `ExitTestRun` ＋ 退 Play（`L-32`）；**不为过线凑数**（`L-30`）。
2. ❌ **只补 prefab ＋ 兜底校验**：**不动 `UnitData` 的任何数值字段**、**不改 `SpriteRefTable`**、**不动 `ArtImportPipeline`**。
3. ❌ **不碰 `GameScene.unity`**（`HH.271`/`HH.275` 的场）；❌ **不碰 `MapGenRules.cs`**（`HH.272` 的场）。
4. ❌ **不碰 `AI.Core`**；若发现必须触碰 ⇒ **停手列报**（`sim-sync` 义务判定）。
5. ❌ **禁自造美术素材**（素材已在场；若发现某个 artId 实际缺图 ⇒ **列报阻塞·不替代**）。
6. **件2 的负探针为临时改动 ⇒ 必须还原**，并在回执声明还原后状态。
7. 写-改-commit 同串 · **具名 `git add`** · 禁 `-A`/`-u`/`.` · **不 push**；改前重读磁盘。
8. `多Agent交接/_任务队列.md` **一行不写**；**王国 AI 专向队列（`3.1 §六`）状态由策划端更新**。
9. 交付报告按账本**实时水位线取号**（**禁预留**·`D640 #10`）。

---

## 六、排雷

| # | 雷 | 处置 |
|---|---|---|
| **M1** | **`UnitFactory.SpawnUnit` 有两个签名** | 实读注意：`SpawnUnit(UnitData, Vector2, int kingdomId)`（`:57`）与 `SiegeProductionSystem` 调用的**另一重载** ⇒ **两处都要核**（返回值语义可能不同：`GameObject` vs `bool`） |
| **M2** | **AI 账户 ≠ 玩家账户** | 兜底退款须**按 kingdomId 回原账户**（AI 走 per-kingdom 国库）；**只退玩家＝半修** |
| **M3** | **对象池可能掩盖缺失** | `UnitFactory` 用 `_instancePool`（`:72`）⇒ **池命中时不会走 `Instantiate`** ⇒ 负探针须**确保池内无该类型**，否则验不出来 |
| **M4** | **prefab 结构照抄可能带错组件** | 3 个**机器**（`mortar`/`vinecatapult`/`ram`）与**步兵**组件需求不同（`ammo`/`crewRequired`/`isStatic` 在 SO 已配）⇒ **照机器类模板（如 `SiegeMachine.prefab`）而非步兵模板** |
| **M5** | **专有 artId 命名差异** | `Elf_DeerRider` 的 artId 是 **`unit_elf_deerrider_*`**（**无下划线·连写**）⇒ 取图时按 artId 精确匹配（`SpriteRefTable` 为准），**勿按文件名猜** |
| **M6** | **可能发现"不止 10 个"缺** | 本批范围＝实读的 10 个；**若施工中发现更多 ⇒ 列报不扩围**（避免批内膨胀） |
| **M7** | **承 `L-29`/`L-30`** | 判定线可达性：本批判据均可实测（计数／返回值／扣费前后），无结构性不可达风险 |

---

## 七、产出

1. **开工回执（占 HH.282 号 · 不另取号）**：含 §六 排雷 **M1~M7 逐条自答** ＋ **`SpawnUnit` 全量调用点 grep 清单**（每个调用点标注「是否校验返回值」）＋ **两个重载的签名与返回值语义实读** ＋ **10 个 prefab 的结构选型说明**（步兵模板 vs 机器模板）＋ `sim-sync` 核查结论。
2. **交付报告（按水位线另取号）**：§四 七条逐项证据（**计数实证／逐单位生成实证／负探针扣费前后／AI 侧同验／数值零改 diff**）＋ `L-34` 五列 ＋ 实机截图。
3. 截图落 `screenshots/HH282_专属兵prefab/`（`.gitignore:60` 已忽略 ⇒ **不入库**）。

---

> 签发：主策划端｜2026-09-15｜**D726**｜依据＝用户放行王国 AI 主线 ＋ `3.1 §十` 序（`G2-2` 排队首项）＋ 台账 `DZ-097` ＋ 策划端实读（10 个空 prefab 清单／素材全覆盖／两处断链代码）。
