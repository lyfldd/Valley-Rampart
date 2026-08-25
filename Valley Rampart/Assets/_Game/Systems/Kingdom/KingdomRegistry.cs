using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 王国统一注册表（2_16 步骤1，D303/D314/D385；对齐 TaskScheduler/TrainingSystem Singleton 惯例）。
/// 职责：Register/Get/GetAll/Count；id 单调递增不复用（D385）；玩家 id=0 开局注册（D303）。
/// Count 含玩家（D314：全局上限检查口径 = Count &lt; maxKingdomsGlobal，上限值落步骤4 FoundingConfig）。
/// 事件：KingdomFoundedEvent（第一代/玩家）/KingdomEmergedEvent（动态立国）——本片只发事件，
///       消费方归 2_10（染色）/2_13（播报/名单）。无订阅者时不发布（对齐 RegionHeatChangedEvent 守卫）。
/// 存档：ISaveable（KingdomRegistrySaveData）。2_16 步骤8——
///       nextId 显式入档（D385：跨存档 id 不复用，不从存量 max 推导，防 P1/2_19 王国移除后复用已删 id）；
///       旧档无 KingdomRegistry 模块 → 不 LoadState → 靠 WorldSystem.EnsurePlayerRegistered 兜底只注册玩家（Count=1）。
/// </summary>
public class KingdomRegistry : Singleton<KingdomRegistry>, ISaveable
{
    private readonly List<KingdomState> _kingdoms = new List<KingdomState>();

    /// <summary>下一个待分配 id（0 保留给玩家，动态/第一代从 1 起，单调递增不复用 D385）。</summary>
    private int _nextId = 1;

    /// <summary>是否已注册玩家（幂等守卫：新建只注册一次，读档走 LoadState 恢复不重复注册）。</summary>
    private bool _playerRegistered;

    /// <summary>全局立国冷却时间戳（2_16 步骤11 D312）：只由动态立国更新。初始值守卫 int.MinValue=未立国过 → 不阻首个动态立国（冷却期不插旗、营地继续生长，到期即立）。入档。</summary>
    public int lastFoundingDay = int.MinValue;

    /// <summary>王国总数（含玩家 id=0）。</summary>
    public int Count => _kingdoms.Count;

    /// <summary>冷却期判定（D312）：lastFoundingDay 未置(初值) 或 距上次立国 ≥ cooldownDays 才允许动态立国。</summary>
    public bool CanFoundNow(int currentDay, int cooldownDays) =>
        lastFoundingDay == int.MinValue || (currentDay - lastFoundingDay) >= Mathf.Max(0, cooldownDays);

    /// <summary>标记本次动态立国（更新冷却时间戳，D312：只由动态立国更新）。</summary>
    public void MarkFounding(int day) => lastFoundingDay = day;

