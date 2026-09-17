# HH.304 · `HH.294` 底层重构批 · 片 5（资源池落地）交付报告（待策划验收）

> **端**：执行端 ｜ **批次**：`HH.294` 底层重构批 · **片 5 · 资源池落地**（`底层执行计划.md` §二「批 4 · 资源池落地」）
> **要求源**：`多Agent交接/策划端/HH.294_片5_5-0报裁_裁决.md`（`D773`）＋ 用户拍「甲」（`D774`）
> **口径源**：`最高优先级文档/03_地图即数据库.md` §6.7 / §6.7.1（**定案版**）/ §6.7.2 / §6.8 A 类 ｜ `04_上层接入指南.md` §六 五条不许
> **交付时间**：2026-09-17 ｜ **行尾**：全部改动文件 **LF**（实测，见 §六）｜ **取号**：按水位线（`HH.303` → **`HH.304`**）
> ⚠️ **账本纪律**：按 `D767` 新纪律「**执行端不代写策划端四档账本**」，本次**未改** `_编号登记.md`／`_当前快照.md`／`_任务队列.md`／`测试基线台账.md` ⇒ **应登记项见 §四·末「应登记项声明」**（同 `HH.298`/`HH.299` 做法）
> **证据**：`Logs/hh294_s5_probe_edit.log`（编辑态）／`Logs/hh294_s5_probe_live.log`（实机正门）／`Logs/hh294_s5_probe_live_prior.log`（前一跑次留存）
> **未 push** ｜ 具名 `git add`（未用 `-A`/`-u`/`.`）

---

## 〇、一句话结论

`2-1`~`2-5` 与 `4-A`~`4-F` **全部落地**并取得实测读数：权重表落盘（四带实变 **16** 格）／`poolCapBase=96` 在场·`resourcesPerChunkBase` 归零／难度乘在 `cap`／6.6 `target` 改开局目标／**删第二日历**／生成期不铺满／配额按锚点／重生落新位（**8/8 不在原位**）／池子模型＋单类上限＋每日补量＋分帧分摊。
**判据 1~14 逐条读数见 §一**。**3 项列报**（§四）：`P-5` 矿洞「1 簇＝1 点」单位口径落地式（**已实测自洽**，但请策划端追认）／`P-6` 日补量曲线第 3/6/7 天与 `03` §6.7.2 表有 **±1** 偏差（闭式 vs 表值取整差）／`P-7` 判据 3 能力句「±20% 窗」在**第 1~3 天不成立**（开局 40% 与当天目标重叠所致，非缺陷）。

---

## 一、判据逐条读数

### 判据 1 · ⭐ 权重表落盘（逐带逐字段 diff 面）＋ 重跑 `A3`/`A4`/`A5`

**① SO 资产逐字段 diff 面（改前 → 改后）**——`MapGenRulesConfig.asset`：

| 带 | 字段 | 改前 | 改后 | |
|---|---|---|---|---|
| 热带 | `tree` | 0.20 | 0.20 | 同 |
| 热带 | `stonePile` | 0.45 | **0.50** | ★改 |
| 热带 | `woodPile` | 0.45 | **0.30** | ★改 |
| 热带 | `oreVein` | 0.50 | **0.70** | ★改 |
| 热带 | `mine` | 0.90 | **0.95** | ★改 |
| 亚热带 | `tree` | 0.20 | 0.20 | 同 |
| 亚热带 | `stonePile` | 0.40 | **0.50** | ★改 |
| 亚热带 | `woodPile` | 0.40 | **0.30** | ★改 |
| 亚热带 | `oreVein` | 0.45 | **0.70** | ★改 |
| 亚热带 | `mine` | 0.84 | **0.95** | ★改 |
| 温带 | `tree` | 0.25 | **0.20** | ★改 |
| 温带 | `stonePile` | 0.40 | **0.50** | ★改 |
| 温带 | `woodPile` | 0.40 | **0.30** | ★改 |
| 温带 | `oreVein` | 0.40 | **0.70** | ★改 |
| 温带 | `mine` | 0.80 | **0.95** | ★改 |
| 寒带 | `tree` | 0.60 | 0.60 | 同（**保留「树最少」**） |
| 寒带 | `stonePile` | 0.45 | **0.50** | ★改 |
| 寒带 | `woodPile` | 1.00 | 1.00 | 同（**保留「不生成木堆」**） |
| 寒带 | `oreVein` | 0.55 | **0.70** | ★改 |
| 寒带 | `mine` | 0.92 | **0.95** | ★改 |

⇒ **逐带逐字段 20 格，实变 16 格**（读数命令与输出片段：编辑态探针 §判据 1）

```
  [温带] 树：0.25 → 0.20  ★改
  [寒带] 树：0.60 → 0.60   同
  [寒带] 木堆：1.00 → 1.00   同
  ⇒ 逐带逐字段共 20 格，实际变更 **16** 格
  实盘断言：热=亚热 True；亚热=温 True；四带 mine 全 0.95 True；寒 tree=0.60；寒 woodPile=1.00（1−w=0 ⇒ 木堆占比 0）
```

**② 重跑 `HH.291`/`HH.292` 的 `A3`/`A4`/`A5` 对照（不得退化）**：

| 项 | 改前基线 | 本轮实测 | 判定 |
|---|---|---|---|
| `A3` 地带丰度（四带 `T = cap` 实测均值） | `108.6/155.9/130.8/84.2`（旧 `T` 口径 120 基准） | `86/125/106/67`（**新 `cap` 口径**·`M7` 均值 **96.0**） | 口径已换（`P-2`/`P-3` 裁决所致）·**与 `03` §6.7 表逐带一致** |
| `A4` 坑位（max 坑位/格） | **4** | **4**（须=4） | **未退化** |
| `A4` 满格数 | 6824 | **1962** | ⚠️ **见下** |
| `A4` 全域 >4 计数 | 0 | **0** | **未退化** |
| `A5` 空区块 | 0 | **0**（min=26 均=40.8 max=55） | **未退化** |

⚠️ **满格数 6824 → 1962 的归因**：这是 `4-B`（生成期**不再铺满**·落格目标改开局目标 `E_i^open`）的**预期后果**，不是退化 —— 改前落格目标＝满池 `E_i`（温带 105.6）⇒ 满格多；改后落格目标＝开局目标 `E_i^open`（温带 42.2）。**须策划端确认该差值属「口径变更」而非「退化」**（见 §四 `P-8`）。判据要求的「**不得退化**」项（`>4` 计数 0、空区块 0、max 坑位 4）**逐项守住**。

