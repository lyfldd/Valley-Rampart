# HH.319 · M1-F · F-1 补证单交付验收裁决（⛔ 发现阻塞级缺陷）

- 被验：`多Agent交接/执行端/HH.319_M1-F-F1补证_交付报告.md` · 提交 `c8b224ec`（6 文件 · ⛔ 未 push）
- 依据：`D808` 验收裁决（补证三项：长局观察／回归补齐／`need` 删）
- 判据三直读：①契约（`09` §4.3 D 组 ＋ §7.2 ＋ `09#40`）②代码 `file:line` ③档位/字段
- 日期：2026-09-21 · 策划端（砚）· 代号 `D809`

---

## 〇 本端独立取证（11 项）

| # | 取证点 | 实读结论 |
|---|---|---|
| 1 | `StorageComponent.HarvestCarry:320-328` | ✅ 逐行坐实：`type = PrimaryStoredType()` ⇒ `amount = min(存量, 携带量)` ⇒ ⭐ `TakeOut(type, amount)` ⇒ ⭐ `RulerController.Instance?.ModifyResource(type, true, taken)` |
| 2 | `RulerController.ModifyResource:230-246` | ✅ 非金分支 ⇒ `:242 int moved = isIncrease ? tv.Deposit(type, amount) : tv.Take(type, amount);` ⇒ ⚠️ **返回值 `moved` 只用于日志**（`:244 Debug.Log`）⇒ ⛔ **无告警、无回滚**（§二-3 措辞勘正） |
| 3 | `TreasureVault` 收水面 | ✅ `VaultPaths = [res_material, res_food, res_currency]`（`M1-E` 加）⇒ **不含 `res_fluid.water`** ⇒ `Accepts(Water) = false` ⇒ `Deposit(Water, n)` 返 **0** |
| 4 | ⭐⭐ **销毁链成立**（⚠️ **`D810` 勘正：定性由「销毁」改为「转箱」** —— 见文末 §勘正） | ✅ `TakeOut` 已扣 ＋ 落点拒收 ⇒ **水消失**（报告核心读数：井仓 12→2 ＝ `HarvestCarry` 返回 10；玩家国全库 Water 12→2 ＝ 差 −10） |
| 5 | `ScheduleCenterStub.cs:106-135` | ✅ `:106 FindObjectsOfType<StorageComponent>()`（**全场景所有仓**）⇒ `:118 !IsReadyToHarvest() ⇒ continue`（判据 ＝ `TotalCount > 0`）⇒ `:131 carry = GetCarryAmount()` ⇒ `:132 batches = ceil(TotalCount/carry)` ⇒ ⭐ **无任何标签/仓类型过滤** |
| 6 | `StorageComponent.IsReadyToHarvest:270` | ✅ `=> TotalCount > 0` ⇒ ⭐ **"有内容"即"可搬"** |
| 7 | `BehaviorExecutor.cs:175` | ✅ `sc.HarvestCarry()`（`ScheduleCenterStub` 派工后的落点动作） |
| 8 | ⭐ `TaskScheduler.cs:672 HarvestCarry` | ⭐ **本端补第 2 路**：`ExecuteCompletion` 的 `Transport` 兜底（`st != null && carryInv == null` ⇒ 无背包组件 ⇒ **直接入国库**）⇒ 同落点 ⇒ 同丢 |
| 9 | ⭐⭐ `StorageComponent.Harvest:273-286` ← `BuildingPanel:401-408` | ⭐ **本端新发现第 3 路（报告未列）**：`:405 if (!storage.IsReadyToHarvest()) return;` ⇒ `:406 storage.Harvest()` ⇒ `:276-283` **遍历所有类型 ⇒ 全量 ⇒ `_items.Clear()`** ⇒ ⚠️ **玩家点"收"＝井仓的水一次全没**（比每批 ≤10 更严重）；且 `BuildingPanel:187 _harvestButton.SetEnabled(IsReadyToHarvest())` ⇒ **按钮可点** |
| 10 | `Building.cs:1511-1545` 广告顺序 | ✅ ② 生产（`:1515 !sched.HasWorkerAssigned(this)`）⇒ ③ 搬运（`:1524 TotalCount > 0 && stored >= capacity×transportThreshold`）⇒ ④ 搬水（`:1548`）⇒ ⭐ **顺序拦截成立**（§二-2） |
| 11 | `need` 删（件 3） | ✅ `HaulToSiteArgs.need` **仍在**（`:88`）· `HaulWaterArgs.need` **已无** ⇒ 与本端 `D808` 要求一致 ✓ |

