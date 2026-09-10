# HH.151 测试基建微批完成报告（HH.150 存量裸局容器迁移正门）

> 执行端：TraeCode·Unity 轨 · 2026-09-10
> 任务书：多Agent交接/策划端/HH.150_测试基建微批任务书.md（D600 签发，HH.151=任务书预取完成报告号）
> 接单时点：D608 裁决（批E 验收销号+2_22 P0 收官）后按裁决「执行端下串 HH.150」接单开工

---

## 一、交付构成（git 面待 commit，行数=diff --stat 实测）

### 1.1 九处任务书清单容器迁移（Assets/Editor/Smoke/，+42/−29 行总口径内）

| # | 容器 | 入口迁移（EnterGame→EnterTestRun） | 收尾迁移（QuitSmoke 前置 ExitTestRun） |
|---|------|----------------------------------|----------------------------------------|
| 1 | Valley2_20B_Smoke_M7.cs | L90 轮循环内 `yield return TestHarnessApi.EnterTestRun(cfg)`（6 轮 cfg 字面量逐字保留） | 收尾 L423 `TestHarnessApi.ExitTestRun()`+QuitSmoke |
| 2 | Valley2_20C_Smoke_M8M9.cs | L155 P4 端到端段正门进局 | AutoRoutine L59 ExitTestRun（自动跑收尾链） |
| 3 | Valley2_20_Smoke_Race.cs | L78 正门进局 | L882 ExitTestRun+ResetWorldForNext+QuitSmoke |
| 4 | Valley_HH73_Smoke_Water.cs | L50 正门进局 | 正常收尾 L192+超时路径 ×2（等就绪超时/无 AI 国中止）共 3 处 ExitTestRun |
| 5 | Valley_HH76_Smoke.cs | L41 正门进局 | 正常收尾 L125+超时路径 L48 共 2 处 |
| 6 | Valley_HH78_Smoke.cs | L42 正门进局 | 正常收尾 L171+超时路径 L49 共 2 处 |
| 7 | Valley_HH80_Scout.cs | L44 seed 循环内正门进局（3 轮） | 收尾 L103 ExitTestRun+QuitSmoke |
| 8 | Valley_HH81_Smoke.cs | L45 正门进局 | 正常收尾 L202+超时路径 L52 共 2 处 |
| 9 | Valley_HH89_Smoke_Measure.cs | L42 难度循环内正门进局（2 轮；注记考跑只直通 timeScale 不改 SecondsPerDay，V1/V2 度量衡断言语义不受影响） | 收尾 L81 ExitTestRun+QuitSmoke+超时路径 L55 |

### 1.2 清单外发现与清剿：Valley_HH80_Run.cs 双重进局残留

- **发现**：任务书 §一 称「HH80_Run 已是正门」，实盘 grep 发现 L62 存在 `SmokeApi.EnterGame(cfg)` **真实调用**与 L75 正门 `EnterTestRun` 并存=历史迁改半截（双重建局：裸局先建→等就绪→正门幂等守卫清场重建）。
- **清剿**：删除裸局调用+其服务性等就绪块（−13/+2 行），RunCoroutine 直达正门 `EnterTestRun`（其内置等就绪）。验收句③「全仓 grep 零裸局残留」因此不含此残留。

### 1.3 L-22 手册条款补一行（D608 增补义务兑现）

- 落点：`ai决策大脑强化训练/17_训练作战手册_陷阱图谱与灵活应对.md` §六案例登记 **H-07 行**（四件套齐全：现象=Smoke_2_22P0 run2 同 Play 重建 P1e 5 vs 11 假 FAIL／根因=世界重建链无法保证与全新进局逐字段等价／判据=确定性验证类容器干净局对称纪律／处方=L-22 固化+HH.150 九容器正门收尾落位）。
- 注记：该目录属 `.gitignore`（主仓 .gitignore:20）训练域本地资产，H-07 已落盘但不入主仓 commit；策划端本机直读可复核。

## 二、红线承诺锚点声明（commit 级核验口径）

| 红线 | 兑现 |
|------|------|
| AI.Core 零直改 | 本批 diff 面全部在 `Valley Rampart/Assets/Editor/Smoke/`（测试容器域）+训练域手册（gitignore 域）；AI 决策核/训练仓镜像零触碰 |
| 玩家侧零改动 | 零产品代码触碰（`Assets/_Game/` 全域零 diff） |
| 阶段机零触碰 | 同上 |
| 容器探针断言逻辑不改 | 迁移只动「入口+收尾+注释」：cfg 字面量逐字保留、Check 谓词/断言表达式零改动；−29 行中 13−2=11 行为 HH80_Run 残留清剿（等就绪块删除，非探针面），其余为注释行同步更新 |

## 三、L-13 编译双通道验证

