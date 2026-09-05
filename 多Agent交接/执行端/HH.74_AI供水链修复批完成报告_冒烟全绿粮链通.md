# HH.74 AI 供水链修复批——完成报告（D535 全清·冒烟 ALL PASS·P5 同 seed 对照粮链通实锤）

> 类型：完成报告（修复批交付）
> 状态：⏳待策划端验收
> 日期：2026-09-05 · 发起端：执行端 · 任务书：多Agent交接/策划端/HH.73_AI供水链修复批_任务书.md（开工回执=其 §七）
> 决策号：D535（0.6 §六十七）

## 一、结论（先行）

**H0 修复全清**：AI 供水链三因素全部落解——①AI 井产水入 AI 桶（D454 拦截解除改路由）②三族 AI 预置补井（farm 后插 Well）+buildingCount 3/4/5→4/5/6 ③AI 桶入档（additive v2）+动态立国同批补井。**冒烟 P1~P4 ALL PASS+P5 同 seed 22360 对照跑粮曲线剧变（D2 即入账：k1 密林 125 vs 熔断局 40），供水链语义对 AI 真实成立**。

## 二、施工 diff（七件，git diff --stat 对照=102+/23-）

| # | 文件 | 改动 |
|---|------|------|
| 1 | `Systems/Building/ProducerComponent.cs`（+16 区间） | 水井分支 D454 拦截解除：`if (kingdomId > 0) return;` 删除→`TickWaterToNetwork(_building.kingdomId)` 归属路由；TickWaterToNetwork 加 kingdomId 参数（玩家 =0 逐位原逻辑：IsBucketFull(0)≡IsFull+AddWater(0)≡单参重载；AI 桶满停产对齐玩家语义）；注释更新注 D535 |
| 2 | `Systems/Kingdom/WaterNetwork.cs`（+66 区间） | 头部 B′ 双语义注释更新（①解除注 D535/②保留）；+`IsBucketFull(int)`/`+GetStored(int)`（AI 桶公开读写口）；SaveState/LoadState 补 AI 桶（`aiBuckets` List<AIBucket{kingdomId,stored}>+saveDataVersion 1→2 版本判据，旧档缺字段→空→水井自愈零迁移）；ResetState 补 `_aiStoredByKingdom.Clear()`（跨轮清场完整性）；ConsumeWater AI 分支注释更新 |
| 3 | `Kingdom_DenseForest/Bedrock/IronHoof.asset`（各+1 行） | baseBuildingDefIds：`castle,farm,**Well**,mine,Warehouse,quarry`（**Well 大写=资产 id 实值**，任务书原文「well」小写为笔误——FindDefById L42 逐字==大小写敏感，小写将静默跳过致修复失效；回执 §7.3 已声明） |
| 4 | `KingdomFoundingConfig.asset`（6 行） | staggerTiers buildingCount 帐篷/村落/要塞 3/4/5→**4/5/6**（Normal 档新取 castle/farm/Well/mine/Warehouse，Warehouse 保留） |
| 5 | `Systems/Kingdom/KingdomFoundry.cs`（+34） | 动态立国同批补井：FoundFromCamp→PlaceCampCastle 后+`PlaceCampWell(camp, id)`（FindDefById("Well")/castle 东邻 NearestWalkable 取点/失败仅告警不阻断立国）——动态立国走独立预置（仅 castle，非 baseBuildingDefIds 链）实锤后按任务书授权补井 |
| 6 | `Editor/Smoke/Valley_HH73_Smoke_Water.cs`（新增） | 冒烟容器（P1~P4，见 §三） |
| 7 | `Editor/Smoke/Valley_P1_Observer.cs`（白名单+1 行） | 顺手项：镜像白名单+[WaterNetwork]/[AIEconomySettlement]（任务书 §四.5 授权；本文件为 HH.71 批 Editor-only 工装，随本批入库） |

## 三、冒烟报告（Valley_HH73_Smoke_Water，seed=20273/smoke_w73 槽，第三轮 ALL PASS）

