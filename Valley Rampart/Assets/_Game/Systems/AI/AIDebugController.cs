using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI 调试控制器（3.0.1 附录 A / 3.0.1_2）。
///
/// 职责：管理 NPC 选择状态 + 收集 AI 调试数据。
/// UI 面板只调用本控制器的公开方法，不直接访问 NPCBrain。
///
/// 交互流程（UI 实现）：
///   1. F1 -> ToggleDebugMode()
///   2. 点击 NPC -> TrySelectAtWorldPosition(worldPos)
///   3. 每帧调 GetSnapshot() 获取数据 -> 渲染面板
///   4. ESC -> ClearSelection() / ToggleDebugMode()
/// </summary>
public class AIDebugController : MonoBehaviour
{
    private static AIDebugController _instance;
    /// <summary>单例（非 Singleton 子类，轻量级，不 DontDestroyOnLoad）</summary>
    public static AIDebugController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<AIDebugController>();
                if (_instance == null)
                {
                    var go = new GameObject("[AIDebugController]");
                    _instance = go.AddComponent<AIDebugController>();
                }
            }
            return _instance;
        }
    }

    /// <summary>是否在调试模式（UI F1 切换）</summary>
    public bool IsDebugMode { get; private set; }

    /// <summary>当前选中的 NPC（null = 未选中）</summary>
    public IAIDebugInfo SelectedBrain { get; private set; }

    /// <summary>选中的 NPC 的 GameObject（供 UI 高亮）</summary>
    public GameObject SelectedGameObject { get; private set; }

    // 可复用的缓冲区，避免每帧 GC
    private readonly List<StimulusDebugInfo> _stimuliBuffer = new List<StimulusDebugInfo>();
    private readonly List<AISwitchRecord> _switchBuffer = new List<AISwitchRecord>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    // ===== 交互方法（UI 调用）=====

    /// <summary>切换调试模式开关。</summary>
    public void ToggleDebugMode()
    {
        IsDebugMode = !IsDebugMode;
        if (!IsDebugMode)
            ClearSelection();
    }

    /// <summary>清除当前选择。</summary>
    public void ClearSelection()
    {
        SelectedBrain = null;
        SelectedGameObject = null;
    }

    /// <summary>
    /// 在世界位置尝试选择 NPC。返回是否选中成功。
    /// UI 在鼠标点击时调用此方法，传入世界坐标。
    /// </summary>
    public bool TrySelectAtWorldPosition(Vector2 worldPos, float maxDistance = 1.5f)
    {
        var brains = FindObjectsOfType<NPCBrain>();
        NPCBrain nearest = null;
        float minDist = maxDistance;

        foreach (var brain in brains)
        {
            if (brain == null) continue;
            float dist = Vector2.Distance(brain.transform.position, worldPos);
            if (dist <= minDist)
            {
                minDist = dist;
                nearest = brain;
            }
        }

        if (nearest != null)
        {
            SelectedBrain = nearest;
            SelectedGameObject = nearest.gameObject;
            return true;
        }
        return false;
    }

    /// <summary>直接选择指定 NPC（UI 自行做 raycast 时用）。</summary>
    public void SelectNPC(IAIDebugInfo brain, GameObject go)
    {
        SelectedBrain = brain;
        SelectedGameObject = go;
    }

    // ===== 数据收集（UI 每帧调用）=====

    /// <summary>
    /// 收集选中 NPC 的全部 AI 调试数据。
    /// UI 面板每帧调用此方法，渲染返回的快照。
    /// </summary>
    public AIDebugSnapshot GetSnapshot()
    {
        var snapshot = new AIDebugSnapshot
        {
            HasSelection = SelectedBrain != null,
            TopStimuli = _stimuliBuffer,
            RecentSwitches = _switchBuffer
        };

        if (SelectedBrain == null)
        {
            _stimuliBuffer.Clear();
            _switchBuffer.Clear();
            return snapshot;
        }

        // 收集基本数据（IAIDebugInfo 接口属性）
        snapshot.NPCName = SelectedGameObject != null ? SelectedGameObject.name : "Unknown";
        snapshot.CurrentFocus = SelectedBrain.CurrentFocus;
        snapshot.CurrentSpectrum = SelectedBrain.CurrentSpectrum;
        snapshot.CurrentThreatLevel = SelectedBrain.CurrentThreatLevel;
        snapshot.NearbyEnemyCount = SelectedBrain.NearbyEnemyCount;
        snapshot.NearbyAllyCount = SelectedBrain.NearbyAllyCount;
        snapshot.HasProtection = SelectedBrain.HasProtection;
        snapshot.InSafetyConfirmation = SelectedBrain.InSafetyConfirmation;
        snapshot.IsInHitCooldown = SelectedBrain.IsInHitCooldown;

        // 收集扩展数据（IAIDebugInfo 接口方法）
        var extended = SelectedBrain as IAIDebugInfoExtended;
        if (extended != null)
        {
            snapshot.NPCPosition = extended.DebugPosition;
            snapshot.HPRatio = extended.DebugHPRatio;
            _stimuliBuffer.Clear();
            extended.GetTopStimuli(_stimuliBuffer, 5);
            _switchBuffer.Clear();
            extended.GetSwitchHistory(_switchBuffer, 5);
        }
        else
        {
            snapshot.NPCPosition = SelectedGameObject != null
                ? (Vector2)SelectedGameObject.transform.position : Vector2.zero;
            snapshot.HPRatio = 0f;
            _stimuliBuffer.Clear();
            _switchBuffer.Clear();
        }

        return snapshot;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
