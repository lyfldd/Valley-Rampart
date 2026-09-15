# HH.288 军事建造需求口径治本批 交付报告（HH.285 重做·`DZ-105` ㉔ ＋ `DZ-156` ㉑）

> 类型：交付报告｜状态：🟢 **已交付·七条验收线全过（探针 8/8 PASS）**
> 日期：2026-09-15 · 执行端 · 承：HH.285 开工回执（D732 重做版·同串）
> 取号：**HH.288**（账本水位线 287→288 · `D640 #10` 禁预留）
> HEAD 基线：`6865457a` · 未 push
> 原始证据：`Valley Rampart/Logs/hh285_probe.log`（探针全量输出）

---

## §一、一句话结论

**治本落地**：id25 `need 17→21`（㉔ 建投掷机厂·`ExclusiveGap` 恒 0.5 → `MachineDemand` 态势驱动）、id20 `need 17→22`（㉑ 建地脉熔炉·对齐 `MilitaryBuildingGap`）——**asset 恰 2 行、零 `.cs`、探针 8/8 PASS**（ⓐ 态势驱动 None=0.000/Alert=0.800 精确命中理论值·ⓑ ㉑ 缺口驱动+族门禁双向·ⓒ 互斥链四象限全验）＋回归 M7 6/6＋Race ALL PASS。**`HH.284` 第二步前置已备**。

---

## §二、验收线逐项证据（任务书 §四·七条）

### 线1｜改动面最小（核心）——✅

`git diff -- ...UtilityActionConfig.asset` **恰 2 行**（各 1 删 1 增）：

```diff
@@ -379,7 +379,7 @@ MonoBehaviour:      （id=20 建地脉熔炉 块内）
     minStage: 2
     axis: 1
     axisWeight: 1
-    need: 17
+    need: 22
     needA: 1
@@ -474,7 +474,7 @@ MonoBehaviour:      （id=25 建投掷机厂 块内）
     minStage: 3
     axis: 0
     axisWeight: 1
-    need: 17
+    need: 21
     needA: 1
```

### 线2｜静态直读——✅

**探针实读**（`LoadConfig()` 真资产·非 git 层）：

```
[PASS] 静态直读 ㉔ need==21：True｜㉑ need==22：True
[INFO] 静态直读：Find(BuildSiegeWorkshop).need=21（期望 21=MachineDemand）
[INFO] 静态直读：Find(BuildLeyForge).need=22（期望 22=MilitaryBuildingGap）
```

**其余 25 条逐条未变·双证**：①`git diff` 恰 2 行（上）＝其余无任何改动行；②探针 **27 条全遍历**（`hh285_probe.log` 全表）与任务书 §2.1 现状表逐条对照一致——id26=21（未动·对照真源）、id18/19/21/23/24=22、id22=18、id7=8…全部原值；need=17 消费者归零（M5）。

### 线3｜行为探针（核心·正门·真 def 防假通过）——✅ 8/8 PASS

**铁律落实**：全部从 `UtilityActionConfig.LoadConfig().Find(...)` 取资产真 def（`hh285_probe.log` 静态直读段即真 def 实读）——**未自造任何 `UtilityActionDef`**（D732 §3.1① 两先例硬编码陷阱规避）。

**ⓐ ㉔ 评分前后对照（真 def·军事期注入·注入复原）**：

```
[PASS] ⓐ ㉔ 态势驱动（真 def·军事期注入）：None档=0.000(0)／Alert档=0.800(>0)·改前恒0.500→改后态势驱动
```

- 改前基线＝`HH.285`（原跑局报告）实测「㉔ score 恒 0.500」（ExclusiveGap 占位·`D732` 收编）
- 改后：None 档（无威胁+姿态 None）＝**0.000**（态势空 ⇒ 0）；Alert 档＝**0.800**（`wantM=max(1,needA=1)`·`MachineCount=0` ⇒ `1/(1+0)×0.8=0.8`·**M1 理论值精确命中**）
- 注入法照先例（`scriptPhase=Military` ＋ `SituationHub.Put`/`PostureHub.Put`·用毕复原）

**ⓑ ㉑ 进池（真 def·缺口驱动＋族门禁双向）**：

```
[INFO] ⓑ 族探测：dwarfId=2 nonDwarfId=1（k1~k4 实读 KingdomRace）
[PASS] ⓑ ㉑ 进池（真 def·MilitaryBuildingGap）：矮人局缺口>0＋Feasible true＋非矮人拦截
```

- 矮人局 k2（raceId=2=LeyForge.raceId）＋军事缺口快照（`GeneralCount=0<limit2`）⇒ `NeedScore>0`（`MilitaryBuildingGap`＝三军事缺口 max）；资源注入下 `Feasible=true`（族门禁过·`uniquePerKingdom` 未建）；非矮人 k1 ⇒ `Feasible=false`（`:574` raceId 拦截·负例）

**ⓒ 互斥链（`UtilityScorer.Feasible` 直调·四象限）**：

```
[PASS] ⓒ-1 无厂：Feasible(㉔)=True（期望 true·资源已注入）／Feasible(㉕)=False（期望 false·:616 厂前置）
[PASS] 建厂提交：spot=True built=True（FindAIBuildSpot+TryBuild·先例 P9b 同法）
[PASS] 厂 Active=True（等 4 日）
[PASS] ⓒ-2 建厂后：Feasible(㉔)=False（期望 false·:577 cap=1）／Feasible(㉕)=True（期望 true·厂前置+prefab 已备 HH.282）
```

