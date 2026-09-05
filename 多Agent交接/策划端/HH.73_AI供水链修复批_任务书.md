# HH.73 任务书：AI 供水链修复批（P1 粮链死锁根因修复）

> 类型：任务书（修复批派单）
> 状态：⏳待执行端接单
> 日期：2026-09-05 · 发起端：策划端 · 前序：HH.72 P1 熔断报告（§策划裁决已回写，H1/H2/H3 全否另立 H0）
> 决策号：D535（0.6 §六十七 已登记）

## 一、背景与根因（策划端实盘定位，执行端照单施工）

HH.72 P1 长局熔断：三 AI 粮产出恒零→⑥招工不可支付→工 6<8 扩张门死锁→军事期不可达。策划端代码面独立定位真根因（H0），**非运行时 bug，为三因素历史叠加的设计性断水**：

| # | 因素 | 代码锚点 |
|---|------|---------|
| ① | 农场产粮前置=耗水（DR-9/DR-18：每次产出事件耗 2 水，缺水停产） | `ProducerComponent.cs` Tick 农场分支 `TryConsumeFarmWater()` |
| ② | AI 农田耗 AI 桶水，AI 桶**恒 0**（2_17 步骤11 批3a B′：堵"AI 农田吃玩家网水"泄漏面时断供，注释自认「桶结构保留供未来 AI 供水链」——供水链从未实现） | `WaterNetwork.cs` L69-81 ConsumeWater AI 分支 |
| ③ | AI 水井产水被 D454 守卫拦截不进任何网 + AI 立国预置建筑**无井**（Normal 档取 baseBuildingDefIds 前 4=castle/farm/mine/Warehouse） | `ProducerComponent.cs` 水井分支 `if (kingdomId > 0) return;` + `Kingdom_DenseForest/Bedrock/IronHoof.asset` |

派工/工人/任务链全部正常（farm 持续发 Production 任务、工人周期 Working、完成触发 prod.Tick() 后被缺水拦截白干）——粮链断点唯一且确定。

**用户拍板（2026-09-05 三连）**：方案 A 供水链落地（AI 井产水入 AI 桶+预置补井，系统语义对 AI 真实成立，不开挂）/ 同 seed 22360 对照首跑 / 策划端签发本任务书。

## 二、修复方案 A 落地项（策划端裁决定稿，执行端照单施工）

### 项1：AI 水井产水入 AI 桶（解除 D454 拦截，改路由）

`ProducerComponent.cs` 水井分支：`kingdomId > 0` 时不再直接 `return`，改为产水入本国 AI 桶——`WaterNetwork.Instance.AddWater(water, kingdomId)`（**带 kingdomId 重载批3a 已备好**，L46-60，AI 桶结构在，只差产水端接线）。

- AI 桶满停产语义对齐玩家（玩家 IsFull 停产；AI 桶满同样停止累计，防白算）。
- 玩家路径（kingdomId=0）逐位不动（HH.30 零回归红线）。
- 注释更新：B′ 双语义①「AI 井恒不产水」解除并注记本批裁决号 D535；语义②「AI 农田耗自己桶」**保留**（泄漏面堵法从「断供」升级为「own 供水」）。

### 项2：AI 预置建筑补井（策划端定稿插序与档位）

三族 AI KingdomDef（DenseForest/Bedrock/IronHoof）`baseBuildingDefIds` 在 farm 后插 `well`：
`castle, farm, well, mine, Warehouse, quarry`

`KingdomFoundingConfig.asset` staggerTiers `buildingCount` 三档 **3/4/5 → 4/5/6**：

| 档 | 原取 | 新取 |
|---|------|------|
| 帐篷(Easy) | castle/farm/mine | castle/farm/well/mine |
| 村落(Normal，P1 用档) | castle/farm/mine/Warehouse | castle/farm/well/mine/Warehouse |
| 要塞(Hard) | castle/farm/mine/Warehouse/quarry | castle/farm/well/mine/Warehouse/quarry |

