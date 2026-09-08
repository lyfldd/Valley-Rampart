# HH.95 训练端 2.0 世界模型改造 W 批 完成报告（待策划验收）

> 类型：完成报告（HH.94 任务书交付）· 日期：2026-09-07 · 训练师会话（TraeCode 训练轨，主指挥）
> 状态：🚧 待策划验收（验收口径=HH.94 任务书 §六 + HH.102/D553 条件 A/B 落实核验）
> 单一事实源：《HH.94_..._W批_实施清单.md》T0~T21 回执区（逐条状态+证据）+ §五·补 M 雷处置表。

## 一、指挥链声明（P0-3）

- **TraeCode（本会话）=主指挥**：读战报/定位病根/定方案/下达任务/亲跑验收/记台账/裁决偏离。
- **opencode=施工手**：本批共下达 5 个四件套任务（W-A1/A2/A3/B1/B2/C1，任务书全存 `ai决策大脑强化训练/opencode_tasks/`）；模型链 deepseek-v4-flash（失效）→ deepseek-v4-pro → glm-5.3-flash（主指挥 W-C1 起切换）。
- **申报不可信原则执行记录**：全部 opencode「已完成/全绿」申报均经主指挥亲跑验收后才采信/commit；两次 worker 偏离被亲验拦截定性（W-A2 的 S1 引用退役职业触雷停下待裁=正确行为；W-C1 的 tuning 缺键走容错路径=裁决采信）。
- W-B1 施工中 Trae 会话重启致 worker 进程阵亡（M 新发现④）：按手册 1.6 收回，主指挥评估树上成果亲验收尾。

## 二、交付摘要与 commit 链（训练仓独立仓）

| 批 | 交付 | commit |
|---|---|---|
| 存档点 | 17 号作战手册入库+台账行 | 6567fe9 |
| 批0 | 00 v2 总纲（9 章节+M13 拓扑取代声明+17 手册阅读链+F31/K15 卡图+拓扑序草案+教训十条+v9 分代口径）；旧 00 原样归档；15_账本 一·补七 方向性豁免；04 滞后注记 | 0b3deb9 |
| T2 | 坐标消费面清单 34 处（M1 排雷，宏/微域别+归属批次） | 04c413f |
| W-A1 | SimGrid 双层 2D（宏 256²/微 1024²/divisor=4）+消费面 11 文件适配；**y=0 行为不变实证=S1 determinism 改前改后 23250/23047 字节一致** | 73f3377 |
| W-A2 | professions v2 四族 25 职业（真源=35 资产 YAML 确定性抽取）；快照+raceId/钩子六 7 字段；champion 受控重注册 **tuning diff=0 亲验 True**（职业 37→25）；M4 三相等 25/25/25 亲验；M5 FAIL-fast 亲验；M3 映射表+M11 三职业对拍 | d75c99b |
| W-A3 | 野性三分支 D468+wildBase D498（Max 公式）+自卫层 D500；7 参数入 TuningSnapshot+registry_overrides 草案；v9_probe 四探针；Faction.cs 双端 MD5 828597BF 相等 | 12a525a |
| D553 落实 | 00 v2「随 1D 框架退役」标注；映射表逐 id disposition（35 资产+10 Undead=45 id，勘正通报笔误 12→10）；经济 5 职业 W-K 批0 前回池入 00 v2 §五注记④ | 5e72e2a |
| W-B1 | T10 感知单遍（D509）+T11 逃逸链（16 向圈×扇区×IsWalkableMicro，directionSectorWeight 占位入 registry）+T12 冲锋/reform 2D+T13 吸引场/意图权重对照开关（默认零回归）+T14 钩子六消费+CheckEnd 扩参战阵营；M12 零残留；9 探针全绿 | d07819c |
| W-B2 | gen-v9 生成器 2D（菱形布阵+点对称镜像+地形 patch 微格阻挡+tasks 2D）+CardPool v2 池（21 卡全换四族，Undead/SiegeMachine 引用清零）+63 场景（21 卡×3）+新兵种 10/10 入场断言 | 3a97b3d |
| W-C1 | v9 分代建档：**126 场景×100 局=12600 局，均速 0.03s/局（总 357s）**，M10 远低红线无需 probe 预案；baseline report.json+holdout_report.json（总分 0.506）落 results/baseline/v9（与 v8 分代隔离）；holdout_v9 四场景（独立 seed）；v8 退役护栏；卡池 K*_2_ 21 卡补生成 | c3e399d |
| 收口 | 15_账本 一·补九（#1~#8 回填）+一·补十（champion tuning 缺口/RunTrain 欠账）+卡骨架 46 张+本报告 | 见训练仓 HEAD |

