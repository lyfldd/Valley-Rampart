# HH.266「美术接入收口批（HH.264）」交付报告 · ① A+B 段（播放器补完 ＋ P1~P8 行为探针）

> 类型：交付报告（**分段交付 · 第①段＝A+B**）｜状态：🟡 **待验收（A+B 段）**
> 日期：2026-09-13 · 发起端：执行端 · Gate：`G3-1` · 依据 D709／HH.264 任务书／`美术资源接入_SpriteAnimator播放器设计规格.md`（**唯一实现规格**）
> 取号：**HH.266**（账本水位线 265→266 · D640 #10 禁预留；HH.265＝本批开工回执）
> 交付形态：**A+B 先交**（任务书 §二「分段交付允许·鼓励」）——**C（H5 压测）／D（H6 图集）待交第②段**
> commit：本批代码 `0d34e11`（A+B）／取号 `f…`／本报告（见 §七）

---

## 〇、一句话结论

`SpriteAnimator` 已按**规格**补齐：**A1~A6 六项全落**（`SpriteAnimatorConfig` SO 在场 · F-04 完成回调 · F-09 三级回退链 · F-13 接既有 `LODSystem` · F-15 钩子只预留 · F-14 池复位）；
**§7.2 行为级探针 P1~P8 逐条读数取证 —— ALL PASS（11/11 探针 + 12/12 含收尾）**；编译 0 error，正门进局、收尾 `ExitTestRun`＋`QuitSmoke`＋已退 Play。
**修掉 3 个真缺陷**（均为探针实测暴露）：攻击意图被 hold 截断 / 随机相位被同 tick 覆写 / 某缺图路径漏一次性告警。

---

## 一、A 段（播放器补完）逐项落实

