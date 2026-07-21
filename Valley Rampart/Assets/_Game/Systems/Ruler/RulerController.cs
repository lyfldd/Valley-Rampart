using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RulerController : Singleton<RulerController>
{
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
    public UnitController MonarchUnit => monarchUnit;

    //君主是否存活
    public bool IsMonarchAlive => monarchUnit != null && monarchUnit.CurrentHp > 0;

    public int Gold { get; private set; }
    public int Stone { get; private set; }
    public int Wood { get; private set; }
    public int Food { get; private set; }

    // 战斗属性（Attack/Defense/WalkSpeed/RunSpeed/Hp）已移至 UnitController
    // 访问方式：RulerController.Instance.MonarchUnit.Attack 等

    protected override void Awake()
    {
        base.Awake();

        // 尝试加载君主数据并应用（Inspector 优先，其次 UnitDataManager）
        // 所有属性在 ApplyRulerData() 中从资产同步，无需 initial* 兜底字段
        TryLoadRulerData();

        // 订阅事件
        EventBus.Subscribe<UnitDataLoadedEvent>(OnUnitDataLoaded);
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
    }
    private void OnDestroy()
    {
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
        if (monarchUnit != null)
        {
            Debug.LogWarning("[RulerController] 君主已存在，跳过重复创建。");
            return;
        }

        // 确保我们有君主的配置数据
        if (rulerData == null)
        {
            FetchRulerDataFromManager();
        }

        if (rulerData == null)
        {
            Debug.LogError("[RulerController] 没有君主 RulerData，无法创建君主！");
            return;
        }

        // 先检查场景里是否已手动放了君主（手动创建优先）
        monarchUnit = FindExistingMonarch();

        if (monarchUnit != null)
        {
            // 场景里已有君主，给它注入数据（手动放的没有经过 UnitFactory.Initialize）
            monarchUnit.Initialize(rulerData);
            Debug.Log($"[RulerController] 使用场景中已有的君主: {monarchUnit.name}，已注入数据");
            return;
        }

        // 场景里没有，代码兜底创建
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
    /// 通过 UnitController.Data 的 faction + occupation 判断是否为君主。
    /// 未初始化的（Data==null）跳过。
    /// </summary>
    private UnitController FindExistingMonarch()
    {
        // 查找所有 UnitController，找 faction=Human_Player 且 occupation=Ruler 的
        UnitController[] allUnits = FindObjectsOfType<UnitController>();
        foreach (var unit in allUnits)
        {
            if (unit.Data != null &&
                unit.Data.faction == Faction.Human_Player &&
                unit.Data.occupation == Occupation.Ruler)
            {
                return unit;
            }
        }

        // 也可能手动放了但还没初始化（Data==null）
        // 用 PlayerInputHandler 组件识别君主——只有君主才会挂这个组件
        foreach (var unit in allUnits)
        {
            if (unit.Data == null && unit.GetComponent<PlayerInputHandler>() != null)
            {
                return unit;
            }
        }

        return null;
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
}
