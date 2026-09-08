# HH.111 任务书：战争机器生产入口批（P8 首案玩家侧处置 · D564 排程转正）

> 签发：策划端 2026-09-08（HH.108 验收串兑现 D564 承诺）｜ 账本：HH.111 🟡 / HH.112 🔵预留（完成报告）
> 接收方：执行端（TraeCode）｜ 排期：**0.5~1 天**（接单口径=HH.109 收口后，链序见 D564）
> 前置：**须在 HH.107 已合入基线上施工**（本批复用其 ProduceMachine/TreasureVault 扩面）

---

## 〇、背景（P8 首案玩家侧处置，D558 ④ 候选落定）

战争机器产线现状：产地（战争机器工坊）+生产函数（SiegeProductionSystem.ProduceMachine 玩家/AI 双 overload+IsRaceAllowedMachine 族白名单+上限/扣费）+厂级弹药仓全在——**唯缺触发端**（游戏内零调用，仅冒烟探针）。本批补玩家侧入口；**AI 触发方维持归 2_18 军事期不变（D558/D561 两裁），本批不含**。

**本批边界红线**：只做玩家 UI 入口+接线，禁碰 ProduceMachine 校验逻辑/白名单表/上限公式（已验既有语义，D558 红线=族检硬红线，改即破坏）。

---

## 一、施工详单（件1~3）

### 件1 生产面板 UI
1. 战争机器工坊建筑交互入口：仿既有建筑交互面板先例（BuildingPanel 交互模式）——选中本族战争机器工坊→「生产」按钮→Push 生产面板（IUIPanel 栈，TrainingPanel 同构）。
2. 面板内容（数据源=IsRaceAllowedMachine+SiegeProductionConfig+ProduceMachine 同源，禁抄表）：
   - 本族可用机器列表（族检过滤：人=重弩炮/矮=臼炮/精=藤蔓弹射器/兽=攻城槌；**共通投掷机/他族机器一律不显示**——白名单负向不可见）
   - 每项：名称（TrainingPanel.OccName 同源）+造价（SiegeProductionConfig.GetMachineCost 同源）+当前余量（GetMachineLimit−GetPlacedMachineCount）
   - 置灰条件：上限满/资源不足（CanAfford 同源判定）——**prefab 缺失的三族机器（臼炮/藤蔓/攻城槌）本批正常显示可点**（美术批7 prefab 到位后 UnitFactory 自动恢复生成；生成失败路径见件2.3 兜底）
3. UI 走 USS 类名规范+事件驱动刷新（TrainingPanel 刷新策略先例）。

### 件2 生产接线
1. 面板「生产」→进入放置模式（放置栈条目先例）→玩家点选地面合法格→`SiegeProductionSystem.ProduceMachine(type, spawnPos)`（玩家 overload——扣费/白名单/上限校验全在函数内，UI 只传 type+pos）。
2. 放置合法性与建筑放置规则对齐（不可放水/占格——复用既有放置校验；具体校验面执行端核对列报）。
3. **失败兜底可见性**：ProduceMachine 返回 false 的三分支（白名单/上限/资源）各有 Debug.Log——UI 侧 toast 提示对齐（ToastManager 先例）；UnitFactory prefab 缺失拒生成→toast「图纸绘制中（美术批7）」列报口径。
4. 产出即入编队/作战链=既有 UnitFactory/编队系统零改动（机器生成后走既有单位链）。

### 件3 探针（行为级）
- **P1** 人类局全链：点厂→面板列重弩炮（且无他族机器）→选型→放置→生成成功→厂仓弹药可装填（对齐 HH.107 P4 链）。
- **P2** 族检负探针：矮人局面板不显示重弩炮（白名单 UI 过滤）+直调 ProduceMachine(Ram) 拒（白名单日志）。
- **P3** 上限负探针：2 台满→第三台拒（「机器已达上限」toast+日志）。
- **P4** 资源负探针：扣费不足拒（「资源不足」toast）。
- **P5** prefab 缺失口径：三族机器点生产→UnitFactory 拒生成→toast 列报口径实测（美术批7 后自动消除）。
- **P6** 四容器回归玩家零回归+编译 0 警 0 错。

## 二、排雷（M1~M5）

| # | 雷 | 处置 |
|---|---|---|
| M1 | 族检硬红线 | IsRaceAllowedMachine 禁改——UI 过滤只读调用，禁复制白名单逻辑（单源） |
| M2 | AI 触发方 | 本批禁接 AI 行动池（归 2_18）；禁动 ProduceMachine AI overload |
| M3 | 既有校验逻辑 | ProduceMachine 内校验禁改；UI 侧判定仅用于置灰，真校验以函数返回为准 |
| M4 | HH.42 落盘幻觉 | grep 双锚点+git diff 自查=交付前置 |
| M5 | 文件面 | 新 UI 文件+SiegeProductionSystem 只读引用+ToastManager（若接）；开工前 git status 划界列报 |

## 二·补、D579 搭车两笔（2026-09-08 HH.115 验收串追加，用户拍板）

- **补笔A 出册日志静默**：PopulationSystem 出册日志（L169 附近，逐条出册）改静默+注释（与 D563② 入册 L160 汇总口径对称——人口计数对账价值由 LogCountSnapshot/开局汇总行承载；六考长局每次单位死亡一条=刷屏面）。
- **补笔B 战斗降速域死代码收口**：TimeManager `IsCombatSlowed` 恒 false 死分支三处（Update L162/SetGameSpeed L319/L387）+`ExitCombatSlow`/`HasActiveEnemies`（EnterCombatSlow 删除后永不触发）删除；`Valley_HH80_Run.cs` L72 过时注释（「夜战锁 1x 由游戏内 EnterCombatSlow 自动——损耗计入」）同步更正（HH.80 容器语义=主加速 3x 即可，删去死引用半句）。
- 两笔合计约 20 行+1 注释；不改变本批探针与验收口径（P1~P6 照跑）；git diff 自查清单加入两笔涉及文件（PopulationSystem/TimeManager/Valley_HH80_Run）。

## 三、验收口径

- P1~P6 全绿+四容器零回归+UI 三负探针（族检/上限/资源）行为级
- 完成报告=**HH.112**（教训核查行+git diff 自查）
- 策划端将抽验：面板数据源单源性（禁抄表）/族检负探针/ProduceMachine 零改动 diff 确认

---

*签发：策划端 2026-09-08（HH.108 验收串兑现 D564）。账本占号 HH.111/112 后落盘。*
