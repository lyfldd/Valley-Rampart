# HH.341 · 小源② `ChestEntity` **补证轮（只读）**交付报告

> 执行端 ｜ 2026-09-29 ｜ 依据：`D917`（判绿裁定 · 采 (a) 逐项同轮落盘 ⇒ 源级停手待补证）＋ `D914`（基线口径逐项 3）
> ⛔ 本轮**只读运行 + 报告加注**：零生产码/零资产改动、未碰 `GameScene.unity`、未用回滚命令（回退手段限 `git show`）、未 push、未取新 D 号
> 证据前缀 `Valley Rampart/Logs/hh341_small_chest_entity_*`（本轮新增 `*_probe6.*`／`*_l95b_*`；⛔ 未覆盖上一轮任何证据文件）
> 唯一 run ＝ `hh341_small_chest_entity_probe6.cs`（slot=`hh341chest`、seed=424242、`WorldSize.Small`、difficulty=2、`timeScale`=15.0 实测；墙钟 45.03s；游戏秒总窗 657.6）

---

## 一、开工回执

| 项 | 内容 |
|---|---|
| 当前源 / 顺序 | `ChestEntity` ／ 第 **2** 源（D911 一次性例外）；⛔ 未申请第 3 源 |
| **HEAD 实测** | **`5442a6a1`**（与裁定所列**一致** · 无需解释差异）；源码提交面 `872ec5d5`（两文件 +276/−0）已入档、本轮**未改动**（`git diff --name-only -- Valley Rampart/Assets/_Game` ＝ 空） |
| 工作树状态 | `_Game` 仅 4 个**既有未跟踪** Art 文件（`New Palette.prefab*`／`ground_tropical.asset*`，⛔ 非本端）；运行前＝运行后一致 |
| **本轮写动作计数** | **＝ 0**（生产码 0／资产 0／场景 0；探针脚本与全部日志落 `Logs/`（gitignored）⇒ 不入仓库提交面）；⛔ 回滚类命令未执行（`L-99`） |
| `GameScene.unity` | **只登记** `git hash-object`＝`4a86f26f6a7c2aab3c440903ddfa80020ddec9a3`（挂账 `O-14` 载体；⛔ 未还原/未 checkout） |

---

## 二、同轮闭合表（①~⑩ · 每项「同一 run ＋ 同一箱」证据原文）

**同轮边界**：下列 ①~⑦⑧ 全部属**箱 A**（`inst=-25552` · `cell=(93,60)` · 落点= k1 工人 0（npcId=6）位置 · `CFG.packA=Stone:10`），全部在同一 run 的 **P1 段**内按序发生；⑨ 属**箱 B**（`inst=-33474`→读档重建 `inst=-34040` · **同格**）的**同 run P2 段**。⛔ 无任何跨轮引用。

