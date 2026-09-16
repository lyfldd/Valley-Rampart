# HH.295 · 底层重构批（片1 清障 ＋ 片2 纯删）· 开工回执

> 执行端｜2026-09-16｜接单 **HH.294**（任务书 `多Agent交接/策划端/HH.294_底层重构批_任务书.md`·D764）｜取号 **HH.295**（开工回执）
> 冷启已读：`_索引.md`（主文档）／`04_上层接入指南.md`／`底层执行计划.md`／`02_空间与粒度.md`／`_当前快照.md`／HH.294 任务书 ＋（1-A 处置依据）`HH.293_批A补正批_任务书.md`
> MCP 说明：本会话工具面未注册双 Unity MCP（平台层），按 skill `unity-mcp-first` §D698「终端应急恢复」走 **HTTP 8080 长连直调**（`node Temp/ums.js`），实测可用（`execute_code`/`refresh_unity`/`read_console`/`manage_editor`/`manage_scene`）。

---

## 一、接单范围

| 片 | 状态 | 说明 |
|---|---|---|
| **片 1 清障** | ✅ **接单开工** | `HH.293` 并入 ＋ 粒度代价实测 ＋ `F-10` 帧卡顿实测 |
| **片 2 纯删** | 🟢 **接单开工** | `TerrainType` 整层删 ＋ `PlainSubState` 删 ＋ 死接口删 |
| 片 3~6 | ⛔ **不开工** | 片 3 硬卡点＝片 1 实测（本回执附数字，待策划端判"放行/折中"）；片 4~6 依次等前片 |

**1-A 处置结论**：`HH.293` 截至开工时 **无执行端接单证据**（`多Agent交接/执行端/` 无 HH.293 回执文件；`_编号登记.md` HH.293 行仍为「🟢 待接单」）⇒ 按任务书 **"未接单则并入本片"** ⇒ 其 `B1`/`B2`/`B3` 三项并入片 1 一并施工（报告随片 1）。

## 二、并行改动隔离声明

工作区现有**非本批**改动（开工前已存在，一律不碰、不提交）：`Valley Rampart/Assets/Scenes/GameScene.unity`／`Assets/Editor/Smoke/Valley_HH284_Probe.cs`／`Packages/manifest.json`／`packages-lock.json`／`pixel-forge/**`／`美术资源文件夹/Ground/*.png`／`河谷防线开发计划书具体内容/3.6、3.8` 等。
本批提交只用**具名 `git add <file>…`**（禁 `-A/-u/.`）；**不 push**；写-改-commit 同命令串。

## 三、施工序（本批内）

1. 片 1-B 粒度实测（探针 `Valley_HH294_GrainProbe`）→ 2. 片 1-A（HH.293 三项）＋ 1-C（`Valley_HH294_FrameProbe`）
3. 片 2 代码改造（GridTypes/GridSystem/ResourceGenConfig/BuildingDef/PlacementValidator ＋ Smoke 同步）
4. 片 2 资产数据迁移（13 个 BuildingDef ＋ ResourceGenConfig.asset；Unity 侧写回，非手改 YAML）
5. 编译 0 error → 正门 `EnterTestRun` 跑验证探针（`Valley_HH294_Slice2Probe`）→ 收尾 `ExitTestRun`+`QuitSmoke`
6. 交付报告（片 1＝HH.296／片 2＝HH.297）＋ 台账回写

## 四、红线自检（开工时）

| # | 红线 | 自检 |
|---|---|---|
| 1 | 不碰中层（建筑功能 08／交互 05／任务 06／伤害 07／建筑数据化） | ✅ 本批未触 |
| 2 | 并行改动不碰；具名 add | ✅ 见 §二 |
| 3 | 不 push；写-改-commit 同串 | ✅ |
| 4 | 判据＝实测读数；未完成项显式列出 | ✅ 见两份交付报告 |
| 5 | 行尾（`core.autocrlf=true`）：改共享文本文件前验行尾 | ✅ `多Agent交接/**` 改前逐件验（CRLF/混合者走 python 二进制替换） |
| 6 | 确定性：同 seed 两次 ⇒ 逐格一致（含 climateZones） | ✅ 片 1/片 2 复验通过 |
| 7 | `AI.Core` 零触 | ✅ 本批未触（`_Game`＋`Assets/Editor`＋`Resources` 内） |
| 8 | 正门 `EnterTestRun` ＋ 三态退 Play | ✅ 探针均走正门自收尾 |