> **注**：`A3` 的「四带产额」在 `4-B` 后**语义已变**（从「满池铺量」改为「开局铺量」），故上表同时给新旧两套口径值；旧值取自 `HH.292` 报告（`T` 实测 108.6/155.9/130.8/84.2·`M7` 均值 120.0），新值本轮实读。

---

### 判据 2 · ⭐ `poolCapBase=96` 在场；`resourcesPerChunkBase` 已不存在 ＋ 5 处 Editor 探针改后原文行

**① `poolCapBase` 在场（定义处 = `.cs` 默认值 ＋ `.asset` 实际值）**：

```
Valley Rampart/Assets/_Game/Data/MapGenRulesConfig.cs:43:    public int poolCapBase = 96;
Valley Rampart/Assets/Resources/Grid/MapGenRulesConfig.asset:24:  poolCapBase: 96
```

**② `resourcesPerChunkBase` 已不存在**（复现命令输出**片段**，`git grep -n resourcesPerChunkBase -- "Valley Rampart/Assets"`）：

```
Valley Rampart/Assets/Editor/Smoke/Valley_HH272_MapGenProbe.cs:136:        float T = cfg != null ? cfg.poolCapBase : 96f;   // 【HH.294 片 5】`resourcesPerChunkBase` 已删 ⇒ 改读 `poolCapBase`（cap 基准）
Valley Rampart/Assets/Editor/Smoke/Valley_HH291_MapGenProbe.cs:75:        float T = cfg != null ? cfg.poolCapBase : 96f;   // 【HH.294 片 5】`resourcesPerChunkBase` 已删 ⇒ 改读 `poolCapBase`（cap 基准）
```

⇒ **仅剩 2 处命中，且均为「已删除」声明注释**（零代码引用）；**生产码与 SO 定义处 0 命中**（字段行已整行删除，见 §五 asset diff）。

**③ 5 处 Editor 探针改后原文行**（改读 `poolCapBase`）：

| # | file:line | 改后原文行 |
|---|---|---|
| 1 | `Valley_HH272_MapGenProbe.cs:136` | `float T = cfg != null ? cfg.poolCapBase : 96f;   // 【HH.294 片 5】…` |
| 2 | `Valley_HH272_MapGenProbe.cs:357` | `sb.AppendLine($"配置：poolCapBase={cfg.poolCapBase} guaranteeRatio={cfg.guaranteeRatio} " +` |
| 3 | `Valley_HH291_MapGenProbe.cs:75` | `float T = cfg != null ? cfg.poolCapBase : 96f;   // 【HH.294 片 5】…` |
| 4 | `Valley_HH291_MapGenProbe.cs:145` | `sb.AppendLine($"  [{label}] **M7**：T×abundance 四带均值 = **{mean:0.0}**（基准 poolCapBase={cfg.poolCapBase}，难度 {difficulty}）");` |
| 5 | `Valley_HH291_MapGenProbe.cs:264` | `_log.AppendLine($"配置：poolCapBase={cfg.poolCapBase} guaranteeRatio={cfg.guaranteeRatio} 难度系数=[{string.Join(",", cfg.difficultyResourceScale)}] clearR={cfg.kingdomClearRadius}");` |

（复现：`git grep -n poolCapBase -- "Valley Rampart/Assets"` ⇒ 命中 13 行，其中 Editor 探针 **5** 行，如上表。）

---

### 判据 3 · ⭐ 能力句「生成一张图 ⇒ 开局每区块点数 ≈ 38」＋ 逐带实测 ＋ 存在性反证

**能力句**：生成一张 256²/Normal 图 ⇒ **开局每区块点数 ≈ 38**（四带 热 34.6／亚热 49.9／温 42.2／寒 26.9；全图均值 **37.7~37.8**）。

**逐带实测（实机正门·池子实读 `Logs/hh294_s5_probe_live.log`）**：

| 带 | 区块数 | 池均值实测 | 期望 `cap×0.4` | 偏差 | min | max | ±20% 内 |
|---|---|---|---|---|---|---|---|
| 热带 | 63 | **33.7** | 34.6 | −0.8 | 26 | 34 | 62/63 |
| 亚热带 | 60 | **49.3** | 49.9 | −0.6 | 37 | 50 | 59/60 |
| 温带 | 67 | **41.9** | 42.2 | −0.4 | 38 | 42 | 67/67 |
| 寒带 | 66 | **26.6** | 26.9 | −0.3 | 19 | 27 | 64/66 |

**存在性反证**：落在期望值 ±20% 内的区块 **252/256**（须 ≥ 1）⇒ ✅ 成立；全图均值 **37.7**（编辑态独立复算 **37.8**，逐带 `33.7/49.9/41.5/26.9`）。

**按带取区块给读数**（编辑态·生成层直调·逐类量化目标对照）：

```
  热带 | 63 | **33.7** | 34.6 | -0.8 | 23 | 34 | 62/63 | 86.4 | 逐类量化目标=34
  亚热带 | 60 | **49.9** | 49.9 | +0.0 | 46 | 50 | 60/60 | 124.8 | 逐类量化目标=50
  温带 | 67 | **41.5** | 42.2 | -0.7 | 30 | 42 | 65/67 | 105.6 | 逐类量化目标=42
  寒带 | 66 | **26.9** | 26.9 | +0.0 | 22 | 27 | 66/66 | 67.2 | 逐类量化目标=27
  ⇒ 全图均值 = **37.8**（四带期望均值 = poolCapBase×0.4 = 38.4）
```

**归因读数（诚实说明）**：实测均值≈期望的 **97.5~100%**，偏差来自 **逐类 `RoundToInt` 量化**（各类分别取整后求和 ≠ 浮点总和）＋ 落格时受**候选格/净空区限制**的少量未达。**非「刚好等于」**（能力句要求「≈38」，实测 37.7/37.8）。

---

### 判据 4 · ⭐ 能力句「第 7 天 ⇒ 每区块 ≈ `cap` 的 95%+」＋ 逐天曲线实测

**能力句**：第 7 天 ⇒ 每区块 ≈ `cap` 的 **95%+**。

**① 逐天曲线实测（实机·`SettleDay` 驱动·分帧落格完成）**：

