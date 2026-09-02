# HH.49 输入1清障批交付报告（验收成立，2026-09-02 策划端验收）
> 接手批：输入1（左键框选事件化+EventSystem 切换）。前手=Unity 子目录执行端（2026-09-02 17:00 交接报告），TraeCode 接手完成交接主项（冒烟全套复跑+行为级验收）并清偿遗留疑云（P3 归因）。
> 指令源：HH.46 §三-5 架构观察（输入双栈并存）→ 策划端裁决统一 → 输入1 清障批。

---

## 一、批进度对账（4 任务全清）

| # | 任务 | 状态 |
|---|------|------|
| 1 | 侦察（队列/HH.46/在途盘点） | ✅ 前手完成 |
| 2 | 左键框选事件化（代码侧） | ✅ 落盘+编译通过；**本轮行为级已验**（§三-2） |
| 3 | EventSystem 切换（双场景） | ✅ 实证代替施工（GameScene 已随 40a5ac9 入库；MainMenuScene 收编随批呈报） |
| 4 | 冒烟全套复跑+行为级验收 | ✅ **本轮完成**（交接主项清偿，§三） |

## 二、已落盘改动（git 实证，均未提交）

本批产物（6 M）：

- `Assets/Resources/Config/Input/GameInput.inputactions`（M）：新增 leftClickRelease 动作（id 尾插 9200 序列）+ `<Mouse>/leftButton` press(behavior=1) ReleaseOnly 绑定
- `Assets/Resources/Config/Input/GameInput.cs`（M）：重导入自动再生（零手工编辑，diff 全为生成痕迹）
- `Assets/_Game/Core/GameEvents.cs`（M）：新增 `LeftClickReleasedEvent`（readonly struct，镜像 Left/Right 样式）
- `Assets/_Game/Core/InputManager.cs`（M）：leftClickRelease 绑定 + OnLeftClickRelease 处理器（守门与 OnLeftClick 全同：Performed+IsInteractionEnabled+Playing）+ Cleanup 退订
- `Assets/_Game/Systems/Interaction/SelectionController.cs`（M）：Update legacy 轮询退役 → 订阅 LeftClickPressed/ReleasedEvent 双事件；IsDragging 语义保留；守门复用既有 `IsInteractionBlocked()`（面板开吞事件）；右键/控制组零改动
- `Assets/Scenes/MainMenuScene.unity`（M）：**EventSystem 切换收编呈报**——StandaloneInputModule(4f231c4f)→InputSystemUIInputModule(01614664，与 inputsystem@1.14.4-t3 包 meta 精确匹配)。先于本批存在（批C+批D 会话漏提交），前手 SaveScene 仅落盘编辑器内存态、语义未变，随本批交策划端 commit

非本批产物（勿误伤）：美术轨 3 文档 M（3.1.2/3.1.3/0.6 审查决策记录）——策划端并行产物，不在本批提交范围。

## 三、验收与回归（本轮完成）

### 1. 冒烟四场（菜单 Valley/验证/…，先 Play 后点，一套一调用短超时）

| 冒烟 | 判定 | 备注 |
|------|------|------|
| 2_13_交互输入_AB | **ALL PASS**（P1~P8） | 含交接遗留疑云 P3（见 §四归因） |
| 2_13_批C_流程UI与输入档 | **ALL PASS**（P1~P7） | |
| 2_13_批D_四职责承接 | **ALL PASS**（P1~P6） | |
| 2_17_步骤14_抽象经济 | **ALL PASS**（P1~P6+#9/#12） | 首轮 P2 False 归因见下 |

Smoke_14 首轮 P2（玩家零回归负探针）False 归因：**裸 Play 无开局链 → 玩家国未注册 → 探针首行 `reg.Get(0)==null` 早退**（旁证=批D DIAG「玩家国未注册→兜底注册 id=1」）。`EnsurePlayerRegistered()`（D303 正式 API）后复跑 **ALL PASS，P2=True**。非经济回归。

工装备忘：裸 Play 直跑 Smoke_14 须先执行 `KingdomRegistry.Instance.EnsurePlayerRegistered()`（真实进局则无此问题）。

### 2. 行为级全交互（真实进局=Menu→NewGame 全链真实虚拟设备点击）

**L142 硬性条目达成路径**（每步均为 Input System 真事件，非 API 直调）：

