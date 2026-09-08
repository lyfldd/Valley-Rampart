# HH.109 任务书：存档域小批（DZ-074 Chest 入档 + DZ-075 训练队列入档/清场）

> 签发：策划端 2026-09-08 ｜ 立项：D564（0.6 §九十四，预签态——**执行端链在 HH.107 收口后接单**）｜ 账本：HH.109 🟡 / HH.110 🔵预留（完成报告）
> 接收方：执行端（TraeCode）｜ 排期：**0.5 天**
> 上游：HH.106 生命周期审计片E（D559）→ D561 分流 → D564 预签；缺陷=2_11 步骤8 欠账显形（存读档循环资源/进度蒸发）

---

## 〇、缺陷本体（审计实锤，勿重排查）

- **DZ-074🟡**：Chest 全链不入档——ChestEntity.cs L11（`MonoBehaviour, IInteractable`，注释自认「存档归 2_11」未实现）×ChestManager.cs L8（注释「本步不留 ISaveable」）。影响：溢出装箱（D223）/兽人战利品箱（TrySpawnOrcLoot）/地图宝箱在存读档循环**全部蒸发**。
- **DZ-075🟡**：训练队列不入档+清场泄漏——TrainingSystem 非 ISaveable（_queues L19 运行时态）；Building.cs 无 OnDestroy 钩子（Die/EnterRuined 才调 OnBuildingDestroyed）×WorldLifecycle 编排清单无 TrainingSystem。影响：①存读档循环在训/排队条目静默蒸发（不退款不完成不回退）②拆/毁在训建筑→_queues 死键+死 unit 引用永久滞留。

---

## 一、施工详单（件1~3）

### 件1 DZ-074 Chest 全链入档

1. ChestManager 挂 `ISaveable`（SaveId=`"ChestManager"`；SaveLoadPhase 选型执行端核对新妈语义后定——Chest 实体随场景/地图，参照既有 Scene/Global 分拣先例，**列报选择**）。
2. 序列化字段（最小集）：位置（cell 或世界坐标）/内容物（ResourceType+数量列表）/开启交互态（若有）。
3. 读档重建：LoadPhase 时 ChestManager 重建全部 ChestEntity（覆盖三类来源：溢出装箱 D223/兽人战利品箱 TrySpawnOrcLoot/地图宝箱）。
4. **冲突核对（列报）**：地图生成自带宝箱 vs 档内宝箱——读档链若重跑地图生成会双份；核对 MapGenerated 宝箱逻辑，先清后建或幂等去重，按现有读档时序选最小方案。
5. 新增 ISaveable=新增 payload 类型，**零 schema bump**（列报确认；禁动既有 SaveId/版本号）。

### 件2 DZ-075 训练队列入档+清场

1. TrainingSystem 挂 `ISaveable`：_queues（建筑→队列条目：目标职业+进度+数量）入档；读档恢复在训进度与排队条目。**裁定口径=入档**（D564：优于「明示丢失+退款」——玩家体验优先）。
2. **读档时序核对（列报）**：队列条目引用建筑——LoadPhase 顺序必须建筑先重建、队列后恢复；核对该建筑 LoadPhase 枚举顺序，不满足则列报调整方案。
3. **清场泄漏修**：拆/毁在训建筑路径补队列清理——执行端按最小侵入选一：Building 补 OnDestroy 钩子 / WorldLifecycle ResetState 编排补 TrainingSystem 清理（仿 HH.93 T13 先例）；列报选择。
4. 进度单位=游戏日（对齐 TrainingSystem costDays 语义），跨读档连续。

### 件3 冒烟探针（行为级，全绿=验收主判据）

- **P1** Chest 入档：容器放箱+装入资源→存→读→断言箱在+内容物逐项一致。
- **P2** 队列入档：建筑排训（含在训进度）→存→读→断言队列恢复+进度一致+续训可完成。
- **P3** 清场无泄漏：拆/毁在训建筑→再存再读→断言 _queues 无死键（或 ResetState 后清空）。
- **P4** 四容器回归（2_20/2_20B/2_20C/2_13_C）**玩家零回归硬红线**；编译 0 警 0 错。

## 二、排雷（M1~M5）

| # | 雷 | 处置 |
|---|---|---|
| M1 | schema 稳定 | 新 payload 零 bump；禁动既有 SaveId/LoadPhase 语义；field 尾插 |
| M2 | 读档时序 | 建筑先重建→队列后恢复；MapGenerated 宝箱幂等 |
| M3 | HH.42 落盘幻觉 | 每笔编辑 grep 双锚点+git diff 自查=交付前置 |
| M4 | 与 HH.107 文件面 | HH.107 触 TaskScheduler/TimeManager/GameEvents/BuildingFactory——本批触 ChestEntity/ChestManager/TrainingSystem/Building（列报处）——**接单前 git status 划界**，Building.cs 若 HH.107 亦改（列报处）协调串行 |
| M5 | 冒烟口径 | 自含容器禁挂 SmokeApi.EnterGame（HH.64 口径）； chests/训练直建用工厂先例 |

## 三、验收口径（策划端将验）

- P1~P4 全绿证据（存读档前后状态对照）/schema 零 bump 确认/清场方案列报成立/四容器玩家零回归/DZ-074/075 台账转✅验收句
- 完成报告=**HH.110**（教训核查行+git diff 自查）

---

*签发：策划端 2026-09-08（预签态，HH.107 收口后执行端接单）。账本占号 HH.109/110 后落盘。*
