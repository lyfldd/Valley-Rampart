# HH.131 任务书：六考后零碎包（磐石行为级补测+观察器白名单补条）

> 签发：策划端 2026-09-09（D590 四单签发；账本占号 HH.131，完成报告=HH.134 同笔预留）
> **⚠️ 2026-09-10 并入 HH.159 件8（D612 范围扩，用户拍板「最多并入」）**——本任务书转从属，执行以 HH.159 任务书为汇总载体；本文件规格继续有效（逐件对照核进度）。
> 执行端：TraeCode·Unity 轨 ｜ 性质：微批合单（**纯工装，零产品代码改动**） ｜ 排期：0.5 天
> 本批相关教训引用（钩子1）：L-02（写后必验）/L-05（白名单核对）/L-06（生产链直建禁）/L-13（Play 态双通道验证）/L-16（协程捕获器）/L-17（容器入口正门）/L-19（冒烟摆位三连）

---

## 一、批次定位

清偿挂账池两行（六考收口后统筹首批，D590 签发）：
1. **磐石 20→10 行为级补测**（D579①——HH.115 件3 已做资产断言+公式复算基准，行为级因 Bedrock prefab 缺失未跑；本批=T6 行为级升级版）。
2. **观察器白名单补条**（D589 列报4 采纳——六考实证野怪杀 Worker 两段不可统计[镜像白名单无 combat tag]+GameOver「河谷失守」行不在镜像[Editor.log 兜底]；镜像白名单同缺，一并补）。

---

## 二、施工详单

### 件1 磐石 20→10 行为级补测（替身直调法）

- 新容器 `Valley_HH131_BedrockProbe`（Assets/Editor/Smoke/）：**正门 EnterTestRun 进局**（L-17：动单位生命周期）+协程捕获器（L-16）+Finish `ExitTestRun` 全量恢复。
- 替身法步骤：
  1. spawn 一个在场单位（**生产 Prefab 实例化**，禁裸 AddComponent——2_13 P4 物理查询教训）；
  2. Play 态**内存**载入磐石三值（NpcProfessionDef=磐石卫士 Occ31 引用 + rangedDamageReduce=0.45 + armorK 按 DamageConfig 实时值）；
  3. 直调 `DamagePipeline.ApplyDamage(isRanged=true)`。
- **断言=实收值对照 HH.115 容器 T6 公式复算基准 ±1**（远程伤 20 经 45% 减伤链+armor 链；不硬编码终值，以 DamageConfig 实时链计算为准——防策划端口算错链）。
- 探针：
  - **P1** 三值载入读回断言（内存态=磐石值）；
  - **P2** 直调 ApplyDamage(20, isRanged=true) → 实收=T6 公式基准±1；
  - **P3** 对照同单位 isRanged=false → 实收=近战链基准（45% 减伤不触发）；
  - **P4** 非磐石对照单位同伤 isRanged=true → 无 45% 减伤（证明减伤=磐石特有，非全局）。
- **禁写回 SO 资产**（三值=内存实例态；测试后单位销毁，资产零污染）。

### 件2 观察器白名单补条（combat 死亡 tag+GameOver tag）

- 目标：combat 死亡事件+GameOver 判负事件进 P1 观察器镜像白名单（野怪杀 Worker 可统计+GameOver 行入镜像）。
- 步骤：
  1. **侦察**：grep 死亡路径（UnitController.Die/UnitRegistry 注销/伤害致死）与 GameOver 触发路径（ThroneAnchor 判负链/GameOverPanel）现有日志 tag 与文案，列报；
  2. tag 语义清晰可直接补条并随报告列报清单；语义含糊（多候选 tag）则列报待策划确认再补；
  3. 补条位置=`Valley_P1_Observer` 白名单数组+镜像过滤器（同文件，名单集中管理=D563 口径）；
  4. **验证**：微容器触发一次单位死亡→镜像文件含该行；GameOver 行验证可用 Editor.log 历史行佐证或触发手段列报。

---

### 件3 Smoke_9 #19 断言分型（D592 列报1 裁 a 承接，2026-09-09 追加）

- 症状：Smoke_9 #19（HH.25 期断言「f19.Update 后 LastTop≠None」）vs HH.86 份额式纠偏后产品形态（FocusController popAlarm 占位轮 `recruitedTurn→SetFocus(⑥); return;` 不跑评分不设 LastTop）——**断言漂移非产品缺陷**（D592 归因三重证据：#3 评分器本体健康/A4 新 case 零交集/FocusController 直读复现）。
- 修法（裁 a=断言分型）：popAlarm 占位轮相位（recruitedTurn）断「焦点=FocusRecruitWorker」；让位轮断「LastTop≠None」；非 popAlarm 态维持原断言。
- 验收：Smoke_9 全 OK+分型断言仍具判别力（人为构造让位轮场景验证非平凡通过）+**零产品代码**（只改 Smoke_9 容器断言）。
## 三、排雷

| # | 条目 |
|---|---|
| R1 | **零产品代码改动**（磐石数值 HH.115 已落值不再动；本包=容器+观察器纯工装） |
| R2 | 容器纪律=L-17 正门 EnterTestRun/ExitTestRun+L-16 协程捕获器+L-19 摆位（SnapWorld 吸附粒度/微格单占同点互顶/位置邻近探针强控回锚）+L-06 禁生产链直建 |
| R3 | 白名单=**只加不删**+集中管理（D563 口径）；新增 tag 语义随报告列报 |
| R4 | L-02 写后必验：SearchReplace 回显≠落盘，pwsh/PowerShell 直读磁盘验证 |
| R5 | 验收断言只认明细+判定行，汇总行仅索引（L-11） |

---

## 四、验收口径与完成报告

- 件1：P1~P4 ALL PASS（行为级，替身法成立=不依赖 Bedrock prefab 的判据兑现）。
- 件2：死亡 tag 行+GameOver tag 行入镜像（或验证方式列报采信）+补条清单列报。
- 挂账池两行（磐石微批/白名单补条）销行。
- **完成报告=HH.134**：探针明细+补条清单+教训核查行+git 面（预期=容器 1 新+Observer 1 改+Smoke_9 断言分型 1 改+meta，零产品代码）。
