# HH.66 批5（M10）+零碎搭车任务书（Q10 收官批）

- **策划端**：签发（2026-09-04）
- **执行端**：TraeCode（接收后开工）
- **依据**：2_20 实施清单 M10 行（依赖已解除 D505/D523 勘正）+ 排期三原则（零碎搭车=存档清理/学院复核/VagrantCamp 三笔挂账到期）
- **状态**：待执行端开工回执（HH.66 回写）→ 实施 → HH.67 完成报告 → 策划端验收 → **Q10 本体批（M1~M10）全收官，2_10染 解锁**

---

## 一、段A：M10 开局选族 UI 收口（主体）

**任务真源**：实施清单 M10 行 + 2_13 头部/§5.4B + D431（UI 侧）。

1. **选族界面接真数据**：2_13 批C 已落「选族暂存骨架」（当时 RaceDef 缺口让渡 Q10-M1 域）——现 RaceDef 四资产就位（批1 M1），把骨架接真：族名/族描述/族主色（bannerColor 或主题色）从 RaceDef 资产读出渲染选族卡；与王国名/地图档位同屏（2_13 §5.4B 布局）
2. **数据链贯通验收**（实施清单 M10 验收句）：选族 UI → `NewGameConfig.raceId` → GameSceneEntrance → M5a `EnsurePlayerRegistered` → `GetKingdomRace` 回填全链一致
3. **探针模式**：复用 2_13 批D-2「Menu 全链真实点击」先例（虚拟设备点选族卡→进局→raceId 断言）；四族各一轮可挂既有容器
4. UI 实现走 2_13 既有 UI Toolkit 惯例（UXML/USS），选族卡静态结构优先（SaveSlots 教训：动态创建按钮虚拟设备点击不触发）

## 二、段B：零碎搭车三件（挂账到期）

| # | 项 | 动作 | 出处 |
|---|---|------|------|
| 1 | 学院 -25% 叠乘口径终核 | TryTrain 的 academyMul×rallyMul×种族 speedMul 乘算序与数值复核（占位值合流终核；发现异样列报 P0 调优） | D521 挂账归 M10 · HH.61 §六.4 |
| 2 | 冒烟存档清理 | `Saves/smoke_*.json` ×10 清理 + **SmokeApi.QuitSmoke 收尾自动清 smoke_ 前缀槽位**（防堆积复发，冒烟基建自愈） | HH.65 §六.4 |
| 3 | VagrantCampSystem 补 ResetState | 营地数据清空方法 + WorldLifecycle ⑤ 序散点清单补一行（跨轮营地残留清偿，D522 挂账） | HH.63 §七.1 · D522 |

## 三、段C：Q10 全批收口汇总

M1~M10 十项全绿汇总（实施清单回执区终核+§十一.4/§十二.6 回归证据归档）——**P1 总验收前置条件达成确认**（P1=≥2 AI 自主至军事期，仍排在 2_10染 后）。

## 四、红线

1. AI.Core/训练仓零触碰（预期纯 Unity UI+SO 消费，零 sim 义务；回执列评估）
2. 接口纪律条款 D520：选族数据走 NewGameConfig 标准桥，禁掏私有字段
3. 段B#3 触碰 WorldLifecycle——只加编排行不动既有序列（2_14 复用面保护）
4. 交付前置：git diff 自查+清单回执区 grep 在场性（卫生指令常设）

## 五、验收标准

- 段A：选族界面在场（四族真数据渲染）+数据链行为级探针全过（选族→进局→raceId 生效）
- 段B：#1 复核结论（含 P0 调优输入列报）；#2 存档清零+QuitSmoke 自动清实证；#3 跨轮营地残留负探针（清场后 Camp 列表空）
- 段C：Q10 收口汇总表（M1~M10 状态+证据指针）
- 编译 0 error；既有冒烟（2_20/2_20B/2_20C）回归一致

## 六、流程

开工回执（HH.66 回写：段A UI 方案+探针编排+段B 可行性+sim 评估）→ 实施 → HH.67 完成报告 → 策划端验收 → commit 代执 → **Q10 收官，2_10染 排入**（P1 总验收随其后）。

---

## 七、执行端开工回执（2026-09-04，TraeCode 回写）

**状态**：✅ 已接单开工（commit f5187e3 基线）。

### 7.1 段A UI 方案与拟改文件

