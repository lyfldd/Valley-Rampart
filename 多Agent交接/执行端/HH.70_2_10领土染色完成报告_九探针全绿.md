# HH.70 完成报告：2_10 领土染色实施批（步骤13 全清 · 九组探针 ALL PASS）

> 报告人：执行端 · 2026-09-05
> 任务真源：HH.69 开工令（2_10 实施计划步骤13 · 设计 §5.10 · D443+D448~D452）
> 结论：**段全清——实施/场景/冒烟三线闭环，P1~P9 ALL PASS**，编译 0 error，sim 零 T 级兑现。

## 一、交付清单（git status 实照）

| 件 | 类型 | 内容 |
|----|------|------|
| `Assets/_Game/Systems/Rendering/TerritoryOverlay.cs` | 新建 | 染色覆盖层核心（挂 MapRender 根） |
| `Assets/_Game/Data/TerritoryOverlayConfig.cs` + `Resources/Config/TerritoryOverlayConfig.asset` | 新建 | SO 配置（zoomLods {0,0}/{0.35,0.50}/{0.55,0.65}×3 档/0.3s/2.0s/1.0/1.1/true——Load 资产实测字段全对） |
| `Assets/Scenes/GameScene.unity` | 场景改动 | MapRender 第六层 `Tilemap_Territory`（Tilemap+Renderer order=5）+ 根挂 TerritoryOverlay（config/tilemap 引用已连，编辑器验证 True/True） |
| `Assets/Editor/Smoke/Valley2_10_Smoke_Territory.cs` | 新建 | 冒烟容器（RunFromMenu+自动跑 D522/暖 boot 规程/QuitSmoke 收尾） |
| `MapRenderService.cs` | 最小扩展 | +`OnChunkRendered` 静态事件（RenderChunk 尾触发，D445②）+`ChunkSize` 公共只读+`CreateIsoDiamondSprite` 提 public（白菱形复用，纯可见性） |
| `CameraRig.cs` | 最小扩展 | +`ZoomIndex` 只读 getter（档位四触发源轮询读点，D448；零行为变更） |
| `WorldLifecycle.cs` | 最小扩展 | ⑤ 序 +1 行 `TerritoryOverlay.ClearOverlay()`（清场染色层零残留，VagrantCamp 同款编排行模式） |
| `2_10_渲染与摄像机_实施计划.md` | 文档 | 头部回执行（2026-09-05 已实施） |

## 二、设计逐条兑现（§5.10 修订版）

| 设计条 | 兑现 | 探针 |
|--------|------|------|
| D448 视口分级 | zoomLods 对齐 zoomLevels 下标（超表取末档） | P3 4× 边界 0.65/内部 0.55 精确命中 |
| D449 近景全无色 | 整层 SetActive(false) 零 overdraw+alpha 全 0 | P1 层隐藏=True+主城 alpha=0 |
| D450 边界恒浓 | mid 级 8 邻域异主/无主检测；边界 mid 整块取 boundaryAlpha | P2 无主透明+k3 国边界 0.50；P5 孤立 0.50→注满转内部 0.35→外缘恒 0.50 |
| D451 跨档过渡 | 0.3s SO 化；SetTile 不随缩放；出近景先激活渐显（from=0）、回近景渐隐完再隐藏 | P4 同帧无跳变 0.65→中途 0.544→隐藏→渐显中途 0.074→到位 0.50 |
| D445 刷新三路 | ①TerritoryChangedEvent 增量（Added±1 圈边界重算）②OnChunkRendered 补染 ③GameLoadedEvent 全量重染 | P5 增量/P6 补染（直调 6+真链即染 6，homeMid 锚定）/P8 幂等 65 全一致 |
| D448 档位第四触发源 | Update 轮询 CameraRig.ZoomIndex 跨档重定 alpha | P4 跨档链 |
| D446/D379 灭国渐隐 | FadeOutKingdom(kid) 2s alpha→0 后清 tile——纯渲染不占数据；调用点=2_19（Removed 事件扩展时接线，未预改 2_17） | P7 前=0.5→渐隐中 0.489→2.6s 后清空+他国保留 |
| D447 配色派生 | bannerColor 渲染时 HSV 派生（S×1.0/V×1.1），旗色数据零污染，按 kid 缓存 | P2 四国实色：k0 蓝(0.22,0.42,0.83)/k1 绿(0.33,0.72,0.39)/k2 灰(0.50,0.50,0.55)/k3 铜(0.61,0.44,0.33)——色异成立 |
| D443 渲染结构 | 单一白菱形 tile（MapRenderService 同款 128×64@PPU100）+per-cell SetColor；order=5（Ground 0 上/Feature 10 下）；无主不铺；玩家 id=0 同染 | P2 无主透明+56 mid 全染（=Ledger 全量） |
| D452 高亮 | HighlightKingdom=临时中景浓度（MidLod 无视档位）+FocusOn 领地质心（GridToIso 基准）；(-1) 取消回当前档位 | P9 近景隐藏→临时激活+中景 0.35→取消回隐 |
| 2_13 接口 | SetVisible(bool)/HighlightKingdom(int) 已备（UI 消费归 2_13 实施批） | P9 路径即接口本体 |

