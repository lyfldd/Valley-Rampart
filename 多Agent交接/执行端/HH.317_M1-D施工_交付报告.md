# HH.317 · M1-D「掉落与战利品」· 施工交付报告

- 任务号：HH.317 · `M1-D`（承 `D802` 报裁全裁 ＋ `D803` 补取证全采 ⇒ 口径齐备）
- 本轮性质：**施工批**（件 1~件 10 全落地）＋ 同局冒烟取证（判据 1~12）＋ 本报告
- 日期：2026-09-21
- 口径真源：HH.317 施工任务书 §件1~件10；`09#45`/`#49`/`#57`/`#58`/`#59`/`#60`；`D802`/`D803` 裁决
- 交付面：生产码 **13 文件** ＋ Editor 探针 **5 文件**（含新建 HH317 探针 ＋ meta）＋ 交付报告 1 文件

---

## 〇 结论速览

| 项 | 读数 | 判定 |
|---|---|---|
| 编译 | **0 error**（Unity 域重载实证；警告 25 条全为存量） | ✅ |
| 判据 1~11（HH317 探针 · 正门） | ⭐ **PASS=16 / FAIL=0**（第二跑；`Logs/hh317_m1d/hh317_m1d_smoke.txt`） | ✅ |
| 判据 12① M1-C 回归 | 读数与基线**逐值一致**（同 6512B；`Logs/hh315_m1c/hh315_m1c_smoke.txt` · 跑次 11:27） | ✅ |
| 判据 12② M7 回归 | **六轮 ALL PASS**（含 P11/P12 **改生产路径**后 · `[2_20B冒烟] ==== 第 N 轮汇总 ALL PASS ====`） | ✅ |
| 收尾三态（L-32） | 探针残留=0（cleanup 清 2）｜isDirty=False｜根对象数=119｜真暂停+封盘 True | ✅ |
| 行为变化 | 怪物被击杀不再掉落资源（`D803` 裁 · 已显式声明，见 §三-8） | ✅ 声明放行 |

---

## 一 · 改动清单（件 → 文件 → 落点）