| 天 | 计划补量 | 实落格 | 全图均值/区块 | 均值Δ | `03` 表(按 `cap/96` 缩放) | ±20% 窗内 | 最大单帧 ms | 帧数 |
|---|---|---|---|---|---|---|---|---|
| 1 | +3407 | 3407 | **51** | +13 | +14 | 0/256 | 7.69 | 235 |
| 2 | +2563 | 2563 | **61** | +10 | +10 | 0/256 | 9.92 | 205 |
| 3 | +2104 | 2104 | **69** | +8 | +9 | 0/256 | 5.25 | 160 |
| 4 | +1788 | 1788 | **76** | +7 | +7 | 180/256 | 4.30 | 145 |
| 5 | +1592 | 1592 | **82** | +6 | +6 | 256/256 | 3.80 | 137 |
| 6 | +1402 | 1402 | **88** | +6 | +6 | 256/256 | 4.18 | 136 |
| 7 | +1212 | 1212 | **93** | +5 | +6 | 256/256 | 3.93 | 123 |

⇒ **第 7 天全图均值 93 ／ 四带 `cap` 均值 95.5 ／ 占比 = 97.4%** ⇒ ✅ **能力句成立**（95%+）。

**② 闭式解析对照（编辑态·`cap=96` 与 温带 `cap=105.6`）**：

```
  天 | 目标点数(cap=96) | 当日补量 | 03 §6.7.2 表(96 基准) | 偏差 | 目标% | 补后累计%
  1 | 52.2 | +14 | +14 | +0 | 54.4% | 54.2%
  2 | 62.4 | +10 | +10 | +0 | 65.0% | 64.6%
  3 | 70.5 | +8 | +9 | -1 | 73.5% | 72.9%
  4 | 77.4 | +7 | +7 | +0 | 80.6% | 80.2%
  5 | 83.5 | +6 | +6 | +0 | 87.0% | 86.5%
  6 | 88.9 | +5 | +6 | -1 | 92.6% | 91.7%
  7 | 93.5 | +5 | +6 | -1 | 97.4% | 96.9%
  ⇒ 第 7 天 = 93/96 = **96.9%**（能力句「第 7 天 ≈ cap 的 95%+」）
  温带 cap=105.6 逐日（按 cap/96 缩放）： D1=+15 D2=+11 D3=+9 D4=+8 D5=+7 D6=+6 D7=+5 ⇒ D7=103/106=97.5%
```

⚠️ **偏差说明**：第 3/6/7 天与 `03` §6.7.2 表差 **−1**。根因＝表值是**手写档位**、实现是**闭式曲线**（`(1+3x)³` 每天 +`9/(base×96)`）＋ `FloorToInt` 取整。**非缺陷**，但请策划端确认取「闭式」为口径（见 §四 `P-6`）。

**③ 上界反证**：第 7 天均值 95 → 第 14 天均值 **95（Δ=0）** ⇒ 达 `cap` 后增量为 0，**总上限生效**。

---

### 判据 5 · ⭐ 单类上限生效（`capKind_i` 逐类逐带值 ＋ 存在性反证 ＋ 满池期望对照）

**① `capKind_i` 逐类逐带值（=`cap × p_i × 1.5`·`D773` 裁）**：

| 带 | 树 | 石堆 | 木堆 | 矿脉 | 矿洞 |
|---|---|---|---|---|---|
| 热带（`cap`86.4） | 44.12 | 27.57 | 38.60 | 16.54 | 2.76 |
| 亚热带（`cap`124.8） | 63.73 | 39.83 | 55.76 | 23.90 | 3.98 |
| 温带（`cap`105.6） | 53.92 | 33.70 | 47.18 | 20.22 | 3.37 |
| 寒带（`cap`67.2） | 32.26 | 40.32 | **0.00** | 24.19 | 4.03 |

**② 满池期望对照（`E_i = cap × p_i`）**：温带树 **35.9**（须 ≈36）／温带矿洞 **2.2**（须 ≈2.2）⇒ ✅ **与 `03` §6.7 表逐值吻合**。

**③ 存在性反证（构造·生产同一处谓词 `ResourceRespawnSystem.KindAllowed`）**：

```
  构造①（总点数=106≥cap=105.6·该类=0）⇒ **False**（须 False：总满不再补）
  构造②（该类=54≥capKind=53.9·总点数=40<cap）⇒ **False**（须 False：该类达上限不再增）
  构造③（该类=53<capKind·总点数=105<cap 上限侧）⇒ **True**（须 True：双未满可补）
  构造④（cap=0）⇒ **False**（须 False）
  ⇒ 存在性反证：✅「该类达上限后不再增」由生产同一谓词否定
```

**④ 观测反证（7 天后实测·逐带逐类均值 ≤ `capKind_i`）**：20 组（4 带 × 5 类）**全部「未越界」** ⇒ ✅。抽样读数：

```
  温带 | 树 | 35.64 | 53.92 | 未越界 | 35.9
  温带 | 矿洞 | 2.31 | 3.37 | 未越界 | 2.2
  寒带 | 木堆 | 0.00 | 0.00 | 未越界 | 0.0
```

---

### 判据 6 · ⭐ 能力句「重生 ⇒ 落点不在原位」＋ 存在性反证 ＋ 兜底路径读数

**能力句**：重生 ⇒ 落点**不在原位**。

**① 存在性反证（实测 N 次·`Logs/hh294_s5_probe_live.log`）**：

```
  构造：采走 8 棵树（分处 8 个区块）⇒ 采后仍非 Plain 的 = 0（须 0）
  N = **8** 次重生窗口（该天全图实落格 8 点）：新位 ≠ 原位的 = **8**/8（须全部 ≠ 原位） ✅
```

⇒ **N = 8，8/8 新位 ≠ 原位** ⇒ ✅（量词「全部」被满足）。

**② 实体路径（一次性资源 `WoodPile`）**：

```
  木堆格 (16,2) 采集 ⇒ 池子木堆 45 → 44；该格现值 = **Plain**（须 Plain ⇒ 不在原位）；该区块木堆 feature 数 = 33（> 0 ⇒ 已落新位）
```

**③ 兜底路径读数（抽不到位置 ⇒ 原位）**——生产同一处 `PickCell` 构造：

```
  构造①（fresh={501,502}·freed={999}）依次取：501 → 502 → **999**（fresh 尽 ⇒ 兜底＝原位格 999）→ -1（须 −1）
  构造②（fresh 空·freed 空）⇒ **-1**（须 −1：本区块不可落）
  ⇒ 读数链：✅ fresh 优先（不在原位）／fresh 尽 ⇒ 原位兜底／皆尽 ⇒ 放弃
  实测兜底触发次数：本天走 freed（原位兜底）落格数 = **0**（fresh 候选充足 ⇒ 0；抽不到位置才回原位）
```

