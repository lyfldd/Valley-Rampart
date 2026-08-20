using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI 调试 - 手动放置单位控制器（后端接口）。
///
/// 职责：为 F1 调试面板提供"手动生成单位"能力。
/// UI 只调用本控制器的公开方法，不直接访问 UnitFactory / GridSystem。
///
/// 支持的单位（DebugSpawnType 枚举）：
///   己方：将军 / 近战士兵 / 弓箭手 / 工人
///   敌方：近战士兵 / 弓箭手
///
/// 前端 UI 用法：
///   - AIDebugSpawnController.Instance.GetAvailableTypes() -> 获取可生成清单（渲染按钮）
///   - AIDebugSpawnController.Instance.Spawn(type, worldPos) -> 在指定位置生成单位
///   - AIDebugSpawnController.Instance.Spawn(type) -> 在鼠标位置生成单位（内部转换）
/// </summary>
public enum DebugSpawnType
{
    // ===== 己方（Human_Player）=====
    PlayerGeneral,      // 将军（挂 FormationController，统帅编队）
    PlayerWarrior,      // 近战士兵
    PlayerArcher,       // 弓箭手
    PlayerCivilian,     // 工人
    PlayerCavalry,      // 骑兵（3.6 §五：冲锋 + 击飞）

    // ===== 敌方（Undead）=====
    EnemyWarrior,       // 敌方近战士兵
    EnemyArcher,        // 敌方弓箭手
    EnemyGeneral,       // 敌方将军（3.0.1_6 §4.3：挂 FormationController + FormationTable_Enemy）
    EnemyCavalry,       // 敌方骑兵（3.6）

    // ===== 3.7 新职业（己方 Human_Player，验证 M8 全兵种）=====
    PlayerMage,             // 法师：远程高伤
    PlayerArchmage,         // 大法师：远程更远
    PlayerCrossbowman,      // 弩手：远程点杀
    PlayerHealer,           // 治疗师
    PlayerHeavyWarrior,     // 重装战士：高防近战
    PlayerShieldGuard,      // 盾卫：高防抗线
    PlayerBishop,           // 主教：远程治疗

    // ===== 3.7 机器/工事（己方静态单位）=====
    PlayerSiegeMachine,     // 投掷机（弹药：石/火/魔）
    PlayerBallista,         // 弩炮（弹药：重弩矢）
    PlayerArrowTower,       // 箭塔
    PlayerBarricade,        // 拒马
    PlayerWall,             // 城墙
    PlayerGate,             // 城门

    // ===== 3.7 新职业（敌方 Undead）=====
    EnemyMage,              // 法师
    EnemyArchmage,          // 大法师
    EnemyCrossbowman,       // 弩手
    EnemyHealer,            // 治疗师
    EnemyHeavyWarrior,      // 重装战士
    EnemyShieldGuard,       // 盾卫
    EnemyBishop,            // 主教
}

/// <summary>可生成单位的描述（供 UI 渲染按钮）</summary>
public struct DebugSpawnOption
{
    public DebugSpawnType Type;
    public string DisplayName;   // 按钮显示名
    public string FactionName;   // 阵营显示名
}

/// <summary>手动放置单位结果</summary>
public struct DebugSpawnResult
{
    public bool Success;
    public string Message;
    public GameObject Spawned;
}

