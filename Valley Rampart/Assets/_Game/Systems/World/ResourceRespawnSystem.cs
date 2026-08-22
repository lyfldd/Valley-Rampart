using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2_12 步骤6 全资源刷新系统（HH.10 裁决三：丙⁺ 双路径 / 决策五：D108 双列表 / D61 树短石矿长）。
///
/// 双路径设计（决策三）：
///   • 数据路径（Tree）：树是 features 数据格，不建实体。工人经 TreeGatherSource 砍树 →
///     木入包 → HandleTreeGathered 把格翻 Plain + 刷新渲染 → RecordDataDepleted 记录重生倒计时 →
///     Tick 到点 → 格翻回 Tree + 刷新渲染（重生，可再次砍）。
///   • 实体路径（OreVein/WoodPile/StonePile）：一次性实体（Building）采集销毁 → HandleEntityDepleted
///     把格翻 Plain + 记录重生 → 玩家采集路径已由 Building.OnGatherCompleted 触发守卫失去（不重复）→
///     Tick 到点 → 通过 BuildingFactory 重建实体 + 格翻回原 feature。
///
/// 存档（决策五）：DataRespawnEntry / EntityRespawnEntry 两列表分开（生命周期不同，绝不混一个 list）。
/// 冒烟快验：把 RespawnConfig.daySeconds 调小即可数秒内等重生，别等真实周期。
/// </summary>
public class ResourceRespawnSystem : Singleton<ResourceRespawnSystem>, ISaveable
{
    public string SaveId => "ResourceRespawnSystem";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    private void Awake()
    {
        base.Awake();   // Singleton：自动创建实例 + DontDestroyOnLoad
        if (SaveManager.Instance != null) SaveManager.Instance.RegisterSaveable(this);
    }
    /// <summary>数据路径重生记录（Tree 格翻转）。cell 用格坐标，重生时定位 features 数组。</summary>
    private struct DataRespawnEntry
    {
        public GridCoord cell;
        public float dueGameDay;   // 按 daySeconds 折算的游戏天到期点
    }

    /// <summary>实体路径重生记录（OreVein/WoodPile/StonePile 重建）。</summary>
    private struct EntityRespawnEntry
    {
        public GridCoord cell;
        public FeatureType feature;
        public float dueGameDay;
    }

    private readonly List<DataRespawnEntry> _data = new List<DataRespawnEntry>();
    private readonly List<EntityRespawnEntry> _entity = new List<EntityRespawnEntry>();
    private float _elapsed;
    private float _currentDay;
    private bool _init;

    public static bool HasInstance => Instance != null;

    private RespawnConfig Cfg => RespawnConfig.Instance;

    private void EnsureInit()
    {
        if (_init) return;
        _init = true;
        _currentDay = 0f;
        _elapsed = 0f;
    }

    /// <summary>新地图/读档后重置所有重生记录（由 WorldManager 地图生成时调）。</summary>
    public void ResetRespawns()
    {
        _data.Clear();
        _entity.Clear();
        _init = false;
        EnsureInit();
    }

    /// <summary>每帧推进重生倒计时（游戏天 = 累计秒 / daySeconds）。</summary>
    private void Update()
    {
        if (Cfg == null || !Cfg.enabled) return;
        EnsureInit();
        _elapsed += Time.deltaTime;
        _currentDay = _elapsed / Mathf.Max(0.0001f, Cfg.daySeconds);

        // 数据路径：到点的 Tree 格翻回 Tree
        if (_data.Count > 0)
        {
            for (int i = _data.Count - 1; i >= 0; i--)
            {
                if (_currentDay < _data[i].dueGameDay) continue;
                SetFeature(_data[i].cell, FeatureType.Tree);
                _data.RemoveAt(i);
            }
        }

        // 实体路径：到点的重建一次性资源实体
        if (_entity.Count > 0)
        {
            for (int i = _entity.Count - 1; i >= 0; i--)
            {
                var e = _entity[i];
                if (_currentDay < e.dueGameDay) continue;
                RebuildEntity(e.cell, e.feature);
                _entity.RemoveAt(i);
            }
        }
    }

    // ===== Tree 数据格采集入口（2_13 交互 UI 调用；本步先供程序化/测试触发）=====

    /// <summary>
    /// 确认采集一棵树（数据格）：校验该格是 Tree feature → 创建一个 TreeGatherSource 注册进调度器，
    /// 下一 tick 即派最近工人来砍。返回是否成功触发。
    /// 玩家点击地图树格（2_13 交互入口）经此调用；本步验收用程序化触发验证全链。
    /// </summary>
    public bool ConfirmTreeGather(GridCoord cell)
    {
        if (!Cfg || !Cfg.enabled) return false;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null || map.features == null) return false;
        int i = cell.y * map.width + cell.x;
        if (i < 0 || i >= map.features.Length || map.features[i] != FeatureType.Tree) return false;

