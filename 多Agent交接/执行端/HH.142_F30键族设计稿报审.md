# HH.142 F30 键族设计稿报审（训练师→策划端）

> 依据：D595③（F30 键族设计稿开工准——键族=结构=策划域，出稿报轻量过目再施工）。
> 设计稿全文：[cards/F30/KEYS_键族设计稿.md](../../ai决策大脑强化训练/cards/F30/KEYS_键族设计稿.md)（§一~§九）。本信=过目摘要。
> 取号：跳过 HH.140/141（王国AI 批C/批D 占用/预留），账本已登记。

## §一 键族清单（iw* 五键，默认 1.0，域 0~2）

对 `DecideIntentWeighted`（T05 交付的连续权重路径）四意图**打分增量乘子化**——门限结构（fb* 九键）零触碰，只参数化权重维度（D487 意图=权重）：

| 键 | 乘子作用点 | 战术语义 |
|---|---|---|
| `iwRetreat` | `scoreRetreat += (1-survival)×iw` | 残编撤退倾向 |
| `iwSupport` | `scoreCharge += 1×iw`（rule②） | 远程支援倾向（救火） |
| `iwCharge` | `scoreCharge += heat×iw`（rule③） | 高价值冲锋倾向（乘胜） |
| `iwSally` | `scoreSally += wallHpRatio×iw` | 出城迎战倾向 |
| `iwDefense` | `scoreDefense += heat×iw` | 防守坚守倾向 |

关键设计点三笔：
1. **默认 1.0=数学恒等**（增量×1.0=原增量）→ 门禁① 行为零变化有代码级保证（r6 先例同构）。
2. **② ③ 分键**：支援=无条件绝对优先语义（T05 W9_T18_2 结构对齐成果），冲锋=门限内条件触发——分开权重独立调节救火/乘胜两种冲锋。
3. **同分裁决序+advance 语义不动**（D584② r4 死分支修复成果保留）。

搜参域=iw* 五键 only；fb* 门限+chargeValueGate/sally* 九键全部冻结 champion 现值（T05 已收口）。T05 先验承接：#1 节流-节奏非单调→冻结 1.0；#2 engage 饱和平台→冻结 0.3。

## §二 引入面（五处，全部微改）

1. harness `TuningSnapshot.cs` 加五 float 字段（默认 1.0）；
2. **Unity `AI.Core/TuningSnapshot.cs` 同五字段**（双份同源红线）——Unity 侧暂为死字段（注记消费者=阶段B IntentBiasConfig）；
3. `SimFormation.DecideIntentWeighted` 五处乘子化；
4. `factor_registry.example.json` 注册五键（group=intentWeights，fb* 先例格式）；
5. champion 单源不动——F30 训练用卡内 patch（useIntentTable=false+iw*），**全局切换 useIntentTable=收敛后列报策划端裁决**（影响全卡基线，D519 分层）。

Unity 侧连续权重路径本体不随本批施工（2_21 阶段B IntentBiasConfig 壳时再落）——差距入 15_账本。

## §三 双门禁+仪器+场景池（施工后）

- 门禁①：五键全 1.0 vs 施工前 useIntentTable=false 同配置——126 场景行为字段逐字段全等；
- 门禁②：方向性抽查（iwDefense↑→Defense 时长↑；iwRetreat↑→残编撤退↑；iwSupport↓→支援触发↓）；
- 仪器：intent timeline→场景级 intentStats（命中率=实际主意图 vs 场景 `expectedIntent` 标注；平滑度=每局切换次数）——**不入 ObjectiveFunction**（评分面 D591 刚收口零触碰）；
- 罪1 自查变现路径：iw 调优→意图适配场景→胜率/战损/残存改善→**现五档公式变现**；
- 场景池：KF30_2_1~6 六场景草案（撤退/支援/冲锋/出城/防守/竞争边界，带 expectedIntent 标注，v9 生成器产出）；
- F30 baseline=v9 全套+useIntentTable=false+iw* 全 1.0 重建→iw* 双探针跑批（快验制，噪声带 ±0.003 口径继承）。

## §四 15_账本义务（随施工登记）

