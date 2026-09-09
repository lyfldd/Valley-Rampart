# HH.112 战争机器生产入口批完成报告（执行端 → 策划端）

> 批次：HH.111《战争机器生产入口批》（D564 排程转正 / D558④ P8 首案玩家侧处置 / D579 二·补两笔搭车）
> 报告：HH.112（账本预留号）｜ 日期：2026-09-09 ｜ 施工：执行端（TraeCode·Unity 轨）
> 验收主判据：件1~件3 全落+二·补两笔全落+HH.111 冒烟容器两局 11 探针 ALL PASS+四容器回归玩家零回归+编译 0 错 0 新警

---

## 一、施工清单（件1~件3+二·补两笔全落）

**件1 生产面板 UI**
1. **MachinePanel.cs（新，`Systems/UI/`）**：IUIPanel 栈面板（TrainingPanel 同构：SetTarget→Push→Open/Close/Refresh+事件驱动刷新订阅 UnitDiedEvent/RulerResourceChangedEvent）。「生产」按钮点击→prefab 预检→进放置模式。渲染载体=**运行时动态构建 UIDocument+PanelSettings（ToastManager 先例，零场景/prefab 资产改动）**，USS 类名=machine-* 规范+inline style+UIDragHelper 拖动（先例同款）。
2. **数据源单源（任务书件1.2 禁抄表三条全兑现）**：本族机器列表=`MachinePanel.GetVisibleMachineList(race)`（内部调 `SiegeProductionSystem.IsMachineAllowed` 单源转发）——共通退役槽 SiegeMachine/他族机器一律不显示；名称=`TrainingPanel.OccName(occ)` 同源调用；造价=`PeekMachineCost` 单源转发；余量=`GetMachineLimit−GetPlacedMachineCount`；置灰=上限满/资源不足（`RulerController.CanAfford`）。
3. **BuildingPanel 交互入口**：`RefreshFunctionArea()` 加 `def.id == "SiegeWorkshop"` 分支→「生产」按钮→`OnMachineProduceClicked`→`MachinePanel.Instance` SetTarget→Push（TrainingPanel 入口同构）。
4. **TrainingPanel 同源化**：`OccName` private→public（供 MachinePanel 调用，名称表单源禁抄）+补 `case Occupation.Ballista: return "重弩炮"`（D496 收编名，名称表自身缺行——原 default 回退 "Ballista" 英文）。

**件2 生产接线**
1. **MachinePlacement.cs（新，`Systems/Building/`）**：`MachinePlacementEntry : IUIStackEntry`（**BuildModeEntry 虚拟栈条目先例同构**：Open→进放置，Close→退出+重开 MachinePanel 刷新余量）+`MachinePlacer : Singleton`（ghost 跟随+微格吸附+绿/红反馈，BuildController.Update 先例同构；输入复用 InputMode.Build 禁交互点击，鼠标 legacy Input 轮询=BuildController 同款——Build 模式下 InputManager 不发事件）。
2. **放置合法性（件2.2 与建筑放置规则对齐）**：`MachinePlacer.CanPlaceCell(cell)` static——界内+非水（WalkFlags.Water）+可走（IsWalkable）+非障碍（IsObstacle）+无建筑占格（BuildingRegistry.GetAt），API 与 PlacementValidator 全同源（GridSystem/BuildingRegistry）；static 供探针直调。核对列报：机器=1 格无 footprint，桥水域特例/城门拐角/接岸/资源点/资源扣费五项不适用（无 BuildingDef 载体），实质校验面=水/障碍/占格三项+越界。
3. **失败兜底可见性（件2.3）**：放置点击瞬间 UI 侧预检（上限满→「机器已达上限 N（需升级战争机器工坊）」/资源不足→「资源不足，无法生产机器」/放置点非法→「此处不可放置（水域/障碍/占格）」toast+日志，对齐 ProduceMachine 内三分支日志语义；**M3 真校验以 ProduceMachine 返回为准**，预检仅文案对齐）；prefab 缺失在面板点击时被 `IsPrefabMissing` 预检拦截（toast「图纸绘制中（美术批7）」，不进放置不扣费）。
4. **生产调用**：`SiegeProductionSystem.ProduceMachine(type, spawnPos)` 玩家 overload（扣费/白名单/上限校验全在函数内，UI 只传 type+pos）；产出走既有 UnitFactory/编队链零改动（件2.4）。成功/失败均 Pop 回面板（余量/置灰刷新）。

