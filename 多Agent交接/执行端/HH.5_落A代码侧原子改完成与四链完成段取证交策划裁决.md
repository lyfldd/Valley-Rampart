# HH.5 落A代码侧原子改完成 + 四链完成段取证（交策划裁决）

> 类型：进度同步 + 待决策
> 状态：✅已裁决（2026-08-22 策划端，见文末回写）
> 日期：2026-08-22 · 发起端：执行端 · 关联清单/文档：HH.3 §五§七 / HH.4 裁决二 / 河谷防线_开发计划书.md(顶部08-22日志)

## 一、做了什么（执行端填，带证据）

### A. ③ 代码侧原子改（HH.4 裁决二放行，按三边界执行）
- **③a** `GridSystem.cs` 四个坐标函数（WorldToCoord/CoordToWorld/WorldToSubCoord/SubCoordToWorld）改 iso origin-free，与 `MapRenderService.GridToIso/IsoToCell` 同公式；`GridConfig.cs` 注释同步（"逻辑=格基准，世界=等轴嵌入；HH.3 裁决统一 iso"）。commit `75b495b`。
- **③b** API 全调用方回归 + `SpawnCoordBasisProbe` 扩为全象限 round-trip，判据改**一致性契约**（A 正向 `CoordToWorld(c)==GridToIso(c)` / B 逆向 `WorldToCoord(w)==IsoToCell(w)`）；SUBGRID 措辞改"恢复精确/浮点边界非恢复"。真实场景 **13/13 A+B 全过、恢复 13**。commit `09442e7` `2066275`。
- **③c** `BuildingFactory.cs` `BuildingPlacedEvent` **发布侧剔除**（`isPlayerBuilt` 才 Publish，自然预置不 Publish，非订阅收窄）；`MovementConfig.cs` + asset `npcSpeed` 2.0→**0.716/格**（iso 步长重标定）。commit `5b641e8`。
- **③d** `RulerController.ResolveSpawnPosition`：正交偏移改等轴邻格偏移，君主归位 on-cell `(-0.64, 81.60)`。commit（随 09442e7/2066275 之外）。
- **中止线未触发**：回归无系统性破坏（编译 0 error；全象限探针全过）。

### B. ③ 四链**完成段**取证追验（本会话）
补充 3 处完成段取证日志（最小侵入）：
- ②守卫交锋：`DamageSystem.ApplyDamage`→`[ChainFox] 守卫交锋 ... 受击 N @ pos <- src`（节流点后打，防高频）
- ⑥探雾：`VisionSystem.MarkExplored`→`[ChainPatrol] 揭雾新增 N 格`（仅新揭格时打一条）
- K7撤退：`RetreatToSafeAnchorBehavior.ResolveRetreatTarget`→`[ChainRetreat] 撤退→安全锚点 ...`

Play 精准压敌跑四链，grep 完成段标记：
| 链 | 完成段证据 | 状态 |
|----|-----------|------|
| ① | `[TaskScheduler] 派发/完成 Production 任务`（多次，npcId 19） | ✅ 完整 |
| ② | `[GuardDeploymentSystem] 已部署守卫区域: ore_vein @ (121,131)` + `[ChainFox] 守卫交锋: Undead_Warrior 受击 4 <- ShieldGuard`（经真实 ApplyDamage 命中） | ✅ 日志链路已证 / 有机自动交锋未触发（见决策1） |
| ⑥ | `[PatrolTaskSystem] 发布巡逻: 单位 22` | ⚠️ 巡逻已发；`[ChainPatrol]` 所在`MarkExplored`被 `VisionConfig.Instance==null` 提前返回（本构建迷雾 asset 缺失→禁用），配置态非代码回归 |
| K7 | `[ChainRetreat] 撤退→安全锚点`（大量，敌人边界压近 score 0.22→0） | ✅ 完整 |

## 二、现状与阻塞
- 无硬阻塞；本批代码侧改动 0 error，中止线未触发。
- 完成段取证：①/K7 证据完整；②交锋日志链路已验证但**有机自动大规模交锋**未在沙盒自然触发（敌方按 QQQ 低安全感先撤退避开接战）；⑥揭雾受本构建 `VisionConfig` asset 缺失门控未实跑。
- 3 处取证日志**尚未 commit**（待本 HH 决策后再落库）。

## 三、待决策事项（每项：选项 + 推荐 + 影响）

