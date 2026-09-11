# HH.214｜P1 终验收 · 七考重验 · 交付报告

> 执行端（TraeCode·Unity 轨）· 2026-09-11 · 状态：🔴**判定线未 PASS（如实列报，不注水）**
> 锚点：**HH.203 任务书（D657／0.6 §一百八十六）** ｜ 前置＝**HH.208 ⑰收口批验收成立（D661）** ｜ 开工回执＝**HH.211**（协议六项＋seed 报备＋容器级列报）
> 取号：遵 D640 #10（水位线 HH.213→**HH.214**，独立单行 commit `46e53a5`）

---

## 一、判定结论（D589 判定线）

> **「净状态下 ≥2 个 AI 王国自主推进至军事期」＝ P1 终验收 PASS**

**⇒ 🔴 未 PASS**。容器收工档原文：

```
===== HH.80 三考收工 =====
熔断：D120 无 ≥2 AI 军事期（已达标=，AI 存活 1/4）
终速=0 存盘 p1_run8=True 时间=21:38:02
```

| 项 | 实测 |
|---|---|
| 达军事期 AI 数 | **0 / 4**（「剧本阶段 → 军事」镜像行 **= 0**；「军事期达标」**= 0**） |
| 收工方式 | **熔断 @D120**（非提前达标、非灭绝全灭） |
| AI 存活 | **1 / 4**（k1 存活；k2 全灭 D60／k4 D48／k3 D103） |
| 全程 stage 分布 | 四国**仅** Survive/Develop/**Expand**，**零 Military**（CSV 逐国逐日：k1 21D+97E+1S／k2 3D+115E+1S／k3 21D+97E+1S／k4 11D+107E+1S） |
| D120 终态（CSV） | k1 密林(精灵) 工12 战**0** 领32 金546 粮4793 `Expand`；k2/k3/k4 全灭 `Expand` |
| 跑局时长 | **47.5 现实分钟**（15x，实测 `Time.timeScale=15`；游戏 120 日） |

## 二、协议兑现（HH.203 §二 六项）

| # | 协议 | 兑现证据 |
|---|---|---|
| 1 | 正门唯一入口 | console `[HH80跑] 正门进局 seed=64513 槽=p1_run8 考跑守卫全开`；`Valley_HH80_Run.cs:76` `EnterTestRun` |
| 2 | 守卫全开＋玩家真实局态 | **实证**：k0（玩家）D120 工人/战士 **0/0** 而**无 GameOver、无 timeScale 冻结**（跑满 120 日）⇒ ThroneAnchor 判负封死生效（**对照六考 D45 裸跑截断**） |
| 3 | 15x | 进局后实测 `Time.timeScale=15`；`WorldConfig.time.testSpeedMultiplier` SO 直通（未直设 timeScale） |
| 4 | 5 日检查点→`MainSlot`＋回正 | `Saves/p1_run8_day005~120.json` **×24** ＋ `p1_run8.json`（`[P1观察] 检查点 D40/45/…/120: p1_run8_dayXXX=True 回存p1_run8=True`）；**未覆盖 `p1_run6`/`p1_run6b`/`p1_run7`** |
| 5 | 120 日熔断（＋提前达标＋灭绝停跑） | 熔断触发 @D120（见上）；本跑未触发提前达标（0 军事期）、未触发全灭（存活 1） |
| 6 | 收尾 `ExitTestRun`＋封盘 | 状态档 `终速=0 存盘 p1_run8=True`；**实测 `isPlaying=False`**（容器末尾 `ExitPlaymode`，**L-32 兑现＝无 1x 余留世界**） |

## 三、判定线主证据与**机理实证**（§三-1）

### 3.1 🔴 主阻断（结构性）＝ **D348 兵力目标自限（≤3）与军事期阈值（≥4）冲突**

- **闸门**：[ScriptStageMachine.cs:96-101](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/ScriptStageMachine.cs#L96-L101) 扩张→军事需 `warriorCount ≥ expandToMilitary_warriorsMin` ＋ `population ≥ 12` ＋ `expansionChunks ≥ 2`。
- **实盘 `militaryTarget`（D348 目标，DiagMilitary 逐日）**：k1 ＝ **2 值 22 天／3 值 97 天**；k3 ＝ **2 值 43 天／3 值 76 天** —— **全程从未 ≥ 4**。
- **实盘 `warrior`（k3 抽样）**：D10 0 → D40 **1** → D50 **2** → D60 **3** → **D70/80/90 恒 3** ⇒ **⑦ 招到 target（3）即停，被钳在 3 < 4**。
- **⇒ k3 建军链全通仍差 1 兵**：⑰ 落地（`⑰a/⑰b need=0.000` 93/87 天＝Barracks/TrainingCamp 在场）＋ ⑦ 可招（`feasible=True` **14** 天）＋ 招兵落地（warrior 0→3）——**唯一差的就是第 4 个兵，而 target 自限卡死**。**这是本批判定线未 PASS 的决定性结构原因**（与 D589「袭扰依赖」同源：无袭扰→邻国威胁低→D348 target≈3）。

### 3.2 次阻断＝**资源链断裂**（DiagCapacity 实盘）

| k | D60 | D120 | 后果 |
|---|---|---|---|
| k1 密林 | Food prod=2/in=45 grainDays=105.6 | Food prod=2/in=0/out=12 grainDays=399；**Stone prod=0/in=0**（存量 3）；**Wood prod=0** | 存活但**石材枯竭**⇒ 建不了 Barracks（需 10 石）⇒ ⑦ 无建筑前置 |
| k2 磐石 | Food in=0 grainDays=0 | 全 0 | **全灭 D60** |
| k3 铁蹄 | Food in=8/out=14 grainDays=15.8 | Food prod=2/in=0/out=0 | **全灭 D103**（前段建军链已通） |
| k4 霜岩 | Food in=0 grainDays=0 | 全 0 | **全灭 D48** |

- 断链面：[DiagChain] k1 D120 `missingMustHave={kind=0 req=3 act=2}{kind=2 req=4 act=1}`（可读口径）。

### 3.3 焦点霸占（与 HH.189「wall 181 次」同族现象复现）

- census top 计数（全期）：**k1 `BuildWall` 97/120**；**k2 `BuildWall` 115/120**；**k4 `BuildWall` 107/120**；k3 仅 8。
- ⇒ 三国焦点被 `BuildWall` 长期占用，`⑦/⑰` census top **全 0**（k1 `RecruitWarrior` 0／`BuildBarracks` 0）⇒ **建军链从未启动**（k3 是唯一例外，证明 ⑰↔⑦ 咬合**在能走到评分层时确实有效**）。

## 四、回归照看（§三-2，L-31 双向）

| 面 | 证据 | 结论 |
|---|---|---|
| **⑰ 军事建筑落地** | k3 `⑰a建兵营 need=0.000` **93 天** ＋ `⑰b建训练营 need=0.000` **87 天**（= 建筑在场体；`CountActiveDef≥1⇒0` 同 case）＋ census top `BuildBarracks` 1／`BuildTrainingCamp` 2 | **落地 ✅**（k3） |
| **⑦ 招兵落地** | k3 `⑦招战士 feasible=True` **14 天**、census top `RecruitWarrior` **13 天**、`warrior` 0→1→2→**3** | **落地 ✅**（k3） |
| 其余三国 | ⑦/⑰ census top 全 0、⑦ feasible 全 False（无训练建筑前置） | 未启动（§3.2/3.3 已归因） |

## 五、三军事向专属营（§三-3，答 **DZ-103**）

- **落地计数 ＝ 0**（census top 全期无 `BuildWarCamp`/`BuildArcheryRange`/`BuildWarAcademy`；无一国达军事期）。
- **归因（非 need 口径缺陷）**：三营 asset `minStage=3`（军事期）⇒ [UtilityScorer.cs:127](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L127) 阶段门控 ⇒ **本跑全程 `stageGateBlocked=True`** ⇒ 不可见。
- **need/族门禁均正常**：`need=1.04~1.09`（真实缺口，非恒定 0.5）；**族门禁逐国精确** —— k1（精灵）`ArcheryRange feasible=True` **110/119 天**；k3（兽人）`WarCamp feasible=True` **27/天**（受木料成本门控制）；余者全 False。
- ⇒ **DZ-103 收口口径**：三专属营「0 落地」＝**军事期未达的可见性前置**所致，**与 HH.208 换 need 口径无关**（口径 side 已由 HH.208/D661 结清）。

## 六、观测口与证据文件（§三-4/§三-5）

- 镜像日志 `Logs/P1/p1_log_20260911_205015.log`（**3.52MB，D 行 40,114**；含 `[DiagMilitary]`/`[DiagCapacity]`/`[DiagChain]`/`[P1观察]`）
- 日快照 CSV `Logs/P1/p1_snap.csv`（本 session `20260911_205015` **716 行**，day 1→120 完整；含 `stage` 列）
- 检查点 `Saves/p1_run8_day005~120.json` ×24 ＋ `p1_run8.json`
- 收工档 `Logs/P1/hh80_run_status.log`；侦察 `Logs/P1/hh80_scout_result.log`
- 确定性（D657⑥ 长局口径）：**关键事件级**——本次关键事件＝「零军事期过渡／四军终态 `Expand`／k3 建军链双端落地」，均**逐日 CSV＋镜像可复现**（**不作逐值/逐字节断言**）。

## 七、门禁与红线自检（§三-6）

- ✅ 正门 `EnterTestRun`＋15x；守卫全开（**k0 全灭无 GameOver 实证**，D585）
- ✅ **跑局期业务代码零改动**（`Assets/_Game` 无 diff）；**AI.Core 零触碰**；枚举尾插未动（L-28）；无参数微调（D563③）
- ✅ **收工退 Play**（L-32；容器 `ExitPlaymode` 生效，实测 `isPlaying=False`）
- ✅ 容器级改动**已列报**（HH.211 §二：`SEED 64513`／`SLOT p1_run8`／`SEEDS{70403,82007,64513}`／L-32 两处收尾）；seed 报备带阳性对照（L-29）
- ✅ 写-改-commit 同串、只提本串、不 push；交付即 commit（D640 #9）

## 八、判读义务（HH.185/122 口径，本批可见面）

| 项 | 本批判读 |
|---|---|
| D563③ 兽人首训 `effDays`／兵 0→N | **不可判读**（兽人 k3 未达军事期、无首训）；兵 0→N：**k3 0→3** ✅（⑦ 落地） |
| D569/D579 水晶增长／Vault 积压 | **不可判读**（镜像白名单不含业务/国库 tag；`TreasureVault` 仅玩家主城口径）⇒ 列报 |
| P0 行为面（⑤三通道／③产能种类化／派工权重／断链自愈） | 观测面数据在案（`[DiagCapacity]`/`[DiagChain]`/`[DiagTriage]`/`[DiagBias]` 逐日）；**本批不逐项裁读**，供后续按需取用 |
| 军事期判定面（⑯⑰／⑦／姿态） | 见 §三/§四（⑯ 未触发、⑰/⑦ 仅 k3 落地） |

## 九、列报 / 请裁

1. **🔴 结构性阻断两笔（本批判定线未 PASS 的直接原因，非本批口径问题）**：
   - **(A) `militaryTarget`(D348) ≤3 vs `expandToMilitary_warriorsMin`(=4)** ⇒ 无袭扰/低威胁环境下 **AI 永不可能攒够 4 兵**（k3 建军链全通仍停在 3）⇒ **军事期结构性不可达**。请裁方向：①调整 D348 目标下限/威胁敏感度 ②调阈值 4 ③其他（**执行端不擅动数值**，D563③）。
   - **(B) 资源链断裂**（k1 石枯竭 `Stone prod=0`；k2/k4 粮链死）⇒ 三国早灭、k1 无石建兵营。与 HH.193 §七-3 列报同项。
2. **焦点霸占 `BuildWall`**（k1 97/120、k2 115/120、k4 107/120）——HH.189「wall 选址失败 181 次」同族现象复现，建议与 (A)/(B) 同批处置。
3. **正面实证（建议嘉奖/入档）**：k0 工人 0 而**跑满 120 日无截断** ⇒ 正门守卫（D585/D249）治本生效，与六考 D45 截断形成对照。
4. **观察项**：①k1 `Stone prod=0`（HH.193 §七-3 同项）②本跑 AI 数 = 4（k1~k4，seed 64513 无 k5）③`p1_snap.csv`/镜像文件**目录元数据 mtime 滞后**（实测内容完整，取数应以内容为准，勿信 `LastWriteTime`）。

---
*交付：执行端 2026-09-11（HH.214）。**判定＝P1 终验收未 PASS**（熔断 D120，0/4 AI 军事期），机理已逐条实证；请策划端验收＋对 §九-1 结构性阻断裁示。*
