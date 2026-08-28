# HH.1 ③守卫警报MVP-A闭环 + 2_10开跑/步骤2投影核心/三拍板落档

> 类型：进度同步
> 状态：✅已处理（无待裁决）
> 日期：2026-08-21 · 发起端：执行端 · 关联清单/文档：2_8 / 2_10 / 2_13

## 一、做了什么（执行端填，带证据）

- **③守卫警报 MVP-A**（Q3 终稿落地）：新增 `Systems/UI/ToastManager.cs`——通用 toast（UI/ 通用层，标注「2_13 可复用」）+ 订阅 `GuardRegionLostEvent` 弹「守卫告警:X区受袭」。编译 0 错误、类型已在 Assembly-CSharp。**方向字段后做未现编**（挂 2_13 待办）。`fd51285`
- **2_10 开跑·前置就绪核对**：doc1 正交坐标 `GridSystem.WorldToCoord/CoordToWorld` / 2_1 `MapData`(WorldState.cs) / 2_2 `BuildingVisual.ApplyPlaceholder` / 2_3 `Facing8`+2D移动链(`UnitController`+`PathFollower`+`AStarSolver`) 全真实落地→打勾，状态改「实施中」。`45f19eb`
- **2_10 步骤2 投影核心**：新增 `Systems/Rendering/MapRenderService.cs`——`GridToIso`/`IsoToCell`/`IsoDepth` 纯等轴2:1菱形投影（读 `GridSystem.Config.cellSize`，空网格回退 (1.28,0.64)），铁律只渲染不反哺逻辑坐标。Editor 反射 5 例 round-trip 全 PASS、编译 0 错误。`4f60376`（+ .meta `41655d7`）
- **2_10 三开口策划拍板(D19) 落档**：①先串小剧场再铺主城可见产出（主城王座/旗帜归 2_12）②几何菱形片占位、不给基调图（占位→正式=步骤10 纯资产替换）③摄像机 1× 横向约24格/纵向13行，初始镜头=主城锚点（`FocusOn` 默认=王座/旗帜锚点 2_12 契约，小剧场期用 `WorldManager.GetKingdomAnchorWorld` 代），clamp margin 视域半高+1格起调。`3fa3d39`
- **小剧场串联验收清单（钉死）**：K7「受袭→撤退→回位」+ ①采集搬运 + ④守卫迎战 + ⑥巡逻推进，一次清完 2_8 尾。`3fa3d39`
- 每次提交后已同步 `河谷防线_开发计划书.md` 工作日志（最新 `9084f36`）。

## 二、现状与阻塞

- **渲染层尚未落地**：2_10 步骤1（5 层 Tilemap 结构）/ 步骤2（`RenderMap`/`UpdateCell` 铺格）/ 步骤3（`CameraRig`）都还没做——`MapRenderService` 只有投影核心，`RenderMap`/`UpdateCell`/`ScreenToGrid` 是待补接口。
- **阻塞点＝需要 Unity 编辑器场景实操**：等轴投影首次与现实画面对齐、5 层 Tilemap 结构创建、摄像机与拾取都只能在编辑器里实测标定，非纯代码可先行。当前无待裁决设计决策。

## 三、待决策事项（每项必须：选项 + 推荐 + 影响）

- 无待裁决事项。三拍板已由策划端拍板并落档（HH 无开放式决策）。
- 唯一执行节奏问题（**用户/执行端**定，不属策划范畴）：是否立即进入 2_10 步骤1+2 的编辑器落地，或在动手前先由用户过一眼 2_10 文档。无推荐之外的约束。

## 四、下一步建议（恢复执行入口）

1. **2_10 步骤1 渲染层结构**：场景内建 5 层（Tilemap_Ground/Feature + 预制体建筑层 + 单位 + Overlay），Iso 模式、Cell Size (1.28,0.64)。几何菱形片占位 + 调试配色（地形/建筑/单位按类型分色）。
2. **步骤2 `RenderMap`/`UpdateCell`**：遍历 `MapData.features` 用 `GridToIso` 铺地皮/特征物层——这是投影核心首次实机验证。
3. **步骤3 `CameraRig`**：正交 pan/zoom 整数档吸附/clamp（视域半高+1格）/`FocusOn`（初始=主城锚点 `GetKingdomAnchorWorld`）+ `ScreenToGrid`（Camera→IsoToCell）。
4. **小剧场串联验收**：K7 受袭→撤退→回位 + ①采集搬运 + ④守卫迎战 + ⑥巡逻，一次清 2_8 尾。
5. 提示：`GetKingdomAnchorWorld` 存在性在 `CameraRig` 实现时核实（文档沿用用户反馈，未在代码复核）。

---

## 策划裁决（策划端回写，裁决前保持空白）

无待裁决事项，策划端无需回写。如需补充小剧场验收口径或 2_10 细节，可在本行起追加。