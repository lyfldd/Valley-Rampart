# 美术资源接入 · SpriteAnimator 播放器设计规格

> 日期：2026-09-12
> 状态：⚠️ 设计规格定稿（域内待实施）——架构定档＝**全配档**（用户 2026-09-12 两问全准）
> 归属：**策划端单写者**（美术接入域，D689 授权代理）；执行端施工引用只读。
> 依据：[美术资源接入计划](./美术资源接入计划.md) §七.2（路线 A·D689）· [HH.239 任务书](../../多Agent交接/策划端/HH.239_美术资源接入批_任务书.md) §8.4 **H5/H6**（全配定档＋图集）· [映射表](./美术资源接入映射表.md) §六/§八（批5 规格/导入规范）· D568（批5 作业单/万能编辑转侧面）· D602（坐骑系 run 例外）· 3.5.2（工人工作动作）
> 施工归：**HH.239 T13**（Gate=`G3-1`）；切帧配套＝T5；图集配套＝H6。
> 编号说明：非 3.x 正式文档，属 `改造计划/` 下美术接入域施工配套（同《美术资源接入计划》先例，不编号）。

---

## 〇、一句话定位

**小电机，不是汽车引擎**：帧序列动画运行时播放器＝"给定状态 → 循环播帧 ＋ flipX 补左向"。不含混合/骨骼/状态机可视化/IK——这些是 Animator 为骨骼动画造的机器，帧动画**物理上不存在混合语义**（"半张走路帧"不存在），全部明确不做（§八）。

## 一、为什么自研（裁决记录）

### 1.1 成本结构对比（400 单位量级，P1 目标＝活跃带 300~400 单位帧率达标）

| 维度 | Unity Animator + AnimationClip | 自研 SpriteAnimator |
|---|---|---|
| 每帧 CPU | 每单位一条 PlayableGraph：图遍历→状态机求值→曲线采样→写 sprite 四层；数百个＝毫秒级 | 集中管理器遍历结构体数组：400 次 float 加法＋索引比较 ≈ **0.02~0.05ms**；sprite 写仅换帧时发生（12fps＝80 次/帧均值） |
| 内存 | 每单位 Animator＋Controller 运行时实例＋PlayableGraph；编辑期生成 ~180 个 clip 资产（46 角色×3~4 状态） | `Sprite[]` 共享引用数组，**零新增资产**（帧图由导入器切出） |
| 状态源 | 大脑状态→Animator 参数→状态机跳转＝**第二套状态源**，与 NPCBrain/UnitController 有失同步风险 | 大脑直调 `SetState()`，状态源唯一 |
| 池复位 | Rebind/重置状态机，易残留 | 清 timer/index/state 即可（AGENTS.md 复用复位） |
| 可用性 | 核心价值（混合/IK/Layer）帧动画**用不到**＝白付税 | 功能全部对着本项目需求（§四） |

### 1.2 项目性能账本（大量 NPC 的真实分账）

| 层 | 量级 | 现状/归属 |
|---|---|---|
| AI 决策（think/attention） | **最大头**，毫秒级 | **已解决**：`LODSystem.cs:23` 按 `LodLevel` 给 thinkHz 分档降频（`NPCBrain.cs:502-506` 实锚） |
| 动画推进（本组件） | 0.02~0.05ms | 本规格，架构选对即闭环 |
| **换帧→批处理断裂** | 混编单位各自 sheet 独立纹理 ⇒ dynamic batching 按纹理断裂，draw call 随可见纹理数上涨 | **真风险**，解药＝**SpriteAtlas 图集（H6，本批纳入）** |
| 物理/寻路/碰撞 | 既有系统 | 与本组件无关 |

## 二、架构（全配档）

### 2.1 数据流

```
NPCBrain / UnitController（状态源·唯一）
    │ SetState(animState) / SetFlip(facing) / SetSpeed(scale)
    ▼
SpriteAnimatorSystem（管理器·Singleton·集中 Update·ProjectileManager 同款）
    ├─ AnimRecord[] 平铺结构体数组（timer/frame/fps/framesRef/playMode/speedScale/lod/visible）
    ├─ 帧数组注册表： (raceId, occId, state) → Sprite[]   共享缓存（SpriteRefTable 取帧）
    └─ 遍历：累计 scaled deltaTime → 帧推进 → 脏写（帧变才写 sprite 属性）
    ▼
SpriteRenderer（flipX 朝向；同 sheet 子帧同尺寸同 pivot ⇒ 换帧 bounds 不变 ⇒ 排序稳定）
```

