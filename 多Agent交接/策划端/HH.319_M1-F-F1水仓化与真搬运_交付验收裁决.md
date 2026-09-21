# HH.319 · M1-F · F-1 批（水仓化 ＋ 真搬运链）· 交付验收裁决

- 被验：`多Agent交接/执行端/HH.319_M1-F-F1水仓化与真搬运_交付报告.md` · 施工 `64c7d9de`（24 项 · ⛔ 未 push）
- 依据：`D807` 评估裁决（`Q1`~`Q6` ＋ §二-1 国别过滤 ＋ `Well.droppable`）＋ `F-1` 施工任务书
- 判据三直读：①契约原文（`09` §4.3 D 组 ＋ `09#44`）②代码 `file:line` ③档位/字段
- 日期：2026-09-21 · 策划端（砚）· 代号 `D808`

---

## 〇 本端独立取证（14 项 · ⛔ 不采信转述）

| # | 取证点 | 实读结论 |
|---|---|---|
| 1 | 改动面 | ✅ 24 项与 `git show --name-status` 逐条一致（生产 11 ＋ Editor 9（含新增探针）＋ 资产 2 ＋ 报告 1 ＋ `WaterNetwork.cs` 删除） |
| 2 | ⭐ **`.meta` rename 风险** | ✅ **无 guid 复用**：旧 `WaterNetwork.cs.meta` guid ＝ `D3If5Hn8…`；新 `Valley_HH319_M1F_Probe.cs.meta` guid ＝ `CytNsHik…`（**新生成**）；git 的 `R076` 只是相似度检测 |
| 3 | ⭐ **旧 guid 残留引用** | ✅ **0 命中**（全库 grep `D3If5Hn8Vym6NZ3HNfEsQl9PIH/l8JtKbosSTiNlpEl5L0sKrXDYGJo=`）⇒ **无缺失脚本风险** |
| 4 | `WaterNetwork` 残留 | ✅ 27 处**逐条核**：全部为注释 / 探针字符串（`GetType("WaterNetwork")==null`、`HasSaveable("WaterNetwork")`）/ 枚举占位 ⇒ **零代码引用** |
| 5 | 件 2 `ProducerComponent:70-74` ＋ `:105-116 TickWaterToStorage` | ✅ `_isWell` 早返回**保留**（免工自产）＋ `IsFullFor(Water)` 停产 ＋ `_mainAccumulator` ＋ `FloorToInt` ＋ `_storage.Add(Water,n)` |
| 6 | 件 3 `:122-132 TryConsumeFarmWater` | ✅ `CanTake(Water,2)` → `TakeOut(Water,2)`；不足 ⇒ 气泡 ＋ `false`（形制不变，补 `_building != null` 守卫） |
| 7 | 件 4 `Building.cs:1461` | ✅ `public int waterThreshold = 20;`（**已改 int**）＋ Tooltip 改「**农场仓** Water 量」 |
| 8 | 件 4 `Building.cs:1548-1567` 挑水段 | ✅ 判据＝农场仓 `GetAmount(Water) < waterThreshold`；⭐ `task.source = well`（源＝水井 ⇒ 第一段位移）；`destType = SpecificBuilding` ＋ `destPos = transform.position`；args ＝ `HaulWaterArgs{target=storage, need=缺口}`；⛔ 无候选不发布 |
| 9 | ⭐ 件 4 `FindNearestSameKingdomWellWithWater:1578-1595` | ✅ **国别过滤在位**（`:1588 w.kingdomId != kingdomId continue`）＋ `def.id=="Well"` ∧ `Active` ∧ `GetAmount(Water) > 0` ⇒ ⭐ **本端补的必做项已落实**；附加收益：走 `BuildingRegistry.All`（⛔ 非 `FindObjectsOfType`） |
| 10 | 件 5 `TaskScheduler:525-542`（装载） | ✅ 新增 `WaterHaul && HaulWaterArgs` 分支 ⇒ 复用 `LoadInventoryFromSource`（源＝水井 ⇒ `GetComponent<StorageComponent>()` 直接命中）⇒ 转 `MovingToDest`；⚠️ 报告注释自陈"本分支是修复核心"✓ |
| 11 | 件 5 `:562-563` ＋ `:936-957 DepositWaterToFarm` | ✅ `target.Add(Water,n)` ＋ 溢出 `ReturnOverflow(..., wellStore)`（退回水井仓）＋ `target==null`（农场仓毁）同路 ⇒ ⛔ 资源不丢 |
| 12 | 件 5 删除面 | ✅ `:679-681` 凭空入桶 `case` **整段已删**（留注释痕）；`:1066-1068` `case KingdomDestType.WaterNetwork` **已删**；`ResolveWaterSource` **零代码引用**（残留全注释） |
| 13 | 件 1 落码 | ✅ `GameEvents.cs:199 Water`（**末尾追加** · 枚举值 13）＋ ⚠️ 该文件 **CRLF=666**（执行端走 python 二进制 ✓ 未出幽灵 diff）；`ResourceCatalog:57` `Entry(Water, [res_fluid.water], 1, "水")` |
| 14 | 件 7 资产 | ✅ `Well.asset:34 droppable: 0`；`farm.asset:33-34` ＝ `[res_food.grain, res_fluid.water]`；`waterCarryAmount` 改 `int` ＋ Tooltip 自陈"⛔ 不再参与搬水链" |

