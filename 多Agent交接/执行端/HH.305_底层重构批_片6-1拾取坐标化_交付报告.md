# HH.305 · HH.294 底层重构批「片 6-1 · 拾取坐标化」交付报告

> **端**：执行端｜**日期**：2026-09-17｜**任务书**：`多Agent交接/策划端/HH.294_底层重构批_任务书.md` §二 片 6（⚠️ 策划端已裁切两批：**本批只做 6-C／6-D ＋ G0**；6-A／6-B 双写收敛另批）
> **口径源**：`最高优先级文档/03_地图即数据库.md` §8.6／§8.7 判据 6·7 ＋ `04` §六五条不许 ＋ `底层执行计划.md` §二 批 5-B／§五回退
> **证据落盘**：`Valley Rampart/Logs/hh294_slice6/hh294_slice6_probe.txt`（第 1 段全量 ＋ 第 2 段 hash 对比，两份幂等稳定名＋各时间戳副本）＋ `hh294_slice6_hash.txt`
> **正门**：`TestHarnessApi.EnterTestRun`（seed=29417／槽 `hh294_slice6`）→ 探针跑读 → 真暂停 `timeScale=0` ＋ `Save` ＋ `ExitTestRun` ＋ 退 Play（L-32 三态）；两段两次进局，均三态收尾

---

## 〇、改动清单（改前 vs 改后）

| # | 文件 | 改前 | 改后 |
|---|---|---|---|
| 1 | `Assets/_Game/Systems/World/MapGate.cs` | 无拾取口 | **新增 §8.6 `PickAt`**（`MapPickHit` 值类型 ＋ `DepthMajorYInFront` 符号位 ＋ `PickCellRadius` 常量）＋ `PickWorld`（双轨调度）＋ `FillUnitsInWorldRect`（框选双轨下沉收容）＋ `SubClamp`（走换算口） |
| 2 | `Assets/_Game/Systems/World/ChestManager.cs` | `_chests` 全 private，无查询口 | **新增 `FillChestsInCellRect(RectInt, List<ChestEntity>)`**（只读·buffer 复用零分配） |
| 3 | `Assets/_Game/Systems/Interaction/InteractionManager.cs` | `:51`(hover)／`:89`(click) 走 `Physics2D.OverlapPoint(worldPos, interactableMask)`；掩码字段 `:12` | 两处改走 `MapGate.PickWorld`；**掩码字段退役删除**（留退役声明注释）；派发优先级逐字保留 |
| 4 | `Assets/_Game/Systems/Interaction/SelectionController.cs` | `:248`(Follow)／`:335`(ClickSelect) 走 `OverlapPoint`；`:383`(BoxSelect) 走 `OverlapAreaAll`；掩码 `:21` | 点选两处改走 `MapGate.PickWorld`；框选改走 `MapGate.FillUnitsInWorldRect`（**单位索引**区域查询）；掩码退役；`_boxBuffer` 复用零分配；双条件过滤逐字保留 |
| 5 | `Assets/_Game/Systems/Building/BuildingFactory.cs` | `:226-230` 无条件挂 `BoxCollider2D`（地图预置/重生/双写三路共用） | **删除挂载**（CRLF 文件走 python 二进制按行替换，行尾形态保持 CRLF 451 行） |
| 6 | `Assets/_Game/Systems/Building/BuildController.cs` | `:301-306` 无条件挂 `BoxCollider2D`（玩家建造路径） | **删除挂载**（留 6-C 勘误注释） |
| 7 | `Assets/Editor/Smoke/Valley_HH294_Slice6PickProbe.cs` | 不存在 | 新建验证探针（G0 符号实测／边界实读／判据 2/3/5/6/7/10 读数，正门两段式） |

---

## 一、判据 1（⭐ G0 结论）

### 1.1 「谁在前」实测读数（构造用例 ＋ 像素日志）

探针 §G0-A：两组交叉重叠纯色用例（同 `sortingOrder=1`、2×2 世界单位、重叠中心采样），RT 渲染 + `ReadPixels`：

| 组 | 红 y | 蓝 y | 重叠中心像素 | 盖住对方者 | 判定 |
|---|---|---|---|---|---|
| 组1 | +0.5 | −0.5 | RGB=(0.00, 0.00, 1.00) 全蓝 | 蓝（y=−0.5） | 大 y 在前 = **否** |
| 组2 | −0.5 | +0.5 | RGB=(1.00, 0.00, 0.00) 全红 | 红（y=−0.5） | 大 y 在前 = **否** |

