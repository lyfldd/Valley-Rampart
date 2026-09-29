# HH.341 · 小源首源 —— **`D909` A 方案：一次只读「玩家王国侧供给面」核查**（玩家建筑全集 ＋ 玩家仓储全集（含空仓）＋ 资源需求交叉表）交付报告

> 执行端 ｜ 2026-09-29 ｜ 单会话 ｜ ⛔ 零生产码／资产／政策值 ｜ ⛔ 未 push ｜ 挂 `D909`（⛔ 未取新号）
> 依据：`D909` 方向裁断 —— 台账 **§二百二十四**（`Q2` 范围再收窄 —— §二百二十二 末尾加注）｜`D908` 复议 —— **§二百二十三**｜`L-97`（＋补条 `D897`／`D904`／`D905`）｜`L-98`（＋补条②）｜`L-99`
> 边界照办：**只读探针** —— ⛔ **未调 `TryAdvertiseTask`／`FindPickup`／任何源侧方法**；⛔ **未调 `BeginMaterialPhase`**；⛔ 未修改建筑状态／配方／投料；⛔ 不申请 `ChestEntity`；⛔ 未并 `DZ-7`；⛔ 不预先采用 `D` 新口径；⛔ 不碰 `GameScene.unity`（只登记 hash）；⛔ 未 `checkout --`／未 `restore`；⛔ 不改 `taskTimeout`／`Complete`；⛔ 未在报告或落盘串写「判绿／判红」
> ⭐ **写动作计数＝0**（游戏实体）；唯一入口动作＝HH.92 建局门面 `TestHarnessApi.EnterTestRun`（历轮同型；含新槽位 `hh341d909` 由门面落盘——如实声明，见 §7-1）。

**三块完成状态**：① 玩家建筑全集 ✅（`kingdomId==0`＝**1 座**；非空配方承载者＝**0**）｜② 玩家仓储全集（含空仓）✅（全量 **23 座**枚举（绕开 `GatherActive`）；`k0` 侧＝**1 座**）｜③ 资源需求交叉表 ✅（**`SUPPLY_GATE ＝ 不适用`**（没有真实 `CSS` want）×2 扫描）——无未做项。

---

## 1. 开工基线（实测）

- `HEAD`：开工＝收工＝ **`62098d8e31dd2997e7932b8c13f2f1e1c603aa6f`**（无漂移；相对本端上轮报告 `8e32f6ec`／`af7c0d64`，其间 **3 笔非本端 docs**：`42ff555e` `D908` 核实、`cf516479` `D908` 复议裁定、`62098d8e` `D909` 方向裁断——如实列报）
- `Assets/_Game` diff＝**空**（实测输出见 §附）
- `GameScene.unity`：`git hash-object` 开跑前＝收工后＝ **`4a86f26f6a7c2aab3c440903ddfa80020ddec9a3`**（逐位一致；仍为挂账 `O-14` 载体 ⇒ 只登记）
- ⛔ **未执行任何回滚类命令**（无 `checkout --`／无 `restore`；`L-99` 连续第 6 轮遵守）
- **本端写动作计数＝0**（游戏实体：⛔ 未调 `BeginMaterialPhase`／`TryAdvertiseTask`／`FindPickup`／任何源侧方法；⛔ 未改建筑状态／配方／政策值）；入口＝HH.92 建局门面（`EnterTestRun`，t=0.84，含建局）（详见 §7-1 声明）
- 探针 run：`success=true` `elapsed_ms=18340`；墙钟＝**12.80 s**；`playMode` 收尾＝**stopped**（stop 载荷实测）

## 2. 玩家建筑全集（只读）

**行集来源**：`## S BLD_ROW`（`BuildingRegistry.All` 全量·分母用）＋`## S PBLD_ROW`（`kingdomId==0`·逐座 8 项）。**候选判据沿用 `D905`**：`def.cost` 非空 ∨ `levels[level-1].upgradeCost` 非空。

**三数＋分母（逐扫描）**：`[scan0]` 原始 **21** → 排除 20（非 `k0`）→ 有效 **1**（分母 21）｜`[scan1]` 同（21→20→1，分母 21）。非采样行＝段标记／汇总行（不计入）。
`BLD.kingdom` 域（两扫描一致）：`0=1｜1=6｜2=6｜3=6｜-1=2`｜`BLD.cand` 域：`True=12｜False=9`（候选 12 座全为非 `k0` 侧）。

