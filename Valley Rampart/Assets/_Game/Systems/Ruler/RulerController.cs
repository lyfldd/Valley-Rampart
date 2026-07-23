using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 君主控制器（RulerController）
// 职责：管理玩家君主单位的生命周期、国家资源和游戏结束条件。
//
// 核心设计（引导书第 5 节）：
//   - 单例模式：全局唯一，挂载在君主 Prefab（Human_Player_Ruler.prefab）上。
//   - 重复实例处理：不销毁整个 GameObject（因为同 GameObject 上还有 UnitController 等必需组件），
//     只移除多余的 RulerController 组件自身。
//   - 资源管理：通过 ModifyResource 统一入口修改资源，每次修改发布 RulerResourceChangedEvent。
//   - 君主死亡：订阅 UnitDiedEvent 检测君主阵亡，触发 GameState.GameOver。
//   - 存档集成：实现 ISaveable 接口，SaveManager 在 Global 阶段保存/恢复君主国家资源。
//
// 战斗属性（Attack/Defense/WalkSpeed/RunSpeed/Hp）由 UnitController 管理，
// 访问方式：RulerController.Instance.MonarchUnit.Attack 等。
public class RulerController : Singleton<RulerController>, ISaveable
{
    // ISaveable 标识：存档系统通过此 ID 查找并恢复君主控制器状态
    public string SaveId => "RulerController";

    // 存档加载阶段：Global（场景加载前恢复，先于场景内单位）
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    [Header("========== 君主配置引用 ==========")]
    [Tooltip("手动拖入君主的 RulerData 资产（优先使用）。留空则自动从 UnitDataManager 获取。")]
    [SerializeField] private RulerData rulerData;

    [Header("========== 君主出生位置 ==========")]
    [SerializeField] private Vector2 spawnPosition = new Vector2(0f, 0f);

    [Header("========== 运行时状态 ==========")]
    [SerializeField] private UnitController monarchUnit;

    // ===== 公开属性 =====

    // 君主的配置数据（ScriptableObject 引用），包含初始资源等
    public RulerData RulerData => rulerData;

    // 君主运行时单位引用。如果引用已失效（对象被销毁），自动清理为 null。
    public UnitController MonarchUnit
    {
        get
        {
            // Unity null-check：对象被销毁后 C# 引用非 null 但 Unity 判定为 null
            if (monarchUnit != null && monarchUnit.gameObject == null)
            {
                Debug.LogWarning("[RulerController] MonarchUnit 引用已失效（对象已销毁），自动清除。");
                monarchUnit = null;
            }
            return monarchUnit;
        }
    }

    // 君主是否存活（单位存在且 HP > 0）
    public bool IsMonarchAlive => MonarchUnit != null && MonarchUnit.CurrentHp > 0;

    // 国家资源（金币/石材/木材/食物），通过 ModifyResource 统一修改
    public int Gold { get; private set; }
    public int Stone { get; private set; }
    public int Wood { get; private set; }
    public int Food { get; private set; }

    // 统治者名字（新建游戏时玩家输入，存档恢复时从 RulerSaveData 读取）
    public string RulerName { get; private set; } = "无名君主";

    // 标记当前实例是否为真正的单例（非重复副本）。
    // 用于 OnDestroy 判断是否需要执行反订阅/反注册等清理逻辑。
    private bool _isRealSingleton;

    // 战斗属性（Attack/Defense/WalkSpeed/RunSpeed/Hp）已移至 UnitController
    // 访问方式：RulerController.Instance.MonarchUnit.Attack 等

