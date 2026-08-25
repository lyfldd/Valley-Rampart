using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  2_16 步骤11 - 营地晋升调度（CampUpgrader，D294/D295/D306/D312/D313/D314）
//  挂 DayCycleSettlement（对齐 VagrantCampSystem.OnNewDay）：每天对活跃营地做五条件判定，
//  全满足 → KingdomFoundry.FoundFromCamp（动态立国）；冷却期/人数不足 → 日志说明原因（营地继续生长）。
//  条件（§3.4 占位，落 KingdomFoundingConfig SO）：
//    1. 营地人数 ≥ foundingThresholdVagrants(12)
//    2. 存续 ≥ foundingPersistenceDays(5)
//    3. 全局王国数 < maxKingdomsGlobal(8)（Registry.Count 含玩家 D314）
//    4. 营地中心格无主（2_17 TerritorySystem 落地前恒真；真判定接线归 2_17 步骤12）
//    5. 全局立国冷却 ≥ foundingCooldownDays(10)（冷却阻立国本身 D312；到期即立）
//  吞并出口B（D306）：营地中心格有主 → ConvertVagrantsToWorkers(owner) + 移除 Camp，立国流程终止（不产生飞地 D283）。
//    触发端（领土圈入检测）归 2_17 步骤12 接线——本片实现执行端管线 + 挂点，判定恒假则不触发。
// ============================================================================

/// <summary>营地日 tick 晋升调度（步骤11）。由 DayCycleSettlement 在每日结算末尾调用。</summary>
public static class CampUpgrader
{
    private static readonly System.Random _rng = new System.Random();   // 玩法随机（非世界生成，R4 纪律不受影响）

    /// <summary>遍历活跃营地做五条件判定/吞并，触发立国。日 tick 入口。</summary>
    public static void TickAll()
    {
        var vcs = VagrantCampSystem.Instance;
        var registry = KingdomRegistry.Instance;
        if (vcs == null || registry == null) return;
        var cfg = Resources.Load<KingdomFoundingConfig>("Config/Kingdoms/KingdomFoundingConfig");
        int currentDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;

        // 拷贝遍历：FoundFromCamp/吞并会 RemoveCamp，避免改集合中枚举
        var camps = new List<Camp>(vcs.Camps);
        for (int i = 0; i < camps.Count; i++)
        {
            var camp = camps[i];
            if (camp == null || camp.foundedFlag) continue;

            // 吞并出口B（D306）：营地中心格有主 → 转 owner 工人 + 移除，立国终止。当前判定占位恒假（2_17 接线）。
            if (TryAnnex(vcs, camp)) continue;

            var (pass, reason) = CheckConditions(camp, currentDay, registry, cfg);
            if (pass)
            {
                KingdomFoundry.FoundFromCamp(camp, _rng);
            }
            else
            {
                Debug.Log($"[CampUpgrader] 营地 ({camp.centerCell.x},{camp.centerCell.y}) 不立国：{reason}" +
                          $"（成员 {camp.memberIds.Count}/{Threshold(cfg)}，存续 {camp.persistenceDays}/{PerDays(cfg)}，" +
                          $"王国 {registry.Count}/{MaxKingdoms(cfg)}，冷却 {CooldownOk(registry, currentDay, cfg)}）");
            }
        }
    }

    /// <summary>五条件纯判定（不入单例/世界读写，编辑器冒烟可调）。返回是否全满足 + 未满足首条原因。</summary>
    public static (bool pass, string reason) CheckConditions(
        Camp camp, int currentDay, KingdomRegistry registry, KingdomFoundingConfig cfg)
    {
        if (camp == null) return (false, "营地空");
        int count = camp.memberIds != null ? camp.memberIds.Count : 0;

        if (count < Threshold(cfg)) return (false, $"人数 {count}<{Threshold(cfg)}");
        if (camp.persistenceDays < PerDays(cfg)) return (false, $"存续 {camp.persistenceDays}<{PerDays(cfg)}");
        if (registry == null) return (false, "Registry 空");
        if (registry.Count >= MaxKingdoms(cfg)) return (false, $"王国数已达上限 {MaxKingdoms(cfg)} (Count 含玩家 D314)");
        // 条件4 营地中心格无主：2_17 TerritorySystem 落地前恒真（占位；真判定接线归 2_17 步骤12）
        if (!CooldownOk(registry, currentDay, cfg)) return (false, $"冷却期未过（距上次立国需 ≥{Cooldown(cfg)} 日）");
        return (true, "五条件全满足");
    }

    /// <summary>吞并出口B 执行端（D306）：营地中心格有主 → 成员转该国工人 + 移除 Camp 记录。触发端归 2_17 步骤12；本片判定恒假。</summary>
    static bool TryAnnex(VagrantCampSystem vcs, Camp camp)
    {
        int ownerKingdomId = ResolveOwnerCampCell(camp);   // 占位：2_17 前恒 -1=无主
        if (ownerKingdomId < 0) return false;               // 无主 → 不吞并，走正常立国判定

        // 出口B：不插旗不建新国，流民并入该国工人（D306/D283 防飞地）
        KingdomFoundry.ConvertVagrantsToWorkers(camp.memberIds, ownerKingdomId);
        vcs.RemoveCamp(camp);
        Debug.Log($"[CampUpgrader] 吞并出口B（D306）：营地 ({camp.centerCell.x},{camp.centerCell.y}) 并入王国 {ownerKingdomId}（领土圈入检测归 2_17 接线）。");
        return true;
    }

    /// <summary>占位：营地中心格归属王国 id（2_17 TerritorySystem 落地前恒 -1=无主）。真判定接线归 2_17 步骤12（TerritoryChangedEvent 联动）。</summary>
    static int ResolveOwnerCampCell(Camp camp) => -1;

    // ===== SO 访问守卫（cfg 为空回退占位默认，防空引用）=====
    static int Threshold(KingdomFoundingConfig c) => c != null ? c.foundingThresholdVagrants : 12;
    static int PerDays(KingdomFoundingConfig c) => c != null ? c.foundingPersistenceDays : 5;
    static int MaxKingdoms(KingdomFoundingConfig c) => c != null ? c.maxKingdomsGlobal : 8;
    static int Cooldown(KingdomFoundingConfig c) => c != null ? c.foundingCooldownDays : 10;
    static bool CooldownOk(KingdomRegistry r, int day, KingdomFoundingConfig c) => r.CanFoundNow(day, Cooldown(c));
}