**件 1~8 ＝ 全部落地且正确** ✓（与报告 §一 逐条吻合）

---

## 一 ⭐ 本端新发现 —— `HaulWaterArgs.need` **零消费方**（报告未提）

| 落点 | 实读 |
|---|---|
| `KingdomTask.cs:97／:104` | 注释**两处承诺**：`need` ＝ 缺口量（**装载上限 · ⛔ 不多搬**） |
| `TaskScheduler:525-542` | 装载调 `LoadInventoryFromSource(brain, task)` |
| `TaskScheduler:767-796 LoadInventoryFromSource` | ⭐ **完全不读 `task.args`**（除 `ScaleTaskArgs` 副产分支）⇒ 上限 ＝ `st.GetCarryAmount(carried)`（携带量） |

⇒ ⭐ **`need` 写了但没人读** ⇒ 属 **"备而未用"**（`L-47` 家族；与 `L-54` 对偶 —— 一个查"谁写"，一个查"**谁会读我写的**"）。

⚠️ **本端自纠**：`need` 这个字段**是我在任务书里写的**（我写「装载上限 · ⛔ 不多搬」时，假定复用的装载段会读 args）⇒ **我犯的正是我刚立的 `L-57` 的同类错**（未核被复用方法的实际入参面）。

**实害评估（本端复算 · 结论：无实害）**：`ResourceCarryConfig` **无 `Water` 条目** ⇒ `GetCarryAmount(Water)` 走 `defaultCarryAmount=10` ⇒ 每次搬 10；`WaterHaul` 广告条件为"水 < 20" ⇒ 补后 ~30 即停 ⇒ 农场仓 100 格内水占 ≤30 ⇒ **粮位充足** ⇒ ⛔ 不构成缺陷。

⇒ **裁**：⭐ **删 `need` 字段**（两处：`:104` 声明 ＋ `:97` 注释）—— 理由：水搬运与 `Transport` **同构**（按携带量搬），而 `09` §16.1-2 的「阈值拦截」是**建造专属契约** ⇒ 水不需要它；保留一个未被消费的字段＝**误导后读**。
⚠️ **零行为影响** ⇒ ⛔ 不阻塞销号；列入 `F-2` 同批或独立小改。

---

## 二 判据复核（报告 9/10 · 本端逐条核）

| # | 报告判定 | 本端复核 |
|---|---|---|
| 1 | ✅ 落码 | ✅ 认可（§〇-13） |
| 2 | ✅ 井产水入本仓 ＋ 满仓停产 | ✅ 认可（§〇-5）；读数 `t0=0→8/16` ＋ 注满 100→持平 = 停产 ✓ |
| 3 | ⚠️ 3a＋3c 通过 · 3b 不稳 | ⚠️ **有条件**（见 §三-1） |
| 4 | ⚠️ 逻辑通过 · 环境不稳 | ⚠️ **有条件**（同 §三-1）；v6 读数（耗 4 水 / 产 4 粮 / 水尽停产）**逻辑自洽** ✓ |
| 5 | ✅ 国别过滤 | ✅ **认可 · 本端重点项**（§〇-9 实读在位）＋ 报告给了"玩家井 100 作诱饵 + AI 井清空 ⇒ null"的**双侧读数** ⇒ 鉴别力成立 |
| 6 | ✅ 退役 | ✅ 认可（§〇-2/3/4 三项独立核） |
| 7 | ✅ 资产 | ✅ 认可（§〇-14）＋ 报告给"farm 有水 ⇒ 箱数 0→1"的**对照** ✓ 鉴别力成立 |
| 8 | ✅ 整数化 | ✅ 认可（§〇-7/14） |
| 9 | ✅ 读数项 | ✅ 读数认可 ⇒ 裁决见 §四-③ |
| 10 | ✅ 存档往返 | ✅ 认可（`BuildingSaveData.storageContents` ⇒ 零新存档面） |
| 11 | ⚠️ 部分 | ⚠️ `HH316 §A~§D` ✓ ／ `§E~§H` ＋ `HH315`／`HH317`／`2_20B_M7` ⛔ **未跑** |

