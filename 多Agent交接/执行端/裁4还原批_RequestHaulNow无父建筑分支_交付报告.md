# 裁4还原批 · `RequestHaulNow` 无父建筑分支（只还原 · 不开 `M2`）交付报告

- **编号**：⚠️ 未取（按任务提示「编号由策划端另登」）｜**性质**：**单点还原**（不改行为语义，只把工作区偏离还原回 `HEAD`／`D826` 件 G-2）
- **依据**：`HH.324` 交付报告 §六-4（`9c950dbf`）＋ 策划裁决第 4 条｜`D826` §五 件 G-2 已裁「无父建筑 ⇒ `LogWarning` 后返回，⛔ 不落国库」
- **基线**：`HEAD = 9c950dbf`
- **日期**：2026-09-24

---

## 〇、一句话

✅ **还原完成**：`TaskScheduler.RequestHaulNow(StorageComponent)` 的 `if (b == null)` 分支已与 `HEAD` **逐字一致**；**改后全文件相对 `HEAD` 的 diff ＝ 1 个 hunk / 1 行（仅方法摘要句）**；本批我改动的生产码 **只有这 1 文件 1 处**。

---

## 一、开工前 diff（`git diff HEAD -- <file>` · 开工前）

```diff
diff --git a/Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs b/Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs
index fd649b99..ac704e5b 100644
--- a/Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs
+++ b/Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs
@@ -379,9 +379,7 @@ public class TaskScheduler : Singleton<TaskScheduler>, ITaskScheduler
         var b = st.GetComponentInParent<Building>();
         if (b == null)
         {
-            // ⭐ `M1-G-1c` 件G-2（`D826` §五）：⛔ **不落国库**（`Harvest()` 是"写死玩家国库"的旧旁路，与 `#40` 同族）
-            //   ⇒ 只告警并返回（该仓无父 `Building` ⇒ 广告面不可达 ⇒ 无事可做）。
-            Debug.LogWarning($"[TaskScheduler] RequestHaulNow：储物「{st.name}」无父 `Building` ⇒ 无法立案搬运（⛔ 不落国库）");
+            st.Harvest();   // ⛔ 无父建筑（异常态）⇒ 退回旧口径（保底不丢）
             return;
         }
         b.RequestForceHaulOnce();
```

**开工前 hunk 数 ＝ 1**（恰为待还原处 ⇒ ⛔ 停手条件「diff 多于这一处」**未命中**）。

---

## 二、本批改动（仅 2 处 · 同一方法内）

| # | 位置 | 改动 | 类型 |
|---|---|---|---|
| 1 | 方法摘要末行（原 `:375`） | 「若该仓无父 `Building`（或已不在册）⇒ 退化为**直接 `Harvest()`**（旧口径兜底 · ⛔ 不静默失败）。」⇒ 改为「无父 `Building` ⇒ `LogWarning` 后返回（`D826` 件 G-2，不落国库，不调用 `Harvest()`）。」（删「或已不在册」／「退化为直接 `Harvest()`」／「旧口径兜底」；**摘要不再提"已不在册"**） | 文档句对齐 |
| 2 | `if (b == null)` 分支体（原 `:382`） | `st.Harvest();   // ⛔ 无父建筑（异常态）⇒ 退回旧口径（保底不丢）` ⇒ **还原为 `HEAD` 逐字 3 行**（2 行注释 ＋ `Debug.LogWarning(...)`） | 行为还原 |

**该方法其余语句未动**（逐字保持）：`if (st == null) return;`（`:378`）／`b.RequestForceHaulOnce();`（`:387`）／`RequestHaulNow((ITaskSource)b);`（`:388`）。

---

## 三、改后 diff（`git diff HEAD -- <file>` · 改后）

```diff
diff --git a/Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs b/Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs
index fd649b99..20b76192 100644
--- a/Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs
+++ b/Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs
@@ -372,7 +372,7 @@ public class TaskScheduler : Singleton<TaskScheduler>, ITaskScheduler
     /// 传 `StorageComponent`（⛔ 非 `ITaskSource`）⇒ 取 `GetComponentInParent<Building>`（兼容「箱＝仓」：箱容器挂本体）。
     /// 语义：给该建筑置**一次性强制搬运标记**（`Building.RequestForceHaulOnce`）⇒ 广告侧**先判 ④ 并跳阈值** ⇒
     /// 再复用 `RequestHaulNow((ITaskSource)b)` 立即调度一次（⭐ 派出的任务类型**必须是 `Transport`**）。
-    /// ⚠️ 若该仓无父 `Building`（或已不在册）⇒ 退化为**直接 `Harvest()`**（旧口径兜底 · ⛔ 不静默失败）。</summary>
+    /// ⚠️ 无父 `Building` ⇒ `LogWarning` 后返回（`D826` 件 G-2，不落国库，不调用 `Harvest()`）。</summary>
     public void RequestHaulNow(StorageComponent st)
     {
         if (st == null) return;
```