---

## 一 总判

⚠️ **补证单未达成目标**（判据 1 ❌ 长局搬水往返未复现）＋ ⭐⭐ **发现阻塞级缺陷**（⚠️ **`D810` 勘正：定性 ＝「静默转箱 + 空转」，⛔ 非「资源销毁」**）⇒ **`F-1` 继续 ⛔ 不销号** ⇒ 立 **止血批**。

**须先肯定报告的三点质量**：① ⭐ **停手报裁**（⛔ 未自行改生产码变绿）✓ ② ⭐ **同帧隔离铁证**（井仓 12→2 ／全库 Water 12→2 ⇒ 差 −10 ⇒ 证明"未进入任何收水仓"）—— 这是**本单最有价值的读数** ③ ⭐ **时基假设如实裁定**「未证实也未推翻」（⛔ 未强行归因）✓

---

## 二 裁决

### 1. ⭐⭐ 阻塞级缺陷：水被静默丢弃 —— **立 `U-15` · 止血批**

**缺陷链（本端三路独立坐实）**：

```
路 1（报告已报）ScheduleCenterStub:118（IsReadyToHarvest = TotalCount>0 · 无过滤）
                 ⇒ BehaviorExecutor:175 sc.HarvestCarry()
路 2（本端补）  TaskScheduler:672 st.HarvestCarry()   ← Transport 完成的"无背包兜底"
路 3（⭐本端新发现）BuildingPanel:405/406 ⇒ StorageComponent.Harvest()  ← 玩家手动收 · 全量清仓
                 共同落点 ⇒ TakeOut(type,n) ＋ ModifyResource(type, true, n)
                 ⇒ 国库 Deposit ⇒ Accepts(Water)=false ⇒ 返 0 ⇒ ⚠️ **`D810` 勘正：返 0 ⇒ 触发 `Deposit:110-111` overflow ⇒ 装箱（⛔ 非消失）**
```

**为什么 `F-1` 才暴露**：`HarvestCarry`／`Harvest` **在 `F-1` 前就存在**，但当时**水在 `WaterNetwork`** ⇒ 水井仓是**空仓** ⇒ `TotalCount = 0` ⇒ 不被任何一路选中 ⇒ ⭐ **水仓化后才暴露** ✓（报告判定正确）
⇒ ⭐ **与 `L-45`（退役/变更一个掩盖物后暴露）同族** ⇒ 立 **`L-59`**（见 §三）。

**止血裁法 —— 甲′（通用可收性判据 ＋ 落点保护 · 双保险）**：

| 案 | 裁决 |
|---|---|
| 甲 仅搬"可入国库"资源 | ⭐ **准 · 但采"通用判据"形态**（见下甲′） |
| 乙 `IsReadyToHarvest` **排除 Water** | ⛔ **否决** —— 硬编码水 ⇒ 语义扭曲（水井仓里若将来有别的资源怎么办）＋ 治标；且**同一判据被 `Harvest()`／`BuildingPanel` 共用** ⇒ 排除后"玩家收井仓"的语义自相矛盾 |
| 丙 `M1-G` 的 `HarvestCarry` 删除**提前** | ⛔ **否决本批提前** —— `M1-G` 未开工，提前删会**打断 `ScheduleCenterStub` 链**（`BehaviorExecutor:175` 直接崩）；应作**根除项**留 `M1-G` |
| ⭐ **甲′（本端裁）** | **① 判据层**：`IsReadyToHarvest()` 从「`TotalCount > 0`」改为「**存在可入国库的内容**」（逐资源问国库 `CanAccept`）⇒ 水井仓只有水 ⇒ **判据假 ⇒ 三路都不选中** ✓ **② 落点层**：`HarvestCarry`／`Harvest` 的 `TakeOut` 前加保护（**能收多少拿多少**，⛔ 不先拿后丢）⇒ 双保险 |

