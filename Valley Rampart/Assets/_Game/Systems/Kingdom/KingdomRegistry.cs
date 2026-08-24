using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 王国统一注册表（2_16 步骤1，D303/D314/D385；对齐 TaskScheduler/TrainingSystem Singleton 惯例）。
/// 职责：Register/Get/GetAll/Count；id 单调递增不复用（D385）；玩家 id=0 开局注册（D303）。
/// Count 含玩家（D314：全局上限检查口径 = Count &lt; maxKingdomsGlobal，上限值落步骤4 FoundingConfig）。
/// 事件：KingdomFoundedEvent（第一代/玩家）/KingdomEmergedEvent（动态立国）——本片只发事件，
///       消费方归 2_10（染色）/2_13（播报/名单）。无订阅者时不发布（对齐 RegionHeatChangedEvent 守卫）。
/// 存档：步骤8 实现 ISaveable（KingdomRegistrySaveData）。
/// </summary>
public class KingdomRegistry : Singleton<KingdomRegistry>
{
    private readonly List<KingdomState> _kingdoms = new List<KingdomState>();

    /// <summary>下一个待分配 id（0 保留给玩家，动态/第一代从 1 起，单调递增不复用 D385）。</summary>
    private int _nextId = 1;

    /// <summary>是否已注册玩家（幂等守卫：新建只注册一次，读档走 LoadState 恢复不重复注册）。</summary>
    private bool _playerRegistered;

    /// <summary>王国总数（含玩家 id=0）。</summary>
    public int Count => _kingdoms.Count;

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
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
    }
}