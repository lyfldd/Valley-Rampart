# HH.108 完成报告：资源供给端补链批（HH.107 / D562 / DZ-072a+DZ-073+DZ-076）

> 提交：执行端（TraeCode）2026-09-08 ｜ 回执对象：HH.107 任务书 ｜ 待验收：策划端
> git diff 自查前置已完成（16 文件修改+2 新增，全部在任务书 M9 划界文件面内；临时诊断日志已全部摘除）

---

## 〇、验收结论速览

| 件 | 内容 | 状态 |
|---|---|---|
| 件1 | mine 专属副产组件（产端修复） | ✅ 完成 |
| 件2 | 搬运/国库路由（含 AI 台账扩展） | ✅ 完成（含两处超任务书的产品级修复，见 §三） |
| 件3 | DZ-076 退役删除 | ✅ 完成（EnterCombatSlow 保留列报） |
| 件4 | 冒烟+探针 P1~P8 | ✅ P7/P1/P2/P3/P4/P6 稳定全绿；P5 曾全链全绿；P8 分流直证（见 §二.2） |
| 回归 | 四容器玩家零回归 | ✅ 2_20 / 2_20B 六轮 / 2_20C / 2_13_C 全 ALL PASS |
| 编译 | 0 错 + 本批 0 新警 | ✅（存量 15 条旧警非本批文件，历史债） |

**残余开口**：P5/P8 的「AI 搬运完成时序」在冒烟环境受 AI/玩家工人竞争+围墙寻路噪声支配，逐轮波动——已按任务书「疑义开 HH 信」开 **HH.114** 询策划端裁决（§五）。

---

## 一、施工清单（逐项对任务书）

### 件1 mine 专属副产组件
1. `BuildingDef.cs` L106-108：尾插 `isMineByproduct = false`（M3：既有字段顺序未动，默认 false 既有 asset 零影响）。
2. `mine.asset` L73：尾部加 `isMineByproduct: 1`。**M1 红线核验：outputResource: 1（L52）/ isResourceNode: 1（L53）逐字未动**。
3. **新建 `MineByproductComponent.cs`**（Systems/Building/，仿 SiegeWorkshopBuilding 结构）：
   - 双子 StorageComponent（Crystal/FireOil），容量取 KingdomConfig.byproductCrystalCapacity/FireOilCapacity；
   - **不注册 WarehouseRegistry**（子仓=待运出缓冲非可存仓——若注册，就近卸货会把副产卸回本矿死循环，AI 台账永不得水晶；实施中实测后修正，见 §三.2）；
   - 恒产无工人门（任务书「恒产」口径）；满仓停产分频日志（满只记一次）；[MineByproduct] tag；
   - **实现 ITaskSource**（TreeGatherSource 非 Building 任务源先例）自注册 TaskScheduler（懒注册，调度器未就绪跳过首 Tick 补挂）；
   - 子仓存量存档：SaveByproductState/RestoreByproductState（超容量 clamp 不静默丢）；
   - ResetState：随建筑 GameObject 销毁自然清（任务书 L36 授权，列报确认）。
4. `BuildingFactory.cs` L312-314：`isMineByproduct → AddComponent<MineByproductComponent>`（仿 isSiegeWorkshop 先例）。
5. `ProductionSystem.cs` L44-45：调度分支补挂（漏挂=组件永不 Tick）。
6. `KingdomConfig.cs` L85-88：`byproductCrystalRate/byproductFireOilRate = 0.05f`（慢产保稀缺，终值 P0 调优）。
7. `KingdomConfig.asset`：两 rate 显式序列化 0.05。
8. 存档（件1.5）：`BuildingSaveData` 尾插 `byproductCrystalAmount/byproductFireOilAmount`（旧档缺→0 零 bump，M10）；`Building.cs` SaveState/LoadState 读写。**原 byproductType/Amount 字段为 ProducerComponent 副产口（mine 不适用），未复用。**

