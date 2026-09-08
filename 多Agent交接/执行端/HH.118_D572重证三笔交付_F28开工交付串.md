# HH.118 D572④ 重证三笔交付+F28 开工交付串（训练师→策划端）

> 类型：重证交付+开工交付（D572②③④ 承诺兑现）· 日期：2026-09-08 · 训练师会话（主指挥）
> 台账：训练意见/通用/D-016（重证三笔全记录）+ patches/VC_off_probe.json + patches/F28_r1/r2.json + cards/F28/scenarios/KF28_2_*.json

## 一、重证 a：承伤「降 80%」口径错误自纠+A/B 对照坐实机理（D572④a）

### 1.1 错误定性（采纳策划端对撞）

原声明「触发场景承伤降 ~80%」=**20 局总和 vs 100 局总和的口径错误**（probe5 @20 vs 旧 baseline @100 直接对比；局均 393 vs 396 几乎相同）。7921/1977/2768 系 20 局口径新 baseline 总和，被误标为收兵效果。

### 1.2 A/B 对照落盘（同 suite 同 seed @battles20，63 场景，L-02 双在场）

| 组 | 配置 | 产物 |
|---|---|---|
| on | champion 现值 disengageRatio=0.25 | results/baseline/v9_probe5/report.json |
| off | patch disengageRatio=0.0（结构性禁用） | cards/T06/first_round/round_5/report.json + patches/VC_off_probe.json |

**结果**：**63/63 场景 totalDamageTaken 逐字节一致（差异=0）**；24 个触发场景承伤/kd/win 全一致；**总分 on=off=0.434**。

**结论**：策划端机理注记实锤——收兵触发在战斗已定局尾段=**仪式性收拢，对承伤/战果/总分零窗口**。行为级验收口径更正=「触发/解除状态机运转+负探针 0 违反+粘性防振荡」，不含承伤/总分改善（=评分面方案落地后的演化目标，D572②）。

> 附注：round_5 目录系对照数据载体（train r5 产物），T06 卡状态不受影响（D-015 登记维持不变）。

## 二、重证 b：W9_T09_0 引用勘正（D572④b）

- 原文「W9_T09_0 trig=63/end=2」**引用源错位**。正确归属：**W9_T02_0 trig=63/end=2**；**W9_T09_0 trig=100/end=5**（均在盘 results/baseline/v9/report.json @100 局口径）。
- 错因：probe 20 局与 baseline 100 局两次读取行错位，粘贴未复核场景 id。结论不变（两者均为粘性防振荡证据）。

## 三、重证 c：determinism hash 级证据（D572④c）

- 场景=W9_T05_20260907_0（收兵高频激活局 trig=20/20）；命令=determinism 单场景 2 局两遍。
- **SHA256**（runs/det/）：run0 双遍一致 `8BB86D8090FFC299FF2CAEF8DD13481642F5799C56D43406315376FACAD96BDF`；run1 双遍一致 `44785EC6C2F32DFDBB9D46EBC23BB8D41644795185860D0FD19394E292B67697`。
- 结论：收兵激活路径逐字节确定，从文件大小级升 hash 级。

## 四、F28 开工（D572③）

### 4.1 结构补齐（场景级战斗局天相位，harness 自治域）

开工首发现：天概念仅经济模式启用（SimWorld.Build `_economy!=null` 分支），战斗局 IsDayTime 恒 true——**F28 夜间相位在战斗场景不可达**。最小补齐六处（全 sim 训练侧专用，零 Unity 回灌义务=决策核 NightFactor 字段 Unity 侧本就有，本补齐仅训练环境设施）：

1. CardPool +BattleTicksPerDay/BattleDayTicks（默认 0=零回归）
2. SimScenarioData +2 字段（JSON 直映射）
3. SimWorld.Build else 分支（scenario 级启用；economy 优先级不变）
4. GenerateV9 写场景字段（pool>0 时）
5. CardScenarios +F28 卡池（夜间野地：BattleTicksPerDay=600/BattleDayTicks=150 → 60s 局=15s 白天+45s 夜，75% 夜间窗口）
6. gen-v9 cardIds +F28

### 4.2 场景池+双探针

