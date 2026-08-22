using UnityEngine;

/// <summary>
/// 移动手感全局配置（2_3 步骤6 SO 化，继承旧 AttentionTuningConfig.arrivalThreshold 语义）。
/// 资产：Resources/Config/MovementConfig.asset。NPC 移动/PathFollower 状态机调参入口。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/MovementConfig", fileName = "MovementConfig")]
public class MovementConfig : ScriptableObject
{
    private static MovementConfig _instance;

    /// <summary>懒加载（缺资产用类默认值兜底，需在 Resources/Config 放资产）。</summary>
    public static MovementConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<MovementConfig>("Config/MovementConfig");
            return _instance;
        }
    }

    [Header("移动手感（HH.3 裁决 2026-08-22 统一 iso：格四邻步长全等 0.716/格）")]
    [Tooltip("NPC 基础速度（世界单位/秒，按 iso 步长 0.716/格重标=1格/秒基准；单位个体速度取 UnitData.walkSpeed/runSpeed，本值为全局基础）")]
    public float npcSpeed = 0.716f;

    [Tooltip("到达半径（格单位，§1.6：√((Δx/cellW)²+(Δy/cellH)²) ≤ 此值视为到达）")]
    public float arriveRadiusCells = 0.3f;

    [Header("PathFollower 状态机（2_3 步骤4，与 2_6 对齐）")]
    [Tooltip("下一路径点失效后的重寻冷却（秒）")]
    public float repathCooldownSeconds = 1.0f;

    [Tooltip("等路径超时（秒）：Pending 态等待超时上报 Failed，防寻路未回时呆立")]
    public float pendingTimeoutSeconds = 3.0f;

    [Tooltip("连续失败上限：达到则进入 Failed 并发 PathFailedEvent")]
    public int maxConsecutiveFails = 3;
}