**件2 前置（SiegeProductionSystem 只读门面，+8 行净增）**
- `public static bool IsMachineAllowed(int race, Occupation type) => IsRaceAllowedMachine(race, type);`
- `public ResourcePack PeekMachineCost(Occupation type) => GetMachineCost(type);`
- **git diff 直读实锤：ProduceMachine 两 overload/IsRaceAllowedMachine/GetMachineCost 本体零改动**（M1 族检硬红线+M3 校验逻辑零改动达成，策划端 diff 确认面）。

**件3 探针（行为级，两局方案）**
- 容器=`Valley_HH111_Smoke_MachineEntry.cs`（新，seed=21111 人类局/21112 矮人局，槽 smoke_h111，菜单 Valley/验证/HH111_战争机器生产入口；L-17 正门 EnterTestRun+ExitTestRun 全退出路径收尾，L-16 RunHost 协程异常捕获器，EnterTestRun 走 EnterGame 真实建局链路连续两局）。
- 探针与面板共用同一数据/判定函数（GetVisibleMachineList/IsPrefabMissing）=行为级锚不漂移；建筑直建走 BuildingFactory 生产链（HH.107 BuildAt 先例，SiegeWorkshop 同款直建）。

**二·补A 出册日志静默（D579⑤）**
- PopulationSystem.UnregisterEntity 出册 `Debug.Log` 删除+注释（与 D563② 入册 L160 汇总口径对称，对账由 LogCountSnapshot 承载）。

**二·补B 战斗降速域死代码收口（D579⑥）**
- TimeManager.cs：`IsCombatSlowed` 属性+恒 false 死分支三处（Update 恢复链/SetTimeScale L319/SetGameSpeed L375+L380 三元）+`ExitCombatSlow`+`HasActiveEnemies` 全删（原 L75/L162/L282/L309/L319/L325/L336/L375/L380 九处引用清零）；相关注释三处同步（ResetState/「倍速控制+战斗降速」区头/SetGameSpeed summary）；`_pendingScale = _simLocked1x ? 1f : _pendingScale` 进分支恒真简化为 `= 1f`（语义不变）。
- Valley_HH80_Run.cs L72 注释更正（「夜战锁 1x 由游戏内 EnterCombatSlow 自动——损耗计入，R3」→「战斗降速域已随 HH.111 补笔B 收口删净，主加速 3x 即可——D579⑥」，删死引用半句）。

## 二、红线/排雷自查（M1~M5）

| # | 雷 | 结果 |
|---|---|---|
| M1 | 族检硬红线 IsRaceAllowedMachine 禁改 | ✅ git diff 直读=方法本体零改动；UI 只走 IsMachineAllowed 单源转发 |
| M2 | AI 触发方禁接 | ✅ ProduceMachine AI overload 零触碰（diff 实锤）；UtilityScorer/行动池不在 git 变更面；AI 行动池归 2_22 P0 不动 |
| M3 | 既有校验逻辑零改动 | ✅ ProduceMachine 两 overload diff 零变化；UI 预检仅置灰/文案，真校验以函数返回为准 |
| M4 | HH.42 落盘幻觉 | ✅ grep 双锚点+git diff 自查（§六）+L-13 双通道编译验证 |
| M5 | 文件面划界 | ✅ 施工件=6改+3新含 meta 全在批内（§六）；pixel-forge/3.1.2/图片资源/Logs/Output/.tmp-* 零混入 |

