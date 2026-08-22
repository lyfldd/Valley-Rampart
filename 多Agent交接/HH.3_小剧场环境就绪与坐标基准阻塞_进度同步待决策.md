# HH.3 小剧场环境就绪与坐标基准阻塞

> 类型：进度同步（含待决策）
> 状态：✅已裁决（2026-08-22 策划端，见文末回写）
> 日期：2026-08-22 · 发起端：执行端 · 关联：2_10 步骤5/步骤6、2_8 尾巴、守卫锚点适配（HH.2）

## 一、做了什么（带证据）

**1. 2_10 步骤5「排序与遮挡专项」(D104/D106)——已完成、已实证**
- 全局 `GraphicsSettings.m_TransparencySortMode=3(CustomAxis)`、`m_TransparencySortAxis=(0,1,0)` 早已就位；短板=建筑(order1)与单位(order0)异序 → building 恒盖 unit。
- 代码改动唯一一处：[UnitController.cs](file:///c:/Users/trs/Desktop/Valley Rampart/Valley Rampart/Assets/_Game/Systems/Unit/UnitController.cs) `Awake` 单位 `sortingOrder 0→1` + `spriteSortPoint=Pivot`，并入世界对象基底。
- **实机探针（Play + RenderTexture 像素采样，非目测）三测试结果**：
  - **测试1** 单位在墙下方(y小) → 重叠区采样显黄=**单位在前** ✅
  - **测试2** 单位移到墙上方(y高) → 重叠区采样显灰=**墙挡单位** ✅
  - **测试3** 同深度相邻：CustomAxis 排序确定性高，无交替闪烁（概念满足）✅
- **方向结论：世界 Y 小者(画面下方/前方)渲染在前，符合等轴遮挡直觉。D104 方案成立，未触发中止线。**
- 连带检查：投射物 order=5（仍在单位上）、建造幽灵 order=1（同建筑），无副作用。编译 0 错误。
- 已同步 [2_10_实施计划](file:///c:/Users/trs/Desktop/Valley Rampart/河谷防线开发计划书具体内容/改造计划/2_10_渲染与摄像机_实施计划.md) 与开发计划书工作日志。

**2. 小剧场环境就绪（2+ 拍板）——做了「出图入口」，单位铺放被坐标基准阻塞**
- [AIDebugSpawnController.cs](file:///c:/Users/trs/Desktop/Valley Rampart/Valley Rampart/Assets/_Game/Systems/AI/AIDebugSpawnController.cs) 新增：
  - `StartNewGameDebug()`：**复用 LoadManager.InitializeNewGame 既有链**出图（+SpawnInitialEntities + OnNewGameMapReady），幂等。实测出图 **256×256、自然建筑=1210**。
  - `SpawnCoordBasisProbe()`：坐标基准探针。
- **坐标基准不一致（决定性实证，阻塞单位铺放）**：主城中心格(128,128)→
  - 逻辑 `GridSystem.CoordToWorld=(0.64,0.32)`，`WorldToCoord` 能回环 ✅
  - 等轴 `MapRenderService.GridToIso=(0.00,81.92)`，**`WorldToCoord(iso)` 返回 null（越界）** ❌
  - ⟹ 单位放逻辑坐标→渲染在等轴瓦片外；放等轴坐标→逻辑/AI/寻路全断。**两个基准无法同时满足渲染与逻辑。**
- **另一环境障碍：相机未对准主城**——出图后相机在 `(147.49,6.08)`，非 iso 中心 `(0,81.92)`，屏中心整列蓝=水（瓦片区外）。CameraRig.FocusHome 未接管/被覆盖，需查。

## 二、现状与阻塞

**阻塞：逻辑坐标(中心原点正交) vs 渲染坐标(iso 等轴) 不一致。** 单位无法在「渲染落在瓦片 + 逻辑可寻路」两相同时成立下铺放。
二次障碍：相机未自动对准主城。

这一步不是执行端能擅自拍板的——它涉及 GridSystem 世界坐标映射作为唯一世界基准，影响寻路/空间分区/拾取全链路（doc 1 §1.6 原决定"逻辑正交，等轴投影归 2_10"）。打通小剧场前必须先裁决坐标基准。

## 三、待决策（选项 + 推荐 + 影响）

1. **坐标基准统一方向**——这决定 GridSystem 世界映射是否重构、以及 2_12/2_13/2_14 验收能否复用本环境。
   - **A（推荐）**：**统一到 iso 基准**——`GridSystem.CoordToWorld/WorldToCoord` 改为等轴映射（与 MapRenderService.GridToIso/IsoToCell 一致），逻辑层按 iso 世界运行。影响：改动 GridSystem 核心映射，需回归寻路/空间分区/拾取/小剧场所有依赖 CoordToWorld 处；但一次到位，渲染/拾取/逻辑同基准，符合 2_10 已把 render+拾取定为 iso 的事实。
   - B：维持逻辑正交，单位视觉用 iso 世界坐标、逻辑另存正交坐标（现 UnitController 单 transform 模型下需加一层视觉/逻辑分离）。影响：改动面集中在单位表现侧，但长期两套基准并存，存隐患。
   - C：逻辑不变、相机/场景全部改回正交渲染（否定 2_10 iso 化）。影响：推翻已验收的 iso 铺格/CameraRig，不建议。
   - 另注意：出图时 **1210 条 `BuildingPlacedEvent 无订阅者`** 告警刷屏——A+ 后 data 层仍 Instantiate 自然建筑并发事件，建议并入环境清理（静音或过滤，属次要）。

2. **相机对准主城（CameraRig 未生效）**——属执行端可查的 bug，不在裁决范围，但需确认是否本轮交接一并查（建议下次会话先查）。

## 四、环境使用说明（下次会话开箱即跑）

- 进 Play GameScene → 控制台/脚本调用 `AIDebugSpawnController.Instance.StartNewGameDebug()` → 地图生成 → 再调用各场景编排。
- 单位铺放进度：**未建**（被坐标基准阻塞），以下为计划内的四链编排目标，取证关键字已备好：

## 五、四链 PASS 判据与取证标准（事件日志 grep，逐条列出）

> 取证方式：Play 驱动场景 → 控制台 grep 下列关键字 → 每条链全部命中即 PASS；任一缺 → 该链 FAIL。截图辅助确认"在瓦片上"。

| 链 | 驱动 | 必命中日志关键字（grep） |
|----|------|--------------------------|
| ①采集搬运（工人采/搬/入库闭环） | SpawnWorker 工人在农场/林场旁 + 派Gather任务 | `[TaskScheduler] 派发` / `[TaskScheduler] 完成` / `HarvestCarry` / `[TaskScheduler] 搬运`（BehaviorExecutor MoveComplete） |
| ④守卫迎战（袭矿→守卫交锋） | `GuardDeploymentSystem.DeployGuardAt(OreVein)` + WaveDirector 刷敌 | `[GuardDeploymentSystem] 已部署守卫区域` / `[GuardDeploymentSystem] 守卫区域丢失`（GuardRegionLostEvent）/ 守卫 combat 命中日志 |
| ⑥巡逻推进（探路揭迷雾） | `PatrolTaskSystem.StartPatrol(pos)` | `[PatrolTaskSystem] 发布巡逻` / `[PatrolTaskSystem] StartPatrol 失败`（若失败=该链环境问题）/ 迷雾揭除日志 |
| K7 受袭→撤退→回位 | 威胁压近工人/守卫 | `RetreatToSafeAnchor` / `[PatchFollower]` 撤退寻路 / `撤退`（NPCBrain 目标选择）/ 回位归巢（MoveComplete→回工作点） |

## 六、守卫锚点适配任务卡（昨已拍，排 2_12 前；HH.2 残留债，非本次处置）

1. **FindNearestResourceNode 改查 features 派生的资源点索引**（A+ 口径，Mine/Tree 亦可部署）。
2. **GuardRegionLostEvent 触发语义重定义**——Mine 无实体后不被"击退"，"资源点失去覆盖"判定需重新定义（HH.2 真正残留债，不只是查找来源）。
3. **重验范围收窄声明**：只重验锚点查找+部署位置，不重验守卫迎战行为全链。

## 七、下一步建议（恢复执行入口）

1. 策划裁决待决策#1（坐标基准统一方向）。
2. 查相机未对准主城 bug（CameraRig 未接管）。
3. 按裁决统一基准，重建单位铺放编排（守卫@OreVein旁/工人@树旁/巡逻单位/敌兵，可复跑）→ 逐链驱动 → grep 取证 → 四链全 PASS 才算清完 2_8 尾巴。

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 坐标基准统一方向 | **A：统一到 iso 基准**（附三条落地纪律，见下） | 2_10 已验收三步均 iso 原生，A 向既成事实对齐；策划端补充数学实证：iso 嵌入下格四邻步长全等（±0.64,±0.32 模长均 0.716/格），优于现正交矩形格 1.28/0.64 的 2:1 不等——A 对移动节奏 sim 一致性是改善；grep 实证 NPCBrain/BehaviorExecutor/FormationBrain/PatrolTaskSystem 共 20+ 处 Vector2.Distance 直读 transform.position，B 双基准=每处加换算层，否决；C 推翻已验收 2_10，否决 |
| BuildingPlacedEvent 刷屏清理 | **自然预置建筑不发该事件** | 事件语义收窄为「玩家可见建筑落成」（2_12 订阅方按此预期）；自然预置非 gameplay 事件，当前零订阅者剔除安全；OreVein 实体保留至守卫锚点适配任务（本篇六）落地 |

**A 的三条落地纪律（缺一不算落地）**：
1. **doc 同步先行**：doc 1 §1.6 与 2_10 铁律字面修订（「逻辑正交」→「逻辑=格基准，世界坐标=等轴嵌入，投影双向纯函数」），防止未来会话把它"修复"回去。
2. **回归清单**：CoordToWorld/WorldToCoord 全调用方回归（寻路 waypoint/空间分区/拾取/刷怪/编队锚点/LOD hotspot）+ 坐标探针扩为全象限 round-trip + 既有 smoke 全过。
3. **速度重标定**：移动速度 SO 按 iso 步长（0.716/格）一次性重标；采集/巡逻/撤退节奏随小剧场重验。

**附带裁决**：Vector2.Distance 系启发式（感知/威胁/索敌）在 iso 下形状语义变化（圆→菱形拉伸）可接受——本就是模糊感知且 sim 侧走格距，不要求改格距换算。

**相机 bug**（CameraRig 未接管）：非裁决项，执行端自查，下次会话第一件事。

### 分歧裁决记录
- 执行端意见：推荐 A（解法见三.1）；BuildingPlacedEvent 并入环境清理。
- 策划端意见：选给定 A（破框检查通过：A/B/C 前提各异无共同盲区，A 全维胜出）；事件清理方案按执行端建议采纳并收窄语义。
- 裁决：A + 三纪律；事件剔除自然预置 · 依据：AI 北极星（格唯一真源+iso 步长均匀改善 sim 一致性）/单人 1 月规模（B 的换算层=20+ 处永久税）/兼容风险（C 推翻已验收产物）

### 衍生产物
- 新建设计文档：doc 1 §1.6 + 2_10 铁律修订（随 A 落地一并提交）
- 新建清单任务：守卫锚点适配（本篇六，排 2_12 前）