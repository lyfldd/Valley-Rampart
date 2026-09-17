# HH.308 · HH.294 底层重构批「片 6-2 收尾批」交付报告

> **端**：执行端｜**日期**：2026-09-17｜**任务源**：派工提示词（`D779` 验收报告 §九 残余 `R2′`／`S1`~`S5`）＋ `底层执行计划` §五（"验收后删"）
> **口径源**：`最高优先级文档/03_地图即数据库.md`（§6.7／§7／§8）＋ `04_上层接入指南.md`（五条不许）＋ `HH.294_片6-2_验收报告.md`（§三 `R2` 判不成立·§3.5 三条修法）
> **正门证据**：`Valley Rampart/Logs/hh294_slice6_2/`（稳定名 `hh294_slice6_2_probe.txt`＝末次跑（复原跑次）＋ 时间戳副本 ＋ `hh294_slice6_2_hash.txt`）
> **本批证据副本**：`Logs/hh294_cleanup/`：`chain_audit_before.txt`／`chain_audit_after.txt`／`r2p_flip_run.txt`（取反跑次）／`r2p_restore_run.txt`（复原跑次）／`s5_probe_live_run.txt`（S5 片 5 探针重跑）
> **交付物**：① 清场 `A`（commit `5a48d8e9`）② `R2′` 证据重做 `B`（commit `a0f4249c`）③ 本报告。**均未 push**。

---

## 〇、改动清单（两个 commit）

| commit | 内容 | 文件 | numstat |
|---|---|---|---|
| **`5a48d8e9`** | **A · 旧路径清场（`D779` 残余 `S1`）** | **20 文件**（含 2 删除） | **+139 / −354** |
| **`a0f4249c`** | **B · `R2′` 独立渲染对照证据重做** | 1 文件（探针） | **+359 / −105** |

> `git show --numstat` 逐文件值见 §十一。⛔ 并行会话改动（`Valley_HH284_Probe.cs`／`GameScene.unity`／`Packages`／美术 `Ground/*`／`pixel-forge`／`3.6`·`3.8` doc／`HH289`·`HH290` 探针）**一律未碰**（具名 `git add`，未用 `-A`/`-u`/`.`）。

---

## 一、判据 1 —— ⭐ 能力句「`SpawnResourceEntities` 已不存在」＋ 五符号全库零命中（残留仅"已勘正注释"）

**命令**（复现）与**输出片段**：

```
$ git grep -n "TreeGatherSource" -- "Valley Rampart/Assets"
（无输出 ⇒ 0 命中）

$ git grep -n "ReSpawnNaturalBuilding" -- "Valley Rampart/Assets"
（无输出 ⇒ 0 命中）

$ git grep -n "SpawnResourceEntities" -- "Valley Rampart/Assets"
Valley Rampart/Assets/Editor/Smoke/Valley_HH294_Slice6_2Probe.cs:17://    A/B 对照开关（`SpawnResourceEntities`）已删 ⇒ 不再跑对照段（改前读数已落盘 `HH.307` §一，不复须）。

$ git grep -n "FeatureToBuildingType" -- "Valley Rampart/Assets"
Valley Rampart/Assets/Editor/ChainAudit/Validators/R5_SixStage.cs:45:    /// 经「格表 ＋ 门」（`MapGate`）进世界、采集走数据寻址 —— 旧口径「与 `BuildingFactory.FeatureToBuildingType`
（该行后一句即"…同源」已随该函数删除失效"·勘正文本）

$ git grep -n "SpawnEntityFor" -- "Valley Rampart/Assets"
Valley Rampart/Assets/_Game/Systems/World/ResourceRespawnSystem.cs:387:        return true;   // 【HH.294 片 6-2 收尾】旧路径 `SpawnEntityFor`（附加实体重建）已清场删除：落格即纯数据
```

**残留 3 处（全部＝"已删"勘正注释·逐处列出）**：
1. `Valley_HH294_Slice6_2Probe.cs:17`（探针头注："已删 ⇒ 不再跑对照段"）
2. `R5_SixStage.cs:45`（审计口径注："已随该函数删除失效"）
3. `ResourceRespawnSystem.cs:387`（返回行注："已清场删除"）