### ⚠️ 判据 3 为何"有条件" —— `L-51` 的直接命中

报告的 **3c 是"反射直调生产方法"**（`DepositWaterToFarm` 走 `GetMethod` 直调）⇒ 属**构造法**；而 3b（走调度器端到端）8 轮不稳。
⇒ ⭐ 这正是 **`L-51`** 要求的"**样本若绕过生产入口，必须单列一条生产路径可达性读数**" —— 而该读数**本轮未能拿到**。
⇒ ⇒ **判据 3 未满足 `L-51`** ⇒ 这不是"报告写得不好"（它**如实列报**了 8 轮迭代实录 ✓），而是**本轮确实缺一条生产路径证据**。

---

## 三 三条报裁 · 裁决

### ① 探针环境不可控（判据 3b 端到端 ＋ 判据 4 读数不稳）

**报告实录**（8 轮）：v4/v5 被旧 `Production` 刺激拉离 ／ v6 卡在 `MovingToSource`（距井 1.29 未达 `ArrivalThreshold`）／ v7 任务被抢占 ／ v8 `派WaterHaul=False`。

**本端裁**：⭐ **要求补一次「真实长局观察」**（⛔ 不豁免）—— 依据三条：
1. **`L-51` 硬要求**：生产路径可达性必须有一次走生产入口的读数（3c 是构造法 ⇒ 不能替代）；
2. ⭐ **v6 那条值得警惕**：若"工人走到水井"在真实局中也不可靠 ⇒ 那是**生产缺陷**，而非探针问题 ⇒ 只能由长局回答；
3. `M1-C` / `M1-D` / `M1-E` 的验收都拿到了生产路径读数 ⇒ **本片不应例外**。

⚠️ **并入哪一批**：由用户定 —— 可 ① **验收批补跑** ② 并入 **`F-2`** 同局跑 ③ 独立小单。⚠️ 本端**不自行降低判据**（同意报告立场）。

### ② `WaterHaul`（C 档）× `Production`（B 档）刺激竞争

**报告读数**：同一工人身上同时有"指向水井的 `WaterHaul`（C 档）"与"指向农场的 `Production`（B 档）"⇒ 被 B 档拉向农场（距井单调增大）。
**报告归因**（本端认可）：改前 `source=农场` ⇒ 两刺激**同向** ⇒ 冲突不可见；改后 `source=水井` ⇒ **反向** ⇒ 显形。⚠️ 与 `D807 §二-1` **同族**（缺陷被"半假搬运"掩盖）。

⭐ **本端补一条关键取证**（报告未提）：`Building.TryAdvertiseTask:1514` 的生产段守卫含 **`!sched.HasWorkerAssigned(this)`**
⇒ ⭐ **工人一旦被指派（含 `WaterHaul`）⇒ `Production` 不再广告** ⇒ **不会注入新刺激**；
⇒ 故该冲突的实际窗口 ＝ **已注入旧刺激的存活期**（`taskExpiry = 5s`）⇒ **短暂扰动**，而非"永久拉走"。

**裁**：⭐⭐ **立 `U-14`**（`WaterHaul` 刺激竞争 · 归 `F-2` 同批或独立评估）——
- ⛔ **本批不修**（理由：需独立取证"刺激注入/失效机制 ＋ 优先级表全局影响"，不宜在验收里拍）；
- ⚠️ **但长局观察必须记录**：「工人接到 `WaterHaul` 后**是否最终到达水井并完成搬水**」⇒ 该读数**决定 `U-14` 的严重度**（若长局中稳定完成 ⇒ 降为观察项；若被拉走 ⇒ 升为缺陷）。
⇒ ⛔ **不阻塞 `F-1` 销号**（当前的"短暂扰动"定性未坐实为缺陷）。

### ③ 判据 9：井仓满 ⇒ `Transport` 直达农场，与"农场缺水 `WaterHaul`"功能重叠