### 2.2 职责分工

| 组件 | 职责 | 不做 |
|---|---|---|
| UnitController/NPCBrain | 状态源：行为态→动画态映射（§五）；朝向；回调消费 | 不算帧、不管播放 |
| SpriteAnimatorSystem | 集中推进/注册表/LOD 分层/回退/脏写 | 不做决策、不反向驱动逻辑（红线） |
| SpriteRefTable（T6） | artId→Sprite 帧源；缺图回退链上游 | — |
| SpriteAtlas（H6） | Units 全帧入集，保批处理 | — |

## 三、数据结构（建议草案）

> 执行端可按实现微调字段组织，**但不得违反 §六纪律与 §四功能口径**。

```csharp
public enum AnimState { Idle, Walk, Attack, Run, Loot, Death }   // int 枚举；扩展=尾插
public enum PlayMode { Loop, OnceReturn, OnceHold }              // 循环 / 单次回落 / 单次定格

// 管理器平铺数组元素（SOA/AOS 皆可，取连续内存）
struct AnimRecord {
    int unitId;  SpriteRenderer renderer;
    AnimState current;  PlayMode mode;
    Sprite[] frames;                  // ← 注册表共享引用（禁逐单位复制）
    float timer;  int frame;
    float fps;  float speedScale;     // fps 来自 SO；speedScale=局部变速
    int  lodTier;  bool visible;
    System.Action onOnceComplete;    // 完成回调（P0）
}
```

**管理器 API**（表面）：
- `Register(unit, renderer, raceId, occId)` / `Unregister(unit)` —— **O(1)**（swap-remove 数组，禁 `List.Remove`）；单位 `OnDestroy` 必注销（防泄漏）。
- `SetState(unit, AnimState, PlayMode? override=null)` —— 同状态重入＝**打断重播**（one-shot 从第 0 帧重开）。
- `SetFlip(unit, facingLeft)` / `SetSpeed(unit, scale)`。
- `SubscribeComplete(unit, Action)` —— death 播完→衔接既有死亡流程；attack 单次→回落默认态。
- 预留：`OnFrame(unit, index)` 帧事件钩子（**只预留不接逻辑**，表现禁驱动逻辑）。

**SO 配置**（`SpriteAnimatorConfig`，遵 so-data-driven）：
| 字段 | 出厂 | 说明 |
|---|---|---|
| `defaultFps` | 12 | 批5 规格（D259/D568） |
| `fpsByState` | 12 全态 | per-state 可覆写 |
| `attackSpeedScale` | 1.0 | 可选：对齐战斗 CD |
| `runAsWalkSpeedScale` | 1.5 | 非坐骑 run＝walk 加速（D568「run 并 walk 代码加速」） |
| `lodFpsScale` | 近 1.0 / 远 0.5 / 冻结档 0 | 对接既有 `LodLevel`（禁另建 LOD 系统） |
| `stateFallback` | Attack→Idle / Run→Walk / Loot→Idle / … | 缺状态回退表 |

## 四、功能规格（F-01~F-16）