**`k0` 逐座 8 项（`PBLD_ROW` 原样 · 两扫描逐位一致）**：

| # | 项 | 读数 |
|---|---|---|
| ① | `def.id` | **`castle`** |
| ② | `state` | **`Abandoned`**（`level=1`） |
| ③ | `def.cost` 项数 | **0**（`costTok=<空配方>`） |
| ④ | `levels[level-1].upgradeCost` 项数 | **0**（`upCostTok=<无该级upgradeCost>`） |
| ⑤ | `IsSiteAwaitingMaterials` | **False** |
| ⑥ | `siteStore` | **null** |
| ⑦ | 已登记 `_sources` | **False** |
| ⑧ | `SaveId`／坐标／唯一实例标识 | `Building_3eeae32dff9a432f90d1524d6f165ce5`｜`coord=108,97`｜世界 `8.32,66.24`｜`footprint=3,3`｜`instId=-4276` |

⭐ **`k0` 候选命中数＝0**（`PBLD.candidate=False` ×2 扫描；分母＝`bldK0=1`）⇒ **玩家侧无任何非空配方建筑**（⛔ 未修改状态／配方／触发投料）。

## 3. 玩家仓储全集（只读 · ⚠️ 含空仓）

**行集来源**：`## S STO_ROW`（＋`COUNT_ROW`）。**枚举口径**：`FindObjectsOfType(StorageComponent, includeInactive:true)` ∪ 反射只读 `WarehouseRegistry._storages`（两源并集）。
⭐ **声明：是否绕开 `GatherActive` ＝ 是** —— ⛔ 未以 `WarehouseRegistry.GatherActive(0)` 作枚举（其 `:46 TotalCount>0` 会系统性滤掉空仓）；`GatherActive(0)` 仅作『过滤视图』对照计数（见下）。

**三数＋分母（逐扫描）**：`[scan0]` 原始 **23** → 排除 22（非 `k0`）→ 有效 **1**（分母 23）｜`[scan1]` 同（23→22→1）。
**全量域（两扫描一致）**：`STO.kingdom`＝`0=1｜1=7｜2=7｜3=7｜-1=1`｜`inRegistry`＝`True=13｜False=10`｜`viaFind`＝`True=23`｜`enabled`＝`True=23`｜`activeInHier`＝`True=23`｜非 `k0` 仓 `parentDef`＝`castle=3｜Warehouse=3｜Well=3｜farm=3｜mine=9｜<null>=1`。
**`COUNT_ROW`（两扫描一致）**：`stoFind=23｜stoReg=13｜stoUnion=23｜gatherActive0=1｜k0Sto=1｜stoByKingdom=-1:1/0:1/3:7/2:7/1:7｜k0Workers=4`。
**空仓计数（含空仓口径）**：`[scan0]` 空仓 **21 座**（分母 23；非空＝`k0 Vault`＋`Chest`）｜`[scan1]` 空仓 **9 座**（分母 23；窗内 AI 侧产出/搬水入仓 ⇒ 非空增至 14 座）——**`k0` 面两次扫描均 0 座空仓**。

**`k0` 逐仓 8 项（`STO_ROW` 原样 · 两扫描逐位一致）**：

| # | 项 | 读数 |
|---|---|---|
| ① | 所属 `kingdomId` | **0** |
| ② | 父建筑 `def.id` | **`castle`**（仓体 `name=Vault`；`parentSaveId=Building_3eeae32dff9a432f90d1524d6f165ce5`） |
| ③ | 是否激活／在册 | `enabled=True`｜`activeInHier=True`｜`inRegistry=True`｜`viaFind=True` |
| ④ | `Accepts(resource)` | **收 10 类**：`Gold+Stone+Wood+Ore+Metal+Crystal+FireOil+Food+SpecialFood+Meat`（声明路径 `res_material+res_food+res_currency`） |
| ⑤ | `GetAmount(resource)` | `Stone=100`｜`Gold=100`｜`Wood=100`｜`Food=50`｜其余 0 |
| ⑥ | `TotalCount` | **350**（`capacity=250`；`items=Gold:100+Stone:100+Wood:100+Food:50`） |
| ⑦ | **`Stone` 标签与存量** | **`stoneAcc=True`｜`stoneAmt=100`** |
| ⑧ | 其他资源标签与存量 | 标签＝④所列；存量＝`Gold:100｜Wood:100｜Food:50`；`Ore/Metal/Crystal/FireOil/SpecialFood/Meat`＝0 |

