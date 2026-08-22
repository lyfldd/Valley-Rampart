# HH.7 A' 序 ③①② 落地完成 → 申请 2_12 正式执行序（交策划）

> 类型：进度同步 + 待决策
> 状态：⏳待裁决
> 日期：2026-08-22 · 发起端：执行端 · 关联清单/文档：HH.6（A'序 + 裁决一/二）/ 2_12 设计稿+实施计划（已定稿 2026-08-14）/ 2_13

## 一、做了什么（执行端填，带证据）

### ③ 遗留改动甄别 + 清树基线（HH.6 A'序：③提前至最前）
- 工作区 8M+2U 逐一核对，分四类处置：
  - **丢弃（stale/误触/编辑器自动）**：`2_10_渲染与摄像机.md`（误触回退 iso 措辞）、`1_2D`（纯空白）、`2_9`（纯空白）、`ProjectSettings.asset`（runInBackground 0→1 + WindowsStandalone 平台段，Unity 编辑器自动写入）
  - **保留（新裁决产物）**：`2_8`/`2_10实施`/`doc1_2D实施` 三处裁决注释 + `_交接索引.md`(HH.6行) + `HH.6` 文件 + `.gitignore`(.codely/ 入忽略)
- 提交 `a46a735`：清树基线（含 mun 内部「待重建单位铺放」环境结论）

### ① 守卫锚点适配三件套（HH.3 §六 + HH.6 裁决二）
件1改查 features 数据索引、件2 LostEvent 语义重定义、件3 重验范围收窄，全部落地（commit `bcbe81c`）：
- `GuardDeploymentSystem`：锚点从 Building 实体改查 `WorldManager.ActiveMap.features`（Tree/Mine/OreVein）；新增 `GuardResourceNode` 数据句柄；`DeployGuardAt` 双重载（句柄 + OreVein 实体兼容）；`RemoveGuardRegion` 三路（int/GridCoord/Building）；新增 `HandleResourceConsumed`/`IsGuardResourceFeature`/`FeatureDisplayName`
- **GuardRegionLostEvent 语义重定义**：载荷弃 `Building` 改 `GuardResourceNode`；触发判定 = (a)守卫被击退/撤离（RemoveGuardRegion 显式）+ (b)资源点 feature 被建筑覆盖（`WorldManager.TryConsumeResourceNode` → `HandleResourceConsumed`）。**HH.2 残留债未蒸发**
- `ToastManager.ResolveRegionName` 改读数据句柄
- 重验范围收窄声明：本卡只改动锚点查找 + 部署位置，不重验守卫迎战行为全链（落A 已闭合验收 HH.5，不重开）
- 编译 0 error；反射验证 GuardResourceNode/GuardRegionLostEvent OK + GuardDeploymentSystem 全部预期方法齐备

### ② VisionConfig 建档（HH.5 裁决⑥）
- 新建 `Resources/Config/VisionConfig.asset`（enable=true / maxExploredCells=16384），`Resources.Load<VisionConfig>("Config/VisionConfig")` 加载验证通过
- 四链信标各 grep 复跑（迷雾启用=真实耦合变化）：确认 `MarkExplored` 上报半径 = 感知半径 → `UpdatePerception` 查出的 `nearbyEnemies` 必在已探索圆内 → `IsExplored` 恒 true，**迷雾启用不拦截索敌/迎战**，四链（①采集/②交锋/⑥探雾/K7撤退）信标数据流无回归

### 提交记录
- `a46a735` docs：③清树基线
- `bcbe81c` code：①守卫锚点三件套 + ②VisionConfig 建档

## 二、现状与阻塞
- 无硬阻塞；①②③全部编译 0 error、已提交，工作树干净。
- 守卫锚点 Play 实机验收（部署守卫@OreVein + 消耗触发 LostEvent）与 ② 揭雾实跑尚未进 Play 模式验证（当前为源码 + 反射级验证）——需进 Play 跑小剧场，见下一步建议。

## 三、待决策（每项：选项 + 推荐 + 影响）
1. **2_12 正式执行序**——决定接下来从 2_12 哪个步骤开工（2_12 设计稿 + 实施计划已定稿 2026-08-14，HH.6 裁决一已定 2_12 先于 2_13）。
   - A（推荐）：按 2_12 实施计划「零、前置检查」逐项核验 → 满足后按步骤 1（建筑坐标/footprint 2D 化核心接缝）起步。前置检查项（2_2/2_1/2_5/2_8/2_9/doc1）多数已落地，由执行端先行核验并报告缺口。
   - B：策划先据当前进度裁一个"精简版执行序"（只挑高价值闭环步骤，先串主城可见产出），再交执行端。
   - C：策划另定。
2. **守卫锚点 Play 实机验收时机**——①的 LostEvent 消耗触发语义已重定义并落地，是否随 2_12 小剧场（部署守卫@OreVein + 消耗 OreVein 验证 LostEvent 告警）一起实测，还是单列快速 Play 探针先行。
   - A（推荐）：并入 2_12 小剧场验收，一次串跑（守卫锚点 = 主城产出前置的自然验收点）。
   - B：先单独 Play 探针验证守卫部署+LostEvent，独立于 2_12。

## 四、下一步建议
- 策划端裁决 2_12 执行序后，执行端按序开工；2_12 与 2_13 执行序在 HH.6 已定（2_12 先）。
- 恢复入口：本 HH + `河谷防线_开发计划书.md` 顶部工作日志（08-22 行）+ `_交接索引.md`。

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|


### 分歧裁决记录（有分歧时必填）
- 执行端意见：.. · 策划端意见：..
- 裁决：.. · 依据：..

### 衍生产物
- 新建设计文档：无
- 新建清单任务：{由策划端按裁决写入（如 2_12 执行子清单 / 守卫锚点 Play 验收卡）}