```
[HH73冒烟] P1 结构 预置Well数=3(需≥3) AI桶有水=True(k1=12 k2=12 k3=12) =True
[HH73冒烟] P2 行为正 AI桶窗口峰谷降=76.0(需≥2) farmStorage峰=9 =True
[HH73冒烟] P3 行为负 玩家桶零泄漏=True(终值30) AI桶独立波动=True =True
[HH73冒烟] P4 存档 save=True load=True AI桶(k1) 存档时=20.0 改后=100.0 读回=20.0(期望≈存档值+产水增量) =True
[HH73冒烟] ===== ALL PASS（4/4）=====
```

- **P1 结构**：3 AI 国各 1 座 Well 预置+AI 桶有水（GetStored 公开口）。
- **P2 行为正**：AI 桶窗口峰谷降 76.0——ConsumeWater(2/次) 只由 TryConsumeFarmWater 调用，桶水大幅下降=AI 农田产粮事件真实发生；farmStorage 峰 9=产出面双证。
- **P3 行为负**：玩家桶零负跳变（AI 农田/井不碰玩家桶；终值 30=玩家自身 WaterHaul 挑水链上涨，TaskScheduler L577 玩家任务，非泄漏）+AI 桶独立波动=路由互斥证明（若 AI 农田错走玩家桶，玩家桶 0 不足以支付→farm 必缺水停产→P2 必失败）。
- **P4 存档**：扣桶至 20→Save→改 100→Load→读回 **20.0 精确命中存档值**——aiBuckets 入档/恢复全链成立。

**探针修正链（三轮迭代，如实列报）**：第一轮环境错场（MainMenuScene 跑 EnterGame→暖 boot 教训重演→切 GameScene 重跑）；第二轮 P2 峰谷只采窗口末值（桶单调涨时 trough==peak 误判）→改窗口内逐国 min 跟踪；P4 存档点恰逢桶满（98/100 差 2 无法区分）→Save 前 ConsumeWater 扣桶至 <20 制造区分度+判据改区间语义（读回≥存档值-2 且 ≤改后-10，对井产水时序鲁棒）。历次 FAIL 全归因探针设计，零产品缺陷回退。

## 四、P5 同 seed 22360 对照跑（观察器+HH.71 协议，p1_rerun1 槽，D8 收跑）

粮曲线对照（同 seed 同起点，封存=Logs/P1/p1_snap_P5RERUN_D8.csv）：

| 日 | k1 密林(精灵) | k2 磐石(矮人) | k3 铁蹄(兽人) | 熔断局三国 |
|---|---|---|---|---|
| D2 | **125** | 40 | 48 | 40/40/40 |
| D4 | 318 | 28 | 48 | 28/28/28 |
| D6 | 416 | 22 | 48 | 22/22/22 |
| D8 | 412 | 10 | 48 | 10/10/10 |

- **判据满足（D2~D10 段从「-6 耗零入账」转为有入账）**：k1 D2 即 +85 入账、D5 峰值 416；k3 D2 +8 后日产=耗平衡（48 恒定）；AI 桶全程 100 满（1 井 4/s vs 农田 2/事件，供大于耗=正常态，任务书项4 预判属实）。
- **k2 与熔断局同斜率（-6/日 零产出）——已排除供水因素**（k2 桶 100 满+farm 预置在场+封存日志 k2 无 Granary 建造占用[D7 仅 1 条 House]），归因开放=派工域（farm 无 Working 工人，TaskScheduler 活权重=2_23 资源 P0 批B 清单已覆盖）或农田位置/地形，非本批范围，列报移交。
- 次要观察：k1 工人流失 6→2（死亡源不在镜像白名单，Editor.log 不可达未定位）；三族 farm 产出差异与 D503 挂账（兽人 0.70/矮人 0.75 farmMul）方向一致但 k2 量级非乘数可解释——归 P0 端到端调优批。
- **换 seed 复跑列报**：按任务书 §三属 P1 重跑本体，可与验收串分离——节奏建议=P1 重跑首跑即用 1 新 seed（协议同 HH.71：每 20 日评审+熔断 120 日），k2 现象若跨 seed 复现则坐实派工域派单。
- 复核包：p1_rerun1/p1_rerun1_day8 存档+p1_log_P5RERUN_D8.log+p1_snap_P5RERUN_D8.csv。