**报告读数**：`井仓=100/100 ⇒ well.TryAdvertiseTask=True · 类型=Transport · 终点=NearestWarehouse`；全库**唯一收水仓 ＝ farm**。

**裁**：⭐ **甲 —— 视为「合理兜底」**（⛔ 非打架）。理由三条：
1. `09` §4.3 D 组 只规定"水进**普通仓** ＋ 工人搬"⇒ 井仓满 ⇒ 广告 `Transport`（搬去能收的仓）是**通用规则的自然结果**，⛔ 非本片新增机制；
2. ⭐ **两条链目的不同**：`WaterHaul` ＝ **按需补水**（农场水 < 阈值时触发）／`Transport` ＝ **防溢**（井仓 ≥ `capacity×threshold` 时触发）⇒ **不是重复，是互补**；
3. 挑水玩法的核心（"农场缺水 ⇒ 有人去取"）**仍在** ⇒ `WaterHaul` 未被架空。

⚠️ **连带**：`09` §4.3 D 组 **加注**说明"两条搬水链并存 ＋ 分工"（⛔ 不留白 ⇒ 防后读误判为缺陷）。

---

## 四 红线复核

| 红线 | 本端核 |
|---|---|
| 改动面**限水域** | ✅ 生产 11 文件全在水域（`ProducerComponent`／`TaskScheduler`／`Building`／`KingdomTask`／`GameEvents`／`ResourceCatalog`／`TaskPriorityConfig`／`WorldLifecycle`／`KingdomState`(仅注释)／`AIDebugSpawnController`(仅注释)／删 `WaterNetwork`）；⛔ 未动 `WorkerInventory`／`UnitController`／`DamageSystem`（多资源化属 `F-2`）✓ |
| ⛔ 不动退款公式 | ✅ `git grep` 零命中 |
| ⛔ 不动 `DeathCause`／产金端／存档 schema | ✅ 认可 |
| 行尾纪律 | ✅ `GameEvents.cs` **CRLF=666** ⇒ 走 python 二进制 ✓（本端独立复读确认该文件仍 CRLF）；其余 LF ✓ |
| ⛔ 不碰 `WarehousePanel`／四档账本／美术／`pixel-forge`／`GameScene`／`Packages`／`3.6`·`3.8` doc | ✅ 认可 |
| 探针口径演进留痕（`件6` 要求） | ✅ **合规**：报告 §二 单列 9 行（含"改文本 ＋ 注明演进"）；本端抽核 `Valley2_17_Smoke_11.cs:8`〔改「水仓化结构探针」〕／`Valley2_17_Smoke_2b.cs:129`〔"演变说明（⛔ 不得静默改）"〕✓ |

---

## 五 落账

- 本裁决书新建
- 台账 **§一百一十二**
- `_编号登记.md`：`HH.319` ⇒ **已交付 · 验收有条件通过（待补证）**
- `_任务队列.md`：`M1-F` 行更新（补 `F-1` 状态）；新插 **`U-14`**（刺激竞争）
- `_当前快照.md`：`D808`
- `_策划教训库.md`：新立 **`L-58`**
- `09_资源与仓库.md`：§4.3 D 组加注（两条搬水链分工 · 判据 9 裁决）

---

## 六 状态与下一步

| | |
|---|---|
| `HH.319` `F-1` | ⚠️ **有条件通过 · ⛔ 不销号** —— 件 1~8 ✅ 全落地正确；判据 1/2/5/6/7/8/9/10 ✅；**判据 3 · 4 有条件**（缺生产路径读数）；判据 11 回归部分 |
| 待补（`F-1` 销号前置） | ① ⭐ **真实长局观察**（生产路径搬水链 ＋ 记录 `U-14` 的两读数）② **回归补齐**（`HH315`／`HH317`／`2_20B_M7` ＋ `HH316 §E~§H`） |
| 本端新裁 | ⭐ `HaulWaterArgs.need` ⇒ **删**（备而未用 · 零行为 · ⛔ 不阻塞）／判据 9 ⇒ **甲·合理兜底** ＋ `09` 加注 |
| `M1` 完成度 | **5/7**（`F` 进行中 · `G` 未开） |

**▶️ 下一步（两笔可并行）**：① **`F-1` 补证单**（长局观察 ＋ 回归补齐 ＋ `need` 删除）② **`F-2` 施工**（NPC 仓多资源化 ＋ 自交互）—— ⚠️ 后者前置＝报 `NpcProfessionDef` 字段形状（`D807` §四-6 已要求）。
