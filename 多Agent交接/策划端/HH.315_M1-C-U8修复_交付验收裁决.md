# HH.315 · `M1-C` · `U-8` 补丁批 —— 交付验收裁决

- 裁决日：2026-09-21（`D801`）
- 被裁文书：`多Agent交接/执行端/HH.315_M1-C-U8修复_交付报告.md`
- 被验代码：**`8d32e0fc`**（三文件 `+131/−8`）＋ `f8f41a6b`（报告 2 文件 ＋ 补上轮评估报告）＋ `7532a097`（报告回填）
- 验收方式：判据三直读（契约原文 ／ 代码 `file:line` 实读 ／ 档位·日志直读）＋ 独立复算 ＋ diff 红线核验
- **总判**：✅ **通过 · 准予销号** —— ⭐ **`M1-C` 待修项清零 ⇒ ✅ `M1-C` 销号**（见 §六）
- ⚠️ 含 **1 条列报勘正**（`O1` 的定性）＋ **3 项须裁**（`O1`／`O3`／`O4`）

---

## 〇 验收集（本端独立取证 12 项）

| # | 本端实读 | 结果 |
|---|---|---|
| 1 | `git show --stat` 三 commit | ✅ 与报告逐行一致（代码 `+131/−8` ＝ `Building.cs` 100／`TaskScheduler.cs` 32／`BuildingSaveData.cs` 7） |
| 2 | `diff` 搜 `PaidStageCost`／`CurrentStageCost`／`GetRepairCost`／`BuildRefundPack`／`IsRefundResource`／`SumCostOf` | ⭐ **六名全零命中** ⇒ 「⛔ 未改退款公式」**成立** |
| 3 | `diff` 搜 `DropSiteStoreToChest` | ⚠️ 命中 3 处 —— 逐处核**全在注释内**（`:74`／`:169`／`:255`）⇒ **零代码改动** ✅ |
| 4 | 三文件行尾（工作副本） | ✅ `Building.cs` CRLF=0／`TaskScheduler.cs` CRLF=0／`BuildingSaveData.cs` CRLF=0（纯 bare-LF · 无 BOM） |
| 5 | 原始日志 `Valley Rampart/Logs/u8/drvA\|B\|C.log` | ✅ 存在（未跟踪）· 关键读数与报告**逐条一致** |
| 6 | 件 1 顺序约束 | ✅ `Demolish()` `:890 _demolishing=true` → `:895 UnregisterSiteStore()` → `:897 EnsureRegistered()` |
| 7 | `EnsureRegistered` `:1530-1533` | ✅ 无 state 条件 · ⛔ 未新增 `IsRegistered` 查询口（符合提示词建议）· `RegisterWithTaskScheduler:1520` 仍 `Active` 语义 |
| 8 | 件 3 硬化 `:579` | ✅ 在 `if (!_awaitingMaterials) return;`（`:580`）**之前** |
| 9 | 件 6/7 `:1264-1265`／`:1279` | ✅ 清态在 `state=Ruined`（`:1257`）之后 · 注销在 `Unregister(this)`（`:1273`）之后 · 注释写明「留仓」论证 |
| 10 | 件 5 `:659`／`:667` | ✅ 用静态谓词 `IsDemolishTask`（⛔ 不捕获 ⇒ 无每帧分配）；新重载 `TaskScheduler.cs:166-177` 带 `filter != null` 守卫 |
| 11 | 件 4 `TaskScheduler.cs:1319-1323` | ✅ 活读 `da.target.DemolishDuration()` ＋ `ds > 0f` 兜底（照 Gather 形制） |
| 12 | 件 9 三处 | ✅ `BuildingSaveData.cs:68-73` 尾插零 bump ／ `SaveState:1083-1084` ／ `LoadState:1165-1166`（在 `_demolishProgress` 后）｜`InProgressStageIsBuild:1006-1012` 注释改为「旧档兜底」**且保留失效条件警告** ｜ `N-3:1117-1124` 勘正为「构造法样本」 |

---

## 一 · 十件落码 —— ✅ 全落且逐件合规

