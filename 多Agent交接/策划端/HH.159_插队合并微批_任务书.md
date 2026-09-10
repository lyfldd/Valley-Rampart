# HH.159 积压小任务清仓批（矿石复活链+footprint+2_24 批1+挂账池低优+UI/零碎/代码化/弹话）· 任务书

> 类型：施工任务书（**策划端 → 执行端**）
> 状态：🟡 已签发待接单（2026-09-10，用户拍板插队；**D612 范围扩=件5~件11 并入**）
> 日期：2026-09-10 · 发起端：策划端 · 账本占号：HH.159（2026-09-10，先登记后落盘）
> 裁决链：D609（矿洞产物裁决+矿石复活定稿=金属原料说）· D610（本批签发，0.6 §一百三十九）· **D612（范围扩，0.6 §一百四十一）** · **D613（mine footprint 2×2，0.6 §一百四十二）** · **D614（House footprint 2×2+房容补偿，0.6 §一百四十三）**
> 来源（用户拍板合并）：①矿石复活（HH.158 疑问 A）②footprint 正方形化（HH.155）③2_24 批1（D605 B1-1~B1-5）④挂账池低优三笔（DZ-077/078/079）⑤协同三笔（DZ-054/061/062）⑥注释卫生（DZ-083/063）⑦HH.131 零碎包 ⑧HH.133 UI/视觉小批 ⑨HH.132 代码化二态 v1 ⑩HH.153 AI 弹话文案族化——**共 11 件**（用户拍板「最多并入」，执行端 1M 上下文承载）
> 用户附加指令：**合并完成后策划端对全批小任务做完整性审查**（§六+0.6 D612，策划端义务）
> **配套执行计划=《HH.159_实施清单.md》**（同目录）：**50 条**任务表+跨文档依赖矩阵+分件施工详单+串行纪律+完整性校验表——**施工以实施清单为准**（更细），本任务书=范围与主线载体。
> 纪律钩子：vr-id-ledger（HH.159/160 已登记）· vr-planner-leadership 钩子1（教训引用见 §五）· 并发文件冲突纪律 · sim-sync（件1 涉 sim 镜像义务见 §四）

---

## 〇、接手入口（执行端开工恢复序）

1. 读 D609（0.6 §一百三十八）=矿石设计真源；本任务书 §一=施工规格
2. 读 HH.155 信（美术端\）§一=footprint 终版表（美术端已逐资产核对，权威快照）
3. 读 2_24 实施计划（改造计划/2_24_基础服务主体对称_实施计划.md）批1 节=B1-1~B1-5 规格
4. 当前局势：HH.103/150/153 均🟡已签发待接单——本批与三者**零文件面冲突**（见 §四 并行面），可并行接单；本批=用户拍板插队，接单优先级最高

---

## 一、件1：矿石复活链（D609 金属原料说，核心件）

### 1.1 设计定稿（用户 2026-09-10 两轮拍板，第一性）

- **矿石定位**：铁匠铺加工原料——变换链由「石→Metal」改为 **Ore→Metal**；金属既有下游（兵种强化/工事升级/高级建造/贸易）零改动；矿石使命到进金属为止，石头退守低中级建造+石弹。
- **产出端（用户原设计还原）**：mine=石头主产+矿石伴生；ore_vein=一次性主产矿石（忠实还原「采尽坍塌遗留石堆」二段产出，成本探明后列报，降级=单产矿石）；stone_pile 不动。

### 1.2 施工清单（锚点均策划端实盘探明）

