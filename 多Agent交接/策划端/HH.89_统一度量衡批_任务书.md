# HH.89 统一度量衡批任务书

- **签发**：策划端，2026-09-07
- **决策号**：D547（0.6 §七十七）
- **优先级**：小批（与 HH.88 文件面零重叠，可并行施工；排期执行端自定）
- **背景**：用户核查时间换算后拍板"统一度量衡"——难度档影响秒/天=用难度改"现实秒↔游戏日"度量衡本身，与倍速机能（0.5/1/2/3x）重叠、破坏时间度量一致性，废除。

## 用户拍板（四项）

1. **统一基准 = 360 秒/天**（原 Hard 值；全难度一致，节奏调节完全交给倍速按钮）
2. **彻底移除**难度档→时间耦合（字段+调用链全删，防再犯）
3. 存档不做迁移（游戏未发布）；`LoadState` 恢复逻辑保留不动
4. 难度系数死值（`CurrentFactor` 每季+0.25 零消费方）挂账 **DZ-066** 另议，**本批不动难度系数链**

## 施工清单

### 件1 统一秒/天=360

1. `Valley Rampart/Assets/Resources/World/WorldConfig.asset`：`time.secondsPerDay: 480 → 360`（难度覆盖移除后的唯一真源）
2. `Valley Rampart/Assets/_Game/Data/WorldConfig.cs` L32：注释「默认 480（8分钟/天）」→「默认 360（6分钟/天）」
3. `Valley Rampart/Assets/_Game/Systems/Time/TimeManager.cs`：字段默认兜底 `secondsPerDay = 480f` → `360f`（约 L36）+ 头注释「默认 480 秒 = 8 分钟/天」同步更正

### 件2 彻底移除难度-时间耦合

4. `Assets/_Game/Data/WorldConfig.cs`：删 `DifficultyPreset.secondsPerDay` 字段及其 Tooltip（约 L95-96）+ `GetPreset` 默认兜底 `new DifficultyPreset { ... secondsPerDay = 480f }` 中的该字段（约 L70）
5. `Assets/_Game/Systems/Difficulty/DifficultyManager.cs`：`Initialize` 删 preset 获取与 `SetSecondsPerDay` 调用段（约 L68-75，日志同步去掉"秒/天="）；`SyncConfigFromWorld()` 方法整体删除（约 L77-86）
6. `Assets/_Game/Systems/World/WorldSystem.cs` L138：删 `DifficultyManager.Instance.SyncConfigFromWorld();` 调用
7. `Assets/Resources/World/WorldConfig.asset`：presets 三档的 `secondsPerDay` YAML 行删除（600/480/360 三行）

### 件3 验证

8. 编译 0 error 0 warning
9. 冒烟：Easy 与 Hard 各新建一局（可复用现有容器指定难度），Console 断言：
   - `[TimeManager] 初始化: ... 360s/天` 在场（两档一致）
   - `[DifficultyManager] 初始化` 日志不再含「秒/天=」
10. 读档回归：新档存 360 → 读回 360（`LoadState` 链保留验证）
11. 四容器回归（2_20 / 2_20B / 2_20C / 2_13_C）——冒烟按游戏日推进预期零影响，回归兜底

### 件4 完成报告（HH.90，落 多Agent交接/执行端/）

- grep 证据：`SyncConfigFromWorld` 全项目零命中；`DifficultyManager.cs` 内 `secondsPerDay` 零命中
- 施工文件清单 + 冒烟日志摘录

## 边界与纪律

- **不动** `TimeManager.SetSecondsPerDay` 方法本体（`LoadState` 仍消费）
- **不动**难度系数链（`CurrentFactor` / `DifficultyChangedEvent` / `factorGrowthPerSeason`）——DZ-066 裁决域
- **不动** P1 观察清单（480s 日→360s 日协议回写由策划端随验收做）
- 与 HH.88（SimModeConfig/KingdomBrain/SimModeManager）文件面零重叠
- 施工前先 git status 确认工作区状态，交付前 git diff 自查为前置（HH.42 家族教训：grep 双锚点复验=注册类标准动作）
