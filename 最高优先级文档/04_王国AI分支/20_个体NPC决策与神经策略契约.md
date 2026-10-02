# 个体 NPC 决策与神经策略契约

> 日期：2026-09-28
> 状态：设计基线；未声明代码、模型权重或运行时推理已经接通。
> 归属：最高优先级文档；实现与训练归 `ai决策大脑强化训练/`，运行时接入归 `Assets/_Game/Systems/AI` 与 `AI.Core`。
> 本文件解决个体 NPC 的决策边界。王国级长期计划、王国生命周期和王国级打断仍以 `19_王国AI决策循环与打断契约.md` 为准。

## 一、结论先行

用户最初提出的“输入 → 神经网络 → 输出”方向可以保留，但不能让网络直接控制 Unity 对象、资源、任务或每帧位移。个体 NPC 采用**分层混合策略**：

```text
权威系统
  ↓
Canonical Observation（不可变、带版本）
  ↓
硬约束与 Action Mask
  ↓
候选动作/意图集合 ← 现有确定性 L1/L2/L3 基线
  ↓
NPC Policy（神经网络，只输出候选评分、置信度和价值）
  ↓
确定性仲裁 + Safety Shield
  ↓
Intent / BehaviorCommand
  ↓
BehaviorExecutor（每帧执行）
  ↓
Result / Event / DecisionTrace
```

网络是可替换的策略插件，不是生命周期管理器、任务系统、账本写入器或物理控制器。策略失败、模型缺失、输入版本不兼容或置信度不足时，系统必须回退到现有确定性基线。

## 二、当前真值与证据边界

| 项目 | 当前判断 | 证据或限制 |
|---|---|---|
| 个体决策管线 | **支持** | `ai决策大脑强化训练/01_决策大脑解剖.md` 记录 `NPCBrain.Think` 的 L1→L2→L3 管线；`NPCBrain.cs` 组装 `FactorContext`、计算 `L1FocusEvaluator`、`L2PostureDecider`、`L3CommandComputer`，再缓存命令给执行器。 |
| Think/Execute 分离 | **支持** | `NPCBrain` 按 LOD 降低 Think 频率，Execute 每帧复用上一条命令；任务让位期还会跳过普通移动执行。 |
| 训练侧共享决策核 | **支持（文档口径）** | `ai决策大脑强化训练/README.md` 与 `00_训练体系主文档.md` 记录 sim/harness 与 Unity 决策核的同源、确定性和门禁要求；当前仍需以具体 commit、构建和回放重新验收。 |
| Unity 神经网络推理 | **未观察到** | 当前 `Assets/_Game` 未观察到 ONNX、Sentis、Barracuda、ML-Agents 或推理适配器引用；因此本文件不把神经网络写成现成功能。 |
| 模型文件、输入顺序、输出协议 | **未取得** | 没有可引用的版本号、权重摘要、特征字典或输出枚举；接入前必须建立这些工件。 |
| 运行时在线学习 | **禁止** | 运行时只消费版本化模型和配置；权重变化必须来自离线训练与审查，避免存档、回放和平衡发生隐式漂移。 |

“支持”表示有当前源代码或现行文档证据；“未观察到”表示本次检索未发现，不等于证明全世界不存在；“未取得”表示不能据此做实现或性能结论。

## 三、为什么纯端到端输出不适合直接落地