| # | 判据 | 同轮同箱证据（原文） | 时点（游戏秒/tick） |
|---|---|---|---|
| ① | 真实箱源卡片创建 | `## S CARD_ROW CARD.P1.add CARD.t=13.67 box#-25552 id=6 state=Assigned worker=10 retry=0/1 consumed=0 blocked=0 abort=None lastUn=None`（A 箱同 run 逐张：`id=6/7/11/12`；终值 `A total=4`） | t=13.67 |
| ② | `Transport` 派发 | `## S DISP_ROW DISP.P1.add DISP.t=13.67 DISP.npcId=10 DISP.v=Transport/chest#-25552/MovingToSource/Gold:0/k2`（同箱追加：`t=60.67 npcId=4 …/k1`、`t=126.36 npcId=19 …/k0`、`t=163.52 npcId=6 …/k1`） | t=13.67~163.52 |
| ③ | `MovingToSource → Working` | 旧链：`DISP.P1.chg DISP.t=189.22 DISP.npcId=6 DISP.from=Transport/chest#-25552/MovingToSource/Water:0/k1 DISP.to=Transport/chest#-25552/Working/Water:0/k1`；协议侧：`CARD.P1.chg CARD.t=189.22 …id_12…Assigned… to[…state_Executing…]`（B 接缝 `OnProtocolArrived`） | t=189.22 |
| ④ | 箱内容装载 | `DISP.P1.chg DISP.t=191.84 DISP.npcId=6 …Working… → …MovingToDest/Stone:10/k1` ＋ `EVT.P1.A.cnt 10->0 EVT.t=191.84 EVT.live=1 EVT.tick=191`（**同箱存量 10→0**） | t=191.84 |
| ⑤ | `MovingToDest` | 同 ④ 转段（`…/MovingToDest/Stone:10/k1`） | t=191.84 |
| ⑥ | 卸货与 `Complete` | `CARD.P1.rem CARD.t=196.79 id=12 last[…id_12_state_Executing_worker_6…]` ＋ `[TaskScheduler] 完成 Transport 任务 → npcId 6`（t=196.79） ＋ 终止行 `A=…cnt_0_live_0_total_4_done_1_ab_3_lastW_6_inSrc_False_invFlag_True`（**卡层 `done=1`、全终态 `live=0`**） | t=196.79 |
| ⑦ | **自然搬空 ⇒ Tick① D**（本轮硬目标） | **三连落盘**：`EVT.P1.A.cnt 10->0（t=191.84·tick191）` → **`EVT.P1.A.invalidated=True EVT.t=193.14 EVT.tick=193 EVT.empty=True EVT.live=1`** → **`EVT.P1.A.inSources=False EVT.t=193.14 EVT.tick=193`**（`_sourceInvalidated` 为**反射直读** `ChestEntity._sourceInvalidated` 字段；tick 191→193＝装载后**下一 Tick① 源清理段**） | t=191.84→193.14 |
| ⑧ | 四表终态归零 ＋ 不变量 | `SNAP.invVio=0`（**126/126 快照全 0**）；A 箱：`live=0`（1×`Done`＋3×`Aborted(RetryExhausted)` ⇒ 全终态释放；封口样例 `id=6/7/11` 均 `rem` 于池外）；B2 收口：`SNAP.P3 boxes 2→1`＋`reserv 7→1`（**−6 ＝ B2 的预定份额释放**） | 全程 / P3 |
| ⑨ | 跨档重建后重新广告与派工 | 同 run P2 段：`nextIdBefore=43 → nextIdAfter=52`；`oldMaxId=42`；**首卡 `id=43`（43>42 ⇒ 无复用）**；`tsSame=True`（调度器实例保活）；B2 重建 `inst=-34040 cnt_999 live_8 total_8 inSrc_True`（**3 帧内已 8 卡＝重新广告+再派工**）；`[P2] 停止 reason=newDone1`（t=636.94） | t=611→637 |
| ⑩ | 基线无漂移 | 运行前＝运行后：`SchedRef 71/17`、`NewKT 15/10（14/9）`、`ITaskSched 5/2（2/2）`、`ITaskSrc 49/13（30/12）`、`AllAssets 167/30`；`git status -- _Game` 前后一致 | 前/后 |

**⑦ 的机制细节（如实）**：`invalidated` 时刻 `EVT.live=1`——即 `OnProtocolSourceInvalidated` **封口了非在途卡、保留了在途卡**（id=12 正在 `MovingToDest`）⇒ 该卡随后走完卸货并在 t=196.79 `Done`——**「源失效在途保护」的同轮实证**。

---

## 三、⑦ 专项（自然搬空 ⇒ Tick① D · 直接落盘读数）

**直接落盘读数（取得 ✓）**：见 §二-⑦ 三连 EVT；字段读法＝反射 `typeof(ChestEntity).GetField("_sourceInvalidated", NonPublic|Instance)`（`REFLECT invFlagField=True` 确认字段可读）；时序＝**装载搬空（tick191）→ 下个 Tick①（tick193）`invalidated=True`＋`inSources=False`**（与 `TaskScheduler.Tick` ① 源清理段注入点 `+279,3` 语义一致）。

**放弃原因分列**（全窗口实时计数 · `ABANDON_ROW` · 分母＝窗口内 `Abandon Transport` 全部事件）：

| reason | 计数 |
|---|---|
| `Unreachable` | 1369 |
| `Timeout` | 135 |
| `DestFull` | 2 |
| `SourceInvalid` | 19 |
| （`BrainLost`） | **0**（本窗口未出现·含零计数项） |
| **`Dead`** | **0 —— 「未观察到」**（⛔ 不写「不存在」；观测范围＝全窗口 Abandon 实时计数，`reasonKinds=4`） |

**⑦ 结论**：**「自然搬空 ⇒ 协议侧 Tick① D」＝ 本轮取得（直接落盘）**；放弃路径分列如上（4 种出现＋`Dead` 未观察到）。

---

## 四、`k0` 与其他工人分列（`D917` 逐项 6）

| 口径 | 读数 | 来源 |
|---|---|---|
| **箱源接单分列** | **`k0=64` ／ 其他国 `=17`**（逐国：`k1=16`、`k2=1`、`k3=0`·含零项） | `KING_ROW`（`_chestDispK0`/`_chestDispOth`/明细） |
| 在册分列（快照样例） | 首快照 `k0OnBook=0／othOnBook=5`；P2b 末 `k0OnBook=4／othOnBook=4` | `SNAP_ROW` |
| 工人总数分列 | `k0W=5` ／ `othW=18→21` | `SNAP_ROW` |
| 箱 A 的完成搬运者 | `worker=6`（**k1**·`DISP.v=…/k1`） | §二-③④⑥ |

⛔ 未据此修改接单规则或任何生产代码。

---

