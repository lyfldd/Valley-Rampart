# HH.35 · 2_17 步骤12 批B 交付报告（AI 推进·⑩推边界闭环）· 待策划验收

> 类型：交付报告（Gate 收口，批次交付）
> 状态：✅ 批B 验收成立（2026-08-29，用户代表策划端放行）；队列 Q1 批B 完工 → 放行批C
> 日期：2026-08-28 交付 · 2026-08-29 验收 · 发起端：执行端 · 关联：HH.33 §五 验收（2026-08-28 放行批B）/ HH.32 §六 裁1/裁2

## 〇、锚点声明（vr-triage-flow §四）

本交付所依据裁决：
- **HH.33 §五（2026-08-28）**：批A 验收=成立，放行批B；**随裁修正一条**（ClaimInitial 只纳无主 + Smoke_12 P2 负探针）随批B 首 commit 落地。
- **HH.32 §六 裁1/裁2**：⑩minStage=Expand；TerritoryGap 否原案裁 **A′=clamp01((needA−非初始占区)/needA)**，needA=6 SO 化；D327 额度只留 ExpandTick 做硬容量门。
- **HH.32 §六 裁3**：吞并/扩张=A 日 tick（ExecuteExpand 焦点占位、实际=DayCycle 日 tick，避免双写）。

> 随裁修正已先行独立 commit ffc6b92（批B 首落，先于评分池改动，符合 HH.33 §五 指定顺序）；本报告主体=批B AI 推进。

---

## 一、本次交付（做了什么 + 证据）

| # | 改动 | 位置（file:line） | 动作 |
|---|------|-------------------|------|
| 0 | **随裁修正** ClaimInitial 只纳无主 + Smoke_12 P2 负探针 | `TerritorySystem.cs:98-114` / `Smoke_12.cs:ProbeClaimInitial` | 先行 commit ffc6b92 |
| 1 | **KingdomBrainConfig** +⑩ SO 参数（冷却5/日推1~2/可走率≥50%/D327 容量门 β=4 max=96） | `KingdomBrainConfig.cs:100-112` | 新增字段 |
| 2 | **UtilityScorer** `NeedKind.TerritoryGap` 分支（裁2 A′）+ Feasible Expand | `UtilityScorer.cs:54,154-160,264-271` | 新增 |
| 3 | **UtilityActionConfig** 默认预填 +⑩ Expand def；SO 资产 `UtilityActionConfig.asset` 追加 id:10（need=14/secondary），默认父级 SO 化 needA=6 | `UtilityActionConfig.cs:33` / `.asset` | 新增 |
| 4 | **TerritorySystem** +`NonInitialTerritoryCount` / `ExpandTick`(D326 升序/冷却/容量硬门/可走率) + 4 邻接候选 | `TerritorySystem.cs:133-247` | 新增 |
| 5 | **KingdomBrain** `ExecuteExpand` 焦点占位（实际=DayCycle 日 tick，裁3 A 日 tick 一致性） | `KingdomBrain.cs:294-304` | 新增 |
| 6 | **DayCycleSettlement** 步骤3 接线 `ExpandTick`（先于 CampUpgrader 步骤4） | `DayCycleSettlement.cs:40-43` | 新增 |

### 验收证据（实盘输出）

**编译**：`start_compilation_pipeline` → **0 error**（新增 0 warning；仅既有 Smoke_5/P0 两处预存 warning）。

**Smoke_12 ALL PASS**（Play 上下文实跑，P4/P5 为批B 新增探针）：
```
[2_17_12冒烟] P1 吞并真判定 有主→77==77 无主→-1==-1 =True | P2 缺口① ClaimInitial ...只纳无主负探针 =True | P3 DZ008 满员拦截立国=True 吞并不受上限=True | P4 批B ⑩ TerritoryGap A′评分+非初始占区计数 =True | P5 批B ⑩ ExpandTick 推进+冷却+只纳无主 =True
[2_17_12冒烟] ===== ALL PASS（P1真判定/P2缺圈入/P3 DZ-008/P4 TerritoryGap/P5 ExpandTick）=====
```

**P0 状态面基线**（同一 Play 上下文跑 Valley2_17_Smoke_P0）：
- A3 确定性逐字节 = OK（两纯轮逐字节一致；A3wood 二分首差=行-1 → 零新增分叉）
- A4 玩家零回归 = OK；RD2-①轮间清点 b=2684/2684/2684 一致（结构未变）
- B1/B2/B5 = FAIL，根因=自动化裸 Play 环境空单位/流浪汉池（u=0 非 u=22；HH.27 环境让渡项归人工 Play），**非批B 引入**（批B 改动为 ExpandTick/评分/def，不触 UnitRegistry/unit 生成）。
- 玩家侧基线未破 → 无需停手报裁。