**类文件在盘性**：`TreeGatherSource.cs` 与 `.cs.meta` 均已删（`Test-Path = False`；git 记录两文件 `delete mode 100644`）。
**`DeriveNaturalBuildings` 命中＝4 处（应为定义＋调用点）**：`MapGenRules.cs:1511`（定义·单行清空）＋ `WorldManager.cs:202`（生产调用）＋ `HH272:56`／`HH291:70`（探针调用）——⛔ 未整删（`B 案`：函数名保留为契约槽位）。

**反射在场性（编译后第二证据·MCP `execute_code`）**：
`TreeGatherSource=False ｜ hasSpawnResourceEntitiesField=False ｜ hasSpawnEntityFor=False ｜ hasDerive=True ｜ hasConfirm=True ｜ isCompiling=False ｜ isPlaying=False`

---

## 二、判据 2 —— ⭐ 生成一张图 ⇒ 三型 `Building` 实例数 = 0（＋ Registry 总数对照上批不变）

**读数（复原跑次·`EnterTestRun` seed 29418·Medium/diff2）**：

```
§A BuildingRegistry 总数 = **21**（清场后目标态；对照上批 `HH.307` 目标态读数 **21** ⇒ 须不变）
§A 三型实例数：OreVein=0 StonePile=0 WoodPile=0 ⇒ 合计 = **0**（目标态应 = 0） ✅
§A 对照类目：mine 建筑（按 sourceType）= 3（按 def.id）= 3（AI 预置/T6 转型面·不在本批）
```

⇒ **21 ＝ 21（逐值不变）**；三型 **0**。对照上批 A/B 对照段（ON ⇒ 1854／三型 1833）＝**改前读数已在 `HH.307` 落盘，本批不再复测**（清场后无 A/B 段；回退面＝commit 历史 `5a48d8e9^`）。

---

## 三、判据 3 —— ⭐ 功能不回归：四型采集各 1 次（命令 → 派工 → 完成 ⇒ 采后读数）

| 型 | 格 | 发布命令（`PrioritizeHarvestCommand`） | 工人状态 | 进入采集 |
|---|---|---|---|---|
| `Tree` | (179,5) | True | **MovingToSource** | ✅ |
| `OreVein` | (92,5) | True | **MovingToSource** | ✅ |
| `StonePile` | (95,5) | True | **MovingToSource** | ✅ |
| `WoodPile` | (14,5) | True | **MovingToSource** | ✅ |

```
§C 消费者在场：EventBus.HasSubscribers<PrioritizeHarvestCommand>() = **True** ✅
§D 格(93,6) feature：改前=OreVein ⇒ 改后=**Plain** ✅（翻 Plain）
§D 池子读数（区块 ci=5 类=OreVein）：改前=6 ⇒ 改后=**5**（Δ=-1·须 −1）
§D 幂等（第二次 HandleCellGathered）：池子=5（无二次扣减 ✅）
§D 守卫面：部署后区域=1 ⇒ 采集后=0（Δ=-1） GuardRegionLostEvent=1 次；再直调 ⇒ 区域=0 事件累计=1 ⇒ 幂等成立
```

**（附）占格面零变化**：三型资源格抽样 20 ⇒ `GetOccupant != null` = **0**；可走 5/5 true；`House` 压 `OreVein`(4,9) ⇒ `ok=False reason=Blocked`（承接成立）。

---

## 四、判据 4 —— ⭐ `R5_SixStage` 口径同步原文 ＋ ChainAudit 改前/改后对照（⛔ 无新增 🔴；🟡 逐条定性）

**改后原文（`R5_SixStage.cs:44-60` 摘）**：

