# 君主系统 Bug 修复记录

> 日期：2026-07-21  
> 涉及文件：`RulerController.cs`、`GameBootstrap.cs`、`SaveManager.cs`

---

## 背景

`RulerController`（Singleton 全局管理器）被错误地挂载在 `Human_Player_Ruler.prefab` 上。该 Prefab 同时包含 `UnitController`、`PlayerInputHandler` 等运行时必需的组件。当 Prefab 被 `UnitFactory.SpawnUnit()` 实例化时，Singleton 冲突导致一系列连锁 Bug。

---

## Bug ①：君主数据丢失，移动系统失效

### 现象

```
[PlayerInputHandler] Move: input=(1.00, 0.00), run=True, unit=True, data=False
```

`unit=True` 但 `data=False`——UnitController 存在，其 `Data` 属性为 null，`Move()` 方法第一行就 return 了。

### 根因

```
UnitFactory.SpawnUnit()
  └─ Instantiate(Human_Player_Ruler.prefab)
       └─ RulerController.Awake()
            └─ Singleton<RulerController>.Awake()
                 └─ _instance 已存在（之前访问 RulerController.Instance 时自动创建的）
                      └─ Destroy(gameObject) ← 销毁了整个君主 GameObject！
                          包括 UnitController、PlayerInputHandler 全部被销毁
```

### 修复

**文件**：`RulerController.cs` — `Awake()` 方法

不再调用 `base.Awake()`（它会在重复检测时 `Destroy(gameObject)`），改为手动处理 Singleton 逻辑：

```csharp
if (_instance != null && _instance != this)
{
    // 仅移除 RulerController 自身，保留同一 GameObject 上的 UnitController 等
    Destroy(this);
    return;  // _isRealSingleton 保持 false，OnDestroy 不做多余清理
}

_instance = this;
_isRealSingleton = true;
DontDestroyOnLoad(gameObject);
// ... 订阅事件、注册 ISaveable
```

| 修改前 | 修改后 |
|--------|--------|
| `Destroy(gameObject)` — 销毁整个 Prefab 实例 | `Destroy(this)` — 仅移除 RulerController 组件 |

---

## Bug ②：修改后出现两个君主（新建游戏）

### 现象

开始游戏后有两个君主：场景中预置的一个（可控），另一个接收第一个指令后行为异常。

### 根因

修复 Bug① 后，Prefab 实例的 `UnitController` + `PlayerInputHandler` 存活了。但 `SpawnMonarch()` 的防重复逻辑不够健壮：

```
场景中已有 Prefab 实例（Data=null, 有 PlayerInputHandler）
         +
FindExistingMonarch() 在某些条件下找不到它
         ↓
SpawnUnit() 又 Instantiate 一个 → 两个君主
```

### 修复

**文件**：`RulerController.cs`

#### 1. `MonarchUnit` 属性改为显式 getter

自动检测并清除已销毁对象的悬空引用：

```csharp
public UnitController MonarchUnit
{
    get
    {
        if (monarchUnit != null && monarchUnit.gameObject == null)
        {
            monarchUnit = null;
        }
        return monarchUnit;
    }
}
```

#### 2. `FindExistingMonarch()` 增强为 3 级优先级搜索

| 优先级 | 条件 | 说明 |
|--------|------|------|
| 1 | `Data != null` 且 faction/occupation 匹配 | 最可靠 |
| 2 | `Data != null` 且有 PlayerInputHandler | 兜底识别 |
| 3 | `Data == null` 且有 PlayerInputHandler | 场景手动放置 |

每级都加了 `unit == null || unit.gameObject == null` 防护。

#### 3. 新增 `RemoveDuplicateMonarchs()` 方法

扫描场景中所有君主单位，只保留第一个，其余销毁（含 SaveManager/UnitRegistry 注销）：

```
发现 2+ 个君主 → 保留第 1 个 → 其余 Destroy + 注销 → 只剩 1 个
```

#### 4. `SpawnMonarch()` 重构为 4 步防御

```
Step 0: 验证已有 monarchUnit 引用是否存活
Step 1: RemoveDuplicateMonarchs() 清理重复
Step 2: 确保 rulerData 可用
Step 3: FindExistingMonarch() 查找已有君主
Step 4: 只在确认没有君主时，才通过 UnitFactory 创建
```

---

## Bug ③：读档时出现两个君主（核心问题）

### 现象

新建游戏正常（1 个君主），**读档后有 2 个**。