⚠️ **甲′ 的连带影响（须一并确认）**：`BuildingPanel:187` 收按钮会**对纯水井仓变灰**（⇒ ⭐ **这正是正确行为**：收了会丢）。

### 2. ⭐ 措辞勘正：不是"无声"，是"**静默丢失**（无告警 · 无回滚）"

> ⚠️ **`D810` 再勘正**：本节"丢失"一词亦须限缩 —— 水**未丢**（转成掉落箱）。仅"无告警 · 无回滚 · 调用方已 `TakeOut`"三点成立。

`ModifyResource:244` **有普通日志**（`Debug.Log(... {moved} ...)`）⇒ ⚠️ 报告写"无声销毁"**不准确**；精确表述 ＝ **有普通日志（`moved` 会显示 0）但⛔ 无告警、⛔ 无回滚，调用方已 `TakeOut`** ⇒ 定为「**静默丢失**」。
⇒ ⚠️ 这不是吹毛求疵：**"有日志"会让人以为可查**（实际需人肉看 `+0`）⇒ 定性影响止血优先级与后续监控方案。

### 3. `WaterHaul` 1× 下 0 次派发 ⇒ 真因拆三层（报告措辞须勘正）

| 层 | 内容 |
|---|---|
| **L1 · 结构（⭐ 要点）** | `TryAdvertiseTask` 是**顺序 return**：② 生产（`:1515 !HasWorkerAssigned(this)`）⇒ ③ 搬运 ⇒ ④ 搬水 ⇒ ⭐ **农场无工人时 ② 必中 ⇒ 永不到 ④** ⇒ ∴ 报告「**搬水分支要求农场已有工人**」**结论对**，但 ⚠️ **理由不是显式守卫，而是 ② 的顺序拦截**（措辞须勘正） |
| **L2 · 占用** | ⭐ 而工人被**路 1**（`ScheduleCenterStub` 派水井仓）锁在水井旁（报告读数：距井 0.13／距农场 1012）⇒ 农场长期无工人 ⇒ ⇒ **与 L1 形成死结** |
| **L3 · 时基** | 1× 下 180 真实秒 ＝ **180 游戏秒**（vs 15× ＝ 2700 游戏秒）⇒ 推进量确实小（报告解释**部分成立**） |

⇒ ⭐ **3b「卡在 `MovingToSource`」本轮两次长局均未复现** ⇒ 时基假设**未证实也未推翻**（本端认可该结论）；⚠️ **但 L1／L2 才是主因**，时基只是放大器 ⇒ 下一批应**先解 L2**（路 1 止血后工人即释放）再复跑。

### 4. `U-14` 严重度：⏸️ **暂不降级**

报告倾向"自愈"（15× 下同工人 4 次重派 ＋ 最终有水到农场）⇒ ⚠️ 但**证据不全**（读数 B 因 Console 被挤未取到 · 探针缺陷已如实列报）＋ ⭐ **且 L2 与本项交织**（工人被路 1 占用 ⇒ 读数不可比）⇒ **裁：维持 `U-14` 现状**，**止血批后复跑长局再定严重度**。

### 5. 件 3 `need` 删 ✅ **通过**

`:88 HaulToSiteArgs.need` **仍在**（有消费方 · `M1-C` 阈值拦截）／`HaulWaterArgs.need` **已无** ⇒ ⭐ **未误删**（本端重点项）✓ 注释半句同步删 ✓ 编译 0 error ✓