1. MainMenuScene Play → 主菜单渲染
2. 「新建游戏」点击 → OnNewGameClicked 真触发（三槽满占用拒绝分支真跑，4 条 `[MainMenu] 三个槽位都被占用` 日志为证）
3. 「继续游戏」点击 → SaveSlots 面板真切换（GameStateManager MainMenu→SaveSlotSelect）
4. 清槽（§五-3 工装绕行：`SaveManager.Delete` 生产 API）→「返回」→ MainMenu
5. 「新建游戏」→ CharacterCreation（真切换）
6. 「开始游戏」（creation-confirm-button）→ 进局 **GameScene / Playing / 玩家国自动注册（河谷王国 id=0）/ 256×256 真地图**

**进局后三交互**（timeScale 冻结消除单位游走错位后）：

| 交互 | 结果 | 证据 |
|------|------|------|
| 左键点选 | ✅ selected=1（Worker） | **down/up 双事件探针实证成对发布**（`[vinput探针] pressed @(894.63,167.04)` + `released` 同点）→ OnLeftClickPressed(IsDragging) → OnLeftClickReleased(<5px) → ClickSelect → OverlapPoint 命中 |
| 左键框选 | ✅ selected=2（Worker+Resident） | mouse_drag down→移动→up，距离>5px → BoxSelect → OverlapAreaAll → 多选 |
| 右键 | ⛔ 工具阻塞（§五-1/2） | 本批右键代码零改动（diff 确认）；沿用 HH.46 全真端到端证据 + Smoke_AB P3~P8 现行覆盖 |

**左键捕获层自动化边界收敛（任务书验收核心）达成**：HH.46 时代左键只能反射直调（legacy 轮询不可注入）→ 本批事件化后 **down/up 全真链路可自动化**（探针+点选+框选均虚拟设备真事件驱动）。

工装口径备忘：单位游走会致「点击-查询错位」假阴性（首击未中，冻结 timeScale=0 后一次命中）——非代码缺陷，自动化验证须冻结或即时闭环。

### 3. P3 失败归因（交接遗留疑云，本轮钉死）

P3Diag 五步实跑（有资源环境）+ 代码确证：