| # | 功能 | 口径 | 依据 |
|---|---|---|---|
| F-01 | 状态注册表 | per `(race,occ,state)` 共享缓存 `Sprite[]`（400 单位共享 46 套 sheet，数组只建一份）；状态集**可变**（普通 3 态/坐骑系 4 态含 run〔D602〕/小孩 2 态/怪物含 loot〔HH.233〕） | 映射表 §六 |
| F-02 | **playMode 三态** | `Loop` 循环 / `OnceReturn` 单次→回落默认态 / `OnceHold` 单次→定格末帧。⚠️ **工人 attack＝工作循环（Loop）**，战斗 attack＝OnceReturn，death＝OnceHold——三态必须分清（复审勘正 K1） | 3.5.2 工人动作 |
| F-03 | 循环播放 | idle/walk/run 回绕 | 批5 |
| F-04 | **单次＋完成回调（P0）** | OnceReturn/OnceHold 播完触发回调（death→既有死亡流程衔接；attack→回落） | 复审勘正 K2 |
| F-05 | 打断重播 | 同状态 SetState→one-shot 从第 0 帧重开（attack CD 1s < 动画 1.33s 场景） | 复审新增 |
| F-06 | **变速三轴** | ①全局＝`Time.deltaTime`（自动随倍速 0.5~3x 与 timeScale 冻结，免写）；②局部 speedScale（run=walk 加速）；③per-state fps SO 可配 | D568 |
| F-07 | flipX 两向 | 只画朝右，左向＝`SpriteRenderer.flipX`；pivot=底面中心 ⇒ 翻转不错位 | D689 两向 |
| F-08 | 随机起始相位 | **仅 Loop 态**进场随机起始帧（防 400 单位全场齐步走）；one-shot 不随机 | 复审新增 |
| F-09 | 缺图回退链 | 状态缺→`stateFallback` 回退态（Attack→Idle…）→静态立绘→占位；**负查询缓存＋一次性告警**（禁逐帧刷日志） | 计划 §九.2 |
| F-10 | icon 态排除 | `icon.png`＝UI/立绘用，不入运行时状态集 | HH.233 |
| F-11 | O(1) 注册/注销 | swap-remove＋OnDestroy 必注销 | 复审新增 |
| F-12 | 不可见注销 | `OnBecameInvisible` 移出活跃表 / `OnBecameVisible` 回表 | H5 |
| F-13 | **LOD 动画分层** | 查本单位**既有** `LodLevel` 档位：近＝全速／远＝降半频／冻结档＝静态帧。禁另建 LOD 系统 | `LODSystem.cs:23`/`MidChunkLodState.cs:11`/`NPCBrain.cs:502` 先例 |
| F-14 | 池复位 | 复用回收时 ResetState：current=Idle、timer=0、frame=随机、回调清空 | AGENTS.md |
| F-15 | 帧事件钩子 | `OnFrame(index)` **预留**；本批不接游戏逻辑（表现禁驱动逻辑红线） | 复审预留 |
| F-16 | 统一 pivot 双重收益 | 同 sheet 子帧同尺寸同 pivot ⇒ 换帧不改 bounds ⇒ **排序稳定不重算**（切帧坑②的第二重价值，写进验收理由） | 计划 §七.1 |

## 五、接线映射表（建议稿）

> ⚠️ 执行端开工回执须**复核 UnitController/NPCBrain 实际状态枚举**后列报定稿；本表为策划侧建议。

| 游戏态（UnitController/NPCBrain） | 动画态 | playMode | 依据 |
|---|---|---|---|
| Idle/待机 | idle | Loop | — |
| 移动·非坐骑 | walk | Loop | run＝walk×speedScale（D568） |
| 移动·坐骑系 3 单位（战马骑士/狼骑/鹿骑） | run | Loop | D602 独立 run 16 帧 |
| 战斗攻击（每次攻击事件） | attack | OnceReturn | CD 驱动周期；播完回落 idle/walk |
| 工人 Working（采集/建造/农活/交互） | attack | **Loop** | 3.5.2 工作循环；sheet=共用挥舞（D568） |
| 死亡 | death | OnceHold | 4 帧倒下模板全族复用；播完→既有死亡流程 |
| 怪物 loot | loot | 待接线侦察定 | HH.233 交付；触发方归 2_14 域，缺触发则不播 |
| 搬运携带态 | （不属于本组件） | — | 独立子 SpriteRenderer＋8 搬运物贴图（3.1.2 §8.3） |
| 受击 | （不做） | — | DamageFeedback 既有闪红 tint（D568） |

## 六、实现纪律（硬）

1. **零 GC**：遍历路径零分配（无 LINQ/无闭包/无字符串拼接）；帧数组注册表一次构建。
2. **枚举状态**：`AnimState` int 枚举，**禁每帧字符串比较**。
3. **脏写跳过**：帧索引未变不写 `sprite` 属性。
4. **O(1) 生死**：swap-remove；OnDestroy 必注销。
5. **静默回退**：负查询缓存＋一次性告警（对齐观察器日志纪律，L-05 同族）。
6. **表现单向**：本组件只消费状态，**禁反向驱动逻辑**（伤害/弹药生成归战斗系统；F-15 钩子本批不接）。
7. **scaled time**：一律 `Time.deltaTime`（判负冻结 timeScale=0 自动停；倍速自动跟随）。
8. **池复位**：回收清态（F-14）。

