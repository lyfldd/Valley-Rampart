# BUGFIX：进入游戏时 WorldSystem 找不到 + BuildingFactory 30 条空引用连发

- **日期**：2026-07-29
- **触发场景**：MainMenu → 选存档（读档恢复）→ Loading → Ready → 继续游戏，进入 SampleScene
- **报错日志（原始）**：

```
[LoadManager] 场景中未找到 WorldSystem！
   UnityEngine.Debug:LogError(object)
   LoadManager:Awake() (at Assets/_Game/Systems/Loading/LoadManager.cs:47)

[SaveManager] 模块 WorldManager LoadState 失败: System.NullReferenceException: Object reference not set to an instance of an object
   at BuildingFactory.CreateBuilding(BuildingPlaceholder ph, Region region) [0x00111] in ...\BuildingFactory.cs:109
   at BuildingFactory.InstantiateFromMap(MapData map) [0x00087] in ...\BuildingFactory.cs:46
   at WorldManager.GenerateWorld(Int32 worldSeed, WorldSize size, Int32 difficulty) [0x00095] in ...\WorldManager.cs:110
   at WorldManager.LoadState(SavePayload payload) [0x000c0] in ...\WorldManager.cs:649
   ... (SaveManager / LoadManager / GameBootstrap 分发链路)
```

---

## 报错 1：`[LoadManager] 场景中未找到 WorldSystem！`

### 现象
- LoadManager.Awake 时 `FindObjectOfType<WorldSystem>()` 返回 null → 打 Error 日志
- 但因为 Singleton 会在 Instance getter 里自创建，后续功能不一定受影响；不过这条 Error 级别日志本身就是问题（不该在正常流程出现）

### 根因
- SampleScene 里只创建了 `GridSystem`、`BuildingSystems`、`UnitDataManager` 等对象，**遗漏了 `WorldSystem` GameObject**
- 代码层面 LoadManager 在 Awake 里强依赖 `FindObjectOfType<WorldSystem>()`，不允许在自创建分支通过

### 修复
- **场景资产修复**（Unity Editor 操作）：在 SampleScene 根节点新建 GameObject 命名 `WorldSystem`，并挂载 `WorldSystem` 组件，确保 Awake 时 FindObjectOfType 能拿到
- （治本选项，暂未做）：LoadManager.Awake 找不到时降级为 Warning + 走 Singleton.Instance 自创建，避免"场景里必须有一个对象才能不报错"的强耦合

---

## 报错 2：`BuildingFactory.CreateBuilding` 连发 30 条 NullReferenceException（每种资源 BuildingPlaceholder 一条）

### 现象
- SampleScene 地图生成 `[MapVisualizer] 可视化完成: 15 个大区块, 资源点 43 个, 裂隙 1 个` 之后，立刻出现 30 条错误
- 错误类型整齐划一：全部是 `[BuildingFactory] ... Object reference not set to an instance of an object`，类型覆盖 StonePile / OreVein / Mine / Farmland / Tree / WoodPile / TreasureBox / Rift / CastleCore，无一幸免
- **最开始的诊断误导**：NullReference 位置在 InitFromPlaceholder 内部，但 InitFromPlaceholder 内部全是平凡字段赋值（def/coord/cellWidth/faction/grade/level/HP 计算），插桩 DBG 日志后发现 Unity 根本没跑到 DBG 首行——说明 NRE 发生在调用 InitFromPlaceholder 之前，但被 catch 的统一消息包装成了 "InitFromPlaceholder 失败"，一开始误判

### 关键线索（隐藏在报错前一行）
在 BuildingFactory 的 Error 日志**上一行**其实有一条 Unity 原生警告（不显眼，早期被忽略了）：
```
Adding component failed. Add required component of type 'BoxCollider2D' or 'CapsuleCollider2D' or 'CircleCollider2D' or 'CompositeCollider2D' or 'CustomCollider2D' or 'EdgeCollider2D' or 'PolygonCollider2D' or 'TilemapCollider2D' to the game object 'Building_StonePile_37' first.
```

### 真正根因（决定性 Bug）
`Building.cs` 类顶部写了：
```csharp
[RequireComponent(typeof(Collider2D))]
public class Building : MonoBehaviour, IInteractable { ... }
```

而 `BuildingFactory.CreateBuilding()` 的流程是：
1. `go = new GameObject($"Building_{ph.type}_{globalCellX}");` —— **空壳 GameObject，没有任何组件**
2. `b = go.AddComponent<Building>();` —— Unity 看到 `[RequireComponent(typeof(Collider2D))]`，尝试**自动给 go 补加一个 Collider2D 组件**

