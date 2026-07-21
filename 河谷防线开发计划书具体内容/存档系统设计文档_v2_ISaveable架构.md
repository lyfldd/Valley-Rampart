# 河谷防线 · 存档系统设计文档 v2 · ISaveable 架构

> 版本：v2.2 · 2026-07-21
> 状态：替代 v1 的硬编码方案
> 关系：v1 文档（`存档系统设计文档.md`）中的「POCO 数据结构」「原子写入」「版本迁移」「命名重构建议」仍然有效；本文件替代 v1 中的 `SaveManager.CaptureSnapshot` / `ApplySnapshot` 硬编码部分。
>
> **v2.1 修订（2026-07-21）：** 修复 `ISaveableSpawner.SpawnFromSave` 的签名漏洞——原参数 `SavePayload` 不含 `saveId`，导致读档时新建的单位实例无法匹配存档里的 `saveId`，`LoadState` 不会被调用。改为传 `ModuleSaveEntry`，并新增 `UnitController.OverrideSaveId` 方法。涉及第 4.3 节、第 5.4 节、第 8 节流程图。
>
> **v2.2 修订（2026-07-21）：** 补充程序化地图存档支持。新增 `WorldManager : ISaveable`（第 5.3 节）管理 `mapSeed` / `difficulty` / `totalDays`；`RulerSaveData` 加 `rulerName` 字段；`GameSaveRoot` 加 `GameSaveSummary` 供存档槽 UI 快速读取。响应策划案评审的 P0-3/4/5 和 P1-2。

---

## 0. 为什么推翻 v1

v1 的 `SaveManager` 长这样：

```csharp
private GameSaveData CaptureSnapshot()
{
    var data = new GameSaveData();
    data.ruler.gold = RulerController.Instance.Gold;   // ← 硬编码
    data.time.currentDay = TimeManager.Instance.CurrentDay;  // ← 硬编码
    foreach (var unit in UnitRegistry.Instance.GetAllUnits()) { ... }  // ← 硬编码
    return data;
}
```

**问题：** 每加一个系统（背包、任务、建筑），都要回头改 `SaveManager`，违反开闭原则。`SaveManager` 应该是**纯工具层**——只做"序列化 → 写磁盘"和反向操作，不耦合任何业务字段。

v2 用 **ISaveable 接口契约模式**重构：业务系统自己声明要存什么，`SaveManager` 统一收集。

---

## 1. 你贴的方案有 4 个坑

你研究的 ISaveable 思路是对的，但直接抄会踩雷。先列坑：

### 坑 1：`Dictionary<string, object>` 不能用 JsonUtility 序列化

你的 `GameSaveRoot.moduleData = Dictionary<string, object>` 在 `JsonUtility` 下直接报废——`JsonUtility` 不支持 `Dictionary`，也不支持 `object` 多态。

**两个出路：**
- (A) 引入 Newtonsoft.Json（Unity 现在官方支持，Package Manager 装 `com.unity.nuget.newtonsoft-json`）
- (B) 保留 JsonUtility，但改数据结构——每个模块的数据序列化成字符串存进 `List<ModuleSaveEntry>`

**本项目选 (B)**，理由：
1. 你是 Built-in 渲染管线 + 团结引擎 1.8.5，能不引外部包就不引
2. Newtonsoft 反序列化 `object` 时需要 `TypeNameHandling`，会写入完整类型名，存档可读性差且类型重命名就崩
3. 字符串方案让每个模块**自己负责自己的序列化**，`SaveManager` 真的成了纯工具

### 坑 2：`object` 多态反序列化拿不回原始类型

你的 `LoadState(object state)` 里 `if (state is PlayerSaveData)`——但 `JsonUtility` 反序列化 `object` 时只能给你一个空 object，类型信息丢了。Newtonsoft 靠 `TypeNameHandling` 解决，但见坑 1 的反对理由。

**修正：** 让 `ISaveable` 不返回 `object`，而是返回一个**自带类型信息的 `SavePayload`**（typeName + json 字符串）。每个模块自己 `JsonUtility.ToJson` 自己的数据，`SaveManager` 只搬运字符串。

### 坑 3：动态对象（单位）的注册时机

全局系统（`TimeManager`、`RulerController`）是 Singleton，场景加载就常驻，注册简单。但 `UnitController` 是动态的：

- `UnitFactory.SpawnUnit` 时才创建实例 → 必须在 `Initialize` 后注册 `ISaveable`
- `Die()` 销毁时必须立即注销 → 否则下次 `SaveGame` 会抓到已销毁对象，触发 `MissingReferenceException`
- 读档时旧实例全被 `Destroy`，新实例由 `UnitFactory` 创建——**这中间有个鸡生蛋问题**：`SaveManager` 想分发 `LoadState`，但新单位实例还没创建，注册表是空的

**修正：** 引入**加载分阶段**（`SaveLoadPhase`），见第 4 节。

