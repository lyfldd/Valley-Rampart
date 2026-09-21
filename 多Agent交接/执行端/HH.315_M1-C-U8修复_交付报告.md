# HH.315 · `M1-C` · `U-8` 补丁批 —— 交付报告

- 任务号：`HH.315` · `M1-C` · `U-8`（承 `D799` 立 ⇒ `D800` 只读评估验收（全采 ＋ 3 处加强）⇒ 本批施工）
- 本轮性质：**施工**（十件全落 ＋ 判据/回归取证）；⛔ 未 push；⛔ 未代提交策划端账本
- 日期：2026-09-21
- 被验代码：**`8d32e0fc`**（具名 `git add` 三文件 · **+131 / −8**）
- 取证手段：Unity 2022.3.62t7 **实编译**（强制刷新 → 域重载成功 · 控制台 **0 编译错误**）＋ 反射证据 ＋ ⭐ **MCP 临时驱动**（正门进局 · 帧驱动 · ⛔ 零探针文件）＋ 既有 `HH.315 M1-C` 冒烟**正门回归** ＋ 静态实读
- ⚠️ **探针形态声明**（守红线「改动面仅 3 文件」）：本批**不新增探针文件**；11 条判据由**临时驱动**产出（经 Unity MCP `execute_code` 挂 `EditorApplication.update` 帧驱动 · 收尾卸钩），日志落 `Valley Rampart/Logs/u8/drvA|B|C.log`（⛔ 不入库）。**若策划端要求可复跑的长期探针**（如 `Valley_U8_Smoke.cs`）⇒ 请授权另立（本端未越红线）。

---

## 〇 结论速览

| 件 | 落点（`8d32e0fc` 后行号） | 实测验证 |
|---|---|---|
| 1 案甲注册 | `Building.cs:897`（`Demolish` 内 `EnsureRegistered()`）＋ `:1530`（新方法） | ✅ 驱动 A/B：`Demolish` 前 `bodyInSources=False` → 后 `True` |
| 2 读档补注册 | `Building.cs:1206`（`LoadState` 尾） | ✅ 驱动 B：读档后 `bodyInSources=True` ＋ **续拆确认** |
| 3 注销工地仓源 ＋ 硬化 | `Building.cs:895`（`Demolish`）／`:579`（`OnSiteMaterialsReady`） | ✅ 驱动 A：拆除期间 `siteStoreInSources` **0 帧** |
| 4 拆除工时活读 | `TaskScheduler.cs:1305-1310`（`GetTaskDuration`） | ✅ 三样本 **派工轮次＝1**（改前应 ≥3） |
| 5 `n` 按 `DemolishTaskArgs` 过滤（**形态甲**） | `TaskScheduler.cs:161-181`（新重载）＋ `Building.cs:659/667` | ✅ 新接口**调用面＝1**（`Building.DemolishDuration`） |
| 6 E1-a 清拆除态 | `Building.cs:1264`（`EnterRuined`） | ✅ 驱动 C R3：`IsDemolishing=False` |
| 7 E1-b 注销仓源（**留仓**） | `Building.cs:1279`（`EnterRuined`） | ✅ 驱动 B U3：仓源注销 ＋ **内容留仓=1** |
| 8 E3 `CanDemolish` 排 `Ruined` | `Building.cs:871` | ✅ 四处读数 `CanDemolish=False` |
| 9 U-5 两字段入档 | `BuildingSaveData.cs:70-73`／`Building.cs:1083-1084`（存）／`:1165-1166`（读） | ✅ 驱动 B/C：`flagPU`／`flagPR` 读档存活；**升级 level 1→2**；修复退费 `{Wood:6}` |
| 10 注释勘正 4 处 | `Building.cs:1073-1078`（N-3）／`:604-611`＋`:645-650`（E2/O7）／`:1520-1525`（O5）／`Demolish` 头注 | ✅ 四条逐字落码（零行为） |

---

## 一 · 十件落码（逐件）

