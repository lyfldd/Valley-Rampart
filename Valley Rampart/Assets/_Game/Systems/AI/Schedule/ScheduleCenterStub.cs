using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - 调度中心占位（P0 场景验证用）
//  详见 3.0.1_2_输入输出决定层设计.md §7 / §11 P0 第8项
//  昼夜节律：夜间停发 B/C 户外任务（输入端一致性要求）
// ============================================================================

/// <summary>
/// 调度中心占位（§7 输入端一致性，P0 场景验证用）。
/// 白天派砍树任务（B级）/跟随任务（B级），夜间停发 B/C 户外任务。
/// 防止"调度中心夜间刚派活、威胁层就撤退"两系统打架。
/// P1 替换为正式调度中心。
/// </summary>
public class ScheduleCenterStub : MonoBehaviour
{
    [Header("测试用任务配置")]
    [Tooltip("测试用砍树任务位置（白天派发 B 级任务）")]
    public Transform treeTarget;

    [Tooltip("测试用部队队长（跟随任务锚点）")]
    public UnitController squadLeader;

    [Tooltip("任务派发半径（格数，范围内 NPC 才派发）")]
    public float assignRadiusCells = 10f;

    [Tooltip("砍树任务刺激强度（B 级）")]
    public float treeTaskIntensity = 2f;

    [Tooltip("跟随任务刺激强度（B 级）")]
    public float followTaskIntensity = 2f;

    private float _assignTimer;
    private const float AssignInterval = 1f;  // 派发间隔

    private void Update()
    {
        _assignTimer += Time.deltaTime;
        if (_assignTimer < AssignInterval) return;
        _assignTimer = 0f;

        // 昼夜节律：夜间停发 B/C 户外任务（§7 输入端一致性）
        bool isNight = TimeManager.Instance != null
            && (TimeManager.Instance.CurrentPhase == TimePhase.Night
                || TimeManager.Instance.CurrentPhase == TimePhase.Dusk);

        if (isNight) return;  // 夜间不派户外任务，SafetyStimulus 自动兜底

        // 白天派发任务给附近 NPC
        float cellSize = GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize : 2.26f;
        float assignWorld = assignRadiusCells * cellSize;

        // 查找范围内 NPC 并派发任务
        var npcs = FindObjectsOfType<NPCBrain>();
        foreach (var npc in npcs)
        {
            float dist = Vector2.Distance(transform.position, npc.DebugPosition);
            if (dist > assignWorld) continue;

            // 砍树任务（B 级）
            if (treeTarget != null)
            {
                // P0: 通过 TaskStimulus 注入（简化：直接调 NPCBrain 内部接口需扩展）
                // 正式实装时由调度中心管理任务生命周期，此处仅占位示意
            }

            // 跟随任务（B 级，锚点=部队队长）
            if (squadLeader != null)
            {
                // FollowStimulus 由 NPCBrain.FollowProvider 接收
                // P0: 需 NPCBrain 暴露 SetFollowAnchor 接口（见下方法）
            }
        }
    }

    /// <summary>设置跟随锚点（测试用：手动调 NPCBrain 接收跟随任务）</summary>
    public void AssignFollow(NPCBrain npc, UnitController anchor, TaskPriority priority, float intensity)
    {
        // P0: NPCBrain 需暴露 FollowProvider 访问接口
        // 正式实装时调度中心通过统一接口派发，此处仅占位
    }
}