## 五、项4 两笔列报（任务书 §二项4）

1. **sim 水约束差异（实锤）**：训练仓 `harness/Economy/SimEconomy.cs` L133-137 农田产出公式 `FarmDaily(6)×等级系数×WorkersAssigned/divisor`，唯一 gate=WorkersAssigned>0；资源池 7 种无水字段；全训练仓 grep water/耗水零经济路径命中。→**15_账本登记提案**：「Unity 农田耗水闸门（DR-9/DR-18 每次产粮耗 2 水）vs sim 无此语义——Unity 侧系统语义差异事实注记，无 T/F 级义务」（登记口径=策划端裁决行，执行端不代登 15_账本， HH.30 先例）。
2. **AI 桶容量=100 观察结论**：本批沿用玩家 capacity。实测供大于耗（AI 1 井 4/s vs 农田 2/事件，桶常满 100=正常态；P2/P5 双局 AI 桶恒 100）——无需调参，列报观察结论即任务书要求全部。

## 六、追加列报（执行中实测）

1. **动态立国 farm 缺口**：动态国预置仅 castle（本批已补 Well）——farm 缺位期间粮 0 且⑥招工同锁死（新建国经济面），建议归 2_23 资源 P0 或另单。
2. **HH.42 复发三笔全闭环**（git 自查前置抓出）：KingdomFoundingConfig 三笔 SearchReplace 两笔未落盘（pwsh 直写补正）→补正脚本自身 $Matches 短路覆盖缺陷致缩进丢失（YAML 结构破坏）→二次字面替换修复+全量 Read 复验；ProducerComponent 第一笔未落盘（与已参数化签名矛盾=编译必炸态）→重补+grep 复验。**教训追加**：Unity YAML 中文 tierName 为字面 `\uXXXX` 转义文本，pwsh 正则需 `\\u` 双转义；`-and` 链中第二次 `-match` 会覆盖 `$Matches` 导致捕获组丢失。
3. 编译全程 0 error；编译态两笔（WaterNetwork/ProducerComponent 半改矛盾态）在 HH.42 抓出后即时闭环，未流入冒烟。

## 七、红线自查（任务书 §四）

| 红线 | 兑现 |
|---|---|
| 玩家水链零回归 | 玩家路径逐位不动：TickWaterToNetwork(0)≡原逻辑（IsBucketFull(0)≡IsFull/AddWater(×,0)≡单参）/ConsumeWater(0) 不动/玩家 Kingdom_RiverBay.asset 不动/WaterHaul 不动；P3 探针实证玩家桶零 AI 归因变化 |
| AI.Core/sim/champion/训练仓零触碰 | 零触碰（sim 排查只读；grep 级无改动） |
| RulerController 零触碰 | 零触碰 |
| 冒烟全绿才 commit | P1~P4 ALL PASS+P5 判据满足后才进入报告（commit 待策划验收后代执） |
| git diff HEAD 自查 | §八 |
| 观察器 Editor-only | 白名单两 tag 仅 Editor/Smoke 域 |

## 八、git 对照（git diff --stat HEAD 实盘）

- **本批 7 修改+2 新增**：ProducerComponent/WaterNetwork/KingdomFoundry/三族 KingdomDef.asset/KingdomFoundingConfig.asset+Valley_HH73_Smoke_Water.cs(+meta 新增)+Valley_P1_Observer.cs(+meta，上批遗留件随本批入库)。
- **域外（策划端域排除 commit）**：0.6_审查决策记录.md（D535 登记）/3.1.3_美术资源生产排期.md/美术资源规范_等轴立方体瓦片.md/图片资源\四族风格锚点\（untracked）。
- **本批文档件（随批 commit）**：HH.73 任务书（§七 回执）/HH.74 本报告/_交接索引.md/HH.71 回执（上批 untracked 遗留补收）。
- CRLF warning 六笔=行尾转换提示（仓库既有 autocrlf 行为，非内容变更）。