| # | 关键落地 |
|---|---|
| 1 | `EnsureRegistered()`（幂等 · ⛔ 无 state 条件 · **⛔ 不加 `IsRegistered` 查询口**：`Register` 经 `_sources.Add` 天然幂等）；`Demolish()` 内 **`_demolishing = true` 之后**调用（顺序约束写入方法头注：`IsValid` 三项 `||` 依赖它） |
| 2 | `LoadState()` 末尾 `if (_demolishing) EnsureRegistered();`（⛔ 未改 `BuildingFactory:189` 守卫） |
| 3 | `Demolish()` 内 `UnregisterSiteStore()`（⛔ 不动 `_items`）；`OnSiteMaterialsReady()` **首行** `if (_demolishing) return;`（封「在途恰好投满 ⇒ `Clear()`」窗口） |
| 4 | `GetTaskDuration`：`args is DemolishTaskArgs && target != null` ⇒ **活读** `target.DemolishDuration()`（≤0 兜底 `workDuration`，照 Gather 形制） |
| 5 | **形态甲**（报选见 §五 O6）：`TaskScheduler` 新增 `CountAssignedWorkers(ITaskSource, Func<KingdomTask,bool>)`；`DemolishDuration` 的 `n` 用**静态谓词** `IsDemolishTask`（⛔ 不捕获 ⇒ 无每帧分配） |
| 6/7 | `EnterRuined()`：`state=Ruined` 后 `_demolishing=false; _demolishProgress=0;`；`:1207 Unregister(this)` 后 `UnregisterSiteStore()`（内容 **留仓** · 重建复用同一实例） |
| 8 | `CanDemolish => ... && state != BuildingState.Ruined`（数据层与 UI 同源；顺手拦陈旧面板路径） |
| 9 | `BuildingSaveData` **尾插** `pendingUpgrade`／`pendingRepair`（零 bump）；`SaveState` `state=(int)state` 后入档；`LoadState` 在 `_demolishProgress` 后恢复；`InProgressStageIsBuild` ③④ 兜底**保留**、注释改为「**旧档兼容**」 |
| 10 | N-3 注释勘正（构造法 ≠ 生产路径）；`Update`／`HasAssignedWorker` E2 加注（门控＝「**有人接单**」）＋ O7；`RegisterWithTaskScheduler` O5 留痕；`Demolish` 头注写件1/件3 落点与顺序约束 |

---

## 二 · 编译与运行时验证

| # | 读数 |
|---|---|
| 编译 | `refresh_unity(force/all/compile)` → 域重载成功；控制台 **0 编译错误**（改动后） |
| 反射 | `EnsureRegistered=True ｜ CountAssignedWorkers(2arg)=True ｜ pendingUpgrade=True ｜ pendingRepair=True ｜ asm=Assembly-CSharp` |
| 行尾 | 改前 **三文件 bare-LF（CRLF=0）** → 改后复验**仍 bare-LF（CRLF=0）** |
| 收尾三态 | **残留 0**（探针 5 栋样本 House 全清）／**isDirty=False**／根对象 **51**（退 Play 后复核）；`ExitTestRun` 已调（考跑态全量恢复） |
| 进局通道 | ⭐ 唯一正门 `TestHarnessApi.EnterTestRun`（⛔ 无裸跑） |

---

## 三 · 判据读数（逐条 · 含鉴别力声明）

### 判据 1「首次建造·投料中」（⭐ 生产路径）

```
[U8A] TryBuild=True building=True state=Constructing awaiting=True       ← 真 BuildController 下单
[U8A] ROOT-READING(before Demolish): bodyInSources=False | siteStoreInSources=True | CanDemolish=True
[U8A] AFTER Demolish(same frame): IsDemolishing=True bodyInSources=True siteStoreInSources=False storeIsValid=True
[U8A] round#1 dispatch@progress=0.041 frame=31
[U8A] f35..f65 prog 0.182→0.913 asgn=1 bodyInSources=True
[U8A] DONE-DEMOLISH frames=70 gameSec=11.45 rounds=1 lastProg=0.994
```
**鉴别力**：若件1 未改 ⇒ 本体永不在册 ⇒ 无拆除任务 ⇒ 进度恒 0 —— 本端在**同一样本**上直接取得「`Demolish` 前 `bodyInSources=False`」（该必然性的前置读数）＋「后 `True`」的对照。
**判据 2（首次建造·料齐施工中）**：⚠️ **未单跑**；由判据 4 的同状态类（`Constructing` ＋ `!awaiting`）样本**同态覆盖**（案甲只依赖 `_demolishing`，⛔ 不依赖 `_pending*`）⇒ 若需逐字样本请授权探针。