| 件 | 文件 | 落点 |
|---|---|---|
| 件1 Killer 链 | `IDamageable.cs` | `TakeDamage(int, IDamageable source = null)`（接口 · 默认参数） |
| | `UnitController.cs` | 签名对齐 ＋ `_lastDamageSource` 暂存（`TakeDamage` 内）＋ `Die` 发布 `Killer` 真值 ＋ 发布后清空 |
| | `Building.cs` | 签名对齐 ＋ `_lastDamageSource`（⚠️ `Die` 按 cause 判：仅 `Killed` 采用，防"先被打后拆除"带出旧攻击者） |
| | `Portal.cs` | 仅签名对齐（死亡走 `DestroyPortal()` 不发事件 · 无战利品语义） |
| | `DamageSystem.cs` | `:412` 主路径传 `source`；`:406` 盾卫转移传**原 `source`**（⛔ 非 victim） |
| 件2 D490 | （随件1 自然复活） | `BerserkerFrenzy` 订阅 `Killer` ⇒ 生产可达（探针判据2 ＋ M7 P11 双证） |
| 件3 战利品来源 | `DamageSystem.cs` | `TrySpawnOrcLoot` 整段重写：来源＝死者 `WorkerInventory`（`GetComponent` 查询 · ⛔ 不给死者补挂）＋ 背包空不落箱 ＋ 抽空背包（防回池复用复制） |
| 件4 标签（形状1） | `StorageComponent.cs` | 新增 `public bool droppable = true;`（第二维 · 与 `Accepts` 零耦合 · ⛔ 不入档）＋ `Init` 从 `BuildingDef` 注入 |
| | `BuildingDef.cs` | 新增 `public bool droppable = true;`（数据行 · Tooltip 注明正交/不入档） |
| | `ChestEntity.cs` | `EnsureStore` 设 `droppable=false`（§九 :358 箱护栏落地 · 防"箱再掉箱"递归） |
| 件5 多容器分箱 | `Building.cs` | `Die` 内三路独立：`DropSiteStoreToChest`（M1-C 既有）＋ **`DropStorageToChest`**（产出仓 · 读 `droppable`）＋ **`DropVaultToChest`**（国库 · `ResetAll` 后落箱）；`EnterRuined` **不动**（仓留存 · Q3 负向） |
| 件6 洒落邻格 | `ChestManager.cs` | `EnforceCellLimit` 改洒落（渐扩环 1~4 找未满格 · 确定性）＋ 邻域皆满 ⇒ **不驱逐＋告警**（⛔ 不静默/不销毁）；新增 `FindSpillCell` |
| 件7 去 faction | `ChestManager.cs` | `SpawnChest(GridCoord, ResourceList)`（去参）＋ `ResetDrop` 同步；`ownerFaction` 恒 `None`（字段保留 · 仅存档保真） |
| | `TreasureVault.cs`／`Building.cs`(×2)／`DamageSystem.cs` | 5 生产调用点全改道 |
| | `ChestEntity.cs`／`ChestSaveData.cs` | 字段注释「语义已废 · 仅存档保真」（⛔ 格式不动） |
| 件8 怪物退役 | `MonsterController.cs` | `DropLoot()`／`BuildLootPack()` 删除 ＋ 墓碑注释（含行为变化声明）；`Die()` 只走基类 |
| | `MonsterDef.cs` | 两字段标注「M1-D 后无消费」（字段保留 · `D803` 裁） |
| 件9/#60 日志 | `DamageSystem.cs` | 日志改记 `killer` 身份（`r{raceId}/npc{npcId}(k{kingdomId})`）——⛔ 不再用乘后值反推；`[OrcLoot]` 关键字保留 |
| 件10 注释勘正 | `ChestManager.cs` | 类头「三调用方」⇒ **五调用方**全列 ＋ **防漂移注**；`:219` 同步 |
| 探针 | `Valley_HH317_M1D_Probe.cs`（新）＋ meta | 判据 1~11 全覆盖（正门 `EnterTestRun`） |
| | `Valley2_20B_Smoke_M7.cs` | P11/P12 **改生产路径**（`ApplyDamage` 真击杀 ＋ 第三方负例 ＋ 箱内容=死者背包） |
| | `Valley_HH316_U2Smoke.cs`／`Valley_HH294_Slice6PickProbe.cs`／`Valley_HH109_Smoke_SaveDomain.cs` | 调用点同步去 faction 实参（**编译必需**的 Editor 面同步） |

---

## 二 · 判据读数（1~12 · 逐条）

**探针：`Valley_HH317_M1D_Probe`（正门 · seed=31721 · Small/difficulty=2 · 15× · 槽 `hh317_m1d`）**
落盘：`Valley Rampart/Logs/hh317_m1d/hh317_m1d_smoke.txt`（＋时间戳副本 `_20260921_112354.txt`）

