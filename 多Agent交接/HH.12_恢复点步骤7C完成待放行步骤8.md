# HH.12 恢复点：步骤7C 完成，待策划放行步骤8

> 类型：进度同步 + 待决策
> 状态：✅已裁决（2026-08-22 策划端，见文末回写）
> 日期：2026-08-22 · 发起端：执行端 · 关联文档：2_12_王国建筑系统迁移_实施计划

## 一、做了什么（执行端，带证据）

2_12 步骤6→7→7B→7C 全线完成并提交（均 Play 冒烟 + 编译 0 error）：

| 步骤 | Commit | 内容 | 状态 |
|------|--------|------|------|
| 步骤6 | dc1d959 | 全资源刷新 丙⁺ 双路径（Tree 数据格 + 三类实体重建 D108 双列表） | ✅策划PASS |
| 步骤7 | 1949a62 | 建筑生命周期状态机（Ruined/修复链 D155~D165/totalInvested/RepairConfig SO） | ✅策划PASS |
| 步骤7B | d86e616 | 施工进度 UI（BuildProgressBar D117）+ Upgrading 修复（HH.11） | ✅（HH.11 正主复核 PASS） |
| 步骤7C | 0546bb8 + 3b1b74d | 箱子实体化（ChestManager/ChestEntity D245~D248）+ Upgrading 修复 | ⏳待策划 PASS |

- 最后 push：`4ba18883..3b1b74d8 main`（已全部同步远端）
- 工作树剩余：环境文档修改（`_目录`/`0.6_审查`/`0_改造总计划`）+ 未跟踪（`AGENTS.md`/`设计方法论_生命周期工作流.md`）——非本次职责，未提交

## 二、现状与阻塞

- **阻塞点**：步骤7C 完成汇报已发出（内容齐全+Play 证据+Upgrading 修复），**等策划端回 PASS + 放行步骤8**。协议要求不得替策划拍板。
- 步骤8 范围已确认：铁匠铺与 Metal 数据层（D199~D201），新建 `Systems/Kingdom/BlacksmithBuilding.cs` + `Data/BlacksmithDef.cs`（SO，stoneToMetalRatio 占位 2:1）+ `ResourceType.Metal` 入库（实体资源）
- HH.8 已埋指针：Metal 留步骤8；ResourceAmount 本卡立；Transform 签名立住防 sim 侧形状漂移

## 三、待决策事项

1. **步骤7C PASS + 放行步骤8**——这决定能否开工。证据已齐（四接口 Play 验证：Spawn/拾取/单格上限4/Strike重置/过期扫描全 PASS，编译 0 error）。
   - A（推荐）：判定 7C PASS，放行步骤8 开工。
   - B：需补验（说明补什么）。
2. **开发计划书 git 提交异常**——工作日志已写入磁盘（顶部新增步骤7/7B/7C 行），但 git 显示无 diff（疑似追踪了不同编码名副本）。不影响内容交付，是否需处理 git 层。

## 四、下一步建议（恢复后）

1. 取策划对 HH.12/HH.11 的裁决（等 PASS）→ 开工**步骤8 铁匠铺与 Metal 数据层（D199~D201）**
2. 步骤8 三件：`ResourceType.Metal` 入库 + `BlacksmithDef` SO（2:1）+ `BlacksmithBuilding`（石→Metal 就地加工 D200，Metal 入仓库/装箱/搬运）
3. Metal 消耗方（D201）：兵种强化 D132 + 工事升级 D131，从王国仓库取 Metal
4. 待议：是否先做不需要裁决的搭桥前置（Metal 枚举入库）——等策划拍板

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 步骤7C PASS + 放行步骤8 | **PASS，放行步骤8**。策划核证：提交链 0546bb8/3b1b74d ✓、ChestManager/ChestEntity/ChestConfig 三件就位 ✓、职责边界正确（触发源留 2_8/2_14/步骤11，渲染留 2_10，存档留 2_11，本步只做容器本体无越界）。**Upgrading 修复确认闭环**（UpdateProgressBar 已显式声明 Upgrading 走 Constructing 态，HH.11 复核必须项兑现，2_13 取证义务保留）。附：Metal 搭桥前置**不做单独 commit**（无生产无消费无对拍，无独立验收意义），并入步骤8 首个动作与 BlacksmithDef SO 同 commit | 四接口 Play 验证齐 + 边界干净 |
| 开发计划书 git 异常处理 | **非异常，未提交而已**。策划 git diff 实测：工作日志 4 行新增（7C/7B/7/6）在工作区未 commit；"不同编码名副本"不成立（ls-files 追踪即此文件，diff 正常）。处理：随步骤8 的 git-plan-sync 一并提交，不单独提交、不排查 git 层 | 误判根因，勿浪费排查 |

**步骤8 范围确认**：Metal 枚举 + BlacksmithDef SO（2:1 占位）+ BlacksmithBuilding（石→Metal 就地加工 D200，Transform Metal 分支兑现）+ Metal 消费方（D131 工事升级/D132 兵种强化，从仓库取）+ **RulerController 退役三件套（资源池迁移+退役+GameOver 切换，HH.12/步骤5 挂账归此）**。sim 侧 IWarehouse 门禁（HH.8 握手）收尾时核对。

### 分歧裁决记录（有分歧时必填）
- 执行端意见：7C PASS + 放行步骤8（A）；git 异常需处理。
- 策划端意见：7C PASS 确认；git"异常"实为未提交（误判），随步骤8 收尾提交；Metal 前置并入开工首个动作。
- 裁决：PASS+放行 / 随步骤8 提交 · 依据：验收纪律（证据齐）/ 最小提交粒度（无意义单 commit=噪音）

### 衍生产物
- 新建设计文档：无
- 新建清单任务：AGENTS.md/设计方法论 悬空条目（HH.11 裁决②遗留）——下次 HH 列明再裁，继续有效