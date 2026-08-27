using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  王国脑注册表（2_17 步骤8，D337/D338）
//  持全部 AI 王国脑实例（key=kingdomId>0）。玩家 id=0 永不含（D338 双短路之一）。
//  Unregister 供 2_19 灭亡管线吊钩（本步留钩子；P0 无灭国路径→空转安全）。
//  与 KingdomRegistry（单例）同惯例：Singleton MonoBehaviour。
// ============================================================================

public class KingdomBrainRegistry : Singleton<KingdomBrainRegistry>
{
    private readonly Dictionary<int, KingdomBrain> _brains = new Dictionary<int, KingdomBrain>();

    /// <summary>已建王国脑数（不含玩家 id=0）。</summary>
    public int Count => _brains.Count;

    /// <summary>注册一个王国脑（玩家 id≤0 拒绝注册——Registry 永不含玩家脑，D338）。</summary>
    public void Register(KingdomBrain brain)
    {
        if (brain == null || brain.kingdomId <= 0) return;   // 玩家无脑（D338）
        _brains[brain.kingdomId] = brain;
    }

    /// <summary>
    /// 销毁/退订并移除一个王国脑（D337；2_19 灭亡管线调用）。
    /// EventBus 订阅成对退订（D340），无收到已死王国的打断回调。
    /// </summary>
    public void Unregister(int kingdomId)
    {
        if (kingdomId <= 0) return;
        if (_brains.TryGetValue(kingdomId, out var brain))
        {
            brain.Unsubscribe();
            _brains.Remove(kingdomId);
            Debug.Log($"[KingdomBrainRegistry] 王国(k{kingdomId}) 脑销毁+事件退订（D337/D340）。");
        }
    }

    /// <summary>按 id 取王国脑（无/玩家返回 null）。</summary>
    public KingdomBrain Get(int kingdomId)
    {
        return _brains.TryGetValue(kingdomId, out var b) ? b : null;
    }

    /// <summary>全部王国脑（只读遍历，值视图）。</summary>
    public IEnumerable<KingdomBrain> GetAll() => _brains.Values;

    /// <summary>是否已建某王国脑。</summary>
    public bool HasBrain(int kingdomId) => _brains.ContainsKey(kingdomId);

    /// <summary>重置（返回主菜单清空，对齐 KingdomRegistry.ResetState）。
    /// 先逐脑 Unsubscribe 再 Clear（D340 对称，防旧脑 EventBus 幽灵订阅存活导致 A3 跨轮分叉）。</summary>
    public void ResetState()
    {
        foreach (var brain in _brains.Values)
        {
            if (brain != null) brain.Unsubscribe();
        }
        _brains.Clear();
    }
}