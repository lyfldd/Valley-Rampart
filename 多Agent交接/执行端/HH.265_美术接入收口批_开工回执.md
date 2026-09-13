# HH.265「美术接入收口批（HH.264 · Gate=`G3-1`）」开工回执

> 类型：开工回执（含 §五 排雷 M1~M6 逐条自答 ＋ `L-30` gap 表两列 ＋ 四段推进序）
> 状态：🟢 **可施工**（**本批无请裁项**——口径已由 D707/D709 定稿；发现新歧义 ⇒ 报裁不停工）
> 日期：2026-09-13 · 发起端：执行端 · Gate：`G3-1`
> 关联：**HH.264 任务书**（D709）／**`改造计划/美术资源接入_SpriteAnimator播放器设计规格.md`＝本批唯一实现规格**／`HH.263 交付报告` §二 T13＋§七 ／ `0.6 §二百三十六`（D707）·`§二百三十八`（D709）
> 取号：**HH.265**（账本水位线 264→265 · 独立单行 commit）
> ⚠️ **取号说明**：本回执先出 ⇒ 占 **HH.265**；按 D640 #10「按实时水位线取号·禁预留」，**交付报告届时另取**（预计 HH.266，以落盘当时账本为准）

---

## 一、起点复述（已落项不回退·不重做）

`HH.263` 已落（**本批不动**）：F-01 状态注册表／F-02 playMode 三态骨架／F-03 循环／F-05 打断重播／F-06 变速三轴／F-07 flipX／F-08 随机相位／F-10 icon 排除／F-11 O(1) 生死／F-12 不可见注销／F-14 池复位（**部分**）／F-16 统一 pivot；
架构（单管理器集中推进＋`AnimSlot` 结构体数组内联＋脏写跳过＋零逐帧字符串比较）**已合规**，本批**不重构**。

**本批补**：`SpriteAnimatorConfig` SO（A1）／F-04 完成回调（A2）／F-09 回退**链**（A3）／F-13 LOD 分层（A4）／F-15 帧钩子预留（A5）／F-14 复核（A6）；
**本批给**：§7.2 **P1~P8 逐条读数**（B）／§7.1 预算（C）／§7.3 图集（D）。

---

## 二、§五 排雷逐条自答（M1~M6）

| # | 雷 | 自答（处置口径＋代价） |
|---|---|---|
| **M1** | A4 LOD 查询代价 | **禁逐帧逐单位查 `Dictionary`**。策略＝**Driver 侧 per-slot 时间闸**：每 slot 持 `lodTimer`，每 `lodRefreshInterval`（**SO 字段·默认 0.5s**）查一次 `LODSystem.Instance.GetLevelAt(transform.position)`；另在 `OnBecameVisible` 回表时**立即刷新一次**（防"回表用旧档"）。**代价**＝400 单位 / 0.5s ≈ **≤14 次 Dictionary 查/帧**（`WorldToMidChunk` 两次整除 + `TryGetValue`；零分配、零 LINQ）。**不订阅"档位变更事件"**：`LODSystem` 为既有系统，本批**不为其新增事件面**（避免扩既有系统接口）；若后续需事件化，另批（列报）。**实测读数进 C 段**（Driver.LateUpdate 段 <0.1ms 已含该查询）。 |
| **M2** | P7 告警刷屏 | 负查询缓存 `_missingSets`（key＝`race*1000+(int)occ`）＋ `_warned` 一次性告警**已落**（HH.263）。本批 A3 接上 `stateFallback` 后，**"状态缺"不再等于"整套缺"** ⇒ 告警粒度＝**整套无真图**（fallback 链末端才告警）。P7 探针＝构造缺图 artId 后**连续 N 帧**观察，**统计 `[SpriteAnimator]` 告警条数＝1**（禁刷屏实证）。 |
| **M3** | D 段图集破坏既有三件 | 规格 §八：图集不得破坏 ①`SpriteRefTable` 间接层 ②缺图回退链 ③统一 pivot 排序稳定。处置＝**D 段逐条回归**：①入集后 `SpriteRefTable.TryGet(artId)` 仍返回非 null 且 `Contains(sprite)==true`（`Sprite` 对象引用不变，仅底层 texture 被 atlas 替换 ⇒ 间接层不破）；②入集后**无素材 artId** 仍走回退（`bld_academy` 占位非崩）；③同一 sheet 子帧**同尺寸同 pivot** 实读（换帧 bounds 不变 ⇒ 排序不重算）。 |
| **M4** | C 段场景规模 | 压测容器**四族混编**：按 `RaceIds` 四值各 spawn ≥75（合计 **≥300**，目标 320~400），职业混编（工人/战士/弓箭手/居民）使状态分布覆盖 idle/walk/attack。**禁单族**（否则推进/DrawCall 读数不可信）。 |
| **M5** | 承 `L-29` | 本批一切"键不存在/无引用"判定**禁用文本 grep**：一律走 ①`SpriteRefTable.Instance.GetAllArtIds()/GetAllFrameArtIds()` 运行时枚举 ②`AssetDatabase` 编辑器键扫描。**文本 grep 仅用于"旧键是否残留于代码"这类正向存在性核对**（存在性可用 grep，**不存在性不可**）。 |
| **M6** | 承 `L-30` | gap 表见 §三，**两列分列**〔前置条件在场性｜签发前必闭〕/〔施工后可达性｜可留施工后〕。 |