**被排除的仓及排除原因逐条计数（口径＝`FindPickup` 四规则逐字：同国 ∧ `StorageComponent` ∧ `Accepts(r)` ∧ `GetAmount(r)>0`；分母＝全量 23 座）**：

| 排除原因 | 计数（每资源行恒定值 · 14 资源行） | 说明 |
|---|---|---|
| **非 `kingdomId==0`** | **22**（分母 23；×14 资源行恒定） | 非 `k0` 仓一律排除（`k1/k2/k3` 各 7＋`k-1` 1） |
| **非 `StorageComponent`** | **0**（零计数项） | 枚举源（`FindObjectsOfType(StorageComponent)` ∪ 注册表）本身即 `StorageComponent` ⇒ 恒 0 |
| **`Accepts=False`**（`k0` 内） | **1**（出现于 4 个资源行：`StoneAmmo/FireballAmmo/MagicAmmo/Water`） | `k0` 仓 1 座对上述 4 类标签不收 |
| **`GetAmount<=0`**（`k0` 内） | **1**（出现于 6 个资源行：`Ore/Metal/Crystal/FireOil/SpecialFood/Meat`） | 收但零存量 |
| **命中（match=1）** | **1**（出现于 4 个资源行：`Gold/Stone/Wood/Food`） | `Vault` 满足四规则 |
| `Stone` 单资源读数（分母＝`k0Storages=1`） | **`exclAccept=0`｜`exclAmt=0`｜`match=1`｜`avail=100`** | 两次扫描一致 |

## 4. 资源需求交叉表（条件性）

⭐ **判定：`SUPPLY_GATE ＝ 不适用`（没有真实 `CSS` want）** —— 依据：**`k0` 非空配方承载者＝0**（`PBLD.candidate` 全 `False`；分母＝`bldK0=1`；两扫描一致；`CROSS_ROW`＝0 行 ⇒ 与 `SUPPLY_GATE` 不适用一致）。⛔ **未凭 AI 的 `want=Stone` 推导玩家侧实际需求**（本轮报告未引用任何 AI 侧 want 作玩家需求）。

**补充：全域供给对照表**（行集＝`## S SUPPLY_ROW`；口径＝按 `ResourceCatalog` 资源表 14 项逐一、对 `k0` 仓逐字应用 `FindPickup` 四规则（只读复演）；⚠️ 这是**供给能力对照**，⛔ 非需求推导、⛔ 不构成 `CSS` want）：

| 资源 | `match`（可用仓数） | `avail` | 原因（`k0` 内） |
|---|---|---|---|
| Gold／Stone／Wood | **1｜1｜1** | **100｜100｜100** | 满足四规则（`Vault`） |
| Food | **1** | **50** | 满足四规则（`Vault`） |
| Ore／Metal／Crystal／FireOil／SpecialFood／Meat | 0 | 0 | `GetAmount<=0`（收但零存量） |
| StoneAmmo／FireballAmmo／MagicAmmo／Water | 0 | 0 | `Accepts=False`（标签不收） |

（`SUP.exclNotK0=22`／`SUP.exclNotStorage=0` 恒值；两次扫描逐位一致。）

## 5. 收工判据逐条（照录 · 只能二择一）

| # | 条件（照录） | 本轮判定 |
|---|---|---|
| ① | `kingdomId==0` 的非空配方承载者为 0 | **满足**（两次扫描：`PBLD.candidate=False` ×2；分母＝1 座 `k0` 建筑；判据＝`def.cost` 非空 ∨ `levels[level-1].upgradeCost` 非空） |
| ② | 在真实玩家需求集合中，没有任何满足「同国 ∧ `Accepts(resource)` ∧ `GetAmount(resource)>0`」的仓 | **不适用／不成立** —— 需求集合为空（① 成立 ⇒ 无真实 `CSS` want）⇒ **无判定对象**；⭐ 且全域对照实测**存在**匹配仓（`Vault`：`Gold/Stone/Wood/Food`）⇒ 按任一具体资源评估，「**没有任何满足规则的仓**」这一表述**不成立** |
| ③ | 两项均有完整行集、分母、字段域和观察窗记录 | **满足**：行集＝`BLD_ROW`／`PBLD_ROW`／`STO_ROW`／`SUPPLY_ROW`／`COUNT_ROW`（各标分母与三数）＋观察窗 7 项（§6） |

⭐ **结论（二择一）：⛔ 不收口为「玩家侧结构性阻塞」**（② 未满足收口要件）。

