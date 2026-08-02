# AI 决策大脑强化训练 · 总览

> 日期：2026-08-02（大脑 3.0.1_8 重构完成后建）
> 状态：工作流文档集 v1（M0 前置冻结已完成）
> 目标：把"改因子 → 开 Unity → 手摆 → 肉眼看"的**天级迭代**，换成"改配置 → 跑模拟器 → 看报告"的**分钟级迭代**；并让外部 AI（opencode + DeepSeek）作为训练师自动提案。

---

## 一、管线总览

```
            决策核（纯 C#，同一份源文件，两端编译）
           ┌────────────────┴────────────────┐
     Unity 适配器（壳）               Headless 控制台模拟器
     NPCBrain/BehaviorExecutor        点单位 · 直线弓箭 · tick 制
     参赛版照跑，只做验证                    │ 批量跑局
                                          ▼
                                  指标 + JSONL 战报
                                   ┌──────┴──────┐
                            本地黑盒调参      LLM 训练师（opencode+DeepSeek）
                            (随机搜索→CMA-ES)  (读报告→提改参假设)
                                   └──────┬──────┘
                                          ▼
                                   参数配置 JSON → 回灌 Unity 抽检
```

## 二、三条铁律（全程不可违背）

1. **决策核只有一份源码**。Unity 与模拟器编译同一批 .cs，禁止复制粘贴出"模拟器版"——双写必然漂移。
2. **同卷考试**。所有版本在同一套基准场景（固定剧本+固定种子）上跑分，分数才可比较。
3. **LLM 不进 tick 循环**。训练师只在元层读报告、提提案；跑局、打分、淘汰全部本地 harness 做。

## 三、文档地图

| 文档 | 内容 | 什么时候读 |
|------|------|-----------|
| `01_决策大脑解剖.md` | 新大脑（3.0.1_8 后）完整解剖：管线/五综合因子/全部参数/函数清单/死参数 | 动手前必读；训练师的知识底稿 |
| `02_可迭代性分级与AI修改边界.md` | T1 参数/T2 公式/T3 结构三级迭代手段；AI 能改什么、禁改什么 | 设计提案机制前读 |
| `03_大脑提取与双适配工程.md` | 脱 UnityEngine 的 5 类接缝与解法、共享编译布局、迁移步骤 | M1 抽核时照做 |
| `04_模拟器规格.md` | 1D 世界模型、保真度契约、场景套件、日志格式、确定性 | M2 写模拟器时照做 |
| `05_训练师协议_opencode_DeepSeek.md` | DeepSeek 训练师工作循环、提案 schema、harness 命令契约、opencode 配置 | M5 接 AI 时照做 |
| `06_执行计划与验收.md` | M0-M6 里程碑，每步验收标准与工时 | 每天开工看一眼 |
| `07_训练师启动指南.md` | 启动 opencode + 和训练师对话的实操手册（话术模板/常见报错/日常节奏） | 要叫训练师干活时照抄 |
| `AGENTS.md` | 给 opencode 读的训练师行为准则 | opencode 自动加载 |
| `schemas/` | 因子注册表/提案/战报 三个 JSON 规范与示例 | 写 harness 与提案时对照 |

## 四、MVP 最小可跑路径（先跑起来，后续再优化）

> 原则：每一里程碑都有可验证产出，任何一步停下都不浪费。

```
M1 抽核双编译（2-3天）      → Unity 行为不变 + 控制台能 new 出决策核
M2 模拟器 v0（1-2天）       → 固定剧本跑通出 JSONL
M3 基准套件+确定性（0.5-1天）→ 同配置跑两次结果完全一致
M4 手动调参闭环（0.5-1天）   → 人改 1 个参数走完全流程 <10 分钟
── 到这里，"几天一版"已变成"几分钟一版"，AI 训练师是加速器不是前提 ──
M5 DeepSeek 训练师（1天）    → AI 独立完成 3 轮提案并留档
M6+ 优化项                  → CMA-ES / 公式变体市场 / holdout 防过拟合
```

## 五、当前状态

