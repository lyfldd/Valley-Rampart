# HH.294 片 6-1「拾取坐标化」验收报告

> **端**：策划端（主策划·砚）｜**日期**：2026-09-17｜**裁决号**：`D776`
> **交付**：`HH.305`（施工 commit `0b745c8b`·**9 文件 `+1231/−37`**，`git show --numstat` 逐值吻合 ✅）
> **口径源**：`03` §八/§8.6/§8.7 判据 6·7｜`04` §六｜`底层执行计划` §二 批 5-B/§五回退
> **证据原件**（我逐份读过）：`Valley Rampart/Logs/hh294_slice6/hh294_slice6_probe_20260917_163127.txt`（第 1 段全量）＋ `..._163728.txt`（第 2 段）
> **纪律**：本报告全部读数由策划端**独立复读**（`git show --numstat`／`git diff` 原文／探针日志原件／自跑枚举脚本），**未采信任何自述汇总**。

---

## 一、结论

**🟢 销号成立（`D776`）** —— 判据 1~11 全部独立复核通过，**无功能回归**。

**残余 2 条必改 ＋ 3 条观察 ＋ 1 条微瑕，随片 6-2 搭车**（均**不阻断**销号）：

| 号 | 内容 | 定性 | 处置 |
|---|---|---|---|
| **`R1`** | 拾取候选窗口半径 1 格（3×3）**小于建筑 sprite 溢出**（量化见 §三） | 能力句边界未完全达成（**⛔ 非回归**：改前 Collider 仅 `size=1×1`，比新窗口更小） | **必改·搭车片 6-2** |
| **`R2`** | 判据 2 的「渲染最前者」列**与 `PickAt` 同源同常量重算** ⇒ 自证、**无鉴别力** | 证据缺陷（代码无问题） | **必改·搭车片 6-2**（只需补探针） |
| `R3` | 壳 prefab 引用的源 guid `16d758dd…` 在仓库内**核不出定义** | 观察 | 要一条 `AssetDatabase.GetAssetPath` 证据 |
| `R4` | GC `Gen0=1` vs 空窗 `0` 来源未定位 | 观察（已诚实列出） | 保持观察 |
| `R5` | 同深度键平局（tie）按遍历序 | 观察 | 保持观察（其已列边界） |
| `R6` | 报告 §三.3「反面预期」句仍复引**已被本批推翻**的旧措辞 | 微瑕（`L-35` 同族） | 改措辞，不阻断 |

---

## 二、逐条判据独立复读