### 坑 4：模块间加载依赖

单位 `RestoreFromSave` 时如果触发了 `UnitSpawnedEvent`，订阅者（比如敌人 AI）可能去查 `TimeManager.CurrentPhase` 决定行为——如果 `TimeManager` 还没恢复，查到的是默认值（第 1 天早晨），逻辑就错了。

**修正：** 全局系统必须先于场景对象恢复。引入 `SaveLoadPhase.Global` / `SaveLoadPhase.Scene` 两阶段。

---

## 2. 核心接口设计

### 2.1 SavePayload——自带类型信息的载荷

```csharp
using System;

/// <summary>
/// 模块存档载荷。每个 ISaveable 把自己的数据序列化成 JSON 字符串塞进来，
/// SaveManager 只搬运字符串，不关心内容。
/// </summary>
[Serializable]
public struct SavePayload
{
    /// <summary>数据类型的 AssemblyQualifiedName，用于反序列化时找回类型。</summary>
    public string typeName;

    /// <summary>模块数据序列化后的 JSON 字符串。</summary>
    public string json;

    /// <summary>数据版本号（模块自治，独立于全局 saveVersion），用于模块内迁移。</summary>
    public int version;
}
```

### 2.2 ISaveable——业务契约

```csharp
/// <summary>
/// 可存档对象契约。所有需要持久化的业务模块都实现此接口。
/// 业务模块自己管自己的数据定义、序列化、反序列化、版本迁移；
/// SaveManager 只负责收集和分发。
/// </summary>
public interface ISaveable
{
    /// <summary>全局唯一存档 ID。</summary>
    string SaveId { get; }

    /// <summary>加载阶段。全局系统 = Global，场景对象 = Scene。</summary>
    SaveLoadPhase LoadPhase { get; }

    /// <summary>保存时调用：把自身状态序列化成 SavePayload 返回。</summary>
    SavePayload SaveState();

    /// <summary>加载时调用：接收存档载荷，自行反序列化并恢复状态。</summary>
    void LoadState(SavePayload payload);
}

/// <summary>
/// 加载阶段。决定 LoadState 的调用顺序。
/// Global 阶段先执行（TimeManager、RulerController 等常驻系统），
/// Scene 阶段后执行（单位、物品等动态对象）。
/// </summary>
public enum SaveLoadPhase
{
    Global,
    Scene
}
```

### 2.3 GameSaveRoot——存档根结构（JsonUtility 友好）

```csharp
using System;
using System.Collections.Generic;

/// <summary>
/// 单个模块的存档条目。
/// </summary>
[Serializable]
public class ModuleSaveEntry
{
    public string saveId;
    public string typeName;
    public string json;
    public int version;
    public int phase;  // (int)SaveLoadPhase，存档里冗余一份，便于加载时排序
}

/// <summary>
/// 存档根容器。一个存档槽 = 一个 GameSaveRoot 实例 = 一个 JSON 文件。
/// </summary>
[Serializable]
public class GameSaveRoot
{
    // —— 元数据 ——
    public int saveVersion = 1;       // 全局存档格式版本
    public string saveTime;           // ISO8601 时间戳
    public string slotName;

    // —— 存档摘要（供存档槽 UI 快速读取，避免反序列化所有模块）——
    public GameSaveSummary summary = new GameSaveSummary();

    // —— 模块数据（List 而非 Dictionary，因为 JsonUtility 不支持 Dictionary）——
    public List<ModuleSaveEntry> modules = new List<ModuleSaveEntry>();
}

/// <summary>
/// 存档摘要。存档槽 UI 显示用，只含展示需要的字段。
/// 由 SaveManager 在 Save 时通过 GameSavingEvent 收集（见第 3 节 Save 流程）。
/// </summary>
[Serializable]
public class GameSaveSummary
{
    public string rulerName;       // 统治者名字
    public int currentDay;         // 第几天
    public int currentSeason;      // 季节
    public int difficulty;         // 难度
}
```

**为什么不用 Dictionary？** JsonUtility 不支持。用 `List<ModuleSaveEntry>` 在代码里转一下就行，性能差距对独立游戏可忽略。

---

## 3. SaveManager——纯工具层

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 存档管理器。纯工具层：序列化 → 写磁盘；读磁盘 → 反序列化 → 分发。
/// 不耦合任何业务字段，不知道"血量""金币""天数"是什么。
/// </summary>
public class SaveManager : Singleton<SaveManager>
{
    private const int CurrentSaveVersion = 1;
    private const string SaveFolderName = "Saves";
    private const string SaveFileExtension = ".json";

    // 注册表：SaveId → ISaveable
    private readonly Dictionary<string, ISaveable> _saveables = new Dictionary<string, ISaveable>();

    // ===== 注册 / 注销 =====

