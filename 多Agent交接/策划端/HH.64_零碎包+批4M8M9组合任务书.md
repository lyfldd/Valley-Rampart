# HH.64 零碎包+批4（M8+M9）组合任务书

- **策划端**：签发（2026-09-04）
- **执行端**：TraeCode（接收后按 §二 顺序执行）
- **依据**：2026-09-04 排期三原则（零碎任务优先/文档已大半优先/大文档靠后——先收割后开荒）；批3+冒烟自动批已验收（D521/D522），零碎包到期点已到
- **状态**：待执行端开工回执（回执后按序实施，每段完成可分段交付报告，也可合并 HH.65 一次性交付）

---

## 一、任务清单（三段，按序执行）

### 段A：D498 Worker 基线回调小批（先收割，约 0.5 人天）

**来源**：HH.53 §裁决 D498（Worker 基线缺口 A+回调合流 C）、2_20 冒烟头部「实盘缺口注」。

1. `WildnessConfig` SO 新增 `wildBaseAttack`（绝对基线字段，int，与 wildStrengthRatio 并列）
2. `TryGetWildCombatOverride` 下限兜底改为读该字段（现硬编码 attack≥1/range≥1/cd≥0.5 转正为可调初值——数值默认按现硬编码值原样转正，不调数值）
3. 2_20 冒烟头部实盘缺口注随批销注
4. 红线：AI.Core/训练仓零触碰（纯 Unity 侧 SO+消费点）；`wildBaseAttack` 入 factor_registry 草案义务归 sim 批（列报即可）

### 段B：Unity 会话零碎包（攒一次进局清四项）

| # | 项 | 动作 | 出处 |
|---|---|------|------|
| 1 | Smoke_12/夜灾系补跑 | **挂 SmokeApi 自动跑**（D522 红利：无需手动进局）——验证 D500 受击不追抑制回归面（驻守/移动单位受击不再追击） | D500 · HH.53 §裁决 |
| 2 | 2_16 P0 取证复跑 | 9 组合确定性取证，**专用容器纪律前置=禁活局**（HH.57 §五铁律）；P-A6 明细丢失随复跑重建 | D508 · HH.57 §五 |
| 3 | SaveSlots 删除按钮 | **需用户物理鼠标点一次**（虚拟设备不触发已知）——执行端铺好环境+说明点位，请用户配合 30 秒 | HH.49 §五-3/§六 |
| 4 | unity_scene 保存行为 | 下次 Unity 会话先验证团结引擎下 .scene 重复文件是否复发（已修复保 GUID） | HH.46 §三-3/4 |

> 段B 允许部分完成：#3 等用户配合、#4 等窗口，未完成项如实挂账续留，不阻塞段C。

### 段C：Q10 批4（M8+M9）本体批

**任务真源=2_20 实施清单 M8/M9 行+2_20 总纲 §六**：

- **M8（架构）AI 王国策略消费接入**：KingdomBrain 读 RaceDef 五轴基准 × KingdomDef 扰动（D426 合并逻辑）；策略倾向生效（好战度→军事优先级等）。验收=同族两 AI 王国行为观测有差异（扰动）+种族基准生效探针
- **M9（验证）共通 5 职业零改动验证**：Warrior/Archer/Mage/Healer/General 资产走查+四族共用冒烟（可挂 2_20B 六轮框架）+**Cavalry 负探针**（其余三族无骑兵训练条目——批3 已落 raceId=0，本批纯验证）

## 二、执行顺序与边界

```
段A（独立小批，可单独 commit）→ 段B（一次进局）→ 段C（批4 主体，HH.64 回执→实施→交付报告）
```

**段C 三点边界（开工回执须回应）**：