- ✅ **M0 前置冻结已完成**（2026-08-02）：3.0.1_8 全因子分层落地（Threat/Formation/Safety/AbandonTask/Work 五综合因子 + L2 连续仲裁）、HomePoint 分阵营、敌方军队（FormationBrain+编队）、E 键作战面板。战斗规则进入稳定期。
- ✅ **M1 抽核双编译已完成**（2026-08-02，ebad4c5）：AI.Core 纯 C# 决策核（30 文件，Shim/Ports/快照/L1-L3/Attention/记忆/Formation 纯函数）+ 壳适配（IUnitHandle/IWorldQuery 注入）+ harness.csproj 链接同源 + smoke test 通过。Unity 编译 0 错误、`grep -r UnityEngine AI.Core` 零代码引用。
- ✅ **M2 模拟器 v0 已完成**（2026-08-02，7936184）：harness/Sim 全套（SimWorld 8 步 tick 循环/SimDamage 时间轮/SimBrain Think 管线/SimFormation 编队/SimHeat/SimGrid/SimMetrics/SimLogger）+ S1/S2 场景 JSON + CLI（acceptance/determinism/smoke）。验收：S1 胜率 52%（∈45-55%）/ S2 弓手被贴身 0s 存活率 100% / 确定性逐字节一致。
- ✅ **M3 基准套件+确定性已完成**（2026-08-02，ed4fb89）：S3-S6 场景 JSON + 3 patch（SimPatchLoader 反射部分覆盖）+ ObjectiveFunction 目标函数 v0 + SimReporter 报告聚合（report.json 对齐 schema）+ suite/differentiation/determinism-all 子命令。验收：确定性全剧本逐字节一致 / 区分度行为指标显著敏感（S2 kdRatio 2.97→1.92、S3 全灭率 2%→94%）/ S2 趋势一致（弓手白嫖=阵型保护有效）。已知限制：S2 被贴身恒 0（正面案例，负面场景留 M4）、S4 无撤退触发（场景调参留 M4）、S6 依赖 v1。
- ✅ **M4 手动调参闭环已完成**（2026-08-02，622ef1b）：SimChampion（champion 全量快照导出/加载）+ SimVerdict（冠军裁决：总分 Δ + 场景退化 >5% 红线 + 双条件 candidate/rejected）+ benchmark/champion 子命令（对齐 05 §四 CLI 契约：--config/--patch/--battles/--out → report.json + verdict.json）。验收：改 1 参数 → patch JSON → 跑分 → 看报告 → 留/弃，全流程秒级完成（champion 加载=默认配置 0.545 逐分一致；rfdist20 Δ+0.001 判 candidate；undeadfast S1 退化 -10% 判 rejected + 退化标记）。
- ✅ **M5 DeepSeek 训练师已完成**（2026-08-02，b0fa8be）：factor_registry.example.json 全量化 v1.0（tuning ~70 字段 + 职业字段，min/max/语义/harness=false 标记）+ SimProposalValidator 提案校验（≤3改动/注册/边界/死参数/冻结/Σ软提示）+ propose 子命令（validate/run/list）+ opencode.json + proposals/history.log。验收：3 轮提案闭环（p_0001 rejected Δ=0 敏感性教训 / p_0002 p_0004 拒收路径 / p_0005 candidate Δ=+0.001 无退化）+ history.log 复盘引用 verdict 数据。遗留：opencode+DeepSeek 接入需 DEEPSEEK_API_KEY（运营项）。
- ✅ **M6+ 优化项全部落地**（2026-08-02，9badcca）：holdout（H1/H2 隐藏场景 + verdict 冠军三条件防背题）+ CMA-ES（SimCMAES 确定性搜索 + search 子命令）+ 公式变体市场（IThreatFormula/注册表/formula compare T2 守门 + DistSquaredV1 示例）+ FormationBrain v1（SimFormation 自动意图接 SimHeat，S6 已切 v1）+ 敌我阵型差异化（场景 JSON 每编队独立 slots）。验收：确定性全剧本逐字节一致 / S6 v1 subScore 0.771=v0 持平 / DistSquaredV1 Δ+0.002 过守门 / p_0005 三条件 candidate。
- ✅ **M0-M6 全部完成**——AI 强化训练管线（抽核→模拟→基准→调参→训练师→防过拟合）完整闭环。

## 六、与 3.0.1_7 的关系

本文档集是 `河谷防线开发计划书具体内容/3.0.1_7_离线AI训练与分层强化学习方向.md` 的**实施层细化与修订**：
- 取代其 P4 的 Python/gym 方案 → 纯 C# harness，无 Python、无 IPC 桥
- 动作空间定案修订：赛期内只做 A（参数调优）+ B 的思想（军队层意图脚本化），C 端到端 FuN 留赛后
- P0-P3（抽核/模拟器/保真）在 03/04 中展开为可执行清单

## 七、术语

| 术语 | 含义 |
|------|------|
| 决策核 | L1/L2/L3 + 综合因子计算 + 撤退/威胁公式 + 记忆组件 + 刺激体系（纯 C# 部分） |
| 壳 | NPCBrain/FormationController/BehaviorExecutor 等 MonoBehaviour 适配层，留 Unity 侧 |
| harness | 控制台训练程序：加载场景套件+配置→跑局→出报告→执行提案 |
| 训练师 | opencode + DeepSeek，读战报提改参建议的外部 AI |
| 基准套件 | 固定剧本+固定种子的场景集合，所有版本同卷考试 |
| 死参数 | 代码里存在但已不被消费的残留字段，调了没效果（见 01 §八） |