        Vector2 pos = GridSystem.Instance != null ? GridSystem.Instance.CoordToWorld(cell) : Vector2.zero;
        var src = new TreeGatherSource(cell, pos, Cfg.treeGatherSeconds, Cfg.treeGatherAmount);
        if (TaskScheduler.HasInstance) TaskScheduler.Instance.Register(src);
        else return false;
        return true;
    }

    /// <summary>树被砍完成（TreeGatherSource.OnGatherCompletion 调）：格翻 Plain + 刷新 + 守卫失去 + 记重生。</summary>
    public void HandleTreeGathered(GridCoord cell)
    {
        if (Cfg == null || !Cfg.enabled) return;
        if (SetFeature(cell, FeatureType.Plain))
        {
            // 守卫锚点语义（HH.3 §六 / HH.6）：高价值资源点（树）被采走/覆盖 → 守卫区域失覆盖 → LostEvent
            GuardDeploymentSystem.HandleResourceConsumed(cell);
            // 数据路径重生记录（D61：树短）
            RecordDepleted(cell, Cfg.treeRespawnDays);
        }
    }

    /// <summary>一次性实体被采集销毁（Building.OnGatherCompleted 调）：格翻 Plain + 记重生（守卫失去已在原路径处理）。</summary>
    public void HandleEntityDepleted(GridCoord cell, FeatureType feature)
    {
        if (Cfg == null || !Cfg.enabled) return;
        if (SetFeature(cell, FeatureType.Plain))
            RecordDepleted(cell, feature, RespawnDaysOf(feature));
    }

    // ===== 内部：计时/重生 =====

    private void RecordDepleted(GridCoord cell, float respawnDays)
    {
        float due = _currentDay + respawnDays;
        // 同一格已有数据重生则更新到期时间（树从采完起算）
        for (int i = 0; i < _data.Count; i++)
            if (SameCell(_data[i].cell, cell)) { _data[i] = new DataRespawnEntry { cell = cell, dueGameDay = due }; return; }
        _data.Add(new DataRespawnEntry { cell = cell, dueGameDay = due });
    }

    private void RecordDepleted(GridCoord cell, FeatureType feature, float respawnDays)
    {
        float due = _currentDay + respawnDays;
        for (int i = 0; i < _entity.Count; i++)
            if (SameCell(_entity[i].cell, cell)) { _entity[i] = new EntityRespawnEntry { cell = cell, feature = feature, dueGameDay = due }; return; }
        _entity.Add(new EntityRespawnEntry { cell = cell, feature = feature, dueGameDay = due });
    }

    private float RespawnDaysOf(FeatureType f) => f switch
    {
        FeatureType.WoodPile => Cfg.woodRespawnDays,
        FeatureType.StonePile => Cfg.stoneRespawnDays,
        _ => Cfg.oreRespawnDays
    };

    /// <summary>把一格 feature 改为目标值 + 刷新地形/可走 + 渲染。返回是否实际改变。</summary>
    private bool SetFeature(GridCoord cell, FeatureType target)
    {
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null || map.features == null) return false;
        int i = cell.y * map.width + cell.x;
        if (i < 0 || i >= map.features.Length || map.features[i] == target) return false;
        map.features[i] = target;
        if (GridSystem.Instance != null) GridSystem.Instance.RefreshCellFromFeature(cell, target);
        if (MapRenderService.Instance != null) MapRenderService.Instance.UpdateCell(cell);
        return true;
    }

    private void RebuildEntity(GridCoord cell, FeatureType feature)
    {
        // 先把格翻回原 feature（SetFeature 内部刷新渲染），再重建实体
        SetFeature(cell, feature);
        if (BuildingFactory.Instance != null)
            BuildingFactory.Instance.ReSpawnNaturalBuilding(cell, feature);
    }

    private static bool SameCell(GridCoord a, GridCoord b) => a.x == b.x && a.y == b.y;

    // ===== D108 存档：数据路径(Tree 格) / 实体路径(一次性实体) 双列表分开 =====

    public SavePayload SaveState()
    {
        EnsureInit();
        var data = new ResourceRespawnSaveData
        {
            currentDay = _currentDay,
            dataEntries = _data.ConvertAll(e => new DataRespawnSaveEntry { x = e.cell.x, y = e.cell.y, dueGameDay = e.dueGameDay }),
            entityEntries = _entity.ConvertAll(e => new EntityRespawnSaveEntry { x = e.cell.x, y = e.cell.y, feature = (int)e.feature, dueGameDay = e.dueGameDay })
        };
        return new SavePayload
        {
            typeName = typeof(ResourceRespawnSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(ResourceRespawnSaveData).AssemblyQualifiedName) return;
        try
        {
            var data = JsonUtility.FromJson<ResourceRespawnSaveData>(payload.json);
            _data.Clear();
            _entity.Clear();
            foreach (var e in data.dataEntries)
                _data.Add(new DataRespawnEntry { cell = new GridCoord(e.x, e.y), dueGameDay = e.dueGameDay });
            foreach (var e in data.entityEntries)
                _entity.Add(new EntityRespawnEntry { cell = new GridCoord(e.x, e.y), feature = (FeatureType)e.feature, dueGameDay = e.dueGameDay });
            _currentDay = data.currentDay;   // 保持游戏天连续，未到期条目继续倒计时
            _init = true;
            Debug.Log($"[ResourceRespawnSystem] 存档恢复：数据重生 {_data.Count} 条 + 实体重生 {_entity.Count} 条");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ResourceRespawnSystem] 存档恢复失败，放弃重生记录：{ex.Message}");
        }
    }
}

/// <summary>
/// D108 全资源刷新存档（HH.10 裁决五：数据路径与实体路径分列表，绝不混一个 list）。
/// 字段独立设计：数据格(Tree) 按格坐标重生；实体(OreVein/WoodPile/StonePile) 按格+feature 重建。
/// </summary>
[System.Serializable]
public class ResourceRespawnSaveData
{
    public float currentDay;                    // 当前游戏天（保持倒计时连续）
    public System.Collections.Generic.List<DataRespawnSaveEntry> dataEntries = new();
    public System.Collections.Generic.List<EntityRespawnSaveEntry> entityEntries = new();
}

/// <summary>数据路径条目（Tree 格翻转重生）。</summary>
[System.Serializable]
public struct DataRespawnSaveEntry
{
    public int x, y;
    public float dueGameDay;
}

/// <summary>实体路径条目（一次性实体重建）。</summary>
[System.Serializable]
public struct EntityRespawnSaveEntry
{
    public int x, y;
    public int feature;      // (int)FeatureType
    public float dueGameDay;
}