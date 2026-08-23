# 河谷防线 · Agent 开发准则（生命周期方法论）

> **本文件是 Agent 在本仓库开发时**常驻遵循的最低约束**，源文档《设计方法论_生命周期工作流.md》。
> 你做的任何系统、UI、实体、服务都要过这套准则；它不给你"答案"，只给你**必须回答的问题**和**回答的骨架**。
> 下面是已按本 Unity 项目（C# / MonoBehaviour / ScriptableObject）填好决策点的版本——直接执行，不再让你每次重新填空。

---

## 〇、底层骨架（不变地基，与任何模块无关）

### 两条铁律
- **铁律一：一切对象皆有生命周期，且只有四个阶段。**
  | 阶段 | 本项目的承载 | 语义 |
  |------|--------------|------|
  | OnBirth | Awake 前初始化 / 工厂创建 | 获得标识、读配置、声明依赖、订阅消息、注册进归属者 |
  | OnActivate | OnEnable / Start | 开始接收、开始被驱动（tick/刷新/渲染） |
  | OnDeactivate | OnDisable | 停止接收、停止被驱动，但**不释放资源**（必须无损恢复到 Activate） |
  | OnDeath | OnDestroy / Die() | 退订全部消息、从归属者注销、清算引用、释放资源 |
- **铁律二：所有权闭环。** 谁创建谁回收；有订阅必有退订、有注册必有注销——成对出现。初始化自底向上（被依赖者先就绪），销毁自顶向下（**严格按初始化的反序**）。

**项目已有对应设施（直接用）**：
- 事件通信：[EventBus.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Core/EventBus.cs)（static，struct 事件）。用 `EventBus.Subscribe<T>` / `EventBus.Unsubscribe<T>` / `EventBus.Publish<T>`。
- 全局服务：[Singleton.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Core/Singleton.cs)（所有 Manager 继承；子类 override `OnDestroy` **必须调 `base.OnDestroy()`**，否则静态引用残留 / fake null）。**禁止在 OnDestroy 里访问其他可能已销毁的服务。**
- 启动顺序：[CoreBootstrap.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Core/CoreBootstrap.cs)（`[-200]` 创建 Core 单例，业务层此后推进）。新增全局服务要按依赖顺序找准插入点。

### 三条推论
1. **反向空指针**：任何使用点都必须保证对象已诞生——"代码写好了但资源没接上"与"只有开始没有结束"是同一病的两种症状。
2. **可重入性**：第二次 OnBirth 必须等价于第一次。对象来自池/缓存复用时（如 `ReturnBuildingToPool`），OnDeath 必须把它复位到"从未活过"的状态。
3. **死亡 ≠ 消失**：终结态本身可以是状态（废墟、遗产）。OnDeath 是"转换到终结态"，不一定是"从世界上抹掉"。

### 顺序表（本项目已定）
```
Core 单例(GameStateManager/InputManager/SaveManager/WorldManager) → EventBus(static 已就绪)
→ 业务 Manager → 世界/空间(GridSystem/WorldManager) → 表现层(UI)
```
销毁顺序是该表的严格反序。**销毁侧只做"退订/注销/释放"，不做"查询别人"。**

### 八格检查表（每个系统/实体过一遍，空格 = 下一个 bug）
| # | 环节 | 必答问题 |
|---|------|----------|
| 1 | 诞生 | 触发源唯一吗？谁负责创建它？ |
| 2 | 归属 | 统一管理它的是谁（注册表/管理器/父对象）？ |
| 3 | 更新 | 谁驱动它？降频/休眠时逻辑还推进吗？ |
| 4 | 挂起/恢复 | 被暂停/打断/切走时处于什么状态？能恢复吗？ |
| 5 | 正常终结 | "完成/死亡/关闭"的判定条件是什么？ |
| 6 | 异常终结 | 中途被打断的路径走通了吗？（取消/覆盖/强制退出） |
| 7 | 清算 | 消息退订了吗？还引用它的人被通知了吗？资源归谁？ |
| 8 | 持久化 | 状态（含终结态）要保存吗？版本变了怎么迁移？ |

**反向三问**（每个使用点自检）：它诞生了吗？怎么死？谁还拿着它？

---