    public void RegisterSaveable(ISaveable saveable)
    {
        if (saveable == null) return;

        if (_saveables.TryGetValue(saveable.SaveId, out var existing))
        {
            Debug.LogWarning($"[SaveManager] SaveId 重复: {saveable.SaveId}，已存在 {existing}，新的 {saveable} 被忽略。");
            return;
        }
        _saveables.Add(saveable.SaveId, saveable);
    }

    public void UnregisterSaveable(ISaveable saveable)
    {
        if (saveable == null) return;
        _saveables.Remove(saveable.SaveId);
    }

    // ===== 保存 =====

    public bool Save(string slotId)
    {
        try
        {
            var root = new GameSaveRoot
            {
                saveVersion = CurrentSaveVersion,
                saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                slotName = slotId
            };

            // 遍历所有注册对象，收集状态。SaveManager 不关心每个模块存了什么。
            foreach (var kvp in _saveables)
            {
                ISaveable saveable = kvp.Value;
                SavePayload payload = saveable.SaveState();

                root.modules.Add(new ModuleSaveEntry
                {
                    saveId = saveable.SaveId,
                    typeName = payload.typeName,
                    json = payload.json,
                    version = payload.version,
                    phase = (int)saveable.LoadPhase
                });
            }

            string json = JsonUtility.ToJson(root, prettyPrint: true);
            string path = GetSavePath(slotId);

            // 原子写入：先写 .tmp 再替换，防断电损坏
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);

            Debug.Log($"[SaveManager] 保存成功: {path}，共 {root.modules.Count} 个模块");
            EventBus.Publish(new GameSavedEvent(slotId, true));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 保存失败: {e}");
            EventBus.Publish(new GameSavedEvent(slotId, false));
            return false;
        }
    }

    // ===== 加载 =====

    public bool Load(string slotId)
    {
        string path = GetSavePath(slotId);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveManager] 存档不存在: {path}");
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            GameSaveRoot root = JsonUtility.FromJson<GameSaveRoot>(json);

            // 全局版本迁移（整体格式升级，比如改了 GameSaveRoot 结构）
            if (root.saveVersion > CurrentSaveVersion)
            {
                Debug.LogError($"[SaveManager] 存档版本 {root.saveVersion} 高于当前支持 {CurrentSaveVersion}，拒绝加载。");
                return false;
            }

            // 分发载荷——分两阶段
            DistributePayloads(root.modules, SaveLoadPhase.Global);
            DistributePayloads(root.modules, SaveLoadPhase.Scene);

            Debug.Log($"[SaveManager] 加载成功: {path}");
            EventBus.Publish(new GameLoadedEvent(slotId, true));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 加载失败: {e}");
            EventBus.Publish(new GameLoadedEvent(slotId, false));
            return false;
        }
    }

    /// <summary>按阶段分发载荷给已注册的 ISaveable。</summary>
    private void DistributePayloads(List<ModuleSaveEntry> modules, SaveLoadPhase phase)
    {
        foreach (var entry in modules)
        {
            if (entry.phase != (int)phase) continue;

            if (!_saveables.TryGetValue(entry.saveId, out var saveable))
            {
                Debug.LogWarning($"[SaveManager] 存档模块 {entry.saveId} 未找到对应对象，跳过。");
                continue;
            }

            var payload = new SavePayload
            {
                typeName = entry.typeName,
                json = entry.json,
                version = entry.version
            };

            try
            {
                saveable.LoadState(payload);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 模块 {entry.saveId} LoadState 失败: {e}");
            }
        }
    }

    // ===== 删除 / 查询 =====

    public bool Delete(string slotId)
    {
        string path = GetSavePath(slotId);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public bool HasSave(string slotId) => File.Exists(GetSavePath(slotId));

    /// <summary>读取存档元数据（用于存档列表 UI，不触发 LoadState）。</summary>
    public GameSaveRoot GetSaveMeta(string slotId)
    {
        string path = GetSavePath(slotId);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonUtility.FromJson<GameSaveRoot>(File.ReadAllText(path));
        }
        catch { return null; }
    }

    private string GetSavePath(string slotId)
    {
        string folder = Path.Combine(Application.persistentDataPath, SaveFolderName);
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        return Path.Combine(folder, slotId + SaveFileExtension);
    }
}
```

**注意 `SaveManager` 现在干净了：** 没有任何 `RulerController.Instance.Gold`、`TimeManager.Instance.CurrentDay` 这种业务字段。它就是个"收集 + 分发 + 写文件"的工具。

---

## 4. 加载分阶段——解决动态对象和依赖问题

### 4.1 问题回顾

读档时场景里所有单位都被 `Destroy`，注册表空了。`SaveManager` 想分发 `UnitController.LoadState` 但找不到对象。同时，单位恢复时可能触发事件，订阅者会查 `TimeManager`——所以 `TimeManager` 必须先恢复。

### 4.2 两阶段加载

```
Load(slotId)
  │
  ├─ 读 JSON → GameSaveRoot
  │
  ├─ 阶段 1: DistributePayloads(Global)
  │     └─ TimeManager.LoadState   ← 常驻 Singleton，注册表里已有
  │     └─ RulerController.LoadState
  │
  ├─ 事件: SceneObjectsNeedSpawnEvent
  │     └─ UnitFactory 监听，从 root 里拉出所有 UnitSaveData，
  │        调 SpawnUnit 创建实例 → 实例 Initialize 时注册自己为 ISaveable
  │
  ├─ 阶段 2: DistributePayloads(Scene)
  │     └─ 每个 UnitController.LoadState ← 此时注册表已有这些实例
  │
  └─ GameLoadedEvent
