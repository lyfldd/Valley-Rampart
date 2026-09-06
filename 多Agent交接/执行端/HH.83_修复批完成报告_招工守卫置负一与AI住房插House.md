# HH.83 修复批完成报告（⑥招工守卫 kingdomId=-1+AI 住房插 House——P1 四考前置）

> 类型：完成报告（修复批）
> 状态：⏳待策划端验收
> 日期：2026-09-06 · 发起端：执行端 · 任务书：策划端/HH.81_招工守卫与住房缺口修复批_任务书.md（D542）
> 冒烟：**ALL PASS 7/7**（第三轮；前两轮为探针口径修正迭代）+ json 存档补证闭环

## 一、三件施工 diff

### 件1 流浪生成链置 kingdomId=-1（语义正解）

- **三 Spawn 点全置**（[VagrantCampSystem.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/VagrantCampSystem.cs)）：
  - `SpawnVagrantNear`（L358-361，每日补员链）：`SpawnUnit(Faction.PlayerCamp, Occupation.Vagrant, spawnPos, -1)`
  - `SpawnVagrantAt`（L387-392，初始预置+自然刷点链——TryNaturalRespawn 经本方法生成，一处覆盖两链）：同置 -1
  - 注释含语义依据（D329 归属门面无主语义+营地建筑=-1 先例+HH.80 三考实锤回指）。
- **静默双坑顺手修**（[KingdomBrain.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs) L192-211，+18）：FindRecruitableVagrant 返回 null 处补诊断日志——「⑥招工无候选：流浪池活体={pool}（已入籍拒{x}/已招募拒{y}/异族拒{z}）」——P0 调优观测口；遍历为只读计数（雷区纪律 ✓）。

### 件1 影响面排查列报（逐处 grep 实证，非凭感觉）

全项目 kingdomId 比较消费点 62 处扫描（`kingdomId [><=]+ 0` 系），流浪 -1 触达面分类：

| 类 | 处 | 判定 |
|---|---|---|
| **零回归（结构性排除/双条件兜底）** | IsPopulationEntity L139（>0 拦 AI；-1 落到 L84 `Vagrant return false` 挡——与旧 0 同挡）/CountAliveByKingdom L106+L108（-1 ≠ 0 与 Vagrant 双挡，净行为同旧）/AbstractEconomySettlement FindFirstUnit L102（-1 不匹配 AI 国，同旧）/KingdomRace L52（GetKingdomRace 调用方传招募国 id>0，非流浪 id）/UnitController L92 Faction 派生（-1 不满足 >0→保持 PlayerCamp，同旧——玩家招募交互前置 ✓）/TerritoryOverlay L533（<0 跳过渐隐 ✓ 同营地建筑旧例）/Building L494+TerritorySystem L78+WaveDirector L239（建筑域 -1 旧已兼容） | **-1 行为逐位=旧 0** |
| **语义修正（旧=流浪误入玩家桶统计，新=不入——流浪本无国）** | SatietySystem L95（玩家均饱食不再被流浪稀释）/HappinessSystem L145（玩家均幸福同）/ThroneAnchor L41/L57（玩家 GameOver 存活统计不再含流浪） | 统计面变化=**正确方向修正**（任务书「预期各守卫本就排除非正数归属=零回归方向」的实证面），数值漂移如实列报 |
| **行为变化（Monster 收窄守卫副作用）** | MonsterAI L192/MonsterController L127（`!=0 continue`：怪物只袭玩家王国 0——**流浪 -1 后免野怪袭**） | 与营地建筑（-1）免袭同构=口径自洽；流浪池稳定性↑；野性生态面变化列报（D468 野性敌意**不受影响**——NPCBrain L619 判定按职业+招募标记不看 kingdomId，流浪仍照常发起野性攻击） |
| **配套修（旧=靠流浪 kingdomId=0 的错误语义「顺带正确」，新须显式补）** | **SelectionController 三处门**（L148 Follow/L241 点选/L279 框选 `==0`）→ `(kingdomId == 0 \|\| EffectiveOccupation == Vagrant)`——否则 -1 流浪选不中=**玩家招募点击交互（InteractAction recruit）不可达=玩家招募链断**（实际回归面，已修）；**RecruitVagrant 补置籍**（VagrantCampSystem L250 后 `unit.kingdomId = 0`）——玩家招募=入籍动作，与 ConvertVagrantsToWorkers `uc.kingdomId = kingdomId`（L398 AI 侧已有 ✓）同构；不补则招募 Resident 挂 -1 脱离玩家派生统计=玩家轨回归（实际回归面，已修） | AI 工人（>0）仍排除 ✓ 玩家轨逐位不动 ✓ |

