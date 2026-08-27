# HH.27 P0 完整局验收批执行报告（确定性状态机验收 · 终判版 · 待策划终裁 P0 收官）

> 类型：P0 收官验收批执行报告（确定性小卡+重跑后终判）
> 状态：🔴A3 仍漂（确定性小卡后未转绿），其余 B1/B4/B5/B3 按让渡口径收口，待策划终裁 P0 收官
> 日期：2026-08-27 · 发起端：执行端 · 关联：HH.26 / HH.25 / HH.24 / 2_17_AI王国脑与自主成长.md
> 前置：HH.26 裁决全准 → HH.27 策划裁定①抽象结算升格/②定向深挖(残留先于产品)/③B1补验 → 三问终裁 → 执行序落地

---

## 〇、一句话

**HH.27 执行序全部落地：回写二轮版 → push → 确定性小卡（全库 UnityEngine.Random 清点 + TrainingSystem L305 改 npcId 稳定选人 + A3 wood 二分探针 + B1 spawn 流浪汉补验，另修 VagrantCampSystem 每日补员未播种 System.Random）→ 重跑套件全终判。终判：A4/B2/RD2-①/② 全绿；A3 确定性逐字节仍 FAIL 且首差日=行0（day1 即分叉，跨轮系统性偏移，非随机浮动）→ 按"仍漂=回我深挖"上报，收敛到"脑/注册表级跨轮残留"（RD2-①只清 Unit/Building 未覆盖 KingdomBrainRegistry）待策划定向；B1 通道真 pTrue、AI 招工 k1t0 半链归人工 Play；B4/B5/B3 按让渡/跟随小卡口径收口。P0 收官时点=终轮 A3 绿 or 带人工 Play 黄旗，报策划终裁。**

---

## 一、HH.27 执行序逐项落地（终判实测）

| 执行序 | 落地 + 实测 |
|------|------|
| 回写 HH.27 二轮版 | 二轮甄定版（转绿四项/两真问题/§四小卡/§五三问）写盘+commit d346c7b |
| push 三笔/两笔闭合提交 | 6c33d36/763c8fa0/d346c7bc 上 origin/main |
| 确定性小卡 | 全库 UnityEngine.Random 清点（22 消费面）+ TrainingSystem.cs L305 改 npcId 稳定选人（实锤修）+ A3 wood 二分探针落码 + B1 Programmatic spawn 流浪汉补验通道；另修 VagrantCampSystem 每日补员未播种 `_runRng` → 世界种子^当日派生（R4，ff2415b） |
| 重跑套件全终判 | A3/B1/B4/B5/B3 全评，见 §二 |
| push 收束 | c2621900..ff2415bc 上 origin/main |

---

## 二、确定性小卡 + 重跑后终判（区别于二轮版）

```
RD2-①轮间清点=OK(b=2684/2684/2684 u=22/22/22)      ← 残留修复生效
A4玩家零回归=OK
B2供水抽象产出=OK                                     ← 裁决1 升格绿
RD2-②存读v2门控=OK(loadVer=2 走B全权重建)             ← A/B双份排除
----
A3确定性逐字节=FAIL    ← 轴修复+确定性小卡后仍 FAIL；首差日=行0
B1正向招募并行=FAIL(pTrue/True k1t0/0 k2t0/0)  ← 通道真通，AI 招工仍 0（半链待人工）
B3+C6存读回环=FAIL     ← 跟随小卡，roundtrip=False 未变（读档恢复语义待扫）
B4剧本三段封顶=FAIL    ← R1-+无军事 R2-+无军事（都停 D，成长卡招工）
B5派遣双证分列=FAIL    ← K1 build46 train0
```

**时间线**：R1=`45×D`；R2=`第1天S + 44×D`（不再到 Expand）。残留清除后两轮成长行为收敛为"同都停 D"。

**A3 首差日二进制定位（策划裁定探针）**：
```
A3wood二分: 末一致日=-1(无一致日)  首差日=行0(day1 即分叉)
R1@行0 [day1: k11,1,0;k21,1,0;k31,1,0;  K1 g32/f64/w52/s49  wk6 wa0 b4  t0B3]
R2@行0 [day1: k10,1,0;k20,1,0;k30,1,0;  K1 g32/f64/w60/s49  wk6 wa0 b4  t0B1]
```
- 分歧**唯一**表现为：R1 首日三 AI 王国 phase=1(Develop)/build3/wood52，R2 首日 phase=0(Survive)/build1/wood60。
- food/gold/stone、wk/wa/b 全一致（32/64/49、wk6/wa0/b4）。wood 差 8 = **(build3−build1)×4 wood/座** → wood 是建造消费的**下游**，真分歧点=**首日 AI 建造与剧本阶段演进**。

---

## 三、A3 深挖结论（确定性小卡后仍漂 → 回执策划，依据"仍漂=回我深挖"）

