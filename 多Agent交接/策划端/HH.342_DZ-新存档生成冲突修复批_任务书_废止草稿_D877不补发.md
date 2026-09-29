# HH.342｜「DZ-新」存档生成冲突修复批｜任务书正文

> 主策划端 B1（2026-09-26）
>
> 本批占位号：HH.342。D 号由事务端按实存顺延，本任务书不自取 D 号。
>
> 本批与 HH.341 D1 首片施工是两份独立任务书、两套独立红线、两套独立提交面。A 必须先完成修复、重新基线并经事务端核实，B 才能进行 D1 的正式进局生产回归。

## 一、性质与目标

- 性质：施工修复批，目标是修复 DZ-新 建筑读档重建的确定性双路径生成冲突。
- 目标：同一存档建筑在读档时只产生一份有效 Building，其坐标、saveId、defId、王国归属和后续状态恢复保持一致；不得以吞错、静默跳过或改日志文案代替修复。
- 时序硬约束：必须先于 HH.341 D1 首片 WorldGatherSource 的正式进局回归完成修复并重新建立基线。带着四条冲突做 D1 回归会污染 Console 和存档对拍，A 未验收前 B 不得进入正式进局回归。
- 提交隔离：A 与 B 不混改、不共用提交面。A 的代码/日志面只服务读档冲突修复；B 的代码/日志面只服务任务协议首片切换。任何跨面需求先停手报裁。

## 二、已核实证据与读档链

以下锚点来自事务端直读，执行端开工前仍须打印当前行号对照：

1. Valley Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs:260-326：SpawnFromSave 反序列化 BuildingSaveData、解析坐标、检查网格占用、调用 CreateBuildingInstance，随后覆盖存档 saveId 并恢复状态。
2. Valley Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs:297-310：占用格已有 Building 时打印 [BuildingFactory] SpawnFromSave 冲突；该日志的四个恒定坐标为 (75,28)、(71,29)、(45,110)、(42,112)。
3. Valley Rampart/Assets/_Game/Systems/Save/SaveManager.cs:308-370：Load 先校验并设置 LastLoadedSaveVersion，再做 Global 分发，随后按 Scene 条目遍历 _spawners 调 SpawnFromSave，最后做 Scene LoadState 分发。
4. Valley Rampart/Assets/_Game/Systems/World/WorldManager.cs:345-350：LoadState 根据 LastLoadedSaveVersion 决定 instantiateBuildings，再调用 GenerateWorld；WorldManager.cs:122-142 是网格填充与地图建筑实例化闸门。
5. 调用方与复现：SaveManager.cs:363 → spawner.SpawnFromSave(entry)；HH.314（2026-09-20）、HH.341 D1 前置门、HH.341 跨档对拍均逐字复现，每次进局必现。该事实按“确定性存档生成缺陷”处理，不按随机噪声降级。

## 三、允许改动文件路径清单（A 独立提交面）

下面是本批唯一允许的生产代码面。每个文件只允许处理双路径生成、读档时序、重建去重和必要的清场/占用接缝；不得顺手重构。

1. Valley Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs
   - 允许锚点：SpawnFromSave（当前约 :260-341）及其为去重/清场所必需的同文件辅助逻辑（当前约 :363-376）。
   - 目标：保证读档重建只保留存档侧实例，冲突时有可判定的处理结果，且不产生第二份注册、占格或存档对象。
2. Valley Rampart/Assets/_Game/Systems/Save/SaveManager.cs
   - 允许锚点：Load（当前约 :308-381）及 _spawners 注册/查询的最小接缝（当前约 :164-175）。
   - 目标：保证 Global→Scene 重建顺序、spawner 调用次数和重复 saveId 处理可复算；不得扩展存档格式或改无关模块。
3. Valley Rampart/Assets/_Game/Systems/World/WorldManager.cs
   - 允许锚点：GenerateWorld（当前约 :96-148）与建筑 LoadState（当前约 :330-363）。
   - 目标：保证读档路径不会与 SpawnFromSave 并行生成同坐标建筑；不得改地图数据、地形算法或资产键。

A 面明确排除：Building.cs、GridSystem.cs、场景、Prefab、Assets/Resources/Buildings/**/*.asset 旧序列化键、AI.Core、最高优先级文档/、四本账本、HH.341 D1 允许面中的任务协议/源文件。若取证表明必须触碰排除项，立即停手报裁，执行端不得自选边。

测试与日志面：只允许写入 Logs/hh342_*（原始 Console、扫描、读档对拍、编译和 git 证据）；若需临时 Editor 探针，必须单列为临时文件、用后删除并在报告中列删除前后路径。A 与 B 不共享日志文件名或暂存路径。

## 四、修复要求

1. 先建立修复前基线：以同一可复现 seed/存档执行进局→保存→读档，记录四坐标冲突各自的日志条数、占用者 saveId、存档侧 saveId/defId、SaveManager 注册数和 BuildingRegistry/网格占用数。
2. 只修复双路径或重复注册根因。不得用“已有占用就跳过但不恢复”、吞掉异常、改成 Warning、删除四坐标数据或改存档键来压低计数。
3. 修复后必须重新编译并重复同一 seed/存档；同一存档至少完成一次保存→读档→再次保存→再次读档的闭环，证明没有复合腐坏。
4. 读档后四个坐标各保留一份有效 Building：坐标占用唯一，saveId 与存档条目一致，defId 与存档数据一致；LoadState 能按该 saveId 找到对象并恢复状态。
5. Console 证据走 L-95 三段法：
   - 段 1（Play 内）：正式进局读档运行期间读取并保存 Console 原始输出；
   - 段 2（退 Play 后）：退出 Play 后再次读取同一批次输出，记录过滤边界；
   - 段 3（空白对照复跑）：清空/隔离上一轮过滤状态后，用同参数复跑，证明不是旧日志残留。