```csharp
    /// <summary>⭐【HH.294 片 6-2 收尾·口径同步（`D779` 残余 `S1`）】**格表资源点（非实体出口）**：
    /// 经「格表 ＋ 门」（`MapGate`）进世界、采集走数据寻址 —— 旧口径「与 `BuildingFactory.FeatureToBuildingType`
    /// 同源」已随该函数删除失效（实体派生路径已清场·`naturalBuildings` 恒空）。
    /// 纳入 <see cref="Row.HasAny"/> ⇒ 如实记为「**有出口（非 Building 路径）**」。
    ///  不含 `Mine`（锚点·T6 转型面 ⇒ 单列 <see cref="GridAnchorTypes"/>）。</summary>
    private static readonly HashSet<BuildingType> GridTableResourceTypes = new HashSet<BuildingType>
    { BuildingType.Tree, BuildingType.OreVein, BuildingType.WoodPile, BuildingType.StonePile };
```

（`Row` 新增 `GridTable`／`GridAnchor` 两列；`HasAny` 计入 `GridTable`、**不计** `GridAnchor`——锚点是世界特征非建筑生成路径；`Mine` 走 `AiPreset` 实锚＝`KingdomDef.baseBuildingDefIds` 含 `mine`。）

**矩阵改前/改后（该 5 行）**：

| def | 改前 | 改后 |
|---|---|---|
| `mine` | 玩家菜单✗ **地图自然✓** AI预置✓ … | 玩家菜单✗ **格表资源 格表锚点✓** AI预置✓ … （格表锚点·T6 转型面） |
| `tree`／`ore_vein`／`stone_pile`／`wood_pile` | 玩家菜单✗ **地图自然✓** AI预置✗ … | 玩家菜单✗ **格表资源✓ 格表锚点✗** AI预置 … |

**违例面改前/改后（ChainAudit 静态层·`Logs/hh294_cleanup/chain_audit_before.txt` ↔ `..._after.txt`）**：

```
改前：-- R5 违例 = 6 条 --   分级：🔴=1  🟡=20  豁免=0
改后：-- R5 违例 = 6 条 --   分级：🔴=1  =20  豁免=0
```

- ⛔ **无新增 🔴**（唯一 🔴＝`GameEvents.cs 订阅 BuildingDestroyedEvent ×1／退订 ×0`，**改前即存在**·与本批无关）。
- 🟡 **无新增**：`farmland`／`rift`／`ruins`（声明态无入口）＋ `portal`／`treasure_box`（独立管线实体）＝5 条逐条定性**与本批无关**（均为既有声明态）；差异面＝矩阵文本（`地图自然✗` → `格表资源✗ 格表锚点✗`）逐行可 diff。

**⚠️ 随附（请策划端处置·不代写）**：`生命周期登记表.md` §0／§1 的 `tree`／`mine`／`ore_vein`／`stone_pile`／`wood_pile` 行**生成侧锚点已过时**（`BuildingFactory.InstantiateFromMap L96-166`／`FeatureToBuildingType L63`／`ReSpawnNaturalBuilding L175` 均已删；`tree` 行 `TreeGatherSource` 亦已删）。

---

## 五、判据 5 —— ⭐ `R2′` 三件套（身份可区分／背景对照／覆盖）＋ 两列读数表

**① 身份可区分（改后原文摘·`Valley_HH294_Slice6_2Probe.cs`）**：

```csharp
        string nA = ua.name, nB = ub.name;
        ua.name = nA + "_A"; ub.name = nB + "_B";            // ① 身份可区分
        ...
            if (ReferenceEquals(h.source, instA)) hitA++;      // 两列按**实例比对**（⛔ 禁按 name 比同类同精灵）
            else if (ReferenceEquals(h.source, instB)) hitB++;
            else { hitThird++; ... }
```

日志逐例打印：`**拾取返回者**=Human_Player_Worker_B（∈{B}）（A=0 B=6 第三=0 空=0）`／`**第三方**(Building_farm_70_171/Building)`（第三方首跑实录·见 §十二-2）。

**② 对照点有效性（背景对照·改后原文）**：

```csharp
            a.enabled = false; b.enabled = false; var cAB = SampleAtWorld(cam, pts[i], 256);
            a.enabled = true; b.enabled = true;
            bool chA = ColorChanged(c0, cA), chB = ColorChanged(c0, cB);
            bool underA = ColorChanged(cA, cAB);   // 遮 A 后与"遮双"不同 ⇒ A 之下有内容贡献（背景对照）
            bool underB = ColorChanged(cB, cAB);
            bool validA = chA && underA, validB = chB && underB;
```

