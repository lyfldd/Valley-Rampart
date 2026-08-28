# HH.32 · 2_17 步骤12 设计报告（领土推进 + 吞并接线 + ④债清偿）· 待策划裁决

> 类型：策划报告请求（Gate 前置——先报设计再动代码，HH.24 之裁）
> 状态：⏳待策划裁决
> 日期：2026-08-28 · 发起端：执行端 · 关联：HH.30 §七（步骤11 收官确认）/ 2_17 实施计划 L208-216（步骤12 原始定义）/ ④债源=HH.24② + 实施计划 L70/L174（到期必落，不可再顺延）
> 口径声明：引用实施计划已对照 HH.31 对账（🟡 账本滞后项存在），本报告全部以**代码实读 + 最新裁决**为准，行号均标注。

---

## 〇、执行端勘察声明（重读代码，非凭记忆）

实读文件：`TerritorySystem.cs`（全文 91 行）/ `CampUpgrader.cs`（全文 94 行）/ `KingdomFoundry.cs` L300-360（出口A+共用转换）/ `LoadManager.cs` L112-141（EnterPlaying 注入点）/ `DayCycleSettlement.cs` L40（领土预留位）/ `UtilityActionConfig.cs`（全文，14 def 逐条清点）/ `UtilityScorer.cs` L29（Expand 枚举位）/ `GameEvents.cs` L568（TerritoryChangedEvent 结构）。关键结论逐条带行号。

---

## 一、必答三问

### 问a 领土推进规则（TerritorySystem 从只读转推进：触发/节奏）

**现状（勘察）**：TerritorySystem 是纯只读账本（头注释 L7「只读 + 染色事件，不推进」）——唯一写点 `RebuildInitial`（L45，两路都跑）+ 广播 TerritoryChangedEvent（L84）；无推进 API、无日 tick。DayCycleSettlement L40 预留位注释「领土变更（P0 占位空跑——AI 推边界/玩家建造纳土归步骤12）」。

**设计（照实施计划 L212-213 数字，不发明）**：

| 项 | 规则 | 落点 |
|---|---|---|
| AI 触发 | ⑩Expand 被评分选中 → ExecuteFocus 下发 ExecuteExpand | ⑩ 枚举位已在（UtilityScorer.cs L29）但 **UtilityActionConfig.actions 14 条无 Expand def（勘察缺口，见 §二）**→ 本步补 def（axis=Expansion、need=TerritoryGap 新缺口、minStage 待裁 §五-1）+ UtilityScorer 加 TerritoryGap 分支 + KingdomBrain 加 ExecuteExpand |
| 推进节奏 | 每日 1~2 块**邻接无主**中区块（可走率 ≥50%）；冷却 5 日；同日多国竞争按 kingdomId 升序（D326 确定性）；扩张额度 `clamp(4+工人−非初始占区, 0, 96)`（D327/D341） | TerritorySystem 新增 `ExpandTick()`（遍历王国 id 升序 → 冷却/额度门 → 邻接无主可选集排序 → 推块 → 写账本 → 广播 TerritoryChangedEvent） |
| 入口接线 | DayCycleSettlement L40 预留位 → 调 TerritorySystem.ExpandTick() | 五步权威序不动，只在预留位接入（步骤8 骨架纪律照旧） |
| 玩家纳土 | 建筑落成 → 邻接无主中区块自动纳入（D327）；**只纳无主**，有主（他国）不动 | 建造落成回调接线（BuildController 落成点 → TerritorySystem.ClaimAdjacentUnclaimed(kingdomId, coord)） |

### 问b 吞并接线（2_16 出口B 判定占位 → 真触发）

**现状（勘察）**：执行端管线**已备**——CampUpgrader.TryAnnex（L73-83：ConvertVagrantsToWorkers L79 + RemoveCamp L80 + 日志）+ KingdomFoundry.ConvertVagrantsToWorkers（L347，两出口共用，人口守恒）。缺的只有**真判定**：
- `ResolveOwnerCampCell`（L86）**占位恒 -1=无主**（注释「真判定接线归 2_17 步骤12」）
- `CheckConditions` 条件4「营地中心格无主」（L67）**占位恒真**

**设计**：
- ResolveOwnerCampCell 真判定：`GridSystem.CellToMidChunk(camp.centerCell)` → `TerritorySystem.Ledger` 反查归属 id（无主返 -1）。
- 条件4 真判定：同源查询——中心格有主 → 不立国（TryAnnex 在前置已拦截吞并，条件4 为同源双保险，防「有主仍立国」穿越）。
- **即时性两案（请裁 §五-3）**：
  - **A 日 tick 查询（执行端建议）**：TickAll 每日已遍历营地，TryAnnex 内直接查账本。延迟 ≤1 日（领土圈入/推进后最迟次日吞并），零事件接线，确定性最好。D306 无即时性要求。
  - B 事件即时：订阅 TerritoryChangedEvent，格子易主即吞并（贴实施计划 L214 字面）。接线多一层，且事件广播在 RebuildInitial 会一次性全量广播（需防重复触发）。

