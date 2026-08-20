# BUGFIX：逃逸点评分往敌堆方向逃三面围合失效

> 2026-08-20 · 提交 0d68f3e9

## 现象（发生了什么）
2_7 冒烟（K4，程序化搭 60×60 全可走网格，真 Play 跑 `EscapePointSampler.TryPick`）验证三面围合逃逸方向：
- 敌人分布于 左/右/下 三面（世界坐标），开口朝上，self 居中。
- `flee`（期望逃逸方向）质心正确算出 `(0, 1)`（向上）。
- 但实际返回的逃逸点为 `(0, -1.28)`——**正对下方唯一敌人，往敌堆里逃**。

Console 关键日志：
```
[SMOKE] K4_escape MODE=Play grid=(60x60)
[SMOKE] K4_escape MODE=Play escape=(0.00, -1.28) (期望向上, y>1.3) FAIL
[SMOKE] K4_escape MODE=Play TOTAL FAIL 三面围合逃逸方向未按预期指向开口侧
```

## 根因
[EscapePointSampler.cs](c:\Users\trs\Desktop\Valley Rampart\Valley Rampart\Assets\_Game\Systems\AI\EscapePointSampler.cs) 采样评分公式方向性错误：

```csharp
// 旧
float score = sectorW / Mathf.Max(0.01f, threat) + distBias;  // 取最小分
```

- `threat`（该采样方向上朝向敌人的惩罚，越大越危险）被当作**除数**：`threat` 越大 → `score` 越小 → 越容易被选中。方向完全反向，士兵专挑有敌方向逃。
- 开口侧（上方）`threat≈0`，被 `Mathf.Max(0.01f, threat)` 兜底成 `100+` 的高分 → 反而被排除。

即「避敌」与「期望扇区」两个意图在公式里都反了，导致三面围合逃向唯一开口侧的核心语义失效。

## 修复方式（怎么修复的）
| 文件 | 改动 |
|------|------|
| `Assets/_Game/Systems/AI/EscapePointSampler.cs` 评分行 | 评分改为 `score = (1f - sectorW) + threat * threatScale + distBias`。`treat` 越大分越高（避敌，惩罚项），`(1-sectorW)` 越靠近期望扇区分越低（鼓励开口方向），`distBias` 就近理想距离。取最小分 → 选中「远离威胁 + 靠近期望 + 近理想距离」的方向 |
| 同上 | `threatScale` 读 `CostBiasConfig.Instance.threatWeight`（当前 2.0），不硬编码魔法数，对齐 CostBiasConfig 数据驱动纪律 |

## 修复性质：彻底根除
- **彻底根除**：改的是公式本源（评分方向），不是调用处打补丁。删掉此改动，三面围合必复现往敌堆逃。
- **验证**：K4 探针回归，逃逸点从 `(0,-1.28)`（对敌下）修正为 `(0, 1.02)`（开口上）PASS。断言阈值同步校正（采样圈半径上限=retreatRadius=2×cell.y≈1.28，故选中点落在 [0.77,1.28]，原 `y>2×cell.y` 阈值本身设错，改判「明显向开口侧」）。

## 验证方式
1. 程序化撑 60×60 全可走网格，三面围合（左/右/下），开口在上。
2. 真 Play 跑 `EscapePointSampler.TryPick`。
3. 预期：`escape.y` 明显 >0（向上离开围合），不再为负（对敌）。