```

### 4.3 场景对象重建事件

`SaveManager` 加载 Global 阶段后、Scene 阶段前，发布一个事件，让 `UnitFactory` 去拉起所有单位。但 `UnitFactory` 怎么知道存档里有哪些单位？

**答案：** `UnitController` 的存档载荷里有 `faction` + `occupation`，`UnitFactory` 可以从 `root.modules` 里筛出 `typeName == typeof(UnitSaveData).AssemblyQualifiedName` 的条目，反序列化拿到配置枚举，再 `SpawnUnit`。

但这又把 `UnitFactory` 和 `UnitSaveData` 类型耦合了——`UnitFactory` 知道"单位存档数据长这样"。

**更干净的方案：** 引入 `ISaveableSpawner` 接口，让业务模块自己声明"我需要从存档重建实例"。

```csharp
/// <summary>
/// 场景对象重建器。需要从存档动态创建实例的模块实现此接口。
/// SaveManager 在 Global 阶段后、Scene 阶段前调用 SpawnFromSave。
/// </summary>
public interface ISaveableSpawner
{
    /// <summary>该 spawner 负责的模块 SaveId 前缀（如 "Unit_"）。</summary>
    string SaveIdPrefix { get; }

    /// <summary>
    /// 根据存档条目创建实例并注册为 ISaveable。
    /// 参数是 ModuleSaveEntry 而不是 SavePayload——因为 spawner 创建实例后
    /// 需要把存档里的 saveId 赋给新实例（覆盖 Initialize 时分配的新 GUID），
    /// 否则 SaveManager 在 Scene 阶段按 saveId 分发 LoadState 时找不到对象。
    /// </summary>
    void SpawnFromSave(ModuleSaveEntry entry);
}
```

`UnitFactory` 实现：

```csharp
public class UnitFactory : Singleton<UnitFactory>, ISaveableSpawner
{
    public string SaveIdPrefix => "Unit_";

    public void SpawnFromSave(ModuleSaveEntry entry)
    {
        if (entry.typeName != typeof(UnitSaveData).AssemblyQualifiedName) return;

        var data = JsonUtility.FromJson<UnitSaveData>(entry.json);
        var faction = (Faction)data.faction;
        var occupation = (Occupation)data.occupation;

        UnitData config = UnitDataManager.Instance.GetData(faction, occupation);
        if (config == null)
        {
            Debug.LogError($"[UnitFactory] 找不到配置: {faction}_{occupation}");
            return;
        }

        Vector2 pos = new Vector2(data.posX, data.posY);
        // SpawnUnit → Initialize → 用新 GUID 注册 ISaveable
        GameObject go = SpawnUnit(config, pos);

        // ⚠️ 关键：把存档里的 saveId 覆盖到新实例上
        // 否则 SaveManager 在 Scene 阶段按 saveId 分发 LoadState 时找不到对象
        if (go != null)
        {
            var controller = go.GetComponent<UnitController>();
            controller.OverrideSaveId(entry.saveId);
        }
    }
}
```

`UnitController` 需要加一个 `OverrideSaveId` 方法（见第 5.3 节 UnitController 实现）。

`SaveManager` 加载流程更新：

```csharp
public bool Load(string slotId)
{
    // ... 读 JSON ...

    // 阶段 1: 全局模块恢复
    DistributePayloads(root.modules, SaveLoadPhase.Global);

    // 阶段 1.5: 场景对象重建（让 spawner 把实例创建出来并注册 ISaveable）
    foreach (var entry in root.modules)
    {
        if (entry.phase != (int)SaveLoadPhase.Scene) continue;
        foreach (var spawner in _spawners)
        {
            if (entry.saveId.StartsWith(spawner.SaveIdPrefix))
            {
                // 直接传 entry，spawner 内部会用 entry.saveId 覆盖新实例的 SaveId
                spawner.SpawnFromSave(entry);
                break;
            }
        }
    }

    // 阶段 2: 场景模块恢复 LoadState（此时实例已注册）
    DistributePayloads(root.modules, SaveLoadPhase.Scene);

    // ...
}

private readonly List<ISaveableSpawner> _spawners = new List<ISaveableSpawner>();