⇒ 全文件相对 `HEAD` **只剩摘要句 1 行**；**函数体因 diff 不显示即与 `HEAD` 逐字相同**。

---

## 四、判据 1~3 命令输出

### 判据 1 ✅ `git --no-pager diff HEAD -- <file>` ＋ hunk 计数

```
=== 判据1：改后 diff HEAD（应只剩摘要句一处）===
@@ -372,7 +372,7 @@ ...（仅摘要句 1 行，见 §三）
=== hunk 数: 1 ===
```

✅ **达成**：diff 只剩方法摘要 1 行；函数体与 `HEAD` 逐字相同（diff 无该区段）。

### 判据 2 ⚠️ **代码面 0 ✅ ／ 字面 2（须策划端明示口径）**

```
=== 判据2：RequestHaulNow(StorageComponent) 体内 Harvest( 计数 ===
方法区间 376 .. 389
代码面（剔除整行注释）Harvest( 计数 ===  0
字面（含注释）Harvest( 计数      ===  2
   · :375 新摘要句「…不调用 `Harvest()`）。」
   · :382 HEAD 自带注释「…（`Harvest()` 是"写死玩家国库"的旧旁路…）」
```

方法体逐行（改后）：

```
376:    public void RequestHaulNow(StorageComponent st)
377:    {
378:        if (st == null) return;
379:        var b = st.GetComponentInParent<Building>();
380:        if (b == null)
381:        {
382:            // ⭐ `M1-G-1c` 件G-2（`D826` §五）：⛔ **不落国库**（`Harvest()` 是"写死玩家国库"的旧旁路，与 `#40` 同族）
383:            //   ⇒ 只告警并返回（该仓无父 `Building` ⇒ 广告面不可达 ⇒ 无事可做）。
384:            Debug.LogWarning($"[TaskScheduler] RequestHaulNow：储物「{st.name}」无父 `Building` ⇒ 无法立案搬运（⛔ 不落国库）");
385:            return;
386:        }
387:        b.RequestForceHaulOnce();
388:        RequestHaulNow((ITaskSource)b);
389:    }
```

⚠️ **列报（两判据字面互斥 · 本端未擅改，请裁）**：**判据 1** 要求「函数体与 `HEAD` 逐字相同」，而 `HEAD` 的函数体**自带**含 `Harvest()` 字样的注释行（`:382`）＋新摘要句亦含该词 ⇒ 若 **判据 2** 按**字面** grep 要求 `Harvest(` ＝ 0，则只能删改 `HEAD` 自带注释或摘要句 ⇒ **与判据 1 冲突**。
本端处置＝**取「代码面（调用）＝ 0」解释**（`:382` 为**说明性注释**、`:384` 已改为 `LogWarning`），⛔ 未改 `HEAD` 文本。

### 判据 3 ✅ 本批生产码改动面

```
=== 判据3：本批生产码改动面 ===
 Valley Rampart/Assets/Scenes/GameScene.unity                        | 6 +++++-
 .../Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs         | 2 +-   ← 本批（1 行）
```

- ✅ **本批我改动的生产码 ＝ 仅 `TaskScheduler.cs` 1 文件 1 处**（`2 +-` ＝ 摘要句 1 −/1 ＋）。
- ⚠️ `GameScene.unity`（`5+/1−`）＝ ⛔ **前序脏点，非本批**：① 开工前的会话快照即在 `M` 列；② 内容与本批无关（`m_IsActive: 1→0` ＋ `ruler` PrefabInstance 的 `m_IsActive: 0`）；③ 本批未碰该文件（未开 Unity、未写资产）。

---

## 五、停手条件检查（三项均 **未命中**）

| 条件 | 结果 |
|---|---|
| 开工前 diff 多于这一处 | ⛔ 未命中（hunk 数 ＝ **1**） |
| 要还原就必须改本方法以外的代码 | ⛔ 未命中（改动全在本方法内） |
| `HEAD` 上该分支已不是 `LogWarning` 后返回 | ⛔ 未命中（`HEAD:380-386` 实读＝注释 ＋ `LogWarning` ＋ `return`） |

---

## 六、红线声明

- ⛔ 未 commit · ⛔ 未 push（按要求：本报告留在工作区，不提交）
- ⛔ 未开 `M2`／未改交互表／未改 `05`／`06`／`中层执行计划`
- ⛔ 未碰其他脏文件（`GameScene.unity`／`Packages`／美术／`pixel-forge`／前序报告）
- ⛔ 未整文件覆盖（`replace_in_file` 两处定点替换；未新增/删除 `.cs` ⇒ 无 `.meta` 事项）
- ⛔ 未编译、未跑局（判据 4：已裁行为的还原，非新功能）
- 行尾：`git ls-files --eol` ＝ `i/lf w/lf`（改前改后一致，未变行尾）

---

## 七、零方案声明

本报告只含：开工前 diff、改动两处、改后 diff、判据 1~3 命令输出、互斥列报。⛔ 无方案、无建议、无后续动作提议。
