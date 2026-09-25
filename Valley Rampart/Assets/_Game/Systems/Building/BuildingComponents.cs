using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 建筑行为组件接口（3.3.4 批次4 组件化架构）。
/// Building 类只管基础状态 + 交互 + 生命周期，具体行为拆成独立 Component 挂在同一 GameObject。
/// BuildingFactory 按 BuildingDef 配置决定挂哪些组件；组件从 def 读配置，不自己持有持久状态。
///
/// 本轮实现：ProducerComponent + StorageComponent（批次5）。
/// 留接口空壳：Pickup/Spawner/Combat/Rift/CastleCore（定义类 + 挂载判断，具体逻辑后续阶段）。
/// </summary>
public interface IBuildingComponent
{
    /// <summary>组件初始化（BuildingFactory 实例化后调用，传入宿主 Building）。</summary>
    void Init(Building building);
}

/// <summary>⭐ `M4-A`（`08` §3.4）：**可被 `ProductionSystem` 每秒 tick 的建筑组件**。
/// `ProductionSystem.TickAll` 只遍历本接口（⛔ 不再点名 4 个具体组件类）⇒ 新增带 `Tick` 的组件
/// 只需实现本接口 ＋ 数据行填键，**不用回来改** `ProductionSystem`。</summary>
public interface ITickable : IBuildingComponent
{
    /// <summary>每秒一次（由 `ProductionSystem` 统一驱动 · 节奏不变）。</summary>
    void Tick();
}

// ===== 留接口空壳组件（3.3.4 批次4：定义类 + 挂载判断，具体逻辑后续阶段）=====

/// <summary>一次性采集组件（宝箱/木头堆/石头堆）。依赖：无。后续阶段实现采集逻辑。</summary>
public class PickupComponent : MonoBehaviour, IBuildingComponent
{
    public void Init(Building building) { }
}

/// <summary>战斗组件（箭塔/弩炮/魔法塔）。依赖：3.4 伤害管线 / 3.5 防御建筑。
/// 2_12 步骤12（P1 接缝）：补全 2_5 射程圆 + 360° 瞄准 + 最近目标（2D 欧氏距离）→ DamageSystem.RegisterAttack。
/// 工事/弹药/AOE 等细分工事规则归 2_5；本组件只做防御建筑"建筑层"战斗接入驱动。</summary>
public class CombatComponent : MonoBehaviour, IBuildingComponent
{
    private Building _building;
    private float _cooldown;
    private const float DefaultAttackCD = 0.5f;   // 攻速回退值（CombatConfig.attackCooldown 未配 0 时用）

    /// <summary>攻击冷却秒（2_12 步骤14：攻速迁 SO——读取 CombConfig.attackCooldown；0 回退默认 0.5s）。</summary>
    private float GetAttackCD()
    {
        if (_building != null && _building.def != null && _building.def.combat.attackCooldown > 0f)
            return _building.def.combat.attackCooldown;
        return DefaultAttackCD;
    }

    /// <summary>当前是否锁定目标（供表现层绘制射程圆/瞄准线）。</summary>
    public bool HasTarget { get; private set; }
    /// <summary>当前瞄准世界坐标（射程圆内最近敌，360° 朝向）。</summary>
    public Vector2 AimPoint { get; private set; }

    /// <summary>是否可开火（工人操作解锁：Catapult 等 crewRequired>0 建筑需工人操作才可发射，改动②）。</summary>
    public bool IsOperational => _building != null && _building.HasEnoughCrew();

    // 【HH.320 件2】单遍查表缓冲（**成员持有** ⇒ 稳态零分配 · ⛔ 禁每次查询 new List）
    private readonly List<UnitController> _queryBuf = new List<UnitController>(64);
    private RectInt _queryRect;

    public void Init(Building building)
    {
        _building = building;
        _cooldown = 0f;
        HasTarget = false;
    }