| # | 改动 | 锚点 |
|---|------|------|
| 1a | 变换输入改 Ore：`_storage.Transform(ResourceType.Stone, ResourceType.Metal, stoneNeeded)` → `ResourceType.Ore` | BlacksmithBuilding.cs L51 |
| 1b | Transform 守卫改 `@in != ResourceType.Ore \|\| @out != ResourceType.Metal`；注释同步（原料改国库 Ore 真源） | StorageComponent.cs L99/L93-96 |
| 1c | BlacksmithDef 字段改名 `stoneToMetalRatio`→`oreToMetalRatio`（含 MetalFrom 方法名/MetalFrom 参数语义），**资产 BlacksmithDef.asset 同步**；tooltip 改「矿石→Metal 转化率（占位 2:1，D609）」 | BlacksmithDef.cs L11-16 |
| 1d | mine 矿石伴生：MineByproductComponent 加 Ore 第三子仓（rate/capacity 取 KingdomConfig 新字段 byproductOreRate/byproductOreCapacity，**SO 尾插默认 0.05/20**，存档尾插 byproductOreAmount 旧档缺→0 零 bump——全仿 Crystal/FireOil 既有双仓模式）；GetStore/SaveByproductState/RestoreByproductState/TryAdvertiseStore 三仓扩展 | MineByproductComponent.cs 全文+KingdomConfig+BuildingSaveData |
| 1e | ore_vein 改矿石主产：ore_vein.asset `outputResource: 1`→`4`（一次性采集链 Gather args 取 def.outputResource 自动跟随，零代码）；描述/显示名核对 | ore_vein.asset L39 |
| 1f | 注释与显示勘正：GameEvents.cs L182（`Ore // 矿石（矿洞主产；→仓库）`→`矿石（矿场伴生/矿脉主产；铁匠铺 Ore→Metal 原料，D609）`）+L183-184 Crystal/FireOil 注释「矿洞 Lv2/Lv3 副产」→「矿场副产（档位化=重构批件③目标态）」+L191 前例句核对+BuildingDef.cs L59 isBlacksmith tooltip「石→Metal」→「矿石→Metal」+AbstractEconomySettlement.cs L202 注释同步（**代码语义不动**——sim 侧按产出映射，铁匠铺依旧产 Metal） | 五文件注释级 |
| 1g | 数值锚：铁匠铺建筑描述 asset「石→Metal」→「矿→Metal」；AI 行动池若引用 Blacksmith 文案同步 grep | BuildingDef asset+grep「石→Metal」全库 |
| 1h | **Ore 国库链同构补齐（T1.8，HH.164/D617 前提项，先做）**：①`TreasureVault.Managed += ResourceType.Ore` ②`KingdomManager.TreasuryOre`（含 LoadState/ResetState 同步）③`KingdomSaveData.treasuryOre` 尾插（旧档缺→0 零 bump）④`TreasureVault.Init` 回填；**AI 侧**=`KingdomState.ore` 独立桶 + `TaskScheduler.AddGatherOverflow` 加 `case Ore`（照水晶/火油先例，**不进 ResourcePack 五经济资源**） | TreasureVault.cs/KingdomManager.cs/KingdomSaveData.cs/KingdomState.cs/TaskScheduler.cs |

### 1.3 红线与边界

- **Metal 下游零改动**：TrainingSystem/RulerController/TradeSystem/UtilityScorer/TreasureVault 等金属消费面（策划端已全查 21 处）禁止触碰
- **石头链零回归**：stone_pile/石弹链/低级建造 cost 不动；mine 石头主产（isResourceNode/gatherSeconds）不动（M1 红线重申）
- **⚠️ Ore 搬运链已在位，但「国库槽」实为空（HH.164/D617 勘误）**：ResourceCarryConfig resourceType:4=10+贸易行/显示文案在位；但 `TreasureVault.Managed`（L17-22）/`KingdomManager.Treasury*`（L394-402）/`KingdomSaveData` **均无 Ore**（`Deposit(Ore)` 返回 0 **静默丢**）——**T1.8 为 T1.1/T1.2/T1.4/T1.5 硬前置**（原「禁止重复建设，grep 复核即可」=清单假设被 HH.164 实盘证伪，本条勘误）
- 唯一守卫点：低级建造若 Ore 与 Stone 双可选属后续设计（本批不做），Ore 不进任何 cost 字段

---

## 二、件2：footprint 正方形化（HH.155 用户拍板，12 变更+1 新建；D614 追加 House 房容代码）=**★含代码**

| # | def | 旧→新 |
|---|-----|-------|
| 1 | Barracks | 3×1→2×2 |
| 2 | Blacksmith | 2×1→2×2 |
| 3 | Hospital | 2×1→2×2 |
| 4 | Church | 2×1→2×2 |
| 5 | Warehouse | 2×1→2×2 |
| 6 | SiegeWorkshop | 2×1→2×2 |
| 7 | Ranch | 2×1→2×2 |
| 8 | VagrantCamp | 4×1→2×2 |
| 9 | farm | 1×1→2×2 |
| 10 | castle | 2×1→3×3 |
| 11 | mine 矿场 | 1×1→**2×2**（D613/HH.161：四产物语义+构图统一+零返工窗口） |
| 12 | House 房屋 | 1×1→**2×2**（D614/HH.163：房容 ×4=12/20/32+SO 化，用户拍板「美术效果不好」） |
| 新建 | 传送门 def（威胁臂建筑，现无 def） | 建档落 2×2（字段参照 rift 退役档语义+HH.155 终版口径；faction/role 按威胁臂设计=敌对阵营） |

**维持不改**：gate 2×1（嵌墙线语义）；1×1 系（Well/wall/bridge/箭塔/弩塔/魔法塔/tree/ore_vein/stone_pile/wood_pile，**mine 已 D613、House 已 D614 移入变更表**）；market+专属 4 栋（已 2×2）。