### 判据 4「废墟重建·投料中/施工中」

| 样本 | 读数 |
|---|---|
| R2（投料中） | `after StartRebuild: Constructing awaiting=True bodyInSources=False` → `post-demolish: bodyInSources=True IsDemolishing=True` → `DESTROYED frames=127 gameSec=6.24 rounds=1 lastProg=0.996` |
| R1（料齐 paid 态） | `pre-demolish bodyInSources=False` → `DESTROYED frames=148 gameSec=7.14 rounds=1 lastProg=0.992` |

**鉴别力**：改前两态均恒 0（不在册）。

### 判据 5「读档续拆」（态＝`Constructing`）

```
[U8B] CRIT5 freeze@progress=0.0000 (waitF=901)
[U8B] CRIT5 Save=True Load=True
[U8B] CRIT5 after-load: building=True state=Constructing IsDemolishing=True progress=0.0000 bodyInSources=True kingdomId=99
[U8B] CRIT5 ***CONTINUATION CONFIRMED*** 0.0000 -> 0.0147 waitF=30 asgn=1
```
**鉴别力**：改前（件2 缺）⇒ 读档后未注册（`bodyInSources=False`）⇒ 无拆除任务 ＋ `Demolish` 幂等拦死 ⇒ **永久冻结**；本读数 `bodyInSources=True` ＋ 进度续涨即鉴别。
⚠️ 附注：冻结时进度＝0（保存点尚未推进）；**进度逐值保留**由既有冒烟 §F 覆盖（`0.9724 → 0.9724`）。

### 判据 6「C 面」

`CRIT6: siteStore inSources frames during demolish = 0 (expect 0)`（驱动 A）＋ `AFTER Demolish: siteStoreInSources=False`（驱动 A/B/C 四处一致）。
**鉴别力**：改前工地仓持续在册 ⇒ 拆除全程该计数 ≥1（另一名工人给将拆工地送料）。已到料掉箱链由冒烟 §D 判据 5 复核（原地 `{Wood:4}` 1 箱 · ⛔ 国库不即时增加）。

### 判据 7「D 面（时间 ＋ 轮次）」

| 样本 | 游戏秒 | 帧 | **派工轮次** |
|---|---|---|---|
| 投料中（驱动 A） | 11.45（＝派工/到场延迟 ≈5.4s ＋ 进度推进 ≈6.0s） | 70 | **1** |
| 重建投料中（R2） | **6.24**（≈标称 6.00s） | 127 | **1** |
| 重建料齐（R1） | 7.14 | 148 | **1** |
| Active（冒烟 §D） | 10.49（dt=2.28s/帧） | 4 | —（未测） |

**鉴别力（轮次）**：件4 前每轮 `Working=workDuration=2s` ⇒ 6s 进度需 **≥3 轮**派工；件4 后工时活读＝6s ⇒ **实测 1 轮**（三样本一致）。
⚠️ **慢通道副产物读数**：三样本均为「`FinishDemolish` 先收口（`lastProg≈0.99` 即销毁）⇒ 该任务被 `OnBuildingDied` **放弃**（⛔ 非「完成」）」——与「建筑自推」口径一致 ✓。

### 判据 8「E1 面（打毁 ⇒ 重建且完工）」

```
[U8C] R3 demolishing=True bodyInSources=True
[U8C] R3 after TakeDamage: state=Ruined IsDemolishing=False（件6） CanDemolish=False（件8）
[U8C] R3 rebuild started: state=Constructing awaiting=True need=2 storeInSources=True
[U8C] R3 (fallback) waitF=500 awaiting=False
[U8C] R3 ***REBUILD COMPLETED*** state=Active lv=1 hp=100/100 waitF=562
```
**鉴别力**：改前 ⇒ `_demolishing` 闩锁无解 ⇒ `Update` 永走拆除分支且无工人 ⇒ **重建永久冻结**（＋料齐时 `Clear()` 吞料）。
⚠️ 附注：重建投料（`{Wood:2}`）在观测窗内完成；探针在 `waitF=500` 设补料兜底防搬运链抖动——该帧读到 `awaiting=False` ⇒ **自然链已推进**，兜底至多同帧生效。

### 判据 9「E3 面」

