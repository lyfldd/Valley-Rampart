# HH.183｜2_23 资源 P0 批C · C′ 补笔 完成报告（D644 裁 A 收敛版）

> 执行端（Unity 轨）· 2026-09-11
> 状态：⏳ 待策划端验收（批 commit 已落盘，**未 push**）
> 锚点：**D644（0.6 §一百七十三，2026-09-11）**＝HH.181 三条勘正全成立＋**裁 A（C′ 收敛版）**＋姊妹项立 **DZ-089** 挂账；上游 D643（批C 销号）／HH.181 §六（裁决规格）／HH.182（开工回执）
> 编号：**HH.183**（并行会话已在 HH.184 行备注「HH.183 为批C 预计完成报告」＝vr-id-ledger 防撞号预留；水位线维持 HH.184）

## 一、三件交付（按裁 A 逐条兑现）

### R-C′1｜真产能守卫（本串**唯一代码改动**）

[MapProduceToEco](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L492-L505)（KingdomBrain.cs:492-505）首行守卫：

```csharp
if (def == null || def.producer.kind != ProduceKind.Resource
    || def.producer.rate <= 0f || def.isResourceNode) return -1;   // C′：真产能守卫（D644）
```

- 口径**逐字对齐** [BuildingFactory.cs:295](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs#L295)（`rate > 0f && kind == Resource && !isResourceNode`）；**其余一字未动**。
- 效果：诊断侧只计**真产能建筑** ⇒ 解 **⑤通道A**（`Count(Production[r])==0`）与 **③R-C2**（`ResolveTriageDefId` 按 `Count/pop` 挑最缺）被仓储（Granary→Food／Warehouse→Wood）与非产能建筑（~25 个 `outputResource` 默认 0=Gold、wood_pile/stone_pile）**掩蔽**。
- **零行为漂移**：工厂侧本就未给 `rate=0` / `isResourceNode` 建筑挂 `ProducerComponent`（实际产出未变），本改仅收口**诊断口径**漂移。

### R-C′2｜探针（[Smoke_2_23RP0.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Smoke_2_23RP0.cs) ＋P6a~P6e 五项）

| 探针 | 内容 | 实测 |
|------|------|------|
| P6a | C′ 反射面在场（MapProduceToEco/BuildEconomyBlock/CountProductionOf/ExecuteBuildFocus） | True ✅ |
| P6b | **负例①真产能仍计**：farm→Food=3／quarry→Stone=1／Blacksmith→Metal=4 | ✅ |
| P6c | **负例②rate=0 不再计**：Granary/warehouse=-1、wood_pile/stone_pile=-1、castle/House/market（Gold-默认族抽样）=-1 | ✅ |
| P6d | **负例③资源点(isResourceNode)不再计**：tree/farmland/mine/ore_vein 全 -1 | ✅ |
| **P6e** | **正例（真实管线端到端）**：清场→**真实建 1 座 Granary**→反射调 `BuildEconomyBlock` 取真值 → `CountProductionOf(eco,Food)`=**0** → 反射调 `DecideTriage`=**BuildCapacity** | ✅ |

- 正例走**真实诊断管线**（非手工拼 EconomyBlock），证据链最硬：**有 Granary 无 Farm 时 Food 产能仍为 0 ⇒ 通道A 仍选建 farm**（＝Farm 被摧毁后 wartime 恢复路径已通）。
- 注记：`BuildEconomyBlock` 内含 `TakeFlow`（读后清零＝生产同款语义），探针窗口内一次性消费；双跑同点位执行 ⇒ 确定性不受影响。
- 探针走**正门** `TestHarnessApi.EnterTestRun` / 收尾 `ExitTestRun`（L-17）。

### R-C′3｜契约同步（[HH.180](file:///c:/Users/trs/Desktop/Valley%20Rampart/多Agent交接/执行端/HH.180_2_23资源P0批C完成报告.md) §六）

`EconomyInput` 的 `Production` 计数口径改为 **`rate>0 && kind==Resource && !isResourceNode`**，并注明「**真产能建筑**按资源聚合计数；仓储类／资源点／非产能建筑均不计」＋「本口径为 C′ 补笔落盘后的**正式对拍口径**，训练仓 sim 镜像请以此为准（收窄前口径已作废）」。

## 二、门禁证据（全过）

| 门禁 | 实测 |
|------|------|
| 编译 | `start_compilation_pipeline` → **errors=0** / warnings=21（**全为既有基线**：IUIPanel/ToastManager/ChestManager/BuildingMenuPanel/…；KingdomBrain.cs 与 Smoke_2_23RP0.cs **零新增警**） |
| `Smoke_2_23RP0` run1 | **PASS 27 / FAIL 0**（`Logs/P1/smoke_2_23rp0_run1.log`，2440B，13:32:37） |
| `Smoke_2_23RP0` run2 | **PASS 27 / FAIL 0**（`smoke_2_23rp0_run2.log`，2440B，13:34:26）＝归一化（tag/时间戳剔除）**逐行一致＝确定性** |
| `Smoke_2_22P0` run1（既有基线零退化） | **PASS 32 / FAIL 0**（`smoke_2_22p0_run1.log`，**2422B＝与基线同字节**，13:51:02）；P8c 选格(424,932)/Score=0.3452、P9f MachineCount=2 等**逐位一致** |
| `AI.Core` git status | **空** ✅ |
| 死表 | `TaskPriorityConfig` / `TaskScheduler.cs` **不在变更面** ✅ |
| 变更面 | **2 文件**：Smoke_2_23RP0.cs +75 ／ KingdomBrain.cs +10/−2（**资产零改动**） |
| 取号 | D640 #10 原子化：HH.182 独立 commit `0b689bc`；HH.183 独立 commit `9e5a168` |

L-22 纪律：run1/run2/2_22P0 三段之间均 `stop → play` 完整退进（非容器内重建）；收尾 `stop` 退 Play（最终 playMode=stopped）。

## 三、探针实录（run1 全文，与 run2 仅 tag/时间戳差异）

```
[PASS] P1a 正门进局 seed=21140 tag=run1
[PASS] P1b 分诊反射面在场（resolve/decide/grain/brain）=True
[PASS] P2a 粮触发（GrainReserveDays=1 < 2）→ 3(粮)=3
[PASS] P2b 金触发（Net<0 且水位<基线）→ 0(金)=0
[PASS] P2c 木触发（金不触时取严重度序内木）→ 2(木)=2
[PASS] P2d 铁不参与触发（metalOut=30 净负+存量低 → 仍 -1）=-1
[PASS] P2e 全正常不触发 → -1（⑤ 正常态不为屯粮空转）=-1
[PASS] P3a 通道A 正（无粮产能→建 farm）=BuildCapacity
[PASS] P3b 通道B 正（有产能但日产出/人口低→NoOp=偏向生效）=NoOp
[PASS] P3c 通道C 正（占用溢出→粮建 Granary）=BuildGranary
[PASS] P3d 通道C 非粮（占用溢出→Stone 建 Warehouse）=BuildWarehouse
[PASS] P3e 固定序（A 态+B 态同场→A 优先建 farm）=BuildCapacity
[PASS] P3f 确定性（DecideTriage 同输入复算全等）=True
[PASS] P4a NeedScore 反射面在场=True
[PASS] P4b 断链注入 Farm→④强度采集 need=1（==1.0 拉满）
[PASS] P4c 断链注入 Warehouse→②建仓 need=1（==1.0 拉满）
[PASS] P4d 断链注入 Fort→⑨修工事 need=1（==1.0 拉满）
[PASS] P4e 断链注入负探针（招工行动不受缺链路——need<1）=0.4
[PASS] P5-pre 清场后 food 产能（farm+Granary）=0（探针前置：清 0 才入通道A 态）
[DBG] farmDef=True CanAfford=Y timeScale=0.5 radius=48
[PASS] P5a E-B1 分诊通道A 建 farm（0→1，focus=BoostHarvest）
[DBG-P5b] 各型计数 farm=0 gran=0 wh=1
[PASS] P5b 正常态 no-op（无触发→⑤ 不动工，同帧比数 1→1）
[PASS] P6a C′ 反射面在场（MapProduceToEco/BuildEconomyBlock/CountProductionOf/ExecuteBuildFocus）=True
[PASS] P6b 负例 真产能仍计（farm→Food=3／quarry→Stone=1／Blacksmith→Metal=4）
[PASS] P6c 负例 rate=0 建筑不再计产能（Granary=-1 Warehouse=-1 wood_pile=-1 stone_pile=-1 castle=-1 House=-1 market=-1 ）
[PASS] P6d 负例 资源点(isResourceNode)不再计产能（tree=-1 farmland=-1 mine=-1 ore_vein=-1 ）
[列报] Well 映射=0（rate=4 越守卫；outputResource 默认 0=Gold＝同族残留，供 DZ-089 家族处置）
[PASS] P6e 正例 有 Granary(1)无 Farm → 诊断 Food 产能=0 → 通道A 决策=BuildCapacity
[PASS] P1d 恢复：SituationHub 交回日 tick（Remove 已清注入）
===== Smoke_2_23RP0 资源P0批C冒烟收工 =====
PASS=27 FAIL=0 时间=13:32:37
```

## 四、列报（待裁/知悉 2 项）

1. **Well 越守卫仍计 Gold＝同族残留（观测，未擅动）**：`Well.asset` `rate: 4`（>0）＋`outputResource: 0`（＝Gold 默认值）＋`isResourceNode: 0` ⇒ **通过本守卫**，仍被计为 Gold 产能（探针实录 `[列报] Well 映射=0`）。旁证：`AbstractEconomySettlement.cs:197-198` **显式 skip Well**（「AI 井恒不产水，不入国库」），而 `MapProduceToEco` 无此排除 ⇒ **又一处口径漂移**（同族：默认值/缺排除）。建议**并入 DZ-089 家族**一并裁（本轮未改＝守「只加守卫、零漂移」）。
2. **姊妹项知悉**：`AbstractEconomySettlement.cs:197` 同缺 `rate>0` 守卫（离屏 Abstract 经济侧）⇒ 已按裁 **DZ-089 挂账 + 独立裁决**，本批**未动**。

## 五、红线自检

- ✅ **AI.Core 零直改**（git status 空）
- ✅ **资产零改动**（`ProduceKind.None` / 资产 `kind` 显式声明均按裁**未做**）
- ✅ **离屏 `AbstractEconomySettlement` 零改动**（DZ-089 挂账）
- ✅ 死表 `TaskPriorityConfig` / `TaskScheduler.cs` 零动
- ✅ 常设底线三级序（粮→人口→被攻）不动
- ✅ 确定性（同 seed 双跑归一化逐行一致）／so-data-driven（判据参数仍全 SO）
- ✅ 训练仓不代提（15_账本 S-B1 等属训练仓）
- 排雷兑现：**L-29**（检索必做阳性对照 / 优先 `Grep` 工具——本轮全库审计用 Grep 40/40 命中）／**L-28**（分类字段默认值＝有效值：查明真实路径为「有 `producer` 段但 `rate=0`」）／**L-01**（加守卫后验消费端：P6e 真实管线正例）／**L-02**（落盘即验：git diff/编译/探针三验）／**L-12**（单点串行）／**L-17**（探针走正门）／**L-24·L-25**（根因/字段带 `file:line` 直读）

## 六、回写与提交

- 取号：HH.182（`0b689bc`）／HH.183 登记（`9e5a168`）——均独立单行 commit（D640 #10）
- 本报告 + 索引 HH.183 行 + 队列批C 行 + 主计划书工作日志插行（随本串）
- 交付 commit：`KingdomBrain.cs` / `Smoke_2_23RP0.cs` / `HH.182 开工回执` / `HH.183 本报告` / `HH.180 §六 契约同步` / 四回写文件；**只提本串**、禁 `git add -A`、**未 push**

## 七、下一步（C′ 落盘 → 收官）

策划端验收 HH.183 ⇒ **「2_23 资源 P0」收官** ⇒ **P1 七考放行**（D644：「C′ 落盘 → 验收 → 收官 → 七考放行」）。