## 三、冒烟证据（Valley2_10_Smoke_Territory，真实进局暖 boot#6）

```
P1 近景负探针 层隐藏=True 主城mid alpha=0=True
P2 中景着色 激活=True painted=56 色异=True(kids=4 色[k0=(0.22,0.42,0.83,0.50) k1=(0.33,0.72,0.39,0.50) k2=(0.50,0.50,0.55,0.50) k3=(0.61,0.44,0.33,0.50)]) 无主透明=True
P3 远景浓色 边界=0.65(≈0.65) 内部=0.55(≈0.55) fI=True fB=True
P4 跨档平滑 同帧=0.65 中途=0.544 回近景隐藏=True 渐显中途=0.074 到位=0.5=True
P5 增量+边界重算 孤立=0.5 注满转内部=0.35 外缘恒浓=0.5
P6 chunk补染 直调=6/主城0.5 重铺=True 钩子重载=True 订阅者=1 即染=6 painted=6 主城=0.5
P7 灭国渐隐 kid=1(15mid) 前=0.5 渐隐中=0.489 清空=True 他国保留=True
P8 读档重染幂等 基线=65 重染一致=True 事件触发一致=True
P9 高亮(D452) 近景隐藏=True 临时激活=True 中景浓度=0.35 取消回隐=True
===== ALL PASS =====
```
收尾 QuitSmoke：`清 smoke_ 槽位存档+退 Play`（smoke_t10 入自愈覆盖面 ✓）。

**迭代链如实列报（5 跑）**：①首跑 4 FAIL（P1 近景直落缺隐藏分支/P4 出近景渐显未启过渡/P6 钩子未生效/P2 色异全白）→②修产品两笔+容器调试化（3 FAIL，P2 色值全白实锤）→③局内诊断（tile 已铺 sprite 128×64 色白(1,1,1,1)=未写色）→**LockColor 破案**（见 §四）→④修 EnsureTile（1 FAIL，P4 断言未落盘+P6 主城锚定错位）→⑤P6 homeMid 锚定+chunk 未加载放行（1 FAIL，P7 前置全染）→⑥**ALL PASS**。每次 FAIL 如实归因，无产品缺陷遗留。

## 四、关键实测破案（经验沉淀）：运行时 Tile 默认 flags=LockColor

- **现象**：per-cell `Tilemap.SetColor` 全部失效（写后 GetColor 读回白 (1,1,1,1)），且激活/解锁层均无效；`GetTile`/sprite 全正常。
- **实锤**：局内读 `GetTileFlags(pos)=LockColor`——**运行时 `ScriptableObject.CreateInstance<Tile>()` 默认 flags 含 LockColor**，锁 per-cell 着色。
- **修复**：EnsureTile 里 `_tile.flags = TileFlags.None` 解锁后 SetColor 立即生效（写 (1,0,0,0.5) 读回精确命中）。
- **影响面**：Ground/Feature 层历史 tile 从未用 SetColor（每色一 tile 实例走纹理色），故该默认值此前从未暴露；本批是项目内首个 per-cell tint 消费端。
- **建议沉淀**：so-data-driven/冒烟知识库——"运行时创建 Tile 后若要用 SetColor tint，必须 flags 解锁 LockColor"。

## 五、开工回执→实施期口径修正两笔（如实列报）

1. **MapGenerated 兜底重染不采用**（HH.69 §二.5 原拟"Ledger.Count>0 时兜底"）：实读 SaveManager 读档链——`GameLoadedEvent`（收尾 L354）**晚于** MapGeneratedEvent（Global 段建图），且新游戏多轮冒烟时 MapGenerated 时点 Ledger 含上轮残留——兜底重染会染旧账本。修正：MapGeneratedEvent 仅清层防残留；读档全量重染唯一挂点=GameLoadedEvent(success)（LoadState 不广播事件，实测时序安全）。HH.69 已声明"实施中发现修正升级列报"，此为兑现。
2. **P5 探针正坐标域**：原拟 (0,0) 域注入含负 mid——`CellToMidChunk` 整数除法对负数取整歧义（-3/4→0）会错位；改为 mid(5,5) 正坐标域（8 邻全正），断言三段全绿。

## 六、降级口径列报（全非阻塞）

