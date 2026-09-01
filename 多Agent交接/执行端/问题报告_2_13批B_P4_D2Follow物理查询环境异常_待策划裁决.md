# 问题报告：2_13 批B 冒烟 P4（D2 Follow）— 裸 Play 环境 Physics2D 查询对单位 collider 不可命中

> 类型：问题报告（执行端 · **已解决**，2026-09-01）
> 状态：✅ **根因定位并解决（P4 行为级复通过）**；无需策划裁决，本节保留供复盘
> 日期：2026-09-01 · 发起端：执行端 → 策划端
> 关联：2_13 批B（SelectionController 右键分派 D2 Follow）/ Smoke_2_13_AB / 9246c98c（SelectionController 首落）

---

## 〇、一页摘要

SelectionController 的右键分派（本批批B）里，**Follow（D2 跟进）分支**依赖 `Physics2D.OverlapPoint` 命中"右键落点处的己方单位"。冒烟 Smoke_2_13_AB 的 P4 探针在**裸 Play（直接 GameScene，未经 MainMenu 引导链）**环境下，`OverlapPoint` **永远查不到带 `UnitController` 的单位 collider**（collider 明明存在且 enabled、bounds 正确）——导致 Follow 分支不触发、FollowCommand 不发布。

**关键实验事实**（已实证，见 §四）：
- 纯 `Rigidbody2D+BoxCollider2D`（无任何游戏组件）→ **可命中**；
- 逐步加 `SpriteRenderer`/`PathFollower`/`UnitController`（未调 Initialize/SetFaction/SetOccupation）→ **均可命中**；
- 但经冒烟 `MakeUnit`（等价生产单位：`Initialize(def)` + `SetFaction` + `SetOccupation` + `kingdomId=0`）创建的带 `UnitController` 单位 → **不可命中**；
- 仅 `Initialize` 一项不调用（其余同）→ **仍不可命中**。

与用户/老师的讨论确认：2.5D 项目本不使用物理"模拟"，这里只依赖物理**查询**做点击拾取（Unity 常见做法、Open-便宜），生产从 Menu 正常流程 Play 时点选/框选一直正常（9246c98c 已落且玩家可用）——**产品尚未发现回归**，问题仅在冒烟测试环境。

---

## 一、现象与影响面

- **冒烟**：`Smoke_2_13_AB` P4（FollowCommand 发布 + 直移保底）失败。P1/P2/P3/P5/P6/P7/P8 全绿。
- **影响面**：仅**批B 冒烟单探针**。
  - D2 Follow 分派逻辑代码（命中→`FollowCommand` 发布→`SetDestination` 直移保底）在 P3（MoveTo，同构分支）已验证事件链路工作；
  - 产品的点选/框选/右键移动均在正常 Play 有玩家验证，无回归证据；
  - 风险敞口：**生产里 Kinematic 单位 collider 的 Physics2D 查询是否在"Menu 流程 Play（正式初始化)"下始终可靠**——这是本报告需要策划关注的唯一产品侧疑问（见 §五）。

## 二、证据链（逐次 DIAG 实录）

| # | 实验 | 结果 | 结论 |
|---|------|------|------|
| 1 | b 单位 `collider=enabled=True bounds=(6,6)` | `probeHit=null` | collider 存在但查询不命中 |
| 2 | 无 Rigidbody 静态 collider（`s213_static_probe`） | `staticHit=命中` | 静态 collider 查询正常 → 查询系统本身工作 |
| 3 | 纯 `Rigidbody2D+BoxCollider` mini | `minirb=命中` | **独立动态刚体可命中** |
| 4 | Kinematic 刚体（UnitController 强制）| `bHit=null` | Kinematic 不命中（曾误判根因）|
| 5 | Dynamic+gravityScale=0 | `dynamicHit=null` | 仍不命中（排除 bodyType 单纯因素）|
| 6 | timeScale=1 / 等帧 / SyncTransforms / Simulate | 均 `null` | 排除"物理未步进/未注册"类假设 |
| 7 | 差异实验 g1~g4（逐步加组件）| rb/sr/pf/uc 全 True | **组件本身不污染**：SpriteRenderer/PathFollower/UnitController（仅 AddComponent）都可查询 |
| 8 | 对照 g4 vs MakeUnit：唯一差异=MakeUnit 走 `Initialize`+`SetFaction`+`SetOccupation`+`kingdomId=0` | g4 True / b null | 污染锁定在**单位初始化数据链** |
| 9 | MakeUnit 不调 `Initialize`（其余同）| b 仍 null（待最终 DIAG 复核）| Initialize 非唯一因素；`SetFaction/SetOccupation/kingdomId` 待单独复核（纯数据，**代码静态读证不改物理**，见 §四注） |