| 件 | 落点 | 本端核 |
|---|---|---|
| 1 | `Building.cs:897`＋`:1530` | ✅ 案甲正确 · 顺序约束满足 · 头注写明「`IsValid` 三项 `\|\|` 依赖它」 |
| 2 | `Building.cs:1206`（`LoadState` 尾） | ✅ ⛔ 未改 `BuildingFactory:189`（职责不串层） |
| 3 | `Building.cs:895`＋`:579` | ✅ 注销不动 `_items` · 硬化在首行 |
| 4 | `TaskScheduler.cs:1319-1323` | ✅ **活读**（⛔ 非快照 —— 符合本端 `Q4` 裁） |
| 5 | `TaskScheduler.cs:166-177`＋`Building.cs:659/667` | ✅ **形态甲**（本端准其报选 · 见 §五 `O6`） |
| 6 | `Building.cs:1264-1265` | ✅ `E1-a` 互斥口径 |
| 7 | `Building.cs:1279` | ✅ `E1-b` 注销 ＋ 留仓（见 §五 `O3`） |
| 8 | `Building.cs:870-871` | ✅ `state != Ruined` ＋ 注释写明「顺手封陈旧面板路径」 |
| 9 | `BuildingSaveData.cs:68-73`＋`Building.cs:1083/1165` | ✅ 尾插零 bump · 兜底保留 |
| 10 | 注释 4 处 | ✅ 四条逐字落码（⭐ `N-3` 勘正做得比要求更完整 —— 连"补注册为必需"的依据也写入） |

---

## 二 · 判据复核（9/11 有直接读数）

### 2.1 有读数者 —— 本端逐条核原始日志

| 判据 | 原始日志关键行（本端实读） | 判定 |
|---|---|---|
| 1 首次建造·投料中 | `ROOT-READING(before Demolish): bodyInSources=False` → `AFTER Demolish(same frame): IsDemolishing=True bodyInSources=True siteStoreInSources=False storeIsValid=True` → `DONE-DEMOLISH frames=70 rounds=1 lastProg=0.994` | ✅ ⭐ **鉴别力成立**：改前根因（不在册）与修后（在册）**同一样本直接对照** |
| 4 废墟重建 | R2 `after StartRebuild: Constructing awaiting=True bodyInSources=False` → `DESTROYED frames=127 gameSec=6.24 rounds=1` ／ R1（料齐）`frames=148 gameSec=7.14 rounds=1` | ✅ |
| 5 读档续拆 | `after-load: state=Constructing IsDemolishing=True bodyInSources=True` → `***CONTINUATION CONFIRMED*** 0.0000 -> 0.0147` | ✅ 改前＝永久冻结 |
| 6 C 面 | `CRIT6: siteStore inSources frames during demolish = 0 (expect 0)` ＋ `storeIsValid=True` | ✅ 源注销 ＋ **内容安全**（两者同时成立） |
| 7 D 面 | 三样本 `rounds=1`（改前应 ≥3）· R2 `6.24s ≈ 标称 6.00s` | ✅ **轮次鉴别力成立** |
| 8 E1 面 | `R3 after TakeDamage: state=Ruined IsDemolishing=False` → `***REBUILD COMPLETED*** state=Active lv=1 hp=100/100` | ✅ 改前＝重建永久冻结 |
| 9 E3 面 | `state=Ruined CanDemolish=False`（驱动 C 四处） | ⚠️ 后半（陈旧面板拦截）以**静态链替代**（无头驱动不可点 UI）⇒ 本端认可（见 §四） |
| 10 U-5 面 | `flagPU=True` ⇒ `level=2`；`flagPR=True` ⇒ `refundChest {Wood:6}` | ✅ 见 2.2 复算 |
| 11 存档往返 | 两新字段 `flagPU`／`flagPR` 存活 ＋ 冒烟 §F 既有字段往返 | ✅ |

### 2.2 ⭐ 本端独立复算 2 处（⛔ 不采信转述）

**判据 10 修复面** —— `{Wood:6}`：
```
PaidStageCost（Building.cs:915-945）：isRepair = inProgress && _pendingRepair = true
 ① !isBuild（_pendingRepair ⇒ InProgressStageIsBuild 返 false）⇒ pack += def.cost = {Wood:4}
 ③ inProgress && paid && !isBuild && !isRepair ⇒ isRepair=true ⇒ **不 n++** ⇒ 不加升级 ✓
 ④ isRepair && paid ⇒ pack += pack × RepairCostRatio(0.5) = {Wood:4} + {Wood:2}
 ⇒ {Wood:6} ✓ 与实测吻合
```
**判据 10 改前口径** —— 报告称「改前应 `{Wood:14,Stone:6}`」：
```
_pendingRepair 丢失 ⇒ isRepair=false；兜底 SamePack(_siteNeed, def.cost) 为 false ⇒ isBuild=false
 ① !isBuild ⇒ pack += def.cost = {Wood:4}
 ③ n = max(0, level-1) = 0；inProgress && paid && !isBuild && **!isRepair(true)** ⇒ n++ ⇒ n=1
    ⇒ 加 levels[0].upgradeCost = {Stone:6, Wood:10}
 ⇒ {Wood:4+10, Stone:6} = **{Wood:14, Stone:6}** ✓ 复算吻合
```
⇒ ⭐ 该鉴别力声明的**两侧预期均可复算**，**成立**。