| # | 判据 | 读数 | 备注（鉴别力） |
|---|---|---|---|
| 1 | Killer 链 | ✅ `UnitDiedEvent.Killer == 致死者 k1(npc33)` | 样本走 `DamageSystem.ApplyDamage(source,target,9999)` 生产入口（⛔ 非直构事件）；**改前生产恒 null** |
| 2 | D490 狂战 | ✅ `Frenzy.Stacks=1`（≥1） | 改前恒 0（Killer 断裂 ⇒ 不可达） |
| 3 | 战利品来源 | ✅ 正例：新箱内容 `Wood=7`（＝死者背包逐值）；✅ 负例：背包空 ⇒ 箱数不变 | 改前＝凭空随机金（与背包无关） |
| 4 | ×1.5 不落地 | ✅ `Gold=10 ⇒ 箱内容 Gold=10`（⛔ 未乘） | 本批预期（金不在仓 · 待 `M1-E`） |
| 5 | 标签 | ✅ 可掉仓（默认 true）⇒ 掉箱 `Wood=3`；✅ `droppable=false` ⇒ 箱数不变 | ⛔ 覆盖"人口仓/水仓"未来落地口径 |
| 6 | #49 三分类 | ✅① 工事被打毁 ⇒ 产出仓掉箱 `Wood=4`（`IsFortification=True`）；✅② 拆除 ⇒ **分箱 2 箱**（工地仓{3}＋产出仓{4} · Σ=7）；✅③ `EnterRuined` ⇒ `state=Ruined`、箱数 7→7、仓 `Wood=5` 留存 | ② 是"多容器⇒分箱"＋"盲区②工地仓纳管"的同框双证；③ 负向（若弹箱＝未改对） |
| 7 | 洒落邻格 | ✅ `at=4` 恒 4；`ΣWood 25→34`（+9 恰为第 5 箱）；`总箱 11→12`；环内新增=1（洒落箱）；日志 `最早箱洒落 (39,39)→(38,38) 内容木材×1` | ⛔ 不静默/不销毁 |
| 8 | 去参 | ✅ 12 箱 `ownerFaction` 全 `None`（非 None=0） | 5 生产调用点全改道（编译面已证） |
| 9 | 怪物同型 | ✅ 无包单位（怪物等价样本：无 `WorkerInventory` 的 `UnitController`）⇒ 箱数 12→12 | ＋代码面：`DropLoot/BuildLootPack` 全库 0 命中 |
| 10 | #60 日志 | ✅ `[OrcLoot] 兽人 r3/npc33(k0) 击杀 Warrior @ … → 战利品箱 木材×7（来源=死者背包）落地` | 含 killer 身份；`[OrcLoot]` 保留（七考观察锚） |
| 11 | 存档往返 | ✅ 非空箱 12→12、差异=0（cell/bornDay/ownerFaction/contents 逐值）；空箱剔除 before=0/after=0 | ⚠️ 空箱口径见 §四-2 |
| 12① | M1-C 回归 | ✅ 读数与基线逐值一致（判据1~7 全读数齐 · 跑次 11:27:19） | 本批不改其链（结果不变） |
| 12② | M7 回归 | ✅ **六轮 ALL PASS**（人类/精灵/矮人/兽人 ＋ 换 seed 两轮）；改造项读数：`P11 第三方击杀不叠层（负·生产路径）killer=tuc0`／`P11 击杀叠层（正·生产路径）0→1`／`P12 兽人战利品（生产路径）箱 1→2`／`P12 箱内容＝死者背包 Wood=5` | ⛔ 已无直构事件假阳性 |

---

## 三 · 件级实现说明（关键决策）

1. **件1 甲案落地**：接口＋3 实现（`UnitController`/`Building`/`Portal`）四处均写 `= null` 默认参数 ⇒ **15 处无 source 调用点编译零改动**（生产 2：`SatietySystem:233`／`AIDebugUIManager:817`；Editor 13）。`DamageSystem:412/:406` 两处传真值。
2. **`Killer` 传递实现**：`UnitController` 用字段 `_lastDamageSource` 暂存（`Die()` 无参 ⇒ 不改虚方法签名 ⇒ `MonsterController` 覆写零波及）；发布后置空（防池化残留）。`Building` 侧按 `cause` 判（`Demolished` 恒 null）。
3. **件3 抽空背包**：落箱后 `inv.carriedAmount = 0`（"物随人死"·防对象池复用复制）；⚠️ 非掉落路径（非兽人击杀）残留见 §六-2 列报。
4. **件4 形状1**：`droppable` 独立维度（与 `Accepts` 前缀匹配**零耦合** · 判据 ⓒ 天然满足）；**默认可掉**（`D803` 裁）⇒ 33 栋空声明资产零迁移。
5. **件5 三路独立**：工地仓（M1-C 既有）／产出仓（新）／国库仓（新）互不重叠；⚠️ 子仓（矿洞副产×3／弹药×3）**未纳**（归"各自独立"决策 · 见 §六-3）。
6. **件6 保底**：邻域（环 1~4）无可落格 ⇒ 暂不驱逐＋`LogWarning`（内容留原箱 · 下次再试）——⛔ 不退回"直接销毁"。
7. **件7 存档保真**：`ownerFaction` 字段读写**原样保留**（⛔ 不改存档格式）；新箱恒 `None`。
8. **件8 行为变化声明（供回写 `09`）**：