## 三、行为级证据（HH.111 冒烟容器，两局 11 探针首跑全绿）

**局1 人类局（seed=21111）**：
- P1a 人类可见机器=[Ballista]（他族+退役共通槽不可见）=True
- P1b 工坊直建 Lv.1 弹药仓就绪=True（3 子仓）
- P1c 合法格=True 生产=True 场上机器 0→1 Ballista 在场=True（真链 ProduceMachine→UnitFactory）
- P1d 产石弹=5 弹仓 0→5 可取=2 国库石 235→230=True（厂仓弹药可装填，HH.107 P4 链口径）
- P2a 直调 ProduceMachine(Ram) 拒=True 计数不变=True（白名单日志实锤）
- P4 金=0 生产拒=True 计数不变=True（资源不足）
- P3 产至 2/2（全成）→第 3 台拒=True 计数不变（上限）
- P5a 臼炮/藤蔓/攻城槌缺图=True+重弩炮 prefab 在场=True（反向锚，图纸口径）

**局2 矮人局（seed=21112, raceId=2）**：
- P2b 矮人局玩家族=2 可见机器=[Mortar]（重弩炮不可见=白名单 UI 过滤行为级）=True
- P2c 矮人局直调 ProduceMachine(Ballista) 拒=True（族检双族向实锤）
- P5b 现状列报：ProduceMachine(Mortar) 返回=True 金 2000→1985 场上 0→0（**扣费无生成=既有语义**，UI 预检兜正常流程不扣费；美术批7 prefab 到位后自动消除）=True

**判定：ALL PASS（11/11）**

## 四、四容器回归（玩家零回归硬红线）

| 容器 | 结果 |
|---|---|
| 2_20_种族域Play冒烟 | ALL PASS（种族域 D467~D472 行为级探针） |
| 2_20B_M7种族专属冒烟 | 六轮 ALL PASS（清场前营地数=3，跨轮实例/残留探针全过） |
| 2_20C_M8M9批4冒烟_自动跑 | ALL PASS（M8 基准/同族差异/零回归/端到端+M9 走查/Cavalry 负/运行时/P9 消费） |
| 2_13_批C_流程UI与输入档 | ALL PASS（P1~P7） |

## 五、编译验证（L-13 双通道）

- 通道一 read_console：0 错；警告面 5 条全为存量旧警（CS0162/CS0219/CS0472，2_17/2_21A/2_20 容器——本批修改行零新警）
- 通道二 execute_code 探活：MachinePanel.GetVisibleMachineList/IsPrefabMissing+MachinePlacer.CanPlaceCell+SiegeProductionSystem.IsMachineAllowed/PeekMachineCost 全在场+OccName public+OccName(Ballista)="重弩炮"+TimeManager IsCombatSlowed/ExitCombatSlow/HasActiveEnemies 反射全 null（删净实锤）=全 True
- 过程插曲：首版 MachinePanel CS1061（UIElements 无 borderColor 聚合属性→逐边四色）已修；"Import Error Code:(4)" 清台后未复现（导入缓存噪声）

## 六、git 面（自查）

- **主仓施工件 6改+3新（含 .meta）全在 HH.111 界内**：改=Valley_HH80_Run.cs（1 行注释）/BuildingPanel.cs（+17）/PopulationSystem.cs（5±）/SiegeProductionSystem.cs（+8 纯门面）/TimeManager.cs（-44+13）/TrainingPanel.cs（5±）；新=MachinePanel.cs/MachinePlacement.cs/Valley_HH111_Smoke_MachineEntry.cs 各含 .meta
- 河谷防线_开发计划书.md（2 insertions）=第〇步C HH.115 施工落库行（bullet+表格），随本批 commit 或单独提交请验收时裁定
- **多Agent交接/_编号登记.md 的 M 状态=训练轨侧 D580 串（HH.120 验收+T04 登记）并行产物非本批执行端改动，未混入**（列报请策划端/训练轨侧自行处置）
- pixel-forge×3/3.1.2/图片资源/Logs/Output/.oc_*/.tmp-* 零混入

