using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 加载阶段。决定当前加载进度，其他系统可查询后决定是否取资源。
/// </summary>
public enum LoadPhase
{
    Booting,         // 引擎初始化
    StaticConfigs,   // 静态配置加载中
    WorldInit,       // 新建游戏初始化中
    SaveRestore,     // 读档恢复中
    Playing          // 运行时，可按需动态加载
}

/// <summary>
/// 加载总管。统一加载入口 + 资源获取门面。
/// 职责：阶段化调度加载 + 委托转发资源请求。
/// 不自己缓存资源——各子加载器（UnitDataManager/UnitFactory）保持自己的缓存。
/// </summary>
[DefaultExecutionOrder(-110)]  // 比 GameBootstrap(-100) 更早
public class LoadManager : Singleton<LoadManager>
{
    // ===== 加载阶段状态 =====

    public LoadPhase CurrentPhase { get; private set; } = LoadPhase.Booting;

    // ===== 子加载器引用（场景里挂的 Singleton，启动后获取）=====

    private UnitDataManager _configLoader;
    private UnitFactory _prefabLoader;
    private SaveManager _saveLoader;
    private WorldSystem _worldSystem;

    protected override void Awake()
    {
        base.Awake();

        // 校验关键 Singleton（不靠隐式创建，要求场景里显式放置）
        _configLoader = FindObjectOfType<UnitDataManager>();
        _prefabLoader = FindObjectOfType<UnitFactory>();
        _saveLoader = SaveManager.Instance;
        _worldSystem = FindObjectOfType<WorldSystem>();

        if (_configLoader == null) Debug.LogError("[LoadManager] 场景中未找到 UnitDataManager！");
        if (_prefabLoader == null) Debug.LogError("[LoadManager] 场景中未找到 UnitFactory！");
        if (_worldSystem == null) Debug.LogError("[LoadManager] 场景中未找到 WorldSystem！");
    }

    // ===== 阶段1：静态配置加载（新建/读档都要）=====

    /// <summary>
    /// 加载所有静态配置（SO + Prefab）。新建和读档都调用一次。
    /// 由 GameBootstrap 在 Loading 状态时调。
    /// </summary>
    public void LoadStaticConfigs()
    {
        CurrentPhase = LoadPhase.StaticConfigs;
        Debug.Log("[LoadManager] 阶段1：加载静态配置...");

        if (_configLoader != null) _configLoader.LoadAll();           // UnitData 等
        if (_prefabLoader != null) _prefabLoader.PreloadAll();        // UnitPrefabs
        // WorldConfig（WorldSystem.Awake 已 Resources.Load，这里只确认阶段）

        Debug.Log("[LoadManager] 静态配置加载完成");
        EventBus.Publish(new ConfigsLoadedEvent(true));
    }

    // ===== 阶段2a：新建游戏初始化 =====

    /// <summary>
    /// 新建游戏：世界初始化 + 君主生成。由 GameBootstrap 在 Ready 状态、新建模式时调。
    /// </summary>
    public void InitializeNewGame(NewGameConfig config)
    {
        CurrentPhase = LoadPhase.WorldInit;
        Debug.Log("[LoadManager] 阶段2a：新建游戏初始化...");

        if (_worldSystem == null)
        {
            Debug.LogError("[LoadManager] WorldSystem 不可用，无法初始化世界！请确保场景中已放置 WorldSystem。");
            return;
        }

        _worldSystem.InitializeWorld(config);
        RulerController.Instance.SpawnMonarch();   // 君主生成（阶段2a 内）

        Debug.Log("[LoadManager] 新建游戏初始化完成");
        EnterPlaying();
    }

    // ===== 阶段2b：读档恢复 =====

    /// <summary>
    /// 读档：存档恢复。由 GameBootstrap 在 Ready 状态、读档模式时调。
    /// 读档前的场景清理与读档后的君主绑定由 GameBootstrap 负责（场景相关）。
    /// 返回 false 表示读档失败（存档不存在 / 已标记结束 / 反序列化异常）。
    /// </summary>
    public bool LoadSave(string slotId)
    {
        CurrentPhase = LoadPhase.SaveRestore;
        Debug.Log($"[LoadManager] 阶段2b：读档恢复 slot={slotId}...");

        bool success = _saveLoader.Load(slotId);
        if (!success)
        {
            Debug.LogError($"[LoadManager] 读档失败: {slotId}，不进入 Playing。");
            return false;
        }

        Debug.Log("[LoadManager] 读档恢复完成");
        EnterPlaying();
        return true;
    }

    // ===== 阶段3：进入运行时 =====

    private void EnterPlaying()
    {
        CurrentPhase = LoadPhase.Playing;
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetState(GameState.Playing);
        }
        Debug.Log("[LoadManager] 进入运行时，可按需动态加载");
    }

    // ===== 运行时动态加载（未来扩展）=====

    /// <summary>运行时按需加载资源（建筑/敌人/VFX Prefab 等）。</summary>
    public T LoadAsset<T>(string path) where T : Object
    {
        T asset = Resources.Load<T>(path);
        if (asset == null)
        {
            Debug.LogError($"[LoadManager] 找不到资源: {path}");
        }
        return asset;
    }

    // ===== 门面接口：资源获取（委托转发）=====

    /// <summary>获取单位配置（委托 UnitDataManager）。</summary>
    public UnitData GetUnitData(Faction faction, Occupation occupation)
    {
        return _configLoader != null ? _configLoader.GetData(faction, occupation) : null;
    }

    /// <summary>获取所有单位配置（委托 UnitDataManager）。</summary>
    public IEnumerable<UnitData> GetAllUnitData()
    {
        return _configLoader != null ? _configLoader.GetAllData() : null;
    }

    /// <summary>获取单位 Prefab（委托 UnitFactory）。</summary>
    public GameObject GetPrefab(string key)
    {
        return _prefabLoader != null ? _prefabLoader.GetPrefab(key) : null;
    }

    /// <summary>创建单位实例（委托 UnitFactory）。</summary>
    public GameObject SpawnUnit(UnitData data, Vector2 position)
    {
        return _prefabLoader != null ? _prefabLoader.SpawnUnit(data, position) : null;
    }

    /// <summary>世界配置（委托 WorldSystem）。</summary>
    public WorldConfig WorldConfig => _worldSystem != null ? _worldSystem.Config : null;

    /// <summary>当前难度系数（委托 DifficultyManager）。</summary>
    public float CurrentDifficultyFactor
        => DifficultyManager.Instance != null ? DifficultyManager.Instance.CurrentFactor : 1f;

    /// <summary>存档是否存在（委托 SaveManager）。</summary>
    public bool HasSave(string slotId) => _saveLoader != null && _saveLoader.HasSave(slotId);
}
