# HH.246 HH.245 返工报告（#3 极性修复）（执行端 → 主策划端）

> 返工依据：`0.6_审查决策记录.md` §二百二十四（**D695**，2026-09-13，主策划端）§8.5 返工要求
> 原始交付：`多Agent交接/执行端/HH.245_1D残留全向索敌失明修复批_交付报告.md`
> 交付日期：2026-09-13　｜ 完成报告号＝**HH.246**（按账本实时水位线取号 · 先登记后落盘 · D640 #10 禁预留）
> 状态：🟡 **待验收**

---

## 〇、一句话结论

按 D695 §8.5 只改一处：`MonsterController.FindNearestHuman` 第 111 行 `Faction.PlayerCamp` → **`GetFaction()`**（一行 token）；**`kingdomId==0` 守卫与外层逻辑零改动**，**已成立的 5 处（`#1/#2/#4/#5/#6`）零触碰**（`git diff` 佐证）；编译 **0 错**；**必补实证已完成且为决定性**——把 `kingdomId==0` 的玩家单位置于该怪 `rangeWorld` 内，**旧码返回 `NULL`（找不到人）／新码返回该玩家（`Worker k0 dist=2.00`）**，且额外验证了 `kingdomId==0` 守卫在更近的 `k>0` 冒充单位在场时仍正确生效。

---

## 一、改动清单（只提本串文件）

| # | 文件 | 性质 | 规模 |
|---|---|---|---|
| 1 | `Assets/_Game/Systems/Disaster/MonsterController.cs` | **改**（D695 §8.5-1 指定单点） | 1 行 + 3 行注释 |
| 2 | `多Agent交接/执行端/HH.246_HH245返工_#3极性修复_交付报告.md` | 新建（本报告） | — |
| 3 | `多Agent交接/_编号登记.md` | 取号 HH.246（单行；已独立 commit `a47d0d6`） | 1 行 |

**未触碰核验**：
```
$ git diff --stat -- 'Valley Rampart/Assets/_Game/Systems/AI/NPCBrain.cs' \
                    'Valley Rampart/Assets/_Game/Systems/Combat/ProjectileManager.cs' \
                    'Valley Rampart/Assets/_Game/Systems/Unit/UnitController.cs'
（空 —— 已成立 5 处零触碰，符合 D695 §8.5-3）
```

---

## 二、D695 §8.5 返工要求逐条兑现

| 要求 | 兑现 | 证据 |
|---|---|---|
| **1. 改 `#3`**：第 111 行 `Faction.PlayerCamp` → `GetFaction()`；保留 `kingdomId==0` 守卫与外层逻辑不变 | ✅ | §三·1 diff |
| **2. 补实证**：`kingdomId==0` 玩家单位置于该怪 `rangeWorld` 内，证明 `FindNearestHuman` 返回该玩家（**旧码 vs 新码对照**） | ✅ | §三·2 |
| **3. 不动已成立的 5 处** | ✅ | §一 未触碰核验 |
| **4. `DZ-153/154` 不在本批范围**（只登记） | ✅ 未改 | 本报告零涉及 |
| **5. 返工后报告按账本实时水位线取号；写-改-commit 同串、只提本串文件、不 push** | ✅ | 取号 `a47d0d6`；未推送 |

---

## 三、硬证据

### 1. 改动 diff（`#3` 极性修复）

```diff
--- a/Valley Rampart/Assets/_Game/Systems/Disaster/MonsterController.cs
+++ b/Valley Rampart/Assets/_Game/Systems/Disaster/MonsterController.cs
@@ -108,7 +108,10 @@ public class MonsterController : UnitController
     public IDamageable FindNearestHuman(float rangeWorld)
     {
         if (UnitRegistry.Instance == null) return null;
-        PerceptionSystem.QueryNearby(_rb.position, rangeWorld, Faction.PlayerCamp, true, _queryResults);
+        // D695 返工：此处须传 GetFaction()（＝Faction.Monster），不可传 Faction.PlayerCamp。
+        // QueryNearby 的 findEnemies=true 语义＝收「f != myFaction && f != None」（PerceptionSystem.cs:38-43），
+        // 传 PlayerCamp 会把玩家整个排除、返回非玩家阵营 ⇒ 本方法永远返回不了"人"（功能回归）。
+        PerceptionSystem.QueryNearby(_rb.position, rangeWorld, GetFaction(), true, _queryResults);
         IDamageable nearest = null;
         float nearestDist = float.MaxValue;
         for (int i = 0; i < _queryResults.Count; i++)
```

**外层逻辑与 `kingdomId==0` 守卫**（第 112-126 行）：**逐字未改**。

### 2. 必补实证（D695 §8.5-2 · 旧码 vs 新码对照）

**环境**：正门 `SmokeApi.EnterGame`（`TestHarnessApi.EnterTestRun` 同链）建局 `seed=21140 / Medium / difficulty=2`，`state=Playing`、`units=39`；怪物 fixture 经**正式链路** `MonsterSpawner.Spawn(Resources.Load<MonsterDef>("Config/Disaster/Raider"), pos)` 生成。