**结论**：任务书点名的六处守卫（IsPopulationEntity/_entities 桶/AIEconomySettlement/CountAliveByKingdom/野性敌意扫描/招募限同族 D469）全部零回归（前四处结构性排除、野性敌意不看 kingdomId、D469 判 raceId）；**实际回归面在玩家交互链两处**（选中门+招募置籍）——已配套修齐并在冒烟 P3 验证。

### 件2 六 AI 模板插 House（快解）

- 五 AI 模板（DenseForest/GoldenWheat/Bedrock/IronHoof/SnowRock）baseBuildingDefIds **castle 后第二位插 `- House`**——目标序 `castle,House,farm,Well,mine,Warehouse,quarry`（五模板同构，grep L34 复验全在）；**RiverBay 玩家模板不动** ✓。
- 资产 id 实值先查：[House.asset](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Resources/Buildings/House.asset) L15 `id: House`（大写 H ✓，HH.73 笔误教训应用）。
- KingdomFoundingConfig buildingCount **4/5/6→5/6/7**（L36/L51/L66 帐篷/村落/要塞；帐篷新取 castle,House,farm,Well——粮水房三要素）。

### 件3 零代码登记

- AI 建造评分人口驱动 House=2_23 批B 域、野怪杀 Worker=数值观察项（挂账池）——本批零施工 ✓（勿顺手条款遵守）。

## 二、冒烟证据链（Valley_HH81_Smoke，seed=42424，45 游戏日压场@5s/日）

三轮迭代（前两轮=探针口径修正，非产品问题）：

| 轮 | 结果 | 事件 |
|---|---|---|
| 1 | 6/7 | P3 两探针设计 bug：裸 CountAliveByKingdom(0) 混入 Monster（30 个 kingdomId=0）→基线 39 误判；CanRecruit=False 玩家粮 0（玩家侧既有，三考 CSV 玩家粮恒 0） |
| 2 | 6/7 | P3 修 Monster 污染后仍 False：玩家人口「单调不丢」判据过强——玩家工被野怪杀=世界生态（三考同款），非本批回归 |
| 3 | **ALL PASS 7/7** | P3 改结构性判定（流浪残留 0+招募口不炸）——玩家桶定义 kingdomId==0 精确匹配=-1 天然不入=零污染结构性保证 |

第三轮终局：
- **P1 结构**：三 AI 国 House=1/房容=3（件2 立国链生效）=True
- **P2b 生育**：k2 AI Child raceId=2（期望2✓）=True——**房容>0 过门槛，件1+件2 联动解锁生育分支**
- **P2a 招工**：⑥招工落地日志×10+AI Worker 分布 k1=8/k3=10/k2=2=True——**-1 流浪被招募入籍实锤**
- **P2c 直生**：AI小孩长大×2=True
- **P3 玩家零回归**：流浪=150（全-1:150/残留0:0）+招募口不炸=True
- **P4 存档**：save=True（镜像 Logs/P1/hh81_p3_save.json）
- **P5 雷区**：InvalidOperationException 0=True（件1 动 Spawn 点+招工遍历的复检通过）

### P4 json 存档补证（hh81_stat_units.ps1，UnitSaveData 总数 200）