1. **②守卫交锋完成段判据**——决定是否需补"有机自动交锋"证据。
   - A（推荐）：判 PASS。守卫区域部署（`[GuardDeploymentSystem] 已部署守卫区域`=事件证据）+ 交锋日志经真实伤害路径命中（=计算端到端）已构成 ②完成段取证；有机自动交锋归入后续 WaveDirector 波次集成验收，非本批原子改范围。
   - B：须补有机自动交锋（下批 spawn 波次 或 临时禁敌兵撤退）再判 PASS。

2. **⑥揭雾完成段判据**——`VisionConfig` asset 缺失导致迷雾禁用，`[ChainPatrol]` 未实跑。
   - A（推荐）：接受为"配置门控未实跑、日志点已就绪"。迷雾链在禁态非本批原子改回归；待迷雾 asset 恢复后由该日志点自证。
   - B：须强制启用 `VisionConfig` 重跑取证后再判定。

3. **新增 3 处取证日志去留**——高频风险：`[ChainRetreat]` 在低安全感下几乎每帧触发，刷屏严重。
   - A（推荐）：保留，但 `ChainRetreat` 加节流/降频（如首次落入低分或锚点变化才打），规避刷屏。
   - B：取证完毕即移除，保持零侵扰。

4. **`npcSpeed=0.716/格` 重标定（③c）确认**——作为 ③ 原子改一部分随 commit，不进 B+ 文档族；请策划确认该 iso 步长数值。

## 四、下一步建议
- 决策后：按裁决调整取证日志（节流/移除/保留）→ commit 本批取证改动（保持"文档提交"与"代码提交"分离）→ git-plan-sync 同步开发计划书工作日志。
- 恢复入口：本 HH + `河谷防线_开发计划书.md` 顶部 08-22 工作日志 + `_交接索引.md`。

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 1. ②交锋完成段判据 | **A：判 PASS**。部署事件 + 真实 ApplyDamage 命中=端到端已证；**有机自动交锋未触发的根因恰是 K7 在工作**（敌方低安全感先撤=大脑正确行为，非守卫链缺陷）；大规模有机交锋归 2_14 波次集成验收 | 完成段判据=链路端到端，非特定敌情剧本 |
| 2. ⑥揭雾完成段判据 | **A：配置门控未实跑，接受**。策划核码实证：VisionConfig.asset 从未存在（Resources 无此资产），`VisionSystem L21 cfg==null→迷雾禁用`为设计内降级，非本批回归。**附登记义务：2_10 步骤 9（迷雾渲染）落地时 VisionConfig.asset 必须按 SO 铁律建于 Resources/Config/，且 ⑥ 随步骤 9 重验——此句写入 2_10 实施计划步骤 9 验收行** | 事实核清为"从未创建"而非"丢失"；防迷雾启用时 ⑥ 被遗忘 |
| 3. 取证日志去留 | **A：保留 + 节流**。三处 [Chain*] 为四链常驻回归信标（2_12/2_13/2_14 落地均需重跑四链，拆了还得加回）。节流口径：[ChainRetreat] 仅状态变化时打（首次落入低安全感/撤退锚点变更），禁每帧；[ChainFox]/[ChainPatrol] 现有节流维持 | 信标是耐久验收基建 |
| 4. npcSpeed=0.716 确认 | **确认**。数学复核 √(0.64²+0.32²)≈0.7155，0.716=恰好 1格/秒，构造正确。备忘：旧正交格速随方向漂移（1.56~3.1格/秒），新统一 1格/秒体感偏慢——SO 旋钮，手感调参归小剧场/后续 QQQ，勿现在拍 | 纪律3 要求的即为步长锚定，非手感终值 |

### 分歧裁决记录（有分歧时必填）
- 执行端意见：四项均推荐 A/A/A/确认，与策划一致，无分歧。
- 策划端意见：核码补证决策2事实（asset 从未存在，非丢失）；决策1 补判据框架（端到端链路 vs 特定敌情剧本）；决策4 补数学复核与手感备忘。
- 裁决：A/A/A(节流)/确认 · 依据：AI 北极星（K7 撤退行为本身是②未接战的原因=大脑正确）/ SO 铁律（VisionConfig 建档义务+速度旋钮归调参）/ 验收纪律（信标常驻供 2_12~2_14 重跑）

### 衍生产物
- 新建设计文档：无
- 新建清单任务：2_10 实施计划步骤 9 验收行补"VisionConfig.asset 建档 + ⑥ 随步骤 9 重验"（随本 HH 裁决落档）