⇒ 判据＝「`chA ∧ hideA≠hideBoth` ⇒ A 遮挡 B」；无效点**跳过**（旧版 `chA^chB` 已废）。

**③ 覆盖 ＋ 两列读数表（复原跑次·`DepthMajorYInFront=False`）**：

| 例 | 类型 | 实体对（实例） | 候选点 | **有效点** | 拾取列（`PickAt`·实例比对） | 像素列（**外部观测**·RT 四点采样） | 一致 |
|---|---|---|---|---|---|---|---|
| ① | **同类**（两单位·Pivot·不同 sprite） | `Human_Player_Worker_B` vs `Human_Player_Vagrant_A` | 25 | **6** | `Worker_B`（∈{B}·6/6） | `Worker_B` | **✅** |
| ② | **异类**（单位 × 建筑·Pivot vs Center） | `Human_Player_Worker_A` vs `Building_castle_130_142_B` | 25 | **13** | `Worker_A`（∈{A}·13/13） | `Worker_A` | **✅** |
| ③ | **跨层带**（order 5 vs 1·受控构造） | `Human_Player_Worker_A`(临时 order 5) vs `Building_castle_130_142_B` | 25 | **13** | `Worker_A`（∈{A}·13/13） | `Worker_A` | **✅** |

```
§G  总判定（三件套）：用例①（同类·两单位）=✅ ｜ 用例②（异类·单位×建筑）=✅ ｜ 用例③（跨层带）=✅
   ⇒ **✅ 判据成立（两列一致·有效点 ≥3）**
```

- **两列非同源**：像素列＝RT 真实渲染像素（外部观测·不含任何拾取键/常量）；拾取列＝`MapGate.PickAt`。
- **每例有效点 ≥3**：6／13／13（ 非"首点即定论"：逐点读数全量落盘于 `r2p_restore_run.txt` §G）。
- **抗污染（探针侧·如实列报）**：第三方点过滤（除 A/B 外的建筑/单位/宝箱 bounds 覆盖 ⇒ 弃点；实锚＝`farm`(F-15 footprint 重叠) 曾整例压过 `castle`）＋ 像素列多数票（≥2:1）。

---

## 六、判据 6 —— ⭐ `R2′` 鉴别力自证（两次跑·各给读数与日志片段）

**跑次 1（临时取反 `MapGate.DepthMajorYInFront`＝true）** —— 日志 `r2p_flip_run.txt`：

```
# 跑次：2026-09-17 20:30:48｜MapGate.DepthMajorYInFront=True
§G 用例① ... 有效点 5 点：**拾取返回者**=Human_Player_Vagrant_A（∈{A}） ｜ **像素实测最前者**=Human_Player_Worker_B ｜ 一致=❌
§G 用例② ... 有效点 14 点：**拾取返回者**=Building_castle_130_142_B（∈{B}） ｜ **像素实测最前者**=Human_Player_Worker_A ｜ 一致=❌
§G 用例③ ... 有效点 14 点：**拾取返回者**=Human_Player_Worker_A（∈{A}） ｜ **像素实测最前者**=Human_Player_Worker_A ｜ 一致=✅
§G  总判定（三件套）：用例①=❌ ｜ 用例②=❌ ｜ 用例③=✅ ⇒ **❌ 判据不成立**
```

⇒ ⭐ **判据变 ❌**（①② 按要求翻转；③＝层带规则·不受深度键符号影响·保持 ✅）——**鉴别力成立**。

**跑次 2（复原 `DepthMajorYInFront`＝false）** —— 日志 `r2p_restore_run.txt`：

```
# 跑次：2026-09-17 20:33:19｜MapGate.DepthMajorYInFront=False
§G 用例①=✅ ｜ 用例②=✅ ｜ 用例③=✅ ⇒ **✅ 判据成立（两列一致·有效点 ≥3）**
```

⇒ ⭐ **✅ 复现**（与 §五 表同源读数一致）。

