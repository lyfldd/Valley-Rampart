using System.Linq;
using UnityEngine;

/// <summary>
/// 王座/旗帜锚点（2_12 步骤5 / 设计 §5.2，D2/D49/D139/D249）。
/// 上帝视角无君主实体：王国锚点（失败判定/王国归属/怪潮目标）挂在主城建筑上，而非某个 NPC。
/// RulerController 退役后，其"君主死亡→GameOver"判定由本锚点的"工人全灭→GameOver"替代。
/// </summary>
public class ThroneAnchor : MonoBehaviour
{
    private static ThroneAnchor _instance;
    public static ThroneAnchor Instance => _instance;

    /// <summary>主城建筑引用（王座/旗帜挂载点）。</summary>
    public Building castle;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>
    /// 王国是否已失败（D249 终审，2026-08-14）：GameOver 唯一条件 = 工人全灭。
    /// 主城被破**不再判负**——主城废墟可无限重建，破城期（D164）降级为风味状态。
    /// </summary>
    public bool IsKingdomLost => !HasRemainingWorker();

    /// <summary>是否还有存活工人（Worker/Civilian 职业）。工人=最小重建单位（D49）。</summary>
    public bool HasRemainingWorker()
    {
        if (UnitRegistry.Instance == null) return true;   // 注册表未就绪时保守判断（不误判失败）
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive) continue;
            var occ = u.EffectiveOccupation;
            if (occ == Occupation.Worker || occ == Occupation.Civilian)
                return true;
        }
        return false;
    }

    /// <summary>存活工人数（调试/UI 用）。</summary>
    public int AliveWorkerCount()
    {
        if (UnitRegistry.Instance == null) return 0;
        int count = 0;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive) continue;
            var occ = u.EffectiveOccupation;
            if (occ == Occupation.Worker || occ == Occupation.Civilian) count++;
        }
        return count;
    }
}