---

## 三、`L-30` gap 表（两列）

| 环 | 前置条件在场性（签发前必闭） | 施工后可达性（可留施工后·须给判据） |
|---|---|---|
| A1 `SpriteAnimatorConfig` | `Resources/Config/Art/` 目录在场 ✅（HH.263 已建 `SpriteRefTable.asset` 于此）；`ScriptableObject`＋`Resources.Load` 范式在场 ✅ | 生成 `.asset` 后**实读字段**（`defaultFps`/`fpsByState`/`attackSpeedScale`/`runAsWalkSpeedScale`/`lodFpsScale[3]`/`stateFallback`）✅ |
| A2 F-04 回调 | `AnimSlot`（本仓自有）＋`UnitController.Die()`（`UnitController.cs:668`）在场 ✅；死亡**素材缺**（328 图无 `_death`）⚠️ | 回调触发可由探针直接驱动验证；**death 真帧素材缺** ⇒ 走 fallback 链验证"定格＋回调"，**素材缺列报** |
| A3 F-09 回退链 | `SpriteRefTable`（帧＋单图）＋`PlaceholderSprites`（占位）＋`portrait_{race}_{occ}` 键在场 ✅ | 三级链逐级降级**逐级读数**（状态缺→fallback 态；整套缺→静态立绘；无立绘→占位）✅ |
| A4 F-13 LOD | `LODSystem.Instance`／`GetLevelAt`（`LODSystem.cs:378`）／`LodLevel` 三档（`MidChunkLodState.cs:11`）在场 ✅ | 需世界中区块**真实登记档位**：正门进局后 `GetLevelAt` 对同点多次查询返回 `Active/SemiActive/Dormant` 至少两档（否则读数不可信）⇒ 探针**记录实际返回值** |
| A5 F-15 钩子 | `Action<UnitController,int>` 类型在场 ✅ | 只预留：探针订阅后**观察回调被调**且**游戏逻辑零变化**（红线 4）✅ |
| B P4 工人 Working | `TaskScheduler.GetWorkerState`／`TaskState.Working`／`FaceAndShowWorking`（`TaskScheduler.cs:536`）在场 ✅；工人派工链已落（HH.257/261）✅ | 需正门进局后工人**真实进入 Working**（`GetWorkerState==Working` 为判据），非人工造态 ✅ |
| B P5 death | `Die()` 在场 ✅；**`_death` 帧素材缺** ⚠️ | 以 fallback 链验证；真帧素材补产另批 |
| C 300~400 单位 | `UnitFactory.SpawnUnit(faction,occ,pos,kingdomId)` 在场 ✅；`LODSystem`/`Animation` 无规模前置 | 需建局后成功 spawn ≥300 且**帧率达标**（对齐 0_总计划 §九⑥）⇒ 记录 `Time.deltaTime` 与 Profiler 段读数 |
| D 图集 | `com.unity.feature.2d` 2.0.1 在场（含 `com.unity.2d.sprite` ⇒ `SpriteAtlas` 可用）✅ | packing 后 ①引用不失效 ②回退链不破 ③pivot 稳定 三回归 ✅；DrawCall 前后对照需**同一容器两次采样** |

---

## 四、四段推进序（分段交付·分段验收）

```
A 段（播放器补完）→ B 段（P1~P8 行为探针）→ [交付①：A+B 段报告] → C 段（H5 压测）→ D 段（H6 图集）→ [交付②：C+D 段报告]
```
- **A→B 顺序不可换**：P1~P8 里 P4/P5/P7/P8 直接考 A 段新功能（fallback 链/LOD/回调），A 未落则探针无对象。
- **C/D 可后交**：与 A/B 无耦合（C 只测 Driver 稳态；D 只测图集）。
- 每段：`refresh_unity(compile=request)` → console error **0** → 探针读数 → 不达标即就地修（不为过线凑数·`L-30`）。
- 收尾：`ExitTestRun`＋`QuitSmoke`＋**退 Play**；commit 只含本批文件·不 push。

---

## 五、红线自检（开工时）

- ❌ 不给 `AmmoDef`/`GroundEffectDef` 加字段（D707 R4 继承）；❌ 不另建 LOD 系统（接 `LODSystem`）；❌ F-15 不接游戏逻辑；❌ 不采规格 §八 否决清单（Animator／Burst／GPU 动画／分桶分片／上下半身分层）。
- `AI.Core` **零触**；`_任务队列.md`（CRLF-blob）**不写**；改前重读磁盘；共享文档增量改。

> 执行端（TraeCode）｜2026-09-13｜HH.265｜本回执＝HH.264 开工前置（M1~M6 自答＋gap 两列＋推进序）；**本批无请裁项 ⇒ 落盘后直接进入 A 段施工**。