| # | 判据 | 我独立读得 | 判 |
|---|---|---|---|
| 1 | G0 符号实测 | ⭐ 探针 §G0-A **两组交叉纯色用例 + 重叠中心像素读数**（组1 红+0.5/蓝−0.5 ⇒ 全蓝；组2 红−0.5/蓝+0.5 ⇒ 全红）⇒ **小 y 在前**，与直觉**相反**；符号位 `MapGate.cs:408 DepthMajorYInFront=false` ＋ `:397-407` 注释逐字写入 | ✅ 独立证据 |
| 1b | 两类排序点实读 | `§G0-B`：建筑 `sortPoint=Center`（`BuildingVisual.cs:148` 只设 sortingOrder）／单位 `sortPoint=Pivot`（`UnitController.cs:316`）；键按各自实际取（`:561`） | ✅（与我 2-1 预告的待核不一致吻合，已按实盘处理） |
| 2 | 拾取＝渲染最前 | ⚠️ 见 `R2`：两列**同源**（`Valley_HH294_Slice6PickProbe.cs:282-287` 用同一 `MapGate.DepthMajorYInFront` ＋同一键重算）⇒ **只能证实现与常量一致**，不能证与真实渲染一致 | ⚠️ 证据不足 |
| 3 | 宝箱可点 ＋ 单位可点 | 日志 `§D`：宝箱真无 Collider ⇒ 改道后 `PickAt` 命中=`True`、`IInteractable` 可达=`True`（改前点不了）；单位 `无Collider=0`／命中 **19**、被更前者遮挡 **18**、未命中 **2**（19+18+2=39 ✅ 自洽） | ✅ |
| 4 | 生产码不再物理拾取 | 我自跑 `git grep 'Physics2D\.' -- Assets/_Game`：**改前 6 处/2 文件 ⇒ 改后 2 处/1 文件**（`MapGate.cs:494/510`，**全在双轨分支内**）＋ 2 行注释；上层（`InteractionManager`／`SelectionController`）**0** | ✅ 默认路径零物理 |
| 5 | 建筑 Collider = 0 | 日志 `§E`：**1850 座 ⇒ 带 Collider 0**；抽样反证 **0/20**；玩家建造路径 `TryBuild(def=castle)` 建成 ⇒ 1851 座、Collider=0。**我另加两道**：`AddComponent<BoxCollider2D|CircleCollider2D|Collider2D>` 生产码 **0 残留**；`GetComponent<Collider2D>` 类**读取**生产码 **0**；`OnTrigger*/OnCollision*` **0** ⇒ 退役**无消费者**，安全性成立 | ✅ 强于报告 |
| 6 | 代价／候选上限／GC | 日志 `§F`：单次 **0.0038 ms**（命中）／**0.0020 ms**（空白）＝≈0.06% 帧预算；候选建筑 **≤9**（`PickCellRadius=1`·代码常量）；`GetTotalMemory` 差 **0 B**；GC 增量 1 vs 空窗 0 已诚实标注未定位 | ✅（`R1` 另见 §三） |
| 7 | 框选对照 | 日志 `§G`：新（单位索引）**0.0025 ms** vs 旧（`OverlapAreaAll`）**0.0057 ms** ⇒ **−56%**；1 差已归因（旧路径收不进无 Collider 物）；⛔ 未走 `QueryCells` 六项复合查询 —— ⇒ **`FillUnitsInRect` 首个真实消费者**（片 4 判据「0 调用点·备而未用」就此兑现） | ✅ |
| 8 | 掩码退役 | 我自跑 grep：全库 `interactableMask|selectableMask` **仅 2 行退役注释**（`InteractionManager.cs:10`／`SelectionController.cs:20`），零消费者 | ✅ |
| 9 | 派发优先级未变 | `git diff` 逐字核：命中源由物理改 `MapGate.PickWorld`，`IInteractable`（`:94`）→ `IClickInteractable`（`:116-117`）→ 关面板（`:122`）**三段逐字保留**；框选双条件过滤（`kingdomId==0`／流浪放行）逐字保留 | ✅ |
| 10 | 常规 | 同 seed **两段两次独立建局 hash 同为 `C445B25A73022B44`**（日志原件）；行尾我复测：`BuildingFactory.cs` **CRLF 451 行保持**（CRLF 文件走 python 二进制 ✅）、其余全 LF 无 BOM；探针跑通 ⇒ 编译通过（等效证据） | ✅ |
| 11 | 回退预案 | `MapGate.cs:547 UseLegacyPhysicsPick=false` 在场；旧路径**全部收容在门内单文件**；⭐ **诚实声明回退边界**：因 6-C 已删挂载，开关 true 只回退拾取、旧路径对建筑/宝箱失效 ⇒ 完全回退须连两块挂载一并 revert（git 单 commit revert 即完整） | ✅ 处置正确 |

**`04` 五条不许**：拾取实现**落在门内**（`MapGate.PickAt`／`PickWorld`／`FillUnitsInWorldRect`），上层只调门；内部换算全走 `GridSystem` 口（`WorldToCoord`／`WorldToSubCoord`／`SubDiv`／`FillUnitsInRect`），⛔ 无第二套换算 ✅。

---

## 三、必改残余

### `R1` · 候选窗口半径 1 格 **小于** 建筑 sprite 溢出（能力句边界）

**事实（我实读＋量化）**：`MapGate.cs:421 PickCellRadius = 1` ⇒ 候选窗 ＝ 3×3 格。地块尺寸 `GridConfig.asset:15 cellSize {1.28, 0.64}` ＋ `GridView` 半格步长 0.32 ⇒ **窗口世界尺寸 ＝ 3.84 × 1.92**。

对比日志 `§G0-B` 实测 sprite 尺寸：

| 建筑 | sprite (w,h) | 折算格覆盖 | footprint | 溢出窗口？ |
|---|---|---|---|---|
| `castle` | (4.00, **4.38**) | 3.1 格宽 × **6.8 格高** | 3×3 | ⚠️ 是（窗口高仅 1.92） |
| `mine` | (2.54, **2.48**) | 2.0 × **3.9** | 1×1 | ⚠️ 临界 |
| `farm` | (2.74, 2.12) | 2.1 × 3.3 | 1×1 | ⚠️ 临界 |
| `House` | (2.24, 2.17) | 1.8 × 3.4 | 1×1 | ⚠️ 临界 |
| `Well` | (0.84, 0.84) | 0.7 × 1.3 | 1×1 | ✅ |

