# HH.156 F30 训练串交付报告：2_4 卡池流程重生成 + 新池 baseline + r2-A 复测 + 受控重注册兑现

> **执行端·训练轨** ｜ 2026-09-10 ｜ 依据：HH.147 §六·裁决（D601）下串任务书
> 工作仓：`ai决策大脑强化训练`（训练仓）；本信落主仓 `多Agent交接/执行端/`。

---

## §一 执行摘要（任务书 T1~T7 全兑现）

| 任务 | 结果 | 证据锚点 |
|---|---|---|
| T1 2_4 卡池流程（D601 裁①A） | ✅ 兑现——新考题=1×Orc_Ram 贴脸（x=7.9），rule② 劫持触发面消除+可判定（r2-A 0.709 vs baseline 0.139） | `harness/Cards/F30/scenarios/KF30_2_4.json`、D-001 §11 |
| T2 新池 baseline 重建 | ✅ 兑现——v9 全卷 @100 Δ=+0.006（无退化）/KF30 池 @100 新锚点 mean=0.664 | `results/f30_baseline_hh152_2_4new/` |
| T3 r2-A 差异面复测 | ✅ **评分门 PASS**——Δ=0.000 ≤ ±0.003（对新池 baseline）、holdout +0.001、无新梯度 | `results/f30_r2A_hh152_2_4new/` |
| T4 受控重注册+15_账本 | ✅ 兑现——iw* 五键终值入 champion、行为零变化门 PASS（六场景逐位全等）、缺字段警告消失 | `champion/tuning.champion.json`、15_账本一·补二十二 |
| T5 KEYS §四 阶跃注记 | ✅ 兑现 | `cards/F30/KEYS_键族设计稿.md` §四 |
| T6 .gitignore 收编 | ✅ 兑现——14800 untracked 清零 | `.gitignore` L28-30 |
| T7 取号+交付 | ✅ 本信（HH.156；HH.152~155 已被并行会话占用，先登记后落盘） | `多Agent交接/_编号登记.md` |

---

## §二 T1：2_4 考题重生成（新旧差异+设计意图口径）

**实验收敛谱系**（全部 @30 局留痕，含废案）：

| 变体 | r2-A hit | 判定 |
|---|---|---|
| 旧 2_4（双 wolf：贴脸+远置 x=-34） | 0.450 | rule② 劫持伪读数（D601 裁① B 否决依据） |
| 双 wolf 同侧 x=6.5 | 0.286 | wolf 残血逃逸（maxHitCount=3）→Sally 断裂 |
| 3×Bedrock 贴脸 | 0.067 | Bedrock 攻 10 反杀将军（t=14.8 阵亡实证）——废 |
| 3×Ram 贴脸 x=6.8 | 0.227 | 将军 Sally 出击冲出中区块热区（多敌贴脸威胁位移）——废 |
| 1×Ram x=6.8/7.5/7.9（三位置） | 0.710/0.717/0.708 | **收敛**（位置不敏感） |
| **新 2_4=1×Orc_Ram x=7.9 y=0** | **0.709 @100** | 定稿 |

**新旧 2_4 差异**（详表见 D-001 §11）：敌构成（双 wolf 双波 → 1 Ram 单波）、敌速/逃逸（wolf 3.6+逃逸 → Ram 1.8+maxHitCount=99 不逃）、劫持面（远置空窗+热点残留 → 无空窗，歼敌 8.5s 快于 heat 衰减）。

**设计意图口径**：守城编队（isGarrison+满墙）在敌贴脸压城时持续出城迎战（Sally）歼敌至战斗结束。选 Orc_Ram 三因：①unitDamageMul=0（对守军 0 伤，30 局 totalDamageTaken=0 实证）；②maxHitCount=99/courage=99 不逃逸；③慢速贴脸 enemyDist 全程 ≤20。

**可判定性验证**（区分度）：baseline（iw 全 1.0）hit=**0.101**（同分裁决序 Charge 压制 Sally）vs r2-A（iwSally=1.5）hit=**0.709**——Δ=0.607，考题可判定。

