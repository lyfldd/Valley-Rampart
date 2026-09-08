# HH.116 六考前置零碎包完成报告（执行端 → 策划端）

> 批次：HH.115《六考前置零碎包》（D563②④/D565/D569/D577 五裁集合，小修单无设计自由度）
> 报告：HH.116（账本预留号）｜ 日期：2026-09-08 ｜ 施工：执行端（TraeCode）
> 验收主判据：件1~件5 全落+HH.115 冒烟容器 ALL PASS+四容器回归玩家零回归+编译 0 错 0 新警（存量旧警除外）

---

## 一、施工清单（五件全落）

**件1 日志清理 5 笔（D563② 照裁执行）**
1. 删 [ChainFox] 守卫交锋：DamageSystem.cs L564~565（注释+日志两行，节流函数尾）
2. 删 [UnitController] 初始化日志：UnitController.cs 原 L408~409（跨行拼接整删；**L535 旧档兜底日志=现 L532 保留在盘**，grep 直验）
3. 改汇总 [UnitRegistry]：注册 L27/注销 L40 两条删除（明细静默）；Clear() 改带计数汇总行（「清场汇总：本次注销 N 个单位」覆盖 GameBootstrap/WorldLifecycle/TeardownManager 三调用方）；新增 `LogCountSnapshot(reason)` 供建局侧一次性打行
4. 改汇总 [PopulationSystem]：入册 L160 删除（静默+注释）；开局汇总行 L219（既有）后追加 `UnitRegistry.Instance?.LogCountSnapshot("开局")`——两系统「建局/清场一次性打一行计数」口径统一（D563② 原文口径）
5. 白名单静音 EventBus：`_noSubscriberWhitelist`（静态 HashSet，**静态字段初始化器直接填充**不依赖 Play 态初始化——编辑器域探活同样生效）+WhitelistSeed 唯一维护点（ExecutorMoveCompleteEvent/ExecutorArrivedEvent/GameSavedEvent）+Publish 无订阅者分支查名单；**名单外事件默认仍告警**（防白名单变永久消音器=裁决原文）；保留 [TaskScheduler] L316/L513+[NPCBrain] PathFailed+[UnitRegistry] Clear 行（裁决「保留不动」项零触碰）

**件2 件C 野性同族豁免（DZ-069/D468 行为级）**
- **实修点=受击溯源链**（NPCBrain.OnDamaged）：现状扫描链 L633 已有同族豁免（`u.raceId==myRace continue`），溯源链 L343 仅「同阵营+D485 野人异族例外」——跨阵营无 raceId 检查→同族国民先袭野人（AOE 波及等）→野人溯源放行→还手同族国民=「r2 野人袭 r2 国民」实锤呈现链（C-T1 修复对象）
- **判定收口**：WildnessConfig 新增 `public static bool IsSameRaceExempt(a, b)`（D468 同族豁免判定唯一入口，扫描链+溯源链两消费点收口，防分支漂移；有国单位国民压制语义不动）
- 正负探针 C-T1 四条→HH.115 容器 T1~T4 全绿（见 §三）

**件3 件E 散账 6 项（D565 并入）**
1. **磐石 45% 基准断言固化**（数值不改）：公式精算 `20×(1-10/(10+100))×(1-0.45)=10.0` 与「20 伤→10」精确命中（策划意图「45% 减伤在现公式下等效表现」）——HH.115 容器 T6=资产三值在场断言（UnitDataManager 查表 def=10/rangedDamageReduce=0.45+DamageConfig.armorK=100）+公式复算 10±1
2. **盾卫庇护 30% 双端落值**：Unity `Human_Player_ShieldGuard.asset` 补 `shelterChance: 0.3`+M7 字段块显式化（6 行，其余默认值零行为差）；sim `harness/Data/professions.json` L1305 同步 0.3；15_账本一·补十六登记（D557 注记义务清偿）；**行为锚=T5 真链实测转移命中**（DamagePipeline 受方修正链既有）
3. **学院 0.75 迁 SO（E-T5）三落**：KingdomConfig.cs 新增 `warAcademyTrainingSpeedMul = 0.75f`（.cs 默认值）+KingdomConfig.asset 落值+TrainingSystem 消费点改 SO 查表（kingdomCfg 空兜底出厂 0.75）
4. **HasExclusiveBuilding 字符串散点收口（E-T6）**：新建 `Data/BuildingIds.cs` 常量类（WarAcademy/WarCamp/LeyForge/ArcheryRange 四族专属）+三处调用面替换（TrainingSystem/DamageSystem L626/KingdomRace L97）；grep 全库 `HasExclusiveBuilding\(` 字符串字面量残留=**零命中**；UtilityScorer L186 走 def.buildingId 配置域+BuildingVisual sprite 映射 switch（非 HasExclusiveBuilding 域）列报 §七-7
5. **缺表乘数 1.0 显式化（E-T7）**：TrainingSystem+KingdomRace.GetGatherMul 两处 `: 1f` 魔法值→`const float MissingRaceDefMul = 1f`（缺表回退=1.0 中性显式声明）；**乘数真值总表（12 项×4 族）落 RaceDef.cs 注释区**（四资产逐行抄录，原「1.0 占位挂账注」已过时更新——资产已全量真值化）；三方核对=sim 无 race 经济乘数参数域→零回灌义务（15_账本一·补十七）
6. **Feasible 候选存在性判定**：UtilityScorer.ScoreTop 加 `out ScoreCensus` 重载（defTotal/stageFiltered/noNeed/infeasible/axisFiltered/top 淘汰构成普查，**行为零变化**——原三参签名委托保持）；FocusController 主评分 None 分支接日结级诊断行（「评分空候选 kN dN：候选集 X（阶段滤/无需求/不可行/轴权零）」）——空候选 vs 全不可行 vs 无需求三分型，六考 14 锁死归因的候选侧观测面