⇒ **㉔/㉕ 共用 `MachineDemand` 不双高分**（M3 实证闭环）。

### 线4｜零代码——✅

本批 `.asset` 外唯一工作区改动＝第一步容器清理（Editor-only 观测域·D732 明令：`Valley_DiagMilitary.cs` 回退 27 行【净零·逐字还原】／`Valley_HH285_Observe.cs(.meta)` 删＝`Valley_HH284_Probe.cs(.meta)` 增【改名·guid 同步】／`Valley_HH285_Probe.cs` 新建【本批探针容器·独立】）。**业务代码零改动**：`UtilityScorer.cs`/`KingdomBrain.cs`/`UtilityActionConfig.cs` 及全部 `Assets/_Game/**.cs` **逐字未动**（`git status` 佐证）；`AI.Core` 零触。

### 线5｜sim-sync——✅ 零义务

纯数据层：改动为 Unity 侧 Kingdom AI 行动配置（KingdomBrain 效用层），sim 战场层无对应物；不触决策核/三方同步面/champion/场景 JSON ⇒ 双端 MD5 义务=0、15 账本登记义务=0（详见回执 §五）。

### 线6｜回归——✅

| 项 | 结果 |
|---|---|
| 编译 | `refresh_unity(compile=request)`＋`read_console(types=[error])`＝**0 error**（唯一条目=良性编辑器消息「233 node options」） |
| `2_20B_M7` | **6/6 轮 ALL PASS**（四族 seed22360＋换 seed 7841/31337·逐轮汇总见 Editor.log `[2_20B冒烟] 第 N 轮汇总 ALL PASS`） |
| `2_20_Smoke_Race` | **ALL PASS**（`[2_20冒烟] ===== ALL PASS（种族域 D467~D472 行为级探针）=====`） |
| 收尾三态（L-32） | 探针/冒烟均 `ExitTestRun`（或终速 0）＋`QuitSmoke`＋退 Play 自动执行 ✅ |

### 线7｜写后验——✅

本批相关路径 `git status` 全空（提交后）；hash 见 §五；**未 push**；无临时改动需还原（注入态全部运行时复原·资产改动即交付物本体）。

---

## §三、L-34 五列（探针判据）

| 列 | ⓐ ㉔ 态势驱动 | ⓑ ㉑ 进池 | ⓒ 互斥链 |
|---|---|---|---|
| 可判定最早时点 | 注入即判（EnterTestRun 就绪后·真暂停态） | 同左 | ⓒ-1 注入即判；ⓒ-2 需厂 Active（WaitDays(4)·L-20） |
| 命中即停 | 不适用（单点断言·非长局） | 同左 | 同左 |
| 服务验收句 | 「㉔ 评分由恒定 0.5 变态势驱动」：None 档 0／Alert 档 >0（理论 0.8） | 「㉑ 缺口口径对齐族专属建筑」：缺口 >0＋矮人可行/非矮人拦截 | 「㉔/㉕ 天然互斥」：无厂 ㉔✓㉕✗→建厂后 ㉔✗㉕✓ |
| 作用域 | 真资产 id25 def × k1（注入军事期/态势·复原） | 真资产 id20 def × k2(矮人)＋k1(非矮人负例) | 真资产 id25/id26 def × k1（含真实建厂链） |
| 口径来源／排除项 | `NeedScore`（公开 API）·排除项=非注入态（复原后不受残影）；改前基线=`D732` 收编 0.500 | `NeedScore`＋`Feasible`（反射只读·HH.282 同法）·排除项=资源注入只影响 Feasible 不影响 need | `Feasible` 直调＋执行侧 `TryBuild` 真链（L-31 双向）·排除项=玩家国 id=0 不涉及 |

---

## §四、与上批（打回版）的差异声明

| 项 | 打回版（`69998be6`） | 本版 |
|---|---|---|
| 交付物 | HH.284 跑局活（误配） | **asset 治本 2 行**＋探针 8/8 |
| `.cs` 改动 | 2 个（DiagMilitary 尾插＋新容器） | **业务零改**；Editor 观测域＝D732 明令的清理＋新探针容器 |
| 容器 | `Valley_DiagMilitary` 违裁尾插＋误名 `Valley_HH285_Observe` | 尾插回退迁入 `Valley_HH284_Probe`（正名）＋`Valley_HH285_Probe`（本批独立·禁改 DiagMilitary 遵守） |
| 长局 | 跑了 120 日 | **不跑**（§3.1④·NeedScore 容器直调） |

---

## §五、commit

- 本批单笔 commit（容器清理＋asset 治本＋探针＋回执/报告/账本·写-改-commit 同串）：**`bc8a50df`**（11 files·+580/−51·`Valley_HH285_Observe.cs.meta → Valley_HH284_Probe.cs.meta` 改名识别 100%）
- **未 push**

## §六、移交

- `HH.284` 第二步（㉕机器实产读数）**前置已备**：治本后 ㉔ 由恒 0.5 占位变态势驱动·军事期竞争格局改变 ⇒ 上批「环1 自建被压制」根因已按 DZ-068 同族思路解除一半（评分侧）；木链（`DZ-159`）**不混入本批**（另批待排）——长局读数若再现 0 厂，届时按「评分已通×执行缺木」分域归因。
- 待策划端：验收 HH.288 ＋ 放行 HH.284 第二步。

---

> 执行端｜2026-09-15｜HH.288 军事建造需求口径治本批：七线全过·探针 8/8·回归全绿·零 .cs·未 push