    private void Update()
    {
        if (_building == null || DamageSystem.Instance == null || GridSystem.Instance == null) return;
        var def = _building.def;
        if (def == null || def.combat.attack <= 0) { HasTarget = false; return; }

        // 工人门控：工人不足停火停机（对齐 sim CrewMachineThinkCore，改动②）
        if (!IsOperational) { HasTarget = false; return; }

        if (_cooldown > 0f) _cooldown -= Time.deltaTime;

        // 2_5 射程圆：圈内最近目标按欧氏距离（360° 无朝向限制）
        // 【HH.320 件2 · D843】射程改**视觉格域**：`def.combat.range` 本身就表「格」（⛔ 不再 ×cellSize 标量 · R5）
        IDamageable target = FindNearestEnemyInRange(def.combat.range);
        HasTarget = target != null;                                   // 【HH.321 批 2 · DZ-4】真 null 判定（保留）
        if (target == null || CombatRules.IsUnityNull(target)) return; // 假 null 判定（接口静态类型 ⇒ 须补）

        AimAt(target.GetPosition());

        if (_cooldown <= 0f)
        {
            _cooldown = GetAttackCD();
            var profile = new AttackProfile
            {
                attack = def.combat.attack,
                range = def.combat.range,
                cd = GetAttackCD(),
                isRanged = true,
                projectileType = ProjectileType.Arrow, // 2_5 按塔种细分弹种（箭塔/弩塔/投掷机）
            };
            DamageSystem.Instance.RegisterAttack(_building, target, profile);
        }
    }

    /// <summary>射程圆内最近敌对单位（2_5 射程圆 · **视觉格域** ＋ 件 3′ 视线过滤）。
    /// 【HH.320 件2/件18】改「**微格超集窗 ＋ 单遍过滤**」：
    ///   ① 窗 ＝ <see cref="GridMath.SubWindowForVisualRadius"/>（**超集**：覆盖视觉格圆所需的子格索引差上界；
    ///      ⛔ 原 `⌈rangeWorld/cellSize⌉` 地块窗**不是**视觉圆的超集 ⇒ 圈内目标可能不进扫描 ⇒ 漏索敌）；
    ///   ② 查 ＝ `GridSystem.FillUnitsInRect`（**单遍** `O(N)` · 无分配；⛔ 原逐格 `GetUnitsInCell` 为 `O(N)/格`
    ///      （121~225 格 × 每次全表枚举）属 D485 已判「不可接受」的同族残留）；
    ///   ③ 过滤 ＝ `DistVisual ≤ range`（与判定/命中同口径）＋ **建筑炮塔全远程** ⇒ **逐个候选**视线过滤
    ///      （件 3′：⛔ 非「任一无效即整体放弃」，须跳过无视线者继续找次近）。</summary>
    private IDamageable FindNearestEnemyInRange(float rangeVisual)
    {
        var grid = GridSystem.Instance;
        if (grid == null || grid.Config == null) return null;
        var centerOpt = grid.WorldToSubCoord(_building.transform.position);
        if (!centerOpt.HasValue) return null;
        GridCoord c = centerOpt.Value;
        int n = GridMath.SubWindowForVisualRadius(rangeVisual, grid.Config.subCellDivisor);
        _queryRect = new RectInt(c.x - n, c.y - n, 2 * n + 1, 2 * n + 1);   // RectInt＝值类型 ⇒ 无分配
        _queryBuf.Clear();
        grid.FillUnitsInRect(_queryRect, _queryBuf);

        Vector2 myPos = _building.transform.position;
        IDamageable nearest = null;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < _queryBuf.Count; i++)
        {
            var uc = _queryBuf[i];
            if (uc == null || !uc.IsAlive || uc.CurrentHp <= 0) continue;
            var f = uc.GetFaction();
            if (f == _building.GetFaction() || f == Faction.None) continue;
            Vector2 p = uc.transform.position;
            float d = GridMath.DistVisual(myPos, p);
            if (d > rangeVisual || d >= nearestDist) continue;
            if (!CombatRules.HasLineOfSight(myPos, p)) continue;   // 件 3′：无视线 ⇒ 跳过该候选（继续找次近）
            nearestDist = d;
            nearest = uc;
        }
        return nearest;
    }

    /// <summary>360° 旋转朝目标（2D z 朝向瞄准线）。</summary>
    private void AimAt(Vector2 targetPos)
    {
        AimPoint = targetPos;
        Vector2 dir = targetPos - (Vector2)_building.transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        _building.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}

/// <summary>主城核心组件（HQ 面板 / 科技解锁 / 失败条件）。批次7 做最小实现支撑主城流程。</summary>
public class CastleCoreComponent : MonoBehaviour, IBuildingComponent
{
    public void Init(Building building)
    {
        // 2_12 步骤5：主城挂王座/旗帜锚点（ThroneAnchor，D2/D49/D249）——王国失败判定锚点。
        // 上帝视角无君主实体，IsKingdomLost = 工人全灭（D249 终审：主城被破不再判负）。
        if (building != null && ThroneAnchor.Instance == null)
            building.gameObject.AddComponent<ThroneAnchor>().castle = building;

        // 2_12 步骤8.4：主城挂国库仓库（HH.16 裁决 B 多仓库聚合）——非金资源真源。
        if (building != null && building.gameObject.GetComponent<TreasureVault>() == null)
            building.gameObject.AddComponent<TreasureVault>()?.Init(building);
    }
}