1. **口径锚定**：HH.61 尾句「M8 分权语义/M9 王国扩张」措辞与实施清单不一致——以实施清单+总纲 §六 为准；若总纲另有口径列待决策项交策划端
2. **防范围蔓延**：D515 邻接修正归 2_22 P0 清单不在本批；KingdomBrain 深度改造（态势层/建军链/姿态档）全是 2_22 P0 域——**M8 只挂五轴消费接线，禁动骨架**
3. **sim 义务评估**：M8 触碰 KingdomBrain（Unity 侧 Systems/AI，非 AI.Core 镜像区）——预期零 T 级，但回执须列评估（对照 sim-sync §六 分级）

**通用红线**：段A/段C 代码完成后 git diff 自查为交付前置；清单回执区写完 grep 在场性自查（HH.61 卫生指令）；共享文件写完即 commit 关窗。

## 三、验收标准

- 段A：wildBaseAttack 字段在场+冒烟下限探针走 SO 值+缺口注销注
- 段B：#1 Smoke_12 全 PASS 回归证据；#2 九组合取证产物；#3/#4 如实回执（完成或挂账）
- 段C：M8 同族双 AI 差异行为级探针+五轴生效正探针；M9 四族共用冒烟+Cavalry 负探针；编译 0 error；§十一.4/§十二.6 探针回归
- 批4 验收后批5（M10，依赖已解除 D505）解锁

## 四、流程

开工回执（HH.64 回写：段A 数值转正确认+段B 可行性+段C 口径锚定与 sim 评估）→ 实施 → 交付报告（段A/B 可并入 HH.65 或分段）→ 策划端验收 → commit 代执 → 批5 解锁。

执行端回执后开工，交付 HH.65 报告回来验收。届时段A/B/C 全清 + 批5（M10）解锁。策划端 2_22 P0 清单签发窗口我这边随时可启动——那是我下一件文档活。

---

## 五、执行端开工回执（2026-09-04，TraeCode）

- **状态**：✅ 已接单开工（三段一次接单，按 §二 顺序执行；HH.63 已交验、批3+冒烟自动批验收成立 D521/D522 前提确认）
- **本会话拟改文件声明**（单写者纪律）：`WildnessConfig.cs`/`.asset`、`NPCBrain.cs`（仅 TryGetWildCombatOverride 消费点）、`Valley2_17_Smoke_12.cs`、`KingdomFoundry.cs`（仅 personality 生成点）、冒烟容器（新建/改）、`SaveSlotPanel.cs`（如需探针日志）、2_20 实施清单 M8/M9 行回执区、本文件回执区、`_交接索引.md`

### 段A 数值转正确认

- `wildBaseAttack`（int，默认 **1**）=attack 绝对基线转正；**range≥1/cd≥0.5 两下限一并 SO 化**（`wildBaseRange` float 默认 1.0 / `wildBaseCd` float 默认 0.5）——依据 §段A.2 括号"现硬编码 attack≥1/range≥1/cd≥0.5 转正为可调初值"三项并提 + so-data-driven 铁律；数值默认全部原样转正**零行为变化**（Max 兜底公式镜像）。若策划端认为只应 attack 单字段，验收时驳回即可回退（range/cd 两字段属可拆增量）。
- 2_20 冒烟头部「实盘缺口注」随批销注（WildnessConfig.cs L45-47 + NPCBrain.cs L666-668 双处）。
- AI.Core/训练仓零触碰 ✓；`wildBaseAttack` 入 factor_registry = sim 批义务（HH.65 列报，不动训练仓）。

### 段B 可行性回执