| # | 规格项 | 落实 | `file:line` 锚点 |
|---|---|---|---|
| **A1** | §三 `SpriteAnimatorConfig` SO | ✅ **资产在场**：`Assets/Resources/Config/Art/SpriteAnimatorConfig.asset`；**实读字段**＝`defaultFps=12`／`fpsByState=[12×6]`／`attackSpeedScale=1`／`runAsWalkSpeedScale=1.5`／`lodFpsScale=[1, 0.5, 0]`（按 `LodLevel` Active/SemiActive/Dormant 三档）／`stateFallback=[0,0,0,1,0,-1]`（Attack→Idle・Run→Walk・Loot→Idle・Death 无回退）／`lodRefreshInterval=0.5`／`stateMode=[0,0,1,0,2,2]`（Loop/Loop/OnceReturn/Loop/OnceHold/OnceHold）／`workAttackLoop=true`。**硬编码值全部迁入 SO**（Driver/Animator 内零 fps/mode 常量） | 类：[SpriteAnimatorConfig.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Rendering/SpriteAnimatorConfig.cs)；落盘：[ArtImportPipeline.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/ArtImportPipeline.cs) `EnsureAnimatorConfig()` |
| **A2** | **F-04 单次＋完成回调（P0）** | ✅ `AnimSlot` 语义由 Driver 平铺槽承载；`SubscribeComplete(cb)`；`OnOnceFinished(mode)`＝**按播放模式收口**（`OnceReturn`→清态回落／`OnceHold`→**定格保留**）＋回调**一次性**触发；**death 播完 ⇒ 衔接既有死亡流程**（`Die()` 里 `HasFramesFor(Death)` 判在场：有帧则 `SubscribeComplete(ReturnToPoolAfterDeath)` 延后回收，无帧则**即时回收＝既有语义零变化**）；**F-14 池复位回调清空** | [SpriteAnimator.cs:173-174](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/SpriteAnimator.cs#L173-L174) `SubscribeComplete`／[:235](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/SpriteAnimator.cs#L235) `OnOnceFinished(mode)`／[Driver.cs:136](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/SpriteAnimatorDriver.cs#L136)·[:191](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/SpriteAnimatorDriver.cs#L191) 触发点／[UnitController.cs:679](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/UnitController.cs#L679)·[:712-714](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/UnitController.cs#L712-L714) |
| **A3** | **F-09 缺图回退链** | ✅ **三级链**：① 状态缺 ⇒ `stateFallback` 回退态（**最多两跳防环**）→ ② 整套缺 ⇒ **静态立绘** `portrait_{race}_{occ}` → ③ 无立绘 ⇒ **占位**（保持 prefab 原图）；**负查询缓存 + 一次性告警**（`WarnMissing`，含 `token==null` 与机器线两条早期返回路径） | [SpriteAnimator.cs:356-364](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/SpriteAnimator.cs#L356-L364)／[:377-386](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/SpriteAnimator.cs#L377-L386) `WarnMissing` |
| **A4** | **F-13 LOD 动画分层** | ✅ 接**既有** `LODSystem.Instance.GetLevelAt(pos)`（**未另建 LOD 系统**）；Driver 侧 **per-slot 时间闸**（`lodRefreshInterval=0.5s`）＋`OnBecameVisible` 回表立即刷新；`scale = speedScale × lodFpsScale[tier]`，`0` ⇒ 冻结静态帧 | [Driver.cs:141-149](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/SpriteAnimatorDriver.cs#L141-L149)；既有源 [LODSystem.cs:378](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/LOD/LODSystem.cs#L378) |
| **A5** | **F-15 帧事件钩子（预留）** | ✅ `SubscribeFrame(Action<UnitController,int>)`；Driver 在**帧索引变化时**调用；**未接任何游戏逻辑**（全库无 `SubscribeFrame` 业务调用方） | [SpriteAnimator.cs:177](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/SpriteAnimator.cs#L177)／[Driver.cs:205-207](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/SpriteAnimatorDriver.cs#L205-L207) |
| **A6** | F-14 池复位复核 | ✅ `ResetForReuse()`：**清 `_onOnceComplete`** ＋ `Slot.frame` 交下一帧 Loop 入场随机 ＋ `state=-1`（强制回 Idle 相位）＋ 清 LOD 计时/档位 | [SpriteAnimator.cs:243-266](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/SpriteAnimator.cs#L243-L266) |

### 1.1 施工中**实测暴露并修掉**的 3 个真缺陷（探针首跑 5/11 → 修后 11/11）

| # | 缺陷（首跑读数） | 根因 | 修法 |
|---|---|---|---|
| D1 | P3「attack 播完回落」**假通过**：attack 仅 0.4s 就被切回（`观察窗最大攻击帧=0`） | 战斗攻击用 **0.4s hold 意图**定态；而 attack 动画 16 帧 @12fps＝**1.33s** ⇒ hold 先过期，动画被截断（规格 F-05 明确「CD 1s < 动画 1.33s」应按动画播完） | 攻击意图改 **sticky**（`_attackIntend`），由 **once 播完** 收口（[SpriteAnimator.cs:82](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/SpriteAnimator.cs#L82)） |
| D2 | P1「随机相位」失效（`f0a==f0b==f0c`） | 入场选随机帧后 `timer` 仍为 0 ⇒ **同 tick 推进**把 `frame` 覆写成 0 | 入场时 `timer = frame / fps`（相位与时间轴对齐）（[Driver.cs:125-126](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/SpriteAnimatorDriver.cs#L125-L126)） |
| D3 | P7「一次性告警」条数=0 | `token==null` 早退路径**未走** `WarnMissing` | 抽出 `WarnMissing` 并覆盖全部缺图早退路径 |

> D1/D2/D3 **均为 HH.263 版实现潜伏、由本批 §7.2 探针首次暴露**——即规格 §7.2 作为验收门槛的必要性实证。

---

## 二、B 段 §7.2 行为级探针 P1~P8（**逐条读数**·原始日志）

> 探针容器：[Valley_HH264_AnimProbe.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH264_AnimProbe.cs)（Editor-only）；正门 `TestHarnessApi.EnterTestRun(seed=21107, 1x)`；原始读数落盘 `Logs/hh264_anim_probe.log`（逐条 flush，防域重载清控制台）。
> 例：`P0 前置：探针单位 warriors=3 worker=1 | 帧集=True 帧数(idle)=8 | LOD档位(探针区)=0 | 档位分布 Active=4 Semi=0 Dormant=32`

| # | 探针 | 实测读数（原文） | 判定 |
|---|---|---|---|
| **P1** | idle 循环 | `有效帧数=8（切9播8） 观察窗最大帧=7（≤7＝末格空不播） 最小帧=0 回绕推进次数=19/1.6s 起始相位 w1=7 w2=5 w3=5（3 单位相位对差值=2≥1） state=0` | ✅ |
| **P2** | walk＋flipX | `右移 反向翻转次数=0（无抖动） 换帧次数=7 期间state=1(walk) 左移 反向翻转次数=1（只翻一次） 现flipX=True（左向）` | ✅ |
| **P3** | 战斗 attack 单次 | `前置state=0(idle) 触发后state=2(attack) 观察窗最大攻击帧=15（＝末帧·播满16帧） 播完回落state=0 ⇒ OnceReturn 回落成立` | ✅ |
| **P4** | **工人工作 attack 循环** | `Working 期 state==attack 占比=25/26 mode=0(Loop)｜停发工作通知 0.6s 后 state=0（回落）` | ✅ |
| **P5a** | OnceHold 定格＋回调 | `回调触发次数=1 末帧=7/7 0.5s 后仍=7（定格不动） state=4(loot)` | ✅ |
| **P5b** | death 路径 | `death 真帧在场=False（素材缺） onOnceComplete 触发次数=1（即时＝既有回收零延迟） sprite 前=后（保持不崩）` | ✅ |
| **P6** | 打断重播 | `触发前帧=4 重触发后帧=0（回到起点） token=6→7（+1＝F-05 令牌生效）` | ✅ |
| **P7** | 缺图回退 | `HasRealFrames=False（回退末端） 连续 1.0s 告警条数=1（禁刷屏） sprite 前=后（不崩·保持原图）` | ✅ |
| **P8** | LOD/不可见 | `档位对照 近 tier=0(scale=1) 帧推进/1s=12 vs 远 tier=2(scale=0) 帧推进/1s=0（冻结生效）｜档位分布 Active=4 Semi=0 Dormant=32｜不可见注销：位移后 IsRegistered=True（相机数=1）回调直证 True→OnBecameInvisible∈活跃表=False→OnBecameVisible∈活跃表=True` | ✅ |
| — | 汇总 | `==== HH264 P1~P8 汇总：PASS=11 FAIL=0 ==== / 收尾 PASS=12 FAIL=0 ====` | ✅ |

**P4 接线**：`TaskScheduler.FaceAndShowWorking`（Working 期每 tick）→ `UnitController.NotifyWorkVisual()` → `SpriteAnimator.NotifyWork()`（[TaskScheduler.cs:546](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L546)／[UnitController.cs:280](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/UnitController.cs#L280)）——**接的是真实工人 Working 态**（非人工造态）。

---

## 三、`L-34` 五列在线判据表

| 判据 | 可判定最早时点 | 命中即停 | 作用域 | 口径来源 | 结果 |
|---|---|---|---|---|---|
| P1 idle 8 帧回绕＋随机相位 | 单位入队后 **1.6s 窗** | 最大帧 >7（播到空末格）或推进=0 ⇒ 停 | 3 个探针战士 | `CurrentFrameIndex` 逐帧采样 | ✅ 19 次回绕·最大帧=7·相位差=2 |
| P2 flipX 无抖动 | 持续单向移动 **0.6s** | 期间 flipX 抖动 ≥1（同向） ⇒ 停 | 探针战士 | `SpriteRenderer.flipX` 逐帧 | ✅ 右 0 抖 / 左 1 翻 |
| P3 OnceReturn 回落 | 触发后 **2.0s** | 观察窗内未达末帧或未回落 ⇒ 停 | 探针战士 | `CurrentStateId`+帧 | ✅ 帧 15 + 回落 idle |
| P4 工人 Loop 不回落 | Working 期 **1.5s** | 期间 state≠attack 超 2 帧 ⇒ 停 | 探针工人 | `CurrentStateId`+`CurrentMode` | ✅ 25/26·mode=Loop·停发后回落 |
| P5 OnceHold 定格＋回调 | 播完 **+0.5s** | 回调≠1 次或帧未停在末帧 ⇒ 停 | 探针战士（Loot/death） | `onOnceComplete` 计数 + 帧 | ✅ 1 次·7/7 定格 |
| P6 打断重播 | 重触发 **同帧** | 帧未回 0/1 或令牌未 +1 ⇒ 停 | 探针战士 | `RestartToken`+帧 | ✅ 4→0·token+1 |
| P7 缺图回退禁刷屏 | 缺图后 **1.0s**（≈60 帧） | 告警 >1 条或崩 ⇒ 停 | 探针战士（改为无素材 occupation） | 日志计数 + `HasRealFrames` | ✅ 1 条·不崩 |
| P8 LOD/不可见 | 档位刷新后 **1.0s 窗** | 远档推进 ≥ 近档 ⇒ 停 | 同场 Active vs Dormant 单位 | `LodTier`+帧推进+`IsRegistered` | ✅ 12→0·回调直证移入/移出 |

---

## 四、`L-35` 口径来源与排除项

**口径来源（全部实盘直读）**：① SO 字段＝`AssetDatabase.LoadAssetAtPath<SpriteAnimatorConfig>` 读回；② 探针读数＝`SpriteAnimator` 公开只读面（`CurrentFrameIndex/CurrentStateId/Framecount/LodTier/IsRegistered/RestartToken`）逐帧采样；③ LOD＝`LODSystem.GetLevelAt` 实际返回值；④ 帧源＝`SpriteRefTable` 运行时枚举；⑤ 证据载体＝`Logs/hh264_anim_probe.log`（逐条落盘）。
**排除项**：① 禁文本 grep 判"无引用"（`L-29`；本批用运行时枚举/组件位读取）；② 不采规格 §八 否决清单任一档（Animator/Burst/GPU 动画/分桶分片/上下半身分层）；③ 未改 `AmmoDef`/`GroundEffectDef`（D707 R4 继承）；④ 未另建 LOD 系统；⑤ F-15 未接任何游戏逻辑；⑥ **C 段 Profiler 读数与 D 段 DrawCall 对照未在本段采集**（第②段交付）；⑦ 四族四轮回归未在本段重跑（第②段交付）。

---

## 五、回归与红线自检

- **编译**：`refresh_unity(compile=request)` ⇒ **0 error**（仅存量 `233 node options…` 噪声）。
- **收尾**：`TestHarnessApi.ExitTestRun()` ＋ `SmokeApi.QuitSmoke()`（清场＋清 `smoke_` 槽＋退 Play）⇒ **已退 Play**（`EditorApplication.isPlaying=False` 复核）。
- **零改动面**：`Systems/AI.Core/**` **零触**；`AmmoDef`/`GroundEffectDef` **零改动**；**未另建 LOD 系统**；F-15 未接逻辑；`_任务队列.md` 未触碰；未 push。
- **并发写纪律**：改前重读磁盘；账本/索引**增量改**。

---

## 六、列报（本段）

| # | 项 | 说明 | 建议 |
|---|---|---|---|
| N1 | **death 真帧素材缺** | 328 图无 `_death`（规格 §五「4 帧倒下模板全族复用」未产）；当前 death 走「无帧 ⇒ **即时回调**、不延后既有回收」⇒ **P5b 以机制直证**（回调 1 次、不崩）；OnceHold「定格」行为由 **Loot（怪物真帧）**作等价实证（P5a） | 素材补产后 death 自动走「定格＋播完衔接回收」同链，无需改码 |
| N2 | **Loot 触发方未接** | 规格 §五：「怪物 loot 触发方归 2_14 域，缺触发则不播」⇒ 本批**只备通道**（`NotifyLoot`），未接线 | 照规格维持；2_14 域接线时直接调用 |
| N3 | **不可见剔除未由位移触发** | P8 位移 3000 世界单位后 `IsRegistered` 仍 True（相机数=1）；改用**回调直证**（`OnBecameInvisible`→移出活跃表=False／`OnBecameVisible`→回表=True） | 如实记录：场景相机/剔除策略下位移不必然触发；机制接线已直证 |
| N4 | 探针区初值落 Dormant | 首跑探针区 tier=2 ⇒ 动画冻结、P1~P6 不可判；已用**既有** `LODSystem.SetFocalCenter` 把活跃带按到探针区（tier→0）后取证 | 说明「视口内=Active」的既有语义对表现的必要性 |
| N5 | C/D 段未交 | H5 压测（300~400 四族混编·<0.1ms/帧·GC 0B/帧）与 H6 图集（SpriteAtlas·DrawCall 对照）**第②段交付** | 待本段验收后继续 |
| N6 | 四族四轮回归未重跑 | 本段改动了 `SpriteAnimator`/`UnitController`（全族共用）⇒ 回归应在第②段一并跑（对 HH.263 基线） | 第②段补齐 |

---

## 七、commit

- A+B 代码：`0d34e11`（9 files：`SpriteAnimatorConfig.cs/.asset` · `SpriteAnimator.cs` · `SpriteAnimatorDriver.cs` · `UnitController.cs` · `TaskScheduler.cs` · `ArtImportPipeline.cs` · `Valley_HH264_AnimProbe.cs`）
- 取号：本次提交（账本水位线 265→266·单行）
- 本报告：本文件所在 commit
- **未 push**（承红线）

> 执行端（TraeCode）｜2026-09-13｜HH.266｜① A+B 段交付｜**C/D 段待交第②段**