// ============================================================================
//  ⭐ `M4-A`（`08` §3.3「组件注册表」／§7.3「数据行显式列表」）：
//  **键 → 组件类的唯一映射表**。`BuildingFactory.AttachComponents` 只做
//  「遍历 `BuildingDef.components` ＋ 查本表」，⛔ 不再由 9 处 `if` 决定挂什么。
//  加一种内部行为 ＝ ① 写一个组件类 ② 在本表登记一个键 ③ 数据行填键。
// ============================================================================
public static class BuildingComponentRegistry
{
    /// <summary>绑定器：把该键对应的组件挂到建筑 Go 上（同类型已挂则跳过）并 `Init`。</summary>
    public delegate bool Binder(GameObject go, Building b);

    // ===== 键名（与 `08` §7.3 的 `comp.*` 命名一致）=====
    public const string Storage       = "comp.storage";
    public const string Producer      = "comp.producer";
    public const string Blacksmith    = "comp.blacksmith";
    public const string SiegeWorkshop = "comp.siege_workshop";
    public const string MineByproduct = "comp.mine_byproduct";
    public const string Combat        = "comp.combat";
    public const string Pickup        = "comp.pickup";
    public const string CastleCore    = "comp.castle_core";

    private static readonly Dictionary<string, Binder> _byKey = new Dictionary<string, Binder>();

    static BuildingComponentRegistry()
    {
        Register(Storage,       Add<StorageComponent>);
        Register(Producer,      Add<ProducerComponent>);
        Register(Blacksmith,    Add<BlacksmithBuilding>);
        Register(SiegeWorkshop, Add<SiegeWorkshopBuilding>);
        Register(MineByproduct, Add<MineByproductComponent>);
        Register(Combat,        Add<CombatComponent>);
        Register(Pickup,        Add<PickupComponent>);
        // ⚠️ 本键**保「来源守卫」**（`M4-A` 等价要求）：其判定读的是 **`b.sourceType`**（运行时来源），
        //   而玩家建造路径（`Building.Init` ⇒ `sourceType = BuildingType.None`）与调试路径
        //   （`AIDebugSpawnController` 传 `BuildingType.None`）都不是 def 的 sourceType
        //   ⇒ 若只看数据行，这两条路径会**多挂**组件（以 `castle` 为例）⇒ 守卫令其与改前零差异。
        Register(CastleCore,    (go, b) => b != null && b.sourceType == BuildingType.CastleCore
                                            ? Add<CastleCoreComponent>(go, b) : true);
    }

    /// <summary>登记／覆盖一个键（扩展口：Editor 探针可临时登记测试组件，⛔ 不写生产数据行）。</summary>
    public static void Register(string key, Binder binder)
    {
        if (string.IsNullOrEmpty(key) || binder == null) return;
        _byKey[key] = binder;
    }

    /// <summary>按数据行的键挂组件；返回 `false` ＝ **该键未登记**（调用方须告警·死数据可见）。</summary>
    public static bool TryAttach(string key, Building b)
    {
        if (b == null || string.IsNullOrEmpty(key)) return true;
        if (!_byKey.TryGetValue(key, out var binder) || binder == null) return false;
        binder(b.gameObject, b);
        return true;
    }

    /// <summary>已登记键数（自检/探针读口）。</summary>
    public static int Count => _byKey.Count;

    /// <summary>该键是否已登记（自检/探针读口）。</summary>
    public static bool Has(string key) => !string.IsNullOrEmpty(key) && _byKey.ContainsKey(key);

    /// <summary>挂组件：**同类型已挂 ⇒ 跳过**（⭐ 结构保「同一栋不许挂两个 `StorageComponent`」）。</summary>
    private static bool Add<T>(GameObject go, Building b) where T : Component, IBuildingComponent
    {
        if (go == null) return true;
        if (go.GetComponent<T>() != null) return true;
        var c = go.AddComponent<T>();
        if (c == null) return false;
        c.Init(b);
        return true;
    }
}