public void RegisterSpawner(ISaveableSpawner spawner)
{
    if (!_spawners.Contains(spawner)) _spawners.Add(spawner);
}
```

`GameBootstrap` 启动时调一次 `SaveManager.Instance.RegisterSpawner(UnitFactory.Instance)`。

---

## 5. 业务系统实现 ISaveable

### 5.1 TimeManager

```csharp
public class TimeManager : Singleton<TimeManager>, ISaveable
{
    public string SaveId => "TimeManager";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    // ... 原有字段 ...

    protected override void Awake()
    {
        base.Awake();
        SaveManager.Instance.RegisterSaveable(this);
    }

    public SavePayload SaveState()
    {
        var data = new TimeSaveData
        {
            currentDay = CurrentDay,
            currentTimeOfDay = CurrentTimeOfDay,
            currentSeason = (int)CurrentSeason,
            currentPhase = (int)CurrentPhase,
            secondsPerDay = SecondsPerDay,
            totalDays = TotalDays,
            daysPerSeason = DaysPerSeason
        };
        return new SavePayload
        {
            typeName = typeof(TimeSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(TimeSaveData).AssemblyQualifiedName)
        {
            Debug.LogWarning("[TimeManager] 存档类型不匹配，跳过。");
            return;
        }

        var data = JsonUtility.FromJson<TimeSaveData>(payload.json);

        // 先配置再状态，防止 AdvanceDay 误触发
        SetSecondsPerDay(data.secondsPerDay);
        SetTotalDays(data.totalDays);
        SetDaysPerSeason(data.daysPerSeason);

        // 直接赋值，不发事件
        CurrentDay = data.currentDay;
        CurrentTimeOfDay = data.currentTimeOfDay;
        CurrentSeason = (Season)data.currentSeason;
        CurrentPhase = (TimePhase)data.currentPhase;
        CurrentHour = Mathf.Clamp(Mathf.FloorToInt(CurrentTimeOfDay), 0, 23);
        _dayTimer = (CurrentTimeOfDay / 24f) * secondsPerDay;
    }
}
```

### 5.2 RulerController

```csharp
public class RulerController : Singleton<RulerController>, ISaveable
{
    public string SaveId => "RulerController";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    // ... 原有字段 ...

    // 新增：统治者名字（新建游戏时玩家输入）
    public string RulerName { get; private set; } = "无名君主";

    protected override void Awake()
    {
        base.Awake();
        SaveManager.Instance.RegisterSaveable(this);
        // ... 原有逻辑 ...
    }

    // 新增：新建游戏时设置名字（CharacterCreation 面板调用）
    public void SetRulerName(string name)
    {
        if (!string.IsNullOrEmpty(name)) RulerName = name;
    }

    public SavePayload SaveState()
    {
        var data = new RulerSaveData
        {
            rulerName = RulerName,
            gold = Gold, stone = Stone, wood = Wood, food = Food
        };
        return new SavePayload
        {
            typeName = typeof(RulerSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(RulerSaveData).AssemblyQualifiedName) return;

        var data = JsonUtility.FromJson<RulerSaveData>(payload.json);
        RulerName = string.IsNullOrEmpty(data.rulerName) ? "无名君主" : data.rulerName;
        Gold = data.gold;
        Stone = data.stone;
        Wood = data.wood;
        Food = data.food;

        // 不在这里 SpawnMonarch——君主作为单位走 UnitController 的 ISaveable 流程
        // 如果当前场景里君主还没创建，由 UnitFactory.SpawnFromSave 创建
    }
}
```

### 5.3 WorldManager（新增 · 程序化地图种子 + 难度）

管理新建游戏时确定的「游戏会话级」配置：地图种子、难度、总天数。
地图是程序化生成的（基于群落模板），读档后必须用同一个 seed 复现地形，否则单位和建筑的位置对不上新地形。

```csharp
public class WorldManager : Singleton<WorldManager>, ISaveable
{
    public string SaveId => "WorldManager";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    public int MapSeed { get; private set; }
    public int Difficulty { get; private set; }   // 0=Easy, 1=Normal, 2=Hard
    public int TotalDays { get; private set; }     // 胜利条件之一

    protected override void Awake()
    {
        base.Awake();
        SaveManager.Instance.RegisterSaveable(this);
    }

    /// <summary>新建游戏时由 CharacterCreation 流程调用。</summary>
    public void ApplyConfig(int mapSeed, int difficulty, int totalDays)
    {
        MapSeed = mapSeed;
        Difficulty = difficulty;
        TotalDays = totalDays;
        // 触发地图程序化生成（用 MapSeed 初始化生成器）
        // GenerateWorld(MapSeed);
    }