- 三档粮链全通、Normal/要塞保留 Warehouse（Transport 卸货终点不回退）。
- 玩家模板 `Kingdom_RiverBay.asset` **不动**（玩家供水链=自主建造 Well，超出本批范围；其预置清单用途若涉及动态立国，见项4 排查）。
- **动态立国路径排查**（D471 同族营地插旗建国）：若动态立国建筑预置共用 baseBuildingDefIds 链则本改动自然覆盖；若走独立清单同样无井，列报+同批补井（新建国断粮=同根因）。

### 项3：AI 桶入档（存档缺口，本批一并补）

`WaterNetworkSaveData` 当前只存玩家桶（stored/capacity），`_aiStoredByKingdom` 不入档——读档后 AI 桶清零，农田断水至水井重新蓄满（时长短但语义不洁）。本批补：

- SaveData 加 AI 桶字典序列化字段（additive 兼容旧档：旧档无字段→AI 桶默认 0，水井重新蓄水自愈，零迁移成本）。
- `saveDataVersion` 处置与 2_11 schema 纪律对齐（additive 字段是否 bump 由执行端按 v2 存档文档口径定，列报即可）。

### 项4：不确定项列报（不阻塞，随交付报告列报）

- sim 侧 `SimEconomy`（QQQ.5 经济沙盘）农田产出**有无水约束**：预计无。若确认无→登记 15_账本「Unity 农田耗水闸门 vs sim 无此语义」已知差异（Unity 侧系统语义差异，非 AI.Core/sim 源码改动，无 T/F 级义务，登记口径=事实注记）。策划端裁决登记行。
- AI 桶容量沿用玩家 capacity=100 是否合宜（AI 单国 1 井 4 水/秒 vs 农田 2 水/事件，供大于耗，桶常满=正常态）——列报观察结论即可，本批不调参。

## 三、验收探针（冒烟容器全绿为 commit 前置，HH.53 教训）

| # | 探针 | 判据 |
|---|------|------|
| P1 结构 | AI 立国预置 5 座含 well；AI 桶有水 | 预置日志/桶水量日志在场 |
| P2 行为正 | AI 农田恢复产粮 | AI 农场「缺水」气泡消失；国库粮产出 >0（日结入账） |
| P3 行为负 | 玩家桶零泄漏不回归 | AI 农田耗水只扣 AI 桶；玩家桶水量变化仅来自玩家水井（批3a 泄漏面回归探针） |
| P4 存档 | AI 桶入档 | 存→读→AI 桶水量保持 |
| P5 对照 | **同 seed 22360 P1 对照跑**（复用 Valley_P1_Observer，协议同 HH.71） | 粮曲线对比熔断局：D2~D10 段从「-6 耗零入账」转为有入账（不必到正增长，产出链通了即可；量级看 farm 产率） |

P5 对照跑过关后按用户拍板换 1~2 个新 seed 复跑防单 seed 压缩缺陷面（D520 先例）——换 seed 复跑属 P1 重跑本体，可与修复批验收串分离，执行端自定节奏列报。

## 四、红线

1. 玩家水链零回归：玩家桶逻辑/玩家 IsFull/AddWater/农田耗水逐位不动（HH.30 契约延续）。
2. AI.Core / sim harness / champion / 训练仓：零触碰（本批纯 Unity 侧系统+资产）。
3. RulerController：零触碰（AI 资源不入玩家国库的既有纪律面不动）。
4. 冒烟全绿才 commit（HH.53 教训）；交付前 `git diff HEAD` 自查文件构成为交付前置（HH.42/HH.59 卫生指令延续）。
5. 观察器 Valley_P1_Observer 可按探针需要加白名单 tag（如 [WaterNetwork]/[AIEconomySettlement]），Editor-only 不入运行时。

## 五、交付物

1. 施工 diff（ProducerComponent/WaterNetwork+SaveData/三族 KingdomDef.asset/KingdomFoundingConfig.asset+动态立国路径若涉及）。
2. 冒烟报告（P1~P5 逐项+ALL PASS）。
3. HH.74 完成报告（含项4 两笔列报+15_账本登记行提案+换 seed 复跑节奏）。

## 六、策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 实施方案与范围 | | |
| 冒烟与对照跑结果 | | |
| 项4 两笔列报处置 | | |
