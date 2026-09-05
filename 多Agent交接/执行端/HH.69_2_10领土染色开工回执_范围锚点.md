# HH.69 开工回执：2_10 领土染色实施批（步骤13 · D443+D448~D452）

> 回执人：执行端 · 2026-09-05
> 任务真源：实施计划 步骤13（2026-08-31 视口分级修订版）+ 设计 §5.10 + 0.6 §四十一/四十二（D443~D447/D448~D452）
> 前置确认：Q10 本体批 M1~M10 十项全绿（D530），2_10染 按排期解锁。数据三件套前置零缺口已实读核验。

## 一、交付范围

| 件 | 类型 | 说明 |
|----|------|------|
| `Systems/Rendering/TerritoryOverlay.cs` | 新建 | 染色覆盖层（挂 MapRender 根）：视口分级/三路刷新+档位第四触发源/渐变过渡/灭国渐隐/2_13 接口 |
| `Data/TerritoryOverlayConfig.cs` + `Resources/Config/TerritoryOverlayConfig.asset` | 新建 | SO 配置：zoomLods[3]/两段渐变时长/配色派生/enableOnStart（so-data-driven） |
| GameScene MapRender 根第六层 | 场景改动 | `Tilemap_Territory`（Tilemap+TilemapRenderer sortingOrder=5）+ 根挂 TerritoryOverlay 组件 |
| `Valley2_10_Smoke_Territory.cs`（Editor/Smoke） | 新建 | 行为级冒烟容器（SmokeApi/暖 boot 规程），九组探针 |
| MapRenderService / CameraRig / WorldLifecycle | 最小扩展 | 三笔，见 §二.4 渲染层接口扩展列报 |

## 二、锚点实读确认（开工前全部实读）

