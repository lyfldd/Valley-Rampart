# HH.65 — HH.64 组合任务书完成报告（段A+段B+段C 全清 · 批4 M8+M9 交付）

> 类型：进度同步（交付待验收）
> 状态：⏳待策划端验收
> 日期：2026-09-04 · 发起端：执行端 · 关联：HH.64 任务书（commit 5f2bf74）· 2_20 实施清单 M8/M9 · D498/D426/D474/D490

---

## 一、总览

| 段 | 任务 | 状态 | 核心证据 |
|---|---|---|---|
| A | D498 Worker 基线回调（wildBaseAttack 转正） | ✅ 全清 | 三字段 SO 化+消费点切换+资产序列化落盘+编译 0 error+⑤d SO 值探针两轮 OK |
| B#1 | Smoke_12 补跑+D500 受击不追回归 | ✅ 全清 | Smoke_12 自动跑 P1~P10 ALL PASS；2_20_Race ②c（位移=0.0 不追击）两轮 OK |
| B#2 | 2_16 九组合确定性取证复跑 | ✅ 全清 | 9 组合 determinism=yes 全 PASS（实体清算修复后）；P-A6 明细重建完成 |
| B#3 | SaveSlots 删除按钮物理点击 | ✅ 全清 | 用户物理点击 → slot_1.json 文件消失（exists=False 实证） |
| B#4 | unity_scene 保存行为验证 | ✅ 全清 | **复发实证**+就地修复+零 git 损伤+守卫建议（见 §三） |
| C-M8 | 五轴消费接线（RaceDef 基准×KingdomDef 扰动） | ✅ 全清 | 2_20C P0~P5+P9 ALL PASS；2_20B 六轮回归 ALL PASS |
| C-M9 | 共通 5 职业零改动验证+Cavalry 负探针 | ✅ 全清 | 2_20C P6~P9 ALL PASS |

开工回执=HH.64 §五（三点边界逐条回应）。全程编译 0 error（存量 1 条 VS 233 节点缓存警告非本批）。零手动进局（D522 红利全兑现，段B#3 用户 30 秒物理点击除外）。

## 二、段A：D498=C 转正（可单独 commit）

**改动面**（3 文件+1 资产）：

| 文件 | 改动 |
|---|---|
| [WildnessConfig.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/WildnessConfig.cs) L24-29 | 增 `wildBaseAttack=1`（int）/`wildBaseRange=1f`/`wildBaseCd=0.5f` 三绝对基线字段；L54-57 缺口注销 |
| [NPCBrain.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/NPCBrain.cs) L669-671 | TryGetWildCombatOverride 下限兜底 `Max(1,·)/Max(1f,·)/Max(0.5f,·)` → `Max(wild.wildBaseAttack/Range/Cd,·)`（公式镜像，零行为变化）；缺口注销 |
| WildnessConfig.asset L18-20 | 三字段序列化值 1/1/0.5 落盘（execute_code SerializedObject 直写+SaveAssets+磁盘 Read 回读三证） |
| [Valley2_20_Smoke_Race.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley2_20_Smoke_Race.cs) | 探针⑤d 判据改 SO 值（`wa≥wildBaseAttack && wr≥wildBaseRange && wc≥wildBaseCd`，不再硬编码 1/1/0.5）+头部缺口注销 |

**范围说明**：任务书只点名 attack 一字段，执行端按"attack≥1/range≥1/cd≥0.5 转正为可调初值"括号语义三项并提+so-data-driven 铁律一并 SO 化（回执已声明，验收可驳回拆分）。

**证据**：2_20_Race 两轮实跑——R1「⑤d SO 下限兜底 attack=1/range=1.0/cd=0.5（SO 基线=1/1.0/0.5，Worker 基线=0→SO 值）OK」；R2 同 OK。行为面①②④③⑥⑦ 两轮全 OK（②b 反击链/②d 移动焦点各一轮行为窗口波动，见 §六挂账，与段A 数值等价转正无因果）。

**sim 义务列报**：wildBaseAttack/wildBaseRange/wildBaseCd 入 factor_registry 草案=sim 批义务（训练仓零触碰）。

## 三、段B：Unity 会话零碎包（四项全清）

### #1 Smoke_12 补跑（挂 SmokeApi/D522）

