# `HH.323` `O-22` **性能甲案小批** 任务书（`QueryNearbyUnits` 单遍化）

- **签发**：策划端（砚）｜**`D858`**｜**日期**：2026-09-24
- **性质**：**施工**（⚠️ **单文件** · 低风险）
- **依据**：`D857` 裁定 3（`O-22` 甲案立项）｜评估报告 `多Agent交接/执行端/HH.322_伤害域收尾_只读评估报告.md` §3.3 甲案
- **前置**：⛔ 无（不依赖其他在飞批）

---

## 〇、一句话

`ProjectileManager.QueryNearbyUnits` 现对**每发投射物**做 `(2·subRange+1)² ＝ 25` 次 `GridSystem.GetUnitsInSubCell`（每次 `new List` ＋ 全表扫）⇒ ⭐ 改为**单遍** `UnitRegistry.GetUnitsEnumerator()` ＋ 命中半径筛 ⇒ **25 遍 → 1 遍**。

---

## 一、落点（**本端实读** 2026-09-24 · ⛔ 不得凭报告转述）

| 项 | 位置 |
|---|---|
| 待改函数 | `ProjectileManager.cs:363-385 QueryNearbyUnits`（签名 `private List<UnitController> QueryNearbyUnits(Vector2 worldPos, float radiusCells)`） |
| 唯一调用者 | `ProjectileManager.cs:213` `List<UnitController> candidates = QueryNearbyUnits(p.targetPos, hitRadiusCells);` |
| 现状实现 | `:374 int subRange = GridMath.SubWindowForVisualRadius(radiusCells, subDiv);` ⇒ `:376-380` 双层循环 `result.AddRange(GridSystem.Instance.GetUnitsInSubCell(...))` ＝ **25 次**（`subRange = 2`） |
| 被调方成本 | `GridSystem.cs:536 GetUnitsInSubCell` ＝ `new List` ＋ `foreach (_unitSubCells)` **全表扫** |
| ⭐ **收敛口（已备 · 无需改）** | `UnitRegistry.cs:57` `public HashSet<UnitController>.Enumerator GetUnitsEnumerator() => _aliveUnits.GetEnumerator();`（⚠️ 路径 ＝ `Assets/_Game/Systems/Unit/UnitRegistry.cs`） |
| ⭐ **同构先例（照抄）** | `CombatRules.cs:213-214` **已**用 `GetUnitsEnumerator()` 单遍（`P2` 硬约束①/②）⇒ 本批与之**同法** |
| 距离判据口 | `GridMath.cs:89 DistVisual(Vector2, Vector2)`（视觉格） |

---

## 二、⛔ 必做前置（**先报后改** —— 未报不得动生产码）

1. ⭐ 实读 `ProjectileManager.cs:213` **之后** `candidates` 的**全部使用面**（排序／取最近／遍历／早退／是否被传往下游），判断：**候选从「微格窗超集」变为「命中半径精确集」（语义更严）是否改变调用方行为**？
   - ⚠️ 若调用方**依赖超集**（如"距离稍远者也须参与某判定"）⇒ ⭐ **停手报裁**，⛔ **不得**自行扩半径补偿。
   - ⭐ 若调用方只做「最近/首个命中」类判定、或语义等价/更严 ⇒ **直接施工**。
2. 报**改前对照口径**：`radiusCells` 与该调用点实参（`hitRadiusCells`）的实值 ＋ `subRange` 现算值。

---

## 三、施工（3 点）

1. `QueryNearbyUnits` 内**删** 25 次微格窗循环 ⇒ 改**单遍**：
   - 走 `UnitRegistry.Instance.GetUnitsEnumerator()` **结构枚举**（⚠️ **零装箱** · 照 `CombatRules.cs:213-214` 写法）；
   - 逐单位以 `GridMath.DistVisual(worldPos, unitPos) <= radiusCells` 筛（⚠️ 半径口径 ＝ **视觉格**，与调用点实参**同口径**）。
2. ⚠️ **保留** `GridSystem.Instance == null` 类守卫；若新实现不再需要 `WorldToSubCoord` ⇒ 相关守卫可随之简化，**但须在报告中说明**。
3. ⛔ **只动 `ProjectileManager.cs` 一个文件**；⛔ 不改 `UnitRegistry`／`GridSystem`／`GridMath`／`CombatRules`。

---

## 四、判据（逐条给实测读数 · ⛔ 禁形容词）

| # | 判据 | 口径 |
|---|---|---|
| **1** | ⭐ **功能等价（核心）** | 同一构造下 **改前/改后逐发对照**：命中单位集合**一致**或**更严**｜⛔ **不得出现"改前命中、改后漏"**｜⚠️ 须给 **≥10 发**对照读数 ＋ **≥1 例边界**（单位恰在半径边缘／恰在窗角） |
| **2** | ⭐ **零分配** | 每次调用**零堆分配**｜⚠️ **必须先做仪器校验**（**`L-93`**：用已知量的真值标定仪器 —— 例 `1000×new byte[1024]`；⚠️ 若仪器报 `0 B` ⇒ **先疑工具失效**；⛔ 不得直接采信首次「0 B」；⚠️ 须声明所用仪器） |
| **3** | **耗时对照** | 改前（25 窗）vs 改后（1 遍）**带测法**读数（同载荷 · ≥3 轮） |
| **4** | ⭐ **并发在飞发数** | 给「**每帧峰值在飞投射物数**」（"值不值得"的量级锚）｜⚠️ 取不到 ⇒ 如实写「未测」＋ 说明需要什么场景 |
| **5** | 编译／回归 | `errors 0`｜⚠️ 相邻面（建筑兜底链 `:216` 起）行为不变 |
| **6** | 红线 | ⛔ **未动其他文件**（`git diff --stat` 证据）｜⛔ 未 push |

---

## 五、停手条件

① 必做前置判明**调用方依赖超集** ⇒ 停手报裁；② 判据 1 出现**任何"改前命中、改后漏"** ⇒ 停手报裁；③ 发现 `:213` **之外**还有第二调用点 ⇒ 列报。

---

## 六、交付物

① 改动清单（`file:line`）② 判据 1~6 读数（含**边界例**）③ ⭐ **改前/改后 耗时 ＋ 分配 双列对照** ④ §待裁条数 ⑤ 未 push 声明