⇒ 点建筑 sprite 的**可见上半部**，其世界点map回格号会落到 footprint 外侧 **2~3.4 格**，超出 ±1 窗口 ⇒ 落不进候选集 ⇒ **「看到的」但点不到**。
（代码注释 `:452` 自称「sprite 溢出 footprint **半格**仍可点」—— **低估**：实为半格~3.4 格。）

**⛔ 但这不是回归** —— 我核了改前覆盖：`BuildingFactory` 挂的是 `col.size = Vector2.one`（**1×1 局部**，与 footprint 无关），且真实美术路径 `Building.cs:581 transform.localScale = Vector3.one` ⇒ 改前可点区 ≈ **1×1 世界单位**（且轴对齐、非菱形）。⇒ **新窗口（footprint ±1 格）严格大于改前**。故定性为「能力句边界未完全达成」而非缺陷引入。

**处置**：片 6-2 搭车，**按实际 sprite 半高推窗口半径**（建议取 `ceil(maxSpriteHalfHeight / (0.5*cellSize.y))` 或等价常量，并把上限写进接口旁成本特征）；判据＝**能力句 ＋ 存在性反证**（构造：点 `castle` sprite 上沿 ⇒ 命中该 castle）。

### `R2` · 判据 2 的证据**自证**（无鉴别力）

**事实**：`Valley_HH294_Slice6PickProbe.cs:282-287` 的「渲染最前者」列 ＝ 用 `sr.sortingOrder` ＋ `sr.bounds.center.y`／`transform.position.y` ＋ 常量 `MapGate.DepthMajorYInFront` **显式计算** ⇒ 与 `PickAt` 内部（`MapGate.cs:555-573`）**同键、同常量**。
⇒ **符号若相反，两列仍会一致 ✅** —— 该判据**在结构上无法发现符号错误**（本批 `D775` 刚立的 `L-30` 新维度「反证须有鉴别力」的**同族第 2 次**）。

**真正独立的证据只有 `§G0-A`**（真实渲染 + `ReadPixels` 像素读数），且它只覆盖：**同 `spriteSortPoint`（默认 Center）／同层带（1）／纯色 2×2 精灵**。
⇒ **未覆盖**：① `Pivot`（单位）键 ② 跨层带（宝箱 5 ＞ 建筑 1）③ 真实实体（多 sprite 嵌套 / `GetComponentInChildren` 取到的 SR）。

**处置**：片 6-2 搭车补**独立渲染对照**（建议：对**真实实体**取重叠点做 RT 像素采样，各 ≥1 例覆盖 Pivot 与跨层带）。**本项为证据补全，非改码。**

---

## 四、观察项与微瑕（记录，不阻断）

| 号 | 内容 |
|---|---|
| `R3` | **壳 prefab 的源 guid 在场性核不出**：我扫 `Valley Rampart/` 全树 **10,299 个 `.meta`**（**9,133 个 hex32 ＋ 1,166 个 base64-56** 两种形态并存）＋ 对 base64 形态**逐条解码比对全串**，`16d758dd…` **零命中**；`Library/PackageCache`（8,834 meta）／全局 Tuanjie 缓存（9,127 meta）／`Medieval Kingdom` 亦零命中；`Library/SourceAssetDB`（16 MB）二进制未命中。⇒ **仅凭磁盘无法判定源 prefab 位置**。⚠️ 但**运行期证据（39/39 带 `BoxCollider2D`）优先于我的静态核验**，故接受勘正；仅要求补一条 `AssetDatabase.GetAssetPath(...)` 一次性坐实 |
| `R4` | GC `Gen0 增量 1` vs 空窗 `0` 来源未定位 —— 其已诚实标注「非每次分配形态（4000 次仅 1 次、`GetTotalMemory` 差 0 B）」，我认可该表述（**未过度声称**） |
| `R5` | 同深度键平局按遍历序；Unity 对同投影值亦按实例序 ⇒ 其已列为已知边界，保持观察 |
| `R6` | 报告 §三.3「反面预期值核对」句仍写「资产面 36 prefab 仅 Ruler/Monster 带 Collider」——该措辞**已被本批自己推翻**，宜改为「改前物理路径下无 Collider 者不可点」（`L-35`：判据须写口径来源与排除项；防后人复引作废文本） |