---

## 三 · 红线核验

| 项 | 本端核 |
|---|---|
| 改动面 ＝ 3 文件 | ✅ `git show --stat` 逐行一致；报告 2 文件另计 |
| ⛔ 未改退款公式 | ✅ **六名零命中**（见 §〇 第 2 项）｜`DropSiteStoreToChest` 三处**全在注释** |
| 行尾守恒 | ✅ 三文件纯 bare-LF · CRLF=0 · 无 BOM |
| 未 push ／ 未代提交账本 | ✅ 本端工作区账本零改动 |
| 探针形态 | ✅ ⭐ **零探针文件**（MCP 临时驱动 · 收尾卸钩）· 日志 `Logs/u8/` 未入库 —— **如实声明 ＋ 未越红线**，本端认可 |
| 收尾三态 | ✅ 残留 0 ／ `isDirty=False` ／ 根对象 51 ／ `ExitTestRun` 已调 |

---

## 四 · 未落地项裁定

| # | 项 | 裁定 |
|---|---|---|
| 判据 2（首次建造·**料齐施工中**） | 报告以**同态覆盖**替代（由判据 4 的 R1「料齐 paid 态」覆盖） | ✅ **准**：注册落点**唯一** ＝ `Demolish()`，与 state 子型无关；且 R1 恰好覆盖关键差异格（`_awaitingMaterials=false` ＋ `!_demolishing` ⇒ `IsValid=false` ⇒ 正是"会被 `Tick:223` 清源"那一格）⇒ 覆盖点选得对 ⛔ 不必补跑 |
| 判据 3（升级·投料中） | ⛔ 未跑（**零鉴别力回归件**） | ✅ **准** —— 与 `D800` 一致（该子型改前已能拆 ⇒ ⛔ 不得当 `U-8` 判据）⇒ 并入下一回归批 |
| 判据 9 前半 | 陈旧面板拦截以**数据栏读数 ＋ 调用链静态证据**替代 | ✅ **准**：`CanDemolish` 是同源判据，`BuildingPanel:393` 有 `if (!_target.CanDemolish) return;` ⇒ 静态链完整；无头驱动确实不可点 UI ⇒ 如实声明即合规（符合 `L-51` 的"绕过须声明"精神） |
| 回归 ① `§B`（真下单搬运）未在 120s 窗内落地 | 报告归"环境/窗口抖动"（`dt=2.28s/帧`）· 本片**零改**搬运链 | ⚠️ **要求复跑**，⛔ **不阻塞销号**（既有冒烟判据 · 非本片新增；同日基线轮已通过） ⇒ 记挂账 |
| 回归 ② `2_20B_M7` ／ ③ 1× 时基 | ⛔ 预算外 | ✅ **延后**，⛔ 不阻塞 ⇒ 记挂账 |

---

## 五 · 须裁三项

### `O6` · 件 5 形态甲报选 ⇒ ✅ **准**

**形态甲**（保留结构：新重载 ＋ 静态谓词 · 调用面 **1 处** · ⛔ 零每帧分配 ＋ `k` 分支显式保留加注）。
本端核：`:166-177` 的 `filter != null` 守卫正确 · 谓词 `IsDemolishTask:667` 为 `static` ⇒ **Roslyn 缓存委托 ⇒ 无每帧分配** ✓
⛔ 未走形态乙（`n≡1` 硬编码 ⇒ 使 `k` 分支失去实现意义 ＋ 日后放开多工人需回改）⇒ **判断正确**。⭐ 且"调用面 ＝ 1 处"已写入注释（符合「新建接口须报调用面」纪律）✓

### ⭐ `O1` · `MissingReferenceException` ⇒ ⚠️ **列报定性须勘正** ＋ ✅ **准立 `DZ-4`**（优先级高）

**报告原文**：「既有 `DamageSystem` 待处理攻击表在攻方建筑被销毁后仍访问其位置（`DamageSystem.cs:284-301` ⛔ **无 Unity 假空守卫**）」

**本端实读推翻「无守卫」** —— `DamageSystem.cs:273-282` **有**一个假 null 守卫：

```csharp
private void ExecuteAttack(IDamageable attacker)
{
    // HH.92/T13 收尾：attacker 已销毁（Unity 假 null，如被拆的箭塔）防御——…
    // （THD R3 实测 4586 次/轮；玩家侧拆塔瞬间同样可触发）。
    if (attacker == null)          // ⚠️ attacker 的**编译期类型 ＝ IDamageable（接口）**
    { _registrations.Remove(attacker); return; }
```