**复原在场性**：`git diff --stat -- "…/MapGate.cs"` **无输出（零 diff）** ⇒ 该文件与 `HEAD` 逐字节一致；`execute_code` 复核 `constFlip=False`。

---

## 七、判据 7 —— 探针连带（逐条处置与改后原文）

| # | 项 | 处置 | 改后原文/读数 |
|---|---|---|---|
| 1 | `Slice6_2Probe` 对照段 | **删**（5 处 `SpawnResourceEntities` 引用全清；单段回归） | RunFromMenu 单段头：`【目标态全量·收尾清场后单段】`；`Run()` 无 `isSecond` 分支；§E-对照／§B-对照段整删 |
| 2 | `HH272_MapGenProbe:54-55` | 注释勘正（删开关引用） | `// 【HH.294 片 6-2 收尾 同步】清场后（实体派生路径与对照开关已删）⇒ naturalBuildings 恒空（资源点＝格表）…` |
| 3 | `HH291_MapGenProbe:67-69` | 注释勘正 | `// …本探针的 nb 读数只涉 Mine（HH.293 B1 起恒 0）与 SameMap 逐项比对（两侧同为空 ⇒ 仍一致）⇒ 判定语义不变。` |
| 4 | `HH291:MineDeriveRead`（`:208`） | **标作废**（判据已废·`ok` 恒 False 属预期） | `⚠️ 本段判据已作废（D737 · HH.293 B1 回退 ＋ HH.294 片 6-2 收尾清场）… nb 恒 0（ok 恒 False 属预期·非缺陷）`；判定行加注 `MineDeriveOk={mineOk}(作废项·恒预期 False)` |
| 5 | `HH291:SpawnMineDistanceRead`（M6·`:225`） | ⭐ **改读口**（旧口 `naturalBuildings` 恒空 ⇒ 读数退化） | `Mine 簇角 = 左/上邻非 Mine 的 Mine 格（2×2 簇重建·语义与原读口一致）`；`for (int y = 0; y < m.height; y++) for (int x = 0; x < m.width; x++) { … if (!left && !up) anchors.Add(…) }` |
| 6 | `HH291:SameMap`（`:262`） | 保留（**不静默留绿**·如实注记恒等） | `// 【HH.294 片 6-2 收尾】naturalBuildings 恒空 ⇒ 本段比对恒等（**不失真**：主域 features/climateZones/spawns 逐格/逐个比对仍是确定性判据主体）` |
| 7 | `HH291` 实机 A6①（`:372-376`） | 加注 | `地图 Mine nb={nbMine}（恒 0）（【片 6-2 收尾】naturalBuildings 恒空 ⇒ nb 恒 0·Mine 锚点现由 features 承载，见 M6 读口）` |
| 8 | `Smoke_2_23RB:161-167`（P6 玩家源） | **改走 `WorldGatherSource.ForCell(…, 0, …)`**（保 P6「玩家源」语义） | `var tg = WorldGatherSource.ForCell(new GridCoord(10, 10), FeatureType.StonePile, new Vector2(1f, 1f), 0, RespawnConfig.Instance, 5);`＋日志文案 `（WorldGatherSource, kingdomId=0）` |

---

## 八、判据 8 —— 调试面（`AIDebugSpawnController` 改后原文 ＋ 如何验证新采集链）

```csharp
        // ④ 木头堆（【HH.294 片 6-2 收尾】改走**格表落点**：旧 `PlaceBuilding("Buildings/wood_pile")`
        //    在实体退役后会放出**无采集链的孤立建筑** ⇒ 改为 `MapGate.PlaceResourceNode` 落 WoodPile 格）；
        //    采集链＝右键该格（`PrioritizeHarvestCommand` → `ResourceRespawnSystem.ConfirmResourceGather`
        //    → `WorldGatherSource`）→ 工人到点采集 → 完成格翻 Plain ＋ 池子 −1（数据寻址·验证新采集链）。
        {
            var wp = center + new Vector2(6f * cs, 0f);
            var wpCell = GridSystem.Instance != null ? GridSystem.Instance.WorldToCoord(wp) : null;
            bool placed = wpCell.HasValue && MapGate.PlaceResourceNode(wpCell.Value, FeatureType.WoodPile);
            if (!placed) Debug.LogWarning("[AIDebugSpawn] 木头堆格表落点失败（越界/非空格）。");
            else Debug.Log("[AIDebugSpawn] 木头堆资源格已落 (" + wpCell.Value.x + "," + wpCell.Value.y + ")——右键该格验证采集链。");
        }
```