### 问c ④债清偿方案（RebuildInitial foundKingdoms 门控——到期必落）

**债原文（实施计划 L70/L174）**：RebuildInitial 现两路（新游戏/读档）都跑（P0 领土无存档、读档从建筑重推=正确）。**领土入档/扩张落地时必须加 foundKingdoms 门控**（新游戏走 RebuildInitial、读档走存档恢复=领土账本=存档值），与 FoundFirstGeneration 门控（2_16 根因二）同族，**防读档把扩张后领土重置回初始圈入**。

**清偿方案（与 kingdoms[] 拆分 2_11 解耦，不阻塞）**：
1. **领土入档**：TerritorySystem 实现 ISaveable（SaveId="TerritorySystem"、SaveLoadPhase.Global——WaterNetwork 同款先例），存 `List<{x,y,kingdomId}>`（账本序列化）。
2. **门控**：LoadManager.EnterPlaying（L129-130 现无条件 RebuildInitial）改为——**读档且存档含领土段 → LoadState 恢复**；**新游戏或旧档无领土段 → RebuildInitial 兜底**（旧档向后兼容不炸）。门控判据用「存档段存在性」，语义即 foundKingdoms 同族（读档=恢复不重推）。
3. **验收探针**：正=读档后账本=存档值（扩张领土保真）；负=新游戏仍重推初始圈入；旧档（无段）读档回退 RebuildInitial 不报错。

---

## 二、勘察新发现（诚实对账，两项缺口，报告必含）

1. **缺口① 动态立国无初始领土圈入**：FoundFromCamp（L301-345）PlaceCampCastle 插旗（L329）后**没有**任何领土写入——RebuildInitial 只在 EnterPlaying 跑一次，动态立国新王国的城堡不入账本 → 它判不出自己的领土、吞并判定也拿它没办法。**本步补**：FoundFromCamp 调 `TerritorySystem.ClaimInitial(state.id)`（复用 3×3 圈入逻辑）+ 广播事件（出口B 吞并因此才能对动态立国生效）。
2. **缺口② ⑩def 缺失**：UtilityActionConfig.actions 现 14 条（①~⑥+⑦⑧⑨⑫+⑬⑭），**无 Expand(⑩)**——步骤10 报告的「15 项全量」实为 14+⑪⑮占位。本步补 ⑩ def 后 15 项真全量（⑪⑮占位桩不动照裁）。

---

## 三、实施序与批次建议（沿 HH.30 三批制·按玩家侧风险分）

| 批 | 内容 | 风险 | 验证 |
|---|---|---|---|
| 批A·营地与立国侧 | 吞并真判定（ResolveOwnerCampCell+条件4）+ FoundFromCamp 初始圈入（缺口①） | 玩家零接触（营地/AI 国侧） | P0×1 + Smoke_12 吞并探针 |
| 批B·AI 推进 | ⑩def（缺口②）+ TerritoryGap 缺口 + ExecuteExpand + DayCycle L40 接线 + ExpandTick | AI 侧（⑩首入评分池，行为面新增） | P0×1 + Smoke_12 推进探针 |
| 批C·玩家纳土+④债 | 建造落成纳土回调 + 领土入档 + EnterPlaying 门控 | **动玩家真实接触面**（建造回调+存读两路） | Smoke_12 正/负探针（执行端自跑）+ P0×1 |

每批单 commit（写-改-commit 同串关窗，vr-triage-flow §三纪律）；批内玩家零回归逐文件核证。

---

## 四、验收方案

- 实施计划 L216 期望「冒烟 #6/#7/#8/#15/#18 全绿」：现有套件 Smoke_5/7/8/9/2b/FixCard/Treasury2a——**#6/#15/#18 无对应文件=步骤12 新写**；本步落 **Valley2_17_Smoke_12**（领土三探针：推进节奏/吞并接线/门控恢复）承接其验收语义；#7/#8 既有覆盖面实施时核对，不重叠不漏项。
- P0 锚定值判据照旧（b=2684/u=22 族）。诚实声明：pump 无帧 → 建造落成纳土与逐帧推进归人工 Play 黄旗（照 HH.27 让渡口径）；冒烟与 P0 验纯谓词+状态面。

---

## 五、待策划裁决（请拍板四点）

1. **⑩ minStage**：建议 `Expand`（D327 额度公式的扩张语义；Develop 期 AI 忙内政）；备选 Develop 后期。
2. **TerritoryGap 缺口口径**：建议 `needA=目标领土块数（占位 6）`，评分=clamp01(非初始占区数/needA) 单调；备选按额度余量。
3. **吞并即时性**：建议 A 日 tick 查询（延迟≤1日、零事件接线）；备选 B 事件即时（贴实施计划 L214 字面，需防 RebuildInitial 全量广播重复触发）。
4. **玩家纳土边界确认**：只纳**无主**（D327 字面），他国领地上的玩家建造不触发任何领土变更、也不吞并——确认或反驳。

> 裁后按 §三 批序动工；中途玩家侧基线破 → 停手报裁（HH.30 纪律照旧）。