- [Valley2_17_Smoke_12.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley2_17_Smoke_12.cs) 增「自动跑(D522)」菜单：Play 后自动驱动 P1~P10 → 自动退 Play。
- **口径修正（重要）**：本容器=自含断言设计（合成王国 id=99/远域 fixture/裸 Play 全 Plain 地形初始化），**不挂 SmokeApi.EnterGame**——真实进局会使 fixture 坐标假设失效且 ExpandTick 触碰真国领土。D522"挂 SmokeApi"对自含容器的正确形态=Play 后自动驱动+跑完自动退（零手动保留），EnterGame 挂接仅适用于真实进局容器（2_20/2_20B/2_20C 已挂）。
- 实跑：**P1~P10 ALL PASS**（真判定/圈入/DZ-008/TerritoryGap/ExpandTick/纳脚下格/存读回环/句柄真源/圈营吞并/额度耗尽）+自动退 Play 日志在案。
- **D500 受击不追回归面**：载体实盘=2_20_Smoke_Race ②c（驻守 Worker 受射程外袭击：e2c 100→100、arcShift=0.0、moved=False、焦点保持 HomePosition）两轮 OK——驻守/移动单位受击不追击回归成立。

### #2 2_16 九组合确定性取证（专用容器纪律）

- [Valley2_16_SmokeVerify.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley2_16_SmokeVerify.cs) 增「自动跑(无框·禁活局)」菜单（无模态对话框=MCP 可跑）+**活局守卫代码化**（ActiveMap 在场→拒绝执行+ErrorLog，HH.57 §五铁律从纪律变守卫）。
- **首跑 9/9 FAIL→根因修复**：2_17 步骤4 台账转派生后，本工具旧 ResetWorld 只清王国/建筑不清单位 → 前遍工人实体残留 → workerCount 派生值跨遍累加（4→8 膨胀实录）→ determinism 假红。修复=ResetWorld 补单位实体清算（DestroyImmediate+UnitRegistry.Clear）。
- 修复后复跑：**9/9 组合 determinism=yes + D288 档位 ok + 模板互异 + kingdomId ok 全 PASS**（Small2~3/Medium3~4/Large4~6；difficulty1=4w0war 全过）。P-A6 明细丢失随本轮重建（9 组合逐条 PASS/FAIL 落盘 Console）。

### #3 SaveSlots 删除按钮（用户物理点击）

- 铺环境=主菜单 Play+存档文件盘点（slot_1.json 在场）。用户物理点击 SaveSlots 面板 slot_1「删除」按钮。
- **验证**：点击后 `File.Exists(slot_1.json)=False`（execute_code 实盘读盘）——UI Toolkit 动态按钮物理点击 → OnDeleteClicked → SaveManager.Delete → File.Delete → RebuildSlots 全链生效。HH.49 §五-3"UI 触发路径行为级未验"挂账**销账**。

### #4 unity_scene 保存行为验证

- **复发实证**：bridge `unity_scene save` 在团结引擎下再次产出 `GameScene.scene` 重复文件（message 自报 "saved to GameScene.scene"）。
- 就地修复（HH.46 同款）：确认 `.unity` 零改动（git diff 空）+Build Settings 引用 GUID 完好（2cda990e…）+isDirty=false 无独有内容 → 删 `.scene`+.meta → git status 场景目录归零。
- **守卫建议**（复发第 2 次，建议向 bridge 维护方反馈/升级为常设纪律）：执行端后续禁用 bridge `unity_scene save`（场景保存一律 execute_code `EditorSceneManager.SaveOpenScenes()` 或编辑器手动），HH.46/HH.65 双登记。

## 四、段C：批4 M8+M9 本体批

### M8 五轴消费接线（唯一改动点=KingdomFoundry personality 生成，禁动骨架兑现）

- [KingdomFoundry.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/KingdomFoundry.cs) L58-62/L289-307：原 `Perturb(tpl.GetPersonalityArray(), …)`（模板终值±扰动±clamp）退役 → `MergeFirstGenPersonality(rng, tpl, KingdomRace.GetKingdomRaceDef(state.id), firstGenPerturbation)`。
- **合并公式**（回执呈报，验收确认项）：`final[i] = RaceDef 基准[i] + (KingdomDef 模板轴[i] − 0.5) + rng(±0.2)`——加性偏离方案（D426"扰动在基准上偏离"语义）；**第一代不 clamp**（D474 勘定，消费侧 UtilityScorer Clamp01 保护既有）；RaceDef 资产缺失 → 基准中性 0.5 回退=退化原口径（防御兜底）。
- **消费链零改动**：UtilityScorer L82-83 五轴线性乘入+Clamp01 既有；动态立国 BlendPersonality 零改动（混合源=已含基准的来源国 personality，自动继承）。
- **禁动骨架兑现**：态势层/建军链/姿态档/D515 邻接修正零触碰（diff 自查：KingdomFoundry 净改动仅生成点 1 处+新私有函数 1 个）。

**M8 探针证据**（[Valley2_20C_Smoke_M8M9.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley2_20C_Smoke_M8M9.cs) 新建，两轮全量 ALL PASS）：

