using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 王国仓库注册表（2_12 步骤8.4，落 WarehouseHelper.FindObjectsOfType 的 TODO）。
/// 维护参与"王国仓库凑单"的 IWarehouse 提供者集合，替换 WarehouseHelper 内全场景扫描：
///   - StorageComponent（产能建筑本地库，存量>0 才参与）；
///   - 未来：国库仓库（随主城升级，随 8.4 国库真源切换后纳入同一集合）。
///
/// 采用"结算时点 Gather + 过滤存量>0"，与旧实现行为等价（重启后仍只取存量>0 的库），
/// 但定位从 FindObjectsOfType（全场景扫描）改为常驻注册表（O(注册数)）。
/// ⚠️ 调用频率红线沿袭 WarehouseHelper：只许结算时点用，禁入 Update/每帧路径。
///
/// 注册纪律（8.4）：StorageComponent 在 Init/OnDestroy 时 Add/Remove；
/// 真源切换完成前，本注册表与 RulerController 国库字段**并存**但只读不双写（禁双写红线）。
/// </summary>
public static class WarehouseRegistry
{
    private static readonly List<StorageComponent> _storages = new List<StorageComponent>();

    /// <summary>注册产能建筑本地库（重复注册忽略）。</summary>
    public static void Register(StorageComponent s)
    {
        if (s == null || _storages.Contains(s)) return;
        _storages.Add(s);
    }

    /// <summary>注销本地库（建筑销毁时）。</summary>
    public static void Unregister(StorageComponent s)
    {
        if (s != null) _storages.Remove(s);
    }

    /// <summary>
    /// 收集当前"参与凑单"的王国仓库（存量>0 的 StorageComponent），仅同国仓储计入。
    /// 与旧 GatherWarehouses 行为等价（仅定位方式不同）；结算时点调用。
    /// 2_17 修复卡γ：过滤改"同王国匹配"——玩家(0)结算只凑玩家仓，AI 王国结算只凑 AI 仓，
    /// 保"玩家资源绝不流入 AI 库"原始意图，同时 AI 凑单/物流可通（非补丁D的玩家专属硬过滤）。
    /// </summary>
    public static List<IWarehouse> GatherActive(int kingdomId)
    {
        var result = new List<IWarehouse>(_storages.Count);
        for (int i = 0; i < _storages.Count; i++)
        {
            var s = _storages[i];
            if (s == null || KingdomOf(s) != kingdomId) continue;
            if (s.storedAmount > 0) result.Add(s);
        }
        return result;
    }

    /// <summary>
    /// 找距 worldPos 最近的"同王国、同 resourceType 且还有余量"的 StorageComponent（搬运第二段卸货落点）。
    /// 步骤11 切替 TaskScheduler.UnloadInventory 的 FindObjectsOfType 全场景扫描（D51 就近卸货）。
    /// 2_17 修复卡γ：过滤改"同王国匹配"（第 3 参 kingdomId=工人归属国）——玩家工人只卸玩家库（绝不流入
    /// AI 库的原始意图保住），AI 工人也能卸回自己的 AI 仓库（修复缺陷γ AI 物流链断）。
    /// 返回 null=无可用仓库（调用方兜底国库）。
    /// </summary>
    public static StorageComponent FindNearestAvailable(ResourceType type, UnityEngine.Vector3 worldPos, int kingdomId)
    {
        StorageComponent best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < _storages.Count; i++)
        {
            var s = _storages[i];
            if (s == null || KingdomOf(s) != kingdomId) continue;
            if (s.resourceType != type || s.capacity <= s.storedAmount) continue;
            float d = (s.transform.position - worldPos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = s; }
        }
        return best;
    }

    /// <summary>从属建筑王国 id（StorageComponent 挂在 Building 上；未挂建筑/构建中按非玩家兜底）。</summary>
    private static int KingdomOf(StorageComponent s)
    {
        var b = s != null ? s.GetComponentInParent<Building>() : null;
        return b != null ? b.kingdomId : -1;
    }
}