    protected override void Awake()
    {
        // 不直接调用 base.Awake()，因为 Singleton 基类的 duplicate 检测会 Destroy(gameObject)
        // 但 RulerController 被挂在单位 Prefab 上（Human_Player_Ruler.prefab），
        // 同一 GameObject 上还有 UnitController、PlayerInputHandler 等运行时必需的组件。
        // 当 Prefab 被 UnitFactory 实例化时，如果已存在 RulerController 单例，
        // 只应移除 RulerController 自身，而不能销毁整个 GameObject。
        if (_instance != null && _instance != this)
        {
            Debug.Log($"[RulerController] 检测到重复实例（GameObject='{gameObject.name}'），移除自身组件保留单位。");
            Destroy(this);
            return;  // _isRealSingleton 保持 false，OnDestroy 不会做多余清理
        }

        _instance = this;
        _isRealSingleton = true;
        DontDestroyOnLoad(gameObject);

        // 尝试加载君主数据并应用（Inspector 优先，其次 UnitDataManager）
        // 所有属性在 ApplyRulerData() 中从资产同步，无需 initial* 兜底字段
        TryLoadRulerData();

        // 订阅事件（仅真正的单例订阅）
        // 注：UnitDataLoadedEvent 已废弃（LoadManager 改发 ConfigsLoadedEvent），
        // 君主数据在 SpawnMonarch 时由 LoadManager.GetUnitData 获取，不依赖事件。
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);

