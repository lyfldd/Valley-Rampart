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
    /// 收集当前"参与凑单"的王国仓库（存量>0 的 StorageComponent）。
    /// 与旧 GatherWarehouses 行为等价（仅定位方式不同）；结算时点调用。
    /// </summary>
    public static List<IWarehouse> GatherActive()
    {
        var result = new List<IWarehouse>(_storages.Count);
        for (int i = 0; i < _storages.Count; i++)
        {
            var s = _storages[i];
            if (s != null && s.storedAmount > 0) result.Add(s);
        }
        return result;
    }
}