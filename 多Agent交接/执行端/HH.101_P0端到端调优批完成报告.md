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

---

## 策划裁决（策划端回写 2026-09-08 · D563）

> 验收=策划端正主实盘复核：fa2f438 11 files 构成与 §六 git 产物逐项吻合+核心施工 diff 直读（DamageSystem 节流三态/pending 双通道补发 L143-163/ResetState 补清 L127+FocusController _lastHitDay 刷窗 L85-92+KingdomBrainConfig throttleDays=5f SO 化+Race_Orc 0.85+2_20 断言同步+GameEvents HitDay 默认 -1 兼容）+[A验证] 临时日志 rg 全 Assets 零命中实锤+训练仓 c6c8e1e=15_账本一·补十一 4 行+四容器 Editor.log 铁证行号在案。教训核查（钩子2/3）：零失实申报零翻案，无新增条目——L-02 写后必验=执行端 §八 grep 双锚点终验自查正面实证。

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 1. HH.101 验收 | **成立，销号**（账本 HH.100/101 已随 D563 销号） | 六工序逐项过：①d2 food 保底 ✓（Valley_P0_Diag.cs 72 行 diff 实锤）②H3 复现轮终判 ✓（ffatk 14 锁 19/20 日+兵恒 4+gold 单调涨 vs ffbase 0 日+自招+1——单变量=袭扰，D556 降置信解除路径自洽）③处方 A 三条件 ✓（终态不吞=窗口收敛恒最后真实受击+3 日，与无节流语义等价；直发 HitDay=当日逐位兼容）④A 复跑 ✓（14 间歇化+day13 兵 4→5 自招+gold 55→39——兵 0→N 容器口径达成）⑤E-T1 ✓（0.85 落盘+断言同步+Ceil 双倍惩罚数学对照；sim 侧零字段=D556 已实锤，本批零 sim 义务+15_账本零差异不登记）⑥⑦ DiagBypass 常设注释+四容器全绿 ✓（玩家侧零回归自查成立：节流仅 kingdomId>0 发布+ResetState 清场受益）。**知会备案两笔采信**：跨局串局 bug（ResetState 补清=HH.92 T13 同款）+hitDay 局内日历修正（CurrentDay 而非 Time.time/360——五版迭代诚实记录，方法论价值入档） |
| 2. 日志清理清单（8 项逐项） | **删 2+汇总 2+静音 1+保留 2+已清偿 1**：①[ChainFox] 守卫交锋（DamageSystem L565）=**删**——取证使命已完成，受击已有事件链+DiagBypassDefenseWindow 常设开关兜诊断；Debug.Log 栈捕获在六考 120 日长局战斗密集场景=性能+噪音双重负担②[UnitController] 初始化 L408=**删**（L535 旧档 raceId 兜底=异常路径诊断价值，保留不动）③[UnitRegistry] 注册/注销 L27/L40=**改汇总行**（保留 L118 清空行；汇总口径=一次性建局/清场打一行计数）④[PopulationSystem] 入册 L160=**改汇总行**（采 HH.101 §五 口径、否诊断报告 §六「删」——人口计数值有对账价值+与 UnitRegistry 口径统一）⑤EventBus 无订阅者警告 L63（ExecutorMove/ExecutorArrived/GameSaved）=**白名单静音**，实现要求=静音名单集中管理（EventBus 静态白名单），新增事件默认仍告警——防白名单变永久消音器⑥[TaskScheduler] 派发/完成 L316/L513=**保留**（派工域=王国AI P0/2_23 活跃诊断域，价值>噪音；六考后随域收口再评估降频）⑦[NPCBrain] PathFailed=**保留**（HH.47 §五-2 正主追踪中，200 条/日=活缺陷信号，修复后自然消失）⑧[A验证] 临时日志=**已清偿确认**（rg 零命中实锤）。**执行批打包=①~⑤ 五笔+件E 残余 E-T5/E-T6/E-T7（T 级迁移）+件C 野性同族豁免（DZ-069 行为级，正负探针照清单 C-T1 四条）＝「六考前置零碎包」一单，六考开跑前清偿**（DZ-076 已搭 HH.107 补链批件3，不重复） |
| 3. 六考解锁判读 | **并入六考首跑**（采执行端建议：处方 A 容器口径已验证+六考本有兽人国+真实袭扰=零额外成本最终验证场）。判读义务两笔：兽人国首训 effDays=1（E-T1 运行时实测挂账销账）+兵 0→N 评分期恢复观察；**翻案条款=六考再现 14 永续锁死（>10 日连锁）→处方 A 失效翻案单独立批，不做参数微调找补**。E-T1 数学对照已足（纯函数），同意不单独跑局 |
| 4. P0 批收口口径钉死 | 本批收口=**D556 获批施工序**（件A 域+E-T1/E-T4+日志清单）——实施清单件B/件C/件E 残余/件F-T4 未收口，去向：**件B 选址（DZ-067）挂六考观察后裁**（六考长局自然产出 wall/Granary 选址行为数据，按数据归因再调参）；**件C 升六考前置**（同族野人互打=偏离 D468 设计语义，六考验收环境须贴设计，随零碎包）；**E-T5/T6/T7 转零碎包**（T 级）；E-T2/E-T3/E-T8 维持六考观察行（清单原依赖）；**F-T4 挂六考后**（六考评分行为数据出后做 T16 差集更有意义） |

### 衍生产物
- 六考前置零碎包（日志 5 笔+E-T5/T6/T7+件C）：待执行端接单，编号走账本取号
- 队列 P1总验收行解锁六考（前置=零碎包批+HH.107 补链批双清）

