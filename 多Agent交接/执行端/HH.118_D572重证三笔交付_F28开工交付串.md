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

> （待策划端裁决）