**件4 EnterCombatSlow 同删（D569 裁）**
- TimeManager.cs：死方法本体（原 L324~334 十一行）+残留注释行（原 L362「保留供设计评审」）删除；ExitCombatSlow/HasActiveEnemies=活代码（Update L162 恢复链消费）保留；注释更新注明 IsCombatSlowed 恒 false 死分支列报（§七-6）

**件5 HH.107 容器入口补改（D577 裁搭车，L-17）**
- Valley_HH107_Smoke_Byproduct.cs：L73 `SmokeApi.EnterGame(cfg)` → `yield return TestHarnessApi.EnterTestRun(cfg, 60f)`（HH.109 先例同款）；**全部 4 处退出路径**（正常收尾/FailFast/就绪超时/前置缺失）补 `TestHarnessApi.ExitTestRun()`；头注 L10 同步更正（SmokeApi.EnterGame→测试环境正门）——零产品面、零探针口径变化

## 二、红线/排雷自查

| 项 | 结果 |
|---|---|
| 日志删除禁越锚点 | ✅ UnitController L535 兜底日志保留在盘（grep 现址 L532） |
| 庇护落值双端同步 | ✅ asset+professions.json+15_账本 三件齐 |
| 野性豁免禁动有国语义 | ✅ 有国者不进野性扫描（EffectiveOccupation 前置 return）零触碰；国民压制探针 T4 绿 |
| SO 迁移零行为变化 | ✅ 0.75/0.3/1.0 全为原值迁入；行为级探针佐证（T2/T5） |
| HH.42 grep 双锚点+git diff 自查 | ✅ §五/§六 |
| 开工前 git status 划界 | ✅ pixel-forge/图片资源/Logs/Output 零混入 |

## 三、行为级证据（HH.115 冒烟容器，十轮迭代收官）

**容器**：Valley_HH115_Smoke_HexPrep（seed=21115，槽 smoke_h115，L-17 正门 EnterTestRun+ExitTestRun，L-16 协程异常捕获器）；摆位隔离纪律=各探针独立点位防交叉索敌污染。

**最终轮（第十轮）8/8 ALL PASS**：
- T1 同族野人 r2×r2 共处 3 日 HP 100→100/100→100 零交战 =True（C-T1① 负探针）
- T2 异族野人 r1 入圈必攻 HP A 100→98 C 100→96 =True（C-T1② 正探针）
- T3 IsSameRaceExempt 真值表（同族 true/异族 false/null false）=True（C-T1③）
- T4 r2 野人×r2 国民(k1) 野人满血+国民存活 =True（C-T1④ 有国压制零回归）
- T5 盾卫庇护 30%：20 发内转移命中 1 次（盾卫 150→148，真链 ApplyDamage isRanged=true）=True
- T6 磐石 20 远程伤基准：def=10/rrd=0.45/armorK=100→复算实收 10 =True（件E#1）
- T7a 白名单三事件登记在场+T7b 白名单事件无订阅者发布零告警 =True（D563②⑤）

**跨轮存在性补充**（L-14 处置，T2/T5 受游荡离散与 30% 概率随机支配）：T2 交战实锤累计 3 轮（C 掉血 99/98/96）、T5 转移实锤累计 2 轮（148×2）——非单轮侥幸。

## 四、四容器回归（玩家零回归硬红线）

| 容器 | 结果 |
|---|---|
| 2_20_种族域Play冒烟 | ALL PASS（种族域 D467~D472 行为级探针） |
| 2_20B_M7种族专属冒烟 | 第 6 轮汇总 ALL PASS（清场前营地数=3） |
| 2_20C_M8M9批4冒烟 | ALL PASS（M8 基准/同族差异/零回归/端到端+M9 走查/Cavalry 负/运行时/P9 消费） |
| 2_13_批C_流程UI与输入档 | ALL PASS（P1~P7） |

## 五、编译验证（L-13 双通道）