---

## 五、执行端两项勘正的裁决

### 勘正 1（⭐ 勘正我方 `2-4`）⇒ **接受，我错了**

- 我读得：`Resources/UnitPrefabs/*.prefab` **34/36 是 `PrefabInstance` 壳**（我实读 `Human_Player_Vagrant.prefab` 全文 125 行：`--- !u!1001 PrefabInstance:` ＋ `m_SourcePrefab: {guid: 16d758dd…}`，**不含组件 YAML**）；壳内 `m_Modifications` 改的正是 `fileID 3164712263951564209` 的 `m_Size/m_Offset`（Collider 属性）⇒ 与"源带 Collider"自洽。
- ⇒ 我上批断言「36 个中仅 2 个带 Collider2D ⇒ 其余 34 个点不到」**错误**：**对 YAML 壳文件做文本 grep 判不出组件构成**。**能力句修正照准**：真实被修缺口 ＝ **宝箱**（真无 Collider）＋ **拾取与渲染同序**（改前物理查询序 ≠ 渲染序）。
- ⚠️ 仅其**解释**（"真身在包内 guid 引用的包内 prefab"）我核不出（见 `R3`）⇒ **接受结论、不采信该解释**。

### 勘正 2（同局二次 `EnterTestRun` 杀探针协程）⇒ **接受**

`TeardownManager.TeardownScene()` 销毁探针 Host ⇒ 探针改两段式、hash 即时落盘跨段对比。**这是可复用的探针作者避坑项**，已入教训库。

---

## 六、本端自查（我方缺陷 ＋ 方法学发现）

| # | 内容 |
|---|---|
| 缺陷 1 | **`2-4` 断言错**（§五.勘正 1）：方法错——**对 YAML 壳文件做文本 grep 判组件构成**。根因与 `L-39` **同族但方向相反**：`L-39` 是"零命中别判未实现"，本条是"**命中数别当组件面**" |
| 缺陷 2 | **我另一处判断也错过**：先把「Y 序」判为「全库无实现」（只查了 `sortingOrder`，**没查 `ProjectSettings/GraphicsSettings.asset`**）。⇒ 反证：把「符号禁凭记忆/禁推断」写进提示词**逼出了 G0 实测**，结果**推翻直觉（小 y 在前）** —— 纪律兑现的正面样本 |
| 方法学发现 | ⭐ **本仓库 `.meta` 的 guid 有 9,133 个 hex32 ＋ 1,166 个 base64-56 两种形态并存** ⇒ **单形态（hex）grep 核 guid 在场性会漏**；对"标识符在场性"类核验，须**覆盖两种编码形态并解码比对**（`L-40` 家族再扩一维） |

---

## 七、契约侧回填（`03`）

已按 `HH.305` §十四-6 的请求落定（**策划端落，执行端不碰最高优先级文档**）：
- `03` §8.6 补：**`PickAt` 已落地**（`HH.305`·`MapGate.cs:440`）＋ **符号结论＝小 y 在前**（G0 实测·`GraphicsSettings.asset:42-43`）＋ **候选窗口 `PickCellRadius=1`（⚠️ 大 sprite 溢出未覆盖·`R1` 待修）**。
- `03` §8.7 判据 6 补：**证据要求**——「拾取同源」的验证**须含真实渲染的独立读数**（同位比较像素/排序点），**禁以同源重算自证**（`R2` 立）。

---

## 八、落账

- 本报告（`多Agent交接/策划端/HH.294_片6-1_验收报告.md`）
- 台账 **§四十七**（`D776`）
- `_编号登记.md`（`D776`／HH 水位线 → **HH.305**）
- `_任务队列.md`（片 6-1 销号 ＋ 片 6-2 待派）
- `_当前快照.md`（水位线／在途／§七）
- `多Agent交接/策划端/_策划教训库.md`（`L-39`／`L-30`／`L-40` 各加维度）
- ⚠️ `0.6_审查决策记录.md` **未写**（自 `D739` 停更·按**实况口径**）