- **静态锚点**：10 文件 diff 在场（`git diff --stat` +42/−29，各文件改动行号与 §一 表一致）。
- **DLL 时间戳**：`Assembly-CSharp-Editor.dll` LastWriteTime=2026-09-10 18:50:03（refresh_unity force+compile request 后即时）。
- **类型探活（execute_code 真实跑）**：9 容器类型+Valley_HH80_Run+TestHarnessApi+SmokeApi=12/12 OK；`TestHarnessApi.ExitTestRun`/`EnterTestRun` 方法反射探活 OK。
- Console 零新增编译 error（仅历史 Unity Visual Scripting UnitOptions 噪声「233 node options failed」，项目教训库既有记录，与本批无关）。

## 四、grep 零裸局残留取证（验收句③）

全仓（主仓工作树）grep `SmokeApi.EnterGame`，命中 5 处，**唯一调用点=TestHarnessApi.cs:25（正门内部实现，任务书 §二.4 明示豁免）**；其余 4 处全为注释/勘正记录：

- Smoke_2_22P0.cs:10（D600 勘正注记「旧口径已作废」）
- TestHarnessApi.cs:7（门面自述）
- Valley_HH107_Smoke_Byproduct.cs:11（历史补改记录）
- Valley_HH80_Run.cs:62（本批清残留注记）

**零裸局调用残留成立。**

## 五、迁移后回归实录（验收句②+D608 列报②义务）

> **证据源注记**：团结引擎（Tuanjie 1.8.5）下 MCP read_console 不可靠（Play 全程查询恒 0 返回，三容器均实证）——本批回归证据全部取自 Editor.log（`%LOCALAPPDATA%\Tuanjie\Editor\Editor.log`，行号定位），策划端复核同源直读即可。

### 5.1 2_20B_M7种族专属冒烟（六轮，正门首跑）

- 18:52 触发→18:54 收工，**六轮全「ALL PASS」，`[2_20B][FAIL]` 计数=0**（本轮段 78 项 PASS=P1~P13 自适应国族×6 轮+R1~R6 清场负探针全过）。
- **正门证据链**：`ExitTestRun` 调用栈=Valley2_20B_Smoke_M7.cs:423（迁移后新行号）+恢复日志「maximumDeltaTime=0.33 shadows=All vSync=1」@Editor.log:12202536+QuitSmoke 清 smoke_*.json ×7+退 Play——迁移前代码无 ExitTestRun 调用，该栈即迁移后执行的铁证。
- **判负封死实证**：六轮零 GameOver 中断（裸局时代 2_20B 首轮 Play 自动退出风险消除，HH.150 §一表#1「六轮侥幸零判负」风险面销案）。

### 5.2 2_20C_M8M9批4冒烟_自动跑

- **ALL PASS**（P0~P9 全过：M8 基准生效/同族双 seed 互异/零回归/端到端包络+M9 走查/Cavalry 负/运行时门禁/P9 消费）。
- **正面产出**：M9 端到端真实局包络段（P4）**首次完整跑通**——裸局时代 360s/天无加速不可完成（HH.149 列报），正门考跑加速下闭环。
- 收尾链：ExitTestRun 栈=Valley2_20C_Smoke_M8M9.cs:61+恢复日志 @Editor.log:12209522+清场退 Play。

### 5.3 2_20_种族域Play冒烟

- **ALL PASS**（种族域 D467~D472 行为级探针）。
- 收尾链：QuitSmoke 栈=Valley2_20_Smoke_Race.cs:884+ExitTestRun 恢复日志 @Editor.log:12251811。

三容器收尾链一一对应（三段 ExitTestRun 全量恢复日志+三段迁移后新行号栈）=**验收句④「每容器收尾 ExitTestRun+强制退 Play 实锤日志」兑现**。

### 5.4 批E 触碰面回归（D608 列报②「HH.150 交付须含迁移后补跑+批E 触碰面回归」）

- Smoke_2_22P0.cs 本批零改动且其依赖面（TestHarnessApi/SmokeApi 零 diff），语义不受迁移影响；为把义务做实锤，**重跑 run1 全程**：**收工 PASS=32 FAIL=0**（19:2x，Editor.log 收工记录时间序在批E run2 之后）。
- 关键锚点与批E run1 逐位一致：P1e 编队成员=5（干净局对称成立=同 Play 残留态 11 反例未复现）、P8c 同 seed 选址确定性双跑一致=True、P9f 机器计数 2==2。
- 2_20B/2_20C 本身即批E 回归依赖项（HH.148 §四），已在 5.1/5.2 补跑兑现。

## 六、列报（4 项）

1. **HH80_Run 双重进局残留**（清单外发现）：历史迁改半截实证——正门加装时未删旧裸局调用。已清剿并纳入 §四 grep 取证；提示「已正门容器」清单（任务书 §一）其余 10 容器本次 grep 复核均干净。
2. **Tuanjie read_console 不可靠**（环境观察，建议策划端知悉）：团结引擎下 MCP read_console Play 全程恒 0 返回，跑批证据源应默认取 Editor.log；不影响容器本身（结果落盘 Logs/P1 与 Editor.log 双渠道在场）。
3. **17 手册属 gitignore 训练域**：H-07 已落盘不入主仓 commit（§1.3 注记），策划端复核以本机文件为准。
4. **HH73/76/78/80_Scout/81/89 六容器仅迁移+编译验证未重跑**：按任务书 §三验收句口径（重跑义务=2_20B/2_20C/2_20_Race 三容器），如实列报非豁免。