**④ `03` §6.7 一致性**：「户口＝锚点所在区块；**重生记区块不记格**」—— 本片已把 `_data[]`/`_entity[]` 的**逐格到期表**改造为**区块池子表**（`_count[区块×类]` ＋ `_targetF[区块]`；见 §二 `4-D`）。

---

### 判据 7 · ⭐ 能力句「`RespawnConfig.daySeconds` 与 `ResourceRespawnSystem._elapsed`／`_currentDay` 已不存在」＋ grep 完整输出片段

**grep 完整输出片段**（`git grep -n "daySeconds\|_elapsed\|_currentDay\|dueGameDay" -- "Valley Rampart/Assets"`）：

```
Valley Rampart/Assets/_Game/Data/RespawnConfig.cs:9:/// 节奏由池子曲线（`03` §6.7.2）决定 ⇒ ⛔ **删 `daySeconds`（第二日历）与四类重生天数**
Valley Rampart/Assets/_Game/Systems/UI/SplashPanel.cs:15:    private float _elapsed;
Valley Rampart/Assets/_Game/Systems/UI/SplashPanel.cs:31:        _elapsed = 0f;
Valley Rampart/Assets/_Game/Systems/UI/SplashPanel.cs:39:        _elapsed += Time.unscaledDeltaTime;
Valley Rampart/Assets/_Game/Systems/UI/SplashPanel.cs:44:            _hintLabel.style.opacity = (_elapsed % 1.0f) < 0.5f ? 1.0f : 0.3f;
Valley Rampart/Assets/_Game/Systems/UI/SplashPanel.cs:55:        if (_elapsed >= autoSkipSeconds)
Valley Rampart/Assets/_Game/Systems/World/ResourceRespawnSystem.cs:9:///   <item><b>4-A 删第二日历</b>：删 `RespawnConfig.daySeconds` 与 `_elapsed`／`_currentDay` —— 时间基准改
Valley Rampart/Assets/_Game/Systems/World/ResourceRespawnSystem.cs:11:///   <item><b>4-D／4-E 池子模型</b>：改前是「**逐格到期表**」（`_data[]`／`_entity[]`，各记 cell ＋ dueGameDay）；
Valley Rampart/Assets/_Game/Systems/World/ResourceRespawnSystem.cs:290:        // ⚠️ 本方法**不再**推进任何日历（4-A：删 `_elapsed`／`_currentDay`）；
```

**逐项核（能力句含限定词 ⇒ 逐项核「能否在受限语境外被调用」）**：

| 词 | 命中数 | 逐处定性 |
|---|---|---|
| `daySeconds` | **0 处代码**（2 处声明注释：`RespawnConfig.cs:9`／`ResourceRespawnSystem.cs:9`） | 字段**已删**（`.cs` 定义行 ＋ `.asset` 值行均删，见 §五 diff）⇒ 注释只是「已删除」存档声明 |
| `_elapsed`（本系统） | **0 处代码**（1 处注释 `:290`） | `ResourceRespawnSystem._elapsed` **已删**；剩余 5 处命中全部属 **`SplashPanel`（启动画面计时器·同名不同物）** ⇒ **可在受限语境外被调用，但属他人字段·非本片标的** |
| `_currentDay`（本系统） | **0 处**（含注释） | **已删**（本系统内零残留） |
| `dueGameDay` | **0 处代码**（1 处注释 `:11`） | 字段**已删** |

**存在性反证（受限语境外调用核）**：`ResourceRespawnSystem` 的旧日历三件（`daySeconds`／`_elapsed`／`_currentDay`／`dueGameDay`）在**全库零代码引用**；`SplashPanel._elapsed` 为**另一类型的私有字段**（`SplashPanel.cs:15`），与本系统无调用关系（本系统内 grep 零命中）。⇒ **能力句成立**（限定词「`RespawnConfig.` 与 `ResourceRespawnSystem.` 」精确命中，未越界断言他物）。

---

### 判据 8 · ⭐ 每日推进一次（该路径调用计数 = 1；不是每帧/每秒）

**① 幂等 ＋ 单调（构造·`SettleDay` 3 连调 ＋ 回退天）**：

```
  构造：同一天 `SettleDay(9)` 连调 3 次 ⇒ DaySettleCount 增量 = **1**（须 1：同日幂等）
  构造：回退天 `SettleDay(8)` ⇒ 增量 = **0**（须 0：单调不重算）
```

**② 真实时钟节流窗（`TimeDayChangedEvent` → 结算调用 1:1）**：

```
  实测窗 6s（15x 加速·`SetSecondsPerDay(30)`）：游戏天推进 = **6** ／ TimeDayChangedEvent 收到 = **6** ／ 结算调用增量 = **6** ／ 经过帧数 = **14**
  ⇒ 每天一次：事件数 6 vs 结算数 6 ⇒ ✅ 一个游戏天恰结算 1 次；事件 vs 天推进：✅ 一天一个事件；
    非每帧/每秒反证：帧数 14 ≫ 结算 6（若每帧调应 = 14，若每秒调应 ≈ 6）
```

⇒ **一游戏天 ＝ 结算 1 次**（事件 6 ↔ 结算 6 逐值相等）；**非每帧/每秒反证**：经过 **14** 帧仅结算 **6** 次（每帧调应为 14）。

**③ 时间基准同源核**（`4-A`）：订阅 `TimeDayChangedEvent`（`GameEvents.cs` 定义·先例 `GameBootstrap.cs:40`）＋ `Awake` 里 `EventBus.Subscribe`（`ResourceRespawnSystem.cs:123`），`OnDestroy` 对称退订（`:130`）。**本系统 `Update()` 只做分帧落格、不推进任何日历**（`:288-292`，注释已显式声明）。

---

### 判据 9 · 分帧分摊（最大单帧耗时 ＋ 分摊帧数 ＋ 16.6 ms 对照）

```
  第 7 天（满池前补量日）：最大单帧耗时 = **3.93 ms** ／ 占用帧数 = **123** ／ 当天落格 = 1212 点
    （帧预算 16.6ms ⇒ 最坏帧占 23.6%）；⛔ 非「一天一帧全落」（帧数 > 1）
  最近一次结算（节流窗内）：最大单帧耗时 = 10.47 ms ／ 占用帧数 = 249 ／ 当天落格 = 3287 点
```

| 天 | 实落格 | 最大单帧 ms | 占用帧数 | 最坏帧 / 16.6 ms |
|---|---|---|---|---|
| 1 | 3407 | 7.69 | 235 | 46.3% |
| 3 | 2104 | 5.25 | 160 | 31.6% |
| 5 | 1592 | 3.80 | 137 | 22.9% |
| 7 | 1212 | 3.93 | 123 | 23.6% |