### 6. 件 2 回归 ⛔ 未跑 ⇒ **随止血批补齐**（本端准其停手）

---

## 三 ⭐ 新立教训

**`L-59` ＝ 「把一个新资源放进旧容器」时，必须回扫所有以「容器非空」为条件的既有通用逻辑。**

`IsReadyToHarvest() => TotalCount > 0` 是一条**通用"有内容就搬"**逻辑（三路共用 · 消费者 `ScheduleCenterStub`／`TaskScheduler:672`／`BuildingPanel`）。
`F-1` 把水放进**普通仓**后 ⇒ 该逻辑**立刻把水卷入旧去向（国库）** ⇒ 而国库不收水 ⇒ **丢弃**。
⇒ ⭐ 与 `L-45`（**退役**某折扣 ⇒ 暴露被它掩盖的缺陷）**方向相反** ⇒ 二者合为**「掩盖物两侧」**：`L-45` 查「**退役留下什么**」，`L-59` 查「**新增被谁捡走**」；＋ `E1`（新增需要什么持续条件）⇒ **三足**。
⇒ **操作句**：新增资源/条目进**既有通用容器**时 ⇒ **① 列出所有"以容器非空/非满为条件"的既有逻辑 ② 逐条问"它会把这个新东西送去哪" ③ 该去向若不收 ⇒ 必先加保护**。

---

## 四 落账

- 本裁决书新建
- 台账 **§一百一十三**
- `_编号登记.md`：`HH.319` ⇒ **补证已交付 · 发现阻塞级缺陷 · 待止血**
- `_任务队列.md`：`M1-F` 行更新；新插 **`U-15`**（水被静默丢弃 · 止血批 · ⛔ 阻塞）
- `_当前快照.md`：`D809`
- `_策划教训库.md`：新立 **`L-59`**
- `09_资源与仓库.md`：§4.3 D 组加注（**水仓化的连带面：三路"有内容就搬"逻辑须带可收性判据**）；§10 收口清单加注（`HarvestCarry`/`Harvest` 双路归 `M1-G`）

---

## 五 状态与下一步

| | |
|---|---|
| `HH.319` `F-1` | ⛔ **不销号** —— 补证未达目标 ＋ **新增阻塞级缺陷 `U-15`** |
| ⭐ 立即（止血批） | **甲′**：`IsReadyToHarvest` 改通用可收性判据 ＋ `HarvestCarry`/`Harvest` 落点保护（⛔ 不先拿后丢） |
| 随后 | ① 止血批验收 ⇒ ② 复跑长局（验 L2 解除 ＋ `U-14` 定级 ＋ 时基对照）⇒ ③ 回归补齐（`HH315`/`HH317`/`M7`/`HH316 §E~§H`）⇒ ④ `F-1` 销号 |
| 根除（归 `M1-G`） | 删 `HarvestCarry`／`Harvest` 直通国库（`09#40`）＋ `ScheduleCenterStub` 与 `TaskScheduler` **双链合一**（`#41`/`#42`）—— ⚠️ 本批已证明双链**不只是冗余，而是丢资源** |
| `M1` 完成度 | **5/7**（`F` 受阻 · `G` 未开） |

**▶️ 下一步**：出 **`U-15` 止血批**施工提示词（⚠️ 建议与回归补跑**同批**，一次收口）。

---

## 勘正（`D810` · 2026-09-21）

> ⚠️ 本节对 `D809` 的 **`U-15` 机制定性**作实质勘正（⛔ 不改止血方案本身）。

### 一 · 勘正内容：**「销毁」⇒「转箱」**

`D809` 及执行端报告均判「水被**静默销毁**（资源损失）」。本端补核 `TreasureVault.Deposit` 全文 ⇒ **不成立**：

