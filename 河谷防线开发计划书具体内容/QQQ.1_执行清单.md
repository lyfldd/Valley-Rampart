# QQQ.1 执行清单

> 配套文档：QQQ.1_地图与NPC细节优化.md
> 生成于 2026-08-07

## 任务总表

| 编号 | 任务 | 需求# | 类型 | 涉及文件 | 验收标准 | 状态 |
|------|------|-------|------|---------|---------|------|
| T1 | Region 类加 `bool isProtectedResource` 字段 + `TerrainType protectedTerrain` 字段（记录保障地形类型） | 需求1 | 架构 | WorldManager.cs（Region 内部类） | grep `isProtectedResource` 能在 Region 类定义中找到 | ✅ GridTypes.cs:188-191 |
| T2 | GenerateMap Step 3-5 前新增 `PreReserveResources` 方法：在左右资源区各预选1个区块，设 isProtectedResource=true、protectedTerrain=Forest/Quarry | 需求1 | 架构 | WorldManager.cs（GenerateMap L173前 + 新方法） | 预占位后日志打印保障区块索引；左右资源区各至少1个 Forest + 1个 Quarry 占位 | ✅ WorldManager.cs:195,347-361 |
| T3 | PickTerrainByZone 对 isProtectedResource 区块直接返回 protectedTerrain，不走 PickWeighted | 需求1 | 架构 | WorldManager.cs（PickTerrainByZone L257-283） | 保障区块 terrain = protectedTerrain（Forest/Quarry），不被权重随机覆盖 | ✅ WorldManager.cs:274-277 |
| T4 | EnforceAdjacency 邻接违规时 fixIdx 跳过 isProtectedResource 区块（改邻居不改保障）；两相邻保障区块都跳过 | 需求1 | 架构 | WorldManager.cs（EnforceAdjacency L360-379） | EnforceAdjacency 后保障区块 terrain 仍为 Forest/Quarry（grep 验证或断言日志） | ✅ WorldManager.cs:385,395-411 |
| T5 | EnsureResourceCoverage 删除 ForceReplace 调用，保留统计 Debug.Log（"资源数量: forest=X quarry=Y fertile=Z"）；ForceReplace 方法可删除或保留空壳 | 需求1 | 架构 | WorldManager.cs（EnsureResourceCoverage L313-336） | grep EnsureResourceCoverage 无 ForceReplace 调用；运行时日志输出资源数量统计 | ✅ WorldManager.cs:333-339（ForceReplace 已删除） |
| T6 | 连续生成 20 张地图（不同 seed）验证无 Error 报错 | 需求1 | 验证 | — | Console 无 `[Error] 四资源保障` 报错；每张图至少1林地+1矿山 | ⬜ 需 Play Mode 实测（代码层已保证：4 保障区块不可被覆盖） |
| T7 | GridConfig 加 `float originX` 字段（默认0） | 需求4 | 参数 | GridConfig.cs + GridConfig.asset | grep `originX` 在 GridConfig.cs 中存在；asset 字段默认0 | ✅ GridConfig.cs:25 |
| T8 | GridSystem.WorldToCoord 改为 `FloorToInt((pos.x + originX) / cellSize)`；CoordToWorld 改为 `(coord.x + 0.5) × cellSize - originX` | 需求4 | 架构 | GridSystem.cs（WorldToCoord L41-47 + CoordToWorld L49-56） | 城堡中心 WorldToCoord 返回城堡中心cell；CoordToWorld(castleCenterCell).x = 0 | ✅ GridSystem.cs:44-45,54-55 |
| T9 | WorldManager.GenerateMap 在 PlaceAbandonedCastle 之后设置 `GridSystem.config.originX = castleCenterCellGlobal × cellSize`，其中 castleCenterCellGlobal = castleRegionIdx × cellCount + cellCount/2 | 需求4 | 架构 | WorldManager.cs（GenerateMap L224 PlaceAbandonedCastle 之后） | 运行时 GridSystem.config.originX = 城堡中心cell×cellSize；城堡世界坐标=(0,-3) | ✅ WorldManager.cs:235-241 |
| T10 | SceneHomePointProvider.GetHomePoint 人类阵营改为调用 `WorldManager.Instance.GetKingdomAnchorWorld()`；敌方保留 enemyHomePointAnchor | 需求2+3 | 架构 | SceneHomePointProvider.cs（GetHomePoint L28-38） | 人类 NPC HomePoint = 城堡中心(0,-3)；敌方 NPC HomePoint = enemyHomePointAnchor | ✅ SceneHomePointProvider.cs:29-40 |
| T11 | SceneHomePointProvider 在 WorldManager 未初始化（GetKingdomAnchorWorld 返回0,0 时）Debug.LogError 暴露时序问题 | 需求2+3 | 架构 | SceneHomePointProvider.cs | WorldManager 未就绪时 Console 报 Error 日志（而非静默返回0,0） | ✅ SceneHomePointProvider.cs:46-56 |
| T12 | UnitController 新增 `GetTalkLinesByOccupation(Occupation, bool hungry, bool injured)` 方法返回 string[]，含正常/饥饿/受伤三组对话池 | 需求5 | 架构 | UnitController.cs（新增方法，替代 GetTalkLineByOccupation L1107-1121） | 方法存在且按职业+状态返回对应 string[]；旧 GetTalkLineByOccupation 可删除或保留兼容 | ✅ UnitController.cs:1119-1128（旧 GetTalkLineByOccupation 已删除） |
| T13 | BuildInteractActions 对话动作文案改从 GetTalkLinesByOccupation 随机抽取（Random.Range(0, lines.Length)）；加状态判断：satiety<30→hungry、hp<40%maxHp→injured | 需求5 | 架构 | UnitController.cs（BuildInteractActions L1047-1104） | 点击同职业NPC多次显示不同对话；饥饿/受伤时显示对应状态对话 | ✅ UnitController.cs:1110-1118 |
| T14 | 填入 QQQ.1 文档中设计的全部职业对话文案（Worker/Porter/Resident/Child/Vagrant/Ruler/General/Warrior/Archer/Crossbowman/HeavyWarrior/Cavalry/ShieldGuard/Mage/Healer/Bishop/Archmage 共17职业） | 需求5 | 参数 | UnitController.cs（GetTalkLinesByOccupation 对话池） | 每职业正常5-8句 + 饥饿/受伤（如适用）2-3句；grep 对话字符串数量 ≥ 80条 | ✅ UnitController.cs:1134-1252（17 职业全量文案） |