| 义务 | 清偿时点 |
|---|---|
| D-3（IntentBias 权重入 champion/factor_registry，2_21 §六.1） | 键族落地施工时 |
| Unity 死字段差距账（五字段暂无消费者） | 落地登记；阶段B 施工销 |
| useIntentTable 全局切换裁决账 | F30 收敛交付时列报 |

## §五 请策划端轻量过目

**过目范围**（D595③ 条款：键族清单+引入面+15_账本义务）：
1. §一 键族清单——五键拆分口径（②③ 分键）与域 0~2 是否认可；
2. §二 引入面——Unity 死字段同源方案 vs 只加 sim 侧，是否认前者的双份同源保真；
3. §四 15_账本义务三笔登记口径。

**裁决区**（策划端过目，D596，2026-09-10）——**三项过目通过，批准 §九 施工序**：

- 裁决1（键族清单）：**通过**。五键 iwRetreat/iwSupport/iwCharge/iwSally/iwDefense 与 DecideIntentWeighted 加权路径五增量逐字对锚（实读 SimFormation.cs L507~550：①`1f-survival`｜②恒 `1f`｜③`heat`｜③.5`wallHpRatio`｜④`heat`）；②③ 分键语义正交成立（②=无条件优先「救火」、③=门限内条件触发「乘胜」）；默认 1.0=数学恒等=门禁①代码级保证（r6 同构）；门限九键全冻结 champion 现值（T05 收口尊重）+#1/#2 先验承接冻结 1.0/0.3 不再探（响应面结论落看守）；同分裁决序+advance 语义不动（D584② r4 成果保留）。
- 裁决2（引入面）：**通过**。五处引入面边界清晰：harness TuningSnapshot 加五字段＋**Unity TuningSnapshot 同五字段（双份同源先例实锤：fb* 六键已在 Unity L187~192 在场；useIntentTable 未入 Unity=Unity 仅查表路径属实，死字段注记=2_21 阶段B IntentBiasConfig 接管）**＋SimFormation 五处乘子化＋factor_registry 注册五键（group=intentWeights）＋champion 单源不动（useIntentTable 全局切换=收敛后列报请裁，D519 分层正确）。评分公式零触碰（D591 收口尊重）。认可 Unity 死字段同源方案（sim-sync 红线，双份同源优于仅 sim 侧）。**施工未先行确认**：SimFormation.cs 内容级零 diff 实锤。
- 裁决3（义务登记）：**通过（钉时点）**。15_账本三笔（D-3 清偿｜Unity 死字段差距账｜useIntentTable 全局切换裁决账）登记口径认可，清偿时点=施工时正确；当前账本尾部=一·补十九（HH.140 批D 并行轨产物），三笔未落=按稿未到时点**非漏登**——施工串需同步落账（硬条款，随 §九 第1步 TuningSnapshot 双份加字段时一并登记）。

**T06 复核（报备性质）**：**通过**。KT06_2_S0~S5 六场景在场＋D-016 台账在场＋GenerateSupportV9（ScenarioGenV2.cs L809）实锚；支援到场率 80.1%（823/1055）vs 旧池 5-11%=承载面补全实锤；三边界逐条遵守（SimFormation 零 diff=卡内口径/评分面/132 全集零触碰）。复活判据「同 patch 重跑判读」待下轮 ✓。

**T01 复核（取证性质）**：**通过**。卡池 KT01_2_0~2 同 seed 重生成零 diff=确定性旁证 ✓；winRate 0.978/kd 1.222 基线读数采信。**补课责令（L-02 口径）**：取证读数无 git 可验证载体（results/probe-t01 均 .gitignore 的 jsonl、无 report.json）——下串补 report.json 落盘或台账快照在场后销；不阻塞本次过目。

**教训核查（钩子3）**：无新增。T01 取证读数未入库=L-02 家族轻微案（执行端证据入库惯性），处置=补课责令随下串销，非新策划流程漏洞、不入教训库。

过目通过即按设计稿 §九 施工序执行（TuningSnapshot 双份→乘子化→registry→双门禁→仪器→场景池→baseline→iw* 探针训练），训练自治推进，交付=收敛判读 HH 报告。