**0.709 vs 理论 ~0.9 残差归因（如实列报）**：**引擎几何上限**——将军 Sally 出击恒速 2.5/s 推进，~6s 冲出中区块 0（x∈[-6.78,9.04)，C# 整数除法向零取整边界）→ 本地 heat 衰减归零 → Sally 破 → 尾段短劫持（sw=2）。三位置实验读数稳定 ~0.71=考题可达上限。**归因指向引擎聚合几何（heat 中区块聚合+advance 移动），非加权核结构面**——不构成 T3 条款「新结构面」升级条件，不擅改，留策划端判读（如需抬升至 ~0.9 需动 heat 聚合粒度或 Sally 移动语义=结构域）。

**结构零触碰确认**：rule②/③.5 域分离=C 议题（2_21 阶段B 立档），本串零触碰加权核；实验诊断代码已 revert（SimFormation.cs 无残留）。

## §三 T2：新池 baseline 重建

1. **KF30 池 baseline @100**（patch=useIntentTable=false+iw* 全 1.0=F30 训练态口径）：2_1 **0.519** / 2_2 **0.738** / 2_3 **0.927** / **2_4 0.139（新锚点）** / 2_5 **1.000** / 2_6 豁免——5 场景 mean=**0.664**。报告独立落盘：`results/f30_baseline_hh152_2_4new/kf30_pool_baseline_report.json`（H-06 口径，防 probe-f30 单文件覆盖位）。
2. **v9 全卷 @100+holdout**（champion 无 patch 重测）：total **0.417**（vs 既有 F30 baseline 0.417，Δ=+0.006 同配置两次跑批噪声带内）、holdout **0.498**（Δ=0.000）、regression=0、verdict=candidate。报告 `results/f30_baseline_hh152_2_4new/{report,holdout_report,verdict}.json`。
3. 变更面=场景数据+baseline+文档（**零 .cs delta**，诊断代码已 revert，build 0 错 0 警）。
4. 新锚点已入 README（训练态口径行）+D-001 §11。

## §四 T3：r2-A 差异面复测（判读=评分门 PASS）

- **判据**：评分面无退化门 Δ≤±0.003（对**新池 baseline**）。
- **实读**：r2-A @100 全卷 total **0.417** vs 新池 baseline 0.417 → **Δ=0.000** ✅；holdout 0.499 vs 0.498 → **Δ=+0.001** ✅；regression=0 ✅。
- **KF30 六场景 subScores 逐位一致**（base=r2A：2_1 0.145/2_2 0.454/2_3 0.579/2_4 0.543/2_5 0.562/2_6 0.386）=战斗评分面无新梯度。
- **意图面（卡指标）**：新 2_4 @100 r2-A hit=**0.709** vs baseline 0.139（D601 裁①B 否决依据的 0.45 伪读数在新考题下不复现——劫持窗口消除后 r2-A 真实 Sally 能力显现）。
- **元2 最小代价口径**：2_1/2_2/2_3/2_5 独立读数**沿用 round-2 @30 复测**（r2-A：0.977/0.982/0.956/1.000，HH.147 在案）不重跑；本轮 KF30 池 @100 subScores 逐位一致为其行为面零变化旁证。
- **无新梯度/新胜者** → 不触发重搜；**r2-A 维持 round-2 收敛候选**，重注册对象=r2-A 口径。

## §五 T4：受控重注册兑现（T3 复验通过后执行）

1. **落点**：`champion/tuning.champion.json` tuning 段新增 iw* 五键终值：`iwRetreat=1.0 / iwSupport=1.0 / iwCharge=0.5 / iwSally=1.5 / iwDefense=0.0`（r2-A 口径）。
2. **useIntentTable 维持 true 不动**（全局切换挂账维持，与 2_21 阶段B 合并裁决，不自行切）——true 路径（查表 DecideIntent）不消费 iw*，重注册=字段完整性回灌非行为切换。
3. **行为零变化门 PASS**：git HEAD 旧 champion vs 重注册后 champion，KF30 池 @30 对跑**六场景逐位全等**（hit/sw/dur/win 全同；报告 `results/f30_baseline_hh152_2_4new/{pre,post}_reregister_kf30_30.json`）。
4. **缺字段警告消失**：重注册前每次加载警告 5 个 → 重注册后实盘日志零警告。
5. **15_账本**：一·补二十二 登记（重注册兑现+行为零变化门+消费语义注记：champion 全局态 iw*=true 路径死字段，阶段B IntentBiasConfig/全局切换裁决时激活）。一·补二十①「champion 回灌义务」清偿销号。
6. D-001 §9 补记兑现注记。