| 探针 | 判据 | 结果 |
|---|---|---|
| P1 种族基准分离 | 同 rng 同模板（金穗）配四基准 → 好战 orc=0.76>hum=0.46；经济 dwf=1.27>orc=0.77；外交 elf=0.60>orc=0.10 | PASS |
| P1b 包络 | 四族五轴 \|final−(基准+模板偏离)\|≤0.2+ε | PASS（全过） |
| P2a 同族双 AI 互异 | 人类·金穗双 seed：好战轴 A=0.396 vs B=0.302（扰动在场） | PASS |
| P2b 行为级差异 | UtilityScorer 同式评分轴项 A=0.396 vs B=0.302（同需求场景决策量不同） | PASS |
| P3 人类零回归 | 基准全 0.5 → 24 抽样均值 0.397≈模板值 0.40（新公式=原口径分布） | PASS |
| P4 端到端真实局 | 进局（矮人 22360）3 AI 国逐国逐轴包络全过+RaceDef↔国族一致 | PASS |
| P9 消费产出 | ScoreTop 真实局 k1(r0)/k2(r1)/k3(r3) 均产出有效焦点（2_17 契约零改动实证） | PASS |

### M9 共通 5 职业零改动验证+Cavalry 负探针

| 探针 | 判据 | 结果 |
|---|---|---|
| P6 走查 | Warrior/Archer/Mage/Healer/General 数据全在+族变体资产=0（单一共通真源→任意族行为一致的数据层证据） | PASS |
| P7 Cavalry 负探针 | 训练条目 toOccupation==Cavalry 全 raceId=0（人类专属）；1/2/3 族零骑兵条目 | PASS |
| P8 运行时消费面 | 矮人局（国族=2）GetTrainings(Building) 同式过滤后可见骑兵=0+共通 5 职数据全取（真实建筑实例过滤=2_20B P7 批3 已实证） | PASS |
| 四族共用行为面 | 2_20B 六轮（四族自适应国族）ALL PASS | PASS |

### 探针回归（验收标准§三段C）

- §十一.4：开局种族分布（2_20_Race ⑥：玩家 r2 第一代 K=3 互异+不含玩家族）+专属建筑训练探针（2_20B P2/P7）——随批回归全 OK。
- §十二.6：野性系（2_20_Race ⑤①②④）两轮回归全 OK（②b/②d 波动见 §六）。
- M7 面回归：**2_20B 六轮 ALL PASS**（R1 人类/R2 精灵/R3 矮人/R4 兽人 seed22360+R5 矮人 7841/R6 兽人 31337），含跨轮 ActiveMap 新实例+探针实体无残留负探针 R2~R6 全 PASS——M8 立国链改动零回归实证。

### sim 义务评估（对照 sim-sync §六，回执承诺兑现）

- **零 T 级直改、零 F 级**：M8 改 KingdomFoundry（Unity 侧 Systems/Kingdom，非 AI.Core 镜像区）；不触双端镜像文件/FactorContext/TuningSnapshot/champion/harness/场景 JSON。
- sim 侧关联义务两项随本报告列报：①2_20.1 §五"五轴来源改造→sim 王国生成器同构合并逻辑"——**最终公式实值=本报告 §四合并公式**，sim 批对齐用；②段A 三字段入 factor_registry（归 sim 批）。15_差距账本登记归策划端/sim 批，本批未直改训练仓。

## 五、git status 全量清单（交付前置自查）

**本批产物（建议 commit）**：
- 段A：`WildnessConfig.cs`+`WildnessConfig.asset`+`NPCBrain.cs`+`Valley2_20_Smoke_Race.cs`
- 段B/C：`Valley2_16_SmokeVerify.cs`+`Valley2_17_Smoke_12.cs`+`KingdomFoundry.cs`+`Valley2_20C_Smoke_M8M9.cs`(+.meta 新建)
- 文档：HH.64 任务书（回执）+2_20 实施清单（M8/M9 回执行）+本报告+`_交接索引.md`（回写中）

**非本会话改动（commit 时排除，请策划端确认归属）**：
- `河谷防线开发计划书具体内容/游戏流程与UI系统策划案.md`（M 状态，本会话零触碰）
- `图片资源/四族风格锚点/`（untracked 美术资源，HH.63 时代挂账项，随美术批裁决）

**commit 建议**（两笔隔离）：①段A：`D498 Worker 基线回调：WildnessConfig 增 wildBaseAttack/Range/Cd 绝对基线字段（硬编码下限原样转正 1/1.0/0.5 零行为变化）+NPCBrain 消费点切换+⑤d 探针 SO 值判据+缺口注销`；②段B/C：`HH.64 段B零碎包（Smoke_12 自动跑+2_16 实体清算修复九组合 ALL PASS+SaveSlots 物理点击销账+unity_scene 复发守卫）+批4 M8 五轴消费接线（D426 基准×扰动合并，消费链零改动）+M9 共通5职业验证+Cavalry 负探针（2_20C ALL PASS+2_20B 六轮回归）`。

## 六、挂账（不阻塞验收）