**按裁分列登记（二者分开登记，⛔ 未合并成一句）**：
1. **「承载者」缺口登记**：**`k0` 非空配方承载者＝0**（本轮两次扫描实测；限定本观测域）——玩家侧当前**无任何可投料承载者**。
2. **「仓储供给」登记**：⛔ **未观测到仓储供给缺口** —— 实测 `k0` 存在 **1 座可用取料仓**（`Vault`，`castle` 所属），对 `Gold/Stone/Wood/Food` **满足 `FindPickup` 四规则**；其余 10 类不匹配的原因逐条可复核（6 类无存量／4 类标签不收）。⇒ 「`k0` 取料仓不可用」这一假设**在本观测域内未获支持**（⚠️ ⛔ 不写成全局否证）。

## 6. 口径落实声明（逐条）

1. **行集来源**：每表均标 `## S <行型>`（§2＝`BLD_ROW`／`PBLD_ROW`；§3＝`STO_ROW`／`COUNT_ROW`；§4＝`SUPPLY_ROW`（＋`CROSS_ROW`＝0／`SUPPLY_GATE` 行））；**同一小节未混列不同 `## S` 行型**；分母与三数随表给出；**零计数项**（`exclNotStorage=0`）已如实列出。
2. **`Time.time` 与墙钟分列**：游戏秒＝`Time.time` 差（实测：`t_enter=0.84 → t_wrap=181.19`，窗 180.3）；墙钟＝`Stopwatch` 单列（12.80 s）；折算常数 `timeScale=15.0`（⛔ 不引短窗倍率、⛔ 禁 `n×timeScale`）。
3. **观察窗 7 项（`L-98` 补条②，原样）**：①暖机 t=0.51→0.84（含建局）游戏秒 0.3｜②轮询 t=0.84→181.19 游戏秒 180.3（逐帧心跳＋两次全量扫描 `scan0`／`scan1`）｜③零点＝进局就绪 t=0.84（**本轮无触发动作·只读**）｜④观测窗 t=0.84→181.19＝**180.0 游戏秒窗**｜⑤墙钟 12.80 s；`timeScale=15.0`｜⑥收工条件＝`scan0` 起 180 游戏秒 → `scan1` 完成 或 会话安全阀 420 秒；实际＝`window180`｜⑦在任务段内＝是（窗内全量派发事件 114）。
4. **`Q2` 适用范围收窄照办**：⛔ **未据 `Q2`（`k1` AI 控制样本）读数刻画 `k0` 供给面** —— 本轮 `k0` 全部读数均为**本轮自采**（`BLD_ROW`／`PBLD_ROW`／`STO_ROW`／`SUPPLY_ROW`）。
5. **量纲声明**：`WaitForSeconds(n)` ⇒ 游戏秒＝n（本轮未用长等待门面）；单帧游戏推进＝`unscaledDt × timeScale`；⛔ 短窗倍率不作折算结论。

## 7. 自报瑕疵（⛔ 不软化、不省略）

1. **入口动作声明**：本轮入口＝HH.92 建局门面 `TestHarnessApi.EnterTestRun`（新槽位 `hh341d909` 由门面落盘）——**游戏实体写动作计数＝0**（⛔ 未调 `BeginMaterialPhase`／`TryAdvertiseTask`／`FindPickup`／任何源侧方法）；入口与槽位落盘如实声明，⛔ 未删除既有槽位。
2. **观测窗＝建局初始 180 游戏秒**（无任何玩家输入）：⛔ 未覆盖「玩家行动产生真实 want 后」的供给面（该场景需写动作，⛔ 本轮未做）——列 §8-5。
3. **`k0` 侧两次扫描逐位一致**（castle／Vault 零变化）；窗内变化全在非 `k0` 侧（AI 井/农场入水、矿副产仓入料 ⇒ 非空仓由 2 座增至 14 座）——如实并列。
4. **`GatherActive(0)` 对照＝1（＝全量 `k0` 枚举数）**：本 run `k0` 侧**无空仓** ⇒ 过滤效应在 `k0` 面不可见；空仓过滤效应仅体现在非 `k0` 面（21 座空仓）。⛔ 未以该对照替代全量枚举。
5. **枚举口径限制**：`FindObjectsOfType(..., includeInactive:true)` 含未激活对象，但⛔ 不含 prefab 资产（未用 `FindObjectsOfTypeAll`）；两源并集＝23 座（注册表 13 ⊂ 并集 23）⇒ 未增未漏（`viaFind=True` ×23）。
6. **静态快照**：本核查为两次时点快照（`scan0`／`scan1`），⛔ 非连续轨迹；`enabled/activeInHier` 为采样时刻值。
7. **同 seed ≠ 同一局**：本 run 与 `D906` 轮同 seed `424242` 但为**独立新局**（`k0` castle `SaveId=Building_3eeae32d…` 跨局不同属预期；坐标 `108,97`／世界 `8.32,66.24` 与本端历轮读数一致）。

