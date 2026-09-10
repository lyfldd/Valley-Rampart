# QQQ.6 执行清单

> 配套文档：QQQ.6_AI弹话文案族化与丰富化.md
> 生成于 2026-09-10 · 签发载体=HH.153（策划端→执行端施工任务书）

## 任务总表

| 编号 | 任务 | 需求# | 类型 | 涉及文件 | 验收标准 | 状态 |
|------|------|-------|------|---------|---------|------|
| T1 | AttentionTuningConfig.cs 新增 `talkRaceChance` 字段（默认 0.4，Tooltip 注明"正常态抽族池概率，QQQ.6"）；**不进 ToSnapshot()**（表现层，sim 零义务）；AttentionTuningConfig.asset 同步落值 | 需求4 | 参数 | AttentionTuningConfig.cs/.asset | grep `talkRaceChance` 在 cs+asset 双在场；`ToSnapshot()` 无该字段；编译 0 error | ⬜ |
| T2 | `PickTalkLine()` 改为读自身 `raceId` 双层混合：正常态 `Random.value < talkRaceChance` → `GetRaceTalkPool(raceId)`，否则按状态走职业池；饥饿/受伤态 100% 职业状态池 | 需求1 | 架构 | UnitController.cs:1444-1450 | 代码直读：raceId 引用 + 双层分支；talkRaceChance 从 SO 读 | ⬜ |
| T3 | 新增 `GetRaceTalkPool(int raceId)`：4 族 × 8 句逐字录入（QQQ.6 §需求2），兜底 raceId 未知 → null（回退职业池） | 需求2 | 内容 | UnitController.cs | 4×8 句逐字与 QQQ.6 比对零差异；grep 每族首句在场 | ⬜ |
| T4 | `GetTalkPool` 十七职业文案全量替换为 QQQ.6 §需求3 定稿（含状态池扩充）；**战斗职业删除"随时准备战斗！"共用开场** | 需求3 | 内容 | UnitController.cs:1468-1594 | grep `随时准备战斗` 在 UnitController.cs 零命中；每职业正常池 ≥6 句 | ⬜ |
| T5 | 对话池提 static：`GetTalkPool`/`GetRaceTalkPool` 各池改 `static readonly` 一次分配（禁每次 new string[]） | 需求4 | 架构 | UnitController.cs | 代码直读：池为 static readonly；无逐次 new | ⬜ |
| T6 | 进局验证（正门）：EnterTestRun 建局 → 玩家王国 NPC（含各职业）点击对话/空闲弹话随机句正常显示，族池句按 raceId 出现；AI 王国 NPC raceId 非全兜底 Human 抽查（D467/Q10-M2 已回填，实测确认） | 需求1-3 | 验证 | — | 编译 0 error；弹话/上限/冷却行为无回归；族池句在场 | ⬜ |

## 跨需求依赖

| 任务 | 依赖 | 说明 |
|------|------|------|
| T2 | T1 | PickTalkLine 读 talkRaceChance 需 T1 先落字段 |
| T3/T4 | — | 文案录入与结构独立，可并行 |
| T6 | T1~T5 | 进局验证需全部施工完成 |

## 完整性校验

| 需求# | 文档章节 | 对应任务 | 状态 |
|-------|---------|---------|------|
| 需求1 | §需求1 双层池 | T2 | ✅ |
| 需求2 | §需求2 四族族池 | T3 | ✅ |
| 需求3 | §需求3 十七职业池 | T4 | ✅ |
| 需求4 | §需求4 性能与 sim 边界 | T1,T5,T6 | ✅ |