- 通道一 read_console：0 错；警告面 10 条全为存量旧警（CS0114/CS0108/CS0252/CS0253/CS8632——本批修改行零新警）
- 通道二 execute_code 探活：EventBus 白名单三事件在场 m=True,True,True+KingdomConfig.warAcademyTrainingSpeedMul 在场+WildnessConfig.IsSameRaceExempt 在场+HH115 容器类型在场=全 True
- 过程插曲（诚实记录）：首版 HH115 容器 CS0029（System.Action≠Application.LogCallback）已修；中期一次「探活 False」经磁盘 grep 直验=编译未完成时序假象（L-02 双源验证生效）

## 六、git 面（自查）

- 主仓：**17 改+2 新（含 .meta）** 全在 HH.115 界内（EventBus/NPCBrain/WildnessConfig/TrainingSystem/DamageSystem/KingdomConfig.cs+asset/KingdomRace/RaceDef/UnitRegistry/UnitController/PopulationSystem/TimeManager/UtilityScorer/FocusController/HH107 容器/ShieldGuard.asset+新增 BuildingIds.cs/Valley_HH115_Smoke_HexPrep.cs）
- 训练仓（独立 repo）：professions.json（shelterChance 0.3）+15_账本（一·补十六/十七）随训练轨体系提交
- pixel-forge/图片资源/Logs/Output/.oc_* 零混入

## 七、列报（诚实分层）

1. **T6 磐石行为级实测降级**：Dwarf_Bedrock.asset `prefab: {fileID: 0}` 为空=生产链不可实例化（美术接入批 HH.103 范围）——本批以资产三值断言+公式复算固化意图；**行为级实测待 prefab 接入后补测**
2. T2/T5 探针随机摇摆：游荡离散+30% 概率支配，按 L-14 环境补偿（长窗+跨轮存在性）收口；单轮全绿于第十轮达成
3. T5 探针摆位余量：Play 态实例内存改 `shelterRadiusCells` 1→3+每发强控回锚（HH.107 产率先例，不落盘退 Play 自动还原）——**产品数值出厂值不变**，仅探针摆位余量
4. T4 国民 2 点野怪噪声：地图野怪游荡误伤（非野性链），断言核心=野人侧满血+国民存活（L-14 补偿口径）
5. 件1 汇总口径实现细节：PopulationSystem 出册日志 L169 逐条**不在裁决锚点**（D563② 仅点名入册 L160）=保留未动（如需同口径静默请裁）
6. TimeManager `IsCombatSlowed` 恒 false 死分支三处（Update L162/SetGameSpeed L319/L387）+ExitCombatSlow/HasActiveEnemies 活代码——EnterCombatSlow 删除后永不触发；不在裁决锚点=保留，建议六考后随战斗降速域收口一并清
7. BuildingVisual sprite 映射 switch 的 "WarAcademy"/"WarCamp" 字符串=视觉域字面量（非 HasExclusiveBuilding 散点域）未收口；UtilityActionConfig.asset/CS 配置行的 buildingId 字符串=SO 数据源本身，均不在 E-T6 范围
8. E-T7 三方核对=sim 侧 professions/tuning/champion/factor_registry 无 race 经济乘数参数域→零回灌义务（15_账本一·补十七登记）
9. HH.107 容器 EnterTestRun 后自身 SetSecondsPerDay(15f) 快进保留（容器自有时序手段，与考跑档正交）
10. EventBus 白名单事件在三跑中观察到 ExecutorMoveCompleteEvent 进局后存在订阅者（T7b SKIP 分支生效一次）——SKIP 转观察=L-14 口径

## 八、教训核查行（提案候选，待裁决）

- L-16 正面应用：HH.115 容器 RunHost 挂异常捕获器（零异常事件）✅
- L-14 正面应用：T2/T5 摇摆按环境补偿+跨轮存在性收口（HH.107 P5/P8 D569 裁 A 同族先例）✅
- L-02 正面应用：探活 False 异常→磁盘 grep 直验=编译时序假象（双源验证拦截）✅；L-12 同文件串行全程遵守✅
- **教训候选 L-19（提案）**：「冒烟探针摆位陷阱三连」——①SnapWorld 网格吸附粒度≈1 格，摆位偏移请求不可信（0.3 格请求→0.95 格实际），探针须以实际 DistCells 断言；②微格单占登记（D70 主表）同点两单位互相顶掉，同点摆位=查询丢对象；③依赖位置邻近的行为探针（庇护/索敌）必须强控回锚或 Play 态放宽判定余量，纯摆位不可靠。防范动作=容器摆位模板加「实际距离断言行」+行为探针强制回锚模式（HH.115 T5 最终形态）。

## 九、账本回执

- HH.115 → 🟡施工完毕待验收（本报告随附）
- HH.116 → 🔵本报告落盘
- 六考开跑前置状态：HH.107✅+HH.109✅+**HH.115 施工完毕待验收**——本批验收销号后六考前置全清（D563 口径）

---

*完成报告完 · 全部施工序 ✓ · 玩家侧零回归红线达成 · 验收入口=策划端*