## 五、基线对拍（运行前后 · 同命令同域）

域＝`Valley Rampart/Assets/_Game/**`（全 `Assets` 对照另列）；命令＝`git grep -nE <pat> [HEAD] -- <域>`（`Logs/hh341_small_chest_entity_scan.ps1`）；口径＝命中行数/文件数（剔注释另列）。

| 项 | 运行前 | 运行后 | 差异 |
|---|---|---|---|
| `TaskScheduler.Instance\|HasInstance` | 71/17（剔注释 71/17） | 71/17 | **0** |
| `new KingdomTask(` | 15/10（14/9） | 15/10（14/9） | **0** |
| `ITaskScheduler` | 5/2（2/2） | 5/2（2/2） | **0** |
| `ITaskSource` | 49/13（30/12） | 49/13（30/12） | **0** |
| 全 `Assets` 对照（SchedRef） | 167/30 | 167/30 | **0** |
| `git status -- _Game` | 仅 4 既有未跟踪 Art | 同 | **0** |
| `HEAD` | `5442a6a1` | `5442a6a1` | **0** |

---

## 六、观察项登记（`D917` 逐项 5/6 准予 · ⛔ 不归责本源）

1. **卸货段失败组合**（远仓／`DestFull`／地图空间约束）：本轮 A 箱（落 k1 工人旁）**成功走通全链**（含 Done）；对照组＝上一轮 v3（落玩家区·`DestFull`/超时）、v5（同格但零装载）⇒ 登记为**独立观察项**（影响箱源链路成功率的可达性因素；⛔ 不归责本源）。
2. **`k0` 接单不一致**：本轮 k0 接单 **64** 次（主导）；上一轮 v1/v4 曾出现「k0 从未接单」的读数 ⇒ 逐局差异（工人 `IsIdleForTask`/在册状态相关），登记观察（⛔ 不改规则）。
3. **新登记 · 探针停止条件口径瑕疵**：v6 的 P1/P3 停止条件误用了**全局** `BindingManager` 计数（含其他源）⇒ P1 `cap600`／P3 `cap30` **并非本箱未闭合**；**本箱口径**下 A 已全闭合（`live=0`＋`invalidated`＋`inSrc=false`）、B2 已收口（`boxes 2→1`＋`reserv −6`）——见 §二-⑦⑧ 补证。

---

## 七、`L-95` 三段（本轮进局）＋ `O-14` 归档

| 段 | 读数 | 归因 |
|---|---|---|
| **段1 Play 内**（生产链运行期） | `types=["error"]`＝**3 条**：`ruler` 缺脚本／`Theme Style Sheet`／`287 node options` | 三条＝**常驻既有** |
| **段2 退 Play 后** | **4 条**＝上述 3 ＋ **`Some objects were not cleaned up when closing the scene. … [SpriteAnimatorDriver]`** | ⚠️ **新增 1 条**（占位符见下注）：`SpriteAnimatorDriver`＝**既有动画驱动类**（⛔ 非 `ChestEntity` 系；`ChestEntity.OnDestroy` 只做协议收口、无 `new GameObject`）；归属＝**退场清理警告（偶发）**·**单独归档** |
| **段3 空白对照**（只进 Play·不建局） | `types=["error"]`＝**3 条**（同段1，逐条一致） | 常驻既有基准 |

- **生产面（更硬）**：探针内实时 `errs`＝**0**（`error/exception` 订阅计数·全运行期）⇒ 链路运行**零新增未归因错误**。
- **`O-14` 噪声单独归档**：`ruler`／`Theme`／`287 node` 三条（三段共现）；**段2 的 `SpriteAnimatorDriver` 清理警告＝本轮新增登记项**（⛔ 不归 `ChestEntity` 本源新增错误；如需追因可另轮）。
- 每段 `attempt=1`（同一工具 `read_console`）；原始响应：`…_l95b_seg{1,2,3}_err.txt`／`…_l95b_seg{2,3}_stop.txt`（⛔ 未覆盖上一轮 `…_l95_seg*`）。
- **影子面＝0 条**（⛔ 未使用）。

---

## 八、落盘核验闸门 4 步（任务书 §十一-9 · `D917` 逐项 7）