| 风险 | 会发生什么 | 必须增加的边界 |
|---|---|---|
| 非法动作 | 网络输出无法到达的目标、无权限任务、资源不足或已死亡单位仍执行动作。 | Action Catalog、硬约束过滤和执行前 Validator。 |
| 安全响应不稳定 | 受击、灭亡、核心任务和路径失败需要即时、可解释的处理；模型可能延迟或给出低置信度结果。 | Survival/Combat/Lifecycle Safety Shield；高严重度事件可直接打断。 |
| 每帧抖动 | 网络每帧重新选动作会在追击、撤退、工作之间来回切换。 | 网络只在决策 Tick 评分；Intent 保持、迟滞、冷却和确定性排序；Execute 每帧只执行已接受命令。 |
| 部分可观测 | 当前输入不能表示最近受击、任务进度、失败原因和已知情报的时间。 | 显式记忆特征与 `IMemoryComponent`；先用可存档的显式记忆，暂不依赖无法回放的隐藏 RNN 状态。 |
| 模拟器投机 | 策略利用 sim 与 Unity 的差异，在训练分数上很好，真实场景却失败。 | 共享特征/动作 schema、golden replay、随机场景、独立 holdout 和 Unity 抽检。 |
| 黑盒难排查 | 只记录“网络选了 X”无法解释为什么选、哪些候选被排除或是否被安全层改写。 | `DecisionTrace` 记录特征版本、候选、mask 原因、logits/score、置信度、覆盖原因、模型版本和 seed。 |
| 分布外输入 | 新职业、地图、人口规模或 LOD 档位超出训练分布。 | 输入范围检查、未知标记、不确定性/置信度门、确定性基线回退和分布外指标。 |
| 资源与平台成本 | 每个 NPC 每帧推理会放大 CPU、内存和批处理开销。 | 与现有 LOD/think 分片绑定；只在 Think 阶段推理；批量推理是优化项，不改变语义。 |

因此需要修改的不是“是否使用神经网络”这一点，而是网络在系统中的**权限、时间粒度、动作空间、记忆和验收方式**。

## 四、运行时分层契约

### 4.1 Observation

`Observation` 是一次决策 Tick 的唯一只读输入，至少包含：

- 自身：身份、职业、种族、阵营/王国、位置、生命、装备、昼夜和 LOD 档位；
- 行为条件：勇气、服从、职业偏好、编队角色和已版本化的个体人格/性格修正；网络消费这些条件，不能偷偷把人格差异埋进不可解释的权重或临时状态。
- 感知：敌我列表、距离/方向、威胁、热点、编队槽位、任务焦点和路径状态；
- 记忆：最近受击、当前任务阶段、上次 Result、失败/阻塞原因、回家点和安全状态；
- 约束：可用动作、任务优先级、资源/工人预留、权限、期限和生命周期状态；
- 元数据：`observationId`、世界 Tick、版本、事件序号、随机种子和特征 schema 版本。

原始事实、派生因子和模型特征必须区分。未知值使用 `Unknown`/有效位表示，不能填成零；特征归一化必须有固定范围、缺失值规则和版本号。一个 Tick 内所有候选动作使用同一快照。

### 4.2 Action Catalog 与 PolicyOutput

第一阶段只允许离散候选动作，例如 `HoldPosition`、`MoveToFocus`、`Retreat`、`WorkAt`、`FollowFormation`、`AttackTarget`、`Heal`、`OperateMachine`。每个动作必须声明目标类型、前置条件、资源/任务占用、可中断性、完成条件和失败结果。

网络输出结构建议固定为：

```text
PolicyOutput {
    schemaVersion
    modelVersion
    actionScores[actionId]
    confidence
    valueEstimate
    suggestedTargetKey
    inferenceFlags
}
```

`actionScores` 是候选排序依据，不是对世界的写操作；`suggestedTargetKey` 只能引用 Observation 中已存在的目标；网络不能输出任意坐标、资源数量、任务对象引用或事件回调。

### 4.3 Action Mask 与 Safety Shield

Mask 分两层：

1. **静态/事实约束**：生命周期已死亡、权限不符、目标不存在、资源未预留、任务冲突、路径不可达、射程/弹药不满足时，候选直接排除。
2. **动态安全约束**：`Emergency`、`Terminal`、受击溯源、撤退阈值、核心建筑危险或任务让位期时，安全层可覆盖策略输出并选择安全动作。

安全层必须输出 `overrideReason`。覆盖率、非法动作率、无可行动作率和回退率是验收指标；具体阈值当前**未取得**，不能先写成调参结论。

### 4.4 Intent、Command、Result

- `Intent`：决策 Tick 产生的可追踪行动请求，带 `intentId`、父任务、目标、期限、优先级、模型/基线来源和确定性排序键。
- `BehaviorCommand`：执行器能理解的有限命令；由确定性适配器从 Intent 转换，不由模型直接构造副作用。
- `Result`：执行系统返回 `Accepted`、`InProgress`、`Completed`、`Blocked`、`Rejected`、`Failed`、`Interrupted` 或 `Expired`，带实际进度和原因。

NPC 的下一次 Observation 消费 Result。模型不会自行修改任务、库存、生命、外交或建筑状态。

