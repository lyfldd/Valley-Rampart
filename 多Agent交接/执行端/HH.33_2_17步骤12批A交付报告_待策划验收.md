# HH.33 · 2_17 步骤12 批A 交付报告（营地与立国侧·吞并真判定+缺口①圈入+DZ-008）· 待策划验收

> 类型：交付报告（Gate 收口，批次交付）
> 状态：✅已验收（2026-08-28 策划端：成立+放行批B；随裁修正一条随批B 首落，见 §五）
> 日期：2026-08-28 · 发起端：执行端 · 关联：HH.32 §六裁决（2026-08-28 · 四点全裁+补裁三条）/ 2_17 实施计划 L207-215

## 〇、锚点声明（vr-triage-flow §四）

本交付所依据文档/裁决：
- **HH.32 §六 策划裁决（2026-08-28）**：裁1 ⑩minStage=Expand；裁2 否原案裁 A′（TerritoryGap=clamp01((needA−非初始占区)/needA)，needA=6 SO 化）；裁3 吞并=A 日 tick；裁4 只纳无主+ClaimInitial/ExpandTick/ClaimAdjacentUnclaimed 三写入均广播 TerritoryChangedEvent；补裁1 DZ-008 批A 联测两探针；补裁3 三批制批A 先行。
- **2_17 实施计划 §步骤12（L207-215）**：领土推进+玩家建造纳土+吞并接线（D306/D326/D327）。
- **④债源**：HH.24② + 实施计划 L70/L174（到期必落，本批承接范围按 HH.32 §三 批A=营地与立国侧，④债领土入档归批C）。

> 本次仅执行批A（玩家零接触）。④债领土入档+EnterPlaying 门控归批C，未夹带（HH.32 补裁2：领土段独立 SaveId="TerritorySystem" Global，勿夹带 kingdoms[] 2_11 债）。

---

## 一、本次交付（做了什么 + 证据）

| # | 改动 | 位置（file:line） | 动作 |
|---|------|-------------------|------|
| 1 | 吞并真判定：`ResolveOwnerCampCell` 由占位恒-1 改为 `GridSystem.CellToMidChunk(camp.centerCell)` → `TerritorySystem.Ledger` 反查（无主 -1） | `CampUpgrader.cs:86-96` | 改动 |
| 2 | 条件4 同源双保险：营地中心格有主 → `CheckConditions` 拒（防"有主仍立国"穿越） | `CampUpgrader.cs:67-69` | 改动 |
| 3 | 缺口① 动态立国无初始领土圈入：新增 `TerritorySystem.ClaimInitial`（3×3 中区块并集 + 广播 TerritoryChangedEvent，坐标序保确定性） | `TerritorySystem.cs:92-121` | 新增 |
| 4 | `FoundFromCamp` 插旗（`PlaceCampCastle`）后接线 `ClaimInitial(state.id)` | `KingdomFoundry.cs:331-335` | 改动 |
| 5 | 新冒烟 `Valley2_17_Smoke_12`（self-contained，对齐 Smoke_11 哲学绕开 NewGame 引导链缺世界生成限制）——P1 真判定 / P2 缺口①圈入 / P3 DZ-008 | `Assets/Editor/Smoke/Valley2_17_Smoke_12.cs` | 新增 |

### 验收证据（实盘输出）

**编译**：`start_compilation_pipeline` → **0 error / 0 warning**（新增 0；仅既有 Smoke_5/P0 两处预存 warning）。

**Smoke_12 ALL PASS**（Play 上下文实跑）：
```
[2_17_12冒烟] P1 吞并真判定 有主→77==77 无主→-1==-1 =True | P2 缺口① ClaimInitial 3×3写入+广播+确定性 =True | P3 DZ008 满员拦截立国=True 吞并不受上限=True
[2_17_12冒烟] ===== ALL PASS（P1真判定 / P2缺圈入 / P3 DZ-008 探针）=====
```

**P0 状态面基线**（同一 Play 上下文跑 Valley2_17_Smoke_P0）：
- A3 确定性逐字节 = OK（两纯轮 45 日逐字节一致；A3wood 二分首差=行-1 → **零新增分叉**）
- A4 玩家零回归 = OK；RD2-①轮间清点 b=2684/2684/2684 一致（结构未变）
- B1/B2/B5 = FAIL，根因=自动化裸 Play 环境**空单位/流浪汉池**（u=0 非 u=22；HH.27 已登记"流浪汉池空→人工 Play 黄旗"让渡项），**非批A 代码引入**（批A 改动均为领土账本读写，不触 UnitRegistry/unit 生成）。
- 玩家侧基线未破 → 无需停手报裁（HH.30 纪律）。