**已坐实/排除**：
- wood 二分定位完成：首差日=day1，当天差异为"三王国 phase 统一偏移 + 建造数 3 vs 1"。
- **低概率排除随机源漂移**：确定性小卡修了 TrainingSystem L305（prof永未走到）+ NewDayRng 播种后，A3 数值**逐字节不变**（R1 恒 52/3，R2 恒 60/1）——若是"未播种随机逐轮浮动"，两轮值不应每次恒复现同数。故**非 Random 随机逐轮漂移**；
- **真面目为"跨轮系统性偏移"**：同 seed 两轮（R1/R2 皆 RT=False）首日即确定性错位，且跨不同 Play 会话复现同值 → 更贴近**跨轮残留（解释2）的隐蔽层**，而非产品随机（解释3）。
- **收敛候选**：RD2-① 只清了 UnitRegistry/BuildingRegistry，**未清查 KingdomBrainRegistry**。若脑实例/Scope 跨轮未重建，StageMachine.Stage 与剧本推进输入（如活跃建筑口径）泄漏到下一轮 → 首日 phase/build 系统性偏。**这超出 RD2-① 覆盖面**，为本次深挖新界线。

**待策划定向**（不代裁，交回）：
1. 是否批准追踪 KingdomBrainRegistry 跨轮生命周期（脑清册/重建），把"首日跨轮偏移"这条线彻底闭合？
2. 若确证脑级残留 → 属 harness 残留（修套件清脑册）还是产品生命周期（BrainRegistry 缺清册钩子）？前者改套件重跑即可绿，后者入产品小卡。
3. 若不追此线，接受"终轮 A3 以『带人工 Play 跨轮残留黄旗』收官"？

**B1/B4/B5 让渡口径（策划已裁"准"，此处收口）**：
- B1 通道真通：`pTrue/True`（Programmatic spawn 流浪汉 → RecruitVagrant=True 转居民）。
- AI 招工仍 `k1t0/k2t0`（`FindRecruitableVagrant` 在 pump 内找不到可招流浪汉）→ **招工→成长半链归人工 Play**。B4 卡此 → 均停 D；B5 K1 train0 同因。三项一并归人工 Play 清单。

---

## 四、登记：确定性小卡（落地项 + 遗留）

**已修**：
- `TrainingSystem.cs L305`：UnityEngine.Random→npcId 稳定最小选人（玩家确定性命门，c262190）。
- `VagrantCampSystem` 每日补员 `_runRng`（未播种 System.Random）→ `new System.Random(seed ^ day*7919)`（R4，ff2415b）。**真 R4 Bug**，无论 A3 是否因它，均该修。

**全库 UnityEngine.Random 消费面清点（22 处，归后续小卡/独立扫）**：
- 决策/日链外表现：WanderAnchorPool(NPC 锚点)、NPCBrain(对话延迟/冲锋)、Stimulus(锚点刷新/采集轮盘)、PopulationSystem(生育/择偶)、Combat 投射物/击退、UnitController 对话台词、BehaviorExecutor 游荡位移、AIDebugSpawn、CharacterCreationPanel 种子回退、WorldManager seed=0 回退。
- 原则（Ramsey/R4）：凡进"决策/接受面"必须种子化；纯表现/玩法随机不阻塞 A3。

---

## 五、请策划终裁 P0 收官

1. **A3**：确定性小卡 + 重跑后**仍 FAIL 首日即分叉**（跨轮系统性偏移、随机播种无效）。依据"仍漂=回我深挖"，三问已定位到"脑/注册表级跨轮残留"（RD2-①覆盖面不足）——**请策划定向上报，或接受终轮带此黄旗收官**。
2. **B1/B4/B5**：通道真通（pTrue）、AI 招工→成长半链归人工 Play（三项正式接单）——**终轮带人工 Play 黄旗**。
3. **B3**：跟随确定性小卡，roundtrip=False 存读恢复语义待扫——**本轮不单独挖，随 A3 残留线一并看**。

---

## 六、交接状态

- **push 已完成**：c2621900..ff2415bc 上 origin/main（含 dd9a8 轴修复、ff2415b VagrantCamp 播种）。
- **人工 Play 三项（策划正式接单，非本批执行）**：①细模拟经济闭环（工人真走真产）②招工→成长链 ③GameOver 路径（灾变三禁未验）。
- **④债**：维持不变（归步骤12/领土入档先到者，触发必落 foundKingdoms 门控+三处追记回写）。

---

## 附：三版形实对照（供策划核验）

```
首轮(残留u): u=18/36/54   R1=45D       R2=SDD...E...   A3 wood 漂移大
二轮(清残留): u=22/22/22   R1=45D       R2=SDDDD...     A3 wood 52 vs 60（唯一分歧）
终判(小卡后): u=22/22/22   R1=45D       R2=SDDDD...     A3 仍漂(首日即分叉 build3/1 phase全1/0)
深挖:         随机播种无效(值恒复现)→非随机浮动→收敛"脑/注册表跨轮残留"待策划定向
```