**样本构造**：确认首个 `PlayerCamp ∧ kingdomId==0` 玩家单位（`Worker`）位置，怪生成于其 **2.00 世界单位**处（远小于所用 `rangeWorld`）。

#### 2a. 主对照（`rangeWorld = 10`）

```
monFaction = Monster          range = 10
OLD(PlayerCamp) res = NULL            rawBufCount = 1
NEW(GetFaction) res = Worker k0 dist = 2.00
```

**判读**：
- **旧码**：`QueryNearby(pos, 10, Faction.PlayerCamp, findEnemies:true, buf)` ⇒ 收「f ≠ PlayerCamp ∧ f ≠ None」⇒ 结果集里**根本没有玩家**，`FindNearestHuman` 返回 **`NULL`** ⇒ **怪找不到人**（D695 所指功能回归，实测坐实）。
- **新码**：传 `GetFaction()` = `Faction.Monster` ⇒ 收「f ≠ Monster ∧ f ≠ None」⇒ 玩家进入结果集 ⇒ 返回该玩家 `Worker k0`，`dist=2.00` ⇒ **语义复原**。

#### 2b. 旧码 buffer 内容（说明为何返回 NULL）

```
OLD_PolarityBuffer(count=1): [MonsterController fac=Monster k=0 IS_THIS_MONSTER]
```

⇒ 旧极性下，`rangeWorld=10` 内**唯一**被收进结果集的是**怪自己**（`f=Monster ≠ PlayerCamp`）；随后被循环内 `uc == this` continue 剔除 ⇒ 结果 `NULL`。**这精确复现了 D695 的极性分析**。

#### 2c. `kingdomId==0` 守卫负例（越近的冒充态须被排除）

```
realPlayer  dist = 2.00                     （真玩家 k=0）
fakeAiK1    fac = AiKingdom k=1 dist = 0.40  （比真玩家更近）
RESULT => Worker k0 dist = 2.00
判读 = ✅ 守卫生效：更近的 k>0 冒充态被排除，返回真玩家 k=0
```

**判读**：构造一个 `PlayerCamp` 出身但 `kingdomId=1` 的冒充态（`UnitFactory.SpawnUnit(Faction.PlayerCamp, Occupation.Civilian, pos, kingdomId:1)`，其 `EffectiveFaction` 派生为 `AiKingdom`），置于**比真玩家更近**处（0.40 < 2.00）。结果 `FindNearestHuman` **仍返回真玩家 `k=0`**（而非更近的 `k=1`）⇒ **`kingdomId==0` 守卫按 D695 §8.5-1 要求逐字保留且确实生效**（2_17 步骤4 patch C ③ 的 AI 工人盲区防护未被本次修复破坏）。

### 3. 编译（0 错）

```
read_console(types=["error"]) → 1 条：
  "233 node options failed to load and were skipped."
⇒ 存量无关项（本批前既存）；本批改动零 error / 零 warning。
```

### 4. 收工纪律

- 进局走正门（`GameStateManager` 达 `Playing`、`activeMap=True`）。
- 实证结束 **退 Play**（`manage_editor stop` 成功）· `L-32`。

---

## 四、口径与边界声明

1. **本批只改一行**：除 `#3` 极性外，**未新增/未修改任何其他逻辑**（含注释仅为记录返工理由）。
2. **关于"冒烟兜底"**：D695 §8.6 已确认怪物侧路径**被 T11 考跑守卫静默**（`MonsterSpawner.Spawn` 在 `TestHarnessMode` 下 `return null`）⇒ 常规冒烟对怪物侧为**结构性零覆盖**。故本次实证**未依赖冒烟容器**，而是走正门建局后以正式 spawn 链路 + 反射直调真实方法完成——**这正是 D695 要求的"唯一兜底"路径**。
3. **实证性质**：运行时行为观测（非新增探针容器、非新增产品代码）；所有构造均在 Play 会话内，退 Play 后即销毁，**无资产/场景残留**。
4. **未列报新事项**（`DZ-153/154` 归策划端登记；"怪物侧冒烟盲区"由策划端另记，见 D695 §8.6）。

---

## 五、锚点声明

| 项 | 值 |
|---|---|
| 取号 commit | `a47d0d6`（`多Agent交接/_编号登记.md` 单行，水位线 `HH.245 → HH.246`） |
| 实施 commit | **本报告所在 commit（`git log -1 --format=%h` 自证）** |
| 分支状态 | `main`，**保持未推送态** |
| 改动规模 | `MonsterController.cs`：1 行（+ 3 行注释） |

---

## 六、回执区（策划端）

- [ ] 验收 D695 §8.5 五项返工要求
- [ ] 复核 §三·2 必补实证（旧码 vs 新码对照＋守卫负例）
- [ ] 判定 HH.245 是否可销号