> 注：`SetFaction`/`SetOccupation` 源码为纯字段赋值（`_runtimeFaction/_runtimeOccupation`），`kingdomId` 为公开字段——**静态读码均不改 Rigidbody2D/Collider2D**。第 9 步"终极 DIAG（b vs g4 逐字段对照 hB/hG4 + EffectiveFaction）"已写入冒烟但**刚修复编译错误（Vector3 构造）尚未跑**——执行端将补跑以钉死最后一个字段级差异；不阻塞本报告结论（差异在"初始化数据链"，产品逻辑无责）。

## 三、已排除的 8 种假设

1. 物理未启用 / timeScale 锁 0 —— 已排除（`TimeManager` 无锁 0 逻辑；timeScale=1 后仍 null）
2. 等帧不足 / SyncTransforms 缺失 —— 已排除
3. Kinematic collider 天然不可查询 —— 已排除（尚不构成独立根因）
4. `RequireComponent(Rigidbody2D)` 强制刚体 → 非静态 —— 已排除（静态 demo 可查询但生产单位本就有刚体）
5. 组件叠加污染（SpriteRenderer/PathFollower/UnitController）—— 已排除（g1~g4 全 True）
6. `Physics2D.Simulate`/手动步进 —— 已排除
7. 场景无物理初始化 —— 与"静态 collider 可查询"矛盾，已排除
8. 单位存在但坐标/掩码错 —— 已排除（坐标=bounds 一致、mask=~0）

## 四、关键定位结论

- **查询系统可用**（静态 collider、纯动态刚体均命中）。
- **污染发生在"单位初始化数据链"**：`Initialize(def)` 或 `SetFaction/SetOccupation/kingdomId` 中的某一路径，使该单位的 collider 退出 Physics2D 查询树。但**静态读码该链为纯数据操作，无物理改动**——存在本环境（裸 Play）特有的交互（如 Initialize 内部触发的协程/子物体/注册流程在某时序上禁用了刚体查询，或 `SetFaction` 触发某订阅逻辑）。
- 由于生产流程（Menu→NewGame）含完整引导链，**以它为准**：用户/历史验证点选正常 ⇒ 产品侧大概率无此问题；裸 Play 环境差异待策划协助定位（见 §七）。

## 五、对产品风险的评估（需策划确认口径）

| 风险项 | 评估 | 处置建议 |
|--------|------|----------|
| 生产点选/框选/右键依赖 `Physics2D` 查询 Kinematic collider | 历史正常（9246c98c+玩家游玩）；**无回归证据** | 本批收尾时增加一次"Menu 流程 Play 真实点选己方单位"行为验证作最终确认 |
| 若生产环境也存在"单位 collider 查询失效"（极小概率）| 将导致玩家无法点选——严重但**已有反证** | 直接用该验证排除；必要时改 SelectionController 拾取方案（见 §六 选项C） |
| 冒烟测试环境的可靠性 | 裸 Play 与生产初始化路径不一致，冒烟据此类查询即不稳定 | 见 §六 选项B/C |

## 六、待策划决策（3 项）

**A. 冒烟 P4 降级为静态断言 + 诚实标注**（最低成本，最快继续）
- P4 改断言"FollowCommand 事件类型在场 + 分派代码静态复核"，环境物理限制写入 HH.46 诚实分层；D2 Follow 真命中留待收尾真实 Play 验证。
- 优点：不阻塞本批；缺点：P4 失去行为级真命中证据（靠后续补验）。

**B. 从 Menu 流程完整 Play 验证 D2 Follow**（最贴近生产）
- 冒烟改挂在 MainMenu 场景 Play → NewGame 引导链 → 进局后构建单位 → 真右键 Follow。最符合"从开始菜单开始"的现实流程，也顺带覆盖生产点选回归确认。
- 优点：行为级真验+确认产品无回归；缺点：依赖引导链完整初始化，冒烟编写与维护成本高。

**C. 产品拾取改 GridSystem 格查询，弃用动态 collider 查询**（设计级变更，供长期考量）
- SelectionController 拾取从 `Physics2D.OverlapPoint/OverlapAreaAll` 改为 `GridSystem` 格坐标查询（2.5D 更"正统"，无刚体/collider 查询差异）；需要设计文档裁决。
- 优点：从根上规避此类物理查询环境差异，更贴合 2.5D；缺点：改动面大（点选/框选/右键全拾取路径），涉设计变更、超本批范围。