| 文件 | 动作 |
|------|------|
| `RaceDef.cs` | 加展示字段组：`raceDescription`（string）+ `bannerColor`（Color）——纯展示，非决策输入不入快照 |
| `Race_Human/Elf/Dwarf/Orc.asset` ×4 | 回填两字段：主色对齐 D457/D458 既有四族主题色占位（人类金 0.95/0.8/0.2、精灵翠绿 0.2/0.6/0.3、矮人铜橙 0.7/0.4/0.15、兽人暗红 0.6/0.1/0.1）；描述=执行端按既有五轴/专属建筑数据派生的**一句话占位文案**，真文案请策划端回填（HH.67 列报） |
| `CharacterCreationPanel.uxml` | `race-select` DropdownField **替换为静态四张选族卡**（race-card-0~3 写死 UXML，每卡=色带+族名+描述 Label）——遵循 SaveSlots 教训（静态结构优先，物理点击可达）；与王国名/地图档位同屏不变 |
| `Theme.uss` | 加 race-card 样式（卡布局/色带/选中态高亮） |
| `CharacterCreationPanel.cs` | OnEnable 经 KingdomRace 新入口读四资产渲染卡+绑 clicked；点击=选中态 class+`_selectedRaceId`；OnConfirm 填 `raceId=_selectedRaceId`；`RaceTextToValue` 退役删除 |
| `KingdomRace.cs` | 加 `GetRaceDef(int raceId)` 公开入口（D420 防散落 Resources.Load：UI 侧禁自行 LoadAll） |
| `Valley2_13_Smoke_C.cs` | P6 探针同步：映射逻辑断言 → 选族卡在场+真数据渲染断言（原 RaceTextToValue 反射必假红） |
| `Valley2_13_Smoke_M10.cs`（新建） | M10 进局断言容器：SessionState 期望族 → GetKingdomRace(0)/KingdomState.raceId/GetKingdomRaceDef(0).raceId 三点一致 + 期望值比对 |

**数据链现状确认**（下游已通零改动）：`NewGameConfig.raceId` → `GameSceneEntrance.SetNewGame` → `LoadManager.InitializeNewGame` → `WorldSystem L63 EnsurePlayerRegistered(newConfig.raceId)` → `KingdomState.raceId` → `GetKingdomRace` 全链 M5a 已实装（HH.58）。本批只接 UI 真数据+行为级验证。

**探针编排（四族各一轮）**：MainMenuScene Play → execute_code 设 SessionState 期望族 → **Computer Use 物理点击全链**（新建游戏→点选族卡 N→开始游戏，静态卡物理点击可达，非 API 直调，批D-2 先例）→ 进局 Console `[KingdomRegistry] 玩家王国开局注册: raceId=N` 实证 → M10 容器断言 → 退 Play → 删该轮 slot 存档（防四轮占满 3 槽）→ 下一族。

### 7.2 段B 可行性与预核发现

- **#1 学院叠乘终核——预核已发现一处异样实锤**：`TrainingSystem L294` 时长公式 `costDays / speedMul * academyMul` 走**除法**，而 2_20.1 §二 权威口径=`trainSpeedMul=训练时长%（<1 加速）`=**乘法语义**；RaceDef Tooltip 同写"<1 加速"。当前占位全 1.0 → 零行为差异，**真值回填即方向反转**（0.9 会算成 1.11× 变慢）。处置=本批顺手改乘法对齐权威口径（占位期零行为变化）+HH.67 列报；`academyMul 0.75` 方向正确（时长×0.75）+`rallyMul` 成本域一次性窗口口径正确；`0.75 硬编码`+`HasExclusiveBuilding("WarAcademy") 字符串散点` → 列报 P0 调优（迁 SO 域），本批不动。
- **#2 冒烟存档清理+QuitSmoke 自愈**：可行。SmokeApi.QuitSmoke（Editor-only）加 `smoke_` 前缀槽位自动清理（对齐 SaveManager.GetSavePath 口径）；HH.65 挂账 `smoke_*.json` ×10 现场清零实证。
- **#3 VagrantCamp 补 ResetState**：可行。清 `_camps/_restoredCampSeeds/_mapReady` 三件；WorldLifecycle ⑤ 散点加一行 `VagrantCampSystem.ResetState()`（红线3 遵守：只加编排行，①~⑦ 既有序列零触碰）。

### 7.3 sim 义务评估

**零 T 级直改、零 F 级行为变化，sim 义务=零**：
- 段A raceDescription/bannerColor=纯展示字段，非决策输入不入 TuningSnapshot/FactorContext；grep 实证 sim 侧（AI.Core/训练仓）无 trainSpeedMul/RaceDef 展示字段消费。
- 段B#1 speedMul 乘法修正=占位全 1.0 下 `1×1≡1/1` 行为等价（占位期）；TrainingSystem 为 Unity 侧系统，sim 无训练时长镜像（grep 零命中）。
- 段B#3 新增 ResetState=清场编排，sim 无感知。

### 7.4 防雷三条遵守声明

1. 选族卡走**静态 UXML** 结构（四卡写死），运行时只填数据+绑事件——SaveSlots 动态按钮教训遵循。
2. 选族数据走 **NewGameConfig 标准桥既有 raceId 字段**，零新接口零掏私有字段（D520）。
3. WorldLifecycle **只加 ⑤ 序一行**，既有序列零触碰（2_14 复用面保护）。

