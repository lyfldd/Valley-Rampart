using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 君主控制器（RulerController）
// 职责：管理玩家君主单位的生命周期、国家资源和游戏结束条件。
//
// 核心设计（引导书第 5 节）：
//   - 单例模式：全局唯一，挂在 MainMenuScene 的独立 GameObject 上，DontDestroyOnLoad 跟随场景。
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
    [Tooltip("兜底出生位置（地图未就绪/找不到城堡时用）。正常流程：废弃城堡左侧 1 个小区块距离，动态计算")]
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
    // 2_12 步骤8.4（HH.16 裁决 B：多仓库聚合）：非金资源真源迁国库仓库（主城 TreasureVault），
    // 旧字段改为只读中转国库；金(Gold)=货币直通保留字段（HH.8）。
    // ===== 3.5 P1 粮大类子资源（§13.11 特殊食物/肉；真源同迁国库）=====
    public int Stone => GetResourceValue(ResourceType.Stone);
    public int Wood => GetResourceValue(ResourceType.Wood);
    public int Food => GetResourceValue(ResourceType.Food);
    public int SpecialFood => GetResourceValue(ResourceType.SpecialFood);
    public int Meat => GetResourceValue(ResourceType.Meat);
    // ===== 2_12 步骤8 铁（D199；真源同迁国库铁仓库）=====
    public int Metal => GetResourceValue(ResourceType.Metal);

    // 统治者名字（新建游戏时玩家输入，存档恢复时从 RulerSaveData 读取）
    public string RulerName { get; private set; } = "无名君主";

    // 战斗属性（Attack/Defense/WalkSpeed/RunSpeed/Hp）已移至 UnitController
    // 访问方式：RulerController.Instance.MonarchUnit.Attack 等

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;  // 重复实例，base 已销毁 gameObject

        // 尝试加载君主数据并应用（Inspector 优先，其次 UnitDataManager）
        // 所有属性在 ApplyRulerData() 中从资产同步，无需 initial* 兜底字段
        TryLoadRulerData();

        // 订阅事件
        // 注：UnitDataLoadedEvent 已废弃（LoadManager 改发 ConfigsLoadedEvent），
        // 君主数据在 SpawnMonarch 时由 LoadManager.GetUnitData 获取，不依赖事件。
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);

        SaveManager.Instance.RegisterSaveable(this);
    }

    protected override void OnDestroy()
    {
        if (_instance != this) return;  // 不是当前单例，跳过清理

        base.OnDestroy();
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

        // 2_12 步骤8.4：金=货币直通字段；非金初始由难度初始化/读档统一入国库，不再直写旧字段。
        Gold = rulerData.initialGold;
        Debug.Log($"[RulerController] 已从资产同步君主数据: {rulerData.name}");
    }

    // 在出生位置创建君主单位，场景上没有君主时代码兜底
    // 流程：验证已有引用 → 清理重复 → 确保数据 → 查找已有 → 代码创建
    // 3.5.1 E-S3：新建游戏君主必落废弃城堡旁——历史遗留（跨局残留/场景预置君主停在旧位置）
    // 通过绑定后统一 Teleport(spawnPos) 根治。
    public void SpawnMonarch()
    {
        // 出生位置先算好（废弃城堡左侧 1 格；地图未就绪回退 Inspector spawnPosition）
        Vector2 spawnPos = ResolveSpawnPosition();

        // Step 0: 验证已有引用是否仍然有效
        if (monarchUnit != null)
        {
            if (monarchUnit.gameObject != null)  // Unity null-check：对象未被销毁
            {
                Debug.LogWarning("[RulerController] 君主已存在，跳过重复创建（强制归位城堡旁）。");
                monarchUnit.Teleport(spawnPos);
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
            // E-S3：绑定后强制归位城堡旁（根治跨局残留/预置单位停在旧位置的老毛病）
            monarchUnit.Teleport(spawnPos);
            Debug.Log($"[RulerController] 君主已归位废弃城堡旁: {spawnPos}");
            return;
        }

        // Step 4: 场景中确实没有君主，代码兜底创建
        // Prefab 已由 LoadManager 阶段1 预加载，无需再 PreloadAll
        Debug.Log($"[RulerController] 场景中未找到君主，通过 UnitFactory 创建（位置={spawnPos}）...");
        GameObject rulerGo = LoadManager.Instance.SpawnUnit(rulerData, spawnPos);
        if (rulerGo != null)
        {
            monarchUnit = rulerGo.GetComponent<UnitController>();
            Debug.Log($"[RulerController] 君主已创建: "
                + $"位置={spawnPos}, "
                + $"HP={monarchUnit.CurrentHp}/{rulerData.maxHp}, "
                + $"攻击={rulerData.attack}");
        }
        else
        {
            Debug.LogError("[RulerController] 君主 Prefab 实例化失败！");
        }
    }

    /// <summary>
    /// 计算君主出生位置：废弃城堡左侧 1 格（固定左边），走 WorldManager 王国锚点（3.5.1 E-S3 统一）。
    /// HH.3 裁决 2026-08-22 统一 iso：左侧偏移取等轴邻格世界差（CoordToWorld 同一条映射），不再用 cellSize.x 当正交标量。
    /// 地图未就绪或找不到城堡时，回退 Inspector 配置的 spawnPosition。
    /// </summary>
    private Vector2 ResolveSpawnPosition()
    {
        var wm = WorldManager.Instance;
        var grid = GridSystem.Instance;
        if (wm != null && grid != null && grid.Config != null)
        {
            var map = wm.ActiveMap;
            if (map != null)
            {
                Vector2 anchor = wm.GetKingdomAnchorWorld();
                if (anchor != Vector2.zero)
                {
                    // 左侧 1 格 = 中心格 (x-1,y) 的等轴世界差（doc 1 §1.6，iso origin-free）
                    var centerCoord = new GridCoord(map.width / 2, map.height / 2);
                    Vector2 leftNeighbor = grid.CoordToWorld(new GridCoord(centerCoord.x - 1, centerCoord.y));
                    return anchor + (leftNeighbor - anchor);
                }
            }
        }
        return spawnPosition;
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

    // ===== 状态重置（由 TeardownManager 返回主菜单时调用）=====

    /// <summary>
    /// 重置运行时状态到默认值。不反订阅、不反注册 ISaveable（Manager 保留，订阅和注册继续用）。
    /// </summary>
    public void ResetState()
    {
        monarchUnit = null;
        RulerName = "无名君主";
        Gold = 0;
        // 2_12 步骤8.4：非金真源=国库仓库，随主城一并清空
        TreasureVault.Instance?.ResetAll();
        Debug.Log("[RulerController] ResetState: 引用已清除，资源归零");
    }

    /// <summary>清除君主引用（单位已在外部销毁时使用，如 TeardownManager.TeardownScene）。</summary>
    public void ClearMonarchReference()
    {
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
    //
    // TODO(2_12步骤8)：随 Ruler 全量迁移到仓库系统(IWarehouse)退役当前真源记账。HH.8 裁决分批A。
    //   * 金(Gold)=货币不占存储，本方法保留直通；
    //   * 非金资源在步骤3~7 阶段仍是真源（本步稳定为兼容壳的语义就位，不双写、不改逻辑）；
    //   * 禁双写红线：不得在 Ruler 字段 与 IWarehouse.Deposit 对新账并行记账（会资源复制）——迁移完成一次性切换真源。
    public void ModifyResource(ResourceType type, bool isIncrease, int amount)
    {
        amount = Mathf.Abs(amount);  // 防止负数反向操作

        // 金：货币直通，字段记账（HH.8）。
        if (type == ResourceType.Gold)
        {
            int old = Gold;
            int nv = Mathf.Max(0, isIncrease ? old + amount : old - amount);
            Gold = nv;
            Debug.Log($"[RulerController] 金 {(isIncrease ? "+" : "-")}{amount}，当前: {nv}");
            EventBus.Publish(new RulerResourceChangedEvent(type, old, nv));
            return;
        }

        // 2_12 步骤8.4（禁双写红线物理落点，HH.8）：非金真源=国库仓库，旧字段退役。
        var tv = TreasureVault.Instance;
        if (tv == null)
        {
            Debug.LogWarning($"[RulerController] 国库未就绪，忽略非金资源变化: {type} {amount}");
            return;
        }
        int before = tv.GetAmount(type);
        int moved = isIncrease ? tv.Deposit(type, amount) : tv.Take(type, amount);
        int after = tv.GetAmount(type);
        Debug.Log($"[RulerController] 国库 {type} {(isIncrease ? "+" : "-")}{moved}，当前: {after}");
        EventBus.Publish(new RulerResourceChangedEvent(type, before, after));
    }

    // ===== 资源包批量操作（3.3.1 P7，供 BuildController / BuildingPanel 用）=====

    /// <summary>公共资源读取（供王国经济系统/贸易查询当前持有量）。</summary>
    public int GetResource(ResourceType type) => GetResourceValue(type);

    /// <summary>单资源是否足够（供 UI 造价行逐项高亮用）。</summary>
    public bool HasAmount(ResourceType type, int amount)
    {
        if (amount <= 0) return true;
        return GetResourceValue(type) >= amount;
    }

    /// <summary>是否负担得起该资源包（四资源全部满足，原子校验）。</summary>
    public bool CanAfford(ResourcePack cost)
    {
        // 2_12 步骤8 D131：工事升级含铁（metal），纳入原子校验
        return Gold >= cost.gold && Stone >= cost.stone && Wood >= cost.wood && Food >= cost.food && Metal >= cost.metal;
    }

    /// <summary>扣除资源包（调用前需先 CanAfford；逐项调 ModifyResource 保证事件发布）。</summary>
    public void Spend(ResourcePack cost)
    {
        if (cost.gold > 0) ModifyResource(ResourceType.Gold, false, cost.gold);
        if (cost.stone > 0) ModifyResource(ResourceType.Stone, false, cost.stone);
        if (cost.wood > 0) ModifyResource(ResourceType.Wood, false, cost.wood);
        if (cost.food > 0) ModifyResource(ResourceType.Food, false, cost.food);
        if (cost.metal > 0) ModifyResource(ResourceType.Metal, false, cost.metal);
    }

    /// <summary>按比例退还资源包（拆除退款 ratio=0.5）。metal 随比退还，不静默丢铁。</summary>
    public void Refund(ResourcePack cost, float ratio = 1.0f)
    {
        if (cost.gold > 0) ModifyResource(ResourceType.Gold, true, Mathf.RoundToInt(cost.gold * ratio));
        if (cost.stone > 0) ModifyResource(ResourceType.Stone, true, Mathf.RoundToInt(cost.stone * ratio));
        if (cost.wood > 0) ModifyResource(ResourceType.Wood, true, Mathf.RoundToInt(cost.wood * ratio));
        if (cost.food > 0) ModifyResource(ResourceType.Food, true, Mathf.RoundToInt(cost.food * ratio));
        if (cost.metal > 0) ModifyResource(ResourceType.Metal, true, Mathf.RoundToInt(cost.metal * ratio));
    }

    // 按资源类型获取当前值
    private int GetResourceValue(ResourceType type)
    {
        if (type == ResourceType.Gold) return Gold;   // 金=货币直通字段
        // 2_12 步骤8.4：非金真源=国库仓库（HH.16 裁决 B）
        return TreasureVault.Instance != null ? TreasureVault.Instance.GetAmount(type) : 0;
    }

    // ===== 君主死亡处理 =====

    // 订阅 UnitDiedEvent，检测君主是否阵亡
    // 3.4：evt.Unit 改为 IDamageable，需 as UnitController 判等（君主是 UnitController）
    private void OnUnitDied(UnitDiedEvent evt)
    {
        if (evt.Unit as UnitController == monarchUnit)
        {
            OnMonarchDied();
        }
    }

    // 君主阵亡处理（2_12 步骤8.4 / D249 修订：君主死亡**不再判负**）。
    // GameOver 唯一条件=工人全灭（ThroneAnchor.IsKingdomLost 轮询驱动，见 ThroneAnchor.Update）。
    // 君主死亡仅清引用+记日志；王国只要存有工人即可继续（破城期风味，D164）。
    public void OnMonarchDied()
    {
        // ⚠️ 君主阵亡是正常游戏流程，用 Log 而非 LogError，避免触发 Error Pause 冻结 UI。
        Debug.Log("[RulerController] 君主阵亡（D249：不再判负）。工人仍在则王国继续。");
        monarchUnit = null;
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
        // 2_12 步骤8.4：金=货币直通字段；非金一次性绝对入国库（先清后入，防累积重复）
        Gold = Mathf.Max(0, res.gold);
        var tv = TreasureVault.Instance;
        if (tv != null)
        {
            tv.ResetAll();
            DepositToTreasury(ResourceType.Stone, res.stone);
            DepositToTreasury(ResourceType.Wood, res.wood);
            DepositToTreasury(ResourceType.Food, res.food);
        }
        else
        {
            Debug.LogWarning("[RulerController] 按难度初始化：国库未就绪，非金初始暂不落地");
        }
        Debug.Log($"[RulerController] 按难度应用初始资源: Gold={Gold}, Stone={tv?.GetAmount(ResourceType.Stone) ?? 0}, Wood={tv?.GetAmount(ResourceType.Wood) ?? 0}, Food={tv?.GetAmount(ResourceType.Food) ?? 0}");
    }

    /// <summary>非金资源一次性入国库（初始/读档迁移共用；绝对置入前提是国库已清空）。</summary>
    private void DepositToTreasury(ResourceType type, int amount)
    {
        if (amount <= 0) return;
        TreasureVault.Instance?.Deposit(type, amount);
    }

    // ===== ISaveable 实现 =====

    // 序列化君主控制器的运行时状态到存档载荷
    public SavePayload SaveState()
    {
        var data = new RulerSaveData
        {
            rulerName = RulerName,
            gold = Gold,
            // 2_12 步骤8.4（修正1：保留字段 + 读档迁入 + 写档置零）：
            // 非金真源已迁国库仓库，此处固定写 0；字段保留供旧档读档迁移期识别。
            stone = 0, wood = 0, food = 0,
            specialFood = 0, meat = 0
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
        // 2_12 步骤8.4：金=货币直通字段恢复。
        Gold = data.gold;
        // 修正1：旧档非金字段检测到非零 → 一次性迁入国库（防读档回退）；
        // 若国库未就绪（读档时序），先缓存在 _pendingLoadTreasury，待国库就绪后补入——见 EnsureTreasuryMigration()。
        _pendingLoadTreasury = null;
        if (data.stone > 0 || data.wood > 0 || data.food > 0 || data.specialFood > 0 || data.meat > 0)
        {
            _pendingLoadTreasury = new ResourcePack
            {
                stone = data.stone, wood = data.wood, food = data.food
            };
            // 特殊食物/肉不在 ResourcePack，单独缓存
            _pendingLoadSpecial = data.specialFood;
            _pendingLoadMeat = data.meat;
            EnsureTreasuryMigration();
        }
    }

    // 旧档非金读档迁移缓存（国库未就绪时先存，Info/Global 之后由 EnsureTreasuryMigration 补入）
    private ResourcePack? _pendingLoadTreasury;
    private int _pendingLoadSpecial;
    private int _pendingLoadMeat;

    /// <summary>确保旧档缓存非金迁入国库（国库就绪后调用，防读档回退）。</summary>
    public void EnsureTreasuryMigration()
    {
        var tv = TreasureVault.Instance;
        if (tv == null || !_pendingLoadTreasury.HasValue) return;
        var p = _pendingLoadTreasury.Value;
        DepositToTreasury(ResourceType.Stone, p.stone);
        DepositToTreasury(ResourceType.Wood, p.wood);
        DepositToTreasury(ResourceType.Food, p.food);
        DepositToTreasury(ResourceType.SpecialFood, _pendingLoadSpecial);
        DepositToTreasury(ResourceType.Meat, _pendingLoadMeat);
        _pendingLoadTreasury = null;
        Debug.Log("[RulerController] 旧档非金资源已迁入国库");
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
    // ===== 3.5 P1（v1 兼容：旧档缺字段 JsonUtility 给默认 0）=====
    public int specialFood;
    public int meat;
}