**机制（三重额度）**：每帧最多 **4 个区块** ／ 最多 **16 个落格点** ／ 时间预算 **2.0 ms**（`ResourceRespawnSystem.cs:48-51`）。区块未补完时**不推进游标**、下一帧续做（可跨帧续），⇒ 单帧上限被点数额度硬钳（16 点/帧），**⛔ 未出现「一天一帧全落」**。

**读数说明（诚实）**：节流窗内那次 10.47 ms 是**首次分帧起步帧**（含区块候选格构建＋shuffle 的冷启动），后续帧稳定在 3~5 ms；**仍未越 16.6 ms 帧预算**。

---

### 判据 10 · 配额按锚点（跨区块边界的实体 ⇒ 配额只记 1 次）

**① 构造用例（32×32 / 2×2 区块·跨界簇锚点 (15,7) 横跨 x 边界）**：

```
  构造：32×32（2×2 区块）·跨界簇锚点=(15,7)（占 15/16 × 7/8）·区块内整簇锚点=(20,7)
  区块(0,0) 点数=1（mine=**1**·须 1＝跨界簇锚点所在地）
  区块(1,0) 点数=1（mine=**1**·须 1＝本区块内整簇；(16,7)/(16,8) 两格**不另计**）
  区块(0,1) mine=0（须 0）·区块(1,1) mine=0（须 0）
  Σ 全区块 mine 点数 = 2（须 2 ＝ 两簇；Mine 格数 = 8）
  ⇒ ✅ 跨界实体**只记 1 次**（记在锚点所在区块），邻区块不重算
```

**② 实图不变量（256²/Normal·无重计/漏计）**：

```
  Mine 格 = 1024 ⇒ 簇（÷4）= 256.0；Σ 逐区块锚点点数 = **256** ⇒ ✅ 逐区块点数×4 == 格数（无重计/漏计）
  参照：树 格=950／坑位=3291（非矿按坑位计，与区块归属无关）
```

⇒ **跨区块边界只记 1 次**（构造用例 §①）＋ **全图 Σ 无重计/漏计**（§② 独立判据）—— 两条互为交叉验证。

---

### 判据 11 · `EnsureChunkResourceQuota`：能力句「无区块被 6.6 补到满池」＋ `target` 改后原文行