**施工注意（美术端已核实消费端，HH.155 §二）**：
- 占格判定字段驱动（GridSystem.MarkOccupiedFootprint，Building.cs:79/686）预计零代码改动，纯资产值变更
- **AI 预置链核对**：baseBuildingDefIds 六模板摆放点位按新占地复核（2×1→2×2 多占一排格可能挤相邻预设建筑；营地 4×1→2×2 同理）——发现问题列报，预置链迁移主体仍归建筑体系重构批
- **选址回归**：占地变化影响 AI 行动池建造选址打分空间（D598 F1 等选址特征面），冒烟回归确认无新 regression
- 旧档兼容：Building.cs:655-657 已存档 footprintW/H 与 def 脱钩——未发布不迁移（D547 先例），知情即可
- 城门 GateOrientation 判向（Building.cs:68）不受影响（gate 维持 2×1）

**⚠️ 件2 含代码改动（D614 追加，House 笔≠零代码；其余 11 笔仍纯资产值）**：
- **房容 ×4**：[HappinessSystem.cs:297](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/HappinessSystem.cs#L297) `GetHouseCapacity` 硬编码 3/5/8 → **12/20/32**（保密度 12/4格=3人/格=旧值；递进比吻合 House.asset statScale 1.67/1.6）
- **房容 SO 化**：数值入 KingdomConfig（与 `happinessHouseWeight` 同源；唯一真源 Resources/Config/KingdomConfig.asset）——治 so-data-driven 违规点
- **测试夹具重算**：TestFixtureTiersConfig「每 House 容量=3」假设失效（×4 后夹具 capacity>pop 过得太容易=生育门槛测不出）→**重算 houses 数**
- **读口复核**：HH81 P1 `cap>0` 断言 + TestFixtureApi.EvaluateBirthConditions 仍成立（`>` 语义），诊断读数随新值刷新
- 消费面全在 Unity 侧（PopulationSystem 闸门/HappinessSystem houseFactor/UtilityScorer HouseGap/BuildingPanel/测试读口）；**决策核/态势层不受影响**（DrivePopPressure 用 (工+战)/20 不读房容）→ 预计无 sim 镜像（训练仓兜底 grep `HouseCapacity`）

---

## 三、件3：2_24 批1 主体对称五项（D605 立项「随批携带」兑现）

按 `改造计划/2_24_基础服务主体对称_实施计划.md` 批1 节规格执行（本批=其排期条款「批1 随既有批次携带」的宿主批）：

| # | 项 | 规格（详见实施计划） |
|---|-----|------|
| B1-1 | BuildingFactory faction 派生 | CreateBuildingInstance faction 按 kingdomId 派生（对齐 UnitFactory.SpawnUnit 先例；DZ-040） |
| B1-2 | TaskScheduler 兜底分流 | 兜底按 brain.kingdomId 分流（DZ-045 AI 资敌修复） |
| B1-3 | Satiety null→0 | SatietySystem null→0 统一（隐性 0 特权消除） |
| B1-4 | Ranch 双桶 | RanchSystem 结算双桶参数化（**与件1 mine 副产同文件面注意串行编辑**） |
| B1-5 | WarehouseHelper 参数化 | GatherActive 参数化（低优先，可随批） |

**红线**：玩家零回归（D605 全批通用条款）；每项参数化必过冒烟全集。

---

## 四、件4：挂账池低优三笔（HH.106 分流的小批顺手项）

| # | 台账 | 规格 |
|---|------|------|
| DZ-077 | EventBus 死事件/噪音族 | 两死定义删（UnitAttackEvent/BuildingProductionTickEvent）+LODSystem L336 恒 0 表达式修+守卫补（D563 已静音三事件部分清偿，本笔清尾） |
| DZ-078 | market 误挂 Gold 死仓 | econStorageOnly 分支条件排除 outputResource==Gold（三行级） |
| DZ-079 | 医院回血 per-kingdom | SatietySystem L240 HasBuilding("Hospital") 复用 L253 per-kingdom overload（玩家医院不再给 AI 回血） |

---

## 四·补、件5~件11（2026-09-10 D612 范围扩：用户拍板「最多并入」）

> 用户指令（2026-09-10）：把积累的小任务尽量并成一个批（执行端上下文 1M 足以承载）；凡有独立任务书者，**以本任务书为汇总执行载体，原子任务书转为从属（标注「并入 HH.159 件X」）**。

### 件5：文档核对（策划端已在 D609 完成，执行端仅核对）

3.1.3 §六（footprint 档位+图量约 77 张）+总表 v2 §1.1+1.3 D199 注记——已由策划端亲改；执行端核对美术清单行与之一致即可，无改动义务。

### 件6：A 组·与件1 同主题同文件协同三笔（D612 新增）

| # | 台账 | 规格 | 锚点 |
|---|------|------|------|
| DZ-054 | 一次性资源点 isResourceNode 标志不一致（**D617 裁 B：降级=维持现状+注记，不统一**） | ore_vein=1 vs stone_pile/wood_pile=0（实盘确认）；**HH.164 实盘=4 消费面**（`WanderStimulusProvider` 会使 def 成 NPC 游荡锚点候选）→「三资产全置 1」会致 AI 行为漂移，**维持现状+注记三者语义差异**；台账以实盘 4 消费面修正确销案（原「消费面仅 Demolish 守卫」失准） | ore_vein/stone_pile/wood_pile.asset（**不动**）+注记 |
| DZ-061 | 玩家含铁造价豁免 | WarehouseHelper.TrySettle/CanAfford 只锁 Stone/Wood/Food，不查不扣 Metal（含 metal 造价玩家白嫖铁）；补 Metal 凑单（国库 Vault_Metal 已在 Registry） | WarehouseHelper.cs（三行级） |
| DZ-062 | 判定双口径 | BuildingMenuPanel 菜单置灰（ruler.CanAfford 单看国库）vs BuildController.CanPayBuild（WarehouseHelper 本地仓凑单）口径不一；菜单判定改走 WarehouseHelper 同口径 | BuildingMenuPanel/BuildController（与 DZ-061 同批） |

### 件7：B 组·注释/注记卫生（D612 新增，零行为风险）

| # | 台账 | 规格 |
|---|------|------|
| DZ-083 | 过时注释卫生 | a.CampUpgrader.cs L76「触发端归 2_17 步骤12；本片判定恒假」已在盘确认过时（HH.51 批B TryAnnex 真判定已接线）→ 改注释；b.GuardDeploymentSystem.cs L64「交互入口归 2_13」**执行端核实是否已修**（策划端 grep 未命中，疑已清）→ 未清则改 |
| DZ-063 | 三笔注记 | ①特食/肉溢出装箱折损（TreasureVault.SpillToChest）加注待数值批裁 ②AI 非经济资源无消费端加注 ③AIEconomySettlement 头注释漂移（D535 后失实）修 |
| HH.164 决策7（T7.3） | ProducerComponent Ore 死分支清理 | `UpdateByproductConfig` L91「outputResource==Ore 判矿洞」在 D562 后**已不可达**（mine 改 Stone+独立 MineByproductComponent）=死代码+双源误导 → 删该分支+`_hasByproduct/_byproductType/_byproductCapacity` 死字段，grep 兜底 |

### 件8：HH.131 六考后零碎包并入（原任务书=策划端/HH.131，规格照原文）

- 件1 磐石 20→10 行为级补测（替身直调法；容器 Valley_HH131_BedrockProbe 正门+P1~P4+断言=T6 公式基准±1）
- 件2 观察器白名单补 combat/GameOver tag（侦察→补条→验证）
- 性质=纯工装零产品代码；0.5 天

### 件9：HH.133 UI/视觉小批并入（原任务书=策划端/HH.133，规格照原文）

- 件1 选族卡排版（双分辨率）｜件2 DZ-081 巡逻入口（最小入口=选中→巡逻按钮→2~4 路径点循环）｜件3 DZ-082 情报面板②③+染色高亮联动｜件4 D240 下拉对比度
- 性质=玩家侧 UI/交互，AI 行为零触碰；~1 天

### 件10：HH.132 代码化二态 v1 并入（原实施计划=策划端/HH.132，规格照原文）

- LifecycleAudit Editor-only 校验器：R1 事件双向对拍（三类报告+豁免表=登记表 §4 初始集）+R2 存档覆盖对拍（Singleton×ISaveable+编排×ResetState 差集）+检测力标定 T1~T3
- 性质=Editor-only 工装（零运行时/零生产码）；1~1.5 天；**豁免表变更需策划端确认**（D561 红线）

### 件11：HH.153 AI 弹话文案族化批并入（原任务书=策划端/HH.153，规格照原文+QQQ.6 文案全稿）

- 双层池（族池×4+职业池×17 搞怪化）+`PickTalkLine` 加 raceId 维度+`talkRaceChance` SO 化 0.4（**落 AttentionTuningConfig**；不进 ToSnapshot）+池 static readonly 防 GC
- 完成报告=HH.154（原预留维持）

---

## 五、并行面与排雷图

**并行面（D612 范围扩后更新）**：本批现含件1~件11。**外部并行会话**=HH.103（美术接入，动 Art 资源+Ground 改名）/HH.150（测试基建，已交付/由并行策划验收中——**本批勿碰**）——本批与 HH.103 零文件交集。
**批内串行红线（M5 升级）**：`KingdomConfig` **被两处触碰**——件1（byproductOreRate/Capacity 尾插）+件2 T2.16（房容字段）→**一次规划全部字段后一次落**（**HH.164 复核勘正**：件3 B1-4 已实装无新字段、件11 talkRaceChance 落 `AttentionTuningConfig` 非本 SO）；`ore_vein.asset` 仅件1（outputResource）——**件6 DZ-054 已 D617 降级=不动，脱离串行**；**新增 T1.8 触碰 TreasureVault/KingdomManager/KingdomSaveData/KingdomState/TaskScheduler 五文件（须最先落，T1.1/T1.2/T1.4/T1.5 前置）**。
**分件独立验收**：件1~件11 各自独立自证（见 §六），单件失败不阻塞其余件，但同一文件串行编辑须遵守 L-12。

| 编号 | 教训 | 本批处置 |
|------|------|---------|
| M1 | L-06 冒烟走生产链 | 矿石链验证走真实采集→搬运→铁匠铺加工全链，禁直调 Transform |
| M2 | L-17 容器正门 | 所有冒烟 EnterTestRun 正门 |
| M3 | L-02 落盘证据 | 改名（oreToMetalRatio）后全库 grep stoneToMetalRatio 零命中+双锚点 |
| M4 | L-20 施工≠竣工 | Ore 链断言=端到端（矿产出→国库 Ore>0→铁匠铺加工→Metal 增）非函数级 |
| M5 | L-12 同文件多处编辑串行 | KingdomConfig 尾插须一次规划全部字段（**HH.164 复核勘正=仅 T1.4/T2.16 两处；B1-4 已实装无新字段**） |
| M6 | L-19 摆位陷阱 | footprint 变更探针须核对预置建筑新占地无重叠 |
| M7 | sim-sync | 件1 改 KingdomConfig（Unity 侧）+1e 存档字段——**15_账本登记义务**：KingdomConfig 两字段（byproductOreRate/Capacity）+BuildingSaveData 尾插+**KingdomSaveData.treasuryOre（HH.164/D617 扩面）**+变换链 Ore→Metal 的 sim 语义核对（Abstract 侧产出映射不变=Metal 照产，输入端差异注记）；若 sim 有 Blacksmith 变换镜像则同步（策划端预核=sim 侧无 Transform 语义，仅注记） |
| M8 | L-12 跨件同文件串行（D612 新增+D617 更新+HH.164 复核勘正） | KingdomConfig（**件1/件2-T2.16 两处**）与 **T1.8 五文件（TreasureVault/KingdomManager/KingdomSaveData/KingdomState/TaskScheduler）**——按 §五 串行红线执行，一次规划字段；ore_vein.asset 仅 T1.5（DZ-054 已降级） |
| M9 | L-15 承接实体化（D612 新增） | 件8/9/10/11=原四份独立任务书的实体化承接——**勿因本批汇总而漏原件规格**，逐件对照原任务书核进度 |
| M10 | L-02 写后必验（D612 新增） | 件7 注释卫生/件6 三笔资产改后，grep 双锚点核旧文本零残留 |

---

## 六、验收探针（行为级，正门容器）

- **P1 Ore 产出**：正门建局→确认 mine 存量 Ore 子仓增长（伴生 rate 生效）→ore_vein 采集 Gather 后**国库 Ore>0（T1.8 后 `Deposit(Ore)` 真入桶，非静默丢）**
- **P2 变换链**：国库 Ore≥ratio→铁匠铺 Tick→Metal 增+Ore 扣减（Stone 不参与）
- **P3 金属下游零回归**：兵种强化/建造含 metal cost 路径照常（存量冒烟覆盖，零改动验证）
- **P4 石头链零回归**：石弹/低级建造/石堆采集照常（存量冒烟覆盖）
- **P5 footprint 回归**：12 笔资产改后四容器冒烟全绿+AI 预置链摆放无重叠断言+选址无新 regression（D598 F1 特征面）
- **P6 2_24 批1 五项**：按 2_24 实施计划批1 探针节（含玩家零回归断言）
- **P7 DZ 三笔**：DZ-077 死定义删后编译 0 错+无订阅守卫生效；DZ-078 market 不再误挂；DZ-079 AI 无玩家医院加成断言
- **P8 件6 协同三笔**：DZ-054（**降级=维持现状**）三资产值不动+注记在位；DZ-061 含 metal 造价玩家实扣铁（断言国库 Metal 减）；DZ-062 菜单置灰与扣费口径一致（国库不足但本地仓足→按钮可点）
- **P9 件7 注释卫生**：DZ-083 改后 grep 旧注释文本零命中；DZ-063 三处注记在位；**T7.3 ProducerComponent Ore 死分支零残留**
- **P10 件8~件11**：按各原任务书验收节（HH.131 探针 P1~P4/HH.133 四件/HH.132 R1R2 标定 T1~T3/HH.153 弹话族化效果）
- **回归底座**：四容器（2_20/2_20B/2_20C/Smoke_5）+同 seed 双跑（L-22 纪律）
- **grep 双锚点**：stoneToMetalRatio 全库零命中（M3）+「石→Metal」中文文案零残留（1g）

**量级预估（D612+D613/D614+D617 后）**：件1 ≈0.75 天（含 T1.8 Ore 国库链）+件2 ≈0.75 天（含房容 ×4+SO 化+夹具重算）+件3 ≈1 天（3 项降级核验）+件4 ≈0.5 天+件6 ≈0.25 天+件7 ≈0.25 天+件8 ≈0.5 天+件9 ≈1 天+件10 ≈1~1.5 天+件11 ≈0.5 天=**约 6.5~7.0 天**（十一件合并为一次接单+一套回归底座，省去各批独立开销与回归轮次；用户拍板「最多并入」）。

---

## 七、执行与回写纪律

1. 动文件前必重读磁盘最新内容；同文件多处编辑串行（M5）
2. **不改设计文档**（本批文档面已由策划端 D609 落定：总表 v2 §1.1/1.3/3.1.3 §六）；发现问题列报请裁
3. 完成报告=HH.160（预留）：git 面构成+探针判读+grep 双锚点+15_账本登记回执（M7）
4. commit+git-plan-sync 惯例；只提本批文件勿 `git add -A`

## 八、回执区（执行端施工后填写）

- 件1 矿石链：⬜（1a~1h 逐项；1h=T1.8 Ore 国库链，先做）
- 件2 footprint：⬜（12+1 逐笔；含 D614 房容 ×4+SO 化+夹具重算）
- 件3 2_24 批1：⬜（B1-1~B1-5）
- 件4 DZ 三笔：⬜
- 件5 文档同步：⬜（3.1.3 §六 Ranch 计漏+77 张修正已在 D609 完成=策划端；执行端仅核对美术清单行）
- 件6 A组协同三笔：⬜（DZ-054 降级维持现状+注记/061/062）
- 件7 B组注释卫生：⬜（DZ-083/063 + T7.3 死分支清理）
- 件8 HH.131 零碎包：⬜（磐石补测+白名单）
- 件9 HH.133 UI/视觉：⬜（四笔）
- 件10 HH.132 代码化二态 v1：⬜（R1+R2）
- 件11 HH.153 AI 弹话：⬜
- 探针 P1~P10 判读：⬜
- grep 双锚点证据：⬜
- 15_账本登记（M7）：⬜

---

> 签发：策划端 2026-09-10（D610 随本签发落 0.6 §一百三十九；**D612 范围扩=件5~件11 并入，0.6 §一百四十一**；**D613/D614=件2 footprint 扩至 12 笔，0.6 §一百四十二/一百四十三**；**D617=HH.164 裁决：件1 +T1.8 Ore 国库链、件7 +T7.3 死分支清理、T6.1 降级、件3 改核验，任务 48→50，0.6 §一百四十六**）· 用户拍板插队+最大化并入 · 接单即开工

---

## 九、补（2026-09-10，进度8 列报裁决 + 件12 矿山锚点簇并入；D619）

> 触发：执行端「汇报 8」（件8/9/10/11 三路并行完成=45/50 代码+静态验收，5 条待进局跑批）+ 用户指令「**把矿山三缺口加进去**」「**①（生成器产矿山簇）照准、改动思路=彻底解决**」「**测试由策划端用专用测试环境亲跑**」。

### 9.1 列报六项裁决

| # | 列报 | 裁决 | 分流 |
|---|------|------|------|
| 1 | 件9 T9.2/T9.3 与任务书假设不符：全库无「既有单位操作面板」；PatrolTaskSystem 既有结构=「方向+推进点」非「路径点集合」 | **采信 sub-agent 的最小侵入处置**（既有 TopLeftHUD 作最小入口载体、新增 `Waypoints`/`WaypointIndex` 且方向模式原路径零改动、未新建面板/未改场景）——**实盘直读纠偏优先于硬做清单**（L-15「清单假设≠实盘」正面样本），嘉奖 | 回执/报告如实记录；**扩**：新增字段的**入档语义**须在 T9.2 P4 列报（保持原验收） |
| 2 | 件10 R2A 检出 `DamageSystem`（Singleton 未实现 ISaveable，而登记表 §3.1 反将其列入 ISaveable 全集）=登记表与代码不符 | **以代码为准：登记表 §3.1 修正「DamageSystem 非 ISaveable」**；R2A 能检出=**审计器有效性的正向证据**（采信，嘉奖） | 归策划端（登记表 Owner=策划端单写者 D561）+ 台账 **DZ-085** |
| 3 | 登记表 §4 漂移：`GeneralDiedEvent`/`FormationDisbandedEvent` 在场未登；`UnitAttackEvent`/`BuildingProductionTickEvent`/`EnemyEnteredRegionEvent` 已删未回写 | **照实补登/销注**（不新开任务） | 归策划端 + 台账 **DZ-085**（与 #2 合并一笔） |
| 4 | `TerritoryOverlay.HighlightKingdom` 切王国不重绘上一个（存量） | **并入 T9.3 同趟做**——切走/取消选中时清上一国高亮（一行级）；同属「染色高亮联动」面，一次做完，避免"点选有高亮、切走留残影" | 件9 T9.3 扩 |
| 5 | `PatrolTaskSystem.Clear()` 零调用方（存量） | **并入 T9.2 同趟处理**——巡逻入口接线时补 `Clear` 调用；若核实确无需调用方则须给出依据（防巡逻态残留泄漏） | 件9 T9.2 扩 |
| 6 | `--color-text-secondary` 未定义（执行端已加 fallback） | **接受 fallback=不阻塞**；但 **补变量定义（正解）**列为件9 顺手项（防 fallback 长期掩盖变量缺失） | 件9 T9.1/T9.4 顺手项 |

### 9.2 答复 A/B/C

**裁定 = 「B 先行 + 跑批由策划端接管 + 执行端转做件12」**：

- **执行端**：先 **commit 落库**（代码+静态验收成果，防丢失；只提本批文件勿 `git add -A`）→ 然后**接 件12**（§9.3）。
- **跑批**（5 条待跑项 + 收口四容器回归 + 同 seed 双跑）：**由策划端用 HH.92 正门 `TestHarnessApi.EnterTestRun`（15x 考跑加速档）亲跑**（用户指令；策划端本会话已接通 Unity MCP），结果回写、作为 HH.160 前置证据。
- 理由：单会话上下文吃紧是真实约束；跑批=长时机械操作，适合持测试环境（HH.92）的一侧承担；执行端专注实现新件。

### 9.3 新增件12 矿山锚点簇 + 水域避让（D618 + DZ-084；用户指令「彻底解决、加进去」）

> 设计真源=报告《矿山锚点与矿洞产链现状核查_2026-09-10》§策划裁决（D618）· 0.6 §一百四十七。**生成侧地基改动**：玩家可见效果在 mine 转可建（重构批 T6）时兑现，但**现在做=一次改一段生成代码，避免 T6 时再开一次生成器**（用户「彻底解决」口径）。

| 编号 | 任务 | 类型 | 验收标准 |
|------|------|------|---------|
| T12.1 | **矿山锚点簇（缺口 A，D618 定档①）**：`MapGenRules.FillFeatures` 改按 **2×2 成簇**撒布矿山 + `EnsureOne`→**簇保底**（保底半径内至少一个 ≥2×2 完整簇）+**不留孤立矿山格**（无 2×2 可建子块者并簇/消除）+密度口径按簇（权重表语义「格」→「簇」，1 簇=1 争夺点） | 架构 | 同 seed 全图**每个 `FeatureType.Mine` 格均处完整 2×2 可建子块**（无孤立格）；出生半径保底簇可建；既有冒烟无「矿山格计数」类硬断言（若有→列报） |
| T12.2 | **水域避让矿山（缺口 B，DZ-084）**：`PlaceRiver`/`PlaceLakes` 增 `FeatureType.Mine` 避让判断（河/湖不覆写矿山格）——**与 T12.1 同一段生成代码一次改**，禁二次返工 | 架构 | 同 seed 图水域后**无被切碎的矿山簇**；河穿矿=岩石出露（语义自洽）；Tree/StonePile **不保护**（河穿林石属自然，维持现状） |

**件12 纪律**：①与 T12.1 同文件串行（L-12）；②改前重读 `MapGenRules.cs` 磁盘态；③**必须与缺口 A 同趟落地**（半态=图面出现大量可见但不可建的矿山格，比 1×1 时代更糟）；④不碰 mine.asset（footprint 2×2 已 D613 落，本件只改生成侧）。
**D621 开工复核补三条硬性**（executor 实读复述时新增）：⑤**`EnsureOne`「已有」判定须改**——现 L299「半径内**任一** mine 格即 return」改为「半径内存在**完整 ≥2×2 轴对齐可建块**才 return」（否则孤立格充数=**保底静默失效**，且孤立格又被 T12.1 清掉=保底彻底落空；L-01 就位≠生效）；**簇=轴对齐 2×2 完整块（非任意连通 L 形）**，判定写死；⑥**总矿格数近似不变**（D618「避免矿区总量膨胀」⇒**簇数≈原格数÷4**；改前/改后同 seed 对照按此口径报，非"变多 4 倍"）；⑦**`MineralRich` 立国选址兼容核对**（`MapGenRules.cs` L208-210 `ChunkFeatureRatio(Mine)≥cfg.mineralDensityThreshold`[缺省 0.05=**按格占比**]——簇化改空间分布与总量⇒阈值口径漂移⇒MineralRich 模板命中率变化→D292 回退或改变 AI 立国选址；**须给同 seed 改前/改后命中率对照 + 阈值按新口径重标定**，登记 15_账本/总表）。
**嘉奖**：executor 实读比 D618 多找出一个覆盖点（**PlaceLakes L341-343 亦无避让**，D618 原只记 PlaceRiver）——实盘直读补全，正是本批要的行为。

### 9.4 流水线计数

任务计数 **50 → 52 条**（+件12=2；件9 的 T9.2/T9.3 为**同趟扩做**非新增条目）。

### 9.5 跑批结果（策划端亲跑，HH.92 正门 + 15x；2026-09-10）

**跑批方式**：策划端本会话直连 Unity MCP HTTP 端点（`127.0.0.1:8080/mcp`，serverInfo=mcp-for-unity-server 3.4.7）→ `manage_editor play` → 等就绪（WorldManager 在场）→ `execute_menu_item` 触发容器 → **`Editor.log` 直读判读**（D611 证据源）→ `manage_editor stop` 收尾。证据日志=`Logs/vr_regress.log`。

| 容器 | 结果 | 证据（Editor.log 原文） |
|------|------|------------------------|
| **2_20B**（M7 四族+换 seed 六轮） | ✅ ALL PASS | `[2_20B冒烟] ==== 第 6 轮汇总 ALL PASS | 清场前营地数=3 ====`（P8~P13 + R6 清场三项逐条 PASS） |
| **2_20C**（M8/M9） | ✅ ALL PASS | `[2_20C冒烟] ===== ALL PASS（M8 基准生效/同族差异/零回归/端到端 + M9 走查/Cavalry负/运行时/P9 消费）=====` |
| **2_20**（种族域 D467~D472） | ✅ ALL PASS | `[2_20冒烟] ===== ALL PASS（种族域 D467~D472 行为级探针）=====` + `⑤a/⑤b/⑤c OK` |
| **Smoke_5**（2_17 步骤10 兵力目标 D348） | ✅ ALL PASS | `[2_17_5冒烟] ===== ALL PASS（威胁上调→目标升→⑦分数升，D348）=====` |

**汇总**：`[FAIL]` 末 12 万行 **0 条**；编译 console 仅存量 1 条（`233 node options failed to load`）→ **件8/9/10/11 无编译错误/回归退化**。

**T2.14 预置链摆放核对（本跑批派生证据=部分达成）**：`Editor.log` 直读 `[KingdomFoundry] <模板> 王国(kingdomId=N) 建筑预置 6/6 座`（RiverBay/DenseForest/IronHoof/SnowRock/Bedrock 全 **6/6**）+`第一代立国完成：立国 3~4 个 AI 王国` → **新 footprint（含 mine/House 2×2 多占格）下六模板预置零覆盖失败**；逐 def 点位的完整核对仍归执行端清单化。

**观察项（非阻塞，件9 T9.2 派生）**：`TopLeftHUD.Update → UpdatePatrolButton`（L281）每帧取 `SelectionController.Instance`（UI→系统强耦合 + Singleton 懒创建告警 `[SelectionController] 场景中未找到实例，自动创建`）；同款告警 ToastManager/LoadManager 既有（**非新缺陷**）→ 建议 T9.2 顺手改 null 安全读/缓存（低优）。

**未覆盖（交还执行端，附规格）**：
- **T8.1 磐石 20→10 行为级补测**：容器 `Valley_HH131_BedrockProbe` **尚未建档**（新建 Editor 容器属代码=执行端）；2_20B 的 P9 已覆盖「18×0.55→10」，但 ≠ 「20→10 基准±1」。
- **T11.6 弹话进局验证**：需活局多采样族池句（本跑批容器不含该断言，需专项探针）。
- **件9 行为探针 P1/P2（巡逻派发/打断回巡逻）、P5/P7（阶段列/染色）**：需专项活局探针（未建档）。
- **T9.1/T9.4 双分辨率过目**：视觉过目=人眼项（用户/策划端）。
- **同 seed 双跑（L-22）**：本次为单跑；确定性双跑归执行端收口（HH.160）。
- **M7 15_账本登记**：训练仓侧，归执行端。