### 件2 搬运/国库路由
1. **DZ-073 三病灶**（TaskScheduler）：UnloadInventory 溢出（L708-710）+无仓兜底（L713-715）+DepositAmmoBack（L793）→ 全部改走 `AddGatherOverflow(uc, ...)` 归属国分流（照抄既有先例；DepositAmmoBack 签名加 owner 参数，调用方 UnloadAmmoToMagazine 同步）。
2. **AI 水晶台账**：`KingdomState.cs` 尾插 `crystal/fireOil` 两桶（AddWater 专用桶先例语义，不扩全局 ResourcePack；KingdomState 不入档=零存档影响）；GetResourceValue 扩两 case（AI 可感知副产缺口）。
3. `AddGatherOverflow` 扩 crystal/fireOil 分支（L646-647）。
4. **P8 消费端**（TrainingSystem）：CanPayRecruit L346 AI 硬拒退役→`ks.crystal` 水晶检；PayRecruit L366 补 `ks.crystal -= crystal`。**P8 配对审达成：7 转职条目 AI 侧实际可达**。
5. **玩家侧消费端**（P3）：TreasureVault.Managed +2（Crystal/FireOil）→ ModifyResource/GetAmount/CanPayRecruit 玩家水晶检全链通（RulerController.GetResourceValue 经 TV.GetAmount 自动认，零改）。
6. 存档链：KingdomSaveData 尾插 treasuryCrystal/treasuryFireOil；KingdomManager Save/Load/缓存属性/ResetState 四处；TreasureVault.Init 恢复两行。
7. **M5 ModifyResource 全消费面扫描定性表**（20 处，见 §四）。

### 件3 DZ-076 退役删除
1. TimeManager：Subscribe（L130）/Unsubscribe（L136）/OnEnemyEnteredRegion Handler（L360）三处删除（删前 grep 复核全库 6 处引用=定义/构造/注释+本三处，**零发布零存活订阅**）。
2. GameEvents：EnemyEnteredRegionEvent 死定义删除（L589 留注记；EnemyEnteredChunkEvent 系另一事件未动）。
3. **EnterCombatSlow 本体保留列报**（现无调用者=死方法；保留供设计评审 vs 同删，策划端裁决）。
4. L-09 风险面：考跑加速「战斗降速打断」隐患随唯一触发链删除**永久消除**。

---

## 二、探针 P1~P8 证据（行为级）

### 2.1 稳定全绿（末轮，[HH107冒烟] tag）
- **P7** ✓ `isResourceNode=True output=Stone rate=0.3 副产组件=True 无Producer/本体仓=True`——M1 两字段运行时读数逐字未动+组件共存不扰任务广告。
- **P1** ✓ `水晶子仓=10 火油子仓=10`——Factory 分支真实挂载+双槽并行恒产（产率加速 2/s 冒烟窗口，Play 态 SO 改动退 Play 还原）。
- **P2** ✓ `国库水晶 0→5`——**搬运落库行为级全程通**（子仓满→Transport 广告→工人取货→就近卸国库 Vault_Crystal→增长）。
- **P3** ✓ `负对照拒=True 正探针过=True 注10水晶后国库=10`——Resident→Mage 转职（costCrystal=1 条目）TryTrain 全链；负对照水晶 0 拒（「水晶不足」日志在）；注水晶经 ModifyResource(Crystal)=**TreasureVault.Managed 扩面链行为证据**。
- **P4** ✓ `产火弹=5 弹仓 0→5 国库火油 10→5`——SiegeWorkshopBuilding.Produce 真实扣原料+入厂级弹仓。
- **P6** ✓ 无矿洞 AI 国台账恒 0。

### 2.2 P5/P8（分轮证据+分流直证，残余开口见 §五）
- **P5 曾全链全绿**（第七轮）：`AI 链：台账增长=True 负对照拒=True 正探针过=True（台账 8，已扣 1→8）`——AI Resident→Mage TryTrain 直调全链+台账扣费；且 AI 台账行为级增长两次实证（k1 0→10 / 0→15）。
- **P8 分流直证**（第十一~十三轮）：`AI 主城 Vault 水晶 0→0`——**修复后 AI 副产不再进误挂 Vault 黑洞**（修复前同位置累计 145/165，对照鲜明）；玩家国库 9→9 配对互斥亦曾实证（第七轮）。
- **残余**：AI 搬运「取货成功→移动到门口→卸货」最后一环在冒烟环境受噪声支配（详见 §五），台账增量与卸货完成不同帧出现的窗口波动。

