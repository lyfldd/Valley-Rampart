# HH.27 P0 完整局验收批执行报告（确定性状态机验收 · 待策划裁真值深挖方向）

> 类型：P0 收官验收批执行报告
> 状态：⏳已执行一轮，多判据 FAIL；初步归因=纯 pump 经济断链 + harness 存读残留（未确认），**待策划裁决深挖/让渡口径**
> 日期：2026-08-27 · 发起端：执行端 · 关联：HH.26（P0 验收清单）/ HH.25 / HH.24 / 2_17_AI王国脑与自主成长.md
> 前置：HH.26 三条裁决全准，批准跑完整局批次

---

## 〇、一句话

**按 HH.27 执行端提交 → 策划三条裁定（①②③）全部落实，完成「确定性状态机验收」纯逻辑 pump 单套件 `Valley2_17_Smoke_P0` 并对骨架跑出一轮。A4 玩家零回归=OK；A3/B1/B3+C6/B4/B5 首轮 FAIL。归因初步指向「纯 pump 经济产出闭环断链(hh.27①让渡项) + harness 存读残留」，是否产品缺陷未甄定。待策划裁决后续深挖方向。**

---

## 一、策划三条裁定 → 落地对照

| 裁定 | 要求 | 落地 |
|------|------|------|
| ① 口径合法性 | 交付命名「确定性状态机验收」（pump 推日、断言状态时间线），非"妥协版完整局"；真实走位归人工 Play 职责归位 | 套件命名 `确定性状态机验收`，纯逻辑 pump（反射 `TimeManager.AdvanceTime` 走完整事件链），状态快照链断言时间线；NavMesh 走位/逐帧表现声明归人工 Play（文件头职责归位声明） |
| ① 唯一实质让渡 | NavMesh 走位驱动的经济闭环（工人走到建筑才产出）——pump 下拿不到收入 | 报告显式登记该让渡（见 §四），不伪造收入增长；B2 供水=农场入账>0 属间接证据且依赖逐帧产出→一并登记让渡 |
| ② 灾变域准予+登记 | WaveDirector/PortalDisasterTrigger/ThroneAnchor 三禁为验收环境构造；登记"玩家死亡/GameOver 链路本批未验" | `DisarmDisasters()` 三禁置 enabled=false（防玩家死→时钟冻结污染 A 判据）；**登记：ThroneAnchor 被禁=玩家死亡/GameOver 路径本批次未覆盖，留独立回归** |
| ③ pump 实现约束 | 反射 `TimeManager.AdvanceTime` 走 `AdvanceDay→TimeDayChangedEvent→DayCycleSettlement` 完整事件链；**禁直接调 `OnDayChanged`**；GameState 必须 Playing；SetSecondsPerDay 走公开 L296 | `ReflectAdvance` 反射 `AdvanceTime(TEST_SPD)`（内部 while→`AdvanceDay`→事件链，真链）；未调 `OnDayChanged`；pump 内 `GameStateManager.SetState(Playing)` 强推保证推进期 Playing；`SetSecondsPerDay(TEST_SPD=60)` 公开 API；`Time.timeScale=0` 冻结 Update 自推，`AdvanceTime` 成唯一推手（确定性） |

---

## 二、执行过程中的两连卡顿（诚实归因，非产品缺陷）

1. **首次时间不动**：查明根因 = 玩家被密度放大的夜袭打死 → GameOver → 全局时钟冻结（`TimeManager.Update` 仅 Playing 推进）。非死循环。→ harness 加 `DisarmDisasters()` 三禁修复（验收环境构造，横向策划准予）。
2. **编辑器无响应(二次)**：根因 = **方案失当（执行端认领）**——首版 harness 走"真实流"（压缩日历 15s/天×3 倍速 + 全场景单位 AI 逐帧仿真），中等地图上单位累积把笔记本 CPU 打满，主线程被占 → 窗口卡死、MCP ping 不应答。→ 按策划②裁**用纯逻辑 pump 收敛**（反射推日、秒级完成、零逐帧负载），彻底消除卡顿源。

**交付因此从「活世界帧仿真」收敛为「确定性状态机验收」**——此恰好是 A3(同 seed 逐字节) 的唯一可严格证明之法（HH.27① 裁明）。

---

## 三、套件设计与真相

