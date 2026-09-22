using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 工人内置背包（QQQ.4 T8，需求5 资源生命周期：采集/搬运先入背包 → 搬运到仓库 → 玩家收取入国库）。
/// 携带量按资源类型（ResourceCarryConfig SO：木/石/矿=10，粮=20，水晶/火油=5，数据驱动，见 3.5 搬运携带量）。
/// 挂 Worker prefab（UnitFactory.SpawnUnit 兜底 AddComponent）；存档由 UnitController v5 代理（carriedType/carriedAmount）。
/// 背包规则：单资源类型不可混装（背包满/类型不符拒绝存储）。
///
/// ⚠️ **过渡态（`M1-A`／`09#37` · `D789` 裁 Q2=A）**：本片只做**签名适配**（随 `IWarehouse` 改多资源契约），
/// **内部仍是单资源** —— `carriedType`／`carriedAmount` 字段与其自有 API（`UnloadAll`／`IsEmpty`／`IsFull`／
/// `GetCarryCapacity`／`TryStore`）**原样保留** ⇒ `TaskScheduler` 的 7 处 `inv.carriedType` 直读**零改动**。
/// ⭐ **多资源化 ＋ 卸货路由 ＋ `UnitController` v5 存档代理（两标量→字典）⇒ 归 `M1-F`**（`09` §15.1）；
/// 「单资源语义假设点」清单见 `M1-A` 交付报告 §未完成项。
/// </summary>
public class WorkerInventory : MonoBehaviour, IWarehouse
{
    [Tooltip("当前背包资源类型（空背包=默认）")]
    public ResourceType carriedType;

    [Tooltip("当前背包资源量")]
    public int carriedAmount;

    /// <summary>背包是否为空。</summary>
    public bool IsEmpty => carriedAmount <= 0;

    /// <summary>背包是否已满（按当前资源类型容量）。</summary>
    public bool IsFull => carriedAmount >= GetCarryCapacity();

    /// <summary>当前携带容量（按背包资源类型查 ResourceCarryConfig；空背包用默认 10）。
    /// 2_20 M5/D420：×种族负重修正 carryCapMul（归属国从 UnitController 读；非单位载体/查无 → 中性）。</summary>
    public int GetCarryCapacity()
    {
        var cfg = Resources.Load<ResourceCarryConfig>("Config/ResourceCarryConfig");
        int cap = cfg != null ? cfg.GetCarryAmount(carriedType) : 10;
        var uc = GetComponent<UnitController>();
        if (uc != null)
        {
            var rd = KingdomRace.GetKingdomRaceDef(uc.kingdomId);
            if (rd != null) cap = Mathf.Max(1, Mathf.RoundToInt(cap * rd.carryCapMul));
        }
        return cap;
    }

    /// <summary>
    /// 存入资源（同类型可追加；超容量拒绝并返回实际存入量；类型不符返回 0）。
    /// </summary>
    public int TryStore(ResourceType type, int amount)
    {
        if (amount <= 0) return 0;
        if (!IsEmpty && carriedType != type) return 0;   // 单资源背包：不可混装
        int cap = GetCarryCapacity();
        int room = cap - carriedAmount;
        if (room <= 0) return 0;
        int stored = Mathf.Min(amount, room);
        carriedType = type;
        carriedAmount += stored;
        return stored;
    }

    /// <summary>清空背包并返回全部资源量（卸货/入国库用）。</summary>
    public int UnloadAll()
    {
        int amount = carriedAmount;
        carriedAmount = 0;
        return amount;
    }

    // ===== IWarehouse 实现（2_12 步骤3，移动仓库；⭐ M1-A 起为多资源契约，本类过渡态见类注释）=====

    /// <summary>存量条目：非空 ⇒ 单条（`carriedType`／`carriedAmount`）；空包 ⇒ 空列表（⛔ 非 null）。</summary>
    public List<ResourceAmount> Query()
    {
        var list = new List<ResourceAmount>(1);
        if (!IsEmpty) list.Add(new ResourceAmount(carriedType, carriedAmount));
        return list;
    }

    public bool CanTake(ResourceType t, int amt) => !IsEmpty && carriedType == t && carriedAmount >= amt;

    /// <summary>取出资源（⭐ `M1-G-1` `D824` §一-2：修为 `IWarehouse.Take` 契约的**尽力档**语义 ——
    /// 「同类型 ⇒ 尽力取（≤ 存量）」，⛔ **无 `CanTake` 前置**（旧实现 `amt > 存量` 时整笔返 0 ⇒
    /// 无法"取可入量"）。⚠️ 全库零调用（裁定已核）⇒ 改动零回归面。</summary>
    public int Take(ResourceType t, int amt)
    {
        if (amt <= 0 || IsEmpty || carriedType != t) return 0;
        int taken = Mathf.Min(amt, carriedAmount);
        carriedAmount -= taken;
        return taken;
    }

    /// <summary>入包（同名代理 `TryStore`；返回实际存入量 · 多资源签名的单资源实现）。</summary>
    public int Deposit(ResourceType t, int amt) => TryStore(t, amt);

    /// <summary>背包不加工，返回 0（签名对齐用；加工只在加工建筑）。</summary>
    public int Transform(ResourceType @in, ResourceType @out, int amt) => 0;
}