### 根因

读档路径和新建路径不同，缺少两个关键步骤：

```
新建游戏：
  SpawnMonarch() → FindExistingMonarch() → 绑定到场景中唯一的君主 ✓

读档游戏：
  SaveManager.Load()
    └─ SpawnFromSave() → SpawnUnit() → 创建君主 ①
  场景中预置的 Prefab 实例从未被清理                      → 君主 ②
  RulerController.monarchUnit 一直是 null（没人调用绑定）  → 相机不跟随、HUD 不显示
```

### 修复

#### 文件：`GameBootstrap.cs` — `InitializeScene()` 读档分支

```csharp
if (IsLoadingSave)
{
    // ① 清理场景中手动放置的君主
    RulerController.Instance.DestroyAllSceneMonarchs();

    // ② 从存档恢复唯一的君主
    SaveManager.Instance.Load(SaveSlotToLoad);

    // ③ RulerController 绑定到恢复的君主
    RulerController.Instance.BindExistingMonarch();
}
```

#### 文件：`RulerController.cs` — 新增两个公开方法

**`DestroyAllSceneMonarchs()`**：扫描并销毁所有带 `PlayerInputHandler` 的单位。

**`BindExistingMonarch()`**：用 `FindExistingMonarch()` 查找场景中唯一的君主并设置 `monarchUnit`。

---

## Bug ④：DestroyAllSceneMonarchs 抛空值异常

### 现象

```
ArgumentNullException: Value cannot be null.
Parameter name: key
  at Dictionary.Remove(null)
  at SaveManager.UnregisterSaveable()
  at RulerController.DestroyAllSceneMonarchs()
```

### 根因

场景中手动放置的君主 `UnitController` 从未被 `Initialize()` 调用，`SaveId == null`。

`DestroyAllSceneMonarchs()` → `SaveManager.UnregisterSaveable(unit)` → `_saveables.Remove(null)` → 异常。

### 修复

**文件**：`SaveManager.cs` — `UnregisterSaveable()` 增加空值防御：

```csharp
public void UnregisterSaveable(ISaveable saveable)
{
    if (saveable == null) return;
    if (string.IsNullOrEmpty(saveable.SaveId)) return;  // ← 新增
    _saveables.Remove(saveable.SaveId);
}
```

逻辑：如果 `SaveId` 为 null/empty，说明这个对象从未被注册过，自然不需要注销。

---

## 修改文件汇总

| 文件 | 修改内容 |
|------|---------|
| `RulerController.cs` | 重写 `Awake`/`OnDestroy`/`SpawnMonarch`/`FindExistingMonarch`；新增 `MonarchUnit` 显式 getter、`_isRealSingleton` 标记、`RemoveDuplicateMonarchs()`、`DestroyAllSceneMonarchs()`、`BindExistingMonarch()` |
| `GameBootstrap.cs` | 读档分支改为 3 步：清理场景君主 → 加载存档 → 绑定君主 |
| `SaveManager.cs` | `UnregisterSaveable()` 增加 `SaveId` 空值防御 |

---

## 根因链总图

```
RulerController (Singleton) 挂在 Human_Player_Ruler.prefab 上
    │
    ├── Bug①: 工厂 Instantiate 时 Destroy(gameObject) 炸掉一切
    │         └─ 修复: Destroy(this) 只移除组件，保留 UnitController
    │
    ├── Bug②: Prefab 实例存活后，防重复机制不够 → 两个君主
    │         └─ 修复: RemoveDuplicateMonarchs + 增强 FindExistingMonarch
    │
    └── Bug③: 读档流程缺少清理 + 绑定步骤 → 读档时两个君主
              └─ 修复: DestroyAllSceneMonarchs + BindExistingMonarch
                   │
                   └── Bug④: 未初始化单位的 SaveId=null → 注销空值异常
                            └─ 修复: SaveManager 加空值防御
```

---

## 架构建议

从根本上讲，应在 Unity Editor 中从 `Human_Player_Ruler.prefab` 上**移除 `RulerController` 组件**，将其单独挂在一个管理器 GameObject 上。

理由：
- `RulerController` 是全局管理器（Singleton + `DontDestroyOnLoad`）
- 不应挂在会被反复 `Instantiate` 的 Prefab 上
- 分离后所有 4 个 Bug 都不会发生

当前代码已全面防御各种情况，两种方式（挂 Prefab 上 / 独立放置）均可正常工作。
