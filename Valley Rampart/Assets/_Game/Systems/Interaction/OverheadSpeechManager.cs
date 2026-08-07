using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  QQQ.2 T2 / DR-10 - 头顶气泡多 NPC 数量管控
//  详见 QQQ.2_NPC任务修正以及一些小问题.md §需求1
//  解决 100-200 人同时冒字一片文字海：
//   ① 视野裁剪：相机视野外 NPC 不冒字
//   ② 同时上限：视野内同时气泡 ≤ talkMaxActive(6)，超出进轮转队列
//   ③ 轮转队列：空槽按"该 NPC 距上次说话时间最久"优先补位
//   ④ 同 NPC 冷却：talkCooldown(8s) 防刷屏
//  单 NPC 触发条件（NPCBrain.TickAutoTalk 负责）：IsIdleForTask + SafetyScore>0.6 + 非 Caution。
// ============================================================================

/// <summary>头顶气泡管理器（单例）。</summary>
public class OverheadSpeechManager : Singleton<OverheadSpeechManager>
{
    private AttentionTuningConfig _config;

    private readonly List<ActiveBubble> _active = new List<ActiveBubble>();
    private readonly List<PendingSpeak> _pending = new List<PendingSpeak>();
    private readonly Dictionary<int, float> _lastSpeakTime = new Dictionary<int, float>();

    private struct ActiveBubble
    {
        public int npcId;
        public float expireAt;
    }

    private struct PendingSpeak
    {
        public int npcId;
        public string line;
        public Vector2 worldPos;
        public Transform host;
    }

    protected override void Awake()
    {
        base.Awake();
        _config = Resources.Load<AttentionTuningConfig>("Config/AttentionTuningConfig");
    }

    int MaxActive => _config != null ? Mathf.Max(1, _config.talkMaxActive) : 6;
    float Cooldown => _config != null ? _config.talkCooldown : 8f;

    /// <summary>
    /// 请求冒泡（NPCBrain 空闲自动说话 / 其他表现调）：
    /// 冷却中 / 视野外 → false（本次不冒，NPC 计时器下轮重试）；
    /// 有空槽 → 立即冒泡；满员 → 入轮转队列（等待空槽补位）。
    /// </summary>
    public bool TrySpeak(Transform host, int npcId, string line, Vector2 worldPos)
    {
        if (host == null || npcId == 0 || string.IsNullOrEmpty(line)) return false;
        float now = Time.time;

        // ④ 同 NPC 冷却（DR-10 防刷屏）
        if (_lastSpeakTime.TryGetValue(npcId, out float last) && now - last < Cooldown)
            return false;

        // ① 视野裁剪（相机视野外不冒字）
        if (!IsInView(worldPos)) return false;

        // ② 同时上限：满员入轮转队列
        if (_active.Count < MaxActive)
        {
            Grant(host, npcId, line, now);
        }
        else
        {
            _pending.Add(new PendingSpeak { npcId = npcId, line = line, worldPos = worldPos, host = host });
        }
        return true;
    }

    private void Update()
    {
        float now = Time.time;

        // 过期气泡释放（气泡 2.5s 自动消失后空出槽位）
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (now >= _active[i].expireAt) _active.RemoveAt(i);
        }

        // ③ 轮转补位：空槽按"距上次说话最久"优先（最久没说的先补上）
        while (_active.Count < MaxActive && _pending.Count > 0)
        {
            int bestIdx = 0;
            float bestLast = float.MaxValue;
            for (int i = 0; i < _pending.Count; i++)
            {
                _lastSpeakTime.TryGetValue(_pending[i].npcId, out float lt);
                if (lt < bestLast) { bestLast = lt; bestIdx = i; }
            }
            var p = _pending[bestIdx];
            _pending.RemoveAt(bestIdx);
            if (!IsInView(p.worldPos)) continue;  // 排队期间移出视野 → 放弃本轮
            Grant(p.host, p.npcId, p.line, now);
        }
    }

    void Grant(Transform host, int npcId, string line, float now)
    {
        OverheadSpeech.Show(host, line);   // 每单位复用覆盖 + 2.5s 自动消失（DR-6/T1 已实现）
        _active.Add(new ActiveBubble { npcId = npcId, expireAt = now + OverheadSpeech.BubbleDuration });
        _lastSpeakTime[npcId] = now;
    }

    /// <summary>相机视野裁剪（无主相机不裁剪）。</summary>
    bool IsInView(Vector2 worldPos)
    {
        var cam = Camera.main;
        if (cam == null) return true;
        Vector3 v = cam.WorldToViewportPoint(worldPos);
        return v.z > 0f && v.x >= -0.1f && v.x <= 1.1f && v.y >= -0.1f && v.y <= 1.1f;
    }

    /// <summary>场景重置清空（气泡/队列/说话记录）。</summary>
    public void ResetState()
    {
        _active.Clear();
        _pending.Clear();
        _lastSpeakTime.Clear();
    }
}
