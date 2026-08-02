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

    // ===== 敌方（Undead）=====
    EnemyWarrior,       // 敌方近战士兵
    EnemyArcher,        // 敌方弓箭手
    EnemyGeneral,       // 敌方将军（3.0.1_6 §4.3：挂 FormationController + FormationTable_Enemy）
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
                return Faction.Human_Player;
            case DebugSpawnType.EnemyWarrior:
            case DebugSpawnType.EnemyArcher:
            case DebugSpawnType.EnemyGeneral:
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
            case DebugSpawnType.EnemyWarrior: return Occupation.Warrior;
            case DebugSpawnType.EnemyArcher: return Occupation.Archer;
            case DebugSpawnType.EnemyGeneral: return Occupation.General;
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
            case DebugSpawnType.EnemyWarrior: return "敌方士兵";
            case DebugSpawnType.EnemyArcher: return "敌方弓手";
            case DebugSpawnType.EnemyGeneral: return "敌方将军";
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
            GridCoord coord = GridSystem.Instance.WorldToCoord(pos);
            GridSystem.Instance.TryEnter(controller, coord);
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
}