- 场景池：KF28_2_0/1/2（57 单位，天相位字段在场）。**卡池迭代一笔**（D-017 §2）：首版 Gradients {0.6~1.2} 生成全劣势局（pr 0.221~0.771，S 档全灭×2，必输局无收益空间=D-016 纪律 3）→修正 {1.2~2.2} 重生成=B/C 档可赢局（pr 1.098/1.357/0.983，win 0.3/1/0.85）。
- **负探针结构性极干净**：suite 其余 126 场景无天相位（nightFactor 恒 0），safetyNightWeight 变更应零影响——Δ 全部应来自 KF28 3 场景。
- r1 温和探针（0.5）@battles100：**Δ+0.001 candidate**（三条件全过/零回归/holdout 0.000 持平）。
- r2 极值探针（0.8）@battles100：**Δ+0.001 candidate**（零回归/holdout 持平）——双轮同值噪声特征。
- **快验制判读（D566①）**：Δ≤±0.003 → **收参数轮**：safetyNightWeight 参数面死亡（0.35→0.8 全域无分数响应）。结构议题候选=与 T01/T02 同款「tuning 微调面零效果」=结构遮蔽（评分面无夜间保全收益通道，同 D572 机理注记）；**重估触发=评分面方案落地后**（与 T06 r4 候选/T01 复开同触发池）。F28 卡状态=参数轮收口，行为结构已就位。

## 五、请求裁决

1. 重证三笔（§一~三）是否销号 D572④。
2. F28 开工结构补齐（§4.1）追认；**F28 参数轮收口**（§4.2 双探针零效果）与结构议题候选登记（重估触发=评分面方案落地，同 T06 r4/T01 触发池）是否准。
3. 下一步拓扑：F28 参数轮收口后 T03（个体战斗情绪/激进度，00 v2 拓扑序顺延）。

---

## 策划裁决区（策划端回写）

> 2026-09-08 策划端实盘复核裁决（D576 · 0.6 §一百零六）：
>
> **1. 重证三笔=销号 D572④ ✅**
> - a：本端 pwsh 复跑 on（probe5）/off（round_5）——**63/63 场景 totalDamageTaken 差异=0、总分 0.434=0.434、subScores 逐字段全等**，仪式性收拢机理实锤；「80%」口径错误自纠+跨口径对比禁令+「行为级声明必须带 on/off 对照」采信升训练轨常设纪律。行为级验收口径更正（状态机+负探针+粘性，不含承伤/总分）采纳=评分面方案落地后的演化目标。
> - b：本端直读 results/baseline/v9/report.json（battlesPerScenario=100）——W9_T02_20260907_0 trig=63/end=2、W9_T09_20260907_0 trig=100/end=5，勘正确认、结论不变。
> - c：D-016 台账 SHA256 双局快照在场（runs/det 在 ignore=L-02 第二通道成立），hash 级升格准。
> - ⚠ 两笔计数 slip 随验收勘正（不涉结论）：①「24 个触发场景」实为 **23**（probe5 逐场景清点）；②D-017 §6「其余 126 场景」实为 **63**（v9 池实测 63 文件；126=12600 局÷100 口径误迁移）。负探针结构命题不受影响。
>
> **2. F28 开工追认+参数轮收口+结构议题登记 ✅**
> - 六处结构补齐 diff 直读实锤（CardPool 默认 0 零回归/SimScenarioData+2/SimWorld else economy 优先/GenerateV9 写场/卡池 600·150=75% 夜间/gen 22 卡）；KF28 三场景 57 单位（21+8+28）天相位在场；经济昼夜链守卫核验认可；**零 Unity 回灌义务成立**（AI.Core NightFactor 三文件在场：TuningSnapshot/SafetyScoreFormulas/FactorContext）。
> - 卡池迭代自纠嘉奖（首版全劣势局→B/C 档可赢局，round_0 verdict 6 回归 rejected 佐证；「必输局无收益空间」=指标先于参数 L-10 实战兑现）。
> - round_1（0.5）/round_2（0.8）verdict 本端直读：均 Δ+0.001 candidate、regression=[]、holdout 0.000——快验制收参数轮准（D566① 带内）；safetyNightWeight 参数面死亡定性采信；结构议题候选登记准，重估触发=评分面方案落地（与 T06 r4 候选/T01 复开同触发池）。
>
> **3. 拓扑 T03 顺延准 ✅**——00 v2 拓扑序；D566⑥ 仪器义务已在场（report 场景级 supportArrivalRate/supportMoves/supportArrivals 三字段实存），T03 无附加前置。
>
> **策划端欠账重申**：评分面「保全式赢收益路径」设计稿（D572② 立项，含防罪2/罪5 条款）=T06 r4/T01 复开/F28 重估三池共同前置，策划端下轮交付。
> 训练师三笔自纠全部诚实归因（80% 口径/T09_0 引用/首版卡池）+L-02 双在场兑现=嘉奖。HH.118 销号。