## 七、性能预算与验收

### 7.1 预算表

| 项 | 预算 | 判法 |
|---|---|---|
| 推进（400 单位） | **<0.1ms/帧** | Profiler 截图（SpriteAnimatorSystem.Update 段） |
| sprite 写 | ~80 次/帧均值（400×12fps/60） | 隐含于脏写纪律 |
| GC alloc | **0 B/帧**（稳态） | Profiler GC Alloc 列 |
| Draw call | 四族混编不随族数线性上涨 | SpriteAtlas（H6）前后对照 |

### 7.2 行为级探针（HH.239 交付报告必含，P1~P8）

| # | 探针 | 判据 |
|---|---|---|
| P1 | idle 循环 | 8 帧回绕（末格空不播）；随机相位生效（两单位起始帧不同） |
| P2 | walk＋flipX | 左右移动动画正确翻转、无抖动/错帧（对齐计划 §九.4） |
| P3 | 战斗 attack 单次 | 播完回落 idle/walk（OnceReturn） |
| P4 | 工人工作 attack 循环 | Working 期间持续循环**不回落**（playMode 三态行为级分离） |
| P5 | death 定格＋回调 | 播完停末帧＋onOnceComplete 触发（衔接既有死亡流程） |
| P6 | 打断重播 | attack 中再触发→帧回 0 重开 |
| P7 | 缺图回退 | 临时移走某 artId 帧→回退态渲染不崩＋一次性告警（禁刷屏） |
| P8 | LOD/不可见 | 远档降半频或冻结（读数/计数证据）；不可见移出活跃表 |

### 7.3 配套验收

- **切 9 播 8**：idle/loot 网格 9 格、播放器按有效帧数 8 配置（计划 §七.1 特例③）。
- **SpriteAtlas（H6）**：Units 全帧（含 monster）入集；`SpriteRefTable` 直接引用入集后 sprite 不失效；2_20B 四族混编容器 DrawCall 对照。
- **帧率红线**：300~400 单位场景帧率达标（对齐 0_总计划 §九⑥）。

## 八、否决清单（钉死，防自由发挥）

| 档 | 否决理由 | 重启条件 |
|---|---|---|
| 分桶时间分片（N 桶×降频遍历） | 收益 <0.01ms，纯复杂度 | 无 |
| Burst/Jobs | sprite 属性写必须回主线程；400 单位不值得 | 无 |
| GPU/Shader 动画（图集+shader 换 UV，零属性写） | 破坏三件既有资产：SpriteRefTable 间接层、缺图回退链、统一 pivot 排序稳定 | **远期备选**：P1 帧率红线不达标才议 |
| Animator/AnimationClip（Unity 原生） | §一全部；核心价值（混合）帧动画用不到 | 无 |
| 上下半身分层/crossfade | 帧动画无混合语义 | 无 |

## 九、关联

- 施工载体：HH.239 任务书 T13（§8.4 H5＝本规格验收口径）＋T5（切帧）＋H6（图集）
- 上游：[美术资源接入计划](./美术资源接入计划.md) §七（导入/切帧/播放）；[映射表](./美术资源接入映射表.md) §六（批5 动画产物）/§八（导入规范）
- 机制依据：D568（批5 作业单/run 并 walk）/D602（坐骑 run 例外）/D259（序列帧口径）·3.5.2（工人动作）/3.1.2 §8（武器坐骑烘焙/icon）
- 性能锚：`LODSystem.cs:23`（Singleton）·`MidChunkLodState.cs:11`（`LodLevel`）·`NPCBrain.cs:502-506`（thinkHz 分档先例）

---

> 版本：2026-09-12 初稿（美术接入域策划整理自两轮功能复审＋用户全配定档；散载收拢＝HH.239 §8.4 H5 ＋ 计划 §七.2 → 本规格为 T13 唯一实现规格）。

