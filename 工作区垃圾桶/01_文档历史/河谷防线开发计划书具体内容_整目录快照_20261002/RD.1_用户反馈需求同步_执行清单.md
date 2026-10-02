# RD.1 用户反馈需求同步（加载卡顿/chunk渲染/自然装饰数据化）执行清单

> 配套追记：
> - [改造计划/2_10_渲染与摄像机.md](./改造计划/2_10_渲染与摄像机.md)（WASD/MapVisualizer退役/chunk动态加载）
> - [改造计划/2_1_2D地图生成.md](./改造计划/2_1_2D地图生成.md)（自然装饰数据化）
> - [改造计划/2_2_建筑与占格.md](./改造计划/2_2_建筑与占格.md)（工具建筑放置数据覆盖）
> - 生成于 2026-08-21 · 来源：Unity 测试/用户反馈（require-doc-sync 补同步）

## 背景

用户上一轮提出的需求（WASD 自由滑动、MapVisualizer 退役、加载卡顿优化、A+ 自然装饰数据化）均已改代码，但未按 requirement-doc-sync 追记到负责文档。本条清单为补同步，任务均为**已实施**，逐项验收留证。

## 任务总表

| 编号 | 任务 | 涉及文件 | 验收标准 | 状态 |
|------|------|---------|---------|------|
| T1 | WASD 四向平移 + 初始主城上方（FocusHome 后相机上移半屏高） | CameraRig.cs | Play 中 WASD 可平移、初始相机 y=主城中心-半屏；编译 0 错误 | ✅ |
| T2 | MapVisualizer 及旧 1D 正交底图（参考图/平原/9-Sliced/Baseline/08_image/13_image/Palette1111）退役 SetActive(false) | GameScene.unity | get_active 盘点仅剩 MapRender + units；场景保存 | ✅ |
| T3 | chunk 视域动态加载（chunkSize=24、EnsureStrongHomeArea+UpdateViewport、lookaheadChunks 环形预加载） | MapRenderService.cs | 初始 groundCells=5184（全图8%），镜头右移2chunk 增量到18240；chunkSize=0 退全量 | ✅ |
| T4 | DeriveNaturalBuildings 只派生一次可采集 OreVein（树/矿/雪山不再派生） | MapGenRules.cs | naturalBuildings=1354（全 OreVein，Tree/Mine=0） | ✅ |
| T5 | WorldManager.TryConsumeResourceNode + IsResourceNodeAvailable；GridSystem.RefreshCellFromFeature | WorldManager.cs, GridSystem.cs | 伐木场建树上 IsResourceNodeAvailable(Tree)=True、TryConsumeResourceNode Tree→Plain+渲染刷新 | ✅ |
| T6 | BuildController 伐木场/采石场放置改数据覆盖；PlacementValidator needsNode 改 features 判定 | BuildController.cs, PlacementValidator.cs | 无 node 实体时靠 IsResourceNodeAvailable 通过校验；OreVein 采集链路不受影响 | ✅ |
| T7 | 加载时间根治 |（多文件） | ApplyConfig 16322ms→1103ms（15倍） | ✅ |

## 完整性校验

| 需求 | 追记文档 | 对应任务 | 状态 |
|------|---------|---------|------|
| WASD 自由滑动+初始主城上方 | 2_10 变更追记 | T1 | ✅ |
| MapVisualizer/正交底图退役 | 2_10 变更追记 | T2 | ✅ |
| chunk 视域动态加载 | 2_10 变更追记 | T3 | ✅ |
| 自然装饰全量数据化 | 2_1 变更追记 | T4, T5 | ✅ |
| 工具建筑放置数据覆盖 | 2_2 变更追记 | T5, T6 | ✅ |
| 加载卡顿根治（总目标） | 2_1/2_2/2_10 | T7 | ✅ |

## 待后续（记录，非本次处置）

- 守卫锚点 `GuardDeploymentSystem.FindNearestResourceNode` 原依赖 isResourceNode 实体，树/矿消失后仅剩 OreVein 锚点——待按 A+ 口径改查 features 索引（登记在 2_1 变更追记备注）。