⭐ **真根因**：`IDamageable` 是**接口**（`IDamageable.cs:15`）⇒ 接口类型的 `==` 走**引用比较**（`object.operator==`），**⛔ 不会调用 `UnityEngine.Object` 重载的假 null 检查** ⇒ 被销毁的 `Building`（`m_CachedPtr==0` 但托管引用非 null）**判不出来** ⇒ 继续走到 `:301 attacker.GetPosition()` ⇒ 抛异常。
⭐ 同一问题存在于 `:293 target == null`（`target` 亦为接口）。

⇒ ⭐ **性质重定义**：不是"缺守卫"，而是 ⭐ **「守卫存在但判据在当前类型下不生效」** —— `HH.92/T13` 那次修复自陈「收尾」，实际**未生效**（同类注释误导）。⇒ 立 **`L-53`**。

**裁定**：
- ✅ **准立 `DZ-4`**（既有缺陷 · 与 `U-8` **零行交集**）⇒ ⛔ **不阻塞 `M1-C` 销号**
- ⭐ **优先级：高** —— 理由：异常在 `ExecuteAttack` 内抛出 ⇒ ① 该注册条目**永不清除**（异常跳过 `Remove`）⇒ 每帧重复抛；② 日志污染会**掩盖其他缺陷**（本次验收就吃过"日志刷屏"的干扰）；③ ⚠️ 且该帧攻击结算被中断
- ⚠️ **本端未独立复现「≥200 条」计数**（报告称堆栈已取证）⇒ 如实声明；`DZ-4` 派评估时要求给**计数 ＋ 调用点是否在 `try/catch` 内**两项读数
- ⚠️ 修法方向（⛔ 本端不写码 · 仅供评估）：接口类型判假 null 须转 `UnityEngine.Object`（如 `attacker as Component == null` 或 `attacker is Object o && o == null`）

### `O3` · 件 7 属防御性收口 ⇒ ✅ **认可**（且复核其判断成立）

**本端独立复核"生产不可达"成立**：
- `Building.TakeDamage:1182` 首守卫 `if (state != BuildingState.Active) return;` ⇒ 投料/施工态（`Constructing`）**不可受伤**
- ⚠️ 另加一层（报告未提）：**件 3 已在 `Demolish()` 里注销工地仓源** ⇒ 「拆除中被打毁」路径下仓源**已不在册** ⇒ 「`Ruined` × 仓源在册」**双重不可达**
⇒ ✅ 件 7 确为**不变量式加固**（若未来某路径让 `Ruined` 时仓源仍在册，它是最后一道防线）⇒ **保留** ＋ 注释已写明机制 ✓

### ⭐ `O4` · `Ruined` 清理出口 ⇒ ⚠️ **报告措辞须勘正** ＋ ✅ **立 `U-10`**（挂 `M1-G`）

**报告措辞**：「`Ruined` 清理出口此后只剩「重建」… 本片**新增副作用**」

⚠️ **本端勘正**：这不是本片新增的行为 —— `BuildingPanel.cs:79`（`Ruined` 分支）**本来就隐藏拆除按钮**（`Abandoned` 同理 `:94`）⇒ **UI 层早已无出口**。本片件 8 只是**把数据层对齐到 UI 层**（并使 `E1` 的陈旧面板路径失效）。
⇒ ⭐ 准确表述：**「废墟无清理出口」是既有口径（UI 隐藏）；本片在数据层固化了它。** ⚠️ 定性差别重要 —— 前者会被误读为"本片引入的回归"。

**裁定**：
- ✅ **承认该缺口真实**（废墟永久占格 ＋ 阻挡 ⇒ 若无法重建则永久占地，是真实玩家困境）
- ⭐ **⛔ 不在本片补** —— 理由：① 补出口的正确形态是「给废墟一条**独立的**清理/拆除通道」，⛔ 不是"把 `Ruined` 加回 `CanDemolish`"（那会**自相矛盾地撤销件 8**）；② 属**新增玩法出口**（非缺陷修复）；③ 归属天然匹配 **`M1-G`（清理）**
- ⇒ 立 **`U-10`**：废墟清理出口（挂 `M1-G`）· ⚠️ 派工前置：须**先只读评估**（口径空缺：废墟能否拆／拆了给不给退还（`09` §16.2 全退口径是否适用）／废墟是否算"玩家建造"）· ⚠️ 且若放开须回查 `E1` 互斥口径

