# HH.23 2_17 步骤2b 落地（AI段日结转账）· 报策划验收 / 放行步骤7

> 类型：进度同步
> 状态：✅已裁决（2026-08-26 策划端：2b 收口成立 + 步骤7 放行；附三项收口卫生指令）
> 日期：2026-08-26 · 发起端：执行端 · 关联清单/文档：2_17_AI王国脑与自主成长_实施计划（⑤-3 与 2b 落地追记）

## 一、做了什么（执行端填，带证据）

- **新增 `AIEconomySettlement`**（Systems/Kingdom/AIEconomySettlement.cs）：`Tick()` 遍历 KingdomRegistry 非玩家王国（kingdomId>0，IsPlayer 直接跳过=玩家零回归）→ `QueryKingdomBuildings` 收集本王国 IsActive 建筑 → **固定排序**（主键 coord.y→coord.x，次键 def.id String.CompareOrdinal，彻底弃注册序/FindObjects 序＝⑤-3 硬性 a）→ 按**五经济资源白名单**（Gold/Stone/Wood/Food/Metal；Ore/Crystal/FireOil/SpecialFood/Meat/弹药跳过保留）→ `kingdom.AddResources(pack)` 入国库 → `storage.Take(storedAmount)` 清零并触发 OnStorageChanged（IWarehouse 有 Take，修 CS0070 不可从类外 Invoke event）。
- **DayCycleSettlement 接入**：新增第 6 段调用 `AIEconomySettlement.Tick()`（交易额度冷却后、牧场/营地/CampUpgrader 前），不扰②残核一侧⑤步权威序。
- **15_差距账本登记**：`ai决策大脑强化训练/15_训练侧harness与Unity端差距文档.md` 新增「一·补二 AI 经济入账语义差异」——sim 瞬时入账无 Storage 中介 vs Unity 日结两段式，1 日滞后对账标注（步骤14 镜像 sim 公式时处理）。
- **冒烟 `Valley2_17_Smoke_2b.cs`**（Menu「Valley/验证/2_17_步骤2b_AI日结转账」，Play 上下文，SEED=20260826 两轮）**四项全绿 ===== ALL PASS =====**：`AI日结入账清零=OK`（kStone=125=锚定基线100+25，Storage 清零）/ `玩家零回归=OK` / `AI水井不产水=OK` / `确定性两轮逐字节一致=OK`（活世界方法债清偿）。
- **commit**：`d34b849`（2b 产品+冒烟+注释笔误修正，5 files +340）；`26e58c3`（chore(env) ProjectSettings runInBackground 0→1，随2b批次不混产品码）。
- **git-plan-sync**：开发计划书工作日志顶行插 2b 落地；2_17 实施计划追加「2b 落地(2026-08-26)」追记。

## 二、现状与阻塞

- 2b 收口静态+动态全绿，无阻塞。
- 解析过程中先因世界生成长度方差导致确定性 FAIL：已用「ClearAmbient 真正销毁非受控 AI 建筑（不只 Unregister）+ 锚定受控仓库基线 100」归一化测试输入，两轮一致（kStone=125）。

## 三、待决策事项

1. **确认 2b 收口成立**——据 d34b849 冒烟四项全绿，请策划确认 2_17 步骤2b 验收通过、15_账本登记已履约。影响：决定是否进入步骤7。
   - A（推荐）：验收通过，步骤7 开工（建造+招募 kingdomId 门面 = 指令通道最小集，方案见 2_17 计划正文）
   - B：验收通过但先补 X（再登记对齐 sim 缺点 / 新增某断言）

## 四、下一步建议

1. 新会话「恢复三连」：读开发计划书工作日志 → 读交接索引最新 HH → 本 HH。
2. 本 HH 已裁决 ✅：**三项卫生指令执行（补工作日志提交/边缘滚屏独立提交/训练仓 15_ commit）→ 2_17 步骤7 开工**（指令通道最小集：建造+招募加 kingdomId 门面，方案见 2_17 计划正文）。

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 2b 收口成立 + 步骤7 放行 | **✅ 成立 + 放行**（选 A） | 冒烟四项全绿含确定性债清偿（活世界两轮逐字节一致）；15_账本登记实查在案（L47-56「一·补二」，内容质量高：粒度/滞后双行+对账时点标注+步骤14 王国脑预算偏差预警）；产品码上轮静态验收吻合⑤-3 全部硬性。**三项收口卫生指令**（见下）完成后步骤7 开工 |

### 收口卫生指令（放行附带，先清再进步骤7）

1. **补提交工作日志**：d88ed37f 提交说明写「工作日志」但实际只含 HH.23/交接索引/2_17计划三文件——工作日志 d34b849 行仍在工作区未提交（实测核对）。补一笔 docs 提交收口。
2. **边缘滚屏线独立提交**：工作区 CameraConfig.cs/CameraRig.cs（enableEdgeScroll 默认关）+ _目录/2_10 文档追记=完整独立小改动（来源用户实测反馈，代码+文档齐），**单独一笔**提交（产品+文档一起，勿混入 2_17 线），commit message 注明 2_10 体验优化。
3. **训练仓 15_账本 commit**：15_文档 M 状态未提交——sim-sync 纪律登记落 commit 才算数（防丢失）。只 add 该文件单独提交；训练仓其余工作区痕迹（results 清理等）不属本卡，勿混。

### 分歧裁决记录
- 无分歧。执行端 A 建议采纳，卫生项为策划端核验新增（d88ed37f 提交说明与文件构成不符/双线未提交/训练仓未 commit），执行端未报属疏漏非分歧。