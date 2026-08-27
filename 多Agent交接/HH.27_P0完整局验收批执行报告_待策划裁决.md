# HH.27 P0 完整局验收批执行报告（确定性状态机验收 · 二轮甄定版 · 待策划终裁）

> 类型：P0 收官验收批执行报告（二轮甄定）
> 状态：二轮甄定完成，遗留"产品 AI wood 漂移"待策划终裁 P0 收官
> 日期：2026-08-27 · 发起端：执行端 · 关联：HH.26 / HH.25 / HH.24 / 2_17_AI王国脑与自主成长.md
> 前置：HH.26 裁决全准 → HH.27 策划裁定①抽象结算升格/②定向深挖(残留先于产品)/③B1补验，批准跑二轮

---

## 〇、一句话

**HH.27 策划①②③ 全部落地：pump 内置 D281 抽象结算（B2 供水抽象产出=OK）→ 轮间清点断言暴露并修复 UnitRegistry 残留（u 从 18/36/54 归一到 22/22/22）→ 存读 v2 门控 OK（loadVer=2 走 B 全权重建）→ 残留解释排除。二轮甄定后仍残留的「A3 wood 漂移(52 vs 60)+R2早停+train0+流浪汉池空」收束为两个真问题：B1/B5/B4 卡"流浪汉池空"(pump 无地图 spawn,产品-环境断层) + 产品 AI 非种子随机(wood 建造决策漂移)。待策划终裁。**

---

## 一、HH.27 三条裁定的落地对照（二轮实测）

| 裁定 | 要求 | 落地 + 二轮实测 |
|------|------|------|
| ① 抽象结算升格 | pump 内置 D281 抽象结算（SimEconomy 同构：人口×生产率→入账），A1/A2/B4/B5 不降级 N/A；效力脚注收入侧=harness 实现归步骤14 | ApplyAbstractSettlement()：每日对每个 AI 王国 AddResources(worker×4 粮/木/石/金 + 建筑×2 税)（预演值）。B2 供水抽象产出=OK |
| ① 生效声明 | 报告标注"收入侧为 harness 抽象结算，产品侧归步骤14" | 文件头效力脚注 + 本报告 §一目 |
| ② 深挖优先级 | 解释2(残留)先于解释3(产品)；清点断言 + v2 门控；仍分歧才转产品 | 轮间清点断言：Unit/BuildingRegistry.Clear 补入 → RD2-①=OK(u=22 一致)；v2 门控 RD2-②=OK(loadVer=2)。残留排除。仍分歧(wood)→转产品深挖（§三） |
| ② 实锤登记 | TrainingSystem.cs L305 Random.Range 未种子化 → 独立确定性小卡 | 实锤确认 TrainingSystem.cs:305 Random.Range 未种子化。登记独立小卡（§四），不阻塞本批 |
| ③ B1 招募补验 | 注入玩家资源 + 确认真实流浪汉在场 + 打 pFalse 根因 | DoPlayerRecruit 注入粮到足 + 流民预置(OnNewGameMapReady) + 分层日志。破根因=流浪汉池空（粮够=True 通道层其余全满足） |

---

## 二、二轮实测判据结果（区别于首轮）

```
RD2-①轮间清点=OK(b=2684一致/u=22/22/22)   残留修复生效(首轮 u=18/36/54)
A4玩家零回归=OK
B2供水抽象产出=OK                          裁决1 升格后绿
RD2-②存读v2门控=OK(loadVer=2 走B全权重建)  A/B双份排除
----
A3确定性逐字节=FAIL   分歧缩小为仅 wood(52 vs 60)
B1正向招募并行=FAIL   根因=流浪汉池空(粮够/通道其余全满足)
B3+C6存读回环=FAIL    存读轮 roundtrip=False
B4剧本三段封顶=FAIL   R1/R2 都停 Develop(均无军事)
B5派遣双证分列=FAIL   K1 全程 build(46/1) train0
```

**时间线**：R1=45×D；R2=第1天S+44×D（不再到 Expand）——趋同于都停 D，残留清掉后成长行为收敛（都因流浪汉池空无法招工）。

---