**该场景如何验证新采集链**：`SpawnLifecycleScenario` 落格后，右键该木头堆格 ⇒ 发 `PrioritizeHarvestCommand` ⇒ `ResourceRespawnSystem.OnPrioritizeHarvest` → `ConfirmResourceGather` 立案（`WorldGatherSource`）⇒ 3 工人中 1 人被派 `MovingToSource` ⇒ 采完入背包 → 搬运到仓库 → 格翻 `Plain` ＋ 池子 −1（同 §三 探针同一条链·探针已逐项实测）。

---

## 九、判据 9 —— 陈文本勘正（`Slice6PickProbe:593-596`·**断言未改**声明）

```csharp
        Log("§F 候选上限：建筑 ≤(2r+1)²（r=MapGate.PickCellRadius **动态**·按最大 sprite 半高重推——【片 6-2 收尾勘正】"
            + "本批实测=7 ⇒ 15×15 footprint 反查·实数 289 上界·旧文本『PickCellRadius=1 ⇒ 3×3』系 R1 前历史值"
            + "，⛔ 本行只勘文本、断言未改）；实测命中点 建筑候选=" + candB
            + " 单位(窗口内)=" + candU + " 宝箱=" + candC + "｜ 单位索引窗口上限=窗口内单位数（同 r 窗口）");
```

 **仅改文本**：该文件本批 `+4/−2`（全部为文本行），未动任何断言/逻辑（片 6-1 已闭项不碰）。

---

## 十、判据 10 —— 常规

| 项 | 读数 |
|---|---|
| 编译 | **0 error / 0 新增 warning**（`refresh_unity(compile=request)` ＋ `read_console`：warning 25 条**全为存量**（IUIPanel／CameraSetup／`Valley2_17_Smoke_5` 等，与 `HH.307` 同集）；error 面除 MCP 桥一条既有噪声外 0） |
| 同 seed 逐格一致 | ⭐ **四连跑同 hash `9424A5D99D9C3543`**（取反×2 ＋ 复原×2；`features`＋`climateZones` FNV 双混合·seed=29418）⇒ 逐格一致 ✅ |
| 无每帧全图扫 | `WanderStimulusProvider` OreVein 格表来源＝**静态 12s 缓存**：首扫 **0.261 ms**（全图 features 扫→438 候选）／缓存内第二扫 **0.027 ms**；落格刷新走既有分帧分摊（S5 重跑：最坏单帧 **0.77 ms**·4.6% 帧预算）⇒ 本批未新增每帧全图扫 |
| `AI.Core` 命中 | **0**（`git grep "SpawnResourceEntities\|TreeGatherSource" -- …/AI.Core` 无输出；两 commit 全在 `Assets/_Game/**`／`Assets/Editor/**`） |
| 改动文件行尾 | 全 **LF**（20＋1 文件逐一 python 复核 `CRLF=0`）；**`BuildingFactory.cs` 保持 CRLF 373 行**（改前 445 ⇒ 净 −72·裸 LF=0·python 二进制按行替换·未用 Edit/Write） |
| 正门三态 | 探针三跑均 `EnterTestRun` → 真暂停 `Time.timeScale=0` ＋ `Save(hh294_slice6_2)` ＋ `ExitTestRun` ＋ 退 Play（实测 `isPlaying=False`）；**S5 片 5 探针重跑**同正门：`SetState(Playing)+ExitTestRun`（探针内）＋ 本端补 `TS=0`＋`Save(hh294s5_live)`＋退 Play（`isPlaying=False` 实测） |