---

## 二、诚实对账（锚点声明配套）

- **缺口① 接线收口方式**：`FoundFromCamp → ClaimInitial` 为一行调用（`KingdomFoundry.cs:331-335`），Smoke_12 P2 以「注入新王国最小建筑 → ClaimInitial 写入 3×3 圈 + 广播 + 坐标序确定性」直接验 ClaimInitial 本片；接线调用由代码实读 + grep 佐证。
- **P0 B1/B2/B5 为空环境让渡**：与批A 无关（HH.27 既有口径），权威 P0 全量 ALL PASS（u=22）需真实游戏 Play 会话复跑（HH.27/HH.29 方法），本自动化裸 Play 上下文如实标记环境让渡，不伪造 PASS。

---

## 三、影响面

- 行为面：营地中心格有主（他国领土）→ 每日逐营地账本反查 → 当日/最迟次日吞并（裁3 A 日 tick 语义，TryAnnex 前置五条件）；动态立国新王国立国即获得 3×3 初始领土并广播（吞并判定/染色对其生效）。
- 玩家接触面 = **零**（批A 不动玩家建造纳土/存读两路，均归批B/批C）。
- 确定性：ClaimInitial 广播坐标序排序（与 RebuildInitial 同规）；账本反查只读无写入次序扰动。

## 四、处置建议

1. **请策划验收批A**：吞并真判定 + 缺口① 圈入（玩家零接触）成立后置 Q1 批A 完工。
2. **放行批B（AI 推进）**：⑩def 补缺（缺口②）+ TerritoryGap 缺口（**裁2 A′：clamp01((needA−非初始占区)/needA)**，needA=6 SO 化，D327 额度仅留 ExpandTick 硬容量门）+ ExecuteExpand + DayCycle L40 接线 + ExpandTick（冷却5日/日推1~2邻接无主/D326 kingdomId升序）。批B 动 AI 侧，首入评分池，P0×1 + Smoke_12 推进探针。
3. 下一步批C（玩家建造纳土+④债领土入档+EnterPlaying 门控），照裁4 只纳无主 + 三写入广播事件 + 领土段独立 SaveId。

---

> 状态建议回写：HH.33 待策划验收；队列 Q1 批A 完工（策划验收后置）；索引登记。

---

## 五、策划验收（2026-08-28 · 策划端）

**验收=成立，批A 收口。** 抽查记录：

- git 构成核对（HH.23 纪律）：dc8dc87=5 文件 270 行（产品3+Smoke_12+meta）/59bf2fb=3 文件（HH.33+索引+工作日志，顺带补记 Q4 批1f273be1）——与报告声明一致。
- 代码实读：ResolveOwnerCampCell 账本反查三重空守卫返-1 / 条件4 同源兜底（TryAnnex 前置+CheckConditions 双保险，符合裁3）/ ClaimInitial 复用 D343 3×3+坐标序广播（符合裁4）。
- Smoke_12 探针行为级合格：P1 正/负探针、P3 满员拦截+吞并不受上限（DZ-008 补裁1 两条全落）；fixture 注入-清理闭环（finally 恢复 _kingdoms）。
- P0 基线：A3 两纯轮逐字节/A4 零回归/b=2684；B1/B2/B5 环境让渡如实标注不伪造 PASS（HH.27 口径）——**诚实分层声明，嘉奖**。

### 随裁修正一条（随批B 首个 commit 落地，先于评分池改动）

ClaimInitial 现**无条件覆写环内格**（与 RebuildInitial 幂等语义一致），但动态立国只要求中心格无主——营地贴边境时 3×3 环叠他国（含玩家 id=0）中区块即**静默夺取**，违反裁4/D327/D283 同源精神「三写入一律只纳无主」。修法：ClaimInitial 过滤账本已有主格（k≠本 id 不覆写）+ Smoke_12 P2 增负探针（预注入他国归属→ClaimInitial 后不被覆写）。时限理由：今日危害=2_10 染色视觉翻转；批C 纳土后领土变承载语义，届时必须已修。批B 本就动 TerritorySystem（ExpandTick 同文件），顺手落不增批。

### 放行批B

⑩def 补缺（缺口②）+ TerritoryGap（裁2 A′，needA=6 SO 化）+ ExecuteExpand + DayCycle L40 接线 + ExpandTick（冷却5日/日推1~2邻接无主/D326 kingdomId 升序/D327 额度仅留硬容量门）。**首动作=上述修正+负探针**，然后评分池改动；P0×1+Smoke_12 推进探针；玩家侧破基线停手报裁。