---

## 二、诚实对账（锚点声明配套）

- **随裁修正已承诺顺序执行**：ClaimInitial 只纳无主修正 + P2 负探针（批B 首个 commit ffc6b92）先于评分池改动，符合 HH.33 §五 指定。
- **ExecuteExpand 设计说明**：按 HH.32 裁3「吞并/扩张=A 日 tick」，扩张引擎=TerritorySystem.ExpandTick（DayCycle 步骤3 每日跑，D326 升序部署所有 AI 王国）。ExecuteExpand 为焦点一致性占位（选⑩ 即确认意图），**不重复写扩张**——避免双写/S0 实体指令，与⑬⑭/None 姿态占位同规。
- **TerritoryGap 欲望与容量分离**：A′ 欲望（评分）× D327 容量硬门（ExpandTick 内 clamp(β+工人−非初始占区,0,max)≤0 停）——裁2 明确的「欲望与容量分离」双机制。

## 三、影响面

- 行为面：AI 王国达 Expand 阶段（minStage=Expand）后，⑩ 进入评分池；选中后随日 tick 向 4-邻接无主可走中区块推进（冷却5日/日推1~2块），写入账本 + 广播 → 2_10 染色/吞并判定可见。D327 容量门随工人数增长放宽扩张上限；非初始占区达 needA(6) 后欲望归零（⑩ 停选）。
- 玩家接触面 = 零（批B 不动玩家建造纳土/存读两路，均归批C）。
- 确定性：ExpandTick 按 kingdomId 升序 + 候选坐标序排序（D326/D343 同规）；NonInitialTerritoryCount 复用 CollectMidRing 同源。

## 四、处置建议

1. **请策划验收批B**：⑩ 推边界闭环（TerritoryGap A′ / ExpandTick / ExecuteExpand / DayCycle L40 接线）成立后置 Q1 批B 完工。
2. **放行批C**：玩家建造纳土（`ClaimAdjacentUnclaimed`，只纳无主 + 广播）+ ④债领土入档（TerritorySystem.SaveId="TerritorySystem" Global + EnterPlaying 门控：读档恢复/新游戏重推/旧档兜底）。批C 首动作=批B 边界内顺手落 ClaimAdjacentUnclaimed（批量裁4 三写入广播齐）。
3. 批C 之后步骤12 全量（批A/B/C）组侧 → P0 + Smoke_12 全绿 + 完整局回归。

---

> 状态建议回写：HH.34 待策划验收；队列 Q1 批B 完工（策划验收后置）；索引登记。

## 五、策划验收（2026-08-29 · 用户代表策划端）

**批B 验收=成立，放行批C。** 抽查记录：

- git 构成核对：8528188=8 文件 293+/ffc6b92=2 文件 34/17，与交付声明一致（产品6+Smoke_12+asset / 修正2 文件）。
- 代码实读：TerritoryGap 裁2 A′（clamp01((needA−非初始占区)/needA)）符合 HH.32 裁2；ExpandTick D326 升序+D327 容量硬门+4-邻接只纳无主；DayCycle L40 接线明确标注玩家纳土归批C。
- 冒烟行为级：Smoke_12 P1-P5 ALL PASS（P4 TerritoryGap A′ 评分、P5 ExpandTick 推进+冷却+只纳无主行为级合格）；P0 A3 逐字节/A4 零回归/b=2684 无新增分叉。
- B1/B2/B5 空单位/流浪汉池环境让渡如实标注，不伪造 PASS（HH.27 口径）。

### 随裁放行批C（抄 HH.35 §四 / HH.32 §六 裁4）

1. **玩家建造纳土 `ClaimAdjacentUnclaimed(kingdomId, coord)`**：建筑落成 → 该建筑脚下中区块的 4-邻接无主中区块自动纳入（D327）；**只纳无主**（裁4），他国领土上的玩家建造静默不动、不吞并（D283 防飞地）；广播 TerritoryChangedEvent（坐标序保确定性）——补全「三写入广播」最后一件（裁4 补遗）。
2. **④债领土入档**：`TerritorySystem` 实现 `ISaveable`（SaveId="TerritorySystem"，独立 Global 段，勿夹带 kingdoms[] 2_11 债——HH.32 补裁2）；`EnterPlaying` 门控三路：读档走存档恢复 / 新游戏 `RebuildInitial` 重推 / 旧档无段 → 兜底 `RebuildInitial`。
3. 批C 首动作=挂账④债（到期必落），ClaimAdjacentUnclaimed 接线 BuildController 落成点。

> 批C 玩家纳土属玩家侧接触面——验收标准含「玩家侧基线不破」；若破基线即停手报裁（HH.30 纪律）。