## 跨需求依赖

| 任务 | 依赖 | 说明 |
|------|------|------|
| T2 | T1 | 先加 isProtectedResource 字段才能写预占位逻辑 |
| T3 | T1, T2 | PickTerrainByZone 需读 isProtectedResource/protectedTerrain |
| T4 | T1 | EnforceAdjacency 需检查 isProtectedResource |
| T5 | T2, T3, T4 | 删除 ForceReplace 补丁前，保障逻辑（T2-T4）必须先到位 |
| T6 | T1-T5 | 验证需全部需求1任务完成 |
| T8 | T7 | GridSystem 偏移需读 GridConfig.originX |
| T9 | T8 | WorldManager 设置 originX 需 GridSystem 已支持 |
| T10 | T9 | 建议坐标系调整后再改 HomePoint（主城坐标变为(0,-3)更直观）；不强制依赖，但建议顺序 |
| T13 | T12 | BuildInteractActions 调 GetTalkLinesByOccupation 需方法先存在 |
| T14 | T12 | 文案填入对话池需方法先存在 |

## 建议执行顺序

1. **批次1（需求4坐标系）**：T7 → T8 → T9（先改坐标系，后续坐标自动对齐）
2. **批次2（需求1地图报错）**：T1 → T2 → T3 → T4 → T5 → T6（底层重构，独立于其他）
3. **批次3（需求2+3 HomePoint）**：T10 → T11（坐标系已对齐，主城=(0,-3)）
4. **批次4（需求5 NPC文字）**：T12 → T13 → T14（独立，可并行于批次2/3）

> 说明：批次1和批次2都改 WorldManager.GenerateMap，建议串行避免冲突。批次4独立可并行。

## 完整性校验

| 需求# | 文档章节 | 对应任务 | 状态 |
|-------|---------|---------|------|
| 需求1 | §需求1 地图生成资源缺失报错 | T1, T2, T3, T4, T5, T6 | ✅ 6任务覆盖（字段+占位+尊重+跳过+删补丁+验证；T6 待 Play Mode 实测） |
| 需求2 | §需求2+3 NPC安全地带判断 | T10, T11 | ✅ 2任务覆盖（改来源+加错误暴露） |
| 需求3 | §需求2+3 开局NPC走(0,0) | T10, T11 | ✅ 随需求2解决，无独立任务 |
| 需求4 | §需求4 坐标系原点调整 | T7, T8, T9 | ✅ 3任务覆盖（加字段+改转换+设原点） |
| 需求5 | §需求5 点击NPC文字丰富 | T12, T13, T14 | ✅ 3任务覆盖（新方法+改调用+填文案） |

## 反偷懒自查

- [x] 每条任务能否回答"改哪个文件/改成什么样/怎么验收" → 全部能回答
- [x] 模糊动词清零 → 无"完善/实现/对接/优化"，全改成具体动作
- [x] 每个需求都有对应任务 → 完整性校验表全部 ✅
- [x] 跨需求依赖显式标注 → 依赖表已列
- [x] 执行顺序明确 → 4批次串行/并行建议已给