- 单套件 `Assets/Editor/Smoke/Valley2_17_Smoke_P0.cs`（菜单 `Valley/验证/2_17_P0_完整局验收`）。
- 三轮 pump：两纯轮(A3) + 一存读轮(B3+C6)。SEED 固定，Difficulty=2，Medium 世界，CAP_DAYS=45，SAVE_DAY=25。
- 每轮 pump 首部对齐「回主菜单→新开一局」真实复位语义：`KingdomRegistry.ResetState()`（玩家占位/nextId）+ `TimeManager.ResetState()`（回 day1）+ `WorldManager.ResetState()` + `KingdomBrain.ResetDispatchStats()`——**必须在 `InitializeNewGame` 之前**（否则新局注册 foundedDay 读到上轮累积 CurrentDay，已修）。Day 归置已验：三轮开局均 Day=1, KCount=4。

---

## 四、首轮判据结果（抽真证据）

爆款，随后引用。最终 LLM 输出是唯一可信汇总（五次实测复现）。

```
A3确定性逐字节=FAIL      ← 两纯轮快照链不一致
A4玩家零回归=OK
B1正向招募并行=FAIL(pFalse/False k1t0/0 k2t0/0)   ← 玩家招募失败 + AI 招工人 0
B3+C6存读回环含脑态=FAIL  ← 存读残存（SpwanFromSave 冲突报错已现）
B4剧本三段封顶=FAIL(R1-+无军事  R2E+无军事)         ← R1 停 Develop，R2 到 Expand（分歧）
B5派遣双证分列=FAIL(K1 build10 train0)            ← 有建造落地、无招工落地
```

**时间线**：R1=全 `D`（Develop 到底）；R2=`SDDDEEE...`（存活→发育→扩张）。

---

## 五、初步归因（未甄定，待策划裁决）

**关键证据**：
- `B5 train0` + `R1 停 Develop` 同时出现：**K1 全程只建造(build10)未招工人(train0)**，工人数不足以触发 Develop→Expand。
- **R1 全 D vs R2 到 E = 同 seed 新局却有成长分歧**——这是最需要深挖的疑点。
- 存读轮报 **`BuildingFactory.SpawnFromSave 冲突`**（路径A 新随机 GUID+默认 kingdomId vs 存档侧）——B3 存读/复合腐坏**疑似产品读档重建双路径 bug**（属 2_2 读档路径，非 2_17）。

**候选解释（需甄别）**：
1. **纯 pump 经济断链（HH.27①让渡项，预期）**：ProducerComponent 产出依赖 TaskScheduler 逐帧派工/走位到达，pump 无帧不产 → AI 无收入 → 招工人/建产能缺资源 → 停在 Develop。B5 train0 与之吻合。
2. **harness 存读残留**：三轮共用 slot 的 Save/Delete、BuildingRegistry/UnitRegistry 跨轮残留可能污染 A3。
3. **产品 AI 不确定性（真疑点）**：R1 vs R2 同 seed 成长分歧，若剔除 harness 因素仍现，则指向王国脑/Foundry 初始状态残留。

**执行端当前判断**：`B5 train0`、`停 Develop` 大概率属解释1(纯 pump 断链)；A3 分歧可能含解释2(harness)；但 R1/R2 同在纯 pump 下分歧，不排除解释3。**需深入甄别后才能定性，不武断归因产品。**

---

## 六、请策划裁决（裁后执行端深挖/收敛）

1. **接受「让渡让这些判据在 pump 下为 N/A」**（解释1成立=纯 pump 环境限制，A1/A2/B2 + 经济产出相关 B5/B4 归人工 Play）——是否接受？若否，是否要求 AI 工人在 pump 下不依赖走位的产出补偿（改动 harness 模拟产出）？
2. **是否批准深挖「R1/R2 同 seed 分歧 + SpawnFromSave 冲突」**（甄别解释2 harness vs 解释3 产品 AI/读档 bug）？深挖会再消耗笔记本编辑器资源（纯 pump 已无卡顿，风险有限）。
3. **B1 玩家招募(pFalse)否要核查根因**：Harness `RecruitVagrant` 在纯 pump 下可能因无真实流浪汉/粮异常而返回 false——是否归"人工 Play"还是需 pump 补递归活流浪汉证其通道？

**不影响 P0 收官的项**：步骤9 冒烟三条（#19/#4/#3）此前已 ALL PASS；王国脑决策核/评分器逻辑非 pump 依赖，A4 玩家零回归已 OK。

---

## 七、交接状态

- **执行端产物已就绪**：`Valley2_17_Smoke_P0` 套件（含策划①②③全部约束落地 + 两卡顿根因修复）。
- **未提交**：本批套件未 commit（待策划裁决方向后再提交，避免带未甄定 FAIL 入库）。
- **文档**：本 HH.27 为中间汇报，正式收口待策划裁决后补全。
- **④债**：维持不变（归步骤12/领土入档先到者，触发必落 foundKingdoms 门控+三处追记回写）。