```
TreasureVault.Deposit:109   int added = _container.Add(Water, amt);   ⇒ Accepts=false ⇒ added = 0
:110-111                    int overflow = amt - added = amt > 0
                            if (overflow > 0) SpillToChest(Water, overflow);   ⭐ 溢出装箱
SpillToChest:116-124        if (amount <= 0 || ChestManager.HasInstance == false) return;
                            ⚠️ HasInstance ⇒ Singleton.Instance（`Singleton.cs:37-46` lazy 自动创建）⇒ 恒 true
:123                        ChestManager.Instance.SpawnChest(cell, pack);
SpawnChest:72               if (pack.IsZero) return null;   ⇒ {Water:n} 非零 ⇒ ⭐ 真创建箱子
```

⇒ ⭐ **水的实际去向 ＝ 掉落箱**（落主城格），⛔ **不是"消失"**。

### 二 · 为何"同帧隔离铁证"不成立（`L-48` 家族）

执行端铁证读数为「井仓 12→2」＋「**玩家国全库** Water 12→2」⇒ 差 −10。⚠️ 但"全库"的口径是
**`StorageComponent` 合计** ⇒ ⛔ **未覆盖「箱子」这条产出面** ⇒ 「差 −10」**同样可由"进了箱子"解释**
⇒ 该隔离**不能推出"销毁"**。

### 三 · 修正后的真实危害（⚠️ 仍为阻塞级）

| 维度 | 修正后 |
|---|---|
| 资源 | ⛔ **不丢**（转箱） |
| ⭐ **空转** | 掉落箱自广告搬运任务（`U-2` 链 A）⇒ 工人搬回 ⇒ `FindNearestAvailable(Water)` 命中**水井/农场**（唯二收水仓）⇒ 水回井仓 ⇒ **再次被 `HarvestCarry` 选中** ⇒ **无限循环** |
| 工人 | ⭐ **永久占用** —— 这正是执行端观测到「工人被拉在水井旁（距农场恒定）」的直接成因（与 `U-14` 同源） |
| 箱子 | 主城格堆积 ⇒ `EnforceCellLimit` 上限 4 ⇒ 洒落邻格 ⇒ **箱子遍地** |

⇒ 严重度**维持阻塞级**（水**永远到不了农场** ＋ 工人被占死），但**定性由"资源损失"改为"资源空转"**
⇒ ⚠️ 影响 `M1-G` 根除方案的表述（`#41`/`#42` 双链合一 ≠ "防丢资源"，而是"防空转"）。

### 四 · 止血方案（甲′）**不变** ✓

判据层用 `CanAccept(type) > 0` —— `CanAccept:147` 首步 `if (!Accepts(type)) return 0;` ⇒ **对水仍判假** ✓
落点层"能收多少拿多少"同理 ⇒ **方案无须改**，仅**定性/严重度/判据面**更新。

### 五 · ⭐ 新立 `L-60`：一个返回值承载两种语义 ⇒ 下游只能猜

`StorageComponent.Add` 返 `int added`，把两种**语义完全不同**的失败压成同一个 `0`：
① **标签不收**（`Accepts=false` · 设计决定）② **容量不足**（`CanAccept=0` · 临时状态）

⇒ `Deposit:110` 只能把**两者都当"溢出"** ⇒ `SpillToChest` ⇒ 对"标签不收"的资源**装箱是错的**
（装箱 ⇒ 搬运链搬回 ⇒ 循环）。

⇒ **通则**：**「拒收」必须与「满」区分** —— ⛔ 不得用一个返回值承载两种语义；若必须合并 ⇒ 须在
**调用方**分层（先问 `Accepts`，再问 `CanAccept`）。

### 六 · 补证要求（并入 `U-15` 止血批）

⭐ 止血批**新增判据**：**箱子侧读数** —— 止血前 `ChestManager.Count` 与箱内容物合计（须能读出"水在箱里"）
／止血后应为 **0**。⚠️ 这是"止血是否真止住"的**唯一可靠判据**（仓侧读数两者都对）。
