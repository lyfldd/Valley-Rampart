# HH.261「OB1-2 A 批：三处产能组件自持 `Production` ＋ 落岗门」交付报告

> 类型：交付报告｜状态：🟡 待验收
> 日期：2026-09-13 · 发起端：执行端 · Gate：`G1-2`
> 关联：**HH.257 任务书（D703）＋尾部勘正块（D704）**／**D705**／HH.258／HH.259／HH.260
> 取号：HH.261（账本水位线 260→261·独立单行 commit `b7f05e5`）

---

## §口径（D705 裁③ 必写项 · 先写明免误读）

1. **同源判据参数差异＝架构差异所致，非口径冲突**：`ProducerComponent.cs` 既有门读 `HasWorkerAssigned(_building)`——因**现状任务源＝`Building`**（由 `Building.TryAdvertiseTask` 的 ② 分支代言）；**本批三处组件自持广告 ⇒ 任务源＝组件 ⇒ 门读 `HasWorkerAssigned(组件)`**。
2. **第 4 环 🔴 的定性（D705 裁②）**：gap 表第 4 环的「前置条件」＝`Production` 广告源，**即本批施工产出物本身** ⇒ 属「**施工前必缺、施工后必在场**」，与 `L-30` 分列中「**签发前必闭**」型前置（判定口在场／输入链在场）**语义不同** ⇒ **第 4 环的 🔴 是时点标记，不是缺陷记录**（§3.1 六环已逐条实读闭）。

---

## 一、施工内容（A 段 3 cs · 白名单内）

| 文件 | 改动 |
|---|---|
| `BlacksmithBuilding.cs` | 实现 `ITaskSource`（自持广告 `Production`·source＝组件·`destType=None`）＋ `LazyRegister`/`OnDestroy` 注册链 ＋ **在岗门**（`HasWorkerOnDuty` ＝ `HasWorkerAssigned(组件) ‖ HasWorkerAssigned(_building)`） |
| `SiegeWorkshopBuilding.cs` | 同上（门＝`HasWorkerAssigned(组件)`；无本体搬运广告故不并计建筑） |
| `MineByproductComponent.cs` | 既有 `TryAdvertiseTask` 加 **硬 if/else**：**无在岗 ⇒ 发 `Production`；有在岗 ⇒ 走既有三仓 `Transport` 判定**（禁同 tick 双发）；`Tick` 三槽累积前落**同源在岗门** |

- **`LazyRegister()` 置于岗门之前**（否则无在岗时永不注册 ⇒ 永不自举）——三处同构。
- **❌ 未动**：`Building.cs`（含 `TryAdvertiseTask` 分支结构）／`AI.Core`／sim／训练仓代码／数据 SO／`TaskScheduler` 主体路由／`ProducerComponent`。
- **新增（非 `_Game` 面）**：`Assets/Editor/Smoke/Valley_OB12_InGate_Probe.cs`（**只读验收容器**，正门 `EnterTestRun`）。

## 二、验收证据（**同 seed 修前/修后对照**：seed=21107＝HH107 冒烟同 seed）

**修前锚（HH107 冒烟·同 seed 21107·文档在案）**：P1「无工人 ⇒ 矿洞副产水晶/火油入子仓」**通过**（＝旧「恒产」行为实证）。
**修后（本批容器·同 seed 21107·两跑次一致 → 判定 `ALL PASS`）**：

| 探针 | 实测（实盘 console 输出） | 判定 |
|---|---|---|
| **P0 结构** | `ITaskSource 自持＋注册(矿True/黑True/厂True)` ＋ `无在岗发 Production(矿True/黑True/厂True·destType=None)` | ✅ |
| **P1 负探针（无在岗 ⇒ 恒 0）** | 窗 8s：`水晶0→0 火油0→0 矿石0→0 黑匠Metal0→0 厂弹0→0`；`HasWorkerAssigned 全程=False` | ✅ |
| **P2 正探针（有在岗 ⇒ 产）** | 窗（补员 12·AI 国）：`水晶=4 火油=4 矿石=4 黑匠Metal=1 厂弹>0（另跑次实测 0→5）` ⇒ **三处均增=True** | ✅ |
| **P2-补证（黑匠·实盘日志）** | `[AIEconomySettlement] k1 Blacksmith @159,64 日结入账 Metal×N → 国库` ＋ `[RulerController] 国库 Ore -2`（持续）⇒ **黑匠持续产 Metal 并耗 Ore**（本地仓被日结清空 ⇒ 探针首跑误读本地仓，已修正口径） | ✅ |
| **P3 线6 后段门（AI 自然国·零补员）** | `k1 自然mine 派驻=19 最大无派驻间隔=4.9s(<60 ⇒ 不报裁) idleMin=6`；**`verdict=P3_hit`** | ✅ **核心可达** |

**M7 新旧输入集硬断言**：**无在岗 ⇒ 产量恒 0**（P1 三处全 0 逐条命中）✓。

### L-34 五列在线判据表

