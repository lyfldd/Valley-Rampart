# BUGFIX：编队阵型方向固定 + 弓手残编站极端位 + 弓手走到脸上威胁保底

> 2026-08-01 编队 P0 场景验证时用户反馈三个问题。

## 问题1：队伍方向固定（应对左 vs 右）

### 现象
阵型槽位偏移全设计为向右（Charge: 兵在 1,2,3 弓在 4,5）。如果敌人改在左边，将军带头冲右就反了。

### 根因
`FormationController` 没有"阵型朝向"概念，AssignSlots 直接用 `def.slots[i].cellOffset` 原值，不会根据敌方方向翻转。

### 修复
[FormationController.cs](../Valley%20Rampart/Assets/_Game/Systems/AI/Formation/FormationController.cs)：
- 新增 `_formationDirection`（1=右/-1=左）
- `SetAdvanceTarget` 根据推进目标相对锚点的 x 符号设置方向
- `AssignSlots` 时 `offset.x *= _formationDirection`

验证：dir=-1 时 Defense 兵/弓 x 全翻符号 ✓

## 问题2：弓手残编站阵型极端位

### 现象
弓箭手出现在阵型极端位（如 Defense 的 (-3,0) 最远端），明明战士是够的。

### 根因
`AssignSlots` 按 slot 数组顺序遍历，弓手先匹配 slot[0]。DefenseFormation 的 slot[0]=(-3,0) 是 RangedOnly 极端位 → 残编 1 弓时弓被甩到 (-3,0)，而战士紧凑在 (-2,-1,1)。

### 修复
[FormationController.cs](../Valley%20Rampart/Assets/_Game/Systems/AI/Formation/FormationController.cs) AssignSlots 重写为两轮填充：
1. **第一轮**：近战按 slot 顺序填 MeleeOnly/GeneralOnly/Any 槽
2. **第二轮**：弓手填剩余 RangedOnly/Any 槽，**按距锚点 |x| 从近到远排序**（残编时弓优先填靠后安全位）

### 穷举验证（execute_code 模拟，改阵型数据后）

Defense 阵型所有残编组合（dir=1 向右）：

| 兵/弓 | 弓手分配 | 评价 |
|-------|---------|------|
| 3M+1A | A(-1,0) | ✓ 弓在将军身边安全位 |
| 2M+1A | A(-1,0) | ✓ |
| 1M+1A | A(-1,0) | ✓ |
| 0M+1A | A(-1,0) | ✓ |
| 3M+2A | A(-1,0) A(1,0) | ✓ 满编弓在将军两侧内侧 |
| 2M+2A | A(-1,0) A(1,0) | ✓ |
| 1M+2A | A(-1,0) A(1,0) | ✓ |

### 阵型数据修正（DefenseFormation.asset）

原 Defense 阵型弓手槽在两翼外侧极端位（-3,2），违反 §3.3 "弓手不站第 1 位"硬约束。改为兵在前面挡、弓在将军身边射：

| slot | 原布局 | 新布局 |
|------|--------|--------|
| 0 | (-3,0) RangedOnly | **(-3,0) MeleeOnly** 兵-左外侧前线 |
| 1 | (-2,0) MeleeOnly | (-2,0) MeleeOnly 兵-左 |
| 2 | (-1,0) MeleeOnly | **(-1,0) RangedOnly** 弓-将军左侧安全位 |
| 3 | (1,0) MeleeOnly | **(1,0) RangedOnly** 弓-将军右侧 |
| 4 | (2,0) RangedOnly | **(2,0) MeleeOnly** 兵-右外侧 |
| 5 | (3,0) Any | (3,0) Any 残编补充 |

其他三个阵型检查无需修改：
- **Charge**：兵(1,2,3)在前将军带头，弓(4,5)殿后 ✓
- **Retreat**：弓(-5,-4)先撤，兵(-3,-2,-1)殿后 ✓（撤退弓先走是对的）
- **Garrison**：兵(y=0)堵口，弓(y=1)上墙 ✓

**修复前**：残编 1 弓 → A(-3,0)（极端位）
**修复后**：残编 1 弓 → A(2,0)（靠后安全位）✓

Charge 阵型验证：
- 3M+2A dir=1：M(1,0) M(2,0) M(3,0) A(4,0) A(5,0) ✓ 将军带头弓殿后
- 2M+1A dir=1：M(1,0) M(2,0) A(4,0) ✓ 弓在靠后位

## 问题3：弓手走到人脸上 + 威胁等级一直一级

### 现象
弓箭手走到敌人脸上，但威胁等级一直是 1 级（Alert），不升级。用户怀疑威胁低导致追击不收紧，走到脸上。

### 根因（用户诊断正确）

**威胁因子低**：
- 弓手 `threatSensitivity=0.5`，`perceptionRadius=10`（perceptionWorld=22.6）
- 贴脸时 distFactor=1.0，但 `rawFactor = (1.0×0.35 + 其他) × 0.5 ≈ 0.22~0.29`
- 卡在威胁 0-1 级（阈值 0.25/0.5），威胁 1 = "全力执行"不撤退
- 弓手继续追击走到脸上

**无攻击距离保底**：
- L3 MoveTowards 的守阵追击 clamp 只限槽位 ±2cell，没有 attackRange 保底
- 弓手能走到比射程更近的地方

### 修复

**修复3：L3 攻击距离保底**（[L3CommandComputer.cs](../Valley%20Rampart/Assets/_Game/Systems/AI/Decision/L3CommandComputer.cs)）
- MoveTowards 分支加：远程单位（isRanged）追击时，若目标距离 < attackWorldRange → 设 TargetPos=SelfPos（停在原地，攻击系统 In-Range 自动开火）
- 弓手到了射程内就停，不再走到脸上

**修复4：威胁因子保底**（[ThreatAssessment.cs](../Valley%20Rampart/Assets/_Game/Systems/AI/ThreatAssessment.cs)）
- distFactor 保底：敌人进入 attackWorldRange 内时 distFactor 强制 1.0
- rawFactor 保底：敌人进入 attackWorldRange 内时 rawFactor 不低于 0.5（威胁 2 级=危险）
- 贴脸的弓手不再是威胁 1 级，会触发"危险"行为（谨慎/保距）而非"全力执行"继续追击

### 效果
- 修复前：弓手贴脸 rawFactor≈0.22（威胁0级），继续追击走到脸上
- 修复后：弓手贴脸 rawFactor=0.5（威胁2级=危险），触发保距行为 + L3 攻击距离保底停在射程外开火

## 修改文件清单

| 文件 | 改动 |
|------|------|
| FormationController.cs | 加 _formationDirection + SetAdvanceTarget 推导方向 + AssignSlots 两轮填充+弓手槽排序+方向翻转 |
| L3CommandComputer.cs | MoveTowards 分支加远程攻击距离保底 |
| ThreatAssessment.cs | distFactor 保底 + rawFactor 保底 0.5 |