        SaveManager.Instance.RegisterSaveable(this);
    }

    private void OnDestroy()
    {
        // 只有真正的单例才做了订阅和注册，重复副本跳过清理
        if (!_isRealSingleton) return;

        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
    }

    // 尝试加载君主 RulerData
    // 优先使用 Inspector 手动指定的资产，否则从 UnitDataManager 获取
    // 如果 UnitDataManager 尚未初始化（阶段1 未完成），延迟到 SpawnMonarch 时再获取
    private void TryLoadRulerData()
    {
        if (rulerData != null)
        {
            Debug.Log($"[RulerController] 使用手动指定的君主数据: {rulerData.name}");
            ApplyRulerData();
            return;
        }

        // UnitDataManager 可能还没初始化（LoadManager 阶段1 还没跑）
        if (!UnitDataManager.Instance.IsInitialized)
        {
            Debug.Log("[RulerController] UnitDataManager 尚未初始化，等待 SpawnMonarch 时由 LoadManager 获取...");
            return;
        }

        // 从 UnitDataManager 获取君主数据
        FetchRulerDataFromManager();
    }

    // 从 UnitDataManager 查找君主数据资产
    // 使用 Faction.Human_Player + Occupation.Ruler 作为组合键
    private void FetchRulerDataFromManager()
    {
        UnitData data = LoadManager.Instance.GetUnitData(Faction.Human_Player, Occupation.Ruler);
        rulerData = data as RulerData;

        if (rulerData != null)
        {
            Debug.Log($"[RulerController] 从 UnitDataManager 获取君主数据: {rulerData.name}");
            ApplyRulerData();
        }
        else if (data != null)
        {
            Debug.LogError("[RulerController] 找到君主数据但类型不是 RulerData！请使用 RulerData 资产而非普通 UnitData。");
        }
        else
        {
            Debug.LogError("[RulerController] 未找到君主数据！请确保 Resources/UnitData/ 下有 Human_Player_Ruler.asset (RulerData 类型)");
        }
    }

    // 将 RulerData 资产中的国家资源同步到运行时属性。
    // 单位级属性（HP/Attack/Defense/Speed）由 UnitController 管理，不在此处理。
    private void ApplyRulerData()
    {
        if (rulerData == null) return;

        // 初始国家资源（来自 RulerData 子类的额外字段）
        Gold = rulerData.initialGold;
        Stone = rulerData.initialStone;
        Wood = rulerData.initialWood;
        Food = rulerData.initialFood;

        Debug.Log($"[RulerController] 已从资产同步国家资源: "
            + $"Gold={Gold}, Stone={Stone}, Wood={Wood}, Food={Food}");
    }

    // 在出生位置创建君主单位，场景上没有君主时代码兜底
    // 流程：验证已有引用 → 清理重复 → 确保数据 → 查找已有 → 代码创建
    public void SpawnMonarch()
    {
        // Step 0: 验证已有引用是否仍然有效
        if (monarchUnit != null)
        {
            if (monarchUnit.gameObject != null)  // Unity null-check：对象未被销毁
            {
                Debug.LogWarning("[RulerController] 君主已存在，跳过重复创建。");
                return;
            }
            else
            {
                Debug.LogWarning("[RulerController] 君主引用已失效（对象已销毁），清除后重新查找。");
                monarchUnit = null;
            }
        }

        // Step 1: 清理场景中可能存在的重复君主（防御：如果之前某次调用创建了多余的）
        int removed = RemoveDuplicateMonarchs();
        if (removed > 0)
        {
            Debug.LogWarning($"[RulerController] 清理了 {removed} 个重复的君主单位。");
        }

        // Step 2: 确保我们有君主的配置数据
        if (rulerData == null)
        {
            FetchRulerDataFromManager();
        }

        if (rulerData == null)
        {
            Debug.LogError("[RulerController] 没有君主 RulerData，无法创建君主！");
            return;
        }

        // Step 3: 在场景中查找已有的君主（已初始化 或 未初始化但有 PlayerInputHandler）
        monarchUnit = FindExistingMonarch();

        if (monarchUnit != null)
        {
            // 如果找到的是未初始化的，注入数据
            if (monarchUnit.Data == null)
            {
                monarchUnit.Initialize(rulerData);
                Debug.Log($"[RulerController] 使用场景中已有的君主: {monarchUnit.name}，已注入数据");
            }
            else
            {
                Debug.Log($"[RulerController] 绑定到已初始化的君主: {monarchUnit.name}");
            }
            return;
        }

        // Step 4: 场景中确实没有君主，代码兜底创建
        // Prefab 已由 LoadManager 阶段1 预加载，无需再 PreloadAll
        Debug.Log("[RulerController] 场景中未找到君主，通过 UnitFactory 创建...");
        GameObject rulerGo = LoadManager.Instance.SpawnUnit(rulerData, spawnPosition);
        if (rulerGo != null)
        {
            monarchUnit = rulerGo.GetComponent<UnitController>();
            Debug.Log($"[RulerController] 君主已创建: "
                + $"位置={spawnPosition}, "
                + $"HP={monarchUnit.CurrentHp}/{rulerData.maxHp}, "
                + $"攻击={rulerData.attack}");
        }
        else
        {
            Debug.LogError("[RulerController] 君主 Prefab 实例化失败！");
        }
    }

    // 在场景中查找已有的君主单位。
    // 识别标准（优先级从高到低）：
    //   1. Data 已初始化 且 faction=Human_Player, occupation=Ruler
    //   2. Data 已初始化 但带有 PlayerInputHandler（兜底识别）
    //   3. Data==null 但有 PlayerInputHandler（场景中手动放置但未初始化）
    private UnitController FindExistingMonarch()
    {
        UnitController[] allUnits = FindObjectsOfType<UnitController>();

        // 优先级 1：已初始化的君主（最可靠，通过 Data 的 faction+occupation 判断）
        foreach (var unit in allUnits)
        {
            if (unit == null || unit.gameObject == null) continue;
            if (unit.Data != null &&
                unit.Data.faction == Faction.Human_Player &&
                unit.Data.occupation == Occupation.Ruler)
            {
                Debug.Log($"[RulerController] FindExistingMonarch → 找到已初始化君主: {unit.name}");
                return unit;
            }
        }

        // 优先级 2：已初始化的单位但带有 PlayerInputHandler（兜底：可能 Data 的 faction/occupation 被误设）
        foreach (var unit in allUnits)
        {
            if (unit == null || unit.gameObject == null) continue;
            if (unit.Data != null && unit.GetComponent<PlayerInputHandler>() != null)
            {
                Debug.Log($"[RulerController] FindExistingMonarch → 通过 PlayerInputHandler 找到已初始化单位: {unit.name}");
                return unit;
            }
        }

        // 优先级 3：未初始化但手动放置的单位（Data==null, 有 PlayerInputHandler）
        foreach (var unit in allUnits)
        {
            if (unit == null || unit.gameObject == null) continue;
            if (unit.Data == null && unit.GetComponent<PlayerInputHandler>() != null)
            {
                Debug.Log($"[RulerController] FindExistingMonarch → 找到未初始化君主（场景手动放置）: {unit.name}");
                return unit;
            }
        }

        return null;
    }

    // 清理场景中多余的君主单位。只保留第一个找到的，其余销毁。
    // 返回清理的数量。
    private int RemoveDuplicateMonarchs()
    {
        UnitController[] allUnits = FindObjectsOfType<UnitController>();
        UnitController firstMonarch = null;
        int removed = 0;

        foreach (var unit in allUnits)
        {
            if (unit == null || unit.gameObject == null) continue;

            bool isMonarch = unit.GetComponent<PlayerInputHandler>() != null;
            if (!isMonarch && unit.Data != null)
            {
                isMonarch = unit.Data.faction == Faction.Human_Player
                         && unit.Data.occupation == Occupation.Ruler;
            }

            if (!isMonarch) continue;

            if (firstMonarch == null)
            {
                firstMonarch = unit;  // 保留第一个
            }
            else
            {
                Debug.LogWarning($"[RulerController] 移除重复君主: {unit.name} (SaveId={unit.SaveId})");
                // 先注销 ISaveable（SaveId 可能为 null，UnregisterSaveable 内部已防御），再销毁
                SaveManager.Instance.UnregisterSaveable(unit);
                UnitRegistry.Instance.Unregister(unit);
                Destroy(unit.gameObject);
                removed++;
            }
        }

        return removed;
    }

    // ===== 读档相关：清理 + 绑定 =====

    // 销毁场景中所有手动放置的单位（不仅限于君主）。
    // 用于读档前清理，避免场景预置单位与存档恢复的单位重复（引导书 R2 规则）。
    // 同时清除 monarchUnit 引用，由后续流程重新赋值。
    public void DestroyAllSceneUnits()
    {
        var allUnits = FindObjectsOfType<UnitController>();
        int destroyed = 0;

        foreach (var unit in allUnits)
        {
            if (unit == null || unit.gameObject == null) continue;

            Debug.Log($"[RulerController] 读档前销毁场景单位: {unit.name} (SaveId={unit.SaveId})");
            // SaveId 可能为 null（未初始化），UnregisterSaveable 内部已做防御
            SaveManager.Instance.UnregisterSaveable(unit);
            UnitRegistry.Instance.Unregister(unit);
            Destroy(unit.gameObject);
            destroyed++;
        }

        if (destroyed > 0)
        {
            Debug.Log($"[RulerController] 共清理 {destroyed} 个场景单位（为读档做准备）");
        }

        // 清除引用，由后续流程重新赋值
        monarchUnit = null;
    }

    // 读档完成后调用：在场景中查找已恢复的君主单位并绑定。
    // 新建模式不需要调用（SpawnMonarch 中已完成绑定）。
    public void BindExistingMonarch()
    {
        if (monarchUnit != null && monarchUnit.gameObject != null)
        {
            Debug.Log("[RulerController] 君主已绑定，跳过。");
            return;
        }

        monarchUnit = FindExistingMonarch();

        if (monarchUnit != null)
        {
            Debug.Log($"[RulerController] 读档后绑定到君主: {monarchUnit.name} (HP={monarchUnit.CurrentHp}/{monarchUnit.MaxHp})");
        }
        else
        {
            Debug.LogWarning("[RulerController] 读档后未找到君主单位！场景中可能没有君主。");
        }
    }

    // ===== 资源管理 =====

    // 统一资源修改入口（引导书 5.4 节）。
    // type=资源类型，isIncrease=true增加/false减少，amount=变化量。
    // 每次修改都会发布 RulerResourceChangedEvent 通知其他系统（UI 刷新、成就检测等）。
    // 防御逻辑：Mathf.Abs 防止负数反向操作，Mathf.Max(0) 防止资源变为负数。
    public void ModifyResource(ResourceType type, bool isIncrease, int amount)
    {
        amount = Mathf.Abs(amount);  // 防止负数反向操作

        int oldValue = GetResourceValue(type);
        int newValue = isIncrease ? oldValue + amount : oldValue - amount;
        newValue = Mathf.Max(0, newValue);  // 防止资源变为负数

        SetResourceValue(type, newValue);

        Debug.Log($"[RulerController] {type} {(isIncrease ? "+" : "-")}{amount}，当前: {newValue}");
        EventBus.Publish(new RulerResourceChangedEvent(type, oldValue, newValue));
    }

    // 按资源类型获取当前值
    private int GetResourceValue(ResourceType type)
    {
        return type switch
        {
            ResourceType.Gold => Gold,
            ResourceType.Stone => Stone,
            ResourceType.Wood => Wood,
            ResourceType.Food => Food,
            _ => 0
        };
    }

    // 按资源类型设置值（仅 ModifyResource 内部调用）
    private void SetResourceValue(ResourceType type, int value)
    {
        switch (type)
        {
            case ResourceType.Gold: Gold = value; break;
            case ResourceType.Stone: Stone = value; break;
            case ResourceType.Wood: Wood = value; break;
            case ResourceType.Food: Food = value; break;
        }
    }

    // ===== 君主死亡处理 =====

    // 订阅 UnitDiedEvent，检测君主是否阵亡
    private void OnUnitDied(UnitDiedEvent evt)
    {
        if (evt.Unit == monarchUnit)
        {
            OnMonarchDied();
        }
    }

    // 君主阵亡处理：清除引用，推进游戏状态到 GameOver
    public void OnMonarchDied()
    {
        Debug.LogError("[RulerController] ☠ 君主已阵亡！游戏结束。");
        monarchUnit = null;
        GameStateManager.Instance.SetState(GameState.GameOver);
    }

    // ===== 新建游戏时设置统治者名字 =====

    // 新建游戏时由角色创建面板调用，设置玩家输入的君主名字
    public void SetRulerName(string name)
    {
        if (!string.IsNullOrEmpty(name)) RulerName = name;
    }

    // 新建游戏时按当前难度应用初始国家资源（覆盖 RulerData 资产默认值）。
    // 由 WorldSystem.InitializeWorld 在 DifficultyManager.Initialize 之后调用，
    // 确保难度系统已就绪后再调整资源。
    public void ApplyInitialResourcesFromDifficulty()
    {
        if (DifficultyManager.Instance == null)
        {
            Debug.LogWarning("[RulerController] DifficultyManager 不可用，保留 RulerData 默认资源。");
            return;
        }
        var res = DifficultyManager.Instance.GetInitialResources();
        Gold = Mathf.Max(0, res.gold);
        Stone = Mathf.Max(0, res.stone);
        Wood = Mathf.Max(0, res.wood);
        Food = Mathf.Max(0, res.food);
        Debug.Log($"[RulerController] 按难度应用初始资源: Gold={Gold}, Stone={Stone}, Wood={Wood}, Food={Food}");
    }

    // ===== ISaveable 实现 =====

    // 序列化君主控制器的运行时状态到存档载荷
    public SavePayload SaveState()
    {
        var data = new RulerSaveData
        {
            rulerName = RulerName,
            gold = Gold,
            stone = Stone,
            wood = Wood,
            food = Food
        };
        return new SavePayload
        {
            typeName = typeof(RulerSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    // 从存档载荷恢复君主控制器的运行时状态
    // 注意：不在此处 SpawnMonarch——君主作为单位走 UnitController 的 ISaveable 流程，
    // 如果当前场景里君主还没创建，由 UnitFactory.SpawnFromSave 创建。
    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(RulerSaveData).AssemblyQualifiedName) return;

        var data = JsonUtility.FromJson<RulerSaveData>(payload.json);
        RulerName = string.IsNullOrEmpty(data.rulerName) ? "无名君主" : data.rulerName;
        Gold = data.gold;
        Stone = data.stone;
        Wood = data.wood;
        Food = data.food;
    }
}

// 君主存档数据结构。仅保存国家资源和君主名字，
// 战斗属性由 UnitController 的 ISaveable 单独保存。
[System.Serializable]
public class RulerSaveData
{
    public string rulerName;
    public int gold;
    public int stone;
    public int wood;
    public int food;
}