# 05 训练师协议（opencode + DeepSeek）

> 角色定义：DeepSeek（经 opencode）= **训练师**——读战报、提改参假设、解释学歪原因。
> 它不是：实时玩家（不进 tick 循环）、裁判（打分归 harness）、最终决策者（冠军替换归人）。

---

## 一、三方分工

| 角色 | 谁 | 职责 |
|------|-----|------|
| 训练师 | opencode + DeepSeek | 分析战报 → 写提案（JSON）→ 复盘提案结果 |
| 裁判 | harness（本地控制台） | 执行提案 → 跑基准 → 出裁决报告 |
| 总监 | 阿铁 | 批准冠军替换、T2 公式合并、方向决策 |

## 二、七步工作循环

```
1. harness 跑基准（champion 配置 × 场景套件 × N 局）→ results/baseline/
2. 训练师读：benchmark_report.json + factor_registry.json + proposals/history.log
3. 训练师写提案：proposals/p_{n}.json（≤3 个参数改动 + 每项理由 + 引用日志证据）
4. harness 执行：tuning.champion + patch → 跑套件 → results/{ts}_p{n}/report.json + verdict
5. 裁决：score 优于 champion 且 holdout 不退 → 标记 candidate；否则归档 rejected
6. 训练师复盘：读 verdict + 自己上一个提案 → 写复盘进 history.log（哪些假设错了）
7. 人抽检：candidate 累积 3-5 个时，人挑一个替换 champion（或批量替换）
```

## 三、提案 JSON 契约（schemas/tune_proposal.schema.json）

```json
{
  "id": "p_0042",
  "base": "champion@2026-08-05",
  "hypothesis": "弓手被贴身时间过长源于编队抵抗过低，切阵型6.0也压不住0.6+威胁",
  "evidence": ["results/baseline/S2/report.json: 弓手被贴身率 41%", "S2/tick日志: 威胁0.5-0.7时编队抵抗仅0.28"],
  "changes": [
    {"path": "tuning.formationResistScale", "from": 0.4, "to": 0.5, "rationale": "抬编队抵抗，让中威胁下军令更硬"},
    {"path": "tuning.rfDistWeight", "from": 0.35, "to": 0.30, "rationale": "降距离敏感度，防弓手过早恐慌"}
  ],
  "expected": {"弓手被贴身率": "41% → <30%", "S2胜率": "不退化 >2%"},
  "risk": "编队过硬可能导致该撤不撤，关注 S4 战损比"
}
```

**规则**：`changes` ≤3 项；每项必须在 factor_registry 边界内；`evidence` 必须引用真实文件（防空谈）；死参数直接拒收（harness 校验）。

## 四、harness CLI 契约

```bash
# 跑基准（训练师和人共用同一条命令）
dotnet run --project harness -- benchmark \
  --config champion/tuning.champion.json [--patch proposals/p_0042.json] \
  --suite Scenarios/suite_v1 --battles 200 --seed 42 \
  --out results/{自动时间戳}/

# 输出（固定三件套）
report.json        # 场景×指标矩阵 + 总分（schemas/benchmark_report.example.json）
events/*.jsonl     # 每场战斗事件流
verdict.json       # 与 champion 对比：score delta / 各场景退化标记 / holdout 结果
```

训练师通过 opencode 的 bash 能力直接执行此命令——**这是它能动手的地方；提案之外它只读不写**。

## 五、opencode 配置（模板，首次用时按 opencode 文档核对）

```jsonc
// ai决策大脑强化训练/opencode.json
{
  "$schema": "https://opencode.ai/config.json",
  "provider": {
    "deepseek": {
      "npm": "@ai-sdk/openai-compatible",
      "options": {
        "baseURL": "https://api.deepseek.com",
        "apiKey": "{env:DEEPSEEK_API_KEY}"
      },
      "models": {
        "deepseek-chat": { "name": "DeepSeek-V3 提案手" },
        "deepseek-reasoner": { "name": "DeepSeek-R1 归因分析" }
      }
    }
  },
  "model": "deepseek/deepseek-chat"
}
```

- `deepseek-chat`：日常提案（便宜快）
- `deepseek-reasoner`：学歪归因/疑难分析（"连续 5 轮提案都被拒，为什么"）
- API Key 存环境变量，不进任何文件

## 六、训练师行为准则（已落盘为 AGENTS.md，opencode 自动加载）

要点：① 每次提案≤3 参数 ② 必须引用证据 ③ 死参数禁区 ④ 边界表硬约束 ⑤ 复盘义务 ⑥ 禁改 C#/场景/holdout。
完整版见本目录 `AGENTS.md`——**改训练师行为=改 AGENTS.md，不改代码**。

## 七、防过拟合（AI 调参的头号翻车方式）

1. **holdout 场景**：H1/H2 细节不写进任何训练师可见文件，只由 harness 在 verdict 阶段跑
2. **种子轮换**：每周换一批基准 seed（suite 不变，seed 变），防"背题"
3. **退化红线**：任何场景 score 退化 >5% 直接拒（即使总分升）
4. **冠军双条件**：总分升 AND holdout 不退，缺一不可

## 八、成本估算（DeepSeek 官方价）

一轮提案 ≈ 输入 30K tokens（报告+注册表+历史）+ 输出 1K：
- deepseek-chat：约 ¥0.03/轮 → 一天 50 轮 ≈ ¥1.5
- deepseek-reasoner：归因一次 ≈ ¥0.3，按需
跑局算力全在本地，零 API 成本。**瓶颈从来不是钱，是提案质量。**

## 九、人工介入点（不能省）

| 时点 | 动作 |
|------|------|
| 每天 | 扫一眼 verdict 列表，批准/驳回 candidate |
| 提案连续 5 轮被拒 | 叫 reasoner 归因，可能要改目标函数权重 |
| T2 公式提案 | 人审 diff 后合并（T2 永远是人流） |
| 每周 | 回灌 champion 到 Unity，手玩 10 分钟抽检（最终裁判永远是手感） |