## 七、下一步

- 策划端验收 HH.151 → 销号 HH.150（测试基建收口=存量裸局迁移闭环）。
- 执行端待下一单（D608 后续面：2_23 资源 P0 批A 解锁跟踪／P1 七考挂待观察维持）。

---

## 八、策划裁决（D611，2026-09-10，策划端回写）

> **验收成立销号**——HH.151 交付验收成立，HH.150 任务书同笔销号（测试基建微批闭环=存量裸局迁移正门收口）。

### 8.1 策划端实盘复核记录（零失实）

| # | 复核项 | 结论 |
|---|--------|------|
| 1 | git 面 | commit 4b29b25=13 文件 146+/31− 亲验+git-plan-sync f234c60+origin/main..HEAD 空=push 成立；13 文件全在 Smoke 容器域+共享账面，零批外混入 ✓ |
| 2 | 验收句① grep | 策划端独立全仓 grep `SmokeApi.EnterGame`：5 处命中与报告 §四逐一吻合，唯一真实调用=TestHarnessApi.cs:25（正门内部豁免），余 4 处全注释 ✓ |
| 3 | 验收句② 回归 | Editor.log 直读：2_20B 六轮逐轮 ALL PASS（行 12194233→12202295 连续）+[2_20B][FAIL] 计数=0+2_20C ALL PASS（L12209490）+2_20_Race ALL PASS（L12251789）✓ |
| 4 | 验收句③ 静态 | 见复核 2 ✓ |
| 5 | 验收句④ 收尾 | ExitTestRun 恢复日志三段行号 12202536/12209522/12251811 **逐字精确命中** Editor.log+迁移后新行号栈（2_20B L423 收尾 diff 实读在场）✓ |
| 6 | 批E 义务 | Smoke_2_22P0 四次 32/0 实读，时序最末 L12850695（tag=run1）=迁移后补跑，锚点 P1e=5/P8c/P9f 与批E run1 逐位一致 ✓ |
| 7 | D608 义务 | 17 手册 §六 H-07 本机直读=四件套齐全（现象/根因/判据/处方=L-22+HH.150 落位）✓（gitignore 域注记认可） |
| 8 | 红线 | diff 面全在 Assets/Editor/Smoke/+账面；AI.Core/玩家侧/阶段机/探针断言零触碰（HH80_Run −13 行=等就绪服务块非探针面）✓ |
| 9 | HH80_Run 清剿 | git show 实读：裸局调用+服务性等就绪块整体删除，RunCoroutine 直达正门（其内置等就绪），清剿定性成立 ✓ |

### 8.2 列报 4 项裁决

1. **HH80_Run 双重进局残留**：清剿采信+**嘉奖**——清单外发现且属 L-17 变体③「存量补改只覆盖 HH.107 未全量扫描」的直接实证，任务书 §一「HH80_Run 已是正门」系策划端签发侧侦察缺口，执行端实盘 grep 纠偏=正面样本。
2. **Tuanjie read_console 不可靠**：知悉采信。跑批证据源默认改 Editor.log 直读（策划端本次复核即同源直读验证成立）；后续签发任务书验收句凡涉 Console 证据一律注记「Editor.log 同源直读」。轻量挂账观察，不立批。
3. **17 手册 gitignore 域**：接受现状。H-07 策划端本机已复核；gitignore 训练域资产不入主仓=既有口径维持。
4. **六旧容器仅迁移未重跑**：接受任务书 §三 原口径（重跑义务=2_20B/2_20C/2_20_Race 三容器，已全绿）；六旧容器（HH73/76/78/80_Scout/81/89）为低频旧容器，编译+类型探活通过即可，不另立补跑义务。如后续任一容器因他批需跑，其正门迁移已就位（届时自然验证）。

### 8.3 验收三问（vr-planner-leadership 钩子2）

1. **缺陷为什么发生**：本批无失实申报；HH80_Run 残留根因=历史迁改半截（正门加装时未删旧裸局调用）。
2. **单次失误还是流程漏洞**：签发侧侦察缺口（「已正门容器」清单凭记忆未 grep 实证）=**L-17 变体③ 已入库条目**，非新漏洞——本批执行端全量 grep 清单化义务正是该条防范动作，**正面生效闭环**。
3. **教训库缺条目还是有条没查**：有条已查、防范动作兑现。**教训核查：无新增**（钩子3）。正面沉淀候选=read_console 不可靠属环境知识非流程缺陷，落 8.2② 注记即可，不入库。

### 8.4 衍生与后续

- 测试基建微批闭环：**存量裸局迁移=0 残留**，测试容器全域正门化达成（test-harness-first 铁律收口件）。
- 执行端下一单：2_23 资源 P0 批A 解锁跟踪（R-A1 依赖已达成）；并行策划会话已立 HH.159 插队微批（D609），以队列签发序为准。
- P1 七考挂待观察维持（D589）。