## 五、可复算验收判据

### 5.1 四坐标零复现表

报告必须逐行列出四坐标，计数单位固定为“坐标 × L-95 段”：

| 坐标 | Play 内冲突条数 | 退 Play 后冲突条数 | 空白对照冲突条数 | 唯一占用数 | 存档 saveId 对齐 | 结果 |
|---|---:|---:|---:|---:|---|---|
| (75,28) | 0 | 0 | 0 | 1 | 是 | PASS |
| (71,29) | 0 | 0 | 0 | 1 | 是 | PASS |
| (45,110) | 0 | 0 | 0 | 1 | 是 | PASS |
| (42,112) | 0 | 0 | 0 | 1 | 是 | PASS |

表内四行必须全部为 0/0/0、唯一占用数为 1、saveId 对齐；任何一格缺读数即不验收。过滤词须明确为完整前缀 [BuildingFactory] SpawnFromSave 冲突，并附原始日志文件与命令。

### 5.2 读档链一致性

- 同一 ModuleSaveEntry 只命中一个 ISaveableSpawner，SpawnFromSave 调用数＝建筑 Scene 条目数；重复 saveId 新增数为 0。
- 四坐标的 defId、saveId、坐标和最终 LoadState 恢复对象逐项相等；再次保存后不增加同坐标条目。
- BuildingRegistry、网格占用和 SaveManager 注册表对四坐标均为 1；不得以 currentWorkers 或其他无关字段作证据。
- 编译 errors＝0；本批新增 Console error＝0。既有错误须分列、给完整堆栈和归因，不得混入本批通过数。

### 5.3 边界与回归

- git diff --name-only 只出现 A 允许面及 Logs/hh342_*；git diff --check 结果附原始输出。
- B 的 WorldGatherSource、任务协议、TaskScheduler 和 TaskBindingManager 文件在 A 提交面中命中为 0；反向也成立。
- 不得把 Edit 模式探针、静态扫描或单次 Console 过滤当作进局通过证据；至少一轮真实 Play 进局读档闭环必须在场。
- 修复前/后同参数读数、差异根因、回退点和执行命令齐全；未能复算的数字按缺证处理。

## 六、停手条件（不得自选边）

命中任一项立即停手，保留原始证据并报事务端/策划端：

1. 四坐标任一在任一 L-95 段仍有冲突，或只能通过改过滤器得到 0；
2. 无法证明是 A/B 双路径或重复注册导致，出现第二种未归因生成路径；
3. 需要改场景、Prefab、旧资产键、地图数据、AI.Core、四本账本或最高优先级文档；
4. 需要改 Building.cs/GridSystem.cs 等排除文件才能继续；
5. 需要扩大存档格式、引入新的全局台账、删除存档条目或吞掉 LoadState 错误；
6. 新增 Console error、编译 error、saveId 漂移、重复注册或二次保存条目增长；
7. A 与 B 出现同一文件、同一暂存路径或同一提交面命中；
8. 事务端尚未核实逐文件改动面和行号锚点，却要求入提交；
9. 任何判据只能靠 Edit 探针、静态假设或“看起来没有报错”证明。

## 七、交付物清单

1. 开工回执：修复假设、三条允许生产路径、当前行号复核、回退点和进局入口。
2. 修复报告：逐文件 git diff --name-only、file:line 改动锚点、编译结果、四坐标表、读档链计数和 L-95 三段原始日志。
3. 复现包：同 seed/存档、保存→读档→再保存→再读档命令与原始输出；报告注明日志过滤词和计数单位。
4. 回退包：修复前基线、修复后基线、差异根因、可独立回退的提交/补丁边界。
5. 事务端核实记录：逐文件、逐行确认 A 面未越界后，方可入提交；本任务书不授权执行端改写本文件或四本账本。

## 八、与 HH.341 D1 的时序关系

HH.342 必须先完成：修复 → 编译 → 同参数进局读档 → L-95 三段 Console 对照 → 四坐标零复现 → 事务端核实并重新基线。只有上述链路全部成立，HH.341 才能开始首片正式进局回归；B 的 D0 既有取证可作前置背景，但不能替代 A 修复后的重新基线。

## 【应登记项】

HH.342 | 「DZ-新」存档生成冲突修复批 | 主策划策划端 | 待执行 | 先修复四坐标双路径读档冲突并完成进局/L-95 重新基线，再放行 HH.341 D1 正式回归 | 不写 D 号

## 【本批写入与提交声明】

本任务书只声明 A 的施工边界；执行端每次写代码或日志，必须在回执和交付报告中逐文件列出改动路径及 file:line 区间，交事务端核实后方可入提交。主策划端不替用户拍板定值、不自取 D 号、不改四本账本。