## 五、推荐的个体 Tick

```text
1. 生命周期门：对象必须处于可决策阶段；死亡/销毁直接停止。
2. 组装 Observation：读取权威系统，写入固定 schema 和版本。
3. 记忆更新：运行显式记忆组件，带真实时间差和上次 Result。
4. 生成候选：沿用现有刺激、焦点、任务和执行能力生成候选集合。
5. 硬约束/Mask：排除非法、冲突、不可达和生命周期不允许的动作。
6. 基线评分：运行现有 L1/L2/L3，得到可解释的安全基线。
7. 策略评分：模型只对剩余候选给 score/confidence/value；模型异常则回退基线。
8. 仲裁：安全层覆盖高严重度动作；其余按策略分、保持奖励、迟滞和稳定键选一个。
9. Validator：再次检查目标、权限、预留和版本；失败返回 Result，不直接写状态。
10. Execute：每帧执行已接受命令；Think 不重复调用网络。
11. Trace：记录输入摘要、候选、mask、输出、覆盖、Result 和下一次唤醒建议。
```

现有频率作为初始运行契约：活跃区约 10Hz、半活跃区约 2Hz、休眠区约 0.5Hz；具体数值仍以 `LODSystem`/配置真源为准。网络推理跟随 Think，不能绕过 LOD 分片，也不能把日结算降级成“只算醒着的 NPC”。

## 六、状态与所有权

神经网络不取代对象生命周期。对象仍遵守仓库的四阶段：`OnBirth` 读取配置并建立依赖，`OnActivate` 开始接收/驱动，`OnDeactivate` 停止接收但可恢复，`OnDeath` 退订、注销、清理模型/记忆引用。复用对象必须在 OnDeath 清空策略状态、目标、Trace 缓冲和隐藏记忆。

运行时行为使用有限执行状态保存可恢复进度，例如 `Idle`、`Moving`、`Working`、`Attacking`、`Retreating`、`Following`、`Interrupted`、`Dead`。这些状态负责动作的进入、持续、暂停、完成和退出；策略只决定候选意图及其优先级，不能绕过状态出口。

## 七、训练与发布路线

### 7.1 阶段顺序

1. **契约阶段**：冻结 Observation、Action Catalog、Result、Trace、schema 版本和 fallback；不接真实网络。
2. **基线阶段**：用现有 L1/L2/L3 产生专家轨迹，建立随机场景、困难场景和独立 holdout；记录基线行为。
3. **候选评分阶段**：训练网络学习候选排序或动作价值，输出只接入 Shadow Mode；比较策略建议与基线，不改变行为。
4. **受限在线决策阶段**：只在通过 Mask/Safety/Validator 的候选中启用策略；按职业、LOD 和场景分批放量，保留一键全局回退。
5. **结构升级阶段**：只有当候选评分长期稳定且 Trace 可解释，才评估连续控制或递归策略；每次结构变更都重新跑双端门禁和 holdout。

### 7.2 训练纪律

- 训练端、Unity 适配端和回放工具共享特征/动作 schema；任何字段顺序变化都升 schema 版本。
- 使用随机地图、随机种子、职业和规模谱系；独立 holdout 不参与调参。
- 同 seed 对 Observation、PolicyOutput、Intent、Result、Event 顺序做逐字节或等价确定性校验。
- 训练只在离线 harness 发生；运行时不更新权重、不探索、不写训练样本。
- 每个模型发布包包含模型版本、schema 版本、权重摘要、配置快照、训练数据范围、评估报告和回退模型版本。
- 训练结果先进入 Shadow/回放报告，再进入 Unity；文档只能引用已保存的报告和 commit，不引用口头分数。

### 7.3 必须观察的指标

至少记录：生存率、任务完成率、目标切换次数、动作抖动、路径失败率、`Blocked/Rejected/Failed` 比例、Safety override 率、回退率、推理耗时、内存占用、同 seed 一致性和分布外输入比例。没有完整场景人口、回合数、有效样本数和排除规则时，指标只能标记为**未取得**。

## 八、需要修改的项目口径