## 三、门禁四件输出（T18，主指挥亲跑）

1. `dotnet build harness -warnaserror` → **0 警告 0 错误**（每批 commit 前均复验）
2. determinism：v9_probe 9 探针+63 v9 场景抽检+卡池抽检 **逐字节一致**（W-B2 抽 15 场景失败 0）
3. `champion baseline --battles 100 --suite v9` → **12600 局建档成功**（均速 0.03s/局）
4. `champion holdout --suite v9` → holdout_v9 四场景（H9_mirror/H9_chokepoint/H9_wild/H9_depth，独立 seed 778899+20260907）**全跑通建档**（总分 0.506）
- 职业计数三相等：`[M4] professions.json=25 champion=25 config=25`（亲验）
- M12：`grep new Random(|Math.random|Random.shared` → **零残留**（亲验）

## 四、新旧判据对照表（D553 条件①）

| 判据 | 旧（1D 时代） | 新（v9 时代，本批生效） |
|---|---|---|
| P0-4 环境自检 determinism | s1_plains_symmetric 同 seed 逐字节（23250/23047B） | **作废**——S1 引用已退役 Undead_Warrior（M5 FAIL-fast 亲验）；开工时刻该演示有效（W-A1 还用它实证 y=0 行为不变），随后载体随豁免挂起 |
| determinism 载体 | suite_v1/v8/hand_v2/R*/Cards 旧池/Holdout v8 | **v9_probe 2D 探针（9 个，固定 seed 逐字节）+ v9/卡池场景抽检** |
| baseline | champion baseline --suite v1/v8 | **--suite v9**（126 场景全量；battles<100 精简池 63+探针）；--suite v8 运行时拒绝（readonly 隔离输出考古用） |
| holdout | Holdout v8 | **holdout_v9/**（独立 seed 段+人工审构成四场景） |
| 作废理由 | 1D 死世界+Undead 框架退役（D550/D553 追认） | 旧场景集原地挂起禁改禁删（00 v2 §8.1「随 1D 框架退役」） |

## 五、T6「全量」注记（D553 条件②A）

T6 判据「全量」=**全集数据对账完成**（35 资产 YAML 全抽取+逐 id disposition 表 45 id 全覆盖+映射+抽查≥10），**非「全集入 json」**；json 收录=战斗可训 25（M4 三相等绑定不变式）。逐 id disposition 见 `opencode_tasks/W-A2_映射表.md` 附录。

## 六、M 雷处置与 W-C1 六偏离裁决（摘要，全量见清单 §五·补）

- M1~M16 逐条处置记录在清单 §五·补表（含「确认未触发」项）；新发现 4 笔（CheckEnd 欠账已修/opencode 模型失效已修/沙盒锁文件 XDG 重定向/worker 阵亡收回）。
- W-C1 六偏离主指挥裁决：①卡池 K*_2_ 补生成（seed 20260908 错开）**采信**；②SimChampion.ApplyStructAll 容错路径（tuning 缺 9 键=警告+代码默认，职业段 FAIL-fast 不变）**采信且不执行 champion 重注册**（守 D553③「tuning diff=0 维持」字面；缺口登记 15_账本 一·补十，W-K 批0 经济 5 职业回池时一并补齐）——**此条请策划端知悉性追认**；③v8-readonly 未实测（R* 重建会触发禁跑集）**采信**；④SimReporter 可选 Determinism 字段**采信**；⑤套件口径（<100 精简池/≥100 全量 126）**采信**；⑥RunTrain 硬编码 BuildSuiteV8 欠账**登记**（F 战役第一卡开工前必须清偿）。
- 勘正：中期通报「Undead 12」系笔误，实数 **10**（git show 73f3377 亲取键名）。

## 七、Unity 适配义务（15_账本 一·补八 U1~U5+一·补十）

U1 快照 7 新字段 / U3 SimTask 2D / U4 野性 7 参数 / U5 ParseFaction 五阵营 = ⬜ 待 Unity（champion 回灌前必须清偿，sim-sync §三红线）；U2 对账无差异 ✅。一·补六 #1~#4/#6 已 sim 侧清偿（一·补九 回填），#5/#7/#8 状态如实保持。

## 八、卡骨架 46 张（T20）

- F 31 张 `harness/Cards/{卡}/README_2.0.md`（T01~T21+F22~F31，每卡含拓扑依赖/就绪态/训练什么/核心指标/罪1 自查占位行）
- K 15 张 `harness/Cards/K1~K15/README.md`（定义占位，W-K 批+K 批另签任务书后施工）
- 旧卡台账不删不改；F 战役自治授权按 00 v2 §九（拓扑序 T01 起）。

## 九、已知欠账与后续（非本批范围，防僵尸）

1. RunTrain BuildSuiteV8 → v9（F 战役第一卡前清偿）
2. champion json tuning 9 键补齐（W-K 批0 经济 5 职业回池时受控重注册）
3. U1/U3/U4/U5 Unity 适配（champion 回灌前）
4. 主仓根 `.oc_state`（opencode 运行态，本报告提交后清理）
5. 一·补六 #5（D503 五轴合并）待 sim 多王国化；#7 行为面归 F28 卡；#8 归 W-K

## 十、请求

请策划端按 HH.94 任务书 §六 口径验收（门禁复跑/diff 抽查/账本完整性/00 v2 审阅/champion 旧值/卡骨架 46 张+§六 D553 条件 A/B 落实核验）；W-C1 偏离②（容错路径不重注册）请知悉性追认。验收成立后 HH.94 销号，训练师按 00 v2 §六 拓扑序自治推进 F 战役（T01 起，无需新任务书）。

---

## 十一、策划端验收裁决区（D557 · 2026-09-08 · 0.6 §八十七）

**结论：✅ HH.94 W 批验收成立（七项口径全过+W-C1 追认成立+D553 条件 A/B 落实核验通过）——HH.94/95 销号，F 战役自治授权生效（00 v2 §六 拓扑序 T01 起）。**

### 11.1 七项验收逐条（策划端实盘复核）

| # | 口径 | 结论 |
|---|---|---|
| ① 门禁四件复跑 | **全绿**：build -warnaserror 0w0e（亲跑）；determinism v9_probe **9/9 探针「确定性验证：通过」**（逐场景亲跑）；baseline --suite v9 **12600 局复跑**（本端 0.09s/局/总 1111s），**score 段 total=0.421+126 subScores 与建档版逐字段一致**，meta 差异仅 timestamp/durationMs（HH.40 口径=实质通过），seed 一致，内嵌 determinism 3/3 hash 相同双跑在案；holdout 同卷总分 **0.506 与申报一致**（baseline 命令顺带 holdout 同卷，门禁④一并覆盖） |
| ② diff 抽查 | **成立**：A1=18_W批坐标消费面清单 34 消费点+域别+归属批次在场（51 表行）；A2=professions v2 数值对 2_20.1 **14 项抽查全过**（游侠 8.5/风行 perception 12+walk 1.5/鹿骑 1.7/磐石 165+0.45+0.6/火枪 20+穿 1/狼骑 3.5+1.8/攻城槌 bld2+unit0/战士 110+11 等，≥10 达标）；A3=野性三分支代码真实性直读（SimBrain L518 WildThinkCore+L551 同族不攻+L506 有国者不走野性+L578-580 wildBase Max 三件套+L1308-1317 自卫层，与 D468/D498/D500 语义吻合） |
| ③ 15_账本完整性 | **成立**：一·补七（方向性豁免）/一·补八（U1~U5 义务表含锚点）/一·补九（一·补六 #1~#4/#6 sim 清偿+D499 同阵营放行+【#5/#7/#8 如实保持不臆改】）/一·补十（9 键缺口+RunTrain 欠账）四节全在场且内容实 |
| ④ 00 v2 审阅 | **成立**：§0 拓扑取代+§一~§九 9 章节齐；§七 教训十条（10 条目在场）；§六 拓扑序；§8.1「随 1D 框架退役」标注在场；§五 K 卡注记①②③（L-07/L-08/L-09 映射）+④回池前置 |
| ⑤ champion 旧值核对 | **成立**：pwsh 解析对比 6567fe9→HEAD，**tuning 段语义一致 True**（diff=0 实锤）；职业 37→25 与申报一致 |
| ⑥ 卡骨架 46 张 | **成立**：T01~T21+F22~F31=31 张 README_2.0 全在场+K1~K15=15 张 README 全在场（T-K/T-R 旧目录残留不删不改=守纪律） |
| ⑦ D553 条件 A/B | **成立**：A①=00 v2 §8.1 退役标注；A②=映射表逐 id disposition 表 **45 id 全覆盖**（25 入库+5 经济回池+5 设计退役+10 Undead）+T6「全量」注记+Undead 12→10 勘正在案；B=00 v2 §五注记④（五职业点名+W-K 批0 检查项=双在场 M4 扩展）+映射表经济线处置一致 |

### 11.2 W-C1 偏离②追认：**成立**

追认依据：①「tuning 缺 9 键」本端实锤（新 tuning 段无 wildAggroRadius）；②容错路径=缺键警告+代码默认（TuningSnapshot 默认=设计值，运行时行为正确）+职业段 FAIL-fast 不变；③守 D553③「tuning diff=0 维持」字面=正确取舍；④缺口已登记一·补十。**补强条件（随追认生效）**：野性参数相关训练卡（F 线野性圆域卡等任何 wild* 调参卡）开工前，若 9 键仍未入 champion json，必须先受控重注册补齐 9 键再训（防「训不可调参数」空转）——不等 W-K 批0。

### 11.3 F 战役自治授权（生效）

授权生效：训练师按 00 v2 §六 拓扑序自 T01 个体战斗 2.0 起自治推进 F 战役，无需逐卡新任务书；罪1 自查卡签发时填写（L-10 铁则）；里程碑抽查口径=HH.96 件C 预签（约每 5~8 卡交里程碑包+策划端抽 1~2 卡行为级回放）。开工前置清偿两项：**RunTrain BuildSuiteV8→v9**（F 第一卡前）+champion 9 键（涉及野性调参卡前）。

### 11.4 欠账与列报采信

①RunTrain BuildSuiteV8 欠账（§九.1）采信+F 第一卡前硬到期；②9 键补齐窗口（§九.2）按 §11.2 补强条件收紧；③U1/U3/U4/U5 Unity 适配维持挂账池行（champion 回灌前）；④**runs/det 4 文件未提交=运行产物残留**（复跑即覆盖，不阻塞）——建议训练仓将 runs/det 纳入 .gitignore（训练仓自治，随下次 commit 处理）；⑤§九.4 .oc_state 清理=训练师自查项知悉。

### 11.5 教训核查（vr-planner-leadership 钩子3）

本批无策划端验收失实翻案（七项全过零返工）；训练师两次 worker 偏离拦截（W-A2 触雷停下/W-C1 容错裁决）+Undead 12→10 自勘正=指挥链纪律有效运行，**嘉奖**。无新增教训条目；L-10（评分缺正确行为项）在 T-K/T-R 部分场景 0.0 分上体现=训练空间，F 战役签卡时按 L-10 自查问句执行。
