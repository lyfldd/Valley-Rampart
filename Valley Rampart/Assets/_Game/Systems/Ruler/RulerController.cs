using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RulerController : Singleton<RulerController>, ISaveable
{
    public string SaveId => "RulerController";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    [Header("========== 君主配置引用 ==========")]
    [Tooltip("手动拖入君主的 RulerData 资产（优先使用）。留空则自动从 UnitDataManager 获取。")]
    [SerializeField] private RulerData rulerData;

    [Header("========== 君主出生位置 ==========")]
    [SerializeField] private Vector2 spawnPosition = new Vector2(0f, 0f);

    [Header("========== 运行时状态 ==========")]
    [SerializeField] private UnitController monarchUnit;

    // ===== 公开属性 =====

    //君主的配置数据（ScriptableObject 引用）
    public RulerData RulerData => rulerData;

    //君主运行时单位引用
    public UnitController MonarchUnit
    {
        get
        {
            // 如果引用已失效（对象被销毁），自动清理
            if (monarchUnit != null && monarchUnit.gameObject == null)
            {
                Debug.LogWarning("[RulerController] MonarchUnit 引用已失效（对象已销毁），自动清除。");
                monarchUnit = null;
            }
            return monarchUnit;
        }
    }

    //君主是否存活
    public bool IsMonarchAlive => MonarchUnit != null && MonarchUnit.CurrentHp > 0;

    public int Gold { get; private set; }
    public int Stone { get; private set; }
    public int Wood { get; private set; }
    public int Food { get; private set; }

    /// <summary>统治者名字（新建游戏时玩家输入）。</summary>
    public string RulerName { get; private set; } = "无名君主";

    /// <summary>标记当前实例是否为真正的单例（非重复副本）。用于 OnDestroy 判断是否需要清理。</summary>
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
        EventBus.Subscribe<UnitDataLoadedEvent>(OnUnitDataLoaded);
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);

        SaveManager.Instance.RegisterSaveable(this);
    }
    private void OnDestroy()
    {
        // 只有真正的单例才做了订阅和注册，重复副本跳过清理
        if (!_isRealSingleton) return;

        EventBus.Unsubscribe<UnitDataLoadedEvent>(OnUnitDataLoaded);
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
    }

    //尝试加载君主 RulerData
    //如果 Inspector 里已经手动指定了 rulerData，则直接应用
    //否则从 UnitDataManager 获取
    private void TryLoadRulerData()
    {
        if (rulerData != null)
        {
            Debug.Log($"[RulerController] 使用手动指定的君主数据: {rulerData.name}");
            ApplyRulerData();
            return;
        }

        // UnitDataManager 可能还没创建，访问 Instance 会触发 Singleton 自动创建
        if (!UnitDataManager.Instance.IsInitialized)
        {
            Debug.Log("[RulerController] UnitDataManager 尚未初始化，等待数据加载完成事件...");
            return;
        }

        //从 UnitDataManager 获取君主数据
        FetchRulerDataFromManager();
    }

    private void FetchRulerDataFromManager()
    {
        UnitData data = UnitDataManager.Instance.GetData(Faction.Human_Player, Occupation.Ruler);
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

    /// <summary>
    /// 将 RulerData 资产中的国家资源同步到运行时属性。
    /// 单位级属性（HP/Attack/Defense/Speed）由 UnitController 管理，不在此处理。
    /// </summary>
    private void ApplyRulerData()
    {
        if (rulerData == null) return;

        // 初始国家资源（来自 RulerData 子类）
        Gold = rulerData.initialGold;
        Stone = rulerData.initialStone;
        Wood = rulerData.initialWood;
        Food = rulerData.initialFood;

        Debug.Log($"[RulerController] 已从资产同步国家资源: "
            + $"Gold={Gold}, Stone={Stone}, Wood={Wood}, Food={Food}");
    }

    // UnitDataManager 加载完成的回调。如果此时还没有君主数据，补取一次。
    private void OnUnitDataLoaded(UnitDataLoadedEvent evt)
    {
        if (!evt.IsSuccess) return;

        if (rulerData == null)
        {
            Debug.Log("[RulerController] 收到数据加载完成事件，尝试获取君主数据...");
            FetchRulerDataFromManager();
        }
    }

    //在出生位置创建君主单位，场景上没有君主时代码兜底
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
        Debug.Log("[RulerController] 场景中未找到君主，通过 UnitFactory 创建...");
        UnitFactory.Instance.PreloadAll();

        GameObject rulerGo = UnitFactory.Instance.SpawnUnit(rulerData, spawnPosition);
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

    /// <summary>
    /// 在场景中查找已有的君主单位。
    /// 识别标准（优先级从高到低）：
    ///   1. Data 已初始化 且 faction=Human_Player, occupation=Ruler
    ///   2. Data 已初始化 但带有 PlayerInputHandler（兜底识别）
    ///   3. Data==null 但有 PlayerInputHandler（场景中手动放置但未初始化）
    /// </summary>
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

    /// <summary>
    /// 清理场景中多余的君主单位。只保留第一个找到的，其余销毁。
    /// 返回清理的数量。
    /// </summary>
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

    /// <summary>
    /// R2: 销毁场景中所有手动放置的单位（不仅限于君主）。
    /// 用于读档前清理，避免场景预置单位与存档恢复的单位重复。
    /// 同时清除 monarchUnit 引用。
    /// </summary>
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

    /// <summary>
    /// 读档完成后调用：在场景中查找已恢复的君主单位并绑定。
    /// 新建模式不需要调用（SpawnMonarch 中已完成绑定）。
    /// </summary>
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

    /// <summary>
    /// 统一资源修改入口。type=资源类型，isIncrease=true增加/false减少，amount=变化量。
    /// 每次修改都会发布 RulerResourceChangedEvent 通知其他系统。
    /// </summary>
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

    public void OnMonarchDied()
    {
        Debug.LogError("[RulerController] ☠ 君主已阵亡！游戏结束。");
        monarchUnit = null;
        GameStateManager.Instance.SetState(GameState.GameOver);
    }

    // ===== 新建游戏时设置统治者名字 =====

    /// <summary>新建游戏时由角色创建面板调用。</summary>
    public void SetRulerName(string name)
    {
        if (!string.IsNullOrEmpty(name)) RulerName = name;
    }

    /// <summary>新建游戏时设置起始国家资源（覆盖 RulerData 资产里的初始值）。</summary>
    public void ApplyStartResources(int gold, int stone, int wood, int food)
    {
        Gold = Mathf.Max(0, gold);
        Stone = Mathf.Max(0, stone);
        Wood = Mathf.Max(0, wood);
        Food = Mathf.Max(0, food);
        Debug.Log($"[RulerController] 应用起始资源: Gold={Gold}, Stone={Stone}, Wood={Wood}, Food={Food}");
    }

    // ===== ISaveable 实现 =====

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

[System.Serializable]
public class RulerSaveData
{
    public string rulerName;
    public int gold;
    public int stone;
    public int wood;
    public int food;
}
