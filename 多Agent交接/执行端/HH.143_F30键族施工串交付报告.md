# HH.143 F30 键族施工串交付报告（训练师→策划端）

> **取号**：vr-id-ledger 读 `_编号登记.md`，140/141 王国AI 占用/预留、131~136 批占——取 **HH.143**（先登记后落盘）。
> **依据**：D596 三项通过（HH.142 §五）批准 §九 施工序；硬条款三项（15_账本三笔随第 1 步落账/L-02 补课/门禁与纪律）逐项兑现。
> **施工实录台账**：[cards/F30/decisions/D-001](../../ai决策大脑强化训练/cards/F30/decisions/D-001_键族施工与响应面测绘.md)；训练仓 commit 见文末。

---

## §一 施工面（§九 步骤 1~3，全部 build 过）

| 步 | 内容 | 状态 |
|---|---|---|
| 1 | TuningSnapshot **双份**加 iw* 五字段（harness+Unity；Unity=死字段注记消费者=阶段B IntentBiasConfig）+ SimConfig.DefaultTuning 默认 1f + Unity AttentionTuningConfig SO 默认值表/ToSnapshot 透传 | ✅ harness `dotnet build` 0 错；Unity `refresh_unity(compile=request)` 后 console **0 CS 错误** |
| 2 | SimFormation.DecideIntentWeighted **五处打分增量 × iw***（同分裁决序+advance 语义不动=D584② 修复成果保留） | ✅ build 0 错 |
| 3 | factor_registry 注册五键（group=`intentWeights`，域 0~2，consumers=SimFormation.DecideIntentWeighted+Unity 阶段B 注记，格式对齐 fb* 先例） | ✅ |
| 附 | 15_账本三笔（一·补二十：①D-3 清偿销号②Unity 死字段差距账新立③useIntentTable 全局切换裁决账新立） | ✅ 随第 1 步同步落账（硬条款兑现） |

**W-C1 缺字段机制工作正常**：champion json 无 iw* 五键 → 每次加载警告「缺字段 5 个 → 用代码默认值」→ 默认 1.0=数学恒等兜底 ✓（警告即证据；受控重注册=F30 收敛回灌时销）。

**自纠披露（教训复用）**：Unity AttentionTuningConfig 两处编辑并行提交曾致第二写覆盖第一写（CS0103×5），当轮发现串行补回后编译过——上串账本竞态同款，教训「同文件多处编辑必须串行」二次实证。

## §二 门禁①：行为级零变化——**PASS**

- **口径**：施工前基线（改码前先取证：champion+useIntentTable=false）vs 施工后（同配置+iw* 全 1.0 显式），同 seed 全卷对跑 `--battles 100 --suite v9`。
- **证据**：`runs/f30_gate1/{pre_useIntentTableFalse,post_iwAllOne}/report.json`（138 场景×100 局+holdout 4 场景）；比对脚本 `runs/f30_gate1/compare_gate1.ps1`（入 git 可复跑）。
- **结果**：**138/138 场景 + holdout 4/4 逐场景逐行为字段全等，0 diff**（intentStats=施工后新增仪器字段，白名单豁免）。
- 数学恒等论证（增量×1.0 逐位恒等）实盘成立——r6 同构先例复现。

## §三 门禁②：方向性抽查——4 键决定性 + iwRetreat 掩蔽上报

探针 @battles30（KF30 池，噪声无关=确定性种子逐位比对），全表见 D-001 §5：

| 键 | 探针 | 场景 | baseline → probed | 判定 |
|---|---|---|---|---|
| iwDefense | 1.5 | 2_1 | 0.522 → **0.002** | ✅ 响应陡（④放大吃掉 retreat） |
| iwSupport | 0.0 | 2_3 | 0.924 → **0.000** | ✅ 响应陡（=0 抑制：scoreCharge=0→⑤维持，行军塌缩） |
| iwSupport | 0.5 | 2_2 | 0.758 → **0.758**（逐位） | ✅ **独占域语义预测命中**：rule② 域（heat<engage）支援=唯一非零分项，中间值不改触发——设计稿 §四「0.5→支援触发↓」在现语义下不可达，**有效抑制点=iw=0**（此为设计稿勘正候选，报告如实列报） |
| iwSally | 1.5 | 2_4 | 0.158 → **0.450** | ✅ 响应最陡（Sally=1.5 翻转饱和域③④⑤同分裁决） |
| iwCharge | 1.5/0.5 | 2_4 | 0.158 → **0.064 / 0.450** | ✅ 双向响应 |
| iwRetreat | 1.5/0.0 | 2_1 | 0.522 → **0.522 / 0.522**（逐位） | ⚠️ **结构掩蔽**（§五） |

无死键、无方向反转（§四「停手回报」条款未触发）；iwRetreat 为掩蔽非死亡，详见 §五。

## §四 仪器与场景池（§九 步骤 5~7）

- **仪器四层**（不入 ObjectiveFunction）：SimFormation 意图时长计账 → SimWorld 聚合 → SimMetrics 汇总 → report per-scenario `intentStats{expectedHitRate,annotatedDwell,avgSwitches}`。expectedIntent=编队级标注（D-011 口径复用）；无标注=分母不含（豁免语义）。
- **KF30_2_1~6 场景池**（`gen-v9 --card F30 --kf30 6 --seed 20260910`，六文件反加载校验过）：baseline 命中率 **2_1=0.522 / 2_2=0.758 / 2_3=0.924 / 2_4=0.158 / 2_5=1.000 / 2_6=豁免（dwell=0）**；平滑度 avgSwitches 0~4.1。
- **F30 baseline 重建**：`runs/f30_baseline/` 全卷 @100（含 KF30 六景=144 场景）：**总分 0.417 / holdout 0.498**（对照 champion 基线 0.411=查表态；Δ+0.006=连续权重路径自身基线，非本串改动——useIntentTable=false 训练态基准确立）。