- **occ=25 kid=-1 ×150**（race 0=143/race 1=7）——流浪全量 -1 入档 ✓（件1 主实锤）
- **occ=23 kid=1 race=1 ×5+kid=3 race=3 ×4**——招募/直生入籍 AI 国且族别正确 ✓（⑥通道+件2 Child 直生双证）
- occ=23 kid=1/2/3 race=0 ×11——Foundry 预置原生（raceId=0 既有面不变）
- occ=26 Child 残留 **0**——45 日终局直生链全消化 ✓
- occ=27 kid=0 Monster ×30——生态对照

## 三、Spawn 雷区 35 处复核（硬性条目）

- 件1 动的三处 Spawn：SpawnVagrantNear/SpawnVagrantAt 方法体内 Spawn，调用点=camps 遍历（BuildingRegistry 快照）/OnNewGameMapReady 预置/TryNaturalRespawn 24 次尝试循环——**无一在 UnitRegistry 遍历体内** ✓
- 件1 新增诊断日志 foreach（KingdomBrain L199-210）：只读计数，零增删 ✓
- SelectionController/VagrantCampSystem 置籍：无遍历改动 ✓
- 存量 32 处 HH.76/78 已查未触碰 ✓；P5 运行时监听 45 日压场 0 命中佐证

## 四、红线自查

| 项 | 兑现 |
|---|---|
| Spawn 不入 foreach GetAllUnits 遍历体 | ✓（§三） |
| 玩家轨逐位不动 | ✓ 交互门放行=恢复旧可达行为（旧流浪 0 可选中）；招募置籍=补齐旧语义依赖的入籍动作；RecruitVagrant 扣粮/走回/入册链逐位不动 |
| AI.Core/sim/champion/训练仓/RulerController 零触碰 | ✓ |
| 冒烟全绿才 commit | ALL PASS 7/7 后出报告（commit 待验收代执） |
| git diff 自查+grep 双锚点 | 本批=5 模板 asset+FoundingConfig+3 代码件（KingdomBrain/Selection/VagrantCamp）+新容器；**grep 双锚点复验全执**（House×5=L34/buildingCount 5/6/7=L36/51/66/Spawn -1×2=L361/392/诊断日志=L211/Selection 三门=L151/246/286/置籍=L250 区）；域外=策划端并行活动（**建筑 API 全面审计报告×5**+HH.86 治理批任务书+美术文档+缺陷台账）不裹入 commit |

## 五、验收请求

1. 三件施工与冒烟验收（ALL PASS 7/7+json 补证）。
2. 件1 影响面排查结论认可（§一表：零回归/语义修正/行为变化/配套修四分类；**两个实际回归面已修**——选中门+招募置籍）。
3. 验收通过→commit 代执（本批 9 件+容器）→**P1 四考解锁**。

---


## 六、教训沉淀（HH.42 复发第 7 笔，验收裁决采信入册 2026-09-06）

- **现象**：_交接索引.md 登记 HH.82 行时，SearchReplace 回显 diff 成功+Read 工具视图显示行在案（L98）——pwsh `ReadAllLines` 实锤磁盘 97 行尾部无该行。**双幻读：回显与 Read 视图均≠落盘**。
- **根因**：SearchReplace 回显/Read 视图是会话内视图态，存在与磁盘不同步窗口；同族前六笔（SearchReplace 未落盘、grep 验伪补救等）。
- **证据纪律（升注册类施工/登记标准动作）**：凡写盘必须 ①pwsh 直写 ②MD5 前后对照 ③`Select-String` 磁盘复验，三步缺一不可；Read 视图与工具回显一律不可当落盘证据。
- 本笔修复实录：`Logs/P1/hh82_index_append.ps1`（AppendAllText+MD5 DF7F28E8→2C5E0F5B+复验 L99 命中）。
## 策划裁决（策划端回写，裁决前保持空白）