1. **数据三件套（均落地，零缺口）**：
   - `TerritorySystem.Ledger`：`IReadOnlyDictionary<Vector2Int,int>`（[TerritorySystem.cs L42](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/TerritorySystem.cs#L42)）——**key=中区块 mid**（CellToMidChunk，默认 midChunkSize=4），非格坐标。
   - `TerritoryChangedEvent{KingdomId, Added[]}`（[GameEvents.cs L620](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Core/GameEvents.cs#L620)）：坐标序保确定性；**现状仅 Added**（清除语义 Removed 随 2_19 八步管线扩展，本批不预改 2_17 已落代码，D446 前向契约）。
   - `KingdomState.bannerColor`（KingdomRegistry id=0 玩家默认在表）。
2. **染色粒度口径（本批声明）**：Ledger 真源粒度=中区块 → 染色 per-mid 展开 `midChunkSize²`（默认 16）格 tile 同色同 alpha；**边界检测在 mid 级做 8 邻域异主/无主**，边界 mid 整块用档位表 boundaryAlpha（D450 边界恒浓）；±1 圈邻域重算同在 mid 级。
3. **场景现状**：MapRender 根=Grid（IsometricZAsY，cellSize 1.28×0.64）+MapRenderService；五子层=Tilemap_Ground（sortingOrder **0**）/Tilemap_Feature（**10**）/Prefab_Building/Units/Overlay；Sprites-Default 材质 → **Tilemap_Territory 取 order=5**（Ground 之上、Feature/实体之下，D443）。
4. **渲染层接口扩展三笔（最小侵入，列报）**：
   - `CameraRig`：+`ZoomIndex` 只读 getter（档位四触发源 D448 需读取档位；私有 `_zoomIndex` 无公开口）。TerritoryOverlay Update 轮询跨档检测，渲染层自治，不加事件总线、零行为变更。
   - `MapRenderService`：+`OnChunkRendered` 静态事件（chunk 铺设完成钩子，D445②）+`CreateIsoDiamondSprite` 提 public static（染色 tile 白菱形复用，纯可见性微调）。
   - `WorldLifecycle` ⑤ 序 +1 行 `TerritoryOverlay.ClearOverlay()`（清场染色层零残留；VagrantCamp 同款编排行模式；MapRenderService.ClearAllTiles 现只清 Ground/Feature，跨轮冒烟旧色块会残留）。
5. **读档全量重染触发源**：`GameLoadedEvent(slotId, true)`（SaveManager L354 读档成功广播）→ ReapplyAll；新游戏=RebuildInitial/ClaimInitial 事件天然覆盖；`MapGeneratedEvent` 时若 `Ledger.Count>0` 兜底重染（防"账本恢复早/晚于地图生成"时序差）。EnterPlayingGate 调用点无事件，由上述三路并集覆盖（D445③ 语义达成）。
6. **SO 惯例**：`Resources/Config/` 平铺（CameraConfig.asset 同层）；LoadConfig 模式=Resources.Load 缺失回退 CreateInstance（KingdomBrain.LoadConfig 同款）。
7. **冒烟基建**：SmokeApi.EnterGame/ResetWorldForNext/QuitSmoke + 暖 boot 规程（HH.68 沉淀）；容器模式=2_17_12（RunFromMenu/RunAuto/RunHost/反射/清理防污染）。

## 三、设计要点实施口径（§5.10 修订版逐条对照）

| 设计条 | 实施口径 |
|--------|---------|
| D448 视口分级 | `zoomLods[]` 对齐 zoomLevels 下标（缺档取末档）：[0]={0,0} 近景全无色 / [1]={0.35,0.50} 中景 / [2]={0.55,0.65} 远景 |
| D449 近景隐藏 | lod=0 整层 SetActive(false)（零 overdraw）；tile 常驻不随缩放重铺（D451） |
| D451 跨档过渡 | 0.3s SO 化平滑插值；出近景先激活再渐显（from=0），回近景渐隐至 0 完成后再隐藏；SetTile 不发生，只动 SetColor |
| D450 边界恒浓 | 8 邻 mid 异主/无主 → 边界 mid 整块 boundaryAlpha；近景 0 自洽归零 |
| D445 刷新三路 | ①事件增量（Added 逐格+±1 圈边界重算）②chunk 钩子查 Ledger 补染 ③全量重染（MapGenerated 兜底+GameLoadedEvent 读档）+④档位轮询跨档（Ledger 内存直查） |
| D446/D379 灭国渐隐 | 渲染侧 API 就绪：`FadeOutKingdom(kingdomId)` 2s alpha→0 后清 tile（纯渲染不占数据状态）；调用点=2_19 实施批（Removed 事件扩展时接线）；冒烟负探针用同 API 验证 |
| D447 配色 | bannerColor 渲染时派生（HSV：S×colorSaturation、V×colorBrightness），旗色数据零污染；派生色按 kingdomId 缓存 |
| D452 高亮 | `HighlightKingdom(kid)`=该 kid 全部 mid 临时按中景档浓度显色（无视当前档位，近景下亦有反馈→临时激活层）+FocusOn 领地质心（GridToIso 基准）；`HighlightKingdom(-1)` 回当前档位状态 |
| D443 无主/玩家 | 无主地透明不铺；玩家 id=0 同染（D303 统一注册） |
| 2_13 接口 | `SetVisible(bool)` 染色总开关（enableOnStart 初始值）+ `HighlightKingdom(int)`；UI 消费归 2_13 实施批 |

## 四、sim 评估列报

本批=**纯 Unity 渲染消费端**（2_16/2_17 数据层只读：Ledger/事件/bannerColor；不触 AI.Core/TuningSnapshot/ProfessionSnapshot/champion SO/训练仓；Unity 侧零新增决策输入——相机档位是渲染态不进决策核）。**预期零 T 级、零 sim 同步义务**。若实施中发现任何触面越界，升级列报。

## 五、冒烟探针计划（Valley2_10_Smoke_Territory，真实进局）

1. 近景负探针：默认 1× 档层隐藏/全无色（D449）
2. 2× 着色：ZoomTo(1)→各国着色且色异+无主地透明（负探针）——**染色可见实证（解除 2_16 P1 末录屏阻塞项）**
3. 4× 远景浓色：alpha 高于中景（D448 世界盒观感）
4. 跨档平滑：切档瞬时 alpha 处于过渡区间无跳变+0.3s 后到位+回 1× 渐隐后层隐藏（D451）
5. 增量：正规写点 ClaimFootprintChunk 新 mid → 当帧着色+旧边界 mid 恒浓→内部 alpha 重算（D450/D445）
6. chunk 竞态：清染色缓存模拟"事件早于 chunk"→OnChunkRendered 补染恢复（D445②）
7. 灭国渐隐负探针：FadeOutKingdom→2s 后该 kid tile 全清+不误伤他国（D446 渲染侧）
8. 读档重染幂等：全染快照→清→ReapplyAll→逐 mid 一致 + GameLoadedEvent 触发路径（D445③）
9. SetVisible(false) 全隐藏 + HighlightKingdom 近景下临时显色+聚焦（D452）

收尾 QuitSmoke（smoke_ 槽位自愈）。探针分批次跑，轮间 ResetWorldForNext。

## 六、风险与边界

- 灭国渐隐与跨档过渡正交实现（fadeMul 乘法因子独立于档位 alpha），避免两系统叠加互相污染。
- 染色层 SetColor 频度：过渡/渐隐期间逐帧写，稳定期零写（事件驱动）——性能面符合"渲染零逻辑副作用"铁律。
- 冒烟探针 8（读档幂等）采用快照-重染口径，真读档 LoadScene 全链不在本容器重放（2_17_12 P7 同款降级，如实列报）。

## 七、执行序

① TerritoryOverlayConfig.cs+TerritoryOverlay.cs → ② 三笔最小扩展（MapRenderService/CameraRig/WorldLifecycle）→ ③ 编译 0 error → ④ SO 资产+场景第六层+挂组件（Unity MCP）→ ⑤ 冒烟容器+跑批（暖 boot）→ ⑥ 回执区落盘+git 自查 → HH.70 完成报告。