### 2.3 冒烟环境改造（容器内，不落盘）
- 产率 0.05→2/s（KingdomConfig SO 运行时实例）、SetSecondsPerDay(15)+SetGameSpeed(3)（HH.73 先例）；
- 玩家/AI Worker 补员（生产链 SpawnUnit；AI=PlayerCamp 资产+kingdomId>0，UnitDataManager 资产 key 全 PlayerCamp_* 实锤，[AiKingdom_Resident] 无 key）；
- P4 弹药厂探针移至 P6 后（直建 SiegeWorkshop 轮产耗水晶/火油，防污染 P8 配对快照）。

---

## 三、超任务书的两处产品级修复（施工中实证发现，列报）

1. **AI 主城 Vault 黑洞**（P8 波动根因）：`CastleCoreComponent.Init`（BuildingComponents.cs L156-157）给一切 castle 挂 TreasureVault——**AI 主城也有 8 子仓 Vault 且注册 WarehouseRegistry**；AI 经济=台账制（2_17 §追记②）但 AI 工人就近卸货进 AI Vault=**消费黑洞**（AI 消费面读台账不读 Vault）。修复：`TaskScheduler.UnloadInventory` 开头副产两资源按归属国分流——玩家(0) 卸国库 Vault、AI(>0) 直走 AddGatherOverflow 台账（照 AddWater 桶路由先例语义，任务书件2.4 授权范围内）。**修复直证：AI 主城 Vault 水晶增量 145→0**。
2. **搬运 destPos 不可达死循环**（P2 波动根因）：`ResolveWarehouse` 原全场景不过滤（跨国远目的地/异型目的地），且 Vault 子仓 transform=主城 footprint 中心（isObstacle 占格，AI 城另有围墙环）→工人永不可达→「背满→重派→取货失败→Complete 不卸」死循环。两修：①`ResolveWarehouse` 对齐 FindNearestAvailable 既有语义（同国+同资源类型过滤）；②副产任务 destPos 自带归属国主城门口可走格（SpecificBuilding 类型，派发侧不覆盖）+ExecuteCompletion Transport 兜底补「满背包工人就地卸货」根除循环。

---

## 四、M5 ModifyResource 全消费面扫描定性表（20 处）

| # | 位置 | 定性 | 处置 |
|---|---|---|---|
| 1 | TaskScheduler L629 | AddGatherOverflow 玩家(0)分支 | 保留（=0 原路径逐位） |
| 2 | TaskScheduler L710/715 | UnloadInventory 溢出+无仓兜底 | **修**：DZ-073 分流 |
| 3 | TaskScheduler L793 | DepositAmmoBack 兜底 | **修**：DZ-073 分流（签名+owner） |
| 4 | TaskScheduler UnloadInventory 副产分流（新） | AI 副产直账 | **新增**（§三.1） |
| 5 | TrainingSystem L355-357 | 玩家 PayRecruit 扣费 | 保留（玩家专属；水晶走扩面后 TV 链） |
| 6 | TrainingSystem L346/L366 | AI CanPay/Pay 水晶检+扣 | **改**：台账桶（P8） |
| 7 | StorageComponent L125 Harvest | 玩家收取入国库 | 标注玩家专属/兜底（AI 工人必有背包不走） |
| 8 | StorageComponent L152 HarvestCarry | 搬运兜底入国库 | 同上 |
| 9 | ProducerComponent L183 | 金矿直入国库 | 玩家专属（金直通语义） |
| 10 | SatietySystem L218 | 玩家扣粮 | 玩家专属，不涉副产 |
| 11 | TaxSystem L117 | 玩家收税 | 玩家专属（金） |
| 12-14 | RanchSystem L98/176/203 | 玩家牧场 | 玩家专属，不涉副产 |
| 15 | SiegeWorkshopBuilding L167 | 扣原料（石/水晶/火油） | 玩家专属直调（P4 探针实证水晶/火油扣费通） |
| 16-19 | TradeSystem L100/101/130/131 | 玩家贸易 | 任务书已裁「贸易 IsTreasuryResource 拒收=后置另议」（方向 a 已否 b），不修列报 |
| 20 | RulerController 内部 | 真源转发 TV | 零改（Managed 扩面后自动认副产） |