---

## 六 · 总判

### ✅ `U-8` 补丁批 —— 通过

十件全落且逐件合规 ／ 判据 9·11 有直接读数（核心判据 1 取得「改前根因的直接对照读数」）／ 未落地项均有正当理由 ／ 红线自证成立（**diff 六名零命中** ＝ 退款公式确未碰）。

### ⭐ `M1-C` —— ✅ **准予销号**（按 `L-49` 回查批次账）

**`M1-C` 全部待修项清单**（逐项回查）：

| 项 | 状态 |
|---|---|
| `U-1`（退还/修复费口径错配） | ✅ `D796` 销号（`75b5f3c4`） |
| `U-3`（修复费复利） | ✅ `D796` 销号 |
| `U-4`（投料中态双重退还） | ✅ `D797` 销号（`edaed15d`） |
| 件 3（退还随本体掉箱）依赖 `U-2` | ✅ `D799` 闭合（`cc2be8dc` 链 A 上线） |
| **`U-8`**（非 Active 态拆除卡死） | ✅ **本批销号**（`8d32e0fc`） |
| **`U-5`**（`_pending*` 未入档） | ✅ **本批销号**（同批 · 见 §一 件 9） |
| **`E1`**（闩锁 → 重建冻结 ＋ 吞料） | ✅ **本批销号**（件 6/7） |
| `E3` 口径分裂 | ✅ 本批销号（件 8） |

⭐ **唯一遗留 ＝ `DZ-1`**（`WarehousePanel` 容量列跨行合计虚增 · 当前**不可见不可达**）—— ⚠️ 它此前记账为「归 `M1-C`」，而 `M1-C` 面板域已收口 ⇒ ⭐ **本端裁：转挂 `M1-G`（清理）**（`DZ-1` 属**面板显示层**合计口径，⛔ 非建筑功能层；且"通用仓上线才成真" ⇒ 与 `M1-G` 同域）⇒ 转挂后 **`M1-C` 批次账干净** ✓

⇒ ⭐ **`M1-C` 销号 ⇒ ✅ 准**。`M1` 模块完成度 **3/7**（`A` ✅ ／ `B` ✅ ／ **`C` ✅**）。

### 观察项（新增）

| # | 项 |
|---|---|
| `O9` | 回归 `§B` 搬运窗抖动（既有冒烟判据 · 待复跑） |
| `O10` | `2_20B_M7` ／ 1× 时基跑批（预算外 · 待批） |

---

## 七 · 落账

- 本裁决书（新建）
- `河谷防线开发计划书具体内容/测试基线台账.md` ⇒ **§一百零五**（含 `M1-C` 销号）
- `多Agent交接/_编号登记.md` ⇒ `HH.315` 行 ⇒ ✅ **已交付并验收 · 销号**
- `多Agent交接/_任务队列.md` ⇒ `M1-C` 行 ⇒ ✅ 销号 ／ `U-8`／`U-5` 行 ⇒ ✅ ／ **`DZ-1` 转挂 `M1-G`** ／ 新插 **`DZ-4`**／**`U-10`**（⚠️ MIXED 行尾 ＋ 1 NUL ⇒ python 二进制按行改）
- `多Agent交接/_当前快照.md` ⇒ `D801` 段
- `多Agent交接/策划端/_策划教训库.md` ⇒ 新立 **`L-53`**
- `最高优先级文档/09_资源与仓库.md` ⇒ §16.2 补「`Ruined` 态不可拆 ＋ 清理出口待定」口径

---

## 八 · 状态

| | |
|---|---|
| `U-8` 补丁批 | ✅ **通过**（`8d32e0fc` 三文件 `+131/−8`） |
| ⭐ **`M1-C`** | ✅ **销号**（`D801` · 待修项清零 · `DZ-1` 转挂 `M1-G`） |
| `M1` 模块 | **3/7**（`A` ✅ ／ `B` ✅ ／ **`C` ✅**）⇒ 余 `D` 掉落战利品／`E` 金币／`F` 水与自交互／`G` 清理 |
| `DZ-4`（新立 · 高优） | `DamageSystem` 假 null 守卫**接口类型失效** ⇒ `MissingReferenceException` |
| `U-10`（新立 · 挂 `M1-G`） | 废墟清理出口（⚠️ 先只读评估） |
| ▶️ 下一步 | ① **开 `M1-D`（掉落战利品）**（⭐ `M1` 下一子片 · 承接 `U-6`／`O-2`／`O-3`／`O-4`）② `DZ-4` 只读评估（高优）③ 回归 `§B` ＋ ② ③ 补跑 |