问题在于：**`Collider2D` 是抽象类，Unity 无法实例化抽象类型**。它不知道你要加 BoxCollider2D / CircleCollider2D / CapsuleCollider2D / ... 哪一个具体子类。

所以 Unity 的行为是：
- 拒绝添加 Collider2D（抽象类实例化失败）
- **连带 Building 组件也不添加**（因为 RequireComponent 依赖没满足）
- `go.AddComponent<Building>()` **返回 `null`**
- 随后任何 `b.def = def` / `b.coord = coord` 等字段赋值 → **NullReferenceException**

因为地图上有 30 个 BuildingPlaceholder，所以就 NRE × 30。

### 为什么一开始难以定位？
1. 30 条错误用 try/catch 包了一层，输出的是"InitFromPlaceholder 失败：type=xx err=NullReferenceException"，把真正发生在 `b.def = def`（或内联初始化第一行）的 NRE 位置信息吞掉了，只保留 NRE 文本没保留堆栈
2. Unity 那条 `Adding component failed...` 警告在 BuildingFactory.Error 之前输出，初筛时被当成无关警告忽略
3. Building 类的字段全是普通值类型/引用类型赋值，没有 getter 链，代码审查以为内部绝对不会炸，排查走入了"是不是 InitFromPlaceholder 内部访问了某个静态单例"的歧途

### 修复
1. **[Building.cs](file:///D:/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L10)**：删除类顶部的 `[RequireComponent(typeof(Collider2D))]` 注解
   - 原因：Collider2D 是抽象类，Requiring 抽象组件本身就是错的；需要 Collider 的地方（BuildingFactory.CreateBuilding 和 BuildController.Place）都是手动显式 `AddComponent<BoxCollider2D>()`
   - 修正后反射验证：`Building.GetCustomAttributes(typeof(RequireComponent), true).Length == 0` ✅

2. **[BuildingFactory.cs](file:///D:/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs#L115-L126)**：`AddComponent<Building>()` 后加断言式空值检查
   ```csharp
   var b = go.GetComponent<Building>();
   if (b == null) {
       b = go.AddComponent<Building>();
       if (b == null) {
           Debug.LogError($"[BuildingFactory] 添加 Building 组件失败！type={ph.type}, go={go.name}");
           Object.DestroyImmediate(go);
           return false;
       }
   }
   ```
   万一将来又引入了 RequireComponent(抽象类) 之类的问题，会直接报错 "添加 Building 组件失败"，而不是在后续赋值处 NRE 误导到下游。

3. **[BuildingFactory.cs](file:///D:/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs#L128-L158)**：Building 字段初始化改走内联赋值（不再调用 `b.InitFromPlaceholder(def, ph, coord)`），HP 计算独立 try/catch → 失败降级为 100
   - 目的：绕开 InitFromPlaceholder 内部可能引入的单例时序问题；同时把 HP/gradeScale 等可能出问题的点独立降级
   - InitFromPlaceholder 方法保留以兼容手动调用入口，但 BuildingFactory 不再走此路径

### 校验结果
- 重启 Unity 后重新 Play Mode + 继续游戏 → 上述两类报错完全消失
- 控制台不再出现 `Adding component failed. Add required component ... first.` 警告
- Building 注册/占用占用标记链路正常（GridSystem.MarkOccupiedFootprint / BuildingRegistry.Register / EventBus.Publish<BuildingPlacedEvent>）
- 代码反射校验：Building 类 RequireComponent 数量 = 0；def、cellWidth 等字段存在

---

## 教训 & 编码规范建议

1. **`[RequireComponent(T)]` 严禁写抽象类**：Collider2D / Renderer / Component 等抽象基类一律不能用于 RequireComponent，必须具体到 BoxCollider2D / SpriteRenderer 等实现类
2. **`AddComponent<T>()` 返回值必须做 null 检查**：当 T 被 RequireComponent(抽象类) 注解时 Unity 会静默返回 null——这是 Unity 一个不抛异常、只打 Warning 的坑点；关键路径一律判空
3. **日志 catch 块必须打全堆栈（`ex.ToString()`）**：只打 `ex.Message` 会丢失 `at xxx.cs:行号` 信息，上游排错直接失去精准定位能力；本次修复后所有 BuildingFactory catch 块都用 `$"err={ex}"` 格式
4. **Unity 原生警告不能轻易忽略**：`Adding component failed.` 这种 Unity 引擎层警告通常是后续 NRE 的直接上游，比业务层 Error 日志更接近根因