1. 在最高优先级文档中把 L3 个体 AI 明确写成“训练轨归属 + 运行时安全契约”，不再让“输入→网络→输出”被理解成直接控制器。
2. 在训练轨新增 NPC Policy 的 schema、Action Catalog、Shadow Mode 和 Safety/Validator 验收项；现有 F 线卡先继续作为行为覆盖集，不能自动等同于神经网络已训练完成。
3. 在 `AI.Core` 增加纯数据协议和接口边界：`NpcObservation`、`NpcPolicyOutput`、`NpcActionCandidate`、`NpcDecisionTrace`、`INpcPolicy`、`INpcActionValidator`。先提供确定性基线适配器，再提供模型适配器。
4. `NPCBrain` 只负责装配快照、调用策略端口、接收 Result 和驱动执行器；网络适配器不得引用 `UnityEngine`、`EventBus` 或资源写入服务。
5. `harness/SimBrain` 与 Unity 壳使用相同候选动作和结果语义；sim 可以替换推理实现，但不能复制一套不同动作规则。
6. 为 `NPCBrain` 增加三类探针：非法动作被拒、Emergency 覆盖策略、模型缺失/版本不兼容回退；再增加同 seed 回放和存档恢复探针。
7. 只有在模型运行时、权重、schema、回退和门禁均有可核验工件后，才把本文件的状态从“设计基线”改为“已接线”或“已验证”。

## 九、当前不决项与停止条件

以下问题不能靠设计稿猜定，必须取得实验或用户裁决：

- 推理运行时和平台支持；当前**未取得**包、版本和性能基线。
- 网络训练方式（模仿学习、离线 RL 或混合）；当前**未解决**，先采用候选评分作为最小可逆路径。
- 是否需要递归网络；在显式记忆、存档和回放未闭合前，默认不引入隐藏状态。
- 候选动作全集、职业条件和目标键语义；需要从现有 `BehaviorModule`、任务层和交互层逐项登记。
- Safety override、回退率、推理耗时的通过线；需要以完整场景人口和观察窗口取得分母后再定。

停止条件：出现不可解释的生存率下降、非法动作、存档后策略状态漂移、同 seed 不一致、模型版本/特征 schema 不匹配、Safety 层无法覆盖终止事件，或策略只能靠不断提高回退率才能达到基线时，停止扩大模型权限，回到确定性基线和 Shadow Mode。

## 十、验收门

1. 每个决策 Tick 只有一个最终 Intent；所有候选、覆盖和回退都能在 Trace 中追溯。
2. 模型不能直接写世界状态；所有动作通过 Mask、Validator 和正式执行入口。
3. 死亡、生命周期终止、紧急威胁和任务冲突能在模型输出后被确定性拦截。
4. Think 降频只改变决策触发，不改变 Execute、日结算和世界事实。
5. 模型缺失、版本不匹配、推理异常和低置信度都能回退，并产生明确 Result/Trace。
6. Unity 与 harness 对同一 Observation、Action Catalog 和 seed 得到可重复的决策结果；差异必须进入差异账本。
7. 存档/读档后，显式记忆、活动 Intent、冷却和回退状态可恢复；没有未保存的隐藏策略状态。
8. 训练报告包含完整人口、回合数、有效/排除样本和独立 holdout；没有这些分母，不宣布“训练有效”。

## 十一、与现有文件的映射

| 契约 | 当前落点 | 下一步 |
|---|---|---|
| 观察和记忆 | `NPCBrain.cs`、`AI.Core/Memory/FactorContext.cs`、`Stimulus/` | 抽成版本化 `NpcObservation`，保留显式记忆来源。 |
| 基线评分 | `AI.Core/Decision/L1FocusEvaluator.cs`、`L2PostureDecider.cs`、`L3CommandComputer.cs` | 包装成 `INpcPolicy` 的确定性实现，行为先保持不变。 |
| 执行 | `BehaviorExecutor`、任务层、伤害层和交互层 | 只接受经过 Validator 的 `BehaviorCommand`。 |
| 训练 | `ai决策大脑强化训练/harness/Core`、`harness/Sim`、F 线卡 | 增加 Action Catalog/Trace/Shadow 记录和 holdout 指标。 |
| 王国级上层 | `19_王国AI决策循环与打断契约.md` | 王国 Brain 产生高层任务；个体 Policy 只在任务与安全边界内做局部选择。 |

本文件与现有代码之间的差异是待实施清单，不是完成声明。