| 步 | 回执 |
|---|---|
| **1 `mtime` 前后变化（正文含实际值）** | **写入前**：本报告**不存在**（`Test-Path=False` 实测 · **2026-09-29 12:15:26**）；**写入后（首写）**＝**`2026-09-29 12:16:25`**（实测 · size=14482 · SHA256=`8312483F2A32FD9FB97B07223D901C97601057E9EFD1A1A4F2811C9B680ECDC6`）；**回填动作后（最终）**＝**`2026-09-29 12:17:33`**（Shell 兜底回填完成实测 · ⛔ 非推算；本次同值写回自身后的 final mtime 见 `gate6.txt` 全序列） |
| **2 磁盘内容重新读取** | `[IO.File]::ReadAllText` 独立重读（⛔ 不复用内存缓冲）；读数与 hash 落 `Logs/hh341_small_chest_entity_gate6.txt` |
| **3 内容 hash / 逐字比对** | SHA256 落盘 + 10 节标题逐个 `Contains` 核验 + 关键证据串计数（`invalidated=True`／`inSources=False`／`id=43`／`k0=64` 等） |
| **4 `git show <commit>:<path>` 对比** | commit（仅本报告 1 文件）后：`git hash-object`（工作区）== `git rev-parse <commit>:<path>`（**逐字节一致**）——值落 `…_gate6.txt` |

> ⛔ 提交面只含本报告 1 文件；`Logs/` 全量命中 `.gitignore`。

---

## 九、自报瑕疵（⛔ 不软化）＋【请裁】

**瑕疵**：
1. **探针停止条件误用全局表计数**（P1 `cap600`／P3 `cap30` 非实质未闭合；本箱口径证据见 §二-⑦⑧）——探针工装侧瑕疵，已用本箱读数补证。
2. **箱 A 的 `id=11` 首现即为 `Pending`**（0.25s 采样间隔内发生「建卡→失败→回待派」，采样未捕获其中间态）——采样粒度限制，如实登记。
3. **`Dead` 仍未观察到（0 命中）**（全窗口 4 种 reason；⛔ 未伪造、⛔ 不写「不存在」）。
4. **段2 新增 1 条清理警告**（`SpriteAnimatorDriver`·既有类）——上轮段2 无、本轮有：**偶发**，已单独归档并登记。
5. **同轮闭合的「同一箱」边界**：⑨ 使用箱 B（同 run·同格）而非箱 A——**结构原因**：A 箱（10 货）在 P1 已**自然搬空**（⑦ 的硬目标前提），**搬空的箱在 `LoadState` 不重建**（`pack.IsZero ⇒ continue`）⇒ 「⑨ 与 ①~⑦ 同箱」与「自然搬空」互斥；故按 `D917` 判据表「⑨＝同轮内的读档段」以 **B 箱（同 run）**闭合。
6. `k0` 接单量跨轮不一致（64 vs 0）——观察项（§六-2），未根因。`n7. **编辑工具再现「报成功但未落盘」**（本轮 Edit 回填被 hash 实测发现**未生效**：mtime/size/hash 与首写完全一致、回填串 0 命中）⇒ 已改用 **Shell 直写兜底并逐条核验**（`REPLACED_OK`＋hash 变化＋关键串命中 1）——如实登记（同族前科再次发生）。

**【请裁】（≤5 条）**：
1. **同轮闭合口径**：①~⑦⑧ 绑箱 A、⑨ 绑箱 B（**同一 run**）是否满足 `D917` 的「同轮落盘」？（理由见瑕疵 5——两者结构性互斥）
2. **⑧ 的「归零」口径**：全局表含其他源（地图箱等）⇒ 以「**本箱份额释放**」（A：卡全终态；B2：`reserv −6`）为准是否可接受？
3. **`Dead` 未观察到**（4 种 reason）——是否以「测试环境低烈度」收口（⛔ 不伪造、不写不存在）？
4. **段2 `SpriteAnimatorDriver` 清理警告**是否需另轮追因（⛔ 不属本源）。
5. **draft** k0 接单不一致是否继续跟踪（⛔ 不改规则）。

---

## 十、源级结论

**`ChestEntity` 源级 ＝ 仍未判绿（遵循 `D917`）；本轮达成「同轮闭合（①~⑦⑧＝箱 A · ⑨＝箱 B · 同一 run）」＋「自然搬空 ⇒ Tick① D 直接落盘」⇒ 移交事务端核实；⛔ 判绿由主策划裁断。**

## 【应登记项】

```
HH.341 | 小源② ChestEntity 补证轮（只读） | 执行端 | 同轮闭合：①~⑦⑧=箱A(-25552·P1段)·⑨=箱B(同run·P2段)；⑦硬目标取得：cnt10→0(tick191)→_sourceInvalidated=True+inSources=False(tick193) 直接落盘；⑧ invVio=0×126·B2收口reserv−6；⑨ 首卡43>oldMax42·tsSame=True；k0分列 64/17；基线前后0漂移；errs=0；L-95b 三段 3/4(含SpriteAnimatorDriver)/3 | ⛔ 未判绿 · 待事务端核实 | 不写 D 号
```

## 【本批写入与提交声明】

本报告为执行端交付件；本轮**零生产码/零资产改动**（源码保持 `872ec5d5` 入档态）；探针与日志全部落 `Logs/`（gitignored）；⛔ 未改本任务书、四本账本或最高优先级文档；⛔ 未 push。