using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 训练系统（3.5 实施计划 P0 步骤4，数据结构先行）。
/// 职责：查询训练定义 + 执行转职（无职业 → 工人/搬运工）。
///
/// P0 占位原则：转职 = 改 occupation（数据层），NPC 站桩/训练表演后置到 AI 稳定后
/// （走 IWorkerTaskExecutor 接口，本系统不实现 NPC 行为）。
/// 职业变更写入 UnitController.RuntimeOccupation（不污染共享 UnitData SO）并随 UnitSaveData 持久化。
/// </summary>
public class TrainingSystem : Singleton<TrainingSystem>
{
    private TrainingConfig _config;
    private readonly Dictionary<string, List<TrainingDef>> _byBuilding = new Dictionary<string, List<TrainingDef>>();

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        _config = Resources.Load<TrainingConfig>("Config/TrainingConfig");
        BuildLookup();
    }

    private void BuildLookup()
    {
        _byBuilding.Clear();
        if (_config == null || _config.trainings == null) return;
        foreach (var t in _config.trainings)
        {
            if (string.IsNullOrEmpty(t.buildingId)) continue;
            if (!_byBuilding.TryGetValue(t.buildingId, out var list))
            {
                list = new List<TrainingDef>();
                _byBuilding[t.buildingId] = list;
            }
            list.Add(t);
        }
    }

    /// <summary>某训练设施可提供的全部训练项（空 = 无配置）。</summary>
    public IReadOnlyList<TrainingDef> GetTrainings(string buildingId)
    {
        if (buildingId == null || _byBuilding.Count == 0) BuildLookup();
        return _byBuilding.TryGetValue(buildingId ?? "", out var list) ? list : s_empty;
    }
    private static readonly List<TrainingDef> s_empty = new List<TrainingDef>();

    /// <summary>
    /// 执行转职（P0 数据层）：校验起职 + 金 → 扣费 → 改 occupation。
    /// 返回是否成功。NPC 视觉/站桩行为后置（IWorkerTaskExecutor）。
    /// </summary>
    public bool TryTrain(UnitController unit, TrainingDef def)
    {
        if (unit == null || unit.Data == null) return false;
        Occupation cur = unit.EffectiveOccupation;
        if (cur != def.fromOccupation)
        {
            Debug.Log($"[TrainingSystem] 转职失败：{cur} ≠ 起始职业 {def.fromOccupation}");
            return false;
        }
        if (RulerController.Instance == null || RulerController.Instance.Gold < def.costGold)
        {
            Debug.Log("[TrainingSystem] 转职失败：金币不足");
            return false;
        }
        // P1：魔法训练额外耗水晶（§10 法师/治疗师 水晶1）
        if (def.costCrystal > 0 && RulerController.Instance.GetResource(ResourceType.Crystal) < def.costCrystal)
        {
            Debug.Log("[TrainingSystem] 转职失败：水晶不足");
            return false;
        }

        // P2：将军训练限量（KingdomConfig.generalLimit，§10 将军限量 2 可配置）
        if (def.toOccupation == Occupation.General && !CanTrainGeneral())
            return false;

        RulerController.Instance.ModifyResource(ResourceType.Gold, false, def.costGold);
        if (def.costCrystal > 0)
            RulerController.Instance.ModifyResource(ResourceType.Crystal, false, def.costCrystal);
        unit.SetOccupation(def.toOccupation);
        Debug.Log($"[TrainingSystem] 转职完成：{def.fromOccupation} → {def.toOccupation}（{def.buildingId}，耗金{def.costGold} 水晶{def.costCrystal}，{def.costDays}天）");
        // P0：训练时长/队列/NPC 表演后置，固化数据结构先行。
        return true;
    }

    /// <summary>
    /// 将军训练限量校验（3.5 P2，§10 将军限量 2 可配置）。
    /// 统计当前我方将军数（Occupation.General），达到 KingdomConfig.generalLimit 则拒绝。
    /// </summary>
    private bool CanTrainGeneral()
    {
        var cfg = KingdomManager.Instance != null ? KingdomManager.Instance.Config : null;
        int limit = cfg != null && cfg.generalLimit > 0 ? cfg.generalLimit : 2;
        int count = 0;
        if (UnitRegistry.Instance != null)
        {
            foreach (var unit in UnitRegistry.Instance.GetAllUnits())
            {
                if (unit == null || unit.Data == null) continue;
                if (unit.Data.faction != Faction.Human_Player) continue;
                if (unit.EffectiveOccupation == Occupation.General) count++;
            }
        }
        if (count >= limit)
        {
            Debug.Log($"[TrainingSystem] 转职失败：将军已达上限 {limit}（当前 {count}）");
            return false;
        }
        return true;
    }
}