## 九、sim 义务

零 T/F 级（纯 Unity 侧系统+资产；sim 排查只读；观察器 Editor-only）。15_账本登记行提案见 §五.1（策划端裁决后登记）。

## 十、验收请求

1. **验收施工与冒烟**：七件 diff+P1~P4 ALL PASS+P5 对照判据满足。
2. **项4 两笔列报处置**（§五）：sim 水约束 15_账本登记行裁决+AI 桶容量观察结论知悉。
3. **追加列报处置**（§六）：动态立国 farm 缺口归属（2_23 资源 P0/另单）+k2 现象派工域派单确认（2_23 批B）。
4. **验收通过→commit 代执**（构成见 §八）→**P1 重跑解锁**（HH.71 协议，首跑建议换新 seed+k2 现象观察点）。

---

## 策划裁决（策划端回写，裁决前保持空白）

> 策划端实盘复核（2026-09-05）：七件施工逐件 diff 核读（ProducerComponent 路由/WaterNetwork 公开口+v2 additive+Clamp+版本判据/三族资产 `Well` 大写插行/buildingCount 4/5/6/PlaceCampWell 防御性告警/观察器两 tag/冒烟容器在案）+Well 笔误声明独立验证（Well.asset id=`Well` 大写+FindDefById L42 逐字 ==）+冒烟数据自洽性审查（P2 峰谷 76=ConsumeWater 唯一调用方实锤/P3 路由互斥论证成立/P4 读回 20.0 精确）全实锤。

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 施工与冒烟验收 | **✅ 成立（D537）** | 七件 diff 逐字吻合报告声明；P1~P4 ALL PASS 数据自洽；P5 对照判据满足（k1 D2 +85/峰值 416 vs 熔断局同起点 40；k3 平衡 48=产耗均衡态）。**Well 大小写笔误抓出=嘉奖**（策划端任务书笔误[照 farm/mine 小写类推错误]，FindDefById 逐字比较下照抄将静默跳过→修复整体失效且难从冒烟快速定位——施工时抓出避免一轮返工）；探针三轮修正链如实列报+零产品回退归因，诚信面佳 |
| 项4 两笔列报处置 | ①sim 水约束 15_账本登记行**准**（口径=「Unity 农田耗水闸门[DR-9/DR-18 每次产粮耗 2 水] vs sim 无此语义」，事实注记无 T/F 级；登记义务归策划端本串代登）②AI 桶容量 100 观察结论**知悉**（供大于耗=正常态，无需调参） | SimEconomy L133-137 唯一 gate=WorkersAssigned 与执行端实锤一致；Unity 侧系统语义差异登记与 HH.30 先例同口径 |
| 追加列报处置（动态 farm 缺口/k2 现象） | ①动态国 farm 缺口→**归 2_23 资源 P0**（挂账池登记；本批已补 Well=供水面闭环，farm 面待资源批）②k2 矮人零产→**P1 重跑正式观察点**（首跑即换新 seed=k2 天然跨 seed 侦察；跨 seed 复现→坐实派工域归 2_23 批B；不复现→归布局/地形偶发另察） | k2 已排除供水（桶满+farm 在+无建造占用）且 k1/k3 产粮链通=派工链本体活，k2 特异性开放合理；P1 判定线=≥2 AI，k1/k3 达标即可判 PASS（k2 作已知限制注记留痕不污染判定）；P1 重跑本身即 k2 诊断数据源，先跑=侦察 |
| commit 代执+P1 重跑解锁 | **✅ 代执**（构成=§八：施工 7+新增 2+HH 域文档[HH.73/74/索引/HH.71 补收]；排除 0.6/3.1.3/美术规范/图片资源=并行美术会话域，0.6 现行未提交段=并行会话 D536 非本串产物）；**P1 重跑解锁**（HH.71 协议原样+首跑换新 seed+k2 正式观察点+k1/k3 进军事期且 k2 注记=可判 PASS 口径预告，最终以实测为准） | 用户转发确认验收流程；D520 换 seed 先例沿用（22360 对照使命已完成于 P5） |