## §六 T5/T6：轻量注记兑现（D601 验收处方）

- **T5 KEYS §四**：HH.147/D601 轻量注记已插——iwSupport 消费语义=**行为面阶跃非连续权重**（0/1 两点实证+理论缝隙 0<iw≤0.2 不 flip 结论不动摇+搜参域内等效二元「0=关/>0=开」+搜参除名维持）。
- **T6 .gitignore**：新增 `runs/**/gate_*/*.jsonl`(+.tmp) 规则——git untracked **14800→0**（实盘 `git status --porcelain` 复核），git 卫生恢复。
- **T1c 申报口径**（习惯级不落档，D601 验收三问处方）：本信所有 grep 对账均注明「合法账目提及除外」语境。

## §七 待决策与列报

1. **新 2_4 hit 0.709 vs 理论 ~0.9**：残差=引擎几何上限（将军出击 6s 冲出 heat 聚合区块），非权重面非加权核结构面。若策划端要求 ~0.9 量级读数，需动 heat 聚合粒度/Sally 移动语义=结构域议题（归 2_21 阶段B 议程一并裁决，或单独立档候裁）。训练侧判读：0.709 已远超 baseline 0.101，考题区分度充分，**建议接受现考题**。
2. **useIntentTable 全局切换**：维持挂账（champion true 现值不动），与 2_21 阶段B IntentBiasConfig 合并裁决。
3. **r2-A 收敛判读**：round-2 收敛候选经新池复测维持，F30 卡内搜参收敛态达成（待本信验收后 F30 主线收口，进下一卡或 T06/T01 队列）。

## §八 红线自检

- 评分公式/键族结构/加权核语义 **零触碰**（唯一 .cs 改动=诊断代码已 revert，build 0 错 0 警实证）。
- champion 单源：仅按 D601② 既定受控口径加 iw* 五键字段，useIntentTable/其余字段零改动。
- 并行会话零触碰：王国AI 批E（HH.148/149）+测试基建微批（HH.150/151）+美术端（HH.155）文件未动；HH.152~155 号已占用故顺延取号 HH.156（先登记后落盘）。
- 跨仓验证全程实盘命令输出（L-02）；病9 同文件多处编辑均串行+替换后 grep 对账（本次 D-001 曾发生一次脚本 AddRange 异常致文件截断，**已从 git HEAD 完整恢复后重做**，恢复后 125 行→150 行含 §11，教训=「同文件全量重写脚本禁用 AddRange 模式、一律 AppendAllText/单点 Replace」）。

## §九 产物清单

| 类型 | 路径 |
|---|---|
| 场景 | `harness/Cards/F30/scenarios/KF30_2_4.json`（新考题） |
| baseline 报告 | `results/f30_baseline_hh152_2_4new/`（v9 全卷+holdout+verdict+KF30 池 @100+重注册对照 4 件） |
| r2-A 复测报告 | `results/f30_r2A_hh152_2_4new/`（v9 全卷+holdout+verdict） |
| 台账 | `cards/F30/decisions/D-001_键族施工与响应面测绘.md` §9 注记+§11 |
| 卡文档 | `cards/F30/README.md`（状态/口径/待办刷新）、`cards/F30/KEYS_键族设计稿.md` §四 注记 |
| 账本 | `15_训练侧harness与Unity端差距文档.md` 一·补二十二 |
| 卫生 | `.gitignore` gate 规则（14800 清零） |
| champion | `champion/tuning.champion.json`（iw* 五键终值入位） |