    public SavePayload SaveState()
    {
        var data = new WorldSaveData
        {
            mapSeed = MapSeed,
            difficulty = Difficulty,
            totalDays = TotalDays
        };
        return new SavePayload
        {
            typeName = typeof(WorldSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(WorldSaveData).AssemblyQualifiedName) return;

        var data = JsonUtility.FromJson<WorldSaveData>(payload.json);
        MapSeed = data.mapSeed;
        Difficulty = data.difficulty;
        TotalDays = data.totalDays;

        // 读档时用存档的 seed 重新生成地图（要求生成过程是确定性的：同 seed 同结果）
        // GenerateWorld(MapSeed);
    }
}
```

**关键要求：** 地图生成必须是**确定性的**——同一个 `mapSeed` 必须生成完全相同的地形。生成过程中不能使用 `Random.Range`（每次运行结果不同），必须用 `System.Random` 并用 `mapSeed` 初始化：
```csharp
var rng = new System.Random(MapSeed);
// 后续所有随机都走 rng.Next() / rng.NextDouble()
```

**NewGame 路径调用顺序：** `WorldManager.ApplyConfig` → `TimeManager.ApplyConfig` → `RulerController.SetRulerName` + `ApplyStartResources` → `UnitFactory.SpawnMonarch` → 存初始档。

**ContinueGame 路径：** `WorldManager.LoadState` 在 Global 阶段恢复 seed 后，可以触发地图生成——此时 `TimeManager` / `RulerController` 的 LoadState 也在同阶段，顺序靠注册顺序保证 `WorldManager` 最先（注册点表第一行）。

**关键变化：** `RulerController.LoadState` 不再 `SpawnMonarch`。君主作为单位，和其他单位一样走 `UnitController.ISaveable` 流程，由 `UnitFactory.SpawnFromSave` 创建。这避免了"君主资源恢复但君主 GameObject 还没生成"的状态不一致。

### 5.4 UnitController——动态对象

```csharp
public class UnitController : MonoBehaviour, ISaveable
{
    // SaveId 必须全局唯一——同类单位有多个，不能用 "Unit_Archer"
    // 用 GUID 保证唯一性
    public string SaveId { get; private set; }
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Scene;

    // ... 原有字段 ...

    public virtual void Initialize(UnitData data)
    {
        Data = data;
        // ... 原有初始化 ...

        // 分配唯一 SaveId
        SaveId = $"Unit_{Data.faction}_{Data.occupation}_{System.Guid.NewGuid():N}";

        SaveManager.Instance.RegisterSaveable(this);
    }

    protected virtual void Die()
    {
        // 先注销 ISaveable，再销毁对象，防止 SaveManager 抓到已销毁实例
        SaveManager.Instance.UnregisterSaveable(this);

        // ... 原有 Die 逻辑 ...
    }

    /// <summary>
    /// 用存档里的 SaveId 覆盖 Initialize 时分配的新 GUID。
    /// 由 UnitFactory.SpawnFromSave 在读档时调用——读档流程：
    ///   1. SpawnUnit → Initialize（用新 GUID 注册）
    ///   2. OverrideSaveId（注销旧的，改用存档 saveId，重新注册）
    ///   3. SaveManager 在 Scene 阶段按存档 saveId 分发 LoadState，此时能找到这个实例
    /// 不调这个方法会导致 SaveManager 找不到对象，LoadState 不被调用，单位状态丢失。
    /// </summary>
    public void OverrideSaveId(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        // 已注册过（Initialize 时），先注销再换 ID 重新注册
        if (!string.IsNullOrEmpty(SaveId))
        {
            SaveManager.Instance.UnregisterSaveable(this);
        }

        SaveId = id;
        SaveManager.Instance.RegisterSaveable(this);
    }