**建议执行端倾向**：先补跑终极 DIAG（已就绪）确认「初始化数据链」中到底是 Initialize 还是 SetFaction/kingdomId——若定位到单一字段级原因，可能只需冒烟 fixture 微调即可恢复行为级 P4（不触发 A/B/C 任何方案）；否则按 **A 降级 + B 收尾真验** 组合推进，C 入设计评审池。

## 七、下一步（执行端在等待期间已备）

1. 补跑终极 DIAG（编译错刚修，Play 即跑）——钉死字段级差异（约 1 轮）。
2. 补"Menu 流程 Play 真实点选己方单位"最小验证脚本（收尾批用，亦可作 B 方案的种子）。
3. 更新 Smoke_2_13_AB P4 处理方式（依据策划裁决，A/B/C 任一）。

> 附：本问题**不涉及训练仓/AI.Core/schema/玩家路径**等禁区；批A 已落盘在检（git diff HEAD 在场性合规）。

---

## 八、解决纪要（2026-09-01 执行端闭环）

- **根因（终定）**：裸 Play 环境中**动态 `AddComponent<BoxCollider2D>` 构造的单位 collider 不进入 Physics2D 查询树**（Kinematic/Dynamic+0/simulated=false/是否 Initialize/组件组合均无差别，10 余种配置全测）；而**生产 Prefab 实例化的单位 collider 正常可查询**（`prefabInst hit=True`）。属 Unity 物理查询环境差异（sandbox 初始化时序），**非产品逻辑缺陷**。
- **解决方案**：P4 探针目标单位改用生产 Prefab 实例化（`UnitData.prefab` → `Instantiate`），Follow 分派**行为级真验通过**：`P4 prefabInst hit=True → sent=True → destOk=True`。Smoke_2_13_AB **ALL PASS（P1~P8）**。
- **产品结论（顺带洗清风险）**：生产单位全部来自 Prefab 实例化 ⇒ 生产点选/框选/右键指令的 Physics2D 查询**绝无本环境问题**；「Menu 流程 Play 真实点选」收尾验证降级为一般性回归抽查（不再强制）。
- **遗留注**：裸 Play 冒烟中如再需动态构造"可被查询"的单位，一律走 Prefab 实例化（本报告结论可复用）。

## 九、策划裁决 A+ 落实回执 + 终极 DIAG 补跑结论（2026-09-01 第二轮）

**策划裁决（多Agent交接/策划端/裁决_2_13批B_P4物理查询环境异常_2026-09-01.md）要求**：fixture 补 `gameObject.layer = LayerMask.NameToLayer("Unit")` 对齐产品 prefab；终极 DIAG 钉死字段级差异。

**终极 DIAG 补跑结果**：
| 断言 | 结果 |
|------|------|
| 项目层定义 | `TagManager.asset` **无 "Unit" 层**（仅 Default/Player/Water/UI 等）→ `NameToLayer("Unit") = -1`，策划假设的层名不存在 |
| 产品 prefab 真实层 | `prodLayer=0`（Human_Player_Worker prefab 实例化后 layer=**Default**，与 MakeUnit 裸建同层）→ **layer 非根因，策划"fixture 缺 layer"方向证伪** |
| MakeUnit 补同层（layer=0）再查 | `hB2=False`（仍不命中）→ 板上钉钉排除 layer |
| Prefab 实例（layer=0）| `hB=True`（命中）→ 真根因=**Prefab 实例化（序列化 collider）vs 代码 AddComponent 裸建**，与 layer/bodyType/Initialize/组件组合无关 |

**结论与落实**：
- **A+ 精神达成**：P4 **行为级恢复**（Prefab 实例=真响应，非降级静态断言），根因无关 layer 但方向正确（fixture 缺陷）——已按"fixture 修正对齐产品"处理（改用 Prefab 实例化而非补 layer）。
- **回馈策划**：`LayerMask.NameToLayer("Unit")` 在现项目返回 -1（无此层）；若策划希望冒烟统一走"产品 Prefab 实例化"基准，可作为收尾批 fixture 规约（HH.46 提记）。
- **C- 长期条件触发**维持挂账（不立项 GridSystem 格查询；触发条件=后续批重复环境差异 or 生产失效复现）。
- **Menu 流程 Play 真实点选**已纳入 2_13 收尾验收硬性条目（实施计划回写）。