**① `target` 改后原文行**（[MapGenRules.cs:1187](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/MapGenRules.cs#L1187)）：

```csharp
int target = Mathf.Max(Mathf.RoundToInt(e[t]), b[t]);   // e[] ＝ 开局目标 E_i^open（P-4）
```

（`e[]` 由 `ComputeQuota` 写入 `E_i^open = cap × p_i × poolOpenRatio`，见 `:1000` 起 `ComputeQuota`；矿洞行 `:1178` `int mineWant = Mathf.RoundToInt(e[ResMine]);`。）

**② 能力句「无区块被 6.6 补到满池」＋ 逐区块最大值**：

```
  判据 11 读数（6.6 是否把区块补到满池）：全图区块点数最大值 = **50**；温带 cap = **105.6** ⇒ ✅ 无区块被补到满池（最大 < cap）
```

（`A5` 独立读数：`区块=256 空区块=0 min=26 均=40.8 max=55` ⇒ 全局最大 55 < 最小 `cap` 67.2 ⇒ 亦成立。）

---

### 判据 12 · `R5` 预算重估（本片是否改变该路径触发频率）

**结论：本片不改变该路径触发频率 ⇒ 无新读数**（沿用 `HH.303` §四 的 `143.54`／`154.10 ms` 观测底座，**仍属「生产未走」**）。

**逐项核（能力句含限定词 ⇒ 逐项核受限语境外调用）**：

- 子格域 `flood-fill` **唯一全图在场点**＝Editor 探针 `Valley_HH294_Slice3Probe.cs:336`（`g.IsSubWalkable(...)` 入队循环）。
- 生产码 `IsSubWalkable` 调用 **13 处命中**（`GridSystem.cs:254` 定义 ＋ `:585/:594` 内部 ＋ `FormationController:550`／`CombatRules:68`／`SpawnPosSnapper:36,60`／`PathFollower:145`／`PathfindingService:31` ＋ 3 处注释）—— **逐处均为「单点判定」·无一处全图 flood**。
- 本片改动面（`ResourceRespawnSystem`／`MapGenRules` 配额段／`WorldManager` 一行／2 个 asset）**不触碰寻路/子格域**；`4-A` 把重生节奏从「每帧自攒秒」改为「每天一次事件」，**方向是降低**而非增加调用频次，且与 `IsSubWalkable` 路径**无调用关系**。

⇒ **触发频率不变 ⇒ `R5` 新读数 ＝ 不产生**（同 `HH.303` §四 判定）。

---

### 判据 13 · 常规（编译／确定性／无每帧全图扫／`AI.Core` 零触／LF／正门三态）

| 项 | 读数 |
|---|---|
| 编译 | **0 error**（`read_console` 实测：`Retrieved 0 log entries`） |
| 新增 warning | **0**（全量 warning 清单 24 条**全部为存量**：`IUIPanel`/`GroundEffectManager`/`ToastManager`/`ChestManager`/`BuildingMenuPanel`/`ProjectileManager`/`NPCBrain`/`FormationPanel`/`CameraSetup`/`PathfindingScheduler`/`VisionSystem` ＋ Editor 探针 9 条） |
| 同 seed 逐格一致 | **✅**（编辑态：`同 seed 两次生成：✅ 逐格一致（features+climateZones+spawns+nb）`） |
| 无每帧全图扫 | ✅ 本系统 `Update()` 仅在 `HasPendingWork` 时按 4 区块/帧 推进（`:288`）；**无待落格时零工作** |
| `AI.Core` 零触 | ✅ `git status --porcelain -- "AI.Core"` ⇒ **空输出** |
| 改动文件行尾 | ✅ **10/10 全 LF**（实测：`非LF文件数 = 0`） |
| 正门三态 | ✅ `TestHarnessApi.EnterTestRun` 进局（2.62s 就绪）→ 读数期间 `GameState.Paused` 冻结 → **收尾先回 `Playing` 再 `ExitTestRun`**；探针跑毕已退 Play |

**行尾实测（复现命令输出片段）**：

```
LF crlf=0 lf=637 Valley Rampart/Assets/_Game/Systems/World/ResourceRespawnSystem.cs
LF crlf=0 lf=1536 Valley Rampart/Assets/_Game/Systems/World/MapGenRules.cs
LF crlf=0 lf=408 Valley Rampart/Assets/_Game/Systems/World/WorldManager.cs
LF crlf=0 lf=172 Valley Rampart/Assets/_Game/Data/MapGenRulesConfig.cs
LF crlf=0 lf=35 Valley Rampart/Assets/_Game/Data/RespawnConfig.cs
LF crlf=0 lf=81 Valley Rampart/Assets/Resources/Grid/MapGenRulesConfig.asset
LF crlf=0 lf=17 Valley Rampart/Assets/Resources/Config/RespawnConfig.asset
LF crlf=0 lf=436 Valley Rampart/Assets/Editor/Smoke/Valley_HH272_MapGenProbe.cs
LF crlf=0 lf=637 Valley Rampart/Assets/Editor/Smoke/Valley_HH291_MapGenProbe.cs
LF crlf=0 lf=640 Valley Rampart/Assets/Editor/Smoke/Valley_HH294_Slice5Probe.cs
非LF文件数 = 0
```

---

### 判据 14 · ⭐ SO 资产改动面（逐字段 改前→改后）＋ 改动文件清单

**① `MapGenRulesConfig.asset` 逐字段**（`git diff` 原文）：

```
-  resourcesPerChunkBase: 120
+  poolCapBase: 96
+  poolOpenRatio: 0.4
+  kindCapRelax: 1.5
   resourceWeights:
   - tree: 0.2
-    stonePile: 0.45          +    stonePile: 0.5
-    woodPile: 0.45           +    woodPile: 0.3
-    oreVein: 0.5             +    oreVein: 0.7
-    mine: 0.9                +    mine: 0.95
   - tree: 0.2
-    stonePile: 0.4           +    stonePile: 0.5
-    woodPile: 0.4            +    woodPile: 0.3
-    oreVein: 0.45            +    oreVein: 0.7
-    mine: 0.84               +    mine: 0.95
-  - tree: 0.25              +  - tree: 0.2
-    stonePile: 0.4           +    stonePile: 0.5
-    woodPile: 0.4            +    woodPile: 0.3
-    oreVein: 0.4             +    oreVein: 0.7
-    mine: 0.8                +    mine: 0.95
   - tree: 0.6
-    stonePile: 0.45          +    stonePile: 0.5
     woodPile: 1
-    oreVein: 0.55            +    oreVein: 0.7
-    mine: 0.92               +    mine: 0.95
   guaranteeRatio: 0.5          （未变）
   difficultyResourceScale: 0.7 （未变）
```

**② `RespawnConfig.asset` 逐字段**（`git diff` 原文）：

```
   enabled: 1                   （未变）
-  daySeconds: 3               ← 删（第二日历）
   treeGatherSeconds: 2         （未变）
   treeGatherAmount: 5          （未变）
-  treeRespawnDays: 7          ← 删（旧四类重生天数作废）
-  woodRespawnDays: 4          ← 删
-  stoneRespawnDays: 10        ← 删
-  oreRespawnDays: 12          ← 删
```

> 注：`03` §6.7 明示「旧数值 树7/木4/石10/矿12 天 **作废**」⇒ 四类天数 **一并删除**（未保留兼容空字段）。

**③ 改动文件清单（10 文件·全部 `git add` 具名）**：

| # | 文件 | 性质 |
|---|---|---|
| 1 | `Valley Rampart/Assets/_Game/Data/MapGenRulesConfig.cs` | SO 定义：删 `resourcesPerChunkBase`／加 `poolCapBase`/`poolOpenRatio`/`kindCapRelax`／权重表四带换值／`guaranteeRatio` tooltip 改口径 |
| 2 | `Valley Rampart/Assets/Resources/Grid/MapGenRulesConfig.asset` | SO 实值（见 ①） |
| 3 | `Valley Rampart/Assets/_Game/Data/RespawnConfig.cs` | SO 定义：删 `daySeconds` ＋ 四类重生天数 |
| 4 | `Valley Rampart/Assets/Resources/Config/RespawnConfig.asset` | SO 实值（见 ②） |
| 5 | `Valley Rampart/Assets/_Game/Systems/World/ResourceRespawnSystem.cs` | **4-A/4-D/4-E 主体重写**（池子模型·区块表·每日结算·分帧落格·落点器·存档结构 v2） |
| 6 | `Valley Rampart/Assets/_Game/Systems/World/MapGenRules.cs` | `2-3/2-5`（`ResolvePoolQuota` 新增·`ComputeQuota` 改开局目标）＋ `4-B`（`T`→`cap`）＋ `4-C`（`CountChunkPoints` 按锚点）＋ `2-4`（6.6 `target`） |
| 7 | `Valley Rampart/Assets/_Game/Systems/World/WorldManager.cs` | `4-A` 配套：`ResetRespawns(map, difficulty)` 显式传参（原地图就绪前 `ActiveMap` 仍 null） |
| 8 | `Valley Rampart/Assets/Editor/Smoke/Valley_HH272_MapGenProbe.cs` | `P-2`：2 处探针改读 `poolCapBase` |
| 9 | `Valley Rampart/Assets/Editor/Smoke/Valley_HH291_MapGenProbe.cs` | `P-2`：3 处探针改读 `poolCapBase` |
| 10 | `Valley Rampart/Assets/Editor/Smoke/Valley_HH294_Slice5Probe.cs` | **新建**片 5 探针（编辑态 ＋ 实机正门双入口） |

（另有 `04` 红线禁碰项一律**未动**：`pixel-forge/**`／`GameScene.unity`／`Packages/**`／美术 png／`3.6`·`3.8` doc／`AI.Core`。）

---

## 二、施工项落地对账（4-A ~ 4-F）

| 项 | 要求 | 落点（file:line 实核） | 状态 |
|---|---|---|---|
| `4-A` | 删第二日历 ⇒ `TimeManager.CurrentDay` ＋ `TimeDayChangedEvent`·每天推进一次 | `ResourceRespawnSystem.cs:123`（订阅）／`:130`（退订）／`:217` `SettleDay`／`:288` `Update` 只分帧 | ✅ |
| `4-B` | 生成期不再铺满（开局 40%） | `MapGenRules.cs:813` `PlaceResourceQuota`（`e[]` ＝ 开局目标）／`:850` 矿洞按点 | ✅ |
| `4-C` | 配额统计按锚点（跨界不重算） | `MapGenRules.cs:1027` `CountChunkPoints`（认领式·锚点＝左上角）／`:1158` 6.6 调用点 | ✅ |
| `4-D` | 重生落点器随机落新位（不原地复活）＋ 兜底原位 | `ResourceRespawnSystem.cs:354` `PickCell`（fresh→freed→−1）／`:263` `FlushPending` | ✅ |
| `4-E` | 池子模型 ＋ 单类上限 ＋ 每日补量 ＋ 分帧分摊 | `ResourceRespawnSystem.cs:217`（每日结算入队）／`:251` `DailyTarget`（闭式曲线）／`:426` `KindAllowed`（双上限谓词）／`:263`（分帧） | ✅ |
| `4-F` | 上限 `96 × 丰度`（并入 `2-1`/`2-3`） | `MapGenRules.cs:970` `ResolvePoolQuota`（`poolCapBase × 丰度 × 难度`） | ✅ |

**口径落地对账（`2-1`~`2-5`）**：`2-1` 权重表 ✅（判据 1）／`2-2` `poolCapBase` ＋ 删旧字段 ＋ 5 探针 ✅（判据 2）／`2-3` 难度乘在 `cap` ✅（`cap` 比例实测 `0.700:1.000:1.300`）／`2-4` 6.6 `target` ✅（判据 11）／`2-5` 占比与目标 ✅（判据 3/5：`ΣE_i = cap` 四带逐带相等；`ΣE_i^open = cap×0.4` 逐带相等）。

**`2-5` 派生值对照（`03` §6.7 表·难度中档）**：

| 带 | `cap` 实现值 | `03` 表值 | p_树 实现 | 满池树 `E_树` | 满池矿洞 `E_mine` |
|---|---|---|---|---|---|
| 热带 | 86.4 | 86.4 | 34.04% | 29.4 | 1.8 |
| 亚热带 | 124.8 | 124.8 | 34.04% | 42.5 | 2.7 |
| 温带 | 105.6 | 105.6 | 34.04% | 35.9 | 2.2 |
| 寒带 | 67.2 | 67.2 | 32.00% | 21.5 | 2.7（木堆 **0**） |

⇒ **四带逐值吻合**（判据要求「温带树 ≈36／矿洞 ≈2.2」✅；「四带均值树 32.3」✅ 实测 `(29.4+42.5+35.9+21.5)/4 = 32.3`）。

---

## 三、新增/变更的公共接口（供上层核对·判据 8/⑧ 收口清单到「函数级 ＋ 调用面」两列）

| 新增/改签名 | 函数级落点 | 调用面（生产码） |
|---|---|---|
| `MapGenRules.ResolvePoolQuota(cfg, band, difficulty, share[], kindCap[], out cap)` | `MapGenRules.cs:970` | `ComputeQuota:1000`（生成期 2 处调用点）＋ `ResourceRespawnSystem` 池子初始化（运行期同一处算式） |
| `MapGenRules.CountChunkPoints(map, cx, cy, dst)` | `MapGenRules.cs:1027` | `EnsureChunkResourceQuota:1158` |
| `MapGenRules.ComputeQuota(..., kindCap[], shareBuf[], out cap)` | `MapGenRules.cs:1000` | `PlaceResourceQuota`（生成期）／`EnsureChunkResourceQuota` |
| `ResourceRespawnSystem.ResetRespawns(MapData, int)` | `:140` | `WorldManager.cs:212`（**唯一调用点**） |
| `ResourceRespawnSystem.SettleDay(int)` | `:217` | `OnDayChanged`（事件·`:122`）；探针直调（快进） |
| `ResourceRespawnSystem.DailyTarget(float, float)` | `:251` | `SettleDay`；探针 |
| `ResourceRespawnSystem.FlushPending()` | `:263` | `Update():288`；探针 |
| `ResourceRespawnSystem.PickCell(...)` | `:354` | `PlaceOne`／`TryPlaceMineCluster`；探针构造 |
| `ResourceRespawnSystem.KindAllowed(...)` | `:426` | `PickKind`（生产）＋ 探针构造反证（**同一处谓词**） |
| `ResourceRespawnSystem` 读数口 `PointsOf/CapOf/KindCapOf/ShareOf/BandOf/PoolReady/PoolChunkCount/DaySettleCount/...` | `:526` `PointsOf`／`:534` `PointsOf(ci,kind)`／`:537` `CapOf`／`:540` `KindCapOf`／`:543` `ShareOf`／`:546` `BandOf` | 探针／HUD（**生产码零消费·备而未用**，同 `D772` `Query*` 情形） |
| `ResourceRespawnSaveData`（结构换代 v2） | `:627` | `SaveState`/`LoadState` |

---

## 四、未完成项 · 列报（不伪造、不静默跳过）

> 以下各项**如实列出**，均不属「已实现」；请策划端逐项处置。

| # | 项 | 事实读数 | 性质 |
|---|---|---|---|
| `P-5` | **矿洞「1 簇（2×2 Cell）＝ 1 点」落地式** | `03` §6.7 系列未明写矿洞的点数单位；本片据 **`Σ_i E_i = cap` 自洽** ＋ §6.7.1「矿洞 2.2（≈2 簇）」**判定为 1 簇 ＝ 1 点**，并据此把改前的 `RoundToInt(e[ResMine] / 4)` 改为 `RoundToInt(e[ResMine])`（`MapGenRules.cs:850`／`:1178`）。实测：四带矿洞簇均值 `1.94~2.64`（与表 1.8~2.7 吻合）＋ 全图 `Σ 锚点点数×4 == Mine 格数`（1024） | ⭐ **需追认**（口径解释权属策划端） |
| `P-6` | **日补量曲线第 3/6/7 天与 `03` §6.7.2 表差 −1** | 解析：`+14/+10/+8/+7/+6/+5/+5` vs 表 `+14/+10/+9/+7/+6/+6/+6`。根因＝**表是手写档位·实现是闭式曲线**，＋ `FloorToInt` 取整 | ⭐ **请裁**（认「闭式为准」并勘正表，或改实现贴表） |
| `P-7` | **判据 3 能力句「±20% 窗」在第 1~3 天不成立** | 逐日 `±20% 窗内区块` ＝ `0/256`（D1~D3）→ `180/256`（D4）→ `256/256`（D5+）。根因＝该窗以**满池 `cap`** 为基准，而池子**仍在从 40% 向 100% 爬升**（D1~D3 池子离 `cap` 远） | 判据**表述**问题（非实现缺陷）；判据 3 的**开局**读数已 ✅（252/256） |
| `P-8` | **`A4` 满格数 6824 → 1962** | 归因＝`4-B`「生成期不再铺满」的**预期后果**（落格目标从满池 105.6 降为开局 42.2）。判据要求的「不得退化」三项（`>4` 计数 0／空区块 0／max 坑位 4）**全部守住** | ⭐ **请确认属「口径变更」而非「退化」** |
| ⚠️ 1 | **`3-B` 阵列下移面**（`_walkFlags`/`_occupants`） | **本片未触碰**（属片 3 已交付范围·`D770` 销号） | 不在本片范围 |
| ⚠️ 2 | **`F-14`（富贫矿 `gradeScale`）** | **本片未触碰**（`BuildingFactory.cs:97/116/145` 三处 `grade: ResourceGrade.Normal` **逐字未改**） | 红线规定不在本批 |
| ⚠️ 3 | **片 6**（双写收敛／`Collider2D` 按需／`PickAt` 拾取坐标化） | **未开工**（红线规定：⛔ 不碰） | 红线规定不在本批 |
| ⚠️ 4 | **中层**（`08` 建筑功能／`05` 交互／`06` 任务／`07` 伤害／`#14` 建筑数据化） | **未触碰** | 红线规定不在本批 |
| ⚠️ 5 | **`F-15`**（9 座建筑 footprint 重叠） | 本片未触碰（`D773` 已升「待修」·另批） | 红线规定不在本批 |
| ⚠️ 6 | **`R5` 新读数** | **不产生**（本片不改变该路径触发频率·见判据 12） | 判据要求本身允许 |
| ⚠️ 7 | **判据 6 的 N 值偏小（N=8）** | 探针构造「每区块只吃 1 棵」以保该区块仍有 fresh 空位；**8/8 全不在原位**已满足量词，但样本量小 ⇒ 若策划端要求更大 N，可加跑（需增探针） | 样本量说明（非失败） |
| ⚠️ 8 | **`ResourceRespawnSystem` 读数口（`PointsOf`/`CapOf`/…）生产码零消费** | 目前仅探针使用（同 `D772` 发现的 `Query*` 情形） | 备而未用·如实列报 |

**红线遵守声明**：① 只做 `4-A`~`4-F` ＋ `R5` 预算 ✅ ② 不碰片 6 ✅ ③ 不碰中层 ✅ ④ `F-14` 未顺手改 ✅ ⑤ 并行会话未提交改动（`pixel-forge`/`GameScene.unity`/`Packages`/美术 png/`3.6`·`3.8` doc）**一律未碰** ✅ ⑥ 提交只用具名 `git add` ✅ ⑦ 未 push ✅ ⑧ `AI.Core` 零触 ✅ ⑨ 未改设计文档 ✅ ⑩ 行尾：所有改动文件 LF（`多Agent交接/**` 用 python 二进制按行改，未用 Edit/Write）✅

---

## 五、证据清单

| 载体 | 说明 |
|---|---|
| `Valley Rampart/Logs/hh294_s5_probe_edit.log` | 编辑态读数：权重 diff／口径表／谓词构造／`PickCell` 构造／开局点数／锚点不变量＋构造用例／曲线解析／`A3`/`A4`/`A5` 重跑／确定性 |
| `Valley Rampart/Logs/hh294_s5_probe_live.log` | 实机正门读数：开局池子／逐天曲线／单类上限／重生不在原位／节流窗／分帧分摊 |
| `Valley Rampart/Logs/hh294_s5_probe_live_prior.log` | 前一跑次留存（对照，验证读数可复现） |
| `Valley Rampart/Assets/Editor/Smoke/Valley_HH294_Slice5Probe.cs` | 片 5 探针（编辑态 `RunEdit()` ＋ 实机 `RunLive()` 双入口·**新建**） |

---

## 六、结论与请裁

**成立项**：`2-1`~`2-5` ＋ `4-A`~`4-F` **全部落地**；判据 1~14 **逐条取得实测读数**；编译 0 error／0 新增 warning；同 seed 逐格一致；`AI.Core` 零触；改动文件全 LF；正门三态。

**请裁 3 项**：`P-5`（矿洞点数单位口径追认）／`P-6`（曲线 ±1 偏差认哪个为准）／`P-8`（`A4` 满格数变化属口径变更）＋ **`P-7`（判据 3 表述勘正·非实现缺陷）**。

**未完成 8 项**见 §四（其中 ⚠️1~5 属红线规定不属本片范围，⚠️6 判据本身允许，⚠️7/⚠️8 为样本量/备而未用说明）。

---

## 七、应登记项声明（`D767` 新纪律：执行端只声明、不代写策划端账本）

本次**未改**策划端四档账本（`_编号登记.md`／`_当前快照.md`／`_任务队列.md`／`测试基线台账.md`）。按 `HH.298` 先例，仅在此声明**应登记项**，请策划端落账：

| # | 账本 | 应登记内容 |
|---|---|---|
| 1 | `_编号登记.md` | **HH.304** ＝ 执行端「`HH.294` 片 5（资源池落地）」交付报告·持有端＝执行端·状态＝🟡待验收·登记时间 2026-09-17；**水位线 HH.303 → HH.304** |
| 2 | `_编号登记.md` 在途表 | 片 5 交付行（用途／状态／关联 `HH.294_片5_5-0报裁_裁决.md`＋`D774`；证据 `Logs/hh294_s5_probe_edit.log`＋`Logs/hh294_s5_probe_live.log`） |
| 3 | `测试基线台账.md`（D 区／§五 `F-12`） | **`F-12`（刷新年历 120×）＝ 已修**（`4-A` 删第二日历：`RespawnConfig.daySeconds`／`_elapsed`／`_currentDay`／`dueGameDay` 全删，改 `TimeManager.CurrentDay` ＋ `TimeDayChangedEvent`）⇒ 建议状态由「❌ 待修·无批」改「✅ 已修（片 5）」 |
| 4 | `测试基线台账.md`（`D762` 资源池参数 §三十二） | **口径已落地**：`poolCapBase=96`／`poolOpenRatio=0.4`／`kindCapRelax=1.5`／四带权重表（`D774` 定案版）；`resourcesPerChunkBase` 已删 ⇒ 建议在 §三十二 追记「已落地（`HH.304`）」 |
| 5 | `_当前快照.md` | §一 水位线行 HH 列 → `HH.304`；§三 `0k` 行片 5 状态 → 🟡待验收；§七 接手第一件事 → 补「片 5 已交付待验收（`HH.304`）」 |
| 6 | 台账新增节 | 建议 **§四十六** ＝ 片 5 交付记录（含 4 项请裁 `P-5`~`P-8`） |
| 7 | 挂账更新 | `F-12` 消项；`F-14`／`F-15`／片 6 **仍挂账**（本片未触） |