    public SavePayload SaveState()
    {
        var data = new UnitSaveData
        {
            faction = (int)Data.faction,
            occupation = (int)Data.occupation,
            currentHp = CurrentHp,
            maxHp = MaxHp,
            attack = Attack,
            defense = Defense,
            walkSpeed = WalkSpeed,
            runSpeed = RunSpeed,
            posX = transform.position.x,
            posY = transform.position.y
        };
        return new SavePayload
        {
            typeName = typeof(UnitSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(UnitSaveData).AssemblyQualifiedName) return;

        var data = JsonUtility.FromJson<UnitSaveData>(payload.json);
        CurrentHp = data.currentHp;
        MaxHp = data.maxHp;
        Attack = data.attack;
        Defense = data.defense;
        WalkSpeed = data.walkSpeed;
        RunSpeed = data.runSpeed;
        // 位置已在 SpawnFromSave 时由 UnitFactory.SpawnUnit 设置
    }
}
```

**SaveId 用 GUID 的原因：** 场景里可能有 10 个 Archer，如果都用 `"Unit_Archer"` 当 SaveId，注册时第一个能进，后 9 个被 `RegisterSaveable` 拒绝。GUID 保证唯一。

**代价：** 存档文件里每个单位的 SaveId 都是一长串 GUID，可读性差。但这是 JSON 存档的通病，UI 不需要直接展示这个 ID。

---

## 6. 存档数据结构（每个模块自己定义）

这部分和 v1 一致，但归属改为「各业务模块自己的命名空间」：

```csharp
// TimeManager.cs 同文件或 Data/TimeSaveData.cs
[Serializable]
public class TimeSaveData
{
    public int currentDay;
    public float currentTimeOfDay;
    public int currentSeason;
    public int currentPhase;
    public float secondsPerDay;
    public int totalDays;
    public int daysPerSeason;
}

// RulerController.cs 同文件或 Data/RulerSaveData.cs
[Serializable]
public class RulerSaveData
{
    public string rulerName;        // 统治者名字（新建游戏时玩家输入，必须存档）
    public int gold, stone, wood, food;
}

// WorldManager.cs 同文件或 Data/WorldSaveData.cs
// 地图程序化生成的种子 + 难度等"游戏会话级"配置
[Serializable]
public class WorldSaveData
{
    public int mapSeed;             // 地图生成种子（读档用同 seed 复现地形）
    public int difficulty;          // 难度（0=Easy, 1=Normal, 2=Hard）
    public int totalDays;           // 总目标天数（胜利条件之一）
    // 未来可加：已探索区域、已生成群落状态等
}

// UnitController.cs 同文件或 Data/UnitSaveData.cs
[Serializable]
public class UnitSaveData
{
    public int faction;
    public int occupation;
    public int currentHp, maxHp, attack, defense;
    public float walkSpeed, runSpeed;
    public float posX, posY;
}
```

**没有 `GameSaveData` 这种"上帝对象"了**——每个模块管自己的存档数据类，互不耦合。新增系统时，新系统自己定义自己的 `XxxSaveData`，实现 `ISaveable`，注册到 `SaveManager`，完事。`SaveManager` 一行代码都不用改。

---

## 7. 你项目里的完整注册点

| 系统 | 类型 | LoadPhase | 注册时机 | SaveId 策略 |
|---|---|---|---|---|
| `WorldManager` | Singleton | Global | `Awake` | 固定 `"WorldManager"` |
| `TimeManager` | Singleton | Global | `Awake` | 固定 `"TimeManager"` |
| `RulerController` | Singleton | Global | `Awake` | 固定 `"RulerController"` |
| `UnitController` | 动态实例 | Scene | `Initialize` | `Unit_{faction}_{occupation}_{GUID}` |
| `UnitFactory` | Singleton + Spawner | - | `Awake` 注册 Spawner | - |
| `GameStateManager` | Singleton | - | 不存档 | - |
| `InputManager` | Singleton | - | 不存档 | - |
| `UnitDataManager` | Singleton | - | 不存档（配置目录） | - |
| `UnitRegistry` | Singleton | - | 不存档（运行时索引） | - |

未来加背包系统：`InventoryManager` 实现 `ISaveable`，`Awake` 注册，定义 `InventorySaveData`。`SaveManager` 不用动。

---

## 8. 完整加载流程

```
玩家点「继续游戏」
  │
  ▼
SaveManager.Load("slot_1")
  │
  ├─ 读 JSON → GameSaveRoot
  │
  ├─ 阶段 1: Global 模块恢复
  │   ├─ TimeManager.LoadState      ← 常驻 Singleton，注册表里已有
  │   └─ RulerController.LoadState  ← 资源数值恢复
  │
  ├─ 阶段 1.5: 场景对象重建
  │   └─ 遍历 root.modules 中 phase=Scene 的条目
  │       └─ UnitFactory.SpawnFromSave(entry)
  │           ├─ 反序列化 UnitSaveData
  │           ├─ 从 UnitDataManager 查配置
  │           ├─ SpawnUnit(config, pos)
  │           │   └─ Initialize → 用新 GUID 注册 ISaveable
  │           └─ OverrideSaveId(entry.saveId)
  │               └─ 注销新 GUID，改用存档 saveId 重新注册
  │                   （保证阶段 2 能按存档 saveId 找到这个实例）
  │
  ├─ 阶段 2: Scene 模块恢复
  │   └─ 每个 UnitController.LoadState ← 用存档值覆盖 Initialize 设的默认值
  │
  ├─ GameLoadedEvent
  │
  └─ GameStateManager.SetState(Playing)
```

**关键保证：**
- 阶段 1 完成后 `TimeManager` 已就位，阶段 2 单位恢复触发的事件查到的是正确时间
- 阶段 1.5 创建实例后阶段 2 才分发 LoadState，注册表不会找不到对象
- `UnitController.Initialize` 先设默认值，`LoadState` 再覆盖——旧存档缺字段也能兜底

---

## 9. 版本迁移——两层

### 9.1 全局版本（GameSaveRoot.saveVersion）

改了 `GameSaveRoot` 结构（比如加字段、改 List 为 Dictionary）时升。在 `SaveManager.Load` 里处理：

```csharp
while (root.saveVersion < CurrentSaveVersion)
{
    switch (root.saveVersion)
    {
        case 1: root = MigrateRootV1ToV2(root); break;
        // case 2: root = MigrateRootV2ToV3(root); break;
    }
}
```

### 9.2 模块版本（SavePayload.version）

每个模块自己管。比如 `TimeManager` 加了"难度等级"字段，升 `TimeSaveData` 的版本：

```csharp
public void LoadState(SavePayload payload)
{
    var data = JsonUtility.FromJson<TimeSaveData>(payload.json);

    // 模块内迁移
    if (payload.version < 2)
    {
        // 旧版本没有 difficultyLevel 字段，给默认值
        data.difficultyLevel = 1;
    }

    // ... 应用 data ...
}
```

**好处：** 每个模块的迁移逻辑自己管，`SaveManager` 不掺和。新增模块时旧存档里没它的条目，模块就跳过 LoadState，等于默认初始化——天然兼容。

---

## 10. 模块版本迁移的天然兼容

这是 ISaveable 模式的一个隐藏好处：**新增模块自动兼容旧存档**。

场景：v1.0 没有背包系统，v1.1 加了 `InventoryManager : ISaveable`。
- 加载 v1.0 存档：`root.modules` 里没有 `InventoryManager` 条目 → `DistributePayloads` 跳过 → `InventoryManager` 保持 `Awake` 时的默认状态。完美。
- 加载 v1.1 存档：正常分发。

反过来，删除模块也兼容：v1.2 删了某个系统，加载 v1.1 存档时 `root.modules` 里有它的条目，但注册表里没有对应 ISaveable → `DistributePayloads` 打个 warning 跳过，不崩。

---

## 11. 实施步骤

| 步骤 | 内容 |
|---|---|
| 1 | 定义 `ISaveable` / `ISaveableSpawner` / `SavePayload` / `SaveLoadPhase`（新文件 `SaveableContracts.cs`） |
| 2 | 定义 `GameSaveRoot` / `ModuleSaveEntry`（新文件 `SaveRoot.cs`） |
| 3 | 重写 `SaveManager`（替换 v1 版本） |
| 4 | `TimeManager` 实现 `ISaveable`，定义 `TimeSaveData` |
| 5 | `RulerController` 实现 `ISaveable`，定义 `RulerSaveData`；移除 `SpawnMonarch` 在 LoadState 里的调用 |
| 6 | `UnitController` 实现 `ISaveable`，定义 `UnitSaveData`；`Initialize` 里分配 GUID 并注册；`Die` 里先注销 |
| 7 | `UnitFactory` 实现 `ISaveableSpawner`，`GameBootstrap` 启动时 `RegisterSpawner` |
| 8 | 加 `GameSavedEvent` / `GameLoadedEvent` |
| 9 | UI 接存档列表 |
| 10 | （可选）`UnitDataManager` → `UnitConfigCatalog` 重命名 |

---

## 12. 对比 v1 vs v2

| 维度 | v1 硬编码 | v2 ISaveable |
|---|---|---|
| 新增系统 | 改 SaveManager + 改 GameSaveData | 实现 ISaveable 即可，SaveManager 不动 |
| 业务字段耦合 | SaveManager 知道所有字段 | SaveManager 零业务知识 |
| 测试 | SaveManager 测试要 mock 所有系统 | 每个模块独立测试 SaveState/LoadState |
| 动态对象 | 难处理（鸡生蛋） | ISaveableSpawner 显式声明重建责任 |
| 加载依赖 | 隐式（按代码顺序） | 显式（SaveLoadPhase） |
| 存档可读性 | 高（字段名清晰） | 低（模块数据是 JSON 字符串套娃） |
| 性能 | 一次序列化 | 多次序列化（每模块一次） |

**性能担忧：** 每个模块单独 `JsonUtility.ToJson` 会有多次反射开销。对独立游戏规模（几十个模块、几百个单位）可忽略。如果未来单位数量上千，可以批量化——让 `UnitRegistry` 实现一个 `ISaveable` 把所有单位打包成一个 `List<UnitSaveData>` 一次序列化，SaveId 用 `"UnitRegistry"`。这是优化项，不是架构问题。

**可读性担忧：** 存档 JSON 里每个模块的数据是嵌套字符串，人眼直接读不友好。可以写个 Editor 工具把存档"展开"成可读格式，或者开发期用 `prettyPrint: true` + 手动 `JsonUtility` 二次反序列化查看。

---

## 13. 一句话总结

v1 的 `SaveManager` 是个"知道所有事的管家"——糟糕。
v2 的 `SaveManager` 是个"只管搬箱子的快递员"——业务模块自己装箱拆箱，SaveManager 只负责运。

你研究的方向完全正确，ISaveable 是 Unity 生态事实标准。本文件把你方案里的 4 个坑（Dictionary 序列化、object 多态、动态对象注册、加载依赖）填了，并落地到你的 `TimeManager` / `RulerController` / `UnitController` / `UnitFactory` 上。