## 三、二轮甄定结论（据 HH.27 裁定"清点OK仍分歧→转产品"）

**已排除（解释2 harness 残留）**：轮间清点 OK、存读 v2 门控 OK。残留污染不成立。

**遗留真问题（转产品深挖）**，两个独立失败模式（对齐 HH.26"拆双证"）：

1. **流浪汉池空＝B1/B5/B4 共通卡点（产品-环境断层，非判定产品 defect）**
   - pump 走完整 DayCycleSettlement，但地图流浪汉由 GlobalMap 初始预置 + 营地每日补员（依赖 ActiveMap/营地滤镜）驱动，纯 pump 的 InitializeNewGame 无引导时序、ActiveMap 未产 → 流浪汉池空。
   - 玩家 RecruitVagrant(pFalse根本因=候选缺失) + AI ExecuteRecruitWorker(FindRecruitableVagrant 空→train0) 全部空转 → 人口不长 → 停 Develop → B4 未达 Expand。
   - 判定：需 harness 侧程序化 spawn 一个 Occupation.Vagrant 单位注入 UnitRegistry 续验 B1 通道；招工→成长段归人工 Play（恰合 HH.27①让渡）。
2. **A3 wood 漂移（52 vs 60）+ train0 已成行** → **产品 AI 非种子随机**
   - 残留已清（entry 一致、worker 均 6 一致）→ 两纯轮起点相同，却 wood 消耗不同(建 build3 vs build1)。来源：AI 建造选址/焦点决策经 UnityEngine.Random（非种子）。
   - 待甄别归属：不急着归 TrainingSystem L305（本轮 train0 根本没走到它）。wood ±8 分叉日需二分定位，坐实或排除 Random.Range 嫌疑（§四算法附探针）。

---

## 四、登记：确定性全库扫描小卡（HH.27 裁决②实锤，独立不阻塞）

- 实锤：TrainingSystem.cs:305 var chosen = pool[Random.Range(0, pool.Count)] —— UnityEngine.Random 未种子化，多候选池确定性破坏（策划 grep 实锤，执行端复核确证）。
- 归属：独立确定性纪律小卡【确定性全库扫描】——扫描 KingdomBrain/决策核/训练/建造/AI 消费面所有 UnityEngine.Random，改用种子派生 System.Random（对齐 VagrantCampSystem D308 先例）。
- 附探针要求（策划裁定 A3）：wood 漂移 ±8 的分叉日二分定位探针（哪一天开始差、当天谁动了 wood），把 Random.Range 嫌疑坐实或排除；全库 UnityEngine.Random 消费面全挖。
- 不阻塞 P0 收官：A4 玩家零回归不在受影响面；A3 判据待小卡修复后重跑终判。

---

## 五、请策划终裁 P0 收官（三问）

1. A3：wood 漂移指向产品 AI 非种子随机——置为小卡修后重跑 A3 终判（策划已裁：准，附二分定位探针）。
2. B1/B4/B5 流浪汉卡点：接受 harness 补程序化 spawn 真实流浪汉续验 B1 通道，招工→成长段归人工 Play（策划已裁：准，人工 Play 三项正式接单）。
3. B3 存读回环：跟随确定性小卡一并扫，本轮不单独挖（策划已裁：准）。

---

## 六、交接状态

- 执行序：回写 HH.27 二轮版 → push → 确定性小卡（清点+L305修+wood 二分）→ 重跑套件全终判 → HH.27 终版报裁 P0 收官。
- 人工 Play 三项（策划正式接单）：①细模拟经济闭环（工人真走真产）②招工→成长链③GameOver 路径（灾变三禁未验）。
- ④债：维持不变（归步骤12/领土入档先到者，触发必落 foundKingdoms 门控+三处追记回写）。

---

## 附：形实快照（供策划核对）

```
首轮(残留u):  u=18/36/54   R1=45D    R2=SDD...E...   A3 wood 漂移大
二轮(清残留):  u=22/22/22   R1=45D    R2=SDDDD...     A3 wood 52 vs 60（唯一分歧）
甄定:         残留排除 → 转产品 AI 非种子随机 + 流浪汉池空(环境断层)
```