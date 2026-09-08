# HH.113 训练策略系统性信号：tuning 标量参数两卡六参数全零——F 战役训练策略调整请裁决（训练师→策划端）

> 类型：设计争议升级（D557 纪律触发）· 日期：2026-09-08 · 训练师会话（主指挥）
> 台账：cards/T01/decisions/D-008/D-009 + cards/T02/decisions/D-010/D-011/D-012/D-013；commit 链 8d171bd→f578c63→cfad28f

## 一、系统性信号

F 战役前两卡（T01 个体战斗/T02 角色分工）**六个 tuning 标量参数全量实证零效果**：

| 卡 | 参数面 | 实证 |
|---|---|---|
| T01 走位 | baseSafetyPull/stepRetreatCells/holdPositionIntensity/baseFollowCells | 温和 Δ0.000；maxprobe 极值 Δ+0.016@battles20 复核 Δ+0.001=噪声（D-009 判死） |
| T02 支援 | hotspotSupportIntensity/traceDecayTime | Δ+0.001@battles100 rejected（D-013） |

**结论**：v9 行为分布由结构/职业快照/构成主导，tuning 标量微调面普遍不敏感。若系统性成立，F 战役 31 卡中 tuning 微调类卡收益存疑。

## 二、本批附带产出（已闭环）

1. **分工键三源对齐**（D-010/D-012）：extract 真值→professions.json+champion 25 职×2 键（isHeavyArmor/isRanged），此前 champion 仅 Bedrock 独苗。
2. **gen-v9 考题口径修复**（D-011/D-012）：expectedIntent 注入对齐编队可答空间（单候选不注入）——「不可答题」根因修复，v9/卡池/holdout 全量重生成。
3. **baseline 重建**（D560 议题③报备）：12600 局新总分 0.42（旧 0.421 过期作废），holdout 0.507，证据入库。
4. 仪器补缺待办：report.json 场景级缺 supportArrivalRate 字段。

## 三、请求裁决

1. **训练策略转向**：a) tuning 微调类卡降级（快验一轮即收，不做多轮收敛）+F 战役聚焦构成/结构层卡；b) 或维持现策略逐卡穷尽。我倾向 a——两卡实证+T01 v1 时代同类结论互证。
2. **T02 收手**：参数轮已探（D-013），建议按 D-006 收手规则收卡进 T03；支援到场率 30.5% 是否达 v9 合理线请给口径（无 v1 对照基线）。
3. **T01 复开取证义务**（D560 前置一）我可在 T03 开工前补：v9 baseline T01 分档表+优势档行为流——请确认是否随下批产出。
4. **知悉**：T02 意图考核面归 T09 多候选池（gen-v9 口径修复后单候选不考）；9 野性键 F27 前重注册不变。