> **`D213` 怪物掉落退役（`M1-D`）**：怪物（`Raider`/`Slinger`/`Brute`）被击杀**不再掉落资源箱**——掉落统一由「死者仓＋标签」口径决定，怪物**无仓** ⇒ 不产生箱（`09` §9.7 清单无怪物项）。`MonsterDef.carryResource`/`lootResource` **保留但无消费**（`D803` 裁）。掠夺链（`MonsterAI._carryingHome`）本就**无资源面实现**，本批**不新增**。

---

## 四 · 探针方法论修正（如实记录 · L-51 精神）

1. **首跑 2 条 FAIL 的根因＝探针观测口径缺陷（非生产缺陷）**：
   - 判据7/11 首跑用 `Object.FindObjectsOfType<ChestEntity>()` 读箱 ⇒ 同帧 `Destroy` 的旧箱**尚未真正销毁**（幽灵对象）⇒ 总量多计 1／读档后 13→25 假红。
   - 生产侧日志反而证明机制正确：`单格上限：最早箱洒落 (39,39)→(38,38)`；`读档重建：12 个箱子`。
   - **修复**：箱集合改走 `ChestManager` 权威口径（`FillChestsInCellRect` 全图矩形 · `_chests` 即时增删）＋ 存档快照剔除空箱 ⇒ **二跑全绿**。
2. **空箱语义**：`LoadState` 跳过空箱（`pack.IsZero ⇒ continue`）为**既定语义**（`SpawnChest` 同源 · HH.109）；首跑 13 箱内含 1 空箱（工人搬空后实体留存 = 已知缺口 `O-4`）⇒ 快照口径已剔除并单列计数。
3. **判据 9 样本声明**：真怪物生成依赖 Portal/波次链（进局成本高）⇒ 采用**机制等价样本**（无包 `UnitController` ⇒ 与 `MonsterController` 同为无包子类）＋代码面 `DropLoot` 已删（grep 0 命中）双证。
4. **判据 1 样本声明**：走 `DamageSystem.ApplyDamage` 生产入口（投射物/近战/冲锋的唯一伤害函数）⇒ 完整驱动 `TakeDamage(source)→Die→事件`；未走 `NPCBrain` 自主索敌（非判据必需）。

---

## 五 · 回归与互不干扰（判据 12）

- **M1-C 冒烟**（`HH315 M1-C …同局冒烟`）：跑次 11:27:19 · 判据 1~7 读数与基线**逐值一致**（含判据5"退还掉箱 0→1 {Wood:4}"——本批改其调用点去参后结果不变）。
- **M7 冒烟**：六轮 `ALL PASS`；R1~R6 清场/跨轮断言全过（每轮 `ResetWorldForNext` 无残留）；收尾 `[SmokeApi] QuitSmoke: 冒烟全部完成，退出 Play 模式`。
- **HH317 探针**与两冒烟互不干扰（各自独立进局 · 独立槽位 `hh317_m1d`/`hh315_m1c`/`smoke_*`）。

---

## 六 · 列报（超范围观察 · ⛔ 本批不改）

1. ⭐ **`RulerController.OnUnitDied` 的 `null==null` 误触发**（既有 · 根因已坐实）：
   `if (evt.Unit as UnitController == monarchUnit)` —— God-view 下 `monarchUnit == null`，而**建筑死亡事件**的 `evt.Unit as UnitController` 亦为 `null` ⇒ **两个 null 相等 ⇒ 误触发 `OnMonarchDied()`**（日志"君主阵亡（D249：不再判负）"＋`monarchUnit=null` 幂等）。
   影响：仅日志噪声（D249 不再判负 ⇒ 无实质后果）。建议（供裁）：加 `monarchUnit != null` 守卫 ⇒ 归观察项/后续小批，⛔ 本批不动。