## 一、EventBus / 事件总线（模块解耦通信）
- **嵌入生命周期**：OnBirth 订阅 → OnActivate 接收 → OnDeactivate 临时退订 → **OnDeath 强制退订全部（重中之重，已死对象收到回调是最经典的崩溃源）**。
- **附属纪律**：订阅与退订成对（入口订、出口退）；发布侧也守生命周期（无消费者不广播，防洪泛）；消息只带数据，不带"可调用对象"。
- **已实现**：`EventBus.Subscribe<T>/Unsubscribe<T>/Publish<T>`。事件必须是 `struct`。
- **给你**：列出该系统该订阅/发布哪些消息；生成成对的订阅/退订代码；**扫描"订了没退"的位置**；输出"谁发送/谁监听"对照。

## 二、简易 MVC（界面专用）
- 一个面板 = 一个独立生命周期实体。Model 备数据；View 绑定控件 + Activate 时刷新一次 + Deactivate 隐藏 + Death 解绑全部回调/清引用；Controller 粘合 View↔Model。
- **纪律**：View/Controller 只读数据、只发消息，**不直接改游戏状态**。
- **打开/切走/关闭是三种不同的终结深度**：打开=Birth+Activate，切走=Deactivate（隐藏保留），关闭=Death（解绑清引用）。混用会导致残留回调污染下一个面板。

## 三、轻量 ECS（大批量实体专用）
- **不做全局 ECS**，只给"数量巨大的同类动态实体"用（子弹、成群小兵）；单例级、决策重的对象不适合。
- Entity 仅是 ID；Component 纯数据无生命周期；System 走路批更新。
- **给你**：判断哪些对象适合进 ECS、哪些不适合；拆实体/组件/System 并生成基础代码。

## 四、FSM 有限状态机（行为组织）
- **分工**：生命周期管"状态机能不能跑"，状态机管"活着时干什么"。OnDeath 强制退出当前状态 + 清缓存 + 销毁状态机。
- 参考范例：[Building.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L5-L14) 的 `BuildingState` 枚举（Placing/Constructing/Active/Dead/Abandoned/Ruined）。
- **给你（收益最大）**：① 状态全列表；② 每个状态切换条件；③ 状态流转图；④ 代码模板；⑤ **死状态检查**（进得去出不来的状态、无 Enter/Exit 清理的状态）。

## 五、ServiceLocator / 全局服务
- 本项目就是 [Singleton<T>](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Core/Singleton.cs) 容器。每个全局服务是顶层生命周期实体，插入顺序表。
- OnBirth=初始化+登记；OnDeath=从容器注销+释放资源，**禁止此时访问其他服务**。
- **给你**：排初始化/销毁顺序（顺序错乱=空指针，最大风险点）；**检测循环依赖**。

## 六、数据驱动（配置外置）
- **已实现**：ScriptableObject 放在 `Assets/Resources/Config/` 与 `Assets/Resources/Buildings/` 等；运行时经 `Resources.Load<T>("Config/...")` 读取，不写代码时间；Magic number 必须外置。
- **纪律**：实体 OnBirth 按标识读配置初始化；实体 OnDeath 不碰配置；同一参数**唯一真源**，其它皆派生；多载体必须显式同步规则。
- **给你**：配置结构定义、字段文档、**全仓扫描硬编码数值并提醒外置**、多载体同步清单。

---

## 七、统一 AI 工作流模板（开发新模块时按此提交）

```
我的底层框架规则（本仓库已定，直接套用）：
- 一切对象走四阶段生命周期（本项目映射见 §〇）。
- 所有权闭环：谁创建谁销毁；初始化自底向上，销毁严格反序。
- 事件用 EventBus（struct 事件，订阅退订成对）。
- 全局服务用 Singleton<T>，OnDestroy 调 base + 不访问其他服务。
- 配置一律 ScriptableObject 外置，不写 Magic number。

现在需要开发【模块名】。请完成：
1. 判断适合嵌入哪几种工具（EventBus/MVC/ECS/FSM/ServiceLocator/数据驱动），说明理由
2. 四阶段各做什么（必须含异常终结路径；若会被复用，说明 OnDeath 如何复位）
3. 依赖哪些全局服务 + 在初始化/销毁顺序表中的位置
4. 发布/订阅哪些消息（谁发送、谁监听）
5. 复杂行为：状态机（状态/切换条件/流转图/死状态检查）
6. 全部参数及配置归属（唯一真源、是否需要多载体同步）
7. 输出最小可运行基础模板，不填充复杂业务
```

## 八、验收闭环
新模块交付前，**回填八格检查表（§〇）**，空格清零才算完成。任何"有生无死/有死无生"的格子 = 直接指出，不放过。