- **P6 竞态探针**=反射重跑主城 chunk（真钩子链 SetCell→OnChunkRendered→Ledger 补染，直调/真链两段分解双证）；真 chunk 竞态视觉窗口（镜头滑入未加载 chunk）归 2_16 P1 末录屏链观察——本批"染色可见"实证（P2 色异+tile 数）已解除其阻塞项。
- **P8 读档幂等**=快照-重染口径（ReapplyAll 基线→重染一致 65→GameLoadedEvent 触发一致）+2_17_12 P7 同款降级；真 LoadScene 读档全链不重放（该链已有独立存档回归面）。
- P7 渐隐验证用渲染侧 API（FadeOutKingdom）直驱——2_19 八步管线接线后的真事件链归其验收批（D446 前向契约，本批不预改）。

## 七、红线自查

1. **AI.Core/训练仓零触碰**：本批文件=渲染层（TerritoryOverlay/TerritoryOverlayConfig/MapRenderService/CameraRig/WorldLifecycle 1 行）+场景+Editor 冒烟；AI.Core/TuningSnapshot/champion/factor_registry 零 grep 命中改动。sim 评估=纯渲染消费端（Ledger/bannerColor 只读）——**零 T 级兑现**。
2. **2_16/2_17 数据层零改动**：TerritorySystem/KingdomRegistry/KingdomState/事件结构零修改（Removed 语义预留随 2_19）。
3. **WorldLifecycle 只加 1 行编排行**（⑤ 序 ClearOverlay），不动既有序列。
4. **冒烟走 SmokeApi/暖 boot 规程**（HH.68 沉淀），QuitSmoke 自愈收尾。

## 八、git 全量对照

本批 8 件（§一）+ 域外文档（HH.69/HH.70/索引/实施计划）。**排除项（策划端并行域，commit 时不入库）**：`图片资源\四族风格锚点\`（untracked）、策划端文档改动。工作树无意外文件。

## 九、验收请求

1. **验收通过→commit 代执**：本批 8 件+域外文档（策划端域排除，同 HH.68 隔离惯例）。
2. **2_10染 收口**：步骤13 回执区已落盘；2_16 P1 末录屏验收链"染色可见"阻塞解除（P2 实证）。
3. **P1 总验收看台**：「≥2 AI 自主至军事期」前置全达成（本体运行排期后即开考）。
4. 挂账：本批零新增挂账；§六三笔降级口径如上，无需裁量。

## 十、策划端验收（D532，2026-09-05，✅验收成立——2_10染 收口，P1 总验收解锁）

| 项 | 策划端裁决 |
|----|-----------|
| 段全清（实施/场景/冒烟 P1~P9） | ✅ 实盘复核全数实锤：TerritoryOverlay 源码核读（三路刷新注释含读档时序实测说明/ FadeOutKingdom/2_13 接口/L8「零写入数据源」声明）；三笔最小扩展 diff 逐行核对（WorldLifecycle 仅 1 行编排行+CameraRig 只读 getter+MapRenderService 钩子/ChunkSize/CreateIsoDiamondSprite 纯可见性放开）——**渲染消费端口径兑现，TerritorySystem/事件结构零触碰**；GameScene 第六层+142 行合理；P1~P9 断言含中间值（0.544/0.074/0.489）=真实跑过痕迹 |
| LockColor 破案沉淀归档 | ✅ 采信——运行时 CreateInstance<Tile> 默认 flags 含 LockColor 锁 per-cell SetColor，TileFlags.None 解锁；已落 2_10 实施计划回执行（知识归档完成），项目内首个 per-cell tint 消费端经验 |
| 口径修正两笔（§五）采信 | ✅ ①MapGenerated 兜底重染不采用（读档链时序实测：GameLoadedEvent 晚于 MapGeneratedEvent+新游戏 Ledger 残留会染旧账——修正正确）②P5 正坐标域（负数整除歧义） |
| 降级三笔（§六）采信 | ✅ 全归既有验收面（P6 竞态视觉窗口→2_16 录屏链/P8 真读档链→独立存档回归/P7 真事件链→2_19 D446 契约），零新增挂账确认 |
| commit 代执（策划端域排除） | ✅ 本串执行（执行端 8 件+域外文档+验收件；美术目录/策划域排除） |
| P1 总验收排期 | ✅ 前置全达成（Q10 收官+2_10染 收口+染色可见阻塞解除）——**P1 总验收（≥2 AI 自主至军事期）排期看台**，验收方案由策划端随排期签发 |

**嘉奖**：LockColor 破案（局内读 GetTileFlags 实锤→TileFlags.None 解锁→写读精确命中）=项目内首个 per-cell tint 消费端的经验性发现，探针迭代链 5 跑全如实归因（无产品缺陷遗留），诚实分层纪律持续兑现。
