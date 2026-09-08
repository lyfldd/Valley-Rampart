# HH.101 · P0 端到端调优批 完成报告

- **承接**：HH.100 开工回执+D556 裁决（0.6 §八十六/HH.100 §八）· 账本预留号 HH.101
- **执行端**：TraeCode　**日期**：2026-09-07
- **施工序对照**（D556 任务书 §三）：①d2 food 保底 ✓ ②H3 复现轮 ✓ ③处方 A 施工 ✓ ④A 落地复跑 ✓ ⑤E-T1 ✓ ⑥DiagBypass 注释 ✓ ⑦四容器回归 ✓

## 一、处方 A 施工（三条件逐项证据）

| 条件 | 落实 | 证据 |
|---|---|---|
| ①节流参数 SO 化 | `KingdomBrainConfig.kingdomAttackEventThrottleDays=5f`（游戏日；asset 显式 5+代码兜底 5f+0=禁用；**窗长>focusMinDurationDays(3) 结构性破窗口永续**——袭扰持续下 focus 以[3 日防御窗/2 日评分期]交替，D322 语义保留） | KingdomBrainConfig.cs L62-65+asset L30+运行时读回验证（A验证轮 asset=True throttleDays=5） |
| ②终态不吞 | 窗内被吞受击记 pendingHitDay→双通道补发（窗满下次受击携带真实受击日/受击流停止后 DamageSystem.Update 主动补发）→FocusController 按 **HitDay（真实受击日）** 刷窗→**窗口收敛恒=最后真实受击+3 日**；GameEvents.KingdomAttackedEvent 新增 HitDay（默认 -1 旧行为兼容） | DamageSystem L529-560（发布节流）+L143-163（Update 补发）+FocusController L63-68/L88-96；日志铁证「k4 受击命中 hitDay=5」「日结刷窗 endDay=8」 |
| ③四容器回归+玩家侧零回归 | 全绿（见 §四） | Editor.log L10574459/10581458~10599954/10608441/10612221 |

**语义边界（M6）**：评分公式/打分器结构/决策核语义零触碰；节流=发布频率治理+窗口刷新口径修正（直发事件 HitDay=当日，与原语义逐位等价）。

## 二、H3 复现轮终判+复现轮发现

- **H3 实锤（降置信解除）**：ffatk（粮健康 200++袭扰旁路注入 3/日）→**14 锁 19/20 日+兵恒 4 停招+gold 108 单调涨零消费**；ffbase（同粮同 seed 无袭扰）→14 占 0 日+兵自招+1——单变量=袭扰，五考表型完整复现。汇总行 Editor.log L1748935~37/L3323042~44。
- **副产品**：d2 food 保底标准件（ffbase/ffatk 变体，~10 行 Editor-only）。

## 三、A 落地复跑（施工序④）——**兵 0→N 通道验证**

终版 ffatk（20 日，Editor.log L9605262~10516968）：
- **14 间歇化**：day3-6 锁→day7 评分（⑩）→day8-12 锁→**day13 评分期 ⑥招工**→day14-18 锁→day19 评分→day20-21 锁——窗口不再永续。
- **兵 0→N 通道验证**：day13 评分期兵 4→**5 自招**（六考前五考口径预验证；五考兵 0 场景待六考正式复跑）。
- **gold 消费恢复**：day13 gold 55→39（⑥招工支出），与五考「gold 单调涨零消费」对照判若两局。
- stage 照常跃迁至 Military（D556 正交性预告验证 ✓）。

## 四、四容器回归（⑦）+E-T1（⑤）+⑥注释

1. **四容器全绿**：[2_20] ALL PASS（L10574459，**断言表随 E-T1 同步**：Orc 行 trainSpeedMul 1.15→0.85，Valley2_20_Smoke_Race.cs L831）/[2_20B] 六轮 ALL PASS（L10581458~10599954）/[2_20C] ALL PASS（L10608441）/[2_13_C] ALL PASS（L10612221）。
2. **E-T1**：Race_Orc.asset trainSpeedMul 1.15→0.85（一行）；**数学对照**：effDays=Max(1,Ceil(costDays×mul))，Warrior base=1→1.15 得 **2 日**（Ceil 双倍惩罚坐实值错向）/0.85 得 **1 日**（恢复快 15% 设计意图）；运行时实测挂六考兽人国首训。**触发条款已登记挂账池**（字段歧义再致错→升 T 级改名专项）。
3. **DiagBypassDefenseWindow**：注释已更新「D556 裁定常设诊断开关」（默认 false）。
4. **E-T4**：15_账本一·补十一登记完成（训练仓独立 commit c6c8e1e）；触发条款（>5 金量级/进 sim 对拍域→重审）已随登。

## 五、随批发现与修复（列报）

1. **跨局串局 bug（已修）**：DamageSystem._kingdomAtkPub 跨 ResetWorldForNext 残留→上局 pending 新局乱发（实测 KingdomId=1/2/3 HitDay=19 串入 mini 局）——ResetState 补清（HH.92 T13 同款）。
2. **考跑环境新知**：随机立国 3 AI（k1~k3）与 fixture 国互打——「无袭扰」基线并不无菌；容器事件计数已按 kidA 过滤，后续诊断容器沿用。
3. **管线注入教训**：远程=位置驱动弹打移动单位必 miss（实收 0）——诊断注入须近战即时命中路径。
4. **日志清理清单**（策划端口头问询）：8 项评估表已落诊断报告 §六（[ChainFox] 守卫交锋每击一条建议删/节流、注册注销/初始化/入册改汇总、EventBus 无订阅者静音、PathFailed 保留至正主修复）——**待裁决后下批执行**；[A验证] 临时日志本批已摘除。

## 六、git 产物

- 主仓：DamageSystem.cs（节流+补发+ResetState）/GameEvents.cs（HitDay）/KingdomBrainConfig.cs+asset/FocusController.cs（hitDay 刷窗+DiagBypass 注释）/Race_Orc.asset（0.85）/Valley2_20_Smoke_Race.cs（断言同步）/Valley_P0_Diag.cs（d2+ff 变体）/报告跑局卷终版/HH.100 §七补注/账本队列
- 训练仓：c6c8e1e（15_账本一·补十一 E-T4 登记）

---

> 完成报告完 · 全部施工序 ✓ · 玩家侧零回归红线达成 · 验收入口=策划端（账本销号 HH.101+队列 P0 行收口；六考解锁判读=六考口径重跑兽人/兵 0 场景）
