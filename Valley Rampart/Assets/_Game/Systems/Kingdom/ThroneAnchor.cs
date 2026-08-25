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
            if (u.kingdomId != 0) continue;   // 2_17 步骤4：玩家 GameOver 只统计玩家(桶0)实体——防 AI 工人虚增保命
            var occ = u.EffectiveOccupation;
            if (occ == Occupation.Worker || occ == Occupation.Civilian)
                return true;
        }
        return false;
    }

    /// <summary>存活工人数（调试/UI 用；仅玩家桶0）。</summary>
    public int AliveWorkerCount()
    {
        if (UnitRegistry.Instance == null) return 0;
        int count = 0;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive) continue;
            if (u.kingdomId != 0) continue;   // 2_17 步骤4：仅玩家桶0，防 AI 工人计入玩家存活工人
            var occ = u.EffectiveOccupation;
            if (occ == Occupation.Worker || occ == Occupation.Civilian) count++;
        }
        return count;
    }

    // ===== GameOver 轮询（2_12 步骤8.4 / D249，替代 RulerController 君主死亡判定）=====
    // 已切换：GameOver 由本锚点"工人全灭→IsKingdomLost"驱动（8.4 落地），君主死亡不再判负
    // （D249 终审 2026-08-14；8.4 已切，注解与代码一致）。间隔轮询避免逐帧遍历单位。
    private float _pollTimer;
    private const float PollInterval = 0.5f;

    void Update()
    {
        _pollTimer -= Time.deltaTime;
        if (_pollTimer > 0f) return;
        _pollTimer = PollInterval;

        var gsm = GameStateManager.Instance;
        if (gsm == null) return;
        var s = gsm.CurrentState;
        if (s != GameState.Playing && s != GameState.Paused) return;   // 仅游戏内判定，避免菜单/加载误触发
        if (!IsKingdomLost) return;
        Debug.Log("[ThroneAnchor] 工人全灭，王国覆灭 → GameOver（D249）。");
        gsm.SetState(GameState.GameOver);
    }
}