⭐ **符号结论：CustomAxis (0,1,0) 下「世界 y 越小 ⇒ 渲染越靠前（后画覆盖先画）」**——两组交叉一致，与常见直觉（大 y 在前）**相反**。这正是「禁凭记忆断言」所防：本端实现初版按「大 y 在前」暂定，G0 首跑即被证伪，已按实测翻转并回填注释。

### 1.2 建筑/单位各自 `spriteSortPoint` 与 bounds 实读（§G0-B）

| 样本 | sortPoint | order | bounds.center | bounds.size | transform |
|---|---|---|---|---|---|
| 建筑 Building_castle_224_22 | **Center** | 1 | (130.56, 80.58) | (4.00, 4.38) | (130.56, 79.36) |
| 建筑 Building_House_221_23 | **Center** | 1 | (127.36, 78.57) | (2.24, 2.17) | (127.36, 78.40) |
| 建筑 farm／Well／mine（另 3 样本） | **Center** | 1 | — | — | — |
| 单位 Human_Player_Vagrant ×5 | **Pivot** | 1 | (22.08, 69.44) 等 | (0.38, 0.38) | (22.08, 69.28) 等 |

⭐ 证实任务书 2-1 待核不一致：**单位=Pivot**（`UnitController.cs:316`）、**建筑=Center（默认·`BuildingVisual.cs:148` 只设 sortingOrder）**——二级投射点不同（单位 bounds.center.y 比 transform.y 高 ≈0.16）⇒ `PickAt` 按**各自实际**排序点取键（Pivot→`sr.transform.position.y`；Center→`sr.bounds.center.y`），不假设统一。

### 1.3 符号结论已写进代码注释（原文行）