**S5（片 5 探针全量判据重跑）读数摘**（`s5_probe_live_run.txt`·与 `D775` 验收同口径）：开局均值 **37.7**（±20% 窗 **252/256**）／第 7 天 **97.4%**（93 ÷ 95.5）／单类全带全类 ≤ `capKind_i`／重生 **8/8 不在原位**／节流窗事件=结算=6（非每帧/每秒）／最坏单帧 **0.77 ms** ⇒ **片 5 判据重跑全过**。

---

## 十一、判据 11 —— ⭐ 两个 commit 的 hash 与逐文件 `+N/−M`（`git show --numstat` 复算）

**`5a48d8e9`（A·清场·20 文件·+139/−354）**：

```
2/2   Valley Rampart/Assets/Editor/ChainAudit/Validators/R3_SupplyChain.cs
26/11 Valley Rampart/Assets/Editor/ChainAudit/Validators/R5_SixStage.cs
3/3   Valley Rampart/Assets/Editor/Smoke/Smoke_2_23RB.cs
2/2   Valley Rampart/Assets/Editor/Smoke/Valley_HH272_MapGenProbe.cs
26/12 Valley Rampart/Assets/Editor/Smoke/Valley_HH291_MapGenProbe.cs
1/1   Valley Rampart/Assets/Editor/Smoke/Valley_HH294_Slice5Probe.cs
4/2   Valley Rampart/Assets/Editor/Smoke/Valley_HH294_Slice6PickProbe.cs
21/79 Valley Rampart/Assets/Editor/Smoke/Valley_HH294_Slice6_2Probe.cs
13/3  Valley Rampart/Assets/_Game/Systems/AI/AIDebugSpawnController.cs
9/14  Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs
2/2   Valley Rampart/Assets/_Game/Systems/Building/Building.cs
5/77  Valley Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs
3/3   Valley Rampart/Assets/_Game/Systems/Building/MineByproductComponent.cs
1/1   Valley Rampart/Assets/_Game/Systems/Kingdom/BlacksmithBuilding.cs
1/1   Valley Rampart/Assets/_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs
8/42  Valley Rampart/Assets/_Game/Systems/World/MapGenRules.cs
7/19  Valley Rampart/Assets/_Game/Systems/World/ResourceRespawnSystem.cs
0/64  Valley Rampart/Assets/_Game/Systems/World/TreeGatherSource.cs（删除）
0/11  Valley Rampart/Assets/_Game/Systems/World/TreeGatherSource.cs.meta（删除）
5/5   Valley Rampart/Assets/_Game/Systems/World/WorldGatherSource.cs
```

**`a0f4249c`（B·`R2′`·1 文件·+359/−105）**：`Valley Rampart/Assets/Editor/Smoke/Valley_HH294_Slice6_2Probe.cs`

---

## 十二、偏离与自决项（**须验收裁断·如实列出**）

| # | 项 | 说明 |
|---|---|---|
| 1 | ⭐ **用例③ 构造变更（宝箱 → 受控 order）** | 原 `R2` 用例②「宝箱 order=5 vs 建筑 order=1」在本批**三连跑实测有效点 ≤1**（宝箱 sprite 绘制内容只占其 AABB 极小比例·9×9 细网格亦命不中）⇒ 改为**受控构造**：测试单位 `sortingOrder` **临时置 5**（测毕复原）、建筑保持 order=1。语义仍是"层带大者前"（鉴别力自证期③稳定 ✅ 即其证据）。⚠️ 若裁「必须用真实层级差异对象」⇒ 需另寻 order>1 的资产（现库仅宝箱·且其像素覆盖不可用） |
| 2 | **像素列判定采用「多数票 ≥2:1」** | 边缘像素可有个别相反票（实测 13:1）⇒ 以多数票定"像素最前者"，非 100% 单侧。⛔ 若裁须严格单侧 ⇒ 需再收紧采样点或剔除边缘点 |
| 3 | **用例② 采用候选 1（AI 城堡 `130,142`）** | 候选 0（玩家城堡 `68,169`）**过滤后净点 0**（被 `farm` 覆盖·F-15 footprint 重叠实锤）⇒ 机制内建的"换候选"生效。属**如实降级**而非偏离；F-15 仍待修 |
| 4 | **探针侧强化三件（第三方过滤／相机临时对准／RT 临时放大）** | 均为**探针侧**手段（同帧复原；⛔ 不动生产实现）：第三方点过滤（`PointClearOfThirdParties`）／建筑原地不可移形改「相机对准」／正交相机 `orthographicSize×0.35` 临时放大（同帧复原） |
| 5 | **清单外文本勘正 2 处** | `BuildingFactory` 两条日志文案（「跳过自然建筑实例化」→「跳过地图预置建筑实例化」；「（自然建筑 + 主城）」→「（主城·自然建筑自 HH.294 片 6-2 收尾起不再派生）」）——零行为·属同一文件勘正 |
| 6 | **`map.naturalBuildings` 字段＋`InstantiateFromMap` 主城段保留** | 按 A7 保留项执行（契约槽位／主城链路）；`WorldManager.cs:215` 日志恒打「自然建筑=0」（属实·按裁不动） |

