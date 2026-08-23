# HH.15 sim 侧 IWarehouse 同步待办登记（响应 HH.14 退回重验）

> 类型：待办登记（sim 同步，交训练仓会话处理；HH.8 纪律——执行端只登记、不代做、不在训练仓 commit）
> 状态：✅根因已裁定（2026-08-23 策划端正主复验，见 §五）；待办仍挂训练仓会话执行
> 日期：2026-08-23 · 发起端：执行端 · 关联：HH.14（退回重验）/ HH.8（同源契约+三纪律）/ HH.13（Metal 消费方）· 2_12 步骤8

---

## 一、为何发起（策划退回重验摘要）

策划端实盘核验训练仓（文件名+内容级递归搜索）：`harness/` 下**不存在** `IWarehouse.cs`，`harness/Core/` 七个子目录（Config/Decision/Formation/Memory/Ports/Shim/Stimulus）无 IWarehouse/ResourceType 文件或引用。判定 HH.14"sim 侧 IWarehouse.cs（Metal 末尾追加、逐字一致）"申报不实；可能 HH.8 落盘后被训练仓门禁回滚，或执行端比对了过期快照。要求执行端：实盘复查→登记 sim 同步待办交接训练仓会话→Metal 真值方向确认但以接口真实存在为前提。

## 二、执行端实盘复查结果（贴实盘输出，履行跨仓实盘纪律）

**工具输出（本次不再凭记忆/忽略区快照）：**

- `Glob **/IWarehouse.cs`（范围 `C:\Users\trs\Desktop\Valley Rampart`）实盘命中 2 项：
  - `Valley Rampart\Valley Rampart\Assets\_Game\Systems\Kingdom\IWarehouse.cs`（Unity 运行时侧）
  - `Valley Rampart\Valley Rampart\ai决策大脑强化训练\harness\Core\IWarehouse.cs`
- `LS ...\ai决策大脑强化训练\harness` 实盘：仅列出 `Core/IWarehouse.cs`。
- `git check-ignore -v` 实盘输出：`.gitignore:20:ai决策大脑强化训练/` → 该目录被**主仓库 .gitignore 忽略**（非主仓库 git 跟踪，属训练仓区域/独立仓库）。
- `git ls-files "Valley Rampart/ai决策大脑强化训练"`：无输出（主仓库未跟踪该目录任何文件）。

**执行端判定**：
- 执行端此前 8.1/8.5 比对的"sim 侧 `IWarehouse.cs`"实际是**主仓库忽略区的本地副本**（`ai决策大脑强化训练\harness\Core\IWarehouse.cs`），非策划训练仓真身。
- 执行端无法直接访问/实证策划侧训练仓真身 → 以策划实盘为准：训练仓 `harness/Core` 确无 `IWarehouse.cs`（可能门禁回滚或从未经门禁同步）。
- **撤回 HH.14"ResourceType.Metal 末尾追加、sim 逐字一致"申报**；sim 侧 Metal 枚举同步**未确认在训练仓真身落地**。接受退回重验。

## 三、sim 同步待办（交接训练仓会话处理，执行端不代做）

- [ ] **harness/Core 落盘 `IWarehouse.cs`**：使训练仓真身存在该接口，`ResourceType` 枚举含 `Metal` **及 HH.19 裁决 3 弹种（StoneAmmo/FireballAmmo/MagicAmmo）**（与 Unity `GameEvents.cs` 末尾追加同值，保旧值稳定），签名对齐 Unity `Systems/Kingdom/IWarehouse.cs`（Query/CanTake/Take/Deposit/Transform + ResourceAmount）。**命名锁定 Xxx Ammo 后缀，避开训练仓既有 `ProjectileType.Stone/Fireball/Magic` 撞名（SimBrain.cs:39 实盘确有）**。
- [ ] **走训练仓自身门禁**（HH.8 裁决三纪律）：训练仓内 commit → 改 → 双门禁 → 过则留；训练仓会话自治，执行端不代管、不在训练仓 commit。
- [ ] **台账登记**：更新训练仓/交接台账，记录 IWarehouse 落盘与 Metal 枚举同步。
- [ ] **Metal 加工 sim 真值**：接口真实存在后，"Metal 加工同源真值挂 2_9/步骤14"前提成立（HH.14 决策 A 方向保留）。
- [ ] **HH.19 行为差登记（2026-08-23 裁决回写追加）**：Unity 侧 `UnitController.TickAmmoResupply` 假搬运定时器已随 2_12 步骤9 退役（真实后勤链：工人装填真耗仓库存量）；sim 侧 SimUnit/SimBrain 石弹自动补给仍在——**是否对齐真实后勤（或保留快照基线）由训练仓会话按训练目标决策**并记台账，勿默认照搬。

## 四、对步骤8 的影响

- 8.1~8.3（Metal 枚举/铁匠铺/消费方）、8.4 GameOver 切换：均不依赖 sim 侧文件，**不受本待办阻塞**，执行端已完成并提交。
- 8.5 sim 门禁核对：**结论退回，挂起至训练仓会话完成本待办、执行端凭实盘复核后另行申报**。

---

## 五、策划端正主复验与根因裁定（2026-08-23）

> 策划端三处实盘核验（真训练仓 git log / 两处 harness\Core 递归 / 目录对比），谜底揭开——**存在两个"训练仓"，HH.15 §二的解释只对一半**：

| 路径 | 性质 | IWarehouse.cs |
|------|------|---------------|
| `Valley Rampart\ai决策大脑强化训练\`（**仓库根下**） | **真·训练仓**：独立 git（d32b368 最新，含 QQQ.5 T2.4 / 决策对齐等真实训练历史） | **不存在** |
| `Valley Rampart\Valley Rampart\ai决策大脑强化训练\`（**Unity 工程内嵌套**） | 伪训练仓：空壳 harness\Core（七子目录全缺，仅单文件） | 存在（执行端 8.1/8.5 比对的"sim 侧"即此） |

**裁定**：
1. **根因 = 落盘路径错误**：HH.8 裁决三"接口放 harness/Core"执行时，写进了 Unity 工程内嵌套目录 `Valley Rampart\Valley Rampart\ai决策大脑强化训练\harness\Core\`——真训练仓（仓库根下）从未收到该文件（其 git log 无相关 commit）。**非训练仓门禁回滚**（HH.15 §二"可能被门禁回滚"的猜测排除）。
2. **伪目录处置**：Unity 工程内嵌套 `ai决策大脑强化训练\` 整目录为误造（空壳+单文件），**删除**；其唯一文件 IWarehouse.cs 内容即 §三待办的落盘源。删除由执行端在主仓侧执行（该目录不在任何 git 跟踪内）。
3. **待办路径修正**：§三训练仓落盘目标 = **仓库根 `ai决策大脑强化训练\harness\Core\IWarehouse.cs`**（勿再写 Unity 工程内）。
4. **防再犯**：跨仓操作前必须 `git -C <路径> rev-parse --is-inside-work-tree` + `git log` 确认真身；**真训练仓路径唯一：仓库根 `ai决策大脑强化训练\`**。并入跨仓实盘纪律（HH.14 裁决补强）。

**执行端确认事项**：上述裁定与 §二复查结论的关系——§二"比对了忽略区本地副本"方向正确，但未发现根因是**自己 8.1 写错位置**；接受根因裁定后，删除伪目录动作随下次主仓 commit 一并执行。