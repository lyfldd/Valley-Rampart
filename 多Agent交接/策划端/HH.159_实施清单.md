# HH.159 积压小任务清仓批 · 实施清单（执行计划）

> 定位：**HH.159 任务书的配套执行计划**——把 11 件逐项拆到「改哪个文件 / 改成什么样 / 怎么验收 / 证据在哪」的可执行颗粒度。
> 生成依据：skill `implementation-plan-sync`（五铁律：逐节扫源头 / 跨文档依赖显式 / 参数层与架构层并重 / 可执行可验收 / 完整性校验）。
> 裁决链：D609（矿石复活）· D610（HH.159 签发）· **D612（范围扩=件5~件11）** · **D613（mine footprint 2×2）** · **D614（House footprint 2×2+房容 ×4+SO 化）**。
> 主体：**策划端单写者**（本清单）→ **执行端施工**（HH.159 载体）→ 验收后完成报告=HH.160。
> 状态：🟡 待执行端接单；本清单与 HH.159 任务书冲突时以**本清单**为准（更细），与源任务书（HH.131/132/133/153、2_24 实施计划、QQQ.6）冲突时以**源任务书**为准。

---

## §一 来源文档集（清单源头，铁律 1）

| # | 文档 | 作用 | 扫描范围 |
|---|------|------|---------|
| 1 | `多Agent交接/策划端/HH.159_插队合并微批_任务书.md` | 主载体（件1~件11 范围） | 全文 §一~§八 |
| 2 | `0.6_审查决策记录.md` §一百三十八（D609） | 件1 矿石设计真源 | D609 全文 |
| 3 | `改造计划/建筑功能与等级总表_v2.md` §1/§1.1 | 件2 footprint 终版口径 | §1.1 全表 |
| 4 | `3.1.3_美术资源生产排期.md` §六 | 件2 画布档位+验收四查 | §六 格式规范/图量 |
| 5 | `改造计划/2_24_基础服务主体对称_实施计划.md` 批1 节 | 件3 B1-1~B1-5 规格 | 批1 任务表 |
| 6 | `多Agent交接/美术端/HH.155_建筑footprint正方形化定稿任务包.md` + `HH.161_矿洞footprint改2x2变更申报.md` + `HH.163_房屋footprint改2x2含房容补偿变更申报.md` | 件2 施工注意+两笔变更终裁 | §一/§二 + HH.161/163 裁决区 |
| 7 | `多Agent交接/策划端/HH.131_六考后零碎包_任务书.md` | 件8 规格 | §二 施工详单 |
| 8 | `多Agent交接/策划端/HH.133_UI视觉小批总锚_任务书.md` | 件9 规格 | §二 施工详单 |
| 9 | `多Agent交接/策划端/HH.132_代码化二态v1_实施计划.md` | 件10 规格 | §二/§三 |
| 10 | `多Agent交接/策划端/HH.153_AI弹话文案族化批_任务书.md` + `QQQ.6_AI弹话文案族化与丰富化.md` | 件11 规格+文案全稿 | §二 范围/QQQ.6 需求2/3 |
| 11 | `缺陷台账.md` DZ-054/061/062/063/077/078/079/083 | 件4/件6/件7 来源 | 对应行 |
| 12 | `多Agent交接/执行端/HH.164_HH159件1矿石落库链缺口与D614同步缺口上报.md` §裁决区（D617） | 件1 Ore 国库链/T6.1 降级/T7.3/件3 核验 裁决 | 七项全裁 |

---

## §二 任务总表（铁律 3/4：参数层与架构层并重、可执行可验收）

### 件1 矿石复活链（架构，D609）

| 编号 | 任务 | 类型 | 依赖 | 文档出处 | 验收标准 | 证据文件 |
|------|------|------|------|---------|---------|---------|
| T1.1 | 铁匠铺变换输入改 Ore：`_storage.Transform(ResourceType.Stone,…)` → `ResourceType.Ore` | 架构 | — | D609·HH.159 §1.2-1a | 代码直读=Ore；Stone 不再参与 | BlacksmithBuilding.cs L51 |
| T1.2 | Transform 守卫改 `@in != ResourceType.Ore \|\| @out != ResourceType.Metal` + 注释同步（原料取国库 Ore 真源） | 架构 | T1.1 | D609·1b | 代码直读守卫=Ore | StorageComponent.cs L99/L93-96 |
| T1.3 | BlacksmithDef 字段改名 `stoneToMetalRatio`→`oreToMetalRatio`（含 MetalFrom）+资产同步+tooltip 改 | 参数 | T1.1 | D609·1c | grep `stoneToMetalRatio` 全库零命中 | BlacksmithDef.cs L11-16 + BlacksmithDef.asset |
| T1.4 | mine 矿石伴生：MineByproductComponent 加 Ore 第三子仓（rate/capacity 取 KingdomConfig 新字段）+存档尾插 | 架构 | — | D609·1d | 正门建局 mine Ore 子仓增长；旧档缺字段→0 | MineByproductComponent.cs 全文 + KingdomConfig + BuildingSaveData |
| T1.5 | ore_vein 改矿石主产：`outputResource: 1`→`4` | 参数 | — | D609·1e | ore_vein 采集后国库 Ore>0 | ore_vein.asset L39 |
| T1.6 | 注释/显示勘正五文件：GameEvents L182-184/L191 前例句、BuildingDef L59 tooltip、AbstractEconomySettlement L202 | 参数 | T1.2/T1.3 | D609·1f | grep「矿洞主产」「石→Metal」注释零残留 | GameEvents.cs / BuildingDef.cs / AbstractEconomySettlement.cs |
| T1.7 | 建筑描述 asset「石→Metal」→「矿→Metal」+全库 grep 该文案 | 参数 | T1.3 | D609·1g | grep「石→Metal」零命中 | Blacksmith.asset |
| T1.8 | **Ore 国库链同构补齐（HH.164/D617 前提项，T1.1/T1.2/T1.4/T1.5 前置）**：①`TreasureVault.Managed += Ore` ②`KingdomManager.TreasuryOre`（含 LoadState/ResetState 同步）③`KingdomSaveData.treasuryOre` 尾插（旧档缺→0，零 schema bump）④`TreasureVault.Init` 回填一行；**AI 侧**=`KingdomState.ore` 独立桶 + `TaskScheduler.AddGatherOverflow` 加 `case Ore`（照水晶/火油先例，**不进 ResourcePack 五经济资源**） | 架构 | — | HH.164 §二阻塞1 / D617 | Ore 入库不静默丢（`Deposit(Ore)`>0）；AI 侧 Ore 不落空；读档回归 | TreasureVault.cs + KingdomManager.cs + KingdomSaveData.cs + KingdomState.cs + TaskScheduler.cs |