## 七、列报（诚实分层）

1. **渲染载体偏差列报**：任务书件1.1/1.3 写「TrainingPanel 同构+USS 类名规范」——MachinePanel 实现为 **IUIPanel 栈/SetTarget/事件驱动刷新/UIDragHelper 全同构，渲染载体走 ToastManager 运行时动态构建先例**（零场景资产改动=git 面干净+免场景挂载风险）；类名 machine-* 规范+inline style（无独立 .uss 文件）。如策划端裁 UXML 化，补 3 文件+场景挂载即可（结构已预留）。
2. **ProduceMachine「扣费无生成」现状**（P5b 实测）：prefab 缺失时玩家 overload 先 Spend 后 SpawnUnit 拒（返回 true）——**既有语义零改动（M3）**；正常玩家流程被 MachinePanel.IsPrefabMissing 预检兜住（不进放置不扣费，P5a 图纸面实锤）。AI overload 同现状（AI 触发方归 2_22 P0，届时同预检口径）。
3. Shift 连放未实现：机器上限低（2+每级2），单发回面板=最小面；如需连放加 `Input.GetKey(KeyCode.LeftShift)` 一行级（BuildController 先例同款）。
4. 名称表补行：TrainingPanel.OccName 升 public（同源调用所需，零行为差）+case Ballista="重弩炮"（D496 收编名，名称表自身缺行非新增语义）。
5. P2 族检负探针=真实两局（人类局+矮人局 raceId=2，GetKingdomRace(0) 运行时读数=2）非数据面模拟；直调双族向实锤（人类局调 Ram 拒+矮人局调 Ballista 拒）。
6. 放置合法性核对列报（件2.2）：实质校验面=越界/水/障碍/建筑占格四项（PlacementValidator 同源 API）；footprint/旋转/桥/门/资源点/领内前置/资源扣费不适用（机器 1 格无 BuildingDef，资源校验在 ProduceMachine 内=M3 分工）。
7. TimeManager `_pendingScale` 三元简化：`_simLocked1x ? 1f : _pendingScale` 进分支时 `_simLocked1x` 恒真→`= 1f`（语义不变，diff 直读可验）。
8. 2_20C 回归首跑误读自纠：read_console filter 分页窗口截断 P4+ 探针行→误判「卡死」，实为 AutoRoutine 正常完成（汇总行 ALL PASS+「自动跑完成→清场退 Play」在场）；复跑验证全绿。教训=容器回归验收只认汇总行/判定行，filter 窗口截断不作否定性结论（L-02 双源验证正面应用）。

## 八、教训核查行

- L-16 正面应用：HH.111 容器 RunHost 挂异常捕获器（全程零异常事件）✅
- L-06 生产链：机器生成=ProduceMachine→UnitFactory 真链+工坊=BuildingFactory 直建（HH.107 BuildAt 先例）✅
- L-17 容器入口纪律：EnterTestRun 正门+ExitTestRun 两局全部退出路径（含 WaitWorldReady 超时/FailFast）收尾 ✅
- L-13 双通道：read_console+execute_code 探活齐备后才声明编译通过 ✅
- L-12 同文件串行：TimeManager 5 处编辑逐处 SearchReplace+立即验证 ✅
- L-02 双源验证：P4+ 行缺失否定性结论先 grep 汇总行复核→拦截误判（§七-8）✅
- 教训候选：**无新条提案**（2_20C 误读=read_console 工具分页行为认知，非项目流程漏洞，§七-8 如实列报即可）

## 九、账本回执

- HH.111 → 🟡施工完毕待验收（本报告随附）
- HH.112 → 🔵本报告落盘
- 六考开跑前置：本批验收销号后=最后一单收口（D564 总排：HH.111→P1 六考），策划端签发六考开跑
- 代码未 commit（纪律：验收成立后走 git-plan-sync；第〇步 HH.115 落库已先行完成=主仓 809693a+训练仓 d3e8cc5）

