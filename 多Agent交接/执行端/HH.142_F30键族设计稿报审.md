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

**裁决区**（待回写）：

- 裁决1（键族清单）：
- 裁决2（引入面）：
- 裁决3（义务登记）：

过目通过即按设计稿 §九 施工序执行（TuningSnapshot 双份→乘子化→registry→双门禁→仪器→场景池→baseline→iw* 探针训练），训练自治推进，交付=收敛判读 HH 报告。