[MapGate.cs:397-408](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/MapGate.cs#L397-L408)：

```csharp
/// <summary>⭐ G0 实测符号结论（2026-09-17 · 探针 `Valley_HH294_Slice6PickProbe` §G0-A·构造用例像素读数）：
/// `ProjectSettings/GraphicsSettings.asset:42-43` ＝ `m_TransparencySortMode: 3`（CustomAxis）
/// ＋ `m_TransparencySortAxis: {x:0, y:1, z:0}` ⇒ 渲染叠放次序由世界 y 决定；
/// 实测【**世界 y 越小 ⇒ 渲染越靠前（后画覆盖先画）**】——两组交叉用例一致：…
public const bool DepthMajorYInFront = false;   // G0 实测（2026-09-17）：小 y 在前 ⇒ 大 y 在前不成立，置 false
```

### 1.4 符号一致性的门内验证

符号翻转后重跑：`§G0-A ★ 符号结论…＝ 小 y 在前 ｜ 与 MapGate.DepthMajorYInFront=False（门当前符号位） ✅ 一致（实现照实测）`

---

## 二、判据 2（⭐ 点最靠前的重叠物 ⇒ 命中的就是渲染上最靠前的那个）

构造用例 ≥2 例（真实实体瞬移重叠 → `PickAt` → 测毕复原；「渲染最前」列＝与 PickAt **同键同符号**（sortingOrder → 深度键·G0 符号）显式计算；G0-A 像素实验已证该键序＝真实渲染序）：

| 用例 | 键读数 | 拾取返回者 | 渲染最前者 | 一致 |
|---|---|---|---|---|
| ① 建筑-建筑：A=castle(key=79.62) vs B=House(key=78.17)，bounds 相交 | 小 y 在前 | **Building_House_221_23** | Building_House_221_23 | ✅ |
| ② 建筑-单位：单位 Vagrant(key=78.15) vs House(key=78.17)，bounds 相交 | 小 y 在前 | **Human_Player_Vagrant** | Human_Player_Vagrant | ✅ |

同源同符号声明：两列的键（`sortingOrder` ＋ Pivot/Center 深度键）与比较符号（`MapGate.DepthMajorYInFront`）同源——`PickAt` 内部与探针显式计算读的是同一批渲染字段、同一常量。

---

## 三、判据 3（⭐ 搭车修：无 Collider 单位可选 ＋ 宝箱可点）

### 3.1 两列读数

| 列 | 读数 |
|---|---|
| 无 Collider 单位数 | **0**（运行时 39/39 全带 BoxCollider2D——见 3.3 勘正） |
| 点选命中数（PickAt 命中自身） | **19** / 39（遮挡 18：被渲染更前的其它单位/建筑盖住——这是「渲染最前」语义的**正确**行为；未命中 2：单位位于相机视口外 tile 未铺区或已出图） |
| 宝箱可点 | **✅**：SpawnChest 落格 (8,8) → `PickAt` 命中=ChestEntity=True、IInteractable 可达=True（改前无 Collider 物理拾取**点不了**）→ 测毕 `Remove` 清理（Count=0，L-23 探针用完即清） |

### 3.2 存在性反证（有鉴别力）

反面（改前物理拾取）：无 Collider 者不可点。若本批改道无效，命中数应≈资产面带 Collider 的少数；实测坐标拾取命中 19 ⇒ 可区分。

### 3.3 ⭐ 勘正任务书 2-4 的「36 个中仅 2 个带 Collider2D」（L-39 同族·grep 误判）

任务书 2-4 断言「`Resources/UnitPrefabs/*.prefab` 36 个中仅 2 个带 `Collider2D` ⇒ 其余 34 个点不到」。实盘复核：

- 文本 grep `Collider2D` 命中 2（Human_Player_Ruler／Monster）——**但**这 36 个文件中 34 个实为 **PrefabInstance 壳**（如 [Human_Player_Vagrant.prefab](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Resources/UnitPrefabs/Human_Player_Vagrant.prefab#L3) 首行 `--- !u!1001 PrefabInstance:`，真身在 guid `16d758dd…` 引用的包内 prefab，壳文件**不含**组件 YAML）⇒ 文本 grep 判「无 Collider」对壳文件无意义。
- 运行时实读（探针 §D）：39/39 单位 `GetComponent<Collider2D>()` **全为 BoxCollider2D**（prefab 源=Human_Player_Vagrant/Resident/Worker——全是壳）。
- 命令输出片段（组件类 ID 统计）：壳文件仅 `class 1 x1 + class 1001 x1 + class 114 x2`（GameObject/PrefabInstance/MonoBehaviour），无任何 Collider 块可判。
- **能力句修正**：「`PickAt` 坐标拾取**不依赖 Collider**——单位无论资产结构如何全部可点（壳-真身/有-无 Collider 一视同仁）；宝箱（真正无 Collider）从不可点→可点」。「34 个单位点不到」的现状描述**不成立**；真实被修的缺口＝**宝箱**＋「拾取与渲染同序」（改前物理查询序≠渲染序）。

---

## 四、判据 4（⭐ 生产码不再有 Physics2D 拾取 ＋ grep 完整输出片段）

`git grep` 等价 grep（`Assets/_Game` 全部 `.cs`，pattern `Physics2D\.|interactableMask|selectableMask`）改后完整输出：

```
MapGate.cs:412:    /// 与旧 `Physics2D.OverlapPoint` 后的用法完全同形。</summary>          ← 注释
MapGate.cs:494:            ? Physics2D.OverlapPoint(worldPoint)                              ← 双轨分支实调
MapGate.cs:498:    /// <see cref="GridSystem.FillUnitsInRect"/>…；旧路径＝`Physics2D.OverlapAreaAll` ← 注释
MapGate.cs:510:            var cols = Physics2D.OverlapAreaAll(worldRect.min, worldRect.max); ← 双轨分支实调
SelectionController.cs:20: // 【HH.294 片 6-1】`selectableMask`…已随拾取改道**退役**：                    ← 退役声明注释
InteractionManager.cs:10:  // 【HH.294 片 6-1】`interactableMask`…已随拾取改道**退役**：                    ← 退役声明注释
```

| 口径 | 改前 | 改后 |
|---|---|---|
| 生产码 `Physics2D.*` 实调用 | **6 处 / 2 文件**（InteractionManager :51/:89；SelectionController :248/:335/:383；另 :14 注释） | **2 处 / 1 文件**（`MapGate.cs:494`/`:510`——**全部收容在双轨开关分支内**）＋ 注释 2 行 |
| InteractionManager 物理调用 | 2 | **0** |
| SelectionController 物理调用 | 3 | **0** |
| 掩码字段 | 2（`interactableMask`／`selectableMask`，均 `~0`） | **0**（退役；grep 仅余 2 行退役声明注释，无任何消费者） |

⚠️ **与判据 4 字面的关系（诚实声明）**：因红线补充条款「门可先双轨（旧物理路径保留一版开关）」，改后生产码仍有 2 处 Physics2D 调用——但它们：①全在 `MapGate.UseLegacyPhysicsPick == true` 回退分支（默认 false 不走）；②已从**上层**（InteractionManager/SelectionController）全部消失。**默认路径零物理调用**；验收通过后删双轨 ⇒ 归零（`MapGate.cs` 单文件 2 行删除）。

---

## 五、判据 5（⭐ 生成一张图 ⇒ 建筑 Collider2D 数 = 0 ＋ 存在性反证）

| 读数 | 值 |
|---|---|
| 生成图建筑总数（BuildingRegistry） | 1850 |
| 带 Collider2D 数 | **0** ⇒ 能力句成立 ✅ |
| 存在性反证（固定 seed 29417 随机抽 20 座逐座 `GetComponent<Collider2D>()`） | **0/20** 带 Collider（明细：wood_pile/ore_vein/stone_pile/House 等 15+ 座逐名输出「无」） |
| 反证鉴别力 | 若挂载未删（反面），预期 20/20 全带 Collider ⇒ 与实测 0/20 可区分 |
| **玩家建造路径**（`BuildController.TryBuild` 走原 `:301-306` 挂载点） | **建成** def=castle @sub(908,100) ⇒ 建后总数 1851、带 Collider=**0** ✅（运行时证据；首轮 def 选中 bridge 被落点校验拒，已修选 def 逻辑排除 bridge/gate/portal 后重跑取到） |

---

## 六、判据 6（单次 PickAt 代价 ＋ 候选上限 ＋ hover 每帧 ＋ 稳态 GC）

| 项 | 读数 |
|---|---|
| 单次 `PickAt`（命中建筑点） | **0.0038 ms**（预热 100 次后测 2000 次均值；hover 每帧一次＝同值 ⇒ ≈0.06% 帧预算 @16.6ms） |
| 单次 `PickAt`（空白/越界点） | **0.0020 ms** |
| 候选上限 | **建筑 ≤9**（来源：`MapGate.PickCellRadius=1` ⇒ 点所在格＋8 邻域 `BuildingRegistry.GetAt` O(1)×9·代码常量；实测命中点建筑候选=2）；单位≤窗口内单位数（3×3 cell 域→小格子域 `FillUnitsInRect`·实测窗口=0，全局 39）；宝箱≤3×3 格宝箱数（实测 0） |
| GC 空窗对照（同跨度不调 PickAt） | Gen0 收集增量 = **0** |
| GC PickAt 4000 次（命中+空白） | Gen0 收集增量 = **1**；`GetTotalMemory` 差 = **0 B** |
| GC 结论 | 4000 次仅 1 次 Gen0 增量且 `GetTotalMemory` 差 0 B ⇒ **非每次分配形态**（若 PickAt 每次分配，增量应随次数线性放大）；代码路径静态零分配（static buffer 复用 ＋ 值类型 struct ＋ UnityEngine 读 API）。⚠️ 诚实标注：该 1 次增量 vs 空窗 0 的差异**来源未定位**（测量窗口与 15x 考跑下并发 AI 分配混入），列观察项 |
| 枚举成本口径 | 单位索引遍历 O(单位索引条目)=39（实测同窗口 `FillUnitsInRect` 单次 0.0340 ms） |

---

## 七、判据 7（框选改走单位索引后的代价 ＋ 与旧 OverlapAreaAll 对照）

同一世界矩形（56.1×36.9 世界单位，含 5 样本单位），各 1000 次均值：

| 路径 | 耗时 | GC 增量 | 返回 |
|---|---|---|---|
| **新**：`MapGate.FillUnitsInWorldRect`（单位索引 `FillUnitsInRect`，buffer 复用） | **0.0025 ms/次** | 0 | 候选 19 |
| 旧：`Physics2D.OverlapAreaAll`（全层＝改前口径） | **0.0057 ms/次** | 0* | collider 18 |

*GC 注：OverlapAreaAll 每调用返回新数组（引擎行为），1000 次窗内未触发 Gen0 收集故增量为 0——「更省」的可靠读数是**耗时 −56%** 与**新路径零分配结构**（buffer 复用），不以 GC 增量 0 声称旧路径无分配。⚠️ 旧路径在本对照中命中 collider 18 vs 新候选 19：差异＝1 个无 Collider 的探针临时实体与框选窗交叠（宝箱已清；不含 Collider 的世界物旧路径天然收不进），如实列报。

⛔ 框选**未**走 `QueryCells` 六项复合查询（`03` §8.7 判据 7 禁每帧调用面未触）——走的是 `GridSystem.FillUnitsInRect` 单位专用索引（遍历 `_unitSubCells`，无 6 项组包），其启用即片 4 判据「`FillUnitsInRect` 全库 0 调用点·备而未用」的首个真实消费者。

---

## 八、判据 8（掩码退役：消费者清单 ＋ 退役后原文行）

- 消费者清单（改前）：`InteractionManager.cs:12`（定义）＋ `:51`/`:89`（消费）；`SelectionController.cs:21`（定义）＋ `:248`/`:335`/`:383`（消费）；`Assets/Editor` 全部 `.cs` grep **零命中**（无探针/编辑器消费者）。
- 退役后：两字段删除，原文处各留一行退役声明注释（[InteractionManager.cs:10-11](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Interaction/InteractionManager.cs#L10-L11)／[SelectionController.cs:20-22](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Interaction/SelectionController.cs#L20-L22)）；全库 grep `interactableMask|selectableMask` 仅命中该 2 行注释（输出片段见 §四）。

## 九、判据 9（派发优先级未变）

改后原文行（[InteractionManager.cs:91-122](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Interaction/InteractionManager.cs#L91-L122)）：`hitSource` 非空 → `:94 GetComponentInParent<IInteractable>()` 命中走 `Interact(ctx)`（ShowUI/DoAction/OpenSubmenu 派发）→ 否则 `:116 GetComponentInParent<IClickInteractable>()` → `:117 ClickInteractDispatcher.TryDispatch` → 都未命中 `:122 UIManager.Instance?.CloseCurrent()`。**三段优先级逐字保留**，仅「命中物来源」由物理改坐标拾取。

---

## 十、判据 10（常规）

| 项 | 读数 |
|---|---|
| 编译 | **0 error**（refresh_unity compile=request ＋ read_console types=[error] 仅存量 1 条无关旧项）；**0 新增 warning**（本批新增文件的 CS0219 已当场清除；warning 面为存量 9 条历史项） |
| 同 seed 逐格一致 | **两段两次独立建局**（seed=29417）hash 均为 `C445B25A73022B44`（features＋climateZones FNV 双混合 ＋ 长度）⇒ **逐格一致 ✅**（第 2 段原文：`上次=C445B25A73022B44 本次=C445B25A73022B44 ⇒ 逐格一致（hash+长度双同）✅`） |
| `AI.Core` 命中 | **0**（改动 7 文件全部在 `Assets/_Game/Systems/{World,Interaction,Building}` 与 `Assets/Editor/Smoke`；`AI.Core/**` 零触碰） |
| 改动文件行尾 | `MapGate/ChestManager/InteractionManager/SelectionController/BuildController/探针` 全 LF；`BuildingFactory.cs` 保持 **CRLF**（改前 CRLF 453 行 → 改后 CRLF 451 行、裸 LF=0，python 二进制按行替换保形态；该文件未用 Edit/Write） |
| 正门三态 | 两段均 `EnterTestRun` 进局 → 真暂停 `timeScale=0` ＋ `Save(hh294_slice6)` ＋ `ExitTestRun` ＋ `ExitPlaymode`（探针 Finish 原文：`★ 收尾：真暂停(TS=0)+封盘=True（槽=hh294_slice6）→ 退 Play`） |

---

## 十一、判据 11（回退预案：双轨开关——已落地）

- **开关本体**：[MapGate.cs:545-548](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/MapGate.cs#L545-L548) `public static bool UseLegacyPhysicsPick = false;`（原文行在场）。
- **收容面**：`MapGate.PickWorld`（`:490-493`，点拾取双轨）＋ `MapGate.FillUnitsInWorldRect`（`:502-530`，框选双轨）——旧路径全部收容在门内一个文件，上层零物理调用。
- **⚠️ 回退边界（诚实声明）**：`UseLegacyPhysicsPick=true` 只回退**拾取路径**；因 6-C 已删建筑 Collider 挂载，旧路径对建筑/宝箱**不可用**。完全回退（改前行为）须连 `BuildingFactory.cs`/`BuildController.cs` 两块挂载一并 revert（git 层面单 commit revert 即完整回到改前）。
- 对照 `底层执行计划` §五 5-B 回退预案「Collider2D 按需挂回（BuildingFactory 加判断）」：本批采用「开关＋revert 链」组合，效果等价且不留死代码。

---

## 十二、执行中发现的机理与勘正（随交付上报）

| # | 内容 |
|---|---|
| 1 | ⭐ **G0 符号实测推翻直觉**：CustomAxis (0,1,0) 下**小 y 在前**（两组交叉像素证据）；初版实现按「大 y 在前」暂定即被证伪——「符号禁凭记忆断言」纪律两次兑现（实现占位被 G0 纠正 ＋ 探针构造用例防止误判固化） |
| 2 | ⭐ **任务书 2-4「36 中 2」为壳文件误判**（§三.3 详证）：Resources/UnitPrefabs 34/36 是 PrefabInstance 壳，真身（包内 guid）带 Collider ⇒ 运行时 39/39 单位全带 BoxCollider2D。判据 3 的能力句已按实盘修正；「拾取不再依赖 Collider」的本质收益不受影响 |
| 3 | **同局内二次 `EnterTestRun` 会杀死探针协程**：`ResetWorldForNext` → `TeardownManager.TeardownScene()` 销毁探针 Host（首轮实测：协程静默死亡、无错误日志）⇒ 探针改两段式（两次菜单触发，hash 即时落盘跨段对比）——后续探针作者避坑 |
| 4 | **Editor 的 `Directory.GetCurrentDirectory()`＝Unity 项目根**（`Valley Rampart/Valley Rampart/`），非仓库根 ⇒ 探针落盘真实路径 `Valley Rampart/Logs/hh294_slice6/`（本报告引用路径以此为准） |
| 5 | `ChestManager` 拾取链验证可行：`ChestEntity` 实现 `IInteractable`（`Interact→ChestManager.Pickup`），`PickAt` 命中后走既有派发链零改动即可拾取 |

---

## 十三、未完成项与观察项（显式列出）

| # | 项 | 状态 |
|---|---|---|
| 1 | **6-A／6-B（双写收敛）** | ⛔ 本批不含（策划端已裁切批）——「资源点转纯数据／采集统一数据寻址」未动；`BuildingPanel` 采集流程零触碰（`BuildingPanel.cs:477-483→:588-592→StartGather` 原样） |
| 2 | GC「Gen0 增量 1 vs 空窗 0」来源 | 观察项：非每次分配形态（两轮实测均 ≤1·GetTotalMemory 差 0 B·静态路径零分配），精确归因需 Profiler 深挖，本批不作硬凑结论 |
| 3 | §G 旧路径命中 18 vs 新候选 19 的 1 差 | 归因说明（§七注）＝旧路径天然收不进无 Collider 物体；若验收口径要求新旧候选完全一致须另立判据 |
| 4 | 判据 3 措辞前提（「34 个无 Collider 单位」） | 被本批实盘证伪（§三.3）⇒ 已按实盘修正能力句并勘正任务书 2-4，**请验收端对勘正裁决** |
| 5 | `TryBuild` 选 def 逻辑首轮选到 bridge 被拒 | 已修（排除 bridge/gate/portal）并重跑取得玩家路径运行时证据；bridge 特殊落点校验路径未再测（非本批范围） |
| 6 | 同 y 平局（tie） | `PickAt` 同层带同键取先遍历者；Unity 渲染对同投影值的 tie 按实例序，理论可出现极低概率不一致（等轴格网下两实体深度键完全同值罕见），列已知边界 |
| 7 | 双轨删除 | 验收通过后删 `UseLegacyPhysicsPick` 两分支（`MapGate.cs` 单文件）⇒ Physics2D 生产码归零；本批不预删（守「验收后再删」） |

## 十四、应登记项（D767 纪律：不代写策划端账本，仅声明）

1. `_编号登记.md`：HH 水位线 304→**HH.305**（本报告）。
2. `_当前快照.md`：片 6-1 交付行（片 6 拆两批后第一段；6-A/6-B 仍挂）。
3. `_任务队列.md`：HH.294 片 6-1 交付行。
4. 台账新增节（§四十七）：本批 G0 符号结论 ＋ 2-4 壳文件勘正 ＋ 判据读数。
5. 教训库候选：`L-39`（文本 grep 判 YAML/壳文件结构）＋1 实例；`L-02`＋1（Editor CWD≠仓库根）。
6. `03` §8.6 可回填：「PickAt 已落地（HH.305）·符号结论＝小 y 在前（G0 实测）」——属契约侧，**请策划端落**（执行端不碰最高优先级文档）。

---

## 十五、commit

本报告与施工改动**同串 commit**（具名 `git add`，不含任何并行会话改动文件：美术／pixel-forge／GameScene／Packages／AI.Core 均未触碰）；不 push。

**证据文件（gitignore 域就地留档·不入 commit）**：`Valley Rampart/Logs/hh294_slice6/hh294_slice6_probe.txt`（＋两段时间戳副本）＋ `hh294_slice6_hash.txt`。
