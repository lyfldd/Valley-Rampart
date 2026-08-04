# BUGFIX：模拟器S3破阵场景死循环

> 2026-08-04 · 未提交（工作区改动，ai决策大脑强化训练仓库）

## 现象（发生了什么）

模拟器（harness）跑 S3 破阵场景时进程**卡死**：CPU 拉满、无任何输出、需强制结束。

- 触发路径：`dotnet run --project harness -- determinism harness/Scenarios/s3_formation_break.json 1`，或 `determinism all` 跑到 S3 时卡死。
- 临时打印定位：tick 正常推进到约 tick 250（仿真 25s，aliveH=3 aliveU=4），此后不再推进 = 某个 tick 的内部步骤死循环（非变慢）。
- 关键对照：S1 正常（4.5s）；`git stash` 全部未提交 sim 改动后 S3 正常（7.3s）→ 死循环在"未提交改动"里。
- 跨会话反复排查：2026-08-03 首次卡住 → 保存进度次日再修 → 2026-08-04 定位修复。

## 根因

3.6 越墙判定在 `SimDamage.IsBlockedByFort` 新增"射手→目标区间格子遍历查工事"，其 for 循环在**同格**时永不收敛：

```csharp
int c1 = _grid.WorldToCellX(attacker.Position.x);
int c2 = _grid.WorldToCellX(target.Position.x);
int dir = c1 < c2 ? 1 : -1;                      // c1==c2 时 c1<c2 为 false → dir=-1
for (int cx = c1 + dir; cx != c2; cx += dir)     // cx 从 c1-1 单调递减，永不等于 c2(=c1) → 死循环
```

因果链：S3 是破阵场景，含 2 名 `Human_Player_Archer`（远程）。破阵减员后弓箭手被敌战士**贴身到同一格**（x 差 < cellSize 2.26）→ 远程延迟命中结算 `ProcessScheduledHits` 调 `IsBlockedByFort` → `c1==c2` → for 死循环 → 整个 tick 卡死、进程无响应。

**区分表层与根因**：表层是"S3 卡死"；根因是新增的区间遍历 for 循环未处理 `start==end` 边界。骑兵冲锋、GroundEffect、工事减免均被先后禁用排除（S3 无骑兵/无工事无地面效果，那些步骤本就空转），真正触发点是每次远程攻击都会走的越墙判定。

## 修复方式（怎么修复的）

| 文件 | 改动 |
|------|------|
| harness/Sim/SimDamage.cs（IsBlockedByFort） | 在算 `dir` 前加 `if (c1 == c2) return false;` —— 同格时射手与目标之间无中间格、无工事可挡，直接返回"不挡"，绕过不收敛的 for 循环 |

排查手段（供复用）：逐一二分禁用新增 tick 步骤（`_effects.Tick` / `TickCharge`）均无效 → 转用 `git diff --stat` 审查全部未提交 sim 文件 → 锁定改动量大且每 tick 触发的 SimDamage → 读 diff 发现 for 循环边界缺陷。

## 修复性质：彻底根除

- 改的是**原因**（for 循环边界条件缺陷），不是症状；同格远程攻击的越墙判定语义本就应返回"不挡"。
- 移除这个 `c1==c2` 早退补丁后问题会复发，但该补丁本身即是修复根因（循环不变量错误），非防御性绕过。
- 同类问题（任何同格远程攻击）不会再以相同机制死循环。

**教训 / 预防**：新增"区间/范围遍历 for 循环"（`for(i=a; i!=b; i+=step)`）必须显式处理 `a==b`、`step` 方向与终止条件匹配；优先用 `i < b`/`i > b` 有界条件替代 `!=`，可从根本上避免不收敛。

## 验证方式

1. `determinism harness/Scenarios/s3_formation_break.json 1` → **3.6s 通过**，逐字节一致。
2. `determinism all`（S1-S7）→ 全剧本通过。
3. 7 个新场景（m8_X5/X6/E8/E9/E10/D7 + c1）单独 determinism → 全通过。
4. `champion baseline --suite v8` 重建 baseline（100 battles × 35 场景，含 6 新场景 + 协同指标）+ holdout 同卷 → 正常出分，无卡死。