---

*完成报告完 · 全部施工序 ✓ · 玩家侧零回归红线达成 · ProduceMachine 校验逻辑零改动（diff 待抽验）· 验收入口=策划端*

---

## 十、策划裁决区（验收回写 2026-09-09，D582）

**验收结论：✅ 成立销号**（HH.111/112 同笔销号，0.6 §一百一十二）。

**红线复核全过**：M1=SiegeProductionSystem diff 净增恰 8 行纯转发门面（IsMachineAllowed=>IsRaceAllowedMachine/PeekMachineCost=>GetMachineCost），IsRaceAllowedMachine 本体零触碰；M2=AI overload 零改动+grep 补强（IsMachineAllowed/PeekMachineCost 全库消费端仅 UI 三文件，AI 侧零接线）；M3=两 overload/GetMachineCost 本体零变化，UI 预检仅置灰+文案；数据源单源四链全实锤（族检转发/名称 OccName/造价 PeekMachineCost/余量差值）；负探针=EnterTestRun 真实建局两局（seed 21111/21112）+GetKingdomRace(0)==2 运行时读数断言+P2a/P2c 直调双族向拒；四容器回归=采信（Play 独占不可本端复跑，D579 先例口径）；工作区 6改+3新+主计划书 2 insertions 逐件吻合、批外件零混入成立。

**§七 列报 8 项裁决**：
1. 渲染载体偏差=**接受现状（用户拍板）**：ToastManager 动态构建先例在案+零场景资产改动=git 面干净；逻辑层 IUIPanel 栈/SetTarget/事件驱动/UIDragHelper 全同构达标；UXML 化挂「UI/视觉小批总锚」候选（届时若裁 MachinePanel 随迁，结构已预留）。
2. 「扣费无生成」既有语义=**认可留待美术批7 自动消除（用户拍板）**：M3 红线内既有语义非本批引入+玩家流程 IsPrefabMissing 预检兜住不扣费（P5a 实锤）；**附带裁决细化=2_22 P0 批B（B8 机器双行动）实施时必须带 prefab 预检（同 IsPrefabMissing 口径，缺失机器不进行动池评分）防 AI 侧踩同坑**——已落 0.6 §一百一十二。
3. Shift 连放未实现=知悉采纳（上限低 2+每级2=最小面 YAGNI，需要时 BuildController 先例一行级）。
4. 名称表补行=知悉采纳（OccName public 化零行为差+Ballista=D496 收编名补行）。
5. 负探针真实两局=已验证确认（容器+读数断言+双族向直调三面实锤）。
6. 放置校验面=已验证 CanPlaceCell 逐项一致（界内/水/障碍/建筑占格；无 BuildingDef 五项不适用成立，资源校验在 ProduceMachine 内=M3 分工正确）。
7. TimeManager 三元简化=已验证 diff（进分支 _simLocked1x 恒真，语义不变）。
8. 2_20C 首跑误读自纠=知悉采纳（read_console filter 分页截断=工具行为认知非流程漏洞；L-02 双源验证正面拦截=教训有效性实证）。

**验收三问（vr-planner-leadership 钩子2）**：无失实申报——列报诚实分层（渲染载体偏差/扣费无生成/2_20C 误读三笔主动列报）；教训核查行 L-16/L-17/L-13/L-06/L-02/L-12 全正面应用；**教训核查无新增（钩子3）**。**嘉奖**：数据面断言与面板共用同一函数（GetVisibleMachineList/IsPrefabMissing）=行为级锚不漂移设计佳。

**后续**：9 件代码（6改+3新含 meta）+本报告+账本+主计划书（HH.115 落库行 2 insertions 一并）走 git-plan-sync commit；P1 六考开跑任务书=HH.122 已签发（多Agent交接/策划端/HH.122_P1六考开跑_任务书.md），六考前置全清（HH.107✅+HH.109✅+HH.115✅+HH.111✅）。