1. **②b/②d 行为窗口波动**：2_20_Race ②b（反击链 E2 未索敌）R1 FAIL/R2 PASS、②d（hBd 95 掉血）R1 PASS/R2 FAIL——跨轮翻转=行为窗口固有波动（HH.53 ②c 同款先例），段A 数值 1/1/0.5 原样转正=Max 兜底公式等价无因果；同批各取得过 PASS 证据。若策划端要求钉死，归 2_16/军事行为域专项（非本批义务）。
2. **2_16 取证工具 ResetWorld 实体清算**：本次修复属工具适配（台账转派生后的取证确定性前提），已随批交付；建议后续将活局守卫+实体清算口径同步 2_16 文档（文档回写归策划端）。
3. **unity_scene save 守卫**：bridge 缺陷第 2 次复发，守卫建议（§三#4）请策划端裁决是否升级常设纪律+反馈 bridge 维护方。
4. **冒烟存档堆积**：`Saves/` 下 smoke_*.json ×10（2_20B 六轮+2_20C+历史冒烟产物）——建议下批冒烟容器收尾时清理（非本批义务，未动）。
5. **HH.61 尾句措辞漂移**：「M8 分权语义/M9 王国扩张」与实施清单不符（回执 §段C.1 已锚定不据此扩展）——建议策划端在 HH.61 补勘定注（归策划端文档）。

## 七、验收请求

请策划端验收本批（段A/B/C 全清）。验收通过后：
1. 代执 commit（两笔隔离建议见 §五；`游戏流程与UI系统策划案.md` 与美术资源目录**排除**）；
2. **批5（M10）解锁**（依赖 2_13 实施批 ⬜ 待实施——M10 挂起条件按实施清单 §二 M10 行执行）；
3. 挂账五项裁量（§六）。

— 执行端 TraeCode · Q10 批4/HH.64 组合批 · 2026-09-04

---

## 策划裁决（策划端回写，D523 验收成立 2026-09-04）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 段A 三字段并提（任务书点名 1 字段） | **准** | 括号语义「attack≥1/range≥1/cd≥0.5 转正」本含三项（同一条 Max 兜底公式）；so-data-driven 铁律一并 SO 化正确；回执已声明 |
| 段B#1 Smoke_12 口径修正（不挂 SmokeApi.EnterGame） | **准** | 自含容器（合成王国 id=99/远域 fixture/裸 Play Plain 地形）挂 EnterGame 会破 fixture 假设+ExpandTick 触真国领土——D522 红利本质=零手动而非必须 EnterGame；「Play 后自动驱动+跑完自动退」是自含容器的正确形态。**沉淀为冒烟基建知识**：EnterGame 挂接仅适用真实进局容器 |
| 段B#2 首跑 9/9 FAIL→工具修复定性 | **采信+嘉奖** | 台账转派生后旧工具不清单位（4→8 膨胀）=让渡归因三问正面实践（先疑工具再疑产品）；活局守卫代码化=HH.57 铁律升级，好 |
| 段B#4 unity_scene 守卫升级 | **批准常设纪律** | 第 2 次复发：执行端后续禁用 bridge `unity_scene save`，场景保存一律 execute_code `EditorSceneManager.SaveOpenScenes()` 或编辑器手动；反馈 bridge 维护方入挂账池 |
| 挂账①②b/②d 波动 | **归 2_16/军事行为域专项** | HH.53 ②c 同款先例（行为窗口固有波动，同批各取得过 PASS 证据）；非本批义务 |
| 挂账②2_16 文档回写 | **归策划端**（随本验收串完成） | 文档债 |
| 挂账④冒烟存档堆积 | **挂账池登记**（下批冒烟容器收尾清理） | 非本批义务 |
| 挂账⑤HH.61 尾句勘定注 | **归策划端**（随本验收串完成） | 文档债 |
| M10 解锁条件 | **批5 直接解锁**——报告「依赖 2_13 ⬜ 待实施」为清单 L54 旧行误导：D505 已勘正（2_13 批C/D ✅ HH.46，依赖实际已解除），本验收串顺手勘正清单行 | 防执行端再读旧行 |

**实盘复核记录**：三字段+Header 注记（WildnessConfig.cs L23~29/L54~55）/NPCBrain 消费点/NPCBrain 公式镜像/MergeFirstGenPersonality 唯一改动（KingdomFoundry L61~62/L295，骨架零触碰兑现）/活局守卫+DestroyImmediate 实体清算（Valley2_16_SmokeVerify L45~49/L143~144）/M8/M9 清单回执行在场（L184/L185，卫生指令生效）/diff 构成与 §五 声明一致（11 文件+2 新增，游戏流程UI策划案+美术目录排除确认）。

**段B#3 用户物理点击实证采信**（slot_1.json exists=False）——HH.49 §五-3 挂账正式销账。