    // ===== ISaveable（2_16 步骤8）=====
    public string SaveId => "KingdomRegistry";
    /// <summary>Global：先于场景建筑/单位恢复，保证实体归属重建时 Registry 句柄已就绪。</summary>
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        SaveManager.Instance?.RegisterSaveable(this);   // 对齐 Building.Awake 注册惯例（常驻全局单例）
    }

    /// <summary>
    /// 确保玩家王国 id=0 已注册（D303）。须在 AI 第一代立国（ApplyConfig→Foundry）之前调用，
    /// 保证玩家占 id=0；name 接 KingdomManager.KingdomName（2_13 已由 WorldSystem 写入）。
    /// </summary>
    public void EnsurePlayerRegistered()
    {
        if (_playerRegistered) return;
        _playerRegistered = true;

        var km = KingdomManager.Instance;
        var state = new KingdomState
        {
            id = 0,
            name = km != null ? km.KingdomName : "河谷王国",
            bannerColor = new Color(0.20f, 0.38f, 0.75f),   // 玩家默认王旗色（占位，染色归 2_10）
            foundedDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1,
            templateSourceId = -1
        };
        _kingdoms.Add(state);
        PublishFounded(state);
        Debug.Log($"[KingdomRegistry] 玩家王国开局注册: id=0, name={state.name}, foundedDay={state.foundedDay}");
    }

    /// <summary>注册一个新王国（第一代/动态立国共用），分配单调递增 id 并发布事件。</summary>
    public KingdomState RegisterNewKingdom(string name, Color bannerColor, int foundedDay, int templateSourceId)
    {
        var state = new KingdomState
        {
            id = _nextId++,
            name = name,
            bannerColor = bannerColor,
            foundedDay = foundedDay,
            templateSourceId = templateSourceId
        };
        _kingdoms.Add(state);
        PublishFounded(state);
        return state;
    }

    /// <summary>按 id 取王国（含玩家 id=0）；不存在返回 null。</summary>
    public KingdomState Get(int id)
    {
        for (int i = 0; i < _kingdoms.Count; i++)
            if (_kingdoms[i].id == id) return _kingdoms[i];
        return null;
    }

    /// <summary>全部王国列表（只读）。</summary>
    public IReadOnlyList<KingdomState> GetAll() => _kingdoms;

    /// <summary>发布立国事件（无订阅者不发布，避免开局无意义警告）。</summary>
    private static void PublishFounded(KingdomState state)
    {
        if (EventBus.HasSubscribers<KingdomFoundedEvent>())
            EventBus.Publish(new KingdomFoundedEvent(state));
    }

    /// <summary>清空（返回主菜单重置，对齐 KingdomManager.ResetState）。</summary>
    public void ResetState()
    {
        _kingdoms.Clear();
        _playerRegistered = false;
        _nextId = 1;
        lastFoundingDay = int.MinValue;   // 2_16 步骤11 冷却时间戳随重置清零
    }

    // ===== ISaveable：存档 =====

    public SavePayload SaveState()
    {
        var data = new KingdomRegistrySaveData
        {
            nextId = _nextId,   // D385：显式入档
            lastFoundingDay = lastFoundingDay,   // 2_16 步骤11 D312：冷却时间戳入档（与 Camp 存续计数同段）
            kingdoms = new List<KingdomEntryData>(_kingdoms.Count)
        };
        for (int i = 0; i < _kingdoms.Count; i++)
        {
            var k = _kingdoms[i];
            data.kingdoms.Add(new KingdomEntryData
            {
                id = k.id,
                name = k.name,
                bannerColor = k.bannerColor,
                foundedDay = k.foundedDay,
                personality = k.personality != null ? k.personality : new float[5],
                templateSourceId = k.templateSourceId,
                resources = k.resources,
                workerCount = k.workerCount,
                warriorCount = k.warriorCount
            });
        }
        return new SavePayload
        {
            typeName = typeof(KingdomRegistrySaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(KingdomRegistrySaveData).AssemblyQualifiedName) return;
        _kingdoms.Clear();
        _playerRegistered = false;
        var data = JsonUtility.FromJson<KingdomRegistrySaveData>(payload.json);

        // D385：nextId 显式恢复（不从存量 max 推导——王国移除后复用已删 id 违反 id 不复用铁律）
        _nextId = data.nextId > 0 ? data.nextId : 1;

        // 2_16 步骤11 D312：冷却时间戳恢复（旧档无此字段 → int 默认 0 → 视为已立国过、距 0 天 ≥冷却；但首个动态立国判定走 CanFoundNow 时仅当 lastFoundingDay==int.MinValue 才放行。
        // 为兼容旧档让首个动态立国不被旧档冷却误阻，0 视为初值并重置为 int.MinValue——否则新开档首日即立呈现异常。设计 §1.1"立国冷却时间戳"从新档起记。）
        lastFoundingDay = data.lastFoundingDay;
        if (lastFoundingDay <= 0) lastFoundingDay = int.MinValue;

        if (data.kingdoms != null)
        {
            for (int i = 0; i < data.kingdoms.Count; i++)
            {
                var e = data.kingdoms[i];
                var state = new KingdomState
                {
                    id = e.id,
                    name = e.name,
                    bannerColor = e.bannerColor,
                    foundedDay = e.foundedDay,
                    personality = (e.personality != null && e.personality.Length == 5)
                        ? e.personality : new float[5],
                    templateSourceId = e.templateSourceId,
                    resources = e.resources
                    // 2_17 步骤4 台账转派生：workerCount/warriorCount 为实体派生只读属性，不再从存档恢复
                    // （读档单位 SpawnFromSave 回笼，王国派生统计自动重建）
                };
                if (state.IsPlayer) _playerRegistered = true;
                _kingdoms.Add(state);
            }
        }

        Debug.Log($"[KingdomRegistry] 读档恢复 {_kingdoms.Count} 个王国（nextId={_nextId}, 含玩家={_playerRegistered}）。");
    }
}

/// <summary>王国注册表存档（2_16 步骤8）。旧档无此字段 → kingdoms=null → LoadState 不触发（WorldSystem 兜底玩家注册）。</summary>
[System.Serializable]
public struct KingdomRegistrySaveData
{
    /// <summary>下一个待分配 id（D385：跨存档不复用，必须显式入档，严禁从存量 max 推导）。</summary>
    public int nextId;
    /// <summary>全局立国冷却时间戳（2_16 步骤11 D312；≤0 视为未立国过 → 读档重置 int.MinValue 不阻首个动态立国）。</summary>
    public int lastFoundingDay;
    /// <summary>王国条目列表（含玩家 id=0）。</summary>
    public List<KingdomEntryData> kingdoms;
}

/// <summary>单王国存档条目（KingomState 全字段平铺，读档重建）。</summary>
[System.Serializable]
public struct KingdomEntryData
{
    public int id;
    public string name;
    public Color bannerColor;
    public int foundedDay;
    public float[] personality;          // 五轴（0好战/1经济/2防守/3扩张/4外交），读档缺省兜底中性
    public int templateSourceId;          // -1=无来源（玩家/占位）
    public ResourcePack resources;        // 起始过渡账本
    public int workerCount;
    public int warriorCount;
}