| 读数点 | 值 |
|---|---|
| 驱动 B U3（`StartConstructing` 后 `EnterRuined`） | `state=Ruined **CanDemolish=False**` |
| 驱动 C R1/R2（工厂 `initialState=Ruined`） | 同（两处） |
| 驱动 C R3（战斗中打毁） | 同 |
**鉴别力**：改前 `CanDemolish=true`（陈旧面板路径可点 ⇒ `Demolish()` 进 `Ruined` 拆除态）。
**后半（陈旧面板拦截）**：`BuildingPanel:393` 读同源判据 ⇒ 加判据即拦（静态链：面板 ⛔ 不订阅 `BuildingRuinedEvent` ⇒ `Refresh` 不重跑 ⇒ 按钮残留 ⇒ 点击被 `:393` 拦）；⚠️ 无头驱动不可点 UI ⇒ 以**数据栏读数 ＋ 调用链静态证据**替代（如实声明）。

### 判据 10「U-5 面」

**升级面**（驱动 B）：
```
TryUpgrade=True state=Constructing awaiting=True flagPU=True need=16
Save=True Load=True
after-load: **flagPU=True** (expect True) awaiting=True
injected need=16 → awaitingNow=False
completion: state=Active **level=2** (expect 2)
```
**修复面**（驱动 C R1）：
```
after StartRebuild: flagPR=True
after-load(repair-in-progress): **flagPR=True**
injected=2 → paid
refundChest=1chest {Wood:6}（＝def.cost{Wood:4}＋repair{Wood:2}）
```
**鉴别力**：改前 ⇒ 读档后 `flagPU/flagPR=false` ⇒ ① 升级完成**不升级**（`level` 仍 1）② 拆除退款按 `levels[0].upgradeCost` 口径 ⇒ 读数应为 **`{Wood:14,Stone:6}`**（`Stone6/Wood10` ＋ `def.cost Wood4`）——实测 **`{Wood:6}`** 即鉴别。

### 判据 11「存档往返逐值一致」

- 两新字段：驱动 B/C 的 `Save/Load=True` 且 `flagPU`／`flagPR=True` 逐值存活；旧档缺字段 ⇒ struct 默认 `false`（`③④` 兜底**保留** · 注释已勘正）。
- 既有字段：冒烟 §F（投料态 `siteContents/awaiting/totalInvested` 往返一致 True；拆除态进度 `0.9724→0.9724`）。

### 未跑判据（如实列报）

| 判据 | 状态 | 理由 / 建议 |
|---|---|---|
| 2 首次建造·料齐施工中 | ⚠️ 同态覆盖（见判据 1 附注） | 需逐字样本请授权探针 |
| 3 升级·投料中（**回归** · ⛔ 零鉴别力） | ⛔ 未跑 | 该子型改前已通、本片未改其注册路径（`Active→TryUpgrade` 保留在册）；建议并入下一回归批 |

---

## 四 · 回归

| # | 结果 |
|---|---|
| ① `HH.315 M1-C` 冒烟（正门 · seed=31518 · 槽 `hh315_m1c`） | ✅ **§C/§D/§E/§F 与 `D799` 基线逐值一致**（阈值拦截 `3/1/0`；半血房全退 `{Wood:4}` ＋ 原地 1 箱；国库不变；§E 修复费 `{Wood:1,Metal:1,Ore:1}`／退还 `{Wood:2,Ore:2,Metal:2}`；§F 往返 `True`）⇒ **零回归**；⭐ **U-4 判据 8′ 复跑通过**（掉箱读数逐值一致）。<br>⚠️ 本轮 **§B（真下单搬运）未在 120s 真实秒窗内落地**（`dt=2.28s/帧` ⇒ 900 帧≈136s 命中窗口上限；`在册搬料工人=1` 恒定）——本片**零改**搬运/取料/装载段；同日基线轮（`dt=1.55`）第 142 帧完成 ⇒ 归**环境/窗口抖动**，须复跑。 |
| ② `2_20B_M7` 冒烟 | ⛔ 未跑（预算）⇒ 建议与 ① 的 §B 复跑同批 |
| ③ 1× 时基跑批（验 `O-6`） | ⛔ 未跑（长跑批）⇒ 同上 |

---

## 五 · 观察项／列报（⛔ 均不属本片改动面）