public class AIDebugSpawnController : MonoBehaviour
{
    private static AIDebugSpawnController _instance;
    /// <summary>单例（轻量级，不 DontDestroyOnLoad）</summary>
    public static AIDebugSpawnController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<AIDebugSpawnController>();
                if (_instance == null)
                {
                    var go = new GameObject("[AIDebugSpawnController]");
                    _instance = go.AddComponent<AIDebugSpawnController>();
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    // ===== 单位类型配置（Type -> Faction/Occupation 映射）=====

    /// <summary>单位类型对应的阵营</summary>
    private static Faction GetFaction(DebugSpawnType type)
    {
        switch (type)
        {
            case DebugSpawnType.PlayerGeneral:
            case DebugSpawnType.PlayerWarrior:
            case DebugSpawnType.PlayerArcher:
            case DebugSpawnType.PlayerCivilian:
            case DebugSpawnType.PlayerCavalry:
            case DebugSpawnType.PlayerMage:
            case DebugSpawnType.PlayerArchmage:
            case DebugSpawnType.PlayerCrossbowman:
            case DebugSpawnType.PlayerHealer:
            case DebugSpawnType.PlayerHeavyWarrior:
            case DebugSpawnType.PlayerShieldGuard:
            case DebugSpawnType.PlayerBishop:
            case DebugSpawnType.PlayerSiegeMachine:
            case DebugSpawnType.PlayerBallista:
            case DebugSpawnType.PlayerArrowTower:
            case DebugSpawnType.PlayerBarricade:
            case DebugSpawnType.PlayerWall:
            case DebugSpawnType.PlayerGate:
                return Faction.Human_Player;
            case DebugSpawnType.EnemyWarrior:
            case DebugSpawnType.EnemyArcher:
            case DebugSpawnType.EnemyGeneral:
            case DebugSpawnType.EnemyCavalry:
            case DebugSpawnType.EnemyMage:
            case DebugSpawnType.EnemyArchmage:
            case DebugSpawnType.EnemyCrossbowman:
            case DebugSpawnType.EnemyHealer:
            case DebugSpawnType.EnemyHeavyWarrior:
            case DebugSpawnType.EnemyShieldGuard:
            case DebugSpawnType.EnemyBishop:
                return Faction.Undead;
            default:
                return Faction.None;
        }
    }

    /// <summary>单位类型对应的职业</summary>
    private static Occupation GetOccupation(DebugSpawnType type)
    {
        switch (type)
        {
            case DebugSpawnType.PlayerGeneral: return Occupation.General;
            case DebugSpawnType.PlayerWarrior: return Occupation.Warrior;
            case DebugSpawnType.PlayerArcher: return Occupation.Archer;
            case DebugSpawnType.PlayerCivilian: return Occupation.Civilian;
            case DebugSpawnType.PlayerCavalry: return Occupation.Cavalry;
            case DebugSpawnType.PlayerMage: return Occupation.Mage;
            case DebugSpawnType.PlayerArchmage: return Occupation.Archmage;
            case DebugSpawnType.PlayerCrossbowman: return Occupation.Crossbowman;
            case DebugSpawnType.PlayerHealer: return Occupation.Healer;
            case DebugSpawnType.PlayerHeavyWarrior: return Occupation.HeavyWarrior;
            case DebugSpawnType.PlayerShieldGuard: return Occupation.ShieldGuard;
            case DebugSpawnType.PlayerBishop: return Occupation.Bishop;
            case DebugSpawnType.PlayerSiegeMachine: return Occupation.SiegeMachine;
            case DebugSpawnType.PlayerBallista: return Occupation.Ballista;
            case DebugSpawnType.PlayerArrowTower: return Occupation.ArrowTower;
            case DebugSpawnType.PlayerBarricade: return Occupation.Barricade;
            case DebugSpawnType.PlayerWall: return Occupation.Wall;
            case DebugSpawnType.PlayerGate: return Occupation.Gate;
            case DebugSpawnType.EnemyWarrior: return Occupation.Warrior;
            case DebugSpawnType.EnemyArcher: return Occupation.Archer;
            case DebugSpawnType.EnemyGeneral: return Occupation.General;
            case DebugSpawnType.EnemyCavalry: return Occupation.Cavalry;
            case DebugSpawnType.EnemyMage: return Occupation.Mage;
            case DebugSpawnType.EnemyArchmage: return Occupation.Archmage;
            case DebugSpawnType.EnemyCrossbowman: return Occupation.Crossbowman;
            case DebugSpawnType.EnemyHealer: return Occupation.Healer;
            case DebugSpawnType.EnemyHeavyWarrior: return Occupation.HeavyWarrior;
            case DebugSpawnType.EnemyShieldGuard: return Occupation.ShieldGuard;
            case DebugSpawnType.EnemyBishop: return Occupation.Bishop;
            default: return Occupation.Civilian;
        }
    }

    /// <summary>单位类型显示名</summary>
    private static string GetDisplayName(DebugSpawnType type)
    {
        switch (type)
        {
            case DebugSpawnType.PlayerGeneral: return "己方将军";
            case DebugSpawnType.PlayerWarrior: return "己方士兵";
            case DebugSpawnType.PlayerArcher: return "己方弓手";
            case DebugSpawnType.PlayerCivilian: return "己方工人";
            case DebugSpawnType.PlayerCavalry: return "己方骑兵";
            case DebugSpawnType.PlayerMage: return "己方法师";
            case DebugSpawnType.PlayerArchmage: return "己方大法师";
            case DebugSpawnType.PlayerCrossbowman: return "己方弩手";
            case DebugSpawnType.PlayerHealer: return "己方治疗师";
            case DebugSpawnType.PlayerHeavyWarrior: return "己方重装";
            case DebugSpawnType.PlayerShieldGuard: return "己方盾卫";
            case DebugSpawnType.PlayerBishop: return "己方主教";
            case DebugSpawnType.PlayerSiegeMachine: return "己方投掷机";
            case DebugSpawnType.PlayerBallista: return "己方弩炮";
            case DebugSpawnType.PlayerArrowTower: return "己方箭塔";
            case DebugSpawnType.PlayerBarricade: return "己方拒马";
            case DebugSpawnType.PlayerWall: return "己方城墙";
            case DebugSpawnType.PlayerGate: return "己方城门";
            case DebugSpawnType.EnemyWarrior: return "敌方士兵";
            case DebugSpawnType.EnemyArcher: return "敌方弓手";
            case DebugSpawnType.EnemyGeneral: return "敌方将军";
            case DebugSpawnType.EnemyCavalry: return "敌方骑兵";
            case DebugSpawnType.EnemyMage: return "敌方法师";
            case DebugSpawnType.EnemyArchmage: return "敌方大法师";
            case DebugSpawnType.EnemyCrossbowman: return "敌方弩手";
            case DebugSpawnType.EnemyHealer: return "敌方治疗师";
            case DebugSpawnType.EnemyHeavyWarrior: return "敌方重装";
            case DebugSpawnType.EnemyShieldGuard: return "敌方盾卫";
            case DebugSpawnType.EnemyBishop: return "敌方主教";
            default: return "未知";
        }
    }

    private static string GetFactionName(DebugSpawnType type)
    {
        return GetFaction(type) == Faction.Human_Player ? "己方" : "敌方";
    }

    // ===== 公开接口（UI 调用）=====

    /// <summary>
    /// 获取可生成单位清单（供 UI 动态渲染按钮）。
    /// </summary>
    public List<DebugSpawnOption> GetAvailableTypes()
    {
        var result = new List<DebugSpawnOption>();
        foreach (DebugSpawnType type in System.Enum.GetValues(typeof(DebugSpawnType)))
        {
            result.Add(new DebugSpawnOption
            {
                Type = type,
                DisplayName = GetDisplayName(type),
                FactionName = GetFactionName(type),
            });
        }
        return result;
    }

    /// <summary>
    /// 在指定世界位置生成单位。
    /// UI 传 worldPos（世界坐标，y 基线 -3 自动吸附）。
    /// </summary>
    public DebugSpawnResult Spawn(DebugSpawnType type, Vector2 worldPos)
    {
        // 预检查：UnitFactory / UnitDataManager 就绪
        if (UnitFactory.Instance == null || UnitDataManager.Instance == null || !UnitDataManager.Instance.IsInitialized)
        {
            return new DebugSpawnResult { Success = false, Message = "单位工厂未就绪（等加载完成）" };
        }

        // y 吸附到地面基线 -3
        Vector2 pos = new Vector2(worldPos.x, -3f);

        // 生成（复用 UnitFactory 完整链路：UnitData 查找 + Prefab 实例化 + NPCBrain.Init）
        GameObject go = UnitFactory.Instance.SpawnUnit(GetFaction(type), GetOccupation(type), pos);
        if (go == null)
        {
            return new DebugSpawnResult
            {
                Success = false,
                Message = $"生成失败: {GetFactionName(type)} {GetDisplayName(type)}（检查 UnitData/Prefab 是否存在）",
            };
        }

        // 注册到 GridSystem（空间分区查目标/投射物到达检测依赖此注册）
        var controller = go.GetComponent<UnitController>();
        if (controller != null && GridSystem.Instance != null)
        {
            // doc1 改造：WorldToCoord 返回 GridCoord?（null=越界），越界跳过登记
            var coordOpt = GridSystem.Instance.WorldToCoord(pos);
            if (coordOpt.HasValue)
                GridSystem.Instance.TryEnter(controller, coordOpt.Value);
        }

        Debug.Log($"[AIDebugSpawn] 生成: {GetFactionName(type)} {GetDisplayName(type)} @ {pos}");
        return new DebugSpawnResult { Success = true, Message = $"已生成 {GetDisplayName(type)}", Spawned = go };
    }

    /// <summary>
    /// 在鼠标位置生成单位（内部把屏幕坐标转世界坐标）。
    /// UI 点击"生成"按钮后，再点击场景某处时调用。
    /// </summary>
    public DebugSpawnResult SpawnAtCursor(DebugSpawnType type)
    {
        if (Camera.main == null)
        {
            return new DebugSpawnResult { Success = false, Message = "无主相机" };
        }
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return Spawn(type, worldPos);
    }

    /// <summary>
    /// 生成后可选：绑定将军编队（若生成的是己方将军）。
    /// UI 可调用此方法让将军自动招募编队。
    /// </summary>
    public void BindGeneralFormation(GameObject generalGo)
    {
        if (generalGo == null) return;
        var generalUnit = generalGo.GetComponent<UnitController>();
        if (generalUnit == null) return;

        // 复用 CombatTestSpawner 的编组逻辑：挂 FormationController + 加载阵型表 + 招募满编
        var fc = generalGo.GetComponent<FormationController>();
        if (fc == null)
            fc = generalGo.AddComponent<FormationController>();

        // 3.0.1_6 §4.3：按将军阵营分流——敌方将军用 Undead + FormationTable_Enemy（独立阵型表）
        fc.faction = generalUnit.Data != null ? generalUnit.Data.faction : Faction.Human_Player;
        if (fc.formationTable == null)
            fc.formationTable = Resources.Load<FormationTable>(
                fc.faction == Faction.Undead ? "Formations/FormationTable_Enemy" : "Formations/FormationTable");

        fc.BindGeneral(generalUnit);
        fc.RecruitStandard();
        Debug.Log($"[AIDebugSpawn] {(fc.faction == Faction.Undead ? "敌方" : "己方")}将军已编组，招募满编。");
    }

    // ===== 王国任务（7.8 T-K/T-R）验证辅助 =====

    /// <summary>
    /// 给指定工人 GameObject 派发王国任务（QQQ.2 T18：WorkerTask 内化为工厂，经调度器派发）。
    /// 供 Play 验证用：构造 KingdomTask → TaskScheduler.DispatchExternal（NPC 靠 TaskStimulus 走向任务点）。
    /// </summary>
    public void AssignKingdomTask(GameObject workerGo, WorkerTaskType type, float sourceX, float destX, float workDuration)
    {
        if (workerGo == null) return;
        var brain = workerGo.GetComponent<NPCBrain>();
        if (brain == null || !TaskScheduler.HasInstance) return;
        var task = WorkerTask.CreateTask(type,
            new Vector2(sourceX, workerGo.transform.position.y),
            new Vector2(destX, workerGo.transform.position.y),
            workDuration, ResourceType.Gold);
        TaskScheduler.Instance.DispatchExternal(brain, task);
        Debug.Log($"[AIDebugSpawn] 任务派发: {workerGo.name} {type} source={sourceX} dest={destX} dur={workDuration}");
    }

    /// <summary>
    /// 生成一个工人并领取王国任务（一步到位，供 Play 验证）。
    /// 返回生成结果（含 Spawned 对象）。
    /// </summary>
    public DebugSpawnResult SpawnCivilianWithTask(Vector2 worldPos, WorkerTaskType type, float sourceX, float destX, float workDuration)
    {
        var result = Spawn(DebugSpawnType.PlayerCivilian, worldPos);
        if (result.Success)
            AssignKingdomTask(result.Spawned, type, sourceX, destX, workDuration);
        return result;
    }

    // ===== QQQ.2 T8 / DR-21 验证场景：闲逛遇敌撤退 =====

    /// <summary>
    /// 一键生成"闲逛遇敌撤退"验证场景（默认以王国锚点为中心，含城墙）。
    /// 详见 QQQ.2_需求4.5：验证 SafetyScore 三路合并 + WanderAnchorPool + RetreatToSafeAnchor。
    /// </summary>
    public void SpawnWanderRetreatScenario()
    {
        Vector2 center = Vector2.zero;
        if (WorldManager.Instance != null)
            center = WorldManager.Instance.GetKingdomAnchorWorld();
        SpawnWanderRetreatScenario(center, withWalls: true);
    }

    /// <summary>
    /// QQQ.2 T8 / DR-21 验证场景：闲逛遇敌撤退（一键生成）。
    /// 王国内部 6 个空闲工人（无任务，走 Wander 闲逛）+ 边界 2 个敌方士兵 + 可选城墙双段。
    /// 验证点（需求 4.5）：
    ///   ① 空闲 NPC 分散在锚点池各处闲逛（不聚城堡单点）
    ///   ② 城墙内 wallFactor → 工人在城墙内 SafetyScore 高
    ///   ③ 敌人压近 → SafetyScore 跌破阈值 → 往最近安全锚点撤退（RetreatToSafeAnchor），不卡在敌我之间
    /// 用法：F1 调试面板或脚本调用；withWalls=false 验证无城墙场景（靠距离+友军判定安全）。
    /// </summary>
    public void SpawnWanderRetreatScenario(Vector2 center, bool withWalls)
    {
        float cs = GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize.x : 2.26f;
        float wallOffset = 6f * cs;       // 城墙段距中心（内层防线）
        float borderOffset = 8.5f * cs;   // 敌兵压近距（感知半径内触发威胁）

        if (withWalls)
        {
            Spawn(DebugSpawnType.PlayerWall, center + new Vector2(-wallOffset, 0f));
            Spawn(DebugSpawnType.PlayerWall, center + new Vector2(wallOffset, 0f));
        }
        // 6 个空闲工人：城堡附近 ±2 格散布（开局即 Wander 闲逛，10-20s 后分散到各锚点）
        for (int i = 0; i < 6; i++)
        {
            float dx = (i % 3 - 1) * cs * 2f + Random.Range(-cs * 0.8f, cs * 0.8f);
            Spawn(DebugSpawnType.PlayerCivilian, center + new Vector2(dx, 0f));
        }
        // 边界 2 个敌方士兵（压近触发 SafetyScore 跌破阈值 → 撤退）
        Spawn(DebugSpawnType.EnemyWarrior, center + new Vector2(borderOffset, 0f));
        Spawn(DebugSpawnType.EnemyWarrior, center + new Vector2(borderOffset + cs, 0f));
        Debug.Log($"[AIDebugSpawn] 闲逛遇敌撤退场景生成完成（withWalls={withWalls}）：6 工人闲逛 + 2 敌兵边界压近，观察 RetreatToSafeAnchor。");
    }

    // ===== QQQ.2 T22 验证场景：生产链路端到端 =====

    /// <summary>
    /// 一键生成"生产链路端到端"验证场景（默认以王国锚点为中心）。
    /// 详见 QQQ.2 T22（R2 缺口）：农场有工人+水→产粮→搬运入仓。
    /// </summary>
    public void SpawnProductionChainScenario()
    {
        Vector2 center = Vector2.zero;
        if (WorldManager.Instance != null)
            center = WorldManager.Instance.GetKingdomAnchorWorld();
        SpawnProductionChainScenario(center);
    }

    /// <summary>
    /// QQQ.2 T22 验证场景：生产链路端到端联调。
    /// 一键生成：水井（产水入网，不需工人）+ 农场（耗水产粮，需工人）+ 仓库（接收搬运）+ 2 工人。
    /// 验证链（依赖 T9/T15/T17/T19，本会话已完成）：
    ///   ① 水井 → WaterNetwork 产水（4 水/秒，容量 100）
    ///   ② 农场有工人（HasWorkerAssigned）+ 水（ConsumeWater 2/次）→ 产粮
    ///   ③ 农场存储达标 → TaskScheduler 派搬运任务 → 工人搬粮入仓（StorageComponent.HarvestCarry）
    /// 仓库面板（T12）落地后可同步观察实时显示。
    /// </summary>
    public void SpawnProductionChainScenario(Vector2 center)
    {
        float cs = GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize.x : 2.26f;
        if (BuildingFactory.Instance == null) return;

        // ① 水井（产水入网，不需要工人）
        PlaceBuilding("Buildings/Well", center + new Vector2(-4f * cs, 0f));
        // ② 农场（耗水产粮，需工人派生产任务）
        PlaceBuilding("Buildings/farm", center + new Vector2(1f * cs, 0f));
        // ③ 仓库（接收搬运入仓）
        PlaceBuilding("Buildings/Warehouse", center + new Vector2(3f * cs, 0f));
        // ④ 2 个工人（调度器派生产/搬运任务）
        Spawn(DebugSpawnType.PlayerCivilian, center + new Vector2(-1f * cs, 0f));
        Spawn(DebugSpawnType.PlayerCivilian, center + new Vector2(2f * cs, 0f));
        Debug.Log("[AIDebugSpawn] 生产链路场景生成完成：水井+农场+仓库+2工人，观察产粮→搬运入仓。");
    }

    /// <summary>
    /// QQQ.4 T13 验证场景：资源生命周期端到端（双任务并行 + 工人背包 + 搬运入仓）。
    /// 一键生成：水井（产水入网）+ 农场（耗水产粮）+ 仓库（卸货目标）+ 木头堆（采集点）+ 3 工人。
    /// 验证链（依赖 QQQ.4 T1-T12）：
    ///   ① 农场双任务（T1/T2）：初始水网缺水(&lt;20) → 农场同时派 Production（耕作）+ WaterHaul（挑水）→ 2 工人分工
    ///   ② 采集入背包（T8/T10/T12）：点击木头堆采集 → 工人采集入背包 → 搬运到仓库 → 左上角资源增加
    ///   ③ 流浪汉营地徘徊（T4/T5）：流浪汉仅在营地 ±3 格活动，不朝主城
    /// </summary>
    public void SpawnLifecycleScenario()
    {
        float cs = GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize.x : 2.26f;
        if (BuildingFactory.Instance == null) return;
        Vector2 center = WorldManager.Instance != null ? WorldManager.Instance.GetKingdomAnchorWorld() : Vector2.zero;

        // ① 水井（产水入网，不需要工人）
        PlaceBuilding("Buildings/Well", center + new Vector2(-4f * cs, 0f));
        // ② 农场（耗水产粮，需工人派生产任务）
        PlaceBuilding("Buildings/farm", center + new Vector2(1f * cs, 0f));
        // ③ 仓库（接收背包卸货入仓）
        PlaceBuilding("Buildings/Warehouse", center + new Vector2(3f * cs, 0f));
        // ④ 木头堆（采集点，玩家点击 StartGather 触发采集入背包链路）
        PlaceBuilding("Buildings/wood_pile", center + new Vector2(6f * cs, 0f));
        // ⑤ 3 个工人（1 耕作 + 1 挑水 + 1 采集搬运）
        Spawn(DebugSpawnType.PlayerCivilian, center + new Vector2(-1f * cs, 0f));
        Spawn(DebugSpawnType.PlayerCivilian, center + new Vector2(2f * cs, 0f));
        Spawn(DebugSpawnType.PlayerCivilian, center + new Vector2(5f * cs, 0f));
        Debug.Log("[AIDebugSpawn] QQQ.4 生命周期场景生成：水井+农场+仓库+木头堆+3工人，观察双任务/采集入背包→搬运入仓。");
    }

    /// <summary>按资产路径放置建筑（走 BuildingFactory 完整链路：占用/注册/挂件/事件）。</summary>
    bool PlaceBuilding(string assetPath, Vector2 worldPos)
    {
        if (BuildingFactory.Instance == null || GridSystem.Instance == null || GridSystem.Instance.Config == null)
            return false;
        var def = Resources.Load<BuildingDef>(assetPath);
        if (def == null)
        {
            Debug.LogWarning($"[AIDebugSpawn] 未找到建筑资产 {assetPath}");
            return false;
        }
        var coordOpt = GridSystem.Instance.WorldToCoord(worldPos);
        if (!coordOpt.HasValue) return false; // doc1 改造：越界返回 null，不可放置
        GridCoord coord = coordOpt.Value;
        int w = def.footprint.x > 0 ? def.footprint.x : 1;
        return BuildingFactory.Instance.CreateBuildingInstance(
            def, BuildingType.None, coord, w, worldPos,
            isPlayerBuilt: true, ResourceGrade.Normal, def.isConsumable, BuildingState.Active);
    }
}