| 判据 | 可判定最早日/窗口 | 命中即停 | 服务哪条验收句 | 作用域 | 口径来源与排除项（`L-35`） |
|---|---|---|---|---|---|
| P1 三处产出零增量且 `HasWorkerAssigned` 恒 false | 8s 窗（≈1.6 游戏日） | 命中即定论（负探针） | 线1~3 负探针＋M7 | **指定站点（`kingdomId=9901` 无单位国）** | 子仓 `storedAmount` 增量／黑匠 Metal 仓／厂弹三仓合计；**排除**容量满·原料缺·未 `IsActive` |
| P2 三处产出 >0 | 60s 窗（命中即 break） | 命中即停（提前 break） | 线1~3 正探针 | 指定国（`aiKid`·直建站点） | 子仓/Metal 仓/弹仓；黑匠**排除**日结清仓（改判 Metal 增长或 Ore 消耗） |
| P3 AI 自然 mine `Production` 派驻 ≥1 且**无派驻间隔 <60s** | 60s 窗 | `派驻=0` 或 `间隔≥60s` ⇒ **报裁** | 线6 AI 保底（D705 峰值观测） | 指定国（自然国 k1·零补员） | `HasWorkerAssigned(自然mine组件)` 0→1 跳变计数；**排除**直建站点与补员工人 |

---

## 三、承接账（同批清残 · **判据＝误表述语义零残留 ＋ 正例**，非纯关键词零命中·`L-26`）

**已清残的活文档**：

| 文档 | 处置 |
|---|---|
| `2_24_架构提案.md` §8.1 定案表 | 三行门控列改「✅ 已门控（须在岗）·原口径作废」＋ **📌 正确措辞正例**一处 |
| `2_24_实施计划.md` OB1-2 行 | 追加「✅ 2026-09-13 落地」＋探针结果＋承接账已清残 |
| `生命周期登记表.md`（Ore/Crystal/FireOil 行） | 「三子仓**恒产**」→「三子仓**在岗才产**（D704/D705；原「恒产」口径作废）」 |
| `建筑功能与等级总表_v2.md`（mine 行） | 「过渡态现状（D562）=副产**恒产无门槛**」→「已由 D704/D705 改为**工人在岗 `Working` 才产**」 |
| `15_账本`「一·补二十九」③ | 「待落地项」→「**落地状态·已落地 2026-09-13**」＋结果＋「恒产口径自此正式作废（禁再以此描述）」 |

**正确措辞正例（写入活文档）**：**三处（铁匠铺／投掷机厂／矿洞副产）均须工人在岗 `Working` 才产，无人在岗即停产**；门＝`TaskScheduler.HasWorkerAssigned(本组件)`（铁匠铺另并计建筑）。

**不作改的（历史记录，非活口径）**：`0.6_审查决策记录.md` 的 D 记录叙述／`HH.107/108/158` 等历史报告／**主计划书内 4 处「恒产」均处 `D671/D672` 决策记录叙述**／代码注释（`BuildingFactory.cs`/`ProductionSystem.cs`/`Valley_HH107_Smoke_Byproduct.cs` 等，**在 3 cs 白名单之外** ⇒ 本轮不动，**已列报留后续批**）。

---

## 四、红线自检

- **零改动面**：本批唯一 cs 面＝**7**（4 删除面 HH.259 已交付 ＋ 3 门面本批）＋ 1 个 Editor-only 只读容器；❌ 未动 `Building.cs`／`AI.Core`／sim／训练仓代码／数据 SO／`TaskScheduler` 主体路由／`ProducerComponent`。
- **编译 0 error**（存量分离）＋ **退 Play**（`isPlaying=False` 实测）；`QuitSmoke` 收尾（`L-32`：无余留世界）。
- **正门** `TestHarnessApi.EnterTestRun`；**同 seed**（21107）不试 seed（`L-30`）。
- **写-改-commit 同串·只提本批文件·未 push**；`_任务队列.md`（CRLF-blob）**未触碰**。
- **sim-sync**：复述 D702＝**零义务**（不重审）；本批不触 `AI.Core`。
- **口径（`L-35`）**：上文所有断言口径来源＝实盘 console 输出／`file:line`；排除项＝已逐条列出。

---

## 五、列报（非阻塞 · 供策划端裁）

1. **P3 参考读数未达（非本批缺陷）**：60s 窗内 AI 台账水晶 `0→0`、石 `21→21`（未增）。**归因**：①副产子仓在窗内仅涨到 ~10/20（<80% 触发线）⇒ Transport 未发（**窗长不足**）；②`stone in>0` 属 G1-1 域（D701 已达成），本批不触采集链 ⇒ 未作独立复核（**排除项声明**）。**建议**：如需台账端到端实证，另开长局（≥D30）并入既有 G1 观测，不在本批扩窗。
2. **`⑦/⑰ 可进池` 读数＝`Feasible` False**（k1 时点）：本批不动 AI 决策 ⇒ 读数不可由本批改变；**「可进池」的准确口径（`Feasible` 抑或 stage 过滤后候选池）请策划端明确**，以便对齐 `D701`/`HH.221` 记法。
3. **冒烟容器口径缺陷（已就地修正）**：首跑 P2 为 `False` 系**探针目视黑匠本地仓**，而该仓被 `AIEconomySettlement` **日结清空**；已改为「Metal 增长 ‖ Ore 消耗」判定并复跑通过。**留档备查**。
4. **D705 裁④ 跟踪项**：`DZ-135` 关闭时须重评三产面工人需求（并计 `WaterHaul` 常驻）——**本批不预构**，照裁挂账。
5. **代码注释级「恒产」残留**（`BuildingFactory.cs`/`ProductionSystem.cs`/`HH107` 冒烟注释）：白名单外，**列报留后续代码卫生批**。

---

> 执行端（TraeCode）｜2026-09-13｜本报告＝OB1-2 A 批交付（探针 ALL PASS·编译 0 error·退 Play·承接账清残）。