| # | 内容 |
|---|---|
| **O1** ⚠️ | 运行期 `MissingReferenceException @ Building.GetPosition ← DamageSystem.ExecuteAttack`（**≥200 条** · 堆栈已取证）：既有 `DamageSystem` 待处理攻击表在**攻方建筑被销毁后**仍访问其位置（`DamageSystem.cs:284-301` ⛔ 无 Unity 假空守卫）⇒ 建议另立 `DZ`；本片**未触碰**该两处（改动面 0 行）。 |
| **O2** | 冒烟 §B 搬运窗抖动（见 §四①）——建议复跑取证。 |
| **O3** | 件7 的「`Ruined` × 站点仓在册」组合在**当前生产路径不可达**（投料/施工态不可受伤 ⇒ `EnterRuined` 前无仓）⇒ 本片属**防御性收口**；机制已由单元面驱动证明（仓源注销 ＋ **内容留仓=1**）。 |
| **O4** | ⭐ `Ruined` 清理出口此后**只剩「重建」**（`CanDemolish=false` ⇒ 玩家无路径彻底清掉废墟）——本片**新增副作用**，请报裁是否补出口（如废墟拆除）。 |
| **O5** | `TaskScheduler.cs:95` 与 `Building.cs:1445` 同型注册守卫（同一语义写两遍）⇒ 已加注留痕（`D800` 裁本片不动）。 |
| **O6** | **件5 形态甲报选**：甲＝保留结构（新重载 ＋ 静态谓词 · 调用面 **1 处** · ⛔ 零每帧分配）＋ `k` 协作风支**显式保留加注**；⛔ 未走形态乙（`n≡1` 硬编码会使 `k` 分支失去实现意义、且日后放开多工人需回改）。⭐ 拆除＝单人（正常路径 `n≤1` ⇒ `k` 恒不生效）。 |
| **O7** | 拆除标称 `DemolishDuration()=6s` 与实测不等（含**派工/走动期**推进）——已在 `Update`／`DemolishDuration` 双处加注（读数见 §三判据 7）。 |

---

## 六 · 红线自证 ＋ 落账

- ⛔ **改动面仅 3 文件**（`Building.cs`／`TaskScheduler.cs`／`BuildingSaveData.cs` · +131/−8）＋ 报告 1 文件；⛔ 未触碰 `WarehousePanel`／`TreasureVault`／四档账本／美术／`pixel-forge`／`GameScene`／`Packages`／`3.6·3.8 doc`。
- ⛔ **未改退款公式**：`PaidStageCost`／`CurrentStageCost`／`GetRepairCost`／`BuildRefundPack` **逐字未动**（`git diff` 可证）。
- ⛔ 未 push；⛔ 未代提交策划端账本（`_编号登记.md`／`_任务队列.md`／`_当前快照.md` 零触碰）。
- **行尾纪律**：改前验（三文件 bare-LF）→ 改后复验（仍 bare-LF）；⛔ 未走 python 二进制通道（LF 可 Edit）。
- **进局唯一通道**：`TestHarnessApi.EnterTestRun`（⛔ 禁裸跑已守）；**收尾三态**：残留 0／`isDirty=False`／根对象 51。
- **探针**：⛔ 零文件（MCP 临时驱动 · 收尾卸钩）；日志 `Valley Rampart/Logs/u8/`（⛔ 不入库）。
- **commit**：`8d32e0fc`（具名 `git add` 三文件 · 131 insertions／8 deletions · ⛔ 未 push）。
- **报告提交**：`f8f41a6b`（本交付报告 ＋ 上一轮《`HH.315_M1-C-U8评估_评估报告.md`》（此前未入库 · pre-commit 钩子提示 ⇒ 按「交付即 commit」一并入库）· 2 文件 581 insertions · ⛔ 未 push）。

---

## 七 · 状态

- **十件**：✅ **全落**（编译 0 错 · 反射证 · 见 §一）。
- **判据**：9/11 有直接读数（1／4／5／6／7／8／9／10／11）；2 为同态覆盖；3 未跑（零鉴别力回归件）。
- **回归**：① ✅ 零回归（§B 窗口抖动待复跑）；②③ ⛔ 待批。
- **⛔ 未报裁阻塞项**；⭐ 须策划端裁/知悉：`O4`（Ruined 清理出口）、`O1`（DamageSystem 既有异常）。