### 件2 footprint 正方形化+房容（参数/架构，HH.155/D613/D614）

| 编号 | 任务 | 类型 | 依赖 | 文档出处 | 验收标准 | 证据文件 |
|------|------|------|------|---------|---------|---------|
| T2.1~T2.12 | 12 笔 `.asset footprint` 变更（Barracks 3×1→2×2 / Blacksmith / Hospital / Church / Warehouse / SiegeWorkshop / Ranch 2×1→2×2 / VagrantCamp 4×1→2×2 / farm 1×1→2×2 / castle 2×1→3×3 / **mine 1×1→2×2（D613/HH.161）** / **House 1×1→2×2（D614/HH.163）**） | 参数 | — | HH.155 §1.1 + HH.161 + HH.163 | 逐笔 `footprint: {x,y}` 直读=新值 | Assets/Resources/Buildings/*.asset |
| T2.13 | 传送门 def 建档落 2×2（威胁臂建筑；字段参照 rift 退役档语义+faction=敌对） | 架构 | — | HH.155 §1.2 | def 在场+footprint 2×2 | 新建 portal.asset |
| T2.14 | AI 预置链摆放核对（baseBuildingDefIds 六模板按新占地复核，重叠列报；**mine/House 2×2 多占一格已含**） | 架构 | T2.1~T2.13 | HH.155 §2.1 + HH.161 §2.5 + HH.163 §3.6 | 六模板预置建筑新占地无重叠；有问题列报 | Kingdom_*.asset |
| T2.15 | 选址回归（占地变化→AI 行动池选址打分空间；冒烟确认无新 regression） | 架构 | T2.1~T2.13 | HH.155 §2.1 | D598 F1 特征面无新退化 | Smoke 容器日志 |
| T2.16 | **房容 ×4+SO 化**：`HappinessSystem.cs:297` `GetHouseCapacity` 硬编码 3/5/8→**12/20/32** + 数值移入 `KingdomConfig`（读口 GetHouseCapacity/GetHouseCapacityByKingdom 自动生效） | 架构 | — | HH.163 §3.2 / D614 | 代码直读房容=12/20/32；grep 硬编码 `? 8 :` 三分支零命中；`KingdomConfig.asset` 含房容字段 | HappinessSystem.cs + KingdomConfig.cs/.asset |
| T2.17 | **测试夹具重算+读口复核**：`TestFixtureTiersConfig`「每 House=3」假设按 ×4 重算 `houses` 数（否则生育门槛恒过=测不出）+ HH81 P1 `cap>0` + `TestFixtureApi.EvaluateBirthConditions` 读数刷新 | 参数/测试 | T2.16 | HH.163 §3.5 / D614 | T9/M8 生育门槛断言仍可判别（非恒过）；HH81 P1 全绿 | TestFixtureTiersConfig.cs + Valley_HH81_Smoke.cs + TestFixtureApi.cs |

### 件3 2_24 批1（架构/参数，D605）

| 编号 | 任务 | 类型 | 依赖 | 文档出处 | 验收标准 | 证据文件 |
|------|------|------|------|---------|---------|---------|
| T3.1 | B1-1 BuildingFactory.CreateBuildingInstance faction 按 kingdomId 派生（0=PlayerCamp/>0=AiKingdom/-1=None；对齐 UnitFactory.SpawnUnit）**〔HH.164 核查：前批 HH.86 件2a 已实装→本批降级=核验/对拍，禁重复实装〕** | 架构 | — | 2_24 批1·DZ-040 / HH.164 补1·D617 | 代码直读 faction 由 kingdomId 派生非 def.faction；grep 无回退 | BuildingFactory.cs |
| T3.2 | B1-2 TaskScheduler 兜底按 brain.kingdomId 分流（Gather/Transport 溢出不再硬编码玩家国库）**〔HH.164 核查：前批 HH.86 件2d 已实装→降级核验〕** | 架构 | — | 2_24 批1·DZ-045 / HH.164 补1·D617 | AI 溢出不落玩家国库（负探针） | TaskScheduler.cs |
| T3.3 | B1-3 SatietySystem 玩家国库源 null→0 统一（**边缘语义确认（HH.164 补1·D617）：`kid>0 但国已注销` 旧=fallback 玩家国库[主体串味/资敌]→新=不进食[国已注销=单位消亡]，认可**） | 参数 | — | 2_24 批1 / HH.164 补1·D617 | null 判定改显式 0；国已注销→不进食；同 seed 行为零变化 | SatietySystem.cs |
| T3.4 | B1-4 RanchSystem 结算双桶 SettleBucket 参数化**〔HH.164 核查：前批 2_17 步骤11 批2 已实装→降级核验〕** | 架构 | — | 2_24 批1 / HH.164 补1·D617 | 桶按 kingdomId 路由 | RanchSystem.cs |
| T3.5 | B1-5 WarehouseHelper.GatherActive 参数化（硬编码 kingdomId=0→入参） | 参数 | — | 2_24 批1 | GatherActive(kingdomId)；无行为变化 | WarehouseHelper.cs |

### 件4 挂账池低优三笔（参数）

| 编号 | 任务 | 类型 | 依赖 | 文档出处 | 验收标准 | 证据文件 |
|------|------|------|------|---------|---------|---------|
| T4.1 | DZ-077：无订阅处补 HasSubscribers 守卫 or 删发布 + 两死定义删（UnitAttackEvent/BuildingProductionTickEvent）+ LODSystem L336 恒 0 表达式修 | 参数 | — | DZ-077 | 编译 0 错；无订阅处不再刷警告 | BuildController/SelectionController/NPCBrain/SaveManager/GateController/Building/LODSystem |
| T4.2 | DZ-078：econStorageOnly 分支排除 outputResource==Gold（market 误挂死仓） | 参数 | — | DZ-078 | market 不再挂 Gold 仓（grep/直读） | BuildingFactory.cs L275-279 |
| T4.3 | DZ-079：SatietySystem L240 HasBuilding("Hospital") 复用 L253 per-kingdom overload | 参数 | — | DZ-079 | AI 无玩家医院加成（正负探针） | SatietySystem.cs L240/L253 |

### 件5 文档核对（参数，策划端已完成）

| 编号 | 任务 | 类型 | 依赖 | 文档出处 | 验收标准 | 证据文件 |
|------|------|------|------|---------|---------|---------|
| T5.1 | 核对美术清单行与 3.1.3 §六（约 77 张）+总表 v2 §1.1 一致 | 参数 | — | D609 件5 | 清单行一致无冲突（策划端已改，执行端仅核） | 3.1.3 §六 / 总表 v2 §1.1 |

### 件6 A 组协同三笔（参数，D612 新增）

| 编号 | 任务 | 类型 | 依赖 | 文档出处 | 验收标准 | 证据文件 |
|------|------|------|------|---------|---------|---------|
| T6.1 | DZ-054：**降级=维持现状（ore_vein=1 / stone_pile/wood_pile=0）+补注记**（HH.164/D617 裁 B：A「三资产全置 1」会经 `WanderStimulusProvider.IsResourceDef` 真实新增 NPC 游荡锚点=批内行为漂移；三者语义差异如实非缺陷）；**DZ-054 台账以实盘 4 消费面修正确销案** | 参数 | — | HH.164 决策2 / D617 | 三资产值**不动**；注记在位；DZ-054 销案 | ore_vein/stone_pile/wood_pile.asset + 注记 |
| T6.2 | DZ-061：WarehouseHelper.TrySettle/CanAfford 补 Metal 凑单（含 metal 造价玩家不再白嫖铁） | 架构 | — | DZ-061 | 含 metal 造价实扣铁（断言国库 Metal 减） | WarehouseHelper.cs |
| T6.3 | DZ-062：BuildingMenuPanel 菜单判定改走 WarehouseHelper 同口径（与 T6.2 同批） | 参数 | T6.2 | DZ-062 | 国库不足但本地仓足→按钮可点 | BuildingMenuPanel.cs / BuildController.cs |

### 件7 B 组注释卫生（参数，D612 新增）

| 编号 | 任务 | 类型 | 依赖 | 文档出处 | 验收标准 | 证据文件 |
|------|------|------|------|---------|---------|---------|
| T7.1 | DZ-083a：CampUpgrader.cs L76 过时注释改（HH.51 批B 已接线 TryAnnex）；DZ-083b：GuardDeploymentSystem.cs L64 核实是否已修，未清则改 | 参数 | — | DZ-083 | grep 旧注释文本零命中 | CampUpgrader.cs L76 / GuardDeploymentSystem.cs L64 |
| T7.2 | DZ-063：三笔注记（①SpillToChest 折损加注待数值批裁 ②AI 非经济资源路由加注 ③AIEconomySettlement 头注释漂移修） | 参数 | — | DZ-063 | 三处注记在位 | TreasureVault.cs / AIEconomySettlement.cs |
| T7.3 | **ProducerComponent Ore 死分支清理（HH.164 决策7/D617）**：`UpdateByproductConfig` L91「outputResource==Ore 判矿洞」分支（D562 后**已不可达**：mine 改 outputResource=Stone+独立 MineByproductComponent）+`_hasByproduct/_byproductType/_byproductCapacity` 死字段删，grep 兜底；若牵连超预期→降级留最小注释+台账登记 | 参数 | — | HH.164 §二附 / D617 | 死分支+死字段零残留；编译 0 错 | ProducerComponent.cs |

### 件8 HH.131 六考后零碎包（参数/工装，纯工装零产品代码）

| 编号 | 任务 | 类型 | 依赖 | 文档出处 | 验收标准 | 证据文件 |
|------|------|------|------|---------|---------|---------|
| T8.1 | 磐石 20→10 行为级补测：新容器 `Valley_HH131_BedrockProbe`（正门 EnterTestRun+协程捕获器+Finish ExitTestRun）；替身直调法 3 步 | 架构 | — | HH.131 §二件1 | P1~P4 ALL PASS（实收=T6 公式基准±1） | 新容器 + Editor.log |
| T8.2 | 观察器白名单补 combat 死亡 tag + GameOver tag（侦察→补条→验证；只加不删） | 参数 | — | HH.131 §二件2 | 死亡 tag 行+GameOver tag 行入镜像 | Valley_P1_Observer.cs |
| T8.3 | Smoke_9 #19 断言分型（popAlarm 占位轮/让位轮/非 popAlarm 态分型；零产品代码） | 参数 | — | HH.131 §二件3·D592 列报1 | Smoke_9 全 OK+分型断言仍有判别力 | Valley2_...Smoke_9.cs |

### 件9 HH.133 UI/视觉小批（架构/参数，玩家侧 AI 零触碰）

| 编号 | 任务 | 类型 | 依赖 | 文档出处 | 验收标准 | 证据文件 |
|------|------|------|------|---------|---------|---------|
| T9.1 | 选族卡排版修正（USS 换行/加宽/加高，执行端按效果定） | 参数 | — | HH.133 §二件1 | 四族描述不裁切+四卡不超窗+1280×720 与 1920×1080 双分辨率过目 | 选族卡 USS/UXML |
| T9.2 | DZ-081 玩家巡逻入口接线（选中→巡逻按钮→2~4 路径点循环；遇敌打断回巡逻） | 架构 | — | HH.133 §二件2 | P1 真派发 / P2 打断回巡逻 / P3 AI 无入口负探针 / P4 入档语义列报 | 单位操作 UI + PatrolTaskSystem.StartPatrol |
| T9.3 | DZ-082 情报面板②阶段列+③per-kingdom 幸福读口（读公开口，禁掏私有字段）+染色高亮联动 | 架构 | — | HH.133 §二件3 | P5 阶段列一致 / P6 幸福列不串值 / P7 点选→染色高亮 | KingdomIntelPanel / KingdomListPanel / TerritoryOverlay |
| T9.4 | D240 下拉对比度视觉修正 | 参数 | — | HH.133 §二件4 | 过目 | 对应 USS |

### 件10 HH.132 代码化二态 v1（架构/工装，Editor-only）

| 编号 | 任务 | 类型 | 依赖 | 文档出处 | 验收标准 | 证据文件 |
|------|------|------|------|---------|---------|---------|
| T10.1 | LifecycleAudit.cs 骨架（`Assets/Editor/Lifecycle/`；菜单 Valley/审计；明细行+汇总行；落盘 Logs/；幂等） | 架构 | — | HH.132 §二件1 | 菜单可跑+幂等 | LifecycleAudit.cs |
| T10.2 | R1 事件双向对拍（事件全集=readonly struct `*Event`/`*Command`；三类报告；豁免表=登记表 §4 54 事件基线对账） | 架构 | T10.1 | HH.132 §二件2 | ③类报告齐；差异列报 | LifecycleAudit.cs |
| T10.3 | R2 存档覆盖对拍（A: Singleton×ISaveable 差集；B: WorldLifecycle 编排×ResetState 差集） | 架构 | T10.1 | HH.132 §二件3 | 两对拍差集报出 | LifecycleAudit.cs |
| T10.4 | 检测力标定 T1~T3（合成缺陷注入→全检出→删除，commit 零残留）+现库全扫零误报抽样 | 参数 | T10.2/T10.3 | HH.132 §二件4 | T1~T3 全检出+零误报+注入删除 | 标定记录 |

### 件11 HH.153 AI 弹话文案族化（架构/参数）

| 编号 | 任务 | 类型 | 依赖 | 文档出处 | 验收标准 | 证据文件 |
|------|------|------|------|---------|---------|---------|
| T11.1 | T1：AttentionTuningConfig 加 `talkRaceChance`(0.4) SO 化（不进 ToSnapshot） | 参数 | — | HH.153 件1/QQQ.6 T1 | grep 双在场+ToSnapshot 无该字段 | AttentionTuningConfig.cs+.asset |
| T11.2 | T2：PickTalkLine 读自身 raceId 双层混合（正常态 40% 族池/60% 职业池；饥饿受伤态 100% 职业状态池） | 架构 | T11.1/T11.3 | HH.153 件2 | 双层混合生效 | UnitController.cs |
| T11.3 | T3：新增 GetRaceTalkPool(raceId)，4 族×8 句逐字录入 QQQ.6 §需求2 | 参数 | — | HH.153 件3 | 4×8 与 QQQ.6 逐字零差异 | UnitController.cs |
| T11.4 | T4：GetTalkPool 十七职业文案全量替换+战斗职业删「随时准备战斗！」 | 参数 | — | HH.153 件4 | grep「随时准备战斗」零命中 | UnitController.cs |
| T11.5 | T5：对话池提 static readonly（禁每次 new string[]） | 参数 | T11.3/T11.4 | HH.153 件5 | 池=static readonly | UnitController.cs |
| T11.6 | T6：进局验证（正门 EnterTestRun）+ AI NPC raceId 非全兜底 Human 抽查 | 架构 | T11.1~T11.5 | HH.153 件6 | 族池句多采样命中；raceId 抽查列报 | 容器日志 |

### 件12 矿山锚点簇+水域避让（生成侧，D618+DZ-084；用户指令「彻底解决、加进去」，进度8 补入）

| 编号 | 任务 | 类型 | 依赖 | 文档出处 | 验收标准 | 证据文件 |
|------|------|------|------|---------|---------|---------|
| T12.1 | **矿山锚点簇（缺口 A，D618 定档①）**：`FillFeatures` 按 **2×2 成簇**撒布矿山 + `EnsureOne`→**簇保底**（EnsurePatch，保底半径内 ≥2×2 完整簇）+**不留孤立矿山格**（无 2×2 可建子块者并簇/消除）+密度口径按簇（权重表语义「格」→「簇」） | 架构 | — | D618 / 报告 §策划裁决 | 同 seed 全图每个 `FeatureType.Mine` 格均处完整 2×2 可建子块（无孤立格，**簇=轴对齐 2×2 完整块**）；出生半径保底簇可建（**`EnsureOne` 判定改「存在完整 ≥2×2 块」≠ 任一矿格**）；**总矿格数近似不变**（簇数≈原格数÷4）；**MineralRich 命中率对照**（同 seed 改前/改后 + 阈值重标定）；既有冒烟无「矿山格计数」硬断言（有→列报，D621） | MapGenRules.cs |
| T12.2 | **水域避让矿山（缺口 B，DZ-084）**：`PlaceRiver`/`PlaceLakes` 增 `FeatureType.Mine` 避让判断（河/湖不覆写矿山格）——**与 T12.1 同段生成代码一次改** | 架构 | T12.1（同趟） | D618 / DZ-084 | 同 seed 图水域后无被切碎的矿山簇；Tree/StonePile 不保护（维持现状） | MapGenRules.cs |

**件12 纪律**：与 T12.1 同文件串行（L-12）；改前重读磁盘态；**必须与缺口 A 同趟落地**（半态=大量可见不可建矿山格）；不碰 `mine.asset`（footprint 已 D613 落）。**D621 复核补三条硬性**：①**`EnsureOne`「已有」判定须改**（现 L299=半径内**任一** mine 格即 return → 改为「半径内存在**完整 ≥2×2 轴对齐可建块**才 return」，否则孤立格充数=**保底静默失效**[而孤立格又被 T12.1 清掉=保底彻底落空]，L-01 就位≠生效）；**簇=轴对齐 2×2 完整块（非任意连通 L 形）**，判定写死；②**总矿格数近似不变**（D618「避免矿区总量膨胀」⇒**簇数≈原格数÷4**，改后总格数落原值±小比例内；改前/改后同 seed 对照按此口径报，**非"变多 4 倍"**）；③**`MineralRich` 立国选址兼容核对**（`MapGenRules.cs` L208-210 `ChunkFeatureRatio(Mine)≥cfg.mineralDensityThreshold`[缺省 0.05=**按格占比**]——簇化改空间分布与总量⇒阈值口径漂移⇒MineralRich 模板命中率变化→走 D292 回退或改变 AI 立国选址；**须给同 seed 改前/改后命中率对照 + 阈值按新口径重标定**，登记 15_账本/总表）。**建议（非硬性）**：成簇+清孤立+簇保底写成**按特征参数化的可复用工具**（不硬编码 Mine），为 OreVein 将来 2×2 预留（矿石已复活成真资源 D609/D617），防二次返工（用户"彻底解决"口径）；成本明显超一行级则列报不强求。

**任务计数**：件1=8 · 件2=17 · 件3=5 · 件4=3 · 件5=1 · 件6=3 · 件7=3 · 件8=3 · 件9=4 · 件10=4 · 件11=6 · **件12=2** = **52 条**（件1 由 7→8=HH.164/D617 +T1.8 Ore 国库链；件7 由 2→3=+T7.3 死分支清理；件2 17=HH.163/D614；**件12=2=D619 进度8 补入（D618 矿山锚点簇+DZ-084 水域避让）**；件3 三项降级核验/件6 T6.1 降级=D617，条数不变）。

---

## §三 跨文档依赖矩阵（铁律 2）

| 本批任务 | 依赖文档 | 依赖任务/接口 | 状态 |
|---------|---------|--------------|------|
| T1.1/T1.2/T1.3 | D609 | 「Ore→Metal」变换链设计定稿 | ✅ 已定（0.6 §138） |
| T1.4 | KingdomConfig（Unity 侧） | 新增 byproductOreRate/Capacity 字段 | ⚠️ 待建（本批任务内） |
| T1.4 | BuildingSaveData | byproductOreAmount 尾插 | ⚠️ 待建（本批任务内） |
| T1.4 | sim-sync / 15_账本 | KingdomConfig 两字段（byproductOreRate/Capacity）+尾插+变换语义注记 | ⚠️ 登记义务（M7，**HH.164/D617 扩面 +KingdomSaveData.treasuryOre**） |
| T1.8 | 无（本批任务内新建） | TreasureVault/KingdomManager/KingdomSaveData/KingdomState 加 Ore 桶 | ⚠️ 待建（T1.1/T1.2/T1.4/T1.5 前置） |
| T2.1~T2.12 | HH.155+HH.161+HH.163 | footprint 12 变更+1 新建终版表 | ✅ 已定（用户拍板/D613/D614） |
| T2.13/T2.14 | 建筑体系重构批 | 预置链迁移主体（本批仅核对，发现问题列报） | 🔴 六考后（不阻塞本批核对） |
| T2.16/T2.17 | HH.163 §3.2/§3.5（D614） | 房容值+SO 字段规划（KingdomConfig 单写） | ✅ 已定 |
| T3.x | 2_24 实施计划批1 | B1-1~B1-5 规格（**B1-1/B1-2/B1-4 前批已实装→本批核验；B1-3/B1-5 实装**，HH.164 补1/D617） | ✅ 已定（D605） |
| T3.1 | UnitFactory.SpawnUnit | faction 派生典范参照 | ✅ 已在库 |
| T6.1 | —（降级为维持现状+注记） | 不再改 ore_vein.asset（HH.164/D617） | ✅ 已降级 |
| T6.3 | T6.2 | 同批（口径一致） | ✅ 批内 |
| T8.1 | Bedrock prefab / NpcProfessionDef Occ31 | 替身法绕 prefab 缺失（内存载三值） | ✅ 已定（HH.131） |
| T9.2 | PatrolTaskSystem.StartPatrol | 消费端接线 | ✅ 接口已在库 |
| T9.3 | TerritoryOverlay.HighlightKingdom | D452 高亮端口 | ✅ 已在库 |
| T9.2 | 2_11 存档 v2 | 巡逻态入档与否（列报，不扩本批） | ⚪ 归属 2_11 |
| T10.2 | 生命周期登记表 §4 | 54 事件基线（豁免表初始集） | ✅ 已在库（HH.106） |
| T10.3 | WorldLifecycle.ResetWorldForNext | 编排清单 | ✅ 已在库 |
| T11.x | QQQ.6 | 文案全稿（逐字） | ✅ 已用户拍板 |
| T11.1 | sim-sync | talkRaceChance 不进 ToSnapshot（表现层零义务） | ✅ 已判（sim 零义务） |

**前置缺口**：无**外部**阻塞性前置，但**批内 T1.8（Ore 国库链）为 T1.1/T1.2/T1.4/T1.5 硬前置**（HH.164/D617）；T1.4 的 KingdomConfig/BuildingSaveData / T1.8 的 TreasureVault/KingdomManager/KingdomSaveData/KingdomState 字段均属本批任务内新建，非外部依赖。

---

## §四 分件施工详单（怎么做）

> 通用开工序：**动文件前必重读磁盘最新内容**（并发纪律 #1）→ 改 → **写后 grep/pwsh 直读验证**（L-02）→ 单件自证后再下一件。

### 件1 矿石复活链（核心件，施工序：**T1.8**→T1.1→T1.2→T1.3→T1.4→T1.5→T1.6→T1.7）

**前置 T1.8（先做，HH.164/D617）**：Ore 国库链同构补齐——①`TreasureVault.Managed` 加 `ResourceType.Ore` ②`KingdomManager` 加 `TreasuryOre`（`LoadState`/`ResetState` 同步）③`KingdomSaveData.treasuryOre` 尾插（旧档缺→0，零 bump）④`TreasureVault.Init` 回填（`Deposit(Ore, km.TreasuryOre)`）；**AI 侧**=`KingdomState` 加 `ore` 独立桶（照 `crystal/fireOil` 先例，**不进 ResourcePack 五经济资源**）+`TaskScheduler.AddGatherOverflow` 加 `case ResourceType.Ore`。**不补则 T1.5 改产后 Ore 入国库静默丢**（HH.164 实盘证伪清单「国库槽已在场」：`Deposit` 对未纳管类型 `return 0`）。

1. **T1.1**：`BlacksmithBuilding.cs` Tick() 内 `_storage.Transform(ResourceType.Stone, ResourceType.Metal, stoneNeeded)` → 第一参改 `ResourceType.Ore`。
2. **T1.2**：`StorageComponent.cs` Transform() 首行守卫改 `if (@in != ResourceType.Ore || @out != ResourceType.Metal) return 0;`；XML 注释「校验输入石足够」→「校验输入矿石足够」。
3. **T1.3**：`BlacksmithDef.cs` 字段 `stoneToMetalRatio`→`oreToMetalRatio`、方法 `MetalFrom(int stoneAmount)`→参数名/语义改 Ore；`BlacksmithDef.asset` 同步字段名（**改前重读 asset**）；tooltip 改「矿石→Metal 转化率（占位 2:1，D609）」。
4. **T1.4**：`MineByproductComponent.cs` 仿 Crystal/FireOil 双仓加第三个 `_oreStore`（CreateSubStore/GetStore/TickStore/SaveByproductState/RestoreByproductState/TryAdvertiseStore 六处扩展）；`KingdomConfig` 尾插 `byproductOreRate=0.05f`/`byproductOreCapacity=20`；`BuildingSaveData` 尾插 `byproductOreAmount`（旧档缺→0 零 bump）。
5. **T1.5**：`ore_vein.asset` L39 `outputResource: 1`→`4`（**与 T6.1 同文件→串行，见 §五**）。
6. **T1.6**：`GameEvents.cs` L182 `Ore // 矿石（矿洞主产；→仓库）`→`矿石（矿场伴生/矿脉主产；铁匠铺 Ore→Metal 原料，D609）`；L183-184 Crystal/FireOil 注释补「档位化=重构批件③目标态」；L191 前例句核对；`BuildingDef.cs` L59 isBlacksmith tooltip「石→Metal」→「矿石→Metal」；`AbstractEconomySettlement.cs` L202 注释同步（**代码语义不动**，铁匠铺仍产 Metal）。
7. **T1.7**：`Blacksmith.asset` description「石→Metal」→「矿→Metal」；全库 grep「石→Metal」清残留。

**探针**：P1 正门建局→mine Ore 子仓增长；P2 国库 Ore≥ratio→铁匠铺 Tick→Metal 增+Ore 扣（Stone 不参与）；P3 金属下游零回归（兵种强化/建造 metal cost 照常）；P4 石头链零回归（石弹/低级建造/石堆采集）。

### 件2 footprint+房容（施工序：T2.1~T2.12 逐笔 → T2.13 传送门 → T2.14 预置链 → T2.15 选址回归 → T2.16 房容代码 → T2.17 夹具/读口复核）

1. 逐笔改 `Assets/Resources/Buildings/*.asset` 的 `footprint: {x, y}`（**T2.1~T2.12 共 12 笔，含 mine 1×1→2×2（D613）+House 1×1→2×2（D614）**）；新建 `portal.asset` 落 2×2（T2.13）。
2. T2.14：核对六 `Kingdom_*.asset` 的 `baseBuildingDefIds` 预置点位按新占地无重叠（2×1→2×2 多占一排格；营地 4×1→2×2 同理；mine/House 2×2 多占一格）。
3. T2.15：四容器冒烟确认 AI 选址打分无新 regression（D598 F1 特征面）。
4. **T2.16 房容代码**：`HappinessSystem.cs:297` 把 `level >= 3 ? 8 : level >= 2 ? 5 : 3` 三值改 **12/20/32**，并将数值移入 `KingdomConfig`（新字段，如 `houseCapacityByLevel`/`houseBaseCapacity`）——`GetHouseCapacity`/`GetHouseCapacityByKingdom` 从 cfg 读（读口统一，全链自动生效）。**工前先规划 KingdomConfig 字段一次落**（§五 M5 串行红线：与件1 byproductOreRate **同 SO 一次落**；**HH.164 复核勘正**=KingdomConfig 实际仅 T1.4/T2.16 两处触碰——T3.4 已实装无新字段、T11.1 talkRaceChance 落 `AttentionTuningConfig` 非本 SO）。
5. **T2.17 测试夹具+读口复核**：`TestFixtureTiersConfig` 的 `houses` 数按每 House=12 重算（保 `houseCapacity>population` 且**非恒过**——需保留可判别性）；HH81 P1 `cap>0` 与 `TestFixtureApi.EvaluateBirthConditions` 读数刷新。

**施工注意（HH.155 §二）**：占格判定字段驱动（`GridSystem.MarkOccupiedFootprint`，Building.cs:79/686）=预计零代码改动；旧档 footprintW/H 与 def 脱钩（Building.cs:655-657，未发布不迁移 D547）；城门 GateOrientation（Building.cs:68）不受影响（gate 维持 2×1）。

### 件3 2_24 批1（B1-1~B1-5，规格照 2_24 实施计划批1 表）

- B1-1 对齐 `UnitFactory.SpawnUnit` 的 faction 派生写法；B1-2 照抄 `AddGatherOverflow` 分流模式；**B1-4 已实装（`SettleBucket(kingdomId)` per-kingdom 参数在位）无新 KingdomConfig 字段——不进串行面**（HH.164 复核勘正）。
- 每项验收=代码直读+grep 双锚点+四容器玩家零回归+编译 0 error。

### 件4 挂账池低优三笔

- T4.1 按 DZ-077 点位清单逐处补 `HasSubscribers` 守卫（参考已在库的 RegionHeatChanged/KingdomBrainCreated 典范）+删两死定义+修 LODSystem L336 恒 0 表达式。
- T4.2 `BuildingFactory.cs` L275-279 econStorageOnly 分支加 `outputResource != ResourceType.Gold`（或 `>0` 校验）。
- T4.3 `SatietySystem.cs` L240 `HasBuilding("Hospital")` 加 kingdomId 参数复用 L253 overload。

### 件5 文档核对

- 核对 3.1.3 §六 美术清单行与总表 v2 §1.1 一致（策划端已在 D609 改完），无改动义务。

### 件6 A 组协同

- T6.1 与 T1.5 同文件串行；三资产 `isResourceNode` 统一（ore_vein=1 为对齐基线，stone_pile/wood_pile 是否改齐=执行端按消费面判定后列报）。
- T6.2 `WarehouseHelper.TrySettle/CanAfford` 的 LockTakes 三资源扩为含 Metal（Vault_Metal 已在 Registry）。
- T6.3 菜单判定改走 WarehouseHelper 同口径（与 T6.2 同一批改，防口径再次分裂）。

### 件7 B 组注释卫生

- T7.1 a 改 CampUpgrader.cs L76；b 先 grep/直读 GuardDeploymentSystem.cs L64 确认状态（未清则改）。
- T7.2 三处注记（SpillToChest 折损注/AI 非经济资源路由注/AIE 头注释漂移修）。

### 件8 HH.131（规格照原任务书 §二 三件）

- T8.1 替身法 3 步（spawn 生产 Prefab 实例→内存载磐石三值→直调 ApplyDamage）+P1~P4；**禁写回 SO**。
- T8.2 侦察→补条→验证（只加不删+集中管理）。
- T8.3 Smoke_9 #19 断言分型（零产品代码）。

### 件9 HH.133（规格照原任务书 §二 四件）

- T9.2 巡逻入口接线（D520 接口纪律：走公开口，禁掏私有字段）。
- T9.3 情报面板两笔+染色高亮联动（读公开口）。
- 行为级探针正负双侧（L-01）。

### 件10 HH.132（Editor-only，规格照原实施计划 §二 四件）

- 反射域=Assembly-CSharp 仅；豁免表变更需策划端确认（D561 红线）；T1~T3 注入缺陷验证后删除（commit 零残留）。

### 件11 HH.153（规格照原任务书+QQQ.6）

- 文案**逐字录入 QQQ.6**，禁自行润色；注意力/冷却触发面零触碰（QQQ.2 DR-10 已封死）。

---

## §五 施工序与串行纪律

**推荐施工序**（低耦合先行、核心件留余量）：件6/件7（注释+协同，快）→ 件4（低优）→ 件2（资产批量）→ 件1（核心链）→ 件3（2_24）→ 件5（核对）→ 件8/件9/件10/件11（独立小批）。

**批内串行红线（M8，必须遵守）**：

| 共享文件 | 触碰任务 | 纪律 |
|---------|---------|------|
| `KingdomConfig`（+asset） | T1.4 · **T2.16** | **一次规划全部字段后一次落**（T1.4 byproductOreRate/Capacity 两字段 + T2.16 房容字段；**HH.164 复核勘正**：T3.4 已实装无新字段、T11.1 talkRaceChance 落 `AttentionTuningConfig` 非本 SO） |
| `ore_vein.asset` | T1.5（**T6.1 已降级=不动，脱离串行**） | 单改，T1.5 |
| `TreasureVault.cs`/`KingdomManager.cs`/`KingdomSaveData.cs`/`KingdomState.cs`/`TaskScheduler.cs` | **T1.8**（另与 T1.4 共 `KingdomState`/`TaskScheduler`、与 T3.2 共 `TaskScheduler`） | **T1.8 最先落**（T1.1/T1.2/T1.4/T1.5 前置）；`TaskScheduler.cs` 同文件串行 |

**其他纪律**：同文件多处编辑串行（L-12）；改前必重读（并发 #1）；写后必验（L-02）；进局测试一律正门 `TestEnterRun`（L-17）；冒烟走生产链（L-06）；容器收尾 `ExitTestRun`（L-17）。

---

## §六 完整性校验表（铁律 5：反向核对）

| 文档章节 | 清单任务编号 | 状态 |
|---------|------------|------|
| D609 输出端 mine 伴生 | T1.4 | ✅ |
| D609 输出端 ore_vein 改产 | T1.5 | ✅ |
| D609 变换链 Ore→Metal | T1.1/T1.2/T1.3 | ✅ |
| D609 注释勘正 | T1.6/T1.7 | ✅ |
| D609 石头链/金属下游零回归 | P3/P4 探针 | ✅ |
| HH.155 §1.1 十笔变更 + HH.161 第 11 笔（mine）+ HH.163 第 12 笔（House） | T2.1~T2.12 | ✅ |
| HH.155 §1.2 传送门建档 | T2.13 | ✅ |
| HH.155 §1.3 维持不改（gate 2×1） | （不改=预留） | ✅ 已覆盖（mine 已 D613、House 已 D614 移入变更表） |
| HH.155 §2.1 预置链+选址 | T2.14/T2.15 | ✅ |
| HH.163 §3.2 房容 ×4+SO 化 | T2.16 | ✅ |
| HH.163 §3.5 测试夹具重算 | T2.17 | ✅ |
| 2_24 批1 B1-1~B1-5 | T3.1~T3.5 | ✅ |
| DZ-077/078/079 | T4.1/T4.2/T4.3 | ✅ |
| D609 件5 文档核对 | T5.1 | ✅ |
| DZ-054/061/062 | T6.1（降级）/T6.2/T6.3 | ✅ |
| HH.164 件1 Ore 国库链缺口 | T1.8 | ✅ |
| HH.164 件6 isResourceNode 维持现状（DZ-054 修正销案） | T6.1（降级） | ✅ |
| HH.164 件7 ProducerComponent 死分支 | T7.3 | ✅ |
| HH.164 件3 已实装核验（B1-1/2/4）+B1-3 边缘语义 | T3.1/T3.2/T3.3/T3.4（核验） | ✅ |
| HH.164 决策3 LODSystem（已落地） | T4.1 | ✅ |
| DZ-083/063 | T7.1/T7.2 | ✅ |
| HH.131 §二件1/2/3 | T8.1/T8.2/T8.3 | ✅ |
| HH.133 §二件1/2/3/4 | T9.1/T9.2/T9.3/T9.4 | ✅ |
| HH.132 §二件1/2/3/4 | T10.1/T10.2/T10.3/T10.4 | ✅ |
| HH.153 件1~件6（=QQQ.6 T1~T6） | T11.1~T11.6 | ✅ |

**校验结论**：11 件全覆盖，无遗漏；无未承接的跨文档依赖（见 §三）。**唯一范围外项**：HH.103 美术资源接入批（独立，未并入本批）。

---

## §七 验收证据清单与完成报告

- **完成报告 = HH.160**：需含 ①git 面构成（逐件文件清单）②**50 条**任务逐条自证（探针判读+grep 双锚点）③15_账本登记回执（M7：KingdomConfig 两字段+BuildingSaveData 尾插+**KingdomSaveData.treasuryOre**+变换语义注记）④教训核查行。
- **分件独立验收**：件1~件11 各自独立自证，单件失败不阻塞其余件（但同文件串行编辑须遵 L-12）。
- **回归底座**：四容器（2_20/2_20B/2_20C/Smoke_5）+同 seed 双跑（L-22 纪律）。
- **grep 双锚点总表**：`stoneToMetalRatio` 零命中 · 「石→Metal」零命中 · 「随时准备战斗」零命中 · 旧注释文本零命中（件7）· `talkRaceChance` cs+asset 双在场且 ToSnapshot 无该字段。

---

> 生成：策划端 2026-09-10（D612 范围扩配套；implementation-plan-sync 五铁律）· 主体=策划端单写者
> 后续：用户将启用 spec 技能——本清单可作 spec 的输入源（需求以本清单 §二/§四 为骨架）