| # | 可行性 | 方案 |
|---|---|---|
| 1 | ✅ 可行 | Smoke_12（领土接线）现为"先 Play 再点"半自动——改挂 SmokeApi：自动进局（真实开局链，替代其 EnsurePlayerRegistered 兜底）→跑 P1~P10→`SmokeApi.QuitSmoke`；D500 受击不追回归面载体实盘=2_20_Smoke_Race ②c（D486 位移<0.5 格判据，上批已挂 SmokeApi）→同会话一并复跑取回归证据 |
| 2 | ✅ 可行 | Valley2_16_SmokeVerify（含 ResetWorld+GenerateMapForPreview×18=禁活局正主）→专用会话自动跑（无用户活局时执行，铁律=HH.57 §五 审计通过后方可触发）；P-A6 明细重建=本轮完整逐组合 PASS/FAIL 落盘日志 |
| 3 | ✅ 环境可铺 | 删除按钮代码已在场（SaveSlotPanel L69-75 OnDeleteClicked）；卡点=UI Toolkit 动态按钮虚拟设备不触发（HH.49 §五-3）。铺法=主菜单 Play+SaveSlots 面板打开+日志探针就位，请用户物理鼠标点一次"删除"；用户不在/不配合→如实挂账 |
| 4 | ✅ 顺带 | 会话开始即验证 unity_scene 保存行为（.scene 重复文件是否复发，HH.46 §三-3/4）；不阻塞 |

### 段C 口径锚定与 sim 评估（三点边界逐条回应）

1. **口径锚定**：采纳实施清单 M8/M9 行+总纲 §六 为准（2_20.1 §四为数值/公式权威）。HH.61 尾句「M8 分权语义/M9 王国扩张」措辞与清单不符——**不据此扩展**，该句按任务书裁定视为笔误级措辞漂移。总纲 §六「策略倾向」行与清单 M8 口径一致（消费逻辑不变、只改五轴来源），**无另立口径 → 无待决策项**。
2. **实施点收敛（禁动骨架兑现）**：唯一改动点=`KingdomFoundry` 第一代立国 personality 生成（现 L58 `Perturb(tpl.GetPersonalityArray())` 模板终值）→ RaceDef 基准合并。**消费链零改动**（UtilityScorer L82-83 五轴线性乘入现成，2_20.1 §四"只改来源"）。动态立国 BlendPersonality 混合源=已含基准的来源国 personality，逻辑零改动自动继承。D515/态势层/建军链/姿态档全不碰。
   - **合并公式呈报**（文档未给显式公式，执行端按 D426"扰动在基准上偏离"语义取加性方案）：`final[i] = RaceDef.baseline[i] + (KingdomDef.axis[i] − 0.5) + rng(±firstGenPerturbation)`，第一代不 clamp（D474 勘定；消费侧 UtilityScorer 既有 Clamp01 保护）。**良性质**：人类族基准全 0.5 → 人类模板国 personality 与现状完全等价（零回归）；兽人铁蹄好战=0.80+0.35=1.15→消费 1.0（好战拉满，占位基准画像表语义）。若策划端另有乘性公式口径（"×"字面），验收时驳回切换成本单点。
3. **sim 义务评估（对照 sim-sync §六分级）**：M8 改 KingdomFoundry（Unity 侧 Systems/Kingdom，非 AI.Core 镜像区）+M9 纯验证——**预期零 T 级直改、零 F 级**：不触双端镜像文件、不扩 FactorContext/TuningSnapshot、不动 champion/harness。sim 侧关联义务=①"五轴来源改造→sim 王国生成器同构合并逻辑"（2_20.1 §五行在场，**随 HH.65 列报最终公式实值**供 sim 批对齐）②段A wildBaseAttack 入 factor_registry（归 sim 批）。15_差距账本登记随策划端/sim 批，本批不直改训练仓。
   - **M9 落点**：共通 5 职业资产走查（Warrior/Archer/Mage/Healer/General SO 三方值）+四族共用冒烟挂 2_20B 六轮框架+**Cavalry 负探针**（TrainingConfig.raceId 门禁：断言骑兵训练条目仅 raceId=0 人类在场、 Elf/Dwarf/Orc 过滤列表无骑兵——批3 已落，本批纯验证）。

### 下一步

按段A→段B→段C 顺序实施；段A 完成可单独 commit（待验收后执行）；交付 HH.65（段A/B/C 合并一次性）。

— 执行端 TraeCode · 2026-09-04
