# HH.18 阶段④核心收口完成与待办登记

> 类型：进度同步
> 状态：✅已处理（无待裁决，用户已拍板）
> 日期：2026-08-23 · 发起端：执行端 · 关联清单/文档：2_12 步骤8.4 / 2_14 步骤14 / HH.17

## 一、做了什么（带证据）

**阶段④核心收口全部落地并推送成功**：

| commit | 内容 |
|--------|------|
| `67d3b671` | 阶段④ God-view 君主实体退役（HH.17 裁决 A/决策3修正：去守卫·人口去君主10→9·删血量条）+ HUD 王国面板 |
| `26a94b0` | AI 王国子系列 2_15~2_19 设计启动落档（独立成 commit，未混入本批）+ 设计方法论_生命周期工作流定稿 + AGENTS.md |
| `70b9e6b` | 本批 git-plan-sync 收口：2_14 存档迁移待办登记行（HH.17 决策4 遗留）+ 工作计划书工作日志顶行更新 |

> 推送实盘：`67d3b671..70b9e6b4  main -> main`，main 与 origin/main 已同步，工作区 clean。

**用户最后拍板两项（已处置）**：

1. **monarch 旧档过滤 → 留 2_14**：同意。未提前碰 SaveManager（避开 UnitFactory.GetUnitData(Faction, Occupation.Ruler) 退役后返回 null 报警的时点问题）；已在 `2_14_敌怪与传送门灾害_实施计划.md` 步骤14 存档迁移段**登记待办行**（HH.17 决策4 遗留：旧档 occupation=Ruler 单位过滤，随 2_14 一并处理）。留后但有登记，2_14 开工不至于遗忘。
2. **GUI 存档→读档 Play 铁闭环 → 不接受构造式替代，随阶段④ Play 补跑**：接受。验收清单 = 存档→读档→国库 6 资源恢复相等（含铁）+ 人口 9 实体 + 无多余单位。Play 环境当前不可靠→已记 bug 待查，不静默降级为构造式。

## 二、现状与阻塞（Play 铁闭环待查，已记 bug 不私下绕过）

- **Play 环境停在开局前现场**：直进 Play 时 `totalUnits=2 / pop=0 / TreasureVault=NOT_YET`。
- **根因线索（非代码 bug）**：开局依赖场景流 `MainMenu→GameSceneEntrance.SetNewGame→GameBootstrap.StartNewGame`，harness 直接进 Play 未驱动 New Game，故装配层铁闭环未展开。
- **处置**：记录为待查 bug（"国库就绪时序"），不自行为绕过验证而降级为构造式。自动验需脚本驱动 `SetNewGame` + 场景切换。

### 2026-08-23 补跑收口：✅ 三件套全 PASS（脚本驱动真实场景流，零代码改动）

执行端按§四建议完成补跑验证：

1. **驱动链（等价 GUI 路径）**：MainMenuScene 进 Play → `MainMenuController.OnCharacterCreateConfirmed(config)`（slot_test / seed=20260823 / 普通）→ SetNewGame → LoadScene(GameScene) → GameBootstrap.StartNewGame 全链。
2. **开局现场**：gameState=Playing、TreasureVault READY（BaseCapacity=250）、popCount=9（4工人+5居民）、ruler 单位=0；场景预置调试单位 `ruler`/`VFriendly`（Data=null）2 个残留属预期（R2 规则：预置单位仅读档路径经 TeardownScene 清理，新建路径不清）。
3. **存档→读档往返（含防 no-op 扰动）**：`ModifyResource` 官方写路径扰动 R0→R1（石133/木100/粮130/铁7/金105）→ `SaveManager.Save(slot_test)`（2755 模块；磁盘 JSON 交叉核验：KingdomManager `treasuryMetal=7` 含铁✅、RulerController 非金写零✅）→ 内存再扰动 R2（石123/金155）制造内存≠磁盘分叉 → `TeardownForReturnToMenu(false)` → 主菜单 `OnSaveSlotSelected(slot_test)` → ContinueFromSave（TeardownScene+LoadSave+BindExistingMonarch）→ **读档后验收**：国库 6 资源含铁==R1 逐项相等（Stone=133/Wood=100/Food=130/SFood=0/Meat=0/Metal=7）、Gold=105（非 R2 的 155）、unit total=9（worker=4/resident=5/ruler=0/noData=0/预置单位已清）、popCount=9、rulerName 恢复。
4. **结论**：三件套全 PASS，铁闭环无代码 bug；§2"国库就绪时序"确认为 harness 未驱动 NewGame 场景流的症状，根因闭合，待查撤销。全程 Console 0 error/0 exception（仅 1 条既有无关 PanelSettings 主题告警）。测试档 `slot_test.json` 已删除，编辑器场景已复位。

## 三、待决策事项

无。本批两个决策（monarch 留 2_14 / Play 补跑）已由用户拍板并落地。

## 四、下一步建议（执行端下次恢复入口）

- ~~**Play 铁闭环补跑**~~ ✅ 2026-08-23 完成，三件套全 PASS（见§2 补跑收口节）。
- **sim 侧 IWarehouse 同步待办**（HH.15，登记不代做）：训练仓会话落盘 harness/Core + 双门禁 + 台账，交接训练仓。
- **后续指令开门判据**：用户预告下次大概率是 步骤9/10/11（市场贸易/科技/箱子溢出）或 2_13——届时按 HH 开门判据自行判断，无需事事请示。

---

## 策划裁决（无待决策，留空）

本 HH 为纯进度同步 + 待办登记，无需策划裁决。阶段④核心收口已推送，下一步事项已登记，恢复路径见§四。