---

## 十三、残余与未完成项（**显式列出·不隐瞒**）

| # | 项 | 状态 |
|---|---|---|
| 1 | 片 6-1 探针（`Slice6PickProbe`）**未重跑**（仅文本勘正） | 不在本批判据内 ⇒ 未跑；如需全量回归请指示 |
| 2 | `O3`（`PrioritizeHarvestCommand.Workers` 未消费） | 留 `05` 交互层（承上批） |
| 3 | `F-15`（9 座 footprint 重叠） | 本批**再次实锤**（farm 压 castle ⇒ 用例②候选 0 净点 0）·待修 |
| 4 | `F-14`（`grade` 进格表） | 片 6-3（未动） |
| 5 | 探针 §C 会留下 1~4 个采集源/若干格被采 | 探针槽不入正式局（承上批残余 9）；测毕销毁测试工人 |
| 6 | 用例③ 有效点中含 1 票反向（多数票处理） | 已列 §十二-2；逐点读数为实（可复算） |
| 7 | `登记表 §0/§1` 生成侧锚点过时 | 请策划端处置（§四 末·本端不代写） |

---

## 十四、应登记项（`D767` 纪律：不代写策划端账本，仅声明）

1. `_编号登记.md`：HH 水位线 **HH.307 → HH.308（本报告）**
2. `_任务队列.md`：片 6-2 收尾批行 ⇒ 交付待验收（`HH.308`）
3. `_当前快照.md`：`D779` 残余 `R2′`／`S1`~`S5` ⇒ **已闭**（`5a48d8e9`／`a0f4249c`）＋ 本批读数
4. 台账新增节：`R2′` 三件套读数 ＋ 鉴别力自证两跑 ＋ `R5` 口径同步 ＋ S5 重跑
5. 教训库候选：`L-30` 家族 —— **对照类判据第三件套落地实证**（身份/背景/覆盖）＋ 新实锚两条：①**同类同精灵像素法结构性退化**（同图同色 ⇒ 有效点恒 0 ⇒ 必须异 sprite）②**"对方在该像素无贡献"必须用 hideBoth 排除**（本批 `farm`/宝箱两处实锚）
6. 契约侧（请策划端落）：`03` §8.7 判据 6 证据规范补「像素隐藏法·四点采样（含 hideBoth 背景对照）」＋「同类同 sprite 不可用」排除条；`03` §6.2/§7 补「资源格不占格 ⇒ 放置阻挡改由格表承担」（承上批建议·本批已实锚）

---

## 十五、commit

- `5a48d8e9`（A·清场·20 文件）／`a0f4249c`（B·`R2′`·1 文件）——均**未 push**。
- 分置两 commit 以便单侧回退：清场回退面＝`5a48d8e9^`；探针回退面＝`a0f4249c^`（清场不受影响）。
- 证据文件（`Logs/` 域·不入 commit）：`Valley Rampart/Logs/hh294_slice6_2/*`（稳定＋时间戳）／`Logs/hh294_cleanup/*`（ChainAudit 前后＋两跑次＋S5）／`Valley Rampart/Logs/hh294_s5_probe_live.log`。