## 8. 【请裁】（5 条 · 只列需裁定的）

1. **② 判读口径确认**：真实需求集合为空（① 成立）⇒ ②「不适用（无判定对象）」；本轮据此**不收口**（⛔ 未采「真空成立」读法）。请确认该读法（或指定字面读法）——⚠️ 两种读法下收口前提均不牢：实测 `k0` 存在匹配仓。
2. **「承载者」缺口登记**：`k0` 非空配方承载者＝0（本轮两次扫描实测）⇒ 是否登记为「**玩家侧承载者缺口（已支持 · 限定本观测域）**」？
3. **供给面读数登记**：`k0` 存在可用取料仓（`Vault`：`Gold/Stone/Wood/Food` 满足四规则；6 类无存量／4 类标签不收）⇒ 是否登记「**`k0` 取料仓不可用 ＝ 本观测域内未获支持**」（⚠️ ⛔ 非全局否证），并是否将供给侧读数入账？
4. **全量枚举与在册差异**：全量 23 座（含空仓；在册 13 座；`mine` 副产仓 9 ＋ `Chest` 1 不在册）⇒ 是否需将「非在册仓」纳入后续口径（本轮已全量列示，含空仓）？
5. **覆盖范围**：本核查＝建局初始 180 游戏秒窗（零玩家输入）；若需覆盖「玩家行动后真实 want 的供给面」⇒ 需另立动作（触发玩家侧建造＝写动作，⛔ 本轮未做）——是否后续单独立项/由主策划决定？

## 9. 源级结论

⛔ **维持 `ConstructionSiteStore` 未通过 · 停手**（`D909` 口径：**即使本轮判出「结构性阻塞」，源级仍维持未通过** —— 且本轮**未**收口；⛔ 未写「判绿／判红」字样；⛔ 未申请下一源、⛔ 未并 `DZ-7`、⛔ 未取新号、⛔ 未代写策划端账本、⛔ 未预先采用 `D` 新口径）。
本轮新增事实：**`k0` 建筑=1 座 `castle`（`Abandoned`·配方空；非空配方承载者＝0）**｜**`k0` 仓储=1 座 `Vault`（含空仓口径下全量 23 座；`Vault` 对 `Gold/Stone/Wood/Food` 满足 `FindPickup` 四规则；`Stone=100`）**｜**交叉表＝`SUPPLY_GATE 不适用`（没有真实 `CSS` want）**｜**收工判据＝①满足／②不适用且不成立／③满足 ⇒ 不收口；「承载者缺口」与「仓储供给读数」已分开登记**。

---

## 附：合规声明（实测输出）与交付件

```
git rev-parse HEAD（开工=收工）        62098d8e31dd2997e7932b8c13f2f1e1c603aa6f
git diff --name-only -- Valley Rampart/Assets/_Game     （空）
git hash-object GameScene.unity（开跑前=收工后）        4a86f26f6a7c2aab3c440903ddfa80020ddec9a3
回滚类命令                              未执行（checkout -- / restore 均无）
本端写动作计数（游戏实体）               0（入口=HH.92 建局门面；未调任何源侧方法/未触发投料）
探针 run                                success=true elapsed_ms=18340 墙钟=12.80s
playMode 收尾                            stopped（stop 载荷实测）
```

**交付件（`Valley Rampart/Logs/`，均命中 `.gitignore`）**：`hh341_small_construction_site_d909.{cs,json,txt}`＋`…_d909_run.ps1`＋`…_d909.cs.runtime.json`／`…_d909.cs.runtime_resp.txt`＋`…_d909.logs.txt`＋`…_d909_stat.py → …_d909_stat.txt`（空跑 `…_d909_stat_dry.txt`）＋`…_d909_state.json`／`…_d909_stop.json`（bridge 状态与收尾，只读）。
（`HH.341` 报告文件＝本文件；⛔ 未 push。）