2. **对象池洗涤不含背包**（既有）：`UnitController.ResetForReuse` 未重置 `WorkerInventory` ⇒ **非掉落路径**（非兽人击杀）的死者背包残留，池化复用后可能"继承货物"（资源守恒隐患）。本批只在**掉落路径**抽空；建议归观察项（或 `M1-F` 卸货路由一并收）。
3. **子容器未纳 `Die` 掉箱**（本批决策）：矿洞副产仓×3／攻城工坊弹药仓×3 为**子物体挂载**，`GetComponent` 不命中 ⇒ 本批只做"工地/产出/国库"三路（对齐任务书"各自独立"）。若需纳管 ⇒ 另裁。
4. **`EnforceCellLimit` 保底档**：邻域皆满时"暂不驱逐"⇒ 该格可持续超限（每格最多超 1 箱·下次再试）；属保底设计（⛔ 宁可不驱逐也不丢）。
5. **"君主阵亡"日志噪声**（本轮探针观测到 4 次）：即 §六-1 的表现面。

---

## 七 · 红线自查

| 红线 | 自查 |
|---|---|
| 改动面 | ✅ 仅生产码 13 ＋ Editor 探针 5（含新探针）＋ 文书；⛔ `WarehousePanel`／四档账本／美术／pixel-forge／GameScene／Packages／3.6·3.8 doc 未触 |
| ⛔ 不动 | ✅ `PaidStageCost`/`CurrentStageCost`/`GetRepairCost`/`BuildRefundPack` 未触；`DeathCause` 枚举未扩（恒 `Killed`/`Demolished`）；存档格式未动（`ownerFaction` 字段保留 · 标签字段**未入档**） |
| 行尾纪律 | ✅ 改前逐文件检测（`IDamageable.cs`=CRLF、`UnitController.cs`=混合 1819CRLF+7LF ⇒ 走 python 二进制按行替换；其余 LF 直编）；改后复检：CRLF 46/LF 0、1827+7（**零污染**）；`git diff --stat` 规模=精确改动量（无整文件行尾翻转） |
| 正门纪律 | ✅ HH317 探针走 `TestHarnessApi.EnterTestRun`；M7/M1-C 亦正门 |
| 收尾三态 | ✅ 真暂停（TS=0）＋封盘 ＋ `ExitTestRun` ＋退 Play；探针残留 0／isDirty=False／根对象数已记档 |
| git | 具名 `add`（逐文件）＋ **⛔ 不 push**；⛔ 不代提交策划端账本（`_编号登记.md`/`_任务队列.md`/`_当前快照.md`/`_策划教训库.md` 未触）；**本报告随本批 commit**（补前两轮文书同提） |

## 八 · 应登记项（供策划端）

1. **行为变化回写 `09`**：§三-8 声明文本（`D213` 怪物掉落退役）。
2. **观察项建议**：① `RulerController` null 守卫（§六-1）② 池化背包洗涤（§六-2）③ 子容器掉箱纳管（§六-3）④ 空箱留存（`O-4` 既有 · 本批存档往返已按既定语义剔除）。
3. **`M1-E` 挂账**：`×1.5` 乘算（金进仓后 · `TrySpawnOrcLoot` 内已留注释位）。
4. **证据路径**：`Valley Rampart/Logs/hh317_m1d/hh317_m1d_smoke.txt`（＋时间戳副本）；`Valley Rampart/Logs/hh315_m1c/hh315_m1c_smoke.txt`；M7 读数为 console（`[2_20B][PASS]` 六轮 ALL PASS · ⛔ 该探针不落盘文件属其既有形态）。

---

*本端：执行端 · 2026-09-21 · M1-D 施工完毕（十件全落地 · 判据 1~12 全绿 · 编译 0 error）*