- ③ `SnapWorld(30,40)` 偏移 0.00 —— **寻路2 snap 无嫌疑**
- ④ 分派 = `PrioritizeHarvest=True / UnitCommandEvent=False` —— 右键(30,40) 附近有资源点（树木区@(25,25)）+ 全工人 → **D115 优先采集分支优先**
- ⑤ `_destination` 未设 —— 采集分派不设直移目标（设计如此）
- 代码正源：`SelectionController.IssueRightClick` 分派优先级 Follow → D116 → **D115（allWorkers && nearResource）** → MoveTo（[SelectionController.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Interaction/SelectionController.cs#L163-L185)）

**结论：P3 探针断言「UnitCommandEvent 发布」只在无资源世界成立；有资源环境走 D115 是设计行为。前任/用户所见 P3 失败=有资源世界（真局/出图残留环境），本轮 AB 冒烟 ALL PASS=裸 Play 无地图恰好走 MoveTo。非输入栈回归、非寻路2 snap 问题。**

待裁决：P3 探针口径修正（断言「UnitCommandEvent OR PrioritizeHarvestCommand 二选一」或 fixture 固定注入无资源世界）——涉及验收口径，归策划端。

## 四、Console 残留甄别

- VisualScripting「233 node options failed to load」：陈疴噪音（引用步骤14 已退役类型），建议顺手 Regenerate Nodes，非本批义务
- `MapGenDebugDrawer.OnDrawGizmos → DrawClimateZones`（[MapGenDebugDrawer.cs:44](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/MapGenDebugDrawer.cs#L44)）NRE 每帧刷屏：既有代码在冒烟注入环境（无 climate 数据）下渲染异常，Play 态 Gizmo 噪音，不影响逻辑，只登记不修
- 编译：删临时文件后 force 编译 **0 error** + Rider csproj 同步完成

## 五、工具缺陷/工装限制登记（诚实分层）

1. **unity_input mouse button 参数校验自相矛盾复现**（HH.46 §三-4 同款）：`button` 传 `"1"`/`1` 均被 schema 拒绝 → 右键无法经 unity_input 注入
2. **虚拟设备 rightButton 状态异常**：CodelyVirtualMouse 手动 QueueStateEvent buttons=2 + pump 后 `rightButton.isPressed=False`（leftButton 同法正常）+ RightClickPressedEvent 探针零达——右键注入双通道全堵
3. **动态创建 UI Toolkit 按钮虚拟设备点击不触发**：SaveSlots 删除钮（runtime `new Button`）4 击均无 PointerEnter/clicked（静态 UXML 按钮全通；panel.Pick 命中正确；两 PEH 均 active）——疑似帧驱动/分发差异未深挖（成本裁定）。**本轮以 `SaveManager.Delete` 生产 API 清槽绕行（删除按钮的 UI 触发路径行为级未验，登记）**
4. **编辑器后台无自然帧**：跨调用「注入→等帧」策略失效（事件躺队列）；`InputSystem.Update()` 手动 pump 可达 onEvent 层但帧驱动系统（EventSystem.TickModules 等）不跑——自动化验证须同调用内闭环或聚焦编辑器
5. m_InputModules 空 + module 注册时序：Unity 后台时 EventSystem.Update 未跑所致，前台恢复（观察项，不阻塞）

## 六、未完成/待裁决汇总

| 项 | 状态 | 建议 |
|----|------|------|
| 右键行为级本批内补验 | 工具阻塞（§五-1/2） | 接受既有证据（本批右键零改动+HH.46 全真端到端+Smoke P3~P8）；工具缺陷向 bridge 维护方反馈 |
| P3 探针口径修正 | 待裁决（§三-3） | 策划端定口径后小修冒烟脚本 |
| SaveSlots 删除按钮 UI 触发路径 | 未验（§五-3） | 物理鼠标人工点一次即验；或并入下批冒烟 |
| Smoke_14 环境前置 | 工装备忘（§三-1） | 裸 Play 跑 Smoke_14 先 EnsurePlayerRegistered |
| MainMenuScene 收编 | 随批呈报（§二） | 随本批 commit |
| VS 233 节点缓存 | 建议 | Regenerate Nodes（非本批义务） |

## 七、纪律合规声明（诚实分层）

- 未 commit ✅（策划端代执）；禁区四项未触碰 ✅；CameraRig/InteractionManager legacy 零改动 ✅
- git diff 在场自查：本轮完成且终态复核（接手时前手未完成项清偿）✅
- 临时清理：`ValleyInput1_P3Diag.cs`+`.meta` 用完即删 ✅；`.codely-cli/tmp/input1_edits.ps1` 删除 ✅；删后编译 0 error+csproj 同步 ✅
- 桥残留 job：接手时 24 个已自消化，全程 0 新增堆积 ✅
- 冒烟执行纪律：一套一调用、短超时、跑完即读 Console（交接教训执行到位）✅

## 八、建议 commit 口径（策划端代执参考）

1. `fix(输入1): 左键框选事件化——leftClickRelease(ReleaseOnly)+LeftClickReleasedEvent+SelectionController 订阅化，legacy 轮询退役（HH.49）`
   （GameInput.inputactions / GameInput.cs / GameEvents.cs / InputManager.cs / SelectionController.cs）
2. `chore(scene): MainMenuScene EventSystem 切 InputSystemUIInputModule（收编，HH.46 §三-5 裁决落地，HH.49）`
3. 美术轨 3 文档 M 不在本批，策划端并行产物自行处置

---

## 策划裁决（2026-09-02 策划端，验收成立 · D473=0.6 §四十八）

**验收成立**（策划端实盘复核：6 文件 diff 全量核读与 §二 逐项吻合——GameEvents 事件镜像样式/InputManager 守门三件套+Cleanup 成对退订/SelectionController legacy 轮询退役+订阅成对+右键零改动/场景 GUID 4f231c4f→01614664 精确切换/inputactions leftClickRelease=press(behavior=1) ReleaseOnly 正确/生成文件 FindAction+property 在场；行为级=冒烟四场 ALL PASS+Menu 全链真实点击 down/up 探针成对实证）。

三裁决（D473）：
1. **右键行为级=接受既有证据**（本批右键代码零改动 diff 确认+HH.46 全真端到端+Smoke P3~P8 现行覆盖）；工具阻塞双通道诚实申报=嘉奖，维持挂账池。
2. **P3 探针口径修正=断言改「UnitCommandEvent OR PrioritizeHarvestCommand 二选一」**（P3Diag 五步钉死：有资源环境走 D115 优先采集=设计行为，原断言只在无资源世界成立——探针环境敏感非产品缺陷；fixture 注入无资源世界不采纳）；冒烟脚本小修随下批顺手，不立专项。
3. **MainMenuScene 收编确认**（HH.46 §三-5 裁决落地闭环）。

附裁：Smoke_14 裸 Play 前置 EnsurePlayerRegistered 工装备忘采纳；SaveSlots 删除按钮 UI 触发路径+虚拟设备动态 UI 按钮点击不触发两笔入挂账池。commit 按 §八 口径分批代执（代码 31c458f/场景 91a1bc1）。