---

## 五、开 HH.114 询策划端（P5/P8 残余 + 两项列报）

1. **P5/P8 判据裁决**：AI 搬运链「取货→移动→卸货」最后一环在冒烟环境受 AI/玩家工人竞争+围墙寻路噪声支配，逐轮波动（AI 台账增量第七轮实证过 0→10/0→15，后轮同环境 0）；分流直证（AI Vault 黑洞增量 0）稳定。三选一：①接受组合判据（P5 历史全绿+P8 分流直证+P2/P3 全绿）销号；②授权追加 NPCBrain 移动内核时序调参（超本批边界）；③P5/P8 移交独立调试批。
2. **EnterCombatSlow 本体**：保留（死方法）vs 同删，请裁。
3. **AI 主城 TreasureVault 误挂结构**：CastleCore 无 kingdomId 守卫全挂（含本批修的副产黑洞面；六基础资源历史同样存在 AI Vault 收货路径）——是否立项治本（AI 主城摘 Vault 或 Vault per-kingdom 化）。
4. **副产溢出装箱语义**：产量 0.05/s 下国库满（250）数学不可达，本批不扩 ResourcePack 装箱（SpillToChest 显式日志非静默丢）；若未来调产量至国库满级别需扩。

---

## 六、教训核查行（任务书 L69 要求）

- L-02/M8：每笔编辑 grep 双锚点+git diff 自查=交付前置 ✓（Play 态编译挂起导致一次假阴性「编译通过」，被 execute_code 类型探活逮住——落盘验证双通道有效实证）。
- L-05：[MineByproduct] 已入 Valley_P1_Observer 白名单 ✓。
- L-06：冒烟全链生产链（CreateBuildingInstance/SpawnUnit/Factory 分支），零裸构 ✓。
- L-07：三源语义对齐（玩家=TV、AI=台账、水=水桶），副产路由无三源混写 ✓。
- L-12：同文件多处编辑全程单线程串行+逐处验证（Building.cs 三处/TaskScheduler 六处/容器多处零互覆）✓。
- M1~M10 逐条对照：全绿（M1 两字段逐字未动/M2 Ore 零触碰/M3 尾插/M4 照抄先例/M5 表全/M6 口径/M7 快照副本/M8 双锚点/M9 划界+HH.100 不在途/M10 零 bump）。

## 七、文件面（交付清单）

修改 16：BuildingDef/mine.asset/KingdomConfig.cs+.asset/BuildingFactory/ProductionSystem/Building.cs/BuildingSaveData/TaskScheduler/TrainingSystem/KingdomState/TreasureVault/KingdomManager/KingdomSaveData/GameEvents/TimeManager/Valley_P1_Observer
新增 2：MineByproductComponent.cs / Valley_HH107_Smoke_Byproduct.cs（冒烟容器，菜单 Valley/验证/HH107_资源供给端补链）
临时诊断日志：全部摘除（grep「QQQ临时」零残留）。

## 八、15_账本登记回执（任务书 L66 代登义务）

已登 `ai决策大脑强化训练/15_训练侧harness与Unity端差距文档.md` **一·补四 矿洞副产语义**：sim 侧无矿洞副产语义（Unity-only 供给：水晶/火油产运消全链），非决策输入不产生即时回灌义务；AI 经济副产消费语义对齐归 W-K 批0 训练师；rate 终值 P0 调优后如需 sim 经济感知再登记。账本 HH.107→✅施工完毕待验收/HH.108→🔵已落盘/HH.114→🟡新登记（取号时水位 113 已销号）。

*提交：执行端 2026-09-08。待策划端验收 HH.107 销号+HH.114 四项裁决。*
