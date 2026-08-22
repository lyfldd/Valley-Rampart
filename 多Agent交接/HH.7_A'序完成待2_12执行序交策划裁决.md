# HH.7 A' 序 ③①② 落地完成 → 申请 2_12 正式执行序（交策划）

> 类型：进度同步 + 待决策
> 状态：✅已裁决（2026-08-22 策划端，见文末回写）
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
| 1. 2_12 正式执行序 | **A：按定稿实施计划「零、前置检查」逐项核验 → 步骤1 起步，附三条边界**。⑴**前置核验报告制**：8 项逐项打勾/缺口后报策划一眼再开工——其中 2 项（2_9 sim 对拍真值 / 伐木场引用清理）是历史薄弱位；⑵**A+ 坐标契约带入**：树/矿已数据化（features 索引），步骤1"从 naturalBuildings 实例化"调用面已缩水——按 HH.2/HH.7 现状适配，遇"文档说有实体、代码已是数据"接缝冲突记 HH 回策划，不擅自改设计稿；⑶主城可见产出串到步骤5 时带上守卫锚点 Play 验收（联动决策2） | 定稿计划本身即策划裁过的执行序；B"精简版"=当场重裁既定稿，违反"定稿后照走"分工 |
| 2. 守卫锚点 Play 验收时机 | **A：并入 2_12 小剧场（主城产出=守卫部署自然舞台），补一处前置探针例外**：若步骤1~4 推进中任何一处动 GuardDeploymentSystem 调用面（如建筑覆盖资源 TryConsumeResourceNode 接缝），该接缝点先跑 5 分钟 Play 探针验"守卫部署+LostEvent 触发"再继续——防锚点适配在未实机验证状态下被 2_12 改动二次扰动，两个未验状态叠加无法定位 | 单列探针=重复搭场景；但接缝扰动点需即时闭环 |

### 分歧裁决记录（有分歧时必填）
- 执行端意见：决策1 推荐 A；决策2 推荐 A（并入小剧场）。
- 策划端意见：两决策均选 A（给定选项已最优，无需自造），各附边界补强（核验报告制/A+契约冲突记HH/接缝探针例外）。
- 裁决：A+三边界 / A+探针例外 · 依据：设计稿定稿纪律（定稿后执行端照走，重构序须回策划）/ 验收纪律（薄弱位前置报备，未验状态不叠加）

### 衍生产物
- 新建设计文档：无
- 新建清单任务：守卫锚点 Play 验收（并入 2_12 小剧场，含接缝探针例外条款；随 2_12 步骤5 主城锚点一并串跑）