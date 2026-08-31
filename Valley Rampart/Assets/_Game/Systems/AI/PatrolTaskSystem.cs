using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 巡逻/探路任务系统（2_8 步骤9，§5.4B D175，迷雾联动 D167/D169/D170）。
///
/// 行为规则：士兵沿指定方向/区域推进探路，视野并集实时揭开迷雾（D167），
/// 遇敌进入战斗（2_5），无威胁则持续探索。
/// 迷雾不阻塞寻路（D170）：巡逻本身="走进去才见"。
///
/// 实现：给巡逻单位注入 TaskStimulus（杂务级探索目标）驱动决策核移动；
/// 威胁刺激（ThreatStimulus 贴脸 ≥90）天然压过任务刺激 → 遇敌转战斗，敌灭后自动续巡。
/// 视野并集揭迷雾消费 2_7 VisionSystem 副作用（各单位 NPCBrain.UpdatePerception 已每 tick
/// MarkExplored，D262；本系统另按固定半径主动补标，确保探路即揭雾）。
///
/// 交互入口（框选士兵→右键未知区）归 2_13；本篇落行为规则，StartPatrol 为脚本化/debug 入口。
/// </summary>
public static class PatrolTaskSystem
{
    /// <summary>巡逻任务（一个单位沿方向持续推进探路）。</summary>
    private sealed class PatrolTask
    {
        public NPCBrain Brain;
        public UnitController Unit;
        public Vector2 Direction;      // 推进方向（世界单位）
        public Vector2 NextWaypoint;   // 当前目标点
        public float StepCells = 6f;   // 每步推进距离（格）
    }

    private static readonly List<PatrolTask> _tasks = new List<PatrolTask>();
    private static Runner _runner;

    /// <summary>每帧驱动巡逻的任务（惰性自建，无需场景挂载）。</summary>
    private sealed class Runner : MonoBehaviour
    {
        private void Update() => PatrolTaskSystem.Tick(Time.deltaTime);
    }

    private static void EnsureRunner()
    {
        if (_runner != null) return;
        var go = new GameObject("PatrolTaskSystemRunner");
        go.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<Runner>();
    }

    /// <summary>当前活跃巡逻数（调试/套件查询）。</summary>
    public static int ActiveCount => _tasks.Count;

    // ===== 脚本化/debug 入口 =====

    /// <summary>发布巡逻：指定单位沿默认方向（+X 世界推进）探路。</summary>
    public static void StartPatrol(NPCBrain brain)
    {
        StartPatrol(brain, Vector2.right);
    }

    /// <summary>
    /// 发布巡逻：指定单位沿 directionOrTarget 推进。
    /// 参数为微分/大多数时兼容两种语义：以位移（>1 格）则视为目标点取方向，否则视为方向。
    /// </summary>
    public static void StartPatrol(NPCBrain brain, Vector2 directionOrTarget)
    {
        if (brain == null) return;
        EnsureRunner();

        var unit = brain.GetComponent<UnitController>();
        if (unit == null || !unit.IsAlive) return;

        // 幂等：已有该单位的巡逻任务则复用（更新方向）
        for (int i = 0; i < _tasks.Count; i++)
        {
            if (ReferenceEquals(_tasks[i].Unit, unit))
            {
                _tasks[i].Direction = ResolveDirection(unit, directionOrTarget);
                _tasks[i].NextWaypoint = (Vector2)unit.transform.position + _tasks[i].Direction * StepToWorld(_tasks[i].StepCells);
                _tasks[i].Brain = brain;
                return;
            }
        }

        Vector2 dir = ResolveDirection(unit, directionOrTarget);
        var task = new PatrolTask
        {
            Brain = brain,
            Unit = unit,
            Direction = dir,
            NextWaypoint = (Vector2)unit.transform.position + dir * StepToWorld(6f),
        };
        _tasks.Add(task);
        Debug.Log($"[PatrolTaskSystem] 发布巡逻: 单位 {unit.npcId} 方向 {dir}");
    }

    /// <summary>按位置发布巡逻（debug 入口）：就近取 <paramref name="pos"/> 附近的己方士兵。</summary>
    public static void StartPatrol(Vector2 pos)
    {
        var brain = FindNearestPlayerBrain(pos);
        if (brain == null)
        {
            Debug.LogWarning("[PatrolTaskSystem] StartPatrol(pos) 失败：附近无己方士兵");
            return;
        }
        StartPatrol(brain, Vector2.right);
    }

    /// <summary>停止某单位的巡逻（目标改变/任务完成时调）。</summary>
    public static void StopPatrol(NPCBrain brain)
    {
        for (int i = _tasks.Count - 1; i >= 0; i--)
        {
            if (brain != null && ReferenceEquals(_tasks[i].Brain, brain))
            {
                _tasks.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>清空全部巡逻（场景切换/重载时调）。</summary>
    public static void Clear()
    {
        _tasks.Clear();
    }

    // ===== 内部推进 =====

    private static void Tick(float dt)
    {
        float now = Time.time;
        for (int i = _tasks.Count - 1; i >= 0; i--)
        {
            var t = _tasks[i];
            if (t.Brain == null || t.Unit == null || !t.Unit.IsAlive || t.Brain == null)
            {
                _tasks.RemoveAt(i);
                continue;
            }

            Vector2 pos = t.Unit.transform.position;

            // 视野并集揭迷雾（D167/D262）：主动补标，探路即见
            float cs = CellSize();
            VisionSystem.MarkExplored(pos, cs * 4f);

            float stepWorld = StepToWorld(t.StepCells);
            // 到达当前目标点 → 沿方向续推下一探路点（持续推进探索）
            if (Vector2.Distance(pos, t.NextWaypoint) < stepWorld * 0.5f)
            {
                t.NextWaypoint += t.Direction * stepWorld;
            }

            // 注入任务刺激（5 秒刷新；威胁刺激更高 → 自动遇敌转战斗）
            t.Brain.AddTaskStimulus(new TaskStimulus(
                TaskPriority.C,
                Vector2XUnity.FromUnity(t.NextWaypoint),
                intensity: 0.8f,
                expiry: now + 1f,
                issuer: _runner
            ));
        }
    }

    private static Vector2 ResolveDirection(UnitController unit, Vector2 dirOrTarget)
    {
        float cs = CellSize();
        // 位移超过 1 格：视为目标点，方向取单位→目标
        if (dirOrTarget.sqrMagnitude > cs * cs)
        {
            Vector2 d = dirOrTarget - (Vector2)unit.transform.position;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.right;
        }
        return dirOrTarget.sqrMagnitude > 1e-6f ? dirOrTarget.normalized : Vector2.right;
    }

    private static float StepToWorld(float stepCells) => stepCells * CellSize();

    private static float CellSize()
    {
        return (GridSystem.Instance != null && GridSystem.Instance.Config != null)
            ? GridSystem.Instance.Config.cellSize.x : 2.26f;
    }

    private static NPCBrain FindNearestPlayerBrain(Vector2 pos)
    {
        if (UnitRegistry.Instance == null) return null;
        NPCBrain best = null;
        float bestSq = float.MaxValue;
        var units = UnitRegistry.Instance.GetUnitsByFaction(Faction.PlayerCamp);
        if (units == null) return null;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null || !u.IsAlive) continue;
            var brain = u.GetComponent<NPCBrain>();
            if (brain == null) continue;
            float sq = ((Vector2)u.transform.position - pos).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = brain; }
        }
        return best;
    }
}