## §五 iwRetreat 掩蔽发现（列报，请策划端知情/裁决）

残编场景三重掩蔽（战日志逐行实锤，D-001 §6）：
1. **D567 收兵=残编 retreat 主供体**：VictoryConsolidation 经 SetIntent(Retreat) 供 retreat 意图，与 iwRetreat 无关（iwRetreat=0 下 t=20 Retreat 照发）；
2. **rule② 先手劫持**：敌出生 enter heat（0.2）=假热点 → 开局 rule② Charge 扑敌，rule① 无出场机会；
3. **近战接触 ~1s heat 饱和**：rule① 竞争带 heat∈(0.3,0.833) 行为窗 <1 决策拍，饱和后同分裁决序接管。

**训练含义**：iwRetreat 在残编族搜索梯度可能近零（retreat 行为被收兵供体主导）——若需 rule① 独立响应面（如收兵与 rule① 的供体解耦、假热点过滤），属结构/场景域=**策划域裁决项**，训练侧未擅自动。非死键：评分路径在场，轻度场景（远程低频命中）可能可分辨，本串三次场景旋钮未找到稳定考题（实录 D-001 §5/§6）。

## §六 快验制双探针（@100，±0.003 口径继承）与收敛判读

- probe-A=**iwSally=1.5**（响应最陡键的正向极值探针）；probe-B=**iwSupport=0.5**（场景级已证行为逐位零变化 → **预测总分 Δ=0.000=噪声带校准点**，校准 ±0.003 判读机器）。
- **判读（@100 全卷 144 场景，锚=F30 baseline 0.417）**：**probe-A Δ=0.000 / probe-B Δ=0.000**（holdout 双双 0.498 持平）——双双带内（±0.003）。判读语义：
  - probe-B Δ=0.000=**校准点确认**（场景级行为逐位全等 → 评分逐位不变，机器判读与门禁②证据自洽）；
  - probe-A Δ=0.000=**意图重分布在仪器面可见、在评分面不敏感**（KF30_2_4 intent dwell 0.158→0.45 大幅移动，但战斗 outcome 对 Sally/Charge 分配不敏感）——这正是 F30 卡指标=「意图命中率/平滑度」（00 §4.3）而非胜率的设计理由实证：**iw* 的训练压力源=intentStats（场景级仪器），ObjectiveFunction 对单键小步长不敏感**。
- **收敛判读**：五键响应面已测绘（§三）；iw* 权重搜索若以 ObjectiveFunction 为判据，单键小步长处于盲区（probe-A 实证）——**建议训练判据=intentStats 场景级命中率/平滑度（卡指标）+ 评分面作无退化门**（D519：指标归卡定义，此处训练自治范围，列报知悉）。iwRetreat 搜参策略待 §五 裁决。

## §七 L-02 补课销账（D596 责令兑现）

- T01 取证读数复跑：`probe-k --card T01 @30`（champion 口径）→ **winRate=0.978 / kdRatio=1.222**，与上串口头读数**逐位一致**（跨会话跨构建确定性旁证+1）。
- 载体：`results/probe-t01/report.json` 落盘（probe-k 新增 `--report` per-scenario 落盘机制=病根治本；后续任何卡取证跑批自动带 git 可验证报告）。**L-02 本笔销账**。

## §八 useIntentTable 全局切换列报（请裁）

- 现状：champion 单源 useIntentTable=true（查表档位），F30 训练全走卡内 patch（false+iw*），全局基线零触碰 ✓（15_账本 ③ 号义务在账）。
- **请裁事项**：F30 权重搜索收敛后，是否将全局 champion 切换至 false+iw* 终值（影响面=全卡行为基线+五池既有 baseline 口径；D519 分层=结构归策划域）。训练侧建议：待 iw* 收敛+holdout 无退化证据齐备后，与 2_21 阶段B IntentBiasConfig 施工节奏合并裁决（一次切换一份回归账）。
- 关联：Unity 侧 iw*=死字段（15_账本 ② 号义务），阶段B 施工时销账。

## §九 交付物清单与 commit

- **代码**：harness（TuningSnapshot/SimConfig/SimFormation/SimWorld/SimMetrics/SimReporter/ScenarioGenV2/Program.cs）+ Unity（TuningSnapshot/AttentionTuningConfig）；registry 五键；15_账本一·补二十。
- **场景**：harness/Cards/F30/scenarios/KF30_2_1~6.json（seed 20260910）。
- **证据**：runs/f30_gate1/（门禁①双报告+比对脚本）、runs/f30_gate2/（七探针 patch+KF30 基线报告）、runs/f30_baseline/、runs/f30_probes/（双探针）、results/probe-f30/report.json、results/probe-t01/report.json。
- **文档**：KEYS 设计稿状态行、F30 README/D-001、本信。
- **commit**：训练仓+主仓两笔（见信尾补记）。

---

## §十 策划裁决区（待回写）

1. **门禁① PASS 采信**（138+holdout 逐字段全等 0 diff）：
2. **门禁② 判读**：4 键决定性方向证据采信 / iwSupport「0.5 不改触发、0=抑制」独占域语义勘正采纳 / **iwRetreat 掩蔽发现处置**（训练搜参时跳过 or 保留观察 or 立结构议题）：
3. **useIntentTable 全局切换**：时点与口径（本报告 §八 建议=与阶段B 合并裁决）：

> 训练师 2026-09-10
